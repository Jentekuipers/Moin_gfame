using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DigDeep
{
    public enum Btn { Left, Right, Up, Down, Attack, Special, Item, Jump, Cycle, Pause, Dash, Swap, Reload, Interact, CyclePrev, Zoom, COUNT }

    // Every button on a gamepad (Xbox names; a PlayStation pad maps A=Cross, B=Circle, X=Square, Y=Triangle).
    public enum Pad { None, A, B, X, Y, LB, RB, LT, RT, LS, RS, DUp, DDown, DLeft, DRight, Start, View, COUNT }

    // WASD / arrows move, mouse aims, left click shoots (Z too), Shift (or right click / X) special, C item, V cycle items,
    // Q or mouse wheel swaps weapon, E interacts (open, take, buy), Space jumps, Esc pauses.
    // Works with the new Input System (Unity 6 default) or the legacy Input Manager, plus gamepads
    // (left stick move, right stick aim, RT shoot, LT / Y special, RB swap weapon, LB cycle items).
    public static class GameInput
    {
        const int N = (int)Btn.COUNT;
        static readonly bool[] cur = new bool[N], prev = new bool[N];
        static readonly float[] buf = new float[N];
        static float scrollBuf;
        static Vector2 lastMouse;

        public static Vector2 MouseScreen { get; private set; }
        public static Vector2 Stick { get; private set; }      // right stick aim
        public static bool UsingStick { get; private set; }    // last aim input came from a gamepad

        public static void Tick(float dt)
        {
            ReadPad();
            ReadAim();
            for (int i = 0; i < N; i++)
            {
                prev[i] = cur[i];
                cur[i] = Raw((Btn)i);
                if (cur[i] && !prev[i]) buf[i] = 0.13f;
                else buf[i] = Mathf.Max(0f, buf[i] - dt);
            }
            // mouse wheel swaps weapons too
            float sc = Scroll();
            if (Mathf.Abs(sc) > 0.01f && scrollBuf <= 0f) { buf[(int)Btn.Swap] = 0.13f; scrollBuf = 0.18f; }
            scrollBuf -= dt;
        }

        static void ReadAim()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) MouseScreen = Mouse.current.position.ReadValue();
            var g = Gamepad.current;
            Stick = g != null ? (SwapSticks ? g.leftStick.ReadValue() : g.rightStick.ReadValue()) : Vector2.zero;
            MoveStick = g != null ? (SwapSticks ? g.rightStick.ReadValue() : g.leftStick.ReadValue()) : Vector2.zero;
#else
            MouseScreen = Input.mousePosition;
            Stick = Vector2.zero; MoveStick = Vector2.zero;
#endif
            bool keys = false;
#if ENABLE_INPUT_SYSTEM
            keys = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
#endif
            if ((MouseScreen - lastMouse).sqrMagnitude > 4f || MouseClicked() || keys) UsingStick = false;
            if (Stick.sqrMagnitude > 0.16f || MoveStick.sqrMagnitude > 0.25f || AnyPadPressed()) UsingStick = true;
            lastMouse = MouseScreen;
        }

        // ------------------------------------------------------------------ gamepad

        public static Vector2 MoveStick { get; private set; }   // the walking stick (left, unless swapped)
        public static bool SwapSticks;                          // SOUTHPAW: walk with the right stick, aim with the left
        public static float AimRadius = 4.5f;                   // how far the reticle sits from you (tiles); x2 while zooming
        public static int PadPreset;                            // index into Presets (or -1: custom)
        const int PN = (int)Pad.COUNT, Slots = 3;
        static readonly bool[] padCur = new bool[PN], padPrev = new bool[PN];
        public static readonly Pad[,] padBinds = new Pad[N, Slots];
        public static readonly Btn[] PadRebindable = { Btn.Jump, Btn.Attack, Btn.Special, Btn.Item, Btn.Interact, Btn.Swap, Btn.Reload, Btn.Cycle, Btn.CyclePrev, Btn.Zoom, Btn.Down };

        public static bool PadConnected
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Gamepad.current != null;
#else
                return false;
#endif
            }
        }

        // what the options screen says about the controller it sees (or why it sees none)
        public static string PadStatus
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Gamepad.current != null) return "CONNECTED: " + Gamepad.current.displayName.ToUpperInvariant();
                if (Joystick.current != null) return "FOUND " + Joystick.current.displayName.ToUpperInvariant() + ", BUT NOT AS A GAMEPAD (TRY XINPUT / X-MODE)";
                return "NO GAMEPAD CONNECTED";
