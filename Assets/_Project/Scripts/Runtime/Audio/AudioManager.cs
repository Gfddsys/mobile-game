using System.Collections.Generic;
using UnityEngine;

namespace TurtleBlaster
{
    /// <summary>
    /// Plays SFX and music. Everything is synthesised in code as placeholder
    /// 16-bit chiptune style audio. To use real audio just drop clips named
    /// like the keys below into Resources/Audio (e.g. "coin", "music_run").
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        const int Rate = 22050;

        AudioSource sfx, thrustSrc, musicSrc;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        bool thrusting;

        // Null-safe static helpers (Unity objects must not be used with ?.)
        public static void Sfx(string name, float volume = 1f, float pitch = 1f) { if (Instance != null) Instance.Play(name, volume, pitch); }
        public static void Thrust(bool on) { if (Instance != null) Instance.SetThrust(on); }
        public static void StartMusic() { if (Instance != null) Instance.PlayMusic(); }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public static AudioManager Create(Transform parent)
        {
            var go = new GameObject("AudioManager");
            go.transform.SetParent(parent, false);
            return go.AddComponent<AudioManager>();
        }

        void Awake()
        {
            Instance = this;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            thrustSrc = gameObject.AddComponent<AudioSource>();
            thrustSrc.loop = true; thrustSrc.volume = 0f; thrustSrc.playOnAwake = false;
            musicSrc = gameObject.AddComponent<AudioSource>();
            musicSrc.loop = true; musicSrc.volume = 0.35f; musicSrc.playOnAwake = false;
        }

        void Update()
        {
            var save = SaveData.Current;
            float target = (thrusting && save.sfxOn) ? 0.4f : 0f;
            if (target > 0f && !thrustSrc.isPlaying)
            {
                thrustSrc.clip = Get("thrust");
                thrustSrc.Play();
            }
            thrustSrc.volume = Mathf.MoveTowards(thrustSrc.volume, target, Time.unscaledDeltaTime * 6f);
            if (thrustSrc.volume <= 0.001f && thrustSrc.isPlaying) thrustSrc.Pause();

            musicSrc.mute = !save.musicOn;
        }

        public void PlayMusic()
        {
            if (musicSrc.clip == null) musicSrc.clip = Get("music_run");
            if (!musicSrc.isPlaying) musicSrc.Play();
        }

        public void SetThrust(bool on) => thrusting = on;

        public void Play(string name, float volume = 1f, float pitch = 1f)
        {
            if (!SaveData.Current.sfxOn) return;
            sfx.pitch = pitch;
            sfx.PlayOneShot(Get(name), volume);
        }

        AudioClip Get(string name)
        {
            if (clips.TryGetValue(name, out var c) && c != null) return c;
            c = Resources.Load<AudioClip>("Audio/" + name);
            if (c == null) c = Generate(name);
            clips[name] = c;
            return c;
        }

        // ---------------------------------------------------------------
        // Synthesis
        // ---------------------------------------------------------------
        static AudioClip Generate(string name)
        {
            switch (name)
            {
                case "coin":    return Seq(name, 0.09f, Wave.Square, 0.25f, 988, 1319);
                case "part":    return Seq(name, 0.08f, Wave.Square, 0.25f, 523, 659, 784, 1047);
                case "perfect": return Seq(name, 0.07f, Wave.Square, 0.28f, 523, 659, 784, 1047, 1319);
                case "rough":   return Seq(name, 0.09f, Wave.Triangle, 0.4f, 196, 147);
                case "flip":    return Seq(name, 0.05f, Wave.Square, 0.2f, 440, 554, 659);
                case "boost":   return Seq(name, 0.06f, Wave.Saw, 0.25f, 262, 330, 392, 523, 659, 784, 1047);
                case "hit":     return Noise(name, 0.25f, 0.5f, 0.35f);
                case "crash":   return Noise(name, 0.7f, 0.7f, 0.4f);
                case "click":   return Seq(name, 0.04f, Wave.Square, 0.2f, 784);
                case "buy":     return Seq(name, 0.07f, Wave.Square, 0.25f, 659, 784, 988, 1319);
                case "deny":    return Seq(name, 0.1f, Wave.Square, 0.25f, 196, 165);
                case "thrust":  return LoopNoise(name);
                case "music_run": return Music();
            }
            return Seq(name, 0.1f, Wave.Square, 0.2f, 440);
        }

        enum Wave { Square, Triangle, Saw, Pulse25 }

