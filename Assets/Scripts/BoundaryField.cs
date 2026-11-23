using System.Collections.Generic;
using UnityEngine;

// Makes a map's boundary walls visible as force fields. For each wall (a box collider thin in one
// horizontal direction), it adds a glowing panel on the side facing the middle of the map, from
// the ground up to the top of the collider. The colliders themselves are left as they are.
//  - Downtown: on "Bounds", the parent of its invisible wall colliders (the list is left empty).
//  - Test: on "Design", with its four see-through outer wall cubes listed; their own renderers
//    are switched off so only the field shows.
// Shots that hit the field send a ripple across it instead of leaving a scorch mark (see Ripple),
// and the field hums when you're close to it. Where the panels go is worked out by BoundaryLayout.
public class BoundaryField : MonoBehaviour
{
    private static readonly int PlayerPosId = Shader.PropertyToID("_CapshotsPlayerPos");
    private static readonly int RipplesId = Shader.PropertyToID("_CapshotsRipples");

    // Must match the shader's ripple array
    private const int MaxRipples = 8;
    // A hit this close (metres) to a panel counts as hitting the field
    private const float HitTolerance = 0.3f;
    // The hum is at full volume within HumNear metres of the field and silent beyond HumFar
    private const float HumNear = 1.5f, HumFar = 12f;

    [Tooltip("A material using the Capshots/Force Field shader")]
    public Material material;
    [Tooltip("The walls to cover. Leave empty to use every wall-shaped box collider under this object.")]
    public BoxCollider[] walls;
    [Tooltip("Find the ground along each wall with raycasts; falls back to Ground Height where nothing is hit")]
    public bool findGround = true;
    [Tooltip("World height of the ground along the walls; the field's glow starts here")]
    public float groundHeight = 0f;
    [Tooltip("Switch off the walls' own mesh renderers, so only the force field shows")]
    public bool hideWallRenderers = true;
    [Tooltip("Volume of the hum right next to the field")]
    public float humVolume = 0.035f;

    private static readonly List<BoundaryField> active = new List<BoundaryField>();
    private static readonly Vector4[] ripples = new Vector4[MaxRipples];
    private static int nextRipple;
    private static AudioClip humClip;

    private readonly List<BoundaryPanel> panels = new List<BoundaryPanel>();
    private readonly List<float> bottoms = new List<float>();
    private AudioSource hum;

    // Called for every shot that hits the world (on every client). If it hit a force field, sends a
    // ripple across it from there and returns true, so the shot leaves no scorch mark.
    public static bool Ripple(Vector3 point)
    {
        foreach (BoundaryField field in active)
        {
            for (int i = 0; i < field.panels.Count; i++)
            {
                Vector3 closest = field.panels[i].ClosestPoint(point, field.bottoms[i]);
                if ((closest - point).sqrMagnitude > HitTolerance * HitTolerance) continue;

                // _Time.y in shaders is the time since the scene loaded
                ripples[nextRipple] = new Vector4(point.x, point.y, point.z, Time.timeSinceLevelLoad);
                nextRipple = (nextRipple + 1) % MaxRipples;
                Shader.SetGlobalVectorArray(RipplesId, ripples);
                return true;
            }
        }
        return false;
    }

    private void Start()
    {
        if (material == null) return;

        BoxCollider[] candidates = walls != null && walls.Length > 0 ? walls : GetComponentsInChildren<BoxCollider>();
        var wallBounds = new List<Bounds>();
        foreach (BoxCollider wall in candidates)
        {
            if (wall == null || !BoundaryLayout.IsWall(wall.bounds)) continue;
            wallBounds.Add(wall.bounds);
            if (hideWallRenderers && wall.TryGetComponent(out MeshRenderer own)) own.enabled = false;
        }

        foreach (BoundaryPanel panel in BoundaryLayout.Build(wallBounds))
        {
            float bottom = findGround ? FindGround(panel) : groundHeight;
            if (panel.top <= bottom) continue;
            panels.Add(panel);
            bottoms.Add(bottom);
            AddPanel(panel, bottom);
        }
        if (panels.Count == 0) return;

        ClearRipples();
        active.Add(this);
        StartHum();
    }

    private void OnDestroy()
    {
        active.Remove(this);
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        Vector3 pos = cam != null ? cam.transform.position : new Vector3(0f, -100000f, 0f);
        Shader.SetGlobalVector(PlayerPosId, pos);

        // The hum comes from the nearest point on the field, so it's heard from that direction
        if (hum == null || panels.Count == 0) return;
        Vector3 nearest = pos;
        float best = float.PositiveInfinity;
        for (int i = 0; i < panels.Count; i++)
        {
            Vector3 q = panels[i].ClosestPoint(pos, bottoms[i]);
            float d = (q - pos).sqrMagnitude;
            if (d < best) { best = d; nearest = q; }
        }
        hum.transform.position = nearest;
    }

