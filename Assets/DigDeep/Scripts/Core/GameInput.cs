using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DigDeep
{
    public enum Btn { Left, Right, Up, Down, Attack, Special, Item, Jump, Cycle, Pause, Dash, Swap, Reload, Interact, COUNT }

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
            Stick = g != null ? g.rightStick.ReadValue() : Vector2.zero;
#else
            MouseScreen = Input.mousePosition;
            Stick = Vector2.zero;
#endif
            if ((MouseScreen - lastMouse).sqrMagnitude > 4f) UsingStick = false;
            if (Stick.sqrMagnitude > 0.16f) UsingStick = true;
            lastMouse = MouseScreen;
        }

        static float Scroll()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
            return Input.mouseScrollDelta.y;
#endif
        }

        public static bool Held(Btn b) => cur[(int)b];

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
        public static readonly Btn[] Rebindable = { Btn.Left, Btn.Right, Btn.Up, Btn.Down, Btn.Jump, Btn.Attack, Btn.Special, Btn.Item, Btn.Cycle, Btn.Swap, Btn.Reload, Btn.Interact, Btn.Pause };

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
        }

        public static void LoadBindings()
        {
            DefaultBindings();
            foreach (var b in Rebindable)
                for (int slot = 0; slot < 2; slot++)
                    binds[(int)b, slot] = PlayerPrefs.GetString("bind_" + b + "_" + slot, binds[(int)b, slot]);
        }

        public static void SaveBindings()
        {
            foreach (var b in Rebindable)
                for (int slot = 0; slot < 2; slot++)
                    PlayerPrefs.SetString("bind_" + b + "_" + slot, binds[(int)b, slot] ?? "");
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
            var g = Gamepad.current;
            if (g != null)
            {
                Vector2 st = g.leftStick.ReadValue();
                switch (b)
                {
                    case Btn.Left: gp = g.dpad.left.isPressed || st.x < -0.5f; break;
                    case Btn.Right: gp = g.dpad.right.isPressed || st.x > 0.5f; break;
                    case Btn.Up: gp = st.y > 0.5f; break;
                    case Btn.Down: gp = g.dpad.down.isPressed || st.y < -0.5f; break;
                    case Btn.Jump: gp = g.buttonSouth.isPressed; break;
                    case Btn.Attack: gp = g.buttonWest.isPressed || g.rightTrigger.ReadValue() > 0.4f; break;
                    case Btn.Special: gp = g.buttonNorth.isPressed || g.leftTrigger.ReadValue() > 0.4f; break;
                    case Btn.Item: gp = g.buttonEast.isPressed; break;
                    case Btn.Cycle: gp = g.leftShoulder.isPressed; break;
                    case Btn.Pause: gp = g.startButton.isPressed; break;
                    case Btn.Dash: break;
                    case Btn.Swap: gp = g.rightShoulder.isPressed; break;
                    case Btn.Reload: gp = g.rightStickButton.isPressed; break;
                    case Btn.Interact: gp = g.dpad.up.isPressed; break;
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
