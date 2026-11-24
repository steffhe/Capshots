using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// The Office map's glass walls, pane by pane. A pane cracks the first time it's shot and
// shatters the second time: it falls to the floor in pieces and leaves a gap you can walk and
// shoot through.
//
// The host decides: Weapon calls Shoot for every shot it fires, and the panes the shot passed
// through are counted on the Manager (Manager.Glass.cs), which tells every player. Each player's
// Manager then calls SetState here, so everyone sees the same broken panes, late joiners too.
//
// Panes are drawn merged into one mesh per 24 m square (one for intact glass, one for cracked
// glass and one for the frosted bands); a square is rebuilt when one of its panes changes.
public class OfficeGlass : MonoBehaviour
{
    public static OfficeGlass Instance { get; private set; }

    public const int Intact = 0, Cracked = 1, Broken = 2;

    private const float CellSize = 24f;
    private const int CollidersPerObject = 256;
    private const int MaxShards = 700;

    private class Pane
    {
        public OfficeMapData.Pane data;
        public int state;
        public Chunk chunk;
        public BoxCollider collider;
    }

    private class Chunk
    {
        public readonly List<Pane> panes = new List<Pane>();
        public Mesh intact, cracked, frost;
        public bool dirty = true;
    }

    private struct Shard
    {
        public Vector3 position, velocity, size, spin;
        public Quaternion rotation;
        public float age, life;
        public bool resting;
    }

    private readonly List<Pane> panes = new List<Pane>();
    private readonly List<Chunk> chunks = new List<Chunk>();
    private readonly Dictionary<Collider, int> paneOfCollider = new Dictionary<Collider, int>();
    private readonly List<Shard> shards = new List<Shard>();
    private readonly OfficeMap.MeshBuilder builder = new OfficeMap.MeshBuilder();
    private int glassMask;
    private Material shardMaterial;
    private Mesh shardMesh;

