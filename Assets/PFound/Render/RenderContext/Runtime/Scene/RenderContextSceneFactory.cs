using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Builds the per-entry hierarchy (wrapper root + Camera child + ContentRoot child) and
    /// resets per-lease camera properties on each Lease via <see cref="ResetCamera"/>.
    ///
    /// Topology — Camera and ContentRoot are SIBLINGS, both children of a wrapper "context root".
    /// This isolates camera placement from content placement: moving the camera does not drag
    /// content with it, and vice versa. The wrapper is what the pool tracks for lifecycle.
    /// </summary>
    internal static class RenderContextSceneFactory
    {
        internal static (GameObject root, Camera camera, Transform contentRoot) BuildHierarchy(
            Transform ownerParent,
            in RenderContextPoolKey key,
            RenderTexture rt)
        {
            var root = new GameObject($"RenderContextRoot_{key}");
            root.transform.SetParent(ownerParent, worldPositionStays: false);
            root.hideFlags = HideFlags.DontSave;

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(root.transform, worldPositionStays: false);
            camGo.transform.localPosition = new Vector3(0f, 0.4f, -3.5f);
            camGo.transform.localRotation = Quaternion.LookRotation(Vector3.forward * 1f + Vector3.down * 0.1f, Vector3.up);

            var camera = camGo.AddComponent<Camera>();
            camera.targetTexture = rt;
            camera.enabled = true;

            // URP additional camera data — registers the camera with URP so post-process
            // volumes, camera-stack flags, render-shadows toggle become available.
            // Required for SubmitRenderRequest support (Camera.Render alone bypasses
            // URP's renderer setup on some paths).
            var uacd = camGo.AddComponent<UniversalAdditionalCameraData>();
            uacd.renderType = CameraRenderType.Base;
            uacd.renderPostProcessing = false; // disabled by default; consumer opt-in
            uacd.renderShadows = false;        // off by default — footgun warning territory
            uacd.dithering = false;

            var contentRootGo = new GameObject("ContentRoot");
            contentRootGo.transform.SetParent(root.transform, worldPositionStays: false);
            var contentRoot = contentRootGo.transform;

            return (root, camera, contentRoot);
        }

        internal static void ResetCamera(Camera camera, in RenderContextDescriptor desc)
        {
            camera.cullingMask = desc.CullingMask;
            camera.clearFlags = desc.ClearFlags;
            camera.backgroundColor = desc.BackgroundColor;
            camera.orthographic = desc.Orthographic;
            if (desc.Orthographic)
                camera.orthographicSize = desc.OrthographicSize;
            else
                camera.fieldOfView = desc.FieldOfView;
        }

        internal static void DestroyChildren(Transform parent)
        {
            if (parent == null) return;
            // Detach-then-destroy: Unity does not guarantee parent.childCount drops on the
            // same statement as DestroyImmediate in EditMode (caught by pool test failure).
            // Detaching first immediately drops the count; the GO destruction happens after.
            while (parent.childCount > 0)
            {
                var child = parent.GetChild(0);
                child.SetParent(null, worldPositionStays: false);
                if (Application.isPlaying)
                    Object.Destroy(child.gameObject);
                else
                    Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}
