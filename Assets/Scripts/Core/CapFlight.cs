using UnityEngine;

// Cap Grab: the path of a cap shot off a head (CapPickup). The same on every machine, so everyone
// sees it land where the host put the pickup. Kept here so the EditMode tests can check it.
public static class CapFlight
{
    // Where the cap is at s (0 when it's knocked off, 1 when it lands): most of the way across early,
    // like something thrown; up quickly to height above the straight line, slowing at the top, then
    // fluttering down slower; swaying a little sideways (along side) on the way, but not at either end
    public static Vector3 Position(Vector3 from, Vector3 to, Vector3 side, float height, float s)
    {
        s = Mathf.Clamp01(s);
        float across = 1f - (1f - s) * (1f - s);
        Vector3 p = Vector3.Lerp(from, to, across);
        // The arc peaks about 40% of the way through
        float k = Mathf.Pow(s, 0.75f) * 2f - 1f;
        p.y += height * (1f - k * k);
        float sway = Mathf.Sin(s * Mathf.PI * 3f) * 0.18f * s * (1f - s) * 4f;
        return p + side * sway;
    }

    // How far it has turned end over end (degrees): three whole turns, slowing to land upright
    public static float Tumble(float s)
    {
        s = Mathf.Clamp01(s);
        return 1080f * (1f - (1f - s) * (1f - s) * (1f - s));
    }
}
