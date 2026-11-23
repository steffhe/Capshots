using NUnit.Framework;

public class GunEnergyTests
{
    [Test]
    public void CanFire_NeedsTheFullCostOfAShot()
    {
        Assert.IsTrue(GunEnergy.CanFire(20f, 20f));
        Assert.IsFalse(GunEnergy.CanFire(19f, 20f));
        Assert.IsTrue(GunEnergy.CanFire(0f, 0f));
    }

    [TestCase(100f, 10f, 10)]
    [TestCase(100f, 3.4f, 29)]
    [TestCase(100f, 16f, 6)]
    [TestCase(19.9f, 20f, 0)]
    [TestCase(0f, 10f, 0)]
    public void ShotsLeft_CountsWholeShots(float energy, float cost, int expected)
    {
        Assert.AreEqual(expected, GunEnergy.ShotsLeft(energy, cost));
    }

    [Test]
    public void ShotsLeft_ToleratesRounding()
    {
        float cost = 100f / 3f;
        Assert.AreEqual(3, GunEnergy.ShotsLeft(cost * 3f, cost));
    }

    [Test]
    public void Regenerate_AddsRateOverTime_UpToMax()
    {
        Assert.AreEqual(40f, GunEnergy.Regenerate(10f, 100f, 30f, 1f), 1e-4f);
        Assert.AreEqual(100f, GunEnergy.Regenerate(95f, 100f, 30f, 1f), 1e-4f);
        Assert.AreEqual(10f, GunEnergy.Regenerate(10f, 100f, -5f, 1f), 1e-4f);
    }
}
