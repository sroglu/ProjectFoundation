using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kunai
{
    /// <summary>
    /// Realtime perf overlay: rolling-window FPS / frame-time / GC delta,
    /// 240-frame two-line history graph (avg + peak), draw-call hint
    /// (Editor only). Sampling runs every frame so the graph stays smooth,
    /// but the displayed text refreshes at a throttled rate
    /// (<see cref="TextRefreshHz"/>) so values stay readable instead of
    /// flickering at the game's frame rate.
    /// </summary>
    public class KuiProfilerWindow : KuWindow
    {
        public override string Title => KuiIcons.Cpu + " Profiler";

        const float CapLineFps   = 60f;             // 60 fps budget reference (graph in FPS units)
        const float GraphHeight  = 90f;
        // Editor focus loss / mobile app pause produces a single huge
        // deltaTime (multiple seconds) that swamps the Y-scale. Clamp at
        // 100 ms so chunky real hitches still register.
        const float MaxAcceptedFrameSec = 0.1f;

        // Graph time window in seconds. Visible samples = Hz × DisplaySec.
        // 4s @ 60Hz = 240 entries (the sampler ring capacity); at lower Hz
        // the same time window holds fewer entries and the visible graph
        // narrows accordingly so there's always 4 seconds of history.
        const float DisplaySec = 4f;

        // Refresh rate clamp. One slider controls BOTH the sampler commit
        // (= graph scroll cadence) and the text refresh — so the displayed
        // numbers and the graph always agree on the time window they
        // describe.
        const int   MinHz     = 1;
        const int   MaxHz     = 60;
        const int   DefaultHz = 30;
        const string HzPrefKey = "Kunai.Profiler.Hz";

        KuiFrameSampler _sampler;
        bool            _initialised;
        int             _lastSampledFrame = -1;

        // Single shared cadence: both the graph commit and the text
        // recompute live on this Hz.
        int              _hz = DefaultHz;
        bool             _prefsLoaded;
        float            _nextTick;
        string           _txtFps   = "FPS: --";
        string           _txtMs    = "ms:  --";
        string           _txtGc    = "GC:  --";
        string           _txtDraws = "Draws: --";
        Color32          _txtFpsColor = KuiOkColor;

        public override void Initialize()
        {
            // Height accommodates DPI-scaled content on Retina: title +
            // 4 readouts + refresh slider (label + slider rows) + separator
            // + graph (DPI-scaled GraphHeight) + legend row + paddings.
            // The first-run rect persists in PlayerPrefs so the user can
            // shrink/grow afterwards and the master toolbox restores layout.
            WindowRect = new Rect(800, 460, 480, 440);
            _sampler = KuiFrameSampler.Create(240);
            _initialised = true;
            _nextTick = 0f;
        }

        public override void Shutdown()
        {
            if (_initialised) _sampler.Dispose();
            _initialised = false;
        }

        public override void OnRenderUI()
        {
            if (!_initialised || !_sampler.IsCreated) return;

            if (!_prefsLoaded)
            {
                _hz = Mathf.Clamp(
                    PlayerPrefs.GetInt(HzPrefKey, DefaultHz),
                    MinHz, MaxHz);
                _prefsLoaded = true;
            }

            // Per-frame: accumulate into the sampler's pending bucket. Cheap
            // (no ring write). Clamp huge frame-times (Editor focus pauses,
            // mobile app-resume) so a single multi-second delta doesn't
            // dominate the Y-scale. 100 ms ≈ a chunky real hitch.
            if (Time.frameCount != _lastSampledFrame)
            {
                float dt = Time.unscaledDeltaTime;
                if (dt > MaxAcceptedFrameSec) dt = MaxAcceptedFrameSec;
                _sampler.AccumulateFrame(dt);
                _lastSampledFrame = Time.frameCount;
            }

            float now = Time.unscaledTime;

            // Single tick: commit graph entry + recompute text. Both share
            // the slider Hz so the visible time-window matches across
            // readouts and the graph.
            if (now >= _nextTick)
            {
                _nextTick = now + (1f / _hz);
                _sampler.CommitTick();

                // Stats window = most recent ~1s worth of ticks at the
                // current Hz (so text always describes the last 1s no matter
                // how slow or fast the slider is).
                int statsN = _hz < _sampler.Count ? _hz : _sampler.Count;
                if (statsN < 1) statsN = 1;
                var s = _sampler.ComputeStats(statsN);
                _txtFps = "FPS: " + Mathf.RoundToInt(s.AvgFps) + " avg / "
                                  + Mathf.RoundToInt(s.MinFps) + " min";
                // ms shows avg + min (the floor the engine can hit) per user
                // request: a single hitch dominates "peak" and obscures
                // sustained perf, while min reads as "what the engine could
                // sustain without bottlenecks". The graph still draws the peak
                // line so spikes remain visible over time.
                _txtMs  = "ms:  " + s.AvgMs.ToString("F2") + " avg / "
                                  + s.MinMs.ToString("F2") + " min";
                _txtGc  = "GC:  " + s.AvgGcKb.ToString("F2") + " KB/f avg";
                _txtDraws = "Draws: " + GetDrawCallsHint();

                _txtFpsColor = s.AvgFps >= 58f ? KuiOkColor
                             : s.AvgFps >= 28f ? KuiWarnColor
                             : KuiErrColor;
            }

            // Vertical stack — KUI.Label has no width overload that plays well
            // with BeginGroup's horizontal layout, and four short readouts read
            // perfectly fine stacked.
            KUI.Label(_txtFps, _txtFpsColor);
            KUI.Label(_txtMs);
            KUI.Label(_txtGc);
            KUI.Label(_txtDraws);

            // Refresh slider: governs the shared graph + text cadence.
            // Lower Hz = calmer text AND a narrower graph (right-aligned in
            // the available area; left side stays empty rather than
            // stretching the few visible samples to fill width).
            KUI.Label("Refresh: " + _hz + " Hz");
            float newHz = KUI.Slider(_hz, MinHz, MaxHz);
            int rounded = Mathf.RoundToInt(newHz);
            if (rounded != _hz)
            {
                _hz = Mathf.Clamp(rounded, MinHz, MaxHz);
                PlayerPrefs.SetInt(HzPrefKey, _hz);
                PlayerPrefs.Save();
            }

            KUI.Separator();

            // Graph occupies the remaining width; height fixed.
            var ctx = KuiContext.Instance;
            if (ctx == null) return;

            float h = KuiDPI.Px(GraphHeight);
            float4 rect = ctx.Layout.NextRect(h);

            KuiFrameGraphRenderer.Render(_sampler, rect, CapLineFps, ctx, _hz, DisplaySec);

            // Tiny legend under the graph. Two stacked rows because
            // KUI.Label inside BeginGroup uses NextRect(height) which takes
            // ContentWidth without advancing CursorX — siblings would draw
            // on top of each other (same overlap as the inspector stepper).
            KUI.Label("— avg FPS",              KuiAvgLabelColor);
            KUI.Label("— min FPS (bottleneck)", KuiBottleneckLabelColor);
        }

        static string GetDrawCallsHint()
        {
#if UNITY_EDITOR
            // UnityEditor.UnityStats is editor-only; in standalone we don't have
            // a cheap counter, so we report n/a.
            try { return UnityStats.drawCalls.ToString(); }
            catch { return "n/a"; }
#else
            return "n/a";
#endif
        }

        static readonly Color32 KuiOkColor        = new(120, 220, 120, 255);
        static readonly Color32 KuiWarnColor      = new(220, 200,  90, 255);
        static readonly Color32 KuiErrColor       = new(230, 100, 100, 255);
        static readonly Color32 KuiAvgLabelColor        = new(120, 180, 230, 255);
        static readonly Color32 KuiBottleneckLabelColor = new(230, 140, 110, 255);
    }
}
