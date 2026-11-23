using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Mouse and stick look for the local player. Keeps the view as a yaw and pitch angle, which are
// sent to the host with the player's input (see InputCollector) and also applied here every frame,
// so turning never waits for the network.
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

    [Header("Kill punch")]
    // A short shake of the view on a kill. Only the camera moves: where you aim (Yaw, Pitch) doesn't.
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
        initialized = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (!initialized || owner == null || !owner.IsLocal) return;

        if (Pause.paused) return;

        // Sensitivity is changed in the settings menu and saved in the profile
        xSensitivity = GameSettings.XSensitivity;
        ySensitivity = GameSettings.YSensitivity;

        ApplyRecoil();

        Vector2 look = LookInput();
        Yaw = Mathf.Repeat(Yaw + look.x * xSensitivity * SensitivityScale, 360f);
        Pitch = Mathf.Clamp(Pitch + look.y * ySensitivity * SensitivityScale, -maxAngle, maxAngle);

        UpdateCursorLock();
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
        Vector2 look = GameInput.MouseLookValue + GameInput.StickLookValue * StickLookScale * Time.deltaTime;

        if (weaponComponent != null && weaponComponent.isAiming) look *= GameSettings.AdsSensitivity;

        return look;
    }

    void ApplyView()
    {
        player.rotation = Quaternion.Euler(0f, Yaw, 0f);
        cam.localRotation = camCenter * Quaternion.AngleAxis(Pitch, -Vector3.right) * KillPunch();
        weapon.rotation = cam.rotation;
    }

    // Shakes the view for a moment: strength is the first swing, in degrees
    public void Punch(float strength)
    {
        punchStrength = strength;
        punchAge = 0f;
        punchSeed = Random.Range(0f, 100f);
    }

    // This frame's shake: a quick wobble up and sideways that dies away over punchTime
    Quaternion KillPunch()
    {
        if (punchAge >= punchTime) return Quaternion.identity;
        punchAge += Time.deltaTime;
        float t = punchAge;
        float fade = Mathf.Exp(-t * 9f) * Mathf.Clamp01(1f - t / punchTime);
        float up = Mathf.Sin(t * 55f + punchSeed) * punchStrength * fade;
        float side = Mathf.Sin(t * 41f + punchSeed * 1.7f) * punchStrength * 0.6f * fade;
        float roll = Mathf.Sin(t * 33f + punchSeed * 0.5f) * punchStrength * 0.8f * fade;
        return Quaternion.Euler(-up, side, roll);
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
    }

    void ApplyRecoil()
    {
        Vector2 previous = recoilCurrent;
        recoilCurrent = Vector2.Lerp(recoilCurrent, recoilTarget, Time.deltaTime * recoilSnappiness);
        recoilTarget = Vector2.Lerp(recoilTarget, Vector2.zero, Time.deltaTime * recoilRecovery);

        Vector2 delta = recoilCurrent - previous;
        Pitch = Mathf.Clamp(Pitch + delta.x, -maxAngle, maxAngle);
        Yaw = Mathf.Repeat(Yaw + delta.y, 360f);
    }

    #endregion
}
