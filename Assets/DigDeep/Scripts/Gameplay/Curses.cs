using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Curses ride on weapons: a cursed weapon always carries one extra modifier, and the curse is the price.
    // While you carry it (either slot) the curse is on you; after 10 kills it lifts and the weapon stays.
    public enum Curse { None, Digger, Flame, Speed, Glass, Darkness, Poverty, Recoil, Jamming, Weight, Shaking, Hunted, Thirst, Restless, LeadBoots, Rust, Boomerang, Water, Lava, COUNT }

    public static class Curses
    {
        public const int KillsToLift = 10;

        public static readonly string[] Names =
        {
            "", "CURSE OF THE DIGGER", "CURSE OF THE FLAME", "CURSE OF SPEED", "CURSE OF GLASS", "CURSE OF DARKNESS",
            "CURSE OF POVERTY", "CURSE OF RECOIL", "CURSE OF JAMMING", "CURSE OF WEIGHT", "CURSE OF SHAKING",
            "CURSE OF THE HUNTED", "CURSE OF THIRST", "CURSE OF THE RESTLESS DEAD", "CURSE OF LEAD BOOTS", "CURSE OF RUST", "CURSE OF THE BOOMERANG", "CURSE OF WATER", "CURSE OF LAVA",
        };

        public static readonly string[] Desc =
        {
            "",
            "PICKAXES ORBIT YOU AND DIG AWAY THE GROUND (THEY NEVER HURT FOES)",
            "EVERY BLOCK YOU STAND ON BURSTS INTO FLAME WHEN YOU STEP OFF IT",
            "EVERYTHING MOVES 1.5X AS FAST",
            "YOU TAKE 50% MORE DAMAGE",
            "YOUR LIGHT SHRINKS TO HALF",
            "EVERY HIT KNOCKS 15% OF YOUR GOLD OUT OF YOUR POCKETS",
            "YOUR GUNS KICK THREE TIMES AS HARD",
            "RELOADS TAKE TWICE AS LONG, ENERGY GUNS HEAT UP FASTER",
            "YOU JUMP 25% LOWER",
            "YOUR AIM SHAKES: TRIPLE SPREAD",
            "ENEMIES SPOT YOU FROM TWICE AS FAR AWAY",
            "HEALING DOES NOTHING",
            "SLAIN FOES SOMETIMES RISE AGAIN AS BATS",
            "YOU SINK IN WATER AND CAN BARELY SWIM",
            "SHOP PRICES +50%",
            "YOUR SHOTS STALL FAST AND FLY BACK TO YOU",
            "EVERY SHOT LEAVES A PUDDLE OF WATER WHERE IT HITS",
            "SLAIN FOES LEAVE A POOL OF LAVA",
        };

        // the colour of the trapped ghost in each curse's lantern
        static readonly Color32[] Ghost =
        {
            new Color32(0, 0, 0, 0),
            U.C(214, 168, 96), U.C(255, 132, 52), U.C(110, 238, 255), U.C(226, 242, 255), U.C(118, 92, 214),
            U.C(250, 212, 70), U.C(255, 92, 92), U.C(176, 176, 190), U.C(126, 158, 120), U.C(255, 150, 222),
            U.C(204, 46, 60), U.C(84, 164, 255), U.C(184, 112, 255), U.C(96, 116, 150), U.C(206, 112, 62), U.C(120, 230, 170), U.C(70, 140, 255), U.C(255, 96, 30),
        };

        public static Color Col(Curse c) => Ghost[(int)c];
        public static string Name(Curse c) => Names[(int)c];

        // ---- which curses can show up, and how often

        public static void MaybeCurse(Weapon w, int depth, System.Random rng)
        {
            if (w == null || w.def == DB.Pickaxe) return;
            var pl = Game.I != null ? Game.I.player : null;
            double chance = Mathf.Min(0.1f + depth * 0.0005f, 0.3f) / (1f + 0.1f * Mathf.Max(0f, pl != null ? pl.s.luck : 0f));   // luck keeps curses away
            if (rng.NextDouble() >= chance) return;
            w.curse = (Curse)(1 + rng.Next((int)Curse.COUNT - 1));
            w.curseKills = 0;
            // the payment: one more modifier than it would have had, even past the normal limit
            var pool = new List<Mod>();
            for (int i = 0; i < (int)Mod.COUNT; i++) if (!w.Has((Mod)i) && Mods.Fits(w.def, (Mod)i)) pool.Add((Mod)i);   // may go past the usual cap
            if (pool.Count > 0) w.mods.Add(pool[rng.Next(pool.Count)]);
        }

        // ---- lantern icons: a trapped ghost in a little iron lantern, wisps curling around it.
        // Each curse has its own ghost colour and its own sigil on the lantern's cap.

        static Sprite[] icons;
        static readonly string[][] Sigils =
        {
            new[] { "..e..", ".eee." }, new[] { ".e.e.", "..e.." }, new[] { "e...e", ".eee." }, new[] { "..e..", "..e.." },
            new[] { ".eee.", "..e.." }, new[] { "e.e.e", "e.e.e" }, new[] { ".e.e.", ".e.e." }, new[] { "eeeee", "....." },
        };

        public static Sprite Icon(Curse c)
        {
            if (icons == null)
            {
                icons = new Sprite[(int)Curse.COUNT];
                for (int i = 1; i < icons.Length; i++) icons[i] = Build(i);
            }
            return icons[Mathf.Clamp((int)c, 1, icons.Length - 1)];
        }

        static Sprite Build(int i)
        {
            Color32 g = Ghost[i];
            var p = new Dictionary<char, Color32>
            {
                { 'K', U.C(30, 22, 40) }, { 'D', U.C(96, 86, 110) }, { 'G', U.C(116, 106, 132) }, { 'g', U.C(70, 62, 86) },
                { 'L', Pal.Ramp(g, 0) }, { 'C', g }, { 'W', Pal.Ramp(g, 4) }, { 'E', U.C(36, 18, 52) }, { 'e', Pal.Ramp(g, 3) }, { 'w', Pal.Ramp(g, 1) },
            };
            var s = Sigils[i % Sigils.Length];
            string cap1 = ".KGG" + s[0] + "GGK.", cap2 = ".Kgg" + s[1] + "ggK.";
            string[] map =
            {
                ".....KKK.....",
                "....KDDDK....",
                "...KD...DK...",
                "..KKKKKKKKK..",
                cap1,
                cap2,
                "w.KGLLLLLGK..",
                ".wKGLWCWLGKw.",
                "w.KGCECECGK.w",
                "w.KGCCCCCGK.w",
                ".wKGCCECCGKw.",
                "..KGLCCCLGK.w",
                "..KGLCLCLGKw.",
                ".KgggggggggK.",
                ".KGGGGGGGGGK.",
                "..KKKKKKKKK..",
            };
            return Art.Make(map, p, true, true);
        }
    }
}
