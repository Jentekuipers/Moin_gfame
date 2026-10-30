using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Guns, blades, bullets, the slash arc and the new enemies. All drawn pointing right.
    public static partial class Art
    {
        public static readonly Dictionary<string, Sprite> Bullets = new Dictionary<string, Sprite>();
        public static Sprite SlashArc, Glow;

        static void InitGuns()
        {
            Color32 gold = U.C(250, 205, 60), dgold = U.C(170, 120, 30);

            // ---------------------------------------------------------------- guns
            string[] revolver =
            {
                "gGGGGGGGGGGW.",
                "gggGGg.......",
                ".bbgg........",
                ".bb..........",
                ".bb..........",
            };
            Icon("revolver", revolver, pal);
            Icon("goldgun", revolver, Swap(pal, ('G', gold), ('g', dgold), ('W', U.C(255, 250, 200)), ('b', U.C(110, 40, 40))));
            Icon("smg", new[]
            {
                "GGGGGGGGGGW.",
                "gggggggGg...",
                ".bb.gg......",
                ".bb.gg......",
                "....g.......",
            }, pal);
            Icon("shotgun", new[]
            {
                "bbbbbGGGGGGGGGGW",
                "bbbbggggggggggg.",
                "..bb..bbb.......",
                "..b.............",
            }, pal);
            Icon("rifle", new[]
            {
                "....g...........",
                "bbbGGGGGGGGGGGGW",
                "bbbggggGgggg....",
                "..bb.gg.g.......",
                "..b..gg.........",
            }, pal);
            Icon("sniper", new[]
            {
                "......DDDD........",
                ".....g....g.......",
                "bbbbGGGGGGGGGGGGGW",
                "bbbbggggggg.......",
                "..bb.g............",
            }, pal);
            Icon("minigun", new[]
            {
                "..GGGGGGGGGGGGW.",
                "gGgggggggggggg..",
                "gGGGGGGGGGGGGGW.",
                "gGgggggggggggg..",
                "gGGGGGGGGGGGGGW.",
                ".bb.OO..........",
                ".bb.............",
            }, pal);
            Icon("boltcaster", new[]
            {
                "....b.........",
                "....bb........",
                "....b.b.......",
                "bbbbGGGGGGGGW.",
                "bbbbgggggg....",
                "....b.b.......",
                "....bb........",
                "....b.........",
            }, pal);
            Icon("laserpistol", new[]
            {
                "gggCCCCCCCW.",
                "ggggggg.....",
                ".gg.C.......",
                ".gg.........",
            }, pal);
            Icon("plasma", new[]
            {
                ".....PPP......",
                "....PuWuP.....",
                "gggPuWWuPGGGW.",
                "ggggPuuPggg...",
                ".gg..PP.......",
                ".gg...........",
            }, pal);
            // a proper grenade launcher: a fat, short tube with a hinge, and a wooden grip raked back
            Icon("grenade", new[]
            {
                "...gggggggggg.",
                "..GGGGGGGGGGGW",
                "DgGGGGGGGGGGGW",
                "DgGGGOOGGGGGGW",
                "..ggggggggggg.",
                "..bb.g........",
                ".bbb..........",
                ".bb...........",
            }, pal);
            Icon("rocket", new[]
            {
                "..vvvvvvvvvvvvv...",
                "vVVVVVVVVVVVVVVVOW",
                "vVVVVVVVVVVVVVVVOW",
                "..vvvvvvvvvvvvv...",
                ".....bb..g........",
                ".....bb...........",
            }, pal);
            Icon("flamer", new[]
            {
                "..OOOO..........",
                ".OFFFFO.........",
                ".OFFFFOggggggGGR",
                ".OFFFFOGGGGGGGG.",
                "..OOOO.bb.......",
                ".......bb.......",
            }, pal);
            Icon("disc", new[]
            {
                "...GGGG.......",
                "..GWWWWG......",
                "gggGGGGGgggGW.",
                "gggggggggg....",
                ".bb...........",
                ".bb...........",
            }, pal);
            Icon("railgun", new[]
            {
                "gCgCgCgCgCgCg.....",
                "GGGGGGGGGGGGGGGGCW",
                "gggggggggggggggg..",
                "gCgCgCgCgCgCg.....",
                "..bb..............",
                "..bb..............",
            }, pal);
            Icon("seeker", new[]
            {
                "..rrrrrrrrrr....",
                ".rRRRRRRRRRRRX..",
                ".rrrrrrrrrrrr...",
                ".rRRRRRRRRRRRX..",
                ".rrrrrrrrrrrr...",
                "..bb..g.........",
                "..bb............",
            }, pal);

            // ---------------------------------------------------------------- blades (drawn point-up)
            string[] sword =
            {
                "...W...",
                "..WGW..",
                "..WGg..",
                "..WGg..",
                "..WGg..",
                "..WGg..",
                "..WGg..",
                "..WGg..",
                "..WGg..",
                "..WGg..",
                "YYYYYYY",
                "...b...",
                "...b...",
                "...Y...",
            };
            Icon("sword", sword, pal);
            Icon("esword", sword, Swap(pal, ('W', U.C(200, 255, 255)), ('G', U.C(90, 230, 255)), ('g', U.C(40, 150, 220)), ('Y', U.C(120, 125, 140)), ('b', U.C(60, 62, 75))));

            Icon("superbomb", new[]
            {
                "........YO..",
                ".......K....",
                "......K.....",
                "...WWWWW....",
                "..WWWWWWW...",
                ".WWKKWKKWW..",
                ".WWKKWKKWW..",
                ".WWWWWWWWW..",
                ".WWWKWKWWW..",
                "..WWWWWWW...",
                "...WWWWW....",
            }, Swap(pal, ('W', U.C(255, 240, 210))));

            // ---------------------------------------------------------------- bullets (fat on purpose)
            Bullets["b_bullet"] = Make(new[] { "YYLLW.", "YLLLLW", "YYLLW." }, pal, true, true);
            Bullets["b_pellet"] = Make(new[] { "LLW", "LWW", "LLW" }, pal, true, true);
            Bullets["b_laser"] = Make(new[] { "CCCCCCCWW", "CCCCCCCWW" }, pal, true, true);
            Bullets["b_plasma"] = Make(new[] { "..PPP..", ".PuuuP.", "PuWWWuP", "PuWWWuP", "PuWWWuP", ".PuuuP.", "..PPP.." }, pal, true, true);
            Bullets["b_grenade"] = Make(new[] { ".vvv.", "vVVVv", "vVHVv", "vVVVv", ".vvv." }, pal, true, true);
            Bullets["b_rocket"] = Make(new[] { "O.ggggGW.", "OOggggGGW", "O.ggggGW." }, pal, true, true);
            Bullets["b_flame"] = Make(new[] { ".WWW.", "WWWWW", "WWWWW", "WWWWW", ".WWW." }, pal, false, true);
            // chunky flame puffs (Super Crate Box style): blocky clumps, no soft edges; tinted by age in Bullet
            Bullets["b_flame1"] = Make(new[] { "..WW...", ".WWWWW.", "WWWWWW.", "WWWWWWW", ".WWWWWW", ".WWWW..", "...W..." }, pal, false, true);
            Bullets["b_flame2"] = Make(new[] { ".WW.WW.", "WWWWWWW", "WWWWWW.", ".WWWWWW", "WWWWWW.", ".WW.WW." }, pal, false, true);
            Bullets["b_flame3"] = Make(new[] { "...WW..", "..WWWW.", "WWWWWWW", "WWWWWWW", ".WWWWW.", "..W.WW." }, pal, false, true);
            Bullets["b_flame4"] = Make(new[] { ".W..", "WWWW", "WWW.", ".WW." }, pal, false, true);
            Bullets["b_disc"] = Make(new[] { "..GGG..", ".GWWWG.", "GWWGWWG", "GWGgGWG", "GWWGWWG", ".GWWWG.", "..GGG.." }, pal, true, true);
            Bullets["b_seeker"] = Make(new[] { "rRRX", "rRRX" }, pal, true, true);
            Bullets["b_enemy"] = Make(new[] { ".XXX.", "XWWOX", "XWWOX", "XOOOX", ".XXX." }, pal, true, true);
            Bullets["b_bolt"] = Bolt;
            Bullets["b_fire"] = Fireball;
            Bullets["b_spit"] = Spit;

            SlashArc = MakeArc();
            ThrustArc = MakeThrust();
            HeavyArc = MakeArc(7.5f, 75f);

            // ---------------------------------------------------------------- hand cannons, the buzz SMG, crossbows
            string[] eagle =
            {
                ".K..................",
                "KYKK......K.K.K.K.K.",
                ".KYKKKKKKKyKyKyKyKyK",
                "KWYYKYKYYYyYyYyYyYyK",
                ".KYYYYYYYYYYYYYYYYYK",
                ".KWYYyyYYYWWWWWWWYK.",
                ".KWWWYYWKKKKKKKKKK..",
                "..KDWWWY.K..........",
                ".KDDWDKKK...........",
                ".KDWWDK.............",
                ".KWWDK..............",
                "..KKKK..............",
            };
            // THE ONE: Jente's own golden hand cannon, pixel for pixel (it brings its own outline)
            var oneP = new Dictionary<char, Color32> { { 'K', U.C(24, 12, 34) }, { 'Y', U.C(250, 205, 60) }, { 'y', U.C(255, 250, 200) }, { 'W', U.C(170, 120, 30) }, { 'D', U.C(110, 40, 40) } };
            Icons["theone"] = Make(eagle, oneP, false, true);
            // the plain one: gunmetal grey, no shine on the slide
            var eagleP = new Dictionary<char, Color32> { { 'K', U.C(24, 12, 34) }, { 'Y', U.C(150, 156, 168) }, { 'y', U.C(150, 156, 168) }, { 'W', U.C(92, 96, 110) }, { 'D', U.C(74, 54, 44) } };
            Icons["deagle"] = Make(eagle, eagleP, false, true);
            Icon("uzi", new[]
            {
                ".....DDDD.......",
                "DDDDDDDDDDDDDGGW",
                "DgggggggggggD...",
                "..Dg..Dg........",
                "..Dg..Dg........",
                "......Dg........",
            }, pal);
            Icon("crossbow", new[]
            {
                "............G...",
                "............GG..",
                "bbbbbbbbbbbbGTTW",
                "bbbbbbbbbbbbG...",
                "bb..bb......GG..",
                "............G...",
            }, pal);
            Icon("autocrossbow", new[]
            {
                "....gggg....G...",
                "....gDDg....GG..",
                "DDDDDDDDDDDDGTTW",
                "DgggggggggggG...",
                "Dg..Dg......GG..",
                "............G...",
            }, pal);
            Icon("supercrossbow", new[]
            {
                "..............GG..",
                "..............G.TW",
                "DDDDDDDDDDDDDDGGTW",
                "DggggggggggggDGGTW",
                "Dg...Dg.......G.TW",
                "..............GG..",
            }, pal);
            Icon("deepbow", new[]
            {
                "..................G..",
                ".....PPPP........GGG.",
                "DDDDDDDDDDDDDDDDGG.uW",
                "DgPgPgPgPgPgggggGGuuW",
                "DDDDDDDDDDDDDDDDGG.uW",
                "Dg....Dg.........GGG.",
                "..................G..",
            }, pal);
            Glow = MakeGlow();

            // ---------------------------------------------------------------- raiders (surface treasure hunters with guns)
            string[] raiderA =
            {
                "......bbbb......",
                ".....bbbbbb.....",
                "....bbSSSSbb....",
                "....bGGSGGSb....",
                "....bSSSSSSb....",
                ".....SSSSSS.....",
                "....RRRRRRRR....",
                "...TTTTTTTTTT...",
                "...TTTTTTTTTT...",
                "...STTTTTTTTS...",
                "....TTTTTTTT....",
                "....gggggggg....",
                "....gg....gg....",
                "....KK....KK....",
            };
            string[] raiderB = (string[])raiderA.Clone();
            raiderB[12] = ".....gg..gg.....";
            raiderB[13] = "...KK......KK...";
            AddEnemy("raider", false, Swap(pal, ('T', U.C(150, 100, 60)), ('R', U.C(200, 50, 45)), ('G', U.C(120, 220, 255))), raiderA, raiderB);
            AddEnemy("brute", false, Swap(pal, ('T', U.C(110, 112, 128)), ('R', U.C(240, 140, 40)), ('b', U.C(70, 70, 80)), ('G', U.C(255, 90, 60))), raiderA, raiderB);
            AddEnemy("marksman", false, Swap(pal, ('T', U.C(70, 110, 60)), ('R', U.C(40, 60, 35)), ('b', U.C(50, 80, 45)), ('G', U.C(255, 60, 60))), raiderA, raiderB);
            AddEnemy("bomber", false, Swap(pal, ('T', U.C(200, 110, 40)), ('R', U.C(250, 220, 70)), ('G', U.C(60, 60, 70))), raiderA, raiderB);

            // ---------------------------------------------------------------- the raider captain (boss)
            AddEnemy("captain", false, Swap(pal, ('T', U.C(190, 145, 50)), ('R', U.C(170, 30, 30)), ('b', U.C(30, 28, 34)), ('G', U.C(255, 70, 50)), ('S', U.C(225, 175, 135))), raiderA, raiderB);

            // ---------------------------------------------------------------- cave worm (head + body segment)
            var wormPal = Swap(pal, ('R', U.C(205, 115, 115)), ('r', U.C(140, 70, 78)), ('N', U.C(250, 180, 175)));
            AddEnemy("wormhead", true, wormPal, new[]
            {
                "...rrrrr....",
                "..rRRRRRrr..",
                ".rRRNNRRRWr.",
                "rRRNRRRRWKWr",
                "rRRRRRRRRRK.",
                "rRRRRRRRWKWr",
                ".rRRRRRRRWr.",
                "..rRRRRRrr..",
                "...rrrrr....",
            });
            AddEnemy("wormseg", true, wormPal, new[]
            {
                "...rrrr...",
                ".rrRRRRrr.",
                ".rRRNNRRr.",
                "rRRNRRNRRr",
                "rRRRRRRRRr",
                "rRRRRRRRRr",
                ".rRRRRRRr.",
                ".rrRRRRrr.",
                "...rrrr...",
            });

            // ---------------------------------------------------------------- the ash wraith (boss) and its skulls
            string[] wrA =
            {
                "....PPPPPP......",
                "...PPPPPPPP.....",
                "..PPXPPPXPPP....",
                "..PPXPPPXPPP....",
                "..PPPPPPPPPP....",
                "..PPPKKKKPPP....",
                "..PPPPPPPPPPP...",
                "...PPPPPPPPPPP..",
                "...PPPPPPPPPPPP.",
                "....PPP.PPP.PPP.",
                "....PP...PP..P..",
                "....P.....P.....",
            };
            string[] wrB = (string[])wrA.Clone();
            wrB[9] = "....PPP.PPP..PP.";
            wrB[10] = ".....P..PP...P..";
            wrB[11] = "........P.......";
            AddEnemy("wraith", true, Swap(pal, ('P', U.C(80, 70, 82)), ('X', U.C(255, 140, 40))), wrA, wrB);
            AddEnemy("skull", true, Swap(pal, ('W', U.C(235, 225, 205)), ('X', U.C(255, 110, 40))), new[]
            {
                ".WWWWW.",
                "WWWWWWW",
                "WKKWKKW",
                "WKXWKXW",
                "WWWKWWW",
                ".WWWWW.",
                ".W.W.W.",
            });

            // ---------------------------------------------------------------- water slime (crawler shape, made of water)
            {
                string[] slA =
                {
                    "......CCCC......",
                    "....CCWWCCCC....",
                    "...CCWWCCCCCCY..",
                    "..CCCCCCCCCCCYY.",
                    "..BBBBBBBBBBBBB.",
                    "...B.B...B.B....",
                    "..B..B..B..B....",
                };
                string[] slB = (string[])slA.Clone();
                slB[5] = "..B.B...B.B.....";
                slB[6] = "...B..B...B.B...";
                AddEnemy("slime", false, Swap(pal, ('C', U.C(110, 190, 255)), ('B', U.C(50, 110, 210)), ('Y', U.C(230, 250, 255))), slA, slB);
            }

            // ---------------------------------------------------------------- mirror beetle
            string[] beetleA =
            {
                ".....CCCCCC.....",
                "...CCWWCCCCCC...",
                "..CCWCCCCCCCCC..",
                ".CCCCCCCCCCCCCC.",
                ".BBBBBBBBBBBBBBY",
                "..BBBBBBBBBBBEB.",
                "..g.g.g..g.g.g..",
                ".g..g..g..g..g..",
            };
            string[] beetleB = (string[])beetleA.Clone();
            beetleB[6] = "...g.g.g..g.g.g.";
            beetleB[7] = "..g..g..g..g..g.";
            AddEnemy("beetle", false, Swap(pal, ('C', U.C(150, 230, 240)), ('B', U.C(50, 90, 130)), ('E', U.C(255, 240, 120))), beetleA, beetleB);
        }

        // Soft round light, 2 world units across: drawn above the darkness to tint the world (weapon glows).
        static Sprite MakeGlow()
        {
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            float c = N * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
        }

        // A crescent swoosh, 2 world units across, opening to the right.
        public static Sprite ThrustArc, HeavyArc;

        // the dagger's stab: a thin arrow-shaped streak, out from the hand to a point
        static Sprite MakeThrust()
        {
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            int c = N / 2;
            for (int x = c + 3; x < N - 1; x++)
            {
                int tipIn = N - 2 - x;   // the arrowhead: wider just behind the point
                int half = x > N - 9 ? Mathf.Min(3, tipIn) : 0;
                for (int dy = -half; dy <= half; dy++)
                    px[(c + dy) * N + x] = new Color32(255, 255, 255, (byte)(dy == 0 ? 255 : 210));
                if (x < N - 9) px[(c - 1) * N + x] = new Color32(255, 255, 255, (byte)(120 + (x - c) * 8));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
        }

        static Sprite MakeArc() => MakeArc(4.5f, 70f);
        static Sprite MakeArc(float thick, float deg)
        {
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            float c = N * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (Mathf.Abs(a) > deg) continue;
                    // thick in the middle of the sweep, thin at the tips
                    float th = Mathf.Lerp(thick, 1f, Mathf.Abs(a) / deg);
                    if (d > 15f || d < 15f - th) continue;
                    byte al = (byte)(d > 14f ? 255 : 200);
                    px[y * N + x] = new Color32(255, 255, 255, al);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
        }
    }
}
