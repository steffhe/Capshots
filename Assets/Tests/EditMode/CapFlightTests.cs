using NUnit.Framework;
using UnityEngine;

public class CapFlightTests
{
    private static readonly Vector3 From = new Vector3(0f, 1.5f, 0f);
    private static readonly Vector3 To = new Vector3(1.6f, 0.9f, 0f);
    private static readonly Vector3 Side = Vector3.forward;

    [Test]
    public void StartsAtTheHead_AndLandsOnTheSpot()
    {
        Assert.Less(Vector3.Distance(From, CapFlight.Position(From, To, Side, 2.6f, 0f)), 0.0001f);
        Assert.Less(Vector3.Distance(To, CapFlight.Position(From, To, Side, 2.6f, 1f)), 0.0001f);
    }

    [Test]
    public void FliesWellAboveBothEnds()
    {
        float highest = float.MinValue;
        for (int i = 0; i <= 100; i++) highest = Mathf.Max(highest, CapFlight.Position(From, To, Side, 2.6f, i / 100f).y);
        Assert.Greater(highest, From.y + 2f);
    }

    [Test]
    public void PeaksBeforeHalfway()
    {
        float best = float.MinValue, at = 0f;
        for (int i = 0; i <= 100; i++)
        {
            float y = CapFlight.Position(From, To, Side, 2.6f, i / 100f).y;
            if (y > best) { best = y; at = i / 100f; }
        }
        Assert.Less(at, 0.5f);
    }

    [Test]
    public void LandsUpright()
    {
        Assert.AreEqual(0f, CapFlight.Tumble(0f));
        Assert.AreEqual(0f, Mathf.Repeat(CapFlight.Tumble(1f), 360f), 0.001f);
    }
}
