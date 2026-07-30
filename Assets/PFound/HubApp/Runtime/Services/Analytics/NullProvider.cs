using System.Collections.Generic;

namespace PFound.HubApp.Services.Analytics
{
	/// <summary>
	/// No-op <see cref="IAnalyticsProvider"/>. Default for development builds and for the
	/// "user opted out" path so callers don't have to null-check the provider.
	/// </summary>
	public sealed class NullProvider : IAnalyticsProvider
	{
		public void Track(string eventName, IReadOnlyDictionary<string, object> properties) { }
	}
}
