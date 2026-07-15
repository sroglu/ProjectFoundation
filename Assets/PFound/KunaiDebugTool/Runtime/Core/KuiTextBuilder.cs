using System;

namespace Kunai
{
    /// <summary>
    /// Allocation-free text assembly into a caller-owned (or KUI-shared) <c>char[]</c>.
    /// Appends string literals (copied char-by-char, never a new string), integers, and
    /// fixed-2-decimal floats without producing any managed garbage.
    ///
    /// Preferred use from window code is the fluent, KUI-shared form so custom windows stay
    /// zero-GC without managing their own buffer:
    /// <code>KUI.Label(KUI.Text().Add("FPS: ").Add(fps).Add(" / ").Add(ms));</code>
    /// The chained <see cref="Add(string)"/> overloads return the updated builder by value, so
    /// the running length flows through the chain while the backing <c>char[]</c> is written in
    /// place. Writes are clamped to the backing buffer's length; overflow is dropped, never grown.
    /// Build-and-consume immediately (do not store a builder across frames — the shared buffer is
    /// reused by the next <see cref="KUI.Text"/> call).
    /// </summary>
    public struct KuiTextBuilder
    {
        readonly char[] _buf;
        int _len;

        public KuiTextBuilder(char[] buffer)
        {
            _buf = buffer;
            _len = 0;
        }

        public char[] Buffer => _buf;
        public int Length => _len;

        public void Clear() => _len = 0;

        // ---- fluent, chainable (return the updated builder by value) ----------
        public KuiTextBuilder Add(string s)      { Append(s);      return this; }
        public KuiTextBuilder Add(char c)        { Append(c);      return this; }
        public KuiTextBuilder Add(int value)     { AppendInt(value); return this; }
        public KuiTextBuilder Add(long value)    { AppendInt(value); return this; }
        public KuiTextBuilder Add(float value)   { AppendF2(value);  return this; }
        public KuiTextBuilder Add(double value)  { AppendF2((float)value); return this; }
        public KuiTextBuilder Add(bool value)    { Append(value ? "true" : "false"); return this; }

        // ---- primitives -------------------------------------------------------
        public void Append(char c)
        {
            if (_len < _buf.Length) _buf[_len++] = c;
        }

        public void Append(string s)
        {
            if (s == null) return;
            int n = s.Length;
            for (int i = 0; i < n && _len < _buf.Length; i++) _buf[_len++] = s[i];
        }

        /// <summary>Appends the base-10 form of <paramref name="value"/> with no allocation.</summary>
        public void AppendInt(long value)
        {
            if (value < 0) { Append('-'); value = -value; }

            Span<char> tmp = stackalloc char[20];
            int n = 0;
            if (value == 0) tmp[n++] = '0';
            else while (value > 0) { tmp[n++] = (char)('0' + (int)(value % 10)); value /= 10; }
            for (int i = n - 1; i >= 0; i--) Append(tmp[i]);
        }

        /// <summary>Appends <paramref name="value"/> rounded to two decimals (fixed-point), no allocation.</summary>
        public void AppendF2(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) { Append("--"); return; }

            bool neg = value < 0f;
            if (neg) { Append('-'); value = -value; }

            long total   = (long)((double)value * 100.0 + 0.5);
            long intPart = total / 100;
            int  frac    = (int)(total % 100);

            AppendInt(intPart);
            Append('.');
            Append((char)('0' + frac / 10));
            Append((char)('0' + frac % 10));
        }
    }
}
