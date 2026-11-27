using NUnit.Framework;

// The gun's changes of form: when they happen, how, and when the gun may fire (GunTransform), and the
// form a player's game asks for (GunFormRequest)
public class GunTransformTests
{
    private const int Forms = 4;

    // ---- Decide ----

    [Test]
    public void Decide_ADifferentFormTransforms()
    {
        Assert.AreEqual(GunTransform.Change.Morph, GunTransform.Decide(3, 1, Forms, false, true, 5, false));
    }

    [TestCase(-1)]
    [TestCase(4)]
    [TestCase(99)]
    public void Decide_IgnoresFormsThatDontExist(int requested)
    {
        Assert.AreEqual(GunTransform.Change.None, GunTransform.Decide(requested, 1, Forms, false, false, 0, true));
    }

    [Test]
    public void Decide_TheFormHeldIsNoChange()
    {
        // Including mid-transformation: asking again for the form it's turning into doesn't restart it
        Assert.AreEqual(GunTransform.Change.None, GunTransform.Decide(2, 2, Forms, false, true, 0, false));
        Assert.AreEqual(GunTransform.Change.None, GunTransform.Decide(2, 2, Forms, true, false, 0, true));
    }

    [Test]
    public void Decide_TheStartingPickIsInstant()
    {
        Assert.AreEqual(GunTransform.Change.Instant, GunTransform.Decide(0, 1, Forms, true, false, 0, true));
    }

    [Test]
    public void Decide_APlayersOwnChoiceRightAfterSpawningStillTransforms()
    {
        // The exploit this closes: with the default starting form, no starting change is asked for,
        // and the first change by hand used to be instant too
        Assert.AreEqual(GunTransform.Change.Morph, GunTransform.Decide(0, 1, Forms, false, false, 0, true));
    }

    [Test]
    public void Decide_TheStartingPickIsInstantOnlyOnce()
    {
        Assert.AreEqual(GunTransform.Change.Morph, GunTransform.Decide(0, 1, Forms, true, true, 0, true));
    }

    [Test]
    public void Decide_TheStartingPickTransformsAfterAShot()
    {
        Assert.AreEqual(GunTransform.Change.Morph, GunTransform.Decide(0, 1, Forms, true, false, 1, true));
    }

    [Test]
    public void Decide_TheStartingPickTransformsOnceTheWindowHasClosed()
    {
        // A slow connection whose first input came late
        Assert.AreEqual(GunTransform.Change.Morph, GunTransform.Decide(0, 1, Forms, true, false, 0, false));
    }

    [Test]
    public void Decide_AnEmptyLoadoutNeverChanges()
    {
        Assert.AreEqual(GunTransform.Change.None, GunTransform.Decide(0, 0, 0, true, false, 0, true));
    }

    // ---- CanFire ----

    [Test]
    public void CanFire_OnlyWhenNothingHoldsTheGun()
    {
        Assert.IsTrue(GunTransform.CanFire(false, false, false, false, 1, Forms));
        Assert.IsFalse(GunTransform.CanFire(true, false, false, false, 1, Forms), "changing form");
        Assert.IsFalse(GunTransform.CanFire(false, true, false, false, 1, Forms), "swapping the cell");
        Assert.IsFalse(GunTransform.CanFire(false, false, true, false, 1, Forms), "throwing");
        Assert.IsFalse(GunTransform.CanFire(false, false, false, true, 1, Forms), "cooking a grenade");
    }

    [TestCase(-1)]
    [TestCase(4)]
    public void CanFire_NeverWithAFormThatDoesntExist(int equipped)
    {
        Assert.IsFalse(GunTransform.CanFire(false, false, false, false, equipped, Forms));
    }

    // ---- Animation ----

