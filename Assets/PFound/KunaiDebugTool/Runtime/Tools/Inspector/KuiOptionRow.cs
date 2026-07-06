using System;
using UnityEngine;

namespace Kunai
{
    /// <summary>
    /// Per-type row renderer for an Inspector option. Draws label + control;
    /// reads via <see cref="KuiOptionEntry.Get"/> on every render and writes
    /// via <see cref="KuiOptionEntry.Set"/> when the user changes the widget
    /// (no observer pattern, no dirty-tracking — see research.md R11).
    /// </summary>
    internal static class KuiOptionRow
    {
        public static void Draw(KuiOptionEntry entry)
        {
            object cur;
            try { cur = entry.Get(); }
            catch (Exception e)
            {
                KUI.Label($"  {entry.Label}: <get failed: {e.Message}>");
                return;
            }

            if (entry.MemberType == typeof(bool))
            {
                bool prev = cur is bool b && b;
                bool next = KUI.Toggle(prev, "  " + entry.Label);
                if (next != prev) Write(entry, next);
            }
            else if (entry.MemberType == typeof(int)
                  || entry.MemberType == typeof(float)
                  || entry.MemberType == typeof(double))
            {
                DrawNumeric(entry, cur);
            }
            else if (entry.MemberType.IsEnum)
            {
                DrawEnum(entry, cur);
            }
            else if (entry.MemberType == typeof(string))
            {
                // Read-only Label per spec (no inline TextField in v1 to keep
                // the UI predictable; users wanting editable strings should
                // expose a typed property + Commander command).
                KUI.Label($"  {entry.Label}: {cur ?? "<null>"}");
            }
            else
            {
                KUI.Label($"  {entry.Label}: <unsupported type {entry.MemberType.Name}>");
            }
        }

        // ---- numeric ----------------------------------------------------------

        static void DrawNumeric(KuiOptionEntry entry, object cur)
        {
            double curD = ToDouble(cur);

            if (entry.Min.HasValue && entry.Max.HasValue)
            {
                // Slider mode: clamps on user interaction.
                float min = (float)entry.Min.Value;
                float max = (float)entry.Max.Value;
                KUI.Label($"  {entry.Label}: {Format(entry.MemberType, curD)}");
                float prev = (float)curD;
                float next = KUI.Slider(prev, min, max);
                if (!Mathf.Approximately(next, prev))
                    Write(entry, ConvertTo(entry.MemberType, next));
            }
            else
            {
                // Stepper: label-with-value on its own row above the -/+ buttons.
                // Original single-row layout placed three full-width Labels in
                // a horizontal group; KuiLayout.NextRect(height) (no-width
                // overload) takes the full ContentWidth and does NOT advance
                // CursorX, so the +/- buttons drew on top of the value Label.
                // Splitting into label-row + buttons-row mirrors the slider
                // mode and avoids the overlap entirely.
                KUI.Label($"  {entry.Label}: {Format(entry.MemberType, curD)}");
                using (KUI.BeginGroup())
                {
                    if (KUI.Button("-", 60f)) Write(entry, ConvertTo(entry.MemberType, curD - StepFor(entry.MemberType)));
                    if (KUI.Button("+", 60f)) Write(entry, ConvertTo(entry.MemberType, curD + StepFor(entry.MemberType)));
                }
            }
        }

        static double StepFor(Type t)
        {
            if (t == typeof(int)) return 1.0;
            return 0.1;     // float / double
        }

        static double ToDouble(object v)
        {
            switch (v)
            {
                case int    i: return i;
                case float  f: return f;
                case double d: return d;
            }
            return 0.0;
        }

        static object ConvertTo(Type t, double v)
        {
            if (t == typeof(int))    return (int)Math.Round(v);
            if (t == typeof(float))  return (float)v;
            if (t == typeof(double)) return v;
            return v;
        }

        static string Format(Type t, double v)
        {
            if (t == typeof(int)) return ((int)Math.Round(v)).ToString();
            return v.ToString("F2");
        }

        // ---- enum -------------------------------------------------------------

        static void DrawEnum(KuiOptionEntry entry, object cur)
        {
            // Cycle button: shows current value; clicking advances to the next
            // declared value (wraps).
            string label = $"  {entry.Label}: {cur}";
            if (KUI.Button(label))
            {
                var values = Enum.GetValues(entry.MemberType);
                int curIdx = -1;
                for (int i = 0; i < values.Length; i++)
                    if (Equals(values.GetValue(i), cur)) { curIdx = i; break; }
                int nextIdx = (curIdx + 1) % values.Length;
                Write(entry, values.GetValue(nextIdx));
            }
        }

        // ---- write helper -----------------------------------------------------

        static void Write(KuiOptionEntry entry, object value)
        {
            try { entry.Set(value); }
            catch (Exception e)
            {
                Debug.LogWarning($"[KuiOption] '{entry.Label}': set failed — {e.Message}");
            }
        }
    }
}
