using System;
using UnityEngine;

namespace PacMan.Game
{
    /// <summary>
    /// Chiptune-style sound effects, synthesised at startup (no audio files). The intro jingle is an
    /// original tune, not the arcade's.
    /// </summary>
    public sealed class Sfx
    {
        const int Rate = 22050;

        readonly AudioSource oneShot, loop, chomp, music;
        readonly AudioClip wa, ka, siren, fright, eyes, eatGhost, eatFruit, death, intro, extraLife, coin, blip, titleTune;
        bool chompToggle, muted;

        /// <summary>The M key: silences everything without touching the saved volumes.</summary>
        public bool Muted
        {
            get => muted;
            set
            {
                muted = value;
                ApplyVolumes();
            }
        }

        public Sfx(GameObject host)
        {
            oneShot = host.AddComponent<AudioSource>();
            loop = host.AddComponent<AudioSource>();
            chomp = host.AddComponent<AudioSource>();
            music = host.AddComponent<AudioSource>();
            foreach (var s in new[] { oneShot, loop, chomp, music })
            {
                s.playOnAwake = false;
                s.spatialBlend = 0f;
            }
            loop.loop = true;

            wa = Sweep("wa", 0.11f, 760f, 300f, Wave.Triangle, 0.9f);
            ka = Sweep("ka", 0.11f, 300f, 760f, Wave.Triangle, 0.9f);
            siren = MakeSiren();
            fright = MakeFright();
            eyes = MakeEyes();
            eatGhost = MakeEatGhost();
            eatFruit = MakeEatFruit();
            death = MakeDeath();
            intro = MakeIntro();
            extraLife = MakeExtraLife();
            coin = Sweep("coin", 0.18f, 900f, 1800f, Wave.Square, 0.35f);
            blip = Sweep("blip", 0.045f, 700f, 950f, Wave.Square, 0.3f);
            titleTune = MakeTitleTune();
            ApplyVolumes();
        }

        /// <summary>Applies <see cref="Settings"/>: master on the listener, then one level per channel.</summary>
        public void ApplyVolumes()
        {
            AudioListener.volume = muted ? 0f : Settings.Master;
            oneShot.volume = Settings.Effects;
            chomp.volume = 0.5f * Settings.Effects;
            loop.volume = 0.32f * Settings.Siren;
            music.volume = 0.55f * Settings.Music;
        }

        // ------------------------------------------------------------------ playback

        public void Chomp()
        {
            if (chomp.isPlaying) return;
            chomp.clip = chompToggle ? ka : wa;
            chompToggle = !chompToggle;
            chomp.Play();
        }

        public void Coin() => Play(coin, 0.6f);
        public void Blip() => Play(blip, 0.5f);
        public void EatGhost() => Play(eatGhost, 0.6f);
        public void EatFruit() => Play(eatFruit, 0.6f);
        public void ExtraLife() => Play(extraLife, 0.6f);

        public void Death()
        {
            StopLoop();
            Play(death, 0.6f);
        }

        void Play(AudioClip clip, float volume) => oneShot.PlayOneShot(clip, volume);

        /// <summary>The jingle before a game, on the music channel (it replaces the title tune).</summary>
        public void Intro()
        {
            music.loop = false;
            music.clip = intro;
            music.Play();
        }

        /// <summary>A gentle looping tune behind the title screen and the demo.</summary>
        public void TitleMusic()
        {
            if (music.clip == titleTune && music.isPlaying) return;
            music.loop = true;
            music.clip = titleTune;
            music.Play();
        }

        public void StopMusic() => music.Stop();

        public enum Loop { None, Siren, Fright, Eyes }

        /// <summary>Keeps the right background loop running. Siren pitch rises as the maze empties.</summary>
        public void SetLoop(Loop which, float progress)
        {
            var clip = which == Loop.Siren ? siren : which == Loop.Fright ? fright : which == Loop.Eyes ? eyes : null;
            if (clip == null)
            {
                StopLoop();
                return;
            }
            if (loop.clip != clip || !loop.isPlaying)
            {
                loop.clip = clip;
                loop.Play();
            }
            loop.pitch = which == Loop.Siren ? 1f + 0.45f * Mathf.Clamp01(progress) : 1f;
        }

