using System;
using System.Collections.Generic;
using System.IO;
using PFound.HubApp.Save;
using PFound.HubApp.Services.Analytics;
using NUnit.Framework;

namespace PFound.HubApp.Tests
{
	[TestFixture]
	public class AnalyticsServiceTests
	{
		private string _saveRoot;
		private SaveService _save;
		private RecordingProvider _provider;
		private AnalyticsService _svc;

		// Test double — captures every Track call so we can assert on what reached the provider.
		private sealed class RecordingProvider : IAnalyticsProvider
		{
			public readonly List<(string Name, IReadOnlyDictionary<string, object> Props)> Calls
				= new List<(string, IReadOnlyDictionary<string, object>)>();

			public void Track(string eventName, IReadOnlyDictionary<string, object> properties)
			{
				Calls.Add((eventName, properties));
			}
		}

		[SetUp]
		public void Setup()
		{
			_saveRoot = Path.Combine(Path.GetTempPath(), "AnalyticsServiceTests_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_saveRoot);

			_save = new SaveService(_saveRoot);
			_save.Load();

			_provider = new RecordingProvider();
			_svc = new AnalyticsService(_save, _provider);
		}

		[TearDown]
		public void Teardown()
		{
			if (Directory.Exists(_saveRoot))
				Directory.Delete(_saveRoot, recursive: true);
		}

		// ─── Ctor (boundary validation) ──────────────────

		[Test]
		public void Ctor_NullSave_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new AnalyticsService(null, _provider));
		}

		[Test]
		public void Ctor_NullProvider_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new AnalyticsService(_save, null));
		}

		// ─── Opt-in gating ──────────────────────────────

		[Test]
		public void Track_OptedOut_ProviderNotCalled()
		{
			Assert.IsFalse(_save.Schema.Settings.AnalyticsOptIn, "Default schema must be opt-out per COPPA.");

			_svc.Track("session_start", null);
			Assert.IsEmpty(_provider.Calls);
		}

		[Test]
		public void Track_OptedIn_ProviderCalled()
		{
			_save.Schema.Settings.AnalyticsOptIn = true;

			_svc.Track("session_start", null);
			Assert.AreEqual(1, _provider.Calls.Count);
			Assert.AreEqual("session_start", _provider.Calls[0].Name);
		}

		[Test]
		public void Track_OptedIn_ForwardsProperties()
		{
			_save.Schema.Settings.AnalyticsOptIn = true;
			var props = new Dictionary<string, object> { { "duration_s", 120 }, { "game", "tossy" } };

			_svc.Track("minigame_complete", props);

			Assert.AreSame(props, _provider.Calls[0].Props);
		}

		[Test]
		public void Track_OptInToggle_TakesEffectImmediately()
		{
			_svc.Track("a", null);
			Assert.IsEmpty(_provider.Calls);

			_save.Schema.Settings.AnalyticsOptIn = true;
			_svc.Track("b", null);
			Assert.AreEqual(1, _provider.Calls.Count);

			_save.Schema.Settings.AnalyticsOptIn = false;
			_svc.Track("c", null);
			Assert.AreEqual(1, _provider.Calls.Count, "Toggling back to opt-out must drop subsequent events.");
		}

		// ─── Argument validation ────────────────────────

		[Test]
		public void Track_EmptyEventName_Throws()
		{
			_save.Schema.Settings.AnalyticsOptIn = true;
			Assert.Throws<ArgumentException>(() => _svc.Track("", null));
		}

		// ─── NullProvider sanity ────────────────────────

		[Test]
		public void NullProvider_DoesNotThrow()
		{
			var nullProv = new NullProvider();
			Assert.DoesNotThrow(() => nullProv.Track("anything", null));
		}
	}
}
