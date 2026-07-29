using System;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Marks a <c>partial class</c> as an RPC operation (a request paired with a reply). The source
    /// generator reads the fields tagged with <see cref="RequestAttribute"/> / <see cref="ReplyAttribute"/>
    /// and emits the immutable wire structs, the poolable request/reply envelopes, and the catalog
    /// enrolment — so a game declares one compact partial instead of hand-writing all the boilerplate.
    /// </summary>
    /// <remarks>
    /// <paramref name="domain"/> and <paramref name="op"/> are the same banded pair the catalog consumes
    /// (a per-game <c>NetDomain</c> value + a per-domain op value). They are typed <see cref="object"/> so
    /// any two enums can be passed as attribute constants; the generator recovers each enum type and value
    /// to fold the wire opcode exactly as <see cref="MessageCatalog.Enroll{TRequest, TReply}(Enum, Enum)"/>
    /// does. The reply is decoded by correlation and carries no opcode of its own.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class NetworkOpAttribute : Attribute
    {
        public object Domain { get; }
        public object Op { get; }

        public NetworkOpAttribute(object domain, object op)
        {
            Domain = domain;
            Op = op;
        }
    }
}
