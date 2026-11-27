using UnityEngine;

// Makes the players look like they hover instead of walk. Added by Player at runtime, so no prefab changes
// are needed. Runs on every client from how the player moves on this machine (measured in LateUpdate,
// after Fusion has placed the body for the frame), so it needs no network traffic.
//  - The body floats a few centimetres up and bobs gently (less while moving, as if the thrusters hold
//    it steady). It leans into the direction it moves on a spring, so it tips into a start, and rocks
//    back a little when it brakes, like a drone.
//  - A hover cushion: the body sinks on landing and dips as it lifts off, then springs back
//    (Suspension; Player also lets the local player's gun ride it).
//  - A soft glow on the ground under the body (brightest in the middle, fading to nothing at its edge, so
//    it reads as light rather than a solid disc), with a small light that tints the ground, in the
//    player's colour (the team colour in Team Deathmatch). It pulses with the bob and brightens while
//    moving. It stays on the ground during a jump, fading and spreading with height, so it shows where
//    a player in the air is over.
// Only the body model moves: the hitboxes stay put, so the lean and cushion are kept small to keep them lined up.
public class HoverVisuals : MonoBehaviour
{
    // Degrees of lean per m/s, and the most it leans (walking is about 10 m/s, sprinting about 15)
    public float leanPerSpeed = 0.5f;
    public float maxLean = 8f;
    // How high the body floats above where it used to stand, and how far it bobs up and down
    public float lift = 0.05f;
    public float bobHeight = 0.03f;
    public float bobSpeed = 0.7f; // bobs per second
    // Share of the bob left at full speed
    [Range(0f, 1f)] public float movingBob = 0.4f;
    // The lean's spring: how quickly it follows (radians per second) and how much it rocks past (1 = not at all)
    public float leanFrequency = 12f;
    public float leanDamping = 0.5f;
    // The hover cushion: how far the body sinks for a sudden change in vertical speed (about 0.6 x this
    // per m/s of change: 3.5 cm landing from a full jump), the most it moves down or up, and its spring
    public float cushionPerSpeed = 0.006f;
    public float cushionDown = 0.07f, cushionUp = 0.04f;
    public float cushionFrequency = 16f;
    public float cushionDamping = 0.45f;
    // Above this height the glow on the ground has faded out
    public float glowFadeHeight = 4f;

    private Player player;
    private Transform body;
    private Vector3 bodyPosition;
    private Quaternion bodyRotation;

    private Vector3 lastPosition;
    private Vector3 velocity;
    private float lastVerticalSpeed;
    private float phase;
    private float moving;
    // Spring states: lean in degrees (pitch, roll) and the cushion in metres, each with its speed
    private Vector2 lean, leanSpeed;
    private float cushion, cushionSpeed;
    private float feet;

    private Transform glow;
    private Renderer glowRenderer;
    private Light glowLight;
    private MaterialPropertyBlock block;
    private Color color;
    private int colorVersion = -1;
    // Wider than the old solid disc (0.9), as the glow fades out well before its edge
    private const float GlowSize = 1.3f;
    private static Texture2D glowTexture;

    private static Material glowMaterial;
    private static bool glowMaterialLooked;

    // body: the model to float and lean. feetHeight: where the bottom of the body is, relative to the player.
    // layer: the body's layer, so the glow is hidden from the player's own camera like the rest of the body.
    public static HoverVisuals Attach(Player owner, Transform body, float feetHeight, int layer)
    {
        HoverVisuals visuals = owner.gameObject.AddComponent<HoverVisuals>();
        visuals.Setup(owner, body, feetHeight, layer);
        return visuals;
    }

    private void Setup(Player owner, Transform model, float feetHeight, int layer)
    {
        player = owner;
        body = model;
        if (body != null)
        {
            bodyPosition = body.localPosition;
            bodyRotation = body.localRotation;
        }
        lastPosition = transform.position;
        feet = feetHeight;
        // Each player bobs at its own point in the cycle
        phase = Mathf.Repeat(player.ActorId * 0.618034f, 1f) * Mathf.PI * 2f;
        block = new MaterialPropertyBlock();

        CreateGlow(feetHeight, layer);
    }

