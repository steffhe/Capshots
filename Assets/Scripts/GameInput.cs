using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// All gameplay input, on Unity's Input System. Actions are created in code with keyboard/mouse
// and gamepad bindings, so there's no input asset to set up. Keyboard/mouse bindings can be
// changed in the settings menu; the changes are saved in PlayerPrefs. On phones and tablets the
// on-screen TouchControls are read here too.
public static class GameInput
{
    public const string KeyboardMouseGroup = "KeyboardMouse";
    public const string GamepadGroup = "Gamepad";
    private const string OverridesKey = "input.bindings";

    // Mouse delta is in pixels. The old input axes scaled it by 0.1, so keep that to make
    // existing sensitivity values feel the same.
    public const float MouseScale = 0.1f;

    public static InputActionMap Map { get; private set; }

    public static InputAction Move, Look, LookStick, Fire, Aim, Jump, Sprint, Crouch, Slide, Reload, Grenade;
    public static InputAction Weapon1, Weapon2, Weapon3, Weapon4, WeaponNext, WeaponPrev, WeaponScroll;
    public static InputAction Scoreboard, Chat, PauseMenu, Ready;

    // Actions shown in the settings menu for rebinding, with their display names
    public static readonly List<KeyValuePair<string, InputAction>> Rebindable = new List<KeyValuePair<string, InputAction>>();

    private static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        Map = new InputActionMap("Gameplay");

        Move = Map.AddAction("Move", InputActionType.Value);
        Move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w", KeyboardMouseGroup)
            .With("Down", "<Keyboard>/s", KeyboardMouseGroup)
            .With("Left", "<Keyboard>/a", KeyboardMouseGroup)
            .With("Right", "<Keyboard>/d", KeyboardMouseGroup);
        Move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow", KeyboardMouseGroup)
            .With("Down", "<Keyboard>/downArrow", KeyboardMouseGroup)
            .With("Left", "<Keyboard>/leftArrow", KeyboardMouseGroup)
            .With("Right", "<Keyboard>/rightArrow", KeyboardMouseGroup);
        Move.AddBinding("<Gamepad>/leftStick", groups: GamepadGroup);

        Look = Map.AddAction("Look", InputActionType.Value);
        Look.AddBinding("<Mouse>/delta", groups: KeyboardMouseGroup);
        LookStick = Map.AddAction("LookStick", InputActionType.Value);
        LookStick.AddBinding("<Gamepad>/rightStick", groups: GamepadGroup);

        Fire = Button("Fire", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
        Aim = Button("Aim", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
        Jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
        Sprint = Button("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
        Sprint.AddBinding("<Keyboard>/rightShift", groups: KeyboardMouseGroup);
        // In a browser, Ctrl+W closes the tab and a web page can't stop it, so crouch isn't on Ctrl there
        Crouch = Button("Crouch", Browser.IsBrowser ? "<Keyboard>/z" : "<Keyboard>/leftCtrl", "<Gamepad>/buttonEast");
        Slide = Button("Slide", "<Keyboard>/c", "<Gamepad>/rightStickPress");
        Reload = Button("Reload", "<Keyboard>/r", "<Gamepad>/buttonWest");
        Grenade = Button("Grenade", "<Keyboard>/g", "<Gamepad>/buttonNorth");
        Weapon1 = Button("Weapon1", "<Keyboard>/1", null);
        Weapon2 = Button("Weapon2", "<Keyboard>/2", null);
        Weapon3 = Button("Weapon3", "<Keyboard>/3", null);
        Weapon4 = Button("Weapon4", "<Keyboard>/4", null);
        WeaponNext = Button("WeaponNext", null, "<Gamepad>/rightShoulder");
        WeaponPrev = Button("WeaponPrev", null, "<Gamepad>/leftShoulder");
        WeaponScroll = Map.AddAction("WeaponScroll", InputActionType.Value);
        WeaponScroll.AddBinding("<Mouse>/scroll/y", groups: KeyboardMouseGroup);
        Scoreboard = Button("Scoreboard", "<Keyboard>/tab", "<Gamepad>/select");
        Chat = Button("Chat", "<Keyboard>/t", null);
        Chat.AddBinding("<Keyboard>/enter", groups: KeyboardMouseGroup);
        PauseMenu = Button("Pause", "<Keyboard>/escape", "<Gamepad>/start");
        // In a created room's lobby: ready to start, or not after all (Manager.Lobby.cs)
        Ready = Button("Ready", "<Keyboard>/f", "<Gamepad>/dpad/up");

        Rebindable.Add(new KeyValuePair<string, InputAction>("FIRE", Fire));
        Rebindable.Add(new KeyValuePair<string, InputAction>("AIM", Aim));
        Rebindable.Add(new KeyValuePair<string, InputAction>("RELOAD", Reload));
        Rebindable.Add(new KeyValuePair<string, InputAction>("GRENADE", Grenade));
        Rebindable.Add(new KeyValuePair<string, InputAction>("JUMP", Jump));
        Rebindable.Add(new KeyValuePair<string, InputAction>("SPRINT", Sprint));
        Rebindable.Add(new KeyValuePair<string, InputAction>("CROUCH", Crouch));
        Rebindable.Add(new KeyValuePair<string, InputAction>("SLIDE", Slide));
        Rebindable.Add(new KeyValuePair<string, InputAction>("WEAPON 1", Weapon1));
        Rebindable.Add(new KeyValuePair<string, InputAction>("WEAPON 2", Weapon2));
        Rebindable.Add(new KeyValuePair<string, InputAction>("WEAPON 3", Weapon3));
        Rebindable.Add(new KeyValuePair<string, InputAction>("WEAPON 4", Weapon4));
        Rebindable.Add(new KeyValuePair<string, InputAction>("SCOREBOARD", Scoreboard));
        Rebindable.Add(new KeyValuePair<string, InputAction>("CHAT", Chat));
        Rebindable.Add(new KeyValuePair<string, InputAction>("READY (LOBBY)", Ready));

        LoadBindings();
        Map.Enable();
    }

    private static InputAction Button(string name, string keyboardMouse, string gamepad)
    {
        InputAction action = Map.AddAction(name, InputActionType.Button);
        if (keyboardMouse != null) action.AddBinding(keyboardMouse, groups: KeyboardMouseGroup);
        if (gamepad != null) action.AddBinding(gamepad, groups: GamepadGroup);
        return action;
    }

    #region Reading input

    // Gameplay input is ignored while paused, typing in chat or changing a key binding
    public static bool Blocked => Pause.paused || ChatUI.IsTyping || IsRebinding;
    public static bool IsRebinding { get; set; }

    public static Vector2 MoveValue => Blocked ? Vector2.zero : Vector2.ClampMagnitude(Move.ReadValue<Vector2>() + TouchControls.MoveValue, 1f);

    // Mouse (or touch drag) look in the same units as the old "Mouse X"/"Mouse Y" axes
    public static Vector2 MouseLookValue => Blocked ? Vector2.zero : Look.ReadValue<Vector2>() * MouseScale + TouchControls.LookDelta;
    public static Vector2 StickLookValue => Blocked ? Vector2.zero : LookStick.ReadValue<Vector2>();

    public static bool Held(InputAction action) => !Blocked && (action.IsPressed() || TouchControls.IsHeld(action));
    public static bool Pressed(InputAction action) => !Blocked && (action.WasPressedThisFrame() || TouchControls.WasPressed(action));
    public static bool Released(InputAction action) => action.WasReleasedThisFrame() || TouchControls.WasReleased(action);

    // Pause works while paused (to unpause), but not while typing in chat: there Escape closes the chat
    public static bool PausePressed
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // In a browser, Escape always releases the mouse, and the game pauses when that happens
            // (see Player.PauseWhenBrowserReleasesMouse). Here Escape only resumes, so the two can't fight.
            // With touch controls nothing locks the mouse, so there Escape on a keyboard pauses too.
            if (!Pause.paused && !TouchControls.UsingTouch) return false;
#endif
            return !IsRebinding && !ChatUI.IsTyping && ChatUI.ClosedFrame != Time.frameCount && PauseMenu.WasPressedThisFrame();
        }
    }

