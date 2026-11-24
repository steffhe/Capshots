using System.Collections.Generic;
using UnityEngine;

// A dead drone's broken neck shorting out: where the head came off, both the stump on the body and
// the underside of the flying head spit little bursts of sparks, quick at first and sparser as the
// drone dies, each with a flicker of light and now and then a faint crackle. Smaller than the kill
// spark (KillSpark) and the cap shot burst (CapshotBurst), so those stay the big moments.
// Added by DeadPlayer when the head comes off. Purely cosmetic and local, like the ragdoll.
public class NeckSparks : MonoBehaviour
{
    // How long it keeps sparking, and the gaps between bursts (they stretch out as it dies)
    private const float Duration = 3f;
    private const float FirstGap = 0.06f, LastGap = 0.45f;
    private const float Gravity = 9.81f;
    private const float FlashIntensity = 1.4f, FlashTime = 0.05f;
    // Very quiet, high-pitched crackles: the kill's zap, sped up
    private const float CrackleVolume = 0.035f;

    private class Spark
    {
        public Transform transform;
        public Vector3 velocity;
        public Vector3 scale;
        public float life, age;
    }

    private readonly List<Spark> sparks = new List<Spark>();
    private Vector3 outward; // in the host's space: the way the broken end faces
    private Color color;
    private float age, nextBurst;
    private Light flash;
    private float flashAge = 1f;
    private AudioSource source;
    private MaterialPropertyBlock block;

    // host: the body or the head. localPoint: the broken end, in its space. localOutward: the way it
    // faces, in its space. color: the dead player's colour.
    public static NeckSparks Attach(Transform host, Vector3 localPoint, Vector3 localOutward, Color color)
    {
        if (host == null) return null;
        GameObject emitter = new GameObject("Neck Sparks");
        emitter.transform.SetParent(host, false);
        emitter.transform.localPosition = localPoint;
        NeckSparks sparks = emitter.AddComponent<NeckSparks>();
        sparks.outward = localOutward.sqrMagnitude > 1e-6f ? localOutward.normalized : Vector3.up;
        sparks.color = Color.Lerp(color, Color.white, 0.5f);
        sparks.nextBurst = Random.Range(0f, 0.05f);
        return sparks;
    }

    private void Awake()
    {
        block = new MaterialPropertyBlock();

        flash = gameObject.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.range = 1.5f;
        flash.intensity = 0f;
        flash.shadows = LightShadows.None;

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1.5f;
        source.maxDistance = 20f;
        source.dopplerLevel = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        if (age < Duration && age >= nextBurst)
        {
            Burst();
            float progress = age / Duration;
            nextBurst = age + Mathf.Lerp(FirstGap, LastGap, progress) * Random.Range(0.5f, 1.5f);
        }

        // The flicker of each burst
        flashAge += dt;
        if (flash != null) flash.intensity = flashAge < FlashTime ? FlashIntensity * (Random.value < 0.3f ? 0.4f : 1f) : 0f;

        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Spark s = sparks[i];
            s.age += dt;
            float left = s.life - s.age;
            if (left <= 0f || s.transform == null)
            {
                if (s.transform != null) Destroy(s.transform.gameObject);
                sparks.RemoveAt(i);
                continue;
            }
            s.velocity += Vector3.down * Gravity * dt;
            s.transform.position += s.velocity * dt;
            if (s.velocity.sqrMagnitude > 0.01f) s.transform.rotation = Quaternion.LookRotation(s.velocity);
            // Shrink as they burn out
            s.transform.localScale = s.scale * Mathf.Clamp01(left / 0.1f);
        }

        if (age >= Duration && sparks.Count == 0) Destroy(gameObject);
    }

    private void Burst()
    {
        float strength = 1f - age / Duration;
        int count = Mathf.Max(1, Mathf.RoundToInt(Random.Range(2f, 6f) * (0.4f + 0.6f * strength)));
        // The broken end may have shrunk away with the ragdoll (or been scaled): sparks keep a real size
        Vector3 direction = transform.parent != null ? transform.parent.TransformDirection(outward).normalized : outward;

        for (int i = 0; i < count; i++)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(spark.GetComponent<Collider>());
            spark.name = "Spark";
            // In the world, not on the host: they fly off on their own instead of tumbling with it
            spark.transform.position = transform.position + Random.insideUnitSphere * 0.03f;

            Renderer renderer = spark.GetComponent<Renderer>();
            renderer.sharedMaterial = PlayerCap.SharedMaterial(renderer.sharedMaterial);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Mostly white-hot, now and then in the player's colour
            Color c = Random.value < 0.3f ? color : new Color(1f, 0.95f, 0.8f);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            renderer.SetPropertyBlock(block);

            Vector3 velocity = (direction + Random.insideUnitSphere * 0.8f).normalized * Random.Range(1f, 3.2f);
            float size = Random.Range(0.008f, 0.016f);
            Spark s = new Spark
            {
                transform = spark.transform,
                velocity = velocity,
                scale = new Vector3(size, size, size * 3.5f),
                life = Random.Range(0.15f, 0.35f),
            };
            spark.transform.rotation = Quaternion.LookRotation(velocity);
            spark.transform.localScale = s.scale;
            sparks.Add(s);
        }

        flashAge = 0f;
        if (flash != null) flash.color = Random.value < 0.5f ? color : new Color(1f, 0.95f, 0.85f);
        if (Random.value < 0.4f)
        {
            source.pitch = Random.Range(1.6f, 2.4f);
            source.PlayOneShot(KillSpark.ZapClip(), CrackleVolume * (0.5f + 0.5f * strength));
        }
    }

    private void OnDestroy()
    {
        // The host (the ragdoll, or its head) went: take any sparks still flying with it
        foreach (Spark s in sparks) if (s.transform != null) Destroy(s.transform.gameObject);
        sparks.Clear();
    }
}
