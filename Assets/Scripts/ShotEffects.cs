using System.Collections.Generic;
using UnityEngine;

// Reuses scorch marks, laser beams and impact sparks instead of making and destroying them for every shot. A
// shotgun blast is 8 of each and the machine gun fires 40 times a second, so in a full room that
// churn of objects (and the garbage it leaves) cost frames, most of all in the browser.
// It's made on the first shot and belongs to the map's scene, so it goes with the map and the next
// map starts with a fresh one.
public class ShotEffects : MonoBehaviour
{
    // Enough for a long firefight; past this, the oldest mark is moved to the newest spot
    private const int MaxMarks = 96;
    private const float MarkLife = 5f;

    private static ShotEffects instance;

    private readonly GameObject[] marks = new GameObject[MaxMarks];
    private readonly float[] markExpiry = new float[MaxMarks];
    private int nextMark;

    private readonly Stack<BeamFade> freeBeams = new Stack<BeamFade>();

    // Sparks where a shot hits the world: small streaks moved here, like KillSpark's but made once
    // and reused. Past this many at once, the oldest is reused early.
    private const int MaxSparks = 160;
    private const float Gravity = 9.81f;
    private static readonly Color HotWhite = new Color(1f, 0.92f, 0.75f);

    private readonly Transform[] sparks = new Transform[MaxSparks];
    private readonly Renderer[] sparkRenderers = new Renderer[MaxSparks];
    private readonly Vector3[] sparkVelocity = new Vector3[MaxSparks];
    private readonly Vector3[] sparkScale = new Vector3[MaxSparks];
    private readonly float[] sparkLife = new float[MaxSparks];
    private readonly float[] sparkAge = new float[MaxSparks];
    private int nextSpark;
    private MaterialPropertyBlock sparkBlock;

    private static ShotEffects Instance
    {
        get
        {
            if (instance == null) instance = new GameObject("Shot Effects").AddComponent<ShotEffects>();
            return instance;
        }
    }

    // A scorch mark at point, facing out along normal, for 5 seconds
    public static void Mark(GameObject prefab, Vector3 point, Vector3 normal)
    {
        if (prefab == null) return;
        ShotEffects fx = Instance;

        int i = fx.nextMark;
        fx.nextMark = (i + 1) % MaxMarks;

        GameObject mark = fx.marks[i];
        if (mark == null)
        {
            mark = Instantiate(prefab, fx.transform);
            fx.marks[i] = mark;
        }
        mark.transform.position = point + normal * 0.001f;
        mark.transform.LookAt(point + normal);
        mark.SetActive(true);
        fx.markExpiry[i] = Time.time + MarkLife;
    }

    // A few sparks spat off a surface at point, mostly white-hot with some in the shooter's colour
    public static void Sparks(Vector3 point, Vector3 normal, Color color, int count)
    {
        ShotEffects fx = Instance;
        if (fx.sparkBlock == null) fx.sparkBlock = new MaterialPropertyBlock();
        Vector3 away = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        Color tint = Color.Lerp(color, Color.white, 0.3f);

        for (int n = 0; n < count; n++)
        {
            int i = fx.nextSpark;
            fx.nextSpark = (i + 1) % MaxSparks;

            Transform spark = fx.sparks[i];
            if (spark == null)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyImmediate(go.GetComponent<Collider>());
                go.name = "Impact Spark";
                go.transform.SetParent(fx.transform, false);
                Renderer renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = PlayerCap.SharedMaterial(renderer.sharedMaterial);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                spark = go.transform;
                fx.sparks[i] = spark;
                fx.sparkRenderers[i] = renderer;
            }

            Color c = n % 3 == 0 ? tint : HotWhite;
            fx.sparkBlock.SetColor("_BaseColor", c);
            fx.sparkBlock.SetColor("_Color", c);
            fx.sparkRenderers[i].SetPropertyBlock(fx.sparkBlock);

            // Thin streaks flung off the surface in a cone, then falling
            float size = Random.Range(0.01f, 0.02f);
            fx.sparkScale[i] = new Vector3(size, size, size * 3.5f);
            Vector3 direction = (away + Random.insideUnitSphere * 0.9f).normalized;
            fx.sparkVelocity[i] = direction * Random.Range(1.5f, 4f);
            fx.sparkLife[i] = Random.Range(0.15f, 0.35f);
            fx.sparkAge[i] = 0f;

            spark.position = point + away * 0.02f;
            spark.rotation = Quaternion.LookRotation(direction);
            spark.localScale = fx.sparkScale[i];
            spark.gameObject.SetActive(true);
        }
    }

    // A laser beam set up like template, on the given layer, ready for its positions and colour.
    // Call Begin on it to start the fade; it comes back here when it's done.
    public static BeamFade Beam(LineRenderer template, int layer)
    {
        ShotEffects fx = Instance;

        BeamFade beam = null;
        // A beam can be destroyed along with its scene, so skip any that are gone
        while (beam == null && fx.freeBeams.Count > 0) beam = fx.freeBeams.Pop();
        if (beam == null)
        {
            GameObject beamObject = new GameObject("Laser");
            beamObject.transform.SetParent(fx.transform, false);
            beamObject.AddComponent<LineRenderer>();
            beam = beamObject.AddComponent<BeamFade>();
        }

        beam.gameObject.layer = layer;
        Weapon.CopyLineSettings(template, beam.Line);
        beam.gameObject.SetActive(true);
        return beam;
    }

    // Puts a finished beam away, or removes it if it didn't come from here
    public static void Release(BeamFade beam)
    {
        if (instance != null && beam.transform.parent == instance.transform)
        {
            beam.gameObject.SetActive(false);
            instance.freeBeams.Push(beam);
        }
        else
        {
            Destroy(beam.gameObject);
        }
    }

    void Update()
    {
        float now = Time.time;
        for (int i = 0; i < MaxMarks; i++)
        {
            GameObject mark = marks[i];
            if (mark != null && mark.activeSelf && now >= markExpiry[i]) mark.SetActive(false);
        }

        float dt = Time.deltaTime;
        for (int i = 0; i < MaxSparks; i++)
        {
            if (sparkLife[i] <= 0f) continue;
            Transform spark = sparks[i];
            if (spark == null) { sparkLife[i] = 0f; continue; }

            sparkAge[i] += dt;
            float left = sparkLife[i] - sparkAge[i];
            if (left <= 0f)
            {
                sparkLife[i] = 0f;
                spark.gameObject.SetActive(false);
                continue;
            }

            sparkVelocity[i] += Vector3.down * Gravity * dt;
            spark.position += sparkVelocity[i] * dt;
            if (sparkVelocity[i].sqrMagnitude > 0.01f) spark.rotation = Quaternion.LookRotation(sparkVelocity[i]);
            // Shrink as they burn out
            spark.localScale = sparkScale[i] * Mathf.Clamp01(left / 0.1f);
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
