using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Combat feedback for the local player: damage flash, damage direction, kill feed,
// death screen ("ELIMINATED BY", how, and a respawn countdown) and short feedback sounds. Everything is built at runtime,
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

    // A red glow round the screen's edges while badly hurt, beating like a pulse that quickens as
    // health drops. Fed every frame by the local player; gone when it isn't (dead, or no body).
    private const float LowHealthThreshold = 0.35f;
    private Image lowHealthEdge;
    private float lowHealthAmount, lowHealthShown;
    private int lowHealthFrame = -1;
    private float pulsePhase;

    private RectTransform indicatorPivot;
    private Image indicator;
    private float indicatorTimeLeft;
    private Vector3 attackerPosition;
    private Transform viewer;

    private Text deathText;
    // Under the weapon line: "RESPAWN IN 3"
    private Text respawnText;
    // The death screen's second line: the weapon, and on a cap shot the cap logo and CAPSHOT
    private Text deathWeapon;
    private Image deathIcon;
    private Text protectionText;
    private Coroutine deathRoutine;

    private RectTransform killFeed;
    private readonly List<GameObject> killFeedEntries = new List<GameObject>();

    private AudioSource audioSource;
    private AudioClip hitClip, headshotClip, killClip, capshotKillClip, hurtClip, pickupClip, deathClip, shieldClip, calloutClip, levelUpClip;

    // Multi-kill and streak callouts, above the XP popup
    private RectTransform callout;
    private CanvasGroup calloutGroup;
    private Text calloutTitle, calloutDetail;
    private Coroutine calloutRoutine;

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

        // Red edges while badly hurt, under the damage flash
        lowHealthEdge = NewImage("Low Health", root, new Color(0.75f, 0f, 0f, 0f));
        lowHealthEdge.sprite = EdgeSprite();
        Stretch(lowHealthEdge.rectTransform);

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
        Center(deathText.rectTransform, new Vector2(900f, 44f));
        deathText.rectTransform.anchoredPosition = new Vector2(0f, 76f);
        // The weapon line under it, with room for the cap logo on its left
        deathWeapon = NewText("Weapon", deathText.transform, 22, TextAnchor.MiddleCenter);
        Center(deathWeapon.rectTransform, new Vector2(900f, 30f));
        deathWeapon.rectTransform.anchoredPosition = new Vector2(0f, -36f);
        deathIcon = NewImage("Capshot Icon", deathText.transform, Color.white);
        deathIcon.preserveAspect = true;
        Center(deathIcon.rectTransform, new Vector2(DeathIconSize, DeathIconSize));
        deathIcon.gameObject.SetActive(false);
        respawnText = NewText("Respawn", deathText.transform, 18, TextAnchor.MiddleCenter);
        Center(respawnText.rectTransform, new Vector2(900f, 26f));
        respawnText.rectTransform.anchoredPosition = new Vector2(0f, -70f);
        respawnText.color = UITheme.HudMuted;
        deathText.gameObject.SetActive(false);

        // Spawn protection countdown under the crosshair
        protectionText = NewText("Spawn Protection Text", root, 22, TextAnchor.MiddleCenter);
        Center(protectionText.rectTransform, new Vector2(600f, 40f));
        protectionText.rectTransform.anchoredPosition = new Vector2(0f, -90f);
        protectionText.color = UITheme.Shield;
        protectionText.gameObject.SetActive(false);

        // Kill feed in the top right corner
        GameObject feed = new GameObject("Kill Feed", typeof(RectTransform));
        killFeed = (RectTransform)feed.transform;
        killFeed.SetParent(root, false);
        killFeed.anchorMin = killFeed.anchorMax = killFeed.pivot = new Vector2(1f, 1f);
        killFeed.anchoredPosition = new Vector2(-20f, -20f);
        killFeed.sizeDelta = new Vector2(600f, 300f);

        // Multi-kill and streak callouts: a big gold word high in the middle, a smaller line under it.
        // Hung from the top, under the timer, with the XP popup (Manager.Xp) a fixed gap below, so the
        // two never meet whatever the screen's shape (the HUD is shorter on wide screens and phones)
        GameObject calloutObject = new GameObject("Callout", typeof(RectTransform), typeof(CanvasGroup));
        callout = (RectTransform)calloutObject.transform;
        callout.SetParent(root, false);
        callout.anchorMin = callout.anchorMax = callout.pivot = new Vector2(0.5f, 1f);
        callout.anchoredPosition = new Vector2(0f, -CalloutTop);
        callout.sizeDelta = new Vector2(900f, CalloutHeight);
        calloutGroup = calloutObject.GetComponent<CanvasGroup>();
        calloutGroup.interactable = calloutGroup.blocksRaycasts = false;
        calloutTitle = NewText("Title", callout, 46, TextAnchor.MiddleCenter);
        Center(calloutTitle.rectTransform, new Vector2(900f, 56f));
        calloutTitle.rectTransform.anchoredPosition = new Vector2(0f, 14f);
        calloutTitle.color = new Color(1f, 0.8f, 0.3f);
        calloutDetail = NewText("Detail", callout, 20, TextAnchor.MiddleCenter);
        Center(calloutDetail.rectTransform, new Vector2(900f, 28f));
        calloutDetail.rectTransform.anchoredPosition = new Vector2(0f, -28f);
        calloutObject.SetActive(false);

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
        // A dull knock: a shot that hit a spawn shield and did nothing
        shieldClip = MakeClip("Shield", 0.09f, t => (Tone(t, 420f) * 0.7f + Tone(t, 630f) * 0.3f) * Decay(t, 45f));
        // A callout: three quick rising notes, then a held chord
        calloutClip = MakeClip("Callout", 0.7f, t =>
            (Tone(t, 660f) * (t < 0.08f ? 1f : 0f) + Tone(t, 880f) * (t >= 0.08f && t < 0.16f ? 1f : 0f) +
             (Tone(t, 1100f) + Tone(t, 1320f) * 0.6f + Tone(t, 1650f) * 0.35f) * (t >= 0.16f ? Decay(t - 0.16f, 4.5f) : 0f)) * 0.45f);
        levelUpClip = MakeLevelUpClip();
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

    // The local player's health, 0 to 1, every frame while they're alive
    public static void LowHealth(float fraction)
    {
        if (Instance == null) return;
        Instance.lowHealthAmount = fraction > 0f && fraction < LowHealthThreshold ? 1f - fraction / LowHealthThreshold : 0f;
        Instance.lowHealthFrame = Time.frameCount;
    }

    // Local player hit someone else
    public static void Hit(bool headshot) => Hit(headshot, 0.5f);

    // ...louder the harder it landed (strength 0 to 1, its share of the target's health), so a stream
    // of machine gun hits ticks softly and a shotgun blast or sniper round lands with weight
    public static void Hit(bool headshot, float strength)
    {
        if (Instance == null) return;
        float level = Mathf.Lerp(0.8f, 1.2f, Mathf.Clamp01(strength));
        Instance.Play(headshot ? Instance.headshotClip : Instance.hitClip, (headshot ? 0.142f : 0.136f) * level);
    }

    // Local player's shot hit someone under spawn protection: it did nothing
    public static void ShieldHit()
    {
        if (Instance == null) return;
        Instance.Play(Instance.shieldClip, 0.14f);
    }

    // A multi-kill or streak for the local player: "DOUBLE KILL" big and gold, with an optional detail
    // line under it. Pops in, holds and fades; a new one replaces the last.
    public static void Callout(string title, string detail = null)
    {
        if (Instance == null) return;
        FeedbackUI ui = Instance;
        ui.calloutTitle.text = title;
        ui.calloutDetail.text = detail ?? "";
        ui.callout.gameObject.SetActive(true);
        if (ui.calloutRoutine != null) ui.StopCoroutine(ui.calloutRoutine);
        ui.calloutRoutine = ui.StartCoroutine(ui.ShowCallout());
        ui.Play(ui.calloutClip, 0.3f);
    }

    // The local player reached a new level: a fanfare, a moment later so it doesn't land on top of
    // the kill (or results) sound that earned it
    public static void LevelUp()
    {
        if (Instance == null) return;
        Instance.StartCoroutine(Instance.PlayLater(Instance.levelUpClip, 0.5f, 0.25f));
    }

    private IEnumerator PlayLater(AudioClip clip, float clipVolume, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Play(clip, clipVolume);
    }

    // An award comes up on the awards screen at the end of a match
    public static void AwardShown()
    {
        if (Instance == null) return;
        Instance.Play(Instance.killClip, 0.22f);
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

    // The death screen until respawning. killerName is null when nobody else got the kill; then
    // 'headline' says what happened instead (a fall, your own grenade), or ELIMINATED. duration is the
    // time to the respawn, counted down under it.
    public static void ShowDeath(string killerName, string weaponName, bool headshot, float duration, string headline = null)
    {
        if (Instance == null) return;
        FeedbackUI ui = Instance;

        string text = !string.IsNullOrEmpty(killerName)
            ? $"ELIMINATED BY <color={UITheme.Hex(UITheme.Danger)}>{ChatUI.Escape(killerName)}</color>"
            : string.IsNullOrEmpty(headline) ? "ELIMINATED" : headline;
        string weapon = string.IsNullOrEmpty(weaponName) ? "" : weaponName.ToUpper();
        // A cap shot: "(cap logo) SNIPER · CAPSHOT", the CAPSHOT in the logo's gold
        if (headshot) weapon = (weapon.Length > 0 ? weapon + "  ·  " : "") + $"<color={CapshotColor}>CAPSHOT</color>";
        ui.deathWeapon.text = weapon;

        Sprite icon = headshot ? CapshotIcon : null;
        ui.deathIcon.gameObject.SetActive(icon != null);
        ui.deathWeapon.rectTransform.anchoredPosition = new Vector2(0f, -36f);
        if (icon != null)
        {
            // Logo and text centred together as one group
            ui.deathIcon.sprite = icon;
            float width = ui.deathWeapon.preferredWidth;
            float group = DeathIconSize + IconGap + width;
            ui.deathIcon.rectTransform.anchoredPosition = new Vector2(-group / 2f + DeathIconSize / 2f, -36f);
            ui.deathWeapon.rectTransform.anchoredPosition = new Vector2(group / 2f - width / 2f, -36f);
        }

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

    // The cap logo (the game's icon), shown with a cap shot in the kill feed, on the death screen
    // and in the XP popup. Null if it's missing, and then they show without it.
    private static Sprite capshotIcon;
    public static Sprite CapshotIcon => capshotIcon != null ? capshotIcon : (capshotIcon = Resources.Load<Sprite>("UI/CapshotIcon"));
    // The callout's place, from the top of the screen: below the timer (and the touch SCORE and II
    // buttons beside it). CalloutBottom is where the XP popup's space starts.
    private const float CalloutTop = 128f;
    private const float CalloutHeight = 90f;
    public const float CalloutBottom = CalloutTop + CalloutHeight;

    private const float CapshotIconSize = 26f;
    private const float DeathIconSize = 30f;
    private const float IconGap = 6f;
    // The gold of the logo's sparkle
    public const string CapshotColor = "#ffcc4d";

    // In team mode, killerColor and victimColor give each name its team colour.
    // A headshot is a cap shot: "KILLER  (cap logo) [SNIPER · CAPSHOT]  VICTIM", in gold.
    public static void AddKillFeed(string killer, string victim, string weapon, bool headshot, bool involvesMe, string killerColor = null, string victimColor = null)
    {
        if (Instance == null) return;
        FeedbackUI ui = Instance;

        RectTransform rect = ui.NewKillFeedEntry("Kill Feed Entry", involvesMe);
        GameObject entry = rect.gameObject;

        if (killerColor == null) killerColor = involvesMe ? "#ffd84a" : "#ff6a5a";
        if (victimColor == null) victimColor = involvesMe ? "#ffd84a" : "#ffffff";
        // In team mode the names are team coloured, so mark your own kills and deaths with an arrow
        string marker = involvesMe ? "<color=#ffd84a>></color> " : "";
        string killerPart = marker + $"<color={killerColor}>{killer}</color>";
        string weaponPart = headshot ? $"<color={CapshotColor}>[{weapon} · CAPSHOT]</color>" : $"<color=#bbbbbb>[{weapon}]</color>";
        string victimPart = $"<color={victimColor}>{victim}</color>";

        Sprite iconSprite = headshot ? CapshotIcon : null;
        if (iconSprite == null)
        {
            Text line = ui.NewText("Text", rect, 20, TextAnchor.MiddleRight);
            FeedInset(line.rectTransform, 0f);
            line.text = killerPart + "  " + weaponPart + "  " + victimPart;
            FitKillFeedEntry(rect, line.preferredWidth);
        }
        else
        {
            // Right to left: weapon and victim, the cap logo, then the killer
            Text right = ui.NewText("Text", rect, 20, TextAnchor.MiddleRight);
            FeedInset(right.rectTransform, 0f);
            right.text = weaponPart + "  " + victimPart;
            float iconRight = right.preferredWidth + IconGap;

            Image icon = ui.NewImage("Capshot Icon", rect, Color.white);
            icon.sprite = iconSprite;
            icon.preserveAspect = true;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(1f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-(FeedPadding + iconRight), 0f);
            iconRect.sizeDelta = new Vector2(CapshotIconSize, CapshotIconSize);

            Text left = ui.NewText("Killer", rect, 20, TextAnchor.MiddleRight);
            FeedInset(left.rectTransform, iconRight + CapshotIconSize + IconGap);
            left.text = killerPart;
            FitKillFeedEntry(rect, iconRight + CapshotIconSize + IconGap + left.preferredWidth);
        }

        ui.AddKillFeedEntry(entry);
    }

    // A line of its own in the kill feed, like "VOLT IS ON A KILLING SPREE", in light grey
    public static void AddKillFeedNote(string text)
    {
        if (Instance == null) return;
        FeedbackUI ui = Instance;
        RectTransform rect = ui.NewKillFeedEntry("Kill Feed Note", false);
        Text line = ui.NewText("Text", rect, 18, TextAnchor.MiddleRight);
        FeedInset(line.rectTransform, 0f);
        line.color = new Color(0.85f, 0.85f, 0.85f);
        line.text = text;
        FitKillFeedEntry(rect, line.preferredWidth);
        ui.AddKillFeedEntry(rect.gameObject);
    }

    // Kill feed rows: each on its own dark, see-through plate sized to its text, so the names stay
    // readable over a bright sky. Your own kills and deaths get a gold stripe on the plate's left.
    private const float FeedRowHeight = 30f;
    private const float FeedRowGap = 5f;
    private const float FeedPadding = 12f;
    private static readonly Color FeedPlate = new Color(0.02f, 0.03f, 0.06f, 0.55f);
    private static readonly Color FeedStripe = new Color(1f, 0.85f, 0.29f, 0.95f);

    private RectTransform NewKillFeedEntry(string name, bool involvesMe)
    {
        GameObject entry = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)entry.transform;
        rect.SetParent(killFeed, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(600f, FeedRowHeight);

        Image plate = UITheme.NewImage("Plate", rect, FeedPlate, 6f);
        RectTransform plateRect = plate.rectTransform;
        plateRect.anchorMin = plateRect.anchorMax = plateRect.pivot = new Vector2(1f, 0.5f);
        plateRect.anchoredPosition = Vector2.zero;
        plateRect.sizeDelta = new Vector2(0f, FeedRowHeight);
        if (involvesMe)
        {
            Image stripe = UITheme.NewImage("Stripe", plateRect, FeedStripe, 1.5f);
            RectTransform stripeRect = stripe.rectTransform;
            stripeRect.anchorMin = new Vector2(0f, 0f);
            stripeRect.anchorMax = new Vector2(0f, 1f);
            stripeRect.pivot = new Vector2(0f, 0.5f);
            stripeRect.offsetMin = new Vector2(0f, 4f);
            stripeRect.offsetMax = new Vector2(3f, -4f);
        }
        return rect;
    }

    // The row's text: the full row, minus the padding and 'right' more on the right
    private static void FeedInset(RectTransform rect, float right)
    {
        Stretch(rect);
        rect.offsetMax = new Vector2(-(FeedPadding + right), 0f);
    }

    private static void FitKillFeedEntry(RectTransform entry, float contentWidth)
    {
        RectTransform plate = entry.Find("Plate") as RectTransform;
        if (plate != null) plate.sizeDelta = new Vector2(contentWidth + FeedPadding * 2f, FeedRowHeight);
    }

    private void AddKillFeedEntry(GameObject entry)
    {
        killFeedEntries.Insert(0, entry);
        while (killFeedEntries.Count > maxKillFeedEntries)
        {
            Destroy(killFeedEntries[killFeedEntries.Count - 1]);
            killFeedEntries.RemoveAt(killFeedEntries.Count - 1);
        }
        LayoutKillFeed();
        StartCoroutine(ExpireKillFeedEntry(entry));
    }

    #endregion

    #region Update

    private float killFeedInset = -1f;

    private void Update()
    {
        // On a very wide screen the kill feed keeps to the HUD's centred area (HudRules.SafeInset)
        RectTransform canvasRect = root.parent as RectTransform;
        if (canvasRect != null)
        {
            float inset = HudRules.SafeInset(canvasRect.rect.width, canvasRect.rect.height);
            if (!Mathf.Approximately(inset, killFeedInset))
            {
                killFeedInset = inset;
                killFeed.anchoredPosition = new Vector2(-20f - inset, -20f);
            }
        }

        // Damage flash
        if (flashAlpha > 0f)
        {
            flashAlpha = Mathf.Max(0f, flashAlpha - Time.deltaTime * (maxFlashAlpha / flashFadeTime));
        }
        Color flash = damageFlash.color;
        flash.a = flashAlpha;
        damageFlash.color = flash;

        // Low health: a pulse from about 70 to 140 beats a minute as health drops
        float lowTarget = lowHealthFrame >= Time.frameCount - 1 ? lowHealthAmount : 0f;
        lowHealthShown = Mathf.MoveTowards(lowHealthShown, lowTarget, Time.deltaTime * 2f);
        if (lowHealthShown > 0f)
        {
            pulsePhase += Time.deltaTime * Mathf.Lerp(1.2f, 2.3f, lowHealthShown);
            float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(pulsePhase * Mathf.PI)), 4f);
            Color edge = lowHealthEdge.color;
            edge.a = lowHealthShown * (0.35f + 0.3f * beat);
            lowHealthEdge.color = edge;
        }
        else if (lowHealthEdge.color.a > 0f)
        {
            Color edge = lowHealthEdge.color;
            edge.a = 0f;
            lowHealthEdge.color = edge;
        }

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

    private IEnumerator ShowCallout()
    {
        const float pop = 0.18f, hold = 1.4f, fade = 0.4f;
        for (float t = 0f; t < pop + hold + fade; t += Time.deltaTime)
        {
            float scale = t < pop ? Mathf.Lerp(1.6f, 1f, 1f - Mathf.Pow(1f - t / pop, 3f)) : 1f;
            callout.localScale = Vector3.one * scale;
            calloutGroup.alpha = t < pop ? t / pop : t < pop + hold ? 1f : 1f - (t - pop - hold) / fade;
            yield return null;
        }
        callout.gameObject.SetActive(false);
        calloutRoutine = null;
    }

    // The death screen, with the respawn counting down under it. The host respawns the player
    // respawnTime after the death, so counted from here (when the kill feed arrived) it's a close
    // estimate; at zero it says RESPAWNING until the body appears.
    private IEnumerator DeathText(string text, float duration)
    {
        deathText.text = text;
        deathText.gameObject.SetActive(true);
        int shown = -1;
        for (float left = duration; left > 0f; left -= Time.deltaTime)
        {
            // Back early (the respawn came sooner than counted): the screen goes with the new body
            if (left < duration - 1f && Player.Local != null) break;
            int seconds = HudRules.CountdownSeconds(left);
            if (seconds != shown)
            {
                shown = seconds;
                respawnText.text = "RESPAWN IN " + seconds;
            }
            yield return null;
        }
        if (Player.Local == null) respawnText.text = "RESPAWNING";
        // Gone once the body is back (or after a moment, if something holds the respawn up)
        for (float wait = 0f; wait < 3f && Player.Local == null; wait += Time.deltaTime) yield return null;
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
            ((RectTransform)killFeedEntries[i].transform).anchoredPosition = new Vector2(0f, -i * (FeedRowHeight + FeedRowGap));
        }
    }

    #endregion

    #region Helpers

    private void Play(AudioClip clip, float clipVolume)
    {
        if (clip != null) audioSource.PlayOneShot(clip, clipVolume * volume);
    }

    // Clear in the middle, solid towards the edges and corners: tinted by the image's colour
    private static Sprite edgeSprite;

    private static Sprite EdgeSprite()
    {
        if (edgeSprite != null) return edgeSprite;
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Low Health Edge";
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x / (size - 1f) * 2f - 1f, y / (size - 1f) * 2f - 1f);
                // A rounded square, so the glow hugs the sides instead of a circle
                float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(p.x), 4f) + Mathf.Pow(Mathf.Abs(p.y), 4f), 0.25f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.05f, d));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        edgeSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return edgeSprite;
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

    // Levelling up: a quick run up a major arpeggio (C E G C), then a big bright chord that swells and
    // rings out with a little vibrato, and a sparkle of high notes twinkling over it. Grander and
    // longer than a streak callout, about 1.6 seconds.
    private static AudioClip MakeLevelUpClip()
    {
        const float length = 1.6f;
        const float step = 0.085f;
        float[] run = { 523.25f, 659.25f, 783.99f, 1046.5f };
        float chordAt = step * run.Length;
        float[] chord = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f };
        float[] sparkle = { 2093f, 2637f, 3136f, 2637f, 3520f, 4186f };
        return MakeClip("Level Up", length, t =>
        {
            float v = 0f;
            // The run: each note bright (a little square-ish) and short
            int note = Mathf.FloorToInt(t / step);
            if (note < run.Length)
            {
                float nt = t - note * step;
                float f = run[note];
                v += (Tone(t, f) + Tone(t, f * 2f) * 0.3f + Tone(t, f * 3f) * 0.12f) * Decay(nt, 14f) * 0.55f;
            }
            // The chord: swells in over a few hundredths of a second, then rings out
            if (t >= chordAt)
            {
                float ct = t - chordAt;
                float vibrato = 1f + 0.006f * Mathf.Sin(2f * Mathf.PI * 5.5f * ct) * Mathf.Clamp01(ct / 0.3f);
                float swell = Mathf.Clamp01(ct / 0.04f) * Decay(ct, 2.2f);
                float c = 0f;
                for (int i = 0; i < chord.Length; i++)
                {
                    float f = chord[i] * vibrato;
                    c += (Tone(ct, f) + Tone(ct, f * 2f) * 0.2f) * (i == 0 ? 1f : 0.7f);
                }
                v += c / chord.Length * swell * 0.9f;
                // Sparkle: high pings, one after another
                int ping = Mathf.FloorToInt(ct / 0.07f);
                if (ping < sparkle.Length)
                {
                    float pt = ct - ping * 0.07f;
                    v += Tone(pt, sparkle[ping]) * Decay(pt, 22f) * 0.18f;
                }
            }
            return v * 0.8f;
        });
    }

    private static float Tone(float t, float frequency) => frequency <= 0f ? 0f : Mathf.Sin(2f * Mathf.PI * frequency * t);
    private static float Decay(float t, float speed) => Mathf.Exp(-t * speed);

    #endregion
}
