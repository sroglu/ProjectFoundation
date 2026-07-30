namespace PFound.NetworkLayer
{
    /// <summary>
    /// Implemented by an envelope that carries a serializable payload DTO. The envelope itself is a
    /// runtime-only pooling/routing carrier and is NEVER serialized — the codec serializes only the DTO it
    /// carries (<see cref="Payload"/>, typed <see cref="PayloadType"/>), so the wire is the flat DTO with no
    /// envelope wrapper. The generated request/reply/notify envelopes implement this; the codec reads the DTO
    /// out on pack and writes it back in on unpack.
    /// </summary>
    public interface IMessagePayload
    {
        /// <summary>The concrete DTO type the codec serializes for this envelope.</summary>
        System.Type PayloadType { get; }

        /// <summary>The carried DTO — read on pack, assigned the decoded value on unpack.</summary>
        object Payload { get; set; }
    }

    /// <summary>
    /// Root of the message model that game projects extend with their own payload
    /// fields. Instances are poolable: a peer rents one, fills it, hands it to the
    /// send API, and the layer recycles it. Subclasses override <see cref="Clear"/>
    /// to wipe their fields so a reused instance never leaks stale data.
    /// </summary>
    public abstract class Message
    {
        /// <summary>Reset every field to its default before the instance re-enters the pool.</summary>
        public virtual void Clear() { }
    }

    /// <summary>A message that expects a correlated reply (the RPC direction).</summary>
    public abstract class RequestMessage : Message { }

    /// <summary>
    /// A reply to a <see cref="RequestMessage"/>. The success/failure verdict lives
    /// in the frame header and is mirrored here for the caller to read; payload
    /// fields carry the actual result.
    /// </summary>
    public abstract class ReplyMessage : Message
    {
        public ReplyStatus Status;

        public override void Clear()
        {
            Status = ReplyStatus.Ok;
        }
    }

    /// <summary>A fire-and-forget one-way message; no reply is correlated.</summary>
    public abstract class NotifyMessage : Message { }
}
