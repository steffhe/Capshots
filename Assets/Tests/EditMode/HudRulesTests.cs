using NUnit.Framework;

public class HudRulesTests
{
    [TestCase(1920f, 1080f, 0f)]      // 16:9 uses the full width
    [TestCase(1728f, 1080f, 0f)]      // 16:10
    [TestCase(2520f, 1080f, 180f)]    // 21:9: 2160 wide, centred
    [TestCase(3840f, 1080f, 840f)]    // 32:9
    public void SafeInset_OnlyNarrowsVeryWideScreens(float width, float height, float expected)
    {
        Assert.AreEqual(expected, HudRules.SafeInset(width, height), 0.01f);
    }

    [Test]
    public void SafeInset_IgnoresEmptyCanvas()
    {
        Assert.AreEqual(0f, HudRules.SafeInset(0f, 0f));
        Assert.AreEqual(0f, HudRules.SafeInset(1920f, 0f));
    }

    [TestCase(100f, 20f, 5)]       // five shots: five segments
    [TestCase(100f, 100f / 3f, 3)] // rounding still gives three
    [TestCase(100f, 3.4f, 0)]      // 29 shots: a smooth bar
    [TestCase(100f, 0f, 0)]        // free shots: a smooth bar
    public void EnergySegments_OnePerShotUpToTheLimit(float max, float cost, int expected)
    {
        Assert.AreEqual(expected, HudRules.EnergySegments(max, cost));
    }

    [Test]
    public void SegmentFill_ShowsTheNextShotRefilling()
    {
        // 50 energy at 20 a shot: two full segments and half of the third
        Assert.AreEqual(1f, HudRules.SegmentFill(50f, 20f, 0), 0.001f);
        Assert.AreEqual(1f, HudRules.SegmentFill(50f, 20f, 1), 0.001f);
        Assert.AreEqual(0.5f, HudRules.SegmentFill(50f, 20f, 2), 0.001f);
        Assert.AreEqual(0f, HudRules.SegmentFill(50f, 20f, 3), 0.001f);
    }

    [TestCase(1f, HudRules.HealthState.Healthy)]
    [TestCase(0.51f, HudRules.HealthState.Healthy)]
    [TestCase(0.5f, HudRules.HealthState.Low)]
    [TestCase(0.26f, HudRules.HealthState.Low)]
    [TestCase(0.25f, HudRules.HealthState.Critical)]
    [TestCase(0f, HudRules.HealthState.Critical)]
    public void StateFor_UsesHalfAndQuarter(float ratio, HudRules.HealthState expected)
    {
        Assert.AreEqual(expected, HudRules.StateFor(ratio));
    }

    [Test]
    public void OpponentLabel_OnlyClaimsALeadYouHave()
    {
        Assert.AreEqual("2ND", HudRules.OpponentLabel(5, 3));
        Assert.AreEqual("LEADER", HudRules.OpponentLabel(3, 3));
        Assert.AreEqual("LEADER", HudRules.OpponentLabel(1, 4));
        Assert.AreEqual("LEADER", HudRules.OpponentLabel(0, 0));
    }

    [Test]
    public void GunGameForm_CountsFromOneAndStopsAtTheTop()
    {
        Assert.AreEqual(1, HudRules.GunGameForm(0));
        Assert.AreEqual(1, HudRules.GunGameForm(GunGameRules.KillsPerStep - 1));
        Assert.AreEqual(2, HudRules.GunGameForm(GunGameRules.KillsPerStep));
        Assert.AreEqual(GunGameRules.Ladder.Length, HudRules.GunGameForm(GunGameRules.KillsToWin + 5));
    }

    [TestCase(4.2f, 5)]
    [TestCase(0.01f, 1)]
    [TestCase(0f, 0)]
    [TestCase(-1f, 0)]
    public void CountdownSeconds_RoundsUp(float remaining, int expected)
    {
        Assert.AreEqual(expected, HudRules.CountdownSeconds(remaining));
    }
}
