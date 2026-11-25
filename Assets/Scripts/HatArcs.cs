using UnityEngine;

// The plasma globe's lightning (see PlayerCap): bolts from the middle of the globe out to its glass,
// each in two crooked segments, jumping to new places several times a second. Only a look: nothing
// is sent.
public class HatArcs : MonoBehaviour
{
    private Transform[] segments; // two for each bolt: from the middle to a kink, and on to the glass
    private float radius, width;
    private float next;

    // segments: thin cylinders (Unity's, 2 tall) under this object, two for each bolt. radius: how
    // far the bolts reach from this object's centre (metres)
    public void Setup(Transform[] segments, float radius, float width)
    {
        this.segments = segments;
        this.radius = radius;
        this.width = width;
        Jump();
    }

    private void Update()
    {
        if (segments == null || Time.time < next) return;
        Jump();
    }

    private void Jump()
    {
        next = Time.time + Random.Range(0.05f, 0.14f);
        for (int i = 0; i + 1 < segments.Length; i += 2)
        {
            Vector3 end = Random.onUnitSphere * radius;
            Vector3 kink = end * Random.Range(0.35f, 0.65f) + Random.insideUnitSphere * (radius * 0.25f);
            Place(segments[i], Vector3.zero, kink);
            Place(segments[i + 1], kink, end);
        }
    }

    private void Place(Transform segment, Vector3 from, Vector3 to)
    {
        if (segment == null) return;
        Vector3 along = to - from;
        segment.localPosition = (from + to) * 0.5f;
        segment.localRotation = Quaternion.FromToRotation(Vector3.up, along);
        segment.localScale = new Vector3(width, along.magnitude * 0.5f, width);
    }
}
