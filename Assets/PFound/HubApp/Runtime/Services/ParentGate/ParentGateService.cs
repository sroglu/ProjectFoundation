using System;
using System.Collections.Generic;

namespace PFound.HubApp.Services.ParentGate
{
	/// <summary>
	/// Concrete <see cref="IParentGateService"/>. Generates 4-option number-pick challenges
	/// over the digit range [<see cref="MinDigit"/>, <see cref="MaxDigit"/>] (inclusive).
	/// Range chosen so options are visually distinct and solvable in &lt; 5s by a parent.
	/// </summary>
	/// <remarks>
	/// The RNG seed is injectable for deterministic tests; production wiring uses an unseeded
	/// <see cref="Random"/>. Verification is a constant-time index compare — no replay
	/// protection needed because each challenge is single-use (UI discards after verify).
	/// </remarks>
	public class ParentGateService : IParentGateService
	{
		public const int MinDigit = 2;
		public const int MaxDigit = 9;
		public const int OptionCount = 4;

		private readonly Random _rng;

		public ParentGateService(int? seed = null)
		{
			_rng = seed.HasValue ? new Random(seed.Value) : new Random();
		}

		public ParentGateChallenge GenerateChallenge()
		{
			// Pool of distinct digits to draw from — guarantees options are unique without
			// repeated-roll loops. Pool size [MinDigit..MaxDigit] = 8 ≥ OptionCount = 4.
			var pool = new List<int>(MaxDigit - MinDigit + 1);
			for (var n = MinDigit; n <= MaxDigit; n++)
				pool.Add(n);

			var options = new int[OptionCount];
			for (var i = 0; i < OptionCount; i++)
			{
				var pick = _rng.Next(pool.Count);
				options[i] = pool[pick];
				pool.RemoveAt(pick);
			}

			var correctIndex = _rng.Next(OptionCount);
			var promptNumber = options[correctIndex];

			return new ParentGateChallenge(promptNumber, options, correctIndex);
		}

		public bool Verify(ParentGateChallenge challenge, int chosenIndex)
		{
			if (challenge == null) throw new ArgumentNullException(nameof(challenge));
			if (chosenIndex < 0 || chosenIndex >= challenge.Options.Length)
				throw new ArgumentOutOfRangeException(nameof(chosenIndex),
					$"chosenIndex {chosenIndex} out of range for challenge with {challenge.Options.Length} options.");

			return chosenIndex == challenge.CorrectIndex;
		}
	}
}
