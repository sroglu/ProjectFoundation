# KunaiDebugTool

## Purpose

High-performance, immediate-mode in-game debug overlay for Unity: console, commander, inspector,
profiler, system info, and bug reporter. The whole UI renders in **one draw call** with **zero
per-frame GC allocations** via a Burst-compiled vertex pipeline. Two-layer architecture:

- **UI Layer** (Phase 1): immediate-mode widget API, a deferred command buffer, the Burst pipeline,
  and a single combined shader.
- **Tools Layer** (Phase 2+): Console, Inspector, Profiler, Commander, System Info, Bug Reporter,
  and a Master toolbox — all built on top of the UI layer.

## Assemblies

| Assembly | Folder | Namespace |
|---|---|---|
| `mehmetsrl.KunaiDebugTool` | `Runtime/` | `Kunai` |
| `mehmetsrl.KunaiDebugTool.Editor` | `Editor/` | `Kunai` |
| `mehmetsrl.KunaiDebugTool.Tests` | `Tests/Runtime/` | `Kunai` |

The `mehmetsrl.KunaiDebugTool*` asmdef namespace is a sanctioned, retained exception — do not rename
it. The runtime C# namespace is `Kunai`; the static entry point is `KUI.*`.

## Dependencies

Third-party packages (runtime asmdef references):

- `Unity.Burst` — compiles the hot-path vertex jobs to native SIMD.
- `Unity.Collections` — `NativeArray` / `NativeList` / `NativeHashMap` for zero-GC data.
- `Unity.Mathematics` — `float2` / `float4` / `math.*` for Burst-compatible math.

Standalone **leaf** module — no dependency on any other PFound module. Registers directly on
`UnityEngine.Application.onBeforeRender` to fill the command buffer before the camera renders it, so
there is no framework/host dependency.

## Key Types

**Facade & core (`Runtime/Widgets`, `Runtime/Core`)**
- `KUI` — the entire static public surface (lifecycle, window registration, immediate-mode widgets).
- `KuWindow` — base class for panels; subclass, override `Title` + `OnRenderUI()`.
- `KuiContext` — owns the tick, command buffer, canvas, input, and window manager; built by
  `KUI.Initialize`.
- `KuiCommandBuffer` — deferred `KuiDrawCommand` buffer flushed once per frame.
- `KuiCanvas` — issues the flushed command buffer to the back buffer.
- `KuiOverlayRunner` — hidden `DontDestroyOnLoad` MonoBehaviour that drives the end-of-frame flush.
- `KuiWindowManager`, `KuiWindowState`, `KuiInputFocus` — window registry, per-window state,
  single-owner widget focus.

**Tool windows (`Runtime/Tools`)**
- `KuiConsoleWindow`, `KuiCommanderWindow`, `KuiInspectorWindow`, `KuiProfilerWindow`,
  `KuiSystemInfoWindow`, `KuiMasterWindow` — the built-in panels.
- `KuiTouchOverlay` — touch/mouse indicator ring (drawn last, not a window).
- `KuiBugReporterWindow` — editor-side bug reporter panel.

**Logging (`Runtime/Tools/Console`)**
- `KuiLogger` — static logging facade that mirrors to both the Unity Console and the overlay.
- `KuiLog` — tag-bound `readonly struct` logger returned by `KuiLogger.For<T>()` / `For(tag)`.
- `KuiConsole` — static entry that hooks Unity log capture (`KuiConsole.Initialize`).

**Attributes (`Runtime/Tools`)**
- `KuiOptionAttribute` (`[KuiOption]`), `KuiRangeAttribute` (`[KuiRange]`) — Inspector tweakables.
- `KuiCommandAttribute` (`[KuiCommand]`) — Commander REPL commands.
- `KuiCategoryAttribute` (`[KuiCategory]`) — shared grouping tag for Inspector + Commander.

**Bug reporting (`Runtime/Tools/BugReporter`)**
- `KuiBugReport` — init-only report DTO. `IBugReportSink` — the sink contract.
- `KuiBugReporter` — static sink registry + dispatch.

