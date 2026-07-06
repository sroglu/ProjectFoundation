namespace PFound.HubApp.MiniGame
{
	/// <summary>
	/// Per-(profile, game) scoped key-value save handle. Mini-games receive this via
	/// <c>MiniGameContext</c> and can only read/write inside their own namespace
	/// (<c>games.{profileId}.{gameId}.*</c>) — the contract enforces save isolation
	/// (HubApp_TDD §7.3).
	/// </summary>
	/// <remarks>
	/// Fail-fast semantics: <see cref="Get{T}"/> and <see cref="Remove"/> throw
	/// <see cref="System.Collections.Generic.KeyNotFoundException"/> on missing keys.
	/// Callers that want a default must opt-in explicitly:
	/// <code>var progress = save.Has("progress") ? save.Get&lt;int&gt;("progress") : 0;</code>
	/// </remarks>
	public interface IScopedSaveService
	{
		bool Has(string key);
		T Get<T>(string key);
		void Set<T>(string key, T value);
		void Remove(string key);
	}
}
