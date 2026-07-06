using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Thin MonoBehaviour wrapper around <see cref="IRenderContextService"/> (CODING-STYLE.md §3).
    /// Resolves the service via <see cref="RenderContextResolver.Resolve"/> — host configures
    /// the resolution strategy at boot (singleton, DependencyContainer, delegate, custom). The
    /// wrapper itself is strategy-agnostic. Cross-wrapper pool reuse is preserved when all
    /// wrappers resolve to the same service instance.
    ///
    /// Auto-resolves a sibling <see cref="RawImage"/> or <see cref="MeshRenderer"/> as the
    /// anchor target. For UI Toolkit, set <see cref="_uiDocument"/> + <see cref="_elementName"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RenderContextSinkBehaviour : MonoBehaviour
    {
        [Header("Descriptor")]
        [SerializeField] private int _width = 512;
        [SerializeField] private int _height = 512;
        [SerializeField] private RenderTextureFormat _format = RenderTextureFormat.ARGB32;
        [SerializeField] private int _depthBits = 16;
        [SerializeField, Range(1, 8)] private int _msaa = 1;
        [SerializeField] private RenderTextureReadWrite _colorSpace = RenderTextureReadWrite.Default;
        [SerializeField] private LayerMask _cullingMask = ~0;
        [SerializeField] private CameraClearFlags _clearFlags = CameraClearFlags.SolidColor;
        [SerializeField] private Color _backgroundColor = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private bool _orthographic;
        [SerializeField] private float _orthographicSize = 5f;
        [SerializeField, Range(1f, 179f)] private float _fieldOfView = 60f;

        [Header("UI Toolkit (used if no sibling RawImage/MeshRenderer)")]
        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private string _elementName;

        private IRenderContextService _service;
        private IRenderContextHandle _handle;

        public RenderTexture Texture => _handle != null && _handle.IsAlive ? _handle.Texture : null;
        public Camera Camera => _handle != null && _handle.IsAlive ? _handle.Camera : null;
        public Transform ContentRoot => _handle != null && _handle.IsAlive ? _handle.ContentRoot : null;
        public bool IsAlive => _handle != null && _handle.IsAlive;

        private void OnEnable()
        {
            try
            {
                _service = RenderContextResolver.Resolve();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[RenderContextSinkBehaviour] {ex.Message}", this);
                return;
            }
            var desc = new RenderContextDescriptor
            {
                Width = _width,
                Height = _height,
                Format = _format,
                DepthBits = _depthBits,
                Msaa = _msaa,
                ColorSpace = _colorSpace,
                CullingMask = _cullingMask,
                ClearFlags = _clearFlags,
                BackgroundColor = _backgroundColor,
                Orthographic = _orthographic,
                OrthographicSize = _orthographicSize,
                FieldOfView = _fieldOfView,
            };

            var anchor = ResolveAnchor();
            if (anchor == null)
            {
                Debug.LogWarning($"[RenderContextSinkBehaviour] No anchor target found on '{name}'. Attach a RawImage, MeshRenderer, or assign UIDocument + elementName.", this);
                return;
            }
            try
            {
                _handle = _service.Acquire(desc, anchor);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[RenderContextSinkBehaviour] Acquire failed: {ex.Message}", this);
            }
        }

        private void OnDisable()
        {
            try { _handle?.Dispose(); } catch { /* swallow */ }
            _handle = null;
            // Do NOT dispose the service — it's owned by the host (registry).
            _service = null;
        }

        private IRenderContextAnchor ResolveAnchor()
        {
            var raw = GetComponent<RawImage>();
            if (raw != null) return new RawImageAnchor(raw);

            var mesh = GetComponent<MeshRenderer>();
            if (mesh != null) return new MeshRendererAnchor(mesh);

            if (_uiDocument != null && !string.IsNullOrEmpty(_elementName))
            {
                var root = _uiDocument.rootVisualElement;
                if (root != null)
                {
                    var ve = root.Q<VisualElement>(_elementName);
                    if (ve != null) return new VisualElementAnchor(ve);
                }
            }
            return null;
        }
    }
}
