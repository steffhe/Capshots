using UnityEngine;

// Small looping animations for hat parts (see PlayerCap): turning round and round, sweeping back
// and forth, bobbing up and down, flickering like a flame, rising and popping like a bubble, or a
// glow pulsing, twinkling or blinking. Each starts at a random point in its loop, so a room full of
// the same hat doesn't move in step. Only a look: nothing is sent.
public class HatMotion : MonoBehaviour
{
    public enum Kind { Spin, Sweep, Bob, Flicker, Rise, Pulse, Twinkle, Blink }

    private Kind kind;
    private Vector3 axis;
    // Spin: degrees a second. The rest: how fast it goes, in loops a second (roughly)
    private float speed;
    // Sweep: degrees each way. Bob: distance each way, Rise: distance risen (metres). Flicker: how
    // much it changes size. Pulse, Twinkle: how much the glow changes (0 to 1). Blink: the part of
    // each loop it's lit for.
    private float amount;
    private float phase;
    private float angle;
    private Vector3 basePosition, baseScale;
    private Quaternion baseRotation;
    // The glowing kinds: the renderer whose glow changes, and the hat it belongs to (a locked preview doesn't glow)
    private Renderer glowRenderer;
    private PlayerCap cap;
    private MaterialPropertyBlock block;

    public static HatMotion Add(GameObject part, Kind kind, Vector3 axis, float speed, float amount = 0f)
    {
        HatMotion motion = part.AddComponent<HatMotion>();
        motion.kind = kind;
        motion.axis = axis.sqrMagnitude > 0f ? axis.normalized : Vector3.up;
        motion.speed = speed;
        motion.amount = amount;
        motion.phase = Random.value * 100f;
        motion.basePosition = part.transform.localPosition;
        motion.baseRotation = part.transform.localRotation;
        motion.baseScale = part.transform.localScale;
        if (kind == Kind.Pulse || kind == Kind.Twinkle || kind == Kind.Blink)
        {
            motion.glowRenderer = part.GetComponent<Renderer>();
            motion.cap = part.GetComponentInParent<PlayerCap>();
        }
        return motion;
    }

    private void LateUpdate()
    {
        float t = Time.time + phase;
        switch (kind)
        {
            case Kind.Spin:
                angle = Mathf.Repeat(angle + speed * Time.deltaTime, 360f);
                transform.localRotation = baseRotation * Quaternion.AngleAxis(angle, axis);
                break;
            case Kind.Sweep:
                transform.localRotation = baseRotation * Quaternion.AngleAxis(Mathf.Sin(t * speed * 2f * Mathf.PI) * amount, axis);
                break;
            case Kind.Bob:
                transform.localPosition = basePosition + axis * (Mathf.Sin(t * speed * 2f * Mathf.PI) * amount);
                break;
            case Kind.Flicker:
                // Two noises: one for its height, which flickers most, and one for its width
                float tall = Mathf.PerlinNoise(t * speed, 0.3f) - 0.5f;
                float wide = Mathf.PerlinNoise(0.7f, t * speed) - 0.5f;
                transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + wide * amount, 1f + tall * amount * 2f, 1f + wide * amount));
                break;
            case Kind.Rise:
            {
                // Rises from its start, growing in as it starts and popping at the top
                float u = Mathf.Repeat(t * speed, 1f);
                transform.localPosition = basePosition + axis * (u * amount);
                float grow = Mathf.Clamp01(u / 0.15f) * Mathf.Clamp01((1f - u) / 0.08f);
                transform.localScale = baseScale * grow;
                break;
            }
            case Kind.Pulse:
                Glow(1f - amount * 0.5f * (1f - Mathf.Cos(t * speed * 2f * Mathf.PI)));
                break;
            case Kind.Twinkle:
            {
                // A soft shimmer with a bright glint now and then
                float shimmer = Mathf.PerlinNoise(t * speed, 0.5f);
                float glint = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * speed * 0.7f)), 24f);
                Glow(1f - amount * (1f - shimmer) + glint * 1.5f);
                transform.localScale = baseScale * (1f + glint * 0.35f);
                break;
            }
            case Kind.Blink:
                Glow(Mathf.Repeat(t * speed, 1f) < amount ? 1.4f : 0.08f);
                break;
        }
    }

    // Sets how bright the part glows, as a share of its full glow
    private void Glow(float share)
    {
        if (glowRenderer == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        glowRenderer.GetPropertyBlock(block);
        bool off = cap != null && cap.IsLocked;
        Color baseColor = block.GetColor("_BaseColor");
        block.SetColor("_EmissionColor", off ? Color.black : baseColor * (PlayerCap.GlowStrength * Mathf.Max(0f, share)));
        glowRenderer.SetPropertyBlock(block);
    }
}
