// The order the create tab's mode list shows the game modes in (MatchMode values, which can't change:
// rooms send them, and older builds read them). Team Cap Grab comes right under Cap Grab. Kept here
// so the EditMode tests can check it.
public static class ModeOrder
{
    // FreeForAll, TeamDeathmatch, CapGrab, TeamCapGrab, GunGame
    public static readonly int[] Menu = { 0, 1, 2, 4, 3 };
}
