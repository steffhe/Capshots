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
        gun.clipSize = 10;
        gun.ammo = 30;
        gun.headshotMultiplier = 2f;
        gun.falloffStart = 20f;
        gun.falloffEnd = 40f;
        gun.minDamageMultiplier = 0.5f;
        gun.Initialize();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(gun);
    }

    [Test]
    public void Initialize_FillsClipAndReserve()
    {
        Assert.AreEqual(10, gun.GetClip());
        Assert.AreEqual(30, gun.GetStash());
    }

    [Test]
    public void FireBullet_UsesOneRound_AndFailsWhenEmpty()
    {
        for (int i = 0; i < 10; i++) Assert.IsTrue(gun.FireBullet());
        Assert.AreEqual(0, gun.GetClip());
        Assert.IsFalse(gun.FireBullet());
    }

    [Test]
    public void Reload_MovesRoundsFromReserveIntoClip()
    {
        for (int i = 0; i < 4; i++) gun.FireBullet();

        Assert.IsTrue(gun.CanReload());
        gun.Reload();

        Assert.AreEqual(10, gun.GetClip());
        Assert.AreEqual(26, gun.GetStash());
    }

    [Test]
    public void Reload_WithLittleReserve_OnlyPartlyFillsClip()
    {
        gun.ammo = 3;
        gun.Initialize();
        for (int i = 0; i < 10; i++) gun.FireBullet();

        gun.Reload();

        Assert.AreEqual(3, gun.GetClip());
        Assert.AreEqual(0, gun.GetStash());
        Assert.IsFalse(gun.CanReload());
    }

    [Test]
    public void CanReload_IsFalseWithFullClip()
    {
        Assert.IsFalse(gun.CanReload());
    }

    [Test]
    public void AddAmmo_IsCappedAtStartingAmount()
    {
        Assert.IsFalse(gun.NeedsAmmo());
        Assert.IsFalse(gun.AddAmmo(10), "a full reserve shouldn't take more ammo");

        gun.FireBullet();
        gun.Reload(); // reserve now 29
        Assert.IsTrue(gun.NeedsAmmo());
        Assert.IsTrue(gun.AddAmmo(10));
        Assert.AreEqual(30, gun.GetStash());
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
