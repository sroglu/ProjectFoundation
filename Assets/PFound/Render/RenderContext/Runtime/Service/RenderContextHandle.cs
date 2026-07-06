using System;
using UnityEngine;

namespace PFound.Render.RenderContext
{
    internal interface IRenderContextHandleOwner
    {
        void NotifyHandleDisposed(RenderContextHandle handle);
    }

    internal sealed class RenderContextHandle : IRenderContextHandle
    {
        private readonly IRenderContextHandleOwner _owner;
        private readonly RenderContextPool _pool;
        private readonly AnchorResizeWatcher _watcher;
        private readonly RenderContextDescriptor _descriptor;
        private IRenderContextAnchor _anchor;
        private IRenderContextSink _sink;
        private PooledEntry _entry;
        private bool _disposed;

        internal RenderContextHandle(
            IRenderContextHandleOwner owner,
            RenderContextPool pool,
            AnchorResizeWatcher watcher,
            IRenderContextAnchor anchor,
            IRenderContextSink sink,
            in PooledEntry entry,
            in RenderContextDescriptor descriptor)
        {
            _owner = owner;
            _pool = pool;
            _watcher = watcher;
            _anchor = anchor;
            _sink = sink;
            _entry = entry;
            _descriptor = descriptor;
            Target = anchor?.Target;
        }

        public RenderTexture Texture
        {
            get { ThrowIfDisposed(); return _entry.Rt; }
        }

        public Camera Camera
        {
            get { ThrowIfDisposed(); return _entry.Camera; }
        }

        public Transform ContentRoot
        {
            get { ThrowIfDisposed(); return _entry.ContentRoot; }
        }

        public bool IsAlive => !_disposed;

        internal IRenderContextAnchor Anchor => _anchor;
        internal int CurrentWidth => _entry.Rt != null ? _entry.Rt.width : 0;
        internal int CurrentHeight => _entry.Rt != null ? _entry.Rt.height : 0;
        internal object Target { get; }

        public void Refresh()
        {
            ThrowIfDisposed();

            int pw = _anchor.PreferredWidth;
            int ph = _anchor.PreferredHeight;
            if (pw <= 0 || ph <= 0) return;
            if (pw == _entry.Rt.width && ph == _entry.Rt.height) return;

            var newKey = RenderContextPoolKey.FromDescriptor(in _descriptor, pw, ph);
            var newEntry = _pool.Lease(in newKey, in _descriptor);

            _sink.Unbind();
            _sink.Bind(newEntry.Rt);

            var oldEntry = _entry;
            _entry = newEntry;
            _pool.Return(in oldEntry);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _sink?.Unbind();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RenderContext] Sink unbind failed: {ex}");
            }

            if (_pool != null && _entry.Rt != null)
            {
                _pool.Return(in _entry);
            }

            _watcher?.Unregister(this);
            _owner?.NotifyHandleDisposed(this);

            _sink = null;
            _anchor = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RenderContextHandle));
        }
    }
}
