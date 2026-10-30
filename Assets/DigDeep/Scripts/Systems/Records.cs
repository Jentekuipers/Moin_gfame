using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Lifetime records (kept in PlayerPrefs): runs, deaths and what caused them, kills, bosses beaten, and which
    // weapons, trinkets and bosses you've ever come across (for the UNLOCKS screen). Test runs never count.
    public static class Records
    {
        const string P = "rec_";

        public static int Get(string k) => PlayerPrefs.GetInt(P + k, 0);
        public static void Add(string k, int n = 1) { if (!TestMode.On) PlayerPrefs.SetInt(P + k, Get(k) + n); }

        // ---- things seen ("w" weapons, "s" trinkets, "b" bosses by kind)
        public static void Mark(string kind, string id)
        {
            if (TestMode.On || string.IsNullOrEmpty(id)) return;
            string k = P + "seen_" + kind + "_" + id;
            if (PlayerPrefs.GetInt(k, 0) == 1) return;
            PlayerPrefs.SetInt(k, 1);
        }
        public static bool Seen(string kind, string id) => PlayerPrefs.GetInt(P + "seen_" + kind + "_" + id, 0) == 1;

        // ---- the end of a run
        public static void RunEnded(Player p, bool won, string cause, float seconds)
        {
            if (TestMode.On || p == null) return;
            Add("runs");
            if (won) Add("wins");
            else
            {
                Add("deaths");
                if (string.IsNullOrEmpty(cause)) cause = "THE DARK";
                string list = PlayerPrefs.GetString(P + "causes", "");
                if (("|" + list + "|").IndexOf("|" + cause + "|") < 0) PlayerPrefs.SetString(P + "causes", list.Length > 0 ? list + "|" + cause : cause);
                Add("cause_" + cause);
            }
            Add("kills", p.s.kills);
            Add("gold", p.s.gold);
            Add("secs", Mathf.RoundToInt(seconds));
            PlayerPrefs.Save();
        }

        public static List<(string, int)> Causes()
        {
            var r = new List<(string, int)>();
            string list = PlayerPrefs.GetString(P + "causes", "");
            if (list.Length == 0) return r;
            foreach (var c in list.Split('|')) r.Add((c, Get("cause_" + c)));
            r.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            return r;
        }
    }

    // The STATS and UNLOCKS screens (from the title menu). Any key or click goes back.
    public static class RecordScreens
    {
        static void Box(float x, float y, float w, float h, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), Art.White);
            GUI.color = old;
        }

        static void Sprite(UnityEngine.Sprite s, float x, float y, float size, Color c)
        {
            if (s == null) return;
            var t = s.texture;
            var r = s.textureRect;
            float k = size / Mathf.Max(r.width, r.height);
            float w = r.width * k, h = r.height * k;
            var old = GUI.color; GUI.color = c;
            GUI.DrawTextureWithTexCoords(new Rect(x + (size - w) * 0.5f, y + (size - h) * 0.5f, w, h), t,
                new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height));
            GUI.color = old;
        }

        static float Scale => Mathf.Max(2f, Mathf.Floor(Screen.height / 270f * Settings.UiScale));
        static readonly Color Gold = new Color(1f, 0.82f, 0.32f), Grey = new Color(0.7f, 0.7f, 0.75f);

        static void Frame(string title, float S)
        {
            float W = Screen.width, H = Screen.height;
            Box(0, 0, W, H, new Color(0.03f, 0.02f, 0.06f, 0.96f));
            PixelFont.Draw(W * 0.5f, 10f * S, title, S * 2f, Gold, 1);
            PixelFont.Draw(W * 0.5f, H - 12f * S, (GameInput.UsingStick ? "B" : "ESC") + " OR CLICK: BACK", Mathf.Max(1f, S - 1f), Grey, 1);
        }

        // ------------------------------------------------------------------ stats

        public static void DrawStats()
        {
            float S = Scale, sm = Mathf.Max(1f, S - 1f);
            float W = Screen.width;
            Frame("STATS", S);
            int secs = Records.Get("secs");
            (string, string)[] rows =
            {
                ("RUNS", Records.Get("runs").ToString()),
                ("FINISHED RUNS (GOD-DIAMOND)", Records.Get("wins").ToString()),
                ("VOID RUNS BEATEN", PlayerPrefs.GetInt("digdeep_voidwins", 0).ToString()),
                ("DEATHS", Records.Get("deaths").ToString()),
                ("KILLS", Records.Get("kills").ToString()),
                ("BOSSES BEATEN", Records.Get("bosses").ToString()),
                ("GOLD CARRIED OUT", Records.Get("gold").ToString()),
                ("BEST DEPTH", "-" + PlayerPrefs.GetInt("digdeep_best", 0)),
                ("TIME DIGGING", (secs / 3600) + "H " + (secs / 60 % 60).ToString("00") + "M"),
            };
            float lx = W * 0.12f, vx = W * 0.47f, y = 40f * S, line = 11f * S;
            foreach (var (k, v) in rows)
            {
                PixelFont.Draw(lx, y, k, sm, Grey);
                PixelFont.Draw(vx, y, v, sm, Color.white, 2);
                y += line;
            }
            // what got you, most often first
            float cx = W * 0.55f, cvx = W * 0.9f, cy = 40f * S;
            PixelFont.Draw(cx, cy, "CAUSES OF DEATH", sm, new Color(1f, 0.45f, 0.4f));
            cy += line;
            var causes = Records.Causes();
            if (causes.Count == 0) PixelFont.Draw(cx, cy, "NONE YET", sm, Grey);
            foreach (var (c, n) in causes)
            {
                if (cy > Screen.height - 24f * S) break;
                PixelFont.Draw(cx, cy, c, sm, Color.white);
                PixelFont.Draw(cvx, cy, n.ToString(), sm, Color.white, 2);
                cy += line * 0.9f;
            }
        }

        // ------------------------------------------------------------------ unlocks

        public static void DrawUnlocks()
        {
            float S = Scale, sm = Mathf.Max(1f, S - 1f);
            float W = Screen.width, H = Screen.height;
            Frame("UNLOCKS", S);
            float top = 32f * S, bottom = H - 18f * S, mid = (top + bottom) * 0.5f;
            float pad = 8f * S;
            var mouse = Event.current.mousePosition;
            string tip = null;

            // CHARACTERS: a diamond (and a void diamond) for each digger who finished a run
            {
                var r = new Rect(pad, top, W * 0.5f - pad * 1.5f, mid - top - pad * 0.5f);
                int have = 0;
                foreach (var c in Chars.All) if (Unlocks.CharOpen(c)) have++;
                Panel(r, "CHARACTERS", have, Chars.All.Count, S, sm);
                float size = 22f * S, x = r.x + 6f * S, y = r.y + 16f * S;
                foreach (var c in Chars.All)
                {
                    bool won = PlayerPrefs.GetInt("digdeep_won_" + c.id, 0) == 1;
                    bool vwon = PlayerPrefs.GetInt("digdeep_voidwon_" + c.id, 0) == 1;
                    if (x + size > r.xMax - 4f * S) { x = r.x + 6f * S; y += size + 10f * S; }
                    bool open = Unlocks.CharOpen(c);
                    // locked: a black shape (hover says how to free them); open: full colour, diamonds underneath
                    Sprite(c.frames != null && c.frames.Length > 0 ? c.frames[0] : null, x, y, size, !open ? Color.black : won ? Color.white : new Color(0.62f, 0.62f, 0.68f));
                    if (won) PixelFont.Draw(x + size * 0.5f, y + size + S, vwon ? "* *" : "*", sm, vwon ? new Color(0.8f, 0.55f, 1f) : new Color(0.6f, 0.9f, 1f), 1);
                    if (new Rect(x, y, size, size).Contains(mouse))
                        tip = !open ? "LOCKED: " + Unlocks.How("char:" + c.id) : c.name + (won ? (vwon ? ": GOD-DIAMOND AND VOID DIAMOND" : ": GOD-DIAMOND") : ": NO DIAMOND YET");
                    x += size + 4f * S;
                }
            }
            // WEAPONS
            {
                var r = new Rect(W * 0.5f + pad * 0.5f, top, W * 0.5f - pad * 1.5f, mid - top - pad * 0.5f);
                var list = new List<Def>();
                foreach (var a in DB.Attacks) list.Add(a);
                tip = Grid(r, "WEAPONS", list, "w", S, sm, mouse) ?? tip;
            }
            // SKILLS (trinkets)
            {
                var r = new Rect(pad, mid + pad * 0.5f, W * 0.5f - pad * 1.5f, bottom - mid - pad * 0.5f);
                var list = new List<Def>();
                foreach (var t in DB.Trinkets) list.Add(t);
                tip = Grid(r, "SKILLS", list, "s", S, sm, mouse) ?? tip;
            }
            // BOSSES: beaten ones by name
            {
                var r = new Rect(W * 0.5f + pad * 0.5f, mid + pad * 0.5f, W * 0.5f - pad * 1.5f, bottom - mid - pad * 0.5f);
                var kinds = BossArena.BossKinds;
                int have = 0, total = 0;
                for (int i = 0; i < kinds.Length; i++) { if (i == 11) continue; total++; if (Records.Seen("b", i.ToString())) have++; }
                Panel(r, "BOSSES", have, total, S, sm);
                float x = r.x + 6f * S, y = r.y + 16f * S, colW = (r.width - 12f * S) * 0.5f, lh = 9f * sm;
                int n = 0;
                for (int i = 0; i < kinds.Length; i++)
                {
                    if (i == 11) continue;
                    bool beat = Records.Seen("b", i.ToString());
                    float px = x + (n % 2) * colW, py = y + (n / 2) * lh;
                    if (py < r.yMax - lh) PixelFont.Draw(px, py, beat ? kinds[i] : "???", Mathf.Max(1f, sm - 1f), beat ? new Color(1f, 0.55f, 0.45f) : new Color(0.35f, 0.35f, 0.4f));
                    n++;
                }
            }
            if (tip != null)
            {
                float ts = sm;
                while (ts > 1f && PixelFont.Width(tip, ts) > W * 0.94f) ts -= 1f;
                PixelFont.Draw(W * 0.5f, H - 22f * S, tip, ts, tip.StartsWith("LOCKED") ? new Color(1f, 0.82f, 0.4f) : Color.white, 1);
            }
            {
                int got = 0;
                foreach (var u in Unlocks.All) if (Unlocks.Done(u.id)) got++;
                PixelFont.Draw(W - pad, 8f * S, "ACHIEVEMENTS " + got + " / " + Unlocks.All.Count, sm, new Color(1f, 0.82f, 0.4f), 2);
            }
        }

        static void Panel(Rect r, string name, int have, int total, float S, float sm)
        {
            Box(r.x, r.y, r.width, r.height, new Color(1f, 1f, 1f, 0.04f));
            Box(r.x, r.y, r.width, S, new Color(Gold.r, Gold.g, Gold.b, 0.5f));
            PixelFont.Draw(r.x + 6f * S, r.y + 4f * S, name, sm, Gold);
            PixelFont.Draw(r.xMax - 6f * S, r.y + 4f * S, have + " / " + total, sm, Color.white, 2);
        }

        // a grid of icons: found ones in colour, the rest as dark shapes. Returns the hovered one's line.
        static string Grid(Rect r, string name, List<Def> list, string kind, float S, float sm, Vector2 mouse)
        {
            int have = 0;
            foreach (var d in list) if (Records.Seen(kind, d.id)) have++;
            Panel(r, name, have, list.Count, S, sm);
            float x0 = r.x + 5f * S, y0 = r.y + 15f * S;
            float area = (r.width - 10f * S) * (r.height - 18f * S);
            float size = Mathf.Clamp(Mathf.Floor(Mathf.Sqrt(area / Mathf.Max(1, list.Count)) - 2f * S), 6f * S, 16f * S);
            int cols = Mathf.Max(1, Mathf.FloorToInt((r.width - 10f * S) / (size + 2f * S)));
            string tip = null;
            for (int i = 0; i < list.Count; i++)
            {
                float x = x0 + (i % cols) * (size + 2f * S), y = y0 + (i / cols) * (size + 2f * S);
                if (y + size > r.yMax) break;
                bool seen = Records.Seen(kind, list[i].id);
                string how = Unlocks.How(list[i].id);   // locked behind an achievement?
                // locked: solid black, hover tells you how; unlocked but never found: a dim shape; found: full colour
                Sprite(list[i].icon, x, y, size, how != null ? Color.black : seen ? Color.white : new Color(0.3f, 0.3f, 0.36f, 0.9f));
                if (new Rect(x, y, size, size).Contains(mouse)) tip = how != null ? "LOCKED: " + how : seen ? list[i].name : "NOT FOUND YET";
            }
            return tip;
        }
    }
}
