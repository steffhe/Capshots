using System.Collections.Generic;
using UnityEngine;

// How a drone is feeling, which changes its face for a moment (or, for Tired, while it lasts):
// Hurt when hit, Happy after a kill, Tired while badly hurt
public enum FaceMood { Normal, Hurt, Happy, Tired }

// What a face shape is drawn in: the eyes' glow, the face screen's own dark (to cut into a glowing
// shape), or the pink of a blush
public enum FaceInk { Glow, Dark, Blush }

// One flat shape of a face, drawn on the face screen (see DroneBody and SurfacePrint). All in head
// radii, across the screen as seen from inside the head: x towards the drone's left, y up.
public struct FaceShape
{
    // Its outline, anticlockwise, where it sits on the screen
    public List<Vector2> outline;
    // The middle of the group it belongs to (an eye, a brow, a mouth): it blinks towards it, and
    // moves with it when the eyes look round
    public Vector2 anchor;
    // How many shapes it's drawn over (each a hair further out from the screen)
    public int layer;
    public FaceInk ink;
    // Whether it squashes shut when the drone blinks
    public bool blinks;
    // How far it moves when the eyes look round, of the full look (inner parts of an eye move more
    // than its rim, so it looks round inside it; brows and mouths move less, cheeks not at all)
    public float look;
}

// The drone faces (see Faces) as flat shapes on the face screen, for each mood. Each face keeps
// its own look in every mood: the cyclops keeps its single lens, the visor its one bar, the dot eyes
// stay small, the angry face keeps its brows, and the faces with cheeks keep them.
public static class FaceDesigns
{
    // The face screen, an ellipsoid bulging out of the front of the head: its centre and radii
    public static readonly Vector3 ScreenCentre = new Vector3(0f, -0.14f, 0.6f);
    public static readonly Vector3 ScreenRadii = new Vector3(0.8f, 0.46f, 0.47f);
    // How far the first layer of shapes is off the screen, and each next layer off the last
    public const float Lift = 0.008f, LayerStep = 0.006f;
    // How far the eyes move at most when looking round: across and up
    public static readonly Vector2 LookReach = new Vector2(0.07f, 0.045f);

    // Where an eye sits for most faces
    private const float EyeX = 0.24f;

    private class Builder
    {
        public readonly List<FaceShape> shapes = new List<FaceShape>();
        public Vector2 anchor;
        public bool blinks;
        public float look;

        public void Group(float x, float y, bool blinks, float look = 1f)
        {
            anchor = new Vector2(x, y);
            this.blinks = blinks;
            this.look = look;
        }

        // A shape at x, y from the group's middle, turned by roll degrees
        public void Add(List<Vector2> outline, float x, float y, FaceInk ink, int layer, float roll = 0f, float look = -1f)
        {
            shapes.Add(new FaceShape
            {
                outline = SurfacePrint.Turned(outline, anchor.x + x, anchor.y + y, roll),
                anchor = anchor,
                layer = layer,
                ink = ink,
                blinks = blinks,
                look = look >= 0f ? look : this.look,
            });
        }

        public void Oval(float x, float y, float w, float h, int layer = 0, float roll = 0f, float look = -1f) => Add(SurfacePrint.Ellipse(w, h), x, y, FaceInk.Glow, layer, roll, look);
        public void DarkOval(float x, float y, float w, float h, int layer = 1, float look = -1f) => Add(SurfacePrint.Ellipse(w, h), x, y, FaceInk.Dark, layer, 0f, look);
        // A bar with round ends, w thick and length long, lying up and down before it's turned
        public void Bar(float x, float y, float w, float length, float roll = 0f, int layer = 0, float look = -1f) => Add(SurfacePrint.Bar(w, length), x, y, FaceInk.Glow, layer, roll, look);
        public void Box(float x, float y, float w, float h, float roll = 0f, int layer = 0) => Add(SurfacePrint.Box(w, h), x, y, FaceInk.Glow, layer, roll);
        public void DarkBox(float x, float y, float w, float h, float roll = 0f, int layer = 1) => Add(SurfacePrint.Box(w, h), x, y, FaceInk.Dark, layer, roll);