#else
                return "GAMEPADS NEED THE INPUT SYSTEM PACKAGE";
#endif
            }
        }

        static void ReadPad()
        {
            for (int i = 0; i < PN; i++) { padPrev[i] = padCur[i]; padCur[i] = PadRaw((Pad)i); }
        }

        static bool PadRaw(Pad p)
        {
#if ENABLE_INPUT_SYSTEM
            var g = Gamepad.current;
            if (g == null) return false;
            switch (p)
            {
                case Pad.A: return g.buttonSouth.isPressed;
                case Pad.B: return g.buttonEast.isPressed;
                case Pad.X: return g.buttonWest.isPressed;
                case Pad.Y: return g.buttonNorth.isPressed;
                case Pad.LB: return g.leftShoulder.isPressed;
                case Pad.RB: return g.rightShoulder.isPressed;
                case Pad.LT: return g.leftTrigger.ReadValue() > 0.4f;
                case Pad.RT: return g.rightTrigger.ReadValue() > 0.4f;
                case Pad.LS: return g.leftStickButton.isPressed;
                case Pad.RS: return g.rightStickButton.isPressed;
                case Pad.DUp: return g.dpad.up.isPressed;
                case Pad.DDown: return g.dpad.down.isPressed;
                case Pad.DLeft: return g.dpad.left.isPressed;
                case Pad.DRight: return g.dpad.right.isPressed;
                case Pad.Start: return g.startButton.isPressed;
                case Pad.View: return g.selectButton.isPressed;
            }
#endif
            return false;
        }

        public static bool PadHeld(Pad p) => p > Pad.None && p < Pad.COUNT && padCur[(int)p];
        public static bool PadPressed(Pad p) => p > Pad.None && p < Pad.COUNT && padCur[(int)p] && !padPrev[(int)p];

        static bool AnyPadPressed()
        {
            for (int i = 1; i < PN; i++) if (padCur[i] && !padPrev[i]) return true;
            return false;
        }

        // the pad button that went down this frame (for rebinding); None if none
        public static Pad PadPressedThisFrame()
        {
            for (int i = 1; i < PN; i++) if (padCur[i] && !padPrev[i]) return (Pad)i;
            return Pad.None;
        }

        public static string PadLabel(Pad p)
        {
            switch (p)
            {
                case Pad.None: return "-";
                case Pad.DUp: return "D-UP";
                case Pad.DDown: return "D-DOWN";
                case Pad.DLeft: return "D-LEFT";
                case Pad.DRight: return "D-RIGHT";
                case Pad.LS: return "L-STICK";
                case Pad.RS: return "R-STICK";
                case Pad.Start: return "MENU";
                case Pad.View: return "VIEW";
            }
            return p.ToString();
        }

        // what to show in a prompt for this action on the device in use ("E", "X", ...)
        public static string Prompt(Btn b)
        {
            if (UsingStick)
            {
                for (int s = 0; s < Slots; s++) if (padBinds[(int)b, s] != Pad.None) return PadLabel(padBinds[(int)b, s]);
                return "-";
            }
            if (b == Btn.Attack) return "CLICK";            // the mouse is the main way to shoot
            string k = KeyLabel(binds[(int)b, 0]);
            if (k == "-") k = KeyLabel(binds[(int)b, 1]);
            return k == "L-SHIFT" ? "SHIFT" : k;
        }

        // ---- layout presets
        public class Preset
        {
            public string name, blurb;
            public bool swap;
            public readonly System.Collections.Generic.List<(Btn, Pad)> map = new System.Collections.Generic.List<(Btn, Pad)>();
            public Preset(string n, string b, bool swap, params (Btn, Pad)[] m) { name = n; blurb = b; this.swap = swap; map.AddRange(m); }
        }

        public static readonly Preset[] Presets =
        {
            new Preset("STANDARD", "A JUMPS, TRIGGERS SHOOT AND USE SPECIALS, HOLD RB TO LOOK FAR", false,
                (Btn.Jump, Pad.A), (Btn.Interact, Pad.X), (Btn.Swap, Pad.Y), (Btn.Reload, Pad.B), (Btn.Attack, Pad.RT), (Btn.Special, Pad.LT),
                (Btn.Item, Pad.LB), (Btn.Item, Pad.DUp), (Btn.Zoom, Pad.RB), (Btn.Zoom, Pad.RS), (Btn.Cycle, Pad.DRight), (Btn.CyclePrev, Pad.DLeft), (Btn.Down, Pad.DDown)),
            new Preset("TACTICAL", "BUMPERS JUMP AND SHOOT, HOLD RT TO LOOK FAR", false,
                (Btn.Interact, Pad.A), (Btn.Reload, Pad.X), (Btn.Swap, Pad.Y), (Btn.Item, Pad.B), (Btn.Item, Pad.DUp), (Btn.Zoom, Pad.RT), (Btn.Special, Pad.LT),
                (Btn.Jump, Pad.LB), (Btn.Attack, Pad.RB), (Btn.Cycle, Pad.DRight), (Btn.CyclePrev, Pad.DLeft), (Btn.Down, Pad.DDown)),
            new Preset("DOLPHIN DIVE", "CLICK THE LEFT STICK TO JUMP, HOLD LT TO LOOK FAR", false,
                (Btn.Interact, Pad.A), (Btn.Reload, Pad.X), (Btn.Swap, Pad.Y), (Btn.Item, Pad.B), (Btn.Item, Pad.RB), (Btn.Item, Pad.DUp), (Btn.Attack, Pad.RT), (Btn.Zoom, Pad.LT),
                (Btn.Special, Pad.LB), (Btn.Jump, Pad.LS), (Btn.Cycle, Pad.DRight), (Btn.CyclePrev, Pad.DLeft), (Btn.Down, Pad.DDown)),
            new Preset("PLATFORMER", "FACE BUTTONS DO IT ALL, LIKE AN OLD-SCHOOL PLATFORMER", false,
                (Btn.Jump, Pad.A), (Btn.Attack, Pad.X), (Btn.Attack, Pad.RT), (Btn.Special, Pad.B), (Btn.Swap, Pad.Y), (Btn.Reload, Pad.LB), (Btn.Item, Pad.RB), (Btn.Item, Pad.DUp),
                (Btn.Interact, Pad.LT), (Btn.Cycle, Pad.DRight), (Btn.CyclePrev, Pad.DLeft), (Btn.Zoom, Pad.RS), (Btn.Down, Pad.DDown)),
            new Preset("ARENA", "TWIN-STICK SHOOTER: TRIGGERS JUMP AND SHOOT, BUMPERS FOR POWERS", false,
                (Btn.Jump, Pad.LT), (Btn.Attack, Pad.RT), (Btn.Special, Pad.LB), (Btn.Item, Pad.RB), (Btn.Item, Pad.DUp), (Btn.Interact, Pad.A), (Btn.Reload, Pad.X),
                (Btn.Swap, Pad.Y), (Btn.Down, Pad.B), (Btn.Cycle, Pad.DRight), (Btn.CyclePrev, Pad.DLeft), (Btn.Zoom, Pad.RS)),
            new Preset("SOUTHPAW", "STANDARD, BUT WALK WITH THE RIGHT STICK AND AIM WITH THE LEFT", true,
                (Btn.Jump, Pad.A), (Btn.Interact, Pad.X), (Btn.Swap, Pad.Y), (Btn.Reload, Pad.B), (Btn.Attack, Pad.LT), (Btn.Special, Pad.RT),
                (Btn.Item, Pad.RB), (Btn.Item, Pad.DUp), (Btn.Zoom, Pad.LB), (Btn.Zoom, Pad.LS), (Btn.Cycle, Pad.DRight), (Btn.CyclePrev, Pad.DLeft), (Btn.Down, Pad.DDown)),
        };

        public static void ApplyPreset(int i)
        {
            i = Mathf.Clamp(i, 0, Presets.Length - 1);
            for (int b = 0; b < N; b++) for (int s = 0; s < Slots; s++) padBinds[b, s] = Pad.None;
            foreach (var (b, p) in Presets[i].map)
                for (int s = 0; s < Slots; s++) if (padBinds[(int)b, s] == Pad.None) { padBinds[(int)b, s] = p; break; }
            SwapSticks = Presets[i].swap;
            PadPreset = i;
        }

        // does the current layout still match its preset? (if not, it shows as CUSTOM)
        public static bool MatchesPreset(int i)
        {
            if (i < 0 || i >= Presets.Length) return false;
            var want = new System.Collections.Generic.HashSet<(Btn, Pad)>(Presets[i].map);
            var have = new System.Collections.Generic.HashSet<(Btn, Pad)>();
            for (int b = 0; b < N; b++) for (int s = 0; s < Slots; s++) if (padBinds[b, s] != Pad.None) have.Add(((Btn)b, padBinds[b, s]));
            return want.SetEquals(have) && SwapSticks == Presets[i].swap;
        }

        public static void BindPad(Btn b, int slot, Pad p)
        {
            if (p != Pad.None)
                // a button does one thing: take it away from wherever else it was bound
                for (int o = 0; o < N; o++) for (int s = 0; s < Slots; s++) if (padBinds[o, s] == p) padBinds[o, s] = Pad.None;
            padBinds[(int)b, slot] = p;
        }

        static void LoadPad()
        {
            ApplyPreset(PlayerPrefs.GetInt("pad_preset", 0));
            if (PlayerPrefs.GetInt("pad_custom", 0) == 1)
                for (int b = 0; b < N; b++)
                    for (int s = 0; s < Slots; s++)
                    {
                        string v = PlayerPrefs.GetString("pad_" + (Btn)b + "_" + s, "");
                        if (v.Length > 0 && System.Enum.TryParse(v, out Pad p)) padBinds[b, s] = p;
                        else if (v == "") { }
                    }
            SwapSticks = PlayerPrefs.GetInt("pad_swap", SwapSticks ? 1 : 0) == 1;
            AimRadius = PlayerPrefs.GetFloat("pad_aimr", 4.5f);
        }

        static void SavePad()
        {
            PlayerPrefs.SetInt("pad_preset", Mathf.Max(0, PadPreset));
            bool custom = !MatchesPreset(PadPreset);
            PlayerPrefs.SetInt("pad_custom", custom ? 1 : 0);
            for (int b = 0; b < N; b++) for (int s = 0; s < Slots; s++) PlayerPrefs.SetString("pad_" + (Btn)b + "_" + s, padBinds[b, s].ToString());
            PlayerPrefs.SetInt("pad_swap", SwapSticks ? 1 : 0);
            PlayerPrefs.SetFloat("pad_aimr", AimRadius);
        }

        public static void DefaultPad() { ApplyPreset(0); AimRadius = 4.5f; }

        // menus are steered the same on every layout: d-pad or stick to move, A to confirm, B to go back
        static bool InMenu => Game.I == null || Game.I.state != Game.State.Playing;

        static float Scroll()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
            return Input.mouseScrollDelta.y;
