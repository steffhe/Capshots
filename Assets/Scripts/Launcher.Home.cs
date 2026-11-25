using UnityEngine;
using UnityEngine.UI;

// The main menu's front page, rearranged at runtime on top of the scene's column:
//  - QUICK PLAY first, big and in the logo's blue with a soft pulse, so a new player's eye goes
//    straight to it. Under it, "play with friends": the room list and creating a match, side by side.
//  - PRACTICE VS BOTS, with a line saying what it is, then Settings and Quit (not in a browser).
//  - CUSTOMIZE DRONE under the drone in the scene, saying what it changes, with a NEW badge while
//    there are hats unlocked since Customize was last opened.
//  - The player's name, level and XP on a card of their own in the top right, with a pencil
//    button to edit the name; the build's version in the bottom right corner.
//  - A controller starts on Quick Play (MenuNavigation), and B goes back from the other tabs.
public partial class Launcher
{
    public static Launcher Instance { get; private set; }

    private const float QuickPlayHeight = 84f;
    private const float FriendsRowHeight = 50f;
    private const float PracticeHeight = 66f;

    private Button quickPlayButton;
    private Image quickPlayPulse;
    private GameObject newHatsBadge;
    private RectTransform profileCard;
    private Button editNameButton;
    private Button practiceButton;

    // Finds a button in the main column by (part of) its label
    private Button ColumnButton(string labelPart)
    {
        Transform anchor = tabMain != null ? tabMain.transform.Find("Anchor") : null;
        if (anchor == null) return null;
        foreach (Transform child in anchor)
        {
            Button button = child.GetComponent<Button>();
            Text label = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (label != null && label.text.ToUpperInvariant().Contains(labelPart)) return button;
        }
        return null;
    }

    private void BuildHome()
    {
        Transform anchor = tabMain != null ? tabMain.transform.Find("Anchor") : null;
        if (anchor == null) return;

        Button roomList = ColumnButton("ROOM");
        Button create = ColumnButton("CREATE");
        Button practice = ColumnButton("PRACTICE");
        practiceButton = practice;
        Button customize = ColumnButton("CUSTOMIZE");
        Button template = roomList != null ? roomList : practice;
        if (template == null) return;

        // Logo, then Quick Play
        Transform logo = anchor.Find("Logo");
        int index = logo != null ? logo.GetSiblingIndex() + 1 : 0;
        quickPlayButton = CopyButton(template, anchor, "Quick Play", Join);
        quickPlayButton.transform.SetSiblingIndex(index++);
        ((RectTransform)quickPlayButton.transform).sizeDelta = new Vector2(ColumnWidth, QuickPlayHeight);
        UITheme.StyleButton(quickPlayButton, UITheme.ButtonStyle.Primary, 30);
        TwoLineLabel(quickPlayButton, "QUICK PLAY", "JOIN A MATCH, OR START ONE WITH BOTS", 30);
        Outline glow = quickPlayButton.gameObject.AddComponent<Outline>();
        glow.effectColor = new Color(UITheme.AccentText.r, UITheme.AccentText.g, UITheme.AccentText.b, 0.55f);
        glow.effectDistance = new Vector2(2f, -2f);
        // A soft light over the button that slowly brightens and fades (see Update)
        quickPlayPulse = UITheme.NewImage("Pulse", quickPlayButton.transform, new Color(1f, 1f, 1f, 0f), 8f);
        UITheme.Stretch(quickPlayPulse.rectTransform);
        quickPlayPulse.transform.SetAsFirstSibling();

        // "Or play with friends": the room list and creating a match, side by side
        if (roomList != null && create != null)
        {
            RectTransform caption = ColumnBlock(tabMain, "Friends Caption", index++, 18f);
            Text captionText = UITheme.NewText("Text", caption, 14, UITheme.MutedText, TextAnchor.MiddleLeft);
            captionText.text = "OR PLAY WITH FRIENDS";

            RectTransform row = ColumnBlock(tabMain, "Friends", index++, FriendsRowHeight);
            HalfOf(roomList, row, 0f, 0.5f, "ROOM LIST");
            HalfOf(create, row, 0.5f, 1f, "CREATE MATCH");
        }

        // A little space before the rest
        ColumnBlock(tabMain, "Gap", index++, 6f);

        if (practice != null)
        {
            practice.transform.SetSiblingIndex(index++);
            ((RectTransform)practice.transform).sizeDelta = new Vector2(ColumnWidth, PracticeHeight);
            TwoLineLabel(practice, "PRACTICE VS BOTS", "OFFLINE  ·  TRY THE GUNS AND YOUR AIM", 22);
        }

        if (customize != null) BuildCustomizeButton(customize);

        BuildVersionTag();
        // Text fields are left out of controller navigation: moving onto one would start typing
        // (and on a phone, pop up its keyboard)
        if (roomnameField != null) roomnameField.navigation = new Navigation { mode = Navigation.Mode.None };
        if (joinCodeField != null) joinCodeField.navigation = new Navigation { mode = Navigation.Mode.None };
        WireHomeNavigation(anchor);
        MenuNavigation.Home = quickPlayButton;
        MenuNavigation.Focus(quickPlayButton);
    }

