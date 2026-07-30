using System;
using System.Collections.Generic;

namespace PFound.Render.PostProcess
{
    /// <summary>
    /// Adapter contract — owns the per-effect stack and writes the resolved value into the
    /// underlying VolumeComponent. Built-in adapters live in this asmdef; custom adapters
    /// implement <see cref="IRenderPostProcessAdapter{TRequest}"/> and pass the instance to
    /// <c>RenderPostProcessRegistration.Register(...)</c>.
    /// </summary>
    public interface IRenderPostProcessAdapter : IDisposable
    {
        /// <summary>The strongly-typed request payload this adapter accepts.</summary>
        Type RequestType { get; }

        /// <summary>Called once during service construction. Adapter may capture the service back-reference.</summary>
        void Initialize(IRenderPostProcess service);

        /// <summary>
        /// Called by the service each BeforeRender tick. The adapter advances its own
        /// fade timers and invokes its own <see cref="IRenderPostProcessAdapter{TRequest}.ResolveAndApply"/>.
        /// </summary>
        void TickFrame(float deltaTime);
    }

    /// <summary>
    /// Generic adapter — implements the per-request payload resolve.
    /// </summary>
    /// <typeparam name="TRequest">Strongly-typed payload struct (e.g., <c>BlurRequest</c>).</typeparam>
    public interface IRenderPostProcessAdapter<TRequest> : IRenderPostProcessAdapter
        where TRequest : struct
    {
        /// <summary>The blend policy this adapter uses for stack resolution.</summary>
        PostProcessBlendPolicy Policy { get; }

        /// <summary>
        /// Resolves <paramref name="stack"/> to a single effective value and writes it to the
        /// adapter's underlying VolumeComponent. The list is a read-only snapshot owned by the
        /// service; adapters MUST NOT cache it.
        /// </summary>
        void ResolveAndApply(
            IReadOnlyList<ActiveRequest<TRequest>> stack,
            float deltaTime);
    }
}
