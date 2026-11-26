using UnityEngine;

// The gun's machined body, built from GunDesign's parts onto a gun model when it's made (by Weapon,
// and by the menu, menu battle and awards screens). The prefab's old blocks (Body, Grip, Under,
// Barrel) stay as invisible measuring blocks: GunSatellites fits its floating pieces round them and
// DroneHands its hand-orbs, so both work as before.
//
// Driven every frame by the gun's GunSatellites (Drive), which already follows everything about it:
// the player's colour, shots, aiming, the energy cell swap, the energy left and spawn protection.
// It floats the floating parts (the rings round the core, the side modules, the muzzle ring, the
// stock), runs each form's energy behaviour, takes the core out while the cell is swapped (the core
// is the cell), and adds the glow flare and dip of a change of form (GunMorph moves the parts).
// Purely cosmetic and worked out from state every machine already has: no network traffic.
public class GunModel : MonoBehaviour
{
    public const string GroupName = "Model";
    // An empty marker under the core, where the cell swap's fresh cell goes in (DroneHands)
    public const string CoreSlotName = "Core Slot";
    // The prefab blocks the rest of the game measures; their renderers are removed
    private static readonly string[] MeasuringBlocks = { "Body", "Grip", "Under", "Barrel" };

    // How brightly each kind of glowing part glows, times the player's colour
    private const float CoreGlow = 2.8f, RingGlow = 2.2f, LineGlow = 1.4f, ModuleGlow = 1.6f, MuzzleGlow = 2f, VentGlow = 2.4f, TrimGlow = 1.2f;

    public struct State
    {
        public float lastShotTime; // Time.time of the last shot, or below 0 if none
        public float aim;          // 0 hip to 1 aiming down the sights
        public float reload;       // progress of the cell swap, 0 to 1, or below 0 when not swapping
        public float loose;        // how loose the floating parts are during the cell swap, 0 to 1
        public float energy;       // energy left, 0 to 1
        public Color color;        // the player's (or team's) colour
        public bool ghost;         // spawn protection: drawn see-through
        public float ghostAlpha;
        public bool detail;        // the small parts too (your own gun, and display guns)
    }

    private class Item
    {
        public GunDesign.Part part;
        public Transform transform;
        public Renderer renderer;
        public Vector3 position, scale;
        public Quaternion rotation;
        public float phase;
    }

    private Item[] items;
    private GunSatelliteLayout.Form form;
    private MaterialPropertyBlock block;
    private static Material ghostMaterial;
    private bool ghosted, detailShown = true;
    private float heat, seenShot = -1f, ringSpin, phase;
    // The lock sound at the end of a change of form, through the player's sounds (none on display guns)
    private PlayerSounds sounds;
    private bool lockPlayed;
    private const float LockAt = 0.8f;

    public GunSatelliteLayout.Form Form => form;

    // Builds the parts onto a newly made gun model; does nothing if it already has them
    public static GunModel Build(GameObject gun, Gun gunForm)
    {
        if (gun == null) return null;
        GunModel model = gun.GetComponent<GunModel>();
        if (model != null) return model;
        Transform design = gun.transform.Find("Anchor/Design");
        if (design == null) return null;
        model = gun.AddComponent<GunModel>();
        model.form = GunSatelliteLayout.FormOf(gunForm);
        model.Create(design);
        return model;
    }

    // Where a part comes loose to in the "release" stage of a change of form (GunMorph)
    public Vector3 ReleaseOf(string name)
    {
        if (items == null) return Vector3.zero;
        foreach (Item item in items) if (item.part.name == name) return GunDesign.ReleaseOffset(item.part);
        return Vector3.zero;
    }

    public bool IsCollapsed(string name)
    {
        if (items == null) return true;
        foreach (Item item in items) if (item.part.name == name) return item.part.Collapsed;
        return true;
    }