        public void StopLoop()
        {
            if (loop.isPlaying) loop.Stop();
        }

        public void StopAll()
        {
            StopLoop();
            oneShot.Stop();
            chomp.Stop();
            music.Stop();
        }

        // ------------------------------------------------------------------ synthesis

        enum Wave { Square, Triangle, Sine, Noise }

        static readonly System.Random Noise = new System.Random(5);

        static float Osc(Wave w, double phase)
        {
            double f = phase - Math.Floor(phase);
            switch (w)
            {
                case Wave.Square: return f < 0.5 ? 0.6f : -0.6f;
                case Wave.Triangle: return (float)(4.0 * Math.Abs(f - 0.5) - 1.0);
                case Wave.Noise: return (float)(Noise.NextDouble() * 2.0 - 1.0) * 0.5f;
                default: return (float)Math.Sin(f * Math.PI * 2.0);
            }
        }

        /// <summary>Renders a tone whose frequency follows f(u) for u in [0, 1], with short fades at both ends.</summary>
        static void Tone(float[] buf, int start, int length, Func<float, float> freq, Wave wave, float volume)
        {
            double phase = 0;
            int fade = Mathf.Min(length / 4, Rate / 250);
            for (int i = 0; i < length && start + i < buf.Length; i++)
            {
                float u = (float)i / length;
                phase += freq(u) / Rate;
                float env = 1f;
                if (i < fade) env = (float)i / fade;
                else if (i > length - fade) env = (float)(length - i) / fade;
                buf[start + i] += Osc(wave, phase) * volume * env;
            }
        }

