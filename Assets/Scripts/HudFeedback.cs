using UnityEngine;
using UnityEngine.UI;

// Live HUD feedback for the local player, on top of the styling in UITheme.StyleHud (which adds this
// to the HUD when the map loads):
//  - Health: the number next to the bar, a red trail on the bar showing the damage just taken that
//    drains away after a moment, and a short flash of the number and track on every hit.
//  - Weapon: the active form's name above the ammo count, which pops when the form changes.
//  - Crosshair: four small ticks round the dot that sit where the gun's spread currently reaches
//    (wider while moving or in the air, tight while aiming) and kick out a little with each shot.
// Everything is safe to call when there's no HUD (static methods do nothing).
public class HudFeedback : MonoBehaviour
{
    public static HudFeedback Instance { get; private set; }

    // Health
    private RectTransform bar;
    private RectTransform chip;
    private Image chipImage;
    private Image track;
    private Color trackColor;
    private Text value;
    private int shownHealth = -1;
    private float barRatio = 1f, chipRatio = 1f;
    private float chipHoldUntil;
    private float hitFlash;

    private static readonly Color ChipColor = new Color(1f, 0.36f, 0.30f, 0.9f);
    private static readonly Color TrackHit = new Color(0.55f, 0.05f, 0.05f, 0.8f);
    private const float ChipHold = 0.45f;     // Seconds the trail waits before draining
    private const float ChipDrain = 0.9f;     // Bar widths a second
    private const float FlashTime = 0.3f;

    // Weapon
    private Text weaponName;
    private float weaponPop;

    // Crosshair
    private RectTransform[] ticks;
    private Graphic[] tickGraphics;
    private float gap, kick, tickAlpha;
    private int crosshairFrame = -1;
    private const float MinGap = 7f, MaxGap = 90f;

    public static void Attach(Transform hud, RectTransform healthBar, Image healthTrack, Text healthValue, Text weaponLabel, Transform crosshair)
    {
        if (hud == null) return;
        HudFeedback feedback = hud.GetComponent<HudFeedback>();
        if (feedback == null) feedback = hud.gameObject.AddComponent<HudFeedback>();
        feedback.Setup(healthBar, healthTrack, healthValue, weaponLabel, crosshair);
    }

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Setup(RectTransform healthBar, Image healthTrack, Text healthValue, Text weaponLabel, Transform crosshair)
    {
        Instance = this;
        bar = healthBar;
        track = healthTrack;
        if (track != null) trackColor = track.color;
        value = healthValue;
        weaponName = weaponLabel;

        // The damage trail: a copy of the bar's shape, drawn just under it
        if (bar != null && chip == null)
        {
            chipImage = UITheme.NewImage("Damage Trail", bar.parent, ChipColor, 0f);
            chip = chipImage.rectTransform;
            UITheme.Place(chip, Vector2.zero, bar.anchoredPosition, bar.sizeDelta);
            chip.SetSiblingIndex(bar.GetSiblingIndex());
            chip.localScale = new Vector3(1f, 1f, 1f);
        }

        if (crosshair != null && ticks == null) BuildTicks(crosshair);
    }