        // A round-ended bar from one point to another
        public void Line(Vector2 from, Vector2 to, float w, int layer = 0, FaceInk ink = FaceInk.Glow)
        {
            Vector2 middle = (from + to) * 0.5f, way = to - from;
            float roll = Mathf.Atan2(way.y, way.x) * Mathf.Rad2Deg - 90f;
            Add(SurfacePrint.Bar(w, way.magnitude + w), middle.x, middle.y, ink, layer, roll);
        }

        // An upturned V, like a smiling closed eye: two short bars meeting at the top
        // (thick: how thick its bars are, if not in step with its size)
        public void Chevron(float size = 1f, float y = 0f, float thick = 0f)
        {
            float w = thick > 0f ? thick : 0.07f * size;
            Bar(-0.065f * size, y - 0.02f * size, w, 0.2f * size, -55f);
            Bar(0.065f * size, y - 0.02f * size, w, 0.2f * size, 55f);
        }

        // A tightly shut, wincing eye: a V on its side pointing in to the middle of the face
        public void Squeeze(float inner, float size = 1f)
        {
            Vector2 tip = new Vector2(inner * 0.07f * size, 0f);
            Line(tip, new Vector2(-inner * 0.07f * size, 0.075f * size), 0.06f * size);
            Line(tip, new Vector2(-inner * 0.07f * size, -0.075f * size), 0.06f * size);
        }

        // A half-shut eye: a low flat oval under a heavy lid
        public void HalfLid(float w = 0.22f)
        {
            Oval(0f, -0.01f, w, 0.12f * w / 0.22f);
            Bar(0f, 0.07f * w / 0.22f, 0.035f, w + 0.04f, 90f);
        }

        // A heart: two round lobes over a point
        public void Heart(float size = 1f, float y = 0f)
        {
            Oval(-0.055f * size, y + 0.04f * size, 0.14f * size, 0.14f * size);
            Oval(0.055f * size, y + 0.04f * size, 0.14f * size, 0.14f * size);
            Box(0f, y - 0.035f * size, 0.135f * size, 0.135f * size, 45f);
        }

        // A broken heart: a heart with a dark crack zigzagging down it
        public void BrokenHeart()
        {
            Heart();
            Line(new Vector2(0.005f, 0.11f), new Vector2(-0.025f, 0.04f), 0.025f, 1, FaceInk.Dark);
            Line(new Vector2(-0.025f, 0.04f), new Vector2(0.025f, -0.01f), 0.025f, 1, FaceInk.Dark);
            Line(new Vector2(0.025f, -0.01f), new Vector2(-0.005f, -0.08f), 0.025f, 1, FaceInk.Dark);
        }

        // A four-pointed star, with shorter points between, turned by roll degrees
        public void StarEye(float size = 1f, float y = 0f, float roll = 0f)
        {
            Oval(0f, y, 0.08f * size, 0.34f * size, 0, roll);
            Oval(0f, y, 0.08f * size, 0.34f * size, 0, roll + 90f);
            Oval(0f, y, 0.05f * size, 0.18f * size, 0, roll + 45f);
            Oval(0f, y, 0.05f * size, 0.18f * size, 0, roll - 45f);
        }

        // A four-pointed twinkle: two thin crossed ovals, the across one a little shorter
        public void Twinkle(float x, float y, float size, int layer, float look = -1f)
        {
            Oval(x, y, 0.22f * size, size, layer, 0f, look);
            Oval(x, y, 0.8f * size, 0.22f * size, layer, 0f, look);
        }

        // Mouths, below the eyes, which don't blink and move only a little as the eyes look round
        public void MouthGroup(float y = -0.33f) => Group(0f, y, false, 0.5f);
        // A small smile: two short bars meeting at the bottom
        public void Smile(float size = 1f)
        {
            Bar(-0.045f * size, 0f, 0.045f, 0.13f * size, 68f);
            Bar(0.045f * size, 0f, 0.045f, 0.13f * size, -68f);
        }
        // A small frown: two short bars meeting at the top
        public void Frown()
        {
            Bar(-0.045f, 0f, 0.045f, 0.13f, -68f);
            Bar(0.045f, 0f, 0.045f, 0.13f, 68f);
        }
        // A little open mouth, "oh"
        public void Oh()
        {
            Oval(0f, 0f, 0.075f, 0.085f);
            DarkOval(0f, 0f, 0.035f, 0.045f);
        }
    }