    // True for the glow's renderer, which keeps its own colour instead of the body's team tint
    public bool IsGlow(Renderer r) => glowRenderer != null && r == glowRenderer;

    private void CreateGlow(float feetHeight, int layer)
    {
        Material material = GlowMaterial();

        glow = new GameObject("Hover Glow").transform;
        glow.SetParent(transform, false);
        glow.localPosition = new Vector3(0f, feetHeight + 0.03f, 0f);
        glow.gameObject.layer = layer;

        if (material != null)
        {
            // A flat quad facing up, painted with a round falloff: a pool of light, not a solid disc
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
            disc.name = "Disc";
            // Right away, not at the end of the frame: a collider under the player would trip its own movement
            DestroyImmediate(disc.GetComponent<Collider>());
            disc.layer = layer;
            disc.transform.SetParent(glow, false);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localScale = new Vector3(GlowSize, GlowSize, 1f);
            glowRenderer = disc.GetComponent<Renderer>();
            glowRenderer.sharedMaterial = material;
            glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowRenderer.receiveShadows = false;
            glowRenderer.GetPropertyBlock(block);
            block.SetTexture("_BaseMap", GlowTexture()); // URP
            block.SetTexture("_MainTex", GlowTexture()); // built-in shaders
            glowRenderer.SetPropertyBlock(block);
        }

        GameObject lightObject = new GameObject("Light");
        lightObject.transform.SetParent(glow, false);
        lightObject.transform.localPosition = Vector3.up * 0.25f;
        glowLight = lightObject.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.range = 2.5f;
        glowLight.shadows = LightShadows.None;

        RefreshColor();
    }

    // White, its alpha falling off from the middle: full at the centre, a soft shoulder, nothing at the rim
    private static Texture2D GlowTexture()
    {
        if (glowTexture != null) return glowTexture;
        const int size = 64;
        glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Hover Glow Falloff",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r);
                a = a * a * (0.6f + 0.4f * a);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        glowTexture.SetPixels32(pixels);
        glowTexture.Apply();
        return glowTexture;
    }

    // A see-through copy of the guns' beam material, which is in every build
    private static Material GlowMaterial()
    {
        if (glowMaterialLooked) return glowMaterial;
        glowMaterialLooked = true;

        GameObject gun = Resources.Load<GameObject>("Pistol");
        LineRenderer line = gun != null ? gun.GetComponentInChildren<LineRenderer>(true) : null;
        if (line == null || line.sharedMaterial == null) return null;

        glowMaterial = new Material(line.sharedMaterial) { name = "Hover Glow" };
        return glowMaterial;
    }

    private void RefreshColor()
    {
        colorVersion = Manager.TeamsVersion;
        color = Color.Lerp(PlayerCap.ColorFor(player.ActorId), Color.white, 0.25f);
        if (glowLight != null) glowLight.color = color;
    }

