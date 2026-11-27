using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Settings panel inside the pause menu, in four tabs (LB / RB step through them):
//  - GAME: look sensitivity, stick sensitivity, aim sensitivity, field of view; sprint mode, invert Y
//    and camera effects (screen shake and speed FOV).
//  - AUDIO & VIDEO: master, effects and music volume; graphics quality, colours and the FPS counter.
//  - HUD: the crosshair's size, colour, centre dot and spread ticks, with a preview.
//  - CONTROLS: every keyboard and mouse binding, and the controller's buttons, each changed by
//    pressing its row and then the new key or button. Built at runtime from copies of the pause menu's own button,
// slider and label (styled by UITheme.StylePause), so the Pause prefab doesn't need editing.
// It also adds the pause menu's Customize button and panel (CustomizeMenu), under Settings.
// The main menu has the same panel (CreateForMainMenu), built from copies of its own button,
// slider and label, and opened by its own Settings button.
public class SettingsMenu : MenuPanel
{
    private Transform anchor;
    private GameObject mainButtons;
    private GameObject sensitivity;
    private GameObject panel;

    private Text xSensValue, ySensValue, stickValue, fovValue, adsValue, volumeValue, effectsValue, musicValue;
    private Slider xSensSlider, ySensSlider, stickSlider, fovSlider, adsSlider, volumeSlider, effectsSlider, musicSlider;
    private Text qualityText;
    private Text coloursText;
    private Text fpsText;
    private Text invertText;
    private Text sprintText;
    private Text shakeText;
    // The on / off settings' buttons, for their switches (UITheme.SetSwitch)
    private Button fpsButton, invertButton, shakeButton;

    private class BindingRow
    {
        public string name;
        public InputAction action;
        public int bindingIndex;
        public Text key;
        // A controller binding (rebound with a controller button) rather than a key
        public bool gamepad;
    }

    // The HUD tab: the crosshair settings and their preview
    private Slider crosshairSizeSlider;
    private Text crosshairSizeValue, crosshairColourText, crosshairDotText, crosshairTicksText;
    private Button crosshairDotButton, crosshairTicksButton;
    private Image previewDot;
    private readonly Image[] previewTicks = new Image[4];
    private readonly List<BindingRow> bindingRows = new List<BindingRow>();

    // Panel layout, in canvas units from the panel's centre
    private const float PanelWidth = 1040f;
    // 860 tall: on screens wider than 16:9 an in-match canvas can be shorter, so FitToScreen shrinks it
    private const float PanelHeight = 860f;
    private const float LeftColumn = -250f;
    private const float RightColumn = 250f;
    private const float ColumnWidth = 400f;

    // In the main menu rather than the pause menu
    private bool inMainMenu;

    public override bool IsOpen => panel != null && panel.activeSelf;
    // What was selected when the panel opened (its button), to go back to with a controller
    private Selectable returnTo;

    public static SettingsMenu Create(Pause pause)
    {
        return pause.gameObject.AddComponent<SettingsMenu>();
    }

    // For the main menu: the panel goes on canvas, hideWhileOpen (the menu column) is hidden while
    // it's open, and its buttons, sliders and labels are copies of the templates given. Open it
    // with Open().
    public static SettingsMenu CreateForMainMenu(Transform canvas, GameObject hideWhileOpen, GameObject buttonTemplate, Slider sliderTemplate, Text labelTemplate)
    {
        GameObject holder = new GameObject("Settings Menu", typeof(RectTransform));
        RectTransform rect = (RectTransform)holder.transform;
        rect.SetParent(canvas, false);
        rect.SetAsLastSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        SettingsMenu menu = holder.AddComponent<SettingsMenu>();
        menu.inMainMenu = true;
        menu.anchor = rect;
        menu.mainButtons = hideWhileOpen;
        menu.buttonTemplate = buttonTemplate;
        menu.sliderTemplate = sliderTemplate;
        menu.labelTemplate = labelTemplate;
        return menu;
    }

    public void Open()
    {
        if (panel == null) Build();
        if (panel != null) OpenSettings();
    }

    private void Build()
    {
        if (panel != null || buttonTemplate == null || sliderTemplate == null || labelTemplate == null) return;
        BuildPanel();
        panel.SetActive(false);
    }

