using System;
using System.Collections.Generic;
using PFound.Utilities.Collections;
using PFound.Utilities.EnumTools;
using PFColl = PFound.Utilities.Collections.CollectionExtensions;

namespace PFound.Utilities.Tests
{
    // Standalone (Unity-free) test runner for the engine-free utility modules.
    // Build & run:
    //   csc -nologo -warn:0 -out:/tmp/pf_util.exe \
    //       Assets/PFound/Utilities/Collections/CollectionExtensions.cs \
    //       Assets/PFound/Utilities/EnumTools/EnumHelper.cs \
    //       Assets/PFound/Utilities/Collections/Tests/UtilitiesTestRunner.cs \
    //   && mono /tmp/pf_util.exe
    public static class UtilitiesTestRunner
    {
        private static int _passed;
        private static int _total;

        public static int Main()
        {
            // ---- IsNullOrEmpty / HasAny ------------------------------------
            Check("IsNullOrEmpty null", ((List<int>)null).IsNullOrEmpty());
            Check("IsNullOrEmpty empty", new List<int>().IsNullOrEmpty());
            Check("IsNullOrEmpty non-empty", !new List<int> { 1 }.IsNullOrEmpty());
            Check("HasAny true", new List<int> { 1 }.HasAny());
            Check("HasAny false", !new List<int>().HasAny());

            // ---- AddUnique / AddRangeUnique --------------------------------
            var uq = new List<int> { 1, 2 };
            Check("AddUnique adds new", uq.AddUnique(3) && uq.Count == 3);
            Check("AddUnique skips dup", !uq.AddUnique(2) && uq.Count == 3);
            Check("AddRangeUnique count", uq.AddRangeUnique(new[] { 2, 3, 4, 5 }) == 2 && uq.Count == 5);

            // ---- InsertSorted / InsertSortedUnique -------------------------
            var sorted = new List<int> { 1, 3, 5, 7 };
            int idx = sorted.InsertSorted(4);
            Check("InsertSorted index", idx == 2);
            Check("InsertSorted order", IsAscending(sorted) && sorted.Count == 5);
            var su = new List<int> { 1, 3, 5 };
            Check("InsertSortedUnique new", su.InsertSortedUnique(4) == 2);
            Check("InsertSortedUnique dup", su.InsertSortedUnique(3) == -1 && su.Count == 4);
            var empty = new List<int>();
            Check("InsertSorted into empty", empty.InsertSorted(9) == 0 && empty[0] == 9);

            // ---- RemoveNulls -----------------------------------------------
            var withNulls = new List<string> { "a", null, "b", null, null, "c" };
            Check("RemoveNulls count", withNulls.RemoveNulls() == 3);
            Check("RemoveNulls survivors", withNulls.Count == 3 && withNulls[0] == "a" && withNulls[2] == "c");

            // ---- GetOrCreate -----------------------------------------------
            var goc = new Dictionary<string, List<int>>();
            goc.GetOrCreate("x").Add(10);
            goc.GetOrCreate("x").Add(20);
            Check("GetOrCreate default new()", goc["x"].Count == 2);
            var gocf = new Dictionary<string, int>();
            int made = gocf.GetOrCreate("k", () => 42);
            Check("GetOrCreate factory", made == 42 && gocf.GetOrCreate("k", () => 99) == 42);

            // ---- AddToList / RemoveFromList --------------------------------
            var dol = new Dictionary<string, List<int>>();
            dol.AddToList("g", 1);
            dol.AddToList("g", 2);
            Check("AddToList builds list", dol["g"].Count == 2);
            Check("RemoveFromList removes", dol.RemoveFromList("g", 1) && dol["g"].Count == 1);
            Check("RemoveFromList drops empty key", dol.RemoveFromList("g", 2) && !dol.ContainsKey("g"));
            Check("RemoveFromList missing key", !dol.RemoveFromList("nope", 5));

            // ---- Increment / Decrement -------------------------------------
            var counter = new Dictionary<string, int>();
            Check("Increment first", counter.Increment("a") == 1);
            Check("Increment again", counter.Increment("a") == 2);
            Check("Increment amount", counter.Increment("a", 5) == 7);
            Check("Decrement", counter.Decrement("a", 3) == 4);

            // ---- FindDuplicates / RemoveDuplicates -------------------------
            var dups = new[] { 1, 2, 2, 3, 3, 3, 4 }.FindDuplicates();
            Check("FindDuplicates", dups.Count == 2 && dups.Contains(2) && dups.Contains(3));
            var rd = new List<int> { 1, 1, 2, 3, 3, 3, 1 };
            Check("RemoveDuplicates count", rd.RemoveDuplicates() == 4);
            Check("RemoveDuplicates order", rd.Count == 3 && rd[0] == 1 && rd[1] == 2 && rd[2] == 3);

            // ---- Move / Swap -----------------------------------------------
            var mv = new List<int> { 0, 1, 2, 3, 4 };
            mv.Move(0, 4);
            Check("Move to end", mv[4] == 0 && mv[0] == 1);
            mv.Move(4, 0);
            Check("Move back", mv[0] == 0 && mv[1] == 1);
            var sw = new List<int> { 10, 20, 30 };
            sw.Swap(0, 2);
            Check("Swap", sw[0] == 30 && sw[2] == 10);

            // ---- Fill ------------------------------------------------------
            var fillVal = new List<int> { 0, 0, 0 };
            fillVal.Fill(7);
            Check("Fill value", fillVal[0] == 7 && fillVal[2] == 7);
            var fillFac = new int[4];
            fillFac.Fill(i => i * i);
            Check("Fill factory", fillFac[3] == 9 && fillFac[2] == 4);

            // ---- DequeueFront ----------------------------------------------
            var q = new List<int> { 100, 200, 300 };
            Check("DequeueFront value", q.DequeueFront() == 100 && q.Count == 2 && q[0] == 200);
            var q2 = new List<int>();
            Check("DequeueFrontOrDefault empty", q2.DequeueFrontOrDefault(out var found) == 0 && !found);
            Check("DequeueFront empty throws", Throws(() => new List<int>().DequeueFront()));

            // ---- EnsureCapacity / Resize -----------------------------------
            var cap = new List<int>();
            cap.EnsureCapacity(64);
            Check("EnsureCapacity grows", cap.Capacity >= 64);
            int[] arr = { 1, 2, 3 };
            PFColl.Resize(ref arr, 5);
            Check("Resize grow", arr.Length == 5 && arr[0] == 1 && arr[4] == 0);
            PFColl.Resize(ref arr, 2);
            Check("Resize shrink", arr.Length == 2 && arr[1] == 2);
            int[] nullArr = null;
            PFColl.Resize(ref nullArr, 3);
            Check("Resize from null", nullArr.Length == 3);

            // ---- Concat / ConcatAll ----------------------------------------
            var c = new[] { 1, 2 }.Concat(new[] { 3, 4, 5 });
            Check("Concat", c.Length == 5 && c[0] == 1 && c[4] == 5);
            var ca = PFColl.ConcatAll(new[] { 1 }, null, new[] { 2, 3 }, new int[0]);
            Check("ConcatAll skips null/empty", ca.Length == 3 && ca[2] == 3);

            // ---- Byte-sequence search --------------------------------------
            byte[] hay = { 0, 1, 2, 3, 1, 2, 3, 9, 1, 2, 3 };
            byte[] needle = { 1, 2, 3 };
            Check("IndexOfSequence first", hay.IndexOfSequence(needle) == 1);
            Check("IndexOfSequence from", hay.IndexOfSequence(needle, 2) == 4);
            Check("IndexOfSequence miss", hay.IndexOfSequence(new byte[] { 5, 5 }) == -1);
            Check("IndexOfSequence empty pattern", hay.IndexOfSequence(new byte[0]) == 0);
            var all = hay.IndexesOfSequence(needle);
            Check("IndexesOfSequence all", all.Count == 3 && all[0] == 1 && all[1] == 4 && all[2] == 8);

            // ---- EnumHelper ------------------------------------------------
            Check("ParseOrDefault valid", EnumHelper.ParseOrDefault("Green", Color.Red) == Color.Green);
            Check("ParseOrDefault invalid", EnumHelper.ParseOrDefault("Purple", Color.Red) == Color.Red);
            Check("ParseOrDefault blank", EnumHelper.ParseOrDefault("  ", Color.Blue) == Color.Blue);
            Check("ParseOrDefault case", EnumHelper.ParseOrDefault("green", Color.Red) == Color.Green);
            Check("TryParse success", EnumHelper.TryParse("Blue", Color.Red, out var tp) && tp == Color.Blue);
            Check("TryParse failure", !EnumHelper.TryParse("Nope", Color.Red, out var tp2) && tp2 == Color.Red);
            var vals = EnumHelper.GetValues<Color>();
            Check("GetValues", vals.Length == 3 && vals[0] == Color.Red);
            var names = EnumHelper.GetNames<Color>();
            Check("GetNames", names.Length == 3 && names[1] == "Green");
            var entries = EnumHelper.GetEntries<Color>();
            Check("GetEntries pairing", entries.Length == 3 && entries[2].IntValue == 5 && entries[2].Name == "Blue");
            Check("IsAnyOf true", Color.Green.IsAnyOf(Color.Red, Color.Green));
            Check("IsAnyOf false", !Color.Blue.IsAnyOf(Color.Red, Color.Green));
            Check("IsDefined value true", EnumHelper.IsDefined(Color.Blue));
            Check("IsDefined int true", EnumHelper.IsDefined<Color>(5));
            Check("IsDefined int false", !EnumHelper.IsDefined<Color>(99));

            Console.WriteLine($"\n{_passed}/{_total} passed.");
            return _passed == _total ? 0 : 1;
        }

        private enum Color { Red = 0, Green = 1, Blue = 5 }

        private static bool IsAscending(IList<int> list)
        {
            for (int i = 1; i < list.Count; i++)
                if (list[i] < list[i - 1]) return false;
            return true;
        }

        private static bool Throws(Action action)
        {
            try { action(); return false; }
            catch { return true; }
        }

        private static void Check(string name, bool condition)
        {
            _total++;
            if (condition)
            {
                _passed++;
            }
            else
            {
                Console.WriteLine($"FAIL: {name}");
            }
        }
    }
}
