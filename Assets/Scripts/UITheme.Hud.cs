using UnityEngine;
using UnityEngine.UI;

// In-match screens: HUD, scoreboard, end of match and pause menu. The Manager styles the HUD,
// scoreboard and end screen when the map loads; the pause menu styles itself.
public static partial class UITheme
{
    public static readonly Color HealthFull = new Color(1f, 1f, 1f, 0.92f);
    public static readonly Color HealthLow = new Color(0.95f, 0.27f, 0.27f, 0.95f);

    // White health bar that turns red as it runs low
    public static Color HealthColor(float ratio) => Color.Lerp(HealthLow, HealthFull, Mathf.InverseLerp(0.25f, 0.6f, ratio));

    #region HUD

    public static void StyleHud(Transform hud)
    {
        if (hud == null) return;
        UseFontIn(hud.root);

        // Health: a slim bar on a dark track in the bottom left, with the player's name above it
        RectTransform health = hud.Find("Health") as RectTransform;
        RectTransform bar = health != null ? health.Find("Bar") as RectTransform : null;
        if (bar != null)
        {
            Vector2 barPosition = new Vector2(40f, 40f);
            Vector2 barSize = new Vector2(360f, 14f);
            // The prefab's own background (missing in some maps) becomes the track; otherwise add one
            Image track = health.Find("Background")?.GetComponent<Image>();
            if (track == null) track = health.Find("Track")?.GetComponent<Image>();
            if (track == null) track = NewImage("Track", health, Color.clear, 0f);
            track.sprite = null;
            track.type = Image.Type.Simple;
            track.color = new Color(0f, 0f, 0f, 0.65f);
            track.raycastTarget = false;
            track.transform.localScale = Vector3.one;
            track.transform.SetSiblingIndex(bar.GetSiblingIndex());
            Place(track.rectTransform, Vector2.zero, barPosition - new Vector2(2f, 2f), barSize + new Vector2(4f, 4f));
            // The Player scales the bar's width from its left edge, so the pivot stays bottom left
            Place(bar, Vector2.zero, barPosition, barSize);
            Image barImage = bar.GetComponent<Image>();
            if (barImage != null) barImage.color = HealthFull;
        }

        RectTransform username = hud.Find("Username") as RectTransform;
        if (username != null)
        {
            Place(username, Vector2.zero, new Vector2(40f, 62f), Vector2.zero);
            Text name = username.GetComponentInChildren<Text>(true);
            if (name != null)
            {
                StyleText(name, 20, TextColor, TextAnchor.LowerLeft);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                Place(name.rectTransform, Vector2.zero, Vector2.zero, new Vector2(400f, 28f));
                AddShadow(name);
            }
        }

        // Ammo: big clip count in the bottom right
        RectTransform ammo = hud.Find("Ammo") as RectTransform;
        if (ammo != null)
        {
            Place(ammo, new Vector2(1f, 0f), new Vector2(-40f, 30f), Vector2.zero);
            Text text = ammo.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                StyleText(text, 46, TextColor, TextAnchor.LowerRight);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                Place(text.rectTransform, new Vector2(1f, 0f), Vector2.zero, new Vector2(300f, 60f));
                AddShadow(text);
            }
        }

        // Timer: top centre on a dark pill
        RectTransform timer = hud.Find("Timer") as RectTransform;
        if (timer != null)
        {
            Place(timer, new Vector2(0.5f, 1f), new Vector2(0f, -20f), Vector2.zero);
            if (timer.Find("Backdrop") == null)
            {
                Image backdrop = NewImage("Backdrop", timer, new Color(0f, 0f, 0f, 0.6f), 22f);
                backdrop.transform.SetAsFirstSibling();
                Place(backdrop.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(150f, 44f));
            }
            Text text = timer.Find("Text")?.GetComponent<Text>();
            if (text != null)
            {
                StyleText(text, 28, TextColor, TextAnchor.MiddleCenter);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                Place(text.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(150f, 44f));
            }
        }

        // Kills and deaths: top left (the kill feed is in the top right)
        RectTransform stats = hud.Find("Stats") as RectTransform;
        if (stats != null)
        {
            Place(stats, new Vector2(0f, 1f), new Vector2(40f, -26f), Vector2.zero);
            StatLine(stats.Find("Kills"), 0f, TextColor);
            StatLine(stats.Find("Deaths"), -30f, MutedText);
        }

