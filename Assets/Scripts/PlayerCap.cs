using UnityEngine;

// The hat every player wears (see Hats for the list): the baseball cap to start with, and more as
// they level up. Built from primitives and a few generated meshes. Each part is made of a surface
// (Surface): cotton twill, knit, felt, satin, glossy plastic, metal or hammered metal, each a
// material in Resources/Hats with its own shine and a tiling normal map (Tools/SurfaceTextures),
// clear glass for the fishbowl, or a glow for the halo and other glowing bits. It sits on the
// head, so on a dead player's ragdoll it flies off together with the head (or on its own, on a
// headshot). Every hat is drawn in shades of one colour: the team colour in Team Deathmatch,
// otherwise a colour picked from the player's actor number, so every client shows the same colour
// for the same player without sending anything.
public class PlayerCap : MonoBehaviour
{
    // Which shade of the hat's colour a part is drawn in
    private enum Shade { Main, Dark, Light }

    // What a part is made of: a material in Resources/Hats ("Hat" + the name), or the glow
    private enum Surface { Twill, Knit, Felt, Satin, Plastic, Metal, Hammered, Glass, Glow }
    private static readonly System.Collections.Generic.Dictionary<Surface, Material> surfaceMaterials = new System.Collections.Generic.Dictionary<Surface, Material>();
    // How brightly a glowing part (the halo) glows, in its light shade (HatMotion pulses it from this)
    internal const float GlowStrength = 1.8f;
    // How see-through glass is: its colour's alpha
    private const float GlassAlpha = 0.2f;
    // A locked hat's preview (ShowLocked): see-through grey
    private static readonly Color LockedGrey = new Color(0.62f, 0.62f, 0.64f);
    private const float LockedAlpha = 0.35f;
    private bool locked;
    public bool IsLocked => locked;

    private Renderer[] renderers;
    private Shade[] shades;
    private Surface[] surfaces;
    private float[] glows;
    private static Material capMaterial;

    // The hat this is (see Hats)
    public int Hat { get; private set; }

