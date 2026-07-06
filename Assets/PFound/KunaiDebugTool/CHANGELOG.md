# KunaiDebugTool — CHANGELOG

## Render order — overlay sits on top of UI Toolkit panels (2026-05-10)

- `KuiOverlayRunner` MonoBehaviour pulls `KuiCanvas.ExecuteOnBackBuffer` from a
  `WaitForEndOfFrame` coroutine, so the overlay renders AFTER UI Toolkit /
  uGUI screen-space panels composite. Previously `CameraEvent.AfterEverything`
  (Built-in) and `RenderPipelineManager.endCameraRendering` (SRP) both fired
  BEFORE UI Toolkit's panel compose, leaving the overlay underneath any
  `UIDocument` (visible only in scenes with no UI panels).
- `KuiCanvas`: removed camera attachment + SRP hook; new public
  `ExecuteOnBackBuffer()` issues `Graphics.ExecuteCommandBuffer` directly
  against the back buffer. The CommandBuffer's explicit ortho ViewProjection
  needs no camera state.
- `KuiContext.Create`/`Destroy` spawn / destroy a hidden `[KunaiOverlayRunner]`
  GameObject (`HideAndDontSave`, `DontDestroyOnLoad`) that hosts the coroutine.

## Phase 2 — Console enhancements + 6 new tools + master toolbox (2026-05-03)

Grew the single-Console Phase 1 overlay into a 7-window debug suite without
breaking the perf budget (1 draw call, ≈0 GC, < 1 ms tick budget). Spec:
[`specs/009-kunai-phase2-suite/`](../../specs/009-kunai-phase2-suite/).

### Wave 0 — shared infrastructure

- `KuiInputFocus` — single-owner widget focus model (multiple TextFields
  arbitrate one keyboard).
- `KUI.TextField` (+ `KuiTextFieldState` value-type with fixed 256-char buffer,
  desktop `Input.inputString` + mobile `TouchScreenKeyboard`).
- `KUI.ChipStrip` — horizontal-scroll toggleable chips.
- `KUI.BeginCollapsible` / `EndCollapsible`.
- `KuiReflectionScanner` — generic `Scan<TAttr>` reused by D4 / D5.
- `KuWindow.Shutdown` virtual + `KuiContext.Dispose` walks every window.
- `KuiCommandBuffer.PushLabel(char[], start, count, ...)` overload.
- `KuiCommandBuffer.PushLabel(string, start, count, ...)` slice overload +
  `\n` / `\r` newline handling in `VertexWriteJob`.
- `KuWindow.StretchHorizontal` / `StretchInsetPx` virtuals.
- `KuiScrollView` IDisposable → `KuiScrollHandle` struct (no boxing per frame).

### D2 Console enhancements

- `KuiLogger` public façade — `KuiLogger.Get(category).Info / Warn / Error / Exception`.
- `KuiCategoryParser` (`[Foo]` prefix grammar per research R4) +
  `KuiCategoryRegistry` (32-cap + overflow set).
- `KuiLogEntry.Category` + `KuiLogBuffer.Drain(KuiCategoryRegistry)` registers
  observed categories on the main thread.
- `KuiLogBuffer.CollapseConsecutive(visibleIndices)` → `RunSpan(firstIndex,
  runCount)` with a per-fixture-reused list.
- ConsoleWindow toolbar reorder, search TextField, category chip strip,
  collapse-duplicates toggle, pin-at-top wiring.
- Per-row category badge with tight clip; Cmd/Ctrl+C copy skipped while
  search has focus.

### D5 Commander REPL

- `[KuiCommand("name", help: "...")]` static-method attribute (validates
  name length, no `[`, no whitespace).
- `[KuiCategory("...")]` shared with D4 Inspector (D5 ships first, owns the
  attribute file).
- `KuiCommandRegistry.BuildFromScan` via `KuiReflectionScanner`; skips
  instance methods + unsupported parameter types with a warning log.
