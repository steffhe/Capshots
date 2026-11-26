using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// The building kit for Test Site 07: shared flat-colour materials, flat-shaded low-poly meshes, and a
// small prefab builder that merges a prefab's visual parts into one mesh (one sub-mesh per material)
// and keeps its collision as separate primitive colliders on child objects.
//
// Everything is generated, so the whole kit can be rebuilt from code (Capshots > Test Site 07). Assets
// are updated in place, so their GUIDs (and every reference to them) survive a rebuild.
namespace TestSite07
{
    public static class Kit
    {
        public const string Root = "Assets/Prefabs/Test Site 07";
        public const string MaterialDir = Root + "/Materials";
        public const string MeshDir = Root + "/Meshes";
        public const string ModuleDir = Root + "/Modules";
        public const string PropDir = Root + "/Props";
        public const string BlockDir = Root + "/Blocks";
        public const string BuildingDir = Root + "/Buildings";

        public static int Ground => LayerMask.NameToLayer("Ground");
        public static int GlassLayer => LayerMask.NameToLayer("Glass");

        // ---------------------------------------------------------------- materials

        // name, colour, smoothness, metallic, emission (black for none), alpha (< 1: transparent)
        private static readonly (string name, Color color, float smooth, float metal, Color emission, float alpha)[] Specs =
        {
            // Buildings
            ("Turquoise",   C(0.47f, 0.78f, 0.75f), 0.15f, 0f, Color.black, 1f),
            ("Cream",       C(0.96f, 0.90f, 0.77f), 0.12f, 0f, Color.black, 1f),
            ("Coral",       C(0.91f, 0.53f, 0.47f), 0.15f, 0f, Color.black, 1f),
            ("Orange",      C(0.93f, 0.66f, 0.42f), 0.12f, 0f, Color.black, 1f),
            ("Brick",       C(0.71f, 0.42f, 0.34f), 0.08f, 0f, Color.black, 1f),
            ("RoofFlat",    C(0.55f, 0.52f, 0.49f), 0.05f, 0f, Color.black, 1f),
            ("RoofRed",     C(0.66f, 0.38f, 0.33f), 0.10f, 0f, Color.black, 1f),
            ("RoofSlate",   C(0.37f, 0.47f, 0.50f), 0.10f, 0f, Color.black, 1f),
            ("WindowDark",  C(0.22f, 0.33f, 0.40f), 0.85f, 0f, Color.black, 1f),
            ("Glass",       C(0.72f, 0.88f, 0.93f), 0.95f, 0f, Color.black, 0.22f),
            ("ChainLink",   C(0.55f, 0.58f, 0.60f), 0.40f, 0.3f, Color.black, 0.45f),
            // Environment
            ("Sand",        C(0.86f, 0.75f, 0.56f), 0.04f, 0f, Color.black, 1f),
            ("SandDark",    C(0.78f, 0.64f, 0.46f), 0.04f, 0f, Color.black, 1f),
            ("Mesa",        C(0.80f, 0.55f, 0.40f), 0.04f, 0f, Color.black, 1f),
            ("Grass",       C(0.55f, 0.60f, 0.31f), 0.05f, 0f, Color.black, 1f),
            ("Concrete",    C(0.67f, 0.64f, 0.60f), 0.08f, 0f, Color.black, 1f),
            ("Sidewalk",    C(0.79f, 0.76f, 0.71f), 0.08f, 0f, Color.black, 1f),
            ("Asphalt",     C(0.27f, 0.27f, 0.29f), 0.10f, 0f, Color.black, 1f),
            ("Paint",       C(0.95f, 0.93f, 0.85f), 0.10f, 0f, Color.black, 1f),
            // Props
            ("Red",         C(0.72f, 0.29f, 0.27f), 0.25f, 0f, Color.black, 1f),
            ("Yellow",      C(0.93f, 0.81f, 0.43f), 0.20f, 0f, Color.black, 1f),
            ("OffWhite",    C(0.94f, 0.92f, 0.87f), 0.20f, 0f, Color.black, 1f),
            ("DarkGreen",   C(0.22f, 0.37f, 0.27f), 0.10f, 0f, Color.black, 1f),
            ("Foliage",     C(0.45f, 0.56f, 0.28f), 0.05f, 0f, Color.black, 1f),
            ("FoliageDark", C(0.32f, 0.46f, 0.25f), 0.05f, 0f, Color.black, 1f),
            ("Wood",        C(0.63f, 0.46f, 0.32f), 0.10f, 0f, Color.black, 1f),
            ("Bark",        C(0.42f, 0.31f, 0.23f), 0.05f, 0f, Color.black, 1f),
            ("Burlap",      C(0.72f, 0.63f, 0.46f), 0.03f, 0f, Color.black, 1f),
            ("Metal",       C(0.52f, 0.55f, 0.57f), 0.45f, 0.4f, Color.black, 1f),
            ("Chrome",      C(0.85f, 0.86f, 0.88f), 0.80f, 0.8f, Color.black, 1f),
            ("Dark",        C(0.16f, 0.16f, 0.18f), 0.20f, 0f, Color.black, 1f),
            // Glowing parts (lamps, neon, dials)
            ("NeonPink",    C(1.00f, 0.45f, 0.60f), 0.30f, 0f, C(1.00f, 0.40f, 0.58f) * 2.2f, 1f),
            ("NeonTeal",    C(0.40f, 1.00f, 0.92f), 0.30f, 0f, C(0.35f, 1.00f, 0.90f) * 1.8f, 1f),
            ("LampGlow",    C(1.00f, 0.92f, 0.72f), 0.10f, 0f, C(1.00f, 0.88f, 0.62f) * 1.4f, 1f),
            ("DialGlow",    C(0.95f, 0.80f, 0.40f), 0.10f, 0f, C(1.00f, 0.75f, 0.30f) * 1.2f, 1f),
        };

