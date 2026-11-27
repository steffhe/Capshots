using System.Collections;
using UnityEngine;

// Hover, take-off, landing and reload sounds for a player. Added by Player at runtime, so no prefab changes are needed.
// The characters float rather than walk, so moving plays a continuous UFO-like hum and woosh that rises in
// pitch and volume with speed, instead of footsteps.
// Runs on every client: the hover sound for remote players is worked out from how they move on this machine,
// so they need no network traffic. Reload sounds are started by Weapon through an RPC.
// All sounds are synthesized once on first use, so no audio files need to be imported.
public class PlayerSounds : MonoBehaviour
{
    // Mix levels, set from loudness measurements of each synthesized clip (A-weighted) rather than by ear.
    // Target, relative to a sniper shot: shotgun -1.5 dB, pistol -3.5, machine gun -6, reload -12 to -15, landing -14,
    // hover while sprinting about -18, while walking about -22. UI sounds in FeedbackUI sit at -8 to -10.
    public float hoverVolume = 0.04f;
    public float landingVolume = 0.06f;
    // A jump's thrust. Set by ear just under the landing, not yet measured like the levels above.
    public float thrustVolume = 0.045f;
    public float reloadVolume = 0.027f;
    // Gunshots: this is the sniper rifle, the loudest sound in the game. The other guns are scaled from it.
    // Low, because the sustained energy zaps sound much louder than their peak level suggests.
    public float shotVolume = 0.16f;
    // Slower than this (m/s) the hover sound fades out, so small corrections stay quiet
    private const float MinHoverSpeed = 1f;
    // Speed (m/s) where the hover sound reaches full strength
    private const float FullHoverSpeed = 5f;
    // Seconds for the hover sound to swell in when moving off and to die away when stopping
    private const float HoverAttack = 0.25f;
    private const float HoverRelease = 0.5f;
    // Seconds over which the measured speed is evened out before it drives the sound
    private const float SpeedSmoothing = 0.12f;
    // Falling faster than this (m/s) plays a landing sound when touching the ground
    private const float MinLandingSpeed = 3f;
    // Landing faster than this (m/s) plays the landing sound at its loudest
    private const float HardLandingSpeed = 15f;
    // Leaving the ground going up faster than this (m/s) is a jump: the thrusters fire
    private const float MinThrustSpeed = 5f;

    private Player player;
    private AudioSource movementSource, hoverSource, reloadSource, morphSource;

    private Vector3 lastPosition;
    private float verticalSpeed;
    // The fastest fall (m/s, negative) since leaving the ground: how hard the landing is
    private float fallSpeed;
    private bool wasGrounded = true;
    private float hoverIntensity, hoverPitch = HoverPitchSlow, hoverLoudness, smoothedSpeed;
    private Coroutine reloadRoutine;

    private static AudioClip hoverClip;
    private static AudioClip thrustClip, landingClip, magOutClip, magInClip, boltClip, lockClip;
    private static AudioClip[] pistolShots, machineGunShots, sniperShots, shotgunShots;
    private AudioSource shotSource;

    // Each form's mechanism, heard after (or with) its shots: the shotgun's pump, the sniper rifle's
    // capacitor recharging until it can fire again, the pistol's slide snapping, and the machine
    // gun's rotary barrel whirring up with sustained fire (rotorClip, looped)
    private static AudioClip pumpClip, rechargeClip, snapClip, rotorClip;
    private AudioSource cycleSource, rotorSource;
    private Coroutine cycleRoutine;
    // How fast the machine gun's barrel is spinning, 0 to 1: up with each shot, winding down after
    private float rotorLevel;
    // The machine gun's shots take turns, so the rhythm stays even and no clip plays twice in a row
    private int nextMachineGunShot;
    // Mechanical sounds against the shot itself (they sit well under it)
    public float pumpVolume = 0.5f, rechargeVolume = 0.22f, snapVolume = 0.32f, rotorVolume = 0.16f;
    private const float RotorPerShot = 0.12f, RotorWindDown = 1.8f;

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
        // Its own source, so stopping a reload's sounds (on a change of form) never cuts it short
        morphSource = MakeSource();
        shotSource = MakeSource();
        // Shots keep a plain linear fade, but carry much further than the hover sound
        shotSource.rolloffMode = AudioRolloffMode.Linear;
        shotSource.minDistance = 2f;
        shotSource.maxDistance = 150f;
        // Mechanical sounds are quieter and fade out sooner, like the reload
        cycleSource = MakeSource();
        rotorSource = MakeSource();
        rotorSource.clip = rotorClip;
        rotorSource.loop = true;
        rotorSource.volume = 0f;

