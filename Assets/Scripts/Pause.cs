using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Pause : MonoBehaviour
{
    public static bool paused = false;
    private bool disconnecting = false;

    private void Awake()
    {
        // "paused" is static, so it would otherwise still be true after quitting a match from the pause menu
        paused = false;
    }

    private void Start()
    {
        // Restyle before the settings panel is built, since it copies the pause menu's controls
        UITheme.StylePause(transform);
        // Adds the Settings button and panel to this pause menu
        SettingsMenu.Create(this);

        // The pause menu lives in the match scene, so this covers exactly the time spent in a match
        Browser.SetLeaveWarning(true);
    }

    private void OnDestroy()
    {
        Browser.SetLeaveWarning(false);
    }

    public void TogglePause()
    {
        if (disconnecting) return;

        paused = !paused;

        transform.GetChild(0).gameObject.SetActive(paused);
        Cursor.lockState = (paused) ? CursorLockMode.None : CursorLockMode.Confined;
        Cursor.visible = paused;
    }

    public void Quit()
    {
        disconnecting = true;
        // Shuts the runner down, which then loads the main menu
        NetworkSession.Leave();
    }


}
