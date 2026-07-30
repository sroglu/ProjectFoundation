namespace PFound.HubApp.Save
{
	/// <summary>
	/// Profile-namespaced JSON save persistence. Owns the in-memory <see cref="SaveSchema"/>
	/// and bridges it to disk via atomic writes (temp+fsync+rename) with .bak fallback.
	/// </summary>
	/// <remarks>
	/// Lifecycle:
	/// <list type="bullet">
	///   <item><see cref="Load"/> at app boot. Until then, <see cref="Schema"/> is null —
	///         calling sites that touch it before Load WILL NRE (intentional fail-fast).</item>
	///   <item><see cref="Flush"/> (or <see cref="Save"/>) on app pause / mini-game exit /
	///         settings change. Hooking these into Application lifecycle is the Shell's job
	///         (Faz 2), not this service's.</item>
	///   <item><see cref="Reset"/> wipes the in-memory schema back to defaults. Caller still
	///         needs to <see cref="Save"/> to persist the wipe.</item>
	/// </list>
	/// </remarks>
	public interface ISaveService
	{
		/// <summary>The live in-memory schema. Null before <see cref="Load"/>.</summary>
		SaveSchema Schema { get; }

		/// <summary>
		/// Reads save.json (or .bak fallback if main is missing/corrupt at the IO layer).
		/// Missing both → fresh default schema is created in memory and persisted on next save.
		/// Throws if the JSON is present but malformed (caller chooses recovery — silent fall
		/// to defaults would mask real corruption bugs).
		/// </summary>
		void Load();

		/// <summary>Synonym for <see cref="Flush"/>. Persists <see cref="Schema"/> to disk.</summary>
		void Save();

		/// <summary>Persists the current in-memory <see cref="Schema"/> to disk atomically.</summary>
		void Flush();

		/// <summary>Resets <see cref="Schema"/> to defaults in memory. Does NOT touch disk — call <see cref="Save"/> to persist.</summary>
		void Reset();
	}
}
