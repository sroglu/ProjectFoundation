#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Kunai;
using UnityEngine;

namespace PFound.SampleGame
{
    /// <summary>
    /// Wires the Kunai debug overlay into the sample game (editor / development builds only).
    /// Loads the font atlas from Resources, registers the standard windows, and leaves the
    /// overlay hidden — reveal it with the toggle key (` / F1) or the 3-tap top-left gesture.
    /// </summary>
    static class SampleKunaiBootstrap
    {
        public static void Initialize()
        {
            var atlas = Resources.Load<Texture2D>("IosevkaKunai");
            var metrics = Resources.Load<TextAsset>("IosevkaKunai.fnt");
            Debug.Assert(atlas != null && metrics != null,
                "[Kunai] IosevkaKunai font atlas/metrics missing under any Resources/ folder.");

            KUI.Initialize(atlas, metrics);

            KUI.RegisterWindow(new KuiMasterWindow());
            KuiConsole.Initialize();
            KUI.RegisterWindow(new KuiConsoleWindow());
            KUI.RegisterWindow(new KuiProfilerWindow());
            KUI.RegisterWindow(new KuiInspectorWindow());
            KUI.RegisterWindow(new KuiCommanderWindow());
            KUI.RegisterWindow(new KuiSystemInfoWindow());
            KUI.RegisterWindow(new KuiBugReporterWindow());

            KUI.IsVisible = false;
        }
    }
}
#endif
