using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Fusion;

// A player's guns. Firing, ammo, reloading and switching run in FixedUpdateNetwork from the
// player's input: the host decides what is hit (with lag compensation, so shots are checked against
// where targets were on the shooter's screen) and the shooter's own game predicts the rest, so
// shooting feels instant. Tracers, sounds and the gun model are local presentation.
public class Weapon : NetworkBehaviour
{
    #region Variables
    public Gun[] loadout;
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
    public Color muzzleFlashColor = new Color(1f, 0.8f, 0.45f);

    [Header("Accuracy")]
    // Spread multiplier while jumping or falling, for every gun
    public float airborneBloomMultiplier = 2f;

    [Header("Ammo pickups")]
    // Each pickup adds this many clips of reserve ammo to every gun
    public int clipsPerPickup = 2;

    private const int MaxGuns = 4;

    private GameObject currentWeapon;
    private int shownIndex = -1;

    // Looked up once instead of every shot or frame
    private Player player;
    private Look look;
    private Image hitmarkImage;
    private readonly List<Image> arsenalImages = new List<Image>();
    private Transform weaponAnchor, stateAds, stateHip;
    private Coroutine hitmarkRoutine;

    public int EquippedIndex => Object != null && Object.IsValid ? Equipped : 0;
    public Gun CurrentGun => loadout != null && EquippedIndex >= 0 && EquippedIndex < loadout.Length ? loadout[EquippedIndex] : null;
    public bool isAiming => Object != null && Object.IsValid && Aiming;
    #endregion

    #region Networked state

    [Networked, OnChangedRender(nameof(OnEquippedChanged))] private int Equipped { get; set; }
    [Networked, Capacity(MaxGuns)] private NetworkArray<int> Clips => default;
    [Networked, Capacity(MaxGuns)] private NetworkArray<int> Stashes => default;
    [Networked] private TickTimer FireCooldown { get; set; }
    [Networked] private TickTimer ReloadTimer { get; set; }
    [Networked] private TickTimer BurstTimer { get; set; }
    [Networked] private int BurstLeft { get; set; }
    [Networked] private NetworkBool Aiming { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    // Counts every shot, so each one gets its own (but on every machine the same) random spread
    [Networked] private int ShotCount { get; set; }

    #endregion

    #region Fusion Callbacks

    void Awake()
    {
        player = GetComponent<Player>();
        look = GetComponent<Look>();
        if (loadout.Length > MaxGuns) Debug.LogError($"Weapon supports at most {MaxGuns} guns");
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            for (int i = 0; i < loadout.Length && i < MaxGuns; i++)
            {
                Clips.Set(i, loadout[i].clipSize);
                Stashes.Set(i, loadout[i].ammo);
            }
            Equipped = 0;
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

            int ind = 0;
            foreach (Gun gun in loadout)
            {
                Vector3 position = arsenal.transform.position + Vector3.up * (ind * 120f);
                GameObject weaponimageObject = Instantiate(weaponImagePrefab, position, arsenal.transform.rotation, arsenal.transform);
                weaponimageObject.name = "WeaponImage" + ind.ToString();

                // Set the icon on the copy, not on the prefab asset
                Image image = weaponimageObject.GetComponent<Image>();
                Rect rect = new Rect(0, 0, gun.image.width, gun.image.height);
                image.sprite = Sprite.Create(gun.image, rect, new Vector2(0.5f, 0.5f), 100);
                arsenalImages.Add(image);
                ind += 1;
            }

            hitmark = GameObject.Find("HUD/Crosshair/Hitmark");
            if (hitmark != null) hitmarkImage = hitmark.GetComponent<Image>();
        }

        ShowGun(Equipped);
    }

