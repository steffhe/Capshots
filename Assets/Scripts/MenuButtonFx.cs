using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A menu button's accent rail: a short bar on its left edge that slides in while the button is
// pointed at, or selected with a controller (where MenuNavigation's frame also shows), and out
// again after. UITheme.StyleButtonBackground adds it to every styled button. Local presentation only.
public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private Selectable selectable;
    private Image rail;
    private bool hovered, selected;
    private float shown = -1f;

    private const float RailWidth = 3f;
    private const float RailInset = 10f;

    // The rail's colour: white on a primary (accent) button, the accent otherwise, red for leaving
    public void SetColor(Color color)
    {
        Build();
        Color c = color;
        c.a = rail.color.a;
        rail.color = c;
        railColor = color;
    }

    private Color railColor = Color.white;

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

    private void OnDisable()
    {
        hovered = selected = false;
        shown = -1f;
        if (rail != null) rail.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;
    public void OnSelect(BaseEventData eventData) => selected = true;
    public void OnDeselect(BaseEventData eventData) => selected = false;

    private void LateUpdate()
    {
        if (rail == null) return;
        bool live = selectable == null || selectable.IsInteractable();
        // A mouse click leaves the button selected: only a controller's selection shows the rail
        float target = live && (hovered || (selected && MenuNavigation.UsingGamepad)) ? 1f : 0f;
        float next = Mathf.MoveTowards(Mathf.Max(0f, shown), target, Time.unscaledDeltaTime * 10f);
        if (Mathf.Approximately(next, shown)) return;
        shown = next;
        rail.enabled = shown > 0f;
        // Slides in from the edge as it brightens
        rail.rectTransform.localScale = new Vector3(1f, Mathf.Lerp(0.4f, 1f, shown), 1f);
        Color c = railColor;
        c.a = shown;
        rail.color = c;
    }
}
