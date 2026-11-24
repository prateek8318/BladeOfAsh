using System;
using System.Threading.Tasks;
using UnityEngine;

// Original background score, composed in code and synthesised at startup on worker threads
// (no audio files). Four looping tracks: menu lament, battle drums, boss war-choir, dawn ending.
// Music.Play(Track) crossfades between them and survives scene reloads.
public class Music : MonoBehaviour
{
    public enum Track { None, Menu, Battle, Boss, Ending }

    const int Rate = 22050;
    static Music inst;
    static float[][] pcm = new float[5][];
    static AudioClip[] clips = new AudioClip[5];
    static bool started;

    AudioSource[] src = new AudioSource[2];
    int active;
    Track current = Track.None, wanted = Track.None;
    float wantedPitch = 1f;
    float duck = 1f, duckTarget = 1f;
    public static float Volume = 0.5f;

    public static void Play(Track t, float pitch = 1f)
    {
        Ensure();
        inst.wanted = t;
        inst.wantedPitch = pitch;
    }

    // Lower the music (pause menu, game over) without switching track.
    public static void Duck(float v) { Ensure(); inst.duckTarget = v; }

    static void Ensure()
    {
        if (inst != null) return;
        var go = new GameObject("Music");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<Music>();
        for (int i = 0; i < 2; i++)
        {
            var s = go.AddComponent<AudioSource>();
            s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f; s.volume = 0f;
            inst.src[i] = s;
        }
        if (!started)
        {
            started = true;
            // Menu first so it is ready quickly, the rest in parallel.
            Task.Run(() => pcm[(int)Track.Menu] = Compose.Menu());
            Task.Run(() => pcm[(int)Track.Battle] = Compose.Battle());
            Task.Run(() => pcm[(int)Track.Boss] = Compose.Boss());
            Task.Run(() => pcm[(int)Track.Ending] = Compose.Ending());
        }
    }

    void Update()
    {
        // Turn finished PCM into clips on the main thread.
        for (int i = 1; i < 5; i++)
        {
            if (clips[i] == null && pcm[i] != null)
            {
                var c = AudioClip.Create(((Track)i).ToString(), pcm[i].Length, 1, Rate, false);
                c.SetData(pcm[i], 0);
                clips[i] = c;
            }
        }

        if (wanted != current && (wanted == Track.None || clips[(int)wanted] != null))
        {
            active = 1 - active;
            var s = src[active];
            s.Stop();
            if (wanted != Track.None) { s.clip = clips[(int)wanted]; s.time = 0f; s.Play(); }
            current = wanted;
        }
        src[active].pitch = Mathf.MoveTowards(src[active].pitch, wantedPitch, Time.unscaledDeltaTime * 0.5f);
        duck = Mathf.MoveTowards(duck, duckTarget, Time.unscaledDeltaTime * 1.5f);
        float dt = Time.unscaledDeltaTime;
        src[active].volume = Mathf.MoveTowards(src[active].volume, current == Track.None ? 0f : Volume * duck, dt * 0.6f);
        src[1 - active].volume = Mathf.MoveTowards(src[1 - active].volume, 0f, dt * 0.5f);
        if (src[1 - active].volume <= 0f && src[1 - active].isPlaying) src[1 - active].Stop();
    }

    // =====================================================================================
    // Composition + synthesis. Everything here runs off the main thread: only System.Math.
    static class Compose
    {
        const int D3 = 50; // tonal centre: D minor / D harmonic minor

