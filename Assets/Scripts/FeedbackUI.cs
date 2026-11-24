using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Combat feedback for the local player: damage flash, damage direction, kill feed,
// "killed by" screen and short feedback sounds. Everything is built at runtime,
// so no scene or prefab changes are needed. The Manager creates it when a map loads.
public class FeedbackUI : MonoBehaviour
{
    public static FeedbackUI Instance { get; private set; }

    [Header("Damage")]
    public float maxFlashAlpha = 0.45f;
    public float flashFadeTime = 0.6f;
    public float indicatorTime = 1.5f;
    public float indicatorRadius = 140f;

    [Header("Kill feed")]
    public int maxKillFeedEntries = 5;
    public float killFeedTime = 5f;

    [Header("Sounds")]
    // Master level for the UI sounds. Each sound's own level is set so they sit 8-10 dB under a sniper
    // shot (measured, A-weighted): clear over the action without being louder than the guns.
    [Range(0f, 1f)] public float volume = 0.6f;

    private Font font;
    private RectTransform root;

    private Image damageFlash;
    private float flashAlpha;

    private RectTransform indicatorPivot;
    private Image indicator;
    private float indicatorTimeLeft;
    private Vector3 attackerPosition;
    private Transform viewer;

    private Text deathText;
    private Text protectionText;
    private Coroutine deathRoutine;

    private RectTransform killFeed;
    private readonly List<GameObject> killFeedEntries = new List<GameObject>();

    private AudioSource audioSource;
    private AudioClip hitClip, headshotClip, killClip, capshotKillClip, hurtClip, pickupClip, deathClip;

    #region Setup

