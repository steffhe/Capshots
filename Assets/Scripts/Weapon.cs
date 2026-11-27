using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Fusion;

// A player's gun: one sidearm that transforms between forms, with small pieces floating round it
// that rearrange for each form (GunSatellites) (shotgun, machine gun, pistol, sniper
// rifle: shortest range first, so "next" always reaches further), each with its own stats (the Gun
// assets in loadout). Everyone spawns holding defaultStartingGun, or the form they picked in the
// settings (GameSettings.StartingGun). All forms draw on one shared pool of energy,
// which refills by itself shortly after the last shot; each shot costs its form's energyPerShot.
// Trying to fire without enough energy swaps the energy cell (a full refill, which the reload key
// also does), unless the refill is about to cover the shot anyway. Changing form takes a moment, during which the gun can't fire or aim (GunMorph
// animates it).
// Firing, energy and switching run in FixedUpdateNetwork from the player's input: the host decides
// what is hit (with lag compensation, so shots are checked against where targets were on the
// shooter's screen) and the shooter's own game predicts the rest, so shooting feels instant. Laser
// beams, sounds and the gun model are local presentation.
public class Weapon : NetworkBehaviour
{
    #region Variables
    public Gun[] loadout;
    // The form a player spawns holding unless they picked another in the settings (bots always do)
    public Gun defaultStartingGun;
    public Transform weaponParent;
    public GameObject bulletholePrefab;
    public GameObject hitmark;
    public LayerMask canBeShot;
    public LineRenderer lineRend;

    public GameObject weaponImagePrefab;

    [Header("Game feel")]
    // Camera kick per shot, as a fraction of each gun's recoil value
    public float cameraRecoilScale = 0.15f;
    public float aimingRecoilMultiplier = 0.5f;

    [Header("Accuracy")]
    // Spread multiplier while jumping or falling, for every gun
    public float airborneBloomMultiplier = 2f;

    [Header("Energy")]
    // The shared pool every form fires from
    public float maxEnergy = 100f;
    // Refilling starts this long after the last shot...
    public float regenDelay = 0.9f;
    // ...at this many points a second
    public float regenRate = 40f;
    // Swapping the energy cell (a full refill) takes this long. Waiting for the refill is quicker
    // above about 64% energy, so both are worth doing.
    public float cellSwapTime = 1.8f;
    // Firing a sliver short of a shot's energy waits up to this long for the refill to cover it
    // instead of swapping the cell (see GunEnergy.ShouldSwapCell)
    public float autoSwapPatience = 0.25f;

    [Header("Transforming")]
    // How long the gun takes to change form; it can't fire or aim meanwhile
    public float morphTime = 0.5f;

    private const int MaxGuns = 4;
    // How long after spawning the starting form can still be picked up instantly (room for the
    // first input to arrive on a slow connection)
    private const float StartingFormSeconds = 1f;

    private GameObject currentWeapon;
    // The gun model being shown, for the drone's hand-orbs
    public GameObject GunModel => currentWeapon;
    private int shownIndex = -1;
    // The form whose model is showing, and when (Time.time) the last shot's effects played: for the
    // pieces floating round the gun (GunSatellites)
    public Gun ShownGun => loadout != null && shownIndex >= 0 && shownIndex < loadout.Length ? loadout[shownIndex] : null;
    public float LastShotTime { get; private set; } = -1f;

    // Looked up once instead of every shot or frame
    private Player player;
    private Look look;
    private Image hitmarkImage;
    // The HUD's centre dot, faded out while aiming so the gun's own sights take over
    private Graphic crosshairDot;
    private float crosshairAlpha = 1f;
    private readonly List<Image> arsenalImages = new List<Image>();
    // An accent bar beside the equipped form's icon
    private RectTransform arsenalMarker;
    // The gun form icons' holder on the HUD; hidden while touch controls are showing, which cover it
    private GameObject arsenalRoot;
    private const float ArsenalIconStep = 120f;
    private Transform weaponAnchor, stateAds, stateHip;
    private Coroutine hitmarkRoutine;

    public int EquippedIndex => Object != null && Object.IsValid ? Equipped : 0;
    public Gun CurrentGun => loadout != null && EquippedIndex >= 0 && EquippedIndex < loadout.Length ? loadout[EquippedIndex] : null;
    // Not while swapping the energy cell (the gun is tipped over) or changing form
    public bool isAiming => Object != null && Object.IsValid && Aiming && !ReloadTimer.IsRunning && !MorphTimer.IsRunning;
    public float CellSwapTime => cellSwapTime;

    // How far through a reload this player is (0 to 1), or -1 when not reloading. For the reload animation.
    public float ReloadProgress
    {
        get
        {
            if (Object == null || !Object.IsValid || !ReloadTimer.IsRunning) return -1f;
            float? remaining = ReloadTimer.RemainingTime(Runner);
            if (cellSwapTime <= 0f || remaining == null) return -1f;
            return Mathf.Clamp01(1f - remaining.Value / cellSwapTime);
        }
    }
    #endregion

    #region Networked state