**Assets / helpers**
- `KuiIcons` — string constants for ~40 BMP-range Nerd Font glyphs.
- `KuiSettings` — runtime configuration (`KUI.Settings`), incl. `EnableTouchIndicator`.
- `KuiTextFieldState`, `KuiScrollHandle` — value-type widget state.

## Public API

### `KUI` facade

**Lifecycle**
- `KUI.Initialize(Texture2D fontAtlas, TextAsset fontMetrics)` — build + install the overlay.
- `KUI.Shutdown()` — tear it all down.
- `KUI.IsVisible` (get/set), `KUI.Settings`, `KUI.LastTickDurationNs`.

**Windows**
- `KUI.RegisterWindow(KuWindow)`, `KUI.UnregisterWindow(KuWindow)`.

**Widgets** — valid only inside a `KuWindow.OnRenderUI()`:

| Widget | API | Returns |
|--------|-----|---------|
| Label | `KUI.Label(text)` | void |
| Rect | `KUI.Rect(w, h, color)` | void |
| Button | `KUI.Button(text)` | bool (true on click) |
| Toggle | `KUI.Toggle(value, label)` | bool (new state) |
| Slider | `KUI.Slider(value, min, max)` | float (new value) |
| Separator | `KUI.Separator()` | void |
| Scroll | `KUI.BeginScroll(ref pos)` / `KUI.EndScroll(ref pos, h)` | `KuiScrollHandle` |
| Group | `KUI.BeginGroup()` | IDisposable |
| **TextField** (Phase 2) | `KUI.TextField(int id, ref KuiTextFieldState s, float width=0)` | bool (true on Enter) |
| **ChipStrip** (Phase 2) | `KUI.ChipStrip(int id, IReadOnlyList<string> labels, IList<bool> states, ref Vector2 scroll, float h=0)` | int (toggled index, -1 if none) |
| **Collapsible** (Phase 2) | `KUI.BeginCollapsible(int id, string title, ref bool expanded)` / `KUI.EndCollapsible()` | bool (renders children when true) |

`KuiTextFieldState` is a value-type with a fixed 256-char backing buffer, public `GetText()` /
`SetText(string)` / `Clear()`. Initialise with `default`.

### Console entry

`KuiConsole.Initialize(int capacity = 5000)` hooks Unity log events; then
`KUI.RegisterWindow(new KuiConsoleWindow())` mounts the console UI.

### Phase 2 public APIs

The higher-level surfaces (`KuiLogger` / `KuiLog` / Verbose, the `[KuiOption]`/`[KuiRange]` inspector
attributes, the `[KuiCommand]` commander attribute, `IBugReportSink`/`KuiBugReport`, the Master
toolbox, and the touch indicator) are documented in [Phase 2 APIs](#phase-2-apis) below.

## Setup / wiring

The overlay is **self-hosting** — you do NOT place any GameObject or prefab in a scene.
`KUI.Initialize(...)`:

1. builds the `KuiContext` (command buffer, canvas, input, window manager),
2. subscribes the context tick to `Application.onBeforeRender` (fills the command buffer before the
   camera renders), and
3. spawns a hidden `DontDestroyOnLoad` GameObject (`[KunaiOverlayRunner]`, `HideAndDontSave`) that
   issues the overlay's `CommandBuffer` at `WaitForEndOfFrame` — so the overlay composites on top of
   every uGUI / UI Toolkit panel.

Render-pipeline hookup (Built-in vs URP / HDRP / any SRP) is detected at runtime — no pipeline
package reference is required.

```csharp
using Kunai;

// once, at startup (e.g. a bootstrap MonoBehaviour.Awake)
KUI.Initialize(fontAtlasTexture, fontMetricsTextAsset);   // your baked font atlas + metrics asset
KuiConsole.Initialize();                                  // optional: capture Unity logs
KUI.RegisterWindow(new KuiConsoleWindow());               // mount a tool window

// author your own window
public sealed class MyDebug : KuWindow
{
    public override string Title => "Debug";
    public override void OnRenderUI()
    {
        KUI.Label("Hello");
        if (KUI.Button("Reset")) ResetGame();
    }
}
KUI.RegisterWindow(new MyDebug());
```

Toggle the overlay at runtime with the `` ` `` or F1 key (or the top-left double-tap on touch), or
programmatically via `KUI.IsVisible`. Call `KUI.Shutdown()` to tear it all down.

### Required project configuration

1. **Shader in Always Included Shaders.** Add `Hidden/KUI-Combined` to
   *Project Settings → Graphics → Always Included Shaders*. It has no scene/material reference and
   would otherwise be stripped from a player build; `KUI.Initialize` asserts it can be found and
   throws (`Shader.Find` returns null) if it is missing.
2. **Legacy input must be enabled.** *Player → Active Input Handling* must be `Input Manager (Old)`
   or `Both`. Kunai reads keyboard / mouse / touch via the legacy `UnityEngine.Input` API. Under
   `Input System Package (New)` **only**, legacy `Input` is fully disabled — the toggle keys
   (`` ` `` / F1), the top-left double-tap, and touch indicators all silently no-op (the overlay
   still renders if you set `KUI.IsVisible = true` programmatically). Changing this setting requires
   an Editor restart to swap the input backend.
