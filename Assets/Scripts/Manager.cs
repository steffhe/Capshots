using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Fusion;

// The match: player list, game state and timer, and the end of the match.
// A network object in each map scene. The host owns it: it keeps everyone's stats, runs the
// match timer and spawns players; Fusion syncs that state to everyone else.
// Other parts of this class are in Manager.Teams.cs, Manager.Scoreboard.cs, Manager.Spawning.cs,
// Manager.Pickups.cs, Manager.Grenades.cs, Manager.Chat.cs, Manager.Xp.cs and Manager.Lobby.cs.
public partial class Manager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    public static Manager Instance { get; private set; }

    public const int MaxPlayers = 16;

    public int mainmenu = 0;
    // How the match is won, from the room's settings (these are the defaults for a map opened
    // without them). 0 means no limit: a match is won either on kills, with the clock counting up,
    // or on time, by whoever has the most kills when it runs out.
    public int killcount = 20;
    public int matchlength = 180;

    public bool HasKillLimit => killcount > 0;
    public bool HasTimeLimit => matchlength > 0;

    [Header("Match start")]
    public int minPlayersToStart = 2;
    public int startCountdown = 5;

    public GameObject mapcam;

    // A player who falls below this height (off the map, or through a gap) dies
    public float killHeight = -25f;

    public string playerPrefabString;
    public string playerPrefab;
    public Transform[] spawnPoints;

    // Everyone's stats, rebuilt from the networked Stats whenever they change
    public List<PlayerInfo> playerInfo = new List<PlayerInfo>();
    public int myind = -1;

    private Text myKills;
    private Text myDeaths;
    private Text timer;

    public Transform leaderboard;
    private Transform endgame;

    public GameObject Hud;

    private Text uiUsername;
    private bool matchEnded = false;
    private bool ready;

    // This client's view of the match state, updated from the networked State
    private GameState state = GameState.Waiting;
    private int currentMatchTime;

    private ChangeDetector changes;

    public bool MatchOver => matchEnded;

    // The local player's accuracy, cap shots and favourite gun, for the results screen
    public static readonly MatchSummary Summary = new MatchSummary();
    // Whether shots and kills count towards it now: only while the match is being played
    public bool CountsForSummary => state == GameState.Playing && !matchEnded;

    #region Networked state

    [Networked, Capacity(MaxPlayers)] private NetworkDictionary<PlayerRef, PlayerStats> Stats => default;
    [Networked] private GameState State { get; set; }
    [Networked] private int MatchTime { get; set; }
    // Ticks once a second on the host to run the match timer
    [Networked] private TickTimer Heartbeat { get; set; }

    #endregion

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        DestroyEndVolume();
        BotNavigation.Clear();
    }

    private void Start()
    {
        // A map scene opened without going through the menu has no session to be part of
        if (NetworkSession.Runner == null) SceneManager.LoadScene(mainmenu);
    }

    public override void Spawned()
    {
        // After a host migration the match carries on: keep the XP and accuracy so far
        MigrationCarry migrated = TakeCarry();
        if (migrated == null)
        {
            Summary.Reset();
            RememberStartingXp();
        }
        else RestoreXp(migrated);
        ReadMatchSettings();

        InitializeUI();
        // The map view directs itself around the map (see MapCamera)
        if (mapcam != null)
        {
            MapCamera director = mapcam.GetComponent<MapCamera>();
            if (director == null) director = mapcam.AddComponent<MapCamera>();
            director.Init(spawnPoints);
        }
        changes = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority)
        {
            // Open again after the last map's results (a map voted for on the results screen)
            NetworkSession.SetOpen(Runner, true);
            State = GameState.Waiting;
            MatchTime = matchlength;
            HostPlayer = Runner.LocalPlayer;
            Heartbeat = TickTimer.CreateFromSeconds(Runner, 1f);
            if (migrated != null) ResumeMatch(migrated);
            foreach (PlayerRef player in Runner.ActivePlayers) AddPlayer(player);
            UpdateBots();
            if (migrated != null) RestoreGlass(migrated);
        }

        // Tell the host our name and level for the scoreboard, and the hat everyone sees us in. After
        // a host migration, also who we were before, so the new host can give us our score back.
        RPC_Register(Launcher.myProfile.username, Launcher.myProfile.level, GameSettings.Hat, GameSettings.Face, migrated != null ? migrated.localActor : -1);
        GameSettings.HatChanged += SendHat;
        GameSettings.FaceChanged += SendFace;
        GameSettings.ColoursChanged += OnColoursChanged;

        state = State;
        currentMatchTime = MatchTime;
        OnStatsChanged();
        RefreshTimerUI();
        // Glass already broken when joining a match in progress
        SyncGlass(false);
        ready = true;

        // The map is in: lift the loading screen and introduce the map
        PlayIntro();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        GameSettings.HatChanged -= SendHat;
        GameSettings.FaceChanged -= SendFace;
        GameSettings.ColoursChanged -= OnColoursChanged;
        ready = false;
        if (lobbyPanel != null) Destroy(lobbyPanel.gameObject);
    }

    private void ReadMatchSettings()
    {
        var properties = NetworkSession.MatchProperties(Runner);
        if (properties == null) return;

        SessionProperty value;
        if (properties.TryGetValue("killcount", out value)) killcount = (int)value;
        if (properties.TryGetValue("mode", out value)) mode = (MatchMode)(int)value;
        if (properties.TryGetValue("matchlength", out value)) matchlength = (int)value;
        if (properties.TryGetValue("bots", out value)) botSetting = (int)value;
        if (properties.TryGetValue("lobby", out value)) IsLobby = (int)value != 0 && !NetworkSession.IsPractice(Runner) && string.IsNullOrEmpty(NextMapName);
        if (properties.TryGetValue("botskill", out value)) BotSkill = Mathf.Clamp((int)value, BotLogic.Easy, BotLogic.Hard);
        // A match needs some way to end
        if (!HasKillLimit && !HasTimeLimit) killcount = 20;
        // Gun Game always ends at the top of the ladder (and maybe on time as well)
        if (mode == MatchMode.GunGame) killcount = GunGameRules.KillsToWin;
    }

    private void Update()
    {
        if (!ready) return;

        // The map camera shows while the local player is dead or the match is over, once the death
        // camera has finished lingering on the body
        bool alive = Player.Local != null;
        bool showMap = !alive && !DeathCam.Active;
        if (mapcam.activeSelf != showMap && !matchEnded) mapcam.SetActive(showMap);
        // The HUD goes away for the end of match banner and results
        bool showHud = alive && !matchEnded;
        if (Hud.activeSelf != showHud) Hud.SetActive(showHud);
        UpdateCapArrows();
        UpdateLobby();

        if (state == GameState.Ending) return;

        if (GameInput.Pressed(GameInput.Scoreboard))
        {
            Leaderboard(leaderboard);
        }else if (GameInput.Released(GameInput.Scoreboard))
        {
           leaderboard.gameObject.SetActive(false);
        }
    }

    public override void Render()
    {
        bool statsChanged = false, stateChanged = false, timeChanged = false, glassChanged = false;
        foreach (string change in changes.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(Stats): statsChanged = true; break;
                case nameof(State): stateChanged = true; break;
                case nameof(MatchTime): timeChanged = true; break;
                case nameof(GlassPanes): glassChanged = true; break;
                case nameof(VoteCounts): RefreshVotes(); break;
            }
        }

        if (glassChanged) SyncGlass(true);

        if (timeChanged)
        {
            currentMatchTime = MatchTime;
            RefreshTimerUI();
        }

        if (statsChanged) OnStatsChanged();

        if (stateChanged && State != state)
        {
            GameState previousState = state;
            state = State;
            OnStateChanged(previousState);
            StateCheck();
        }
    }

    public void InitializeUI()
    {
        // Restyle first: the XP popup, chat, kill feed and touch controls copy the HUD's font and style
        Transform canvas = GameObject.Find("Canvas").transform;
        UITheme.StyleHud(Hud != null ? Hud.transform : canvas.Find("HUD"));
        UITheme.StyleEndGame(canvas.Find("End Game"));

        myKills = GameObject.Find("HUD/Stats/Kills").GetComponent<Text>();
        myDeaths = GameObject.Find("HUD/Stats/Deaths").GetComponent<Text>();
        timer = GameObject.Find("HUD/Timer/Text").GetComponent<Text>();
        // Out of the HUD (which hides while you're dead) so the scoreboard can be held up while waiting
        // to respawn too. Right above the HUD, as before: under the chat, pause menu and results.
        Transform hud = GameObject.Find("HUD").transform;
        leaderboard = hud.Find("Leaderboard");
        if (leaderboard != null)
        {
            leaderboard.SetParent(hud.parent, false);
            leaderboard.SetSiblingIndex(hud.GetSiblingIndex() + 1);
        }
        else leaderboard = hud.parent.Find("Leaderboard");
        endgame = GameObject.Find("Canvas").transform.Find("End Game").transform;
        uiUsername = GameObject.Find("HUD/Username/Text").GetComponent<Text>();
        CreateXpPopup();
        FeedbackUI.Create(GameObject.Find("Canvas").transform, myKills.font);
        CreateTeamScore();
        ChatUI.Create(GameObject.Find("Canvas").transform, myKills.font, this);
        TouchControls.Create(myKills.font);
        CreateLobbyPanel(canvas);

        RefreshMyStats();
    }

    private void RefreshTimerUI()
    {
        // The lobby's countdown line
        RefreshLobbyPanel();
        if (timer == null) return;

        if (state == GameState.Waiting)
        {
            timer.text = IsLobby ? "LOBBY" : "WAITING";
            return;
        }

        int time = Mathf.Max(0, currentMatchTime);
        string minutes = (time / 60).ToString("00");
        string seconds = (time % 60).ToString("00");
        timer.text = $"{minutes}:{seconds}";
    }

    private void RefreshMyStats()
    {
        if (myKills == null) return;

        if (myind >= 0 && playerInfo.Count > myind)
        {
            myKills.text = $"{playerInfo[myind].kills} {ScoreWord}";
            myDeaths.text = $"{playerInfo[myind].deaths} DEATHS";
        }
        else
        {
            myKills.text = $"0 {ScoreWord}";
            myDeaths.text = "0 DEATHS";
        }

        RefreshTeamScore();
        RefreshLobbyPanel();
        if (leaderboard.gameObject.activeSelf) Leaderboard(leaderboard);
    }

    private void StateCheck()
    {
        if (state == GameState.Ending)
        {
            EndGame();
        }
    }

    // Rebuilds the local scoreboard list from the host's stats
    private void OnStatsChanged()
    {
        List<PlayerInfo> list = new List<PlayerInfo>();
        foreach (KeyValuePair<PlayerRef, PlayerStats> entry in Stats)
        {
            PlayerStats s = entry.Value;
            PlayerInfo p = new PlayerInfo(new ProfileData(s.Username.ToString(), s.Level, 0, 150f, 150f), entry.Key.PlayerId, s.Kills, s.Deaths);
            p.team = s.Team;
            p.ping = s.Ping;
            p.hat = s.Hat;
            p.face = s.Face;
            p.ready = s.Ready;
            list.Add(p);
        }
        list.Sort((a, b) => a.actor.CompareTo(b.actor));
        playerInfo = list;

        RecalculateMyIndex();
        TeamsVersion++;
        RefreshMyStats();
        AnnounceGunGameStep();
        AnnounceTeam();
    }

    public string GetPlayerName(int actor)
    {
        int index = FindPlayerIndex(actor);
        if (index >= 0 && !string.IsNullOrEmpty(playerInfo[index].profile.username)) return playerInfo[index].profile.username;
        return "Unknown";
    }

    public int GetPlayerLevel(int actor)
    {
        int index = FindPlayerIndex(actor);
        return index >= 0 ? playerInfo[index].profile.level : 1;
    }

    // The hat a player wears (see Hats); the cap for anyone not on the list
    public int GetPlayerHat(int actor)
    {
        int index = FindPlayerIndex(actor);
        return index >= 0 && Hats.IsValid(playerInfo[index].hat) ? playerInfo[index].hat : Hats.Cap;
    }

    private int FindPlayerIndex(int actor)
    {
        for (int i = 0; i < playerInfo.Count; i++)
        {
            if (playerInfo[i].actor == actor) return i;
        }
        return -1;
    }

    private void RecalculateMyIndex()
    {
        myind = Runner != null ? FindPlayerIndex(Runner.LocalPlayer.PlayerId) : -1;
    }

    private int LocalActor => Runner != null ? Runner.LocalPlayer.PlayerId : -1;

    #region Host: players joining and leaving

    public void PlayerJoined(PlayerRef player)
    {
        if (!HasStateAuthority) return;
        // A bot makes way for them first, so the player list never holds more than it has room for
        UpdateBots(1);
        AddPlayer(player);
        UpdateBots();
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        Stats.Remove(player);
        pendingSpawns.Remove(player);
        NetworkObject body;
        if (Runner.TryGetPlayerObject(player, out body) && body != null) Runner.Despawn(body);
        // A bot takes their place
        UpdateBots();
    }

    private void AddPlayer(PlayerRef player)
    {
        if (Stats.ContainsKey(player)) return;

        PlayerStats stats = new PlayerStats
        {
            Username = "PLAYER " + player.PlayerId,
            Level = 1,
            Team = ChooseTeamForNewPlayer(),
            Ping = -1,
        };
        Stats.Set(player, stats);
        ScheduleSpawn(player);
    }

    // The face on a player's drone (see Faces); the classic face for anyone not on the list
    public int GetPlayerFace(int actor)
    {
        int index = FindPlayerIndex(actor);
        return index >= 0 && Faces.IsValid(playerInfo[index].face) ? playerInfo[index].face : Faces.Classic;
    }

    // A new face picked in the pause menu's Customize panel: everyone sees it at once
    private void SendFace()
    {
        if (Object != null && Object.IsValid) RPC_SetFace(GameSettings.Face);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetFace(int face, RpcInfo info = default)
    {
        PlayerRef player = info.Source.IsRealPlayer ? info.Source : Runner.LocalPlayer;
        PlayerStats stats;
        if (!Faces.IsValid(face) || !Stats.TryGet(player, out stats)) return;
        stats.Face = (byte)face;
        Stats.Set(player, stats);
    }

    // Each player tells the host its name, level, hat and face once it has joined
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Register(string username, int level, int hat, int face, int oldActor, RpcInfo info = default)
    {
        PlayerRef player = info.Source.IsRealPlayer ? info.Source : Runner.LocalPlayer;
        if (!Stats.ContainsKey(player)) AddPlayer(player);

        PlayerStats stats = Stats[player];
        if (username != null && username.Length > 20) username = username.Substring(0, 20);
        stats.Username = string.IsNullOrWhiteSpace(username) ? "PLAYER " + player.PlayerId : username;
        stats.Level = Mathf.Max(1, level);
        stats.Hat = (byte)Hats.Wearable(hat, level);
        stats.Face = (byte)Faces.Wearable(face, level);
        RestoreReturningPlayer(ref stats, oldActor);
        Stats.Set(player, stats);
    }

    // A new hat picked in the pause menu's Customize panel: everyone sees it at once
    private void SendHat()
    {
        if (Object != null && Object.IsValid) RPC_SetHat(GameSettings.Hat);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetHat(int hat, RpcInfo info = default)
    {
        PlayerRef player = info.Source.IsRealPlayer ? info.Source : Runner.LocalPlayer;
        PlayerStats stats;
        if (!Hats.IsValid(hat) || !Stats.TryGet(player, out stats)) return;
        stats.Hat = (byte)hat;
        Stats.Set(player, stats);
    }

    #endregion

    #region Host: match flow

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (Heartbeat.ExpiredOrNotRunning(Runner))
        {
            Heartbeat = TickTimer.CreateFromSeconds(Runner, 1f);
            TimerStep();
            UpdatePings();
        }

        SpawnWaitingPlayers();
        CheckPickups();
        CheckCaps();
        UpdateGrenades();
        BotNavigation.Tick();

        // The match is over and everyone has cut away to the results: remove the bodies
        if (despawnTimer.Expired(Runner))
        {
            despawnTimer = TickTimer.None;
            DespawnPlayers();
        }
    }

    // Host only: when to remove the frozen players at the end of the match
    private TickTimer despawnTimer;

    // Runs once a second on the host. Drives Waiting -> Starting -> Playing -> Ending.
    private void TimerStep()
    {
        switch (State)
        {
            case GameState.Waiting:
                if (CanStartMatch())
                {
                    MatchTime = startCountdown;
                    State = GameState.Starting;
                }
                break;

            case GameState.Starting:
                if (!CanStartMatch())
                {
                    MatchTime = matchlength;
                    State = GameState.Waiting;
                    break;
                }

                MatchTime -= 1;
                if (MatchTime <= 0)
                {
                    // Warmup is over: everyone starts the match on 0 kills and 0 deaths (and no streaks)
                    streaks.Clear();
                    awardCounts.Clear();
                    foreach (PlayerRef player in StatsKeys())
                    {
                        PlayerStats stats = Stats[player];
                        stats.Kills = 0;
                        stats.Deaths = 0;
                        Stats.Set(player, stats);
                    }
                    MatchTime = matchlength;
                    State = GameState.Playing;
                    forceStart = false;
                }
                break;

            case GameState.Playing:
                // Won on kills only: the clock shows how long the match has been going
                if (!HasTimeLimit)
                {
                    MatchTime += 1;
                    break;
                }
                MatchTime -= 1;
                if (MatchTime < 0)
                {
                    MatchTime = 0;
                    EndMatch();
                }
                break;
        }
    }

    private bool EnoughPlayersToStart()
    {
        int needed = minPlayersToStart;
        int maxPlayers = Runner.SessionInfo.MaxPlayers;
        if (maxPlayers > 0) needed = Mathf.Min(needed, maxPlayers);
        return Stats.Count >= needed;
    }

    // Host only: a player died. Updates the score, tells everyone and schedules the respawn.
    public void PlayerKilled(PlayerRef victim, PlayerRef killer, string weaponName, bool headshot)
    {
        if (!HasStateAuthority) return;

        // Kills and deaths during warmup and the countdown don't count
        int multiKill = 0, streak = 0, endedStreak = 0;
        if (State == GameState.Playing)
        {
            ChangeStats(victim, 0, 1);
            // In Cap Grab a kill only drops the cap: grabbing it is what scores (Manager.CapGrab.cs).
            // In Gun Game only gun kills move you up the ladder (Manager.GunGame.cs).
            bool scores = IsCapGrab ? false : IsGunGame ? GunGameRules.Counts(weaponName, PlasmaWeaponName) : true;
            if (killer.IsRealPlayer && killer != victim && scores) ChangeStats(killer, 1, 0);
            CountStreaks(victim, killer, out multiKill, out streak, out endedStreak);
            CountForAwards(killer, victim, weaponName, headshot, streak);
        }

        RPC_KillFeed(killer.IsRealPlayer ? killer.PlayerId : -1, victim.PlayerId, weaponName, headshot,
            (byte)Mathf.Min(multiKill, 255), (byte)Mathf.Min(streak, 255), (byte)Mathf.Min(endedStreak, 255));

        ScheduleSpawn(victim, true);
        ScoreCheck();
    }

    // Host only: kills in a row without dying, and kills in quick succession, for each player
    private struct StreakCount
    {
        public int streak, multiKill;
        public float lastKillAt;
    }
    private readonly Dictionary<PlayerRef, StreakCount> streaks = new Dictionary<PlayerRef, StreakCount>();

    // The killer's multi-kill and streak after this kill (0 if there's no killer), and the streak the
    // victim had, which this death ends
    private void CountStreaks(PlayerRef victim, PlayerRef killer, out int multiKill, out int streak, out int endedStreak)
    {
        multiKill = streak = 0;
        streaks.TryGetValue(victim, out StreakCount lost);
        endedStreak = lost.streak;
        streaks.Remove(victim);

        if (!killer.IsRealPlayer || killer == victim) return;
        float now = Runner.SimulationTime;
        streaks.TryGetValue(killer, out StreakCount count);
        count.multiKill = count.multiKill > 0 && now - count.lastKillAt <= KillStreaks.MultiKillWindow ? count.multiKill + 1 : 1;
        count.streak++;
        count.lastKillAt = now;
        streaks[killer] = count;
        multiKill = count.multiKill;
        streak = count.streak;
    }

    private void ChangeStats(PlayerRef player, int kills, int deaths)
    {
        PlayerStats stats;
        if (!Stats.TryGet(player, out stats)) return;
        stats.Kills += (short)kills;
        stats.Deaths += (short)deaths;
        Stats.Set(player, stats);
    }

    private void ScoreCheck()
    {
        if (State != GameState.Playing || !HasKillLimit) return;

        bool detectwin = false;
        if (IsTeamMode)
        {
            detectwin = HostTeamKills(0) >= killcount || HostTeamKills(1) >= killcount;
        }
        else foreach (KeyValuePair<PlayerRef, PlayerStats> entry in Stats)
        {
            if (entry.Value.Kills >= killcount)
            {
                detectwin = true;
                break;
            }
        }

        if (detectwin) EndMatch();
    }

    private int HostTeamKills(int team)
    {
        int kills = 0;
        foreach (KeyValuePair<PlayerRef, PlayerStats> entry in Stats)
        {
            if (entry.Value.Team == team) kills += entry.Value.Kills;
        }
        return kills;
    }

    // Host only: closes the session to new players. Everyone freezes where they stand (Player and
    // Weapon stop using input once the state is Ending); the bodies are removed a few seconds
    // later, after everyone has cut away to the results.
    private void EndMatch()
    {
        if (State == GameState.Ending) return;
        State = GameState.Ending;
        pendingSpawns.Clear();

        NetworkSession.SetOpen(Runner, false);
        ChooseVoteMaps();
        SendAwards();

        despawnTimer = TickTimer.CreateFromSeconds(Runner, DespawnDelay);
    }

    // A copy of the keys, so Stats can be changed while going through them
    private List<PlayerRef> StatsKeys()
    {
        List<PlayerRef> keys = new List<PlayerRef>();
        foreach (KeyValuePair<PlayerRef, PlayerStats> entry in Stats) keys.Add(entry.Key);
        return keys;
    }

    #endregion

    #region Everyone: match state

    private void EndGame()
    {
        state = GameState.Ending;

        if (matchEnded) return;
        matchEnded = true;

        // The host only stops the clock at zero when time runs out; a kill limit ends it early
        bool timeUp = HasTimeLimit && MatchTime <= 0;

        AwardMatchXp();

        // A countdown stops at zero; a clock counting up stops where it is
        if (HasTimeLimit) currentMatchTime = 0;
        RefreshTimerUI();

        // Banner, results, then back to the menu (Manager.Presentation.cs). The host stays on the
        // results a moment longer, so everyone else leaves on their own instead of seeing "host left".
        StartCoroutine(EndSequence(timeUp));
    }

    private void OnStateChanged(GameState previousState)
    {
        RefreshTimerUI();

        switch (state)
        {
            case GameState.Waiting:
                if (!IsLobby) ShowMessage("WAITING FOR PLAYERS");
                // Someone in the lobby wasn't ready after all (or a new player came in)
                else if (previousState == GameState.Starting) ShowMessage("COUNTDOWN STOPPED");
                break;
            case GameState.Starting:
                ShowMessage("MATCH STARTING");
                break;
            case GameState.Playing:
                // A new match (not one carrying on after a host migration)
                if (previousState == GameState.Starting)
                {
                    ShowMessage("FIGHT!");
                    Summary.Reset();
                }
                break;
        }
    }

    // The session ended without this player leaving (the host left or the connection dropped).
    // Returns true if a message is shown, so the menu loads a little later.
    public bool ShowDisconnected(ShutdownReason reason)
    {
        if (matchEnded) return false;
        bool timedOut = reason == ShutdownReason.ConnectionTimeout || reason == ShutdownReason.PhotonCloudTimeout || reason == ShutdownReason.OperationTimeout;
        ShowMessage(timedOut ? "CONNECTION LOST" : "THE HOST LEFT THE MATCH");
        return true;
    }

    private string CurrentMapName()
    {
        if (!string.IsNullOrEmpty(NextMapName)) return NextMapName;
        SessionProperty mapName;
        var properties = NetworkSession.MatchProperties(Runner);
        if (properties != null && properties.TryGetValue("mapname", out mapName))
        {
            return (string)mapName;
        }
        return SceneManager.GetActiveScene().name;
    }

    #endregion
}
