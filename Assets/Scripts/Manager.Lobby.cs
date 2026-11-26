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

    // Top left, under the kills and deaths: a card with the room code, how many are ready (with a
    // bar), a row per person with a READY / NOT READY pill, the countdown, and how to ready up
    private RectTransform lobbyPanel;
    private Text lobbyTitle, lobbyCode, lobbyCount, lobbyBots, lobbyCountdown, lobbyStatus, lobbyKey, lobbyHint, lobbyHostHint;
    private RectTransform lobbyCodeChip, lobbyKeyChip, lobbyProgress, lobbyProgressFill;
    private readonly List<LobbyRow> lobbyRows = new List<LobbyRow>();

    private class LobbyRow
    {
        public RectTransform root;
        public Image plate, stripe, pill;
        public Text name, host, state;
    }

    private const float LobbyWidth = 420f, LobbyPadding = 18f;
    private const float LobbyRowHeight = 34f, LobbyRowGap = 4f, LobbyPillWidth = 104f;
    // Rows for the people, from under the ready bar
    private const float LobbyListTop = 96f;

    private static readonly Color LobbyRowColor = new Color(1f, 1f, 1f, 0.06f);
    private static readonly Color LobbyMyRowColor = new Color(0.45f, 0.78f, 1f, 0.14f);

    private void CreateLobbyPanel(Transform canvas)
    {
        if (!IsLobby || lobbyPanel != null) return;

        GameObject go = new GameObject("Lobby", typeof(RectTransform));
        lobbyPanel = (RectTransform)go.transform;
        lobbyPanel.SetParent(canvas, false);
        // Right above the HUD, like the scoreboard (which goes over it): under the chat, pause menu and results
        if (Hud != null && Hud.transform.parent == canvas) lobbyPanel.SetSiblingIndex(Hud.transform.GetSiblingIndex() + 1);
        UITheme.Place(lobbyPanel, new Vector2(0f, 1f), new Vector2(28f, -100f), new Vector2(LobbyWidth, 200f));

        Image background = UITheme.NewImage("Background", lobbyPanel, UITheme.Panel, 12f);
        UITheme.Stretch(background.rectTransform);
        // A thin accent line along the top, like the match clock's
        Image accent = UITheme.NewImage("Accent", lobbyPanel, UITheme.Accent, 1.5f);
        LobbyPlace(accent.rectTransform, 0f, 3f, 12f, 12f);

        // Header: LOBBY on the left, the room code on a chip on the right
        lobbyTitle = UITheme.NewText("Title", lobbyPanel, 24, UITheme.TextColor, TextAnchor.MiddleLeft);
        LobbyPlace(lobbyTitle.rectTransform, 14f, 34f, LobbyPadding, LobbyPadding);
        lobbyTitle.text = "LOBBY";
        UITheme.AddShadow(lobbyTitle);

        Image codeChip = UITheme.NewImage("Code", lobbyPanel, UITheme.Field, 8f);
        lobbyCodeChip = codeChip.rectTransform;
        UITheme.Place(lobbyCodeChip, new Vector2(1f, 1f), new Vector2(-LobbyPadding, -14f), new Vector2(140f, 34f));
        lobbyCode = UITheme.NewText("Text", lobbyCodeChip, 22, UITheme.TextColor, TextAnchor.MiddleCenter);
        lobbyCode.supportRichText = true;

        // How many are ready, and a bar filling up as they do
        lobbyCount = UITheme.NewText("Count", lobbyPanel, 15, UITheme.MutedText, TextAnchor.MiddleLeft);
        lobbyCount.supportRichText = true;
        LobbyPlace(lobbyCount.rectTransform, 56f, 20f, LobbyPadding, LobbyPadding);
        Image track = UITheme.NewImage("Ready Bar", lobbyPanel, UITheme.Track, 2f);
        lobbyProgress = track.rectTransform;
        LobbyPlace(lobbyProgress, 80f, 4f, LobbyPadding, LobbyPadding);
        Image fill = UITheme.NewImage("Fill", lobbyProgress, UITheme.AccentText, 2f);
        lobbyProgressFill = fill.rectTransform;
        lobbyProgressFill.pivot = new Vector2(0f, 0.5f);
        lobbyProgressFill.offsetMin = lobbyProgressFill.offsetMax = Vector2.zero;

        lobbyBots = UITheme.NewText("Bots", lobbyPanel, 16, UITheme.MutedText, TextAnchor.MiddleLeft);

        // The countdown, big, and the status line under it
        lobbyCountdown = UITheme.NewText("Countdown", lobbyPanel, 28, UITheme.AccentText, TextAnchor.MiddleLeft);
        UITheme.AddShadow(lobbyCountdown);
        lobbyStatus = UITheme.NewText("Status", lobbyPanel, 17, UITheme.TextColor, TextAnchor.MiddleLeft);
        lobbyStatus.supportRichText = true;

        // How to ready up: the key on a keycap, then what it does
        Image keyChip = UITheme.NewImage("Key", lobbyPanel, UITheme.Track, 6f);
        lobbyKeyChip = keyChip.rectTransform;
        lobbyKey = UITheme.NewText("Text", lobbyKeyChip, 16, UITheme.TextColor, TextAnchor.MiddleCenter);
        lobbyHint = UITheme.NewText("Hint", lobbyPanel, 16, UITheme.TextColor, TextAnchor.MiddleLeft);
        lobbyHostHint = UITheme.NewText("Host Hint", lobbyPanel, 14, UITheme.MutedText, TextAnchor.MiddleLeft);

        RefreshLobbyPanel();
    }

    // A strip 'top' units down the panel, 'height' tall, inset from its sides
    private void LobbyPlace(RectTransform rect, float top, float height, float left, float right)
    {
        UITheme.Row(rect, top, height);
        rect.offsetMin = new Vector2(left, rect.offsetMin.y);
        rect.offsetMax = new Vector2(-right, rect.offsetMax.y);
    }

    private void RefreshLobbyPanel()
    {
        if (lobbyPanel == null) return;

        string muted = UITheme.Hex(UITheme.MutedText);
        string code = NetworkSession.RoomCode;
        lobbyCodeChip.gameObject.SetActive(code != null);
        if (code != null)
        {
            lobbyCode.text = $"<size=13><color={muted}>CODE</color></size>  {code}";
            lobbyCodeChip.sizeDelta = new Vector2(lobbyCode.preferredWidth + 28f, 34f);
        }

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
            if (people >= lobbyRows.Count) lobbyRows.Add(NewLobbyRow());
            LobbyRow row = lobbyRows[people];
            row.root.gameObject.SetActive(true);
            LobbyPlace(row.root, y, LobbyRowHeight, LobbyPadding - 6f, LobbyPadding - 6f);

            bool isMe = p.actor == LocalActor;
            bool isHost = p.actor == hostActor;
            row.plate.color = isMe ? LobbyMyRowColor : LobbyRowColor;
            row.stripe.gameObject.SetActive(isMe);
            row.name.text = string.IsNullOrEmpty(p.profile.username) ? "PLAYER" : p.profile.username;
            row.name.color = isMe ? UITheme.AccentText : UITheme.TextColor;
            row.host.gameObject.SetActive(isHost);
            // The name stops short of the HOST tag and the pill
            row.name.rectTransform.offsetMax = new Vector2(-(10f + LobbyPillWidth + (isHost ? 58f : 10f)), 0f);

            row.pill.color = p.ready ? UITheme.Accent : UITheme.Field;
            row.state.text = p.ready ? "READY" : "NOT READY";
            row.state.color = p.ready ? UITheme.TextColor : UITheme.MutedText;

            people++;
            if (p.ready) ready++;
            y += LobbyRowHeight + LobbyRowGap;
        }
        for (int i = people; i < lobbyRows.Count; i++) lobbyRows[i].root.gameObject.SetActive(false);

        lobbyBots.gameObject.SetActive(botCount > 0);
        if (botCount > 0)
        {
            LobbyPlace(lobbyBots.rectTransform, y, 24f, LobbyPadding, LobbyPadding);
            lobbyBots.text = botCount == 1 ? "+ 1 BOT" : $"+ {botCount} BOTS";
            y += 26f;
        }

        // Ready count and bar
        lobbyCount.text = people > 0 ? $"<color=#FFFFFFFF>{ready}</color> / {people} READY" : "NOBODY HERE YET";
        float fraction = people > 0 ? (float)ready / people : 0f;
        lobbyProgressFill.anchorMin = Vector2.zero;
        lobbyProgressFill.anchorMax = new Vector2(fraction, 1f);
        lobbyProgressFill.offsetMin = lobbyProgressFill.offsetMax = Vector2.zero;
        lobbyProgressFill.gameObject.SetActive(fraction > 0f);

        // The countdown, or what it's waiting for
        y += 8f;
        bool counting = state == GameState.Starting;
        lobbyCountdown.gameObject.SetActive(counting);
        if (counting)
        {
            lobbyCountdown.text = $"STARTING IN {Mathf.Max(0, currentMatchTime)}";
            LobbyPlace(lobbyCountdown.rectTransform, y, 36f, LobbyPadding, LobbyPadding);
            y += 36f;
        }
        lobbyStatus.text = LobbyRules.Status(people, ready, counting);
        // Under the countdown it's a muted footnote; otherwise accent once everyone's ready
        bool allReady = people > 0 && ready >= people;
        lobbyStatus.color = counting ? UITheme.MutedText : allReady ? UITheme.AccentText : UITheme.TextColor;
        LobbyPlace(lobbyStatus.rectTransform, y, 26f, LobbyPadding, LobbyPadding);
        y += 32f;

        // How to ready up: a keycap with the key as bound now (none on touch: it's in the pause menu)
        string action = LocalReady ? "NOT READY AFTER ALL" : "READY UP";
        float hintLeft = LobbyPadding;
        bool touch = TouchControls.UsingTouch;
        lobbyKeyChip.gameObject.SetActive(!touch);
        if (touch)
        {
            lobbyHint.text = $"PAUSE (II) TO {action}";
        }
        else
        {
            string key = GameInput.BindingText(GameInput.Ready, GameInput.KeyboardBindingIndex(GameInput.Ready));
            if (Gamepad.current != null) key += " / D-PAD UP";
            lobbyKey.text = key;
            float keyWidth = Mathf.Max(30f, lobbyKey.preferredWidth + 18f);
            UITheme.Place(lobbyKeyChip, new Vector2(0f, 1f), new Vector2(LobbyPadding, -y), new Vector2(keyWidth, 28f));
            hintLeft += keyWidth + 10f;
            lobbyHint.text = action;
        }
        lobbyHint.color = LocalReady ? UITheme.MutedText : UITheme.TextColor;
        LobbyPlace(lobbyHint.rectTransform, y, 28f, hintLeft, LobbyPadding);
        y += 32f;

        bool hostHint = CanForceStart;
        lobbyHostHint.gameObject.SetActive(hostHint);
        if (hostHint)
        {
            lobbyHostHint.text = "HOST: START NOW FROM THE PAUSE MENU";
            LobbyPlace(lobbyHostHint.rectTransform, y, 22f, LobbyPadding, LobbyPadding);
            y += 24f;
        }

        lobbyPanel.sizeDelta = new Vector2(LobbyWidth, y + LobbyPadding - 6f);
    }

    // A person's row: a faint plate (tinted, with an accent stripe, for you), their name, a HOST tag
    // for the host, and a READY / NOT READY pill on the right
    private LobbyRow NewLobbyRow()
    {
        LobbyRow row = new LobbyRow();
        GameObject go = new GameObject("Player", typeof(RectTransform));
        row.root = (RectTransform)go.transform;
        row.root.SetParent(lobbyPanel, false);

        row.plate = UITheme.NewImage("Plate", row.root, LobbyRowColor, 6f);
        UITheme.Stretch(row.plate.rectTransform);
        row.stripe = UITheme.NewImage("Stripe", row.root, UITheme.AccentText, 1.5f);
        RectTransform stripe = row.stripe.rectTransform;
        stripe.anchorMin = Vector2.zero;
        stripe.anchorMax = new Vector2(0f, 1f);
        stripe.pivot = new Vector2(0f, 0.5f);
        stripe.offsetMin = new Vector2(0f, 6f);
        stripe.offsetMax = new Vector2(3f, -6f);

        row.name = UITheme.NewText("Name", row.root, 19, UITheme.TextColor, TextAnchor.MiddleLeft);
        // Long names are cut short rather than running into the pill
        row.name.horizontalOverflow = HorizontalWrapMode.Wrap;
        row.name.verticalOverflow = VerticalWrapMode.Truncate;
        row.name.rectTransform.offsetMin = new Vector2(14f, 0f);
        UITheme.AddShadow(row.name, 0.5f);

        row.host = UITheme.NewText("Host", row.root, 13, UITheme.MutedText, TextAnchor.MiddleRight);
        row.host.text = "HOST";
        RectTransform host = row.host.rectTransform;
        host.anchorMin = new Vector2(1f, 0f);
        host.anchorMax = Vector2.one;
        host.pivot = new Vector2(1f, 0.5f);
        host.offsetMin = new Vector2(-(8f + LobbyPillWidth + 50f), 0f);
        host.offsetMax = new Vector2(-(8f + LobbyPillWidth + 10f), 0f);

        row.pill = UITheme.NewImage("Pill", row.root, UITheme.Field, 12f);
        UITheme.Place(row.pill.rectTransform, new Vector2(1f, 0.5f), new Vector2(-5f, 0f), new Vector2(LobbyPillWidth, 24f));
        row.state = UITheme.NewText("Text", row.pill.rectTransform, 14, UITheme.MutedText, TextAnchor.MiddleCenter);
        return row;
    }

    #endregion
}
