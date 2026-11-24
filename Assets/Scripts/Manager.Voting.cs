using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Fusion;

// Voting for the next map on the results screen. When the match ends the host picks the choices
// (this map first, to play again, then others at random), everyone votes with a click, and when the
// results time is up the host loads the winner for the whole session: same room, same settings, same
// people and bots. Anyone can leave to the menu instead.
public partial class Manager
{
    public const int VoteOptions = 3;
    // How long the results (and the vote) stay up
    private const float VoteTime = 15f;

    // The choices: map index + 1 (in Launcher.Maps), 0 for none
    [Networked, Capacity(VoteOptions)] private NetworkArray<int> VoteMaps => default;
    [Networked, Capacity(VoteOptions)] private NetworkArray<byte> VoteCounts => default;

    // Host only: who voted for what
    private readonly Dictionary<PlayerRef, int> votes = new Dictionary<PlayerRef, int>();

    // This player's vote, and the buttons showing the choices
    private int myVote = -1;
    private readonly List<VoteButton> voteButtons = new List<VoteButton>();

    private class VoteButton
    {
        public Button button;
        public Image background;
        public Text count;
    }

    // The next map's name, from the host, until the session's properties catch up (they go through
    // the Photon cloud and can arrive after the new map has loaded). Cleared back in the menu.
    public static string NextMapName;

    private bool VotingAvailable => Object != null && Object.IsValid && Launcher.Maps != null && VoteMaps[0] > 0;

    // Host, when the match ends
    private void ChooseVoteMaps()
    {
        votes.Clear();
        for (int i = 0; i < VoteOptions; i++)
        {
            VoteMaps.Set(i, 0);
            VoteCounts.Set(i, 0);
        }
        MapData[] maps = Launcher.Maps;
        if (maps == null || maps.Length == 0) return;

        int current = CurrentMapIndex();
        List<int> options = new List<int>();
        if (current >= 0 && current < maps.Length && maps[current].enabled) options.Add(current);
        List<int> others = new List<int>();
        for (int i = 0; i < maps.Length; i++)
        {
            if (i != current && maps[i].enabled) others.Add(i);
        }
        while (options.Count < VoteOptions && others.Count > 0)
        {
            int pick = Random.Range(0, others.Count);
            options.Add(others[pick]);
            others.RemoveAt(pick);
        }
        for (int i = 0; i < options.Count; i++) VoteMaps.Set(i, options[i] + 1);
    }

