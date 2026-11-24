using UnityEngine;

// Where the floating pieces of the gun (GunSatellites) are for each form, and how they move, kept
// apart so it can be tested. The pieces are built into the gun rather than hung round it:
//  - Sniper rifle: the barrel itself, as a line of floating segments (the model's own Barrel is
//    hidden). Idle, the segments breathe and turn; a shot runs a wave down them, back to front.
//  - Machine gun: a rotary barrel of slats sleeved over the front of the body, which spins up
//    while firing and bursts wide apart with every shot.
//  - Shotgun: a claw of petals flared round the muzzle, open at the top so the bead stays in view,
//    that blooms wide on each shot; and two plates on the pump that rack back after each shot.
//  - Pistol: panels down both sides that recoil like a slide, a cap in front of the muzzle and a
//    plate under it. The other pieces tuck away inside the body.
// Everything is in the gun model's space (+z out of the muzzle, +y up) and sized from the model's
// own parts, so it fits any gun. Nothing reaches above the sights.
public static class GunSatelliteLayout
{
    public const int Count = 8;

    public enum Form
    {
        Tight, // pistol
        Orbit, // machine gun
        Fan,   // shotgun
        Rail,  // sniper rifle
    }

    // The parts of the gun model the pieces are fitted to, in its space
    public struct Shape
    {
        public Bounds body;
        public bool hasBarrel;
        public Bounds barrel;
        public bool hasUnder;
        public Bounds under;

        // The front of the barrel, else of the body, on its centre line
        public Vector3 Muzzle => hasBarrel
            ? new Vector3(barrel.center.x, barrel.center.y, barrel.max.z)
            : new Vector3(body.center.x, body.center.y, body.max.z);

        // The barrel, or for a gun without one, where it would be: out of the front of the body
        public Bounds Barrel => hasBarrel ? barrel
            : new Bounds(new Vector3(body.center.x, body.center.y, body.max.z + body.size.z * 0.5f),
                         new Vector3(body.size.x * 0.75f, body.size.y * 0.35f, body.size.z));
    }

    // What drives the movement
    public struct Motion
    {
        public float time;    // seconds, for idle movement
        public float spin;    // radians the machine gun's barrel has turned
        public float shotAge; // seconds since the last shot (large if none)
        public float aim;     // 0 hip to 1 aiming down the sights
    }

    public struct Pose
    {
        public Vector3 position;
        public Quaternion rotation;
        // The piece's width (x), thickness (y, its outward face) and length (z, along its
        // rotation's forward). Zero: tucked away.
        public Vector3 size;
    }

    // The name of the model's part the pieces stand in for in this form (hidden while it's shown)
    public static string ReplacedPart(Form form) => form == Form.Rail ? "Barrel" : null;

    public static Form FormOf(Gun gun)
    {
        if (gun == null) return Form.Tight;
        if (gun.pellets > 1) return Form.Fan;
        if (gun.scopeFov > 0f) return Form.Rail;
        if (gun.burst == 1) return Form.Orbit;
        return Form.Tight;
    }

    // A shot's punch: up fast, then dying away over about a tenth of a second
    public static float Kick(float age)
    {
        if (age < 0f) return 0f;
        const float rise = 0.035f;
        if (age < rise) return age / rise;
        return Mathf.Exp(-(age - rise) * 13f);
    }

    // The shotgun's pump: back and forward again, after the shot
    public static float Pump(float age)
    {
        const float start = 0.12f, length = 0.45f;
        if (age < start || age > start + length) return 0f;
        return Mathf.Sin(Mathf.PI * (age - start) / length);
    }

    public static Pose Arrange(Form form, int i, Shape shape, Motion motion)
    {
        switch (form)
        {
            case Form.Rail: return Barrel(i, shape, motion);
            case Form.Orbit: return Rotary(i, shape, motion);
            case Form.Fan: return Claw(i, shape, motion);
            default: return Slide(i, shape, motion);
        }
    }

