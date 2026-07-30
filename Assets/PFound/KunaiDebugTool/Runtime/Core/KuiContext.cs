using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Kunai
{
    internal class KuiContext : IDisposable
    {
        public static KuiContext Instance { get; private set; }

        public KuiCommandBuffer CommandBuffer;
        public KuiCanvas Canvas;
        public KuiFontAtlas FontAtlas;
        public KuiSettings Settings;
        public KuiInputHandler InputHandler;
        public KuiLayout Layout;
        public bool IsVisible;

        readonly List<KuWindow> _windows = new();
        readonly List<KuiWindowState> _windowStates = new();
        int _nextWindowId;
        float _lastScreenWidth;
        float _lastScreenHeight;
        GameObject _overlayRunner;

        static readonly ProfilerMarker s_tickMarker = new(ProfilerCategory.Scripts, "Kunai.Tick");

        // Public stopwatch-based measurement. Last frame's Tick duration in nanoseconds.
        // Set right after Tick completes; readable from outside (test/instrumentation code).
        public static long LastTickDurationNs;

        KuiContext() { }

        public static void Create(Texture2D fontAtlasTexture, TextAsset fontMetrics)
        {
            if (Instance != null) return;

            var ctx = new KuiContext();
            ctx.Settings = new KuiSettings();
            ctx.CommandBuffer = new KuiCommandBuffer(512, 4096);

            ctx.FontAtlas = new KuiFontAtlas();
            ctx.FontAtlas.Load(fontAtlasTexture, fontMetrics);

            // Shader is kept in the project's Always Included Shaders list so it survives player-build
            // stripping (it has no material/scene reference to keep it alive otherwise).
            var shader = Shader.Find("Hidden/KUI-Combined");
            Debug.Assert(shader != null,
                "[Kunai] 'Hidden/KUI-Combined' not found — add it to Graphics > Always Included Shaders.");
            ctx.Canvas = new KuiCanvas(shader, fontAtlasTexture);

            ctx.InputHandler = new KuiInputHandler(ctx.Settings);
            ctx.Layout = new KuiLayout();
            ctx.IsVisible = false;

            ctx._lastScreenWidth = Screen.width;
            ctx._lastScreenHeight = Screen.height;

            Instance = ctx;

            Application.onBeforeRender += ctx.Tick;

            // WaitForEndOfFrame coroutine host. The overlay used to attach a
            // CommandBuffer to Camera.AfterEverything (and an SRP equivalent),
            // but both fire BEFORE UI Toolkit composites screen-overlay
            // panels — so any UIDocument would draw on top of the overlay.
            // Pulling the buffer in WaitForEndOfFrame guarantees the overlay
            // sits above every UI panel.
            ctx._overlayRunner = new GameObject("[KunaiOverlayRunner]")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            UnityEngine.Object.DontDestroyOnLoad(ctx._overlayRunner);
            ctx._overlayRunner.AddComponent<KuiOverlayRunner>();
        }

        public static void Destroy()
        {
            if (Instance == null) return;

            Application.onBeforeRender -= Instance.Tick;

            if (Instance._overlayRunner != null)
            {
                UnityEngine.Object.Destroy(Instance._overlayRunner);
                Instance._overlayRunner = null;
            }

            Instance.Dispose();
            Instance = null;
        }

        public int RegisterWindow(KuWindow window)
        {
            int id = _nextWindowId++;

            window.State = new KuiWindowState
            {
                Id = id,
                Title = window.Title,
                MinSize = new Vector2(KuiStyles.MinWindowWidth, KuiStyles.MinWindowHeight),
                IsMinimized = false,
                ZOrder = id,
                IsVisible = true
            };

            // Let window.Initialize() populate Rect (and any other fields) via WindowRect setter.
            window.Initialize();

            _windows.Add(window);
            _windowStates.Add(window.State);
            return id;
        }

        public void UnregisterWindow(KuWindow window)
        {
            int idx = _windows.IndexOf(window);
            if (idx < 0) return;
            _windows.RemoveAt(idx);
            _windowStates.RemoveAt(idx);
        }

        public List<KuWindow> Windows => _windows;
        public List<KuiWindowState> WindowStates => _windowStates;

        // Set during the per-window OnRenderUI() call so widgets like BeginScroll
        // resolve the correct rect for the currently-rendering window
        // (not the last-registered window in the list).
        internal Rect CurrentWindowRect;

        // True only while rendering the topmost window under the pointer, with no drag in progress and
        // no just-ended drag-release. KuiHit gates every widget on this so input never falls through to
        // an overlapped lower window and a drag-release isn't mis-read as a click.
        internal bool CurrentWindowInputActive;

        void Tick()
        {
            // Defensive: a Tick may fire from an in-flight listener snapshot after Destroy
            // already disposed the command buffer. Bail out early in that case.
            if (!CommandBuffer.Commands.IsCreated) return;

            using var _ = s_tickMarker.Auto();
            long tStart = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                TickInner();
            }
            finally
            {
                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - tStart;
                // Convert ticks to nanoseconds: ns = ticks * (1e9 / Frequency)
                LastTickDurationNs = elapsed * 1_000_000_000L / System.Diagnostics.Stopwatch.Frequency;
            }
        }

        void TickInner()
        {

            InputHandler.ReadInput();
            InputHandler.DetectToggle(ref IsVisible, _windowStates);

            if (!IsVisible)
            {
                // Camera holds onto the cmd buffer with last-frame's contents; clear so
                // it stops drawing the stale mesh every frame.
                Canvas?.ClearCommandBuffer();
                return;
            }
            if (_windows.Count == 0)
            {
                Canvas?.ClearCommandBuffer();
                return;
            }

            // Camera target dimensions are authoritative. In Editor Game View,
            // Screen.width returns the Editor window size, not the actual render
            // target (which the cmd buffer writes into). Mismatch causes ortho
            // proj to misalign with the rendered image.
            var renderCam = Camera.main;
            float screenW = renderCam != null ? renderCam.pixelWidth : Screen.width;
            float screenH = renderCam != null ? renderCam.pixelHeight : Screen.height;

            if (Math.Abs(screenW - _lastScreenWidth) > 0.5f || Math.Abs(screenH - _lastScreenHeight) > 0.5f)
            {
                _lastScreenWidth = screenW;
                _lastScreenHeight = screenH;
                KuiDPI.UpdateScale(Settings);
            }

            CommandBuffer.BeginFrame();
            Layout.BeginFrame(screenW, screenH);
            KuiSlider.ResetIds();

            float4 fullClip = new Unity.Mathematics.float4(0, 0, screenW, screenH);

            InputHandler.UpdateWindowInteraction(_windowStates, Settings);
            KuiWindowManager.UpdateInteraction(_windowStates, InputHandler, Settings);

            for (int i = 0; i < _windows.Count; i++)
            {
                var state = _windowStates[i];
                if (!state.IsVisible) continue;

                _windows[i].State = state;

                if (state.IsMinimized)
                {
                    DrawMinimizedBubble(state, fullClip);
                    continue;
                }

                if (_windows[i].StretchHorizontal)
                {
                    float inset = KuiDPI.Px(_windows[i].StretchInsetPx);
                    var r = state.Rect;
                    r.x = inset;
                    r.width = screenW - inset * 2f;
                    state.Rect = r;
                    _windowStates[i] = state;
                }

                CurrentWindowRect = state.Rect;
                // Only the topmost window under the pointer accepts widget input (no overlap fall-through),
                // and never mid-drag or on a drag-release frame.
                CurrentWindowInputActive = i == InputHandler.HotWindowIndex
                                        && !KuiWindowManager.IsDragging
                                        && !KuiWindowManager.ConsumedRelease;
                Layout.BeginWindow(state.Rect, fullClip);
                DrawWindowChrome(state, fullClip);
                _windows[i].OnRenderUI();
                DrawResizeHandles(state, fullClip);   // after content so the grips stay on top
                Layout.EndWindow();

                _windowStates[i] = _windows[i].State;
            }

            // Click-outside-any-focusable-widget clears focus. Focusable widgets
            // (TextField) call KuiInputFocus.TryAcquire on MouseDown, which bumps
            // LastChangeFrame to this frame. If MouseDown happened this frame and
            // no widget claimed it, the user clicked empty space.
            if (InputHandler.State.MouseDown
             && KuiInputFocus.LastChangeFrame != Time.frameCount)
            {
                KuiInputFocus.Clear();
            }

            // Touch overlay renders LAST so the ring sits on top of every
            // window. Internally guards on Settings.EnableTouchIndicator —
            // costs ~12 PushRects when enabled, zero when not.
            KuiTouchOverlay.Render(this, screenW, screenH);

            float fontScale = FontAtlas.IsLoaded
                ? KuiDPI.Px(Settings.BaseFontSize) / FontAtlas.BaseFontSize
                : 1f;

            Canvas.GenerateAndFlush(ref CommandBuffer, FontAtlas.AsciiCache, FontAtlas.ExtendedCache, fontScale, FontAtlas.BaseFontSize, screenW, screenH);
        }

        void DrawWindowChrome(KuiWindowState state, Unity.Mathematics.float4 clip)
        {
            var r = state.Rect;
            CommandBuffer.PushRect(new Unity.Mathematics.float4(r.x, r.y, r.width, r.height), KuiStyles.Panel, clip);
            CommandBuffer.PushRect(new Unity.Mathematics.float4(r.x, r.y, r.width, KuiDPI.Px(KuiStyles.TitleBarHeight)), KuiStyles.TitleBar, clip);

            // Minimize button — explicit, visible square in the title bar just left of the TR resize
            // grip (geometry shared with the press hit-test in KuiWindowManager so it's WYSIWYG).
            var mb = KuiWindowManager.MinimizeButtonRect(r);
            var mouse = InputHandler.State.MousePosition;
            bool mbHover = mouse.x >= mb.x && mouse.x <= mb.x + mb.z && mouse.y >= mb.y && mouse.y <= mb.y + mb.w;
            float inset = KuiDPI.Px(3f);
            CommandBuffer.PushRect(
                new Unity.Mathematics.float4(mb.x + inset, mb.y + inset, mb.z - inset * 2f, mb.w - inset * 2f),
                mbHover ? KuiStyles.ButtonHover : KuiStyles.Button, clip);
            // Underscore glyph = "minimize".
            float glyphPad = mb.z * 0.3f;
            float glyphY = mb.y + mb.w * 0.62f;
            CommandBuffer.PushLine(
                new Unity.Mathematics.float2(mb.x + glyphPad, glyphY),
                new Unity.Mathematics.float2(mb.x + mb.z - glyphPad, glyphY),
                KuiDPI.Px(2f), KuiStyles.Text, clip);

            float titleX = r.x + KuiDPI.Px(KuiStyles.Padding);
            float titleY = r.y + KuiDPI.Px(2f);
            // Label stops before the minimize button so long titles don't run under it.
            float titleW = math.max(0f, mb.x - KuiDPI.Px(KuiStyles.Padding) - titleX);
            CommandBuffer.PushLabel(state.Title,
                new Unity.Mathematics.float4(titleX, titleY, titleW, KuiDPI.Px(KuiStyles.TitleBarHeight)),
                KuiStyles.Text, clip);
        }

        // Visible resize grips on the TOP-RIGHT (controls right + top) and BOTTOM-LEFT (controls left +
        // bottom) corners — a diagonal pair. The minimize button sits just left of the top-right grip
        // (no overlap; the grip is the rightmost titleH px). Each square exactly matches
        // KuiWindowManager's corner grab zone, so "what you see is what you grab".
        void DrawResizeHandles(KuiWindowState state, Unity.Mathematics.float4 clip)
        {
            var r = state.Rect;
            float g = KuiDPI.Px(KuiStyles.ResizeGrabWidth);
            DrawResizeHandle(r.x + r.width - g, r.y,                g, clip);   // top-right
            DrawResizeHandle(r.x,               r.y + r.height - g, g, clip);   // bottom-left
        }

        void DrawResizeHandle(float x, float y, float g, Unity.Mathematics.float4 clip)
        {
            CommandBuffer.PushRect(new Unity.Mathematics.float4(x, y, g, g), KuiStyles.ResizeHandle, clip);

            // Three parallel anti-diagonal (⟋) grip lines — point toward the TR / BL corners.
            float th = KuiDPI.Px(1.5f);
            for (int i = 1; i <= 3; i++)
            {
                float c = g * i / 2f;   // g/2, g, 3g/2 (local x+y = c, clamped to the square)
                var p0 = new Unity.Mathematics.float2(x + Mathf.Max(0f, c - g), y + Mathf.Min(c, g));
                var p1 = new Unity.Mathematics.float2(x + Mathf.Min(c, g),      y + Mathf.Max(0f, c - g));
                CommandBuffer.PushLine(p0, p1, th, KuiStyles.ResizeHandleGrip, clip);
            }
        }

        void DrawMinimizedBubble(KuiWindowState state, Unity.Mathematics.float4 clip)
        {
            var b = KuiWindowManager.BubbleRect(state);
            CommandBuffer.PushRect(b, KuiStyles.WindowMinBubble, clip);

            // Restore button — rightmost square of the bubble. The rest is a drag handle.
            var rb = KuiWindowManager.BubbleRestoreButtonRect(state);
            var mouse = InputHandler.State.MousePosition;
            bool hover = mouse.x >= rb.x && mouse.x <= rb.x + rb.z && mouse.y >= rb.y && mouse.y <= rb.y + rb.w;
            float inset = KuiDPI.Px(3f);
            CommandBuffer.PushRect(
                new Unity.Mathematics.float4(rb.x + inset, rb.y + inset, rb.z - inset * 2f, rb.w - inset * 2f),
                hover ? KuiStyles.ButtonHover : KuiStyles.Button, clip);
            // Hollow-square glyph = "restore / maximize".
            float gp = rb.z * 0.3f;
            float gx0 = rb.x + gp, gx1 = rb.x + rb.z - gp;
            float gy0 = rb.y + gp, gy1 = rb.y + rb.w - gp;
            float th = KuiDPI.Px(1.5f);
            CommandBuffer.PushLine(new Unity.Mathematics.float2(gx0, gy0), new Unity.Mathematics.float2(gx1, gy0), th, KuiStyles.Text, clip);
            CommandBuffer.PushLine(new Unity.Mathematics.float2(gx0, gy1), new Unity.Mathematics.float2(gx1, gy1), th, KuiStyles.Text, clip);
            CommandBuffer.PushLine(new Unity.Mathematics.float2(gx0, gy0), new Unity.Mathematics.float2(gx0, gy1), th, KuiStyles.Text, clip);
            CommandBuffer.PushLine(new Unity.Mathematics.float2(gx1, gy0), new Unity.Mathematics.float2(gx1, gy1), th, KuiStyles.Text, clip);

            float pad = KuiDPI.Px(4f);
            float titleW = math.max(0f, rb.x - pad - (b.x + pad));
            CommandBuffer.PushLabel(state.Title,
                new Unity.Mathematics.float4(b.x + pad, b.y + KuiDPI.Px(2f), titleW, b.w),
                KuiStyles.Text, clip);
        }

        public void Dispose()
        {
            // Walk windows so they can release any native containers / handles
            // they own (e.g., KuiProfilerWindow's frame sampler ring buffers).
            for (int i = 0; i < _windows.Count; i++)
            {
                try { _windows[i]?.Shutdown(); }
                catch (System.Exception ex) { UnityEngine.Debug.LogException(ex); }
            }

            CommandBuffer.Dispose();
            Canvas?.Dispose();
            FontAtlas?.Dispose();
        }
    }
}
