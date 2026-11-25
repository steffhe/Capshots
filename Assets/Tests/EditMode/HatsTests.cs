using NUnit.Framework;

public class HatsTests
{
    [Test]
    public void Cap_IsUnlockedFromTheStart()
    {
        Assert.AreEqual(0, Hats.UnlockLevel(Hats.Cap));
        Assert.IsTrue(Hats.IsUnlocked(Hats.Cap, 0));
    }

    [Test]
    public void UnlockLevels_RiseThroughTheList()
    {
        for (int i = 1; i < Hats.Count; i++)
        {
            Assert.Greater(Hats.UnlockLevel(i), Hats.UnlockLevel(i - 1), Hats.Name(i));
        }
    }

    [TestCase(Hats.TopHat, 15, false)]
    [TestCase(Hats.TopHat, 16, true)]
    [TestCase(Hats.TopHat, 40, true)]
    [TestCase(-1, 100, false)]
    [TestCase(99, 100, false)]
    public void IsUnlocked_OnceTheLevelIsReached(int hat, int level, bool expected)
    {
        Assert.AreEqual(expected, Hats.IsUnlocked(hat, level));
    }

    [TestCase(Hats.Beanie, 4, Hats.Beanie)]
    [TestCase(Hats.Beanie, 3, Hats.Cap)]
    [TestCase(99, 50, Hats.Cap)]
    public void Wearable_FallsBackToTheCap(int hat, int level, int expected)
    {
        Assert.AreEqual(expected, Hats.Wearable(hat, level));
    }

    [Test]
    public void UnlockedBetween_ListsOnlyTheNewHats()
    {
        // Levels 3 to 9 pass the beanie (4), chef's hat (5), bucket hat (6), antenna (7) and hard hat (9)
        CollectionAssert.AreEqual(new[] { Hats.Beanie, Hats.ChefsHat, Hats.BucketHat, Hats.Antenna, Hats.HardHat }, Hats.UnlockedBetween(3, 9));
        CollectionAssert.IsEmpty(Hats.UnlockedBetween(7, 8));
        CollectionAssert.IsEmpty(Hats.UnlockedBetween(5, 5));
    }

    [Test]
    public void NextUnlock_IsTheFirstLockedHat()
    {
        Assert.AreEqual(Hats.CatEarHeadset, Hats.NextUnlock(0));
        Assert.AreEqual(Hats.CowboyHat, Hats.NextUnlock(12));
        Assert.AreEqual(-1, Hats.NextUnlock(Hats.UnlockLevel(Hats.Count - 1)));
    }

    [Test]
    public void Names_AreThereForEveryHat()
    {
        for (int i = 0; i < Hats.Count; i++) Assert.IsFalse(string.IsNullOrEmpty(Hats.Name(i)));
    }
}
