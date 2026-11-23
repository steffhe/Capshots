using UnityEngine;
using UnityEngine.AI;

// A bot's mind. Lives on a bot's body on the host only, and once per tick turns what the bot sees
// into the same input a person's game would send (PlayerInputData). Player and Weapon then run that
// input exactly like a person's, so bots move, shoot, reload and change gun form by the same rules.
//
// What it does: heads for the nearest enemy along the map's walkable paths (BotNavigation), and once
// it can see one, turns to aim (with a reaction delay and some aim error that settles the longer it
// tracks), sidesteps, keeps a sensible distance for its gun form and fires when on target.
[DisallowMultipleComponent]
public class BotBrain : MonoBehaviour
{
    private Player player;
    private Weapon weapon;
    private Manager manager;

    // The input for the tick last asked about. Player and Weapon both ask once per tick.
    private int inputTick = -1;
    private PlayerInputData input;

    // Where the bot looks
    private float yaw, pitch;

    // The enemy being fought, and since when it has been in sight
    private Player target;
    private float targetSince;
    private float fireAllowedAt;
    private float nextScanAt;
    private Vector3 aimOffset;
    private float nextAimOffsetAt;
    private Vector3 lastSeenPosition;
    private float lastSeenAt = -100f;

    // Moving: the path being followed and where to
    private readonly NavMeshPath path = new NavMeshPath();
    private Vector3[] corners = new Vector3[0];
    private int cornerIndex;
    private Vector3 goal;
    private bool hasGoal;
    private float nextRepathAt;

    // Sidestepping while fighting
    private float strafeDirection = 1f;
    private float nextStrafeChangeAt;

    // Getting unstuck
    private Vector3 stuckCheckPosition;
    private float nextStuckCheckAt;
    private int stuckCount;
    private bool jumpNextTick;

    // Gun handling
    private float nextGunChangeAt;
    private bool triggerWasDown;
    private bool reloadNextTick;

    private int lastHealth;
    private Player lastAttacker;

    // The layers a bot's view is blocked by: the same as a shot's (scenery), without player bodies
    private int sightMask;

    public void Initialize(Player body)
    {
        player = body;
        weapon = body.GetComponent<Weapon>();
        manager = Manager.Instance;
        yaw = body.transform.eulerAngles.y;
        pitch = 0f;
        lastHealth = body.currentHealth;
        stuckCheckPosition = body.transform.position;
        nextStuckCheckAt = Time.time + 1f;
        // A short pause after spawning, like a person getting their bearings
        nextScanAt = Time.time + Random.Range(0.2f, 0.6f);

        int shootable = weapon != null ? weapon.canBeShot.value : ~0;
        sightMask = shootable & ~((1 << Layers.LocalPlayer) | (1 << Layers.RemotePlayer) | (1 << Layers.Hitbox) | (1 << Layers.Glass));
    }

    // Host only: this tick's input, worked out on the first call of the tick
    public PlayerInputData InputForTick(int tick, float deltaTime)
    {
        if (tick != inputTick)
        {
            inputTick = tick;
            input = Think(deltaTime);
        }
        return input;
    }

    // Host only: someone hit this bot. It turns to fight back if it can see them.
    public void OnDamaged(Player attacker)
    {
        if (attacker != null && attacker != player) lastAttacker = attacker;
    }

