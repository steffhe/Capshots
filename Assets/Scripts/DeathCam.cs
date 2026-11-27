using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// What you see right after dying: instead of cutting straight to the map view, a camera starts where
// your eyes were, pulls back and follows your head as it flies off (or your cap, when a headshot
// knocked it off) for a moment. Then it fades through black to your killer: over their shoulder,
// looking where they aim, until you respawn (SPECTATING and their name at the bottom). Without a
// killer to watch (a fall, your own grenade, or they died too), it fades to the map view instead.
// Purely local presentation; nothing here is networked.
//
// Two things start it, in either order (which one arrives first depends on the network): the local
// player's body going away (Begin, from Player) and that player's ragdoll appearing (Follow, from
// Manager). Without a ragdoll soon after, it just ends, and the map view shows as before.
public class DeathCam : MonoBehaviour
{
    // How long the camera stays on your body before the map view
    public const float LingerTime = 2.5f;
    private const float FadeTime = 0.22f;
    // Waiting this long for the ragdoll before giving up
    private const float TargetWait = 0.6f;
    // Where the camera settles: this far back from the head and this far above it
    private const float Distance = 3.2f;
    private const float Height = 1.4f;

    public static bool Active => instance != null;

    private static DeathCam instance;
    // A ragdoll that turned up before the body went away
    private static Transform pendingTarget;
    private static float pendingAt = -10f;

    private Camera cam;
    private Transform target;
    private Vector3 back;
    private float age;
    private float targetWaited;
    private Image fade;
    private bool fadingOut;
    private float fadeOutAge;

    // Spectating the killer: who (actor id and name), and how far the fade back in has got
    private const float SpectateDistance = 2.4f, SpectateHeight = 0.55f, SpectateSide = 0.45f;
    private static int pendingKiller = -1;
    private static string pendingKillerName;
    private static float pendingKillerAt = -10f;
    private int killer = -1;
    private string killerName;
    private bool spectating;
    private float fadeInAge;
    private Text spectateLabel;

    // The local player's body just went away. eyes: its camera, for where to start from.
    public static void Begin(Camera eyes)
    {
        End();
        if (eyes == null) return;

        GameObject go = new GameObject("Death Camera");
        go.transform.SetPositionAndRotation(eyes.transform.position, eyes.transform.rotation);
        instance = go.AddComponent<DeathCam>();
        instance.Build(eyes);

        if (pendingTarget != null && Time.time - pendingAt < 1f) instance.SetTarget(pendingTarget);
        pendingTarget = null;
        if (pendingKiller >= 0 && Time.time - pendingKillerAt < 2f)
        {
            instance.killer = pendingKiller;
            instance.killerName = pendingKillerName;
        }
        pendingKiller = -1;
    }

    // Who killed the local player (from the kill feed), to watch after the body. -1: nobody to watch.
    public static void SetKiller(int actor, string name)
    {
        if (instance != null)
        {
            instance.killer = actor;
            instance.killerName = name;
        }
        else
        {
            pendingKiller = actor;
            pendingKillerName = name;
            pendingKillerAt = Time.time;
        }
    }

    // The local player's ragdoll: focus is the head, or the cap if it came off
    public static void Follow(Transform focus)
    {
        if (focus == null) return;
        if (instance != null) instance.SetTarget(focus);
        else
        {
            pendingTarget = focus;
            pendingAt = Time.time;
        }
    }

    public static void End()
    {
        if (instance != null) Destroy(instance.gameObject);
        instance = null;
    }

