using NUnit.Framework;
using UnityEngine;
using Layout = GunSatelliteLayout;

public class GunSatelliteLayoutTests
{
    // The game's gun models' parts (Assets/Resources), in their own space
    private static Layout.Shape Pistol => new Layout.Shape
    {
        body = new Bounds(new Vector3(0.444f, 0.288f, 1.333f), new Vector3(0.14f, 0.218f, 0.62f)),
    };
    private static Layout.Shape MachineGun => new Layout.Shape
    {
        body = new Bounds(new Vector3(0.444f, 0.288f, 1.628f), new Vector3(0.14f, 0.218f, 1.496f)),
    };
    private static Layout.Shape Shotgun => new Layout.Shape
    {
        body = new Bounds(new Vector3(0.444f, 0.288f, 1.45f), new Vector3(0.2f, 0.26f, 1.15f)),
        hasUnder = true,
        under = new Bounds(new Vector3(0.444f, 0.12f, 1.68f), new Vector3(0.17f, 0.1f, 0.42f)),
    };
    private static Layout.Shape Sniper => new Layout.Shape
    {
        body = new Bounds(new Vector3(0.444f, 0.288f, 1.564f), new Vector3(0.14f, 0.218f, 1.125f)),
        hasBarrel = true,
        barrel = new Bounds(new Vector3(0.444f, 0.34f, 2.66f), new Vector3(0.109f, 0.075f, 1.496f)),
    };

    private static Layout.Motion At(float shotAge, float time = 0f, float spin = 0f, float aim = 0f)
    {
        return new Layout.Motion { time = time, spin = spin, shotAge = shotAge, aim = aim };
    }

