using UnityEngine;

// Spins the propeller on a propeller beanie: steadily while standing still, and faster the faster
// the hat moves (running, sliding, or flying off on a cap shot). Only a look: nothing is sent.
public class HatSpinner : MonoBehaviour
{
    private const float IdleSpeed = 240f;      // degrees a second
    private const float SpeedPerMetre = 220f;  // extra degrees a second for each m/s the hat moves
    private const float MaxSpeed = 2400f;

    private Vector3 lastPosition;
    private float speed = IdleSpeed;
    private bool started;

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 position = transform.position;
        float moving = started ? (position - lastPosition).magnitude / dt : 0f;
        lastPosition = position;
        started = true;

        // Spins up quickly and winds down slowly, like a real propeller
        float target = Mathf.Min(MaxSpeed, IdleSpeed + moving * SpeedPerMetre);
        speed = Mathf.Lerp(speed, target, 1f - Mathf.Exp(-(target > speed ? 6f : 1.5f) * dt));
        transform.Rotate(0f, speed * dt, 0f, Space.Self);
    }
}
