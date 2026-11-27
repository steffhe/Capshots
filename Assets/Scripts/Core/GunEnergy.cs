using UnityEngine;

// The rules of the gun's shared energy pool (see Weapon), kept apart so they can be tested
public static class GunEnergy
{
    // A sliver of slack, so a pool of exactly 3 x 33.33 still allows 3 shots despite rounding
    private const float Slack = 0.001f;

    public static bool CanFire(float energy, float costPerShot)
    {
        return energy + Slack >= Mathf.Max(0f, costPerShot);
    }

    // Whole shots the energy pays for at this cost per shot
    public static int ShotsLeft(float energy, float costPerShot)
    {
        if (costPerShot <= 0f) return 99;
        return Mathf.Max(0, Mathf.FloorToInt((energy + Slack) / costPerShot));
    }

    // The energy after refilling for 'seconds' at 'rate' a second, never above max
    public static float Regenerate(float energy, float max, float rate, float seconds)
    {
        return Mathf.Min(max, energy + Mathf.Max(0f, rate) * Mathf.Max(0f, seconds));
    }

    // Out of energy for a shot: whether to swap the cell (true) or just wait for the shot's energy to
    // refill (false). Waiting wins when the refill, starting 'secondsUntilRegen' from now at 'rate' a
    // second, covers the shot within 'patience' seconds, so a sniper rifle a sliver short of a shot
    // doesn't start a whole cell swap.
    public static bool ShouldSwapCell(float energy, float costPerShot, float rate, float secondsUntilRegen, float patience)
    {
        if (CanFire(energy, costPerShot)) return false;
        if (rate <= 0f) return true;
        float wait = Mathf.Max(0f, secondsUntilRegen) + (costPerShot - energy) / rate;
        return wait > patience;
    }
}
