using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using PFound.Render.Core.Pipeline;
using PFound.Render.Core.RenderTextures;

namespace PFound.Render.Effects.Blur
{
    /// <summary>
    /// Screen-space separable Gaussian blur pass. Resolves source from URP
    /// <see cref="BlurStrengthVolumeComponent"/>, runs ping-pong downsample + H/V iterations,
    /// dispatches output per <see cref="BlurOutputMode"/>: camera composite, _RenderBlurTexture
    /// global publish, or both.
    /// </summary>
    public sealed class BlurPass : RenderPassBase<BlurPassData>, IDisposable
    {
        private static readonly int s_BlurResult         = Shader.PropertyToID("_BlurResult");
        private static readonly int s_BlurStrength       = Shader.PropertyToID("_BlurStrength");
        private static readonly int s_RenderBlurTexture  = Shader.PropertyToID("_RenderBlurTexture");
        private static readonly int s_RenderBlurTexTexel = Shader.PropertyToID("_RenderBlurTexture_TexelSize");

        private readonly Material _material;
        private readonly BlurDownsample _defaultDownsample;
        private readonly int _defaultIterations;
        private RenderTexturePool _pool;
        private bool _warnedInvalidTarget;

        /// <summary>One-shot warning on invalid target (width/height &lt; 2). Default: editor + dev build only.</summary>
        public bool WarnOnInvalidTarget { get; set; }

        public BlurPass(
            Material blurMaterial,
            RenderTexturePool pool,
            BlurDownsample defaultDownsample,
            int defaultIterations,
            bool warnOnInvalidTarget,
            RenderPassEvent injectionPoint)
            : base("Render.BlurPass", injectionPoint)
        {
            _material = blurMaterial;
            _pool = pool;
            _defaultDownsample = defaultDownsample;
            _defaultIterations = defaultIterations;
            WarnOnInvalidTarget = warnOnInvalidTarget;
        }

        protected override void Populate(
            IRasterRenderGraphBuilder builder,
            ref BlurPassData data,
            ContextContainer frameData)
        {
            // Reset; URP RenderGraph reuses the DTO.
            data.Active = false;
            data.Strength = 0f;
            data.Downsample = (int)_defaultDownsample;
            data.Iterations = _defaultIterations;
            data.OutputMode = BlurOutputMode.CameraComposite;

            var stack = VolumeManager.instance?.stack;
            var vol = stack != null ? stack.GetComponent<BlurStrengthVolumeComponent>() : null;
            if (vol == null || !vol.Enable.value) return;

            data.Strength = vol.Strength.value;
            data.OutputMode = vol.OutputMode.value;

            if (vol.OverrideQualitySettings.value)
            {
                data.Downsample = (int)vol.Downsample.value;
                data.Iterations = vol.Iterations.value;
            }

            // CameraComposite-only zero-strength no-op (GlobalTexture/Both still run — global has consumers).
            if (data.OutputMode == BlurOutputMode.CameraComposite && data.Strength <= 0f) return;

            data.Active = true;
        }

        protected override void Execute(RasterCommandBuffer cmd, in BlurPassData data)
        {
            if (!data.Active || _material == null || _pool == null) return;

            // The BlurPass is enqueued through URP; we don't have direct camera descriptor here.
            // The Blitter.BlitTexture call uses URP's _BlitTexture binding which is the camera
            // color attachment at this injection point. For ping-pong + downsample, we lease
            // transient RTs at downsampled dims; URP provides the camera dim via screen size.
            int srcW = Screen.width;
            int srcH = Screen.height;
            int ds = data.Downsample > 0 ? data.Downsample : 1;
            int rtW = Mathf.Max(1, srcW / ds);
            int rtH = Mathf.Max(1, srcH / ds);

            if (rtW < 2 || rtH < 2)
            {
                MaybeWarnInvalidTarget(srcW, srcH);
                return;
            }

            var key = new RenderTextureKey(rtW, rtH, GraphicsFormat.R8G8B8A8_UNorm);
            var leaseA = _pool.Lease(key);
            var leaseB = _pool.Lease(key);

            try
            {
                // First H-pass (downsamples via destination RT being smaller).
                Blitter.BlitTexture(cmd, leaseA.RT, Vector4.zero, _material, 0);

                for (int i = 0; i < data.Iterations - 1; i++)
                {
                    Blitter.BlitTexture(cmd, leaseB.RT, Vector4.zero, _material, 1); // V
                    Blitter.BlitTexture(cmd, leaseA.RT, Vector4.zero, _material, 0); // H (no further downsample needed)
                }

                Blitter.BlitTexture(cmd, leaseB.RT, Vector4.zero, _material, 1); // final V

                if (data.OutputMode == BlurOutputMode.GlobalTexture || data.OutputMode == BlurOutputMode.Both)
                {
                    Shader.SetGlobalTexture(s_RenderBlurTexture, leaseB.RT);
                    Shader.SetGlobalVector(s_RenderBlurTexTexel,
                        new Vector4(1f / leaseB.RT.width, 1f / leaseB.RT.height,
                                    leaseB.RT.width, leaseB.RT.height));
                }

                if (data.OutputMode == BlurOutputMode.CameraComposite || data.OutputMode == BlurOutputMode.Both)
                {
                    Shader.SetGlobalTexture(s_BlurResult, leaseB.RT);
                    Shader.SetGlobalFloat(s_BlurStrength, data.Strength);
                    Blitter.BlitTexture(cmd, Vector4.zero, _material, 2);
                }
            }
            finally
            {
                _pool.Release(leaseA);
                _pool.Release(leaseB);
            }
        }

        /// <summary>Releases the pool reference. Does NOT dispose the pool — the owning RenderFeature does.</summary>
        public void Dispose()
        {
            _pool = null;
        }

        private void MaybeWarnInvalidTarget(int w, int h)
        {
            if (!WarnOnInvalidTarget || _warnedInvalidTarget) return;
            _warnedInvalidTarget = true;
            Debug.LogWarning($"[Effects.Blur] Camera target too small for blur ({w}x{h}). Pass will no-op until target grows.");
        }
    }
}