    private PlayerInputData Think(float dt)
    {
        PlayerInputData data = default;
        if (player == null || manager == null)
        {
            data.Yaw = yaw;
            data.Pitch = pitch;
            return data;
        }

        float now = Time.time;
        Vector3 eye = player.EyePosition;

        UpdateTarget(now, eye);
        bool fighting = target != null;
        float targetDistance = fighting ? Vector3.Distance(eye, AimPoint(target)) : -1f;

        // Gun form for the distance (not too often: changing form takes half a second)
        if (weapon != null && now >= nextGunChangeAt)
        {
            int wanted = BotLogic.ChooseGun(weapon.loadout, targetDistance);
            if (wanted != weapon.EquippedIndex)
            {
                data.WeaponSlot = (byte)(wanted + 1);
                nextGunChangeAt = now + Random.Range(2.5f, 4f);
            }
        }
        Gun gun = weapon != null ? weapon.CurrentGun : null;

        // Where to go and which way to move
        Vector3 moveDirection = Vector3.zero;
        bool sprint = false;
        if (fighting)
        {
            moveDirection = CombatMovement(now, targetDistance, gun);
            hasGoal = false;
        }
        else
        {
            moveDirection = RoamMovement(now);
            sprint = moveDirection != Vector3.zero;
        }

        // Where to look: at the target, or the way the bot is going
        Vector2 wantLook;
        if (fighting)
        {
            if (now >= nextAimOffsetAt)
            {
                float error = BotLogic.AimError(now - targetSince);
                float metres = Mathf.Tan(error * Mathf.Deg2Rad) * Mathf.Max(targetDistance, 1f);
                aimOffset = Random.insideUnitSphere * metres;
                nextAimOffsetAt = now + Random.Range(0.25f, 0.5f);
            }
            wantLook = BotLogic.LookAngles(eye, AimPoint(target) + aimOffset);
        }
        else if (moveDirection != Vector3.zero)
        {
            wantLook = new Vector2(Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg, 0f);
        }
        else wantLook = new Vector2(yaw, 0f);

        yaw = BotLogic.TurnTowards(yaw, wantLook.x, dt);
        pitch = Mathf.Clamp(BotLogic.TurnTowards(pitch, wantLook.y, dt), -85f, 85f);
        data.Yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
        data.Pitch = pitch;

        data.Move = BotLogic.MoveInput(yaw, moveDirection);
        // Sprinting only works going forwards
        sprint &= data.Move.y > 0.7f;
        data.Buttons.Set(InputButton.Sprint, sprint);

        if (jumpNextTick)
        {
            data.Buttons.Set(InputButton.Jump, true);
            jumpNextTick = false;
        }

        // Shooting
        bool trigger = false;
        bool aim = false;
        // Never from inside a wall: the shot would start inside it and go straight through
        bool eyeInWall = Physics.CheckSphere(eye, EyeClearance, sightMask, QueryTriggerInteraction.Ignore);
        if (fighting && gun != null && now >= fireAllowedAt && !eyeInWall)
        {
            Vector2 exact = BotLogic.LookAngles(eye, AimPoint(target));
            float yawError = Mathf.DeltaAngle(yaw, exact.x);
            float pitchError = Mathf.DeltaAngle(pitch, exact.y);
            if (BotLogic.OnTarget(yawError, pitchError, targetDistance))
            {
                // Automatic guns hold the trigger; the others need a fresh pull for each shot
                trigger = gun.burst == 1 || !triggerWasDown;
            }
            // Scoped and far away: aim down the sights (steadier, slower)
            aim = gun.scopeFov > 0f && targetDistance > 20f;
        }
        triggerWasDown = trigger;
        data.Buttons.Set(InputButton.Fire, trigger);
        data.Buttons.Set(InputButton.Aim, aim);

        // Top up the energy between fights
        if (!fighting && weapon != null && weapon.EnergyFraction < 0.3f && !reloadNextTick)
        {
            data.Buttons.Set(InputButton.Reload, true);
            reloadNextTick = true;
        }
        else reloadNextTick = false;

        return data;
    }

    #region Seeing enemies

    // Where on a player a bot aims: the middle of the body, a little below the head
    private static Vector3 AimPoint(Player p)
    {
        return p.EyePosition + Vector3.down * 0.45f;
    }

