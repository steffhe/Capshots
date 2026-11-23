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
    }

    private readonly List<Part> parts = new List<Part>();
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
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(p.start, p.end, t));
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
