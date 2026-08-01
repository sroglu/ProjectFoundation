namespace GameSpecific.Backend.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class InMemoryWalletRepository : IWalletRepository
    {
        readonly Dictionary<long, PlayerWallet> _wallets = new();
        readonly object _lock = new();

        public InMemoryWalletRepository()
        {
            // Pre-populate for testing
            _wallets[1] = new PlayerWallet { PlayerId = 1, Coins = 1000 };
            _wallets[2] = new PlayerWallet { PlayerId = 2, Coins = 500 };
        }

        public Task<PlayerWallet> GetAsync(long playerId, CancellationToken ct)
        {
            lock (_lock)
            {
                if (_wallets.TryGetValue(playerId, out var wallet))
                    return Task.FromResult(new PlayerWallet { PlayerId = wallet.PlayerId, Coins = wallet.Coins });
                throw new KeyNotFoundException($"Wallet not found: {playerId}");
            }
        }

        public Task SaveAsync(PlayerWallet wallet, CancellationToken ct)
        {
            lock (_lock)
            {
                _wallets[wallet.PlayerId] = wallet;
                return Task.CompletedTask;
            }
        }
    }
}
