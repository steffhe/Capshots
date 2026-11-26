using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Builds the Test Site 07 map: Capshots > Test Site 07.
//  - Build Kit: the materials, meshes and prefabs under Assets/Prefabs/Test Site 07
//  - Build Scene: Assets/Scenes/Test Site 07.unity, made from the Prototype scene the first time (for
//    its Manager, HUD canvas, event system and map camera), with the map under its "Map" object. Later
//    builds only replace that "Map" object, so the scene keeps its GUID and anything set on the Manager.
//  - Register: adds the scene to the build and the map to the menu's map list
// The scene is edited without being made the active scene, so whatever is open in the editor is left
// as it is.
namespace TestSite07
{
    public static class Builder
    {
        public const string ScenePath = "Assets/Scenes/Test Site 07.unity";
        private const string TemplatePath = "Assets/Scenes/Prototype.unity";
        private const string MenuPath = "Assets/Scenes/Menu.unity";
        private const string LightingPath = "Assets/Scenes/Test Site 07 Lighting.lighting";
        public const string MapName = "Test Site 07";

        [MenuItem("Capshots/Test Site 07/Build Everything")]
        public static void BuildEverything()
        {
            BuildKit();
            BuildScene();
            Register();
        }

        [MenuItem("Capshots/Test Site 07/Build Kit")]
        public static void BuildKit()
        {
            try
            {
                EditorUtility.DisplayProgressBar(MapName, "Materials", 0.1f);
                Kit.BuildMaterials();
                Kit.RebuildBlocks();
                EditorUtility.DisplayProgressBar(MapName, "Modules and props", 0.3f);
                Props.BuildAll();
                EditorUtility.DisplayProgressBar(MapName, "Buildings", 0.7f);
                Buildings.BuildAll();
                AssetDatabase.SaveAssets();
                Debug.Log("Test Site 07: kit built");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Capshots/Test Site 07/Build Scene")]
        public static void BuildScene()
        {
            if (!File.Exists(ScenePath))
            {
                if (!AssetDatabase.CopyAsset(TemplatePath, ScenePath)) throw new System.Exception("Couldn't copy " + TemplatePath);
                AssetDatabase.ImportAsset(ScenePath);
            }

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasOpen = scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                EditorUtility.DisplayProgressBar(MapName, "Placing the map", 0.5f);
                // The template's map (or the last build) goes; the Manager, HUD and camera stay
                foreach (GameObject r in scene.GetRootGameObjects())
                    if (r.name == "Map") Object.DestroyImmediate(r);

                var map = new GameObject("Map");
                SceneManager.MoveGameObjectToScene(map, scene);
                var design = new GameObject(MapName);
                design.transform.SetParent(map.transform, false);
                var layout = new Layout(design.transform);
                layout.BuildAll();

                Light sun = CreateSun(map.transform);
                CreateBounds(map.transform);
                CreateLightProbes(map.transform);

                Manager manager = null;
                foreach (GameObject r in scene.GetRootGameObjects())
                {
                    manager = r.GetComponentInChildren<Manager>(true);
                    if (manager != null) break;
                }
                if (manager == null) throw new System.Exception("No Manager in " + ScenePath);
                var so = new SerializedObject(manager);
                SerializedProperty points = so.FindProperty("spawnPoints");
                points.arraySize = layout.spawns.Count;
                for (int i = 0; i < layout.spawns.Count; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = layout.spawns[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                if (manager.mapcam != null)
                {
                    // The map view's hand-picked shot: over team A's shoulder, looking up the map
                    manager.mapcam.transform.SetPositionAndRotation(new Vector3(60f, 46f, -16f), Quaternion.LookRotation(new Vector3(60f, 0f, 44f) - new Vector3(60f, 46f, -16f)));
                }

                Scene previous = SceneManager.GetActiveScene();
                SceneManager.SetActiveScene(scene);
                ApplyRenderSettings(sun);
                SceneManager.SetActiveScene(previous);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"Test Site 07: scene built ({layout.spawns.Count} spawn points, {layout.objectives.Count} objectives)");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            }
        }

        // Bright afternoon sun from the west-south-west
        private static Light CreateSun(Transform parent)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(42f, 62f, 0f);
            Light sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.78f;
            // Mixed: real-time direct light and shadows (players and props move), baked bounce light
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            return sun;
        }

        // Four invisible walls round the map, shown as force fields like the other outdoor maps
        private static void CreateBounds(Transform parent)
        {
            var bounds = new GameObject("Bounds");
            bounds.transform.SetParent(parent, false);
            const float h = 24f, t = 1f, x = Layout.SizeX, z = Layout.SizeZ;
            void Wall(string name, Vector3 min, Vector3 max)
            {
                var w = new GameObject(name) { layer = Kit.Ground };
                w.transform.SetParent(bounds.transform, false);
                w.transform.position = (min + max) / 2f;
                w.AddComponent<BoxCollider>().size = max - min;
                GameObjectUtility.SetStaticEditorFlags(w, StaticEditorFlags.BatchingStatic);
            }
            Wall("Wall West", new Vector3(-t, -1f, -t), new Vector3(0f, h, z + t));
            Wall("Wall East", new Vector3(x, -1f, -t), new Vector3(x + t, h, z + t));
            Wall("Wall South", new Vector3(-t, -1f, -t), new Vector3(x + t, h, 0f));
            Wall("Wall North", new Vector3(-t, -1f, z), new Vector3(x + t, h, z + t));
            BoundaryField field = bounds.AddComponent<BoundaryField>();
            field.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ForceField.mat");
            field.walls = new BoxCollider[0];
        }

        // A grid of light probes (every 10 m, at head height and at upper-floor height) so props and
        // players pick up the baked bounce light
        private static void CreateLightProbes(Transform parent)
        {
            var go = new GameObject("Light Probes");
            go.transform.SetParent(parent, false);
            var group = go.AddComponent<LightProbeGroup>();
            var positions = new List<Vector3>();
            for (float x = 2f; x <= Layout.SizeX; x += 10f)
                for (float z = 2f; z <= Layout.SizeZ; z += 10f)
                {
                    positions.Add(new Vector3(x, 1.2f, z));
                    positions.Add(new Vector3(x, 6f, z));
                }
            group.probePositions = positions.ToArray();
        }

        // Run with the map's scene active: ambient, fog and lightmap settings
        private static void ApplyRenderSettings(Light sun)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.68f, 0.77f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.80f, 0.72f, 0.60f);
            RenderSettings.ambientGroundColor = new Color(0.47f, 0.40f, 0.32f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.87f, 0.81f, 0.71f);
            RenderSettings.fogDensity = 0.0042f;
            RenderSettings.sun = sun;

            LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            if (settings == null)
            {
                settings = new LightingSettings { name = "Test Site 07 Lighting" };
                AssetDatabase.CreateAsset(settings, LightingPath);
            }
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            settings.lightmapResolution = 3f;
            settings.lightmapPadding = 2;
            settings.lightmapMaxSize = 1024;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.lightmapCompression = LightmapCompression.NormalQuality;
            settings.ao = true;
            settings.aoMaxDistance = 1.2f;
            settings.directSampleCount = 32;
            settings.indirectSampleCount = 256;
            settings.maxBounces = 2;
            EditorUtility.SetDirty(settings);
            Lightmapping.lightingSettings = settings;
        }

        // Adds the scene to the build (after the other maps) and the map to the menu's map list
        [MenuItem("Capshots/Test Site 07/Register In Build And Menu")]
        public static void Register()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int at = scenes.FindIndex(s => s.path == ScenePath);
            if (at < 0) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            else scenes[at].enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();

            // Build index = position among the enabled scenes
            int buildIndex = 0;
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (s.path == ScenePath) break;
                if (s.enabled) buildIndex++;
            }

            Scene menu = SceneManager.GetSceneByPath(MenuPath);
            bool wasOpen = menu.isLoaded;
            if (wasOpen && menu.isDirty)
            {
                Debug.LogWarning("Test Site 07: Menu.unity has unsaved changes, so the map wasn't added to its map list. Save it and run Register again.");
                return;
            }
            if (!wasOpen) menu = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Additive);
            try
            {
                Launcher launcher = null;
                foreach (GameObject r in menu.GetRootGameObjects())
                {
                    launcher = r.GetComponentInChildren<Launcher>(true);
                    if (launcher != null) break;
                }
                if (launcher == null) throw new System.Exception("No Launcher in " + MenuPath);
                var so = new SerializedObject(launcher);
                SerializedProperty maps = so.FindProperty("maps");
                int found = -1;
                for (int i = 0; i < maps.arraySize; i++)
                    if (maps.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == MapName) found = i;
                if (found < 0)
                {
                    found = maps.arraySize;
                    maps.arraySize++;
                }
                SerializedProperty entry = maps.GetArrayElementAtIndex(found);
                entry.FindPropertyRelative("name").stringValue = MapName;
                entry.FindPropertyRelative("scene").intValue = buildIndex;
                entry.FindPropertyRelative("enabled").boolValue = true;
                if (so.ApplyModifiedPropertiesWithoutUndo())
                {
                    EditorSceneManager.MarkSceneDirty(menu);
                    EditorSceneManager.SaveScene(menu);
                }
                Debug.Log($"Test Site 07: in the build as scene {buildIndex}, and in the menu's map list");
            }
            finally
            {
                if (!wasOpen) EditorSceneManager.CloseScene(menu, true);
            }
        }
    }
}
