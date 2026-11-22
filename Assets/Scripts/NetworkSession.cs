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

    private static async Task<StartGameResult> Start(GameMode mode, string sessionName, int maxPlayers, Dictionary<string, SessionProperty> properties, int sceneIndex)
    {
        NetworkSession session = Instance;
        // The runner that joined the lobby starts the match too; make a new one only if there is none
        // or the old one is on its way out
        NetworkRunner startRunner = session.runner != null && !session.leaving && !session.runner.SessionInfo.IsValid
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

        // Shutting down only the lobby runner (to start a match) needs nothing else
        if (SceneManager.GetActiveScene().buildIndex == MenuScene) return;

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
        yield return new WaitForSecondsRealtime(seconds);
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
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
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
}
