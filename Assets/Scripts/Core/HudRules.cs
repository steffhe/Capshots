using UnityEngine;

// The arithmetic behind the combat HUD (HudFeedback, Manager.ScoreStrip), kept apart from the UI so
// the EditMode tests can check it: the safe width on very wide screens, the energy meter's segments,
// health states, and who the score strip compares you with.
public static class HudRules
{
    #region Safe width

    // The HUD keeps to a centred area no wider than this (width / height), so on an ultrawide screen
    // the health and weapon readouts stay near the crosshair instead of in the far corners. 2.0 lets
    // 16:9 and 16:10 use the full width and brings 21:9 in by about 7% on each side.
    public const float MaxHudAspect = 2f;

    // How far in from each side the HUD sits, in canvas units, for a canvas this size: 0 unless the
    // canvas is wider than maxAspect allows
    public static float SafeInset(float width, float height, float maxAspect = MaxHudAspect)
    {
        if (width <= 0f || height <= 0f || maxAspect <= 0f) return 0f;
        float allowed = height * maxAspect;
        return width > allowed ? (width - allowed) * 0.5f : 0f;
    }

    #endregion

    #region Energy meter

    // At most this many segments are drawn; a form with more shots to a cell gets a smooth bar
    public const int MaxEnergySegments = 12;

    // How many segments the meter shows for a form: one per shot in a full cell, or 0 for a smooth
    // bar when that would be more than maxSegments (the machine gun's 29) or there's no cost
    public static int EnergySegments(float maxEnergy, float costPerShot, int maxSegments = MaxEnergySegments)
    {
        int shots = GunEnergy.ShotsLeft(maxEnergy, costPerShot);
        if (costPerShot <= 0f || shots <= 0 || shots > maxSegments) return 0;
        return shots;
    }

    // How full segment 'index' (0 = leftmost) is, 0 to 1. A segment is one shot's worth of energy,
    // so the segment being refilled shows how close the next shot is.
    public static float SegmentFill(float energy, float costPerShot, int index)
    {
        if (costPerShot <= 0f) return 1f;
        return Mathf.Clamp01(energy / costPerShot - index);
    }

    #endregion

    #region Health

    public enum HealthState { Healthy, Low, Critical }

    // Thresholds as a share of full health: amber at half, red at a quarter
    public const float LowHealth = 0.5f;
    public const float CriticalHealth = 0.25f;

    public static HealthState StateFor(float ratio)
    {
        if (ratio <= CriticalHealth) return HealthState.Critical;
        if (ratio <= LowHealth) return HealthState.Low;
        return HealthState.Healthy;
    }

    #endregion

    #region Score strip

    // Free for all: who the strip compares you with. While you lead outright it's the runner-up
    // ("2ND"), otherwise the leader (a tie at the top counts as someone else leading, so the strip
    // never claims a lead you don't have)
    public static string OpponentLabel(int mine, int bestOfOthers)
    {
        return mine > bestOfOthers ? "2ND" : "LEADER";
    }

    // Gun Game: the form a player is on, counted from 1, for "2/4"
    public static int GunGameForm(int kills)
    {
        return GunGameRules.StepFor(kills) + 1;
    }

    // Seconds to show on a countdown: rounded up, so it reads 1 until it's over, never below 0
    public static int CountdownSeconds(float remaining)
    {
        return remaining <= 0f ? 0 : Mathf.CeilToInt(remaining);
    }

    #endregion
}