        static float Osc(Wave w, float phase)
        {
            phase -= Mathf.Floor(phase);
            switch (w)
            {
                case Wave.Square: return phase < 0.5f ? 1f : -1f;
                case Wave.Pulse25: return phase < 0.25f ? 1f : -1f;
                case Wave.Triangle: return Mathf.Abs(phase * 4f - 2f) - 1f;
                default: return phase * 2f - 1f;
            }
        }

        static AudioClip Seq(string name, float noteLen, Wave wave, float vol, params float[] freqs)
        {
            int per = Mathf.RoundToInt(noteLen * Rate);
            var data = new float[per * freqs.Length];
            for (int n = 0; n < freqs.Length; n++)
                for (int i = 0; i < per; i++)
                {
                    float env = 1f - (i / (float)per) * 0.6f;
                    float t = i / (float)Rate;
                    data[n * per + i] = Osc(wave, freqs[n] * t) * vol * env;
                }
            return MakeClip(name, data);
        }

        static AudioClip Noise(string name, float seconds, float vol, float decay)
        {
            var rng = new System.Random(7);
            int len = Mathf.RoundToInt(seconds * Rate);
            var data = new float[len];
            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float env = Mathf.Exp(-i / (len * decay));
                float n = (float)(rng.NextDouble() * 2 - 1);
                lp += (n - lp) * 0.35f;
                data[i] = lp * vol * env;
            }
            return MakeClip(name, data);
        }

        static AudioClip LoopNoise(string name)
        {
            var rng = new System.Random(3);
            int len = Rate / 2;
            var data = new float[len];
            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float n = (float)(rng.NextDouble() * 2 - 1);
                lp += (n - lp) * 0.18f;
                data[i] = lp * 0.6f;
            }
            // cross-fade the loop point to avoid a click
            int fade = 400;
            for (int i = 0; i < fade; i++)
            {
                float a = i / (float)fade;
                data[i] = Mathf.Lerp(data[len - fade + i], data[i], a);
            }
            return MakeClip(name, data);
        }

        static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

        /// <summary>Simple 16-bar punk-skate chiptune loop (Am - F - C - G).</summary>
        static AudioClip Music()
        {
            const float bpm = 150f;
            float step = 60f / bpm / 2f; // eighth note
            int stepSamples = Mathf.RoundToInt(step * Rate);
            int[][] chords =
            {
                new[] { 57, 60, 64 }, // Am
                new[] { 53, 57, 60 }, // F
                new[] { 48, 52, 55 }, // C
                new[] { 55, 59, 62 }, // G
            };
            int[] bassRoot = { 33, 29, 36, 31 };
            int bars = 8;
            int steps = bars * 8;
            var data = new float[steps * stepSamples];
            var rng = new System.Random(11);

            for (int s = 0; s < steps; s++)
            {
                int bar = s / 8;
                var chord = chords[bar % 4];
                int inBar = s % 8;
                // lead arpeggio (octave up, melody variation every other bar)
                int arp = (bar % 2 == 0) ? new[] { 0, 1, 2, 1, 0, 1, 2, 1 }[inBar] : new[] { 2, 1, 0, 1, 2, 2, 1, 0 }[inBar];
                float leadF = Midi(chord[arp] + 12);
                float bassF = Midi(bassRoot[bar % 4] + ((inBar % 2 == 1) ? 12 : 0));
                for (int i = 0; i < stepSamples; i++)
                {
                    int idx = s * stepSamples + i;
                    float t = i / (float)Rate;
                    float env = 1f - (i / (float)stepSamples) * 0.7f;
                    float v = Osc(Wave.Pulse25, leadF * t + s * 0.13f) * 0.10f * env;
                    v += Osc(Wave.Triangle, bassF * t) * 0.22f;
                    // drums: kick on 0 & 4, snare noise on 2 & 6, hat on odd
                    if (inBar == 0 || inBar == 4)
                        v += Mathf.Sin(2 * Mathf.PI * (90f - t * 200f) * t) * 0.35f * Mathf.Exp(-t * 18f);
                    if (inBar == 2 || inBar == 6)
                        v += ((float)rng.NextDouble() * 2 - 1) * 0.16f * Mathf.Exp(-t * 22f);
                    if (inBar % 2 == 1)
                        v += ((float)rng.NextDouble() * 2 - 1) * 0.05f * Mathf.Exp(-t * 60f);
                    data[idx] = Mathf.Clamp(v, -1f, 1f);
                }
            }
            return MakeClip("music_run", data);
        }

        static AudioClip MakeClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
