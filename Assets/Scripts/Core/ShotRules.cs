using UnityEngine;

// Rules for a single shot of the gun (see Weapon), kept apart so they can be tested
public static class ShotRules
{
    // How far aiming down the sights has steadied the gun, 0 (just raised) to 1 (fully steady),
    // after aiming for 'seconds'. Aiming only tightens the spread over the gun's adsTime, so pressing
    // aim and fire together (a "quickscope" or a key bound to both) doesn't give scoped accuracy.
    public static float AimProgress(float seconds, float adsTime)
    {
        if (adsTime <= 0f) return 1f;
        return Mathf.Clamp01(seconds / adsTime);
    }

    // The spread while aiming 'progress' of the way in: from the hip spread down to the fully aimed
    // spread (hip divided by aimDivisor), in a straight line
    public static float AimedBloom(float hipBloom, float aimDivisor, float progress)
    {
        float aimed = hipBloom / Mathf.Max(1f, aimDivisor);
        return Mathf.Lerp(hipBloom, aimed, Mathf.Clamp01(progress));
    }

    // Whether a shot's hit on one player counts as a cap shot (a headshot). A single bullet does when
    // it hit the head. A shotgun blast does when at least half of its damage to that player came from
    // pellets that hit the head, so one stray pellet clipping the head doesn't make a blast to the
    // body a cap shot.
    public static bool IsCapshot(int headDamage, int totalDamage)
    {
        if (headDamage <= 0 || totalDamage <= 0) return false;
        return headDamage * 2 >= totalDamage;
    }
}
