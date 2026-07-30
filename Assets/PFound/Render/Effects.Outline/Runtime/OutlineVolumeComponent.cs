using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PFound.Render.Effects.Outline
{
    /// <summary>
    /// URP <see cref="VolumeComponent"/> exposing depth-edge outline controls. Drop this into any
    /// Volume Profile and the <see cref="OutlineRenderFeature"/> picks it up on the matching camera.
    /// </summary>
    [Serializable, VolumeComponentMenu("Render/Outline")]
    public sealed class OutlineVolumeComponent : VolumeComponent
    {
        /// <summary>Master toggle. When false the OutlinePass no-ops.</summary>
        public BoolParameter Enable = new BoolParameter(false);

        /// <summary>Outline color. Alpha controls overlay opacity (premultiplied into the composite weight).</summary>
        public ColorParameter EdgeColor = new ColorParameter(Color.black, hdr: false, showAlpha: true, showEyeDropper: true);

        /// <summary>Sample offset radius in texels (1..16). Larger values yield thicker outlines.</summary>
        public ClampedIntParameter Thickness = new ClampedIntParameter(2, 1, 16);

        /// <summary>Composite mix factor in [0, 1]. <c>0</c> = original camera color, <c>1</c> = full outline composite.</summary>
        public ClampedFloatParameter Strength = new ClampedFloatParameter(1f, 0f, 1f);
    }
}
