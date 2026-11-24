using UnityEngine;
using UnityEngine.UI;

// One look for the menus and HUD: colours, the Jura font, rounded shapes and styling helpers.
// The scenes and prefabs keep their original layout and are restyled at runtime with these
// (see Launcher.Style.cs and UITheme.Hud.cs), so no scene or prefab needs editing.
public static partial class UITheme
{
    #region Palette

    // The logo's blue, and lighter shades of it for hover and small text
    public static readonly Color Accent = PlayerCap.LogoColor;
    public static readonly Color AccentHover = new Color(0.10f, 0.55f, 0.85f);
    public static readonly Color AccentPressed = new Color(0.01f, 0.32f, 0.52f);
    public static readonly Color AccentText = new Color(0.45f, 0.78f, 1f);

    public static readonly Color TextColor = Color.white;
    public static readonly Color MutedText = new Color(1f, 1f, 1f, 0.6f);

    // Dark, slightly blue surfaces, see-through so the 3D scene still shows
    public static readonly Color Backdrop = new Color(0.02f, 0.03f, 0.06f, 0.9f);
    public static readonly Color Panel = new Color(0.05f, 0.07f, 0.10f, 0.94f);
    public static readonly Color Surface = new Color(0.12f, 0.15f, 0.20f, 0.92f);
    public static readonly Color SurfaceAlt = new Color(0.16f, 0.20f, 0.26f, 0.92f);
    public static readonly Color Field = new Color(1f, 1f, 1f, 0.1f);
    public static readonly Color Track = new Color(1f, 1f, 1f, 0.18f);
    public static readonly Color Dim = new Color(0.02f, 0.03f, 0.05f, 0.85f);

    public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGBA(c);

    // "LABEL   value" with the label muted, for one-line option buttons like the map picker
    public static string Option(string label, string value) => $"<color={Hex(MutedText)}>{label}</color>   {value}";

    #endregion

    #region Font

    private static Font font;

    // The game's font. Taken from any Jura text under 'root' the first time, since the font
    // isn't in a Resources folder. Falls back to whatever font the first text uses.
    public static Font Font => font;

    public static void UseFontIn(Transform root)
    {
        if (font != null || root == null) return;
        Text fallback = null;
        foreach (Text t in root.GetComponentsInChildren<Text>(true))
        {
            if (t.font == null) continue;
            if (t.font.name.StartsWith("Jura")) { font = t.font; return; }
            if (fallback == null) fallback = t;
        }
        if (fallback != null) font = fallback.font;
    }

    #endregion

    #region Sprites

    private const int SpriteRadius = 16;
    private static Sprite rounded;
    private static Sprite fade;

    // White rounded rectangle, 9-sliced. Image.pixelsPerUnitMultiplier sets the corner radius.
    public static Sprite Rounded
    {
        get
        {
            if (rounded != null) return rounded;
            int size = SpriteRadius * 2 + 2;
            Texture2D tex = NewTexture("UITheme Rounded", size, size);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    Vector2 c = new Vector2(Mathf.Clamp(p.x, SpriteRadius, size - SpriteRadius), Mathf.Clamp(p.y, SpriteRadius, size - SpriteRadius));
                    float alpha = Mathf.Clamp01(SpriteRadius - Vector2.Distance(p, c) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            float border = SpriteRadius + 0.5f;
            rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            rounded.name = tex.name;
            return rounded;
        }
    }

    // White, solid on the left and fading out to the right. Used for the menu sidebar.
    public static Sprite FadeRight
    {
        get
        {
            if (fade != null) return fade;
            const int width = 256;
            Texture2D tex = NewTexture("UITheme Fade", width, 1);
            Color32[] pixels = new Color32[width];
            for (int x = 0; x < width; x++)
            {
                float t = Mathf.InverseLerp(0.6f, 1f, (x + 0.5f) / width);
                pixels[x] = new Color32(255, 255, 255, (byte)(255f * (1f - Mathf.SmoothStep(0f, 1f, t))));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            fade = Sprite.Create(tex, new Rect(0, 0, width, 1), new Vector2(0.5f, 0.5f), 100f);
            fade.name = tex.name;
            return fade;
        }
    }

    private static Texture2D NewTexture(string name, int width, int height)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
    }

    // Gives an image the rounded sprite with the given corner radius (in canvas units). A radius of
    // 0 gives a plain, sharp-cornered rectangle (no sprite): the small rounded sprite stretched
    // unsliced over a big area turns into a blurry blob with see-through corners.
    public static void Round(Image image, float radius)
    {
        if (image == null) return;
        image.fillCenter = true;
        if (radius <= 0f)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 1f;
            return;
        }
        image.sprite = Rounded;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = (SpriteRadius + 0.5f) / radius;
    }

