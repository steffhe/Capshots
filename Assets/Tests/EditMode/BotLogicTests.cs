using NUnit.Framework;
using UnityEngine;

public class BotLogicTests
{
    #region Room filling

    [TestCase(0, 1, 8, 0)]
    [TestCase(3, 1, 8, 3)]
    [TestCase(3, 6, 8, 2)]
    [TestCase(3, 8, 8, 0)]
    [TestCase(5, 1, 4, 3)]
    [TestCase(3, 0, 8, 3)]
    [TestCase(3, 1, 0, 0)]
    [TestCase(-2, 1, 8, 0)]
    public void DesiredBotCount_LeavesRoomForPeople(int setting, int humans, int maxPlayers, int expected)
    {
        Assert.AreEqual(expected, BotLogic.DesiredBotCount(setting, humans, maxPlayers));
    }

    [Test]
    public void BotName_IsMarkedAndUniqueForAWholeRoom()
    {
        var names = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < 40; i++)
        {
            string name = BotLogic.BotName(i);
            StringAssert.StartsWith("BOT ", name);
            Assert.LessOrEqual(name.Length, 20, "Usernames are cut to 20 characters");
            Assert.IsTrue(names.Add(name), "Duplicate name " + name);
        }
    }

    #endregion

    #region Gun forms

    private static Gun MakeGun(int burst, int pellets = 1, float scopeFov = 0f)
    {
        Gun gun = ScriptableObject.CreateInstance<Gun>();
        gun.burst = burst;
        gun.pellets = pellets;
        gun.scopeFov = scopeFov;
        return gun;
    }

    // The game's loadout order: shotgun, machine gun, pistol, sniper rifle
    private static Gun[] Loadout()
    {
        return new[] { MakeGun(0, 8), MakeGun(1), MakeGun(0), MakeGun(0, 1, 20f) };
    }

    [TestCase(3f, 0)]
    [TestCase(20f, 1)]
    [TestCase(80f, 3)]
    [TestCase(-1f, 1)]
    public void ChooseGun_PicksTheFormForTheDistance(float distance, int expected)
    {
        Assert.AreEqual(expected, BotLogic.ChooseGun(Loadout(), distance));
    }

    [Test]
    public void Personalities_TakeTurnsBySlot()
    {
        Assert.AreEqual(BotLogic.Personality.Balanced, BotLogic.PersonalityFor(0));
        Assert.AreEqual(BotLogic.Personality.Rusher, BotLogic.PersonalityFor(1));
        Assert.AreEqual(BotLogic.Personality.Sniper, BotLogic.PersonalityFor(2));
        Assert.AreEqual(BotLogic.Personality.Grenadier, BotLogic.PersonalityFor(3));
        Assert.AreEqual(BotLogic.Personality.Balanced, BotLogic.PersonalityFor(4));
        Assert.AreEqual(BotLogic.Personality.Grenadier, BotLogic.PersonalityFor(-1));
    }

    [Test]
    public void Personalities_PickTheirOwnGunForms()
    {
        BotLogic.Style rusher = BotLogic.StyleFor(BotLogic.Personality.Rusher);
        BotLogic.Style sniper = BotLogic.StyleFor(BotLogic.Personality.Sniper);
        // At 10 m a rusher still wants the shotgun (further out than a balanced bot), and never the
        // scoped form
        Assert.AreEqual(0, BotLogic.ChooseGun(Loadout(), 10f, rusher.shotgunRange, rusher.sniperRange));
        Assert.Greater(rusher.shotgunRange, BotLogic.ShotgunRange);
        Assert.AreEqual(1, BotLogic.ChooseGun(Loadout(), 80f, rusher.shotgunRange, rusher.sniperRange));
        // A sniper scopes in from 15 m
        Assert.AreEqual(3, BotLogic.ChooseGun(Loadout(), 20f, sniper.shotgunRange, sniper.sniperRange));
        Assert.IsFalse(rusher.retreats);
        Assert.Less(BotLogic.StyleFor(BotLogic.Personality.Grenadier).grenadeIntervalMax, BotLogic.StyleFor(BotLogic.Personality.Balanced).grenadeIntervalMin);
    }

    [Test]
    public void Balanced_IsHowBotsPlayedBefore()
    {
        BotLogic.Style balanced = BotLogic.StyleFor(BotLogic.Personality.Balanced);
        Assert.AreEqual(BotLogic.ShotgunRange, balanced.shotgunRange);
        Assert.AreEqual(BotLogic.SniperRange, balanced.sniperRange);
        Assert.AreEqual(1f, balanced.distanceScale);
        Assert.IsTrue(balanced.retreats);
    }

    [Test]
    public void ChooseGun_FallsBackWhenAFormIsMissing()
    {
        Gun[] pistolOnly = { MakeGun(0) };
        Assert.AreEqual(0, BotLogic.ChooseGun(pistolOnly, 3f));
        Assert.AreEqual(0, BotLogic.ChooseGun(pistolOnly, 80f));

        Gun[] noAutomatic = { MakeGun(0, 8), MakeGun(0) };
        Assert.AreEqual(1, BotLogic.ChooseGun(noAutomatic, 20f));

        Assert.AreEqual(0, BotLogic.ChooseGun(new Gun[0], 20f));
        Assert.AreEqual(0, BotLogic.ChooseGun(null, 20f));
    }

    #endregion

    #region Aiming and steering

    [Test]
    public void LookAngles_MatchTheGamesYawAndPitch()
    {
        Vector2 ahead = BotLogic.LookAngles(Vector3.zero, new Vector3(0f, 0f, 10f));
        Assert.AreEqual(0f, ahead.x, 1e-3f);
        Assert.AreEqual(0f, ahead.y, 1e-3f);

        Vector2 right = BotLogic.LookAngles(Vector3.zero, new Vector3(10f, 0f, 0f));
        Assert.AreEqual(90f, right.x, 1e-3f);

        // Positive pitch looks up
        Vector2 up = BotLogic.LookAngles(Vector3.zero, new Vector3(0f, 10f, 10f));
        Assert.AreEqual(45f, up.y, 1e-3f);

        // The direction those angles give, the way Weapon builds a shot, points back at the target
        Vector3 target = new Vector3(-4f, 2f, 7f);
        Vector2 angles = BotLogic.LookAngles(Vector3.zero, target);
        Vector3 shot = Quaternion.Euler(-angles.y, angles.x, 0f) * Vector3.forward;
        Assert.Less(Vector3.Angle(shot, target), 0.01f);
    }

    [Test]
    public void TurnTowards_TakesTheShortWayAndIsCappedBySpeed()
    {
        // From 170 to -170 is 20 degrees through 180, not 340 the long way
        float turned = BotLogic.TurnTowards(170f, -170f, 0.1f);
        Assert.Greater(Mathf.DeltaAngle(170f, turned), 0f);

        float step = Mathf.Abs(Mathf.DeltaAngle(0f, BotLogic.TurnTowards(0f, 179f, 0.016f)));
        Assert.LessOrEqual(step, BotLogic.MaxTurnSpeed * 0.016f + 1e-3f);
    }

    [Test]
    public void TurnTowards_SettlesOnTheTarget()
    {
        float angle = 0f;
        for (int i = 0; i < 120; i++) angle = BotLogic.TurnTowards(angle, 60f, 1f / 60f);
        Assert.AreEqual(60f, angle, 0.01f);
    }

    [Test]
    public void AimError_ShrinksWhileTracking()
    {
        Assert.AreEqual(BotLogic.AimErrorStart, BotLogic.AimError(0f), 1e-4f);
        Assert.AreEqual(BotLogic.AimErrorSettled, BotLogic.AimError(10f), 1e-4f);
        Assert.Less(BotLogic.AimError(0.5f), BotLogic.AimError(0.1f));
    }

    [Test]
    public void OnTarget_IsMoreForgivingUpClose()
    {
        Assert.IsTrue(BotLogic.OnTarget(1f, 0.5f, 50f));
        Assert.IsFalse(BotLogic.OnTarget(5f, 0f, 50f));
        Assert.IsTrue(BotLogic.OnTarget(10f, 0f, 2f));
    }

    [Test]
    public void MoveInput_IsRelativeToFacing()
    {
        // Facing north (+z) and wanting to go north: straight forward
        Vector2 forward = BotLogic.MoveInput(0f, Vector3.forward);
        Assert.AreEqual(0f, forward.x, 1e-4f);
        Assert.AreEqual(1f, forward.y, 1e-4f);

        // Facing east (+x) and wanting to go north: that's to the left
        Vector2 left = BotLogic.MoveInput(90f, Vector3.forward);
        Assert.AreEqual(-1f, left.x, 1e-4f);
        Assert.AreEqual(0f, left.y, 1e-4f);

        // Turned back into the world the way Player does it, the input walks the wanted way
        Vector3 wanted = new Vector3(3f, 5f, -2f);
        Vector2 move = BotLogic.MoveInput(37f, wanted);
        Vector3 walked = Quaternion.Euler(0f, 37f, 0f) * new Vector3(move.x, 0f, move.y);
        Assert.Less(Vector3.Angle(walked, new Vector3(wanted.x, 0f, wanted.z)), 0.01f);

        Assert.AreEqual(Vector2.zero, BotLogic.MoveInput(0f, Vector3.up));
    }

    #endregion
}