    private static Pose Tucked(Shape shape)
    {
        return new Pose { position = shape.body.center, rotation = Quaternion.identity, size = Vector3.zero };
    }

    // Sniper: the barrel, in segments
    private static Pose Barrel(int i, Shape shape, Motion m)
    {
        Bounds barrel = shape.Barrel;
        float segment = barrel.size.z / Count;
        // Closer together while aiming
        float fill = Mathf.Lerp(0.76f, 0.86f, m.aim);
        // The shot's wave runs from the back segment to the front one
        float kick = Kick(m.shotAge - i * 0.028f);
        float idle = 1f - 0.6f * m.aim;

        float z = barrel.min.z + segment * (i + 0.5f);
        z += Mathf.Sin(m.time * 1.6f - i * 0.55f) * segment * 0.06f * idle;
        z += kick * segment * 1.1f;
        // Every other segment jumps out to one side, then the other
        float scatter = kick * barrel.size.x * 0.45f * (i % 2 == 0 ? 1f : -1f);

        float roll = Mathf.Sin(m.time * 1.1f + i * 0.9f) * 7f * idle + kick * 15f * (i % 2 == 0 ? 1f : -1f);
        float swell = 1.08f + 0.25f * kick;
        return new Pose
        {
            position = new Vector3(barrel.center.x + scatter, barrel.center.y, z),
            rotation = Quaternion.Euler(0f, 0f, roll),
            size = new Vector3(barrel.size.x * swell, barrel.size.y * swell, segment * fill),
        };
    }

    // Machine gun: slats round the line of fire, sleeved over the front of the body
    public static float RotaryRadius(Shape shape) => shape.body.size.x * 0.48f;
    // How far a shot bursts the slats out, as a share of the body's width
    public const float BurstWidth = 0.9f;

    private static Pose Rotary(int i, Shape shape, Motion m)
    {
        float width = shape.body.size.x;
        float length = shape.body.size.z * 0.35f;
        Vector3 muzzle = shape.Muzzle;
        float kick = Kick(m.shotAge);

        float angle = i * Mathf.PI * 2f / Count + m.spin;
        Vector3 outward = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        // Each shot bursts the slats wide apart (much less while aiming, so the sights stay clear)
        float radius = RotaryRadius(shape) * (1f - 0.1f * m.aim) + kick * width * BurstWidth * (1f - 0.8f * m.aim);
        // A sleeve over the front of the body, reaching on past it; every other slat sticks out a
        // little further, so the barrel looks machined rather than a tube
        float z = muzzle.z - length * 0.08f + (i % 2) * length * 0.08f;
        return new Pose
        {
            position = new Vector3(muzzle.x, muzzle.y, z) + outward * radius,
            rotation = Quaternion.LookRotation(Vector3.forward, outward),
            size = new Vector3(width * 0.28f, width * 0.13f, length),
        };
    }

    // Shotgun: six petals round the muzzle, opening on each shot, and two pump plates
    public const int ClawPetals = 6;

    // The petals' directions round the muzzle: a horseshoe from 150 degrees round to 390 (= 30),
    // leaving the top clear for the bead
    public static float PetalAngle(int i) => (150f + i * 48f) * Mathf.Deg2Rad;

