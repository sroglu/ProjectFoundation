# KunaiDebugTool — the zero-allocation render path

> Deep dive companion to [MODULE.md](MODULE.md). Explains **how** the overlay draws a full debug UI
> — console, profiler, inspector, commander — in **one draw call** with **no per-frame managed
> allocation**, exactly where the guarantee comes from, and exactly where it does *not* hold.

The short version: there is no single trick. It is a chain of decisions, one per layer, and
breaking any single link (one `$"..."` in one custom window) is enough to put the GC back on the
frame budget. This document walks the chain end to end.

---

## 0. What a conventional Unity debug overlay allocates

| Source | Per-frame cost |
|---|---|
| `$"FPS: {fps}"` / `string.Format` / `+` concat | a fresh `string` every frame, plus `float.ToString` internals |
| `Debug.Log`-style formatting | same, plus a stack trace string |
| uGUI `Text` / TMP | mesh rebuild + canvas re-batch on every character change |
| Unity IMGUI (`GUILayout.*`) | `GUIContent`, `GUIStyle` lookups, `params` arrays, boxing of value types |
| A `GameObject` per widget | retained hierarchy, `Transform` churn, layout-group rebuilds |
| `Dictionary<char, Glyph>` glyph lookup | managed, and unreachable from Burst |
| `mesh.vertices = array` | a managed array copy on every set **and** every get |

Kunai replaces all seven. Below, each replacement.

---

## 1. Layer one — no strings are ever produced

`Runtime/Core/KuiTextBuilder.cs`

`KuiTextBuilder` is a **struct** that writes into a caller-supplied `char[]`. `KUI.Text()` hands
out a builder over a single shared static buffer:

```csharp
// Runtime/Widgets/KUI.cs:85
static readonly char[] s_textBuffer = new char[1024];
public static KuiTextBuilder Text() => new KuiTextBuilder(s_textBuffer);
```

So the canonical zero-GC label is:

```csharp
KUI.Label(KUI.Text().Add("FPS: ").Add(fps).Add(" / ").Add(ms).Add(" ms"));
KUI.Label("FPS: ", fps);          // convenience overload, same path (KUI.cs:108-111)
```

What makes it allocation-free:

- **Integers** — `AppendInt` writes base-10 digits into `stackalloc char[20]`, then copies them out
  in reverse. Stack memory; nothing reaches the GC heap. No `int.ToString()`.
- **Floats** — `AppendF2` is fixed-point: `(long)(v * 100.0 + 0.5)`, then integer part, `'.'`, two
  digits. No `ToString("F2")`, no culture lookup, no `NumberFormatInfo`.
- **Literals** — `Append(string)` copies char by char into the buffer. The literal itself is
  interned at compile time and already on the heap; nothing new is created.
- **Bools** — `value ? "true" : "false"`, two interned literals.
- **The fluent chain** — `Add(...)` returns `KuiTextBuilder` **by value**, not an interface. A
  struct returned by value never boxes. The running `_len` flows through the chain while the
  backing `char[]` is written in place.
- **Overflow is dropped, not grown** — every write is guarded by `_len < _buf.Length`. A builder
  can never trigger a reallocation; worst case, text is truncated at 1024 chars.

Consequences the caller must respect:

1. **Build and consume in the same expression.** The buffer is shared — the next `KUI.Text()`
   call reuses it. Never store a builder across frames or across widgets.
2. **`KuiTextBuilder` is the only sanctioned way to build dynamic text.** This is the one place
   where the guarantee is contagious: a custom window that writes `$"..."` puts the allocation
   back, and nothing in the pipeline can undo that.

---

## 2. Layer two — immediate mode, so there is no retained widget state

Widgets are not objects. There is no `KuiButton` instance, no `GameObject`, no `Transform`. A
widget call runs, emits draw commands, and returns a value:

```csharp
if (KUI.Button("Reload")) { ... }
value = KUI.Slider(value, 0f, 1f);
```

Where state is genuinely needed it is a **value type owned by the caller**:

- `KuiTextFieldState` — fixed 256-char backing buffer, initialised with `default`, passed by `ref`.
  Editing a text field never allocates a string; the field renders straight from the `char[]` via
  the `KUI.Label(char[], count)` overload (`KUI.cs:66`).
- `KuiScrollHandle` — value type returned by `KUI.BeginScroll`.

200 widgets on screen therefore cost **zero** heap objects. Compare against uGUI, where 200 widgets
is 200 GameObjects with components, layout groups, and a canvas rebuild whenever anything moves.

---

## 3. Layer three — draw commands live in native memory

`Runtime/Core/KuiCommandBuffer.cs`

The frame's UI is recorded into two persistent native containers:

```csharp
Commands  = new NativeList<KuiDrawCommand>(cap, Allocator.Persistent);
TextChars = new NativeList<char>(charCap,     Allocator.Persistent);
```

