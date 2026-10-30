using UnityEngine;

namespace DigDeep
{
    // Music player. Clips live in Assets/DigDeep/Resources/DigDeepMusic/.
    //  - menu.wav       : loops as a whole (24 bars @ 170 BPM)
    //  - boss_intro.wav : 8-bar intro, played once
    //  - boss_loop.wav  : bars 8-55, scheduled sample-accurately right after the intro, then loops forever
    //  - menu_base / menu_chars / menu_options / menu_stats .wav : the title music in layers (Mario Kart style). They all
    //    play together, in sync, from the start; each menu screen just fades layers in and out: the base alone on the
    //    title, base + characters on the digger select, base + options in the options, and the stats mix (a whole
    //    track of its own) on stats, unlocks and credits.
    //  - void_run.wav : the level music down in the void world
    // Missing clips are simply skipped, so the game still runs without them.
    public class Music
    {
        public enum Track { None, Menu, Boss, Intro, Level }
        public Track Current { get; private set; }
        public float volume = 0.6f;      // overall music level vs. the procedural SFX
        float duck = 1f;                 // lowered while paused

        readonly AudioSource a, b;       // a: intro / menu, b: boss loop
        readonly AudioClip menu, bossIntro, bossLoop, introHead, introLoop, levelHead, levelLoop, voidRun;
        // the layered title music
        public enum MenuMix { Title, Characters, Options, Stats }
        MenuMix mix;
        readonly AudioClip[] layerClips = new AudioClip[4];      // base, characters, options, stats
        readonly AudioSource[] layers = new AudioSource[4];
        readonly float[] layerVol = new float[4];
        bool layered, levelVoid;
        float fade = 1f, fadeTarget = 1f, fadeSpeed = 1f;
        Track pending = Track.None;

        public Music(GameObject host)
        {
            a = host.AddComponent<AudioSource>();
            b = host.AddComponent<AudioSource>();
            foreach (var s in new[] { a, b })
            {
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.priority = 0;       // never let SFX steal the music voice
                s.volume = 0f;
            }
            menu = Load("menu");
            bossIntro = Load("boss_intro");
            bossLoop = Load("boss_loop");
            introHead = Load("intro_head");
            introLoop = Load("intro_loop");
            levelHead = Load("level_head");
            levelLoop = Load("level_loop");
            voidRun = Load("void_run");
            string[] ln = { "menu_base", "menu_chars", "menu_options", "menu_stats" };
            for (int i = 0; i < 4; i++)
            {
                layerClips[i] = Load(ln[i]);
                layers[i] = host.AddComponent<AudioSource>();
                layers[i].playOnAwake = false; layers[i].spatialBlend = 0f; layers[i].priority = 0; layers[i].volume = 0f; layers[i].loop = true;
            }
        }

        // which menu screen we're on: the layers fade to match
        public void SetMenu(MenuMix m) => mix = m;

        float LayerTarget(int i)
        {
            switch (mix)
            {
                case MenuMix.Characters: return i == 0 || i == 1 ? 1f : 0f;
                case MenuMix.Options: return i == 0 || i == 2 ? 1f : 0f;
                case MenuMix.Stats: return i == 3 ? 1f : 0f;
                default: return i == 0 ? 1f : 0f;
            }
        }

        static AudioClip Load(string name)
        {
            var c = Resources.Load<AudioClip>("DigDeepMusic/" + name);
            if (c == null) Debug.LogWarning("DigDeep: music clip DigDeepMusic/" + name + " not found");
            else if (c.loadState != AudioDataLoadState.Loaded) c.LoadAudioData();
            return c;
        }

        public void PlayMenu(float fadeIn = 1.5f) => Switch(Track.Menu, fadeIn);
        public void PlayBoss(float fadeIn = 0.4f) => Switch(Track.Boss, fadeIn);
        public void PlayIntro(float fadeIn = 1.5f) => Switch(Track.Intro, fadeIn);
        public void PlayLevel(float fadeIn = 2f) => Switch(Track.Level, fadeIn);

