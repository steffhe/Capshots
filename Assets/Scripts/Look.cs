using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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

    private Slider xslider;
    private Slider yslider;

    private ProfileData myProfile;
    private UnityEngine.Events.UnityAction<float> onXSensChanged;
    private UnityEngine.Events.UnityAction<float> onYSensChanged;

    #endregion

    #region MonoBehaviour Callbacks
    // Start is called before the first frame update
    void Start()
    {
        camCenter = cam.localRotation;

        if (!photonView.IsMine) return;

        // Share the profile with the rest of the game so saving sensitivity never overwrites XP or level
        myProfile = Launcher.myProfile;
        xSensitivity = myProfile.xsens;
        ySensitivity = myProfile.ysens;

        xslider = GameObject.Find("Canvas/Pause/Anchor/Sensitivity/XSens").GetComponent<Slider>();
        yslider = GameObject.Find("Canvas/Pause/Anchor/Sensitivity/YSens").GetComponent<Slider>();

        xslider.value = xSensitivity;
        yslider.value = ySensitivity;

        onXSensChanged = delegate { SetXSens(); };
        onYSensChanged = delegate { SetYSens(); };
        xslider.onValueChanged.AddListener(onXSensChanged);
        yslider.onValueChanged.AddListener(onYSensChanged);
    }

    private void OnDestroy()
    {
        if (xslider != null && onXSensChanged != null) xslider.onValueChanged.RemoveListener(onXSensChanged);
        if (yslider != null && onYSensChanged != null) yslider.onValueChanged.RemoveListener(onYSensChanged);
    }


    // Update is called once per frame
    void Update()
    {
        if (!photonView.IsMine) return;
        if (Pause.paused && photonView.IsMine) return;

        SetX();
        SetY();
        UpdateCursorLock();
    }

    #endregion

    #region Private Methods

    void SetY()
    {
        float input = Input.GetAxis("Mouse Y") * ySensitivity * Time.deltaTime;
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
        float input = Input.GetAxis("Mouse X") * xSensitivity * Time.deltaTime;
        Quaternion adj = Quaternion.AngleAxis(input, Vector3.up);
        Quaternion delta = player.localRotation * adj;
            player.localRotation = delta;

    }

    void UpdateCursorLock()
    {
        if (cursorLocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                cursorLocked = false;
            }
        }
        else {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                cursorLocked = true;
            }
        }

    }

    public void SetXSens()
    {
        xSensitivity = xslider.value;
        myProfile.xsens = xSensitivity;
        Data.SaveProfile(myProfile);
    }

    public void SetYSens()
    {
        ySensitivity = yslider.value;
        myProfile.ysens = ySensitivity;
        Data.SaveProfile(myProfile);

    }

    #endregion
}
