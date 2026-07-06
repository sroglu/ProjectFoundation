#if UNITY_5_3_OR_NEWER
using UnityEngine;

namespace PFound.Utilities.ApplicationTools
{
    /// <summary>
    /// Reads and writes the operating-system text clipboard through Unity's
    /// <see cref="GUIUtility.systemCopyBuffer"/>. Isolated behind
    /// <c>UNITY_5_3_OR_NEWER</c> so the rest of the module stays engine-free and
    /// compilable outside Unity.
    /// </summary>
    public static class Clipboard
    {
        /// <summary>Gets the current text contents of the system clipboard.</summary>
        public static string GetText()
        {
            return GUIUtility.systemCopyBuffer;
        }

        /// <summary>Replaces the system clipboard contents with the given text.</summary>
        public static void SetText(string text)
        {
            GUIUtility.systemCopyBuffer = text ?? string.Empty;
        }
    }
}
#endif
