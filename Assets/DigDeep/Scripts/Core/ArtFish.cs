using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Fish: a silver minnow, a red-bellied biter, and a big mottled grouper. Two frames each (tail flick).
    public static partial class Art
    {
        static void InitFish()
        {
            var pal = new Dictionary<char, Color32>
            {
                { 'S', U.C(150, 172, 194) }, { 'L', U.C(214, 226, 236) }, { 'D', U.C(80, 96, 124) }, { 'K', U.C(20, 18, 26) },
                { 'G', U.C(92, 98, 116) }, { 'R', U.C(196, 64, 58) }, { 'W', U.C(245, 245, 235) },
                { 'B', U.C(92, 112, 78) }, { 'b', U.C(58, 74, 52) }, { 'Y', U.C(156, 166, 92) }, { 'M', U.C(40, 30, 30) },
            };
            AddEnemy("fish_minnow", true, pal,
                new[] { "....SSS...", "D.SSSSSSS.", "DDSSSSSSKS", "D.SLLLLLS.", "....LL...." },
                new[] { "D...SSS...", "DDSSSSSSS.", ".DSSSSSSKS", "..SLLLLLS.", "....LL...." });
            var gl = new Dictionary<char, Color32>(pal) { ['L'] = U.C(176, 178, 144) };
            AddEnemy("fish_piranha", true, pal,
                new[] { "....GGGG....", "..GGGGGGGG..", "D.GGGGGGGKG.", "DDGGGGGGGGGW", "DDRRRRRRRWW.", "D.RRRRRRRRW.", "..RRRRRRR...", "....R..R...." },
                new[] { "....GGGG....", "..GGGGGGGG..", ".DGGGGGGGKG.", "DDGGGGGGGGGW", "DDRRRRRRRWW.", ".DRRRRRRRRW.", "..RRRRRRR...", "....R..R...." });
            AddEnemy("fish_grouper", true, gl,
                new[] { "......bbbbb.......", "....bBBBBBBBbb....", "..bBBBYBBBBBBBb...", "bbBBBBBBBYBBBBBBb.", "bbBBYBBBBBBBBBKBBb", "bbBBBBBBBBBBBBBBMM", "bbLLLLLLLLLLLLLLLb", "..LLLLLLLLLLLLLL..", "....bLLLLLLLLb....", "......b...b......." },
                new[] { "......bbbbb.......", "....bBBBBBBBbb....", "..bBBBYBBBBBBBb...", ".bBBBBBBBYBBBBBBb.", "bbBBYBBBBBBBBBKBBb", "bbBBBBBBBBBBBBBBMM", ".bLLLLLLLLLLLLLLLb", "..LLLLLLLLLLLLLL..", "....bLLLLLLLLb....", "......b...b......." });
        }
    }
}