`KuiDrawCommand` (`Runtime/Core/KuiDrawCommand.cs`) is a 64-byte
`[StructLayout(LayoutKind.Explicit, Size = 64)]` value type — `Rect`, packed colour, type tag,
text offset/length, clip rect, layer, thickness. Blittable, Burst-readable, GC-invisible.

Two details matter more than they look:

- **A label command stores no string reference.** `PushLabel` copies the characters into the
  shared `TextChars` native list and records `(TextOffset, TextLength)`
  (`KuiCommandBuffer.cs:60-90`). There are `string` and `char[]` overloads — both copy, neither
  retains. This is what lets the vertex job run in Burst: it never sees a managed reference.
- **`BeginFrame()` calls `Clear()`, not a reallocation.** `NativeList.Clear` resets length and
  keeps capacity. After the first few frames the containers reach their working-set size and never
  allocate again — the steady state is genuinely zero, native side included.

Colour packing avoids a conversion allocation too:
`UnsafeUtility.As<Color32, uint>(ref color)` — a reinterpret, not a copy.

---

## 4. Layer four — the Burst pipeline

`Runtime/Pipeline/` — four jobs, all `[BurstCompile]`, in a classic GPU-style shape:

| Stage | File | What it does |
|---|---|---|
| 1. Sort | `LayerSortJob.cs` | 3-bucket counting sort by `Layer` (background / content / overlay), `Allocator.Temp` scratch. O(n), stable, no comparison sort. |
| 2. Count | `VertexCountJob.cs` | `IJobParallelFor`; how many vertices each command needs. Rect = 4 or **0 if fully clipped**; Label = `TextLength * 4`; Line = 4 or 0 by bounding-box cull. |
| 3. Prefix sum | `PrefixSumJob.cs` | Exclusive scan → per-command write offset + grand total. Single-threaded by nature. |
| 4. Write | `VertexWriteJob.cs` | `IJobParallelFor` with `[NativeDisableParallelForRestriction]` — every command writes its own disjoint slice of the persistent vertex array, in parallel, no locks. |

**Why this is the structural core of the guarantee:** Burst-compiled code *cannot touch managed
memory at all*. Once the hot path is inside these four jobs, "zero GC" stops being a discipline you
have to maintain and becomes a property the compiler enforces. Anything that allocates simply would
not compile.

Supporting decisions inside the jobs:

- **Glyph lookup without a `Dictionary`.** ASCII is a flat `NativeArray<KuiGlyph>[128]` indexed
  directly by char; everything above 127 goes to a `NativeParallelHashMap<int, KuiGlyph>`, with
  `'?'` as the miss fallback (`VertexWriteJob.cs:209-211`). Both live in `KuiFontAtlas` as
  `Allocator.Persistent`, built once at load.
- **Per-glyph clipping happens in the vertex writer, not on the CPU-side layout.** Each glyph quad
  is intersected with the clip rect and its UVs are `lerp`-ed to match
  (`VertexWriteJob.cs:217-241`), so a half-scrolled line of text is cut mid-glyph correctly — with
  no extra draw call, no stencil, no `RectMask2D`.
- **Culled geometry costs zero vertices, not a degenerate quad.** `VertexCountJob` returns 0 for a
  fully-clipped rect, so a 1000-item scroll list with 20 visible rows only pays for 20.
  (Exception: `'\n'` / `'\r'` emit a zero-area quad, because stage 2 already reserved
  `TextLength * 4` slots before knowing the content.)
- **Y-flip in the shader-free path.** `ScreenHeight - y` is applied at vertex write time, so no
  matrix work per frame beyond the single ortho projection.

---

## 5. Layer five — one mesh upload, one draw call

`Runtime/Core/KuiCanvas.cs`

```csharp
var meshDataArray = Mesh.AllocateWritableMeshData(1);
...
Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, _mesh,
      MeshUpdateFlags.DontRecalculateBounds
    | MeshUpdateFlags.DontValidateIndices
    | MeshUpdateFlags.DontNotifyMeshUsers);
```

- **`AllocateWritableMeshData` instead of `mesh.vertices = ...`.** The classic property path copies
  a managed array in both directions on every access. The writable-mesh-data API writes straight
  into the mesh's native vertex buffer.
- **The three `MeshUpdateFlags`** strip the bounds recalculation, the index validation pass, and
  the change notification — all pure overhead for an overlay that is already in screen space and
  whose indices are generated by construction.
- **Index buffer is generated once.** `RebuildIndices` fills the standard `0,1,2 / 0,2,3` quad
  pattern and is only re-run when `EnsureCapacity` doubles the buffers. Vertex/index arrays are
  `Allocator.Persistent` and survive the frame.
