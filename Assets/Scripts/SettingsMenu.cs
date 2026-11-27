using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Settings panel inside the pause menu: look sensitivity, stick sensitivity, ADS sensitivity, field of view, master,
// effects and music volume, graphics quality, colours, the FPS counter, key bindings, invert Y and
// camera effects (screen shake and speed FOV) and the sprint mode. Built at runtime from copies of the pause menu's own button,
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
    }
    private readonly List<BindingRow> bindingRows = new List<BindingRow>();

    // Panel layout, in canvas units from the panel's centre
    private const float PanelWidth = 1040f;
    // 860 tall: on screens wider than 16:9 an in-match canvas can be shorter, so FitToScreen shrinks it
    private const float PanelHeight = 860f;
    private const float LeftColumn = -250f;
    private const float RightColumn = 250f;
    private const float ColumnWidth = 400f;
    private const float HeaderY = 320f;

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
        // In the main menu, Esc closes the panel (in a match it unpauses, closing it as above)
        if (inMainMenu && panel != null && panel.activeSelf && !GameInput.IsRebinding && !OptionPicker.Showing)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) CloseSettings();
        }
    }

    #region Panel

    // Shrinks the panel when the screen is too short for it (an in-match canvas scales with the
    // screen's width, so on a screen wider than 16:9 it's shorter than 1080), never enlarges it
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

        Text title = NewLabel(rect, "SETTINGS", new Vector2(0f, PanelHeight / 2f - 48f), PanelWidth, UITheme.MenuTitleSize, TextAnchor.MiddleCenter);
        title.color = UITheme.TextColor;

        // Left column: sliders, then the graphics and reset buttons
        UITheme.SectionHeader(NewLabel(rect, "GENERAL", new Vector2(LeftColumn, HeaderY), ColumnWidth, UITheme.MenuCaptionSize, TextAnchor.MiddleLeft));

        float y = HeaderY - 44f;
        const float rowStep = 50f;
        xSensSlider = SliderRow(rect, "LOOK SENSITIVITY X", ref xSensValue, y, GameSettings.MinSensitivity, GameSettings.MaxSensitivity, true, GameSettings.XSensitivity, v =>
        {
            GameSettings.XSensitivity = v;
            RefreshLabels();
        });
        y -= rowStep;
        ySensSlider = SliderRow(rect, "LOOK SENSITIVITY Y", ref ySensValue, y, GameSettings.MinSensitivity, GameSettings.MaxSensitivity, true, GameSettings.YSensitivity, v =>
        {
            GameSettings.YSensitivity = v;
            RefreshLabels();
        });
        y -= rowStep;
        // Gamepad look, separate from the mouse: shown as degrees a second at a full push
        stickSlider = SliderRow(rect, "STICK SENSITIVITY", ref stickValue, y, GameSettings.MinStickSensitivity, GameSettings.MaxStickSensitivity, false, GameSettings.StickSensitivity, v =>
        {
            GameSettings.StickSensitivity = Mathf.Round(v * 20f) / 20f;
            RefreshLabels();
        });
        y -= rowStep;
        adsSlider = SliderRow(rect, "AIM SENSITIVITY", ref adsValue, y, 0.1f, 2f, false, GameSettings.AdsSensitivity, v =>
        {
            GameSettings.AdsSensitivity = Mathf.Round(v * 20f) / 20f;
            RefreshLabels();
        });
        y -= rowStep;
        fovSlider = SliderRow(rect, "FIELD OF VIEW", ref fovValue, y, GameSettings.MinFov, GameSettings.MaxFov, true, GameSettings.Fov, v =>
        {
            GameSettings.Fov = v;
            RefreshLabels();
        });
        y -= rowStep;
        volumeSlider = SliderRow(rect, "MASTER VOLUME", ref volumeValue, y, 0f, 1f, false, GameSettings.Volume, v =>
        {
            GameSettings.Volume = v;
            RefreshLabels();
        });
        y -= rowStep;
        effectsSlider = SliderRow(rect, "SOUND EFFECTS", ref effectsValue, y, 0f, 1f, false, GameSettings.EffectsVolume, v =>
        {
            GameSettings.EffectsVolume = v;
            RefreshLabels();
        });
        y -= rowStep;
        // The menu's music and the end of a match's
        musicSlider = SliderRow(rect, "MUSIC", ref musicValue, y, 0f, 1f, false, GameSettings.MusicVolume, v =>
        {
            GameSettings.MusicVolume = v;
            RefreshLabels();
        });

        Button qualityButton = null;
        GameObject quality = NewButton(rect, "", () =>
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
        UITheme.StyleSelector(quality.GetComponent<Button>());
        Place(quality, new Vector2(LeftColumn, y - 66f), new Vector2(ColumnWidth, 48f));
        qualityText = quality.GetComponentInChildren<Text>();

        // Standard colours, or ones that stay apart with red-green colour blindness
        GameObject colours = NewButton(rect, "", () =>
        {
            GameSettings.Colourblind = !GameSettings.Colourblind;
            RefreshLabels();
        });
        UITheme.StyleCycle(colours.GetComponent<Button>());
        Place(colours, new Vector2(LeftColumn, y - 122f), new Vector2(ColumnWidth, 48f));
        coloursText = colours.GetComponentInChildren<Text>();

        // The frame rate readout in the corner (FpsCounter)
        GameObject fps = NewButton(rect, "", () =>
        {
            GameSettings.ShowFps = !GameSettings.ShowFps;
            RefreshLabels();
        });
        UITheme.StyleSelector(fps.GetComponent<Button>());
        UITheme.AddSwitch(fps.GetComponent<Button>());
        Place(fps, new Vector2(LeftColumn, y - 178f), new Vector2(ColumnWidth, 48f));
        fpsText = fps.GetComponentInChildren<Text>();
        fpsButton = fps.GetComponent<Button>();

        GameObject reset = NewButton(rect, "RESET CONTROLS", () =>
        {
            GameInput.ResetBindings();
            RefreshBindings();
        });
        // A quiet ghost button: it undoes your bindings
        UITheme.StyleButton(reset.GetComponent<Button>(), UITheme.ButtonStyle.Ghost, 18);
        // An 8 gap between the buttons, so eight sliders fit above them
        Place(reset, new Vector2(LeftColumn, y - 234f), new Vector2(ColumnWidth, 48f));

        GameObject done = NewButton(rect, "DONE", CloseSettings);
        UITheme.StyleButton(done.GetComponent<Button>(), UITheme.ButtonStyle.Primary);
        Place(done, new Vector2(LeftColumn, -PanelHeight / 2f + 58f), new Vector2(ColumnWidth, 52f));

        // Right column: key bindings
        // Its rule stops short of the hint on the right
        Text controls = NewLabel(rect, "CONTROLS", new Vector2(RightColumn - 70f, HeaderY), ColumnWidth - 140f, UITheme.MenuCaptionSize, TextAnchor.MiddleLeft);
        UITheme.SectionHeader(controls);
        NewLabel(rect, "CLICK TO CHANGE", new Vector2(RightColumn, HeaderY), ColumnWidth, UITheme.MenuCaptionSize, TextAnchor.MiddleRight).color = UITheme.MutedText;

        int[] moveParts = GameInput.MovePartIndices();
        string[] moveNames = { "MOVE FORWARD", "MOVE BACK", "MOVE LEFT", "MOVE RIGHT" };
        for (int i = 0; i < moveParts.Length && i < moveNames.Length; i++)
        {
            AddBindingRow(rect, moveNames[i], GameInput.Move, moveParts[i]);
        }
        foreach (KeyValuePair<string, InputAction> entry in GameInput.Rebindable)
        {
            AddBindingRow(rect, entry.Key, entry.Value, GameInput.KeyboardBindingIndex(entry.Value));
        }

        // Under the bindings, level with DONE: camera effects (screen shake and speed FOV), invert Y above it
        // and the sprint mode above that
        float toggleY = -PanelHeight / 2f + 58f;
        GameObject shake = NewButton(rect, "", () =>
        {
            GameSettings.ScreenShake = !GameSettings.ScreenShake;
            RefreshLabels();
        });
        UITheme.StyleSelector(shake.GetComponent<Button>());
        UITheme.AddSwitch(shake.GetComponent<Button>());
        Place(shake, new Vector2(RightColumn, toggleY), new Vector2(ColumnWidth, 48f));
        shakeText = shake.GetComponentInChildren<Text>();
        shakeButton = shake.GetComponent<Button>();

        GameObject invert = NewButton(rect, "", () =>
        {
            GameSettings.InvertY = !GameSettings.InvertY;
            RefreshLabels();
        });
        UITheme.StyleSelector(invert.GetComponent<Button>());
        UITheme.AddSwitch(invert.GetComponent<Button>());
        Place(invert, new Vector2(RightColumn, toggleY + 56f), new Vector2(ColumnWidth, 48f));
        invertText = invert.GetComponentInChildren<Text>();
        invertButton = invert.GetComponent<Button>();

        GameObject sprintMode = NewButton(rect, "", () =>
        {
            GameSettings.ToggleSprint = !GameSettings.ToggleSprint;
            RefreshLabels();
        });
        UITheme.StyleCycle(sprintMode.GetComponent<Button>());
        Place(sprintMode, new Vector2(RightColumn, toggleY + 112f), new Vector2(ColumnWidth, 48f));
        sprintText = sprintMode.GetComponentInChildren<Text>();

        // Rows share the column's height above those between them, so a longer list still fits
        float top = HeaderY - 30f;
        float bottom = toggleY + 112f + 24f + 8f;
        float step = bindingRows.Count > 0 ? Mathf.Min(36f, (top - bottom) / bindingRows.Count) : 0f;
        y = top - step / 2f;
        foreach (BindingRow row in bindingRows)
        {
            Place(row.key.transform.parent.gameObject, new Vector2(RightColumn, y), new Vector2(ColumnWidth, step - 4f));
            y -= step;
        }

        RefreshLabels();
        RefreshBindings();
    }

    // Name on the left and value on the right above a full-width slider
    private Slider SliderRow(RectTransform parent, string name, ref Text value, float y, float min, float max, bool wholeNumbers, float current, UnityAction<float> onChange)
    {
        NewLabel(parent, name, new Vector2(LeftColumn, y), ColumnWidth, 16, TextAnchor.MiddleLeft);
        value = NewLabel(parent, "", new Vector2(LeftColumn, y), ColumnWidth, 16, TextAnchor.MiddleRight);
        value.color = UITheme.AccentText;
        return NewSlider(parent, new Vector2(LeftColumn, y - 26f), ColumnWidth, min, max, wholeNumbers, current, onChange);
    }

    private void AddBindingRow(RectTransform parent, string name, InputAction action, int bindingIndex)
    {
        if (bindingIndex < 0) return;

        BindingRow row = new BindingRow { name = name, action = action, bindingIndex = bindingIndex };
        GameObject button = NewButton(parent, "", () => Rebind(row));
        UITheme.StyleButtonBackground(button.GetComponent<Button>(), UITheme.ButtonStyle.Secondary, 6f);

        Text label = button.GetComponentInChildren<Text>();
        UITheme.StyleText(label, 15, UITheme.TextColor, TextAnchor.MiddleLeft);
        UITheme.Inset(label.rectTransform, 14f, 14f);
        label.text = name;

        row.key = UITheme.NewText("Key", button.transform, 15, UITheme.AccentText, TextAnchor.MiddleRight);
        UITheme.Inset(row.key.rectTransform, 14f, 14f);
        bindingRows.Add(row);
    }

    private void Rebind(BindingRow row)
    {
        if (GameInput.IsRebinding) return;
        // These are keyboard and mouse bindings: pressed with a controller's A, it would wait for a
        // key with no way to cancel from the controller
        if (MenuNavigation.UsingGamepad) return;

        row.key.text = "PRESS A KEY  (ESC CANCELS)";
        row.key.color = UITheme.TextColor;
        GameInput.StartRebind(row.action, row.bindingIndex, RefreshBindings);
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
        RefreshLabels();
        RefreshBindings();
        MenuNavigation.Focus(xSensSlider);
    }

    private void CloseSettings()
    {
        panel.SetActive(false);
        mainButtons.SetActive(true);
        MenuNavigation.Focus(returnTo);
    }

    #endregion
}
