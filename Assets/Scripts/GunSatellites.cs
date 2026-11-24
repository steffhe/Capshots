using UnityEngine;

// Small cast-stone plates, each with a glowing strip in the player's (or team's) colour, that hover
// around the player's dark metal gun and move into a different arrangement for each form
// (GunSatelliteLayout): tucked along the pistol's sides, circling the machine gun's barrel (faster
// while firing), fanned round the shotgun's muzzle, strung out under the sniper rifle's barrel. They glide to the new arrangement while the gun
// transforms, jump outward with each shot, dim as the energy runs down, and come loose and drift
// while the energy cell is swapped, snapping back as the fresh cell clicks in.
// Everyone sees everyone's, so a player's form can be told from a distance. Added by Player at
// runtime; purely cosmetic, driven by state every machine already has, so no network traffic.
[DefaultExecutionOrder(100)] // After DroneHands, which tips the gun over for the cell swap
public class GunSatellites : MonoBehaviour
{
    private const int Count = 5;
    // Rough, non-metallic cast stone, with a fine grain and pores (StoneMaterial)
    private static readonly Color StoneColor = new Color(0.6f, 0.59f, 0.56f);
    private const float StoneSmoothness = 0.12f;
    private static Material stoneMaterial;
    private static readonly Vector3 PlateSize = new Vector3(0.034f, 0.007f, 0.06f);
    private static readonly Vector3 StripSize = new Vector3(0.012f, 0.004f, 0.046f);
    private const float Glow = 2.6f;
    // The share of a cell swap at which the fresh cell clicks in (DroneHands' Inserted)
    private const float CellIn = 0.58f;
    // How far a shot pushes the pieces out, in metres
    private const float KickDistance = 0.035f;
    // The machine gun's ring: turns this fast at rest (radians a second), up to MaxSpin while firing
    private const float RestSpin = 1.2f, SpinPerShot = 2.5f, MaxSpin = 16f;

    private Player player;
    private Weapon weapon;
    private Transform[] pieces;
    private Renderer[] plates, strips;
    private float[] rates; // how quickly each piece follows its place, slightly different for each

    // Per piece, in the current gun model's space
    private Vector3[] positions;
    private Quaternion[] rotations;
    private float[] scales;

    private GameObject shownGun;
    private Transform design;
    private Bounds gunBounds;
    private Vector3 muzzle;
    private GunSatelliteLayout.Form form;
    private bool placed;

