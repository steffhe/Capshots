using System.Collections.Generic;
using UnityEngine;

// The look of the player's one transforming gun: a single set of machined parts that every form
// is built from (GunModel builds them, GunMorph moves them from one form's layout to the next).
// Every form has every part, with the same name, so a change of form moves each part from where it
// was to where it goes and nothing pops in or out. A part a form doesn't use is collapsed (size 0)
// into the receiver, where it extends from and retracts into.
//
// The shared core never changes: the receiver, built round an open window where the glowing energy
// core sits between two clamps with two rings floating round it, the rear housing and cap, the
// raked grip and the trigger guard. What changes per form:
//  - Pistol: a short shroud, a stub of barrel and a floating muzzle ring; side fins on the rear housing.
//  - Machine gun: a long vented shroud and a drop foregrip; heat-sink modules floating off the
//    shroud; a spindle down the middle of the rotary barrel (GunSatellites' slats) with a floating
//    ring at its tip; a stock stub.
//  - Shotgun: a tall, wide shroud with a pump under it; shell plates floating high on its sides;
//    a wide bore inside the claw (GunSatellites' petals).
//  - Sniper rifle: a slim shroud; a spindle through the floating barrel segments (GunSatellites)
//    with a muzzle brake at the tip; scope mounts; cheek plates on the rear housing and a full
//    stock floating behind the rear cap.
//
// Positions are in the gun model's design space (Anchor/Design of the prefabs in Assets/Resources:
// +z out of the muzzle, +y up), fitted round the parts the rest of the game measures, which stay in
// the prefabs as invisible blocks: Body (GunSatellites fits its pieces round it, DroneHands its
// hands), Grip (the grip hand), Under and Barrel. The top stays at the sights' base (0.397), so
// aiming down the sights is unchanged.
public static class GunDesign
{
    public enum Shape
    {
        Box,     // a plain box
        Chamfer, // a box with bevelled long edges (an octagonal prism along z)
        Cylinder,// along z
        Ring,    // round z: size x and y are the outside diameter, z the thickness
    }

    public enum Finish
    {
        Graphite, // matte, nearly black
        Gunmetal, // satin dark metal (the old gun's colour)
        Steel,    // brushed, light
        Rubber,   // dark, dull
        Glow,     // the player's (or team's) colour, glowing
    }

    // How GunModel moves a part on top of where it sits
    public enum Motion
    {
        None,
        Core,       // the energy core: its glow behaves per form, and it's the cell swapped on reload
        CoreRing,   // turns round the core and bobs along it
        SideModule, // floats off the side, kicks out with a shot, drifts loose during the cell swap
        Muzzle,     // floats at the muzzle, punched forward by a shot (turned by the machine gun's spin)
        Stock,      // floats behind the rear cap, kicked back by a shot
        Vent,       // glows with the machine gun's heat, the shotgun's recharge
        Line,       // the energy channels; a pulse runs along them with a sniper shot
    }

    public struct Part
    {
        public string name;
        public Shape shape;
        public Finish finish;
        public Vector3 position;
        public Vector3 rotation; // Euler angles
        public Vector3 size;
        public Motion motion;
        public int side;         // -1 left, 1 right, 0 middle
        public bool detail;      // small: left out of other players' guns
        public bool shared;      // the same in every form

        public bool Collapsed => size.x <= 0f || size.y <= 0f || size.z <= 0f;
    }

    // The centre line the parts are built round
    public const float X = 0.444f;
    public const float CentreY = 0.288f;
    public const float Top = 0.397f;      // the sights sit here
    // The energy core: in the receiver's window, in front of the grip
    public static readonly Vector3 CorePosition = new Vector3(X, 0.275f, 1.25f);
    public const float CoreDiameter = 0.085f, CoreLength = 0.15f;
    // Where collapsed parts tuck away: inside the receiver's front
    private static readonly Vector3 Tuck = new Vector3(X, 0.29f, 1.40f);

    // Parts move this far apart during the "release" stage of a change of form, and these do
    private const float Release = 0.012f;

