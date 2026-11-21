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
// Manager.Pickups.cs, Manager.Chat.cs and Manager.Xp.cs.
public partial class Manager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    public static Manager Instance { get; private set; }

    public const int MaxPlayers = 16;

    public int mainmenu = 0;
    public int killcount = 20;
    public int matchlength = 180;

    [Header("Match start")]
    public int minPlayersToStart = 2;
    public int startCountdown = 5;

    public GameObject mapcam;

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
    }

    private void Start()
    {
        // A map scene opened without going through the menu has no session to be part of
        if (NetworkSession.Runner == null) SceneManager.LoadScene(mainmenu);
    }

    public override void Spawned()
    {
        ReadMatchSettings();

        InitializeUI();
        changes = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority)
        {
            State = GameState.Waiting;
            MatchTime = matchlength;
            Heartbeat = TickTimer.CreateFromSeconds(Runner, 1f);
            foreach (PlayerRef player in Runner.ActivePlayers) AddPlayer(player);
        }

        // Tell the host our name and level for the scoreboard
        RPC_Register(Launcher.myProfile.username, Launcher.myProfile.level);

        state = State;
        currentMatchTime = MatchTime;
        OnStatsChanged();
        RefreshTimerUI();
        ready = true;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        ready = false;
    }

    private void ReadMatchSettings()
    {
        var properties = Runner.SessionInfo.Properties;
        if (properties == null) return;

        SessionProperty value;
        if (properties.TryGetValue("killcount", out value)) killcount = (int)value;
        if (properties.TryGetValue("mode", out value)) mode = (MatchMode)(int)value;
        if (properties.TryGetValue("matchlength", out value)) matchlength = (int)value;
    }

    private void Update()
    {
        if (!ready) return;

        // The map camera shows while the local player is dead or the match is over
        bool alive = Player.Local != null;
        if (mapcam.activeSelf == alive && !matchEnded) mapcam.SetActive(!alive);
        if (Hud.activeSelf != alive) Hud.SetActive(alive);

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
        bool statsChanged = false, stateChanged = false, timeChanged = false;
        foreach (string change in changes.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(Stats): statsChanged = true; break;
                case nameof(State): stateChanged = true; break;
                case nameof(MatchTime): timeChanged = true; break;
            }
        }

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
        leaderboard = GameObject.Find("HUD").transform.Find("Leaderboard").transform;
        endgame = GameObject.Find("Canvas").transform.Find("End Game").transform;
        uiUsername = GameObject.Find("HUD/Username/Text").GetComponent<Text>();
        CreateXpPopup();
        FeedbackUI.Create(GameObject.Find("Canvas").transform, myKills.font);
        CreateTeamScore();
        ChatUI.Create(GameObject.Find("Canvas").transform, myKills.font, this);
        TouchControls.Create(myKills.font);

        RefreshMyStats();
    }

    private void RefreshTimerUI()
    {
        if (timer == null) return;

        if (state == GameState.Waiting)
        {
            timer.text = "WAITING";
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
            myKills.text = $"{playerInfo[myind].kills} KILLS";
            myDeaths.text = $"{playerInfo[myind].deaths} DEATHS";
        }
        else
        {
            myKills.text = "0 KILLS";
            myDeaths.text = "0 DEATHS";
        }

        RefreshTeamScore();
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
            list.Add(p);
        }
        list.Sort((a, b) => a.actor.CompareTo(b.actor));
        playerInfo = list;

        RecalculateMyIndex();
        TeamsVersion++;
        RefreshMyStats();
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
        if (HasStateAuthority) AddPlayer(player);
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        Stats.Remove(player);
        pendingSpawns.Remove(player);
        NetworkObject body;
        if (Runner.TryGetPlayerObject(player, out body) && body != null) Runner.Despawn(body);
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

    // Each player tells the host its name and level once it has joined
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Register(string username, int level, RpcInfo info = default)
    {
        PlayerRef player = info.Source.IsRealPlayer ? info.Source : Runner.LocalPlayer;
        if (!Stats.ContainsKey(player)) AddPlayer(player);

        PlayerStats stats = Stats[player];
        if (username != null && username.Length > 20) username = username.Substring(0, 20);
        stats.Username = string.IsNullOrWhiteSpace(username) ? "PLAYER " + player.PlayerId : username;
        stats.Level = Mathf.Max(1, level);
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
    }

    // Runs once a second on the host. Drives Waiting -> Starting -> Playing -> Ending.
    private void TimerStep()
    {
        switch (State)
        {
            case GameState.Waiting:
                if (EnoughPlayersToStart())
                {
                    MatchTime = startCountdown;
                    State = GameState.Starting;
                }
                break;

            case GameState.Starting:
                if (!EnoughPlayersToStart())
                {
                    MatchTime = matchlength;
                    State = GameState.Waiting;
                    break;
                }

                MatchTime -= 1;
                if (MatchTime <= 0)
                {
                    // Warmup is over: everyone starts the match on 0 kills and 0 deaths
                    foreach (PlayerRef player in StatsKeys())
                    {
                        PlayerStats stats = Stats[player];
                        stats.Kills = 0;
                        stats.Deaths = 0;
                        Stats.Set(player, stats);
                    }
                    MatchTime = matchlength;
                    State = GameState.Playing;
                }
                break;

            case GameState.Playing:
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
        if (State == GameState.Playing)
        {
            ChangeStats(victim, 0, 1);
            if (killer.IsRealPlayer && killer != victim) ChangeStats(killer, 1, 0);
        }

        RPC_KillFeed(killer.IsRealPlayer ? killer.PlayerId : -1, victim.PlayerId, weaponName, headshot);

        ScheduleSpawn(victim);
        ScoreCheck();
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
        if (State != GameState.Playing) return;

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

    // Host only: closes the session to new players and removes everyone's body
    private void EndMatch()
    {
        if (State == GameState.Ending) return;
        State = GameState.Ending;
        pendingSpawns.Clear();

        Runner.SessionInfo.IsOpen = false;
        Runner.SessionInfo.IsVisible = false;

        foreach (Player p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (p.Object != null && p.Object.IsValid) Runner.Despawn(p.Object);
        }
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

        AwardMatchXp();

        currentMatchTime = 0;
        RefreshTimerUI();

        mapcam.SetActive(true);

        endgame.gameObject.SetActive(true);
        Leaderboard(endgame.Find("Leaderboard"));

        // The host waits a moment longer, so everyone else leaves on their own instead of seeing "host left"
        StartCoroutine(End(HasStateAuthority ? 7f : 6f));
    }

    private IEnumerator End(float wait)
    {
        yield return new WaitForSeconds(wait);
        NetworkSession.Leave();
    }

    private void OnStateChanged(GameState previousState)
    {
        RefreshTimerUI();

        switch (state)
        {
            case GameState.Waiting:
                ShowMessage("WAITING FOR PLAYERS");
                break;
            case GameState.Starting:
                ShowMessage("MATCH STARTING");
                break;
            case GameState.Playing:
                if (previousState == GameState.Starting) ShowMessage("FIGHT!");
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
        SessionProperty mapName;
        if (Runner != null && Runner.SessionInfo.Properties != null && Runner.SessionInfo.Properties.TryGetValue("mapname", out mapName))
        {
            return (string)mapName;
        }
        return SceneManager.GetActiveScene().name;
    }

    #endregion
}