    [Networked, OnChangedRender(nameof(OnEquippedChanged))] private int Equipped { get; set; }
    // The shared energy pool, 0 to maxEnergy
    [Networked] private float Energy { get; set; }
    [Networked] private TickTimer RegenDelay { get; set; }
    // Running while the gun changes form
    [Networked] private TickTimer MorphTimer { get; set; }
    [Networked] private TickTimer FireCooldown { get; set; }
    // A semi-automatic click that came a moment before the gun was ready: it fires as soon as it is
    // (see ShotRules.FireBufferSeconds), instead of being lost
    [Networked] private TickTimer FireQueue { get; set; }
    [Networked] private TickTimer ReloadTimer { get; set; }
    [Networked] private TickTimer BurstTimer { get; set; }
    [Networked] private int BurstLeft { get; set; }
    [Networked] private NetworkBool Aiming { get; set; }
    // The tick the sights went up, or 0 while they're down (also while swapping the cell or changing
    // form). The spread tightens over the gun's adsTime from then. Set once per aim, not every tick.
    [Networked] private int AimStartTick { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    // Counts every shot, so each one gets its own (but on every machine the same) random spread
    [Networked] private int ShotCount { get; set; }
    // Running for a moment after spawning. A first change of form in that time, before any shot,
    // is the starting form picked in the settings, and happens instantly.
    [Networked] private TickTimer StartingFormWindow { get; set; }
    [Networked] private NetworkBool HasChangedForm { get; set; }
    // Plasma grenades carried (see PlasmaGrenadeRules.MaxCarried), how many this body has thrown (each
    // throw's number, to tell them apart), and the moment after a throw when the gun can't fire
    [Networked] private int Grenades { get; set; }
    // A thrown grenade recharging (see PlasmaGrenadeRules.RechargeTime)
    [Networked] private TickTimer GrenadeRecharge { get; set; }
    [Networked] private int GrenadesThrown { get; set; }
    [Networked] private TickTimer ThrowTimer { get; set; }
    // Running while a grenade is held with the button down ("cooking"): its fuse is already burning
    [Networked] private TickTimer CookTimer { get; set; }

    #endregion

    #region Fusion Callbacks

    void Awake()
    {
        player = GetComponent<Player>();
        look = GetComponent<Look>();
        if (loadout.Length > MaxGuns) Debug.LogError($"Weapon supports at most {MaxGuns} guns");
        // Grenades bounce off whatever bullets hit
        PlasmaGrenade.SetWorldMask(canBeShot.value);
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            Energy = maxEnergy;
            Equipped = DefaultSlot;
            // Gun Game: the form for this player's place on the ladder
            Manager m = Manager.Instance;
            if (m != null && m.IsGunGame) Equipped = m.GunGameSlot(player.ActorId, loadout, DefaultSlot);
            Grenades = PlasmaGrenadeRules.MaxCarried;
            StartingFormWindow = TickTimer.CreateFromSeconds(Runner, StartingFormSeconds);
        }

        if (HasInputAuthority)
        {
            // The HUD is hidden while the local player has no body, and GameObject.Find can't see hidden objects
            if (Manager.Instance != null && Manager.Instance.Hud != null) Manager.Instance.Hud.SetActive(true);

            GameObject arsenal = GameObject.Find("HUD/Arsenal");
            foreach (Transform child in arsenal.transform)
            {
                GameObject.Destroy(child.gameObject);
            }

            arsenalRoot = arsenal;
            int ind = 0;
            foreach (Gun gun in loadout)
            {
                GameObject weaponimageObject = Instantiate(weaponImagePrefab, arsenal.transform);
                weaponimageObject.name = "WeaponImage" + ind.ToString();
                // Stacked upwards in canvas units (not screen pixels), so the spacing is the same at
                // any resolution: at 1440p and up, pixel spacing packed the icons into each other
                RectTransform iconRect = (RectTransform)weaponimageObject.transform;
                iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = Vector2.zero;
                iconRect.anchoredPosition = new Vector2(0f, ind * ArsenalIconStep);

                // Set the icon on the copy, not on the prefab asset
                Image image = weaponimageObject.GetComponent<Image>();
                Rect rect = new Rect(0, 0, gun.image.width, gun.image.height);
                image.sprite = Sprite.Create(gun.image, rect, new Vector2(0.5f, 0.5f), 100);
                // A dark edge so the dimmed icons still read over sky and bright walls (the thin
                // sniper rifle at the top of the stack all but vanished without one)
                Outline edge = weaponimageObject.GetComponent<Outline>();
                if (edge == null) edge = weaponimageObject.AddComponent<Outline>();
                edge.effectColor = new Color(0f, 0f, 0f, 0.55f);
                edge.effectDistance = new Vector2(1.5f, -1.5f);
                arsenalImages.Add(image);
                ind += 1;
            }
            // Right of the icons (200 x 100 each), moved to the equipped one in ShowGun
            Image marker = UITheme.NewImage("Active Marker", arsenal.transform, UITheme.Accent, 2f);
            arsenalMarker = marker.rectTransform;
            arsenalMarker.anchorMin = arsenalMarker.anchorMax = Vector2.zero;
            arsenalMarker.pivot = new Vector2(0f, 0.5f);
            arsenalMarker.sizeDelta = new Vector2(4f, 56f);

            hitmark = GameObject.Find("HUD/Crosshair/Hitmark");
            if (hitmark != null) hitmarkImage = hitmark.GetComponent<Image>();
            HideHitmark();
            GameObject dot = GameObject.Find("HUD/Crosshair/Dot");
            crosshairDot = dot != null ? dot.GetComponent<Graphic>() : null;
            SetCrosshairAlpha(1f);
            BuildGrenadeIndicator();
        }

        ShowGun(Equipped);
    }

    // The slot of defaultStartingGun (the first form if it isn't in the loadout)
    public int DefaultSlot => defaultStartingGun != null ? GunForms.IndexOf(loadout, defaultStartingGun.gunName, 0) : 0;

    // The form picked in the settings, which the player's first input after spawning asks for
    // (see InputCollector)
    public int PreferredStartingSlot => GunForms.IndexOf(loadout, GameSettings.StartingGun, DefaultSlot);

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // Killed (or gone) with a grenade in the hand: it drops where they were, still counting down
        if (hasState && HasStateAuthority && CookTimer.IsRunning && Manager.Instance != null && !Manager.Instance.Ending)
        {
            float left = CookTimer.RemainingTime(runner) ?? 0f;
            Manager.Instance.ThrowGrenade(cookOwner, cookActor, cookBody, GrenadesThrown, transform.position + Vector3.up * 0.3f, Vector3.zero, left);
        }

