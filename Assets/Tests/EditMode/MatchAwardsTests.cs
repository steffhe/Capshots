using System.Collections.Generic;
using NUnit.Framework;

public class MatchAwardsTests
{
    private static MatchAwards.Tally T(int actor, int kills, int deaths, int capshots = 0, int grenades = 0, int streak = 0)
    {
        return new MatchAwards.Tally { actor = actor, name = "P" + actor, kills = kills, deaths = deaths, capshots = capshots, grenadeKills = grenades, bestStreak = streak };
    }

    private static MatchAwards.Award Find(List<MatchAwards.Award> awards, string title)
    {
        foreach (MatchAwards.Award a in awards) if (a.title == title) return a;
        Assert.Fail("No " + title + " award");
        return default;
    }

    [Test]
    public void NoAwards_WithFewerThanTwoPlayers()
    {
        Assert.IsEmpty(MatchAwards.Choose(new List<MatchAwards.Tally> { T(1, 10, 0, capshots: 5) }));
        Assert.IsEmpty(MatchAwards.Choose(null));
    }

    [Test]
    public void EachAward_GoesToWhoeverDidItMost()
    {
        var players = new List<MatchAwards.Tally>
        {
            T(1, kills: 8, deaths: 2, capshots: 1, grenades: 0, streak: 4),
            T(2, kills: 5, deaths: 6, capshots: 3, grenades: 2, streak: 2),
            T(3, kills: 1, deaths: 1, capshots: 0, grenades: 0, streak: 1),
        };
        var awards = MatchAwards.Choose(players);

        Assert.AreEqual(2, Find(awards, "CAPSHOT KING").actor);
        Assert.AreEqual(1, Find(awards, "ON A ROLL").actor);
        Assert.AreEqual(2, Find(awards, "STICKY FINGERS").actor);
        // Fewest deaths among those with a kill; a tie goes to more kills
        Assert.AreEqual(3, Find(awards, "UNTOUCHABLE").actor);
        Assert.LessOrEqual(awards.Count, MatchAwards.MaxAwards);
    }

    [Test]
    public void AwardsNobodyQualifiesFor_AreLeftOut()
    {
        var players = new List<MatchAwards.Tally> { T(1, 2, 1, streak: 2), T(2, 0, 2) };
        var awards = MatchAwards.Choose(players);
        foreach (MatchAwards.Award a in awards)
        {
            Assert.AreNotEqual("CAPSHOT KING", a.title);
            Assert.AreNotEqual("ON A ROLL", a.title);
            Assert.AreNotEqual("STICKY FINGERS", a.title);
            Assert.AreNotEqual("PUNCHING BAG", a.title);
        }
        Assert.AreEqual(1, Find(awards, "UNTOUCHABLE").actor);
    }

    [Test]
    public void Ties_GoToMoreKills()
    {
        var players = new List<MatchAwards.Tally> { T(1, 3, 2, capshots: 2), T(2, 6, 4, capshots: 2) };
        Assert.AreEqual(2, Find(MatchAwards.Choose(players), "CAPSHOT KING").actor);
    }

    [Test]
    public void PackAndUnpack_RoundTrip_AndKeepSeparatorsOutOfNames()
    {
        var awards = new List<MatchAwards.Award>
        {
            new MatchAwards.Award { title = "CAPSHOT KING", detail = "3 CAPSHOTS", actor = 4, name = "Odd|Name" },
            new MatchAwards.Award { title = "UNTOUCHABLE", detail = "NEVER DIED", actor = 101, name = "Volt BOT" },
        };
        var back = MatchAwards.Unpack(MatchAwards.Pack(awards));
        Assert.AreEqual(2, back.Count);
        Assert.AreEqual("CAPSHOT KING", back[0].title);
        Assert.AreEqual("3 CAPSHOTS", back[0].detail);
        Assert.AreEqual(4, back[0].actor);
        Assert.AreEqual("Odd/Name", back[0].name);
        Assert.AreEqual(101, back[1].actor);
        Assert.IsEmpty(MatchAwards.Unpack(""));
    }

    [Test]
    public void GroupByPlayer_OneGroupPerPlayer_InOrderOfTheirFirstAward()
    {
        var awards = new List<MatchAwards.Award>
        {
            new MatchAwards.Award { title = "CAPSHOT KING", actor = 7 },
            new MatchAwards.Award { title = "ON A ROLL", actor = 3 },
            new MatchAwards.Award { title = "STICKY FINGERS", actor = 7 },
            new MatchAwards.Award { title = "PUNCHING BAG", actor = 9 },
        };
        var groups = MatchAwards.GroupByPlayer(awards);
        Assert.AreEqual(3, groups.Count);
        Assert.AreEqual(7, groups[0][0].actor);
        Assert.AreEqual(2, groups[0].Count);
        Assert.AreEqual("CAPSHOT KING", groups[0][0].title);
        Assert.AreEqual("STICKY FINGERS", groups[0][1].title);
        Assert.AreEqual(3, groups[1][0].actor);
        Assert.AreEqual(9, groups[2][0].actor);
        Assert.IsEmpty(MatchAwards.GroupByPlayer(null));
    }
}
