// Gun Game: everyone climbs the same ladder of gun forms, KillsPerStep kills on each, and the first
// to get the last kill with the last form wins. Only gun kills count (not grenades). The match
// (Manager, Weapon) moves each player's gun along as they score; the rules are here so the EditMode
// tests can check them.
public static class GunGameRules
{
    // The ladder, by gun name (Gun.gunName): the forgiving automatic first, the sniper rifle last
    public static readonly string[] Ladder = { "Machine Gun", "Shotgun", "Pistol", "Sniper Rifle" };
    public const int KillsPerStep = 2;
    public static int KillsToWin => Ladder.Length * KillsPerStep;

    // The step (0 = first form) for a player with this many ladder kills
    public static int StepFor(int kills)
    {
        if (kills < 0) kills = 0;
        int step = kills / KillsPerStep;
        return step < Ladder.Length ? step : Ladder.Length - 1;
    }

    // Kills still needed on this step before the next form (or the win, on the last step)
    public static int KillsLeftOnStep(int kills)
    {
        if (kills < 0) kills = 0;
        if (kills >= KillsToWin) return 0;
        return KillsPerStep - kills % KillsPerStep;
    }

    // Whether a kill with this weapon moves the killer up the ladder (gun kills only)
    public static bool Counts(string weaponName, string grenadeName)
    {
        return !string.IsNullOrEmpty(weaponName) && weaponName != grenadeName && weaponName != "Fell";
    }

    // The loadout index of a step's form, or fallback if the loadout hasn't got it
    public static int SlotFor(Gun[] loadout, int step, int fallback)
    {
        if (step < 0) step = 0;
        if (step >= Ladder.Length) step = Ladder.Length - 1;
        return GunForms.IndexOf(loadout, Ladder[step], fallback);
    }
}