        lastPosition = transform.position;
    }

    // After Fusion has placed the body for this frame, so the measured movement is this frame's
    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f) return;

        UpdateRotor();

        Vector3 position = transform.position;
        Vector3 delta = position - lastPosition;
        lastPosition = position;

        // Big jumps are respawns or network snaps, not movement
        if (delta.sqrMagnitude > 25f) return;

        verticalSpeed = delta.y / Time.deltaTime;

        bool grounded = IsGrounded();

        if (!grounded) fallSpeed = Mathf.Min(fallSpeed, verticalSpeed);
        if (grounded && !wasGrounded && fallSpeed < -MinLandingSpeed)
        {
            // Harder landings are louder: a hop is soft, a drop off a roof thumps
            float hardness = Mathf.InverseLerp(MinLandingSpeed, HardLandingSpeed, -fallSpeed);
            Play(movementSource, landingClip, landingVolume * Mathf.Lerp(0.7f, 1.3f, hardness), 1.05f - 0.1f * hardness, 1.1f - 0.1f * hardness);
        }
        if (!grounded && wasGrounded && verticalSpeed > MinThrustSpeed)
        {
            // Jumping: the hover thrusters push off, heard by everyone nearby
            Play(movementSource, thrustClip, thrustVolume, 0.95f, 1.05f);
        }
        if (grounded) fallSpeed = 0f;
        wasGrounded = grounded;

        UpdateHover(player.HorizontalSpeed);
    }

    private const float HoverPitchSlow = 0.85f, HoverPitchFast = 1.15f;

    // The hover sound keeps playing in the air too, since the characters float anyway.
    // Crouching (and sliding, which crouches) is silent, so sneaking up on someone still works.
    private void UpdateHover(float horizontalSpeed)
    {
        float dt = Time.deltaTime;
        // Even out the speed first, so changing direction or the odd uneven tick doesn't dip the sound
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, horizontalSpeed, 1f - Mathf.Exp(-dt / SpeedSmoothing));

        float target = player.IsCrouched ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(MinHoverSpeed, FullHoverSpeed, smoothedSpeed));
        // From just under walking speed to just under sprinting speed (Player's own speeds)
        float fast = Mathf.InverseLerp(player.WalkSpeed * 0.9f, player.SprintSpeed * 0.95f, smoothedSpeed);

        // Smoothed, so jittery speeds (especially for remote players) don't make the sound flutter
        hoverIntensity = Mathf.Lerp(hoverIntensity, target, 1f - Mathf.Exp(-dt / (target > hoverIntensity ? HoverAttack : HoverRelease)));
        // Faster movement raises the pitch, like an engine working harder; only while moving, so it
        // doesn't bend down as the sound fades out
        if (target > 0f)
        {
            float smooth = 1f - Mathf.Exp(-dt / 0.5f);
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
        // Any scenery, like the simulation (Player.OnGround), so landing on a crate sounds like landing on a floor
        return Physics.Raycast(origin.position + Vector3.up * 0.1f, Vector3.down, 0.35f, player.ground | Player.WorldMask, QueryTriggerInteraction.Ignore);
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
        Play(reloadSource, magInClip, reloadVolume * 1.1f, 0.97f, 1.03f);
        yield return new WaitForSeconds(Mathf.Max(0f, reloadTime * 0.95f - reloadTime * 0.55f - 0.05f));
        Play(reloadSource, boltClip, reloadVolume * 1f, 0.97f, 1.03f);
        reloadRoutine = null;
    }

    private static void Play(AudioSource source, AudioClip clip, float volume, float minPitch, float maxPitch)
    {
        // PlayOneShot uses the source's pitch, so vary it per sound for a bit of variety
        source.pitch = Random.Range(minPitch, maxPitch);
        source.PlayOneShot(clip, volume);
    }

    // The gun's parts locking into place at the end of a change of form (GunModel), on every client
    public void PlayLock()
    {
        if (morphSource == null || lockClip == null) return;
        Play(morphSource, lockClip, reloadVolume * 1.1f, 0.95f, 1.05f);
    }

    private enum ShotKind { Pistol, MachineGun, Shotgun, Sniper }

    private static ShotKind KindOf(Gun gun)
    {
        string gunName = (gun.gunName ?? "").ToLowerInvariant();
        if (gunName.Contains("sniper")) return ShotKind.Sniper;
        if (gunName.Contains("shotgun") || gun.pellets > 1) return ShotKind.Shotgun;
        if (gunName.Contains("machine") || gun.burst == 1) return ShotKind.MachineGun;
        return ShotKind.Pistol;
    }

    // Called on every client for each shot this player fires
    public void PlayShot(Gun gun)
    {
        if (gun == null) return;
        ShotKind kind = KindOf(gun);

        // A clip assigned on the gun asset still wins, so real recordings can be dropped in later
        if (gun.shootingSound != null)
        {
            shotSource.pitch = 1f;
            shotSource.PlayOneShot(gun.shootingSound, shotVolume);
        }
        else
        {
            // Each gun's volume evens out how loud its clip sounds (measured, A-weighted) to its place in the mix
            AudioClip clip;
            float volume = shotVolume;
            switch (kind)
            {
                case ShotKind.Sniper: clip = sniperShots[Random.Range(0, sniperShots.Length)]; break;
                case ShotKind.Shotgun: clip = shotgunShots[Random.Range(0, shotgunShots.Length)]; volume *= 1.3f; break;
                case ShotKind.MachineGun:
                    clip = machineGunShots[nextMachineGunShot];
                    nextMachineGunShot = (nextMachineGunShot + 1) % machineGunShots.Length;
                    volume *= 0.77f;
                    break;
                default: clip = pistolShots[Random.Range(0, pistolShots.Length)]; volume *= 0.71f; break;
            }

            // Pitch stays at 1: changing it would bend the tails of earlier shots still ringing out
            shotSource.pitch = 1f;
            shotSource.PlayOneShot(clip, volume);
        }

        PlayMechanism(kind, gun);
    }

    // The form's mechanism after the shot
    private void PlayMechanism(ShotKind kind, Gun gun)
    {
        switch (kind)
        {
            case ShotKind.Shotgun:
                // In time with the pump plates racking back and forward (GunSatelliteLayout.Pump)
                PlayCycle(pumpClip, 0.13f, shotVolume * pumpVolume);
                break;
            case ShotKind.Sniper:
                // Finishes with its ready ping just as the rifle can fire again
                PlayCycle(rechargeClip, Mathf.Max(0.05f, gun.fireRate - rechargeClip.length), shotVolume * rechargeVolume);
                break;
            case ShotKind.Pistol:
                Play(cycleSource, snapClip, shotVolume * snapVolume, 0.96f, 1.04f);
                break;
            case ShotKind.MachineGun:
                rotorLevel = Mathf.Min(1f, rotorLevel + RotorPerShot);
                break;
        }
    }

    private void PlayCycle(AudioClip clip, float delay, float volume)
    {
        StopCycle();
        cycleRoutine = StartCoroutine(CycleAfter(clip, delay, volume));
    }

    private IEnumerator CycleAfter(AudioClip clip, float delay, float volume)
    {
        yield return new WaitForSeconds(delay);
        Play(cycleSource, clip, volume, 0.98f, 1.02f);
        cycleRoutine = null;
    }

    // Cuts a pump or recharge still to come or playing, e.g. on a change of form
    public void StopCycle()
    {
        if (cycleRoutine != null) StopCoroutine(cycleRoutine);
        cycleRoutine = null;
        if (cycleSource != null) cycleSource.Stop();
    }

    // The machine gun's rotary barrel: spins up with each shot and winds down after, rising in pitch
    // as it speeds up. Paused while still, so idle players cost no audio voice.
    private void UpdateRotor()
    {
        if (rotorSource == null) return;
        rotorLevel = Mathf.MoveTowards(rotorLevel, 0f, Time.deltaTime * RotorWindDown * Mathf.Max(0.35f, rotorLevel));
        if (rotorLevel <= 0.005f)
        {
            if (rotorSource.isPlaying) rotorSource.Pause();
            return;
        }
        if (!rotorSource.isPlaying) rotorSource.Play();
        float level = Mathf.SmoothStep(0f, 1f, rotorLevel);
        rotorSource.volume = shotVolume * rotorVolume * level;
        rotorSource.pitch = Mathf.Lerp(0.7f, 1.25f, level);
    }

    #region Synthesized clips

    private static void BuildClips()
    {
        if (hoverClip != null) return;

        hoverClip = MakeHoverLoop();

        // Landing: the hover field cushions the drop, a soft puff of air with a low whomp
        {
            System.Random rng = new System.Random(7);
            float lowpass = 0f, lowpass2 = 0f;
            landingClip = MakeClip("Landing", 0.35f, t =>
            {
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lowpass += (noise - lowpass) * 0.1f;
                lowpass2 += (lowpass - lowpass2) * 0.1f;
                float puff = lowpass2 * 6f * Mathf.Min(1f, t / 0.015f) * Decay(t, 11f);
                float whomp = Tone(t, 55f + 40f * Decay(t, 20f)) * Mathf.Min(1f, t / 0.01f) * Decay(t, 14f) * 0.6f;
                return puff + whomp;
            });
        }

        // Jumping: the thrusters kick, a short rush of air over a low tone that rises as the body lifts
        {
            System.Random rng = new System.Random(11);
            float lowpass = 0f, lowpass2 = 0f;
            thrustClip = MakeClip("Thrust", 0.3f, t =>
            {
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lowpass += (noise - lowpass) * 0.22f;
                lowpass2 += (lowpass - lowpass2) * 0.22f;
                float rush = lowpass2 * 4f * Mathf.Min(1f, t / 0.01f) * Decay(t, 10f);
                float lift = Tone(t, 80f + 140f * (1f - Decay(t, 9f))) * Mathf.Min(1f, t / 0.008f) * Decay(t, 12f) * 0.5f;
                return rush + lift;
            });
        }

        // Reloading an energy weapon: the cell powers down, a new one charges up, then a ready chirp
        magOutClip = MakeSweep("Power Down", 0.28f, 900f, 220f, 0.6f, 0.5f, 12f, 6f,
            (t, x) => Mathf.Min(1f, t / 0.005f) * Mathf.Pow(1f - x, 1.5f));
        magInClip = MakeSweep("Charge Up", 0.32f, 280f, 1500f, 1.6f, 0.35f, 8f, 30f,
            (t, x) => (0.3f + 0.7f * x) * Mathf.Min(1f, (0.32f - t) / 0.02f));
        boltClip = MakeClip("Ready", 0.2f, t =>
        {
            float first = t < 0.09f ? Tone(t, 1320f) * Mathf.Min(1f, t / 0.003f) * (t < 0.05f ? 1f : Decay(t - 0.05f, 80f)) : 0f;
            float second = t >= 0.08f ? Tone(t, 1980f) * Decay(t - 0.08f, 30f) : 0f;
            return (first + second) * 0.8f;
        });

        // The gun's parts locking at the end of a change of form: two quick metal clicks (the parts
        // seating, then the clamps closing) over a low thunk, and a short rising chirp as the energy settles
        {
            System.Random rng = new System.Random(11);
            lockClip = MakeClip("Lock", 0.24f, t =>
            {
                float v = 0f;
                for (int i = 0; i < 2; i++)
                {
                    float at = i * 0.032f, level = i == 0 ? 1f : 0.6f;
                    if (t < at) continue;
                    float c = t - at;
                    float noise = (float)rng.NextDouble() * 2f - 1f;
                    v += level * (noise * Decay(c, 260f) * 0.7f
                        + Tone(c, 2900f + 300f * i) * Decay(c, 48f) * 0.3f
                        + Tone(c, 4400f) * Decay(c, 70f) * 0.15f
                        + Tone(c, 150f - 30f * i) * Mathf.Min(1f, c / 0.002f) * Decay(c, 30f) * 0.6f);
                }
                if (t > 0.07f)
                {
                    float c = t - 0.07f;
                    v += Tone(c, 700f + 900f * Mathf.Min(1f, c / 0.08f)) * Mathf.Min(1f, c / 0.01f) * Decay(c, 22f) * 0.18f;
                }
                return v * 0.8f;
            });
        }

        // Gunshots: energy weapons. A few variations each, so rapid fire doesn't sound like one sample on repeat.
        pistolShots = new AudioClip[3];
        machineGunShots = new AudioClip[4];
        sniperShots = new AudioClip[2];
        shotgunShots = new AudioClip[2];
        for (int i = 0; i < pistolShots.Length; i++)
            // A bright "pew": a fast falling zap with a little punch
            pistolShots[i] = MakeLaser("Pistol Shot " + i, 200 + i, 0.3f, 2600f, 420f, 28f, 14f, 0.3f, 140f, 22f, 120f, 0.15f, 9f, 1, 0f, 1f + i * 0.04f);
        for (int i = 0; i < machineGunShots.Length; i++)
            // Short, buzzy pulses, so they don't smear at a high fire rate
            machineGunShots[i] = MakeLaser("Machine Gun Shot " + i, 300 + i, 0.18f, 1700f, 360f, 45f, 26f, 0.6f, 95f, 30f, 150f, 0.1f, 16f, 1, 0f, 1f + i * 0.035f);
        for (int i = 0; i < sniperShots.Length; i++)
            // Railgun: a huge sweep from a shriek down to a rumble, with a long crackling tail
            sniperShots[i] = MakeLaser("Sniper Shot " + i, 400 + i, 1.3f, 4200f, 110f, 10f, 3.2f, 0.5f, 55f, 6f, 60f, 0.5f, 2.6f, 2, 0.012f, 1f + i * 0.05f);
        for (int i = 0; i < shotgunShots.Length; i++)
            // Plasma blast: several detuned, gritty zaps at once over a heavy thump and a hissing tail
            shotgunShots[i] = MakeLaser("Shotgun Shot " + i, 500 + i, 0.6f, 2000f, 180f, 22f, 8f, 0.7f, 70f, 10f, 40f, 0.7f, 6f, 3, 0.03f, 1f + i * 0.05f);

        BuildMechanismClips();
    }

    // An energy weapon shot: a zap sweeping down from startFreq to endFreq (buzz adds a gritty, squarer
    // edge, extra voices detuned from each other make it thicker), a low thump for punch, a short noise
    // crackle at the start and a muffled, fluttering plasma tail. pitchScale shifts the zap slightly so
    // the variations differ.
    private static AudioClip MakeLaser(string name, int seed, float length,
        float startFreq, float endFreq, float sweep, float decay, float buzz,
        float thumpFreq, float thumpDecay, float crackleDecay,
        float tailLevel, float tailDecay, int voices, float detune, float pitchScale)
    {
        const int rate = 44100;
        System.Random rng = new System.Random(seed);
        int samples = Mathf.CeilToInt(length * rate);
        float[] data = new float[samples];
        float[] phases = new float[voices];
        float thumpPhase = 0f, crackleFilter = 0f, tailFilter = 0f, tailFilter2 = 0f, peak = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;

            // Phases are integrated, so the sweeps stay smooth
            float freq = Mathf.Lerp(endFreq, startFreq, Decay(t, sweep)) * pitchScale;
            float zap = 0f;
            for (int v = 0; v < voices; v++)
            {
                phases[v] += 2f * Mathf.PI * freq * (1f + detune * v) / rate;
                float sine = Mathf.Sin(phases[v]);
                zap += (sine + buzz * (float)System.Math.Tanh(4f * sine)) / (1f + buzz);
            }
            zap = zap / voices * Decay(t, decay);

            thumpPhase += 2f * Mathf.PI * thumpFreq * (1f + Decay(t, 30f)) / rate;
            float thump = Mathf.Sin(thumpPhase) * Decay(t, thumpDecay);

            crackleFilter += (noise - crackleFilter) * 0.5f;
            float crackle = crackleFilter * Decay(t, crackleDecay);

            tailFilter += (noise - tailFilter) * 0.08f;
            tailFilter2 += (tailFilter - tailFilter2) * 0.08f;
            float tail = tailFilter2 * 8f * tailLevel * Decay(t, tailDecay) * (0.6f + 0.4f * Tone(t, 23f));

            // Soft clip, so the parts blend into one sound
            float sample = (float)System.Math.Tanh(zap * 0.9f + thump * 0.7f + crackle * 0.5f + tail);
            float edge = Mathf.Clamp01(i / 8f) * Mathf.Clamp01((samples - i) / 1024f);
            data[i] = sample * edge;
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }

        // Same peak level for every clip; the volumes in PlayShot even out how loud each one sounds
        if (peak > 0f)
        {
            float gain = 0.9f / peak;
            for (int i = 0; i < samples; i++) data[i] *= gain;
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // A tone sliding from startFreq to endFreq (shape > 1 starts slowly, < 1 starts fast), with some buzz and a
    // tremolo whose speed changes from tremoloStart to tremoloEnd. envelope(t, x) gets the time and 0..1 progress.
    private static AudioClip MakeSweep(string name, float length, float startFreq, float endFreq, float shape,
        float buzz, float tremoloStart, float tremoloEnd, System.Func<float, float, float> envelope)
    {
        float phase = 0f;
        return MakeClip(name, length, t =>
        {
            float x = t / length;
            float freq = Mathf.Lerp(startFreq, endFreq, Mathf.Pow(x, shape));
            phase += 2f * Mathf.PI * freq / 44100f;
            float sine = Mathf.Sin(phase);
            float tone = (sine + buzz * (float)System.Math.Tanh(4f * sine)) / (1f + buzz);
            float tremolo = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(tremoloStart, tremoloEnd, x) * t);
            return tone * tremolo * envelope(t, x);
        });
    }

    // The hover sound: a seamless 2 second loop of a low humming drone, a theremin-like tone that wobbles
    // and glides, a softly breathing ring-modulated shimmer, a faint high sparkle and a soft airy woosh. Every tone fits a whole number of cycles into the loop, and
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

            // 330 Hz with a gentle 5 Hz vibrato, gliding about 30 Hz up and down over the loop: the flying-saucer theremin
            float warble = Mathf.Sin(2f * Mathf.PI * 330f * t + 1.5f * Tone(t, 5f) + 60f * Tone(t, 0.5f)) * (0.88f + 0.12f * Tone(t, 1f));

            // Ring modulation gives a metallic, electronic shimmer, breathing slowly rather than pulsing
            float ring = Tone(t, 880f) * Tone(t, 73f) * (0.8f + 0.2f * Tone(t, 1.5f));

            // Two high tones 1.5 Hz apart, a faint twinkling sparkle on top
            float sparkle = (Tone(t, 1760f) + Tone(t, 1761.5f)) * 0.5f;

            // Band-passed noise (the difference of two low-pass filters) for the rushing air
            fastFilter += (noise - fastFilter) * 0.12f;
            slowFilter += (noise - slowFilter) * 0.02f;
            float swell = 0.85f + 0.15f * Tone(t, 1f) * Mathf.Sin(2f * Mathf.PI * 0.5f * t + 1f);
            float woosh = (fastFilter - slowFilter) * swell * 2.2f;

            raw[i] = hum * 0.4f + warble * 0.26f + ring * 0.08f + sparkle * 0.03f + woosh * 0.3f;
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

    // Each form's mechanism (see PlayMechanism)
    private static void BuildMechanismClips()
    {
        // Shotgun pump: a heavy "chk" as the plates rack back, then a sharper "clack" as they slam
        // forward, each a burst of metal over a low knock
        {
            System.Random rng = new System.Random(41);
            float[] at = { 0f, 0.3f };
            float[] level = { 0.8f, 1f };
            float[] knock = { 95f, 130f };
            float[] ring = { 1900f, 2600f };
            pumpClip = MakeClip("Pump", 0.5f, t =>
            {
                float v = 0f;
                for (int i = 0; i < 2; i++)
                {
                    if (t < at[i]) continue;
                    float c = t - at[i];
                    float noise = (float)rng.NextDouble() * 2f - 1f;
                    v += level[i] * (noise * Decay(c, 140f) * 0.6f
                        + Tone(c, ring[i]) * Decay(c, 45f) * 0.25f
                        + Tone(c, knock[i]) * Mathf.Min(1f, c / 0.002f) * Decay(c, 28f) * 0.7f);
                }
                return v * 0.85f;
            });
        }

        // Sniper capacitor: a thin whine rising as it charges, with a slight shimmer, ending in a
        // clean two-note "ready" ping
        {
            const float length = 0.8f, chargeEnd = 0.68f;
            float phase = 0f;
            rechargeClip = MakeClip("Recharge", length, t =>
            {
                float v = 0f;
                if (t < chargeEnd)
                {
                    float x = t / chargeEnd;
                    float freq = Mathf.Lerp(600f, 2400f, x * x);
                    phase += 2f * Mathf.PI * freq / 44100f;
                    float swell = Mathf.Min(1f, t / 0.05f) * Mathf.Lerp(0.25f, 0.6f, x) * Mathf.Min(1f, (chargeEnd - t) / 0.02f);
                    v += Mathf.Sin(phase) * swell * (0.85f + 0.15f * Tone(t, 31f));
                }
                if (t >= chargeEnd)
                {
                    float c = t - chargeEnd;
                    v += (Tone(c, 2640f) * Decay(c, 30f) + (c > 0.045f ? Tone(c, 3520f) * Decay(c - 0.045f, 26f) : 0f)) * 0.7f;
                }
                return v;
            });
        }

        // Pistol slide: one tight, bright metallic snap
        {
            System.Random rng = new System.Random(53);
            snapClip = MakeClip("Snap", 0.09f, t =>
            {
                float noise = (float)rng.NextDouble() * 2f - 1f;
                return noise * Decay(t, 320f) * 0.7f + Tone(t, 3400f) * Decay(t, 90f) * 0.35f + Tone(t, 180f) * Decay(t, 60f) * 0.4f;
            });
        }

        // Machine gun rotor: a whirring hum chopped by the spinning slats. Half a second whose tones all
        // fit whole cycles into it, so it loops without a click.
        rotorClip = MakeClip("Rotor", 0.5f, t =>
        {
            float hum = Tone(t, 110f) * 0.45f + Tone(t, 220f) * 0.3f + Tone(t, 440f) * 0.12f + Tone(t, 1320f) * 0.05f;
            float chop = 0.6f + 0.4f * Tone(t, 26f);
            return hum * chop * 0.9f;
        }, edges: false);
    }

    // edges: a tiny fade in and out to avoid clicks (off for a seamless loop)
    private static AudioClip MakeClip(string name, float length, System.Func<float, float> wave, bool edges = true)
    {
        const int rate = 44100;
        int samples = Mathf.CeilToInt(length * rate);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            float edge = edges ? Mathf.Clamp01(i / 32f) * Mathf.Clamp01((samples - i) / 256f) : 1f;
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
