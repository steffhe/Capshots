using System.Collections.Generic;
using UnityEngine;

// The transformation between two forms of the player's one gun (pistol, machine gun, shotgun, sniper).
// Each form is its own model (Assets/Resources), built from boxes under Anchor/Design. Parts with the
// same name in both forms (Body, Grip, Under, Front Post, ...) slide, turn and stretch from the old
// shape into the new one. Parts only the old form has are copied over and retract into the new body;
// parts only the new form has extend out of it. In three overlapping steps: the old form's extras
// fold away, the shared parts reshape, then the new form's extras come out.
// Added by Weapon to the new model when the equipped form changes; removes itself when done.
public class GunMorph : MonoBehaviour
{
    // Names of copied parts on their way out, so they aren't matched again by a quick second switch
    private const string RetractingSuffix = " (retracting)";

    private struct Part
    {
        public Transform transform;
        public Vector3 fromPosition, toPosition;
        public Quaternion fromRotation, toRotation;
        public Vector3 fromScale, toScale;
        public float start, end; // the share of the morph this part moves in
        public bool temporary;   // a copy of an old part, removed at the end
        // One of the gun's machined parts (GunModel): moved in stages, with a lift as it comes
        // loose and a small overshoot as it locks into place
        public bool staged;
        public Vector3 release;
    }

    private readonly List<Part> parts = new List<Part>();
    private GunModel newModel, oldModel;

    // The stages of a change of form for the machined parts (GunModel), as shares of it. The glow
    // follows the same stages (GunModel.MorphGlow):
    //  1. energy activation (0 to 0.2): the core flares, nothing moves yet
    //  2. mechanical release (0.1 to 0.3): clamps open, modules and the front assembly lift away
    //  3. reconfiguration (0.2 to 0.8): every part slides to where the new form has it
    //  4. mechanical lock (0.8 to 0.9): the parts overshoot a touch and seat, the lift closes up
    //  5. energy stabilisation (0.9 to 1): the glow settles
    private const float StagedStart = 0.2f, StagedEnd = 0.8f, LiftStart = 0.1f, LiftEnd = 0.9f;

    // How far through the change of form it is, 0 to 1 (GunModel's glow follows it)
    public float Progress => duration > 0f ? Mathf.Clamp01((Time.time - startTime) / duration) : 1f;
    private float startTime;
    private float duration;

    // Starts reshaping 'newGun' out of 'oldGun', over 'seconds'. Call before 'oldGun' is destroyed.
    public static GunMorph Begin(GameObject newGun, GameObject oldGun, float seconds)
    {
        Transform newDesign = Design(newGun);
        Transform oldDesign = Design(oldGun);
        if (newDesign == null || oldDesign == null || seconds <= 0f) return null;

        // Start where the old gun was held, so the gun doesn't jump between hip and aim poses
        Transform newAnchor = newGun.transform.Find("Anchor");
        Transform oldAnchor = oldGun.transform.Find("Anchor");
        if (newAnchor != null && oldAnchor != null) newAnchor.position = oldAnchor.position;

        GunMorph morph = newGun.AddComponent<GunMorph>();
        morph.duration = seconds;
        morph.startTime = Time.time;
        morph.newModel = newGun.GetComponent<GunModel>();
        morph.oldModel = oldGun.GetComponent<GunModel>();
        morph.Build(newDesign, oldDesign);
        morph.Apply(0f);
        return morph;
    }

    private static Transform Design(GameObject gun)
    {
        if (gun == null) return null;
        Transform design = gun.transform.Find("Anchor/Design");
        return design != null ? design : gun.transform;
    }

    private void Build(Transform newDesign, Transform oldDesign)
    {
        // The old form's parts, by name, in its own design space
        var oldParts = new Dictionary<string, Renderer>();
        foreach (Renderer r in oldDesign.GetComponentsInChildren<Renderer>(true))
        {
            if (r is MeshRenderer && !oldParts.ContainsKey(r.name)) oldParts[r.name] = r;
        }

        // The new body's box, for parts to retract into and extend from
        Transform body = newDesign.Find("Body");
        Bounds bodyBox = body != null
            ? new Bounds(DesignPosition(newDesign, body), DesignScale(newDesign, body))
            : new Bounds(Vector3.zero, Vector3.zero);

        var matched = new HashSet<string>();
        foreach (Renderer r in newDesign.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is MeshRenderer)) continue;
            Transform t = r.transform;
            Part part = new Part
            {
                transform = t,
                toPosition = t.localPosition,
                toRotation = t.localRotation,
                toScale = t.localScale,
            };

