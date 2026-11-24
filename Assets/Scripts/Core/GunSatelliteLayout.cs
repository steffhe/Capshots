using UnityEngine;

// Where the floating pieces around the gun (GunSatellites) sit for each form, kept apart so it can be
// tested. Everything is in the gun model's own space: +z out of the muzzle, +y up. Nothing goes
// above the barrel, where it would block the iron sights or the scope while aiming.
public static class GunSatelliteLayout
{
    public enum Form
    {
        Tight, // pistol: a few pieces tucked in along the sides
        Orbit, // machine gun: a ring circling the barrel, spinning faster while firing
        Fan,   // shotgun: a wide ring fanned out round the muzzle
        Rail,  // sniper rifle: a line stretched out under and ahead of the barrel
    }

    public struct Pose
    {
        public Vector3 position;
        public Quaternion rotation;
        public float scale; // 0 = tucked away inside the gun, 1 = full size
    }

    // How many pieces the pistol shows; the rest tuck away
    public const int TightCount = 3;

    public static Form FormOf(Gun gun)
    {
        if (gun == null) return Form.Tight;
        if (gun.pellets > 1) return Form.Fan;
        if (gun.scopeFov > 0f) return Form.Rail;
        if (gun.burst == 1) return Form.Orbit;
        return Form.Tight;
    }

    // Radius of the rings round the barrel, from how wide the gun is
    public static float RingRadius(Bounds gun)
    {
        return Mathf.Clamp(gun.size.x * 0.5f + 0.06f, 0.07f, 0.14f);
    }

    // Piece i of count. spin (radians) turns the machine gun's ring.
    public static Pose Arrange(Form form, int i, int count, Bounds gun, Vector3 muzzle, float spin)
    {
        count = Mathf.Max(1, count);
        Vector3 forward = Vector3.forward;
        float radius = RingRadius(gun);
        Pose pose = new Pose { scale = 1f };

        switch (form)
        {
            case Form.Fan:
            {
                // Petals round the muzzle, tilted outward like the shot spreading
                float angle = i * Mathf.PI * 2f / count + Mathf.PI * 0.5f;
                Vector3 outward = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                pose.position = muzzle + outward * (radius + 0.03f) + forward * 0.02f;
                Vector3 along = (forward * Mathf.Cos(30f * Mathf.Deg2Rad) + outward * Mathf.Sin(30f * Mathf.Deg2Rad)).normalized;
                pose.rotation = Quaternion.LookRotation(along, outward);
                break;
            }
            case Form.Orbit:
            {
                // A ring lying along the barrel, a little behind the muzzle, every other piece staggered
                float angle = i * Mathf.PI * 2f / count + spin;
                Vector3 outward = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                float back = Mathf.Min(0.15f, gun.size.z * 0.3f) + (i % 2) * 0.04f;
                pose.position = muzzle + outward * radius - forward * back;
                pose.rotation = Quaternion.LookRotation(forward, outward);
                break;
            }
            case Form.Rail:
            {
                // Evenly spaced from the middle of the gun to just past the muzzle, under the line of fire
                float t = count > 1 ? (float)i / (count - 1) : 1f;
                float z = Mathf.Lerp(gun.center.z, muzzle.z + 0.12f, t);
                pose.position = new Vector3(muzzle.x, muzzle.y - 0.05f, z);
                pose.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 1f : -1f) * 8f);
                pose.scale = Mathf.Lerp(1f, 0.8f, t);
                break;
            }
            default:
            {
                // Two along the sides of the slide and one under the muzzle; any others tuck away
                if (i >= TightCount)
                {
                    pose.position = gun.center;
                    pose.rotation = Quaternion.identity;
                    pose.scale = 0f;
                    break;
                }
                float side = gun.extents.x + 0.03f;
                float z = Mathf.Lerp(gun.center.z, muzzle.z - 0.03f, 0.5f);
                if (i == 0) pose.position = new Vector3(gun.center.x - side, muzzle.y - 0.01f, z);
                else if (i == 1) pose.position = new Vector3(gun.center.x + side, muzzle.y - 0.01f, z);
                else pose.position = new Vector3(muzzle.x, muzzle.y - 0.06f, muzzle.z - 0.02f);
                Vector3 outward = i == 0 ? Vector3.left : i == 1 ? Vector3.right : Vector3.down;
                pose.rotation = Quaternion.LookRotation(forward, outward);
                break;
            }
        }
        return pose;
    }

    // The direction a piece is pushed on each shot: away from the barrel, or forward on the rail
    public static Vector3 KickDirection(Form form, Vector3 position, Vector3 muzzle)
    {
        if (form == Form.Rail) return Vector3.forward;
        Vector3 outward = new Vector3(position.x - muzzle.x, position.y - muzzle.y, 0f);
        return outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.up;
    }
}
