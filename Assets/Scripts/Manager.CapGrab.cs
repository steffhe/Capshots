using System.Collections.Generic;
using Fusion;
using UnityEngine;

// Cap Grab: free for all, but a kill doesn't score on its own. The victim's cap comes off (it drops
// where they died, or on a cap shot flies off their head and lands nearby: CapPickup) and whoever walks over it first gets it: an enemy's cap scores a point, your
// own cap back denies it to them. Caps vanish after a while. The score is kept in the scoreboard's
// kills (shown as CAPS), so the win checks, sorting and awards work unchanged.
//
// Like the ammo boxes, every client shows its own copy of each cap; the host decides who gets it.
public partial class Manager
{
    public const float CapLifetime = 15f;
    public int xpPerCap = 50;
    public int xpPerDeny = 25;

    public bool IsCapGrab => mode == MatchMode.CapGrab || mode == MatchMode.TeamCapGrab;
    // What the score counts, for the HUD, scoreboard and results
    public string ScoreWord => IsCapGrab ? "CAPS" : "KILLS";

    private readonly Dictionary<int, CapPickup> caps = new Dictionary<int, CapPickup>();
    // Host only: when each cap vanishes
    private readonly Dictionary<int, float> capExpiry = new Dictionary<int, float>();
    private int capCounter;

    // Host only: whether a player dying here drops their cap (Cap Grab, the match not over, and not
    // fallen off the map, where nobody could reach it)
    public bool CapDrops(Vector3 position)
    {
        return IsCapGrab && HasStateAuthority && State != GameState.Ending && position.y >= killHeight + 2f;
    }

    // Host only: a player died here; in Cap Grab their cap drops for everyone. On an ordinary kill it
    // just appears on the ground where they fell (the body has none). On a cap shot it's knocked off
    // their head: it flies up from head (along the shot's direction, a little) and lands as the pickup.
    public void DropCap(Vector3 position, int ownerActor, bool capshot = false, Vector3 head = default, Vector3 shotDirection = default)
    {
        if (!CapDrops(position)) return;
        if (capshot) position = CapLanding(position, shotDirection);
        int id = ++capCounter;
        // A flying cap can't be taken until it lands, and its time on the ground starts then
        capExpiry[id] = Runner.SimulationTime + CapLifetime + (capshot ? CapPickup.FlightTime : 0f);
        RPC_SpawnCap(id, position, ownerActor, (byte)GetPlayerHat(ownerActor), capshot, head);
    }

