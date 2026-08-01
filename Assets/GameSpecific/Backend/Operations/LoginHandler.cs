namespace GameSpecific.Backend.Operations
{
    using System.Threading;
    using System.Threading.Tasks;
    using PFound.Backend.Core;
    using PFound.NetworkLayer;
    using GameSpecific.Networking.Operations;
    using GameSpecific.Networking.Data;
    using GameSpecific.Backend.Auth;

    /// <summary>
    /// Authenticates peer credentials and binds the peer to a player session.
    /// On success, stores the player ID in ISessionStore so other operations can
    /// resolve the authenticated player from the peer ID.
    /// </summary>
    public sealed class LoginHandler
        : IOperationHandler<LoginOperation.RequestMessage, LoginOperation.ReplyMessage>
    {
        readonly IAuthenticator<LoginCredentials> _authenticator;
        readonly ISessionStore _sessions;

        public LoginHandler(IAuthenticator<LoginCredentials> authenticator, ISessionStore sessions)
        {
            _authenticator = authenticator;
            _sessions = sessions;
        }

        public async Task<LoginOperation.ReplyMessage> HandleAsync(
            int peerId,
            LoginOperation.RequestMessage req,
            CancellationToken ct)
        {
            // RULE 3: Copy request values BEFORE await
            string username = req.Content.Username;
            string password = req.Content.Password;

            // RULE 3: All awaits after extraction
            var authResult = await _authenticator.AuthenticateAsync(
                new LoginCredentials { Username = username, Password = password },
                ct);

            // RULE 5: Check auth result, fail with status if auth failed
            if (!authResult.Success)
            {
                return new LoginOperation.ReplyMessage
                {
                    Status = ReplyStatus.Faulted,
                    Content = default
                };
            }

            // On success, bind the peer to the authenticated player session
            var session = new PlayerSession
            {
                PlayerId = authResult.PlayerId,
                AuthToken = authResult.ErrorCode ?? "token"  // ErrorCode carries the token in this impl
            };
            _sessions.Bind(peerId, session);

            // RULE 3: Build reply fresh, just before return
            return new LoginOperation.ReplyMessage
            {
                Status = ReplyStatus.Ok,
                Content = new LoginResult
                {
                    PlayerId = new PlayerId { Value = authResult.PlayerId },
                    AuthToken = session.AuthToken
                }
            };
            // RULE 2: Reply() called on pump thread in registry, not here ✅
        }
    }
}
