using UnityEngine;

// Gun Game (GunGameRules): everyone's gun form follows their place on the ladder, two kills on each
// form, and the last kill with the last form wins. The ladder kills are the scoreboard's kills, so
// the win check and sorting work unchanged; Weapon asks GunGameSlot which form a player holds.
public partial class Manager
{
    public bool IsGunGame => mode == MatchMode.GunGame;

    // The step this player was last told about, so each climb is called out once
    private int announcedGunGameStep = -1;

    // The loadout index of the form this player holds now
    public int GunGameSlot(int actor, Gun[] loadout, int fallback)
    {
        int index = FindPlayerIndex(actor);
        int kills = index >= 0 ? playerInfo[index].kills : 0;
        return GunGameRules.SlotFor(loadout, GunGameRules.StepFor(kills), fallback);
    }

    // The local player climbed the ladder: the new form in the middle of the screen
    private void AnnounceGunGameStep()
    {
        if (!IsGunGame || myind < 0 || myind >= playerInfo.Count) return;
        int kills = playerInfo[myind].kills;
        int step = GunGameRules.StepFor(kills);
        bool first = announcedGunGameStep < 0;
        if (step == announcedGunGameStep) return;
        announcedGunGameStep = step;
        if (first || state != GameState.Playing || kills >= GunGameRules.KillsToWin) return;

        bool last = step == GunGameRules.Ladder.Length - 1;
        int left = GunGameRules.KillsLeftOnStep(kills);
        FeedbackUI.Callout(GunGameRules.Ladder[step].ToUpperInvariant(), last ? $"LAST FORM: {left} KILLS TO WIN" : $"STEP {step + 1} OF {GunGameRules.Ladder.Length}");
    }
}
