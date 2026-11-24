// Multi-kills and kill streaks: the host counts them (Manager.PlayerKilled) and every player's game
// shows the callouts. Kept here, apart from the match, so the EditMode tests can check them.
public static class KillStreaks
{
    // Kills this close together (seconds) count as one multi-kill
    public const float MultiKillWindow = 4f;
    // A streak this long or longer is announced to everyone when it's ended
    public const int ShutdownStreak = 5;
    public const int ShutdownXp = 100;

    // "DOUBLE KILL" for the second kill in a row within the window, and so on; null for a single kill
    public static string MultiKillName(int kills)
    {
        switch (kills)
        {
            case 0:
            case 1: return null;
            case 2: return "DOUBLE KILL";
            case 3: return "TRIPLE KILL";
            case 4: return "QUAD KILL";
            default: return "MULTI KILL";
        }
    }

    // The callout for reaching a streak (kills without dying), or null if this length isn't a milestone
    public static string StreakName(int streak)
    {
        if (streak < 5 || streak % 5 != 0) return null;
        switch (streak)
        {
            case 5: return "KILLING SPREE";
            case 10: return "RAMPAGE";
            case 15: return "UNSTOPPABLE";
            default: return "LEGENDARY";
        }
    }

    // Bonus XP on top of the kill's own: 50 for each kill after the first in a multi-kill (at most
    // 200), and 100 for reaching a streak milestone
    public static int BonusXp(int multiKill, int streak)
    {
        int bonus = 0;
        if (multiKill >= 2) bonus += System.Math.Min(200, 50 * (multiKill - 1));
        if (StreakName(streak) != null) bonus += 100;
        return bonus;
    }
}
