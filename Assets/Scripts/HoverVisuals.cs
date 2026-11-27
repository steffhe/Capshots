using UnityEngine;

// Makes the players look like they hover instead of walk. Added by Player at runtime, so no prefab changes
// are needed. Runs on every client from how the player moves on this machine, so it needs no network traffic.
//  - The body floats a few centimetres up and bobs gently, and leans into the direction it moves, like a drone.
//  - A soft glow under the body (brightest in the middle, fading to nothing at its edge, so it reads as
//    light rather than a solid disc), with a small light that tints the ground, in the player's colour
//    (the team colour in Team Deathmatch). It pulses with the bob and brightens while moving.
// Only the body model moves: the hitboxes stay put, so the lean is kept small to keep them lined up.
public class HoverVisuals : MonoBehaviour
{
    // Degrees of lean per m/s, and the most it leans (walking is about 10 m/s, sprinting about 15)
    public float leanPerSpeed = 0.5f;
    public float maxLean = 8f;
    // How high the body floats above where it used to stand, and how far it bobs up and down
    public float lift = 0.05f;
    public float bobHeight = 0.03f;
    public float bobSpeed = 0.7f; // bobs per second

    private Player player;
    private Transform body;
    private Vector3 bodyPosition;
    private Quaternion bodyRotation;

    private Vector3 lastPosition;
    private Vector3 velocity;
    private float phase;
    private float moving;

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

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || player == null) return;
        if (colorVersion != Manager.TeamsVersion) RefreshColor();

        // Velocity from how this player moved here, smoothed, so network updates don't make it twitch
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        // Big jumps are respawns or network snaps, not movement
        Vector3 measured = delta.sqrMagnitude > 25f ? Vector3.zero : delta / dt;
        measured.y = 0f;
        velocity = Vector3.Lerp(velocity, measured, 1f - Mathf.Exp(-dt / 0.12f));
        moving = Mathf.Clamp01(velocity.magnitude / 10f);

        phase += dt * bobSpeed * Mathf.PI * 2f;
        float bob = Mathf.Sin(phase);

        if (body != null)
        {
            Vector3 local = transform.InverseTransformDirection(velocity);
            // Positive pitch tips the top forward; negative roll tips it to the right
            float pitch = Mathf.Clamp(local.z * leanPerSpeed, -maxLean, maxLean);
            float roll = Mathf.Clamp(-local.x * leanPerSpeed, -maxLean, maxLean);
            body.localRotation = bodyRotation * Quaternion.Euler(pitch, 0f, roll);
            body.localPosition = bodyPosition + Vector3.up * (lift + bob * bobHeight);
        }

        // Brighter while moving, and a little smaller and fainter at the top of each bob
        float strength = Mathf.Lerp(0.55f, 1f, moving) * (1f - 0.15f * bob);
        if (glowRenderer != null)
        {
            glowRenderer.transform.localScale = new Vector3(GlowSize, GlowSize, 1f) * (1f - 0.08f * bob);
            glowRenderer.GetPropertyBlock(block);
            Color c = new Color(color.r, color.g, color.b, 0.75f * strength);
            block.SetColor("_BaseColor", c); // URP
            block.SetColor("_Color", c);     // built-in shaders
            glowRenderer.SetPropertyBlock(block);
        }
        if (glowLight != null) glowLight.intensity = 1.6f * strength;
    }
}
