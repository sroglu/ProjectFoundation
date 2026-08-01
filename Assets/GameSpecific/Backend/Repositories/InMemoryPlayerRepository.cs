namespace GameSpecific.Backend.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class InMemoryPlayerRepository : IPlayerRepository
    {
        readonly Dictionary<long, PlayerProfile> _players = new();
        readonly object _lock = new();

        public InMemoryPlayerRepository()
        {
            // Pre-populate for testing
            _players[1] = new PlayerProfile { PlayerId = 1, Name = "Player One", Level = 5 };
            _players[2] = new PlayerProfile { PlayerId = 2, Name = "Player Two", Level = 3 };
        }

        public Task<PlayerProfile> GetAsync(long playerId, CancellationToken ct)
        {
            lock (_lock)
            {
                if (_players.TryGetValue(playerId, out var profile))
                    return Task.FromResult(new PlayerProfile
                    {
                        PlayerId = profile.PlayerId,
                        Name = profile.Name,
                        Level = profile.Level
                    });
                throw new KeyNotFoundException($"Player not found: {playerId}");
            }
        }

        public Task SaveAsync(PlayerProfile profile, CancellationToken ct)
        {
            lock (_lock)
            {
                _players[profile.PlayerId] = profile;
                return Task.CompletedTask;
            }
        }
    }
}
