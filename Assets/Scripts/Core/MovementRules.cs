using UnityEngine;

// The rules for how a player's horizontal speed changes each tick: thrust towards what the input
// asks for, braking, and the weaker control in the air. Pure maths, kept apart from Player so it can
// be tested. Player.Move runs it once per simulated tick (host and the predicting owner alike), so it
// must stay deterministic: no time, no randomness, only its arguments.
//
// Velocities are horizontal, as (x, z) in a Vector2.
public static class MovementRules
{
    // How fast speed changes, in m/s per second. Filled in from Player's Inspector fields.
    public struct Tuning
    {
        // Thrust up to normal (walking) speed
        public float accel;
        // Slowing to a stop with no input
        public float brake;
        // Slowing to a lower speed while still moving (letting go of sprint, crouching, aiming, the
        // end of a slide)
        public float overspeedBrake;
        // Thrust above normal speed, up to sprint speed
        public float sprintAccel;
        // Thrust in the air
        public float airAccel;
        // In the air, speed above airSoftCap bleeds off this fast
        public float airDrag;
        public float airSoftCap;
    }

    // One tick on the ground. wish: the direction the input asks for (length 0 to 1 is ignored, only
    // its direction counts); wishSpeed: how fast, in m/s; normalSpeed: the speed above which the
    // gentler sprintAccel takes over.
    public static Vector2 GroundStep(Vector2 velocity, Vector2 wish, float wishSpeed, float normalSpeed, Tuning t, float dt)
    {
        if (wishSpeed <= 0.0001f || wish.sqrMagnitude < 0.000001f)
        {
            return Vector2.MoveTowards(velocity, Vector2.zero, t.brake * dt);
        }

        Vector2 dir = wish.normalized;
        float along = Vector2.Dot(velocity, dir);
        Vector2 side = velocity - dir * along;

        // Drift the input no longer asks for (turning, a new strafe) is braked and thrusted against
        float counter = (t.brake + t.accel) * dt;
        side = Vector2.MoveTowards(side, Vector2.zero, counter);

        if (along < 0f)
        {
            // Pushing the opposite way: braking and thrust together, so a counter-strafe stops quicker
            // than letting go
            along = Mathf.Min(along + counter, wishSpeed);
        }
        else if (along > wishSpeed)
        {
            along = Mathf.Max(wishSpeed, along - t.overspeedBrake * dt);
        }
        else
        {
            float normal = Mathf.Min(wishSpeed, normalSpeed);
            if (along < normal) along = Mathf.Min(normal, along + t.accel * dt);
            else along = Mathf.Min(wishSpeed, along + t.sprintAccel * dt);
        }

        return dir * along + side;
    }

    // One tick in the air: momentum is kept, input steers towards the wished direction but can't make
    // the body go faster than wishSpeed or than it already goes, and speed over airSoftCap bleeds off
    public static Vector2 AirStep(Vector2 velocity, Vector2 wish, float wishSpeed, Tuning t, float dt)
    {
        if (wishSpeed > 0.0001f && wish.sqrMagnitude >= 0.000001f)
        {
            float cap = Mathf.Max(wishSpeed, velocity.magnitude);
            velocity = Vector2.MoveTowards(velocity, wish.normalized * cap, t.airAccel * dt);
        }

        float speed = velocity.magnitude;
        if (speed > t.airSoftCap && speed > 0f)
        {
            velocity *= Mathf.Max(t.airSoftCap, speed - t.airDrag * dt) / speed;
        }
        return velocity;
    }

    // After the body has moved: if it moved less than its velocity said (a wall), the velocity
    // becomes what it actually did, so pushing into a wall doesn't store speed. Never increases it.
    public static Vector2 LimitToActual(Vector2 velocity, Vector2 moved, float dt)
    {
        if (dt <= 0f) return velocity;
        Vector2 actual = moved / dt;
        return actual.sqrMagnitude < velocity.sqrMagnitude ? actual : velocity;
    }

    // How high a jump rises, as Player simulates it: gravity (negative) is added to the vertical speed
    // before each tick's move
    public static float JumpApex(float jumpSpeed, float gravity, float dt)
    {
        float height = 0f, best = 0f;
        float v = jumpSpeed;
        for (int i = 0; i < 10000; i++)
        {
            v += gravity * dt;
            if (v <= 0f) break;
            height += v * dt;
            best = height;
        }
        return best;
    }
}
