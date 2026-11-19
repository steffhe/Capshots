using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using UnityEngine.UI;
using System;
using TMPro;
using UnityEngine.Rendering.Universal;

public class Player : MonoBehaviourPunCallbacks, IPunObservable
{
    #region Variables
    public float speed;
    public float sprintModifier;
    public float crouchModifier;
    public float jumpForce;
    public int maxHealth;
    public int currentHealth;
    public Camera normalCam;
    public GameObject cameraParent;
    public Transform weaponParent;
    public Transform groundDetector;
    public LayerMask ground;
    public float lengthOfSlide;
    public float slideModifier;
    public float crouchAmount;
    public float slideAmount;
    public GameObject standingCollider;
    public GameObject standingColliderHead;
    public GameObject crouchingCollider;
    public GameObject crouchingColliderHead;
    public GameObject deadPrefab;

    public ProfileData playerProfile;
    public TextMeshPro playerUsername;

    private Transform uiHealthBar;
    private Text uiAmmo;
    private Text uiUsername;

    private Rigidbody rig;
    private float baseFOV;
    private float sprintFOVModifier = 1.25f;

    private Vector3 weaponParentCurrentPosition;
    private Vector3 weaponParentorigin;
    private Vector3 targetWeaponBobPosition;

    private float movementCounter;
    private float idleCounter;
    private Manager manager;
    private Weapon weapon;
    private Look look;
    private Pause pauseMenu;
    private Camera viewmodelCam;
    private bool wasCursorLocked;

    private bool isDead;

    private bool crouched;
    private bool sliding;
    private bool slideRequested;
    private float slideTime;
    private Vector3 slideDirection;

    private Vector3 origin;

    private float aimAngle;

    [Header("Spawn protection")]
    // Seconds a freshly spawned player can't be damaged. Firing ends it early.
    public float spawnProtectionTime = 3f;
    public float protectionBlinkRate = 8f;

    private bool isProtected;
    private float protectionLeft;
    private Renderer[] blinkRenderers;
    private bool[] blinkRenderersEnabled;

    public bool IsProtected => isProtected;

    [Header("Health regeneration")]
    // Seconds without taking damage before health starts coming back
    public float regenDelay = 5f;
    public float regenPerSecond = 150f;

    private float lastDamageTime = float.NegativeInfinity;
    private float regenBuffer;

    [Header("Team colours")]
    // How strongly other players' bodies are tinted with their team colour in team mode
    [Range(0f, 1f)] public float teamTintStrength = 0.6f;
    private int teamVisualsVersion = -1;

    // Updated every physics step, used by Weapon for accuracy
    public bool IsGrounded { get; private set; } = true;
    // Horizontal speed compared to normal walking speed (0 = standing still, 1 = walking or faster)
    public float MoveSpeedRatio { get; private set; }

    [Header("Viewmodel")]
    // Field of view the player's own gun is drawn with. It stays the same when the
    // field of view setting changes or the player aims, so the gun never stretches.
    public float viewmodelFov = 70f;

    // The player this client controls, or null while dead
    public static Player Local { get; private set; }



    #endregion

