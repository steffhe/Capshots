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

public partial class Launcher : MonoBehaviour
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

    // Bots to fill the room with (they make way as people join). 0 = none.
    private const int MaxBots = 7;
    private int currentbots = 0;
    private Text botsValue;

    public void Awake()
    {
        myProfile = Data.LoadProfile();
        usernameField.text = myProfile.username;
        CreateModeButton();
        CreateBotsButton();
        GameObject settingsButton = CreateSettingsButton();
        // The player model in the menu wears a cap too, in the logo's colour...
        PlayerCap.AttachToSceneHeads(PlayerCap.LogoColor);
        // ...and is the same little drone as in a match
        DroneBody.DressSceneModels(PlayerCap.LogoColor);
        ShowWordmark();
        ApplyStyle();
        // The settings panel, built from the styled menu's own button, slider and label
        if (settingsButton != null)
        {
            settingsMenu = SettingsMenu.CreateForMainMenu(tabMain.transform.parent, tabMain, settingsButton, maxPlayersSlider, maxPlayersValue);
        }
        MenuScenery.Create();
        Connect();
        // Back from a match (or a join that failed): reveal the menu under the loading screen
        LoadingScreen.Hide(0.15f);
        // Build it now (after the font is set up by ApplyStyle) rather than on the first Play click
        LoadingScreen.Prepare();
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
        LoadingScreen.Show("FINDING A MATCH");
        StartGameResult result = await NetworkSession.JoinRandomOrHost((int)maxPlayersSlider.value, RoomProperties(), maps[currentmap].scene);
        starting = false;
        if (!result.Ok)
        {
            Debug.LogWarning("Quick play failed: " + result.ShutdownReason);
            LoadingScreen.Fail("COULDN'T FIND A MATCH");
        }
    }

    public async void Create()
    {
        if (starting) return;
        starting = true;
        PrepareProfile();
        LoadingScreen.Show("CREATING MATCH", maps[currentmap].name, LoadingScreen.ModeName(currentmode));
        StartGameResult result = await NetworkSession.Host(roomnameField.text, (int)maxPlayersSlider.value, RoomProperties(), maps[currentmap].scene);
        starting = false;
        if (!result.Ok)
        {
            Debug.LogWarning("Couldn't create the room: " + result.ShutdownReason);
            LoadingScreen.Fail("COULDN'T CREATE THE MATCH");
        }
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
        properties.Add("bots", currentbots);
        return properties;
    }

    public void ChangeMap()
    {
        currentmap = NextEnabledMap(currentmap + 1);
        mapValue.text = UITheme.Option("MAP", maps[currentmap].name.ToUpper());
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
        if (modeValue != null) modeValue.text = UITheme.Option("MODE", modeNames[currentmode]);
    }

    public void ChangeBots()
    {
        currentbots = (currentbots + 1) % (MaxBots + 1);
        RefreshBotsText();
    }

    private void RefreshBotsText()
    {
        if (botsValue != null) botsValue.text = UITheme.Option("BOTS", currentbots == 0 ? "OFF" : currentbots.ToString());
    }

    // Replaces the "Capshots" title text on each menu tab with the wordmark logo
    // (Assets/Resources/UI/CapshotsWordmark.png). Done here so the scene needn't change.
    private void ShowWordmark()
    {
        Sprite wordmark = Resources.Load<Sprite>("UI/CapshotsWordmark");
        if (wordmark == null) return; // keep the text title

        foreach (Text title in tabMain.transform.parent.GetComponentsInChildren<Text>(true))
        {
            if (title.name != "Logo" || title.text != "Capshots") continue;

            // The Text and Image can't share a GameObject, so the logo goes on a child centred on the title
            GameObject logo = new GameObject("Wordmark", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)logo.transform;
            rect.SetParent(title.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            // A little wider than the old text title; the height follows the logo's shape
            float width = 380f;
            rect.sizeDelta = new Vector2(width, width * wordmark.rect.height / wordmark.rect.width);

            Image image = logo.GetComponent<Image>();
            image.sprite = wordmark;
            image.preserveAspect = true;
            image.raycastTarget = false;

            title.enabled = false;
        }
    }

    private SettingsMenu settingsMenu;

    // Adds a "Settings" button to the main tab's column, above Quit (or at the end), by copying
    // one of its buttons. Returns it, or null if the column isn't there.
    private GameObject CreateSettingsButton()
    {
        Transform anchor = tabMain != null ? tabMain.transform.Find("Anchor") : null;
        if (anchor == null) return null;

        Button template = null;
        Transform quit = null;
        foreach (Transform child in anchor)
        {
            Button button = child.GetComponent<Button>();
            Text label = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (label == null) continue;
            if (template == null) template = button;
            if (label.text.ToUpperInvariant().Contains("QUIT")) quit = child;
        }
        if (template == null) return null;

        GameObject copy = Instantiate(template.gameObject, anchor);
        copy.name = "Settings";
        copy.transform.SetSiblingIndex(quit != null ? quit.GetSiblingIndex() : anchor.childCount - 1);
        Button settings = copy.GetComponent<Button>();
        // The copy still opens whatever the original did: switch that off, keep anything else (like a click sound)
        for (int i = 0; i < settings.onClick.GetPersistentEventCount(); i++)
        {
            if (settings.onClick.GetPersistentTarget(i) is Launcher)
            {
                settings.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
            }
        }
        settings.onClick.AddListener(() => { if (settingsMenu != null) settingsMenu.Open(); });
        copy.GetComponentInChildren<Text>(true).text = "SETTINGS";
        return copy;
    }

    // Adds a "Mode" button under the "Map" button in the create-room tab by copying it,
    // and moves the rows below it down, so the menu scene doesn't need editing
    private void CreateModeButton()
    {
        modeValue = CopyMapRow(mapValue, "Mode", ChangeMode);
        RefreshModeText();
    }

    // And a "Bots" button under that
    private void CreateBotsButton()
    {
        botsValue = CopyMapRow(modeValue != null ? modeValue : mapValue, "Bots", ChangeBots);
        RefreshBotsText();
    }

    // A copy of the "Map" button placed under the row holding 'above', calling onClick instead of
    // ChangeMap. Returns its label.
    private Text CopyMapRow(Text above, string rowName, UnityEngine.Events.UnityAction onClick)
    {
        if (mapValue == null || above == null) return null;

        RectTransform mapRow = mapValue.transform.parent as RectTransform;
        RectTransform aboveRow = above.transform.parent as RectTransform;
        if (mapRow == null || aboveRow == null || aboveRow.parent == null) return null;

        const float rowSpacing = 60f;
        float aboveY = aboveRow.anchoredPosition.y;

        foreach (Transform sibling in aboveRow.parent)
        {
            RectTransform row = sibling as RectTransform;
            if (row != null && row != aboveRow && row.anchoredPosition.y < aboveY - 1f)
            {
                row.anchoredPosition -= new Vector2(0f, rowSpacing);
            }
        }

        GameObject newRow = Instantiate(mapRow.gameObject, aboveRow.parent);
        newRow.name = rowName;
        newRow.transform.SetSiblingIndex(aboveRow.GetSiblingIndex() + 1);
        ((RectTransform)newRow.transform).anchoredPosition = aboveRow.anchoredPosition - new Vector2(0f, rowSpacing);

        Button button = newRow.GetComponent<Button>();
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
            button.onClick.AddListener(onClick);
        }

        return newRow.GetComponentInChildren<Text>();
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
        mapValue.text = UITheme.Option("MAP", maps[currentmap].name.ToUpper());

        currentmode = 0;
        RefreshModeText();

        maxPlayersSlider.value = maxPlayersSlider.maxValue;
        maxPlayersValue.text = Mathf.RoundToInt(maxPlayersSlider.value).ToString(); 

        if (matchLengthSlider != null) ChangeMatchLengthSlider(matchLengthSlider.value);
    }

    private void ClearRoomList()
    {
        foreach (Transform a in RoomListContent) Destroy(a.gameObject);
    }

    // The room list's scroll content (found by component, since the menu layout is rearranged at runtime)
    private Transform RoomListContent => tabRooms.GetComponentInChildren<ScrollRect>(true).content;
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

        int shown = 0;
        foreach (SessionInfo a in roomList)
        {
            if (!a.IsVisible) continue;

            string roomMap = "-----";
            SessionProperty map, mode;
            if (a.Properties != null && a.Properties.TryGetValue("map", out map) && (int)map >= 0 && (int)map < maps.Length)
            {
                roomMap = maps[(int)map].name;
                if (a.Properties.TryGetValue("mode", out mode)) roomMap += "  ·  " + modeShortNames[(int)mode % modeShortNames.Length];
            }

            GameObject newRoomButton = CreateRoomRow(a.Name, a.PlayerCount + " / " + a.MaxPlayers, roomMap);
            newRoomButton.GetComponent<Button>().onClick.AddListener(delegate { JoinRoom(newRoomButton.transform); });
            shown++;
        }
        ShowRoomsEmptyState(shown == 0);
    }

    // One row of the room list. JoinRoom reads the room's name back from the row's "Name" text.
    private GameObject CreateRoomRow(string roomName, string players, string map)
    {
        GameObject row = Instantiate(buttontRooms, RoomListContent);
        row.transform.Find("Name").GetComponent<Text>().text = roomName;
        row.transform.Find("Players").GetComponent<Text>().text = players;
        row.transform.Find("Map/Name").GetComponent<Text>().text = map;
        StyleRoomRow(row);
        return row;
    }

    public async void JoinRoom(Transform button)
    {
        if (starting) return;
        starting = true;
        PrepareProfile();
        string roomName = button.Find("Name").GetComponent<Text>().text;
        LoadingScreen.Show("JOINING " + roomName.ToUpperInvariant());
        StartGameResult result = await NetworkSession.Join(roomName);
        starting = false;
        if (!result.Ok)
        {
            Debug.LogWarning($"Couldn't join {roomName}: {result.ShutdownReason}");
            LoadingScreen.Fail(result.ShutdownReason == ShutdownReason.GameIsFull ? "THAT MATCH IS FULL" : "COULDN'T JOIN THE MATCH");
        }
    }

}
