using System.Threading.Tasks;
using PFound.NetworkLayer;                 // .Forget() extension + [Notify] Notify(...)
using GameSpecific.Networking.Data;
using GameSpecific.Networking.Operations;

namespace GameSpecific.Networking
{
    /// <summary>
    /// How a screen/controller calls the network under the MANDATORY ServerOperation policy (see
    /// <see cref="NetworkingPolicy"/>): every reply-bearing call goes through the operation's uniform
    /// <c>Execute(args)</c> entry point, never the raw direct <c>Execute</c> shortcut (which the policy makes a
    /// compile error). <c>Execute</c> takes ONLY the operation's own request parameters, runs the operation's flow
    /// lifecycle, and returns a <see cref="Task"/> — so the caller either <c>await</c>s it (reading the returned DTO
    /// for a query) or fires it and drops the outcome with <c>.Forget()</c>. The transport, seams, run policy, and
    /// any mutated game state are all resolved from the ambient host configured once at boot (see
    /// <see cref="GameNetworkSetup"/>), so nothing is threaded through the call. A one-way <c>[Notify]</c> stays a
    /// plain <c>Notify(...)</c>: it carries no reply, so the policy leaves it alone.
    /// </summary>
    public static class NetworkCallsExample
    {
        public static async Task RunAsync(PlayerId playerId, int spendAmount, AllianceId allianceId)
        {
            // Query — NO flow needed (changes nothing locally): the generated Execute sends + returns the DTO.
            PlayerData player = await GetPlayerDataOperation.Execute(playerId);

            // Mutation — the client-predicted spend lifecycle; the authoritative balance lands in the ambient wallet.
            await SpendCoinsOperation.Execute(spendAmount);

            // Another query — same shape, returns the join outcome DTO.
            AllianceJoinResult allianceJoin = await JoinAllianceOperation.Execute(allianceId, player.PlayerId);

            // Fire-and-forget, TWO distinct tools:

            // (a) Notify — genuinely one-way, NO reply on the wire. Notify IS the fire-and-forget; it returns
            //     void, so you never chain .Forget() onto it. Allowed under the mandatory policy.
            PresencePingOperation.Notify(playerId, true);

            // (b) A reply-bearing operation you simply don't want to wait for — fire Execute and drop the outcome.
            //     .Forget() extends Task, and Execute's Task (or Task<DTO>, which is-a Task) binds to it; a fault is
            //     routed to ForgetExtensions.OnFault.
            GetPlayerDataOperation.Execute(playerId).Forget();
            SpendCoinsOperation.Execute(spendAmount).Forget();

            // (values used so the sample reads as real call sites)
            System.Console.WriteLine(
                $"{player.Name} now has {PlayerWallet.Current.Balance} coins; alliance size {allianceJoin.MemberCount}");
        }
    }
}
