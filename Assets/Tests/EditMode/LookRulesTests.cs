using NUnit.Framework;
using UnityEngine;

// Core/LookRules: aim sensitivity matched to zoom, mouse travel per turn, and frame-rate independent easing
public class LookRulesTests
{
    [Test]
    public void ZoomSensitivity_IsOneWithoutZoom()
    {
        Assert.AreEqual(1f, LookRules.ZoomSensitivity(70f, 70f), 1e-5f);
    }

    [Test]
    public void ZoomSensitivity_SlowsTurningAsTheViewNarrows()
    {
        // The plain aim (60% of a 70 degree view) and the sniper scope (20 degrees)
        float aim = LookRules.ZoomSensitivity(42f, 70f);
        float scope = LookRules.ZoomSensitivity(20f, 70f);
        Assert.AreEqual(Mathf.Tan(21f * Mathf.Deg2Rad) / Mathf.Tan(35f * Mathf.Deg2Rad), aim, 1e-5f);
        Assert.Less(scope, aim);
        Assert.Less(aim, 1f);
    }

    [Test]
    public void ZoomSensitivity_IgnoresNonsense()
    {
        Assert.AreEqual(1f, LookRules.ZoomSensitivity(0f, 70f));
        Assert.AreEqual(1f, LookRules.ZoomSensitivity(40f, -5f));
        Assert.AreEqual(1f, LookRules.ZoomSensitivity(40f, 180f));
    }

    [Test]
    public void StickResponse_SlowsSmallPushesAndKeepsFullOnes()
    {
        Assert.AreEqual(0.25f, LookRules.StickResponse(new Vector2(0.5f, 0f), 2f).x, 1e-5f);
        Assert.AreEqual(1f, LookRules.StickResponse(new Vector2(0f, 1f), 2f).y, 1e-5f);
        Assert.AreEqual(Vector2.zero, LookRules.StickResponse(Vector2.zero, 2f));
    }

    [Test]
    public void StickResponse_KeepsTheDirection()
    {
        Vector2 stick = new Vector2(0.3f, 0.4f); // a push of 0.5 up and to the right
        Vector2 curved = LookRules.StickResponse(stick, 2f);
        Assert.AreEqual(0.25f, curved.magnitude, 1e-5f);
        Assert.AreEqual(0f, Vector2.Angle(stick, curved), 1e-3f);
    }

    [Test]
    public void StickResponse_NeverGoesPastAFullPush()
    {
        Assert.AreEqual(1f, LookRules.StickResponse(new Vector2(1f, 1f), 2f).magnitude, 1e-5f);
    }

    [Test]
    public void CentimetresPer360_MatchesAKnownSetting()
    {
        // 0.1 degrees per count: 3600 counts a turn, 4.5 inches at 800 DPI
        Assert.AreEqual(4.5f * 2.54f, LookRules.CentimetresPer360(0.1f, 800f), 1e-3f);
        Assert.AreEqual(0f, LookRules.CentimetresPer360(0f, 800f));
    }

    [Test]
    public void Ease_IsTheOldLerpAtSixtyFramesASecond()
    {
        Assert.AreEqual(25f / 60f, LookRules.Ease(25f, 1f / 60f), 1e-5f);
    }

    [Test]
    public void Ease_CoversTheSameGroundAtAnyFrameRate()
    {
        // Half a second of easing towards 1, at 30, 60 and 144 frames a second
        float At(int fps)
        {
            float x = 0f;
            for (int i = 0; i < fps / 2; i++) x = Mathf.Lerp(x, 1f, LookRules.Ease(8f, 1f / fps));
            return x;
        }
        Assert.AreEqual(At(60), At(30), 1e-3f);
        Assert.AreEqual(At(60), At(144), 1e-3f);
    }

    [Test]
    public void Ease_NeverOvershootsAtLowFrameRates()
    {
        float share = LookRules.Ease(25f, 0.2f);
        Assert.Greater(share, 0f);
        Assert.LessOrEqual(share, 1f);
    }
}
