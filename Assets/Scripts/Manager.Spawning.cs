using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

// Spawning: the host spawns each player's body after a short wait, at a spawn point away from
// enemies. Ragdolls of players who died.
public partial class Manager
{
    [Header("Respawn")]
    // Seconds from dying to coming back (the death camera, then the map view)
    public float respawnTime = 5f;
    // Seconds from joining the match to the first spawn
    public float joinSpawnTime = 3f;
    // Spawn points an enemy can see count as this many metres closer to the enemy
    public float spawnSightPenalty = 30f;
    // Random metres added to each spawn point's score, so the choice isn't always the same
    public float spawnRandomness = 5f;
    // In team modes, spawn points on the player's own side of the map (marked with SpawnSide) count as
    // this many metres farther from enemies. Maps without SpawnSide markers aren't affected.
    public float ownSideSpawnBonus = 25f;

    // The SpawnSide of each spawn point (null where there's none), looked up once
    private SpawnSide[] spawnSides;

    // Host only: players waiting to (re)spawn, and when
    private readonly Dictionary<PlayerRef, TickTimer> pendingSpawns = new Dictionary<PlayerRef, TickTimer>();
    private NetworkObject playerPrefabObject;

    // afterDeath: the longer wait after dying, rather than the one after joining
    private void ScheduleSpawn(PlayerRef player, bool afterDeath = false)
    {
        if (!HasStateAuthority || State == GameState.Ending) return;
        pendingSpawns[player] = TickTimer.CreateFromSeconds(Runner, InRange ? 0.75f : afterDeath ? respawnTime : joinSpawnTime);
    }

    private void SpawnWaitingPlayers()
    {
        if (pendingSpawns.Count == 0) return;

        List<PlayerRef> due = null;
        foreach (KeyValuePair<PlayerRef, TickTimer> entry in pendingSpawns)
        {
            if (!entry.Value.Expired(Runner)) continue;
            if (due == null) due = new List<PlayerRef>();
            due.Add(entry.Key);
        }
        if (due == null) return;

        foreach (PlayerRef player in due)
        {
            pendingSpawns.Remove(player);
            if (Stats.ContainsKey(player)) SpawnPlayer(player);
        }
    }

    private void SpawnPlayer(PlayerRef player)
    {
        // Never two bodies for one player (bots aren't Fusion players, so they're looked up by body)
        NetworkObject existing;
        if (IsBot(player)) { if (Player.FindPlayer(player) != null) return; }
        else if (Runner.TryGetPlayerObject(player, out existing) && existing != null && existing.IsValid) return;

        if (playerPrefabObject == null) playerPrefabObject = Resources.Load<NetworkObject>(playerPrefab);
        if (playerPrefabObject == null)
        {
            Debug.LogError($"No network object prefab called '{playerPrefab}' in Resources. Run Capshots > Set Up Fusion.");
            return;
        }

        Transform spawn = InRange ? RangeSpawn(IsBot(player)) : ChooseSpawnPoint(player);
        if (IsBot(player)) SpawnBot(player, spawn);
        else Runner.Spawn(playerPrefabObject, spawn.position, spawn.rotation, player);
    }

    // Picks the spawn point farthest from living enemies, avoiding points an enemy can see. On maps whose
    // spawn points are marked with SpawnSide, a team prefers its own side and lane-only points are left
    // out of team modes.
    private Transform ChooseSpawnPoint(PlayerRef forPlayer)
    {
        if (spawnSides == null || spawnSides.Length != spawnPoints.Length)
        {
            spawnSides = new SpawnSide[spawnPoints.Length];
            for (int i = 0; i < spawnPoints.Length; i++) spawnSides[i] = spawnPoints[i] != null ? spawnPoints[i].GetComponent<SpawnSide>() : null;
        }
        bool teamMode = IsTeamMode;
        int team = teamMode ? GetTeam(forPlayer.PlayerId) : -1;

        // The points this mode can use, and those on the player's own side
        List<Transform> usable = new List<Transform>(spawnPoints.Length);
        List<Transform> ownSide = new List<Transform>();
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            SpawnSide side = spawnSides[i];
            if (teamMode && side != null && side.freeForAllOnly) continue;
            usable.Add(spawnPoints[i]);
            if (team >= 0 && side != null && side.team == team) ownSide.Add(spawnPoints[i]);
        }
        if (usable.Count == 0) usable.AddRange(spawnPoints);

        List<Vector3> enemyEyes = new List<Vector3>();
        foreach (Player p in Player.All)
        {
            if (p.Object == null || !p.Object.IsValid || p.IsDead) continue;
            if (p.Owner == forPlayer) continue;
            if (!AreEnemies(forPlayer.PlayerId, p.ActorId)) continue;
            enemyEyes.Add(p.normalCam != null ? p.normalCam.transform.position : p.transform.position + Vector3.up * 1.5f);
        }

        if (enemyEyes.Count == 0 || usable.Count <= 1)
        {
            // Nobody to avoid: anywhere on our own side, if the map has sides
            List<Transform> pool = ownSide.Count > 0 ? ownSide : usable;
            return pool[Random.Range(0, pool.Count)];
        }

        // Players' own bodies and glass walls shouldn't block line of sight
        int sightMask = ~((1 << Layers.RemotePlayer) | (1 << Layers.LocalPlayer) | (1 << Layers.Hitbox) | (1 << Layers.Glass) | (1 << Layers.Ragdoll));

        Transform best = usable[0];
        float bestScore = float.MinValue;

        foreach (Transform point in usable)
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
            if (ownSide.Count > 0 && ownSide.Contains(point)) score += ownSideSpawnBonus;
            if (score > bestScore)
            {
                bestScore = score;
                best = point;
            }
        }

        return best;
    }

    // Host only: tells every client to spawn its own ragdoll for a player who just died. The ragdolls
    // aren't networked objects: each client simulates its copy locally, so they never fight a network sync.
    // actor: the dead player's PlayerId, which picks their hat and its colour. capDrops: Cap Grab is
    // dropping their cap as a pickup (DropCap), so the body has none: the pickup is the cap.
    public void SpawnRagdoll(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 hitDirection, Vector3 hitOffset, float knockback, bool headshot, int actor, bool capDrops = false)
    {
        RPC_SpawnRagdoll(position, rotation, velocity, hitDirection, hitOffset, knockback, headshot, actor, capDrops);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SpawnRagdoll(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 hitDirection, Vector3 hitOffset, float knockback, NetworkBool headshot, int actor, NetworkBool capDrops)
    {
        GameObject prefab = Resources.Load<GameObject>("Dead Player");
        if (prefab == null) return;

        GameObject ragdoll = Instantiate(prefab, position, rotation);
        DeadPlayer dead = ragdoll.GetComponent<DeadPlayer>();
        if (dead != null)
        {
            dead.Dress(PlayerCap.ColorFor(actor), IsTeamMode);
            dead.Launch(velocity, hitDirection, hitOffset, knockback, headshot, PlayerCap.ColorFor(actor), capDrops ? DeadPlayer.NoHat : GetPlayerHat(actor));
            // Your own body: the death camera watches it fly
            if (actor == LocalActor) DeathCam.Follow(dead.FocusPoint);
        }

        // An ordinary kill shorts the drone out where the bullet hit; a cap shot has its own, bigger burst
        if (!headshot) KillSpark.Spawn(position + hitOffset, hitDirection, PlayerCap.ColorFor(actor));
    }
}
