using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The end of match awards' music (see MatchMusic), made in code like the menu's (MenuMusic): a bright,
// triumphant loop in C major at 120 BPM, 16 bars (32 seconds) of C, G, Am, F, C, G, F, G, played
// twice. A wide pad and a pumping octave bass under four-on-the-floor drums with a clap, open hats
// and a crash at the top of each half; brass stabs on the chords, and the second time round a
// fanfare melody on top. Every note is written into the loop wrapping round its end, so the loop is
// seamless. Build makes it a note at a time (a step per frame), then Clip holds it.
public class VictoryMusic
{
    private const int Rate = 32000;
    private const float Bpm = 120f;
    private const int Bars = 16;
    private const float Beat = 60f / Bpm, Bar = Beat * 4f;

    // One chord a bar: the bass's note and the pad's (MIDI note numbers)
    private static readonly int[] Progression = { 0, 1, 2, 3, 0, 1, 3, 1 };
    private static readonly int[] BassNotes = { 36, 43, 45, 41 };
    private static readonly int[][] PadNotes =
    {
        new[] { 60, 64, 67 }, // C
        new[] { 59, 62, 67 }, // G (B D G)
        new[] { 60, 64, 69 }, // Am (C E A)
        new[] { 60, 65, 69 }, // F (C F A)
    };
    // The brass stabs' rhythm in each bar (beats): the anthem's push, ahead of beats two and four
    private static readonly float[] StabBeats = { 0f, 1.5f, 2.5f };

    // The fanfare, the second time round: (bar, start beat, length in beats, note)
    private static readonly float[,] Melody =
    {
        { 8, 0f, 1f, 67 }, { 8, 1f, 1f, 72 }, { 8, 2f, 1.5f, 76 }, { 8, 3.5f, 0.5f, 74 },
        { 9, 0f, 1.5f, 74 }, { 9, 1.5f, 0.5f, 71 }, { 9, 2f, 2f, 67 },
        { 10, 0f, 1f, 69 }, { 10, 1f, 1f, 72 }, { 10, 2f, 1f, 76 }, { 10, 3f, 1f, 79 },
        { 11, 0f, 1.5f, 77 }, { 11, 1.5f, 0.5f, 76 }, { 11, 2f, 2f, 74 },
        { 12, 0f, 1f, 76 }, { 12, 1f, 1f, 79 }, { 12, 2f, 2f, 84 },
        { 13, 0f, 1f, 83 }, { 13, 1f, 1f, 79 }, { 13, 2f, 2f, 74 },
        { 14, 0f, 1f, 77 }, { 14, 1f, 1f, 81 }, { 14, 2f, 1f, 79 }, { 14, 3f, 1f, 77 },
        { 15, 0f, 3f, 79 }, { 15, 3f, 0.5f, 74 }, { 15, 3.5f, 0.5f, 71 },
    };

    public AudioClip Clip { get; private set; }

    private int length;
    // Pad, bass and brass (ducked under the kick), and the rest (left and right)
    private float[] duckedL, duckedR, dryL, dryR;
    private float[] kickTimes;
    private uint noise = 24680u;

    public IEnumerator Build()
    {
        length = Mathf.RoundToInt(Bars * Bar * Rate);
        duckedL = new float[length];
        duckedR = new float[length];
        dryL = new float[length];
        dryR = new float[length];
        List<float> kicks = new List<float>();

        for (int bar = 0; bar < Bars; bar++)
        {
            int chord = Progression[bar % Progression.Length];
            float barStart = bar * Bar;
            bool second = bar >= Bars / 2;

            // The pad: the chord held for the bar
            foreach (int note in PadNotes[chord])
            {
                Pad(barStart, Bar, note);
                yield return null;
            }

            // The bass: eighth notes jumping between the root and its octave
            for (int i = 0; i < 8; i++) Bass(barStart + i * Beat * 0.5f, Beat * 0.5f, BassNotes[chord] + (i % 2 == 1 ? 12 : 0));

            // Drums: a kick on every beat, a clap on two and four, open hats on the off-beats, and a
            // crash at the top of each half
            for (int beat = 0; beat < 4; beat++)
            {
                float at = barStart + beat * Beat;
                Kick(at);
                kicks.Add(at);
                if (beat % 2 == 1) Clap(at);
                Hat(at + Beat * 0.5f, 0.12f, 1f);
                Hat(at, 0.03f, 0.45f);
            }
            if (bar % 8 == 0) Crash(barStart);

            // Brass stabs on the chord, softer under the fanfare
            foreach (float stab in StabBeats)
            {
                foreach (int note in PadNotes[chord])
                {
                    Brass(barStart + stab * Beat, Beat * 0.35f, note, second ? 0.55f : 0.85f, 0.5f + (note - 64) * 0.04f);
                }
            }
            yield return null;
        }
        kickTimes = kicks.ToArray();

        // The fanfare, doubled an octave down for weight
        for (int n = 0; n < Melody.GetLength(0); n++)
        {
            float start = Melody[n, 0] * Bar + Melody[n, 1] * Beat;
            float hold = Melody[n, 2] * Beat;
            int note = (int)Melody[n, 3];
            Brass(start, hold * 0.92f, note, 1f, 0.5f);
            Brass(start, hold * 0.92f, note - 12, 0.35f, 0.5f);
            if (n % 3 == 2) yield return null;
        }

        float[] data = Mix();
        yield return null;
        Clip = AudioClip.Create("Victory Music", length, 2, Rate, false);
        Clip.SetData(data, 0);
    }

