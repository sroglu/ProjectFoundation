using System;
using UnityEngine.Rendering;

namespace PFound.Render.Effects.Blur
{
    /// <summary>URP <see cref="VolumeParameter{T}"/> for <see cref="BlurDownsample"/>.</summary>
    [Serializable]
    public sealed class BlurDownsampleParameter : VolumeParameter<BlurDownsample>
    {
        public BlurDownsampleParameter(BlurDownsample value = BlurDownsample.x2, bool overrideState = false)
            : base(value, overrideState) { }
    }
}