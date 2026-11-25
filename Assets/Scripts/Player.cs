using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Fusion;
using TMPro;
using UnityEngine.Rendering.Universal;

// A player's body. The host runs movement, health and death from the player's input
// (FixedUpdateNetwork); the player's own game predicts the same movement so it feels instant.
// Everything else here (camera, gun drift, HUD, team colours) is local presentation.
[RequireComponent(typeof(CharacterController))]
public class Player : NetworkBehaviour
{
    #region Variables
    public float speed;
    public float sprintModifier;
    public float crouchModifier;
    public float jumpForce;
    public int maxHealth;
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

    [Header("Character controller")]
    // Set by Capshots > Set Up Fusion from the old body colliders: the capsule's bottom (relative
    // to the player's position) and its height standing and crouching
    public float bodyBottom = -1f;
    public float standingHeight = 2f;
    public float crouchingHeight = 1.5f;

    public ProfileData playerProfile;
    public TextMeshPro playerUsername;

    // Speeds and the jump force were tuned for the old Rigidbody movement, which multiplied them by
    // the 0.02 s physics step (and a mass of 1). This turns them into metres per second.
    private const float LegacyStep = 0.02f;

    private Transform uiHealthBar;
    private Image uiHealthBarImage;
    private Text uiAmmo;
    private Text uiUsername;

    private CharacterController controller;
    private HitboxRoot hitboxRoot;
    private Hitbox[] standingHitboxes = new Hitbox[0];
    private Hitbox[] crouchingHitboxes = new Hitbox[0];
    private float baseFOV;
    private float sprintFOVModifier = 1.25f;
    // Field of view while aiming a gun without a scope, as a share of the normal one
    private const float AimFovMultiplier = 0.6f;

    private Vector3 weaponParentCurrentPosition;
    private Vector3 weaponParentorigin;

    // The gun's hover drift: time along its slow loop, and how far it trails behind the movement
    private float hoverCounter;
    private Vector2 weaponTrail;
    private Manager manager;
    private Weapon weapon;
    private Look look;
    private Pause pauseMenu;
    private Camera viewmodelCam;
    private bool wasCursorLocked;

    private PlayerCap[] caps = new PlayerCap[0];
    // The hat the caps are (see Hats)
    private int wornHat = -1;
    private int wornFace = -1;
    private DroneBody[] drones = new DroneBody[0];
    private DroneHands hands;
    private GunSatellites satellites;
    private HoverVisuals hoverVisuals;
    private bool appliedCrouch;
    private string shownName;

    private Vector3 origin;

    [Header("Spawn protection")]
    // Seconds a freshly spawned player can't be damaged. Firing ends it early.
    public float spawnProtectionTime = 3f;
    // How see-through other players look while protected (0 invisible, 1 solid)
    [Range(0f, 1f)] public float protectionAlpha = 0.35f;

    // While protected: each body part drawn see-through, with what to put back afterwards
    private class GhostedRenderer
    {
        public Material[] materials;
        public UnityEngine.Rendering.ShadowCastingMode shadows;
    }
    private Dictionary<Renderer, GhostedRenderer> ghosted;
    private GameObject ghostedGun;
    private MaterialPropertyBlock ghostBlock;
    private static Material ghostMaterial;

    [Header("Health regeneration")]
    // Seconds without taking damage before health starts coming back
    public float regenDelay = 5f;
    public float regenPerSecond = 150f;

    [Header("Team colours")]
    // How strongly other players' bodies are tinted with their team colour in team mode
    [Range(0f, 1f)] public float teamTintStrength = 0.6f;
    private int teamVisualsVersion = -1;
    private int ownHandsVersion = -1;

    [Header("Viewmodel")]
    // Field of view the player's own gun is drawn with. It stays the same when the
    // field of view setting changes or the player aims, so the gun never stretches.
    public float viewmodelFov = 70f;

    // The player this client controls, or null while dead
    public static Player Local { get; private set; }
    // The middle of the head, this far above the player's position (the prefab's proportions)
    public const float HeadHeight = 0.55f;

    // Every body in the match (people and bots), alive or not yet despawned
    public static readonly List<Player> All = new List<Player>();

    // Thinks for this body if it's a bot (host only)
    private BotBrain brain;

    #endregion

    #region Networked state