    // A controller's way round the front page, set out by hand. Unity's automatic navigation picks
    // what's most nearly straight below, so from Quick Play it went past Room List and Create Match
    // (to the sides, in their row) to Practice. Here the column goes row by row, top to bottom:
    // down from Quick Play reaches the friends row, left and right move along a row, right from
    // the column reaches Customize, and up from Quick Play reaches the name's edit button.
    private void WireHomeNavigation(Transform anchor)
    {
        // The column's rows, top to bottom: each a button of its own, or a row of them side by side
        var rows = new System.Collections.Generic.List<System.Collections.Generic.List<Selectable>>();
        foreach (Transform child in anchor)
        {
            if (!child.gameObject.activeSelf) continue;
            var row = new System.Collections.Generic.List<Selectable>();
            Selectable own = child.GetComponent<Selectable>();
            if (own != null) row.Add(own);
            else
            {
                foreach (Transform inner in child)
                {
                    Selectable s = inner.GetComponent<Selectable>();
                    if (s != null && inner.gameObject.activeSelf) row.Add(s);
                }
            }
            row.RemoveAll(s => s.navigation.mode == Navigation.Mode.None);
            if (row.Count > 0) rows.Add(row);
        }
        if (rows.Count == 0) return;

        Button customize = customizeRect != null ? customizeRect.GetComponent<Button>() : null;
        for (int i = 0; i < rows.Count; i++)
        {
            for (int j = 0; j < rows[i].Count; j++)
            {
                Navigation nav = new Navigation { mode = Navigation.Mode.Explicit, wrapAround = false };
                // Up and down: the button in the same place in the next row (or that row's last)
                if (i > 0) nav.selectOnUp = rows[i - 1][Mathf.Min(j, rows[i - 1].Count - 1)];
                else nav.selectOnUp = editNameButton;
                if (i < rows.Count - 1) nav.selectOnDown = rows[i + 1][Mathf.Min(j, rows[i + 1].Count - 1)];
                if (j > 0) nav.selectOnLeft = rows[i][j - 1];
                if (j < rows[i].Count - 1) nav.selectOnRight = rows[i][j + 1];
                else nav.selectOnRight = customize;
                rows[i][j].navigation = nav;
            }
        }

        // Customize, out under the drone: left goes back to the column, to Practice if it's there
        if (customize != null)
        {
            Selectable back = practiceButton != null ? practiceButton : rows[0][0];
            customize.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = back };
        }
        // The edit button: down goes back to Quick Play
        if (editNameButton != null)
        {
            editNameButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = rows[0][0], selectOnLeft = rows[0][0] };
        }
    }

    // A copy of one of the column's buttons, doing onClick instead of what the original does
    private Button CopyButton(Button template, Transform parent, string name, UnityEngine.Events.UnityAction onClick)
    {
        GameObject copy = Instantiate(template.gameObject, parent);
        copy.name = name;
        Button button = copy.GetComponent<Button>();
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) is Launcher || button.onClick.GetPersistentTarget(i) is MainMenu)
            {
                button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
            }
        }
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);
        return button;
    }

    // A big title with a small, quieter line under it saying what the button does
    private static void TwoLineLabel(Button button, string title, string subtitle, int titleSize)
    {
        Text label = button.GetComponentInChildren<Text>(true);
        if (label == null) return;
        label.supportRichText = true;
        label.fontSize = titleSize;
        label.lineSpacing = 0.9f;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.text = $"{title}\n<size=13><color={UITheme.Hex(new Color(1f, 1f, 1f, 0.72f))}>{subtitle}</color></size>";
    }

    // Moves a column button into a side-by-side row, filling part of its width
    private static void HalfOf(Button button, RectTransform row, float xMin, float xMax, string text)
    {
        RectTransform rect = (RectTransform)button.transform;
        rect.SetParent(row, false);
        rect.anchorMin = new Vector2(xMin, 0f);
        rect.anchorMax = new Vector2(xMax, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(xMin > 0f ? 5f : 0f, 0f);
        rect.offsetMax = new Vector2(xMax < 1f ? -5f : 0f, 0f);
        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = text;
            label.fontSize = 17;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
        }
    }

    // A small glowing pill on the right of a button, like "NEW"
    private static GameObject AddBadge(Button button, string text)
    {
        Image pill = UITheme.NewImage("Badge", button.transform, UITheme.AccentText, 11f);
        UITheme.Place(pill.rectTransform, new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(52f, 22f));
        Outline glow = pill.gameObject.AddComponent<Outline>();
        glow.effectColor = new Color(UITheme.AccentText.r, UITheme.AccentText.g, UITheme.AccentText.b, 0.4f);
        glow.effectDistance = new Vector2(2f, -2f);
        Text label = UITheme.NewText("Text", pill.transform, 13, new Color(0.02f, 0.05f, 0.1f), TextAnchor.MiddleCenter);
        label.fontStyle = FontStyle.Bold;
        label.text = text;
        pill.gameObject.SetActive(false);
        return pill.gameObject;
    }

    #region Customize button

    // Customize stands under the drone in the scene, saying what it changes, rather than in the
    // column with the ways to play: it's about the drone, so it sits with it. It follows the drone's
    // place on screen as the window changes shape, staying clear of the column and on screen.
    private const float CustomizeWidth = 270f;
    private const float CustomizeHeight = 62f;
    private const float CustomizeGap = 28f; // between the column and the button, and round the screen's edges
    private RectTransform customizeRect;
    private Transform menuModel;
    private Collider menuModelCollider;

    private void BuildCustomizeButton(Button customize)
    {
        Transform canvas = tabMain.transform.parent;
        customizeRect = (RectTransform)customize.transform;
        customizeRect.SetParent(canvas, false);
        customizeRect.anchorMin = customizeRect.anchorMax = new Vector2(0.5f, 0.5f);
        customizeRect.pivot = new Vector2(0.5f, 1f);
        customizeRect.sizeDelta = new Vector2(CustomizeWidth, CustomizeHeight);
        TwoLineLabel(customize, "CUSTOMIZE DRONE", "HATS  ·  WEAPONS", 20);
        newHatsBadge = AddBadge(customize, "NEW");
        // The badge sits on the button's top right corner, so it doesn't cover the label
        UITheme.Place((RectTransform)newHatsBadge.transform, new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(52f, 22f));

        // The drone in the scene: the model whose head the menu dresses (see DressMenuModel)
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!t.CompareTag("Head") || t.GetComponent<MeshFilter>() == null) continue;
            menuModel = t.parent != null ? t.parent : t;
            menuModelCollider = menuModel.GetComponent<Collider>();
            break;
        }
        PlaceCustomizeButton();
    }

    // Puts the button just under the drone's feet on screen
    private void PlaceCustomizeButton()
    {
        if (customizeRect == null) return;
        RectTransform canvas = (RectTransform)customizeRect.parent;
        Canvas root = canvas.GetComponentInParent<Canvas>().rootCanvas;
        Camera uiCamera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        Camera sceneCamera = Camera.main;
        Rect area = canvas.rect;

        // Where it wants to be: under the drone, or failing that, low down right of the middle
        Vector2 wanted = new Vector2(area.width * 0.15f, -area.height * 0.25f);
        if (menuModel != null && sceneCamera != null)
        {
            Vector3 feet = menuModelCollider != null
                ? new Vector3(menuModelCollider.bounds.center.x, menuModelCollider.bounds.min.y, menuModelCollider.bounds.center.z)
                : menuModel.position;
            Vector3 screen = sceneCamera.WorldToScreenPoint(feet);
            Vector2 local;
            if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, uiCamera, out local))
            {
                wanted = local + new Vector2(0f, -16f);
            }
        }

        // Clear of the column on the left, and on screen
        float left = area.xMin + CustomizeGap;
        Transform column = tabMain.transform.Find("Anchor");
        if (column is RectTransform columnRect)
        {
            Vector3[] corners = new Vector3[4];
            columnRect.GetWorldCorners(corners);
            left = Mathf.Max(left, canvas.InverseTransformPoint(corners[2]).x + CustomizeGap);
        }
        float x = Mathf.Clamp(wanted.x, left + CustomizeWidth / 2f, Mathf.Max(left + CustomizeWidth / 2f, area.xMax - CustomizeGap - CustomizeWidth / 2f));
        float y = Mathf.Clamp(wanted.y, area.yMin + CustomizeGap + CustomizeHeight, area.yMax - CustomizeGap);
        customizeRect.anchoredPosition = new Vector2(x, y);
    }

    #endregion

    // The build's version, small, in the bottom right corner
    private void BuildVersionTag()
    {
        Transform canvas = tabMain.transform.parent;
        Text version = UITheme.NewText("Version", canvas, 14, new Color(1f, 1f, 1f, 0.35f), TextAnchor.LowerRight);
        UITheme.Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(-20f, 14f), new Vector2(300f, 20f));
        string number = Application.version;
        version.text = string.IsNullOrEmpty(number) ? "" : char.IsDigit(number[0]) ? "v" + number : number;
    }

    #region Profile card

    // The name (with a pencil button to edit it), level and an XP bar, on a card in the top right
    private void BuildProfileCard()
    {
        Transform profile = tabMain.transform.Find("Profile");
        if (profile == null || usernameField == null) return;
        Transform canvas = tabMain.transform.parent;

        Image cardImage = UITheme.NewImage("Profile Card", canvas, UITheme.Panel, 12f);
        UITheme.AddShadow(cardImage, 0.4f);
        profileCard = cardImage.rectTransform;
        UITheme.Place(profileCard, new Vector2(1f, 1f), new Vector2(-32f, -28f), new Vector2(360f, 112f));
        RectTransform content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(profileCard, false);
        UITheme.Stretch(content);
        content.offsetMin = new Vector2(18f, 14f);
        content.offsetMax = new Vector2(-18f, -14f);

        // The name reads as a name, not a form field: no box until pointed at or typed in
        RectTransform field = (RectTransform)usernameField.transform;
        field.SetParent(content, false);
        UITheme.Row(field, 0f, 40f);
        field.offsetMin = new Vector2(-10f, field.offsetMin.y);
        field.offsetMax = new Vector2(-46f, field.offsetMax.y);
        UITheme.StyleInput(usernameField, "ENTER YOUR NAME", 24);
        ColorBlock colors = usernameField.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = UITheme.Field;
        usernameField.colors = colors;
        if (usernameField.textComponent != null) usernameField.textComponent.fontStyle = FontStyle.Bold;
        // Left out of controller navigation (moving onto it would start typing): the pencil button edits it
        usernameField.navigation = new Navigation { mode = Navigation.Mode.None };
        usernameField.onEndEdit.AddListener(_ =>
        {
            VerifyUsername();
            usernameField.text = myProfile.username;
            Data.SaveProfile(myProfile);
        });

        Button pencil = PencilButton(content);
        editNameButton = pencil;
        pencil.onClick.AddListener(() =>
        {
            usernameField.Select();
            usernameField.ActivateInputField();
        });

        // The scene's own caption isn't needed: the name speaks for itself
        Transform caption = profile.Find("ProfileText");
        if (caption != null) caption.gameObject.SetActive(false);

        levelText = profile.Find("Level")?.GetComponent<Text>();
        if (levelText != null)
        {
            levelText.transform.SetParent(content, false);
            UITheme.StyleText(levelText, 16, UITheme.AccentText, TextAnchor.MiddleLeft);
            UITheme.Row(levelText.rectTransform, 48f, 20f, 0f, 0.5f);
        }

        xpText = profile.Find("XP")?.GetComponent<Text>();
        if (xpText != null)
        {
            xpText.transform.SetParent(content, false);
            UITheme.StyleText(xpText, 14, UITheme.MutedText, TextAnchor.MiddleRight);
            UITheme.Row(xpText.rectTransform, 48f, 20f, 0.5f, 1f);
        }

        Image track = UITheme.NewImage("XP Bar", content, UITheme.Track, 3f);
        UITheme.Row(track.rectTransform, 76f, 6f);
        Image fill = UITheme.NewImage("Fill", track.transform, UITheme.AccentText, 3f);
        xpFill = fill.rectTransform;
        UITheme.Stretch(xpFill);
    }

    // A small square button with a pencil drawn on it, at the right of the name
    private static Button PencilButton(RectTransform parent)
    {
        Image background = UITheme.NewImage("Edit Name", parent, Color.white, 8f);
        background.raycastTarget = true;
        UITheme.Place(background.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(36f, 36f));
        Button button = background.gameObject.AddComponent<Button>();
        UITheme.StyleButtonBackground(button, UITheme.ButtonStyle.Secondary, 8f);

        // The pencil: a body, a pointed tip and an eraser end, at 45 degrees
        RectTransform pencil = new GameObject("Pencil", typeof(RectTransform)).GetComponent<RectTransform>();
        pencil.SetParent(background.transform, false);
        pencil.anchorMin = pencil.anchorMax = pencil.pivot = new Vector2(0.5f, 0.5f);
        pencil.sizeDelta = new Vector2(6f, 20f);
        pencil.localEulerAngles = new Vector3(0f, 0f, -45f);
        Image body = UITheme.NewImage("Body", pencil, UITheme.TextColor, 1f);
        UITheme.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(6f, 13f));
        Image eraser = UITheme.NewImage("Eraser", pencil, UITheme.AccentText, 1f);
        UITheme.Place(eraser.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(6f, 3f));
        // The tip: a small square turned 45 degrees, half of it hidden under the body
        Image tip = UITheme.NewImage("Tip", pencil, UITheme.TextColor, 0f);
        UITheme.Place(tip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -5.5f), new Vector2(4.2f, 4.2f));
        tip.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        return button;
    }

    #endregion

    private void Update()
    {
        // The profile card belongs to the front page: hidden on the other tabs and panels
        bool home = tabMain != null && tabMain.activeInHierarchy;
        if (profileCard != null && profileCard.gameObject.activeSelf != home) profileCard.gameObject.SetActive(home);
        // So is the Customize button, which follows the drone as the window changes shape
        if (customizeRect != null)
        {
            if (customizeRect.gameObject.activeSelf != home) customizeRect.gameObject.SetActive(home);
            if (home) PlaceCustomizeButton();
        }

        if (newHatsBadge != null)
        {
            bool fresh = GameSettings.HasNewHats;
            if (newHatsBadge.activeSelf != fresh) newHatsBadge.SetActive(fresh);
            if (fresh) newHatsBadge.transform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 4f));
        }

        if (quickPlayPulse != null)
        {
            float pulse = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * 2f);
            quickPlayPulse.color = new Color(1f, 1f, 1f, 0.12f * pulse);
        }

        // Back to the front page from the room list or the create tab: Esc, or a controller's B
        bool onSubTab = (tabRooms != null && tabRooms.activeSelf) || (tabCreate != null && tabCreate.activeSelf);
        if (onSubTab && !OptionPicker.Showing && !LoadingScreen.Visible)
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            bool escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !AnyFieldFocused();
            if (escape || MenuNavigation.TakeBack()) TabOpenMain();
        }
    }

    private bool AnyFieldFocused()
    {
        return (roomnameField != null && roomnameField.isFocused) || (joinCodeField != null && joinCodeField.isFocused);
    }
}
