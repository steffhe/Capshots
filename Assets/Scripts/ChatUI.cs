using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// In-match text chat. Press the Chat key (T or Enter by default) to type, Enter to send,
// Escape to cancel. Messages go to everyone in the room through the Manager.
// Built at runtime, so no scene or prefab changes are needed.
public class ChatUI : MonoBehaviour
{
    public static ChatUI Instance { get; private set; }

    // True while the chat box is open; gameplay input is ignored meanwhile
    public static bool IsTyping { get; private set; }
    // The frame the chat box closed on, so the Escape that closed it doesn't also open the pause menu
    public static int ClosedFrame { get; private set; } = -1;

    public const int MaxMessageLength = 120;

    [Header("Chat")]
    public int maxMessages = 8;
    // Seconds a message stays on screen before fading out (all show again while typing)
    public float messageTime = 30f;

    private Manager manager;
    private Font font;
    private RectTransform log;
    private InputField input;
    private int openFrame;

    private class Entry
    {
        public Text text;
        public CanvasGroup group;
        public float time;
    }
    private readonly List<Entry> entries = new List<Entry>();

    public static ChatUI Create(Transform canvas, Font font, Manager manager)
    {
        GameObject go = new GameObject("Chat UI", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        go.transform.SetAsFirstSibling();

        ChatUI chat = go.AddComponent<ChatUI>();
        chat.font = font;
        chat.manager = manager;
        chat.Build();
        return chat;
    }

    private void Awake()
    {
        Instance = this;
        IsTyping = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        IsTyping = false;
    }

    private void Build()
    {
        RectTransform root = (RectTransform)transform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;

        // Message log in the lower left, above the timer and team score; newest at the bottom
        GameObject logObject = new GameObject("Chat Log", typeof(RectTransform));
        log = (RectTransform)logObject.transform;
        log.SetParent(root, false);
        log.anchorMin = log.anchorMax = log.pivot = Vector2.zero;
        log.anchoredPosition = new Vector2(20f, 230f);
        log.sizeDelta = new Vector2(600f, 200f);

        // Input box under the log
        GameObject inputObject = new GameObject("Chat Input", typeof(RectTransform), typeof(Image), typeof(InputField));
        RectTransform inputRect = (RectTransform)inputObject.transform;
        inputRect.SetParent(root, false);
        inputRect.anchorMin = inputRect.anchorMax = inputRect.pivot = Vector2.zero;
        inputRect.anchoredPosition = new Vector2(20f, 192f);
        inputRect.sizeDelta = new Vector2(600f, 32f);
        inputObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        Text text = NewText("Text", inputRect, 18, TextAnchor.MiddleLeft);
        text.supportRichText = false;
        Inset((RectTransform)text.transform);

        Text placeholder = NewText("Placeholder", inputRect, 18, TextAnchor.MiddleLeft);
        placeholder.text = "Say something... (Enter to send, Esc to cancel)";
        placeholder.color = new Color(1f, 1f, 1f, 0.4f);
        placeholder.fontStyle = FontStyle.Italic;
        Inset((RectTransform)placeholder.transform);

        input = inputObject.GetComponent<InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.characterLimit = MaxMessageLength;
        input.lineType = InputField.LineType.SingleLine;
        inputObject.SetActive(false);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (IsTyping)
        {
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
            else if (keyboard != null && Time.frameCount > openFrame && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                string message = input.text;
                Close();
                if (manager != null && !string.IsNullOrWhiteSpace(message)) manager.SendChat_S(message);
            }
            else if (!input.isFocused && Time.frameCount > openFrame + 1)
            {
                // Keep the focus if something else took it
                input.ActivateInputField();
            }
        }
        else if (!Pause.paused && !GameInput.IsRebinding && GameInput.Chat.WasPressedThisFrame())
        {
            Open();
        }

        UpdateFading();
    }

    private void Open()
    {
        IsTyping = true;
        openFrame = Time.frameCount;
        input.text = "";
        input.gameObject.SetActive(true);
        // Focus next frame, so the key that opened the chat isn't typed into it
        StartCoroutine(FocusNextFrame());
    }

    private IEnumerator FocusNextFrame()
    {
        yield return null;
        if (!IsTyping) yield break;
        input.text = "";
        input.ActivateInputField();
    }

    private void Close()
    {
        IsTyping = false;
        ClosedFrame = Time.frameCount;
        input.DeactivateInputField();
        input.text = "";
        input.gameObject.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    // Adds a line to the log. sender is shown in senderColor (a hex colour like #ffffff).
    public static void AddMessage(string sender, string senderColor, string message)
    {
        if (Instance == null) return;
        ChatUI chat = Instance;

        Text line = chat.NewText("Chat Line", chat.log, 18, TextAnchor.LowerLeft);
        RectTransform rect = (RectTransform)line.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.sizeDelta = new Vector2(600f, 22f);
        line.text = $"<color={senderColor}>{Escape(sender)}</color>: {Escape(message)}";

        // Fade with a CanvasGroup: changing the text colour's alpha wouldn't fade the
        // sender's name, which has its own colour tag
        CanvasGroup group = line.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        chat.entries.Add(new Entry { text = line, group = group, time = Time.time });
        while (chat.entries.Count > chat.maxMessages)
        {
            Destroy(chat.entries[0].text.gameObject);
            chat.entries.RemoveAt(0);
        }
        chat.Layout();
    }

    // Stops players from using rich text tags (huge text, colours) in chat
    public static string Escape(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Replace("<", "<​");
    }

    private void Layout()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            int fromBottom = entries.Count - 1 - i;
            ((RectTransform)entries[i].text.transform).anchoredPosition = new Vector2(0f, fromBottom * 22f);
        }
    }

    // Old messages fade out, but all of them show again while typing
    private void UpdateFading()
    {
        foreach (Entry entry in entries)
        {
            float age = Time.time - entry.time;
            float alpha = IsTyping ? 1f : Mathf.Clamp01(1f - (age - messageTime) / 1f);
            if (!Mathf.Approximately(entry.group.alpha, alpha)) entry.group.alpha = alpha;
        }
    }

    private Text NewText(string name, Transform parent, int size, TextAnchor alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        go.GetComponent<Shadow>().effectDistance = new Vector2(1f, -1f);
        return text;
    }

    private static void Inset(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 2f);
        rect.offsetMax = new Vector2(-8f, -2f);
    }
}
