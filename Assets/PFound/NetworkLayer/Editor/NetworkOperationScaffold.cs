using System.IO;
using UnityEditor;

namespace PFound.NetworkLayer.EditorTools
{
    /// <summary>
    /// Project-window command that scaffolds a new network operation. Unlike the source generator (which
    /// runs every build and fills the wire messages), this is a one-time author action: it WRITES a real,
    /// editable, compile-clean starter file the developer then fills in.
    ///
    /// The file it writes pairs the compact <c>[NetworkOp]</c> spec partial (whose <c>RequestMessage</c> /
    /// <c>ReplyMessage</c> the generator emits) with a matching <c>ServerOperation</c> subclass whose
    /// lifecycle hooks are stubbed so it compiles as-is. It is created in inline-rename mode — exactly like
    /// "Create &gt; C# Script": the name the developer types becomes both the op partial and the subclass.
    /// </summary>
    static class NetworkOperationScaffold
    {
        const string TemplateFileName = "NetworkOperationTemplate.cs.txt";
        const string DefaultAssetName = "NewNetworkOperation.cs";

        [MenuItem("Assets/Create/PFound/Network Operation", false, 82)]
        static void CreateNetworkOperation()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(ResolveTemplatePath(), DefaultAssetName);
        }

        /// <summary>
        /// Locate the template by its imported name rather than a hard-coded path, so the scaffold keeps
        /// working if the template file is moved. Fail-fast: without the template there is nothing to write.
        /// </summary>
        static string ResolveTemplatePath()
        {
            foreach (string guid in AssetDatabase.FindAssets("NetworkOperationTemplate t:TextAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == TemplateFileName)
                    return path;
            }

            throw new FileNotFoundException(
                $"Network Operation scaffold template '{TemplateFileName}' was not found in the project.");
        }
    }
}
