using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

// On-screen controls for phones and tablets. The left side of the screen is a floating joystick
// (push it far forward to sprint), dragging anywhere else turns the view, and buttons on the right
// shoot, aim, jump, crouch/slide, reload and switch weapons. Dragging on the fire button also turns
// the view, so you can shoot and aim at the same time with one thumb.
// GameInput reads this alongside the keyboard, mouse and gamepad, so gameplay code doesn't change.
// A keyboard always works too. Using a mouse or trackpad (on a tablet) hides the on-screen controls
// and plays like a computer; touching the screen brings them back.
// Built at runtime, so no scene or prefab changes are needed.
[DefaultExecutionOrder(-500)] // Before Player, Weapon and Look read the input each frame
public class TouchControls : MonoBehaviour
{
    // Whether this device should get touch controls: phones and tablets in a browser. In the editor,
    // only while touch is simulated (Input Debugger → Options → Simulate Touch Input From Mouse).
    // Desktop browsers, touchscreen laptops and the Windows build keep mouse and keyboard.
    public static bool Enabled
    {
        get
        {
            if (Browser.IsTouchDevice) return true;
#if UNITY_EDITOR
            // Only when touch is simulated on purpose, so a touchscreen laptop keeps mouse and keyboard
            return TouchSimulation.instance != null && TouchSimulation.instance.enabled;
#else
            return false;
#endif
        }
    }

    // True while the on-screen controls are in use; false when playing with a mouse or trackpad instead
    public static bool UsingTouch => instance != null && instance.usingTouch;

    // Movement in -1..1, and view movement this frame in the same units as GameInput.MouseLookValue
    public static Vector2 MoveValue { get; private set; }
    public static Vector2 LookDelta { get; private set; }

    // How much a drag turns the view, per 1080th of the screen height. With the default sensitivity,
    // a drag across half the screen height turns about 90 degrees.
    private const float LookScale = 0.07f;

    // Layout, in a 1080-pixel-tall screen; positions are from the given screen corner
    private const float ReferenceHeight = 1080f;
    private const float StickRadius = 120f;
    private const float SprintDistance = 1.35f;   // pushed this many radii forward → sprint
    private static readonly Vector2 StickHome = new Vector2(260f, 280f);

    private class TouchButton
    {
        public string label;
        public InputAction[] actions;   // Pressing the button presses all of these
        public Vector2 anchor;          // Screen corner (0..1)
        public Vector2 offset;          // From that corner
        public float radius;
        public bool toggle;             // Tap to turn on, tap again to turn off
        public bool look;               // Dragging on it also turns the view
        public System.Action onPress;   // Instead of actions

        public bool held;
        public int pressFrame = -1, releaseFrame = -1;
        public Image image;
    }

    private enum Owner { Stick, Look, Button }

    private class Finger
    {
        public Owner owner;
        public TouchButton button;
        public Vector2 last;
    }

    private static TouchControls instance;
    private readonly List<TouchButton> buttons = new List<TouchButton>();
    private readonly Dictionary<int, Finger> fingers = new Dictionary<int, Finger>();
    private readonly HashSet<int> seen = new HashSet<int>();

    private Font font;
    private Sprite circle;
    private Canvas canvas;
    private RectTransform root;
    private RectTransform stickBase, stickKnob;
    private Image stickKnobImage;
    private int stickFinger = -1;
    private Vector2 stickOrigin;
    private bool sprinting;
    private TouchButton aimButton;
    private bool usingTouch;
    private int lastPointerType;

    private static readonly Color Idle = new Color(1f, 1f, 1f, 0.22f);
    private static readonly Color Down = new Color(1f, 1f, 1f, 0.5f);
    private static readonly Color On = new Color(1f, 0.8f, 0.2f, 0.55f);

    public static TouchControls Create(Font font)
    {
        if (!Enabled) return null;

        GameObject go = new GameObject("Touch Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1; // Under the match UI, so the pause menu and scoreboard cover it

        // 1 canvas unit = 1/1080 of the screen height, the same units the layout uses
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, ReferenceHeight);
        scaler.matchWidthOrHeight = 1f;

        TouchControls controls = go.AddComponent<TouchControls>();
        controls.font = font;
        controls.Build();
        return controls;
    }

    private void Awake()
    {
        instance = this;
        EnhancedTouchSupport.Enable();
        lastPointerType = Browser.LastPointerType;
        SetUsingTouch(lastPointerType != Browser.PointerMouse);
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        MoveValue = LookDelta = Vector2.zero;
        EnableMouse(true);
    }

