using UnityEngine;

// A plasma grenade as everyone sees it: a glowing ball of plasma in the thrower's colour (the team
// colour in Team Deathmatch) held in a small dark cage, with a short glowing tail while it flies.
// It sticks to players only; walls and floors it bounces off. Once stuck it fizzles, pulses faster and
// faster and swells until it goes off.
// Purely cosmetic: every client flies its own copy along the same arc (Step, the same code the host
// runs), and the host corrects it when it bounces, sticks or goes off (see Manager.Grenades). The
// thrower's own game starts its copy straight away, so the throw never waits for the host.
public class PlasmaGrenade : MonoBehaviour
{
    // How long a copy waits for the host to say it went off before quietly fading away (a throw the
    // host never saw, or a lost message)
    private const float OrphanTime = PlasmaGrenadeRules.FreeFuse + 2f;
    private const float CoreGlow = 3.5f;
    private const int TailLength = 5;

    public long Key { get; private set; }
    public int Actor { get; private set; }
    public Vector3 Position => position;

    private Vector3 position, velocity;
    private float stepTime, accumulator, age;
    private Transform followed;
    private Vector3 followOffset;
    private bool stuck, wasStuck;
    private float stuckAge;

    private Color color;
    private Transform visual, core;
    private Renderer coreRenderer;
    private Light glow;
    private Transform[] tail;
    private Renderer[] tailRenderers;
    private Vector3[] tailPositions;
    private AudioSource hum;
    private MaterialPropertyBlock block;

    // Scenery a grenade bounces off: what shots hit, minus players and their hitboxes, plus glass.
    // Weapon sets it from its canBeShot when it wakes.
    private static int worldMask = Physics.DefaultRaycastLayers & ~((1 << Layers.LocalPlayer) | (1 << Layers.RemotePlayer) | (1 << Layers.Gun) | (1 << Layers.LocalGun) | (1 << Layers.Hitbox));
    public static int WorldMask => worldMask;
    public static void SetWorldMask(int canBeShot)
    {
        worldMask = (canBeShot & ~((1 << Layers.LocalPlayer) | (1 << Layers.RemotePlayer) | (1 << Layers.Hitbox))) | (1 << Layers.Glass);
    }

    public static long MakeKey(int body, int number) => ((long)body << 20) | (uint)(number & 0xFFFFF);

    #region Flight

    // One step of flight: gravity, then move, bouncing off any scenery on the way. Returns how hard it
    // hit something, in m/s into the surface (0 when it hit nothing).
    public static float Step(ref Vector3 position, ref Vector3 velocity, float dt)
    {
        velocity += Vector3.down * PlasmaGrenadeRules.Gravity * dt;
        Vector3 move = velocity * dt;
        float distance = move.magnitude;
        if (distance < 1e-5f) return 0f;

        Vector3 direction = move / distance;
        if (Physics.SphereCast(position, PlasmaGrenadeRules.Radius, direction, out RaycastHit hit, distance, worldMask, QueryTriggerInteraction.Ignore))
        {
            float impact = Mathf.Max(0f, -Vector3.Dot(velocity, hit.normal));
            position += direction * hit.distance + hit.normal * 0.005f;
            velocity = PlasmaGrenadeRules.Bounce(velocity, hit.normal);
            return Mathf.Max(impact, 0.001f);
        }

        position += move;
        return 0f;
    }

    #endregion

    #region Creating

    public static PlasmaGrenade Create(long key, int actor, Vector3 origin, Vector3 velocity, float stepTime)
    {
        GameObject root = new GameObject("Plasma Grenade");
        root.transform.position = origin;
        PlasmaGrenade grenade = root.AddComponent<PlasmaGrenade>();
        grenade.Key = key;
        grenade.Actor = actor;
        grenade.position = origin;
        grenade.velocity = velocity;
        grenade.stepTime = Mathf.Max(0.005f, stepTime);
        grenade.Build(Color.Lerp(PlayerCap.ColorFor(actor), Color.white, 0.25f));
        return grenade;
    }

    private void Build(Color c)
    {
        color = c;
        block = new MaterialPropertyBlock();

        visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);

        float d = PlasmaGrenadeRules.Radius * 2f;
        // The plasma ball
        core = Part("Core", PrimitiveType.Sphere, DroneBody.GlowMaterial, Vector3.zero, Vector3.one * d * 0.85f, visual);
        coreRenderer = core.GetComponent<Renderer>();
        Paint(coreRenderer, color, color * CoreGlow);

