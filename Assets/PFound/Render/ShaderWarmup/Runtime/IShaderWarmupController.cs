using System;
using System.Collections.Generic;

namespace PFound.Render.ShaderWarmup
{
    /// <summary>
    /// Phase 7 ShaderWarmup orchestration service. Wraps Unity's
    /// <c>ShaderVariantCollection.WarmUpProgressively(int)</c> across one or more batches in a
    /// time-sliced session. Pipeline-agnostic (Built-in / URP / HDRP). Ticks once per frame via
    /// <c>PFound.LoopScheduler</c> BeforeRender.
    /// </summary>
    public interface IShaderWarmupController : IDisposable
    {
        /// <summary>
        /// Begin a warmup session over the supplied batches. Empty batch list returns an
        /// already-complete session. Throws on null enumerable or null
        /// <see cref="WarmupBatch.Collection"/> inside any batch.
        /// </summary>
        /// <exception cref="ArgumentNullException">When <paramref name="batches"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">When the controller has been disposed.</exception>
        IShaderWarmupSession BeginSession(params WarmupBatch[] batches);

        /// <summary>Begin a warmup session from an enumerable. Same semantics as the params overload.</summary>
        IShaderWarmupSession BeginSession(IEnumerable<WarmupBatch> batches);

        /// <summary>
        /// Toggles <c>UnityEngine.Rendering.GraphicsSettings.logWhenShaderIsCompiled</c>. Original
        /// value is captured on controller construction and restored on
        /// <see cref="IDisposable.Dispose"/>. Setting this to false (or disposing the controller)
        /// restores the original.
        /// </summary>
        bool DiagnosticMode { get; set; }

        /// <summary>Live view of in-progress sessions; auto-pruned each tick as sessions complete.</summary>
        IReadOnlyList<IShaderWarmupSession> ActiveSessions { get; }
    }
}
