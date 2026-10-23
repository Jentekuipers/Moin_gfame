using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // The diggers, drawn whole (not a dwarf body with an animal head): each has its own silhouette, colours and
    // outfit. Frames: idle, run 1, run 2, jump, dig/swing, sitting.
    public static partial class Art
    {
        // legs drawn in the digger's own leg (L) and foot (F) colours
        static readonly string[][] LegSets =
        {
            new[] { ".....LLLLLL.....", ".....LL..LL.....", ".....FF..FF....." },   // idle
            new[] { ".....LLLLLL.....", "....LL....LL....", "....FF....FF...." },   // run 1
            new[] { ".....LLLLLL.....", "......LLLL......", "......FFFF......" },   // run 2
            new[] { ".....LLLLLL.....", "....LL..LL......", "...FF....FF....." },   // jump
            new[] { ".....LLLLLLLLL..", "...........FF..." },                       // sitting
        };

        static string[] Legs(string[] rows, char l, char f)
        {
            var r = new string[rows.Length];
            for (int i = 0; i < rows.Length; i++) r[i] = rows[i].Replace('L', l).Replace('F', f);
            return r;
        }

        static string[] Cat2(string[] a, string[] b)
        {
            var r = new string[a.Length + b.Length];
            a.CopyTo(r, 0); b.CopyTo(r, a.Length);
            return r;
        }

        // the swing/dig pose: the head and shoulders lean one pixel forward
        static string[] Lean(string[] upper)
        {
            var r = (string[])upper.Clone();
            for (int i = 0; i < Mathf.Min(7, r.Length); i++) r[i] = "." + r[i].Substring(0, r[i].Length - 1);
            return r;
        }

        // Builds the 6 frames of a digger. customLegs (optional) replaces the standard legs: idle, run1, run2, jump, sit.
        public static Sprite[] Digger(string[] upper, char legC, char footC, Dictionary<char, Color32> pal, string[][] customLegs = null)
        {
            var legs = new string[5][];
            for (int i = 0; i < 5; i++) legs[i] = customLegs != null ? customLegs[i] : Legs(LegSets[i], legC, footC);
            var p = new Dictionary<char, Color32>(pal);
            if (!p.ContainsKey('K')) p['K'] = U.C(25, 22, 30);
            return new[]
            {
                Make(Cat2(upper, legs[0]), p), Make(Cat2(upper, legs[1]), p), Make(Cat2(upper, legs[2]), p),
                Make(Cat2(upper, legs[3]), p), Make(Cat2(Lean(upper), legs[0]), p), Make(Cat2(upper, legs[4]), p),
            };
        }

        static Dictionary<char, Color32> HPal(params (char, Color32)[] cs)
        {
            var d = new Dictionary<char, Color32>();
            foreach (var (k, c) in cs) d[k] = c;
            return d;
        }

        // ---- the roster (all drawn facing right)

        public static Sprite[] HeroMole() => Digger(new[]
        {
            "......yy........",
            "....bbbbbb......",
            "...bBBBBBBb.....",
            "..bBBBBkBBBb....",
            "..bBBBBBBBBpp...",
            "..rrrrrrrrrrr...",
            "..rRRrrrrrRRr...",
            ".gGGGBBBBGGGg...",
            "gGGGGBBBBGGGGg..",
            "gGG.ByyyB.GGg...",
            ".gg.BBBBB.gg....",
        }, 'b', 'K', HPal(('b', U.C(92, 64, 52)), ('B', U.C(140, 100, 76)), ('k', U.C(30, 20, 20)), ('p', U.C(240, 140, 160)), ('y', U.C(250, 210, 80)),
                         ('r', U.C(210, 40, 50)), ('R', U.C(150, 24, 36)), ('g', U.C(54, 96, 60)), ('G', U.C(82, 140, 80))));

        public static Sprite[] HeroFox() => Digger(new[]
        {
            "....a.....a.....",
            "....aA...Aa.....",
            "....AAAAAAA.....",
            "....AAEAAAAccc..",
            "....AAAAAcccccN.",
            ".....AAAAccc....",
            "....ssssssss....",
            "...BBBssBBBB....",
            "AABBBBcccBBBB...",
            "AcBB.BcccB.BB...",
            ".cc.BBBBBBB.....",
        }, 'B', 'K', HPal(('A', U.C(232, 122, 40)), ('a', U.C(150, 64, 22)), ('c', U.C(250, 236, 212)), ('E', U.C(30, 24, 30)), ('N', U.C(30, 20, 20)),
                         ('s', U.C(70, 150, 100)), ('B', U.C(96, 66, 48))));

        public static Sprite[] HeroFrog() => Digger(new[]
        {
            "...WW....WW.....",
            "..WKWW..WKWW....",
            "..gGGGGGGGGg....",
            ".gGGGGGGGGGGg...",
            ".gGGGGGGGGGGGr..",
            ".gGrrrrrrrrrg...",
            "..gGGGGGGGGg....",
            ".vVVyyyyyVVv....",
            "gvVyyyyyyyVvg...",
            "g.vVyyyyyVv.g...",
            "...vvvvvvv......",
        }, 'g', 'G', HPal(('W', U.C(250, 250, 236)), ('g', U.C(56, 140, 64)), ('G', U.C(110, 200, 90)), ('r', U.C(40, 80, 40)),
                         ('y', U.C(236, 226, 130)), ('v', U.C(110, 70, 44)), ('V', U.C(150, 100, 60))));

        public static Sprite[] HeroBeetle() => Digger(new[]
        {
            "...........hh...",
            "..........hhH...",
            "....ssssssshh...",
            "...sSSSSSSSsEs..",
            "..sSSWSSSSSSSs..",
            "..sSSSSSSSSSSs..",
            ".sSSSSSWSSSSSSs.",
            ".sSSSSSSSSSSSSs.",
            ".sSSSSSSSSSSSSs.",
            "..sSSSSSSSSSSs..",
            "...ssssssssss...",
        }, 'k', 'K', HPal(('s', U.C(30, 60, 70)), ('S', U.C(46, 104, 112)), ('W', U.C(150, 220, 210)), ('h', U.C(36, 48, 56)), ('H', U.C(120, 140, 150)),
                         ('E', U.C(240, 210, 90)), ('k', U.C(40, 44, 52))));

        public static Sprite[] HeroDolphin() => Digger(new[]
        {
            ".......dd.......",
            "......dDDd......",
            "....dDDDDDDd....",
            "...dDDDDDDDDd...",
            "...dDDEDDDDDDNN.",
            "...dDDDDwwwwww..",
            "....dDDwwwwww...",
            "...yYYYwwwwYYy..",
            "..yYYYYwwwwYYYy.",
            ".dYY.YwwwwY.YYd.",
            "..d..YYYYYY..d..",
        }, 'd', 'D', HPal(('d', U.C(58, 92, 130)), ('D', U.C(96, 146, 190)), ('E', U.C(25, 22, 30)), ('N', U.C(124, 170, 206)), ('w', U.C(226, 236, 244)),
                         ('y', U.C(200, 100, 30)), ('Y', U.C(250, 150, 50))));

        // after the hooded owl in the moodboard: a grey hood with a gold trim, a white face disc, orange eyes, a cloak of wings
        public static Sprite[] HeroOwl() => Digger(new[]
        {
            "....hhhhhhh.....",
            "...hHHHHHHHh....",
            "..hHtwwwwwtHh...",
            "..hHwOwwwOwHh...",
            "..hHwwwNwwwHh...",
            "..hhHwwwwwHhh...",
            "..ggtttttttgg...",
            ".gGGGcccccGGGg..",
            "gGGGccOcccGGGg..",
            "gGGG.ccccc.GGg..",
            ".gGgg.ccc.gGg...",
        }, 'c', 'y', HPal(('h', U.C(62, 70, 92)), ('H', U.C(96, 108, 132)), ('t', U.C(214, 150, 60)), ('w', U.C(244, 240, 230)), ('O', U.C(250, 150, 40)),
                         ('N', U.C(60, 50, 50)), ('g', U.C(110, 104, 112)), ('G', U.C(176, 170, 176)), ('c', U.C(230, 222, 206)), ('y', U.C(240, 170, 50))));

        public static Sprite[] HeroCat() => Digger(new[]
        {
            "....a.....a.....",
            "....aA...Aa.....",
            "....AAAAAAA.....",
            "....AAEAAAEA....",
            "....AAAAApAA....",
            ".....AAAAAA.....",
            "....rrrryrrr....",
            "...cCCCCCCCCc...",
            "..cCCAAAAACCCc..",
            "AAcC.AAAAA.Cc...",
            "A..c.AAAAA.c....",
        }, 'A', 'a', HPal(('A', U.C(46, 42, 58)), ('a', U.C(28, 26, 36)), ('E', U.C(150, 240, 110)), ('p', U.C(230, 140, 170)), ('r', U.C(190, 40, 50)),
                         ('y', U.C(250, 210, 80)), ('C', U.C(92, 60, 120)), ('c', U.C(60, 40, 84))));

        // after the octopus in the moodboard: a purple bulb with salmon spots, big eyes, curling tentacles for legs
        public static Sprite[] HeroOctopus() => Digger(new[]
        {
            ".....PPPPP......",
            "....PPPPPPPP....",
            "...PPsPPPPsPP...",
            "...PPPPPPPPPP...",
            "...PWKPPPWKPP...",
            "...PPPPPPPPPP...",
            "....PPpppPPP....",
            "...PsPPPPPPsP...",
            "..PP.PPsPPP.PP..",
            ".PP..PPPPPP..PP.",
            ".p...PP..PP...p.",
        }, 'P', 'p', HPal(('P', U.C(140, 70, 132)), ('p', U.C(92, 40, 92)), ('s', U.C(240, 120, 110)), ('W', U.C(250, 250, 250)), ('K', U.C(25, 22, 30))),
            new[]
            {
                new[] { "....PP.PP.PP....", "...P..P..P..P...", "..pp.pp..pp.pp.." },
                new[] { "...PP.PP.PP.....", "..P..P..P..P....", ".pp..pp.pp..pp.." },
                new[] { ".....PP.PP.PP...", "....P..P..P..P..", "...pp.pp.pp..pp." },
                new[] { "....PP.PP.PP....", "...P.P....P.P...", "..p..p....p..p.." },
                new[] { "....PPPPPPPPP...", "...........pp..." },
            });

        public static Sprite[] HeroLynx() => Digger(new[]
        {
            "...a.......a....",
            "...aa.....aa....",
            "....AAAAAAA.....",
            "....AAEAAAEA....",
            "....cAAANAAc....",
            "....ccAAAAcc....",
            "....TTTTTTTT....",
            "...TttBBBBttT...",
            "..ttBBBBBBBBtt..",
            "..tt.BBBBBB.tt..",
            ".....BBBBBB.....",
        }, 'A', 'K', HPal(('a', U.C(110, 80, 55)), ('A', U.C(204, 164, 112)), ('E', U.C(40, 60, 40)), ('N', U.C(200, 110, 110)), ('c', U.C(240, 230, 210)),
                         ('T', U.C(50, 140, 140)), ('t', U.C(30, 90, 96)), ('B', U.C(80, 70, 84))));

        static void InitHeroes()
        {
            // the black cat's dagger, drawn point-up like every melee weapon in the hand
            Icons["dagger"] = Make(new[] { "..W..", ".WsW.", ".WsW.", ".WsW.", ".WsW.", "yyyyy", "..b..", "..b.." },
                HPal(('W', U.C(236, 240, 248)), ('s', U.C(150, 160, 180)), ('y', U.C(230, 190, 70)), ('b', U.C(110, 70, 40))), true, true);
        }
    }
}
