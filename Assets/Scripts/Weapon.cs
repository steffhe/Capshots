using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.UI;

public class Weapon : MonoBehaviourPunCallbacks
{
    #region Variables
    public Gun[] loadout;
    public Transform weaponParent;
    public GameObject bulletholePrefab;
    public GameObject hitmark;
    public LayerMask canBeShot;
    public bool isAiming = false;
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


    private float currentCooldown;
    public int currentIndex;
    private GameObject currentWeapon;
    private bool isReloading;
    private Coroutine reloadRoutine;
    private bool isBursting;
    private Coroutine burstRoutine;

    // Looked up once instead of every shot or frame
    private Player player;
    private Look look;
    private Transform playerCamera;
    private Image hitmarkImage;
    private readonly List<Image> arsenalImages = new List<Image>();
    private Transform weaponAnchor, stateAds, stateHip;

    public Gun CurrentGun => loadout != null && currentIndex >= 0 && currentIndex < loadout.Length ? loadout[currentIndex] : null;
    #endregion

    #region MonoBehaviour Callbacks
    void Awake()
    {
        // Work on per-player copies so ammo state never modifies the shared Gun assets
        for (int i = 0; i < loadout.Length; i++)
        {
            loadout[i] = Instantiate(loadout[i]);
        }

        player = GetComponent<Player>();
        look = GetComponent<Look>();
        playerCamera = transform.Find("Cameras/Normal Camera");
    }

