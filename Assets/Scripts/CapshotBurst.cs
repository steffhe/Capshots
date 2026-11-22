using UnityEngine;

// The burst on a headshot kill: a quick flash, a "pop" with a sparkly ring to it, a spray of confetti in
// the cap's colours and a few yellow four-pointed stars like the sparkle in the logo, flung up and along
// the bullet's path. Each piece is a small mesh moved by this script rather than a ParticleSystem, so it
// can use the same lit material as the caps (a particle shader might be left out of builds).
// Purely cosmetic and local: every client spawns its own burst together with the ragdoll.
public class CapshotBurst : MonoBehaviour
{
    private const int ConfettiCount = 22;
    private const int StarCount = 6;
    private const float Gravity = 9.81f;
    private const float FlashTime = 0.12f;

    // The yellow of the sparkle in the logo
    private static readonly Color StarColor = new Color(1f, 0.82f, 0.31f);

    // Measured (A-weighted) against the other synthesized sounds: about 6 dB under a sniper shot, so
    // louder than reloads and the hover sound and clearly heard across the map, but still under the guns
    private const float PopVolume = 0.123f;

    private Transform[] pieces;
    private Vector3[] velocities;
    private Vector3[] spins;
    private float[] lifetimes;
    private Vector3[] scales;
    private float[] drags;
    private float[] gravityScales;
    private bool[] isStar;
    private Light flash;
    private float age;
    // Height of the floor under the burst: pieces settle on it instead of sinking through
    private float groundY = float.NegativeInfinity;

    private static Mesh starMesh;
    private static AudioClip popClip;

    public static void Spawn(Vector3 position, Vector3 push, Color capColor)
    {
        GameObject root = new GameObject("Capshot Burst");
        root.transform.position = position;
        root.AddComponent<CapshotBurst>().Build(push, capColor);
    }

    private void Build(Vector3 push, Color capColor)
    {
        // Flash of light in the cap's colour, brightest at the start
        flash = gameObject.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = Color.Lerp(capColor, Color.white, 0.4f);
        flash.range = 5f;
        flash.intensity = 4f;
        flash.shadows = LightShadows.None;

        PlayPop();

        // The nearest surface below that isn't moving: the ragdoll, its head and players have Rigidbodies
        float nearest = float.PositiveInfinity;
        foreach (RaycastHit hit in Physics.RaycastAll(transform.position + Vector3.up * 0.5f, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.rigidbody != null || hit.distance >= nearest) continue;
            nearest = hit.distance;
            groundY = hit.point.y;
        }

        Color dark = Color.Lerp(capColor, Color.black, 0.35f);
        Color[] palette = { capColor, dark, Color.white };
        // Sprays mostly upwards, leaning the way the bullet was going
        Vector3 mainDirection = (Vector3.up * 1.5f + push).normalized;

        int count = ConfettiCount + StarCount;
        pieces = new Transform[count];
        velocities = new Vector3[count];
        spins = new Vector3[count];
        lifetimes = new float[count];
        scales = new Vector3[count];
        drags = new float[count];
        gravityScales = new float[count];
        isStar = new bool[count];
        MaterialPropertyBlock block = new MaterialPropertyBlock();

        for (int i = 0; i < count; i++)
        {
            bool star = i >= ConfettiCount;
            isStar[i] = star;

            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(piece.GetComponent<Collider>());
            piece.name = star ? "Sparkle" : "Confetti";
            piece.transform.SetParent(transform, false);
            piece.transform.localPosition = Random.insideUnitSphere * 0.1f;

            Renderer renderer = piece.GetComponent<Renderer>();
            renderer.sharedMaterial = PlayerCap.SharedMaterial(renderer.sharedMaterial);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Color color = star ? StarColor : palette[i % palette.Length];
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);

            if (star)
            {
                // A flat star a bit bigger than the confetti, rising slower and drifting down gently,
                // turning in its own plane with a little wobble so it catches the light
                piece.GetComponent<MeshFilter>().sharedMesh = StarMesh();
                float size = Random.Range(0.12f, 0.18f);
                scales[i] = new Vector3(size, size, size);
                piece.transform.localRotation = Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(0f, 360f), 0f);
                Vector3 direction = (Vector3.up * 2f + push * 0.5f + Random.insideUnitSphere * 0.8f).normalized;
                velocities[i] = direction * Random.Range(2.5f, 4.5f);
                spins[i] = new Vector3(Random.Range(-40f, 40f), Random.Range(-40f, 40f), Random.Range(180f, 360f) * (Random.value < 0.5f ? -1f : 1f));
                lifetimes[i] = Random.Range(1.3f, 1.9f);
                drags[i] = 3.5f;
                gravityScales[i] = 0.3f;
            }
            else
            {
                // Thin flakes of a few centimetres, sprayed in a cone around the main direction
                scales[i] = new Vector3(Random.Range(0.04f, 0.08f), Random.Range(0.04f, 0.08f), 0.006f);
                piece.transform.localRotation = Random.rotation;
                Vector3 direction = (mainDirection + Random.insideUnitSphere * 0.9f).normalized;
                velocities[i] = direction * Random.Range(3f, 7f);
                spins[i] = Random.insideUnitSphere * 720f;
                lifetimes[i] = Random.Range(0.9f, 1.5f);
                drags[i] = 2.5f;
                gravityScales[i] = 1f;
            }

