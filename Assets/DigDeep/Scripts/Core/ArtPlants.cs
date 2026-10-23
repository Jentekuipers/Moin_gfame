using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Jungle sprites: snapper plants, seed pods and the Verdant Maw.
    public static partial class Art
    {
        public static Sprite Seed, MawBush;

        static void InitPlants()
        {
            var pp = new Dictionary<char, Color32>
            {
                { 'G', U.C(80, 190, 80) }, { 'g', U.C(40, 112, 56) }, { 'L', U.C(170, 240, 110) }, { 'R', U.C(220, 48, 70) },
                { 'r', U.C(130, 22, 48) }, { 'W', U.C(250, 245, 220) }, { 'Y', U.C(255, 220, 80) }, { 'P', U.C(196, 90, 220) },
                { 'p', U.C(120, 50, 150) }, { 'K', U.C(26, 16, 30) }, { 'B', U.C(110, 80, 50) },
            };

            // SNAPPER: a flytrap on a stalk. Closed, then gaping wide.
            string[] snapA =
            {
                "....GGGGGG......",
                "..GGLLGGGGGG....",
                ".GLGGGGGGGGGG...",
                ".GGWGWGWGWGGG...",
                ".GGGRRRRRRRGG...",
                "..GGWGWGWGWG....",
                "...GGGGGGGG.....",
                ".....gGg........",
                "..LL..Gg..LL....",
                "...LLgGg.LL.....",
                ".....gGG........",
                "....ggGGg.......",
            };
            string[] snapB =
            {
                ".GGGGGGG........",
                "GLLGGGGGGG......",
                "GGWGWGWGWGG.....",
                "GRRRRRRRRRRG....",
                "GRrrrrrrrrRG....",
                "GRRRRRRRRRRG....",
                "GGWGWGWGWGG.....",
                ".GGGGGGGGG......",
                ".....gGg........",
                "..LL.gGg..LL....",
                "...LLgGg.LL.....",
                "....ggGGg.......",
            };
            AddEnemy("snapper", false, pp, snapA, snapB);

            // SEED POD: a purple bulb hanging from the ceiling that spits poison seeds
            string[] podA =
            {
                "......gg......",
                "......Gg......",
                ".....GGGG.....",
                "...PPPPPPPP...",
                "..PPpPPPPpPP..",
                "..PPPPYYPPPP..",
                "..PpPPYYPPpP..",
                "...PPPPPPPP...",
                "....PPPPPP....",
                ".....PRRP.....",
            };
            string[] podB = (string[])podA.Clone();
            podB[8] = "....PPRRPP....";
            podB[9] = ".....RRRR.....";
            AddEnemy("seedpod", true, pp, podA, podB);

            // THE VERDANT MAW: a huge rooted flower with a ring of petals and a maw full of thorns
            string[] mawA =
            {
                "..........PPPP..........",
                "......PP.PPppPP.PP......",
                ".....PppPPPppPPPppP.....",
                "...PPPPPPPPPPPPPPPPPP...",
                "..PpPPGGGGGGGGGGGGPPpP..",
                ".PPPGGLLGGGGGGGGGGGPPPP.",
                ".PpGGLGWGWGWGWGWGWGGPpP.",
                "PPPGGGRRRRRRRRRRRRGGGPPP",
                "PpPGGRrrrrrrrrrrrrRGGPpP",
                "PPPGGRrrYrrrrrrYrrRGGPPP",
                "PpPGGRrrrrrrrrrrrrRGGPpP",
                ".PPGGGRRRRRRRRRRRRGGGPP.",
                ".PpPGGWGWGWGWGWGWGGGPpP.",
                "..PPPGGGGGGGGGGGGGGPPP..",
                "...PPPPGGGGGGGGGGPPPP...",
                ".....PPPPgGGGgPPPP......",
                "..LL.....gGGGg.....LL...",
                "...LLL...gGGGg...LLL....",
                ".....LLLgGGGGGgLLL......",
                ".......ggGGGGGgg........",
                "....LLggGGGGGGGggLL.....",
                "..LLL.gggGGGGGggg.LLL...",
            };
            string[] mawB = (string[])mawA.Clone();
            mawB[7] = "PPPGGRRRRRRRRRRRRRRGGPPP";
            mawB[8] = "PpPGRrrrrrrrrrrrrrrRGPpP";
            mawB[9] = "PPPGRrrYrrrrrrrrYrrRGPPP";
            mawB[10] = "PpPGRrrrrrrrrrrrrrrRGPpP";
            mawB[11] = ".PPGGRRRRRRRRRRRRRRGGPP.";
            AddEnemy("verdantmaw", false, pp, mawA, mawB);

            Seed = Make(new[] { ".PP.", "PYPP", "PPPp", ".pp." }, pp, true, true);

            // the thick jungle bushes the Verdant Maw tunnels between
            MawBush = Make(new[]
            {
                "......LL..GG.......",
                "...GGLGGGGGGLG.....",
                "..GLGGGgGGGLGGGG...",
                ".GGGgGGGGPGGGgGGG..",
                "GGLGGGgGGGGGgGGLGG.",
                "GgGGPGGGgGGGGGGgGGG",
                "gGGGGgGGGGgGGPGGGgG",
                ".ggGGGgggGGGgggGgg.",
            }, pp);
            Bullets["b_seed"] = Seed;
        }
    }
}
