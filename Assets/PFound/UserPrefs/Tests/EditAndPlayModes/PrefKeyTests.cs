using NUnit.Framework;
using PFound.UserPrefs;

namespace PFound.UserPrefs.Tests
{
    public class PrefKeyTests
    {
        [Test]
        public void Equality_SameKey_AreEqual()
        {
            var a = new PrefKey<int>("foo", 1);
            var b = new PrefKey<int>("foo", 99);
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
        }

        [Test]
        public void Equality_DifferentKey_AreNotEqual()
        {
            var a = new PrefKey<int>("foo", 1);
            var b = new PrefKey<int>("bar", 1);
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void Construction_NullKey_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new PrefKey<int>(null, 0));
        }

        [Test]
        public void Construction_EmptyKey_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new PrefKey<int>("", 0));
        }

        [Test]
        public void Construction_WhitespaceKey_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new PrefKey<int>("   ", 0));
        }

        [Test]
        public void Construction_ReservedPrefix_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new PrefKey<int>("__userprefs.version", 0));
        }

        [Test]
        public void DefaultValue_StoredCorrectly()
        {
            var k = new PrefKey<float>("audio.master", 0.7f);
            Assert.AreEqual(0.7f, k.DefaultValue);
        }

        [Test]
        public void Storage_AutoIsDefault()
        {
            var k = new PrefKey<int>("foo", 0);
            Assert.AreEqual(PrefStorage.Auto, k.Storage);
        }
    }
}
