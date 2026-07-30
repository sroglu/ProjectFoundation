using System;
using System.Collections.Generic;
using PFound.HubApp.Services.ParentGate;
using NUnit.Framework;

namespace PFound.HubApp.Tests
{
	[TestFixture]
	public class ParentGateServiceTests
	{
		// ─── GenerateChallenge: shape ────────────────────

		[Test]
		public void GenerateChallenge_HasFourOptions()
		{
			var svc = new ParentGateService(seed: 42);
			var c = svc.GenerateChallenge();
			Assert.AreEqual(ParentGateService.OptionCount, c.Options.Length);
		}

		[Test]
		public void GenerateChallenge_OptionsInRange()
		{
			var svc = new ParentGateService(seed: 42);
			for (var i = 0; i < 50; i++)
			{
				var c = svc.GenerateChallenge();
				foreach (var opt in c.Options)
				{
					Assert.GreaterOrEqual(opt, ParentGateService.MinDigit);
					Assert.LessOrEqual(opt, ParentGateService.MaxDigit);
				}
			}
		}

		[Test]
		public void GenerateChallenge_OptionsDistinct()
		{
			// Repeated digits would let a toddler tap any of two matching tiles — defeats the gate.
			var svc = new ParentGateService(seed: 42);
			for (var i = 0; i < 50; i++)
			{
				var c = svc.GenerateChallenge();
				var set = new HashSet<int>(c.Options);
				Assert.AreEqual(c.Options.Length, set.Count, $"Challenge options not unique: [{string.Join(",", c.Options)}]");
			}
		}

		[Test]
		public void GenerateChallenge_CorrectIndexPointsToPromptNumber()
		{
			var svc = new ParentGateService(seed: 42);
			for (var i = 0; i < 50; i++)
			{
				var c = svc.GenerateChallenge();
				Assert.AreEqual(c.PromptNumber, c.Options[c.CorrectIndex]);
			}
		}

		// ─── Verify ──────────────────────────────────────

		[Test]
		public void Verify_CorrectIndex_ReturnsTrue()
		{
			var svc = new ParentGateService(seed: 42);
			var c = svc.GenerateChallenge();
			Assert.IsTrue(svc.Verify(c, c.CorrectIndex));
		}

		[Test]
		public void Verify_WrongIndex_ReturnsFalse()
		{
			var svc = new ParentGateService(seed: 42);
			var c = svc.GenerateChallenge();
			var wrong = (c.CorrectIndex + 1) % c.Options.Length;
			Assert.IsFalse(svc.Verify(c, wrong));
		}

		[Test]
		public void Verify_NullChallenge_Throws()
		{
			var svc = new ParentGateService();
			Assert.Throws<ArgumentNullException>(() => svc.Verify(null, 0));
		}

		[Test]
		public void Verify_OutOfRangeIndex_Throws()
		{
			var svc = new ParentGateService(seed: 42);
			var c = svc.GenerateChallenge();
			Assert.Throws<ArgumentOutOfRangeException>(() => svc.Verify(c, -1));
			Assert.Throws<ArgumentOutOfRangeException>(() => svc.Verify(c, c.Options.Length));
		}

		// ─── Determinism via seed ───────────────────────

		[Test]
		public void GenerateChallenge_SameSeed_ProducesSameSequence()
		{
			var a = new ParentGateService(seed: 12345);
			var b = new ParentGateService(seed: 12345);

			for (var i = 0; i < 5; i++)
			{
				var ca = a.GenerateChallenge();
				var cb = b.GenerateChallenge();
				Assert.AreEqual(ca.PromptNumber, cb.PromptNumber);
				Assert.AreEqual(ca.CorrectIndex, cb.CorrectIndex);
				CollectionAssert.AreEqual(ca.Options, cb.Options);
			}
		}
	}
}
