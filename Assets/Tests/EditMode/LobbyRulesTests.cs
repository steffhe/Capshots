using NUnit.Framework;

public class LobbyRulesTests
{
    [TestCase(1, 1, false, true)]
    [TestCase(3, 3, false, true)]
    [TestCase(3, 2, false, false)]
    [TestCase(3, 0, false, false)]
    [TestCase(0, 0, false, false)]
    [TestCase(3, 0, true, true)]
    [TestCase(0, 0, true, true)]
    public void CanStart_WhenEveryoneIsReady_OrTheHostForcesIt(int people, int ready, bool forced, bool expected)
    {
        Assert.AreEqual(expected, LobbyRules.CanStart(people, ready, forced));
    }

    [Test]
    public void Status_CountsWhoIsStillNotReady()
    {
        Assert.AreEqual("WAITING FOR 1 PLAYER TO READY UP", LobbyRules.Status(3, 2, false));
        Assert.AreEqual("WAITING FOR 3 PLAYERS TO READY UP", LobbyRules.Status(3, 0, false));
        Assert.AreEqual("EVERYONE'S READY", LobbyRules.Status(3, 3, false));
        Assert.AreEqual("WAITING FOR PLAYERS", LobbyRules.Status(0, 0, false));
    }

    [Test]
    public void Status_DuringTheCountdown_SaysWhoStartedIt()
    {
        Assert.AreEqual("EVERYONE'S READY", LobbyRules.Status(2, 2, true));
        Assert.AreEqual("THE HOST STARTED THE MATCH", LobbyRules.Status(2, 1, true));
    }
}
