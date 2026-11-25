using System.Collections.Generic;
using UnityEngine;

// Turns a player's capsule body and sphere head into a cute, round hover drone, built from primitives so
// no art is needed:
//  - Body: a slim white capsule (crouching makes it shorter, not thinner) with a coloured belt, a glowing
//    chest light, a glowing collar under the head and a thruster underneath, over HoverVisuals' glow disc.
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
    // The body capsule's radius, standing and crouching (the head's is about 0.35)
    private const float BodyRadius = 0.3f;

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

    private static Material shellMaterial, visorMaterial, glowMaterial, trimMaterial;

    // Hit feedback and damage, set by Player from its health on every machine: the shell flashes
    // white for a moment when hit, and below LowHealth the lights and eyes flicker and the collar
    // spits sparks now and then, more as it gets worse
    private const float LowHealth = 0.35f;
    private const float HitFlashTime = 0.15f;
    private float hitFlash;
    private float health = 1f;
    private bool flickering;
    private float nextDamageSpark;
    private Color shellColorNow = ShellColor, eyeColor = Color.white;
    private Transform collar;
    // Off for the local player's own body, which they don't see (sparks would show in front of their eyes)
    public bool showSparks = true;

    // Dresses a capsule body and the sphere head on (or above) it. Safe to call twice: the second call
    // only recolours. dead: a ragdoll, with glowing X-es for eyes and its other lights off.
    // face: the face to show (see Faces), or -1 to keep the one it has.
    public static DroneBody Dress(Transform body, Transform head, Color accent, bool teamMode = false, bool dead = false, int face = -1)
    {
        if (body == null) return null;
        DroneBody drone = body.GetComponent<DroneBody>();
        if (drone == null)
        {
            drone = body.gameObject.AddComponent<DroneBody>();
            drone.Build(body, head, dead);
        }
        drone.SetColor(accent, teamMode);
        if (face >= 0) drone.SetFace(face);
        return drone;
    }

    // Dresses every player model in the scene that isn't dressed yet, e.g. the main menu's player model
    public static void DressSceneModels(Color accent, int face = -1)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!t.CompareTag("Head") || t.GetComponent<MeshFilter>() == null) continue;
            Transform body = t.parent;
            if (body == null || body.GetComponent<MeshFilter>() == null || body.GetComponent<DroneBody>() != null) continue;
            Dress(body, t, accent, false, false, face);
        }
    }

    public void SetColor(Color color, bool teamMode)
    {
        accent = color;
        if (block == null) block = new MaterialPropertyBlock();

        Color shellColor = teamMode ? Color.Lerp(ShellColor, color, TeamShellTint) : ShellColor;
        shellColorNow = shellColor;
        Paint(shell, shellColor, Color.black);
        Paint(accents, Color.Lerp(color, Color.black, 0.1f), Color.black);
        Paint(lights, color, dead ? Color.black : color * LightGlow);
        Color eye = Color.Lerp(color, Color.white, 0.65f);
        eyeColor = eye;
        // A ragdoll's X-eyes glow just like living eyes
        Paint(eyes, eye, eye * EyeGlow);
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

    // Taking a hit: a quick white flash of the shell
    public void Hit()
    {
        if (dead) return;
        hitFlash = 1f;
    }

    // Health from 0 to 1. Below LowHealth the drone flickers and sparks.
    public void SetHealth(float fraction)
    {
        health = Mathf.Clamp01(fraction);
    }

    private void Update()
    {
        if (dead) return;
        float time = Time.time;
        UpdateDamage(time);

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

        // The thruster flickers gently (and cuts out now and then when badly hurt)
        if (thruster != null && block != null)
        {
            float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(flickerSeed, time * 6f);
            if (flickering) flicker *= damageFlicker;
            Paint(thruster, accent, accent * (LightGlow * flicker));
        }
    }

    private float damageFlicker = 1f;

    private void UpdateDamage(float time)
    {
        if (block == null) return;

        if (hitFlash > 0f)
        {
            hitFlash = Mathf.Max(0f, hitFlash - Time.deltaTime / HitFlashTime);
            Paint(shell, shellColorNow, Color.white * (1.6f * hitFlash));
        }

        bool low = health > 0f && health < LowHealth;
        if (low)
        {
            // How bad it is: 0 at the threshold, 1 at no health left
            float hurt = 1f - health / LowHealth;
            // Lights mostly on, cutting out for a frame or two more often the worse it gets
            damageFlicker = Random.value < 0.05f + 0.2f * hurt ? Random.Range(0.05f, 0.3f) : 1f;
            Paint(lights, accent, accent * (LightGlow * damageFlicker));
            Paint(eyes, eyeColor, eyeColor * (EyeGlow * Mathf.Max(0.25f, damageFlicker)));
            flickering = true;

            if (showSparks && collar != null && collar.gameObject.activeInHierarchy && time >= nextDamageSpark)
            {
                KillSpark.SpawnSmall(collar.position + Random.insideUnitSphere * 0.08f, Random.onUnitSphere + Vector3.up, Color.Lerp(accent, Color.white, 0.4f), Random.Range(2, 4));
                nextDamageSpark = time + Random.Range(0.5f, 1.4f) * Mathf.Lerp(1.2f, 0.4f, hurt);
            }
        }
        else if (flickering)
        {
            // Healed back up: steady lights again
            flickering = false;
            damageFlicker = 1f;
            Paint(lights, accent, accent * LightGlow);
            Paint(eyes, eyeColor, eyeColor * EyeGlow);
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

        // A capsule like the original body, but slimmer than the head, and with the same radius standing
        // and crouching: crouching only makes it shorter. Its top tucks up into the head, as before.
        float headRadius = head != null ? 0.5f * Mathf.Max(head.lossyScale.x, head.lossyScale.z) : 0.35f;
        float headY = head != null ? root.InverseTransformPoint(head.position).y : halfHeight + headRadius;
        float headBottom = headY - headRadius;
        const float thrusterSpace = 0.12f;
        float R = Mathf.Min(BodyRadius, radius);
        float shellBottom = bottom + thrusterSpace;
        float shellTop = Mathf.Min(halfHeight, headY);
        float height = Mathf.Max(2f * R, shellTop - shellBottom);
        shellTop = shellBottom + height;
        float cylinderTop = shellTop - R;

        Renderer egg = Part(root, "Shell", PrimitiveType.Capsule, ShellMaterial, new Vector3(0f, shellBottom + height * 0.5f, 0f), Vector3.one);
        egg.GetComponent<MeshFilter>().sharedMesh = CapsuleMesh(R, height);

        // Belt: a band round the bottom of the straight part
        float beltY = shellBottom + R + Mathf.Min(0.1f, (height - 2f * R) * 0.3f);
        float beltWidth = 2f * R + 0.02f;
        Renderer belt = Part(root, "Belt", PrimitiveType.Cylinder, TrimMaterial, new Vector3(0f, beltY, 0f), new Vector3(beltWidth, 0.025f, beltWidth));

        // Chest light: a flat round lamp on the front, between the belt and the head
        float chestY = beltY + Mathf.Min(0.16f, (headBottom - beltY) * 0.5f);
        float chestZ = BodyRadiusAt(chestY, R, cylinderTop, shellBottom + R);
        Renderer chest = Part(root, "Chest Light", PrimitiveType.Sphere, GlowMaterial, new Vector3(0f, chestY, chestZ - 0.01f), new Vector3(0.12f, 0.12f, 0.05f), false);

        // Thruster: a dark cap under the shell with a glowing nozzle
        Renderer housing = Part(root, "Thruster", PrimitiveType.Cylinder, TrimMaterial, new Vector3(0f, shellBottom + 0.03f, 0f), new Vector3(0.3f, 0.035f, 0.3f));
        thruster = Part(root, "Nozzle", PrimitiveType.Cylinder, GlowMaterial, new Vector3(0f, shellBottom - 0.005f, 0f), new Vector3(0.2f, 0.012f, 0.2f), false);

        // A glowing collar where the body meets the head
        float collarY = headBottom + 0.02f;
        float collarWidth = 2f * BodyRadiusAt(collarY, R, cylinderTop, shellBottom + R) + 0.03f;
        Renderer neck = Part(root, "Collar", PrimitiveType.Cylinder, GlowMaterial, new Vector3(0f, collarY, 0f), new Vector3(collarWidth, 0.01f, collarWidth), false);
        collar = neck.transform;

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

        // The face: eyes (and cheeks, for some faces) on the screen's surface. Built by BuildFace, so
        // a different face can be put on later (SetFace).
        faceRoot = root;
        faceCentre = visorCentre;
        faceRadii = visorRadii;
        faceSize = r;
        BuildFace(Faces.Classic);

        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            // Ear pods: round discs on the sides of the head, with a glowing dot in the middle
            Vector3 earPosition = new Vector3(side * 0.97f * r, -0.02f * r, 0f);
            accentList.Add(Part(root, "Ear", PrimitiveType.Cylinder, TrimMaterial, earPosition, new Vector3(0.45f * r, 0.1f * r, 0.45f * r), true, new Vector3(0f, 0f, 90f)));
            lightList.Add(Part(root, "Ear Light", PrimitiveType.Cylinder, GlowMaterial, earPosition, new Vector3(0.2f * r, 0.11f * r, 0.2f * r), false, new Vector3(0f, 0f, 90f)));
        }
    }

    #region Face

    // Where the face goes: the head's part root, the face screen's centre and radii, and the head's radius
    private Transform faceRoot;
    private Vector3 faceCentre, faceRadii;
    private float faceSize;
    // The face showing (see Faces), and the object its parts are under (rebuilt on a change)
    private int currentFace = -1;
    private Transform faceParts;
    // Parts drawn in the face screen's own dark colour (pupils, brows), over the glowing ones
    private Renderer[] darkParts;

    // Shows a different face (see Faces). A dead drone keeps its X-es.
    public void SetFace(int face)
    {
        if (!Faces.IsValid(face)) face = Faces.Classic;
        if (face == currentFace || faceRoot == null) return;
        BuildFace(face);
    }

    // Builds the eyes (and cheeks) for a face. Each eye is built round a pivot lying on the face
    // screen's surface, facing out of it (x across, y up, z out), and blinks by squashing that pivot.
    private void BuildFace(int face)
    {
        currentFace = face;
        if (faceParts != null)
        {
            if (Application.isPlaying) Destroy(faceParts.gameObject);
            else DestroyImmediate(faceParts.gameObject);
        }
        faceParts = new GameObject("Face Parts").transform;
        faceParts.gameObject.layer = faceRoot.gameObject.layer;
        faceParts.SetParent(faceRoot, false);

        var glow = new List<Renderer>();
        var dark = new List<Renderer>();
        var pivots = new List<Transform>();
        float r = faceSize;

        Transform Eye(float x, float y)
        {
            Vector3 at = OnVisor(new Vector2(x * r, y * r), faceCentre, faceRadii, 0.02f * r);
            Vector3 normal = Vector3.Scale(at - faceCentre, new Vector3(1f / (faceRadii.x * faceRadii.x), 1f / (faceRadii.y * faceRadii.y), 1f / (faceRadii.z * faceRadii.z))).normalized;
            Transform pivot = new GameObject("Eye").transform;
            pivot.gameObject.layer = faceParts.gameObject.layer;
            pivot.SetParent(faceParts, false);
            pivot.localPosition = at;
            pivot.localRotation = Quaternion.LookRotation(normal, Vector3.up);
            pivots.Add(pivot);
            return pivot;
        }
        // z: how far out of the screen (a glint sits in front of a pupil)
        void Lit(Transform eye, PrimitiveType type, float x, float y, Vector3 size, float roll = 0f, float z = 0f) =>
            glow.Add(Part(eye, "Glow", type, GlowMaterial, new Vector3(x * r, y * r, z * r), size * r, false, new Vector3(0f, 0f, roll)));
        void Shade(Transform eye, PrimitiveType type, float x, float y, Vector3 size, float roll = 0f) =>
            dark.Add(Part(eye, "Dark", type, VisorMaterial, new Vector3(x * r, y * r, 0.045f * r), size * r, false, new Vector3(0f, 0f, roll)));
        // An upturned V, like a smiling closed eye: two short bars meeting at the top
        void Chevron(Transform eye)
        {
            Lit(eye, PrimitiveType.Capsule, -0.065f, -0.02f, new Vector3(0.07f, 0.1f, 0.04f), -55f);
            Lit(eye, PrimitiveType.Capsule, 0.065f, -0.02f, new Vector3(0.07f, 0.1f, 0.04f), 55f);
        }
        Vector3 oval = new Vector3(0.2f, 0.3f, 0.08f);

        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            // Towards the middle of the face, from this eye
            float inner = -side;
            if (dead)
            {
                // An X of two crossed rounded bars for each eye
                Transform x = Eye(side * 0.24f, -0.06f);
                Lit(x, PrimitiveType.Capsule, 0f, 0f, new Vector3(0.075f, 0.17f, 0.04f), 45f);
                Lit(x, PrimitiveType.Capsule, 0f, 0f, new Vector3(0.075f, 0.17f, 0.04f), -45f);
                continue;
            }
            switch (face)
            {
                case Faces.Happy:
                    Chevron(Eye(side * 0.24f, -0.02f));
                    break;
                case Faces.Wink:
                    if (i == 0) Lit(Eye(side * 0.24f, -0.06f), PrimitiveType.Sphere, 0f, 0f, oval);
                    else Chevron(Eye(side * 0.24f, -0.02f));
                    break;
                case Faces.Sleepy:
                {
                    // Half shut: a low flat oval under a heavy lid
                    Transform eye = Eye(side * 0.24f, -0.1f);
                    Lit(eye, PrimitiveType.Sphere, 0f, -0.01f, new Vector3(0.22f, 0.12f, 0.08f));
                    Lit(eye, PrimitiveType.Capsule, 0f, 0.07f, new Vector3(0.035f, 0.13f, 0.03f), 90f);
                    break;
                }
                case Faces.Visor:
                    // One wide bar across the face: built once
                    if (i == 0) Lit(Eye(0f, -0.06f), PrimitiveType.Capsule, 0f, 0f, new Vector3(0.12f, 0.36f, 0.05f), 90f);
                    break;
                case Faces.Angry:
                {
                    // Ovals with their top inner corners cut off by a dark slanting brow
                    Transform eye = Eye(side * 0.24f, -0.06f);
                    Lit(eye, PrimitiveType.Sphere, 0f, 0f, oval);
                    Shade(eye, PrimitiveType.Cube, inner * 0.06f, 0.115f, new Vector3(0.34f, 0.14f, 0.05f), -inner * 32f);
                    break;
                }
                case Faces.Hearts:
                {
                    // Two round lobes over a point
                    Transform eye = Eye(side * 0.24f, -0.04f);
                    Lit(eye, PrimitiveType.Sphere, -0.055f, 0.04f, new Vector3(0.14f, 0.14f, 0.06f));
                    Lit(eye, PrimitiveType.Sphere, 0.055f, 0.04f, new Vector3(0.14f, 0.14f, 0.06f));
                    Lit(eye, PrimitiveType.Cube, 0f, -0.035f, new Vector3(0.135f, 0.135f, 0.05f), 45f);
                    break;
                }
                case Faces.Stars:
                {
                    // A four-pointed star, with shorter points between
                    Transform eye = Eye(side * 0.24f, -0.05f);
                    Lit(eye, PrimitiveType.Sphere, 0f, 0f, new Vector3(0.08f, 0.34f, 0.05f));
                    Lit(eye, PrimitiveType.Sphere, 0f, 0f, new Vector3(0.08f, 0.34f, 0.05f), 90f);
                    Lit(eye, PrimitiveType.Sphere, 0f, 0f, new Vector3(0.05f, 0.18f, 0.05f), 45f);
                    Lit(eye, PrimitiveType.Sphere, 0f, 0f, new Vector3(0.05f, 0.18f, 0.05f), -45f);
                    break;
                }
                case Faces.Cyclops:
                {
                    // One big eye in the middle, with a dark pupil and a glint: built once
                    if (i != 0) break;
                    Transform eye = Eye(0f, -0.06f);
                    Lit(eye, PrimitiveType.Sphere, 0f, 0f, new Vector3(0.36f, 0.42f, 0.08f));
                    Shade(eye, PrimitiveType.Sphere, 0f, -0.03f, new Vector3(0.16f, 0.2f, 0.04f));
                    Lit(eye, PrimitiveType.Sphere, 0.05f, 0.03f, new Vector3(0.06f, 0.06f, 0.03f), 0f, 0.075f);
                    break;
                }
                case Faces.Dots:
                    // Small round eyes, and a little mouth below them (built once)
                    Lit(Eye(side * 0.2f, -0.02f), PrimitiveType.Sphere, 0f, 0f, new Vector3(0.12f, 0.13f, 0.06f));
                    if (i == 0)
                    {
                        Transform mouth = Eye(0f, -0.25f);
                        pivots.Remove(mouth); // the mouth doesn't blink
                        Lit(mouth, PrimitiveType.Capsule, 0f, 0f, new Vector3(0.035f, 0.07f, 0.03f), 90f);
                    }
                    break;
                case Faces.Sparkle:
                {
                    // Big shiny eyes: a dark pupil low in each, with two glints
                    Transform eye = Eye(side * 0.24f, -0.06f);
                    Lit(eye, PrimitiveType.Sphere, 0f, 0f, new Vector3(0.22f, 0.32f, 0.08f));
                    Shade(eye, PrimitiveType.Sphere, 0f, -0.03f, new Vector3(0.13f, 0.18f, 0.04f));
                    Lit(eye, PrimitiveType.Sphere, 0.035f, 0.035f, new Vector3(0.06f, 0.06f, 0.03f), 0f, 0.075f);
                    Lit(eye, PrimitiveType.Sphere, -0.035f, -0.08f, new Vector3(0.03f, 0.03f, 0.03f), 0f, 0.075f);
                    break;
                }
                default:
                    // Classic and Blushing: the two big ovals
                    Lit(Eye(side * 0.24f, -0.06f), PrimitiveType.Sphere, 0f, 0f, oval);
                    break;
            }
        }

        // Pink cheeks under the eyes
        var cheeks = new List<Renderer>();
        if (Faces.HasCheeks(face))
        {
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector3 at = OnVisor(new Vector2(side * 0.44f * r, -0.3f * r), faceCentre, faceRadii, 0.01f * r);
                cheeks.Add(Part(faceParts, "Blush", PrimitiveType.Sphere, GlowMaterial, at, new Vector3(0.14f * r, 0.07f * r, 0.04f * r), false));
            }
        }

        eyes = glow.ToArray();
        darkParts = dark.ToArray();
        blush = cheeks.ToArray();
        eyeTransforms = pivots.ToArray();
        // The pivots blink, so their own scale is the one to squash
        eyeScale = Vector3.one;

        if (block == null) block = new MaterialPropertyBlock();
        Paint(eyes, eyeColor, eyeColor * EyeGlow);
        Paint(darkParts, VisorColor, Color.black);
        Paint(blush, BlushColor, dead ? Color.black : BlushColor * 0.6f);
    }

    #endregion

    // Half the width of the body capsule at height y (bottomCentre/topCentre: the centres of its round ends)
    private static float BodyRadiusAt(float y, float radius, float topCentre, float bottomCentre)
    {
        float d = y > topCentre ? y - topCentre : (y < bottomCentre ? bottomCentre - y : 0f);
        return Mathf.Sqrt(Mathf.Max(0f, radius * radius - d * d));
    }

    private static readonly Dictionary<Vector2, Mesh> capsules = new Dictionary<Vector2, Mesh>();

    // A capsule of the given radius and total height, centred on the origin. Unity's capsule can only be
    // scaled, which squashes its round ends when it's made shorter than it is wide.
    private static Mesh CapsuleMesh(float radius, float height)
    {
        Vector2 key = new Vector2(Mathf.Round(radius * 1000f), Mathf.Round(height * 1000f));
        if (capsules.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        const int segments = 32, capRings = 10;
        float half = Mathf.Max(0f, height * 0.5f - radius);
        // Rings from the bottom pole to the top pole; the two at the middle of the run are the straight part
        int rings = capRings * 2 + 1;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        for (int i = 0; i <= rings; i++)
        {
            float angle; // from -90 (bottom pole) to 90 (top pole) degrees
            float offset;
            if (i <= capRings) { angle = -90f + 90f * i / capRings; offset = -half; }
            else { angle = 90f * (i - capRings - 1) / capRings; offset = half; }
            float a = angle * Mathf.Deg2Rad;
            float ringRadius = Mathf.Cos(a), y = Mathf.Sin(a);
            for (int j = 0; j <= segments; j++)
            {
                float around = 2f * Mathf.PI * j / segments;
                Vector3 n = new Vector3(Mathf.Sin(around) * ringRadius, y, Mathf.Cos(around) * ringRadius);
                vertices.Add(n * radius + Vector3.up * offset);
                normals.Add(n);
                uvs.Add(new Vector2((float)j / segments, (float)i / rings));
            }
        }
        var triangles = new List<int>();
        for (int i = 0; i < rings; i++)
        {
            for (int j = 0; j < segments; j++)
            {
                int a0 = i * (segments + 1) + j, a1 = a0 + 1, b0 = a0 + segments + 1, b1 = b0 + 1;
                // Wound so the outside faces outwards
                triangles.Add(a0); triangles.Add(a1); triangles.Add(b0);
                triangles.Add(a1); triangles.Add(b1); triangles.Add(b0);
            }
        }

        Mesh mesh = new Mesh { name = "Drone Capsule" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        capsules[key] = mesh;
        return mesh;
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

    // Glossy, like lacquered plastic
    internal static Material ShellMaterial => shellMaterial != null ? shellMaterial : (shellMaterial = Lit("Drone Shell", 0.82f));
    // Satin metal, for the trim (belt, ear pods, thruster housing), in its own colours
    internal static Material TrimMaterial => trimMaterial != null ? trimMaterial : (trimMaterial = Lit("Drone Trim", 0.6f, 0.75f));
    internal static Material VisorMaterial => visorMaterial != null ? visorMaterial : (visorMaterial = Lit("Drone Visor", 0.92f));

    // Glowing parts use a material asset with emission switched on, so builds keep the shader variant
    // that glows (a material made in code can't ask for one that was left out)
    internal static Material GlowMaterial
    {
        get
        {
            if (glowMaterial != null) return glowMaterial;
            Material asset = Resources.Load<Material>("DroneGlow");
            glowMaterial = asset != null ? asset : Lit("Drone Glow", 0.5f);
            return glowMaterial;
        }
    }

    private static Material Lit(string name, float smoothness, float metallic = 0f)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        // The same shader the cap and the original body use, which every build includes
        Material material = lit != null ? new Material(lit) : new Material(PlayerCap.SharedMaterial(null));
        material.name = name;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        return material;
    }

    #endregion
}
