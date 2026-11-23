using UnityEngine;

// A dead player's body, tumbling around under physics for a few seconds.
// Purely cosmetic: every client spawns its own copy (see Manager.SpawnRagdoll_R) and simulates it
// locally, so nothing is synced over the network and the body never gets pulled back to where the
// player died. On death the head comes off and flies separately, harder on a headshot, which also
// knocks the cap off the head.
public class DeadPlayer : MonoBehaviour
{
    public float lifetime = 5f;
    // How long the body takes to shrink away at the end of its lifetime
    public float shrinkTime = 0.4f;

    private Rigidbody body;
    private Transform head, cap;
    private Rigidbody capBody;
    private Vector3 bodyScale, headScale, capScale;
    private float age;

    // What the death camera watches: the cap if a headshot knocked it off, else the head
    public Transform FocusPoint => cap != null ? cap : head != null ? head : transform;

    // Makes the body and head look like the player's drone, switched off (eyes shut, lights out).
    // Call before Launch, which takes the head off.
    public void Dress(Color accent, bool teamMode)
    {
        SphereCollider headCollider = GetComponentInChildren<SphereCollider>();
        CapsuleCollider bodyCollider = GetComponentInChildren<CapsuleCollider>();
        MeshFilter bodyMesh = bodyCollider != null ? bodyCollider.GetComponent<MeshFilter>() : null;
        if (bodyMesh == null)
        {
            // The body mesh is whichever one isn't the head
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>())
                if (headCollider == null || filter.transform != headCollider.transform) { bodyMesh = filter; break; }
        }
        if (bodyMesh == null) return;
        DroneBody.Dress(bodyMesh.transform, headCollider != null ? headCollider.transform : null, accent, teamMode, true);
    }

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
        Collider[] ragdollColliders = AllColliders();

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

    // Every collider of the ragdoll: body, head and (when knocked off) cap
    private Collider[] AllColliders()
    {
        var all = new System.Collections.Generic.List<Collider>(GetComponentsInChildren<Collider>());
        if (head != null) all.AddRange(head.GetComponentsInChildren<Collider>());
        if (cap != null) all.AddRange(cap.GetComponentsInChildren<Collider>());
        return all.ToArray();
    }

    private void DetachHead(Vector3 velocity, Vector3 push, float knockback, bool headshot, Color capColor)
    {
        SphereCollider headCollider = GetComponentInChildren<SphereCollider>();
        if (headCollider == null) return;

        head = headCollider.transform;
        PlayerCap headCap = PlayerCap.Attach(head, capColor, true);
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
        Vector3 headVelocity = velocity + push * force + Vector3.up * lift + Random.insideUnitSphere * 0.5f;
        headBody.linearVelocity = headVelocity;
        headBody.angularVelocity = Random.insideUnitSphere * (headshot ? 15f : 8f);

        // A headshot also knocks the cap off: it flies up and on ahead of the head, spinning, and being
        // light it flutters down. Otherwise it stays on the head and rolls away with it.
        if (headshot && headCap != null)
        {
            KnockOffCap(headCap.transform, headVelocity, push, knockback);
            CapshotBurst.Spawn(head.position, push, capColor);
        }

        // Head and body start out touching: ignore each other briefly so they don't shove each other apart
        IgnoreBriefly(head.GetComponentsInChildren<Collider>(), GetComponentsInChildren<Collider>());
        if (cap != null)
        {
            Collider[] capColliders = cap.GetComponentsInChildren<Collider>();
            IgnoreBriefly(capColliders, GetComponentsInChildren<Collider>());
            // The crown hugs the head, so the two start out overlapping: they never collide with each other
            foreach (Collider capCollider in capColliders)
                foreach (Collider headPart in head.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(capCollider, headPart, true);
        }

        headScale = head.localScale;
    }

    private void KnockOffCap(Transform capTransform, Vector3 headVelocity, Vector3 push, float knockback)
    {
        cap = capTransform;
        cap.SetParent(null, true);
        capScale = cap.localScale;

        capBody = cap.gameObject.AddComponent<Rigidbody>();
        capBody.mass = 0.06f;
        // Little air resistance on the way up, so it shoots high; Update raises it once the cap starts
        // falling, so it floats down instead of dropping like a stone
        capBody.linearDamping = 0.3f;
        capBody.angularDamping = 0.6f;
        capBody.interpolation = RigidbodyInterpolation.Interpolate;
        capBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Mostly straight up (about 3 m high), only partly following the bullet, so it's clearly a cap flying off
        capBody.linearVelocity = headVelocity * 0.5f + push * (knockback * 0.3f) + Vector3.up * 8f + Random.insideUnitSphere * 0.6f;
        // Spins mostly end over end along the bullet's path, plus some wobble
        capBody.angularVelocity = Vector3.Cross(Vector3.up, push) * 18f + Random.insideUnitSphere * 8f;
    }

    // Stops two sets of colliders hitting each other for a quarter of a second, while they move apart
    private void IgnoreBriefly(Collider[] a, Collider[] b)
    {
        SetIgnored(a, b, true);
        StartCoroutine(Reenable(a, b));
    }

    private System.Collections.IEnumerator Reenable(Collider[] a, Collider[] b)
    {
        yield return new WaitForSeconds(0.25f);
        SetIgnored(a, b, false);
    }

    private static void SetIgnored(Collider[] a, Collider[] b, bool ignore)
    {
        foreach (Collider x in a)
            foreach (Collider y in b)
                if (x != null && y != null) Physics.IgnoreCollision(x, y, ignore);
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

        // A knocked-off cap that has started falling catches the air and flutters down
        if (capBody != null && capBody.linearDamping < 2f && capBody.linearVelocity.y < 0f) capBody.linearDamping = 2f;

        float left = lifetime - age;
        if (left <= 0f)
        {
            Destroy(gameObject); // OnDestroy removes the head and cap too
            return;
        }

        // Shrink away at the end instead of popping out of existence
        if (left < shrinkTime)
        {
            float scale = left / shrinkTime;
            transform.localScale = bodyScale * scale;
            if (head != null) head.localScale = headScale * scale;
            if (cap != null) cap.localScale = capScale * scale;
        }
    }

    private void OnDestroy()
    {
        if (head != null) Destroy(head.gameObject);
        if (cap != null) Destroy(cap.gameObject);
    }
}
