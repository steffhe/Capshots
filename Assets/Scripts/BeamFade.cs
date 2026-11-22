using UnityEngine;

// Thins out and fades a laser beam, then removes it
public class BeamFade : MonoBehaviour
{
    private LineRenderer beam;
    private float width, duration, age;
    private MaterialPropertyBlock block;
    private Color color;

    public void Begin(LineRenderer line, float startWidth, float seconds)
    {
        beam = line;
        width = startWidth;
        duration = Mathf.Max(0.01f, seconds);
        block = new MaterialPropertyBlock();
        beam.GetPropertyBlock(block);
        color = block.GetColor("_BaseColor");
    }

    void Update()
    {
        if (beam == null) { Destroy(gameObject); return; }

        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        // Fades quickly at first, then lingers faintly
        float left = (1f - t) * (1f - t);
        beam.widthMultiplier = width * Mathf.Lerp(0.3f, 1f, left);
        Color c = color;
        c.a *= left;
        block.SetColor("_BaseColor", c);
        block.SetColor("_Color", c);
        beam.SetPropertyBlock(block);

        if (t >= 1f) Destroy(gameObject);
    }
}
