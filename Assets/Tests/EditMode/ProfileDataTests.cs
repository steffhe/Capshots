using NUnit.Framework;

public class ProfileDataTests
{
    [Test]
    public void XpToNextLevel_GrowsEachLevel()
    {
        Assert.AreEqual(500, ProfileData.XpToNextLevel(0));
        Assert.AreEqual(750, ProfileData.XpToNextLevel(1));
        Assert.AreEqual(1000, ProfileData.XpToNextLevel(2));
    }

    [Test]
    public void AddXp_BelowThreshold_DoesNotLevelUp()
    {
        ProfileData profile = new ProfileData();

        Assert.AreEqual(0, profile.AddXp(499));
        Assert.AreEqual(0, profile.level);
        Assert.AreEqual(499, profile.xp);
    }

    [Test]
    public void AddXp_ReachingThreshold_LevelsUpAndCarriesOverTheRest()
    {
        ProfileData profile = new ProfileData();

        Assert.AreEqual(1, profile.AddXp(600));
        Assert.AreEqual(1, profile.level);
        Assert.AreEqual(100, profile.xp);
    }

    [Test]
    public void AddXp_CanGainSeveralLevelsAtOnce()
    {
        ProfileData profile = new ProfileData();

        // 500 for level 1, then 750 for level 2
        Assert.AreEqual(2, profile.AddXp(1250));
        Assert.AreEqual(2, profile.level);
        Assert.AreEqual(0, profile.xp);
    }

    [Test]
    public void NewProfile_HasDefaultSensitivity()
    {
        ProfileData profile = new ProfileData();

        Assert.AreEqual(150f, profile.xsens);
        Assert.AreEqual(150f, profile.ysens);
    }
}
