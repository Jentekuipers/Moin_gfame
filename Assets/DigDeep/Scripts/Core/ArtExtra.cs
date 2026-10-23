using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Trinket icon shapes (recoloured per trinket, Isaac-style) and altar / event sprites.
    public static partial class Art
    {
        public static Sprite PlateUp, PlateDown, Bucket, StatueRun, StatueSit, Flag, AltarBase, Arrow;
        public static Sprite CampLog, TentA, TentB;
        public static Sprite[] Flames;
        public static Sprite CampBarrel, CampCrates, CampLantern, CampPick, CampBedroll, CampCart, CampSign, CampWood, CampPot, CampShroom, CampShroomBlue, CampFlag;
        public static Sprite[] CampFire;
        static readonly Dictionary<string, string[]> shapes = new Dictionary<string, string[]>();
        static readonly Dictionary<string, Sprite> trinketCache = new Dictionary<string, Sprite>();

        // a = main colour, b = shade, c = highlight, K = outline-dark, W = white, R = red gem
        public static Sprite Trinket(string shape, Color32 col)
        {
            string key = shape + col.r + "," + col.g + "," + col.b;
            if (trinketCache.TryGetValue(key, out var s)) return s;
            if (!shapes.TryGetValue(shape, out var map)) map = shapes["orb"];
            var p = new Dictionary<char, Color32>
            {
                { 'a', col }, { 'b', U.Mul(col, 0.58f) }, { 'c', Color32.Lerp(col, new Color32(255, 255, 255, 255), 0.55f) },
                { 'K', U.C(22, 16, 20) }, { 'W', U.C(240, 240, 235) }, { 'R', U.C(230, 40, 50) },
            };
            s = Make(map, p, true, true);
            trinketCache[key] = s;
            return s;
        }

        static void Shape(string k, params string[] rows) => shapes[k] = rows;

        static string[][] heroMaps;

        // Palette-swapped player sprites for each playable character: idle, run1, run2, jump, dig.
        // Half-dwarf, half-animal: replaces the 6 head rows of the dwarf sprite with an animal head.
        public static Sprite[] HeroAnimal(string[] head, params (char, Color32)[] swaps)
        {
            var p = Swap(pal, swaps);
            var r = new Sprite[heroMaps.Length];
            for (int i = 0; i < r.Length; i++)
            {
                var m = (string[])heroMaps[i].Clone();
                for (int j = 0; j < head.Length && j < m.Length; j++) m[j] = head[j];
                r[i] = Make(m, p);
            }
            return r;
        }

        public static Sprite[] Hero(params (char, Color32)[] swaps)
        {
            var p = Swap(pal, swaps);
            var r = new Sprite[heroMaps.Length];
            for (int i = 0; i < r.Length; i++) r[i] = Make(heroMaps[i], p);
            return r;
        }

        static void InitExtra()
        {
            Shape("heart",
                "..........", ".aa....aa.", "acca..aaab", "acaaaaaaab", "aaaaaaaaab",
                ".aaaaaaab.", "..aaaaab..", "...aaab...", "....ab....", "..........");
            Shape("fist",
                "..........", "..a.a.a...", ".aaaaaaa..", ".acaaaaab.", ".aaaaaaab.",
                ".aaaaaaab.", "..aaaaab..", "..bbbbbb..", "..cccccc..", "..bbbbbb..");
            Shape("boot",
                "...aaaa...", "...acaa...", "...acab...", "...aaab...", "...aaab...",
                "...aaaab..", "..aaaaaaa.", ".aaaaaaaab", ".bbbbbbbbb", "..........");
            Shape("potion",
                "....KK....", "....cc....", "....ab....", "...aaab...", "..acaaab..",
                ".acaaaaab.", ".aaaaaaab.", ".aaaaaaab.", "..abbbbb..", "..........");
            Shape("gem",
                "..........", "...cccc...", "..caaaab..", ".caaaaaab.", "cccccbbbbb",
                ".aaaaaaab.", "..aaaaab..", "...aaab...", "....ab....", "..........");
            Shape("horseshoe",
                "..........", "..aaaaaa..", ".aaaaaaaa.", "aab....baa", "aa......aa",
                "aa......aa", "aa......aa", "WW......WW", "WW......WW", "..........");
            Shape("shield",
                ".aaaaaaaa.", "acccaaaaab", "acaaaaaaab", "acaaccaaab", "aaaaccaaab",
                ".aaaaaaab.", ".aaaaaaab.", "..aaaaab..", "...aaab...", "....ab....");
            Shape("coin",
                "..........", "...aaaa...", "..acccab..", ".acaaaaab.", ".acaccaab.",
                ".acaccaab.", ".aaaaaaab.", "..abbbbb..", "...bbbb...", "..........");
            Shape("eye",
                "..........", "..........", "...aaaa...", ".aaWWWWaa.", "aaWWKKWWaa",
                "aaWWKKWWaa", ".aaWWWWaa.", "...aaaa...", "..........", "..........");
            Shape("feather",
                ".......aa.", "......aca.", ".....acab.", "....acab..", "...acab...",
                "..acab....", "..aab.....", ".b........", "b.........", "..........");
            Shape("leaf",
                "......aaa.", "....aaacc.", "...aaaca..", "..aaacaa..", "..aacaab..",
                ".aacaab...", ".acabb....", ".cbb......", "c.........", "..........");
            Shape("wing",
                ".........a", ".......aca", ".....aacaa", "...aacaaab", ".aacaaaab.",
                "acaaaaab..", ".aaaaab...", "..aaab....", "...bb.....", "..........");
            Shape("hourglass",
                ".KKKKKKKK.", "..acccca..", "..aaaaab..", "...aaab...", "....cb....",
                "....cb....", "...a.cb...", "..a.cccb..", "..acccca..", ".KKKKKKKK.");
            Shape("bag",
                "....bb....", "...b..b...", "..bbbbbb..", ".aaaaaaab.", "acaaaaaaab",
                "acaaccaaab", "aaaaccaaab", "aaaaaaaaab", ".abbbbbbb.", "..........");
            Shape("orb",
                "...cccc...", "..caaaaa..", ".caWaaaab.", ".caaaaaab.", ".aaaaaaab.",
                ".aaaaaaab.", "..aaaaab..", "...bbbb...", "..KKKKKK..", "..........");
            Shape("ring",
                "....WW....", "...WccW...", "..aaaaaa..", ".ab....ba.", ".a......a.",
                ".a......a.", ".ab....ba.", "..aaaaaa..", "...bbbb...", "..........");
            Shape("blade",
                ".........c", "........cc", ".......cab", "......cab.", ".....cab..",
                "..K.cab...", "...Kab....", "...bK.....", "..b..K....", ".b........");
            Shape("rock",
                "..........", "...aaaa...", "..acaaab..", ".accaaaab.", ".acaaKaab.",
                ".aaaKKaab.", ".aaKaaaab.", "..aaaaab..", "...bbbb...", "..........");
            Shape("tooth",
                "..........", ".cccccccc.", "caaaaaaaab", "caaaaaaaab", ".aaaaaaab.",
                ".aaaaaaab.", ".aab.aab..", ".aab.aab..", "..b...b...", "..........");
            Shape("coil",
                "..cccccc..", ".......a..", "..aaaaaa..", "..a.......", "..aaaaaa..",
                ".......a..", "..aaaaaa..", "..a.......", "..bbbbbb..", "..........");
            Shape("skull",
                "..cccccc..", ".caaaaaaa.", "caaaaaaaab", "aaKKaaKKab", "aaKKaaKKab",
                "aaaaKKaaab", ".aaaaaaab.", "..aKaKab..", "..aaaaab..", "..........");
            Shape("star",
                "....cc....", "....ca....", "...caab...", "cccaaaabbb", ".aaaaaaab.",
                "..aaaaab..", "..aabaab..", ".aab..aab.", ".ab....ab.", "..........");
            Shape("crown",
                "..........", "a...a...a.", "ac.aca.ab.", "aaaaaaaab.", "aaRaaRaab.",
                "aaaaaaaab.", ".bbbbbbbb.", "..........");

            Shape("helmet",
                "..........", "...cccc...", "..caaaab..", ".caaaaaab.", ".aaaWWaab.",
                ".aaaWWaab.", "aaaaaaaaab", "bbbbbbbbbb", "..........", "..........");
            Shape("mushroom",
                "..aaaaaa..", ".acWaaWaa.", "acaaaaaaab", "aaWaaaWaab", "bbbbbbbbbb",
                "...WWWW...", "...WWWK...", "...WWWK...", "...WWKK...", "..........");

            // ---------- event sprites ----------
            AltarBase = Make(new[]
            {
                ".GGGGGGGGGGGGGG.",
                "GWGGGGGGGGGGGGGg",
                "gggggggggggggggg",
                "..gGGGGGGGGGGg..",
                "..gGgggggggGGg..",
                "..gGgYYYYYgGGg..",
                "..gGgggggggGGg..",
                ".gggggggggggggg.",
            }, pal);
            PlateUp = Make(new[]
            {
                "....RRRRRRRR....",
                "...RXXXXXXXXR...",
                ".gggggggggggggg.",
                "gggggggggggggggg",
            }, pal);
            PlateDown = Make(new[]
            {
                "...RRRRRRRRRR...",
                ".gggggggggggggg.",
                "gggggggggggggggg",
            }, pal);
            Bucket = Make(new[]
            {
                "YY..........YY",
                "YbYYYYYYYYYYbY",
                ".Yb........bY.",
                ".Yb........bY.",
                ".bQb......bQb.",
                ".bQb......bQb.",
                "..bQb....bQb..",
                "..bQQbbbbQQb..",
                "...bQQQQQQb...",
                "....bbbbbb....",
                "...gggggggg...",
            }, pal);
            StatueRun = Make(new[]
            {
                ".....gg.....",
                "....gGGg....",
                "....gGGg....",
                ".....gg.....",
                "...gGGGGg...",
                "..gGgGGgGGg.",
                ".gG.gGGg.gG.",
                "....gGGg....",
                "....gGGGg...",
                "...gGg.gGg..",
                "..gGg...gGg.",
                ".gGg.....gG.",
                ".gg.......g.",
                "gggggggggggg",
                "gGGGGGGGGGGg",
                "gggggggggggg",
            }, pal);
            StatueSit = Make(new[]
            {
                "......gg......",
                ".....gGGg.....",
                ".....gGGg.....",
                "......gg......",
                "....gGGGGg....",
                "...gGGGGGGg...",
                "..gGgGGGGgGg..",
                "..gG.gGGg.Gg..",
                "...gGGGGGGg...",
                ".gGGGGGGGGGGg.",
                "gGGGGGGGGGGGGg",
                "gggggggggggggg",
                "gGGGGGGGGGGGGg",
                "gggggggggggggg",
            }, pal);
            Flag = Make(new[]
            {
                "gWKWKWK...",
                "gKWKWKW...",
                "gWKWKWK...",
                "gKWKWKW...",
                "g.........",
                "g.........",
                "g.........",
                "g.........",
                "g.........",
                "g.........",
                "g.........",
                "g.........",
                "g.........",
                "g.........",
                "gg........",
                "gggg......",
            }, pal);
            Arrow = Make(new[]
            {
                "....W...",
                "....WW..",
                "WWWWWWW.",
                "WWWWWWWW",
                "WWWWWWW.",
                "....WW..",
                "....W...",
            }, pal, true, true);

            // ---------- starting camp ----------
            string[] fire1 =
            {
                ".....Y......",
                "....YY......",
                "....YOY.....",
                "...YOOY..Y..",
                "...YOWOY.YY.",
                "..YOWWOOYOY.",
                "..YOWWWOOOY.",
                "..YOOWWOOY..",
                "...YOOOOY...",
                "..bbYYYYbb..",
                ".bbbbbbbbbb.",
                "..bb....bb..",
            };
            string[] fire2 = (string[])fire1.Clone();
            fire2[0] = "......Y.....";
            fire2[1] = "..Y...YY....";
            fire2[2] = "..YY.YOY....";
            fire2[3] = "..YOYOOY.Y..";
            string[] fire3 = (string[])fire1.Clone();
            fire3[0] = "............";
            fire3[1] = ".....Y...Y..";
            fire3[2] = "....YOY..Y..";
            fire3[3] = "...YOOOYYOY.";
            CampFire = new[] { Make(fire1, pal), Make(fire2, pal), Make(fire3, pal) };
            CampLog = Make(new[]
            {
                ".bbbbbbbbbbbb.",
                "bQbbbbbbbbbbQb",
                "bbbbbbbbbbbbbb",
                ".bbbbbbbbbbbb.",
            }, pal);
            string[] tent =
            {
                "..........KK..........",
                ".........KTTK.........",
                "........KTTTTK........",
                ".......KTTTQTTK.......",
                "......KTTTQQQTTK......",
                ".....KTTTQQKQQTTK.....",
                "....KTTTQQKKKQQTTK....",
                "...KTTTQQKKKKKQQTTK...",
                "..KTTTQQKKKKKKKQQTTK..",
                ".KTTTQQKKKKKKKKKQQTTK.",
                "KKKKKKKKKKKKKKKKKKKKKK",
            };
            TentA = Make(tent, pal);
            TentB = Make(tent, Swap(pal, ('T', U.C(130, 150, 95)), ('Q', U.C(95, 115, 70))));

            // ---------- fire: three flickering frames, exactly one block wide, standing on the block below ----------
            var fp = new Dictionary<char, Color32> { { 'W', U.C(255, 252, 210) }, { 'Y', U.C(255, 220, 90) }, { 'O', U.C(255, 140, 40) }, { 'R', U.C(220, 60, 40) }, { 'r', U.C(140, 30, 40) } };
            Flames = new[]
            {
                Make(new[]
                {
                    "......R.........",
                    ".....RR.....R...",
                    ".....ROR...RR...",
                    "....ROOR..ROR...",
                    "...ROOYOR.ROR...",
                    "...ROYYOORROOR..",
                    "..ROOYWYOOOOOR..",
                    "..ROYYWWYOYOOR..",
                    ".ROOYWWWYYYYOOR.",
                    ".ROYYWWWWYWYOOR.",
                    ".ROYWWWWWWWYYOR.",
                    "rROOYYWWWWYYOORr",
                    ".rRROOOOOOOORRr.",
                }, fp, false),
                Make(new[]
                {
                    "..........R.....",
                    "...R.....RR.....",
                    "...RR...ROR.....",
                    "...ROR..ROOR....",
                    "..ROOR.ROYOR....",
                    "..ROYORROYOOR...",
                    ".ROOYOOOYWYOR...",
                    ".ROYYWOOYWYOOR..",
                    ".ROYWWYYWWYYOR..",
                    "ROOYWWWWWWYYOOR.",
                    "ROYYWWWWWWWYYOR.",
                    "rROOYYWWWWYYOORr",
                    ".rRROOOOOOOORRr.",
                }, fp, false),
                Make(new[]
                {
                    ".......R........",
                    "......RR...R....",
                    "..R...ROR..RR...",
                    "..RR.ROOR.ROR...",
                    "..ROR.ROYORROR..",
                    ".ROOR.ROYOOOOR..",
                    ".ROYORROYWYOYOR.",
                    "ROOYYOOYWWYYOOR.",
                    "ROYYWYYWWWWYYOR.",
                    "ROYWWWWWWWWWYOR.",
                    "ROYYWWWWWWWYYOOR",
                    "rROOYYWWWWYYOORr",
                    ".rRROOOOOOOORRr.",
                }, fp, false),
            };

            // ---------- more camp clutter ----------
            var cp = new Dictionary<char, Color32>
            {
                { 'b', U.C(95, 60, 35) }, { 'B', U.C(150, 95, 55) }, { 'L', U.C(190, 135, 80) }, { 'M', U.C(125, 130, 145) },
                { 'm', U.C(70, 72, 85) }, { 'Y', U.C(255, 225, 110) }, { 'O', U.C(255, 150, 50) }, { 'G', U.C(250, 200, 60) },
                { 'R', U.C(185, 60, 50) }, { 'r', U.C(120, 35, 35) }, { 'C', U.C(230, 215, 170) }, { 'S', U.C(125, 125, 135) },
                { 'W', U.C(255, 250, 235) }, { 'g', U.C(90, 140, 70) },
            };
            CampBarrel = Make(new[]
            {
                "..bBBBBb..",
                ".mMMMMMMm.",
                ".bBLBBLBb.",
                ".bBLBBLBb.",
                ".mMMMMMMm.",
                ".bBLBBLBb.",
                ".bBLBBLBb.",
                ".mMMMMMMm.",
                "..bBBBBb..",
            }, cp);
            CampCrates = Make(new[]
            {
                "..bbbbbbbb....",
                "..bLLLLLLb....",
                "..bLbLLbLb....",
                "..bLLbbLLb....",
                "..bLbLLbLb....",
                "..bbbbbbbb....",
                "bbbbbbbbbbbbbb",
                "bBBBBBBBBBBBBb",
                "bBbBBBBBBBBbBb",
                "bBBbBBBBBBbBBb",
                "bBBBbbbbbbBBBb",
                "bBBbBBBBBBbBBb",
                "bBbBBBBBBBBbBb",
                "bbbbbbbbbbbbbb",
            }, cp);
            CampLantern = Make(new[]
            {
                "..m..",
                ".mMm.",
                ".YOY.",
                ".OWO.",
                ".YOY.",
                ".mMm.",
                "..b..",
                "..b..",
                "..b..",
                "..b..",
                "..b..",
                "..b..",
                "..b..",
                "..b..",
                ".bbb.",
            }, cp);
            CampPick = Make(new[]
            {
                "..MMMMM..",
                ".M..b..M.",
                "M...b...M",
                "....b....",
                "....b....",
                "....b....",
                "....b....",
                "...SbS...",
            }, cp);
            CampBedroll = Make(new[]
            {
                "..CCCRRRRRRRRRR.",
                ".CCCCRrRRRRRRRRr",
                ".CCCCRRRRRRRRRRr",
                "..rrrrrrrrrrrrr.",
            }, cp);
            CampCart = Make(new[]
            {
                "...G.GGG.G....",
                "..GGGGGGGGGG..",
                ".MMMMMMMMMMMM.",
                ".MmmmmmmmmmmM.",
                ".MmMMMMMMMMmM.",
                ".MmmmmmmmmmmM.",
                "..MMMMMMMMMM..",
                "..mmm....mmm..",
                "..mSm....mSm..",
                "..mmm....mmm..",
            }, cp);
            CampSign = Make(new[]
            {
                "BBBBBBBBBB",
                "BLLLbbLLLB",
                "BLLLbbLLLB",
                "BLbbbbbbLB",
                "BLLbbbbLLB",
                "BLLLbbLLLB",
                "BBBBBBBBBB",
                "....bb....",
                "....bb....",
                "....bb....",
                "....bb....",
                "...bbbb...",
            }, cp);
            CampWood = Make(new[]
            {
                "....LLLL....",
                "....LbbL....",
                "..LLLLLLLL..",
                "..LbbLLbbL..",
                "LLLLLLLLLLLL",
                "LbbLLbbLLbbL",
                "LLLLLLLLLLLL",
            }, cp);
            CampPot = Make(new[]
            {
                "..m......m..",
                "..mMMMMMMm..",
                ".mMgggggMMm.",
                ".mMMMMMMMMm.",
                ".mMMMMMMMMm.",
                "..mMMMMMMm..",
                "...mmmmmm...",
                "..SS....SS..",
            }, cp);
            string[] shroom =
            {
                ".RRR.",
                "RRWRR",
                "RWRRR",
                "..C..",
                "..C..",
            };
            CampShroom = Make(shroom, cp);
            CampShroomBlue = Make(shroom, Swap(cp, ('R', U.C(80, 170, 255)), ('W', U.C(200, 240, 255))));
            CampFlag = Make(new[]
            {
                "bRRRRR",
                "bRRWRr",
                "bRRRr.",
                "bRRr..",
                "bRr...",
                "b.....",
                "b.....",
                "b.....",
                "b.....",
                "b.....",
                "b.....",
                "bb....",
            }, cp);

            // ---------- King Ornin, undead (final boss) ----------
            string[] ornA =
            {
                "..........Y..Y..Y.........",
                ".........YYYLYYLYY........",
                ".........YCYYYYYCY........",
                "........vVVVVVVVVVv.......",
                "........VVXVVVVXVVV.......",
                "........VvVVvvVVVvV.......",
                ".......GWWWWVVVWWWWG......",
                "....RRGGWWWWWWWWWWGGRR....",
                "...RRGGGWWWWWWWWWWGGGRR...",
                "..rRRDDDGWWWWWWWWGDDDRRr..",
                "..rRDDDDDWWWWWWWWDDDDDRr..",
                "..rRDgDDDDWWWWWWDDDDgDRr..",
                "..rRDDDDDDWWWWWWDDDDDDRr..",
                ".rRRDDDDDDVVWWVVDDDDDDRRr.",
                ".rRRgDDDDvVVYYVVvDDDDgRRr.",
                ".rRRYYYYYYYYYYYYYYYYYYRRr.",
                ".rRRDDDDDDDDLLDDDDDDDDRRr.",
                "rRRRDDDDDDDDLLDDDDDDDDRRRr",
                "rRRRDDDDDDDDLLDDDDDDDDRRRr",
                "rRRRDDDDDD..LL..DDDDDDRRRr",
                "rrRRDDDDDD..LL..DDDDDDRRrr",
                ".rRRDDDDD...LL...DDDDDRRr.",
                "..rrDDDDD...LL...DDDDDrr..",
                "....ggggg...LL...ggggg....",
                "....bbbbb...LL...bbbbb....",
                "...bbbbbb...LL...bbbbbb...",
                "...KKKKKK...LL...KKKKKK...",
            };
            string[] ornB = (string[])ornA.Clone();
            ornB[4] = "........VVYVVVVYVVV.......";
            ornB[20] = "rRRRDDDDDD..LL..DDDDDDRRRr";
            ornB[21] = "rrRRDDDDD...LL...DDDDDRRrr";
            ornB[22] = ".rrrDDDDD...LL...DDDDDrrr.";
            AddEnemy("ornin", false, Swap(pal, ('V', U.C(120, 150, 90)), ('v', U.C(80, 100, 60)), ('W', U.C(200, 200, 190)),
                ('R', U.C(150, 40, 40)), ('r', U.C(90, 20, 25)), ('L', U.C(230, 235, 240))), ornA, ornB);

            Icons["goddiamond"] = Make(new[]
            {
                "......W......",
                ".....WCW.....",
                "....WCLCW....",
                "...WCLWLCW...",
                "..WCLWWWLCW..",
                ".WWWWWWWWWWW.",
                "WCCLLCWCLLCCW",
                ".WCCLCWCLCCW.",
                "..WCCLWLCCW..",
                "...WCLWLCW...",
                "....WCWCW....",
                ".....WCW.....",
                "......W......",
            }, Swap(pal, ('C', U.C(120, 230, 255)), ('L', U.C(210, 250, 255))), true, true);

            // ---------- altar-only weapons ----------
            string[] bomb =
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
            };
            Icons["megabomb"] = Make(bomb, Swap(pal, ('D', U.C(130, 50, 170)), ('W', U.C(255, 180, 255))), true, true);
            string[] hammer =
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
            };
            Icons["earthsplitter"] = Make(hammer, Swap(pal, ('G', U.C(255, 120, 40)), ('W', U.C(255, 230, 120)), ('b', U.C(60, 40, 40))), true, true);
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
            Icons["starpick"] = Make(pick, Swap(pal, ('G', U.C(255, 250, 200)), ('b', U.C(255, 200, 60))), true, true);

            // SUPER JUMP: a spring boot shooting upwards
            var jp = new Dictionary<char, Color32> { { 'Y', U.C(255, 220, 80) }, { 'y', U.C(200, 150, 40) }, { 'B', U.C(150, 95, 55) }, { 'b', U.C(95, 60, 35) }, { 'S', U.C(190, 195, 210) }, { 'W', U.C(255, 255, 240) } };
            Icons["superjump"] = Make(new[]
            {
                ".....W.....",
                "....WYW....",
                "...WYYYW...",
                "..YYyYyYY..",
                ".....Y.....",
                "...BBBB....",
                "...BBBB....",
                "...BBBBBB..",
                "..bbbbbbb..",
                "...S.S.S...",
                "..S.S.S....",
            }, jp, true, true);
            // SENTRY: a little gun on a tripod
            var tp = new Dictionary<char, Color32> { { 'G', U.C(110, 120, 140) }, { 'g', U.C(70, 75, 90) }, { 'K', U.C(40, 42, 52) }, { 'R', U.C(255, 70, 60) }, { 'L', U.C(160, 165, 180) } };
            Icons["sentry"] = Make(new[]
            {
                "...........",
                "..GGGG.....",
                ".GGRGGKKKKK",
                ".GGGGGKKKKK",
                "..gggg.....",
                "....g......",
                "....g......",
                "...L.L.....",
                "..L...L....",
                ".L.....L...",
            }, tp, true, false);
        }
    }
}