    private static AudioClip crackClip, shatterClip;
    private static Texture2D crackTexture;
    private static readonly RaycastHit[] hits = new RaycastHit[16];

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Build(List<OfficeMapData.Pane> paneData, Material glass, Material frost, int glassLayer)
    {
        Instance = this;
        glassMask = 1 << glassLayer;

        // Cracked glass: the glass colour, with the cracks drawn in its texture
        Material cracked = new Material(glass) { name = "Office cracked glass" };
        Color tint = glass.HasProperty("_BaseColor") ? glass.GetColor("_BaseColor") : Color.white;
        if (cracked.HasProperty("_BaseColor")) cracked.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, 1f));
        if (cracked.HasProperty("_BaseMap")) cracked.SetTexture("_BaseMap", CrackTexture());

        shardMaterial = new Material(glass) { name = "Office glass shard" };
        if (shardMaterial.HasProperty("_BaseColor")) shardMaterial.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, 0.5f));
        shardMesh = new Mesh { name = "Glass shard" };
        builder.Clear();
        OfficeMap.AddBox(builder, Vector3.zero, Vector3.one, 0f);
        builder.Fill(shardMesh);

        Transform meshes = new GameObject("Glass") { layer = gameObject.layer }.transform;
        meshes.SetParent(transform, false);
        Transform colliders = new GameObject("Glass Colliders").transform;
        colliders.SetParent(transform, false);

        var chunkOf = new Dictionary<(int, int), Chunk>();
        GameObject holder = null;
        for (int i = 0; i < paneData.Count && i < OfficeMapData.MaxPanes; i++)
        {
            OfficeMapData.Pane d = paneData[i];
            var key = (Mathf.FloorToInt(d.center.x / CellSize), Mathf.FloorToInt(d.center.z / CellSize));
            if (!chunkOf.TryGetValue(key, out Chunk chunk))
            {
                chunk = new Chunk();
                chunk.intact = NewRenderer(meshes, "Glass", glass);
                chunk.cracked = NewRenderer(meshes, "Cracked Glass", cracked);
                chunk.frost = NewRenderer(meshes, "Frosted Glass", frost);
                chunkOf[key] = chunk;
                chunks.Add(chunk);
            }

            if (i % CollidersPerObject == 0)
            {
                holder = new GameObject("Panes") { layer = glassLayer };
                holder.transform.SetParent(colliders, false);
            }
            BoxCollider collider = holder.AddComponent<BoxCollider>();
            collider.center = d.center;
            collider.size = d.size;
            paneOfCollider[collider] = i;

            var pane = new Pane { data = d, chunk = chunk, collider = collider };
            chunk.panes.Add(pane);
            panes.Add(pane);
        }
        if (paneData.Count > OfficeMapData.MaxPanes)
        {
            Debug.LogWarning("OfficeGlass: only the first " + OfficeMapData.MaxPanes + " glass panes can break");
        }
        RebuildDirty();
    }

    private static Mesh NewRenderer(Transform parent, string name, Material material)
    {
        var go = new GameObject(name) { layer = parent.gameObject.layer };
        go.transform.SetParent(parent, false);
        var mesh = new Mesh { name = "Office " + name };
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return mesh;
    }

    #region Breaking

    // On the host, for each pellet: every pane between the gun and what the pellet hit (or
    // 'distance' metres) takes a hit
    public static void Shoot(Vector3 origin, Vector3 direction, float distance)
    {
        if (Instance == null || Manager.Instance == null) return;
        int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, Instance.glassMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (Instance.paneOfCollider.TryGetValue(hits[i].collider, out int pane)) Manager.Instance.HitGlass(pane);
        }
    }

    public int PaneCount => panes.Count;

    // A pane's collider (the bots' map leaves it out once the pane is broken)
    public Collider ColliderOf(int index) => index >= 0 && index < panes.Count ? panes[index].collider : null;

    // A pane's state from the Manager. With effects, a crack or shatter is seen and heard;
    // without (a player joining a match in progress), the pane just shows as it is.
    public void SetState(int index, int state, bool effects)
    {
        if (index < 0 || index >= panes.Count) return;
        Pane pane = panes[index];
        state = Mathf.Clamp(state, Intact, Broken);
        if (state == pane.state) return;
        pane.state = state;
        pane.chunk.dirty = true;
        pane.collider.enabled = state != Broken;

        if (!effects) return;
        if (state == Broken)
        {
            Shatter(pane.data);
            AudioSource.PlayClipAtPoint(ShatterClip(), pane.data.center, 0.9f);
        }
        else if (state == Cracked)
        {
            AudioSource.PlayClipAtPoint(CrackClip(), pane.data.center, 0.6f);
        }
    }

    private void Shatter(OfficeMapData.Pane pane)
    {
        Vector3 size = pane.size;
        bool alongX = size.x >= size.z;
        float width = alongX ? size.x : size.z;
        Vector3 across = alongX ? Vector3.right : Vector3.forward;
        Vector3 normal = alongX ? Vector3.forward : Vector3.right;
        int count = Mathf.Clamp(Mathf.RoundToInt(width * size.y * 6f), 8, 30);
        for (int i = 0; i < count; i++)
        {
            if (shards.Count >= MaxShards) shards.RemoveAt(0);
            Vector3 position = pane.center
                + across * Random.Range(-0.5f, 0.5f) * width
                + Vector3.up * Random.Range(-0.5f, 0.5f) * size.y;
            float side = Random.value < 0.5f ? -1f : 1f;
            float s = Random.Range(0.07f, 0.28f);
            shards.Add(new Shard
            {
                position = position,
                velocity = normal * side * Random.Range(0.3f, 2.2f) + across * Random.Range(-0.6f, 0.6f) + Vector3.up * Random.Range(-0.2f, 1.2f),
                size = new Vector3(s, s * Random.Range(0.5f, 1.4f), 0.008f),
                rotation = Random.rotation,
                spin = Random.insideUnitSphere * 720f,
                life = Random.Range(5f, 8f),
            });
        }
    }

    #endregion

    #region Drawing

    private void LateUpdate()
    {
        RebuildDirty();
        UpdateShards(Time.deltaTime);
    }

    private void RebuildDirty()
    {
        foreach (Chunk chunk in chunks)
        {
            if (!chunk.dirty) continue;
            chunk.dirty = false;
            Fill(chunk, chunk.intact, Intact, false);
            Fill(chunk, chunk.cracked, Cracked, false);
            Fill(chunk, chunk.frost, -1, true);
        }
    }

    // The panes in a given state (or, for the frosted bands, every pane still standing)
    private void Fill(Chunk chunk, Mesh mesh, int state, bool frost)
    {
        builder.Clear();
        foreach (Pane pane in chunk.panes)
        {
            OfficeMapData.Pane d = pane.data;
            if (frost)
            {
                if (pane.state == Broken || d.frostHeight <= 0f) continue;
                Vector3 size = d.size;
                if (size.x < size.z) size.x += 0.01f; else size.z += 0.01f;
                size.y = d.frostHeight;
                OfficeMap.AddBox(builder, new Vector3(d.center.x, d.frostY, d.center.z), size, 0f);
            }
            else if (pane.state == state)
            {
                OfficeMap.AddBox(builder, d.center, d.size, 0f);
            }
        }
        builder.Fill(mesh);
    }

    private void UpdateShards(float dt)
    {
        if (shards.Count == 0) return;
        const float floor = 0.01f;
        for (int i = shards.Count - 1; i >= 0; i--)
        {
            Shard s = shards[i];
            s.age += dt;
            if (s.age >= s.life)
            {
                shards.RemoveAt(i);
                continue;
            }
            if (!s.resting)
            {
                s.velocity += Physics.gravity * dt;
                s.position += s.velocity * dt;
                s.rotation = Quaternion.Euler(s.spin * dt) * s.rotation;
                if (s.position.y <= floor + s.size.z)
                {
                    // Lands flat and slides to a stop after a small bounce or two
                    s.position.y = floor + s.size.z;
                    s.velocity = new Vector3(s.velocity.x * 0.4f, -s.velocity.y * 0.25f, s.velocity.z * 0.4f);
                    s.spin *= 0.3f;
                    if (s.velocity.sqrMagnitude < 0.05f)
                    {
                        s.resting = true;
                        Vector3 forward = Vector3.ProjectOnPlane(s.rotation * Vector3.forward, Vector3.up);
                        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                        s.rotation = Quaternion.LookRotation(Vector3.up, forward);
                    }
                }
            }
            shards[i] = s;

            // Shrinks away over its last second
            float scale = Mathf.Clamp01(s.life - s.age);
            Matrix4x4 matrix = Matrix4x4.TRS(s.position, s.rotation, s.size * scale);
            Graphics.DrawMesh(shardMesh, matrix, shardMaterial, gameObject.layer, null, 0, null, ShadowCastingMode.Off, false);
        }
    }

    #endregion

    #region Generated texture and sounds

    // White cracks spreading from a point near the middle, on a faint see-through tint
    private static Texture2D CrackTexture()
    {
        if (crackTexture != null) return crackTexture;
        const int size = 256;
        var random = new System.Random(1234);
        var alpha = new float[size * size];
        for (int i = 0; i < alpha.Length; i++) alpha[i] = 0.14f;

        Vector2 center = new Vector2(size * 0.55f, size * 0.45f);
        int rays = 13;
        var ends = new List<Vector2>[3];
        for (int ring = 0; ring < 3; ring++) ends[ring] = new List<Vector2>();
        for (int r = 0; r < rays; r++)
        {
            float angle = (r + (float)random.NextDouble() * 0.6f) / rays * Mathf.PI * 2f;
            Vector2 p = center;
            float length = size * (0.35f + 0.5f * (float)random.NextDouble());
            int steps = 7;
            for (int s = 1; s <= steps; s++)
            {
                angle += ((float)random.NextDouble() - 0.5f) * 0.35f;
                Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length * s / steps;
                Line(alpha, size, p, next, s < 3 ? 0.95f : 0.8f);
                p = next;
                if (s == 1) ends[0].Add(p);
                if (s == 3) ends[1].Add(p);
                if (s == 5) ends[2].Add(p);
            }
        }
        // Rings joining the rays
        for (int ring = 0; ring < 3; ring++)
        {
            List<Vector2> points = ends[ring];
            for (int i = 0; i < points.Count; i++)
            {
                if (random.NextDouble() < 0.3) continue;
                Line(alpha, size, points[i], points[(i + 1) % points.Count], 0.7f);
            }
        }

        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha[i]) * 255f));
        }
        crackTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Glass cracks" };
        crackTexture.wrapMode = TextureWrapMode.Clamp;
        crackTexture.filterMode = FilterMode.Trilinear;
        crackTexture.SetPixels32(pixels);
        crackTexture.Apply(true, true);
        return crackTexture;
    }

    private static void Line(float[] alpha, int size, Vector2 a, Vector2 b, float strength)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * 2f) + 1;
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
            int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = x + dx, py = y + dy;
                    if (px < 0 || py < 0 || px >= size || py >= size) continue;
                    float s = dx == 0 && dy == 0 ? strength : strength * 0.35f;
                    int k = py * size + px;
                    alpha[k] = Mathf.Max(alpha[k], s);
                }
            }
        }
    }

    // A sharp tick with a short ring
    private static AudioClip CrackClip()
    {
        if (crackClip == null) crackClip = GlassSound("Glass Crack", 0.25f, 0.04f, 3, 11);
        return crackClip;
    }

    // A crash, then pieces tinkling as they land
    private static AudioClip ShatterClip()
    {
        if (shatterClip == null) shatterClip = GlassSound("Glass Shatter", 1.4f, 0.25f, 45, 23);
        return shatterClip;
    }

    private static AudioClip GlassSound(string name, float seconds, float burst, int tinkles, int seed)
    {
        const int rate = 44100;
        int samples = Mathf.CeilToInt(seconds * rate);
        var data = new float[samples];
        var random = new System.Random(seed);

        // A burst of bright noise (differenced to take the low end out)
        float previous = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            float noise = (float)random.NextDouble() * 2f - 1f;
            float bright = noise - previous;
            previous = noise;
            data[i] += bright * 0.5f * Mathf.Exp(-t / burst * 4f);
        }

        // Tinkles: short high pings, most of them early
        for (int k = 0; k < tinkles; k++)
        {
            float u = (float)random.NextDouble();
            float start = u * u * (seconds - 0.15f);
            float frequency = 2400f + 5200f * (float)random.NextDouble();
            float amplitude = (0.15f + 0.35f * (float)random.NextDouble()) * (1f - 0.5f * start / seconds);
            float decay = 30f + 50f * (float)random.NextDouble();
            int first = Mathf.FloorToInt(start * rate);
            for (int i = first; i < samples; i++)
            {
                float t = (i - first) / (float)rate;
                float env = Mathf.Exp(-t * decay);
                if (env < 0.001f) break;
                data[i] += Mathf.Sin(2f * Mathf.PI * frequency * t) * amplitude * env;
            }
        }

        float peak = 0f;
        for (int i = 0; i < samples; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        float scale = peak > 0f ? 0.85f / peak : 1f;
        for (int i = 0; i < samples; i++) data[i] *= scale;

        AudioClip clip = AudioClip.Create(name, samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    #endregion
}
