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

    [Test]
    public void DiskPoint_StaysInsideTheCircleAndReachesItsEdge()
    {
        float furthest = 0f;
        for (int i = 0; i <= 20; i++)
            for (int j = 0; j <= 20; j++)
            {
                UnityEngine.Vector2 p = ShotRules.DiskPoint(i / 20f, j / 20f);
                Assert.LessOrEqual(p.magnitude, 1f + 1e-5f);
                furthest = UnityEngine.Mathf.Max(furthest, p.magnitude);
            }
        Assert.AreEqual(1f, furthest, 1e-4f);
        Assert.AreEqual(0f, ShotRules.DiskPoint(0f, 0.3f).magnitude, 1e-5f);
    }

    [Test]
    public void PelletOffset_IsACentrePelletAndARing()
    {
        UnityEngine.Vector2 none = UnityEngine.Vector2.zero;
        Assert.AreEqual(0f, ShotRules.PelletOffset(0, 8, 1.2f, none).magnitude, 1e-5f);
        for (int p = 1; p < 8; p++)
            Assert.AreEqual(ShotRules.PelletRing, ShotRules.PelletOffset(p, 8, 1.2f, none).magnitude, 1e-4f, "pellet " + p);
        // Ring pellets are spread evenly round it
        float step = UnityEngine.Vector2.Angle(ShotRules.PelletOffset(1, 8, 0f, none), ShotRules.PelletOffset(2, 8, 0f, none));
        Assert.AreEqual(360f / 7f, step, 0.01f);
    }

    [Test]
    public void PelletOffset_StaysWithinTheSpread()
    {
        UnityEngine.Vector2 big = new UnityEngine.Vector2(5f, 5f);
        for (int p = 0; p < 8; p++)
            Assert.LessOrEqual(ShotRules.PelletOffset(p, 8, 0.4f, big).magnitude, 1f + 1e-4f, "pellet " + p);
    }

    [Test]
    public void PelletDirection_IsTheSameForTheSameShot()
    {
        // The host, the shooter and everyone drawing the shot from its message must agree
        UnityEngine.Quaternion view = UnityEngine.Quaternion.Euler(-12f, 200f, 0f);
        for (int p = 0; p < 8; p++)
            Assert.AreEqual(ShotRules.PelletDirection(view, 78f, 123456, p, 8), ShotRules.PelletDirection(view, 78f, 123456, p, 8));
        Assert.AreNotEqual(ShotRules.PelletDirection(view, 30f, 1, 0, 1), ShotRules.PelletDirection(view, 30f, 2, 0, 1), "different shots differ");
    }

    [Test]
    public void PelletDirection_StaysWithinTheSpread()
    {
        UnityEngine.Quaternion view = UnityEngine.Quaternion.Euler(20f, 45f, 0f);
        UnityEngine.Vector3 forward = view * UnityEngine.Vector3.forward;
        float limit = UnityEngine.Mathf.Atan(30f / 1000f) * UnityEngine.Mathf.Rad2Deg + 1e-3f;
        for (int shot = 0; shot < 200; shot++)
            Assert.LessOrEqual(UnityEngine.Vector3.Angle(forward, ShotRules.PelletDirection(view, 30f, shot * 7919, 0, 1)), limit);
        // No spread: dead ahead
        Assert.Less(UnityEngine.Vector3.Angle(forward, ShotRules.PelletDirection(view, 0f, 5, 0, 1)), 1e-3f);
    }

    [Test]
    public void Seeded_StaysInRange()
    {
        for (int seed = -1000; seed < 1000; seed += 7)
        {
            float v = ShotRules.Seeded(seed);
            Assert.GreaterOrEqual(v, -1f);
            Assert.LessOrEqual(v, 1f);
        }
    }
}
