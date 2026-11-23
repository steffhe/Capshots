using UnityEngine;

// The parts of the bots' thinking that don't need a match running: how many bots to fill a room
// with, which gun form suits a distance, and the aiming and steering maths. BotBrain uses these
// every tick on the host; they're kept here so the EditMode tests can check them.
public static class BotLogic
{
    #region Skill

    // How a bot plays. One set of values for now; tune here.
    // Seconds between first seeing an enemy and the first shot
    public const float ReactionTimeMin = 0.35f;
    public const float ReactionTimeMax = 0.65f;
    // How fast a bot can turn, in degrees a second, and how quickly it closes in on where it wants to look
    public const float MaxTurnSpeed = 540f;
    public const float AimSharpness = 9f;
    // Aim error in degrees: large on first sight, settling to the smaller value the longer the bot
    // keeps its aim on the same target
    public const float AimErrorStart = 5f;
    public const float AimErrorSettled = 1.4f;
    public const float AimSettleTime = 1.5f;
    // How far a bot notices enemies: anywhere within close range, and ahead of it up to max range
    public const float NoticeRange = 10f;
    public const float SightRange = 110f;
    public const float SightHalfAngle = 75f;

    #endregion

    #region Room filling

    // How many bots a room should have: the host's setting, but never more than the room has space
    // for alongside the people in it. People always get the space: bots leave as they join.
    public static int DesiredBotCount(int botSetting, int humans, int maxPlayers)
    {
        if (botSetting <= 0 || maxPlayers <= 0) return 0;
        return Mathf.Clamp(maxPlayers - humans, 0, botSetting);
    }

    private static readonly string[] Names =
    {
        "Volt", "Sprocket", "Rivet", "Gizmo", "Ratchet", "Piston", "Dynamo", "Servo",
        "Cog", "Flux", "Torque", "Pixel", "Widget", "Bolt", "Diode", "Gauge",
    };

    // A name for the nth bot added to a match
    public static string BotName(int n)
    {
        if (n < 0) n = -n;
        string name = Names[n % Names.Length];
        int round = n / Names.Length;
        return "BOT " + name + (round > 0 ? " " + (round + 1) : "");
    }

    #endregion

    #region Gun forms

    // Distances (metres) where a bot changes from the shotgun, and to the sniper
    public const float ShotgunRange = 9f;
    public const float SniperRange = 40f;

    // The loadout index of the form a bot wants at this distance to its target: shotgun up close,
    // the scoped form far away, otherwise an automatic one (or failing that the first ordinary form).
    // distance < 0 means no target: the all-round form.
    public static int ChooseGun(Gun[] loadout, float distance)
    {
        if (loadout == null || loadout.Length == 0) return 0;

        int shotgun = -1, sniper = -1, automatic = -1, plain = -1;
        for (int i = 0; i < loadout.Length; i++)
        {
            Gun gun = loadout[i];
            if (gun == null) continue;
            if (gun.pellets > 1) { if (shotgun < 0) shotgun = i; }
            else if (gun.scopeFov > 0f) { if (sniper < 0) sniper = i; }
            else if (gun.burst == 1) { if (automatic < 0) automatic = i; }
            else if (plain < 0) plain = i;
        }

        int allRound = automatic >= 0 ? automatic : plain >= 0 ? plain : 0;
        if (distance < 0f) return allRound;
        if (distance < ShotgunRange && shotgun >= 0) return shotgun;
        if (distance > SniperRange && sniper >= 0) return sniper;
        return allRound;
    }

    #endregion

    #region Aiming and steering

    // Yaw and pitch (degrees, the way PlayerInputData has them: positive pitch looks up) to look
    // from one point at another
    public static Vector2 LookAngles(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float flat = new Vector2(d.x, d.z).magnitude;
        float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        float pitch = Mathf.Atan2(d.y, flat) * Mathf.Rad2Deg;
        return new Vector2(yaw, pitch);
    }

    // Turns smoothly from the current angle towards the wanted one: quickly when far off, easing in
    // when close, never faster than MaxTurnSpeed. Works across the -180/180 wrap.
    public static float TurnTowards(float current, float target, float deltaTime)
    {
        float difference = Mathf.DeltaAngle(current, target);
        float step = difference * (1f - Mathf.Exp(-AimSharpness * deltaTime));
        float maxStep = MaxTurnSpeed * deltaTime;
        step = Mathf.Clamp(step, -maxStep, maxStep);
        // Snap the last tiny bit, so the bot can actually settle on its target
        if (Mathf.Abs(difference) < 0.05f) step = difference;
        return current + step;
    }

    // The aim error, in degrees, after keeping aim on one target for this many seconds
    public static float AimError(float trackingTime)
    {
        return Mathf.Lerp(AimErrorStart, AimErrorSettled, Mathf.Clamp01(trackingTime / AimSettleTime));
    }

    // Whether the gun points close enough to the target to be worth firing: within the target's own
    // size (about half a metre either side) or within a couple of degrees, whichever is wider
    public static bool OnTarget(float yawError, float pitchError, float distance)
    {
        float size = distance > 0.1f ? Mathf.Atan2(0.5f, distance) * Mathf.Rad2Deg : 90f;
        float tolerance = Mathf.Max(size, 2f);
        return Mathf.Abs(yawError) <= tolerance && Mathf.Abs(pitchError) <= tolerance;
    }

    // The movement input (x = strafe right, y = forward, as PlayerInputData.Move) that walks a body
    // facing 'yaw' in the world direction given. Only the horizontal part of the direction counts.
    public static Vector2 MoveInput(float yaw, Vector3 worldDirection)
    {
        Vector3 flat = new Vector3(worldDirection.x, 0f, worldDirection.z);
        if (flat.sqrMagnitude < 0.0001f) return Vector2.zero;
        Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * flat.normalized;
        return new Vector2(local.x, local.z);
    }

    #endregion
}
