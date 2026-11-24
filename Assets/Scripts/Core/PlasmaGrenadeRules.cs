using UnityEngine;

// The rules of the plasma grenade (see Manager.Grenades and PlasmaGrenade), kept apart so they can be
// tested: how it's thrown, how it bounces, when it sticks to a player and how hard the blast hits.
// It sticks only to players. Walls and floors it bounces off, and it goes off a few seconds after the
// throw wherever it ended up.
public static class PlasmaGrenadeRules
{
    // Everyone spawns with one, and can carry no more. An ammo box gives a used one back.
    public const int MaxCarried = 1;

    // The glowing ball's radius, for bouncing off scenery
    public const float Radius = 0.12f;
    // Extra reach when checking whether it touched a player, so a near miss still sticks
    public const float StickSlack = 0.15f;

    // Throw: speed along the view, plus a little lift so it carries in an arc
    public const float ThrowSpeed = 20f;
    public const float ThrowLift = 3.5f;
    // How much of the thrower's own movement it carries
    public const float InheritVelocity = 0.5f;
    // Lighter than the players' gravity (20 m/s²), for a floaty plasma arc
    public const float Gravity = 13f;

    // A bounce keeps this much of the speed into the surface...
    public const float Restitution = 0.35f;
    // ...and this much of the speed along it
    public const float Friction = 0.75f;
    // Slower than this into a surface and it doesn't bounce, it slides
    public const float MinBounceSpeed = 1.5f;

    // Goes off this long after the throw if it never found a player...
    public const float FreeFuse = 3f;
    // ...or this long after sticking to one
    public const float StuckFuse = 1.5f;
    // You can't fire for this long after throwing
    public const float ThrowTime = 0.45f;

    // Blast: full damage up to InnerRadius, falling to EdgeDamage at BlastRadius, nothing beyond.
    // Players have 1000 health, so the middle of the blast kills.
    public const float BlastRadius = 4.5f;
    public const float InnerRadius = 1f;
    public const int CentreDamage = 1000;
    public const int EdgeDamage = 150;
    // Whoever it's stuck to: always enough to kill
    public const int StuckDamage = 100000;

    // The throw's starting velocity, from where the player looks and how they're moving
    public static Vector3 ThrowVelocity(Vector3 viewForward, Vector3 throwerVelocity)
    {
        Vector3 forward = viewForward.sqrMagnitude > 0f ? viewForward.normalized : Vector3.forward;
        return forward * ThrowSpeed + Vector3.up * ThrowLift + throwerVelocity * InheritVelocity;
    }

    // Damage at a distance from the blast's centre (to the nearest point of the player's body)
    public static int BlastDamage(float distance)
    {
        if (distance >= BlastRadius) return 0;
        if (distance <= InnerRadius) return CentreDamage;
        float t = (distance - InnerRadius) / (BlastRadius - InnerRadius);
        return Mathf.RoundToInt(Mathf.Lerp(CentreDamage, EdgeDamage, t));
    }

    // The velocity after hitting a surface with this normal: bounces off it when hitting it hard
    // enough, otherwise just loses the speed into it and slides along it. Moving away from the
    // surface is left alone.
    public static Vector3 Bounce(Vector3 velocity, Vector3 normal)
    {
        normal = normal.normalized;
        float into = Vector3.Dot(velocity, normal);
        if (into >= 0f) return velocity;

        Vector3 along = (velocity - normal * into) * Friction;
        float speedIn = -into;
        if (speedIn < MinBounceSpeed) return along;
        return along + normal * speedIn * Restitution;
    }

    // Closest points between segment p1-q1 and segment p2-q2 (from Real-Time Collision Detection,
    // Ericson 5.1.9). s and t say how far along each segment the points are, 0 to 1.
    public static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 c1, out Vector3 c2)
    {
        const float Epsilon = 1e-6f;
        Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
        float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
        float s, t;

        if (a <= Epsilon && e <= Epsilon)
        {
            s = t = 0f;
        }
        else if (a <= Epsilon)
        {
            s = 0f;
            t = Mathf.Clamp01(f / e);
        }
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e <= Epsilon)
            {
                t = 0f;
                s = Mathf.Clamp01(-c / a);
            }
            else
            {
                float b = Vector3.Dot(d1, d2);
                float denom = a * e - b * b;
                s = denom > Epsilon ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                t = (b * s + f) / e;
                if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
            }
        }

        c1 = p1 + d1 * s;
        c2 = p2 + d2 * t;
        return Vector3.Distance(c1, c2);
    }

    // Whether a grenade moving from 'from' to 'to' touches a body capsule (the line from bottom to
    // top, swollen by radius). If it does, 'stickPoint' is where on the body's surface it lands.
    public static bool TouchesCapsule(Vector3 from, Vector3 to, Vector3 bottom, Vector3 top, float radius, out Vector3 stickPoint)
    {
        float reach = radius + Radius + StickSlack;
        float distance = SegmentDistance(from, to, bottom, top, out Vector3 onPath, out Vector3 onAxis);
        stickPoint = onPath;
        if (distance > reach) return false;

        Vector3 outward = onPath - onAxis;
        if (outward.sqrMagnitude < 1e-6f)
        {
            // Right through the middle: put it on the side it came from
            outward = from - to;
            outward.y = 0f;
            if (outward.sqrMagnitude < 1e-6f) outward = Vector3.forward;
        }
        stickPoint = onAxis + outward.normalized * radius;
        return true;
    }
}
