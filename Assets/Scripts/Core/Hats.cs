using System.Collections.Generic;

// The hats players can wear, and the level each one unlocks at. A hat is unlocked once the player's
// level reaches it, so nothing about unlocks is saved: only which hat is picked (ProfileData.hat).
// A hat's number is its place in this list, which is what's saved and sent over the network, so new
// hats go on the end. Every hat is drawn in the player's (or their team's) colour by PlayerCap.
public static class Hats
{
    public const int Cap = 0, BackwardsCap = 1, Beanie = 2, BucketHat = 3, HardHat = 4,
        PropellerBeanie = 5, TopHat = 6, VikingHelmet = 7, Crown = 8, Halo = 9;

    private static readonly string[] names =
    {
        "CAP", "BACKWARDS CAP", "BEANIE", "BUCKET HAT", "HARD HAT",
        "PROPELLER BEANIE", "TOP HAT", "VIKING HELMET", "CROWN", "HALO",
    };

    // Early hats come within the first few matches; later ones are long-term goals
    private static readonly int[] unlockLevels = { 0, 2, 4, 6, 9, 12, 16, 20, 25, 30 };

    // Bots wear one of the first few hats, so they look like players who've played a little
    public const int BotHatCount = 5;

    public static int Count => names.Length;

    public static bool IsValid(int hat) => hat >= 0 && hat < names.Length;

    public static string Name(int hat) => IsValid(hat) ? names[hat] : names[Cap];

    public static int UnlockLevel(int hat) => IsValid(hat) ? unlockLevels[hat] : int.MaxValue;

    public static bool IsUnlocked(int hat, int level) => IsValid(hat) && level >= unlockLevels[hat];

    // The hat to actually wear: the one picked if it's unlocked at this level, otherwise the cap
    public static int Wearable(int hat, int level) => IsUnlocked(hat, level) ? hat : Cap;

    // The hats unlocked by going from one level to another, in unlock order
    public static List<int> UnlockedBetween(int oldLevel, int newLevel)
    {
        List<int> unlocked = new List<int>();
        for (int i = 0; i < names.Length; i++)
        {
            if (unlockLevels[i] > oldLevel && unlockLevels[i] <= newLevel) unlocked.Add(i);
        }
        return unlocked;
    }

    // The next hat still to unlock at this level, or -1 if they're all unlocked
    public static int NextUnlock(int level)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (unlockLevels[i] > level) return i;
        }
        return -1;
    }
}
