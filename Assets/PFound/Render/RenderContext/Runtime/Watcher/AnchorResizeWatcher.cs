using System;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.Render.RenderContext
{
    /// <summary>
    /// Per-frame BeforeRender tick that polls each live handle's anchor for size changes + liveness.
    /// Re-entrancy safe: <see cref="Unregister"/> calls during <see cref="Tick"/> defer to a pending list.
    /// </summary>
    internal sealed class AnchorResizeWatcher : IDisposable
    {
        private readonly List<RenderContextHandle> _handles = new();
        private readonly List<RenderContextHandle> _pendingRemove = new();
        private readonly GameObject _owner;
        private readonly RenderContextPool _pool;
        private bool _ticking;
        private bool _disposed;

        internal AnchorResizeWatcher(GameObject owner, RenderContextPool pool)
        {
            _owner = owner;
            _pool = pool;
            PFound.LoopScheduler.LoopScheduler.RegisterBeforeRenderLoop(Tick, owner);
        }

        internal void Register(RenderContextHandle handle)
        {
            if (_disposed) return;
            _handles.Add(handle);
        }

        internal void Unregister(RenderContextHandle handle)
        {
            if (_disposed) return;
            if (_ticking)
                _pendingRemove.Add(handle);
            else
                _handles.Remove(handle);
        }

        internal int LiveHandleCount => _handles.Count;

        internal void Tick()
        {
            _ticking = true;
            try
            {
                // Drive Core's pool tick (idle-frame eviction + leak detection sweep).
                _pool?.Tick(Time.frameCount);

                for (int i = 0; i < _handles.Count; i++)
                {
                    var h = _handles[i];
                    if (!h.IsAlive) continue;

                    var anchor = h.Anchor;
                    if (anchor == null) continue;

                    if (!anchor.TargetAlive)
                    {
                        Debug.LogWarning(
                            $"[RenderContext] Anchor target destroyed externally; auto-disposing handle " +
                            $"({anchor.GetType().Name}, was {h.CurrentWidth}×{h.CurrentHeight}).");
                        h.Dispose();
                        continue;
                    }

                    int pw = anchor.PreferredWidth;
                    int ph = anchor.PreferredHeight;
                    if (pw <= 0 || ph <= 0) continue;
                    if (pw == h.CurrentWidth && ph == h.CurrentHeight) continue;

                    try
                    {
                        h.Refresh();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[RenderContext] Refresh failed: {ex}");
                    }
                }
            }
            finally
            {
                _ticking = false;
                if (_pendingRemove.Count > 0)
                {
                    foreach (var h in _pendingRemove) _handles.Remove(h);
                    _pendingRemove.Clear();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            PFound.LoopScheduler.LoopScheduler.DeregisterBeforeRenderLoop(Tick);
            _handles.Clear();
            _pendingRemove.Clear();
        }
    }
}
