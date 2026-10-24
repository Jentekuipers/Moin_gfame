using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // The controller picture on the GAMEPAD options tab: a chunky pixel-art pad (dark body, bright outline, like a
    // sticker), drawn once into a texture, with call-out lines from every button to what it does.
    public static class PadArt
    {
        // the picture is hand-made pixel art (Resources/DigDeepArt/pad.bytes, a PNG); a plain one is built if it's missing
        public static int TW = 102, TH = 73;
        static Texture2D tex;
        static Color32[] px;

        static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        static readonly Color32 Body = new Color32(30, 28, 36, 255), Line = new Color32(238, 234, 226, 255);
        static readonly Color32 Part = new Color32(58, 55, 66, 255), Part2 = new Color32(84, 80, 94, 255);

        public static Texture2D Tex
        {
            get
            {
                if (tex != null) return tex;
                var ta = Resources.Load<TextAsset>("DigDeepArt/pad");
                if (ta != null)
                {
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.LoadImage(ta.bytes);
                    tex.filterMode = FilterMode.Point;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    TW = tex.width; TH = tex.height;
                }
                else Build();
                return tex;
            }
        }

        // where each control sits on the picture (texture pixels, y down)
        public static Vector2 Pos(Pad p)
        {
            switch (p)
            {
                case Pad.LT: return new Vector2(20, 2);
                case Pad.RT: return new Vector2(82, 2);
                case Pad.LB: return new Vector2(12, 6);
                case Pad.RB: return new Vector2(90, 6);
                case Pad.DUp: return new Vector2(37, 30);
                case Pad.DDown: return new Vector2(37, 41);
                case Pad.DLeft: return new Vector2(32, 35);
                case Pad.DRight: return new Vector2(43, 35);
                case Pad.Y: return new Vector2(81, 13);
                case Pad.B: return new Vector2(87, 21);
                case Pad.A: return new Vector2(80, 27);
                case Pad.X: return new Vector2(73, 21);
                case Pad.LS: return new Vector2(23, 20);
                case Pad.RS: return new Vector2(66, 35);
                case Pad.View: return new Vector2(43, 21);
                case Pad.Start: return new Vector2(59, 21);
            }
            return Vector2.zero;
        }

        public static Color ButtonColor(Pad p)
        {
            switch (p)
            {
                case Pad.A: return new Color(0.45f, 0.8f, 0.35f);
                case Pad.B: return new Color(0.9f, 0.35f, 0.3f);
                case Pad.X: return new Color(0.35f, 0.6f, 1f);
                case Pad.Y: return new Color(1f, 0.82f, 0.3f);
            }
            return new Color(0.85f, 0.85f, 0.9f);
        }

        static void Set(int x, int y, Color32 c) { if (x >= 0 && x < TW && y >= 0 && y < TH) px[(TH - 1 - y) * TW + x] = c; }
        static Color32 Get(int x, int y) => x >= 0 && x < TW && y >= 0 && y < TH ? px[(TH - 1 - y) * TW + x] : Clear;

        static void Ell(float cx, float cy, float rx, float ry, Color32 c, float maxY = 999f)
        {
            for (int y = (int)(cy - ry) - 1; y <= cy + ry + 1; y++)
                for (int x = (int)(cx - rx) - 1; x <= cx + rx + 1; x++)
                {
                    if (y > maxY) continue;
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                }
        }

        static void Rect(int x0, int y0, int x1, int y1, Color32 c) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(x, y, c); }

        // a white line around every shape of colour `inside` (on transparent or another colour)
        static void Outline(System.Func<Color32, bool> inside, Color32 c)
        {
            var marks = new List<Vector2Int>();
            for (int y = 0; y < TH; y++)
                for (int x = 0; x < TW; x++)
                {
                    if (!inside(Get(x, y))) continue;
                    if (!inside(Get(x + 1, y)) || !inside(Get(x - 1, y)) || !inside(Get(x, y + 1)) || !inside(Get(x, y - 1))) marks.Add(new Vector2Int(x, y));
                }
            foreach (var m in marks) Set(m.x, m.y, c);
        }

        static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        static void Build()
        {
            px = new Color32[TW * TH];
            for (int i = 0; i < px.Length; i++) px[i] = Clear;
            // triggers and bumpers
            Rect(15, 1, 29, 5, Part2); Rect(67, 1, 81, 5, Part2);
            Rect(12, 6, 32, 10, Part); Rect(64, 6, 84, 10, Part);
            Outline(c => c.a > 0, Line);
            // body: a wide top and two grips
            Rect(16, 11, 80, 36, Body);
            Ell(24, 22, 14, 12, Body); Ell(72, 22, 14, 12, Body);
            Ell(22, 38, 13, 19, Body, 57); Ell(74, 38, 13, 19, Body, 57);
            Ell(48, 37, 22, 9, Body);                  // the curve between the grips, round the sticks
            Outline(c => Same(c, Body), Line);
            // centre panel
            Rect(38, 13, 58, 22, Part); Outline(c => Same(c, Part) && true, Part2);
            Ell(42, 26, 1.8f, 1.8f, Part2); Ell(54, 26, 1.8f, 1.8f, Part2);
            // d-pad
            Rect(20, 17, 24, 31, Part); Rect(15, 22, 29, 26, Part);
            Outline(c => Same(c, Part), Line);
            Rect(21, 23, 23, 25, Part2);
            // face buttons
            foreach (var p in new[] { Pad.A, Pad.B, Pad.X, Pad.Y })
            {
                var q = Pos(p); var col = ButtonColor(p);
                Ell(q.x + 0.5f, q.y + 0.5f, 3f, 3f, Line);
                Ell(q.x + 0.5f, q.y + 0.5f, 2.2f, 2.2f, new Color32((byte)(col.r * 200), (byte)(col.g * 200), (byte)(col.b * 200), 255));
            }
            // sticks
            foreach (var p in new[] { Pad.LS, Pad.RS })
            {
                var q = Pos(p);
                Ell(q.x + 0.5f, q.y + 0.5f, 6.5f, 6.5f, Line);
                Ell(q.x + 0.5f, q.y + 0.5f, 5.5f, 5.5f, Body);
                Ell(q.x + 0.5f, q.y + 0.5f, 4f, 4f, Part2);
                Ell(q.x - 0.5f, q.y - 0.5f, 2f, 2f, Line);
            }
            tex = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px); tex.Apply();
        }

        static void Box(float x, float y, float w, float h, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), Art.White);
            GUI.color = old;
        }

        // the short name of what a control does in the current layout
        public static string Does(Pad p)
        {
            bool swap = GameInput.SwapSticks;
            string fixedPart = p == Pad.LS ? (swap ? "AIM" : "MOVE") : p == Pad.RS ? (swap ? "MOVE" : "AIM") : p == Pad.Start ? "PAUSE" : null;
            string bound = null;
            for (int b = 0; b < (int)Btn.COUNT; b++)
                for (int s = 0; s < 3; s++)
                    if (GameInput.padBinds[b, s] == p) bound = ShortAction((Btn)b);
            if (fixedPart != null && bound != null) return fixedPart + " (CLICK: " + bound + ")";
            return fixedPart ?? bound;
        }

        public static string ShortAction(Btn b)
        {
            switch (b)
            {
                case Btn.Jump: return "JUMP";
                case Btn.Attack: return "SHOOT";
                case Btn.Special: return "SPECIAL";
                case Btn.Item: return "CONSUMABLE";
                case Btn.Interact: return "INTERACT";
                case Btn.Swap: return "SWITCH GUN";
                case Btn.Reload: return "RELOAD";
                case Btn.Cycle: return "NEXT ITEM";
                case Btn.CyclePrev: return "PREV ITEM";
                case Btn.Zoom: return "LOOK / AIM (HOLD)";
                case Btn.Down: return "DOWN / SLAM";
            }
            return b.ToString().ToUpperInvariant();
        }

        static readonly Pad[] LeftSide = { Pad.LT, Pad.LB, Pad.View, Pad.DUp, Pad.DLeft, Pad.DRight, Pad.DDown, Pad.LS };
        static readonly Pad[] RightSide = { Pad.RT, Pad.RB, Pad.Start, Pad.Y, Pad.X, Pad.B, Pad.A, Pad.RS };

        // the picture in the middle of `area`, with call-outs on both sides; `hot` lights up one control
        public static void Draw(Rect area, float S, float sm, Pad hot = Pad.None)
        {
            var t = Tex;   // (loads the picture and its size first)
            float k = Mathf.Max(1f, Mathf.Floor(Mathf.Min(area.width * 0.42f / TW, area.height / TH)));
            float w = TW * k, h = TH * k;
            float x0 = Mathf.Round(area.center.x - w * 0.5f), y0 = Mathf.Round(area.y + (area.height - h) * 0.5f);
            GUI.DrawTexture(new Rect(x0, y0, w, h), t);
            DrawSide(LeftSide, area.x + 2f * S, x0, y0, k, area, S, sm, -1, hot);
            DrawSide(RightSide, area.xMax - 2f * S, x0, y0, k, area, S, sm, 1, hot);
        }

        static void DrawSide(Pad[] pads, float edgeX, float x0, float y0, float k, Rect area, float S, float sm, int side, Pad hot)
        {
            float rowH = Mathf.Max(9f * sm, area.height / pads.Length);
            for (int i = 0; i < pads.Length; i++)
            {
                var p = pads[i];
                string what = Does(p);
                bool on = p == hot;
                var q = Pos(p);
                float bx = x0 + (q.x + 0.5f) * k, by = y0 + (q.y + 0.5f) * k;
                float ly = area.y + i * rowH + rowH * 0.5f;
                Color lc = what == null ? new Color(1f, 1f, 1f, 0.15f) : on ? new Color(1f, 0.82f, 0.3f) : new Color(1f, 1f, 1f, 0.45f);
                // the call-out: across from the label, then down/up to the button
                float elbow = side < 0 ? x0 - 4f * S - i % 2 * 2f * S : x0 + TW * k + 4f * S + i % 2 * 2f * S;
                float t = Mathf.Max(1f, Mathf.Round(S * 0.5f));
                float lx0 = side < 0 ? edgeX + 70f * sm : elbow, lx1 = side < 0 ? elbow : edgeX - 70f * sm;
                Box(Mathf.Min(lx0, lx1), ly, Mathf.Abs(lx1 - lx0), t, lc);
                Box(elbow, Mathf.Min(ly, by), t, Mathf.Abs(by - ly), lc);
                Box(Mathf.Min(elbow, bx), by, Mathf.Abs(bx - elbow), t, lc);
                Box(bx - t, by - t, t * 3f, t * 3f, lc);
                // the badge and the label
                string badge = GameInput.PadLabel(p);
                Color bc = ButtonColor(p);
                float bw = PixelFont.Width(badge, sm) + 4f * sm;
                float tx = side < 0 ? edgeX : edgeX - bw;
                Box(tx, ly - 4f * sm, bw, 9f * sm, new Color(bc.r, bc.g, bc.b, what == null ? 0.25f : 0.9f));
                PixelFont.Draw(tx + bw * 0.5f, ly - 3f * sm, badge, sm, Color.black, 1, false);
                string label = what ?? "-";
                float maxW = 70f * sm - bw - 6f * sm;
                float ls = sm;
                while (ls > 1f && PixelFont.Width(label, ls) > maxW) ls -= 1f;
                if (side < 0) PixelFont.Draw(tx + bw + 3f * sm, ly - 3f * sm, label, ls, on ? new Color(1f, 0.82f, 0.3f) : Color.white);
                else PixelFont.Draw(tx - 3f * sm, ly - 3f * sm, label, ls, on ? new Color(1f, 0.82f, 0.3f) : Color.white, 2);
            }
        }
    }
}