    [Networked] public int Health { get; set; }
    [Networked] private NetworkBool Crouched { get; set; }
    [Networked] private NetworkBool Sliding { get; set; }
    [Networked] private TickTimer SlideTimer { get; set; }
    [Networked] private Vector3 SlideDirection { get; set; }
    [Networked] private Vector3 Velocity { get; set; }
    [Networked] private NetworkBool Grounded { get; set; }
    // Where this player looks up or down, in degrees (positive = up). Other players see the gun tilt with it.
    [Networked] public float Pitch { get; set; }
    [Networked] private TickTimer ProtectionTimer { get; set; }
    [Networked] private TickTimer RegenDelayTimer { get; set; }
    [Networked] private float RegenBuffer { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    [Networked] private NetworkBool Dead { get; set; }
    // Set when this body is a bot: the bot's own id, standing in for the player that would control
    // it. Bots have no input authority (nobody's game sends their input); the host runs them.
    [Networked] public PlayerRef BotId { get; set; }

    #endregion

    #region Public state

    public bool IsLocal => Object != null && Object.IsValid && HasInputAuthority;
    public bool IsBot => Object != null && Object.IsValid && BotId.IsRealPlayer;
    // Who this body belongs to on the scoreboard: the player controlling it, or the bot's id
    public PlayerRef Owner => Object != null && Object.IsValid ? (BotId.IsRealPlayer ? BotId : Object.InputAuthority) : PlayerRef.None;
    // Owner's PlayerRef.PlayerId
    public int ActorId => Object != null && Object.IsValid ? Owner.PlayerId : -1;
    public int currentHealth => Object != null && Object.IsValid ? Health : maxHealth;
    public bool IsCrouched => Object != null && Object.IsValid && Crouched;
    public bool IsProtected => Object != null && Object.IsValid && ProtectionTimer.IsRunning;
    public bool IsDead => Object != null && Object.IsValid && Dead;

    // Updated every tick, used by Weapon for accuracy
    public bool IsGrounded => Object == null || !Object.IsValid || Grounded;
    // Horizontal speed compared to normal walking speed (0 = standing still, 1 = walking or faster)
    public float MoveSpeedRatio
    {
        get
        {
            if (Object == null || !Object.IsValid) return 0f;
            Vector3 horizontal = Velocity;
            horizontal.y = 0f;
            float walkSpeed = speed * LegacyStep;
            return walkSpeed > 0f ? Mathf.Clamp01(horizontal.magnitude / walkSpeed) : 0f;
        }
    }

    // Horizontal speed in m/s, from the simulated velocity (steadier than measuring how far the
    // body moved since the last frame, which jumps around with frame and network timing)
    public float HorizontalSpeed
    {
        get
        {
            if (Object == null || !Object.IsValid) return 0f;
            Vector3 v = Velocity;
            v.y = 0f;
            return v.magnitude;
        }
    }

    // Where shots come from: the camera position this tick, following crouching and sliding
    public Vector3 EyePosition
    {
        get
        {
            float drop = 0f;
            if (Sliding) drop = slideAmount;
            else if (Crouched) drop = crouchAmount;
            Vector3 local = origin + Vector3.down * drop;
            return cameraParent.transform.TransformPoint(local);
        }
    }

    // How fast the body is moving (a thrown grenade carries some of it)
    public Vector3 CurrentVelocity => Object != null && Object.IsValid ? Velocity : Vector3.zero;

    // The body's capsule in world space (its character controller), for plasma grenades sticking to it
    public void BodyCapsule(out Vector3 bottom, out Vector3 top, out float radius)
    {
        if (controller == null)
        {
            radius = 0.5f;
            bottom = transform.position + Vector3.up * (bodyBottom + radius);
            top = transform.position + Vector3.up * (bodyBottom + standingHeight - radius);
            return;
        }
        radius = controller.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        float half = Mathf.Max(0f, controller.height * 0.5f * transform.lossyScale.y - radius);
        Vector3 center = transform.TransformPoint(controller.center);
        bottom = center - transform.up * half;
        top = center + transform.up * half;
    }

    #endregion

    #region Fusion Callbacks

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        hitboxRoot = GetComponent<HitboxRoot>();
        weapon = GetComponent<Weapon>();
        look = GetComponent<Look>();
        origin = normalCam.transform.localPosition;
        weaponParentorigin = weaponParent.localPosition;
        weaponParentCurrentPosition = weaponParentorigin;

        standingHitboxes = HitboxesUnder(standingCollider, standingColliderHead);
        crouchingHitboxes = HitboxesUnder(crouchingCollider, crouchingColliderHead);

        // The hitboxes sit under the body parts in the prefab, but the crouching parts start switched
        // off, and a switched-off hitbox is never initialised. Move them to the player itself (same
        // place in the world) so they're always active; the host turns each set on or off instead.
        foreach (Hitbox h in standingHitboxes) h.transform.SetParent(transform, true);
        foreach (Hitbox h in crouchingHitboxes) h.transform.SetParent(transform, true);
        // Moving them changes their order, so the root has to list them again
        if (hitboxRoot != null) hitboxRoot.InitHitboxes();
    }

