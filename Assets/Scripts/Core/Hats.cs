using System.Collections.Generic;

// The hats players can wear, and the level each one unlocks at. A hat is unlocked once the player's
// level reaches it, so nothing about unlocks is saved: only which hat is picked (ProfileData.hat).
// A hat's number is its place in this list, which is what's saved and sent over the network: hats
// are kept in unlock order, so adding one before a saved hat changes which hat that save picks.
// Every hat is drawn in the player's (or their team's) colour by PlayerCap.
public static class Hats
{
    public const int Cap = 0, CatEarHeadset = 1, BackwardsCap = 2, TrafficCone = 3, Beanie = 4,
        ChefsHat = 5, BucketHat = 6, Antenna = 7, HardHat = 8, Plunger = 9, PropellerBeanie = 10,
        CowboyHat = 11, TopHat = 12, Sombrero = 13, VikingHelmet = 14, PirateHat = 15, Fishbowl = 16,
        Crown = 17, WizardHat = 18, DevilHorns = 19, Halo = 20;

    private static readonly string[] names =
    {
        "CAP", "CAT-EAR HEADSET", "BACKWARDS CAP", "TRAFFIC CONE", "BEANIE",
        "CHEF'S HAT", "BUCKET HAT", "ANTENNA", "HARD HAT", "PLUNGER", "PROPELLER BEANIE",
        "COWBOY HAT", "TOP HAT", "SOMBRERO", "VIKING HELMET", "PIRATE HAT", "FISHBOWL",
        "CROWN", "WIZARD HAT", "DEVIL HORNS", "HALO",
    };

    // Early hats come within the first few matches; later ones are long-term goals
    private static readonly int[] unlockLevels = { 0, 1, 2, 3, 4, 5, 6, 7, 9, 10, 12, 14, 16, 18, 20, 22, 24, 25, 27, 28, 30 };

    // Bots wear one of the hats unlocked by level 9, so they look like players who've played a little
    public const int BotHatCount = 9;

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
