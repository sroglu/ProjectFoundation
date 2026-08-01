namespace GameSpecific.Backend.Repositories
{
    using System.Threading;
    using System.Threading.Tasks;
    using GameSpecific.Networking.Data;

    public interface IPlayerRepository
    {
        Task<PlayerProfile> GetAsync(long playerId, CancellationToken ct);
        Task SaveAsync(PlayerProfile profile, CancellationToken ct);
    }

    public sealed class PlayerProfile
    {
        public long PlayerId { get; set; }
        public string Name { get; set; }
        public ushort Level { get; set; }
    }
}
