using MessagePack;
using GameSpecific.Networking.Data;

namespace GameSpecific.Networking.Data
{
    /// <summary>
    /// The reply DTO for <c>LoginOperation</c>: authenticated player ID and auth token.
    /// Returned on successful authentication after server binds the peer to the player session.
    /// </summary>
    [MessagePackObject]
    public readonly partial struct LoginResult
    {
        [Key(0)] public PlayerId PlayerId { get; init; }
        [Key(1)] public string AuthToken { get; init; }
    }
}