        public void Stop(float fadeOut = 1.5f)
        {
            pending = Track.None;
            fadeTarget = 0f;
            fadeSpeed = 1f / Mathf.Max(0.01f, fadeOut);
        }

        public void SetDuck(bool on) => duck = on ? 0.45f : 1f;

        // Fade the current track out quickly, then start the new one.
        void Switch(Track t, float fadeIn)
        {
            if (Current == t && fadeTarget > 0f && !(t == Track.Level && levelVoid != World.Void)) return;   // (into the void: its own music)
            if (Current == Track.None || !(a.isPlaying || b.isPlaying || layered)) { Start(t, fadeIn); return; }
            pending = t;
            pendingFade = fadeIn;
            fadeTarget = 0f;
            fadeSpeed = 1f / 0.35f;
        }

        float pendingFade;

        void Start(Track t, float fadeIn)
        {
            a.Stop(); b.Stop();
            StopLayers();
            Current = t;
            levelVoid = World.Void;
            fade = 0f; fadeTarget = 1f; fadeSpeed = 1f / Mathf.Max(0.01f, fadeIn);
            if (t == Track.Menu && layerClips[0] != null)
            {
                // every layer starts on the very same sample, looping forever; only their volumes change
                double t0 = AudioSettings.dspTime + 0.1;
                for (int i = 0; i < 4; i++)
                {
                    layerVol[i] = LayerTarget(i);
                    if (layerClips[i] == null) continue;
                    layers[i].clip = layerClips[i];
                    layers[i].PlayScheduled(t0);
                }
                layered = true;
            }
            else if (t == Track.Menu && menu != null)
            {
                a.clip = menu; a.loop = true;
                a.Play();
            }
            else if (t == Track.Level && World.Void && voidRun != null)
            {
                b.clip = voidRun; b.loop = true;   // the void world has its own music
                b.Play();
            }
            else if (t == Track.Boss && bossLoop != null) PlayPair(bossIntro, bossLoop);
            else if (t == Track.Intro && introLoop != null) PlayPair(introHead, introLoop);
            else if (t == Track.Level && levelLoop != null) PlayPair(levelHead, levelLoop);
            else Current = Track.None;
        }

        // gapless: schedule the loop to start on the exact sample the head ends
        void PlayPair(AudioClip head, AudioClip loop)
        {
            if (head != null)
            {
                double t0 = AudioSettings.dspTime + 0.1;
                a.clip = head; a.loop = false;
                a.PlayScheduled(t0);
                b.clip = loop; b.loop = true;
                b.PlayScheduled(t0 + (double)head.samples / head.frequency);
            }
            else
            {
                b.clip = loop; b.loop = true;
                b.Play();
            }
        }

        public void Tick(float dt)
        {
            fade = Mathf.MoveTowards(fade, fadeTarget, fadeSpeed * dt);
            if (fade <= 0f && fadeTarget <= 0f)
            {
                if (a.isPlaying || b.isPlaying) { a.Stop(); b.Stop(); }
                StopLayers();
                var next = pending;
                pending = Track.None;
                if (next != Track.None) Start(next, pendingFade);
                else Current = Track.None;
            }
            float v = volume * duck * fade * fade;   // squared fade = smoother perceived fade
            a.volume = v;
            b.volume = v;
            if (layered)
                for (int i = 0; i < 4; i++)
                {
                    layerVol[i] = Mathf.MoveTowards(layerVol[i], LayerTarget(i), dt * 1.2f);   // instruments swell in over ~a bar
                    layers[i].volume = v * layerVol[i];
                }
        }

        void StopLayers()
        {
            if (!layered) return;
            layered = false;
            for (int i = 0; i < 4; i++) { layers[i].Stop(); layers[i].volume = 0f; }
        }
    }
}
