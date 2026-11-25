using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Cap Grab: an arrow at the edge of the screen for each dropped cap nearby that's out of view, pointing
// the way to it, so a cap behind you isn't missed. In the cap's colour, or white for your own (and in
// teams, a teammate's): one to get back. The arrows are part of the HUD, so they go away with it
// while dead and at the end of the match.
public partial class Manager
{
    // Caps further away than this get no arrow: the column of light is for finding those
    private const float CapArrowRange = 45f;
    // How far in from the screen's edges the arrows sit, in canvas units
    private const float CapArrowMargin = 70f;
    private const float CapArrowSize = 34f;

    private readonly List<Image> capArrows = new List<Image>();
    private static Sprite arrowSprite;

    // Every frame while alive, from Update
    private void UpdateCapArrows()
    {
        int used = 0;
        Player local = Player.Local;
        Camera cam = local != null ? local.normalCam : null;
        if (IsCapGrab && cam != null && Hud != null && Hud.activeInHierarchy)
        {
            RectTransform hud = (RectTransform)Hud.transform;
            Vector2 half = hud.rect.size / 2f - new Vector2(CapArrowMargin, CapArrowMargin);
            foreach (KeyValuePair<int, CapPickup> entry in caps)
            {
                CapPickup cap = entry.Value;
                if (cap == null) continue;
                Vector3 target = cap.Pivot != null ? cap.Pivot.position : cap.BasePosition;
                if (Vector3.Distance(cam.transform.position, target) > CapArrowRange) continue;

                Vector3 view = cam.WorldToViewportPoint(target);
                bool onScreen = view.z > 0f && view.x > 0.02f && view.x < 0.98f && view.y > 0.02f && view.y < 0.98f;
                if (onScreen) continue;

                // The way to it on screen, from the middle (flipped when it's behind the camera)
                Vector2 way = new Vector2(view.x - 0.5f, view.y - 0.5f);
                if (view.z < 0f) way = -way;
                if (way.sqrMagnitude < 0.0001f) way = Vector2.down;
                way.Normalize();
                // Out to the edge of the margin's rectangle
                float scale = Mathf.Min(Mathf.Abs(way.x) > 0.0001f ? half.x / Mathf.Abs(way.x) : float.MaxValue,
                                        Mathf.Abs(way.y) > 0.0001f ? half.y / Mathf.Abs(way.y) : float.MaxValue);

                Image arrow = CapArrow(used++, hud);
                bool ours = cap.OwnerActor == LocalActor || SameTeam(cap.OwnerActor, LocalActor);
                Color color = ours ? Color.white : PlayerCap.ColorFor(cap.OwnerActor);
                color.a = 0.9f;
                arrow.color = color;
                RectTransform rect = arrow.rectTransform;
                rect.anchoredPosition = way * scale;
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(way.y, way.x) * Mathf.Rad2Deg - 90f);
                arrow.gameObject.SetActive(true);
            }
        }
        for (int i = used; i < capArrows.Count; i++)
        {
            if (capArrows[i] != null && capArrows[i].gameObject.activeSelf) capArrows[i].gameObject.SetActive(false);
        }
    }

    private Image CapArrow(int index, RectTransform hud)
    {
        while (capArrows.Count <= index)
        {
            Image image = UITheme.NewImage("Cap Arrow", hud, Color.white, 0f);
            image.sprite = ArrowSprite();
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(CapArrowSize, CapArrowSize);
            UITheme.AddShadow(image, 0.6f);
            capArrows.Add(image);
        }
        return capArrows[index];
    }

    // A soft-edged triangle pointing up
    private static Sprite ArrowSprite()
    {
        if (arrowSprite != null) return arrowSprite;
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Cap Arrow";
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Inside a triangle with its point at the top middle and its base along the bottom
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float halfWidth = 0.46f * (1f - v);
                float edge = Mathf.Min(halfWidth - Mathf.Abs(u - 0.5f), v - 0.08f);
                float a = Mathf.Clamp01(edge * size * 0.6f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        arrowSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return arrowSprite;
    }
}