            piece.transform.localScale = scales[i];
            pieces[i] = piece.transform;
        }
    }

    private void PlayPop()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f; // heard from where the head was
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = 80f;
        source.dopplerLevel = 0f;
        source.PlayOneShot(PopClip(), PopVolume);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        if (flash != null)
        {
            flash.intensity = Mathf.Lerp(4f, 0f, age / FlashTime);
            if (age >= FlashTime) { Destroy(flash); flash = null; }
        }

        bool anyLeft = false;
        for (int i = 0; i < pieces.Length; i++)
        {
            Transform piece = pieces[i];
            if (piece == null) continue;

            float left = lifetimes[i] - age;
            if (left <= 0f) { Destroy(piece.gameObject); pieces[i] = null; continue; }
            anyLeft = true;

            if (velocities[i] != Vector3.zero)
            {
                // Falls, slowed by the air like paper, and tumbles
                velocities[i] = velocities[i] * Mathf.Exp(-drags[i] * dt) + Vector3.down * (Gravity * gravityScales[i] * dt);
                Vector3 position = piece.position + velocities[i] * dt;
                piece.Rotate(spins[i] * dt, Space.Self);

                // Landed: lie flat on the floor and stay put
                if (position.y <= groundY + 0.005f && velocities[i].y < 0f)
                {
                    position.y = groundY + 0.005f;
                    velocities[i] = Vector3.zero;
                    piece.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
                }
                piece.position = position;
            }

            // Stars twinkle by pulsing in size; everything shrinks away over its last moments
            float scale = isStar[i] ? 1f + 0.2f * Mathf.Sin(age * 18f + i) : 1f;
            if (left < 0.3f) scale *= left / 0.3f;
            piece.localScale = scales[i] * scale;
        }

        // Stay until the pop has finished playing too
        if (!anyLeft && flash == null && age > PopClip().length) Destroy(gameObject);
    }

    // A flat four-pointed star in the XY plane, one unit across, drawn from both sides.
    // The sides curve inwards between the points, like the sparkle in the logo.
    private static Mesh StarMesh()
    {
        if (starMesh != null) return starMesh;

        const int pointsPerEdge = 6;
        const float waist = 0.1f; // how far in the sides curve, as a fraction of the size
        int rim = 4 * pointsPerEdge;
        Vector3[] outline = new Vector3[rim];
        for (int p = 0; p < 4; p++)
        {
            float a0 = p * Mathf.PI * 0.5f, a1 = a0 + Mathf.PI * 0.5f;
            Vector3 tip0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0)) * 0.5f;
            Vector3 tip1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1)) * 0.5f;
            for (int k = 0; k < pointsPerEdge; k++)
            {
                float t = (float)k / pointsPerEdge;
                // A curve from one tip to the next that bends in towards the centre
                Vector3 straight = Vector3.Lerp(tip0, tip1, t);
                float pull = Mathf.Sin(t * Mathf.PI); // 0 at the tips, 1 halfway
                Vector3 inward = Vector3.Lerp(straight, straight.normalized * waist, pull);
                outline[p * pointsPerEdge + k] = k == 0 ? tip0 : inward;
            }
        }

        // Front: centre + rim, normals towards -Z (Unity's camera-facing side for an unrotated quad);
        // back: the same vertices again with flipped normals and winding
        Vector3[] vertices = new Vector3[(rim + 1) * 2];
        Vector3[] normals = new Vector3[vertices.Length];
        int[] triangles = new int[rim * 6];
        for (int side = 0; side < 2; side++)
        {
            int start = side * (rim + 1);
            Vector3 normal = side == 0 ? Vector3.back : Vector3.forward;
            vertices[start] = Vector3.zero;
            normals[start] = normal;
            for (int k = 0; k < rim; k++)
            {
                vertices[start + 1 + k] = outline[k];
                normals[start + 1 + k] = normal;
            }
            for (int k = 0; k < rim; k++)
            {
                int a = start + 1 + k, b = start + 1 + (k + 1) % rim;
                int t = side * rim * 3 + k * 3;
                triangles[t] = start;
                triangles[t + 1] = side == 0 ? b : a;
                triangles[t + 2] = side == 0 ? a : b;
            }
        }

        starMesh = new Mesh { name = "Sparkle Star" };
        starMesh.vertices = vertices;
        starMesh.normals = normals;
        starMesh.triangles = triangles;
        starMesh.RecalculateBounds();
        return starMesh;
    }

    // A cork-like pop (a fast downward pitch sweep with a click) followed by a short shimmering "ting",
    // synthesized once so no audio file is needed
    private static AudioClip PopClip()
    {
        if (popClip != null) return popClip;

        const int rate = 44100;
        int samples = Mathf.CeilToInt(0.6f * rate);
        float[] data = new float[samples];
        System.Random rng = new System.Random(5);
        float phase = 0f, noiseFilter = 0f, peak = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;

            float frequency = 250f + 650f * Mathf.Exp(-t * 60f);
            phase += 2f * Mathf.PI * frequency / rate;
            float body = Mathf.Sin(phase) * Mathf.Exp(-t * 28f);

            noiseFilter += (noise - noiseFilter) * 0.6f;
            float click = noiseFilter * Mathf.Exp(-t * 220f) * 0.8f;

            float sinceTing = Mathf.Max(0f, t - 0.03f);
            float shimmer = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 9f * t);
            float ting = t > 0.03f
                ? (Mathf.Sin(2f * Mathf.PI * 2350f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 3520f * t) * 0.3f) * Mathf.Exp(-sinceTing * 7f) * shimmer * 0.45f
                : 0f;

            float edge = Mathf.Clamp01(i / 16f) * Mathf.Clamp01((samples - i) / 1024f);
            data[i] = (float)System.Math.Tanh(body + click + ting) * edge;
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }

        // Same peak level as the other synthesized sounds, so PopVolume alone sets how loud it is
        if (peak > 0f)
        {
            float gain = 0.9f / peak;
            for (int i = 0; i < samples; i++) data[i] *= gain;
        }

        popClip = AudioClip.Create("Capshot Pop", samples, 1, rate, false);
        popClip.SetData(data, 0);
        return popClip;
    }
}
