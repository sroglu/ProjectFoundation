using UnityEngine;
using UnityEngine.Rendering;

namespace PFound.Render.Utilities
{
    /// <summary>
    /// Small, allocation-conscious helpers for common runtime rendering chores:
    /// switching a Standard-shader material into an alpha-blended mode, applying a
    /// camera "scissor" viewport via a projection-matrix skew, colour tinting and
    /// assigning shared materials onto a renderer.
    /// </summary>
    public static class RenderingTools
    {
        // Property ids for the Standard shader blend/depth state. Cached so repeated
        // mode switches don't re-hash the strings.
        static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        static readonly int ZWriteId   = Shader.PropertyToID("_ZWrite");
        static readonly int ColorId    = Shader.PropertyToID("_Color");

        const string AlphaBlendKeyword   = "_ALPHABLEND_ON";
        const string AlphaPremulKeyword  = "_ALPHAPREMULTIPLY_ON";
        const string AlphaTestKeyword    = "_ALPHATEST_ON";

        /// <summary>
        /// Reconfigures a Standard-shader material for straight alpha blending
        /// ("Fade"): geometry fades out including its specular/reflection response.
        /// </summary>
        public static void SetMaterialFade(Material material)
        {
            ApplyBlendState(
                material,
                BlendMode.SrcAlpha,
                BlendMode.OneMinusSrcAlpha,
                AlphaBlendKeyword);
        }

        /// <summary>
        /// Reconfigures a Standard-shader material for premultiplied "Transparent"
        /// blending, where highlights and reflections survive as the surface fades.
        /// </summary>
        public static void SetMaterialTransparent(Material material)
        {
            ApplyBlendState(
                material,
                BlendMode.One,
                BlendMode.OneMinusSrcAlpha,
                AlphaPremulKeyword);
        }

        static void ApplyBlendState(Material material, BlendMode source, BlendMode destination, string blendKeyword)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt(SrcBlendId, (int)source);
            material.SetInt(DstBlendId, (int)destination);
            material.SetInt(ZWriteId, 0);

            material.DisableKeyword(AlphaTestKeyword);
            material.DisableKeyword(AlphaBlendKeyword);
            material.DisableKeyword(AlphaPremulKeyword);
            material.EnableKeyword(blendKeyword);

            material.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>
        /// Restricts a camera's rendered output to a sub-region of the viewport
        /// ("scissor") without shrinking the frustum, by skewing the projection
        /// matrix. The rect is expressed in normalised viewport coordinates (0..1)
        /// and is clamped to the visible area.
        /// </summary>
        public static void SetCameraScissor(Camera camera, Rect viewportRect)
        {
            // Clamp the requested rect into the unit viewport.
            if (viewportRect.x < 0f) { viewportRect.width += viewportRect.x; viewportRect.x = 0f; }
            if (viewportRect.y < 0f) { viewportRect.height += viewportRect.y; viewportRect.y = 0f; }
            viewportRect.width  = Mathf.Min(1f - viewportRect.x, viewportRect.width);
            viewportRect.height = Mathf.Min(1f - viewportRect.y, viewportRect.height);

            // Capture the untouched full-screen projection first.
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.ResetProjectionMatrix();
            Matrix4x4 baseProjection = camera.projectionMatrix;

            // Hand the same rect to the viewport so clears/aspect stay consistent.
            camera.rect = viewportRect;

            float invW = 1f / viewportRect.width;
            float invH = 1f / viewportRect.height;

            // Compose: place origin, scale up so the sub-rect fills clip space,
            // then recentre. Result is baseProjection pre-multiplied by the skew.
            Matrix4x4 place = Matrix4x4.TRS(
                new Vector3(viewportRect.x, viewportRect.y, 0f),
                Quaternion.identity,
                Vector3.one);

            Matrix4x4 scale = Matrix4x4.TRS(
                new Vector3(invW - 1f, invH - 1f, 0f),
                Quaternion.identity,
                new Vector3(invW, invH, 1f));

            Matrix4x4 recentre = Matrix4x4.TRS(
                new Vector3(-viewportRect.x * 2f * invW, -viewportRect.y * 2f * invH, 0f),
                Quaternion.identity,
                Vector3.one);

            camera.projectionMatrix = recentre * scale * place * baseProjection;
        }

        /// <summary>
        /// Multiplies the material's main colour (<c>_Color</c>) by the supplied tint.
        /// </summary>
        public static void TintMaterial(Material material, Color tint)
        {
            Color current = material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
            material.SetColor(ColorId, current * tint);
        }

        /// <summary>
        /// Assigns the given materials as the renderer's shared materials. Passing a
        /// single material is the common case; the params overload keeps that tidy.
        /// </summary>
        public static void SetSharedMaterials(Renderer renderer, params Material[] materials)
        {
            renderer.sharedMaterials = materials;
        }
    }
}
