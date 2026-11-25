using System.Collections.Generic;

// The hats players can wear, and the level each one unlocks at. A hat is unlocked once the player's
// level reaches it, so nothing about unlocks is saved: only which hat is picked (ProfileData.hat).
// A hat's number is its place in this list, which is what's saved and sent over the network: hats
// are kept in unlock order, so adding one before a saved hat changes which hat that save picks.
// Every hat is drawn in the player's (or their team's) colour by PlayerCap.
public static class Hats
{
    public const int Cap = 0, Headset = 1, BackwardsCap = 2, TrafficCone = 3, Beanie = 4,
        Toadstool = 5, BucketHat = 6, Antenna = 7, GraduationCap = 8, HardHat = 9, Plunger = 10,
        MilitaryBeret = 11, PropellerBeanie = 12, BirthdayCake = 13, CowboyHat = 14, Siren = 15, TopHat = 16,
        RadarDish = 17, Sombrero = 18, JesterHat = 19, VikingHelmet = 20, PirateHat = 21,
        PlasmaGlobe = 22, Fishbowl = 23, Crown = 24, Ufo = 25, WizardHat = 26, DevilHorns = 27, Atom = 28, Halo = 29;

    private static readonly string[] names =
    {
        "CAP", "GAMER HEADSET", "BACKWARDS CAP", "TRAFFIC CONE", "BEANIE",
        "TOADSTOOL", "BUCKET HAT", "ANTENNA", "GRADUATION CAP", "HARD HAT", "PLUNGER",
        "MILITARY BERET", "PROPELLER BEANIE", "BIRTHDAY CAKE", "COWBOY HAT", "SIREN", "TOP HAT",
        "RADAR DISH", "SOMBRERO", "JESTER HAT", "VIKING HELMET", "PIRATE HAT",
        "PLASMA GLOBE", "FISHBOWL", "CROWN", "UFO", "WIZARD HAT", "DEVIL HORNS", "ATOM", "HALO",
    };

    // Early hats come within the first few matches; later ones are long-term goals
    private static readonly int[] unlockLevels =
    {
        0, 1, 2, 3, 4,
        5, 6, 7, 8, 9, 10,
        11, 12, 13, 14, 15, 16,
        17, 18, 19, 20, 22,
        23, 24, 25, 26, 27, 28, 29, 30,
    };

    // Bots wear one of the hats unlocked by level 9, so they look like players who've played a little
    public const int BotHatCount = 10;

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

    // Whether going from one level to another unlocks any hat (UnlockedBetween, without the list)
    public static bool AnyUnlockedBetween(int oldLevel, int newLevel)
    {
        for (int i = 0; i < unlockLevels.Length; i++)
        {
            if (unlockLevels[i] > oldLevel && unlockLevels[i] <= newLevel) return true;
        }
        return false;
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
