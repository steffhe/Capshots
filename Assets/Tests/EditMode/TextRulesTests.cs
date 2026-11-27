using NUnit.Framework;

public class TextRulesTests
{
    [TestCase("VOLT", "VOLT")]
    [TestCase("  Volt   the   Drone  ", "Volt the Drone")]
    [TestCase("<size=300>BIG</size>", "size=300BIG/size")]
    [TestCase("<color=red>x", "color=redx")]
    [TestCase("tab\there\nline", "tab here line")]
    [TestCase("   ", "")]
    [TestCase(null, "")]
    public void CleanName_RemovesTagsAndTidiesSpaces(string typed, string expected)
    {
        Assert.AreEqual(expected, TextRules.CleanName(typed));
    }

    [Test]
    public void CleanName_CutsToTheLimit()
    {
        Assert.AreEqual(TextRules.MaxNameLength, TextRules.CleanName(new string('A', 50)).Length);
        Assert.AreEqual(5, TextRules.CleanName("ABCDEFGH", 5).Length);
    }

    [Test]
    public void AllowedInName_RefusesTagBrackets()
    {
        Assert.IsTrue(TextRules.AllowedInName('a'));
        Assert.IsTrue(TextRules.AllowedInName(' '));
        Assert.IsFalse(TextRules.AllowedInName('<'));
        Assert.IsFalse(TextRules.AllowedInName('>'));
        Assert.IsFalse(TextRules.AllowedInName('\n'));
    }

    [Test]
    public void Escape_BreaksTags()
    {
        string escaped = TextRules.Escape("<b>x</b>");
        Assert.IsFalse(escaped.Contains("<b>"));
        Assert.AreEqual("", TextRules.Escape(null));
    }
}
