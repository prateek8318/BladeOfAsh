using UnityEngine;

// Procedurally synthesised sound effects (no audio assets needed).
public static class SFX
{
    public enum S { Swing, SwingHeavy, Hit, HeavyHit, Boom, Thunder, Zap, Parry, Throw, Hurt, Pickup, Shatter, Spawn, Blink, Cast, Roar }

    static AudioClip[] clips;
    static AudioSource[] pool;
    static int next;
    const int Rate = 44100;

    static void Init()
    {
        if (pool != null && pool[0] != null) return;
        var go = new GameObject("SFX");
        Object.DontDestroyOnLoad(go);
        pool = new AudioSource[24];
        for (int i = 0; i < pool.Length; i++)
        {
            pool[i] = go.AddComponent<AudioSource>();
            pool[i].playOnAwake = false;
            pool[i].spatialBlend = 0f;
        }
        if (clips != null) return;
        clips = new AudioClip[16];
        var rnd = new System.Random(1234);
        clips[(int)S.Swing] = Make("swing", 0.18f, rnd, (t, n, k) => Band(n, 0.25f) * Env(k, 0.35f, 0.65f) * 0.55f);
        clips[(int)S.SwingHeavy] = Make("swingH", 0.32f, rnd, (t, n, k) => Band(n, 0.12f) * Env(k, 0.45f, 0.55f) * 0.75f);
        clips[(int)S.Hit] = Make("hit", 0.16f, rnd, (t, n, k) => (n * 0.7f + Mathf.Sin(t * 2f * Mathf.PI * 140f) * 0.6f) * Decay(k, 7f) * 0.8f);
        clips[(int)S.HeavyHit] = Make("hitH", 0.35f, rnd, (t, n, k) => (n * 0.6f + Mathf.Sin(t * 2f * Mathf.PI * (90f - k * 40f)) * 0.9f) * Decay(k, 5f));
        clips[(int)S.Boom] = Make("boom", 0.9f, rnd, (t, n, k) => (Mathf.Sin(t * 2f * Mathf.PI * (65f - k * 35f)) * 1.0f + Band(n, 0.08f) * 0.8f) * Decay(k, 3.5f));
        clips[(int)S.Thunder] = Make("thunder", 1.3f, rnd, (t, n, k) => (Crackle(n, t) * Decay(k, 9f) * 0.9f + Band(n, 0.04f) * 1.4f * Decay(k, 2.2f)));
        clips[(int)S.Zap] = Make("zap", 0.22f, rnd, (t, n, k) => Crackle(n, t) * Decay(k, 6f) * 0.7f + Mathf.Sign(Mathf.Sin(t * 2f * Mathf.PI * (900f - k * 600f))) * 0.12f * Decay(k, 8f));
        clips[(int)S.Parry] = Make("parry", 0.6f, rnd, (t, n, k) =>
            (Mathf.Sin(t * 2f * Mathf.PI * 1240f) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 1873f) * 0.35f + Mathf.Sin(t * 2f * Mathf.PI * 2711f) * 0.2f + n * 0.4f * Decay(k, 30f)) * Decay(k, 6f));
        clips[(int)S.Throw] = Make("throw", 0.25f, rnd, (t, n, k) => Band(n, 0.4f) * Env(k, 0.2f, 0.8f) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * (300f + k * 500f)) * 0.15f * Decay(k, 4f));
        clips[(int)S.Hurt] = Make("hurt", 0.22f, rnd, (t, n, k) => (Mathf.Sin(t * 2f * Mathf.PI * (180f - k * 80f)) * 0.7f + n * 0.4f) * Decay(k, 5f));
        clips[(int)S.Pickup] = Make("pickup", 0.35f, rnd, (t, n, k) => Mathf.Sin(t * 2f * Mathf.PI * (k < 0.5f ? 660f : 990f)) * 0.4f * Decay(k, 3f));
        clips[(int)S.Shatter] = Make("shatter", 0.5f, rnd, (t, n, k) => (n * 0.6f * Decay(k, 6f) + Mathf.Sin(t * 2f * Mathf.PI * 3100f) * 0.15f * Decay(k, 10f) + Mathf.Sin(t * 2f * Mathf.PI * 2300f) * 0.12f * Decay(k, 4f)));
        clips[(int)S.Spawn] = Make("spawn", 0.7f, rnd, (t, n, k) => (Band(n, 0.1f) * 0.6f + Mathf.Sin(t * 2f * Mathf.PI * (50f + k * 90f)) * 0.5f) * Env(k, 0.5f, 0.5f));
        clips[(int)S.Blink] = Make("blink", 0.25f, rnd, (t, n, k) => Mathf.Sin(t * 2f * Mathf.PI * (1400f - k * 1200f)) * 0.3f * Decay(k, 4f) + Band(n, 0.3f) * 0.3f * Decay(k, 8f));
        clips[(int)S.Cast] = Make("cast", 0.4f, rnd, (t, n, k) => (Mathf.Sin(t * 2f * Mathf.PI * (220f + k * 440f)) * 0.3f + Band(n, 0.2f) * 0.3f) * Env(k, 0.6f, 0.4f));
        clips[(int)S.Roar] = Make("roar", 1.4f, rnd, (t, n, k) =>
            (Band(n, 0.06f) * 1.1f + Mathf.Sin(t * 2f * Mathf.PI * (75f + Mathf.Sin(t * 40f) * 8f)) * 0.7f) * Env(k, 0.15f, 0.85f));
    }

    // ---------------------------------------------------------- synthesis helpers
    delegate float Gen(float t, float noise, float k);
    static float lp; // one-pole low-pass state

    static AudioClip Make(string name, float seconds, System.Random rnd, Gen g)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        var data = new float[n];
        lp = 0f;
        float peak = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rnd.NextDouble() * 2f - 1f;
            data[i] = g(t, noise, i / (float)n);
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        float norm = peak > 0.95f ? 0.95f / peak : 1f;
        for (int i = 0; i < n; i++) data[i] *= norm;
        var c = AudioClip.Create(name, n, 1, Rate, false);
        c.SetData(data, 0);
        return c;
    }

    static float Band(float noise, float a) { lp += (noise - lp) * a; return lp * (1f / Mathf.Sqrt(a)) * 0.6f; }
    static float Decay(float k, float rate) => Mathf.Exp(-k * rate);
    static float Env(float k, float attack, float release) => k < attack ? k / attack : Mathf.Clamp01(1f - (k - attack) / release);
    static float Crackle(float noise, float t) => Mathf.PerlinNoise(t * 900f, 0.5f) > 0.62f ? noise * 1.6f : noise * 0.25f;

    // ---------------------------------------------------------- playback
    public static void Play(S s, float volume = 1f, float pitch = 1f, float pitchJitter = 0.08f)
    {
        Init();
        var src = pool[next];
        next = (next + 1) % pool.Length;
        src.clip = clips[(int)s];
        src.volume = Mathf.Clamp01(volume) * 0.8f;
        src.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
        src.Play();
    }
}
