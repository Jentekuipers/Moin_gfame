using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Every sound is synthesised at startup: no audio assets needed.
    public class Sfx
    {
        const int SR = 44100;
        readonly AudioSource[] src;
        int idx;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly System.Random r = new System.Random(7);
        public float master = 0.85f;

        public Sfx(GameObject host)
        {
            src = new AudioSource[28];
            for (int i = 0; i < src.Length; i++)
            {
                src[i] = host.AddComponent<AudioSource>();
                src[i].playOnAwake = false;
                src[i].spatialBlend = 0f;
            }
            Build();
        }

        // A sound that happens somewhere in the world: full volume close to you, fading out with distance,
        // silent beyond `range` tiles.
        public void PlayAt(Vector2 at, string n, float vol = 1f, float pitch = 1f, float var = 0.06f, float range = 11f)
        {
            var g = Game.I;
            if (g != null && g.player != null)
            {
                float d = (g.player.pos - at).magnitude;
                if (d >= range) return;
                float k = 1f - Mathf.Clamp01((d - range * 0.35f) / (range * 0.65f));
                vol *= k;
                if (vol < 0.02f) return;
            }
            Play(n, vol, pitch, var);
        }

        public void Play(string n, float vol = 1f, float pitch = 1f, float var = 0.06f)
        {
            if (!clips.TryGetValue(n, out var c)) return;
            var s = src[idx];
            idx = (idx + 1) % src.Length;
            s.pitch = pitch * (1f + Random.Range(-var, var));
            s.volume = vol * master;
            s.clip = c;
            s.Play();
        }

        // Block hit sounds by material. Vlambeer rules: several variants, random pitch every time,
        // quiet, and rate-limited per material so a machine pistol into rock doesn't become a drill in your ear.
        readonly Dictionary<string, float> lastMat = new Dictionary<string, float>();
        int matRoll;

        public void Material(Block b, bool broke, float vol = 1f)
        {
            string m = MaterialOf(b);
            if (m == null) return;
            float now = Time.unscaledTime;
            if (lastMat.TryGetValue(m, out var t) && now - t < 0.05f) return;
            lastMat[m] = now;
            matRoll = (matRoll + 1 + Random.Range(0, 2)) % 3;
            Play(m + matRoll, (broke ? 0.3f : 0.2f) * vol, Random.Range(0.85f, 1.15f) * (broke ? 0.88f : 1f), 0f);
        }

        static string MaterialOf(Block b)
        {
            switch (b)
            {
                case Block.Dirt: case Block.Sand: return "m_dirt";
                case Block.Stone: case Block.HardStone: case Block.Obsidian: case Block.Boulder: case Block.Magma: case Block.Brick: return "m_rock";
                case Block.Gold: case Block.Gem: case Block.Crystal: case Block.Volatile: case Block.BigGold2: case Block.BigGold4: case Block.BigGold6: case Block.Ice: return "m_crystal";
                case Block.Crate: case Block.Spikes: return "m_metal";
                case Block.Chest: case Block.TNT: case Block.Wood: return "m_wood";
                case Block.DarkMatter: case Block.Seal: case Block.Bedrock: return "m_void";
            }
            return null;
        }

        // ---------------------------------------------------------------- reloads: one per kind of weapon
        // a metallic click at time t0 (pitch f), and a short noisy slide between t0 and t1
        float Click(float t, float t0, float f, float vol = 0.5f)
        {
            float d = t - t0;
            if (d < 0f || d > 0.06f) return 0f;
            return (Mathf.Sin(2 * Mathf.PI * f * d) * 0.6f + Mathf.Sin(2 * Mathf.PI * f * 1.62f * d) * 0.4f + Noise() * 0.6f) * vol * Mathf.Exp(-d * 90f);
        }

        float Slide(float t, float t0, float t1, float vol = 0.25f)
        {
            if (t < t0 || t > t1) return 0f;
            return Noise() * vol * Mathf.Sin(Mathf.PI * (t - t0) / (t1 - t0));
        }

        void BuildReloads()
        {
            // revolver: cylinder swings out, shells drop, six quick clicks, snap shut
            Make("rl_revolver", 0.8f, t =>
            {
                float v = Click(t, 0f, 1800f, 0.6f) + Slide(t, 0.04f, 0.2f, 0.15f);
                for (int i = 0; i < 6; i++) v += Click(t, 0.25f + i * 0.07f, 2600f + i * 40f, 0.3f);
                return v + Click(t, 0.72f, 1500f, 0.8f);
            });
            // magazine weapons: mag out, mag in, charging handle
            Make("rl_mag", 0.9f, t => Click(t, 0f, 1200f, 0.5f) + Slide(t, 0.02f, 0.15f, 0.2f) + Click(t, 0.45f, 900f, 0.8f) + Slide(t, 0.62f, 0.78f, 0.25f) + Click(t, 0.8f, 2100f, 0.6f));
            // a single shell pushed in
            Make("rl_shell", 0.14f, t => Slide(t, 0f, 0.08f, 0.2f) + Click(t, 0.08f, 1400f, 0.5f));
            // shotgun pump: back and forward
            Make("rl_pump", 0.3f, t => Slide(t, 0f, 0.1f, 0.35f) + Click(t, 0.1f, 700f, 0.7f) + Slide(t, 0.14f, 0.24f, 0.35f) + Click(t, 0.24f, 950f, 0.8f));
            // bolt caster: the string creaks back and clicks
            float ph = 0f;
            Make("rl_bolt", 0.7f, t =>
            {
                ph += (90f + t * 160f) / SR;
                float creak = t < 0.55f ? Saw(ph) * 0.15f * (0.6f + 0.4f * Mathf.Sin(t * 70f)) : 0f;
                return creak + Click(t, 0.58f, 1700f, 0.8f);
            });
            // sniper: bolt up, back, forward, down
            Make("rl_sniper", 0.7f, t => Click(t, 0f, 1300f, 0.6f) + Slide(t, 0.08f, 0.22f, 0.3f) + Click(t, 0.25f, 800f, 0.6f) + Slide(t, 0.4f, 0.55f, 0.3f) + Click(t, 0.58f, 1100f, 0.8f) + Click(t, 0.66f, 1500f, 0.6f));
            // grenade launcher: a fat shell thunks into the tube
            float ph2 = 0f;
            Make("rl_grenade", 0.2f, t =>
            {
                ph2 += Mathf.Lerp(220f, 120f, t / 0.2f) / SR;
                return Sin(ph2) * 0.5f * Mathf.Exp(-t * 25f) + Click(t, 0.02f, 600f, 0.5f);
            });
            // rocket: heavy metal tube scrape and a clunk
            Make("rl_rocket", 0.9f, t =>
            {
                float clunk = t > 0.7f ? Mathf.Sin(2 * Mathf.PI * 140f * (t - 0.7f)) * 0.7f * Mathf.Exp(-(t - 0.7f) * 18f) : 0f;
                return Slide(t, 0.05f, 0.6f, 0.3f) + clunk + Click(t, 0.72f, 600f, 0.6f);
            });
            // flamethrower: canister clank and a hiss of gas
            float lp = 0f;
            Make("rl_flame", 1f, t =>
            {
                lp += (Noise() - lp) * 0.5f;
                float hiss = t > 0.3f && t < 0.9f ? (Noise() - lp) * 0.3f * Mathf.Sin(Mathf.PI * (t - 0.3f) / 0.6f) : 0f;
                return Click(t, 0f, 500f, 0.8f) + Click(t, 0.25f, 700f, 0.6f) + hiss;
            });
            // disc gun: blades slotting in with a ring
            Make("rl_disc", 0.6f, t =>
            {
                float v = 0f;
                for (int i = 0; i < 3; i++)
                {
                    float d = t - (0.1f + i * 0.15f);
                    if (d >= 0f) v += (Mathf.Sin(2 * Mathf.PI * 2600 * d) + 0.6f * Mathf.Sin(2 * Mathf.PI * 3900 * d)) * 0.12f * Mathf.Exp(-d * 20f);
                }
                return v + Click(t, 0f, 1100f, 0.5f);
            });
            // energy weapons: steam venting
            float lp2 = 0f;
            Make("vent", 0.8f, t =>
            {
                lp2 += (Noise() - lp2) * 0.6f;
                return (Noise() - lp2 * 0.5f) * 0.45f * Mathf.Exp(-t * 3f) * Mathf.Min(1f, t * 60f);
            });
        }

        void BuildMaterials()
        {
            for (int v = 0; v < 3; v++)
            {
                float lp = 0, lp2 = 0, ph = 0;
                float f1 = 650f + v * 170f;
                // rock: a dry clack, inharmonic pings and a little low knock
                Make("m_rock" + v, 0.13f, t =>
                {
                    float n = Noise();
                    lp += (n - lp) * 0.5f;
                    lp2 += (lp - lp2) * 0.12f;
                    float ping = Mathf.Sin(2 * Mathf.PI * f1 * t) + 0.6f * Mathf.Sin(2 * Mathf.PI * f1 * 1.47f * t) + 0.35f * Mathf.Sin(2 * Mathf.PI * f1 * 2.31f * t);
                    return (lp - lp2) * 1.2f * Mathf.Exp(-t * 55f) + ping * 0.16f * Mathf.Exp(-t * 48f) + Mathf.Sin(2 * Mathf.PI * (110f + v * 12f) * t) * 0.35f * Mathf.Exp(-t * 35f);
                });

                lp = 0; float fd = 70f + v * 14f, a = 0.07f + v * 0.025f;
                // dirt: a soft, muffled thud
                Make("m_dirt" + v, 0.1f, t =>
                {
                    lp += (Noise() - lp) * a;
                    return lp * 2.2f * Mathf.Exp(-t * 32f) + Mathf.Sin(2 * Mathf.PI * fd * t) * 0.45f * Mathf.Exp(-t * 30f);
                });

                float fc = 2100f + v * 380f;
                // crystal / ore: a glassy ting
                Make("m_crystal" + v, 0.26f, t =>
                    (Mathf.Sin(2 * Mathf.PI * fc * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * fc * 1.51f * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * fc * 2.76f * t)) * 0.16f * Mathf.Exp(-t * 18f)
                    + Noise() * 0.25f * Mathf.Exp(-t * 120f));

                float fm = 420f + v * 70f;
                // metal: a short clank
                Make("m_metal" + v, 0.18f, t =>
                    (Sq(fm * t) * 0.12f + Mathf.Sin(2 * Mathf.PI * fm * 1.73f * t) * 0.2f + Mathf.Sin(2 * Mathf.PI * fm * 2.9f * t) * 0.12f) * Mathf.Exp(-t * 22f)
                    + Noise() * 0.3f * Mathf.Exp(-t * 90f));

                lp = 0; float fw = 180f + v * 35f;
                // wood: a hollow knock
                Make("m_wood" + v, 0.1f, t =>
                {
                    lp += (Noise() - lp) * 0.3f;
                    return Mathf.Sin(2 * Mathf.PI * fw * t) * 0.5f * Mathf.Exp(-t * 40f) + lp * 0.6f * Mathf.Exp(-t * 70f);
                });

                ph = 0; float fv = 230f + v * 70f, rm = 333f + v * 57f;
                // void: an alien warble, ring-modulated and bending, like the stone is answering back
                Make("m_void" + v, 0.36f, t =>
                {
                    float f = fv * (1f + 0.5f * Mathf.Sin(2 * Mathf.PI * (6f + v) * t)) * (1f + t * (v == 1 ? -1.2f : 1.6f));
                    ph += f / SR;
                    float carrier = Sin(ph) * Mathf.Sin(2 * Mathf.PI * rm * t);
                    float shimmer = Mathf.Sin(2 * Mathf.PI * (fv * 3.03f) * t) * 0.3f * Mathf.Sin(2 * Mathf.PI * 11f * t);
                    return (carrier * 0.38f + shimmer * 0.15f) * Mathf.Min(1f, t * 120f) * Mathf.Exp(-t * 7f);
                });
            }
        }

        float Noise() => (float)(r.NextDouble() * 2.0 - 1.0);
        static float Sq(float ph) => (ph - Mathf.Floor(ph)) < 0.5f ? 1f : -1f;
        static float Saw(float ph) => 2f * (ph - Mathf.Floor(ph)) - 1f;
        static float Tri(float ph) { float f = ph - Mathf.Floor(ph); return 1f - 4f * Mathf.Abs(f - 0.5f); }
        static float Sin(float ph) => Mathf.Sin(ph * 2f * Mathf.PI);

        void Make(string name, float len, System.Func<float, float> f)
        {
            int n = Mathf.Max(1, (int)(len * SR));
            var d = new float[n];
            int fade = Mathf.Min(n, SR / 200);
            for (int i = 0; i < n; i++)
            {
                float v = f(i / (float)SR);
                if (i > n - fade) v *= (n - i) / (float)fade;
                d[i] = v;
            }
            // Super Crate Box clarity: every clip is normalised so it hits hard and clean. Block-material
            // ticks stay deliberately quiet (they're background texture), everything else peaks at ~0.9.
            float peak = 0f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(d[i]));
            float gain = name.StartsWith("m_") ? 1f : Mathf.Min(4f, 0.9f / Mathf.Max(0.0001f, peak));
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(d[i] * gain, -1f, 1f);
            var c = AudioClip.Create(name, n, 1, SR, false);
            c.SetData(d, 0);
            clips[name] = c;
        }

        void Notes(string name, float[] notes, float each, float vol, bool tri)
        {
            float ph = 0;
            Make(name, notes.Length * each, t =>
            {
                int k = Mathf.Min(notes.Length - 1, (int)(t / each));
                float lt = t - k * each;
                ph += notes[k] / SR;
                float w = tri ? Tri(ph) : Sq(ph) * 0.6f;
                return w * vol * Mathf.Exp(-lt * 10f);
            });
        }

        void Build()
        {
            float ph = 0, lp = 0, lp2 = 0;

            ph = 0; lp = 0;
            Make("dig", 0.14f, t =>
            {
                float f = Mathf.Lerp(190f, 55f, t / 0.14f);
                ph += f / SR;
                lp += (Noise() - lp) * 0.3f;
                return Sin(ph) * Mathf.Exp(-t * 26f) * 0.9f + lp * Mathf.Exp(-t * 38f) * 1.3f;
            });

            lp = 0; ph = 0;
            Make("chip", 0.1f, t =>
            {
                float n = Noise();
                lp += (n - lp) * 0.2f;
                ph += 640f / SR;
                return ((n - lp) * 0.6f + Sq(ph) * 0.18f) * Mathf.Exp(-t * 40f) + Sin(ph * 0.25f) * 0.4f * Mathf.Exp(-t * 30f);
            });

            Make("clank", 0.28f, t =>
                (Mathf.Sin(2 * Mathf.PI * 1250 * t) + 0.6f * Mathf.Sin(2 * Mathf.PI * 1870 * t) + 0.4f * Mathf.Sin(2 * Mathf.PI * 3200 * t)) * 0.3f * Mathf.Exp(-t * 16f)
                + Noise() * 0.4f * Mathf.Exp(-t * 80f));

            lp = 0; lp2 = 0;
            Make("swing", 0.07f, t =>
            {
                float n = Noise();
                lp += (n - lp) * 0.5f;
                lp2 += (lp - lp2) * 0.12f;
                return (lp - lp2) * Mathf.Sin(Mathf.PI * t / 0.07f) * 0.9f;
            });

            ph = 0;
            // a short, bright square-wave "pip" with a noise click on top: you always hear that you hit
            Make("hit", 0.09f, t =>
            {
                ph += Mathf.Lerp(950f, 260f, t / 0.09f) / SR;
                return Sq(ph) * 0.45f * Mathf.Exp(-t * 26f) + Noise() * 0.45f * Mathf.Exp(-t * 60f);
            });

            // fire catching: a soft rising whoosh
            {
                float l1 = 0f;
                Make("ignite", 0.35f, t =>
                {
                    float n = Noise();
                    l1 += (n - l1) * Mathf.Lerp(0.05f, 0.25f, t / 0.35f);
                    return l1 * 1.4f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.35f));
                });
            }
            // a revive: a bright, swelling choir chord with a shimmer on top
            float[] holyF = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f };
            Make("holy", 1.8f, t =>
            {
                float env = Mathf.Min(1f, t / 0.25f) * Mathf.Exp(-Mathf.Max(0f, t - 0.6f) * 2.2f);
                float vib = 1f + Mathf.Sin(t * 30f) * 0.004f;
                float v = 0f;
                for (int i = 0; i < holyF.Length; i++) v += Mathf.Sin(2f * Mathf.PI * holyF[i] * vib * t) * (i < 3 ? 0.22f : 0.12f) + Mathf.Sin(2f * Mathf.PI * holyF[i] * 2.001f * t) * 0.04f;
                v += Mathf.Sin(2f * Mathf.PI * 2093f * t) * 0.05f * Mathf.Abs(Mathf.Sin(t * 13f));   // sparkle
                return v * env;
            });
            // electricity: a buzzing crackle, sharp clicks over a mains hum
            {
                float lz = 0f;
                Make("shock", 0.45f, t =>
                {
                    float n = Noise();
                    lz += (n - lz) * 0.8f;
                    float crackle = Random.value < 0.08f ? Noise() * 1.2f : (n - lz) * 0.35f;
                    float hum = (Mathf.Repeat(t * 120f, 1f) < 0.5f ? 0.25f : -0.25f) * (0.6f + 0.4f * Mathf.Sin(t * 70f));
                    return (crackle + hum) * Mathf.Min(1f, t * 80f) * Mathf.Exp(-t * 5f);
                });
            }
            // water on fire: a sharp hiss that fades out
            {
                float l2 = 0f;
                Make("fizz", 0.5f, t =>
                {
                    float n = Noise();
                    l2 += (n - l2) * 0.6f;
                    return (n - l2) * 0.9f * Mathf.Exp(-t * 6f) * Mathf.Min(1f, t * 60f);
                });
            }

            ph = 0;
            // the special is ready again: a clear rising two-note chime
            Make("ready", 0.22f, t =>
            {
                ph += (t < 0.07f ? 1047f : 1568f) / SR;
                float env = t < 0.07f ? 1f : Mathf.Exp(-(t - 0.07f) * 12f);
                return (Sq(ph) * 0.3f + Mathf.Sin(2f * Mathf.PI * ph * 2f) * 0.2f) * env;
            });

            ph = 0;
            Make("kill", 0.28f, t =>
            {
                ph += Mathf.Lerp(880f, 70f, Mathf.Sqrt(t / 0.28f)) / SR;
                return Sq(ph) * 0.4f * (1f - t / 0.28f) + Noise() * 0.5f * Mathf.Exp(-t * 18f);
            });

            ph = 0;
            Make("coin", 0.2f, t =>
            {
                ph += (t < 0.05f ? 988f : 1319f) / SR;
                return Sq(ph) * 0.2f * (t < 0.05f ? 1f : Mathf.Exp(-(t - 0.05f) * 14f));
            });

            ph = 0;
            Make("jump", 0.11f, t =>
            {
                ph += Mathf.Lerp(260f, 560f, t / 0.11f) / SR;
                return Sq(ph) * 0.14f * (1f - t / 0.11f);
            });

            ph = 0; lp = 0;
            Make("land", 0.1f, t =>
            {
                ph += Mathf.Lerp(100f, 40f, t / 0.1f) / SR;
                lp += (Noise() - lp) * 0.15f;
                return Sin(ph) * Mathf.Exp(-t * 30f) * 0.8f + lp * Mathf.Exp(-t * 40f) * 0.8f;
            });

            ph = 0;
            Make("hurt", 0.32f, t =>
            {
                ph += Mathf.Lerp(420f, 110f, t / 0.32f) / SR;
                return (Saw(ph) * 0.4f + Noise() * 0.25f) * Mathf.Exp(-t * 6f);
            });

            ph = 0; lp = 0;
            Make("explode", 0.85f, t =>
            {
                float a = Mathf.Lerp(0.5f, 0.02f, t / 0.85f);
                lp += (Noise() - lp) * a;
                ph += Mathf.Lerp(70f, 34f, t / 0.85f) / SR;
                return (lp * 1.5f + Sin(ph) * 1.1f) * Mathf.Exp(-t * 4.2f);
            });

            lp = 0;
            Make("break", 0.26f, t =>
            {
                lp += (Noise() - lp) * 0.3f;
                return lp * 1.4f * Mathf.Exp(-t * 12f);
            });

            Notes("pickup", new[] { 523f, 659f, 784f, 1047f }, 0.06f, 0.28f, false);
            Notes("buy", new[] { 784f, 988f, 1319f, 1568f }, 0.055f, 0.28f, false);
            Notes("chest", new[] { 392f, 523f, 659f, 784f, 1047f }, 0.06f, 0.45f, true);
            Notes("heal", new[] { 523f, 659f, 784f }, 0.09f, 0.45f, true);

            ph = 0;
            Make("deny", 0.2f, t =>
            {
                ph += 150f / SR;
                return Sq(ph) * 0.25f * ((t % 0.1f) < 0.06f ? 1f : 0f);
            });

            ph = 0;
            Make("shoot", 0.13f, t =>
            {
                ph += Mathf.Lerp(1200f, 300f, t / 0.13f) / SR;
                return Noise() * 0.4f * Mathf.Exp(-t * 40f) + Sq(ph) * 0.18f * Mathf.Exp(-t * 15f);
            });

            ph = 0;
            Make("special", 0.18f, t =>
            {
                ph += Mathf.Lerp(200f, 900f, t / 0.18f) / SR;
                return Sq(ph) * 0.2f * (1f - t / 0.18f);
            });

            lp = 0;
            Make("dash", 0.26f, t =>
            {
                float a = Mathf.Lerp(0.05f, 0.6f, t / 0.26f);
                lp += (Noise() - lp) * a;
                return lp * 0.9f * Mathf.Sin(Mathf.PI * t / 0.26f);
            });

            lp = 0;
            Make("fuse", 0.35f, t =>
            {
                float n = Noise();
                lp += (n - lp) * 0.3f;
                return (n - lp) * 0.35f * (0.7f + 0.3f * Mathf.Sin(t * 60f));
            });

            lp = 0;
            Make("rumble", 0.55f, t =>
            {
                lp += (Noise() - lp) * 0.04f;
                return lp * 3f * Mathf.Sin(Mathf.PI * t / 0.55f);
            });

            ph = 0;
            Make("grapple", 0.12f, t =>
            {
                ph += Mathf.Lerp(900f, 1500f, t / 0.12f) / SR;
                return Sq(ph) * 0.15f * Mathf.Exp(-t * 18f);
            });

            Make("charge", 0.12f, t => (Mathf.Sin(2 * Mathf.PI * 880 * t) + Mathf.Sin(2 * Mathf.PI * 1320 * t)) * 0.15f * Mathf.Exp(-t * 20f));

            ph = 0;
            Make("death", 0.9f, t =>
            {
                ph += Mathf.Lerp(400f, 40f, t / 0.9f) / SR;
                return (Saw(ph) * 0.35f + Noise() * 0.15f) * (1f - t / 0.9f);
            });

            ph = 0;
            Make("blip", 0.035f, t =>
            {
                ph += 520f / SR;
                return Sq(ph) * 0.35f * (1f - t / 0.035f);
            });

            BuildGuns();
            BuildMaterials();
            BuildReloads();

            ph = 0;
            Make("bat", 0.09f, t =>
            {
                ph += (1800f + 400f * Mathf.Sin(t * 200f)) / SR;
                return Sq(ph) * 0.12f * Mathf.Exp(-t * 25f);
            });
        }

        // ---------------------------------------------------------------- weapons: every one sounds different
        void BuildGuns()
        {
            float ph = 0, ph2 = 0, lp = 0, lp2 = 0;

            // a gunshot = a noise crack + a pitched snap + a low thump
            void Shot(string name, float len, float crack, float snapHi, float snapLo, float thumpHz, float thump, float decay)
            {
                float p1 = 0, p2 = 0, l1 = 0;
                Make(name, len, t =>
                {
                    float n = Noise();
                    l1 += (n - l1) * 0.35f;
                    p1 += Mathf.Lerp(snapHi, snapLo, Mathf.Clamp01(t / (len * 0.6f))) / SR;
                    p2 += Mathf.Lerp(thumpHz * 1.6f, thumpHz, Mathf.Clamp01(t / len)) / SR;
                    return n * crack * Mathf.Exp(-t * 70f)
                         + l1 * 0.9f * Mathf.Exp(-t * decay)
                         + Sq(p1) * 0.16f * Mathf.Exp(-t * 30f)
                         + Sin(p2) * thump * Mathf.Exp(-t * 18f);
                });
            }
            Shot("pistol", 0.2f, 0.7f, 1100f, 260f, 110f, 0.7f, 22f);
            Shot("smg", 0.11f, 0.55f, 1500f, 420f, 140f, 0.45f, 38f);
            Shot("rifle", 0.16f, 0.7f, 1300f, 320f, 120f, 0.6f, 28f);
            Shot("eshot", 0.15f, 0.4f, 700f, 200f, 90f, 0.5f, 26f);

            lp = 0; ph = 0;
            Make("shotgun", 0.42f, t =>
            {
                float n = Noise();
                lp += (n - lp) * Mathf.Lerp(0.6f, 0.08f, t / 0.42f);
                ph += Mathf.Lerp(120f, 45f, t / 0.42f) / SR;
                float pump = t > 0.28f && t < 0.33f ? Sq(t * 1800f) * 0.15f : 0f;
                return n * 0.8f * Mathf.Exp(-t * 60f) + lp * 1.5f * Mathf.Exp(-t * 9f) + Sin(ph) * 0.9f * Mathf.Exp(-t * 10f) + pump;
            });

            lp = 0; ph = 0;
            Make("sniper", 0.7f, t =>
            {
                float n = Noise();
                lp += (n - lp) * Mathf.Lerp(0.5f, 0.04f, t / 0.7f);
                ph += Mathf.Lerp(90f, 38f, t / 0.7f) / SR;
                return n * 1f * Mathf.Exp(-t * 90f) + lp * 1.3f * Mathf.Exp(-t * 5f) + Sin(ph) * 1f * Mathf.Exp(-t * 7f);
            });

            ph = 0; ph2 = 0;
            Make("laser", 0.17f, t =>
            {
                ph += Mathf.Lerp(2200f, 500f, t / 0.17f) / SR;
                ph2 += Mathf.Lerp(1100f, 250f, t / 0.17f) / SR;
                return (Sq(ph) * 0.18f + Sin(ph2) * 0.3f) * Mathf.Exp(-t * 14f);
            });

            ph = 0; ph2 = 0;
            Make("plasma", 0.34f, t =>
            {
                ph += Mathf.Lerp(420f, 90f, t / 0.34f) / SR;
                ph2 += Mathf.Lerp(437f, 95f, t / 0.34f) / SR;
                return (Sin(ph) + Sin(ph2) + Saw(ph * 0.5f) * 0.3f) * 0.32f * Mathf.Exp(-t * 7f) + Noise() * 0.2f * Mathf.Exp(-t * 40f);
            });

            lp = 0; ph = 0;
            Make("launcher", 0.28f, t =>
            {
                lp += (Noise() - lp) * 0.15f;
                ph += Mathf.Lerp(160f, 55f, t / 0.28f) / SR;
                return lp * 1.4f * Mathf.Exp(-t * 14f) + Sin(ph) * 0.8f * Mathf.Exp(-t * 12f);
            });

            lp = 0; lp2 = 0;
            Make("rocket", 0.5f, t =>
            {
                float n = Noise();
                lp += (n - lp) * Mathf.Lerp(0.05f, 0.4f, t / 0.5f);
                lp2 += (n - lp2) * 0.3f;
                return lp * 1.4f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t / 0.5f)) + lp2 * 0.8f * Mathf.Exp(-t * 30f);
            });

            lp = 0; lp2 = 0;
            Make("flame", 0.12f, t =>
            {
                float n = Noise();
                lp += (n - lp) * 0.25f;
                lp2 += (lp - lp2) * 0.1f;
                return (lp - lp2) * 1.4f * Mathf.Sin(Mathf.PI * t / 0.12f);
            });

            Make("disc", 0.25f, t =>
                (Mathf.Sin(2 * Mathf.PI * 2400 * t) + 0.7f * Mathf.Sin(2 * Mathf.PI * 3170 * t) + 0.4f * Mathf.Sin(2 * Mathf.PI * 4410 * t)) * 0.18f * Mathf.Exp(-t * 14f)
                + Noise() * 0.4f * Mathf.Exp(-t * 60f));

            ph = 0; lp = 0;
            Make("rail", 0.55f, t =>
            {
                ph += Mathf.Lerp(3000f, 120f, Mathf.Clamp01(t / 0.25f)) / SR;
                lp += (Noise() - lp) * 0.2f;
                return Sq(ph) * 0.25f * Mathf.Exp(-t * 8f) + Noise() * 0.9f * Mathf.Exp(-t * 50f) + lp * 1.1f * Mathf.Exp(-t * 6f);
            });

            ph = 0; lp = 0;
            Make("bolt", 0.24f, t =>
            {
                ph += 196f / SR;
                lp += (Noise() - lp) * 0.4f;
                return Saw(ph) * 0.35f * Mathf.Exp(-t * 16f) + lp * 0.8f * Mathf.Exp(-t * 40f);
            });

            lp = 0; lp2 = 0;
            Make("sword", 0.16f, t =>
            {
                float n = Noise();
                lp += (n - lp) * Mathf.Lerp(0.1f, 0.6f, t / 0.16f);
                lp2 += (lp - lp2) * 0.15f;
                return (lp - lp2) * 1.3f * Mathf.Sin(Mathf.PI * t / 0.16f) + Mathf.Sin(2 * Mathf.PI * 2600 * t) * 0.08f * Mathf.Exp(-t * 20f);
            });

            lp = 0; lp2 = 0;
            Make("hammer", 0.24f, t =>
            {
                float n = Noise();
                lp += (n - lp) * Mathf.Lerp(0.03f, 0.25f, t / 0.24f);
                lp2 += (lp - lp2) * 0.08f;
                return (lp - lp2) * 2f * Mathf.Sin(Mathf.PI * t / 0.24f);
            });

            ph = 0; lp = 0;
            Make("esword", 0.2f, t =>
            {
                ph += (180f + 60f * Mathf.Sin(t * 90f)) / SR;
                lp += (Noise() - lp) * 0.5f;
                return (Saw(ph) * 0.25f + lp * 0.5f) * Mathf.Sin(Mathf.PI * t / 0.2f);
            });

            Make("reflect", 0.16f, t => (Mathf.Sin(2 * Mathf.PI * 1600 * t) + Mathf.Sin(2 * Mathf.PI * 2400 * t)) * 0.22f * Mathf.Exp(-t * 22f));
            Make("ricochet", 0.2f, t =>
            {
                float f = Mathf.Lerp(3200f, 1800f, t / 0.2f);
                return Mathf.Sin(2 * Mathf.PI * f * t) * 0.18f * Mathf.Exp(-t * 18f) + Noise() * 0.2f * Mathf.Exp(-t * 80f);
            });

            Make("reload", 0.16f, t =>
                (t < 0.03f || (t > 0.09f && t < 0.12f) ? Noise() * 0.5f + Sq(t * 2600f) * 0.2f : 0f) * Mathf.Exp(-t * 6f));

            lp = 0;
            Make("dashq", 0.14f, t =>
            {
                float a = Mathf.Lerp(0.6f, 0.05f, t / 0.14f);
                lp += (Noise() - lp) * a;
                return lp * 1.1f * Mathf.Sin(Mathf.PI * t / 0.14f);
            });

            lp = 0; ph = 0;
            Make("splat", 0.2f, t =>
            {
                lp += (Noise() - lp) * 0.12f;
                ph += Mathf.Lerp(300f, 80f, t / 0.2f) / SR;
                return lp * 1.6f * Mathf.Exp(-t * 18f) + Sin(ph) * 0.3f * Mathf.Exp(-t * 25f);
            });

            // the bass that makes explosions physical
            ph = 0; lp = 0;
            Make("boom", 1.1f, t =>
            {
                ph += Mathf.Lerp(62f, 24f, t / 1.1f) / SR;
                lp += (Noise() - lp) * 0.02f;
                return (Sin(ph) * 1.2f + lp * 2.5f) * Mathf.Exp(-t * 3.2f) * Mathf.Min(1f, t * 200f);
            });

            Make("freeze", 0.35f, t =>
                (Mathf.Sin(2 * Mathf.PI * 2900 * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * 4100 * t) * 0.35f) * 0.25f * Mathf.Exp(-t * 9f) * (0.6f + 0.4f * Mathf.Sin(t * 260f))
                + Noise() * 0.3f * Mathf.Exp(-t * 30f));

            ph = 0;
            Make("zap", 0.14f, t =>
            {
                ph += (1400f + 900f * Noise()) / SR;
                return Sq(ph) * 0.22f * Mathf.Exp(-t * 20f) + Noise() * 0.25f * Mathf.Exp(-t * 40f);
            });

            ph = 0;
            Make("shield", 0.3f, t =>
            {
                ph += (900f + 500f * Mathf.Sin(t * 40f)) / SR;
                return Sin(ph) * 0.2f * Mathf.Sin(Mathf.PI * t / 0.3f);
            });
        }
    }
}
