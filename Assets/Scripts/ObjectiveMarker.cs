using UnityEngine;

// A capture point a map has room for (the Test Site 07 map has A, B and C). There's no objective game
// mode yet: this only marks where the points go and how big they are, so a mode added later can find
// them with FindObjectsByType<ObjectiveMarker>. Nothing here runs in a match.
public class ObjectiveMarker : MonoBehaviour
{
    [Tooltip("The point's letter, as shown to players")]
    public string label = "A";
    [Tooltip("Capture radius in metres, around this transform (which sits on the floor)")]
    public float radius = 4f;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
        const int segments = 32;
        Vector3 previous = transform.position + new Vector3(radius, 0.05f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector3 next = transform.position + new Vector3(Mathf.Cos(a) * radius, 0.05f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 3f);
    }
}
