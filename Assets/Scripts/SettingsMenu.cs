using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Settings panel inside the pause menu: look sensitivity, ADS sensitivity, field of view, volume,
// graphics quality and key bindings. Built at runtime from copies of the pause menu's own button,
// slider and label, so it matches their style and the Pause prefab doesn't need editing.
public class SettingsMenu : MonoBehaviour
{
    private Transform anchor;
    private GameObject mainButtons;
    private GameObject sensitivity;
    private GameObject panel;

    private GameObject buttonTemplate;
    private Slider sliderTemplate;
    private Text labelTemplate;

    private Text xSensLabel, ySensLabel, fovLabel, adsLabel, volumeLabel;
    private Slider xSensSlider, ySensSlider, fovSlider, adsSlider, volumeSlider;
    private Text qualityText;

    private class BindingRow
    {
        public string name;
        public InputAction action;
        public int bindingIndex;
        public Text text;
    }
    private readonly List<BindingRow> bindingRows = new List<BindingRow>();

    public static SettingsMenu Create(Pause pause)
    {
        return pause.gameObject.AddComponent<SettingsMenu>();
    }

    private void Start()
    {
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

        // "Settings" goes where Quit was, and Quit moves down a row
        RectTransform quitRect = (RectTransform)quit;
        GameObject settingsButton = NewButton(buttons, "SETTINGS", OpenSettings);
        settingsButton.transform.SetSiblingIndex(quit.GetSiblingIndex());
        ((RectTransform)settingsButton.transform).anchoredPosition = quitRect.anchoredPosition;
        quitRect.anchoredPosition -= new Vector2(0f, 60f);

        BuildPanel();
        panel.SetActive(false);
    }

    private void Update()
    {
        // Unpausing hides the whole pause menu: go back to the main pause buttons for next time
        if (panel != null && panel.activeSelf && !anchor.gameObject.activeSelf) CloseSettings();
    }

    #region Panel

    private void BuildPanel()
    {
        panel = new GameObject("Settings", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)panel.transform;
        rect.SetParent(anchor, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1000f, 660f);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

        NewLabel(rect, "SETTINGS", new Vector2(0f, 300f), 600f, 34);

        // Left column: sliders and buttons, one row every 62 px
        const float left = -250f;
        xSensLabel = NewLabel(rect, "", new Vector2(left, 245f), 400f, 0);
        xSensSlider = NewSlider(rect, new Vector2(left, 217f), GameSettings.MinSensitivity, GameSettings.MaxSensitivity, true, GameSettings.XSensitivity, v =>
        {
            GameSettings.XSensitivity = v;
            RefreshLabels();
        });

        ySensLabel = NewLabel(rect, "", new Vector2(left, 183f), 400f, 0);
        ySensSlider = NewSlider(rect, new Vector2(left, 155f), GameSettings.MinSensitivity, GameSettings.MaxSensitivity, true, GameSettings.YSensitivity, v =>
        {
            GameSettings.YSensitivity = v;
            RefreshLabels();
        });

        adsLabel = NewLabel(rect, "", new Vector2(left, 121f), 400f, 0);
        adsSlider = NewSlider(rect, new Vector2(left, 93f), 0.1f, 2f, false, GameSettings.AdsSensitivity, v =>
        {
            GameSettings.AdsSensitivity = Mathf.Round(v * 20f) / 20f;
            RefreshLabels();
        });

        fovLabel = NewLabel(rect, "", new Vector2(left, 59f), 400f, 0);
        fovSlider = NewSlider(rect, new Vector2(left, 31f), GameSettings.MinFov, GameSettings.MaxFov, true, GameSettings.Fov, v =>
        {
            GameSettings.Fov = v;
            RefreshLabels();
        });

        volumeLabel = NewLabel(rect, "", new Vector2(left, -3f), 400f, 0);
        volumeSlider = NewSlider(rect, new Vector2(left, -31f), 0f, 1f, false, GameSettings.Volume, v =>
        {
            GameSettings.Volume = v;
            RefreshLabels();
        });

        GameObject quality = NewButton(rect, "", () =>
        {
            if (QualitySettings.names.Length > 0) GameSettings.Quality = (GameSettings.Quality + 1) % QualitySettings.names.Length;
            RefreshLabels();
        });
        Place(quality, new Vector2(left, -95f), new Vector2(340f, 46f));
        qualityText = quality.GetComponentInChildren<Text>();

        GameObject reset = NewButton(rect, "RESET CONTROLS", () =>
        {
            GameInput.ResetBindings();
            RefreshBindings();
        });
        Place(reset, new Vector2(left, -155f), new Vector2(340f, 46f));

        GameObject back = NewButton(rect, "BACK", CloseSettings);
        Place(back, new Vector2(left, -270f), new Vector2(340f, 46f));

        // Right column: key bindings
        const float right = 250f;
        NewLabel(rect, "CONTROLS  (CLICK TO CHANGE)", new Vector2(right, 255f), 440f, 0);

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

        float y = 220f;
        foreach (BindingRow row in bindingRows)
        {
            Place(row.text.transform.parent.gameObject, new Vector2(right, y), new Vector2(440f, 30f));
            y -= 33f;
        }

        RefreshLabels();
        RefreshBindings();
    }

