namespace PFound.HubApp.Services.ParentGate
{
	/// <summary>
	/// Single parent-gate prompt. Read-only after construction — verifying a challenge is
	/// just an index comparison; mutating state would let the UI accidentally alter the
	/// correct answer between render and verify.
	/// </summary>
	public sealed class ParentGateChallenge
	{
		/// <summary>The number the parent must tap, e.g. 6.</summary>
		public int PromptNumber { get; }

		/// <summary>Multiple-choice options in display order, e.g. [4, 6, 8, 2]. One of them equals <see cref="PromptNumber"/>.</summary>
		public int[] Options { get; }

		/// <summary>Index in <see cref="Options"/> of the correct answer.</summary>
		public int CorrectIndex { get; }

		public ParentGateChallenge(int promptNumber, int[] options, int correctIndex)
		{
			PromptNumber = promptNumber;
			Options = options;
			CorrectIndex = correctIndex;
		}
	}
}
