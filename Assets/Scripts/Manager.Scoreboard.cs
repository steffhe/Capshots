using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The scoreboard shown while holding the scoreboard key and at the end of the match. Each row: level,
// name (with YOU and the ping), cap shots, score (kills, or caps in Cap Grab) and deaths. Rows are
// copies of the board's template row (PlayerCard), made once and reused: the board refreshes whenever
// the host's stats change (pings included, up to once a second while it's held up).
public partial class Manager
{
    // The rows each board has made, in order (the held-up scoreboard and the end screen's)
    private readonly Dictionary<Transform, List<GameObject>> boardRows = new Dictionary<Transform, List<GameObject>>();

    private void Leaderboard(Transform lb)
    {
        Text modeText = lb.Find("Header/Mode").GetComponent<Text>();
        modeText.supportRichText = true;
        modeText.horizontalOverflow = HorizontalWrapMode.Overflow;
        modeText.text = IsTeamMode ? TeamHeader() : LoadingScreen.ModeName((int)mode);
        // The map, and the room code to give friends
        string code = NetworkSession.RoomCode;
        Text mapText = lb.Find("Header/Map").GetComponent<Text>();
        mapText.supportRichText = true;
        mapText.horizontalOverflow = HorizontalWrapMode.Overflow;
        mapText.text = code != null ? $"{CurrentMapName()}   <color={UITheme.Hex(UITheme.MutedText)}>CODE</color> {code}" : CurrentMapName();

        GameObject playerCard = lb.GetChild(1).gameObject;
        playerCard.SetActive(false);

        if (!boardRows.TryGetValue(lb, out List<GameObject> rows))
        {
            rows = new List<GameObject>();
            boardRows[lb] = rows;
            // Anything after the template from before (the scene's own) goes
            for (int i = lb.childCount - 1; i >= 2; i--) Destroy(lb.GetChild(i).gameObject);
        }

        List<PlayerInfo> sorted = SortPlayers(playerInfo);
        int me = LocalActor;
        for (int r = 0; r < sorted.Count; r++)
        {
            if (r >= rows.Count) rows.Add(Instantiate(playerCard, lb));
            FillRow(rows[r], sorted[r], r, sorted[r].actor == me);
        }
        for (int r = sorted.Count; r < rows.Count; r++)
        {
            if (rows[r].activeSelf) rows[r].SetActive(false);
        }

        lb.gameObject.SetActive(true);
    }

    private void FillRow(GameObject card, PlayerInfo a, int index, bool mine)
    {
        Image image = card.GetComponent<Image>();
        if (image != null)
        {
            Color color = index % 2 == 1 ? UITheme.SurfaceAlt : UITheme.Surface;
            if (IsTeamMode)
            {
                color = TeamColors[a.team % 2];
                color.a = 0.35f;
            }
            // Your own row: a brighter background (in teams, a stronger team tint) and a light outline,
            // so you can find yourself at a glance
            if (mine) color = IsTeamMode ? new Color(color.r, color.g, color.b, 0.7f) : new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.75f);
            image.color = color;
        }
        Outline outline = card.GetComponent<Outline>();
        if (mine && outline == null)
        {
            outline = card.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;
        }
        if (outline != null) outline.enabled = mine;

        card.transform.Find("Level").GetComponent<Text>().text = a.profile.level.ToString("00");
        Text usernameText = card.transform.Find("Username").GetComponent<Text>();
        usernameText.supportRichText = true;
        usernameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        usernameText.text = TextRules.Escape(a.profile.username) + (mine ? $"  <color={UITheme.Hex(UITheme.AccentText)}>YOU</color>" : "") + PingLabel(a.actor);

        // The prefab's SCORE column (it showed kills x 100) shows cap shots, in the cap logo's gold
        SetCell(card, "Score Tag", "CAPSHOTS", UITheme.MutedText);
        SetCell(card, "Score Value", a.capshots.ToString(), a.capshots > 0 ? UITheme.Capshot : UITheme.TextColor);
        // Kills, or caps in Cap Grab
        SetCell(card, "Kills Tag", ScoreWord, UITheme.MutedText);
        SetCell(card, "Kills Value", a.kills.ToString(), UITheme.TextColor);
        SetCell(card, "Deaths Value", a.deaths.ToString(), UITheme.TextColor);

        if (!card.activeSelf) card.SetActive(true);
    }

    private static void SetCell(GameObject card, string name, string text, Color color)
    {
        Text cell = card.transform.Find(name)?.GetComponent<Text>();
        if (cell == null) return;
        cell.text = text;
        cell.color = color;
    }

    private List<PlayerInfo> SortPlayers (List<PlayerInfo> info)
    {
        List<PlayerInfo> sorted = new List<PlayerInfo>(info);
        // Team mode: blue team first, then red, each by score. Free for all: by score. Ties go to
        // more cap shots, then fewer deaths, then join order, so every machine shows the same order.
        sorted.Sort((x, y) =>
        {
            if (IsTeamMode && x.team != y.team) return x.team.CompareTo(y.team);
            if (x.kills != y.kills) return y.kills.CompareTo(x.kills);
            if (x.capshots != y.capshots) return y.capshots.CompareTo(x.capshots);
            if (x.deaths != y.deaths) return x.deaths.CompareTo(y.deaths);
            return x.actor.CompareTo(y.actor);
        });
        return sorted;
    }
}
