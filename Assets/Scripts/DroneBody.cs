using System.Collections.Generic;
using UnityEngine;

// Turns a player's capsule body and sphere head into a cute, round hover drone, built from primitives so
// no art is needed:
//  - Body: a white egg-shaped shell with a coloured belt, a glowing chest light and a thruster underneath
//    (which sits on HoverVisuals' glow disc), and a glowing ring in the gap below the floating head.
//  - Head: the same white shell, a dark glossy face screen with two big glowing eyes that blink now and
//    then, a little blush, and round "ear" pods on the sides. The cap still sits on top.
// Accents are the player's colour (the team colour in Team Deathmatch, which also tints the shell a bit).
// Only looks change: the capsule and sphere keep their colliders and hitboxes, their renderers are just
// swapped for the drone's parts. None of the parts have colliders.
public class DroneBody : MonoBehaviour
{
    // Off-white shell, and how far it's tinted towards the team colour in Team Deathmatch
    private static readonly Color ShellColor = new Color(0.93f, 0.94f, 0.96f);
    private const float TeamShellTint = 0.3f;
    private static readonly Color VisorColor = new Color(0.04f, 0.05f, 0.08f);
    private static readonly Color TrimColor = new Color(0.22f, 0.24f, 0.28f);
    private static readonly Color BlushColor = new Color(1f, 0.45f, 0.6f);
    // How bright the glowing parts are (above 1 so the bloom picks them up)
    private const float EyeGlow = 2.2f;
    private const float LightGlow = 2.6f;

    private Renderer[] shell;   // body shell and head
    private Renderer[] accents; // belt and ear pods: lit, in the accent colour
    private Renderer[] lights;  // chest light, thruster, neck ring and ear dots: glowing accent colour
    private Renderer[] eyes;
    private Renderer[] blush;
    private Renderer thruster;
    private Transform[] eyeTransforms;
    private Vector3 eyeScale;
    private bool dead;
    private Color accent;
    private MaterialPropertyBlock block;

    private float nextBlink;
    private float blinkStart = -1f;
    private float flickerSeed;

    private static Material shellMaterial, visorMaterial, glowMaterial;

    // Dresses a capsule body and the sphere head on (or above) it. Safe to call twice: the second call
    // only recolours. dead: a ragdoll, with closed eyes and the lights off.
    public static DroneBody Dress(Transform body, Transform head, Color accent, bool teamMode = false, bool dead = false)
    {
        if (body == null) return null;
        DroneBody drone = body.GetComponent<DroneBody>();
        if (drone == null)
        {
            drone = body.gameObject.AddComponent<DroneBody>();
            drone.Build(body, head, dead);
        }
        drone.SetColor(accent, teamMode);
        return drone;
    }