    // +1 next weapon, -1 previous, 0 none
    public static int WeaponSwitch
    {
        get
        {
            if (Blocked) return 0;
            float scroll = WeaponScroll.ReadValue<float>();
            if (scroll > 0f || WeaponNext.WasPressedThisFrame() || TouchControls.WasPressed(WeaponNext)) return 1;
            if (scroll < 0f || WeaponPrev.WasPressedThisFrame()) return -1;
            return 0;
        }
    }

    #endregion

    #region Bindings

    // The binding shown in the settings menu: the first keyboard/mouse one
    public static int KeyboardBindingIndex(InputAction action)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding b = action.bindings[i];
            if (!b.isComposite && !b.isPartOfComposite && b.groups != null && b.groups.Contains(KeyboardMouseGroup)) return i;
        }
        return -1;
    }

    // Indices of the WASD composite parts of Move, in up/down/left/right order
    public static int[] MovePartIndices()
    {
        List<int> parts = new List<int>();
        for (int i = 0; i < Move.bindings.Count && parts.Count < 4; i++)
        {
            if (Move.bindings[i].isPartOfComposite) parts.Add(i);
        }
        return parts.ToArray();
    }

    public static string BindingText(InputAction action, int bindingIndex)
    {
        if (bindingIndex < 0) return "-";
        return action.GetBindingDisplayString(bindingIndex).ToUpper();
    }

    public static void SaveBindings()
    {
        PlayerPrefs.SetString(OverridesKey, Map.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    private static void LoadBindings()
    {
        string json = PlayerPrefs.GetString(OverridesKey, "");
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            Map.LoadBindingOverridesFromJson(json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not load key bindings: " + e.Message);
        }
    }

    public static void ResetBindings()
    {
        Map.RemoveAllBindingOverrides();
        SaveBindings();
    }

    // Waits for a key or mouse button and binds it. Escape cancels.
    public static void StartRebind(InputAction action, int bindingIndex, System.Action onDone)
    {
        IsRebinding = true;
        Map.Disable();

        action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsHavingToMatchPath("<Keyboard>")
            .WithControlsHavingToMatchPath("<Mouse>")
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(operation => FinishRebind(operation, onDone))
            .OnCancel(operation => FinishRebind(operation, onDone))
            .Start();
    }

    private static void FinishRebind(InputActionRebindingExtensions.RebindingOperation operation, System.Action onDone)
    {
        operation.Dispose();
        Map.Enable();
        SaveBindings();
        IsRebinding = false;
        if (onDone != null) onDone();
    }

    #endregion
}
