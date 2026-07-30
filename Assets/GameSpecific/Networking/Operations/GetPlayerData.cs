using GameSpecific.Networking.Data;
using PFound.NetworkLayer;

namespace GameSpecific.Networking.Operations
{
    /// <summary>
    /// The reference QUERY — "pass a playerId → get a <see cref="PlayerData"/> back". Calling it is uniform
    /// with every other operation: <c>var data = await GetPlayerData.CallAsync(playerId);</c> returns the
    /// reply DTO (<see cref="PlayerData"/>) by await. The <c>[RemoteProcedure]</c> spec is the whole wire
    /// contract: it names the request DTO (<see cref="PlayerDataReq"/>) and the reply DTO — the SHARED
    /// <see cref="PlayerData"/> (defined once in <c>Data/</c>), because a reply is always a DTO. The generator
    /// expands the spec into <c>GetPlayerData.RequestMessage</c> / <c>GetPlayerData.ReplyMessage</c> +
    /// <c>Register</c> + <c>CallAsync(PlayerDataReq)</c> returning <see cref="PlayerData"/> directly, plus a
    /// convenience overload <c>CallAsync(long playerId)</c>. A query changes nothing locally, so there is no
    /// <c>ServerOperation</c> and no authoritative state to apply — the uniform call IS the whole client side.
    /// </summary>
    [RemoteProcedure(NetDomain.Player, PlayerOp.GetData, typeof(PlayerId), typeof(PlayerData))]
    public partial class GetPlayerData { }
}
