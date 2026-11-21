using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using UnityEngine.UI;

    [System.Serializable]
    public class MapData
    {
        public string name;
        public int scene;
        // Unticked maps are hidden from the create-room picker (e.g. while still in progress).
        public bool enabled = true;
    }

public class Launcher : MonoBehaviour
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

    private List<SessionInfo> roomList;
    // True while a match is being started or joined, so repeated clicks don't start a second one
    private bool starting;

    public MapData[] maps;
    private int currentmap = 0;

    private static readonly string[] modeNames = { "FREE FOR ALL", "TEAM DEATHMATCH" };
    private static readonly string[] modeShortNames = { "FFA", "TDM" };
    private int currentmode = 0;
    private Text modeValue;

    public void Awake()
    {
        myProfile = Data.LoadProfile();
        usernameField.text = myProfile.username;
        CreateModeButton();
        // The player model in the menu wears a cap too, in the logo's colour
        PlayerCap.AttachToSceneHeads(PlayerCap.LogoColor);
        ShowWordmark();
        Connect();
    }

    private void OnEnable()
    {
        NetworkSession.SessionListUpdated += OnRoomListUpdate;
    }

    private void OnDisable()
    {
        NetworkSession.SessionListUpdated -= OnRoomListUpdate;
    }

    // Joins the session lobby so the room list fills in. The app version (set from the release
    // tag by GitHub Actions) keeps players on different builds apart.
    public void Connect ()
    {
        NetworkSession.JoinLobby();
    }

    // Quick play: join any open match, or host one with the create tab's current settings
    public async void Join ()
    {
        if (starting) return;
        starting = true;
        PrepareProfile();
        // Quick play from the main tab never opened the create tab, so make sure the map is a playable one
        if (!maps[currentmap].enabled) currentmap = NextEnabledMap(0);
        StartGameResult result = await NetworkSession.JoinRandomOrHost((int)maxPlayersSlider.value, RoomProperties(), maps[currentmap].scene);
        starting = false;
        if (!result.Ok) Debug.LogWarning("Quick play failed: " + result.ShutdownReason);
    }

    public async void Create()
    {
        if (starting) return;
        starting = true;
        PrepareProfile();
        StartGameResult result = await NetworkSession.Host(roomnameField.text, (int)maxPlayersSlider.value, RoomProperties(), maps[currentmap].scene);
        starting = false;
        if (!result.Ok) Debug.LogWarning("Couldn't create the room: " + result.ShutdownReason);
    }

    // The match settings, shared with everyone through the session (and shown in the room list)
    private Dictionary<string, SessionProperty> RoomProperties()
    {
        Dictionary<string, SessionProperty> properties = new Dictionary<string, SessionProperty>();
        properties.Add("map", currentmap);
        properties.Add("killcount", (int)killsToWinSlider.value);
        properties.Add("mapname", maps[currentmap].name);
        properties.Add("mode", currentmode);
        if (matchLengthSlider != null) properties.Add("matchlength", Mathf.RoundToInt(matchLengthSlider.value) * 60);
        return properties;
    }

    public void ChangeMap()
    {
        currentmap = NextEnabledMap(currentmap + 1);
        mapValue.text = "MAP: " + maps[currentmap].name.ToUpper();
    }

    // First enabled map at or after 'start' (wrapping around). Falls back to 0 if none are enabled.
    private int NextEnabledMap(int start)
    {
        for (int i = 0; i < maps.Length; i++)
        {
            int index = (start + i) % maps.Length;
            if (maps[index].enabled) return index;
        }
        return 0;
    }

    public void ChangeMode()
    {
        currentmode = (currentmode + 1) % modeNames.Length;
        RefreshModeText();
    }

    private void RefreshModeText()
    {
        if (modeValue != null) modeValue.text = "MODE: " + modeNames[currentmode];
    }

    // Replaces the "Capshots" title text on each menu tab with the wordmark logo
    // (Assets/Resources/UI/CapshotsWordmark.png). Done here so the scene needn't change.
    private void ShowWordmark()
    {
        Sprite wordmark = Resources.Load<Sprite>("UI/CapshotsWordmark");
        if (wordmark == null) return; // keep the text title

        foreach (Text title in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (title.name != "Logo" || title.text != "Capshots") continue;

            // The Text and Image can't share a GameObject, so the logo goes on a child centred on the title
            GameObject logo = new GameObject("Wordmark", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)logo.transform;
            rect.SetParent(title.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            // A little wider than the old text title; the height follows the logo's shape
            float width = 420f;
            rect.sizeDelta = new Vector2(width, width * wordmark.rect.height / wordmark.rect.width);

            Image image = logo.GetComponent<Image>();
            image.sprite = wordmark;
            image.preserveAspect = true;
            image.raycastTarget = false;

            title.enabled = false;
        }
    }

    // Adds a "Mode" button under the "Map" button in the create-room tab by copying it,
    // and moves the rows below it down, so the menu scene doesn't need editing
    private void CreateModeButton()
    {
        if (mapValue == null) return;

        RectTransform mapRow = mapValue.transform.parent as RectTransform;
        if (mapRow == null || mapRow.parent == null) return;

        const float rowSpacing = 60f;
        float mapY = mapRow.anchoredPosition.y;

        foreach (Transform sibling in mapRow.parent)
        {
            RectTransform row = sibling as RectTransform;
            if (row != null && row != mapRow && row.anchoredPosition.y < mapY - 1f)
            {
                row.anchoredPosition -= new Vector2(0f, rowSpacing);
            }
        }

        GameObject modeRow = Instantiate(mapRow.gameObject, mapRow.parent);
        modeRow.name = "Mode";
        modeRow.transform.SetSiblingIndex(mapRow.GetSiblingIndex() + 1);
        ((RectTransform)modeRow.transform).anchoredPosition = mapRow.anchoredPosition - new Vector2(0f, rowSpacing);

        modeValue = modeRow.GetComponentInChildren<Text>();

        Button button = modeRow.GetComponent<Button>();
        if (button != null)
        {
            // The copy still calls ChangeMap from the Inspector: switch that off, keep anything else (like a click sound)
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentMethodName(i) == nameof(ChangeMap))
                {
                    button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
                }
            }
            button.onClick.AddListener(ChangeMode);
        }

        RefreshModeText();
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

    // The name typed in the menu is used for the match and saved. The host's StartGame loads the
    // map, and Fusion takes everyone who joins to the same scene.
    private void PrepareProfile()
    {
        VerifyUsername();
        Data.SaveProfile(myProfile);
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

        currentmap = NextEnabledMap(0);
        mapValue.text = "MAP: " + maps[currentmap].name.ToUpper();

        currentmode = 0;
        RefreshModeText();

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
    private void OnRoomListUpdate(List<SessionInfo> list)
    {
        if (tabRooms == null) return;

        roomList = list;
        ClearRoomList();

        Transform content = tabRooms.transform.Find("Scroll View/Viewport/Content");
        foreach (SessionInfo a in roomList)
        {
            if (!a.IsVisible) continue;

            GameObject newRoomButton = Instantiate(buttontRooms, content) as GameObject;

            newRoomButton.transform.Find("Name").GetComponent<Text>().text = a.Name;
            newRoomButton.transform.Find("Players").GetComponent<Text>().text = a.PlayerCount + " / " + a.MaxPlayers;

            SessionProperty map, mode;
            if (a.Properties != null && a.Properties.TryGetValue("map", out map) && (int)map >= 0 && (int)map < maps.Length)
            {
                string roomMap = maps[(int)map].name;
                if (a.Properties.TryGetValue("mode", out mode)) roomMap += "  ·  " + modeShortNames[(int)mode % modeShortNames.Length];
                newRoomButton.transform.Find("Map/Name").GetComponent<Text>().text = roomMap;
            }
            else
            {
                newRoomButton.transform.Find("Map/Name").GetComponent<Text>().text = "-----";

            }

            newRoomButton.GetComponent<Button>().onClick.AddListener(delegate { JoinRoom(newRoomButton.transform); });

        }
    }

    public async void JoinRoom(Transform button)
    {
        if (starting) return;
        starting = true;
        PrepareProfile();
        string roomName = button.Find("Name").GetComponent<Text>().text;
        StartGameResult result = await NetworkSession.Join(roomName);
        starting = false;
        if (!result.Ok) Debug.LogWarning($"Couldn't join {roomName}: {result.ShutdownReason}");
    }

}
