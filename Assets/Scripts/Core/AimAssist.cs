using UnityEngine;

// Aim assist for gamepad and touch players (Look): turning slows down while the crosshair is over an
// enemy, so a stick or a thumb can hold it there, as a mouse can. Only a slowdown, never a pull, and
// never for a mouse. Kept here so the EditMode tests can check it.
public static class AimAssist
{
    // How slow turning gets right on target, as a share of normal
    public const float StrongestSlowdown = 0.45f;
    // No help beyond this distance (metres)
    public const float Range = 60f;
    // The target's size: about a body's half-width, in metres; the help fades out over this much
    // more angle again beyond it
    private const float TargetRadius = 0.6f;
    private const float MinCone = 1.5f, MaxCone = 8f;

    // The cone (half-angle, degrees) around a target at this distance inside which turning slows
    public static float Cone(float distance)
    {
        if (distance <= 0.01f) return MaxCone;
        float angle = Mathf.Atan2(TargetRadius, distance) * Mathf.Rad2Deg * 2f;
        return Mathf.Clamp(angle, MinCone, MaxCone);
    }

    // How much to scale turning by, for a target this many degrees off the crosshair at this distance:
    // 1 (no change) outside the cone or out of range, down to StrongestSlowdown right on it
    public static float Slowdown(float angle, float distance)
    {
        if (distance > Range || distance < 0f) return 1f;
        float cone = Cone(distance);
        if (angle >= cone) return 1f;
        return Mathf.Lerp(StrongestSlowdown, 1f, Mathf.Clamp01(angle / cone));
    }
}