        static AudioClip Clip(string name, float[] data)
        {
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        static AudioClip Sweep(string name, float seconds, float from, float to, Wave wave, float volume)
        {
            var buf = Buffer(seconds);
            Tone(buf, 0, buf.Length, u => Mathf.Lerp(from, to, u), wave, volume);
            return Clip(name, buf);
        }

        /// <summary>A loop whose frequency moves smoothly between lo and hi, tuned to a whole number of cycles.</summary>
        static AudioClip Wobble(string name, float seconds, int wobbles, float lo, float hi, Wave wave, float volume)
        {
            var buf = Buffer(seconds);
            Func<float, float> f = u => Mathf.Lerp(lo, hi, 0.5f - 0.5f * Mathf.Cos(u * wobbles * Mathf.PI * 2f));
            double cycles = 0;
            for (int i = 0; i < buf.Length; i++) cycles += f((float)i / buf.Length) / Rate;
            float k = (float)(Math.Round(cycles) / cycles);
            double phase = 0;
            for (int i = 0; i < buf.Length; i++)
            {
                phase += f((float)i / buf.Length) * k / Rate;
                buf[i] = Osc(wave, phase) * volume;
            }
            return Clip(name, buf);
        }

        static AudioClip MakeSiren() => Wobble("siren", 0.84f, 2, 330f, 620f, Wave.Sine, 0.7f);
        static AudioClip MakeFright() => Wobble("fright", 0.6f, 4, 140f, 300f, Wave.Triangle, 0.7f);

        static AudioClip MakeEyes()
        {
            var buf = Buffer(0.6f);
            int seg = buf.Length / 4;
            for (int i = 0; i < 4; i++) Tone(buf, i * seg, seg, u => Mathf.Lerp(1500f, 500f, u), Wave.Square, 0.35f);
            return Clip("eyes", buf);
        }

        static AudioClip MakeEatGhost()
        {
            var buf = Buffer(0.5f);
            Tone(buf, 0, buf.Length, u => Mathf.Lerp(180f, 1900f, u * u) * (1f + 0.06f * Mathf.Sin(u * 90f)), Wave.Square, 0.45f);
            return Clip("eatGhost", buf);
        }

        static AudioClip MakeEatFruit()
        {
            var buf = Buffer(0.36f);
            Tone(buf, 0, buf.Length, u => Mathf.Repeat(u * 8f, 1f) < 0.5f ? 640f : 1280f, Wave.Triangle, 0.8f);
            return Clip("eatFruit", buf);
        }

        static AudioClip MakeDeath()
        {
            var buf = Buffer(1.55f);
            int seg = Rate * 12 / 100;
            for (int i = 0; i < 9; i++)
            {
                float top = 980f - i * 75f;
                Tone(buf, i * seg, seg, u => Mathf.Lerp(top, top * 0.45f, u), Wave.Triangle, 0.8f);
            }
            int popStart = 9 * seg + seg / 2;
            for (int p = 0; p < 2; p++)
                Tone(buf, popStart + p * seg, seg * 2 / 3, u => Mathf.Lerp(150f, 900f, u), Wave.Square, 0.45f);
            return Clip("death", buf);
        }

        static AudioClip MakeExtraLife()
        {
            var buf = Buffer(0.75f);
            int seg = Rate / 4;
            for (int i = 0; i < 3; i++) Tone(buf, i * seg, seg * 2 / 3, u => 1568f, Wave.Square, 0.35f);
            return Clip("extraLife", buf);
        }

        /// <summary>
        /// An original, easy-going 8-bar loop (C - Am - F - G twice) for the title screen: square lead,
        /// soft triangle arpeggios and bass. 0 holds the previous note, -1 is a rest.
        /// </summary>
        static AudioClip MakeTitleTune()
        {
            int[] lead =
            {
                76, 0, 79, 76, 74, 72, 74, 76,
                72, 0, 69, 72, 76, 0, 74, 72,
                69, 72, 77, 76, 74, 0, 72, 69,
                71, 74, 79, 0, 77, 76, 74, -1,
                76, 0, 79, 81, 79, 76, 74, 76,
                72, 76, 81, 0, 79, 76, 72, -1,
                77, 76, 74, 72, 74, 77, 81, 79,
                79, 0, 77, 74, 71, 0, 74, -1,
            };
            int[][] chords = { new[] { 60, 64, 67 }, new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 } };
            const float eighth = 0.25f;
            int step = Mathf.RoundToInt(eighth * Rate);
            var buf = new float[step * lead.Length];

            for (int i = 0; i < lead.Length; i++)
            {
                if (lead[i] <= 0) continue;
                int len = 1;
                while (i + len < lead.Length && lead[i + len] == 0) len++;
                float f = Midi(lead[i]);
                Tone(buf, i * step, step * len - step / 8, u => f * (1f + 0.004f * Mathf.Sin(u * 40f)), Wave.Square, 0.16f);
            }

            int sixteenth = step / 2;
            for (int bar = 0; bar < 8; bar++)
            {
                var chord = chords[bar % 4];
                for (int k = 0; k < 16; k++)
                {
                    float f = Midi(chord[k % 3] + 12);
                    Tone(buf, bar * 8 * step + k * sixteenth, sixteenth * 3 / 4, u => f, Wave.Triangle, 0.1f);
                }
                for (int q = 0; q < 4; q++)
                {
                    float f = Midi(chord[0] - 24 + (q % 2 == 1 ? 7 : 0));
                    Tone(buf, bar * 8 * step + q * 2 * step, step * 2 - step / 4, u => f, Wave.Triangle, 0.38f);
                }
            }
            return Clip("titleTune", buf);
        }

        static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

        /// <summary>A bouncy original 4-bar jingle: square-wave lead over a triangle bass.</summary>
        static AudioClip MakeIntro()
        {
            int[] lead =
            {
                72, 76, 79, 84, 83, 79, 76, 79,
                74, 77, 81, 86, 84, 81, 77, 81,
                76, 79, 83, 88, 86, 83, 79, 83,
                84, 84, 79, 79, 76, 76, 72, -1,
            };
            int[] bass = { 48, 55, 50, 57, 52, 59, 48, -1 };
            const float eighth = 4.2f / 32f;
            var buf = Buffer(4.2f);
            int step = Mathf.RoundToInt(eighth * Rate);
            for (int i = 0; i < lead.Length; i++)
            {
                if (lead[i] < 0) continue;
                float f = Midi(lead[i]);
                Tone(buf, i * step, step * 9 / 10, u => f, Wave.Square, 0.32f);
            }
            for (int i = 0; i < bass.Length; i++)
            {
                if (bass[i] < 0) continue;
                float f = Midi(bass[i]);
                Tone(buf, i * step * 4, step * 7 / 2, u => f, Wave.Triangle, 0.55f);
            }
            return Clip("intro", buf);
        }
    }
}
