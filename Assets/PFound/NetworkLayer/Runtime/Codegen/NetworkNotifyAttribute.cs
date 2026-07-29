using System;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Marks a <c>partial class</c> as a one-way notify (fire-and-forget, no reply). The source generator
    /// reads the fields tagged with <see cref="FieldAttribute"/> and emits the immutable wire struct, the
    /// notify envelope, and the single-opcode catalog enrolment.
    /// </summary>
    /// <remarks>
    /// <paramref name="domain"/> and <paramref name="op"/> share the same op space as
    /// <see cref="NetworkOpAttribute"/> within a domain (a notify costs one opcode). They are typed
    /// <see cref="object"/> so any two enums can be passed as attribute constants; the generator recovers
    /// each enum type and value.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class NetworkNotifyAttribute : Attribute
    {
        public object Domain { get; }
        public object Op { get; }

        public NetworkNotifyAttribute(object domain, object op)
        {
            Domain = domain;
            Op = op;
        }
    }
}
