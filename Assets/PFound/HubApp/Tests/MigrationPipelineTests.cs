using System;
using PFound.HubApp.Save;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace PFound.HubApp.Tests
{
	[TestFixture]
	public class MigrationPipelineTests
	{
		// ─── Test fixtures (stub migrations) ─────────────

		private class RenameOldToNew_V1ToV2 : IMigration
		{
			public int FromVersion => 1;
			public int ToVersion => 2;
			public void Apply(JObject root)
			{
				var v = (string)root["old"];
				root.Remove("old");
				root["new"] = v;
			}
		}

		private class AddCounter_V2ToV3 : IMigration
		{
			public int FromVersion => 2;
			public int ToVersion => 3;
			public void Apply(JObject root) => root["counter"] = 42;
		}

		// ─── Apply ────────────────────────────────────────

		[Test]
		public void Apply_NoOp_WhenCurrentEqualsTarget()
		{
			var pipe = new MigrationPipeline();
			var root = JObject.Parse(@"{""SchemaVersion"":1,""x"":""y""}");

			pipe.Apply(root, currentVersion: 1, targetVersion: 1);

			Assert.AreEqual(1, (int)root["SchemaVersion"]);
			Assert.AreEqual("y", (string)root["x"]);
		}

		[Test]
		public void Apply_SingleStep_TransformsAndBumpsVersion()
		{
			var pipe = new MigrationPipeline();
			pipe.Register(new RenameOldToNew_V1ToV2());

			var root = JObject.Parse(@"{""SchemaVersion"":1,""old"":""hello""}");
			pipe.Apply(root, currentVersion: 1, targetVersion: 2);

			Assert.AreEqual(2, (int)root["SchemaVersion"]);
			Assert.IsNull(root["old"]);
			Assert.AreEqual("hello", (string)root["new"]);
		}

		[Test]
		public void Apply_MultiStepChain_AppliesInOrder()
		{
			var pipe = new MigrationPipeline();
			pipe.Register(new RenameOldToNew_V1ToV2());
			pipe.Register(new AddCounter_V2ToV3());

			var root = JObject.Parse(@"{""SchemaVersion"":1,""old"":""hi""}");
			pipe.Apply(root, currentVersion: 1, targetVersion: 3);

			Assert.AreEqual(3, (int)root["SchemaVersion"]);
			Assert.AreEqual("hi", (string)root["new"]);
			Assert.AreEqual(42, (int)root["counter"]);
		}

		// ─── Fail-fast: structural errors throw ──────────

		[Test]
		public void Apply_GapInChain_Throws()
		{
			var pipe = new MigrationPipeline();
			pipe.Register(new RenameOldToNew_V1ToV2());
			// v2→v3 NOT registered

			var root = JObject.Parse(@"{""SchemaVersion"":1}");
			var ex = Assert.Throws<InvalidOperationException>(() =>
				pipe.Apply(root, currentVersion: 1, targetVersion: 3));
			StringAssert.Contains("v2", ex.Message);
			StringAssert.Contains("v3", ex.Message);
		}

		[Test]
		public void Apply_DowngradeAttempt_Throws()
		{
			var pipe = new MigrationPipeline();
			var root = JObject.Parse(@"{""SchemaVersion"":3}");

			var ex = Assert.Throws<InvalidOperationException>(() =>
				pipe.Apply(root, currentVersion: 3, targetVersion: 1));
			StringAssert.Contains("downgrade", ex.Message.ToLowerInvariant());
		}

		[Test]
		public void Apply_NullRoot_Throws()
		{
			var pipe = new MigrationPipeline();
			Assert.Throws<ArgumentNullException>(() => pipe.Apply(null, 1, 1));
		}

		// ─── Fail-fast: registration errors throw ────────

		[Test]
		public void Register_DuplicateFromVersion_Throws()
		{
			var pipe = new MigrationPipeline();
			pipe.Register(new RenameOldToNew_V1ToV2());

			Assert.Throws<InvalidOperationException>(() =>
				pipe.Register(new RenameOldToNew_V1ToV2()));
		}

		[Test]
		public void Register_NonAdjacentStep_Throws()
		{
			var pipe = new MigrationPipeline();
			Assert.Throws<ArgumentException>(() =>
				pipe.Register(new SkippingMigration_V1ToV3()));
		}

		[Test]
		public void Register_Null_Throws()
		{
			var pipe = new MigrationPipeline();
			Assert.Throws<ArgumentNullException>(() => pipe.Register(null));
		}

		private class SkippingMigration_V1ToV3 : IMigration
		{
			public int FromVersion => 1;
			public int ToVersion => 3;
			public void Apply(JObject root) { }
		}
	}
}
