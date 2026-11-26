using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Makes the main menu's 3D backdrop feel alive: a dusk mood (fog in the sky's colour, a warm rim
// light on the player, a soft-focus background), a camera that drifts slowly and leans with the
// mouse, and a player model that breathes and glances towards the cursor. Built at runtime by
// the Launcher, on top of the menu scene. The model holds the gun form picked as the starting gun
// in Customize, with its hand-orbs and floating pieces, as in a match. Behind it, three bots fight
// (MenuBattle).
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
    // The menu model's gun, what holds it (aimed at the cursor by the scene), and the model
    private GameObject heldGun;
    private Transform gunHolder, gunOwner;
    private Quaternion modelRotation;
    private Vector3 modelPosition;
    private float modelYaw;

    // In Customize, the model can be turned round to see the hat from every side: dragged with the
    // mouse or a finger (anywhere off the menu), or with a controller's right stick. It keeps
    // spinning a little after a flick, and turns back to face the camera when Customize closes.
    public static bool Turnable { get; set; }
    private const float DragTurn = 0.45f;     // degrees per pixel dragged
    private const float StickTurn = 200f;     // degrees a second at full stick
    private float turnYaw, turnSpeed;
    private bool dragging;
    private float lastDragX;

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
        }

        // The model holds the starting gun. Picking another in Customize swaps it at once.
        FindHeldGun();
        // The menu scene's model has no Player script: it's the one holding the gun. It breathes and
        // glances towards the cursor (see Update).
        if (model == null && gunOwner != null)
        {
            model = gunOwner;
            modelRotation = model.rotation;
            modelPosition = model.position;
        }
        HoldStartingGun();
        GameSettings.StartingGunChanged += HoldStartingGun;

        SetMood();
        AddRimLight();
        volume = CreateVolume(main);

        // A few bots fighting in the background, behind the model
        MenuBattle.Create(main, model);
    }

    // The menu scene's model holds a pistol placed in the scene. Swap it for the starting gun (the
    // same model a match would show) and give it hand-orbs and floating pieces, in the logo's colour.
    // The menu scene's model is a copy of the player with its scripts removed, holding a pistol
    // placed in the scene: the one object there with a gun's Sway on it
    private void FindHeldGun()
    {
        foreach (Sway sway in FindObjectsByType<Sway>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (sway.transform.parent == null) continue;
            heldGun = sway.gameObject;
            gunHolder = sway.transform.parent;
            gunOwner = gunHolder.root;
            return;
        }
    }

    // Swaps the held gun for the starting gun (the same model a match would show), held still, with
    // hand-orbs and floating pieces in the logo's colour
    private void HoldStartingGun()
    {
        if (gunHolder == null) return;
        GameObject prefab = Resources.Load<GameObject>("Player");
        Weapon weapon = prefab != null ? prefab.GetComponent<Weapon>() : null;
        if (weapon == null || weapon.loadout == null || weapon.loadout.Length == 0) return;
        Gun gun = weapon.loadout[weapon.PreferredStartingSlot];
        if (gun == null || gun.prefab == null) return;

        // Again after a change of starting gun: the old gun's hand-orbs and pieces go with it
        foreach (DroneHands old in gunOwner.GetComponentsInChildren<DroneHands>(true)) Destroy(old.gameObject);
        foreach (GunSatellites old in gunOwner.GetComponentsInChildren<GunSatellites>(true)) Destroy(old.gameObject);

        if (heldGun == null || heldGun.name != gun.prefab.name)
        {
            GameObject model = Instantiate(gun.prefab, gunHolder, false);
            model.name = gun.prefab.name;
            GunModel.Build(model, gun);
            if (heldGun != null)
            {
                model.transform.localPosition = heldGun.transform.localPosition;
                model.transform.localRotation = heldGun.transform.localRotation;
                model.transform.localScale = heldGun.transform.localScale;
                Destroy(heldGun);
            }
            heldGun = model;
        }

        // Held still: no sway from the mouse, no colliders, and the shot line is only a template
        foreach (Sway sway in heldGun.GetComponentsInChildren<Sway>(true)) sway.enabled = false;
        foreach (Collider c in heldGun.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (LineRenderer line in heldGun.GetComponentsInChildren<LineRenderer>(true)) line.enabled = false;

        DroneHands.AttachToDisplay(gunOwner, heldGun, PlayerCap.LogoColor);
        GunSatellites.AttachToDisplay(gunOwner, heldGun, gun, PlayerCap.LogoColor);
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
            modelYaw = Mathf.Lerp(modelYaw, Turnable ? 0f : -lean.x * 14f, 1f - Mathf.Exp(-dt * 3f));
            Turn(dt);
            model.rotation = Quaternion.Euler(0f, modelYaw + turnYaw, 0f) * modelRotation;
            model.position = modelPosition + Vector3.up * (Mathf.Sin(time * 1.6f) * 0.012f);
        }
    }

    // Turns the model round while Customize is open (see Turnable), or back to face the camera
    private void Turn(float dt)
    {
        if (dt <= 0f) return;
        if (!Turnable)
        {
            dragging = false;
            turnSpeed = 0f;
            turnYaw = Mathf.LerpAngle(turnYaw, 0f, 1f - Mathf.Exp(-dt * 5f));
            if (Mathf.Abs(Mathf.DeltaAngle(turnYaw, 0f)) < 0.05f) turnYaw = 0f;
            return;
        }

        // Dragging: started off the menu, so dragging a slider or scrolling the hat list doesn't turn it.
        // Dragging right turns the model's front to the right.
        Pointer pointer = Pointer.current;
        bool held = pointer != null && pointer.press.isPressed;
        if (held && pointer.press.wasPressedThisFrame)
        {
            UnityEngine.EventSystems.EventSystem events = UnityEngine.EventSystems.EventSystem.current;
            dragging = events == null || !events.IsPointerOverGameObject();
            lastDragX = pointer.position.ReadValue().x;
        }
        if (!held) dragging = false;
        if (dragging)
        {
            float x = pointer.position.ReadValue().x;
            float step = -(x - lastDragX) * DragTurn;
            lastDragX = x;
            turnYaw += step;
            // Its speed, for the spin it keeps after letting go
            turnSpeed = Mathf.Lerp(turnSpeed, step / dt, 1f - Mathf.Exp(-dt * 20f));
            return;
        }

        Gamepad pad = Gamepad.current;
        float stick = pad != null ? pad.rightStick.ReadValue().x : 0f;
        if (Mathf.Abs(stick) > 0.15f)
        {
            turnSpeed = -stick * StickTurn;
        }
        else
        {
            // Let go: the spin dies away
            turnSpeed = Mathf.Lerp(turnSpeed, 0f, 1f - Mathf.Exp(-dt * 3f));
        }
        turnYaw = Mathf.Repeat(turnYaw + turnSpeed * dt + 180f, 360f) - 180f;
    }

    private void OnDestroy()
    {
        Turnable = false;
        GameSettings.StartingGunChanged -= HoldStartingGun;
        if (volume != null)
        {
            if (volume.sharedProfile != null) Destroy(volume.sharedProfile);
            Destroy(volume.gameObject);
        }
    }
}
