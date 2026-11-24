using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

// Ammo boxes dropped by players. Every client shows its own copy; the host decides who gets each box
// by checking which player walks over it, so two players can never both take it.
public partial class Manager
{
    [Header("Ammo pickups")]
    public bool dropAmmoOnDeath = true;
    public float pickupLifetime = 20f;

    private readonly Dictionary<int, AmmoPickup> pickups = new Dictionary<int, AmmoPickup>();
    private int pickupCounter;

    // Host only: drops an ammo box for everyone at the given position
    public void DropAmmo(Vector3 position)
    {
        if (!dropAmmoOnDeath || !HasStateAuthority) return;
        RPC_SpawnPickup(++pickupCounter, position);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnPickup(int id, Vector3 position)
    {
        if (pickups.ContainsKey(id)) return;
        pickups[id] = AmmoPickup.Create(id, position, this, pickupLifetime);
    }

    // Host only, every tick: gives a box to the first living player standing on it who needs ammo
    private void CheckPickups()
    {
        if (pickups.Count == 0) return;

        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        List<int> taken = null;

        foreach (KeyValuePair<int, AmmoPickup> entry in pickups)
        {
            if (entry.Value == null) continue;
            Vector3 boxPosition = entry.Value.BasePosition;

            foreach (Player p in players)
            {
                if (p.Object == null || !p.Object.IsValid || p.IsDead) continue;
                if (Vector3.Distance(p.transform.position, boxPosition) > AmmoPickup.PickupRadius) continue;

                Weapon weapon = p.GetComponent<Weapon>();
                if (weapon == null || !weapon.NeedsAmmo()) continue;

                weapon.AddAmmoPickup();
                if (taken == null) taken = new List<int>();
                taken.Add(entry.Key);
                RPC_PickupTaken(entry.Key, p.ActorId);
                break;
            }
        }

        if (taken != null) foreach (int id in taken) RemovePickup(id, true);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PickupTaken(int id, int actor)
    {
        RemovePickup(id, true);

        if (actor == LocalActor)
        {
            FeedbackUI.Pickup();
            ShowMessage("+ AMMO");
        }
    }

    // Host only (bots): the nearest ammo box within reach, as the crow flies
    public bool NearestPickup(Vector3 from, float maxDistance, out Vector3 position)
    {
        position = from;
        float best = maxDistance;
        bool found = false;
        foreach (KeyValuePair<int, AmmoPickup> entry in pickups)
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

    public void RemovePickup(int id, bool destroy = false)
    {
        AmmoPickup pickup;
        if (!pickups.TryGetValue(id, out pickup)) return;

        pickups.Remove(id);
        if (destroy && pickup != null) Destroy(pickup.gameObject);
    }
}
