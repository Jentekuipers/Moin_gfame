using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // The options screen (from the title screen and the pause menu): audio, video, gameplay & accessibility, and
    // controls with key rebinding. Mouse and keyboard both work: W/S pick a row, A/D change it, Space/click to
    // press or rebind, Q/E (or click) switch tabs, Esc goes back (and saves).
    public class OptionsMenu
    {
        enum Tab { Audio, Video, Gameplay, Controls, Gamepad }
        static readonly string[] TabNames = { "AUDIO", "VIDEO", "GAMEPLAY", "CONTROLS" };
        const int TabCount = 4;   // CONTROLS holds two tabs of its own: keyboard and controller
        bool padSub;
        int VisTab => tab == Tab.Gamepad ? 3 : (int)tab;
        bool InControls => tab == Tab.Controls || tab == Tab.Gamepad;

        void SetSub(bool pad)
        {
            if (padSub == pad && InControls) return;
            padSub = pad;
            tab = pad ? Tab.Gamepad : Tab.Controls; sel = 0; rebinding = Btn.COUNT; padRebinding = Btn.COUNT;
            Game.I.sfx.Play("swing", 0.5f, 1.1f);
        }

        class Row
        {
            public string label, hint;
            public System.Func<string> value;
            public System.Action<int> change;      // A/D or click: -1 / +1
            public System.Action activate;         // Space / click on a button row
            public System.Func<float> get01;       // sliders
            public System.Action<float> set01;
            public Btn bind = Btn.COUNT;           // a controls row
            public Btn padBind = Btn.COUNT;        // a gamepad row
            public bool dim;
        }

        Tab tab;
        int sel, bindSlot;
        Btn rebinding = Btn.COUNT, padRebinding = Btn.COUNT;
        bool armed;                                 // the key that started a rebind must not be the key it binds
        float resetArmT, defaultsArmT;
        Game.State back;
        readonly List<Row> rows = new List<Row>();

        public bool FromPause => back == Game.State.Paused;

        public void Show(Game.State from)
        {
            back = from;
            openFrame = Time.frameCount;   // the click that opened the menu mustn't also press something in it
            sel = 0;
            tab = Tab.Audio; padSub = GameInput.UsingStick;
            rebinding = Btn.COUNT; padRebinding = Btn.COUNT;
            Game.I.state = Game.State.Options;
        }

        void Close()
        {
            Settings.Save();
            Game.I.state = back;
            Game.I.sfx.Play("swing", 0.5f, 1.2f);
        }

        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";
        static string OnOff(bool b) => b ? "ON" : "OFF";

        void Build()
        {
            rows.Clear();
            switch (tab)
            {
                case Tab.Audio:
                    Slider("MASTER VOLUME", () => Settings.Master, v => Settings.Master = v);
                    Slider("MUSIC", () => Settings.Music, v => Settings.Music = v);
                    Slider("SOUND EFFECTS", () => Settings.Sfx, v => Settings.Sfx = v);
                    break;
                case Tab.Video:
                    rows.Add(new Row { label = "DISPLAY MODE", value = () => Settings.DisplayNames[Settings.DisplayMode],
                        change = d => { Settings.DisplayMode = (Settings.DisplayMode + d + 3) % 3; Settings.Apply(true); } });
                    rows.Add(new Row { label = "RESOLUTION", value = () => Settings.ResName(Settings.ResIndex),
                        change = d => { int n = Settings.Resolutions.Length + 1; Settings.ResIndex = ((Settings.ResIndex + 1 + d) % n + n) % n - 1; Settings.Apply(true); } });
                    rows.Add(new Row { label = "VSYNC", value = () => OnOff(Settings.VSync), change = d => { Settings.VSync = !Settings.VSync; Settings.Apply(false); } });
                    rows.Add(new Row { label = "FRAME RATE CAP", dim = Settings.VSync, hint = "ONLY WITHOUT VSYNC",
                        value = () => { int c = Settings.FpsCaps[Settings.FpsCap]; return c < 0 ? "UNLIMITED" : c.ToString(); },
                        change = d => { int n = Settings.FpsCaps.Length; Settings.FpsCap = (Settings.FpsCap + d + n) % n; Settings.Apply(false); } });
                    rows.Add(new Row { label = "SHOW FPS", value = () => OnOff(Settings.ShowFps), change = d => Settings.ShowFps = !Settings.ShowFps });
                    break;
                case Tab.Gameplay:
                    rows.Add(new Row { label = "SCREEN SHAKE", value = () => Pct(Settings.Shake), get01 = () => Settings.Shake / 1.5f, set01 = v => Settings.Shake = Mathf.Round(v * 1.5f * 20f) / 20f,
                        change = d => Settings.Shake = Mathf.Clamp(Settings.Shake + d * 0.1f, 0f, 1.5f) });
                    rows.Add(new Row { label = "SCREEN FLASHES", hint = "WHITE AND RED FULL-SCREEN FLASHES", value = () => OnOff(Settings.Flashes), change = d => Settings.Flashes = !Settings.Flashes });
                    rows.Add(new Row { label = "HIT-STOP", hint = "THE TINY FREEZE ON BIG HITS", value = () => OnOff(Settings.HitStop), change = d => Settings.HitStop = !Settings.HitStop });
                    Slider("BRIGHTNESS", () => Settings.Brightness, v => Settings.Brightness = v);
                    rows.Add(new Row { label = "UI SCALE", value = () => Pct(Settings.UiScale), get01 = () => (Settings.UiScale - 0.75f) / 0.75f, set01 = v => Settings.UiScale = 0.75f + Mathf.Round(v * 15f) / 20f,
                        change = d => Settings.UiScale = Mathf.Clamp(Settings.UiScale + d * 0.05f, 0.75f, 1.5f) });
                    rows.Add(new Row { label = "AUTO PICK-UP", hint = "OFF: PRESS INTERACT TO TAKE TRINKETS AND ITEMS", value = () => OnOff(Settings.AutoPickup), change = d => Settings.AutoPickup = !Settings.AutoPickup });
                    rows.Add(new Row { label = "PAUSE WHEN UNFOCUSED", value = () => OnOff(Settings.PauseOnFocusLoss), change = d => Settings.PauseOnFocusLoss = !Settings.PauseOnFocusLoss });
                    rows.Add(new Row { label = resetArmT > 0f ? "SURE? PRESS AGAIN TO ERASE" : "RESET ALL PROGRESS", hint = "BEST DEPTH, WINS, UNLOCKS AND THE SAVED RUN",
                        value = () => "", activate = () =>
                        {
                            if (resetArmT > 0f) { Settings.ResetProgress(); resetArmT = 0f; Game.I.sfx.Play("death", 0.6f); }
                            else { resetArmT = 3f; Game.I.sfx.Play("deny", 0.6f); }
                        } });
                    break;
                case Tab.Controls:
                    DeviceRow();
                    foreach (var b in GameInput.Rebindable) rows.Add(new Row { label = GameInput.ActionName(b), bind = b });
                    rows.Add(new Row { label = "RESET CONTROLS", value = () => "", activate = () => { GameInput.DefaultBindings(); Game.I.sfx.Play("buy", 0.6f); } });
                    break;
                case Tab.Gamepad:
                    DeviceRow();
                    rows.Add(new Row { label = "LAYOUT", hint = GameInput.Presets[Mathf.Clamp(GameInput.PadPreset, 0, GameInput.Presets.Length - 1)].blurb,
                        value = () => GameInput.MatchesPreset(GameInput.PadPreset) ? GameInput.Presets[GameInput.PadPreset].name : "CUSTOM",
                        change = d =>
                        {
                            int n = GameInput.Presets.Length;
                            GameInput.ApplyPreset(((GameInput.PadPreset + d) % n + n) % n);
                        } });
                    rows.Add(new Row { label = "SWAP STICKS (SOUTHPAW)", hint = "WALK WITH THE RIGHT STICK, AIM WITH THE LEFT", value = () => OnOff(GameInput.SwapSticks), change = d => GameInput.SwapSticks = !GameInput.SwapSticks });
                    rows.Add(new Row { label = "LOOK / AIM", hint = "HOW FAR FROM YOU THE RETICLE SITS (DOUBLED WHILE HOLDING LOOK / AIM)",
                        value = () => GameInput.AimRadius.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                        get01 = () => (GameInput.AimRadius - 2.5f) / 5.5f, set01 = v => GameInput.AimRadius = 2.5f + Mathf.Round(v * 11f) / 2f,
                        change = d => GameInput.AimRadius = Mathf.Clamp(GameInput.AimRadius + d * 0.5f, 2.5f, 8f) });
                    foreach (var b in GameInput.PadRebindable) rows.Add(new Row { label = PadArt.ShortAction(b), padBind = b });
                    rows.Add(new Row { label = "RESET GAMEPAD", value = () => "", activate = () => { GameInput.DefaultPad(); Game.I.sfx.Play("buy", 0.6f); } });
                    break;
            }
            rows.Add(new Row { label = defaultsArmT > 0f ? "SURE? PRESS AGAIN" : "RESET ALL SETTINGS", value = () => "", activate = () =>
            {
                if (defaultsArmT > 0f) { Settings.Defaults(); defaultsArmT = 0f; Game.I.sfx.Play("buy", 0.6f); }
                else { defaultsArmT = 3f; Game.I.sfx.Play("deny", 0.5f); }
            } });
            rows.Add(new Row { label = "BACK", value = () => "", activate = Close });
        }

        void DeviceRow() => rows.Add(new Row { label = "SHOWING", hint = "KEYBOARD OR CONTROLLER BUTTONS (OR CLICK THE TABS ABOVE)",
            value = () => padSub ? "CONTROLLER" : "KEYBOARD", change = d => SetSub(!padSub) });

        void Slider(string label, System.Func<float> get, System.Action<float> set)
        {
            rows.Add(new Row { label = label, value = () => Pct(get()), get01 = get, set01 = v => set(Mathf.Round(v * 20f) / 20f),
                change = d => set(Mathf.Clamp01(Mathf.Round((get() + d * 0.05f) * 20f) / 20f)) });
        }

        // ------------------------------------------------------------------ keyboard / gamepad

        public void Tick(float dt)
        {
            resetArmT -= dt; defaultsArmT -= dt;
            Build();
            var sfx = Game.I.sfx;
            if (padRebinding != Btn.COUNT)
            {
                if (!armed) { armed = true; return; }
                var pp = GameInput.PadPressedThisFrame();
                if (GameInput.KeyPressedThisFrame() == "Escape" || pp == Pad.Start) { padRebinding = Btn.COUNT; sfx.Play("deny", 0.4f); return; }
                if (pp == Pad.None) return;
                GameInput.BindPad(padRebinding, bindSlot, pp == Pad.View ? Pad.None : pp);   // VIEW clears the slot
                padRebinding = Btn.COUNT;
                sfx.Play("buy", 0.6f, 1.3f);
                Settings.Save();
                return;
            }
            if (rebinding != Btn.COUNT)
            {
                if (!armed) { armed = true; return; }
                string k = GameInput.KeyPressedThisFrame();
                if (k == null) return;
                if (k == "Escape") { rebinding = Btn.COUNT; sfx.Play("deny", 0.4f); return; }
                if (k == "Backspace" || k == "Delete") GameInput.binds[(int)rebinding, bindSlot] = "";
                else
                {
                    // a key does one thing: take it away from wherever else it was bound
                    foreach (var o in GameInput.Rebindable)
                        for (int s = 0; s < 2; s++)
                            if (GameInput.binds[(int)o, s] == k) GameInput.binds[(int)o, s] = "";
                    GameInput.binds[(int)rebinding, bindSlot] = k;
                }
                rebinding = Btn.COUNT;
                sfx.Play("buy", 0.6f, 1.3f);
                Settings.Save();
                return;
            }
            if (GameInput.Pressed(Btn.Pause)) { Close(); return; }
            if (GameInput.Pressed(Btn.Swap) || GameInput.PadPressed(Pad.LB)) SetTab((VisTab + TabCount - 1) % TabCount);
            if (GameInput.Pressed(Btn.Interact) || GameInput.PadPressed(Pad.RB)) SetTab((VisTab + 1) % TabCount);
            if (GameInput.Pressed(Btn.Up)) { sel = (sel + rows.Count - 1) % rows.Count; sfx.Play("swing", 0.4f, 1.4f); }
            if (GameInput.Pressed(Btn.Down)) { sel = (sel + 1) % rows.Count; sfx.Play("swing", 0.4f, 1.4f); }
            sel = Mathf.Clamp(sel, 0, rows.Count - 1);
            var r = rows[sel];
            int dir = GameInput.Pressed(Btn.Left) ? -1 : GameInput.Pressed(Btn.Right) ? 1 : 0;
            if (r.bind != Btn.COUNT)
            {
                if (dir != 0) { bindSlot = dir < 0 ? 0 : 1; sfx.Play("swing", 0.3f, 1.6f); }
                if (GameInput.Pressed(Btn.Jump)) StartRebind(r.bind, bindSlot);
                return;
            }
            if (r.padBind != Btn.COUNT)
            {
                if (dir != 0) { bindSlot = Mathf.Clamp(bindSlot + dir, 0, 2); sfx.Play("swing", 0.3f, 1.6f); }
                if (GameInput.Pressed(Btn.Jump)) StartPadRebind(r.padBind, bindSlot);
                return;
            }
            if (dir != 0 && r.change != null) { r.change(dir); Settings.Apply(false); sfx.Play("blip", 0.4f, 1.2f); }
            if (GameInput.Pressed(Btn.Jump))
            {
                if (r.activate != null) r.activate();
                else if (r.change != null) { r.change(1); Settings.Apply(false); sfx.Play("blip", 0.4f, 1.2f); }
            }
        }

        void SetTab(int t)
        {
            if (VisTab == t) return;
            tab = t == 3 ? (padSub ? Tab.Gamepad : Tab.Controls) : (Tab)t; sel = 0; rebinding = Btn.COUNT; padRebinding = Btn.COUNT;
            Game.I.sfx.Play("swing", 0.5f, 1.1f);
        }

        void StartPadRebind(Btn b, int slot)
        {
            padRebinding = b; bindSlot = slot; armed = false;
            Game.I.sfx.Play("blip", 0.5f, 0.9f);
        }

        void StartRebind(Btn b, int slot)
        {
            rebinding = b; bindSlot = slot; armed = false;
            Game.I.sfx.Play("blip", 0.5f, 0.9f);
        }

        // ------------------------------------------------------------------ drawing (and the mouse)

        static void Box(float x, float y, float w, float h, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), Art.White);
            GUI.color = old;
        }

        public void Draw()
        {
            if (rows.Count == 0) Build();
            float W = Screen.width, H = Screen.height;
            float S = Mathf.Max(2f, Mathf.Floor(H / 270f * Settings.UiScale));
            float sm = Mathf.Max(1f, S - 1f);
            var mouse = Event.current.mousePosition;
            bool fresh = Time.frameCount == openFrame;
            bool click = !fresh && GameInput.MouseClicked(), held = !fresh && GameInput.MouseHeld();
            var gold = new Color(1f, 0.82f, 0.3f);

            bool padTab = tab == Tab.Gamepad;
            float pw = Mathf.Min(W - 20f * S, padTab ? 400f * S : 300f * S), ph = Mathf.Min(H - 16f * S, padTab ? 262f * S : 230f * S);
            float px = (W - pw) * 0.5f, py = (H - ph) * 0.5f;
            Box(0, 0, W, H, new Color(0f, 0f, 0f, 0.75f));
            Box(px, py, pw, ph, new Color(0.06f, 0.05f, 0.1f, 0.97f));
            Box(px, py, pw, S, gold); Box(px, py + ph - S, pw, S, gold);
            PixelFont.Draw(W * 0.5f, py + 6f * S, "OPTIONS", S * 2f, gold, 1);

            // tabs
            float tw = (pw - 16f * S) / TabCount, ty = py + 24f * S;
            for (int i = 0; i < TabCount; i++)
            {
                float tx = px + 8f * S + i * tw;
                var rect = new Rect(tx, ty, tw - 2f * S, 12f * S);
                bool hov = rect.Contains(mouse) && rebinding == Btn.COUNT && padRebinding == Btn.COUNT;
                if (hov && click) SetTab(i);
                bool on = VisTab == i;
                Box(rect.x, rect.y, rect.width, rect.height, on ? new Color(1f, 0.82f, 0.3f, 0.85f) : new Color(1f, 1f, 1f, hov ? 0.15f : 0.06f));
                PixelFont.Draw(rect.center.x, ty + 3f * S, TabNames[i], sm, on ? Color.black : Color.white, 1);
            }
            Build();

            // CONTROLS: a keyboard tab and a controller tab
            if (InControls)
            {
                float sw = (pw - 16f * S) * 0.5f, sy = ty + 14f * S;
                for (int i = 0; i < 2; i++)
                {
                    var rect = new Rect(px + 8f * S + i * sw, sy, sw - 2f * S, 11f * S);
                    bool hov = rect.Contains(mouse) && rebinding == Btn.COUNT && padRebinding == Btn.COUNT;
                    if (hov && click) SetSub(i == 1);
                    bool on = padSub == (i == 1);
                    Box(rect.x, rect.y, rect.width, rect.height, on ? new Color(0.55f, 0.8f, 1f, 0.8f) : new Color(1f, 1f, 1f, hov ? 0.15f : 0.06f));
                    PixelFont.Draw(rect.center.x, sy + 3f * S, i == 0 ? "KEYBOARD" : "CONTROLLER", sm, on ? Color.black : Color.white, 1);
                }
                ty += 14f * S;
            }

            // the controller picture with what every button does
            if (padTab)
            {
                Pad hot = Pad.None;
                if (sel >= 0 && sel < rows.Count && rows[sel].padBind != Btn.COUNT) hot = GameInput.padBinds[(int)rows[sel].padBind, Mathf.Clamp(bindSlot, 0, 2)];
                PadArt.Draw(new Rect(px + 6f * S, ty + 16f * S, pw - 12f * S, 74f * S), S, sm, hot);
                PixelFont.Draw(W * 0.5f, ty + 92f * S, GameInput.PadStatus, Mathf.Max(1f, sm - 1f), GameInput.PadConnected ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.4f), 1);
                ty += 82f * S;
            }

            // rows
            float rowH = 12f * S, top = ty + 18f * S, lx = px + 10f * S, vx = px + pw * 0.55f, vw = pw * 0.45f - 10f * S;
            float maxRows = Mathf.Floor((py + ph - 26f * S - top) / rowH);
            int first = Mathf.Clamp(sel - (int)maxRows + 1, 0, Mathf.Max(0, rows.Count - (int)maxRows));
            string hint = null;
            for (int i = first; i < rows.Count && i < first + maxRows; i++)
            {
                var r = rows[i];
                float y = top + (i - first) * rowH;
                var rect = new Rect(px + 6f * S, y - 2f * S, pw - 12f * S, rowH - S);
                bool hov = rect.Contains(mouse) && rebinding == Btn.COUNT && padRebinding == Btn.COUNT;
                if (hov && (click || (Vector2)mouse != lastMouse)) sel = i;
                bool on = i == sel;
                if (on) Box(rect.x, rect.y, rect.width, rect.height, new Color(1f, 1f, 1f, 0.08f));
                var lc = r.dim ? new Color(0.45f, 0.45f, 0.5f) : on ? Color.white : new Color(0.75f, 0.75f, 0.8f);
                PixelFont.Draw(lx, y, (on ? "> " : "  ") + r.label, sm, r.activate != null && on ? gold : lc);
                if (on && r.hint != null) hint = r.hint;

                if (r.bind != Btn.COUNT)
                {
                    // two key cells per action
                    for (int s = 0; s < 2; s++)
                    {
                        float cx = vx + s * (vw * 0.5f), cw = vw * 0.5f - 3f * S;
                        var cell = new Rect(cx, y - S, cw, rowH - 3f * S);
                        bool chov = cell.Contains(mouse) && rebinding == Btn.COUNT;
                        bool waiting = rebinding == r.bind && bindSlot == s;
                        Box(cell.x, cell.y, cell.width, cell.height, waiting ? new Color(1f, 0.82f, 0.3f, 0.8f) : new Color(1f, 1f, 1f, chov || (on && bindSlot == s) ? 0.2f : 0.07f));
                        string k = waiting ? "PRESS A KEY" : GameInput.KeyLabel(GameInput.binds[(int)r.bind, s]);
                        PixelFont.Draw(cell.center.x, y, k, sm, waiting ? Color.black : Color.white, 1);
                        if (chov && click) StartRebind(r.bind, s);
                    }
                    continue;
                }
                if (r.padBind != Btn.COUNT)
                {
                    // three gamepad cells per action
                    for (int s = 0; s < 3; s++)
                    {
                        float cx = vx + s * (vw / 3f), cw = vw / 3f - 3f * S;
                        var cell = new Rect(cx, y - S, cw, rowH - 3f * S);
                        bool chov = cell.Contains(mouse) && padRebinding == Btn.COUNT;
                        bool waiting = padRebinding == r.padBind && bindSlot == s;
                        var pb = GameInput.padBinds[(int)r.padBind, s];
                        Color bcol = pb == Pad.None ? new Color(1f, 1f, 1f, chov || (on && bindSlot == s) ? 0.2f : 0.07f) : PadArt.ButtonColor(pb) * new Color(1f, 1f, 1f, chov || (on && bindSlot == s) ? 0.55f : 0.3f);
                        Box(cell.x, cell.y, cell.width, cell.height, waiting ? new Color(1f, 0.82f, 0.3f, 0.8f) : bcol);
                        string k = waiting ? "PRESS..." : GameInput.PadLabel(pb);
                        PixelFont.Draw(cell.center.x, y, k, sm, waiting ? Color.black : Color.white, 1);
                        if (chov && click) StartPadRebind(r.padBind, s);
                    }
                    continue;
                }
                if (r.get01 != null)
                {
                    // a slider: click or drag along the bar
                    float bw = vw - 30f * S, bh = 3f * S, by = y + 2f * S;
                    Box(vx, by, bw, bh, new Color(1f, 1f, 1f, 0.15f));
                    float f = Mathf.Clamp01(r.get01());
                    Box(vx, by, bw * f, bh, on ? gold : new Color(0.8f, 0.7f, 0.45f));
                    Box(vx + bw * f - S, by - 2f * S, 2f * S, bh + 4f * S, Color.white);
                    PixelFont.Draw(vx + vw, y, r.value(), sm, Color.white, 2);
                    var bar = new Rect(vx - 2f * S, y - 2f * S, bw + 4f * S, rowH);
                    if (bar.Contains(mouse) && held && rebinding == Btn.COUNT) { r.set01(Mathf.Clamp01((mouse.x - vx) / bw)); Settings.Apply(false); }
                    continue;
                }
                string val = r.value != null ? r.value() : "";
                if (val.Length > 0)
                {
                    string shown = r.change != null && on ? "< " + val + " >" : val;
                    PixelFont.Draw(vx + vw, y, shown, sm, r.dim ? new Color(0.45f, 0.45f, 0.5f) : Color.white, 2);
                }
                if (hov && click)
                {
                    if (r.activate != null) r.activate();
                    else if (r.change != null) { r.change(mouse.x < vx + vw * 0.5f && mouse.x > vx ? -1 : 1); Settings.Apply(false); Game.I.sfx.Play("blip", 0.4f, 1.2f); }
                }
            }
            lastMouse = mouse;

            // footer
            string foot = padRebinding != Btn.COUNT ? "PRESS A GAMEPAD BUTTON    VIEW CLEARS    MENU CANCELS"
                : GameInput.UsingStick ? "D-PAD PICK    LEFT/RIGHT CHANGE    A SELECT    LB/RB TABS    B BACK"
                : rebinding != Btn.COUNT ? "PRESS A KEY TO BIND    BACKSPACE CLEARS    ESC CANCELS"
                : tab == Tab.Controls ? "W/S PICK    A/D KEY 1 OR 2    SPACE OR CLICK TO REBIND    Q/E TABS    ESC BACK"
                : "W/S PICK    A/D CHANGE    SPACE OR CLICK    Q/E TABS    ESC BACK";
            if (hint != null) PixelFont.Draw(W * 0.5f, py + ph - 20f * S, hint, sm, new Color(0.6f, 0.6f, 0.65f), 1);
            if (tab == Tab.Controls && rebinding == Btn.COUNT && hint == null)
                PixelFont.Draw(W * 0.5f, py + ph - 20f * S, "CONTROLLER BUTTONS ARE ON THE CONTROLLER TAB", sm, new Color(0.6f, 0.6f, 0.65f), 1);
            PixelFont.Draw(W * 0.5f, py + ph - 11f * S, foot, Mathf.Max(1f, sm - 1f), new Color(0.75f, 0.75f, 0.8f), 1);
        }

        Vector2 lastMouse;
        int openFrame;
    }
}
