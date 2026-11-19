using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class Look : MonoBehaviourPunCallbacks
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

    #endregion

    #region MonoBehaviour Callbacks
    // Start is called before the first frame update
    void Start()
    {
        camCenter = cam.localRotation;
        weaponComponent = GetComponent<Weapon>();
    }


    // Update is called once per frame
    void Update()
    {
        if (!photonView.IsMine) return;
        if (Pause.paused && photonView.IsMine) return;

        // Sensitivity is changed in the settings menu and saved in the profile
        xSensitivity = GameSettings.XSensitivity;
        ySensitivity = GameSettings.YSensitivity;

        ApplyRecoil();
        SetX();
        SetY();
        UpdateCursorLock();
    }

    #endregion

    #region Private Methods

    // Gamepad stick: full deflection turns about as fast as 60 units of mouse movement per second
    private const float StickLookScale = 60f;
    private Weapon weaponComponent;

    // Mouse and gamepad look combined, in old mouse-axis units, with the ADS multiplier from the settings
    Vector2 LookInput()
    {
        Vector2 look = GameInput.MouseLookValue + GameInput.StickLookValue * StickLookScale * Time.deltaTime;

        if (weaponComponent != null && weaponComponent.isAiming) look *= GameSettings.AdsSensitivity;

        return look;
    }

    void SetY()
    {
        float input = LookInput().y * ySensitivity * SensitivityScale;
        Quaternion adj = Quaternion.AngleAxis(input, -Vector3.right);
        Quaternion delta = cam.localRotation * adj;
        if (Quaternion.Angle(camCenter, delta) < maxAngle)
        {
            cam.localRotation = delta;
        }
            weapon.rotation = cam.rotation;
    }

    void SetX()
    {
        float input = LookInput().x * xSensitivity * SensitivityScale;
        Quaternion adj = Quaternion.AngleAxis(input, Vector3.up);
        Quaternion delta = player.localRotation * adj;
            player.localRotation = delta;

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

        Quaternion pitched = cam.localRotation * Quaternion.AngleAxis(delta.x, -Vector3.right);
        if (Quaternion.Angle(camCenter, pitched) < maxAngle) cam.localRotation = pitched;

        player.localRotation *= Quaternion.AngleAxis(delta.y, Vector3.up);
    }

    #endregion
}
