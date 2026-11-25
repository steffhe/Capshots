using NUnit.Framework;

public class AimAssistTests
{
    [Test]
    public void RightOnTarget_SlowsTheMost()
    {
        Assert.AreEqual(AimAssist.StrongestSlowdown, AimAssist.Slowdown(0f, 15f), 0.0001f);
    }

    [Test]
    public void OutsideTheConeOrOutOfRange_NoChange()
    {
        Assert.AreEqual(1f, AimAssist.Slowdown(AimAssist.Cone(15f) + 0.1f, 15f));
        Assert.AreEqual(1f, AimAssist.Slowdown(0f, AimAssist.Range + 1f));
    }

    [Test]
    public void Slowdown_EasesOffTowardsTheConesEdge()
    {
        float cone = AimAssist.Cone(20f);
        float near = AimAssist.Slowdown(cone * 0.2f, 20f);
        float far = AimAssist.Slowdown(cone * 0.8f, 20f);
        Assert.Less(near, far);
        Assert.Less(far, 1f);
    }

    [Test]
    public void Cone_IsNarrowerFurtherAway_WithinLimits()
    {
        Assert.Greater(AimAssist.Cone(5f), AimAssist.Cone(40f));
        Assert.LessOrEqual(AimAssist.Cone(0.5f), 8f);
        Assert.GreaterOrEqual(AimAssist.Cone(500f), 1.5f);
    }
}
