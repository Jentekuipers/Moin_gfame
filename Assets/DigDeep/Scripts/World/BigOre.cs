using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Multi-tile gold deposits: a semi-rare 2x2 GOLD VEIN, a very rare 4x4 MOTHER LODE and an insanely rare 6x6 KING'S HOARD.
    // Every cell of a deposit shares one health pool (kept in World). Each hit flings a coin or two at
    // the player and cracks the whole thing a bit more; when it breaks it bursts into a fountain of gold.
    public static class BigOre
    {
        const int Cell2 = 10, Cell4 = 24, Cell6 = 40;   // placement grids
        const float Chance2 = 0.055f, Chance4 = 0.025f, Chance6 = 0.006f;
        const int MinDepth2 = Gen.CampFloor + 5, MinDepth4 = 40, MinDepth6 = 80;

        public static bool Is(Block b) => b == Block.BigGold2 || b == Block.BigGold4 || b == Block.BigGold6;

        public static int MaxHp(int size, int depth) => size == 6 ? 120 + depth / 5 : size == 4 ? 45 + depth / 10 : 14 + depth / 25;

        public static int Payout(int size, int depth) => size == 6 ? 300 + depth : size == 4 ? 90 + depth / 3 : 25 + depth / 6;

        static bool T6(int x, int d, out int ox, out int od) => Try(x, d, 6, Cell6, Chance6, MinDepth6, 601, out ox, out od);
        static bool T4(int x, int d, out int ox, out int od) => Try(x, d, 4, Cell4, Chance4, MinDepth4, 401, out ox, out od);
        static bool T2(int x, int d, out int ox, out int od) => Try(x, d, 2, Cell2, Chance2, MinDepth2, 211, out ox, out od);

        // Is (x, depth) part of a deposit? Pure function of the seed, like the rest of the terrain.
        // A semi-rare 2x2 vein, a very rare 4x4 mother lode, and an insanely rare 6x6 KING'S HOARD.
        public static bool Find(int x, int depth, out Block b, out int ox, out int od, out int size)
        {
            if (T6(x, depth, out ox, out od)) { b = Block.BigGold6; size = 6; return true; }
            if (T4(x, depth, out ox, out od) && !Overlaps(ox, od, 4, 6)) { b = Block.BigGold4; size = 4; return true; }
            if (T2(x, depth, out ox, out od) && !Overlaps(ox, od, 2, 4) && !Overlaps(ox, od, 2, 6)) { b = Block.BigGold2; size = 2; return true; }
            b = Block.Air; size = 0;
            return false;
        }

        // does a deposit of `size` at (ox, od) touch any deposit of the bigger size?
        static bool Overlaps(int ox, int od, int size, int big)
        {
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                    if (big == 6 ? T6(ox + i, od + j, out _, out _) : T4(ox + i, od + j, out _, out _)) return true;
            return false;
        }

        static bool Try(int x, int depth, int size, int cell, float chance, int minDepth, int salt, out int ox, out int od)
        {
            int cx = U.FloorDiv(x, cell), cd = U.FloorDiv(depth, cell);
            ox = od = 0;
            int seed = Gen.Seed;
            if (U.H01(cx, cd, seed + salt) >= chance) return false;
            ox = cx * cell + 2 + (int)(U.H01(cx, cd, seed + salt + 1) * (cell - size - 4));
            od = cd * cell + 1 + (int)(U.H01(cx, cd, seed + salt + 2) * (cell - size - 2));
            if (x < ox || x >= ox + size || depth < od || depth >= od + size) return false;
            if (od < minDepth || od + size >= ArenaRoom.Bottom - 1) return false;
            if (ox < 2 || ox + size - 1 > Gen.W - 3) return false;
            for (int j = 0; j < size; j++)            // never cut into shops, puzzles or arenas
                for (int i = 0; i < size; i++)
                    if (Gen.CarveAt(ox + i, od + j) >= 0) return false;
            return true;
        }

        // ------------------------------------------------------------------ drawing

        static readonly Dictionary<long, bool[]> crackCache = new Dictionary<long, bool[]>();

        static bool[] Cracks(int size, int ox, int od, int level)
        {
            long key = ((long)ox << 40) ^ ((long)od << 16) ^ ((long)size << 8) ^ level;
            if (crackCache.TryGetValue(key, out var m)) return m;
            int N = size * 16;
            m = new bool[N * N];
            float c = (N - 1) * 0.5f;
            for (int k = 0; k < level; k++)
            {
                float a = U.H01(ox * 7 + k, od, 900) * Mathf.PI * 2f;
                float x = c + Mathf.Cos(a) * 1.5f, y = c + Mathf.Sin(a) * 1.5f;
                int steps = (int)(N * (0.25f + 0.25f * U.H01(ox, od * 3 + k, 901)));
                for (int s = 0; s < steps; s++)
                {
                    a += (U.H01(ox + s, od + k * 13, 902) - 0.5f) * 1.1f;
                    x += Mathf.Cos(a); y += Mathf.Sin(a);
                    int ix = Mathf.RoundToInt(x), iy = Mathf.RoundToInt(y);
                    if (ix < 1 || iy < 1 || ix >= N - 1 || iy >= N - 1) break;
                    m[iy * N + ix] = true;
                    if (level > 6 && (s & 1) == 0) m[iy * N + ix + 1] = true;   // cracks widen near the end
                }
            }
            crackCache[key] = m;
            if (crackCache.Count > 512) crackCache.Clear();
            return m;
        }

        // Draws the 16x16 slice (cx, cyUp) of a deposit. cyUp counts cells from the deposit's bottom row.
        public static void Draw(Color32[] buf, int stride, int px0, int py0, Block b, int cx, int cyUp, int size,
                                float dmg, int ox, int od, Biome bio)
        {
            int N = size * 16;
            float R = size == 6 ? 13f : size == 4 ? 9f : 5f;
            float half = (N - 1) * 0.5f;
            int level = Mathf.Clamp(Mathf.CeilToInt(dmg * 12f), 0, 12);
            var cracks = level > 0 ? Cracks(size, ox, od, level) : null;
            Color32 stone = bio.stone, outline = U.C(84, 52, 12), crackC = U.C(64, 34, 8), deep = U.C(186, 126, 28);
            Color32 gold = b == Block.BigGold6 ? U.C(255, 200, 70) : b == Block.BigGold4 ? U.C(244, 186, 52) : U.C(232, 178, 48);

            for (int py = 0; py < 16; py++)
                for (int px = 0; px < 16; px++)
                {
                    int X = cx * 16 + px, Y = cyUp * 16 + py;
                    float qx = Mathf.Max(Mathf.Abs(X - half) - (half - R), 0f);
                    float qy = Mathf.Max(Mathf.Abs(Y - half) - (half - R), 0f);
                    float d = Mathf.Sqrt(qx * qx + qy * qy);
                    Color32 c;
                    if (d > R)
                    {
                        // corners show the rock it's embedded in
                        float s0 = 0.8f + 0.3f * U.H01(X >> 2, Y >> 2, 5);
                        c = U.Mul(stone, s0);
                    }
                    else if (d > R - 1.2f) c = outline;
                    else
                    {
                        float light = 0.72f + 0.45f * ((Y / (float)N) * 0.6f + (1f - X / (float)N) * 0.4f);
                        c = U.Mul(gold, light);
                        float n1 = U.H01((X >> 2) + ox * 31, (Y >> 2) + od * 17, 71);
                        float n2 = U.H01(X >> 1, Y >> 1, 72 + ox);
                        if (n1 > 0.8f) c = U.C(255, 228, 112);                                   // raised nuggets
                        else if (U.H01((X >> 3) + ox, (Y >> 2) + od, 74) < 0.12f) c = deep;      // darker veins
                        if (b != Block.BigGold2 && U.H01((X >> 2) + 3, (Y >> 2) + od, 73) > (b == Block.BigGold6 ? 0.88f : 0.93f))
                            c = ((X >> 2) + (Y >> 2)) % 2 == 0 ? U.C(110, 240, 230) : U.C(240, 90, 120);   // embedded gems
                        if (n2 > 0.975f) c = U.C(255, 252, 225);                                 // sparkle
                        if (cracks != null && cracks[Y * N + X]) c = crackC;
                    }
                    buf[(py0 + py) * stride + px0 + px] = c;
                }
        }
    }
}
