using System;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Marks a <c>partial class</c> as a remote procedure operation: a request DTO paired with a reply DTO.
    /// Both are named first-party <c>[MessagePackObject]</c> DTO types, passed as <c>typeof</c> arguments — the
    /// operation references them, it does not inline fields. The source generator reads the attribute and emits
    /// the poolable request/reply envelopes (runtime-only carriers of the DTOs), the catalog enrolment, and a
    /// uniform <c>CallAsync</c> entry point that returns the reply DTO, so a game declares one compact partial
    /// and calls <c>await MyOp.CallAsync(request)</c>.
    /// </summary>
    /// <remarks>
    /// <paramref name="domain"/> and <paramref name="op"/> are the same banded pair the catalog consumes (a
    /// per-game <c>NetDomain</c> value + a per-domain op value); they are typed <see cref="object"/> so any two
    /// enums can be passed as attribute constants. <paramref name="request"/> and <paramref name="reply"/> are
    /// the DTO types the wire carries. The reply is decoded by correlation and carries no opcode of its own.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class RemoteProcedureAttribute : Attribute
    {
        public object Domain { get; }
        public object Op { get; }
        public Type Request { get; }
        public Type Reply { get; }

        public RemoteProcedureAttribute(object domain, object op, Type request, Type reply)
        {
            Domain = domain;
            Op = op;
            Request = request;
            Reply = reply;
        }
    }
}
