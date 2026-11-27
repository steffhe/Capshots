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
    // Spread is divided by this while aiming...
    public float aimBloomDivisor = 2f;
    // ...once the gun has been aimed for this many seconds; it tightens steadily until then
    // (see ShotRules.AimProgress), so aiming and firing at the same moment isn't fully accurate
    public float adsTime = 0.15f;
    // Spread multiplier at full running speed (scaled by how fast you move)
    public float movingBloomMultiplier = 1.5f;

    [Header("Aiming")]
    // Field of view while aiming with a scope. 0 = no scope: aiming zooms in a little (Player.AimFovMultiplier).
    public float scopeFov = 0f;
    // Field of view of the gun model itself while aiming. A small value enlarges the gun, bringing
    // a scope up to the eye so you aim through it. 0 = keep the normal viewmodel field of view.
    public float scopeViewmodelFov = 0f;

    [Header("Feel")]
    // (recoil, above, is how far the gun model tips up with each shot, in degrees, and kickback how far
    // it's pushed back)
    // Camera recoil per shot, in degrees, halved while aiming (Weapon.aimingRecoilMultiplier). It moves
    // where you aim, then settles back at kickRecovery a second. -1 = the old rule: recoil times
    // Weapon.cameraRecoilScale up, and up to 35% of that sideways at random.
    public float kickUp = -1f;
    public float kickSide = -1f;
    public float kickRecovery = 7f;
    // The sideways kick follows the same path in every burst (ShotRules.RecoilPattern), so it can be
    // learnt and pulled against, instead of being random
    public bool patternedRecoil = false;
    // A shake of the view (degrees; the camera only, not where you aim) and a brief widening of the
    // field of view (degrees) with each shot. Both are off with Screen shake off in the settings.
    public float shake = 0f;
    public float fovPunch = 0f;
    // How far (HUD pixels) the spread ticks round the crosshair jump out with each shot
    public float crosshairKick = 5f;

    [Header("Muzzle flash and beam")]
    // The flash of light at the muzzle: brightness, reach (metres) and how long it takes to fade
    // (it always shows for at least one frame)
    public float flashIntensity = 3.75f;
    public float flashRange = 6f;
    public float flashTime = 0.04f;
    // A glowing core of plasma at the muzzle (metres across), 0 for none
    public float flashSize = 0f;
    // The laser beam to where each shot (or pellet) landed: width, times the gun's line, and fade time
    public float beamWidth = 1f;
    public float beamTime = 0.3f;
    // Sparks thrown off a wall where each shot (each pellet) lands
    public int impactSparks = 5;

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