    public static readonly GunSatelliteLayout.Form[] Forms =
    {
        GunSatelliteLayout.Form.Tight, GunSatelliteLayout.Form.Orbit, GunSatelliteLayout.Form.Fan, GunSatelliteLayout.Form.Rail,
    };

    private static readonly Dictionary<GunSatelliteLayout.Form, Part[]> cache = new Dictionary<GunSatelliteLayout.Form, Part[]>();

    public static Part[] For(GunSatelliteLayout.Form form)
    {
        if (!cache.TryGetValue(form, out Part[] parts))
        {
            parts = Build(form).ToArray();
            cache[form] = parts;
        }
        return parts;
    }

    // Whether a part comes loose in the "release" stage of a change of form (clamps open, modules
    // and the front assembly lift away from the receiver before moving), and which way
    public static Vector3 ReleaseOffset(Part part)
    {
        if (part.shared && part.motion != Motion.CoreRing && !part.name.StartsWith("Core Clamp")) return Vector3.zero;
        Vector3 away = new Vector3(part.position.x - X, part.position.y - CorePosition.y, 0f);
        if (part.name.StartsWith("Core Clamp")) away = new Vector3(0f, 0f, part.position.z - CorePosition.z);
        else if (part.side != 0) away = new Vector3(part.side, 0f, 0f);
        else if (away.sqrMagnitude < 1e-6f) away = new Vector3(0f, 0f, part.position.z >= CorePosition.z ? 1f : -1f);
        return away.normalized * Release;
    }

    public static Part Find(GunSatelliteLayout.Form form, string name)
    {
        foreach (Part p in For(form)) if (p.name == name) return p;
        return default;
    }

    #region Layout

    private static List<Part> Build(GunSatelliteLayout.Form form)
    {
        var parts = new List<Part>();
        Shared(parts);
        Front(parts, form);
        return parts;
    }

    private static Part P(string name, Shape shape, Finish finish, Vector3 position, Vector3 size,
        Motion motion = Motion.None, int side = 0, bool detail = false, Vector3 rotation = default)
    {
        return new Part
        {
            name = name, shape = shape, finish = finish, position = position, size = size,
            motion = motion, side = side, detail = detail, rotation = rotation,
        };
    }

    // A part that this form doesn't use: tucked away inside the receiver
    private static Part Hidden(string name, Shape shape, Finish finish, Motion motion = Motion.None, int side = 0, bool detail = false)
    {
        return P(name, shape, finish, Tuck, Vector3.zero, motion, side, detail);
    }

    private static void Add(List<Part> parts, Part part, bool shared = false)
    {
        part.shared = shared;
        parts.Add(part);
    }

