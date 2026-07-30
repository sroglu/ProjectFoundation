using System;

namespace PFound.Render.PostProcess
{
    /// <summary>
    /// Internal ticket implementation. Sealed; instantiated by <see cref="RenderPostProcessService"/>
    /// only. Idempotent <see cref="Release"/> + <see cref="Dispose"/> equivalence.
    /// </summary>
    internal sealed class RenderPostProcessTicket : IRenderPostProcessTicket
    {
        private RenderPostProcessService _owner;
        private readonly int _adapterTypeHandle;
        private readonly int _slotId;
        private readonly int _generation;
        private bool _released;

        internal RenderPostProcessTicket(RenderPostProcessService owner, int adapterTypeHandle, int slotId, int generation)
        {
            _owner = owner;
            _adapterTypeHandle = adapterTypeHandle;
            _slotId = slotId;
            _generation = generation;
        }

        /// <inheritdoc />
        public bool IsReleased => _released;

        /// <inheritdoc />
        public void Release()
        {
            if (_released) return;
            _released = true;

            var owner = _owner;
            _owner = null;
            owner?.ReleaseInternal(_adapterTypeHandle, _slotId, _generation);
        }

        /// <inheritdoc />
        public void Dispose() => Release();
    }
}