    // Where a knocked-off cap comes down: a little way on along the shot, if there's open floor there
    // at about the same height, else where its owner fell
    private Vector3 CapLanding(Vector3 ground, Vector3 shotDirection)
    {
        Vector3 flat = new Vector3(shotDirection.x, 0f, shotDirection.z);
        if (flat.sqrMagnitude < 0.001f) return ground;
        flat.Normalize();
        const float distance = 1.6f;
        Vector3 from = ground + Vector3.up * 1f;
        // The world only: not players, their guns or hitboxes (the victim's body is still here)
        int world = Physics.DefaultRaycastLayers & ~((1 << Layers.LocalPlayer) | (1 << Layers.RemotePlayer) | (1 << Layers.Gun) | (1 << Layers.LocalGun) | (1 << Layers.Hitbox));
        // Something in the way (a wall, a desk, glass): it drops where they fell
        if (Physics.SphereCast(from, 0.2f, flat, out _, distance, world, QueryTriggerInteraction.Ignore)) return ground;
        Vector3 above = from + flat * distance;
        if (!Physics.Raycast(above, Vector3.down, out RaycastHit hit, 3f, world, QueryTriggerInteraction.Ignore)) return ground;
        // Off a ledge or up onto something: not somewhere to walk over easily
        if (Mathf.Abs(hit.point.y + 0.4f - ground.y) > 0.5f) return ground;
        return hit.point + Vector3.up * 0.4f;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnCap(int id, Vector3 position, int ownerActor, byte hat, NetworkBool flies, Vector3 from)
    {
        if (caps.ContainsKey(id)) return;
        // Yours (or, in teams, a teammate's): one to get back before the other side does
        bool ours = ownerActor == LocalActor || SameTeam(ownerActor, LocalActor);
        CapPickup cap = CapPickup.Create(id, position, ownerActor, hat, PlayerCap.ColorFor(ownerActor), CapLifetime, ours, flies ? from : (Vector3?)null);
        caps[id] = cap;
        // Your own cap shot off: the death camera watches the cap fly instead of the head
        if (flies && ownerActor == LocalActor) DeathCam.Follow(cap.Pivot);
    }

    // Host only, every tick: the first living player on a cap takes it
    private void CheckCaps()
    {
        if (capExpiry.Count == 0) return;
        float now = Runner.SimulationTime;
        List<int> done = null;

        foreach (KeyValuePair<int, float> entry in capExpiry)
        {
            int id = entry.Key;
            if (now >= entry.Value || !caps.TryGetValue(id, out CapPickup cap) || cap == null)
            {
                if (done == null) done = new List<int>();
                done.Add(id);
                continue;
            }
            // Still flying off a head
            if (!cap.Landed) continue;

            foreach (Player p in Player.All)
            {
                if (p == null || p.Object == null || !p.Object.IsValid || p.IsDead) continue;
                if (Vector3.Distance(p.transform.position, cap.BasePosition) > CapPickup.PickupRadius) continue;

                // Its owner, or in teams a teammate, takes it back: denied
                bool denied = p.ActorId == cap.OwnerActor || SameTeam(p.ActorId, cap.OwnerActor);
                // Only a match being played counts; in the warmup a cap is just picked up
                if (!denied && State == GameState.Playing)
                {
                    ChangeStats(p.Owner, 1, 0);
                    ScoreCheck();
                }
                if (done == null) done = new List<int>();
                done.Add(id);
                RPC_CapTaken(id, p.ActorId, cap.OwnerActor, denied);
                break;
            }
        }

        if (done != null) foreach (int id in done) { capExpiry.Remove(id); RemoveCap(id); }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_CapTaken(int id, int takerActor, int ownerActor, NetworkBool denied)
    {
        if (caps.TryGetValue(id, out CapPickup cap) && cap != null) cap.Collected(denied);
        caps.Remove(id);

        int me = LocalActor;
        bool counts = state == GameState.Playing && !matchEnded;
        if (takerActor == me)
        {
            FeedbackUI.Pickup();
            if (!counts) ShowMessage(denied ? "CAP BACK" : "CAP GRABBED");
            else if (denied) AwardXp(xpPerDeny, "DENIED");
            else AwardXp(xpPerCap, "CAP GRABBED");
        }
        else if (ownerActor == me && !denied && counts)
        {
            FeedbackUI.AddKillFeedNote($"{GetPlayerName(takerActor)} GRABBED YOUR CAP");
        }
        else if (denied && counts && takerActor != ownerActor && ownerActor == me)
        {
            FeedbackUI.AddKillFeedNote($"{GetPlayerName(takerActor)} SAVED YOUR CAP");
        }
    }

    // In a team mode, whether both are known to be on the same team
    private bool SameTeam(int actorA, int actorB)
    {
        if (!IsTeamMode) return false;
        int a = GetTeam(actorA), b = GetTeam(actorB);
        return a >= 0 && a == b;
    }

    private void RemoveCap(int id)
    {
        if (caps.TryGetValue(id, out CapPickup cap) && cap != null) Destroy(cap.gameObject);
        caps.Remove(id);
    }

    // Host only (bots): the nearest cap on the ground within reach, theirs or anyone's
    public bool NearestCap(Vector3 from, float maxDistance, out Vector3 position)
    {
        position = from;
        float best = maxDistance;
        bool found = false;
        foreach (KeyValuePair<int, CapPickup> entry in caps)
        {
            if (entry.Value == null) continue;
            float d = Vector3.Distance(from, entry.Value.BasePosition);
            if (d >= best) continue;
            best = d;
            position = entry.Value.BasePosition;
            found = true;
        }
        return found;
    }
}
