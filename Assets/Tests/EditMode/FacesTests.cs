using NUnit.Framework;

public class FacesTests
{
    [Test]
    public void Classic_IsUnlockedFromTheStart()
    {
        Assert.AreEqual(0, Faces.UnlockLevel(Faces.Classic));
        Assert.IsTrue(Faces.IsUnlocked(Faces.Classic, 0));
    }

    [Test]
    public void Blushing_IsTheFirstUnlock()
    {
        Assert.AreEqual(1, Faces.UnlockLevel(Faces.Blushing));
        Assert.IsTrue(Faces.HasCheeks(Faces.Blushing));
        Assert.IsFalse(Faces.HasCheeks(Faces.Classic));
    }

    [Test]
    public void UnlockLevels_RiseThroughTheList()
    {
        for (int i = 1; i < Faces.Count; i++)
        {
            Assert.Greater(Faces.UnlockLevel(i), Faces.UnlockLevel(i - 1), Faces.Name(i));
        }
    }

    [TestCase(Faces.Hearts, 13, Faces.Classic)]
    [TestCase(Faces.Hearts, 14, Faces.Hearts)]
    [TestCase(-1, 100, Faces.Classic)]
    [TestCase(99, 100, Faces.Classic)]
    public void Wearable_IsTheClassicFaceUntilUnlocked(int face, int level, int expected)
    {
        Assert.AreEqual(expected, Faces.Wearable(face, level));
    }

    [Test]
    public void BotFaces_AreAllUnlockedByLevelNine()
    {
        for (int i = 0; i < Faces.BotFaceCount; i++) Assert.IsTrue(Faces.IsUnlocked(i, 9), Faces.Name(i));
    }
}
