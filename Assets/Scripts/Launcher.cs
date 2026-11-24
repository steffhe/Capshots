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

    // The kills to win, or the match length in minutes when the match is won on time (see Win By below)
    public Slider killsToWinSlider;
    public Text killsToWinValue;

    public static ProfileData myProfile = new ProfileData();

    public GameObject tabMain;
    public GameObject tabRooms;
    public GameObject tabCreate;

    public GameObject buttontRooms;

    private List<SessionInfo> roomList;
    // True while a match is being started or joined, so repeated clicks don't start a second one
    private bool starting;

    public MapData[] maps;
    // The same list for the map scenes (the next-map vote), which have no Launcher
    public static MapData[] Maps;
    private int currentmap = 0;

    private static readonly string[] modeNames = { "FREE FOR ALL", "TEAM DEATHMATCH" };
    private static readonly string[] modeShortNames = { "FFA", "TDM" };
    private int currentmode = 0;
    private Text modeValue;

    // Bots to fill the room with (they make way as people join). 0 = none.
    private const int MaxBots = 7;
    private int currentbots = 0;
    private Text botsValue;
    // How well the bots play (BotLogic.Easy to Hard)
    private int currentBotSkill = BotLogic.Normal;
    private Text botSkillValue;

    // How the match is won: the first to a number of kills, or the most kills when the time runs out.
    // The kills slider row sets whichever it is, and each keeps its own value.
    private static readonly string[] winByNames = { "KILLS", "TIME" };
    private const int WinByKills = 0, WinByTime = 1;
    private const int MaxKillsToWin = 50, DefaultKillsToWin = 20;
    private const int MaxMatchMinutes = 20, DefaultMatchMinutes = 5;
    private int currentWinBy = WinByKills;
    private int killsToWin = DefaultKillsToWin;
    private int matchMinutes = DefaultMatchMinutes;
    private Text winByValue;

    public void Awake()
    {
        Maps = maps;
        Manager.NextMapName = null;
        myProfile = Data.LoadProfile();
        usernameField.text = myProfile.username;
        CreateModeButton();
        CreateBotsButton();
        CreateBotSkillButton();
        CreateWinByButton();
        // Each goes in just above Quit, so they read in this order: Customize, Settings, Quit
        CreateColumnButton("CUSTOMIZE", () => { if (customizeMenu != null) customizeMenu.Open(); });
        GameObject settingsButton = CreateColumnButton("SETTINGS", () => { if (settingsMenu != null) settingsMenu.Open(); });
        // The player model in the menu wears your hat too, in the logo's colour...
        DressMenuModel();
        GameSettings.HatChanged += DressMenuModel;
        // ...and is the same little drone as in a match
        DroneBody.DressSceneModels(PlayerCap.LogoColor);
        ShowWordmark();
        ApplyStyle();
        // The settings panel, built from the styled menu's own button, slider and label
        if (settingsButton != null)
        {
            settingsMenu = SettingsMenu.CreateForMainMenu(tabMain.transform.parent, tabMain, settingsButton, maxPlayersSlider, maxPlayersValue);
            customizeMenu = CustomizeMenu.CreateForMainMenu(tabMain.transform.parent, tabMain, settingsButton, maxPlayersValue);
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

    private void OnDestroy()
    {
        GameSettings.HatChanged -= DressMenuModel;
    }

    // Puts the hat picked in Customize on the menu's player model
    private void DressMenuModel()
    {
        PlayerCap.AttachToSceneHeads(PlayerCap.LogoColor, GameSettings.Hat);
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
        // 0 means no limit: a match won on kills has no clock, one won on time no kill limit
        properties.Add("killcount", currentWinBy == WinByKills ? killsToWin : 0);
        properties.Add("matchlength", currentWinBy == WinByTime ? matchMinutes * 60 : 0);
        properties.Add("mapname", maps[currentmap].name);
        properties.Add("mode", currentmode);
        properties.Add("bots", currentbots);
        properties.Add("botskill", currentBotSkill);
        return properties;
    }

    // The Map button (and the Mode and Bots buttons made from it) opens a list of the choices
    public void ChangeMap()
    {
        System.Collections.Generic.List<int> enabled = new System.Collections.Generic.List<int>();
        for (int i = 0; i < maps.Length; i++) if (maps[i].enabled) enabled.Add(i);
        if (enabled.Count == 0) return;
        string[] options = new string[enabled.Count];
        for (int i = 0; i < options.Length; i++) options[i] = maps[enabled[i]].name.ToUpper();
        OptionPicker.Open(ButtonOf(mapValue), options, enabled.IndexOf(currentmap), i =>
        {
            currentmap = enabled[i];
            mapValue.text = UITheme.Option("MAP", maps[currentmap].name.ToUpper());
        });
    }

    private static Button ButtonOf(Text label) => label != null ? label.GetComponentInParent<Button>() : null;

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
        OptionPicker.Open(ButtonOf(modeValue), modeNames, currentmode, i =>
        {
            currentmode = i;
            RefreshModeText();
        });
    }

    private void RefreshModeText()
    {
        if (modeValue != null) modeValue.text = UITheme.Option("MODE", modeNames[currentmode]);
    }

    public void ChangeBots()
    {
        string[] options = new string[MaxBots + 1];
        for (int i = 0; i <= MaxBots; i++) options[i] = i == 0 ? "OFF" : i.ToString();
        OptionPicker.Open(ButtonOf(botsValue), options, currentbots, i =>
        {
            currentbots = i;
            RefreshBotsText();
        });
    }

    private void RefreshBotsText()
    {
        if (botsValue != null) botsValue.text = UITheme.Option("BOTS", currentbots == 0 ? "OFF" : currentbots.ToString());
    }

    public void ChangeBotSkill()
    {
        OptionPicker.Open(ButtonOf(botSkillValue), BotLogic.SkillNames, currentBotSkill, i =>
        {
            currentBotSkill = i;
            RefreshBotSkillText();
        });
    }

    private void RefreshBotSkillText()
    {
        if (botSkillValue != null) botSkillValue.text = UITheme.Option("BOT SKILL", BotLogic.SkillNames[currentBotSkill]);
    }

    public void ChangeWinBy()
    {
        OptionPicker.Open(ButtonOf(winByValue), winByNames, currentWinBy, i =>
        {
            currentWinBy = i;
            RefreshWinBy();
        });
    }

    // The Win By button, and the slider row under it showing the kills to win or the match length
    private void RefreshWinBy()
    {
        if (winByValue != null) winByValue.text = UITheme.Option("WIN BY", winByNames[currentWinBy]);
        if (killsToWinSlider == null) return;

        bool time = currentWinBy == WinByTime;
        Text label = killsToWinSlider.transform.parent != null ? killsToWinSlider.transform.parent.Find("Text")?.GetComponent<Text>() : null;
        if (label != null) label.text = time ? "MATCH LENGTH" : "KILLS TO WIN";
        // Without calling ChangeKillsToWinSlider, which would save the value under the other setting
        killsToWinSlider.minValue = 1;
        killsToWinSlider.maxValue = time ? MaxMatchMinutes : MaxKillsToWin;
        killsToWinSlider.SetValueWithoutNotify(time ? matchMinutes : killsToWin);
        RefreshKillsToWinText();
    }

    private void RefreshKillsToWinText()
    {
        if (killsToWinValue == null) return;
        killsToWinValue.text = currentWinBy == WinByTime ? matchMinutes + " MIN" : killsToWin.ToString();
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
    private CustomizeMenu customizeMenu;

    // Adds a button (Customize, then Settings) to the main tab's column, above Quit (or at the end),
    // by copying one of its buttons. Returns it, or null if the column isn't there.
    private GameObject CreateColumnButton(string text, UnityEngine.Events.UnityAction onClick)
    {
        Transform anchor = tabMain != null ? tabMain.transform.Find("Anchor") : null;
        if (anchor == null) return null;

        Button template = null;
        Transform quit = null;
        foreach (Transform child in anchor)
        {
            Button candidate = child.GetComponent<Button>();
            Text label = candidate != null ? candidate.GetComponentInChildren<Text>(true) : null;
            if (label == null) continue;
            if (template == null) template = candidate;
            if (label.text.ToUpperInvariant().Contains("QUIT")) quit = child;
        }
        if (template == null) return null;

        GameObject copy = Instantiate(template.gameObject, anchor);
        copy.name = text.Substring(0, 1) + text.Substring(1).ToLowerInvariant();
        copy.transform.SetSiblingIndex(quit != null ? quit.GetSiblingIndex() : anchor.childCount - 1);
        Button button = copy.GetComponent<Button>();
        // The copy still opens whatever the original did: switch that off, keep anything else (like a click sound)
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) is Launcher)
            {
                button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
            }
        }
        button.onClick.AddListener(onClick);
        copy.GetComponentInChildren<Text>(true).text = text;
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

    // And a "Bot Skill" button under that
    private void CreateBotSkillButton()
    {
        botSkillValue = CopyMapRow(botsValue != null ? botsValue : mapValue, "Bot Skill", ChangeBotSkill);
        RefreshBotSkillText();
    }

    // And a "Win By" button under that
    private void CreateWinByButton()
    {
        Text above = botSkillValue != null ? botSkillValue : botsValue;
        winByValue = CopyMapRow(above != null ? above : mapValue, "Win By", ChangeWinBy);
        RefreshWinBy();
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

    // The kills slider row: the kills to win, or the match length in minutes
    public void ChangeKillsToWinSlider(float value)
    {
        if (currentWinBy == WinByTime) matchMinutes = Mathf.RoundToInt(value);
        else killsToWin = Mathf.RoundToInt(value);
        RefreshKillsToWinText();
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

        currentWinBy = WinByKills;
        killsToWin = DefaultKillsToWin;
        matchMinutes = DefaultMatchMinutes;
        RefreshWinBy();
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
                string goal = MatchGoal(a);
                if (goal != null) roomMap += "  ·  " + goal;
            }

            GameObject newRoomButton = CreateRoomRow(a.Name, a.PlayerCount + " / " + a.MaxPlayers, roomMap);
            newRoomButton.GetComponent<Button>().onClick.AddListener(delegate { JoinRoom(newRoomButton.transform); });
            shown++;
        }
        ShowRoomsEmptyState(shown == 0);
    }

    // How a listed match is won, short: "20 KILLS" or "5 MIN"
    private static string MatchGoal(SessionInfo session)
    {
        var properties = session.Properties;
        if (properties.TryGetValue("killcount", out SessionProperty kills) && (int)kills > 0) return (int)kills + " KILLS";
        if (properties.TryGetValue("matchlength", out SessionProperty length) && (int)length > 0) return Mathf.Max(1, (int)length / 60) + " MIN";
        return null;
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
