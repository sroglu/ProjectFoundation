using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Pure-C# implementation of <see cref="IRenderContextService"/>. Owns a hidden
    /// <see cref="DontDestroyOnLoad"/> GameObject that parents pooled Camera entries,
    /// the pool, and the BeforeRender resize watcher.
    /// </summary>
    public sealed class RenderContextService : IRenderContextService, IRenderContextHandleOwner
    {
        private enum FootgunKind { HighMsaa, CullingMaskEverything, ShadowsEnabled }

        // Park offscreen so consumer-scene cameras don't accidentally render context content
        // (the dedicated camera lives way outside any reasonable consumer world). Override
        // requires zero project setup — no dedicated culling layer needed by default.
        private static readonly Vector3 OffscreenAnchor = new Vector3(10000f, 10000f, 10000f);

        private readonly GameObject _ownerGameObject;
        private readonly RenderContextPool _pool;
        private readonly AnchorResizeWatcher _watcher;
        private readonly HashSet<RenderContextHandle> _liveHandles = new();
        private readonly HashSet<object> _boundTargets = new();
        private readonly HashSet<FootgunKind> _emittedWarnings = new();
        private bool _disposed;

        public RenderContextService()
        {
            _ownerGameObject = new GameObject("[RenderContextService]")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(_ownerGameObject);
            _ownerGameObject.transform.position = OffscreenAnchor;

            _pool = new RenderContextPool(_ownerGameObject.transform);
            _watcher = new AnchorResizeWatcher(_ownerGameObject, _pool);
        }

        internal int LiveHandleCount => _liveHandles.Count;
        internal int PoolTotalCount => _pool.TotalCount;
        internal int PoolCount(int width, int height, RenderTextureFormat format, int depthBits, int msaa, RenderTextureReadWrite colorSpace)
            => _pool.Count(new RenderContextPoolKey(width, height, format, depthBits, msaa, colorSpace));

        public IRenderContextHandle Acquire(RenderContextDescriptor descriptor, IRenderContextAnchor anchor)
        {
            ThrowIfDisposed();
            if (anchor == null) throw new ArgumentNullException(nameof(anchor));

            descriptor.Validate();

            if (anchor is IExplicitSizeAnchor && (descriptor.Width == 0 || descriptor.Height == 0))
            {
                throw new ArgumentException(
                    $"{anchor.GetType().Name} requires explicit non-zero descriptor Width and Height.",
                    nameof(descriptor));
            }

            if (!anchor.TargetAlive)
            {
                throw new InvalidOperationException(
                    $"Anchor target is not alive ({anchor.GetType().Name}).");
            }

            var target = anchor.Target;
            if (target != null && !_boundTargets.Add(target))
            {
                throw new InvalidOperationException(
                    "Anchor target already bound by a live handle.");
            }

            int resolvedWidth = descriptor.Width > 0 ? descriptor.Width : anchor.PreferredWidth;
            int resolvedHeight = descriptor.Height > 0 ? descriptor.Height : anchor.PreferredHeight;
            if (resolvedWidth <= 0) resolvedWidth = 1;
            if (resolvedHeight <= 0) resolvedHeight = 1;

            var key = RenderContextPoolKey.FromDescriptor(in descriptor, resolvedWidth, resolvedHeight);
            var entry = _pool.Lease(in key, in descriptor);

            IRenderContextSink sink = null;
            try
            {
                sink = anchor.CreateSink();
                sink.Bind(entry.Rt);
            }
            catch
            {
                _pool.Return(in entry);
                if (target != null) _boundTargets.Remove(target);
                throw;
            }

            EmitFootgunWarnings(in descriptor, entry.Camera);

            var handle = new RenderContextHandle(this, _pool, _watcher, anchor, sink, in entry, in descriptor);
            _liveHandles.Add(handle);
            _watcher.Register(handle);
            return handle;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_liveHandles.Count > 0)
            {
                var snapshot = new RenderContextHandle[_liveHandles.Count];
                _liveHandles.CopyTo(snapshot);
                foreach (var h in snapshot)
                {
                    try { h.Dispose(); }
                    catch (Exception ex) { Debug.LogError($"[RenderContext] Handle dispose failed: {ex}"); }
                }
            }
            _liveHandles.Clear();
            _boundTargets.Clear();

            _watcher.Dispose();
            _pool.Dispose();

            if (_ownerGameObject != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_ownerGameObject);
                else UnityEngine.Object.DestroyImmediate(_ownerGameObject);
            }
        }

        void IRenderContextHandleOwner.NotifyHandleDisposed(RenderContextHandle handle)
        {
            _liveHandles.Remove(handle);
            if (handle.Target != null) _boundTargets.Remove(handle.Target);
        }

        private void EmitFootgunWarnings(in RenderContextDescriptor desc, Camera camera)
        {
            if (desc.Msaa > 2 && _emittedWarnings.Add(FootgunKind.HighMsaa))
            {
                Debug.LogWarning(
                    $"[RenderContext] MSAA={desc.Msaa} — high sample count costs measurably more fillrate; " +
                    $"consider 1 or 2.");
            }

            if (desc.CullingMask.value == ~0 && _emittedWarnings.Add(FootgunKind.CullingMaskEverything))
            {
                Debug.LogWarning(
                    "[RenderContext] CullingMask='Everything' renders all scene layers into the context — " +
                    "usually unintended; assign a dedicated layer.");
            }

            if (camera != null)
            {
                var uacd = camera.GetComponent<UniversalAdditionalCameraData>();
                if (uacd != null && uacd.renderShadows && _emittedWarnings.Add(FootgunKind.ShadowsEnabled))
                {
                    Debug.LogWarning(
                        "[RenderContext] Camera has shadows enabled — typically wasteful for portrait-scale " +
                        "RTs (extra depth pass). Toggle handle.Camera.GetUniversalAdditionalCameraData().renderShadows = false.");
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RenderContextService));
        }
    }
}
