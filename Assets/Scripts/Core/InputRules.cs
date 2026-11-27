using UnityEngine;

// Checks on what a player's game sends the host each tick (PlayerInputData), kept apart so they can
// be tested. A modified game could send anything: numbers that aren't numbers (NaN, infinity) would
// break the host's copy of the body and its shots, and a pitch past what the camera allows would let
// it aim where a player can't.
public static class InputRules
{
    // Makes the input safe to simulate: a move of at most length 1, the yaw within 0 to 360 and the
    // pitch within +-maxPitch. Anything that isn't a finite number becomes 0.
    public static void Sanitize(ref Vector2 move, ref float yaw, ref float pitch, float maxPitch)
    {
        if (!Finite(move.x) || !Finite(move.y)) move = Vector2.zero;
        else move = Vector2.ClampMagnitude(move, 1f);
        yaw = Finite(yaw) ? Mathf.Repeat(yaw, 360f) : 0f;
        float limit = Finite(maxPitch) ? Mathf.Abs(maxPitch) : 90f;
        pitch = Finite(pitch) ? Mathf.Clamp(pitch, -limit, limit) : 0f;
    }

    private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
}
