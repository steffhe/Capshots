using NUnit.Framework;

public class KillStreaksTests
{
    [TestCase(1, null)]
    [TestCase(2, "DOUBLE KILL")]
    [TestCase(3, "TRIPLE KILL")]
    [TestCase(4, "QUAD KILL")]
    [TestCase(7, "MULTI KILL")]
    public void MultiKillName(int kills, string expected)
    {
        Assert.AreEqual(expected, KillStreaks.MultiKillName(kills));
    }

    [TestCase(4, null)]
    [TestCase(5, "KILLING SPREE")]
    [TestCase(6, null)]
    [TestCase(10, "RAMPAGE")]
    [TestCase(15, "UNSTOPPABLE")]
    [TestCase(25, "LEGENDARY")]
    public void StreakName_OnlyAtMilestones(int streak, string expected)
    {
        Assert.AreEqual(expected, KillStreaks.StreakName(streak));
    }

    [Test]
    public void BonusXp_GrowsWithTheMultiKillUpToACap_PlusMilestones()
    {
        Assert.AreEqual(0, KillStreaks.BonusXp(1, 1));
        Assert.AreEqual(50, KillStreaks.BonusXp(2, 2));
        Assert.AreEqual(100, KillStreaks.BonusXp(3, 3));
        Assert.AreEqual(200, KillStreaks.BonusXp(9, 9));
        Assert.AreEqual(100 + 150, KillStreaks.BonusXp(4, 5));
        Assert.AreEqual(100, KillStreaks.BonusXp(1, 10));
    }
}

public class MatchSummaryTests
{
    [Test]
    public void Accuracy_CountsShotsThatHit_NeverOverAHundred()
    {
        var s = new MatchSummary();
        Assert.AreEqual(0, s.AccuracyPercent);
        for (int i = 0; i < 4; i++) s.Shot();
        s.Hit();
        Assert.AreEqual(25, s.AccuracyPercent);
        for (int i = 0; i < 10; i++) s.Hit();
        Assert.AreEqual(100, s.AccuracyPercent);
    }

    [Test]
    public void Kills_GiveCapshotRateFavouriteGunAndBestStreak()
    {
        var s = new MatchSummary();
        Assert.IsNull(s.FavouriteGun);
        s.Kill("SNIPER", true);
        s.Kill("SHOTGUN", false);
        s.Kill("SHOTGUN", false);
        s.Died();
        s.Kill("SNIPER", true);
        Assert.AreEqual(4, s.kills);
        Assert.AreEqual(50, s.CapshotPercent);
        Assert.AreEqual(3, s.bestStreak);
        // A tie goes to the first to get there
        Assert.AreEqual("SNIPER", s.FavouriteGun);
        s.Reset();
        Assert.AreEqual(0, s.kills);
        Assert.IsNull(s.FavouriteGun);
    }
}
