using System.Collections;
using UnityEngine;

// Hover, landing and reload sounds for a player. Added by Player at runtime, so no prefab changes are needed.
// The characters float rather than walk, so moving plays a continuous UFO-like hum and woosh that rises in
// pitch and volume with speed, instead of footsteps.
// Runs on every client: the hover sound for remote players is worked out from how they move on this machine,
// so they need no network traffic. Reload sounds are started by Weapon through an RPC.
// All sounds are synthesized once on first use, so no audio files need to be imported.
public class PlayerSounds : MonoBehaviour
{
    // Mix levels, set from loudness measurements of each synthesized clip (A-weighted) rather than by ear.
    // Target, relative to a sniper shot: pistol -3 dB, machine gun -5, reload -12 to -15, landing -14,
    // hover while sprinting about -18, while walking about -22. UI sounds in FeedbackUI sit at -8 to -10.
    public float hoverVolume = 0.04f;
    public float landingVolume = 0.093f;
    public float reloadVolume = 0.1f;
    // Gunshots: this is the sniper rifle, the loudest sound in the game. The other guns are scaled from it.
    // Kept well below the old imported shot, which was about 18 dB louder and clipped.
    public float shotVolume = 0.52f;
    // Slower than this (m/s) the hover sound fades out, so small corrections stay quiet
    private const float MinHoverSpeed = 1f;
    // Speed (m/s) where the hover sound reaches full strength
    private const float FullHoverSpeed = 5f;
    // Seconds for the hover sound to swell in when moving off and to die away when stopping
    private const float HoverAttack = 0.15f;
    private const float HoverRelease = 0.35f;
    // Falling faster than this (m/s) plays a landing sound when touching the ground
    private const float MinLandingSpeed = 3f;

    private Player player;
    private AudioSource movementSource, hoverSource, reloadSource;

    private Vector3 lastPosition;
    private float verticalSpeed;
    private bool wasGrounded = true;
    private float hoverIntensity, hoverPitch = HoverPitchSlow, hoverLoudness;
    private Coroutine reloadRoutine;

    private static AudioClip hoverClip;
    private static AudioClip landingClip, magOutClip, magInClip, boltClip;
    private static AudioClip[] pistolShots, machineGunShots, sniperShots;
    private AudioSource shotSource;

    private void Awake()
    {
        player = GetComponent<Player>();
        BuildClips();

        // Separate sources, because changing a source's pitch also bends sounds it is still playing
        movementSource = MakeSource();
        hoverSource = MakeSource();
        hoverSource.clip = hoverClip;
        hoverSource.loop = true;
        hoverSource.volume = 0f;
        // Start each player somewhere else in the loop, so several hovering players don't play in lockstep
        hoverSource.timeSamples = Random.Range(0, hoverClip.samples);
        hoverSource.Play();
        reloadSource = MakeSource();
        shotSource = MakeSource();
        // Shots keep a plain linear fade, but carry much further than the hover sound
        shotSource.rolloffMode = AudioRolloffMode.Linear;
        shotSource.minDistance = 2f;
        shotSource.maxDistance = 150f;

        lastPosition = transform.position;
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f) return;

        Vector3 position = transform.position;
        Vector3 delta = position - lastPosition;
        lastPosition = position;

        // Big jumps are respawns or network snaps, not movement
        if (delta.sqrMagnitude > 25f) return;

        verticalSpeed = delta.y / Time.deltaTime;
        delta.y = 0f;
        float horizontalSpeed = delta.magnitude / Time.deltaTime;

        bool grounded = IsGrounded();

        if (grounded && !wasGrounded && verticalSpeed < -MinLandingSpeed)
        {
            Play(movementSource, landingClip, landingVolume, 0.95f, 1.05f);
        }
        wasGrounded = grounded;

