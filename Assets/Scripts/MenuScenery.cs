using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Makes the main menu's 3D backdrop feel alive: a dusk mood (fog in the sky's colour, a warm rim
// light on the player, a soft-focus background), a camera that drifts slowly and leans with the
// mouse, and a player model that breathes and glances towards the cursor. Built at runtime by
// the Launcher, on top of the menu scene. The model holds the gun form picked as the starting gun
// in the settings, with its hand-orbs and floating pieces, as in a match.
public class MenuScenery : MonoBehaviour
{
    // Camera drift: a slow figure-of-eight, plus a lean towards the mouse
    private const float DriftAmount = 0.12f;
    private const float DriftSpeed = 0.18f;
    private const float ParallaxMove = 0.25f;
    private const float ParallaxTurn = 1.5f;

    private Transform cam;
    private Vector3 camPosition;
    private Quaternion camRotation;
    private Vector2 lean;

    private Transform model;
    private Player menuPlayer;
    private Quaternion modelRotation;
    private Vector3 modelPosition;
    private float modelYaw;

    private float fadeIn;
    private Volume volume;

    public static void Create()
    {
        if (FindAnyObjectByType<MenuScenery>() != null) return;
        new GameObject("Menu Scenery").AddComponent<MenuScenery>();
    }

    private void Start()
    {
        Camera main = Camera.main;
        if (main != null)
        {
            cam = main.transform;
            camPosition = cam.position;
            camRotation = cam.rotation;
        }

        Player player = FindAnyObjectByType<Player>(FindObjectsInactive.Include);
        if (player != null)
        {
            model = player.transform;
            modelRotation = model.rotation;
            modelPosition = model.position;
            menuPlayer = player;
            HoldStartingGun(player);
            // Picking another starting gun in the menu's settings swaps it at once
            GameSettings.StartingGunChanged += OnStartingGunChanged;
        }

        SetMood();
        AddRimLight();
        volume = CreateVolume(main);
    }

    // The menu scene's model holds a pistol placed in the scene. Swap it for the starting gun (the
    // same model a match would show) and give it hand-orbs and floating pieces, in the logo's colour.
    private void OnStartingGunChanged()
    {
        if (menuPlayer != null) HoldStartingGun(menuPlayer);
    }

    private static void HoldStartingGun(Player player)
    {
        // Again after a change of starting gun: the old gun's hand-orbs and pieces go with it
        foreach (DroneHands old in player.GetComponentsInChildren<DroneHands>(true)) Destroy(old.gameObject);
        foreach (GunSatellites old in player.GetComponentsInChildren<GunSatellites>(true)) Destroy(old.gameObject);

        Weapon weapon = player.GetComponent<Weapon>();
        if (weapon == null || weapon.loadout == null || weapon.loadout.Length == 0 || weapon.weaponParent == null) return;
        Gun gun = weapon.loadout[weapon.PreferredStartingSlot];
        if (gun == null || gun.prefab == null) return;

        Transform holder = weapon.weaponParent;
        Transform placed = holder.childCount > 0 ? holder.GetChild(0) : null;
        GameObject model;
        if (placed != null && placed.name == gun.prefab.name)
        {
            model = placed.gameObject;
        }
        else
        {
            model = Instantiate(gun.prefab, holder, false);
            model.name = gun.prefab.name;
            if (placed != null)
            {
                model.transform.localPosition = placed.localPosition;
                model.transform.localRotation = placed.localRotation;
                model.transform.localScale = placed.localScale;
                Destroy(placed.gameObject);
            }
        }

        // Held still: no sway from the mouse, no colliders, and the shot line is only a template
        foreach (Sway sway in model.GetComponentsInChildren<Sway>(true)) sway.enabled = false;
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (LineRenderer line in model.GetComponentsInChildren<LineRenderer>(true)) line.enabled = false;

        DroneHands.AttachToDisplay(player.transform, model, PlayerCap.LogoColor);
        GunSatellites.AttachToDisplay(player.transform, model, gun, PlayerCap.LogoColor);
    }

