using System;
using System.Collections.Generic;
using UnityEngine;
using CoreRT = PFound.Render.Core.RenderTextures;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Composite-entry pool keyed by <see cref="RenderContextPoolKey"/>. Delegates the
    /// <see cref="RenderTexture"/> lifecycle to <see cref="CoreRT.RenderTexturePool"/>
    /// (idle eviction + leak detection for free) and maintains a sidecar of
    /// <see cref="Camera"/> + <see cref="ContentRoot"/> per descriptor variant.
    ///
    /// <para>
    /// ColorSpace folding (Linear / sRGB / Default → <c>GraphicsFormat</c>) is delegated to the
    /// Core <see cref="CoreRT.RenderTextureKey"/> overload that takes
    /// <see cref="RenderTextureFormat"/> + <see cref="RenderTextureReadWrite"/>. Linear and sRGB
    /// requests for the same logical format yield distinct Core keys, so Core's pool correctly
    /// distinguishes them.
    /// </para>
    /// </summary>
    internal sealed class RenderContextPool : IDisposable
    {
        private struct SidecarEntry
        {
            public GameObject Root;
            public Camera Camera;
            public Transform ContentRoot;
        }

        private readonly Transform _ownerParent;
        private readonly CoreRT.RenderTexturePool _rtPool;
        private readonly Dictionary<RenderContextPoolKey, Stack<SidecarEntry>> _sidecars = new();
        private bool _disposed;

        public RenderContextPool(Transform ownerParent)
        {
            _ownerParent = ownerParent ?? throw new ArgumentNullException(nameof(ownerParent));
            _rtPool = new CoreRT.RenderTexturePool();
        }

        public PooledEntry Lease(in RenderContextPoolKey ourKey, in RenderContextDescriptor desc)
        {
            ThrowIfDisposed();

            var coreKey = new CoreRT.RenderTextureKey(
                ourKey.Width, ourKey.Height,
                ourKey.Format, ourKey.ColorSpace,
                ourKey.DepthBits, ourKey.Msaa);

            var lease = _rtPool.Lease(in coreKey);
            var rt = lease.RT;

            SidecarEntry sidecar;
            if (_sidecars.TryGetValue(ourKey, out var stack) && stack.Count > 0)
            {
                sidecar = stack.Pop();
                if (sidecar.Root != null) sidecar.Root.SetActive(true);
                if (sidecar.Camera != null)
                {
                    sidecar.Camera.targetTexture = rt;
                    RenderContextSceneFactory.ResetCamera(sidecar.Camera, desc);
                }
            }
            else
            {
                var (root, camera, contentRoot) = RenderContextSceneFactory.BuildHierarchy(_ownerParent, ourKey, rt);
                RenderContextSceneFactory.ResetCamera(camera, desc);
                sidecar = new SidecarEntry { Root = root, Camera = camera, ContentRoot = contentRoot };
            }

            return new PooledEntry
            {
                Lease = lease,
                Rt = rt,
                Root = sidecar.Root,
                Camera = sidecar.Camera,
                ContentRoot = sidecar.ContentRoot,
                Key = ourKey,
            };
        }

        public void Return(in PooledEntry entry)
        {
            ThrowIfDisposed();

            RenderContextSceneFactory.DestroyChildren(entry.ContentRoot);
            if (entry.Camera != null) entry.Camera.targetTexture = null;
            if (entry.Root != null) entry.Root.SetActive(false);

            _rtPool.Release(in entry.Lease);

            if (!_sidecars.TryGetValue(entry.Key, out var stack))
            {
                stack = new Stack<SidecarEntry>();
                _sidecars[entry.Key] = stack;
            }
            stack.Push(new SidecarEntry { Root = entry.Root, Camera = entry.Camera, ContentRoot = entry.ContentRoot });
        }

        public int Count(in RenderContextPoolKey key)
            => _sidecars.TryGetValue(key, out var stack) ? stack.Count : 0;

        public int TotalCount
        {
            get
            {
                int total = 0;
                foreach (var stack in _sidecars.Values) total += stack.Count;
                return total;
            }
        }

        /// <summary>Per-frame eviction sweep driver (delegates to Core pool's <c>Tick</c>).</summary>
        public void Tick(int currentFrame)
        {
            if (_disposed) return;
            _rtPool.Tick(currentFrame);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Drain the returned-sidecar stacks first. These have `camera.targetTexture` already
            // set to null (cleared in Return) so destroying them in any order is safe.
            foreach (var stack in _sidecars.Values)
            {
                while (stack.Count > 0)
                {
                    var s = stack.Pop();
                    if (s.Root != null)
                    {
                        if (Application.isPlaying) UnityEngine.Object.Destroy(s.Root);
                        else UnityEngine.Object.DestroyImmediate(s.Root);
                    }
                }
            }
            _sidecars.Clear();

            // Outstanding leases (consumer didn't call Return before Dispose) still have a Camera
            // child of `_ownerParent` with `targetTexture` pointing at the RT we're about to
            // release. Walk all child Cameras, null their targetTexture, and destroy the GO BEFORE
            // we dispose the RT pool — otherwise the Core RenderTexturePool emits
            // "Releasing render texture that is set as Camera.targetTexture!" warnings.
            if (_ownerParent != null)
            {
                // Iterate by index (collection mutates as we destroy children).
                for (int i = _ownerParent.childCount - 1; i >= 0; i--)
                {
                    var child = _ownerParent.GetChild(i);
                    if (child == null) continue;
                    var cam = child.GetComponentInChildren<Camera>(true);
                    if (cam != null) cam.targetTexture = null;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(child.gameObject);
                    else UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            _rtPool.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RenderContextPool));
        }
    }
}
