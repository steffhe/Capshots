using System.Collections.Generic;
using Fusion;
using UnityEngine;

// Host migration: when the host leaves mid-match, Photon picks one of the other players to host
// instead and everyone reconnects to them (NetworkSession.OnHostMigration). The map reloads and the
// match carries on where it was: the clock, everyone's kills, deaths and team, the bots and the
// broken glass. Everyone respawns at a spawn point.
//
// Fusion's snapshot of the old host's objects isn't used: players get new PlayerRefs from the new
// host, so the old host's state (keyed by the old PlayerRefs) wouldn't fit. Instead every player
// remembers the match as they last saw it, which is everything the host had synced, and the new
// host rebuilds it from that. Each returning player tells the new host who they were before
// (their old actor id) when they register, and gets their score and team back.
public partial class Manager
{
    private class MigrationCarry
    {
        // This player's actor id under the old host
        public int localActor;
        public GameState state;
        public int matchTime;
        public List<PlayerInfo> players;
        public int[] glass;
        public int xpStartLevel, xpStartXp, xpGained;
        public string xpReason;
    }

    // Survives the map reloading; read (and cleared) by the next Manager to spawn
    private static MigrationCarry carry;

    // Host only, after a migration: the people who were in the match, by their old actor id, until
    // they come back and register
    private readonly Dictionary<int, PlayerInfo> returningPlayers = new Dictionary<int, PlayerInfo>();

    // Whether this Manager is the match carrying on after a host migration
    private bool resumed;

    // Called on every player when the host has left, before the old session shuts down
    public void SaveForHostMigration()
    {
        carry = new MigrationCarry
        {
            localActor = LocalActor,
            state = state,
            matchTime = currentMatchTime,
            players = new List<PlayerInfo>(playerInfo),
            glass = (int[])glassShown.Clone(),
            xpStartLevel = xpStartLevel,
            xpStartXp = xpStartXp,
            xpGained = xpGained,
            xpReason = xpReason,
        };
    }

    // The migration didn't work out (or the player left during it): nothing to carry over
    public static void ClearHostMigration()
    {
        carry = null;
    }

    // Takes what was carried over, if this match is a migrated one. Called first thing in Spawned.
    private MigrationCarry TakeCarry()
    {
        MigrationCarry taken = carry;
        carry = null;
        resumed = taken != null;
        return taken;
    }

    // Everyone: the results screen still shows the whole match's XP and accuracy
    private void RestoreXp(MigrationCarry from)
    {
        xpStartLevel = from.xpStartLevel;
        xpStartXp = from.xpStartXp;
        xpGained = from.xpGained;
        xpReason = from.xpReason;
    }

    // New host: the clock and the bots, before anyone is added. People get their scores back as
    // they register (RestoreReturningPlayer).
    private void ResumeMatch(MigrationCarry from)
    {
        // A match being played carries on; a warmup or countdown starts over
        if (from.state == GameState.Playing)
        {
            State = GameState.Playing;
            MatchTime = from.matchTime;
        }

        returningPlayers.Clear();
        int botCount = 0;
        foreach (PlayerInfo p in from.players)
        {
            if (IsBotActor(p.actor))
            {
                if (bots.Count < MaxPlayers) RestoreBot(p);
                botCount++;
            }
            else returningPlayers[p.actor] = p;
        }
        // New bots get names the returning ones don't have
        botsAdded = botCount;
    }

    // New host: the glass as it was (once the bots' map of the level is built, so they can walk
    // through the gaps)
    private void RestoreGlass(MigrationCarry from)
    {
        if (from.glass == null) return;
        for (int word = 0; word < GlassWords && word < from.glass.Length; word++)
        {
            int value = from.glass[word];
            GlassPanes.Set(word, value);
            if (OfficeGlass.Instance == null) continue;
            for (int i = 0; i < PanesPerWord; i++)
            {
                if (((value >> (i * 2)) & 3) == OfficeGlass.Broken)
                {
                    BotNavigation.RemoveObstacle(OfficeGlass.Instance.ColliderOf(word * PanesPerWord + i));
                }
            }
        }
    }

    private void RestoreBot(PlayerInfo p)
    {
        PlayerRef bot = PlayerRef.FromEncoded(p.actor + 1);
        if (Stats.ContainsKey(bot)) return;

        PlayerStats stats = new PlayerStats
        {
            Username = p.profile.username,
            Level = p.profile.level,
            Kills = p.kills,
            Deaths = p.deaths,
            Team = p.team,
            Ping = 0,
            Hat = (byte)p.hat,
        };
        Stats.Set(bot, stats);
        bots.Add(bot);
        ScheduleSpawn(bot);
    }

    // New host: a player registering after a migration gets back the score and team they had
    private void RestoreReturningPlayer(ref PlayerStats stats, int oldActor)
    {
        if (oldActor < 0 || IsBotActor(oldActor)) return;
        if (!returningPlayers.TryGetValue(oldActor, out PlayerInfo p)) return;
        returningPlayers.Remove(oldActor);

        stats.Kills = p.kills;
        stats.Deaths = p.deaths;
        if (IsTeamMode) stats.Team = p.team;
    }
}
