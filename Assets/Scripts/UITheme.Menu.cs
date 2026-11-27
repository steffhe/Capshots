using UnityEngine;
using UnityEngine.UI;

// The menus' design system, shared by the main menu, settings, customize, pause menu, lobby and the
// end of match screens. Restrained and mechanical: dark plates with small radii, one accent colour,
// thin hairlines, and corner brackets on panels like the marks on a machined part. Interaction states
// are a quiet surface change plus an accent rail on the button's left edge (MenuButtonFx), and screens
// fade in briefly as they open (MenuReveal). Everything here is local presentation.
public static partial class UITheme
{
    #region Menu tokens

    // Type scale: the big result words, panel titles, headings, body text, labels and captions
    public const int MenuDisplaySize = 56;
    public const int MenuTitleSize = 34;
    public const int MenuHeadingSize = 20;
    public const int MenuBodySize = 18;
    public const int MenuLabelSize = 15;
    public const int MenuCaptionSize = 13;

    // Spacing steps (canvas units), on a 4 unit grid
    public const float Space1 = 4f, Space2 = 8f, Space3 = 12f, Space4 = 16f, Space5 = 24f, Space6 = 32f;

    // Corners: small on controls, a little larger on panels (machined, not bubbly)
    public const float ControlRadius = 6f;
    public const float PanelRadius = 10f;

    public static readonly Color Hairline = new Color(1f, 1f, 1f, 0.1f);
    public static readonly Color Bracket = new Color(1f, 1f, 1f, 0.3f);
    // A secondary button pointed at: a lighter surface (the accent is kept for the rail and primary)
    public static readonly Color SurfaceHover = new Color(0.2f, 0.25f, 0.32f, 0.95f);
    // Leaving or quitting: red text, a dark red surface when pointed at
    public static readonly Color DangerText = new Color(1f, 0.6f, 0.55f);
    public static readonly Color DangerSurface = new Color(0.34f, 0.09f, 0.09f, 0.95f);

    #endregion

    #region Panels

    // A menu panel: the dark plate, its corners rounded a little, with corner brackets and a short
    // accent tab along its top edge. Safe to call again; the chrome is added once and ignored by layouts.
    public static void PanelChrome(Image panel, bool tab = true)
    {
        if (panel == null) return;
        Round(panel, PanelRadius);
        panel.color = Panel;
        if (panel.transform.Find("Chrome") != null) return;

        RectTransform chrome = new GameObject("Chrome", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
        chrome.GetComponent<LayoutElement>().ignoreLayout = true;
        chrome.SetParent(panel.transform, false);
        Stretch(chrome);

        // Brackets just inside each corner
        const float arm = 14f, thick = 2f, inset = 6f;
        for (int i = 0; i < 4; i++)
        {
            Vector2 corner = new Vector2(i % 2, i / 2);
            Vector2 inward = new Vector2(corner.x == 0f ? inset : -inset, corner.y == 0f ? inset : -inset);
            Image across = NewImage("Bracket", chrome, Bracket, 0f);
            Place(across.rectTransform, corner, inward, new Vector2(arm, thick));
            Image down = NewImage("Bracket", chrome, Bracket, 0f);
            Place(down.rectTransform, corner, inward, new Vector2(thick, arm));
        }

        if (tab)
        {
            Image accent = NewImage("Tab", chrome, Accent, 0f);
            Place(accent.rectTransform, new Vector2(0f, 1f), new Vector2(PanelRadius + 18f, 0f), new Vector2(40f, 3f));
        }
    }

    // A small heading in the accent colour with a hairline running on to its right edge, over a
    // group of rows ("MATCH SETUP", "CONTROLS"). The text should be left-aligned; its rect sets how far
    // the rule runs. Safe to call again.
    public static Text SectionHeader(Text text, string caption = null)
    {
        if (text == null) return null;
        StyleText(text, MenuCaptionSize, AccentText, text.alignment);
        if (caption != null) text.text = caption;
        AddRule(text);
        return text;
    }

    // The hairline after a left-aligned text, to its rect's right edge
    public static void AddRule(Text text, Color? color = null)
    {
        if (text == null) return;
        Image rule = text.transform.Find("Rule")?.GetComponent<Image>();
        if (rule == null) rule = NewImage("Rule", text.transform, color ?? Hairline, 0f);
        RectTransform r = rule.rectTransform;
        bool lower = text.alignment == TextAnchor.LowerLeft;
        float y = lower ? 0.3f : 0.5f;
        r.anchorMin = new Vector2(0f, y);
        r.anchorMax = new Vector2(1f, y);
        r.pivot = new Vector2(0f, 0.5f);
        r.offsetMin = new Vector2(text.preferredWidth + Space3, -0.5f);
        r.offsetMax = new Vector2(0f, 0.5f);
    }

    #endregion

    #region Switches

    // An on / off switch on the right of a button, for settings that are simply on or off. The
    // button's label names the setting; SetSwitch shows its state.
    public static void AddSwitch(Button button)
    {
        if (button == null || button.transform.Find("Switch") != null) return;
        // The selector's chevron isn't wanted: this doesn't open a list
        Transform chevron = button.transform.Find("Chevron");
        if (chevron != null) chevron.gameObject.SetActive(false);

        Image track = NewImage("Switch", button.transform, Track, 10f);
        Place(track.rectTransform, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(40f, 20f));
        Image knob = NewImage("Knob", track.transform, Color.white, 7f);
        Place(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-10f, 0f), new Vector2(14f, 14f));
    }

    public static void SetSwitch(Button button, bool on)
    {
        if (button == null) return;
        Image track = button.transform.Find("Switch")?.GetComponent<Image>();
        if (track == null) return;
        track.color = on ? Accent : Track;
        RectTransform knob = track.transform.Find("Knob") as RectTransform;
        if (knob != null) knob.anchoredPosition = new Vector2(on ? 10f : -10f, 0f);
    }

    // A button that steps through a few values when pressed (SPRINT: HOLD / TOGGLE): like a selector,
    // but its chevron points onwards rather than down, since no list opens
    public static void StyleCycle(Button button, int fontSize = 20)
    {
        StyleSelector(button, fontSize);
        Transform chevron = button != null ? button.transform.Find("Chevron") : null;
        if (chevron != null) chevron.localEulerAngles = Vector3.zero;
    }

    #endregion

    #region Transitions

    // A screen or panel fades in briefly each time it's shown (MenuReveal). scale adds a slight settle
    // from 98.5%; leave it off where something else sets the object's scale.
    public static void Reveal(GameObject target, bool scale = false)
    {
        if (target == null) return;
        MenuReveal reveal = target.GetComponent<MenuReveal>();
        if (reveal == null) reveal = target.AddComponent<MenuReveal>();
        reveal.scale = scale;
    }

    #endregion
}
