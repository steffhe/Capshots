using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine.UI;

// Ammo boxes dropped by players. The master client decides who gets each box.
public partial class Manager
{
    [Header("Ammo pickups")]
    public bool dropAmmoOnDeath = true;
    public float pickupLifetime = 20f;

    private readonly Dictionary<int, AmmoPickup> pickups = new Dictionary<int, AmmoPickup>();
    private int pickupCounter;

    // Drops an ammo box for everyone at the given position
    public void DropAmmo_S(Vector3 position)
    {
        if (!dropAmmoOnDeath) return;

        int id = PhotonNetwork.LocalPlayer.ActorNumber * 10000 + (pickupCounter++ % 10000);
        object[] package = new object[] { id, position };

        PhotonNetwork.RaiseEvent((byte)EventCodes.SpawnPickup, package, new RaiseEventOptions { Receivers = ReceiverGroup.All }, new SendOptions { Reliability = true });
    }

    public void SpawnPickup_R(object[] data)
    {
        int id = (int)data[0];
        Vector3 position = (Vector3)data[1];
        if (pickups.ContainsKey(id)) return;

        pickups[id] = AmmoPickup.Create(id, position, this, pickupLifetime);
    }

    // Asks the master client for a pickup, so two players can't both take it
    public void ClaimPickup_S(int id)
    {
        object[] package = new object[] { id, PhotonNetwork.LocalPlayer.ActorNumber };

        PhotonNetwork.RaiseEvent((byte)EventCodes.ClaimPickup, package, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, new SendOptions { Reliability = true });
    }

    public void ClaimPickup_R(object[] data)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int id = (int)data[0];
        int actor = (int)data[1];

        // First claim wins: remove it here straight away so later claims are ignored
        if (!pickups.ContainsKey(id)) return;
        RemovePickup(id, true);

        object[] package = new object[] { id, actor };
        PhotonNetwork.RaiseEvent((byte)EventCodes.PickupTaken, package, new RaiseEventOptions { Receivers = ReceiverGroup.All }, new SendOptions { Reliability = true });
    }

    public void PickupTaken_R(object[] data)
    {
        int id = (int)data[0];
        int actor = (int)data[1];

        RemovePickup(id, true);

        if (actor == PhotonNetwork.LocalPlayer.ActorNumber && Player.Local != null)
        {
            Weapon weapon = Player.Local.GetComponent<Weapon>();
            if (weapon != null) weapon.AddAmmoPickup();
            FeedbackUI.Pickup();
            ShowMessage("+ AMMO");
        }
    }

    public void RemovePickup(int id, bool destroy = false)
    {
        AmmoPickup pickup;
        if (!pickups.TryGetValue(id, out pickup)) return;

        pickups.Remove(id);
        if (destroy && pickup != null) Destroy(pickup.gameObject);
    }
}