    private static void Shared(List<Part> parts)
    {
        // The receiver: solid behind and in front of the core, a spine over it and a keel under it,
        // leaving the core open to both sides
        Add(parts, P("Receiver Rear", Shape.Chamfer, Finish.Graphite, new Vector3(X, CentreY, 1.065f), new Vector3(0.14f, 0.218f, 0.21f)), true);
        Add(parts, P("Receiver Front", Shape.Chamfer, Finish.Graphite, new Vector3(X, CentreY, 1.395f), new Vector3(0.14f, 0.218f, 0.13f)), true);
        Add(parts, P("Receiver Spine", Shape.Box, Finish.Graphite, new Vector3(X, 0.373f, 1.25f), new Vector3(0.12f, 0.048f, 0.16f)), true);
        Add(parts, P("Receiver Keel", Shape.Box, Finish.Graphite, new Vector3(X, 0.19f, 1.25f), new Vector3(0.12f, 0.022f, 0.16f)), true);
        // A brushed steel strip along the top of the spine, where the sights' line runs
        Add(parts, P("Spine Plate", Shape.Box, Finish.Steel, new Vector3(X, 0.394f, 1.065f), new Vector3(0.06f, 0.008f, 0.17f), detail: true), true);

        // The energy core (the cell that's swapped on reload), its clamps, and two rings floating round it
        Add(parts, P("Core", Shape.Cylinder, Finish.Glow, CorePosition, new Vector3(CoreDiameter, CoreDiameter, CoreLength), Motion.Core), true);
        Add(parts, P("Core Clamp Rear", Shape.Ring, Finish.Gunmetal, new Vector3(X, CorePosition.y, 1.178f), new Vector3(0.118f, 0.118f, 0.03f)), true);
        Add(parts, P("Core Clamp Front", Shape.Ring, Finish.Gunmetal, new Vector3(X, CorePosition.y, 1.322f), new Vector3(0.118f, 0.118f, 0.03f)), true);
        Add(parts, P("Core Ring A", Shape.Ring, Finish.Glow, new Vector3(X, CorePosition.y, 1.218f), new Vector3(0.13f, 0.13f, 0.014f), Motion.CoreRing), true);
        Add(parts, P("Core Ring B", Shape.Ring, Finish.Glow, new Vector3(X, CorePosition.y, 1.282f), new Vector3(0.13f, 0.13f, 0.014f), Motion.CoreRing), true);

        // The rear housing and its cap, with a glowing disc on the back
        Add(parts, P("Rear Housing", Shape.Chamfer, Finish.Graphite, new Vector3(X, 0.30f, 0.88f), new Vector3(0.12f, 0.19f, 0.16f)), true);
        Add(parts, P("Rear Cap", Shape.Chamfer, Finish.Gunmetal, new Vector3(X, 0.30f, 0.785f), new Vector3(0.13f, 0.15f, 0.03f)), true);
        Add(parts, P("Rear Glow", Shape.Cylinder, Finish.Glow, new Vector3(X, 0.30f, 0.768f), new Vector3(0.05f, 0.05f, 0.006f), detail: true), true);

        // The grip, raked back, with a glowing strip down its back
        Vector3 gripRotation = new Vector3(14f, 0f, 0f);
        Vector3 grip = new Vector3(X, 0.05f, 1.085f);
        Add(parts, P("Grip", Shape.Chamfer, Finish.Rubber, grip, new Vector3(0.105f, 0.27f, 0.115f), rotation: gripRotation), true);
        Add(parts, P("Grip Glow", Shape.Box, Finish.Glow, grip + Quaternion.Euler(gripRotation) * new Vector3(0f, -0.01f, -0.0585f),
            new Vector3(0.02f, 0.16f, 0.006f), detail: true, rotation: gripRotation), true);

        // The trigger guard
        Add(parts, P("Trigger Guard", Shape.Box, Finish.Gunmetal, new Vector3(X, 0.135f, 1.215f), new Vector3(0.03f, 0.012f, 0.14f)), true);
        Add(parts, P("Guard Post", Shape.Box, Finish.Gunmetal, new Vector3(X, 0.16f, 1.28f), new Vector3(0.03f, 0.04f, 0.012f)), true);
        Add(parts, P("Trigger", Shape.Box, Finish.Steel, new Vector3(X, 0.158f, 1.21f), new Vector3(0.014f, 0.04f, 0.012f), detail: true, rotation: new Vector3(10f, 0f, 0f)), true);
    }

