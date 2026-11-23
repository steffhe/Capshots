using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

// Checks the generated Office map (Assets/Resources/Maps/Office.txt, from Tools/OfficeMap/generate.py)
public class OfficeMapTests
{
    private static OfficeMapData data;

    private static OfficeMapData Data
    {
        get
        {
            if (data == null) data = OfficeMapData.Parse(File.ReadAllText("Assets/Resources/Maps/Office.txt"));
            return data;
        }
    }

    [Test]
    public void Parse_ReadsEveryLineType()
    {
        const string text = "# comment\n" +
            "m wood 0.5 0.4 0.3 1 wood 2 0.4 opaque\n" +
            "m glass 0.8 0.9 1 0.2 none 1 0.9 glass\n" +
            "b wood 1 -0.2 3 10 0.4 8 -\n" +
            "b glass 0 1.5 0 2 3 0.03 gs\n" +
            "b - 0 0.4 0 1 0.8 1 o\n" +
            "b wood 0 1 0 1 1 1 n\n" +
            "p 2 1.54 5 1.4 2.92 0.03 1.35 0.8\n" +
            "s 1.5 1.2 -2 90\n" +
            "l 0 2.75 1 180 0.3 0.1 0.1 0.1 H  Bakrommet\n" +
            "c 1 2 3 10 45\n";
        OfficeMapData d = OfficeMapData.Parse(text);

        Assert.AreEqual(2, d.materials.Count);
        Assert.AreEqual(OfficeMapData.MaterialKind.Glass, d.materials[1].kind);
        Assert.AreEqual(4, d.boxes.Count);
        Assert.AreEqual(new Vector3(10f, 0.4f, 8f), d.boxes[0].size);
        Assert.IsTrue(d.boxes[0].collider && d.boxes[0].castShadows && !d.boxes[0].glass);
        Assert.IsTrue(d.boxes[1].glass && d.boxes[1].collider && !d.boxes[1].castShadows);
        Assert.AreEqual(-1, d.boxes[2].material, "collider-only boxes aren't drawn");
        Assert.IsTrue(d.boxes[2].collider);
        Assert.IsFalse(d.boxes[3].collider);
        Assert.AreEqual(1, d.panes.Count);
        Assert.AreEqual(new Vector3(1.4f, 2.92f, 0.03f), d.panes[0].size);
        Assert.AreEqual(1.35f, d.panes[0].frostY, 1e-5f);
        Assert.AreEqual(0.8f, d.panes[0].frostHeight, 1e-5f);
        Assert.AreEqual(90f, d.spawns[0].yaw);
        Assert.AreEqual("H Bakrommet", d.signs[0].text);
        Assert.IsTrue(d.hasCamera);
        Assert.AreEqual(45f, d.cameraYaw);
    }

    [Test]
    public void Parse_UnknownMaterial_Throws()
    {
        Assert.Throws<System.FormatException>(() => OfficeMapData.Parse("b nothing 0 0 0 1 1 1 -\n"));
    }

    [Test]
    public void GeneratedMap_HasEnoughOfEverything()
    {
        Assert.Greater(Data.boxes.Count, 1000);
        Assert.Greater(Data.signs.Count, 50);
        Assert.GreaterOrEqual(Data.spawns.Count, 8);
        Assert.IsTrue(Data.hasCamera);
        Assert.IsTrue(Data.boxes.All(b => b.size.x > 0f && b.size.y > 0f && b.size.z > 0f));
        Assert.IsTrue(Data.boxes.Any(b => b.glass), "outside windows have glass colliders");
    }

    [Test]
    public void GeneratedMap_GlassPanesFitTheNetworkedState()
    {
        Assert.Greater(Data.panes.Count, 100);
        Assert.LessOrEqual(Data.panes.Count, OfficeMapData.MaxPanes, "the Manager keeps the state of at most MaxPanes panes");
        foreach (OfficeMapData.Pane pane in Data.panes)
        {
            // Thin, upright, and standing on the floor or above a door
            Assert.Less(Mathf.Min(pane.size.x, pane.size.z), 0.1f);
            Assert.Greater(Mathf.Max(pane.size.x, pane.size.z), 0.05f);
            Assert.GreaterOrEqual(pane.Bounds.min.y, 0f);
            Assert.LessOrEqual(pane.Bounds.max.y, 3.2f);
            if (pane.frostHeight > 0f)
            {
                Assert.GreaterOrEqual(pane.frostY - pane.frostHeight / 2f, pane.Bounds.min.y);
                Assert.LessOrEqual(pane.frostY + pane.frostHeight / 2f, pane.Bounds.max.y);
            }
        }
    }

    [Test]
    public void GeneratedMap_SpawnsStandOnTheFloorInOpenSpace()
    {
        foreach (OfficeMapData.Spawn spawn in Data.spawns)
        {
            // Something solid right under the spawn point
            bool floor = Data.boxes.Any(b => b.collider && !b.glass && Contains2D(b.Bounds, spawn.position, 0f)
                && b.Bounds.max.y <= spawn.position.y && b.Bounds.max.y > spawn.position.y - 1.5f);
            Assert.IsTrue(floor, "no floor under the spawn at " + spawn.position);

            // Room for the player (1 m wide, 2 m tall) around it
            var body = new Bounds(new Vector3(spawn.position.x, 1.1f, spawn.position.z), new Vector3(1.1f, 1.9f, 1.1f));
            OfficeMapData.Box[] blocking = Data.boxes.Where(b => b.collider && b.Bounds.Intersects(body)).ToArray();
            Assert.IsEmpty(blocking, "the spawn at " + spawn.position + " is blocked");
            Assert.IsFalse(Data.panes.Any(p => p.Bounds.Intersects(body)), "the spawn at " + spawn.position + " is in a glass pane");
        }
    }

    private static bool Contains2D(Bounds b, Vector3 p, float pad)
    {
        return p.x >= b.min.x - pad && p.x <= b.max.x + pad && p.z >= b.min.z - pad && p.z <= b.max.z + pad;
    }
}
