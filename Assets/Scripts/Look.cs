using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Mouse and stick look for the local player. Keeps the view as a yaw and pitch angle, which are
// sent to the host with the player's input (see InputCollector) and also applied here every frame,
// so turning never waits for the network. The mouse is read once per frame (SampleLook), by whichever
// comes first, this script or the input sent for a tick, so shots always go where the crosshair is
// drawn that frame. No smoothing or acceleration is applied. Camera effects (firing shake, kill punch)
// only roll the view, which keeps the crosshair on the real aim; their up and sideways kick goes to
// the gun model instead.
public class Look : MonoBehaviour
{

    #region Variables
    public static bool cursorLocked = true;

    public Transform player;
    public Transform cam;
    public Transform weapon;

    public float xSensitivity;
    public float ySensitivity;
    public float maxAngle;

    private Quaternion camCenter;

    // Mouse input is already a per-frame movement, so it must not be scaled by Time.deltaTime.
    // This fixed scale keeps existing sensitivity values feeling the same as they did at 60 FPS.
    private const float SensitivityScale = 1f / 60f;

    [Header("Recoil")]
    // How fast the view snaps to a new kick, and how fast it settles back afterwards
    public float recoilSnappiness = 25f;
    public float recoilRecovery = 7f;

    private Vector2 recoilTarget;   // x = pitch up, y = yaw right, in degrees
    private Vector2 recoilCurrent;
    // How fast the view settles back after the last kick: the gun form's own (Gun.kickRecovery), or
    // recoilRecovery for kicks that don't say
    private float currentRecovery = -1f;

    [Header("Firing shake")]
    // Each shot's shake of the view (Gun.shake) and widening of the field of view (Gun.fovPunch): short
    // and separate from the kill punch, so firing never cuts a kill's shake short. The view rolls and
    // the gun kicks (ApplyView); where you aim (Yaw, Pitch) and the crosshair don't move.
    public float fireShakeTime = 0.16f;
    public float fovKickRecovery = 14f;
    private float fireShakeStrength;
    private float fireShakeAge = 1f;
    private float fireShakeSeed;
    // Degrees added to the field of view right now (Player adds it), dying away after each shot
    public float FovKick { get; private set; }

    [Header("Kill punch")]
    // A short shake on a kill: the view rolls and the gun kicks (ApplyView); where you aim (Yaw, Pitch)
    // and the crosshair don't move.
    public float punchTime = 0.35f;
    private float punchStrength;
    private float punchAge = 1f;
    private float punchSeed;