    // While building: the parts so far, and whether they get colliders (a ragdoll's hat)
    private readonly System.Collections.Generic.List<Renderer> parts = new System.Collections.Generic.List<Renderer>();
    private readonly System.Collections.Generic.List<Shade> partShades = new System.Collections.Generic.List<Shade>();
    private readonly System.Collections.Generic.List<Surface> partSurfaces = new System.Collections.Generic.List<Surface>();
    private readonly System.Collections.Generic.List<float> partGlows = new System.Collections.Generic.List<float>();
    // What the parts being built are made of (set by each hat as it builds)
    private Surface surface = Surface.Twill;
    // How much brighter than usual glowing parts being built glow (the halo glows the most)
    private float glowBoost = 1f;
    private bool solid;
    // The head's radius (metres), while building
    private float headRadius;
    // The meshes of this hat's prints, made for it alone (destroyed with it)
    private readonly System.Collections.Generic.List<Mesh> printMeshes = new System.Collections.Generic.List<Mesh>();

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
        cap.surfaces = cap.partSurfaces.ToArray();
        cap.glows = cap.partGlows.ToArray();
        cap.parts.Clear();
        cap.partShades.Clear();
        cap.partSurfaces.Clear();
        cap.partGlows.Clear();
        cap.SetColor(color);
        return cap;
    }

    public void SetColor(Color color)
    {
        if (renderers == null) return;
        if (locked) color = LockedGrey;
        Color dark = Color.Lerp(color, Color.black, 0.35f);
        Color light = Color.Lerp(color, Color.white, 0.45f);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color c = shades[i] == Shade.Dark ? dark : shades[i] == Shade.Light ? light : color;
            Surface kind = surfaces != null ? surfaces[i] : Surface.Twill;
            if (locked) c.a = LockedAlpha;
            else if (kind == Surface.Glass) c.a = GlassAlpha;
            renderers[i].GetPropertyBlock(block);
            block.SetColor("_BaseColor", c); // URP
            block.SetColor("_Color", c);     // built-in shaders
            block.SetColor("_EmissionColor", kind == Surface.Glow && !locked ? c * (GlowStrength * GlowOf(i)) : Color.black);
            renderers[i].SetPropertyBlock(block);
        }
    }

    private float GlowOf(int part) => glows != null && part < glows.Length ? glows[part] : 1f;

    // How much brighter than usual this part glows (1 for most; HatMotion's pulses start from it)
    internal float GlowOf(Renderer part)
    {
        if (renderers == null) return 1f;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == part) return GlowOf(i);
        }
        return 1f;
    }

    // Shows the hat as a see-through grey ghost of itself, without its glow: the Customize menu's
    // preview of a hat that isn't unlocked yet. Its moving parts still move.
    public void ShowLocked()
    {
        if (renderers == null || renderers.Length == 0) return;
        locked = true;
        Material glass = SurfaceMaterial(Surface.Glass, renderers[0].sharedMaterial);
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            r.sharedMaterial = glass;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        SetColor(LockedGrey);
    }

    #region Hats

    // The hat's parts, around the centre of a head of radius r (metres). +z is the way the head faces.
    private void Build(int hat, float r)
    {
        headRadius = r;
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
            case Hats.Headset: BuildHeadset(r); break;
            case Hats.TrafficCone: BuildTrafficCone(r); break;
            case Hats.Toadstool: BuildToadstool(r); break;
            case Hats.Antenna: BuildAntenna(r); break;
            case Hats.Plunger: BuildPlunger(r); break;
            case Hats.CowboyHat: BuildCowboyHat(r); break;
            case Hats.Sombrero: BuildSombrero(r); break;
            case Hats.PirateHat: BuildPirateHat(r); break;
            case Hats.Fishbowl: BuildFishbowl(r); break;
            case Hats.WizardHat: BuildWizardHat(r); break;
            case Hats.DevilHorns: BuildDevilHorns(r); break;
            case Hats.GraduationCap: BuildGraduationCap(r); break;
            case Hats.MilitaryBeret: BuildMilitaryBeret(r); break;
            case Hats.BirthdayCake: BuildBirthdayCake(r); break;
            case Hats.Siren: BuildSiren(r); break;
            case Hats.RadarDish: BuildRadarDish(r); break;
            case Hats.JesterHat: BuildJesterHat(r); break;
            case Hats.PlasmaGlobe: BuildPlasmaGlobe(r); break;
            case Hats.Ufo: BuildUfo(r); break;
            case Hats.Atom: BuildAtom(r); break;
            default: BuildCap(r, 1f); break;
        }
    }

    // A baseball cap: a crown hugging the top of the head, a brim (at the front, or the back when
    // facing is -1) and a button on top
    private void BuildCap(float r, float facing)
    {
        surface = Surface.Twill;
        Crown(Vector3.one * (1.07f * r));
        // The six panels' seams, running over the top from the button
        Seams(r, 1.07f, 3, Shade.Dark);
        // Brim: a thin flat disc sticking out from the crown's edge, tipped slightly down, with a
        // line of stitching round it
        Primitive("Brim", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, 0.25f * r, 1.05f * r * facing), new Vector3(1.4f * r, 0.035f * r, 1.4f * r), new Vector3(8f * facing, 0f, 0f), true);
        Generated("Stitching", Torus(0.025f), Shade.Light, new Vector3(0f, 0.25f * r, 1.05f * r * facing) + Quaternion.Euler(8f * facing, 0f, 0f) * new Vector3(0f, 0.04f * r, 0f), new Vector3(0.58f * r, 0.58f * r, 0.58f * r), new Vector3(8f * facing, 0f, 0f), false);
        Primitive("Button", PrimitiveType.Sphere, Shade.Dark, new Vector3(0f, 1.08f * r, 0f), new Vector3(0.2f * r, 0.12f * r, 0.2f * r), Vector3.zero, false);
        // A round badge printed on the front panel, with a darker middle
        SurfacePrint.Map panel = OnEllipsoid(Vector3.zero, Vector3.one * (1.07f * r), Quaternion.Euler(48f * facing, 0f, 0f) * Vector3.up);
        Print("Badge", Shade.Light, panel, SurfacePrint.Ellipse(0.36f, 0.36f));
        Print("Badge Middle", Shade.Dark, panel, SurfacePrint.Ellipse(0.18f, 0.18f), 1);
        // At the back: the arched gap over the strap, and the strap across it
        Vector3 back = new Vector3(0f, 0f, -facing);
        Primitive("Gap", PrimitiveType.Sphere, Shade.Dark, new Vector3(0f, 0.23f * r, 0f) + back * (1.02f * r), new Vector3(0.44f * r, 0.3f * r, 0.08f * r), Vector3.zero, false);
        Primitive("Strap", PrimitiveType.Cube, Shade.Main, new Vector3(0f, 0.27f * r, 0f) + back * (1.04f * r), new Vector3(0.5f * r, 0.06f * r, 0.03f * r), Vector3.zero, false);
        surface = Surface.Metal;
        Primitive("Snap", PrimitiveType.Cylinder, Shade.Light, new Vector3(0f, 0.27f * r, 0f) + back * (1.06f * r), new Vector3(0.07f * r, 0.01f * r, 0.07f * r), new Vector3(90f, 0f, 0f), false);
    }

    // A knitted beanie: a taller crown with ribbed knitting, a turned-up ribbed cuff round the
    // bottom and a fluffy pom-pom that bounces as the player moves
    private void BuildBeanie(float r)
    {
        surface = Surface.Knit;
        Crown(new Vector3(1.08f * r, 1.22f * r, 1.08f * r));
        Line(Generated("Cuff", Frustum(0.97f, false), Shade.Dark, new Vector3(0f, 0.12f * r, 0f), new Vector3(1.12f * r, 0.3f * r, 1.12f * r), Vector3.zero, true), Frustum(0.97f, false, true));
        Ribs("Rib", Shade.Dark, 0.12f * r, 1.12f * r, 0.97f, 0.3f * r, 22, 0.05f * r);
        Generated("Cuff Edge", Torus(0.07f), Shade.Main, new Vector3(0f, 0.42f * r, 0f), Vector3.one * (1.09f * r), Vector3.zero, false);
        Seams(r * 1.0f, 1.09f, 4, Shade.Main, 1.22f / 1.09f);

        Transform pom = Pivot("Pom-pom", new Vector3(0f, 1.18f * r, 0f));
        HatDangle.Add(pom.gameObject, Vector3.up, 0.18f * r, 600f, 0.15f, 6f);
        Primitive("Pom", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, 0.2f * r, 0f), Vector3.one * (0.46f * r), Vector3.zero, false, pom);
        // Tufts all round it, so it looks fluffy rather than a ball
        for (int i = 0; i < 10; i++)
        {
            Vector3 way = Fibonacci(i, 10);
            Primitive("Tuft", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, 0.2f * r, 0f) + way * (0.18f * r), Vector3.one * (0.2f * r), Vector3.zero, false, pom);
        }
        StillBox(new Vector3(0f, 1.38f * r, 0f), Vector3.one * (0.42f * r));
    }

    // A bucket hat: a flat-topped crown narrowing upwards and a brim sloping down all round, with rows
    // of stitching round the brim and a little metal eyelet on each side
    private void BuildBucketHat(float r)
    {
        surface = Surface.Twill;
        Line(Generated("Crown", Frustum(0.82f, true), Shade.Main, new Vector3(0f, 0.18f * r, 0f), new Vector3(1.08f * r, 0.92f * r, 1.08f * r), Vector3.zero, true), Frustum(0.82f, true, true));
        Generated("Band", Frustum(0.96f, false), Shade.Dark, new Vector3(0f, 0.24f * r, 0f), new Vector3(1.09f * r, 0.16f * r, 1.09f * r), Vector3.zero, false);
        Line(Generated("Brim", Frustum(0.68f, false), Shade.Dark, new Vector3(0f, 0.02f * r, 0f), new Vector3(1.6f * r, 0.22f * r, 1.6f * r), Vector3.zero, true), Frustum(0.68f, false, true));
        // Stitching: rings sitting on the sloping brim, and two round the top of the crown
        float[] rows = { 0.18f, 0.42f, 0.66f };
        foreach (float f in rows)
        {
            Generated("Stitching", Torus(0.012f), Shade.Light, new Vector3(0f, (0.02f + 0.22f * f + 0.012f) * r, 0f), Vector3.one * (1.6f * (1f - 0.32f * f) * r), Vector3.zero, false);
        }
        Generated("Stitching", Torus(0.012f), Shade.Light, new Vector3(0f, (0.18f + 0.8f) * r, 0f), Vector3.one * (1.08f * (1f - 0.18f * 0.8f / 0.92f) * 1.01f * r), Vector3.zero, false);
        Generated("Top Stitching", Torus(0.016f), Shade.Light, new Vector3(0f, 1.105f * r, 0f), Vector3.one * (0.6f * r), Vector3.zero, false);
        surface = Surface.Metal;
        for (int side = -1; side <= 1; side += 2)
        {
            Generated("Eyelet", Torus(0.4f), Shade.Light, new Vector3(0.98f * r * side, 0.72f * r, 0f), Vector3.one * (0.06f * r), new Vector3(0f, 0f, 90f - 12f * side), false);
        }
    }

    // A builder's hard hat: a domed shell, a ridge from front to back, a flat brim, longer at the
    // front, and a headlamp on the front whose light shimmers a little
    private void BuildHardHat(float r)
    {
        surface = Surface.Plastic;
        Crown(new Vector3(1.1f * r, 1.15f * r, 1.18f * r));
        Generated("Ridge", Dome(), Shade.Dark, Vector3.zero, new Vector3(0.24f * r, 1.21f * r, 1.24f * r), Vector3.zero, false);
        for (int side = -1; side <= 1; side += 2)
        {
            Generated("Side Ridge", Dome(), Shade.Main, Vector3.zero, new Vector3(0.08f * r, 1.17f * r, 1.2f * r), new Vector3(0f, 0f, 24f * side), false);
        }
        Primitive("Brim", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, 0.23f * r, 0.12f * r), new Vector3(2.5f * r, 0.03f * r, 2.7f * r), Vector3.zero, true);

        // The headlamp: a dark housing on the front, tipped up a little, with a glowing lens
        Vector3 lampAt = new Vector3(0f, 0.56f * r, 1.04f * r);
        Vector3 lampEuler = new Vector3(70f, 0f, 0f);
        Vector3 lampAxis = Quaternion.Euler(lampEuler) * Vector3.up;
        Primitive("Lamp Mount", PrimitiveType.Cube, Shade.Dark, lampAt - lampAxis * (0.04f * r), new Vector3(0.36f * r, 0.08f * r, 0.12f * r), lampEuler, false);
        surface = Surface.Metal;
        Primitive("Lamp", PrimitiveType.Cylinder, Shade.Dark, lampAt + lampAxis * (0.04f * r), new Vector3(0.28f * r, 0.06f * r, 0.28f * r), lampEuler, false);
        surface = Surface.Glow;
        Renderer lens = Primitive("Lens", PrimitiveType.Cylinder, Shade.Light, lampAt + lampAxis * (0.1f * r), new Vector3(0.22f * r, 0.01f * r, 0.22f * r), lampEuler, false);
        HatMotion.Add(lens.gameObject, HatMotion.Kind.Pulse, Vector3.up, 0.4f, 0.25f);
    }

    // A propeller beanie: a cap with a short brim and a propeller on top that spins faster the
    // faster the player moves
    private void BuildPropellerBeanie(float r)
    {
        surface = Surface.Twill;
        Crown(Vector3.one * (1.07f * r));
        // Panels in two shades taking turns, like a real one: a light wedge over every other panel
        for (int i = 0; i < 3; i++)
        {
            Generated("Panel", DomeSlice(60f), Shade.Light, Vector3.zero, Vector3.one * (1.074f * r), new Vector3(0f, 120f * i, 0f), false);
        }
        Seams(r, 1.08f, 3, Shade.Dark);
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
        // Two rounded blades, each twisted a little like a real propeller, in different shades
        for (int side = -1; side <= 1; side += 2)
        {
            Primitive("Blade", PrimitiveType.Sphere, side < 0 ? Shade.Main : Shade.Light, new Vector3(0.5f * r * side, 0f, 0f), new Vector3(0.95f * r, 0.035f * r, 0.26f * r), new Vector3(12f * side, 0f, 0f), false, rotor.transform);
        }

        // On a ragdoll the propeller gets a collider too, or a head landing upside down would sink
        // it into the floor. Not on the blades themselves: they spin, and a collider turning inside
        // a rigidbody every frame makes it jitter. Instead a thin, still disc as wide as the blades
        // sweep, and the stem under it, so the hat rests on its propeller like a real one would.
        if (solid)
        {
            GameObject disc = new GameObject("Propeller Collider");
            disc.layer = gameObject.layer;
            disc.transform.SetParent(transform, false);
            disc.transform.localPosition = rotor.transform.localPosition;
            MeshCollider discCollider = disc.AddComponent<MeshCollider>();
            discCollider.sharedMesh = PropellerDisc();
            discCollider.convex = true;
            disc.transform.localScale = new Vector3(1.95f * r, 0.08f * r, 1.95f * r);
            BoxCollider stem = gameObject.AddComponent<BoxCollider>();
            stem.center = new Vector3(0f, 1.2f * r, 0f);
            stem.size = new Vector3(0.12f * r, 0.2f * r, 0.12f * r);
        }
    }

    // A flat 12-sided disc 1 across and 1 tall, for the propeller's collider
    private static Mesh propellerDisc;
    private static Mesh PropellerDisc()
    {
        if (propellerDisc != null) return propellerDisc;
        const int sides = 12;
        Vector3[] vertices = new Vector3[sides * 2];
        int[] triangles = new int[(sides - 2) * 6 + sides * 6];
        int t = 0;
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            Vector3 rim = new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f);
            vertices[i] = rim + Vector3.up * 0.5f;
            vertices[i + sides] = rim - Vector3.up * 0.5f;
        }
        for (int i = 1; i < sides - 1; i++)
        {
            triangles[t++] = 0; triangles[t++] = i + 1; triangles[t++] = i;
            triangles[t++] = sides; triangles[t++] = sides + i; triangles[t++] = sides + i + 1;
        }
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            triangles[t++] = i; triangles[t++] = j; triangles[t++] = i + sides;
            triangles[t++] = j; triangles[t++] = j + sides; triangles[t++] = i + sides;
        }
        propellerDisc = new Mesh { name = "Propeller Disc Collider", vertices = vertices, triangles = triangles };
        return propellerDisc;
    }

    // A top hat, sitting up on the head: a tall crown with a band, on a flat brim
    private void BuildTopHat(float r)
    {
        const float sit = 0.36f; // where the brim rests, as a fraction of the head's radius: just above the face
        const float radius = 0.96f, height = 1.05f, flare = 1.08f;
        surface = Surface.Felt;
        // The brim curls up gently towards its edge, more at the sides than front and back, and its
        // edge is rolled: one closed piece all the way round
        BrimShape brim = new BrimShape { innerX = 0.94f, innerZ = 0.94f, outerX = 1.42f, outerZ = 1.36f, sideCurl = 0.2f, frontCurl = 0.05f, power = 2.2f, thickness = 0.03f };
        Generated("Brim", BrimMesh(brim), Shade.Dark, new Vector3(0f, sit * r, 0f), Vector3.one * r, Vector3.zero, true);
        Generated("Brim Roll", BrimEdge(brim, 0.035f), Shade.Dark, new Vector3(0f, sit * r, 0f), Vector3.one * r, Vector3.zero, false);
        // The crown flares out a little towards its flat top
        Line(Generated("Crown", Frustum(flare, true), Shade.Main, new Vector3(0f, sit * r, 0f), new Vector3(radius * r, height * r, radius * r), Vector3.zero, true), Frustum(flare, true, true));
        Generated("Top Edge", Torus(0.04f), Shade.Main, new Vector3(0f, (sit + height) * r, 0f), Vector3.one * (radius * flare * 0.99f * r), Vector3.zero, false);
        surface = Surface.Satin;
        Stripe("Band", Shade.Dark, sit * r, radius * r, flare, height * r, 0.02f * r, 0.26f * r);
        // A bow on the band's left side
        Vector3 bow = new Vector3(-radius * 1.03f * r, (sit + 0.14f) * r, 0.05f * r);
        Primitive("Bow Knot", PrimitiveType.Sphere, Shade.Dark, bow, new Vector3(0.06f * r, 0.17f * r, 0.12f * r), Vector3.zero, false);
        for (int side = -1; side <= 1; side += 2)
        {
            Primitive("Bow Loop", PrimitiveType.Sphere, Shade.Dark, bow + new Vector3(0.01f * r, 0f, 0.13f * r * side), new Vector3(0.05f * r, 0.17f * r, 0.22f * r), new Vector3(20f * side, 0f, 0f), false);
        }
        // A playing card tucked into the band on the right, leaning back
        surface = Surface.Plastic;
        Vector3 card = new Vector3(radius * 1.04f * r, (sit + 0.32f) * r, -0.12f * r);
        Primitive("Card", PrimitiveType.Cube, Shade.Light, card, new Vector3(0.012f * r, 0.36f * r, 0.26f * r), new Vector3(-14f, 0f, -6f), false);
        Primitive("Card Mark", PrimitiveType.Sphere, Shade.Dark, card + new Vector3(0.012f * r, 0.02f * r, 0f), new Vector3(0.01f * r, 0.1f * r, 0.08f * r), new Vector3(-14f, 0f, 45f - 6f), false);
    }

    // A horned helmet: a round hammered helmet with a riveted rim, straps over the top, and a curved
    // horn growing out and up from each side out of a metal collar
    private void BuildVikingHelmet(float r)
    {
        // A hammered metal helmet with smooth metal trim, and horns of polished horn
        surface = Surface.Hammered;
        Crown(Vector3.one * (1.08f * r));
        surface = Surface.Metal;
        Line(Generated("Rim", Frustum(1f, false), Shade.Dark, new Vector3(0f, 0.12f * r, 0f), new Vector3(1.11f * r, 0.16f * r, 1.11f * r), Vector3.zero, true), Frustum(1f, false, true));
        Generated("Rim Edge", Torus(0.05f), Shade.Dark, new Vector3(0f, 0.28f * r, 0f), Vector3.one * (1.1f * r), Vector3.zero, false);
        // Straps from front to back and side to side, crossing on top: strips on the helmet's surface
        for (int i = 0; i < 4; i++)
        {
            Generated("Strap", DomeSlice(i % 2 == 0 ? 7f : 5f), Shade.Dark, Vector3.zero, Vector3.one * (1.1f * r), new Vector3(0f, 90f * i - (i % 2 == 0 ? 3.5f : 2.5f), 0f), false);
        }
        Primitive("Boss", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, 1.12f * r, 0f), new Vector3(0.18f * r, 0.1f * r, 0.18f * r), Vector3.zero, false);
        // Rivets round the rim
        const int rivets = 14;
        for (int i = 0; i < rivets; i++)
        {
            Quaternion around = Quaternion.Euler(0f, 360f * (i + 0.5f) / rivets, 0f);
            Primitive("Rivet", PrimitiveType.Sphere, Shade.Light, around * Vector3.forward * (1.11f * r) + Vector3.up * (0.2f * r), Vector3.one * (0.06f * r), Vector3.zero, false);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 root = new Vector3(0.9f * side, 0.42f, 0f).normalized * (1.04f * r);
            surface = Surface.Metal;
            Primitive("Horn Collar", PrimitiveType.Cylinder, Shade.Dark, root, new Vector3(0.38f * r, 0.05f * r, 0.38f * r), new Vector3(0f, 0f, -70f * side), false);
            // Out to the side, then curving up and a little forward
            surface = Surface.Plastic;
            Vector3[] ways = { new Vector3(side, 0.35f, 0f), new Vector3(side, 0.9f, 0.05f), new Vector3(0.45f * side, 1f, 0.15f), new Vector3(0f, 1f, 0.25f) };
            float[] lengths = { 0.26f * r, 0.24f * r, 0.22f * r, 0.2f * r };
            Horn("Horn", root - new Vector3(0.05f * r * side, 0f, 0f), ways, lengths, 0.17f * r, Shade.Light, true);
        }
    }

    // A royal crown: a velvet cap bulging up inside a tall metal band with a rolled rim at the top
    // and bottom and square gems round it, and flat points splaying out from its top edge: broad
    // blades with an oval gem and a three-lobed tip, between tall thin spikes
    private void BuildCrown(float r)
    {
        const float bottom = 0.3f, height = 0.38f, radius = 0.98f, flare = 1.03f;
        const int points = 8;
        float top = (bottom + height) * r;
        float rim = radius * flare * r;

        // The velvet cap, in the dark shade, filling the band and bulging up above it
        surface = Surface.Felt;
        Generated("Cap", Dome(), Shade.Dark, new Vector3(0f, bottom * r, 0f), new Vector3(0.95f * r, 0.86f * r, 0.95f * r), Vector3.zero, false);

        // The band, metal in the player's colour, with a rolled rim round its top and bottom edges
        surface = Surface.Metal;
        Line(Generated("Band", Frustum(flare, false), Shade.Main, new Vector3(0f, bottom * r, 0f), new Vector3(radius * r, height * r, radius * r), Vector3.zero, true), Frustum(flare, false, true));
        Generated("Rim", Torus(0.05f), Shade.Dark, new Vector3(0f, bottom * r, 0f), Vector3.one * (radius * 1.01f * r), Vector3.zero, false);
        Generated("Rim", Torus(0.05f), Shade.Dark, new Vector3(0f, top, 0f), Vector3.one * (rim * 1.01f), Vector3.zero, false);

        for (int i = 0; i < points; i++)
        {
            float angle = 360f * i / points;
            Quaternion around = Quaternion.Euler(0f, angle, 0f);
            Vector3 outward = around * Vector3.forward;
            Vector3 sideways = around * Vector3.right;

            // A square gem on the band between each pair of points, set on a corner
            surface = Surface.Plastic;
            Quaternion between = Quaternion.Euler(0f, angle + 180f / points, 0f);
            Vector3 gemAt = between * Vector3.forward * (radius * (1f + (flare - 1f) * 0.5f) * r) + Vector3.up * ((bottom + height * 0.5f) * r);
            Primitive("Gem", PrimitiveType.Cube, Shade.Light, gemAt, new Vector3(0.12f * r, 0.12f * r, 0.05f * r), (between * Quaternion.Euler(0f, 0f, 45f)).eulerAngles, false);

            // The points: flattened cones standing on the band's top edge, splaying outwards
            surface = Surface.Metal;
            bool blade = i % 2 == 0;
            Quaternion tilt = around * Quaternion.Euler(blade ? 8f : 12f, 0f, 0f);
            Vector3 basePoint = outward * (rim * 0.98f) + Vector3.up * (top - 0.03f * r);
            Vector3 up = tilt * Vector3.up, front = tilt * Vector3.forward;
            if (blade)
            {
                const float tall = 0.5f, wide = 0.16f, deep = 0.045f, taper = 0.2f;
                Generated("Point", Frustum(taper, false), Shade.Main, basePoint, new Vector3(wide * r, tall * r, deep * r), tilt.eulerAngles, false);
                // An oval gem low on the blade's face
                surface = Surface.Plastic;
                float faceDepth = deep * (1f - (1f - taper) * 0.3f) * r;
                Primitive("Gem", PrimitiveType.Sphere, Shade.Light, basePoint + up * (0.15f * r) + front * faceDepth, new Vector3(0.065f * r, 0.13f * r, 0.04f * r), tilt.eulerAngles, false);
                // A three-lobed tip: a tall middle leaf, a smaller one each side, and a gem in its middle
                surface = Surface.Metal;
                Vector3 tip = basePoint + up * (tall * r);
                Primitive("Tip", PrimitiveType.Sphere, Shade.Main, tip + up * (0.07f * r), new Vector3(0.12f * r, 0.2f * r, 0.045f * r), tilt.eulerAngles, false);
                for (int s = -1; s <= 1; s += 2)
                {
                    Primitive("Leaf", PrimitiveType.Sphere, Shade.Main, tip + up * (0.03f * r) + sideways * (0.08f * r * s), new Vector3(0.1f * r, 0.07f * r, 0.04f * r), (tilt * Quaternion.Euler(0f, 0f, -30f * s)).eulerAngles, false);
                }
                // The tip's gem glints now and then
                surface = Surface.Glow;
                Renderer glint = Primitive("Gem", PrimitiveType.Sphere, Shade.Light, tip + up * (0.07f * r) + front * (0.02f * r), new Vector3(0.06f * r, 0.08f * r, 0.03f * r), tilt.eulerAngles, false);
                HatMotion.Add(glint.gameObject, HatMotion.Kind.Twinkle, Vector3.up, 0.6f, 0.6f);
            }
            else
            {
                const float tall = 0.64f;
                Generated("Spike", Frustum(0.3f, false), Shade.Main, basePoint, new Vector3(0.055f * r, tall * r, 0.035f * r), tilt.eulerAngles, false);
                Primitive("Tip", PrimitiveType.Sphere, Shade.Main, basePoint + up * ((tall + 0.04f) * r), new Vector3(0.07f * r, 0.13f * r, 0.035f * r), tilt.eulerAngles, false);
            }
        }
    }

    // A halo: a glowing ring floating above the head, bobbing gently and rocking a little, with its
    // glow slowly breathing and a few glints running round it
    private void BuildHalo(float r)
    {
        surface = Surface.Glow;
        // The brightest glow of any hat: it should light up the screen's bloom
        glowBoost = 3f;
        if (solid)
        {
            Generated("Ring", Torus(0.24f), Shade.Light, new Vector3(0f, 1.42f * r, 0f), Vector3.one * (0.68f * r), new Vector3(-8f, 0f, 0f), true);
            return;
        }
        Transform halo = Pivot("Halo", new Vector3(0f, 1.42f * r, 0f), new Vector3(-8f, 0f, 0f));
        HatMotion.Add(halo.gameObject, HatMotion.Kind.Bob, Vector3.up, 0.45f, 0.05f * r);
        Transform rock = Pivot("Rock", Vector3.zero, Vector3.zero, halo);
        HatMotion.Add(rock.gameObject, HatMotion.Kind.Sweep, Vector3.forward, 0.3f, 5f);
        Renderer ring = Generated("Ring", Torus(0.24f), Shade.Light, Vector3.zero, Vector3.one * (0.68f * r), Vector3.zero, false, rock);
        HatMotion.Add(ring.gameObject, HatMotion.Kind.Pulse, Vector3.up, 0.35f, 0.25f);
        // The glints brighter still
        glowBoost = 4f;
        Transform glints = Pivot("Glints", Vector3.zero, Vector3.zero, rock);
        glints.gameObject.AddComponent<HatSpinner>().SetSteady(70f);
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.PI * 2f / 3f;
            Renderer glint = Primitive("Glint", PrimitiveType.Sphere, Shade.Light, new Vector3(Mathf.Cos(a), 0.12f, Mathf.Sin(a)) * (0.68f * r), Vector3.one * (0.09f * r), Vector3.zero, false, glints);
            HatMotion.Add(glint.gameObject, HatMotion.Kind.Twinkle, Vector3.up, 1.5f, 0.8f);
        }
    }

    // A big gamer headset: a chunky padded band over the head from ear to ear, a large round cup over
    // each of the drone's ear pods with a soft cushion against the head, a glowing ring on the outside
    // that slowly pulses, and a microphone on a boom in front of the mouth
    private void BuildHeadset(float r)
    {
        const float band = 1.22f; // the band's radius, round the head's centre
        const float cupX = 1.13f, cupSize = 0.98f, cupDepth = 0.17f;
        surface = Surface.Plastic;
        // Half a ring, stood up across the head, with a cushion along its top
        Generated("Band", Torus(0.2f, 180f), Shade.Dark, Vector3.zero, Vector3.one * (band * r), new Vector3(-90f, 0f, 0f), true);
        surface = Surface.Satin;
        Generated("Cushion", Torus(0.28f, 80f), Shade.Main, Vector3.zero, Vector3.one * ((band - 0.06f) * r), (Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0f, -50f, 0f)).eulerAngles, false);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 axis = new Vector3(0f, 0f, 90f);
            surface = Surface.Plastic;
            // The band slides into a yoke on top of each cup
            Primitive("Yoke", PrimitiveType.Cube, Shade.Dark, new Vector3(cupX * r * side, 0.42f * r, 0f), new Vector3(0.1f * r, 0.22f * r, 0.18f * r), Vector3.zero, false);
            Primitive("Cup", PrimitiveType.Cylinder, Shade.Main, new Vector3(cupX * r * side, 0f, 0f), new Vector3(cupSize * r, 0.5f * cupDepth * r, cupSize * r), axis, true);
            Generated("Cup Rim", Torus(0.14f), Shade.Dark, new Vector3((cupX + 0.5f * cupDepth) * r * side, 0f, 0f), Vector3.one * (0.48f * cupSize * r), axis, false);
            // The cushion between the cup and the head
            surface = Surface.Knit;
            Generated("Ear Cushion", Torus(0.32f), Shade.Dark, new Vector3((cupX - 0.5f * cupDepth - 0.02f) * r * side, 0f, 0f), Vector3.one * (0.4f * cupSize * r), axis, false);
            // A glowing ring on the outside of the cup, round a dark centre
            surface = Surface.Glow;
            Renderer ring = Generated("Light", Torus(0.12f), Shade.Light, new Vector3((cupX + 0.5f * cupDepth + 0.005f) * r * side, 0f, 0f), Vector3.one * (0.3f * cupSize * r), axis, false);
            HatMotion.Add(ring.gameObject, HatMotion.Kind.Pulse, Vector3.up, 0.5f, 0.5f);
            surface = Surface.Plastic;
            Primitive("Cup Plate", PrimitiveType.Cylinder, Shade.Dark, new Vector3((cupX + 0.5f * cupDepth) * r * side, 0f, 0f), new Vector3(0.5f * cupSize * r, 0.01f * r, 0.5f * cupSize * r), axis, false);
        }
        // The microphone: a boom from the left cup forward and down to the mouth, with a foam ball
        surface = Surface.Plastic;
        Vector3 boomFrom = new Vector3(-(cupX + 0.05f) * r, -0.1f * r, 0.2f * r);
        Vector3 boomTo = new Vector3(-0.6f * r, -0.52f * r, 0.9f * r);
        Primitive("Boom", PrimitiveType.Cylinder, Shade.Dark, (boomFrom + boomTo) * 0.5f, new Vector3(0.05f * r, 0.5f * (boomTo - boomFrom).magnitude, 0.05f * r), Quaternion.FromToRotation(Vector3.up, boomTo - boomFrom).eulerAngles, false);
        surface = Surface.Knit;
        Primitive("Mic", PrimitiveType.Sphere, Shade.Dark, boomTo, Vector3.one * (0.16f * r), Vector3.zero, false);
        surface = Surface.Glow;
        Renderer onAir = Primitive("Mic Light", PrimitiveType.Sphere, Shade.Light, boomFrom + (boomTo - boomFrom) * 0.25f + new Vector3(0f, 0.035f * r, 0f), Vector3.one * (0.04f * r), Vector3.zero, false);
        HatMotion.Add(onAir.gameObject, HatMotion.Kind.Blink, Vector3.up, 0.7f, 0.5f);
    }

    // A traffic cone sitting on the head: a square base, the cone with a lip round its top, and two
    // shiny stripes round it. It wobbles as the player moves.
    private void BuildTrafficCone(float r)
    {
        // Pulled down over the head to just above the face, wide enough for the head inside it
        const float sit = 0.3f, radius = 1.02f, height = 1.9f, top = 0.15f;
        // A ragdoll's cone stays put: its colliders can't move inside the falling hat
        Transform cone = transform;
        if (!solid)
        {
            cone = Pivot("Wobble", new Vector3(0f, sit * r, 0f));
            HatDangle.Add(cone.gameObject, Vector3.up, height * r, 1400f, 0f, 9f);
        }
        Vector3 at = solid ? new Vector3(0f, sit * r, 0f) : Vector3.zero;
        surface = Surface.Plastic;
        // The square base: a frame round the cone's open bottom, so it's hollow seen from below
        const float baseSize = 2.25f, bar = (baseSize - 2f * radius) / 2f + 0.03f;
        for (int side = -1; side <= 1; side += 2)
        {
            Primitive("Base", PrimitiveType.Cube, Shade.Dark, at + new Vector3(0f, 0f, (baseSize - bar) * 0.5f * side * r), new Vector3(baseSize * r, 0.08f * r, bar * r), Vector3.zero, true, cone);
            Primitive("Base", PrimitiveType.Cube, Shade.Dark, at + new Vector3((baseSize - bar) * 0.5f * side * r, 0f, 0f), new Vector3(bar * r, 0.08f * r, (baseSize - 2f * bar) * r), Vector3.zero, true, cone);
            // Corner pieces, filling the square between the frame and the cone
            for (int other = -1; other <= 1; other += 2)
            {
                Primitive("Base Corner", PrimitiveType.Cube, Shade.Dark, at + new Vector3(0.85f * side * r, 0f, 0.85f * other * r), new Vector3(0.3f * r, 0.08f * r, 0.3f * r), Vector3.zero, false, cone);
            }
        }
        Generated("Base Lip", Frustum(0.9f, false), Shade.Dark, at + new Vector3(0f, 0.04f * r, 0f), new Vector3((radius + 0.08f) * r, 0.06f * r, (radius + 0.08f) * r), Vector3.zero, false, cone);
        Line(Generated("Cone", Frustum(top, true), Shade.Main, at + new Vector3(0f, 0.04f * r, 0f), new Vector3(radius * r, height * r, radius * r), Vector3.zero, true, cone), Frustum(top, true, true));
        Generated("Top Lip", Torus(0.3f), Shade.Main, at + new Vector3(0f, (0.04f + height) * r, 0f), Vector3.one * (radius * top * r), Vector3.zero, false, cone);
        surface = Surface.Satin;
        Stripe("Stripe", Shade.Light, at.y + 0.04f * r, radius * r, top, height * r, 0.55f * r, 0.84f * r, 1f, cone);
        Stripe("Stripe", Shade.Light, at.y + 0.04f * r, radius * r, top, height * r, 1.12f * r, 1.34f * r, 1f, cone);
    }

    // A toadstool: a wide, squat mushroom cap pulled down snug over the head like the drone is its stalk, with a
    // rolled rim, pale spots scattered over the top and fine gills fanning out underneath (seen when
    // it's knocked off and lands upside down). It sways slightly as the player moves.
    private void BuildToadstool(float r)
    {
        // The cap's dome: its base this high up the head, this wide and this tall (of the head's radius)
        const float baseY = 0.3f, width = 1.55f, height = 1.0f;
        surface = Surface.Felt;

        // A ragdoll's hat stays still: its colliders can't move inside the falling hat
        Transform cap = transform;
        Vector3 at = new Vector3(0f, baseY * r, 0f);
        if (!solid)
        {
            cap = Pivot("Cap", at);
            // Stiff and quickly settled: a slight sway, not a wobble
            HatDangle.Add(cap.gameObject, Vector3.up, height * r, 6000f, 0f, 16f);
            at = Vector3.zero;
        }

        Generated("Cap", Dome(), Shade.Main, at, new Vector3(width * r, height * r, width * r), Vector3.zero, true, cap);
        // The rim rolls under a little
        Generated("Rim", Torus(0.05f), Shade.Main, at, Vector3.one * (width * 0.985f * r), Vector3.zero, false, cap);
        // Underneath: a pale disc closing the cap, tucked up inside the rim so it doesn't show from the
        // side, with gills fanning out from the middle below it
        Primitive("Underside", PrimitiveType.Cylinder, Shade.Light, at + new Vector3(0f, 0.03f * r, 0f), new Vector3(2f * width * 0.97f * r, 0.01f * r, 2f * width * 0.97f * r), Vector3.zero, false, cap);
        const int gills = 28;
        for (int i = 0; i < gills; i++)
        {
            Quaternion around = Quaternion.Euler(0f, 360f * i / gills, 0f);
            Primitive("Gill", PrimitiveType.Cube, Shade.Dark, at + around * Vector3.forward * (1.15f * r) - new Vector3(0f, 0.03f * r, 0f), new Vector3(0.025f * r, 0.04f * r, 0.7f * r), around.eulerAngles, false, cap);
        }

        // The spots, printed on the dome: (degrees round from the front, degrees down from the top,
        // size), scattered rather than in neat rings, and all clear of the dome's edge
        Vector3[] spots =
        {
            new Vector3(0f, 0f, 0.6f), new Vector3(35f, 40f, 0.48f), new Vector3(150f, 45f, 0.52f),
            new Vector3(262f, 36f, 0.45f), new Vector3(95f, 64f, 0.38f), new Vector3(205f, 62f, 0.4f),
            new Vector3(318f, 62f, 0.38f), new Vector3(10f, 66f, 0.28f), new Vector3(122f, 68f, 0.26f),
        };
        Vector3 domeRadii = new Vector3(width * r, height * r, width * r);
        foreach (Vector3 spot in spots)
        {
            float around = spot.x * Mathf.Deg2Rad, down = spot.y * Mathf.Deg2Rad;
            // The way from the dome's centre to the spot's middle
            Vector3 way = Vector3.Scale(new Vector3(Mathf.Sin(down) * Mathf.Sin(around), Mathf.Cos(down), Mathf.Sin(down) * Mathf.Cos(around)), domeRadii);
            Print("Spot", Shade.Light, OnEllipsoid(at, domeRadii, way), SurfacePrint.Ellipse(spot.z, spot.z), 0, cap);
        }
    }

    // Rabbit-ear antennas: a round metal base on top of the head with a blinking light, and two
    // telescopic rods in a V that boing about as the player moves, each with a glowing ball on its
    // tip that pulses
    private void BuildAntenna(float r)
    {
        surface = Surface.Metal;
        Vector3 basePoint = new Vector3(0f, 1.02f * r, 0f);
        Primitive("Base", PrimitiveType.Sphere, Shade.Dark, new Vector3(0f, 0.98f * r, 0f), new Vector3(0.45f * r, 0.28f * r, 0.45f * r), Vector3.zero, true);
        Generated("Base Ring", Torus(0.1f), Shade.Main, new Vector3(0f, 0.98f * r, 0f), Vector3.one * (0.23f * r), Vector3.zero, false);
        surface = Surface.Glow;
        Renderer led = Primitive("LED", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, 1.02f * r, 0.2f * r), Vector3.one * (0.06f * r), Vector3.zero, false);
        HatMotion.Add(led.gameObject, HatMotion.Kind.Blink, Vector3.up, 0.8f, 0.2f);
        const float length = 1.5f;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 euler = new Vector3(-10f, 0f, -28f * side);
            Vector3 up = Quaternion.Euler(euler) * Vector3.up;
            Transform rod = Pivot("Antenna", basePoint, euler);
            HatDangle.Add(rod.gameObject, up, length * r, 500f, 0.02f, 6f);
            surface = Surface.Metal;
            Primitive("Rod", PrimitiveType.Cylinder, Shade.Main, new Vector3(0f, 0.3f * length * r, 0f), new Vector3(0.06f * r, 0.3f * length * r, 0.06f * r), Vector3.zero, false, rod);
            Primitive("Rod", PrimitiveType.Cylinder, Shade.Light, new Vector3(0f, 0.75f * length * r, 0f), new Vector3(0.04f * r, 0.25f * length * r, 0.04f * r), Vector3.zero, false, rod);
            Primitive("Joint", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, 0.6f * length * r, 0f), new Vector3(0.075f * r, 0.025f * r, 0.075f * r), Vector3.zero, false, rod);
            surface = Surface.Glow;
            Renderer tip = Primitive("Tip", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, length * r, 0f), Vector3.one * (0.16f * r), Vector3.zero, false, rod);
            HatMotion.Add(tip.gameObject, HatMotion.Kind.Pulse, Vector3.up, 0.9f, 0.55f);
        }
        StillBox(new Vector3(0f, 1.9f * r, 0.12f * r), new Vector3(1.6f * r, 1.2f * r, 0.3f * r));
    }

    // A plunger stuck to the top of the head: a rubber cup with a rolled rim and a wooden handle
    // leaning a little to one side, which wobbles about as the player moves
    private void BuildPlunger(float r)
    {
        const float cupY = 0.72f, cupHeight = 0.38f, handle = 1.5f;
        surface = Surface.Plastic;
        Line(Generated("Cup", Dome(), Shade.Main, new Vector3(0f, cupY * r, 0f), new Vector3(0.62f * r, cupHeight * r, 0.62f * r), Vector3.zero, true), LiningDome());
        Generated("Cup Rim", Torus(0.14f), Shade.Dark, new Vector3(0f, (cupY + 0.2f * cupHeight) * r, 0f), Vector3.one * (0.6f * r), Vector3.zero, false);
        Vector3 cupTop = new Vector3(0f, (cupY + cupHeight - 0.02f) * r, 0f);
        Generated("Cup Neck", Frustum(0.7f, false), Shade.Main, cupTop - new Vector3(0f, 0.04f * r, 0f), new Vector3(0.13f * r, 0.12f * r, 0.13f * r), Vector3.zero, false);
        Vector3 euler = new Vector3(4f, 0f, -8f);
        Vector3 up = Quaternion.Euler(euler) * Vector3.up;
        Transform stick = transform;
        Vector3 at = cupTop;
        Vector3 along = up;
        Vector3 stickEuler = euler;
        if (!solid)
        {
            stick = Pivot("Handle", cupTop, euler);
            HatDangle.Add(stick.gameObject, up, handle * r, 600f, 0.02f, 7f);
            at = Vector3.zero;
            along = Vector3.up;
            stickEuler = Vector3.zero;
        }
        surface = Surface.Satin;
        Primitive("Handle", PrimitiveType.Cylinder, Shade.Light, at + along * (0.5f * handle * r), new Vector3(0.13f * r, 0.5f * handle * r, 0.13f * r), stickEuler, solid, stick);
        Primitive("Knob", PrimitiveType.Sphere, Shade.Dark, at + along * (handle * r), Vector3.one * (0.2f * r), Vector3.zero, false, stick);
        surface = Surface.Metal;
        Primitive("Ferrule", PrimitiveType.Cylinder, Shade.Dark, at + along * (0.06f * r), new Vector3(0.16f * r, 0.06f * r, 0.16f * r), stickEuler, false, stick);
    }

    // A cowboy hat: a tall crown, longer front to back, with a deep crease down the middle of its top
    // between two rounded ridges, a band with a buckle, on a wide brim that sweeps up hard at the
    // sides and dips a little at the front and back
    private void BuildCowboyHat(float r)
    {
        const float sit = 0.38f; // just above the face, so the crown is wide enough for the head inside it
        const float crownX = 0.97f, crownZ = 1.08f, crownTop = 0.78f, crownHeight = 0.82f;
        float crownFoot = (sit - 0.02f) * r;
        surface = Surface.Felt;
        BrimShape brim = new BrimShape { innerX = 0.95f, innerZ = 1.06f, outerX = 1.55f, outerZ = 1.8f, sideCurl = 0.55f, frontCurl = -0.1f, power = 2f, thickness = 0.03f };
        Generated("Brim", BrimMesh(brim), Shade.Dark, new Vector3(0f, sit * r, 0f), Vector3.one * r, Vector3.zero, true);
        Generated("Brim Edge", BrimEdge(brim, 0.03f), Shade.Dark, new Vector3(0f, sit * r, 0f), Vector3.one * r, Vector3.zero, false);

        Line(Generated("Crown", Frustum(crownTop, true), Shade.Main, new Vector3(0f, crownFoot, 0f), new Vector3(crownX * r, crownHeight * r, crownZ * r), Vector3.zero, true), Frustum(crownTop, true, true));
        // The top: two long rounded ridges side by side, meeting in a crease down the middle
        float top = crownFoot + crownHeight * r;
        for (int side = -1; side <= 1; side += 2)
        {
            Primitive("Crown Ridge", PrimitiveType.Sphere, Shade.Main, new Vector3(0.37f * r * side, top, -0.02f * r), new Vector3(0.74f * r, 0.3f * r, 1.44f * r), new Vector3(0f, 0f, -6f * side), false);
        }

        surface = Surface.Satin;
        Stripe("Band", Shade.Dark, crownFoot, crownX * r, crownTop, crownHeight * r, 0.06f * r, 0.2f * r, crownZ / crownX);
        // A metal buckle on the front of the band, and studs round it, sitting on the band
        surface = Surface.Metal;
        float bandY = crownFoot + 0.13f * r;
        float bandScale = (1f - (1f - crownTop) * 0.13f / crownHeight) * 1.03f;
        Primitive("Buckle", PrimitiveType.Cube, Shade.Light, new Vector3(0f, bandY, crownZ * bandScale * r), new Vector3(0.2f * r, 0.15f * r, 0.03f * r), new Vector3(-8f, 0f, 0f), false);
        Primitive("Buckle Middle", PrimitiveType.Cube, Shade.Dark, new Vector3(0f, bandY, (crownZ * bandScale + 0.015f) * r), new Vector3(0.11f * r, 0.07f * r, 0.02f * r), new Vector3(-8f, 0f, 0f), false);
        for (int i = 1; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            Primitive("Stud", PrimitiveType.Sphere, Shade.Light, new Vector3(Mathf.Sin(a) * crownX * bandScale * r, bandY, Mathf.Cos(a) * crownZ * bandScale * r), Vector3.one * (0.07f * r), Vector3.zero, false);
        }
    }

    // A sombrero: a tall rounded crown with a band, on a huge brim whose edge turns up, with a ring of
    // embroidered dots round the brim and little pom-poms hanging off its edge that swing as the
    // player moves
    private void BuildSombrero(float r)
    {
        const float sit = 0.38f, crownRadius = 0.97f, crownTop = 0.5f, crownHeight = 1.05f;
        surface = Surface.Felt;
        Primitive("Brim", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, sit * r, 0f), new Vector3(3.4f * r, 0.025f * r, 3.4f * r), Vector3.zero, true);
        Generated("Brim Edge", Frustum(1.1f, false), Shade.Light, new Vector3(0f, sit * r, 0f), new Vector3(1.7f * r, 0.22f * r, 1.7f * r), Vector3.zero, false);
        Generated("Brim Roll", Torus(0.06f), Shade.Light, new Vector3(0f, (sit + 0.22f) * r, 0f), Vector3.one * (1.87f * r), Vector3.zero, false);
        Line(Generated("Crown", Frustum(crownTop, true), Shade.Main, new Vector3(0f, sit * r, 0f), new Vector3(crownRadius * r, crownHeight * r, crownRadius * r), Vector3.zero, true), Frustum(crownTop, true, true));
        Primitive("Crown Top", PrimitiveType.Sphere, Shade.Main, new Vector3(0f, (sit + crownHeight) * r, 0f), new Vector3(2f * crownRadius * crownTop * r, 0.2f * r, 2f * crownRadius * crownTop * r), Vector3.zero, false);
        surface = Surface.Satin;
        Stripe("Band", Shade.Light, sit * r, crownRadius * r, crownTop, crownHeight * r, 0.05f * r, 0.22f * r);
        // Embroidery: two rings of dots round the brim, in turns of the light and main shades
        const int dots = 20;
        for (int i = 0; i < dots; i++)
        {
            float a = (i + 0.5f) * Mathf.PI * 2f / dots;
            Vector3 way = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            Primitive("Stitch", PrimitiveType.Sphere, i % 2 == 0 ? Shade.Light : Shade.Main, way * (1.4f * r) + Vector3.up * ((sit + 0.015f) * r), new Vector3(0.12f * r, 0.02f * r, 0.12f * r), Vector3.zero, false);
            Primitive("Stitch", PrimitiveType.Sphere, Shade.Light, way * (1.15f * r) + Vector3.up * ((sit + 0.015f) * r), new Vector3(0.06f * r, 0.02f * r, 0.06f * r), Vector3.zero, false);
        }
        // Pom-poms on short strings round the edge
        surface = Surface.Knit;
        const int poms = 14;
        for (int i = 0; i < poms; i++)
        {
            float a = i * Mathf.PI * 2f / poms;
            Vector3 hang = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (1.88f * r) + Vector3.up * ((sit + 0.2f) * r);
            Transform pom = Pivot("Pom-pom", hang);
            HatDangle.Add(pom.gameObject, Vector3.down, 0.2f * r, 40f, 1f, 4f);
            Primitive("String", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, -0.07f * r, 0f), new Vector3(0.02f * r, 0.07f * r, 0.02f * r), Vector3.zero, false, pom);
            Primitive("Pom", PrimitiveType.Sphere, i % 2 == 0 ? Shade.Light : Shade.Main, new Vector3(0f, -0.2f * r, 0f), Vector3.one * (0.14f * r), Vector3.zero, false, pom);
        }
    }

    // A pirate's tricorne: a low crown inside three upturned flaps that make a triangle round it,
    // each with a light trim along its top, and a big skull and crossbones on the front flap
    private void BuildPirateHat(float r)
    {
        const float sit = 0.48f;   // where the brim starts, up the head: just above the face

        surface = Surface.Felt;
        Line(Generated("Crown", Frustum(0.72f, true), Shade.Dark, new Vector3(0f, (sit - 0.06f) * r, 0f), new Vector3(0.93f * r, 0.86f * r, 0.93f * r), Vector3.zero, true), Frustum(0.72f, true, true));
        // One brim, turned up steeply into a triangle round the crown: a flat side at the front and
        // corners flaring out at the sides and back, like a real tricorne. The trim runs along its
        // top edge all the way round.
        BrimShape brim = new BrimShape { innerX = 0.91f, innerZ = 0.91f, apothem = 1.12f, cornerCap = 1.5f, sideCurl = 0.58f, frontCurl = 0.66f, power = 1.25f, thickness = 0.05f };
        Vector3 brimAt = new Vector3(0f, sit * r, 0f);
        Generated("Brim", BrimMesh(brim), Shade.Main, brimAt, Vector3.one * r, Vector3.zero, true);
        surface = Surface.Satin;
        Generated("Trim", BrimEdge(brim, 0.045f), Shade.Light, brimAt, Vector3.one * r, Vector3.zero, false);
        // A satin bead on each corner
        for (int i = 0; i < 3; i++)
        {
            Primitive("Corner", PrimitiveType.Sphere, Shade.Light, brimAt + BrimPoint(brim, (60f + 120f * i) * Mathf.Deg2Rad, 1f) * r, Vector3.one * (0.13f * r), Vector3.zero, false);
        }

        {
            // The skull and crossbones, printed on the outside of the front flap: x across it (round
            // the brim towards +x), y up it (towards its edge). The flap's outside is a half thickness
            // out from the brim's middle surface.
            const float skullT = 0.56f, e = 0.002f;
            float perAround = (BrimPoint(brim, e, skullT) - BrimPoint(brim, -e, skullT)).magnitude / (2f * e) * r;
            float perOut = (BrimPoint(brim, 0f, skullT + e) - BrimPoint(brim, 0f, skullT - e)).magnitude / (2f * e) * r;
            SurfacePrint.Map flap = (Vector2 uv, out Vector3 normal) =>
            {
                float a = uv.x / perAround, t = skullT + uv.y / perOut;
                normal = -BrimNormal(brim, a, t);
                return brimAt + BrimPoint(brim, a, t) * r + normal * (0.5f * brim.thickness * r);
            };
            surface = Surface.Satin;
            // Two crossed bones, each with a double knob at both ends
            for (int side = -1; side <= 1; side += 2)
            {
                float angle = 32f * side;
                Vector2 along = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Vector2 across = new Vector2(-along.y, along.x);
                Vector2 middle = new Vector2(0f, -0.04f);
                Print("Bone", Shade.Light, flap, At(SurfacePrint.Bar(0.07f, 0.56f), middle.x, middle.y, angle + 90f));
                for (int end = -1; end <= 1; end += 2)
                {
                    for (int knob = -1; knob <= 1; knob += 2)
                    {
                        Vector2 k = middle + along * (0.28f * end) + across * (0.035f * knob);
                        Print("Bone End", Shade.Light, flap, At(SurfacePrint.Ellipse(0.085f, 0.085f), k.x, k.y));
                    }
                }
            }
            // The skull over them: a round head and a jaw, with dark eye holes, a nose and gaps between
            // the teeth
            Print("Skull", Shade.Light, flap, At(SurfacePrint.Ellipse(0.3f, 0.28f), 0f, 0.05f), 1);
            Print("Jaw", Shade.Light, flap, At(SurfacePrint.Box(0.16f, 0.1f), 0f, -0.1f), 1);
            for (int side = -1; side <= 1; side += 2)
            {
                Print("Eye", Shade.Dark, flap, At(SurfacePrint.Ellipse(0.085f, 0.09f), 0.065f * side, 0.06f), 2);
                Print("Tooth Gap", Shade.Dark, flap, At(SurfacePrint.Box(0.012f, 0.06f), 0.03f * side, -0.115f), 2);
            }
            Print("Nose", Shade.Dark, flap, At(SurfacePrint.Box(0.035f, 0.035f), 0f, -0.015f, 45f), 2);
        }

        // A big feather sticking up and back out of the left side, swaying as the player moves
        Vector3 plumeWay = new Vector3(-0.55f, 1f, -0.65f).normalized;
        Transform plume = Pivot("Plume", new Vector3(-0.42f * r, (sit + 0.42f) * r, -0.2f * r), Quaternion.FromToRotation(Vector3.up, plumeWay).eulerAngles);
        HatDangle.Add(plume.gameObject, plumeWay, 0.9f * r, 250f, 0.05f, 5f);
        surface = Surface.Satin;
        Primitive("Quill", PrimitiveType.Cylinder, Shade.Light, new Vector3(0f, 0.45f * r, 0f), new Vector3(0.025f * r, 0.45f * r, 0.025f * r), Vector3.zero, false, plume);
        surface = Surface.Felt;
        Primitive("Feather", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, 0.5f * r, 0f), new Vector3(0.26f * r, 0.78f * r, 0.03f * r), new Vector3(0f, 0f, 0f), false, plume);
        Primitive("Feather Tip", PrimitiveType.Sphere, Shade.Light, new Vector3(0.04f * r, 0.88f * r, 0f), new Vector3(0.16f * r, 0.34f * r, 0.03f * r), new Vector3(0f, 0f, -18f), false, plume);
        Primitive("Feather Inner", PrimitiveType.Sphere, Shade.Main, new Vector3(0f, 0.42f * r, 0.012f * r), new Vector3(0.12f * r, 0.5f * r, 0.02f * r), Vector3.zero, false, plume);
    }

    // An astronaut's fishbowl: a big clear glass bowl round the whole head, a metal collar round its
    // bottom, three fish swimming round in the water over the head (at their own heights and
    // speeds, one the other way round), wagging their tails, and bubbles rising up and popping
    private void BuildFishbowl(float r)
    {
        surface = Surface.Glass;
        Primitive("Bowl", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, 0.3f * r, 0f), Vector3.one * (2.9f * r), Vector3.zero, true);
        surface = Surface.Metal;
        Generated("Collar", Torus(0.22f), Shade.Dark, new Vector3(0f, -0.8f * r, 0f), Vector3.one * (0.94f * r), Vector3.zero, false);
        Generated("Collar Trim", Torus(0.08f), Shade.Light, new Vector3(0f, -0.7f * r, 0f), Vector3.one * (0.98f * r), Vector3.zero, false);

        Fish(r, 1.22f, 0.56f, 1.15f, 55f, Shade.Main);
        Fish(r, 1.0f, 0.72f, 0.95f, -40f, Shade.Light);
        Fish(r, 1.48f, 0.3f, 0.85f, 75f, Shade.Dark);

        surface = Surface.Glass;
        // Rising from the top of the head, near the middle, inside the smallest fish's circle
        Vector3[] bubbles = { new Vector3(0.06f, 1.0f, 0.08f), new Vector3(-0.1f, 1.0f, -0.04f), new Vector3(0.04f, 1.0f, -0.12f), new Vector3(-0.02f, 1.0f, 0.14f), new Vector3(0.14f, 1.0f, -0.02f) };
        for (int i = 0; i < bubbles.Length; i++)
        {
            Renderer bubble = Primitive("Bubble", PrimitiveType.Sphere, Shade.Light, bubbles[i] * r, Vector3.one * ((0.05f + 0.015f * i) * r), Vector3.zero, false);
            HatMotion.Add(bubble.gameObject, HatMotion.Kind.Rise, Vector3.up, 0.28f + 0.05f * i, 0.62f * r);
        }
    }

    // One fish going round the fishbowl: height and radius of its circle (of the head's radius), its
    // size, and its speed in degrees a second (positive: clockwise seen from above)
    private void Fish(float r, float height, float radius, float size, float speed, Shade shade)
    {
        Transform swim = Pivot("Swim", new Vector3(0f, height * r, 0f));
        swim.gameObject.AddComponent<HatSpinner>().SetSteady(speed);
        Transform fish = Pivot("Fish", new Vector3(radius * r, 0f, 0f), Vector3.zero, swim);
        HatMotion.Add(fish.gameObject, HatMotion.Kind.Bob, Vector3.up, 0.7f, 0.03f * r);
        // Facing along its circle: going clockwise, at +x it heads to -z
        float ahead = speed > 0f ? -1f : 1f;
        float s = size * r;
        surface = Surface.Plastic;
        Primitive("Body", PrimitiveType.Sphere, shade, Vector3.zero, new Vector3(0.16f * s, 0.22f * s, 0.36f * s), Vector3.zero, false, fish);
        // The tail wags from side to side
        Transform wag = Pivot("Tail Wag", new Vector3(0f, 0f, -ahead * 0.15f * s), Vector3.zero, fish);
        HatMotion.Add(wag.gameObject, HatMotion.Kind.Sweep, Vector3.up, 2.4f, 28f);
        Primitive("Tail", PrimitiveType.Cube, shade, new Vector3(0f, 0f, -ahead * 0.07f * s), new Vector3(0.03f * s, 0.17f * s, 0.17f * s), new Vector3(45f, 0f, 0f), false, wag);
        Primitive("Fin", PrimitiveType.Cube, shade, new Vector3(0f, 0.11f * s, 0.02f * s * ahead), new Vector3(0.02f * s, 0.09f * s, 0.14f * s), new Vector3(-20f * ahead, 0f, 0f), false, fish);
        Primitive("Stripe", PrimitiveType.Sphere, shade == Shade.Light ? Shade.Main : Shade.Light, new Vector3(0f, 0f, ahead * 0.04f * s), new Vector3(0.165f * s, 0.2f * s, 0.06f * s), Vector3.zero, false, fish);
        for (int side = -1; side <= 1; side += 2)
        {
            Primitive("Eye", PrimitiveType.Sphere, Shade.Dark, new Vector3(0.07f * s * side, 0.04f * s, ahead * 0.12f * s), Vector3.one * (0.05f * s), Vector3.zero, false, fish);
        }
    }

    // A wizard's hat: a wide brim and a tall cone whose floppy tip bends back and sways as the player
    // moves, with a glowing bauble on its end, a band, a crescent moon and twinkling stars
    private void BuildWizardHat(float r)
    {
        // Down to just above the face, so the cone is wide enough for the head inside it
        const float sit = 0.38f, radius = 0.96f, lower = 1.25f, narrow = 0.4f;
        surface = Surface.Felt;
        Primitive("Brim", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, sit * r, 0f), new Vector3(2.7f * r, 0.025f * r, 2.7f * r), Vector3.zero, true);
        Generated("Brim Edge", Torus(0.035f), Shade.Dark, new Vector3(0f, sit * r, 0f), Vector3.one * (1.35f * r), Vector3.zero, false);
        Line(Generated("Cone", Frustum(narrow, false), Shade.Main, new Vector3(0f, sit * r, 0f), new Vector3(radius * r, lower * r, radius * r), Vector3.zero, true), Frustum(narrow, false, true));
        float joint = radius * narrow * r;
        Vector3 tipWay = Quaternion.Euler(-25f, 0f, 0f) * Vector3.up;
        Transform tip = Pivot("Tip", new Vector3(0f, (sit + lower - 0.02f) * r, 0f), new Vector3(-25f, 0f, 0f));
        HatDangle.Add(tip.gameObject, tipWay, 0.9f * r, 200f, 0.15f, 5f);
        Primitive("Tip Joint", PrimitiveType.Sphere, Shade.Main, Vector3.zero, Vector3.one * (2f * joint), Vector3.zero, false, tip);
        Generated("Tip", Frustum(0f, false), Shade.Main, Vector3.zero, new Vector3(joint, 0.9f * r, joint), Vector3.zero, false, tip);
        surface = Surface.Satin;
        Stripe("Band", Shade.Light, sit * r, radius * r, narrow, lower * r, 0.03f * r, 0.18f * r);
        surface = Surface.Glow;
        Renderer bauble = Primitive("Bauble", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, 0.92f * r, 0f), Vector3.one * (0.11f * r), Vector3.zero, false, tip);
        HatMotion.Add(bauble.gameObject, HatMotion.Kind.Pulse, Vector3.up, 0.6f, 0.5f);

        // The cone's surface leans in this far from upright
        float lean = Mathf.Atan2(radius * (1f - narrow), lower) * Mathf.Rad2Deg;
        // Prints on the cone: around is degrees round from the front, up how far up the cone (of its
        // height); x goes round it towards +x, y up its slope
        SurfacePrint.Map OnCone(float around, float up) => (Vector2 uv, out Vector3 normal) =>
        {
            float rise = up * lower * r + uv.y * Mathf.Cos(lean * Mathf.Deg2Rad);
            float ring = radius * (1f - (1f - narrow) * rise / (lower * r)) * r;
            float a = around * Mathf.Deg2Rad + uv.x / ring;
            normal = new Vector3(Mathf.Sin(a), radius * (1f - narrow) / lower, Mathf.Cos(a)).normalized;
            return new Vector3(Mathf.Sin(a) * ring, sit * r + rise, Mathf.Cos(a) * ring);
        };

        // A crescent moon on the front: a glowing disc with a felt one printed over most of it
        SurfacePrint.Map moon = OnCone(0f, 0.5f);
        Print("Moon", Shade.Light, moon, SurfacePrint.Ellipse(0.26f, 0.26f));
        surface = Surface.Felt;
        Print("Moon Shadow", Shade.Main, moon, At(SurfacePrint.Ellipse(0.22f, 0.22f), 0.07f, 0.04f), 1);

        // Four-pointed stars round the cone, each twinkling in its own time
        surface = Surface.Glow;
        float[] heights = { 0.3f, 0.45f, 0.62f, 0.78f, 0.3f };
        float[] angles = { 60f, -60f, 150f, 30f, -150f };
        float[] sizes = { 0.16f, 0.12f, 0.14f, 0.1f, 0.13f };
        for (int i = 0; i < heights.Length; i++)
        {
            Renderer star = Print("Star", Shade.Light, OnCone(angles[i], heights[i]), SurfacePrint.Star(4, sizes[i] * 0.5f, sizes[i] * 0.13f));
            HatMotion.Add(star.gameObject, HatMotion.Kind.Twinkle, Vector3.up, 0.8f + 0.15f * i, 0.7f);
        }
    }

    // A graduation cap: a skullcap under a flat square board, set diamond-wise, with a cord from the
    // button in the middle to a corner and a tassel hanging off it that swings as the player moves
    private void BuildGraduationCap(float r)
    {
        const float board = 1.12f;
        surface = Surface.Felt;
        Crown(Vector3.one * (1.07f * r));
        Primitive("Board", PrimitiveType.Cube, Shade.Dark, new Vector3(0f, board * r, 0f), new Vector3(1.9f * r, 0.06f * r, 1.9f * r), new Vector3(0f, 45f, 0f), true);
        Primitive("Board Edge", PrimitiveType.Cube, Shade.Main, new Vector3(0f, board * r, 0f), new Vector3(1.92f * r, 0.03f * r, 1.92f * r), new Vector3(0f, 45f, 0f), false);
        surface = Surface.Satin;
        float top = (board + 0.03f) * r;
        Primitive("Button", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, top, 0f), new Vector3(0.16f * r, 0.08f * r, 0.16f * r), Vector3.zero, false);
        // The board's corner on the right is 1.34 of the head's radius out
        const float corner = 1.3f;
        Primitive("Cord", PrimitiveType.Cube, Shade.Light, new Vector3(0.5f * corner * r, top + 0.01f * r, 0f), new Vector3(corner * r, 0.02f * r, 0.03f * r), Vector3.zero, false);

        Transform tassel = Pivot("Tassel", new Vector3(corner * r, top, 0f));
        HatDangle.Add(tassel.gameObject, Vector3.down, 0.5f * r, 0f, 1f, 4f);
        Primitive("String", PrimitiveType.Cylinder, Shade.Light, new Vector3(0f, -0.15f * r, 0f), new Vector3(0.03f * r, 0.15f * r, 0.03f * r), Vector3.zero, false, tassel);
        Primitive("Knot", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, -0.3f * r, 0f), Vector3.one * (0.09f * r), Vector3.zero, false, tassel);
        Generated("Tassel", Frustum(0.35f, true), Shade.Light, new Vector3(0f, -0.52f * r, 0f), new Vector3(0.1f * r, 0.24f * r, 0.1f * r), Vector3.zero, false, tassel);
        // Loose threads fanning out at the bottom
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f;
            Quaternion fan = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(8f, 0f, 0f);
            Primitive("Thread", PrimitiveType.Cylinder, Shade.Light, new Vector3(0f, -0.6f * r, 0f) + fan * Vector3.down * (0.04f * r) + Quaternion.Euler(0f, a, 0f) * Vector3.forward * (0.06f * r), new Vector3(0.025f * r, 0.06f * r, 0.025f * r), fan.eulerAngles, false, tassel);
        }
    }

    // A military beret, worn the regulation way: a leather band tight round the head just above the
    // face, the felt snug over the head and pulled down over the right side, and a shield-shaped
    // badge standing on the left front, over the left eye
    private void BuildMilitaryBeret(float r)
    {
        surface = Surface.Felt;
        // The felt hugging the head, a little fuller than the head itself
        Crown(new Vector3(1.05f * r, 1.08f * r, 1.05f * r));
        // The body: a wide, flat round of felt lying on top, its right side pulled down over the
        // ear and its left side standing a little proud of the head. A softer lip rolls round its
        // edge, and a stitched ring marks the middle of the top.
        Quaternion drape = Quaternion.Euler(0f, 0f, -20f);
        Vector3 bodyAt = new Vector3(0.3f * r, 0.84f * r, -0.04f * r);
        Primitive("Body", PrimitiveType.Sphere, Shade.Main, bodyAt, new Vector3(2.34f * r, 0.56f * r, 2.22f * r), drape.eulerAngles, true);
        Generated("Body Lip", Torus(0.09f), Shade.Main, bodyAt + drape * new Vector3(0f, -0.05f * r, 0f), new Vector3(1.13f * r, 0.6f * r, 1.07f * r), drape.eulerAngles, false);
        Generated("Top Stitching", Torus(0.02f), Shade.Dark, bodyAt + drape * new Vector3(0f, 0.27f * r, 0f), Vector3.one * (0.34f * r), drape.eulerAngles, false);
        Primitive("Top Knot", PrimitiveType.Sphere, Shade.Dark, bodyAt + drape * new Vector3(0f, 0.28f * r, 0f), new Vector3(0.1f * r, 0.05f * r, 0.1f * r), drape.eulerAngles, false);
        // The leather band, with its edge piped
        surface = Surface.Satin;
        Line(Generated("Band", Frustum(0.98f, false), Shade.Dark, new Vector3(0f, 0.24f * r, 0f), new Vector3(1.01f * r, 0.12f * r, 1.01f * r), Vector3.zero, true), Frustum(0.98f, false, true));
        Generated("Band Edge", Torus(0.05f), Shade.Dark, new Vector3(0f, 0.24f * r, 0f), Vector3.one * (1.01f * r), Vector3.zero, false);
        // The band's drawstring tied in a bow at the back, its two ribbon tails hanging down
        Primitive("Bow", PrimitiveType.Sphere, Shade.Dark, new Vector3(0f, 0.3f * r, -1.03f * r), new Vector3(0.16f * r, 0.08f * r, 0.05f * r), Vector3.zero, false);
        for (int side = -1; side <= 1; side += 2)
        {
            Primitive("Ribbon", PrimitiveType.Cube, Shade.Dark, new Vector3(0.06f * r * side, 0.08f * r, -1.02f * r), new Vector3(0.07f * r, 0.4f * r, 0.012f * r), new Vector3(-8f, 0f, 9f * side), false);
        }

        // The badge, printed on the felt over the left eye: a shield (square top, pointed bottom) with
        // a darker shield on it, and a star in the middle that catches the light now and then
        Quaternion on = Quaternion.Euler(0f, -24f, 0f) * Quaternion.Euler(56f, 0f, 0f);
        SurfacePrint.Map felt = OnEllipsoid(Vector3.zero, new Vector3(1.05f * r, 1.08f * r, 1.05f * r), on * Vector3.up, on * Vector3.right, on * Vector3.back);
        surface = Surface.Satin;
        Print("Shield", Shade.Light, felt, At(SurfacePrint.Box(0.36f, 0.26f), 0f, 0.05f));
        Print("Shield Point", Shade.Light, felt, At(SurfacePrint.Box(0.255f, 0.255f), 0f, -0.08f, 45f));
        surface = Surface.Metal;
        Print("Inner Shield", Shade.Dark, felt, At(SurfacePrint.Box(0.28f, 0.2f), 0f, 0.05f), 1);
        Print("Inner Point", Shade.Dark, felt, At(SurfacePrint.Box(0.198f, 0.198f), 0f, -0.06f, 45f), 1);
        surface = Surface.Glow;
        Renderer star = Print("Star", Shade.Light, felt, SurfacePrint.Star(5, 0.065f, 0.027f), 2);
        HatMotion.Add(star.gameObject, HatMotion.Kind.Twinkle, Vector3.up, 0.5f, 0.5f);
    }

    // A two-tier birthday cake on a plate, with icing dripping down each tier's edge, sprinkles, and a
    // striped candle on top whose flame flickers and leans as the player moves. It goes out when the
    // hat is knocked off (a dead player's hat).
    private void BuildBirthdayCake(float r)
    {
        const float sit = 0.75f, height = 0.32f, radius = 0.7f, upperHeight = 0.26f, upperRadius = 0.46f;
        surface = Surface.Metal;
        Primitive("Plate", PrimitiveType.Cylinder, Shade.Light, new Vector3(0f, (sit - 0.01f) * r, 0f), new Vector3(1.7f * radius * r, 0.015f * r, 1.7f * radius * r), Vector3.zero, false);
        surface = Surface.Satin;
        Primitive("Cake", PrimitiveType.Cylinder, Shade.Main, new Vector3(0f, (sit + 0.5f * height) * r, 0f), new Vector3(2f * radius * r, 0.5f * height * r, 2f * radius * r), Vector3.zero, true);
        float top = (sit + height) * r;
        Icing(r, top, radius, 10, 0.3f);
        Primitive("Upper Cake", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, top + 0.5f * upperHeight * r, 0f), new Vector3(2f * upperRadius * r, 0.5f * upperHeight * r, 2f * upperRadius * r), Vector3.zero, false);
        float upperTop = top + upperHeight * r;
        Icing(r, upperTop, upperRadius, 7, 0.1f);
        // Piped icing blobs round the bottom tier's top edge, outside the top tier
        const int blobs = 12;
        for (int i = 0; i < blobs; i++)
        {
            float a = i * Mathf.PI * 2f / blobs;
            Primitive("Piping", PrimitiveType.Sphere, Shade.Light, new Vector3(Mathf.Sin(a) * 0.6f * r, top + 0.03f * r, Mathf.Cos(a) * 0.6f * r), new Vector3(0.11f * r, 0.08f * r, 0.11f * r), Vector3.zero, false);
        }
        // Sprinkles scattered over the top tier, in the dark and main shades
        surface = Surface.Plastic;
        System.Random scatter = new System.Random(7);
        for (int i = 0; i < 16; i++)
        {
            float a = (float)scatter.NextDouble() * Mathf.PI * 2f;
            float d = Mathf.Lerp(0.12f, upperRadius - 0.06f, (float)scatter.NextDouble()) * r;
            Primitive("Sprinkle", PrimitiveType.Capsule, i % 2 == 0 ? Shade.Dark : Shade.Main, new Vector3(Mathf.Sin(a) * d, upperTop + 0.025f * r, Mathf.Cos(a) * d), new Vector3(0.025f * r, 0.035f * r, 0.025f * r), new Vector3(90f, (float)scatter.NextDouble() * 360f, 0f), false);
        }

        const float candle = 0.36f;
        Primitive("Candle", PrimitiveType.Cylinder, Shade.Light, new Vector3(0f, upperTop + 0.5f * candle * r, 0f), new Vector3(0.11f * r, 0.5f * candle * r, 0.11f * r), Vector3.zero, false);
        // Stripes twisting round the candle
        for (int i = 0; i < 4; i++)
        {
            Primitive("Stripe", PrimitiveType.Cube, Shade.Main, new Vector3(0f, upperTop + (0.06f + 0.085f * i) * r, 0f), new Vector3(0.118f * r, 0.03f * r, 0.118f * r), new Vector3(0f, 45f * i, 22f), false);
        }
        float wick = upperTop + (candle + 0.04f) * r;
        Primitive("Wick", PrimitiveType.Cube, Shade.Dark, new Vector3(0f, wick, 0f), new Vector3(0.02f * r, 0.08f * r, 0.02f * r), Vector3.zero, false);
        if (solid) return;

        // The flame leans away from the way the player moves, and flickers: a bright core in a bigger glow
        Transform flame = Pivot("Flame", new Vector3(0f, wick, 0f));
        HatDangle.Add(flame.gameObject, Vector3.up, 0.15f * r, 250f, 0f, 6f);
        surface = Surface.Glow;
        Renderer fire = Primitive("Fire", PrimitiveType.Sphere, Shade.Main, new Vector3(0f, 0.12f * r, 0f), new Vector3(0.13f * r, 0.26f * r, 0.13f * r), Vector3.zero, false, flame);
        HatMotion.Add(fire.gameObject, HatMotion.Kind.Flicker, Vector3.up, 6f, 0.5f);
        HatMotion.Add(fire.gameObject, HatMotion.Kind.Pulse, Vector3.up, 3f, 0.3f);
        Renderer core = Primitive("Fire Core", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, 0.09f * r, 0f), new Vector3(0.07f * r, 0.14f * r, 0.07f * r), Vector3.zero, false, flame);
        HatMotion.Add(core.gameObject, HatMotion.Kind.Flicker, Vector3.up, 8f, 0.4f);
    }

    // A tier's icing: a layer over its top and drips down its edge
    private void Icing(float r, float top, float radius, int drips, float offset)
    {
        Primitive("Icing", PrimitiveType.Cylinder, Shade.Light, new Vector3(0f, top, 0f), new Vector3(2.06f * radius * r, 0.03f * r, 2.06f * radius * r), Vector3.zero, false);
        for (int i = 0; i < drips; i++)
        {
            float a = (i + offset) * Mathf.PI * 2f / drips;
            float drop = (i % 3 == 0 ? 0.16f : 0.1f) * r;
            Primitive("Drip", PrimitiveType.Sphere, Shade.Light, new Vector3(Mathf.Sin(a) * radius * r, top - drop * 0.5f, Mathf.Cos(a) * radius * r), new Vector3(0.13f * r, drop + 0.04f * r, 0.13f * r), Vector3.zero, false);
        }
    }

    // A police siren: a glass dome on a metal base, with a pulsing lamp inside and two lights going
    // round it, faster the faster the player moves
    private void BuildSiren(float r)
    {
        const float dome = 1.22f;
        surface = Surface.Metal;
        Primitive("Base", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, 0.98f * r, 0f), new Vector3(0.6f * r, 0.06f * r, 0.6f * r), Vector3.zero, true);
        Generated("Base Rim", Torus(0.12f), Shade.Light, new Vector3(0f, 1.04f * r, 0f), Vector3.one * (0.28f * r), Vector3.zero, false);
        // Bolts round the base
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            Primitive("Bolt", PrimitiveType.Sphere, Shade.Light, new Vector3(Mathf.Sin(a) * 0.27f * r, 1.0f * r, Mathf.Cos(a) * 0.27f * r), Vector3.one * (0.05f * r), Vector3.zero, false);
        }
        Transform lights = Pivot("Lights", new Vector3(0f, dome * r, 0f));
        lights.gameObject.AddComponent<HatSpinner>();
        surface = Surface.Glow;
        Renderer lamp = Primitive("Lamp", PrimitiveType.Sphere, Shade.Main, Vector3.zero, Vector3.one * (0.16f * r), Vector3.zero, false, lights);
        HatMotion.Add(lamp.gameObject, HatMotion.Kind.Pulse, Vector3.up, 2.5f, 0.7f);
        for (int side = -1; side <= 1; side += 2)
        {
            surface = Surface.Metal;
            Primitive("Reflector", PrimitiveType.Cube, Shade.Light, new Vector3(0.11f * side * r, 0f, 0f), new Vector3(0.02f * r, 0.32f * r, 0.24f * r), Vector3.zero, false, lights);
            surface = Surface.Glow;
            Primitive("Light", PrimitiveType.Cube, Shade.Light, new Vector3(0.14f * side * r, 0f, 0f), new Vector3(0.05f * r, 0.3f * r, 0.22f * r), Vector3.zero, false, lights);
        }
        surface = Surface.Glass;
        Primitive("Dome", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, dome * r, 0f), new Vector3(0.52f * r, 0.6f * r, 0.52f * r), Vector3.zero, true);
        surface = Surface.Metal;
        Primitive("Cap", PrimitiveType.Sphere, Shade.Dark, new Vector3(0f, (dome + 0.3f) * r, 0f), new Vector3(0.12f * r, 0.05f * r, 0.12f * r), Vector3.zero, false);
    }

    // A radar dish on a short mast, sweeping slowly from side to side, with a rim round the dish and a
    // receiver in front of it that blinks
    private void BuildRadarDish(float r)
    {
        const float mastTop = 1.25f;
        surface = Surface.Metal;
        Primitive("Mount", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, 0.98f * r, 0f), new Vector3(0.36f * r, 0.04f * r, 0.36f * r), Vector3.zero, false);
        Primitive("Mast", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, 0.5f * (0.96f + mastTop) * r, 0f), new Vector3(0.08f * r, 0.5f * (mastTop - 0.96f) * r, 0.08f * r), Vector3.zero, false);
        Primitive("Turntable", PrimitiveType.Cylinder, Shade.Main, new Vector3(0f, mastTop * r, 0f), new Vector3(0.2f * r, 0.03f * r, 0.2f * r), Vector3.zero, false);
        Transform sweep = Pivot("Sweep", new Vector3(0f, mastTop * r, 0f));
        HatMotion.Add(sweep.gameObject, HatMotion.Kind.Sweep, Vector3.up, 0.15f, 70f);
        // The dish faces forwards and 20 degrees up: its back bulges back and down
        Vector3 dish = new Vector3(0f, 0.3f * r, 0.2f * r);
        Vector3 facing = Quaternion.Euler(-20f, 0f, 0f) * Vector3.forward;
        Generated("Dish", Dome(), Shade.Main, dish, new Vector3(0.45f * r, 0.22f * r, 0.45f * r), new Vector3(-110f, 0f, 0f), false, sweep);
        // The dome's edge is 0.2 of its height above its centre, and 0.98 of its radius out
        Generated("Dish Rim", Torus(0.1f), Shade.Light, dish + Quaternion.Euler(-110f, 0f, 0f) * new Vector3(0f, 0.044f * r, 0f), Vector3.one * (0.44f * r), new Vector3(-110f, 0f, 0f), false, sweep);
        Vector3 back = dish - facing * (0.22f * r);
        Primitive("Arm", PrimitiveType.Cylinder, Shade.Dark, back * 0.5f, new Vector3(0.06f * r, 0.5f * back.magnitude, 0.06f * r), Quaternion.FromToRotation(Vector3.up, back).eulerAngles, false, sweep);
        // Three struts holding the receiver out in front of the dish
        Vector3 receiver = dish + facing * (0.3f * r);
        for (int i = 0; i < 3; i++)
        {
            Vector3 edge = dish + Quaternion.Euler(-110f, 0f, 0f) * (Quaternion.Euler(0f, 120f * i, 0f) * Vector3.forward * (0.42f * r) + new Vector3(0f, 0.05f * r, 0f));
            Vector3 strut = receiver - edge;
            Primitive("Strut", PrimitiveType.Cylinder, Shade.Dark, (edge + receiver) * 0.5f, new Vector3(0.02f * r, 0.5f * strut.magnitude, 0.02f * r), Quaternion.FromToRotation(Vector3.up, strut).eulerAngles, false, sweep);
        }
        surface = Surface.Glow;
        Renderer light = Primitive("Receiver", PrimitiveType.Sphere, Shade.Light, receiver, Vector3.one * (0.09f * r), Vector3.zero, false, sweep);
        HatMotion.Add(light.gameObject, HatMotion.Kind.Blink, Vector3.up, 1.2f, 0.25f);
        StillBox(new Vector3(0f, 1.4f * r, 0f), new Vector3(0.9f * r, 0.6f * r, 0.9f * r));
    }

    // A jester's hat: a cap in two shades with three floppy striped prongs, each with a bell on the
    // end, that bounce and swing as the player moves, over a band of diamonds
    private void BuildJesterHat(float r)
    {
        surface = Surface.Felt;
        Crown(Vector3.one * (1.07f * r));
        // The cap in halves of two shades, like the prongs
        Generated("Half", DomeSlice(180f), Shade.Dark, Vector3.zero, Vector3.one * (1.075f * r), new Vector3(0f, 90f, 0f), false);
        Vector3[] ways = { new Vector3(-1f, 0.7f, 0f), new Vector3(1f, 0.7f, 0f), new Vector3(0f, 1f, -0.35f) };
        Shade[] prongShades = { Shade.Main, Shade.Dark, Shade.Light };
        const float length = 0.8f;
        for (int i = 0; i < ways.Length; i++)
        {
            Vector3 way = ways[i].normalized;
            // Rooted at the cap's surface, so their open ends don't show inside a hat knocked off
            Transform prong = Pivot("Prong", way * (1.065f * r), Quaternion.FromToRotation(Vector3.up, way).eulerAngles);
            HatDangle.Add(prong.gameObject, way, length * r, 300f, 0.4f, 5f);
            surface = Surface.Felt;
            Generated("Prong", Frustum(0.2f, false), prongShades[i], Vector3.zero, new Vector3(0.26f * r, length * r, 0.26f * r), Vector3.zero, false, prong);
            // A ring of the other shade round the prong, near its end
            Stripe("Prong Stripe", prongShades[i] == Shade.Light ? Shade.Main : Shade.Light, 0f, 0.26f * r, 0.2f, length * r, 0.5f * r, 0.6f * r, 1f, prong);
            surface = Surface.Metal;
            Primitive("Bell Cap", PrimitiveType.Cylinder, Shade.Light, new Vector3(0f, (length - 0.01f) * r, 0f), new Vector3(0.08f * r, 0.02f * r, 0.08f * r), Vector3.zero, false, prong);
            Primitive("Bell", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, (length + 0.08f) * r, 0f), Vector3.one * (0.18f * r), Vector3.zero, false, prong);
            Primitive("Bell Slot", PrimitiveType.Cube, Shade.Dark, new Vector3(0f, (length + 0.13f) * r, 0f), new Vector3(0.19f * r, 0.02f * r, 0.03f * r), Vector3.zero, false, prong);
        }
        surface = Surface.Felt;
        Line(Generated("Band", Frustum(1f, false), Shade.Dark, new Vector3(0f, 0.12f * r, 0f), new Vector3(1.1f * r, 0.18f * r, 1.1f * r), Vector3.zero, true), Frustum(1f, false, true));
        // Diamonds printed round the band, in turns of the light and main shades
        const int diamonds = 12;
        float bandRadius = 1.1f * r, bandMiddle = 0.21f * r;
        for (int i = 0; i < diamonds; i++)
        {
            float around = 2f * Mathf.PI * i / diamonds;
            SurfacePrint.Map band = (Vector2 uv, out Vector3 normal) =>
            {
                float a = around + uv.x / bandRadius;
                normal = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                return normal * bandRadius + Vector3.up * (bandMiddle + uv.y);
            };
            Print("Diamond", i % 2 == 0 ? Shade.Light : Shade.Main, band, SurfacePrint.Turned(SurfacePrint.Box(0.1f, 0.1f), 0f, 0f, 45f));
        }
    }

    // A plasma globe on the head: a glass ball on a ribbed metal base, a glowing core in the middle
    // that flickers, and bolts of lightning flickering out from it to the glass
    private void BuildPlasmaGlobe(float r)
    {
        const float centre = 1.42f, radius = 0.4f;
        surface = Surface.Metal;
        Generated("Base", Frustum(0.75f, true), Shade.Dark, new Vector3(0f, 0.93f * r, 0f), new Vector3(0.3f * r, 0.14f * r, 0.3f * r), Vector3.zero, true);
        Generated("Base Ring", Torus(0.12f), Shade.Light, new Vector3(0f, 0.95f * r, 0f), Vector3.one * (0.29f * r), Vector3.zero, false);
        Primitive("Neck", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, 1.08f * r, 0f), new Vector3(0.3f * r, 0.03f * r, 0.3f * r), Vector3.zero, false);
        surface = Surface.Glow;
        Renderer power = Primitive("Power Light", PrimitiveType.Sphere, Shade.Main, new Vector3(0f, 0.99f * r, 0.23f * r), Vector3.one * (0.04f * r), Vector3.zero, false);
        HatMotion.Add(power.gameObject, HatMotion.Kind.Pulse, Vector3.up, 1f, 0.4f);
        Transform arcs = Pivot("Arcs", new Vector3(0f, centre * r, 0f));
        Renderer core = Primitive("Core", PrimitiveType.Sphere, Shade.Main, Vector3.zero, Vector3.one * (0.14f * r), Vector3.zero, false, arcs);
        HatMotion.Add(core.gameObject, HatMotion.Kind.Twinkle, Vector3.up, 4f, 0.6f);
        Primitive("Core Glow", PrimitiveType.Sphere, Shade.Light, Vector3.zero, Vector3.one * (0.08f * r), Vector3.zero, false, arcs);
        const int bolts = 7;
        Transform[] segments = new Transform[bolts * 2];
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i] = Primitive("Bolt", PrimitiveType.Cylinder, Shade.Light, Vector3.zero, Vector3.one * (0.02f * r), Vector3.zero, false, arcs).transform;
        }
        arcs.gameObject.AddComponent<HatArcs>().Setup(segments, (radius - 0.03f) * r, 0.02f * r);
        surface = Surface.Glass;
        Primitive("Globe", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, centre * r, 0f), Vector3.one * (2f * radius * r), Vector3.zero, true);
    }

    // A little flying saucer hovering over the head, bobbing and rocking: a metal hull with a glass
    // dome and a little alien inside, a ring of blinking lights going round its rim, a glowing ring
    // underneath, and a see-through tractor beam shining down onto the head
    private void BuildUfo(float r)
    {
        Transform hover = Pivot("Saucer", new Vector3(0f, 1.5f * r, 0f));
        HatMotion.Add(hover.gameObject, HatMotion.Kind.Bob, Vector3.up, 0.6f, 0.05f * r);
        Transform saucer = Pivot("Rock", Vector3.zero, Vector3.zero, hover);
        HatMotion.Add(saucer.gameObject, HatMotion.Kind.Sweep, new Vector3(1f, 0f, 0.6f), 0.35f, 6f);
        surface = Surface.Metal;
        Primitive("Hull", PrimitiveType.Sphere, Shade.Main, Vector3.zero, new Vector3(1.5f * r, 0.3f * r, 1.5f * r), Vector3.zero, false, saucer);
        Generated("Hull Rim", Torus(0.05f), Shade.Light, Vector3.zero, Vector3.one * (0.75f * r), Vector3.zero, false, saucer);
        Primitive("Underside", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, -0.12f * r, 0f), new Vector3(0.6f * r, 0.03f * r, 0.6f * r), Vector3.zero, false, saucer);
        surface = Surface.Glow;
        Renderer engine = Generated("Engine Ring", Torus(0.12f), Shade.Light, new Vector3(0f, -0.13f * r, 0f), Vector3.one * (0.26f * r), Vector3.zero, false, saucer);
        HatMotion.Add(engine.gameObject, HatMotion.Kind.Pulse, Vector3.up, 1.2f, 0.6f);

        Transform ring = Pivot("Lights", Vector3.zero, Vector3.zero, saucer);
        ring.gameObject.AddComponent<HatSpinner>().SetSteady(120f);
        const int lights = 8;
        for (int i = 0; i < lights; i++)
        {
            float a = i * Mathf.PI * 2f / lights;
            Renderer light = Primitive("Light", PrimitiveType.Sphere, i % 2 == 0 ? Shade.Light : Shade.Main, new Vector3(Mathf.Sin(a) * 0.68f * r, 0f, Mathf.Cos(a) * 0.68f * r), Vector3.one * (0.12f * r), Vector3.zero, false, ring);
            HatMotion.Add(light.gameObject, HatMotion.Kind.Pulse, Vector3.up, 1.5f, 0.8f);
        }

        // The pilot: a round head with big dark eyes and two little antennae
        surface = Surface.Plastic;
        Vector3 pilot = new Vector3(0f, 0.2f * r, 0f);
        Primitive("Alien", PrimitiveType.Sphere, Shade.Light, pilot, new Vector3(0.26f * r, 0.24f * r, 0.24f * r), Vector3.zero, false, saucer);
        for (int side = -1; side <= 1; side += 2)
        {
            Primitive("Alien Eye", PrimitiveType.Sphere, Shade.Dark, pilot + new Vector3(0.055f * side * r, 0.01f * r, 0.1f * r), new Vector3(0.07f * r, 0.09f * r, 0.04f * r), new Vector3(0f, 0f, 25f * side), false, saucer);
            Primitive("Alien Antenna", PrimitiveType.Cylinder, Shade.Light, pilot + new Vector3(0.06f * side * r, 0.16f * r, 0f), new Vector3(0.015f * r, 0.05f * r, 0.015f * r), new Vector3(0f, 0f, -20f * side), false, saucer);
            surface = Surface.Glow;
            Primitive("Alien Antenna Tip", PrimitiveType.Sphere, Shade.Light, pilot + new Vector3(0.08f * side * r, 0.21f * r, 0f), Vector3.one * (0.035f * r), Vector3.zero, false, saucer);
            surface = Surface.Plastic;
        }

        surface = Surface.Glass;
        Primitive("Dome", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, 0.1f * r, 0f), new Vector3(0.6f * r, 0.45f * r, 0.6f * r), Vector3.zero, false, saucer);
        // The beam: a cone widening downwards, from inside the hull to inside the head
        Generated("Beam", Frustum(0.3f, false), Shade.Light, new Vector3(0f, -0.85f * r, 0f), new Vector3(0.7f * r, 0.8f * r, 0.7f * r), Vector3.zero, false, saucer);
        StillBox(new Vector3(0f, 1.5f * r, 0f), new Vector3(1.5f * r, 0.35f * r, 1.5f * r));
    }

    // An atom floating over the head: a glowing, pulsing core with three tilted rings round it, a
    // glowing electron going round each ring at its own speed, and the whole atom slowly turning
    private void BuildAtom(float r)
    {
        const float centre = 1.55f, ring = 0.45f;
        Transform atom = Pivot("Atom", new Vector3(0f, centre * r, 0f));
        HatMotion.Add(atom.gameObject, HatMotion.Kind.Spin, Vector3.up, 30f);
        surface = Surface.Glow;
        // The nucleus: a cluster of balls in two shades
        for (int i = 0; i < 7; i++)
        {
            Vector3 at = i == 0 ? Vector3.zero : Fibonacci(i - 1, 6) * (0.07f * r);
            Renderer nucleon = Primitive("Nucleon", PrimitiveType.Sphere, i % 2 == 0 ? Shade.Light : Shade.Main, at, Vector3.one * (0.13f * r), Vector3.zero, false, atom);
            HatMotion.Add(nucleon.gameObject, HatMotion.Kind.Pulse, Vector3.up, 1.1f, 0.4f);
        }
        float[] speeds = { 220f, -280f, 340f };
        for (int i = 0; i < speeds.Length; i++)
        {
            // Each ring tipped 65 degrees, turned a third of the way round from the last
            Transform plane = Pivot("Ring Plane", Vector3.zero, new Vector3(0f, i * 60f, 65f), atom);
            surface = Surface.Metal;
            Generated("Ring", Torus(0.06f), Shade.Main, Vector3.zero, Vector3.one * (ring * r), Vector3.zero, false, plane);
            Transform orbit = Pivot("Orbit", Vector3.zero, Vector3.zero, plane);
            orbit.gameObject.AddComponent<HatSpinner>().SetSteady(speeds[i]);
            surface = Surface.Glow;
            Primitive("Electron", PrimitiveType.Sphere, Shade.Light, new Vector3(ring * r, 0f, 0f), Vector3.one * (0.1f * r), Vector3.zero, false, orbit);
            // A short fading trail behind it
            for (int t = 1; t <= 3; t++)
            {
                float back = -Mathf.Sign(speeds[i]) * t * 9f * Mathf.Deg2Rad;
                Primitive("Trail", PrimitiveType.Sphere, Shade.Main, new Vector3(Mathf.Cos(back), 0f, Mathf.Sin(back)) * (ring * r), Vector3.one * ((0.1f - 0.022f * t) * r), Vector3.zero, false, orbit);
            }
        }
        StillBox(new Vector3(0f, centre * r, 0f), Vector3.one * (2f * ring * r));
    }

    // Devil horns: a big glossy horn on each side of the top of the head, thick and ringed at its
    // root, sweeping out to the side, then up, then hooking forward and in to a sharp point, with a
    // dark tip that smoulders
    private void BuildDevilHorns(float r)
    {
        const float thickness = 0.28f;
        surface = Surface.Plastic;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 root = new Vector3(0.5f * side, 0.8f, 0.2f).normalized * (0.94f * r);
            Vector3[] ways =
            {
                new Vector3(1f * side, 0.55f, 0.1f), new Vector3(0.8f * side, 1f, 0.15f), new Vector3(0.25f * side, 1f, 0.3f),
                new Vector3(-0.2f * side, 0.85f, 0.55f), new Vector3(-0.4f * side, 0.35f, 0.8f),
            };
            float[] lengths = { 0.3f * r, 0.3f * r, 0.28f * r, 0.24f * r, 0.2f * r };
            Horn("Horn", root, ways, lengths, thickness * r, Shade.Main, true);
            // Rings round the thick first stretch, like a ram's or a goat's
            Vector3 rootWay = ways[0].normalized;
            Vector3 ringEuler = Quaternion.FromToRotation(Vector3.up, rootWay).eulerAngles;
            for (int i = 0; i < 4; i++)
            {
                float along = (0.03f + 0.07f * i) * r;
                float radius = thickness * (1f - along / Sum(lengths)) * 0.98f;
                Generated("Horn Ridge", Torus(0.18f), Shade.Dark, root + rootWay * along, Vector3.one * (radius * r), ringEuler, false);
            }
            // The last stretch in the dark shade, glowing faintly at the very point
            Vector3 tipFrom = root;
            for (int i = 0; i < ways.Length - 1; i++) tipFrom += ways[i].normalized * lengths[i];
            Vector3 tipWay = ways[ways.Length - 1].normalized;
            float tipFromRadius = thickness * lengths[ways.Length - 1] / Sum(lengths);
            Generated("Horn Tip", Frustum(0f, false), Shade.Dark, tipFrom + tipWay * (0.003f * r), new Vector3(tipFromRadius * 1.04f * r, lengths[ways.Length - 1] * r, tipFromRadius * 1.04f * r), Quaternion.FromToRotation(Vector3.up, tipWay).eulerAngles, false);
            surface = Surface.Glow;
            Renderer ember = Primitive("Ember", PrimitiveType.Sphere, Shade.Light, tipFrom + tipWay * (lengths[ways.Length - 1] * 0.9f * r), Vector3.one * (0.035f * r), Vector3.zero, false);
            HatMotion.Add(ember.gameObject, HatMotion.Kind.Pulse, Vector3.up, 0.6f, 0.6f);
            surface = Surface.Plastic;
        }
    }

    private static float Sum(float[] values)
    {
        float total = 0f;
        foreach (float v in values) total += v;
        return total;
    }

    #endregion

    #region Parts

    // The dome most hats are built on, hugging the top of the head. On a ragdoll its collider is a
    // coarser convex copy: a convex collider's hull must stay small, and nobody sees it.
    private void Crown(Vector3 scale)
    {
        Renderer crown = Generated("Crown", Dome(), Shade.Main, Vector3.zero, scale, Vector3.zero, false);
        Line(crown, LiningDome());
        if (solid)
        {
            MeshCollider collider = crown.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = colliderDome != null ? colliderDome : (colliderDome = BuildDome(4, 12, "Cap Crown Collider", false));
            collider.convex = true; // needed on a moving Rigidbody
        }
    }

    // A lining just inside a hat's body (inside: the same shape with only its inside faces), in the
    // hat's dark shade and satin, like a real hat's: it only shows when the hat is knocked off the
    // head and lands showing its inside. A hair smaller than the body, so it's what's seen from in
    // there rather than the inside of the body itself.
    private void Line(Renderer outer, Mesh inside)
    {
        Surface was = surface;
        surface = Surface.Satin;
        Transform t = outer.transform;
        Vector3 s = t.localScale;
        Generated(outer.name + " Lining", inside, Shade.Dark, t.localPosition, new Vector3(s.x * 0.985f, s.y * 0.99f, s.z * 0.985f), t.localEulerAngles, false, t.parent);
        surface = was;
    }

    // Seams over the top of a dome of radius radius (of the head's), count of them crossing at the top
    // so they split it into twice as many panels. stretch: how much taller than wide the dome is.
    private void Seams(float r, float radius, int count, Shade shade, float stretch = 1f)
    {
        for (int i = 0; i < count; i++)
        {
            // Each seam is two narrow strips of the dome's surface, from the edge up to the top and down
            // the far side. (Strips on the surface, not a sheet through the hat: from inside a hat knocked
            // off the head a sheet would show.)
            for (int half = 0; half < 2; half++)
            {
                Generated("Seam", DomeSlice(2f), shade, Vector3.zero, new Vector3(radius * 1.006f * r, radius * stretch * 1.006f * r, radius * 1.006f * r), new Vector3(0f, 180f * i / count + 180f * half, 0f), false);
            }
        }
    }

    // Upright ribs round a frustum part (bottom at y, bottom radius radius, top radius taper of that,
    // height height): count thin bars of width width standing on its side
    // rounded: soft round folds (cylinders) instead of square-edged bars
    private void Ribs(string name, Shade shade, float y, float radius, float taper, float height, int count, float width, Transform parent = null, bool rounded = false)
    {
        float middle = radius * (1f + taper) * 0.5f;
        float lean = Mathf.Atan2(radius * (1f - taper), height) * Mathf.Rad2Deg;
        for (int i = 0; i < count; i++)
        {
            Quaternion around = Quaternion.Euler(0f, 360f * i / count, 0f);
            Primitive(name, rounded ? PrimitiveType.Cylinder : PrimitiveType.Cube, shade, around * Vector3.forward * middle + Vector3.up * (y + height * 0.5f), new Vector3(width, height * (rounded ? 0.51f : 1.02f), width * 0.8f), (around * Quaternion.Euler(-lean, 0f, 0f)).eulerAngles, false, parent);
        }
    }

    // A horn (or anything like one) of joined tapering pieces: from root, each piece going the next
    // way for its length, from radius at the root down to a point at the tip. collide: the first
    // piece gets a collider on a ragdoll's hat.
    private void Horn(string name, Vector3 root, Vector3[] ways, float[] lengths, float radius, Shade shade, bool collide, Transform parent = null)
    {
        float total = 0f;
        foreach (float l in lengths) total += l;
        Vector3 at = root;
        float done = 0f;
        for (int i = 0; i < ways.Length; i++)
        {
            float from = radius * (1f - done / total);
            done += lengths[i];
            float to = radius * (1f - done / total);
            Vector3 way = ways[i].normalized;
            Generated(name, Frustum(from > 0f ? Mathf.Round(to / from * 50f) / 50f : 0f, false), shade, at, new Vector3(from, lengths[i], from), Quaternion.FromToRotation(Vector3.up, way).eulerAngles, collide && i == 0, parent);
            // A ball in each bend, so the pieces join smoothly
            // A ball in each bend, so the pieces join smoothly, and one closing the root
            Primitive(name + (i > 0 ? " Joint" : " Root"), PrimitiveType.Sphere, shade, at, Vector3.one * (2f * from), Vector3.zero, false, parent);
            at += way * lengths[i];
        }
    }

    // The i-th of n directions spread evenly over a sphere
    private static Vector3 Fibonacci(int i, int n)
    {
        float y = 1f - 2f * (i + 0.5f) / n;
        float ring = Mathf.Sqrt(1f - y * y);
        float a = i * Mathf.PI * (3f - Mathf.Sqrt(5f));
        return new Vector3(Mathf.Cos(a) * ring, y, Mathf.Sin(a) * ring);
    }

    // A band round a frustum part (bottom at y, bottom radius radius, top radius taper of that, height
    // height), from from to to above its bottom, a hair outside it. depth stretches it front to back,
    // for a part stretched the same way.
    private void Stripe(string name, Shade shade, float y, float radius, float taper, float height, float from, float to, float depth = 1f, Transform parent = null)
    {
        float r0 = radius * (1f - (1f - taper) * from / height) * 1.02f;
        float r1 = radius * (1f - (1f - taper) * to / height) * 1.02f;
        Generated(name, Frustum(r1 / r0, false), shade, new Vector3(0f, y + from, 0f), new Vector3(r0, to - from, r0 * depth), Vector3.zero, false, parent);
    }

    // Prints: flat shapes on a hat's surface, like paint, print or embroidery (see SurfacePrint),
    // following its curve a hair out from it instead of standing off it. A shape printed over another
    // goes a layer further out. In head radii: the first layer's lift, each next one's, and the
    // longest edge in a print's mesh.
    private const float PrintLift = 0.006f, PrintLayerStep = 0.005f, PrintStep = 0.03f;

    // Prints an outline (head radii across the surface, see SurfacePrint) where map puts it. The part
    // sits at the print's middle, so it scales round it (a twinkling star).
    private Renderer Print(string name, Shade shade, SurfacePrint.Map map, System.Collections.Generic.List<Vector2> outline, int layer = 0, Transform parent = null)
    {
        float r = headRadius;
        for (int k = 0; k < outline.Count; k++) outline[k] *= r;
        Mesh mesh = SurfacePrint.Build("Hat " + name, outline, map, (PrintLift + layer * PrintLayerStep) * r, PrintStep * r, 0.3f / r, out Vector3 at);
        printMeshes.Add(mesh);
        return Generated(name, mesh, shade, at, Vector3.one, Vector3.zero, false, parent);
    }

    // A shape for Print: an outline (head radii) moved to x, y across the surface and turned by roll
    private static System.Collections.Generic.List<Vector2> At(System.Collections.Generic.List<Vector2> outline, float x, float y, float roll = 0f) => SurfacePrint.Turned(outline, x, y, roll);

    // The surface of a dome (an ellipsoid round centre with these radii, metres) for Print, with the
    // print's middle where the way out from the centre is way. across and up: which ways the print's
    // x and y go there (along the surface, as near as they can), or by default up as near world up
    // as it can be.
    private static SurfacePrint.Map OnEllipsoid(Vector3 centre, Vector3 radii, Vector3 way, Vector3 across = default, Vector3 up = default)
    {
        Vector3 dir = way.normalized;
        Vector3 local = dir / new Vector3(dir.x / radii.x, dir.y / radii.y, dir.z / radii.z).magnitude;
        Vector3 normal = new Vector3(local.x / (radii.x * radii.x), local.y / (radii.y * radii.y), local.z / (radii.z * radii.z)).normalized;
        if (across == Vector3.zero) SurfacePrint.Frame(normal, out across, out up);
        else
        {
            Vector3 wanted = up;
            across = Vector3.ProjectOnPlane(across, normal).normalized;
            up = Vector3.Cross(normal, across).normalized;
            // Keep up the way it was asked for (the cross could point it the other way)
            if (Vector3.Dot(up, wanted) < 0f) up = -up;
        }
        return SurfacePrint.Ellipsoid(centre, radii, centre + local, across, up);
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in printMeshes)
        {
            if (mesh != null) Destroy(mesh);
        }
        printMeshes.Clear();
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
    private Renderer Generated(string name, Mesh mesh, Shade shade, Vector3 position, Vector3 scale, Vector3 euler, bool collide, Transform parent = null)
    {
        GameObject part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        return AddPart(part, name, shade, position, scale, euler, collide, parent);
    }

    // An empty object for moving parts to go under (spinning, swinging or bobbing): parts placed under
    // it are placed from it, and move with it. Moving parts never get colliders (see StillBox).
    private Transform Pivot(string name, Vector3 position, Vector3 euler = default, Transform parent = null)
    {
        GameObject pivot = new GameObject(name);
        pivot.layer = gameObject.layer;
        pivot.transform.SetParent(parent != null ? parent : transform, false);
        pivot.transform.localPosition = position;
        pivot.transform.localRotation = Quaternion.Euler(euler);
        return pivot.transform;
    }

    // On a ragdoll's hat: a still box collider round moving parts, so the hat lands on them instead of
    // sinking them into the floor. (A collider moving inside a rigidbody every frame makes it jitter.)
    private void StillBox(Vector3 centre, Vector3 size)
    {
        if (!solid) return;
        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.center = centre;
        box.size = size;
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
        partSurfaces.Add(surface);
        partGlows.Add(glowBoost);
        return renderer;
    }

    #endregion

    // The colour of the cap in the logo, used on the main menu's player model
    public static readonly Color LogoColor = new Color(0.01f, 0.42f, 0.68f);

    // Puts a hat on every player head in the scene, e.g. the main menu's player model, which is a copy
    // of the player without the Player script. Run again, it swaps the hat they wear for this one.
    // locked: as the grey preview of a hat not unlocked yet (see ShowLocked).
    public static void AttachToSceneHeads(Color color, int hat = Hats.Cap, bool locked = false)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!t.CompareTag("Head")) continue;
            if (t.GetComponent<MeshFilter>() == null) continue; // only visible sphere heads
            foreach (PlayerCap old in t.GetComponentsInChildren<PlayerCap>(true)) Destroy(old.gameObject);
            PlayerCap cap = Attach(t, color, false, hat);
            if (locked && cap != null) cap.ShowLocked();
        }
    }

    private static readonly Color[] SafeColors =
    {
        new Color(0.90f, 0.62f, 0f),    // orange
        new Color(0.34f, 0.71f, 0.91f), // sky blue
        new Color(0f, 0.62f, 0.45f),    // bluish green
        new Color(0.94f, 0.89f, 0.26f), // yellow
        new Color(0f, 0.45f, 0.70f),    // blue
        new Color(0.84f, 0.37f, 0f),    // vermillion
        new Color(0.80f, 0.47f, 0.65f), // reddish purple
        new Color(0.92f, 0.92f, 0.92f), // white
    };

    // The cap colour for a player, the same on every client
    public static Color ColorFor(int actor)
    {
        Manager m = Manager.Instance;
        if (m != null && m.IsTeamMode)
        {
            int team = m.GetTeam(actor);
            if (team >= 0) return Manager.TeamColors[team % 2];
        }
        // With the colour blind setting: the Okabe-Ito colours, which stay apart with any colour vision
        if (GameSettings.Colourblind) return SafeColors[((actor % SafeColors.Length) + SafeColors.Length) % SafeColors.Length];
        // Golden-ratio steps round the colour wheel keep neighbouring actor numbers far apart in colour
        float hue = Mathf.Repeat(actor * 0.618034f, 1f);
        return Color.HSVToRGB(hue, 0.7f, 0.9f);
    }

    private static Mesh domeMesh, colliderDome;
    private static readonly System.Collections.Generic.Dictionary<string, Mesh> meshes = new System.Collections.Generic.Dictionary<string, Mesh>();

    private static Mesh Dome() => domeMesh != null ? domeMesh : (domeMesh = BuildDome(10, 24, "Cap Crown", true));

    // The dome's inside faces only, for a lining (see Line)
    private static Mesh liningDome;
    private static Mesh LiningDome()
    {
        if (liningDome != null) return liningDome;
        liningDome = BuildDome(10, 24, "Hat Lining", true);
        // The double-sided dome's triangles are the outside, then the inside: keep the inside
        int[] all = liningDome.triangles;
        int[] inside = new int[all.Length / 2];
        System.Array.Copy(all, all.Length / 2, inside, 0, inside.Length);
        liningDome.triangles = inside;
        liningDome.RecalculateBounds();
        return liningDome;
    }

    // A wedge of the dome, sweep degrees round from +x towards +z: a panel of a cap
    private static Mesh DomeSlice(float sweep)
    {
        string key = "Dome Slice " + sweep.ToString("0.#");
        Mesh mesh;
        if (meshes.TryGetValue(key, out mesh) && mesh != null) return mesh;
        mesh = BuildDome(10, Mathf.Max(2, Mathf.CeilToInt(sweep / 15f)), "Hat " + key, true, sweep);
        meshes[key] = mesh;
        return mesh;
    }

    // A round tube 1 high with a bottom radius of 1 and a top radius of topRadius (0 for a cone),
    // seen from inside and out; capTop closes its top with a flat disc
    // insideOnly: only the inside faces, for a lining (see Line)
    private static Mesh Frustum(float topRadius, bool capTop, bool insideOnly = false)
    {
        string key = "Frustum " + topRadius.ToString("0.###") + (capTop ? " capped" : "") + (insideOnly ? " inside" : "");
        Mesh mesh;
        if (meshes.TryGetValue(key, out mesh) && mesh != null) return mesh;

        const int segments = 24;
        var vertices = new System.Collections.Generic.List<Vector3>();
        var normals = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var triangles = new System.Collections.Generic.List<int>();

        // The side: a ring at the bottom and one at the top. Its outward normal leans up as it narrows.
        for (int side = insideOnly ? 1 : 0; side < 2; side++)
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

        // The top, like the side seen from inside too: from under the hat (a hat knocked off and
        // landed upside down, or the camera inside the brim) it would otherwise be a hole
        for (int side = insideOnly ? 1 : 0; capTop && topRadius > 0f && side < 2; side++)
        {
            Vector3 normal = side == 0 ? Vector3.up : Vector3.down;
            int centre = vertices.Count;
            vertices.Add(Vector3.up); normals.Add(normal); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int j = 0; j <= segments; j++)
            {
                float a = 2f * Mathf.PI * j / segments;
                vertices.Add(new Vector3(Mathf.Cos(a) * topRadius, 1f, Mathf.Sin(a) * topRadius)); normals.Add(normal);
                uvs.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a)));
            }
            for (int j = 0; j < segments; j++)
            {
                if (side == 0) { triangles.Add(centre); triangles.Add(centre + j + 2); triangles.Add(centre + j + 1); }
                else { triangles.Add(centre); triangles.Add(centre + j + 1); triangles.Add(centre + j + 2); }
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

    // The shape of a hat's brim, in head radii, round the crown's foot (y 0): from the inner edge (an
    // ellipse, tucked under the crown) out to the outer edge, which is an ellipse, or a triangle with
    // rounded corners when apothem is set (a tricorne: a flat side to the front). The brim rises
    // towards its edge by curl * (how far out)^power: sideCurl at the sides, frontCurl at the front
    // and back (negative: it dips), blended in between.
    private struct BrimShape
    {
        public float innerX, innerZ, outerX, outerZ;
        public float apothem, cornerCap;
        public float sideCurl, frontCurl, power, thickness;

        public string Key => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.###} {1:0.###} {2:0.###} {3:0.###} {4:0.###} {5:0.###} {6:0.###} {7:0.###} {8:0.###} {9:0.###}",
            innerX, innerZ, outerX, outerZ, apothem, cornerCap, sideCurl, frontCurl, power, thickness);
    }

    // A point on a brim's middle surface: a is radians round from the front (+z) towards +x, t how
    // far out (0 at the crown, 1 at the edge)
    private static Vector3 BrimPoint(BrimShape b, float a, float t)
    {
        float sin = Mathf.Sin(a), cos = Mathf.Cos(a);
        float inner = 1f / Mathf.Sqrt(sin * sin / (b.innerX * b.innerX) + cos * cos / (b.innerZ * b.innerZ));
        float outer;
        if (b.apothem > 0f)
        {
            float side = Mathf.Repeat(a * Mathf.Rad2Deg + 60f, 120f) - 60f;
            outer = Mathf.Min(b.apothem / Mathf.Cos(side * Mathf.Deg2Rad), b.apothem * b.cornerCap);
        }
        else
        {
            outer = 1f / Mathf.Sqrt(sin * sin / (b.outerX * b.outerX) + cos * cos / (b.outerZ * b.outerZ));
        }
        float curl = b.sideCurl * sin * sin + b.frontCurl * cos * cos;
        return new Vector3(sin, 0f, cos) * Mathf.Lerp(inner, outer, t) + Vector3.up * (curl * Mathf.Pow(t, b.power));
    }

    // The way a brim's top faces at a point (up, or in towards the crown where it curls up)
    private static Vector3 BrimNormal(BrimShape b, float a, float t)
    {
        const float e = 0.002f;
        Vector3 around = BrimPoint(b, a + e, t) - BrimPoint(b, a - e, t);
        Vector3 outward = BrimPoint(b, a, Mathf.Min(1f, t + e)) - BrimPoint(b, a, Mathf.Max(0f, t - e));
        return Vector3.Cross(outward, around).normalized;
    }

    // The way out along a brim's surface towards its edge at a point
    private static Vector3 BrimOutward(BrimShape b, float a, float t)
    {
        const float e = 0.002f;
        return (BrimPoint(b, a, Mathf.Min(1f, t + e)) - BrimPoint(b, a, Mathf.Max(0f, t - e))).normalized;
    }

    // A brim as one closed piece: a top and an underside a thickness apart. (Its inner edge is open,
    // but it's tucked under the crown.)
    private static Mesh BrimMesh(BrimShape b)
    {
        string key = "Brim " + b.Key;
        Mesh mesh;
        if (meshes.TryGetValue(key, out mesh) && mesh != null) return mesh;

        const int rings = 10, segments = 72;
        var vertices = new System.Collections.Generic.List<Vector3>();
        var normals = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var triangles = new System.Collections.Generic.List<int>();
        for (int face = 0; face < 2; face++)
        {
            float flip = face == 0 ? 1f : -1f;
            int start = vertices.Count;
            for (int i = 0; i <= rings; i++)
            {
                float t = (float)i / rings;
                for (int j = 0; j < segments; j++)
                {
                    float a = 2f * Mathf.PI * j / segments;
                    Vector3 n = BrimNormal(b, a, t) * flip;
                    vertices.Add(BrimPoint(b, a, t) + n * (b.thickness * 0.5f));
                    normals.Add(n);
                    uvs.Add(new Vector2(4f * j / segments, t));
                }
            }
            for (int i = 0; i < rings; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    int k = (j + 1) % segments;
                    int a0 = start + i * segments + j, a1 = start + i * segments + k;
                    int b0 = a0 + segments, b1 = a1 + segments;
                    Quad(vertices, normals, triangles, a0, b0, a1, b1);
                }
            }
        }
        mesh = FinishMesh(key, vertices, normals, uvs, triangles);
        meshes[key] = mesh;
        return mesh;
    }

    // A round tube along a brim's outer edge: its rolled edge, or a trim
    private static Mesh BrimEdge(BrimShape b, float radius)
    {
        string key = "Brim Edge " + b.Key + " " + radius.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        Mesh mesh;
        if (meshes.TryGetValue(key, out mesh) && mesh != null) return mesh;

        const int segments = 72, sides = 10;
        var vertices = new System.Collections.Generic.List<Vector3>();
        var normals = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var triangles = new System.Collections.Generic.List<int>();
        for (int j = 0; j < segments; j++)
        {
            float a = 2f * Mathf.PI * j / segments;
            Vector3 centre = BrimPoint(b, a, 1f);
            Vector3 up = BrimNormal(b, a, 1f);
            Vector3 outward = BrimOutward(b, a, 1f);
            for (int k = 0; k < sides; k++)
            {
                float c = 2f * Mathf.PI * k / sides;
                Vector3 n = outward * Mathf.Cos(c) + up * Mathf.Sin(c);
                vertices.Add(centre + n * radius);
                normals.Add(n);
                uvs.Add(new Vector2(8f * j / segments, (float)k / sides));
            }
        }
        for (int j = 0; j < segments; j++)
        {
            int jn = (j + 1) % segments;
            for (int k = 0; k < sides; k++)
            {
                int kn = (k + 1) % sides;
                Quad(vertices, normals, triangles, j * sides + k, jn * sides + k, j * sides + kn, jn * sides + kn);
            }
        }
        mesh = FinishMesh(key, vertices, normals, uvs, triangles);
        meshes[key] = mesh;
        return mesh;
    }

    // Two triangles across a quad (a0 a1 along one edge, b0 b1 along the next), each wound so it
    // faces the way its corners' normals point
    private static void Quad(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector3> n, System.Collections.Generic.List<int> triangles, int a0, int b0, int a1, int b1)
    {
        Triangle(v, n, triangles, a0, b0, a1);
        Triangle(v, n, triangles, a1, b0, b1);
    }

    private static void Triangle(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector3> n, System.Collections.Generic.List<int> triangles, int a, int b, int c)
    {
        // Unity draws a triangle's front where Cross(b - a, c - a) points
        Vector3 facing = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (Vector3.Dot(facing, n[a] + n[b] + n[c]) < 0f)
        {
            int swap = b;
            b = c;
            c = swap;
        }
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
    }

    private static Mesh FinishMesh(string name, System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.List<Vector3> normals, System.Collections.Generic.List<Vector2> uvs, System.Collections.Generic.List<int> triangles)
    {
        Mesh mesh = new Mesh { name = "Hat " + name };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    // A ring lying flat, 1 from its centre to the middle of its tube, the tube thickness/2 across.
    // sweep: how much of the ring there is, in degrees round from +x towards +z (all of it at 360).
    private static Mesh Torus(float thickness, float sweep = 360f)
    {
        string key = "Torus " + thickness.ToString("0.###") + " " + sweep.ToString("0.#");
        Mesh mesh;
        if (meshes.TryGetValue(key, out mesh) && mesh != null) return mesh;

        const int segments = 32, sides = 10;
        float tube = thickness * 0.5f;
        Vector3[] vertices = new Vector3[(segments + 1) * (sides + 1)];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] torusUvs = new Vector2[vertices.Length];
        for (int i = 0; i <= segments; i++)
        {
            float a = sweep * Mathf.Deg2Rad * i / segments;
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
    private static Mesh BuildDome(int rings, int segments, string name, bool doubleSided, float sweep = 360f)
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
                float around = sweep * Mathf.Deg2Rad * j / segments;
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