    private void Build(Camera eyes)
    {
        cam = gameObject.AddComponent<Camera>();
        cam.CopyFrom(eyes);
        // Your own body is hidden from your own eyes (its layer), but the ragdoll isn't on that layer,
        // so the copied mask is fine as it is
        gameObject.AddComponent<AudioListener>();

        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData eyeData = eyes.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = eyeData.renderPostProcessing;
        data.antialiasing = eyeData.antialiasing;
        data.antialiasingQuality = eyeData.antialiasingQuality;

        Vector3 forward = eyes.transform.forward;
        forward.y = 0f;
        back = forward.sqrMagnitude > 0.001f ? -forward.normalized : Vector3.back;

        // A black overlay for the fade to the map view
        GameObject canvasObject = new GameObject("Death Camera Fade", typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        GameObject fadeObject = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        fadeObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = (RectTransform)fadeObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        fade = fadeObject.GetComponent<Image>();
        fade.color = new Color(0f, 0f, 0f, 0f);
        fade.raycastTarget = false;

        // SPECTATING and the killer's name, low in the middle, shown while watching them
        spectateLabel = UITheme.NewText("Spectating", canvasObject.transform, 30, Color.white, TextAnchor.MiddleCenter);
        RectTransform label = spectateLabel.rectTransform;
        label.anchorMin = label.anchorMax = label.pivot = new Vector2(0.5f, 0f);
        label.anchoredPosition = new Vector2(0f, 120f);
        label.sizeDelta = new Vector2(900f, 50f);
        spectateLabel.supportRichText = true;
        spectateLabel.gameObject.SetActive(false);
        // Under the fade, so it goes black with the picture
        spectateLabel.transform.SetAsFirstSibling();
    }

    // The killer's body, if they're still alive to watch
    private Player KillerBody()
    {
        if (killer < 0) return null;
        foreach (Player p in Player.All)
        {
            if (p != null && p.Object != null && p.Object.IsValid && !p.IsDead && p.ActorId == killer) return p;
        }
        return null;
    }

    private void SetTarget(Transform focus)
    {
        target = focus;
    }

    private void Update()
    {
        // Back in the game, or the match is over (the end screens take over)
        Manager manager = Manager.Instance;
        if (Player.Local != null || manager == null || manager.MatchOver)
        {
            End();
            return;
        }

        float dt = Time.deltaTime;
        age += dt;

        if (spectating)
        {
            Spectate(dt);
            return;
        }

        if (target == null)
        {
            targetWaited += dt;
            if (targetWaited > TargetWait) End();
            return;
        }

        // Fade to black, then cut to the killer, or hand over to the map view (which shows once this
        // camera is gone)
        if (!fadingOut && age >= LingerTime) fadingOut = true;
        if (fadingOut)
        {
            fadeOutAge += dt;
            fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(fadeOutAge / FadeTime));
            if (fadeOutAge >= FadeTime)
            {
                Player body = KillerBody();
                if (body == null)
                {
                    End();
                    return;
                }
                StartSpectating(body);
                return;
            }
        }

        // Drift back and up from where the eyes were, keeping the head (or cap) in view. The camera
        // always sits on a line from the focus that was checked for walls this frame, so it never cuts
        // through the floor or a wall on its way. When the line straight back is blocked it swings
        // round to a clearer side instead of closing in.
        Vector3 pivot = target.position + Vector3.up * PivotLift;
        if (!orbiting)
        {
            Vector3 fromPivot = transform.position - pivot;
            orbitDistance = fromPivot.magnitude;
            orbitDirection = fromPivot.sqrMagnitude > 0.0001f ? fromPivot.normalized : PreferredDirection(back);
            chosenDirection = PreferredDirection(back);
            orbiting = true;
        }

        float wantedDistance = new Vector2(Distance, Height).magnitude;
        chosenDirection = ChooseDirection(pivot, chosenDirection, wantedDistance);
        orbitDirection = Vector3.Slerp(orbitDirection, chosenDirection, 1f - Mathf.Exp(-3f * dt)).normalized;

        // Ease out to the wanted distance, but come in at once when something is in the way
        float clear = Clearance(pivot, orbitDirection, wantedDistance);
        orbitDistance = Mathf.Lerp(orbitDistance, wantedDistance, 1f - Mathf.Exp(-3.5f * dt));
        orbitDistance = Mathf.Min(orbitDistance, clear);
        transform.position = pivot + orbitDirection * orbitDistance;

        Vector3 look = target.position - transform.position;
        if (look.sqrMagnitude > 0.0001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(look, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, 1f - Mathf.Exp(-6f * dt));
        }
    }

    // The camera's focus sits this far above the head or cap, so checks don't start in the floor
    private const float PivotLift = 0.35f;
    private const float CameraRadius = 0.25f;
    private bool orbiting;
    private Vector3 orbitDirection, chosenDirection;
    private float orbitDistance;
    private static readonly RaycastHit[] hits = new RaycastHit[16];
    private static readonly float[] turns = { 0f, 35f, -35f, 70f, -70f, 110f, -110f, 150f, -150f, 180f };
    private static readonly float[] lifts = { 0f, 20f };

    // Back and up from the focus, as a direction
    private static Vector3 PreferredDirection(Vector3 back) => (back * Distance + Vector3.up * Height).normalized;

    // Keeps the current side while it's clear enough; otherwise the side, turned round the focus,
    // nearest the preferred one that is, or the clearest one if none is
    private Vector3 ChooseDirection(Vector3 pivot, Vector3 current, float wanted)
    {
        float enough = wanted * 0.8f;
        if (Clearance(pivot, current, wanted) >= enough) return current;

        Vector3 preferred = PreferredDirection(back);
        Vector3 best = current;
        float bestClear = -1f;
        foreach (float lift in lifts)
        {
            foreach (float turn in turns)
            {
                Vector3 side = Quaternion.AngleAxis(turn, Vector3.up) * preferred;
                if (lift > 0f) side = Vector3.RotateTowards(side, Vector3.up, lift * Mathf.Deg2Rad, 0f);
                float clear = Clearance(pivot, side, wanted);
                if (clear >= enough) return side;
                if (clear > bestClear)
                {
                    bestClear = clear;
                    best = side;
                }
            }
        }
        return best;
    }