    public static FeedbackUI Create(Transform canvas, Font font)
    {
        GameObject go = new GameObject("Feedback UI", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        // First child: drawn over the world but under the HUD, scoreboard and pause menu
        go.transform.SetAsFirstSibling();

        FeedbackUI ui = go.AddComponent<FeedbackUI>();
        ui.font = font;
        ui.Build();
        return ui;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Build()
    {
        root = (RectTransform)transform;
        Stretch(root);

        // Full screen red flash when taking damage
        damageFlash = NewImage("Damage Flash", root, new Color(0.8f, 0f, 0f, 0f));
        Stretch(damageFlash.rectTransform);

        // Bar that circles the crosshair and points towards whoever shot you
        GameObject pivot = new GameObject("Damage Indicator", typeof(RectTransform));
        indicatorPivot = (RectTransform)pivot.transform;
        indicatorPivot.SetParent(root, false);
        Center(indicatorPivot, Vector2.zero);
        indicator = NewImage("Bar", indicatorPivot, new Color(1f, 0.15f, 0.1f, 0f));
        Center(indicator.rectTransform, new Vector2(90f, 10f));
        indicator.rectTransform.anchoredPosition = new Vector2(0f, indicatorRadius);

        // "Killed by" text in the middle of the screen while waiting to respawn
        deathText = NewText("Death Text", root, 32, TextAnchor.MiddleCenter);
        Center(deathText.rectTransform, new Vector2(900f, 120f));
        deathText.rectTransform.anchoredPosition = new Vector2(0f, 60f);
        deathText.gameObject.SetActive(false);

        // Spawn protection countdown under the crosshair
        protectionText = NewText("Spawn Protection Text", root, 22, TextAnchor.MiddleCenter);
        Center(protectionText.rectTransform, new Vector2(600f, 40f));
        protectionText.rectTransform.anchoredPosition = new Vector2(0f, -90f);
        protectionText.color = new Color(0.55f, 0.85f, 1f);
        protectionText.gameObject.SetActive(false);

        // Kill feed in the top right corner
        GameObject feed = new GameObject("Kill Feed", typeof(RectTransform));
        killFeed = (RectTransform)feed.transform;
        killFeed.SetParent(root, false);
        killFeed.anchorMin = killFeed.anchorMax = killFeed.pivot = new Vector2(1f, 1f);
        killFeed.anchoredPosition = new Vector2(-20f, -20f);
        killFeed.sizeDelta = new Vector2(600f, 300f);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        hitClip = MakeClip("Hit", 0.05f, t => Tone(t, 1900f) * Decay(t, 60f));
        headshotClip = MakeClip("Headshot", 0.18f, t => (Tone(t, 1500f) * Decay(t, 40f) + Tone(t, 2250f) * Decay(Mathf.Max(0f, t - 0.05f), 25f) * (t > 0.05f ? 1f : 0f)) * 0.6f);
        killClip = MakeClip("Kill", 0.35f, t => (Tone(t, 880f) + Tone(t, t > 0.09f ? 1320f : 0f)) * 0.5f * Decay(t, 9f));
        pickupClip = MakeClip("Pickup", 0.2f, t => (Tone(t, t < 0.07f ? 660f : 990f) * 0.5f + Tone(t, t < 0.07f ? 1320f : 1980f) * 0.15f) * Decay(t, 12f));
        // A cap shot kill: the kill sound's two notes plus a third higher up, and a shimmer on top
        capshotKillClip = MakeClip("Capshot Kill", 0.6f, t =>
            (Tone(t, 880f) + Tone(t, t > 0.07f ? 1320f : 0f) + Tone(t, t > 0.14f ? 1760f : 0f) * 0.9f) * 0.4f * Decay(t, 5.5f) +
            Tone(t, 3520f + 220f * Mathf.Sin(2f * Mathf.PI * 18f * t)) * 0.12f * Decay(Mathf.Max(0f, t - 0.14f), 6f) * (t > 0.14f ? 1f : 0f));
        deathClip = MakeDeathClip();
        hurtClip = MakeClip("Hurt", 0.18f, t => (Tone(t, Mathf.Lerp(140f, 55f, t / 0.18f)) * 0.8f + Random.Range(-0.25f, 0.25f)) * Decay(t, 18f));
    }

    #endregion

    #region Public API (safe to call when no FeedbackUI exists)

    // Local player was hit: flash the screen and point towards the attacker
    public static void Damaged(float fraction, Vector3? fromPosition, Transform viewerTransform)
    {
        if (Instance == null) return;
        FeedbackUI ui = Instance;

        ui.flashAlpha = Mathf.Max(ui.flashAlpha, Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(fraction * 2f)) * ui.maxFlashAlpha);

        if (fromPosition.HasValue && viewerTransform != null)
        {
            ui.attackerPosition = fromPosition.Value;
            ui.viewer = viewerTransform;
            ui.indicatorTimeLeft = ui.indicatorTime;
        }

        ui.Play(ui.hurtClip, 0.43f);
    }

    // Local player hit someone else
    public static void Hit(bool headshot)
    {
        if (Instance == null) return;
        Instance.Play(headshot ? Instance.headshotClip : Instance.hitClip, headshot ? 0.142f : 0.136f);
    }

    // Local player picked up ammo
    public static void Pickup()
    {
        if (Instance == null) return;
        Instance.Play(Instance.pickupClip, 0.154f);
    }

    // Local player got a kill: louder, and grander for a cap shot (headshot)
    public static void Kill(bool headshot = false)
    {
        if (Instance == null) return;
        if (headshot) Instance.Play(Instance.capshotKillClip, 0.42f);
        else Instance.Play(Instance.killClip, 0.32f);
    }

    // The local player died: the drone powering down
    public static void Died()
    {
        if (Instance == null) return;
        Instance.Play(Instance.deathClip, 0.3f);
    }

    public static void ShowDeath(string killerName, string weaponName, bool headshot, float duration)
    {
        if (Instance == null) return;
        FeedbackUI ui = Instance;

        string text = string.IsNullOrEmpty(killerName) ? "YOU DIED" : "KILLED BY <color=#ff5a4a>" + killerName + "</color>";
        if (!string.IsNullOrEmpty(weaponName)) text += "\n<size=22>" + weaponName.ToUpper() + (headshot ? "  ·  CAPSHOT" : "") + "</size>";

        if (ui.deathRoutine != null) ui.StopCoroutine(ui.deathRoutine);
        ui.deathRoutine = ui.StartCoroutine(ui.DeathText(text, duration));
    }

    // Shows the spawn protection countdown; 0 or less hides it
    public static void SpawnProtection(float secondsLeft)
    {
        if (Instance == null) return;
        Text text = Instance.protectionText;

        bool show = secondsLeft > 0f;
        if (text.gameObject.activeSelf != show) text.gameObject.SetActive(show);
        if (show) text.text = "SPAWN PROTECTION  " + secondsLeft.ToString("0.0");
    }

    // The cap logo (the game's icon), shown in the kill feed on a cap shot
    private static Sprite capshotIcon;
    private const float CapshotIconSize = 26f;
    // The gold of the logo's sparkle
    private const string CapshotColor = "#ffcc4d";

    // In team mode, killerColor and victimColor give each name its team colour.
    // A headshot is a cap shot: "KILLER  (cap logo) [SNIPER · CAPSHOT]  VICTIM", in gold.
    public static void AddKillFeed(string killer, string victim, string weapon, bool headshot, bool involvesMe, string killerColor = null, string victimColor = null)
    {
        if (Instance == null) return;
        FeedbackUI ui = Instance;

        GameObject entry = new GameObject("Kill Feed Entry", typeof(RectTransform));
        RectTransform rect = (RectTransform)entry.transform;
        rect.SetParent(ui.killFeed, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(600f, 26f);

        if (killerColor == null) killerColor = involvesMe ? "#ffd84a" : "#ff6a5a";
        if (victimColor == null) victimColor = involvesMe ? "#ffd84a" : "#ffffff";
        // In team mode the names are team coloured, so mark your own kills and deaths with an arrow
        string marker = involvesMe ? "<color=#ffd84a>></color> " : "";
        string killerPart = marker + $"<color={killerColor}>{killer}</color>";
        string weaponPart = headshot ? $"<color={CapshotColor}>[{weapon} · CAPSHOT]</color>" : $"<color=#bbbbbb>[{weapon}]</color>";
        string victimPart = $"<color={victimColor}>{victim}</color>";

        if (headshot && capshotIcon == null) capshotIcon = Resources.Load<Sprite>("UI/CapshotIcon");
        if (!headshot || capshotIcon == null)
        {
            Text line = ui.NewText("Text", rect, 20, TextAnchor.MiddleRight);
            UITheme.Stretch(line.rectTransform);
            line.text = killerPart + "  " + weaponPart + "  " + victimPart;
        }
        else
        {
            // Right to left: weapon and victim, the cap logo, then the killer
            Text right = ui.NewText("Text", rect, 20, TextAnchor.MiddleRight);
            UITheme.Stretch(right.rectTransform);
            right.text = weaponPart + "  " + victimPart;
            float iconRight = right.preferredWidth + 6f;

            Image icon = ui.NewImage("Capshot Icon", rect, Color.white);
            icon.sprite = capshotIcon;
            icon.preserveAspect = true;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(1f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-iconRight, 0f);
            iconRect.sizeDelta = new Vector2(CapshotIconSize, CapshotIconSize);

            Text left = ui.NewText("Killer", rect, 20, TextAnchor.MiddleRight);
            UITheme.Stretch(left.rectTransform);
            left.rectTransform.offsetMax = new Vector2(-(iconRight + CapshotIconSize + 6f), 0f);
            left.text = killerPart;
        }

        ui.killFeedEntries.Insert(0, entry);
        while (ui.killFeedEntries.Count > ui.maxKillFeedEntries)
        {
            Destroy(ui.killFeedEntries[ui.killFeedEntries.Count - 1]);
            ui.killFeedEntries.RemoveAt(ui.killFeedEntries.Count - 1);
        }
        ui.LayoutKillFeed();
        ui.StartCoroutine(ui.ExpireKillFeedEntry(entry));
    }

    #endregion

    #region Update

    private void Update()
    {
        // Damage flash
        if (flashAlpha > 0f)
        {
            flashAlpha = Mathf.Max(0f, flashAlpha - Time.deltaTime * (maxFlashAlpha / flashFadeTime));
        }
        Color flash = damageFlash.color;
        flash.a = flashAlpha;
        damageFlash.color = flash;

        // Damage direction, kept pointing at where the shot came from as the player turns
        if (indicatorTimeLeft > 0f && viewer != null)
        {
            indicatorTimeLeft -= Time.deltaTime;

            Vector3 forward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up);
            Vector3 toAttacker = Vector3.ProjectOnPlane(attackerPosition - viewer.position, Vector3.up);
            float angle = Vector3.SignedAngle(forward, toAttacker, Vector3.up);
            indicatorPivot.localEulerAngles = new Vector3(0f, 0f, -angle);

            Color c = indicator.color;
            c.a = Mathf.Clamp01(indicatorTimeLeft / (indicatorTime * 0.5f)) * 0.9f;
            indicator.color = c;
        }
        else if (indicator.color.a > 0f)
        {
            Color c = indicator.color;
            c.a = 0f;
            indicator.color = c;
        }
    }

    private IEnumerator DeathText(string text, float duration)
    {
        deathText.text = text;
        deathText.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        deathText.gameObject.SetActive(false);
        deathRoutine = null;
    }

    private IEnumerator ExpireKillFeedEntry(GameObject entry)
    {
        yield return new WaitForSeconds(killFeedTime);

        // Fade the whole entry, coloured names (and the cap shot logo) included
        const float fade = 0.5f;
        CanvasGroup group = entry != null ? entry.AddComponent<CanvasGroup>() : null;
        for (float t = 0f; t < fade && entry != null; t += Time.deltaTime)
        {
            group.alpha = 1f - t / fade;
            yield return null;
        }

        if (entry == null) yield break;
        killFeedEntries.Remove(entry);
        Destroy(entry);
        LayoutKillFeed();
    }

    private void LayoutKillFeed()
    {
        for (int i = 0; i < killFeedEntries.Count; i++)
        {
            ((RectTransform)killFeedEntries[i].transform).anchoredPosition = new Vector2(0f, -i * 28f);
        }
    }

    #endregion

    #region Helpers

    private void Play(AudioClip clip, float clipVolume)
    {
        if (clip != null) audioSource.PlayOneShot(clip, clipVolume * volume);
    }

    private Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
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
        text.supportRichText = true;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        go.GetComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Center(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    // Short synthesized sounds, so no audio files need to be imported
    private static AudioClip MakeClip(string name, float length, System.Func<float, float> wave)
    {
        const int rate = 44100;
        int samples = Mathf.CeilToInt(length * rate);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            // Tiny fade in and out to avoid clicks
            float edge = Mathf.Clamp01(i / 64f) * Mathf.Clamp01((samples - i) / 256f);
            data[i] = Mathf.Clamp(wave(t), -1f, 1f) * edge;
        }
        AudioClip clip = AudioClip.Create(name, samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // A heavy hit, then the drone powering down: a buzzy tone sliding from 520 Hz to 45 Hz and fading
    // out, with a crackle of static as the lights go. About as loud as the kill sound.
    private static AudioClip MakeDeathClip()
    {
        const float length = 1.1f;
        float phase = 0f;
        float lastT = 0f;
        return MakeClip("Death", length, t =>
        {
            float dt = t - lastT;
            lastT = t;
            float k = t / length;
            // The pitch falls fast at first, then slowly: like a motor spinning down
            float frequency = Mathf.Lerp(45f, 520f, Mathf.Pow(1f - k, 2.2f));
            phase += 2f * Mathf.PI * frequency * dt;
            float buzz = Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 2f) * 0.25f + Mathf.Sign(Mathf.Sin(phase * 3f)) * 0.08f;
            float downEnvelope = Mathf.Clamp01(t / 0.02f) * Mathf.Pow(1f - k, 1.4f);

            // The hit: a low thump with a little noise, over in a tenth of a second
            float thump = (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(110f, 50f, Mathf.Clamp01(t / 0.12f)) * t) + Random.Range(-0.4f, 0.4f)) * Decay(t, 30f);

            // Static as the power goes, strongest in the middle
            float crackle = Random.value < 0.02f ? Random.Range(-0.5f, 0.5f) * Mathf.Sin(Mathf.PI * k) : 0f;

            return buzz * downEnvelope * 0.55f + thump * 0.9f + crackle;
        });
    }

    private static float Tone(float t, float frequency) => frequency <= 0f ? 0f : Mathf.Sin(2f * Mathf.PI * frequency * t);
    private static float Decay(float t, float speed) => Mathf.Exp(-t * speed);

    #endregion
}
