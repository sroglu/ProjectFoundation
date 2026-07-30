using System.Collections.Generic;
using System.Text;

namespace PFound.Commerce.Core
{
    /// <summary>
    /// The ordered list of rewards a product grants. Immutable once built; applied as a unit through the
    /// <see cref="RewardResolverRegistry"/> (which raises the reward signal per entry). The pre-chosen
    /// name is kept per the build spec.
    /// </summary>
    public sealed class RewardSet
    {
        public static readonly RewardSet Empty = new RewardSet(new IReward[0]);

        readonly IReward[] _rewards;

        public RewardSet(IReadOnlyList<IReward> rewards)
        {
            _rewards = new IReward[rewards.Count];
            for (int i = 0; i < rewards.Count; i++) _rewards[i] = rewards[i];
        }

        public int Count => _rewards.Length;
        public IReward this[int index] => _rewards[index];

        /// <summary>Apply every reward in order through the registry, raising the signal for each.</summary>
        public void ApplyThrough(RewardResolverRegistry registry) => registry.ApplyAll(this);

        /// <summary>A compact per-reward description list for the ledger entry.</summary>
        public string[] Describe()
        {
            var lines = new string[_rewards.Length];
            for (int i = 0; i < _rewards.Length; i++) lines[i] = _rewards[i].Describe();
            return lines;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            for (int i = 0; i < _rewards.Length; i++)
            {
                if (i > 0) builder.Append(", ");
                builder.Append(_rewards[i].Describe());
            }
            return builder.ToString();
        }
    }
}
