using System.Collections.Generic;
using UnityEngine;
using Fusion;

// Plasma grenades. The host flies each one, decides who it sticks to and deals the blast; every client
// shows its own copy (PlasmaGrenade) and the host corrects it when it bounces, sticks or goes off.
// Like ammo boxes, they're not network objects, so nothing needs to be registered as a prefab.
// A grenade is named by the thrower's body (its network id) and how many that body has thrown, so the
// thrower's own early copy and the host's messages about it can be matched up.
public partial class Manager
{
    public const string PlasmaWeaponName = "PLASMA";

    // Host only: one grenade in the air or stuck to someone
    private class LiveGrenade
    {
        public long key;
        public int body, number;
        public PlayerRef owner;
        public int ownerActor;
        public Vector3 position, velocity;
        public float fuse;
        public Player stuckTo;
        public Vector3 stuckOffset;
        public bool wasStuck;
    }

    private readonly List<LiveGrenade> liveGrenades = new List<LiveGrenade>();
    // Every client's copies, by key
    private readonly Dictionary<long, PlasmaGrenade> grenadeVisuals = new Dictionary<long, PlasmaGrenade>();

    #region Host

    // Host only: a player threw a grenade with 'fuse' seconds left (less than the full fuse if they
    // cooked it), or dropped it by dying with it in the hand (no velocity)
    public void ThrowGrenade(PlayerRef owner, int ownerActor, int body, int number, Vector3 origin, Vector3 velocity, float fuse)
    {
        if (!HasStateAuthority) return;
        fuse = Mathf.Max(0f, fuse);
        liveGrenades.Add(new LiveGrenade
        {
            key = PlasmaGrenade.MakeKey(body, number),
            body = body,
            number = number,
            owner = owner,
            ownerActor = ownerActor,
            position = origin,
            velocity = velocity,
            fuse = fuse,
        });
        RPC_GrenadeThrown(body, number, ownerActor, origin, velocity, fuse);
    }

    // Host only: a player held a grenade for its whole fuse. It goes off in their hand, as if stuck
    // to them.
    public void GrenadeOffInHand(Player holder, int body, int number, Vector3 position)
    {
        if (!HasStateAuthority || holder == null) return;
        Detonate(new LiveGrenade
        {
            key = PlasmaGrenade.MakeKey(body, number),
            body = body,
            number = number,
            owner = holder.Owner,
            ownerActor = holder.ActorId,
            position = position,
            stuckTo = holder,
        });
    }

    // Host only, every tick: flies, sticks and sets off every grenade
    private void UpdateGrenades()
    {
        if (liveGrenades.Count == 0) return;
        float dt = Runner.DeltaTime;

        for (int i = liveGrenades.Count - 1; i >= 0; i--)
        {
            LiveGrenade g = liveGrenades[i];
            g.fuse -= dt;

            if (g.stuckTo != null)
            {
                if (g.stuckTo.Object == null || !g.stuckTo.Object.IsValid || g.stuckTo.IsDead)
                {
                    // Whoever it was stuck to died first: it drops where it was
                    g.stuckTo = null;
                    g.wasStuck = true;
                    g.velocity = Vector3.zero;
                }
                else g.position = g.stuckTo.transform.TransformPoint(g.stuckOffset);
            }
            else
            {
                Vector3 from = g.position;
                float impact = PlasmaGrenade.Step(ref g.position, ref g.velocity, dt);

                if (!g.wasStuck && FindStickTarget(g, from, g.position, out Player target, out Vector3 point))
                {
                    g.stuckTo = target;
                    g.position = point;
                    g.stuckOffset = target.transform.InverseTransformPoint(point);
                    g.velocity = Vector3.zero;
                    g.fuse = PlasmaGrenadeRules.FuseAfterSticking(g.fuse);
                    RPC_GrenadeStuck(g.body, g.number, g.ownerActor, target.ActorId, g.stuckOffset, g.fuse);
                }
                // Only real bounces are sent; sliding along the floor plays out the same everywhere
                else if (impact >= PlasmaGrenadeRules.MinBounceSpeed)
                {
                    RPC_GrenadeBounced(g.body, g.number, g.position, g.velocity);
                }

                if (g.position.y < killHeight)
                {
                    liveGrenades.RemoveAt(i);
                    RPC_GrenadeGone(g.body, g.number);
                    continue;
                }
            }

            if (g.fuse <= 0f)
            {
                liveGrenades.RemoveAt(i);
                Detonate(g);
            }
        }
    }

    // The first enemy the grenade touched on its way from 'from' to 'to'. It passes through its
    // thrower, teammates, dead players and anyone still spawn protected.
    private bool FindStickTarget(LiveGrenade g, Vector3 from, Vector3 to, out Player target, out Vector3 point)
    {
        target = null;
        point = to;
        float best = float.MaxValue;
        foreach (Player p in Player.All)
        {
            if (p == null || p.Object == null || !p.Object.IsValid || p.IsDead || p.IsProtected) continue;
            if (!AreEnemies(g.ownerActor, p.ActorId)) continue;

            p.BodyCapsule(out Vector3 bottom, out Vector3 top, out float radius);
            if (!PlasmaGrenadeRules.TouchesCapsule(from, to, bottom, top, radius, out Vector3 stick)) continue;

            float distance = (stick - from).sqrMagnitude;
            if (distance >= best) continue;
            best = distance;
            target = p;
            point = stick;
        }
        return target != null;
    }