    // Where the player looks, in degrees. Yaw turns the body; pitch (positive = up) tilts the camera.
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }

    private Player owner;
    private Weapon weaponComponent;
    private bool initialized;
    // The frame the mouse was last read in (SampleLook)
    private int sampledFrame = -1;

    // Degrees turned per count of mouse movement at a sensitivity setting
    public static float DegreesPerCount(float sensitivity) => sensitivity * GameInput.MouseScale * SensitivityScale;

    #endregion

    #region MonoBehaviour Callbacks

    void Awake()
    {
        camCenter = cam.localRotation;
        weaponComponent = GetComponent<Weapon>();
        owner = GetComponent<Player>();
    }

    // Called by Player when the local player spawns, facing the spawn point's direction
    public void Initialize(float yaw)
    {
        Yaw = yaw;
        Pitch = 0f;
        recoilTarget = recoilCurrent = Vector2.zero;
        currentRecovery = -1f;
        fireShakeAge = fireShakeTime;
        FovKick = 0f;
        initialized = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (!initialized || owner == null || !owner.IsLocal) return;

        // The firing punch dies away even while paused, so it never sticks
        FovKick = Mathf.Lerp(FovKick, 0f, 1f - Mathf.Exp(-Time.deltaTime * fovKickRecovery));

        if (Pause.paused) return;

        SampleLook();
        UpdateCursorLock();
    }

    // Turns the view by this frame's mouse and stick movement and recoil, once per frame. Called from
    // Update and from InputCollector before it sends Yaw and Pitch, whichever runs first: Fusion may
    // gather a tick's input before this script's Update, and the shot would then use last frame's aim.
    public void SampleLook()
    {
        if (!initialized || owner == null || !owner.IsLocal || Pause.paused) return;
        if (sampledFrame == Time.frameCount) return;
        sampledFrame = Time.frameCount;

        // Sensitivity is changed in the settings menu and saved in the profile
        xSensitivity = GameSettings.XSensitivity;
        ySensitivity = GameSettings.YSensitivity;

        ApplyRecoil();

        Vector2 look = LookInput();
        Yaw = Mathf.Repeat(Yaw + look.x * xSensitivity * SensitivityScale, 360f);
        if (GameSettings.InvertY) look.y = -look.y;
        Pitch = Mathf.Clamp(Pitch + look.y * ySensitivity * SensitivityScale, -maxAngle, maxAngle);
    }

    // After Fusion has placed the body for this frame, turn it and the camera to the newest view,
    // so looking around stays as responsive as it was before networking was added
    void LateUpdate()
    {
        if (!initialized || owner == null || !owner.IsLocal) return;
        ApplyView();
    }

    #endregion

    #region Private Methods

    // Gamepad stick: full deflection turns about as fast as 60 units of mouse movement per second
    private const float StickLookScale = 60f;

    // Mouse and gamepad look combined, in old mouse-axis units, with the ADS multiplier from the settings
    Vector2 LookInput()
    {
        Vector2 stick = GameInput.StickLookValue * StickLookScale * Time.deltaTime;
        Vector2 pointer = GameInput.MouseLookValue;
        // Aim assist for a stick or a thumb (not a mouse): slower turning over an enemy
        bool touch = TouchControls.UsingTouch;
        if (stick.sqrMagnitude > 0f || (touch && pointer.sqrMagnitude > 0f))
        {
            float slowdown = AimSlowdown();
            stick *= slowdown;
            // With touch controls the drag arrives as pointer movement
            if (touch) pointer *= slowdown;
        }
        Vector2 look = pointer + stick;

        // Aiming zooms in, so turning slows by as much as the view narrows (the same mouse movement
        // sweeps the same distance on screen), then the player's own aim multiplier on top
        if (weaponComponent != null && weaponComponent.isAiming) look *= GameSettings.AdsSensitivity * AimZoomSensitivity();

        return look;
    }

    // How much slower turning is while aiming the current gun: its scope's view, or the plain aim zoom
    float AimZoomSensitivity()
    {
        Gun gun = weaponComponent != null ? weaponComponent.CurrentGun : null;
        float baseFov = GameSettings.Fov;
        float aimFov = gun != null && gun.scopeFov > 0f ? gun.scopeFov : baseFov * Player.AimFovMultiplier;
        return LookRules.ZoomSensitivity(aimFov, baseFov);
    }

    // AimAssist's slowdown for the enemy nearest the crosshair that can be seen, or 1 for none
    float AimSlowdown()
    {
        Manager manager = Manager.Instance;
        if (owner == null || manager == null) return 1f;
        Vector3 eye = cam.position;
        Vector3 forward = cam.forward;
        float best = 1f;
        Player bestTarget = null;
        Vector3 bestPoint = Vector3.zero;
        foreach (Player p in Player.All)
        {
            if (p == null || p == owner || p.IsDead || !manager.AreEnemies(owner.ActorId, p.ActorId)) continue;
            // The middle of the body
            Vector3 point = p.transform.position + Vector3.up * 0.2f;
            Vector3 to = point - eye;
            float distance = to.magnitude;
            if (distance > AimAssist.Range) continue;
            float slowdown = AimAssist.Slowdown(Vector3.Angle(forward, to), distance);
            if (slowdown < best)
            {
                best = slowdown;
                bestTarget = p;
                bestPoint = point;
            }
        }
        if (bestTarget == null) return 1f;
        // Not through walls: only the world counts, not players, guns or hitboxes
        int world = Physics.DefaultRaycastLayers & ~((1 << Layers.LocalPlayer) | (1 << Layers.RemotePlayer) | (1 << Layers.Gun) | (1 << Layers.LocalGun) | (1 << Layers.Hitbox) | (1 << Layers.Glass));
        if (Physics.Linecast(eye, bestPoint, world, QueryTriggerInteraction.Ignore)) return 1f;
        return best;
    }

    void ApplyView()
    {
        player.rotation = Quaternion.Euler(0f, Yaw, 0f);
        // Shakes as (up, sideways, roll) degrees. The view only rolls, which turns the picture round the
        // crosshair without moving it off where shots go; the gun takes the up and sideways kick.
        Vector3 shake = KillPunch() + FireShake();
        cam.localRotation = camCenter * Quaternion.AngleAxis(Pitch, -Vector3.right) * Quaternion.Euler(0f, 0f, shake.z);
        weapon.rotation = cam.rotation * Quaternion.Euler(-shake.x, shake.y, 0f);
    }

    // Shakes the view for a moment: strength is the first swing, in degrees
    public void Punch(float strength)
    {
        // Off in the settings for players who get motion sick
        if (!GameSettings.ScreenShake) return;
        punchStrength = strength;
        punchAge = 0f;
        punchSeed = Random.Range(0f, 100f);
    }

    // A shot's shake of the view (degrees for the first swing) and widening of the field of view
    // (degrees). Off in the settings for players who get motion sick, like the kill punch.
    public void FireKick(float shake, float fovPunch)
    {
        if (!GameSettings.ScreenShake) return;
        if (shake > 0f)
        {
            // A new shot restarts the shake, never weaker than what's still ringing from the last one
            float left = fireShakeAge < fireShakeTime ? fireShakeStrength * (1f - fireShakeAge / fireShakeTime) : 0f;
            fireShakeStrength = Mathf.Max(shake, left);
            fireShakeAge = 0f;
            fireShakeSeed = Random.Range(0f, 100f);
        }
        if (fovPunch > 0f) FovKick = Mathf.Max(FovKick, fovPunch);
    }

    // This frame's firing shake as (up, sideways, roll) degrees: a fast, tight shudder (quicker than the
    // kill punch) that's gone in fireShakeTime
    Vector3 FireShake()
    {
        if (fireShakeAge >= fireShakeTime) return Vector3.zero;
        fireShakeAge += Time.deltaTime;
        float t = fireShakeAge;
        float fade = Mathf.Exp(-t * 18f) * Mathf.Clamp01(1f - t / fireShakeTime);
        float up = Mathf.Sin(t * 90f + fireShakeSeed) * fireShakeStrength * fade;
        float side = Mathf.Sin(t * 70f + fireShakeSeed * 1.3f) * fireShakeStrength * 0.5f * fade;
        float roll = Mathf.Sin(t * 60f + fireShakeSeed * 0.7f) * fireShakeStrength * 0.9f * fade;
        return new Vector3(up, side, roll);
    }

    // This frame's kill shake as (up, sideways, roll) degrees: a quick wobble that dies away over punchTime
    Vector3 KillPunch()
    {
        if (punchAge >= punchTime) return Vector3.zero;
        punchAge += Time.deltaTime;
        float t = punchAge;
        float fade = Mathf.Exp(-t * 9f) * Mathf.Clamp01(1f - t / punchTime);
        float up = Mathf.Sin(t * 55f + punchSeed) * punchStrength * fade;
        float side = Mathf.Sin(t * 41f + punchSeed * 1.7f) * punchStrength * 0.6f * fade;
        float roll = Mathf.Sin(t * 33f + punchSeed * 0.5f) * punchStrength * 0.8f * fade;
        return new Vector3(up, side, roll);
    }

    void UpdateCursorLock()
    {
        // Touch controls have no mouse to lock, and the pause menu has its own on-screen button
        if (TouchControls.UsingTouch) return;

        if (cursorLocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (GameInput.PausePressed)
            {
                cursorLocked = false;
            }
        }
        else {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (GameInput.PausePressed)
            {
                cursorLocked = true;
            }
        }

    }

    // Kicks the view by the given degrees. The kick is smoothed in and then recovers back on its own.
    public void AddRecoil(float pitch, float yaw)
    {
        recoilTarget += new Vector2(pitch, yaw);
        currentRecovery = -1f;
    }

    // The same, settling back at 'recovery' a second (a gun form's own pace) instead of recoilRecovery
    public void AddRecoil(float pitch, float yaw, float recovery)
    {
        recoilTarget += new Vector2(pitch, yaw);
        currentRecovery = recovery;
    }

    void ApplyRecoil()
    {
        Vector2 previous = recoilCurrent;
        float recovery = currentRecovery > 0f ? currentRecovery : recoilRecovery;
        // Eased the same at any frame rate (as these rates felt at 60 frames a second)
        recoilCurrent = Vector2.Lerp(recoilCurrent, recoilTarget, LookRules.Ease(recoilSnappiness, Time.deltaTime));
        recoilTarget = Vector2.Lerp(recoilTarget, Vector2.zero, LookRules.Ease(recovery, Time.deltaTime));

        Vector2 delta = recoilCurrent - previous;
        Pitch = Mathf.Clamp(Pitch + delta.x, -maxAngle, maxAngle);
        Yaw = Mathf.Repeat(Yaw + delta.y, 360f);
    }

    #endregion
}
