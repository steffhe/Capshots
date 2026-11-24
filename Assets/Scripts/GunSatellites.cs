using System.Collections.Generic;
using UnityEngine;

// The floating cast-stone pieces built into the player's gun, each with a glowing strip in the
// player's (or team's) colour. For each form they make up part of the gun (GunSatelliteLayout):
// the sniper rifle's barrel, the machine gun's rotary barrel, the shotgun's flared muzzle and pump,
// the pistol's side panels. They move all the time (breathing, turning, spinning), react to every
// shot and to aiming, trail the gun a touch as it moves, re-form into the next form's parts while
// the gun transforms, dim as the energy runs down, and come loose and drift while the energy cell
// is swapped, snapping back as the fresh cell clicks in.
// Everyone sees everyone's, so a player's form can be told from a distance. Added by Player at
// runtime; purely cosmetic, driven by state every machine already has, so no network traffic.
[DefaultExecutionOrder(100)] // After DroneHands, which tips the gun over for the cell swap
public class GunSatellites : MonoBehaviour
{
    private const int Count = GunSatelliteLayout.Count;
    // Rough, non-metallic cast stone, with a fine grain and pores (StoneMaterial)
    private static readonly Color StoneColor = new Color(0.6f, 0.59f, 0.56f);
    private const float StoneSmoothness = 0.12f;
    private static Material stoneMaterial;
    // The glowing strip along each piece's outer face, as a share of the piece's size
    private static readonly Vector3 StripScale = new Vector3(0.4f, 0.16f, 0.86f);
    private const float Glow = 2.6f;
    // The share of a cell swap at which the fresh cell clicks in (DroneHands' Inserted)
    private const float CellIn = 0.58f;
    // Seconds the pieces take to re-form into a new form's parts, and the stagger between pieces
    private const float ReformTime = 0.45f, ReformStagger = 0.025f;
    // How tightly the pieces follow the gun as it moves (higher: less trailing)
    private const float TrailRate = 28f;
    // The machine gun's rotary barrel: turns this fast at rest (radians a second), faster with
    // every shot up to MaxSpin, and slows back down after
    private const float RestSpin = 1f, SpinPerShot = 4f, MaxSpin = 28f, SpinDown = 1.2f;

    private Player player;
    private Weapon weapon;
    private Transform[] pieces;
    private Renderer[] plates, strips;

    private GameObject shownGun;
    private Transform design;
    private GunSatelliteLayout.Shape shape;
    private GunSatelliteLayout.Form form;
    private bool placed;
    // The model part the pieces stand in for (the sniper's barrel), hidden while they do
    private Renderer replaced;

    // Re-forming: where each piece started, in the current gun model's space, and when
    private Vector3[] fromPositions, fromSizes;
    private Quaternion[] fromRotations;
    private float reformStart = -10f;
    // What each piece showed last frame, in the gun model's space, and where it is in the world
    private Vector3[] shownPositions, shownSizes, worldPositions;
    private Quaternion[] shownRotations;

    private float spin, spinSpeed = RestSpin;
    private float seenShotTime;
    private float aim;
    private float phase;
    private Color color = Color.white;
    private float shownGlow = -1f;
    private MaterialPropertyBlock block;

    public static GunSatellites Attach(Player owner, Color color)
    {
        GameObject holder = new GameObject("Gun Satellites");
        holder.transform.SetParent(owner.transform, false);
        GunSatellites satellites = holder.AddComponent<GunSatellites>();
        satellites.player = owner;
        satellites.weapon = owner.GetComponent<Weapon>();
        satellites.phase = Random.value * 10f;
        satellites.Build();
        satellites.SetColor(color);
        return satellites;
    }

    public void SetColor(Color accent)
    {
        color = accent;
        shownGlow = -1f; // repaint on the next frame
        Paint(1f);
    }

