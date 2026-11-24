using UnityEngine;
using UnityEngine.UI;

// Main menu look: a dark sidebar behind the menu column, a profile card with level and XP,
// a styled room list and option rows in the create tab. Applied at runtime on top of the menu
// scene's original layout, so the scene needn't change.
public partial class Launcher
{
    private const float ColumnWidth = 300f;
    private const float RowHeight = 52f;
    private const float RoomListWidth = 560f;

    private Text levelText;
    private Text xpText;
    private RectTransform xpFill;
    private Text roomsEmptyText;

    private void ApplyStyle()
    {
        Transform canvas = tabMain.transform.parent;
        UITheme.UseFontIn(canvas);

        AddSidebar(canvas);
        StyleColumn(tabMain);
        StyleColumn(tabRooms);
        StyleColumn(tabCreate);
        BuildProfileCard();
        BuildRoomList();
        BuildCreateTab();
        RefreshProfileCard();
    }

    // Dark panel down the left side that fades out towards the player model, so the menu
    // reads well over the bright sky and floor
    private static void AddSidebar(Transform canvas)
    {
        if (canvas.Find("Sidebar") != null) return;
        GameObject sidebar = new GameObject("Sidebar", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)sidebar.transform;
        rect.SetParent(canvas, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(-60f, 0f);

        Image image = sidebar.GetComponent<Image>();
        image.sprite = UITheme.FadeRight;
        image.color = UITheme.Backdrop;
        image.raycastTarget = false;
    }

    // Each tab's "Anchor" column: logo, then rows stacked top to bottom
    private static void StyleColumn(GameObject tab)
    {
        Transform anchor = tab.transform.Find("Anchor");
        if (anchor == null) return;

        VerticalLayoutGroup layout = anchor.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            // The room list's layout is switched off in the scene; the restyled column relies on it
            layout.enabled = true;
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
        }

        Transform logo = anchor.Find("Logo");
        if (logo != null) ((RectTransform)logo).sizeDelta = new Vector2(ColumnWidth, 130f);

        foreach (Transform child in anchor)
        {
            Button button = child.GetComponent<Button>();
            if (button == null) continue;
            ((RectTransform)child).sizeDelta = new Vector2(ColumnWidth, RowHeight);
            UITheme.StyleButton(button, UITheme.ButtonStyle.Secondary);
        }
    }