    [Test]
    public void AnimationSeconds_YourOwnGunFinishesAsItCanFire()
    {
        Assert.AreEqual(0.3f, GunTransform.AnimationSeconds(0.3f, 0.5f, true), 1e-5f);
        // Never longer than a whole change
        Assert.AreEqual(0.5f, GunTransform.AnimationSeconds(0.9f, 0.5f, true), 1e-5f);
    }

    [Test]
    public void AnimationSeconds_OtherPlayersGunsPlayItWhole()
    {
        Assert.AreEqual(0.5f, GunTransform.AnimationSeconds(0.1f, 0.5f, false), 1e-5f);
        Assert.AreEqual(0.5f, GunTransform.AnimationSeconds(null, 0.5f, true), 1e-5f);
    }

    [Test]
    public void AnimationSeconds_NoneWhenAlmostDone()
    {
        Assert.AreEqual(0f, GunTransform.AnimationSeconds(0.01f, 0.5f, true), 1e-5f);
        Assert.AreEqual(0f, GunTransform.AnimationSeconds(0.2f, 0f, true), 1e-5f);
    }

    [Test]
    public void Progress_RunsFromZeroToOne()
    {
        Assert.AreEqual(0f, GunTransform.Progress(0.5f, 0.5f), 1e-5f);
        Assert.AreEqual(0.5f, GunTransform.Progress(0.25f, 0.5f), 1e-5f);
        Assert.AreEqual(1f, GunTransform.Progress(0f, 0.5f), 1e-5f);
        Assert.AreEqual(-1f, GunTransform.Progress(null, 0.5f), 1e-5f);
    }

    // ---- GunFormRequest ----

    [Test]
    public void Request_ANewBodyAsksForTheStartingFormAsTheAutomaticPick()
    {
        var request = new GunFormRequest();
        Assert.AreEqual(0, request.Slot);
        request.NewBody(2);
        Assert.AreEqual(2, request.Wanted);
        Assert.AreEqual(3, request.Slot);
        Assert.IsTrue(request.IsStartingPick);
    }

    [Test]
    public void Request_IsSentEveryTickUntilChanged()
    {
        // Sticky: nothing clears it between inputs, so a lost input can't lose the change
        var request = new GunFormRequest();
        request.NewBody(1);
        request.Pick(3);
        Assert.AreEqual(4, request.Slot);
        Assert.AreEqual(4, request.Slot);
        Assert.IsFalse(request.IsStartingPick);
    }

    [Test]
    public void Request_AChoiceByHandIsNeverTheStartingPick()
    {
        var request = new GunFormRequest();
        request.NewBody(1);
        request.Pick(1);
        Assert.IsFalse(request.IsStartingPick, "picking the same form by hand still counts as a choice");
        request.NewBody(0);
        Assert.IsTrue(request.IsStartingPick, "a new body starts over");
    }

    [Test]
    public void Request_StepsFromTheFormAskedFor()
    {
        // Two quick wheel notches mid-transformation go two forms on, not one
        var request = new GunFormRequest();
        request.NewBody(1);
        request.Step(1, 1, Forms);
        request.Step(1, 1, Forms);
        Assert.AreEqual(3, request.Wanted);
        request.Step(1, 1, Forms);
        Assert.AreEqual(0, request.Wanted, "wraps round");
        request.Step(-1, 1, Forms);
        Assert.AreEqual(3, request.Wanted, "and back");
    }

    [Test]
    public void Request_StepsFromTheFormHeldWhenNothingWasAskedFor()
    {
        var request = new GunFormRequest();
        request.Step(1, 2, Forms);
        Assert.AreEqual(3, request.Wanted);
    }

    [Test]
    public void Request_IgnoresImpossiblePicks()
    {
        var request = new GunFormRequest();
        request.NewBody(1);
        request.Pick(-1);
        Assert.AreEqual(1, request.Wanted);
        request.Step(1, 0, 0);
        Assert.AreEqual(1, request.Wanted);
        request.Clear();
        Assert.AreEqual(0, request.Slot);
        Assert.IsFalse(request.IsStartingPick);
    }
}
