using MessagePack;

namespace GameSpecific.Networking.Data
{
    /// <summary>
    /// The request DTO for <c>LoginOperation</c>: username and password credentials.
    /// </summary>
    [MessagePackObject]
    public readonly partial struct LoginRequest
    {
        [Key(0)] public string Username { get; init; }
        [Key(1)] public string Password { get; init; }
    }
}
