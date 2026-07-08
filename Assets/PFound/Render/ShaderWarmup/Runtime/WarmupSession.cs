using System;

namespace PFound.Render.ShaderWarmup
{
    /// <summary>
    /// Internal sealed session implementation. Holds a defensive copy of the caller's
    /// <see cref="WarmupBatch"/> array, walks them sequentially via
    /// <c>ShaderVariantCollection.WarmUpProgressively(batchSize)</c>, and exposes weighted
    /// aggregate progress.
    /// </summary>
    internal sealed class WarmupSession : IShaderWarmupSession
    {
        private readonly WarmupBatch[] _batches;
        private int _currentBatchIndex;
        private bool _cancelled;
        private bool _disposed;

        internal WarmupSession(WarmupBatch[] batches)
        {
            if (batches == null) throw new ArgumentNullException(nameof(batches));
            _batches = batches; // already defensive-copied by controller
            _currentBatchIndex = 0;
        }

        /// <inheritdoc />
        public float Progress
        {
            get
            {
                if (_batches.Length == 0) return 1f;
                long totalVariants = 0;
                long warmedVariants = 0;
                for (int i = 0; i < _batches.Length; i++)
                {
                    var c = _batches[i].Collection;
                    totalVariants += c.variantCount;
                    warmedVariants += c.warmedUpVariantCount;
                }
                if (totalVariants <= 0) return 1f;
                return (float)((double)warmedVariants / totalVariants);
            }
        }

        /// <inheritdoc />
        public bool IsComplete => _cancelled || _disposed || _currentBatchIndex >= _batches.Length;

        /// <inheritdoc />
        public void Cancel()
        {
            if (_cancelled || _disposed) return;
            _cancelled = true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cancelled = true;
        }

        /// <summary>
        /// Internal per-tick advancement. Called by <see cref="ShaderWarmupController.Tick"/>.
        /// No-op if the session is already complete (cancelled/disposed/natural). Otherwise calls
        /// <c>WarmUpProgressively(BatchSize)</c> on the current batch; advances the index on true.
        /// </summary>
        internal void Advance()
        {
            if (IsComplete) return;
            var current = _batches[_currentBatchIndex];
            bool done = current.Collection.WarmUpProgressively(current.BatchSize);
            if (done) _currentBatchIndex++;
        }
    }
}