    // Host only: the blast. Whoever it's stuck to dies; everyone else nearby, in the open, takes
    // damage by distance. Teammates are safe, the thrower isn't.
    private void Detonate(LiveGrenade g)
    {
        Vector3 center = g.position;
        RPC_GrenadeExploded(g.body, g.number, g.ownerActor, center);
        OfficeGlass.Blast(center, PlasmaGrenadeRules.BlastRadius * 0.6f);

        Player thrower = Player.FindPlayer(g.owner);
        // Bodies are removed as they die, so go through a copy
        blastTargets.Clear();
        blastTargets.AddRange(Player.All);
        foreach (Player p in blastTargets)
        {
            if (p == null || p.Object == null || !p.Object.IsValid || p.IsDead) continue;
            bool stuck = p == g.stuckTo;
            if (!stuck && p.ActorId != g.ownerActor && !AreEnemies(g.ownerActor, p.ActorId)) continue;

            p.BodyCapsule(out Vector3 bottom, out Vector3 top, out float radius);
            PlasmaGrenadeRules.SegmentDistance(center, center, bottom, top, out _, out Vector3 nearest);
            float distance = Mathf.Max(0f, Vector3.Distance(center, nearest) - radius);

            int damage = stuck ? PlasmaGrenadeRules.StuckDamage : PlasmaGrenadeRules.BlastDamage(distance);
            if (damage <= 0) continue;
            // Walls shield you (glass doesn't: it shatters)
            if (!stuck && Physics.Linecast(center, nearest, PlasmaGrenade.WorldMask & ~(1 << Layers.Glass), QueryTriggerInteraction.Ignore)) continue;

            // Thrown up and away from the blast
            Vector3 away = nearest - center;
            away.y = Mathf.Max(away.y, 0f);
            Vector3 direction = (away.normalized + Vector3.up * 0.6f).normalized;
            Vector3 hitOffset = stuck ? center - p.transform.position : default;

            bool wasProtected = p.IsProtected;
            bool killed = p.TakeDamage(damage, g.owner, PlasmaWeaponName, false, direction, hitOffset);
            if (!wasProtected && thrower != null && thrower != p && !thrower.IsBot)
            {
                Weapon weapon = thrower.GetComponent<Weapon>();
                if (weapon != null) weapon.ConfirmHit(killed);
            }
        }
        blastTargets.Clear();
    }
    private static readonly List<Player> blastTargets = new List<Player>();

    #endregion

    #region Everyone

    // The thrower's own game: shows the grenade straight away instead of waiting for the host
    public void PredictGrenade(int body, int number, int ownerActor, Vector3 origin, Vector3 velocity, float fuse)
    {
        if (Runner == null) return;
        long key = PlasmaGrenade.MakeKey(body, number);
        if (grenadeVisuals.ContainsKey(key)) return;
        grenadeVisuals[key] = PlasmaGrenade.Create(key, ownerActor, origin, velocity, Runner.DeltaTime, fuse);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_GrenadeThrown(int body, int number, int ownerActor, Vector3 origin, Vector3 velocity, float fuse)
    {
        long key = PlasmaGrenade.MakeKey(body, number);
        // The thrower already has its own copy, a little ahead of the host's: leave it be
        if (grenadeVisuals.TryGetValue(key, out PlasmaGrenade existing) && existing != null) return;
        grenadeVisuals[key] = PlasmaGrenade.Create(key, ownerActor, origin, velocity, Runner.DeltaTime, fuse);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_GrenadeBounced(int body, int number, Vector3 position, Vector3 velocity)
    {
        PlasmaGrenade grenade = GrenadeVisual(body, number);
        if (grenade != null) grenade.Correct(position, velocity);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_GrenadeStuck(int body, int number, int ownerActor, int victimActor, Vector3 offset, float fuse)
    {
        Player victim = null;
        foreach (Player p in Player.All)
        {
            if (p != null && p.ActorId == victimActor) { victim = p; break; }
        }

        PlasmaGrenade grenade = GrenadeVisual(body, number);
        if (grenade != null) grenade.StickTo(victim != null ? victim.transform : null, offset, fuse);

        int me = LocalActor;
        if (victimActor == me) ShowMessage("YOU'RE STUCK!");
        else if (ownerActor == me) ShowMessage("STUCK!");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_GrenadeExploded(int body, int number, int ownerActor, Vector3 position)
    {
        long key = PlasmaGrenade.MakeKey(body, number);
        PlasmaGrenade grenade = GrenadeVisual(body, number);
        grenadeVisuals.Remove(key);
        if (grenade != null) grenade.Explode(position);
        // Never thrown (it went off in the hand), or never seen flying (joined just now, or a lost
        // message): still see and hear the blast
        else PlasmaBlast.Spawn(position, PlasmaGrenade.GlowColor(ownerActor));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_GrenadeGone(int body, int number)
    {
        long key = PlasmaGrenade.MakeKey(body, number);
        PlasmaGrenade grenade = GrenadeVisual(body, number);
        grenadeVisuals.Remove(key);
        if (grenade != null) Destroy(grenade.gameObject);
    }

    private PlasmaGrenade GrenadeVisual(int body, int number)
    {
        long key = PlasmaGrenade.MakeKey(body, number);
        if (grenadeVisuals.TryGetValue(key, out PlasmaGrenade grenade) && grenade != null) return grenade;
        grenadeVisuals.Remove(key);
        return null;
    }

    #endregion
}
