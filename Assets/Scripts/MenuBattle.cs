using System.Collections.Generic;
using UnityEngine;

// Three bots fighting in the background of the main menu, behind the player model, so the menu shows
// a bit of the game. Purely for show and local: no network, no physics. Each is a drone like a
// player's (DroneBody, a cap, hand-orbs and a gun with its floating pieces) in its own colour. They
// hover between spots on open ground the camera can see, turn to the nearest rival and fire bursts
// of laser beams that hit or miss. A drone that runs out of health shorts out (KillSpark) and comes
// back a few seconds later somewhere else, with another gun form. Made by MenuScenery.
public class MenuBattle : MonoBehaviour
{
    private const int BotCount = 3;
    private const float MaxHealth = 100f;
    private const float MoveSpeed = 3f;
    private const float Acceleration = 6f;
    private const float TurnRate = 6f;
    private const float RespawnTime = 3f;
    private const float PopInTime = 0.35f;
    // Shots only start once the bot is roughly facing its rival
    private const float FireAngle = 25f;
    // The player prefab's proportions: its root is this far above the feet, the gun this far above that
    private const float FeetToRoot = 0.97f;
    private const float RootToGun = 0.556f;
    // Where the fight is: open ground this far behind the menu model, as wide and deep as this
    private const float ArenaWidth = 16f, ArenaDepth = 8f, SpotSpacing = 1.6f;
    private const int MinSpots = 6;

    private class Bot
    {
        public Transform root, holder;
        public Color color;
        public GameObject gun;
        public Gun form;
        public LineRenderer lineTemplate;
        public DroneHands hands;
        public GunSatellites satellites;
        public DroneBody drone;

        public Vector3 position, velocity, destination;
        public float yaw;
        public float health = MaxHealth;
        public bool dead;
        // In the fight yet (the first spawns go one by one)
        public bool placed;
        public float respawnAt, spawnedAt;
        public float nextShotAt, repathAt;
        public int burstLeft;
        public float phase;
    }

    private readonly List<Vector3> spots = new List<Vector3>();
    private readonly List<Bot> bots = new List<Bot>();
    private Gun[] forms;
    private Camera cam;
    private Transform model;

    public static void Create(Camera cam, Transform model)
    {
        if (cam == null || model == null || FindAnyObjectByType<MenuBattle>() != null) return;
        MenuBattle battle = new GameObject("Menu Battle").AddComponent<MenuBattle>();
        battle.cam = cam;
        battle.model = model;
    }

    private void Start()
    {
        GameObject prefab = Resources.Load<GameObject>("Player");
        Weapon weapon = prefab != null ? prefab.GetComponent<Weapon>() : null;
        if (weapon != null && weapon.loadout != null)
        {
            List<Gun> usable = new List<Gun>();
            foreach (Gun g in weapon.loadout) if (g != null && g.prefab != null) usable.Add(g);
            forms = usable.ToArray();
        }

        FindSpots();
        if (forms == null || forms.Length == 0 || spots.Count < BotCount)
        {
            Destroy(gameObject);
            return;
        }

        for (int i = 0; i < BotCount; i++) bots.Add(BuildBot(i));
        for (int i = 0; i < bots.Count; i++) Respawn(bots[i], true);
    }

    #region Where the fight is

    // A grid of spots on open, flat ground behind the model that the camera can see. If the scenery
    // doesn't give enough (no colliders there, say), the bots hover over the grid regardless.
    private void FindSpots()
    {
        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 feet = model.position - Vector3.up * FeetToRoot;
        float groundY = GroundAt(model.position + Vector3.up * 0.5f, feet.y, 3f, out float y) ? y : feet.y;
        float behind = Mathf.Max(6f, Vector3.Distance(cam.transform.position, model.position) * 0.9f) + ArenaDepth * 0.5f;
        Vector3 centre = feet + forward * behind;
        centre.y = groundY;

        List<Vector3> grid = new List<Vector3>();
        for (float x = -ArenaWidth * 0.5f; x <= ArenaWidth * 0.5f + 0.01f; x += SpotSpacing)
        {
            for (float z = -ArenaDepth * 0.5f; z <= ArenaDepth * 0.5f + 0.01f; z += SpotSpacing)
            {
                Vector3 p = centre + right * x + forward * z;
                if (InView(p)) grid.Add(p);
            }
        }

        foreach (Vector3 p in grid)
        {
            if (!GroundAt(p + Vector3.up * 4f, groundY, 1f, out float ground)) continue;
            Vector3 spot = new Vector3(p.x, ground, p.z);
            if (Physics.CheckCapsule(spot + Vector3.up * 0.5f, spot + Vector3.up * 1.8f, 0.45f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            spots.Add(spot);
        }

        if (spots.Count < MinSpots)
        {
            spots.Clear();
            spots.AddRange(grid);
        }
    }

    // The ground under a point (not the menu model), if it's within tolerance of the height expected
    private bool GroundAt(Vector3 from, float expected, float tolerance, out float height)
    {
        height = expected;
        RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(model)) continue;
            if (hit.distance < best)
            {
                best = hit.distance;
                height = hit.point.y;
                found = true;
            }
        }
        return found && Mathf.Abs(height - expected) <= tolerance;
    }

