using Unity.Profiling;

namespace PFound.Utilities.Profiling
{
    /// <summary>
    /// Thin factory helpers for creating <see cref="ProfilerMarker"/>s and opening
    /// <see cref="ProfilerScope"/>s, so call sites stay terse:
    /// <c>using (ProfilerMarkers.Sample(_marker)) { ... }</c>.
    /// </summary>
    public static class ProfilerMarkers
    {
        /// <summary>Creates a named marker.</summary>
        public static ProfilerMarker Create(string name) => new ProfilerMarker(name);

        /// <summary>Creates a marker under an explicit profiler category.</summary>
        public static ProfilerMarker Create(ProfilerCategory category, string name) =>
            new ProfilerMarker(category, name);

        /// <summary>Opens a begin/end scope over an existing marker.</summary>
        public static ProfilerScope Sample(ProfilerMarker marker) => new ProfilerScope(marker);

        /// <summary>Creates a marker for <paramref name="name"/> and opens a scope over it in one call.
        /// Prefer caching the marker for hot paths; this overload favors brevity for one-off regions.</summary>
        public static ProfilerScope Sample(string name) => new ProfilerScope(new ProfilerMarker(name));
    }
}
