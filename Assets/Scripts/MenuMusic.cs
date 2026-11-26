using System.Collections;
using UnityEngine;

// The main menu's music (see MenuAudio), made in code: a mellow synthwave loop in A minor at 96 BPM,
// 16 bars (40 seconds) of Am, F, C, G, two bars each, played twice: a soft detuned pad and a pulsing
// bass, ducking under a soft kick, with hi-hats; the second time round a plucked arpeggio with an echo
// joins in. Every note is written into the loop wrapping round its end, so the loop is seamless.
// Build makes it a bar at a time (a step per frame), then Clip holds it.
public class MenuMusic
{
    private const int Rate = 32000;
    private const float Bpm = 96f;
    private const int Bars = 16;
    private const float Beat = 60f / Bpm, Bar = Beat * 4f;

    // The chords, two bars each: the bass's note, and the pad's (MIDI note numbers)
    private static readonly int[] BassNotes = { 45, 41, 48, 43 };
    private static readonly int[][] PadNotes =
    {
        new[] { 57, 60, 64 }, // Am
        new[] { 57, 60, 65 }, // F (A C F)
        new[] { 55, 60, 64 }, // C (G C E)
        new[] { 55, 59, 62 }, // G
    };
    // The arpeggio's order through the chord's notes (an octave up, plus the root two octaves up)
    private static readonly int[] ArpOrder = { 0, 1, 2, 3, 2, 1, 2, 1 };

    public AudioClip Clip { get; private set; }

    private int length;
    // Pad and bass (ducked under the kick), and the rest (left and right)
    private float[] duckedL, duckedR, dryL, dryR;
    private float[] kickTimes;
    private uint noise = 12345u;

    public IEnumerator Build()
    {
        length = Mathf.RoundToInt(Bars * Bar * Rate);
        duckedL = new float[length];
        duckedR = new float[length];
        dryL = new float[length];
        dryR = new float[length];
        System.Collections.Generic.List<float> kicks = new System.Collections.Generic.List<float>();

        for (int bar = 0; bar < Bars; bar++)
        {
            int chord = (bar / 2) % 4;
            float barStart = bar * Bar;

            // The pad: one long chord over two bars, starting on the first of them
            if (bar % 2 == 0)
            {
                foreach (int note in PadNotes[chord])
                {
                    Pad(barStart, Bar * 2f, note);
                    yield return null;
                }
            }

            // The bass: eighth notes on the root, jumping up an octave on the last one of the bar
            for (int i = 0; i < 8; i++)
            {
                int note = BassNotes[chord] + (i == 7 ? 12 : 0);
                Bass(barStart + i * Beat * 0.5f, Beat * 0.5f, note);
            }

            // Drums: a kick on beats one and three, hi-hats on the off-beats, and a soft clap on two
            // and four in the second half
            for (int beat = 0; beat < 4; beat++)
            {
                float at = barStart + beat * Beat;
                if (beat % 2 == 0)
                {
                    Kick(at);
                    kicks.Add(at);
                }
                Hat(at + Beat * 0.5f, beat % 2 == 1 ? 1f : 0.7f);
                if (bar >= 8 && beat % 2 == 1) Clap(at);
            }

            // The arpeggio, the second time round: sixteenth notes through the chord, with an echo
            if (bar >= 8)
            {
                int[] pad = PadNotes[chord];
                int[] notes = { pad[0] + 12, pad[1] + 12, pad[2] + 12, pad[0] + 24 };
                for (int i = 0; i < 16; i++)
                {
                    // Fades in over the first two bars of it
                    float level = bar == 8 ? 0.4f + 0.6f * i / 16f : 1f;
                    Pluck(barStart + i * Beat * 0.25f, notes[ArpOrder[i % ArpOrder.Length]], level);
                }
            }
            yield return null;
        }
        kickTimes = kicks.ToArray();

        float[] data = Mix();
        yield return null;
        Clip = AudioClip.Create("Menu Music", length, 2, Rate, false);
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

    // A soft pad note: three slightly detuned saws, spread left and right, through a gentle low-pass
    // that opens and closes slowly, with a slow swell in and a long release
    private void Pad(float start, float hold, int note)
    {
        const float attack = 0.9f, release = 1.6f, level = 0.045f;
        float f = Frequency(note);
        float[] detune = { -0.006f, 0f, 0.006f };
        float[] pan = { 0.2f, 0.5f, 0.8f };
        int samples = Mathf.RoundToInt((hold + release) * Rate);
        int startSample = Mathf.RoundToInt(start * Rate);
        for (int v = 0; v < 3; v++)
        {
            float phase = v * 0.31f;
            float step = f * (1f + detune[v]) / Rate;
            float low = 0f;
            float left = Mathf.Sqrt(1f - pan[v]), right = Mathf.Sqrt(pan[v]);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / Rate;
                phase += step;
                phase -= Mathf.Floor(phase);
                float saw = 2f * phase - 1f;
                // The filter's cutoff breathes slowly over the loop
                float cutoff = 900f + 500f * Mathf.Sin((start + t) * 0.4f);
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
                low += (saw - low) * a;
                float envelope = Mathf.Clamp01(t / attack) * (t > hold ? Mathf.Exp(-(t - hold) * 4f / release) : 1f);
                float s = low * envelope * level;
                Add(duckedL, startSample + i, s * left);
                Add(duckedR, startSample + i, s * right);
            }
        }
    }

