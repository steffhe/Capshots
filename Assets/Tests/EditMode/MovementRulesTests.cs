using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

// Core/MovementRules: how a player's speed changes each tick, and how high the prefab's jump goes
public class MovementRulesTests
{
    private const float Dt = 1f / 40f; // the 40 Hz tick (NetworkConfigTests)
    private const float Walk = 10f, Sprint = 15f;

    private static MovementRules.Tuning Tuning => new MovementRules.Tuning
    {
        accel = 140f, brake = 160f, overspeedBrake = 40f, sprintAccel = 20f,
        airAccel = 25f, airDrag = 8f, airSoftCap = Sprint,
    };

    private static int TicksUntil(System.Func<Vector2, Vector2> step, Vector2 start, System.Func<Vector2, bool> done)
    {
        Vector2 v = start;
        for (int i = 1; i <= 400; i++)
        {
            v = step(v);
            if (done(v)) return i;
        }
        return int.MaxValue;
    }

    [Test]
    public void Ground_ReachesWalkingSpeedInAFewTicks()
    {
        int ticks = TicksUntil(v => MovementRules.GroundStep(v, Vector2.up, Walk, Walk, Tuning, Dt), Vector2.zero, v => v.y >= Walk - 1e-4f);
        Assert.That(ticks, Is.InRange(2, 4));
    }

    [Test]
    public void Ground_SprintSpoolsUpMoreSlowlyThanWalking()
    {
        int ticks = TicksUntil(v => MovementRules.GroundStep(v, Vector2.up, Sprint, Walk, Tuning, Dt), new Vector2(0f, Walk), v => v.y >= Sprint - 1e-4f);
        Assert.That(ticks, Is.InRange(8, 12));
    }

    [Test]
    public void Ground_StopsQuicklyWithNoInput()
    {
        int ticks = TicksUntil(v => MovementRules.GroundStep(v, Vector2.zero, 0f, Walk, Tuning, Dt), new Vector2(Walk, 0f), v => v == Vector2.zero);
        Assert.That(ticks, Is.InRange(2, 4));
    }

    [Test]
    public void Ground_CounterStrafeStopsSoonerThanLettingGo()
    {
        Vector2 start = new Vector2(Walk, 0f);
        int released = TicksUntil(v => MovementRules.GroundStep(v, Vector2.zero, 0f, Walk, Tuning, Dt), start, v => v.x <= 0f);
        int countered = TicksUntil(v => MovementRules.GroundStep(v, Vector2.left, Walk, Walk, Tuning, Dt), start, v => v.x <= 0f);
        Assert.Less(countered, released);
    }

    [Test]
    public void Ground_NeverGoesFasterThanAsked()
    {
        Vector2 v = Vector2.zero;
        for (int i = 0; i < 100; i++)
        {
            Vector2 wish = i % 7 < 4 ? new Vector2(1f, 1f) : Vector2.left;
            v = MovementRules.GroundStep(v, wish, Sprint, Walk, Tuning, Dt);
            Assert.LessOrEqual(v.magnitude, Sprint + 1e-3f);
        }
    }

    [Test]
    public void Ground_SlowsGentlyToALowerSpeed()
    {
        Vector2 v = MovementRules.GroundStep(new Vector2(0f, Sprint), Vector2.up, Walk, Walk, Tuning, Dt);
        Assert.AreEqual(Sprint - 40f * Dt, v.y, 1e-4f);
    }

    [Test]
    public void Air_KeepsMomentumWithNoInput()
    {
        Vector2 v = MovementRules.AirStep(new Vector2(0f, Sprint), Vector2.zero, 0f, Tuning, Dt);
        Assert.AreEqual(Sprint, v.magnitude, 1e-4f);
    }

    [Test]
    public void Air_SteeringNeverAddsSpeed()
    {
        Vector2 v = new Vector2(0f, Sprint);
        for (int i = 0; i < 60; i++)
        {
            v = MovementRules.AirStep(v, new Vector2(1f, 0.3f), Walk, Tuning, Dt);
            Assert.LessOrEqual(v.magnitude, Sprint + 1e-3f);
        }
    }

