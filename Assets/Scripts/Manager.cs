using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine.UI;

// The match: player list, game state and timer, network events and the end of the match.
// Other parts of this class are in Manager.Teams.cs, Manager.Scoreboard.cs, Manager.Spawning.cs,
// Manager.Pickups.cs, Manager.Chat.cs and Manager.Xp.cs.
public partial class Manager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public static Manager Instance { get; private set; }

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

    public List<PlayerInfo> playerInfo = new List<PlayerInfo>();
    public int myind;

    private Text myKills;
    private Text myDeaths;
    private Text timer;

    public Transform leaderboard;
    private Transform endgame;

    private int currentMatchTime;
    private Coroutine timerCoroutine;

    public GameObject Hud;

    private Text uiUsername;
    private bool matchEnded = false;

    private GameState state = GameState.Waiting;



    public enum EventCodes : byte
    {
        NewPlayer,
        UpdatePlayers,
        ChangeStat,
        RefreshTimer,
        KillFeed,
        SpawnPickup,
        ClaimPickup,
        PickupTaken,
        ChatMessage,
        SpawnRagdoll
    }


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
        killcount = (int)PhotonNetwork.CurrentRoom.CustomProperties["killcount"];
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("mode"))
        {
            mode = (GameMode)(int)PhotonNetwork.CurrentRoom.CustomProperties["mode"];
        }
        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("matchlength"))
        {
            matchlength = (int)PhotonNetwork.CurrentRoom.CustomProperties["matchlength"];
        }
        mapcam.SetActive(false);
        ValidateConnection();
        InitializeUI();
        InitializeTimer();
        NewPlayer_S(Launcher.myProfile);
        Spawn();
    }

    private void Update()
    {
        if (state == GameState.Ending) return;

        if (GameInput.Pressed(GameInput.Scoreboard))
        {
            Leaderboard(leaderboard);
        }else if (GameInput.Released(GameInput.Scoreboard))
        {
           leaderboard.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }
        private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    public void InitializeUI()
    {
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
        StartCoroutine(SharePing());
       

        RefreshMyStats();
    }

    private void InitializeTimer()
    {
        currentMatchTime = matchlength;
        RefreshTimerUI();
        if (PhotonNetwork.IsMasterClient)
        {
            timerCoroutine = StartCoroutine(Timer());
        }
    }

    private void RefreshTimerUI()
    {
        if (state == GameState.Waiting)
        {
            timer.text = "WAITING";
            return;
        }

        string minutes = (currentMatchTime / 60).ToString("00");
        string seconds = (currentMatchTime % 60).ToString("00");
        timer.text = $"{minutes}:{seconds}";
    }

    private void RefreshMyStats()
    {
        if (myKills == null) return;

        if (myind >= 0 && playerInfo.Count > myind)
        {
            myKills.text = $"{playerInfo[myind].kills} Kills";
            myDeaths.text = $"{playerInfo[myind].deaths} Deaths";
        }
        else
        {
            myKills.text = "0 Kills";
            myDeaths.text = "0 Deaths";
        }

        RefreshTeamScore();
        if (leaderboard.gameObject.activeSelf) Leaderboard(leaderboard);
    }

    private void ValidateConnection() {
        if (PhotonNetwork.IsConnected) return;
        SceneManager.LoadScene(mainmenu);
    }

    private void StateCheck()
    {
        if (state == GameState.Ending)
        {
            EndGame();
        }
    }

    public void NewPlayer_S(ProfileData p)
    {
        object[] package = new object[6];

        package[0] = p.username;
        package[1] = p.level;
        package[2] = p.xp;
        package[3] = PhotonNetwork.LocalPlayer.ActorNumber;
        package[4] = (short)0;
        package[5] = (short)0;

        PhotonNetwork.RaiseEvent((byte)EventCodes.NewPlayer, package, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, new SendOptions { Reliability = true });

    }
    public void NewPlayer_R(object[] data)
    {

        PlayerInfo p = new PlayerInfo(new ProfileData((string)data[0], (int)data[1], (int)data[2], 150f, 50f), (int)data[3], (short)data[4], (short)data[5]);

        // A player re-sends NewPlayer if a host change lost the first one, so never add the same actor twice
        if (FindPlayerIndex(p.actor) < 0)
        {
            p.team = ChooseTeamForNewPlayer();
            playerInfo.Add(p);
        }

        UpdatePlayers_S((int)state, playerInfo);

    }
    public void UpdatePlayers_S(int state, List<PlayerInfo> info)
    {
        object[] package = new object[info.Count + 1];

        package[0] = state;
        for(int i = 0; i < info.Count; i++)
        {
            object[] piece = new object[7];

            piece[0] = info[i].profile.username;
            piece[1] = info[i].profile.level;
            piece[2] = info[i].profile.xp;
            piece[3] = info[i].actor;
            piece[4] = info[i].kills;
            piece[5] = info[i].deaths;
            piece[6] = info[i].team;

            package[i + 1] = piece;
        }


        PhotonNetwork.RaiseEvent((byte)EventCodes.UpdatePlayers, package, new RaiseEventOptions { Receivers = ReceiverGroup.All }, new SendOptions { Reliability = true });

    }

    public void UpdatePlayers_R(object[] data)
    {
        GameState previousState = state;
        state = (GameState)data[0];
        playerInfo = new List<PlayerInfo>();


        for (int i = 1; i < data.Length; i++)
        {
            object[] extract = (object[])data[i];

            PlayerInfo p = new PlayerInfo(new ProfileData((string)extract[0], (int)extract[1], (int)extract[2], 150f, 150f), (int)extract[3], (short)extract[4], (short)extract[5]);
            p.team = (byte)extract[6];

            playerInfo.Add(p);
        }

        RecalculateMyIndex();

        // If our NewPlayer event went to a host that left before handling it, we are missing from the list
        if (myind < 0 && state != GameState.Ending) NewPlayer_S(Launcher.myProfile);

        TeamsVersion++;
        RefreshMyStats();
        if (previousState != state) OnStateChanged(previousState);
        AnnounceTeam();
        StateCheck();

    }

    public void ChangeStat_S(int actor, PlayerStat stat, byte amt)
    {
        object[] package = new object[] {actor, (byte)stat, amt };

        PhotonNetwork.RaiseEvent((byte)EventCodes.ChangeStat, package, new RaiseEventOptions { Receivers = ReceiverGroup.All }, new SendOptions { Reliability = true });

    }

    public void ChangeStat_R(object[] data)
    {
        int actor = (int)data[0];
        PlayerStat stat = (PlayerStat)(byte)data[1];
        byte amt = (byte)data[2];

        // Kills and deaths during warmup and the countdown don't count
        if (stat != PlayerStat.Level && state != GameState.Playing) return;

        for (int i = 0; i < playerInfo.Count; i++)
        {
            if (playerInfo[i].actor == actor)
            {
                switch (stat)
                {
                    case PlayerStat.Kills:
                        playerInfo[i].kills += amt;
                        Debug.Log($"Player {playerInfo[i].profile.username } : kills = {playerInfo[i].kills }");
                        break;

                    case PlayerStat.Deaths:
                        playerInfo[i].deaths += amt;
                        Debug.Log($"Player {playerInfo[i].profile.username } : deaths = {playerInfo[i].deaths }");
                        break;

                    case PlayerStat.Level:
                        playerInfo[i].profile.level += amt;
                        break;
                }

                if (stat == PlayerStat.Kills && actor == PhotonNetwork.LocalPlayer.ActorNumber && !matchEnded)
                {
                    AwardXp(xpPerKill * amt, "KILL");
                }

                if (i == myind) RefreshMyStats();

                if (leaderboard.gameObject.activeSelf) Leaderboard(leaderboard);

                break;
            }
        }

        RefreshTeamScore();
        ScoreCheck();
    }

    public string GetPlayerName(int actor)
    {
        int index = FindPlayerIndex(actor);
        if (index >= 0) return playerInfo[index].profile.username;

        Photon.Realtime.Player player = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(actor) : null;
        return player != null && !string.IsNullOrEmpty(player.NickName) ? player.NickName : "Unknown";
    }

    public void RefreshTimer_S()
    {
        object[] package = new object[] {currentMatchTime};


        PhotonNetwork.RaiseEvent((byte)EventCodes.RefreshTimer, package, new RaiseEventOptions { Receivers = ReceiverGroup.All }, new SendOptions { Reliability = true });

    }

    public void RefreshTimer_R(object[] data)
    {
        currentMatchTime = (int)data[0];
        RefreshTimerUI();
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code >= 200) return;

        EventCodes e = (EventCodes)photonEvent.Code;
        object[] o = (object[])photonEvent.CustomData;

        switch (e)
        {
            case EventCodes.NewPlayer:
                NewPlayer_R(o);
                    break;
            case EventCodes.UpdatePlayers:
                UpdatePlayers_R(o);
                break;
            case EventCodes.ChangeStat:
                ChangeStat_R(o);
                break;
            case EventCodes.RefreshTimer:
                RefreshTimer_R(o);
                break;
            case EventCodes.KillFeed:
                KillFeed_R(o);
                break;
            case EventCodes.SpawnPickup:
                SpawnPickup_R(o);
                break;
            case EventCodes.ClaimPickup:
                ClaimPickup_R(o);
                break;
            case EventCodes.PickupTaken:
                PickupTaken_R(o);
                break;
            case EventCodes.ChatMessage:
                ChatMessage_R(o);
                break;
            case EventCodes.SpawnRagdoll:
                SpawnRagdoll_R(o);
                break;
                
        }
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);

        // Every client removes the player itself, so this works no matter which host is in charge
        int index = FindPlayerIndex(otherPlayer.ActorNumber);
        if (index < 0) return;

        playerInfo.RemoveAt(index);
        TeamsVersion++;
        RecalculateMyIndex();
        RefreshMyStats();
    }

    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        base.OnMasterClientSwitched(newMasterClient);

        if (!PhotonNetwork.IsMasterClient) return;

        // Drop anyone who is no longer in the room, then share our list so everyone is in sync
        playerInfo.RemoveAll(p => PhotonNetwork.CurrentRoom.GetPlayer(p.actor) == null);
        RecalculateMyIndex();

        if (state == GameState.Ending || matchEnded) return;

        UpdatePlayers_S((int)state, playerInfo);

        // The old host ran the match timer, so take it over from the last time it sent
        if (timerCoroutine == null) timerCoroutine = StartCoroutine(Timer());
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
        myind = FindPlayerIndex(PhotonNetwork.LocalPlayer.ActorNumber);
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        SceneManager.LoadScene(mainmenu);
    }

    private void ScoreCheck()
    {
        bool detectwin = false;

        if (IsTeamMode)
        {
            detectwin = TeamKills(0) >= killcount || TeamKills(1) >= killcount;
        }
        else foreach (PlayerInfo a in playerInfo)
        {
            if (a.kills >= killcount)
            {
                detectwin = true;
                    break;
            }
        }

        if (detectwin)
        {
            if (PhotonNetwork.IsMasterClient && state == GameState.Playing)
            {
                UpdatePlayers_S((int)GameState.Ending, playerInfo);
            }
        }
    }

    private void EndGame()
    {
        state = GameState.Ending;

        if (matchEnded) return;
        matchEnded = true;

        AwardMatchXp();

        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        currentMatchTime = 0;
        RefreshTimerUI();
         
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.DestroyAll();
            PhotonNetwork.CurrentRoom.IsVisible = false;
            PhotonNetwork.CurrentRoom.IsOpen = false;
        }

        mapcam.SetActive(true);

        endgame.gameObject.SetActive(true);
        Leaderboard(endgame.Find("Leaderboard"));

        StartCoroutine(End(6f));
    }

    private IEnumerator End(float wait)
    {
        yield return new WaitForSeconds(wait);

        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.LeaveRoom();
    }

    // Runs on the host only. Drives Waiting -> Starting -> Playing -> Ending.
    private IEnumerator Timer()
    {
        yield return new WaitForSeconds(1);

        switch (state)
        {
            case GameState.Waiting:
                if (EnoughPlayersToStart())
                {
                    currentMatchTime = startCountdown;
                    UpdatePlayers_S((int)GameState.Starting, playerInfo);
                    RefreshTimer_S();
                }
                break;

            case GameState.Starting:
                if (!EnoughPlayersToStart())
                {
                    currentMatchTime = matchlength;
                    UpdatePlayers_S((int)GameState.Waiting, playerInfo);
                    RefreshTimer_S();
                    break;
                }

                currentMatchTime -= 1;
                if (currentMatchTime <= 0)
                {
                    // Warmup is over: everyone starts the match on 0 kills and 0 deaths
                    foreach (PlayerInfo p in playerInfo)
                    {
                        p.kills = 0;
                        p.deaths = 0;
                    }
                    currentMatchTime = matchlength;
                    UpdatePlayers_S((int)GameState.Playing, playerInfo);
                }
                RefreshTimer_S();
                break;

            case GameState.Playing:
                currentMatchTime -= 1;
                if (currentMatchTime < 0)
                {
                    timerCoroutine = null;
                    UpdatePlayers_S((int)GameState.Ending, playerInfo);
                    yield break;
                }
                RefreshTimer_S();
                break;

            default:
                timerCoroutine = null;
                yield break;
        }

        timerCoroutine = StartCoroutine(Timer());
    }

    private bool EnoughPlayersToStart()
    {
        int needed = minPlayersToStart;
        int maxPlayers = PhotonNetwork.CurrentRoom.MaxPlayers;
        if (maxPlayers > 0) needed = Mathf.Min(needed, maxPlayers);
        return PhotonNetwork.CurrentRoom.PlayerCount >= needed;
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

    private string CurrentMapName()
    {
        object mapName;
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("mapname", out mapName))
        {
            return mapName.ToString();
        }
        return SceneManager.GetActiveScene().name;
    }
}
