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
        ScaleConsistently(hud);
        Image healthTrack = null;
        Text healthValue = null;
        Text weaponLabel = null;

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
            healthTrack = track;

            // The number, just right of the bar: "100 HP" (set by HudFeedback)
            healthValue = health.Find("Value")?.GetComponent<Text>();
            if (healthValue == null) healthValue = NewText("Value", health, 30, HealthFull, TextAnchor.LowerLeft);
            StyleText(healthValue, 30, HealthFull, TextAnchor.LowerLeft);
            healthValue.text = "";
            // Pivot on the left of the baseline, so the hit flash grows the number away from the bar
            Place(healthValue.rectTransform, Vector2.zero, new Vector2(barPosition.x + barSize.x + 14f, barPosition.y - 8f), new Vector2(150f, 36f));
            healthValue.rectTransform.pivot = new Vector2(0f, 0.25f);
            healthValue.rectTransform.anchoredPosition += new Vector2(0f, 9f);
            AddShadow(healthValue, 0.75f);
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
                AddShadow(text, 0.75f);
            }

            // The active gun form's name, small and in the accent colour above the count (set by HudFeedback)
            weaponLabel = ammo.Find("Weapon")?.GetComponent<Text>();
            if (weaponLabel == null) weaponLabel = NewText("Weapon", ammo, 18, AccentText, TextAnchor.LowerRight);
            StyleText(weaponLabel, 18, AccentText, TextAnchor.LowerRight);
            weaponLabel.text = "";
            Place(weaponLabel.rectTransform, new Vector2(1f, 0f), new Vector2(0f, 62f), new Vector2(300f, 24f));
            AddShadow(weaponLabel, 0.75f);
        }

        // Gun form icons: stacked up the right side from just above the ammo count. Anchored to the
        // bottom (the prefab hangs them from the middle of the screen, so on wide screens and phones,
        // where the HUD is shorter, they slid down into the ammo count)
        RectTransform arsenal = hud.Find("Arsenal") as RectTransform;
        if (arsenal != null) Place(arsenal, new Vector2(1f, 0f), new Vector2(-250f, 140f), Vector2.zero);

        // Timer: top centre on a dark pill with a thin accent line along its bottom, so it reads as
        // the match clock at a glance (the Manager turns it amber, then red, near the end)
        RectTransform timer = hud.Find("Timer") as RectTransform;
        if (timer != null)
        {
            Place(timer, new Vector2(0.5f, 1f), new Vector2(0f, -20f), Vector2.zero);
            Image backdrop = timer.Find("Backdrop")?.GetComponent<Image>();
            if (backdrop == null)
            {
                backdrop = NewImage("Backdrop", timer, TimerBackdrop, TimerSize.y / 2f);
                backdrop.transform.SetAsFirstSibling();
            }
            Place(backdrop.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, TimerSize);
            if (backdrop.transform.Find("Accent") == null)
            {
                Image accent = NewImage("Accent", backdrop.transform, Accent, 1.5f);
                Place(accent.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(TimerSize.x - 48f, 3f));
            }
            Text text = timer.Find("Text")?.GetComponent<Text>();
            if (text != null)
            {
                StyleText(text, 30, TextColor, TextAnchor.MiddleCenter);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                Place(text.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, TimerSize);
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

        HudFeedback.Attach(hud, health != null ? health.Find("Bar") as RectTransform : null, healthTrack, healthValue, weaponLabel, hud.Find("Crosshair"));
    }

    public static readonly Vector2 TimerSize = new Vector2(168f, 46f);
    public static readonly Color TimerBackdrop = new Color(0f, 0f, 0f, 0.6f);
    // The timer near the end of a timed match: amber for the last 30 seconds, red for the last 10
    public static readonly Color TimerWarning = new Color(1f, 0.78f, 0.3f);
    public static readonly Color TimerCritical = new Color(1f, 0.35f, 0.3f);

    // The map scenes' canvases scale with the screen's width, so on wide screens and phones (which
    // are wider than 16:9) the whole HUD grew taller than the screen allowed and crowded together.
    // Expand keeps the canvas at least 1920 x 1080: the HUD is the same size relative to the screen's
    // height on any wide screen, and nothing laid out for 1920 wide is ever cut off on narrower ones.
    private static void ScaleConsistently(Transform hud)
    {
        Canvas canvas = hud.GetComponentInParent<Canvas>();
        CanvasScaler scaler = canvas != null ? canvas.rootCanvas.GetComponent<CanvasScaler>() : null;
        if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
    }

    // A dark pill behind a line of text, sized to the text: for the score lines under the timer,
    // which sit over the bright sky. Call again after the text changes.
    public static Image FitBackdrop(Image backdrop, Text text, float padding = 14f, float height = 30f)
    {
        if (text == null) return backdrop;
        RectTransform rect = text.rectTransform;
        if (backdrop == null)
        {
            backdrop = NewImage(text.name + " Backdrop", rect.parent, new Color(0f, 0f, 0f, 0.5f), height / 2f);
            backdrop.transform.SetSiblingIndex(rect.GetSiblingIndex());
        }
        bool show = text.gameObject.activeSelf && !string.IsNullOrEmpty(text.text);
        backdrop.gameObject.SetActive(show);
        if (!show) return backdrop;
        RectTransform b = backdrop.rectTransform;
        b.anchorMin = rect.anchorMin;
        b.anchorMax = rect.anchorMax;
        b.pivot = new Vector2(0.5f, 0.5f);
        // The text's middle, wherever its pivot is
        b.anchoredPosition = rect.anchoredPosition + new Vector2((0.5f - rect.pivot.x) * rect.sizeDelta.x, (0.5f - rect.pivot.y) * rect.sizeDelta.y);
        b.sizeDelta = new Vector2(text.preferredWidth + padding * 2f, height);
        return backdrop;
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
            // The column is 300 wide and starts at the holder's left edge (upper-left layout): put
            // its middle on the screen's middle, where the settings and customize panels open (the
            // prefab had it 21 units right of centre)
            RectTransform holder = (RectTransform)buttons;
            holder.anchoredPosition = new Vector2(holder.sizeDelta.x / 2f - 150f, holder.anchoredPosition.y);

            Text title = buttons.Find("Logo")?.GetComponent<Text>();
            if (title != null)
            {
                StyleText(title, 56, TextColor, TextAnchor.MiddleCenter);
                // The match carries on behind it, so it isn't "paused": just the menu
                title.text = "MENU";
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
                if (label != null && label.text == "RESUME") label.text = "BACK TO MATCH";
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
