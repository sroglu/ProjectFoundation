using System.Collections.Generic;
using UnityEngine;

namespace PFound.Utilities.GameObjectTools
{
    /// <summary>
    /// Bulk operations applied across a whole transform subtree: layer assignment,
    /// renderer visibility/tinting, and encapsulating world bounds. Renderer/Collider
    /// walks include inactive members so results are independent of active state.
    /// </summary>
    public static class HierarchyOpsExtensions
    {
        // Shared scratch so per-renderer tinting does not instantiate materials or
        // allocate a new property block on every call.
        private static readonly MaterialPropertyBlock TintBlock = new MaterialPropertyBlock();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Assigns <paramref name="layer"/> to this object and every descendant.</summary>
        public static void SetLayerRecursively(this GameObject root, int layer)
        {
            if (root == null) return;
            root.layer = layer;
            Transform t = root.transform;
            int count = t.childCount;
            for (int i = 0; i < count; i++)
                t.GetChild(i).gameObject.SetLayerRecursively(layer);
        }

        /// <summary>Toggles <see cref="Renderer.enabled"/> on every renderer in the subtree.</summary>
        public static void SetRenderersEnabled(this GameObject root, bool enabled)
        {
            if (root == null) return;
            var buffer = ListPool<Renderer>.Get();
            root.GetComponentsInChildren(true, buffer);
            for (int i = 0; i < buffer.Count; i++)
                buffer[i].enabled = enabled;
            ListPool<Renderer>.Release(buffer);
        }

        /// <summary>
        /// Tints every renderer in the subtree via a shared <see cref="MaterialPropertyBlock"/>,
        /// writing both the built-in <c>_Color</c> and the URP <c>_BaseColor</c> so the
        /// change survives on either pipeline without cloning shared materials.
        /// </summary>
        public static void SetRenderersColor(this GameObject root, Color color)
        {
            if (root == null) return;
            var buffer = ListPool<Renderer>.Get();
            root.GetComponentsInChildren(true, buffer);
            for (int i = 0; i < buffer.Count; i++)
            {
                Renderer renderer = buffer[i];
                renderer.GetPropertyBlock(TintBlock);
                TintBlock.SetColor(ColorId, color);
                TintBlock.SetColor(BaseColorId, color);
                renderer.SetPropertyBlock(TintBlock);
            }
            ListPool<Renderer>.Release(buffer);
        }

        /// <summary>
        /// World-space bounds encapsulating every <see cref="Renderer"/> under
        /// <paramref name="root"/>. When none exist, returns a zero-size bounds at the root.
        /// </summary>
        public static Bounds GetRendererBounds(this Transform root)
        {
            var buffer = ListPool<Renderer>.Get();
            root.GetComponentsInChildren(true, buffer);

            Bounds result;
            if (buffer.Count == 0)
            {
                result = new Bounds(root.position, Vector3.zero);
            }
            else
            {
                result = buffer[0].bounds;
                for (int i = 1; i < buffer.Count; i++)
                    result.Encapsulate(buffer[i].bounds);
            }
            ListPool<Renderer>.Release(buffer);
            return result;
        }

        /// <summary>
        /// World-space bounds encapsulating every <see cref="Collider"/> under
        /// <paramref name="root"/>. When none exist, returns a zero-size bounds at the root.
        /// </summary>
        public static Bounds GetColliderBounds(this Transform root)
        {
            var buffer = ListPool<Collider>.Get();
            root.GetComponentsInChildren(true, buffer);

            Bounds result;
            if (buffer.Count == 0)
            {
                result = new Bounds(root.position, Vector3.zero);
            }
            else
            {
                result = buffer[0].bounds;
                for (int i = 1; i < buffer.Count; i++)
                    result.Encapsulate(buffer[i].bounds);
            }
            ListPool<Collider>.Release(buffer);
            return result;
        }

        // Minimal thread-unsafe list pool for the single-threaded Unity main loop.
        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Free = new Stack<List<T>>();

            public static List<T> Get()
            {
                return Free.Count > 0 ? Free.Pop() : new List<T>(16);
            }

            public static void Release(List<T> list)
            {
                list.Clear();
                Free.Push(list);
            }
        }
    }
}