    [Test]
    public void Air_ExtraSpeedAboveTheCapBleedsOff()
    {
        Vector2 v = MovementRules.AirStep(new Vector2(0f, 25f), Vector2.zero, 0f, Tuning, Dt);
        Assert.AreEqual(25f - 8f * Dt, v.magnitude, 1e-4f);
    }

    [Test]
    public void LimitToActual_OnlyEverSlowsDown()
    {
        Vector2 velocity = new Vector2(10f, 0f);
        Assert.AreEqual(new Vector2(2f, 0f), MovementRules.LimitToActual(velocity, new Vector2(2f * Dt, 0f), Dt));
        Assert.AreEqual(velocity, MovementRules.LimitToActual(velocity, new Vector2(20f * Dt, 0f), Dt));
    }

    [Test]
    public void Slide_SlowsToCrouchingSpeedButNoLower()
    {
        Vector2 v = MovementRules.SlideStep(Vector2.up, 18f, Vector2.up, 90f, 17f, 5f, Dt);
        Assert.AreEqual(18f - 17f * Dt, v.magnitude, 1e-4f);
        Vector2 slow = MovementRules.SlideStep(Vector2.up, 5.1f, Vector2.up, 90f, 17f, 5f, Dt);
        Assert.AreEqual(5f, slow.magnitude, 1e-4f);
    }

    [Test]
    public void Slide_TurnsTowardsTheInputAtMostItsSteering()
    {
        // Wanting to go right while sliding forward: one tick turns 90 degrees/s x 1/40 s
        Vector2 v = MovementRules.SlideStep(Vector2.up, 10f, Vector2.right, 90f, 0f, 0f, Dt);
        Assert.AreEqual(90f * Dt, Vector2.Angle(Vector2.up, v), 1e-3f);
        Assert.Greater(v.x, 0f);
        Assert.AreEqual(10f, v.magnitude, 1e-4f);
    }

    [Test]
    public void Slide_WithTheseRatesIsSlowerThanSprintingOnAverage()
    {
        // A slide from 18 m/s for 0.75 s (30 ticks) at 17 m/s per second, down to 5
        float distance = 0f, speed = 18f;
        for (int i = 0; i < 30; i++)
        {
            speed = MovementRules.SlideStep(Vector2.up, speed, Vector2.up, 90f, 17f, 5f, Dt).magnitude;
            distance += speed * Dt;
        }
        Assert.Less(distance / 0.75f, Sprint);
    }

    [Test]
    public void WithinTicks_CoversTheWindowAndNothingElse()
    {
        Assert.IsTrue(MovementRules.WithinTicks(100, 100, 4));
        Assert.IsTrue(MovementRules.WithinTicks(104, 100, 4));
        Assert.IsFalse(MovementRules.WithinTicks(105, 100, 4));
        Assert.IsFalse(MovementRules.WithinTicks(99, 100, 4));
        Assert.IsFalse(MovementRules.WithinTicks(100, 0, 4));
    }

    // The Player prefab's jump, with the project's gravity: high enough to get onto the Prototype map's
    // 2.5 m decks (DECK), not onto its 3 m crates, and well short of its 4.5 m lane walls (LANE_WALL).
    // The capsule also steps up 0.3 m (STEP) on top of the jump.
    [Test]
    public void PrefabJump_ReachesTheDecksButNotTheCrates()
    {
        string prefab = File.ReadAllText(Path.Combine(Application.dataPath, "Resources/Player.prefab"));
        float jumpForce = float.Parse(Regex.Match(prefab, @"\n  jumpForce: ([\d.]+)").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        string dynamics = File.ReadAllText(Path.Combine(Application.dataPath, "../ProjectSettings/DynamicsManager.asset"));
        float gravity = float.Parse(Regex.Match(dynamics, @"m_Gravity: \{x: [-\d.]+, y: ([-\d.]+)").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

        // Player.LegacyStep turns the old jump force into m/s
        float apex = MovementRules.JumpApex(jumpForce * 0.02f, gravity, Dt);
        const float step = 0.3f;
        Assert.GreaterOrEqual(apex + step, 2.5f, "can't jump onto the decks");
        Assert.Less(apex + step, 3f, "can jump onto the crates");
        Assert.Less(apex + step, 4.5f - 0.5f, "too close to clearing the lane walls");
    }
}
