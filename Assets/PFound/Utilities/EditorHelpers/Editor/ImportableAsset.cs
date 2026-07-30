using UnityEngine;

namespace PFound.Utilities.EditorHelpers
{
    /// <summary>
    /// Base class for ScriptableObject assets that expose a one-click Import
    /// action in their Inspector. <see cref="ImportableAssetEditor"/> renders
    /// the button automatically — subclasses that already have a custom editor
    /// can call <see cref="ImportableAssetEditor.DrawImportButton"/> at the end
    /// of their own <c>OnInspectorGUI</c> to keep the same UX.
    /// </summary>
    public abstract class ImportableAsset : ScriptableObject
    {
        /// <summary>Performs the import operation. Called from the Inspector button.</summary>
        public abstract void Import();

        /// <summary>Whether the Import button is currently enabled. Default true.</summary>
        public virtual bool CanImport => true;

        /// <summary>
        /// Optional explanatory text shown above the Import button when
        /// <see cref="CanImport"/> is false (missing URL, file ref, etc.).
        /// </summary>
        public virtual string CannotImportReason => null;
    }
}
