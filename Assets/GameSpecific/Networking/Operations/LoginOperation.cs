using System.Threading.Tasks;
using GameSpecific.Networking.Data;
using PFound.NetworkLayer;
using PFound.ServerOperationFlow.Core;

namespace GameSpecific.Networking.Operations
{
    /// <summary>
    /// Login operation: authenticate peer and bind to player session.
    /// The server handler authenticates credentials, binds the peer to a player ID,
    /// and returns the authenticated player ID + auth token.
    /// </summary>
    [RemoteProcedure(NetDomain.Auth, AuthOp.Login, typeof(LoginRequest), typeof(LoginResult))]
    public partial class LoginOperation { }

    /// <summary>
    /// Simple login flow: submit credentials, server authenticates and binds session.
    /// The flow is fire-and-forget or awaited for the auth result; the session binding
    /// is server-side only (peer ↔ player binding).
    /// </summary>
    public sealed class LoginOperationFlow
        : GameServerOperationFlow<LoginOperation.RequestMessage, LoginOperation.ReplyMessage>
    {
        readonly LoginRequest _request;

        /// <summary>The server's auth response (player ID + token), available after success.</summary>
        public LoginResult Result { get; private set; }

        public LoginOperationFlow(LoginRequest request) => _request = request;

        protected override ServerOperationResult<OpResult> PreCheck()
        {
            // Client-side pre-checks: ensure credentials are provided
            if (string.IsNullOrEmpty(_request.Username) || string.IsNullOrEmpty(_request.Password))
                return OpResults.Fail(OpResult.InvalidCredentials, "username/password required");
            return OpResults.Ok();
        }

        protected override LoginOperation.RequestMessage BuildRequest()
            => new LoginOperation.RequestMessage { Content = _request };

        protected override ServerOperationResult<OpResult> Interpret(LoginOperation.ReplyMessage reply)
        {
            if (reply.Status != ReplyStatus.Ok)
                return OpResults.Fail(OpResults.FromReplyStatus(reply.Status), $"login refused: {reply.Status}");
            Result = reply.Content;
            return OpResults.Ok();
        }

        protected override void ApplySuccess(ServerOperationResult<OpResult> result)
        {
            // On success, store the auth token locally (if needed by game logic)
            // The session binding is server-side in ISessionStore
        }
    }
}
