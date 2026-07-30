using System;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Marks a <c>partial class</c> as a one-way notify (fire-and-forget, no reply). The payload is a named
    /// first-party <c>[MessagePackObject]</c> DTO type, passed as a <c>typeof</c> argument. The source
    /// generator emits the notify envelope (a runtime-only carrier of the DTO) and the single-opcode catalog
    /// enrolment.
    /// </summary>
    /// <remarks>
    /// <paramref name="domain"/> and <paramref name="op"/> share the same op space as
    /// <see cref="RemoteProcedureAttribute"/> within a domain (a notify costs one opcode). They are typed
    /// <see cref="object"/> so any two enums can be passed as attribute constants. <paramref name="payload"/>
    /// is the DTO type the wire carries.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class NotifyAttribute : Attribute
    {
        public object Domain { get; }
        public object Op { get; }
        public Type Payload { get; }

        public NotifyAttribute(object domain, object op, Type payload)
        {
            Domain = domain;
            Op = op;
            Payload = payload;
        }
    }
}
