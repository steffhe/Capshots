using System.Linq;
using NUnit.Framework;

public class ModeOrderTests
{
    // MatchMode values (MatchTypes.cs, outside the Core assembly the tests can see)
    private const int FreeForAll = 0, TeamDeathmatch = 1, CapGrab = 2, GunGame = 3, TeamCapGrab = 4;

    [Test]
    public void ListsEveryModeOnce()
    {
        CollectionAssert.AreEquivalent(new[] { FreeForAll, TeamDeathmatch, CapGrab, GunGame, TeamCapGrab }, ModeOrder.Menu);
    }

    [Test]
    public void TeamCapGrab_ComesRightAfterCapGrab()
    {
        int capGrab = System.Array.IndexOf(ModeOrder.Menu, CapGrab);
        Assert.AreEqual(TeamCapGrab, ModeOrder.Menu[capGrab + 1]);
    }

    [Test]
    public void FreeForAll_IsFirst()
    {
        Assert.AreEqual(FreeForAll, ModeOrder.Menu.First());
    }
}
