using System;
using Unity.Profiling;

namespace PFound.Utilities.Profiling
{
    /// <summary>
    /// A <c>using</c>-scoped wrapper over <see cref="ProfilerMarker"/>: constructing the scope begins a
    /// sample and disposing it ends the sample, so a profiled region reads as a single indented block.
    /// A <c>readonly struct</c> so it allocates nothing on the hot path.
    /// </summary>
    public readonly struct ProfilerScope : IDisposable
    {
        private readonly ProfilerMarker _marker;

        /// <summary>Begins a sample on <paramref name="marker"/>.</summary>
        public ProfilerScope(ProfilerMarker marker)
        {
            _marker = marker;
            _marker.Begin();
        }

        /// <summary>Ends the sample opened by the constructor.</summary>
        public void Dispose()
        {
            _marker.End();
        }
    }
}