- **Growth is amortised doubling**, and it logs when it happens (`KuiCanvas.cs:148`) so a
  pathological window that keeps growing the buffer is visible rather than silent.

### One shader for everything

`Runtime/Shaders/KUI-Combined.shader` handles rects, lines and text in a single pass. The
discriminator is a **sentinel UV**:

```hlsl
float isRect   = step(i.uv.x, -0.5);
float texAlpha = tex2D(_MainTex, i.uv).a;
float alpha    = lerp(texAlpha, 1.0, isRect);
```

Solid geometry is emitted with `UV = (-1, -1)` (`VertexWriteJob.cs:26`), which the fragment shader
reads as "colour only, skip the atlas". Text uses real atlas UVs. Same vertex format, same
material, same texture → **the entire UI batches into one `DrawMesh`**.

Lines are the same story: a line is an oriented quad, four corners offset perpendicular to the
segment by half-thickness, clipped with Liang-Barsky before the quad is built
(`VertexWriteJob.cs:71-140`). No line topology, no second pass.

### Where it composites

`KuiContext` subscribes its tick to `Application.onBeforeRender` (`KuiContext.cs:64`) to fill the
command buffer, and `KuiOverlayRunner` — a hidden `HideAndDontSave` / `DontDestroyOnLoad`
MonoBehaviour — issues the `CommandBuffer` at `WaitForEndOfFrame`. The command buffer carries its
own ortho `SetViewProjectionMatrices`, so it draws straight to the back buffer with no camera state
and no render-pipeline package reference. That is why the overlay sits on top of uGUI *and* UI
Toolkit, on Built-in *and* any SRP.

---

## 6. Layer six — the tools on top stay in budget

- **Log buffer** (`Runtime/Tools/Console/LogBuffer.cs`) — a preallocated `KuiLogEntry[]` ring with
  `_head`/`_count` modular indexing, plus a `ConcurrentQueue` for off-thread ingest that the main
  thread drains once per frame. Overwriting the oldest entry decrements its level counter so the
  totals stay honest. `GetAt` returns `ref KuiLogEntry` — no struct copy per row.
- **Collapse runs** are computed as `RunSpan` value structs over the ring, not by building new
  collapsed strings.
- **Numbers in the profiler / system info windows** go through `KUI.Text()`, never interpolation.

---

## 7. The allocation ledger — an honest accounting

"Zero allocation" is a precise claim and it deserves precise scope. What is actually true:

### Genuinely zero, steady state

| Path | Status |
|---|---|
| Managed (GC) heap, render pipeline | **0 B/frame** — nothing on the path can allocate; Burst enforces it |
| Managed heap, six of the seven built-in windows | **0 B/frame**, provided text goes through `KUI.Text()` |
| Command buffer / text char pool | Persistent, `Clear()`-reused; grows once to working set, then never |
| Vertex + index buffers | Persistent; reallocate only on capacity doubling |
| Glyph caches | Built once at load |

### Not zero — known and accepted

1. **Per-frame `Allocator.TempJob` native arrays.** `KuiCanvas.GenerateAndFlush` allocates
   `commands`, `vertexCounts`, `vertexOffsets`, `totalVertexCount` and `textChars` every frame
   (`KuiCanvas.cs:60-72`), plus `Mesh.AllocateWritableMeshData`. These are **native**, not managed
   — they produce no GC pressure and no collection spike, but the literal phrase "zero allocation"
   is only exact for the managed heap. The accurate claim is: *no managed allocation, therefore no
   GC spike*. Closing even this is possible (hoist the five arrays to `Persistent` and grow them
   with the same doubling policy as the vertex buffer) but has not been done.
2. **The Inspector boxes.** `[KuiOption]` reads live values through `Func<object>`, which boxes
   every value type it touches, and `enum.ToString()` allocates. This is the price of the
   one-attribute design and is **deliberate** — see the rationale in MODULE.md's *Purpose*
   section. It only allocates while a developer has that window open, and the alternatives (a
   tagged union of six `Func<T>`/`Action<T>` pairs, or a generic `KuiOptionEntry<T>` hierarchy with
   IL2CPP/AOT-safe delegates built from reflection) heavy the design substantially for a tool that
   never runs on a shipping hot path.
3. **The measurement is informal.** The recorded perf gate (CHANGELOG, *Perf gate (informal
   observation, T093)*) reads **1–13 KB/f with all 7 windows visible**, attributed largely to
   automation-plumbing logs rather than the overlay. There is **no automated GC-delta test** in
   `Tests/Runtime/`. The cheapest way to turn the claim into a fact:

   ```csharp
   // sketch: assert the render path is managed-allocation-free
   var before = GC.GetTotalMemory(false);
   for (int i = 0; i < 120; i++) DriveOneOverlayFrame();
   Assert.AreEqual(before, GC.GetTotalMemory(false));
   ```

   or a `Unity.PerformanceTesting` `[Test, Performance]` with `GC.Alloc` sampled via
   `ProfilerRecorder`.