    public override void Spawned()
    {
        manager = Manager.Instance != null ? Manager.Instance : FindAnyObjectByType<Manager>();
        if (GetComponent<PlayerSounds>() == null) gameObject.AddComponent<PlayerSounds>();
        if (!All.Contains(this)) All.Add(this);

        if (HasStateAuthority)
        {
            Health = maxHealth;
            ProtectionTimer = spawnProtectionTime > 0f ? TickTimer.CreateFromSeconds(Runner, spawnProtectionTime) : TickTimer.None;
            // Bots aren't Fusion players, so they have no player object
            if (!IsBot) Runner.SetPlayerObject(Object.InputAuthority, Object);
            SetHitboxes(false);

            if (IsBot)
            {
                brain = GetComponent<BotBrain>();
                if (brain == null) brain = gameObject.AddComponent<BotBrain>();
                brain.Initialize(this);
            }
        }

        // The HUD is hidden while the local player has no body, and GameObject.Find can't see hidden objects
        if (HasInputAuthority && manager != null && manager.Hud != null) manager.Hud.SetActive(true);

        SetControllerShape(false);

        cameraParent.SetActive(IsLocal);

        if (!IsLocal)
        {
            gameObject.layer = Layers.RemotePlayer;
            standingCollider.layer = Layers.RemotePlayer;
            standingColliderHead.layer = Layers.RemotePlayer;
            crouchingCollider.layer = Layers.RemotePlayer;
            crouchingColliderHead.layer = Layers.RemotePlayer;
        }
        else
        {
            Local = this;
            look.Initialize(transform.eulerAngles.y);
            // The first time ever: what the controls are (bots never get here: they aren't local players)
            if (!IsBot) ControlsHint.ShowOnce();

            int playerLayer = Layers.LocalPlayer;
            normalCam.cullingMask &= ~(1 << playerLayer);
            CreateViewmodelCamera();

            uiHealthBar = GameObject.Find("HUD/Health/Bar").transform;
            uiHealthBarImage = uiHealthBar.GetComponent<Image>();
            uiAmmo = GameObject.Find("HUD/Ammo/Text").GetComponent<Text>();
            uiUsername = GameObject.Find("HUD/Username/Text").GetComponent<Text>();
            RefreshHealthBar();

            uiUsername.text = Manager.UsernameWithLevel(Launcher.myProfile);
        }

        baseFOV = GameSettings.Fov;
        ApplyCrouchVisuals(true);

        // The player's colour (their team's in Team Deathmatch), for the body, hands, gun pieces and hat
        Color capColor = PlayerCap.ColorFor(ActorId);
        // The body is a little round drone (standing and crouching), on the same layers as the capsule and head
        bool teamMode = manager != null && manager.IsTeamMode;
        drones = new DroneBody[]
        {
            DroneBody.Dress(standingCollider != null ? standingCollider.transform : null, standingColliderHead != null ? standingColliderHead.transform : null, capColor, teamMode, false, WornFace()),
            DroneBody.Dress(crouchingCollider != null ? crouchingCollider.transform : null, crouchingColliderHead != null ? crouchingColliderHead.transform : null, capColor, teamMode, false, WornFace()),
        };
        // You don't see your own body, so its damage sparks would only show in front of your eyes
        foreach (DroneBody drone in drones) if (drone != null) drone.showSparks = !IsLocal;
        // Floating hand-orbs that hold the gun
        if (hands == null) hands = DroneHands.Attach(this, capColor);
        // Pieces floating round the gun, arranged for its form
        if (satellites == null) satellites = GunSatellites.Attach(this, capColor);
        // A hat, after the layers above are set so it's drawn like the head
        PutOnHat(WornHat());

        // The players float: the body hovers and leans, with a glow underneath. On the body's layer, so
        // the glow is hidden from your own camera like the rest of your body.
        hoverVisuals = GetComponent<HoverVisuals>();
        if (hoverVisuals == null)
        {
            Transform design = standingCollider != null ? standingCollider.transform.parent : null;
            hoverVisuals = HoverVisuals.Attach(this, design, bodyBottom, standingCollider != null ? standingCollider.layer : gameObject.layer);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Local == this)
        {
            Local = null;
            // Died: linger on the body a moment before the map view (not when the match is over,
            // which removes everyone's body)
            if (manager != null && !manager.Ending && !manager.MatchOver) DeathCam.Begin(normalCam);
        }
        All.Remove(this);
    }

    // This tick's input: from the controlling player's game, or for a bot from its brain on the host
    public bool ReadInput(out PlayerInputData input)
    {
        if (BotId.IsRealPlayer)
        {
            if (brain != null && HasStateAuthority)
            {
                input = brain.InputForTick(Runner.Tick, Runner.DeltaTime);
                return true;
            }
            input = default;
            return false;
        }
        return GetInput(out input);
    }

    public override void FixedUpdateNetwork()
    {
        if (Dead) return;

        bool hasInput = ReadInput(out PlayerInputData input);
        if (!hasInput)
        {
            // No input arrived for this tick: keep facing the same way and stand still
            input = default;
            input.Yaw = transform.eulerAngles.y;
            input.Pitch = Pitch;
        }

        NetworkButtons pressed = hasInput ? input.Buttons.GetPressed(PreviousButtons) : default;
        if (hasInput) PreviousButtons = input.Buttons;

        // The match is over: stand still (gravity still applies), but keep looking around
        if (manager != null && manager.Ending)
        {
            input.Move = Vector2.zero;
            input.Buttons = default;
            pressed = default;
        }

        transform.rotation = Quaternion.Euler(0f, input.Yaw, 0f);
        Pitch = input.Pitch;

        // When this machine re-runs ticks to correct a prediction, the crouch state is rolled back
        // too; the capsule has to match it
        if (Runner.IsResimulation) SetControllerShape(Crouched);

        Move(input, pressed);

        if (HasStateAuthority)
        {
            if (ProtectionTimer.Expired(Runner)) ProtectionTimer = TickTimer.None;
            RegenerateHealth();
            CheckFellOut();
        }
    }

    #endregion

    #region Movement

