using System.Collections.Generic;
using UnityEngine;

// The floating pieces built into the player's gun, of the same metal as the gun, each with a glowing strip in the
// player's (or team's) colour. For each form they make up part of the gun (GunSatelliteLayout):
// the sniper rifle's barrel, the machine gun's rotary barrel, the shotgun's flared muzzle and pump,
// the pistol's side panels. They move all the time (breathing, turning, spinning), react to every
// shot and to aiming, trail the gun a touch as it moves, re-form into the next form's parts while
// the gun transforms, dim as the energy runs down, and come loose and drift while the energy cell
// is swapped, snapping back as the fresh cell clicks in.
// Everyone sees everyone's, so a player's form can be told from a distance. Added by Player at
// runtime; purely cosmetic, driven by state every machine already has, so no network traffic.
// The main menu's player model gets them too (AttachToDisplay), with a pretend shot now and then
// to show them off.
[DefaultExecutionOrder(100)] // After DroneHands, which tips the gun over for the cell swap
public class GunSatellites : MonoBehaviour
{
    private const int Count = GunSatelliteLayout.Count;
    // The pieces are made of the gun's own material (its Body's), in its own colour
    private Color plateColor = new Color(0.075f, 0.08f, 0.09f);
    private Material metal;
    // While the player has spawn protection the pieces are see-through like the rest of them
    // (Player skips them and leaves it to this): Resources/ProtectionGhost, at Player.protectionAlpha
    private static Material ghostMaterial;
    private float alpha = 1f;
    // The glowing strip along each piece's outer face, as a share of the piece's size
    private static readonly Vector3 StripScale = new Vector3(0.4f, 0.16f, 0.86f);
    private const float Glow = 2.6f;
    // The share of a cell swap at which the fresh cell clicks in (DroneHands' Inserted)
    private const float CellIn = 0.58f;
    // Seconds the pieces take to re-form into a new form's parts, and the stagger between pieces
    private const float ReformTime = 0.45f, ReformStagger = 0.025f;
    // How tightly the pieces follow the gun's own sway and kick (higher: less trailing). They trail
    // in the space of what holds the gun, which moves and turns with the view, so running and
    // looking around never leave them behind; and never by more than MaxTrail of the body's width.
    private const float TrailRate = 40f;
    private const float MaxTrail = 0.3f;
    // The machine gun's rotary barrel: turns this fast at rest (radians a second), faster with
    // every shot up to MaxSpin, and slows back down after
    private const float RestSpin = 1f, SpinPerShot = 6f, MaxSpin = 36f, SpinDown = 1.2f;

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
    // Where each piece is, in the space of what holds the gun (trailing it slightly)
    private Vector3[] shownPositions, shownSizes, heldPositions;
    private Transform heldSpace;
    private Quaternion[] shownRotations;

    private float spin, spinSpeed = RestSpin;
    private float seenShotTime;
    private float aim;
    private float phase;
    private Color color = Color.white;
    private float shownGlow = -1f;
    private MaterialPropertyBlock block;

    // On a display model (the main menu) there's no Player or Weapon: the gun and its form are
    // given, and it fires pretend shots now and then
    private Transform owner;
    private GameObject displayGun;
    private Gun displayForm;
    private float displayShotTime = -1f, nextDemoAt;
    private int demoShotsLeft;

    // What drives the pieces: the player's gun in a match, or the display gun
    private GameObject CurrentGunModel => weapon != null ? weapon.GunModel : displayGun;
    private Gun CurrentForm => weapon != null ? weapon.ShownGun : displayForm;
    private bool Dead => player != null && player.IsDead;
    private float LastShotTime => weapon != null ? weapon.LastShotTime : displayShotTime;
    private bool Aiming => weapon != null && weapon.isAiming;
    private float ReloadProgress => weapon != null ? weapon.ReloadProgress : -1f;
    private float EnergyFraction => weapon != null ? weapon.EnergyFraction : 1f;

