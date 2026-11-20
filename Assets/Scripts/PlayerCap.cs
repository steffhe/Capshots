using UnityEngine;

// The baseball cap every player wears: a crown, a brim and a button on top, built from primitives so no
// art is needed. It sits on the head, so on a dead player's ragdoll it flies off together with the head.
// Colour: the team colour in Team Deathmatch, otherwise a colour picked from the player's actor number,
// so every client shows the same colour for the same player without sending anything.
public class PlayerCap : MonoBehaviour
{
    private Renderer[] renderers;
    private static Material capMaterial;

    // Puts a cap on a head made from Unity's sphere mesh, sized to it and drawn on the head's layer.
    // solid gives the cap colliders, for a ragdoll: on the head it tips over onto its brim instead of
    // sinking into the floor, and knocked off it lands on its own. A living player's cap has no colliders.
    public static PlayerCap Attach(Transform head, Color color, bool solid = false)
    {
        if (head == null) return null;

        GameObject root = new GameObject("Cap");
        root.layer = head.gameObject.layer;
        root.transform.SetParent(head, false);
        // Cancel the head's (and its parents') scale, so the cap's parts can be sized in metres
        Vector3 lossy = head.lossyScale;
        root.transform.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);

        // Head radius in metres: the sphere mesh has a radius of 0.5
        float r = 0.5f * Mathf.Max(lossy.x, lossy.z);

        Color dark = Color.Lerp(color, Color.black, 0.35f);
        PlayerCap cap = root.AddComponent<PlayerCap>();
        cap.renderers = new Renderer[]
        {
            // Crown: a dome hugging the top of the head, slightly bigger than it
            Part(root.transform, "Crown", PrimitiveType.Sphere, Vector3.zero, Vector3.one * (1.07f * r), Vector3.zero),
            // Brim: a thin flat disc sticking out the front from the crown's edge, tipped slightly down
            Part(root.transform, "Brim", PrimitiveType.Cylinder, new Vector3(0f, 0.25f * r, 1.05f * r), new Vector3(1.4f * r, 0.035f * r, 1.4f * r), new Vector3(8f, 0f, 0f)),
            // Button on top
            Part(root.transform, "Button", PrimitiveType.Sphere, new Vector3(0f, 1.08f * r, 0f), new Vector3(0.2f * r, 0.12f * r, 0.2f * r), Vector3.zero),
        };
        cap.renderers[0].GetComponent<MeshFilter>().sharedMesh = domeMesh != null ? domeMesh : (domeMesh = BuildDome(10, 24, "Cap Crown"));
        cap.SetColor(color, dark);
        if (solid)
        {
            MeshCollider crown = cap.renderers[0].gameObject.AddComponent<MeshCollider>();
            // A coarser copy for physics: a convex collider's hull must stay small, and nobody sees it
            crown.sharedMesh = colliderDome != null ? colliderDome : (colliderDome = BuildDome(4, 12, "Cap Crown Collider"));
            crown.convex = true; // needed on a moving Rigidbody
            cap.renderers[1].gameObject.AddComponent<BoxCollider>();
        }
        return cap;
    }

    public void SetColor(Color color)
    {
        SetColor(color, Color.Lerp(color, Color.black, 0.35f));
    }

    private void SetColor(Color crown, Color trim)
    {
        if (renderers == null) return;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            // The crown is the cap colour, the brim and button a darker shade of it
            Color c = i == 0 ? crown : trim;
            renderers[i].GetPropertyBlock(block);
            block.SetColor("_BaseColor", c); // URP
            block.SetColor("_Color", c);     // built-in shaders
            renderers[i].SetPropertyBlock(block);
        }
    }

    // The cap colour for a player, the same on every client
    public static Color ColorFor(int actor)
    {
        Manager m = Manager.Instance;
        if (m != null && m.IsTeamMode)
        {
            int team = m.GetTeam(actor);
            if (team >= 0) return Manager.TeamColors[team % 2];
        }
        // Golden-ratio steps round the colour wheel keep neighbouring actor numbers far apart in colour
        float hue = Mathf.Repeat(actor * 0.618034f, 1f);
        return Color.HSVToRGB(hue, 0.7f, 0.9f);
    }

    private static Renderer Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Vector3 euler)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        // No collider: the cap must never block bullets, movement or the ragdoll. Removed straight away,
        // because a plain Destroy only happens at the end of the frame.
        DestroyImmediate(part.GetComponent<Collider>());
        part.name = name;
        part.layer = parent.gameObject.layer;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(euler);
        part.transform.localScale = scale;

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = SharedMaterial(renderer.sharedMaterial);
        return renderer;
    }

    private static Mesh domeMesh, colliderDome;

    // The top of a unit sphere (radius 1), down to a little above its middle, like the crown of a cap.
    // Unity has no such primitive, and a squashed sphere bulges out below its widest point.
    private static Mesh BuildDome(int rings, int segments, string name)
    {
        // Down to 78 degrees from the top: the edge sits at 0.2 of the radius above the centre
        float maxAngle = 78f * Mathf.Deg2Rad;

        Vector3[] vertices = new Vector3[(rings + 1) * (segments + 1)];
        Vector2[] uvs = new Vector2[vertices.Length];
        for (int i = 0; i <= rings; i++)
        {
            float polar = maxAngle * i / rings;
            for (int j = 0; j <= segments; j++)
            {
                float around = 2f * Mathf.PI * j / segments;
                int v = i * (segments + 1) + j;
                vertices[v] = new Vector3(Mathf.Sin(polar) * Mathf.Cos(around), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(around));
                uvs[v] = new Vector2((float)j / segments, (float)i / rings);
            }
        }

        int[] triangles = new int[rings * segments * 6];
        int t = 0;
        for (int i = 0; i < rings; i++)
        {
            for (int j = 0; j < segments; j++)
            {
                int a = i * (segments + 1) + j, b = a + segments + 1;
                // Wound so the outside faces outwards
                triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = b;
                triangles[t++] = a + 1; triangles[t++] = b + 1; triangles[t++] = b;
            }
        }

        Mesh mesh = new Mesh { name = name };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        // On a unit sphere the normal is the position itself
        mesh.normals = vertices;
        mesh.RecalculateBounds();
        return mesh;
    }

    // One shared white URP material; each cap's colour comes from a property block
    private static Material SharedMaterial(Material fallback)
    {
        if (capMaterial != null) return capMaterial;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        capMaterial = lit != null ? new Material(lit) : new Material(fallback);
        capMaterial.name = "Cap";
        if (capMaterial.HasProperty("_BaseMap")) capMaterial.SetTexture("_BaseMap", null);
        if (capMaterial.HasProperty("_Smoothness")) capMaterial.SetFloat("_Smoothness", 0.15f);
        return capMaterial;
    }
}