    private static float Frequency(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

    // Adds a sample to a buffer, wrapping round the loop's end
    private void Add(float[] buffer, int index, float value)
    {
        index %= length;
        if (index < 0) index += length;
        buffer[index] += value;
    }

    // A wide pad note: three detuned saws spread left and right through a soft low-pass
    private void Pad(float start, float hold, int note)
    {
        const float attack = 0.08f, release = 0.5f, level = 0.03f, cutoff = 1800f;
        float f = Frequency(note);
        float[] detune = { -0.007f, 0f, 0.007f };
        float[] pan = { 0.15f, 0.5f, 0.85f };
        int samples = Mathf.RoundToInt((hold + release) * Rate);
        int startSample = Mathf.RoundToInt(start * Rate);
        float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
        for (int v = 0; v < 3; v++)
        {
            float phase = v * 0.27f;
            float step = f * (1f + detune[v]) / Rate;
            float low = 0f;
            float left = Mathf.Sqrt(1f - pan[v]), right = Mathf.Sqrt(pan[v]);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / Rate;
                phase += step;
                phase -= Mathf.Floor(phase);
                low += (2f * phase - 1f - low) * a;
                float envelope = Mathf.Clamp01(t / attack) * (t > hold ? Mathf.Exp(-(t - hold) * 5f / release) : 1f);
                float s = low * envelope * level;
                Add(duckedL, startSample + i, s * left);
                Add(duckedR, startSample + i, s * right);
            }
        }
    }

