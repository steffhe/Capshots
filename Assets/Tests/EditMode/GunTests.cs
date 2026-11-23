using NUnit.Framework;
using UnityEngine;

public class GunTests
{
    private Gun gun;

    [SetUp]
    public void SetUp()
    {
        gun = ScriptableObject.CreateInstance<Gun>();
        gun.damage = 100;
        gun.headshotMultiplier = 2f;
        gun.falloffStart = 20f;
        gun.falloffEnd = 40f;
        gun.minDamageMultiplier = 0.5f;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(gun);
    }

    [TestCase(0f, 100)]
    [TestCase(20f, 100)]  // falloff starts
    [TestCase(30f, 75)]   // halfway between start and end
    [TestCase(40f, 50)]   // falloff ends at the minimum multiplier
    [TestCase(500f, 50)]  // never below the minimum
    public void DamageAt_FallsOffWithDistance(float distance, int expected)
    {
        Assert.AreEqual(expected, gun.DamageAt(distance, false));
    }

    [Test]
    public void DamageAt_AppliesHeadshotMultiplier()
    {
        Assert.AreEqual(200, gun.DamageAt(10f, true));
        Assert.AreEqual(100, gun.DamageAt(40f, true));
    }

    [Test]
    public void DamageAt_WithoutFalloffRange_IsFullDamage()
    {
        gun.falloffStart = 50f;
        gun.falloffEnd = 50f;
        Assert.AreEqual(100, gun.DamageAt(1000f, false));
    }

    [Test]
    public void DamageAt_IsAlwaysAtLeastOne()
    {
        gun.damage = 1;
        gun.minDamageMultiplier = 0f;
        Assert.AreEqual(1, gun.DamageAt(1000f, false));
    }
}
