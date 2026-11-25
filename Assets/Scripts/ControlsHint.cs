using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The first time a player spawns in a match, a card in the bottom left lists the controls (as they
// are bound now: the keys someone changed in Settings show as changed), for a gamepad if one is in
// use. It fades away after a while and never shows again. Touch controls have their own on-screen
// buttons, so they get none.
public class ControlsHint : MonoBehaviour
{
    private const string ShownKey = "hints.controlsShown";
    private const float ShowTime = 14f, FadeTime = 0.6f;

    private CanvasGroup group;
    private float age;

    // Called by Player when the local player spawns
    public static void ShowOnce()
    {
        if (PlayerPrefs.GetInt(ShownKey, 0) != 0 || TouchControls.UsingTouch) return;
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null) return;
        PlayerPrefs.SetInt(ShownKey, 1);
        PlayerPrefs.Save();

        GameObject go = new GameObject("Controls Hint", typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(28f, 150f);
        ControlsHint hint = go.AddComponent<ControlsHint>();
        hint.Build(rect, UsingGamepad());
    }

    // A gamepad is plugged in and no mouse moved more recently
    private static bool UsingGamepad()
    {
        Gamepad pad = Gamepad.current;
        if (pad == null) return false;
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        double padTime = pad.lastUpdateTime;
        double other = System.Math.Max(mouse != null ? mouse.lastUpdateTime : 0, keyboard != null ? keyboard.lastUpdateTime : 0);
        return padTime > other;
    }

    private static string Key(InputAction action) => GameInput.BindingText(action, GameInput.KeyboardBindingIndex(action));

    private void Build(RectTransform rect, bool gamepad)
    {
        string[] lines = gamepad
            ? new[]
            {
                "LEFT STICK  MOVE   ·   RIGHT STICK  LOOK",
                "RT  FIRE   ·   LT  AIM   ·   X  SWAP CELL",
                "Y  GRENADE (HOLD TO COOK, LET GO TO THROW)",
                "BUMPERS  CHANGE GUN FORM",
                "A  JUMP   ·   B  CROUCH   ·   L3  SPRINT   ·   R3  SLIDE",
                "VIEW  SCOREBOARD   ·   MENU  PAUSE",
            }
            : new[]
            {
                $"{MoveKeys()}  MOVE   ·   {Key(GameInput.Jump)}  JUMP   ·   {Key(GameInput.Sprint)}  SPRINT",
                $"{Key(GameInput.Fire)}  FIRE   ·   {Key(GameInput.Aim)}  AIM   ·   {Key(GameInput.Reload)}  SWAP CELL",
                $"{Key(GameInput.Grenade)}  GRENADE (HOLD TO COOK, LET GO TO THROW)",
                $"{Key(GameInput.Weapon1)}-{Key(GameInput.Weapon4)} OR WHEEL  CHANGE GUN FORM",
                $"{Key(GameInput.Crouch)}  CROUCH   ·   {Key(GameInput.Slide)}  SLIDE (WHILE SPRINTING)",
                $"{Key(GameInput.Scoreboard)}  SCOREBOARD   ·   {Key(GameInput.Chat)}  CHAT   ·   ESC  PAUSE",
            };

        const float lineHeight = 26f, padding = 16f, width = 640f;
        rect.sizeDelta = new Vector2(width, padding * 2f + 30f + lines.Length * lineHeight);
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        Image background = UITheme.NewImage("Background", rect, UITheme.Panel, 10f);
        UITheme.Stretch(background.rectTransform);
        background.raycastTarget = false;

        Text title = UITheme.NewText("Title", rect, 18, UITheme.AccentText, TextAnchor.MiddleLeft);
        UITheme.Row(title.rectTransform, padding, 24f);
        title.rectTransform.offsetMin = new Vector2(padding, title.rectTransform.offsetMin.y);
        title.text = "CONTROLS";

        for (int i = 0; i < lines.Length; i++)
        {
            Text line = UITheme.NewText("Line", rect, 16, UITheme.TextColor, TextAnchor.MiddleLeft);
            UITheme.Row(line.rectTransform, padding + 30f + i * lineHeight, lineHeight);
            line.rectTransform.offsetMin = new Vector2(padding, line.rectTransform.offsetMin.y);
            line.supportRichText = true;
            line.text = lines[i];
        }
    }

    // WASD, or whatever the four move keys are bound to now
    private static string MoveKeys()
    {
        int[] parts = GameInput.MovePartIndices();
        if (parts.Length < 4) return "WASD";
        string keys = "";
        foreach (int i in parts) keys += GameInput.BindingText(GameInput.Move, i);
        // Single keys read as one word (WASD); longer names (arrow keys) need spaces between them
        return keys.Length == 4 ? keys : string.Join(" ", System.Array.ConvertAll(parts, i => GameInput.BindingText(GameInput.Move, i)));
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        // Out of the way once the match is over or the pause menu is up
        if (Pause.paused || (Manager.Instance != null && Manager.Instance.MatchOver)) age = Mathf.Max(age, ShowTime);
        float alpha = age < FadeTime ? age / FadeTime : age < ShowTime ? 1f : 1f - (age - ShowTime) / FadeTime;
        group.alpha = Mathf.Clamp01(alpha);
        if (age >= ShowTime + FadeTime) Destroy(gameObject);
    }
}
