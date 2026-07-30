using UnityEngine;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Runtime feature-detection for backend support. Probes <see cref="SystemInfo"/> once on first
    /// access and caches the result; never re-probed within a process (host GPU capabilities do not
    /// change at runtime).
    /// </summary>
    /// <remarks>
    /// Per research.md R2 the AND-gated probe for <see cref="BackendKind.Indirect"/> and
    /// <see cref="BackendKind.Procedural"/> is:
    /// <c>SystemInfo.supportsComputeShaders &amp;&amp; SystemInfo.supportsIndirectArgumentsBuffer &amp;&amp; SystemInfo.supportsInstancing</c>.
    /// <see cref="BackendKind.Classic"/> requires no probe (universally supported wherever Unity URP
    /// runs); the per-call cap (<see cref="ClassicPerCallCap"/>) is hard-coded to 1023 per research
    /// R5.
    /// </remarks>
    internal static class BackendCapabilityProbe
    {
        private static bool _probed;
        private static bool _supportsIndirect;
        private static bool _supportsProcedural;
        private static string _missingCapability = string.Empty;

        /// <summary>
        /// <see cref="BackendKind.Classic"/> per-call matrix array limit. Hard-coded to 1023 — the
        /// safe minimum across all target platforms (research.md R5).
        /// </summary>
        internal const int ClassicPerCallCap = 1023;

        internal static bool SupportsIndirect
        {
            get { EnsureProbed(); return _supportsIndirect; }
        }

        internal static bool SupportsProcedural
        {
            get { EnsureProbed(); return _supportsProcedural; }
        }

        /// <summary>
        /// Human-readable name of the missing capability when <see cref="SupportsIndirect"/> or
        /// <see cref="SupportsProcedural"/> is <c>false</c>. Empty string when both are supported.
        /// </summary>
        internal static string MissingCapability
        {
            get { EnsureProbed(); return _missingCapability; }
        }

        private static void EnsureProbed()
        {
            if (_probed) return;

            bool compute = SystemInfo.supportsComputeShaders;
            bool indirectArgs = SystemInfo.supportsIndirectArgumentsBuffer;
            bool instancing = SystemInfo.supportsInstancing;

            _supportsIndirect = compute && indirectArgs && instancing;
            _supportsProcedural = _supportsIndirect; // same gate in v1 per R2

            if (!_supportsIndirect)
            {
                if (!compute) _missingCapability = "compute shader support (SystemInfo.supportsComputeShaders == false)";
                else if (!indirectArgs) _missingCapability = "indirect arguments buffer support (SystemInfo.supportsIndirectArgumentsBuffer == false)";
                else _missingCapability = "GPU instancing support (SystemInfo.supportsInstancing == false)";
            }

            _probed = true;
        }

        /// <summary>
        /// Test-only reset. Tests that mock or swap capability flags use this to force a re-probe.
        /// Not for production use.
        /// </summary>
        internal static void ResetForTests()
        {
            _probed = false;
            _supportsIndirect = false;
            _supportsProcedural = false;
            _missingCapability = string.Empty;
        }
    }
}