    private static Pose Claw(int i, Shape shape, Motion m)
    {
        Bounds body = shape.body;
        float width = body.size.x;

        if (i >= ClawPetals)
        {
            // The pump: a plate either side of the forend, racked back after each shot
            Bounds under = shape.hasUnder ? shape.under
                : new Bounds(new Vector3(body.center.x, body.min.y - width * 0.3f, body.max.z - body.size.z * 0.25f),
                             new Vector3(width * 0.8f, width * 0.5f, body.size.z * 0.35f));
            float side = i == ClawPetals ? -1f : 1f;
            float rack = Pump(m.shotAge) * under.size.z * 0.65f;
            return new Pose
            {
                position = new Vector3(under.center.x + side * (under.extents.x + width * 0.07f), under.center.y, under.center.z - rack),
                rotation = Quaternion.LookRotation(Vector3.forward, side < 0f ? Vector3.left : Vector3.right),
                size = new Vector3(under.size.y * 0.9f, width * 0.1f, under.size.z * 0.95f),
            };
        }

        Vector3 muzzle = shape.Muzzle;
        float kick = Kick(m.shotAge);
        float angle = PetalAngle(i);
        Vector3 outward = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        float length = body.size.z * 0.22f;
        // Opens wide with the blast, breathes a little at rest, and narrows while aiming
        float flare = (14f + 3f * Mathf.Sin(m.time * 1.4f + i * 1.1f)) * (1f - 0.5f * m.aim) + 68f * kick;
        float radius = Mathf.Max(body.size.x, body.size.y) * 0.4f + kick * width * 0.35f;

        Vector3 along = Quaternion.AngleAxis(flare, Vector3.Cross(Vector3.forward, outward)) * Vector3.forward;
        Vector3 root = muzzle + outward * radius - Vector3.forward * width * 0.15f;
        return new Pose
        {
            position = root + along * (length * 0.5f),
            rotation = Quaternion.LookRotation(along, outward),
            size = new Vector3(width * 0.42f, width * 0.08f, length),
        };
    }

    // Pistol: side panels, a muzzle cap and an under-plate
    private static Pose Slide(int i, Shape shape, Motion m)
    {
        if (i >= 4) return Tucked(shape);

        Bounds body = shape.body;
        float width = body.size.x, height = body.size.y;
        float kick = Kick(m.shotAge);
        float breathe = Mathf.Sin(m.time * 1.8f + i * 1.3f);

        if (i < 2)
        {
            // Down the sides: hover a little off the body, tighter while aiming, and recoil back
            float side = i == 0 ? -1f : 1f;
            float gap = width * (0.1f + 0.03f * breathe) * (1f - 0.5f * m.aim) + kick * width * 0.28f;
            float length = body.size.z * 0.7f;
            return new Pose
            {
                position = new Vector3(body.center.x + side * (body.extents.x + gap + width * 0.05f), body.center.y + height * 0.1f,
                                       body.center.z + body.size.z * 0.05f - kick * body.size.z * 0.38f),
                rotation = Quaternion.LookRotation(Vector3.forward, side < 0f ? Vector3.left : Vector3.right),
                size = new Vector3(height * 0.55f, width * 0.1f, length),
            };
        }
        if (i == 2)
        {
            // A cap floating in front of the muzzle, punched forward by the shot
            Vector3 muzzle = shape.Muzzle;
            float swell = 1f + 0.45f * kick;
            return new Pose
            {
                position = new Vector3(muzzle.x, body.center.y + height * 0.1f, muzzle.z + width * (0.22f + 0.03f * breathe) + kick * width * 1.3f),
                rotation = Quaternion.identity,
                size = new Vector3(width * 0.9f * swell, height * 0.5f * swell, width * 0.16f),
            };
        }
        // Under the front of the body, dipping with the shot
        float plate = body.size.z * 0.4f;
        return new Pose
        {
            position = new Vector3(body.center.x, body.min.y - width * (0.14f + 0.02f * breathe) - kick * width * 0.4f, body.max.z - plate * 0.5f),
            rotation = Quaternion.LookRotation(Vector3.forward, Vector3.down),
            size = new Vector3(width * 0.8f, width * 0.12f, plate),
        };
    }

    // The highest point a piece reaches, for checking it stays under the sights
    public static float Top(Pose pose)
    {
        float top = float.MinValue;
        for (int c = 0; c < 8; c++)
        {
            Vector3 corner = Vector3.Scale(pose.size * 0.5f, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
            top = Mathf.Max(top, (pose.position + pose.rotation * corner).y);
        }
        return top;
    }
}