    #region Photon Callbacks
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(weaponParent.transform.localEulerAngles.x);
            stream.SendNext(currentHealth);
            stream.SendNext(isProtected);
        }
        else
        {
            aimAngle = (float)stream.ReceiveNext();
            currentHealth = (int)stream.ReceiveNext();
            isProtected = (bool)stream.ReceiveNext();
        }
    }
    #endregion


    #region MonoBehaviour Callbacks
    private void Start()
    {
        manager = Manager.Instance != null ? Manager.Instance : FindAnyObjectByType<Manager>();
        weapon = GetComponent<Weapon>();
        look = GetComponent<Look>();

        currentHealth = maxHealth;
        if (photonView.IsMine) Local = this;
        isProtected = spawnProtectionTime > 0f;
        protectionLeft = spawnProtectionTime;
            cameraParent.SetActive(photonView.IsMine);

        if (!photonView.IsMine)
        {
            gameObject.layer = Layers.RemotePlayer;
            standingCollider.layer = Layers.RemotePlayer;
            standingColliderHead.layer = Layers.RemotePlayer;
            crouchingCollider.layer = Layers.RemotePlayer;
            crouchingColliderHead.layer = Layers.RemotePlayer;
        }
        else
        {
            int playerLayer = Layers.LocalPlayer;
            normalCam.cullingMask &= ~(1 << playerLayer);
            CreateViewmodelCamera();
        }

            weaponParentorigin = weaponParent.localPosition;
        weaponParentCurrentPosition = weaponParentorigin;
        baseFOV = GameSettings.Fov;
        origin = normalCam.transform.localPosition;

        // if (Camera.main)Camera.main.enabled = false;

        rig = GetComponent<Rigidbody>();

        if (photonView.IsMine)
        {
            uiHealthBar =  GameObject.Find("HUD/Health/Bar").transform;
            uiAmmo = GameObject.Find("HUD/Ammo/Text").GetComponent<Text>();
            uiUsername = GameObject.Find("HUD/Username/Text").GetComponent<Text>();
            RefreshHealthBar();

            uiUsername.text = Manager.UsernameWithLevel(Launcher.myProfile);
            photonView.RPC("SyncProfile", RpcTarget.All, Launcher.myProfile.username, Launcher.myProfile.level, Launcher.myProfile.xp);


        }
    }


    private void Update()
    {
        if (!photonView.IsMine)
        {
            RefreshMultiplayerState();
            UpdateProtectionBlink();
            RefreshTeamVisuals();
            return;
        }

        RegenerateHealth();

        if (isProtected)
        {
            protectionLeft -= Time.deltaTime;
            if (protectionLeft <= 0f) EndSpawnProtection();
            else FeedbackUI.SpawnProtection(protectionLeft);
        }

        //Axis'
        Vector2 moveInput = GameInput.MoveValue;
        float hmove = moveInput.x;
        float vmove = moveInput.y;

        //Controls
        bool sprint = GameInput.Held(GameInput.Sprint);
        bool jump = GameInput.Pressed(GameInput.Jump);
        bool crouch = GameInput.Pressed(GameInput.Crouch);
        bool pause = GameInput.PausePressed;

        // A press is only true for one rendered frame, so read it here and let FixedUpdate consume it
        if (GameInput.Pressed(GameInput.Slide)) slideRequested = true;

        //States
        bool isgrounded = Physics.Raycast(groundDetector.position, Vector3.down, 0.15f, ground);
        bool isJumping = jump && isgrounded;
        bool isSprinting = sprint && vmove > 0 && !isJumping && isgrounded;
        bool isCrouching = crouch && !isJumping && !isSprinting && isgrounded;

        //Pause
        if (pause)
        {
            if (pauseMenu == null) pauseMenu = FindAnyObjectByType<Pause>();
            if (pauseMenu != null) pauseMenu.TogglePause();
        }
        PauseWhenBrowserReleasesMouse();
        if ( Pause.paused)
        {
             hmove = 0f;
             vmove = 0f;
             sprint = false;
             jump = false;
             crouch = false;
             isgrounded = false;
             isJumping = false;
             isSprinting = false;
             isCrouching = false;
        }

        //Crouching
        if (isCrouching)
        {
           photonView.RPC("SetCrouch", RpcTarget.All, !crouched);
        }

        //Jumping
        if (isJumping)
        {
            if (crouched) photonView.RPC("SetCrouch", RpcTarget.All, false);
            rig.AddForce(Vector3.up * jumpForce);
        }

        //TEST
        //if (Input.GetKeyDown(KeyCode.U)) TakeDamage(100);

        //Headbob
        if (sliding) {
            HeadBob(movementCounter, .15f, .075f);
            weaponParent.localPosition = Vector3.Lerp(weaponParent.localPosition, targetWeaponBobPosition, Time.deltaTime * 10f);
        }
        else if (hmove == 0 && vmove == 0)
        {
            HeadBob(idleCounter, .025f, .025f);
            idleCounter += Time.deltaTime * 2f;
            weaponParent.localPosition = Vector3.Lerp(weaponParent.localPosition, targetWeaponBobPosition, Time.deltaTime * 2f);
        }
        else if (!isSprinting && !crouched)
        {
            HeadBob(movementCounter, .035f, .035f);
            movementCounter += Time.deltaTime * 3f;
            weaponParent.localPosition = Vector3.Lerp(weaponParent.localPosition, targetWeaponBobPosition, Time.deltaTime * 6f);
        }
        else if (crouched)
        {
            HeadBob(movementCounter, .002f, .002f);
            movementCounter += Time.deltaTime * 3f;
            weaponParent.localPosition = Vector3.Lerp(weaponParent.localPosition, targetWeaponBobPosition, Time.deltaTime * 6f);
        }
        else if (isSprinting)
        {
            HeadBob(movementCounter, .15f, .075f);
            movementCounter += Time.deltaTime * 7f;
        weaponParent.localPosition = Vector3.Lerp(weaponParent.localPosition, targetWeaponBobPosition, Time.deltaTime * 10f);
        }

        //UI Refreshes
        RefreshHealthBar();
        weapon.RefreshAmmo(uiAmmo);

    }


    // Update is called once per frame
    void FixedUpdate()
    {
        if(!photonView.IsMine) return;

        // Picks up changes made in the settings menu
        baseFOV = GameSettings.Fov;

        //Axis'
        Vector2 moveInput = GameInput.MoveValue;
        float hmove = moveInput.x;
        float vmove = moveInput.y;

        //Controls
        bool sprint = GameInput.Held(GameInput.Sprint);
        bool jump = GameInput.Held(GameInput.Jump);
        bool slide = slideRequested;
        slideRequested = false;


        //States
        bool isgrounded = Physics.Raycast(groundDetector.position, Vector3.down, 0.1f,ground );
        IsGrounded = isgrounded;
        Vector3 horizontal = rig.linearVelocity;
        horizontal.y = 0f;
        float walkSpeed = speed * Time.fixedDeltaTime;
        MoveSpeedRatio = walkSpeed > 0f ? Mathf.Clamp01(horizontal.magnitude / walkSpeed) : 0f;
        bool isJumping = jump && isgrounded;
        bool isSprinting = sprint && vmove > 0 && !isJumping && isgrounded;
        bool isSliding = isSprinting && slide && !sliding;

        if (Pause.paused)
        {
            hmove = 0f;
            vmove = 0f;
            sprint = false;
            jump = false;
            isgrounded = false;
            isJumping = false;
            isSprinting = false;
            isSliding = false;
            slide = false;
        }

        Vector3 direction = Vector3.zero;
        float adjustedSpeed = speed;

        //Movement
        if (!sliding)
        {

        direction = new Vector3(hmove, 0, vmove);
        direction.Normalize();
            direction = transform.TransformDirection(direction);

            if (isSprinting)
            {
                if (crouched) photonView.RPC("SetCrouch", RpcTarget.All, false);
                adjustedSpeed *= sprintModifier;
            }else if (crouched){
                adjustedSpeed *= crouchModifier;
            }

            // Heavier guns, and aiming, slow you down
            adjustedSpeed *= weapon.MoveSpeedMultiplier();

        }
        else
        {
            direction = slideDirection;
            adjustedSpeed *= slideModifier;
            slideTime -= Time.deltaTime;
            if (slideTime <= 0)
            {
                if (crouched) photonView.RPC("SetCrouch", RpcTarget.All, false);
                sliding = false;
                weaponParentCurrentPosition += Vector3.up * (slideAmount - crouchAmount);
            }
        }

        Vector3 targetVelocity = direction * adjustedSpeed * Time.deltaTime;
        targetVelocity.y = rig.linearVelocity.y;

        rig.linearVelocity = targetVelocity;

        //Sliding
        if (isSliding)
        {
            sliding = true;
            slideDirection = direction;
            slideTime = lengthOfSlide;
            // Adjust cam
            weaponParentCurrentPosition += Vector3.down * (slideAmount - crouchAmount);
            if (!crouched) photonView.RPC("SetCrouch", RpcTarget.All, true);

        }

        //Camera stuff
        if (sliding)
        {
            normalCam.fieldOfView = Mathf.Lerp(normalCam.fieldOfView, baseFOV * sprintFOVModifier * 1.25f, Time.deltaTime * 8);
            normalCam.transform.localPosition = Vector3.Lerp(normalCam.transform.localPosition, origin + Vector3.down * slideAmount, Time.deltaTime * 6f);
        }
        else{

         if (isSprinting) { normalCam.fieldOfView =  Mathf.Lerp(normalCam.fieldOfView,baseFOV * sprintFOVModifier, Time.deltaTime * 8) ; }
        else { normalCam.fieldOfView = Mathf.Lerp(normalCam.fieldOfView, baseFOV, Time.deltaTime * 8); }

            if (crouched) { normalCam.transform.localPosition = Vector3.Lerp(normalCam.transform.localPosition, origin + Vector3.down * crouchAmount, Time.deltaTime * 6f); }
            else { normalCam.transform.localPosition = Vector3.Lerp(normalCam.transform.localPosition, origin, Time.deltaTime * 6f); }

        }

        if (weapon.isAiming)
        {
            Gun gun = weapon.CurrentGun;
            if (gun != null && gun.scopeFov > 0f)
            {
                // Scoped guns zoom to their own field of view
                normalCam.fieldOfView = Mathf.Lerp(normalCam.fieldOfView, gun.scopeFov, Time.deltaTime * 15f);
            }
            else
            {
            normalCam.fieldOfView = Mathf.Lerp(normalCam.fieldOfView, baseFOV / 4f, Time.deltaTime * 8);

            }
        }

        // Scoped guns also zoom the gun model, so the scope comes up to the eye while aiming
        if (viewmodelCam != null)
        {
            Gun currentGun = weapon.CurrentGun;
            bool scoped = weapon.isAiming && currentGun != null && currentGun.scopeViewmodelFov > 0f;
            float targetFov = scoped ? currentGun.scopeViewmodelFov : viewmodelFov;
            viewmodelCam.fieldOfView = Mathf.Lerp(viewmodelCam.fieldOfView, targetFov, Time.deltaTime * 15f);
        }
    }

    #endregion

    #region Private Methods

    private void RefreshMultiplayerState()
    {
        float cacheEulY = weaponParent.localEulerAngles.y;

        Quaternion targetRotation = Quaternion.identity * Quaternion.AngleAxis(aimAngle, Vector3.right);
        weaponParent.rotation = Quaternion.Slerp(weaponParent.rotation, targetRotation, Time.deltaTime * 8f);

        Vector3 finalRotation = weaponParent.localEulerAngles;
        finalRotation.y = cacheEulY;

        weaponParent.localEulerAngles = finalRotation;
    }


    [PunRPC]
    void SetCrouch(bool state)
    {
        if (crouched == state) return;

        crouched = state;

        if (crouched)
        {
            standingCollider.SetActive(false);
            standingColliderHead.SetActive(false);
            crouchingCollider.SetActive(true);
            crouchingColliderHead.SetActive(true);
            weaponParentCurrentPosition += Vector3.down * crouchAmount;

        }
        else
        {
            standingCollider.SetActive(true);
            standingColliderHead.SetActive(true);
            crouchingCollider.SetActive(false);
            crouchingColliderHead.SetActive(false);
            weaponParentCurrentPosition -= Vector3.down * crouchAmount;

        }

    }

    void HeadBob(float z, float xIntensity, float yIntensity)
    {
        float aimAdjust = 1f;
        if (weapon.isAiming)
        {
            aimAdjust = 0.1f;
        }
        targetWeaponBobPosition = weaponParentCurrentPosition + new Vector3(Mathf.Cos(z * 2) * xIntensity * aimAdjust, Mathf.Sin(z) * yIntensity * aimAdjust,0);
    }

    void RefreshHealthBar()
    {
        float healthRatio = (float)currentHealth / (float)maxHealth;
        uiHealthBar.localScale = new Vector3(healthRatio, 1, 1);
    }

    [PunRPC]
    private void SyncProfile(string username, int level, int xp)
    {
        playerProfile = new ProfileData(username, level,xp,150f, 150f);
        playerUsername.text = playerProfile.username;
    }
    #endregion


    #region Public Methods

    public void TakeDamage(int damage, int actor, string weaponName, bool headshot)
    {
        if (photonView.IsMine)
        {
            if (isDead || isProtected) return;

            lastDamageTime = Time.time;
            regenBuffer = 0f;

            currentHealth -= damage;
            if (currentHealth < 0) currentHealth = 0;

            RefreshHealthBar();

            // Feedback: screen flash, direction to the attacker and a small flinch
            Player attacker = FindPlayerByActor(actor);
            FeedbackUI.Damaged((float)damage / maxHealth, attacker != null ? attacker.transform.position : (Vector3?)null, transform);
            if (look != null) look.AddRecoil(UnityEngine.Random.Range(0.5f, 1.5f), UnityEngine.Random.Range(-1f, 1f));

            if (currentHealth <= 0)
            {
                isDead = true;

                GameObject dp = PhotonNetwork.Instantiate("Dead Player", transform.position, transform.rotation);
                dp.GetComponent<Rigidbody>().linearVelocity = rig.linearVelocity;

                manager.DropAmmo_S(GroundPosition());
                manager.Spawn();
                manager.ChangeStat_S(PhotonNetwork.LocalPlayer.ActorNumber, PlayerStat.Deaths, 1);

                if (actor >= 0) manager.ChangeStat_S(actor, PlayerStat.Kills, 1);

                manager.KillFeed_S(actor, PhotonNetwork.LocalPlayer.ActorNumber, weaponName, headshot);
                FeedbackUI.ShowDeath(manager.GetPlayerName(actor), weaponName, headshot, manager.respawnTime);

                PhotonNetwork.Destroy(gameObject);
            }
        }
    }

    // Colours this player's name tag and body by team. Runs again whenever the team list changes.
    private void RefreshTeamVisuals()
    {
        if (teamVisualsVersion == Manager.TeamsVersion) return;
        teamVisualsVersion = Manager.TeamsVersion;

        Manager m = Manager.Instance;
        int team = m != null ? m.GetTeam(photonView.OwnerActorNr) : -1;
        if (team < 0) return;

        Color teamColor = Manager.TeamColors[team % 2];
        if (playerUsername != null) playerUsername.color = teamColor;

        Color tint = Color.Lerp(Color.white, teamColor, teamTintStrength);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer body in GetComponentsInChildren<Renderer>(true))
        {
            // Body meshes only: not the name tag, the gun or effects
            if (!(body is MeshRenderer || body is SkinnedMeshRenderer)) continue;
            if (body.GetComponent<TextMeshPro>() != null) continue;
            if (weaponParent != null && body.transform.IsChildOf(weaponParent)) continue;

            body.GetPropertyBlock(block);
            block.SetColor("_BaseColor", tint); // URP
            block.SetColor("_Color", tint);     // built-in shaders
            body.SetPropertyBlock(block);
        }
    }

    private void OnDestroy()
    {
        if (Local == this) Local = null;
    }

    // Draws the player's own gun with a second camera on top of the world (a URP overlay camera),
    // so the gun never clips into walls. The main camera skips the gun's layer.
    private void CreateViewmodelCamera()
    {
        normalCam.cullingMask &= ~(1 << Layers.LocalGun);

        GameObject go = new GameObject("Viewmodel Camera");
        go.transform.SetParent(normalCam.transform, false);

        viewmodelCam = go.AddComponent<Camera>();
        viewmodelCam.cullingMask = 1 << Layers.LocalGun;
        viewmodelCam.fieldOfView = viewmodelFov;
        viewmodelCam.nearClipPlane = 0.01f;
        viewmodelCam.farClipPlane = 10f;

        UniversalAdditionalCameraData viewmodelData = viewmodelCam.GetUniversalAdditionalCameraData();
        viewmodelData.renderType = CameraRenderType.Overlay;
        // Overlay cameras clear depth by default (clearDepth is read-only in code), so the gun
        // is always drawn in front of walls
        viewmodelData.renderShadows = false;
        // Match the main camera so the gun gets the same colour grading, bloom and anti-aliasing
        // as the world it's drawn on top of
        UniversalAdditionalCameraData mainData = normalCam.GetUniversalAdditionalCameraData();
        viewmodelData.renderPostProcessing = mainData.renderPostProcessing;
        viewmodelData.antialiasing = mainData.antialiasing;
        viewmodelData.antialiasingQuality = mainData.antialiasingQuality;

        normalCam.GetUniversalAdditionalCameraData().cameraStack.Add(viewmodelCam);
    }

    // Browser builds only: the browser releases the mouse when the player presses Escape (or switches
    // tabs), and the game never sees that Escape. So open the pause menu when the mouse lock is lost.
    // Only the change from locked to unlocked counts: after resuming, the browser waits for a click
    // before locking the mouse again, and that wait must not pause the game again.
    // The lock is read from the browser itself: Cursor.lockState only holds what the game last asked
    // for, and Look sets it to Locked every frame, so it never shows the browser letting go.
    private void PauseWhenBrowserReleasesMouse()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (TouchControls.Enabled) return;
        bool locked = Browser.IsPointerLocked;
        if (wasCursorLocked && !locked && !Pause.paused)
        {
            if (pauseMenu == null) pauseMenu = FindAnyObjectByType<Pause>();
            if (pauseMenu != null) pauseMenu.TogglePause();
        }
        wasCursorLocked = locked;