    // For a model that isn't a player in a match: owner is the model, gun the gun model it holds
    public static GunSatellites AttachToDisplay(Transform owner, GameObject gun, Gun form, Color color)
    {
        GameObject holder = new GameObject("Gun Satellites");
        holder.transform.SetParent(owner, false);
        GunSatellites satellites = holder.AddComponent<GunSatellites>();
        satellites.owner = owner;
        satellites.displayGun = gun;
        satellites.displayForm = form;
        satellites.phase = Random.value * 10f;
        satellites.nextDemoAt = Time.time + 2f;
        satellites.Build();
        satellites.SetColor(color);
        return satellites;
    }

    // The display model's pretend shots: a burst from the machine gun, a double tap from the
    // pistol, a single shot otherwise, every few seconds
    private void UpdateDemo(float now)
    {
        if (displayForm == null || now < nextDemoAt) return;
        if (demoShotsLeft == 0)
        {
            GunSatelliteLayout.Form f = GunSatelliteLayout.FormOf(displayForm);
            demoShotsLeft = f == GunSatelliteLayout.Form.Orbit ? 12 : f == GunSatelliteLayout.Form.Tight ? 2 : 1;
        }
        displayShotTime = now;
        demoShotsLeft--;
        nextDemoAt = demoShotsLeft > 0 ? now + Mathf.Max(displayForm.fireRate, 0.07f) : now + Random.Range(3.5f, 5.5f);
    }

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
        heldPositions = new Vector3[Count];
        for (int i = 0; i < Count; i++)
        {
            // A unit block scaled to the piece's size, with a glowing strip along its outer face
            Transform piece = new GameObject("Piece " + i).transform;
            piece.SetParent(transform, false);
            // The gun's material is set when fitted to a gun (FitTo)
            plates[i] = Part(piece, "Plate", DroneBody.ShellMaterial, Vector3.zero, Vector3.one);
            strips[i] = Part(piece, "Strip", DroneBody.GlowMaterial, new Vector3(0f, 0.5f + StripScale.y * 0.5f, 0f), StripScale);
            pieces[i] = piece;
            shownRotations[i] = fromRotations[i] = Quaternion.identity;
        }
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
            Set(plates[i], plateColor, Color.black);
            Set(strips[i], strip, color * (Glow * glow));
        }
    }

    // The gun's metal for the pieces and the glow for their strips, or the see-through material
    // for both while protected
    private void ApplyMaterials()
    {
        bool ghost = player != null && player.IsProtected;
        if (ghost && ghostMaterial == null) ghostMaterial = Resources.Load<Material>("ProtectionGhost");
        if (ghostMaterial == null) ghost = false;
        Material plate = ghost ? ghostMaterial : metal;
        Material strip = ghost ? ghostMaterial : DroneBody.GlowMaterial;
        if (plate == null || (plates[0].sharedMaterial == plate && strips[0].sharedMaterial == strip)) return;

        alpha = ghost ? player.protectionAlpha : 1f;
        for (int i = 0; i < Count; i++)
        {
            plates[i].sharedMaterial = plate;
            strips[i].sharedMaterial = strip;
        }
        shownGlow = -1f; // repaint, with the new see-through-ness
    }

    private void Set(Renderer r, Color c, Color emission)
    {
        r.GetPropertyBlock(block);
        c.a = alpha;
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
        form = GunSatelliteLayout.FormOf(CurrentForm);

        // Made of the same material as the gun: its Body's, else its first part's. Read from the
        // gun's prefab, as the model itself may be drawn see-through (spawn protection) right now.
        Gun shownGunForm = CurrentForm;
        Transform source = shownGunForm != null && shownGunForm.prefab != null ? shownGunForm.prefab.transform.Find("Anchor/Design") : null;
        if (source == null) source = design;
        Transform bodyPart = source.Find("Body");
        Renderer bodyRenderer = bodyPart != null ? bodyPart.GetComponent<Renderer>() : null;
        if (bodyRenderer == null) bodyRenderer = source.GetComponentInChildren<MeshRenderer>(true);
        Material gunMetal = bodyRenderer != null ? bodyRenderer.sharedMaterial : null;
        if (gunMetal != null)
        {
            metal = gunMetal;
            if (metal.HasProperty("_BaseColor")) plateColor = metal.GetColor("_BaseColor");
            else if (metal.HasProperty("_Color")) plateColor = metal.GetColor("_Color");
            shownGlow = -1f; // repaint in the gun's colour
        }

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
        if ((player == null || weapon == null) && displayGun == null) return;
        if (weapon == null) UpdateDemo(Time.time);
        GameObject gun = CurrentGunModel;
        if (gun != shownGun) FitTo(gun);

        // Hidden without a gun or once dead; placed afresh when they come back
        bool show = design != null && !Dead;
        for (int i = 0; i < Count; i++) if (pieces[i].gameObject.activeSelf != show) pieces[i].gameObject.SetActive(show);
        if (!show)
        {
            placed = false;
            return;
        }

        float dt = Time.deltaTime;
        float now = Time.time;

        // Each shot spins the machine gun's barrel up; it slows back down after
        float lastShot = LastShotTime;
        if (lastShot > seenShotTime)
        {
            seenShotTime = lastShot;
            spinSpeed = Mathf.Min(spinSpeed + SpinPerShot, MaxSpin);
        }
        spinSpeed = Mathf.Lerp(spinSpeed, RestSpin, 1f - Mathf.Exp(-dt * SpinDown));
        spin = Mathf.Repeat(spin + spinSpeed * dt, Mathf.PI * 2f);
        aim = Mathf.MoveTowards(aim, Aiming ? 1f : 0f, dt * 5f);

        GunSatelliteLayout.Motion motion = new GunSatelliteLayout.Motion
        {
            time = now + phase,
            spin = spin,
            shotAge = lastShot >= 0f ? now - lastShot : 100f,
            aim = aim,
        };

        // The cell swap: loose from when the spent cell comes out until the fresh one clicks in
        float reload = ReloadProgress;
        float loose = reload >= 0f ? Ease(0f, 0.15f, reload) * (1f - Ease(CellIn - 0.02f, CellIn + 0.03f, reload)) : 0f;
        float drift = shape.body.size.x * 0.5f;

        // Dimmer as the energy runs down, and almost dark while the cell is out
        ApplyMaterials();
        Paint(Mathf.Lerp(0.3f + 0.7f * EnergyFraction, 0.15f, loose));

        float trail = 1f - Mathf.Exp(-dt * TrailRate);
        // Sizes are in the gun model's units; the pieces hang off this holder, which may be scaled differently
        float holderScale = transform.lossyScale.x;
        float scale = holderScale > 0f ? design.lossyScale.x / holderScale : 1f;
        // What holds the gun (it moves and turns with the view), and the furthest a piece may trail, in its units
        Transform space = shownGun != null && shownGun.transform.parent != null ? shownGun.transform.parent : design;
        float spaceScale = space.lossyScale.x;
        float maxLag = shape.body.size.x * MaxTrail * (spaceScale > 0f ? design.lossyScale.x / spaceScale : 1f);
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

            // Follow the gun's sway a touch behind, so the pieces feel like they float, but only
            // relative to what holds the gun, and never far
            Vector3 held = space.InverseTransformPoint(design.TransformPoint(position));
            if (!placed || space != heldSpace) heldPositions[i] = held;
            else
            {
                Vector3 lag = Vector3.Lerp(heldPositions[i], held, trail) - held;
                heldPositions[i] = held + Vector3.ClampMagnitude(lag, maxLag);
            }

            Transform piece = pieces[i];
            piece.position = space.TransformPoint(heldPositions[i]);
            piece.rotation = design.rotation * rotation;
            piece.localScale = size * scale;
        }
        placed = true;
        heldSpace = space;
    }

    private void OnDestroy()
    {
        if (replaced != null) replaced.enabled = true;
    }
}
