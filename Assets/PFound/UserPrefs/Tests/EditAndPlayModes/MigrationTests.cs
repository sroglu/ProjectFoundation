using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PFound.UserPrefs;

namespace PFound.UserPrefs.Tests
{
    public class MigrationTests
    {
        private static readonly PrefKey<int> Counter = new PrefKey<int>("mig.counter", 0);

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(Counter.Key);
            PlayerPrefs.DeleteKey(PrefsMigrationRunner.VersionKey);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(Counter.Key);
            PlayerPrefs.DeleteKey(PrefsMigrationRunner.VersionKey);
        }

        [Test]
        public void NoMigration_StoredVersionMatches_NoOp()
        {
            // Pre-set version 2
            PlayerPrefs.SetInt(PrefsMigrationRunner.VersionKey, 2);
            int callCount = 0;

            using var store = new PrefsBuilder()
                .Add(Counter)
                .SchemaVersion(2)
                .MigrationFrom(version: 1, to: 2, ctx => callCount++)
                .WithLogger(new NullLogger())
                .Build();

            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void SingleMigration_RunsOnce_VersionUpdated()
        {
            // Stored = 1, current = 2
            PlayerPrefs.SetInt(PrefsMigrationRunner.VersionKey, 1);
            int callCount = 0;

            using var store = new PrefsBuilder()
                .Add(Counter)
                .SchemaVersion(2)
                .MigrationFrom(version: 1, to: 2, ctx => callCount++)
                .WithLogger(new NullLogger())
                .Build();

            Assert.AreEqual(1, callCount);
            Assert.AreEqual(2, PlayerPrefs.GetInt(PrefsMigrationRunner.VersionKey));
        }

        [Test]
        public void ChainedMigrations_RunInOrder()
        {
            PlayerPrefs.SetInt(PrefsMigrationRunner.VersionKey, 1);
            var order = new List<int>();

            using var store = new PrefsBuilder()
                .Add(Counter)
                .SchemaVersion(3)
                .MigrationFrom(version: 1, to: 2, ctx => order.Add(12))
                .MigrationFrom(version: 2, to: 3, ctx => order.Add(23))
                .WithLogger(new NullLogger())
                .Build();

            Assert.AreEqual(2, order.Count);
            Assert.AreEqual(12, order[0]);
            Assert.AreEqual(23, order[1]);
            Assert.AreEqual(3, PlayerPrefs.GetInt(PrefsMigrationRunner.VersionKey));
        }

        [Test]
        public void MigrationException_AbortsChain_VersionNotUpdated()
        {
            PlayerPrefs.SetInt(PrefsMigrationRunner.VersionKey, 1);

            Assert.Throws<PrefsMigrationException>(() =>
            {
                new PrefsBuilder()
                    .Add(Counter)
                    .SchemaVersion(3)
                    .MigrationFrom(version: 1, to: 2, ctx => throw new InvalidOperationException("boom"))
                    .MigrationFrom(version: 2, to: 3, ctx => { /* should not run */ })
                    .WithLogger(new NullLogger())
                    .Build();
            });

            Assert.AreEqual(1, PlayerPrefs.GetInt(PrefsMigrationRunner.VersionKey));
        }

        [Test]
        public void Downgrade_StoredNewerThanCurrent_Throws()
        {
            PlayerPrefs.SetInt(PrefsMigrationRunner.VersionKey, 5);

            Assert.Throws<PrefsSchemaDowngradeException>(() =>
            {
                new PrefsBuilder()
                    .Add(Counter)
                    .SchemaVersion(3)
                    .WithLogger(new NullLogger())
                    .Build();
            });
        }

        [Test]
        public void MissingVersionKey_TreatedAsVersionOne()
        {
            // No version key set
            int callCount = 0;
            using var store = new PrefsBuilder()
                .Add(Counter)
                .SchemaVersion(2)
                .MigrationFrom(version: 1, to: 2, ctx => callCount++)
                .WithLogger(new NullLogger())
                .Build();

            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void Builder_DuplicateMigration_Throws()
        {
            var b = new PrefsBuilder()
                .MigrationFrom(version: 1, to: 2, ctx => { });
            Assert.Throws<ArgumentException>(() =>
                b.MigrationFrom(version: 1, to: 2, ctx => { }));
        }

        [Test]
        public void Builder_NonAdjacentMigration_Throws()
        {
            var b = new PrefsBuilder();
            Assert.Throws<ArgumentException>(() =>
                b.MigrationFrom(version: 1, to: 3, ctx => { }));
        }
    }
}