3. **A `Camera.main` must exist.** Layout and the ortho projection are driven by
   `Camera.main.pixelWidth`/`pixelHeight`, so a camera tagged `MainCamera` must be present.

## File Structure

```
Runtime/
├── Core/        — DrawCommand, Vertex, CommandBuffer, Context, Canvas, KuiInputFocus
├── Pipeline/    — LayerSortJob, VertexCountJob, PrefixSumJob, VertexWriteJob
├── Font/        — Glyph, FontAtlas, BmFontParser, Icons
├── Input/       — InputState (incl. TouchDelta), InputHandler
├── Layout/      — Layout engine, ClipStack
├── Reflection/  — KuiReflectionScanner + Result (Phase 2 shared infra)
├── Widgets/     — KUI facade, Button, Toggle, Slider, Separator, ScrollView,
│                  TextField, ChipStrip, CollapsibleSection
├── Windows/     — KuWindow (+ ShowInMasterToggle), WindowState, WindowManager (+ IsDragging)
├── Tools/       — Phase 2 windows
│   ├── Console/    KuiConsole, ConsoleWindow, LogBuffer (+ CollapseConsecutive),
│   │               LogEntry (+ Category), CategoryParser, CategoryRegistry, KuiLogger
│   ├── Profiler/   FrameSampler, FrameGraphRenderer, ProfilerWindow
│   ├── Inspector/  KuiOption + KuiRange attrs, OptionEntry, OptionRegistry, OptionRow, InspectorWindow
│   ├── Commander/  KuiCommand + KuiCategory attrs, CommandEntry, CommandRegistry,
│   │               CommandParser, CommandHistory, CommanderWindow
│   ├── SystemInfo/ SystemInfoWindow (+ DumpAsText)
│   ├── BugReporter/ KuiBugReport + IBugReportSink, KuiBugReporter (sink registry),
│   │               BugReporterWindow
│   ├── Touch/      KuiTouchOverlay (not a window)
│   └── Master/     KuiMasterWindow (toolbox)
├── DPI/         — DPI detection and scaling
├── Settings/    — Configurable settings (+ EnableTouchIndicator)
├── Styles/      — Color palette constants
├── Shaders/     — KUI-Combined.shader
└── AssemblyInfo.cs — InternalsVisibleTo Tests + IsExternalInit polyfill

Editor/          — bug reporter + editor tooling
Tests/Runtime/   — NUnit Editor-mode (UNITY_INCLUDE_TESTS)
                   KuiTextField, ReflectionScanner, CategoryParser, LogBufferCollapse,
                   FrameSampler, OptionRegistry, CommandParser, CommandRegistry,
                   BugReportSink.
```

