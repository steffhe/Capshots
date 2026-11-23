using UnityEngine;

// Makes a map's boundary walls visible as force fields. For each wall (a box collider thin in one
// horizontal direction), it adds a glowing panel on the side facing the middle of the map, from
// the ground up to the top of the collider. The colliders themselves are left as they are.
//  - Downtown: on "Bounds", the parent of its invisible wall colliders (the list is left empty).
//  - Test: on "Design", with its four see-through outer wall cubes listed; their own renderers
//    are switched off so only the field shows.
public class BoundaryField : MonoBehaviour
{
    private static readonly int PlayerPosId = Shader.PropertyToID("_CapshotsPlayerPos");

    [Tooltip("A material using the Capshots/Force Field shader")]
    public Material material;
    [Tooltip("The walls to cover. Leave empty to use every wall-shaped box collider under this object.")]
    public BoxCollider[] walls;
    [Tooltip("Find the ground along each wall with raycasts; falls back to Ground Height where nothing is hit")]
    public bool findGround = true;
    [Tooltip("World height of the ground along the walls; the field's glow starts here")]
    public float groundHeight = 0f;
    [Tooltip("Switch off the walls' own mesh renderers, so only the force field shows")]
    public bool hideWallRenderers = true;

    private void Start()
    {
        if (material == null) return;

        BoxCollider[] candidates = walls != null && walls.Length > 0 ? walls : GetComponentsInChildren<BoxCollider>();
        var found = new System.Collections.Generic.List<BoxCollider>();
        Vector3 middle = Vector3.zero;
        foreach (BoxCollider wall in candidates)
        {
            if (wall == null || !IsWall(wall.bounds)) continue;
            found.Add(wall);
            middle += wall.bounds.center;
        }
        if (found.Count == 0) return;
        middle /= found.Count;

        // The play area between the walls' inner faces, so panels stop at the corners instead of
        // poking out past the neighbouring wall where the colliders overlap
        Vector2 minXZ = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        Vector2 maxXZ = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        foreach (BoxCollider wall in found)
        {
            Bounds b = wall.bounds;
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

        foreach (BoxCollider wall in found)
        {
            AddPanel(wall.bounds, middle, minXZ, maxXZ);
            if (hideWallRenderers && wall.TryGetComponent(out MeshRenderer own)) own.enabled = false;
        }
    }

    // Tall, and much thinner in one horizontal direction than the other (not the floor)
    private static bool IsWall(Bounds b)
    {
        float thin = Mathf.Min(b.size.x, b.size.z), wide = Mathf.Max(b.size.x, b.size.z);
        return b.size.y > 2f && thin <= 2f && wide > thin * 4f;
    }

    private void AddPanel(Bounds b, Vector3 middle, Vector2 minXZ, Vector2 maxXZ)
    {
        bool alongX = b.size.x > b.size.z; // the wall runs along x (a north or south wall)
        float top = b.max.y;

        // The face toward the middle of the map, nudged in so it doesn't z-fight anything flush
        Vector3 a, c;
        if (alongX)
        {
            float z = middle.z > b.center.z ? b.max.z + 0.02f : b.min.z - 0.02f;
            a = new Vector3(Mathf.Max(b.min.x, minXZ.x), 0f, z);
            c = new Vector3(Mathf.Min(b.max.x, maxXZ.x), 0f, z);
        }
        else
        {
            float x = middle.x > b.center.x ? b.max.x + 0.02f : b.min.x - 0.02f;
            a = new Vector3(x, 0f, Mathf.Max(b.min.z, minXZ.y));
            c = new Vector3(x, 0f, Mathf.Min(b.max.z, maxXZ.y));
        }
        if (Vector3.Distance(a, c) < 0.1f) return;
        float bottom = findGround ? FindGround(a, c, middle, top) : groundHeight;
        if (top <= bottom) return;
        float height = top - bottom;

        Mesh mesh = new Mesh { name = "Force Field" };
        // The panel sits at this object's origin, so its corners go in as local positions
        mesh.vertices = new[]
        {
            transform.InverseTransformPoint(new Vector3(a.x, bottom, a.z)), transform.InverseTransformPoint(new Vector3(c.x, bottom, c.z)),
            transform.InverseTransformPoint(new Vector3(a.x, top, a.z)), transform.InverseTransformPoint(new Vector3(c.x, top, c.z)),
        };
        // x: metres along the wall (world-aligned so the grid lines up at the corners), y: metres up
        float u0 = alongX ? a.x : a.z, u1 = alongX ? c.x : c.z;
        mesh.uv = new[] { new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u0, height), new Vector2(u1, height) };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject panel = new GameObject("Force Field", typeof(MeshFilter), typeof(MeshRenderer));
        panel.transform.SetParent(transform, false);
        panel.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = panel.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    // The lowest ground hit just inside the wall, sampled along its length (the lowest, so a
    // building or crate against the wall doesn't lift the whole field)
    private float FindGround(Vector3 a, Vector3 c, Vector3 middle, float top)
    {
        Vector3 inward = middle - (a + c) * 0.5f;
        inward.y = 0f;
        inward = inward.sqrMagnitude > 0f ? inward.normalized : Vector3.zero;

        float lowest = float.PositiveInfinity;
        const int samples = 9;
        for (int i = 0; i < samples; i++)
        {
            Vector3 p = Vector3.Lerp(a, c, (i + 0.5f) / samples) + inward * 1f;
            p.y = top;
            if (Physics.Raycast(p, Vector3.down, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
                lowest = Mathf.Min(lowest, hit.point.y);
        }
        return float.IsPositiveInfinity(lowest) ? groundHeight : lowest;
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        Vector3 pos = cam != null ? cam.transform.position : new Vector3(0f, -100000f, 0f);
        Shader.SetGlobalVector(PlayerPosId, pos);
    }
}