    private void Create(Transform design)
    {
        block = new MaterialPropertyBlock();
        phase = Random.value * 10f;
        sounds = GetComponentInParent<PlayerSounds>();

        // The old blocks stop being drawn, but keep their shapes for measuring. Removed right away,
        // so a change of form started this frame (GunMorph) doesn't pick them up as parts.
        foreach (string name in MeasuringBlocks)
        {
            Transform measured = design.Find(name);
            Renderer old = measured != null ? measured.GetComponent<Renderer>() : null;
            if (old != null) DestroyImmediate(old);
        }

        Transform group = new GameObject(GroupName).transform;
        group.SetParent(design, false);
        int layer = design.gameObject.layer;
        group.gameObject.layer = layer;

        GunDesign.Part[] parts = GunDesign.For(form);
        items = new Item[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            GunDesign.Part part = parts[i];
            GameObject go = new GameObject(part.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = layer;
            Transform t = go.transform;
            t.SetParent(group, false);
            go.GetComponent<MeshFilter>().sharedMesh = GunParts.MeshOf(part.shape);
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GunParts.MaterialOf(part.finish);
            bool glows = part.finish == GunDesign.Finish.Glow;
            renderer.shadowCastingMode = glows || part.detail ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = !glows;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            Item item = new Item
            {
                part = part,
                transform = t,
                renderer = renderer,
                position = part.position,
                rotation = Quaternion.Euler(part.rotation),
                scale = part.size,
                phase = i * 1.37f,
            };
            t.localPosition = item.position;
            t.localRotation = item.rotation;
            t.localScale = item.scale;
            items[i] = item;
        }

        // Where the cell swap's fresh cell goes in: the bottom of the core's window
        Transform slot = new GameObject(CoreSlotName).transform;
        slot.SetParent(group, false);
        slot.localPosition = new Vector3(GunDesign.CorePosition.x, GunDesign.CorePosition.y - GunDesign.CoreDiameter * 0.5f - 0.03f, GunDesign.CorePosition.z);

        // Until something drives it (a gun with no GunSatellites), glow white
        Drive(new State { lastShotTime = -1f, reload = -1f, energy = 1f, color = Color.white, ghostAlpha = 1f, detail = true });
    }

    #region Every frame

    public void Drive(State s)
    {
        if (items == null) return;
        float now = Time.time;
        float dt = Time.deltaTime;
        float idle = now + phase;

        // A change of form in progress: GunMorph has already moved the parts this frame, so the
        // floating comes on top of where it put them
        GunMorph morph = GetComponent<GunMorph>();
        float morphT = morph != null ? morph.Progress : 1f;
        bool morphing = morph != null && morphT < 1f;
        // 4. Mechanical lock: the parts seat with a clack
        if (!morphing) lockPlayed = false;
        else if (!lockPlayed && morphT >= LockAt)
        {
            lockPlayed = true;
            if (sounds == null) sounds = GetComponentInParent<PlayerSounds>();
            if (sounds != null) sounds.PlayLock();
        }

        float shotAge = s.lastShotTime >= 0f ? now - s.lastShotTime : 100f;
        if (s.lastShotTime > seenShot)
        {
            // The machine gun heats up as it fires
            if (seenShot >= 0f || shotAge < 0.1f) heat = Mathf.Min(1f, heat + 0.12f);
            seenShot = s.lastShotTime;
        }
        heat = Mathf.MoveTowards(heat, 0f, dt * 0.45f);
        float kick = GunSatelliteLayout.Kick(shotAge);

        // Each form's energy: the core's brightness, the vents' and the channels'
        float core, vents, lines;
        switch (form)
        {
            case GunSatelliteLayout.Form.Orbit:
                // Ramps up with sustained fire, and the vents glow with the heat
                core = 0.85f + 0.6f * heat + 0.3f * kick;
                vents = heat;
                lines = 0.8f + 0.5f * heat;
                break;
            case GunSatelliteLayout.Form.Fan:
                // Spent by a blast, then recharges over the time between shots
                float charge = Mathf.Clamp01(shotAge / 0.8f);
                core = 0.25f + 0.75f * charge * charge + 1.5f * kick;
                vents = (1f - charge) * 0.8f + kick;
                lines = 0.4f + 0.6f * charge;
                break;
            case GunSatelliteLayout.Form.Rail:
                // A flash, then a pulse running down the channels
                core = 1f + 1.2f * kick;
                vents = 0.25f + GunSatelliteLayout.Kick(shotAge - 0.06f);
                lines = 1f + 1.5f * GunSatelliteLayout.Kick(shotAge - 0.03f);
                break;
            default:
                // Steady, with a blip on each shot
                core = 1f + 0.8f * kick;
                vents = 0f;
                lines = 1f;
                break;
        }

        // A change of form: a flare as the energy activates, dim while the parts move, a flash as
        // they lock, then settling at the new form's level
        float flare = MorphGlow(morphing ? morphT : 1f);
        // Dimmer as the energy runs down, and almost dark while the cell is out
        float power = (0.3f + 0.7f * Mathf.Clamp01(s.energy)) * Mathf.Lerp(1f, 0.15f, s.loose) * flare;

        // The rings round the core turn faster as the gun works harder, and spin up while transforming
        ringSpin += dt * (1.2f + 6f * heat + 4f * kick + (morphing ? 9f : 0f)) * Mathf.Rad2Deg;
        ringSpin = Mathf.Repeat(ringSpin, 360f);

        // The core is the energy cell: out from when the hand reaches for it until the fresh one
        // clicks in, with a pop as it does
        bool coreOut = s.reload >= DroneHands.ReachSlot && s.reload < DroneHands.Inserted;
        float corePop = s.reload >= DroneHands.Inserted ? 1f + 0.3f * (1f - Mathf.Clamp01((s.reload - DroneHands.Inserted) / 0.06f)) : 1f;

        UpdateMaterials(s);
        float aimCalm = 1f - 0.5f * s.aim;

        foreach (Item item in items)
        {
            Transform t = item.transform;
            Vector3 position = morphing ? t.localPosition : item.position;
            Quaternion rotation = morphing ? t.localRotation : item.rotation;
            // GunMorph sets every part every frame while it runs, so the floating never builds up
            Vector3 scale = morphing ? t.localScale : item.scale;
            float glow = 0f;
            float wave = Mathf.Sin(idle * 1.7f + item.phase);

            switch (item.part.motion)
            {
                case GunDesign.Motion.Core:
                    scale *= coreOut ? 0f : corePop;
                    glow = CoreGlow * core;
                    break;
                case GunDesign.Motion.CoreRing:
                    float turn = item.part.name.EndsWith("A") ? ringSpin : -ringSpin;
                    rotation *= Quaternion.Euler(0f, 0f, turn);
                    position.z += Mathf.Sin(idle * 2.1f + item.phase) * 0.004f * aimCalm + (coreOut ? (item.part.name.EndsWith("A") ? -0.012f : 0.012f) : 0f);
                    glow = RingGlow * Mathf.Lerp(0.8f, 1.2f, Mathf.Clamp01(core - 0.5f));
                    break;
                case GunDesign.Motion.SideModule:
                {
                    // Floats off the side: breathing, kicked out by a shot, loose during the cell swap
                    float drift = (0.003f * wave + 0.010f * kick) * aimCalm + 0.035f * s.loose;
                    position.x += item.part.side * drift;
                    position.y += Mathf.Sin(idle * 1.3f + item.phase) * 0.002f - 0.02f * s.loose;
                    if (s.loose > 0f) rotation *= Quaternion.Euler(Mathf.Sin(idle * 2.3f + item.phase) * 30f * s.loose, 0f, item.part.side * 20f * s.loose);
                    if (item.part.finish == GunDesign.Finish.Glow) glow = ModuleGlow * (1f + 0.8f * kick);
                    break;
                }
                case GunDesign.Motion.Muzzle:
                {
                    // Hovers at the muzzle and is punched forward by each shot
                    float punch = form == GunSatelliteLayout.Form.Tight ? 0.02f : 0.03f;
                    position.z += Mathf.Sin(idle * 2f + item.phase) * 0.003f * aimCalm + kick * punch;
                    if (item.part.shape == GunDesign.Shape.Ring) scale = Vector3.Scale(scale, new Vector3(1f + 0.25f * kick, 1f + 0.25f * kick, 1f));
                    if (item.part.finish == GunDesign.Finish.Glow) glow = MuzzleGlow * (0.8f + 2.5f * kick);
                    break;
                }
                case GunDesign.Motion.Stock:
                    position.z += -kick * 0.02f + Mathf.Sin(idle * 1.1f + item.phase) * 0.002f * aimCalm;
                    if (item.part.finish == GunDesign.Finish.Glow) glow = TrimGlow * (1f + kick);
                    break;
                case GunDesign.Motion.Vent:
                    glow = VentGlow * vents;
                    break;
                case GunDesign.Motion.Line:
                    glow = LineGlow * lines;
                    break;
                default:
                    if (item.part.finish == GunDesign.Finish.Glow) glow = TrimGlow;
                    break;
            }

            t.localPosition = position;
            t.localRotation = rotation;
            t.localScale = scale;

            if (item.part.finish == GunDesign.Finish.Glow) Paint(item.renderer, s, glow * power);
            else if (ghosted) Paint(item.renderer, GunParts.ColorOf(item.part.finish), Color.black, s.ghostAlpha);
        }

        // The small parts only on your own gun (and display guns): other players' are a few metres off
        if (s.detail != detailShown)
        {
            detailShown = s.detail;
            foreach (Item item in items) if (item.part.detail) item.renderer.enabled = s.detail;
        }
    }

    // The glow's level through a change of form (t 0 to 1)
    private static float MorphGlow(float t)
    {
        if (t >= 1f) return 1f;
        // 1. Energy activation: a flare
        if (t < 0.2f) return 1f + 1.3f * Mathf.Sin(Mathf.PI * t / 0.2f);
        // 2-3. Release and reconfiguration: dimmed while the parts move
        if (t < 0.78f) return Mathf.Lerp(1f, 0.55f, Mathf.SmoothStep(0f, 1f, (t - 0.2f) / 0.1f));
        // 4. Lock: a flash as the parts seat
        if (t < 0.9f) return 0.55f + 1.1f * Mathf.Sin(Mathf.PI * (t - 0.78f) / 0.12f);
        // 5. Stabilisation: settling to the new form's level
        return Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.9f) / 0.1f));
    }

    // The gun's own materials, or the see-through one while spawn protected
    private void UpdateMaterials(State s)
    {
        if (s.ghost == ghosted) return;
        if (s.ghost)
        {
            if (ghostMaterial == null) ghostMaterial = Resources.Load<Material>("ProtectionGhost");
            if (ghostMaterial == null) return;
        }
        ghosted = s.ghost;
        foreach (Item item in items)
        {
            item.renderer.sharedMaterial = ghosted ? ghostMaterial : GunParts.MaterialOf(item.part.finish);
            if (!ghosted && item.part.finish != GunDesign.Finish.Glow) item.renderer.SetPropertyBlock(null);
        }
    }

    private void Paint(Renderer r, State s, float glow)
    {
        Color c = s.color;
        Color tint = Color.Lerp(c * 0.35f, c, Mathf.Clamp01(glow / 2f));
        Paint(r, tint, c * glow, ghosted ? s.ghostAlpha : 1f);
    }

    private void Paint(Renderer r, Color color, Color emission, float alpha)
    {
        r.GetPropertyBlock(block);
        color.a = alpha;
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        block.SetColor("_EmissionColor", emission);
        r.SetPropertyBlock(block);
    }

    #endregion
}