        // A dark cage round it: three curved prongs meeting at the back, like a claw holding the plasma
        Material metal = DroneBody.TrimMaterial;
        for (int i = 0; i < 3; i++)
        {
            Quaternion around = Quaternion.Euler(0f, 0f, i * 120f);
            Transform prong = Part("Prong", PrimitiveType.Cube, metal, around * new Vector3(0f, d * 0.42f, -d * 0.05f), new Vector3(d * 0.16f, d * 0.12f, d * 0.95f), visual);
            prong.localRotation = around;
            Transform tip = Part("Tip", PrimitiveType.Sphere, metal, around * new Vector3(0f, d * 0.36f, d * 0.42f), Vector3.one * d * 0.16f, visual);
            tip.localRotation = around;
        }
        Part("Base", PrimitiveType.Sphere, metal, new Vector3(0f, 0f, -d * 0.48f), new Vector3(d * 0.6f, d * 0.6f, d * 0.25f), visual);

        // A short glowing tail of fading blobs behind it in flight
        tail = new Transform[TailLength];
        tailRenderers = new Renderer[TailLength];
        tailPositions = new Vector3[TailLength];
        for (int i = 0; i < TailLength; i++)
        {
            float scale = d * Mathf.Lerp(0.6f, 0.15f, i / (float)(TailLength - 1));
            tail[i] = Part("Tail", PrimitiveType.Sphere, DroneBody.GlowMaterial, Vector3.zero, Vector3.one * scale, null);
            tail[i].position = position;
            tailRenderers[i] = tail[i].GetComponent<Renderer>();
            float fade = 1f - i / (float)TailLength;
            Paint(tailRenderers[i], color, color * (CoreGlow * 0.8f * fade));
            tailPositions[i] = position;
        }

        glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = color;
        glow.range = 4f;
        glow.intensity = 2f;
        glow.shadows = LightShadows.None;

