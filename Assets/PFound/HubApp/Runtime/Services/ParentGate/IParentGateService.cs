namespace PFound.HubApp.Services.ParentGate
{
	/// <summary>
	/// Generates and verifies parent-gate challenges. Pure model — UI rendering lives in the
	/// consuming Shell scene (Playnest's ParentGate.unity, Faz 2). HubApp_TDD §6.4 mandates
	/// the gate fronts every IAP, profile delete, external link, and parental-settings entry.
	/// </summary>
	/// <remarks>
	/// Apple kid-app guidance: challenge must be hard enough that a 2-5 year old can't solve
	/// by accident, easy enough that a parent can solve in &lt; 5s. We use a small-number choice
	/// task ("Tap the number six.") with 4 written-number options — meets the bar without
	/// frustrating parents and avoids text reading the toddler can pattern-match.
	/// </remarks>
	public interface IParentGateService
	{
		/// <summary>Generates a fresh challenge. Each call returns a new prompt and option set.</summary>
		ParentGateChallenge GenerateChallenge();

		/// <summary>Returns true iff <paramref name="chosenIndex"/> matches the challenge's correct option.</summary>
		bool Verify(ParentGateChallenge challenge, int chosenIndex);
	}
}
