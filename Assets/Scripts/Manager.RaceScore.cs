using UnityEngine;
using UnityEngine.UI;

// Free for all modes with a limit (kills, caps or the Gun Game ladder): under the timer, how close
// the match is to its end. "FIRST TO 20 · YOU 14 · LEADER 17", or while you lead, how far ahead of
// the next player you are. (Team modes show the team score there instead: Manager.Teams.cs.)
public partial class Manager
{
    private Text raceScore;
    private Image raceScoreBackdrop;

    private void CreateRaceScore()
    {
        if (IsTeamMode || !HasKillLimit || timer == null) return;

        GameObject scoreObject = Instantiate(timer.gameObject, timer.transform.parent);
        scoreObject.name = "Race Score";
        raceScore = scoreObject.GetComponent<Text>();
        raceScore.supportRichText = true;
        raceScore.horizontalOverflow = HorizontalWrapMode.Overflow;

        RectTransform rect = (RectTransform)scoreObject.transform;
        RectTransform timerRect = (RectTransform)timer.transform;
        rect.anchoredPosition = timerRect.anchoredPosition - new Vector2(0f, timerRect.sizeDelta.y + 4f);
        raceScore.fontSize = 18;
        raceScore.color = UITheme.TextColor;
        UITheme.AddShadow(raceScore);

        RefreshRaceScore();
    }

    private void RefreshRaceScore()
    {
        if (raceScore == null) return;
        int mine = (myind >= 0 && playerInfo.Count > myind) ? playerInfo[myind].kills : 0;
        // The best of everyone else
        int others = 0;
        for (int i = 0; i < playerInfo.Count; i++)
        {
            if (i != myind && playerInfo[i].kills > others) others = playerInfo[i].kills;
        }
        string muted = UITheme.Hex(UITheme.MutedText);
        string accent = UITheme.Hex(UITheme.AccentText);
        string goal = IsGunGame ? "LADDER" : $"FIRST TO {killcount}";
        string race = mine > others && mine > 0
            ? $"<color={accent}>YOU LEAD {mine}</color>   <color={muted}>NEXT</color> {others}"
            : $"<color={muted}>YOU</color> {mine}   <color={muted}>LEADER</color> {others}";
        raceScore.text = $"<color={muted}>{goal}</color>   {race}";
        raceScoreBackdrop = UITheme.FitBackdrop(raceScoreBackdrop, raceScore);
    }
}
