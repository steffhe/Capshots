using NUnit.Framework;

public class PopupQueueTests
{
    private static PopupQueue.Popup P(string text, PopupQueue.Kind kind) => new PopupQueue.Popup { text = text, kind = kind };

    [Test]
    public void FirstMessage_ShowsAtOnce()
    {
        PopupQueue q = new PopupQueue();
        Assert.IsTrue(q.Push(P("FIGHT!", PopupQueue.Kind.Info)));
        Assert.AreEqual("FIGHT!", q.Current.text);
    }

    [Test]
    public void Xp_ReplacesXp()
    {
        PopupQueue q = new PopupQueue();
        q.Push(P("+100 XP KILL", PopupQueue.Kind.Xp));
        Assert.IsTrue(q.Push(P("+150 XP CAPSHOT", PopupQueue.Kind.Xp)));
        Assert.AreEqual("+150 XP CAPSHOT", q.Current.text);
        Assert.AreEqual(0, q.Waiting);
    }

    [Test]
    public void Xp_WaitsBehindInfo_OnlyTheNewest()
    {
        PopupQueue q = new PopupQueue();
        q.Push(P("YOU'RE STUCK!", PopupQueue.Kind.Info));
        Assert.IsFalse(q.Push(P("+100 XP KILL", PopupQueue.Kind.Xp)));
        Assert.IsFalse(q.Push(P("+200 XP DOUBLE KILL", PopupQueue.Kind.Xp)));
        Assert.AreEqual(1, q.Waiting);
        Assert.IsTrue(q.Next(out PopupQueue.Popup next));
        Assert.AreEqual("+200 XP DOUBLE KILL", next.text);
    }

    [Test]
    public void Info_CutsXpShort_AndXpComesBack()
    {
        PopupQueue q = new PopupQueue();
        q.Push(P("+100 XP KILL", PopupQueue.Kind.Xp));
        Assert.IsTrue(q.Push(P("YOU ARE ON BLUE TEAM", PopupQueue.Kind.Info)));
        Assert.IsTrue(q.Next(out PopupQueue.Popup next));
        Assert.AreEqual("+100 XP KILL", next.text);
    }

    [Test]
    public void Info_WaitsBehindInfo_AheadOfXp()
    {
        PopupQueue q = new PopupQueue();
        q.Push(P("MATCH STARTING", PopupQueue.Kind.Info));
        q.Push(P("+100 XP KILL", PopupQueue.Kind.Xp));
        q.Push(P("YOU'RE STUCK!", PopupQueue.Kind.Info));
        q.Next(out PopupQueue.Popup first);
        q.Next(out PopupQueue.Popup second);
        Assert.AreEqual("YOU'RE STUCK!", first.text);
        Assert.AreEqual("+100 XP KILL", second.text);
        Assert.IsFalse(q.Next(out _));
    }

    [Test]
    public void Urgent_ShowsAtOnceAndClearsTheRest()
    {
        PopupQueue q = new PopupQueue();
        q.Push(P("MATCH STARTING", PopupQueue.Kind.Info));
        q.Push(P("YOU'RE STUCK!", PopupQueue.Kind.Info));
        Assert.IsTrue(q.Push(P("CONNECTION LOST", PopupQueue.Kind.Urgent)));
        Assert.AreEqual(0, q.Waiting);
        Assert.AreEqual("CONNECTION LOST", q.Current.text);
    }

    [Test]
    public void Repeats_AreDropped()
    {
        PopupQueue q = new PopupQueue();
        q.Push(P("WAITING FOR PLAYERS", PopupQueue.Kind.Info));
        Assert.IsFalse(q.Push(P("WAITING FOR PLAYERS", PopupQueue.Kind.Info)));
        Assert.AreEqual(0, q.Waiting);
    }

    [Test]
    public void Waiting_IsCapped()
    {
        PopupQueue q = new PopupQueue();
        q.Push(P("A", PopupQueue.Kind.Info));
        for (int i = 0; i < 10; i++) q.Push(P("B" + i, PopupQueue.Kind.Info));
        Assert.AreEqual(PopupQueue.MaxWaiting, q.Waiting);
    }
}
