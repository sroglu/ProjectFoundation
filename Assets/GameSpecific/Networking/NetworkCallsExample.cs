using System.Threading.Tasks;
using PFound.NetworkLayer;                 // .Forget() / .FireAndForget() extensions live here
using GameSpecific.Networking.Data;
using GameSpecific.Networking.Operations;

namespace GameSpecific.Networking
{
    /// <summary>
    /// The uniform way game code calls a network operation: one static entry per operation, awaited, returning
    /// the reply DTO by value. No hand-written helper, no per-call <c>ClientPeer</c> plumbing — the ambient
    /// <c>NetworkClient.Current</c> (configured once at boot, see <see cref="GameNetworkSetup"/>) is where each
    /// call is sent. This mirrors what a real screen/controller would write.
    /// </summary>
    public static class NetworkCallsExample
    {
        /// <summary>Every operation reads the same: <c>var value = await &lt;Op&gt;.CallAsync(args);</c>.</summary>
        public static async Task RunAsync(PlayerId playerId, int spendAmount, AllianceId allianceId)
        {
            // Query — returns the shared PlayerData DTO directly (single-field reply is unwrapped to its value).
            PlayerData player = await GetPlayerData.CallAsync(playerId);

            // Mutation, the plain uniform way — returns the SpendResult DTO.
            SpendResult spend = await SpendCoins.CallAsync(spendAmount);

            // Another operation — same shape, returns the JoinResult DTO.
            AllianceJoinResult allianceJoin = await JoinAlliance.CallAsync(allianceId, playerId);

            // Fire-and-forget, TWO distinct tools:

            // (a) Notify — genuinely one-way, NO reply on the wire. Send IS the fire-and-forget; it returns void,
            //     so you never chain .Forget() onto it.
            PresencePing.Send(playerId, true);

            // (b) A reply-bearing RPC you simply don't want to wait for — fire it and drop the reply — the
            //     familiar fire-and-forget idiom. `.Forget()` and `.FireAndForget()` are the same call (needs the
            //     `using PFound.NetworkLayer;` above); a fault is routed to FireAndForgetExtensions.OnFault.
            GetPlayerData.CallAsync(playerId).Forget();
            SpendCoins.CallAsync(spendAmount).FireAndForget();

            // (values used so the sample reads as real call sites)
            System.Console.WriteLine($"{player.Name} now has {spend.NewBalance} coins; alliance size {allianceJoin.MemberCount}");
        }
    }
}
