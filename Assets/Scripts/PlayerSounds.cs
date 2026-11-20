using System.Collections;
using UnityEngine;

// Footstep, landing and reload sounds for a player. Added by Player at runtime, so no prefab changes are needed.
// Runs on every client: footsteps for remote players are worked out from how they move on this machine,
// so they need no network traffic. Reload sounds are started by Weapon through an RPC.
// All sounds are synthesized once on first use, so no audio files need to be imported.
public class PlayerSounds : MonoBehaviour
{
    // Metres walked between two footsteps. Sprinting covers ground faster, so steps come quicker on their own.
    public float strideLength = 2.1f;
    public float footstepVolume = 0.45f;
    public float landingVolume = 0.6f;
    public float reloadVolume = 0.7f;
    // Slower than this (m/s) and steps aren't played, so small corrections don't tick
    private const float MinStepSpeed = 1f;
    // Falling faster than this (m/s) plays a landing sound when touching the ground
    private const float MinLandingSpeed = 3f;

    private Player player;
    private AudioSource movementSource, reloadSource;

    private Vector3 lastPosition;
    private float verticalSpeed;
    private float distanceSinceStep;
    private bool wasGrounded = true;
    private int lastFootstep = -1;
    private Coroutine reloadRoutine;

    private static AudioClip[] footstepClips;
    private static AudioClip landingClip, magOutClip, magInClip, boltClip;

    private void Awake()
    {
        player = GetComponent<Player>();
        BuildClips();

        // Separate sources, because changing a source's pitch also bends sounds it is still playing
        movementSource = MakeSource();
        reloadSource = MakeSource();

        lastPosition = transform.position;
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f) return;

        Vector3 position = transform.position;
        Vector3 delta = position - lastPosition;
        lastPosition = position;

        // Big jumps are respawns or network snaps, not walking
        if (delta.sqrMagnitude > 25f) { distanceSinceStep = 0f; return; }

        verticalSpeed = delta.y / Time.deltaTime;
        delta.y = 0f;
        float horizontalSpeed = delta.magnitude / Time.deltaTime;

        bool grounded = IsGrounded();

        if (grounded && !wasGrounded && verticalSpeed < -MinLandingSpeed)
        {
            Play(movementSource, landingClip, landingVolume, 0.95f, 1.05f);
            distanceSinceStep = 0f;
        }
        wasGrounded = grounded;

        // Crouching (and sliding, which crouches) is silent, so sneaking up on someone works
        if (!grounded || player.IsCrouched || horizontalSpeed < MinStepSpeed)
        {
            // Start the next walk with a step soon after moving off
            distanceSinceStep = Mathf.Min(distanceSinceStep, strideLength * 0.6f);
            return;
        }

        distanceSinceStep += delta.magnitude;
        if (distanceSinceStep >= strideLength)
        {
            distanceSinceStep -= strideLength;
            PlayFootstep(horizontalSpeed);
        }
    }

    private AudioSource MakeSource()
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 1f; // 3D: other players are heard from where they are
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 2f;
        s.maxDistance = 30f;
        s.dopplerLevel = 0f;
        return s;
    }

    private bool IsGrounded()
    {
        Transform origin = player.groundDetector != null ? player.groundDetector : transform;
        return Physics.Raycast(origin.position + Vector3.up * 0.1f, Vector3.down, 0.35f, player.ground, QueryTriggerInteraction.Ignore);
    }

    private void PlayFootstep(float speed)
    {
        // Never the same clip twice in a row
        int index = Random.Range(0, footstepClips.Length - 1);
        if (index >= lastFootstep) index++;
        index %= footstepClips.Length;
        lastFootstep = index;

        // Running is a bit louder than walking
        float loudness = Mathf.Lerp(0.8f, 1.15f, Mathf.InverseLerp(3f, 10f, speed));
        Play(movementSource, footstepClips[index], footstepVolume * loudness, 0.9f, 1.1f);
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
        Play(reloadSource, magOutClip, reloadVolume, 0.97f, 1.03f);
        yield return new WaitForSeconds(reloadTime * 0.55f);
        Play(reloadSource, magInClip, reloadVolume, 0.97f, 1.03f);
        yield return new WaitForSeconds(Mathf.Max(0f, reloadTime * 0.95f - reloadTime * 0.55f - 0.05f));
        Play(reloadSource, boltClip, reloadVolume, 0.97f, 1.03f);
        reloadRoutine = null;
    }

    private static void Play(AudioSource source, AudioClip clip, float volume, float minPitch, float maxPitch)
    {
        // PlayOneShot uses the source's pitch, so vary it per sound for a bit of variety
        source.pitch = Random.Range(minPitch, maxPitch);
        source.PlayOneShot(clip, volume);
    }

    #region Synthesized clips

    private static void BuildClips()
    {
        if (footstepClips != null) return;

        footstepClips = new AudioClip[4];
        for (int i = 0; i < footstepClips.Length; i++)
        {
            System.Random rng = new System.Random(100 + i);
            float body = 70f + i * 12f;     // low thud of the heel
            float grit = 0.35f + i * 0.08f; // how much the scuff comes through
            float lowpass = 0f;
            footstepClips[i] = MakeClip("Footstep " + i, 0.16f, t =>
            {
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lowpass += (noise - lowpass) * 0.25f; // soft, muffled scuff
                return Tone(t, body) * Decay(t, 35f) * 0.9f + lowpass * grit * Decay(t, 28f) * 2f;
            });
        }

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
