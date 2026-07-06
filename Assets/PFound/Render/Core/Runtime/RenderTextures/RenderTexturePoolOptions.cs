using System;
using UnityEngine;

namespace PFound.Render.Core.RenderTextures
{
    /// <summary>
    /// Configuration for <see cref="RenderTexturePool"/>. POCO (not a
    /// ScriptableObject) so the submodule never produces persisted assets
    /// (Constitution III).
    /// </summary>
    public sealed class RenderTexturePoolOptions
    {
        /// <summary>Frames a pooled RT can stay unleased before eviction.</summary>
        public int IdleFrameThreshold { get; }

        /// <summary>Frames a lease can stay outstanding before being declared a leak.</summary>
        public int LeakFrameThreshold { get; }

        /// <summary>Max in-flight leak records before oldest is overwritten.</summary>
        public int LeakRingBufferCapacity { get; }

        /// <summary>When true, leaks emit `Debug.LogWarning` in addition to the ring buffer write.</summary>
        public bool LogLeaksToConsole { get; }

        public RenderTexturePoolOptions(
            int idleFrameThreshold = 120,
            int leakFrameThreshold = 600,
            int leakRingBufferCapacity = 64,
            bool? logLeaksToConsole = null)
        {
            if (idleFrameThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(idleFrameThreshold));
            if (leakFrameThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(leakFrameThreshold));
            if (leakRingBufferCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(leakRingBufferCapacity));

            IdleFrameThreshold = idleFrameThreshold;
            LeakFrameThreshold = leakFrameThreshold;
            LeakRingBufferCapacity = leakRingBufferCapacity;
            LogLeaksToConsole = logLeaksToConsole ?? Debug.isDebugBuild;
        }

        /// <summary>Default options (idle=120, leak=600, ring=64, log=Debug.isDebugBuild).</summary>
        public static RenderTexturePoolOptions Default => new RenderTexturePoolOptions();
    }
}