    // Dresses every player model in the scene that isn't dressed yet, e.g. the main menu's player model
    public static void DressSceneModels(Color accent)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!t.CompareTag("Head") || t.GetComponent<MeshFilter>() == null) continue;
            Transform body = t.parent;
            if (body == null || body.GetComponent<MeshFilter>() == null || body.GetComponent<DroneBody>() != null) continue;
            Dress(body, t, accent);
        }
    }

    public void SetColor(Color color, bool teamMode)
    {
        accent = color;
        if (block == null) block = new MaterialPropertyBlock();

        Color shellColor = teamMode ? Color.Lerp(ShellColor, color, TeamShellTint) : ShellColor;
        Paint(shell, shellColor, Color.black);
        Paint(accents, Color.Lerp(color, Color.black, 0.1f), Color.black);
        Paint(lights, color, dead ? Color.black : color * LightGlow);
        Color eye = Color.Lerp(color, Color.white, 0.65f);
        Paint(eyes, dead ? TrimColor : eye, dead ? Color.black : eye * EyeGlow);
        Paint(blush, BlushColor, dead ? Color.black : BlushColor * 0.6f);
    }

    private void Paint(Renderer[] renderers, Color color, Color emission)
    {
        if (renderers == null) return;
        foreach (Renderer r in renderers) Paint(r, color, emission);
    }

    private void Paint(Renderer r, Color color, Color emission)
    {
        if (r == null) return;
        r.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color); // URP
        block.SetColor("_Color", color);     // built-in shaders
        block.SetColor("_EmissionColor", emission);
        r.SetPropertyBlock(block);
    }

    private void Update()
    {
        if (dead) return;
        float time = Time.time;

        // Blink every few seconds: the eyes squash shut and open again over about a tenth of a second
        if (eyeTransforms != null)
        {
            if (blinkStart < 0f && time >= nextBlink) blinkStart = time;
            float squash = 1f;
            if (blinkStart >= 0f)
            {
                float t = (time - blinkStart) / 0.14f;
                if (t >= 1f)
                {
                    blinkStart = -1f;
                    // Now and then a quick double blink
                    nextBlink = time + (Random.value < 0.2f ? 0.18f : Random.Range(2f, 5.5f));
                }
                else squash = Mathf.Lerp(1f, 0.1f, 1f - Mathf.Abs(t * 2f - 1f));
            }
            foreach (Transform eyeTransform in eyeTransforms)
                if (eyeTransform != null) eyeTransform.localScale = new Vector3(eyeScale.x, eyeScale.y * squash, eyeScale.z);
        }

        // The thruster flickers gently
        if (thruster != null && block != null)
        {
            float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(flickerSeed, time * 6f);
            Paint(thruster, accent, accent * (LightGlow * flicker));
        }
    }

    #region Building

    private void Build(Transform body, Transform head, bool isDead)
    {
        dead = isDead;
        block = new MaterialPropertyBlock();
        flickerSeed = Random.value * 100f;
        nextBlink = Time.time + Random.Range(1f, 4f);

        // The original capsule stays (colliders, hitbox positions), only hidden
        Renderer capsule = body.GetComponent<Renderer>();
        Mesh capsuleMesh = body.GetComponent<MeshFilter>() != null ? body.GetComponent<MeshFilter>().sharedMesh : null;
        if (capsule != null) capsule.enabled = false;

        // A child that cancels the capsule's (and its parents') scale, so parts can be placed in metres
        // from the capsule's centre
        Transform root = Root("Drone Body", body);
        float halfHeight = (capsuleMesh != null ? capsuleMesh.bounds.extents.y : 1f) * body.lossyScale.y;
        float radius = (capsuleMesh != null ? capsuleMesh.bounds.extents.x : 0.5f) * Mathf.Max(body.lossyScale.x, body.lossyScale.z);
        float bottom = -halfHeight;

        // The head floats just above the shell, so the shell fills the space from the thruster to the chin
        float headRadius = head != null ? 0.5f * Mathf.Max(head.lossyScale.x, head.lossyScale.z) : 0.35f;
        float headY = head != null ? root.InverseTransformPoint(head.position).y : halfHeight + headRadius;
        // Hangs from just under the head, no taller than MaxBodyHeight, so the head stays big and cute
        // and a tall body floats higher over its glow instead of stretching into a pill
        const float thrusterSpace = 0.14f;
        const float MaxBodyHeight = 0.78f;
        float top = headY - headRadius - 0.06f;
        float height = Mathf.Clamp(top - (bottom + thrusterSpace), 0.3f, MaxBodyHeight);
        float shellBottom = top - height;
        // Round: about as wide as tall, and narrower than the capsule
        float width = Mathf.Clamp(height * 1.05f, 0.5f, Mathf.Min(MaxBodyHeight, radius * 2f * 0.84f));
        float centre = shellBottom + height * 0.5f;
        float k = width / MaxBodyHeight; // scales the details with the body

        Renderer egg = Part(root, "Shell", PrimitiveType.Sphere, ShellMaterial, new Vector3(0f, centre, 0f), new Vector3(width, height, width));

        // Belt: a band round the shell a little below its middle, as wide as the shell there
        float beltY = -0.1f * height;
        float beltWidth = width * Mathf.Sqrt(1f - 0.04f) + 0.02f;
        Renderer belt = Part(root, "Belt", PrimitiveType.Cylinder, ShellMaterial, new Vector3(0f, centre + beltY, 0f), new Vector3(beltWidth, 0.025f, beltWidth));

        // Chest light: a flat round lamp on the front, above the belt
        float chestY = 0.14f * height;
        float chestZ = 0.5f * width * Mathf.Sqrt(1f - Mathf.Pow(chestY / (0.5f * height), 2f));
        Renderer chest = Part(root, "Chest Light", PrimitiveType.Sphere, GlowMaterial, new Vector3(0f, centre + chestY, chestZ - 0.01f), new Vector3(0.13f, 0.13f, 0.05f) * k, false);

        // Thruster: a dark cap under the shell with a glowing nozzle
        Renderer housing = Part(root, "Thruster", PrimitiveType.Cylinder, VisorMaterial, new Vector3(0f, shellBottom + 0.01f, 0f), new Vector3(0.32f * k, 0.04f, 0.32f * k));
        thruster = Part(root, "Nozzle", PrimitiveType.Cylinder, GlowMaterial, new Vector3(0f, shellBottom - 0.03f, 0f), new Vector3(0.22f * k, 0.012f, 0.22f * k), false);

        // A thin glowing ring in the gap under the head, as if the head floats on it
        Renderer neck = Part(root, "Neck Ring", PrimitiveType.Cylinder, GlowMaterial, new Vector3(0f, top + 0.03f, 0f), new Vector3(0.26f * k, 0.008f, 0.26f * k), false);

        var shellList = new List<Renderer> { egg };
        var accentList = new List<Renderer> { belt };
        var lightList = new List<Renderer> { chest, thruster, neck };
        var trim = new List<Renderer> { housing };

        if (head != null) BuildHead(head, shellList, accentList, lightList);

        shell = shellList.ToArray();
        accents = accentList.ToArray();
        lights = lightList.ToArray();
        foreach (Renderer r in trim) Paint(r, TrimColor, Color.black);
        // The thruster is painted every frame by Update; a ragdoll's is simply off
        if (dead) thruster = null;
    }

    private void BuildHead(Transform head, List<Renderer> shellList,
        List<Renderer> accentList, List<Renderer> lightList)
    {
        // The head sphere itself becomes part of the shell
        Renderer headRenderer = head.GetComponent<Renderer>();
        if (headRenderer != null)
        {
            headRenderer.sharedMaterial = ShellMaterial;
            shellList.Add(headRenderer);
        }

        Transform root = Root("Drone Face", head);
        float r = 0.5f * Mathf.Max(head.lossyScale.x, head.lossyScale.z);

        // Face screen: a dark glossy bulge on the front, a little below the middle (the cap's brim is above it)
        Vector3 visorCentre = new Vector3(0f, -0.12f * r, 0.6f * r);
        Vector3 visorRadii = new Vector3(0.75f * r, 0.42f * r, 0.45f * r);
        Renderer visor = Part(root, "Face", PrimitiveType.Sphere, VisorMaterial, visorCentre, visorRadii * 2f);
        Paint(visor, VisorColor, Color.black);

        // Two big oval eyes and a little blush, sitting on the face screen's surface
        eyeScale = new Vector3(0.2f * r, 0.3f * r, 0.08f * r);
        eyeTransforms = new Transform[2];
        eyes = new Renderer[2];
        blush = new Renderer[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            Vector3 eyePosition = OnVisor(new Vector2(side * 0.24f * r, -0.06f * r), visorCentre, visorRadii, 0.02f * r);
            Vector3 eyeSize = dead ? new Vector3(eyeScale.x, eyeScale.y * 0.12f, eyeScale.z) : eyeScale;
            eyes[i] = Part(root, i == 0 ? "Left Eye" : "Right Eye", PrimitiveType.Sphere, GlowMaterial, eyePosition, eyeSize, false);
            eyeTransforms[i] = eyes[i].transform;

            Vector3 blushPosition = OnVisor(new Vector2(side * 0.44f * r, -0.3f * r), visorCentre, visorRadii, 0.01f * r);
            blush[i] = Part(root, "Blush", PrimitiveType.Sphere, GlowMaterial, blushPosition, new Vector3(0.14f * r, 0.07f * r, 0.04f * r), false);

            // Ear pods: round discs on the sides of the head, with a glowing dot in the middle
            Vector3 earPosition = new Vector3(side * 0.97f * r, -0.02f * r, 0f);
            accentList.Add(Part(root, "Ear", PrimitiveType.Cylinder, ShellMaterial, earPosition, new Vector3(0.45f * r, 0.1f * r, 0.45f * r), true, new Vector3(0f, 0f, 90f)));
            lightList.Add(Part(root, "Ear Light", PrimitiveType.Cylinder, GlowMaterial, earPosition, new Vector3(0.2f * r, 0.11f * r, 0.2f * r), false, new Vector3(0f, 0f, 90f)));
        }
    }

    // A point on the front of the face screen (an ellipsoid) at the given x and y, pushed out by lift
    private static Vector3 OnVisor(Vector2 xy, Vector3 centre, Vector3 radii, float lift)
    {
        float dx = xy.x / radii.x, dy = (xy.y - centre.y) / radii.y;
        float z = centre.z + radii.z * Mathf.Sqrt(Mathf.Max(0f, 1f - dx * dx - dy * dy));
        return new Vector3(xy.x, xy.y, z - 0.02f * radii.z + lift);
    }

    // A child that cancels the parent's (and its parents') scale, on the parent's layer
    private static Transform Root(string name, Transform parent)
    {
        Transform root = new GameObject(name).transform;
        root.gameObject.layer = parent.gameObject.layer;
        root.SetParent(parent, false);
        Vector3 lossy = parent.lossyScale;
        root.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);
        return root;
    }

    private static Renderer Part(Transform parent, string name, PrimitiveType type, Material material, Vector3 position, Vector3 scale, bool shadows = true, Vector3 euler = default)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        // No collider: the drone's parts must never block bullets, movement or the ragdoll. Removed
        // straight away, because a plain Destroy only happens at the end of the frame.
        DestroyImmediate(part.GetComponent<Collider>());
        part.name = name;
        part.layer = parent.gameObject.layer;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(euler);
        part.transform.localScale = scale;

        Renderer renderer = part.GetComponent<Renderer>();
        if (material != null) renderer.sharedMaterial = material;
        if (!shadows) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return renderer;
    }

    private static Material ShellMaterial => shellMaterial != null ? shellMaterial : (shellMaterial = Lit("Drone Shell", 0.7f));
    private static Material VisorMaterial => visorMaterial != null ? visorMaterial : (visorMaterial = Lit("Drone Visor", 0.92f));

    // Glowing parts use a material asset with emission switched on, so builds keep the shader variant
    // that glows (a material made in code can't ask for one that was left out)
    private static Material GlowMaterial
    {
        get
        {
            if (glowMaterial != null) return glowMaterial;
            Material asset = Resources.Load<Material>("DroneGlow");
            glowMaterial = asset != null ? asset : Lit("Drone Glow", 0.5f);
            return glowMaterial;
        }
    }

    private static Material Lit(string name, float smoothness)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        // The same shader the cap and the original body use, which every build includes
        Material material = lit != null ? new Material(lit) : new Material(PlayerCap.SharedMaterial(null));
        material.name = name;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        return material;
    }

    #endregion
}
