using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using PFound.Render.Core.Pipeline;
using PFound.Render.Core.RenderTextures;

namespace PFound.Render.Effects.Outline
{
    /// <summary>
    /// Screen-space depth-edge outline pass. Resolves the active
    /// <see cref="OutlineVolumeComponent"/>, samples URP's auto-generated
    /// <c>_CameraDepthTexture</c> via a 4-tap Roberts cross filter, and composites the
    /// edges into the camera color attachment with one fullscreen blit.
    /// </summary>
    public sealed class OutlinePass : RenderPassBase<OutlinePassData>, IDisposable
    {
        private static readonly int s_EdgeColor = Shader.PropertyToID("_EdgeColor");
        private static readonly int s_Strength  = Shader.PropertyToID("_Strength");
        private static readonly int s_Thickness = Shader.PropertyToID("_Thickness");

        private readonly Material _material;
        private readonly int _defaultThickness;
        private RenderTexturePool _pool;
        private bool _warnedInvalidConfig;

        /// <summary>One-shot warning when an active volume holds an invalid Thickness. Default: editor + dev build only.</summary>
        public bool WarnOnInvalidConfig { get; set; }

        /// <summary>
        /// Constructs the pass and declares <see cref="ScriptableRenderPassInput.Depth"/> so URP
        /// auto-generates <c>_CameraDepthTexture</c> for the active camera.
        /// </summary>
        /// <param name="outlineMaterial">Material instantiated from <c>Hidden/Render/Outline</c>.</param>
        /// <param name="pool">Per-feature <see cref="RenderTexturePool"/>. Not disposed here; the feature owns it.</param>
        /// <param name="defaultThickness">Fallback sample radius when the volume's value is unusable.</param>
        /// <param name="warnOnInvalidConfig">If true, emit one-shot <c>Debug.LogWarning</c> on invalid configuration.</param>
        /// <param name="injectionPoint">URP <see cref="RenderPassEvent"/>; <c>AfterRenderingPostProcessing</c> by default.</param>
        public OutlinePass(
            Material outlineMaterial,
            RenderTexturePool pool,
            int defaultThickness,
            bool warnOnInvalidConfig,
            RenderPassEvent injectionPoint)
            : base("Render.OutlinePass", injectionPoint)
        {
            if (outlineMaterial == null) throw new ArgumentNullException(nameof(outlineMaterial));
            if (pool == null) throw new ArgumentNullException(nameof(pool));

            _material = outlineMaterial;
            _pool = pool;
            _defaultThickness = defaultThickness;
            WarnOnInvalidConfig = warnOnInvalidConfig;

            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        /// <inheritdoc />
        protected override void Populate(
            IRasterRenderGraphBuilder builder,
            ref OutlinePassData data,
            ContextContainer frameData)
        {
            var stack = VolumeManager.instance?.stack;
            var vol = stack != null ? stack.GetComponent<OutlineVolumeComponent>() : null;
            ResolveActiveData(vol, _defaultThickness, data);

            if (data.Active)
            {
                // Declare depth as a read resource so RenderGraph wires the dependency.
                var resourceData = frameData.Get<UniversalResourceData>();
                var depth = resourceData.cameraDepthTexture;
                if (depth.IsValid())
                    builder.UseTexture(depth, AccessFlags.Read);
            }
            else if (vol != null && vol.Enable.value && vol.Strength.value > 0f && vol.Thickness.value <= 0)
            {
                MaybeWarnInvalidConfig();
            }
        }

        /// <summary>
        /// Resolves the 5-check no-op state machine into <paramref name="data"/>. Test-friendly helper:
        /// no <see cref="VolumeManager"/> / RenderGraph dependency. <paramref name="vol"/> may be null
        /// (treated as "no active volume" — produces <c>Active = false</c>).
        /// </summary>
        /// <param name="vol">Volume component to resolve, or <c>null</c> for the "no active volume" case.</param>
        /// <param name="defaultThickness">Fallback thickness when the volume's value is unusable.</param>
        /// <param name="data">DTO to populate (reset on entry; never null).</param>
        public static void ResolveActiveData(OutlineVolumeComponent vol, int defaultThickness, OutlinePassData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            // Reset; URP RenderGraph reuses the DTO between frames.
            data.Active = false;
            data.EdgeColor = Color.black;
            data.Thickness = defaultThickness;
            data.Strength = 0f;

            if (vol == null) return;
            if (!vol.Enable.value) return;
            if (vol.Strength.value <= 0f) return;

            int thickness = vol.Thickness.value;
            if (thickness <= 0) return;

            Color edgeColor = vol.EdgeColor.value;
            if (edgeColor.a <= 0f) return;

            data.EdgeColor = edgeColor;
            data.Thickness = thickness;
            data.Strength = vol.Strength.value;
            data.Active = true;
        }

        /// <inheritdoc />
        protected override void Execute(RasterCommandBuffer cmd, in OutlinePassData data)
        {
            if (!data.Active || _material == null) return;

            Shader.SetGlobalColor(s_EdgeColor, data.EdgeColor);
            Shader.SetGlobalFloat(s_Strength, data.Strength);
            Shader.SetGlobalFloat(s_Thickness, data.Thickness);
            Blitter.BlitTexture(cmd, Vector4.zero, _material, 0);
        }

        /// <summary>Releases the pool reference. Does NOT dispose the pool — the owning feature does.</summary>
        public void Dispose()
        {
            _pool = null;
        }

        private void MaybeWarnInvalidConfig()
        {
            if (!WarnOnInvalidConfig || _warnedInvalidConfig) return;
            _warnedInvalidConfig = true;
            Debug.LogWarning("[Effects.Outline] Active OutlineVolumeComponent has Thickness <= 0; pass will no-op until corrected.");
        }
    }
}
