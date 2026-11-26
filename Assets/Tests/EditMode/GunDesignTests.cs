using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GunDesignTests
{
    private static IEnumerable<GunSatelliteLayout.Form> Forms => GunDesign.Forms;

    [Test]
    public void EveryFormHasTheSamePartsInTheSameOrder()
    {
        GunDesign.Part[] first = GunDesign.For(GunSatelliteLayout.Form.Tight);
        foreach (GunSatelliteLayout.Form form in Forms)
        {
            GunDesign.Part[] parts = GunDesign.For(form);
            Assert.AreEqual(first.Length, parts.Length, form.ToString());
            for (int i = 0; i < parts.Length; i++)
            {
                Assert.AreEqual(first[i].name, parts[i].name, form.ToString());
                Assert.AreEqual(first[i].shape, parts[i].shape, parts[i].name);
                Assert.AreEqual(first[i].finish, parts[i].finish, parts[i].name);
                Assert.AreEqual(first[i].motion, parts[i].motion, parts[i].name);
            }
        }
    }

    [Test]
    public void PartNamesAreUnique()
    {
        var names = new HashSet<string>();
        foreach (GunDesign.Part p in GunDesign.For(GunSatelliteLayout.Form.Orbit)) Assert.IsTrue(names.Add(p.name), p.name);
    }

    [Test]
    public void PartNamesDontClashWithTheSights()
    {
        // GunMorph matches parts by name across forms, and the sights stay in the prefabs. (The
        // measuring blocks Body, Grip, Under and Barrel are found as Design's direct children, and the
        // design's parts are under Design/Model, so sharing the name Grip is fine.)
        string[] sights = { "Rib", "Bead", "Front Post", "Front Dot", "Rear Base", "Rear Riser", "Reticle", "Lens Front", "Lens Rear", "Scope Top", "Scope Bottom" };
        foreach (GunDesign.Part p in GunDesign.For(GunSatelliteLayout.Form.Tight))
        {
            foreach (string name in sights) Assert.AreNotEqual(name, p.name);
            Assert.AreNotEqual("Body", p.name);
            Assert.AreNotEqual("Barrel", p.name);
            Assert.AreNotEqual("Under", p.name);
        }
    }

    [Test]
    public void TheSharedCoreIsTheSameInEveryForm()
    {
        GunDesign.Part[] first = GunDesign.For(GunSatelliteLayout.Form.Tight);
        int shared = 0;
        foreach (GunSatelliteLayout.Form form in Forms)
        {
            GunDesign.Part[] parts = GunDesign.For(form);
            for (int i = 0; i < parts.Length; i++)
            {
                Assert.AreEqual(first[i].shared, parts[i].shared, parts[i].name);
                if (!parts[i].shared) continue;
                Assert.AreEqual(first[i].position, parts[i].position, parts[i].name);
                Assert.AreEqual(first[i].size, parts[i].size, parts[i].name);
                Assert.AreEqual(first[i].rotation, parts[i].rotation, parts[i].name);
                if (form == GunSatelliteLayout.Form.Tight) shared++;
            }
        }
        Assert.Greater(shared, 10);
        foreach (string name in new[] { "Core", "Grip", "Receiver Rear", "Receiver Front", "Rear Housing" })
        {
            Assert.IsTrue(GunDesign.Find(GunSatelliteLayout.Form.Fan, name).shared, name);
        }
    }

    [Test]
    public void TheCoreSitsInTheReceiversWindow()
    {
        GunDesign.Part core = GunDesign.Find(GunSatelliteLayout.Form.Orbit, "Core");
        GunDesign.Part spine = GunDesign.Find(GunSatelliteLayout.Form.Orbit, "Receiver Spine");
        GunDesign.Part keel = GunDesign.Find(GunSatelliteLayout.Form.Orbit, "Receiver Keel");
        Assert.AreEqual(GunDesign.CorePosition, core.position);
        Assert.LessOrEqual(core.position.y + core.size.y / 2f, spine.position.y - spine.size.y / 2f);
        Assert.GreaterOrEqual(core.position.y - core.size.y / 2f, keel.position.y + keel.size.y / 2f);
        foreach (string ring in new[] { "Core Ring A", "Core Ring B" })
        {
            GunDesign.Part r = GunDesign.Find(GunSatelliteLayout.Form.Orbit, ring);
            Assert.LessOrEqual(r.position.y + r.size.y / 2f, spine.position.y - spine.size.y / 2f, ring);
            Assert.GreaterOrEqual(r.position.y - r.size.y / 2f, keel.position.y + keel.size.y / 2f, ring);
        }
    }

    [Test]
    public void NothingReachesAboveTheSights()
    {
        foreach (GunSatelliteLayout.Form form in Forms)
        {
            foreach (GunDesign.Part p in GunDesign.For(form))
            {
                if (p.Collapsed) continue;
                float top = Top(p);
                // The shotgun's taller shroud reaches its rib, the sniper's mounts its scope
                float limit = form == GunSatelliteLayout.Form.Fan ? 0.4185f
                    : p.name.StartsWith("Scope Mount") ? 0.4225f : GunDesign.Top + 0.0015f;
                Assert.LessOrEqual(top, limit, form + " " + p.name);
            }
        }
    }

    [Test]
    public void EachFormHasItsOwnSilhouette()
    {
        // The front reaches further for the longer guns: pistol, shotgun, machine gun, sniper rifle
        float pistol = Front(GunSatelliteLayout.Form.Tight), shotgun = Front(GunSatelliteLayout.Form.Fan);
        float machineGun = Front(GunSatelliteLayout.Form.Orbit), sniper = Front(GunSatelliteLayout.Form.Rail);
        Assert.Less(pistol, shotgun);
        Assert.Less(shotgun, machineGun);
        Assert.Less(machineGun, sniper);
        // Only the long guns have a stock, only the sniper scope mounts, the pistol no foregrip
        Assert.IsTrue(GunDesign.Find(GunSatelliteLayout.Form.Tight, "Stock").Collapsed);
        Assert.IsTrue(GunDesign.Find(GunSatelliteLayout.Form.Fan, "Stock").Collapsed);
        Assert.IsFalse(GunDesign.Find(GunSatelliteLayout.Form.Rail, "Stock").Collapsed);
        Assert.IsFalse(GunDesign.Find(GunSatelliteLayout.Form.Rail, "Scope Mount Front").Collapsed);
        Assert.IsTrue(GunDesign.Find(GunSatelliteLayout.Form.Orbit, "Scope Mount Front").Collapsed);
        Assert.IsTrue(GunDesign.Find(GunSatelliteLayout.Form.Tight, "Foregrip").Collapsed);
    }

    [Test]
    public void FloatingPartsComeLooseInTheReleaseStage()
    {
        foreach (GunSatelliteLayout.Form form in Forms)
        {
            foreach (GunDesign.Part p in GunDesign.For(form))
            {
                Vector3 release = GunDesign.ReleaseOffset(p);
                if (p.name == "Receiver Rear" || p.name == "Grip" || p.name == "Core") Assert.AreEqual(Vector3.zero, release, p.name);
                if (p.motion == GunDesign.Motion.SideModule) Assert.AreEqual(Mathf.Sign(p.side), Mathf.Sign(release.x), p.name);
                if (p.name.StartsWith("Core Clamp")) Assert.Greater(release.magnitude, 0f, p.name);
            }
        }
    }

    [Test]
    public void ThereAreEnoughGlowingAndFloatingParts()
    {
        int glowing = 0, floating = 0;
        foreach (GunDesign.Part p in GunDesign.For(GunSatelliteLayout.Form.Orbit))
        {
            if (p.Collapsed) continue;
            if (p.finish == GunDesign.Finish.Glow) glowing++;
            if (p.motion == GunDesign.Motion.CoreRing || p.motion == GunDesign.Motion.SideModule ||
                p.motion == GunDesign.Motion.Muzzle || p.motion == GunDesign.Motion.Stock) floating++;
        }
        Assert.GreaterOrEqual(glowing, 12);
        Assert.GreaterOrEqual(floating, 10);
    }

    private static float Front(GunSatelliteLayout.Form form)
    {
        float front = float.MinValue;
        foreach (GunDesign.Part p in GunDesign.For(form)) if (!p.Collapsed) front = Mathf.Max(front, p.position.z + p.size.z / 2f);
        return front;
    }

    private static float Top(GunDesign.Part p)
    {
        Quaternion rotation = Quaternion.Euler(p.rotation);
        float top = float.MinValue;
        for (int c = 0; c < 8; c++)
        {
            Vector3 corner = Vector3.Scale(p.size * 0.5f, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
            top = Mathf.Max(top, (p.position + rotation * corner).y);
        }
        return top;
    }
}