    // How far the camera can go from the focus in this direction before touching the world. Bodies
    // moved by physics (ragdolls, caps, glass shards) don't count: they're what the camera watches.
    private static float Clearance(Vector3 pivot, Vector3 direction, float distance)
    {
        int mask = ~((1 << Layers.LocalPlayer) | (1 << Layers.RemotePlayer) | (1 << Layers.Gun) | (1 << Layers.LocalGun) | (1 << Layers.Hitbox) | (1 << 2));
        int count = Physics.SphereCastNonAlloc(pivot, CameraRadius, direction, hits, distance, mask, QueryTriggerInteraction.Ignore);
        float clear = distance;
        bool startsTouching = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (IsLoose(hit)) continue;
            // Already touching at the start (the focus is right against a wall or the floor): the
            // sphere can't tell how far is free, so a thin ray below checks this direction instead
            if (hit.distance <= 0f)
            {
                startsTouching = true;
                continue;
            }
            clear = Mathf.Min(clear, hit.distance - 0.05f);
        }
        if (startsTouching)
        {
            count = Physics.RaycastNonAlloc(pivot, direction, hits, distance, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!IsLoose(hits[i])) clear = Mathf.Min(clear, hits[i].distance - CameraRadius);
            }
        }
        return Mathf.Max(0.3f, clear);
    }

    private static bool IsLoose(RaycastHit hit)
    {
        Rigidbody body = hit.rigidbody;
        return body != null && !body.isKinematic;
    }

    private void StartSpectating(Player body)
    {
        spectating = true;
        fadingOut = false;
        fadeOutAge = 0f;
        fadeInAge = 0f;
        spectateLabel.text = "SPECTATING  <color=#ff5a4a>" + (string.IsNullOrEmpty(killerName) ? "KILLER" : TextRules.Escape(killerName.ToUpper())) + "</color>";
        spectateLabel.gameObject.SetActive(true);
        // Straight to the spot behind them, under cover of the black
        Vector3 position, aimAt;
        SpectatePose(body, out position, out aimAt);
        transform.position = position;
        transform.rotation = Quaternion.LookRotation(aimAt - position, Vector3.up);
    }

    // Over the killer's shoulder, looking where they aim. When they die or leave: fade to the map view.
    private void Spectate(float dt)
    {
        Player body = KillerBody();
        if (body == null && !fadingOut) fadingOut = true;

        if (fadingOut)
        {
            fadeOutAge += dt;
            fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(fadeOutAge / FadeTime));
            if (fadeOutAge >= FadeTime) End();
            return;
        }

        fadeInAge += dt;
        fade.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(fadeInAge / FadeTime));

        SpectatePose(body, out Vector3 wanted, out Vector3 aimAt);
        float follow = 1f - Mathf.Exp(-10f * dt);
        transform.position = Vector3.Lerp(transform.position, wanted, follow);
        Vector3 look = aimAt - transform.position;
        if (look.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look, Vector3.up), 1f - Mathf.Exp(-12f * dt));
        }
    }

    // Where the spectator camera wants to be, and the point it looks at, for this body
    private static void SpectatePose(Player body, out Vector3 position, out Vector3 aimAt)
    {
        Transform t = body.transform;
        // Their aim: the body's turn, and the pitch they look up (+) or down (-)
        Quaternion aim = Quaternion.Euler(-body.Pitch, t.eulerAngles.y, 0f);
        Vector3 eye = t.position + Vector3.up * SpectateHeight;
        Vector3 forward = aim * Vector3.forward;
        aimAt = eye + forward * 25f;

        Vector3 flatForward = t.forward;
        flatForward.y = 0f;
        flatForward = flatForward.sqrMagnitude > 0.001f ? flatForward.normalized : Vector3.forward;
        Vector3 offset = -flatForward * SpectateDistance + t.right * SpectateSide + Vector3.up * 0.35f;

        // Not through walls: pulled in towards them if something is behind
        int mask = ~((1 << Layers.LocalPlayer) | (1 << Layers.RemotePlayer) | (1 << Layers.Gun) | (1 << Layers.LocalGun) | (1 << Layers.Hitbox) | (1 << Layers.Glass) | (1 << 2));
        float length = offset.magnitude;
        if (Physics.SphereCast(eye, 0.2f, offset / length, out RaycastHit hit, length, mask, QueryTriggerInteraction.Ignore))
        {
            offset = offset / length * Mathf.Max(0.4f, hit.distance - 0.1f);
        }
        position = eye + offset;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
