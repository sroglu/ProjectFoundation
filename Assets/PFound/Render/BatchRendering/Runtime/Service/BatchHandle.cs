using System;
using System.Threading;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Internal concrete <see cref="IBatchHandle"/> returned by
    /// <see cref="BatchRenderingService.RegisterBatch"/>.
    /// </summary>
    /// <remarks>
    /// All public getters honor the stale-handle contract (see <c>contracts/IBatchHandle.md</c>):
    /// safe to read after own dispose or service dispose; return the last observed value or zero.
    /// <para>
    /// <see cref="Dispose"/> is idempotent via <see cref="Interlocked.CompareExchange(ref int, int, int)"/>
    /// on <see cref="_disposedFlag"/>. The dispose chain:
    /// <list type="number">
    /// <item><description>Mark <see cref="IsAlive"/> = <c>false</c>.</description></item>
    /// <item><description>Dispose <see cref="BackendState"/>.</description></item>
    /// <item><description>Notify the owning service (the service removes from its registry on the
    /// next tick — synchronous removal would invalidate an in-flight enumeration).</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    internal sealed class BatchHandle : IBatchHandle
    {
        private readonly Guid _id;
        private BatchRenderingBatch _descriptor;
        private IBackendState _backendState;
        private readonly OneShotGate _diagnostics;
        private readonly WeakReference<BatchRenderingService> _owner;

        private BatchDegradedReason? _degradedReason;
        private bool _isDegraded;
        private int _disposedFlag;

        private int _registeredInstanceCount;
        private int _lastFrameVisibleCount;

        internal BatchHandle(
            Guid id,
            in BatchRenderingBatch descriptor,
            IBackendState backendState,
            OneShotGate diagnostics,
            BatchRenderingService owner)
        {
            _id = id;
            _descriptor = descriptor;
            _backendState = backendState;
            _diagnostics = diagnostics;
            _owner = new WeakReference<BatchRenderingService>(owner);
        }

        // ---------------- IBatchHandle ----------------

        public bool IsAlive => Volatile.Read(ref _disposedFlag) == 0;

        public bool IsDegraded => _isDegraded;

        public BatchDegradedReason? DegradedReason => _degradedReason;

        public int RegisteredInstanceCount => _registeredInstanceCount;

        public int LastFrameVisibleCount => _lastFrameVisibleCount;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposedFlag, 1) != 0) return; // idempotent

            // Backend cleanup is service-owned native resources; safe to do here on the main thread.
            try
            {
                _backendState?.Dispose();
            }
            finally
            {
                _backendState = null;
            }

            // Notify the owning service for registry removal. Skip if the service has already been
            // disposed (stale handle).
            if (_owner.TryGetTarget(out var svc))
            {
                svc.OnHandleDisposed(this);
            }
        }

        // ---------------- Internal mutators (called by the service) ----------------

        internal Guid Id => _id;
        internal ref readonly BatchRenderingBatch Descriptor => ref _descriptor;
        internal IBackendState BackendState => _backendState;
        internal OneShotGate Diagnostics => _diagnostics;

        /// <summary>
        /// Sets the degradation reason if none has been set yet. Sticky: a later call with a
        /// different reason does NOT overwrite the first one. <see cref="BatchDegradedReason.OcclusionStubActive"/>
        /// is the only reason that does NOT flip <see cref="IsDegraded"/> to <c>true</c> when it is
        /// the sole reason — other reasons always mark the batch fully degraded.
        /// </summary>
        internal void SetDegradedReason(BatchDegradedReason reason)
        {
            if (_degradedReason.HasValue) return; // sticky
            _degradedReason = reason;
            if (reason != BatchRendering.BatchDegradedReason.OcclusionStubActive)
            {
                _isDegraded = true;
            }
        }

        internal void RecordTick(int instanceCount, int visibleCount)
        {
            _registeredInstanceCount = instanceCount;
            _lastFrameVisibleCount = visibleCount;
        }
    }
}
