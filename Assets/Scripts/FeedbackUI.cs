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
    private AudioClip hitClip, headshotClip, killClip, hurtClip, pickupClip;

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

        ui.Play(ui.hurtClip, 0.9f);
    }

    // Local player hit someone else
    public static void Hit(bool headshot)
    {
        if (Instance == null) return;
        Instance.Play(headshot ? Instance.headshotClip : Instance.hitClip, headshot ? 0.8f : 0.6f);
    }

    // Local player picked up ammo
    public static void Pickup()
    {
        if (Instance == null) return;
        Instance.Play(Instance.pickupClip, 0.8f);
    }

    // Local player got a kill
    public static void Kill()
    {
        if (Instance == null) return;
        Instance.Play(Instance.killClip, 0.8f);
    }

    public static void ShowDeath(string killerName, string weaponName, bool headshot, float duration)
    {
        if (Instance == null) return;
        FeedbackUI ui = Instance;

        string text = string.IsNullOrEmpty(killerName) ? "YOU DIED" : "KILLED BY <color=#ff5a4a>" + killerName + "</color>";
        if (!string.IsNullOrEmpty(weaponName)) text += "\n<size=22>" + weaponName.ToUpper() + (headshot ? "  ·  HEADSHOT" : "") + "</size>";

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

    public static void AddKillFeed(string killer, string victim, string weapon, bool headshot, bool involvesMe)
    {
        if (Instance == null) return;
        FeedbackUI ui = Instance;

        Text entry = ui.NewText("Kill Feed Entry", ui.killFeed, 20, TextAnchor.MiddleRight);
        RectTransform rect = entry.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(600f, 26f);

        string killerColor = involvesMe ? "#ffd84a" : "#ff6a5a";
        string victimColor = involvesMe ? "#ffd84a" : "#ffffff";
        entry.text = $"<color={killerColor}>{killer}</color>  <color=#bbbbbb>[{weapon}{(headshot ? " · HS" : "")}]</color>  <color={victimColor}>{victim}</color>";

        ui.killFeedEntries.Insert(0, entry.gameObject);
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

    private IEnumerator ExpireKillFeedEntry(Text entry)
    {
        yield return new WaitForSeconds(killFeedTime);

        const float fade = 0.5f;
        for (float t = 0f; t < fade && entry != null; t += Time.deltaTime)
        {
            Color c = entry.color;
            c.a = 1f - t / fade;
            entry.color = c;
            yield return null;
        }

        if (entry == null) yield break;
        killFeedEntries.Remove(entry.gameObject);
        Destroy(entry.gameObject);
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

    private static float Tone(float t, float frequency) => frequency <= 0f ? 0f : Mathf.Sin(2f * Mathf.PI * frequency * t);
    private static float Decay(float t, float speed) => Mathf.Exp(-t * speed);

    #endregion
}
