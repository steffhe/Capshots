using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Browser builds: a controller read straight from the browser's Gamepad API, instead of through
// Unity's own WebGL gamepad support, which didn't pass a controller on (an Xbox pad in Chrome did
// nothing in a match). Each frame the controller in use, if it has the browser's "standard" layout
// (Xbox and PlayStation pads in Chrome, Edge and most others, and every controller in Chrome on
// Android, such as a Backbone), is copied into a gamepad of our own, so GameInput's <Gamepad>
// bindings work as on Windows. Unity's own WebGL gamepads are switched off while ours is there,
// so nothing is read twice.
//
// Phones often list other devices as controllers too (a fingerprint sensor, say), so the one in
// use is the last one a button was pressed on (see ReadBrowserGamepad in Browser.jslib).
//
// Browsers only show a controller to the page after a button on it is pressed with the page open.
// What was found is written to the browser's console (F12) whenever it changes, and shown on
// screen for a few seconds, since a phone has no console to look at.
[DefaultExecutionOrder(-1000)]
public class BrowserGamepad : MonoBehaviour
{
    private const int Axes = 4, Buttons = 17;

    // The browser's "standard" button order, as Input System buttons (the triggers, 6 and 7, are analog)
    private static readonly GamepadButton?[] StandardButtons =
    {
        GamepadButton.South, GamepadButton.East, GamepadButton.West, GamepadButton.North,
        GamepadButton.LeftShoulder, GamepadButton.RightShoulder, null, null,
        GamepadButton.Select, GamepadButton.Start, GamepadButton.LeftStick, GamepadButton.RightStick,
        GamepadButton.DpadUp, GamepadButton.DpadDown, GamepadButton.DpadLeft, GamepadButton.DpadRight,
        null, // The Xbox / PS button: the browser or the system uses it
    };

    private readonly float[] values = new float[Axes + Buttons];
    private Gamepad pad;
    private GamepadState lastSent;
    private bool sentAny;
    private int lastFound = -1, lastIndex = -2;
    private Canvas noticeCanvas;
    private Text notice;
    private float noticeUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (!Browser.IsBrowser) return;
        GameObject go = new GameObject("Browser Gamepad");
        DontDestroyOnLoad(go);
        go.AddComponent<BrowserGamepad>();
    }

    private void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        RemovePad();
    }

    private void Update()
    {
        int found = Browser.ReadGamepad(values);
        int index = Browser.GamepadIndex;
        if (found != lastFound || index != lastIndex)
        {
            bool had = lastFound > Browser.GamepadNone;
            lastFound = found;
            lastIndex = index;
            Report(found, had);
        }
        if (noticeCanvas != null && noticeCanvas.enabled && Time.unscaledTime > noticeUntil) noticeCanvas.enabled = false;

        if (found != Browser.GamepadStandard)
        {
            // No controller, or one without the standard layout: Unity's own support has it (as a joystick)
            RemovePad();
            return;
        }

        if (pad == null)
        {
            pad = InputSystem.AddDevice<Gamepad>("BrowserGamepad");
            sentAny = false;
            SwitchUnityGamepads(false);
        }

        GamepadState state = new GamepadState
        {
            // The browser's sticks point down for positive y; the Input System's point up
            leftStick = new Vector2(values[0], -values[1]),
            rightStick = new Vector2(values[2], -values[3]),
            leftTrigger = values[Axes + 6],
            rightTrigger = values[Axes + 7],
        };
        for (int i = 0; i < Buttons; i++)
        {
            GamepadButton? button = StandardButtons[i];
            if (button.HasValue && values[Axes + i] > 0.5f) state = state.WithButton(button.Value);
        }

        // Only changes are sent: a controller lying still mustn't count as the device in use
        // (ControlsHint shows the gamepad's controls when it was used last)
        if (sentAny && Same(state, lastSent)) return;
        InputSystem.QueueStateEvent(pad, state);
        lastSent = state;
        sentAny = true;
    }

    private static bool Same(GamepadState a, GamepadState b)
    {
        return a.buttons == b.buttons && a.leftStick == b.leftStick && a.rightStick == b.rightStick
            && a.leftTrigger == b.leftTrigger && a.rightTrigger == b.rightTrigger;
    }

    private void RemovePad()
    {
        if (pad == null) return;
        if (pad.added) InputSystem.RemoveDevice(pad);
        pad = null;
        SwitchUnityGamepads(true);
    }

    // Unity's own WebGL gamepads and joysticks: off while ours reads the controller
    private void SwitchUnityGamepads(bool on)
    {
        foreach (InputDevice device in InputSystem.devices)
        {
            if (device == pad || !(device is Gamepad || device is Joystick)) continue;
            if (device.description.interfaceName != "WebGL" || device.enabled == on) continue;
            if (on) InputSystem.EnableDevice(device);
            else InputSystem.DisableDevice(device);
        }
    }

    // One Unity adds (or re-adds) while ours is in use is switched off too
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (pad == null || device == pad) return;
        if (change != InputDeviceChange.Added && change != InputDeviceChange.Reconnected && change != InputDeviceChange.Enabled) return;
        if ((device is Gamepad || device is Joystick) && device.description.interfaceName == "WebGL" && device.enabled)
            InputSystem.DisableDevice(device);
    }

    private void Report(int found, bool had)
    {
        if (found == Browser.GamepadNone)
        {
            Debug.Log("Controller: none found by the browser. Press a button on it with the game open.");
            if (had) ShowNotice("CONTROLLER DISCONNECTED");
            return;
        }
        string name = Browser.GamepadName;
        int count = Browser.GamepadCount;
        string others = count > 1 ? " The browser lists " + count + " controllers; press a button on the one to use." : "";
        if (found == Browser.GamepadStandard)
        {
            Debug.Log("Controller: " + name + ", read through the browser's standard layout." + others);
            ShowNotice("CONTROLLER: " + ShortName(name));
        }
        else
        {
            Debug.Log("Controller: " + name + ". It hasn't got the browser's standard layout, so Unity reads it as a joystick and its buttons may not match." + others);
            ShowNotice("CONTROLLER: " + ShortName(name) + "\n<size=16>NOT A STANDARD LAYOUT, SO IT MAY NOT WORK" + (count > 1 ? ". PRESS A BUTTON ON YOUR CONTROLLER" : "") + "</size>");
        }
    }

    // The browser's name for a controller, without the details in brackets (ids, mapping)
    private static string ShortName(string name)
    {
        int bracket = name.IndexOf('(');
        if (bracket > 0) name = name.Substring(0, bracket);
        name = name.Trim();
        return name.Length > 0 ? name.ToUpperInvariant() : "FOUND";
    }

    // A line at the top of the screen for a few seconds, over everything
    private void ShowNotice(string message)
    {
        if (noticeCanvas == null)
        {
            noticeCanvas = gameObject.AddComponent<Canvas>();
            noticeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            noticeCanvas.sortingOrder = 1002;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Image background = UITheme.NewImage("Notice", transform, UITheme.Panel, 10f);
            RectTransform rect = background.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -24f);
            rect.sizeDelta = new Vector2(760f, 72f);
            notice = UITheme.NewText("Text", rect, 22, UITheme.TextColor, TextAnchor.MiddleCenter);
            if (notice.font == null) notice.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        if (UITheme.Font != null) notice.font = UITheme.Font;
        notice.text = message;
        noticeCanvas.enabled = true;
        noticeUntil = Time.unscaledTime + 5f;
    }
}
