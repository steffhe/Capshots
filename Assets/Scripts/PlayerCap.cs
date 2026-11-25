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
    // How brightly a glowing part (the halo) glows, in its light shade
    private const float HaloGlow = 1.8f;
    // How see-through glass is: its colour's alpha
    private const float GlassAlpha = 0.2f;

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
        Color dark = Color.Lerp(color, Color.black, 0.35f);
        Color light = Color.Lerp(color, Color.white, 0.45f);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color c = shades[i] == Shade.Dark ? dark : shades[i] == Shade.Light ? light : color;
            Surface kind = surfaces != null ? surfaces[i] : Surface.Twill;
            if (kind == Surface.Glass) c.a = GlassAlpha;
            renderers[i].GetPropertyBlock(block);
            block.SetColor("_BaseColor", c); // URP
            block.SetColor("_Color", c);     // built-in shaders
            block.SetColor("_EmissionColor", kind == Surface.Glow ? c * HaloGlow : Color.black);
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
            case Hats.CatEarHeadset: BuildCatEarHeadset(r); break;
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
            case Hats.WindUpKey: BuildWindUpKey(r); break;
            case Hats.BirthdayCake: BuildBirthdayCake(r); break;
            case Hats.Siren: BuildSiren(r); break;
            case Hats.RadarDish: BuildRadarDish(r); break;
            case Hats.JesterHat: BuildJesterHat(r); break;
            case Hats.PlasmaGlobe: BuildPlasmaGlobe(r); break;
            case Hats.Ufo: BuildUfo(r); break;
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

    // A headset with a cat's ears: a band over the head from ear to ear, a round cup over each of the
    // drone's ear pods with a glowing disc on the outside, and a pointed ear on each side of the band
    private void BuildCatEarHeadset(float r)
    {
        const float band = 1.1f; // the band's radius, round the head's centre
        surface = Surface.Plastic;
        // Half a ring, stood up across the head
        Generated("Band", Torus(0.14f, 180f), Shade.Dark, Vector3.zero, Vector3.one * (band * r), new Vector3(-90f, 0f, 0f), true);
        for (int side = -1; side <= 1; side += 2)
        {
            surface = Surface.Plastic;
            Primitive("Cup", PrimitiveType.Cylinder, Shade.Main, new Vector3(1.04f * r * side, 0f, 0f), new Vector3(0.62f * r, 0.09f * r, 0.62f * r), new Vector3(0f, 0f, 90f), true);
            // The ear stands on the band, pointing away from the head's centre
            float tilt = 35f * side;
            Vector3 onBand = new Vector3(Mathf.Sin(tilt * Mathf.Deg2Rad), Mathf.Cos(tilt * Mathf.Deg2Rad), 0f) * (band * r);
            Vector3 euler = new Vector3(0f, 0f, -tilt);
            Generated("Ear", Frustum(0f, false), Shade.Main, onBand, new Vector3(0.22f * r, 0.4f * r, 0.11f * r), euler, false);
            Generated("Inner Ear", Frustum(0f, false), Shade.Light, onBand + new Vector3(0f, 0f, 0.05f * r), new Vector3(0.12f * r, 0.27f * r, 0.07f * r), euler, false);
            surface = Surface.Glow;
            Primitive("Light", PrimitiveType.Cylinder, Shade.Light, new Vector3(1.05f * r * side, 0f, 0f), new Vector3(0.3f * r, 0.1f * r, 0.3f * r), new Vector3(0f, 0f, 90f), false);
        }
    }

    // A traffic cone sitting on the head: a square base, the cone, and two light stripes round it
    private void BuildTrafficCone(float r)
    {
        const float sit = 0.62f, radius = 0.78f, height = 1.7f, top = 0.16f;
        surface = Surface.Plastic;
        Primitive("Base", PrimitiveType.Cube, Shade.Dark, new Vector3(0f, sit * r, 0f), new Vector3(1.75f * r, 0.08f * r, 1.75f * r), Vector3.zero, true);
        Generated("Cone", Frustum(top, true), Shade.Main, new Vector3(0f, (sit + 0.04f) * r, 0f), new Vector3(radius * r, height * r, radius * r), Vector3.zero, true);
        Stripe("Stripe", Shade.Light, (sit + 0.04f) * r, radius * r, top, height * r, 0.45f * r, 0.72f * r);
        Stripe("Stripe", Shade.Light, (sit + 0.04f) * r, radius * r, top, height * r, 0.98f * r, 1.18f * r);
    }

    // A chef's hat: a tall band round the head and a big puffy top of round lumps
    private void BuildChefsHat(float r)
    {
        surface = Surface.Twill;
        Generated("Band", Frustum(1.04f, false), Shade.Main, new Vector3(0f, 0.38f * r, 0f), new Vector3(0.94f * r, 0.5f * r, 0.94f * r), Vector3.zero, true);
        Primitive("Puff", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, 1.3f * r, 0f), new Vector3(1.15f * r, 0.85f * r, 1.15f * r), Vector3.zero, true);
        const int lumps = 6;
        for (int i = 0; i < lumps; i++)
        {
            float angle = (i + 0.5f) * Mathf.PI * 2f / lumps;
            Vector3 at = new Vector3(Mathf.Sin(angle) * 0.55f * r, 1.15f * r, Mathf.Cos(angle) * 0.55f * r);
            Primitive("Puff", PrimitiveType.Sphere, Shade.Light, at, Vector3.one * (0.75f * r), Vector3.zero, false);
        }
    }

    // Rabbit-ear antennas: a round metal base on top of the head with two rods in a V, each with a
    // glowing ball on its tip
    private void BuildAntenna(float r)
    {
        surface = Surface.Metal;
        Vector3 basePoint = new Vector3(0f, 1.02f * r, 0f);
        Primitive("Base", PrimitiveType.Sphere, Shade.Dark, new Vector3(0f, 0.98f * r, 0f), new Vector3(0.45f * r, 0.28f * r, 0.45f * r), Vector3.zero, true);
        const float length = 1.5f;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 euler = new Vector3(-10f, 0f, -28f * side);
            Vector3 up = Quaternion.Euler(euler) * Vector3.up;
            surface = Surface.Metal;
            Primitive("Rod", PrimitiveType.Cylinder, Shade.Main, basePoint + up * (0.5f * length * r), new Vector3(0.05f * r, 0.5f * length * r, 0.05f * r), euler, false);
            surface = Surface.Glow;
            Primitive("Tip", PrimitiveType.Sphere, Shade.Light, basePoint + up * (length * r), Vector3.one * (0.16f * r), Vector3.zero, false);
        }
    }

    // A plunger stuck to the top of the head: a rubber cup with a handle leaning a little to one side
    private void BuildPlunger(float r)
    {
        const float cupY = 0.72f, cupHeight = 0.38f, handle = 1.5f;
        surface = Surface.Plastic;
        Generated("Cup", Dome(), Shade.Main, new Vector3(0f, cupY * r, 0f), new Vector3(0.62f * r, cupHeight * r, 0.62f * r), Vector3.zero, true);
        Vector3 cupTop = new Vector3(0f, (cupY + cupHeight - 0.02f) * r, 0f);
        Vector3 euler = new Vector3(4f, 0f, -8f);
        Vector3 up = Quaternion.Euler(euler) * Vector3.up;
        surface = Surface.Satin;
        Primitive("Handle", PrimitiveType.Cylinder, Shade.Light, cupTop + up * (0.5f * handle * r), new Vector3(0.13f * r, 0.5f * handle * r, 0.13f * r), euler, true);
        Primitive("Knob", PrimitiveType.Sphere, Shade.Dark, cupTop + up * (handle * r), Vector3.one * (0.2f * r), Vector3.zero, false);
    }

    // A cowboy hat: a creased crown, slightly longer front to back, with a band, on a brim that
    // curls up at the sides
    private void BuildCowboyHat(float r)
    {
        const float sit = 0.6f;
        surface = Surface.Felt;
        Primitive("Brim", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, sit * r, 0f), new Vector3(1.7f * r, 0.025f * r, 2.9f * r), Vector3.zero, true);
        for (int side = -1; side <= 1; side += 2)
        {
            // Tipped up 30 degrees, its inner edge meeting the flat brim
            Primitive("Brim Curl", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0.98f * r * side, (sit + 0.25f) * r, 0f), new Vector3(1.0f * r, 0.025f * r, 2.5f * r), new Vector3(0f, 0f, 30f * side), true);
        }
        Generated("Crown", Frustum(0.82f, true), Shade.Main, new Vector3(0f, (sit - 0.02f) * r, 0f), new Vector3(0.88f * r, 0.8f * r, 0.98f * r), Vector3.zero, true);
        // The crease down the middle of the top
        Primitive("Crease", PrimitiveType.Cube, Shade.Dark, new Vector3(0f, (sit + 0.77f) * r, 0f), new Vector3(0.08f * r, 0.03f * r, 1.2f * r), Vector3.zero, false);
        surface = Surface.Satin;
        Stripe("Band", Shade.Dark, (sit - 0.02f) * r, 0.88f * r, 0.82f, 0.8f * r, 0.06f * r, 0.2f * r, 0.98f / 0.88f);
    }

    // A sombrero: a tall rounded crown with a band, on a huge brim whose edge turns up
    private void BuildSombrero(float r)
    {
        const float sit = 0.6f, crownRadius = 0.85f, crownTop = 0.55f, crownHeight = 1.0f;
        surface = Surface.Felt;
        Primitive("Brim", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, sit * r, 0f), new Vector3(3.4f * r, 0.025f * r, 3.4f * r), Vector3.zero, true);
        Generated("Brim Edge", Frustum(1.1f, false), Shade.Light, new Vector3(0f, sit * r, 0f), new Vector3(1.7f * r, 0.22f * r, 1.7f * r), Vector3.zero, false);
        Generated("Crown", Frustum(crownTop, true), Shade.Main, new Vector3(0f, sit * r, 0f), new Vector3(crownRadius * r, crownHeight * r, crownRadius * r), Vector3.zero, true);
        surface = Surface.Satin;
        Stripe("Band", Shade.Light, sit * r, crownRadius * r, crownTop, crownHeight * r, 0.05f * r, 0.22f * r);
    }

    // A pirate's bicorne: a tall, flat half-round hat worn sideways over a skullcap, with a skull and
    // crossbones on the front
    private void BuildPirateHat(float r)
    {
        surface = Surface.Felt;
        Crown(Vector3.one * (1.07f * r));
        Generated("Bicorne", Dome(), Shade.Main, new Vector3(0f, 0.42f * r, 0f), new Vector3(1.4f * r, 0.95f * r, 0.55f * r), Vector3.zero, true);
        // Its front face is about 0.46 of the head's radius forward, at the skull's height
        surface = Surface.Plastic;
        Primitive("Skull", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, 1.0f * r, 0.47f * r), new Vector3(0.22f * r, 0.22f * r, 0.08f * r), Vector3.zero, false);
        for (int side = -1; side <= 1; side += 2)
        {
            Primitive("Bone", PrimitiveType.Cube, Shade.Light, new Vector3(0f, 0.84f * r, 0.46f * r), new Vector3(0.42f * r, 0.06f * r, 0.04f * r), new Vector3(0f, 0f, 35f * side), false);
        }
    }

    // An astronaut's fishbowl: a clear glass sphere round the whole head, a metal collar round its
    // bottom, and a little fish swimming round in the space over the head
    private void BuildFishbowl(float r)
    {
        surface = Surface.Glass;
        Primitive("Bowl", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, 0.12f * r, 0f), Vector3.one * (2.56f * r), Vector3.zero, true);
        surface = Surface.Metal;
        Generated("Collar", Torus(0.22f), Shade.Dark, new Vector3(0f, -0.8f * r, 0f), Vector3.one * (0.86f * r), Vector3.zero, false);

        GameObject swim = new GameObject("Swim");
        swim.layer = gameObject.layer;
        swim.transform.SetParent(transform, false);
        swim.transform.localPosition = new Vector3(0f, 1.18f * r, 0f);
        swim.AddComponent<HatSpinner>().SetSteady(50f);
        // Facing along its circle: the swim turns clockwise seen from above, so at +x it heads to -z
        surface = Surface.Plastic;
        Primitive("Fish", PrimitiveType.Sphere, Shade.Main, new Vector3(0.28f * r, 0f, 0f), new Vector3(0.1f * r, 0.13f * r, 0.2f * r), Vector3.zero, false, swim.transform);
        Primitive("Tail", PrimitiveType.Cube, Shade.Light, new Vector3(0.28f * r, 0f, 0.13f * r), new Vector3(0.02f * r, 0.12f * r, 0.08f * r), new Vector3(45f, 0f, 0f), false, swim.transform);
    }

    // A wizard's hat: a wide brim and a tall cone whose tip bends back, with a band and glowing stars
    private void BuildWizardHat(float r)
    {
        const float sit = 0.6f, radius = 0.8f, lower = 1.1f, narrow = 0.42f;
        surface = Surface.Felt;
        Primitive("Brim", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, sit * r, 0f), new Vector3(2.3f * r, 0.025f * r, 2.3f * r), Vector3.zero, true);
        Generated("Cone", Frustum(narrow, false), Shade.Main, new Vector3(0f, sit * r, 0f), new Vector3(radius * r, lower * r, radius * r), Vector3.zero, true);
        float joint = radius * narrow * r;
        Generated("Tip", Frustum(0f, false), Shade.Main, new Vector3(0f, (sit + lower - 0.02f) * r, 0f), new Vector3(joint, 0.9f * r, joint), new Vector3(-25f, 0f, 0f), false);
        surface = Surface.Satin;
        Stripe("Band", Shade.Light, sit * r, radius * r, narrow, lower * r, 0.03f * r, 0.18f * r);
        surface = Surface.Glow;
        float[] heights = { 0.4f, 0.62f, 0.8f };
        float[] angles = { 25f, -55f, 140f };
        for (int i = 0; i < heights.Length; i++)
        {
            float h = heights[i] * lower;
            float at = radius * (1f - (1f - narrow) * heights[i]) + 0.01f;
            float a = angles[i] * Mathf.Deg2Rad;
            Primitive("Star", PrimitiveType.Sphere, Shade.Light, new Vector3(Mathf.Sin(a) * at * r, (sit + h) * r, Mathf.Cos(a) * at * r), Vector3.one * (0.09f * r), Vector3.zero, false);
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
        surface = Surface.Satin;
        float top = (board + 0.03f) * r;
        Primitive("Button", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, top, 0f), new Vector3(0.14f * r, 0.06f * r, 0.14f * r), Vector3.zero, false);
        // The board's corner on the right is 1.34 of the head's radius out
        const float corner = 1.3f;
        Primitive("Cord", PrimitiveType.Cube, Shade.Light, new Vector3(0.5f * corner * r, top + 0.01f * r, 0f), new Vector3(corner * r, 0.02f * r, 0.03f * r), Vector3.zero, false);

        Transform tassel = Pivot("Tassel", new Vector3(corner * r, top, 0f));
        HatDangle.Add(tassel.gameObject, Vector3.down, 0.5f * r, 0f, 1f, 2f);
        Primitive("String", PrimitiveType.Cylinder, Shade.Light, new Vector3(0f, -0.15f * r, 0f), new Vector3(0.03f * r, 0.15f * r, 0.03f * r), Vector3.zero, false, tassel);
        Generated("Tassel", Frustum(0.35f, true), Shade.Light, new Vector3(0f, -0.52f * r, 0f), new Vector3(0.1f * r, 0.24f * r, 0.1f * r), Vector3.zero, false, tassel);
    }

    // A wind-up key sticking out of the back of the head, turning round and round like a clockwork toy's
    private void BuildWindUpKey(float r)
    {
        surface = Surface.Metal;
        // Where it goes into the head: on the back, a little above the middle
        Vector3 hole = new Vector3(0f, 0.35f * r, -0.93f * r);
        Primitive("Collar", PrimitiveType.Cylinder, Shade.Dark, hole, new Vector3(0.24f * r, 0.04f * r, 0.24f * r), new Vector3(90f, 0f, 0f), false);
        Transform key = Pivot("Key", hole);
        HatMotion.Add(key.gameObject, HatMotion.Kind.Spin, Vector3.forward, 90f);
        Primitive("Shaft", PrimitiveType.Cylinder, Shade.Main, new Vector3(0f, 0f, -0.25f * r), new Vector3(0.1f * r, 0.25f * r, 0.1f * r), new Vector3(90f, 0f, 0f), false, key);
        Primitive("Grip", PrimitiveType.Cube, Shade.Main, new Vector3(0f, 0f, -0.55f * r), new Vector3(0.3f * r, 0.1f * r, 0.08f * r), Vector3.zero, false, key);
        for (int side = -1; side <= 1; side += 2)
        {
            // The two loops of the key's bow, standing up
            Generated("Bow", Torus(0.35f), Shade.Main, new Vector3(0.3f * side * r, 0f, -0.55f * r), Vector3.one * (0.2f * r), new Vector3(90f, 0f, 0f), false, key);
        }
        StillBox(new Vector3(0f, 0.35f * r, -1.25f * r), new Vector3(0.95f * r, 0.45f * r, 0.6f * r));
    }

    // A birthday cake on the head: a round cake with icing dripping down its edge and a candle on top
    // whose flame flickers and leans as the player moves. It goes out when the hat is knocked off
    // (a dead player's hat).
    private void BuildBirthdayCake(float r)
    {
        const float sit = 0.75f, height = 0.4f, radius = 0.7f;
        surface = Surface.Satin;
        Primitive("Cake", PrimitiveType.Cylinder, Shade.Main, new Vector3(0f, (sit + 0.5f * height) * r, 0f), new Vector3(2f * radius * r, 0.5f * height * r, 2f * radius * r), Vector3.zero, true);
        float top = (sit + height) * r;
        Primitive("Icing", PrimitiveType.Cylinder, Shade.Light, new Vector3(0f, top, 0f), new Vector3(2.06f * radius * r, 0.03f * r, 2.06f * radius * r), Vector3.zero, false);
        const int drips = 10;
        for (int i = 0; i < drips; i++)
        {
            float a = (i + 0.3f) * Mathf.PI * 2f / drips;
            float drop = (i % 3 == 0 ? 0.16f : 0.1f) * r;
            Primitive("Drip", PrimitiveType.Sphere, Shade.Light, new Vector3(Mathf.Sin(a) * radius * r, top - drop * 0.5f, Mathf.Cos(a) * radius * r), new Vector3(0.13f * r, drop + 0.04f * r, 0.13f * r), Vector3.zero, false);
        }
        surface = Surface.Plastic;
        const float candle = 0.36f;
        Primitive("Candle", PrimitiveType.Cylinder, Shade.Light, new Vector3(0f, top + 0.5f * candle * r, 0f), new Vector3(0.11f * r, 0.5f * candle * r, 0.11f * r), Vector3.zero, false);
        Primitive("Stripe", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, top + 0.55f * candle * r, 0f), new Vector3(0.115f * r, 0.03f * r, 0.115f * r), Vector3.zero, false);
        float wick = top + (candle + 0.04f) * r;
        Primitive("Wick", PrimitiveType.Cube, Shade.Dark, new Vector3(0f, wick, 0f), new Vector3(0.02f * r, 0.08f * r, 0.02f * r), Vector3.zero, false);
        if (solid) return;

        // The flame leans away from the way the player moves, and flickers
        Transform flame = Pivot("Flame", new Vector3(0f, wick, 0f));
        HatDangle.Add(flame.gameObject, Vector3.up, 0.15f * r, 250f, 0f, 6f);
        surface = Surface.Glow;
        Renderer fire = Primitive("Fire", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, 0.12f * r, 0f), new Vector3(0.11f * r, 0.22f * r, 0.11f * r), Vector3.zero, false, flame);
        HatMotion.Add(fire.gameObject, HatMotion.Kind.Flicker, Vector3.up, 6f, 0.5f);
    }

    // A police siren: a glass dome on a metal base, with a glowing lamp inside and two lights going
    // round it, faster the faster the player moves
    private void BuildSiren(float r)
    {
        const float dome = 1.22f;
        surface = Surface.Metal;
        Primitive("Base", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, 0.98f * r, 0f), new Vector3(0.6f * r, 0.06f * r, 0.6f * r), Vector3.zero, true);
        Transform lights = Pivot("Lights", new Vector3(0f, dome * r, 0f));
        lights.gameObject.AddComponent<HatSpinner>();
        surface = Surface.Glow;
        Primitive("Lamp", PrimitiveType.Sphere, Shade.Main, Vector3.zero, Vector3.one * (0.16f * r), Vector3.zero, false, lights);
        for (int side = -1; side <= 1; side += 2)
        {
            Primitive("Light", PrimitiveType.Cube, Shade.Light, new Vector3(0.14f * side * r, 0f, 0f), new Vector3(0.05f * r, 0.3f * r, 0.22f * r), Vector3.zero, false, lights);
        }
        surface = Surface.Glass;
        Primitive("Dome", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, dome * r, 0f), new Vector3(0.52f * r, 0.6f * r, 0.52f * r), Vector3.zero, true);
    }

    // A radar dish on a short mast, sweeping slowly from side to side, with a glowing receiver in front
    private void BuildRadarDish(float r)
    {
        const float mastTop = 1.25f;
        surface = Surface.Metal;
        Primitive("Mast", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, 0.5f * (0.96f + mastTop) * r, 0f), new Vector3(0.08f * r, 0.5f * (mastTop - 0.96f) * r, 0.08f * r), Vector3.zero, false);
        Transform sweep = Pivot("Sweep", new Vector3(0f, mastTop * r, 0f));
        HatMotion.Add(sweep.gameObject, HatMotion.Kind.Sweep, Vector3.up, 0.15f, 70f);
        // The dish faces forwards and 20 degrees up: its back bulges back and down
        Vector3 dish = new Vector3(0f, 0.3f * r, 0.2f * r);
        Vector3 facing = Quaternion.Euler(-20f, 0f, 0f) * Vector3.forward;
        Generated("Dish", Dome(), Shade.Main, dish, new Vector3(0.45f * r, 0.22f * r, 0.45f * r), new Vector3(-110f, 0f, 0f), false, sweep);
        Vector3 back = dish - facing * (0.22f * r);
        Primitive("Arm", PrimitiveType.Cylinder, Shade.Dark, back * 0.5f, new Vector3(0.06f * r, 0.5f * back.magnitude, 0.06f * r), Quaternion.FromToRotation(Vector3.up, back).eulerAngles, false, sweep);
        Primitive("Feed", PrimitiveType.Cylinder, Shade.Dark, dish + facing * (0.15f * r), new Vector3(0.04f * r, 0.15f * r, 0.04f * r), new Vector3(70f, 0f, 0f), false, sweep);
        surface = Surface.Glow;
        Primitive("Receiver", PrimitiveType.Sphere, Shade.Light, dish + facing * (0.3f * r), Vector3.one * (0.09f * r), Vector3.zero, false, sweep);
        StillBox(new Vector3(0f, 1.4f * r, 0f), new Vector3(0.9f * r, 0.6f * r, 0.9f * r));
    }

    // A jester's hat: a cap with three floppy prongs, each with a bell on the end, that bounce and
    // swing as the player moves
    private void BuildJesterHat(float r)
    {
        surface = Surface.Felt;
        Crown(Vector3.one * (1.07f * r));
        Vector3[] ways = { new Vector3(-1f, 0.7f, 0f), new Vector3(1f, 0.7f, 0f), new Vector3(0f, 1f, -0.35f) };
        Shade[] prongShades = { Shade.Main, Shade.Main, Shade.Dark };
        const float length = 0.8f;
        for (int i = 0; i < ways.Length; i++)
        {
            Vector3 way = ways[i].normalized;
            Transform prong = Pivot("Prong", way * (0.95f * r), Quaternion.FromToRotation(Vector3.up, way).eulerAngles);
            HatDangle.Add(prong.gameObject, way, length * r, 140f, 0.6f, 2.5f);
            surface = Surface.Felt;
            Generated("Prong", Frustum(0.2f, false), prongShades[i], Vector3.zero, new Vector3(0.26f * r, length * r, 0.26f * r), Vector3.zero, false, prong);
            surface = Surface.Metal;
            Primitive("Bell", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, (length + 0.06f) * r, 0f), Vector3.one * (0.18f * r), Vector3.zero, false, prong);
        }
        surface = Surface.Felt;
        Generated("Band", Frustum(1f, false), Shade.Dark, new Vector3(0f, 0.12f * r, 0f), new Vector3(1.1f * r, 0.18f * r, 1.1f * r), Vector3.zero, true);
    }

    // A plasma globe on the head: a glass ball on a metal base, a glowing core in the middle, and
    // bolts of lightning flickering out from it to the glass
    private void BuildPlasmaGlobe(float r)
    {
        const float centre = 1.42f, radius = 0.4f;
        surface = Surface.Metal;
        Primitive("Base", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, 1.0f * r, 0f), new Vector3(0.5f * r, 0.07f * r, 0.5f * r), Vector3.zero, true);
        Primitive("Neck", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, 1.08f * r, 0f), new Vector3(0.3f * r, 0.03f * r, 0.3f * r), Vector3.zero, false);
        Transform arcs = Pivot("Arcs", new Vector3(0f, centre * r, 0f));
        surface = Surface.Glow;
        Primitive("Core", PrimitiveType.Sphere, Shade.Main, Vector3.zero, Vector3.one * (0.14f * r), Vector3.zero, false, arcs);
        const int bolts = 5;
        Transform[] segments = new Transform[bolts * 2];
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i] = Primitive("Bolt", PrimitiveType.Cylinder, Shade.Light, Vector3.zero, Vector3.one * (0.02f * r), Vector3.zero, false, arcs).transform;
        }
        arcs.gameObject.AddComponent<HatArcs>().Setup(segments, (radius - 0.03f) * r, 0.02f * r);
        surface = Surface.Glass;
        Primitive("Globe", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, centre * r, 0f), Vector3.one * (2f * radius * r), Vector3.zero, true);
    }

    // A little flying saucer hovering over the head: a metal hull with a glass dome, a ring of lights
    // going round its rim, and a see-through tractor beam shining down onto the head
    private void BuildUfo(float r)
    {
        Transform saucer = Pivot("Saucer", new Vector3(0f, 1.5f * r, 0f));
        HatMotion.Add(saucer.gameObject, HatMotion.Kind.Bob, Vector3.up, 0.6f, 0.05f * r);
        surface = Surface.Metal;
        Primitive("Hull", PrimitiveType.Sphere, Shade.Main, Vector3.zero, new Vector3(1.5f * r, 0.3f * r, 1.5f * r), Vector3.zero, false, saucer);
        Primitive("Underside", PrimitiveType.Cylinder, Shade.Dark, new Vector3(0f, -0.12f * r, 0f), new Vector3(0.6f * r, 0.03f * r, 0.6f * r), Vector3.zero, false, saucer);

        Transform ring = Pivot("Lights", Vector3.zero, Vector3.zero, saucer);
        ring.gameObject.AddComponent<HatSpinner>().SetSteady(120f);
        surface = Surface.Glow;
        const int lights = 8;
        for (int i = 0; i < lights; i++)
        {
            float a = i * Mathf.PI * 2f / lights;
            Primitive("Light", PrimitiveType.Sphere, i % 2 == 0 ? Shade.Light : Shade.Main, new Vector3(Mathf.Sin(a) * 0.68f * r, 0f, Mathf.Cos(a) * 0.68f * r), Vector3.one * (0.12f * r), Vector3.zero, false, ring);
        }

        surface = Surface.Glass;
        Primitive("Dome", PrimitiveType.Sphere, Shade.Light, new Vector3(0f, 0.1f * r, 0f), new Vector3(0.6f * r, 0.45f * r, 0.6f * r), Vector3.zero, false, saucer);
        // The beam: a cone widening downwards, from inside the hull to inside the head
        Generated("Beam", Frustum(0.3f, false), Shade.Light, new Vector3(0f, -0.85f * r, 0f), new Vector3(0.7f * r, 0.8f * r, 0.7f * r), Vector3.zero, false, saucer);
        StillBox(new Vector3(0f, 1.5f * r, 0f), new Vector3(1.5f * r, 0.35f * r, 1.5f * r));
    }

    // Devil horns: a short horn on each side of the top of the head, leaning out then curving up to a point
    private void BuildDevilHorns(float r)
    {
        surface = Surface.Plastic;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 root = new Vector3(0.42f * side, 0.86f, 0.22f).normalized * (0.97f * r);
            Vector3 lowerEuler = new Vector3(12f, 0f, -30f * side);
            const float lowerHeight = 0.3f, lowerRadius = 0.17f, taper = 0.6f;
            Generated("Horn", Frustum(taper, false), Shade.Main, root, new Vector3(lowerRadius * r, lowerHeight * r, lowerRadius * r), lowerEuler, true);
            Vector3 bend = root + Quaternion.Euler(lowerEuler) * Vector3.up * (lowerHeight * r);
            float tipRadius = lowerRadius * taper * r;
            Generated("Horn Tip", Frustum(0f, false), Shade.Main, bend, new Vector3(tipRadius, 0.38f * r, tipRadius), new Vector3(4f, 0f, 8f * side), false);
        }
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

    // A band round a frustum part (bottom at y, bottom radius radius, top radius taper of that, height
    // height), from from to to above its bottom, a hair outside it. depth stretches it front to back,
    // for a part stretched the same way.
    private void Stripe(string name, Shade shade, float y, float radius, float taper, float height, float from, float to, float depth = 1f)
    {
        float r0 = radius * (1f - (1f - taper) * from / height) * 1.02f;
        float r1 = radius * (1f - (1f - taper) * to / height) * 1.02f;
        Generated(name, Frustum(r1 / r0, false), shade, new Vector3(0f, y + from, 0f), new Vector3(r0, to - from, r0 * depth), Vector3.zero, false);
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

        // The top, like the side seen from inside too: from under the hat (a hat knocked off and
        // landed upside down, or the camera inside the brim) it would otherwise be a hole
        for (int side = 0; capTop && topRadius > 0f && side < 2; side++)
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
