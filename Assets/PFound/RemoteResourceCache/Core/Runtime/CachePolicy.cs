using System;

namespace PFound.RemoteResourceCache.Core
{
    /// <summary>
    /// Tuning for what each tier retains, how it evicts, and how long disk entries stay valid. Both tiers are
    /// bounded by an entry count and/or a byte cap (0 = unbounded on that axis) and each picks its own
    /// <see cref="EvictionStrategy"/>. Every field has a permissive default, so an empty policy is a valid one.
    /// </summary>
    public sealed class CachePolicy
    {
        // ---- memory tier ----
        public int MaxMemoryCount { get; set; }
        public long MaxMemoryBytes { get; set; }
        public EvictionStrategy MemoryEviction { get; set; } = EvictionStrategy.Lru;

        // ---- disk tier ----
        /// <summary>Max entries retained on disk; 0 = unbounded by count. The disk tier ENFORCES this.</summary>
        public int MaxDiskEntries { get; set; }

        /// <summary>Max total bytes retained on disk; 0 = unbounded by bytes. The disk tier ENFORCES this.</summary>
        public long MaxDiskBytes { get; set; }

        public EvictionStrategy DiskEviction { get; set; } = EvictionStrategy.Lru;

        /// <summary>How often the disk metadata index is allowed to flush to its sidecar (debounced). Zero = flush on every mutation.</summary>
        public TimeSpan DiskFlushInterval { get; set; } = TimeSpan.FromSeconds(5);

        // ---- shared ----
        /// <summary>How long a disk blob stays valid before it is treated as a miss and re-fetched; <see cref="TimeSpan.Zero"/> = never expires.</summary>
        public TimeSpan Ttl { get; set; }

        /// <summary>Optional gate for what may be cached; null = cache everything. Non-cacheable keys are still served, just not retained.</summary>
        public Func<string, bool> Cacheable { get; set; }

        public bool CanCache(string key) => Cacheable == null || Cacheable(key);

        public static CachePolicy Default => new CachePolicy();
    }
}
