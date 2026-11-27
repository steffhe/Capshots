using UnityEngine;
using UnityEngine.UI;

// The score strip round the match clock, top centre: your side on a plate left of the clock, the side
// you're up against on the right, and the goal on a line under it. Local presentation, rebuilt from
// the scoreboard (playerInfo) whenever it changes; it lives with the clock outside the HUD, so it stays
// up while you're dead.
//  - Team modes: your team left, the other team right, in team colours; "YOU'RE BLUE · FIRST TO 50 KILLS".
//  - Free for all and Cap Grab: YOU against the LEADER, or the 2ND while you lead (HudRules.OpponentLabel).
//  - Gun Game: the form each of you is on ("2/4"), and how many kills to the next form or the win.
// The plates show from the countdown on (warmup kills don't count); the goal line shows throughout.
public partial class Manager
{
    private class ScorePlate
    {
        public Image plate, line;
        public Text label, value;
    }

    private ScorePlate leftPlate, rightPlate;
    private Text scoreGoal;
    private Image scoreGoalBackdrop;

    private const float PlateWidth = 76f;
    private const float PlateGap = 6f;

    private void CreateScoreStrip()
    {
        if (timer == null || leftPlate != null) return;
        Transform parent = timer.transform.parent;
        float x = UITheme.TimerSize.x / 2f + PlateGap + PlateWidth / 2f;
        leftPlate = NewScorePlate("Score Left", parent, -x);
        rightPlate = NewScorePlate("Score Right", parent, x);

        scoreGoal = UITheme.NewText("Score Goal", parent, UITheme.HudCaptionSize, UITheme.HudMuted, TextAnchor.MiddleCenter);
        UITheme.Place(scoreGoal.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -(UITheme.TimerSize.y + 6f)), new Vector2(480f, 22f));
        scoreGoal.supportRichText = true;
        UITheme.AddShadow(scoreGoal);

        RefreshScoreStrip();
    }

    private static ScorePlate NewScorePlate(string name, Transform parent, float x)
    {
        ScorePlate p = new ScorePlate();
        p.plate = UITheme.NewImage(name, parent, UITheme.TimerBackdrop, 8f);
        RectTransform rect = p.plate.rectTransform;
        UITheme.Place(rect, new Vector2(0.5f, 1f), new Vector2(x, 0f), new Vector2(PlateWidth, UITheme.TimerSize.y));

        // A line along the bottom in the side's colour, like the clock's accent line
        p.line = UITheme.NewImage("Line", rect, UITheme.Accent, 1.5f);
        UITheme.Place(p.line.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(PlateWidth - 20f, 3f));

        p.label = UITheme.NewText("Label", rect, 11, UITheme.HudMuted, TextAnchor.MiddleCenter);
        UITheme.Row(p.label.rectTransform, 3f, 14f);
        p.value = UITheme.NewText("Value", rect, 24, UITheme.TextColor, TextAnchor.MiddleCenter);
        UITheme.Row(p.value.rectTransform, 15f, 26f);
        p.value.supportRichText = true;
        return p;
    }

    private void RefreshScoreStrip()
    {
        if (leftPlate == null) return;

        bool showPlates = state == GameState.Starting || state == GameState.Playing || state == GameState.Ending;
        SetPlateShown(leftPlate, showPlates);
        SetPlateShown(rightPlate, showPlates);

        string goal = HasKillLimit ? $"FIRST TO {killcount} {ScoreWord}" : $"MOST {ScoreWord} WINS";
        string goalLine;

        if (IsTeamMode)
        {
            int myTeam = Mathf.Max(0, GetTeam(LocalActor));
            int otherTeam = 1 - myTeam;
            FillPlate(leftPlate, TeamNames[myTeam], TeamKills(myTeam).ToString(), TeamColors[myTeam]);
            FillPlate(rightPlate, TeamNames[otherTeam], TeamKills(otherTeam).ToString(), TeamColors[otherTeam]);
            goalLine = $"<color={TeamHex(myTeam)}>YOU'RE {TeamNames[myTeam]}</color>   ·   {goal}";
        }
        else
        {
            int mine = (myind >= 0 && playerInfo.Count > myind) ? playerInfo[myind].kills : 0;
            int others = 0;
            for (int i = 0; i < playerInfo.Count; i++)
            {
                if (i != myind && playerInfo[i].kills > others) others = playerInfo[i].kills;
            }
            string opponent = HudRules.OpponentLabel(mine, others);

            if (IsGunGame)
            {
                string of = $"<size=14><color={UITheme.Hex(UITheme.HudMuted)}>/{GunGameRules.Ladder.Length}</color></size>";
                FillPlate(leftPlate, "YOU", HudRules.GunGameForm(mine) + of, UITheme.Accent);
                FillPlate(rightPlate, opponent, HudRules.GunGameForm(others) + of, UITheme.HudMuted);
                int left = GunGameRules.KillsLeftOnStep(mine);
                bool lastForm = GunGameRules.StepFor(mine) == GunGameRules.Ladder.Length - 1;
                goalLine = $"{left} {(left == 1 ? "KILL" : "KILLS")} TO {(lastForm ? "WIN" : "NEXT FORM")}";
            }
            else
            {
                FillPlate(leftPlate, "YOU", mine.ToString(), UITheme.Accent);
                FillPlate(rightPlate, opponent, others.ToString(), UITheme.HudMuted);
                goalLine = goal;
            }
        }

        if (scoreGoal.text != goalLine)
        {
            scoreGoal.text = goalLine;
            scoreGoalBackdrop = UITheme.FitBackdrop(scoreGoalBackdrop, scoreGoal, 12f, 22f);
        }
    }

    private static void SetPlateShown(ScorePlate p, bool show)
    {
        if (p.plate.gameObject.activeSelf != show) p.plate.gameObject.SetActive(show);
    }

    private static void FillPlate(ScorePlate p, string label, string value, Color lineColor)
    {
        p.label.text = label;
        p.value.text = value;
        lineColor.a = 0.9f;
        if (p.line.color != lineColor) p.line.color = lineColor;
    }
}
