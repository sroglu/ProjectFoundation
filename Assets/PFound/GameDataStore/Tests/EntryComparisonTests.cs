using System;
using System.Collections.Generic;
using PFound.GameDataStore.Entries;
using NUnit.Framework;
using Unity.Mathematics;

namespace PFound.GameDataStore.Tests
{
    [TestFixture]
    public class EntryComparisonTests
    {
        // Test data: (EntryType, value1, value2_different) for all non-registry types
        // EntityType is excluded because it requires DataIdentifierRegistry initialization
        private static readonly (EntryType type, Entry val1, Entry val2)[] TestPairs =
        {
            (EntryType.Int,    new Entry(42),               new Entry(99)),
            (EntryType.Int2,   new Entry(new int2(1, 2)),   new Entry(new int2(3, 4))),
            (EntryType.Int3,   new Entry(new int3(1,2,3)),  new Entry(new int3(4,5,6))),
            (EntryType.Int64,  new Entry((long)123456789),  new Entry((long)987654321)),
            (EntryType.UInt,   new Entry((uint)10),         new Entry((uint)20)),
            (EntryType.UInt2,  new Entry(new uint2(1,2)),   new Entry(new uint2(3,4))),
            (EntryType.UInt3,  new Entry(new uint3(1,2,3)), new Entry(new uint3(4,5,6))),
            (EntryType.UInt64, new Entry((ulong)100),       new Entry((ulong)200)),
            (EntryType.Short,  new Entry((short)10),        new Entry((short)20)),
            (EntryType.UShort, new Entry((ushort)10),       new Entry((ushort)20)),
            (EntryType.Byte,   new Entry((byte)1),          new Entry((byte)2)),
            (EntryType.SByte,  new Entry((sbyte)1),         new Entry((sbyte)2)),
            (EntryType.Float,  new Entry(1.5f),             new Entry(2.5f)),
            (EntryType.Float2, new Entry(new float2(1,2)),  new Entry(new float2(3,4))),
            (EntryType.Float3, new Entry(new float3(1,2,3)),new Entry(new float3(4,5,6))),
            (EntryType.Double, new Entry(1.5d),             new Entry(2.5d)),
            (EntryType.Char,   new Entry('a'),              new Entry('z')),
            (EntryType.String, new Entry("hello"),          new Entry("world")),
            (EntryType.Bool,   new Entry(true),             new Entry(false)),
            (EntryType.EntityLevel, new Entry(new EntityLevel(1)), new Entry(new EntityLevel(5))),
            (EntryType.WeekDay, new Entry(WeekDay.Monday),  new Entry(WeekDay.Friday)),
        };

        #region Equals Tests

        [Test]
        public void Equals_SameValue_ReturnsTrue_ForAllTypes()
        {
            foreach (var (type, val1, _) in TestPairs)
            {
                Assert.IsTrue(val1.Equals(val1), $"Equals(self) failed for {type}");

                // Create a copy via constructor to ensure it's a different struct instance
                var copy = val1;
                Assert.IsTrue(val1.Equals(copy), $"Equals(copy) failed for {type}");
            }
        }

        [Test]
        public void Equals_DifferentValue_ReturnsFalse_ForAllTypes()
        {
            foreach (var (type, val1, val2) in TestPairs)
            {
                Assert.IsFalse(val1.Equals(val2), $"Equals(different) should be false for {type}");
            }
        }

        [Test]
        public void Equals_DifferentTypes_ReturnsFalse()
        {
            var intEntry = new Entry(42);
            var floatEntry = new Entry(42f);
            var stringEntry = new Entry("42");

            Assert.IsFalse(intEntry.Equals(floatEntry));
            Assert.IsFalse(intEntry.Equals(stringEntry));
            Assert.IsFalse(floatEntry.Equals(stringEntry));
        }

        [Test]
        public void Equals_InvalidEntries_AreEqual()
        {
            Assert.IsTrue(Entry.Invalid.Equals(Entry.Invalid));
        }

        [Test]
        public void Equals_InvalidAndValid_AreNotEqual()
        {
            Assert.IsFalse(Entry.Invalid.Equals(new Entry(0)));
            Assert.IsFalse(new Entry(0).Equals(Entry.Invalid));
        }

        #endregion

        #region GetHashCode Tests

        [Test]
        public void GetHashCode_ConsistentWithEquals_ForAllTypes()
        {
            foreach (var (type, val1, val2) in TestPairs)
            {
                var copy = val1;
                Assert.AreEqual(val1.GetHashCode(), copy.GetHashCode(),
                    $"Hash mismatch for equal {type} entries");

                // Different values may have same hash (collision), but equal values must have same hash
            }
        }

