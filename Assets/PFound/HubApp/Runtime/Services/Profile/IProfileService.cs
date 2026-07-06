using System.Collections.Generic;
using PFound.HubApp.Save;

namespace PFound.HubApp.Services.Profile
{
	/// <summary>
	/// Multi-profile CRUD on <see cref="SaveSchema.Profile"/>. The hub supports more than one
	/// child per device (sibling use case — see HubApp_TDD §5.2); the active profile gates every
	/// downstream service call (badges, photos, scoped save).
	/// </summary>
	/// <remarks>
	/// Fail-fast posture: <see cref="GetProfile"/> and <see cref="SetActive"/> throw on missing ids
	/// — silently falling back would let stale profile pickers keep operating against a deleted
	/// profile and corrupt downstream writes. Use <see cref="HasProfile"/> to gate.
	/// Mutations do NOT auto-flush the save file; the Shell layer (Faz 2) is responsible for
	/// invoking <see cref="ISaveService.Flush"/> at the appropriate lifecycle points.
	/// </remarks>
	public interface IProfileService
	{
		/// <summary>The currently active profile id, or null if no profile is selected (first launch / cleared).</summary>
		string ActiveProfileId { get; }

		/// <summary>Snapshot of all profile entries in insertion order. Returned list is read-only — mutate via Create/Delete.</summary>
		IReadOnlyList<ProfileEntry> Profiles { get; }

		bool HasProfile(string profileId);

		ProfileEntry GetProfile(string profileId);

		/// <summary>
		/// Creates a new profile and returns the assigned id. Display name + age band are required;
		/// avatar id may be null while the picker UI is being implemented.
		/// </summary>
		ProfileEntry Create(string displayName, string ageBand, string avatarId = null);

		/// <summary>
		/// Switches the active profile to <paramref name="profileId"/> and queues
		/// <c>ProfileSelectedSignal</c>. Throws if the id does not exist.
		/// </summary>
		void SetActive(string profileId);

		/// <summary>Clears the active selection (no profile selected). Queues <c>ProfileSelectedSignal</c>.</summary>
		void ClearActive();

		/// <summary>
		/// Removes the profile and its associated <c>Shared</c>/<c>Games</c> entries. If the deleted
		/// profile was active, <see cref="ActiveProfileId"/> is cleared and the signal fires.
		/// </summary>
		void Delete(string profileId);
	}
}
