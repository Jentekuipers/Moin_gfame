using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Decoration pictures for the newer biomes, drawn in code with a few variations each (seeded, so a spot always
    // gets the same one): palm trees, parasols and sand castles on the beach; seaweed, anemones, brain coral,
    // starfish and shells under the water; nests, weather vanes and sky lanterns in the clouds; bone piles, blood
    // shrooms, thorns and cocoons in the crimson forest; glowing shrooms, flowers and lanterns in the neon canopy.
    // Every picture stands on its bottom middle and gets the usual dark outline.
    public static class ArtDecor
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Color32 Out = new Color32(28, 18, 30, 255);

        class Cv
        {
            public readonly int w, h;
            public readonly Color32[] px;
            public Cv(int w, int h) { this.w = w; this.h = h; px = new Color32[w * h]; }
            public void Set(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = c; }
            public Color32 Get(int x, int y) => x >= 0 && y >= 0 && x < w && y < h ? px[y * w + x] : default;
            public void Rect(int x, int y, int rw, int rh, Color32 c) { for (int j = y; j < y + rh; j++) for (int i = x; i < x + rw; i++) Set(i, j, c); }
            public void Disc(float cx, float cy, float r, Color32 c) { for (int j = (int)(cy - r - 1); j <= cy + r + 1; j++) for (int i = (int)(cx - r - 1); i <= cx + r + 1; i++) if ((i - cx) * (i - cx) + (j - cy) * (j - cy) <= r * r) Set(i, j, c); }
            public void Line(float x0, float y0, float x1, float y1, Color32 c, int thick = 1)
            {
                int n = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))) + 1;
                for (int k = 0; k <= n; k++)
                {
                    float t = k / (float)n;
                    int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                    for (int a = 0; a < thick; a++) for (int b = 0; b < thick; b++) Set(x + a, y + b, c);
                }
            }
            public Sprite Done()
            {
                // the outline round everything
                var o = (Color32[])px.Clone();
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        if (px[y * w + x].a != 0) continue;
                        bool nb = (x > 0 && px[y * w + x - 1].a != 0) || (x < w - 1 && px[y * w + x + 1].a != 0) || (y > 0 && px[(y - 1) * w + x].a != 0) || (y < h - 1 && px[(y + 1) * w + x].a != 0);
                        if (nb) o[y * w + x] = Out;
                    }
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                tex.SetPixels32(o); tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), Art.PPU, 0, SpriteMeshType.FullRect);
            }
        }

        static Color32 C(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

        public static Sprite Get(string kind, int v)
        {
            string key = kind + v;
            if (cache.TryGetValue(key, out var s)) return s;
            var r = new System.Random(v * 7919 + kind.Length * 31);
            switch (kind)
            {
                case "palm": s = Palm(r); break;
                case "parasol": s = Parasol(r, v); break;
                case "castle": s = Castle(r); break;
                case "seaweed": s = Seaweed(r, false); break;
                case "kelp": s = Seaweed(r, true); break;
                case "anemone": s = Anemone(r); break;
                case "brain": s = Brain(r); break;
                case "starfish": s = Starfish(r); break;
                case "shell": s = Shell(r); break;
                case "nest": s = Nest(r); break;
                case "vane": s = Vane(r); break;
                case "skylantern": s = Lantern(r, C(255, 190, 110)); break;
                case "bones": s = Bones(r); break;
                case "bloodshroom": s = Shroom(r, C(190, 30, 50), C(255, 210, 210), false); break;
                case "thorns": s = Thorns(r); break;
                case "cocoon": s = Cocoon(r); break;
                case "neonshroom": s = Shroom(r, r.Next(2) == 0 ? C(255, 70, 210) : C(70, 255, 220), C(255, 255, 255), true); break;
                case "neonflower": s = Flower(r); break;
                case "neonlantern": s = Lantern(r, r.Next(2) == 0 ? C(255, 90, 230) : C(90, 255, 230)); break;
                default: s = Shell(r); break;
            }
            cache[key] = s;
            return s;
        }

        // ------------------------------------------------------------------ the beach
        // A palm: a ringed trunk curving up (taller or shorter, leaning either way), a crown of drooping fronds,
        // and a cluster of coconuts.
        static Sprite Palm(System.Random r)
        {
            int H = 52 + r.Next(30), W = 72;
            var c = new Cv(W, H);
            int lean = r.Next(2) == 0 ? -1 : 1;
            float bend = 8f + (float)r.NextDouble() * 14f;
            Color32 tk = C(150, 98, 56), tkD = C(108, 66, 38), tkL = C(190, 136, 84);
            int topY = H - 16;
            float topX = W / 2f;
            for (int y = 0; y <= topY; y++)
            {
                float t = y / (float)topY;
                float x = W / 2f - lean * bend * 0.4f + lean * bend * t * t;   // a curve, leaning more towards the top
                int wd = t < 0.3f ? 5 : 4, rx = Mathf.RoundToInt(x);
                for (int k = 0; k < wd; k++) c.Set(rx + k - 2, y, k == 0 ? tkL : k >= wd - 1 ? tkD : tk);
                if (y % 5 == 0) for (int k = 1; k < wd; k++) c.Set(rx + k - 2, y, tkD);   // the rings
                if (y == topY) topX = x + 0.5f;
            }
            // the fronds: out to each side at a few angles, arching up then drooping at the tips, leaflets hanging off them
            Color32 gD = C(30, 100, 52), g = C(56, 150, 64), gL = C(104, 190, 74), gH = C(166, 222, 100);
            int[] angs = { 8, 32, 58, 80 };
            for (int side = -1; side <= 1; side += 2)
            {
                int na = 3 + r.Next(2);
                for (int ai = 0; ai < na; ai++)
                {
                    int a0 = angs[ai];
                    float a = (a0 + ((float)r.NextDouble() - 0.5f) * 12f) * Mathf.Deg2Rad;
                    float ln = (a0 < 40 ? 20f : 15f) + (float)r.NextDouble() * 7f;
                    float x = topX, y = topY + 1;
                    float dx = Mathf.Cos(a) * side, dy = Mathf.Sin(a) + 0.35f;
                    for (int k = 0; k < (int)ln; k++)
                    {
                        float t = k / ln;
                        x += dx * 0.95f; y += dy - t * t * (a0 < 60 ? 2.2f : 1.5f);
                        int rx = Mathf.RoundToInt(x), ry = Mathf.RoundToInt(y);
                        c.Set(rx, ry, g); c.Set(rx, ry + 1, t < 0.7f ? gL : g);
                        if (t < 0.5f) c.Set(rx, ry + 2, gH);
                        int ll = (int)(1f + 3.2f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.05f)));
                        for (int j = 1; j <= ll; j++) c.Set(Mathf.RoundToInt(x - dx * j * 0.4f), ry - j, j == ll ? gD : j < ll - 1 ? g : gL);
                    }
                }
            }
            // a few coconuts under the crown
            int nuts = r.Next(4);
            for (int k = 0; k < nuts; k++) c.Disc(topX - 2 + k * 2, topY - 1 - (k % 2), 1.5f, C(104, 64, 34));
            return c.Done();
        }

        static Sprite Parasol(System.Random r, int v)
        {
            var c = new Cv(30, 28);
            Color32[] cols = { C(230, 60, 60), C(60, 130, 230), C(250, 200, 60), C(60, 200, 170), C(240, 120, 200) };
            Color32 a = cols[v % cols.Length], b = C(250, 248, 240);
            int lean = r.Next(3) - 1;
            c.Line(15, 0, 15 + lean * 3, 20, C(200, 190, 170), 1);
            // the canopy: a dome cut into stripes
            for (int y = 18; y < 27; y++)
                for (int x = 1; x < 29; x++)
                {
                    float u = (x - 15f - lean * 3) / 14f, vv = (y - 18f) / 8.5f;
                    if (u * u + vv * vv * 1.2f > 1f) continue;
                    c.Set(x, y, (((x - lean * 3) / 4) & 1) == 0 ? a : b);
                }
            for (int x = 2; x < 28; x += 4) c.Set(x, 18, C(200, 190, 170));
            // a towel underneath now and then
            if (r.Next(2) == 0) c.Rect(3 + r.Next(6), 0, 12, 1, cols[(v + 2) % cols.Length]);
            return c.Done();
        }

        static Sprite Castle(System.Random r)
        {
            int towers = 1 + r.Next(3);
            int W = 10 + towers * 8, H = 22;
            var c = new Cv(W, H);
            Color32 s = C(228, 196, 130), sD = C(190, 154, 96), sL = C(246, 222, 164);
            c.Rect(2, 0, W - 4, 6, s);
            for (int x = 2; x < W - 2; x += 3) c.Set(x, 6, s);
            for (int t = 0; t < towers; t++)
            {
                int tx = 4 + t * 8;
                int th = 9 + r.Next(6);
                c.Rect(tx, 0, 5, th, s);
                c.Set(tx, th, s); c.Set(tx + 2, th, s); c.Set(tx + 4, th, s);
                c.Rect(tx + 4, 0, 1, th, sD);
                c.Rect(tx, 0, 1, th, sL);
                c.Rect(tx + 2, th - 4, 1, 2, C(90, 66, 44));   // a window
                if (r.Next(2) == 0) { c.Line(tx + 2, th + 1, tx + 2, th + 5, C(200, 190, 170)); c.Rect(tx + 3, th + 3, 3, 2, r.Next(2) == 0 ? C(230, 60, 60) : C(60, 130, 230)); }   // a flag
            }
            c.Rect(W / 2 - 1, 0, 2, 3, C(110, 80, 50));   // the gate
            return c.Done();
        }

        // ------------------------------------------------------------------ under the water
        static Sprite Seaweed(System.Random r, bool kelp)
        {
            int H = kelp ? 28 + r.Next(20) : 10 + r.Next(10), W = 12;
            var c = new Cv(W, H);
            Color32 g = kelp ? C(90, 150, 60) : C(60, 170, 100), gL = kelp ? C(140, 190, 80) : C(110, 210, 140);
            int strands = kelp ? 1 + r.Next(2) : 2 + r.Next(2);
            for (int s = 0; s < strands; s++)
            {
                float ph = (float)r.NextDouble() * 6f, x0 = 3 + s * 3 + (float)r.NextDouble();
                for (int y = 0; y < H - s * 3; y++)
                {
                    int x = Mathf.RoundToInt(x0 + Mathf.Sin(y * 0.3f + ph) * 1.5f);
                    c.Set(x, y, g); c.Set(x + 1, y, gL);
                    if (kelp && y % 6 == 3) { c.Set(x + 2, y, gL); c.Set(x + 3, y + 1, g); }   // the blades
                }
            }
            return c.Done();
        }

        static Sprite Anemone(System.Random r)
        {
            var c = new Cv(16, 12);
            Color32 b = r.Next(2) == 0 ? C(255, 110, 150) : C(255, 160, 80), t = C(255, 210, 230);
            c.Rect(5, 0, 6, 4, Pal.Shade(b, 0.8f));
            for (int k = 0; k < 7; k++)
            {
                float a = Mathf.Lerp(150f, 30f, k / 6f) * Mathf.Deg2Rad;
                for (int j = 0; j < 6; j++) c.Set(Mathf.RoundToInt(8 + Mathf.Cos(a) * j), Mathf.RoundToInt(4 + Mathf.Sin(a) * j), j == 5 ? t : b);
            }
            return c.Done();
        }

        static Sprite Brain(System.Random r)
        {
            var c = new Cv(16, 10);
            Color32 b = r.Next(2) == 0 ? C(230, 150, 160) : C(200, 190, 120);
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 16; x++)
                {
                    float u = (x - 7.5f) / 7.5f, v = y / 8.5f;
                    if (u * u + v * v > 1f) continue;
                    c.Set(x, y, Mathf.Sin(x * 1.3f + Mathf.Sin(y * 0.9f) * 2f) > 0.4f ? Pal.Shade(b, 0.75f) : b);   // its folds
                }
            return c.Done();
        }

        static Sprite Starfish(System.Random r)
        {
            var c = new Cv(9, 5);
            Color32 b = r.Next(2) == 0 ? C(255, 130, 90) : C(240, 90, 160);
            c.Rect(3, 0, 3, 2, b); c.Set(4, 2, b); c.Set(4, 3, b); c.Set(1, 1, b); c.Set(2, 1, b); c.Set(6, 1, b); c.Set(7, 1, b); c.Set(0, 0, b); c.Set(8, 0, b); c.Set(4, 4, b);
            return c.Done();
        }

        static Sprite Shell(System.Random r)
        {
            var c = new Cv(7, 5);
            Color32 b = r.Next(2) == 0 ? C(255, 220, 200) : C(240, 200, 230);
            for (int y = 0; y < 5; y++) for (int x = 0; x < 7; x++) { float u = (x - 3f) / 3.5f, v = y / 4.5f; if (u * u + v * v <= 1f) c.Set(x, y, (x & 1) == 0 ? b : Pal.Shade(b, 0.82f)); }
            return c.Done();
        }

        // ------------------------------------------------------------------ the clouds
        static Sprite Nest(System.Random r)
        {
            var c = new Cv(14, 7);
            for (int x = 0; x < 14; x++) for (int y = 0; y < 4; y++) if (Mathf.Abs(x - 6.5f) < 7 - y * 0.6f) c.Set(x, y, (x + y) % 3 == 0 ? C(120, 84, 50) : C(160, 118, 70));
            int eggs = 1 + r.Next(3);
            for (int k = 0; k < eggs; k++) c.Disc(4 + k * 3, 4, 1.4f, C(220, 236, 250));
            return c.Done();
        }

        static Sprite Vane(System.Random r)
        {
            var c = new Cv(14, 22);
            c.Line(7, 0, 7, 17, C(150, 150, 160));
            c.Line(2, 15, 12, 15, C(200, 170, 80));
            c.Set(1, 15, C(200, 170, 80)); c.Set(12, 16, C(200, 170, 80)); c.Set(12, 14, C(200, 170, 80));
            c.Rect(5, 18, 5, 3, C(230, 200, 90));   // a little golden bird on top
            c.Set(10, 19, C(230, 140, 60));
            return c.Done();
        }

        static Sprite Lantern(System.Random r, Color32 glow)
        {
            var c = new Cv(9, 14);
            c.Line(4, 9, 4, 13, C(60, 50, 50));
            for (int y = 2; y < 9; y++) for (int x = 1; x < 8; x++) if (Mathf.Abs(x - 4f) < 3.5f - Mathf.Abs(y - 5.5f) * 0.25f) c.Set(x, y, (y == 2 || y == 8) ? Pal.Shade(glow, 0.6f) : (x == 4 ? C(255, 250, 220) : glow));
            c.Rect(3, 0, 3, 2, Pal.Shade(glow, 0.5f));
            return c.Done();
        }

        // ------------------------------------------------------------------ the crimson forest
        static Sprite Bones(System.Random r)
        {
            var c = new Cv(16, 9);
            Color32 b = C(232, 222, 200), d = C(180, 168, 150);
            c.Line(1, 0, 13, 2, b, 1); c.Line(3, 2, 11, 0, d, 1);
            c.Disc(8, 4, 2.6f, b); c.Set(7, 4, C(30, 20, 20)); c.Set(9, 4, C(30, 20, 20)); c.Rect(7, 2, 3, 1, d);   // a skull
            if (r.Next(2) == 0) for (int k = 0; k < 3; k++) c.Line(12, 1 + k * 2, 15, 2 + k * 2, d);   // ribs
            return c.Done();
        }

        static Sprite Shroom(System.Random r, Color32 cap, Color32 dots, bool tall)
        {
            int H = tall ? 14 + r.Next(10) : 8 + r.Next(5), W = 14;
            var c = new Cv(W, H);
            int stemH = H - 5;
            c.Rect(6, 0, 2, stemH, C(236, 226, 210));
            for (int y = stemH - 1; y < H; y++) for (int x = 0; x < W; x++) { float u = (x - 6.5f) / 6.5f, v = (y - stemH + 1) / 5f; if (u * u + v * v * 1.4f <= 1f && y >= stemH - 1) c.Set(x, y, cap); }
            for (int k = 0; k < 3; k++) c.Set(3 + k * 4, stemH + 1 + (k % 2), dots);
            return c.Done();
        }

        static Sprite Thorns(System.Random r)
        {
            var c = new Cv(16, 12);
            Color32 b = C(90, 30, 40), t = C(200, 60, 70);
            for (int k = 0; k < 4; k++)
            {
                float a = Mathf.Lerp(150f, 30f, k / 3f) * Mathf.Deg2Rad;
                int L = 7 + r.Next(4);
                for (int j = 0; j < L; j++) c.Set(Mathf.RoundToInt(8 + Mathf.Cos(a) * j), Mathf.RoundToInt(Mathf.Sin(a) * j), j > L - 2 ? t : b);
            }
            return c.Done();
        }

        static Sprite Cocoon(System.Random r)
        {
            var c = new Cv(8, 14);
            for (int y = 0; y < 12; y++) for (int x = 0; x < 8; x++) { float u = (x - 3.5f) / 3.5f, v = (y - 6f) / 6f; if (u * u + v * v <= 1f) c.Set(x, y, (y % 3 == 0) ? C(170, 120, 120) : C(214, 170, 160)); }
            c.Line(4, 12, 4, 13, C(200, 190, 180));
            return c.Done();
        }

        // ------------------------------------------------------------------ the neon canopy
        static Sprite Flower(System.Random r)
        {
            var c = new Cv(14, 20);
            Color32 p = r.Next(2) == 0 ? C(255, 80, 220) : C(80, 255, 220), st = C(60, 200, 110);
            int h = 10 + r.Next(8);
            c.Line(7, 0, 7, h, st);
            c.Line(7, 3, 4, 6, st); c.Line(7, 5, 10, 8, st);
            for (int k = 0; k < 6; k++) { float a = k * 60f * Mathf.Deg2Rad; c.Disc(7 + Mathf.Cos(a) * 2.5f, h + Mathf.Sin(a) * 2.5f, 1.4f, p); }
            c.Set(7, h, C(255, 255, 210));
            return c.Done();
        }
    }

    // The decorations standing about the newer biomes: just pictures (nothing to bump into, not creatures), kept
    // out of the entity list so hundreds of them cost nothing. Tall plants sway; lanterns and neon things glow.
    public static class Decor
    {
        struct Item { public Transform t; public Vector2 pos; public bool sway, glow; public float phase; }
        static readonly List<Item> items = new List<Item>();

        public static void Reset() => items.Clear();

        public static void Add(Vector2 at, Sprite s, int order, bool sway, bool glow, bool flip)
        {
            var g = Game.I;
            if (g == null || g.runRoot == null) return;
            var go = new GameObject("Decor");
            go.transform.SetParent(g.runRoot, false);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var sr = Art.AddSR(go, s, order);
            sr.flipX = flip;
            items.Add(new Item { t = go.transform, pos = at, sway = sway, glow = glow, phase = Random.value * 6f });
        }

        // the swaying ones near the view rock gently; the glowing ones in view light the dark
        public static void Tick(Vector2 cam, float hw, float hh, Lighting l)
        {
            float time = Time.time;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.t == null) continue;
                if (Mathf.Abs(it.pos.x - cam.x) > hw + 4f || Mathf.Abs(it.pos.y - cam.y) > hh + 6f) continue;
                if (it.sway) it.t.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.3f + it.phase) * 3f);
                if (it.glow && l != null) l.Add(it.pos + Vector2.up * 0.6f, 2.2f + Mathf.Sin(time * 2f + it.phase) * 0.2f, 0.55f);
            }
        }
    }
}
