using System.Collections.Generic;
using UnityEngine;

// The meshes and materials the gun (GunModel) is built from. Every mesh is a unit shape, made once
// and shared by every gun (each part is scaled to its size), so the gun costs no extra memory per
// player and draws with five materials. Built in code like the hats and drone bodies: no model
// files to import.
public static class GunParts
{
    private static readonly Dictionary<GunDesign.Shape, Mesh> meshes = new Dictionary<GunDesign.Shape, Mesh>();
    private static readonly Dictionary<GunDesign.Finish, Material> materials = new Dictionary<GunDesign.Finish, Material>();

    #region Materials

    // Base colours, for the materials and for the see-through spawn protection copy
    public static Color ColorOf(GunDesign.Finish finish)
    {
        switch (finish)
        {
            case GunDesign.Finish.Graphite: return new Color(0.045f, 0.047f, 0.052f);
            case GunDesign.Finish.Gunmetal: return new Color(0.075f, 0.08f, 0.09f);
            case GunDesign.Finish.Steel: return new Color(0.42f, 0.44f, 0.47f);
            case GunDesign.Finish.Rubber: return new Color(0.03f, 0.03f, 0.032f);
            default: return Color.white;
        }
    }

    public static Material MaterialOf(GunDesign.Finish finish)
    {
        if (materials.TryGetValue(finish, out Material material) && material != null) return material;
        switch (finish)
        {
            // Glow: the drones' glowing material (an asset, so builds keep its glowing shader variant),
            // tinted per gun with a property block
            case GunDesign.Finish.Glow: material = DroneBody.GlowMaterial; break;
            case GunDesign.Finish.Graphite: material = Lit("Gun Graphite", ColorOf(finish), 0.28f, 0.35f); break;
            case GunDesign.Finish.Gunmetal: material = Lit("Gun Gunmetal", ColorOf(finish), 0.5f, 0.92f); break;
            case GunDesign.Finish.Steel: material = Lit("Gun Steel", ColorOf(finish), 0.62f, 1f); break;
            default: material = Lit("Gun Rubber", ColorOf(finish), 0.12f, 0f); break;
        }
        materials[finish] = material;
        return material;
    }

    private static Material Lit(string name, Color color, float smoothness, float metallic)
    {
        // The same shader the drones' and caps' materials made in code use, which every build includes
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Material material = lit != null ? new Material(lit) : new Material(DroneBody.ShellMaterial);
        material.name = name;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        material.enableInstancing = true;
        return material;
    }

    #endregion

    #region Meshes

    public static Mesh MeshOf(GunDesign.Shape shape)
    {
        if (meshes.TryGetValue(shape, out Mesh mesh) && mesh != null) return mesh;
        switch (shape)
        {
            case GunDesign.Shape.Chamfer: mesh = Prism("Gun Chamfer", Octagon(0.16f)); break;
            case GunDesign.Shape.Cylinder: mesh = Prism("Gun Cylinder", Circle(16), smooth: true); break;
            case GunDesign.Shape.Ring: mesh = Ring("Gun Ring", 20, 8, 0.12f); break;
            default: mesh = Prism("Gun Box", new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f) }); break;
        }
        meshes[shape] = mesh;
        return mesh;
    }

    // A unit square with its corners cut off by 'cut' (a share of the side), counter-clockwise
    private static Vector2[] Octagon(float cut)
    {
        float a = 0.5f, b = 0.5f - cut;
        return new[]
        {
            new Vector2(-b, -a), new Vector2(b, -a), new Vector2(a, -b), new Vector2(a, b),
            new Vector2(b, a), new Vector2(-b, a), new Vector2(-a, b), new Vector2(-a, -b),
        };
    }

    // A circle of unit diameter, counter-clockwise
    private static Vector2[] Circle(int sides)
    {
        var points = new Vector2[sides];
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.5f;
        }
        return points;
    }

    // An outline (convex, counter-clockwise seen from the front, +z) extruded from z -0.5 to 0.5,
    // with both ends capped. Flat sides, or smooth ones for a cylinder.
    private static Mesh Prism(string name, Vector2[] outline, bool smooth = false)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();
        int n = outline.Length;

        // Sides
        for (int i = 0; i < n; i++)
        {
            Vector2 p0 = outline[i], p1 = outline[(i + 1) % n];
            Vector3 n0, n1;
            if (smooth)
            {
                n0 = new Vector3(p0.x, p0.y, 0f).normalized;
                n1 = new Vector3(p1.x, p1.y, 0f).normalized;
            }
            else
            {
                Vector2 edge = p1 - p0;
                n0 = n1 = new Vector3(edge.y, -edge.x, 0f).normalized;
            }
            int v = vertices.Count;
            vertices.Add(new Vector3(p0.x, p0.y, -0.5f)); normals.Add(n0);
            vertices.Add(new Vector3(p1.x, p1.y, -0.5f)); normals.Add(n1);
            vertices.Add(new Vector3(p1.x, p1.y, 0.5f)); normals.Add(n1);
            vertices.Add(new Vector3(p0.x, p0.y, 0.5f)); normals.Add(n0);
            // Facing outwards (Unity winds clockwise as seen from the front of a face)
            triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
            triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 3);
        }

        // Caps: a fan from the middle, at the back (facing -z) and the front (facing +z)
        for (int end = 0; end < 2; end++)
        {
            float z = end == 0 ? -0.5f : 0.5f;
            Vector3 normal = new Vector3(0f, 0f, end == 0 ? -1f : 1f);
            int centre = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, z)); normals.Add(normal);
            for (int i = 0; i < n; i++)
            {
                vertices.Add(new Vector3(outline[i].x, outline[i].y, z));
                normals.Add(normal);
            }
            for (int i = 0; i < n; i++)
            {
                int a = centre + 1 + i, b = centre + 1 + (i + 1) % n;
                if (end == 0) { triangles.Add(centre); triangles.Add(b); triangles.Add(a); }
                else { triangles.Add(centre); triangles.Add(a); triangles.Add(b); }
            }
        }

        return Finish(name, vertices, normals, triangles);
    }

    // A torus round z of unit outside diameter: 'tube' is the tube's radius (a share of the
    // diameter). Its thickness along z is 2 * tube, so a part's size z of 2 * tube keeps it round.
    private static Mesh Ring(string name, int segments, int sides, float tube)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();
        float radius = 0.5f - tube;
        // Scaled along z so the mesh is a unit deep: a part's size z is the ring's thickness
        float depth = 0.5f / tube;
        for (int s = 0; s <= segments; s++)
        {
            float a = s * Mathf.PI * 2f / segments;
            Vector3 round = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            for (int t = 0; t <= sides; t++)
            {
                float b = t * Mathf.PI * 2f / sides;
                Vector3 normal = round * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                Vector3 p = round * (radius + tube * Mathf.Cos(b)) + Vector3.forward * (tube * Mathf.Sin(b) * depth);
                vertices.Add(p);
                normals.Add(normal.normalized);
            }
        }
        int row = sides + 1;
        for (int s = 0; s < segments; s++)
        {
            for (int t = 0; t < sides; t++)
            {
                int a = s * row + t, b = (s + 1) * row + t;
                triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                triangles.Add(b); triangles.Add(b + 1); triangles.Add(a + 1);
            }
        }
        return Finish(name, vertices, normals, triangles);
    }

    private static Mesh Finish(string name, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
    {
        Mesh mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        return mesh;
    }

    #endregion
}
