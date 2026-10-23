using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Icons for the run-changing trinkets, and the things they visibly put on your digger.
    public static partial class Art
    {
        static readonly Dictionary<string, Sprite> lookCache = new Dictionary<string, Sprite>();
        static Dictionary<string, string[]> lookMaps;

        static void InitLooks()
        {
            // ---- trinket icon shapes (10x10; a = colour, b = dark, c = light, K = ink, W = white, R = red)
            Shape("barrel2",
                "..........", "..........", ".ccccccccK", ".bbbbbbbbK", ".ccccccccK",
                ".bbbbbbbbK", "..aab.....", "..aab.....", "..aab.....", "..bbb.....");
            Shape("barrel3",
                "..........", ".ccccccccK", ".bbbbbbbbK", ".ccccccccK", ".bbbbbbbbK",
                ".ccccccccK", ".bbbbbbbbK", "..aab.....", "..aab.....", "..bbb.....");
            Shape("barrel4",
                ".ccccccccK", ".bbbbbbbbK", ".ccccccccK", ".bbbbbbbbK", ".ccccccccK",
                ".bbbbbbbbK", ".ccccccccK", ".bbbbbbbbK", "..aab.....", "..bbb.....");
            Shape("prism",
                "....c.....", "....ca....", "...cca....", "...caab...", "..ccaaab..",
                "..caaaab..", ".ccaaaaab.", ".caaaaaab.", "bbbbbbbbbb", "R...R...R.");
            Shape("flame",
                "....a.....", "....aa....", "...aca....", "..acca.a..", "..accaaa..",
                ".aaccWcaa.", ".acWWWcab.", ".acWWWcab.", "..aacccb..", "...bbbb...");
            Shape("snowflake",
                "....c.....", ".c..c..c..", "..c.c.c...", "...ccc....", "ccccWcccc.",
                "...ccc....", "..c.c.c...", ".c..c..c..", "....c.....", "..........");
            Shape("bomb",
                "......Wa..", ".....a....", "....aa....", "..KKKKKK..", ".KccKKKKK.",
                ".KcKKKKKK.", ".KKKKKKKK.", ".KKKKKKKK.", "..KKKKKK..", "..........");
            Shape("drop",
                "....c.....", "...cca....", "...caa....", "..ccaab...", "..caaab...",
                "..aaaab...", "...abb....", "c.......c.", ".a..a..a..", "....a.....");
            Shape("snail",
                "..........", "...aaaa...", "..accccab.", ".acabbacab", ".acbccbab.",
                ".acbaabab.", ".aacbbcab.", "..aaccaab.", "bbbbbbbbbb", "..........");
            Shape("jetpack",
                "..........", ".aab.aab..", ".cab.cab..", ".cab.cab..", ".cabKcab..",
                ".cab.cab..", ".aab.aab..", "..R...R...", "..W...W...", "..R...R...");
            Shape("lens",
                "...KKKK...", "..KccaaK..", ".KcaaaabK.", ".KcaaaabK.", ".KaaaabbK.",
                "..KabbbK..", "...KKKK...", "....R.....", "....R.....", "....R.....");
            Shape("chamber",
                "..........", "...KKKK...", "..KccaaK..", "..KcaaaK..", "..KaaabK..",
                "..KaabbK..", "...KKKK...", "aaaaaaaaab", "bbbbbbbbbb", "..ab......");

            InitSwordShot();

            // ---- looks: worn or grown by the digger (centre pivot)
            lookMaps = new Dictionary<string, string[]>
            {
                { "wings", new[] { "WWW....", "cWWWW..", "cWWcWW.", ".cWWcWW", "..cWWcW", "....cWc", ".....c." } },
                { "halo", new[] { ".yyyyyy.", "y......y", ".yyyyyy." } },
                { "runes", new[] { "vVv.vVv.vVv", ".v...v...v." } },
                { "horns", new[] { "W..........W", "Wc........cW", ".Wc......cW." } },
                { "bighorns", new[] { "R............R", "Rb..........bR", ".Rb........bR.", "..RR......RR.." } },
                { "flamecrown", new[] { "o...y...o.", ".o.yoy.o..", "oyoyoyoyoy", "oooooooooo" } },
                { "jetpack", new[] { ".GG.", "GccG", "GccG", "GccG", "GccG", "DGGD", ".oo." } },
                { "bombhat", new[] { ".....Wo..", "....o....", "..DDDDD..", ".DcDDDDD.", "DDDDDDDDD" } },
                { "cowboy", new[] { "...bbbbbb...", "...baaaab...", "...aaaaaa...", "bbbbbbbbbbbb" } },
                { "visor", new[] { "RRRRRRRR", "rrrrrrrr" } },
                { "buckler", new[] { ".G.", "GWG", "GcG", "GcG", "GyG", "GcG", "GcG", "GWG", ".G." } },
                { "headband", new[] { "RRRRRRRRRR", "rrrrrrrrrR", "........Rr", ".........R" } },
            };
        }

        static readonly Dictionary<char, Color32> lookPal = new Dictionary<char, Color32>
        {
            { 'W', U.C(245, 245, 250) }, { 'c', U.C(196, 206, 228) }, { 'y', U.C(255, 226, 92) }, { 'v', U.C(150, 96, 240) },
            { 'V', U.C(222, 190, 255) }, { 'R', U.C(220, 60, 60) }, { 'r', U.C(150, 30, 40) }, { 'b', U.C(96, 62, 38) },
            { 'a', U.C(150, 104, 62) }, { 'o', U.C(255, 132, 40) }, { 'G', U.C(118, 134, 158) }, { 'D', U.C(52, 50, 64) },
        };

        // ---- pets (2 frames each) and the flying sword (THOUSAND BLADES)
        static readonly Dictionary<char, Color32> petPal = new Dictionary<char, Color32>
        {
            { 'v', U.C(110, 70, 120) }, { 'V', U.C(160, 110, 170) }, { 'y', U.C(255, 220, 90) }, { 'c', U.C(110, 210, 255) },
            { 'W', U.C(235, 250, 255) }, { 'g', U.C(80, 165, 70) }, { 'G', U.C(150, 225, 110) }, { 'k', U.C(40, 36, 40) },
            { 'K', U.C(25, 22, 30) }, { 'w', U.C(232, 222, 200) }, { 'b', U.C(120, 76, 44) }, { 's', U.C(170, 180, 196) },
        };
        static readonly Dictionary<string, Sprite[]> petCache = new Dictionary<string, Sprite[]>();
        static readonly Dictionary<string, string[][]> petMaps = new Dictionary<string, string[][]>
        {
            { "bat", new[] { new[] { "v.......v", "vv.vvv.vv", ".vvVyVvv.", "..vvvvv..", "...v.v..." },
                             new[] { "...vvv...", "..vVyVv..", ".vvvvvvv.", "vv.....vv", "v.......v" } } },
            { "wisp", new[] { new[] { "..c..", ".cWc.", "cWWWc", "cWWWc", ".ccc.", "..c.." },
                              new[] { ".c...", ".cWc.", "cWWWc", "cWWWc", ".ccc.", "...c." } } },
            { "beetle", new[] { new[] { "..gggg..", ".gGgggg.", "ggggggKK", ".kk.kk.." },
                                new[] { "..gggg..", ".gGgggg.", "ggggggKK", "..kk.kk." } } },
            { "orb", new[] { new[] { ".WWW.", "WcWWW", "WWWWW", "WWWWc", ".WWW." },
                             new[] { ".WWW.", "WWWcW", "WWWWW", "cWWWW", ".WWW." } } },
            { "frost", new[] { new[] { ".c.c.", "cWcWc", ".WWW.", "cWcWc", ".c.c." },
                               new[] { "..c..", ".cWc.", "cWWWc", ".cWc.", "..c.." } } },
            { "disc", new[] { new[] { "..ss..", ".sWWs.", "sWkkWs", "sWkkWs", ".sWWs.", "..ss.." },
                              new[] { "s.ss.s", ".sWWs.", "sWkkWs", "sWkkWs", ".sWWs.", "s.ss.s" } } },
            { "magpie", new[] { new[] { "..kk...", ".kykk..", "kkWWkkk", ".kWWk..", "..k.k.." },
                                new[] { ".k...k.", "kkykkk.", ".kWWkkk", "..WWk..", "..k.k.." } } },
            { "fly", new[] { new[] { "W.W", ".k.", "..." }, new[] { "...", "WkW", "..." } } },
            { "spore", new[] { new[] { ".ggggg.", "gGggGgg", "ggggggg", "..wKw..", "..www..", ".k...k." },
                               new[] { ".ggggg.", "gGggGgg", "ggggggg", "..wKw..", "..www..", "..k.k.." } } },
        };

        public static Sprite[] PetFrames(string kind)
        {
            if (petCache.TryGetValue(kind, out var f)) return f;
            if (!petMaps.TryGetValue(kind, out var maps)) maps = petMaps["orb"];
            f = new Sprite[maps.Length];
            for (int i = 0; i < maps.Length; i++) f[i] = Make(maps[i], petPal, true, true);
            petCache[kind] = f;
            return f;
        }

        static void InitSwordShot()
        {
            Bullets["b_sword"] = Make(new[] { "bb.ysssssW.", "bbyyWWWWWWW", "bb.ysssssW." }, petPal, true, true);
        }

        public static Sprite Look(string key)
        {
            if (key == "ward") return Ring;   // the WARDSTONE's bubble
            if (lookMaps == null || !lookMaps.TryGetValue(key, out var map)) return null;
            if (lookCache.TryGetValue(key, out var s)) return s;
            s = Make(map, lookPal, true, true);
            lookCache[key] = s;
            return s;
        }

        // drawn behind the digger (wings, jetpack) rather than on top
        public static bool LookBehind(string key) => key == "wings" || key == "jetpack";
    }
}
