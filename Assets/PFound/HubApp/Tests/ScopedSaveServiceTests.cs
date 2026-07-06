using System;
using System.Collections.Generic;
using PFound.HubApp.MiniGame;
using PFound.HubApp.Save;
using NUnit.Framework;

namespace PFound.HubApp.Tests
{
	[TestFixture]
	public class ScopedSaveServiceTests
	{
		// In-memory ISaveService stub so we don't touch disk in these tests.
		private class InMemorySaveService : ISaveService
		{
			public SaveSchema Schema { get; private set; } = new SaveSchema();
			public void Load() { Schema = new SaveSchema(); }
			public void Save() { }
			public void Flush() { }
			public void Reset() { Schema = new SaveSchema(); }
		}

		private InMemorySaveService _save;

		[SetUp]
		public void Setup()
		{
			_save = new InMemorySaveService();
		}

		// ─── Ctor (boundary validation) ──────────────────

		[Test]
		public void Ctor_NullInner_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => new ScopedSaveService(null, "p_001", "tossytoss"));
		}

		[Test]
		public void Ctor_EmptyProfileId_Throws()
		{
			Assert.Throws<ArgumentException>(() => new ScopedSaveService(_save, "", "tossytoss"));
		}

		[Test]
		public void Ctor_EmptyGameId_Throws()
		{
			Assert.Throws<ArgumentException>(() => new ScopedSaveService(_save, "p_001", ""));
		}

		// ─── Set + Get roundtrip ─────────────────────────

		[Test]
		public void Set_ThenGet_RoundtripPrimitive()
		{
			var scoped = new ScopedSaveService(_save, "p_001", "tossytoss");
			scoped.Set("progress", 5);
			Assert.AreEqual(5, scoped.Get<int>("progress"));
		}

		[Test]
		public void Set_ThenGet_RoundtripPoco()
		{
			var scoped = new ScopedSaveService(_save, "p_001", "tossytoss");
			var src = new Sample { Score = 1240, Name = "Bo" };
			scoped.Set("snapshot", src);
			var got = scoped.Get<Sample>("snapshot");
			Assert.AreEqual(1240, got.Score);
			Assert.AreEqual("Bo", got.Name);
		}

		[Test]
		public void Set_AutoCreatesProfileAndGameSubtrees()
		{
			Assert.IsFalse(_save.Schema.Games.ContainsKey("p_001"));

			var scoped = new ScopedSaveService(_save, "p_001", "tossytoss");
			scoped.Set("k", 1);

			Assert.IsTrue(_save.Schema.Games.ContainsKey("p_001"));
			Assert.IsTrue(_save.Schema.Games["p_001"].PerGameState.ContainsKey("tossytoss"));
		}

		// ─── Has ─────────────────────────────────────────

		[Test]
		public void Has_NoSubtreeYet_ReturnsFalse()
		{
			var scoped = new ScopedSaveService(_save, "p_001", "tossytoss");
			Assert.IsFalse(scoped.Has("anything"));
		}

		[Test]
		public void Has_AfterSet_ReturnsTrue()
		{
			var scoped = new ScopedSaveService(_save, "p_001", "tossytoss");
			scoped.Set("k", 1);
			Assert.IsTrue(scoped.Has("k"));
		}

		[Test]
		public void Has_OtherKey_StillFalse()
		{
			var scoped = new ScopedSaveService(_save, "p_001", "tossytoss");
			scoped.Set("k", 1);
			Assert.IsFalse(scoped.Has("other"));
		}

		// ─── Get fail-fast on missing ────────────────────

		[Test]
		public void Get_MissingProfileSubtree_Throws()
		{
			var scoped = new ScopedSaveService(_save, "p_001", "tossytoss");
			Assert.Throws<KeyNotFoundException>(() => scoped.Get<int>("anything"));
		}

		[Test]
		public void Get_MissingGameSubtree_Throws()
		{
			// Pre-create the profile subtree but NOT the game subtree.
			_save.Schema.Games["p_001"] = new GamesProfileData();
			var scoped = new ScopedSaveService(_save, "p_001", "tossytoss");
			Assert.Throws<KeyNotFoundException>(() => scoped.Get<int>("anything"));
		}

		[Test]
		public void Get_MissingKey_Throws()
		{
			var scoped = new ScopedSaveService(_save, "p_001", "tossytoss");
			scoped.Set("k", 1);
			Assert.Throws<KeyNotFoundException>(() => scoped.Get<int>("missing"));
		}

		// ─── Remove ──────────────────────────────────────

		[Test]
		public void Remove_ExistingKey_Removes()
		{
			var scoped = new ScopedSaveService(_save, "p_001", "tossytoss");
			scoped.Set("k", 1);
			scoped.Remove("k");
			Assert.IsFalse(scoped.Has("k"));
		}

		[Test]
		public void Remove_MissingKey_Throws()
		{
			var scoped = new ScopedSaveService(_save, "p_001", "tossytoss");
			scoped.Set("anchor", 0); // create subtree
			Assert.Throws<KeyNotFoundException>(() => scoped.Remove("nope"));
		}

		// ─── Isolation: profile + game namespacing ───────

		[Test]
		public void DifferentGameIds_DoNotCollide()
		{
			var tossy = new ScopedSaveService(_save, "p_001", "tossytoss");
			var camp = new ScopedSaveService(_save, "p_001", "camping");

			tossy.Set("level", 10);
			camp.Set("level", 99);

			Assert.AreEqual(10, tossy.Get<int>("level"));
			Assert.AreEqual(99, camp.Get<int>("level"));
		}

		[Test]
		public void DifferentProfileIds_DoNotCollide()
		{
			var p1 = new ScopedSaveService(_save, "p_001", "tossytoss");
			var p2 = new ScopedSaveService(_save, "p_002", "tossytoss");

			p1.Set("level", 5);
			p2.Set("level", 50);

			Assert.AreEqual(5, p1.Get<int>("level"));
			Assert.AreEqual(50, p2.Get<int>("level"));
		}

		// ─── Argument validation ─────────────────────────

		[Test]
		public void Set_EmptyKey_Throws()
		{
			var scoped = new ScopedSaveService(_save, "p_001", "tossytoss");
			Assert.Throws<ArgumentException>(() => scoped.Set("", 1));
		}

		[Test]
		public void Get_EmptyKey_Throws()
		{
			var scoped = new ScopedSaveService(_save, "p_001", "tossytoss");
			Assert.Throws<ArgumentException>(() => scoped.Get<int>(""));
		}

		// ─── Test helper POCO ────────────────────────────

		private class Sample
		{
			public int Score;
			public string Name;
		}
	}
}
