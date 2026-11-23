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
}