#endif
        }

        public static bool Held(Btn b) => cur[(int)b];
        public static bool AnyHeld() { for (int i = 0; i < cur.Length; i++) if (cur[i]) return true; return false; }

        // any key / gamepad button / mouse click this frame (used to skip the intro)
        public static bool AnyPressed()
        {
            for (int i = 0; i < N; i++) if (cur[i] && !prev[i]) return true;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            var g = Gamepad.current;
            if (g != null && (g.buttonSouth.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame || g.startButton.wasPressedThisFrame)) return true;
            return false;
#else
            return Input.anyKeyDown;
#endif
        }
        public static bool Pressed(Btn b) => cur[(int)b] && !prev[(int)b];
        public static bool Released(Btn b) => !cur[(int)b] && prev[(int)b];
        public static bool Buffered(Btn b) => buf[(int)b] > 0f;
        public static void Consume(Btn b) => buf[(int)b] = 0f;
        public static bool Take(Btn b)
        {
            if (buf[(int)b] > 0f) { buf[(int)b] = 0f; return true; }
            return false;
        }

        // ------------------------------------------------------------------ keyboard bindings (two keys per action)

        // key names follow the Input System's Key enum (A, Space, LeftShift, UpArrow, Digit1, Enter, ...)
        public static readonly string[,] binds = new string[N, 2];
        public static readonly Btn[] Rebindable = { Btn.Left, Btn.Right, Btn.Up, Btn.Down, Btn.Jump, Btn.Attack, Btn.Special, Btn.Item, Btn.Cycle, Btn.CyclePrev, Btn.Swap, Btn.Reload, Btn.Interact, Btn.Pause };

        public static string ActionName(Btn b)
        {
            switch (b)
            {
                case Btn.Left: return "MOVE LEFT";
                case Btn.Right: return "MOVE RIGHT";
                case Btn.Up: return "UP / CLIMB / FLOAT";
                case Btn.Down: return "DOWN / SLAM";
                case Btn.Jump: return "JUMP";
                case Btn.Attack: return "SHOOT (+ LEFT MOUSE)";
                case Btn.Special: return "SPECIAL (+ RIGHT MOUSE)";
                case Btn.Item: return "USE ITEM";
                case Btn.Cycle: return "CYCLE ITEMS";
                case Btn.Swap: return "SWAP WEAPON (+ WHEEL)";
                case Btn.Reload: return "RELOAD";
                case Btn.Interact: return "INTERACT";
                case Btn.Pause: return "PAUSE (+ ESC)";
                case Btn.CyclePrev: return "CYCLE ITEMS BACK";
                case Btn.Zoom: return "LOOK / AIM FAR (HOLD)";
            }
            return b.ToString().ToUpperInvariant();
        }

        public static void DefaultBindings()
        {
            for (int i = 0; i < N; i++) { binds[i, 0] = ""; binds[i, 1] = ""; }
            void B(Btn b, string k1, string k2) { binds[(int)b, 0] = k1; binds[(int)b, 1] = k2; }
            B(Btn.Left, "A", "LeftArrow");
            B(Btn.Right, "D", "RightArrow");
            B(Btn.Up, "W", "UpArrow");
            B(Btn.Down, "S", "DownArrow");
            B(Btn.Jump, "Space", "");
            B(Btn.Attack, "Z", "J");
            B(Btn.Special, "LeftShift", "X");
            B(Btn.Item, "C", "L");
            B(Btn.Cycle, "V", "Tab");
            B(Btn.Swap, "Q", "");
            B(Btn.Reload, "R", "");
            B(Btn.Interact, "E", "Enter");
            B(Btn.Pause, "P", "");
            B(Btn.CyclePrev, "", "");
        }

        public static void LoadBindings()
        {
            DefaultBindings();
            foreach (var b in Rebindable)
                for (int slot = 0; slot < 2; slot++)
                    binds[(int)b, slot] = PlayerPrefs.GetString("bind_" + b + "_" + slot, binds[(int)b, slot]);
            LoadPad();
        }

        public static void SaveBindings()
        {
            foreach (var b in Rebindable)
                for (int slot = 0; slot < 2; slot++)
                    PlayerPrefs.SetString("bind_" + b + "_" + slot, binds[(int)b, slot] ?? "");
            SavePad();
        }

        public static string KeyLabel(string k)
        {
            if (string.IsNullOrEmpty(k)) return "-";
            if (k.StartsWith("Digit")) return k.Substring(5);
            switch (k)
            {
                case "LeftArrow": return "LEFT";
                case "RightArrow": return "RIGHT";
                case "UpArrow": return "UP";
                case "DownArrow": return "DOWN";
                case "LeftShift": return "L-SHIFT";
                case "RightShift": return "R-SHIFT";
                case "LeftCtrl": return "L-CTRL";
                case "RightCtrl": return "R-CTRL";
                case "LeftAlt": return "L-ALT";
                case "RightAlt": return "R-ALT";
            }
            return k.ToUpperInvariant();
        }

        // which key (by name) went down this frame; null if none. Used to rebind.
        public static string KeyPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return null;
            foreach (var key in k.allKeys)
                if (key != null && key.wasPressedThisFrame) return key.keyCode.ToString();
            return null;
