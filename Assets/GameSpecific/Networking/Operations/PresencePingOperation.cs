using GameSpecific.Networking.Data;
using PFound.NetworkLayer;

namespace GameSpecific.Networking.Operations
{
    [Notify(NetDomain.Player,PlayerOp.Presence, typeof(PlayerPresence))]
    public partial class PresencePingOperation { }
}