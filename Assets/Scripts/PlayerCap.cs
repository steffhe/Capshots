using UnityEngine;

// The hat every player wears (see Hats for the list): the baseball cap to start with, and more as
// they level up. Built from primitives and a few generated meshes. Each part is made of a surface
// (Surface): cotton twill, knit, felt, satin, glossy plastic, metal or hammered metal, each a
// material in Resources/Hats with its own shine and a tiling normal map (Tools/HatTextures), or a
// glow for the halo. It sits on
// the head, so on a dead player's ragdoll it flies off together with the head (or on its own, on a
// headshot). Every hat is drawn in shades of one colour: the team colour in Team Deathmatch,
// otherwise a colour picked from the player's actor number, so every client shows the same colour
// for the same player without sending anything.
public class PlayerCap : MonoBehaviour
{
    // Which shade of the hat's colour a part is drawn in
    private enum Shade { Main, Dark, Light }

    // What a part is made of: a material in Resources/Hats ("Hat" + the name), or the glow
    private enum Surface { Twill, Knit, Felt, Satin, Plastic, Metal, Hammered, Glow }
    private static readonly System.Collections.Generic.Dictionary<Surface, Material> surfaceMaterials = new System.Collections.Generic.Dictionary<Surface, Material>();
    // How brightly a glowing part (the halo) glows, in its light shade
    private const float HaloGlow = 1.8f;

    private Renderer[] renderers;
    private Shade[] shades;
    private bool[] glows;
    private static Material capMaterial;

    // The hat this is (see Hats)
    public int Hat { get; private set; }

    // While building: the parts so far, and whether they get colliders (a ragdoll's hat)
    private readonly System.Collections.Generic.List<Renderer> parts = new System.Collections.Generic.List<Renderer>();
    private readonly System.Collections.Generic.List<Shade> partShades = new System.Collections.Generic.List<Shade>();
    private readonly System.Collections.Generic.List<bool> partGlows = new System.Collections.Generic.List<bool>();
    // What the parts being built are made of (set by each hat as it builds)
    private Surface surface = Surface.Twill;
    private bool solid;

    // Puts a hat on a head made from Unity's sphere mesh, sized to it and drawn on the head's layer.
    // solid gives the hat colliders, for a ragdoll: on the head it tips over onto its brim instead of
    // sinking into the floor, and knocked off it lands on its own. A living player's hat has no
    // colliders, so it never changes what a shot hits: only the head itself counts.
    public static PlayerCap Attach(Transform head, Color color, bool solid = false, int hat = Hats.Cap)
    {
        if (head == null) return null;
        if (!Hats.IsValid(hat)) hat = Hats.Cap;

        GameObject root = new GameObject("Cap");
        root.layer = head.gameObject.layer;
        root.transform.SetParent(head, false);
        // Cancel the head's (and its parents') scale, so the hat's parts can be sized in metres
        Vector3 lossy = head.lossyScale;
        root.transform.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);

        // Head radius in metres: the sphere mesh has a radius of 0.5
        float r = 0.5f * Mathf.Max(lossy.x, lossy.z);

