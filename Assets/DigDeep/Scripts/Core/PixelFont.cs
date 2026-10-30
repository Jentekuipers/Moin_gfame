using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Tiny 5x7 bitmap font rendered with IMGUI so the HUD stays crisp pixel art.
    public static class PixelFont
    {
        static Texture2D atlas;
        static readonly Dictionary<char, int> index = new Dictionary<char, int>();
        static readonly Dictionary<char, string[]> glyphs = new Dictionary<char, string[]>();
        public const int CW = 6, CHT = 8;

        static void Gl(char c, params string[] rows) { glyphs[c] = rows; }

        static void Build()
        {
            if (atlas != null) return;
            glyphs.Clear(); index.Clear();
            Gl(' ', ".....", ".....", ".....", ".....", ".....", ".....", ".....");
            Gl('A', ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
            Gl('B', "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####.");
            Gl('C', ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###.");
            Gl('D', "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####.");
            Gl('E', "#####", "#....", "#....", "####.", "#....", "#....", "#####");
            Gl('F', "#####", "#....", "#....", "####.", "#....", "#....", "#....");
            Gl('G', ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####");
            Gl('H', "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
            Gl('I', ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###.");
            Gl('J', "..###", "...#.", "...#.", "...#.", "#..#.", "#..#.", ".##..");
            Gl('K', "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#");
            Gl('L', "#....", "#....", "#....", "#....", "#....", "#....", "#####");
            Gl('M', "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#");
            Gl('N', "#...#", "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#");
            Gl('O', ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
            Gl('P', "####.", "#...#", "#...#", "####.", "#....", "#....", "#....");
            Gl('Q', ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#");
            Gl('R', "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#");
            Gl('S', ".####", "#....", "#....", ".###.", "....#", "....#", "####.");
            Gl('T', "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#..");
            Gl('U', "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
            Gl('V', "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#..");
            Gl('W', "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "#.#.#", ".#.#.");
            Gl('X', "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#");
            Gl('Y', "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#..");
            Gl('Z', "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####");
            Gl('0', ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###.");
            Gl('1', "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###.");
            Gl('2', ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####");
            Gl('3', "####.", "....#", "....#", ".###.", "....#", "....#", "####.");
            Gl('4', "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#.");
            Gl('5', "#####", "#....", "####.", "....#", "....#", "#...#", ".###.");
            Gl('6', ".###.", "#....", "#....", "####.", "#...#", "#...#", ".###.");
            Gl('7', "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#...");
            Gl('8', ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###.");
            Gl('9', ".###.", "#...#", "#...#", ".####", "....#", "....#", ".###.");
            Gl('.', ".....", ".....", ".....", ".....", ".....", ".##..", ".##..");
            Gl(',', ".....", ".....", ".....", ".....", ".##..", "..#..", ".#...");
            Gl(':', ".....", ".##..", ".##..", ".....", ".##..", ".##..", ".....");
            Gl('\u2620', ".###.", "#####", "#.#.#", "#####", ".###.", ".#.#.", ".....");   // a little skull (curses)
            Gl('!', "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#..");
            Gl('?', ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#..");
            Gl('-', ".....", ".....", ".....", ".###.", ".....", ".....", ".....");
            Gl('+', ".....", "..#..", "..#..", "#####", "..#..", "..#..", ".....");
            Gl('&', ".##..", "#..#.", "#.#..", ".#...", "#.#.#", "#..#.", ".##.#");
            Gl('/', "....#", "....#", "...#.", "..#..", ".#...", "#....", "#....");
            Gl('%', "##..#", "##..#", "...#.", "..#..", ".#...", "#..##", "#..##");
            Gl('\'', "..#..", "..#..", ".#...", ".....", ".....", ".....", ".....");
            Gl('(', "...#.", "..#..", ".#...", ".#...", ".#...", "..#..", "...#.");
            Gl(')', ".#...", "..#..", "...#.", "...#.", "...#.", "..#..", ".#...");
            Gl('<', "...#.", "..#..", ".#...", "#....", ".#...", "..#..", "...#.");
            Gl('>', ".#...", "..#..", "...#.", "....#", "...#.", "..#..", ".#...");
            Gl('=', ".....", ".....", "#####", ".....", "#####", ".....", ".....");
            Gl('*', ".....", "#.#.#", ".###.", "#####", ".###.", "#.#.#", ".....");
            Gl('x', ".....", ".....", "#...#", ".#.#.", "..#..", ".#.#.", "#...#");

            int n = glyphs.Count;
            atlas = new Texture2D(n * CW, CHT, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * CW * CHT];
            int i = 0;
            foreach (var kv in glyphs)
            {
                index[kv.Key] = i;
                for (int r = 0; r < 7; r++)
                    for (int c = 0; c < 5; c++)
                        if (kv.Value[r][c] == '#')
                            px[(7 - r) * (n * CW) + i * CW + c] = new Color32(255, 255, 255, 255);
                i++;
            }
            atlas.SetPixels32(px);
            atlas.Apply();
        }

        public static string[] Glyph(char c)
        {
            Build();
            if (!glyphs.TryGetValue(c, out var g) && !glyphs.TryGetValue(char.ToUpperInvariant(c), out g)) g = glyphs['?'];
            return g;
        }

        public static System.Collections.Generic.List<string> Wrap(string text, int maxChars)
        {
            var lines = new System.Collections.Generic.List<string>();
            var line = "";
            foreach (var w in text.Split(' '))
            {
                if (line.Length == 0) line = w;
                else if (line.Length + 1 + w.Length <= maxChars) line += " " + w;
                else { lines.Add(line); line = w; }
            }
            if (line.Length > 0) lines.Add(line);
            return lines;
        }

        public static float Width(string s, float scale) => string.IsNullOrEmpty(s) ? 0 : (s.Length * CW - 1) * scale;

        // align: 0 left, 1 center, 2 right. y is the top of the text (GUI coordinates).
        public static void Draw(float x, float y, string s, float scale, Color col, int align = 0, bool shadow = true)
        {
            if (string.IsNullOrEmpty(s)) return;
            Build();
            float w = Width(s, scale);
            if (align == 1) x -= w * 0.5f; else if (align == 2) x -= w;
            x = Mathf.Round(x); y = Mathf.Round(y);
            col = Readable(col);
            if (shadow)
            {
                // high contrast: a solid near-black outline all round, plus a drop shadow
                float o = Mathf.Max(1f, Mathf.Round(scale * (scale >= 6f ? 0.5f : 1f)));
                var sc = new Color(0.02f, 0.02f, 0.04f, col.a);
                DrawRaw(x + o, y + o, s, scale, sc);
                DrawRaw(x - o, y, s, scale, sc);
                DrawRaw(x + o, y, s, scale, sc);
                DrawRaw(x, y - o, s, scale, sc);
                DrawRaw(x, y + o, s, scale, sc);
                DrawRaw(x + o, y + o * 2f, s, scale, new Color(0f, 0f, 0f, col.a * 0.6f));
            }
            DrawRaw(x, y, s, scale, col);
        }

        // dim text is lifted so everything reads clearly against the dark: hue kept, brightness raised
        static Color Readable(Color c)
        {
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            const float Min = 0.86f;
            if (m <= 0.001f || m >= Min) return c;
            float k = Min / m;
            return new Color(Mathf.Min(1f, c.r * k), Mathf.Min(1f, c.g * k), Mathf.Min(1f, c.b * k), c.a);
        }

        static void DrawRaw(float x, float y, string s, float scale, Color col)
        {
            var old = GUI.color;
            GUI.color = col;
            float aw = atlas.width;
            foreach (char ch in s)
            {
                char c = ch;
                if (!index.TryGetValue(c, out int gi))
                {
                    c = char.ToUpperInvariant(ch);
                    if (!index.TryGetValue(c, out gi)) gi = index['?'];
                }
                if (ch != ' ')
                {
                    var uv = new Rect(gi * CW / aw, 1f / CHT, 5f / aw, 7f / CHT);
                    GUI.DrawTextureWithTexCoords(new Rect(x, y, 5 * scale, 7 * scale), atlas, uv);
                }
                x += CW * scale;
            }
            GUI.color = old;
        }
    }
}