    private void Build()
    {
        block = new MaterialPropertyBlock();
        pieces = new Transform[Count];
        plates = new Renderer[Count];
        strips = new Renderer[Count];
        fromPositions = new Vector3[Count];
        fromSizes = new Vector3[Count];
        fromRotations = new Quaternion[Count];
        shownPositions = new Vector3[Count];
        shownSizes = new Vector3[Count];
        shownRotations = new Quaternion[Count];
        worldPositions = new Vector3[Count];
        for (int i = 0; i < Count; i++)
        {
            // A unit block scaled to the piece's size, with a glowing strip along its outer face
            Transform piece = new GameObject("Piece " + i).transform;
            piece.SetParent(transform, false);
            plates[i] = Part(piece, "Plate", StoneMaterial, Vector3.zero, Vector3.one);
            strips[i] = Part(piece, "Strip", DroneBody.GlowMaterial, new Vector3(0f, 0.5f + StripScale.y * 0.5f, 0f), StripScale);
            pieces[i] = piece;
            shownRotations[i] = fromRotations[i] = Quaternion.identity;
        }
    }

    // Matte cast stone: URP Lit, not metallic, rough, with a grain texture made here so no texture
    // asset is needed. Tinted by StoneColor through each plate's property block.
    private static Material StoneMaterial
    {
        get
        {
            if (stoneMaterial != null) return stoneMaterial;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            stoneMaterial = lit != null ? new Material(lit) : new Material(DroneBody.ShellMaterial);
            stoneMaterial.name = "Gun Satellite Stone";
            if (stoneMaterial.HasProperty("_Metallic")) stoneMaterial.SetFloat("_Metallic", 0f);
            if (stoneMaterial.HasProperty("_Smoothness")) stoneMaterial.SetFloat("_Smoothness", StoneSmoothness);
            Texture2D grain = StoneGrain(128, 7);
            if (stoneMaterial.HasProperty("_BaseMap")) stoneMaterial.SetTexture("_BaseMap", grain);
            if (stoneMaterial.HasProperty("_MainTex")) stoneMaterial.SetTexture("_MainTex", grain);
            return stoneMaterial;
        }
    }

