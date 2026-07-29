namespace PFound.PlayerDataSync.Core
{
    /// <summary>
    /// Decides which of two divergent saves for the same install wins. Divergence happens when two devices
    /// write independently, or a reinstall reloads from the backend while a local unsynced write exists. The
    /// policy is a pure function of the two saves so it can be reasoned about and tested, and it is a seam:
    /// the default is last-write-wins, but a game with mergeable state (additive currencies, unioned
    /// unlocks) can supply a smarter merge without touching the rest of the module.
    /// </summary>
    public interface IPlayerSaveConflictPolicy
    {
        /// <summary>Return the winning save (or a merged result) given the device-local and backend-remote versions.</summary>
        PlayerSave Resolve(PlayerSave local, PlayerSave remote);
    }

    /// <summary>
    /// The default policy: the save with the higher <see cref="PlayerSave.SaveCounter"/> wins; a tie keeps
    /// the local copy (this device's most recent intent). Simple, predictable, and correct for the common
    /// case where a single player uses one device at a time; the counter is monotonic per save so "higher"
    /// reliably means "written later".
    /// </summary>
    public sealed class LastWriteWinsConflictPolicy : IPlayerSaveConflictPolicy
    {
        public PlayerSave Resolve(PlayerSave local, PlayerSave remote)
        {
            return remote.SaveCounter > local.SaveCounter ? remote : local;
        }
    }
}
