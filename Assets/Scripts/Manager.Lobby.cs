using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The lobby: a room made from the create tab doesn't start its first match as soon as two are in.
// Everyone waits in the warmup (flying about and shooting, nothing counts) while friends join with
// the room code or an invite link. Each person readies up (the Ready key, or the pause menu), and
// once all of them are ready the usual countdown runs. Someone unreadying, or a new player coming
// in, stops it again. The host can start the match anyway from the pause menu, whoever is ready.
// Bots never hold it up. Quick play, practice and the maps after a vote start by themselves as before.
public partial class Manager
{
    // This map's match starts from a lobby (the "lobby" room setting, first map only)
    public bool IsLobby { get; private set; }

    // Host only: the host started the countdown, whoever is ready
    private bool forceStart;

    // Who hosts the match, for the lobby's player list
    [Networked] private PlayerRef HostPlayer { get; set; }

    // The lobby is on: its warmup or countdown, before the match
    public bool InLobby => IsLobby && !matchEnded && (state == GameState.Waiting || state == GameState.Starting);

    public bool IsHost => Object != null && Object.IsValid && HasStateAuthority;

    // The host can still start the match early (it isn't already counting down because they did)
    public bool CanForceStart => IsHost && InLobby && !forceStart;

    public bool LocalReady => myind >= 0 && myind < playerInfo.Count && playerInfo[myind].ready;

    #region Host

    // Whether the countdown may run: in a lobby when everyone is ready (or the host said go), otherwise
    // as soon as enough players are in
    private bool CanStartMatch()
    {
        if (!IsLobby) return EnoughPlayersToStart();
        int people = 0, ready = 0;
        foreach (KeyValuePair<PlayerRef, PlayerStats> entry in Stats)
        {
            if (IsBot(entry.Key)) continue;
            people++;
            if (entry.Value.Ready) ready++;
        }
        return LobbyRules.CanStart(people, ready, forceStart);
    }

