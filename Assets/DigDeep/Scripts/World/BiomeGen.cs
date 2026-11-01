using UnityEngine;

namespace DigDeep
{
    // How the newer biomes shape their caves and fill their rock (Gen asks here for depths inside them).
    //  CLOUD REACHES  - clouds packed together like puffs drifting past each other: narrow tunnels between them, now and
    //                   then a wider gap. Some are ice, some carry a clump of earth on top (with gold about it), some have
    //                   gold caught in them. Lots of water pooled on them.
    //  CRIMSON FOREST - ordinary caves (its trees, and the blood in its pools, come later: see BiomeBuild, World.PoolType)
    //  SUNKEN BEACH   - big caves of sand and sandstone, the bottoms full of warm sea water
    //  NEON CANOPY    - ordinary caves (its canopy and vines come later)
    //  CORAL REEF     - flooded caves, coral growing through the rock
    public static class BiomeGen
    {
        static float O => Gen.Off;

        // the cloud puffs: nearest puff centre (a jittered grid, wider than tall) and how near the edge between two puffs
        static void Puff(int x, int y, out int hx, out int hy, out float relY, out float edge)
        {
            const float CW = 9f, CH = 5.5f;
            float u = x / CW, v = y / CH;
            int cu = Mathf.FloorToInt(u), cv = Mathf.FloorToInt(v);
            float d1 = 99f, d2 = 99f, py1 = 0f; hx = hy = 0;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int qu = cu + i, qv = cv + j;
                    float pu = qu + 0.15f + U.H01(qu, qv, 401) * 0.7f, pv = qv + 0.15f + U.H01(qu, qv, 402) * 0.7f;
                    float d = (u - pu) * (u - pu) + (v - pv) * (v - pv);
                    if (d < d1) { d2 = d1; d1 = d; hx = qu; hy = qv; py1 = pv; }
                    else if (d < d2) d2 = d;
                }
            edge = Mathf.Sqrt(d2) - Mathf.Sqrt(d1);
            relY = v - py1;   // above the puff's middle when positive
        }

        public static bool Open(int x, int y, int depth, int bi)
        {
            switch (bi)
            {
                case Biomes.Cloud:
                    {
                        Puff(x, y, out _, out _, out _, out float edge);
                        if (edge < 0.16f) return true;   // the narrow gaps between the puffs
                        return Mathf.PerlinNoise(x * 0.045f + O, y * 0.07f + O * 0.3f) > 0.74f;   // and now and then a wide one
                    }
                case Biomes.Beach:
                    {
                        float n = U.Fbm(x * 0.06f + O, y * 0.09f + O * 0.7f);
                        return n > 0.56f || Gen.Open(x, y, depth);   // bigger caves than usual
                    }
                case Biomes.Reef:
                    {
                        float n = U.Fbm(x * 0.065f + O * 1.3f, y * 0.09f + O * 0.2f);
                        return n > 0.57f || Gen.Open(x, y, depth);
                    }
                default: return Gen.Open(x, y, depth);
            }
        }

        // what fills an open cell (true: decided here)
        public static bool OpenFill(int x, int y, int depth, int bi, float r, out Block b)
        {
            b = Block.Air;
            bool floor = Gen.RawSolid(x, y - 1);
            float vk = World.Void ? 0.5f : 1f;
            switch (bi)
            {
                case Biomes.Cloud:
                    if ((floor || Gen.RawSolid(x, y - 2)) && Mathf.PerlinNoise(x * 0.09f + O, y * 0.09f + 3.3f) > 0.5f) { b = Block.Water; return true; }   // rain pooled on the clouds
                    if (floor && r < 0.004f * vk * Gen.LuckMul) b = Block.Chest;
                    return true;
                case Biomes.Beach:
                    if ((floor || Gen.RawSolid(x, y - 2) || Gen.RawSolid(x, y - 3)) && Mathf.PerlinNoise(x * 0.05f + O, y * 0.05f + 1.7f) > 0.4f) { b = Block.Water; return true; }   // the sea, in every hollow
                    if (floor && r < 0.004f * vk * Gen.LuckMul) b = Block.Chest;
                    return true;
                case Biomes.Reef:
                    // nearly all of it under water: only the odd pocket of air up under the rock
                    if (!Gen.RawSolid(x, y + 1) || Mathf.PerlinNoise(x * 0.04f + O, y * 0.08f) < 0.74f) { b = Block.Water; if (floor && r < 0.004f * vk * Gen.LuckMul) b = Block.Chest; return true; }
                    if (floor && r < 0.004f * vk * Gen.LuckMul) b = Block.Chest;
                    return true;
            }
            return false;
        }

        // what the rock is made of (true: decided here)
        public static bool Solid(int x, int y, int depth, int bi, float r, out Block b)
        {
            b = Block.Dirt;
            float gold = 0.022f + Mathf.Min(depth, 300) * 0.00004f;
            switch (bi)
            {
                case Biomes.Cloud:
                    {
                        Puff(x, y, out int hx, out int hy, out float relY, out _);
                        float kind = U.H01(hx, hy, 403);
                        if (kind < 0.1f) { b = Block.Ice; return true; }                                   // a puff frozen solid
                        if (kind < 0.3f && relY > 0.05f)                                                    // a clump of earth riding on top of a cloud
                        {
                            b = r < gold * 1.6f ? Block.Gold : relY > 0.35f && U.H01(x, y, 404) < 0.25f ? Block.Stone : Block.Dirt;
                            return true;
                        }
                        b = r < gold ? Block.CloudGold : r > 0.996f ? Block.TNT : Block.Cloud;              // gold caught in the cloud instead of the rock
                        return true;
                    }
                case Biomes.Beach:
                    {
                        float rock = U.Fbm(x * 0.13f + O, y * 0.13f + O * 0.3f);
                        if (rock < 0.58f && Gen.RawSolid(x, y - 1)) { b = Block.Sand; return true; }          // sand wherever it has something to rest on
                        b = r < gold ? Block.Gold : r > 0.998f && Gen.RawSolid(x, y - 1) ? Block.Boulder : Block.Stone;   // sandstone (and the odd coconut)
                        return true;
                    }
                case Biomes.Reef:
                    {
                        float c = Mathf.PerlinNoise(x * 0.16f + O, y * 0.16f + 9.1f);
                        if (c > 0.6f) { b = Block.Coral; return true; }
                        return false;
                    }
            }
            return false;
        }
    }
}
