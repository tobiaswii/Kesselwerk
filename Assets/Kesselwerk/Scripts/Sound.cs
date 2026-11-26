using System.Collections.Generic;
using Kesselwerk.Core;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// Audiosystem mit getrennter Lautstärke für Musik und Effekte.
    /// PLATZHALTER: Alle Klänge werden zur Laufzeit synthetisiert (keine Fremd-Assets).
    /// Echte Sounds können später einfach unter Resources/Audio/&lt;Name&gt; abgelegt werden und ersetzen dann den synthetischen Klang.
    /// </summary>
    public class Sound : MonoBehaviour
    {
        static Sound instance;
        static float musicVolume = 0.6f, sfxVolume = 0.8f;
        AudioSource music;
        readonly List<AudioSource> sfx = new List<AudioSource>();
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        const int Rate = 22050;

        public static float MusicVolume
        {
            get => musicVolume;
            set { musicVolume = Mathf.Clamp01(value); if (instance != null) instance.music.volume = musicVolume * 0.35f; }
        }

        public static float SfxVolume
        {
            get => sfxVolume;
            set => sfxVolume = Mathf.Clamp01(value);
        }

        static Sound I
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("Sound");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<Sound>();
                    instance.music = go.AddComponent<AudioSource>();
                    instance.music.loop = true;
                    instance.music.volume = musicVolume * 0.35f;
                    for (int i = 0; i < 8; i++) instance.sfx.Add(go.AddComponent<AudioSource>());
                }
                return instance;
            }
        }

        public static void Play(string name, float volume = 1f, float pitch = 1f)
        {
            if (sfxVolume <= 0.001f) return;
            var s = I;
            var clip = s.Get(name);
            if (clip == null) return;
            AudioSource src = s.sfx[0];
            foreach (var a in s.sfx) if (!a.isPlaying) { src = a; break; }
            src.pitch = pitch;
            src.PlayOneShot(clip, volume * sfxVolume);
        }

        /// <summary>Jede Kreatur hat eine eigene Stimme (Tonhöhe aus Größe und Element).</summary>
        public static void Voice(CreatureDef d, float volume = 0.6f)
        {
            int h = 0;
            foreach (char ch in d.id) h = h * 31 + ch;
            float pitch = 0.7f + (h & 0xFF) / 255f * 0.8f;
            if (d.role == Role.Tank || d.isBoss) pitch *= 0.65f;
            Play("stimme_" + ((int)d.element % 4), volume, pitch);
        }

        public static void Music(bool battle)
        {
            var s = I;
            var clip = s.Get(battle ? "musik_kampf" : "musik_menue");
            if (s.music.clip == clip && s.music.isPlaying) return;
            s.music.clip = clip;
            s.music.volume = musicVolume * 0.35f;
            s.music.Play();
        }

        AudioClip Get(string name)
        {
            if (clips.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + name) ?? Synth(name);
            clips[name] = c;
            return c;
        }

        // ------------------------------------------------------------------
        // Synthese
        // ------------------------------------------------------------------

        static AudioClip Make(string name, float seconds, System.Func<float, float> f)
        {
            int n = Mathf.Max(1, (int)(seconds * Rate));
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Sin(float t, float hz) => Mathf.Sin(2f * Mathf.PI * hz * t);
        static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);
        static readonly System.Random rnd = new System.Random(7);
        static float Noise() => (float)rnd.NextDouble() * 2f - 1f;

        static AudioClip Synth(string name)
        {
            switch (name)
            {
                case "klick": return Make(name, 0.06f, t => Sin(t, 1100f) * Env(t, 0.002f, 0.015f) * 0.5f);
                case "treffer": return Make(name, 0.22f, t => (Noise() * 0.6f + Sin(t, 90f - t * 200f)) * Env(t, 0.003f, 0.05f) * 0.7f);
                case "krit": return Make(name, 0.3f, t => (Noise() * 0.5f + Sin(t, 120f) + Sin(t, 1600f) * 0.3f) * Env(t, 0.002f, 0.08f) * 0.8f);
                case "zisch": return Make(name, 0.25f, t => Noise() * Mathf.Sin(t / 0.25f * Mathf.PI) * 0.35f);
                case "zauber": return Make(name, 0.8f, t =>
                    (Sin(t, 660f) * Env(t, 0.01f, 0.2f) + Sin(t, 880f) * Env(Mathf.Max(0, t - 0.1f), 0.01f, 0.2f) * (t > 0.1f ? 1 : 0) +
                     Sin(t, 1320f) * Env(Mathf.Max(0, t - 0.2f), 0.01f, 0.3f) * (t > 0.2f ? 1 : 0)) * 0.3f);
                case "heilung": return Make(name, 0.7f, t => Sin(t, 520f + t * 600f) * Env(t, 0.05f, 0.25f) * 0.35f);
                case "schild": return Make(name, 0.5f, t => (Sin(t, 300f) + Sin(t, 450f) * 0.5f) * Env(t, 0.02f, 0.18f) * 0.35f);
                case "tod": return Make(name, 0.6f, t => Sin(t, 400f - t * 500f) * Env(t, 0.01f, 0.2f) * 0.4f);
                case "beschwoerung": return Make(name, 1.4f, t => (Sin(t, 200f + t * t * 500f) * 0.5f + Noise() * 0.15f * t) * Env(t, 0.6f, 0.4f) * 0.5f);
                case "selten": return Make(name, 1.2f, t =>
                    (Sin(t, 523f) + Sin(t, 659f) * (t > 0.12f ? 1 : 0) + Sin(t, 784f) * (t > 0.24f ? 1 : 0) + Sin(t, 1047f) * (t > 0.36f ? 1 : 0)) *
                    Env(t, 0.01f, 0.5f) * 0.22f);
                case "sieg": return Make(name, 1.6f, t =>
                    (Sin(t, 392f) * (t < 0.2f ? 1 : 0) + Sin(t, 523f) * (t >= 0.2f && t < 0.4f ? 1 : 0) +
                     (Sin(t, 659f) + Sin(t, 784f) * 0.7f + Sin(t, 523f) * 0.5f) * (t >= 0.4f ? 1 : 0)) * Env(t, 0.01f, 0.7f) * 0.3f);
                case "niederlage": return Make(name, 1.4f, t =>
                    (Sin(t, 392f) * (t < 0.35f ? 1 : 0) + Sin(t, 349f) * (t >= 0.35f && t < 0.7f ? 1 : 0) + Sin(t, 311f) * (t >= 0.7f ? 1 : 0)) *
                    Env(t, 0.02f, 0.6f) * 0.3f);
                case "stimme_0": return Make(name, 0.25f, t => Sin(t, 300f + 120f * Sin(t, 12f)) * Env(t, 0.01f, 0.08f) * 0.4f);
                case "stimme_1": return Make(name, 0.22f, t => (Sin(t, 260f - t * 300f) + Sin(t, 520f) * 0.3f) * Env(t, 0.01f, 0.07f) * 0.4f);
                case "stimme_2": return Make(name, 0.3f, t => Sin(t, 220f + t * 400f) * Env(t, 0.02f, 0.1f) * 0.4f);
                case "stimme_3": return Make(name, 0.2f, t => (Sin(t, 350f) * Sin(t, 30f)) * Env(t, 0.005f, 0.07f) * 0.5f);
                case "musik_menue": return Music(12f, new[] { 261.6f, 329.6f, 392f, 349.2f }, 0.25f);
                case "musik_kampf": return Music(8f, new[] { 220f, 261.6f, 196f, 246.9f }, 0.125f);
            }
            return null;
        }

        /// <summary>Einfache Hintergrundmusik: weiche Akkorde mit Arpeggio (nahtlose Schleife).</summary>
        static AudioClip Music(float seconds, float[] roots, float noteLen)
        {
            float bar = seconds / roots.Length;
            return Make("musik", seconds, t =>
            {
                int b = Mathf.Min(roots.Length - 1, (int)(t / bar));
                float root = roots[b];
                float tb = t - b * bar;
                float pad = (Sin(t, root * 0.5f) + Sin(t, root * 0.75f) * 0.6f + Sin(t, root) * 0.5f) *
                            Mathf.Clamp01(tb / 0.4f) * Mathf.Clamp01((bar - tb) / 0.4f) * 0.18f;
                int step = (int)(t / noteLen);
                float tn = t - step * noteLen;
                float[] mul = { 1f, 1.25f, 1.5f, 2f, 1.5f, 1.25f };
                float arp = Sin(t, root * 2f * mul[step % mul.Length]) * Mathf.Exp(-tn / (noteLen * 0.5f)) * 0.12f;
                return pad + arp;
            });
        }
    }
}
