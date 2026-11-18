using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;

[System.Serializable]
public class ProfileData
{
    public string username;
    public int level;
    public int xp;
    public float xsens;
    public float ysens;

    public ProfileData()
    {
        this.username = "DEFAULT USERNAME";
        this.level = 0;
        this.xp = 0;
        this.xsens = 150f;
        this.ysens = 150f;
    }

    // XP needed to go from this level to the next one
    public static int XpToNextLevel(int level)
    {
        return 500 + 250 * level;
    }

    // Adds XP and levels up as many times as the XP allows. Returns the number of levels gained.
    public int AddXp(int amount)
    {
        int levelsGained = 0;
        xp += amount;
        while (xp >= XpToNextLevel(level))
        {
            xp -= XpToNextLevel(level);
            level++;
            levelsGained++;
        }
        return levelsGained;
    }

    public ProfileData (string u, int l, int x, float xs, float ys)
    {
        this.username = u;
        this.level = l;
        this.xp = x;
        this.xsens = xs;
        this.ysens = ys;
    }

}
    [System.Serializable]
    public class MapData
    {
        public string name;
        public int scene;
    }

public class Launcher : MonoBehaviourPunCallbacks
{
    public InputField usernameField;
    public InputField roomnameField;
    public Text mapValue;
    public Slider maxPlayersSlider;
    public Text maxPlayersValue;

    public Slider killsToWinSlider;
    public Text killsToWinValue;

    // Optional: match length in minutes. If not assigned, the map's Manager default is used.
    public Slider matchLengthSlider;
    public Text matchLengthValue;

    public static ProfileData myProfile = new ProfileData();

    public GameObject tabMain;
    public GameObject tabRooms;
    public GameObject tabCreate;

    public GameObject buttontRooms;

    private List<RoomInfo> roomList;

    public MapData[] maps;
    private int currentmap = 0;

    public void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        myProfile = Data.LoadProfile();
        usernameField.text = myProfile.username;
        Connect();
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
        base.OnConnectedToMaster();
    }

    public override void OnJoinedRoom()
    {
        StartGame();

        base.OnJoinedRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Create();
        base.OnJoinRandomFailed(returnCode, message);
    }

    public void Connect ()
    {
        // Photon only matches players with the same game version, so builds with
        // different network code never end up in the same room.
        // Release builds from GitHub Actions set it from the version tag (e.g. v0.2 -> 0.2).
        PhotonNetwork.GameVersion = Application.version;
        if (PhotonNetwork.IsConnected)
        {
            if (!PhotonNetwork.InLobby) PhotonNetwork.JoinLobby();
            return;
        }
        PhotonNetwork.ConnectUsingSettings();
    }

    public void Join ()
    {
        PhotonNetwork.JoinRandomRoom();
    }

    public void Create()
    {
        RoomOptions options = new RoomOptions();
        options.MaxPlayers = (byte) maxPlayersSlider.value;

        options.CustomRoomPropertiesForLobby = new string[] { "map" };

        ExitGames.Client.Photon.Hashtable properties = new ExitGames.Client.Photon.Hashtable();
        properties.Add("map", currentmap);
        properties.Add("killcount", (int)killsToWinSlider.value);
        properties.Add("mapname", maps[currentmap].name);
        if (matchLengthSlider != null) properties.Add("matchlength", Mathf.RoundToInt(matchLengthSlider.value) * 60);
        options.CustomRoomProperties = properties;
        PhotonNetwork.CreateRoom(roomnameField.text, options);
    }

    public void ChangeMap()
    {
        currentmap++;
        if (currentmap >= maps.Length) currentmap = 0;
        mapValue.text = "MAP: " + maps[currentmap].name.ToUpper();
    }

    public void ChangeMaxPlayersSlider (float value)
    {
        maxPlayersValue.text = Mathf.RoundToInt(value).ToString();
    }

    public void ChangeKillsToWinSlider(float value)
    {
        killsToWinValue.text = Mathf.RoundToInt(value).ToString();
    }

    public void ChangeMatchLengthSlider(float value)
    {
        if (matchLengthValue != null) matchLengthValue.text = Mathf.RoundToInt(value) + " MIN";
    }

    public void StartGame ()
    {
        VerifyUsername();
        if (PhotonNetwork.CurrentRoom.PlayerCount == 1)
        {
            Data.SaveProfile(myProfile);
            PhotonNetwork.LoadLevel(maps[currentmap].scene);
        }
    }

    public void TabCloseAll()
    {
        tabMain.SetActive(false);
        tabRooms.SetActive(false);
        tabCreate.SetActive(false);
    }

    public void TabOpenMain()
    {
        TabCloseAll();
        tabMain.SetActive(true);
    }
    public void TabOpenRooms()
    {
        TabCloseAll();
        tabRooms.SetActive(true);
    }
    public void TabOpenCreate()
    {
        TabCloseAll();
        tabCreate.SetActive(true);

        roomnameField.text = "";

        currentmap = 0;
        mapValue.text = "MAP: " + maps[currentmap].name.ToUpper();

        maxPlayersSlider.value = maxPlayersSlider.maxValue;
        maxPlayersValue.text = Mathf.RoundToInt(maxPlayersSlider.value).ToString(); 

        if (matchLengthSlider != null) ChangeMatchLengthSlider(matchLengthSlider.value);
    }

    private void ClearRoomList()
    {
        Transform content = tabRooms.transform.Find("Scroll View/Viewport/Content");
        foreach (Transform a in content) Destroy(a.gameObject);
    }
    private void VerifyUsername()
    {

        if (string.IsNullOrWhiteSpace(usernameField.text))
        {
            myProfile.username = "USER_" + Random.Range(100, 1000);
        }
        else
        {
            myProfile.username = usernameField.text;

        }
    }
    public override void OnRoomListUpdate(List<RoomInfo> list)
    {
        roomList = list;
        ClearRoomList();

        Transform content = tabRooms.transform.Find("Scroll View/Viewport/Content");
        foreach (RoomInfo a in roomList)
        {
            GameObject newRoomButton = Instantiate(buttontRooms, content) as GameObject;

            newRoomButton.transform.Find("Name").GetComponent<Text>().text = a.Name;
            newRoomButton.transform.Find("Players").GetComponent<Text>().text = a.PlayerCount + " / " + a.MaxPlayers;

            if (a.CustomProperties.ContainsKey("map"))
            {
                newRoomButton.transform.Find("Map/Name").GetComponent<Text>().text = maps[(int)a.CustomProperties["map"]].name;
            }
            else
            {
                newRoomButton.transform.Find("Map/Name").GetComponent<Text>().text = "-----";

            }

            newRoomButton.GetComponent<Button>().onClick.AddListener(delegate { JoinRoom(newRoomButton.transform); });

        }

        base.OnRoomListUpdate(roomList);
    }

    public void JoinRoom(Transform button)
    {

        string roomName = button.Find("Name").GetComponent<Text>().text;
        PhotonNetwork.JoinRoom(roomName);
    }

}
