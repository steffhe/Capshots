using System.Collections.Generic;
using UnityEngine;

// Flat shapes drawn onto a curved surface, like print, paint or embroidery: the drone's face on its
// face screen, and details on hats (spots, badges, emblems, stars). A shape is an outline across the
// surface (anticlockwise, round its middle), filled with rings from its middle out to its edge so its
// mesh can bend with the surface. A map puts each point of it on the surface, and the mesh sits a hair
// out from there, so nothing stands off the surface the way a squashed sphere or box would.
public static class SurfacePrint
{
    // Puts a point given across the surface (uv) on it, and gives the way out of the surface there
    public delegate Vector3 Map(Vector2 uv, out Vector3 normal);

    #region Outlines

    public static List<Vector2> Ellipse(float w, float h)
    {
        const int points = 40;
        var outline = new List<Vector2>(points);
        for (int k = 0; k < points; k++)
        {
            float a = 2f * Mathf.PI * k / points;
            outline.Add(new Vector2(Mathf.Cos(a) * w * 0.5f, Mathf.Sin(a) * h * 0.5f));
        }
        return outline;
    }

    // Up and down, w thick and length long, with round ends; one no longer than it's thick is a circle
    public static List<Vector2> Bar(float w, float length)
    {
        const int pointsPerEnd = 14;
        float radius = w * 0.5f, half = Mathf.Max(0f, length * 0.5f - radius);
        var outline = new List<Vector2>(pointsPerEnd * 2 + 2);
        // The top end, right to left, then the bottom end, left to right
        for (int k = 0; k <= pointsPerEnd; k++)
        {
            float a = Mathf.PI * k / pointsPerEnd;
            outline.Add(new Vector2(Mathf.Cos(a) * radius, half + Mathf.Sin(a) * radius));
        }
        for (int k = 0; k <= pointsPerEnd; k++)
        {
            float a = Mathf.PI + Mathf.PI * k / pointsPerEnd;
            outline.Add(new Vector2(Mathf.Cos(a) * radius, -half + Mathf.Sin(a) * radius));
        }
        return outline;
    }

    public static List<Vector2> Box(float w, float h)
    {
        float x = w * 0.5f, y = h * 0.5f;
        return new List<Vector2> { new Vector2(-x, -y), new Vector2(x, -y), new Vector2(x, y), new Vector2(-x, y) };
    }

    // A star of so many points, out to outer and in to inner between them, a point straight up
    public static List<Vector2> Star(int points, float outer, float inner)
    {
        var outline = new List<Vector2>(points * 2);
        for (int k = 0; k < points * 2; k++)
        {
            float a = Mathf.PI * 0.5f + Mathf.PI * k / points;
            float reach = k % 2 == 0 ? outer : inner;
            outline.Add(new Vector2(Mathf.Cos(a) * reach, Mathf.Sin(a) * reach));
        }
        return outline;
    }

    // Turned by roll degrees (x towards y) and moved to x, y
    public static List<Vector2> Turned(List<Vector2> outline, float x, float y, float roll = 0f)
    {
        float a = roll * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
        for (int k = 0; k < outline.Count; k++)
        {
            Vector2 p = outline[k];
            outline[k] = new Vector2(x + p.x * cos - p.y * sin, y + p.x * sin + p.y * cos);
        }
        return outline;
    }

    #endregion