    private float spin, spinSpeed = RestSpin;
    private float kick;
    private float seenShotTime;
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
        rates = new float[Count];
        positions = new Vector3[Count];
        rotations = new Quaternion[Count];
        scales = new float[Count];
        for (int i = 0; i < Count; i++)
        {
            Transform piece = new GameObject("Piece " + i).transform;
            piece.SetParent(transform, false);
            // A stone plate with a glowing strip along its outer face
            plates[i] = Part(piece, "Plate", StoneMaterial, Vector3.zero, PlateSize);
            strips[i] = Part(piece, "Strip", DroneBody.GlowMaterial, new Vector3(0f, (PlateSize.y + StripSize.y) * 0.5f, 0f), StripSize);
            pieces[i] = piece;
            rates[i] = 9f + i * 0.9f;
            rotations[i] = Quaternion.identity;
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

    // Measures a newly shown gun model, and carries the pieces over into its space where they are,
    // so they glide from the old form's arrangement to the new one
    private void FitTo(GameObject gun)
    {
        shownGun = gun;
        Transform newDesign = gun != null ? gun.transform.Find("Anchor/Design") : null;
        if (newDesign == null && gun != null) newDesign = gun.transform;
        if (newDesign == null) { design = null; return; }

        // Measured on the finished shape, even while the gun is still transforming into it
        GunMorph morph = gun.GetComponent<GunMorph>();
        if (morph != null) morph.ShowFinalShape(true);

        bool any = false;
        Bounds bounds = new Bounds();
        foreach (MeshFilter filter in newDesign.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds b = LocalBounds(newDesign, filter);
            if (any) bounds.Encapsulate(b); else { bounds = b; any = true; }
        }
        LineRenderer line = weapon.lineRend;
        Vector3 newMuzzle = line != null && line.transform.IsChildOf(gun.transform)
            ? newDesign.InverseTransformPoint(line.transform.position)
            : new Vector3(bounds.center.x, bounds.center.y + bounds.extents.y * 0.4f, bounds.max.z);

        if (morph != null) morph.ShowFinalShape(false);
        if (!any) { design = null; return; }

        if (placed)
        {
            for (int i = 0; i < Count; i++)
            {
                positions[i] = newDesign.InverseTransformPoint(pieces[i].position);
                rotations[i] = Quaternion.Inverse(newDesign.rotation) * pieces[i].rotation;
            }
        }

        design = newDesign;
        gunBounds = bounds;
        muzzle = newMuzzle;

        // The gun's layer: other players' guns are drawn by the main camera, your own by the viewmodel camera
        int layer = design.gameObject.layer;
        foreach (Transform t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
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

    private static float Ease(float from, float to, float t) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, t));

    private void LateUpdate()
    {
        if (player == null || weapon == null) return;
        GameObject gun = weapon.GunModel;
        if (gun != shownGun) FitTo(gun);
        // The form whose model is showing (it can trail the equipped form by a frame)
        Gun shownForm = weapon.ShownGun;
        if (shownForm != null) form = GunSatelliteLayout.FormOf(shownForm);

        // Hidden without a gun or once dead; placed afresh when they come back
        bool show = design != null && !player.IsDead;
        for (int i = 0; i < Count; i++) if (pieces[i].gameObject.activeSelf != show) pieces[i].gameObject.SetActive(show);
        if (!show)
        {
            placed = false;
            return;
        }

        float dt = Time.deltaTime;
        float time = Time.time;

        // A shot: push the pieces out and spin the machine gun's ring up
        if (weapon.LastShotTime > seenShotTime)
        {
            seenShotTime = weapon.LastShotTime;
            kick = 1f;
            spinSpeed = Mathf.Min(spinSpeed + SpinPerShot, MaxSpin);
        }
        kick *= Mathf.Exp(-dt * 14f);
        spinSpeed = Mathf.Lerp(spinSpeed, RestSpin, 1f - Mathf.Exp(-dt * 1.5f));
        spin = Mathf.Repeat(spin + spinSpeed * dt, Mathf.PI * 2f);

        // The cell swap: loose from when the spent cell comes out until the fresh one clicks in
        float reload = weapon.ReloadProgress;
        float loose = reload >= 0f ? Ease(0f, 0.15f, reload) * (1f - Ease(CellIn - 0.02f, CellIn + 0.03f, reload)) : 0f;

        // Dimmer as the energy runs down, and almost dark while the cell is out
        Paint(Mathf.Lerp(0.3f + 0.7f * weapon.EnergyFraction, 0.15f, loose));

        for (int i = 0; i < Count; i++)
        {
            GunSatelliteLayout.Pose pose = GunSatelliteLayout.Arrange(form, i, Count, gunBounds, muzzle, spin);
            Vector3 target = pose.position;

            // Bob gently, each piece out of step
            float t = time * 2.1f + phase + i * 1.7f;
            target += new Vector3(Mathf.Sin(t * 0.7f), Mathf.Sin(t), 0f) * 0.004f;
            target += GunSatelliteLayout.KickDirection(form, pose.position, muzzle) * (KickDistance * kick);

            Quaternion rotation = pose.rotation;
            if (loose > 0f)
            {
                // Drift away from the gun and tumble a little
                Vector3 drift = new Vector3(Mathf.Sin(time * 1.7f + i * 2f), Mathf.Sin(time * 1.3f + i * 3.1f) - 0.8f, Mathf.Sin(time * 1.1f + i));
                target += drift * (0.05f * loose);
                rotation *= Quaternion.Euler(Mathf.Sin(time * 2.3f + i) * 50f * loose, Mathf.Sin(time * 1.9f + i * 2f) * 50f * loose, 0f);
            }

            if (!placed)
            {
                positions[i] = target;
                rotations[i] = rotation;
                scales[i] = pose.scale;
            }
            else
            {
                // Snap back faster as the fresh cell clicks in than they drifted away
                float rate = rates[i] * (reload >= CellIn ? 2f : 1f);
                float follow = 1f - Mathf.Exp(-dt * rate);
                positions[i] = Vector3.Lerp(positions[i], target, follow);
                rotations[i] = Quaternion.Slerp(rotations[i], rotation, follow);
                scales[i] = Mathf.Lerp(scales[i], pose.scale, follow);
            }

            Transform piece = pieces[i];
            piece.position = design.TransformPoint(positions[i]);
            piece.rotation = design.rotation * rotations[i];
            piece.localScale = Vector3.one * scales[i];
        }
        placed = true;
    }
}