        UpdateHover(horizontalSpeed);
    }

    private const float HoverPitchSlow = 0.85f, HoverPitchFast = 1.15f;

    // The hover sound keeps playing in the air too, since the characters float anyway.
    // Crouching (and sliding, which crouches) is silent, so sneaking up on someone still works.
    private void UpdateHover(float horizontalSpeed)
    {
        float target = player.IsCrouched ? 0f : Mathf.InverseLerp(MinHoverSpeed, FullHoverSpeed, horizontalSpeed);
        float fast = Mathf.InverseLerp(3f, 10f, horizontalSpeed);
        float dt = Time.deltaTime;

        // Smoothed, so jittery speeds (especially for remote players) don't make the sound flutter
        hoverIntensity = Mathf.Lerp(hoverIntensity, target, 1f - Mathf.Exp(-dt / (target > hoverIntensity ? HoverAttack : HoverRelease)));
        // Faster movement raises the pitch, like an engine working harder; only while moving, so it
        // doesn't bend down as the sound fades out
        if (target > 0f)
        {
            float smooth = 1f - Mathf.Exp(-dt / 0.3f);
            hoverPitch = Mathf.Lerp(hoverPitch, Mathf.Lerp(HoverPitchSlow, HoverPitchFast, fast), smooth);
            // Sprinting is about 4 dB louder than walking
            hoverLoudness = Mathf.Lerp(hoverLoudness, Mathf.Lerp(0.76f, 1.21f, fast), smooth);
        }

        hoverSource.pitch = hoverPitch;
        // Curved, so the sound fades in and out softly instead of popping up at low speeds
        hoverSource.volume = hoverVolume * hoverLoudness * hoverIntensity * Mathf.Sqrt(hoverIntensity);
    }

    private AudioSource MakeSource()
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 1f; // 3D: other players are heard from where they are
        s.dopplerLevel = 0f;
        // Hover, landings and reloads: a soft fade with a long, quiet tail instead of a straight
        // line that drops to silence at a fixed distance
        s.rolloffMode = AudioRolloffMode.Custom;
        s.maxDistance = FoleyMaxDistance;
        s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, FoleyRolloff());
        return s;
    }

    // Where the hover sound, landings and reloads fade out completely
    private const float FoleyMaxDistance = 45f;
    private static AnimationCurve foleyRolloff;

    // Volume at a distance in metres: full within 2 m, then falling off like a real sound
    // (about -5 dB at 10 m, -11 dB at 20 m, -15 dB at 30 m) and easing to silence between 30 and 45 m.
    private static float FoleyVolume(float distance)
    {
        float falloff = 1f / (1f + Mathf.Pow(Mathf.Max(0f, distance - 2f) / 10f, 1.5f));
        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, FoleyMaxDistance, distance));
        return falloff * fade;
    }

    // The same curve as an AnimationCurve, which Unity reads over 0..1 of maxDistance
    private static AnimationCurve FoleyRolloff()
    {
        if (foleyRolloff != null) return foleyRolloff;

        const int steps = 30;
        Keyframe[] keys = new Keyframe[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float x = (float)i / steps;
            float d = x * FoleyMaxDistance;
            // Slope from a small finite difference, so the curve between keys follows the function smoothly
            const float h = 0.05f;
            float slope = (FoleyVolume(d + h) - FoleyVolume(Mathf.Max(0f, d - h))) / (d + h - Mathf.Max(0f, d - h)) * FoleyMaxDistance;
            keys[i] = new Keyframe(x, FoleyVolume(d), slope, slope);
        }
        foleyRolloff = new AnimationCurve(keys);
        return foleyRolloff;
    }

    private bool IsGrounded()
    {
        Transform origin = player.groundDetector != null ? player.groundDetector : transform;
        return Physics.Raycast(origin.position + Vector3.up * 0.1f, Vector3.down, 0.35f, player.ground, QueryTriggerInteraction.Ignore);
    }

    // Called on every client when this player starts reloading
    public void PlayReload(float reloadTime)
    {
        StopReload();
        reloadRoutine = StartCoroutine(ReloadSequence(Mathf.Max(0.3f, reloadTime)));
    }

    // Called when a reload is cut short, e.g. by switching weapon
    public void StopReload()
    {
        if (reloadRoutine != null) StopCoroutine(reloadRoutine);
        reloadRoutine = null;
        if (reloadSource != null) reloadSource.Stop();
    }

    private IEnumerator ReloadSequence(float reloadTime)
    {
        Play(reloadSource, magOutClip, reloadVolume * 1f, 0.97f, 1.03f);
        yield return new WaitForSeconds(reloadTime * 0.55f);
        Play(reloadSource, magInClip, reloadVolume * 0.74f, 0.97f, 1.03f);
        yield return new WaitForSeconds(Mathf.Max(0f, reloadTime * 0.95f - reloadTime * 0.55f - 0.05f));
        Play(reloadSource, boltClip, reloadVolume * 0.86f, 0.97f, 1.03f);
        reloadRoutine = null;
    }

    private static void Play(AudioSource source, AudioClip clip, float volume, float minPitch, float maxPitch)
    {
        // PlayOneShot uses the source's pitch, so vary it per sound for a bit of variety
        source.pitch = Random.Range(minPitch, maxPitch);
        source.PlayOneShot(clip, volume);
    }

    // Called on every client for each shot this player fires
    public void PlayShot(Gun gun)
    {
        if (gun == null) return;

        // A clip assigned on the gun asset still wins, so real recordings can be dropped in later
        if (gun.shootingSound != null)
        {
            shotSource.pitch = 1f;
            shotSource.PlayOneShot(gun.shootingSound, shotVolume);
            return;
        }

        AudioClip[] clips;
        float volume = shotVolume;
        string gunName = (gun.gunName ?? "").ToLowerInvariant();
        if (gunName.Contains("sniper")) { clips = sniperShots; }
        else if (gunName.Contains("machine") || gun.burst == 1) { clips = machineGunShots; volume *= 0.55f; }
        else { clips = pistolShots; volume *= 0.49f; }

        // Pitch stays at 1: changing it would bend the tails of earlier shots still ringing out
        shotSource.pitch = 1f;
        shotSource.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
    }

    #region Synthesized clips

    private static void BuildClips()
    {
        if (hoverClip != null) return;

        hoverClip = MakeHoverLoop();

        {
            System.Random rng = new System.Random(7);
            float lowpass = 0f;
            landingClip = MakeClip("Landing", 0.28f, t =>
            {
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lowpass += (noise - lowpass) * 0.15f;
                return Tone(t, Mathf.Lerp(90f, 50f, t / 0.28f)) * Decay(t, 16f) + lowpass * Decay(t, 18f) * 1.6f;
            });
        }

        // Magazine sliding out: a click then a short metallic scrape
        {
            System.Random rng = new System.Random(11);
            magOutClip = MakeClip("Mag Out", 0.22f, t =>
            {
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float click = Tone(t, 2400f) * Decay(t, 160f) * 0.7f;
                float scrape = t > 0.03f ? noise * 0.25f * Decay(t - 0.03f, 18f) * (0.6f + 0.4f * Tone(t, 31f)) : 0f;
                return click + scrape;
            });
        }

        // Magazine seated: a firm double clack
        {
            System.Random rng = new System.Random(13);
            magInClip = MakeClip("Mag In", 0.16f, t =>
            {
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float first = (Tone(t, 1700f) * 0.6f + noise * 0.4f) * Decay(t, 90f);
                float second = t > 0.045f ? (Tone(t, 1300f) * 0.7f + noise * 0.4f) * Decay(t - 0.045f, 70f) : 0f;
                return first + second + Tone(t, 160f) * Decay(t, 40f) * 0.4f;
            });
        }

        // Gunshots: a few variations each, so rapid fire doesn't sound like one sample on repeat
        pistolShots = new AudioClip[3];
        machineGunShots = new AudioClip[4];
        sniperShots = new AudioClip[2];
        for (int i = 0; i < pistolShots.Length; i++)
            // Sharp, snappy crack with a short body
            pistolShots[i] = MakeShot("Pistol Shot " + i, 200 + i, 0.4f, 0.55f, 32f, 170f, 75f, 22f, 0.12f, 9f, 0.35f, 1f + i * 0.04f);
        for (int i = 0; i < machineGunShots.Length; i++)
            // Short and punchy, lower and grittier, so it doesn't smear at a high fire rate
            machineGunShots[i] = MakeShot("Machine Gun Shot " + i, 300 + i, 0.28f, 0.4f, 45f, 130f, 60f, 30f, 0.1f, 14f, 0.25f, 1f + i * 0.035f);
        for (int i = 0; i < sniperShots.Length; i++)
            // Loud crack, heavy boom and a long rolling tail
            sniperShots[i] = MakeShot("Sniper Shot " + i, 400 + i, 1.4f, 0.7f, 55f, 95f, 38f, 7f, 0.04f, 2.8f, 0.6f, 1f + i * 0.05f);

        // Charging handle / slide: pulled back then snapped forward
        {
            System.Random rng = new System.Random(17);
            boltClip = MakeClip("Bolt", 0.2f, t =>
            {
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float back = (Tone(t, 2000f) * 0.5f + noise * 0.3f) * Decay(t, 110f);
                float forward = t > 0.09f ? (Tone(t, 2600f) * 0.6f + noise * 0.5f) * Decay(t - 0.09f, 80f) : 0f;
                return back + forward;
            });
        }
    }

    // A gunshot: a bright noise crack, a low thump falling in pitch and a muffled echo tail.
    // pitchScale shifts the whole shot slightly so the variations differ.
    private static AudioClip MakeShot(string name, int seed, float length,
        float crackBrightness, float crackDecay,
        float thumpStart, float thumpEnd, float thumpDecay,
        float tailBrightness, float tailDecay, float tailLevel, float pitchScale)
    {
        const int rate = 44100;
        System.Random rng = new System.Random(seed);
        int samples = Mathf.CeilToInt(length * rate);
        float[] data = new float[samples];
        float crackFilter = 0f, tailFilter = 0f, tailFilter2 = 0f, phase = 0f, peak = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;

            crackFilter += (noise - crackFilter) * crackBrightness;
            float crack = crackFilter * Decay(t, crackDecay) * 1.4f;

            // Thump with a pitch sweep, integrated so the sweep stays smooth
            float freq = Mathf.Lerp(thumpEnd, thumpStart, Decay(t, 25f)) * pitchScale;
            phase += 2f * Mathf.PI * freq / rate;
            float thump = Mathf.Sin(phase) * Decay(t, thumpDecay);

            // Two filter stages for a dull, distant-sounding reflection
            tailFilter += (noise - tailFilter) * tailBrightness;
            tailFilter2 += (tailFilter - tailFilter2) * tailBrightness;
            float tail = tailFilter2 * 6f * tailLevel * Decay(t, tailDecay) * Mathf.Clamp01(t * 40f);

            // Soft clip, like a real shot overloading slightly
            float sample = (float)System.Math.Tanh(crack + thump + tail);
            float edge = Mathf.Clamp01(i / 8f) * Mathf.Clamp01((samples - i) / 1024f);
            data[i] = sample * edge;
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }

        // Same peak level for every clip, so loudness is set only by the volume settings
        if (peak > 0f)
        {
            float gain = 0.9f / peak;
            for (int i = 0; i < samples; i++) data[i] *= gain;
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // The hover sound: a seamless 2 second loop of a low humming drone, a theremin-like tone that wobbles
    // and glides, a pulsing ring-modulated shimmer, a faint high sparkle and a soft airy woosh. Every tone fits a whole number of cycles into the loop, and
    // the noise is crossfaded from the end into the start, so it repeats without a click.
    private static AudioClip MakeHoverLoop()
    {
        const int rate = 44100;
        const float length = 2f;
        int samples = Mathf.RoundToInt(length * rate);
        int fade = rate / 4;
        float[] raw = new float[samples + fade];
        System.Random rng = new System.Random(31);
        float fastFilter = 0f, slowFilter = 0f;

        for (int i = 0; i < raw.Length; i++)
        {
            float t = (float)i / rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;

            // Drone: the two upper voices are half a hertz apart from the root's harmonics, so it slowly beats
            float hum = Tone(t, 55f) * 0.55f + Tone(t, 110.5f) * 0.35f + Tone(t, 165f) * 0.12f;

            // 330 Hz with a 6 Hz vibrato, gliding about 30 Hz up and down over the loop: the flying-saucer theremin
            float warble = Mathf.Sin(2f * Mathf.PI * 330f * t + 4f * Tone(t, 6f) + 60f * Tone(t, 0.5f)) * (0.7f + 0.3f * Tone(t, 1.5f));

            // Ring modulation gives a metallic, electronic shimmer, pulsing 8 times a second
            float ring = Tone(t, 880f) * Tone(t, 73f) * (0.55f + 0.45f * Tone(t, 8f));

            // Two high tones 1.5 Hz apart, a faint twinkling sparkle on top
            float sparkle = (Tone(t, 1760f) + Tone(t, 1761.5f)) * 0.5f;

            // Band-passed noise (the difference of two low-pass filters) for the rushing air
            fastFilter += (noise - fastFilter) * 0.12f;
            slowFilter += (noise - slowFilter) * 0.02f;
            float swell = 0.65f + 0.35f * Tone(t, 1f) * Mathf.Sin(2f * Mathf.PI * 0.5f * t + 1f);
            float woosh = (fastFilter - slowFilter) * swell * 2.2f;

            raw[i] = hum * 0.4f + warble * 0.3f + ring * 0.12f + sparkle * 0.04f + woosh * 0.3f;
        }

        float[] data = new float[samples];
        float peak = 0f;
        for (int i = 0; i < samples; i++)
        {
            // The first part fades in from the extra samples past the end, which continue on from the last sample
            data[i] = i < fade ? Mathf.Lerp(raw[samples + i], raw[i], (float)i / fade) : raw[i];
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }

        // Same peak level as the other synthesized sounds, so hoverVolume alone sets how loud it is
        if (peak > 0f)
        {
            float gain = 0.9f / peak;
            for (int i = 0; i < samples; i++) data[i] *= gain;
        }

        AudioClip clip = AudioClip.Create("Hover", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip MakeClip(string name, float length, System.Func<float, float> wave)
    {
        const int rate = 44100;
        int samples = Mathf.CeilToInt(length * rate);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            // Tiny fade in and out to avoid clicks
            float edge = Mathf.Clamp01(i / 32f) * Mathf.Clamp01((samples - i) / 256f);
            data[i] = Mathf.Clamp(wave(t), -1f, 1f) * edge;
        }
        AudioClip clip = AudioClip.Create(name, samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float Tone(float t, float frequency) => Mathf.Sin(2f * Mathf.PI * frequency * t);
    private static float Decay(float t, float speed) => Mathf.Exp(-t * speed);

    #endregion
}
