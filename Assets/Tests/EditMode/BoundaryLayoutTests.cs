using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class BoundaryLayoutTests
{
    private const float Eps = 0.001f;

    // Downtown's boundary colliders and floor, as in the scene
    private static readonly Bounds DowntownNorth = new Bounds(new Vector3(-1f, 19f, 72.5f), new Vector3(68f, 40f, 1f));
    private static readonly Bounds DowntownSouth = new Bounds(new Vector3(-1f, 19f, -22.5f), new Vector3(68f, 40f, 1f));
    private static readonly Bounds DowntownEast = new Bounds(new Vector3(32.5f, 19f, 25f), new Vector3(1f, 40f, 96f));
    private static readonly Bounds DowntownWest = new Bounds(new Vector3(-34.5f, 19f, 25f), new Vector3(1f, 40f, 96f));
    private static readonly Bounds DowntownFloor = new Bounds(new Vector3(-1f, -1.05f, 25f), new Vector3(66f, 2f, 94f));

    // Test's outer wall cubes, which overlap at the corners
    private static readonly Bounds TestSouth = new Bounds(new Vector3(-19.6f, -29.1f, -36.7f), new Vector3(80.75722f, 32.48928f, 1f));
    private static readonly Bounds TestNorth = new Bounds(new Vector3(-19.2f, -29.1f, 39.7f), new Vector3(80.34826f, 32.48928f, 1f));
    private static readonly Bounds TestEast = new Bounds(new Vector3(21.1f, -29.1f, 1.2f), new Vector3(1f, 32.48928f, 76.10599f));
    private static readonly Bounds TestWest = new Bounds(new Vector3(-58f, -29.1f, 0.61f), new Vector3(1f, 32.48928f, 79.05696f));

    private static BoundaryPanel PanelFacing(List<BoundaryPanel> panels, Vector3 inward)
    {
        return panels.Single(p => p.inward == inward);
    }

    [Test]
    public void IsWall_AcceptsThinTallBoxes_RejectsFloorsAndCrates()
    {
        Assert.IsTrue(BoundaryLayout.IsWall(DowntownNorth));
        Assert.IsTrue(BoundaryLayout.IsWall(DowntownEast));
        Assert.IsFalse(BoundaryLayout.IsWall(DowntownFloor));
        Assert.IsFalse(BoundaryLayout.IsWall(new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f))));
        Assert.IsFalse(BoundaryLayout.IsWall(new Bounds(Vector3.zero, new Vector3(10f, 1f, 0.5f))));
    }

    [Test]
    public void Build_Downtown_OnePanelPerWallOnTheInsideFace()
    {
        var panels = BoundaryLayout.Build(new[] { DowntownNorth, DowntownSouth, DowntownEast, DowntownWest, DowntownFloor });

        Assert.AreEqual(4, panels.Count, "the floor is not a wall");

        BoundaryPanel north = PanelFacing(panels, Vector3.back);
        Assert.AreEqual(72f - BoundaryLayout.FaceOffset, north.start.z, Eps);
        Assert.AreEqual(39f, north.top, Eps); // centre 19 + half height 20
        Assert.IsTrue(north.alongX);

        BoundaryPanel west = PanelFacing(panels, Vector3.right);
        Assert.AreEqual(-34f + BoundaryLayout.FaceOffset, west.start.x, Eps);
        Assert.IsFalse(west.alongX);

        Assert.AreEqual(-22f + BoundaryLayout.FaceOffset, PanelFacing(panels, Vector3.forward).start.z, Eps);
        Assert.AreEqual(32f - BoundaryLayout.FaceOffset, PanelFacing(panels, Vector3.left).start.x, Eps);
    }

    [Test]
    public void Build_TrimsPanelsAtTheNeighbouringWalls()
    {
        var panels = BoundaryLayout.Build(new[] { TestSouth, TestNorth, TestEast, TestWest });
        Assert.AreEqual(4, panels.Count);

        // The south cube runs from x -59.98 to 20.78, past both side walls; its panel stops at their inner faces
        BoundaryPanel south = PanelFacing(panels, Vector3.forward);
        Assert.AreEqual(-57.5f, Mathf.Min(south.start.x, south.end.x), Eps);
        Assert.AreEqual(20.6f, Mathf.Max(south.start.x, south.end.x), Eps);
        Assert.AreEqual(-36.2f + BoundaryLayout.FaceOffset, south.start.z, Eps);

        // The west cube runs from z -38.92 to 40.14; its panel stops at the north and south walls
        BoundaryPanel west = PanelFacing(panels, Vector3.right);
        Assert.AreEqual(-36.2f, Mathf.Min(west.start.z, west.end.z), Eps);
        Assert.AreEqual(39.2f, Mathf.Max(west.start.z, west.end.z), Eps);
    }

    [Test]
    public void Build_WithoutOpposingWalls_KeepsFullLength()
    {
        var panels = BoundaryLayout.Build(new[] { DowntownNorth });
        Assert.AreEqual(1, panels.Count);
        Assert.AreEqual(68f, panels[0].Length, Eps);
    }

    [Test]
    public void Build_NoWalls_GivesNoPanels()
    {
        Assert.AreEqual(0, BoundaryLayout.Build(new[] { DowntownFloor }).Count);
        Assert.AreEqual(0, BoundaryLayout.Build(new Bounds[0]).Count);
    }

    [Test]
    public void ClosestPoint_ClampsToThePanel()
    {
        var panel = new BoundaryPanel { start = new Vector3(0f, 0f, 10f), end = new Vector3(20f, 0f, 10f), top = 30f, inward = Vector3.back, alongX = true };

        Vector3 inFront = panel.ClosestPoint(new Vector3(5f, 2f, 4f), 0f);
        Assert.AreEqual(new Vector3(5f, 2f, 10f), inFront);

        Vector3 pastEnd = panel.ClosestPoint(new Vector3(25f, -5f, 4f), 0f);
        Assert.AreEqual(new Vector3(20f, 0f, 10f), pastEnd, "past the end and below the bottom");

        Vector3 above = panel.ClosestPoint(new Vector3(10f, 50f, 10f), 0f);
        Assert.AreEqual(new Vector3(10f, 30f, 10f), above);
    }
}