            if (oldParts.TryGetValue(r.name, out Renderer old))
            {
                matched.Add(r.name);
                // Reshape from where the same part sits on the old form
                part.fromPosition = ToLocal(newDesign, t.parent, DesignPosition(oldDesign, old.transform));
                part.fromRotation = Quaternion.Inverse(t.parent.rotation) * newDesign.rotation * DesignRotation(oldDesign, old.transform);
                part.fromScale = Divide(DesignScale(oldDesign, old.transform), DesignScale(newDesign, t.parent));
                part.start = 0.15f;
                part.end = 0.85f;
                if (newModel != null && t.parent != null && t.parent.name == GunModel.GroupName)
                {
                    part.staged = true;
                    part.start = StagedStart;
                    part.end = StagedEnd;
                    // Lifts the way it sits in the new form, or in the old one if it's tucking away
                    part.release = !newModel.IsCollapsed(r.name) || oldModel == null ? newModel.ReleaseOf(r.name) : oldModel.ReleaseOf(r.name);
                }
            }
            else
            {
                // Extend out of the body
                Vector3 designPosition = DesignPosition(newDesign, t);
                part.fromPosition = ToLocal(newDesign, t.parent, bodyBox.size == Vector3.zero ? designPosition : bodyBox.ClosestPoint(designPosition));
                part.fromRotation = part.toRotation;
                part.fromScale = Vector3.zero;
                part.start = 0.5f;
                part.end = 1f;
            }
            parts.Add(part);
        }

        // Old parts the new form doesn't have: copies that retract into the new body and are removed
        foreach (var entry in oldParts)
        {
            if (matched.Contains(entry.Key)) continue;
            Transform old = entry.Value.transform;
            GameObject copy = Instantiate(old.gameObject, newDesign, false);
            copy.name = old.name.EndsWith(RetractingSuffix) ? old.name : old.name + RetractingSuffix;
            foreach (Transform child in copy.transform) Destroy(child.gameObject);

            Vector3 from = DesignPosition(oldDesign, old);
            Part part = new Part
            {
                transform = copy.transform,
                fromPosition = from,
                fromRotation = DesignRotation(oldDesign, old),
                fromScale = DesignScale(oldDesign, old),
                toPosition = bodyBox.size == Vector3.zero ? from : bodyBox.ClosestPoint(from),
                toScale = Vector3.zero,
                start = 0f,
                end = 0.5f,
                temporary = true,
            };
            part.toRotation = part.fromRotation;
            parts.Add(part);
        }
    }

    private void Update()
    {
        float t = duration > 0f ? (Time.time - startTime) / duration : 1f;
        if (t >= 1f)
        {
            Finish();
            return;
        }
        Apply(t);
    }

    private void Apply(float t)
    {
        foreach (Part p in parts)
        {
            if (p.transform == null) continue;
            float k = Mathf.InverseLerp(p.start, p.end, t);
            if (p.staged)
            {
                // Overshoots a touch and settles: the lock
                float eased = EaseOutBack(k);
                float lift = Mathf.Sin(Mathf.PI * Mathf.InverseLerp(LiftStart, LiftEnd, t));
                p.transform.localPosition = Vector3.LerpUnclamped(p.fromPosition, p.toPosition, eased) + p.release * lift;
                p.transform.localRotation = Quaternion.SlerpUnclamped(p.fromRotation, p.toRotation, eased);
                p.transform.localScale = Vector3.Max(Vector3.zero, Vector3.LerpUnclamped(p.fromScale, p.toScale, eased));
                continue;
            }
            k = Mathf.SmoothStep(0f, 1f, k);
            p.transform.localPosition = Vector3.LerpUnclamped(p.fromPosition, p.toPosition, k);
            p.transform.localRotation = Quaternion.Slerp(p.fromRotation, p.toRotation, k);
            p.transform.localScale = Vector3.LerpUnclamped(p.fromScale, p.toScale, k);
        }
    }

    private void Finish()
    {
        foreach (Part p in parts)
        {
            if (p.transform == null) continue;
            if (p.temporary)
            {
                Destroy(p.transform.gameObject);
                continue;
            }
            p.transform.localPosition = p.toPosition;
            p.transform.localRotation = p.toRotation;
            p.transform.localScale = p.toScale;
        }
        parts.Clear();
        Destroy(this);
    }

    // Puts every part in its final place for a moment, e.g. to measure the finished shape, then back
    public void ShowFinalShape(bool final)
    {
        if (final)
        {
            foreach (Part p in parts)
            {
                if (p.transform == null) continue;
                p.transform.localPosition = p.toPosition;
                p.transform.localRotation = p.toRotation;
                p.transform.localScale = p.toScale;
            }
        }
        else
        {
            Apply(duration > 0f ? Mathf.Clamp01((Time.time - startTime) / duration) : 1f);
        }
    }

    // From 0 to 1 with a small overshoot past 1 just before the end
    private static float EaseOutBack(float k)
    {
        const float c1 = 1.2f, c3 = c1 + 1f;
        float m = k - 1f;
        return 1f + c3 * m * m * m + c1 * m * m;
    }

    #region Design space

    // The gun's parts sit under Anchor/Design, possibly in groups (Sights, Reticle). These convert
    // between a part's own parent and the design's space, so parts in different groups can be matched.

    private static Vector3 DesignPosition(Transform design, Transform t) => design.InverseTransformPoint(t.position);

    private static Quaternion DesignRotation(Transform design, Transform t) => Quaternion.Inverse(design.rotation) * t.rotation;

    private static Vector3 DesignScale(Transform design, Transform t) => Divide(t.lossyScale, design.lossyScale);

    private static Vector3 ToLocal(Transform design, Transform parent, Vector3 designPosition)
    {
        return parent.InverseTransformPoint(design.TransformPoint(designPosition));
    }

    private static Vector3 Divide(Vector3 a, Vector3 b)
    {
        return new Vector3(b.x != 0f ? a.x / b.x : a.x, b.y != 0f ? a.y / b.y : a.y, b.z != 0f ? a.z / b.z : a.z);
    }

    #endregion
}
