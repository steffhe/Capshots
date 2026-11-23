using UnityEngine;

// One form of the player's transforming gun (pistol, machine gun, shotgun, sniper): its settings,
// set up as an asset in Assets/ScriptableObjects/Guns. Every form fires from the same energy pool
// (see Weapon and GunEnergy).

[CreateAssetMenu(fileName ="New Gun", menuName ="Gun")]
public class Gun : ScriptableObject
{
    public string gunName;
    public float fireRate;
    public float bloom;
    public float recoil;
    public float kickback;
    public float aimSpeed;
    public int damage;
    // Energy each shot takes from the shared pool (100 when full)
    public float energyPerShot = 10f;
    public int burst;  // 0 semi - 1 auto - 2 burst
    public int burstCount = 3; // shots per trigger pull in burst mode
    // Projectiles per shot. More than 1 makes a shotgun: each pellet gets its own spread (bloom) and does
    // damage on its own; the pellets that hit one player are added up into a single hit.
    public int pellets = 1;
    public GameObject prefab;
    public Texture2D image;
    public AudioClip shootingSound;

    [Header("Damage")]
    public float headshotMultiplier = 2f;
    // Full damage up to falloffStart metres, dropping to minDamageMultiplier at falloffEnd and beyond
    public float falloffStart = 30f;
    public float falloffEnd = 60f;
    [Range(0f, 1f)] public float minDamageMultiplier = 1f;

    [Header("Accuracy")]
    // Spread is divided by this while aiming
    public float aimBloomDivisor = 2f;
    // Spread multiplier at full running speed (scaled by how fast you move)
    public float movingBloomMultiplier = 1.5f;

    [Header("Aiming")]
    // Field of view while aiming with a scope. 0 = no scope: aiming zooms in a little (Player.AimFovMultiplier).
    public float scopeFov = 0f;
    // Field of view of the gun model itself while aiming. A small value enlarges the gun, bringing
    // a scope up to the eye so you aim through it. 0 = keep the normal viewmodel field of view.
    public float scopeViewmodelFov = 0f;

    [Header("Movement")]
    // Movement speed multiplier while this gun is equipped, and while aiming with it
    public float moveSpeedMultiplier = 1f;
    public float aimMoveSpeedMultiplier = 0.7f;

    // Damage for a hit at the given distance, before any other modifiers
    public int DamageAt(float distance, bool headshot)
    {
        float falloff = 1f;
        if (falloffEnd > falloffStart)
        {
            float t = Mathf.InverseLerp(falloffStart, falloffEnd, distance);
            falloff = Mathf.Lerp(1f, minDamageMultiplier, t);
        }

        float multiplier = headshot ? headshotMultiplier : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(damage * falloff * multiplier));
    }
}
