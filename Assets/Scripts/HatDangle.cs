using UnityEngine;

// A floppy part of a hat (see PlayerCap) that swings as the hat moves: a tassel, a jester's prongs,
// a candle flame. The part's children hang from this object along restDirection, and its tip is a
// point that lags behind as the hat moves, pulled back towards its rest by a spring and down by
// gravity, so it swings when the player moves or jumps, and whips round when the hat is shot off.
// Only a look: nothing is sent.
public class HatDangle : MonoBehaviour
{
    private Vector3 restDirection; // in the parent's space
    private Quaternion restRotation;
    private float length, stiffness, gravity, damping;
    private Vector3 tip, lastTip;
    private bool started;

    // restDirection: the way it points at rest, in the parent's space. length: metres from here to
    // its tip. stiffness: how hard it's pulled back to rest. gravity: how much it hangs (1: fully).
    // damping: how quickly a swing dies down.
    public static HatDangle Add(GameObject pivot, Vector3 restDirection, float length, float stiffness, float gravity, float damping = 3f)
    {
        HatDangle dangle = pivot.AddComponent<HatDangle>();
        dangle.restDirection = restDirection.normalized;
        dangle.restRotation = pivot.transform.localRotation;
        dangle.length = Mathf.Max(0.001f, length);
        dangle.stiffness = stiffness;
        dangle.gravity = gravity;
        dangle.damping = damping;
        return dangle;
    }

    private void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;
        Transform parent = transform.parent;
        if (parent == null) return;

        Vector3 pivot = transform.position;
        Vector3 rest = parent.TransformDirection(restDirection).normalized;
        Vector3 restTip = pivot + rest * length;
        // Far from where it should be (the first frame, or a respawn): start again at rest
        if (!started || (tip - pivot).sqrMagnitude > 4f * length * length + 1f)
        {
            tip = lastTip = restTip;
            started = true;
        }

        // Verlet: keeps its speed (less damping), pulled by the spring and gravity
        Vector3 velocity = (tip - lastTip) * Mathf.Exp(-damping * dt);
        lastTip = tip;
        // The spring's step is capped so a stiff part (like the toadstool) can't overshoot and shake
        // when the frame rate drops: past 0.9 of the way back in one frame it would start to
        tip += velocity + (restTip - tip) * Mathf.Min(stiffness * dt * dt, 0.9f) + Physics.gravity * (gravity * dt * dt);

        // It can't stretch: keep the tip its length from the pivot
        Vector3 direction = tip - pivot;
        if (direction.sqrMagnitude < 1e-10f) direction = rest;
        direction.Normalize();
        tip = pivot + direction * length;

        transform.rotation = Quaternion.FromToRotation(rest, direction) * parent.rotation * restRotation;
    }
}