#else
            foreach (KeyCode kc in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (kc >= KeyCode.Mouse0) break;
                if (Input.GetKeyDown(kc)) return FromKeyCode(kc);
            }
            return null;
#endif
        }

        // a key (by Input System name, e.g. "F1") went down this frame
        public static bool KeyDown(string name)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null || !System.Enum.TryParse(name, out Key key)) return false;
            var c = k[key];
            return c != null && c.wasPressedThisFrame;
#else
            var kc = ToKeyCode(name);
            return kc != KeyCode.None && Input.GetKeyDown(kc);
#endif
        }

        public static float ScrollY() => Scroll();

        public static bool MouseClicked()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        public static bool MouseHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
            return Input.GetMouseButton(0);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        static bool KeyHeld(Keyboard k, Btn b)
        {
            if (b == Btn.Pause && k.escapeKey.isPressed) return true;   // Esc always pauses, whatever is bound
            for (int slot = 0; slot < 2; slot++)
            {
                string name = binds[(int)b, slot];
                if (string.IsNullOrEmpty(name) || !System.Enum.TryParse(name, out Key key) || key == Key.None) continue;
                var c = k[key];
                if (c != null && c.isPressed) return true;
            }
            return false;
        }
#endif

        // the legacy Input Manager names a few keys differently
        static KeyCode ToKeyCode(string k)
        {
            if (string.IsNullOrEmpty(k)) return KeyCode.None;
            if (k.StartsWith("Digit")) k = "Alpha" + k.Substring(5);
            else if (k.StartsWith("Numpad")) k = "Keypad" + k.Substring(6);
            else if (k == "Enter") k = "Return";
            else if (k == "LeftCtrl") k = "LeftControl";
            else if (k == "RightCtrl") k = "RightControl";
            else if (k == "Backquote") k = "BackQuote";
            return System.Enum.TryParse(k, out KeyCode kc) ? kc : KeyCode.None;
        }

        static string FromKeyCode(KeyCode kc)
        {
            string k = kc.ToString();
            if (k.StartsWith("Alpha")) return "Digit" + k.Substring(5);
            if (k.StartsWith("Keypad")) return "Numpad" + k.Substring(6);
            if (k == "Return") return "Enter";
            if (k == "LeftControl") return "LeftCtrl";
            if (k == "RightControl") return "RightCtrl";
            if (k == "BackQuote") return "Backquote";
            return k;
        }

        static bool Raw(Btn b)
        {
#if ENABLE_INPUT_SYSTEM
            bool kb = false, gp = false;
            var k = Keyboard.current;
            var m = Mouse.current;
            if (k != null) kb = KeyHeld(k, b);
            if (m != null)
            {
                if (b == Btn.Attack) kb |= m.leftButton.isPressed;
                if (b == Btn.Special) kb |= m.rightButton.isPressed;
            }
            if (Gamepad.current != null)
            {
                Vector2 st = MoveStick;
                if (InMenu)
                {
                    // menus: d-pad / stick steer, A confirms, B or MENU backs out, X and Y work as E and Q
                    switch (b)
                    {
                        case Btn.Left: gp = PadHeld(Pad.DLeft) || st.x < -0.5f; break;
                        case Btn.Right: gp = PadHeld(Pad.DRight) || st.x > 0.5f; break;
                        case Btn.Up: gp = PadHeld(Pad.DUp) || st.y > 0.5f; break;
                        case Btn.Down: gp = PadHeld(Pad.DDown) || st.y < -0.5f; break;
                        case Btn.Jump: gp = PadHeld(Pad.A); break;
                        case Btn.Pause: gp = PadHeld(Pad.B) || PadHeld(Pad.Start); break;
                        case Btn.Interact: gp = PadHeld(Pad.X); break;
                        case Btn.Swap: gp = PadHeld(Pad.Y); break;
                    }
                }
                else
                {
                    switch (b)
                    {
                        case Btn.Left: gp = st.x < -0.4f; break;
                        case Btn.Right: gp = st.x > 0.4f; break;
                        case Btn.Up: gp = st.y > 0.55f; break;
                        case Btn.Down: gp = st.y < -0.6f; break;
                        case Btn.Pause: gp = PadHeld(Pad.Start); break;
                    }
                    for (int s = 0; s < Slots && !gp; s++) gp = PadHeld(padBinds[(int)b, s]);
                }
            }
            return kb || gp;
#else
            if (b == Btn.Attack && Input.GetMouseButton(0)) return true;
            if (b == Btn.Special && Input.GetMouseButton(1)) return true;
            for (int slot = 0; slot < 2; slot++)
            {
                var kc = ToKeyCode(binds[(int)b, slot]);
                if (kc != KeyCode.None && Input.GetKey(kc)) return true;
            }
            if (b == Btn.Pause && Input.GetKey(KeyCode.Escape)) return true;
            return false;
#endif
        }
    }
}
