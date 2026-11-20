using UnityEngine;

// A dead player's body, tumbling around under physics for a few seconds.
// Purely cosmetic: every client spawns its own copy (see Manager.SpawnRagdoll_R) and simulates it
// locally, so nothing is synced over the network and the body never gets pulled back to where the
// player died. On death the head comes off and flies separately, harder on a headshot.
public class DeadPlayer : MonoBehaviour
{
    public float lifetime = 5f;
    // How long the body takes to shrink away at the end of its lifetime
    public float shrinkTime = 0.4f;

    private Rigidbody body;
    private Transform head;
    private Vector3 bodyScale, headScale;
    private float age;

    // Sets the ragdoll going.
    // velocity: the player's movement at death. hitDirection: the killing bullet's direction.
    // hitOffset: where the bullet hit, relative to the player's position. knockback: how hard (m/s) the hit throws the body.
    // capColor: the colour of the cap on the head (see PlayerCap.ColorFor).
    public void Launch(Vector3 velocity, Vector3 hitDirection, Vector3 hitOffset, float knockback, bool headshot, Color capColor)
    {
        body = GetComponent<Rigidbody>();
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Vector3 push = hitDirection.sqrMagnitude > 0.001f ? hitDirection.normalized : -transform.forward;

        DetachHead(velocity, push, knockback, headshot, capColor);
        // The head is no longer part of the body: work out the body's own balance point before pushing it
        body.ResetCenterOfMass();
        body.ResetInertiaTensor();

        // Body: keeps the player's momentum plus a small lift so it doesn't just slide along the floor
        body.linearVelocity = velocity + Vector3.up * 0.5f;
        body.angularVelocity = Random.insideUnitSphere * 1.5f;

        // The bullet's push, applied where it hit: a chest shot from the front tips the body over backwards,
        // a shot from above slams it down, one to the side spins it round. A headshot already took the
        // head off, so the body only gets part of that push.
        Vector3 hitPoint = transform.position + hitOffset;
        float bodyShare = headshot ? 0.5f : 1f;
        body.AddForceAtPosition(push * knockback * bodyShare * body.mass, hitPoint, ForceMode.Impulse);

        IgnoreNearbyPlayers();
    }

    // On other clients the dying player's own object is still there for a moment (its removal arrives
    // just after this ragdoll). Overlapping it would shove the ragdoll away, so ignore any player standing
    // right here. Players that spawn later still collide with the body normally.
    private void IgnoreNearbyPlayers()
    {
        Collider[] ragdollColliders = GetComponentsInChildren<Collider>();
        if (head != null) ragdollColliders = AppendHead(ragdollColliders);

        foreach (Player player in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if ((player.transform.position - transform.position).sqrMagnitude > 4f) continue;
            foreach (Collider playerCollider in player.GetComponentsInChildren<Collider>(true))
            {
                foreach (Collider ragdollCollider in ragdollColliders)
                {
                    Physics.IgnoreCollision(playerCollider, ragdollCollider, true);
                }
            }
        }
    }

    // The head's colliders: the head itself and its cap's brim
    private Collider[] AppendHead(Collider[] colliders)
    {
        Collider[] headColliders = head.GetComponentsInChildren<Collider>();
        Collider[] all = new Collider[colliders.Length + headColliders.Length];
        colliders.CopyTo(all, 0);
        headColliders.CopyTo(all, colliders.Length);
        return all;
    }

    private void DetachHead(Vector3 velocity, Vector3 push, float knockback, bool headshot, Color capColor)
    {
        SphereCollider headCollider = GetComponentInChildren<SphereCollider>();
        if (headCollider == null) return;

        head = headCollider.transform;
        // The cap sits on the head, so it flies off together with it
        PlayerCap.Attach(head, capColor, true);
        head.SetParent(null, true);

        Rigidbody headBody = head.gameObject.AddComponent<Rigidbody>();
        headBody.mass = 0.25f;
        headBody.angularDamping = 0.3f;
        headBody.interpolation = RigidbodyInterpolation.Interpolate;
        headBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // A headshot sends the head flying along the bullet's path; otherwise it pops off and rolls away,
        // still nudged a little the way the bullet was going
        float force = knockback * (headshot ? 2f : 0.5f);
        float lift = headshot ? 1.5f : 2f;
        headBody.linearVelocity = velocity + push * force + Vector3.up * lift + Random.insideUnitSphere * 0.5f;
        headBody.angularVelocity = Random.insideUnitSphere * (headshot ? 15f : 8f);

        // Head and body start out touching: ignore each other briefly so they don't shove each other apart
        Collider[] headColliders = head.GetComponentsInChildren<Collider>();
        foreach (Collider bodyCollider in GetComponentsInChildren<Collider>())
        {
            foreach (Collider part in headColliders) Physics.IgnoreCollision(part, bodyCollider, true);
        }
        Invoke(nameof(ReenableHeadCollision), 0.25f);

        headScale = head.localScale;
    }

    private void ReenableHeadCollision()
    {
        if (head == null) return;
        Collider[] headColliders = head.GetComponentsInChildren<Collider>();
        foreach (Collider bodyCollider in GetComponentsInChildren<Collider>())
        {
            foreach (Collider part in headColliders)
            {
                if (part != null && bodyCollider != null) Physics.IgnoreCollision(part, bodyCollider, false);
            }
        }
    }

    private void Start()
    {
        bodyScale = transform.localScale;
        // Spawned without Launch (e.g. dragged into a scene): still fall over
        if (body == null) Launch(Vector3.zero, -transform.forward, Vector3.up * 0.3f, 3f, false, PlayerCap.ColorFor(0));
    }

    private void Update()
    {
        age += Time.deltaTime;

        float left = lifetime - age;
        if (left <= 0f)
        {
            if (head != null) Destroy(head.gameObject);
            Destroy(gameObject);
            return;
        }

        // Shrink away at the end instead of popping out of existence
        if (left < shrinkTime)
        {
            float scale = left / shrinkTime;
            transform.localScale = bodyScale * scale;
            if (head != null) head.localScale = headScale * scale;
        }
    }

    private void OnDestroy()
    {
        if (head != null) Destroy(head.gameObject);
    }
}