        private static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        public static Material Mat(string name)
        {
            if (materials.TryGetValue(name, out Material m) && m != null) return m;
            m = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{name}.mat");
            if (m == null) throw new Exception("Test Site 07: no material " + name + " (run the kit build first)");
            materials[name] = m;
            return m;
        }

        public static void BuildMaterials()
        {
            EnsureFolders();
            materials.Clear();
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var s in Specs)
            {
                string path = $"{MaterialDir}/{s.name}.mat";
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(lit);
                    AssetDatabase.CreateAsset(m, path);
                }
                m.shader = lit;
                Color baseColor = s.color;
                baseColor.a = s.alpha;
                m.SetColor("_BaseColor", baseColor);
                m.SetFloat("_Smoothness", s.smooth);
                m.SetFloat("_Metallic", s.metal);
                m.enableInstancing = true;
                if (s.emission.maxColorComponent > 0f)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", s.emission);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                else
                {
                    m.DisableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", Color.black);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }
                if (s.alpha < 1f) MakeTransparent(m);
                else MakeOpaque(m);
                materials[s.name] = m;
                EditorUtility.SetDirty(m);
            }
            BuildCheckerMaterial(lit);
            AssetDatabase.SaveAssets();
        }

        // The diner's black and white floor: a 2x2 pixel checker, one cell per half metre (the kit's
        // meshes have UVs in metres)
        private static void BuildCheckerMaterial(Shader lit)
        {
            string texPath = $"{MaterialDir}/Checker.png";
            if (!File.Exists(texPath))
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Color a = new Color(0.93f, 0.91f, 0.86f), b = new Color(0.20f, 0.21f, 0.24f);
                tex.SetPixels(new[] { a, b, b, a });
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            Texture2D checker = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            string path = $"{MaterialDir}/Checker.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(lit);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = lit;
            m.SetTexture("_BaseMap", checker);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.35f);
            m.SetFloat("_Metallic", 0f);
            m.enableInstancing = true;
            MakeOpaque(m);
            materials["Checker"] = m;
            EditorUtility.SetDirty(m);
        }

        private static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        private static void MakeOpaque(Material m)
        {
            m.SetFloat("_Surface", 0f);
            m.SetOverrideTag("RenderType", "Opaque");
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = -1;
            m.SetShaderPassEnabled("ShadowCaster", true);
        }

        public static void EnsureFolders()
        {
            foreach (string dir in new[] { Root, MaterialDir, MeshDir, ModuleDir, PropDir, BlockDir, BuildingDir })
            {
                if (AssetDatabase.IsValidFolder(dir)) continue;
                string parent = Path.GetDirectoryName(dir).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
            }
        }

        // ---------------------------------------------------------------- meshes

        // Saves a mesh as an asset, updating an existing one in place so references to it survive
        public static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{MeshDir}/{name}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // ---------------------------------------------------------------- prefabs

        public static GameObject Load(string path)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) throw new Exception("Test Site 07: missing prefab " + path);
            return go;
        }

        public static GameObject Prop(string name) => Load($"{PropDir}/{name}.prefab");
        public static GameObject Module(string name) => Load($"{ModuleDir}/{name}.prefab");
        public static GameObject Building(string name) => Load($"{BuildingDir}/{name}.prefab");

        // A unit cube of one material, to be scaled: with a collider (Block) or without (Slab)
        public static GameObject Block(string material, bool collide = true, bool rebuild = false)
        {
            string name = (collide ? "Block " : "Slab ") + material;
            string path = $"{BlockDir}/{name}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !rebuild) return existing;
            var p = new Proto(name) { structure = true };
            p.Box(material, Vector3.zero, Vector3.one, collide);
            return p.Save(BlockDir);
        }

        // Blocks are made on demand; rebuilding the kit saves the existing ones again (in place, so
        // the scene's references to them hold)
        public static void RebuildBlocks()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { BlockDir }))
            {
                string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                bool collide = name.StartsWith("Block ");
                Block(name.Substring(collide ? 6 : 5), collide, true);
            }
        }

        public static void SetStatic(GameObject go, bool structure)
        {
            StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI |
                                      StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
            if (structure) flags |= StaticEditorFlags.OccluderStatic;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
        }
    }

    // Unit-sized convex shapes as triangle lists (every three points one triangle, facing outward)
    public static class Shapes
    {
        public static readonly List<Vector3> Box = MakeBox();
        public static readonly List<Vector3> Prism = MakePrism();
        public static readonly List<Vector3> Wedge = MakeWedge();
        private static readonly Dictionary<int, List<Vector3>> cylinders = new Dictionary<int, List<Vector3>>();
        private static readonly Dictionary<int, List<Vector3>> cones = new Dictionary<int, List<Vector3>>();
        private static readonly Dictionary<int, List<Vector3>> rocks = new Dictionary<int, List<Vector3>>();

        // -0.5..0.5 on every axis
        private static List<Vector3> MakeBox()
        {
            var t = new List<Vector3>();
            Vector3[] c = new Vector3[8];
            for (int i = 0; i < 8; i++) c[i] = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
            Quad(t, c[0], c[1], c[3], c[2]); // z-
            Quad(t, c[4], c[5], c[7], c[6]); // z+
            Quad(t, c[0], c[2], c[6], c[4]); // x-
            Quad(t, c[1], c[3], c[7], c[5]); // x+
            Quad(t, c[0], c[1], c[5], c[4]); // y-
            Quad(t, c[2], c[3], c[7], c[6]); // y+
            return Orient(t, Vector3.zero);
        }

        // A gable: width x -0.5..0.5 at y = 0, ridge at y = 1 along z, z -0.5..0.5
        private static List<Vector3> MakePrism()
        {
            var t = new List<Vector3>();
            Vector3 a = new Vector3(-0.5f, 0, -0.5f), b = new Vector3(0.5f, 0, -0.5f), c = new Vector3(0.5f, 0, 0.5f), d = new Vector3(-0.5f, 0, 0.5f);
            Vector3 r0 = new Vector3(0, 1, -0.5f), r1 = new Vector3(0, 1, 0.5f);
            Quad(t, a, b, c, d);
            Quad(t, a, d, r1, r0);
            Quad(t, b, c, r1, r0);
            t.Add(a); t.Add(b); t.Add(r0);
            t.Add(d); t.Add(c); t.Add(r1);
            return Orient(t, new Vector3(0, 0.33f, 0));
        }

        // A ramp: x -0.5..0.5, rising from y = 0 at z = -0.5 to y = 1 at z = 0.5
        private static List<Vector3> MakeWedge()
        {
            var t = new List<Vector3>();
            Vector3 a = new Vector3(-0.5f, 0, -0.5f), b = new Vector3(0.5f, 0, -0.5f), c = new Vector3(0.5f, 0, 0.5f), d = new Vector3(-0.5f, 0, 0.5f);
            Vector3 e = new Vector3(0.5f, 1, 0.5f), f = new Vector3(-0.5f, 1, 0.5f);
            Quad(t, a, b, c, d);
            Quad(t, d, c, e, f);
            Quad(t, a, b, e, f);
            t.Add(a); t.Add(d); t.Add(f);
            t.Add(b); t.Add(c); t.Add(e);
            return Orient(t, new Vector3(0, 0.33f, 0.17f));
        }

        // Radius 0.5, y 0..1
        public static List<Vector3> Cylinder(int sides)
        {
            if (cylinders.TryGetValue(sides, out var cached)) return cached;
            var t = new List<Vector3>();
            for (int i = 0; i < sides; i++)
            {
                Vector3 p0 = Ring(i, sides), p1 = Ring(i + 1, sides);
                Quad(t, p0, p1, p1 + Vector3.up, p0 + Vector3.up);
                t.Add(Vector3.zero); t.Add(p0); t.Add(p1);
                t.Add(Vector3.up); t.Add(p0 + Vector3.up); t.Add(p1 + Vector3.up);
            }
            return cylinders[sides] = Orient(t, new Vector3(0, 0.5f, 0));
        }

        // Base radius 0.5 at y = 0, apex at y = 1
        public static List<Vector3> Cone(int sides)
        {
            if (cones.TryGetValue(sides, out var cached)) return cached;
            var t = new List<Vector3>();
            for (int i = 0; i < sides; i++)
            {
                Vector3 p0 = Ring(i, sides), p1 = Ring(i + 1, sides);
                t.Add(p0); t.Add(p1); t.Add(Vector3.up);
                t.Add(Vector3.zero); t.Add(p0); t.Add(p1);
            }
            return cones[sides] = Orient(t, new Vector3(0, 0.25f, 0));
        }

        // A lumpy icosahedron about radius 0.5 (foliage, rocks, sandbags); seed picks the lumps
        public static List<Vector3> Rock(int seed)
        {
            if (rocks.TryGetValue(seed, out var cached)) return cached;
            float g = (1f + Mathf.Sqrt(5f)) / 2f;
            Vector3[] v =
            {
                new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
                new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
                new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1),
            };
            var random = new System.Random(seed * 7919 + 17);
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized * 0.5f * (0.86f + 0.22f * (float)random.NextDouble());
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var t = new List<Vector3>();
            foreach (int i in f) t.Add(v[i]);
            return rocks[seed] = Orient(t, Vector3.zero);
        }

        private static Vector3 Ring(int i, int sides)
        {
            float a = (i % sides) * Mathf.PI * 2f / sides + Mathf.PI / sides;
            return new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f);
        }

        private static void Quad(List<Vector3> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            t.Add(a); t.Add(b); t.Add(c);
            t.Add(a); t.Add(c); t.Add(d);
        }

        // Turns every triangle to face away from a point inside the (convex) shape
        private static List<Vector3> Orient(List<Vector3> t, Vector3 inside)
        {
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 n = Vector3.Cross(t[i + 1] - t[i], t[i + 2] - t[i]);
                Vector3 centre = (t[i] + t[i + 1] + t[i + 2]) / 3f;
                if (Vector3.Dot(n, centre - inside) < 0f)
                {
                    Vector3 swap = t[i + 1];
                    t[i + 1] = t[i + 2];
                    t[i + 2] = swap;
                }
            }
            return t;
        }
    }

    // Collects flat-shaded triangles into sub-meshes. UVs are box-projected in metres.
    public class MeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<List<int>> submeshes = new List<List<int>>();

        public bool Empty => vertices.Count == 0;

        public void Add(List<Vector3> shape, Matrix4x4 m, int submesh)
        {
            while (submeshes.Count <= submesh) submeshes.Add(new List<int>());
            bool flip = m.determinant < 0f;
            for (int i = 0; i < shape.Count; i += 3)
            {
                Vector3 a = m.MultiplyPoint3x4(shape[i]);
                Vector3 b = m.MultiplyPoint3x4(shape[i + 1]);
                Vector3 c = m.MultiplyPoint3x4(shape[i + 2]);
                if (flip) { Vector3 s = b; b = c; c = s; }
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) continue;
                n.Normalize();
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                for (int k = 0; k < 3; k++) normals.Add(n);
                uvs.Add(Project(a, n)); uvs.Add(Project(b, n)); uvs.Add(Project(c, n));
                submeshes[submesh].Add(start);
                submeshes[submesh].Add(start + 1);
                submeshes[submesh].Add(start + 2);
            }
        }

        private static Vector2 Project(Vector3 p, Vector3 n)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (ay >= ax && ay >= az) return new Vector2(p.x, p.z);
            if (ax >= az) return new Vector2(p.z, p.y);
            return new Vector2(p.x, p.y);
        }

        public Mesh Build(string name, bool lightmapUVs)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateBounds();
            if (lightmapUVs)
            {
                var settings = new UnwrapParam();
                UnwrapParam.SetDefaults(out settings);
                settings.packMargin = 0.02f;
                Unwrapping.GenerateSecondaryUVSet(mesh, settings);
            }
            return mesh;
        }
    }

    // A prefab under construction: visual parts (merged on save), colliders, nested prefabs and extras
    public class Proto
    {
        public readonly string name;
        // Structures (buildings, walls, ground) occlude and get lightmaps; props use light probes
        public bool structure;
        // Lightmap UVs for the merged mesh (off for tiny or glowing things)
        public bool lightmap = true;

        private readonly List<(List<Vector3> shape, Matrix4x4 m, string mat)> parts = new List<(List<Vector3>, Matrix4x4, string)>();
        private readonly List<(List<Vector3> shape, Matrix4x4 m, string mat)> glassParts = new List<(List<Vector3>, Matrix4x4, string)>();
        private readonly List<(Vector3 c, Vector3 s, Quaternion r, bool glass)> colliders = new List<(Vector3, Vector3, Quaternion, bool)>();
        private readonly List<(GameObject prefab, Vector3 pos, float yaw, Vector3 scale, string name)> nested = new List<(GameObject, Vector3, float, Vector3, string)>();
        private readonly List<Action<Transform>> extras = new List<Action<Transform>>();

        public Proto(string name) { this.name = name; }

        public Proto Shape(List<Vector3> shape, string mat, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            parts.Add((shape, Matrix4x4.TRS(position, rotation, scale), mat));
            return this;
        }

        public Proto Shape(List<Vector3> shape, string mat, Vector3 position, Vector3 scale, float yaw = 0f)
            => Shape(shape, mat, position, scale, Quaternion.Euler(0f, yaw, 0f));

        // An axis-aligned box by centre and size, with a matching collider unless told otherwise
        public Proto Box(string mat, Vector3 centre, Vector3 size, bool collide = true)
        {
            parts.Add((Shapes.Box, Matrix4x4.TRS(centre, Quaternion.identity, size), mat));
            if (collide) colliders.Add((centre, size, Quaternion.identity, false));
            return this;
        }

        public Proto BoxMM(string mat, Vector3 min, Vector3 max, bool collide = true)
            => Box(mat, (min + max) * 0.5f, max - min, collide);

        public Proto BoxRot(string mat, Vector3 centre, Vector3 size, Quaternion rotation, bool collide = true)
        {
            parts.Add((Shapes.Box, Matrix4x4.TRS(centre, rotation, size), mat));
            if (collide) colliders.Add((centre, size, rotation, false));
            return this;
        }

        // A cylinder standing on basePos (visual only)
        public Proto Cyl(string mat, Vector3 basePos, float radius, float height, int sides = 10)
            => Shape(Shapes.Cylinder(sides), mat, basePos, new Vector3(radius * 2f, height, radius * 2f));

        // A cylinder lying along local x (wheels, pipes): centre, radius, length
        public Proto CylX(string mat, Vector3 centre, float radius, float length, int sides = 10)
            => Shape(Shapes.Cylinder(sides), mat, centre - new Vector3(length * 0.5f, 0f, 0f), new Vector3(radius * 2f, length, radius * 2f), Quaternion.Euler(0f, 0f, -90f));

        // A cylinder lying along local z
        public Proto CylZ(string mat, Vector3 centre, float radius, float length, int sides = 10)
            => Shape(Shapes.Cylinder(sides), mat, centre - new Vector3(0f, 0f, length * 0.5f), new Vector3(radius * 2f, length, radius * 2f), Quaternion.Euler(90f, 0f, 0f));

        public Proto Col(Vector3 centre, Vector3 size) { colliders.Add((centre, size, Quaternion.identity, false)); return this; }
        public Proto ColMM(Vector3 min, Vector3 max) => Col((min + max) * 0.5f, max - min);
        public Proto ColRot(Vector3 centre, Vector3 size, Quaternion rotation) { colliders.Add((centre, size, rotation, false)); return this; }

        // A see-through pane: drawn with the glass material (no shadow), blocking players on the Glass layer
        public Proto Glass(Vector3 centre, Vector3 visualSize, Vector3 colliderSize, string mat = "Glass")
        {
            glassParts.Add((Shapes.Box, Matrix4x4.TRS(centre, Quaternion.identity, visualSize), mat));
            colliders.Add((centre, colliderSize, Quaternion.identity, true));
            return this;
        }

        public Proto GlassCol(Vector3 centre, Vector3 size) { colliders.Add((centre, size, Quaternion.identity, true)); return this; }

        public Proto Nest(GameObject prefab, Vector3 position, float yaw = 0f, Vector3? scale = null, string childName = null)
        {
            nested.Add((prefab, position, yaw, scale ?? Vector3.one, childName));
            return this;
        }

        public Proto Extra(Action<Transform> build) { extras.Add(build); return this; }

        // A flat text label (TextMesh Pro), facing local -z by default (yaw 180 faces +z)
        public Proto Text(string text, Vector3 position, float yaw, float height, Color color, float width = 0f)
        {
            return Extra(root =>
            {
                var go = new GameObject("Text " + text.Replace("\n", " "));
                go.transform.SetParent(root, false);
                go.transform.localPosition = position;
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                TextMeshPro tmp = go.AddComponent<TextMeshPro>();
                tmp.text = text;
                tmp.fontSize = height * 12f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = color;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.rectTransform.sizeDelta = new Vector2(width > 0f ? width : text.Length * height, height * 1.6f);
                tmp.characterSpacing = 4f;
                MeshRenderer r = go.GetComponent<MeshRenderer>();
                if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;
            });
        }

        public GameObject Save(string folder)
        {
            var root = new GameObject(name);
            int ground = Kit.Ground, glassLayer = Kit.GlassLayer;
            try
            {
                root.layer = ground;
                if (parts.Count > 0) AddRenderer(root, parts, name, true);
                if (glassParts.Count > 0)
                {
                    var glass = new GameObject("Glass");
                    glass.transform.SetParent(root.transform, false);
                    glass.layer = glassLayer;
                    AddRenderer(glass, glassParts, name + " Glass", false);
                    glass.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }

                // Axis-aligned colliders share one child per layer; rotated ones get a child each
                GameObject solid = null, pane = null;
                int rotated = 0;
                foreach (var c in colliders)
                {
                    if (c.r == Quaternion.identity)
                    {
                        GameObject holder = c.glass ? (pane ??= Child(root, "Glass Collision", glassLayer)) : (solid ??= Child(root, "Collision", ground));
                        BoxCollider box = holder.AddComponent<BoxCollider>();
                        box.center = c.c;
                        box.size = c.s;
                    }
                    else
                    {
                        GameObject holder = Child(root, "Collision " + (++rotated), c.glass ? glassLayer : ground);
                        holder.transform.localPosition = c.c;
                        holder.transform.localRotation = c.r;
                        holder.AddComponent<BoxCollider>().size = c.s;
                    }
                }

                foreach (var n in nested)
                {
                    var child = (GameObject)PrefabUtility.InstantiatePrefab(n.prefab, root.transform);
                    child.transform.localPosition = n.pos;
                    child.transform.localRotation = Quaternion.Euler(0f, n.yaw, 0f);
                    child.transform.localScale = n.scale;
                    if (n.name != null) child.name = n.name;
                }

                foreach (Action<Transform> extra in extras) extra(root.transform);

                // Static flags on our own objects (nested prefabs carry their own)
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject)) continue;
                    if (t.GetComponent<TextMeshPro>() != null) continue;
                    Kit.SetStatic(t.gameObject, structure);
                }

                string path = $"{folder}/{name}.prefab";
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject Child(GameObject root, string childName, int layer)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(root.transform, false);
            go.layer = layer;
            return go;
        }

        private void AddRenderer(GameObject go, List<(List<Vector3> shape, Matrix4x4 m, string mat)> list, string meshName, bool opaque)
        {
            var order = new List<string>();
            foreach (var p in list) if (!order.Contains(p.mat)) order.Add(p.mat);
            var builder = new MeshBuilder();
            foreach (var p in list) builder.Add(p.shape, p.m, order.IndexOf(p.mat));
            Mesh mesh = Kit.SaveMesh(builder.Build(meshName, opaque && lightmap), meshName);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            var mats = new Material[order.Count];
            for (int i = 0; i < mats.Length; i++) mats[i] = Kit.Mat(order[i]);
            renderer.sharedMaterials = mats;
            renderer.receiveGI = structure && opaque && lightmap ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
        }
    }
}
