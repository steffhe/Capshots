using System.IO;
using System.Text.RegularExpressions;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

// The network settings in Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion. Fusion quietly
// falls back to its default tick rate if the one set there isn't valid, so this checks it is.
public class NetworkConfigTests
{
    private static string ConfigText => File.ReadAllText(Path.Combine(Application.dataPath, "Photon/Fusion/Resources/NetworkProjectConfig.fusion"));

    private static int Value(string text, string name)
    {
        Match match = Regex.Match(text, "\"" + name + "\":\\s*(-?\\d+)");
        Assert.IsTrue(match.Success, name + " not found in the network config");
        return int.Parse(match.Groups[1].Value);
    }

    private static int TickRateSet => Value(ConfigText, "Client");

    [Test]
    public void TickRate_IsOneFusionAccepts()
    {
        string text = ConfigText;
        var selection = new TickRate.Selection
        {
            Client = Value(text, "Client"),
            ClientSendInterval = Value(text, "ClientSendInterval"),
            ServerTickInterval = Value(text, "ServerTickInterval"),
            ServerSendInterval = Value(text, "ServerSendInterval"),
        };
        Assert.AreEqual(TickRate.ValidateResult.Ok, selection.Validate());
        Assert.AreEqual(selection.Client, TickRate.Resolve(selection).Client);
    }

    // Timers round up to whole ticks, so a fire rate between two ticks would fire slower than set
    [Test]
    public void EveryGunsFireRate_IsAWholeNumberOfTicks()
    {
        int rate = TickRateSet;
        string[] guns = Directory.GetFiles(Path.Combine(Application.dataPath, "ScriptableObjects/Guns"), "*.asset");
        Assert.IsNotEmpty(guns);
        foreach (string gun in guns)
        {
            Match match = Regex.Match(File.ReadAllText(gun), @"\bfireRate:\s*([0-9.]+)");
            Assert.IsTrue(match.Success, gun);
            float seconds = float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            float ticks = seconds * rate;
            Assert.AreEqual(Mathf.Round(ticks), ticks, 0.001f, $"{Path.GetFileName(gun)}: {seconds} s is {ticks} ticks at {rate} ticks a second");
        }
    }

    // Shots are checked against where players were on the shooter's screen, which in host mode is
    // about one round trip plus the interpolation delay ago. The hitbox history must reach that far
    // back for players with a high ping (browsers over Photon's relay), or their hits are checked
    // against the wrong positions.
    [Test]
    public void LagCompensation_ReachesBackFarEnoughForHighPing()
    {
        Assert.GreaterOrEqual(Value(ConfigText, "HitboxBufferLengthInMs"), 400);
    }
}
