using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the Office map (the 3rd floor of an office building) when its scene loads, from the
// boxes in Assets/Resources/Maps/Office.txt. That file is made by Tools/OfficeMap/generate.py
// from a layout traced off the floor plan; change the layout there and run the script again.
//
// Boxes with the same material are merged into one mesh per 24 m square, with UVs from world
// position so floor and wall patterns line up and keep their scale. Colliders are plain box
// colliders: on the Ground layer, or on the Glass layer for glass, which blocks players but lets
// shots through (Weapon.canBeShot leaves it out). Every player builds the same map from the same
// file, so nothing here is networked. The spawn points and the map camera are set on the Manager.
public class OfficeMap : MonoBehaviour
{
    [Tooltip("The generated map (Assets/Resources/Maps/Office.txt). Loaded from Resources if empty.")]
    public TextAsset layout;
    [Tooltip("A transparent URP Lit material to copy for glass")]
    public Material glassTemplate;
    [Tooltip("An emissive URP Lit material to copy for lights and screens")]
    public Material glowTemplate;
    [Tooltip("Gets the spawn points and the map camera position")]
    public Manager manager;

    private const float CellSize = 24f;
    private const int CollidersPerObject = 256;

    private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

    private void Awake()
    {
        if (layout == null) layout = Resources.Load<TextAsset>("Maps/Office");
        if (layout == null)
        {
            Debug.LogError("OfficeMap: no layout (Assets/Resources/Maps/Office.txt)");
            return;
        }
        Build(OfficeMapData.Parse(layout.text));
    }

    private void Build(OfficeMapData data)
    {
        Material[] materials = new Material[data.materials.Count];
        for (int i = 0; i < materials.Length; i++) materials[i] = CreateMaterial(data.materials[i]);

        BuildMeshes(data, materials);
        BuildColliders(data);
        BuildGlass(data, materials);
        BuildSigns(data);
        BuildSpawns(data);

        if (data.hasCamera && manager != null && manager.mapcam != null)
        {
            manager.mapcam.transform.SetPositionAndRotation(data.cameraPosition, Quaternion.Euler(data.cameraPitch, data.cameraYaw, 0f));
        }
    }

    #region Materials

