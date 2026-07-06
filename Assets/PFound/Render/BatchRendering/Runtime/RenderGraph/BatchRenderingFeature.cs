using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using PFound.Render.Core.Pipeline;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// URP <see cref="ScriptableRendererFeature"/> that draws registered
    /// <see cref="BatchRenderingBatch"/> entries with
    /// <see cref="BatchRenderingBatch.participatesInRenderGraph"/> = <c>true</c> inside the URP
    /// RenderGraph at the configured injection point (default
    /// <see cref="RenderPassEvent.AfterRenderingOpaques"/>).
    /// </summary>
    /// <remarks>
    /// The feature does NOT own a <see cref="IBatchRenderingService"/> instance — the consumer
    /// constructs the service and calls <see cref="AttachService"/> from their bootstrap (typically
    /// from a MonoBehaviour <c>Start</c>). On scene shutdown the consumer calls
    /// <see cref="DetachService"/> alongside their <c>service.Dispose()</c> call.
    /// <para>
    /// The feature holds a <see cref="WeakReference{T}"/> to the service so a consumer disposing
    /// the service without first calling <see cref="DetachService"/> doesn't leak; the pass
    /// gracefully no-ops on a dead reference (with a one-shot diagnostic).
    /// </para>
    /// </remarks>
    public sealed class BatchRenderingFeature : RenderFeatureBase
    {
        /// <summary>URP injection point at which the BatchRendering pass executes. Default
        /// <see cref="RenderPassEvent.AfterRenderingOpaques"/> (research.md R4).</summary>
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingOpaques;

        private BatchRenderingRenderGraphPass _pass;
        private WeakReference<IBatchRenderingService> _serviceRef;
        private readonly Guid _featureId = Guid.NewGuid();
        private readonly OneShotGate _diagnostics = new OneShotGate();

        /// <summary>
        /// Binds <paramref name="service"/> to this feature. Subsequent frames route the
        /// configured pass through the service's registered RenderGraph batches.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="service"/> is null.</exception>
        public void AttachService(IBatchRenderingService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            _serviceRef = new WeakReference<IBatchRenderingService>(service);
        }

        /// <summary>Clears the service reference. Subsequent frames are a clean no-op.</summary>
        public void DetachService()
        {
            _serviceRef = null;
        }

        /// <inheritdoc/>
        protected override void OnCreate()
        {
            _pass = new BatchRenderingRenderGraphPass(this, injectionPoint);
            EnqueuePass(_pass);
        }

        /// <inheritdoc/>
        protected override void OnDispose()
        {
            _serviceRef = null;
            _pass = null;
        }

        // ---------------- Internal accessors for the pass ----------------

        internal Guid FeatureId => _featureId;
        internal OneShotGate Diagnostics => _diagnostics;

        /// <summary>
        /// Resolves the attached service. Returns <c>true</c> when a live service is available;
        /// <c>false</c> when not attached or the service has been disposed (weak ref dead).
        /// </summary>
        internal bool TryGetService(out BatchRenderingService service)
        {
            if (_serviceRef == null)
            {
                service = null;
                return false;
            }

            if (!_serviceRef.TryGetTarget(out var iface) || iface == null)
            {
                service = null;
                return false;
            }

            // Internal helpers live on the concrete type. Consumers wire only via the interface.
            service = iface as BatchRenderingService;
            return service != null;
        }
    }
}