    // Fog the colour of the sunset sky's horizon, so the far end of the floor melts into it
    private static void SetMood()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.60f, 0.50f, 0.52f);
        RenderSettings.fogDensity = 0.011f;
    }

    // A warm light from behind and to the side, so the player stands out from the background
    private void AddRimLight()
    {
        if (model == null || cam == null) return;
        Vector3 toCamera = camPosition - modelPosition;
        toCamera.y = 0f;
        Vector3 back = -toCamera.normalized;
        Vector3 side = Vector3.Cross(Vector3.up, back);

        Light rim = new GameObject("Rim Light").AddComponent<Light>();
        rim.transform.SetParent(transform, false);
        rim.type = LightType.Point;
        rim.color = new Color(1f, 0.62f, 0.38f);
        rim.intensity = 6f;
        rim.range = 6f;
        rim.shadows = LightShadows.None;
        rim.transform.position = modelPosition + back * 1.6f + side * 1.2f + Vector3.up * 1.4f;

        // A cool fill from the camera side, so the front isn't flat
        Light fill = new GameObject("Fill Light").AddComponent<Light>();
        fill.transform.SetParent(transform, false);
        fill.type = LightType.Point;
        fill.color = new Color(0.55f, 0.75f, 1f);
        fill.intensity = 1.5f;
        fill.range = 6f;
        fill.shadows = LightShadows.None;
        fill.transform.position = modelPosition - back * 2.5f - side * 1.5f + Vector3.up * 1.8f;
    }

    // Soft focus beyond the player and a stronger vignette, on the menu only
    private static Volume CreateVolume(Camera main)
    {
        Volume volume = new GameObject("Menu Look").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 50f;
        volume.weight = 1f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        DepthOfField dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(9f);
        dof.gaussianEnd.Override(45f);
        dof.gaussianMaxRadius.Override(1f);
        dof.highQualitySampling.Override(false);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.45f);

        volume.sharedProfile = profile;
        if (main != null) main.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        return volume;
    }

    private void Update()
    {
        float time = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;

        // Mouse position from the screen centre, -1 to 1 (centred when there's no mouse)
        Vector2 mouse = Vector2.zero;
        Mouse device = Mouse.current;
        if (device != null && Screen.width > 0 && Screen.height > 0)
        {
            Vector2 pointer = device.position.ReadValue();
            mouse = new Vector2(pointer.x / Screen.width * 2f - 1f, pointer.y / Screen.height * 2f - 1f);
            mouse = Vector2.ClampMagnitude(mouse, 1.4f);
        }
        lean = Vector2.Lerp(lean, mouse, 1f - Mathf.Exp(-dt * 2.5f));

        // Ease in from a little further back as the menu opens
        fadeIn = Mathf.MoveTowards(fadeIn, 1f, dt * 0.6f);
        float settle = 1f - Mathf.SmoothStep(0f, 1f, fadeIn);

        if (cam != null)
        {
            Vector3 drift = new Vector3(Mathf.Sin(time * DriftSpeed), Mathf.Sin(time * DriftSpeed * 2f) * 0.5f, 0f) * DriftAmount;
            Vector3 offset = camRotation * (drift + new Vector3(lean.x * ParallaxMove, lean.y * ParallaxMove * 0.5f, -settle * 0.8f));
            cam.position = camPosition + offset;
            cam.rotation = camRotation * Quaternion.Euler(-lean.y * ParallaxTurn * 0.6f, lean.x * ParallaxTurn, Mathf.Sin(time * DriftSpeed * 0.7f) * 0.4f);
        }

        if (model != null)
        {
            // Glance towards the cursor, and breathe
            modelYaw = Mathf.Lerp(modelYaw, -lean.x * 14f, 1f - Mathf.Exp(-dt * 3f));
            model.rotation = Quaternion.Euler(0f, modelYaw, 0f) * modelRotation;
            model.position = modelPosition + Vector3.up * (Mathf.Sin(time * 1.6f) * 0.012f);
        }
    }

    private void OnDestroy()
    {
        GameSettings.StartingGunChanged -= OnStartingGunChanged;
        if (volume != null)
        {
            if (volume.sharedProfile != null) Destroy(volume.sharedProfile);
            Destroy(volume.gameObject);
        }
    }
}
