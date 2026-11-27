using System.Collections;
using UnityEngine;

// Fades a menu screen or panel in over a moment each time it's shown (and, with scale, settles it
// from a touch smaller), so moving between screens reads as one motion instead of a cut. Uses
// unscaled time, so it works while the game is paused or frozen at the end of a match. Only ever
// changes the CanvasGroup's alpha and the object's scale; input works throughout. Runs as a short
// coroutine each time it's shown, so it costs nothing in between. Added with UITheme.Reveal.
// Local presentation only.
[RequireComponent(typeof(RectTransform))]
public class MenuReveal : MonoBehaviour
{
    public bool scale;

    private CanvasGroup group;
    private bool running;

    private const float Duration = 0.16f;
    private const float FromScale = 0.985f;

    private void OnEnable()
    {
        if (group == null)
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        }
        Apply(0f);
        running = true;
        StartCoroutine(Play());
    }

    private void OnDisable()
    {
        // Never leave it half faded (the coroutine stops with the object)
        if (running) Apply(1f);
        running = false;
    }

    private IEnumerator Play()
    {
        for (float time = 0f; time < Duration; time += Time.unscaledDeltaTime)
        {
            float k = time / Duration;
            Apply(1f - (1f - k) * (1f - k));
            yield return null;
        }
        Apply(1f);
        running = false;
    }

    private void Apply(float k)
    {
        group.alpha = k;
        if (scale) transform.localScale = Vector3.one * Mathf.Lerp(FromScale, 1f, k);
    }
}