    // The per-form parts, in the same order for every form
    private static void Front(List<Part> parts, GunSatelliteLayout.Form form)
    {
        // Shroud: from the receiver's front (1.46) to the front of the form's Body block
        float shroudFront, shroudWidth, shroudBottom, shroudTop = Top;
        switch (form)
        {
            case GunSatelliteLayout.Form.Orbit: shroudFront = 2.376f; shroudWidth = 0.13f; shroudBottom = 0.203f; break;
            case GunSatelliteLayout.Form.Fan: shroudFront = 2.025f; shroudWidth = 0.20f; shroudBottom = 0.158f; shroudTop = 0.418f; break;
            case GunSatelliteLayout.Form.Rail: shroudFront = 2.1265f; shroudWidth = 0.12f; shroudBottom = 0.213f; break;
            default: shroudFront = 1.643f; shroudWidth = 0.13f; shroudBottom = 0.193f; break;
        }
        const float shroudBack = 1.46f;
        Add(parts, P("Shroud", Shape.Chamfer, Finish.Graphite,
            new Vector3(X, (shroudTop + shroudBottom) / 2f, (shroudBack + shroudFront) / 2f),
            new Vector3(shroudWidth, shroudTop - shroudBottom, shroudFront - shroudBack)));

        // Vents: glowing slots through the shroud, lit by heat (machine gun) or the recharge (shotgun)
        float[] vents;
        float ventHeight;
        switch (form)
        {
            case GunSatelliteLayout.Form.Orbit: vents = new[] { 1.56f, 1.68f, 1.80f, 1.92f }; ventHeight = 0.10f; break;
            case GunSatelliteLayout.Form.Fan: vents = new[] { 1.60f, 1.70f, 1.80f, 1.90f }; ventHeight = 0.14f; break;
            case GunSatelliteLayout.Form.Rail: vents = new[] { 1.60f, 1.72f, 1.84f, 1.96f }; ventHeight = 0.07f; break;
            default: vents = null; ventHeight = 0f; break;
        }
        float shroudMiddle = (shroudTop + shroudBottom) / 2f;
        for (int i = 0; i < 4; i++)
        {
            string name = "Vent " + (i + 1);
            if (vents == null) Add(parts, Hidden(name, Shape.Box, Finish.Glow, Motion.Vent, detail: true));
            else Add(parts, P(name, Shape.Box, Finish.Glow, new Vector3(X, shroudMiddle, vents[i]),
                new Vector3(shroudWidth + 0.006f, ventHeight, 0.014f), Motion.Vent, detail: true));
        }

        // Energy channels: glowing lines down both sides, from the core to the front
        float lineY = form == GunSatelliteLayout.Form.Rail ? 0.235f : 0.215f;
        float lineX = Mathf.Max(0.07f, shroudWidth / 2f) + 0.0045f;
        float lineBack = 1.34f, lineFront = shroudFront - 0.03f;
        for (int s = -1; s <= 1; s += 2)
        {
            Add(parts, P(s < 0 ? "Energy Line Left" : "Energy Line Right", Shape.Box, Finish.Glow,
                new Vector3(X + s * lineX, lineY, (lineBack + lineFront) / 2f), new Vector3(0.008f, 0.016f, lineFront - lineBack), Motion.Line, s));
        }

        // Rails: brushed steel down the upper sides, which the front assembly slides along
        float railX = Mathf.Max(0.07f, shroudWidth / 2f) + 0.0055f;
        float railFront = shroudFront - 0.04f;
        for (int s = -1; s <= 1; s += 2)
        {
            Add(parts, P(s < 0 ? "Rail Left" : "Rail Right", Shape.Box, Finish.Steel,
                new Vector3(X + s * railX, 0.365f, (0.97f + railFront) / 2f), new Vector3(0.011f, 0.014f, railFront - 0.97f), side: s, detail: true));
        }

        // Side modules: floating plates with a glowing strip on each
        Vector3 module, moduleSize;
        float moduleX;
        switch (form)
        {
            case GunSatelliteLayout.Form.Orbit: module = new Vector3(0f, 0.29f, 2.155f); moduleSize = new Vector3(0.016f, 0.13f, 0.35f); moduleX = 0.081f; break;
            case GunSatelliteLayout.Form.Fan: module = new Vector3(0f, 0.36f, 1.675f); moduleSize = new Vector3(0.016f, 0.08f, 0.35f); moduleX = 0.114f; break;
            case GunSatelliteLayout.Form.Rail: module = new Vector3(0f, 0.31f, 0.91f); moduleSize = new Vector3(0.014f, 0.11f, 0.18f); moduleX = 0.078f; break;
            default: module = new Vector3(0f, 0.30f, 0.98f); moduleSize = new Vector3(0.014f, 0.12f, 0.15f); moduleX = 0.078f; break;
        }
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 at = new Vector3(X + s * moduleX, module.y, module.z);
            Add(parts, P(s < 0 ? "Side Module Left" : "Side Module Right", Shape.Chamfer, Finish.Gunmetal, at, moduleSize, Motion.SideModule, s));
            Add(parts, P(s < 0 ? "Side Glow Left" : "Side Glow Right", Shape.Box, Finish.Glow,
                at + new Vector3(s * (moduleSize.x / 2f + 0.002f), 0f, 0f), new Vector3(0.004f, 0.03f, moduleSize.z * 0.8f), Motion.SideModule, s, detail: true));
        }

