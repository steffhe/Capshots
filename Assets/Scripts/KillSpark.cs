using UnityEngine;

// What an ordinary kill (not a cap shot) leaves behind: the drone shorting out. A quick flash in the
// dead player's colour, a handful of tiny bright sparks spitting out and falling, and a short electric
// zap. Deliberately small and with no stars or confetti, so the cap shot burst (CapshotBurst) stays
// the big one. Purely cosmetic and local: every client spawns its own with the ragdoll.
public class KillSpark : MonoBehaviour
{
    private const int SparkCount = 14;
    private const float Gravity = 9.81f;
    private const float FlashTime = 0.1f;
    private const float FlashIntensity = 3f;
    // Well under the cap shot pop (0.25): heard nearby, not across the map
    private const float ZapVolume = 0.09f;

    private Transform[] sparks;
    private Vector3[] velocities;
    private float[] lifetimes;
    private Vector3[] scales;
    private Light flash;
    private float age;

    private static AudioClip zapClip;

    public static void Spawn(Vector3 position, Vector3 push, Color color)
    {
        GameObject root = new GameObject("Kill Spark");
        root.transform.position = position;
        root.AddComponent<KillSpark>().Build(push, color);
    }

    private void Build(Vector3 push, Color color)
    {
        Color bright = Color.Lerp(color, Color.white, 0.5f);

        flash = gameObject.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = bright;
        flash.range = 4f;
        flash.intensity = FlashIntensity;
        flash.shadows = LightShadows.None;

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = 40f;
        source.dopplerLevel = 0f;
        source.PlayOneShot(ZapClip(), ZapVolume);

        Vector3 away = push.sqrMagnitude > 0.001f ? push.normalized : Vector3.forward;
        sparks = new Transform[SparkCount];
        velocities = new Vector3[SparkCount];
        lifetimes = new float[SparkCount];
        scales = new Vector3[SparkCount];
        MaterialPropertyBlock block = new MaterialPropertyBlock();

        for (int i = 0; i < SparkCount; i++)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(spark.GetComponent<Collider>());
            spark.name = "Spark";
            spark.transform.SetParent(transform, false);
            spark.transform.localPosition = Random.insideUnitSphere * 0.15f;

            Renderer renderer = spark.GetComponent<Renderer>();
            renderer.sharedMaterial = PlayerCap.SharedMaterial(renderer.sharedMaterial);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Mostly white-hot, a few in the player's colour
            Color c = i % 3 == 0 ? bright : new Color(1f, 0.95f, 0.8f);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            renderer.SetPropertyBlock(block);

            // Short thin streaks, spat out all round, a little more the way the bullet went
            float size = Random.Range(0.015f, 0.03f);
            scales[i] = new Vector3(size, size, size * 3f);
            Vector3 direction = (Random.onUnitSphere + away * 0.6f + Vector3.up * 0.5f).normalized;
            velocities[i] = direction * Random.Range(2f, 5f);
            spark.transform.rotation = Quaternion.LookRotation(direction);
            lifetimes[i] = Random.Range(0.25f, 0.5f);
            spark.transform.localScale = scales[i];
            sparks[i] = spark.transform;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        if (flash != null)
        {
            // Flickers as it dies, like a short circuit
            float flicker = Random.value < 0.3f ? 0.3f : 1f;
            flash.intensity = Mathf.Lerp(FlashIntensity, 0f, age / FlashTime) * flicker;
            if (age >= FlashTime) { Destroy(flash); flash = null; }
        }

        bool anyLeft = false;
        for (int i = 0; i < sparks.Length; i++)
        {
            Transform spark = sparks[i];
            if (spark == null) continue;

            float left = lifetimes[i] - age;
            if (left <= 0f) { Destroy(spark.gameObject); sparks[i] = null; continue; }
            anyLeft = true;

            velocities[i] += Vector3.down * Gravity * dt;
            spark.position += velocities[i] * dt;
            if (velocities[i].sqrMagnitude > 0.01f) spark.rotation = Quaternion.LookRotation(velocities[i]);
            // Shrink as they burn out
            spark.localScale = scales[i] * Mathf.Clamp01(left / 0.15f);
        }

        if (!anyLeft && flash == null && age > ZapClip().length) Destroy(gameObject);
    }

    // A short electric zap: a buzzy crackle that drops in pitch, synthesized once
    private static AudioClip ZapClip()
    {
        if (zapClip != null) return zapClip;

        const int rate = 44100;
        const float length = 0.3f;
        int samples = Mathf.CeilToInt(length * rate);
        float[] data = new float[samples];
        System.Random rng = new System.Random(11);
        float phase = 0f, peak = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float frequency = Mathf.Lerp(900f, 120f, Mathf.Sqrt(t / length));
            phase += 2f * Mathf.PI * frequency / rate;
            // A square-ish buzz, chopped on and off for the crackle
            float buzz = Mathf.Sign(Mathf.Sin(phase)) * 0.5f + Mathf.Sin(phase) * 0.3f;
            float chop = Mathf.Sin(2f * Mathf.PI * 47f * t) > -0.2f ? 1f : 0.2f;
            float envelope = Mathf.Exp(-t * 11f);
            float edge = Mathf.Clamp01(i / 32f) * Mathf.Clamp01((samples - i) / 512f);
            data[i] = (buzz * chop + noise * 0.35f) * envelope * edge;
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }

        // Same peak level as the other synthesized sounds, so ZapVolume alone sets how loud it is
        if (peak > 0f)
        {
            float gain = 0.9f / peak;
            for (int i = 0; i < samples; i++) data[i] *= gain;
        }

        zapClip = AudioClip.Create("Kill Zap", samples, 1, rate, false);
        zapClip.SetData(data, 0);
        return zapClip;
    }
}