        hum = gameObject.AddComponent<AudioSource>();
        hum.clip = PlasmaSounds.Hum;
        hum.loop = true;
        hum.spatialBlend = 1f;
        hum.rolloffMode = AudioRolloffMode.Linear;
        hum.minDistance = 1.5f;
        hum.maxDistance = 30f;
        hum.dopplerLevel = 0.6f;
        hum.volume = 0.05f;
        hum.Play();
        AudioSource.PlayClipAtPoint(PlasmaSounds.Throw, position, 0.12f);
    }

    private static Transform Part(string name, PrimitiveType type, Material material, Vector3 localPosition, Vector3 scale, Transform parent)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        DestroyImmediate(part.GetComponent<Collider>());
        part.name = name;
        if (parent != null) part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = scale;
        Renderer r = part.GetComponent<Renderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return part.transform;
    }

    private void Paint(Renderer r, Color baseColor, Color emission)
    {
        if (r == null) return;
        r.GetPropertyBlock(block);
        block.SetColor("_BaseColor", baseColor); // URP
        block.SetColor("_Color", baseColor);     // built-in shaders
        block.SetColor("_EmissionColor", emission);
        r.SetPropertyBlock(block);
    }

    #endregion

    #region Corrections from the host

    // It bounced on the host: take the host's position and velocity from here
    public void Correct(Vector3 hostPosition, Vector3 hostVelocity)
    {
        if (stuck) return;
        position = hostPosition;
        velocity = hostVelocity;
        accumulator = 0f;
    }

    // It stuck to this player (null if they're no longer here), at offset in their local space
    public void StickTo(Transform target, Vector3 offset)
    {
        if (stuck) return;
        stuck = true;
        stuckAge = 0f;
        followed = target;
        followOffset = offset;
        velocity = Vector3.zero;
        if (target != null) position = target.TransformPoint(offset);
        hum.volume = 0.09f;
        AudioSource.PlayClipAtPoint(PlasmaSounds.Stick, position, 0.25f);
    }

    // It went off: the blast, and this copy goes away
    public void Explode(Vector3 at)
    {
        PlasmaBlast.Spawn(at, color);
        Destroy(gameObject);
    }

    #endregion

    #region Every frame

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age > OrphanTime + (stuck ? PlasmaGrenadeRules.StuckFuse : 0f)) { Destroy(gameObject); return; }

        if (stuck)
        {
            stuckAge += dt;
            if (followed != null) position = followed.TransformPoint(followOffset);
            else if (!wasStuck)
            {
                // Whoever it was stuck to is gone (killed by someone else first): it drops
                wasStuck = true;
                stuck = false;
            }
        }
        else
        {
            // The same fixed steps as the host, so the arcs match
            accumulator += dt;
            int steps = 0;
            while (accumulator >= stepTime && steps < 8)
            {
                Step(ref position, ref velocity, stepTime);
                accumulator -= stepTime;
                steps++;
            }
        }

        // Between steps, carry on along the velocity, so it moves smoothly at any frame rate
        Vector3 shown = stuck ? position : position + velocity * accumulator;
        transform.position = shown;
        if (!stuck && velocity.sqrMagnitude > 0.5f) visual.rotation = Quaternion.LookRotation(velocity);
        else if (stuck && followed != null) visual.rotation = Quaternion.LookRotation((shown - followed.position).sqrMagnitude > 1e-4f ? shown - followed.position : Vector3.forward);

        UpdateTail(shown, dt);
        UpdateGlow();
    }

    private void UpdateTail(Vector3 head, float dt)
    {
        // Each blob chases the one in front of it, so the tail stretches out at speed and gathers in
        // when it stops
        float follow = 1f - Mathf.Exp(-dt * 28f);
        Vector3 previous = head;
        for (int i = 0; i < TailLength; i++)
        {
            tailPositions[i] = Vector3.Lerp(tailPositions[i], previous, follow);
            previous = tailPositions[i];
            if (tail[i] != null) tail[i].position = tailPositions[i];
        }
    }

    private void UpdateGlow()
    {
        float pulse, swell = 1f, intensity;
        if (stuck)
        {
            // Pulses faster and faster, and swells, until it goes off
            float t = Mathf.Clamp01(stuckAge / PlasmaGrenadeRules.StuckFuse);
            float frequency = Mathf.Lerp(5f, 22f, t * t);
            pulse = 0.5f + 0.5f * Mathf.Sin(stuckAge * frequency * Mathf.PI * 2f * (0.5f + 0.5f * t));
            swell = 1f + 0.35f * t + 0.15f * pulse;
            intensity = Mathf.Lerp(2.5f, 5f, t) * (0.6f + 0.6f * pulse);
            hum.pitch = Mathf.Lerp(1f, 1.8f, t);
        }
        else
        {
            pulse = 0.5f + 0.5f * Mathf.Sin(age * 9f * Mathf.PI * 2f);
            intensity = 1.8f + 0.5f * pulse;
        }

        // A slight crackle, like it can barely hold together
        float flicker = Random.value < 0.08f ? 0.6f : 1f;
        visual.localScale = Vector3.one * swell;
        glow.intensity = intensity * flicker;
        glow.range = 3.5f + swell * 1.5f;
        Paint(coreRenderer, color, color * (CoreGlow * (0.75f + 0.5f * pulse) * flicker * swell));

        // The tail only shows in flight
        bool flying = !stuck && velocity.sqrMagnitude > 4f;
        for (int i = 0; i < TailLength; i++) if (tailRenderers[i] != null) tailRenderers[i].enabled = flying;
    }

    private void OnDestroy()
    {
        if (tail == null) return;
        foreach (Transform t in tail) if (t != null) Destroy(t.gameObject);
    }

    #endregion
}

// The blast of a plasma grenade going off: a blinding flash and a bubble of plasma in the thrower's
// colour that bursts outward and fades, a ring of sparks, and a deep electric boom. Purely cosmetic.
public class PlasmaBlast : MonoBehaviour
{
    private const float Duration = 0.55f;
    private const float FlashIntensity = 9f;

    private Transform bubble;
    private Renderer bubbleRenderer;
    private Light flash;
    private Color color;
    private float age;
    private MaterialPropertyBlock block;

    public static void Spawn(Vector3 position, Color color)
    {
        GameObject root = new GameObject("Plasma Blast");
        root.transform.position = position;
        root.AddComponent<PlasmaBlast>().Build(color);
    }

    private void Build(Color c)
    {
        color = c;
        block = new MaterialPropertyBlock();

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        DestroyImmediate(sphere.GetComponent<Collider>());
        sphere.name = "Bubble";
        bubble = sphere.transform;
        bubble.SetParent(transform, false);
        bubble.localScale = Vector3.one * 0.3f;
        bubbleRenderer = sphere.GetComponent<Renderer>();
        bubbleRenderer.sharedMaterial = DroneBody.GlowMaterial;
        bubbleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        flash = gameObject.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = Color.Lerp(color, Color.white, 0.3f);
        flash.range = PlasmaGrenadeRules.BlastRadius * 3f;
        flash.intensity = FlashIntensity;
        flash.shadows = LightShadows.None;

        // Sparks spraying out all round: the kill sparks, several times over
        for (int i = 0; i < 4; i++) KillSpark.Spawn(transform.position, Random.onUnitSphere, color);

        AudioSource.PlayClipAtPoint(PlasmaSounds.Boom, transform.position, 0.5f);

        // The local player's view shakes, harder the closer they are
        Player local = Player.Local;
        if (local != null)
        {
            float distance = Vector3.Distance(local.transform.position, transform.position);
            float strength = Mathf.Clamp01(1f - distance / (PlasmaGrenadeRules.BlastRadius * 4f)) * 3f;
            Look look = local.GetComponent<Look>();
            if (look != null && strength > 0.1f) look.Punch(strength);
        }
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Duration);

