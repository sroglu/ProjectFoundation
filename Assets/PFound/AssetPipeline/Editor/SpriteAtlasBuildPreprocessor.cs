using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace PFound.AssetPipeline.Editor
{
    /// <summary>
    /// Stops builder-managed sprite atlases from double-shipping. A managed atlas is marked include-in-build so it
    /// renders in the editor, but its sprites also ship inside a content bundle; if the atlas stayed included it
    /// would be embedded in the player build too — every sprite twice. Just before a build, this preprocessor turns
    /// include-in-build OFF for every managed atlas (recognized by its stored input hash), so the bundle is the sole
    /// copy. The content build produces the bundle from the same authoring source, and the runtime resolves an
    /// atlas's sprites from that bundle.
    /// </summary>
    public sealed class SpriteAtlasBuildPreprocessor : IPreprocessBuildWithReport
    {
        // Run early so the include-in-build flag is already off by the time the atlas/bundle steps read it.
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            int changed = 0;
            foreach (string atlasPath in SpriteAtlasBuilder.ManagedAtlasPaths())
            {
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                if (atlas == null) continue;
                if (!SpriteAtlasExtensions.IsIncludeInBuild(atlas)) continue;

                SpriteAtlasExtensions.SetIncludeInBuild(atlas, false);
                EditorUtility.SetDirty(atlas);
                changed++;
            }

            if (changed > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[AssetPipeline] Disabled include-in-build on {changed} managed sprite atlas(es) so their sprites ship once (via bundle).");
            }
        }
    }
}