    private Material CreateMaterial(OfficeMapData.MaterialDef def)
    {
        Material material;
        switch (def.kind)
        {
            case OfficeMapData.MaterialKind.Glass:
                material = new Material(glassTemplate);
                break;
            case OfficeMapData.MaterialKind.Glow:
                material = new Material(glowTemplate);
                break;
            default:
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                // The fallback is the glow material with its glow turned off below
                material = lit != null ? new Material(lit) : new Material(glowTemplate);
                material.DisableKeyword("_EMISSION");
                break;
        }
        material.name = "Office " + def.name;

        Color color = def.color;
        SetColor(material, "_BaseColor", color);
        SetColor(material, "_Color", color);
        if (def.kind == OfficeMapData.MaterialKind.Glow)
        {
            SetColor(material, "_EmissionColor", new Color(color.r, color.g, color.b) * 1.6f);
        }
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", def.smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", Pattern(def.texture));
            material.SetTextureScale("_BaseMap", Vector2.one);
        }
        if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", null);
        return material;
    }

    private static void SetColor(Material material, string property, Color color)
    {
        if (material.HasProperty(property)) material.SetColor(property, color);
    }

    // Light grey patterns that tint the material's colour: planks, carpet weave, tiles and so on.
    // null for plain materials.
    private static Texture2D Pattern(string kind)
    {
        if (kind == "none") return null;
        if (textures.TryGetValue(kind, out Texture2D cached) && cached != null) return cached;

        const int size = 256;
        var random = new System.Random(kind.GetHashCode() & 0x7fffffff);
        var pixels = new Color32[size * size];
        float[] noise = new float[size * size];
        for (int i = 0; i < noise.Length; i++) noise[i] = (float)random.NextDouble();

        // Per-plank tints and offsets for the wood
        const int planks = 8;
        float[] plankTint = new float[planks];
        float[] plankShift = new float[planks];
        for (int i = 0; i < planks; i++)
        {
            plankTint[i] = 0.82f + 0.18f * (float)random.NextDouble();
            plankShift[i] = (float)random.NextDouble();
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float v = 1f;
                float n = noise[y * size + x];
                switch (kind)
                {
                    case "wood":
                    {
                        int plank = y * planks / size;
                        float along = (x / (float)size + plankShift[plank]) % 1f;
                        float grain = 0.93f + 0.07f * Mathf.Sin((y * 0.9f + noise[(y * size + x / 16) % noise.Length] * 3f));
                        v = plankTint[plank] * grain * (0.97f + 0.03f * n);
                        // Gaps between planks and staggered plank ends
                        if (y % (size / planks) == 0 || along < 0.006f) v *= 0.6f;
                        break;
                    }
                    case "carpet":
                        v = 0.86f + 0.14f * n;
                        if (((x + y) & 3) == 0) v *= 0.95f;
                        break;
                    case "tile":
                        v = 0.95f + 0.05f * n;
                        if (x < 3 || y < 3) v *= 0.82f;
                        break;
                    case "concrete":
                    {
                        float smooth = (noise[y * size + x] + noise[((y + 1) % size) * size + x] + noise[y * size + (x + 1) % size]) / 3f;
                        v = 0.88f + 0.12f * smooth;
                        break;
                    }
                    case "grass":
                        v = 0.72f + 0.28f * n;
                        break;
                }
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
                pixels[y * size + x] = new Color32(b, b, b, 255);
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Office " + kind };
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Trilinear;
        texture.anisoLevel = 4;
        texture.SetPixels32(pixels);
        texture.Apply(true, true);
        textures[kind] = texture;
        return texture;
    }

    #endregion

    #region Meshes

    internal class MeshBuilder
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<int> triangles = new List<int>();

        public void Clear()
        {
            vertices.Clear(); normals.Clear(); uvs.Clear(); triangles.Clear();
        }

        // Puts the geometry into a mesh, replacing what it had
        public void Fill(Mesh mesh)
        {
            mesh.Clear();
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }
    }

    private void BuildMeshes(OfficeMapData data, Material[] materials)
    {
        // Merge boxes by material, shadow casting and 24 m square
        var groups = new Dictionary<(int material, bool shadows, int cx, int cz), MeshBuilder>();
        foreach (OfficeMapData.Box box in data.boxes)
        {
            if (box.material < 0) continue;
            var key = (box.material, box.castShadows, Mathf.FloorToInt(box.center.x / CellSize), Mathf.FloorToInt(box.center.z / CellSize));
            if (!groups.TryGetValue(key, out MeshBuilder builder)) groups[key] = builder = new MeshBuilder();
            AddBox(builder, box.center, box.size, data.materials[box.material].tile);
        }

        Transform parent = new GameObject("Meshes").transform;
        parent.SetParent(transform, false);
        foreach (var entry in groups)
        {
            MeshBuilder b = entry.Value;
            var mesh = new Mesh { name = "Office " + data.materials[entry.Key.material].name };
            if (b.vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(b.vertices);
            mesh.SetNormals(b.normals);
            mesh.SetUVs(0, b.uvs);
            mesh.SetTriangles(b.triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.UploadMeshData(true);

            var go = new GameObject(mesh.name);
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[entry.Key.material];
            renderer.shadowCastingMode = entry.Key.shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = data.materials[entry.Key.material].kind != OfficeMapData.MaterialKind.Glow;
        }
    }

    // Six faces, each with UVs taken from world position along the face (divided by the tile size),
    // or running 0 to 1 across each face when tile is 0
    internal static void AddBox(MeshBuilder b, Vector3 c, Vector3 s, float tile)
    {
        Vector3 h = s * 0.5f;
        Vector3 min = c - h, max = c + h;
        // normal, then two corners spanning the face via its u and v axes
        AddFace(b, Vector3.up, new Vector3(min.x, max.y, min.z), Vector3.right * s.x, Vector3.forward * s.z, tile);
        AddFace(b, Vector3.down, new Vector3(min.x, min.y, max.z), Vector3.right * s.x, Vector3.back * s.z, tile);
        AddFace(b, Vector3.forward, new Vector3(max.x, min.y, max.z), Vector3.left * s.x, Vector3.up * s.y, tile);
        AddFace(b, Vector3.back, new Vector3(min.x, min.y, min.z), Vector3.right * s.x, Vector3.up * s.y, tile);
        AddFace(b, Vector3.right, new Vector3(max.x, min.y, min.z), Vector3.forward * s.z, Vector3.up * s.y, tile);
        AddFace(b, Vector3.left, new Vector3(min.x, min.y, max.z), Vector3.back * s.z, Vector3.up * s.y, tile);
    }

    private static void AddFace(MeshBuilder b, Vector3 normal, Vector3 origin, Vector3 u, Vector3 v, float tile)
    {
        int start = b.vertices.Count;
        Vector3[] corners = { origin, origin + u, origin + u + v, origin + v };
        foreach (Vector3 p in corners)
        {
            b.vertices.Add(p);
            b.normals.Add(normal);
        }
        if (tile > 0f)
        {
            foreach (Vector3 p in corners) b.uvs.Add(WorldUv(p, normal) / tile);
        }
        else
        {
            b.uvs.Add(new Vector2(0f, 0f)); b.uvs.Add(new Vector2(1f, 0f));
            b.uvs.Add(new Vector2(1f, 1f)); b.uvs.Add(new Vector2(0f, 1f));
        }
        // u and v are the face's right and up as seen from outside, so these are clockwise from there
        b.triangles.Add(start); b.triangles.Add(start + 2); b.triangles.Add(start + 1);
        b.triangles.Add(start); b.triangles.Add(start + 3); b.triangles.Add(start + 2);
    }

    private static Vector2 WorldUv(Vector3 p, Vector3 normal)
    {
        if (normal.y != 0f) return new Vector2(p.x, p.z);
        if (normal.z != 0f) return new Vector2(p.x, p.y);
        return new Vector2(p.z, p.y);
    }

    #endregion

    #region Colliders, signs and spawns

    private void BuildColliders(OfficeMapData data)
    {
        int glassLayer = LayerMask.NameToLayer("Glass");
        if (glassLayer < 0) glassLayer = Layers.Glass;
        Transform parent = new GameObject("Colliders").transform;
        parent.SetParent(transform, false);

        GameObject solid = null, glass = null;
        int solidCount = 0, glassCount = 0;
        foreach (OfficeMapData.Box box in data.boxes)
        {
            if (!box.collider) continue;
            GameObject holder;
            if (box.glass)
            {
                if (glass == null || glassCount++ % CollidersPerObject == 0) glass = Holder(parent, "Glass", glassLayer);
                holder = glass;
            }
            else
            {
                if (solid == null || solidCount++ % CollidersPerObject == 0) solid = Holder(parent, "Solid", gameObject.layer);
                holder = solid;
            }
            BoxCollider collider = holder.AddComponent<BoxCollider>();
            collider.center = box.center;
            collider.size = box.size;
        }
    }

    private static GameObject Holder(Transform parent, string name, int layer)
    {
        var go = new GameObject(name) { layer = layer, isStatic = true };
        go.transform.SetParent(parent, false);
        return go;
    }

    // The breakable glass panes: OfficeGlass draws them and breaks them as the Manager says
    private void BuildGlass(OfficeMapData data, Material[] materials)
    {
        if (data.panes.Count == 0) return;
        int glass = data.materials.FindIndex(m => m.name == "glass");
        int frost = data.materials.FindIndex(m => m.name == "frost");
        if (glass < 0 || frost < 0)
        {
            Debug.LogError("OfficeMap: the layout has glass panes but no glass or frost material");
            return;
        }
        int glassLayer = LayerMask.NameToLayer("Glass");
        if (glassLayer < 0) glassLayer = Layers.Glass;
        gameObject.AddComponent<OfficeGlass>().Build(data.panes, materials[glass], materials[frost], glassLayer);
    }

    private void BuildSigns(OfficeMapData data)
    {
        Transform parent = new GameObject("Signs").transform;
        parent.SetParent(transform, false);
        foreach (OfficeMapData.Sign sign in data.signs)
        {
            var go = new GameObject("Sign " + sign.text);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(sign.position, Quaternion.Euler(0f, sign.yaw, 0f));
            TextMeshPro text = go.AddComponent<TextMeshPro>();
            text.text = sign.text;
            // TextMeshPro's 3D text is a tenth of a unit per point; this makes capitals about 'height' tall
            text.fontSize = sign.height * 13f;
            text.color = sign.color;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(30f, sign.height * 2f);
            text.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private void BuildSpawns(OfficeMapData data)
    {
        Transform parent = new GameObject("Spawns").transform;
        parent.SetParent(transform, false);
        var points = new Transform[data.spawns.Count];
        for (int i = 0; i < points.Length; i++)
        {
            var go = new GameObject("Spawn Point (" + i + ")");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(data.spawns[i].position, Quaternion.Euler(0f, data.spawns[i].yaw, 0f));
            points[i] = go.transform;
        }
        if (manager != null && points.Length > 0) manager.spawnPoints = points;
    }

    #endregion
}