        // Bursts out fast, then slows as it fades
        float grow = 1f - Mathf.Pow(1f - t, 3f);
        bubble.localScale = Vector3.one * Mathf.Lerp(0.3f, PlasmaGrenadeRules.BlastRadius * 1.1f, grow);
        float fade = 1f - t;
        bubbleRenderer.GetPropertyBlock(block);
        Color body = Color.Lerp(Color.white, color, t) * fade;
        block.SetColor("_BaseColor", body);
        block.SetColor("_Color", body);
        block.SetColor("_EmissionColor", Color.Lerp(Color.white, color, Mathf.Min(1f, t * 2f)) * (6f * fade * fade));
        bubbleRenderer.SetPropertyBlock(block);
        // Shrinks away near the end rather than hanging there as a dark ball
        bubbleRenderer.enabled = t < 0.95f;

        if (flash != null) flash.intensity = FlashIntensity * Mathf.Pow(fade, 2f);

        if (age >= Duration) Destroy(gameObject);
    }
}

// The plasma grenade's sounds, synthesized once on first use so no audio files are needed
public static class PlasmaSounds
{
    private const int Rate = 44100;
    private static AudioClip hum, stick, boom, throwClip;

    // A bright, wavering electric hum, looped while it's live
    public static AudioClip Hum => hum != null ? hum : (hum = Make("Plasma Hum", 1f, true, (t, rng) =>
    {
        float wobble = 1f + 0.03f * Mathf.Sin(2f * Mathf.PI * 6f * t);
        float tone = Mathf.Sin(2f * Mathf.PI * 330f * wobble * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 660f * wobble * t) * 0.25f;
        float fizz = ((float)rng.NextDouble() * 2f - 1f) * 0.25f;
        return tone + fizz;
    }));

    // A wet thunk and a crackle as it latches on
    public static AudioClip Stick => stick != null ? stick : (stick = Make("Plasma Stick", 0.35f, false, (t, rng) =>
    {
        float thunk = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(220f, 70f, t / 0.12f) * t) * Mathf.Exp(-t * 30f);
        float crackle = ((float)rng.NextDouble() * 2f - 1f) * (rng.NextDouble() < 0.3 ? 1f : 0.2f) * Mathf.Exp(-t * 9f) * 0.6f;
        return thunk + crackle;
    }));

    // A soft rising whoosh as it leaves the hand
    public static AudioClip Throw => throwClip != null ? throwClip : (throwClip = Make("Plasma Throw", 0.3f, false, (t, rng) =>
    {
        float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.3f));
        float noise = (float)rng.NextDouble() * 2f - 1f;
        float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(300f, 900f, t / 0.3f) * t) * 0.3f;
        return (noise * 0.5f + tone) * envelope;
    }));

    // A deep electric boom: a low thump that sweeps down, under a burst of crackling noise
    public static AudioClip Boom => boom != null ? boom : (boom = Make("Plasma Boom", 1.4f, false, (t, rng) =>
    {
        float thump = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(110f, 35f, Mathf.Sqrt(Mathf.Clamp01(t / 0.6f))) * t) * Mathf.Exp(-t * 3.5f);
        float noise = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 5f);
        float zap = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(700f, 90f, Mathf.Clamp01(t / 0.4f)) * t)) * Mathf.Exp(-t * 9f) * 0.35f;
        return thump * 1.1f + noise * 0.6f + zap;
    }));

    private static AudioClip Make(string name, float length, bool loop, System.Func<float, System.Random, float> sample)
    {
        int samples = Mathf.CeilToInt(length * Rate);
        float[] data = new float[samples];
        System.Random rng = new System.Random(name.Length * 7919);
        float peak = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / Rate;
            float v = sample(t, rng);
            // Short fades at the ends so it never clicks (a loop fades less, to keep it seamless)
            float edge = loop ? 1f : Mathf.Clamp01(i / 64f) * Mathf.Clamp01((samples - i) / 1024f);
            data[i] = v * edge;
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        if (peak > 0f) for (int i = 0; i < samples; i++) data[i] *= 0.9f / peak;

        AudioClip clip = AudioClip.Create(name, samples, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
