using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The live combat HUD for the local player, on top of the layout in UITheme.StyleHud (which adds this
// to the HUD when the map loads). Local presentation only: it reads what Player and Weapon hand it
// and never changes game state.
//  - Health: the number over the bar (white, amber from half, red from a quarter), a red trail on
//    the bar showing the damage just taken that drains away after a moment, a short flash on every
//    hit and a small pulse as health regenerates.
//  - Weapon: shots left, coloured as they run low, over a meter with one segment per shot (a smooth
//    bar for the machine gun's many), the segment being refilled showing how close the next shot is.
//    The line left of the count names the form, or says what's stopping the gun firing (swapping the
//    cell, transforming, out of energy). The four forms sit in a strip above with their keys: the
//    active one lit and underlined, a change of form filling in under the one it's becoming, and in
//    Gun Game the forms you can't pick dimmed.
//  - Crosshair: four small ticks round the dot that sit where the gun's spread currently reaches
//    (wider while moving or in the air, tight while aiming) and kick out a little with each shot.
//  - Hit marker: four diagonal ticks that snap in round the crosshair, in five tiers that each look
//    different even at a glance (see HitTier): a hit, a head hit (gold), a kill (heavier, drifting
//    out, with a ring), a cap shot kill (gold, heaviest, with the ring and the cap logo and CAPSHOT
//    stamped just above), and a spawn shield (small, light blue). FeedbackUI decides what to show,
//    once a frame, from the host's confirmations.
//  - The crosshair's colour, size, dot and spread ticks follow the settings (GameSettings.Crosshair*),
//    live while the settings are open.
//  - Ultrawide screens: the HUD keeps to a centred area (HudRules.SafeInset), so health and the gun
//    stay near the crosshair.
// Everything is safe to call when there's no HUD (static methods do nothing).
public class HudFeedback : MonoBehaviour
{
    public static HudFeedback Instance { get; private set; }

    private RectTransform hudRect;
    private RectTransform canvasRect;
    private float appliedInset = -1f;

    #region Health fields

    private RectTransform bar;
    private RectTransform chip;
    private Image chipImage;
    private Image track;
    private Color trackColor;
    private Text value;
    private int shownHealth = -1;
    private float barRatio = 1f, chipRatio = 1f;
    private float chipHoldUntil;
    private float hitFlash, healPulse;

    private static readonly Color ChipColor = new Color(1f, 0.36f, 0.30f, 0.9f);
    private static readonly Color TrackHit = new Color(0.55f, 0.05f, 0.05f, 0.8f);
    private const float ChipHold = 0.45f;     // Seconds the trail waits before draining
    private const float ChipDrain = 0.9f;     // Bar widths a second
    private const float FlashTime = 0.3f;

    #endregion

    #region Weapon fields

    private Text weaponName;
    private float weaponPop;
    // The form's name, and the status shown instead of it while the gun can't fire
    private string formName = "";
    private string shownStatus;

    private Text ammoText;
    private int shownShots = -1, shownFull = -1;
    private Color shownCountColor;

    // The energy meter: one segment per shot, or a single smooth one (HudRules.EnergySegments)
    private RectTransform meter;
    private readonly List<RectTransform> segmentFills = new List<RectTransform>();
    private readonly List<Image> segmentImages = new List<Image>();
    private readonly List<Image> segmentTracks = new List<Image>();
    private int segmentCount = -1;
    private RectTransform swapFill;
    private Image swapImage;
    private const float SegmentGap = 3f;

    // The form strip
    private RectTransform strip;
    private class Slot
    {
        public RectTransform root;
        public Image plate, icon, underline, progress;
        public Text key;
    }
    private readonly List<Slot> slots = new List<Slot>();
    private int activeSlot = -1;
    private bool canSwitchForms = true;
    private Gun[] shownLoadout;
    private string rawFormName;
    private float morphShown = -2f;
    // One sprite per gun icon texture for the whole game, instead of new ones every spawn
    private static readonly Dictionary<Texture2D, Sprite> iconSprites = new Dictionary<Texture2D, Sprite>();

    #endregion

    #region Crosshair fields

    private RectTransform[] ticks;
    private Graphic[] tickGraphics;
    // The centre dot (styled in UITheme.StyleHud; Weapon fades it while aiming), and the settings' look
    private Image dot;
    private Color crosshairColour = Color.white;
    private float crosshairSize = 1f;
    private bool showTicks = true;
    private float gap, kick, tickAlpha;
    private int crosshairFrame = -1;
    private const float MinGap = 7f, MaxGap = 90f;
    private float targetGap = MinGap;

    // The hit marker: four diagonal ticks (Weapon's scene Hitmark image is no longer shown)
    private RectTransform[] marks;
    private Image[] markImages;
    private float markTime = -1f;
    private HudRules.HitTier markTier;
    private float markStrength;

    // Kill confirmation: a ring out from the crosshair, and the cap shot stamp above it
    private Image killRing;
    private float killRingTime = -1f;
    private RectTransform stamp;
    private CanvasGroup stampGroup;
    private float stampTime = -1f;
    private const float KillRingTime = 0.4f;
    private const float StampPop = 0.12f, StampHold = 0.75f, StampFade = 0.3f;

    #endregion

    #region Setup

    public static void Attach(Transform hud, RectTransform healthBar, Image healthTrack, Text healthValue, Text weaponLabel, Transform crosshair,
        RectTransform ammo = null, Text ammoCount = null, RectTransform formStrip = null)
    {
        if (hud == null) return;
        HudFeedback feedback = hud.GetComponent<HudFeedback>();
        if (feedback == null) feedback = hud.gameObject.AddComponent<HudFeedback>();
        feedback.Setup(healthBar, healthTrack, healthValue, weaponLabel, crosshair, ammo, ammoCount, formStrip);
    }

    private void Awake() => Instance = this;

    // The HUD shows again (a new spawn): nothing left over from the last life's hits
    private void OnEnable()
    {
        markTime = killRingTime = stampTime = -1f;
        if (markImages != null) foreach (Image mark in markImages) mark.enabled = false;
        if (killRing != null) killRing.enabled = false;
        if (stamp != null) stamp.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        GameSettings.CrosshairChanged -= ApplyCrosshairSettings;
    }

    private void Setup(RectTransform healthBar, Image healthTrack, Text healthValue, Text weaponLabel, Transform crosshair,
        RectTransform ammo, Text ammoCount, RectTransform formStrip)
    {
        Instance = this;
        hudRect = transform as RectTransform;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvasRect = canvas.rootCanvas.transform as RectTransform;

        bar = healthBar;
        track = healthTrack;
        if (track != null) trackColor = track.color;
        value = healthValue;
        weaponName = weaponLabel;
        ammoText = ammoCount;
        strip = formStrip;

        // The damage trail: a copy of the bar's shape, drawn just under it
        if (bar != null && chip == null)
        {
            chipImage = UITheme.NewImage("Damage Trail", bar.parent, ChipColor, 0f);
            chip = chipImage.rectTransform;
            UITheme.Place(chip, Vector2.zero, bar.anchoredPosition, bar.sizeDelta);
            chip.SetSiblingIndex(bar.GetSiblingIndex());
            chip.localScale = new Vector3(1f, 1f, 1f);
        }

        if (ammo != null && meter == null) BuildMeter(ammo);
        if (crosshair != null && ticks == null) BuildTicks(crosshair);
        if (crosshair != null && killRing == null) BuildKillConfirm(crosshair);
        if (crosshair != null && marks == null) BuildHitMarker(crosshair);
        if (crosshair != null) dot = crosshair.Find("Dot")?.GetComponent<Image>();
        GameSettings.CrosshairChanged -= ApplyCrosshairSettings;
        GameSettings.CrosshairChanged += ApplyCrosshairSettings;
        ApplyCrosshairSettings();
    }

    private void BuildMeter(RectTransform ammo)
    {
        meter = new GameObject("Energy", typeof(RectTransform)).GetComponent<RectTransform>();
        meter.SetParent(ammo, false);
        UITheme.Place(meter, new Vector2(1f, 0f), Vector2.zero, new Vector2(UITheme.HudClusterWidth, UITheme.HudBarHeight));

        // Swapping the cell: an accent bar sweeping across the whole meter, over the segments
        swapImage = UITheme.NewImage("Cell Swap", meter, UITheme.AccentText, 0f);
        swapFill = swapImage.rectTransform;
        UITheme.Place(swapFill, Vector2.zero, Vector2.zero, meter.sizeDelta);
        swapFill.localScale = new Vector3(0f, 1f, 1f);
        swapImage.enabled = false;
    }

    // Builds the meter's segments for a form: one per shot, or one smooth bar
    private void LayoutSegments(int count)
    {
        if (count == segmentCount || meter == null) return;
        segmentCount = count;
        int needed = Mathf.Max(1, count);
        float width = (UITheme.HudClusterWidth - SegmentGap * (needed - 1)) / needed;

        while (segmentTracks.Count < needed)
        {
            Image back = UITheme.NewImage("Segment", meter, UITheme.HudTrack, 0f);
            Image fill = UITheme.NewImage("Fill", back.transform, UITheme.HudText, 0f);
            segmentTracks.Add(back);
            segmentImages.Add(fill);
            segmentFills.Add(fill.rectTransform);
        }
        for (int i = 0; i < segmentTracks.Count; i++)
        {
            bool used = i < needed;
            segmentTracks[i].gameObject.SetActive(used);
            if (!used) continue;
            UITheme.Place(segmentTracks[i].rectTransform, Vector2.zero, new Vector2(i * (width + SegmentGap), 0f), new Vector2(width, UITheme.HudBarHeight));
            UITheme.Place(segmentFills[i], Vector2.zero, Vector2.zero, new Vector2(width, UITheme.HudBarHeight));
        }
        // The swap sweep stays on top
        swapFill.SetAsLastSibling();
    }

    private void BuildTicks(Transform crosshair)
    {
        ticks = new RectTransform[4];
        tickGraphics = new Graphic[4];
        for (int i = 0; i < 4; i++)
        {
            Image tick = UITheme.NewImage("Spread Tick " + i, crosshair, new Color(1f, 1f, 1f, 0.8f), 0f);
            Outline outline = tick.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.5f);
            outline.effectDistance = new Vector2(1f, -1f);
            RectTransform rect = tick.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            // Up, right, down, left; the long side points away from the dot
            rect.sizeDelta = i % 2 == 0 ? new Vector2(2f, 8f) : new Vector2(8f, 2f);
            tick.canvasRenderer.SetAlpha(0f);
            ticks[i] = rect;
            tickGraphics[i] = tick;
        }
        // Under the hit marker
        foreach (RectTransform rect in ticks) rect.SetAsFirstSibling();
        gap = MinGap;
    }

    private void BuildHitMarker(Transform crosshair)
    {
        marks = new RectTransform[4];
        markImages = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            Image mark = UITheme.NewImage("Hit Mark " + i, crosshair, Color.white, 0f);
            Outline outline = mark.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
            outline.effectDistance = new Vector2(1f, -1f);
            RectTransform rect = mark.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            // Pointing out from the crosshair along the diagonals: up-right, down-right, down-left, up-left
            rect.localEulerAngles = new Vector3(0f, 0f, -45f - 90f * i);
            mark.enabled = false;
            marks[i] = rect;
            markImages[i] = mark;
        }
    }

    private void BuildKillConfirm(Transform crosshair)
    {
        killRing = UITheme.NewImage("Kill Ring", crosshair, Color.white, 0f);
        killRing.sprite = UITheme.Ring;
        killRing.type = Image.Type.Simple;
        RectTransform ringRect = killRing.rectTransform;
        ringRect.anchorMin = ringRect.anchorMax = ringRect.pivot = new Vector2(0.5f, 0.5f);
        ringRect.anchoredPosition = Vector2.zero;
        killRing.enabled = false;
        // Under the hit marker, over the spread ticks
        killRing.transform.SetSiblingIndex(4);

        // CAPSHOT with the cap logo on its left, centred together just above the crosshair
        GameObject stampObject = new GameObject("Capshot Stamp", typeof(RectTransform), typeof(CanvasGroup));
        stamp = (RectTransform)stampObject.transform;
        stamp.SetParent(crosshair, false);
        UITheme.Place(stamp, new Vector2(0.5f, 0.5f), new Vector2(0f, 62f), new Vector2(240f, 34f));
        stampGroup = stampObject.GetComponent<CanvasGroup>();
        stampGroup.interactable = stampGroup.blocksRaycasts = false;

        Text word = UITheme.NewText("Word", stamp, 26, UITheme.Capshot, TextAnchor.MiddleCenter);
        word.text = "CAPSHOT";
        UITheme.AddShadow(word, 0.8f);
        float wordWidth = word.preferredWidth;
        const float iconSize = 30f, iconGap = 8f;
        float group = iconSize + iconGap + wordWidth;
        // The word moves right by half the icon and gap, the icon sits at the group's left end
        word.rectTransform.anchoredPosition = new Vector2((iconSize + iconGap) / 2f, 0f);

        Image icon = UITheme.NewImage("Cap", stamp, Color.white, 0f);
        icon.sprite = FeedbackUI.CapshotIcon;
        icon.preserveAspect = true;
        icon.enabled = icon.sprite != null;
        UITheme.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-group / 2f + iconSize / 2f, 0f), new Vector2(iconSize, iconSize));

        stampObject.SetActive(false);
    }

    #endregion

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
        if (Instance.formName == upper) return;
        Instance.formName = upper;
        // A status (transforming, swapping the cell) keeps the line until it's over; then the new name shows
        if (Instance.shownStatus == null)
        {
            Instance.weaponName.text = upper;
            Instance.weaponPop = 1f;
        }
    }

    // Every frame from the local player's weapon: its forms (one slot each in the strip), which one is
    // equipped, and whether the player picks the form (not in Gun Game, where the ladder does). Acts
    // only on changes. Called every frame rather than once on spawning because the body can spawn
    // before the match has built the HUD (a client joining).
    public static void SyncWeapon(Gun[] loadout, bool canSwitch, int active)
    {
        if (Instance == null || loadout == null || loadout.Length == 0) return;
        HudFeedback hud = Instance;
        if (!ReferenceEquals(loadout, hud.shownLoadout) || canSwitch != hud.canSwitchForms)
        {
            hud.shownLoadout = loadout;
            hud.canSwitchForms = canSwitch;
            if (hud.strip != null) hud.BuildSlots(loadout, canSwitch);
        }
        active = Mathf.Clamp(active, 0, loadout.Length - 1);
        if (active != hud.activeSlot)
        {
            hud.activeSlot = active;
            hud.RefreshSlots();
        }
        string name = loadout[active] != null ? loadout[active].gunName : "";
        if (!ReferenceEquals(name, hud.rawFormName))
        {
            hud.rawFormName = name;
            SetWeapon(name);
        }
    }

    // Every frame from the local player's weapon: the energy and what the gun is busy with.
    // reloadProgress and morphProgress are 0 to 1, or below 0 when not swapping the cell or transforming.
    public static void SetGunState(Text count, float energy, float maxEnergy, float costPerShot, float reloadProgress, float morphProgress)
    {
        if (Instance == null) return;
        Instance.OnGunState(count, energy, maxEnergy, costPerShot, reloadProgress, morphProgress);
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

    // Shows the hit marker for this frame's strongest confirmed hit (FeedbackUI merges them).
    // strength is the hit's share of the target's health, 0 to 1: a bigger hit makes a bigger marker
    // that holds a little longer, so machine gun hits tick and a sniper round lands.
    public static void HitMarker(HudRules.HitTier tier, float strength)
    {
        // Not while the HUD is hidden (dead): it would freeze and play on the next spawn
        if (Instance == null || Instance.marks == null || !Instance.isActiveAndEnabled) return;
        Instance.markTier = tier;
        Instance.markStrength = Mathf.Clamp01(strength);
        Instance.markTime = 0f;
    }

    // A kill adds the ring, a cap shot kill the stamp as well (from FeedbackUI, with the marker)
    public static void Confirmed(bool headshot, bool killed)
    {
        if (Instance == null || !killed || !Instance.isActiveAndEnabled) return;
        HudFeedback hud = Instance;
        if (hud.killRing != null)
        {
            hud.killRing.color = headshot ? UITheme.Capshot : Color.white;
            hud.killRing.enabled = true;
            hud.killRingTime = 0f;
        }
        if (headshot && hud.stamp != null)
        {
            hud.stamp.gameObject.SetActive(true);
            hud.stampTime = 0f;
        }
    }

    #endregion

    #region Health

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
            // Healing or a fresh spawn: no trail. Regenerating (not a spawn) gives the number a pulse.
            if (shownHealth > 0 && health > shownHealth) healPulse = 1f;
            chipRatio = ratio;
        }

        shownHealth = health;
        barRatio = ratio;
        if (value != null)
        {
            value.text = health + $"<size={Mathf.RoundToInt(value.fontSize * 0.45f)}><color={UITheme.Hex(UITheme.HudMuted)}> HP</color></size>";
        }
    }

    private void UpdateHealth(float dt)
    {
        if (chip != null)
        {
            if (Time.time >= chipHoldUntil) chipRatio = Mathf.MoveTowards(chipRatio, barRatio, ChipDrain * dt);
            chipRatio = Mathf.Max(chipRatio, barRatio);
            SetScaleX(chip, chipRatio);
            chipImage.enabled = chipRatio > barRatio + 0.001f;
        }
        if (value == null) return;
        Color healthColor = UITheme.HealthColor(barRatio);
        if (hitFlash > 0f || healPulse > 0f || value.rectTransform.localScale.x != 1f)
        {
            hitFlash = Mathf.Max(0f, hitFlash - dt / FlashTime);
            healPulse = Mathf.Max(0f, healPulse - dt / 0.4f);
            float f = hitFlash * hitFlash;
            float h = healPulse * healPulse;
            if (track != null) track.color = Color.Lerp(trackColor, TrackHit, f);
            value.color = Color.Lerp(healthColor, UITheme.Danger, f);
            value.rectTransform.localScale = Vector3.one * (1f + 0.2f * f + 0.06f * h);
        }
        else
        {
            value.color = healthColor;
        }
    }

    #endregion

    #region Weapon

    private void OnGunState(Text count, float energy, float maxEnergy, float cost, float reload, float morph)
    {
        if (count == null) count = ammoText;
        int shots = GunEnergy.ShotsLeft(energy, cost);
        int full = GunEnergy.ShotsLeft(maxEnergy, cost);
        bool canAfford = GunEnergy.CanFire(energy, cost);

        // Shots left large; shots in a full cell smaller and muted. Amber at a quarter of the cell or
        // less, red when there isn't a shot left. Rebuilt only when it changes.
        Color countColor = !canAfford ? UITheme.Danger : shots * 4 <= full ? UITheme.Warning : UITheme.HudText;
        if (count != null && (shots != shownShots || full != shownFull || countColor != shownCountColor))
        {
            shownShots = shots;
            shownFull = full;
            shownCountColor = countColor;
            count.supportRichText = true;
            count.color = countColor;
            count.text = shots.ToString("d2") + $"<size={Mathf.RoundToInt(count.fontSize * 0.45f)}><color={UITheme.Hex(UITheme.HudMuted)}> / " + full.ToString("d2") + "</color></size>";
        }

        // The meter
        LayoutSegments(HudRules.EnergySegments(maxEnergy, cost));
        if (meter != null)
        {
            bool swapping = reload >= 0f;
            swapImage.enabled = swapping;
            if (swapping) SetScaleX(swapFill, reload);
            // Full segments in the count's colour: white, amber when low, red when empty
            Color lit = countColor;
            if (segmentCount <= 0)
            {
                float fill = swapping ? 0f : maxEnergy > 0f ? Mathf.Clamp01(energy / maxEnergy) : 1f;
                SetScaleX(segmentFills[0], fill);
                SetColor(segmentImages[0], lit);
            }
            else
            {
                for (int i = 0; i < segmentCount; i++)
                {
                    float fill = swapping ? 0f : HudRules.SegmentFill(energy, cost, i);
                    SetScaleX(segmentFills[i], fill);
                    // A segment still refilling is drawn faint: that shot isn't ready yet
                    SetColor(segmentImages[i], fill >= 1f ? lit : UITheme.HudFaint);
                }
            }
        }

        // The line by the count: what's stopping the gun, or the form's name
        string status = reload >= 0f ? "SWAPPING CELL" : morph >= 0f ? "TRANSFORMING" : !canAfford ? "NO ENERGY" : null;
        if (weaponName != null && status != shownStatus)
        {
            shownStatus = status;
            weaponName.text = status ?? formName;
            if (status == null) weaponPop = 1f;
        }

        // The change of form fills in under the slot it's becoming
        if (!Mathf.Approximately(morph, morphShown))
        {
            morphShown = morph;
            RefreshSlots();
        }
    }

    private void UpdateWeapon(float dt)
    {
        if (weaponName == null) return;
        // The form's name pops white when it changes; a status holds its own colour
        weaponPop = Mathf.Max(0f, weaponPop - dt / 0.35f);
        float p = weaponPop * weaponPop;
        Color baseColor = shownStatus == null ? UITheme.AccentText : shownStatus == "NO ENERGY" ? UITheme.Danger : UITheme.HudText;
        // Written only when they change: an idle label leaves the canvas alone
        Vector3 scale = Vector3.one * (1f + 0.2f * p);
        if (weaponName.rectTransform.localScale != scale) weaponName.rectTransform.localScale = scale;
        SetColor(weaponName, Color.Lerp(baseColor, Color.white, p));

        // The touch controls cover the bottom right: there the SWAP button takes the strip's place
        if (strip != null)
        {
            bool show = slots.Count > 0 && !TouchControls.UsingTouch;
            if (strip.gameObject.activeSelf != show) strip.gameObject.SetActive(show);
        }
    }

    private void BuildSlots(Gun[] loadout, bool canSwitch)
    {
        canSwitchForms = canSwitch;
        if (slots.Count != loadout.Length)
        {
            // The prefab's own children (and the old icon column) give way to the strip
            for (int i = strip.childCount - 1; i >= 0; i--) Destroy(strip.GetChild(i).gameObject);
            slots.Clear();
            float width = (UITheme.HudClusterWidth - UITheme.HudGap * (loadout.Length - 1)) / Mathf.Max(1, loadout.Length);
            for (int i = 0; i < loadout.Length; i++) slots.Add(NewSlot(i, width));
        }
        for (int i = 0; i < loadout.Length; i++)
        {
            Gun gun = loadout[i];
            slots[i].icon.sprite = gun != null ? IconFor(gun.image) : null;
            slots[i].icon.enabled = slots[i].icon.sprite != null;
            slots[i].root.name = "Slot " + (i + 1) + (gun != null ? " " + gun.gunName : "");
        }
        RefreshSlots();
    }

    private Slot NewSlot(int index, float width)
    {
        Slot slot = new Slot();
        slot.plate = UITheme.NewImage("Slot " + (index + 1), strip, UITheme.HudPlate, 6f);
        slot.root = slot.plate.rectTransform;
        UITheme.Place(slot.root, Vector2.zero, new Vector2(index * (width + UITheme.HudGap), 0f), new Vector2(width, UITheme.FormSlotHeight));

        slot.icon = UITheme.NewImage("Icon", slot.root, Color.white, 0f);
        slot.icon.preserveAspect = true;
        RectTransform iconRect = slot.icon.rectTransform;
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(14f, 8f);
        iconRect.offsetMax = new Vector2(-6f, -6f);
        // A dark edge so the icons still read over sky and bright walls
        Outline edge = slot.icon.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color(0f, 0f, 0f, 0.5f);
        edge.effectDistance = new Vector2(1f, -1f);

        // The key in the top left corner (the number keys; a controller's bumpers step through them)
        slot.key = UITheme.NewText("Key", slot.root, UITheme.HudCaptionSize, UITheme.HudMuted, TextAnchor.UpperLeft);
        slot.key.rectTransform.offsetMin = new Vector2(6f, 0f);
        slot.key.rectTransform.offsetMax = new Vector2(0f, -3f);
        slot.key.text = (index + 1).ToString();

        slot.underline = UITheme.NewImage("Active", slot.root, UITheme.Accent, 1f);
        UITheme.Place(slot.underline.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(width - 12f, 3f));

        slot.progress = UITheme.NewImage("Transform", slot.root, UITheme.AccentText, 0f);
        UITheme.Place(slot.progress.rectTransform, Vector2.zero, new Vector2(6f, 0f), new Vector2(width - 12f, 3f));
        slot.progress.enabled = false;
        return slot;
    }

    // Active: lit and underlined (while transforming, the underline fills in instead). Available:
    // dimmed. Unavailable (Gun Game: the ladder picks): faint, without its key.
    private void RefreshSlots()
    {
        bool morphing = morphShown >= 0f;
        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            bool active = i == activeSlot;
            bool available = canSwitchForms || active;
            SetColor(slot.plate, active ? new Color(0.06f, 0.1f, 0.16f, 0.8f) : UITheme.HudPlate);
            SetColor(slot.icon, new Color(1f, 1f, 1f, active ? 0.95f : available ? 0.45f : 0.15f));
            if (slot.key.enabled != available) slot.key.enabled = available;
            SetColor(slot.key, active ? UITheme.HudText : UITheme.HudMuted);
            bool underline = active && !morphing;
            if (slot.underline.enabled != underline) slot.underline.enabled = underline;
            bool progress = active && morphing;
            if (slot.progress.enabled != progress) slot.progress.enabled = progress;
            if (progress) SetScaleX(slot.progress.rectTransform, morphShown);
        }
    }

    private static Sprite IconFor(Texture2D texture)
    {
        if (texture == null) return null;
        if (iconSprites.TryGetValue(texture, out Sprite sprite) && sprite != null) return sprite;
        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100);
        iconSprites[texture] = sprite;
        return sprite;
    }

    #endregion

    #region Crosshair

    // The crosshair as the settings have it: colour, size, the dot and the spread ticks
    private void ApplyCrosshairSettings()
    {
        crosshairColour = GameSettings.CrosshairColour;
        crosshairSize = GameSettings.CrosshairSize;
        showTicks = GameSettings.CrosshairTicks;
        if (dot != null)
        {
            dot.enabled = GameSettings.CrosshairDot;
            dot.color = new Color(crosshairColour.r, crosshairColour.g, crosshairColour.b, 0.92f);
            dot.rectTransform.sizeDelta = Vector2.one * Mathf.Round(5f * crosshairSize);
        }
        if (ticks != null)
        {
            for (int i = 0; i < ticks.Length; i++)
            {
                float length = Mathf.Round(8f * crosshairSize), width = Mathf.Max(2f, Mathf.Round(2f * crosshairSize));
                ticks[i].sizeDelta = i % 2 == 0 ? new Vector2(width, length) : new Vector2(length, width);
                tickGraphics[i].color = new Color(crosshairColour.r, crosshairColour.g, crosshairColour.b, 0.8f);
            }
        }
    }

    // The spread's reach on screen, in canvas units. The shot's direction is forward * 1000 plus up
    // to 'bloom' sideways (Weapon.Fire), so tan(angle) = bloom / 1000.
    private float SpreadToPixels(float bloom)
    {
        Camera cam = Camera.main;
        float fov = cam != null ? cam.fieldOfView : 70f;
        float halfHeight = canvasRect != null ? canvasRect.rect.height * 0.5f : 540f;
        float pixels = bloom / 1000f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * halfHeight;
        return Mathf.Clamp(MinGap + pixels, MinGap, MaxGap);
    }

    private void UpdateCrosshair(float dt)
    {
        // Spread ticks: hidden when no gun has fed them this frame (dead, no body)
        if (ticks != null)
        {
            bool live = crosshairFrame >= Time.frameCount - 1;
            kick = Mathf.MoveTowards(kick, 0f, dt * 40f);
            gap = Mathf.Lerp(gap, targetGap, 1f - Mathf.Exp(-dt * 14f));
            float distance = gap + kick + 4f;
            float alpha = live && showTicks ? tickAlpha : 0f;
            for (int i = 0; i < ticks.Length; i++)
            {
                Vector2 direction = i == 0 ? Vector2.up : i == 1 ? Vector2.right : i == 2 ? Vector2.down : Vector2.left;
                Vector2 position = direction * distance;
                if (ticks[i].anchoredPosition != position) ticks[i].anchoredPosition = position;
                tickGraphics[i].canvasRenderer.SetAlpha(alpha);
            }
        }

        UpdateHitMarker(dt);

        // The kill ring grows out and fades
        if (killRingTime >= 0f && killRing != null)
        {
            killRingTime += dt;
            float k = Mathf.Clamp01(killRingTime / KillRingTime);
            float ease = 1f - (1f - k) * (1f - k);
            float size = Mathf.Lerp(22f, 64f, ease);
            killRing.rectTransform.sizeDelta = new Vector2(size, size);
            Color c = killRing.color;
            c.a = 1f - k;
            killRing.color = c;
            if (k >= 1f)
            {
                killRing.enabled = false;
                killRingTime = -1f;
            }
        }

        // The cap shot stamp pops in, holds and fades
        if (stampTime >= 0f && stamp != null)
        {
            stampTime += dt;
            float t = stampTime;
            float scale = t < StampPop ? Mathf.Lerp(1.5f, 1f, 1f - Mathf.Pow(1f - t / StampPop, 3f)) : 1f;
            stamp.localScale = Vector3.one * scale;
            stampGroup.alpha = t < StampPop ? t / StampPop : t < StampPop + StampHold ? 1f : 1f - (t - StampPop - StampHold) / StampFade;
            if (t >= StampPop + StampHold + StampFade)
            {
                stamp.gameObject.SetActive(false);
                stampTime = -1f;
            }
        }
    }

    // How each tier looks: tick length and width, how far out from the centre, how far it drifts out
    // over its life (kills spread), and how long it pops in, holds and fades
    private struct MarkStyle
    {
        public Color color;
        public float length, width, distance, drift, pop, hold, fade;
    }

    private MarkStyle StyleFor(HudRules.HitTier tier, float strength)
    {
        switch (tier)
        {
            case HudRules.HitTier.Shield:
                return new MarkStyle { color = UITheme.Shield, length = 6f, width = 2f, distance = 8f, drift = 0f, pop = 0.04f, hold = 0.1f, fade = 0.2f };
            case HudRules.HitTier.Head:
                return new MarkStyle { color = UITheme.Capshot, length = Mathf.Lerp(9f, 12f, strength), width = 3f, distance = 10f, drift = 0f, pop = 0.06f, hold = Mathf.Lerp(0.1f, 0.2f, strength), fade = 0.15f };
            case HudRules.HitTier.Kill:
                return new MarkStyle { color = Color.white, length = 13f, width = 3f, distance = 10f, drift = 7f, pop = 0.07f, hold = 0.3f, fade = 0.25f };
            case HudRules.HitTier.CapshotKill:
                return new MarkStyle { color = UITheme.Capshot, length = 15f, width = 3.5f, distance = 11f, drift = 9f, pop = 0.08f, hold = 0.45f, fade = 0.3f };
            default:
                return new MarkStyle { color = Color.white, length = Mathf.Lerp(7f, 11f, strength), width = 2f, distance = 9f, drift = 0f, pop = 0.05f, hold = Mathf.Lerp(0.05f, 0.16f, strength), fade = 0.12f };
        }
    }

    private void UpdateHitMarker(float dt)
    {
        if (markTime < 0f || marks == null) return;
        markTime += dt;
        MarkStyle style = StyleFor(markTier, markStrength);
        float life = style.pop + style.hold + style.fade;
        if (markTime >= life)
        {
            markTime = -1f;
            for (int i = 0; i < marks.Length; i++) markImages[i].enabled = false;
            return;
        }

        // Snaps in from a little further out, then (for kills) drifts slowly outwards
        float pop = Mathf.Clamp01(markTime / style.pop);
        float snap = 1f - (1f - pop) * (1f - pop) * (1f - pop);
        float distance = style.distance + 6f * (1f - snap) + style.drift * Mathf.Clamp01(markTime / life);
        Color color = style.color;
        color.a = markTime < style.pop + style.hold ? 1f : 1f - (markTime - style.pop - style.hold) / style.fade;
        for (int i = 0; i < marks.Length; i++)
        {
            float angle = (45f - 90f * i) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            marks[i].sizeDelta = new Vector2(style.width, style.length);
            // The tick's middle sits half its length beyond the gap
            marks[i].anchoredPosition = direction * (distance + style.length / 2f);
            markImages[i].color = color;
            markImages[i].enabled = true;
        }
    }

    #endregion

    #region Update

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        KeepToSafeWidth();
        UpdateHealth(dt);
        UpdateWeapon(dt);
        UpdateCrosshair(dt);
    }

    // On a very wide screen the HUD keeps to a centred area (HudRules.SafeInset). Only for the HUD's
    // usual full-screen stretch; set only when the screen's shape changes.
    private void KeepToSafeWidth()
    {
        if (hudRect == null || canvasRect == null) return;
        if (hudRect.anchorMin.x != 0f || hudRect.anchorMax.x != 1f) return;
        Rect canvas = canvasRect.rect;
        float inset = HudRules.SafeInset(canvas.width, canvas.height);
        if (Mathf.Approximately(inset, appliedInset)) return;
        appliedInset = inset;
        hudRect.offsetMin = new Vector2(inset, hudRect.offsetMin.y);
        hudRect.offsetMax = new Vector2(-inset, hudRect.offsetMax.y);
    }

    // Setting a value only when it changes, so steady readouts don't make the canvas redraw them
    private static void SetScaleX(RectTransform rect, float x)
    {
        if (rect != null && rect.localScale.x != x) rect.localScale = new Vector3(x, 1f, 1f);
    }

    private static void SetColor(Graphic graphic, Color color)
    {
        if (graphic != null && graphic.color != color) graphic.color = color;
    }

    #endregion
}
