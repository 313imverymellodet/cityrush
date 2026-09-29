using UnityEngine;

// All audio is synthesized at boot: zero audio files.
public class Sfx : MonoBehaviour
{
    public static Sfx I;
    const int SR = 22050;
    const float TAU = Mathf.PI * 2f;
    AudioSource[] voices; int next;
    AudioSource music, engine, skid, nitroLoop;
    AudioClip boost, crash, bump, beep, go, lap, win, finish, click, whoosh;
    public bool Muted { get; private set; }
    System.Random rnd = new System.Random(7);
    float N() => (float)(rnd.NextDouble() * 2 - 1);
    float lastBump;

    void Awake()
    {
        I = this;
        voices = new AudioSource[8];
        for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; }
        music = Loop(0.26f);
        engine = Loop(0f); skid = Loop(0f); nitroLoop = Loop(0f);
        Build();
        music.clip = Music();
    }

    AudioSource Loop(float vol)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = true; s.volume = vol; s.playOnAwake = false;
        return s;
    }

    public void SetMuted(bool m) { Muted = m; AudioListener.volume = m ? 0 : 1; }
    public void StartMusic() { if (!music.isPlaying) music.Play(); }

    public void Engine(bool on)
    {
        if (on) { if (!engine.isPlaying) { engine.Play(); skid.Play(); nitroLoop.Play(); } }
        else { engine.Stop(); skid.Stop(); nitroLoop.Stop(); }
    }

    float gearPhase;
    public void EngineUpdate(float speed01, bool skidding, bool nitro)
    {
        if (!engine.isPlaying) return;
        // fake 4-speed gearbox: pitch climbs within a gear, drops on shift
        float g = speed01 * 4f;
        int gear = Mathf.Min((int)g, 3);
        float inGear = g - gear;
        float want = 0.55f + gear * 0.12f + inGear * 0.75f;
        engine.pitch = Mathf.Lerp(engine.pitch, want, Time.deltaTime * 8f);
        engine.volume = 0.16f + speed01 * 0.12f;
        skid.volume = Mathf.MoveTowards(skid.volume, skidding ? 0.22f : 0f, Time.deltaTime * 3f);
        nitroLoop.volume = Mathf.MoveTowards(nitroLoop.volume, nitro ? 0.3f : 0f, Time.deltaTime * 4f);
        music.volume = 0.2f + speed01 * 0.08f;
    }

    void Play(AudioClip c, float vol, float pitch = 1f)
    {
        var s = voices[next]; next = (next + 1) % voices.Length;
        s.pitch = pitch; s.PlayOneShot(c, vol);
    }

    public void Boost() => Play(boost, 0.5f);
    public void Crash(float k) => Play(crash, 0.25f + k * 0.45f, Random.Range(0.9f, 1.1f));
    public void Bump() { if (Time.unscaledTime - lastBump < 0.25f) return; lastBump = Time.unscaledTime; Play(bump, 0.4f, Random.Range(0.9f, 1.15f)); }
    public void Beep(bool isGo) => Play(isGo ? go : beep, 0.55f);
    public void Lap() => Play(lap, 0.5f);
    public void Finish(bool first) => Play(first ? win : finish, 0.7f);
    public void Click() => Play(click, 0.4f);
    public void Whoosh() => Play(whoosh, 0.35f, Random.Range(0.9f, 1.1f));

    static AudioClip Clip(string n, float[] d) { var c = AudioClip.Create(n, d.Length, 1, SR, false); c.SetData(d, 0); return c; }
    delegate float Gen(float t, float dt);
    static float[] R(float dur, Gen g, bool loop = false)
    {
        int n = (int)(SR * dur); var d = new float[n]; float dt = 1f / SR;
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(g(i * dt, dt) * (loop ? 1f : Mathf.Clamp01((n - i) / (SR * 0.008f))), -1, 1);
        return d;
    }

    float[] Arp(float[] notes, float step, float tail, float vol)
    {
        float ph = 0;
        return R(step * notes.Length + tail, (t, dt) =>
        {
            int k = Mathf.Min((int)(t / step), notes.Length - 1);
            ph += TAU * notes[k] * dt;
            float lt = t - k * step;
            float saw = 2f * (ph / TAU % 1f) - 1f;
            return (Mathf.Sin(ph) * 0.8f + saw * 0.25f) * Mathf.Exp(-lt * (k < notes.Length - 1 ? 10 : 3f)) * vol;
        });
    }

    void Build()
    {
        // engine loop: firing pulses at 90 Hz base, harmonics + grit. Exactly 1 s so the loop point is seamless.
        float lp = 0;
        engine.clip = Clip("engine", R(1f, (t, dt) =>
        {
            float f = 90f;
            float p = t * f % 1f;
            float pulse = Mathf.Exp(-p * 6f) * 2f - 0.6f;
            lp += (N() - lp) * 0.2f;
            return (pulse * 0.35f + Mathf.Sin(TAU * f * t) * 0.35f + Mathf.Sin(TAU * f * 2 * t) * 0.15f + lp * 0.12f) * 0.8f;
        }, true));
        lp = 0; float hp = 0;
        skid.clip = Clip("skid", R(1f, (t, dt) =>
        {
            float nz = N(); lp += (nz - lp) * 0.35f; float band = lp - hp; hp += (lp - hp) * 0.05f;
            return (band * 1.3f + Mathf.Sin(TAU * 880 * t + Mathf.Sin(TAU * 13 * t) * 3f) * 0.12f);
        }, true));
        lp = 0;
        nitroLoop.clip = Clip("nitro", R(1f, (t, dt) => { lp += (N() - lp) * 0.6f; return lp * 0.6f + Mathf.Sin(TAU * 55 * t) * 0.2f; }, true));

        float ph = 0; lp = 0;
        boost = Clip("boost", R(0.7f, (t, dt) => { lp += (N() - lp) * 0.5f; ph += TAU * Mathf.Lerp(200, 900, t / 0.7f) * dt; return (Mathf.Sin(ph) * 0.4f + lp * 0.5f) * Mathf.Sin(t / 0.7f * Mathf.PI); }));
        ph = 0; lp = 0;
        crash = Clip("crash", R(0.6f, (t, dt) => { lp += (N() - lp) * Mathf.Lerp(0.9f, 0.1f, t / 0.6f); ph += TAU * (70 + 80 * Mathf.Exp(-t * 20)) * dt; return (lp * 0.9f + Mathf.Sin(ph) * 0.6f + (t < 0.2f ? Mathf.Sin(TAU * 1800 * t) * 0.2f * Mathf.Exp(-t * 25) : 0)) * Mathf.Exp(-t * 7); }));
        ph = 0; lp = 0;
        bump = Clip("bump", R(0.18f, (t, dt) => { lp += (N() - lp) * 0.3f; ph += TAU * Mathf.Lerp(160, 70, t / 0.18f) * dt; return (Mathf.Sin(ph) * 0.8f + lp * 0.4f) * Mathf.Exp(-t * 20); }));
        beep = Clip("beep", R(0.25f, (t, dt) => (Mathf.Sin(TAU * 660 * t) * 0.6f + (Mathf.Sin(TAU * 660 * t) > 0 ? 0.15f : -0.15f)) * Mathf.Min(1, (0.25f - t) * 20)));
        go = Clip("go", R(0.7f, (t, dt) => (Mathf.Sin(TAU * 1320 * t) * 0.55f + (Mathf.Sin(TAU * 1320 * t) > 0 ? 0.15f : -0.15f)) * Mathf.Exp(-t * 2.5f)));
        lap = Clip("lap", Arp(new[] { 783.99f, 1046.5f, 1318.5f }, 0.08f, 0.4f, 0.45f));
        win = Clip("win", Arp(new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 783.99f, 1046.5f, 1318.5f, 1568f }, 0.1f, 1.4f, 0.4f));
        finish = Clip("finish", Arp(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.12f, 1.0f, 0.4f));
        ph = 0;
        click = Clip("click", R(0.04f, (t, dt) => { ph += TAU * 1200 * dt; return Mathf.Sin(ph) * Mathf.Exp(-t * 90) * 0.6f; }));
        lp = 0;
        whoosh = Clip("whoosh", R(0.4f, (t, dt) => { lp += (N() - lp) * Mathf.Lerp(0.05f, 0.5f, Mathf.Sin(t / 0.4f * Mathf.PI)); return lp * Mathf.Sin(t / 0.4f * Mathf.PI) * 1.2f; }));
    }

    // 128 bpm driving synthwave in A minor: Am – F – C – G, octave bass, gated pad, lead hook.
    AudioClip Music()
    {
        float bpm = 128f, beat = 60f / bpm;
        int bars = 8; float dur = beat * 4 * bars;
        int n = (int)(SR * dur); var d = new float[n];
        float[][] chords =
        {
            new[] { 220f, 261.63f, 329.63f },
            new[] { 174.61f, 220f, 261.63f },
            new[] { 261.63f, 329.63f, 392f },
            new[] { 196f, 246.94f, 293.66f },
        };
        float[] roots = { 55f, 43.65f, 65.41f, 49f };
        float[] lead = { 659.25f, 587.33f, 523.25f, 587.33f, 659.25f, 783.99f, 659.25f, 587.33f,
                         523.25f, 523.25f, 587.33f, 659.25f, 587.33f, 523.25f, 440f, 493.88f };
        float hp = 0, blp = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR, bt = t / beat;
            int bi = (int)bt, barIdx = bi / 4, bar = barIdx % 4;
            float ib = (bt - bi) * beat;
            float nz = N();
            float kick = Mathf.Sin(TAU * (50 + 110 * Mathf.Exp(-ib * 35)) * ib) * Mathf.Exp(-ib * 9) * 0.6f;
            float snare = (bi % 2 == 1) ? (nz * 0.5f + Mathf.Sin(TAU * 190 * ib) * 0.4f) * Mathf.Exp(-ib * 16) * 0.3f : 0;
            float e8 = bt * 2; int e8i = (int)e8; float i8 = (e8 - e8i) * beat / 2;
            float e16 = bt * 4; int e16i = (int)e16; float i16 = (e16 - e16i) * beat / 4;
            float hat = (nz - hp) * Mathf.Exp(-i16 * 70) * (e16i % 2 == 1 ? 0.07f : 0.035f); hp = nz;
            // octave-jumping 8th bass
            float bf = roots[bar] * (e8i % 2 == 1 ? 2f : 1f);
            float saw = 2f * ((bf * t) % 1f) - 1f;
            blp += (saw - blp) * 0.12f;
            float bass = blp * Mathf.Exp(-i8 * 6) * 0.34f;
            // 16th-gated pad
            float pad = 0;
            foreach (var f in chords[bar])
            {
                float s = 2f * ((f * t) % 1f) - 1f;
                float s2 = 2f * ((f * 1.004f * t) % 1f) - 1f;
                pad += (s + s2) * 0.5f;
            }
            pad *= 0.03f * (0.4f + 0.6f * Mathf.Exp(-i16 * 12));
            // lead hook on the second half
            float ld = 0;
            if (barIdx >= 4)
            {
                float lf = lead[e8i % 16];
                float sq = Mathf.Sin(TAU * lf * t) > 0 ? 1f : -1f;
                ld = (sq * 0.35f + Mathf.Sin(TAU * lf * t) * 0.65f) * Mathf.Exp(-i8 * 5) * 0.06f;
            }
            float duck = 1f - 0.5f * Mathf.Exp(-ib * 10);
            d[i] = Mathf.Clamp((kick + snare + hat + (bass + pad + ld) * duck) * 0.8f, -1, 1);
        }
        return Clip("music", d);
    }
}