    // Feet and head both on screen, away from the edges
    private bool InView(Vector3 feet)
    {
        Vector3 low = cam.WorldToViewportPoint(feet);
        Vector3 high = cam.WorldToViewportPoint(feet + Vector3.up * 1.9f);
        return low.z > 3f && high.z > 3f
            && low.x > 0.04f && low.x < 0.96f && low.y > 0.02f
            && high.x > 0.04f && high.x < 0.96f && high.y < 0.92f;
    }

    #endregion

    #region The bots

    // A drone with the player prefab's proportions: a capsule body and a sphere head, dressed
    private Bot BuildBot(int index)
    {
        Bot bot = new Bot();
        bot.color = PlayerCap.ColorFor(index + 2);
        bot.phase = Random.value * 10f;

        bot.root = new GameObject("Menu Bot " + (index + 1)).transform;
        bot.root.SetParent(transform, false);

        Transform body = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
        body.name = "Standing";
        body.SetParent(bot.root, false);
        body.localPosition = new Vector3(0f, -0.34f, 0f);
        body.localScale = new Vector3(1f, 0.61387f, 1f);

        Transform head = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        head.name = "Head";
        head.SetParent(body, false);
        head.localPosition = new Vector3(0f, 1.4498f, 0f);
        head.localScale = new Vector3(0.70853f, 1.1542f, 0.70853f);

        // Only for show: nothing to bump into or shoot at
        DestroyImmediate(body.GetComponent<Collider>());
        DestroyImmediate(head.GetComponent<Collider>());

        // Each with a face of its own, as in a match
        bot.drone = DroneBody.Dress(body, head, bot.color, false, false, Random.Range(0, Faces.Count));
        PlayerCap.Attach(head, bot.color, false, Random.Range(0, Hats.BotHatCount));

        bot.holder = new GameObject("Weapon").transform;
        bot.holder.SetParent(bot.root, false);
        bot.holder.localPosition = new Vector3(-0.031f, RootToGun, 0f);
        return bot;
    }