        PlayerCap cap = root.AddComponent<PlayerCap>();
        cap.Hat = hat;
        cap.solid = solid;
        cap.Build(hat, r);
        cap.renderers = cap.parts.ToArray();
        cap.shades = cap.partShades.ToArray();
        cap.glows = cap.partGlows.ToArray();
        cap.parts.Clear();
        cap.partShades.Clear();
        cap.partGlows.Clear();
        cap.SetColor(color);
        return cap;
    }

    public void SetColor(Color color)
    {
        if (renderers == null) return;
        Color dark = Color.Lerp(color, Color.black, 0.35f);
        Color light = Color.Lerp(color, Color.white, 0.45f);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color c = shades[i] == Shade.Dark ? dark : shades[i] == Shade.Light ? light : color;
            renderers[i].GetPropertyBlock(block);
            block.SetColor("_BaseColor", c); // URP
            block.SetColor("_Color", c);     // built-in shaders
            block.SetColor("_EmissionColor", glows != null && glows[i] ? c * HaloGlow : Color.black);
            renderers[i].SetPropertyBlock(block);
        }
    }

    #region Hats

    // The hat's parts, around the centre of a head of radius r (metres). +z is the way the head faces.
    private void Build(int hat, float r)
    {
        switch (hat)
        {
            case Hats.BackwardsCap: BuildCap(r, -1f); break;
            case Hats.Beanie: BuildBeanie(r); break;
            case Hats.BucketHat: BuildBucketHat(r); break;
            case Hats.HardHat: BuildHardHat(r); break;
            case Hats.PropellerBeanie: BuildPropellerBeanie(r); break;
            case Hats.TopHat: BuildTopHat(r); break;
            case Hats.VikingHelmet: BuildVikingHelmet(r); break;
            case Hats.Crown: BuildCrown(r); break;
            case Hats.Halo: BuildHalo(r); break;
            default: BuildCap(r, 1f); break;
        }
    }

    // A baseball cap: a crown hugging the top of the head, a brim (at the front, or the back when
    // facing is -1) and a button on top
    private void BuildCap(float r, float facing)
    {
        surface = Surface.Twill;
        Crown(Vector3.one * (1.07f * r));
        // Brim: a thin flat disc sticking out from the crown's edge, tipped slightly down
        Primitive("Brim", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, 0.25f * r, 1.05f * r * facing), new Vector3(1.4f * r, 0.035f * r, 1.4f * r), new Vector3(8f * facing, 0f, 0f), true);
        Primitive("Button", PrimitiveType.Sphere, Shade.Dark, new Vector3(0f, 1.08f * r, 0f), new Vector3(0.2f * r, 0.12f * r, 0.2f * r), Vector3.zero, false);
    }

    // A knitted beanie: a taller crown, a turned-up cuff round the bottom and a pom-pom
    private void BuildBeanie(float r)
    {
        surface = Surface.Knit;
        Crown(new Vector3(1.08f * r, 1.22f * r, 1.08f * r));
        Generated("Cuff", Frustum(0.97f, false), Shade.Dark, new Vector3(0f, 0.12f * r, 0f), new Vector3(1.12f * r, 0.3f * r, 1.12f * r), Vector3.zero, true);
        Primitive("Pom", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, 1.36f * r, 0f), Vector3.one * (0.42f * r), Vector3.zero, false);
    }

    // A bucket hat: a flat-topped crown narrowing upwards and a brim sloping down all round
    private void BuildBucketHat(float r)
    {
        surface = Surface.Twill;
        Generated("Crown", Frustum(0.82f, true), Shade.Main, new Vector3(0f, 0.18f * r, 0f), new Vector3(1.08f * r, 0.92f * r, 1.08f * r), Vector3.zero, true);
        Generated("Band", Frustum(0.96f, false), Shade.Dark, new Vector3(0f, 0.24f * r, 0f), new Vector3(1.09f * r, 0.16f * r, 1.09f * r), Vector3.zero, false);
        Generated("Brim", Frustum(0.68f, false), Shade.Dark, new Vector3(0f, 0.02f * r, 0f), new Vector3(1.6f * r, 0.22f * r, 1.6f * r), Vector3.zero, true);
    }

    // A builder's hard hat: a domed shell, a ridge from front to back and a flat brim, longer at the front
    private void BuildHardHat(float r)
    {
        surface = Surface.Plastic;
        Crown(new Vector3(1.1f * r, 1.15f * r, 1.18f * r));
        Generated("Ridge", Dome(), Shade.Dark, Vector3.zero, new Vector3(0.24f * r, 1.21f * r, 1.24f * r), Vector3.zero, false);
        Primitive("Brim", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, 0.23f * r, 0.12f * r), new Vector3(2.5f * r, 0.03f * r, 2.7f * r), Vector3.zero, true);
    }

    // A propeller beanie: a cap with a short brim and a propeller on top that spins faster the
    // faster the player moves
    private void BuildPropellerBeanie(float r)
    {
        surface = Surface.Twill;
        Crown(Vector3.one * (1.07f * r));
        Primitive("Brim", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, 0.25f * r, 0.95f * r), new Vector3(1.0f * r, 0.035f * r, 0.8f * r), new Vector3(8f, 0f, 0f), true);
        // The propeller is plastic
        surface = Surface.Plastic;
        Primitive("Stem", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, 1.16f * r, 0f), new Vector3(0.09f * r, 0.12f * r, 0.09f * r), Vector3.zero, false);

        GameObject rotor = new GameObject("Rotor");
        rotor.layer = gameObject.layer;
        rotor.transform.SetParent(transform, false);
        rotor.transform.localPosition = new Vector3(0f, 1.3f * r, 0f);
        rotor.AddComponent<HatSpinner>();
        Primitive("Hub", PrimitiveType.Sphere, Shade.Light, Vector3.zero, Vector3.one * (0.16f * r), Vector3.zero, false, rotor.transform);
        // Two blades, each twisted a little like a real propeller
        Primitive("Blade", PrimitiveType.Cube, Shade.Main, new Vector3(0.5f * r, 0f, 0f), new Vector3(0.95f * r, 0.03f * r, 0.24f * r), new Vector3(12f, 0f, 0f), false, rotor.transform);
        Primitive("Blade", PrimitiveType.Cube, Shade.Main, new Vector3(-0.5f * r, 0f, 0f), new Vector3(0.95f * r, 0.03f * r, 0.24f * r), new Vector3(-12f, 0f, 0f), false, rotor.transform);
    }

    // A top hat, sitting up on the head: a tall crown with a band, on a flat brim
    private void BuildTopHat(float r)
    {
        const float sit = 0.6f; // where the brim rests, as a fraction of the head's radius
        surface = Surface.Felt;
        Primitive("Brim", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, sit * r, 0f), new Vector3(2.4f * r, 0.025f * r, 2.4f * r), Vector3.zero, true);
        Primitive("Crown", PrimitiveType.Cylinder, Shade.Main, new Vector3(0f, (sit + 0.56f) * r, 0f), new Vector3(1.6f * r, 0.56f * r, 1.6f * r), Vector3.zero, true);
        surface = Surface.Satin;
        Primitive("Band", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, (sit + 0.12f) * r, 0f), new Vector3(1.63f * r, 0.09f * r, 1.63f * r), Vector3.zero, false);
    }

    // A horned helmet: a round helmet with a rim, and a horn sticking out and up from each side
    private void BuildVikingHelmet(float r)
    {
        // A hammered metal helmet with smooth metal trim, and horns of polished horn
        surface = Surface.Hammered;
        Crown(Vector3.one * (1.08f * r));
        surface = Surface.Metal;
        Generated("Rim", Frustum(1f, false), Shade.Dark, new Vector3(0f, 0.12f * r, 0f), new Vector3(1.11f * r, 0.16f * r, 1.11f * r), Vector3.zero, true);
        Generated("Ridge", Dome(), Shade.Dark, Vector3.zero, new Vector3(0.16f * r, 1.13f * r, 1.13f * r), Vector3.zero, false);
        surface = Surface.Plastic;
        for (int side = -1; side <= 1; side += 2)
        {
            Generated("Horn", Frustum(0f, false), Shade.Light, new Vector3(0.82f * r * side, 0.55f * r, 0f), new Vector3(0.24f * r, 0.8f * r, 0.24f * r), new Vector3(0f, 0f, -58f * side), false);
        }
    }

    // A crown: a band flaring out a little, with points round its top edge, each with a bead on its tip
    private void BuildCrown(float r)
    {
        const float bottom = 0.5f, height = 0.32f, radius = 0.9f, flare = 1.08f;
        // Metal, in the player's colour
        surface = Surface.Metal;
        Generated("Band", Frustum(flare, false), Shade.Main, new Vector3(0f, bottom * r, 0f), new Vector3(radius * r, height * r, radius * r), Vector3.zero, true);
        const int points = 8;
        float top = (bottom + height) * r;
        float rim = radius * flare * r;
        for (int i = 0; i < points; i++)
        {
            float angle = 360f * i / points;
            Quaternion around = Quaternion.Euler(0f, angle, 0f);
            Vector3 outward = around * Vector3.forward;
            // Tipped outwards a little, like the band
            Vector3 euler = (around * Quaternion.Euler(10f, 0f, 0f)).eulerAngles;
            Vector3 basePoint = outward * (rim * 0.97f) + Vector3.up * (top - 0.02f * r);
            Generated("Point", Frustum(0f, false), Shade.Main, basePoint, new Vector3(0.17f * r, 0.34f * r, 0.17f * r), euler, false);
            Vector3 tip = basePoint + Quaternion.Euler(euler) * Vector3.up * (0.34f * r);
            Primitive("Bead", PrimitiveType.Sphere, Shade.Light, tip, Vector3.one * (0.11f * r), Vector3.zero, false);
        }
    }

    // A halo: a ring floating above the head
    private void BuildHalo(float r)
    {
        surface = Surface.Glow;
        Generated("Ring", Torus(0.24f), Shade.Light, new Vector3(0f, 1.42f * r, 0f), Vector3.one * (0.68f * r), new Vector3(-8f, 0f, 0f), true);
    }

    #endregion

    #region Parts

    // The dome most hats are built on, hugging the top of the head. On a ragdoll its collider is a
    // coarser convex copy: a convex collider's hull must stay small, and nobody sees it.
    private void Crown(Vector3 scale)
    {
        Renderer crown = Generated("Crown", Dome(), Shade.Main, Vector3.zero, scale, Vector3.zero, false);
        if (solid)
        {
            MeshCollider collider = crown.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = colliderDome != null ? colliderDome : (colliderDome = BuildDome(4, 12, "Cap Crown Collider", false));
            collider.convex = true; // needed on a moving Rigidbody
        }
    }

    // A part made from one of Unity's primitives. collide: gets a box collider on a ragdoll's hat.
    private Renderer Primitive(string name, PrimitiveType type, Shade shade, Vector3 position, Vector3 scale, Vector3 euler, bool collide, Transform parent = null)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        // No collider: the hat must never block bullets, movement or the ragdoll. Removed straight away,
        // because a plain Destroy only happens at the end of the frame.
        DestroyImmediate(part.GetComponent<Collider>());
        return AddPart(part, name, shade, position, scale, euler, collide, parent);
    }

    // A part with a generated mesh
    private Renderer Generated(string name, Mesh mesh, Shade shade, Vector3 position, Vector3 scale, Vector3 euler, bool collide)
    {
        GameObject part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        return AddPart(part, name, shade, position, scale, euler, collide, null);
    }

    private Renderer AddPart(GameObject part, string name, Shade shade, Vector3 position, Vector3 scale, Vector3 euler, bool collide, Transform parent)
    {
        part.name = name;
        part.layer = gameObject.layer;
        part.transform.SetParent(parent != null ? parent : transform, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(euler);
        part.transform.localScale = scale;
        // A box fitted round the part's mesh: enough for a hat tumbling on the floor
        if (solid && collide) part.AddComponent<BoxCollider>();

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = SurfaceMaterial(surface, renderer.sharedMaterial);
        parts.Add(renderer);
        partShades.Add(shade);
        partGlows.Add(surface == Surface.Glow);
        return renderer;
    }

    #endregion

    // The colour of the cap in the logo, used on the main menu's player model
    public static readonly Color LogoColor = new Color(0.01f, 0.42f, 0.68f);

    // Puts a hat on every player head in the scene, e.g. the main menu's player model, which is a copy
    // of the player without the Player script. Run again, it swaps the hat they wear for this one.
    public static void AttachToSceneHeads(Color color, int hat = Hats.Cap)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!t.CompareTag("Head")) continue;
            if (t.GetComponent<MeshFilter>() == null) continue; // only visible sphere heads
            foreach (PlayerCap old in t.GetComponentsInChildren<PlayerCap>(true)) Destroy(old.gameObject);
            Attach(t, color, false, hat);
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

    private static Mesh domeMesh, colliderDome;
    private static readonly System.Collections.Generic.Dictionary<string, Mesh> meshes = new System.Collections.Generic.Dictionary<string, Mesh>();

    private static Mesh Dome() => domeMesh != null ? domeMesh : (domeMesh = BuildDome(10, 24, "Cap Crown", true));

    // A round tube 1 high with a bottom radius of 1 and a top radius of topRadius (0 for a cone),
    // seen from inside and out; capTop closes its top with a flat disc
    private static Mesh Frustum(float topRadius, bool capTop)
    {
        string key = "Frustum " + topRadius.ToString("0.###") + (capTop ? " capped" : "");
        Mesh mesh;
        if (meshes.TryGetValue(key, out mesh) && mesh != null) return mesh;

        const int segments = 24;
        var vertices = new System.Collections.Generic.List<Vector3>();
        var normals = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var triangles = new System.Collections.Generic.List<int>();

        // The side: a ring at the bottom and one at the top. Its outward normal leans up as it narrows.
        for (int side = 0; side < 2; side++)
        {
            int start = vertices.Count;
            float flip = side == 0 ? 1f : -1f;
            for (int j = 0; j <= segments; j++)
            {
                float a = 2f * Mathf.PI * j / segments;
                Vector3 around = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 normal = (around + Vector3.up * (1f - topRadius)).normalized * flip;
                vertices.Add(around); normals.Add(normal); uvs.Add(new Vector2((float)j / segments, 0f));
                vertices.Add(around * topRadius + Vector3.up); normals.Add(normal); uvs.Add(new Vector2((float)j / segments, 1f));
            }
            for (int j = 0; j < segments; j++)
            {
                int b0 = start + j * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                if (side == 0) { triangles.Add(b0); triangles.Add(t0); triangles.Add(b1); triangles.Add(b1); triangles.Add(t0); triangles.Add(t1); }
                else { triangles.Add(b0); triangles.Add(b1); triangles.Add(t0); triangles.Add(b1); triangles.Add(t1); triangles.Add(t0); }
            }
        }

        if (capTop && topRadius > 0f)
        {
            int centre = vertices.Count;
            vertices.Add(Vector3.up); normals.Add(Vector3.up); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int j = 0; j <= segments; j++)
            {
                float a = 2f * Mathf.PI * j / segments;
                vertices.Add(new Vector3(Mathf.Cos(a) * topRadius, 1f, Mathf.Sin(a) * topRadius)); normals.Add(Vector3.up);
                uvs.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a)));
            }
            for (int j = 0; j < segments; j++)
            {
                triangles.Add(centre); triangles.Add(centre + j + 2); triangles.Add(centre + j + 1);
            }
        }

        mesh = new Mesh { name = "Hat " + key };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        // Tangents, for the surfaces' normal maps
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        meshes[key] = mesh;
        return mesh;
    }

    // A ring lying flat, 1 from its centre to the middle of its tube, the tube thickness/2 across
    private static Mesh Torus(float thickness)
    {
        string key = "Torus " + thickness.ToString("0.###");
        Mesh mesh;
        if (meshes.TryGetValue(key, out mesh) && mesh != null) return mesh;

        const int segments = 32, sides = 10;
        float tube = thickness * 0.5f;
        Vector3[] vertices = new Vector3[(segments + 1) * (sides + 1)];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] torusUvs = new Vector2[vertices.Length];
        for (int i = 0; i <= segments; i++)
        {
            float a = 2f * Mathf.PI * i / segments;
            Vector3 around = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (int j = 0; j <= sides; j++)
            {
                float b = 2f * Mathf.PI * j / sides;
                Vector3 normal = around * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                vertices[i * (sides + 1) + j] = around + normal * tube;
                normals[i * (sides + 1) + j] = normal;
                torusUvs[i * (sides + 1) + j] = new Vector2((float)i / segments, (float)j / sides);
            }
        }
        int[] triangles = new int[segments * sides * 6];
        int t = 0;
        for (int i = 0; i < segments; i++)
        {
            for (int j = 0; j < sides; j++)
            {
                int a = i * (sides + 1) + j, b = a + sides + 1;
                triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = b;
                triangles[t++] = a + 1; triangles[t++] = b + 1; triangles[t++] = b;
            }
        }

        mesh = new Mesh { name = "Hat " + key };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = torusUvs;
        mesh.triangles = triangles;
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        meshes[key] = mesh;
        return mesh;
    }

    // The top of a unit sphere (radius 1), down to a little above its middle, like the crown of a cap.
    // Unity has no such primitive, and a squashed sphere bulges out below its widest point.
    // doubleSided adds the inside of the dome, so an upside-down or knocked-off cap doesn't turn
    // invisible when seen from below (Unity only draws the side a triangle faces).
    private static Mesh BuildDome(int rings, int segments, string name, bool doubleSided)
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

        // On a unit sphere the outward normal is the position itself
        Vector3[] normals = (Vector3[])vertices.Clone();

        if (doubleSided)
        {
            // A second copy of every vertex with the normal pointing in, and every triangle wound the
            // other way round, so the inside is drawn and lit as a surface of its own
            int count = vertices.Length;
            System.Array.Resize(ref vertices, count * 2);
            System.Array.Resize(ref normals, count * 2);
            System.Array.Resize(ref uvs, count * 2);
            for (int v = 0; v < count; v++)
            {
                vertices[count + v] = vertices[v];
                normals[count + v] = -normals[v];
                uvs[count + v] = uvs[v];
            }

            int outside = triangles.Length;
            System.Array.Resize(ref triangles, outside * 2);
            for (int i = 0; i < outside; i += 3)
            {
                triangles[outside + i] = triangles[i] + count;
                triangles[outside + i + 1] = triangles[i + 2] + count;
                triangles[outside + i + 2] = triangles[i + 1] + count;
            }
        }

        Mesh mesh = new Mesh { name = name };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        // Tangents, for the surfaces' normal maps
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    // The material for a surface, white: each hat's colour comes from a property block. Falls back
    // to the plain shared material if its asset is missing.
    private static Material SurfaceMaterial(Surface kind, Material fallback)
    {
        Material material;
        if (surfaceMaterials.TryGetValue(kind, out material) && material != null) return material;
        material = kind == Surface.Glow ? DroneBody.GlowMaterial : Resources.Load<Material>("Hats/Hat" + kind);
        if (material == null) material = SharedMaterial(fallback);
        surfaceMaterials[kind] = material;
        return material;
    }

    // One shared white URP material; each cap's colour comes from a property block
    internal static Material SharedMaterial(Material fallback)
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
