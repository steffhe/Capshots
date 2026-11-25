using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Pause : MonoBehaviour
{
    public static bool paused = false;
    private bool disconnecting = false;
    private Manager manager;
    // Browser builds: whether the browser had the mouse locked last frame (see Update)
    private bool wasPointerLocked;
    // The frame it last opened or closed: Player and Update both listen for Esc, and in the frame
    // the player dies both might hear the same press
    private int toggledFrame = -1;

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
        ShowRoomCode();

        // The pause menu lives in the match scene, so this covers exactly the time spent in a match
        Browser.SetLeaveWarning(true);
    }

    // While the local player is alive, Player opens and closes this menu (and watches the browser's
    // mouse lock). While dead (the death camera, spectating, the map view) there's no Player, so
    // the menu listens here instead, until the match is over and the results take the mouse.
    private void Update()
    {
        bool pointerLocked = Browser.IsPointerLocked;
        bool lockLost = wasPointerLocked && !pointerLocked;
        wasPointerLocked = pointerLocked;

        if (Player.Local != null || disconnecting) return;
        if (manager == null) manager = FindAnyObjectByType<Manager>();
        if (manager != null && (manager.Ending || manager.MatchOver)) return;

        if (GameInput.PausePressed)
        {
            TogglePause();
            return;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        // The browser let go of the mouse (Escape, or another tab): pause, as Player does while alive
        if (lockLost && !paused && !TouchControls.UsingTouch) TogglePause();
#endif
    }

    private void OnDestroy()
    {
        Browser.SetLeaveWarning(false);
    }

    // The match's room code under the pause menu's title, so it can be read out to friends. It's a
    // row of the menu's button column (laid out by its layout group), so it sits between the title
    // and the buttons whatever the screen's shape, and hides with them when Settings or Customize opens.
    private void ShowRoomCode()
    {
        string code = NetworkSession.RoomCode;
        if (code == null || transform.childCount == 0) return;
        Transform panel = transform.GetChild(0);
        Transform buttons = panel.Find("Buttons");
        Transform logo = buttons != null ? buttons.Find("Logo") : null;
        UnityEngine.UI.Text text = UITheme.NewText("Room Code", buttons != null ? buttons : panel, 22, UITheme.TextColor, TextAnchor.MiddleCenter);
        text.rectTransform.sizeDelta = new Vector2(300f, 36f);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        if (logo != null) text.transform.SetSiblingIndex(logo.GetSiblingIndex() + 1);
        else UITheme.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(800f, 32f));
        text.supportRichText = true;
        text.text = $"<color={UITheme.Hex(UITheme.MutedText)}>ROOM CODE</color>   {code}";
    }

    public void TogglePause()
    {
        if (disconnecting || toggledFrame == Time.frameCount) return;
        toggledFrame = Time.frameCount;

        paused = !paused;

        transform.GetChild(0).gameObject.SetActive(paused);
        // Resuming while dead: nothing else locks the mouse again until the next spawn (Look does it
        // while alive), so lock it here, and in a browser the next click takes it back
        CursorLockMode resumed = Player.Local == null && !TouchControls.UsingTouch ? CursorLockMode.Locked : CursorLockMode.Confined;
        Cursor.lockState = (paused) ? CursorLockMode.None : resumed;
        Cursor.visible = paused;
    }

    public void Quit()
    {
        disconnecting = true;
        // Shuts the runner down, which then loads the main menu
        NetworkSession.Leave();
    }


}
