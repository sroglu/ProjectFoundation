using System;
using UnityEngine;

namespace PFound.Render.ShaderWarmup
{
    /// <summary>
    /// Pair of <see cref="ShaderVariantCollection"/> + per-tick variant budget. Passed to
    /// <see cref="IShaderWarmupController.BeginSession(WarmupBatch[])"/>. Value type — no heap
    /// allocation when constructed inline at the call site.
    /// </summary>
    public readonly struct WarmupBatch
    {
        /// <summary>The shader variant collection to warm up. Never null.</summary>
        public readonly ShaderVariantCollection Collection;

        /// <summary>Per-tick variant budget passed to <c>WarmUpProgressively(BatchSize)</c>. Always ≥ 1.</summary>
        public readonly int BatchSize;

        /// <summary>
        /// Constructs a batch. Eagerly validates — invalid batches cannot reach a session.
        /// </summary>
        /// <param name="collection">Non-null SVC reference.</param>
        /// <param name="batchSize">Per-tick variant budget; must be ≥ 1.</param>
        /// <exception cref="ArgumentNullException">When <paramref name="collection"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">When <paramref name="batchSize"/> &lt; 1.</exception>
        public WarmupBatch(ShaderVariantCollection collection, int batchSize)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (batchSize < 1) throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "batchSize must be ≥ 1.");
            Collection = collection;
            BatchSize = batchSize;
        }
    }
}
