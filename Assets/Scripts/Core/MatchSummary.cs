using System.Collections.Generic;

// The local player's own numbers for the results screen: accuracy, cap shots and the gun form they
// got the most kills with. Counted on their own game while the match is being played.
public class MatchSummary
{
    public int shots;
    public int shotsThatHit;
    public int kills;
    public int capshots;
    public int bestStreak;
    private int streak;
    private readonly Dictionary<string, int> killsByGun = new Dictionary<string, int>();

    public void Reset()
    {
        shots = shotsThatHit = kills = capshots = bestStreak = streak = 0;
        killsByGun.Clear();
    }

    public void Shot() => shots++;

    // A shot hit someone (once per shot, however many pellets or players it hit)
    public void Hit()
    {
        if (shotsThatHit < shots) shotsThatHit++;
    }

    public void Kill(string gun, bool capshot)
    {
        kills++;
        if (capshot) capshots++;
        streak++;
        if (streak > bestStreak) bestStreak = streak;
        if (string.IsNullOrEmpty(gun)) return;
        killsByGun.TryGetValue(gun, out int n);
        killsByGun[gun] = n + 1;
    }

    public void Died() => streak = 0;

    // Whole percent of shots that hit someone; 0 with no shots
    public int AccuracyPercent => shots > 0 ? (int)System.Math.Round(100.0 * shotsThatHit / shots) : 0;

    // Whole percent of kills that were cap shots; 0 with no kills
    public int CapshotPercent => kills > 0 ? (int)System.Math.Round(100.0 * capshots / kills) : 0;

    // The gun form with the most kills (the first to get there on a tie), or null with no kills
    public string FavouriteGun
    {
        get
        {
            string best = null;
            int bestKills = 0;
            foreach (KeyValuePair<string, int> entry in killsByGun)
            {
                if (entry.Value > bestKills) { best = entry.Key; bestKills = entry.Value; }
            }
            return best;
        }
    }
}