    // A round bass note: a sine with a little warmth, plucked a touch at its start
    private void Bass(float start, float duration, int note)
    {
        const float level = 0.2f;
        float f = Frequency(note);
        int samples = Mathf.RoundToInt(duration * Rate);
        int startSample = Mathf.RoundToInt(start * Rate);
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / Rate;
            phase += f / Rate;
            float sine = Mathf.Sin(2f * Mathf.PI * phase);
            float warm = (float)System.Math.Tanh(sine * 2.2f) * 0.45f + sine * 0.55f;
            float envelope = Mathf.Clamp01(t / 0.006f) * (0.65f + 0.35f * Mathf.Exp(-t * 12f)) * Mathf.Clamp01((duration - t) / 0.03f);
            float s = warm * envelope * level;
            Add(duckedL, startSample + i, s);
            Add(duckedR, startSample + i, s);
        }
    }

    // A soft kick: a sine dropping quickly in pitch
    private void Kick(float start)
    {
        const float seconds = 0.35f, level = 0.42f;
        int samples = Mathf.RoundToInt(seconds * Rate);
        int startSample = Mathf.RoundToInt(start * Rate);
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / Rate;
            float f = 44f + 90f * Mathf.Exp(-t * 28f);
            phase += f / Rate;
            float s = Mathf.Sin(2f * Mathf.PI * phase) * Mathf.Exp(-t * 9f) * Mathf.Clamp01(t * 800f) * level;
            Add(dryL, startSample + i, s);
            Add(dryR, startSample + i, s);
        }
    }

    // A closed hi-hat: a tick of high noise, a little to the right
    private void Hat(float start, float accent)
    {
        const float level = 0.05f;
        int samples = Mathf.RoundToInt(0.05f * Rate);
        int startSample = Mathf.RoundToInt(start * Rate);
        float previous = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / Rate;
            float n = Noise();
            float high = n - previous; // only the highs
            previous = n;
            float s = high * Mathf.Exp(-t * 70f) * level * accent;
            Add(dryL, startSample + i, s * 0.8f);
            Add(dryR, startSample + i, s);
        }
    }

    // A soft clap: a short burst of mid noise with a little tail
    private void Clap(float start)
    {
        const float level = 0.07f;
        int samples = Mathf.RoundToInt(0.18f * Rate);
        int startSample = Mathf.RoundToInt(start * Rate);
        float low = 0f, band = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / Rate;
            float n = Noise();
            low += (n - low) * 0.35f;
            band += (low - band) * 0.08f;
            float s = (low - band) * Mathf.Exp(-t * 22f) * level;
            Add(dryL, startSample + i, s);
            Add(dryR, startSample + i, s);
        }
    }

    // A plucked arpeggio note (triangle and an octave sine), with an echo a dotted eighth later that
    // bounces between left and right
    private void Pluck(float start, int note, float levelScale)
    {
        const float level = 0.06f, decay = 9f;
        float f = Frequency(note);
        int samples = Mathf.RoundToInt(0.45f * Rate);
        int startSample = Mathf.RoundToInt(start * Rate);
        int echo = Mathf.RoundToInt(Beat * 0.75f * Rate);
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / Rate;
            phase += f / Rate;
            float p = phase - Mathf.Floor(phase);
            float tri = 4f * Mathf.Abs(p - 0.5f) - 1f;
            float s = (tri * 0.75f + Mathf.Sin(4f * Mathf.PI * phase) * 0.25f) * Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 1500f) * level * levelScale;
            Add(dryL, startSample + i, s * 0.75f);
            Add(dryR, startSample + i, s * 0.75f);
            // Three echoes, each softer, alternating sides
            Add(dryL, startSample + i + echo, s * 0.4f);
            Add(dryR, startSample + i + echo * 2, s * 0.22f);
            Add(dryL, startSample + i + echo * 3, s * 0.12f);
        }
    }

    // Pad and bass ducked under each kick, added to the rest, brought to a safe level, as
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
            float duck = 1f - 0.4f * Mathf.Exp(-since / 0.12f);
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
