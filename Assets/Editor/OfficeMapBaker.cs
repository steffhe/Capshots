using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Capshots > Bake Office Map: builds the Office floor's meshes, colliders and signs in the editor,
// with the same code OfficeMap runs when the scene loads, and saves them as a prefab with its
// meshes, materials and textures as assets. The prefab is placed under the Office object in
// Office.unity and set as its "Baked" floor, so the scene loads it instead of building the floor.
//
// With the floor in the scene, Unity can bake lighting (Window > Rendering > Lighting > Generate
// Lighting) and occlusion culling (Window > Rendering > Occlusion Culling > Bake) for it.
//
// Run it again after changing the layout (Tools/OfficeMap): OfficeMap ignores a bake made from an
// older layout and builds the floor at load, as it did before there were bakes.
public static class OfficeMapBaker
{
    private const string ScenePath = "Assets/Scenes/Office.unity";
    private const string OutputFolder = "Assets/Prefabs/Office Floor";
    private const string PrefabPath = OutputFolder + "/Office Floor.prefab";

    [MenuItem("Capshots/Bake Office Map")]
    public static void Bake()
    {
        if (!Prepare(out Scene scene, out OfficeMap map)) return;

        TextAsset layout = map.layout != null ? map.layout : Resources.Load<TextAsset>("Maps/Office");
        if (layout == null)
        {
            EditorUtility.DisplayDialog("Bake Office Map", "There is no layout (Assets/Resources/Maps/Office.txt).", "OK");
            return;
        }
        OfficeMapData data = OfficeMapData.Parse(layout.text);

        RemoveBake(map);
        CreateFolder(OutputFolder);
        CreateFolder(OutputFolder + "/Meshes");
        CreateFolder(OutputFolder + "/Materials");
        CreateFolder(OutputFolder + "/Textures");

        var root = new GameObject("Office Floor") { layer = map.gameObject.layer };
        try
        {
            EditorUtility.DisplayProgressBar("Bake Office Map", "Building the floor", 0.1f);
            Material[] materials = map.BuildStatic(data, root.transform, true);

            EditorUtility.DisplayProgressBar("Bake Office Map", "Saving materials", 0.3f);
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                Texture texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                if (texture != null && !AssetDatabase.Contains(texture))
                {
                    AssetDatabase.CreateAsset(texture, $"{OutputFolder}/Textures/{FileName(texture.name)}.asset");
                }
                AssetDatabase.CreateAsset(material, $"{OutputFolder}/Materials/{i:00} {FileName(material.name)}.mat");
            }

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                // Signs make their own meshes when they load
                if (mesh == null || filters[i].GetComponent<TMPro.TMP_Text>() != null) continue;
                EditorUtility.DisplayProgressBar("Bake Office Map", "Lightmap UVs for " + mesh.name, 0.3f + 0.6f * i / filters.Length);
                // The meshes' own UVs follow world position and overlap, so lightmaps need a set of their own
                Unwrapping.GenerateSecondaryUVSet(mesh);
                AssetDatabase.CreateAsset(mesh, $"{OutputFolder}/Meshes/{i:000} {FileName(mesh.name)}.asset");
            }

            OfficeBake bake = root.AddComponent<OfficeBake>();
            bake.layoutHash = OfficeMap.LayoutHash(layout);
            bake.glass = MaterialNamed(data, materials, "glass");
            bake.frost = MaterialNamed(data, materials, "frost");

            EditorUtility.DisplayProgressBar("Bake Office Map", "Saving the prefab", 0.95f);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            root = null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(map.transform, false);
            map.baked = instance.GetComponent<OfficeBake>();
            EditorUtility.SetDirty(map);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = instance;
            Debug.Log($"Office map baked to {PrefabPath} ({filters.Length} meshes, {materials.Length} materials). " +
                      "Generate lighting and bake occlusion culling in Office.unity to make the most of it.");
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            EditorUtility.ClearProgressBar();
        }
    }

    // Back to building the floor when the scene loads
    [MenuItem("Capshots/Remove Office Map Bake")]
    public static void Remove()
    {
        if (!Prepare(out Scene scene, out OfficeMap map)) return;
        RemoveBake(map);
        EditorUtility.SetDirty(map);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Office map bake removed; the floor is built when the scene loads again.");
    }

    // Opens Office.unity (after offering to save the open scenes) and finds its OfficeMap
    private static bool Prepare(out Scene scene, out OfficeMap map)
    {
        scene = default;
        map = null;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Bake Office Map", "Stop play mode first.", "OK");
            return false;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

        scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        map = Object.FindFirstObjectByType<OfficeMap>();
        if (map == null)
        {
            EditorUtility.DisplayDialog("Bake Office Map", ScenePath + " has no OfficeMap.", "OK");
            return false;
        }
        return true;
    }

    // The bake in the scene and its assets
    private static void RemoveBake(OfficeMap map)
    {
        foreach (OfficeBake old in map.GetComponentsInChildren<OfficeBake>(true))
        {
            Object.DestroyImmediate(old.gameObject);
        }
        map.baked = null;
        if (AssetDatabase.IsValidFolder(OutputFolder)) AssetDatabase.DeleteAsset(OutputFolder);
    }

    private static Material MaterialNamed(OfficeMapData data, Material[] materials, string name)
    {
        int index = data.materials.FindIndex(m => m.name == name);
        return index >= 0 ? materials[index] : null;
    }

    private static void CreateFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        CreateFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static string FileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }
}