    private void AddBindingRow(RectTransform parent, string name, InputAction action, int bindingIndex)
    {
        if (bindingIndex < 0) return;

        BindingRow row = new BindingRow { name = name, action = action, bindingIndex = bindingIndex };
        GameObject button = NewButton(parent, "", () => Rebind(row));
        row.text = button.GetComponentInChildren<Text>();
        row.text.fontSize = Mathf.Min(row.text.fontSize, 18);
        row.text.resizeTextForBestFit = false;
        bindingRows.Add(row);
    }

    private void Rebind(BindingRow row)
    {
        if (GameInput.IsRebinding) return;

        row.text.text = row.name + ":  PRESS A KEY  (ESC TO CANCEL)";
        GameInput.StartRebind(row.action, row.bindingIndex, RefreshBindings);
    }

    private void RefreshBindings()
    {
        foreach (BindingRow row in bindingRows)
        {
            row.text.text = row.name + ":  " + GameInput.BindingText(row.action, row.bindingIndex);
        }
    }

    private void RefreshLabels()
    {
        xSensLabel.text = "LOOK SENSITIVITY X:  " + Mathf.RoundToInt(GameSettings.XSensitivity);
        ySensLabel.text = "LOOK SENSITIVITY Y:  " + Mathf.RoundToInt(GameSettings.YSensitivity);
        fovLabel.text = "FIELD OF VIEW:  " + Mathf.RoundToInt(GameSettings.Fov);
        adsLabel.text = "AIM SENSITIVITY:  " + GameSettings.AdsSensitivity.ToString("0.00") + "x";
        volumeLabel.text = "VOLUME:  " + Mathf.RoundToInt(GameSettings.Volume * 100f) + "%";
        qualityText.text = "GRAPHICS:  " + GameSettings.QualityName;
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

    #region Building blocks

    // Copies the pause menu's Resume button, without its Resume action
    private GameObject NewButton(Transform parent, string text, UnityAction onClick)
    {
        GameObject copy = Instantiate(buttonTemplate, parent);
        copy.name = string.IsNullOrEmpty(text) ? "Button" : text;

        Button button = copy.GetComponent<Button>();
        if (button != null)
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentMethodName(i) == "TogglePause")
                {
                    button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
                }
            }
            button.onClick.AddListener(onClick);
        }

        Text label = copy.GetComponentInChildren<Text>();
        if (label != null) label.text = text;
        return copy;
    }

    private Slider NewSlider(RectTransform parent, Vector2 position, float min, float max, bool wholeNumbers, float value, UnityAction<float> onChange)
    {
        Slider slider = Instantiate(sliderTemplate, parent);
        slider.name = "Slider";
        for (int i = 0; i < slider.onValueChanged.GetPersistentEventCount(); i++)
        {
            slider.onValueChanged.SetPersistentListenerState(i, UnityEventCallState.Off);
        }

        RectTransform rect = (RectTransform)slider.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(320f, rect.sizeDelta.y);

        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(onChange);
        return slider;
    }

    private Text NewLabel(RectTransform parent, string text, Vector2 position, float width, int fontSize)
    {
        Text label = Instantiate(labelTemplate, parent);
        label.name = "Label";
        RectTransform rect = (RectTransform)label.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, 34f);
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        if (fontSize > 0) label.fontSize = fontSize;
        label.text = text;
        return label;
    }

    private static void Place(GameObject go, Vector2 position, Vector2 size)
    {
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    #endregion
}
