using UnityEngine;

// Makes a map's invisible boundary walls visible as force fields. Put it on the parent of the
// wall colliders (Downtown's "Bounds"): for each child box collider that is a wall (thin in one
// horizontal direction), it adds a glowing panel on the side facing the middle of the map, from
// groundHeight up to the top of the collider. The colliders themselves are left as they are.
public class BoundaryField : MonoBehaviour
{
    private static readonly int PlayerPosId = Shader.PropertyToID("_CapshotsPlayerPos");

    [Tooltip("A material using the Capshots/Force Field shader")]
    public Material material;
    [Tooltip("World height of the ground along the walls; the field's glow starts here")]
    public float groundHeight = 0f;

    private void Start()
    {
        if (material == null) return;

        BoxCollider[] walls = GetComponentsInChildren<BoxCollider>();
        Vector3 middle = Vector3.zero;
        int count = 0;
        foreach (BoxCollider wall in walls)
        {
            if (!IsWall(wall.bounds)) continue;
            middle += wall.bounds.center;
            count++;
        }
        if (count == 0) return;
        middle /= count;

        foreach (BoxCollider wall in walls)
        {
            if (IsWall(wall.bounds)) AddPanel(wall.bounds, middle);
        }
    }

    // Tall, and much thinner in one horizontal direction than the other (not the floor)
    private static bool IsWall(Bounds b)
    {
        float thin = Mathf.Min(b.size.x, b.size.z), wide = Mathf.Max(b.size.x, b.size.z);
        return b.size.y > 2f && thin <= 2f && wide > thin * 4f;
    }

    private void AddPanel(Bounds b, Vector3 middle)
    {
        bool alongX = b.size.x > b.size.z; // the wall runs along x (a north or south wall)
        float bottom = groundHeight, top = b.max.y;
        if (top <= bottom) return;

        // The face toward the middle of the map, nudged in so it doesn't z-fight anything flush
        Vector3 a, c;
        if (alongX)
        {
            float z = middle.z > b.center.z ? b.max.z + 0.02f : b.min.z - 0.02f;
            a = new Vector3(b.min.x, 0f, z);
            c = new Vector3(b.max.x, 0f, z);
        }
        else
        {
            float x = middle.x > b.center.x ? b.max.x + 0.02f : b.min.x - 0.02f;
            a = new Vector3(x, 0f, b.min.z);
            c = new Vector3(x, 0f, b.max.z);
        }
        float length = Vector3.Distance(a, c), height = top - bottom;

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

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        Vector3 pos = cam != null ? cam.transform.position : new Vector3(0f, -100000f, 0f);
        Shader.SetGlobalVector(PlayerPosId, pos);
    }
}