#endif
    }

    private void RegenerateHealth()
    {
        if (isDead || currentHealth >= maxHealth || Time.time - lastDamageTime < regenDelay) return;

        regenBuffer += regenPerSecond * Time.deltaTime;
        int amount = (int)regenBuffer;
        if (amount <= 0) return;

        regenBuffer -= amount;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    // Where an ammo box dropped by this player should rest
    private Vector3 GroundPosition()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out hit, 50f, ground, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * 0.4f;
        }
        return transform.position;
    }

    public void EndSpawnProtection()
    {
        if (!isProtected) return;
        isProtected = false;
        protectionLeft = 0f;
        if (photonView.IsMine) FeedbackUI.SpawnProtection(0f);
    }

    // Other players see a protected player blink
    private void UpdateProtectionBlink()
    {
        if (isProtected)
        {
            if (blinkRenderers == null)
            {
                // Only meshes: tracers and effects manage their own visibility
                blinkRenderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(), r => r is MeshRenderer || r is SkinnedMeshRenderer);
                blinkRenderersEnabled = new bool[blinkRenderers.Length];
                for (int i = 0; i < blinkRenderers.Length; i++) blinkRenderersEnabled[i] = blinkRenderers[i].enabled;
            }

            bool visible = Mathf.Repeat(Time.time * protectionBlinkRate, 1f) < 0.5f;
            for (int i = 0; i < blinkRenderers.Length; i++)
            {
                if (blinkRenderers[i] != null && blinkRenderersEnabled[i]) blinkRenderers[i].enabled = visible;
            }
        }
        else if (blinkRenderers != null)
        {
            for (int i = 0; i < blinkRenderers.Length; i++)
            {
                if (blinkRenderers[i] != null) blinkRenderers[i].enabled = blinkRenderersEnabled[i];
            }
            blinkRenderers = null;
            blinkRenderersEnabled = null;
        }
    }

    private static Player FindPlayerByActor(int actor)
    {
        foreach (Player p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (p.photonView.OwnerActorNr == actor) return p;
        }
        return null;
    }




    #endregion

}
