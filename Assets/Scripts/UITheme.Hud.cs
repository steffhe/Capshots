using UnityEngine;
using UnityEngine.UI;

// In-match screens: HUD, scoreboard, end of match and pause menu. The Manager styles the HUD,
// scoreboard and end screen when the map loads; the pause menu styles itself.
public static partial class UITheme
{
    #region HUD tokens

    // The combat HUD's design tokens. Colour carries meaning and nothing else:
    //  - white and muted white for information,
    //  - the accent blue for "you" and the interface (never a team: the blue team has its own blue),
    //  - Capshot gold only for cap shots,
    //  - Danger red only for "you're hurt / in danger", Warning amber for "running low",
    //  - Shield light blue for spawn protection.
    public static readonly Color HudText = new Color(1f, 1f, 1f, 0.92f);
    public static readonly Color HudMuted = new Color(1f, 1f, 1f, 0.55f);
    public static readonly Color HudFaint = new Color(1f, 1f, 1f, 0.22f);
    // The dark plate behind HUD readouts, see-through so the world still shows
    public static readonly Color HudPlate = new Color(0.02f, 0.03f, 0.06f, 0.55f);
    public static readonly Color HudTrack = new Color(0f, 0f, 0f, 0.55f);
    public static readonly Color Capshot = new Color(1f, 0.8f, 0.3f);
    public static readonly Color Danger = new Color(1f, 0.3f, 0.26f);
    public static readonly Color Warning = new Color(1f, 0.78f, 0.3f);
    public static readonly Color Shield = new Color(0.55f, 0.85f, 1f);

    // Type sizes: the big readouts (health, shots), the clock, labels and small captions
    public const int HudValueSize = 44;
    public const int HudClockSize = 30;
    public const int HudLabelSize = 16;
    public const int HudCaptionSize = 13;

    // Spacing: the gap to the screen's edges, the width of the health and weapon clusters, and the
    // gap between rows inside a cluster
    public const float HudMargin = 40f;
    public const float HudClusterWidth = 320f;
    public const float HudGap = 8f;
    public const float HudBarHeight = 10f;

    public static readonly Color HealthFull = HudText;
    public static readonly Color HealthLow = Danger;

    // White while healthy, amber from half health, red from a quarter: three clear states instead of a
    // blend, so the colour alone says how worried to be (see HudRules.StateFor)
    public static Color HealthColor(float ratio)
    {
        switch (HudRules.StateFor(ratio))
        {
            case HudRules.HealthState.Critical: return Danger;
            case HudRules.HealthState.Low: return Warning;
            default: return HudText;
        }
    }

    #endregion

    #region HUD

