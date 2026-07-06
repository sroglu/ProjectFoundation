using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PFound.Utilities.ParticleTools;

namespace PFound.Utilities.ParticleTools.Tests
{
    /// <summary>
    /// EditMode coverage for <see cref="ParticleControl"/>. Particle systems are created as
    /// real components and cleaned up in <see cref="TearDown"/>. Assertions focus on
    /// transform placement and play/stop state, which are deterministic in edit mode.
    /// </summary>
    public class ParticleToolsTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private ParticleSystem NewSystem(string name = "PS")
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var system = go.AddComponent<ParticleSystem>();
            // Stop auto-play so state starts from a known baseline.
            var main = system.main;
            main.playOnAwake = false;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                    Object.DestroyImmediate(_spawned[i]);
            }
            _spawned.Clear();
        }

        [Test]
        public void PlaceAndPlay_MovesTransformAndStartsPlaying()
        {
            ParticleSystem system = NewSystem();
            var target = new Vector3(3f, 4f, 5f);

            system.PlaceAndPlay(target);

            Assert.AreEqual(target, system.transform.position);
            Assert.IsTrue(system.isPlaying);
        }

        [Test]
        public void PlaceAndPlay_AppliesRotation()
        {
            ParticleSystem system = NewSystem();
            Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);

            system.PlaceAndPlay(new Vector3(1f, 0f, 0f), rotation);

            Assert.AreEqual(rotation.eulerAngles.y, system.transform.rotation.eulerAngles.y, 0.01f);
        }

        [Test]
        public void PlaceAndPlay_NullSystem_ReturnsNullWithoutThrowing()
        {
            ParticleSystem nothing = null;
            Assert.DoesNotThrow(() => nothing.PlaceAndPlay(Vector3.zero));
            Assert.IsNull(nothing.PlaceAndPlay(Vector3.zero));
        }

        [Test]
        public void PlayAll_StartsEverySystem()
        {
            var systems = new[] { NewSystem("A"), NewSystem("B") };

            systems.PlayAll();

            Assert.IsTrue(systems[0].isPlaying);
            Assert.IsTrue(systems[1].isPlaying);
        }

        [Test]
        public void StopAll_StopsEverySystem()
        {
            var systems = new[] { NewSystem("A"), NewSystem("B") };
            systems.PlayAll();

            systems.StopAll();

            Assert.IsFalse(systems[0].isEmitting);
            Assert.IsFalse(systems[1].isEmitting);
        }

        [Test]
        public void SetPlaying_TogglesWholeArray()
        {
            var systems = new[] { NewSystem("A"), NewSystem("B") };

            systems.SetPlaying(true);
            Assert.IsTrue(systems[0].isPlaying);

            systems.SetPlaying(false);
            Assert.IsFalse(systems[0].isEmitting);
        }

        [Test]
        public void BatchHelpers_TolerateNullEntriesAndNullArray()
        {
            var systems = new[] { NewSystem("A"), null };

            Assert.DoesNotThrow(() => systems.PlayAll());
            Assert.DoesNotThrow(() => systems.StopAll());
            Assert.DoesNotThrow(() => systems.SetPlaying(true));

            ParticleSystem[] nullArray = null;
            Assert.DoesNotThrow(() => nullArray.PlayAll());
            Assert.DoesNotThrow(() => nullArray.StopAll());
        }
    }
}
