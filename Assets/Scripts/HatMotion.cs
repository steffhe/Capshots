using UnityEngine;

// Small looping animations for hat parts (see PlayerCap): turning round and round, sweeping back
// and forth, bobbing up and down, or flickering like a flame. Each starts at a random point in its
// loop, so a room full of the same hat doesn't move in step. Only a look: nothing is sent.
public class HatMotion : MonoBehaviour
{
    public enum Kind { Spin, Sweep, Bob, Flicker }

    private Kind kind;
    private Vector3 axis;
    private float speed;   // Spin: degrees a second. Sweep, Bob, Flicker: how fast it goes, in loops a second (roughly)
    private float amount;  // Sweep: degrees each way. Bob: distance each way (metres). Flicker: how much it changes size
    private float phase;
    private float angle;
    private Vector3 basePosition, baseScale;
    private Quaternion baseRotation;

    public static HatMotion Add(GameObject part, Kind kind, Vector3 axis, float speed, float amount = 0f)
    {
        HatMotion motion = part.AddComponent<HatMotion>();
        motion.kind = kind;
        motion.axis = axis.normalized;
        motion.speed = speed;
        motion.amount = amount;
        motion.phase = Random.value * 100f;
        motion.basePosition = part.transform.localPosition;
        motion.baseRotation = part.transform.localRotation;
        motion.baseScale = part.transform.localScale;
        return motion;
    }

    private void LateUpdate()
    {
        float t = Time.time + phase;
        switch (kind)
        {
            case Kind.Spin:
                angle = Mathf.Repeat(angle + speed * Time.deltaTime, 360f);
                transform.localRotation = baseRotation * Quaternion.AngleAxis(angle, axis);
                break;
            case Kind.Sweep:
                transform.localRotation = baseRotation * Quaternion.AngleAxis(Mathf.Sin(t * speed * 2f * Mathf.PI) * amount, axis);
                break;
            case Kind.Bob:
                transform.localPosition = basePosition + axis * (Mathf.Sin(t * speed * 2f * Mathf.PI) * amount);
                break;
            case Kind.Flicker:
                // Two noises: one for its height, which flickers most, and one for its width
                float tall = Mathf.PerlinNoise(t * speed, 0.3f) - 0.5f;
                float wide = Mathf.PerlinNoise(0.7f, t * speed) - 0.5f;
                transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + wide * amount, 1f + tall * amount * 2f, 1f + wide * amount));
                break;
        }
    }
}