    private void AddPanel(BoundaryPanel panel, float bottom)
    {
        Vector3 a = panel.start, c = panel.end;
        float height = panel.top - bottom;

        Mesh mesh = new Mesh { name = "Force Field" };
        // The panel sits at this object's origin, so its corners go in as local positions
        mesh.vertices = new[]
        {
            transform.InverseTransformPoint(new Vector3(a.x, bottom, a.z)), transform.InverseTransformPoint(new Vector3(c.x, bottom, c.z)),
            transform.InverseTransformPoint(new Vector3(a.x, panel.top, a.z)), transform.InverseTransformPoint(new Vector3(c.x, panel.top, c.z)),
        };
        // x: metres along the wall (world-aligned so the grid lines up at the corners), y: metres up
        float u0 = panel.alongX ? a.x : a.z, u1 = panel.alongX ? c.x : c.z;
        mesh.uv = new[] { new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u0, height), new Vector2(u1, height) };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject go = new GameObject("Force Field", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    // The lowest ground hit just inside the wall, sampled along its length (the lowest, so a
    // building or crate against the wall doesn't lift the whole field)
    private float FindGround(BoundaryPanel panel)
    {
        float lowest = float.PositiveInfinity;
        const int samples = 9;
        for (int i = 0; i < samples; i++)
        {
            Vector3 p = Vector3.Lerp(panel.start, panel.end, (i + 0.5f) / samples) + panel.inward;
            p.y = panel.top;
            if (Physics.Raycast(p, Vector3.down, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
                lowest = Mathf.Min(lowest, hit.point.y);
        }
        return float.IsPositiveInfinity(lowest) ? groundHeight : lowest;
    }

    private static void ClearRipples()
    {
        for (int i = 0; i < MaxRipples; i++) ripples[i] = new Vector4(0f, -100000f, 0f, -1000f);
        nextRipple = 0;
        Shader.SetGlobalVectorArray(RipplesId, ripples);
    }

    #region Hum

    private void StartHum()
    {
        if (humClip == null) humClip = MakeHumLoop();

        GameObject go = new GameObject("Force Field Hum");
        go.transform.SetParent(transform, false);
        hum = go.AddComponent<AudioSource>();
        hum.clip = humClip;
        hum.loop = true;
        hum.playOnAwake = false;
        hum.volume = humVolume;
        hum.spatialBlend = 1f;
        hum.dopplerLevel = 0f;
        hum.rolloffMode = AudioRolloffMode.Custom;
        hum.maxDistance = HumFar;
        // Full within HumNear, then easing out to silence at HumFar (Unity reads the curve over 0..1 of maxDistance)
        float near = HumNear / HumFar;
        hum.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(near, 1f, 0f, 0f), new Keyframe(near + (1f - near) * 0.35f, 0.3f), new Keyframe(1f, 0f, 0f, 0f)));
        hum.Play();
    }

    // A seamless 2 second loop of an electric hum: a buzzing 100 Hz tone with harmonics, a slow
    // pulse, and a faint high whine that swells and fades. Every part fits a whole number of
    // cycles into the loop, so it repeats without a click.
    private static AudioClip MakeHumLoop()
    {
        const int rate = 44100;
        const float length = 2f;
        int samples = Mathf.RoundToInt(length * rate);
        float[] data = new float[samples];
        float peak = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            float w = 2f * Mathf.PI * t;
            float sine = Mathf.Sin(w * 100f);
            // A little clipping on the fundamental gives it the buzz of mains hum
            float buzz = (float)System.Math.Tanh(3f * sine) * 0.5f + sine * 0.5f;
            float tone = buzz
                + 0.45f * Mathf.Sin(w * 200f + 0.3f)
                + 0.25f * Mathf.Sin(w * 300f + 1.1f)
                + 0.12f * Mathf.Sin(w * 500f + 2.0f);
            // Pulses three times a second, with a slower swell over the whole loop
            float pulse = 0.8f + 0.12f * Mathf.Sin(w * 3f) + 0.08f * Mathf.Sin(w * 0.5f);
            float whine = 0.06f * Mathf.Sin(w * 1750f) * (0.5f + 0.5f * Mathf.Sin(w * 0.5f));
            float s = tone * pulse + whine;
            data[i] = s;
            peak = Mathf.Max(peak, Mathf.Abs(s));
        }
        float scale = peak > 0f ? 0.8f / peak : 1f;
        for (int i = 0; i < samples; i++) data[i] *= scale;

        AudioClip clip = AudioClip.Create("Force Field Hum", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    #endregion
}
