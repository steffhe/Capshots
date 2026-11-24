using System.Collections.Generic;

// The awards on the results screen: a few titles for whoever did something best (or worst) in the
// match. The host counts what each player did (Manager.Awards.cs) and picks them here when the
// match ends; everyone's results screen shows them. Kept apart from the match so the EditMode tests
// can check the rules.
public static class MatchAwards
{
    public const int MaxAwards = 4;
    // A streak shorter than this, or fewer deaths than this, isn't worth an award
    public const int MinStreak = 3;
    public const int MinDeaths = 3;

    // What one player (or bot) did this match
    public struct Tally
    {
        public int actor;
        public string name;
        public int kills, deaths, capshots, grenadeKills, bestStreak;
    }

    public struct Award
    {
        public string title;
        public string detail;
        public int actor;
        public string name;
    }

    // Up to MaxAwards, in this order, each to the player who did it most (on a tie, the one with
    // more kills, then the first listed). An award nobody qualifies for is left out.
    public static List<Award> Choose(IList<Tally> players)
    {
        List<Award> awards = new List<Award>();
        if (players == null || players.Count < 2) return awards;

        AddBest(awards, players, "CAPSHOT KING", t => t.capshots, 1, n => n == 1 ? "1 CAPSHOT" : $"{n} CAPSHOTS");
        AddBest(awards, players, "ON A ROLL", t => t.bestStreak, MinStreak, n => $"{n} KILLS IN A ROW");
        AddBest(awards, players, "STICKY FINGERS", t => t.grenadeKills, 1, n => n == 1 ? "1 GRENADE KILL" : $"{n} GRENADE KILLS");
        AddUntouchable(awards, players);
        AddBest(awards, players, "PUNCHING BAG", t => t.deaths, MinDeaths, n => $"{n} DEATHS");

        if (awards.Count > MaxAwards) awards.RemoveRange(MaxAwards, awards.Count - MaxAwards);
        return awards;
    }

    private static void AddBest(List<Award> awards, IList<Tally> players, string title, System.Func<Tally, int> score, int minimum, System.Func<int, string> detail)
    {
        int best = -1;
        for (int i = 0; i < players.Count; i++)
        {
            int s = score(players[i]);
            if (s < minimum) continue;
            if (best < 0 || s > score(players[best]) || (s == score(players[best]) && players[i].kills > players[best].kills)) best = i;
        }
        if (best < 0) return;
        awards.Add(new Award { title = title, detail = detail(score(players[best])), actor = players[best].actor, name = players[best].name });
    }

    // Fewest deaths among those who got a kill (on a tie, more kills)
    private static void AddUntouchable(List<Award> awards, IList<Tally> players)
    {
        int best = -1;
        for (int i = 0; i < players.Count; i++)
        {
            Tally t = players[i];
            if (t.kills < 1) continue;
            if (best < 0 || t.deaths < players[best].deaths || (t.deaths == players[best].deaths && t.kills > players[best].kills)) best = i;
        }
        if (best < 0) return;
        int deaths = players[best].deaths;
        awards.Add(new Award { title = "UNTOUCHABLE", detail = deaths == 0 ? "NEVER DIED" : deaths == 1 ? "DIED ONCE" : $"DIED {deaths} TIMES", actor = players[best].actor, name = players[best].name });
    }

    // Packed for the RPC that sends them to everyone: title, detail, actor and name, one award per line
    public static string Pack(List<Award> awards)
    {
        System.Text.StringBuilder s = new System.Text.StringBuilder();
        foreach (Award a in awards)
        {
            s.Append(Clean(a.title)).Append('|').Append(Clean(a.detail)).Append('|').Append(a.actor).Append('|').Append(Clean(a.name)).Append('\n');
        }
        return s.ToString();
    }

    public static List<Award> Unpack(string packed)
    {
        List<Award> awards = new List<Award>();
        if (string.IsNullOrEmpty(packed)) return awards;
        foreach (string line in packed.Split('\n'))
        {
            string[] parts = line.Split('|');
            if (parts.Length != 4 || !int.TryParse(parts[2], out int actor)) continue;
            awards.Add(new Award { title = parts[0], detail = parts[1], actor = actor, name = parts[3] });
            if (awards.Count == MaxAwards) break;
        }
        return awards;
    }

    // Names can hold anything a player typed: keep the separators out
    private static string Clean(string text) => string.IsNullOrEmpty(text) ? "" : text.Replace('|', '/').Replace('\n', ' ');
}
