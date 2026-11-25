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

    [TestCase("https://steffhe.github.io/Capshots/?code=KXQT", "KXQT")]
    [TestCase("https://steffhe.github.io/Capshots/?code=kxqt", "KXQT")]
    [TestCase("https://steffhe.github.io/Capshots/?x=1&CODE=kx-qt#top", "KXQT")]
    [TestCase("https://steffhe.github.io/Capshots/?code=KXOT", null)]
    [TestCase("https://steffhe.github.io/Capshots/", null)]
    [TestCase("https://steffhe.github.io/Capshots/?other=KXQT", null)]
    [TestCase("", null)]
    [TestCase(null, null)]
    public void FromUrl_ReadsAValidCodeFromTheQuery(string url, string expected)
    {
        Assert.AreEqual(expected, RoomCodes.FromUrl(url));
    }

    [TestCase("https://steffhe.github.io/Capshots/", "https://steffhe.github.io/Capshots/?code=KXQT")]
    [TestCase("https://steffhe.github.io/Capshots/?code=ABCD", "https://steffhe.github.io/Capshots/?code=KXQT")]
    [TestCase("https://steffhe.github.io/Capshots/#x", "https://steffhe.github.io/Capshots/?code=KXQT")]
    public void InviteLink_ReplacesAnyQuery(string page, string expected)
    {
        Assert.AreEqual(expected, RoomCodes.InviteLink(page, "KXQT"));
        Assert.AreEqual("KXQT", RoomCodes.FromUrl(RoomCodes.InviteLink(page, "KXQT")));
    }
}