        #endregion

        #region CompareTo Tests

        [Test]
        public void CompareTo_SameValue_ReturnsZero_ForAllTypes()
        {
            foreach (var (type, val1, _) in TestPairs)
            {
                if (type == EntryType.EntityLevel) continue; // EntityLevel needs TextId registry

                var copy = val1;
                Assert.AreEqual(0, val1.CompareTo(copy), $"CompareTo(self) should be 0 for {type}");
            }
        }

        [Test]
        public void CompareTo_DifferentValues_ReturnsNonZero_ForAllTypes()
        {
            foreach (var (type, val1, val2) in TestPairs)
            {
                if (type == EntryType.EntityLevel) continue; // EntityLevel needs TextId registry

                Assert.AreNotEqual(0, val1.CompareTo(val2), $"CompareTo(different) should be nonzero for {type}");
            }
        }

        [Test]
        public void CompareTo_VectorTypes_UseLexicographicOrder()
        {
            // Int2: (1,9) < (2,0) because x is compared first
            var a = new Entry(new int2(1, 9));
            var b = new Entry(new int2(2, 0));
            Assert.Less(a.CompareTo(b), 0);
            Assert.Greater(b.CompareTo(a), 0);

            // Int2: same x, compare y
            var c = new Entry(new int2(1, 2));
            var d = new Entry(new int2(1, 5));
            Assert.Less(c.CompareTo(d), 0);

            // Int3: (1,2,9) < (1,3,0) because y differs
            var e = new Entry(new int3(1, 2, 9));
            var f = new Entry(new int3(1, 3, 0));
            Assert.Less(e.CompareTo(f), 0);

            // Float2: same pattern
            var g = new Entry(new float2(1.0f, 9.0f));
            var h = new Entry(new float2(2.0f, 0.0f));
            Assert.Less(g.CompareTo(h), 0);
        }

        [Test]
        public void CompareTo_DifferentTypes_Throws()
        {
            var intEntry = new Entry(42);
            var floatEntry = new Entry(42f);
            Assert.Throws<Exception>(() => intEntry.CompareTo(floatEntry));
        }

        #endregion

        #region TryParse Tests

        private static readonly (EntryType type, string text, Entry expected)[] ParseTestCases =
        {
            (EntryType.Int,    "42",         new Entry(42)),
            (EntryType.Int2,   "1, 2",       new Entry(new int2(1, 2))),
            (EntryType.Int3,   "1,2,3",      new Entry(new int3(1, 2, 3))),
            (EntryType.Int64,  "123456789",  new Entry((long)123456789)),
            (EntryType.UInt,   "10",         new Entry((uint)10)),
            (EntryType.UInt2,  "1,2",        new Entry(new uint2(1, 2))),
            (EntryType.UInt3,  "1, 2, 3",    new Entry(new uint3(1, 2, 3))),
            (EntryType.UInt64, "100",        new Entry((ulong)100)),
            (EntryType.Short,  "10",         new Entry((short)10)),
            (EntryType.UShort, "10",         new Entry((ushort)10)),
            (EntryType.Byte,   "1",          new Entry((byte)1)),
            (EntryType.SByte,  "1",          new Entry((sbyte)1)),
            (EntryType.Float,  "1.5",        new Entry(1.5f)),
            (EntryType.Float2, "1.5, 2.5",   new Entry(new float2(1.5f, 2.5f))),
            (EntryType.Float3, "1.5,2.5,3.5",new Entry(new float3(1.5f, 2.5f, 3.5f))),
            (EntryType.Double, "1.5",        new Entry(1.5d)),
            (EntryType.Char,   "a",          new Entry('a')),
            (EntryType.String, "hello",      new Entry("hello")),
            (EntryType.Bool,   "True",       new Entry(true)),
        };

        [Test]
        public void TryParse_ValidInput_Succeeds_ForAllTypes()
        {
            foreach (var (type, text, expected) in ParseTestCases)
            {
                var success = Entry.TryParse(type, text, out var result);
                Assert.IsTrue(success, $"TryParse failed for {type} with input '{text}'");
                Assert.IsTrue(expected.Equals(result), $"TryParse result mismatch for {type}: expected {expected}, got {result}");
            }
        }

