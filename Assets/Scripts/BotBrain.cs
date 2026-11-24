using UnityEngine;
using UnityEngine.AI;

// A bot's mind. Lives on a bot's body on the host only, and once per tick turns what the bot sees
// into the same input a person's game would send (PlayerInputData). Player and Weapon then run that
// input exactly like a person's, so bots move, shoot, reload and change gun form by the same rules.
//
// What it does: heads for the nearest enemy along the map's walkable paths (BotNavigation), and once
// it can see one, turns to aim (with a reaction delay and some aim error that settles the longer it
// tracks), sidesteps, keeps a sensible distance for its gun form and fires when on target, with the odd
// plasma grenade at mid range. On Normal and Hard (BotLogic.Skill.tactical) it also backs off into
// cover to heal when badly hurt, and picks up a nearby ammo box when its energy is low. With the scoped
// form it holds its ground.
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
    // The path stops short of where it was going: there's no walkable way all the way there
    private bool pathIncomplete;
    // The path leads to the enemy being fought, not to the roaming goal
    private bool pathForCombat;
    private Vector3 goal;
    private bool hasGoal;
    private float nextRepathAt;
    // Got as close as it could to an enemy it can't reach: wander instead of hunting until then
    private float wanderUntil;

    // Backing off into cover to heal: where to, and until when at the latest
    private bool retreating;
    private Vector3 coverPoint;
    private float retreatUntil;
    private float nextCoverSearchAt;
    private readonly NavMeshPath coverPath = new NavMeshPath();

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
    // Plasma grenades: not before this time (Time.time)
    private float nextGrenadeAt;
    // Holding the grenade button (cooking) until this time (Time.time)
    private float grenadeHeldUntil;

    private const float BotGrenadeMinRange = 5f;
    private const float BotGrenadeMaxRange = 14f;

    private int lastHealth;
    private Player lastAttacker;

    // How well it plays (the room's bot difficulty)
    private BotLogic.Skill skill = BotLogic.SkillFor(BotLogic.Normal);

    // The layers a bot's view is blocked by: the same as a shot's (scenery), without player bodies
    private int sightMask;

    public void Initialize(Player body)
    {
        player = body;
        weapon = body.GetComponent<Weapon>();
        manager = Manager.Instance;
        skill = BotLogic.SkillFor(manager != null ? manager.BotSkill : BotLogic.Normal);
        yaw = body.transform.eulerAngles.y;
        pitch = 0f;
        lastHealth = body.currentHealth;
        stuckCheckPosition = body.transform.position;
        nextStuckCheckAt = Time.time + 1f;
        // A short pause after spawning, like a person getting their bearings
        nextScanAt = Time.time + Random.Range(0.2f, 0.6f);
        nextGrenadeAt = Time.time + Random.Range(3f, 6f);
        grenadeHeldUntil = 0f;

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
        UpdateRetreat(now);
        if (retreating)
        {
            moveDirection = RetreatMovement(now);
            hasGoal = false;
            sprint = moveDirection != Vector3.zero && !fighting;
        }
        else if (fighting)
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
                float error = BotLogic.AimError(now - targetSince, skill);
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

        yaw = BotLogic.TurnTowards(yaw, wantLook.x, dt, skill.turnSpeed, skill.aimSharpness);
        pitch = Mathf.Clamp(BotLogic.TurnTowards(pitch, wantLook.y, dt, skill.turnSpeed, skill.aimSharpness), -85f, 85f);
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
        // Now and then a plasma grenade at an enemy in the middle distance, where the arc still reaches
        // them, while looking right at them
        if (fighting && !eyeInWall && weapon != null && weapon.GrenadeCount > 0 && now >= nextGrenadeAt
            && targetDistance >= BotGrenadeMinRange && targetDistance <= BotGrenadeMaxRange)
        {
            Vector2 exact = BotLogic.LookAngles(eye, AimPoint(target));
            bool lookingAtThem = Mathf.Abs(Mathf.DeltaAngle(yaw, exact.x)) < 4f && Mathf.Abs(Mathf.DeltaAngle(pitch, exact.y)) < 4f;
            if (lookingAtThem && Random.value < 0.4f)
            {
                // Cooked for a moment, so it doesn't give them the full fuse to get away
                grenadeHeldUntil = now + Random.Range(0.3f, 1.3f);
                nextGrenadeAt = now + Random.Range(8f, 14f);
            }
            // Not this time: think about it again in a moment
            else nextGrenadeAt = now + Random.Range(0.8f, 1.6f);
        }

        // Holding a cooking grenade: keep the button down until it's time to throw (the gun can't
        // fire meanwhile)
        if (now < grenadeHeldUntil)
        {
            data.Buttons.Set(InputButton.Grenade, true);
            trigger = false;
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
            fireAllowedAt = now + Random.Range(skill.reactionMin, skill.reactionMax);
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
        if (distance > skill.sightRange) return false;

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

    // How close a bot gets to a corner of its path before heading for the next one
    private const float CornerReach = 0.5f;
    // How far ahead a sidestep or backing off is checked for walls and ledges
    private const float StepCheck = 1.5f;

    // While fighting: sidestep, and close in or back off to suit the gun. Closing in follows the
    // walkable path to the target, not a straight line: it may be seen (and shot) through glass, or
    // across a gap, that's in the way of walking straight there.
    private Vector3 CombatMovement(float now, float distance, Gun gun)
    {
        Vector3 feet = player.transform.position + Vector3.up * player.bodyBottom;
        Vector3 toTarget = target.transform.position - player.transform.position;
        toTarget.y = 0f;
        Vector3 forward = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : player.transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        // With the scoped form it holds its ground: no closing in across open ground, and mostly
        // standing still between sidesteps, for a steadier aim
        bool scoped = gun != null && gun.scopeFov > 0f;
        if (now >= nextStrafeChangeAt)
        {
            strafeDirection = Random.value < 0.5f ? -1f : 1f;
            if (scoped && Random.value < 0.6f) strafeDirection = 0f;
            nextStrafeChangeAt = now + Random.Range(0.6f, 1.6f);
        }

        float preferred = 18f;
        if (gun != null && gun.pellets > 1) preferred = 4f;
        else if (scoped) preferred = 45f;

        Vector3 travel = Vector3.zero;
        bool closingIn = distance > preferred * 1.3f && !scoped;
        if (closingIn)
        {
            if (!pathForCombat || now >= nextRepathAt)
            {
                PlanPath(feet, target.transform.position + Vector3.up * player.bodyBottom);
                pathForCombat = true;
                nextRepathAt = now + Random.Range(0.4f, 0.6f);
            }
            travel = FollowPath(feet);
        }
        else if (distance < preferred * 0.6f && CanStep(feet, -forward))
        {
            travel = -forward * 0.6f;
        }

        // Don't sidestep off a ledge or into a wall: try the other way, then don't
        Vector3 strafe = right * strafeDirection;
        if (strafeDirection != 0f && !CanStep(feet, strafe))
        {
            strafeDirection = -strafeDirection;
            strafe = -strafe;
            if (!CanStep(feet, strafe)) strafe = Vector3.zero;
        }
        // Going round something to get there: just go
        if (closingIn && travel != Vector3.zero && Vector3.Dot(travel, forward) < 0.5f) strafe = Vector3.zero;

        Vector3 move = strafe + travel;
        // The path's own way is walkable; the two together might not be
        if (move != Vector3.zero && !CanStep(feet, move)) move = travel;
        return move;
    }

    // Badly hurt (tactical bots): find somewhere close that the enemy can't see and wait there for
    // health to come back. Ends once healed, or after a while (health only comes back after a few
    // seconds without being hit, so a bot that keeps getting shot gives up and fights).
    private void UpdateRetreat(float now)
    {
        float health = player.maxHealth > 0 ? (float)player.currentHealth / player.maxHealth : 1f;
        if (retreating)
        {
            if (health >= 0.8f || now >= retreatUntil) retreating = false;
            return;
        }

        if (!skill.tactical || health >= 0.35f || now < nextCoverSearchAt) return;
        Player threat = target != null ? target : lastAttacker;
        if (threat == null || threat.Object == null || !threat.Object.IsValid || threat.IsDead) return;
        nextCoverSearchAt = now + 1f;

        if (FindCover(threat, out coverPoint))
        {
            retreating = true;
            retreatUntil = now + Random.Range(7f, 10f);
            pathForCombat = false;
            hasGoal = false;
            PlanPath(player.transform.position + Vector3.up * player.bodyBottom, coverPoint);
        }
    }

    // Following the path to cover, then staying put
    private Vector3 RetreatMovement(float now)
    {
        Vector3 feet = player.transform.position + Vector3.up * player.bodyBottom;
        if (FlatDistance(feet, coverPoint) < 0.8f) return Vector3.zero;
        return FollowPath(feet);
    }

    // A walkable point a short way off, hidden from the threat's eye, with the shortest walk there
    private bool FindCover(Player threat, out Vector3 cover)
    {
        cover = Vector3.zero;
        Vector3 feet = player.transform.position + Vector3.up * player.bodyBottom;
        Vector3 eyeHeight = Vector3.up * (player.EyePosition.y - feet.y);
        Vector3 threatEye = threat.EyePosition;
        float best = 20f;
        bool found = false;
        for (int i = 0; i < 10; i++)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(3f, 12f);
            if (!BotNavigation.Snap(feet + new Vector3(offset.x, 0f, offset.y), 2f, out Vector3 point)) continue;
            // Hidden: the threat's eye can't see where this bot's eye would be (or its body)
            if (!Physics.Linecast(threatEye, point + eyeHeight, sightMask, QueryTriggerInteraction.Ignore)) continue;
            if (!Physics.Linecast(threatEye, point + eyeHeight * 0.5f, sightMask, QueryTriggerInteraction.Ignore)) continue;
            if (!BotNavigation.FindPath(feet, point, coverPath, out bool complete) || !complete) continue;

            float length = PathLength(coverPath.corners);
            if (length < best)
            {
                best = length;
                cover = point;
                found = true;
            }
        }
        return found;
    }

    private static float PathLength(Vector3[] points)
    {
        float length = 0f;
        for (int i = 1; i < points.Length; i++) length += Vector3.Distance(points[i - 1], points[i]);
        return length;
    }

    private static bool CanStep(Vector3 feet, Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return false;
        return BotNavigation.CanWalkStraight(feet, feet + direction.normalized * StepCheck);
    }

    // Not fighting: follow a path to the last place an enemy was seen, or else to the nearest enemy,
    // or else anywhere
    private Vector3 RoamMovement(float now)
    {
        Vector3 position = player.transform.position;
        Vector3 feet = position + Vector3.up * player.bodyBottom;

        CheckStuck(now, position);

        // At the end of a path that couldn't get all the way: whoever it was heading for can't be
        // reached from here, so it goes elsewhere for a while instead of waiting against the wall
        if (hasGoal && pathIncomplete && AtPathEnd(feet))
        {
            wanderUntil = now + Random.Range(4f, 7f);
            lastSeenAt = -100f;
            hasGoal = false;
        }

        if (!hasGoal || pathForCombat || now >= nextRepathAt || ReachedGoal(feet))
        {
            ChooseGoal(now, feet);
            nextRepathAt = now + Random.Range(1.5f, 2.5f);
            PlanPath(feet, goal);
            pathForCombat = false;
        }

        return FollowPath(feet);
    }

    // A path from the bot's feet to a point. With no navigation mesh yet, straight there; with one
    // but no way there, nowhere until the next repath (walking straight at it would only end
    // against a wall).
    private void PlanPath(Vector3 feet, Vector3 to)
    {
        cornerIndex = 0;
        if (BotNavigation.FindPath(feet, to, path, out bool complete))
        {
            corners = path.corners;
            pathIncomplete = !complete;
        }
        else if (BotNavigation.CanWalkStraight(feet, to))
        {
            corners = new[] { to };
            pathIncomplete = false;
        }
        else
        {
            corners = new Vector3[0];
            pathIncomplete = true;
        }
    }

    // Which way to go along the path. A corner is passed once the bot is close to it, or as soon as
    // the one after it can be walked to in a straight line, so the bot doesn't turn back for a corner
    // it has cut or overshot.
    private Vector3 FollowPath(Vector3 feet)
    {
        if (corners.Length == 0) return Vector3.zero;
        while (cornerIndex < corners.Length - 1 &&
               (FlatDistance(feet, corners[cornerIndex]) < CornerReach ||
                BotNavigation.CanWalkStraight(feet, corners[cornerIndex + 1])))
        {
            cornerIndex++;
        }

        Vector3 toCorner = corners[cornerIndex] - feet;
        toCorner.y = 0f;
        if (toCorner.sqrMagnitude < CornerReach * CornerReach) return Vector3.zero;
        return toCorner.normalized;
    }

    private bool AtPathEnd(Vector3 feet)
    {
        return corners.Length > 0 && cornerIndex >= corners.Length - 1 &&
               FlatDistance(feet, corners[corners.Length - 1]) < 1f;
    }

    private void ChooseGoal(float now, Vector3 feet)
    {
        hasGoal = true;

        // Low on energy with an ammo box close by: grab it first
        if (skill.tactical && weapon != null && weapon.EnergyFraction < 0.5f &&
            manager.NearestPickup(feet, 20f, out Vector3 box))
        {
            goal = box;
            return;
        }

        if (now >= wanderUntil)
        {
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
        }

        // Nobody to fight (or nobody it can get to): wander to a spawn point or somewhere near by
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
                wanderUntil = now + 3f;
                PlanPath(position + Vector3.up * player.bodyBottom, goal);
                pathForCombat = false;
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
