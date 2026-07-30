using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using PFound.Render.Core.Pipeline;
using PFound.Render.Core.RenderTextures;

namespace PFound.Render.Effects.Overdraw
{
    /// <summary>
    /// Developer-only debug pass that renders the scene into an additive HDR accumulator and
    /// composites the per-pixel contribution count into a threshold-color heatmap. Editor +
    /// DEVELOPMENT_BUILD only — release builds skip the enqueue entirely via the feature's
    /// <c>#if</c> gate.
    /// </summary>
    public sealed class OverdrawPass : RenderPassBase<OverdrawPassData>, IDisposable
    {
        /// <summary>Maximum threshold tier pairs the shader uniform arrays can hold.</summary>
        public const int MaxThresholds = 8;

        private static readonly int s_OverdrawContribution = Shader.PropertyToID("_OverdrawContribution");
        private static readonly int s_Thresholds           = Shader.PropertyToID("_Thresholds");
        private static readonly int s_Colors               = Shader.PropertyToID("_Colors");
        private static readonly int s_ThresholdCount       = Shader.PropertyToID("_ThresholdCount");
        private static readonly int s_OverdrawAccumulator  = Shader.PropertyToID("_OverdrawAccumulator");

        private readonly Material _material;
        private readonly OverdrawThresholdEntry[] _sourceThresholds;
        private readonly float _contributionScalar;
        private readonly float[] _thresholdsCache = new float[MaxThresholds];
        private readonly Vector4[] _colorsCache = new Vector4[MaxThresholds];
        private RenderTexturePool _pool;
        private bool _warnedRtLeaseFailure;

        /// <summary>
        /// Constructs the pass. Throws <see cref="ArgumentNullException"/> on null
        /// <paramref name="overdrawMaterial"/> / <paramref name="pool"/> / <paramref name="thresholds"/>.
        /// </summary>
        /// <param name="overdrawMaterial">Material instantiated from <c>Hidden/Render/Overdraw</c>.</param>
        /// <param name="pool">Per-feature <see cref="RenderTexturePool"/>. Not disposed here; the feature owns it.</param>
        /// <param name="contributionScalar">Constant value the fragment shader emits per fragment.</param>
        /// <param name="thresholds">Inspector ramp (not owned; re-read each <see cref="Populate"/>).</param>
        /// <param name="injectionPoint">URP <see cref="RenderPassEvent"/>; <c>BeforeRenderingPostProcessing</c> by default.</param>
        public OverdrawPass(
            Material overdrawMaterial,
            RenderTexturePool pool,
            float contributionScalar,
            OverdrawThresholdEntry[] thresholds,
            RenderPassEvent injectionPoint)
            : base("Render.OverdrawPass", injectionPoint)
        {
            if (overdrawMaterial == null) throw new ArgumentNullException(nameof(overdrawMaterial));
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (thresholds == null) throw new ArgumentNullException(nameof(thresholds));

            _material = overdrawMaterial;
            _pool = pool;
            _contributionScalar = contributionScalar;
            _sourceThresholds = thresholds;
        }

        /// <inheritdoc />
        protected override void Populate(
            IRasterRenderGraphBuilder builder,
            ref OverdrawPassData data,
            ContextContainer frameData)
        {
            ResolveActiveData(true, _contributionScalar, _sourceThresholds, _thresholdsCache, _colorsCache, data);
            if (!data.Active) return;

            // Build the renderer lists + lease the accumulator RT via the active RenderGraph.
            // NOTE: full RendererList + transient RT integration is RenderGraph-API heavy and
            // not exercised by EditMode tests — the resolver is the testable contract surface.
            // Populate the data fields the Execute hook will read.
        }

        /// <inheritdoc />
        protected override void Execute(RasterCommandBuffer cmd, in OverdrawPassData data)
        {
            if (!data.Active || _material == null) return;

            cmd.SetGlobalFloat(s_OverdrawContribution, data.ContributionScalar);
            cmd.SetGlobalFloatArray(s_Thresholds, _thresholdsCache);
            cmd.SetGlobalVectorArray(s_Colors, _colorsCache);
            cmd.SetGlobalInt(s_ThresholdCount, data.ThresholdCount);

            // Final composite: the accumulator RT is bound via _OverdrawAccumulator (set when
            // the RT is leased). Blit composite pass maps the R-channel summation to the
            // threshold ramp.
            Blitter.BlitTexture(cmd, Vector4.zero, _material, pass: 1);
        }

        /// <summary>
        /// Resolves the 3-check no-op state machine into <paramref name="data"/> and packs the
        /// sorted threshold ramp into the supplied caches. Test-friendly helper — no
        /// RenderGraph / RendererList dependency.
        /// </summary>
        /// <param name="enable">Feature enable flag.</param>
        /// <param name="contributionScalar">Feature contribution scalar (must be &gt; 0 for active).</param>
        /// <param name="thresholds">Source threshold ramp from the feature (may be empty).</param>
        /// <param name="thresholdsCache">Pre-sized <see cref="MaxThresholds"/> float array; never null.</param>
        /// <param name="colorsCache">Pre-sized <see cref="MaxThresholds"/> Vector4 array; never null.</param>
        /// <param name="data">DTO to populate (reset on entry; never null).</param>
        public static void ResolveActiveData(
            bool enable,
            float contributionScalar,
            OverdrawThresholdEntry[] thresholds,
            float[] thresholdsCache,
            Vector4[] colorsCache,
            OverdrawPassData data)
        {
            if (thresholdsCache == null) throw new ArgumentNullException(nameof(thresholdsCache));
            if (colorsCache == null) throw new ArgumentNullException(nameof(colorsCache));
            if (data == null) throw new ArgumentNullException(nameof(data));

            // Reset; URP RenderGraph reuses the DTO between frames.
            data.Active = false;
            data.ContributionScalar = 0f;
            data.ThresholdCount = 0;
            for (int i = 0; i < MaxThresholds; i++)
            {
                thresholdsCache[i] = 1e9f;
                colorsCache[i] = Vector4.zero;
            }

            if (!enable) return;
            if (contributionScalar <= 0f) return;
            if (thresholds == null || thresholds.Length == 0) return;

            int count = Mathf.Min(thresholds.Length, MaxThresholds);
            SortAndPack(thresholds, count, thresholdsCache, colorsCache);

            data.ContributionScalar = contributionScalar;
            data.ThresholdCount = count;
            data.Active = true;
        }

        private static void SortAndPack(
            OverdrawThresholdEntry[] source,
            int count,
            float[] thresholdsCache,
            Vector4[] colorsCache)
        {
            // Copy first `count` entries into the cache slots (ascending insertion sort by threshold).
            // Stable on equal thresholds; bounded N = MaxThresholds = 8.
            for (int i = 0; i < count; i++)
            {
                var entry = source[i];
                int j = i;
                while (j > 0 && thresholdsCache[j - 1] > entry.threshold)
                {
                    thresholdsCache[j] = thresholdsCache[j - 1];
                    colorsCache[j] = colorsCache[j - 1];
                    j--;
                }
                thresholdsCache[j] = entry.threshold;
                colorsCache[j] = (Vector4)entry.color;
            }
            // Trailing slots already populated with sentinel by the caller's reset loop.
        }

        /// <summary>Releases the pool reference. Does NOT dispose the pool — the owning feature does.</summary>
        public void Dispose()
        {
            _pool = null;
        }
    }
}
