using NUnit.Framework;

public class GunGameRulesTests
{
    [Test]
    public void KillsToWin_IsTwoPerForm()
    {
        Assert.AreEqual(GunGameRules.Ladder.Length * GunGameRules.KillsPerStep, GunGameRules.KillsToWin);
        Assert.AreEqual(8, GunGameRules.KillsToWin);
    }

    [TestCase(0, 0)]
    [TestCase(1, 0)]
    [TestCase(2, 1)]
    [TestCase(5, 2)]
    [TestCase(7, 3)]
    [TestCase(8, 3)]
    [TestCase(20, 3)]
    [TestCase(-3, 0)]
    public void StepFor_ClimbsEveryTwoKills_AndStopsAtTheLastForm(int kills, int step)
    {
        Assert.AreEqual(step, GunGameRules.StepFor(kills));
    }

    [TestCase(0, 2)]
    [TestCase(1, 1)]
    [TestCase(6, 2)]
    [TestCase(7, 1)]
    [TestCase(8, 0)]
    public void KillsLeftOnStep(int kills, int left)
    {
        Assert.AreEqual(left, GunGameRules.KillsLeftOnStep(kills));
    }

    [Test]
    public void OnlyGunKillsCount()
    {
        Assert.IsTrue(GunGameRules.Counts("Shotgun", "PLASMA"));
        Assert.IsFalse(GunGameRules.Counts("PLASMA", "PLASMA"));
        Assert.IsFalse(GunGameRules.Counts("Fell", "PLASMA"));
        Assert.IsFalse(GunGameRules.Counts(null, "PLASMA"));
    }

    [Test]
    public void LadderStartsWithTheMachineGunAndEndsWithTheSniper()
    {
        Assert.AreEqual("Machine Gun", GunGameRules.Ladder[0]);
        Assert.AreEqual("Sniper Rifle", GunGameRules.Ladder[GunGameRules.Ladder.Length - 1]);
    }
}