    // Start is called before the first frame update
    void Start()
    {
        if (photonView.IsMine)
        {
            GameObject arsenal = GameObject.Find("HUD/Arsenal");
            foreach (Transform child in arsenal.transform)
            {
                GameObject.Destroy(child.gameObject);
            }

            int ind = 0;
            foreach (Gun gun in loadout)
            {
                gun.Initialize();

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
        photonView.RPC("Equip", RpcTarget.All, 0);

    }

    // Update is called once per frame
    void Update()
    {
        if (Pause.paused && photonView.IsMine) return;

        int weaponSwitch = photonView.IsMine ? GameInput.WeaponSwitch : 0;
        if (weaponSwitch > 0) // forward
        {
            currentIndex = Mathf.Min(currentIndex + 1, loadout.Length - 1);
            photonView.RPC("Equip", RpcTarget.All, currentIndex);
        }
        else if (weaponSwitch < 0) // backwards
        {
            currentIndex = Mathf.Max(currentIndex - 1, 0);
            photonView.RPC("Equip", RpcTarget.All, currentIndex);
        }

        if (photonView.IsMine && GameInput.Pressed(GameInput.Weapon1))
        {
            photonView.RPC("Equip", RpcTarget.All, 0);
        }
        if (photonView.IsMine && GameInput.Pressed(GameInput.Weapon2))
        {
            photonView.RPC("Equip", RpcTarget.All, 1);
        }
        if (photonView.IsMine && GameInput.Pressed(GameInput.Weapon3))
        {
            photonView.RPC("Equip", RpcTarget.All, 2);
        }

        if (currentWeapon != null)
        {
           if (photonView.IsMine)
            {

         Aim(GameInput.Held(GameInput.Aim));
                bool fireInput = loadout[currentIndex].burst == 1 ? GameInput.Held(GameInput.Fire) : GameInput.Pressed(GameInput.Fire);

                if (!isReloading && !isBursting)
                {
                    if (fireInput && currentCooldown <= 0)
                    {
                        if (loadout[currentIndex].burst == 2)
                        {
                            burstRoutine = StartCoroutine(BurstFire(loadout[currentIndex]));
                        }
                        else if (loadout[currentIndex].FireBullet()) { Fire(); }
                        else
                        {
                            StartReload();
                        }
                    }

                    if (GameInput.Pressed(GameInput.Reload))
                    {
                        StartReload();
                    }
                }

                //cooldown
                if (currentCooldown > 0) currentCooldown -= Time.deltaTime;
            }
        //weapon position elasticity
        currentWeapon.transform.localPosition = Vector3.Lerp(currentWeapon.transform.localPosition, Vector3.zero,Time.deltaTime * 4f);
        }


    }
    #endregion

    #region Public Methods

    public bool NeedsAmmo()
    {
        foreach (Gun gun in loadout)
        {
            if (gun.NeedsAmmo()) return true;
        }
        return false;
    }

    public void AddAmmoPickup()
    {
        foreach (Gun gun in loadout)
        {
            gun.AddAmmo(gun.clipSize * clipsPerPickup);
        }

        // Reload straight away if the current gun is empty
        if (loadout[currentIndex].GetClip() == 0 && !isBursting) StartReload();
    }

    public void RefreshAmmo(Text text)
    {
        int clip = loadout[currentIndex].GetClip();
        int stash = loadout[currentIndex].GetStash();

        text.text = clip.ToString("d2") + " / " + stash.ToString("d2");
    }
    #endregion

    #region Private Methods

    void StartReload()
    {
        if (isReloading || !loadout[currentIndex].CanReload()) return;
        reloadRoutine = StartCoroutine(Reload(loadout[currentIndex]));
        photonView.RPC("ReloadSound", RpcTarget.All, loadout[currentIndex].reloadTime);
    }

    // Runs on every client so everyone nearby hears the reload
    [PunRPC]
    void ReloadSound(float reloadTime)
    {
        PlayerSounds sounds = GetComponent<PlayerSounds>();
        if (sounds != null) sounds.PlayReload(reloadTime);
    }

    IEnumerator BurstFire(Gun gun)
    {
        isBursting = true;
        int shots = Mathf.Max(1, gun.burstCount);

        for (int i = 0; i < shots; i++)
        {
            if (!gun.FireBullet())
            {
                // Out of ammo: reload only if the trigger pull didn't fire anything
                if (i == 0)
                {
                    isBursting = false;
                    StartReload();
                }
                break;
            }

            Fire();
            if (i < shots - 1) yield return new WaitForSeconds(gun.fireRate);
        }

        isBursting = false;
        burstRoutine = null;
    }

    void CancelBurst()
    {
        if (burstRoutine != null) StopCoroutine(burstRoutine);
        burstRoutine = null;
        isBursting = false;
    }

    void CancelReload()
    {
        if (reloadRoutine != null) StopCoroutine(reloadRoutine);
        reloadRoutine = null;
        isReloading = false;
    }

    IEnumerator Reload(Gun gun)
    {
        isReloading = true;
        currentWeapon.SetActive(false);
        yield return new WaitForSeconds(gun.reloadTime);
        gun.Reload();
        currentWeapon.SetActive(true);
        isReloading = false;
        reloadRoutine = null;
    }

    IEnumerator HitPlayer(bool targetIsDead)
    {

        if (hitmark == null) yield break;

        hitmarkImage.color = targetIsDead ? Color.red : Color.white;
        hitmark.SetActive(true);
        yield return new WaitForSeconds(0.5f);
        hitmark.SetActive(false);
    }

    IEnumerator ShotLine(Vector3 hitPos)
    {
        if (lineRend != null) {
        lineRend.enabled = true;
        lineRend.SetPosition(0, hitPos);
        lineRend.SetPosition(1, lineRend.gameObject.transform.position);
        yield return new WaitForSeconds(0.5f);
        lineRend.enabled = false;
        }
    }

    [PunRPC]
    void Equip(int ind)
    {
        if (currentWeapon != null)
        {
            CancelReload();
            CancelBurst();
            // Equip runs on every client, so this also stops the reload sound everyone else hears
            PlayerSounds sounds = GetComponent<PlayerSounds>();
            if (sounds != null) sounds.StopReload();
            Destroy(currentWeapon);
        }
        currentIndex = ind;
        GameObject newWeapon = Instantiate(loadout[ind].prefab, weaponParent.position, weaponParent.rotation, weaponParent) as GameObject;
        newWeapon.transform.localPosition = Vector3.zero;
        newWeapon.transform.localEulerAngles = Vector3.zero;
        newWeapon.GetComponent<Sway>().isMine = photonView.IsMine;

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
            c.a = i == currentIndex ? 0.75f : 0.30f;
            arsenalImages[i].color = c;
        }
    }
  
    // The local player's gun goes on the LocalGun layer, which only the viewmodel camera draws, so it
    // never clips into walls. Other players' guns keep the Gun layer.
    void SetGunLayers(GameObject gunObject)
    {
        if (!photonView.IsMine) return;

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

    float CurrentBloom()
    {
        Gun gun = loadout[currentIndex];
        float bloom = gun.bloom;
        if (isAiming) bloom /= Mathf.Max(1f, gun.aimBloomDivisor);

        // Less accurate while moving, and much less while in the air
        if (player != null)
        {
            bloom *= Mathf.Lerp(1f, gun.movingBloomMultiplier, player.MoveSpeedRatio);
            if (!player.IsGrounded) bloom *= airborneBloomMultiplier;
        }
        return bloom;
    }

    // How fast the player can move with the current gun, relative to normal
    public float MoveSpeedMultiplier()
    {
        if (loadout == null || loadout.Length == 0) return 1f;
        Gun gun = loadout[currentIndex];
        return gun.moveSpeedMultiplier * (isAiming ? gun.aimMoveSpeedMultiplier : 1f);
    }

    void Aim(bool isAiming)
    {
        this.isAiming = isAiming;
        if (weaponAnchor == null || stateAds == null || stateHip == null) return;

        Transform target = isAiming ? stateAds : stateHip;
        weaponAnchor.position = Vector3.Lerp(weaponAnchor.position, target.position, Time.deltaTime * loadout[currentIndex].aimSpeed);

    }
    // Runs on the shooter only. The shooter does the raycast and applies damage, then tells
    // everyone where the shot landed so all clients draw the same tracer and bullet hole.
    void Fire()
    {
        Gun gun = loadout[currentIndex];
        Transform spawn = playerCamera;

        // Shooting gives up spawn protection
        player.EndSpawnProtection();

        //bloom
        float spread = CurrentBloom();
        Vector3 bloom = spawn.position + spawn.forward * 1000f;
        bloom += Random.Range(-spread, spread) * spawn.up;
        bloom += Random.Range(-spread, spread) * spawn.right;
        bloom -= spawn.position;
        bloom.Normalize();

        //cooldown
        currentCooldown = gun.fireRate;

        Vector3 endPoint = spawn.position + bloom * 1000f;
        Vector3 hitNormal = Vector3.zero;
        bool hitWorld = false;

        //raycast
        RaycastHit hit;
        if (Physics.Raycast(spawn.position, bloom, out hit, 1000f, canBeShot))
        {
            endPoint = hit.point;
            hitNormal = hit.normal;

            PhotonView target = hit.collider.gameObject.layer == Layers.RemotePlayer ? hit.collider.transform.root.gameObject.GetPhotonView() : null;
            // No friendly fire: teammates are hit like scenery, minus the bullet hole
            bool friendly = target != null && Manager.Instance != null && !Manager.Instance.AreEnemies(photonView.OwnerActorNr, target.OwnerActorNr);

            if (target != null && target.Owner != null && !friendly)
            {
                bool headshot = hit.collider.gameObject.CompareTag("Head");
                int damage = gun.DamageAt(hit.distance, headshot);

                Player targetPlayer = target.GetComponent<Player>();
                // No hit feedback on spawn-protected players. The damage is still sent: the target decides.
                if (!targetPlayer.IsProtected)
                {
                    StopCoroutine("HitPlayer");
                    bool targetIsDead = targetPlayer.currentHealth - damage <= 0;
                    StartCoroutine("HitPlayer", targetIsDead);
                    FeedbackUI.Hit(headshot);
                }

                // Send which gun hit and whether it was a headshot, not a damage number: the victim works out the damage itself
                target.RPC("TakeDamage", target.Owner, currentIndex, headshot, hit.distance);
            }

            hitWorld = hit.collider.gameObject.layer != Layers.RemotePlayer;
        }

        photonView.RPC("ShotEffects", RpcTarget.All, endPoint, hitNormal, hitWorld);

        // Kick the view up and slightly sideways. Look recovers it over the next moments.
        if (look != null)
        {
            float kick = gun.recoil * cameraRecoilScale * (isAiming ? aimingRecoilMultiplier : 1f);
            look.AddRecoil(kick, Random.Range(-kick, kick) * 0.35f);
        }
    }

    // Runs on every client: sound, tracer, bullet hole and gun kick for a shot.
    [PunRPC]
    void ShotEffects(Vector3 endPoint, Vector3 hitNormal, bool hitWorld)
    {
        PlayerSounds sounds = GetComponent<PlayerSounds>();
        if (sounds != null) sounds.PlayShot(loadout[currentIndex]);

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
            currentWeapon.transform.Rotate(-loadout[currentIndex].recoil, 0, 0);
            currentWeapon.transform.position -= currentWeapon.transform.forward * loadout[currentIndex].kickback;
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

    [PunRPC]
    private void TakeDamage(int gunIndex, bool headshot, float distance, PhotonMessageInfo info)
    {
        if (gunIndex < 0 || gunIndex >= loadout.Length || info.Sender == null) return;
        // The victim checks teams too, so a modified client can't damage teammates
        if (Manager.Instance != null && !Manager.Instance.AreEnemies(info.Sender.ActorNumber, photonView.OwnerActorNr)) return;

        // Damage comes from this game's own gun data. A wrong distance can only lower it, never raise it above full damage.
        int damage = loadout[gunIndex].DamageAt(Mathf.Max(0f, distance), headshot);
        player.TakeDamage(damage, info.Sender.ActorNumber, loadout[gunIndex].gunName, headshot);
    }
    #endregion


}
