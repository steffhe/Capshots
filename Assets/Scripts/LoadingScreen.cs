using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

// A full-screen overlay for the moments between scenes: finding or joining a match, loading the
// map, and going back to the menu. It lives across scene loads, fades in and out, and shows the
// wordmark, the match being joined, a spinner and a status line.
public class LoadingScreen : MonoBehaviour
{
    private const float FadeInTime = 0.25f;
    private const float FadeOutTime = 0.5f;
    // Hides on its own after this long, in case nothing else does (e.g. a scene that never finishes)
    private const float Timeout = 45f;

    private static readonly string[] Tips =
    {
        "HEADSHOTS KNOCK THE CAP RIGHT OFF",
        "SHOOTING ENDS YOUR SPAWN PROTECTION EARLY",
        "YOUR PLASMA GRENADE RECHARGES A WHILE AFTER YOU THROW IT",
        "EVERY FORM OF YOUR GUN SHARES ONE ENERGY CELL",
        "STOP FIRING FOR A MOMENT AND YOUR ENERGY COMES BACK",
        "SPRINT, THEN CROUCH TO SLIDE",
        "DAMAGE FALLS OFF OVER DISTANCE",
        "YOUR HEALTH COMES BACK AFTER 5 SECONDS OUT OF FIRE",
        "AIMING STEADIES YOUR SHOTS, MOVING SPREADS THEM",
        "HOLD TAB TO SEE THE SCOREBOARD",
    };

    private static LoadingScreen instance;

    private CanvasGroup group;
    private Text title;
    private Text subtitle;
    private Text status;
    private Text tip;
    private RectTransform spinner;
    private RectTransform sweep;
    private string statusText = "";
    private Coroutine fade;
    private float shownAt;
    private float fadingTo;

    // Showing or fading in (not fading out)
    public static bool Visible => instance != null && instance.gameObject.activeSelf && instance.group.blocksRaycasts && instance.fadingTo > 0f;

    // Fades the overlay in with a status line ("JOINING MATCH") and, if known, the map and mode
    public static void Show(string status, string map = null, string mode = null)
    {
        LoadingScreen screen = Instance;
        screen.SetMatchText(map, mode);
        screen.statusText = status ?? "";
        screen.tip.text = "TIP   " + Tips[Random.Range(0, Tips.Length)];
        screen.shownAt = Time.unscaledTime;
        screen.FadeTo(1f, FadeInTime);
    }

    // Builds the overlay and its textures ahead of time, hidden, so the first Show doesn't stall
    // (the glow texture takes a moment to bake, which is noticeable in the browser)
    public static void Prepare()
    {
        _ = Instance;
    }

    public static void SetStatus(string status)
    {
        if (instance != null) instance.statusText = status ?? "";
    }

    // Fills in the map and mode from the session's properties (set by whoever created the match)
    public static void SetMatch(NetworkRunner runner)
    {
        if (instance == null || runner == null) return;
        var properties = NetworkSession.MatchProperties(runner);
        if (properties == null) return;
        string map = properties.TryGetValue("mapname", out SessionProperty m) ? (string)m : null;
        string mode = properties.TryGetValue("mode", out SessionProperty md) ? ModeName((int)md) : null;
        instance.SetMatchText(map, mode);
    }

    public static void Hide(float delay = 0f)
    {
        if (instance == null || !instance.gameObject.activeSelf) return;
        instance.FadeTo(0f, FadeOutTime, delay);
    }

    // Shows a message for a moment, then fades out (e.g. a match that couldn't be joined)
    public static void Fail(string message)
    {
        if (instance == null) return;
        instance.statusText = message;
        instance.FadeTo(0f, FadeOutTime, 2f);
    }

    private static readonly string[] ModeNames = { "FREE FOR ALL", "TEAM DEATHMATCH", "CAP GRAB", "GUN GAME", "TEAM CAP GRAB" };
    public static string ModeName(int mode) => mode >= 0 && mode < ModeNames.Length ? ModeNames[mode] : ModeNames[0];

    private static LoadingScreen Instance
    {
        get
        {
            if (instance == null) instance = Build();
            return instance;
        }
    }

