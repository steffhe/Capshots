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

    [Test]
    public void RecoilPattern_StartsStraightUpAndStaysInRange()
    {
        Assert.AreEqual(0f, ShotRules.RecoilPattern(0), 1e-5f);
        Assert.Less(UnityEngine.Mathf.Abs(ShotRules.RecoilPattern(1)), 0.3f);
        for (int shot = 0; shot < 60; shot++)
        {
            float side = ShotRules.RecoilPattern(shot);
            Assert.LessOrEqual(UnityEngine.Mathf.Abs(side), 1f, "shot " + shot);
        }
    }

    [Test]
    public void RecoilPattern_IsTheSameEveryBurstAndSwaysBothWays()
    {
        bool left = false, right = false;
        for (int shot = 0; shot < 30; shot++)
        {
            Assert.AreEqual(ShotRules.RecoilPattern(shot), ShotRules.RecoilPattern(shot));
            if (ShotRules.RecoilPattern(shot) > 0.5f) right = true;
            if (ShotRules.RecoilPattern(shot) < -0.5f) left = true;
        }
        Assert.IsTrue(left && right, "a full machine gun cell's burst should sway right and left");
    }

    [Test]
    public void HitWeightByte_IsTheShareOfHealth()
    {
        Assert.AreEqual(0, ShotRules.HitWeightByte(0, 1000));
        Assert.AreEqual(51, ShotRules.HitWeightByte(200, 1000));
        Assert.AreEqual(204, ShotRules.HitWeightByte(800, 1000));
        Assert.AreEqual(255, ShotRules.HitWeightByte(1600, 1000));
        // Even a scratch shows
        Assert.AreEqual(1, ShotRules.HitWeightByte(1, 1000));
    }
}
