namespace GameSpecific.Backend.Operations
{
    using System.Threading;
    using System.Threading.Tasks;
    using PFound.Backend.Core;
    using PFound.NetworkLayer;
    using GameSpecific.Networking.Operations;
    using GameSpecific.Networking.Data;

    public sealed class JoinAllianceHandler
        : IOperationHandler<JoinAllianceOperation.RequestMessage, JoinAllianceOperation.ReplyMessage>
    {
        readonly ISessionStore _sessions;

        public JoinAllianceHandler(ISessionStore sessions)
        {
            _sessions = sessions;
        }

        public Task<JoinAllianceOperation.ReplyMessage> HandleAsync(
            int peerId,
            JoinAllianceOperation.RequestMessage req,
            CancellationToken ct)
        {
            // Resolve the player ID from the authenticated session
            if (!_sessions.TryGet(peerId, out var session))
            {
                return Task.FromResult(new JoinAllianceOperation.ReplyMessage
                {
                    Status = ReplyStatus.Refused,
                    Content = default
                });
            }

            // RULE 3: Copy request values BEFORE await
            var allianceId = req.Content.AllianceId;

            // RULE 5: Check before reply, fail with status
            if (allianceId.Value == 0)
            {
                return Task.FromResult(new JoinAllianceOperation.ReplyMessage
                {
                    Status = ReplyStatus.Faulted,
                    Content = default
                });
            }

            // RULE 3: Build reply fresh, just before return
            return Task.FromResult(new JoinAllianceOperation.ReplyMessage
            {
                Status = ReplyStatus.Ok,
                Content = new AllianceJoinResult
                {
                    Id = allianceId,
                    MemberCount = 5
                }
            });
            // RULE 2: Reply() called on pump thread in registry, not here ✅
        }
    }
}
