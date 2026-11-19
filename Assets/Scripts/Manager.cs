using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine.UI;

public class PlayerInfo
{
    public ProfileData profile;
    public int actor;
    public short kills;
    public short deaths;

    public PlayerInfo (ProfileData p, int a, short k, short d)
    {
        this.profile = p;
        this.actor = a;
        this.kills = k;
        this.deaths = d;
    }
}


public enum GameState
{
    Waiting = 0,
    Starting = 1,
    Playing = 2,
    Ending = 3
}

public class Manager : MonoBehaviourPunCallbacks, IOnEventCallback
{

    public int mainmenu = 0;
    public int killcount = 20;
    public int matchlength = 180;

    [Header("Match start")]
    public int minPlayersToStart = 2;
    public int startCountdown = 5;

    [Header("Respawn")]
    public float respawnTime = 3f;

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

    [Header("XP")]
    public int xpPerKill = 100;
    public int xpMatchComplete = 150;
    public int xpWin = 300;

    private Text uiUsername;
    private Text xpPopup;
    private Coroutine xpPopupRoutine;
    private bool matchEnded = false;

    private GameState state = GameState.Waiting;



    public enum EventCodes : byte
    {
        NewPlayer,
        UpdatePlayers,
        ChangeStat,
        RefreshTimer,
        KillFeed
    }


    private void Start()
    {
        killcount = (int)PhotonNetwork.CurrentRoom.CustomProperties["killcount"];
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

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            Leaderboard(leaderboard);
        }else if (Input.GetKeyUp(KeyCode.Tab))
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

    public void Spawn()
    {
        StartCoroutine(Spawn(respawnTime));

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

        if (leaderboard.gameObject.activeSelf) Leaderboard(leaderboard);
    }

    private void Leaderboard(Transform lb)
    {
        for (int i = 2; i < lb.childCount; i++)
        {
            Destroy(lb.GetChild(i).gameObject);
        }


        lb.Find("Header/Mode").GetComponent<Text>().text = "FREE FOR ALL";
        lb.Find("Header/Map").GetComponent<Text>().text = CurrentMapName();

        GameObject playerCard = lb.GetChild(1).gameObject;
        playerCard.SetActive(false);

        List<PlayerInfo> sorted = SortPlayers(playerInfo);

        bool alternateColors = false;
        foreach(PlayerInfo a in sorted)
        {
            GameObject newcard = Instantiate(playerCard, lb) as GameObject;

            if (alternateColors) newcard.GetComponent<Image>().color = new Color32(0, 0,0, 100);

            newcard.transform.Find("Level").GetComponent<Text>().text = a.profile.level.ToString("00");
            newcard.transform.Find("Username").GetComponent<Text>().text = a.profile.username;
            newcard.transform.Find("Score Value").GetComponent<Text>().text = (a.kills * 100).ToString();
            newcard.transform.Find("Kills Value").GetComponent<Text>().text = a.kills.ToString();
            newcard.transform.Find("Deaths Value").GetComponent<Text>().text = a.deaths.ToString();

            newcard.SetActive(true);
        }

        lb.gameObject.SetActive(true);

    }

    private List<PlayerInfo> SortPlayers (List<PlayerInfo> info)
    {
        List<PlayerInfo> sorted = new List<PlayerInfo>();

        while (sorted.Count < info.Count)
        {
            short highest = -1;
            PlayerInfo selection = info[0];

            foreach (PlayerInfo a in info)
            {
                if (sorted.Contains(a)) continue;
                if (a.kills > highest)
                {
                    selection = a;
                    highest = a.kills;
                }
            }

            sorted.Add(selection);
        }

        return sorted;
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
        if (FindPlayerIndex(p.actor) < 0) playerInfo.Add(p);

        UpdatePlayers_S((int)state, playerInfo);

    }
    public void UpdatePlayers_S(int state, List<PlayerInfo> info)
    {
        object[] package = new object[info.Count + 1];

        package[0] = state;
        for(int i = 0; i < info.Count; i++)
        {
            object[] piece = new object[6];

            piece[0] = info[i].profile.username;
            piece[1] = info[i].profile.level;
            piece[2] = info[i].profile.xp;
            piece[3] = info[i].actor;
            piece[4] = info[i].kills;
            piece[5] = info[i].deaths;

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

            playerInfo.Add(p);
        }

        RecalculateMyIndex();

        // If our NewPlayer event went to a host that left before handling it, we are missing from the list
        if (myind < 0 && state != GameState.Ending) NewPlayer_S(Launcher.myProfile);

        RefreshMyStats();
        if (previousState != state) OnStateChanged(previousState);
        StateCheck();

    }

    public void ChangeStat_S(int actor, byte stat, byte amt)
    {
        object[] package = new object[] {actor, stat, amt };

        PhotonNetwork.RaiseEvent((byte)EventCodes.ChangeStat, package, new RaiseEventOptions { Receivers = ReceiverGroup.All }, new SendOptions { Reliability = true });

    }

