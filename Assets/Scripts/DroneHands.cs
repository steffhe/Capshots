using UnityEngine;

// Two little floating hand-orbs that hold the player's gun: one on the grip, the other under the barrel
// (or beside the grip on a pistol). The drones have no arms, so the orbs simply hover there, bobbing
// gently and trailing the gun a touch.
// Also animates reloading, in time with the reload sounds (PlayerSounds): the gun tips over, the free
// hand pulls the spent energy cell out of the bottom of the handle and flicks it away, fetches a fresh
// glowing cell and pushes it in (the gun jolts), then taps the top of the gun as it comes back up ready.
// Drawn on the gun's layer, so other players see them with the gun, and you see your own in first person.
// Added by Player at runtime; purely cosmetic, so it needs no network traffic.
public class DroneHands : MonoBehaviour
{
    private const float Size = 0.13f;
    private const float RingGlow = 2.4f;
    private const float CellGlow = 3f;
    private static readonly Vector3 CellSize = new Vector3(0.07f, 0.06f, 0.07f); // a capsule, 0.12 long

    private Player player;
    private Weapon weapon;
    private Transform[] hands;
    private Renderer[] shells, rings;
    private Transform cell;
    private Renderer cellRenderer;
    private Vector3[] localGrips = new Vector3[2]; // in the gun model's space
    private Vector3 slot, top;                     // the cell slot under the handle, and the top of the gun
    private Vector3[] positions = new Vector3[2];  // smoothed, in the player's space
    private GameObject shownGun;
    private Transform gunDesign;
    private bool placed;
    private float phase;
    private Color color = Color.white;
    private MaterialPropertyBlock block;

    // The spent cell, flicked away: where it was let go and how it flies, in the player's space
    private Vector3 tossStart, tossVelocity;

    public static DroneHands Attach(Player owner, Color color)
    {
        GameObject holder = new GameObject("Drone Hands");
        holder.transform.SetParent(owner.transform, false);
        DroneHands hands = holder.AddComponent<DroneHands>();
        hands.player = owner;
        hands.weapon = owner.GetComponent<Weapon>();
        hands.phase = Random.value * 10f;
        hands.Build();
        hands.SetColor(color);
        return hands;
    }

    public void SetColor(Color accent)
    {
        color = accent;
        if (block == null) block = new MaterialPropertyBlock();
        Color shell = new Color(0.93f, 0.94f, 0.96f);
        for (int i = 0; i < 2; i++)
        {
            Paint(shells[i], shell, Color.black);
            Paint(rings[i], accent, accent * RingGlow);
        }
        Paint(cellRenderer, accent, accent * CellGlow);
    }

    private void Paint(Renderer r, Color c, Color emission)
    {
        r.GetPropertyBlock(block);
        block.SetColor("_BaseColor", c);
        block.SetColor("_Color", c);
        block.SetColor("_EmissionColor", emission);
        r.SetPropertyBlock(block);
    }

    private void Build()
    {
        block = new MaterialPropertyBlock();
        hands = new Transform[2];
        shells = new Renderer[2];
        rings = new Renderer[2];
        for (int i = 0; i < 2; i++)
        {
            Transform hand = new GameObject(i == 0 ? "Grip Hand" : "Support Hand").transform;
            hand.SetParent(transform, false);
            shells[i] = Part(hand, "Orb", PrimitiveType.Sphere, DroneBody.ShellMaterial, Vector3.one * Size, true);
            // A glowing band round the middle, like a knuckle light
            rings[i] = Part(hand, "Band", PrimitiveType.Cylinder, DroneBody.GlowMaterial, new Vector3(Size * 1.04f, 0.008f, Size * 1.04f), false);
            hands[i] = hand;
        }

        // The energy cell that gets swapped while reloading; hidden the rest of the time
        cell = new GameObject("Energy Cell").transform;
        cell.SetParent(transform, false);
        cellRenderer = Part(cell, "Cell", PrimitiveType.Capsule, DroneBody.GlowMaterial, CellSize, false);
        cell.gameObject.SetActive(false);
    }