    private void Move(PlayerInputData input, NetworkButtons pressed)
    {
        float dt = Runner.DeltaTime;

        float hmove = input.Move.x;
        float vmove = input.Move.y;

        bool sprint = input.Buttons.IsSet(InputButton.Sprint);
        bool jump = pressed.IsSet(InputButton.Jump);
        bool crouch = pressed.IsSet(InputButton.Crouch);
        bool slide = pressed.IsSet(InputButton.Slide);

        //States
        bool isgrounded = controller.isGrounded || Physics.Raycast(groundDetector.position, Vector3.down, 0.15f, ground, QueryTriggerInteraction.Ignore);
        Grounded = isgrounded;
        bool isJumping = jump && isgrounded;
        bool isSprinting = sprint && vmove > 0 && !isJumping && isgrounded;
        bool isCrouching = crouch && !isJumping && !isSprinting && isgrounded;
        bool isSliding = isSprinting && slide && !Sliding;

        Vector3 velocity = Velocity;

        //Crouching
        if (isCrouching) SetCrouch(!Crouched);

        //Jumping
        if (isJumping)
        {
            if (Crouched) SetCrouch(false);
            velocity.y = jumpForce * LegacyStep;
        }

        Vector3 direction;
        float adjustedSpeed = speed;

        //Movement
        if (!Sliding)
        {
            direction = new Vector3(hmove, 0, vmove);
            direction.Normalize();
            direction = transform.TransformDirection(direction);

            if (isSprinting)
            {
                if (Crouched) SetCrouch(false);
                adjustedSpeed *= sprintModifier;
            }
            else if (Crouched)
            {
                adjustedSpeed *= crouchModifier;
            }

            // Heavier guns, and aiming, slow you down
            adjustedSpeed *= weapon.MoveSpeedMultiplier();
        }
        else
        {
            direction = SlideDirection;
            adjustedSpeed *= slideModifier;
            if (SlideTimer.ExpiredOrNotRunning(Runner))
            {
                if (Crouched) SetCrouch(false);
                Sliding = false;
                SlideTimer = TickTimer.None;
            }
        }

        //Sliding
        if (isSliding)
        {
            Sliding = true;
            SlideDirection = direction;
            SlideTimer = TickTimer.CreateFromSeconds(Runner, lengthOfSlide);
            if (!Crouched) SetCrouch(true);
        }

        Vector3 horizontal = direction * adjustedSpeed * LegacyStep;
        velocity.x = horizontal.x;
        velocity.z = horizontal.z;

        // Gravity, with a small push down while standing so the controller stays on slopes and steps
        if (isgrounded && velocity.y < 0f) velocity.y = -2f;
        else velocity.y += Physics.gravity.y * dt;

        controller.Move(velocity * dt);
        if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
        // Hitting a ceiling stops the jump
        if ((controller.collisionFlags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;

        Velocity = velocity;
    }

    void SetCrouch(bool state)
    {
        if (Crouched == state) return;
        Crouched = state;
        SetControllerShape(state);
        SetHitboxes(state);
    }

    // Only the standing or the crouching body can be hit. Hitbox state is networked, so the host sets it.
    private void SetHitboxes(bool crouched)
    {
        if (hitboxRoot == null || !HasStateAuthority) return;
        foreach (Hitbox h in standingHitboxes) hitboxRoot.SetHitboxActive(h, !crouched);
        foreach (Hitbox h in crouchingHitboxes) hitboxRoot.SetHitboxActive(h, crouched);
    }

    // The character controller's capsule shrinks while crouching, so crouching fits under low ceilings
    private void SetControllerShape(bool crouched)
    {
        float height = crouched ? crouchingHeight : standingHeight;
        controller.height = height;
        controller.center = new Vector3(0f, bodyBottom + height / 2f, 0f);
    }

    private static Hitbox[] HitboxesUnder(params GameObject[] parts)
    {
        List<Hitbox> found = new List<Hitbox>();
        foreach (GameObject part in parts)
        {
            if (part != null) found.AddRange(part.GetComponentsInChildren<Hitbox>(true));
        }
        return found.ToArray();
    }

    #endregion

    #region Presentation

    private void Update()
    {
        if (Object == null || !Object.IsValid) return;

        ApplyCrouchVisuals(false);
        RefreshNameTag();
        RefreshHat();
        RefreshFace();
        RefreshDamageVisuals();

        if (!IsLocal)
        {
            RefreshMultiplayerState();
            UpdateProtectionGhost();
            RefreshTeamVisuals();
            return;
        }

        RefreshOwnHandsColor();
        // While protected your own gun and hand-orbs are see-through too, so you can tell (after
        // the colour refresh above, which would otherwise make them solid again)
        UpdateProtectionGhost();

        float protectionLeft = ProtectionTimer.RemainingTime(Runner) ?? 0f;
        FeedbackUI.SpawnProtection(protectionLeft);

        //Axis'
        Vector2 moveInput = GameInput.MoveValue;
        float hmove = moveInput.x;
        float vmove = moveInput.y;

        //Controls
        bool sprint = GameInput.Held(GameInput.Sprint);
        bool pause = GameInput.PausePressed;

        //States
        bool isgrounded = Grounded;
        bool isSprinting = sprint && vmove > 0 && isgrounded;

        //Pause
        if (pause)
        {
            if (pauseMenu == null) pauseMenu = FindAnyObjectByType<Pause>();
            if (pauseMenu != null) pauseMenu.TogglePause();
        }
        PauseWhenBrowserReleasesMouse();
        if (Pause.paused)
        {
            hmove = 0f;
            vmove = 0f;
            isSprinting = false;
        }

        // The gun sits lower while crouching, and lower still while sliding
        weaponParentCurrentPosition = weaponParentorigin;
        if (Sliding) weaponParentCurrentPosition += Vector3.down * slideAmount;
        else if (Crouched) weaponParentCurrentPosition += Vector3.down * crouchAmount;

        UpdateWeaponHover(hmove, vmove, isSprinting);

        UpdateCamera(isSprinting);

        //UI Refreshes
        RefreshHealthBar();
        weapon.RefreshAmmo(uiAmmo);
    }

    // Field of view and camera height: wider while sprinting and sliding, lower while crouching
    private void UpdateCamera(bool isSprinting)
    {
        // Picks up changes made in the settings menu
        baseFOV = GameSettings.Fov;
        float t = Time.deltaTime;

        // One target field of view per frame. (Easing towards the normal one and then towards the
        // aiming one every frame made the two fight: the zoom stopped partway, at a point that
        // depended on the frame rate.)
        float targetFov = baseFOV;
        float fovSpeed = 8f;
        Gun aimedGun = weapon.isAiming ? weapon.CurrentGun : null;
        if (weapon.isAiming && aimedGun != null && aimedGun.scopeFov > 0f)
        {
            // Scoped guns zoom to their own field of view
            targetFov = aimedGun.scopeFov;
            fovSpeed = 15f;
        }
        else if (weapon.isAiming) targetFov = baseFOV * AimFovMultiplier;
        else if (Sliding) targetFov = baseFOV * sprintFOVModifier * 1.25f;
        else if (isSprinting) targetFov = baseFOV * sprintFOVModifier;
        normalCam.fieldOfView = Mathf.Lerp(normalCam.fieldOfView, targetFov, t * fovSpeed);

        Vector3 camTarget = origin;
        if (Sliding) camTarget += Vector3.down * slideAmount;
        else if (Crouched) camTarget += Vector3.down * crouchAmount;
        normalCam.transform.localPosition = Vector3.Lerp(normalCam.transform.localPosition, camTarget, t * 6f);

        // Scoped guns also zoom the gun model, so the scope comes up to the eye while aiming
        if (viewmodelCam != null)
        {
            Gun currentGun = weapon.CurrentGun;
            bool scoped = weapon.isAiming && currentGun != null && currentGun.scopeViewmodelFov > 0f;
            float targetViewmodelFov = scoped ? currentGun.scopeViewmodelFov : viewmodelFov;
            viewmodelCam.fieldOfView = Mathf.Lerp(viewmodelCam.fieldOfView, targetViewmodelFov, t * 15f);
        }
    }

    // Other players' guns tilt up and down with where they look
    private void RefreshMultiplayerState()
    {
        Quaternion targetRotation = Quaternion.AngleAxis(-Pitch, Vector3.right);
        weaponParent.localRotation = Quaternion.Slerp(weaponParent.localRotation, targetRotation, Time.deltaTime * 8f);
    }

    // Shows the standing or crouching body to match the networked crouch
    private void ApplyCrouchVisuals(bool force)
    {
        bool crouched = Crouched;
        if (!force && crouched == appliedCrouch) return;
        appliedCrouch = crouched;

        standingCollider.SetActive(!crouched);
        standingColliderHead.SetActive(!crouched);
        crouchingCollider.SetActive(crouched);
        crouchingColliderHead.SetActive(crouched);

        // Prediction can change the crouch on this machine before the host does; keep the capsule in step
        if (!HasStateAuthority && !IsLocal) SetControllerShape(crouched);
    }

    // The name above the player's head comes from the host's scoreboard
    private void RefreshNameTag()
    {
        if (playerUsername == null || manager == null) return;
        string name = manager.GetPlayerName(ActorId);
        if (name == shownName) return;
        shownName = name;
        playerUsername.text = name;
        playerProfile = new ProfileData(name, manager.GetPlayerLevel(ActorId), 0, 150f, 150f);
    }

    // The gun floats in front of you instead of bobbing with footsteps: a slow drift in a figure of eight,
    // trailing a little behind your movement and tilting as you strafe. Much smaller while aiming.
    private void UpdateWeaponHover(float hmove, float vmove, bool isSprinting)
    {
        float dt = Time.deltaTime;
        float aimAdjust = weapon.isAiming ? 0.1f : 1f;

        hoverCounter += dt;
        Vector3 drift = new Vector3(Mathf.Sin(hoverCounter * 0.9f) * 0.012f, Mathf.Sin(hoverCounter * 1.8f) * 0.01f, 0f);

        float trailAmount = Sliding ? 0.06f : isSprinting ? 0.045f : Crouched ? 0.015f : 0.03f;
        weaponTrail = Vector2.Lerp(weaponTrail, new Vector2(hmove, vmove) * trailAmount, dt * 4f);
        // Moving forward pulls the gun back and slightly down; strafing slides it the other way
        Vector3 trail = new Vector3(-weaponTrail.x, -Mathf.Abs(weaponTrail.y) * 0.4f, -weaponTrail.y);

        Vector3 target = weaponParentCurrentPosition + (drift + trail) * aimAdjust;
        weaponParent.localPosition = Vector3.Lerp(weaponParent.localPosition, target, dt * 8f);
        weaponParent.localRotation = Quaternion.Slerp(weaponParent.localRotation, Quaternion.Euler(0f, 0f, -hmove * 3f * aimAdjust), dt * 6f);
    }

    // Everyone sees a drone get hit (its shell flashes white and sparks fly) and, when it's badly
    // hurt, flicker and spark. Runs on every machine from the health the host syncs.
    private int shownHealth = -1;

    private void RefreshDamageVisuals()
    {
        int health = Health;
        if (shownHealth >= 0 && health < shownHealth && !Dead)
        {
            foreach (DroneBody drone in drones) if (drone != null) drone.Hit();
            if (!IsLocal)
            {
                Vector3 at = transform.position + Vector3.up * Random.Range(0f, 0.6f) + Random.insideUnitSphere * 0.2f;
                KillSpark.SpawnSmall(at, Random.onUnitSphere, Color.white, 4);
            }
        }
        shownHealth = health;

        float fraction = maxHealth > 0 ? (float)health / maxHealth : 1f;
        foreach (DroneBody drone in drones) if (drone != null) drone.SetHealth(fraction);
        if (IsLocal) FeedbackUI.LowHealth(fraction);
    }

    void RefreshHealthBar()
    {
        if (uiHealthBar == null) return;
        float healthRatio = (float)Health / (float)maxHealth;
        uiHealthBar.localScale = new Vector3(healthRatio, 1, 1);
        if (uiHealthBarImage != null) uiHealthBarImage.color = UITheme.HealthColor(healthRatio);
    }

    #endregion

    #region Damage

    // Host only. Falling off the map (or through a gap in it) kills, even during spawn protection.
    private void CheckFellOut()
    {
        if (Dead || manager == null || transform.position.y > manager.killHeight) return;
        ProtectionTimer = TickTimer.None;
        TakeDamage(maxHealth * 10, Owner, "Fell", false);
    }

    // Host only. Applies a hit and reports whether it killed. bulletDirection and hitOffset (where
    // the bullet hit, relative to this player's position) throw the ragdoll on a kill. Leave them out
    // for damage that isn't a bullet: the ragdoll is then knocked away from the attacker.
    public bool TakeDamage(int damage, PlayerRef attacker, string weaponName, bool headshot, Vector3 bulletDirection = default, Vector3 hitOffset = default)
    {
        if (!HasStateAuthority || Dead || IsProtected) return false;

        RegenDelayTimer = TickTimer.CreateFromSeconds(Runner, regenDelay);
        RegenBuffer = 0f;
        Health = Mathf.Max(0, Health - damage);

        Player attackerPlayer = FindPlayer(attacker);
        // Bots have no screen to flash; they turn to face whoever hit them instead
        if (IsBot) { if (brain != null) brain.OnDamaged(attackerPlayer); }
        else RPC_Damaged((float)damage / maxHealth, attackerPlayer != null ? attackerPlayer.transform.position : transform.position, attackerPlayer != null && attackerPlayer != this);

        if (Health > 0) return false;

        Dead = true;

        // Thrown along the killing bullet's path, from the spot it hit. Without a bullet: away from the
        // killer, or backwards for a fall or a suicide.
        Vector3 hitDirection = bulletDirection;
        if (hitDirection == Vector3.zero)
        {
            hitDirection = -transform.forward;
            if (attackerPlayer != null && attackerPlayer != this)
            {
                hitDirection = transform.position - attackerPlayer.transform.position;
                hitDirection.y = 0f;
            }
        }
        if (hitOffset == Vector3.zero) hitOffset = Vector3.up * (headshot ? 0.75f : 0.3f);
        // Harder hits throw the body further: about 3 m/s for a machine gun bullet, 6 m/s for a sniper round
        float knockback = 2f + 4f * Mathf.Clamp01(damage / 800f);

        if (manager != null)
        {
            Vector3 ground = GroundPosition();
            // In Cap Grab the cap leaves the body: knocked flying on a cap shot, gone otherwise
            bool capDrops = manager.CapDrops(ground);
            manager.SpawnRagdoll(transform.position, transform.rotation, Velocity, hitDirection.normalized, hitOffset, knockback, headshot, ActorId, capDrops);
            manager.DropAmmo(ground);
            if (capDrops) manager.DropCap(ground, ActorId, headshot, transform.position + Vector3.up * HeadHeight, hitDirection);
            manager.PlayerKilled(Owner, attacker, weaponName, headshot);
        }

        Runner.Despawn(Object);
        return true;
    }

    // On the victim's own game: screen flash, direction to the attacker and a small flinch
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_Damaged(float fraction, Vector3 attackerPosition, NetworkBool hasAttacker)
    {
        FeedbackUI.Damaged(fraction, hasAttacker ? attackerPosition : (Vector3?)null, transform);
        if (look != null) look.AddRecoil(Random.Range(0.5f, 1.5f), Random.Range(-1f, 1f));
    }

    private void RegenerateHealth()
    {
        if (Health >= maxHealth || !RegenDelayTimer.ExpiredOrNotRunning(Runner)) return;

        float buffer = RegenBuffer + regenPerSecond * Runner.DeltaTime;
        int amount = (int)buffer;
        RegenBuffer = buffer - amount;
        if (amount > 0) Health = Mathf.Min(maxHealth, Health + amount);
    }

    public void EndSpawnProtection()
    {
        if (Object == null || !Object.IsValid || !ProtectionTimer.IsRunning) return;
        ProtectionTimer = TickTimer.None;
    }

    #endregion

    #region Helpers

    // Colours this player's name tag and body by team. Runs again whenever the team list changes.
    private void RefreshTeamVisuals()
    {
        if (teamVisualsVersion == Manager.TeamsVersion) return;
        teamVisualsVersion = Manager.TeamsVersion;

        Manager m = Manager.Instance;
        int team = m != null ? m.GetTeam(ActorId) : -1;
        if (team < 0)
        {
            // No teams: the player's own colour (which the colour blind setting can change mid-match)
            Color own = PlayerCap.ColorFor(ActorId);
            foreach (PlayerCap cap in caps) if (cap != null) cap.SetColor(own);
            foreach (DroneBody drone in drones) if (drone != null) drone.SetColor(own, false);
            if (hands != null) hands.SetColor(own);
            if (satellites != null) satellites.SetColor(own);
            return;
        }

        Color teamColor = Manager.TeamColors[team % 2];
        if (playerUsername != null) playerUsername.color = teamColor;

        Color tint = Color.Lerp(Color.white, teamColor, teamTintStrength);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer body in GetComponentsInChildren<Renderer>(true))
        {
            // Body meshes only: not the name tag, the gun, effects or the cap (coloured below)
            if (!(body is MeshRenderer || body is SkinnedMeshRenderer)) continue;
            if (body.GetComponentInParent<PlayerCap>() != null) continue;
            if (body.GetComponentInParent<DroneBody>() != null || body.GetComponentInParent<DroneHands>() != null || body.GetComponentInParent<GunSatellites>() != null) continue; // coloured below
            if (hoverVisuals != null && hoverVisuals.IsGlow(body)) continue;
            if (body.GetComponent<TextMeshPro>() != null) continue;
            if (weaponParent != null && body.transform.IsChildOf(weaponParent)) continue;

            body.GetPropertyBlock(block);
            block.SetColor("_BaseColor", tint); // URP
            block.SetColor("_Color", tint);     // built-in shaders
            body.SetPropertyBlock(block);
        }

        foreach (PlayerCap cap in caps) if (cap != null) cap.SetColor(teamColor);
        foreach (DroneBody drone in drones) if (drone != null) drone.SetColor(teamColor, true);
        if (hands != null) hands.SetColor(teamColor);
        if (satellites != null) satellites.SetColor(teamColor);
    }

    // The hat this player wears: your own pick, or the one the host has for everyone else (bots included)
    private int WornHat()
    {
        if (IsLocal && !IsBot) return GameSettings.Hat;
        return manager != null ? manager.GetPlayerHat(ActorId) : Hats.Cap;
    }

    // Swaps the hat when it changes: a new pick in the pause menu, or the host's list arriving after
    // this player spawned
    private void RefreshHat()
    {
        int hat = WornHat();
        if (hat != wornHat) PutOnHat(hat);
    }

    // The face this player shows: your own pick, or the one the host has for everyone else (bots included)
    private int WornFace()
    {
        if (IsLocal && !IsBot) return GameSettings.Face;
        return manager != null ? manager.GetPlayerFace(ActorId) : Faces.Classic;
    }

    // Changes the face when it changes: a new pick in the pause menu, or the host's list arriving
    // after this player spawned
    private void RefreshFace()
    {
        int face = WornFace();
        if (face == wornFace) return;
        wornFace = face;
        foreach (DroneBody drone in drones) if (drone != null) drone.SetFace(face);
        // See-through while spawn protected: look again for parts to ghost, so the new face is too
        ghostedGun = null;
    }

    // A hat on both heads (standing and crouching), drawn like the head: hidden from your own camera,
    // visible to everyone else
    private void PutOnHat(int hat)
    {
        wornHat = hat;
        foreach (PlayerCap cap in caps) if (cap != null) Destroy(cap.gameObject);
        Color color = PlayerCap.ColorFor(ActorId);
        caps = new PlayerCap[]
        {
            PlayerCap.Attach(standingColliderHead != null ? standingColliderHead.transform : null, color, false, hat),
            PlayerCap.Attach(crouchingColliderHead != null ? crouchingColliderHead.transform : null, color, false, hat),
        };
        // See-through while spawn protected: look again for parts to ghost, so the new hat is too
        ghostedGun = null;
    }

    // Your own hands (the only part of your body you see) take your team's colour in team mode, like
    // everyone else sees them. The rest of the team colouring is for other players only (above).
    private void RefreshOwnHandsColor()
    {
        if (hands == null || ownHandsVersion == Manager.TeamsVersion) return;
        ownHandsVersion = Manager.TeamsVersion;

        Manager m = Manager.Instance;
        int team = m != null ? m.GetTeam(ActorId) : -1;
        Color own = team >= 0 ? Manager.TeamColors[team % 2] : PlayerCap.ColorFor(ActorId);
        hands.SetColor(own);
        if (satellites != null) satellites.SetColor(own);
    }

    private void OnDestroy()
    {
        if (Local == this) Local = null;
        All.Remove(this);
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
        // Touch controls never lock the mouse, and switching to them lets go of it on purpose
        if (TouchControls.UsingTouch) { wasCursorLocked = false; return; }
        bool locked = Browser.IsPointerLocked;
        if (wasCursorLocked && !locked && !Pause.paused)
        {
            if (pauseMenu == null) pauseMenu = FindAnyObjectByType<Pause>();
            if (pauseMenu != null) pauseMenu.TogglePause();
        }
        wasCursorLocked = locked;
#endif
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

    // A protected player is drawn see-through: by everyone else, and in their own view (their gun
    // and hand-orbs). Each body part's material is swapped for a
    // transparent one (Resources/ProtectionGhost, an asset so builds keep its shader variant) and
    // given the part's own colour and glow at protectionAlpha. Parts drawn with another shader
    // (effects that manage themselves) and text are left alone.
    private void UpdateProtectionGhost()
    {
        if (!IsProtected)
        {
            if (ghosted != null) EndGhost();
            return;
        }

        if (ghostMaterial == null) ghostMaterial = Resources.Load<Material>("ProtectionGhost");
        if (ghostMaterial == null) return;
        if (ghostBlock == null) ghostBlock = new MaterialPropertyBlock();

        // The first frame, and again whenever the gun model changes (a new form brings new parts)
        GameObject gun = weapon != null ? weapon.GunModel : null;
        if (ghosted == null || gun != ghostedGun)
        {
            if (ghosted == null) ghosted = new Dictionary<Renderer, GhostedRenderer>();
            ghostedGun = gun;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (ghosted.ContainsKey(r) || !CanGhost(r)) continue;
                ghosted[r] = new GhostedRenderer { materials = r.sharedMaterials, shadows = r.shadowCastingMode };
                Material[] ghosts = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < ghosts.Length; i++) ghosts[i] = ghostMaterial;
                r.sharedMaterials = ghosts;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // Every frame, as other code may repaint a part (team colours, the gun's glow) meanwhile
        List<Renderer> gone = null;
        foreach (KeyValuePair<Renderer, GhostedRenderer> entry in ghosted)
        {
            if (entry.Key == null) { if (gone == null) gone = new List<Renderer>(); gone.Add(entry.Key); continue; }
            SetGhostAlpha(entry.Key, entry.Value.materials[0], protectionAlpha);
        }
        if (gone != null) foreach (Renderer r in gone) ghosted.Remove(r);
    }

    private static bool CanGhost(Renderer r)
    {
        if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) return false;
        if (r.GetComponent<TextMeshPro>() != null) return false;
        // The gun's floating pieces see to their own (GunSatellites)
        if (r.GetComponentInParent<GunSatellites>() != null) return false;
        // The scope's reticle keeps glowing: it's only seen through the scope, by its owner, and a
        // see-through copy would lose its glow
        if (r.name == DroneHands.ReticleGlowName) return false;
        Material[] materials = r.sharedMaterials;
        if (materials.Length == 0) return false;
        foreach (Material m in materials)
        {
            if (m == null || m.shader != ghostMaterial.shader || m.renderQueue >= 3000) return false;
        }
        return true;
    }

    // The part's own colour and glow (from its property block, else its material) at this alpha
    private void SetGhostAlpha(Renderer r, Material original, float alpha)
    {
        r.GetPropertyBlock(ghostBlock);
        Color color = ghostBlock.HasColor("_BaseColor") ? ghostBlock.GetColor("_BaseColor")
            : original != null && original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : Color.white;
        color.a = alpha;
        ghostBlock.SetColor("_BaseColor", color);
        ghostBlock.SetColor("_Color", color);
        if (!ghostBlock.HasColor("_EmissionColor"))
        {
            bool glows = original != null && original.IsKeywordEnabled("_EMISSION") && original.HasProperty("_EmissionColor");
            ghostBlock.SetColor("_EmissionColor", glows ? original.GetColor("_EmissionColor") : Color.black);
        }
        r.SetPropertyBlock(ghostBlock);
    }

    private void EndGhost()
    {
        foreach (KeyValuePair<Renderer, GhostedRenderer> entry in ghosted)
        {
            Renderer r = entry.Key;
            if (r == null) continue;
            r.sharedMaterials = entry.Value.materials;
            r.shadowCastingMode = entry.Value.shadows;
            SetGhostAlpha(r, entry.Value.materials[0], 1f);
        }
        ghosted = null;
        ghostedGun = null;
    }

    // The body of the given player or bot, or null if they're dead or gone
    public static Player FindPlayer(PlayerRef player)
    {
        if (player == PlayerRef.None) return null;
        foreach (Player p in All)
        {
            if (p.Object != null && p.Object.IsValid && p.Owner == player) return p;
        }
        return null;
    }

    #endregion
}