    public void ChangeStat_R(object[] data)
    {
        int actor = (int)data[0];
        byte stat = (byte)data[1];
        byte amt = (byte)data[2];

        // Kills and deaths during warmup and the countdown don't count
        if (stat != 2 && state != GameState.Playing) return;

        for (int i = 0; i < playerInfo.Count; i++)
        {
            if (playerInfo[i].actor == actor)
            {
                switch (stat)
                {
                    case 0:
                        playerInfo[i].kills += amt;
                        Debug.Log($"Player {playerInfo[i].profile.username } : kills = {playerInfo[i].kills }");
                        break;

                    case 1:
                        playerInfo[i].deaths += amt;
                        Debug.Log($"Player {playerInfo[i].profile.username } : deaths = {playerInfo[i].deaths }");
                        break;

                    case 2:
                        playerInfo[i].profile.level += amt;
                        break;
                }

                if (stat == 0 && actor == PhotonNetwork.LocalPlayer.ActorNumber && !matchEnded)
                {
                    AwardXp(xpPerKill * amt, "KILL");
                }

                if (i == myind) RefreshMyStats();

                if (leaderboard.gameObject.activeSelf) Leaderboard(leaderboard);

                break;
            }
        }

        ScoreCheck();
    }

    // Sent by the player who died, so everyone can show it in the kill feed
    public void KillFeed_S(int killer, int victim, string weapon, bool headshot)
    {
        object[] package = new object[] { killer, victim, weapon, headshot };

        PhotonNetwork.RaiseEvent((byte)EventCodes.KillFeed, package, new RaiseEventOptions { Receivers = ReceiverGroup.All }, new SendOptions { Reliability = true });
    }

    public void KillFeed_R(object[] data)
    {
        int killer = (int)data[0];
        int victim = (int)data[1];
        string weapon = (string)data[2];
        bool headshot = (bool)data[3];

        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        FeedbackUI.AddKillFeed(GetPlayerName(killer), GetPlayerName(victim), weapon, headshot, killer == me || victim == me);

        if (killer == me && victim != me) FeedbackUI.Kill();
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
                
        }
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);

        // Every client removes the player itself, so this works no matter which host is in charge
        int index = FindPlayerIndex(otherPlayer.ActorNumber);
        if (index < 0) return;

        playerInfo.RemoveAt(index);
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

        foreach (PlayerInfo a in playerInfo)
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

    #region XP

    public static string UsernameWithLevel(ProfileData profile)
    {
        return $"{profile.username}  LVL {profile.level}";
    }

    private void AwardMatchXp()
    {
        short myKills = (myind >= 0 && playerInfo.Count > myind) ? playerInfo[myind].kills : (short)0;
        short topKills = 0;
        foreach (PlayerInfo a in playerInfo)
        {
            if (a.kills > topKills) topKills = a.kills;
        }

        bool won = topKills > 0 && myKills == topKills;
        if (won) AwardXp(xpMatchComplete + xpWin, "VICTORY");
        else AwardXp(xpMatchComplete, "MATCH COMPLETE");
    }

    private void AwardXp(int amount, string reason)
    {
        ProfileData profile = Launcher.myProfile;
        int levelsGained = profile.AddXp(amount);
        Data.SaveProfile(profile);

        string message = $"+{amount} XP  {reason}";
        if (levelsGained > 0)
        {
            message += $"\nLEVEL UP!  LVL {profile.level}";
            // Let everyone update this player's level on the scoreboard
            ChangeStat_S(PhotonNetwork.LocalPlayer.ActorNumber, 2, (byte)levelsGained);
        }

        if (uiUsername != null) uiUsername.text = UsernameWithLevel(profile);
        ShowMessage(message);
    }

    private void CreateXpPopup()
    {
        // Built at runtime from the existing Kills text so it matches the HUD font and style
        GameObject popupObject = Instantiate(myKills.gameObject, GameObject.Find("Canvas").transform);
        popupObject.name = "XP Popup";

        xpPopup = popupObject.GetComponent<Text>();
        xpPopup.alignment = TextAnchor.MiddleCenter;
        xpPopup.horizontalOverflow = HorizontalWrapMode.Overflow;
        xpPopup.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = popupObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.7f);
        rect.anchorMax = new Vector2(0.5f, 0.7f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(600f, 100f);

        popupObject.SetActive(false);
    }

    private void ShowMessage(string message)
    {
        if (xpPopup == null) return;
        if (xpPopupRoutine != null) StopCoroutine(xpPopupRoutine);
        xpPopupRoutine = StartCoroutine(XpPopup(message));
    }

    private IEnumerator XpPopup(string message)
    {
        const float holdTime = 1.5f;
        const float fadeTime = 0.5f;

        xpPopup.text = message;
        xpPopup.transform.SetAsLastSibling();
        xpPopup.gameObject.SetActive(true);

        Color color = xpPopup.color;
        color.a = 1f;
        xpPopup.color = color;

        yield return new WaitForSeconds(holdTime);

        for (float t = 0f; t < fadeTime; t += Time.deltaTime)
        {
            color.a = 1f - t / fadeTime;
            xpPopup.color = color;
            yield return null;
        }

        xpPopup.gameObject.SetActive(false);
        xpPopupRoutine = null;
    }

    #endregion

    private IEnumerator End(float wait)
    {
        yield return new WaitForSeconds(wait);

        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.LeaveRoom();
    }

    private IEnumerator Spawn(float wait)
    {
        mapcam.SetActive(true);
        Hud.SetActive(false);
        yield return new WaitForSeconds(wait);
        Transform spawn = spawnPoints[Random.Range(0, spawnPoints.Length)];
        mapcam.SetActive(false);
        Hud.SetActive(true);
        PhotonNetwork.Instantiate(playerPrefab, spawn.position, spawn.rotation);
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
