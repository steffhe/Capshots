using UnityEngine;

// A cap lying where a player died in Cap Grab (Manager.CapGrab.cs): their own hat, in their colour,
// floating at head height and turning slowly over a glowing ring, with a column of light above it so
// it can be found from across the map. It blinks for its last few seconds, then vanishes. Every
// client makes its own copy when the host says a cap dropped; the host decides who takes it.
// On a cap shot it doesn't just appear: it's knocked off the head, flies up tumbling and flutters
// down to where it rests (the same path on every machine), and only then can it be taken.
public class CapPickup : MonoBehaviour
{
    public const float PickupRadius = 1.4f;
    private const float FloatHeight = 0.9f;
    private const float BlinkTime = 3f;
    private const float CollectTime = 0.3f;
    // A cap shot off a head: how long it flies, how high above the head it goes, and how long the
    // ring and column of light take to come up once it lands
    public const float FlightTime = 1.1f;
    private const float FlightHeight = 2.6f;
    private const float AppearTime = 0.3f;

    public int Id { get; private set; }
    public int OwnerActor { get; private set; }
    // Where it rests on the ground, without its bobbing
    public Vector3 BasePosition { get; private set; }
    // The hat itself, e.g. for the death camera to watch it fly
    public Transform Pivot => pivot;
    // Down where it can be taken (always, unless it was shot off a head and is still flying)
    public bool Landed => Time.time >= landsAt;

    private Transform pivot;
    private Renderer[] renderers;
    private Renderer beam, ring;
    private Light glow;
    private Color color;
    private float expiresAt;
    private float collectedAt = -1f;
    private float phase;
    private MaterialPropertyBlock block;
    // A flight off a head: where from, when it lands, and which way it tumbles
    private bool flying;
    private Vector3 flightFrom;
    private float launchedAt, landsAt = -1f;
    private Vector3 tumbleAxis = Vector3.right;
    private float glowIntensity;

    // mine: the local player's own cap, whose column of light is white, so it's easy to tell apart
    // flyFrom: knocked off a head there by a cap shot, so it flies to position first (and its time on
    // the ground starts when it lands)
    public static CapPickup Create(int id, Vector3 position, int ownerActor, int hat, Color color, float lifetime, bool mine, Vector3? flyFrom = null)
    {
        GameObject root = new GameObject("Cap Pickup " + id);
        root.transform.position = position;
        CapPickup pickup = root.AddComponent<CapPickup>();
        pickup.Id = id;
        pickup.OwnerActor = ownerActor;
        pickup.BasePosition = position;
        pickup.color = color;
        pickup.expiresAt = Time.time + lifetime;
        pickup.phase = Random.value * 10f;
        pickup.Build(hat, mine);
        if (flyFrom.HasValue) pickup.Launch(flyFrom.Value, lifetime);
        return pickup;
    }

    private void Build(int hat, bool mine)
    {
        block = new MaterialPropertyBlock();

        // The hat sits on an invisible head the size of a player's
        pivot = new GameObject("Head").transform;
        pivot.SetParent(transform, false);
        pivot.localPosition = Vector3.up * FloatHeight;
        pivot.localScale = Vector3.one * 0.7f;
        PlayerCap.Attach(pivot, color, false, hat);

        // A glowing ring on the ground and a thin column of light above
        ring = Glow(PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(1.1f, 0.01f, 1.1f));
        beam = Glow(PrimitiveType.Cylinder, new Vector3(0f, 3.5f, 0f), new Vector3(0.06f, 3f, 0.06f));
        Paint(ring, color, color * 1.8f);
        Color beamColor = mine ? Color.white : color;
        Paint(beam, beamColor, beamColor * 1.5f);

        glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = color;
        glow.range = 3f;
        glow.intensity = glowIntensity = 1.6f;
        glow.shadows = LightShadows.None;

        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void Launch(Vector3 from, float lifetime)
    {
        flying = true;
        flightFrom = from;
        launchedAt = Time.time;
        landsAt = launchedAt + FlightTime;
        expiresAt = landsAt + lifetime;
        // End over end along the way it flies (or any way, flying straight up)
        Vector3 travel = BasePosition - from;
        travel.y = 0f;
        Vector3 axis = Vector3.Cross(Vector3.up, travel);
        tumbleAxis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.right;
        // The ground marks come up once it lands
        ring.enabled = beam.enabled = false;
        glow.intensity = 0f;
        Fly(launchedAt);
    }

    // Where the hat is on its way down: up quickly, slowing at the top, then fluttering down slower,
    // swaying a little and turning end over end, three times, to land upright
    private void Fly(float time)
    {
        float s = Mathf.Clamp01((time - launchedAt) / FlightTime);
        Vector3 rest = transform.position + Vector3.up * FloatHeight;
        pivot.position = CapFlight.Position(flightFrom, rest, tumbleAxis, FlightHeight, s);
        pivot.rotation = Quaternion.AngleAxis(CapFlight.Tumble(s), tumbleAxis) * Quaternion.Euler(0f, s * 720f, 0f);
        // A bit smaller off the head, growing to the pickup's size
        pivot.localScale = Vector3.one * Mathf.Lerp(0.6f, 0.7f, s);
    }

    private Renderer Glow(PrimitiveType type, Vector3 position, Vector3 scale)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Renderer r = part.GetComponent<Renderer>();
        r.sharedMaterial = DroneBody.GlowMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    private void Paint(Renderer r, Color baseColor, Color emission)
    {
        if (r == null) return;
        r.GetPropertyBlock(block);
        block.SetColor("_BaseColor", baseColor);
        block.SetColor("_Color", baseColor);
        block.SetColor("_EmissionColor", emission);
        r.SetPropertyBlock(block);
    }

    // Someone took it: it shoots up and shrinks away. denied: its owner got it back.
    public void Collected(bool denied)
    {
        if (collectedAt >= 0f) return;
        collectedAt = Time.time;
        if (beam != null) beam.enabled = false;
        if (ring != null) ring.enabled = false;
        if (glow != null) glow.color = denied ? Color.white : color;
        Destroy(gameObject, CollectTime);
    }

    private void Update()
    {
        float time = Time.time;

        if (collectedAt >= 0f)
        {
            float t = Mathf.Clamp01((time - collectedAt) / CollectTime);
            pivot.localPosition = Vector3.up * (FloatHeight + t * 1.2f);
            pivot.localScale = Vector3.one * (0.7f * (1f - t));
            pivot.Rotate(0f, 720f * Time.deltaTime, 0f, Space.World);
            if (glow != null) glow.intensity = 3f * (1f - t);
            return;
        }

        if (flying)
        {
            if (time < landsAt)
            {
                Fly(time);
                return;
            }
            // Landed: the ring, column of light and glow come up
            flying = false;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one * 0.7f;
            ring.enabled = beam.enabled = true;
        }
        if (glow != null && time < landsAt + AppearTime) glow.intensity = glowIntensity * Mathf.Clamp01((time - landsAt) / AppearTime);

        if (time >= expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        // Turn and bob, like a hat on a hovering drone
        pivot.localPosition = Vector3.up * (FloatHeight + Mathf.Sin((time + phase) * 2.2f) * 0.08f);
        pivot.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);

        // Blink faster and faster in its last seconds
        float left = expiresAt - time;
        bool visible = left > BlinkTime || Mathf.Repeat(time * Mathf.Lerp(8f, 3f, left / BlinkTime), 1f) > 0.35f;
        foreach (Renderer r in renderers) if (r != null) r.enabled = visible;
        if (glow != null) glow.enabled = visible;
    }
}
