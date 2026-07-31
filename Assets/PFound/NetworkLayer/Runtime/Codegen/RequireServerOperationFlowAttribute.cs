using System;

namespace PFound.NetworkLayer
{
    /// <summary>
    /// Project-wide policy switch: when this attribute is present on a networking assembly, every server call
    /// in that assembly MUST go through a <c>ServerOperationFlow</c> — the raw generated direct <c>Execute</c>
    /// shortcut on a <see cref="RemoteProcedureAttribute"/> operation is forbidden and reported as a compile-time
    /// error (diagnostic <c>PFNET0010</c>).
    /// </summary>
    /// <remarks>
    /// Place it ONCE, anywhere in the assembly (a small <c>NetworkingPolicy.cs</c> is the usual home):
    /// <code>[assembly: PFound.NetworkLayer.RequireServerOperationFlow]</code>
    /// <para>
    /// <b>Present</b> → MANDATORY mode. Every mutating server call is expressed as a <c>ServerOperationFlow</c>
    /// (client prediction → send → interpret → apply authoritative state), so the raw direct <c>Execute</c> can
    /// never be reached for by mistake. The analyzer flags each raw direct <c>Execute</c> on a
    /// <c>[RemoteProcedure]</c> operation.
    /// </para>
    /// <para>
    /// <b>Absent</b> (the default) → FREE mode. Both the uniform direct <c>Execute</c> shortcut and a
    /// <c>ServerOperationFlow</c> are allowed, and the developer picks per call; the analyzer reports nothing, so a
    /// free project carries zero friction.
    /// </para>
    /// <para>
    /// A one-way <c>[Notify]</c> <c>Send(...)</c> is never affected — a notify has no server-authoritative
    /// lifecycle to route through a <c>ServerOperationFlow</c>, so it stays allowed in both modes. The policy is
    /// per networking assembly: set it on the assembly whose calls you want to constrain.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class RequireServerOperationFlowAttribute : Attribute
    {
    }
}