    // Switches between the on-screen controls and mouse/trackpad play
    private void SetUsingTouch(bool on)
    {
        usingTouch = on;
        EnableMouse(!on);
        if (on)
        {
            // Let go of the mouse lock from mouse play; touch play never locks it
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // Mobile browsers also report taps as mouse clicks, which would shoot or turn the view on every
    // tap, so the mouse is switched off while the on-screen controls are in use. Real mouse or
    // trackpad use is still noticed, through the browser (Browser.LastPointerType).
    // Not in the editor, where simulated touches are made from the mouse.
    private static void EnableMouse(bool enable)
    {
        if (!Browser.IsBrowser) return;
        Mouse mouse = Mouse.current;
        if (mouse == null || mouse.enabled == enable) return;
        if (enable) InputSystem.EnableDevice(mouse);
        else InputSystem.DisableDevice(mouse);
    }

    private void UpdateMode()
    {
        // The browser says which kind of pointer was used last: a mouse or trackpad, or a finger or pen
        int pointer = Browser.LastPointerType;
        if (pointer != lastPointerType)
        {
            lastPointerType = pointer;
            if (pointer == Browser.PointerMouse) SetUsingTouch(false);
            else if (pointer == Browser.PointerTouch) SetUsingTouch(true);
        }

        if (!usingTouch)
        {
            foreach (Touch touch in Touch.activeTouches)
            {
                if (touch.phase == TouchPhase.Began) { SetUsingTouch(true); break; }
            }
        }

        // The mouse device can appear after the switch, on its first event
        if (usingTouch) EnableMouse(false);
    }

    #region Read by GameInput

    public static bool IsHeld(InputAction action)
    {
        // Sprint comes from pushing the joystick far forward rather than from a button
        if (instance != null && action == GameInput.Sprint && instance.sprinting) return true;
        return Find(action, b => b.held);
    }

    public static bool WasPressed(InputAction action) => Find(action, b => b.pressFrame == Time.frameCount);
    public static bool WasReleased(InputAction action) => Find(action, b => b.releaseFrame == Time.frameCount);

    private static bool Find(InputAction action, System.Func<TouchButton, bool> test)
    {
        if (instance == null) return false;
        foreach (TouchButton b in instance.buttons)
        {
            if (b.actions != null && System.Array.IndexOf(b.actions, action) >= 0 && test(b)) return true;
        }
        return false;
    }

    #endregion

    #region Building

    private void Build()
    {
        root = (RectTransform)transform;
        canvas = GetComponent<Canvas>();
        circle = MakeCircle();

        // Right thumb cluster
        AddButton("FIRE", new[] { GameInput.Fire }, new Vector2(1f, 0f), new Vector2(-250f, 340f), 105f).look = true;
        aimButton = AddButton("AIM", new[] { GameInput.Aim }, new Vector2(1f, 0f), new Vector2(-460f, 290f), 62f);
        aimButton.toggle = true;
        AddButton("JUMP", new[] { GameInput.Jump }, new Vector2(1f, 0f), new Vector2(-120f, 170f), 70f);
        // Crouches when standing, slides when sprinting (the player only does the one that fits)
        AddButton("CROUCH", new[] { GameInput.Crouch, GameInput.Slide }, new Vector2(1f, 0f), new Vector2(-310f, 140f), 60f);
        AddButton("RELOAD", new[] { GameInput.Reload }, new Vector2(1f, 0f), new Vector2(-120f, 510f), 55f);
        AddButton("SWAP", new[] { GameInput.WeaponNext }, new Vector2(1f, 0f), new Vector2(-440f, 480f), 55f);
        AddButton("NADE", new[] { GameInput.Grenade }, new Vector2(1f, 0f), new Vector2(-280f, 560f), 50f);

        // Top middle, clear of the kill feed (top right) and the player card (top left)
        AddButton("II", null, new Vector2(0.5f, 1f), new Vector2(70f, -70f), 45f).onPress = OpenPauseMenu;
        AddButton("SCORE", new[] { GameInput.Scoreboard }, new Vector2(0.5f, 1f), new Vector2(-70f, -70f), 45f);

        // Joystick: rests in the lower left and follows the thumb to wherever it lands
        stickBase = NewCircle("Stick", root, StickRadius, new Color(1f, 1f, 1f, 0.12f));
        stickKnob = NewCircle("Knob", stickBase, StickRadius * 0.45f, Idle);
        stickKnobImage = stickKnob.GetComponent<Image>();
        ResetStick();
    }

    private TouchButton AddButton(string label, InputAction[] actions, Vector2 anchor, Vector2 offset, float radius)
    {
        TouchButton b = new TouchButton { label = label, actions = actions, anchor = anchor, offset = offset, radius = radius };

        RectTransform rect = NewCircle(label, root, radius, Idle);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = offset;
        b.image = rect.GetComponent<Image>();

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        Text text = textObject.GetComponent<Text>();
        text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = label;
        text.fontSize = Mathf.RoundToInt(Mathf.Clamp(radius * 0.4f, 18f, 30f));
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 1f, 1f, 0.85f);
        text.raycastTarget = false;

        buttons.Add(b);
        return b;
    }

    private RectTransform NewCircle(string name, Transform parent, float radius, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(radius * 2f, radius * 2f);
        Image image = go.GetComponent<Image>();
        image.sprite = circle;
        image.color = color;
        // Touches are read directly, so the UI event system never needs to see these
        image.raycastTarget = false;
        return rect;
    }

    // A filled circle with a brighter rim
    private static Sprite MakeCircle()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float edge = Mathf.Clamp01(r - d);                     // Anti-aliased outer edge
                float rim = Mathf.Clamp01(1f - Mathf.Abs(d - (r - 4f)) / 3f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, edge * Mathf.Max(0.45f, rim)));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    #endregion

