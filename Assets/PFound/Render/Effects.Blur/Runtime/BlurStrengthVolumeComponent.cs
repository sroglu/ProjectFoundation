using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PFound.Render.Effects.Blur
{
    /// <summary>
    /// URP <see cref="VolumeComponent"/> exposing screen-space blur controls. Drop this into any
    /// Volume Profile and the <see cref="BlurRenderFeature"/> picks it up on the matching camera.
    /// </summary>
    [Serializable, VolumeComponentMenu("Render/Blur Strength")]
    public sealed class BlurStrengthVolumeComponent : VolumeComponent
    {
        /// <summary>Master toggle. When false the BlurPass no-ops.</summary>
        public BoolParameter Enable = new BoolParameter(false);

        /// <summary>Composite mix factor in [0, 1]. <c>0</c> = original camera color, <c>1</c> = fully blurred. Ignored in <see cref="BlurOutputMode.GlobalTexture"/> mode (the global is always full blur — snapshot-friendly).</summary>
        public ClampedFloatParameter Strength = new ClampedFloatParameter(1f, 0f, 1f);

        /// <summary>Output dispatch mode: composite into camera, publish as <c>_RenderBlurTexture</c> global, or both.</summary>
        public BlurOutputModeParameter OutputMode = new BlurOutputModeParameter(BlurOutputMode.CameraComposite);

        /// <summary>When true, this volume's <see cref="Downsample"/> + <see cref="Iterations"/> override the feature's defaults.</summary>
        public BoolParameter OverrideQualitySettings = new BoolParameter(false);

        /// <summary>Per-volume downsample override (used when <see cref="OverrideQualitySettings"/> is true).</summary>
        public BlurDownsampleParameter Downsample = new BlurDownsampleParameter(BlurDownsample.x2);

        /// <summary>Per-volume iteration override (used when <see cref="OverrideQualitySettings"/> is true).</summary>
        public ClampedIntParameter Iterations = new ClampedIntParameter(2, 1, 4);
    }
}