        // Dying mid-marker stops the coroutine before it can hide the marker
        StopAllCoroutines();
        hitmarkRoutine = null;
        HideHitmark();
    }

    public override void FixedUpdateNetwork()
    {
        if (player.IsDead) return;
        if (!player.ReadInput(out PlayerInputData input)) return;

        // The match is over: guns go quiet
        if (Manager.Instance != null && Manager.Instance.Ending)
        {
            Aiming = false;
            AimStartTick = 0;
            BurstLeft = 0;
            return;
        }

        NetworkButtons pressed = input.Buttons.GetPressed(PreviousButtons);
        PreviousButtons = input.Buttons;

        Aiming = input.Buttons.IsSet(InputButton.Aim);

        // Changing form cancels a cell swap or burst in progress, and takes a moment. The starting
        // form (the first change, before any shot) is picked up instantly instead.
        int slot = input.WeaponSlot - 1;
        // Gun Game: the ladder decides the form, not the player
        Manager gunGame = Manager.Instance;
        if (gunGame != null && gunGame.IsGunGame) slot = gunGame.GunGameSlot(player.ActorId, loadout, Equipped);
        if (slot >= 0 && slot < loadout.Length && slot != Equipped)
        {
            bool startingForm = !HasChangedForm && ShotCount == 0 && StartingFormWindow.IsRunning && !StartingFormWindow.Expired(Runner);
            HasChangedForm = true;
            Equipped = slot;
            ReloadTimer = TickTimer.None;
            BurstLeft = 0;
            FireQueue = TickTimer.None;
            MorphTimer = startingForm ? TickTimer.None : TickTimer.CreateFromSeconds(Runner, morphTime);
        }
        if (MorphTimer.Expired(Runner)) MorphTimer = TickTimer.None;
        if (ThrowTimer.Expired(Runner)) ThrowTimer = TickTimer.None;

        Gun gun = loadout[Equipped];

        if (ReloadTimer.Expired(Runner))
        {
            ReloadTimer = TickTimer.None;
            Energy = maxEnergy;
        }

        // The gun steadies while the sights are up; swapping the cell or changing form lowers them
        bool sightsUp = Aiming && !ReloadTimer.IsRunning && !MorphTimer.IsRunning;
        if (!sightsUp) AimStartTick = 0;
        else if (AimStartTick == 0) AimStartTick = Runner.Tick.Raw;

        // The energy refills by itself a little after the last shot
        if (!ReloadTimer.IsRunning && Energy < maxEnergy && RegenDelay.ExpiredOrNotRunning(Runner))
        {
            Energy = GunEnergy.Regenerate(Energy, maxEnergy, regenRate, Runner.DeltaTime);
        }

        // A thrown grenade comes back once it has recharged
        if (GrenadeRecharge.Expired(Runner))
        {
            GrenadeRecharge = TickTimer.None;
            if (Grenades < PlasmaGrenadeRules.MaxCarried) Grenades++;
            if (Grenades < PlasmaGrenadeRules.MaxCarried) GrenadeRecharge = TickTimer.CreateFromSeconds(Runner, PlasmaGrenadeRules.RechargeTime);
        }

        // A grenade in the hand: thrown when the button is let go, or it goes off in the hand if held
        // for the whole fuse
        if (CookTimer.IsRunning)
        {
            if (CookTimer.Expired(Runner))
            {
                CookTimer = TickTimer.None;
                GrenadeGoesOffInHand(input);
            }
            else if (!input.Buttons.IsSet(InputButton.Grenade))
            {
                float left = CookTimer.RemainingTime(Runner) ?? PlasmaGrenadeRules.FreeFuse;
                CookTimer = TickTimer.None;
                ThrowGrenade(input, left);
            }
            return;
        }

        if (BurstLeft > 0)
        {
            // The rest of a burst fires on its own, one shot every fireRate seconds
            if (BurstTimer.ExpiredOrNotRunning(Runner))
            {
                if (GunEnergy.CanFire(Energy, gun.energyPerShot)) FireBurstShot(gun, input);
                else BurstLeft = 0;
            }
        }
        else if (!ReloadTimer.IsRunning && !MorphTimer.IsRunning && !ThrowTimer.IsRunning)
        {
            if (pressed.IsSet(InputButton.Grenade) && Grenades > 0)
            {
                StartCooking();
                return;
            }

            bool fireInput;
            if (gun.burst == 1) fireInput = input.Buttons.IsSet(InputButton.Fire);
            else
            {
                // A click while the gun is still cooling down is remembered for a moment, so clicking
                // a touch early (as everyone does at speed) still fires on the first tick it can
                bool click = pressed.IsSet(InputButton.Fire);
                if (click && !FireCooldown.ExpiredOrNotRunning(Runner)) FireQueue = TickTimer.CreateFromSeconds(Runner, ShotRules.FireBufferSeconds);
                fireInput = click || (FireQueue.IsRunning && !FireQueue.Expired(Runner));
            }

            if (fireInput && FireCooldown.ExpiredOrNotRunning(Runner))
            {
                FireQueue = TickTimer.None;
                if (!GunEnergy.CanFire(Energy, gun.energyPerShot))
                {
                    // Swap the cell, unless the refill is about to cover the shot: then a click waits
                    // for it (held fire just keeps trying)
                    float untilRegen = RegenDelay.ExpiredOrNotRunning(Runner) ? 0f : RegenDelay.RemainingTime(Runner) ?? 0f;
                    if (GunEnergy.ShouldSwapCell(Energy, gun.energyPerShot, regenRate, untilRegen, autoSwapPatience)) StartReload();
                    else if (gun.burst != 1) FireQueue = TickTimer.CreateFromSeconds(Runner, autoSwapPatience + Runner.DeltaTime);
                }
                else if (gun.burst == 2)
                {
                    BurstLeft = Mathf.Max(1, gun.burstCount);
                    FireBurstShot(gun, input);
                }
                else
                {
                    Fire(input);
                }
            }

            if (pressed.IsSet(InputButton.Reload))
            {
                StartReload();
            }
        }
    }

    // Presentation only: gun model position and aiming
    void Update()
    {
        UpdateMuzzleFlash();
        if (Object == null || !Object.IsValid || currentWeapon == null) return;

        // Reloading is animated by DroneHands: the gun stays out and its energy cell is swapped

        if (HasInputAuthority && !Pause.paused) Aim(isAiming);
        if (HasInputAuthority)
        {
            SetCrosshairAlpha(Mathf.MoveTowards(crosshairAlpha, isAiming ? 0f : 1f, Time.deltaTime * 6f));
            // The spread ticks round the dot follow the gun's current spread
            HudFeedback.SetSpread(CurrentBloom(), crosshairAlpha);
        }

        //weapon position elasticity
        currentWeapon.transform.localPosition = Vector3.Lerp(currentWeapon.transform.localPosition, Vector3.zero, Time.deltaTime * 4f);
    }
    #endregion

    #region Public Methods

    // How steady aiming down the sights has made the gun, 0 (not aiming, or just raised) to 1 (as
    // accurate as aiming gets). The tick the sights go up counts as 0, so aiming and firing together
    // fires with the hip spread. Bots wait for 1 before a scoped shot.
    public float AimProgress
    {
        get
        {
            if (Object == null || !Object.IsValid || AimStartTick == 0) return 0f;
            Gun gun = CurrentGun;
            if (gun == null) return 0f;
            int ticks = Mathf.Max(0, Runner.Tick.Raw - AimStartTick);
            return ShotRules.AimProgress(ticks * Runner.DeltaTime, gun.adsTime);
        }
    }

    // How full the energy is, 0 to 1 (bots use it to decide when to swap the cell)
    public float EnergyFraction => Object != null && Object.IsValid && maxEnergy > 0f ? Energy / maxEnergy : 1f;

    // Plasma grenades carried
    public int GrenadeCount => Object != null && Object.IsValid ? Grenades : 0;

    // How far the next grenade has recharged, 0 to 1 (1 when there's nothing recharging)
    public float GrenadeRechargeProgress => Object != null && Object.IsValid && GrenadeRecharge.IsRunning
        ? PlasmaGrenadeRules.RechargeProgress(GrenadeRecharge.RemainingTime(Runner) ?? 0f) : 1f;

    // Seconds until the next grenade is back (0 when there's nothing recharging)
    public float GrenadeRechargeSeconds => Object != null && Object.IsValid && GrenadeRecharge.IsRunning
        ? GrenadeRecharge.RemainingTime(Runner) ?? 0f : 0f;

    // Host only: a grenade this player threw hurt someone. Hit marker and sound, like a shot.
    public void ConfirmHit(bool killed)
    {
        if (Object == null || !Object.IsValid) return;
        // A grenade blast lands hard
        RPC_HitConfirm(false, killed, 200);
    }

    public void RefreshAmmo(Text text)
    {
        if (text == null || Object == null || !Object.IsValid) return;
        // Shots left in this form large; shots in a full cell smaller and muted. The energy is
        // shared, so the numbers change with the form.
        Gun gun = CurrentGun;
        if (gun == null) return;
        int shots = GunEnergy.ShotsLeft(Energy, gun.energyPerShot);
        int full = GunEnergy.ShotsLeft(maxEnergy, gun.energyPerShot);
        text.supportRichText = true;
        // The count turns amber when a quarter of the cell or less is left, as a hint to swap it
        string count = shots.ToString("d2");
        if (shots * 4 <= full) count = $"<color={UITheme.Hex(UITheme.TimerWarning)}>{count}</color>";
        text.text = count + $"<size={Mathf.RoundToInt(text.fontSize * 0.55f)}><color={UITheme.Hex(UITheme.MutedText)}> / " + full.ToString("d2") + "</color></size>";
        RefreshGrenadeIndicator();
    }

    // How fast the player can move with the current gun, relative to normal
    public float MoveSpeedMultiplier()
    {
        if (loadout == null || loadout.Length == 0) return 1f;
        Gun gun = loadout[Equipped];
        return gun.moveSpeedMultiplier * (Aiming ? gun.aimMoveSpeedMultiplier : 1f);
    }
    #endregion

    #region Simulation

    // Swaps the energy cell for a full one (it refills when the swap is done)
    void StartReload()
    {
        if (ReloadTimer.IsRunning || MorphTimer.IsRunning || Energy >= maxEnergy) return;

        ReloadTimer = TickTimer.CreateFromSeconds(Runner, cellSwapTime);

        // Everyone nearby hears the swap: the player straight away, others through the host
        if (Runner.IsForward)
        {
            if (HasInputAuthority) PlayReloadSound(cellSwapTime);
            if (HasStateAuthority) RPC_ReloadSound(cellSwapTime);
        }
    }

    void FireBurstShot(Gun gun, PlayerInputData input)
    {
        Fire(input);
        BurstLeft--;
        BurstTimer = BurstLeft > 0 ? TickTimer.CreateFromSeconds(Runner, gun.fireRate) : TickTimer.None;
    }

    // Who's cooking the grenade in the hand, kept on the host for when the body is despawned mid-cook
    // (its networked identity may already be gone by then)
    PlayerRef cookOwner;
    int cookActor, cookBody;

    // The grenade button went down: a grenade comes out and its fuse starts burning. It's thrown when
    // the button is let go, so holding it "cooks" it.
    void StartCooking()
    {
        Grenades--;
        GrenadesThrown++;
        if (!GrenadeRecharge.IsRunning) GrenadeRecharge = TickTimer.CreateFromSeconds(Runner, PlasmaGrenadeRules.RechargeTime);
        BurstLeft = 0;
        CookTimer = TickTimer.CreateFromSeconds(Runner, PlasmaGrenadeRules.FreeFuse);
        // Pulling a grenade gives up spawn protection, like shooting
        player.EndSpawnProtection();

        if (HasStateAuthority)
        {
            cookOwner = player.Owner;
            cookActor = player.ActorId;
            cookBody = (int)Object.Id.Raw;
        }
    }

    // Throws the plasma grenade in the hand, in an arc along the view, with 'fuse' seconds left. The
    // host flies it and decides what it sticks to (Manager.Grenades); the thrower's own game shows it
    // straight away.
    void ThrowGrenade(PlayerInputData input, float fuse)
    {
        ThrowTimer = TickTimer.CreateFromSeconds(Runner, PlasmaGrenadeRules.ThrowTime);

        if (!Runner.IsForward || Manager.Instance == null) return;

        Quaternion view = Quaternion.Euler(-input.Pitch, input.Yaw, 0f);
        Vector3 hand = HandPosition(view);
        Vector3 velocity = PlasmaGrenadeRules.ThrowVelocity(view * Vector3.forward, player.CurrentVelocity);
        int body = (int)Object.Id.Raw;

        if (HasStateAuthority) Manager.Instance.ThrowGrenade(player.Owner, player.ActorId, body, GrenadesThrown, hand, velocity, fuse);
        else if (HasInputAuthority) Manager.Instance.PredictGrenade(body, GrenadesThrown, player.ActorId, hand, velocity, fuse);
    }

    // Held for the whole fuse: it goes off in the hand. Host only.
    void GrenadeGoesOffInHand(PlayerInputData input)
    {
        if (!Runner.IsForward || !HasStateAuthority || Manager.Instance == null) return;
        Vector3 hand = HandPosition(Quaternion.Euler(-input.Pitch, input.Yaw, 0f));
        Manager.Instance.GrenadeOffInHand(player, (int)Object.Id.Raw, GrenadesThrown, hand);
    }

    // Where a grenade leaves the left hand: a little below and in front of the eyes, or the eyes
    // themselves if that's inside a wall
    Vector3 HandPosition(Quaternion view)
    {
        Vector3 eye = player.EyePosition;
        Vector3 hand = eye + view * new Vector3(-0.25f, -0.2f, 0.45f);
        if (Physics.Linecast(eye, hand, PlasmaGrenade.WorldMask, QueryTriggerInteraction.Ignore)) hand = eye;
        return hand;
    }

    // How far the grenade in the hand has cooked, 0 (just pulled) to 1 (going off), or -1 when no
    // grenade is held. For the hand-orb holding it (DroneHands) and the HUD.
    public float CookProgress
    {
        get
        {
            if (Object == null || !Object.IsValid || !CookTimer.IsRunning) return -1f;
            float? remaining = CookTimer.RemainingTime(Runner);
            if (remaining == null) return -1f;
            return PlasmaGrenadeRules.FuseProgress(remaining.Value);
        }
    }

    // How far through a grenade throw this player is (0 to 1), or -1 when not throwing. For the
    // hand-orb's throwing swing (DroneHands).
    public float ThrowProgress
    {
        get
        {
            if (Object == null || !Object.IsValid || !ThrowTimer.IsRunning) return -1f;
            float? remaining = ThrowTimer.RemainingTime(Runner);
            if (remaining == null) return -1f;
            return Mathf.Clamp01(1f - remaining.Value / PlasmaGrenadeRules.ThrowTime);
        }
    }

    float CurrentBloom()
    {
        Gun gun = loadout[Equipped];
        // Aiming tightens the spread over the gun's adsTime, not instantly
        float bloom = ShotRules.AimedBloom(gun.bloom, gun.aimBloomDivisor, AimProgress);

        // Less accurate while moving, and much less while in the air
        if (player != null)
        {
            bloom *= Mathf.Lerp(1f, gun.movingBloomMultiplier, player.MoveSpeedRatio);
            if (!player.IsGrounded) bloom *= airborneBloomMultiplier;
        }
        return bloom;
    }

    // One shot. Runs on the host (which decides hits and damage) and is predicted on the shooter's
    // own game (ammo, cooldown, tracer and sound without waiting for the host).
    // A shotgun fires several pellets; the ones that hit the same player add up into one hit, which is
    // a cap shot when at least half of its damage came from pellets that hit the head.
    void Fire(PlayerInputData input)
    {
        Gun gun = loadout[Equipped];

        Energy = Mathf.Max(0f, Energy - gun.energyPerShot);
        RegenDelay = TickTimer.CreateFromSeconds(Runner, regenDelay);
        FireCooldown = TickTimer.CreateFromSeconds(Runner, gun.fireRate);
        ShotCount++;

        // Shooting gives up spawn protection
        player.EndSpawnProtection();

        // Re-running a tick to correct a prediction: the shot's state (cooldown, energy, count) is
        // set above, and its hits, damage and effects only ever happen the first time, so the
        // raycasts below would be wasted. The host never re-runs ticks.
        if (!Runner.IsForward) return;

        Vector3 origin = player.EyePosition;
        Quaternion view = Quaternion.Euler(-input.Pitch, input.Yaw, 0f);
        float spread = CurrentBloom();
        int pellets = Mathf.Max(1, gun.pellets);
        bool dealDamage = HasStateAuthority && Runner.IsForward;

        // Scenery is hit with normal physics; players with their hitboxes, rewound to where they were
        // on the shooter's screen. Player bodies' own colliders are left out.
        int mask = (canBeShot.value & ~((1 << Layers.LocalPlayer) | (1 << Layers.RemotePlayer))) | (1 << Layers.Hitbox);
        HitOptions options = HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority | HitOptions.SubtickAccuracy;

        pelletHits.Clear();
        // The shooter's own count for the results screen (once per shot, not on resimulation)
        if (Runner.IsForward && HasInputAuthority && Manager.Instance != null && Manager.Instance.CountsForSummary) Manager.Summary.Shot();
        // The same random spread on every machine, from the shot and pellet number. A shotgun's ring of
        // pellets is turned differently every blast.
        int shotSeed = ShotCount * 7919 + (int)Object.Id.Raw * 104729;
        float ringTurn = Unit(shotSeed + 997) * Mathf.PI * 2f;
        for (int p = 0; p < pellets; p++)
        {
            int seed = shotSeed + p * 2;
            Vector2 random = ShotRules.DiskPoint(Unit(seed), Unit(seed + 1));
            // Round spread for a single shot; the shotgun's pattern (a centre pellet and a ring, each
            // nudged a little) for pellets
            Vector2 offset = pellets > 1 ? ShotRules.PelletOffset(p, pellets, ringTurn, random * ShotRules.PelletJitter) : random;
            Vector3 bloom = view * Vector3.forward * 1000f;
            bloom += offset.y * spread * (view * Vector3.up);
            bloom += offset.x * spread * (view * Vector3.right);
            bloom.Normalize();

            Vector3 endPoint = origin + bloom * 1000f;
            Vector3 hitNormal = Vector3.zero;
            bool hitWorld = false;
            float hitDistance = 1000f;

            LagCompensatedHit hit;
            bool hitSomething = player.IsBot
                ? BotRaycast(origin, bloom, mask, out hit)
                : Runner.LagCompensation.Raycast(origin, bloom, 1000f, Object.InputAuthority, out hit, mask, options, QueryTriggerInteraction.Ignore);
            if (hitSomething)
            {
                endPoint = hit.Point;
                hitNormal = hit.Normal;
                hitDistance = hit.Distance;

                Player target = hit.Hitbox != null ? hit.Hitbox.Root.GetComponent<Player>() : null;
                if (target != null)
                {
                    // No friendly fire: teammates are hit like scenery, minus the bullet hole
                    bool friendly = Manager.Instance != null && !Manager.Instance.AreEnemies(player.ActorId, target.ActorId);
                    if (!friendly && dealDamage)
                    {
                        bool headshot = IsHead(hit.Hitbox.transform);
                        AddPelletHit(target, gun.DamageAt(hit.Distance, headshot), headshot, bloom, hit.Point);
                    }
                }
                else
                {
                    hitWorld = true;
                }
            }

            // Shots go through glass (it isn't in canBeShot), but crack or shatter the panes on the way
            if (dealDamage) OfficeGlass.Shoot(origin, bloom, hitDistance);

            if (Runner.IsForward)
            {
                // Sound, muzzle flash and gun kick once per shot; a tracer for every pellet
                bool first = p == 0;
                if (HasInputAuthority) PlayShotEffects(endPoint, hitNormal, hitWorld, first);
                if (HasStateAuthority) RPC_ShotEffects(endPoint, hitNormal, hitWorld, first);
            }
        }

        foreach (PelletHit h in pelletHits)
        {
            if (h.target == null || h.target.IsDead) continue;
            bool wasProtected = h.target.IsProtected;
            // A cap shot when the head took at least half of this shot's damage (see ShotRules.IsCapshot)
            bool capshot = ShotRules.IsCapshot(h.headDamage, h.damage);
            bool killed = h.target.TakeDamage(h.damage, player.Owner, gun.gunName, capshot, h.direction, h.point - h.target.transform.position);
            // Bots have no screen. A spawn-protected player gets a shield marker: the shot landed but
            // did nothing, so it doesn't look like a miss.
            if (player.IsBot) continue;
            if (wasProtected) RPC_ShieldHit();
            // How hard the hit landed (its share of the target's health), for the marker and sound
            else RPC_HitConfirm(capshot, killed, ShotRules.HitWeightByte(h.damage, h.target.maxHealth));
        }
        pelletHits.Clear();

        // The shooter's own view: recoil, shake and punch
        if (Runner.IsForward && HasInputAuthority && look != null) KickView(gun);
    }

    // Shots fired close together, for the machine gun's recoil pattern (the shooter's own game only)
    private int recoilStreak;
    private float lastKickTime = -10f;

    // Kicks the view up and sideways (this moves where the player aims, so later shots follow it; Look
    // settles it back at the form's own pace), and shakes and punches the camera (looks only). Each form
    // feels different: see the Feel settings on the Gun assets.
    void KickView(Gun gun)
    {
        bool aimed = isAiming;
        float scale = aimed ? aimingRecoilMultiplier : 1f;
        float up = (gun.kickUp >= 0f ? gun.kickUp : gun.recoil * cameraRecoilScale) * scale;
        float side = gun.kickSide >= 0f ? gun.kickSide * scale : up * 0.35f;

        // Shots within about two fire intervals of each other are one burst
        float now = Time.time;
        recoilStreak = now - lastKickTime <= gun.fireRate * 2f + 0.1f ? recoilStreak + 1 : 0;
        lastKickTime = now;
        float sideways = gun.patternedRecoil ? ShotRules.RecoilPattern(recoilStreak) * side : Random.Range(-side, side);
        look.AddRecoil(up, sideways, gun.kickRecovery);

        // Through a scope the view is zoomed in, so the same shake would look several times bigger
        float zoom = aimed && gun.scopeFov > 0f && GameSettings.Fov > 0f ? Mathf.Clamp01(gun.scopeFov / GameSettings.Fov) : 1f;
        look.FireKick(gun.shake * zoom, gun.fovPunch * zoom);
    }

    // A bot's shot, on the host. Bots have no input authority, so the usual "skip the shooter's own
    // hitboxes" option can't tell their body apart from other bots'. Instead this takes every hit
    // along the ray and keeps the nearest one that isn't the bot itself. No rewinding is needed:
    // bots run on the host, and see everyone where the host has them.
    bool BotRaycast(Vector3 origin, Vector3 direction, int mask, out LagCompensatedHit nearest)
    {
        nearest = default;
        botHits.Clear();
        Runner.LagCompensation.RaycastAll(origin, direction, 1000f, PlayerRef.None, botHits, mask, true, HitOptions.IncludePhysX, QueryTriggerInteraction.Ignore);

        HitboxRoot own = GetComponent<HitboxRoot>();
        bool found = false;
        foreach (LagCompensatedHit h in botHits)
        {
            if (h.Hitbox != null && h.Hitbox.Root == own) continue;
            if (found && h.Distance >= nearest.Distance) continue;
            nearest = h;
            found = true;
        }
        botHits.Clear();
        return found;
    }
    static readonly List<LagCompensatedHit> botHits = new List<LagCompensatedHit>();

    // Damage from this shot's pellets, per player hit: in all, and from the pellets that hit the head
    struct PelletHit
    {
        public Player target;
        public int damage;
        public int headDamage;
        public Vector3 direction;
        public Vector3 point;
    }
    static readonly List<PelletHit> pelletHits = new List<PelletHit>();

    static void AddPelletHit(Player target, int damage, bool headshot, Vector3 direction, Vector3 point)
    {
        for (int i = 0; i < pelletHits.Count; i++)
        {
            if (pelletHits[i].target != target) continue;
            PelletHit h = pelletHits[i];
            h.damage += damage;
            if (headshot) h.headDamage += damage;
            pelletHits[i] = h;
            return;
        }
        pelletHits.Add(new PelletHit { target = target, damage = damage, headDamage = headshot ? damage : 0, direction = direction, point = point });
    }

    // A number from 0 to 1 that only depends on the seed
    static float Unit(int seed) => (Spread(seed) + 1f) * 0.5f;

    // A number from -1 to 1 that only depends on the seed
    static float Spread(int seed)
    {
        unchecked
        {
            uint h = (uint)seed * 2654435761u;
            h ^= h >> 13;
            h *= 0x5bd1e995u;
            h ^= h >> 15;
            return h / (float)uint.MaxValue * 2f - 1f;
        }
    }

    // The head hitbox sits on (or under) the object tagged Head
    static bool IsHead(Transform hitbox)
    {
        for (Transform t = hitbox; t != null; t = t.parent)
        {
            if (t.CompareTag("Head")) return true;
            if (t.GetComponent<HitboxRoot>() != null) break;
        }
        return false;
    }

    #endregion

    #region RPCs

    // The shooter's own game hears and sees its shots straight away, so skip them here
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShotEffects(Vector3 endPoint, Vector3 hitNormal, NetworkBool hitWorld, NetworkBool first)
    {
        if (HasInputAuthority) return;
        PlayShotEffects(endPoint, hitNormal, hitWorld, first);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ReloadSound(float reloadTime)
    {
        if (HasInputAuthority) return;
        PlayReloadSound(reloadTime);
    }

    // The host confirms a hit to the shooter: hit marker and sound
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_HitConfirm(NetworkBool headshot, NetworkBool killed, byte weight)
    {
        float strength = weight / 255f;
        if (hitmarkRoutine != null) StopCoroutine(hitmarkRoutine);
        hitmarkRoutine = StartCoroutine(HitPlayer(headshot, killed, strength));
        FeedbackUI.Hit(headshot, strength);
        if (Manager.Instance != null && Manager.Instance.CountsForSummary) Manager.Summary.Hit();
        // A kill shakes the view a little, a cap shot more
        if (killed && look != null) look.Punch(headshot ? 2.2f : 1.3f);
    }

    // The host tells the shooter they hit a spawn-protected player: a small blue marker and a dull
    // knock instead of the hit sound
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ShieldHit()
    {
        if (hitmarkRoutine != null) StopCoroutine(hitmarkRoutine);
        hitmarkRoutine = StartCoroutine(ShieldMarker());
        FeedbackUI.ShieldHit();
    }

    #endregion

    #region Presentation

    IEnumerator ShieldMarker()
    {
        if (hitmark == null) yield break;
        Transform marker = hitmark.transform;
        // The spawn protection countdown's blue
        Color color = new Color(0.55f, 0.85f, 1f, 1f);
        hitmark.SetActive(true);
        const float time = 0.35f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            marker.localScale = Vector3.one * 0.7f;
            color.a = 1f - t / time;
            hitmarkImage.color = color;
            yield return null;
        }
        HideHitmark();
        hitmarkRoutine = null;
    }

    // This player's sounds, looked up once (Player adds them at runtime, so not yet in Awake)
    PlayerSounds soundsCache;
    PlayerSounds Sounds => soundsCache != null ? soundsCache : (soundsCache = GetComponent<PlayerSounds>());

    void PlayReloadSound(float reloadTime)
    {
        PlayerSounds sounds = Sounds;
        if (sounds != null) sounds.PlayReload(reloadTime);
    }

    // Hit marker colours: a hit white, a cap shot (headshot) the cap logo's gold, a kill red
    static readonly Color HitColor = Color.white;
    static readonly Color CapshotHitColor = new Color(1f, 0.8f, 0.3f, 1f);
    static readonly Color KillColor = new Color(1f, 0.22f, 0.18f, 1f);

    // Every hit pops in and fades fast, so a stream of hits reads as separate ticks. A cap shot is
    // gold and bigger; a kill pops out big and holds a moment, red, or gold and bigger still for a
    // cap shot kill.
    // strength is how hard the hit landed, 0 to 1 (its share of the target's health): a machine gun
    // bullet makes a small quick tick, a full shotgun blast or a sniper round a big one that holds
    IEnumerator HitPlayer(bool headshot, bool targetIsDead, float strength)
    {
        if (hitmark == null) yield break;

        Transform marker = hitmark.transform;
        Color color = targetIsDead && !headshot ? KillColor : headshot ? CapshotHitColor : HitColor;
        hitmarkImage.color = color;
        hitmark.SetActive(true);

        float pop, hold, fadeOut, from, to;
        if (targetIsDead)
        {
            pop = 0.15f; hold = headshot ? 0.45f : 0.3f; fadeOut = headshot ? 0.3f : 0.25f;
            from = headshot ? 2.4f : 2f; to = headshot ? 1.2f : 1f;
        }
        else
        {
            pop = 0.07f; hold = headshot ? 0.16f : 0.1f; fadeOut = 0.15f;
            from = headshot ? 1.5f : 1.25f; to = headshot ? 1.1f : 0.9f;
            float size = Mathf.Lerp(0.85f, 1.3f, strength);
            from *= size; to *= size;
            hold *= Mathf.Lerp(0.8f, 1.6f, strength);
        }

        for (float t = 0f; t < pop + hold + fadeOut; t += Time.deltaTime)
        {
            float scale = t < pop ? Mathf.Lerp(from, to, 1f - Mathf.Pow(1f - t / pop, 3f)) : to;
            marker.localScale = Vector3.one * scale;
            color.a = t < pop + hold ? 1f : 1f - (t - pop - hold) / fadeOut;
            hitmarkImage.color = color;
            yield return null;
        }

        HideHitmark();
        hitmarkRoutine = null;
    }

    // The marker lives on the shared HUD, not on this body, so it must be put away explicitly
    void HideHitmark()
    {
        if (hitmark == null) return;
        hitmark.transform.localScale = Vector3.one;
        hitmark.SetActive(false);
    }

    // A laser beam from the muzzle to where the shot landed, in the shooter's colour, that thins
    // out and fades. Each shot gets its own copy of the gun's line, so quick shots don't cut each
    // other short. Not parented to the gun, so it stays where it was fired. Beams are reused (ShotEffects).
    static MaterialPropertyBlock beamBlock;

    void ShotLine(Vector3 hitPos, Gun gun)
    {
        if (lineRend == null) return;

        // The main camera draws the Gun layer, so walls in front of the beam still hide it
        BeamFade fade = ShotEffects.Beam(lineRend, Layers.Gun);
        LineRenderer beam = fade.Line;
        beam.useWorldSpace = true;
        beam.positionCount = 2;
        beam.SetPosition(0, lineRend.transform.position);
        beam.SetPosition(1, hitPos);
        // Each form's own weight of beam (the shotgun's pellets thin and quick, so a blast doesn't fill
        // the screen; the sniper rifle's thick and lingering)
        float width = lineRend.widthMultiplier * gun.beamWidth;
        beam.widthMultiplier = width;
        beam.enabled = true;

        Color color = ShotColor();
        if (beamBlock == null) beamBlock = new MaterialPropertyBlock();
        MaterialPropertyBlock block = beamBlock;
        beam.GetPropertyBlock(block);
        block.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 0.85f)); // URP
        block.SetColor("_Color", new Color(color.r, color.g, color.b, 0.85f));     // built-in shaders
        beam.SetPropertyBlock(block);

        fade.Begin(beam, width, gun.beamTime);
    }

    // The colour this player's shots glow in: the team colour in Team Deathmatch, otherwise the player's
    // own colour (the same as their cap), lightened so it reads as light
    Color ShotColor()
    {
        Color c = PlayerCap.ColorFor(player != null ? player.ActorId : 0);
        return Color.Lerp(c, Color.white, 0.35f);
    }

    void OnEquippedChanged()
    {
        // Picking up the starting form doesn't run the morph timer, and isn't animated either
        ShowGun(Equipped, MorphTimer.IsRunning);
    }

    // Shows the equipped form. A change of form transforms the old model into the new one
    // (GunMorph) when animate is set, and otherwise just swaps it.
    void ShowGun(int ind, bool animate = false)
    {
        if (ind == shownIndex && currentWeapon != null) return;

        GameObject oldWeapon = currentWeapon;
        if (oldWeapon != null)
        {
            // Also stops the cell swap sound everyone else hears. A hit marker still showing stays:
            // changing form right after a (cap shot) kill shouldn't hide it.
            PlayerSounds sounds = Sounds;
            if (sounds != null) { sounds.StopReload(); sounds.StopCycle(); }
        }
        shownIndex = ind;
        GameObject newWeapon = Instantiate(loadout[ind].prefab, weaponParent.position, weaponParent.rotation, weaponParent) as GameObject;
        newWeapon.transform.localPosition = Vector3.zero;
        newWeapon.transform.localEulerAngles = Vector3.zero;
        newWeapon.GetComponent<Sway>().isMine = HasInputAuthority;
        // Bullets never hit guns, and a collider here would block the player's own character controller
        foreach (Collider c in newWeapon.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        // The machined body every form is built from (before the morph, which moves its parts)
        global::GunModel.Build(newWeapon, loadout[ind]);

        currentWeapon = newWeapon;
        if (oldWeapon != null)
        {
            if (animate) GunMorph.Begin(newWeapon, oldWeapon, morphTime);
            Destroy(oldWeapon);
        }
        // The gun's own line is only a template for the laser beams (see ShotLine)
        lineRend = currentWeapon.GetComponentInChildren<LineRenderer>();
        if (lineRend != null) lineRend.enabled = false;
        SetGunLayers(newWeapon);
        weaponAnchor = currentWeapon.transform.Find("Anchor");
        stateAds = currentWeapon.transform.Find("States/ADS");
        stateHip = currentWeapon.transform.Find("States/Hip");

        // Highlight the equipped gun in the HUD: bright white with an accent bar beside it, the others
        // dimmer grey (outlined, so they still show over a bright background), and its name above the ammo count
        for (int i = 0; i < arsenalImages.Count; i++)
        {
            arsenalImages[i].color = i == ind ? new Color(1f, 1f, 1f, 0.95f) : new Color(0.8f, 0.8f, 0.8f, 0.55f);
        }
        if (arsenalMarker != null) arsenalMarker.anchoredPosition = new Vector2(206f, ind * ArsenalIconStep + 50f);
        if (HasInputAuthority) HudFeedback.SetWeapon(loadout[ind].gunName);
    }

    // The local player's gun goes on the LocalGun layer, which only the viewmodel camera draws, so it
    // never clips into walls. Other players' guns keep the Gun layer.
    void SetGunLayers(GameObject gunObject)
    {
        if (!HasInputAuthority) return;

        foreach (Transform t in gunObject.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = Layers.LocalGun;
        }
    }

    // A new beam that looks like the gun's shot line template (also used by MenuBattle)
    public static LineRenderer CopyLine(LineRenderer original, GameObject target)
    {
        LineRenderer line = target.AddComponent<LineRenderer>();
        CopyLineSettings(original, line);
        return line;
    }

    // Makes line look like original
    public static void CopyLineSettings(LineRenderer original, LineRenderer line)
    {
        line.sharedMaterials = original.sharedMaterials;
        line.widthCurve = original.widthCurve;
        line.widthMultiplier = original.widthMultiplier;
        line.colorGradient = original.colorGradient;
        line.numCapVertices = original.numCapVertices;
        line.numCornerVertices = original.numCornerVertices;
        line.alignment = original.alignment;
        line.textureMode = original.textureMode;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    // A glowing dot above the ammo count in the player's colour while they carry a grenade, an empty
    // ring when they don't. The HUD outlives the body, so it's made once and reused.
    Image grenadeDot;
    Text grenadeLabel;

    void BuildGrenadeIndicator()
    {
        GameObject ammo = GameObject.Find("HUD/Ammo");
        if (ammo == null) return;

        Transform existing = ammo.transform.Find("Grenade");
        RectTransform holder;
        if (existing != null) holder = (RectTransform)existing;
        else
        {
            holder = new GameObject("Grenade", typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(ammo.transform, false);

            Image dot = UITheme.NewImage("Dot", holder, Color.white, 9f);
            UITheme.Place(dot.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
            UITheme.AddShadow(dot);

            Text label = UITheme.NewText("Label", holder, 16, UITheme.MutedText, TextAnchor.MiddleRight);
            label.rectTransform.offsetMax = new Vector2(-26f, 0f);
            label.text = "PLASMA";
            UITheme.AddShadow(label);
        }
        // Its own row above the gun form's name (UITheme.Hud puts that at 62 to 86 above the ammo
        // count) and below the gun form icons (110 above the ammo count), so the two never overlap
        UITheme.Place(holder, new Vector2(1f, 0f), new Vector2(0f, 88f), new Vector2(220f, 22f));
        grenadeDot = holder.Find("Dot")?.GetComponent<Image>();
        grenadeLabel = holder.Find("Label")?.GetComponent<Text>();
        RefreshGrenadeIndicator();
    }

    void RefreshGrenadeIndicator()
    {
        // The touch controls cover the bottom right of the screen: there the NADE button shows the
        // grenade instead (TouchControls), and the gun form icons step aside too
        bool touch = TouchControls.UsingTouch;
        if (arsenalRoot != null && arsenalRoot.activeSelf == touch) arsenalRoot.SetActive(!touch);
        if (grenadeDot == null) return;
        GameObject holder = grenadeDot.transform.parent.gameObject;
        if (holder.activeSelf == touch) holder.SetActive(!touch);
        if (touch) return;
        Color c = ShotColor();
        float cook = CookProgress;
        if (cook >= 0f)
        {
            // Cooking: the dot flashes faster and whiter as the fuse runs down, and the label counts it
            float frequency = Mathf.Lerp(3f, 14f, cook * cook);
            float flash = Mathf.Sin(Time.time * frequency * Mathf.PI * 2f) > 0f ? 1f : 0.35f;
            Color hot = Color.Lerp(c, Color.white, cook * 0.7f);
            grenadeDot.color = new Color(hot.r * flash, hot.g * flash, hot.b * flash, 1f);
            if (grenadeLabel != null)
            {
                grenadeLabel.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.3f), cook);
                grenadeLabel.text = ((1f - cook) * PlasmaGrenadeRules.FreeFuse).ToString("0.0");
            }
            return;
        }

        bool has = GrenadeCount > 0;
        // Breathes gently while ready, like the grenade itself. Recharging, it fills in faintly in the
        // player's colour and the label counts down the seconds.
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f);
        float charge = GrenadeRechargeProgress;
        grenadeDot.color = has ? new Color(c.r * pulse, c.g * pulse, c.b * pulse, 1f) : new Color(c.r, c.g, c.b, Mathf.Lerp(0.12f, 0.5f, charge));
        if (grenadeLabel != null)
        {
            grenadeLabel.color = has ? UITheme.MutedText : new Color(1f, 1f, 1f, 0.35f);
            grenadeLabel.text = has ? "PLASMA" : "PLASMA  " + Mathf.CeilToInt(GrenadeRechargeSeconds);
        }
    }

    void SetCrosshairAlpha(float alpha)
    {
        crosshairAlpha = alpha;
        if (crosshairDot != null) crosshairDot.canvasRenderer.SetAlpha(alpha);
    }

    // Brings the dot back if this body goes away mid-aim (death, leaving the match)
    private void OnDestroy()
    {
        if (crosshairDot != null) crosshairDot.canvasRenderer.SetAlpha(1f);
        // Dying mid-marker stops the coroutine before it can hide it
        HideHitmark();
    }

    void Aim(bool aiming)
    {
        if (weaponAnchor == null || stateAds == null || stateHip == null) return;

        Transform target = aiming ? stateAds : stateHip;
        weaponAnchor.position = Vector3.Lerp(weaponAnchor.position, target.position, Time.deltaTime * loadout[EquippedIndex].aimSpeed);
    }

    // Sound, laser beam, scorch mark and gun kick for a shot. first is false for a shotgun's extra
    // pellets, which only add a beam and a mark.
    void PlayShotEffects(Vector3 endPoint, Vector3 hitNormal, bool hitWorld, bool first)
    {
        Gun gun = loadout[EquippedIndex];
        ShotLine(endPoint, gun);

        // A force field ripples where it's hit instead of getting a scorch mark
        if (hitWorld && !BoundaryField.Ripple(endPoint))
        {
            ShotEffects.Mark(bulletholePrefab, endPoint, hitNormal);
            // Each form's own spray (a few per shotgun pellet, so a blast isn't a fountain)
            ShotEffects.Sparks(endPoint, hitNormal, ShotColor(), gun.impactSparks);
        }
        // A shot that ended on a player (a hit has a surface normal; a miss doesn't): a few sparks off
        // the drone where the beam ends. Only a look: the host's hit marker says whether it counted.
        else if (!hitWorld && hitNormal != Vector3.zero)
        {
            ShotEffects.Sparks(endPoint, hitNormal, ShotColor(), Mathf.Max(1, gun.impactSparks / 2));
        }

        if (!first) return;

        PlayerSounds sounds = Sounds;
        if (sounds != null) sounds.PlayShot(gun);
        MuzzleFlash(gun);
        LastShotTime = Time.time;
        if (HasInputAuthority) HudFeedback.Fired(gun.crosshairKick);

        //gun fx
        if (currentWeapon != null)
        {
            currentWeapon.transform.Rotate(-loadout[EquippedIndex].recoil, 0, 0);
            currentWeapon.transform.position -= currentWeapon.transform.forward * loadout[EquippedIndex].kickback;
        }
    }

    // The muzzle flash: one light and one glowing core per player, made on the first shot and reused,
    // faded out over the form's flashTime in Update (UpdateMuzzleFlash). It used to be a new light
    // destroyed after 0.04 s, which at a low frame rate could vanish before it was ever drawn.
    Light flashLight;
    Transform flashCore;
    Renderer flashCoreRenderer;
    MaterialPropertyBlock flashBlock;
    float flashStart = -1f, flashDuration, flashPeak, flashCoreSize;
    int flashFrame;
    Color flashColor;

    Vector3 MuzzlePosition => lineRend != null ? lineRend.transform.position : currentWeapon.transform.position + currentWeapon.transform.forward * 0.5f;

    void MuzzleFlash(Gun gun)
    {
        if (currentWeapon == null) return;

        if (flashLight == null)
        {
            // Under this body, so it goes with it
            GameObject flash = new GameObject("Muzzle Flash");
            flash.transform.SetParent(transform, false);
            flashLight = flash.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.shadows = LightShadows.None;

            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Muzzle Flash Core";
            Destroy(core.GetComponent<Collider>());
            core.transform.SetParent(transform, false);
            flashCoreRenderer = core.GetComponent<Renderer>();
            flashCoreRenderer.sharedMaterial = DroneBody.GlowMaterial;
            flashCoreRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flashCoreRenderer.receiveShadows = false;
            flashCore = core.transform;
            flashBlock = new MaterialPropertyBlock();
        }

        flashColor = ShotColor();
        flashPeak = gun.flashIntensity * Random.Range(0.85f, 1.15f);
        flashDuration = Mathf.Max(0.01f, gun.flashTime);
        flashStart = Time.time;
        flashFrame = Time.frameCount;
        flashLight.color = flashColor;
        flashLight.range = gun.flashRange;
        flashLight.enabled = true;

        // No core through the sniper scope: the gun model is enlarged to bring the scope to the eye, so
        // the core would sit right in front of it
        bool scoped = HasInputAuthority && isAiming && gun.scopeViewmodelFov > 0f;
        flashCoreSize = scoped ? 0f : gun.flashSize;
        bool core = flashCoreSize > 0f;
        flashCore.gameObject.SetActive(core);
        // Drawn with the gun: the viewmodel camera's layer for your own, so walls never cut it off
        if (core) flashCore.gameObject.layer = currentWeapon.layer;
        UpdateMuzzleFlash();
    }

    // Fades the muzzle flash, following the muzzle. Always shown for at least the frame it fired in.
    void UpdateMuzzleFlash()
    {
        if (flashLight == null || !flashLight.enabled) return;
        float t = (Time.time - flashStart) / flashDuration;
        if (currentWeapon == null || (t >= 1f && Time.frameCount > flashFrame))
        {
            flashLight.enabled = false;
            flashCore.gameObject.SetActive(false);
            return;
        }

        float left = 1f - Mathf.Clamp01(t);
        left *= left;
        Vector3 muzzle = MuzzlePosition;
        flashLight.transform.position = muzzle;
        flashLight.intensity = flashPeak * left;

        if (flashCoreSize > 0f)
        {
            // A bolt of plasma along the barrel, swelling a little as it burns out
            Vector3 forward = currentWeapon.transform.forward;
            float size = flashCoreSize * (1f + 0.5f * (1f - left));
            flashCore.SetPositionAndRotation(muzzle + forward * size * 0.4f, Quaternion.LookRotation(forward));
            flashCore.localScale = new Vector3(size, size, size * 1.8f) / Mathf.Max(0.0001f, transform.lossyScale.x);
            Color glow = flashColor * (3f + flashPeak * 0.4f) * left;
            flashCoreRenderer.GetPropertyBlock(flashBlock);
            flashBlock.SetColor("_BaseColor", new Color(flashColor.r, flashColor.g, flashColor.b, 1f));
            flashBlock.SetColor("_Color", new Color(flashColor.r, flashColor.g, flashColor.b, 1f));
            flashBlock.SetColor("_EmissionColor", glow);
            flashCoreRenderer.SetPropertyBlock(flashBlock);
        }
    }
    #endregion
}