        [Test]
        public void TryParse_InvalidInput_ReturnsFalse()
        {
            Assert.IsFalse(Entry.TryParse(EntryType.Int, "not_a_number", out _));
            Assert.IsFalse(Entry.TryParse(EntryType.Int2, "1", out _));
            Assert.IsFalse(Entry.TryParse(EntryType.Int3, "1,2", out _));
            Assert.IsFalse(Entry.TryParse(EntryType.Float2, "abc,def", out _));
            Assert.IsFalse(Entry.TryParse(EntryType.UInt3, "1,2", out _));
        }

        [Test]
        public void TryParse_EmptyOrWhitespace_ReturnsFalse_ForVectorTypes()
        {
            Assert.IsFalse(Entry.TryParse(EntryType.Int2, "", out _));
            Assert.IsFalse(Entry.TryParse(EntryType.Int3, "   ", out _));
            Assert.IsFalse(Entry.TryParse(EntryType.Float2, "", out _));
            Assert.IsFalse(Entry.TryParse(EntryType.Float3, "", out _));
            Assert.IsFalse(Entry.TryParse(EntryType.UInt2, "", out _));
            Assert.IsFalse(Entry.TryParse(EntryType.UInt3, "", out _));
        }

        #endregion

        #region Extensibility Guard

        [Test]
        public void AllEntryTypes_CoveredByEquals()
        {
            foreach (EntryType type in Enum.GetValues(typeof(EntryType)))
            {
                if (type == EntryType.Invalid) continue;
                if (type == EntryType.EntityType) continue; // Requires DataIdentifierRegistry

                var entry = CreateTestEntry(type);
                Assert.DoesNotThrow(() => entry.Equals(entry),
                    $"Equals not implemented for {type}");
            }
        }

        [Test]
        public void AllEntryTypes_CoveredByGetHashCode()
        {
            foreach (EntryType type in Enum.GetValues(typeof(EntryType)))
            {
                if (type == EntryType.Invalid) continue;
                if (type == EntryType.EntityType) continue;

                var entry = CreateTestEntry(type);
                Assert.DoesNotThrow(() => entry.GetHashCode(),
                    $"GetHashCode not implemented for {type}");
            }
        }

        [Test]
        public void AllEntryTypes_CoveredByCompareTo()
        {
            foreach (EntryType type in Enum.GetValues(typeof(EntryType)))
            {
                if (type == EntryType.Invalid) continue;
                if (type == EntryType.EntityType) continue; // Requires DataIdentifierRegistry

                var entry = CreateTestEntry(type);
                Assert.DoesNotThrow(() => entry.CompareTo(entry),
                    $"CompareTo not implemented for {type}");
            }
        }

        [Test]
        public void AllEntryTypes_CoveredByToString()
        {
            foreach (EntryType type in Enum.GetValues(typeof(EntryType)))
            {
                if (type == EntryType.Invalid) continue;
                if (type == EntryType.EntityType) continue;

                var entry = CreateTestEntry(type);
                Assert.DoesNotThrow(() => entry.ToString(),
                    $"ToString not implemented for {type}");
            }
        }

        private static Entry CreateTestEntry(EntryType type)
        {
            switch (type)
            {
                case EntryType.Int:         return new Entry(1);
                case EntryType.Int2:        return new Entry(new int2(1, 2));
                case EntryType.Int3:        return new Entry(new int3(1, 2, 3));
                case EntryType.Int64:       return new Entry((long)1);
                case EntryType.UInt:        return new Entry((uint)1);
                case EntryType.UInt2:       return new Entry(new uint2(1, 2));
                case EntryType.UInt3:       return new Entry(new uint3(1, 2, 3));
                case EntryType.UInt64:      return new Entry((ulong)1);
                case EntryType.Short:       return new Entry((short)1);
                case EntryType.UShort:      return new Entry((ushort)1);
                case EntryType.Byte:        return new Entry((byte)1);
                case EntryType.SByte:       return new Entry((sbyte)1);
                case EntryType.Float:       return new Entry(1.0f);
                case EntryType.Float2:      return new Entry(new float2(1, 2));
                case EntryType.Float3:      return new Entry(new float3(1, 2, 3));
                case EntryType.Double:      return new Entry(1.0d);
                case EntryType.Char:        return new Entry('a');
                case EntryType.String:      return new Entry("test");
                case EntryType.Bool:        return new Entry(true);
                case EntryType.EntityType:  return new Entry(new EntityType(1));
                case EntryType.EntityLevel: return new Entry(new EntityLevel(1));
                case EntryType.WeekDay:     return new Entry(WeekDay.Monday);
                default: throw new ArgumentOutOfRangeException(nameof(type), type, $"CreateTestEntry missing case for {type} — add it when adding a new EntryType!");
            }
        }

        #endregion
    }
}
