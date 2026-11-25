using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Settings panel inside the pause menu: look sensitivity, ADS sensitivity, field of view, volume,
// graphics quality, colours, the FPS counter and key bindings. Built at runtime from copies of the pause menu's own button,
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

    private Text xSensValue, ySensValue, fovValue, adsValue, volumeValue;
    private Slider xSensSlider, ySensSlider, fovSlider, adsSlider, volumeSlider;
    private Text qualityText;
    private Text coloursText;
    private Text fpsText;

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
    private const float PanelHeight = 720f;
    private const float LeftColumn = -250f;
    private const float RightColumn = 250f;
    private const float ColumnWidth = 400f;
    private const float HeaderY = 270f;

    // In the main menu rather than the pause menu
    private bool inMainMenu;

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
        // Unpausing hides the whole pause menu: go back to the main pause buttons for next time
        if (panel != null && panel.activeSelf && !anchor.gameObject.activeSelf) CloseSettings();
        // In the main menu, Esc closes the panel (in a match it unpauses, closing it as above)
        if (inMainMenu && panel != null && panel.activeSelf && !GameInput.IsRebinding && !OptionPicker.Showing)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) CloseSettings();
        }
    }

    #region Panel

    private void BuildPanel()
    {
        panel = new GameObject("Settings", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)panel.transform;
        rect.SetParent(anchor, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        Image background = panel.GetComponent<Image>();
        UITheme.Round(background, 14f);
        background.color = UITheme.Panel;

        Text title = NewLabel(rect, "SETTINGS", new Vector2(0f, PanelHeight / 2f - 48f), PanelWidth, 34, TextAnchor.MiddleCenter);
        title.color = UITheme.TextColor;

        // Left column: sliders, then the graphics and reset buttons
        NewLabel(rect, "GENERAL", new Vector2(LeftColumn, HeaderY), ColumnWidth, 16, TextAnchor.MiddleLeft).color = UITheme.AccentText;

        float y = HeaderY - 44f;
        const float rowStep = 54f;
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
        volumeSlider = SliderRow(rect, "VOLUME", ref volumeValue, y, 0f, 1f, false, GameSettings.Volume, v =>
        {
            GameSettings.Volume = v;
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
        Place(quality, new Vector2(LeftColumn, y - 70f), new Vector2(ColumnWidth, 48f));
        qualityText = quality.GetComponentInChildren<Text>();

        // Standard colours, or ones that stay apart with red-green colour blindness
        GameObject colours = NewButton(rect, "", () =>
        {
            GameSettings.Colourblind = !GameSettings.Colourblind;
            RefreshLabels();
        });
        UITheme.StyleSelector(colours.GetComponent<Button>());
        Place(colours, new Vector2(LeftColumn, y - 130f), new Vector2(ColumnWidth, 48f));
        coloursText = colours.GetComponentInChildren<Text>();

        // The frame rate readout in the corner (FpsCounter)
        GameObject fps = NewButton(rect, "", () =>
        {
            GameSettings.ShowFps = !GameSettings.ShowFps;
            RefreshLabels();
        });
        UITheme.StyleSelector(fps.GetComponent<Button>());
        Place(fps, new Vector2(LeftColumn, y - 190f), new Vector2(ColumnWidth, 48f));
        fpsText = fps.GetComponentInChildren<Text>();

        GameObject reset = NewButton(rect, "RESET CONTROLS", () =>
        {
            GameInput.ResetBindings();
            RefreshBindings();
        });
        // The same 12 gap between the buttons as elsewhere
        Place(reset, new Vector2(LeftColumn, y - 250f), new Vector2(ColumnWidth, 48f));

        GameObject done = NewButton(rect, "DONE", CloseSettings);
        UITheme.StyleButton(done.GetComponent<Button>(), UITheme.ButtonStyle.Primary);
        Place(done, new Vector2(LeftColumn, -PanelHeight / 2f + 58f), new Vector2(ColumnWidth, 52f));

        // Right column: key bindings
        NewLabel(rect, "CONTROLS", new Vector2(RightColumn, HeaderY), ColumnWidth, 16, TextAnchor.MiddleLeft).color = UITheme.AccentText;
        NewLabel(rect, "CLICK TO CHANGE", new Vector2(RightColumn, HeaderY), ColumnWidth, 14, TextAnchor.MiddleRight).color = UITheme.MutedText;

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

        // Rows share the column's height between them, so a longer list still fits the panel
        float top = HeaderY - 30f;
        float bottom = -PanelHeight / 2f + 32f;
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
        xSensValue.text = Mathf.RoundToInt(GameSettings.XSensitivity).ToString();
        ySensValue.text = Mathf.RoundToInt(GameSettings.YSensitivity).ToString();
        fovValue.text = Mathf.RoundToInt(GameSettings.Fov).ToString();
        adsValue.text = GameSettings.AdsSensitivity.ToString("0.00") + "x";
        volumeValue.text = Mathf.RoundToInt(GameSettings.Volume * 100f) + "%";
        qualityText.text = UITheme.Option("GRAPHICS", GameSettings.QualityName.ToUpperInvariant());
        if (fpsText != null) fpsText.text = UITheme.Option("FPS COUNTER", GameSettings.ShowFps ? "ON" : "OFF");
        if (coloursText != null) coloursText.text = UITheme.Option("COLOURS", GameSettings.Colourblind ? "COLOUR BLIND" : "STANDARD");
    }

    private void OpenSettings()
    {
        mainButtons.SetActive(false);
        panel.SetActive(true);

        xSensSlider.SetValueWithoutNotify(GameSettings.XSensitivity);
        ySensSlider.SetValueWithoutNotify(GameSettings.YSensitivity);
        fovSlider.SetValueWithoutNotify(GameSettings.Fov);
        adsSlider.SetValueWithoutNotify(GameSettings.AdsSensitivity);
        volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
        RefreshLabels();
        RefreshBindings();
    }

    private void CloseSettings()
    {
        panel.SetActive(false);
        mainButtons.SetActive(true);
    }

    #endregion
}
