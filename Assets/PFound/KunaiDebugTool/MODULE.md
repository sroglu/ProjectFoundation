# KunaiDebugTool

High-performance debug overlay for Unity. Renders the entire UI in **1 draw call** with **zero per-frame GC allocations** using a Burst-compiled vertex pipeline.

## Purpose

Runtime debug tool for game developers and QA. Two-layer architecture:
- **UI Layer** (Phase 1): Immediate-mode API, deferred command buffer, Burst pipeline, single shader
- **Tools Layer** (Phase 2+): Console, Inspector, Profiler, Commander built on top of UI

## Namespace

`Kunai` — static API via `KUI.*`

## Dependencies

- `Unity.Burst` — compile hot-path jobs to native SIMD
- `Unity.Collections` — NativeArray, NativeList, NativeHashMap for zero-GC data
- `Unity.Mathematics` — float2, float4, math.* for Burst-compatible math
- `PFound.LoopScheduler` — `BeforeRender` callback to fill the cmd buffer before the camera renders it

## Project Setup Requirements

**Active Input Handling must be `Input Manager (Old)` or `Both`.** Kunai's input handler reads keyboard / mouse / touch via the legacy `UnityEngine.Input` API (`Input.GetKeyDown(...)`, `Input.GetTouch(...)`). If your project's `Edit → Project Settings → Player → Active Input Handling` is set to `Input System Package (New)` only, **legacy `Input` is fully disabled** — toggle keys (`` ` `` / F1), the top-left double-tap, and touch indicators will all silently no-op. The overlay still renders if you set `KUI.IsVisible = true` programmatically, but you can't open or close it from input.

Pick one:
- `Input Manager (Old)` — works out of the box, no extra package.
- `Both` — works alongside the `com.unity.inputsystem` package; tiny overhead from running two backends, acceptable for editor-only / dev-build scenarios where the package is wanted for gameplay.
- `Input System Package (New)` only — **not currently supported**. A future migration would gate the input layer behind `#if ENABLE_INPUT_SYSTEM` and add `com.unity.inputsystem` as an optional reference; not on the roadmap.

Changing Active Input Handling **requires an Editor restart** for Unity to swap the input backend.

## Quick Start

```csharp
using Kunai;

// 1. Initialize (once, at startup)
KUI.Initialize(fontAtlasTexture, fontMetricsTextAsset);

// 2. Create a window
public class MyDebug : KuWindow
{
    public override string Title => "Debug";
    public override void OnRenderUI()
    {
        KUI.Label("Hello");
        if (KUI.Button("Click")) Debug.Log("Clicked!");
    }
}

// 3. Register
KUI.RegisterWindow(new MyDebug());
```

## Architecture

```
Frame: Update → ReadInput → BeginFrame → Windows.OnRenderUI → EndFrame → Flush (1 draw call)

Pipeline: DrawCommands → LayerSort → VertexCount → PrefixSum → VertexWrite → Mesh Upload → GPU
```

### Key Design Decisions

- **Deferred command buffer**: Widget calls push DrawCommand structs, GPU work only at Flush
- **Per-command ClipRect**: Embedded at push time for parallel vertex writing
- **Sentinel UV (-1,-1)**: Single shader distinguishes solid rects from font glyphs
- **Vertex-based text scaling**: Font atlas baked once, DPI changes multiply vertex positions
- **Render hookup**: `Tick` runs at `BeforeRender` (PreLateUpdate) and only *fills* a single `CommandBuffer`. Two pipelines, same buffer:
  - **Built-in RP**: buffer is attached to `Camera.main` via `AddCommandBuffer(CameraEvent.AfterEverything)`. Camera executes it during its render pass.
  - **URP / HDRP / any SRP**: buffer is executed in a `RenderPipelineManager.endCameraRendering` callback (gated to `Camera.main`) via `ScriptableRenderContext.ExecuteCommandBuffer`. No URP / HDRP package reference required — `RenderPipelineManager` ships in `UnityEngine.Rendering`.
  - Pipeline is detected at runtime via `GraphicsSettings.currentRenderPipeline`; switching pipelines mid-session re-installs the correct hook on the next frame.
- **Two-tier glyph cache**: ASCII (codepoint < 128) goes into a `NativeArray<KuiGlyph>(128)` for O(1) array access; Unicode codepoints (Nerd Font icons) go into a `NativeParallelHashMap<int, KuiGlyph>` (Burst-compatible). VertexWriteJob takes the array branch first, falls back to hashmap, then to `'?'` glyph.
- **Camera-authoritative resolution**: `Camera.main.pixelWidth/Height` drive the ortho projection and layout, not `Screen.width/height`. In Editor Game View these can differ (Game View resolution ≠ Editor window size); using the camera's pixel rect keeps the overlay aligned with the actual render target.

## Widgets

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
| **Collapsible** (Phase 2) | `KUI.BeginCollapsible(int id, string title, ref bool expanded)` / `KUI.EndCollapsible()` | bool (rendered children when true) |

`KuiTextFieldState` is a value-type with a fixed 256-char backing buffer, public `GetText()` / `SetText(string)` / `Clear()`. Initialise with `default`.

## Phase 2 Public APIs

### Logging — `KuiLogger`

**One API. Every call mirrors to BOTH the Unity Console and the Kunai overlay.** Designed lightweight: zero parsing, single allocation per categorised line, none for uncategorised.

```csharp
using Kunai;

KuiLogger.Info("UI booted");                          // uncategorised
KuiLogger.Info("connected",         "Network");        // category = Network
KuiLogger.Warn("retrying",          "Network");
KuiLogger.Error("invalid token",    "Auth");
KuiLogger.Exception("rpc failed", ex, "Network");
```

Methods are decorated with `[HideInCallstack]` so the Unity Console "Open in IDE" jumps to your call site, not into `KuiLogger.cs`.

#### Recommended pattern: per-class tagged logger

For systematic logging across a project, prefer the per-class `KuiLog` struct over hand-typing the category at every call site:

```csharp
public class HomeScreen
{
    static readonly KuiLog Log = KuiLogger.For<HomeScreen>();

    void OnTilePressed() => Log.Info("tile clicked");
    void OnError(string why) => Log.Error(why);
}
```

`KuiLogger.For<T>()` captures `nameof(T)` once and returns a tiny `readonly struct` (zero allocation per call). Every method on `KuiLog` forwards to the corresponding `KuiLogger` method with the bound tag as category — same `[HideInCallstack]`, same dual-mirror, same parser-bypass.

Why prefer this:

- **One source of truth** — IDE rename of the class also renames the tag (it's `nameof(T)`).
- **Per-class chip filter granularity** in the Kunai Console — isolate a single class's logs without grepping.
- **Terse call sites** — `Log.Info("...")` reads cleaner than `KuiLogger.Info("...", "HomeScreen")` repeated dozens of times.
- **Cost** — same as `KuiLogger.Info(msg, "Cat")` (one `string.Concat`, one direct enqueue, no parser, no boxing).

`KuiLogger.For(string tag)` exists for the rare case where the tag is decided at runtime (e.g. a sub-system name passed in via constructor). For class-level tags, always prefer the generic overload.

The flat `KuiLogger.Info(msg, "Cat")` form is fine for one-off logs that don't justify a per-class field (utility methods, static factories, throw-away diagnostic code). Don't mix the two for the same class — pick one and stick with it.

#### Verbose — Kunai overlay only, define-gated

```csharp
KuiLogger.Verbose("frame budget = 16.6ms");           // uncategorised
KuiLogger.Verbose("retrying...", "Network");          // categorised
Log.Verbose($"player at {transform.position}");       // per-class struct
```

Verbose is for high-frequency / fine-grain debug noise that you want available **in the Kunai overlay** but **never in the Unity Console** (which is shared with the rest of your team's compile errors, third-party warnings, etc.).

Two key properties:

1. **Console-blind.** `Verbose(...)` does NOT call `UnityEngine.Debug.Log*`. The entry only lands in the Kunai buffer. Filter the chip strip + the toolbar `Verbose` toggle in the console window controls visibility.

2. **`KUNAI_VERBOSE` define-gated.** Both `KuiLogger.Verbose` and `KuiLog.Verbose` are decorated with `[Conditional("KUNAI_VERBOSE")]`. When the symbol is **undefined**, the C# compiler removes the entire call expression at the call site — including the argument expressions. So `Log.Verbose($"x = {expensiveCalc()}")` in a release build with the symbol undefined incurs **zero cost** (no string interpolation, no method invocation, no `expensiveCalc()` call).

Define `KUNAI_VERBOSE` in the project's Scripting Define Symbols (Player Settings → Other Settings → Scripting Define Symbols) for builds where you want the verbose stream live. Typical setup:

- **Editor + Development Build**: `KUNAI_VERBOSE` defined → verbose lands in Kunai overlay.
- **Release Build**: `KUNAI_VERBOSE` not defined → verbose call sites disappear at compile time.

To enable verbose in a single test file (instead of project-wide), put `#define KUNAI_VERBOSE` at the very top of that file — the symbol applies only to that compilation unit.

#### Auto-capture — also supported, but **slower**

`UnityEngine.Debug.Log*` calls (your existing code, third-party packages, Unity itself) are still captured via `Application.logMessageReceivedThreaded` and surface in the Kunai overlay automatically. No refactor required. If the message starts with `[Foo]`, the prefix is parsed and the category lands as `Foo`.

This auto-capture path is **strictly heavier** than the direct `KuiLogger` path:

| Path | Per-call cost | When to use |
|---|---|---|
| `KuiLogger.Info("msg")` | 1× `Debug.Log` + 1× direct enqueue. **Zero string concat, zero parser.** | ✅ Default for any new code |
| `KuiLogger.Info("msg", "Cat")` | 1× `string.Concat("[Cat] msg")` + 1× `Debug.Log` + 1× direct enqueue. **Parser never runs.** | ✅ Default for categorised logs |
| `KuiLogger.Verbose(…)` / `Log.Verbose(…)` | **`KUNAI_VERBOSE` defined**: 1× direct enqueue, no Console mirror. **Undefined**: entire call site removed by compiler (zero cost). | ✅ High-frequency debug noise that should never reach the Unity Console |
| `Debug.Log("[Cat] msg")` | 1× `Debug.Log` + hook fires → `KuiCategoryParser` (regex/scan) + substring → 2–3 allocs | ⚠️ Tolerated for legacy / 3rd-party. **Don't write new calls in this form** — use `KuiLogger.Info("msg", "Cat")` instead. |
| `Debug.Log("plain")` | 1× `Debug.Log` + hook fast path (no prefix, no parse) | ✅ Existing code OK; new code prefer `KuiLogger.Info("msg")` for `[HideInCallstack]` cleanup |

#### Other things to know about cost

- **Stack trace** — Unity captures a stack trace for every `Debug.Log*` (default behaviour). For `Info` in production builds you may want to disable globally:
  ```csharp
  Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
  ```
- **No re-entry duplicate** — `KuiLogger` sets a `[ThreadStatic]` guard around its `Debug.Log` mirror; the capture hook drops the echo, so Console + overlay each get exactly one entry.
- **Category cap** — overflow tracked but not displayed in v1 (32 distinct names per session is the soft target).

### Inspector — `[KuiOption]` / `[KuiRange]` / `[KuiCategory]`

```csharp
[KuiOption,                          KuiCategory("Cheats")] public static bool  InfiniteAmmo;
[KuiOption("Speed"), KuiRange(0,20), KuiCategory("Cheats")] public static float PlayerSpeed = 5f;
[KuiOption,                          KuiCategory("Cheats")] public static GameMode Mode;
```
Static fields/properties only. Supported types: `bool` / `int` / `float` / `double` / `string` / `enum`. Renderer: bool→Toggle, numeric+Range→Slider, numeric→stepper, enum→cycle button, string→read-only Label. Reflection scan runs once when KuiInspectorWindow first renders.

### Commander — `[KuiCommand]`

```csharp
[KuiCommand("set-volume", help: "0..1")] public static void SetVolume(float v) => AudioListener.volume = v;
[KuiCommand("teleport"), KuiCategory("Dev")] public static string Teleport(int zone) => $"At {zone}";
```
Static methods only. Parameter types: `string` / `int` / `float` / `double` / `bool` / `enum`. REPL prompt → tokenize (quotes + `\`-escape) → bind → invoke → print return value (or `OK` for void). Up/Down history (50 entries), Tab completes head token to longest common prefix.

### Bug Reporter — `IBugReportSink` / `KuiBugReport`

```csharp
public sealed class HttpSink : IBugReportSink
{
    public void Send(KuiBugReport report) { /* upload report.ScreenshotPng etc. — MUST NOT throw */ }
}
KuiBugReporter.RegisterSink(new HttpSink());   // typically once at app start
```
Capture button writes 4 files to `Application.persistentDataPath/KunaiBugReports/<timestamp>/`: `screenshot.png`, `console.txt`, `system.txt`, `description.txt`. Every registered sink receives the same in-memory `KuiBugReport`. Sinks may run on a background thread; exceptions are logged via `KuiLogger.Get("BugReporter")` and swallowed.

### Master toolbox — `KuiMasterWindow` + `KuWindow.ShowInMasterToggle`

Drop-in window that lists every other registered `KuWindow` whose `ShowInMasterToggle` is true and renders a Toggle that drives `IsVisible`. State persists in PlayerPrefs (`Kunai.Master.Vis.<title-ascii>`). Override `ShowInMasterToggle => false` on always-on panels (Console does this).

### Touch indicator — `KuiSettings.EnableTouchIndicator`

Opt-in ring at the active touch / mouse position. Editor-only `Ctrl+Space+P` raycasts the EventSystem, selects + pings the topmost UI GameObject in the hierarchy.

## Phase 2 Tools

| Tool | Class | Notes |
|------|-------|-------|
| Console | `KuiConsoleWindow` | log capture (auto-detect `[Foo]` prefix) + chip filter + search + collapse-duplicates + pin / Cmd+C copy. Pinned by default — does not appear in master toolbox. |
| Profiler | `KuiProfilerWindow` | Tick-aggregated sampler (per-frame accumulate → flush at slider Hz). Readouts: avg+min FPS, avg+min ms, avg GC KB/f, draw-call hint (Editor only via `UnityStats`). Two-line graph in FPS units (cool-blue avg, red-orange min = bottleneck), 60 FPS cap line, ≈ 4 s of history at any Hz. One Refresh-Hz slider drives both graph commit cadence AND text refresh; lower Hz makes per-sample bars wider (graph fills full width at every Hz), startup right-aligns drawn samples so they don't stretch across the empty left side. `MaxAcceptedFrameSec = 0.1f` clamp keeps editor focus-loss / mobile pause-resume megaframes from swamping the Y-scale. |
| Inspector | `KuiInspectorWindow` | `[KuiOption]` static field/property tweaker, grouped by `[KuiCategory]`. |
| Commander | `KuiCommanderWindow` | `[KuiCommand]` REPL with Tab completion + Up/Down history + isolated output pane. |
| System Info | `KuiSystemInfoWindow` | 5-section read-only data dump (Hardware/OS/Graphics/App/Runtime) + Refresh; `DumpAsText()` static helper for D8. |
| Bug Reporter | `KuiBugReporterWindow` | TextField + Capture → 4-file dump + `IBugReportSink` dispatch + Open-folder button + clickable Console hyperlink. |
| Touch overlay | `KuiTouchOverlay` (not a window) | Ring + crosshair drawn last in `KuiContext.TickInner`. Editor-only Ctrl+Space+P UI ping. |
| Toolbox | `KuiMasterWindow` | On/off control surface for all of the above; PlayerPrefs persistence. |

## Examples in the wild

Kunai is currently used as a git submodule (not a UPM package), so the `Samples~/` convention doesn't apply — pulling samples in via Package Manager isn't an option. Instead, the **Playnest** project (`github.com/sroglu/Playnest`) serves as the canonical reference for active usage:

| Concern | Reference |
|---|---|
| One-time wiring (font load, window registration) | `Assets/Shell/Scripts/KunaiBootstrap.cs` |
| Editor / Dev-build gating + call site | `Assets/Shell/Scripts/AppBootstrapper.cs` (the `KunaiBootstrap.Initialize()` block inside `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`) |
| Per-class `KuiLog` field + `Log.Info / Warn / Error` calls | `Assets/Shell/Scripts/Screens/HomeScreen.cs`, `…/Frames/BadgeGalleryFrame.cs`, `Assets/MiniGames/SumoBumo/SumoBumoLobbyModule.cs` etc. |
| Static-class workaround (string overload) | `Assets/Shell/Scripts/UI/ShellLabelBinder.cs`, `…/LocalizationBootstrap.cs` — both use `KuiLogger.For(nameof(X))` because static types can't be generic args (CS0718) |
| `KUNAI_VERBOSE` Standalone-only define setup | `ProjectSettings/ProjectSettings.asset` — `Standalone: …;KUNAI_VERBOSE`, iOS / Android tabs intentionally don't carry it |

When this module gains a second consumer or graduates to a UPM package, fold the equivalent into proper `Samples~/` scenes.

## Limitations

- **Still missing**: Dropdown, ProgressBar, multi-line TextField, image-blit widgets, theming.
- **BMP-only Unicode**: `TextChars` is `NativeArray<char>` (UTF-16). Codepoints above U+FFFF (Material Design Nerd Font icons `nf-md-*` start at `U+F0001`) need surrogate pairs and are **not supported** — use BMP equivalents (`nf-fa-microchip` instead of `nf-md-cpu_64_bit`). To add SMP support: change `TextChars` to `NativeArray<int>` codepoints and decode surrogates at `PushLabel` time.
- **Render pipeline support**: Built-in, URP, HDRP. Single `Camera.main` is the render target on every pipeline; multi-camera or render-texture overlays still aren't supported.
- **No gradients/rounded corners**: Vertex-colored axis-aligned rects + oriented thin quads (lines) only — same atlas, same shader, same single draw call. No textures other than the font atlas, no rounded corners, no gradients (brutalist aesthetic).
- **Main thread only**: All `KUI.*` calls must be on main thread. Logging via `Application.logMessageReceivedThreaded` is the one exception — capture is lock-free, registry insert deferred to `Drain` on main.
- **Font atlas external**: Must be pre-baked with a BMFont-compatible tool (see `bake/bake.sh`)
- **Single Main Camera**: `Camera.main` is the render target on every pipeline. Multi-camera or RT-target overlays are not supported. Adding either: split the render hook to attach to multiple cameras (Built-in) or filter `endCameraRendering` differently (SRP).
- **Reflection scan once**: D4 Inspector / D5 Commander walk the AppDomain at first window open. Late-loaded asmdefs (Addressables, dynamic DLLs) need a re-scan API (not yet exposed; close `Toolbox`/`Inspector` and re-open as a workaround).
- **Async commands not supported**: `[KuiCommand]` methods returning `Task` / `UniTask` are skipped. Wrap in a sync facade for now.
- **Bug-reporter sinks**: contract says "MUST NOT throw". The pipeline catches anyway and logs via `KuiLogger.Get("BugReporter")`, but a misbehaving sink is a bug in your sink, not in Kunai.

## Lifecycle gotchas

- **Tick after Dispose**: `GameLoop` event listeners may be invoked from a snapshot taken before `KUI.Shutdown` ran the deregister. `KuiContext.Tick` and `KuiCommandBuffer.BeginFrame` defensively check `Commands.IsCreated` and bail out — needed in Editor when entering/exiting Play Mode rapidly. Don't remove these guards.
- **Window registration order**: `KuiContext.RegisterWindow` calls `Initialize()` *before* the state is added to `_windowStates`, so the window's `WindowRect` setter correctly populates `State.Rect`. Reversing this order silently zeroes the rect (rect (0,0,0,0) → VertexCountJob returns 0 → panel doesn't render but text labels still emit at origin — this was a real bug we hit).
- **`KuWindow.Shutdown`**: Override on windows that own native containers (e.g. `KuiProfilerWindow` releases `KuiFrameSampler` arrays). `KuiContext.Dispose` walks every registered window and calls `Shutdown()` once.
- **Master toolbox order**: Register `KuiMasterWindow` FIRST so its first-frame PlayerPrefs apply runs at iteration 0 of `TickInner`. Otherwise other windows render with their default `IsVisible` for one frame before the master overrides them.
- **Soft-keyboard ownership**: `KuiTextField` opens / closes `TouchScreenKeyboard` based on `KuiInputFocus`. Two TextFields can never be focused at once — the focus model is single-owner. Don't poll `Input.inputString` in your own widgets while inside Kunai's frame; it'll fight the TextField's consumer pass.

## Icons (`Kunai.KuiIcons`)

Public string constants for ~40 BMP-range Nerd Font glyphs (Font Awesome subset): `Check`, `Cross`, `Warning`, `Info`, `Bug`, `Cpu`, `Memory`, `Timer`, `Cog`, chevrons, arrows, etc. Concat with text:

```csharp
KUI.Label(KuiIcons.Bug + " null ref at line 42");
```

Adding more icons: edit `bake/chars.txt`, re-run `bake/bake.sh`, optionally add a constant to `KuiIcons.cs`. Code path doesn't change — the parser auto-loads anything in the `.fnt`.

## Font baking (`bake/`)

`bake/bake.sh` invokes [fontbm](https://github.com/vladimirgamalyan/fontbm) (build from source on macOS, no brew tap). Inputs: `bake/chars.txt` (UTF-8 text file, one char per codepoint to bake) + a `.ttf` next to the script. Outputs: `Assets/GameSpecific/KunaiDebugTool/IosevkaKunai.{fnt.txt,png}`.

BMFont format quirks the parser handles:
- `info size=-N` (negative = pixel units, positive = points) — parser takes `abs(N)`
- ASCII fast-path keeps `id < 128` in array; everything else goes into the unicode hashmap
- `.fnt` is renamed to `.fnt.txt` post-bake so Unity recognizes it as `TextAsset`

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

Tests/Runtime/   — NUnit Editor-mode (UNITY_INCLUDE_TESTS)
                   KuiTextField, ReflectionScanner, CategoryParser, LogBufferCollapse,
                   FrameSampler, OptionRegistry, CommandParser, CommandRegistry,
                   BugReportSink.
```
