using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Team Deathmatch: teams, team score and team colours.
public partial class Manager
{
    public MatchMode mode = MatchMode.FreeForAll;
    public bool IsTeamMode => mode == MatchMode.TeamDeathmatch;

    public static readonly string[] TeamNames = { "BLUE", "RED" };
    public static readonly Color[] TeamColors = { new Color(0.3f, 0.6f, 1f), new Color(1f, 0.35f, 0.3f) };
    public static string TeamHex(int team) => team == 0 ? "#4d99ff" : "#ff594d";

    // Goes up whenever the player list changes, so players know to refresh their team colours
    public static int TeamsVersion { get; private set; }

    private Text teamScore;
    private int announcedTeam = -1;

    // The player's team in team mode, or -1 in free for all or if they aren't known yet
    public int GetTeam(int actor)
    {
        if (!IsTeamMode) return -1;
        int index = FindPlayerIndex(actor);
        return index >= 0 ? playerInfo[index].team : -1;
    }

    public bool AreEnemies(int actorA, int actorB)
    {
        if (actorA == actorB) return false;
        if (!IsTeamMode) return true;

        int teamA = GetTeam(actorA);
        int teamB = GetTeam(actorB);
        // Until both teams are known, treat them as enemies so nobody is accidentally unhittable
        if (teamA < 0 || teamB < 0) return true;
        return teamA != teamB;
    }

    public int TeamKills(int team)
    {
        int kills = 0;
        foreach (PlayerInfo p in playerInfo)
        {
            if (p.team == team) kills += p.kills;
        }
        return kills;
    }

    // 0 or 1, or -1 for a draw
    private int WinningTeam()
    {
        int blue = TeamKills(0);
        int red = TeamKills(1);
        if (blue == red) return -1;
        return blue > red ? 0 : 1;
    }

    // Host only: put new players on the smaller team, or the one that's behind
    private byte ChooseTeamForNewPlayer()
    {
        // Counted from the networked stats, which are always up to date on the host
        int blueCount = 0, redCount = 0;
        foreach (KeyValuePair<Fusion.PlayerRef, PlayerStats> entry in Stats)
        {
            if (entry.Value.Team == 0) blueCount++; else redCount++;
        }

        if (blueCount != redCount) return (byte)(blueCount < redCount ? 0 : 1);
        return (byte)(HostTeamKills(0) <= HostTeamKills(1) ? 0 : 1);
    }

    private string TeamHeader()
    {
        string score = $"<color={TeamHex(0)}>{TeamNames[0]} {TeamKills(0)}</color>  -  <color={TeamHex(1)}>{TeamKills(1)} {TeamNames[1]}</color>";
        if (state != GameState.Ending) return "TEAM DEATHMATCH   " + score;

        int winner = WinningTeam();
        string result = winner < 0 ? "DRAW" : $"<color={TeamHex(winner)}>{TeamNames[winner]} TEAM WINS</color>";
        return result + "   " + score;
    }

    // Team score shown above the match timer, built from the timer text so it matches the HUD style
    private void CreateTeamScore()
    {
        if (!IsTeamMode || timer == null) return;

        GameObject scoreObject = Instantiate(timer.gameObject, timer.transform.parent);
        scoreObject.name = "Team Score";

        teamScore = scoreObject.GetComponent<Text>();
        teamScore.supportRichText = true;
        teamScore.horizontalOverflow = HorizontalWrapMode.Overflow;

        RectTransform rect = (RectTransform)scoreObject.transform;
        RectTransform timerRect = (RectTransform)timer.transform;
        rect.anchoredPosition = timerRect.anchoredPosition + new Vector2(0f, timerRect.sizeDelta.y + 6f);

        RefreshTeamScore();
    }

    private void RefreshTeamScore()
    {
        if (teamScore == null) return;
        teamScore.text = $"<color={TeamHex(0)}>{TeamNames[0]} {TeamKills(0)}</color>  -  <color={TeamHex(1)}>{TeamKills(1)} {TeamNames[1]}</color>";
    }

    private void AnnounceTeam()
    {
        if (!IsTeamMode) return;

        int myTeam = GetTeam(LocalActor);
        if (myTeam < 0 || myTeam == announcedTeam) return;

        announcedTeam = myTeam;
        ShowMessage($"YOU ARE ON <color={TeamHex(myTeam)}>{TeamNames[myTeam]}</color> TEAM");
    }
}