    private int CurrentMapIndex()
    {
        var properties = NetworkSession.MatchProperties(Runner);
        if (properties != null && properties.TryGetValue("map", out SessionProperty map)) return (int)map;
        return -1;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Vote(byte option, RpcInfo info = default)
    {
        if (option >= VoteOptions || VoteMaps[option] == 0) return;
        PlayerRef voter = info.Source.IsRealPlayer ? info.Source : Runner.LocalPlayer;
        votes[voter] = option;

        int[] counts = new int[VoteOptions];
        foreach (KeyValuePair<PlayerRef, int> vote in votes) counts[vote.Value]++;
        for (int i = 0; i < VoteOptions; i++) VoteCounts.Set(i, (byte)Mathf.Min(counts[i], 255));
    }

    // Host, when the results time is up: the choice with the most votes (a tie, or no votes at all,
    // is settled at random among the leaders)
    private int WinningVote()
    {
        int best = -1;
        List<int> leaders = new List<int>();
        for (int i = 0; i < VoteOptions; i++)
        {
            if (VoteMaps[i] == 0) continue;
            int count = VoteCounts[i];
            if (count > best) { best = count; leaders.Clear(); }
            if (count == best) leaders.Add(i);
        }
        return leaders.Count > 0 ? leaders[Random.Range(0, leaders.Count)] : 0;
    }

    // Host: loads the winning map for everyone in the session
    private void StartNextMap()
    {
        int map = VoteMaps[WinningVote()] - 1;
        MapData[] maps = Launcher.Maps;
        if (maps == null || map < 0 || map >= maps.Length)
        {
            NetworkSession.Leave();
            return;
        }

        RPC_NextMap(map, maps[map].name);

        // The room list and anyone joining see the new map
        Dictionary<string, SessionProperty> properties = new Dictionary<string, SessionProperty>();
        var current = NetworkSession.MatchProperties(Runner);
        if (current != null)
        {
            foreach (KeyValuePair<string, SessionProperty> entry in current) properties[entry.Key] = entry.Value;
        }
        properties["map"] = map;
        properties["mapname"] = maps[map].name;
        NetworkSession.UpdateMatchProperties(Runner, properties);

        StartCoroutine(LoadMapAfter(maps[map].scene, 0.6f));
    }

    private IEnumerator LoadMapAfter(int scene, float seconds)
    {
        // Time for everyone's loading screen to fade in
        yield return new WaitForSecondsRealtime(seconds);
        Runner.LoadScene(SceneRef.FromIndex(scene), LoadSceneMode.Single);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NextMap(int map, string mapName)
    {
        NextMapName = mapName;
        LoadingScreen.Show("LOADING NEXT MAP", mapName, LoadingScreen.ModeName((int)mode));
    }

    // The vote, down the right of the results screen: a button for each choice with its votes, and
    // a button to leave to the menu instead
    private void BuildVotePanel(Transform parent)
    {
        voteButtons.Clear();
        myVote = -1;
        // The cursor was locked for playing; the results need it to click
        if (!TouchControls.UsingTouch)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        const float width = 300f, rowHeight = 56f, gap = 10f;
        int choices = 0;
        for (int i = 0; i < VoteOptions; i++) if (VoteMaps[i] > 0) choices++;
        float height = 34f + choices * (rowHeight + gap) + 12f + rowHeight;

        RectTransform panel = NewLayer("Vote", new Vector2(1f, 0.5f), new Vector2(width, height), parent);
        panel.anchoredPosition = new Vector2(-48f, 0f);

        Text header = UITheme.NewText("Header", panel, 18, UITheme.MutedText, TextAnchor.MiddleLeft);
        UITheme.Row(header.rectTransform, 0f, 26f);
        header.text = "VOTE FOR THE NEXT MAP";

        MapData[] maps = Launcher.Maps;
        float top = 34f;
        for (int i = 0; i < VoteOptions; i++)
        {
            int map = VoteMaps[i] - 1;
            if (map < 0 || map >= maps.Length) continue;
            int option = i;
            string name = maps[map].name.ToUpperInvariant() + (map == CurrentMapIndex() ? "  <size=14>(AGAIN)</size>" : "");
            VoteButton vote = NewVoteButton(panel, name, top, rowHeight, () => Vote(option));
            voteButtons.Add(vote);
            top += rowHeight + gap;
        }

        VoteButton leave = NewVoteButton(panel, "LEAVE TO MENU", top + 12f, rowHeight, () => NetworkSession.Leave());
        leave.count.text = HasStateAuthority ? "<size=13>ENDS IT FOR ALL</size>" : "";

        RefreshVotes();
    }

    private VoteButton NewVoteButton(RectTransform panel, string label, float top, float height, UnityEngine.Events.UnityAction onClick)
    {
        Image background = UITheme.NewImage("Choice", panel, UITheme.Surface, 8f);
        background.raycastTarget = true;
        UITheme.Row(background.rectTransform, top, height);
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        UITheme.StyleButtonBackground(button, UITheme.ButtonStyle.Secondary);
        button.onClick.AddListener(onClick);

        Text name = UITheme.NewText("Name", background.transform, 20, UITheme.TextColor, TextAnchor.MiddleLeft);
        UITheme.Stretch(name.rectTransform);
        name.rectTransform.offsetMin = new Vector2(18f, 0f);
        name.supportRichText = true;
        name.horizontalOverflow = HorizontalWrapMode.Overflow;
        name.text = label;

        Text count = UITheme.NewText("Votes", background.transform, 20, UITheme.AccentText, TextAnchor.MiddleRight);
        UITheme.Stretch(count.rectTransform);
        count.rectTransform.offsetMax = new Vector2(-18f, 0f);
        count.supportRichText = true;
        count.horizontalOverflow = HorizontalWrapMode.Overflow;

        return new VoteButton { button = button, background = background, count = count };
    }

    private void Vote(int option)
    {
        myVote = option;
        RPC_Vote((byte)option);
        RefreshVotes();
    }

    // The counts, and the player's own choice in the accent colour
    private void RefreshVotes()
    {
        for (int i = 0; i < voteButtons.Count; i++)
        {
            VoteButton vote = voteButtons[i];
            if (vote.button == null) continue;
            int count = VoteCounts[i];
            vote.count.text = count > 0 ? count.ToString() : "";
            ColorBlock colors = vote.button.colors;
            colors.normalColor = colors.selectedColor = i == myVote ? UITheme.Accent : UITheme.Surface;
            vote.button.colors = colors;
        }
    }
}