    private void SetMatchText(string map, string mode)
    {
        title.text = string.IsNullOrEmpty(map) ? "" : map.ToUpperInvariant();
        subtitle.text = mode ?? "";
    }

    private void FadeTo(float target, float duration, float delay = 0f)
    {
        gameObject.SetActive(true);
        fadingTo = target;
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(Fade(target, duration, delay));
    }

    private IEnumerator Fade(float target, float duration, float delay)
    {
        // Block clicks right away when showing, and only once fully gone when hiding
        if (target > 0f) group.blocksRaycasts = true;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        float start = group.alpha;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        group.alpha = target;
        if (target <= 0f)
        {
            group.blocksRaycasts = false;
            gameObject.SetActive(false);
        }
        fade = null;
    }

    private void Update()
    {
        float time = Time.unscaledTime;
        spinner.localRotation = Quaternion.Euler(0f, 0f, -time * 300f);

        // An indeterminate bar: a short accent strip sweeping across the bottom edge
        float x = Mathf.Repeat(time * 0.55f, 1.4f) - 0.2f;
        sweep.anchorMin = new Vector2(x, 0f);
        sweep.anchorMax = new Vector2(x + 0.2f, 1f);

        int dots = (int)(time * 3f) % 4;
        status.text = statusText.Length > 0 ? statusText + new string('.', dots) + new string(' ', 3 - dots) : "";

        if (group.blocksRaycasts && fade == null && time - shownAt > Timeout) Hide();
    }

    #region Building the overlay

    private static LoadingScreen Build()
    {
        GameObject root = new GameObject("Loading Screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        DontDestroyOnLoad(root);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // Above every menu and match canvas

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        LoadingScreen screen = root.AddComponent<LoadingScreen>();
        screen.group = root.GetComponent<CanvasGroup>();
        screen.group.alpha = 0f;
        screen.group.blocksRaycasts = false;
        screen.group.interactable = false;

        Transform t = root.transform;

        // Near-black, with a soft blue glow behind the middle. The glow is baked over the background
        // colour into one opaque, dithered texture: a faint glow drawn see-through over near-black
        // only has a few dozen shades to work with and shows rings (banding) across the screen.
        // It's the only background layer and covers the whole screen at any aspect ratio. A solid
        // background under a smaller glow showed as a darker box while fading, because the canvas
        // group fades each image on its own and the two overlapping layers add up.
        // The area is stretched 80 units past the top, so the glow's centre sits 40 above the middle.
        RectTransform glowArea = new GameObject("Glow Area", typeof(RectTransform)).GetComponent<RectTransform>();
        glowArea.SetParent(t, false);
        UITheme.Stretch(glowArea);
        glowArea.offsetMax = new Vector2(0f, 80f);
        Image glow = UITheme.NewImage("Glow", glowArea, Color.white, 0f);
        glow.sprite = Backdrop;
        glow.type = Image.Type.Simple;
        glow.raycastTarget = true; // Blocks clicks to the menu underneath
        AspectRatioFitter fitter = glow.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 1600f / 900f;

        Sprite wordmark = Resources.Load<Sprite>("UI/CapshotsWordmark");
        if (wordmark != null)
        {
            Image logo = UITheme.NewImage("Wordmark", t, Color.white, 0f);
            logo.sprite = wordmark;
            logo.type = Image.Type.Simple;
            logo.preserveAspect = true;
            float width = 420f;
            UITheme.Place(logo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(width, width * wordmark.rect.height / wordmark.rect.width));
        }

        screen.title = UITheme.NewText("Map", t, 64, UITheme.TextColor, TextAnchor.MiddleCenter);
        UITheme.Place(screen.title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1200f, 80f));
        UITheme.AddShadow(screen.title);

        screen.subtitle = UITheme.NewText("Mode", t, 22, UITheme.AccentText, TextAnchor.MiddleCenter);
        UITheme.Place(screen.subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(1200f, 30f));

        Image ring = UITheme.NewImage("Spinner", t, UITheme.AccentText, 0f);
        ring.sprite = Ring;
        ring.type = Image.Type.Simple;
        screen.spinner = ring.rectTransform;
        UITheme.Place(screen.spinner, new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(44f, 44f));
        screen.spinner.pivot = new Vector2(0.5f, 0.5f);

        screen.status = UITheme.NewText("Status", t, 20, UITheme.TextColor, TextAnchor.MiddleCenter);
        UITheme.Place(screen.status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -190f), new Vector2(1200f, 30f));

