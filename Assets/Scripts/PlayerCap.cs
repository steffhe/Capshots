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
    private static Material capMaterial;

    // The hat this is (see Hats)
    public int Hat { get; private set; }

    // While building: the parts so far, and whether they get colliders (a ragdoll's hat)
    private readonly System.Collections.Generic.List<Renderer> parts = new System.Collections.Generic.List<Renderer>();
    private readonly System.Collections.Generic.List<Shade> partShades = new System.Collections.Generic.List<Shade>();
    private readonly System.Collections.Generic.List<Surface> partSurfaces = new System.Collections.Generic.List<Surface>();
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
        cap.surfaces = cap.partSurfaces.ToArray();
        cap.parts.Clear();
        cap.partShades.Clear();
        cap.partSurfaces.Clear();
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
            block.SetColor("_EmissionColor", kind == Surface.Glow && !locked ? c * GlowStrength : Color.black);
            renderers[i].SetPropertyBlock(block);
        }
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
            case Hats.ChefsHat: BuildChefsHat(r); break;
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
        // A round badge on the front panel
        OnDome(r, 1.07f, "Badge", PrimitiveType.Cylinder, Shade.Light, 0f, 48f * facing, new Vector3(0.36f * r, 0.012f * r, 0.36f * r));
        OnDome(r, 1.07f, "Badge Middle", PrimitiveType.Cylinder, Shade.Dark, 0f, 48f * facing, new Vector3(0.18f * r, 0.02f * r, 0.18f * r));
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
        HatDangle.Add(pom.gameObject, Vector3.up, 0.18f * r, 260f, 0.35f, 3f);
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
        Primitive("Brim", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, sit * r, 0f), new Vector3(2.8f * r, 0.025f * r, 2.8f * r), Vector3.zero, true);
        // The brim's edge rolled up a little, higher at the sides than front and back
        for (int side = -1; side <= 1; side += 2)
        {
            Generated("Brim Roll", Torus(0.05f, 140f), Shade.Dark, new Vector3(0f, (sit + 0.05f) * r, 0f), new Vector3(1.38f * r, 1.38f * r, 1.38f * r), new Vector3(0f, side > 0 ? -70f : 110f, 6f * side), false);
        }
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
        HatMotion.Add(ring.gameObject, HatMotion.Kind.Pulse, Vector3.up, 0.35f, 0.35f);
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
            HatDangle.Add(cone.gameObject, Vector3.up, height * r, 320f, 0f, 3.5f);
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

    // A chef's hat: a band round the head, a tall pleated column above it that widens a little going
    // up, and a soft puffy top billowing out just past the column. The column and top sway and
    // jiggle a little as the player moves.
    private void BuildChefsHat(float r)
    {
        const float bandY = 0.22f, bandHeight = 0.24f, radius = 1.0f;
        const float columnHeight = 1.15f, flare = 1.06f;
        surface = Surface.Twill;
        Line(Generated("Band", Frustum(1.02f, false), Shade.Main, new Vector3(0f, bandY * r, 0f), new Vector3(radius * r, bandHeight * r, radius * r), Vector3.zero, true), Frustum(1.02f, false, true));
        Generated("Band Edge", Torus(0.04f), Shade.Main, new Vector3(0f, (bandY + bandHeight) * r, 0f), Vector3.one * (radius * 1.025f * r), Vector3.zero, false);

        // A ragdoll's hat stays still: its colliders can't move inside the falling hat
        float columnY = (bandY + bandHeight) * r;
        Transform top = transform;
        Vector3 at = new Vector3(0f, columnY, 0f);
        if (!solid)
        {
            top = Pivot("Column", at);
            HatDangle.Add(top.gameObject, Vector3.up, columnHeight * r, 1400f, 0.01f, 9f);
            at = Vector3.zero;
        }
        float columnRadius = radius * 1.005f;
        Line(Generated("Column", Frustum(flare, false), Shade.Light, at - new Vector3(0f, 0.01f * r, 0f), new Vector3(columnRadius * r, columnHeight * r, columnRadius * r), Vector3.zero, true, top), Frustum(flare, false, true));
        // The pleats: soft folds all the way up the column
        Ribs("Pleat", Shade.Light, at.y - 0.01f * r, columnRadius * 0.99f * r, flare, columnHeight * r, 16, 0.1f * r, top, true);

        // The puffy top: a wide soft dome, with billows round its edge and one in the middle
        float topY = at.y + columnHeight * r;
        float topRadius = columnRadius * flare;
        Primitive("Puff", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, topY + 0.3f * r, 0f), new Vector3(2.2f * topRadius * r, 1.25f * r, 2.2f * topRadius * r), Vector3.zero, true, top);
        const int billows = 7;
        for (int i = 0; i < billows; i++)
        {
            float a = (i + 0.5f) * Mathf.PI * 2f / billows;
            Vector3 way = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            Primitive("Billow", PrimitiveType.Sphere, Shade.Light, way * (0.55f * topRadius * r) + Vector3.up * (topY + 0.55f * r), new Vector3(0.9f * r, 0.85f * r, 0.9f * r), Vector3.zero, false, top);
        }
        Primitive("Billow", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, topY + 0.8f * r, 0f), new Vector3(1.0f * r, 0.7f * r, 1.0f * r), Vector3.zero, false, top);
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
            HatDangle.Add(rod.gameObject, up, length * r, 90f, 0.06f, 1.6f);
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
            HatDangle.Add(stick.gameObject, up, handle * r, 110f, 0.05f, 2f);
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

    // A cowboy hat: a creased crown, slightly longer front to back, with a band, on a brim that
    // curls up at the sides
    private void BuildCowboyHat(float r)
    {
        const float sit = 0.38f; // just above the face, so the crown is wide enough for the head inside it
        surface = Surface.Felt;
        Primitive("Brim", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, sit * r, 0f), new Vector3(1.9f * r, 0.025f * r, 3.15f * r), Vector3.zero, true);
        for (int side = -1; side <= 1; side += 2)
        {
            // Tipped up 30 degrees, its inner edge meeting the flat brim
            Primitive("Brim Curl", PrimitiveType.Cylinder, Shade.Dark, new Vector3(1.08f * r * side, (sit + 0.25f) * r, 0f), new Vector3(1.05f * r, 0.025f * r, 2.7f * r), new Vector3(0f, 0f, 30f * side), true);
        }
        Line(Generated("Crown", Frustum(0.82f, true), Shade.Main, new Vector3(0f, (sit - 0.02f) * r, 0f), new Vector3(0.97f * r, 0.8f * r, 1.08f * r), Vector3.zero, true), Frustum(0.82f, true, true));
        // The crease down the middle of the top
        Primitive("Crease", PrimitiveType.Cube, Shade.Dark, new Vector3(0f, (sit + 0.77f) * r, 0f), new Vector3(0.08f * r, 0.03f * r, 1.3f * r), Vector3.zero, false);
        // Two pinches in the front of the crown, either side of the crease
        for (int side = -1; side <= 1; side += 2)
        {
            Primitive("Pinch", PrimitiveType.Sphere, Shade.Dark, new Vector3(0.33f * r * side, (sit + 0.6f) * r, 0.78f * r), new Vector3(0.12f * r, 0.24f * r, 0.06f * r), new Vector3(-20f, 25f * side, 0f), false);
        }
        surface = Surface.Satin;
        Stripe("Band", Shade.Dark, (sit - 0.02f) * r, 0.97f * r, 0.82f, 0.8f * r, 0.06f * r, 0.2f * r, 1.08f / 0.97f);
        // A metal buckle on the front of the band, and studs round it
        surface = Surface.Metal;
        float bandY = (sit + 0.11f) * r;
        Primitive("Buckle", PrimitiveType.Cube, Shade.Light, new Vector3(0f, bandY, 1.06f * r), new Vector3(0.2f * r, 0.15f * r, 0.03f * r), new Vector3(-8f, 0f, 0f), false);
        Primitive("Buckle Middle", PrimitiveType.Cube, Shade.Dark, new Vector3(0f, bandY, 1.075f * r), new Vector3(0.11f * r, 0.07f * r, 0.02f * r), new Vector3(-8f, 0f, 0f), false);
        for (int i = 1; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            Primitive("Stud", PrimitiveType.Sphere, Shade.Light, new Vector3(Mathf.Sin(a) * 0.97f * r, bandY, Mathf.Cos(a) * 1.06f * r), Vector3.one * (0.07f * r), Vector3.zero, false);
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
            HatDangle.Add(pom.gameObject, Vector3.down, 0.2f * r, 40f, 1f, 2.5f);
            Primitive("String", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, -0.07f * r, 0f), new Vector3(0.02f * r, 0.07f * r, 0.02f * r), Vector3.zero, false, pom);
            Primitive("Pom", PrimitiveType.Sphere, i % 2 == 0 ? Shade.Light : Shade.Main, new Vector3(0f, -0.2f * r, 0f), Vector3.one * (0.14f * r), Vector3.zero, false, pom);
        }
    }

    // A pirate's tricorne: a low crown inside three upturned flaps that make a triangle round it,
    // each with a light trim along its top, and a big skull and crossbones on the front flap
    private void BuildPirateHat(float r)
    {
        const float sit = 0.48f;   // where the flaps start, up the head: just above the face
        const float inset = 0.9f;  // from the middle out to each flap, clear of the head
        const float lean = 14f;    // how far the flaps lean out

        surface = Surface.Felt;
        Line(Generated("Crown", Frustum(0.72f, true), Shade.Dark, new Vector3(0f, (sit - 0.06f) * r, 0f), new Vector3(0.93f * r, 0.7f * r, 0.93f * r), Vector3.zero, true), Frustum(0.72f, true, true));
        // A satin bead on each corner, where two flaps meet
        for (int i = 0; i < 3; i++)
        {
            const float cornerHeight = 0.5f;
            Vector3 corner = Quaternion.Euler(0f, 60f + i * 120f, 0f) * Vector3.forward * ((2f * inset + 2f * Mathf.Sin(lean * Mathf.Deg2Rad) * cornerHeight) * r) + Vector3.up * ((sit + cornerHeight * Mathf.Cos(lean * Mathf.Deg2Rad)) * r);
            surface = Surface.Satin;
            Primitive("Corner", PrimitiveType.Sphere, Shade.Light, corner, Vector3.one * (0.12f * r), Vector3.zero, false);
        }
        for (int i = 0; i < 3; i++)
        {
            // The first flap faces forwards, the others back to the sides; the front one is taller
            float height = i == 0 ? 0.62f : 0.5f;
            // Long enough that its top edge meets the next flaps at the corners
            float flapLength = 2f * (inset + Mathf.Sin(lean * Mathf.Deg2Rad) * height) * Mathf.Tan(60f * Mathf.Deg2Rad) + 0.05f;
            Quaternion turn = Quaternion.Euler(0f, i * 120f, 0f) * Quaternion.Euler(lean, 0f, 0f);
            Vector3 foot = Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * (inset * r) + Vector3.up * (sit * r);
            Vector3 middle = foot + turn * Vector3.up * (0.5f * height * r);
            surface = Surface.Felt;
            Primitive("Flap", PrimitiveType.Cube, Shade.Main, middle, new Vector3(flapLength * r, height * r, 0.06f * r), turn.eulerAngles, true);
            surface = Surface.Satin;
            Primitive("Trim", PrimitiveType.Cube, Shade.Light, foot + turn * Vector3.up * (height * r), new Vector3(flapLength * r, 0.06f * r, 0.09f * r), turn.eulerAngles, false);

            if (i != 0) continue;
            // The skull and crossbones, on the front of the front flap
            surface = Surface.Plastic;
            Vector3 face = middle + turn * new Vector3(0f, 0.05f * r, 0.05f * r);
            for (int side = -1; side <= 1; side += 2)
            {
                Primitive("Bone", PrimitiveType.Cube, Shade.Light, face + turn * new Vector3(0f, -0.04f * r, -0.01f * r), new Vector3(0.62f * r, 0.08f * r, 0.03f * r), (turn * Quaternion.Euler(0f, 0f, 32f * side)).eulerAngles, false);
                Primitive("Bone End", PrimitiveType.Sphere, Shade.Light, face + turn * new Vector3(0.27f * r * side, -0.04f * r + 0.17f * r, 0f), Vector3.one * (0.09f * r), Vector3.zero, false);
                Primitive("Bone End", PrimitiveType.Sphere, Shade.Light, face + turn * new Vector3(0.27f * r * side, -0.04f * r - 0.17f * r, 0f), Vector3.one * (0.09f * r), Vector3.zero, false);
            }
            Primitive("Skull", PrimitiveType.Sphere, Shade.Light, face + turn * new Vector3(0f, 0.05f * r, 0.02f * r), new Vector3(0.3f * r, 0.28f * r, 0.1f * r), turn.eulerAngles, false);
            Primitive("Jaw", PrimitiveType.Cube, Shade.Light, face + turn * new Vector3(0f, -0.1f * r, 0.02f * r), new Vector3(0.16f * r, 0.09f * r, 0.07f * r), turn.eulerAngles, false);
            for (int side = -1; side <= 1; side += 2)
            {
                Primitive("Eye", PrimitiveType.Sphere, Shade.Dark, face + turn * new Vector3(0.065f * r * side, 0.06f * r, 0.065f * r), new Vector3(0.08f * r, 0.08f * r, 0.03f * r), turn.eulerAngles, false);
            }
        }

        // A big feather sticking up and back out of the left side, swaying as the player moves
        Vector3 plumeWay = new Vector3(-0.55f, 1f, -0.65f).normalized;
        Transform plume = Pivot("Plume", new Vector3(-0.42f * r, (sit + 0.42f) * r, -0.2f * r), Quaternion.FromToRotation(Vector3.up, plumeWay).eulerAngles);
        HatDangle.Add(plume.gameObject, plumeWay, 0.9f * r, 70f, 0.1f, 2f);
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
        HatDangle.Add(tip.gameObject, tipWay, 0.9f * r, 60f, 0.25f, 2.2f);
        Primitive("Tip Joint", PrimitiveType.Sphere, Shade.Main, Vector3.zero, Vector3.one * (2f * joint), Vector3.zero, false, tip);
        Generated("Tip", Frustum(0f, false), Shade.Main, Vector3.zero, new Vector3(joint, 0.9f * r, joint), Vector3.zero, false, tip);
        surface = Surface.Satin;
        Stripe("Band", Shade.Light, sit * r, radius * r, narrow, lower * r, 0.03f * r, 0.18f * r);
        surface = Surface.Glow;
        Renderer bauble = Primitive("Bauble", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, 0.92f * r, 0f), Vector3.one * (0.11f * r), Vector3.zero, false, tip);
        HatMotion.Add(bauble.gameObject, HatMotion.Kind.Pulse, Vector3.up, 0.6f, 0.5f);

        // The cone's surface leans in this far from upright
        float lean = Mathf.Atan2(radius * (1f - narrow), lower) * Mathf.Rad2Deg;
        // A crescent moon on the front: a glowing disc with a felt one over most of it
        float moonH = 0.5f;
        float moonAt = radius * (1f - (1f - narrow) * moonH);
        Quaternion front = Quaternion.Euler(-lean, 0f, 0f);
        Vector3 moon = new Vector3(0f, (sit + moonH * lower) * r, moonAt * r);
        Primitive("Moon", PrimitiveType.Sphere, Shade.Light, moon + front * new Vector3(0f, 0f, 0.01f * r), new Vector3(0.26f * r, 0.26f * r, 0.02f * r), front.eulerAngles, false);
        surface = Surface.Felt;
        Primitive("Moon Shadow", PrimitiveType.Sphere, Shade.Main, moon + front * new Vector3(0.07f * r, 0.04f * r, 0.022f * r), new Vector3(0.22f * r, 0.22f * r, 0.02f * r), front.eulerAngles, false);

        // Four-pointed stars round the cone, each twinkling in its own time
        surface = Surface.Glow;
        float[] heights = { 0.3f, 0.45f, 0.62f, 0.78f, 0.3f };
        float[] angles = { 60f, -60f, 150f, 30f, -150f };
        float[] sizes = { 0.16f, 0.12f, 0.14f, 0.1f, 0.13f };
        for (int i = 0; i < heights.Length; i++)
        {
            float h = heights[i] * lower;
            float at = radius * (1f - (1f - narrow) * heights[i]) + 0.012f;
            Quaternion facing = Quaternion.Euler(0f, angles[i], 0f) * Quaternion.Euler(-lean, 0f, 0f);
            Vector3 where = Quaternion.Euler(0f, angles[i], 0f) * new Vector3(0f, 0f, at * r) + Vector3.up * ((sit + h) * r);
            Transform star = Pivot("Star", where, facing.eulerAngles);
            for (int arm = 0; arm < 2; arm++)
            {
                Renderer point = Primitive("Star", PrimitiveType.Sphere, Shade.Light, Vector3.zero, new Vector3(sizes[i] * 0.3f * r, sizes[i] * r, 0.02f * r), new Vector3(0f, 0f, 90f * arm), false, star);
                HatMotion.Add(point.gameObject, HatMotion.Kind.Twinkle, Vector3.up, 0.8f + 0.15f * i, 0.7f);
            }
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
        HatDangle.Add(tassel.gameObject, Vector3.down, 0.5f * r, 0f, 1f, 2f);
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
        Crown(new Vector3(1.06f * r, 1.1f * r, 1.06f * r));
        // The body: one soft piece over the top, pulled down over the right side
        Primitive("Body", PrimitiveType.Sphere, Shade.Main, new Vector3(0.32f * r, 0.7f * r, -0.03f * r), new Vector3(2.2f * r, 1.0f * r, 2.16f * r), new Vector3(0f, 0f, -24f), true);
        // The leather band, with its edge piped
        surface = Surface.Satin;
        Line(Generated("Band", Frustum(0.98f, false), Shade.Dark, new Vector3(0f, 0.24f * r, 0f), new Vector3(1.01f * r, 0.12f * r, 1.01f * r), Vector3.zero, true), Frustum(0.98f, false, true));
        Generated("Band Edge", Torus(0.05f), Shade.Dark, new Vector3(0f, 0.24f * r, 0f), Vector3.one * (1.01f * r), Vector3.zero, false);

        // The badge: a shield (square top, pointed bottom) with a darker shield on it, and a star in
        // the middle that catches the light now and then. Placed on the felt over the left eye, lying
        // flat against it: local x across, local z down the face.
        Quaternion on = Quaternion.Euler(0f, -24f, 0f) * Quaternion.Euler(56f, 0f, 0f);
        Vector3 badge = on * Vector3.up * (1.1f * r);
        Vector3 Across(float x, float down, float out_) => badge + on * new Vector3(x * r, out_ * r, down * r);
        Vector3 diamond = (on * Quaternion.Euler(0f, 45f, 0f)).eulerAngles;
        surface = Surface.Satin;
        Primitive("Shield", PrimitiveType.Cube, Shade.Light, Across(0f, -0.05f, 0f), new Vector3(0.36f * r, 0.03f * r, 0.26f * r), on.eulerAngles, false);
        Primitive("Shield Point", PrimitiveType.Cube, Shade.Light, Across(0f, 0.08f, 0f), new Vector3(0.255f * r, 0.03f * r, 0.255f * r), diamond, false);
        surface = Surface.Metal;
        Primitive("Inner Shield", PrimitiveType.Cube, Shade.Dark, Across(0f, -0.05f, 0.012f), new Vector3(0.28f * r, 0.03f * r, 0.2f * r), on.eulerAngles, false);
        Primitive("Inner Point", PrimitiveType.Cube, Shade.Dark, Across(0f, 0.06f, 0.012f), new Vector3(0.198f * r, 0.03f * r, 0.198f * r), diamond, false);
        surface = Surface.Glow;
        Renderer star = Primitive("Star", PrimitiveType.Sphere, Shade.Light, Across(0f, 0f, 0.03f), new Vector3(0.1f * r, 0.02f * r, 0.1f * r), on.eulerAngles, false);
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
            HatDangle.Add(prong.gameObject, way, length * r, 140f, 0.6f, 2.5f);
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
        // Diamonds round the band, in turns of the light and main shades
        const int diamonds = 12;
        for (int i = 0; i < diamonds; i++)
        {
            Quaternion around = Quaternion.Euler(0f, 360f * i / diamonds, 0f);
            Primitive("Diamond", PrimitiveType.Cube, i % 2 == 0 ? Shade.Light : Shade.Main, around * Vector3.forward * (1.1f * r) + Vector3.up * (0.21f * r), new Vector3(0.1f * r, 0.1f * r, 0.02f * r), (around * Quaternion.Euler(0f, 0f, 45f)).eulerAngles, false);
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

    // Devil horns: a glossy horn on each side of the top of the head, ridged at its root, leaning out
    // then curving up and in to a sharp point
    private void BuildDevilHorns(float r)
    {
        surface = Surface.Plastic;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 root = new Vector3(0.42f * side, 0.86f, 0.22f).normalized * (0.95f * r);
            Vector3[] ways = { new Vector3(0.75f * side, 1f, 0.15f), new Vector3(0.45f * side, 1f, 0.1f), new Vector3(0.05f * side, 1f, 0f), new Vector3(-0.35f * side, 1f, -0.05f) };
            float[] lengths = { 0.18f * r, 0.16f * r, 0.15f * r, 0.15f * r };
            Horn("Horn", root, ways, lengths, 0.17f * r, Shade.Main, true);
            Vector3 rootUp = ways[0].normalized;
            for (int i = 0; i < 2; i++)
            {
                Generated("Horn Ridge", Torus(0.2f), Shade.Dark, root + rootUp * ((0.03f + 0.05f * i) * r), Vector3.one * ((0.16f - 0.015f * i) * r), Quaternion.FromToRotation(Vector3.up, rootUp).eulerAngles, false);
            }
        }
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

    // A primitive lying on a dome of radius radius (of the head's) round the head's centre, its up
    // pointing out of the dome: around is degrees round from the front towards the right, down is
    // degrees down from the top (negative: down the back instead)
    private Renderer OnDome(float r, float radius, string name, PrimitiveType type, Shade shade, float around, float down, Vector3 scale)
    {
        Quaternion turn = Quaternion.Euler(0f, around, 0f) * Quaternion.Euler(down, 0f, 0f);
        return Primitive(name, type, shade, turn * Vector3.up * (radius * r), scale, turn.eulerAngles, false);
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
