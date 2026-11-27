using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// A dropdown list for menu options with several choices (map, mode, bots, graphics, starting gun):
// clicking the option's button drops a list of every choice under it (above it if there's no room
// below), the current one highlighted. Picking one closes it; so does clicking anywhere else, Esc,
// a controller's B, or the button going away. With a controller, up and down move through the list
// (and only the list) and A picks. Built at runtime in the menu's style (UITheme).
public class OptionPicker : MonoBehaviour
{
    private const float Gap = 4f, Padding = 4f, MinRowHeight = 34f, MaxRowHeight = 44f;

    private static OptionPicker open;
    private static int closedFrame = -1;

    // Open, or closed this frame: Esc closing a list shouldn't also close the menu behind it
    public static bool Showing => open != null || closedFrame == Time.frameCount;

    private Button owner;
    private Action<int> onPick;

    // Opens the list for owner (the option's button). onPick gets the index of the choice picked.
    public static void Open(Button owner, string[] options, int selected, Action<int> onPick)
    {
        Close();
        if (owner == null || options == null || options.Length == 0) return;
        Canvas canvas = owner.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        canvas = canvas.rootCanvas;

        // A see-through cover over the whole screen: clicking it (anywhere but the list) closes the list
        GameObject cover = new GameObject("Option Picker", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform coverRect = (RectTransform)cover.transform;
        coverRect.SetParent(canvas.transform, false);
        coverRect.SetAsLastSibling();
        UITheme.Stretch(coverRect);
        Image coverImage = cover.GetComponent<Image>();
        coverImage.color = new Color(0f, 0f, 0f, 0f);
        Button coverButton = cover.GetComponent<Button>();
        coverButton.transition = Selectable.Transition.None;
        coverButton.navigation = new Navigation { mode = Navigation.Mode.None };
        coverButton.onClick.AddListener(Close);

        OptionPicker picker = cover.AddComponent<OptionPicker>();
        picker.owner = owner;
        picker.onPick = onPick;
        open = picker;

        // Where the button is, in the cover's space
        RectTransform ownerRect = (RectTransform)owner.transform;
        Vector3[] corners = new Vector3[4];
        ownerRect.GetWorldCorners(corners);
        Vector2 min = coverRect.InverseTransformPoint(corners[0]);
        Vector2 max = coverRect.InverseTransformPoint(corners[2]);
        float width = max.x - min.x;
        float rowHeight = Mathf.Clamp((max.y - min.y) * 0.85f, MinRowHeight, MaxRowHeight);
        float height = options.Length * rowHeight + Padding * 2f;

        // Under the button, or over it if it would run off the bottom; kept on screen either way
        Rect screen = coverRect.rect;
        float top = min.y - Gap;
        if (top - height < screen.yMin && max.y + Gap + height <= screen.yMax) top = max.y + Gap + height;
        top = Mathf.Clamp(top, screen.yMin + height, screen.yMax);

        Image panel = UITheme.NewImage("List", cover.transform, UITheme.Panel, UITheme.PanelRadius);
        panel.raycastTarget = true; // clicks on the list's edges don't fall through to the cover
        UITheme.AddShadow(panel, 0.5f);
        UITheme.PanelChrome(panel, false);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(min.x, top);
        panelRect.sizeDelta = new Vector2(width, height);

        Button[] rows = new Button[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            GameObject row = new GameObject(options[i], typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rowRect = (RectTransform)row.transform;
            rowRect.SetParent(panelRect, false);
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -Padding - i * rowHeight);
            rowRect.sizeDelta = new Vector2(-Padding * 2f, rowHeight - 2f);

            Button button = row.GetComponent<Button>();
            UITheme.StyleButtonBackground(button, i == selected ? UITheme.ButtonStyle.Primary : UITheme.ButtonStyle.Secondary, 6f);
            button.onClick.AddListener(() => picker.Pick(index));
            rows[i] = button;

            Text label = UITheme.NewText("Label", row.transform, 18, UITheme.TextColor, TextAnchor.MiddleLeft);
            UITheme.Inset(label.rectTransform, 14f, 14f);
            label.text = options[i];
        }

        // A controller moves up and down the list, and can't wander off it to the buttons behind
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = rows[(i + rows.Length - 1) % rows.Length],
                selectOnDown = rows[(i + 1) % rows.Length],
            };
        }
        MenuNavigation.Focus(rows[Mathf.Clamp(selected, 0, rows.Length - 1)]);
    }

    public static void Close()
    {
        if (open == null) return;
        closedFrame = Time.frameCount;
        Button owner = open.owner;
        // Gone at once (not at the end of the frame), so a controller's selection can't stay on it
        open.gameObject.SetActive(false);
        Destroy(open.gameObject);
        open = null;
        // A controller goes back to the option's button
        MenuNavigation.Focus(owner);
    }

    private void Pick(int index)
    {
        Action<int> pick = onPick;
        Close();
        pick?.Invoke(index);
    }

    private void Update()
    {
        // The button went away (its tab or menu closed): so does the list
        if (owner == null || !owner.isActiveAndEnabled) { Close(); return; }
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Close(); return; }
        if (MenuNavigation.TakeBack()) Close();
    }

    private void OnDestroy()
    {
        if (open == this) open = null;
    }
}
