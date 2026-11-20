using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine.UI;

// Respawning: choosing a spawn point away from enemies.
public partial class Manager
{
    [Header("Respawn")]
    public float respawnTime = 3f;
    // Spawn points an enemy can see count as this many metres closer to the enemy
    public float spawnSightPenalty = 30f;
    // Random metres added to each spawn point's score, so the choice isn't always the same
    public float spawnRandomness = 5f;

    public void Spawn()
    {
        StartCoroutine(Spawn(respawnTime));

    }

    private IEnumerator Spawn(float wait)
    {
        mapcam.SetActive(true);
        Hud.SetActive(false);
        yield return new WaitForSeconds(wait);
        Transform spawn = ChooseSpawnPoint();
        mapcam.SetActive(false);
        Hud.SetActive(true);
        PhotonNetwork.Instantiate(playerPrefab, spawn.position, spawn.rotation);
    }

    // Picks the spawn point farthest from living enemies, avoiding points an enemy can see
    private Transform ChooseSpawnPoint()
    {
        List<Vector3> enemyEyes = new List<Vector3>();
        foreach (Player p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (p.photonView.IsMine) continue;
            if (!AreEnemies(PhotonNetwork.LocalPlayer.ActorNumber, p.photonView.OwnerActorNr)) continue;
            enemyEyes.Add(p.normalCam != null ? p.normalCam.transform.position : p.transform.position + Vector3.up * 1.5f);
        }

        if (enemyEyes.Count == 0 || spawnPoints.Length <= 1)
        {
            return spawnPoints[Random.Range(0, spawnPoints.Length)];
        }

        // Players' own colliders shouldn't block line of sight
        int sightMask = ~((1 << Layers.RemotePlayer) | (1 << Layers.LocalPlayer));

        Transform best = spawnPoints[0];
        float bestScore = float.MinValue;

        foreach (Transform point in spawnPoints)
        {
            Vector3 eye = point.position + Vector3.up * 1.6f;
            float closest = float.MaxValue;
            bool seen = false;

            foreach (Vector3 enemy in enemyEyes)
            {
                closest = Mathf.Min(closest, Vector3.Distance(eye, enemy));
                if (!seen && !Physics.Linecast(eye, enemy, sightMask, QueryTriggerInteraction.Ignore)) seen = true;
            }

            float score = closest - (seen ? spawnSightPenalty : 0f) + Random.Range(0f, spawnRandomness);
            if (score > bestScore)
            {
                bestScore = score;
                best = point;
            }
        }

        return best;
    }

    // Tells every client to spawn its own ragdoll for a player who just died. The ragdolls aren't
    // networked objects: each client simulates its copy locally, so they never fight a network sync.
    public void SpawnRagdoll_S(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 hitDirection, bool headshot)
    {
        object[] package = new object[] { position, rotation, velocity, hitDirection, headshot };
        PhotonNetwork.RaiseEvent((byte)EventCodes.SpawnRagdoll, package, new RaiseEventOptions { Receivers = ReceiverGroup.All }, new SendOptions { Reliability = true });
    }

    public void SpawnRagdoll_R(object[] data)
    {
        GameObject prefab = Resources.Load<GameObject>("Dead Player");
        if (prefab == null) return;

        GameObject ragdoll = Instantiate(prefab, (Vector3)data[0], (Quaternion)data[1]);
        DeadPlayer dead = ragdoll.GetComponent<DeadPlayer>();
        if (dead != null) dead.Launch((Vector3)data[2], (Vector3)data[3], (bool)data[4]);
    }
}
