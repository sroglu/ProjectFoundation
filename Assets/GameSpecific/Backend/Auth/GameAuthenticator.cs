namespace GameSpecific.Backend.Auth
{
    using System.Threading;
    using System.Threading.Tasks;
    using PFound.Backend.Core;

    public sealed class GameAuthenticator : IAuthenticator<LoginCredentials>
    {
        public Task<AuthResult> AuthenticateAsync(LoginCredentials credentials, CancellationToken ct)
        {
            // MVP: accept all for testing
            return Task.FromResult(new AuthResult
            {
                Success = true,
                PlayerId = 1, // hardcoded for MVP
                ErrorCode = null
            });
        }
    }

    public sealed class LoginCredentials
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
