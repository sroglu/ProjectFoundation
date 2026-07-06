using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PFound.Render.Core.Pipeline
{
    /// <summary>
    /// Abstract base for URP 17 <see cref="ScriptableRendererFeature"/> subclasses.
    /// Wraps lifecycle (<c>Create</c>, <c>AddRenderPasses</c>, <c>Dispose</c>) and
    /// material/pass disposal so subclasses only override <see cref="OnCreate"/>.
    /// </summary>
    /// <remarks>
    /// Subclasses construct their passes inside <see cref="OnCreate"/> using
    /// <see cref="LoadMaterial(Shader)"/> / <see cref="LoadMaterial(string)"/> and
    /// <see cref="EnqueuePass"/>. Every loaded material is auto-destroyed on
    /// <c>Dispose</c>; every enqueued pass implementing <see cref="IDisposable"/>
    /// is auto-disposed.
    /// </remarks>
    public abstract class RenderFeatureBase : ScriptableRendererFeature
    {
        private readonly List<ScriptableRenderPass> _passes = new(2);
        private readonly List<Material> _materials = new(2);
        private bool _disposed;

        /// <summary>
        /// Called once per feature creation (and after assembly reload). Subclasses
        /// construct their passes here using <see cref="LoadMaterial(Shader)"/> and
        /// <see cref="EnqueuePass"/>; the base handles the rest.
        /// </summary>
        protected abstract void OnCreate();

        /// <summary>
        /// Optional override invoked before <c>Dispose</c> tears down materials and
        /// passes. Subclasses release any resources not acquired via the helpers.
        /// </summary>
        protected virtual void OnDispose() { }

        /// <summary>
        /// Loads a material from a shader and registers it for auto-disposal on
        /// feature teardown.
        /// </summary>
        protected Material LoadMaterial(Shader shader)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            var m = CoreUtilsCompat.CreateEngineMaterial(shader);
            _materials.Add(m);
            return m;
        }

        /// <summary>
        /// Loads a material by shader name (e.g., <c>"Hidden/Universal Render Pipeline/Blit"</c>)
        /// and registers it for auto-disposal.
        /// </summary>
        protected Material LoadMaterial(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName)) throw new ArgumentException("Shader name is null or empty.", nameof(shaderName));
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException($"Shader '{shaderName}' not found.");
            return LoadMaterial(shader);
        }

        /// <summary>
        /// Registers a pass for both <c>AddRenderPasses</c> enqueueing and disposal.
        /// Passes implementing <see cref="IDisposable"/> are disposed when the
        /// feature is torn down.
        /// </summary>
        protected void EnqueuePass(ScriptableRenderPass pass)
        {
            if (pass == null) throw new ArgumentNullException(nameof(pass));
            _passes.Add(pass);
        }

        /// <inheritdoc />
        public sealed override void Create()
        {
            _passes.Clear();
            _materials.Clear();
            _disposed = false;
            OnCreate();
        }

        // URP's ScriptableRendererFeature does NOT wire OnDestroy → Dispose(true)
        // (only IDisposable.Dispose() does). Hook it here so feature SO destruction
        // tears down enqueued IDisposable passes and engine materials deterministically.
        private void OnDestroy() => Dispose(true);

        /// <inheritdoc />
        public sealed override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            for (int i = 0; i < _passes.Count; i++)
                renderer.EnqueuePass(_passes[i]);
        }

        /// <inheritdoc />
        protected sealed override void Dispose(bool disposing)
        {
            if (!disposing) return;
            if (_disposed) return;       // idempotent — OnDestroy + explicit Dispose() may both fire
            _disposed = true;

            OnDispose();

            for (int i = 0; i < _passes.Count; i++)
            {
                if (_passes[i] is IDisposable d) d.Dispose();
            }
            _passes.Clear();

            for (int i = 0; i < _materials.Count; i++)
            {
                CoreUtilsCompat.Destroy(_materials[i]);
            }
            _materials.Clear();
        }

        /// <summary>
        /// Thin wrapper around <c>CoreUtils.CreateEngineMaterial</c> /
        /// <c>UnityEngine.Object.DestroyImmediate</c> so we don't take a hard
        /// dependency on a specific helper across URP versions.
        /// </summary>
        private static class CoreUtilsCompat
        {
            public static Material CreateEngineMaterial(Shader shader)
            {
                var m = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                return m;
            }

            public static void Destroy(UnityEngine.Object obj)
            {
                if (obj == null) return;
                if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
                else UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }
}
