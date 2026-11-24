using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

// Owns the Fusion NetworkRunner: the session lobby, hosting and joining matches, sending the
// local player's input and leaving. Lives across scene loads; there is at most one runner at a time.
public class NetworkSession : MonoBehaviour, INetworkRunnerCallbacks
{
    public const int MenuScene = 0;

    private static NetworkSession instance;
    private NetworkRunner runner;
    private readonly InputCollector input = new InputCollector();
    private bool leaving;
    // The host left and this player is reconnecting to the new one (see OnHostMigration)
    private bool migrating;
    private bool menuQueued;

    public static NetworkRunner Runner => instance != null ? instance.runner : null;
    public static bool InSession => Runner != null && Runner.IsRunning && Runner.SessionInfo.IsValid;

    // The sessions listed in the lobby, newest first as Photon sends them
    public static event Action<List<SessionInfo>> SessionListUpdated;

    private static NetworkSession Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("Network Session");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<NetworkSession>();
            }
            return instance;
        }
    }

    // Photon only matches players with the same app version, so builds with different network
    // code never end up in the same session. Release builds set it from the version tag.
    private static FusionAppSettings AppSettings()
    {
        FusionAppSettings settings = PhotonAppSettings.Global.AppSettings.GetCopy();
        settings.AppVersion = Application.version;
        return settings;
    }

    private NetworkRunner CreateRunner()
    {
        // Only one runner at a time. Shutting down also destroys its GameObject.
        if (runner != null) runner.Shutdown();

        GameObject go = new GameObject("Network Runner");
        DontDestroyOnLoad(go);
        runner = go.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;
        go.AddComponent<NetworkSceneManagerDefault>();
        runner.AddCallbacks(this);
        leaving = false;
        return runner;
    }

    // Joins the lobby so the menu can list open sessions. Called by the main menu.
    public static async void JoinLobby()
    {
        NetworkSession session = Instance;
        // Already in the lobby, joining it, or in a match
        if (session.runner != null && !session.leaving) return;

        NetworkRunner lobbyRunner = session.CreateRunner();
        StartGameResult result = await lobbyRunner.JoinSessionLobby(SessionLobby.ClientServer, customAppSettings: AppSettings());
        if (!result.Ok)
        {
            Debug.LogWarning($"Couldn't join the session lobby: {result.ShutdownReason} {result.ErrorMessage}");
            if (session.runner == lobbyRunner) session.runner = null;
        }
    }

    // Starts a new match as its host
    public static Task<StartGameResult> Host(string sessionName, int maxPlayers, Dictionary<string, SessionProperty> properties, int sceneIndex)
    {
        return Start(GameMode.Host, string.IsNullOrWhiteSpace(sessionName) ? null : sessionName, maxPlayers, properties, sceneIndex);
    }

    // Joins any open match, or hosts a new one with these settings if there is none
    public static Task<StartGameResult> JoinRandomOrHost(int maxPlayers, Dictionary<string, SessionProperty> properties, int sceneIndex)
    {
        return Start(GameMode.AutoHostOrClient, null, maxPlayers, properties, sceneIndex);
    }

    // Joins the named match
    public static Task<StartGameResult> Join(string sessionName)
    {
        return Start(GameMode.Client, sessionName, 0, null, -1);
    }

    // A practice match against bots, offline: Fusion's single-player mode, with no connection to
    // Photon at all, so it works with nobody else online (or no internet)
    public static Task<StartGameResult> Practice(Dictionary<string, SessionProperty> properties, int sceneIndex)
    {
        practiceProperties = new Dictionary<string, SessionProperty>(properties);
        // Room for the player and up to 7 bots (bots fill up to the room size)
        return Start(GameMode.Single, "Practice", 8, properties, sceneIndex);
    }

    // A practice match's settings. A single-player session isn't in Photon's cloud, so its
    // properties are kept here instead of trusting SessionInfo to have them.
    private static Dictionary<string, SessionProperty> practiceProperties;

    public static bool IsPractice(NetworkRunner r) => r != null && r.IsRunning && r.IsSinglePlayer;

    // The match's settings (map, mode, bots and how it's won): the session's properties, or a practice
    // match's. Null if there are none.
    public static IReadOnlyDictionary<string, SessionProperty> MatchProperties(NetworkRunner r)
    {
        if (r == null) return null;
        if (r.IsSinglePlayer) return practiceProperties;
        return r.SessionInfo.IsValid ? r.SessionInfo.Properties : null;
    }

    // Changes the match's settings (the next map after a vote)
    public static void UpdateMatchProperties(NetworkRunner r, Dictionary<string, SessionProperty> properties)
    {
        if (r == null) return;
        if (r.IsSinglePlayer) practiceProperties = new Dictionary<string, SessionProperty>(properties);
        else r.SessionInfo.UpdateCustomProperties(properties);
    }

    // Whether the room shows in the room list and lets people in. Nothing to do in practice.
    public static void SetOpen(NetworkRunner r, bool open)
    {
        if (r == null || r.IsSinglePlayer || !r.SessionInfo.IsValid) return;
        r.SessionInfo.IsOpen = open;
        r.SessionInfo.IsVisible = open;
    }

    private static async Task<StartGameResult> Start(GameMode mode, string sessionName, int maxPlayers, Dictionary<string, SessionProperty> properties, int sceneIndex)
    {
        NetworkSession session = Instance;
        // The runner that joined the lobby starts the match too; make a new one only if there is none
        // or the old one is on its way out. A practice match never touches the lobby: always a new one.
        if (mode != GameMode.Single) practiceProperties = null;
        NetworkRunner startRunner = mode != GameMode.Single && session.runner != null && !session.leaving && !session.runner.SessionInfo.IsValid
            ? session.runner
            : session.CreateRunner();

        StartGameArgs args = new StartGameArgs
        {
            GameMode = mode,
            SessionName = sessionName,
            SessionProperties = properties,
            SceneManager = startRunner.GetComponent<NetworkSceneManagerDefault>(),
            CustomPhotonAppSettings = AppSettings(),
        };
        if (maxPlayers > 0) args.PlayerCount = maxPlayers;
        // Single-player mode makes no connection, so it needs no app settings or session name
        if (mode == GameMode.Single) args.CustomPhotonAppSettings = null;
        // Only the host loads the map; clients follow the host's scene
        if (sceneIndex >= 0)
        {
            NetworkSceneInfo scene = new NetworkSceneInfo();
            scene.AddSceneRef(SceneRef.FromIndex(sceneIndex), LoadSceneMode.Single);
            args.Scene = scene;
        }

        StartGameResult result = await startRunner.StartGame(args);
        if (!result.Ok)
        {
            Debug.LogWarning($"Couldn't start the session: {result.ShutdownReason} {result.ErrorMessage}");
            // A failed start shuts the runner down, so get the room list back
            if (session.runner == startRunner) session.runner = null;
            if (SceneManager.GetActiveScene().buildIndex == MenuScene) JoinLobby();
        }
        return result;
    }

    // Leaves the match and goes back to the main menu
    public static void Leave()
    {
        NetworkSession session = instance;
        // Fade to the loading screen first, so the match doesn't just cut to the menu
        bool covered = LoadingScreen.Visible;
        LoadingScreen.Show("RETURNING TO MENU");

        if (session != null && session.migrating)
        {
            // OnHostMigration sees this once the old session has shut down or the new one has started
            session.leaving = true;
            return;
        }
        if (session == null || session.runner == null)
        {
            SceneManager.LoadScene(MenuScene);
            return;
        }
        if (session.leaving) return;

        session.leaving = true;
        session.StartCoroutine(session.ShutdownAfter(covered ? 0f : 0.3f));
    }

    private System.Collections.IEnumerator ShutdownAfter(float seconds)
    {
        if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
        if (runner != null) runner.Shutdown();
    }

    private void Update()
    {
        input.Update();
    }

    #region INetworkRunnerCallbacks

    public void OnInput(NetworkRunner runner, NetworkInput networkInput)
    {
        networkInput.Set(input.Take());
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        SessionListUpdated?.Invoke(sessionList);
    }

    public void OnShutdown(NetworkRunner shutdownRunner, ShutdownReason reason)
    {
        if (shutdownRunner == runner) runner = null;

        // The host left and this player is moving to the new host: the match carries on
        if (reason == ShutdownReason.HostMigration) return;

        // Shutting down only the lobby runner (to start a match) needs nothing else
        if (SceneManager.GetActiveScene().buildIndex == MenuScene) return;
        Manager.ClearHostMigration();

        // The host left or the connection dropped: say so for a moment, then go back to the menu
        if (!leaving && Manager.Instance != null && Manager.Instance.ShowDisconnected(reason))
        {
            StartCoroutine(LoadMenuAfter(3f));
            return;
        }

        // Fade out first, unless leaving already covered the screen
        if (!LoadingScreen.Visible)
        {
            StartCoroutine(LoadMenuAfter(0f));
            return;
        }

        SceneManager.LoadScene(MenuScene);
    }

    private System.Collections.IEnumerator LoadMenuAfter(float seconds)
    {
        if (menuQueued) yield break;
        menuQueued = true;
        yield return new WaitForSecondsRealtime(seconds);
        menuQueued = false;
        LoadingScreen.Show("RETURNING TO MENU");
        yield return new WaitForSecondsRealtime(0.3f);
        if (runner == null) SceneManager.LoadScene(MenuScene);
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"Disconnected from the host: {reason}");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogWarning($"Couldn't connect to the host: {reason}");
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    // Once a match is joined, the loading screen can say which map is loading
    public void OnSceneLoadStart(NetworkRunner runner)
    {
        if (SceneManager.GetActiveScene().buildIndex != MenuScene || !LoadingScreen.Visible) return;
        LoadingScreen.SetMatch(runner);
        LoadingScreen.SetStatus("LOADING MAP");
    }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    #endregion

    #region Host migration

    // The host left mid-match and Photon has picked a new one: this player, or someone else. The old
    // session shuts down, a new runner starts with the token, the map reloads and Manager rebuilds
    // the match from what everyone remembers of it (Manager.HostMigration.cs).
    public async void OnHostMigration(NetworkRunner oldRunner, HostMigrationToken token)
    {
        Manager manager = Manager.Instance;
        // Leaving anyway, or the match is over (results and map voting): end it as before
        if (leaving || manager == null || manager.MatchOver)
        {
            await oldRunner.Shutdown();
            return;
        }

        migrating = true;
        bool hosting = token.GameMode == GameMode.Host;
        manager.SaveForHostMigration();
        LoadingScreen.Show(hosting ? "THE HOST LEFT: YOU'RE HOSTING NOW" : "THE HOST LEFT: MOVING TO THE NEW HOST");
        if (oldRunner.SessionInfo.IsValid) LoadingScreen.SetMatch(oldRunner);
        int scene = SceneManager.GetActiveScene().buildIndex;

        await oldRunner.Shutdown(shutdownReason: ShutdownReason.HostMigration);

        if (leaving)
        {
            migrating = false;
            Manager.ClearHostMigration();
            SceneManager.LoadScene(MenuScene);
            return;
        }

        NetworkRunner newRunner = CreateRunner();
        StartGameArgs args = new StartGameArgs
        {
            // The game mode and session come with the token
            HostMigrationToken = token,
            HostMigrationResume = ResumeAfterHostMigration,
            SceneManager = newRunner.GetComponent<NetworkSceneManagerDefault>(),
            CustomPhotonAppSettings = AppSettings(),
        };
        // The new host reloads the map; everyone else follows its scene
        if (hosting)
        {
            NetworkSceneInfo sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(SceneRef.FromIndex(scene), LoadSceneMode.Single);
            args.Scene = sceneInfo;
        }

        StartGameResult result = await newRunner.StartGame(args);
        migrating = false;

        if (!result.Ok)
        {
            Debug.LogWarning($"Couldn't carry on after the host left: {result.ShutdownReason} {result.ErrorMessage}");
            Manager.ClearHostMigration();
            if (runner == newRunner) runner = null;
            LoadingScreen.SetStatus("THE HOST LEFT THE MATCH");
            if (SceneManager.GetActiveScene().buildIndex != MenuScene) StartCoroutine(LoadMenuAfter(2f));
            return;
        }

        // Pressed Leave while reconnecting
        if (leaving)
        {
            leaving = false;
            Leave();
        }
    }

    // Called on the new host to restore the old host's objects. Nothing is restored from Fusion's
    // snapshot: players reconnect with new PlayerRefs, so Manager rebuilds the match itself and
    // everyone respawns.
    private void ResumeAfterHostMigration(NetworkRunner resumedRunner)
    {
        Debug.Log("Hosting the match after the host left");
    }

    #endregion
}
