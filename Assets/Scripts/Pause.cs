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
        AddLobbyButtons();

        // The pause menu lives in the match scene, so this covers exactly the time spent in a match
        Browser.SetLeaveWarning(true);
    }

    // While the local player is alive, Player opens and closes this menu (and watches the browser's
    // mouse lock). While dead (the death camera, spectating, the map view) there's no Player, so
    // the menu listens here instead. It works on the end screens too (awards, results and the vote),
    // so anyone can leave there without waiting for them to finish.
    private void Update()
    {
        if (paused) RefreshLobbyButtons();

        bool pointerLocked = Browser.IsPointerLocked;
        bool lockLost = wasPointerLocked && !pointerLocked;
        wasPointerLocked = pointerLocked;

        if (Player.Local != null || disconnecting) return;
        if (manager == null) manager = FindAnyObjectByType<Manager>();
        bool ended = MatchEnded;

        // On the end screens the mouse is free, so in a browser Escape reaches the game too
        bool pressed = ended
            ? !GameInput.IsRebinding && !ChatUI.IsTyping && ChatUI.ClosedFrame != Time.frameCount && GameInput.PauseMenu.WasPressedThisFrame()
            : GameInput.PausePressed;
        if (pressed)
        {
            TogglePause();
            return;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        // The browser let go of the mouse (Escape, or another tab): pause, as Player does while alive.
        // Not on the end screens, which free the mouse themselves.
        if (!ended && lockLost && !paused && !TouchControls.UsingTouch) TogglePause();
#endif
    }

    private bool MatchEnded => manager != null && (manager.Ending || manager.MatchOver);

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
        AddInviteButton(buttons, text.transform.GetSiblingIndex() + 1, code);
    }

    // Under the room code: copies a link that opens the browser version straight into this match
    private void AddInviteButton(Transform buttons, int index, string code)
    {
        UnityEngine.UI.Text label;
        UnityEngine.UI.Button button = CopyMenuButton(buttons, "Invite", index, out label);
        if (button == null) return;
        const string idle = "COPY INVITE LINK";
        label.text = idle;
        button.onClick.AddListener(() =>
        {
            Browser.CopyText(Browser.InviteLink(code));
            label.text = "LINK COPIED";
            CancelInvoke(nameof(ResetInviteLabel));
            inviteLabel = label;
            Invoke(nameof(ResetInviteLabel), 2f);
        });
    }

    // A secondary button in the menu's column, made from Resume (without Resume's own action)
    private UnityEngine.UI.Button CopyMenuButton(Transform buttons, string name, int index, out UnityEngine.UI.Text label)
    {
        label = null;
        Transform resume = buttons != null ? buttons.Find("Button (1)") : null;
        if (resume == null) return null;
        GameObject copy = Instantiate(resume.gameObject, buttons);
        copy.name = name;
        copy.transform.SetSiblingIndex(index);
        UnityEngine.UI.Button button = copy.GetComponent<UnityEngine.UI.Button>();
        label = copy.GetComponentInChildren<UnityEngine.UI.Text>(true);
        if (button == null || label == null) { Destroy(copy); return null; }
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
        UITheme.StyleButton(button, UITheme.ButtonStyle.Secondary, 18);
        return button;
    }

    // In a lobby (Manager.Lobby.cs), right under Resume: Ready for everyone, and for the host a
    // button that starts the match whoever is ready. Shown only while the lobby is on.
    private GameObject readyButton, startButton;
    private UnityEngine.UI.Text readyLabel;

    private void AddLobbyButtons()
    {
        if (transform.childCount == 0) return;
        Transform buttons = transform.GetChild(0).Find("Buttons");
        Transform resume = buttons != null ? buttons.Find("Button (1)") : null;
        if (resume == null) return;

        UnityEngine.UI.Text startLabel;
        UnityEngine.UI.Button start = CopyMenuButton(buttons, "Start Match", resume.GetSiblingIndex() + 1, out startLabel);
        if (start != null)
        {
            UITheme.StyleButton(start, UITheme.ButtonStyle.Primary, 18);
            startLabel.text = "START MATCH NOW";
            start.onClick.AddListener(() =>
            {
                if (Manager.Instance != null) Manager.Instance.ForceStart();
                if (paused) TogglePause();
            });
            startButton = start.gameObject;
        }

        UnityEngine.UI.Button ready = CopyMenuButton(buttons, "Ready", resume.GetSiblingIndex() + 1, out readyLabel);
        if (ready != null)
        {
            ready.onClick.AddListener(() =>
            {
                if (Manager.Instance != null) Manager.Instance.ToggleReady();
            });
            readyButton = ready.gameObject;
        }
        RefreshLobbyButtons();
    }

    private void RefreshLobbyButtons()
    {
        Manager m = Manager.Instance;
        bool inLobby = m != null && m.InLobby;
        if (readyButton != null)
        {
            if (readyButton.activeSelf != inLobby) readyButton.SetActive(inLobby);
            if (inLobby && readyLabel != null) readyLabel.text = m.LocalReady ? "CANCEL READY" : "READY UP";
        }
        bool canStart = inLobby && m.CanForceStart;
        if (startButton != null && startButton.activeSelf != canStart) startButton.SetActive(canStart);
    }

    private UnityEngine.UI.Text inviteLabel;
    private void ResetInviteLabel()
    {
        if (inviteLabel != null) inviteLabel.text = "COPY INVITE LINK";
    }

    public void TogglePause()
    {
        if (disconnecting || toggledFrame == Time.frameCount) return;
        toggledFrame = Time.frameCount;

        paused = !paused;
        if (paused) RefreshLobbyButtons();

        transform.GetChild(0).gameObject.SetActive(paused);
        // Over everything else on the screen, the end screens included
        if (paused) transform.SetAsLastSibling();
        // Resuming while dead: nothing else locks the mouse again until the next spawn (Look does it
        // while alive), so lock it here, and in a browser the next click takes it back
        // On the end screens the mouse stays free, for the vote and its buttons.
        if (manager == null) manager = FindAnyObjectByType<Manager>();
        bool ended = MatchEnded && Player.Local == null;
        CursorLockMode resumed = ended ? CursorLockMode.None
            : Player.Local == null && !TouchControls.UsingTouch ? CursorLockMode.Locked : CursorLockMode.Confined;
        Cursor.lockState = (paused) ? CursorLockMode.None : resumed;
        Cursor.visible = paused || (ended && !TouchControls.UsingTouch);
    }

    public void Quit()
    {
        disconnecting = true;
        // Shuts the runner down, which then loads the main menu
        NetworkSession.Leave();
    }


}