        // The barrel's spindle, the glowing bore at its tip and a floating ring round the muzzle
        switch (form)
        {
            case GunSatelliteLayout.Form.Orbit:
                // Down the middle of the rotary barrel's slats
                Add(parts, P("Spindle", Shape.Cylinder, Finish.Steel, new Vector3(X, CentreY, 2.69f), new Vector3(0.035f, 0.035f, 0.62f)));
                Add(parts, P("Bore Glow", Shape.Cylinder, Finish.Glow, new Vector3(X, CentreY, 3.0f), new Vector3(0.03f, 0.03f, 0.01f), Motion.Muzzle));
                Add(parts, P("Muzzle", Shape.Ring, Finish.Gunmetal, new Vector3(X, CentreY, 3.03f), new Vector3(0.17f, 0.17f, 0.025f), Motion.Muzzle));
                Add(parts, P("Muzzle Glow", Shape.Ring, Finish.Glow, new Vector3(X, CentreY, 3.03f), new Vector3(0.15f, 0.15f, 0.03f), Motion.Muzzle, detail: true));
                break;
            case GunSatelliteLayout.Form.Fan:
                // A wide bore inside the claw's petals
                Add(parts, P("Spindle", Shape.Cylinder, Finish.Steel, new Vector3(X, CentreY, 2.045f), new Vector3(0.12f, 0.12f, 0.04f)));
                Add(parts, P("Bore Glow", Shape.Cylinder, Finish.Glow, new Vector3(X, CentreY, 2.067f), new Vector3(0.09f, 0.09f, 0.006f), Motion.Muzzle));
                Add(parts, P("Muzzle", Shape.Ring, Finish.Gunmetal, new Vector3(X, CentreY, 2.02f), new Vector3(0.17f, 0.17f, 0.02f), Motion.Muzzle));
                Add(parts, P("Muzzle Glow", Shape.Ring, Finish.Glow, new Vector3(X, CentreY, 2.02f), new Vector3(0.15f, 0.15f, 0.024f), Motion.Muzzle, detail: true));
                break;
            case GunSatelliteLayout.Form.Rail:
                // Through the floating barrel segments, with a brake at the tip
                Add(parts, P("Spindle", Shape.Cylinder, Finish.Steel, new Vector3(X, 0.34f, 2.66f), new Vector3(0.03f, 0.03f, 1.50f)));
                Add(parts, P("Bore Glow", Shape.Cylinder, Finish.Glow, new Vector3(X, 0.34f, 3.42f), new Vector3(0.03f, 0.03f, 0.01f), Motion.Muzzle));
                Add(parts, P("Muzzle", Shape.Ring, Finish.Gunmetal, new Vector3(X, 0.34f, 3.45f), new Vector3(0.10f, 0.085f, 0.03f), Motion.Muzzle));
                Add(parts, P("Muzzle Glow", Shape.Ring, Finish.Glow, new Vector3(X, 0.34f, 3.45f), new Vector3(0.088f, 0.074f, 0.034f), Motion.Muzzle, detail: true));
                break;
            default:
                // A stub of barrel, with a ring between the shroud and the floating cap (GunSatellites)
                Add(parts, P("Spindle", Shape.Cylinder, Finish.Steel, new Vector3(X, 0.31f, 1.66f), new Vector3(0.04f, 0.04f, 0.05f)));
                Add(parts, P("Bore Glow", Shape.Cylinder, Finish.Glow, new Vector3(X, 0.31f, 1.686f), new Vector3(0.026f, 0.026f, 0.006f), Motion.Muzzle));
                Add(parts, P("Muzzle", Shape.Ring, Finish.Gunmetal, new Vector3(X, 0.31f, 1.652f), new Vector3(0.06f, 0.06f, 0.012f), Motion.Muzzle));
                Add(parts, P("Muzzle Glow", Shape.Ring, Finish.Glow, new Vector3(X, 0.31f, 1.652f), new Vector3(0.05f, 0.05f, 0.016f), Motion.Muzzle, detail: true));
                break;
        }

