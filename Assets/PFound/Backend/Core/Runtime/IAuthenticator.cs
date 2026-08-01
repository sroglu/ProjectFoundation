namespace PFound.Backend.Core
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Seam: credential validation (login operation).</summary>
    public interface IAuthenticator<TCredentials>
    {
        Task<AuthResult> AuthenticateAsync(TCredentials credentials, CancellationToken ct);
    }

    /// <summary>Auth outcome: success/failure + player id on success.</summary>
    public readonly struct AuthResult
    {
        public bool Success { get; init; }
        public long PlayerId { get; init; }
        public string ErrorCode { get; init; }
    }
}
