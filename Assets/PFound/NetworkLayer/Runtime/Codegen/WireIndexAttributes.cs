using System;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Assigns a request-payload field its explicit wire index. The generator copies this number
    /// straight onto the emitted struct's <c>[Key(n)]</c> — the index, NOT declaration order, defines the
    /// wire slot, so reordering source fields never changes the bytes. Indices are append-only: gaps are
    /// legal, but a retired number must never be reused (mark it with <see cref="ReservedAttribute"/>).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class RequestAttribute : Attribute
    {
        public int Index { get; }
        public RequestAttribute(int index) { Index = index; }
    }

    /// <summary>
    /// Assigns a reply-payload field its explicit wire index (only valid on a <see cref="NetworkOpAttribute"/>
    /// target). Same append-only, index-not-order discipline as <see cref="RequestAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class ReplyAttribute : Attribute
    {
        public int Index { get; }
        public ReplyAttribute(int index) { Index = index; }
    }

    /// <summary>
    /// Assigns a notify-payload field its explicit wire index (used on a <see cref="NetworkNotifyAttribute"/>
    /// target, which has no separate reply). Same append-only, index-not-order discipline as
    /// <see cref="RequestAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class FieldAttribute : Attribute
    {
        public int Index { get; }
        public FieldAttribute(int index) { Index = index; }
    }

    /// <summary>
    /// Records one or more retired wire indices on the operation type, so the source itself is the history
    /// of the wire contract (a deleted field's number must never be reused — an old peer's bytes would be
    /// read as a different field). Place it on the operation's <c>partial class</c>:
    /// <c>[Reserved(1)] [Reserved(4, 5)]</c>. The companion analyzer enforces that no live index collides
    /// with a reserved one.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class ReservedAttribute : Attribute
    {
        public int[] Indices { get; }
        public ReservedAttribute(params int[] indices) { Indices = indices; }
    }
}
