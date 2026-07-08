using System;
using UnityEngine.Rendering;

namespace PFound.Render.Effects.Blur
{
    /// <summary>URP <see cref="VolumeParameter{T}"/> for <see cref="BlurOutputMode"/>.</summary>
    [Serializable]
    public sealed class BlurOutputModeParameter : VolumeParameter<BlurOutputMode>
    {
        public BlurOutputModeParameter(BlurOutputMode value = BlurOutputMode.CameraComposite, bool overrideState = false)
            : base(value, overrideState) { }
    }
}