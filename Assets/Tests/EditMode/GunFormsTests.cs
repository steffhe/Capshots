using NUnit.Framework;
using UnityEngine;

public class GunFormsTests
{
    [TestCase(0, 1, 4, 1)]
    [TestCase(2, 1, 4, 3)]
    [TestCase(3, 1, 4, 0)]   // next from the last form wraps to the first (the touch SWAP button)
    [TestCase(0, -1, 4, 3)]  // previous from the first wraps to the last
    [TestCase(2, -1, 4, 1)]
    [TestCase(0, 1, 1, 0)]
    [TestCase(0, 1, 0, 0)]
    public void Step_WrapsRoundBothEnds(int current, int by, int count, int expected)
    {
        Assert.AreEqual(expected, GunForms.Step(current, by, count));
    }

    [Test]
    public void Step_NextReachesEveryFormAndComesBack()
    {
        int slot = 0;
        bool[] seen = new bool[4];
        for (int i = 0; i < 4; i++) { seen[slot] = true; slot = GunForms.Step(slot, 1, 4); }
        Assert.AreEqual(0, slot);
        CollectionAssert.DoesNotContain(seen, false);
    }

    private static Gun Named(string name)
    {
        Gun gun = ScriptableObject.CreateInstance<Gun>();
        gun.gunName = name;
        return gun;
    }

    [Test]
    public void IndexOf_FindsTheFormByName()
    {
        Gun[] loadout = { Named("Shotgun"), Named("Machine Gun"), Named("Pistol"), Named("Sniper Rifle") };
        Assert.AreEqual(2, GunForms.IndexOf(loadout, "Pistol", 1));
        Assert.AreEqual(0, GunForms.IndexOf(loadout, "Shotgun", 1));
    }

    [Test]
    public void IndexOf_FallsBackWhenThereIsNoSuchForm()
    {
        Gun[] loadout = { Named("Shotgun"), Named("Machine Gun") };
        Assert.AreEqual(1, GunForms.IndexOf(loadout, "", 1));
        Assert.AreEqual(1, GunForms.IndexOf(loadout, null, 1));
        Assert.AreEqual(1, GunForms.IndexOf(loadout, "Rocket Launcher", 1));
        Assert.AreEqual(1, GunForms.IndexOf(new Gun[] { null }, "Shotgun", 1));
        Assert.AreEqual(1, GunForms.IndexOf(null, "Shotgun", 1));
    }
}
