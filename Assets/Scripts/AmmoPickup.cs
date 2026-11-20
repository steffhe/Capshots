using UnityEngine;

// An ammo box lying on the ground. Every client creates its own copy when the
// Manager receives a SpawnPickup event. When the local player walks over it,
// the Manager asks the master client for it, so only one player can take it.
public class AmmoPickup : MonoBehaviour
{
    public int id;

    private Manager manager;
    private float expiresAt;
    private float nextClaimTime;
    private Vector3 basePosition;

    private const float PickupRadius = 1.6f;
    private const float ClaimRetryDelay = 1f;

    public static AmmoPickup Create(int id, Vector3 position, Manager manager, float lifetime)
    {
        GameObject root = new GameObject("Ammo Pickup " + id);
        root.transform.position = position;

        // Simple dark grey box made from a primitive, so no prefab or art is needed
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        // Remove the collider right away: a plain Destroy only happens at the end of the frame, and for that
        // one physics step the solid box would sit inside the dead player's body and launch it upwards.
        DestroyImmediate(box.GetComponent<Collider>());
        box.transform.SetParent(root.transform, false);
        box.transform.localScale = new Vector3(0.5f, 0.3f, 0.35f);
        Renderer boxRenderer = box.GetComponent<Renderer>();
        boxRenderer.sharedMaterial = BoxMaterial(boxRenderer.sharedMaterial);
        boxRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Soft glow so it's easy to spot
        Light glow = root.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.85f, 0.4f);
        glow.range = 2.5f;
        glow.intensity = 1.5f;
        glow.shadows = LightShadows.None;

        AmmoPickup pickup = root.AddComponent<AmmoPickup>();
        pickup.id = id;
        pickup.manager = manager;
        pickup.expiresAt = Time.time + lifetime;
        pickup.basePosition = position;
        return pickup;
    }

    private static Material boxMaterial;

    // One shared dark grey URP material for every ammo box
    private static Material BoxMaterial(Material fallback)
    {
        if (boxMaterial != null) return boxMaterial;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        boxMaterial = lit != null ? new Material(lit) : new Material(fallback);
        boxMaterial.name = "Ammo Box";

        Color darkGrey = new Color(0.2f, 0.2f, 0.2f);
        if (boxMaterial.HasProperty("_BaseColor")) boxMaterial.SetColor("_BaseColor", darkGrey);
        if (boxMaterial.HasProperty("_Color")) boxMaterial.SetColor("_Color", darkGrey);
        if (boxMaterial.HasProperty("_BaseMap")) boxMaterial.SetTexture("_BaseMap", null);
        if (boxMaterial.HasProperty("_Smoothness")) boxMaterial.SetFloat("_Smoothness", 0.3f);
        return boxMaterial;
    }

    private void Update()
    {
        if (Time.time >= expiresAt)
        {
            if (manager != null) manager.RemovePickup(id);
            Destroy(gameObject);
            return;
        }

        // Spin and bob
        transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * 2.5f) * 0.1f);
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);

        Player local = Player.Local;
        if (local == null || manager == null || Time.time < nextClaimTime) return;

        Weapon weapon = local.GetComponent<Weapon>();
        if (weapon == null || !weapon.NeedsAmmo()) return;

        if (Vector3.Distance(local.transform.position, basePosition) <= PickupRadius)
        {
            nextClaimTime = Time.time + ClaimRetryDelay;
            manager.ClaimPickup_S(id);
        }
    }
}