4. **Diagnostics allocate on purpose.** `EnsureCapacity` logs an interpolated warning on resize.
   Once per doubling, never in steady state — acceptable.

---

## 8. Rules for custom windows

The guarantee is only as strong as the weakest window. When subclassing `KuWindow`:

- ✅ `KUI.Label(KUI.Text().Add("Score: ").Add(score))`
- ✅ `KUI.Label("Score: ", score)`
- ❌ `KUI.Label($"Score: {score}")` — allocates a string every frame
- ❌ `KUI.Label("Score: " + score)` — same, plus boxing
- ❌ `KUI.Label(value.ToString("F2"))` — allocates; use `.Add(value)` (fixed 2 decimals)
- ❌ LINQ (`.Where(...).ToList()`) inside `OnRenderUI` — iterators + list allocate every frame
- ❌ `foreach` over a non-struct-enumerator collection — boxes the enumerator
- ❌ caching a `KuiTextBuilder` across frames — the shared buffer is reused; build and consume
- ⚠️ text longer than 1024 chars is silently truncated; use your own `char[]` + the
  `KuiTextBuilder(char[])` constructor + `KUI.Label(char[], count)` if you need more

---

## 9. Prior art, and why this combination is rare

It is worth being clear that the *architecture* is not novel — it is the standard immediate-mode
GUI design, and it has existed in C++ for years:

- **Dear ImGui** is exactly this shape: an immediate-mode API recording into a draw-command list,
  flushed into one vertex buffer against one font atlas. Kunai's contribution is not the idea; it
  is fitting that model onto Unity's job system, Burst, and the writable-mesh-data API.
- **Unity IMGUI** is the opposite design — `GUIContent`/`GUIStyle`/boxing make it a garbage
  generator, which is why it is editor-only in practice.
- **Existing in-game debug assets** (SRDebugger, Graphy, in-game console packages) are almost all
  built on uGUI, and inherit its allocation and batching behaviour.

So why is there not a well-known zero-GC Unity equivalent? The reasons are economic more than
technical:

1. **The APIs are recent.** Burst and `Unity.Collections` landed around 2019;
   `Mesh.AllocateWritableMeshData` in 2020.1. This module could not have been written in 2018.
2. **"A debug tool is not on the hot path"** — and for most projects that is true. The case where
   it matters is narrow but real: profiling a 60 FPS mobile build with an overlay that itself
   causes GC spikes means measuring the instrument, not the subject.
3. **Abandoning uGUI means writing everything uGUI gave you for free.** In this module that is a
   BMFont parser and atlas (`Runtime/Font/`), a layout engine and clip stack (`Runtime/Layout/`),
   input routing and single-owner focus (`Runtime/Input/`), scrolling, text editing, DPI scaling —
   roughly the entire surface. That is an unreasonable amount of infrastructure to write for a
   debug tool if you are shipping a product on a schedule.
4. **The discipline is contagious to the consumer.** A library that demands `KUI.Text().Add(...)`
   instead of `$"..."` is imposing a constraint on its users. Most library authors will not sell
   that trade; a first-party internal framework can simply require it.

The result is not magic. It is the absence of a shortcut at any of six consecutive layers.

---

## Appendix — the frame, end to end

```
Application.onBeforeRender
  └─ KuiContext.Tick                                    (KuiContext.cs:139)
       ├─ CommandBuffer.BeginFrame()                    Clear(), capacity retained
       ├─ Layout.BeginFrame(screenW, screenH)
       ├─ window.OnRenderUI()  ×N                       widgets → PushRect / PushLabel / PushLine
       │    └─ KUI.Text() → KuiTextBuilder              chars into a shared char[1024]
       │         └─ PushLabel copies chars → NativeList<char>, records (offset, len)
       └─ Canvas.GenerateAndFlush                       (KuiContext.cs:259)
            ├─ LayerSortJob        [Burst]  3-bucket counting sort
            ├─ VertexCountJob      [Burst, parallel]  per-command vertex count, cull → 0
            ├─ PrefixSumJob        [Burst]  exclusive scan → offsets + total
            ├─ VertexWriteJob      [Burst, parallel]  disjoint slice writes, per-glyph clip
            └─ UploadAndDraw                AllocateWritableMeshData → one DrawMesh

WaitForEndOfFrame
  └─ KuiOverlayRunner → Graphics.ExecuteCommandBuffer   composites over uGUI / UI Toolkit
```

Tick cost is measured into `KUI.LastTickDurationNs` (`KuiContext.cs:146-155`) — the module's own
budget check, surfaced by the profiler window.
