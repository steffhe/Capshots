using UnityEngine;

// The burst on a headshot kill: a quick flash and a spray of confetti in the cap's colour, flung up and
// along the bullet's path. Each piece is a tiny quad moved by this script rather than a ParticleSystem,
// so it can use the same lit material as the caps (a particle shader might be left out of builds).
// Purely cosmetic and local: every client spawns its own burst together with the ragdoll.
public class CapshotBurst : MonoBehaviour
{
    private const int PieceCount = 22;
    private const float Gravity = 9.81f;
    private const float Drag = 2.5f;
    private const float FlashTime = 0.12f;

    private Transform[] pieces;
    private Vector3[] velocities;
    private Vector3[] spins;
    private float[] lifetimes;
    private Vector3[] scales;
    private Light flash;
    private float age;
    // Height of the floor under the burst: pieces settle on it instead of sinking through
    private float groundY = float.NegativeInfinity;

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

        pieces = new Transform[PieceCount];
        velocities = new Vector3[PieceCount];
        spins = new Vector3[PieceCount];
        lifetimes = new float[PieceCount];
        scales = new Vector3[PieceCount];
        MaterialPropertyBlock block = new MaterialPropertyBlock();

        for (int i = 0; i < PieceCount; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(piece.GetComponent<Collider>());
            piece.name = "Confetti";
            piece.transform.SetParent(transform, false);
            piece.transform.localPosition = Random.insideUnitSphere * 0.1f;
            piece.transform.localRotation = Random.rotation;
            // Thin flakes of a few centimetres
            scales[i] = new Vector3(Random.Range(0.04f, 0.08f), Random.Range(0.04f, 0.08f), 0.006f);
            piece.transform.localScale = scales[i];

            Renderer renderer = piece.GetComponent<Renderer>();
            renderer.sharedMaterial = PlayerCap.SharedMaterial(renderer.sharedMaterial);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Color color = palette[i % palette.Length];
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);

            // A cone around the main direction, at a range of speeds so the burst spreads out
            Vector3 direction = (mainDirection + Random.insideUnitSphere * 0.9f).normalized;
            velocities[i] = direction * Random.Range(3f, 7f);
            spins[i] = Random.insideUnitSphere * 720f;
            lifetimes[i] = Random.Range(0.9f, 1.5f);
            pieces[i] = piece.transform;
        }
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
        float damping = Mathf.Exp(-Drag * dt);
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
                velocities[i] = velocities[i] * damping + Vector3.down * (Gravity * dt);
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

            // Shrinks away over its last moments
            if (left < 0.3f) piece.localScale = scales[i] * (left / 0.3f);
        }

        if (!anyLeft && flash == null) Destroy(gameObject);
    }
}