    // The host's Start button in the pause menu: the countdown starts now and runs whoever is ready
    public void ForceStart()
    {
        if (!IsHost || !IsLobby) return;
        if (State != GameState.Waiting && State != GameState.Starting) return;
        forceStart = true;
        if (State == GameState.Waiting)
        {
            MatchTime = startCountdown;
            State = GameState.Starting;
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetReady(NetworkBool isReady, RpcInfo info = default)
    {
        if (!IsLobby || (State != GameState.Waiting && State != GameState.Starting)) return;
        PlayerRef player = info.Source.IsRealPlayer ? info.Source : Runner.LocalPlayer;
        PlayerStats stats;
        if (!Stats.TryGet(player, out stats)) return;
        stats.Ready = isReady;
        Stats.Set(player, stats);
    }

    #endregion

    #region Everyone

    // The Ready key, and the pause menu's Ready button
    public void ToggleReady()
    {
        if (!InLobby || Object == null || !Object.IsValid) return;
        RPC_SetReady(!LocalReady);
    }

    private void UpdateLobby()
    {
        bool show = InLobby;
        if (lobbyPanel != null && lobbyPanel.gameObject.activeSelf != show) lobbyPanel.gameObject.SetActive(show);
        if (show && GameInput.Pressed(GameInput.Ready)) ToggleReady();
    }

    #endregion

    #region Panel

    // Top left, under the kills and deaths: the room code, who's in and who's ready, and how to ready up
    private RectTransform lobbyPanel;
    private Text lobbyTitle, lobbyStatus, lobbyHint, lobbyBots;
    private readonly List<Text> lobbyNames = new List<Text>();
    private readonly List<Text> lobbyStates = new List<Text>();

    private const float LobbyWidth = 400f, LobbyPadding = 16f, LobbyRowHeight = 26f, LobbyListTop = 52f;

    private void CreateLobbyPanel(Transform canvas)
    {
        if (!IsLobby || lobbyPanel != null) return;

        GameObject go = new GameObject("Lobby", typeof(RectTransform));
        lobbyPanel = (RectTransform)go.transform;
        lobbyPanel.SetParent(canvas, false);
        // Right above the HUD, like the scoreboard (which goes over it): under the chat, pause menu and results
        if (Hud != null && Hud.transform.parent == canvas) lobbyPanel.SetSiblingIndex(Hud.transform.GetSiblingIndex() + 1);
        UITheme.Place(lobbyPanel, new Vector2(0f, 1f), new Vector2(28f, -100f), new Vector2(LobbyWidth, 200f));

        Image background = UITheme.NewImage("Background", lobbyPanel, UITheme.Panel, 10f);
        UITheme.Stretch(background.rectTransform);

        lobbyTitle = LobbyLine("Title", 18, UITheme.AccentText, TextAnchor.MiddleLeft, LobbyPadding, 24f);
        lobbyTitle.supportRichText = true;
        lobbyBots = LobbyLine("Bots", 16, UITheme.MutedText, TextAnchor.MiddleLeft, 0f, LobbyRowHeight);
        lobbyStatus = LobbyLine("Status", 16, UITheme.TextColor, TextAnchor.MiddleLeft, 0f, 24f);
        lobbyHint = LobbyLine("Hint", 15, UITheme.MutedText, TextAnchor.UpperLeft, 0f, 44f);
        lobbyHint.supportRichText = true;

        RefreshLobbyPanel();
    }

    private Text LobbyLine(string name, int size, Color color, TextAnchor anchor, float top, float height)
    {
        Text text = UITheme.NewText(name, lobbyPanel, size, color, anchor);
        UITheme.Row(text.rectTransform, top, height);
        text.rectTransform.offsetMin = new Vector2(LobbyPadding, text.rectTransform.offsetMin.y);
        text.rectTransform.offsetMax = new Vector2(-LobbyPadding, text.rectTransform.offsetMax.y);
        return text;
    }

    private void RefreshLobbyPanel()
    {
        if (lobbyPanel == null) return;

        string muted = UITheme.Hex(UITheme.MutedText);
        string code = NetworkSession.RoomCode;
        lobbyTitle.text = code != null ? $"LOBBY   <color={muted}>ROOM CODE</color>  <color=#FFFFFFFF>{code}</color>" : "LOBBY";

        int hostActor = Object != null && Object.IsValid && HostPlayer.IsRealPlayer ? HostPlayer.PlayerId : -1;
        int people = 0, ready = 0, botCount = 0;
        float y = LobbyListTop;
        foreach (PlayerInfo p in playerInfo)
        {
            if (IsBotActor(p.actor))
            {
                botCount++;
                continue;
            }
            if (people >= lobbyNames.Count) AddLobbyRow();
            Text name = lobbyNames[people], status = lobbyStates[people];
            PlaceLobbyRow(name, y);
            PlaceLobbyRow(status, y);
            name.gameObject.SetActive(true);
            status.gameObject.SetActive(true);

            bool isMe = p.actor == LocalActor;
            name.text = string.IsNullOrEmpty(p.profile.username) ? "PLAYER" : p.profile.username;
            name.color = isMe ? UITheme.AccentText : UITheme.TextColor;
            string hostTag = p.actor == hostActor ? $"<color={muted}>HOST</color>   " : "";
            status.text = hostTag + (p.ready ? $"<color={UITheme.Hex(UITheme.AccentText)}>READY</color>" : $"<color={muted}>NOT READY</color>");

            people++;
            if (p.ready) ready++;
            y += LobbyRowHeight;
        }
        for (int i = people; i < lobbyNames.Count; i++)
        {
            lobbyNames[i].gameObject.SetActive(false);
            lobbyStates[i].gameObject.SetActive(false);
        }

        lobbyBots.gameObject.SetActive(botCount > 0);
        if (botCount > 0)
        {
            PlaceLobbyRow(lobbyBots, y);
            lobbyBots.text = botCount == 1 ? "+ 1 BOT" : $"+ {botCount} BOTS";
            y += LobbyRowHeight;
        }

        y += 8f;
        bool counting = state == GameState.Starting;
        string statusLine = LobbyRules.Status(people, ready, counting);
        if (counting) statusLine += $"   <color={UITheme.Hex(UITheme.AccentText)}>STARTING IN {Mathf.Max(0, currentMatchTime)}</color>";
        lobbyStatus.supportRichText = true;
        lobbyStatus.text = statusLine;
        PlaceLobbyRow(lobbyStatus, y);
        y += 26f;

        lobbyHint.text = LobbyHint();
        PlaceLobbyRow(lobbyHint, y);
        int hintLines = lobbyHint.text.Split('\n').Length;
        y += hintLines * 20f + LobbyPadding;

        lobbyPanel.sizeDelta = new Vector2(LobbyWidth, y);
    }

    // How to ready up (as the key is bound now), and for the host how to start anyway
    private string LobbyHint()
    {
        string action = LocalReady ? "NOT READY AFTER ALL" : "READY UP";
        string line;
        if (TouchControls.UsingTouch) line = $"PAUSE (II) TO {action}";
        else
        {
            string key = GameInput.BindingText(GameInput.Ready, GameInput.KeyboardBindingIndex(GameInput.Ready));
            if (Gamepad.current != null) key += " / D-PAD UP";
            line = $"<color=#FFFFFFFF>{key}</color>  {action}";
        }
        if (CanForceStart) line += "\nHOST: START NOW FROM THE PAUSE MENU";
        return line;
    }

    private void AddLobbyRow()
    {
        Text name = UITheme.NewText("Name", lobbyPanel, 17, UITheme.TextColor, TextAnchor.MiddleLeft);
        // Long names are cut short rather than running into the ready column
        name.horizontalOverflow = HorizontalWrapMode.Wrap;
        name.verticalOverflow = VerticalWrapMode.Truncate;
        name.rectTransform.anchorMax = new Vector2(0.55f, 1f);
        Text status = UITheme.NewText("Ready", lobbyPanel, 16, UITheme.TextColor, TextAnchor.MiddleRight);
        status.supportRichText = true;
        lobbyNames.Add(name);
        lobbyStates.Add(status);
    }

    // A row of the panel 'top' units down, keeping the row's own share of the width
    private static void PlaceLobbyRow(Text text, float top)
    {
        RectTransform rect = text.rectTransform;
        float xMin = rect.anchorMin.x, xMax = rect.anchorMax.x;
        UITheme.Row(rect, top, Mathf.Max(LobbyRowHeight, rect.sizeDelta.y));
        rect.anchorMin = new Vector2(xMin, 1f);
        rect.anchorMax = new Vector2(xMax, 1f);
        rect.offsetMin = new Vector2(LobbyPadding, rect.offsetMin.y);
        rect.offsetMax = new Vector2(-LobbyPadding, rect.offsetMax.y);
    }

    #endregion
}
