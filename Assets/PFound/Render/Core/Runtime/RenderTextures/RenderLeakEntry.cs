using Unity.Collections;

namespace PFound.Render.Core.RenderTextures
{
    /// <summary>
    /// Burst-compatible record describing one leaked <see cref="RenderTextureLease"/>.
    /// Stored in the pool's leak ring buffer. All fields are unmanaged primitives
    /// + a fixed-size string for the key.
    /// </summary>
    public readonly struct RenderLeakEntry
    {
        /// <summary>Human-readable key string (from <see cref="RenderTextureKey.ToString"/>).</summary>
        public readonly FixedString64Bytes Key;

        /// <summary>Frame when the lease was issued.</summary>
        public readonly int LeasedFrame;

        /// <summary>Frame when the leak was detected (or pool-dispose frame).</summary>
        public readonly int ReportedFrame;

        /// <summary>Managed thread id of the leasing call.</summary>
        public readonly int ThreadId;

        public RenderLeakEntry(in FixedString64Bytes key, int leasedFrame, int reportedFrame, int threadId)
        {
            Key = key;
            LeasedFrame = leasedFrame;
            ReportedFrame = reportedFrame;
            ThreadId = threadId;
        }
    }
}