        // Under the front: the machine gun's foregrip, the shotgun's pump, the sniper's bipod block
        switch (form)
        {
            case GunSatelliteLayout.Form.Orbit:
                Add(parts, P("Foregrip", Shape.Chamfer, Finish.Rubber, new Vector3(X, 0.10f, 1.857f), new Vector3(0.085f, 0.20f, 0.09f), rotation: new Vector3(8f, 0f, 0f)));
                break;
            case GunSatelliteLayout.Form.Fan:
                Add(parts, P("Foregrip", Shape.Chamfer, Finish.Rubber, new Vector3(X, 0.135f, 1.68f), new Vector3(0.15f, 0.06f, 0.38f)));
                break;
            case GunSatelliteLayout.Form.Rail:
                Add(parts, P("Foregrip", Shape.Chamfer, Finish.Rubber, new Vector3(X, 0.12f, 1.60f), new Vector3(0.08f, 0.17f, 0.09f), rotation: new Vector3(10f, 0f, 0f)));
                break;
            default:
                Add(parts, Hidden("Foregrip", Shape.Chamfer, Finish.Rubber));
                break;
        }

        // Scope mounts, under the sniper's scope
        bool scope = form == GunSatelliteLayout.Form.Rail;
        if (scope)
        {
            Add(parts, P("Scope Mount Rear", Shape.Box, Finish.Gunmetal, new Vector3(X, 0.4095f, 1.05f), new Vector3(0.05f, 0.025f, 0.04f)));
            Add(parts, P("Scope Mount Front", Shape.Box, Finish.Gunmetal, new Vector3(X, 0.4095f, 1.52f), new Vector3(0.05f, 0.025f, 0.04f)));
        }
        else
        {
            Add(parts, Hidden("Scope Mount Rear", Shape.Box, Finish.Gunmetal));
            Add(parts, Hidden("Scope Mount Front", Shape.Box, Finish.Gunmetal));
        }

        // The stock, floating behind the rear cap on a short rod: the sniper's full stock, the machine gun's stub
        if (form == GunSatelliteLayout.Form.Rail || form == GunSatelliteLayout.Form.Orbit)
        {
            bool full = form == GunSatelliteLayout.Form.Rail;
            float y = full ? 0.27f : 0.29f;
            float back = full ? 0.47f : 0.635f;
            const float front = 0.745f;
            Vector3 size = new Vector3(0.10f, full ? 0.20f : 0.12f, front - back);
            Add(parts, P("Stock", Shape.Chamfer, Finish.Graphite, new Vector3(X, y, (front + back) / 2f), size, Motion.Stock));
            Add(parts, P("Stock Pad", Shape.Chamfer, Finish.Rubber, new Vector3(X, y, back - 0.015f), new Vector3(0.11f, size.y + 0.01f, 0.03f), Motion.Stock));
            Add(parts, P("Stock Glow", Shape.Box, Finish.Glow, new Vector3(X, y, (front + back) / 2f), new Vector3(0.104f, 0.012f, size.z * 0.75f), Motion.Stock, detail: true));
            Add(parts, P("Stock Rod", Shape.Cylinder, Finish.Steel, new Vector3(X, 0.30f, 0.7575f), new Vector3(0.022f, 0.022f, 0.035f)));
        }
        else
        {
            Add(parts, Hidden("Stock", Shape.Chamfer, Finish.Graphite, Motion.Stock));
            Add(parts, Hidden("Stock Pad", Shape.Chamfer, Finish.Rubber, Motion.Stock));
            Add(parts, Hidden("Stock Glow", Shape.Box, Finish.Glow, Motion.Stock, detail: true));
            Add(parts, Hidden("Stock Rod", Shape.Cylinder, Finish.Steel));
        }
    }

    #endregion
}
