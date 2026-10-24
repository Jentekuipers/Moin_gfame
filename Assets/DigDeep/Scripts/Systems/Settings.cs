using UnityEngine;

namespace DigDeep
{
    // Every player-facing setting, saved in PlayerPrefs and applied straight away.
    public static class Settings
    {
        // audio
        public static float Master = 1f, Music = 0.8f, Sfx = 1f;
        // video
        public static int DisplayMode;        // 0 borderless fullscreen, 1 exclusive fullscreen, 2 windowed
        public static int ResIndex = -1;      // index into Resolutions (-1: the monitor's own)
        public static bool VSync = true;
        public static int FpsCap = 2;         // index into FpsCaps
        public static readonly int[] FpsCaps = { 30, 60, 120, 144, 240, -1 };
        // gameplay & accessibility
        public static float Shake = 1f;       // screen shake, kicks and zoom punches (0..1.5)
        public static bool Flashes = true;    // full-screen white and red flashes
        public static bool HitStop = true;    // the tiny freeze frames on big hits
        public static float Brightness;       // 0..1: lifts the darkness
        public static float UiScale = 1f;     // 0.75..1.5
        public static bool PauseOnFocusLoss = true;
        public static bool ShowFps;
        public static bool AutoPickup;        // walk over trinkets and items to take them (off: press interact)

        public static readonly string[] DisplayNames = { "BORDERLESS", "FULLSCREEN", "WINDOWED" };

        static Resolution[] res;
        public static Resolution[] Resolutions
        {
            get
            {
                if (res == null)
                {
                    // one entry per size (the highest refresh rate of each)
                    var list = new System.Collections.Generic.List<Resolution>();
                    foreach (var r in Screen.resolutions)
                    {
                        int i = list.FindIndex(x => x.width == r.width && x.height == r.height);
                        if (i < 0) list.Add(r);
                    }
                    if (list.Count == 0) list.Add(Screen.currentResolution);
                    res = list.ToArray();
                }
                return res;
            }
        }

        public static string ResName(int i)
        {
            if (i < 0 || i >= Resolutions.Length) return "NATIVE";
            return Resolutions[i].width + " X " + Resolutions[i].height;
        }

        public static void Load()
        {
            Master = PlayerPrefs.GetFloat("set_master", 1f);
            Music = PlayerPrefs.GetFloat("set_music", 0.8f);
            Sfx = PlayerPrefs.GetFloat("set_sfx", 1f);
            DisplayMode = PlayerPrefs.GetInt("set_display", 0);
            ResIndex = PlayerPrefs.GetInt("set_res", -1);
            VSync = PlayerPrefs.GetInt("set_vsync", 1) == 1;
            FpsCap = PlayerPrefs.GetInt("set_fps", 2);
            Shake = PlayerPrefs.GetFloat("set_shake", 1f);
            Flashes = PlayerPrefs.GetInt("set_flash", 1) == 1;
            HitStop = PlayerPrefs.GetInt("set_hitstop", 1) == 1;
            Brightness = PlayerPrefs.GetFloat("set_bright", 0f);
            UiScale = PlayerPrefs.GetFloat("set_ui", 1f);
            PauseOnFocusLoss = PlayerPrefs.GetInt("set_focus", 1) == 1;
            ShowFps = PlayerPrefs.GetInt("set_fpsshow", 0) == 1;
            AutoPickup = PlayerPrefs.GetInt("set_autopick", 0) == 1;
            GameInput.LoadBindings();
            Apply(true);
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("set_master", Master);
            PlayerPrefs.SetFloat("set_music", Music);
            PlayerPrefs.SetFloat("set_sfx", Sfx);
            PlayerPrefs.SetInt("set_display", DisplayMode);
            PlayerPrefs.SetInt("set_res", ResIndex);
            PlayerPrefs.SetInt("set_vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt("set_fps", FpsCap);
            PlayerPrefs.SetFloat("set_shake", Shake);
            PlayerPrefs.SetInt("set_flash", Flashes ? 1 : 0);
            PlayerPrefs.SetInt("set_hitstop", HitStop ? 1 : 0);
            PlayerPrefs.SetFloat("set_bright", Brightness);
            PlayerPrefs.SetFloat("set_ui", UiScale);
            PlayerPrefs.SetInt("set_focus", PauseOnFocusLoss ? 1 : 0);
            PlayerPrefs.SetInt("set_fpsshow", ShowFps ? 1 : 0);
            PlayerPrefs.SetInt("set_autopick", AutoPickup ? 1 : 0);
            GameInput.SaveBindings();
            PlayerPrefs.Save();
        }

        // Pushes the settings into the engine. Screen changes only when asked (they flicker).
        public static void Apply(bool screen)
        {
            AudioListener.volume = Master;
            var g = Game.I;
            if (g != null)
            {
                if (g.sfx != null) g.sfx.master = 0.85f * Sfx;
                if (g.music != null) g.music.volume = 0.6f * Music;
            }
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            int cap = FpsCaps[Mathf.Clamp(FpsCap, 0, FpsCaps.Length - 1)];
            Application.targetFrameRate = VSync ? -1 : cap;
            if (!screen || Application.isEditor) return;
            var mode = DisplayMode == 0 ? FullScreenMode.FullScreenWindow : DisplayMode == 1 ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
            int w = Display.main.systemWidth, h = Display.main.systemHeight;
            if (ResIndex >= 0 && ResIndex < Resolutions.Length) { w = Resolutions[ResIndex].width; h = Resolutions[ResIndex].height; }
            else if (mode == FullScreenMode.Windowed) { w = Mathf.RoundToInt(w * 0.75f); h = Mathf.RoundToInt(h * 0.75f); }
            Screen.SetResolution(w, h, mode);
        }

        public static void Defaults()
        {
            Master = 1f; Music = 0.8f; Sfx = 1f;
            DisplayMode = 0; ResIndex = -1; VSync = true; FpsCap = 2;
            Shake = 1f; Flashes = true; HitStop = true; Brightness = 0f; UiScale = 1f; PauseOnFocusLoss = true; ShowFps = false; AutoPickup = false;
            GameInput.DefaultBindings();
            GameInput.DefaultPad();
            Apply(true);
            Save();
        }

        // wipes best depth, wins and digger unlocks (settings stay)
        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey("digdeep_best");
            PlayerPrefs.DeleteKey("digdeep_wins");
            foreach (var c in Chars.All) PlayerPrefs.DeleteKey("digdeep_won_" + c.id);
            SaveGame.Delete();
            PlayerPrefs.Save();
            if (Game.I != null) Game.I.best = 0;
        }
    }
}
