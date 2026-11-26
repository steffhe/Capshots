using NUnit.Framework;
using UnityEngine;

public class PlasmaGrenadeRulesTests
{
    [Test]
    public void BlastDamage_KillsInTheMiddleAndFadesToNothing()
    {
        Assert.AreEqual(PlasmaGrenadeRules.CentreDamage, PlasmaGrenadeRules.BlastDamage(0f));
        Assert.AreEqual(PlasmaGrenadeRules.CentreDamage, PlasmaGrenadeRules.BlastDamage(PlasmaGrenadeRules.InnerRadius));
        Assert.AreEqual(0, PlasmaGrenadeRules.BlastDamage(PlasmaGrenadeRules.BlastRadius));
        Assert.AreEqual(0, PlasmaGrenadeRules.BlastDamage(50f));

        int near = PlasmaGrenadeRules.BlastDamage(2f);
        int far = PlasmaGrenadeRules.BlastDamage(4f);
        Assert.Less(far, near);
        Assert.GreaterOrEqual(far, PlasmaGrenadeRules.EdgeDamage);
        Assert.Less(near, PlasmaGrenadeRules.CentreDamage);
    }

    [Test]
    public void StuckDamage_AlwaysKills()
    {
        Assert.Greater(PlasmaGrenadeRules.StuckDamage, PlasmaGrenadeRules.CentreDamage * 10);
    }

    [Test]
    public void FuseAfterSticking_NeverLongerThanTheStuckFuse()
    {
        Assert.AreEqual(PlasmaGrenadeRules.StuckFuse, PlasmaGrenadeRules.FuseAfterSticking(PlasmaGrenadeRules.FreeFuse), 1e-4f);
        // Cooked almost all the way: it keeps the little it had left
        Assert.AreEqual(0.4f, PlasmaGrenadeRules.FuseAfterSticking(0.4f), 1e-4f);
        Assert.AreEqual(0f, PlasmaGrenadeRules.FuseAfterSticking(-1f), 1e-4f);
    }

    [Test]
    public void FuseProgress_RunsFromArmedToGoingOff()
    {
        Assert.AreEqual(0f, PlasmaGrenadeRules.FuseProgress(PlasmaGrenadeRules.FreeFuse), 1e-4f);
        Assert.AreEqual(1f, PlasmaGrenadeRules.FuseProgress(0f), 1e-4f);
        Assert.AreEqual(0.5f, PlasmaGrenadeRules.FuseProgress(PlasmaGrenadeRules.FreeFuse / 2f), 1e-4f);
    }

    [Test]
    public void RechargeProgress_RunsFromThrownToReady()
    {
        Assert.AreEqual(0f, PlasmaGrenadeRules.RechargeProgress(PlasmaGrenadeRules.RechargeTime), 1e-4f);
        Assert.AreEqual(1f, PlasmaGrenadeRules.RechargeProgress(0f), 1e-4f);
        Assert.AreEqual(0.5f, PlasmaGrenadeRules.RechargeProgress(PlasmaGrenadeRules.RechargeTime / 2f), 1e-4f);
        Assert.AreEqual(1f, PlasmaGrenadeRules.RechargeProgress(-2f), 1e-4f);
    }

    [Test]
    public void Bounce_HardHitBouncesBack()
    {
        Vector3 after = PlasmaGrenadeRules.Bounce(new Vector3(0f, -10f, 0f), Vector3.up);
        Assert.AreEqual(10f * PlasmaGrenadeRules.Restitution, after.y, 1e-4f);
    }

    [Test]
    public void Bounce_SoftHitSlidesAndSlows()
    {
        Vector3 after = PlasmaGrenadeRules.Bounce(new Vector3(4f, -0.5f, 0f), Vector3.up);
        Assert.AreEqual(0f, after.y, 1e-4f);
        Assert.AreEqual(4f * PlasmaGrenadeRules.Friction, after.x, 1e-4f);
    }

    [Test]
    public void Bounce_MovingAwayIsLeftAlone()
    {
        Vector3 v = new Vector3(1f, 3f, 0f);
        Assert.AreEqual(v, PlasmaGrenadeRules.Bounce(v, Vector3.up));
    }

    [Test]
    public void ThrowVelocity_GoesForwardAndUp()
    {
        Vector3 v = PlasmaGrenadeRules.ThrowVelocity(Vector3.forward * 5f, Vector3.zero);
        Assert.AreEqual(PlasmaGrenadeRules.ThrowSpeed, v.z, 1e-4f);
        Assert.AreEqual(PlasmaGrenadeRules.ThrowLift, v.y, 1e-4f);

        Vector3 running = PlasmaGrenadeRules.ThrowVelocity(Vector3.forward, new Vector3(6f, 0f, 0f));
        Assert.AreEqual(6f * PlasmaGrenadeRules.InheritVelocity, running.x, 1e-4f);
    }

    [Test]
    public void SegmentDistance_CrossingLines()
    {
        float d = PlasmaGrenadeRules.SegmentDistance(
            new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f),
            new Vector3(0f, -1f, 2f), new Vector3(0f, 1f, 2f), out Vector3 c1, out Vector3 c2);
        Assert.AreEqual(2f, d, 1e-4f);
        Assert.AreEqual(Vector3.zero, c1);
        Assert.AreEqual(new Vector3(0f, 0f, 2f), c2);
    }

    [Test]
    public void SegmentDistance_ParallelAndPoints()
    {
        float parallel = PlasmaGrenadeRules.SegmentDistance(Vector3.zero, Vector3.right, Vector3.up, Vector3.up + Vector3.right, out _, out _);
        Assert.AreEqual(1f, parallel, 1e-4f);

        float points = PlasmaGrenadeRules.SegmentDistance(Vector3.zero, Vector3.zero, Vector3.forward * 3f, Vector3.forward * 3f, out _, out _);
        Assert.AreEqual(3f, points, 1e-4f);
    }

    [Test]
    public void TouchesCapsule_StickOnTheSurfaceFacingTheThrow()
    {
        // A body standing at the origin, 2 m tall, 0.5 m wide; the grenade flies straight at it along -z
        Vector3 bottom = new Vector3(0f, 0.5f, 0f), top = new Vector3(0f, 1.5f, 0f);
        bool hit = PlasmaGrenadeRules.TouchesCapsule(new Vector3(0f, 1f, 3f), new Vector3(0f, 1f, 0.2f), bottom, top, 0.5f, out Vector3 point);
        Assert.IsTrue(hit);
        Assert.AreEqual(0.5f, point.z, 1e-3f);
        Assert.AreEqual(1f, point.y, 1e-3f);
    }

    [Test]
    public void TouchesCapsule_MissesWhenWide()
    {
        Vector3 bottom = new Vector3(0f, 0.5f, 0f), top = new Vector3(0f, 1.5f, 0f);
        bool hit = PlasmaGrenadeRules.TouchesCapsule(new Vector3(2f, 1f, 3f), new Vector3(2f, 1f, -3f), bottom, top, 0.5f, out _);
        Assert.IsFalse(hit);
    }

    [Test]
    public void TouchesCapsule_StraightThroughTheMiddleStillLandsOnTheSurface()
    {
        Vector3 bottom = new Vector3(0f, 0.5f, 0f), top = new Vector3(0f, 1.5f, 0f);
        bool hit = PlasmaGrenadeRules.TouchesCapsule(new Vector3(0f, 1f, 1f), new Vector3(0f, 1f, -1f), bottom, top, 0.5f, out Vector3 point);
        Assert.IsTrue(hit);
        Assert.AreEqual(0.5f, new Vector2(point.x, point.z).magnitude, 1e-3f);
    }
}
