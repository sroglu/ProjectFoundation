namespace GameSpecific.Backend.Repositories
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IWalletRepository
    {
        Task<PlayerWallet> GetAsync(long playerId, CancellationToken ct);
        Task SaveAsync(PlayerWallet wallet, CancellationToken ct);
    }

    public sealed class PlayerWallet
    {
        public long PlayerId { get; set; }
        public int Coins { get; set; }
    }
}