    // How far the body is pushed down (negative) or up by the hover cushion right now, in metres
    public float Suspension => cushion;

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || player == null) return;
        if (colorVersion != Manager.TeamsVersion) RefreshColor();

        // Velocity from how this player moved here, evened out a little; the springs below smooth the rest
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        // Big jumps are respawns or network snaps, not movement
        bool snapped = delta.sqrMagnitude > 25f;
        Vector3 measured = snapped ? Vector3.zero : delta / dt;
        float verticalSpeed = measured.y;
        measured.y = 0f;
        velocity = Vector3.Lerp(velocity, measured, 1f - Mathf.Exp(-dt / 0.05f));
        moving = Mathf.Clamp01(velocity.magnitude / 10f);

        // A sudden change in vertical speed (landing, lifting off, a step) pushes the cushion the other
        // way: the body keeps going for a moment and the hover field catches it
        float change = snapped ? 0f : Mathf.Clamp(verticalSpeed - lastVerticalSpeed, -30f, 30f);
        lastVerticalSpeed = snapped ? 0f : verticalSpeed;
        cushionSpeed -= change * cushionPerSpeed * cushionFrequency;
        Spring(ref cushion, ref cushionSpeed, 0f, cushionFrequency, cushionDamping, dt);
        if (cushion < -cushionDown) { cushion = -cushionDown; cushionSpeed = Mathf.Max(0f, cushionSpeed); }
        if (cushion > cushionUp) { cushion = cushionUp; cushionSpeed = Mathf.Min(0f, cushionSpeed); }

        phase += dt * bobSpeed * Mathf.PI * 2f;
        float bob = Mathf.Sin(phase);

        if (body != null)
        {
            Vector3 local = transform.InverseTransformDirection(velocity);
            // Positive pitch tips the top forward; negative roll tips it to the right
            Vector2 target = new Vector2(Mathf.Clamp(local.z * leanPerSpeed, -maxLean, maxLean), Mathf.Clamp(-local.x * leanPerSpeed, -maxLean, maxLean));
            Spring(ref lean.x, ref leanSpeed.x, target.x, leanFrequency, leanDamping, dt);
            Spring(ref lean.y, ref leanSpeed.y, target.y, leanFrequency, leanDamping, dt);
            lean.x = Mathf.Clamp(lean.x, -maxLean * 1.25f, maxLean * 1.25f);
            lean.y = Mathf.Clamp(lean.y, -maxLean * 1.25f, maxLean * 1.25f);
            body.localRotation = bodyRotation * Quaternion.Euler(lean.x, 0f, lean.y);

            float bobAmount = bobHeight * Mathf.Lerp(1f, movingBob, moving);
            body.localPosition = bodyPosition + Vector3.up * (lift + bob * bobAmount + cushion);
        }

        // Brighter while moving, and a little smaller and fainter at the top of each bob
        float strength = Mathf.Lerp(0.55f, 1f, moving) * (1f - 0.15f * bob);
        float spread = 1f;
        if (glow != null)
        {
            // On the ground below, however high the body is: fainter and wider the higher it is
            Vector3 from = transform.position + Vector3.up * (feet + 0.3f);
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, glowFadeHeight + 0.3f, Player.WorldMask, QueryTriggerInteraction.Ignore))
            {
                float height = Mathf.Max(0f, hit.distance - 0.3f);
                glow.position = hit.point + hit.normal * 0.03f;
                glow.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                strength *= 1f - Mathf.InverseLerp(0.3f, glowFadeHeight, height);
                spread = 1f + 0.35f * Mathf.Clamp01(height / glowFadeHeight);
            }
            else
            {
                strength = 0f;
            }
        }

        if (glowRenderer != null)
        {
            glowRenderer.enabled = strength > 0.01f;
            glowRenderer.transform.localScale = new Vector3(GlowSize, GlowSize, 1f) * (1f - 0.08f * bob) * spread;
            glowRenderer.GetPropertyBlock(block);
            Color c = new Color(color.r, color.g, color.b, 0.75f * strength);
            block.SetColor("_BaseColor", c); // URP
            block.SetColor("_Color", c);     // built-in shaders
            glowRenderer.SetPropertyBlock(block);
        }
        if (glowLight != null)
        {
            glowLight.enabled = strength > 0.01f;
            glowLight.intensity = 1.6f * strength;
        }
    }

    // A damped spring pulling x towards target, stepped in small pieces so it stays steady at low frame rates
    private static void Spring(ref float x, ref float speed, float target, float frequency, float damping, float dt)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(dt * 120f));
        float h = dt / steps;
        for (int i = 0; i < steps; i++)
        {
            speed += (-frequency * frequency * (x - target) - 2f * damping * frequency * speed) * h;
            x += speed * h;
        }
    }
}
