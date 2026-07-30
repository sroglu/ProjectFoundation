using System.Collections.Generic;

namespace PFound.HubApp.Services.Analytics
{
	/// <summary>
	/// Plug point for an analytics backend (GameAnalytics, Unity Analytics, none). The facade
	/// (<see cref="AnalyticsService"/>) handles opt-in gating and PII filtering — providers
	/// just forward the already-vetted event to their SDK.
	/// </summary>
	public interface IAnalyticsProvider
	{
		void Track(string eventName, IReadOnlyDictionary<string, object> properties);
	}
}
