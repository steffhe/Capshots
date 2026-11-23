using System.Collections.Generic;
using UnityEngine;

// One force field panel on the inside of a boundary wall: a vertical rectangle from start to end
// (at ground level, y ignored), from a ground height up to top.
public struct BoundaryPanel
{
    public Vector3 start, end;
    public float top;
    // Horizontal, pointing from the wall into the play area
    public Vector3 inward;
    // True for a wall that runs along x (north or south)
    public bool alongX;

    public float Length => Vector3.Distance(start, end);

    // The nearest point on the panel (from bottom to top) to p
    public Vector3 ClosestPoint(Vector3 p, float bottom)
    {
        Vector3 along = end - start;
        float t = along.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(p - start, along) / along.sqrMagnitude) : 0f;
        Vector3 q = start + along * t;
        q.y = Mathf.Clamp(p.y, bottom, Mathf.Max(bottom, top));
        return q;
    }
}

// Works out where the force field panels go for a map's boundary walls (see BoundaryField). Kept
// apart from the component, with no scene access, so the EditMode tests can check it.
public static class BoundaryLayout
{
    // Panels sit this far in front of the wall's face, so they don't z-fight anything flush with it
    public const float FaceOffset = 0.02f;

    // Tall, and much thinner in one horizontal direction than the other (not the floor or a crate)
    public static bool IsWall(Bounds b)
    {
        float thin = Mathf.Min(b.size.x, b.size.z), wide = Mathf.Max(b.size.x, b.size.z);
        return b.size.y > 2f && thin <= 2f && wide > thin * 4f;
    }

    // One panel per wall, on the face toward the middle of the walls, trimmed at the inner faces of
    // the neighbouring walls so it doesn't poke out past a corner where the colliders overlap.
    // Bounds that aren't walls are skipped.
    public static List<BoundaryPanel> Build(IEnumerable<Bounds> bounds)
    {
        var walls = new List<Bounds>();
        Vector3 middle = Vector3.zero;
        foreach (Bounds b in bounds)
        {
            if (!IsWall(b)) continue;
            walls.Add(b);
            middle += b.center;
        }
        var panels = new List<BoundaryPanel>();
        if (walls.Count == 0) return panels;
        middle /= walls.Count;

        // The play area between the walls' inner faces
        Vector2 minXZ = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        Vector2 maxXZ = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        foreach (Bounds b in walls)
        {
            if (b.size.x > b.size.z)
            {
                if (middle.z > b.center.z) minXZ.y = Mathf.Max(minXZ.y, b.max.z);
                else maxXZ.y = Mathf.Min(maxXZ.y, b.min.z);
            }
            else
            {
                if (middle.x > b.center.x) minXZ.x = Mathf.Max(minXZ.x, b.max.x);
                else maxXZ.x = Mathf.Min(maxXZ.x, b.min.x);
            }
        }

        foreach (Bounds b in walls)
        {
            var panel = new BoundaryPanel { top = b.max.y, alongX = b.size.x > b.size.z };
            if (panel.alongX)
            {
                bool inside = middle.z > b.center.z;
                float z = inside ? b.max.z + FaceOffset : b.min.z - FaceOffset;
                panel.start = new Vector3(Mathf.Max(b.min.x, minXZ.x), 0f, z);
                panel.end = new Vector3(Mathf.Min(b.max.x, maxXZ.x), 0f, z);
                panel.inward = inside ? Vector3.forward : Vector3.back;
            }
            else
            {
                bool inside = middle.x > b.center.x;
                float x = inside ? b.max.x + FaceOffset : b.min.x - FaceOffset;
                panel.start = new Vector3(x, 0f, Mathf.Max(b.min.z, minXZ.y));
                panel.end = new Vector3(x, 0f, Mathf.Min(b.max.z, maxXZ.y));
                panel.inward = inside ? Vector3.right : Vector3.left;
            }
            if (panel.Length >= 0.1f) panels.Add(panel);
        }
        return panels;
    }
}