    // Groups loose elements into a fixed-size block that sits in a tab's column
    private static RectTransform ColumnBlock(GameObject tab, string name, int index, float height)
    {
        Transform anchor = tab.transform.Find("Anchor");
        GameObject block = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)block.transform;
        rect.SetParent(anchor, false);
        rect.SetSiblingIndex(index);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(ColumnWidth, height);
        return rect;
    }

    private static Text Caption(Transform text, string caption)
    {
        Text label = text != null ? text.GetComponent<Text>() : null;
        if (label == null) return null;
        UITheme.StyleText(label, 14, UITheme.MutedText, TextAnchor.MiddleLeft);
        label.text = caption;
        return label;
    }

    #region Main tab

    // Name field, level and an XP bar, under the logo
    private void BuildProfileCard()
    {
        Transform profile = tabMain.transform.Find("Profile");
        if (profile == null || usernameField == null) return;

        RectTransform card = ColumnBlock(tabMain, "Profile Card", 1, 104f);

        Text caption = Caption(profile.Find("ProfileText"), "PLAYER NAME");
        if (caption != null)
        {
            caption.transform.SetParent(card, false);
            UITheme.Row(caption.rectTransform, 0f, 18f);
        }

        usernameField.transform.SetParent(card, false);
        UITheme.Row((RectTransform)usernameField.transform, 22f, 44f);
        UITheme.StyleInput(usernameField, "ENTER YOUR NAME");

        levelText = profile.Find("Level")?.GetComponent<Text>();
        if (levelText != null)
        {
            levelText.transform.SetParent(card, false);
            UITheme.StyleText(levelText, 16, UITheme.TextColor, TextAnchor.MiddleLeft);
            UITheme.Row(levelText.rectTransform, 74f, 20f, 0f, 0.5f);
        }

        xpText = profile.Find("XP")?.GetComponent<Text>();
        if (xpText != null)
        {
            xpText.transform.SetParent(card, false);
            UITheme.StyleText(xpText, 14, UITheme.MutedText, TextAnchor.MiddleRight);
            UITheme.Row(xpText.rectTransform, 74f, 20f, 0.5f, 1f);
        }

        Image track = UITheme.NewImage("XP Bar", card, UITheme.Track, 3f);
        UITheme.Row(track.rectTransform, 98f, 6f);
        Image fill = UITheme.NewImage("Fill", track.transform, UITheme.AccentText, 3f);
        xpFill = fill.rectTransform;
        UITheme.Stretch(xpFill);
    }

    private void RefreshProfileCard()
    {
        int needed = ProfileData.XpToNextLevel(myProfile.level);
        if (levelText != null) levelText.text = "LEVEL " + myProfile.level;
        if (xpText != null) xpText.text = $"{myProfile.xp} / {needed} XP";
        if (xpFill != null) xpFill.anchorMax = new Vector2(Mathf.Clamp01((float)myProfile.xp / needed), 1f);
    }

    #endregion

    #region Room list tab

    // Caption, the list and the back button stacked in the column, with wider styled rows
    private void BuildRoomList()
    {
        ScrollRect scroll = tabRooms.GetComponentInChildren<ScrollRect>(true);
        Transform anchor = tabRooms.transform.Find("Anchor");
        if (scroll == null || anchor == null) return;

        Text caption = Caption(tabRooms.transform.Find("Title"), "OPEN MATCHES");
        if (caption != null)
        {
            caption.transform.SetParent(anchor, false);
            caption.transform.SetSiblingIndex(1);
            UITheme.Place(caption.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(RoomListWidth, 20f));
            caption.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        RectTransform view = (RectTransform)scroll.transform;
        view.SetParent(anchor, false);
        view.SetSiblingIndex(caption != null ? 2 : 1);
        // The scene stretches the list with its scale; size it properly instead
        view.localScale = Vector3.one;
        view.anchorMin = view.anchorMax = new Vector2(0f, 1f);
        view.pivot = new Vector2(0.5f, 0.5f);
        view.sizeDelta = new Vector2(RoomListWidth, 400f);
        Image viewImage = view.GetComponent<Image>();
        if (viewImage != null) viewImage.color = Color.clear;

        // Thin, quiet scrollbar
        const float scrollbarWidth = 6f;
        if (scroll.viewport != null)
        {
            scroll.viewport.offsetMin = Vector2.zero;
            scroll.viewport.offsetMax = new Vector2(-(scrollbarWidth + 8f), 0f);
        }
        Scrollbar bar = scroll.verticalScrollbar;
        if (bar != null)
        {
            RectTransform barRect = (RectTransform)bar.transform;
            barRect.sizeDelta = new Vector2(scrollbarWidth, 0f);
            Image barImage = bar.GetComponent<Image>();
            if (barImage != null) barImage.color = Color.clear;
            Transform area = barRect.Find("Sliding Area");
            if (area != null) UITheme.Stretch((RectTransform)area);
            if (bar.handleRect != null)
            {
                bar.handleRect.offsetMin = bar.handleRect.offsetMax = Vector2.zero;
                Image handle = bar.handleRect.GetComponent<Image>();
                UITheme.Round(handle, 3f);
                if (handle != null) handle.color = new Color(1f, 1f, 1f, 0.3f);
                ColorBlock colors = bar.colors;
                colors.normalColor = colors.selectedColor = Color.white;
                colors.highlightedColor = colors.pressedColor = new Color(1f, 1f, 1f, 1.6f);
                colors.colorMultiplier = 1.6f;
                bar.colors = colors;
            }
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        // Rows fill the list's width
        VerticalLayoutGroup rows = scroll.content.GetComponent<VerticalLayoutGroup>();
        if (rows != null)
        {
            rows.spacing = 8f;
            rows.padding = new RectOffset(0, 0, 0, 0);
            rows.childAlignment = TextAnchor.UpperLeft;
            rows.childControlWidth = true;
            rows.childForceExpandWidth = true;
            rows.childControlHeight = false;
            rows.childForceExpandHeight = false;
        }
        // The content grows downwards with the rows but always spans the list's full width
        ContentSizeFitter fitter = scroll.content.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        scroll.content.anchorMin = new Vector2(0f, 1f);
        scroll.content.anchorMax = Vector2.one;
        scroll.content.pivot = new Vector2(0.5f, 1f);
        scroll.content.anchoredPosition = Vector2.zero;
        scroll.content.sizeDelta = new Vector2(0f, scroll.content.sizeDelta.y);

        roomsEmptyText = UITheme.NewText("Empty", view, 18, UITheme.MutedText, TextAnchor.MiddleCenter);
        UITheme.Row(roomsEmptyText.rectTransform, 24f, 60f);
        roomsEmptyText.lineSpacing = 1.3f;
        ShowRoomsEmptyState(true, "LOOKING FOR MATCHES...");

        // Don't let any room rows from the scene template show before the first update
        foreach (Transform row in scroll.content) row.gameObject.SetActive(false);
    }

    private void ShowRoomsEmptyState(bool empty, string message = null)
    {
        if (roomsEmptyText == null) return;
        roomsEmptyText.gameObject.SetActive(empty);
        roomsEmptyText.text = message ?? $"NO OPEN MATCHES YET\n<size=15><color={UITheme.Hex(new Color(1f, 1f, 1f, 0.4f))}>CREATE ONE AND YOUR FRIENDS CAN JOIN</color></size>";
    }

    // Name on the left, map and mode in the middle, player count on the right
    private static void StyleRoomRow(GameObject row)
    {
        RectTransform rect = (RectTransform)row.transform;
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(RoomListWidth, 56f);
        UITheme.StyleButtonBackground(row.GetComponent<Button>(), UITheme.ButtonStyle.Secondary);
        // The prefab shrinks its texts with their scale; size them with the font instead
        foreach (RectTransform child in row.GetComponentsInChildren<RectTransform>(true)) child.localScale = Vector3.one;

        Text name = row.transform.Find("Name")?.GetComponent<Text>();
        if (name != null)
        {
            UITheme.StyleText(name, 20, UITheme.TextColor, TextAnchor.MiddleLeft);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.supportRichText = false;
            UITheme.Stretch(name.rectTransform);
            name.rectTransform.anchorMax = new Vector2(0.52f, 1f);
            name.rectTransform.offsetMin = new Vector2(18f, 0f);
        }

        RectTransform map = row.transform.Find("Map") as RectTransform;
        if (map != null)
        {
            UITheme.Stretch(map);
            map.anchorMin = new Vector2(0.52f, 0f);
            map.anchorMax = new Vector2(0.82f, 1f);
            Image mapImage = map.GetComponent<Image>();
            if (mapImage != null) mapImage.color = Color.clear;
            Text mapName = map.Find("Name")?.GetComponent<Text>();
            if (mapName != null)
            {
                UITheme.StyleText(mapName, 16, UITheme.MutedText, TextAnchor.MiddleLeft);
                mapName.horizontalOverflow = HorizontalWrapMode.Overflow;
                UITheme.Stretch(mapName.rectTransform);
            }
        }

        Text players = row.transform.Find("Players")?.GetComponent<Text>();
        if (players != null)
        {
            UITheme.StyleText(players, 20, UITheme.AccentText, TextAnchor.MiddleRight);
            players.horizontalOverflow = HorizontalWrapMode.Overflow;
            UITheme.Stretch(players.rectTransform);
            players.rectTransform.anchorMin = new Vector2(0.82f, 0f);
            players.rectTransform.offsetMax = new Vector2(-18f, 0f);
        }
    }

    #endregion

    #region Create tab

    private void BuildCreateTab()
    {
        Transform anchor = tabCreate.transform.Find("Anchor");
        if (anchor == null) return;

        // Match name field under the logo
        if (roomnameField != null)
        {
            RectTransform card = ColumnBlock(tabCreate, "Name Card", 1, 66f);
            Text caption = Caption(tabCreate.transform.Find("CreateTesxt"), "MATCH NAME");
            if (caption != null)
            {
                caption.transform.SetParent(card, false);
                UITheme.Row(caption.rectTransform, 0f, 18f);
            }
            roomnameField.transform.SetParent(card, false);
            UITheme.Row((RectTransform)roomnameField.transform, 22f, 44f);
            UITheme.StyleInput(roomnameField, "OPTIONAL");
        }

        foreach (Transform child in anchor)
        {
            Button button = child.GetComponent<Button>();
            Text label = button != null ? button.GetComponentInChildren<Text>() : null;
            if (label != null && (label == mapValue || label == modeValue || label == botsValue || label == winByValue))
            {
                UITheme.StyleSelector(button);
            }
            else if (child.GetComponentInChildren<Slider>(true) != null)
            {
                StyleSliderRow((RectTransform)child);
            }
        }

        Transform create = anchor.Find("Create");
        if (create != null)
        {
            UITheme.StyleButton(create.GetComponent<Button>(), UITheme.ButtonStyle.Primary);
            Text label = create.GetComponentInChildren<Text>();
            if (label != null) label.text = "CREATE MATCH";
        }
    }

    // A slider row in the create tab: name on the left, slider, value on the right
    private static void StyleSliderRow(RectTransform row)
    {
        row.sizeDelta = new Vector2(ColumnWidth, RowHeight);
        Image background = row.GetComponent<Image>();
        UITheme.Round(background, 8f);
        if (background != null) background.color = UITheme.Surface;

        Text name = row.Find("Text")?.GetComponent<Text>();
        if (name != null)
        {
            UITheme.StyleText(name, 15, UITheme.MutedText, TextAnchor.MiddleLeft);
            name.text = name.text.Trim();
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            UITheme.Stretch(name.rectTransform);
            name.rectTransform.anchorMax = new Vector2(0.45f, 1f);
            name.rectTransform.offsetMin = new Vector2(16f, 0f);
        }

        Slider slider = row.GetComponentInChildren<Slider>(true);
        if (slider != null)
        {
            // A little left of centre, leaving room on the right for a value like "20 MIN"
            UITheme.Place((RectTransform)slider.transform, new Vector2(0.5f, 0.5f), new Vector2(18f, 0f), new Vector2(92f, 20f));
            UITheme.StyleSlider(slider);
        }

        Text value = row.Find("Value")?.GetComponent<Text>();
        if (value != null)
        {
            UITheme.StyleText(value, 18, UITheme.TextColor, TextAnchor.MiddleRight);
            UITheme.Place(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(40f, 30f));
            // "20 MIN" is wider than the box: it grows to the left
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
        }
    }

    #endregion
}