    // Swaps the bot's gun for this form, with its hand-orbs and floating pieces
    private void Equip(Bot bot, Gun form)
    {
        if (bot.hands != null) Destroy(bot.hands.gameObject);
        if (bot.satellites != null) Destroy(bot.satellites.gameObject);
        if (bot.gun != null) Destroy(bot.gun);

        bot.form = form;
        bot.gun = Instantiate(form.prefab, bot.holder, false);
        bot.gun.name = form.prefab.name;
        foreach (Sway sway in bot.gun.GetComponentsInChildren<Sway>(true)) sway.enabled = false;
        foreach (Collider c in bot.gun.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        bot.lineTemplate = null;
        foreach (LineRenderer line in bot.gun.GetComponentsInChildren<LineRenderer>(true))
        {
            if (bot.lineTemplate == null) bot.lineTemplate = line;
            line.enabled = false;
        }

        bot.hands = DroneHands.AttachToDisplay(bot.root, bot.gun, bot.color);
        bot.satellites = GunSatellites.AttachToDisplay(bot.root, bot.gun, form, bot.color);
        bot.satellites.demoShots = false;
    }

    // Back in at the spot furthest from the others, with a new gun form
    private void Respawn(Bot bot, bool first)
    {
        Vector3 spot = spots[Random.Range(0, spots.Count)];
        float bestDistance = -1f;
        for (int tries = 0; tries < 12; tries++)
        {
            Vector3 candidate = spots[Random.Range(0, spots.Count)];
            float nearest = float.MaxValue;
            foreach (Bot other in bots)
            {
                if (other == bot || other.dead || !other.placed) continue;
                nearest = Mathf.Min(nearest, (other.position - candidate).sqrMagnitude);
            }
            if (nearest > bestDistance)
            {
                bestDistance = nearest;
                spot = candidate;
            }
        }

        Equip(bot, forms[Random.Range(0, forms.Length)]);
        bot.position = spot;
        bot.destination = spot;
        bot.velocity = Vector3.zero;
        bot.health = MaxHealth;
        if (bot.drone != null) bot.drone.SetHealth(1f);
        bot.dead = false;
        bot.placed = true;
        bot.burstLeft = 0;
        bot.spawnedAt = Time.time;
        bot.nextShotAt = Time.time + (first ? Random.Range(1f, 3f) : Random.Range(0.6f, 1.2f));
        bot.repathAt = 0f;
        bot.root.gameObject.SetActive(true);
        Place(bot, Time.time);
    }

    #endregion

    private void Update()
    {
        float now = Time.time;
        float dt = Time.deltaTime;
        foreach (Bot bot in bots)
        {
            if (bot.dead)
            {
                if (now >= bot.respawnAt) Respawn(bot, false);
                continue;
            }

            Bot target = NearestRival(bot);
            Move(bot, target, now, dt);
            Place(bot, now);
            if (target != null) Fight(bot, target, now);
        }
    }

    private Bot NearestRival(Bot bot)
    {
        Bot nearest = null;
        float best = float.MaxValue;
        foreach (Bot other in bots)
        {
            if (other == bot || other.dead) continue;
            float d = (other.position - bot.position).sqrMagnitude;
            if (d < best)
            {
                best = d;
                nearest = other;
            }
        }
        return nearest;
    }

    // Hovers from spot to spot, strafing while it fights, and turns to face its rival
    private void Move(Bot bot, Bot target, float now, float dt)
    {
        Vector3 flat = bot.destination - bot.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.3f || now >= bot.repathAt)
        {
            bot.destination = NextSpot(bot);
            bot.repathAt = now + Random.Range(2f, 4.5f);
            flat = bot.destination - bot.position;
            flat.y = 0f;
        }

        // Slows down while firing the heavier forms, like a player aiming
        float speed = MoveSpeed * (bot.burstLeft > 0 ? 0.6f : 1f);
        Vector3 wanted = flat.sqrMagnitude > 0.01f ? flat.normalized * speed : Vector3.zero;
        bot.velocity = Vector3.MoveTowards(bot.velocity, wanted, Acceleration * dt);
        bot.position += bot.velocity * dt;
        bot.position.y = Mathf.Lerp(bot.position.y, bot.destination.y, 1f - Mathf.Exp(-dt * 4f));

        Vector3 facing = target != null ? target.position - bot.position : bot.velocity;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.01f)
        {
            float wantedYaw = Quaternion.LookRotation(facing).eulerAngles.y;
            bot.yaw = Mathf.LerpAngle(bot.yaw, wantedYaw, 1f - Mathf.Exp(-dt * TurnRate));
        }
    }

    // A spot a few metres away, not on top of another bot
    private Vector3 NextSpot(Bot bot)
    {
        Vector3 best = bot.position;
        float bestScore = float.MinValue;
        for (int tries = 0; tries < 8; tries++)
        {
            Vector3 candidate = spots[Random.Range(0, spots.Count)];
            float distance = Vector3.Distance(candidate, bot.position);
            float score = -Mathf.Abs(distance - 4f);
            foreach (Bot other in bots)
            {
                if (other == bot || other.dead) continue;
                if ((other.position - candidate).sqrMagnitude < 4f) score -= 10f;
            }
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }
        return best;
    }

    // Floats and bobs above its spot, leans into its movement and pops in after respawning
    private void Place(Bot bot, float now)
    {
        float bob = Mathf.Sin((now + bot.phase) * 2.2f) * 0.04f + 0.06f;
        bot.root.position = bot.position + Vector3.up * (FeetToRoot + bob);

        Vector3 local = Quaternion.Euler(0f, -bot.yaw, 0f) * bot.velocity;
        Quaternion lean = Quaternion.Euler(local.z * 2.5f, 0f, -local.x * 2.5f);
        bot.root.rotation = Quaternion.Euler(0f, bot.yaw, 0f) * lean;

        float pop = Mathf.Clamp01((now - bot.spawnedAt) / PopInTime);
        bot.root.localScale = Vector3.one * Mathf.Lerp(0.05f, 1f, Mathf.SmoothStep(0f, 1f, pop));
    }

    #region Shooting

    private void Fight(Bot bot, Bot target, float now)
    {
        // Aim the gun at the rival's body
        Vector3 aimPoint = target.root.position + Vector3.up * 0.3f;
        Vector3 aim = aimPoint - bot.holder.position;
        if (aim.sqrMagnitude > 0.01f) bot.holder.rotation = Quaternion.Slerp(bot.holder.rotation, Quaternion.LookRotation(aim), 1f - Mathf.Exp(-Time.deltaTime * 10f));

        if (now < bot.nextShotAt || now - bot.spawnedAt < PopInTime) return;
        Vector3 flat = aim;
        flat.y = 0f;
        if (Vector3.Angle(bot.root.forward, flat) > FireAngle) return;

        if (bot.burstLeft <= 0) bot.burstLeft = BurstLength(bot.form);
        Shoot(bot, target);
        bot.burstLeft--;
        bot.nextShotAt = bot.burstLeft > 0
            ? now + Mathf.Max(bot.form.fireRate, 0.08f)
            : now + Random.Range(0.9f, 2f) + Mathf.Max(bot.form.fireRate, 0.08f);
    }

    // A burst from the machine gun, a double or triple tap from the pistol, single shots otherwise
    private static int BurstLength(Gun form)
    {
        if (form.pellets > 1) return 1;
        if (form.burst == 1) return Random.Range(5, 10);
        if (form.fireRate < 0.4f) return Random.Range(2, 4);
        return 1;
    }

    private void Shoot(Bot bot, Bot target)
    {
        Vector3 muzzle = bot.lineTemplate != null ? bot.lineTemplate.transform.position : bot.holder.position + bot.holder.forward * 0.6f;
        float distance = Vector3.Distance(muzzle, target.root.position);
        // Close shots land more often than far ones; the sniper is steadier
        float chance = Mathf.Lerp(0.65f, 0.25f, distance / 16f);
        if (bot.form.scopeFov > 0f) chance += 0.15f;

        // The shotgun's pellets: a few beams, not all eight, to keep the background calm
        int beams = bot.form.pellets > 1 ? Mathf.Min(bot.form.pellets, 4) : 1;
        float damage = 0f;
        int pelletsPerBeam = Mathf.Max(1, bot.form.pellets / beams);
        for (int i = 0; i < beams; i++)
        {
            bool hit = Random.value < chance;
            Vector3 end;
            if (hit)
            {
                end = target.root.position + Vector3.up * Random.Range(-0.4f, 0.6f) + Random.insideUnitSphere * 0.15f;
                damage += bot.form.DamageAt(distance, false) * pelletsPerBeam;
            }
            else
            {
                // A miss flies past the rival and stops at whatever is behind
                Vector3 past = target.root.position + Random.insideUnitSphere * 1.3f - muzzle;
                end = muzzle + past.normalized * (distance + 25f);
                if (Physics.Raycast(muzzle, past, out RaycastHit wall, distance + 25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) end = wall.point;
            }
            Beam(bot, muzzle, end, beams > 1);
        }

        if (bot.satellites != null) bot.satellites.PretendShot();
        MuzzleFlash(bot, muzzle);

        if (damage <= 0f) return;
        target.health -= damage;
        // The target winces (and droops when badly hurt); the shooter cheers a kill
        if (target.drone != null)
        {
            target.drone.Hit();
            target.drone.SetHealth(Mathf.Max(0f, target.health) / MaxHealth);
        }
        if (target.health <= 0f)
        {
            Kill(target, (target.root.position - muzzle).normalized);
            if (bot.drone != null) bot.drone.Celebrate();
        }
    }

    private void Beam(Bot bot, Vector3 from, Vector3 to, bool pellet)
    {
        if (bot.lineTemplate == null) return;
        GameObject beamObject = new GameObject("Menu Laser");
        beamObject.layer = Layers.Gun;
        beamObject.transform.SetParent(transform, false);
        LineRenderer beam = Weapon.CopyLine(bot.lineTemplate, beamObject);
        beam.useWorldSpace = true;
        beam.positionCount = 2;
        beam.SetPosition(0, from);
        beam.SetPosition(1, to);
        float width = bot.lineTemplate.widthMultiplier * (pellet ? 0.5f : 1f);
        beam.widthMultiplier = width;
        beam.enabled = true;

        Color color = Color.Lerp(bot.color, Color.white, 0.35f);
        color.a = 0.85f;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        beam.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        beam.SetPropertyBlock(block);

        beamObject.AddComponent<BeamFade>().Begin(beam, width, pellet ? 0.15f : 0.3f);
    }

    private void MuzzleFlash(Bot bot, Vector3 at)
    {
        GameObject flash = new GameObject("Muzzle Flash");
        flash.transform.SetParent(transform, false);
        flash.transform.position = at;
        Light light = flash.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = Color.Lerp(bot.color, Color.white, 0.35f);
        light.range = 4f;
        light.intensity = Random.Range(2f, 3f);
        light.shadows = LightShadows.None;
        Destroy(flash, 0.04f);
    }

    // Shorts out in a spark and a zap, and comes back after a moment
    private void Kill(Bot bot, Vector3 push)
    {
        bot.dead = true;
        bot.respawnAt = Time.time + RespawnTime;
        KillSpark.Spawn(bot.root.position + Vector3.up * 0.3f, push * 3f, bot.color);
        bot.root.gameObject.SetActive(false);
    }

    #endregion
}