    #endregion

    #region Building blocks

    public static Text StyleText(Text text, int size, Color color, TextAnchor? alignment = null)
    {
        if (text == null) return null;
        if (font != null) text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Normal;
        text.resizeTextForBestFit = false;
        text.color = color;
        text.supportRichText = true;
        if (alignment.HasValue) text.alignment = alignment.Value;
        return text;
    }

    // A soft drop shadow, so text stays readable over a bright 3D scene
    public static void AddShadow(Graphic graphic, float alpha = 0.6f)
    {
        if (graphic == null) return;
        Shadow shadow = graphic.GetComponent<Shadow>();
        if (shadow == null) shadow = graphic.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, alpha);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

    public enum ButtonStyle { Primary, Secondary }

    // Rounded background and hover / press colours. Leaves the button's text alone.
    public static void StyleButtonBackground(Selectable selectable, ButtonStyle style, float radius = 8f)
    {
        if (selectable == null) return;
        Image image = selectable.targetGraphic as Image;
        if (image == null) image = selectable.GetComponent<Image>();
        if (image != null)
        {
            Round(image, radius);
            image.color = Color.white;
            selectable.targetGraphic = image;
        }

        ColorBlock colors = selectable.colors;
        colors.normalColor = style == ButtonStyle.Primary ? Accent : Surface;
        colors.highlightedColor = AccentHover;
        colors.pressedColor = AccentPressed;
        // After a click the button stays selected: keep it looking normal rather than highlighted
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        selectable.colors = colors;
        selectable.transition = Selectable.Transition.ColorTint;
    }

    // Background plus a centred, upper-case label
    public static void StyleButton(Button button, ButtonStyle style, int fontSize = 22)
    {
        if (button == null) return;
        StyleButtonBackground(button, style);
        Text label = button.GetComponentInChildren<Text>(true);
        if (label == null) return;
        StyleText(label, fontSize, TextColor, TextAnchor.MiddleCenter);
        // Leave rich text alone: upper-casing would break its tags
        if (!label.text.Contains("<")) label.text = label.text.ToUpperInvariant();
    }

    // A button that opens a list of options (OptionPicker): label on the left, a chevron pointing down on the right
    public static void StyleSelector(Button button, int fontSize = 20)
    {
        if (button == null) return;
        StyleButtonBackground(button, ButtonStyle.Secondary);
        Text label = button.GetComponentInChildren<Text>(true);
        if (label == null) return;
        StyleText(label, fontSize, TextColor, TextAnchor.MiddleLeft);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        Inset(label.rectTransform, 18f, 40f);

        if (button.transform.Find("Chevron") != null) return;
        Text chevron = NewText("Chevron", button.transform, 30, MutedText, TextAnchor.MiddleCenter);
        chevron.text = "\u203A";
        // Turned to point down, like a dropdown's
        RectTransform chevronRect = chevron.rectTransform;
        chevronRect.anchorMin = chevronRect.anchorMax = new Vector2(1f, 0.5f);
        chevronRect.pivot = new Vector2(0.5f, 0.5f);
        chevronRect.anchoredPosition = new Vector2(-24f, 0f);
        chevronRect.sizeDelta = new Vector2(24f, 24f);
        chevronRect.localEulerAngles = new Vector3(0f, 0f, -90f);
    }

