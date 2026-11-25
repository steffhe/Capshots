using System.Collections.Generic;
using UnityEngine;

// Reuses scorch marks and laser beams instead of making and destroying them for every shot. A
// shotgun blast is 8 of each and the machine gun fires 40 times a second, so in a full room that
// churn of objects (and the garbage it leaves) cost frames, most of all in the browser.
// It's made on the first shot and belongs to the map's scene, so it goes with the map and the next
// map starts with a fresh one.
public class ShotEffects : MonoBehaviour
{
    // Enough for a long firefight; past this, the oldest mark is moved to the newest spot
    private const int MaxMarks = 96;
    private const float MarkLife = 5f;

    private static ShotEffects instance;

    private readonly GameObject[] marks = new GameObject[MaxMarks];
    private readonly float[] markExpiry = new float[MaxMarks];
    private int nextMark;

    private readonly Stack<BeamFade> freeBeams = new Stack<BeamFade>();

    private static ShotEffects Instance
    {
        get
        {
            if (instance == null) instance = new GameObject("Shot Effects").AddComponent<ShotEffects>();
            return instance;
        }
    }

    // A scorch mark at point, facing out along normal, for 5 seconds
    public static void Mark(GameObject prefab, Vector3 point, Vector3 normal)
    {
        if (prefab == null) return;
        ShotEffects fx = Instance;

        int i = fx.nextMark;
        fx.nextMark = (i + 1) % MaxMarks;

        GameObject mark = fx.marks[i];
        if (mark == null)
        {
            mark = Instantiate(prefab, fx.transform);
            fx.marks[i] = mark;
        }
        mark.transform.position = point + normal * 0.001f;
        mark.transform.LookAt(point + normal);
        mark.SetActive(true);
        fx.markExpiry[i] = Time.time + MarkLife;
    }

    // A laser beam set up like template, on the given layer, ready for its positions and colour.
    // Call Begin on it to start the fade; it comes back here when it's done.
    public static BeamFade Beam(LineRenderer template, int layer)
    {
        ShotEffects fx = Instance;

        BeamFade beam = null;
        // A beam can be destroyed along with its scene, so skip any that are gone
        while (beam == null && fx.freeBeams.Count > 0) beam = fx.freeBeams.Pop();
        if (beam == null)
        {
            GameObject beamObject = new GameObject("Laser");
            beamObject.transform.SetParent(fx.transform, false);
            beamObject.AddComponent<LineRenderer>();
            beam = beamObject.AddComponent<BeamFade>();
        }

        beam.gameObject.layer = layer;
        Weapon.CopyLineSettings(template, beam.Line);
        beam.gameObject.SetActive(true);
        return beam;
    }

    // Puts a finished beam away, or removes it if it didn't come from here
    public static void Release(BeamFade beam)
    {
        if (instance != null && beam.transform.parent == instance.transform)
        {
            beam.gameObject.SetActive(false);
            instance.freeBeams.Push(beam);
        }
        else
        {
            Destroy(beam.gameObject);
        }
    }

    void Update()
    {
        float now = Time.time;
        for (int i = 0; i < MaxMarks; i++)
        {
            GameObject mark = marks[i];
            if (mark != null && mark.activeSelf && now >= markExpiry[i]) mark.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