        static readonly float[] SinT = BuildSin();
        static float[] BuildSin() { var t = new float[4096]; for (int i = 0; i < 4096; i++) t[i] = (float)Math.Sin(i / 4096.0 * Math.PI * 2); return t; }
        static float Sin(double cycles) => SinT[(int)((cycles - Math.Floor(cycles)) * 4096) & 4095];
        static double Hz(float midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

        class Buf
        {
            public float[] d; public double bpm; public int loopLen; public System.Random rnd;
            public Buf(double bpm, int bars, int seed)
            {
                this.bpm = bpm;
                loopLen = (int)(bars * 4 * 60.0 / bpm * Rate);
                d = new float[loopLen + Rate * 4];
                rnd = new System.Random(seed);
            }
            public double T(double beat) => beat * 60.0 / bpm;
            public float Noise() => (float)(rnd.NextDouble() * 2 - 1);
        }

        // ---------------------------------------------------------------- instruments
        static void Taiko(Buf b, double beat, float gain, float tune = 1f)
        {
            int s0 = (int)(b.T(beat) * Rate); int n = (int)(0.9 * Rate);
            double ph = 0; float lp = 0;
            for (int i = 0; i < n && s0 + i < b.d.Length; i++)
            {
                double t = i / (double)Rate;
                double f = 58 * tune * (1 + 1.4 * Math.Exp(-t * 28));
                ph += f / Rate;
                lp += (b.Noise() - lp) * 0.08f;
                float v = Sin(ph) * (float)Math.Exp(-t * 4.5) + lp * 3f * (float)Math.Exp(-t * 22);
                b.d[s0 + i] += v * gain;
            }
        }

        static void Rim(Buf b, double beat, float gain)
        {
            int s0 = (int)(b.T(beat) * Rate); int n = (int)(0.15 * Rate);
            float hp = 0, prev = 0; double ph = 0;
            for (int i = 0; i < n && s0 + i < b.d.Length; i++)
            {
                double t = i / (double)Rate;
                float x = b.Noise(); hp = 0.7f * (hp + x - prev); prev = x;
                ph += 330.0 / Rate;
                b.d[s0 + i] += (hp * 0.7f + Sin(ph) * 0.5f) * (float)Math.Exp(-t * 30) * gain;
            }
        }

        // Forge anvil: inharmonic metallic partials.
        static void Anvil(Buf b, double beat, float gain)
        {
            int s0 = (int)(b.T(beat) * Rate); int n = (int)(1.2 * Rate);
            double[] f = { 1150, 1793, 2620, 3390 }; float[] a = { 0.5f, 0.35f, 0.22f, 0.12f };
            for (int i = 0; i < n && s0 + i < b.d.Length; i++)
            {
                double t = i / (double)Rate; float v = 0;
                for (int k = 0; k < 4; k++) v += Sin(f[k] * t) * a[k] * (float)Math.Exp(-t * (4 + k * 2));
                b.d[s0 + i] += v * gain;
            }
        }

        static void Boom(Buf b, double beat, float gain)
        {
            int s0 = (int)(b.T(beat) * Rate); int n = (int)(2.0 * Rate);
            double ph = 0; float lp = 0;
            for (int i = 0; i < n && s0 + i < b.d.Length; i++)
            {
                double t = i / (double)Rate;
                ph += (36 + 50 * Math.Exp(-t * 10)) / Rate;
                lp += (b.Noise() - lp) * 0.03f;
                b.d[s0 + i] += (Sin(ph) * (float)Math.Exp(-t * 1.8) + lp * 4f * (float)Math.Exp(-t * 3)) * gain;
            }
        }

        // Choir "aah": additive voice with vowel formants (~700 Hz / ~1150 Hz), detuned pair, vibrato.
        static void Choir(Buf b, double beat, double beats, int[] notes, float gain)
        {
            int s0 = (int)(b.T(beat) * Rate); int n = (int)(b.T(beats) * Rate) + (int)(0.8 * Rate);
            double sustain = b.T(beats);
            foreach (var note in notes)
            {
                double f0 = Hz(note);
                int H = Math.Max(4, Math.Min(14, (int)(2800 / f0)));
                var w = new float[H + 1];
                for (int h = 1; h <= H; h++)
                {
                    double fh = h * f0;
                    w[h] = (float)(Math.Exp(-Math.Pow((fh - 720) / 220, 2)) + 0.6 * Math.Exp(-Math.Pow((fh - 1150) / 260, 2)) + 0.35 / h);
                }
                for (int v = 0; v < 2; v++)
                {
                    double det = v == 0 ? 0.997 : 1.004; double ph = b.rnd.NextDouble();
                    for (int i = 0; i < n && s0 + i < b.d.Length; i++)
                    {
                        double t = i / (double)Rate;
                        double env = Math.Min(1, t / 0.45) * (t < sustain ? 1 : Math.Max(0, 1 - (t - sustain) / 0.8));
                        double vib = 1 + 0.004 * Sin(5.1 * t + v * 0.3);
                        ph += f0 * det * vib / Rate;
                        float s = 0;
                        for (int h = 1; h <= H; h++) s += Sin(ph * h) * w[h];
                        b.d[s0 + i] += s * (float)env * gain * 0.12f;
                    }
                }
            }
        }

        // Low bowed string / bass: band-limited-ish saw through a one-pole filter.
        static void Bass(Buf b, double beat, double beats, int note, float gain, float bright = 0.06f, double attack = 0.02)
        {
            int s0 = (int)(b.T(beat) * Rate); double len = b.T(beats); int n = (int)((len + 0.25) * Rate);
            double f = Hz(note); double ph = 0; float lp = 0, lp2 = 0;
            for (int i = 0; i < n && s0 + i < b.d.Length; i++)
            {
                double t = i / (double)Rate;
                ph += f / Rate;
                float saw = (float)(2 * (ph - Math.Floor(ph)) - 1);
                lp += (saw - lp) * bright; lp2 += (lp - lp2) * bright * 2;
                double env = Math.Min(1, t / attack) * (t < len ? 1 : Math.Max(0, 1 - (t - len) / 0.25));
                b.d[s0 + i] += lp2 * (float)env * gain;
            }
        }

        // Brass-like stab: saw chord whose filter opens then closes.
        static void Brass(Buf b, double beat, double beats, int[] notes, float gain)
        {
            int s0 = (int)(b.T(beat) * Rate); double len = b.T(beats); int n = (int)((len + 0.3) * Rate);
            foreach (var note in notes)
            {
                double f = Hz(note); double ph = b.rnd.NextDouble(); float lp = 0;
                for (int i = 0; i < n && s0 + i < b.d.Length; i++)
                {
                    double t = i / (double)Rate;
                    ph += f * (1 + 0.002 * Sin(6 * t)) / Rate;
                    float saw = (float)(2 * (ph - Math.Floor(ph)) - 1);
                    float cut = (float)(0.02 + 0.18 * Math.Exp(-t * 5) * Math.Min(1, t / 0.03));
                    lp += (saw - lp) * cut;
                    double env = Math.Min(1, t / 0.03) * (t < len ? 1 : Math.Max(0, 1 - (t - len) / 0.3));
                    b.d[s0 + i] += lp * (float)env * gain;
                }
            }
        }

        // Reed lead (a breathy double-reed voice) with delayed vibrato.
        static void Lead(Buf b, double beat, double beats, int note, float gain)
        {
            int s0 = (int)(b.T(beat) * Rate); double len = b.T(beats); int n = (int)((len + 0.35) * Rate);
            double f = Hz(note); double ph = 0; float lp = 0;
            for (int i = 0; i < n && s0 + i < b.d.Length; i++)
            {
                double t = i / (double)Rate;
                double vibAmt = 0.008 * Math.Min(1, Math.Max(0, (t - 0.25) / 0.4));
                ph += f * (1 + vibAmt * Sin(5.4 * t)) / Rate;
                float s = Sin(ph) + 0.55f * Sin(ph * 2) + 0.4f * Sin(ph * 3) + 0.22f * Sin(ph * 4) + 0.1f * Sin(ph * 5);
                lp += (b.Noise() - lp) * 0.3f;
                double env = Math.Min(1, t / 0.07) * (t < len ? 1 - 0.15 * Math.Min(1, t / Math.Max(0.01, len)) : Math.Max(0, 1 - (t - len) / 0.35));
                b.d[s0 + i] += (s * 0.8f + lp * 0.12f) * (float)env * gain;
            }
        }

        static void Melody(Buf b, double startBeat, float gain, int transpose, params float[] noteBeats)
        {
            double beat = startBeat;
            for (int i = 0; i + 1 < noteBeats.Length; i += 2)
            {
                int note = (int)noteBeats[i]; double len = noteBeats[i + 1];
                if (note > 0) Lead(b, beat, len * 0.95, note + transpose, gain);
                beat += len;
            }
        }

        // ---------------------------------------------------------------- mix
        static float[] Finish(Buf b, float wet = 0.28f)
        {
            var d = b.d;
            // Schroeder reverb (4 combs + 2 allpasses)
            int[] cd = { 558, 594, 638, 678 }; var outW = new float[d.Length];
            foreach (var L in cd)
            {
                var buf = new float[L]; int idx = 0; float filt = 0;
                for (int i = 0; i < d.Length; i++)
                {
                    float y = buf[idx];
                    filt = y * 0.75f + filt * 0.25f;
                    buf[idx] = d[i] * 0.5f + filt * 0.84f;
                    outW[i] += y * 0.25f;
                    idx = (idx + 1) % L;
                }
            }
            foreach (var L in new[] { 278, 220 })
            {
                var buf = new float[L]; int idx = 0;
                for (int i = 0; i < d.Length; i++)
                {
                    float bo = buf[idx]; float x = outW[i];
                    float y = -x + bo; buf[idx] = x + bo * 0.5f; outW[i] = y;
                    idx = (idx + 1) % L;
                }
            }
            for (int i = 0; i < d.Length; i++) d[i] = d[i] * (1 - wet * 0.5f) + outW[i] * wet;

            // fold the tail into the start so the loop is seamless
            var res = new float[b.loopLen];
            Array.Copy(d, res, b.loopLen);
            for (int i = b.loopLen; i < d.Length; i++) res[i - b.loopLen] += d[i];

            float peak = 0.0001f;
            for (int i = 0; i < res.Length; i++) { res[i] = (float)Math.Tanh(res[i] * 1.2); peak = Math.Max(peak, Math.Abs(res[i])); }
            float g = 0.92f / peak;
            for (int i = 0; i < res.Length; i++) res[i] *= g;
            return res;
        }

        static int[] Chord(int root, bool minor, int octave = 0) => new[] { root + octave, root + (minor ? 3 : 4) + octave, root + 7 + octave };

        // ---------------------------------------------------------------- tracks
        // "Ashen Lament" — slow, haunting: choir, drone and a lonely reed.
        public static float[] Menu()
        {
            var b = new Buf(66, 16, 11);
            int[] roots = { 50, 46, 43, 45, 50, 41, 48, 45, 50, 46, 43, 45, 46, 48, 45, 50 };
            bool[] minor = { true, false, true, false, true, false, false, false, true, false, true, false, false, false, false, true };
            for (int bar = 0; bar < 16; bar++)
            {
                double s = bar * 4;
                Choir(b, s, 4, Chord(roots[bar] + 12, minor[bar]), 0.55f);
                Bass(b, s, 4, roots[bar] - 12, 0.32f, 0.03f, 0.3);
                if (bar % 2 == 0) Taiko(b, s, 0.55f, 0.8f);
                if (bar % 4 == 3) { Taiko(b, s + 2.5, 0.3f, 1.1f); Taiko(b, s + 3, 0.4f, 0.9f); }
                if (bar % 8 == 7) Anvil(b, s + 3.5, 0.12f);
            }
            Melody(b, 0, 0.24f, 0,
                69, 2, 67, 1, 65, 1, 64, 2, 62, 1, 64, 1, 65, 3, 67, 1, 69, 4,
                74, 2, 72, 1, 70, 1, 69, 2, 67, 2, 65, 1, 67, 1, 69, 1, 73, 1, 74, 4);
            Melody(b, 32, 0.22f, 0,
                77, 2, 76, 1, 74, 1, 72, 3, 70, 1, 69, 2, 70, 1, 69, 1, 67, 4,
                65, 2, 67, 1, 69, 1, 70, 2, 69, 1, 67, 1, 64, 2, 61, 2, 62, 4);
            return Finish(b, 0.35f);
        }

        // "Ember March" — driving taiko ostinato, low strings, brass stabs, heroic reed line.
        public static float[] Battle()
        {
            var b = new Buf(124, 16, 22);
            int[] roots = { 50, 50, 46, 48, 50, 50, 43, 45, 50, 46, 48, 45, 50, 46, 43, 45 };
            bool[] minor = { true, true, false, false, true, true, true, false, true, false, false, false, true, false, true, false };
            int[] ost = { 0, 0, 12, 0, 3, 0, 12, 1 };
            for (int bar = 0; bar < 16; bar++)
            {
                double s = bar * 4;
                // drums: BIG . . x | BIG . x .  + rim on 2 and 4
                Taiko(b, s, 0.9f); Taiko(b, s + 1.5, 0.45f, 1.15f); Taiko(b, s + 2, 0.75f); Taiko(b, s + 3, 0.5f, 1.2f); Taiko(b, s + 3.5, 0.45f, 1.3f);
                Rim(b, s + 1, 0.35f); Rim(b, s + 3, 0.35f); Rim(b, s + 3.75, 0.2f);
                if (bar % 4 == 3) { for (int k = 0; k < 4; k++) Taiko(b, s + 2 + k * 0.5, 0.35f + k * 0.12f, 1.3f - k * 0.08f); Anvil(b, s + 3.5, 0.18f); }
                // string ostinato in 8ths
                for (int k = 0; k < 8; k++) Bass(b, s + k * 0.5, 0.42, roots[bar] - 12 + ost[k] % 12 + (ost[k] == 12 ? 12 : 0), 0.22f, 0.1f);
                Choir(b, s, 4, Chord(roots[bar] + 12, minor[bar]), 0.35f);
                if (bar % 2 == 0) Brass(b, s, 0.75, Chord(roots[bar], minor[bar]), 0.12f);
                Brass(b, s + 2.5, 0.4, Chord(roots[bar], minor[bar]), 0.08f);
            }
            Melody(b, 16, 0.22f, 0,
                62, 1, 65, 1, 69, 2, 67, 1, 65, 1, 64, 1, 65, 1, 62, 4, 0, 4,
                70, 1, 69, 1, 67, 2, 65, 1, 64, 1, 65, 2, 69, 3, 73, 1, 74, 4,
                74, 1.5f, 72, 0.5f, 70, 2, 69, 1, 67, 1, 65, 2, 64, 2, 65, 1, 67, 1, 69, 4);
            return Finish(b, 0.22f);
        }

        // "Tyrant's Crown" — fast, heavy, phrygian (Eb over D), booms and full war-choir.
        public static float[] Boss()
        {
            var b = new Buf(142, 16, 33);
            int[] roots = { 50, 51, 50, 51, 46, 45, 50, 51, 50, 51, 48, 46, 45, 46, 45, 45 };
            bool[] minor = { true, false, true, false, false, false, true, false, true, false, true, false, false, false, false, false };
            for (int bar = 0; bar < 16; bar++)
            {
                double s = bar * 4;
                Boom(b, s, bar % 2 == 0 ? 0.8f : 0.5f);
                for (int k = 0; k < 8; k++) Taiko(b, s + k * 0.5, (k % 2 == 0 ? 0.7f : 0.4f), k % 4 == 0 ? 0.9f : 1.25f);
                if (bar % 2 == 1) for (int k = 0; k < 8; k++) Taiko(b, s + 2 + k * 0.25, 0.25f + k * 0.05f, 1.4f);
                Rim(b, s + 1, 0.4f); Rim(b, s + 3, 0.4f);
                Anvil(b, s + (bar % 2 == 0 ? 0 : 2), 0.14f);
                for (int k = 0; k < 16; k++) Bass(b, s + k * 0.25, 0.2, roots[bar] - 12 + (k % 8 == 6 ? 1 : 0), 0.2f, 0.12f);
                Choir(b, s, 4, Chord(roots[bar] + 12, minor[bar]), 0.5f);
                Choir(b, s, 4, new[] { roots[bar] + 24 }, 0.3f);
                Brass(b, s + 0.5, 0.3, Chord(roots[bar] - 12, minor[bar]), 0.11f);
                Brass(b, s + 1.5, 0.3, Chord(roots[bar] - 12, minor[bar]), 0.11f);
                Brass(b, s + 3, 0.9, Chord(roots[bar], minor[bar]), 0.12f);
            }
            Melody(b, 32, 0.24f, 12,
                62, 1, 63, 1, 62, 1, 60, 1, 58, 2, 57, 2, 58, 1, 60, 1, 62, 2, 63, 4,
                65, 1, 63, 1, 62, 1, 63, 1, 65, 2, 67, 2, 69, 3, 70, 1, 69, 4);
            return Finish(b, 0.2f);
        }

        // "Dawn Over Emberhold" — warm D major, hopeful reed melody, soft drums.
        public static float[] Ending()
        {
            var b = new Buf(76, 16, 44);
            int[] roots = { 50, 47, 43, 45, 50, 54, 43, 45, 47, 43, 50, 45, 43, 45, 47, 50 };
            bool[] minor = { false, true, false, false, false, true, false, false, true, false, false, false, false, false, true, false };
            for (int bar = 0; bar < 16; bar++)
            {
                double s = bar * 4;
                Choir(b, s, 4, Chord(roots[bar] + 12, minor[bar]), 0.55f);
                Bass(b, s, 2, roots[bar] - 12, 0.3f, 0.04f, 0.1);
                Bass(b, s + 2, 2, roots[bar] - 12 + 7, 0.22f, 0.04f, 0.1);
                Taiko(b, s, 0.45f, 0.85f);
                if (bar % 2 == 1) Taiko(b, s + 2.5, 0.25f, 1.1f);
                Rim(b, s + 2, 0.12f);
            }
            Melody(b, 0, 0.24f, 0,
                66, 1, 69, 1, 74, 2, 73, 1, 71, 1, 69, 2, 67, 1, 69, 1, 71, 2, 69, 4,
                66, 1, 69, 1, 74, 2, 76, 1, 78, 1, 76, 2, 74, 1, 73, 1, 71, 1, 73, 1, 74, 4,
                78, 2, 76, 1, 74, 1, 73, 2, 71, 2, 69, 1, 71, 1, 73, 2, 74, 4,
                74, 2, 73, 1, 71, 1, 69, 3, 66, 1, 67, 2, 69, 2, 74, 4);
            return Finish(b, 0.35f);
        }
    }
}
