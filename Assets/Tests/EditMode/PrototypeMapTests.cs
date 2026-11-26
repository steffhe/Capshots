using System.IO;
using NUnit.Framework;
using UnityEngine;

// Checks the generated Prototype map (Assets/Resources/Maps/Prototype.txt, from
// Tools/PrototypeMap/generate.py): an open arena in the same box format as the Office
public class PrototypeMapTests
{
    private const float Half = 40f;
    private static OfficeMapData data;

    private static OfficeMapData Data
    {
        get
        {
            if (data == null) data = OfficeMapData.Parse(File.ReadAllText("Assets/Resources/Maps/Prototype.txt"));
            return data;
        }
    }

    [Test]
    public void HasEightSpawnPointsAtEachEnd_AndAMapView()
    {
        Assert.AreEqual(16, Data.spawns.Count);
        Assert.AreEqual(8, Data.spawns.FindAll(s => s.position.z < 0f).Count);
        Assert.AreEqual(8, Data.spawns.FindAll(s => s.position.z > 0f).Count);
        Assert.IsTrue(Data.hasCamera);
    }

    [Test]
    public void EverySpawnPoint_StandsOnSomething_InTheClear()
    {
        foreach (OfficeMapData.Spawn spawn in Data.spawns)
        {
            Vector3 p = spawn.position;
            float feet = p.y - 1.2f;
            Assert.Less(Mathf.Abs(p.x), Half - 1f);
            Assert.Less(Mathf.Abs(p.z), Half - 1f);
            Assert.IsTrue(Data.boxes.Exists(b => b.collider && Mathf.Abs(b.Bounds.max.y - feet) < 0.01f
                && b.Bounds.min.x <= p.x && p.x <= b.Bounds.max.x && b.Bounds.min.z <= p.z && p.z <= b.Bounds.max.z), "spawn at " + p + " is standing on nothing");
            // The player: 2 m tall, 0.5 m round
            var player = new Bounds(new Vector3(p.x, feet + 1.05f, p.z), new Vector3(1f, 1.9f, 1f));
            Assert.IsFalse(Data.boxes.Exists(b => b.collider && b.Bounds.Intersects(player)), "spawn at " + p + " is blocked");
        }
    }

    [Test]
    public void BothEnds_AreTheSame_TurnedRound()
    {
        // Every box has a twin turned 180 degrees about the middle (the floor and outer walls are
        // their own), so both teams get the same cover at the same distances
        foreach (OfficeMapData.Box box in Data.boxes)
        {
            Vector3 turned = new Vector3(-box.center.x, box.center.y, -box.center.z);
            Assert.IsTrue(Data.boxes.Exists(b => (b.center - turned).sqrMagnitude < 1e-4f && (b.size - box.size).sqrMagnitude < 1e-4f), "no twin for the box at " + box.center);
        }
        foreach (OfficeMapData.Spawn spawn in Data.spawns)
        {
            Vector3 turned = new Vector3(-spawn.position.x, spawn.position.y, -spawn.position.z);
            Assert.IsTrue(Data.spawns.Exists(s => (s.position - turned).sqrMagnitude < 1e-4f), "no twin for the spawn at " + spawn.position);
        }
    }

    [Test]
    public void Steps_AreLowEnoughToWalkUp()
    {
        // No block standing on the floor is an awkward height: too high to step onto (0.3 m) but
        // lower than cover (1 m). Stairs are built of thin steps, each at most 0.25 m above the last.
        foreach (OfficeMapData.Box box in Data.boxes)
        {
            if (!box.collider || box.Bounds.min.y > 0.01f) continue;
            float top = box.Bounds.max.y;
            if (box.size.x < 0.9f || box.size.z < 0.9f) continue; // a thin wall or kerb
            Assert.IsFalse(top > 0.3f && top < 1f, "a step " + top + " m high at " + box.center);
        }
    }

    [Test]
    public void EveryBox_IsInsideTheMap()
    {
        foreach (OfficeMapData.Box box in Data.boxes)
        {
            Assert.LessOrEqual(box.Bounds.max.x, Half + 1.001f);
            Assert.GreaterOrEqual(box.Bounds.min.x, -Half - 1.001f);
            Assert.LessOrEqual(box.Bounds.max.z, Half + 1.001f);
            Assert.GreaterOrEqual(box.Bounds.min.z, -Half - 1.001f);
        }
    }
}