    // A tiling greyscale grain: soft blotches, fine speckle, and a scattering of small dark pores
    private static Texture2D StoneGrain(int size, int seed)
    {
        System.Random random = new System.Random(seed);
        float[] coarse = Noise(random, 8), fine = Noise(random, 32);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float value = 0.82f + 0.12f * (Sample(coarse, 8, u, v) - 0.5f) * 2f + 0.08f * (Sample(fine, 32, u, v) - 0.5f) * 2f;
                value += ((float)random.NextDouble() - 0.5f) * 0.06f;
                pixels[y * size + x] = new Color(value, value, value, 1f);
            }
        }
        // Pores: small dark pits, a couple of pixels across
        for (int p = 0; p < size * size / 180; p++)
        {
            int cx = random.Next(size), cy = random.Next(size);
            float depth = 0.25f + (float)random.NextDouble() * 0.25f;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = (cx + dx + size) % size, py = (cy + dy + size) % size;
                    float k = dx == 0 && dy == 0 ? 1f : 0.4f;
                    pixels[py * size + px] *= 1f - depth * k;
                }
            }
        }
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
        texture.name = "Gun Satellite Stone Grain";
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply(true, true);
        return texture;
    }

    // A grid of random values, cells x cells, that wraps round
    private static float[] Noise(System.Random random, int cells)
    {
        float[] grid = new float[cells * cells];
        for (int i = 0; i < grid.Length; i++) grid[i] = (float)random.NextDouble();
        return grid;
    }

    // Smoothly interpolated value from a wrapping noise grid, at u, v in 0 to 1
    private static float Sample(float[] grid, int cells, float u, float v)
    {
        float x = u * cells, y = v * cells;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float tx = Mathf.SmoothStep(0f, 1f, x - x0), ty = Mathf.SmoothStep(0f, 1f, y - y0);
        int x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
        x0 %= cells; y0 %= cells;
        float a = Mathf.Lerp(grid[y0 * cells + x0], grid[y0 * cells + x1], tx);
        float b = Mathf.Lerp(grid[y1 * cells + x0], grid[y1 * cells + x1], tx);
        return Mathf.Lerp(a, b, ty);
    }


    private static Renderer Part(Transform parent, string name, Material material, Vector3 position, Vector3 scale)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        // No collider: right away, so it never trips the player's movement or blocks shots
        DestroyImmediate(part.GetComponent<Collider>());
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Renderer renderer = part.GetComponent<Renderer>();
        if (material != null) renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return renderer;
    }

    // glow: 0 dark to 1 full, from the energy left
    private void Paint(float glow)
    {
        if (block == null || plates == null) return;
        if (Mathf.Abs(glow - shownGlow) < 0.01f) return;
        shownGlow = glow;
        Color strip = Color.Lerp(color * 0.35f, color, glow);
        for (int i = 0; i < Count; i++)
        {
            Set(plates[i], StoneColor, Color.black);
            Set(strips[i], strip, color * (Glow * glow));
        }
    }

    private void Set(Renderer r, Color c, Color emission)
    {
        r.GetPropertyBlock(block);
        // Keep any see-through-ness set on it (Player's spawn protection)
        if (block.HasColor("_BaseColor")) c.a = block.GetColor("_BaseColor").a;
        block.SetColor("_BaseColor", c);
        block.SetColor("_Color", c);
        block.SetColor("_EmissionColor", emission);
        r.SetPropertyBlock(block);
    }


    #region Fitting to the gun

    // Measures a newly shown gun model's parts, and carries the pieces over into its space where
    // they are, so they re-form from wherever they were into the new form's parts
    private void FitTo(GameObject gun)
    {
        if (replaced != null) replaced.enabled = true;
        replaced = null;
        shownGun = gun;

        Transform newDesign = gun != null ? gun.transform.Find("Anchor/Design") : null;
        if (newDesign == null && gun != null) newDesign = gun.transform;
        if (newDesign == null) { design = null; return; }

        // Measured on the finished shape, even while the gun is still transforming into it
        GunMorph morph = gun.GetComponent<GunMorph>();
        if (morph != null) morph.ShowFinalShape(true);

        GunSatelliteLayout.Shape newShape = new GunSatelliteLayout.Shape();
        bool hasBody = PartBounds(newDesign, "Body", out newShape.body);
        newShape.hasBarrel = PartBounds(newDesign, "Barrel", out newShape.barrel);
        newShape.hasUnder = PartBounds(newDesign, "Under", out newShape.under);
        if (!hasBody)
        {
            // No part called Body: the whole gun stands in for it
            bool any = false;
            foreach (MeshFilter filter in newDesign.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds b = LocalBounds(newDesign, filter);
                if (any) newShape.body.Encapsulate(b); else { newShape.body = b; any = true; }
            }
            hasBody = any;
        }
        if (morph != null) morph.ShowFinalShape(false);
        if (!hasBody) { design = null; return; }

        // Carry the pieces over, as they are shown now, into the new model's space
        if (placed && design != null)
        {
            for (int i = 0; i < Count; i++)
            {
                fromPositions[i] = newDesign.InverseTransformPoint(design.TransformPoint(shownPositions[i]));
                fromRotations[i] = Quaternion.Inverse(newDesign.rotation) * design.rotation * shownRotations[i];
                fromSizes[i] = shownSizes[i];
            }
            reformStart = Time.time;
        }

        design = newDesign;
        shape = newShape;
        Gun shownForm = weapon.ShownGun;
        form = GunSatelliteLayout.FormOf(shownForm);

        // The part the pieces stand in for is hidden (its copy retracting into the next form is too)
        string part = GunSatelliteLayout.ReplacedPart(form);
        Transform hidden = part != null ? design.Find(part) : null;
        replaced = hidden != null ? hidden.GetComponent<Renderer>() : null;
        if (replaced != null) replaced.enabled = false;

        // The gun's layer: other players' guns are drawn by the main camera, your own by the viewmodel camera
        int layer = design.gameObject.layer;
        foreach (Transform t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    private static bool PartBounds(Transform design, string name, out Bounds bounds)
    {
        bounds = new Bounds();
        Transform part = design.Find(name);
        MeshFilter filter = part != null ? part.GetComponent<MeshFilter>() : null;
        if (filter == null || filter.sharedMesh == null) return false;
        bounds = LocalBounds(design, filter);
        return true;
    }

    private static Bounds LocalBounds(Transform space, MeshFilter filter)
    {
        Bounds mesh = filter.sharedMesh.bounds;
        Bounds result = new Bounds(space.InverseTransformPoint(filter.transform.TransformPoint(mesh.center)), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            result.Encapsulate(space.InverseTransformPoint(filter.transform.TransformPoint(corner)));
        }
        return result;
    }

    #endregion

    private static float Ease(float from, float to, float t) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, t));

    private void LateUpdate()
    {
        if (player == null || weapon == null) return;
        GameObject gun = weapon.GunModel;
        if (gun != shownGun) FitTo(gun);

        // Hidden without a gun or once dead; placed afresh when they come back
        bool show = design != null && !player.IsDead;
        for (int i = 0; i < Count; i++) if (pieces[i].gameObject.activeSelf != show) pieces[i].gameObject.SetActive(show);
        if (!show)
        {
            placed = false;
            return;
        }

        float dt = Time.deltaTime;
        float now = Time.time;

        // Each shot spins the machine gun's barrel up; it slows back down after
        if (weapon.LastShotTime > seenShotTime)
        {
            seenShotTime = weapon.LastShotTime;
            spinSpeed = Mathf.Min(spinSpeed + SpinPerShot, MaxSpin);
        }
        spinSpeed = Mathf.Lerp(spinSpeed, RestSpin, 1f - Mathf.Exp(-dt * SpinDown));
        spin = Mathf.Repeat(spin + spinSpeed * dt, Mathf.PI * 2f);
        aim = Mathf.MoveTowards(aim, weapon.isAiming ? 1f : 0f, dt * 5f);

        GunSatelliteLayout.Motion motion = new GunSatelliteLayout.Motion
        {
            time = now + phase,
            spin = spin,
            shotAge = weapon.LastShotTime >= 0f ? now - weapon.LastShotTime : 100f,
            aim = aim,
        };

        // The cell swap: loose from when the spent cell comes out until the fresh one clicks in
        float reload = weapon.ReloadProgress;
        float loose = reload >= 0f ? Ease(0f, 0.15f, reload) * (1f - Ease(CellIn - 0.02f, CellIn + 0.03f, reload)) : 0f;
        float drift = shape.body.size.x * 0.5f;

        // Dimmer as the energy runs down, and almost dark while the cell is out
        Paint(Mathf.Lerp(0.3f + 0.7f * weapon.EnergyFraction, 0.15f, loose));

        float trail = 1f - Mathf.Exp(-dt * TrailRate);
        // Sizes are in the gun model's units; the pieces hang off this holder, which may be scaled differently
        float holderScale = transform.lossyScale.x;
        float scale = holderScale > 0f ? design.lossyScale.x / holderScale : 1f;
        for (int i = 0; i < Count; i++)
        {
            GunSatelliteLayout.Pose pose = GunSatelliteLayout.Arrange(form, i, shape, motion);
            Vector3 position = pose.position;
            Quaternion rotation = pose.rotation;
            Vector3 size = pose.size;

            if (loose > 0f)
            {
                // Drift away from the gun and tumble
                float t = now + phase;
                Vector3 away = new Vector3(Mathf.Sin(t * 1.7f + i * 2f), Mathf.Sin(t * 1.3f + i * 3.1f) - 0.6f, Mathf.Sin(t * 1.1f + i));
                position += away * (drift * loose);
                rotation *= Quaternion.Euler(Mathf.Sin(t * 2.3f + i) * 50f * loose, Mathf.Sin(t * 1.9f + i * 2f) * 50f * loose, 0f);
            }

            // Re-forming after a change of form: each piece slides from where it was, a little after the last
            if (placed)
            {
                float k = Ease(0f, 1f, (now - reformStart - i * ReformStagger) / ReformTime);
                if (k < 1f)
                {
                    position = Vector3.Lerp(fromPositions[i], position, k);
                    rotation = Quaternion.Slerp(fromRotations[i], rotation, k);
                    size = Vector3.Lerp(fromSizes[i], size, k);
                }
            }
            shownPositions[i] = position;
            shownRotations[i] = rotation;
            shownSizes[i] = size;

            // Follow the gun a touch behind as it moves, so the pieces feel like they float
            Vector3 world = design.TransformPoint(position);
            worldPositions[i] = placed ? Vector3.Lerp(worldPositions[i], world, trail) : world;

            Transform piece = pieces[i];
            piece.position = worldPositions[i];
            piece.rotation = design.rotation * rotation;
            piece.localScale = size * scale;
        }
        placed = true;
    }

    private void OnDestroy()
    {
        if (replaced != null) replaced.enabled = true;
    }
}
