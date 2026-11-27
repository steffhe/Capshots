using UnityEngine;

// Rules for looking around, kept apart so they can be tested: how aiming changes sensitivity, how
// sensitivity translates into mouse distance, and frame-rate independent easing for camera effects.
// Used by Look and Player on the local player's game only (none of this is simulated).
public static class LookRules
{
    // How much slower to turn while zoomed in so the same mouse movement sweeps the same distance on
    // screen: the ratio of the two views' half-widths. fov values are Unity's vertical field of view
    // in degrees. 1 when either value doesn't make sense.
    public static float ZoomSensitivity(float zoomedFov, float baseFov)
    {
        if (zoomedFov <= 0f || baseFov <= 0f || zoomedFov >= 179f || baseFov >= 179f) return 1f;
        float zoomed = Mathf.Tan(zoomedFov * 0.5f * Mathf.Deg2Rad);
        float normal = Mathf.Tan(baseFov * 0.5f * Mathf.Deg2Rad);
        return normal > 0f ? zoomed / normal : 1f;
    }

    // A gamepad stick's response curve: the push (0 to 1, after the Input System's deadzone) raised to
    // exponent, keeping the direction. With 2, half a push turns at a quarter speed, for fine aiming,
    // and a full push still turns at full speed. Measured round the stick, so diagonals don't bend.
    public static Vector2 StickResponse(Vector2 stick, float exponent)
    {
        float push = stick.magnitude;
        if (push <= 0.0001f) return Vector2.zero;
        float curved = Mathf.Pow(Mathf.Min(push, 1f), Mathf.Max(0.1f, exponent));
        return stick / push * curved;
    }

    // Mouse travel for a full turn, in centimetres, at degreesPerCount and dpi counts per inch
    public static float CentimetresPer360(float degreesPerCount, float dpi)
    {
        if (degreesPerCount <= 0f || dpi <= 0f) return 0f;
        return 360f / degreesPerCount / dpi * 2.54f;
    }

    // The share of the way to move towards a target this frame, for easing that was tuned as
    // Lerp(current, target, rate * deltaTime) at 60 frames a second: the same at 60, and the same
    // overall at any other frame rate (it used to snap at low frame rates and lag at high ones)
    public static float Ease(float rate, float deltaTime)
    {
        if (deltaTime <= 0f || rate <= 0f) return 0f;
        float perFrame = Mathf.Min(rate / 60f, 0.999f);
        return 1f - Mathf.Pow(1f - perFrame, deltaTime * 60f);
    }
}
