using System;
using UnityEngine;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// WorldSpace sink. Captures the <see cref="MeshRenderer.sharedMaterial"/> on first
    /// <see cref="Bind"/>, clones it (so the project asset isn't mutated), writes the RT to the
    /// clone's <c>_BaseMap</c>/<c>_MainTex</c>, and assigns the clone as the runtime instance
    /// material. <see cref="Unbind"/> destroys the clone and restores the captured shared
    /// material. Idempotent.
    /// </summary>
    public sealed class MeshRendererSink : IRenderContextSink
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private readonly MeshRenderer _target;
        private Material _captured;
        private Material _clone;
        private bool _bound;

        public MeshRendererSink(MeshRenderer target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            _target = target;
        }

        public void Bind(RenderTexture rt)
        {
            if (_target == null) return;
            if (!_bound)
            {
                _captured = _target.sharedMaterial;
                _bound = true;
            }
            _clone = _captured != null ? new Material(_captured) : new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture"));
            if (_clone.HasProperty(BaseMapId)) _clone.SetTexture(BaseMapId, rt);
            if (_clone.HasProperty(MainTexId)) _clone.SetTexture(MainTexId, rt);
            _target.material = _clone;
        }

        public void Unbind()
        {
            if (!_bound) return;
            if (_target != null) _target.sharedMaterial = _captured;
            if (_clone != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_clone);
                else UnityEngine.Object.DestroyImmediate(_clone);
                _clone = null;
            }
            _captured = null;
            _bound = false;
        }
    }
}
