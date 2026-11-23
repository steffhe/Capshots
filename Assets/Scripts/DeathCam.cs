using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// What you see right after dying: instead of cutting straight to the map view, a camera starts where
// your eyes were, pulls back and follows your head as it flies off (or your cap, when a headshot
// knocked it off) for a moment, then fades through black to the map view for the rest of the wait
// to respawn. Purely local presentation; nothing here is networked.
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

        if (target == null)
        {
            targetWaited += dt;
            if (targetWaited > TargetWait) End();
            return;
        }

        // Fade to black, then hand over to the map view (which shows once this camera is gone)
        if (!fadingOut && age >= LingerTime) fadingOut = true;
        if (fadingOut)
        {
            fadeOutAge += dt;
            fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(fadeOutAge / FadeTime));
            if (fadeOutAge >= FadeTime)
            {
                End();
                return;
            }
        }

        // Drift back and up from where the eyes were, keeping the head in view and not going through walls
        Vector3 focus = target.position;
        Vector3 wanted = focus + back * Distance + Vector3.up * Height;
        Vector3 toWanted = wanted - focus;
        int mask = ~((1 << Layers.LocalPlayer) | (1 << Layers.RemotePlayer) | (1 << Layers.Gun) | (1 << Layers.LocalGun) | (1 << Layers.Hitbox) | (1 << 2));
        if (Physics.SphereCast(focus, 0.25f, toWanted.normalized, out RaycastHit hit, toWanted.magnitude, mask, QueryTriggerInteraction.Ignore))
        {
            wanted = focus + toWanted.normalized * Mathf.Max(0.6f, hit.distance - 0.1f);
        }

        float follow = 1f - Mathf.Exp(-3.5f * dt);
        transform.position = Vector3.Lerp(transform.position, wanted, follow);
        Vector3 look = focus - transform.position;
        if (look.sqrMagnitude > 0.0001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(look, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, 1f - Mathf.Exp(-6f * dt));
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