    private static readonly Layout.Motion Rest = At(100f);
    // A moment into a shot, at its strongest
    private static readonly Layout.Motion Firing = At(0.035f);

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
        Assert.AreEqual(Layout.Form.Fan, Layout.FormOf(MakeGun(0, 8)));
        Assert.AreEqual(Layout.Form.Orbit, Layout.FormOf(MakeGun(1)));
        Assert.AreEqual(Layout.Form.Tight, Layout.FormOf(MakeGun(0)));
        Assert.AreEqual(Layout.Form.Rail, Layout.FormOf(MakeGun(0, 1, 20f)));
        Assert.AreEqual(Layout.Form.Tight, Layout.FormOf(null));
    }

    [Test]
    public void Kick_RisesFastAndDiesAway()
    {
        Assert.AreEqual(0f, Layout.Kick(-0.1f));
        Assert.AreEqual(0f, Layout.Kick(0f));
        Assert.AreEqual(1f, Layout.Kick(0.035f), 1e-4f);
        Assert.Less(Layout.Kick(0.3f), 0.05f);
        Assert.Less(Layout.Kick(100f), 1e-4f);
    }

    [Test]
    public void Pump_RacksAfterTheShotAndComesBack()
    {
        Assert.AreEqual(0f, Layout.Pump(0f));
        Assert.Greater(Layout.Pump(0.35f), 0.9f);
        Assert.AreEqual(0f, Layout.Pump(1f));
    }

    [Test]
    public void TheSniperBarrelIsTheSegments()
    {
        Assert.AreEqual("Barrel", Layout.ReplacedPart(Layout.Form.Rail));
        Assert.IsNull(Layout.ReplacedPart(Layout.Form.Tight));

        Bounds barrel = Sniper.barrel;
        float lastZ = float.MinValue;
        for (int i = 0; i < Layout.Count; i++)
        {
            Layout.Pose pose = Layout.Arrange(Layout.Form.Rail, i, Sniper, Rest);
            Assert.Greater(pose.position.z, lastZ, "in order, back to front");
            Assert.That(pose.position.z, Is.InRange(barrel.min.z, barrel.max.z), "piece " + i);
            Assert.AreEqual(barrel.center.y, pose.position.y, 1e-4f);
            Assert.Greater(pose.size.x, barrel.size.x * 0.9f, "as thick as the barrel");
            lastZ = pose.position.z;
        }
    }

    [Test]
    public void AShotRunsAWaveDownTheSniperBarrel()
    {
        // Just after the shot the back segment has moved further than the front one
        Layout.Motion early = At(0.02f);
        float back = Layout.Arrange(Layout.Form.Rail, 0, Sniper, early).position.z - Layout.Arrange(Layout.Form.Rail, 0, Sniper, Rest).position.z;
        float front = Layout.Arrange(Layout.Form.Rail, Layout.Count - 1, Sniper, early).position.z - Layout.Arrange(Layout.Form.Rail, Layout.Count - 1, Sniper, Rest).position.z;
        Assert.Greater(back, front);
        Assert.Greater(back, 0f);
    }

    [Test]
    public void TheRotaryBarrelSleevesTheFrontOfTheBodyAndSpins()
    {
        Layout.Shape gun = MachineGun;
        Vector3 muzzle = gun.Muzzle;
        for (int i = 0; i < Layout.Count; i++)
        {
            Layout.Pose pose = Layout.Arrange(Layout.Form.Orbit, i, gun, Rest);
            Assert.Less(pose.position.z - pose.size.z * 0.5f, gun.body.max.z, "reaches back over the body");
            Assert.Greater(pose.position.z + pose.size.z * 0.5f, gun.body.max.z, "and on past it");
            float fromAxis = new Vector2(pose.position.x - muzzle.x, pose.position.y - muzzle.y).magnitude;
            Assert.AreEqual(Layout.RotaryRadius(gun), fromAxis, 1e-4f);
        }
        Vector3 still = Layout.Arrange(Layout.Form.Orbit, 0, gun, At(100f, 0f, 0f)).position;
        Vector3 turned = Layout.Arrange(Layout.Form.Orbit, 0, gun, At(100f, 0f, 1f)).position;
        Assert.Greater(Vector3.Distance(still, turned), 0.03f);
    }

    [Test]
    public void AShotBurstsTheRotaryBarrelWideApart()
    {
        Layout.Shape gun = MachineGun;
        Vector3 muzzle = gun.Muzzle;
        Vector3 blast = Layout.Arrange(Layout.Form.Orbit, 0, gun, Firing).position;
        float fromAxis = new Vector2(blast.x - muzzle.x, blast.y - muzzle.y).magnitude;
        Assert.Greater(fromAxis, Layout.RotaryRadius(gun) * 2f);
    }

    [Test]
    public void TheShotgunClawBloomsOnEachShot()
    {
        Layout.Shape gun = Shotgun;
        Vector3 muzzle = gun.Muzzle;
        for (int i = 0; i < Layout.ClawPetals; i++)
        {
            Vector3 rest = Layout.Arrange(Layout.Form.Fan, i, gun, Rest).position;
            Vector3 blast = Layout.Arrange(Layout.Form.Fan, i, gun, Firing).position;
            float restOut = new Vector2(rest.x - muzzle.x, rest.y - muzzle.y).magnitude;
            float blastOut = new Vector2(blast.x - muzzle.x, blast.y - muzzle.y).magnitude;
            Assert.Greater(blastOut, restOut + 0.02f, "petal " + i);
        }
    }

    [Test]
    public void TheShotgunPumpRacksBack()
    {
        for (int i = Layout.ClawPetals; i < Layout.Count; i++)
        {
            float rest = Layout.Arrange(Layout.Form.Fan, i, Shotgun, Rest).position.z;
            float racked = Layout.Arrange(Layout.Form.Fan, i, Shotgun, At(0.35f)).position.z;
            Assert.Less(racked, rest - 0.1f, "plate " + i);
        }
    }

    [Test]
    public void ThePistolsSidePanelsRecoilAndTheRestTuckAway()
    {
        for (int i = 0; i < Layout.Count; i++)
        {
            Layout.Pose rest = Layout.Arrange(Layout.Form.Tight, i, Pistol, Rest);
            if (i >= 4) { Assert.AreEqual(Vector3.zero, rest.size, "piece " + i); continue; }
            Assert.Greater(rest.size.sqrMagnitude, 0f, "piece " + i);
        }
        for (int i = 0; i < 2; i++)
        {
            Layout.Pose rest = Layout.Arrange(Layout.Form.Tight, i, Pistol, Rest);
            Layout.Pose shot = Layout.Arrange(Layout.Form.Tight, i, Pistol, Firing);
            Assert.Less(shot.position.z, rest.position.z - 0.05f, "panel " + i + " recoils");
            // Standing off the side of the body, reaching out past the muzzle, and thrown out wide by a shot
            float restGap = Mathf.Abs(rest.position.x - Pistol.body.center.x) - Pistol.body.extents.x;
            float shotGap = Mathf.Abs(shot.position.x - Pistol.body.center.x) - Pistol.body.extents.x;
            Assert.Greater(restGap, Pistol.body.size.x * 0.2f, "panel " + i + " off the body");
            Assert.Greater(rest.position.z + rest.size.z * 0.5f, Pistol.body.max.z, "panel " + i + " past the muzzle");
            Assert.Greater(shotGap, restGap + Pistol.body.size.x * 0.4f, "panel " + i + " out wide");
        }
    }

    // Aimed down the sights, nothing may rise into the line of sight above the body (the machine
    // gun's barrel only bursts above it when fired from the hip)
    [TestCase(Layout.Form.Tight)]
    [TestCase(Layout.Form.Orbit)]
    public void NothingRisesAboveTheBody(Layout.Form form)
    {
        Layout.Shape gun = form == Layout.Form.Tight ? Pistol : MachineGun;
        Layout.Motion[] motions = form == Layout.Form.Tight
            ? new[] { Rest, Firing, At(100f, 3f, 2f, 1f) }
            : new[] { Rest, At(0.035f, 0f, 0f, 1f), At(100f, 3f, 2f, 1f) };
        foreach (Layout.Motion motion in motions)
        {
            for (int i = 0; i < Layout.Count; i++)
            {
                Assert.LessOrEqual(Layout.Top(Layout.Arrange(form, i, gun, motion)), gun.body.max.y, "piece " + i);
            }
        }
    }

    [Test]
    public void TheSniperBarrelStaysUnderTheScope()
    {
        const float scopeBottom = 0.422f - 0.005f; // the Scope Bottom part, less its thickness
        foreach (Layout.Motion motion in new[] { Rest, Firing, At(0.05f, 7f) })
        {
            for (int i = 0; i < Layout.Count; i++)
            {
                Assert.Less(Layout.Top(Layout.Arrange(Layout.Form.Rail, i, Sniper, motion)), scopeBottom, "piece " + i);
            }
        }
    }

    [Test]
    public void TheShotgunClawLeavesTheBeadClear()
    {
        // Whatever rises above the body stays well off to the side of the bead
        Layout.Shape gun = Shotgun;
        foreach (Layout.Motion motion in new[] { Rest, Firing })
        {
            for (int i = 0; i < Layout.Count; i++)
            {
                Layout.Pose pose = Layout.Arrange(Layout.Form.Fan, i, gun, motion);
                if (Layout.Top(pose) <= gun.body.max.y) continue;
                Assert.Greater(Mathf.Abs(pose.position.x - gun.body.center.x), gun.body.size.x * 0.5f, "piece " + i);
            }
        }
    }
}