    // The combat HUD, laid out in four clusters round a clear centre:
    //  - bottom left: health, a big number over a slim bar marked in quarters;
    //  - bottom right: the gun, its form (or what it's busy doing) and shots left over a segmented
    //    energy meter, the four forms as a strip with their keys, and the plasma grenade;
    //  - top centre: the clock with the score either side and the goal under it (Manager.ScoreStrip);
    //  - top right: the kill feed (FeedbackUI).
    // Kills and deaths aren't on the HUD any more (the score strip and scoreboard have them), nor is
    // the player's own name. The prefab objects stay where code looks them up; only their look changes.
    public static void StyleHud(Transform hud)
    {
        if (hud == null) return;
        UseFontIn(hud.root);
        ScaleConsistently(hud);
        Image healthTrack = null;
        Text healthValue = null;
        Text weaponLabel = null;
        RectTransform ammo = hud.Find("Ammo") as RectTransform;
        Text ammoText = null;

        // Health: the number above a slim bar on a dark track, bottom left
        RectTransform health = hud.Find("Health") as RectTransform;
        RectTransform bar = health != null ? health.Find("Bar") as RectTransform : null;
        if (bar != null)
        {
            Vector2 barPosition = new Vector2(HudMargin, HudMargin);
            Vector2 barSize = new Vector2(HudClusterWidth, HudBarHeight);
            // The prefab's own background (missing in some maps) becomes the track; otherwise add one
            Image track = health.Find("Background")?.GetComponent<Image>();
            if (track == null) track = health.Find("Track")?.GetComponent<Image>();
            if (track == null) track = NewImage("Track", health, Color.clear, 0f);
            track.sprite = null;
            track.type = Image.Type.Simple;
            track.color = HudTrack;
            track.raycastTarget = false;
            track.transform.localScale = Vector3.one;
            track.transform.SetSiblingIndex(bar.GetSiblingIndex());
            Place(track.rectTransform, Vector2.zero, barPosition - new Vector2(2f, 2f), barSize + new Vector2(4f, 4f));
            // The Player scales the bar's width from its left edge, so the pivot stays bottom left
            Place(bar, Vector2.zero, barPosition, barSize);
            Image barImage = bar.GetComponent<Image>();
            if (barImage != null)
            {
                barImage.sprite = null;
                barImage.type = Image.Type.Simple;
                barImage.color = HealthFull;
            }
            healthTrack = track;

            // Quarter marks over the bar, so a glance tells "half" from "a third"
            if (health.Find("Quarters") == null)
            {
                RectTransform quarters = new GameObject("Quarters", typeof(RectTransform)).GetComponent<RectTransform>();
                quarters.SetParent(health, false);
                Place(quarters, Vector2.zero, barPosition, barSize);
                for (int i = 1; i < 4; i++)
                {
                    Image mark = NewImage("Mark " + i, quarters, HudTrack, 0f);
                    Place(mark.rectTransform, Vector2.zero, new Vector2(barSize.x * i / 4f - 1f, 0f), new Vector2(2f, barSize.y));
                }
            }
            // Above the bar and the damage trail HudFeedback puts under it
            health.Find("Quarters").SetAsLastSibling();

            // The number, above the bar's left end: "100 HP" (set by HudFeedback)
            healthValue = health.Find("Value")?.GetComponent<Text>();
            if (healthValue == null) healthValue = NewText("Value", health, HudValueSize, HealthFull, TextAnchor.LowerLeft);
            StyleText(healthValue, HudValueSize, HealthFull, TextAnchor.LowerLeft);
            healthValue.text = "";
            // Pivot on the bottom left, so the hit flash grows the number up and away from the bar
            Place(healthValue.rectTransform, Vector2.zero, new Vector2(barPosition.x - 2f, barPosition.y + barSize.y + 2f), new Vector2(240f, 52f));
            AddShadow(healthValue, 0.75f);
        }

        // The player's own name and level: kept (Player and Manager write to it) but not drawn, since
        // it tells you nothing in a fight. The scoreboard and the menu show it.
        Text username = hud.Find("Username/Text")?.GetComponent<Text>();
        if (username != null) username.enabled = false;

        // Weapon: shots left big on the right, the form's name (or what the gun's busy doing) on the
        // left of the same line, the energy meter under both (HudFeedback builds it)
        if (ammo != null)
        {
            Place(ammo, new Vector2(1f, 0f), new Vector2(-HudMargin, HudMargin), Vector2.zero);
            ammoText = ammo.Find("Text")?.GetComponent<Text>();
            if (ammoText != null)
            {
                StyleText(ammoText, HudValueSize, HudText, TextAnchor.LowerRight);
                ammoText.horizontalOverflow = HorizontalWrapMode.Overflow;
                Place(ammoText.rectTransform, new Vector2(1f, 0f), new Vector2(0f, HudBarHeight + 2f), new Vector2(200f, 52f));
                AddShadow(ammoText, 0.75f);
            }

            weaponLabel = ammo.Find("Weapon")?.GetComponent<Text>();
            if (weaponLabel == null) weaponLabel = NewText("Weapon", ammo, HudLabelSize, AccentText, TextAnchor.LowerLeft);
            StyleText(weaponLabel, HudLabelSize, AccentText, TextAnchor.LowerLeft);
            weaponLabel.text = "";
            weaponLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            Place(weaponLabel.rectTransform, new Vector2(1f, 0f), new Vector2(-HudClusterWidth, HudBarHeight + 10f), new Vector2(200f, 24f));
            weaponLabel.rectTransform.pivot = Vector2.zero;
            weaponLabel.rectTransform.anchoredPosition = new Vector2(-HudClusterWidth, HudBarHeight + 10f);
            AddShadow(weaponLabel, 0.75f);
        }

        // The gun forms: a strip of four slots over the weapon readout (HudFeedback fills it). The
        // prefab hangs it from the middle of the right edge as a tall column; it's now one short row.
        RectTransform arsenal = hud.Find("Arsenal") as RectTransform;
        if (arsenal != null) Place(arsenal, new Vector2(1f, 0f), new Vector2(-HudMargin, HudMargin + FormStripY), new Vector2(HudClusterWidth, FormSlotHeight));

        // Timer: top centre on a dark pill with a thin accent line along its bottom, so it reads as
        // the match clock at a glance (the Manager turns it amber, then red, near the end, and moves it
        // out of the HUD so it stays up while you're dead)
        RectTransform timer = hud.Find("Timer") as RectTransform;
        if (timer != null)
        {
            Place(timer, new Vector2(0.5f, 1f), new Vector2(0f, -20f), Vector2.zero);
            Image backdrop = timer.Find("Backdrop")?.GetComponent<Image>();
            if (backdrop == null)
            {
                backdrop = NewImage("Backdrop", timer, TimerBackdrop, 8f);
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
                StyleText(text, HudClockSize, TextColor, TextAnchor.MiddleCenter);
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                Place(text.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, TimerSize);
            }
        }

        // Kills and deaths: kept (the Manager writes to them, and copies the Kills text for the XP
        // popup) but not drawn; the score strip shows your score against the leader instead
        RectTransform stats = hud.Find("Stats") as RectTransform;
        if (stats != null)
        {
            Place(stats, new Vector2(0f, 1f), new Vector2(HudMargin, -26f), Vector2.zero);
            StatLine(stats.Find("Kills"), 0f, TextColor);
            StatLine(stats.Find("Deaths"), -30f, MutedText);
            foreach (Text line in stats.GetComponentsInChildren<Text>(true)) line.enabled = false;
        }

        // Crosshair: a small white dot with a dark outline, visible on any background. HudFeedback adds
        // the spread ticks, the kill ring and the cap shot stamp round it.
        Transform dot = hud.Find("Crosshair/Dot");
        if (dot != null)
        {
            Image image = dot.GetComponent<Image>();
            image.sprite = Rounded;
            image.type = Image.Type.Simple;
            image.color = HudText;
            ((RectTransform)dot).sizeDelta = new Vector2(5f, 5f);
            Outline outline = dot.GetComponent<Outline>();
            if (outline == null) outline = dot.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        StyleLeaderboard(hud.Find("Leaderboard"));

        HudFeedback.Attach(hud, health != null ? health.Find("Bar") as RectTransform : null, healthTrack, healthValue,
            weaponLabel, hud.Find("Crosshair"), ammo, ammoText, arsenal);
    }

    // The form strip's place over the weapon readout (bottom of the strip, above the HUD margin) and
    // its slots' height. The grenade sits above the strip (Weapon.BuildGrenadeIndicator).
    public const float FormStripY = 76f;
    public const float FormSlotHeight = 44f;
    public const float GrenadeRowY = FormStripY + FormSlotHeight + HudGap;

    public static readonly Vector2 TimerSize = new Vector2(168f, 46f);
    public static readonly Color TimerBackdrop = new Color(0f, 0f, 0f, 0.6f);
    // The timer near the end of a timed match: amber for the last 30 seconds, red for the last 10
    public static readonly Color TimerWarning = Warning;
    public static readonly Color TimerCritical = Danger;

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
            // The menus' panel with its corner brackets
            PanelChrome(image, false);
            StyleText(header.Find("Mode")?.GetComponent<Text>(), 22, TextColor);
            StyleText(header.Find("Map")?.GetComponent<Text>(), 15, MutedText);
        }

