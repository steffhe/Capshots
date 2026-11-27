using UnityEngine;
using UnityEngine.UI;

// A small frame rate readout at the bottom of the screen, in the middle, turned on in Settings (GameSettings.ShowFps).
// Shows the frames per second and the average and slowest frame time over the last half second,
// so you can tell a steady low frame rate from one that stutters. In the browser it also shows the
// resolution the world is drawn at, while BrowserGraphics has lowered it. Lives for the whole session,
// over every scene and menu.
public class FpsCounter : MonoBehaviour
{
    private const float Interval = 0.5f;

    private static FpsCounter instance;
    private Canvas canvas;
    private Text text;
    private int frames;
    private float elapsed, slowest;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("FPS Counter");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<FpsCounter>();
    }

    private void Awake()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1001; // Above everything, the loading screen included
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var label = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Shadow));
        label.transform.SetParent(transform, false);
        text = label.GetComponent<Text>();
        text.font = UITheme.Font != null ? UITheme.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 16;
        text.color = Color.white;
        text.alignment = TextAnchor.LowerCenter;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        label.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        RectTransform rect = text.rectTransform;
        // Bottom centre: the one edge nothing else uses, in a match (the kill feed is top right) or the menu
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 4f);
        rect.sizeDelta = new Vector2(400f, 20f);

        canvas.enabled = GameSettings.ShowFps;
        GameSettings.ShowFpsChanged += Refresh;
    }

    private void OnDestroy()
    {
        GameSettings.ShowFpsChanged -= Refresh;
    }

    private void Refresh()
    {
        canvas.enabled = GameSettings.ShowFps;
        frames = 0;
        elapsed = slowest = 0f;
    }

    private void Update()
    {
        if (!canvas.enabled) return;

        // The font comes from the first menu that loads, after this was made
        if (UITheme.Font != null && text.font != UITheme.Font) text.font = UITheme.Font;

        float dt = Time.unscaledDeltaTime;
        frames++;
        elapsed += dt;
        if (dt > slowest) slowest = dt;
        if (elapsed < Interval) return;

        float fps = frames / elapsed;
        float average = elapsed / frames * 1000f;
        Color colour = fps >= 55f ? new Color(0.5f, 1f, 0.5f) : fps >= 30f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.4f, 0.4f);
        text.text = $"<color=#{ColorUtility.ToHtmlStringRGB(colour)}>{Mathf.RoundToInt(fps)} FPS</color>   {average:0.0} ms   <color=#FFFFFF99>MAX {slowest * 1000f:0} ms</color>";
        // The browser lowers the resolution when frames are slow (BrowserGraphics)
        if (BrowserGraphics.RenderScale < 0.999f) text.text += $"   <color=#FFFFFF99>RES {Mathf.RoundToInt(BrowserGraphics.RenderScale * 100f)}%</color>";
        frames = 0;
        elapsed = slowest = 0f;
    }
}