        // Crosshair: a small white dot with a dark outline, visible on any background
        Transform dot = hud.Find("Crosshair/Dot");
        if (dot != null)
        {
            Image image = dot.GetComponent<Image>();
            image.sprite = Rounded;
            image.type = Image.Type.Simple;
            image.color = new Color(1f, 1f, 1f, 0.9f);
            ((RectTransform)dot).sizeDelta = new Vector2(6f, 6f);
            Outline outline = dot.GetComponent<Outline>();
            if (outline == null) outline = dot.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        StyleLeaderboard(hud.Find("Leaderboard"));
    }

    private static void StatLine(Transform line, float y, Color color)
    {
        Text text = line != null ? line.GetComponent<Text>() : null;
        if (text == null) return;
        StyleText(text, 22, color, TextAnchor.UpperLeft);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(240f, 30f));
        AddShadow(text);
    }

    #endregion

    #region Scoreboard and end screen

    // Dark header and rows. The Manager copies the row template (PlayerCard) for each player.
    public static void StyleLeaderboard(Transform leaderboard)
    {
        if (leaderboard == null) return;
        UseFontIn(leaderboard.root);

        Transform header = leaderboard.Find("Header");
        if (header != null)
        {
            Image image = header.GetComponent<Image>();
            Round(image, 8f);
            if (image != null) image.color = Panel;
            StyleText(header.Find("Mode")?.GetComponent<Text>(), 22, TextColor);
            StyleText(header.Find("Map")?.GetComponent<Text>(), 15, MutedText);
        }

        Transform card = leaderboard.Find("PlayerCard");
        if (card != null)
        {
            Image image = card.GetComponent<Image>();
            if (image != null) image.color = Surface;
            foreach (Text text in card.GetComponentsInChildren<Text>(true))
            {
                bool tag = text.name.EndsWith("Tag");
                StyleText(text, 15, tag ? MutedText : TextColor);
                if (text.name == "Level") text.color = AccentText;
            }
            Image icon = card.Find("Icon")?.GetComponent<Image>();
            Round(icon, 4f);
            if (icon != null) icon.color = Track;
        }
    }

    public static void StyleEndGame(Transform endGame)
    {
        if (endGame == null) return;
        UseFontIn(endGame.root);

        Image darken = endGame.Find("Design/Darken")?.GetComponent<Image>();
        if (darken != null) darken.color = Dim;

        Text message = endGame.Find("Design/Message")?.GetComponent<Text>();
        if (message != null)
        {
            StyleText(message, 56, TextColor, TextAnchor.MiddleCenter);
            message.horizontalOverflow = HorizontalWrapMode.Overflow;
            Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(800f, 80f));
            AddShadow(message);
        }

        StyleLeaderboard(endGame.Find("Leaderboard"));
    }

    #endregion

    #region Pause menu

    // Dims the match behind the menu and styles its buttons. The settings panel copies the
    // pause menu's button, slider and label, so it picks up this style too.
    public static void StylePause(Transform pause)
    {
        if (pause == null || pause.childCount == 0) return;
        UseFontIn(pause.root);
        Transform anchor = pause.GetChild(0);

        Image overlay = anchor.Find("Overlay")?.GetComponent<Image>();
        if (overlay != null) overlay.color = Dim;

        Transform buttons = anchor.Find("Buttons");
        if (buttons != null)
        {
            VerticalLayoutGroup layout = buttons.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = 12f;
                layout.childForceExpandHeight = false;
            }

            Text title = buttons.Find("Logo")?.GetComponent<Text>();
            if (title != null)
            {
                StyleText(title, 56, TextColor, TextAnchor.MiddleCenter);
                title.text = title.text.ToUpperInvariant();
                title.rectTransform.sizeDelta = new Vector2(300f, 120f);
            }

            foreach (Transform child in buttons)
            {
                Button button = child.GetComponent<Button>();
                if (button == null) continue;
                ((RectTransform)child).sizeDelta = new Vector2(300f, 52f);
                StyleButton(button, child.name == "Button (1)" ? ButtonStyle.Primary : ButtonStyle.Secondary);
                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null && label.text == "QUIT") label.text = "LEAVE MATCH";
            }
        }

        Transform sensitivity = anchor.Find("Sensitivity");
        if (sensitivity != null)
        {
            foreach (Slider slider in sensitivity.GetComponentsInChildren<Slider>(true)) StyleSlider(slider);
            foreach (Transform child in sensitivity)
            {
                Text label = child.GetComponent<Text>();
                if (label != null) StyleText(label, 16, TextColor);
            }
        }
    }

    #endregion
}
