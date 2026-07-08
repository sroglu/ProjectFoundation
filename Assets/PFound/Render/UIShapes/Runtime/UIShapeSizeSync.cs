using UnityEngine;
using UnityEngine.UI;

namespace PFound.Render.UIShapes
{
    /// <summary>
    /// Auto-syncs the <c>_QuadSize</c> and <c>_RectSize</c> shader properties on a UI Graphic's
    /// material from its <see cref="UnityEngine.RectTransform"/> size, so the shape always fits
    /// the RectTransform regardless of resize.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Drop this on any GameObject with a <see cref="Graphic"/> (Image, RawImage, etc.) using
    /// the <c>Render/UI/Shape</c> shader. On <see cref="OnEnable"/> + every
    /// <see cref="OnRectTransformDimensionsChange"/> the component writes:
    /// </para>
    /// <list type="bullet">
    ///   <item><c>_QuadSize ← rect.size</c></item>
    ///   <item><c>_RectSize ← rect.size − 2 × <see cref="Margin"/></c></item>
    /// </list>
    /// <para>
    /// The <see cref="Margin"/> is inset from the RectTransform edge to the visible shape
    /// silhouette — increase it to leave room for outline + shadow that extend beyond the
    /// shape (default <c>0</c> = shape fills the quad).
    /// </para>
    /// <para>
    /// <b>Important:</b> this component <b>writes directly to <see cref="Graphic.material"/></b> —
    /// the material that the Graphic is currently using. If multiple Graphics share a single
    /// material asset, they will overwrite each other's <c>_RectSize/_QuadSize</c> values
    /// (last writer wins). For per-instance sizing, assign a cloned Material to each Graphic
    /// before this component runs (standard Unity pattern: <c>image.material = new Material(asset);</c>).
    /// </para>
    /// </remarks>
    [ExecuteAlways]
    [AddComponentMenu("Render/UIShapes/UI Shape Size Sync")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIShapeSizeSync : MonoBehaviour
    {
        [SerializeField, Min(0f),
            Tooltip("Inset from the RectTransform edge to the visible shape silhouette in pixels. " +
                    "Increase to leave room for outline thickness + shadow blur extending beyond " +
                    "the shape. 0 = shape fills the quad (default).")]
        private float _margin = 0f;

        private Graphic _graphic;
        private RectTransform _rectTransform;

        /// <summary>Inset margin in pixels (≥ 0). Increase to leave room for outline / shadow overshoot.</summary>
        public float Margin
        {
            get => _margin;
            set
            {
                _margin = Mathf.Max(0f, value);
                Sync();
            }
        }

        /// <summary>The active material this component writes to (the Graphic's current material).</summary>
        public Material ActiveMaterial => _graphic != null ? _graphic.material : null;

        private void OnEnable()
        {
            _rectTransform = transform as RectTransform;
            _graphic = GetComponent<Graphic>();
            Sync();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled) return;
            Sync();
        }

        /// <summary>
        /// Pushes the current RectTransform size into the active material's
        /// <c>_QuadSize</c> and (after <see cref="Margin"/> inset) <c>_RectSize</c>.
        /// Called automatically on enable and on RectTransform changes.
        /// </summary>
        public void Sync()
        {
            if (_graphic == null) _graphic = GetComponent<Graphic>();
            if (_rectTransform == null) _rectTransform = transform as RectTransform;
            var mat = ActiveMaterial;
            if (mat == null || _rectTransform == null) return;

            var size = _rectTransform.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;

            mat.SetVector(UIShapeMaterialProperties.QuadSize, new Vector4(size.x, size.y, 0f, 0f));

            float shapeW = Mathf.Max(size.x - 2f * _margin, 1f);
            float shapeH = Mathf.Max(size.y - 2f * _margin, 1f);
            mat.SetVector(UIShapeMaterialProperties.RectSize, new Vector4(shapeW, shapeH, 0f, 0f));
        }
    }
}
