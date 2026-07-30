# ProjectFoundation — Coding Style

Conventions for PFound code (a pure-C# foundation framework with a thin Unity layer).
Tracked and shipped with the code — unlike `docs/`, which is private planning.

Read §1 before touching any `.cs` file; the rest is reference.

---

## 1. Null discipline — fail fast, never defend

**Nothing in our own code should be null.** Do not anticipate a value being null and
write defensive code around it. If something that shouldn't be null *is* null, let the
`NullReferenceException` throw at the access site — that throw is the bug signal we want,
and we actively support it. Defensive guards hide the real lifecycle/wiring bug; the loud
crash points straight at it.

### Forbidden — defensive handling of our own state

- ❌ `_field ??= new Foo();` — lazy init via null-coalescing. Initialize the field eagerly
  (field initializer or ctor) so it is never null.
  → we do: `private readonly EventManager _events = new EventManager();` (`World.Events`).
- ❌ `_field?.Method();` on our own state — if it can be null mid-call, the lifecycle is wrong.
- ❌ `if (_field != null)` as an "is it initialized?" check — initialize deterministically instead.
- ❌ `if (binding.Target == null) return;` defensive skips on our own data — let it throw.
  A binding is valid by construction; if it isn't, fix the lifecycle, don't paper over it.
- ❌ `null` as a sentinel for a valid state. Use an explicit representation.
  → we do: `SystemBase.SceneScoped` (a `bool`) + a non-null array, never `ActiveScenes == null`.

### Allowed — these throw, or read a genuine *external* optional

- ✅ **Actively throwing** on bad input: `ArgumentNullException` on public-API args. That *is*
  "let it throw," with a clearer message. Encouraged at the library's public boundary.
- ✅ **Fail-fast access**: `World.Get<T>` throws when the component is absent — no silent default.
- ✅ A genuine optional from an **external contract**: a reflection lookup that legitimately
  returns null (`attr?.Value ?? default`), a Unity API that returns null, an async/asset load
  that can fail. That null is expected and external — not our state.
- ✅ **Get-or-create** in a dynamic keyed structure: a null array slot meaning "not created
  yet" (e.g. `World.Pool<T>()` lazily creating a component pool). This is an absent-marker by
  design, not defensive null-handling, and cannot be pre-initialized.
- ✅ **Unity-Object liveness** (Unity layer / samples only): a `UnityEngine.Object` ref can
  become "fake-null" when the object is `Destroy`ed. Checking it answers "is the bound object
  still alive?", an external boundary — but prefer coupling the entity's lifetime to the
  object's (destroy the entity with the object) over a per-frame null guard.

### Rule of thumb

"Did I forget to initialize / wire this?" → lifecycle bug, let it throw.
"Did this *external* call find what I asked for?" → boundary check is the right answer.

---

## 2. Prefer plain C# over Unity/MonoBehaviour

It's a Unity project — Unity is there and fine to use. The guideline is simply: **reach for
plain C# first; use Unity types only where you actually need them.** Plain classes/structs are
testable without the editor, cheaper, and predictable, and most logic (rules, state, compute,
scheduling) needs nothing from Unity. No assembly-level split is required — this is about the
default you reach for, not project structure.

Use `MonoBehaviour` / `UnityEngine` only for what genuinely requires the engine: inspector
wiring (`[SerializeField]`), holding a Unity component ref, serialization, or a lifecycle Unity
itself must drive.

**Lifecycle alone is not such a reason.** Init / update / destroy can be plain public methods
the owning object calls — as `World` calls `SystemBase.OnInitialize` / `Execute`, and the host
calls `World.Update`. Reach for a `MonoBehaviour` only when Unity must be the *driver* (the
entry point it instantiates and ticks), and keep that shell thin — forward
`Awake`/`Update`/`OnDestroy` into plain methods.

---

## 3. try/catch only at external boundaries

`try/catch` is not control flow. The throwable thing must be **external** to our process —
network, file/IO, an OS call, async cancellation, or a reflection/loader failure. If the only
thing that can throw is our own NRE / IndexOutOfRange / InvalidCast, don't wrap it — fix the
bug or let it crash.

- ✅ `catch (ReflectionTypeLoadException e) { types = e.Types; }` around `Assembly.GetTypes()`
  (`SystemDiscovery`) — partial-load is an external loader failure.
- ❌ `try { ... } catch { /* ignore */ }` — silent swallow turns bugs into mysteries.
- ❌ `try/catch` around our own code "to be safe."

Test harness code (asserting that something throws) is exempt — that's the test's purpose.

---

## 4. Fail-fast at boundaries

- Don't pre-validate with `Debug.Assert(x != null)` at every method entry — access it; the NRE
  is the report.
- Validate **public-API arguments** explicitly (`ArgumentNullException`, `ArgumentException`)
  with a clear message — that is the contract boundary.
- Surface invariant violations loudly; never silent-skip.

---

## 5. Zero-alloc hot paths

This is a perf-sensitive framework. The per-frame path (`World.Update` → systems →
`Query<...>().ForEach`) must allocate **0 managed bytes** after warmup (verified:
`GC.GetAllocatedBytesForCurrentThread()` delta = 0 over 1000 `Update()` calls).

- Cache `ForEach`/callback delegates in a field (init once); never allocate a closure per
  frame. A capture-free lambda is compiler-cached; a capturing one is not — see
  `ColorPulseSystem` caching its `RefAction` in `OnInitialize`.
- Structs for query builders / results / masks; no LINQ, no boxing, no per-call collections on
  the hot path.
- Build/sort schedules once and cache; invalidate only on structural change.

---

## 6. Centralize cross-cutting behavior

When the same behavior appears at a second call site, extract one shared method and route both
through it — don't copy-paste-and-tweak. A fix then lands in one place for every caller.
Example: `SystemDiscovery` is the single source of truth for "is this a registrable system,"
used by both the editor generator and the reflection oracle.

---

## 7. Verify before "done"

- Core: `csc -nologo -warn:0 -out:/tmp/pf_ecs.exe Assets/PFound/ECS/Runtime/*.cs Assets/PFound/ECS/Tests/*.cs && mono /tmp/pf_ecs.exe` → `failed=0`.
- Unity: console clean (errors + warnings), and a visual/behavior check for anything that runs.
- Hot-path changes: re-confirm GC = 0.
