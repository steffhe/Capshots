using System.Collections.Generic;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

// The player's hitboxes in Assets/Resources/Player.prefab. A body box that reaches into the head
// sphere sticks out in front of it (the box is wider than the head), so shots at the bottom of the
// head would hit the body box first and count as body shots instead of cap shots.
public class HitboxLayoutTests
{
    private static GameObject LoadPlayer()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Player.prefab");
        Assert.IsNotNull(prefab, "Player prefab not found");
        return prefab;
    }

    private static bool IsHead(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            if (t.CompareTag("Head")) return true;
        }
        return false;
    }

    private static Vector3 Centre(Hitbox h) => h.transform.position + h.transform.rotation * h.Offset;

    [Test]
    public void Hitboxes_HaveNoScale()
    {
        // The checks below (and the sizes set on the hitboxes) assume world scale 1
        foreach (Hitbox h in LoadPlayer().GetComponentsInChildren<Hitbox>(true))
        {
            Vector3 s = h.transform.lossyScale;
            Assert.AreEqual(1f, s.x, 1e-3f, h.transform.parent.name);
            Assert.AreEqual(1f, s.y, 1e-3f, h.transform.parent.name);
            Assert.AreEqual(1f, s.z, 1e-3f, h.transform.parent.name);
        }
    }

    [Test]
    public void BodyBoxes_StopBelowTheHead()
    {
        // Standing and crouching each have a body box and a head sphere, under their own body part
        var bodies = new Dictionary<Transform, Hitbox>();
        var heads = new Dictionary<Transform, Hitbox>();
        foreach (Hitbox h in LoadPlayer().GetComponentsInChildren<Hitbox>(true))
        {
            if (IsHead(h.transform))
            {
                Assert.AreEqual(HitboxTypes.Sphere, h.Type, "head hitbox");
                // Head sits under Standing/Head or Crouching/Head
                heads[h.transform.parent.parent] = h;
            }
            else
            {
                Assert.AreEqual(HitboxTypes.Box, h.Type, "body hitbox");
                bodies[h.transform.parent] = h;
            }
        }

        Assert.AreEqual(2, bodies.Count, "standing and crouching body hitboxes");
        foreach (KeyValuePair<Transform, Hitbox> pair in bodies)
        {
            Assert.IsTrue(heads.TryGetValue(pair.Key, out Hitbox head), "no head hitbox for " + pair.Key.name);
            float bodyTop = Centre(pair.Value).y + pair.Value.BoxExtents.y;
            float headBottom = Centre(head).y - head.SphereRadius;
            Assert.LessOrEqual(bodyTop, headBottom + 1e-3f, pair.Key.name);
            // ...but still reaches it, so there's no gap at the neck
            Assert.Greater(bodyTop, headBottom - 0.01f, pair.Key.name);
        }
    }
}
