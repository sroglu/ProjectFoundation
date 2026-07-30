using GameSpecific.Networking.Data;
using PFound.NetworkLayer;

namespace GameSpecific.Networking.Operations
{
    /// <summary>
    /// A second, terser operation — "join an alliance" — called the same uniform way:
    /// <c>var result = await JoinAlliance.CallAsync(allianceId);</c> returns the <see cref="AllianceJoinResult"/> DTO.
    /// The <c>[RemoteProcedure]</c> spec names the request DTO (<see cref="JoinAllianceRequest"/>) and reply DTO
    /// (<see cref="AllianceJoinResult"/>); the generator expands it into the envelopes, the catalog <c>Register</c>,
    /// and the <c>CallAsync(JoinAllianceReq)</c> entry point plus a <c>CallAsync(long allianceId)</c>
    /// convenience overload. The reply carries the <see cref="AllianceJoinResult"/> DTO, NOT a bare <c>int</c>: a reply
    /// is always a DTO.
    /// </summary>
    [RemoteProcedure(NetDomain.Alliance, AllianceOp.Join, typeof(JoinAllianceRequest), typeof(AllianceJoinResult))]
    public partial class JoinAlliance { }
}