- `KuiCommandParser` — tokenize (quotes + `\`-escape per R10) + bind
  (`Convert.ChangeType` for primitives, `Enum.Parse` ignore-case, manual bool
  table for `0/1/yes/no/on/off`).
- `KuiCommandHistory` — 50-entry circular ring with back-to-back dedupe.
- `KuiCommanderWindow` — TextField input + isolated output pane + suggestion
  strip + Tab completion (longest-common-prefix) + Up/Down history + Enter
  execute (returns "OK" for void, exception text rendered red).

### D3 Profiler panel

- `KuiFrameSampler` — Persistent `NativeArray<float>` ring (capacity 240) for
  frame-time + GC-delta. 8-frame smoothed FPS / ms / GC readouts. GC delta via
  `GC.GetTotalMemory(forceFullCollection: false)`.
- `KuiFrameGraphRenderer` — strip-of-rects, Y auto-scale to `max(samples) *
  1.1`, 16.67 ms cap line, over-budget tint.
- `KuiProfilerWindow` — readouts + graph + draw-call hint
  (`UnityStats.drawCalls` Editor-only, `n/a` in standalone). Disposes its
  sampler via `Shutdown()`.

### D4 Inspector

- `[KuiOption(label = null)]` + `[KuiRange(min, max)]` attributes (numeric
  members, `KuiRange` rejects `max < min`).
- `KuiOptionRegistry.BuildFromScan` — supported types: `bool / int / float /
  double / string / enum`. Cached `Func<object>` / `Action<object>` per entry;
  read-only properties get a no-op setter.
- `KuiOptionRow` — bool→Toggle, numeric+`KuiRange`→Slider, numeric→stepper
  (`-` / `+`), enum→cycle button, string→read-only Label. Reads on every
  render, writes only on user interaction.
- `KuiInspectorWindow` — collapsible per-category groups.

### D7 System Info

- `KuiSystemInfoWindow` — 5 sections (Hardware / OS / Graphics / App /
  Runtime). Each row reads inside per-field try/catch → `n/a` on failure.
  Refresh button re-snapshots.
- `KuiSystemInfoWindow.DumpAsText()` — multi-line `key: value` text used by
  D8's bug-report payload.

### D8 Bug Reporter

- `KuiBugReport` init-only DTO (Timestamp, Description, ScreenshotPng,
  ConsoleText, SystemInfoText, OutputDirectory).
- `IBugReportSink` interface + `KuiBugReporter.RegisterSink/UnregisterSink` +
  thread-safe snapshot Dispatch (per-sink try/catch; failures route to
  `KuiLogger.Get("BugReporter")`).
- `KuiBugReporterWindow` — TextField + Capture button. Pipeline (per
  research R9): WaitForEndOfFrame → `ScreenCapture.CaptureScreenshotAsTexture`
  → background-thread file write → main-thread sink dispatch + log path.
  Hidden DontDestroyOnLoad GameObject hosts the coroutine.
- `IsExternalInit` polyfill in `AssemblyInfo.cs` — Unity .NET Standard 2.1
  doesn't ship it; needed for init-only DTO properties.

### D6 Touch indicator

- `KuiSettings.EnableTouchIndicator` (default `false`) + `Radius` /
  `Segments` tuning fields.
- `KuiTouchOverlay` — N-segment ring + center crosshair drawn last in
  `KuiContext.TickInner`. Touch wins over mouse on mobile.
- Editor-only `Ctrl+Space+P` raycasts `EventSystem.RaycastAll`, selects +
  pings the topmost UI GameObject in the hierarchy, logs its full transform
  path.

### Master toolbox + UX polish

- `KuWindow.ShowInMasterToggle` virtual (default `true`); Console overrides
  to `false` (pinned).
- `KuiMasterWindow` — single front-of-house panel listing every togglable
  window. Toggling ON clamps the rect into the current screen. State persists
  in PlayerPrefs (`Kunai.Master.Vis.<title-ascii>`); first-frame load applies
  before subsequent windows render.
- `KuiInputState.TouchDelta` + `KuiWindowManager.IsDragging` static accessor.
- `KuiScrollImpl.Begin` — touch-swipe scroll path (suppressed during window
  drag/resize so a finger on a title bar doesn't also scroll content).
- `KuiConsoleWindow` Pin fix: snap-to-top fires only on new entries arriving;
  manual scroll-down works as expected.
- `KuiBugReporterWindow` "📂 Open folder" button (Editor:
  `EditorUtility.RevealInFinder`, Standalone: `Application.OpenURL`); log
  message wraps the path in `<a href="…">` for clickable Unity 2021.2+
  console hyperlinks.

### Tests (NUnit Editor mode, `UNITY_INCLUDE_TESTS`)

`Tests/Runtime/` asmdef + 8 fixtures: `KuiTextFieldTests`,
`KuiReflectionScannerTests`, `KuiCategoryParserTests`, `KuiLogBufferCollapseTests`,
`KuiFrameSamplerTests`, `KuiOptionRegistryTests`, `KuiCommandParserTests`,
`KuiCommandRegistryTests`, `KuiBugReportSinkTests`.

### Perf gate (informal observation, T093)

Demo with all 7 windows visible at locked Editor framerate (700 FPS):
- `Profiler.ms` smoothed = 1.4 – 4 ms per frame (well below 16.67 ms cap).
- `Profiler.GC` smoothed = 1 – 13 KB/f (variable; bulk of allocation comes
  from MCP plumbing logs during automated testing — quiescent demo sits much
  lower).
- `Profiler.Draws` = 8 (1 Kunai mesh + a handful for the cube, light, skybox).

The full T055 stress benchmark (5 windows + 1000-item scroll + 200 widgets
× ≥60 000 frames) is user-runnable via the existing `KunaiTestRunner` scene.
Phase 2 introduces no new draw call and no new per-frame allocation in the
hot path; the existing budget is preserved.

## Phase 1 — initial commit (2026-04-30)

See parent repo `docs/CHANGELOG.md` entry **025** for the Phase 1 design
overview (1-draw-call architecture, Burst pipeline, font system, base
widgets, single Console window).