        Transform card = leaderboard.Find("PlayerCard");
        if (card != null)
        {
            Image image = card.GetComponent<Image>();
            Round(image, Space1);
            if (image != null) image.color = Surface;
            foreach (Text text in card.GetComponentsInChildren<Text>(true))
            {
                bool tag = text.name.EndsWith("Tag");
                StyleText(text, tag ? MenuCaptionSize : MenuLabelSize, tag ? MutedText : TextColor);
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

    // Dims the match behind the menu and styles its buttons: BACK TO MATCH primary, LEAVE MATCH as a
    // danger button, the rest secondary, all one text size. It fades in as it opens. The settings
    // panel copies the pause menu's button, slider and label, so it picks up this style too.
    public static void StylePause(Transform pause)
    {
        if (pause == null || pause.childCount == 0) return;
        UseFontIn(pause.root);
        Transform anchor = pause.GetChild(0);
        Reveal(anchor.gameObject);

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
                StyleText(title, MenuDisplaySize, TextColor, TextAnchor.MiddleCenter);
                // The match carries on behind it, so it isn't "paused": just the menu
                title.text = "MENU";
                title.rectTransform.sizeDelta = new Vector2(300f, 120f);
            }

            foreach (Transform child in buttons)
            {
                Button button = child.GetComponent<Button>();
                if (button == null) continue;
                ((RectTransform)child).sizeDelta = new Vector2(300f, 52f);
                // "Button (1)" is Resume, "Button" is Quit (leave the match)
                ButtonStyle style = child.name == "Button (1)" ? ButtonStyle.Primary : child.name == "Button" ? ButtonStyle.Danger : ButtonStyle.Secondary;
                StyleButton(button, style, 20);
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
