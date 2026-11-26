using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Plays MenuAudio's blips for one button (or slider, toggle, ...): pointed at, picked with a
// controller, pressed
public class MenuSoundHook : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    private Selectable selectable;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        if (selectable is Button button) button.onClick.AddListener(OnPressed);
        else if (selectable is Toggle toggle) toggle.onValueChanged.AddListener(_ => OnPressed());
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectable != null && selectable.IsInteractable()) MenuAudio.Hovered();
    }

    // Moving between buttons with a controller or the keyboard (pointing is OnPointerEnter's)
    public void OnSelect(BaseEventData eventData)
    {
        if (MenuNavigation.UsingGamepad && selectable != null && selectable.IsInteractable()) MenuAudio.Hovered();
    }

    private void OnPressed()
    {
        Text label = GetComponentInChildren<Text>();
        string text = label != null ? label.text.ToUpperInvariant() : "";
        bool goingBack = text.Contains("BACK") || text.Contains("DONE") || text.Contains("CLOSE") || text.Contains("CANCEL");
        MenuAudio.Pressed(goingBack);
    }
}
