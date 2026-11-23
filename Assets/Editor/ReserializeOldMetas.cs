using System.Linq;
using UnityEditor;
using UnityEngine;

// Capshots > Upgrade Old Asset Metas: rewrites the .meta files of everything under Assets/ExampleAssets in
// the current Unity format. The ExampleAssets textures still have TextureImporter version 6 metas,
// which Unity upgrades in memory on every import and warns about in each CI build. Run once, then
// commit the changed .meta files.
public static class ReserializeOldMetas
{
    [MenuItem("Capshots/Upgrade Old Asset Metas")]
    public static void Run()
    {
        string[] paths = AssetDatabase.FindAssets("", new[] { "Assets/ExampleAssets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct()
            .ToArray();

        AssetDatabase.ForceReserializeAssets(paths, ForceReserializeAssetsOptions.ReserializeMetadata);
        Debug.Log($"Capshots: rewrote {paths.Length} .meta files under Assets/ExampleAssets. Commit the changed files.");
    }
}
