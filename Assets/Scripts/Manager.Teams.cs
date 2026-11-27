using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Team modes (Team Deathmatch, Team Cap Grab): teams, team score and team colours. The team score on
// the HUD is part of the score strip (Manager.ScoreStrip.cs).
public partial class Manager
{
    public MatchMode mode = MatchMode.FreeForAll;
    public bool IsTeamMode => mode == MatchMode.TeamDeathmatch || mode == MatchMode.TeamCapGrab;

    // Blue against red, or against orange with the colour blind setting (GameSettings.Colourblind)
    private static readonly string[] StandardTeamNames = { "BLUE", "RED" };
    private static readonly string[] ColourblindTeamNames = { "BLUE", "ORANGE" };
    private static readonly Color[] StandardTeamColors = { new Color(0.3f, 0.6f, 1f), new Color(1f, 0.35f, 0.3f) };
    private static readonly Color[] ColourblindTeamColors = { new Color(0.2f, 0.55f, 1f), new Color(1f, 0.62f, 0.1f) };
    public static string[] TeamNames => GameSettings.Colourblind ? ColourblindTeamNames : StandardTeamNames;
    public static Color[] TeamColors => GameSettings.Colourblind ? ColourblindTeamColors : StandardTeamColors;
    public static string TeamHex(int team) => GameSettings.Colourblind ? (team == 0 ? "#338cff" : "#ff9e1a") : (team == 0 ? "#4d99ff" : "#ff594d");

    // The colour setting changed mid-match: everyone's body, hands, gun pieces and hat refresh as
    // they do when the teams change
    private void OnColoursChanged()
    {
        TeamsVersion++;
        RefreshScoreStrip();
    }

    // Goes up whenever the player list changes, so players know to refresh their team colours
    public static int TeamsVersion { get; private set; }

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
        if (state != GameState.Ending) return LoadingScreen.ModeName((int)mode) + "   " + score;

        int winner = WinningTeam();
        string result = winner < 0 ? "DRAW" : $"<color={TeamHex(winner)}>{TeamNames[winner]} TEAM WINS</color>";
        return result + "   " + score;
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