    private void Start()
    {
        if (inMainMenu)
        {
            Build();
            return;
        }

        anchor = transform.GetChild(0);
        Transform buttons = anchor.Find("Buttons");
        Transform sens = anchor.Find("Sensitivity");
        Transform resume = buttons != null ? buttons.Find("Button (1)") : null;
        Transform quit = buttons != null ? buttons.Find("Button") : null;
        Transform xSens = sens != null ? sens.Find("XSens") : null;
        Transform label = sens != null ? sens.Find("Text") : null;

        if (resume == null || quit == null || xSens == null || label == null)
        {
            Debug.LogWarning("SettingsMenu: pause menu layout not found, settings button not added");
            enabled = false;
            return;
        }

        mainButtons = buttons.gameObject;
        sensitivity = sens.gameObject;
        // Sensitivity is now in the settings panel. The old sliders stay hidden in the prefab only
        // as style templates for the settings panel's sliders and labels.
        sensitivity.SetActive(false);
        buttonTemplate = resume.gameObject;
        sliderTemplate = xSens.GetComponent<Slider>();
        labelTemplate = label.GetComponent<Text>();

        // "Settings" and "Customize" go where Quit was, and Quit moves down below them
        RectTransform quitRect = (RectTransform)quit;
        GameObject settingsButton = NewButton(buttons, "SETTINGS", OpenSettings);
        settingsButton.transform.SetSiblingIndex(quit.GetSiblingIndex());
        ((RectTransform)settingsButton.transform).anchoredPosition = quitRect.anchoredPosition;
        quitRect.anchoredPosition -= new Vector2(0f, 60f);

        CustomizeMenu customize = CustomizeMenu.CreateForPause(gameObject, anchor, mainButtons, buttonTemplate, labelTemplate);
        GameObject customizeButton = NewButton(buttons, "CUSTOMIZE", customize.Open);
        customizeButton.transform.SetSiblingIndex(quit.GetSiblingIndex());
        ((RectTransform)customizeButton.transform).anchoredPosition = quitRect.anchoredPosition;
        quitRect.anchoredPosition -= new Vector2(0f, 60f);

        BuildPanel();
        panel.SetActive(false);
    }

