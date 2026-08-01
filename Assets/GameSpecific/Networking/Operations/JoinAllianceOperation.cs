using GameSpecific.Networking.Data;
using PFound.NetworkLayer;
using PFound.ServerOperation.Core;

namespace GameSpecific.Networking.Operations
{
    /// <summary>
    /// A second reference operation — "join an alliance" — a mutation with local prediction, so it OWNS a
    /// <see cref="JoinAllianceOperationFlow"/> (client-predict the membership → send → apply the authoritative
    /// result). Called the same uniform way:
    /// <c>AllianceJoinResult result = await JoinAllianceOperation.Execute(allianceId, playerId);</c>. Because a flow
    /// exists, the generator emits the flow-running <c>Execute(JoinAllianceRequest request)</c> (plus a convenience
    /// overload over the request DTO's members), news that flow up, and runs its lifecycle — so this operation class
    /// is EMPTY. The <c>[RemoteProcedure]</c> spec names the request DTO (<see cref="JoinAllianceRequest"/>) and reply
    /// DTO (<see cref="AllianceJoinResult"/>); the reply carries the DTO, NOT a bare <c>int</c>: a reply is always a DTO.
    /// </summary>
    [RemoteProcedure(NetDomain.Alliance, AllianceOp.Join, typeof(JoinAllianceRequest), typeof(AllianceJoinResult))]
    public partial class JoinAllianceOperation { }
    /// <summary>
    /// Ambient local alliance membership the join flow predicts against and reconciles to the server's answer — the
    /// alliance analogue of <see cref="PlayerWallet"/>. A real game would reach its social/alliance singleton here.
    /// </summary>
    public sealed class AllianceMembership
    {
        public AllianceId CurrentAlliance;
        public long MemberCount;

        public bool IsInAlliance => CurrentAlliance.Value != 0;

        public static AllianceMembership Current { get; set; } = new AllianceMembership();
    }

    /// <summary>Server-authoritative flow for JoinAllianceOperation — client-predicts the join, then applies it.</summary>
    public sealed class JoinAllianceOperationFlow
        : ServerOperationFlow<JoinAllianceOperation.RequestMessage, JoinAllianceOperation.ReplyMessage, ServerOperationResult<OpResult>>
    {
        readonly JoinAllianceRequest _request;

        /// <summary>The authoritative join outcome, captured after a successful run — the DTO Execute returns.</summary>
        public AllianceJoinResult Result { get; private set; }

        public JoinAllianceOperationFlow(JoinAllianceRequest request) => _request = request;

        AllianceMembership Membership => AllianceMembership.Current;

        protected override ServerOperationResult<OpResult> PreCheck()
        {
            if (_request.AllianceId.Value == 0)
                return OpResults.Fail(OpResult.AllianceIdMissing, "alliance id must be set");
            if (Membership.IsInAlliance)
                return OpResults.Fail(OpResult.AlreadyInAlliance, "already in an alliance");
            return OpResults.Ok();
        }

        protected override JoinAllianceOperation.RequestMessage BuildRequest() => new JoinAllianceOperation.RequestMessage { Content = _request };

        protected override ServerOperationResult<OpResult> Interpret(JoinAllianceOperation.ReplyMessage reply)
        {
            if (reply.Status != ReplyStatus.Ok)
                return OpResults.Fail(OpResults.FromReplyStatus(reply.Status), $"join refused by server: {reply.Status}");
            Result = reply.Content;
            return OpResults.Ok();
        }

        protected override void ApplySuccess(ServerOperationResult<OpResult> result)
        {
            Membership.CurrentAlliance = Result.Id;
            Membership.MemberCount = Result.MemberCount;
        }
    }
}