    private static Renderer Part(Transform parent, string name, PrimitiveType type, Material material, Vector3 scale, bool shadows)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        // No collider: right away, so it never trips the player's movement or blocks shots
        DestroyImmediate(part.GetComponent<Collider>());
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localScale = scale;
        Renderer renderer = part.GetComponent<Renderer>();
        if (material != null) renderer.sharedMaterial = material;
        if (!shadows) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return renderer;
    }

    // Works out where the hands go on a newly equipped gun, from the shape of its model
    private void FitTo(GameObject gun)
    {
        // Put the previous gun back straight, in case it was swapped mid-reload
        if (gunDesign != null) { gunDesign.localRotation = Quaternion.identity; gunDesign.localPosition = Vector3.zero; }

        shownGun = gun;
        placed = false;
        gunDesign = gun != null ? gun.transform.Find("Anchor/Design") : null;
        if (gunDesign == null && gun != null) gunDesign = gun.transform;
        if (gunDesign == null) return;

        // The biggest part is the gun's body; "Cube (1)" is the handle on every gun in the game
        Bounds body = new Bounds();
        float biggest = -1f;
        Transform handle = gunDesign.Find("Cube (1)");
        foreach (MeshFilter filter in gunDesign.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds b = LocalBounds(filter);
            float volume = b.size.x * b.size.y * b.size.z;
            if (volume > biggest) { biggest = volume; body = b; }
        }
        if (biggest < 0f) { gunDesign = null; return; }

        Vector3 grip;
        float handleBottom;
        MeshFilter handleMesh = handle != null ? handle.GetComponent<MeshFilter>() : null;
        if (handleMesh != null && handleMesh.sharedMesh != null)
        {
            Bounds h = LocalBounds(handleMesh);
            grip = h.center;
            handleBottom = h.min.y;
        }
        else
        {
            grip = new Vector3(body.center.x, body.min.y, body.min.z + body.size.z * 0.2f);
            handleBottom = body.min.y - 0.1f;
        }

        // Grip hand: wrapped round the side of the handle
        localGrips[0] = grip + new Vector3(0.04f, -0.06f, 0f);
        if (body.size.z > 0.9f)
        {
            // A long gun: the other hand holds it underneath, partway along
            float z = Mathf.Min(grip.z + 0.45f, body.max.z - 0.15f);
            localGrips[1] = new Vector3(body.center.x - 0.02f, body.min.y - 0.04f, z);
        }
        else
        {
            // A pistol: two-handed, the other hand cupped beside the grip
            localGrips[1] = grip + new Vector3(-0.09f, -0.09f, 0.03f);
        }
        slot = new Vector3(grip.x, handleBottom, grip.z);
        top = new Vector3(body.center.x, body.max.y + Size * 0.5f, grip.z + 0.1f);

        // The gun's layer: other players' guns are drawn by the main camera, your own by the viewmodel camera
        int layer = gunDesign.gameObject.layer;
        foreach (Transform t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    // A mesh's bounds in the gun model's space
    private Bounds LocalBounds(MeshFilter filter)
    {
        Bounds mesh = filter.sharedMesh.bounds;
        Bounds result = new Bounds(gunDesign.InverseTransformPoint(filter.transform.TransformPoint(mesh.center)), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            result.Encapsulate(gunDesign.InverseTransformPoint(filter.transform.TransformPoint(corner)));
        }
        return result;
    }

    // After the gun has moved this frame (aiming, sway, the player's gun drift)
    private void LateUpdate()
    {
        if (player == null || weapon == null) return;
        GameObject gun = weapon.GunModel;
        if (gun != shownGun) FitTo(gun);

        bool show = gunDesign != null && !player.IsDead;
        for (int i = 0; i < 2; i++) if (hands[i].gameObject.activeSelf != show) hands[i].gameObject.SetActive(show);
        if (!show)
        {
            if (cell.gameObject.activeSelf) cell.gameObject.SetActive(false);
            return;
        }

        float reload = weapon.ReloadProgress;
        PoseGun(reload);

        float time = Time.time;
        float dt = Time.deltaTime;
        Transform root = player.transform;

        for (int i = 0; i < 2; i++)
        {
            // In the gun's space, so the hands tip over with it
            Vector3 local = localGrips[i];
            if (i == 1 && reload >= 0f) local = ReloadHand(reload);
            Vector3 target = root.InverseTransformPoint(gunDesign.TransformPoint(local));
            // A gentle bob, each hand slightly out of step
            target += Vector3.up * (Mathf.Sin(time * 2.4f + phase + i * 1.3f) * 0.008f);

            // Snap there the first time, then trail just behind so they feel like they float
            positions[i] = placed ? Vector3.Lerp(positions[i], target, 1f - Mathf.Exp(-dt * 30f)) : target;
            hands[i].position = root.TransformPoint(positions[i]);
            hands[i].rotation = gunDesign.rotation;
        }
        placed = true;

        UpdateCell(reload, root);
    }

    #region Reload animation

    // The reload, as fractions of the reload time. The spent cell comes out with the first sound (0),
    // the fresh one goes in with the second (0.55), and the hand taps the gun with the ready chirp (0.9).
    private const float ReachSlot = 0.16f, PulledOut = 0.36f, Flick = 0.42f, Fetched = 0.48f,
        Inserted = 0.58f, Back = 0.78f, Tap = 0.9f;

    private static float Ease(float from, float to, float t) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, t));

    // Tips the gun over to the side and nose-up for the cell swap, with jolts when the cell goes in and
    // when the hand taps it ready. Turns round the handle, so the gun stays where it is held.
    private void PoseGun(float p)
    {
        if (p < 0f)
        {
            gunDesign.localRotation = Quaternion.identity;
            gunDesign.localPosition = Vector3.zero;
            return;
        }

        float tip = Ease(0f, 0.14f, p) * (1f - Ease(0.84f, 0.98f, p));
        // Short bumps: the cell clicking home, and the tap on top
        float click = Mathf.Sin(Mathf.PI * Mathf.Clamp01((p - Inserted) / 0.07f));
        float tap = Mathf.Sin(Mathf.PI * Mathf.Clamp01((p - (Tap - 0.02f)) / 0.06f));
        Quaternion rotation = Quaternion.Euler(-14f * tip - 5f * click + 4f * tap, 0f, 28f * tip);

        Vector3 pivot = localGrips[0];
        gunDesign.localRotation = rotation;
        gunDesign.localPosition = pivot - rotation * pivot + Vector3.down * (0.04f * tip);
    }

    // Where the free hand is during a reload, in the gun's space
    private Vector3 ReloadHand(float p)
    {
        Vector3 rest = localGrips[1];
        Vector3 atSlot = slot + Vector3.down * (Size * 0.5f + 0.02f);
        Vector3 pulled = atSlot + new Vector3(0f, -0.2f, -0.04f);
        Vector3 fetch = atSlot + new Vector3(-0.1f, -0.28f, 0.02f);
        Vector3 tapTop = top + Vector3.up * 0.02f;

        if (p < ReachSlot) return Vector3.Lerp(rest, atSlot, Ease(0f, ReachSlot, p));
        if (p < PulledOut) return Vector3.Lerp(atSlot, pulled, Ease(ReachSlot, PulledOut, p));
        if (p < Fetched) return Vector3.Lerp(pulled, fetch, Ease(PulledOut, Fetched, p));
        if (p < Inserted) return Vector3.Lerp(fetch, atSlot, Ease(Fetched, Inserted, p));
        if (p < Back) return Vector3.Lerp(atSlot, rest, Ease(Inserted + 0.04f, Back, p));
        if (p < Tap) return Vector3.Lerp(rest, tapTop, Ease(Back, Tap, p));
        return Vector3.Lerp(tapTop, rest, Ease(Tap, 1f, p));
    }

    // The energy cell: dim and spent coming out, then flicked away; bright and fresh going in
    private void UpdateCell(float p, Transform root)
    {
        bool visible = p >= ReachSlot && p < Inserted + 0.03f;
        if (cell.gameObject.activeSelf != visible) cell.gameObject.SetActive(visible);
        if (!visible) return;

        Transform hand = hands[1];
        // Held upright in the hand, poking up out of it towards the slot
        Vector3 held = hand.position + hand.up * (Size * 0.5f + 0.03f);
        float scale = 1f;
        bool spent = p < Fetched;

        if (p < Flick)
        {
            cell.position = held;
            cell.rotation = hand.rotation;
            tossStart = root.InverseTransformPoint(held);
            // Away from the player, out to the side and down
            tossVelocity = root.InverseTransformDirection(-hand.right * 1.2f + Vector3.down * 0.4f + hand.forward * 0.3f);
        }
        else if (spent)
        {
            // Flicked away, tumbling and shrinking to nothing
            float t = (p - Flick) * (weapon.CurrentGun != null ? weapon.CurrentGun.reloadTime : 1f);
            Vector3 local = tossStart + tossVelocity * t + Vector3.down * (4.9f * t * t);
            cell.position = root.TransformPoint(local);
            cell.rotation = hand.rotation * Quaternion.Euler(t * 900f, 0f, t * 400f);
            scale = 1f - Ease(Flick, Fetched, p);
        }
        else
        {
            // The fresh cell, pushed up into the slot at the end
            cell.rotation = hand.rotation;
            float push = Ease(Inserted - 0.03f, Inserted + 0.03f, p);
            Vector3 inSlot = gunDesign.TransformPoint(slot + Vector3.up * 0.04f);
            cell.position = Vector3.Lerp(held, inSlot, push);
            scale = 1f - Ease(Inserted, Inserted + 0.03f, p);
        }

        cell.localScale = Vector3.one * Mathf.Max(0.001f, scale);
        Color c = spent ? Color.Lerp(color, Color.black, 0.6f) : color;
        Paint(cellRenderer, c, spent ? c * 0.4f : c * CellGlow);
    }

    #endregion
}
