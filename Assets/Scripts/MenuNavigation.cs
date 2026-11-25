using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Controller support for every menu (the main menu and its tabs and panels, the pause menu, the
// option lists and the end-of-match vote): the stick or d-pad moves between buttons, A presses,
// B goes back. One of these lives for the whole game.
//
//  - Each scene's EventSystem is switched from the old StandaloneInputModule, which reads the old
//    input manager and never saw a controller (least of all the browser's, see BrowserGamepad), to
//    the Input System's UI module, which reads <Gamepad> like the rest of the game.
//  - When the controller is used and nothing is selected, the menu's first button is (the one a
//    panel asked for with Focus, else the highest one on screen), and a glowing frame shows which
//    button is selected. Using the mouse or a finger hides the frame again.
//  - B ("back") is handed out once a frame through TakeBack, so the option list, a panel and the
//    pause menu don't all close on the same press.
//  - Outside menus (playing a match) nothing stays selected, so A (jump) can't press a button
//    left selected in a menu that closed.
[DefaultExecutionOrder(-500)]
public class MenuNavigation : MonoBehaviour
{
    private static MenuNavigation instance;

    // True while the controller is what's being used in the menus (since its last press, until
    // the mouse or a finger is used)
    public static bool UsingGamepad { get; private set; }

    private static Selectable wanted;

    // The main menu's first choice (Quick Play): where a controller starts when nothing else is asked for
    public static Selectable Home { get; set; }
    private static int backTakenFrame = -1;

    private EventSystem upgraded;
    private RectTransform frame;
    private Canvas frameCanvas;
    private GameObject lastSelected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        GameObject go = new GameObject("Menu Navigation");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MenuNavigation>();
    }

    // Asks for this to be selected when the controller is in use (now, or when it's next used)
    public static void Focus(Selectable selectable)
    {
        if (selectable == null) return;
        wanted = selectable;
        if (UsingGamepad && EventSystem.current != null && Usable(selectable.gameObject))
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
    }

    // What's selected now (a button, slider...), or null: for a panel to go back to when it closes
    public static Selectable Current
    {
        get
        {
            EventSystem events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            return selected != null ? selected.GetComponent<Selectable>() : null;
        }
    }

    // The controller's B button was pressed this frame in a menu, and nothing else has answered it
    // yet. The first to ask gets it: anyone after that in the same frame gets false.
    public static bool TakeBack()
    {
        if (backTakenFrame == Time.frameCount || !BackPressed()) return false;
        backTakenFrame = Time.frameCount;
        return true;
    }

    // Whether B was pressed this frame and not yet taken, without taking it
    public static bool BackWaiting => backTakenFrame != Time.frameCount && BackPressed();

    private static bool BackPressed()
    {
        if (!InMenu) return false;
        foreach (Gamepad pad in Gamepad.all)
        {
            if (pad.enabled && pad.buttonEast.wasPressedThisFrame) return true;
        }
        return false;
    }

    // Whether a menu is up: the main menu, the pause menu, or the end-of-match screens
    public static bool InMenu
    {
        get
        {
            if (LoadingScreen.Visible) return false;
            if (Pause.paused) return true;
            Manager manager = Manager.Instance;
            if (manager != null) return manager.Ending || manager.MatchOver;
            return Launcher.Instance != null;
        }
    }

    private void Update()
    {
        EventSystem events = EventSystem.current;
        if (events != null && events != upgraded) Upgrade(events);

        if (GamepadUsed()) UsingGamepad = true;
        else if (PointerUsed() && UsingGamepad)
        {
            UsingGamepad = false;
            // Leave a text field being typed in alone; anything else selected loses its frame
            if (events != null && !IsTextField(events.currentSelectedGameObject)) events.SetSelectedGameObject(null);
        }

        if (events == null) { ShowFrame(null); return; }
        GameObject selected = events.currentSelectedGameObject;

        if (!InMenu)
        {
            if (selected != null && !IsTextField(selected)) events.SetSelectedGameObject(null);
            ShowFrame(null);
            return;
        }

        // (A text field being typed in stays selected: the controller's A on a field's edit button
        // starts typing in it, though fields themselves are left out of controller navigation)
        if (UsingGamepad && (selected == null || (!Usable(selected) && !(IsTextField(selected) && selected.activeInHierarchy))))
        {
            Selectable pick = wanted != null && Usable(wanted.gameObject) ? wanted
                : Home != null && Usable(Home.gameObject) ? Home : FirstOnScreen();
            selected = pick != null ? pick.gameObject : null;
            events.SetSelectedGameObject(selected);
        }
        if (selected != lastSelected)
        {
            lastSelected = selected;
            ScrollIntoView(selected);
        }
        ShowFrame(UsingGamepad ? selected : null);
    }

    // Swaps the scene's old UI input module for the Input System's, with its default actions
    // (stick and d-pad to move, A to press, B to cancel; mouse, touch and keyboard as before)
    private void Upgrade(EventSystem events)
    {
        upgraded = events;
        if (events.GetComponent<InputSystemUIInputModule>() != null) return;
        StandaloneInputModule old = events.GetComponent<StandaloneInputModule>();
        if (old != null) DestroyImmediate(old);
        InputSystemUIInputModule module = events.gameObject.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
        // Only the left stick and d-pad move between buttons: the right stick turns the drone in
        // Customize (see MenuScenery), and would otherwise also jump round the hat list
        // (The default actions bind it as its four directions, <Gamepad>/rightStick/up and so on, so
        // every binding starting with it is switched off.)
        InputAction navigate = module.move != null ? module.move.action : null;
        if (navigate != null)
        {
            for (int i = 0; i < navigate.bindings.Count; i++)
            {
                string path = navigate.bindings[i].path;
                if (path != null && path.StartsWith("<Gamepad>/rightStick", System.StringComparison.OrdinalIgnoreCase))
                {
                    navigate.ApplyBindingOverride(i, "");
                }
            }
        }
    }

    private static bool GamepadUsed()
    {
        foreach (Gamepad pad in Gamepad.all)
        {
            if (!pad.enabled) continue;
            // Buttons, and the sticks and d-pad pushed in a direction (each has up/down/left/right buttons)
            foreach (InputControl control in pad.allControls)
            {
                if (control is ButtonControl button && button.wasPressedThisFrame) return true;
            }
        }
        return false;
    }

    private static bool PointerUsed()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.enabled && (mouse.delta.ReadValue().sqrMagnitude > 4f || mouse.leftButton.wasPressedThisFrame)) return true;
        Touchscreen touch = Touchscreen.current;
        return touch != null && touch.primaryTouch.press.wasPressedThisFrame;
    }

    private static bool IsTextField(GameObject go) => go != null && go.GetComponent<InputField>() != null;

    private static bool Usable(GameObject go)
    {
        if (go == null || !go.activeInHierarchy) return false;
        Selectable selectable = go.GetComponent<Selectable>();
        return selectable != null && selectable.IsInteractable() && selectable.navigation.mode != Navigation.Mode.None;
    }

    // The button highest on the screen (then the leftmost) in the frontmost canvas: the first one
    // in a column or panel
    private static Selectable FirstOnScreen()
    {
        Selectable best = null;
        int bestOrder = int.MinValue;
        Vector2 bestPoint = Vector2.zero;
        foreach (Selectable s in Selectable.allSelectablesArray)
        {
            if (s == null || !Usable(s.gameObject)) continue;
            Canvas canvas = s.GetComponentInParent<Canvas>();
            int order = canvas != null ? canvas.rootCanvas.sortingOrder : 0;
            Vector2 point = ScreenCentre((RectTransform)s.transform, canvas);
            bool better = best == null || order > bestOrder
                || (order == bestOrder && (point.y > bestPoint.y + 1f || (Mathf.Abs(point.y - bestPoint.y) <= 1f && point.x < bestPoint.x)));
            if (!better) continue;
            best = s;
            bestOrder = order;
            bestPoint = point;
        }
        return best;
    }

    private static Vector2 ScreenCentre(RectTransform rect, Canvas canvas)
    {
        Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        return RectTransformUtility.WorldToScreenPoint(cam, rect.TransformPoint(rect.rect.center));
    }

    // A selected row in a scrolling list (the room list) is scrolled to
    private static void ScrollIntoView(GameObject selected)
    {
        if (selected == null) return;
        ScrollRect scroll = selected.GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.content == null || scroll.viewport == null) return;
        RectTransform item = (RectTransform)selected.transform;
        Bounds itemBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, item);
        Rect view = scroll.viewport.rect;
        float shift = 0f;
        if (itemBounds.max.y > view.yMax) shift = itemBounds.max.y - view.yMax;
        else if (itemBounds.min.y < view.yMin) shift = itemBounds.min.y - view.yMin;
        if (shift != 0f) scroll.content.anchoredPosition -= new Vector2(0f, shift);
    }

    #region Frame

    private const float FrameThickness = 3f, FramePadding = 4f;

    // A glowing outline round the selected button, over everything, in screen pixels
    private void ShowFrame(GameObject target)
    {
        if (target == null)
        {
            if (frameCanvas != null && frameCanvas.enabled) frameCanvas.enabled = false;
            return;
        }
        if (frame == null) BuildFrame();
        RectTransform rect = target.transform as RectTransform;
        if (rect == null) { frameCanvas.enabled = false; return; }

        Canvas canvas = target.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        float pad = FramePadding * frameCanvas.scaleFactor;
        frame.anchoredPosition = (min + max) * 0.5f / frameCanvas.scaleFactor;
        frame.sizeDelta = (max - min + Vector2.one * (pad * 2f)) / frameCanvas.scaleFactor;
        frameCanvas.enabled = true;
    }

    private void BuildFrame()
    {
        frameCanvas = gameObject.AddComponent<Canvas>();
        frameCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        frameCanvas.sortingOrder = 1001;

        GameObject go = new GameObject("Selection Frame", typeof(RectTransform));
        frame = (RectTransform)go.transform;
        frame.SetParent(transform, false);
        frame.anchorMin = frame.anchorMax = Vector2.zero;
        frame.pivot = new Vector2(0.5f, 0.5f);

        Color glow = UITheme.AccentText;
        // Four bars: top, bottom, left, right
        Edge("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, FrameThickness), glow);
        Edge("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, FrameThickness), glow);
        Edge("Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(FrameThickness, 0f), glow);
        Edge("Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(FrameThickness, 0f), glow);
    }

    private void Edge(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Color color)
    {
        Image edge = UITheme.NewImage(name, frame, color, 0f);
        RectTransform rect = edge.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    #endregion
}
