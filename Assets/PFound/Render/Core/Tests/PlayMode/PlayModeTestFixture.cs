using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace PFound.Render.Core.Tests
{
    /// <summary>
    /// Base PlayMode test fixture. Provides a main camera in an empty scene plus
    /// helpers for swapping the active <see cref="RenderPipelineAsset"/> for the
    /// duration of a test.
    /// </summary>
    /// <remarks>
    /// Each test gets a fresh camera in <see cref="SetUp"/> and a clean teardown.
    /// Subclasses that need a programmatically-created URP renderer asset call
    /// <see cref="UseRenderPipelineAsset(UniversalRenderPipelineAsset)"/> inside
    /// the test; the previously-active pipeline asset is restored automatically
    /// during <see cref="TearDown"/>. Renderer assets and renderer datas are
    /// created in memory only (Constitution III — no persisted assets).
    /// </remarks>
    public abstract class PlayModeTestFixture
    {
        protected Camera MainCamera { get; private set; }

        private GameObject _cameraObject;
        private RenderPipelineAsset _previousPipelineAsset;
        private bool _pipelineSwapped;

        [UnitySetUp]
        public virtual IEnumerator SetUp()
        {
            _cameraObject = new GameObject("Render.Tests.Camera");
            MainCamera = _cameraObject.AddComponent<Camera>();
            _previousPipelineAsset = GraphicsSettings.defaultRenderPipeline;
            _pipelineSwapped = false;
            yield return null;
        }

        [UnityTearDown]
        public virtual IEnumerator TearDown()
        {
            if (_pipelineSwapped)
            {
                GraphicsSettings.defaultRenderPipeline = _previousPipelineAsset;
                _pipelineSwapped = false;
            }
            if (_cameraObject != null) Object.DestroyImmediate(_cameraObject);
            _cameraObject = null;
            MainCamera = null;
            yield return null;
        }

        /// <summary>
        /// Sets <see cref="GraphicsSettings.defaultRenderPipeline"/> to the given
        /// asset for the rest of this test; restored automatically in TearDown.
        /// </summary>
        protected void UseRenderPipelineAsset(RenderPipelineAsset asset)
        {
            GraphicsSettings.defaultRenderPipeline = asset;
            _pipelineSwapped = true;
        }
    }
}
