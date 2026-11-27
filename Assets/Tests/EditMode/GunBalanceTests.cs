using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// The role each gun form plays, from its asset in Assets/ScriptableObjects/Guns, so a stat change
// that quietly takes a form's place shows up here: the shotgun owns point-blank, the machine gun
// close to mid range, the sniper rifle long range, and the pistol is the accurate all-rounder in
// between. Kill times count body shots that all land (spread isn't modelled), from the first shot.
public class GunBalanceTests
{
    private static Gun Load(string name)
    {
        Gun gun = AssetDatabase.LoadAssetAtPath<Gun>($"Assets/ScriptableObjects/Guns/{name}.asset");
        Assert.IsNotNull(gun, name + " not found");
        return gun;
    }

    // The player's health, from the player prefab (Player isn't in an assembly the tests can see)
    private static int Health
    {
        get
        {
            string text = File.ReadAllText(Path.Combine(Application.dataPath, "Resources/Player.prefab"));
            Match match = Regex.Match(text, @"\bmaxHealth:\s*(\d+)");
            Assert.IsTrue(match.Success, "maxHealth not found in the player prefab");
            return int.Parse(match.Groups[1].Value);
        }
    }

    private static int ShotDamage(Gun gun, float distance, bool headshot) => Mathf.Max(1, gun.pellets) * gun.DamageAt(distance, headshot);

    // Seconds from the first shot to the one that kills, with every shot (all its pellets) landing
    private static float KillTime(Gun gun, float distance)
    {
        int shots = Mathf.CeilToInt((float)Health / ShotDamage(gun, distance, false));
        return (shots - 1) * gun.fireRate;
    }

    [Test]
    public void Shotgun_KillsInOneBlastAtPointBlank()
    {
        Assert.GreaterOrEqual(ShotDamage(Load("Shotgun"), 2f, false), Health);
    }

    [Test]
    public void Shotgun_IsTheFastestKillUpClose()
    {
        float shotgun = KillTime(Load("Shotgun"), 3f);
        foreach (string other in new[] { "Machine Gun", "Pistol", "Sniper Rifle" })
            Assert.Less(shotgun, KillTime(Load(other), 3f), other);
    }

    [Test]
    public void MachineGun_KillsFasterThanThePistolUpClose()
    {
        Assert.Less(KillTime(Load("Machine Gun"), 10f), KillTime(Load("Pistol"), 10f));
    }

    [Test]
    public void SniperRifle_KillsWithOneHeadshotAtAnyRange()
    {
        Gun sniper = Load("Sniper Rifle");
        foreach (float distance in new[] { 0f, 50f, 150f, 500f })
            Assert.GreaterOrEqual(sniper.DamageAt(distance, true), Health, distance + " m");
    }

    [Test]
    public void SniperRifle_NeedsTwoBodyShots()
    {
        Assert.Less(Load("Sniper Rifle").DamageAt(0f, false), Health);
    }

    // The bands each form owns (see the balance pass in pull request 79): shotgun to about 8 m, machine
    // gun about 10 to 15 m, pistol about 18 to 40 m, sniper rifle from about 45 m

    [TestCase(45f)]
    [TestCase(60f)]
    [TestCase(80f)]
    public void SniperRifle_OutKillsThePistolAtLongRange(float distance)
    {
        Assert.Less(KillTime(Load("Sniper Rifle"), distance), KillTime(Load("Pistol"), distance));
    }

    [TestCase(10f)]
    [TestCase(12f)]
    [TestCase(15f)]
    public void MachineGun_IsTheFastestKillAtCloseToMidRange(float distance)
    {
        float machineGun = KillTime(Load("Machine Gun"), distance);
        foreach (string other in new[] { "Shotgun", "Pistol", "Sniper Rifle" })
            Assert.Less(machineGun, KillTime(Load(other), distance), other + " at " + distance + " m");
    }

    [TestCase(18f)]
    [TestCase(25f)]
    [TestCase(30f)]
    [TestCase(40f)]
    public void Pistol_IsTheFastestKillAtMidToLongRange(float distance)
    {
        float pistol = KillTime(Load("Pistol"), distance);
        foreach (string other in new[] { "Shotgun", "Machine Gun", "Sniper Rifle" })
            Assert.Less(pistol, KillTime(Load(other), distance), other + " at " + distance + " m");
    }

    [Test]
    public void Shotgun_KillsInOneBlastToSevenMetres()
    {
        Assert.GreaterOrEqual(ShotDamage(Load("Shotgun"), 7f, false), Health);
    }

    // Every form gets at least two body-shot kills up close out of one energy cell (100), so swapping
    // form mid-fight is a choice, not a forced cell swap
    [TestCase("Shotgun")]
    [TestCase("Machine Gun")]
    [TestCase("Pistol")]
    [TestCase("Sniper Rifle")]
    public void EveryForm_GetsTwoKillsFromOneCell(string name)
    {
        Gun gun = Load(name);
        int shotsPerKill = Mathf.CeilToInt((float)Health / ShotDamage(gun, 3f, false));
        Assert.GreaterOrEqual(GunEnergy.ShotsLeft(100f, gun.energyPerShot), shotsPerKill * 2, name);
    }
}
