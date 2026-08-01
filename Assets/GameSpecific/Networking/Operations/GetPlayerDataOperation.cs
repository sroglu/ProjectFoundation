using GameSpecific.Networking.Data;
using PFound.NetworkLayer;
using PFound.ServerOperationFlow.Core;

namespace GameSpecific.Networking.Operations
{
    /// <summary>
    /// The reference QUERY — pass a <see cref="PlayerId"/>, get the shared <see cref="PlayerData"/> DTO back.
    /// It owns a <see cref="GetPlayerDataOperationFlow"/> so the reply is validated (server status check) and the
    /// fetched profile is captured before <c>Execute(playerId)</c> returns it; a pure query applies no local state,
    /// so <c>ApplySuccess</c> is a no-op. (A query that needs none of that can skip the flow entirely — then the
    /// generator emits a direct <c>Execute</c> that just sends and returns the DTO.) The <c>[RemoteProcedure]</c>
    /// spec is the whole wire contract — request DTO (<see cref="PlayerId"/>) and reply DTO
    /// (<see cref="PlayerData"/>, shared from <c>Data/</c>).
    /// </summary>
    [RemoteProcedure(NetDomain.Player, PlayerOp.GetData, typeof(PlayerId), typeof(PlayerData))]
    public partial class GetPlayerDataOperation { }
    /// <summary>Server-authoritative flow for GetPlayerDataOperation — fill PreCheck / Interpret / ApplySuccess.</summary>
    public sealed class GetPlayerDataOperationFlow
        : GameServerOperationFlow<GetPlayerDataOperation.RequestMessage, GetPlayerDataOperation.ReplyMessage>
    {
        readonly PlayerId _request;

        /// <summary>The fetched profile, captured after a successful run — the same DTO Execute returns.</summary>
        public PlayerData Result { get; private set; }

        public GetPlayerDataOperationFlow(PlayerId request) => _request = request;

        protected override ServerOperationResult<OpResult> PreCheck() => OpResults.Ok();

        protected override GetPlayerDataOperation.RequestMessage BuildRequest() => new GetPlayerDataOperation.RequestMessage { Content = _request };

        protected override ServerOperationResult<OpResult> Interpret(GetPlayerDataOperation.ReplyMessage reply)
        {
            if (reply.Status != ReplyStatus.Ok)
                return OpResults.Fail(OpResults.FromReplyStatus(reply.Status), $"player fetch refused by server: {reply.Status}");
            Result = reply.Content;
            return OpResults.Ok();
        }

        // A query mutates no local state — the caller reads the returned PlayerData off Execute. Nothing to apply.
        protected override void ApplySuccess(ServerOperationResult<OpResult> result) { }
    }
}