    // Fills an outline (anticlockwise, and every edge seen from its middle, like any convex shape or a
    // star) with points no further apart than step: the outline cut into short edges, and rings of them
    // in towards the middle. Triangles go anticlockwise across, like the outline.
    public static void Fill(List<Vector2> outline, float step, out Vector2[] points, out int[] triangles)
    {
        var edge = new List<Vector2>();
        for (int k = 0; k < outline.Count; k++)
        {
            Vector2 a = outline[k], b = outline[(k + 1) % outline.Count];
            int pieces = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / step));
            for (int p = 0; p < pieces; p++) edge.Add(Vector2.Lerp(a, b, (float)p / pieces));
        }
        Vector2 middle = Vector2.zero;
        foreach (Vector2 p in outline) middle += p;
        middle /= outline.Count;
        float reach = 0f;
        foreach (Vector2 p in edge) reach = Mathf.Max(reach, Vector2.Distance(p, middle));
        int rings = Mathf.Clamp(Mathf.CeilToInt(reach / step), 1, 12);

        int n = edge.Count;
        points = new Vector2[1 + rings * n];
        points[0] = middle;
        for (int ring = 1; ring <= rings; ring++)
        {
            float t = (float)ring / rings;
            for (int k = 0; k < n; k++) points[1 + (ring - 1) * n + k] = Vector2.Lerp(middle, edge[k], t);
        }
        var list = new List<int>(rings * n * 6);
        for (int k = 0; k < n; k++)
        {
            list.Add(0); list.Add(1 + k); list.Add(1 + (k + 1) % n);
        }
        for (int ring = 1; ring < rings; ring++)
        {
            int inside = 1 + (ring - 1) * n, outside = inside + n;
            for (int k = 0; k < n; k++)
            {
                int next = (k + 1) % n;
                list.Add(outside + k); list.Add(outside + next); list.Add(inside + next);
                list.Add(outside + k); list.Add(inside + next); list.Add(inside + k);
            }
        }
        triangles = list.ToArray();
    }

    // A mesh of an outline on a surface, lift out from it, with its vertices measured from where its
    // middle lands (returned as at, for the part's position, so it scales round its middle).
    // uvScale: how many times a surface's texture repeats per metre across it.
    public static Mesh Build(string name, List<Vector2> outline, Map map, float lift, float step, float uvScale, out Vector3 at)
    {
        Fill(outline, step, out Vector2[] points, out int[] triangles);
        var vertices = new Vector3[points.Length];
        var normals = new Vector3[points.Length];
        var uvs = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 normal;
            Vector3 p = map(points[i], out normal);
            vertices[i] = p + normal * lift;
            normals[i] = normal;
            uvs[i] = points[i] * uvScale;
        }
        at = vertices[0];
        for (int i = 0; i < vertices.Length; i++) vertices[i] -= at;

        // Unity draws the side a triangle's corners go clockwise round. Which way round the outline
        // goes as seen from outside depends on the map, so turn the triangles round if they'd face in.
        float facing = 0f;
        for (int t = 0; t < triangles.Length; t += 3)
        {
            Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
            facing += Vector3.Dot(Vector3.Cross(b - a, c - a), normals[triangles[t]]);
        }
        if (facing < 0f)
        {
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int swap = triangles[t + 1];
                triangles[t + 1] = triangles[t + 2];
                triangles[t + 2] = swap;
            }
        }

        Mesh mesh = new Mesh { name = name };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    #region Maps

    // An ellipsoid round centre with these radii, from a point on it (at), with across and up lying
    // along it there: a point across is found on the flat touching it at at, then taken in (or out) to it
    public static Map Ellipsoid(Vector3 centre, Vector3 radii, Vector3 at, Vector3 across, Vector3 up)
    {
        return (Vector2 uv, out Vector3 normal) =>
        {
            Vector3 q = at + across * uv.x + up * uv.y - centre;
            Vector3 unit = new Vector3(q.x / radii.x, q.y / radii.y, q.z / radii.z).normalized;
            Vector3 p = Vector3.Scale(unit, radii);
            normal = new Vector3(p.x / (radii.x * radii.x), p.y / (radii.y * radii.y), p.z / (radii.z * radii.z)).normalized;
            return centre + p;
        };
    }

    // Across and up along a surface at a point with this normal: up as near world up as it can be
    // (or towards the front at the very top)
    public static void Frame(Vector3 normal, out Vector3 across, out Vector3 up)
    {
        Vector3 side = Vector3.Cross(Vector3.up, normal);
        if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(Vector3.forward, normal);
        across = side.normalized;
        up = Vector3.Cross(normal, across).normalized;
    }

    #endregion
}
