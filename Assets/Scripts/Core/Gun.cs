using UnityEngine;

// A gun's settings, set up as an asset in Assets/ScriptableObjects/Guns, plus its ammo state.
// Each player works on their own copy (see Weapon.Awake).

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
    public int ammo;
    public int clipSize;
    public float reloadTime;
    public int burst;  // 0 semi - 1 auto - 2 burst
    public int burstCount = 3; // shots per trigger pull in burst mode
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
    // Field of view while aiming with a scope. 0 = no scope: aiming zooms to a quarter of the normal field of view.
    public float scopeFov = 0f;
    // Field of view of the gun model itself while aiming. A small value enlarges the gun, bringing
    // a scope up to the eye so you aim through it. 0 = keep the normal viewmodel field of view.
    public float scopeViewmodelFov = 0f;

    [Header("Movement")]
    // Movement speed multiplier while this gun is equipped, and while aiming with it
    public float moveSpeedMultiplier = 1f;
    public float aimMoveSpeedMultiplier = 0.7f;

    private int clip; //current in clip
    private int stash; //current ammo

    public void Initialize()
    {
        stash = ammo;
        clip = clipSize;
    }

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

    public bool FireBullet()
    {
        if (clip> 0)
        {
            clip -= 1;
            return true;
        }
        else
        {
            return false;
        }
    }

    public void Reload()
    {
        stash += clip;
        clip = Mathf.Min(clipSize, stash);
        stash -= clip;
    }

    public bool CanReload()
    {
        return clip < clipSize && stash > 0;
    }

    // Adds reserve ammo, up to the gun's starting amount. Returns true if anything was added.
    public bool AddAmmo(int amount)
    {
        int before = stash;
        stash = Mathf.Min(ammo, stash + amount);
        return stash > before;
    }

    public bool NeedsAmmo()
    {
        return stash < ammo;
    }

    public int GetStash()
    {
        return stash;
    }

    public int GetClip()
    {
        return clip;
    }


}
