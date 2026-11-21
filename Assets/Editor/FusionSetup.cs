using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-off editor step for the move from Photon PUN to Photon Fusion: Capshots > Set Up Fusion.
// Safe to run again; it skips anything already done.
//
//  - Sets the Fusion App ID.
//  - Player prefab: removes the old PUN components and the Rigidbody, adds NetworkObject,
//    NetworkTransform, a CharacterController sized like the old body and lag-compensated hitboxes
//    (HitboxRoot + one Hitbox per body part, on the Hitbox layer) in place of the body colliders.
//  - Other prefabs in Resources: removes leftover PUN components.
//  - Map scenes: adds a NetworkObject to the Manager, removes leftover PUN components.
public static class FusionSetup
{
    private const string AppId = "12ae61a7-04ee-4c22-9e92-c695b9f73e3e";
    private const string PlayerPrefabPath = "Assets/Resources/Player.prefab";

    [MenuItem("Capshots/Set Up Fusion")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string startScene = SceneManager.GetActiveScene().path;

        SetAppId();
        CheckProjectConfig();
        NameHitboxLayer();
        RemovePunDefines();
        SetUpPlayerPrefab();
        CleanOtherPrefabs();
        SetUpScenes();

        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene);
        Debug.Log("Capshots: Fusion set-up finished.");
    }

    private static void SetAppId()
    {
        PhotonAppSettings settings = PhotonAppSettings.Global;
        if (settings == null)
        {
            Debug.LogError("Capshots: PhotonAppSettings not found. Open Tools > Fusion > Fusion Hub once so Fusion creates it, then run this again.");
            return;
        }

        if (settings.AppSettings.AppIdFusion == AppId) return;
        settings.AppSettings.AppIdFusion = AppId;
        EditorUtility.SetDirty(settings);
        Debug.Log("Capshots: Fusion App ID set.");
    }

    private static void CheckProjectConfig()
    {
        NetworkProjectConfig config = NetworkProjectConfig.Global;
        if (config != null && !config.LagCompensation.Enabled)
        {
            Debug.LogWarning("Capshots: lag compensation is off. Turn on Lag Compensation > Enabled in the Network Project Config (Tools > Fusion > Network Project Config), or shots won't hit players.");
        }
    }

    // Layer 10 holds the hitboxes (see Layers.Hitbox)
    private static void NameHitboxLayer()
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layer = tagManager.FindProperty("layers").GetArrayElementAtIndex(Layers.Hitbox);
        if (layer.stringValue == "Hitbox") return;
        if (!string.IsNullOrEmpty(layer.stringValue))
        {
            Debug.LogError($"Capshots: layer {Layers.Hitbox} is already used for '{layer.stringValue}'. Change Layers.Hitbox to a free layer.");
            return;
        }
        layer.stringValue = "Hitbox";
        tagManager.ApplyModifiedProperties();
        Debug.Log($"Capshots: named layer {Layers.Hitbox} 'Hitbox'.");
    }

    // PUN's scripting defines stay behind after the package is deleted
    private static void RemovePunDefines()
    {
        foreach (UnityEditor.Build.NamedBuildTarget target in new[] {
            UnityEditor.Build.NamedBuildTarget.Standalone, UnityEditor.Build.NamedBuildTarget.WebGL,
            UnityEditor.Build.NamedBuildTarget.Android, UnityEditor.Build.NamedBuildTarget.iOS })
        {
            PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
            string[] kept = defines.Where(d => d != "PHOTON_UNITY_NETWORKING" && !d.StartsWith("PUN_")).ToArray();
            if (kept.Length != defines.Length) PlayerSettings.SetScriptingDefineSymbols(target, kept);
        }
    }

    #region Player prefab

    private static void SetUpPlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            RemoveMissingScripts(root);

            Player player = root.GetComponent<Player>();
            if (player == null)
            {
                Debug.LogError("Capshots: the Player prefab has no Player component.");
                return;
            }

            if (root.GetComponent<CharacterController>() == null) AddCharacterController(root, player);
            AddHitboxes(root, player);

            // Everything that collides is now the character controller; any other collider on the
            // body would push against it
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                if (c is CharacterController) continue;
                Debug.Log($"Capshots: removed {c.GetType().Name} from Player/{PathTo(root.transform, c.transform)}");
                Object.DestroyImmediate(c);
            }

            Rigidbody body = root.GetComponent<Rigidbody>();
            if (body != null) Object.DestroyImmediate(body);

            if (root.GetComponent<NetworkObject>() == null) root.AddComponent<NetworkObject>();
            if (root.GetComponent<NetworkTransform>() == null) root.AddComponent<NetworkTransform>();

            HitboxRoot hitboxRoot = root.GetComponent<HitboxRoot>();
            if (hitboxRoot == null) hitboxRoot = root.AddComponent<HitboxRoot>();
            hitboxRoot.InitHitboxes();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("Capshots: Player prefab set up for Fusion.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // A capsule covering the old standing body and head, and the crouching height from the crouching parts
    private static void AddCharacterController(GameObject root, Player player)
    {
        Bounds standing = BoundsOf(root.transform, player.standingCollider, player.standingColliderHead);
        Bounds crouching = BoundsOf(root.transform, player.crouchingCollider, player.crouchingColliderHead);

        CharacterController controller = root.AddComponent<CharacterController>();
        float radius = Mathf.Max(0.2f, Mathf.Min(standing.extents.x, standing.extents.z));
        controller.radius = radius;
        controller.skinWidth = 0.05f;
        controller.stepOffset = 0.3f;
        controller.slopeLimit = 45f;
        controller.minMoveDistance = 0f;

        player.bodyBottom = standing.min.y;
        player.standingHeight = Mathf.Max(standing.size.y, radius * 2f);
        player.crouchingHeight = Mathf.Clamp(crouching.max.y - standing.min.y, radius * 2f, player.standingHeight);
        controller.height = player.standingHeight;
        controller.center = new Vector3(0f, player.bodyBottom + player.standingHeight / 2f, 0f);
        EditorUtility.SetDirty(player);
    }

    // The colliders' bounds in the root's space (the prefab root sits at the origin, unrotated)
    private static Bounds BoundsOf(Transform root, params GameObject[] parts)
    {
        bool any = false;
        Bounds bounds = new Bounds();
        foreach (GameObject part in parts)
        {
            if (part == null) continue;
            foreach (Collider c in part.GetComponentsInChildren<Collider>(true))
            {
                Bounds b = WorldBounds(c);
                b.center -= root.position;
                if (!any) { bounds = b; any = true; }
                else bounds.Encapsulate(b);
            }
        }
        if (!any) bounds = new Bounds(new Vector3(0f, 0f, 0f), new Vector3(1f, 2f, 1f));
        return bounds;
    }

    // Collider.bounds is empty for disabled objects, so work it out from the shape
    private static Bounds WorldBounds(Collider c)
    {
        Transform t = c.transform;
        Vector3 center, size;
        switch (c)
        {
            case BoxCollider box: center = box.center; size = box.size; break;
            case SphereCollider sphere: center = sphere.center; size = Vector3.one * sphere.radius * 2f; break;
            case CapsuleCollider capsule:
                center = capsule.center;
                size = Vector3.one * capsule.radius * 2f;
                size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2f);
                break;
            default:
                return c.bounds;
        }

        Bounds b = new Bounds(t.TransformPoint(center), Vector3.zero);
        Vector3 half = size / 2f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y, (i & 4) == 0 ? -half.z : half.z);
            b.Encapsulate(t.TransformPoint(center + corner));
        }
        return b;
    }

    // One hitbox per body part, on its own child so it can sit on the Hitbox layer while the part
    // keeps the layer the cameras use to show or hide it. Head hitboxes are tagged Head.
    private static void AddHitboxes(GameObject root, Player player)
    {
        foreach (GameObject part in new[] { player.standingCollider, player.standingColliderHead, player.crouchingCollider, player.crouchingColliderHead })
        {
            if (part == null || part.GetComponentInChildren<Hitbox>(true) != null) continue;

            Collider collider = part.GetComponent<Collider>();
            if (collider == null)
            {
                Debug.LogWarning($"Capshots: {part.name} has no collider to size a hitbox from.");
                continue;
            }

            GameObject go = new GameObject("Hitbox");
            go.layer = Layers.Hitbox;
            go.tag = part.tag;
            go.transform.SetParent(part.transform, false);
            // Hitboxes work in unscaled world units, so cancel the part's scale
            Vector3 scale = part.transform.lossyScale;
            go.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);

            Hitbox hitbox = go.AddComponent<Hitbox>();
            switch (collider)
            {
                case SphereCollider sphere:
                    hitbox.Type = HitboxTypes.Sphere;
                    hitbox.SphereRadius = sphere.radius * Mathf.Max(scale.x, scale.y, scale.z);
                    hitbox.Offset = Vector3.Scale(sphere.center, scale);
                    break;
                case BoxCollider box:
                    hitbox.Type = HitboxTypes.Box;
                    hitbox.BoxExtents = Vector3.Scale(box.size, scale) / 2f;
                    hitbox.Offset = Vector3.Scale(box.center, scale);
                    break;
                case CapsuleCollider capsule:
                {
                    // A box around the capsule: close enough for a body, and needs no capsule axis conventions
                    Vector3 size = Vector3.one * capsule.radius * 2f;
                    size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2f);
                    hitbox.Type = HitboxTypes.Box;
                    hitbox.BoxExtents = Vector3.Scale(size, scale) / 2f;
                    hitbox.Offset = Vector3.Scale(capsule.center, scale);
                    break;
                }
                default:
                {
                    Bounds b = WorldBounds(collider);
                    hitbox.Type = HitboxTypes.Box;
                    hitbox.BoxExtents = b.extents;
                    hitbox.Offset = go.transform.InverseTransformPoint(b.center);
                    break;
                }
            }
        }
    }

    #endregion

    #region Other prefabs and scenes

    private static void CleanOtherPrefabs()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == PlayerPrefabPath) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (RemoveMissingScripts(root) > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"Capshots: removed old PUN components from {path}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void SetUpScenes()
    {
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            // Skip disabled scenes and ones that no longer exist (Fusion Hub can add its sample scenes)
            if (!buildScene.enabled || !System.IO.File.Exists(buildScene.path)) continue;

            Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
            bool changed = false;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (RemoveMissingScripts(root) > 0) changed = true;
            }

            Manager manager = Object.FindAnyObjectByType<Manager>(FindObjectsInactive.Include);
            if (manager != null && manager.GetComponent<NetworkObject>() == null)
            {
                manager.gameObject.AddComponent<NetworkObject>();
                changed = true;
                Debug.Log($"Capshots: added a NetworkObject to the Manager in {scene.name}");
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
    }

    private static int RemoveMissingScripts(GameObject root)
    {
        int removed = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        }
        return removed;
    }

    private static string PathTo(Transform root, Transform t)
    {
        List<string> names = new List<string>();
        for (; t != null && t != root; t = t.parent) names.Add(t.name);
        names.Reverse();
        return string.Join("/", names);
    }

    #endregion
}
