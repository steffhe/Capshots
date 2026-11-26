using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SurfacePrintTests
{
    private const float Step = 0.03f;

    private static IEnumerable<TestCaseData> Outlines()
    {
        yield return new TestCaseData(SurfacePrint.Ellipse(0.3f, 0.2f)).SetName("Ellipse");
        yield return new TestCaseData(SurfacePrint.Bar(0.06f, 0.3f)).SetName("Bar");
        yield return new TestCaseData(SurfacePrint.Box(0.36f, 0.14f)).SetName("Box");
        yield return new TestCaseData(SurfacePrint.Turned(SurfacePrint.Box(0.1f, 0.1f), 0.2f, -0.1f, 45f)).SetName("TurnedBox");
        yield return new TestCaseData(SurfacePrint.Star(5, 0.065f, 0.027f)).SetName("Star");
    }

    private static float Area(Vector2 a, Vector2 b, Vector2 c) => ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) * 0.5f;

    [TestCaseSource(nameof(Outlines))]
    public void Fill_TrianglesGoAnticlockwise(List<Vector2> outline)
    {
        SurfacePrint.Fill(outline, Step, out Vector2[] points, out int[] triangles);
        Assert.Greater(triangles.Length, 0);
        for (int t = 0; t < triangles.Length; t += 3)
        {
            Assert.GreaterOrEqual(Area(points[triangles[t]], points[triangles[t + 1]], points[triangles[t + 2]]), -1e-7f, "triangle " + t / 3);
        }
    }

    [TestCaseSource(nameof(Outlines))]
    public void Fill_CoversTheOutline(List<Vector2> outline)
    {
        SurfacePrint.Fill(outline, Step, out Vector2[] points, out int[] triangles);
        float outlineArea = 0f, filledArea = 0f;
        for (int k = 0; k < outline.Count; k++) outlineArea += Area(Vector2.zero, outline[k], outline[(k + 1) % outline.Count]);
        for (int t = 0; t < triangles.Length; t += 3) filledArea += Area(points[triangles[t]], points[triangles[t + 1]], points[triangles[t + 2]]);
        Assert.AreEqual(outlineArea, filledArea, outlineArea * 0.001f);
    }

    [TestCaseSource(nameof(Outlines))]
    public void Fill_KeepsItsPointsClose_SoTheMeshCanBend(List<Vector2> outline)
    {
        SurfacePrint.Fill(outline, Step, out Vector2[] points, out int[] triangles);
        // Along a ring, or out to the next one, at most a step; so a triangle's slanting side (from
        // one ring to the next point along the other) is at most two
        for (int t = 0; t < triangles.Length; t += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                Vector2 a = points[triangles[t + k]], b = points[triangles[t + (k + 1) % 3]];
                Assert.LessOrEqual(Vector2.Distance(a, b), Step * 2f);
            }
        }
    }

    [Test]
    public void Star_AlternatesBetweenItsPointsAndTheGapsBetween()
    {
        List<Vector2> star = SurfacePrint.Star(4, 0.1f, 0.03f);
        Assert.AreEqual(8, star.Count);
        for (int k = 0; k < star.Count; k++) Assert.AreEqual(k % 2 == 0 ? 0.1f : 0.03f, star[k].magnitude, 1e-5f);
        // The first point is straight up
        Assert.AreEqual(0.1f, star[0].y, 1e-5f);
    }

    // A unit sphere round the origin, printed on at its front
    private static SurfacePrint.Map Sphere(bool mirrored) => SurfacePrint.Ellipsoid(Vector3.zero, Vector3.one, Vector3.forward, mirrored ? Vector3.left : Vector3.right, Vector3.up);

    [TestCase(false)]
    [TestCase(true)]
    public void Build_LiesJustOffTheSurface_FacingOut(bool mirrored)
    {
        const float lift = 0.004f;
        Mesh mesh = SurfacePrint.Build("Test", SurfacePrint.Ellipse(0.6f, 0.4f), Sphere(mirrored), lift, Step, 1f, out Vector3 at);
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        foreach (Vector3 v in vertices) Assert.AreEqual(1f + lift, (v + at).magnitude, 1e-4f);
        // Unity draws the side of a triangle its corners go clockwise round: seen from outside, the
        // cross of its edges points out at the viewer
        for (int t = 0; t < triangles.Length; t += 3)
        {
            Vector3 a = vertices[triangles[t]] + at, b = vertices[triangles[t + 1]] + at, c = vertices[triangles[t + 2]] + at;
            Assert.GreaterOrEqual(Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f), -1e-9f, "triangle " + t / 3);
        }
        // Its middle stays at the print's position, which a scaling part grows round
        Assert.AreEqual(0f, vertices[0].magnitude, 1e-5f);
    }

    [Test]
    public void Build_OnAnEllipsoid_FollowsItsCurve()
    {
        Vector3 radii = new Vector3(1.55f, 1f, 1.55f);
        Mesh mesh = SurfacePrint.Build("Test", SurfacePrint.Ellipse(0.6f, 0.6f), SurfacePrint.Ellipsoid(Vector3.zero, radii, Vector3.up, Vector3.right, Vector3.back), 0f, Step, 1f, out Vector3 at);
        foreach (Vector3 v in mesh.vertices)
        {
            Vector3 p = v + at;
            Assert.AreEqual(1f, new Vector3(p.x / radii.x, p.y / radii.y, p.z / radii.z).magnitude, 1e-4f);
        }
    }

    [Test]
    public void Frame_LiesAlongTheSurface_UpAsNearUpAsItCanBe()
    {
        SurfacePrint.Frame(Vector3.forward, out Vector3 across, out Vector3 up);
        Assert.AreEqual(0f, Vector3.Dot(across, Vector3.forward), 1e-5f);
        Assert.AreEqual(1f, Vector3.Dot(up, Vector3.up), 1e-5f);
        // Straight up there's no up along the surface: it still gives a frame
        SurfacePrint.Frame(Vector3.up, out across, out up);
        Assert.AreEqual(1f, across.magnitude, 1e-5f);
        Assert.AreEqual(0f, Vector3.Dot(across, up), 1e-5f);
    }
}
