using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class FaceDesignsTests
{
    private static readonly FaceMood[] Moods = { FaceMood.Normal, FaceMood.Hurt, FaceMood.Happy, FaceMood.Tired };

    private static IEnumerable<TestCaseData> Every()
    {
        for (int face = 0; face < Faces.Count; face++)
        {
            foreach (FaceMood mood in Moods) yield return new TestCaseData(face, mood).SetName(Faces.Name(face) + " " + mood);
        }
    }

    [TestCaseSource(nameof(Every))]
    public void EveryFace_HasSomethingGlowing_InEveryMood(int face, FaceMood mood)
    {
        List<FaceShape> shapes = FaceDesigns.Design(face, mood, false);
        Assert.IsTrue(shapes.Exists(s => s.ink == FaceInk.Glow));
    }

    [TestCaseSource(nameof(Every))]
    public void EveryShape_StaysOnTheScreen_EvenLookingRound(int face, FaceMood mood)
    {
        Vector3 c = FaceDesigns.ScreenCentre, radii = FaceDesigns.ScreenRadii;
        foreach (FaceShape shape in FaceDesigns.Design(face, mood, false))
        {
            if (shape.ink == FaceInk.Dark) continue; // dark on the dark screen: it can spill over
            foreach (Vector2 corner in new[] { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) })
            {
                Vector2 look = Vector2.Scale(corner, FaceDesigns.LookReach) * shape.look;
                foreach (Vector2 p in shape.outline)
                {
                    float dx = (p.x + look.x - c.x) / radii.x, dy = (p.y + look.y - c.y) / radii.y;
                    Assert.Less(dx * dx + dy * dy, 0.9f, "a shape runs off the edge of the screen");
                }
            }
        }
    }

    [TestCaseSource(nameof(Every))]
    public void Outlines_GoAnticlockwise(int face, FaceMood mood)
    {
        foreach (FaceShape shape in FaceDesigns.Design(face, mood, false))
        {
            float area = 0f;
            for (int k = 0; k < shape.outline.Count; k++)
            {
                Vector2 a = shape.outline[k], b = shape.outline[(k + 1) % shape.outline.Count];
                area += a.x * b.y - b.x * a.y;
            }
            Assert.Greater(area, 0f);
        }
    }

    [TestCaseSource(nameof(Every))]
    public void DarkShapes_AreDrawnOverSomething(int face, FaceMood mood)
    {
        foreach (FaceShape shape in FaceDesigns.Design(face, mood, false))
        {
            if (shape.ink == FaceInk.Dark) Assert.GreaterOrEqual(shape.layer, 1);
        }
    }

    [TestCaseSource(nameof(Every))]
    public void FacesWithCheeks_KeepThem_AndTheyStayPut(int face, FaceMood mood)
    {
        List<FaceShape> cheeks = FaceDesigns.Design(face, mood, false).FindAll(s => s.ink == FaceInk.Blush);
        Assert.AreEqual(Faces.HasCheeks(face) ? 2 : 0, cheeks.Count);
        foreach (FaceShape cheek in cheeks)
        {
            Assert.AreEqual(0f, cheek.look);
            Assert.IsFalse(cheek.blinks);
        }
    }

    [TestCase(Faces.Cyclops)]
    [TestCase(Faces.Visor)]
    public void OneEyedFaces_StayOneEyed_InEveryMood(int face)
    {
        foreach (FaceMood mood in Moods)
        {
            foreach (FaceShape shape in FaceDesigns.Design(face, mood, false)) Assert.AreEqual(0f, shape.anchor.x, mood.ToString());
        }
    }

    [Test]
    public void TwoEyedFaces_KeepAnEyeEachSide_InEveryMood()
    {
        for (int face = 0; face < Faces.Count; face++)
        {
            if (face == Faces.Cyclops || face == Faces.Visor) continue;
            foreach (FaceMood mood in Moods)
            {
                List<FaceShape> glow = FaceDesigns.Design(face, mood, false).FindAll(s => s.ink == FaceInk.Glow);
                Assert.IsTrue(glow.Exists(s => s.anchor.x < -0.1f) && glow.Exists(s => s.anchor.x > 0.1f), Faces.Name(face) + " " + mood);
            }
        }
    }

    [Test]
    public void TheCyclops_KeepsItsSightMarks_InEveryMood()
    {
        foreach (FaceMood mood in Moods)
        {
            List<FaceShape> shapes = FaceDesigns.Design(Faces.Cyclops, mood, false);
            Assert.IsTrue(shapes.Exists(s => s.outline.TrueForAll(p => p.x > 0.25f)), mood.ToString());
            Assert.IsTrue(shapes.Exists(s => s.outline.TrueForAll(p => p.x < -0.25f)), mood.ToString());
        }
    }

    [Test]
    public void Reactions_LookDifferent()
    {
        for (int face = 0; face < Faces.Count; face++)
        {
            int normal = FaceDesigns.Design(face, FaceMood.Normal, false).Count;
            foreach (FaceMood mood in new[] { FaceMood.Hurt, FaceMood.Happy, FaceMood.Tired })
            {
                List<FaceShape> a = FaceDesigns.Design(face, FaceMood.Normal, false), b = FaceDesigns.Design(face, mood, false);
                bool same = a.Count == b.Count;
                for (int i = 0; same && i < a.Count; i++) same = a[i].outline.Count == b[i].outline.Count && a[i].outline[0] == b[i].outline[0];
                Assert.IsFalse(same, Faces.Name(face) + " " + mood + " looks just like normal");
            }
            Assert.Greater(normal, 0);
        }
    }

    [Test]
    public void ADeadDrone_ShowsXes_WhateverItsFaceAndMood()
    {
        List<FaceShape> xes = FaceDesigns.Design(Faces.Classic, FaceMood.Normal, true);
        Assert.AreEqual(4, xes.Count);
        for (int face = 0; face < Faces.Count; face++)
        {
            foreach (FaceMood mood in Moods)
            {
                List<FaceShape> shapes = FaceDesigns.Design(face, mood, true);
                Assert.AreEqual(xes.Count, shapes.Count);
                Assert.IsTrue(shapes.TrueForAll(s => s.ink == FaceInk.Glow && !s.blinks));
            }
        }
    }
}