    public static void StyleInput(InputField input, string placeholder, int fontSize = 20)
    {
        if (input == null) return;
        Image image = input.targetGraphic as Image;
        if (image == null) image = input.GetComponent<Image>();
        Round(image, 8f);
        if (image != null) image.color = Color.white;

        ColorBlock colors = input.colors;
        colors.normalColor = Field;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.16f);
        colors.pressedColor = colors.selectedColor = new Color(1f, 1f, 1f, 0.2f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        input.colors = colors;

        if (input.textComponent != null)
        {
            StyleText(input.textComponent, fontSize, TextColor, TextAnchor.MiddleLeft);
            input.textComponent.supportRichText = false;
            Inset(input.textComponent.rectTransform, 14f, 14f);
        }
        Text hint = input.placeholder as Text;
        if (hint != null)
        {
            StyleText(hint, fontSize, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft);
            hint.text = placeholder;
            Inset(hint.rectTransform, 14f, 14f);
        }

        input.customCaretColor = true;
        input.caretColor = TextColor;
        input.caretWidth = 2;
        input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.6f);
    }

    // Thin track, accent fill and a round white handle
    public static void StyleSlider(Slider slider)
    {
        if (slider == null) return;

        Transform background = slider.transform.Find("Background");
        if (background != null)
        {
            Image track = background.GetComponent<Image>();
            Round(track, 3f);
            if (track != null) track.color = Track;
            ThinTrack((RectTransform)background);
        }
        if (slider.fillRect != null)
        {
            Image fill = slider.fillRect.GetComponent<Image>();
            Round(fill, 3f);
            if (fill != null) fill.color = AccentText;
            if (slider.fillRect.parent is RectTransform fillArea) ThinTrack(fillArea);
        }
        if (slider.handleRect != null)
        {
            Image handle = slider.handleRect.GetComponent<Image>();
            if (handle != null)
            {
                handle.sprite = Rounded;
                handle.type = Image.Type.Simple;
                handle.color = Color.white;
                slider.targetGraphic = handle;
            }
            slider.handleRect.sizeDelta = new Vector2(18f, 18f - ((RectTransform)slider.transform).rect.height);
        }

        ColorBlock colors = slider.colors;
        colors.normalColor = colors.selectedColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.94f, 1f);
        colors.pressedColor = AccentText;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        slider.colors = colors;
    }

    private static void ThinTrack(RectTransform rect)
    {
        rect.anchorMin = new Vector2(rect.anchorMin.x, 0.35f);
        rect.anchorMax = new Vector2(rect.anchorMax.x, 0.65f);
    }

    public static Image NewImage(string name, Transform parent, Color color, float radius)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        Round(image, radius);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static Text NewText(string name, Transform parent, int size, Color color, TextAnchor alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        StyleText(text, size, color, alignment);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        Stretch(text.rectTransform);
        return text;
    }

    #endregion

    #region Rect helpers

    // Fills the parent
    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // Fills the parent, minus left and right margins
    public static void Inset(RectTransform rect, float left, float right)
    {
        Stretch(rect);
        rect.offsetMin = new Vector2(left, 0f);
        rect.offsetMax = new Vector2(-right, 0f);
    }

    // A full-width strip hanging from the parent's top edge: 'top' units down, 'height' tall.
    // xMin/xMax pick a part of the width (0 to 1).
    public static void Row(RectTransform rect, float top, float height, float xMin = 0f, float xMax = 1f)
    {
        rect.anchorMin = new Vector2(xMin, 1f);
        rect.anchorMax = new Vector2(xMax, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(0f, height);
    }

    // Fixed-size rect at an anchor point; the pivot matches the anchor
    public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    #endregion
}