    private void Update()
    {
        if (panel != null && panel.activeSelf) FitToScreen();
        // Unpausing hides the whole pause menu: go back to the main pause buttons for next time
        if (panel != null && panel.activeSelf && !anchor.gameObject.activeSelf) CloseSettings();
        // The controller's B closes the panel, in the main menu and the pause menu
        if (panel != null && panel.activeSelf && !GameInput.IsRebinding && !OptionPicker.Showing && MenuNavigation.TakeBack()) CloseSettings();
        // LB / RB step through the tabs
        Gamepad pad = Gamepad.current;
        if (panel != null && panel.activeSelf && pad != null && !GameInput.IsRebinding && !OptionPicker.Showing)
        {
            int step = pad.leftShoulder.wasPressedThisFrame ? -1 : pad.rightShoulder.wasPressedThisFrame ? 1 : 0;
            if (step != 0)
            {
                ShowTab(currentTab + step);
                MenuNavigation.Focus(FirstOnTab());
            }
        }
        // In the main menu, Esc closes the panel (in a match it unpauses, closing it as above)
        if (inMainMenu && panel != null && panel.activeSelf && !GameInput.IsRebinding && !OptionPicker.Showing)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) CloseSettings();
        }
    }

    #region Panel

    // Shrinks the panel if the screen is ever too short for it, never enlarges it. A safety net: the
    // match canvas keeps at least 1080 units of height (UITheme.ScaleConsistently) and the menu's
    // matches the height, so at 860 tall the panel normally fits as it is.
    private void FitToScreen()
    {
        Canvas canvas = panel.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) return;
        Transform parent = panel.transform.parent;
        float parentScale = parent != null ? parent.lossyScale.y : 1f;
        if (parentScale <= 0f) return;
        // The panel's full height on screen, in pixels, at its normal size
        float height = PanelHeight * parentScale;
        float fit = Mathf.Min(1f, Screen.height * 0.98f / height);
        panel.transform.localScale = new Vector3(fit, fit, 1f);
    }

    #region Tabs

    // The panel's four tabs, along the top under the title. LB / RB step through them with a controller.
    private static readonly string[] TabNames = { "GAME", "AUDIO & VIDEO", "HUD", "CONTROLS" };
    private readonly List<GameObject> tabPages = new List<GameObject>();
    private readonly List<Button> tabButtons = new List<Button>();
    private int currentTab;

    private const float TabWidth = 200f, TabHeight = 44f;
    private const float TabBarY = PanelHeight / 2f - 108f;
    // Where a tab's content starts (its section headings), and the step between rows
    private const float ContentTop = TabBarY - 64f;
    private const float SliderStep = 64f;
    private const float ToggleStep = 56f;
    // The controls tab's three columns: keyboard (two) and controller
    private const float BindingColumnWidth = 300f;
    private static readonly float[] BindingColumns = { -340f, 0f, 340f };
    private const float BindingStep = 40f;

    private RectTransform NewTabPage(RectTransform panelRect, string name)
    {
        RectTransform page = new GameObject("Tab " + name, typeof(RectTransform)).GetComponent<RectTransform>();
        page.SetParent(panelRect, false);
        UITheme.Stretch(page);
        tabPages.Add(page.gameObject);
        return page;
    }

    private void BuildTabBar(RectTransform panelRect)
    {
        float width = TabNames.Length * TabWidth + (TabNames.Length - 1) * UITheme.Space2;
        for (int i = 0; i < TabNames.Length; i++)
        {
            int index = i;
            GameObject tab = NewButton(panelRect, TabNames[i], () => ShowTab(index));
            Button button = tab.GetComponent<Button>();
            UITheme.StyleButton(button, UITheme.ButtonStyle.Ghost, UITheme.MenuLabelSize);
            Place(tab, new Vector2(-width / 2f + TabWidth / 2f + i * (TabWidth + UITheme.Space2), TabBarY), new Vector2(TabWidth, TabHeight));
            // The open tab is underlined in the accent
            Image underline = UITheme.NewImage("Underline", tab.transform, UITheme.Accent, 0f);
            UITheme.Place(underline.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(TabWidth - 24f, 3f));
            tabButtons.Add(button);
        }
        // A hairline under the whole bar
        Image rule = UITheme.NewImage("Tab Rule", panelRect, UITheme.Hairline, 0f);
        UITheme.Place(rule.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, TabBarY - TabHeight / 2f - 1f), new Vector2(PanelWidth - 80f, 1f));
    }

    private void ShowTab(int index)
    {
        if (tabPages.Count == 0) return;
        currentTab = (index % tabPages.Count + tabPages.Count) % tabPages.Count;
        for (int i = 0; i < tabPages.Count; i++)
        {
            bool open = i == currentTab;
            tabPages[i].SetActive(open);
            Transform underline = tabButtons[i].transform.Find("Underline");
            if (underline != null) underline.gameObject.SetActive(open);
            Text label = tabButtons[i].GetComponentInChildren<Text>();
            if (label != null) label.color = open ? UITheme.TextColor : UITheme.MutedText;
        }
    }

    // The first control on the open tab, for a controller
    private Selectable FirstOnTab()
    {
        if (tabPages.Count == 0) return null;
        return tabPages[currentTab].GetComponentInChildren<Selectable>();
    }

    #endregion

    #region Panel layout

    private void BuildPanel()
    {
        panel = new GameObject("Settings", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)panel.transform;
        rect.SetParent(anchor, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        Image background = panel.GetComponent<Image>();
        UITheme.PanelChrome(background);
        // Fades in each time it opens
        UITheme.Reveal(panel);

        Text title = NewLabel(rect, "SETTINGS", new Vector2(0f, PanelHeight / 2f - 52f), PanelWidth, UITheme.MenuTitleSize, TextAnchor.MiddleCenter);
        title.color = UITheme.TextColor;
        BuildTabBar(rect);

        BuildGameTab(NewTabPage(rect, "Game"));
        BuildAudioVideoTab(NewTabPage(rect, "Audio Video"));
        BuildHudTab(NewTabPage(rect, "Hud"));
        BuildControlsTab(NewTabPage(rect, "Controls"));

        GameObject done = NewButton(rect, "DONE", CloseSettings);
        UITheme.StyleButton(done.GetComponent<Button>(), UITheme.ButtonStyle.Primary);
        Place(done, new Vector2(0f, -PanelHeight / 2f + 58f), new Vector2(BindingColumnWidth, 52f));

        ShowTab(0);
        RefreshLabels();
        RefreshBindings();
    }

    private void Header(RectTransform parent, string text, float x, float width)
    {
        UITheme.SectionHeader(NewLabel(parent, text, new Vector2(x, ContentTop), width, UITheme.MenuCaptionSize, TextAnchor.MiddleLeft));
    }

    // A button for an on / off setting, with a switch on its right
    private Button SwitchRow(RectTransform parent, float x, float y, UnityAction onClick, out Text label)
    {
        GameObject row = NewButton(parent, "", onClick);
        Button button = row.GetComponent<Button>();
        UITheme.StyleSelector(button);
        UITheme.AddSwitch(button);
        Place(row, new Vector2(x, y), new Vector2(ColumnWidth, 48f));
        label = row.GetComponentInChildren<Text>();
        return button;
    }

    // A button that steps through a few values
    private Button CycleRow(RectTransform parent, float x, float y, UnityAction onClick, out Text label)
    {
        GameObject row = NewButton(parent, "", onClick);
        Button button = row.GetComponent<Button>();
        UITheme.StyleCycle(button);
        Place(row, new Vector2(x, y), new Vector2(ColumnWidth, 48f));
        label = row.GetComponentInChildren<Text>();
        return button;
    }

    // Look and movement: the sensitivities and field of view; sprint, invert Y and camera effects
    private void BuildGameTab(RectTransform page)
    {
        Header(page, "LOOK", LeftColumn, ColumnWidth);
        float y = ContentTop - 44f;
        xSensSlider = SliderRow(page, LeftColumn, "LOOK SENSITIVITY X", ref xSensValue, y, GameSettings.MinSensitivity, GameSettings.MaxSensitivity, true, GameSettings.XSensitivity, v =>
        {
            GameSettings.XSensitivity = v;
            RefreshLabels();
        });
        y -= SliderStep;
        ySensSlider = SliderRow(page, LeftColumn, "LOOK SENSITIVITY Y", ref ySensValue, y, GameSettings.MinSensitivity, GameSettings.MaxSensitivity, true, GameSettings.YSensitivity, v =>
        {
            GameSettings.YSensitivity = v;
            RefreshLabels();
        });
        y -= SliderStep;
        // Gamepad look, separate from the mouse: shown as degrees a second at a full push
        stickSlider = SliderRow(page, LeftColumn, "STICK SENSITIVITY", ref stickValue, y, GameSettings.MinStickSensitivity, GameSettings.MaxStickSensitivity, false, GameSettings.StickSensitivity, v =>
        {
            GameSettings.StickSensitivity = Mathf.Round(v * 20f) / 20f;
            RefreshLabels();
        });
        y -= SliderStep;
        adsSlider = SliderRow(page, LeftColumn, "AIM SENSITIVITY", ref adsValue, y, 0.1f, 2f, false, GameSettings.AdsSensitivity, v =>
        {
            GameSettings.AdsSensitivity = Mathf.Round(v * 20f) / 20f;
            RefreshLabels();
        });
        y -= SliderStep;
        fovSlider = SliderRow(page, LeftColumn, "FIELD OF VIEW", ref fovValue, y, GameSettings.MinFov, GameSettings.MaxFov, true, GameSettings.Fov, v =>
        {
            GameSettings.Fov = v;
            RefreshLabels();
        });

        Header(page, "MOVEMENT & VIEW", RightColumn, ColumnWidth);
        float toggle = ContentTop - 50f;
        CycleRow(page, RightColumn, toggle, () =>
        {
            GameSettings.ToggleSprint = !GameSettings.ToggleSprint;
            RefreshLabels();
        }, out sprintText);
        toggle -= ToggleStep;
        invertButton = SwitchRow(page, RightColumn, toggle, () =>
        {
            GameSettings.InvertY = !GameSettings.InvertY;
            RefreshLabels();
        }, out invertText);
        toggle -= ToggleStep;
        // Camera effects: screen shake and the field of view widening with speed
        shakeButton = SwitchRow(page, RightColumn, toggle, () =>
        {
            GameSettings.ScreenShake = !GameSettings.ScreenShake;
            RefreshLabels();
        }, out shakeText);
    }

    // The three volumes; graphics quality, colours and the FPS counter
    private void BuildAudioVideoTab(RectTransform page)
    {
        Header(page, "AUDIO", LeftColumn, ColumnWidth);
        float y = ContentTop - 44f;
        volumeSlider = SliderRow(page, LeftColumn, "MASTER VOLUME", ref volumeValue, y, 0f, 1f, false, GameSettings.Volume, v =>
        {
            GameSettings.Volume = v;
            RefreshLabels();
        });
        y -= SliderStep;
        effectsSlider = SliderRow(page, LeftColumn, "SOUND EFFECTS", ref effectsValue, y, 0f, 1f, false, GameSettings.EffectsVolume, v =>
        {
            GameSettings.EffectsVolume = v;
            RefreshLabels();
        });
        y -= SliderStep;
        // The menu's music and the end of a match's
        musicSlider = SliderRow(page, LeftColumn, "MUSIC", ref musicValue, y, 0f, 1f, false, GameSettings.MusicVolume, v =>
        {
            GameSettings.MusicVolume = v;
            RefreshLabels();
        });

        Header(page, "VIDEO", RightColumn, ColumnWidth);
        float toggle = ContentTop - 50f;
        Button qualityButton = null;
        GameObject quality = NewButton(page, "", () =>
        {
            string[] names = QualitySettings.names;
            if (names.Length == 0) return;
            string[] options = new string[names.Length];
            for (int i = 0; i < names.Length; i++) options[i] = names[i].ToUpperInvariant();
            OptionPicker.Open(qualityButton, options, GameSettings.Quality, i =>
            {
                GameSettings.Quality = i;
                RefreshLabels();
            });
        });
        qualityButton = quality.GetComponent<Button>();
        UITheme.StyleSelector(qualityButton);
        Place(quality, new Vector2(RightColumn, toggle), new Vector2(ColumnWidth, 48f));
        qualityText = quality.GetComponentInChildren<Text>();
        toggle -= ToggleStep;
        // Standard colours, or ones that stay apart with red-green colour blindness
        CycleRow(page, RightColumn, toggle, () =>
        {
            GameSettings.Colourblind = !GameSettings.Colourblind;
            RefreshLabels();
        }, out coloursText);
        toggle -= ToggleStep;
        // The frame rate readout (FpsCounter)
        fpsButton = SwitchRow(page, RightColumn, toggle, () =>
        {
            GameSettings.ShowFps = !GameSettings.ShowFps;
            RefreshLabels();
        }, out fpsText);
    }

    // The crosshair: size, colour, the dot and the spread ticks, with a preview of it
    private void BuildHudTab(RectTransform page)
    {
        Header(page, "CROSSHAIR", LeftColumn, ColumnWidth);
        float y = ContentTop - 44f;
        crosshairSizeSlider = SliderRow(page, LeftColumn, "SIZE", ref crosshairSizeValue, y, GameSettings.MinCrosshairSize, GameSettings.MaxCrosshairSize, false, GameSettings.CrosshairSize, v =>
        {
            GameSettings.CrosshairSize = Mathf.Round(v * 20f) / 20f;
            RefreshLabels();
        });
        float toggle = y - 76f;
        CycleRow(page, LeftColumn, toggle, () =>
        {
            GameSettings.CrosshairColourIndex = (GameSettings.CrosshairColourIndex + 1) % GameSettings.CrosshairColours.Length;
            RefreshLabels();
        }, out crosshairColourText);
        toggle -= ToggleStep;
        crosshairDotButton = SwitchRow(page, LeftColumn, toggle, () =>
        {
            GameSettings.CrosshairDot = !GameSettings.CrosshairDot;
            RefreshLabels();
        }, out crosshairDotText);
        toggle -= ToggleStep;
        crosshairTicksButton = SwitchRow(page, LeftColumn, toggle, () =>
        {
            GameSettings.CrosshairTicks = !GameSettings.CrosshairTicks;
            RefreshLabels();
        }, out crosshairTicksText);

        // A preview on a mid-grey plate (the colour has to read on light and dark walls alike)
        Header(page, "PREVIEW", RightColumn, ColumnWidth);
        Image plate = UITheme.NewImage("Crosshair Preview", page, new Color(0.38f, 0.42f, 0.46f, 1f), UITheme.ControlRadius);
        UITheme.Place(plate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(RightColumn, ContentTop - 150f), new Vector2(ColumnWidth, 220f));
        previewDot = UITheme.NewImage("Dot", plate.transform, Color.white, 0f);
        previewDot.sprite = UITheme.Rounded;
        previewDot.type = Image.Type.Simple;
        previewDot.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
        UITheme.Place(previewDot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5f, 5f));
        for (int i = 0; i < previewTicks.Length; i++)
        {
            previewTicks[i] = UITheme.NewImage("Tick", plate.transform, Color.white, 0f);
            previewTicks[i].gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.5f);
        }
    }

    // Every binding: keyboard and mouse in two columns, the controller in the third
    private void BuildControlsTab(RectTransform page)
    {
        Header(page, "KEYBOARD & MOUSE", BindingColumns[0], BindingColumnWidth);
        Header(page, "CONTROLLER", BindingColumns[2], BindingColumnWidth);

        int[] moveParts = GameInput.MovePartIndices();
        string[] moveNames = { "MOVE FORWARD", "MOVE BACK", "MOVE LEFT", "MOVE RIGHT" };
        List<BindingRow> keyboard = new List<BindingRow>();
        for (int i = 0; i < moveParts.Length && i < moveNames.Length; i++)
        {
            BindingRow row = AddBindingRow(page, moveNames[i], GameInput.Move, moveParts[i], false);
            if (row != null) keyboard.Add(row);
        }
        foreach (KeyValuePair<string, InputAction> entry in GameInput.Rebindable)
        {
            BindingRow row = AddBindingRow(page, entry.Key, entry.Value, GameInput.KeyboardBindingIndex(entry.Value), false);
            if (row != null) keyboard.Add(row);
        }
        List<BindingRow> controller = new List<BindingRow>();
        foreach (KeyValuePair<string, InputAction> entry in GameInput.GamepadRebindable)
        {
            BindingRow row = AddBindingRow(page, entry.Key, entry.Value, GameInput.GamepadBindingIndex(entry.Value), true);
            if (row != null) controller.Add(row);
        }

        // Keyboard rows down the first column, then the second; the controller's down the third
        float top = ContentTop - 40f;
        int perColumn = Mathf.CeilToInt(keyboard.Count / 2f);
        for (int i = 0; i < keyboard.Count; i++)
        {
            int column = i < perColumn ? 0 : 1;
            int at = column == 0 ? i : i - perColumn;
            Place(keyboard[i].key.transform.parent.gameObject, new Vector2(BindingColumns[column], top - at * BindingStep), new Vector2(BindingColumnWidth, BindingStep - 6f));
        }
        for (int i = 0; i < controller.Count; i++)
        {
            Place(controller[i].key.transform.parent.gameObject, new Vector2(BindingColumns[2], top - i * BindingStep), new Vector2(BindingColumnWidth, BindingStep - 6f));
        }

        float bottom = top - Mathf.Max(perColumn, controller.Count) * BindingStep - 10f;
        GameObject reset = NewButton(page, "RESET CONTROLS", () =>
        {
            GameInput.ResetBindings();
            RefreshBindings();
        });
        // A quiet ghost button: it undoes every binding
        UITheme.StyleButton(reset.GetComponent<Button>(), UITheme.ButtonStyle.Ghost, 18);
        Place(reset, new Vector2(BindingColumns[0], bottom), new Vector2(BindingColumnWidth, 40f));
        Text hint = NewLabel(page, "CLICK A ROW, THEN PRESS THE NEW KEY OR BUTTON", new Vector2(BindingColumns[1] + BindingColumnWidth / 2f, bottom), BindingColumnWidth * 2f, UITheme.MenuCaptionSize, TextAnchor.MiddleCenter);
        hint.color = UITheme.MutedText;
    }

    #endregion

    // Name on the left and value on the right above a full-width slider, in the column at x
    private Slider SliderRow(RectTransform parent, float x, string name, ref Text value, float y, float min, float max, bool wholeNumbers, float current, UnityAction<float> onChange)
    {
        NewLabel(parent, name, new Vector2(x, y), ColumnWidth, UITheme.MenuLabelSize, TextAnchor.MiddleLeft);
        value = NewLabel(parent, "", new Vector2(x, y), ColumnWidth, UITheme.MenuLabelSize, TextAnchor.MiddleRight);
        value.color = UITheme.AccentText;
        return NewSlider(parent, new Vector2(x, y - 26f), ColumnWidth, min, max, wholeNumbers, current, onChange);
    }

    // A row for one binding (placed by the caller), or null if the action has no binding of that kind
    private BindingRow AddBindingRow(RectTransform parent, string name, InputAction action, int bindingIndex, bool gamepad)
    {
        if (bindingIndex < 0) return null;

        BindingRow row = new BindingRow { name = name, action = action, bindingIndex = bindingIndex, gamepad = gamepad };
        GameObject button = NewButton(parent, "", () => Rebind(row));
        UITheme.StyleButtonBackground(button.GetComponent<Button>(), UITheme.ButtonStyle.Secondary);

        Text label = button.GetComponentInChildren<Text>();
        UITheme.StyleText(label, UITheme.MenuLabelSize, UITheme.TextColor, TextAnchor.MiddleLeft);
        UITheme.Inset(label.rectTransform, 14f, 14f);
        label.text = name;

        row.key = UITheme.NewText("Key", button.transform, UITheme.MenuLabelSize, UITheme.AccentText, TextAnchor.MiddleRight);
        UITheme.Inset(row.key.rectTransform, 14f, 14f);
        bindingRows.Add(row);
        return row;
    }

    private void Rebind(BindingRow row)
    {
        if (GameInput.IsRebinding) return;

        if (row.gamepad)
        {
            // Waits for the button that pressed the row (A) to be let go, so it isn't taken as the new one
            StartCoroutine(RebindGamepad(row));
            return;
        }
        // A keyboard binding pressed with a controller's A would wait for a key with no way to cancel
        // from the controller
        if (MenuNavigation.UsingGamepad) return;

        row.key.text = "PRESS A KEY  (ESC CANCELS)";
        row.key.color = UITheme.TextColor;
        GameInput.StartRebind(row.action, row.bindingIndex, RefreshBindings);
    }

    private System.Collections.IEnumerator RebindGamepad(BindingRow row)
    {
        GameInput.IsRebinding = true;
        row.key.text = "PRESS A BUTTON  (START CANCELS)";
        row.key.color = UITheme.TextColor;
        for (float wait = 0f; wait < 2f; wait += Time.unscaledDeltaTime)
        {
            Gamepad pad = Gamepad.current;
            if (pad == null || !pad.buttonSouth.isPressed) break;
            yield return null;
        }
        GameInput.StartGamepadRebind(row.action, row.bindingIndex, RefreshBindings);
    }

    private void RefreshBindings()
    {
        foreach (BindingRow row in bindingRows)
        {
            row.key.text = GameInput.BindingText(row.action, row.bindingIndex);
            row.key.color = UITheme.AccentText;
        }
    }

    private void RefreshLabels()
    {
        // With the mouse travel for a full turn at a common 800 DPI, so the number means something
        float cmPerTurn = LookRules.CentimetresPer360(Look.DegreesPerCount(GameSettings.XSensitivity), 800f);
        xSensValue.text = Mathf.RoundToInt(GameSettings.XSensitivity) + $"  <size=13><color={UITheme.Hex(UITheme.MutedText)}>{cmPerTurn:0.#} cm/360 @800 DPI</color></size>";
        ySensValue.text = Mathf.RoundToInt(GameSettings.YSensitivity).ToString();
        stickValue.text = Mathf.RoundToInt(Look.StickYawSpeed * GameSettings.StickSensitivity) + " DEG/S";
        fovValue.text = Mathf.RoundToInt(GameSettings.Fov).ToString();
        adsValue.text = GameSettings.AdsSensitivity.ToString("0.00") + "x";
        volumeValue.text = Mathf.RoundToInt(GameSettings.Volume * 100f) + "%";
        effectsValue.text = Mathf.RoundToInt(GameSettings.EffectsVolume * 100f) + "%";
        musicValue.text = Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%";
        qualityText.text = UITheme.Option("GRAPHICS", GameSettings.QualityName.ToUpperInvariant());
        // On / off settings: the name, and a switch showing the state
        if (fpsText != null) fpsText.text = "FPS COUNTER";
        UITheme.SetSwitch(fpsButton, GameSettings.ShowFps);
        if (sprintText != null) sprintText.text = UITheme.Option("SPRINT", GameSettings.ToggleSprint ? "TOGGLE" : "HOLD");
        if (invertText != null) invertText.text = "INVERT Y";
        UITheme.SetSwitch(invertButton, GameSettings.InvertY);
        if (shakeText != null) shakeText.text = "CAMERA EFFECTS";
        UITheme.SetSwitch(shakeButton, GameSettings.ScreenShake);
        if (coloursText != null) coloursText.text = UITheme.Option("COLOURS", GameSettings.Colourblind ? "COLOUR BLIND" : "STANDARD");

        // The crosshair
        if (crosshairSizeValue != null) crosshairSizeValue.text = Mathf.RoundToInt(GameSettings.CrosshairSize * 100f) + "%";
        if (crosshairColourText != null) crosshairColourText.text = UITheme.Option("COLOUR", GameSettings.CrosshairColourNames[GameSettings.CrosshairColourIndex]);
        if (crosshairDotText != null) crosshairDotText.text = "CENTRE DOT";
        UITheme.SetSwitch(crosshairDotButton, GameSettings.CrosshairDot);
        if (crosshairTicksText != null) crosshairTicksText.text = "SPREAD TICKS";
        UITheme.SetSwitch(crosshairTicksButton, GameSettings.CrosshairTicks);
        RefreshPreview();
    }

    // The crosshair preview, drawn as HudFeedback draws the real one (ticks at a resting spread)
    private void RefreshPreview()
    {
        if (previewDot == null) return;
        Color colour = GameSettings.CrosshairColour;
        float size = GameSettings.CrosshairSize;
        previewDot.enabled = GameSettings.CrosshairDot;
        previewDot.color = new Color(colour.r, colour.g, colour.b, 0.92f);
        previewDot.rectTransform.sizeDelta = Vector2.one * Mathf.Round(5f * size);
        float length = Mathf.Round(8f * size), width = Mathf.Max(2f, Mathf.Round(2f * size));
        for (int i = 0; i < previewTicks.Length; i++)
        {
            Image tick = previewTicks[i];
            tick.enabled = GameSettings.CrosshairTicks;
            tick.color = new Color(colour.r, colour.g, colour.b, 0.8f);
            Vector2 direction = i == 0 ? Vector2.up : i == 1 ? Vector2.right : i == 2 ? Vector2.down : Vector2.left;
            UITheme.Place(tick.rectTransform, new Vector2(0.5f, 0.5f), direction * (11f + length / 2f), i % 2 == 0 ? new Vector2(width, length) : new Vector2(length, width));
        }
    }

    private void OpenSettings()
    {
        returnTo = MenuNavigation.Current;
        mainButtons.SetActive(false);
        panel.SetActive(true);

        xSensSlider.SetValueWithoutNotify(GameSettings.XSensitivity);
        ySensSlider.SetValueWithoutNotify(GameSettings.YSensitivity);
        stickSlider.SetValueWithoutNotify(GameSettings.StickSensitivity);
        fovSlider.SetValueWithoutNotify(GameSettings.Fov);
        adsSlider.SetValueWithoutNotify(GameSettings.AdsSensitivity);
        volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
        effectsSlider.SetValueWithoutNotify(GameSettings.EffectsVolume);
        musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
        if (crosshairSizeSlider != null) crosshairSizeSlider.SetValueWithoutNotify(GameSettings.CrosshairSize);
        RefreshLabels();
        RefreshBindings();
        // Opens on the tab it was left on
        ShowTab(currentTab);
        MenuNavigation.Focus(FirstOnTab());
    }

    private void CloseSettings()
    {
        panel.SetActive(false);
        mainButtons.SetActive(true);
        MenuNavigation.Focus(returnTo);
    }

    #endregion
}
