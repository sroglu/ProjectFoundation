using System.IO;
using UnityEditor;

namespace PFound.NetworkLayer.EditorTools
{
    /// <summary>
    /// Project-window command that scaffolds a new server operation. Unlike the source generator (which
    /// runs every build and fills the wire messages), this is a one-time author action: it WRITES a real,
    /// editable starter file the developer then fills in.
    ///
    /// The file it writes pairs the compact <c>[RemoteProcedure]</c> spec partial (whose <c>RequestMessage</c> /
    /// <c>ReplyMessage</c> the generator emits) with a matching <c>ServerOperationFlow</c> subclass whose
    /// lifecycle hooks are stubbed. Its opcode references the central <c>NetOpcodes.cs</c> sheet via
    /// <c>NetDomain.TODO</c> / <c>TodoOp.TODO</c> placeholders — the developer points those at a real
    /// domain/op (Alt+Enter → Create member), so a new op is a single line in the one central sheet. It is
    /// created in inline-rename mode — exactly like "Create &gt; C# Script": the name the developer types
    /// becomes both the op partial and the subclass.
    /// </summary>
    static class ServerOperationScaffold
    {
        const string TemplateFileName = "ServerOperationTemplate.cs.txt";
        const string DefaultAssetName = "NewServerOperation.cs";

        [MenuItem("Assets/Create/PFound/Server Operation", false, 82)]
        static void CreateServerOperation()
        {
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(ResolveTemplatePath(), DefaultAssetName);
        }

        /// <summary>
        /// Locate the template by its imported name rather than a hard-coded path, so the scaffold keeps
        /// working if the template file is moved. Fail-fast: without the template there is nothing to write.
        /// </summary>
        static string ResolveTemplatePath()
        {
            foreach (string guid in AssetDatabase.FindAssets("ServerOperationTemplate t:TextAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) == TemplateFileName)
                    return path;
            }

            throw new FileNotFoundException(
                $"Server Operation scaffold template '{TemplateFileName}' was not found in the project.");
        }
    }
}