        screen.tip = UITheme.NewText("Tip", t, 16, UITheme.MutedText, TextAnchor.MiddleCenter);
        UITheme.Place(screen.tip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1400f, 24f));

        Image track = UITheme.NewImage("Progress", t, new Color(1f, 1f, 1f, 0.06f), 0f);
        UITheme.Row(track.rectTransform, 0f, 4f);
        track.rectTransform.anchorMin = Vector2.zero;
        track.rectTransform.anchorMax = new Vector2(1f, 0f);
        track.rectTransform.pivot = new Vector2(0.5f, 0f);
        track.rectTransform.anchoredPosition = Vector2.zero;
        RectMask2D mask = track.gameObject.AddComponent<RectMask2D>();
        mask.enabled = true;
        Image bar = UITheme.NewImage("Sweep", track.transform, UITheme.AccentText, 0f);
        screen.sweep = bar.rectTransform;
        UITheme.Stretch(screen.sweep);

        root.SetActive(false);
        return screen;
    }

    private static readonly Color BackgroundColor = new Color(0.015f, 0.02f, 0.035f, 1f);
    private const float GlowStrength = 0.22f;

    private static Sprite ring;
    private static Sprite backdrop;

    // A three-quarter ring that fades out along its length, for the spinner
    private static Sprite Ring
    {
        get
        {
            if (ring != null) return ring;
            const int size = 64;
            Texture2D tex = Texture("LoadingScreen Ring", size);
            Color32[] pixels = new Color32[size * size];
            float outer = size * 0.5f - 1f, inner = outer - 6f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f);
                    float d = p.magnitude;
                    float edge = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                    float angle = Mathf.Repeat(Mathf.Atan2(p.y, p.x) / (2f * Mathf.PI), 1f);
                    float tail = Mathf.Clamp01((angle - 0.25f) / 0.75f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(edge * tail * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return ring;
        }
    }

    // The background colour with the accent glow blended over it, opaque and dithered. 1600 x 900,
    // about the size it's drawn at on a 1080p canvas: at half size the upscale smeared the dither
    // into a blurry, low-res look. It's stretched to cover the screen; its edge is exactly the
    // background colour, so the glow fades out evenly whatever the screen's shape.
    private static Sprite Backdrop
    {
        get
        {
            if (backdrop != null) return backdrop;
            const int width = 1600, height = 900;
            Texture2D tex = Texture("LoadingScreen Backdrop", width, height);
            Color32[] pixels = new Color32[width * height];

            // Blend in linear space, like the UI does in this project, then dither the sRGB result
            Color bg = BackgroundColor.linear;
            Color accent = UITheme.Accent.linear;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / width - 0.5f, (y + 0.5f) / height - 0.5f) * 2f;
                    float a = 1f - Mathf.SmoothStep(0f, 1f, p.magnitude);
                    float alpha = a * a * GlowStrength;
                    Color c = Color.Lerp(bg, accent, alpha).gamma;
                    // Triangular noise of up to one shade either way breaks up the steps between shades
                    float noise = Hash(x, y) + Hash(x + 7919, y + 104729) - 1f;
                    pixels[y * width + x] = new Color32(Dither(c.r, noise), Dither(c.g, noise), Dither(c.b, noise), 255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            backdrop = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            return backdrop;
        }
    }

    private static byte Dither(float value, float noise)
    {
        return (byte)Mathf.Clamp(Mathf.FloorToInt(value * 255f + 0.5f + noise), 0, 255);
    }

    // A cheap repeatable 0..1 value per pixel
    private static float Hash(int x, int y)
    {
        uint h = (uint)x * 374761393u + (uint)y * 668265263u;
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / 16777215f;
    }

    private static Texture2D Texture(string name, int size) => Texture(name, size, size);

    private static Texture2D Texture(string name, int width, int height)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
    }

    #endregion
}