    private void UpdateTarget(float now, Vector3 eye)
    {
        // Lose the target the moment it's gone; check it can still be seen a few times a second
        if (target != null && (target.Object == null || !target.Object.IsValid || target.IsDead)) target = null;

        bool hurt = player.currentHealth < lastHealth;
        lastHealth = player.currentHealth;

        if (now < nextScanAt && !hurt) return;
        nextScanAt = now + 0.2f;

        // Keep fighting the current target while it's in sight
        if (target != null)
        {
            if (CanSee(eye, target, true))
            {
                lastSeenPosition = target.transform.position;
                lastSeenAt = now;
                return;
            }
            target = null;
        }

        Player best = null;
        float bestDistance = float.MaxValue;
        foreach (Player p in Player.All)
        {
            // Spawn-protected players can't be hurt, so they aren't worth starting a fight with
            if (!IsEnemy(p) || p.IsProtected) continue;
            // Being shot makes a bot look around, so it notices the attacker wherever they are
            bool anyDirection = hurt && p == lastAttacker;
            if (!CanSee(eye, p, anyDirection)) continue;

            float distance = Vector3.Distance(eye, p.EyePosition);
            if (p == lastAttacker && hurt) distance *= 0.25f;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = p;
            }
        }

        if (best != null)
        {
            target = best;
            targetSince = now;
            fireAllowedAt = now + Random.Range(BotLogic.ReactionTimeMin, BotLogic.ReactionTimeMax);
            nextAimOffsetAt = 0f;
            lastSeenPosition = best.transform.position;
            lastSeenAt = now;
        }
    }

    private bool IsEnemy(Player p)
    {
        if (p == null || p == player || p.Object == null || !p.Object.IsValid || p.IsDead) return false;
        return manager.AreEnemies(player.ActorId, p.ActorId);
    }

    // Line of sight from the bot's eye to a player, within sight range. Unless anyDirection is set,
    // only players in front of the bot (or very close) are noticed.
    private bool CanSee(Vector3 eye, Player p, bool anyDirection)
    {
        Vector3 point = AimPoint(p);
        Vector3 toTarget = point - eye;
        float distance = toTarget.magnitude;
        if (distance > BotLogic.SightRange) return false;

        if (!anyDirection && distance > BotLogic.NoticeRange)
        {
            Vector3 facing = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
            if (Vector3.Angle(facing, flat) > BotLogic.SightHalfAngle) return false;
        }

        // A ray that starts inside a collider doesn't hit it. So with its eye inside or touching a
        // wall (pressed up against it, gun poking through), the bot would see, and shoot, straight
        // through the wall. It sees nothing from there.
        if (Physics.CheckSphere(eye, EyeClearance, sightMask, QueryTriggerInteraction.Ignore)) return false;

        // Clear to the body, or failing that to the head...
        bool clear = !Physics.Linecast(eye, point, sightMask, QueryTriggerInteraction.Ignore) ||
                     !Physics.Linecast(eye, p.EyePosition, sightMask, QueryTriggerInteraction.Ignore);
        if (!clear) return false;

        // ...and the same line the other way, from the target's eye. This also catches walls the
        // first check started inside of: the bot can only see someone who could see it back.
        return !Physics.Linecast(p.EyePosition, eye, sightMask, QueryTriggerInteraction.Ignore);
    }

    // How close to the eye solid scenery may be before the bot counts as looking from inside it
    private const float EyeClearance = 0.12f;

    #endregion

    #region Moving

    // While fighting: sidestep, and close in or back off to suit the gun
    private Vector3 CombatMovement(float now, float distance, Gun gun)
    {
        Vector3 toTarget = target.transform.position - player.transform.position;
        toTarget.y = 0f;
        Vector3 forward = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : player.transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        if (now >= nextStrafeChangeAt)
        {
            strafeDirection = Random.value < 0.5f ? -1f : 1f;
            nextStrafeChangeAt = now + Random.Range(0.6f, 1.6f);
        }

        float preferred = 18f;
        if (gun != null && gun.pellets > 1) preferred = 4f;
        else if (gun != null && gun.scopeFov > 0f) preferred = 45f;

        float approach = 0f;
        if (distance > preferred * 1.3f) approach = 1f;
        else if (distance < preferred * 0.6f) approach = -0.6f;

        Vector3 move = right * strafeDirection + forward * approach;

        // Don't sidestep off a ledge or into a wall: try the other way, then just stand
        Vector3 feet = player.transform.position + Vector3.up * player.bodyBottom;
        Vector3 ahead = feet + move.normalized * 1.5f;
        if (!BotNavigation.CanWalkStraight(feet, ahead))
        {
            strafeDirection = -strafeDirection;
            move = right * strafeDirection + forward * approach;
            ahead = feet + move.normalized * 1.5f;
            if (!BotNavigation.CanWalkStraight(feet, ahead)) move = forward * Mathf.Max(0f, approach);
        }
        return move;
    }

    // Not fighting: follow a path to the last place an enemy was seen, or else to the nearest enemy,
    // or else anywhere
    private Vector3 RoamMovement(float now)
    {
        Vector3 position = player.transform.position;
        Vector3 feet = position + Vector3.up * player.bodyBottom;

        CheckStuck(now, position);

        if (!hasGoal || now >= nextRepathAt || ReachedGoal(feet))
        {
            ChooseGoal(now, feet);
            nextRepathAt = now + Random.Range(1.5f, 2.5f);
            corners = BotNavigation.FindPath(feet, goal, path) ? path.corners : new[] { goal };
            cornerIndex = 0;
        }

        // The next corner of the path that isn't right underfoot
        while (cornerIndex < corners.Length - 1 && FlatDistance(feet, corners[cornerIndex]) < 0.8f) cornerIndex++;
        if (corners.Length == 0) return Vector3.zero;

        Vector3 toCorner = corners[cornerIndex] - feet;
        toCorner.y = 0f;
        if (toCorner.sqrMagnitude < 0.25f) return Vector3.zero;
        return toCorner.normalized;
    }

    private void ChooseGoal(float now, Vector3 feet)
    {
        hasGoal = true;

        // Go and check where an enemy was last seen
        if (now - lastSeenAt < 6f && FlatDistance(feet, lastSeenPosition) > 2f)
        {
            goal = lastSeenPosition;
            return;
        }

        // Hunt the nearest enemy, wherever they are
        Player nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Player p in Player.All)
        {
            if (!IsEnemy(p)) continue;
            float d = Vector3.Distance(feet, p.transform.position);
            if (d < nearestDistance) { nearestDistance = d; nearest = p; }
        }
        if (nearest != null)
        {
            goal = nearest.transform.position + Vector3.up * player.bodyBottom;
            return;
        }

        // Nobody to fight: wander to a spawn point or somewhere near by
        Transform[] spawns = manager.spawnPoints;
        if (spawns != null && spawns.Length > 0 && Random.value < 0.6f)
        {
            Transform spawn = spawns[Random.Range(0, spawns.Length)];
            if (spawn != null) { goal = spawn.position; return; }
        }
        if (!BotNavigation.RandomPoint(feet, 25f, out goal)) goal = feet + Random.insideUnitSphere * 10f;
    }

    private bool ReachedGoal(Vector3 feet)
    {
        return FlatDistance(feet, goal) < 1.5f;
    }

    // Barely moved in the last second while trying to: jump, and on the second time pick a new goal
    private void CheckStuck(float now, Vector3 position)
    {
        if (now < nextStuckCheckAt) return;
        nextStuckCheckAt = now + 1f;

        bool moved = FlatDistance(position, stuckCheckPosition) > 0.6f;
        stuckCheckPosition = position;
        if (moved || !hasGoal)
        {
            stuckCount = 0;
            return;
        }

        stuckCount++;
        jumpNextTick = true;
        if (stuckCount >= 2)
        {
            stuckCount = 0;
            hasGoal = false;
            lastSeenAt = -100f;
            if (BotNavigation.RandomPoint(position, 12f, out Vector3 away))
            {
                goal = away;
                hasGoal = true;
                nextRepathAt = now + 3f;
                Vector3 feet = position + Vector3.up * player.bodyBottom;
                corners = BotNavigation.FindPath(feet, goal, path) ? path.corners : new[] { goal };
                cornerIndex = 0;
            }
        }
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    #endregion
}
