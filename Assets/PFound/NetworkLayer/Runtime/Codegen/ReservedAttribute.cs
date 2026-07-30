using System;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Documents a wire key number that once carried a field and must never be reused, so the append-only
    /// history of a <c>[MessagePackObject]</c> DTO stays readable at a glance. This is a marker for humans
    /// only — it has NO effect on serialization: MessagePack tolerates a gap in the key numbers natively, so a
    /// retired key is already reserved simply by not being declared. The attribute makes that intent explicit
    /// (and searchable during a migration) instead of leaving a silent hole in the <c>[Key(n)]</c> sequence.
    /// </summary>
    /// <remarks>
    /// Applied at the type level and repeatable, one per retired key:
    /// <code>
    /// [MessagePackObject]
    /// [Reserved(2, 1.01)]   // key 2 was still live in v1.01 (inclusive) — reusable once no v1.01-or-older client remains
    /// [Reserved(3)]         // key 3 retired, version no longer tracked
    /// public readonly partial struct PlayerData { /* [Key(0)], [Key(1)], [Key(5)] ... */ }
    /// </code>
    /// <paramref name="usedInVersion"/> is optional — the LAST version in which the key was still live,
    /// INCLUSIVE. It marks the reuse boundary: once no client on that version or older is left in the system,
    /// the key number is safe to repurpose for different data. Kept only to make migrations easy to read and
    /// sort; it is a <see cref="double"/> so versions order numerically and no letters can slip in (e.g.
    /// <c>1.01</c>, <c>1.1</c>, <c>2.0</c>). When the version no longer matters, drop it and keep the bare
    /// <see cref="ReservedAttribute(int)"/>, or remove the marker entirely if the gap no longer needs calling out.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class ReservedAttribute : Attribute
    {
        /// <summary>The retired wire key number that must not be reused.</summary>
        public int Key { get; }

        /// <summary>
        /// The last version in which this key was still live, INCLUSIVE — the reuse boundary: once no client on
        /// this version or older remains, the key may be repurposed. <see cref="double.NaN"/> when not tracked.
        /// </summary>
        public double UsedInVersion { get; }

        /// <summary>Whether a version was recorded for this reserved key.</summary>
        public bool HasVersion => !double.IsNaN(UsedInVersion);

        public ReservedAttribute(int key) : this(key, double.NaN) { }

        public ReservedAttribute(int key, double usedInVersion)
        {
            Key = key;
            UsedInVersion = usedInVersion;
        }
    }
}
