using NUnit.Framework;
using UnityEngine;

public class GunSatelliteLayoutTests
{
    private const int Count = 5;
    // A pistol-sized gun: 6 cm wide, 20 cm tall with the grip, 30 cm long, muzzle at the front top
    private static readonly Bounds Pistol = new Bounds(new Vector3(0f, 0f, 0.1f), new Vector3(0.06f, 0.2f, 0.3f));
    private static readonly Vector3 Muzzle = new Vector3(0f, 0.06f, 0.25f);
    // The top of the gun, above which the iron sights and scope are
    private static float Top => Pistol.max.y;

    private static Gun MakeGun(int burst, int pellets = 1, float scopeFov = 0f)
    {
        Gun gun = ScriptableObject.CreateInstance<Gun>();
        gun.burst = burst;
        gun.pellets = pellets;
        gun.scopeFov = scopeFov;
        return gun;
    }

    [Test]
    public void FormOf_ClassifiesEachGun()
    {
        Assert.AreEqual(GunSatelliteLayout.Form.Fan, GunSatelliteLayout.FormOf(MakeGun(0, 8)));
        Assert.AreEqual(GunSatelliteLayout.Form.Orbit, GunSatelliteLayout.FormOf(MakeGun(1)));
        Assert.AreEqual(GunSatelliteLayout.Form.Tight, GunSatelliteLayout.FormOf(MakeGun(0)));
        Assert.AreEqual(GunSatelliteLayout.Form.Rail, GunSatelliteLayout.FormOf(MakeGun(0, 1, 20f)));
        Assert.AreEqual(GunSatelliteLayout.Form.Tight, GunSatelliteLayout.FormOf(null));
    }

    private static GunSatelliteLayout.Pose Arrange(GunSatelliteLayout.Form form, int i, float spin = 0f)
    {
        return GunSatelliteLayout.Arrange(form, i, Count, Pistol, Muzzle, spin);
    }

    [Test]
    public void NothingSitsAboveTheSightsOnTheFormsAimedThroughThem()
    {
        // The pistol and sniper are aimed down their sights: their pieces stay below the top of the gun
        foreach (GunSatelliteLayout.Form form in new[] { GunSatelliteLayout.Form.Tight, GunSatelliteLayout.Form.Rail })
        {
            for (int i = 0; i < Count; i++)
            {
                Assert.LessOrEqual(Arrange(form, i).position.y, Top, form + " piece " + i);
            }
        }
    }

    [TestCase(GunSatelliteLayout.Form.Fan)]
    [TestCase(GunSatelliteLayout.Form.Orbit)]
    public void RingsCircleTheBarrelAndLeaveTheLineOfFireClear(GunSatelliteLayout.Form form)
    {
        float radius = GunSatelliteLayout.RingRadius(Pistol);
        for (int i = 0; i < Count; i++)
        {
            Vector3 p = Arrange(form, i).position;
            float fromAxis = new Vector2(p.x - Muzzle.x, p.y - Muzzle.y).magnitude;
            Assert.GreaterOrEqual(fromAxis, radius - 1e-4f, "piece " + i);
        }
    }

    [Test]
    public void TheFanSpreadsAheadOfTheMuzzleAndTheOrbitWrapsTheBarrel()
    {
        for (int i = 0; i < Count; i++)
        {
            Assert.Greater(Arrange(GunSatelliteLayout.Form.Fan, i).position.z, Muzzle.z, "fan " + i);
            Assert.Less(Arrange(GunSatelliteLayout.Form.Orbit, i).position.z, Muzzle.z, "orbit " + i);
        }
    }

    [Test]
    public void SpinTurnsTheOrbitOnly()
    {
        Vector3 still = Arrange(GunSatelliteLayout.Form.Orbit, 0, 0f).position;
        Vector3 turned = Arrange(GunSatelliteLayout.Form.Orbit, 0, 1f).position;
        Assert.Greater(Vector3.Distance(still, turned), 0.01f);
        Assert.AreEqual(Arrange(GunSatelliteLayout.Form.Fan, 0, 0f).position, Arrange(GunSatelliteLayout.Form.Fan, 0, 1f).position);
    }

    [Test]
    public void TheRailRunsForwardPastTheMuzzle()
    {
        float lastZ = float.MinValue;
        for (int i = 0; i < Count; i++)
        {
            float z = Arrange(GunSatelliteLayout.Form.Rail, i).position.z;
            Assert.Greater(z, lastZ, "piece " + i);
            lastZ = z;
        }
        Assert.Greater(lastZ, Muzzle.z);
    }

    [Test]
    public void ThePistolShowsAFewPiecesAndTucksTheRestAway()
    {
        for (int i = 0; i < Count; i++)
        {
            float scale = Arrange(GunSatelliteLayout.Form.Tight, i).scale;
            if (i < GunSatelliteLayout.TightCount) Assert.AreEqual(1f, scale, "piece " + i);
            else Assert.AreEqual(0f, scale, "piece " + i);
        }
    }

    [Test]
    public void KicksPushAwayFromTheBarrel()
    {
        Vector3 piece = Arrange(GunSatelliteLayout.Form.Fan, 0).position;
        Vector3 kick = GunSatelliteLayout.KickDirection(GunSatelliteLayout.Form.Fan, piece, Muzzle);
        Vector3 after = piece + kick * 0.05f;
        Assert.Greater(new Vector2(after.x - Muzzle.x, after.y - Muzzle.y).magnitude, new Vector2(piece.x - Muzzle.x, piece.y - Muzzle.y).magnitude);
        Assert.AreEqual(Vector3.forward, GunSatelliteLayout.KickDirection(GunSatelliteLayout.Form.Rail, piece, Muzzle));
    }
}
