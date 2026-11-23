using UnityEngine;

// Two little floating hand-orbs that hold the player's gun: one on the grip, the other under the barrel
// (or beside the grip on a pistol). The drones have no arms, so the orbs simply hover there, bobbing
// gently and trailing the gun a touch. While reloading (the gun is put away) they spin round each other.
// Drawn on the gun's layer, so other players see them with the gun, and you see your own in first person.
// Added by Player at runtime; purely cosmetic, so it needs no network traffic.
public class DroneHands : MonoBehaviour
{
    private const float Size = 0.13f;
    private const float RingGlow = 2.4f;

    private Player player;
    private Weapon weapon;
    private Transform[] hands;
    private Renderer[] shells, rings;
    private Vector3[] localGrips = new Vector3[2]; // in the gun model's space
    private Vector3[] positions = new Vector3[2];  // smoothed, in the player's space
    private GameObject shownGun;
    private Transform gunDesign;
    private bool placed;
    private float phase;
    private MaterialPropertyBlock block;

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

    public void SetColor(Color color)
    {
        if (block == null) block = new MaterialPropertyBlock();
        Color shell = new Color(0.93f, 0.94f, 0.96f);
        for (int i = 0; i < 2; i++)
        {
            Paint(shells[i], shell, Color.black);
            Paint(rings[i], color, color * RingGlow);
        }
    }

    private void Paint(Renderer r, Color color, Color emission)
    {
        r.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
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
        if (biggest < 0f) return;

        Vector3 grip = handle != null ? gunDesign.InverseTransformPoint(handle.position) : new Vector3(body.center.x, body.min.y, body.min.z + body.size.z * 0.2f);
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
        GameObject gun = weapon.CurrentGun;
        if (gun != shownGun) FitTo(gun);

        bool show = gunDesign != null && !player.IsDead;
        for (int i = 0; i < 2; i++) if (hands[i].gameObject.activeSelf != show) hands[i].gameObject.SetActive(show);
        if (!show) return;

        float time = Time.time;
        float dt = Time.deltaTime;
        bool reloading = !gun.activeSelf;
        Transform root = player.transform;

        for (int i = 0; i < 2; i++)
        {
            Vector3 target = root.InverseTransformPoint(gunDesign.TransformPoint(localGrips[i]));
            if (reloading)
            {
                // The gun is put away: the two orbs circle round each other where the grip was
                Vector3 centre = root.InverseTransformPoint(gunDesign.TransformPoint((localGrips[0] + localGrips[1]) * 0.5f));
                float angle = time * 9f + i * Mathf.PI;
                target = centre + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f, Mathf.Sin(angle)) * 0.09f;
            }
            // A gentle bob, each hand slightly out of step
            target += Vector3.up * (Mathf.Sin(time * 2.4f + phase + i * 1.3f) * 0.008f);

            // Snap there the first time, then trail just behind so they feel like they float
            positions[i] = placed ? Vector3.Lerp(positions[i], target, 1f - Mathf.Exp(-dt * 30f)) : target;
            hands[i].position = root.TransformPoint(positions[i]);
            hands[i].rotation = gunDesign.rotation;
        }
        placed = true;
    }
}
