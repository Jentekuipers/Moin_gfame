using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // All sprites are built at runtime from tiny character maps, so the project needs no imported assets.
    public static partial class Art
    {
        public const int PPU = 16;
        public static Material SpriteMat;
        public static Texture2D White;
        public static Sprite Pixel, Square, Circle, Ring;
        public static Sprite PlayerIdle, PlayerRun1, PlayerRun2, PlayerJump, PlayerDig, Keeper;
        public static Sprite Coin, BigCoin, GemDrop, Fireball, Spit, Bolt, Pedestal;
        public static readonly Dictionary<string, Sprite[]> Enemy = new Dictionary<string, Sprite[]>();
        public static readonly Dictionary<string, Sprite[]> EnemyWhite = new Dictionary<string, Sprite[]>();
        public static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();
        static readonly Dictionary<int, Sprite> tileSprites = new Dictionary<int, Sprite>();
        static Dictionary<char, Color32> pal;
        static bool inited;

        public static void Init()
        {
            if (inited) return;
            inited = true;

            var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh != null) SpriteMat = new Material(sh);

            White = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            White.SetPixel(0, 0, Color.white); White.Apply();

            Pixel = Solid(1, 1, new Vector2(0.5f, 0.5f));
            Square = Solid(16, 16, new Vector2(0.5f, 0.5f));
            Circle = Disc(32, false);
            Ring = Disc(32, true);

            pal = new Dictionary<char, Color32>
            {
                {'K', U.C(22,16,20)}, {'Y', U.C(255,206,60)}, {'L', U.C(255,255,190)}, {'S', U.C(240,184,140)},
                {'E', U.C(20,10,10)}, {'W', U.C(235,232,220)}, {'R', U.C(200,40,40)}, {'r', U.C(130,20,25)},
                {'B', U.C(60,80,190)}, {'b', U.C(120,72,34)}, {'G', U.C(175,180,192)}, {'g', U.C(95,98,112)},
                {'O', U.C(240,150,30)}, {'D', U.C(64,62,74)}, {'C', U.C(90,220,255)}, {'P', U.C(140,84,170)},
                {'X', U.C(255,70,70)}, {'M', U.C(230,100,60)}, {'N', U.C(255,170,140)}, {'F', U.C(255,120,30)},
                {'H', U.C(200,255,160)}, {'V', U.C(90,190,70)}, {'v', U.C(40,110,40)}, {'U', U.C(200,60,140)},
                {'u', U.C(255,170,220)}, {'T', U.C(225,210,180)}, {'Q', U.C(210,170,110)},
            };

            // ---------- player ----------
            string[] upper =
            {
                "................",
                "......YYYY......",
                ".....YYYYYYL....",
                "....KKKKKKKKK...",
                ".....SSSSSES....",
                ".....SSSSSSS....",
                "....WWWWWWWW....",
                "....RWWWWWWR....",
                "...RRRWWWWRRR...",
                "...SRRRRRRRRS...",
                "....bbbbbbbb....",
            };
            string[] upperDig = (string[])upper.Clone();
            upperDig[8] = "...RRRWWWWRRRSS.";
            upperDig[9] = "....RRRRRRRR....";
            string[] legsIdle = { ".....BBBBBB.....", ".....BB..BB.....", ".....KK..KK....." };
            string[] legsRun1 = { ".....BBBBBB.....", "....BB....BB....", "....KK....KK...." };
            string[] legsRun2 = { ".....BBBBBB.....", "......BBBB......", "......KKKK......" };
            string[] legsJump = { ".....BBBBBB.....", "....BB..BB......", "...KK....KK....." };
            PlayerIdle = Make(Cat(upper, legsIdle), pal);
            PlayerRun1 = Make(Cat(upper, legsRun1), pal);
            PlayerRun2 = Make(Cat(upper, legsRun2), pal);
            PlayerJump = Make(Cat(upper, legsJump), pal);
            PlayerDig = Make(Cat(upperDig, legsIdle), pal);
            heroMaps = new[] { Cat(upper, legsIdle), Cat(upper, legsRun1), Cat(upper, legsRun2), Cat(upper, legsJump), Cat(upperDig, legsIdle),
                Cat(upper, new[] { ".....BBBBBBBBB..", "...........KK..." }) };   // [5] = sitting
            Keeper = Make(Cat(upper, legsIdle),Swap(pal, ('Y', U.C(70, 170, 90)), ('R', U.C(120, 60, 170)), ('L', U.C(255, 240, 150)), ('W', U.C(190, 190, 200))));

            // ---------- enemies ----------
            string[] crawlA =
            {
                "......VVVV......",
                "....VVHHVVVV....",
                "...VVHHVVVVVVY..",
                "..VVVVVVVVVVVYY.",
                "..vvvvvvvvvvvvv.",
                "...v.v...v.v....",
                "..v..v..v..v....",
            };
            string[] crawlB = (string[])crawlA.Clone();
            crawlB[5] = "..v.v...v.v.....";
            crawlB[6] = "...v..v...v.v...";
            AddEnemy("crawler", false, pal, crawlA, crawlB);
            AddEnemy("golem", false, Swap(pal, ('V', U.C(128, 118, 108)), ('H', U.C(185, 175, 165)), ('v', U.C(78, 70, 64)), ('Y', U.C(255, 90, 40))), crawlA, crawlB);

            string[] batA =
            {
                "P......PP......P",
                "PP....PPPP....PP",
                "PPP..PPPPPP..PPP",
                "PPPPPPXPPXPPPPPP",
                ".PPPPPPPPPPPPPP.",
                "..PP..PPPP..PP..",
                "......P..P......",
                "................",
            };
            string[] batB =
            {
                "................",
                ".......PP.......",
                "......PPPP......",
                "..PPPPXPPXPPPP..",
                ".PPPPPPPPPPPPPP.",
                "PPP...PPPP...PPP",
                "PP....P..P....PP",
                "P..............P",
            };
            AddEnemy("bat", true, pal, batA, batB);

            string[] impA =
            {
                "....F......F....",
                "....FF....FF....",
                ".....FFFFFF.....",
                "....FFFFFFFF....",
                "...FFYFFFFYFF...",
                "...FFFFFFFFFF...",
                "....FFFKKFFF....",
                ".....FFFFFF.....",
                "......FFFF......",
                ".......FF.......",
            };
            string[] impB = (string[])impA.Clone();
            impB[0] = "...F........F...";
            impB[9] = "......FF........";
            AddEnemy("imp", true, pal, impA, impB);

            string[] moleA =
            {
                "....MMMMMMM.....",
                "...MMMMMMMMM....",
                "..MMWWWMWWWMM...",
                "..MMWEWMWEWMM...",
                "..MMWWWMWWWMMM..",
                "..MMMMMMMMMMMNN.",
                "..MMMMMMMMMMMNN.",
                "..MMMMMMMMMMMM..",
                "...MMMMMMMMMM...",
                "....KK....KK....",
            };
            string[] moleB = (string[])moleA.Clone();
            moleB[9] = "...KK......KK...";
            AddEnemy("mole", true, pal, moleA, moleB);

            string[] spitA =
            {
                "....UUUUUUUU....",
                "..UUUuUUUUuUUU..",
                ".UUUUUUUUUUUUUU.",
                ".UuuUUUUuuUUUUU.",
                ".UUUUUUUUUUUUUU.",
                "..TTTTTTTTTTTT..",
                "......TTTT......",
                ".....TXTTXT.....",
                ".....TTTTTT.....",
                ".....TTTTTT.....",
                "....TTTTTTTT....",
            };
            string[] spitB = (string[])spitA.Clone();
            spitB[0] = "...UUUUUUUUUU...";
            AddEnemy("spitter", false, pal, spitA, spitB);

            // ---------- small things ----------
            Coin = Make(new[] { ".YYYY.", "YYWYYY", "YWYYYO", "YYYYYO", "YYYYOO", ".OOOO." }, pal, true, true);
            BigCoin = Make(new[] { "..YYYY..", ".YYWWYY.", "YYWYYYYO", "YWYYYYYO", "YYYYYYYO", "YYYYYYOO", ".YYOOOO.", "..OOOO.." }, pal, true, true);
            GemDrop = Make(new[] { "..CC..", ".CWCC.", "CCCCCC", ".CCCC.", "..CC.." }, pal, true, true);
            string[] fire = { "..YY..", ".YWWY.", "YWWWOY", "YOWWOY", ".YOOY.", "..YY.." };
            Fireball = Make(fire, pal, true, true);
            Spit = Make(fire, Swap(pal, ('Y', U.C(140, 230, 90)), ('O', U.C(60, 140, 40)), ('W', U.C(220, 255, 190))), true, true);
            Bolt = Make(new[] { "X.........", "bbbbbbbbGG", "X........." }, pal, true, true);
            Pedestal = Make(new[] { "..GGGGGGGGGG..", "...gGGGGGGg...", "....gGGGGg....", "....gGGGGg....", "..gggggggggg.." }, pal);

            // ---------- icons (12x12) ----------
            string[] pick =
            {
                "....KKKK....",
                "..KGGGGGGK..",
                ".KGGK..KGGK.",
                "KGK..bb..KGK",
                "KK...bb...KK",
                ".....bb.....",
                ".....bb.....",
                ".....bb.....",
                ".....bb.....",
                ".....bb.....",
                ".....bb.....",
                "............",
            };
            Icon("pickaxe", pick, pal);
            Icon("twin", pick, Swap(pal, ('G', U.C(255, 200, 60))));
            Icon("gemcutter", pick, Swap(pal, ('G', U.C(90, 230, 255))));
            Icon("mattock", pick, Swap(pal, ('G', U.C(190, 110, 255))));
            Icon("hammer", new[]
            {
                "............",
                ".KKKKKKKKKK.",
                ".KGGGGGGGGK.",
                ".KGWGGGGGGK.",
                ".KGGGGGGGGK.",
                ".KKKKbbKKKK.",
                ".....bb.....",
                ".....bb.....",
                ".....bb.....",
                ".....bb.....",
                ".....bb.....",
                "............",
            }, pal);
            string[] drill =
            {
                "............",
                "............",
                "KK..........",
                "KbKGG.......",
                "KbbGWGGG....",
                "KbbGGGGGGGG.",
                "KbbGGGGGGGG.",
                "KbbGWGGG....",
                "KbKGG.......",
                "KK..........",
                "............",
                "............",
            };
            Icon("drill", drill, pal);
            Icon("dash", drill, Swap(pal, ('G', U.C(255, 150, 50)), ('W', U.C(255, 230, 150))));
            Icon("bore", RotateCW(drill), Swap(pal, ('G', U.C(190, 110, 255)), ('W', U.C(240, 210, 255))));
            Icon("dynamite", new[]
            {
                "........Y...",
                ".......YO...",
                "......K.....",
                ".....K......",
                "...RRRRR....",
                "...RWRRR....",
                "...RWRRR....",
                "...RRRRR....",
                "...KKKKK....",
                "...RRRRR....",
                "...RRRRR....",
                "...RRRRR....",
            }, pal);
            Icon("crossbow", new[]
            {
                "............",
                "..K.........",
                "..KbK.......",
                "..K.bK......",
                "..K..bK.....",
                "GGGGGGGGGGW.",
                "..K..bK.....",
                "..K.bK......",
                "..KbK.......",
                "..K.........",
                "............",
                "............",
            }, pal);
            Icon("pound", new[]
            {
                "............",
                "...bbbbb....",
                "...bbbbb....",
                "...bbbbb....",
                "...bbbbb....",
                "...bbbbbb...",
                "...bbbbbbbb.",
                "...bbbbbbbbb",
                "...KKKKKKKKK",
                "............",
                "..Y..Y..Y...",
                ".Y...Y...Y..",
            }, pal);
            Icon("shock", new[]
            {
                "...CCCCCC...",
                "..C......C..",
                ".C..CCCC..C.",
                "C..C....C..C",
                "C.C..WW..C.C",
                "C.C.WWWW.C.C",
                "C.C.WWWW.C.C",
                "C.C..WW..C.C",
                "C..C....C..C",
                ".C..CCCC..C.",
                "..C......C..",
                "...CCCCCC...",
            }, pal);
            Icon("grapple", new[]
            {
                "..GGGGGG....",
                ".G......G...",
                "G..GGG...G..",
                "G.G...G..G..",
                ".G....G.G...",
                "......GG....",
                ".....b......",
                "....b.......",
                "...b........",
                "..b.........",
                ".b..........",
                "b...........",
            }, pal);
            Icon("bomb", new[]
            {
                "........YO..",
                ".......K....",
                "......K.....",
                "...DDDDD....",
                "..DDDDDDD...",
                ".DDWDDDDDD..",
                ".DDWDDDDDD..",
                ".DDDDDDDDD..",
                ".DDDDDDDDD..",
                "..DDDDDDD...",
                "...DDDDD....",
                "............",
            }, pal);
            Icon("ale", new[]
            {
                "............",
                "..WWWWWW....",
                ".WWWWWWWW...",
                ".bOOOOOOb...",
                ".bOOOOOObbb.",
                ".bOOOOOOb.b.",
                ".bOOOOOOb.b.",
                ".bOOOOOObbb.",
                ".bOOOOOOb...",
                ".bbbbbbbb...",
                "............",
                "............",
            }, pal);
            Icon("flare", new[]
            {
                ".....Y......",
                "....YOY.....",
                "....OWO.....",
                ".....R......",
                ".....R......",
                ".....R......",
                ".....R......",
                ".....R......",
                ".....R......",
                ".....K......",
                "............",
                "............",
            }, pal);
            Icon("heart", new[]
            {
                "............",
                "..RR....RR..",
                ".RRRR..RRRR.",
                ".RWRRRRRRRR.",
                ".RWRRRRRRRR.",
                ".RRRRRRRRRR.",
                "..RRRRRRRR..",
                "...RRRRRR...",
                "....RRRR....",
                ".....RR.....",
                "............",
                "............",
            }, pal);
            Icon("backpack", new[]
            {
                "....bbbb....",
                "...b....b...",
                "..bbbbbbbb..",
                ".bQQbbbbQQb.",
                ".bQQQQQQQQb.",
                ".bQQQbbQQQb.",
                ".bQQQbbQQQb.",
                ".bQQQQQQQQb.",
                ".bQQQQQQQQb.",
                ".bbbbbbbbbb.",
                "............",
                "............",
            }, pal);
            Icon("coin", new[] { ".YYYY.", "YYWYYY", "YWYYYO", "YYYYYO", "YYYYOO", ".OOOO." }, pal);

            InitExtra();
            InitGuns();
            InitPlants();
            InitStorm();
            InitHeroes();
            InitLooks();
        }

        // ------------------------------------------------------------------ helpers

        public static SpriteRenderer AddSR(GameObject go, Sprite s, int order)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            if (SpriteMat != null) sr.sharedMaterial = SpriteMat;
            return sr;
        }

        static string[] Cat(string[] a, string[] b)
        {
            var r = new string[a.Length + b.Length];
            a.CopyTo(r, 0); b.CopyTo(r, a.Length);
            return r;
        }

        static string[] RotateCW(string[] m)
        {
            int h = m.Length, w = m[0].Length;
            var r = new string[w];
            for (int row = 0; row < w; row++)
            {
                var chars = new char[h];
                for (int col = 0; col < h; col++)
                {
                    string src = m[h - 1 - col];
                    chars[col] = row < src.Length ? src[row] : '.';
                }
                r[row] = new string(chars);
            }
            return r;
        }

        static Dictionary<char, Color32> Swap(Dictionary<char, Color32> p, params (char, Color32)[] s)
        {
            var d = new Dictionary<char, Color32>(p);
            foreach (var kv in s) d[kv.Item1] = kv.Item2;
            return d;
        }

        static void Icon(string key, string[] map, Dictionary<char, Color32> p) => Icons[key] = Make(map, p, true, true);

        static void AddEnemy(string key, bool center, Dictionary<char, Color32> p, params string[][] frames)
        {
            var s = new Sprite[frames.Length];
            var w = new Sprite[frames.Length];
            for (int i = 0; i < frames.Length; i++) { s[i] = Make(frames[i], p, true, center); w[i] = Silhouette(s[i]); }
            Enemy[key] = s; EnemyWhite[key] = w;
        }

        public static Sprite Make(string[] map, Dictionary<char, Color32> p, bool outline = true, bool center = false)
        {
            int h = map.Length, w = 0;
            foreach (var r in map) w = Mathf.Max(w, r.Length);
            int tw = w + 2, th = h + 2;
            var px = new Color32[tw * th];
            var solid = new bool[tw * th];
            int lowRow = th;
            for (int r = 0; r < h; r++)
                for (int c = 0; c < map[r].Length; c++)
                {
                    char ch = map[r][c];
                    if (ch == '.' || ch == ' ') continue;
                    if (!p.TryGetValue(ch, out var col)) col = new Color32(255, 0, 255, 255);
                    int ty = h - r, tx = c + 1;
                    px[ty * tw + tx] = col;
                    solid[ty * tw + tx] = true;
                    lowRow = Mathf.Min(lowRow, ty);
                }
            if (outline && w >= 6 && h >= 6)
            {
                // automatic volume: the top pixel of every surface catches warm light, the bottom one sinks
                // into a violet shadow (hue-shifted ramps), so every sprite reads crisp and solid
                var shaded = (Color32[])px.Clone();
                for (int y = 1; y < th - 1; y++)
                    for (int x = 1; x < tw - 1; x++)
                    {
                        int i = y * tw + x;
                        if (!solid[i]) continue;
                        var c = px[i];
                        if (c.r + c.g + c.b < 60) continue;   // leave near-black details alone
                        bool openUp = !solid[i + tw], openDown = !solid[i - tw];
                        if (openUp && !openDown) shaded[i] = Pal.Ramp(c, 3);
                        else if (openDown && !openUp) shaded[i] = Pal.Ramp(c, 1);
                    }
                px = shaded;
            }
            if (outline)
            {
                var oc = new Color32(24, 12, 34, 255);   // deep violet outline instead of flat black
                for (int y = 0; y < th; y++)
                    for (int x = 0; x < tw; x++)
                    {
                        if (solid[y * tw + x]) continue;
                        bool n = (x > 0 && solid[y * tw + x - 1]) || (x < tw - 1 && solid[y * tw + x + 1]) ||
                                 (y > 0 && solid[(y - 1) * tw + x]) || (y < th - 1 && solid[(y + 1) * tw + x]);
                        if (n) px[y * tw + x] = oc;
                    }
            }
            if (lowRow == th) lowRow = 1;
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            Vector2 pivot = center ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, lowRow / (float)th);
            return Sprite.Create(tex, new Rect(0, 0, tw, th), pivot, PPU, 0, SpriteMeshType.FullRect);
        }

        public static Sprite Silhouette(Sprite s)
        {
            var src = s.texture;
            var px = src.GetPixels32();
            for (int i = 0; i < px.Length; i++)
                if (px[i].a > 0) px[i] = new Color32(255, 255, 255, px[i].a);
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            var pv = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pv, PPU, 0, SpriteMeshType.FullRect);
        }

        static Sprite Solid(int w, int h, Vector2 pivot)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, PPU, 0, SpriteMeshType.FullRect);
        }

        static Sprite Disc(int size, bool ring)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    bool on = ring ? (d <= r && d >= r - 2.5f) : d <= r;
                    px[y * size + x] = on ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px); tex.Apply();
            // PPU = size so the disc is exactly 1 world unit across
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        }

        // Small rune-stone icon with a letter, used for passive skills.
        public static Sprite Rune(Color32 col, char letter)
        {
            var map = new char[12, 12];
            for (int y = 0; y < 12; y++) for (int x = 0; x < 12; x++) map[y, x] = '.';
            for (int y = 1; y < 11; y++)
                for (int x = 1; x < 11; x++)
                {
                    bool corner = (x == 1 || x == 10) && (y == 1 || y == 10);
                    if (corner) continue;
                    bool edge = x == 1 || x == 10 || y == 1 || y == 10;
                    map[y, x] = edge ? 'a' : 'b';
                }
            var g = PixelFont.Glyph(letter);
            for (int r = 0; r < 7; r++)
                for (int c = 0; c < 5; c++)
                    if (g[r][c] == '#') map[r + 2, c + 3] = 'c';
            var rows = new string[12];
            for (int y = 0; y < 12; y++)
            {
                var ch = new char[12];
                for (int x = 0; x < 12; x++) ch[x] = map[y, x];
                rows[y] = new string(ch);
            }
            var p = new Dictionary<char, Color32> { { 'a', U.Mul(col, 0.55f) }, { 'b', col }, { 'c', U.C(255, 255, 240) } };
            return Make(rows, p, true, true);
        }

        // Sprite of a single block, used for falling boulders / lit fuses.
        // a tile with cracks drawn in (level 1..5: how damaged it is), for loose boulders that keep their cracks
        public static Sprite CrackedTileSprite(Block b, Biome bio, int level)
        {
            if (level <= 0) return TileSprite(b, bio);
            int key = 100000 + ((int)b * 16 + System.Array.IndexOf(Biomes.All, bio)) * 8 + level;
            if (tileSprites.TryGetValue(key, out var s)) return s;
            var buf = new Color32[16 * 16];
            TileDraw.clearBg = true;
            TileDraw.Draw(buf, 16, 0, 0, b, 3, -3, TileDraw.UP | TileDraw.DOWN | TileDraw.LEFT | TileDraw.RIGHT, level / 5f * 0.9f, bio);
            TileDraw.clearBg = false;
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(buf); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
            tileSprites[key] = s;
            return s;
        }

        public static Sprite TileSprite(Block b, Biome bio)
        {
            int key = (int)b * 16 + System.Array.IndexOf(Biomes.All, bio);
            if (tileSprites.TryGetValue(key, out var s)) return s;
            var buf = new Color32[16 * 16];
            TileDraw.clearBg = true;
            TileDraw.Draw(buf, 16, 0, 0, b, 3, -3, TileDraw.UP | TileDraw.DOWN | TileDraw.LEFT | TileDraw.RIGHT, 0f, bio);
            TileDraw.clearBg = false;
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(buf); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
            tileSprites[key] = s;
            return s;
        }
    }
}