    private void BuildTicks(Transform crosshair)
    {
        ticks = new RectTransform[4];
        tickGraphics = new Graphic[4];
        for (int i = 0; i < 4; i++)
        {
            Image tick = UITheme.NewImage("Spread Tick " + i, crosshair, new Color(1f, 1f, 1f, 0.75f), 0f);
            Outline outline = tick.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.5f);
            outline.effectDistance = new Vector2(1f, -1f);
            RectTransform rect = tick.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            // Up, right, down, left; the long side points away from the dot
            rect.sizeDelta = i % 2 == 0 ? new Vector2(2f, 7f) : new Vector2(7f, 2f);
            tick.canvasRenderer.SetAlpha(0f);
            ticks[i] = rect;
            tickGraphics[i] = tick;
        }
        // Under the hit marker
        foreach (RectTransform rect in ticks) rect.SetAsFirstSibling();
        gap = MinGap;
    }

    #region Static API

    // Every frame from the local player
    public static void SetHealth(int health, int maxHealth)
    {
        if (Instance == null || maxHealth <= 0) return;
        Instance.OnHealth(Mathf.Max(0, health), maxHealth);
    }

    // The local player's gun changed form
    public static void SetWeapon(string name)
    {
        if (Instance == null || Instance.weaponName == null) return;
        string upper = string.IsNullOrEmpty(name) ? "" : name.ToUpperInvariant();
        if (Instance.weaponName.text == upper) return;
        Instance.weaponName.text = upper;
        Instance.weaponPop = 1f;
    }

    // Every frame from the local player's weapon: the spread as the gun uses it (see Weapon.CurrentBloom)
    // and how visible the crosshair is (it fades out while aiming down the sights)
    public static void SetSpread(float bloom, float alpha)
    {
        if (Instance == null || Instance.ticks == null) return;
        Instance.crosshairFrame = Time.frameCount;
        Instance.targetGap = Instance.SpreadToPixels(bloom);
        Instance.tickAlpha = alpha;
    }

    // The local player fired
    public static void Fired() => Fired(5f);

    // ...with the ticks jumping out 'amount' pixels (each gun form's Gun.crosshairKick)
    public static void Fired(float amount)
    {
        if (Instance == null) return;
        Instance.kick = Mathf.Min(Instance.kick + amount, Mathf.Max(14f, amount));
    }

    #endregion

    private float targetGap = MinGap;

    private void OnHealth(int health, int maxHealth)
    {
        float ratio = Mathf.Clamp01((float)health / maxHealth);
        if (health == shownHealth) return;

        if (shownHealth >= 0 && health < shownHealth)
        {
            // Hurt: the trail stays where the bar was, then drains
            chipRatio = Mathf.Max(chipRatio, barRatio);
            chipHoldUntil = Time.time + ChipHold;
            hitFlash = 1f;
        }
        else
        {
            // Healing or a fresh spawn: no trail
            chipRatio = ratio;
        }

        shownHealth = health;
        barRatio = ratio;
        if (value != null)
        {
            value.text = health + $"<size={Mathf.RoundToInt(value.fontSize * 0.55f)}><color={UITheme.Hex(UITheme.MutedText)}> HP</color></size>";
        }
    }

    // The spread's reach on screen, in canvas units. The shot's direction is forward * 1000 plus up
    // to 'bloom' sideways (Weapon.Fire), so tan(angle) = bloom / 1000.
    private float SpreadToPixels(float bloom)
    {
        Camera cam = Camera.main;
        float fov = cam != null ? cam.fieldOfView : 70f;
        float halfHeight = 540f;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) halfHeight = ((RectTransform)canvas.rootCanvas.transform).rect.height * 0.5f;
        float pixels = bloom / 1000f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * halfHeight;
        return Mathf.Clamp(MinGap + pixels, MinGap, MaxGap);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Health trail and flash
        if (chip != null)
        {
            if (Time.time >= chipHoldUntil) chipRatio = Mathf.MoveTowards(chipRatio, barRatio, ChipDrain * dt);
            chipRatio = Mathf.Max(chipRatio, barRatio);
            chip.localScale = new Vector3(chipRatio, 1f, 1f);
            chipImage.enabled = chipRatio > barRatio + 0.001f;
        }
        if (hitFlash > 0f || (value != null && value.rectTransform.localScale.x != 1f))
        {
            hitFlash = Mathf.Max(0f, hitFlash - dt / FlashTime);
            float f = hitFlash * hitFlash;
            if (track != null) track.color = Color.Lerp(trackColor, TrackHit, f);
            if (value != null)
            {
                value.color = Color.Lerp(UITheme.HealthColor(barRatio), UITheme.HealthLow, f);
                value.rectTransform.localScale = Vector3.one * (1f + 0.2f * f);
            }
        }
        else if (value != null)
        {
            value.color = UITheme.HealthColor(barRatio);
        }

        // Weapon name pop
        if (weaponName != null)
        {
            weaponPop = Mathf.Max(0f, weaponPop - dt / 0.35f);
            float p = weaponPop * weaponPop;
            weaponName.rectTransform.localScale = Vector3.one * (1f + 0.25f * p);
            weaponName.color = Color.Lerp(UITheme.AccentText, Color.white, p);
        }

        // Crosshair ticks: hidden when no gun has fed them this frame (dead, no body)
        if (ticks != null)
        {
            bool live = crosshairFrame >= Time.frameCount - 1;
            kick = Mathf.MoveTowards(kick, 0f, dt * 40f);
            gap = Mathf.Lerp(gap, targetGap, 1f - Mathf.Exp(-dt * 14f));
            float distance = gap + kick + 4f;
            float alpha = live ? tickAlpha : 0f;
            for (int i = 0; i < ticks.Length; i++)
            {
                Vector2 direction = i == 0 ? Vector2.up : i == 1 ? Vector2.right : i == 2 ? Vector2.down : Vector2.left;
                ticks[i].anchoredPosition = direction * distance;
                tickGraphics[i].canvasRenderer.SetAlpha(alpha);
            }
        }
    }
}
