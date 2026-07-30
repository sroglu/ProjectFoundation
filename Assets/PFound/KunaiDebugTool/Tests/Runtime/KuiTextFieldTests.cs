using NUnit.Framework;

namespace Kunai.Tests
{
    [TestFixture]
    public class KuiTextFieldTests
    {
        // ---- SetText / GetText round-trip --------------------------------------

        [Test]
        public void SetText_GetText_RoundTrip_ShortString()
        {
            var s = default(KuiTextFieldState);
            s.SetText("hello");
            Assert.AreEqual("hello", s.GetText());
            Assert.AreEqual(5, s.Length);
            Assert.AreEqual(5, s.Cursor, "Cursor should land at end after SetText");
        }

        [Test]
        public void SetText_Empty_ResetsLengthAndCursor()
        {
            var s = default(KuiTextFieldState);
            s.SetText("seed");
            s.SetText(string.Empty);
            Assert.AreEqual(string.Empty, s.GetText());
            Assert.AreEqual(0, s.Length);
            Assert.AreEqual(0, s.Cursor);
        }

        [Test]
        public void SetText_LongerThanBuffer_TruncatesSilently()
        {
            var s = default(KuiTextFieldState);
            string overflow = new string('x', KuiTextFieldState.BufferCapacity + 50);
            s.SetText(overflow);
            Assert.AreEqual(KuiTextFieldState.BufferCapacity, s.Length);
            Assert.AreEqual(KuiTextFieldState.BufferCapacity, s.Cursor);
        }

        [Test]
        public void GetText_DefaultState_ReturnsEmpty()
        {
            var s = default(KuiTextFieldState);
            Assert.AreEqual(string.Empty, s.GetText());
        }

        // ---- Cursor + edit primitives -----------------------------------------

        [Test]
        public void InsertAtCursor_AppendsAtEnd()
        {
            var s = default(KuiTextFieldState);
            s.EnsureBuffer();
            KuiTextField.InsertAtCursor(ref s, 'a');
            KuiTextField.InsertAtCursor(ref s, 'b');
            KuiTextField.InsertAtCursor(ref s, 'c');
            Assert.AreEqual("abc", s.GetText());
            Assert.AreEqual(3, s.Cursor);
        }

        [Test]
        public void InsertAtCursor_InsertsInMiddle()
        {
            var s = default(KuiTextFieldState);
            s.SetText("ac");
            s.Cursor = 1;
            KuiTextField.InsertAtCursor(ref s, 'b');
            Assert.AreEqual("abc", s.GetText());
            Assert.AreEqual(2, s.Cursor);
        }

        [Test]
        public void InsertAtCursor_AtFullBuffer_NoOp()
        {
            var s = default(KuiTextFieldState);
            s.SetText(new string('x', KuiTextFieldState.BufferCapacity));
            int beforeLen = s.Length;
            int beforeCursor = s.Cursor;
            KuiTextField.InsertAtCursor(ref s, 'y');
            Assert.AreEqual(beforeLen, s.Length);
            Assert.AreEqual(beforeCursor, s.Cursor);
        }

        [Test]
        public void BackspaceAtCursor_AtEnd_RemovesLastChar()
        {
            var s = default(KuiTextFieldState);
            s.SetText("abc");
            KuiTextField.BackspaceAtCursor(ref s);
            Assert.AreEqual("ab", s.GetText());
            Assert.AreEqual(2, s.Cursor);
        }

        [Test]
        public void BackspaceAtCursor_AtMiddle_RemovesPrevChar()
        {
            var s = default(KuiTextFieldState);
            s.SetText("abc");
            s.Cursor = 2;        // between 'b' and 'c'
            KuiTextField.BackspaceAtCursor(ref s);
            Assert.AreEqual("ac", s.GetText());
            Assert.AreEqual(1, s.Cursor);
        }

        [Test]
        public void BackspaceAtCursor_AtZero_NoOp()
        {
            var s = default(KuiTextFieldState);
            s.SetText("abc");
            s.Cursor = 0;
            KuiTextField.BackspaceAtCursor(ref s);
            Assert.AreEqual("abc", s.GetText());
            Assert.AreEqual(0, s.Cursor);
        }

        [Test]
        public void Clear_KeepsBufferResetsLengthCursor()
        {
            var s = default(KuiTextFieldState);
            s.SetText("hello");
            s.Clear();
            Assert.AreEqual(0, s.Length);
            Assert.AreEqual(0, s.Cursor);
            Assert.AreEqual(string.Empty, s.GetText());
        }

        // ---- Mobile-keyboard-style diff (PumpMobileKeyboard internals) ---------

        // The mobile path can't be exercised end-to-end in EditMode (no
        // TouchScreenKeyboard), but the diff invariant is "mirror the source
        // text into the buffer". We can simulate the mirror manually via
        // SetText and check GetText sees the same payload.
        [Test]
        public void SetText_FromExternalSource_BufferMatches()
        {
            var s = default(KuiTextFieldState);
            s.SetText("typed-on-soft-keyboard");
            Assert.AreEqual("typed-on-soft-keyboard", s.GetText());
            Assert.AreEqual(s.Length, s.Cursor, "Cursor follows length on bulk set");
        }
    }
}
