using System.Collections.Generic;
using UnityEngine;
using Fusion;

// Bots: computer players that fill a room up to the number the host chose when creating it. They
// make way for people: when someone joins a full room a bot leaves, and when someone leaves a bot
// takes their place. Bots run on the host only (BotBrain); everyone else sees them as ordinary players.
//
// A bot is a scoreboard entry and a body like anyone else's, under a made-up PlayerRef of its own
// (from BotRawBase up, far above any real player's), so kills, the kill feed, teams and spawning work
// for bots unchanged. Those ids are never handed to Fusion as a player: a bot's body has no input
// authority and carries its id in Player.BotId.
public partial class Manager
{
    // Raw PlayerRef values for bots start here. Real players get 1 to the room's size (16 at most).
    public const int BotRawBase = 101;
    // So a bot's actor id (PlayerRef.PlayerId, the raw value minus one) is at least this
    public const int BotActorBase = BotRawBase - 1;

    // How many bots the host asked for (the "bots" room setting)
    private int botSetting;
    // How well they play (the "botskill" room setting, BotLogic.Easy to Hard)
    public int BotSkill { get; private set; } = BotLogic.Normal;
    // Host only: the bots in the match, oldest first
    private readonly List<PlayerRef> bots = new List<PlayerRef>();
    private int botsAdded;

    public static bool IsBot(PlayerRef player) => player.RawEncoded >= BotRawBase;
    public static bool IsBotActor(int actor) => actor >= BotActorBase;

    // Host only: adds or removes bots until the room has as many as it should. joining: people about
    // to be added to the player list, to make room for.
    private void UpdateBots(int joining = 0)
    {
        if (!HasStateAuthority || State == GameState.Ending) return;

        int humans = joining;
        foreach (KeyValuePair<PlayerRef, PlayerStats> entry in Stats)
        {
            if (!IsBot(entry.Key)) humans++;
        }

        int roomSize = Runner.SessionInfo.MaxPlayers > 0 ? Runner.SessionInfo.MaxPlayers : MaxPlayers;
        int wanted = BotLogic.DesiredBotCount(botSetting, humans, Mathf.Min(roomSize, MaxPlayers));

        while (bots.Count > wanted) RemoveBot(ChooseBotToRemove());
        while (bots.Count < wanted && Stats.Count + joining < MaxPlayers)
        {
            if (!AddBot()) break;
        }

        if (wanted > 0) BotNavigation.Build(spawnPoints);
    }

    private bool AddBot()
    {
        PlayerRef bot = PlayerRef.None;
        for (int slot = 0; slot < MaxPlayers; slot++)
        {
            PlayerRef candidate = PlayerRef.FromEncoded(BotRawBase + slot);
            if (!Stats.ContainsKey(candidate)) { bot = candidate; break; }
        }
        if (bot == PlayerRef.None) return false;

        PlayerStats stats = new PlayerStats
        {
            Username = BotLogic.BotName(botsAdded++),
            Level = 1,
            Team = ChooseTeamForNewPlayer(),
            Ping = 0,
            // One of the first few hats, so bots look like people who've played a little
            Hat = (byte)Random.Range(0, Hats.BotHatCount),
            Face = (byte)Random.Range(0, Faces.BotFaceCount),
        };
        Stats.Set(bot, stats);
        bots.Add(bot);
        ScheduleSpawn(bot);
        return true;
    }

    private void RemoveBot(PlayerRef bot)
    {
        bots.Remove(bot);
        Stats.Remove(bot);
        pendingSpawns.Remove(bot);
        Player body = Player.FindPlayer(bot);
        if (body != null && body.Object != null && body.Object.IsValid) Runner.Despawn(body.Object);
    }

    // The newest bot, or in Team Deathmatch the newest on the bigger team, so the teams stay even
    private PlayerRef ChooseBotToRemove()
    {
        if (IsTeamMode)
        {
            int blue = 0, red = 0;
            foreach (KeyValuePair<PlayerRef, PlayerStats> entry in Stats)
            {
                if (entry.Value.Team == 0) blue++; else red++;
            }
            if (blue != red)
            {
                byte bigger = (byte)(blue > red ? 0 : 1);
                for (int i = bots.Count - 1; i >= 0; i--)
                {
                    if (Stats.TryGet(bots[i], out PlayerStats s) && s.Team == bigger) return bots[i];
                }
            }
        }
        return bots[bots.Count - 1];
    }

    // Host only: a bot's body. Nobody has input authority over it; Player.BotId says whose it is.
    private void SpawnBot(PlayerRef bot, Transform spawn)
    {
        if (Player.FindPlayer(bot) != null) return;
        Runner.Spawn(playerPrefabObject, spawn.position, spawn.rotation, PlayerRef.None,
            (runner, obj) => obj.GetComponent<Player>().BotId = bot);
    }
}
