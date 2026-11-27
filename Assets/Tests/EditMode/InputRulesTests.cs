using NUnit.Framework;
using UnityEngine;

public class InputRulesTests
{
    [Test]
    public void Sanitize_LeavesAnOrdinaryInputAlone()
    {
        Vector2 move = new Vector2(0.6f, -0.8f);
        float yaw = 123.5f, pitch = -42f;
        InputRules.Sanitize(ref move, ref yaw, ref pitch, 60f);
        Assert.AreEqual(new Vector2(0.6f, -0.8f), move);
        Assert.AreEqual(123.5f, yaw, 1e-4f);
        Assert.AreEqual(-42f, pitch, 1e-4f);
    }

    [Test]
    public void Sanitize_ClampsPitchToWhatTheCameraAllows()
    {
        Vector2 move = Vector2.zero;
        float yaw = 0f, up = 89f, down = -500f;
        InputRules.Sanitize(ref move, ref yaw, ref up, 60f);
        InputRules.Sanitize(ref move, ref yaw, ref down, 60f);
        Assert.AreEqual(60f, up, 1e-4f);
        Assert.AreEqual(-60f, down, 1e-4f);
    }

    [Test]
    public void Sanitize_WrapsYawAndShortensLongMoves()
    {
        Vector2 move = new Vector2(5f, 0f);
        float yaw = -90f, pitch = 0f;
        InputRules.Sanitize(ref move, ref yaw, ref pitch, 60f);
        Assert.AreEqual(1f, move.magnitude, 1e-4f);
        Assert.AreEqual(270f, yaw, 1e-4f);
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void Sanitize_ReplacesNumbersThatArentNumbers(float bad)
    {
        Vector2 move = new Vector2(bad, 0.5f);
        float yaw = bad, pitch = bad;
        InputRules.Sanitize(ref move, ref yaw, ref pitch, 60f);
        Assert.AreEqual(Vector2.zero, move);
        Assert.AreEqual(0f, yaw);
        Assert.AreEqual(0f, pitch);
    }
}
