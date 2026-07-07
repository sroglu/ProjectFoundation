using PFound.Signaling;

namespace PFound.SampleGame
{
    /// <summary>
    /// Emitted once the sample boot pipeline finishes. Payload-free: the type is the message, and
    /// listeners branch on the <see cref="SignalKey"/> the tracker hands them.
    /// </summary>
    public sealed class BootCompleted : SignalBase { }
}
