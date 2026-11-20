using UnityEditor;
using UnityEngine;

// Sets up map models (FBX files in Assets/Models/Maps) as Unity imports them:
//  - every part goes on the Ground layer, because the player only counts as standing
//    on that layer (Player.ground), so jumping and sliding work on the whole map
//  - every part is marked static, so Unity batches the many small meshes together
// Colliders come from "Generate Colliders" in the model's import settings.
public class MapModelPostprocessor : AssetPostprocessor
{
    private const string MapModelFolder = "Assets/Models/Maps/";

    private void OnPostprocessModel(GameObject root)
    {
        if (!assetPath.StartsWith(MapModelFolder)) return;

        int ground = LayerMask.NameToLayer("Ground");
        if (ground < 0)
        {
            Debug.LogWarning("MapModelPostprocessor: there is no \"Ground\" layer, so " + assetPath + " stays on its default layer.");
            ground = 0;
        }

        foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
        {
            part.gameObject.layer = ground;
            GameObjectUtility.SetStaticEditorFlags(part.gameObject, StaticEditorFlags.BatchingStatic);
        }
    }
}
