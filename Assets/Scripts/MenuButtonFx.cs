using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A menu button's accent rail: a short bar on its left edge that slides in while the button is
// pointed at, or selected with a controller (where MenuNavigation's frame also shows), and out
// again after. UITheme.StyleButtonBackground adds it to every styled button. Local presentation only.
// The buttons have no per-frame code of their own (menus hold a hundred or so): a pointer or
// selection event hands the button to MenuButtonFxDriver, which animates only the buttons that are
// changing (and any selected one, see Step) and lets the rest go.
public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private Selectable selectable;
    private Image rail;
    private bool hovered, selected;
    private float shown = -1f;
    private Color railColor = Color.white;

    private const float RailWidth = 3f;
    private const float RailInset = 10f;

    // The rail's colour: white on a primary (accent) button, the accent otherwise, red for leaving
    public void SetColor(Color color)
    {
        Build();
        railColor = color;
        Color c = color;
        c.a = rail.color.a;
        rail.color = c;
    }

    private void Awake() => Build();

    private void Build()
    {
        if (rail != null) return;
        selectable = GetComponent<Selectable>();
        // A copy of a styled button (menus copy their buttons) already has a rail: reuse it
        Transform existing = transform.Find("Rail");
        if (existing != null && existing.GetComponent<Image>() != null)
        {
            rail = existing.GetComponent<Image>();
            rail.enabled = false;
            return;
        }
        rail = UITheme.NewImage("Rail", transform, railColor, 1f);
        LayoutElement ignore = rail.gameObject.AddComponent<LayoutElement>();
        ignore.ignoreLayout = true;
        RectTransform r = rail.rectTransform;
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 0.5f);
        r.offsetMin = new Vector2(0f, RailInset);
        r.offsetMax = new Vector2(RailWidth, -RailInset);
        rail.enabled = false;
    }

    // Its menu closed: start from hidden next time, and stop being animated
    private void OnDisable()
    {
        hovered = selected = false;
        shown = -1f;
        if (rail != null) rail.enabled = false;
        MenuButtonFxDriver.Remove(this);
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; MenuButtonFxDriver.Add(this); }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; MenuButtonFxDriver.Add(this); }
    public void OnSelect(BaseEventData eventData) { selected = true; MenuButtonFxDriver.Add(this); }
    public void OnDeselect(BaseEventData eventData) { selected = false; MenuButtonFxDriver.Add(this); }

    // One frame of the rail's animation. False once there's nothing more to do: settled, and neither
    // pointed at nor selected (a selected button is kept, since a switch between mouse and controller,
    // MenuNavigation.UsingGamepad, changes whether its rail shows without any event here).
    public bool Step(float dt)
    {
        if (this == null || rail == null) return false;
        bool live = selectable == null || selectable.IsInteractable();
        // A mouse click leaves the button selected: only a controller's selection shows the rail
        float target = live && (hovered || (selected && MenuNavigation.UsingGamepad)) ? 1f : 0f;
        float next = Mathf.MoveTowards(Mathf.Max(0f, shown), target, dt * 10f);
        if (!Mathf.Approximately(next, shown))
        {
            shown = next;
            rail.enabled = shown > 0f;
            // Slides in from the edge as it brightens
            rail.rectTransform.localScale = new Vector3(1f, Mathf.Lerp(0.4f, 1f, shown), 1f);
            Color c = railColor;
            c.a = shown;
            rail.color = c;
            return true;
        }
        return hovered || selected;
    }
}

// Animates the menu buttons' rails (MenuButtonFx) that are changing, and nothing else: one update for
// the whole game instead of one per button. Made on first use; lives for the whole session.
public class MenuButtonFxDriver : MonoBehaviour
{
    private static MenuButtonFxDriver instance;
    private static readonly List<MenuButtonFx> active = new List<MenuButtonFx>();

    public static void Add(MenuButtonFx fx)
    {
        if (fx == null || active.Contains(fx)) return;
        active.Add(fx);
        if (instance == null)
        {
            GameObject go = new GameObject("Menu Button Fx");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MenuButtonFxDriver>();
        }
    }

    public static void Remove(MenuButtonFx fx) => active.Remove(fx);

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            MenuButtonFx fx = active[i];
            if (fx == null || !fx.Step(dt)) active.RemoveAt(i);
        }
    }
}
