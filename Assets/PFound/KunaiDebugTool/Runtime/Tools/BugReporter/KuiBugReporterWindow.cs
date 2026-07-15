using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Kunai
{
    /// <summary>
    /// Capture-the-state-and-dump-to-disk window. Description TextField
    /// + Capture button. On Capture, writes 4 files into
    /// <c>Application.persistentDataPath/KunaiBugReports/&lt;timestamp&gt;/</c>:
    /// <c>screenshot.png</c>, <c>console.txt</c>, <c>system.txt</c>, <c>description.txt</c>.
    /// All registered <see cref="IBugReportSink"/>s receive the same payload
    /// after the file dump completes.
    ///
    /// Storage hygiene: a user-configurable max-storage cap (slider, persisted
    /// to PlayerPrefs) auto-prunes the oldest report folders after each
    /// capture so the bug-report directory can't grow without bound. A "Clear
    /// all reports" button wipes the whole directory on demand.
    ///
    /// Capture sequence (see research.md R9):
    ///   1. Set "capture pending" flag.
    ///   2. WaitForEndOfFrame → ScreenCapture.CaptureScreenshotAsTexture.
    ///   3. Background-thread file write (allocation-heavy).
    ///   4. Debug.Log path; dispatch to sinks.
    ///   5. Background-thread prune of oldest reports beyond the cap.
    /// </summary>
    public class KuiBugReporterWindow : KuWindow
    {
        public override string Title => KuiIcons.Bug + " Bug Report";

        const int    DescFieldId        = 9501;
        const string MaxSizePrefKey     = "Kunai.BugReporter.MaxSizeMb";
        const string HideOverlayPrefKey = "Kunai.BugReporter.HideOverlay";
        const int    DefaultMaxSizeMb   = 100;
        const int    MinMaxSizeMb       = 10;
        const int    MaxMaxSizeMb       = 1000;

        KuiTextFieldState _description;
        string            _lastReportPath;
        string            _captureStatus = "idle";
        bool              _capturing;
        Coroutine         _captureRoutine;
        int               _maxSizeMb = DefaultMaxSizeMb;
        bool              _hideOverlayInScreenshot = true; // default ON — capture the actual game, not the debug UI
        bool              _prefsLoaded;
        long              _currentTotalBytes = -1; // -1 = unknown, refresh after capture/clear

        // Hidden GameObject to host the coroutine — the window itself is a
        // plain class, not a MonoBehaviour, so it has no StartCoroutine.
        static MonoBehaviour s_coroutineHost;

        public override void Initialize()
        {
            // Slider + clear button + status add a row each — bumped height to
            // fit the whole control surface without scroll on a typical run.
            WindowRect = new Rect(420, 340, 380, 320);
        }

        public override void OnRenderUI()
        {
            if (!_prefsLoaded) LoadPrefs();

            KUI.Label("What broke? (Enter or Capture submits)");
            if (KUI.TextField(DescFieldId, ref _description) && !_capturing)
                BeginCapture();

            // Default-on: most useful capture is "what the user sees" — i.e. the
            // game without the debug overlay obscuring it. Toggling off keeps
            // the overlay visible (useful when the bug IS in the overlay).
            bool prevHide = _hideOverlayInScreenshot;
            _hideOverlayInScreenshot = KUI.Toggle(prevHide, "Hide debug panel in screenshot");
            if (_hideOverlayInScreenshot != prevHide)
            {
                PlayerPrefs.SetInt(HideOverlayPrefKey, _hideOverlayInScreenshot ? 1 : 0);
                PlayerPrefs.Save();
            }

            using (KUI.BeginGroup())
            {
                if (KUI.Button(KuiIcons.Bug + " Capture", 120f) && !_capturing)
                    BeginCapture();
                KUI.Label(_capturing ? "capturing..." : _captureStatus);
            }

            KUI.Separator();

            // Max-storage slider — persists to PlayerPrefs. Reports older than
            // the cap are pruned after each capture (oldest first) so the
            // folder size stays bounded across long testing sessions.
            KUI.Label($"Max storage: {_maxSizeMb} MB" + UsageSuffix());
            float newCap = KUI.Slider(_maxSizeMb, MinMaxSizeMb, MaxMaxSizeMb);
            int rounded = Mathf.RoundToInt(newCap);
            if (rounded != _maxSizeMb)
            {
                _maxSizeMb = rounded;
                PlayerPrefs.SetInt(MaxSizePrefKey, _maxSizeMb);
                PlayerPrefs.Save();
            }

            if (KUI.Button(KuiIcons.Trash + " Clear all reports", 220f))
                ClearAllReports();

            if (!string.IsNullOrEmpty(_lastReportPath))
            {
                KUI.Separator();
                KUI.Label("last report:");
                KUI.Label(_lastReportPath, KuiStyles.TextDim);
                if (KUI.Button(KuiIcons.Folder + " Open folder"))
                    OpenInFileBrowser(_lastReportPath);
            }
        }

        // Editor: reveal in Finder/Explorer (cross-platform — Unity routes
        // to Explorer on Windows, Finder on macOS, file manager on Linux).
        // Player: handoff to OS via OpenURL — works on desktop standalones;
        // mobile platforms have no equivalent and silently no-op.
        static void OpenInFileBrowser(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(path);
#else
            try { Application.OpenURL("file://" + path); }
            catch { /* mobile / restricted platform — best effort */ }
#endif
        }

        // ---- prefs / sizing ---------------------------------------------------

        void LoadPrefs()
        {
            _maxSizeMb = Mathf.Clamp(
                PlayerPrefs.GetInt(MaxSizePrefKey, DefaultMaxSizeMb),
                MinMaxSizeMb, MaxMaxSizeMb);
            // Default ON when no pref exists. Reading 0/1 with a default of 1
            // keeps first-run behavior aligned with the field initializer.
            _hideOverlayInScreenshot = PlayerPrefs.GetInt(HideOverlayPrefKey, 1) != 0;
            _prefsLoaded = true;
        }

        string UsageSuffix()
        {
            // Lazy: only refresh size when last value is stale (after capture
            // or clear). Avoids walking the directory tree every UI frame.
            if (_currentTotalBytes < 0)
                _currentTotalBytes = ComputeTotalBugReportBytes();
            float mb = _currentTotalBytes / (1024f * 1024f);
            return $"   (using {mb:F1} MB)";
        }

        static long ComputeTotalBugReportBytes()
        {
            string root = Path.Combine(Application.persistentDataPath, "KunaiBugReports");
            if (!Directory.Exists(root)) return 0;
            long total = 0;
            try
            {
                foreach (var d in Directory.GetDirectories(root))
                    total += ComputeDirSize(d);
            }
            catch { /* permission / IO race — best effort */ }
            return total;
        }

        static long ComputeDirSize(string dir)
        {
            long sum = 0;
            try
            {
                foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { sum += new FileInfo(f).Length; }
                    catch { /* file vanished mid-walk — skip */ }
                }
            }
            catch { }
            return sum;
        }

        void ClearAllReports()
        {
            string root = Path.Combine(Application.persistentDataPath, "KunaiBugReports");
            if (Directory.Exists(root))
            {
                int cleared = 0;
                foreach (var d in Directory.GetDirectories(root))
                {
                    try { Directory.Delete(d, recursive: true); cleared++; }
                    catch (Exception ex) { Debug.LogWarning($"[BugReporter] failed to delete {d}: {ex.Message}"); }
                }
                _captureStatus = $"cleared {cleared} report(s)";
            }
            else
            {
                _captureStatus = "nothing to clear";
            }
            _lastReportPath = null;
            _currentTotalBytes = -1; // force refresh next frame
        }

        // Delete oldest report folders until the total on-disk size drops below
        // the configured cap. Runs on a background thread because Directory IO
        // can stall on slow disks; UI stays responsive throughout.
        static void PruneOldReports(string root, long maxBytes)
        {
            if (!Directory.Exists(root)) return;
            string[] dirs;
            try { dirs = Directory.GetDirectories(root); }
            catch { return; }
            if (dirs.Length == 0) return;

            var infos = new List<(string path, long size, DateTime ts)>(dirs.Length);
            long total = 0;
            for (int i = 0; i < dirs.Length; i++)
            {
                long sz;
                DateTime ts;
                try { sz = ComputeDirSize(dirs[i]); ts = Directory.GetCreationTime(dirs[i]); }
                catch { continue; }
                infos.Add((dirs[i], sz, ts));
                total += sz;
            }
            if (total <= maxBytes) return;

            infos.Sort((a, b) => a.ts.CompareTo(b.ts)); // oldest first
            for (int i = 0; i < infos.Count && total > maxBytes; i++)
            {
                try
                {
                    Directory.Delete(infos[i].path, recursive: true);
                    total -= infos[i].size;
                }
                catch { /* skip and move on; next capture will re-attempt */ }
            }
        }

        // ---- capture pipeline -------------------------------------------------

        void BeginCapture()
        {
            _capturing = true;
            _captureStatus = "capturing...";
            EnsureCoroutineHost();
            _captureRoutine = s_coroutineHost.StartCoroutine(CaptureRoutine(_description.GetText()));
        }

        IEnumerator CaptureRoutine(string description)
        {
            // Optionally hide the debug overlay so the screenshot captures
            // the actual game (the common case). We toggle BEFORE letting a
            // new frame render — `yield return null` lets one full frame
            // render with the overlay off, then WaitForEndOfFrame guarantees
            // the screenshot matches that overlay-less frame.
            bool hidOverlay = false;
            if (_hideOverlayInScreenshot && KUI.IsVisible)
            {
                KUI.IsVisible = false;
                hidOverlay = true;
                yield return null;
            }

            // 1. Wait until the camera has finished rendering this frame so the
            //    screenshot reflects the frame the user clicked on, not the
            //    next-frame state.
            yield return new WaitForEndOfFrame();

            byte[] png;
            try
            {
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                png = tex.EncodeToPNG();
                UnityEngine.Object.Destroy(tex);
            }
            catch (Exception e)
            {
                if (hidOverlay) KUI.IsVisible = true; // restore even on failure
                FinishWithError("screenshot failed: " + e.Message);
                yield break;
            }

            // Restore the overlay immediately after capture; the rest of the
            // pipeline (text snapshot + background write) runs with the UI back
            // on so the user sees "capturing..." status updates.
            if (hidOverlay) KUI.IsVisible = true;

            // Snapshot text payloads on the main thread (Console buffer / system
            // info accessors are not thread-safe).
            string consoleText = DumpConsoleBuffer();
            string systemText  = KuiSystemInfoWindow.DumpAsText();
            string ts          = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string root        = Path.Combine(Application.persistentDataPath, "KunaiBugReports");
            string outDir      = Path.Combine(root, ts);
            long   maxBytes    = (long)_maxSizeMb * 1024L * 1024L;

            // 2. Background-thread write — file IO is allocation-heavy and
            //    must NOT block the render thread.
            Task<KuiBugReport> writeTask = Task.Run(() =>
            {
                var rep = WriteAndBuildReport(outDir, ts, description, png, consoleText, systemText);
                // Prune oldest after the new write so the new report is always
                // counted against the cap. Failures here don't prevent the
                // capture from succeeding — pruning retries on the next capture.
                try { PruneOldReports(root, maxBytes); } catch { /* logged below */ }
                return rep;
            });

            while (!writeTask.IsCompleted) yield return null;

            if (writeTask.IsFaulted)
            {
                FinishWithError("write failed: " + (writeTask.Exception?.GetBaseException()?.Message ?? "unknown"));
                yield break;
            }

            var report = writeTask.Result;
            _lastReportPath    = report.OutputDirectory;
            _captureStatus     = "done";
            _capturing         = false;
            _currentTotalBytes = -1; // size changed; refresh on next render

            // Unity 2021.2+ renders any <a key="value"> in log strings as a
            // clickable link AND surfaces all attributes via
            // EditorGUI.hyperLinkClicked. We use a custom attribute
            // (`kunaiBugFolder`) instead of `href` so KuiBugReportHyperlinkHandler
            // can route the click to EditorUtility.RevealInFinder — Unity's
            // built-in `href` handler tries Application.OpenURL which silently
            // no-ops on plain directory paths. The inline color+underline gives
            // it the visual link styling Unity normally only applies to href.
            Debug.Log($"[BugReporter] wrote report to <a kunaiBugFolder=\"{report.OutputDirectory}\"><color=#5fb3ff><u>{report.OutputDirectory}</u></color></a>");

            // 3. Dispatch to sinks (per-sink try/catch is in KuiBugReporter.Dispatch).
            KuiBugReporter.Dispatch(report);

            // Clear the description so the next capture starts fresh.
            _description.Clear();
        }

        static KuiBugReport WriteAndBuildReport(string outDir, string ts, string description,
                                                byte[] png, string consoleText, string systemText)
        {
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, "screenshot.png"),  png ?? Array.Empty<byte>());
            File.WriteAllText (Path.Combine(outDir, "console.txt"),     consoleText ?? string.Empty);
            File.WriteAllText (Path.Combine(outDir, "system.txt"),      systemText  ?? string.Empty);
            File.WriteAllText (Path.Combine(outDir, "description.txt"), description ?? string.Empty);

            return new KuiBugReport
            {
                Timestamp       = DateTime.Now,
                Description     = description ?? string.Empty,
                ScreenshotPng   = png         ?? Array.Empty<byte>(),
                ConsoleText     = consoleText ?? string.Empty,
                SystemInfoText  = systemText  ?? string.Empty,
                OutputDirectory = outDir,
            };
        }

        // Render the live KuiConsole.Buffer as multi-line text, newest at the
        // bottom (file-friendly chronological order). Allocation is fine — this
        // runs once per Capture, not per frame.
        static string DumpConsoleBuffer()
        {
            var b = KuiConsole.Buffer;
            if (b == null) return "<KuiConsole not initialised>";
            var sb = new StringBuilder(b.Count * 80);
            for (int i = 0; i < b.Count; i++)
            {
                ref var e = ref b.GetAt(i);
                sb.Append('[').Append(e.TimeSinceStartup.ToString("F2")).Append("s] ");
                sb.Append(e.Level).Append(' ');
                if (!string.IsNullOrEmpty(e.Category)) sb.Append('[').Append(e.Category).Append("] ");
                sb.Append(e.Message ?? string.Empty);
                if (!string.IsNullOrEmpty(e.StackTrace))
                {
                    sb.Append('\n').Append(e.StackTrace);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        void FinishWithError(string err)
        {
            Debug.LogError("[BugReporter] " + err);
            _captureStatus = "error: " + err;
            _capturing = false;
        }

        // The window itself is a plain class — to run a coroutine we need a
        // MonoBehaviour. One hidden GameObject for all bug-report captures.
        static void EnsureCoroutineHost()
        {
            if (s_coroutineHost != null) return;
            var go = new GameObject("KuiBugReporter.CoroutineHost") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            s_coroutineHost = go.AddComponent<CoroutineHost>();
        }

        sealed class CoroutineHost : MonoBehaviour { }
    }
}
