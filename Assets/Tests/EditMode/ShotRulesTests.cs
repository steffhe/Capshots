using NUnit.Framework;

public class ShotRulesTests
{
    [Test]
    public void AimProgress_RisesOverTheAdsTime()
    {
        Assert.AreEqual(0f, ShotRules.AimProgress(0f, 0.25f), 1e-5f);
        Assert.AreEqual(0.5f, ShotRules.AimProgress(0.125f, 0.25f), 1e-5f);
        Assert.AreEqual(1f, ShotRules.AimProgress(0.25f, 0.25f), 1e-5f);
        Assert.AreEqual(1f, ShotRules.AimProgress(3f, 0.25f), 1e-5f);
    }

    [Test]
    public void AimProgress_IsInstantWithNoAdsTime()
    {
        Assert.AreEqual(1f, ShotRules.AimProgress(0f, 0f), 1e-5f);
    }

    [Test]
    public void AimedBloom_GoesFromHipToAimed()
    {
        Assert.AreEqual(40f, ShotRules.AimedBloom(40f, 10f, 0f), 1e-4f);
        Assert.AreEqual(22f, ShotRules.AimedBloom(40f, 10f, 0.5f), 1e-4f);
        Assert.AreEqual(4f, ShotRules.AimedBloom(40f, 10f, 1f), 1e-4f);
        // A divisor under 1 never makes aiming worse than the hip
        Assert.AreEqual(30f, ShotRules.AimedBloom(30f, 0.5f, 1f), 1e-4f);
    }

    [Test]
    public void IsCapshot_ASingleBulletToTheHead()
    {
        Assert.IsTrue(ShotRules.IsCapshot(1600, 1600));
        Assert.IsFalse(ShotRules.IsCapshot(0, 800));
    }

    [Test]
    public void IsCapshot_AShotgunBlastNeedsHalfItsDamageFromTheHead()
    {
        // 1 of 8 pellets on the head (140 x 1.5 = 210) with 7 on the body (980): not a cap shot
        Assert.IsFalse(ShotRules.IsCapshot(210, 1190));
        // 3 on the head (630) and 3 on the body (420): a cap shot
        Assert.IsTrue(ShotRules.IsCapshot(630, 1050));
        // Exactly half counts
        Assert.IsTrue(ShotRules.IsCapshot(500, 1000));
    }

    [Test]
    public void IsCapshot_NoDamageIsNoCapshot()
    {
        Assert.IsFalse(ShotRules.IsCapshot(0, 0));
    }
}