    #region Touch handling

    private void Update()
    {
        LookDelta = Vector2.zero;

        UpdateMode();
        canvas.enabled = usingTouch && !Pause.paused;
        if (!usingTouch || GameInput.Blocked)
        {
            ReleaseAll();
            return;
        }

        float unit = Screen.height / ReferenceHeight;  // Screen pixels per layout unit
        seen.Clear();

        foreach (Touch touch in Touch.activeTouches)
        {
            int id = touch.touchId;
            Vector2 pos = touch.screenPosition;
            seen.Add(id);

            Finger finger;
            if (!fingers.TryGetValue(id, out finger))
            {
                // Only new touches are picked up, so the tap that closed the pause menu doesn't shoot
                if (touch.phase != TouchPhase.Began) continue;
                finger = Assign(id, pos, unit);
                fingers[id] = finger;
            }

            Vector2 delta = pos - finger.last;
            finger.last = pos;

            if (finger.owner == Owner.Stick) MoveStick(pos, unit);
            else if (finger.owner == Owner.Look || (finger.button != null && finger.button.look))
            {
                LookDelta += delta / unit * LookScale;
            }

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) Release(id);
        }

        // Touches the browser dropped without an end event
        if (fingers.Count > seen.Count)
        {
            List<int> gone = new List<int>();
            foreach (int id in fingers.Keys) if (!seen.Contains(id)) gone.Add(id);
            foreach (int id in gone) Release(id);
        }

        // Aim is turned off when changing weapon, since the new weapon starts unscoped
        if (aimButton.held && WasPressed(GameInput.WeaponNext)) SetToggle(aimButton, false);
    }

    private Finger Assign(int id, Vector2 pos, float unit)
    {
        Finger finger = new Finger { last = pos, owner = Owner.Look };

        foreach (TouchButton b in buttons)
        {
            Vector2 center = new Vector2(b.anchor.x * Screen.width, b.anchor.y * Screen.height) + b.offset * unit;
            // A little larger than drawn, so near misses still count
            if (Vector2.Distance(pos, center) <= b.radius * unit * 1.15f)
            {
                finger.owner = Owner.Button;
                finger.button = b;
                Press(b);
                return finger;
            }
        }

        // Lower-left area starts the joystick
        if (stickFinger < 0 && pos.x < Screen.width * 0.4f && pos.y < Screen.height * 0.75f)
        {
            finger.owner = Owner.Stick;
            stickFinger = id;
            stickOrigin = pos;
            stickBase.anchoredPosition = pos / unit;
            MoveStick(pos, unit);
        }

        return finger;
    }

    private void MoveStick(Vector2 pos, float unit)
    {
        Vector2 offset = (pos - stickOrigin) / unit;   // In layout units
        Vector2 clamped = Vector2.ClampMagnitude(offset, StickRadius);
        stickKnob.anchoredPosition = clamped;

        Vector2 move = clamped / StickRadius;
        // A small dead zone, so resting the thumb doesn't creep
        MoveValue = move.magnitude < 0.12f ? Vector2.zero : move;

        // Pushing well past the rim, mostly forward, sprints
        sprinting = offset.y > StickRadius * SprintDistance && Mathf.Abs(offset.x) < offset.y;
        stickKnobImage.color = sprinting ? On : Down;
    }

    private void ResetStick()
    {
        stickFinger = -1;
        sprinting = false;
        MoveValue = Vector2.zero;
        stickBase.anchorMin = stickBase.anchorMax = Vector2.zero;
        stickBase.anchoredPosition = StickHome;
        stickKnob.anchoredPosition = Vector2.zero;
        stickKnobImage.color = Idle;
    }

    private void Press(TouchButton b)
    {
        if (b.onPress != null)
        {
            b.onPress();
            return;
        }

        if (b.toggle)
        {
            SetToggle(b, !b.held);
            return;
        }

        b.held = true;
        b.pressFrame = Time.frameCount;
        b.image.color = Down;
    }

    private void SetToggle(TouchButton b, bool on)
    {
        b.held = on;
        if (on) b.pressFrame = Time.frameCount;
        else b.releaseFrame = Time.frameCount;
        b.image.color = on ? On : Idle;
    }

    private void Release(int id)
    {
        Finger finger;
        if (!fingers.TryGetValue(id, out finger)) return;
        fingers.Remove(id);

        if (finger.owner == Owner.Stick && stickFinger == id) ResetStick();

        TouchButton b = finger.button;
        if (b != null && !b.toggle && b.onPress == null)
        {
            b.held = false;
            b.releaseFrame = Time.frameCount;
            b.image.color = Idle;
        }
    }

    private void ReleaseAll()
    {
        List<int> ids = new List<int>(fingers.Keys);
        foreach (int id in ids) Release(id);
        foreach (TouchButton b in buttons) if (b.toggle && b.held) SetToggle(b, false);
        ResetStick();
    }

    private static void OpenPauseMenu()
    {
        Pause pause = FindAnyObjectByType<Pause>();
        if (pause != null && !Pause.paused) pause.TogglePause();
    }

    #endregion
}
