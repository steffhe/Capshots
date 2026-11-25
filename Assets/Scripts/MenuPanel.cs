using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// What the Settings and Customize panels share: they're built at runtime from copies of a menu's
// own button, slider and label (the pause menu's, or the main menu's), so no prefab or scene needs
// editing, and styled with UITheme.
public abstract class MenuPanel : MonoBehaviour
{
    // Whether the panel is showing
    public abstract bool IsOpen { get; }

    protected GameObject buttonTemplate;
    protected Slider sliderTemplate;
    protected Text labelTemplate;

    #region Building blocks

    // Copies the pause menu's Resume button (or the main menu's button), without its own action
    // (Resume, or opening a menu tab), as a secondary button
    protected GameObject NewButton(Transform parent, string text, UnityAction onClick)
    {
        GameObject copy = Instantiate(buttonTemplate, parent);
        copy.name = string.IsNullOrEmpty(text) ? "Button" : text;

        Button button = copy.GetComponent<Button>();
        if (button != null)
        {
            // Its own way round the panel: the template may have explicit moves set for the menu it
            // came from (the main menu's column wires its buttons by hand), which point at buttons
            // hidden while the panel is open
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentMethodName(i) == "TogglePause" || button.onClick.GetPersistentTarget(i) is Launcher)
                {
                    button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
                }
            }
            button.onClick.AddListener(onClick);
        }

        Text label = copy.GetComponentInChildren<Text>();
        if (label != null) label.text = text;
        UITheme.StyleButton(button, UITheme.ButtonStyle.Secondary, 20);
        return copy;
    }

    protected Slider NewSlider(RectTransform parent, Vector2 position, float width, float min, float max, bool wholeNumbers, float value, UnityAction<float> onChange)
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
        rect.sizeDelta = new Vector2(width, 20f);
        UITheme.StyleSlider(slider);

        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(onChange);
        return slider;
    }

    protected Text NewLabel(RectTransform parent, string text, Vector2 position, float width, int fontSize, TextAnchor alignment)
    {
        Text label = Instantiate(labelTemplate, parent);
        label.name = "Label";
        RectTransform rect = (RectTransform)label.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, 34f);
        UITheme.StyleText(label, fontSize, UITheme.TextColor, alignment);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    protected static void Place(GameObject go, Vector2 position, Vector2 size)
    {
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    #endregion
}
