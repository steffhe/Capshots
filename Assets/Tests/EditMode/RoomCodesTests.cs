using NUnit.Framework;

public class RoomCodesTests
{
    [Test]
    public void New_IsAValidCode()
    {
        System.Random random = new System.Random(7);
        for (int i = 0; i < 200; i++)
        {
            string code = RoomCodes.New(random.Next);
            Assert.IsTrue(RoomCodes.IsValid(code), code);
        }
    }

    [TestCase(" kx-qt ", "KXQT")]
    [TestCase("abcd", "ABCD")]
    [TestCase("", "")]
    [TestCase(null, "")]
    public void Normalize_UpperCasesAndDropsSeparators(string typed, string expected)
    {
        Assert.AreEqual(expected, RoomCodes.Normalize(typed));
    }

    [TestCase("KXQT", true)]
    [TestCase("KXQ", false)]
    [TestCase("KXQTA", false)]
    [TestCase("KXOT", false)]
    [TestCase("KX1T", false)]
    [TestCase("kxqt", false)]
    [TestCase(null, false)]
    public void IsValid_OnlyFourAllowedLetters(string code, bool expected)
    {
        Assert.AreEqual(expected, RoomCodes.IsValid(code));
    }
}