    // A brass note: two detuned saws through a low-pass that opens bright on the attack and settles,
    // with a little vibrato on longer notes
    private void Brass(float start, float hold, int note, float levelScale, float pan)
    {
        const float release = 0.12f, level = 0.05f;
        float f = Frequency(note);
        int samples = Mathf.RoundToInt((hold + release) * Rate);
        int startSample = Mathf.RoundToInt(start * Rate);
        pan = Mathf.Clamp01(pan);
        float left = Mathf.Sqrt(1f - pan), right = Mathf.Sqrt(pan);
        float p1 = 0f, p2 = 0.5f, low = 0f, low2 = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / Rate;
            float vibrato = 1f + 0.004f * Mathf.Sin(2f * Mathf.PI * 5.5f * t) * Mathf.Clamp01((t - 0.25f) * 4f);
            p1 += f * vibrato / Rate;
            p2 += f * vibrato * 1.006f / Rate;
            p1 -= Mathf.Floor(p1);
            p2 -= Mathf.Floor(p2);
            float saw = (2f * p1 - 1f) + (2f * p2 - 1f);
            float cutoff = Mathf.Min(f * 2.5f + 3200f * Mathf.Exp(-t * 7f) * Mathf.Clamp01(t * 60f), 9000f);
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
            low += (saw - low) * a;
            low2 += (low - low2) * a;
            float envelope = Mathf.Clamp01(t / 0.015f) * (t > hold ? Mathf.Exp(-(t - hold) * 5f / release) : 1f);
            float s = low2 * envelope * level * levelScale;
            Add(duckedL, startSample + i, s * left);
            Add(duckedR, startSample + i, s * right);
        }
    }

    // A round bass note: a warm sine, plucked a touch at its start
    private void Bass(float start, float duration, int note)
    {
        const float level = 0.18f;
        float f = Frequency(note);
        int samples = Mathf.RoundToInt(duration * Rate);
        int startSample = Mathf.RoundToInt(start * Rate);
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / Rate;
            phase += f / Rate;
            float sine = Mathf.Sin(2f * Mathf.PI * phase);
            float warm = (float)System.Math.Tanh(sine * 2.5f) * 0.5f + sine * 0.5f;
            float envelope = Mathf.Clamp01(t / 0.005f) * (0.6f + 0.4f * Mathf.Exp(-t * 14f)) * Mathf.Clamp01((duration - t) / 0.02f);
            float s = warm * envelope * level;
            Add(duckedL, startSample + i, s);
            Add(duckedR, startSample + i, s);
        }
    }

    // A punchy kick: a sine dropping quickly in pitch, with a click on top
    private void Kick(float start)
    {
        const float seconds = 0.3f, level = 0.45f;
        int samples = Mathf.RoundToInt(seconds * Rate);
        int startSample = Mathf.RoundToInt(start * Rate);
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / Rate;
            float f = 48f + 120f * Mathf.Exp(-t * 32f);
            phase += f / Rate;
            float s = Mathf.Sin(2f * Mathf.PI * phase) * Mathf.Exp(-t * 10f) * Mathf.Clamp01(t * 800f) * level;
            Add(dryL, startSample + i, s);
            Add(dryR, startSample + i, s);
        }
    }

    // A hi-hat: a tick of high noise, longer for an open one
    private void Hat(float start, float seconds, float accent)
    {
        const float level = 0.045f;
        int samples = Mathf.RoundToInt(seconds * Rate);
        int startSample = Mathf.RoundToInt(start * Rate);
        float previous = 0f;
        float decay = 4f / seconds;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / Rate;
            float n = Noise();
            float high = n - previous;
            previous = n;
            float s = high * Mathf.Exp(-t * decay) * level * accent;
            Add(dryL, startSample + i, s * 0.85f);
            Add(dryR, startSample + i, s);
        }
    }

    // A clap: a short burst of mid noise with a little tail
    private void Clap(float start)
    {
        const float level = 0.1f;
        int samples = Mathf.RoundToInt(0.2f * Rate);
        int startSample = Mathf.RoundToInt(start * Rate);
        float low = 0f, band = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / Rate;
            float n = Noise();
            low += (n - low) * 0.4f;
            band += (low - band) * 0.08f;
            // Three quick hits before the tail, like hands not quite together
            float flutter = t < 0.025f ? (Mathf.Repeat(t, 0.0085f) < 0.004f ? 1f : 0.3f) : 1f;
            float s = (low - band) * Mathf.Exp(-t * 18f) * flutter * level;
            Add(dryL, startSample + i, s);
            Add(dryR, startSample + i, s);
        }
    }

    // A crash cymbal: bright noise ringing out over most of a bar
    private void Crash(float start)
    {
        const float level = 0.05f, seconds = 1.8f;
        int samples = Mathf.RoundToInt(seconds * Rate);
        int startSample = Mathf.RoundToInt(start * Rate);
        float previous = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / Rate;
            float n = Noise();
            float high = n - previous;
            previous = n;
            float s = high * Mathf.Exp(-t * 2.6f) * Mathf.Clamp01(t * 2000f) * level;
            Add(dryL, startSample + i, s);
            Add(dryR, startSample + i, s * 0.9f);
        }
    }

    // Pad, bass and brass ducked under each kick, added to the rest, brought to a safe level, as
    // interleaved stereo
    private float[] Mix()
    {
        float[] data = new float[length * 2];
        float peak = 0.0001f;
        int[] kickSamples = new int[kickTimes.Length];
        for (int k = 0; k < kickTimes.Length; k++) kickSamples[k] = Mathf.RoundToInt(kickTimes[k] * Rate);
        int next = 0;
        int lastKick = kickSamples.Length > 0 ? kickSamples[kickSamples.Length - 1] - length : int.MinValue / 2;
        for (int i = 0; i < length; i++)
        {
            while (next < kickSamples.Length && kickSamples[next] <= i) lastKick = kickSamples[next++];
            float since = (float)(i - lastKick) / Rate;
            float duck = 1f - 0.3f * Mathf.Exp(-since / 0.08f);
            float l = duckedL[i] * duck + dryL[i];
            float r = duckedR[i] * duck + dryR[i];
            data[i * 2] = l;
            data[i * 2 + 1] = r;
            peak = Mathf.Max(peak, Mathf.Abs(l), Mathf.Abs(r));
        }
        float gain = 0.85f / peak;
        for (int i = 0; i < data.Length; i++) data[i] *= gain;
        return data;
    }

    // Repeatable white noise, -1 to 1
    private float Noise()
    {
        noise ^= noise << 13;
        noise ^= noise >> 17;
        noise ^= noise << 5;
        return (noise & 0xFFFFFF) / (float)0x7FFFFF - 1f;
    }
}