    public override void FixedUpdateNetwork()
    {
        if (player.IsDead) return;
        if (!GetInput(out PlayerInputData input)) return;

        NetworkButtons pressed = input.Buttons.GetPressed(PreviousButtons);
        PreviousButtons = input.Buttons;

        Aiming = input.Buttons.IsSet(InputButton.Aim);

        // Switching guns cancels a reload or burst in progress
        int slot = input.WeaponSlot - 1;
        if (slot >= 0 && slot < loadout.Length && slot != Equipped)
        {
            Equipped = slot;
            ReloadTimer = TickTimer.None;
            BurstLeft = 0;
        }

        Gun gun = loadout[Equipped];

        if (ReloadTimer.Expired(Runner))
        {
            ReloadTimer = TickTimer.None;
            FinishReload(Equipped);
        }

        if (BurstLeft > 0)
        {
            // The rest of a burst fires on its own, one shot every fireRate seconds
            if (BurstTimer.ExpiredOrNotRunning(Runner))
            {
                if (Clips[Equipped] > 0) FireBurstShot(gun, input);
                else BurstLeft = 0;
            }
        }
        else if (!ReloadTimer.IsRunning)
        {
            bool fireInput = gun.burst == 1 ? input.Buttons.IsSet(InputButton.Fire) : pressed.IsSet(InputButton.Fire);

            if (fireInput && FireCooldown.ExpiredOrNotRunning(Runner))
            {
                if (Clips[Equipped] <= 0)
                {
                    StartReload();
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

    // Presentation only: gun model position, aiming and the reload pose
    void Update()
    {
        if (Object == null || !Object.IsValid || currentWeapon == null) return;

        // The gun is put away while reloading
        bool reloading = ReloadTimer.IsRunning;
        if (currentWeapon.activeSelf == reloading) currentWeapon.SetActive(!reloading);

        if (HasInputAuthority && !Pause.paused) Aim(isAiming);

        //weapon position elasticity
        currentWeapon.transform.localPosition = Vector3.Lerp(currentWeapon.transform.localPosition, Vector3.zero, Time.deltaTime * 4f);
    }
    #endregion

    #region Public Methods

    public bool NeedsAmmo()
    {
        for (int i = 0; i < loadout.Length; i++)
        {
            if (Stashes[i] < loadout[i].ammo) return true;
        }
        return false;
    }

    // Host only: an ammo box was picked up
    public void AddAmmoPickup()
    {
        for (int i = 0; i < loadout.Length; i++)
        {
            Stashes.Set(i, Mathf.Min(loadout[i].ammo, Stashes[i] + loadout[i].clipSize * clipsPerPickup));
        }

        // Reload straight away if the current gun is empty
        if (Clips[Equipped] == 0 && BurstLeft == 0) StartReload();
    }

    public void RefreshAmmo(Text text)
    {
        if (text == null || Object == null || !Object.IsValid) return;
        // Clip count large, the spare ammo smaller and muted
        text.supportRichText = true;
        text.text = Clips[Equipped].ToString("d2") + $"<size={Mathf.RoundToInt(text.fontSize * 0.55f)}><color={UITheme.Hex(UITheme.MutedText)}> / " + Stashes[Equipped].ToString("d2") + "</color></size>";
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

    void StartReload()
    {
        Gun gun = loadout[Equipped];
        if (ReloadTimer.IsRunning || Clips[Equipped] >= gun.clipSize || Stashes[Equipped] <= 0) return;

        ReloadTimer = TickTimer.CreateFromSeconds(Runner, gun.reloadTime);

        // Everyone nearby hears the reload: the reloading player straight away, others through the host
        if (Runner.IsForward)
        {
            if (HasInputAuthority) PlayReloadSound(gun.reloadTime);
            if (HasStateAuthority) RPC_ReloadSound(gun.reloadTime);
        }
    }

    void FinishReload(int index)
    {
        Gun gun = loadout[index];
        int stash = Stashes[index] + Clips[index];
        int clip = Mathf.Min(gun.clipSize, stash);
        Clips.Set(index, clip);
        Stashes.Set(index, stash - clip);
    }

    void FireBurstShot(Gun gun, PlayerInputData input)
    {
        Fire(input);
        BurstLeft--;
        BurstTimer = BurstLeft > 0 ? TickTimer.CreateFromSeconds(Runner, gun.fireRate) : TickTimer.None;
    }

    float CurrentBloom()
    {
        Gun gun = loadout[Equipped];
        float bloom = gun.bloom;
        if (Aiming) bloom /= Mathf.Max(1f, gun.aimBloomDivisor);

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
    void Fire(PlayerInputData input)
    {
        Gun gun = loadout[Equipped];

        Clips.Set(Equipped, Clips[Equipped] - 1);
        FireCooldown = TickTimer.CreateFromSeconds(Runner, gun.fireRate);
        ShotCount++;

        // Shooting gives up spawn protection
        player.EndSpawnProtection();

        //bloom: the same random spread on every machine, from the shot number
        Vector3 origin = player.EyePosition;
        Quaternion view = Quaternion.Euler(-input.Pitch, input.Yaw, 0f);
        float spread = CurrentBloom();
        int seed = ShotCount * 7919 + (int)Object.Id.Raw * 104729;
        Vector3 bloom = view * Vector3.forward * 1000f;
        bloom += Spread(seed) * spread * (view * Vector3.up);
        bloom += Spread(seed + 1) * spread * (view * Vector3.right);
        bloom.Normalize();

        Vector3 endPoint = origin + bloom * 1000f;
        Vector3 hitNormal = Vector3.zero;
        bool hitWorld = false;

        // Scenery is hit with normal physics; players with their hitboxes, rewound to where they were
        // on the shooter's screen. Player bodies' own colliders are left out.
        int mask = (canBeShot.value & ~((1 << Layers.LocalPlayer) | (1 << Layers.RemotePlayer))) | (1 << Layers.Hitbox);
        HitOptions options = HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority | HitOptions.SubtickAccuracy;

        if (Runner.LagCompensation.Raycast(origin, bloom, 1000f, Object.InputAuthority, out LagCompensatedHit hit, mask, options, QueryTriggerInteraction.Ignore))
        {
            endPoint = hit.Point;
            hitNormal = hit.Normal;

            Player target = hit.Hitbox != null ? hit.Hitbox.Root.GetComponent<Player>() : null;
            if (target != null)
            {
                // No friendly fire: teammates are hit like scenery, minus the bullet hole
                bool friendly = Manager.Instance != null && !Manager.Instance.AreEnemies(player.ActorId, target.ActorId);
                if (!friendly && HasStateAuthority && Runner.IsForward)
                {
                    bool headshot = IsHead(hit.Hitbox.transform);
                    int damage = gun.DamageAt(hit.Distance, headshot);
                    bool wasProtected = target.IsProtected;
                    bool killed = target.TakeDamage(damage, Object.InputAuthority, gun.gunName, headshot, bloom, hit.Point - target.transform.position);
                    // No hit feedback on spawn-protected players
                    if (!wasProtected) RPC_HitConfirm(headshot, killed);
                }
            }
            else
            {
                hitWorld = true;
            }
        }

        if (Runner.IsForward)
        {
            if (HasInputAuthority)
            {
                PlayShotEffects(endPoint, hitNormal, hitWorld);

                // Kick the view up and slightly sideways. Look recovers it over the next moments.
                if (look != null)
                {
                    float kick = gun.recoil * cameraRecoilScale * (Aiming ? aimingRecoilMultiplier : 1f);
                    look.AddRecoil(kick, Random.Range(-kick, kick) * 0.35f);
                }
            }
            if (HasStateAuthority) RPC_ShotEffects(endPoint, hitNormal, hitWorld);
        }
    }

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
    private void RPC_ShotEffects(Vector3 endPoint, Vector3 hitNormal, NetworkBool hitWorld)
    {
        if (HasInputAuthority) return;
        PlayShotEffects(endPoint, hitNormal, hitWorld);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ReloadSound(float reloadTime)
    {
        if (HasInputAuthority) return;
        PlayReloadSound(reloadTime);
    }

    // The host confirms a hit to the shooter: hit marker and sound
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_HitConfirm(NetworkBool headshot, NetworkBool killed)
    {
        if (hitmarkRoutine != null) StopCoroutine(hitmarkRoutine);
        hitmarkRoutine = StartCoroutine(HitPlayer(killed));
        FeedbackUI.Hit(headshot);
    }

    #endregion

    #region Presentation

    void PlayReloadSound(float reloadTime)
    {
        PlayerSounds sounds = GetComponent<PlayerSounds>();
        if (sounds != null) sounds.PlayReload(reloadTime);
    }

    IEnumerator HitPlayer(bool targetIsDead)
    {
        if (hitmark == null) yield break;

        hitmarkImage.color = targetIsDead ? Color.red : Color.white;
        hitmark.SetActive(true);
        yield return new WaitForSeconds(0.5f);
        hitmark.SetActive(false);
        hitmarkRoutine = null;
    }

    IEnumerator ShotLine(Vector3 hitPos)
    {
        if (lineRend != null) {
        lineRend.enabled = true;
        lineRend.SetPosition(0, hitPos);
        lineRend.SetPosition(1, lineRend.gameObject.transform.position);
        yield return new WaitForSeconds(0.5f);
        if (lineRend != null) lineRend.enabled = false;
        }
    }

    void OnEquippedChanged()
    {
        ShowGun(Equipped);
    }

    // Swaps the gun model to the equipped gun
    void ShowGun(int ind)
    {
        if (ind == shownIndex && currentWeapon != null) return;

        if (currentWeapon != null)
        {
            // Also stops the reload sound everyone else hears
            PlayerSounds sounds = GetComponent<PlayerSounds>();
            if (sounds != null) sounds.StopReload();
            StopAllCoroutines();
            hitmarkRoutine = null;
            if (hitmark != null && HasInputAuthority) hitmark.SetActive(false);
            Destroy(currentWeapon);
        }
        shownIndex = ind;
        GameObject newWeapon = Instantiate(loadout[ind].prefab, weaponParent.position, weaponParent.rotation, weaponParent) as GameObject;
        newWeapon.transform.localPosition = Vector3.zero;
        newWeapon.transform.localEulerAngles = Vector3.zero;
        newWeapon.GetComponent<Sway>().isMine = HasInputAuthority;
        // Bullets never hit guns, and a collider here would block the player's own character controller
        foreach (Collider c in newWeapon.GetComponentsInChildren<Collider>(true)) c.enabled = false;

        currentWeapon = newWeapon;
        lineRend = currentWeapon.GetComponentInChildren<LineRenderer>();
        SetGunLayers(newWeapon);
        weaponAnchor = currentWeapon.transform.Find("Anchor");
        stateAds = currentWeapon.transform.Find("States/ADS");
        stateHip = currentWeapon.transform.Find("States/Hip");

        // Highlight the equipped gun in the HUD
        for (int i = 0; i < arsenalImages.Count; i++)
        {
            Color c = arsenalImages[i].color;
            c.a = i == ind ? 0.75f : 0.30f;
            arsenalImages[i].color = c;
        }
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

        // The tracer's LineRenderer sits on a visible part of the gun model (the barrel). Move it to
        // its own child on the Gun layer, so the main camera draws it and walls still hide it,
        // while the barrel itself stays with the rest of the gun on the viewmodel camera.
        if (lineRend != null) lineRend = MoveTracerToOwnObject(lineRend);
    }

    LineRenderer MoveTracerToOwnObject(LineRenderer original)
    {
        GameObject tracerObject = new GameObject("Tracer");
        tracerObject.layer = Layers.Gun;
        tracerObject.transform.SetParent(original.transform, false);

        LineRenderer tracer = tracerObject.AddComponent<LineRenderer>();
        tracer.sharedMaterials = original.sharedMaterials;
        tracer.useWorldSpace = original.useWorldSpace;
        tracer.positionCount = original.positionCount;
        tracer.widthCurve = original.widthCurve;
        tracer.widthMultiplier = original.widthMultiplier;
        tracer.colorGradient = original.colorGradient;
        tracer.numCapVertices = original.numCapVertices;
        tracer.numCornerVertices = original.numCornerVertices;
        tracer.alignment = original.alignment;
        tracer.textureMode = original.textureMode;
        tracer.shadowCastingMode = original.shadowCastingMode;
        tracer.receiveShadows = original.receiveShadows;
        tracer.enabled = false;

        original.enabled = false;
        return tracer;
    }

    void Aim(bool aiming)
    {
        if (weaponAnchor == null || stateAds == null || stateHip == null) return;

        Transform target = aiming ? stateAds : stateHip;
        weaponAnchor.position = Vector3.Lerp(weaponAnchor.position, target.position, Time.deltaTime * loadout[EquippedIndex].aimSpeed);
    }

    // Sound, tracer, bullet hole and gun kick for a shot
    void PlayShotEffects(Vector3 endPoint, Vector3 hitNormal, bool hitWorld)
    {
        PlayerSounds sounds = GetComponent<PlayerSounds>();
        if (sounds != null) sounds.PlayShot(loadout[EquippedIndex]);

        StartCoroutine(ShotLine(endPoint));
        MuzzleFlash();

        if (hitWorld)
        {
            GameObject newHole = Instantiate(bulletholePrefab, endPoint + hitNormal * 0.001f, Quaternion.identity) as GameObject;
            newHole.transform.LookAt(endPoint + hitNormal);
            Destroy(newHole, 5);
        }

        //gun fx
        if (currentWeapon != null)
        {
            currentWeapon.transform.Rotate(-loadout[EquippedIndex].recoil, 0, 0);
            currentWeapon.transform.position -= currentWeapon.transform.forward * loadout[EquippedIndex].kickback;
        }
    }

    void MuzzleFlash()
    {
        if (currentWeapon == null) return;

        Vector3 muzzle = lineRend != null ? lineRend.transform.position : currentWeapon.transform.position + currentWeapon.transform.forward * 0.5f;

        GameObject flash = new GameObject("Muzzle Flash");
        flash.transform.position = muzzle;
        Light light = flash.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = muzzleFlashColor;
        light.range = 6f;
        light.intensity = Random.Range(2.5f, 4f);
        light.shadows = LightShadows.None;

        Destroy(flash, 0.04f);
    }
    #endregion
}