    // The shapes of a face in a mood. A dead drone shows X-es whatever its face and mood.
    public static List<FaceShape> Design(int face, FaceMood mood, bool dead)
    {
        if (!Faces.IsValid(face)) face = Faces.Classic;
        var b = new Builder();
        if (dead)
        {
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                b.Group(side * EyeX, -0.06f, false);
                b.Bar(0f, 0f, 0.075f, 0.34f, 45f);
                b.Bar(0f, 0f, 0.075f, 0.34f, -45f);
            }
            return b.shapes;
        }

        if (face == Faces.Visor) Visor(b, mood);
        else if (face == Faces.Cyclops) Cyclops(b, mood);
        else
        {
            for (int i = 0; i < 2; i++) TwoEyed(b, face, mood, i == 0 ? -1f : 1f);
            Mouth(b, face, mood);
        }

        // Pink cheeks under the eyes, in every mood
        if (Faces.HasCheeks(face))
        {
            for (int i = 0; i < 2; i++)
            {
                b.Group(i == 0 ? -0.44f : 0.44f, -0.3f, false, 0f);
                b.Add(SurfacePrint.Ellipse(0.14f, 0.07f), 0f, 0f, FaceInk.Blush, 0);
            }
        }
        return b.shapes;
    }

    // One eye (and what goes with it: a brow, a floating twinkle) of the faces with two
    private static void TwoEyed(Builder b, int face, FaceMood mood, float side)
    {
        // Towards the middle of the face, from this eye
        float inner = -side;
        float x = side * (face == Faces.Dots ? 0.2f : EyeX);
        float dots = face == Faces.Dots ? 0.6f : 1f;

        if (mood == FaceMood.Hurt)
        {
            switch (face)
            {
                case Faces.Hearts:
                    // Heartbroken
                    b.Group(x, -0.04f, false);
                    b.BrokenHeart();
                    return;
                case Faces.Stars:
                    // Seeing stars: smaller, knocked askew
                    b.Group(x, -0.05f, false);
                    b.StarEye(0.75f, 0f, 30f * side);
                    return;
                default:
                    b.Group(x, -0.05f, false);
                    b.Squeeze(inner, dots);
                    if (face == Faces.Angry) Brow(b, side, 0.05f);
                    // The big glossy eyes well up: a tear under each
                    if (face == Faces.Sparkle) b.Oval(-inner * 0.06f, -0.185f, 0.045f, 0.065f);
                    return;
            }
        }

        if (mood == FaceMood.Happy)
        {
            switch (face)
            {
                case Faces.Hearts:
                    // Hearts swell with it
                    b.Group(x, -0.04f, true);
                    b.Heart(1.25f);
                    return;
                case Faces.Stars:
                    // Bigger stars, with a twinkle off each
                    b.Group(x, -0.05f, true);
                    b.StarEye(1.2f);
                    b.Twinkle(side * 0.17f, 0.15f, 0.1f, 0);
                    return;
                case Faces.Angry:
                {
                    // A smug squint: the angry eye cut from below as well, under the brows
                    b.Group(x, -0.08f, true);
                    b.Oval(0f, 0f, 0.23f, 0.24f);
                    b.DarkBox(inner * 0.06f, 0.1f, 0.36f, 0.14f, -inner * 30f);
                    b.DarkOval(0f, -0.15f, 0.3f, 0.2f);
                    Brow(b, side, 0.085f);
                    return;
                }
                case Faces.Sparkle:
                    b.Group(x, -0.04f, true);
                    b.Chevron(1.1f);
                    b.Group(side * 0.44f, 0.12f, false, 0.2f);
                    b.Twinkle(0f, 0f, 0.12f, 0);
                    return;
                default:
                    // Smiling closed eyes; the happy face's own smile gets wider
                    b.Group(x, -0.02f, true);
                    b.Chevron(face == Faces.Happy ? 1.15f : dots);
                    return;
            }
        }

        if (mood == FaceMood.Tired)
        {
            switch (face)
            {
                case Faces.Sleepy:
                    // Nearly asleep: just the heavy lids, low down
                    b.Group(x, -0.12f, true);
                    b.Bar(0f, 0f, 0.04f, 0.24f, 90f);
                    return;
                case Faces.Angry:
                {
                    // Still cross, but the eye's drooping: low and flat under the slanted cut
                    b.Group(x, -0.11f, true);
                    b.Oval(0f, 0f, 0.23f, 0.15f);
                    b.DarkBox(inner * 0.06f, 0.07f, 0.36f, 0.12f, -inner * 22f);
                    Brow(b, side, 0.06f, -0.11f, 22f);
                    return;
                }
                case Faces.Hearts:
                    // Small hearts, sagging
                    b.Group(x, -0.1f, true);
                    b.Heart(0.7f);
                    return;
                case Faces.Stars:
                    b.Group(x, -0.1f, true);
                    b.StarEye(0.65f);
                    return;
                default:
                    b.Group(x, -0.1f, true);
                    b.HalfLid(face == Faces.Dots ? 0.13f : face == Faces.Sparkle ? 0.26f : 0.22f);
                    return;
            }
        }

        // Normal
        switch (face)
        {
            case Faces.Happy:
                b.Group(x, -0.02f, true);
                b.Chevron();
                break;
            case Faces.Wink:
                if (side < 0f)
                {
                    b.Group(x, -0.06f, true);
                    b.Oval(0f, 0f, 0.2f, 0.3f);
                }
                else
                {
                    b.Group(x, -0.02f, true);
                    b.Chevron();
                }
                break;
            case Faces.Sleepy:
                b.Group(x, -0.1f, true);
                b.HalfLid();
                break;
            case Faces.Angry:
                // Narrowed eyes, their tops cut off along a slant down to the middle, under thick
                // glowing brows on the same slant
                b.Group(x, -0.08f, true);
                b.Oval(0f, 0f, 0.23f, 0.24f);
                b.DarkBox(inner * 0.06f, 0.1f, 0.36f, 0.14f, -inner * 30f);
                Brow(b, side, 0.085f);
                break;
            case Faces.Hearts:
                b.Group(x, -0.04f, true);
                b.Heart();
                break;
            case Faces.Stars:
                b.Group(x, -0.05f, true);
                b.StarEye();
                break;
            case Faces.Dots:
                b.Group(x, -0.02f, true);
                b.Oval(0f, 0f, 0.12f, 0.13f);
                break;
            case Faces.Sparkle:
                // Big glossy eyes: a glowing rim round a large dark iris with a four-pointed twinkle
                // high up and a small glint low down, and a little twinkle floating off each eye's
                // outer top corner. The iris and its glints move more than the rim as it looks round.
                b.Group(x, -0.07f, true, 0.8f);
                b.Oval(0f, 0f, 0.27f, 0.36f);
                b.DarkOval(0f, -0.01f, 0.2f, 0.29f, 1, 1f);
                b.Twinkle(side * 0.025f, 0.05f, 0.14f, 2, 1.1f);
                b.Oval(inner * 0.04f, -0.08f, 0.04f, 0.04f, 2, 0f, 1.1f);
                b.Group(side * 0.44f, 0.12f, false, 0.2f);
                b.Twinkle(0f, 0f, 0.12f, 0);
                break;
            default:
                // Classic and Blushing: the two big ovals
                b.Group(x, -0.06f, true);
                b.Oval(0f, 0f, 0.2f, 0.3f);
                break;
        }
    }

    // The angry face's brow over an eye: a thick glowing bar along the cut, slanting down to the
    // middle; it doesn't blink, and moves a little less than the eye
    private static void Brow(Builder b, float side, float y, float eyeY = -0.08f, float slant = 30f)
    {
        float inner = -side;
        b.Group(side * EyeX, eyeY, false, 0.6f);
        b.Bar(inner * 0.04f, y, 0.06f, 0.3f, 90f - inner * slant, 2);
    }

    private static void Mouth(Builder b, int face, FaceMood mood)
    {
        if (face == Faces.Angry)
        {
            b.MouthGroup();
            if (mood == FaceMood.Happy)
            {
                // A smirk up to one side
                b.Bar(0.02f, 0.01f, 0.045f, 0.16f, 78f);
            }
            else b.Frown();
        }
        else if (face == Faces.Dots)
        {
            b.MouthGroup(-0.25f);
            if (mood == FaceMood.Hurt) b.Oh();
            else if (mood == FaceMood.Happy) b.Smile(1.2f);
            else b.Bar(0f, 0f, 0.035f, mood == FaceMood.Tired ? 0.1f : 0.14f, 90f);
        }
        else if (face == Faces.Happy && mood == FaceMood.Happy)
        {
            b.MouthGroup(-0.3f);
            b.Smile();
        }
    }

    // The visor: one wide bar across the face
    private static void Visor(Builder b, FaceMood mood)
    {
        switch (mood)
        {
            case FaceMood.Hurt:
                // The bar jolts into a zigzag
                b.Group(0f, -0.06f, false);
                for (int k = 0; k < 4; k++)
                {
                    float x0 = -0.32f + 0.16f * k;
                    float y0 = k % 2 == 0 ? -0.04f : 0.04f;
                    b.Line(new Vector2(x0, y0), new Vector2(x0 + 0.16f, -y0), 0.055f);
                }
                break;
            case FaceMood.Happy:
                // An arch, like a smiling closed eye stretched across the face
                b.Group(0f, -0.04f, true);
                b.Line(new Vector2(-0.34f, -0.08f), new Vector2(-0.12f, 0.02f), 0.09f);
                b.Line(new Vector2(-0.12f, 0.02f), new Vector2(0.12f, 0.02f), 0.09f);
                b.Line(new Vector2(0.12f, 0.02f), new Vector2(0.34f, -0.08f), 0.09f);
                break;
            case FaceMood.Tired:
                // Dimmed to a thin, lower line
                b.Group(0f, -0.12f, true);
                b.Bar(0f, 0f, 0.05f, 0.6f, 90f);
                break;
            default:
                b.Group(0f, -0.06f, true);
                b.Bar(0f, 0f, 0.12f, 0.72f, 90f);
                break;
        }
    }

    // The cyclops: one big round lens in the middle, with short sight marks either side, like a scope
    private static void Cyclops(Builder b, FaceMood mood)
    {
        switch (mood)
        {
            case FaceMood.Hurt:
                // The lens snaps shut: its rim round a dark middle with a glowing slit across it
                b.Group(0f, -0.07f, false, 0.55f);
                b.Oval(0f, 0f, 0.48f, 0.48f);
                b.DarkOval(0f, 0f, 0.38f, 0.38f);
                b.Bar(0f, 0f, 0.05f, 0.3f, 90f, 2);
                break;
            case FaceMood.Happy:
                // One big smiling closed eye
                b.Group(0f, -0.04f, true);
                b.Chevron(1.9f, 0.02f, 0.085f);
                break;
            case FaceMood.Tired:
                // The lens half shut: flattened and low, under a heavy lid, its iris sunk low
                b.Group(0f, -0.11f, true, 0.55f);
                b.Oval(0f, 0f, 0.46f, 0.28f);
                b.DarkOval(0f, 0f, 0.36f, 0.19f, 1, 0.55f);
                b.Oval(0f, -0.02f, 0.15f, 0.12f, 2, 0f, 0.85f);
                b.DarkOval(0f, -0.025f, 0.065f, 0.055f, 3, 1.1f);
                b.Bar(0f, 0.17f, 0.04f, 0.5f, 90f, 0, 0.55f);
                break;
            default:
                // A glowing rim, a dark ring, a glowing iris round a dark pupil, and glints, each
                // just in front of the one before. The iris and pupil move more than the rim.
                b.Group(0f, -0.07f, true, 0.55f);
                b.Oval(0f, 0f, 0.48f, 0.48f);
                b.DarkOval(0f, 0f, 0.38f, 0.38f);
                b.Oval(0f, -0.01f, 0.27f, 0.27f, 2, 0f, 0.85f);
                b.DarkOval(0f, -0.015f, 0.12f, 0.12f, 3, 1.1f);
                b.Oval(0.06f, 0.055f, 0.06f, 0.06f, 4, 0f, 1.1f);
                b.Oval(-0.035f, -0.085f, 0.025f, 0.025f, 4, 0f, 1.1f);
                break;
        }
        // The sight marks stay in every mood, so it's always the cyclops
        b.Group(0f, mood == FaceMood.Tired ? -0.11f : -0.07f, false, 0.3f);
        for (int mark = -1; mark <= 1; mark += 2) b.Bar(mark * 0.32f, 0f, 0.035f, 0.1f, 90f);
    }
}