## Downstream Dependents

None within PFound — this is a standalone leaf module. The canonical external consumer is the
**Playnest** project (see [Examples in the wild](#examples-in-the-wild)).

## Limitations / Known Gaps

- **Still missing widgets**: Dropdown, ProgressBar, multi-line TextField, image-blit widgets,
  theming.
- **BMP-only Unicode**: `TextChars` is `NativeArray<char>` (UTF-16). Codepoints above U+FFFF
  (Material Design Nerd Font icons `nf-md-*` start at `U+F0001`) need surrogate pairs and are **not
  supported** — use BMP equivalents (`nf-fa-microchip` instead of `nf-md-cpu_64_bit`). To add SMP
  support: change `TextChars` to `NativeArray<int>` codepoints and decode surrogates at `PushLabel`
  time.
- **Render pipeline support**: Built-in, URP, HDRP. A single `Camera.main` is the render target on
  every pipeline; multi-camera or render-texture overlays are not supported. Adding either: split
  the render hook to attach to multiple cameras (Built-in) or filter `endCameraRendering`
  differently (SRP).
- **No gradients / rounded corners**: vertex-colored axis-aligned rects + oriented thin quads
  (lines) only — same atlas, same shader, same single draw call. No textures other than the font
  atlas (brutalist aesthetic).
- **Main thread only**: all `KUI.*` calls must be on the main thread. Logging via
  `Application.logMessageReceivedThreaded` is the one exception — capture is lock-free, registry
  insert deferred to `Drain` on main.
- **Font atlas external**: must be pre-baked with a BMFont-compatible tool (see `bake/bake.sh`).
- **Reflection scan once**: Inspector / Commander walk the AppDomain at first window open.
  Late-loaded asmdefs (Addressables, dynamic DLLs) need a re-scan API (not yet exposed; close
  `Toolbox`/`Inspector` and re-open as a workaround).
- **Async commands not supported**: `[KuiCommand]` methods returning `Task` / `UniTask` are skipped.
  Wrap in a sync facade for now.
- **Bug-reporter sinks**: the contract says "MUST NOT throw". The pipeline catches anyway and logs
  via `KuiLogger.Error(..., "BugReporter")`, but a misbehaving sink is a bug in your sink, not in
  Kunai.

## Architecture

```
Frame: Update → ReadInput → BeginFrame → Windows.OnRenderUI → EndFrame → Flush (1 draw call)

Pipeline: DrawCommands → LayerSort → VertexCount → PrefixSum → VertexWrite → Mesh Upload → GPU
```

Widget calls push `KuiDrawCommand` structs into a deferred command buffer; GPU work happens once at
flush. Windows subclass `KuWindow` and override `Title` + `OnRenderUI()`; tool windows are prebuilt
panels layered on the same UI API.

### Key design decisions

- **Deferred command buffer**: widget calls push `KuiDrawCommand` structs; GPU work only at Flush.
- **Per-command ClipRect**: embedded at push time for parallel vertex writing.
- **Sentinel UV (-1,-1)**: a single shader distinguishes solid rects from font glyphs.
- **Vertex-based text scaling**: the font atlas is baked once; DPI changes multiply vertex positions.
- **Render hookup**: `Tick` runs at `BeforeRender` (PreLateUpdate) and only *fills* a single
  `CommandBuffer`; `KuiCanvas.ExecuteOnBackBuffer` issues it from the overlay runner's
  `WaitForEndOfFrame` coroutine via `Graphics.ExecuteCommandBuffer` against the back buffer. The
  buffer carries its own explicit ortho ViewProjection, so it needs no camera state and composites
  after uGUI / UI Toolkit panels. Pipeline is detected at runtime via
  `GraphicsSettings.currentRenderPipeline`; `RenderPipelineManager` ships in `UnityEngine.Rendering`,
  so no URP / HDRP package reference is required.
- **Two-tier glyph cache**: ASCII (codepoint < 128) goes into a `NativeArray<KuiGlyph>(128)` for
  O(1) access; Unicode codepoints (Nerd Font icons) go into a `NativeParallelHashMap<int, KuiGlyph>`
  (Burst-compatible). `VertexWriteJob` takes the array branch first, falls back to hashmap, then to
  the `'?'` glyph.
- **Camera-authoritative resolution**: `Camera.main.pixelWidth`/`pixelHeight` drive the ortho
  projection and layout, not `Screen.width`/`height`. In Editor Game View these can differ; using the
  camera's pixel rect keeps the overlay aligned with the actual render target.

## Phase 2 APIs

### Logging — `KuiLogger`

**One API. Every call mirrors to BOTH the Unity Console and the Kunai overlay.** Designed
lightweight: zero parsing, single allocation per categorised line, none for uncategorised.

```csharp
using Kunai;

KuiLogger.Info("UI booted");                          // uncategorised
KuiLogger.Info("connected",         "Network");        // category = Network
KuiLogger.Warn("retrying",          "Network");
KuiLogger.Error("invalid token",    "Auth");
KuiLogger.Exception("rpc failed", ex, "Network");
```

Methods are decorated with `[HideInCallstack]` so the Unity Console "Open in IDE" jumps to your call
site, not into `KuiLogger.cs`.

#### Recommended pattern: per-class tagged logger

For systematic logging across a project, prefer the per-class `KuiLog` struct over hand-typing the
category at every call site:

```csharp
public class HomeScreen
{
    static readonly KuiLog Log = KuiLogger.For<HomeScreen>();

    void OnTilePressed() => Log.Info("tile clicked");
    void OnError(string why) => Log.Error(why);
}
```

`KuiLogger.For<T>()` captures `typeof(T).Name` once (truncated to the 31-char category limit) and
returns a tiny `readonly struct` (zero allocation per call). Every method on `KuiLog` forwards to the
corresponding `KuiLogger` method with the bound tag as category — same `[HideInCallstack]`, same
dual-mirror, same parser-bypass.

Why prefer this:

- **One source of truth** — IDE rename of the class also renames the tag (it's `typeof(T).Name`).
- **Per-class chip filter granularity** in the Kunai Console — isolate a single class's logs.
- **Terse call sites** — `Log.Info("...")` reads cleaner than the flat form repeated dozens of times.
- **Cost** — same as `KuiLogger.Info(msg, "Cat")` (one `string.Concat`, one direct enqueue, no
  parser, no boxing).

`KuiLogger.For(string tag)` exists for the rare case where the tag is decided at runtime (e.g. a
sub-system name passed in via constructor). For class-level tags, always prefer the generic overload.

The flat `KuiLogger.Info(msg, "Cat")` form is fine for one-off logs that don't justify a per-class
field. Don't mix the two for the same class — pick one and stick with it.

#### Verbose — Kunai overlay only, define-gated

```csharp
KuiLogger.Verbose("frame budget = 16.6ms");           // uncategorised
KuiLogger.Verbose("retrying...", "Network");          // categorised
Log.Verbose($"player at {transform.position}");       // per-class struct
```

Verbose is for high-frequency / fine-grain debug noise you want available **in the Kunai overlay**
but **never in the Unity Console**.

1. **Console-blind.** `Verbose(...)` does NOT call `UnityEngine.Debug.Log*`. The entry only lands in
   the Kunai buffer. The chip strip + the toolbar `Verbose` toggle in the console window control
   visibility.
2. **`KUNAI_VERBOSE` define-gated.** Both `KuiLogger.Verbose` and `KuiLog.Verbose` are decorated with
   `[Conditional("KUNAI_VERBOSE")]`. When the symbol is **undefined**, the compiler removes the
   entire call expression at the call site — including the argument expressions. So
   `Log.Verbose($"x = {expensiveCalc()}")` in a release build with the symbol undefined incurs
   **zero cost**.

Define `KUNAI_VERBOSE` in the project's Scripting Define Symbols
(Player Settings → Other Settings → Scripting Define Symbols) for builds where you want the verbose
stream live. Typical setup:

- **Editor + Development Build**: `KUNAI_VERBOSE` defined → verbose lands in the Kunai overlay.
- **Release Build**: `KUNAI_VERBOSE` not defined → verbose call sites disappear at compile time.

To enable verbose in a single test file, put `#define KUNAI_VERBOSE` at the very top of that file —
the symbol then applies only to that compilation unit.

#### Auto-capture — also supported, but **slower**

`UnityEngine.Debug.Log*` calls (your existing code, third-party packages, Unity itself) are still
captured via `Application.logMessageReceivedThreaded` and surface in the Kunai overlay automatically.
No refactor required. If the message starts with `[Foo]`, the prefix is parsed and the category lands
as `Foo`. This auto-capture path is **strictly heavier** than the direct `KuiLogger` path:

| Path | Per-call cost | When to use |
|---|---|---|
| `KuiLogger.Info("msg")` | 1× `Debug.Log` + 1× direct enqueue. Zero string concat, zero parser. | Default for any new code |
| `KuiLogger.Info("msg", "Cat")` | 1× `string.Concat("[Cat] msg")` + 1× `Debug.Log` + 1× direct enqueue. Parser never runs. | Default for categorised logs |
| `KuiLogger.Verbose(…)` / `Log.Verbose(…)` | Defined: 1× direct enqueue, no Console mirror. Undefined: entire call site removed by compiler. | High-frequency debug noise that should never reach the Unity Console |
| `Debug.Log("[Cat] msg")` | 1× `Debug.Log` + hook → `KuiCategoryParser` (scan) + substring → 2–3 allocs | Tolerated for legacy / 3rd-party; don't write new calls this way |
| `Debug.Log("plain")` | 1× `Debug.Log` + hook fast path (no prefix, no parse) | Existing code OK; new code prefer `KuiLogger.Info("msg")` |

#### Other things to know about cost

- **Stack trace** — Unity captures a stack trace for every `Debug.Log*`. For `Info` in production
  builds you may want to disable globally:
  ```csharp
  Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
  ```
- **No re-entry duplicate** — `KuiLogger` sets a `[ThreadStatic]` guard around its `Debug.Log`
  mirror; the capture hook drops the echo, so Console + overlay each get exactly one entry.
- **Category cap** — overflow tracked but not displayed in v1 (32 distinct names per session is the
  soft target).

### Inspector — `[KuiOption]` / `[KuiRange]` / `[KuiCategory]`

```csharp
[KuiOption,                          KuiCategory("Cheats")] public static bool  InfiniteAmmo;
[KuiOption("Speed"), KuiRange(0,20), KuiCategory("Cheats")] public static float PlayerSpeed = 5f;
[KuiOption,                          KuiCategory("Cheats")] public static GameMode Mode;
```

Static fields/properties only. Supported types: `bool` / `int` / `float` / `double` / `string` /
`enum`. Renderer: bool→Toggle, numeric+Range→Slider, numeric→stepper, enum→cycle button,
string→read-only Label. The reflection scan runs once when `KuiInspectorWindow` first renders.

### Commander — `[KuiCommand]`

```csharp
[KuiCommand("set-volume", help: "0..1")] public static void SetVolume(float v) => AudioListener.volume = v;
[KuiCommand("teleport"), KuiCategory("Dev")] public static string Teleport(int zone) => $"At {zone}";
```

Static methods only. Parameter types: `string` / `int` / `float` / `double` / `bool` / `enum`. REPL
prompt → tokenize (quotes + `\`-escape) → bind → invoke → print return value (or `OK` for void).
Up/Down history (50 entries), Tab completes the head token to the longest common prefix.

### Bug Reporter — `IBugReportSink` / `KuiBugReport`

```csharp
public sealed class HttpSink : IBugReportSink
{
    public void Send(KuiBugReport report) { /* upload report.ScreenshotPng etc. — MUST NOT throw */ }
}
KuiBugReporter.RegisterSink(new HttpSink());   // typically once at app start
```

The Capture button writes 4 files to
`Application.persistentDataPath/KunaiBugReports/<timestamp>/`: `screenshot.png`, `console.txt`,
`system.txt`, `description.txt`. Every registered sink receives the same in-memory `KuiBugReport`.
Sinks may run on a background thread; exceptions are logged via `KuiLogger.Error(..., "BugReporter")`
and swallowed.

### Master toolbox — `KuiMasterWindow` + `KuWindow.ShowInMasterToggle`

Drop-in window that lists every other registered `KuWindow` whose `ShowInMasterToggle` is true and
renders a Toggle that drives `IsVisible`. State persists in PlayerPrefs
(`Kunai.Master.Vis.<title-ascii>`). Override `ShowInMasterToggle => false` on always-on panels
(Console does this).

### Touch indicator — `KuiSettings.EnableTouchIndicator`

Opt-in ring at the active touch / mouse position. Editor-only `Ctrl+Space+P` raycasts the
EventSystem, selects + pings the topmost UI GameObject in the hierarchy.

## Tools

| Tool | Class | Notes |
|------|-------|-------|
| Console | `KuiConsoleWindow` | log capture (auto-detect `[Foo]` prefix) + chip filter + search + collapse-duplicates + pin / Cmd+C copy. Pinned by default — does not appear in the master toolbox. |
| Profiler | `KuiProfilerWindow` | Tick-aggregated sampler (per-frame accumulate → flush at slider Hz). Readouts: avg+min FPS, avg+min ms, avg GC KB/f, draw-call hint (Editor only via `UnityStats`). Two-line graph in FPS units (cool-blue avg, red-orange min = bottleneck), 60 FPS cap line, ≈ 4 s of history at any Hz. One Refresh-Hz slider drives both graph commit cadence AND text refresh. `MaxAcceptedFrameSec = 0.1f` clamp keeps editor focus-loss / mobile pause-resume megaframes from swamping the Y-scale. |
| Inspector | `KuiInspectorWindow` | `[KuiOption]` static field/property tweaker, grouped by `[KuiCategory]`. |
| Commander | `KuiCommanderWindow` | `[KuiCommand]` REPL with Tab completion + Up/Down history + isolated output pane. |
| System Info | `KuiSystemInfoWindow` | 5-section read-only data dump (Hardware/OS/Graphics/App/Runtime) + Refresh; `DumpAsText()` static helper for the bug-report payload. |
| Bug Reporter | `KuiBugReporterWindow` | TextField + Capture → 4-file dump + `IBugReportSink` dispatch + Open-folder button + clickable Console hyperlink. |
| Touch overlay | `KuiTouchOverlay` (not a window) | Ring + crosshair drawn last in `KuiContext.TickInner`. Editor-only Ctrl+Space+P UI ping. |
| Toolbox | `KuiMasterWindow` | On/off control surface for all of the above; PlayerPrefs persistence. |

## Lifecycle gotchas

- **Tick after Dispose**: event listeners may be invoked from a snapshot taken before `KUI.Shutdown`
  ran the deregister. `KuiContext.Tick` and `KuiCommandBuffer.BeginFrame` defensively check
  `Commands.IsCreated` and bail out — needed in Editor when entering/exiting Play Mode rapidly.
  Don't remove these guards.
- **Window registration order**: `KuiContext.RegisterWindow` calls `Initialize()` *before* the state
  is added to `_windowStates`, so the window's `WindowRect` setter correctly populates `State.Rect`.
  Reversing this order silently zeroes the rect (rect (0,0,0,0) → VertexCountJob returns 0 → panel
  doesn't render but text labels still emit at origin — this was a real bug).
- **`KuWindow.Shutdown`**: override on windows that own native containers (e.g. `KuiProfilerWindow`
  releases `KuiFrameSampler` arrays). `KuiContext.Dispose` walks every registered window and calls
  `Shutdown()` once.
- **Master toolbox order**: register `KuiMasterWindow` FIRST so its first-frame PlayerPrefs apply
  runs at iteration 0 of `TickInner`. Otherwise other windows render with their default `IsVisible`
  for one frame before the master overrides them.
- **Soft-keyboard ownership**: `KuiTextField` opens / closes `TouchScreenKeyboard` based on
  `KuiInputFocus`. Two TextFields can never be focused at once — the focus model is single-owner.
  Don't poll `Input.inputString` in your own widgets while inside Kunai's frame; it'll fight the
  TextField's consumer pass.

## Icons (`Kunai.KuiIcons`)

Public string constants for ~40 BMP-range Nerd Font glyphs (Font Awesome subset): `Check`, `Cross`,
`Warning`, `Info`, `Bug`, `Cpu`, `Memory`, `Timer`, `Cog`, chevrons, arrows, etc. Concat with text:

```csharp
KUI.Label(KuiIcons.Bug + " null ref at line 42");
```

Adding more icons: edit `bake/chars.txt`, re-run `bake/bake.sh`, optionally add a constant to
`KuiIcons.cs`. The code path doesn't change — the parser auto-loads anything in the `.fnt`.

## Font baking (`bake/`)

`bake/bake.sh` invokes [fontbm](https://github.com/vladimirgamalyan/fontbm) (build from source on
macOS, no brew tap). Inputs: `bake/chars.txt` (UTF-8 text file, one char per codepoint to bake) +
a `.ttf` next to the script. Outputs: `Assets/GameSpecific/KunaiDebugTool/IosevkaKunai.{fnt.txt,png}`.

BMFont format quirks the parser handles:
- `info size=-N` (negative = pixel units, positive = points) — parser takes `abs(N)`.
- ASCII fast-path keeps `id < 128` in the array; everything else goes into the unicode hashmap.
- `.fnt` is renamed to `.fnt.txt` post-bake so Unity recognizes it as a `TextAsset`.

## Examples in the wild

Kunai is currently used as a git submodule (not a UPM package), so the `Samples~/` convention doesn't
apply. Instead, the **Playnest** project (`github.com/sroglu/Playnest`) is the canonical reference for
active usage:

| Concern | Reference |
|---|---|
| One-time wiring (font load, window registration) | `Assets/Shell/Scripts/KunaiBootstrap.cs` |
| Editor / Dev-build gating + call site | `Assets/Shell/Scripts/AppBootstrapper.cs` (the `KunaiBootstrap.Initialize()` block inside `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`) |
| Per-class `KuiLog` field + `Log.Info / Warn / Error` calls | `Assets/Shell/Scripts/Screens/HomeScreen.cs`, `…/Frames/BadgeGalleryFrame.cs`, `Assets/MiniGames/SumoBumo/SumoBumoLobbyModule.cs` etc. |
| Static-class workaround (string overload) | `Assets/Shell/Scripts/UI/ShellLabelBinder.cs`, `…/LocalizationBootstrap.cs` — both use `KuiLogger.For(nameof(X))` because static types can't be generic args (CS0718) |
| `KUNAI_VERBOSE` Standalone-only define setup | `ProjectSettings/ProjectSettings.asset` — `Standalone: …;KUNAI_VERBOSE`, iOS / Android tabs intentionally don't carry it |

When this module gains a second consumer or graduates to a UPM package, fold the equivalent into
proper `Samples~/` scenes.

## Testing

`Tests/Runtime/` is an NUnit Editor-mode suite (`UNITY_INCLUDE_TESTS`, asmdef
`mehmetsrl.KunaiDebugTool.Tests`) with fixtures: `KuiTextFieldTests`, `KuiReflectionScannerTests`,
`KuiCategoryParserTests`, `KuiLogBufferCollapseTests`, `KuiFrameSamplerTests`,
`KuiOptionRegistryTests`, `KuiCommandParserTests`, `KuiCommandRegistryTests`,
`KuiBugReportSinkTests`.
