using UnityEngine;

// Rules for a single shot of the gun (see Weapon), kept apart so they can be tested
public static class ShotRules
{
    // How early a semi-automatic click may come before the gun is ready and still fire, as soon as
    // it is: 4 ticks at 40 a second. Short enough that a click for a later shot isn't taken early.
    public const float FireBufferSeconds = 0.1f;

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

    // The sideways kick of shot number 'shot' in a burst (0 = the first), -1 (left) to 1 (right), the
    // same in every burst so it can be learnt: the first shots go nearly straight up, then the kick
    // sways right and back left about once every 11 shots.
    public static float RecoilPattern(int shot)
    {
        if (shot <= 0) return 0f;
        float rampIn = Mathf.Clamp01(shot / 4f);
        return Mathf.Sin(shot * 0.55f + 0.4f) * rampIn;
    }

    // How hard a hit landed, as a byte for the network (0 = nothing, 255 = all of the target's health
    // or more): the hit marker and sound grow with it
    public static byte HitWeightByte(int damage, int maxHealth)
    {
        if (damage <= 0 || maxHealth <= 0) return 0;
        return (byte)Mathf.Clamp(Mathf.RoundToInt(255f * damage / maxHealth), 1, 255);
    }

    // Where a shot lands inside its spread, from two random numbers 0 to 1: evenly over a disc of
    // radius 1 (times the spread), so the spread is round, as the HUD's ticks show it. (It used to be
    // a square, whose corners reached 41% further than the ticks.)
    public static Vector2 DiskPoint(float u1, float u2)
    {
        float r = Mathf.Sqrt(Mathf.Clamp01(u1));
        float a = u2 * Mathf.PI * 2f;
        return new Vector2(r * Mathf.Cos(a), r * Mathf.Sin(a));
    }

    // The shotgun's pellets keep a pattern, so a blast lands about the same every time: one in the
    // middle and the rest in a ring at PelletRing of the spread, the ring turned by 'rotation'
    // (radians, different every blast) and each pellet nudged by 'jitter' (up to PelletJitter).
    public const float PelletRing = 0.7f;
    public const float PelletJitter = 0.3f;

    public static Vector2 PelletOffset(int pellet, int pellets, float rotation, Vector2 jitter)
    {
        Vector2 nudge = Vector2.ClampMagnitude(jitter, PelletJitter);
        if (pellet <= 0 || pellets <= 1) return nudge;
        float a = rotation + Mathf.PI * 2f * (pellet - 1) / (pellets - 1);
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * PelletRing + nudge;
    }

    // A number from -1 to 1 that only depends on the seed, the same on every machine
    public static float Seeded(int seed)
    {
        unchecked
        {
            uint h = (uint)seed * 2654435761u;
            h ^= h >> 13;
            h *= 0x5bd1e995u;
            h ^= h >> 15;
            return h / (float)uint.MaxValue * 2f - 1f;
        }
    }

    // The same, from 0 to 1
    public static float SeededUnit(int seed) => (Seeded(seed) + 1f) * 0.5f;

    // The direction of pellet 'pellet' of a shot of 'pellets', aimed along 'view' with 'spread'
    // (thousandths of the distance): the same on every machine for the same shot seed, so the host,
    // the shooter's own game and everyone drawing the shot from its message (Weapon.RPC_Shot) agree.
    // Round spread for a single shot; the shotgun's pattern (a centre pellet and a ring, each nudged a
    // little, the ring turned per blast) for pellets.
    public static Vector3 PelletDirection(Quaternion view, float spread, int shotSeed, int pellet, int pellets)
    {
        int seed = shotSeed + pellet * 2;
        Vector2 random = DiskPoint(SeededUnit(seed), SeededUnit(seed + 1));
        Vector2 offset = random;
        if (pellets > 1)
        {
            float ringTurn = SeededUnit(shotSeed + 997) * Mathf.PI * 2f;
            offset = PelletOffset(pellet, pellets, ringTurn, random * PelletJitter);
        }
        Vector3 direction = view * Vector3.forward * 1000f;
        direction += offset.y * spread * (view * Vector3.up);
        direction += offset.x * spread * (view * Vector3.right);
        return direction.normalized;
    }
}
