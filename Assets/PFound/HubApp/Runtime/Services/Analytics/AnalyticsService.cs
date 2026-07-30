using System;
using System.Collections.Generic;
using PFound.HubApp.Save;

namespace PFound.HubApp.Services.Analytics
{
	/// <summary>
	/// Facade in front of an <see cref="IAnalyticsProvider"/>. Enforces the COPPA/GDPR-K opt-in
	/// rule (HubApp_TDD §11.2): events are dropped when <c>SaveSchema.Settings.AnalyticsOptIn</c>
	/// is false. The provider never sees the event in that case.
	/// </summary>
	/// <remarks>
	/// The opt-in gate runs on every <see cref="Track"/> call (not cached) so toggling consent
	/// in Settings takes effect immediately without re-wiring anything. There is no PII filter
	/// at this layer — callers are expected to pass gameplay-only properties per the §11.2 rule
	/// (no display name, no avatar id, no profile id even). Catching that mistake is the
	/// reviewer/tester's job, not a runtime check; we'd just hide the bug otherwise.
	/// </remarks>
	public class AnalyticsService
	{
		private readonly ISaveService _save;
		private readonly IAnalyticsProvider _provider;

		public AnalyticsService(ISaveService save, IAnalyticsProvider provider)
		{
			if (save == null) throw new ArgumentNullException(nameof(save));
			if (provider == null) throw new ArgumentNullException(nameof(provider));

			_save = save;
			_provider = provider;
		}

		/// <summary>The active provider. Exposed for diagnostic/testing — not for runtime swapping.</summary>
		public IAnalyticsProvider Provider => _provider;

		/// <summary>
		/// Records <paramref name="eventName"/> with optional <paramref name="properties"/>.
		/// Silently no-ops when the user has not opted in.
		/// </summary>
		public void Track(string eventName, IReadOnlyDictionary<string, object> properties = null)
		{
			if (string.IsNullOrEmpty(eventName))
				throw new ArgumentException("eventName must not be empty.", nameof(eventName));

			if (!_save.Schema.Settings.AnalyticsOptIn)
				return;

			_provider.Track(eventName, properties);
		}
	}
}
