using UnityEngine;

namespace DigDeep
{
    public enum Block : byte
    {
        Air, Dirt, Sand, Stone, HardStone, Obsidian, Bedrock, Gold, Gem, Chest,
        Boulder, TNT, Crystal, Volatile, Lava, Spikes, Brick, Magma, Seal, DarkMatter, BigGold2, BigGold4, BigGold6, Crate, Water, Ice, Plate, Wood, GildBrick, Supply, COUNT
    }

    public class TileDef
    {
        public string name;
        public int hp = 1;
        public bool solid = true, soft, gravity, unbreakable, hazard, explosive, burnable;
        public int goldMin, goldMax;
        public float light, lightI = 0.6f;
    }

    public static class Tiles
    {
        public static readonly TileDef[] D = new TileDef[(int)Block.COUNT];

        static Tiles()
        {
            D[(int)Block.Air] = new TileDef { name = "AIR", solid = false, hp = 0 };
            D[(int)Block.Dirt] = new TileDef { name = "DIRT", hp = 1, soft = true };
            D[(int)Block.Sand] = new TileDef { name = "SAND", hp = 1, soft = true, gravity = true };
            D[(int)Block.Stone] = new TileDef { name = "STONE", hp = 3 };
            D[(int)Block.HardStone] = new TileDef { name = "HARD STONE", hp = 6 };
            D[(int)Block.Obsidian] = new TileDef { name = "OBSIDIAN", hp = 12 };
            D[(int)Block.Bedrock] = new TileDef { name = "VOID", hp = 999, unbreakable = true };
            D[(int)Block.Gold] = new TileDef { name = "GOLD ORE", hp = 3, goldMin = 3, goldMax = 6, light = 1.3f, lightI = 0.35f };
            D[(int)Block.Gem] = new TileDef { name = "GEM ORE", hp = 5, goldMin = 10, goldMax = 18, light = 1.7f, lightI = 0.5f };
            D[(int)Block.Chest] = new TileDef { name = "CHEST", hp = 2, light = 2.4f, lightI = 0.65f, burnable = true };
            D[(int)Block.Boulder] = new TileDef { name = "BOULDER", hp = 4, gravity = true };
            D[(int)Block.TNT] = new TileDef { name = "TNT", hp = 1, explosive = true };
            D[(int)Block.Crystal] = new TileDef { name = "CRYSTAL", hp = 4, goldMin = 2, goldMax = 4, light = 2.2f, lightI = 0.55f };
            D[(int)Block.Volatile] = new TileDef { name = "VOLATILE CRYSTAL", hp = 3, explosive = true, light = 2.4f, lightI = 0.7f };
            D[(int)Block.Lava] = new TileDef { name = "LAVA", solid = false, hp = 0, unbreakable = true, hazard = true, light = 3.2f, lightI = 0.9f };
            D[(int)Block.Spikes] = new TileDef { name = "SPIKES", solid = false, hp = 1, hazard = true };
            D[(int)Block.Brick] = new TileDef { name = "BRICK", hp = 4 };
            D[(int)Block.Supply] = new TileDef { name = "SUPPLY CRATE", hp = 2, burnable = true };
            D[(int)Block.GildBrick] = new TileDef { name = "GILDED BRICK", hp = 4, light = 0.9f, lightI = 0.25f };
            D[(int)Block.Magma] = new TileDef { name = "MAGMA ROCK", hp = 5, hazard = true, light = 1.5f, lightI = 0.45f };
            D[(int)Block.BigGold2] = new TileDef { name = "GOLD VEIN", hp = 255, light = 1.5f, lightI = 0.35f };
            D[(int)Block.BigGold4] = new TileDef { name = "MOTHER LODE", hp = 255, light = 2.1f, lightI = 0.45f };
            D[(int)Block.BigGold6] = new TileDef { name = "KING'S HOARD", hp = 255, light = 2.8f, lightI = 0.55f };
            D[(int)Block.Crate] = new TileDef { name = "WEAPON CRATE", hp = 2, light = 2.4f, lightI = 0.65f, burnable = true };
            D[(int)Block.Wood] = new TileDef { name = "DWARVEN WOOD", hp = 2, burnable = true };
            D[(int)Block.Water] = new TileDef { name = "WATER", solid = false, hp = 0 };   // generation marker only: becomes fluid
            D[(int)Block.Plate] = new TileDef { name = "SHOP PLATE", hp = 999, unbreakable = true };
            D[(int)Block.Ice] = new TileDef { name = "ICE", hp = 2, light = 0.6f, lightI = 0.2f };
            D[(int)Block.DarkMatter] = new TileDef { name = "DARK MATTER", hp = 999, unbreakable = true };
            D[(int)Block.Seal] = new TileDef { name = "ARENA SEAL", hp = 999, unbreakable = true, light = 1.1f, lightI = 0.4f };
        }

        public static TileDef Def(Block b) => D[(int)b];
        public static bool Solid(Block b) => D[(int)b].solid;
    }

    public class Biome
    {
        public string name;
        public int start;
        public Color32 dirt, stone, hard, bg, rim, plant, crystal;
    }

    public static class Biomes
    {
        // biome indices (in depth order)
        public const int Topsoil = 0, Jungle = 1, Fungal = 2, Crystal = 3, Storm = 4, Magma = 5, Kingdom = 6;

        public static readonly Biome[] All =
        {
            // palettes after the moodboard: warm terracotta and lavender stone over deep violet, teal-green moss,
            // a steaming green jungle, magenta fungus, cold cyan crystal, ember reds, and a cream-and-teal ancient kingdom.
            new Biome { name = "TOPSOIL", start = 0,
                dirt = U.C(156, 84, 62), stone = U.C(118, 104, 142), hard = U.C(84, 72, 122), bg = U.C(34, 22, 48),
                rim = U.C(150, 224, 104), plant = U.C(84, 190, 120), crystal = U.C(96, 214, 232) },
            new Biome { name = "OVERGROWN JUNGLE", start = 100,
                dirt = U.C(104, 82, 52), stone = U.C(82, 112, 96), hard = U.C(56, 82, 76), bg = U.C(14, 34, 30),
                rim = U.C(160, 246, 96), plant = U.C(74, 200, 92), crystal = U.C(190, 255, 120) },
            new Biome { name = "FUNGAL HOLLOWS", start = 195,
                dirt = U.C(128, 48, 104), stone = U.C(110, 78, 138), hard = U.C(76, 52, 110), bg = U.C(36, 12, 44),
                rim = U.C(255, 112, 196), plant = U.C(96, 232, 196), crystal = U.C(214, 128, 255) },
            new Biome { name = "CRYSTAL DEPTHS", start = 252,
                dirt = U.C(52, 74, 138), stone = U.C(82, 102, 154), hard = U.C(56, 62, 118), bg = U.C(12, 18, 44),
                rim = U.C(104, 242, 255), plant = U.C(84, 222, 232), crystal = U.C(140, 230, 255) },
            new Biome { name = "STORM CAVERNS", start = 315,
                dirt = U.C(58, 62, 92), stone = U.C(86, 94, 128), hard = U.C(48, 54, 86), bg = U.C(10, 12, 30),
                rim = U.C(255, 236, 90), plant = U.C(120, 220, 255), crystal = U.C(255, 240, 130) },
            new Biome { name = "MAGMA CORE", start = 368,
                dirt = U.C(118, 42, 44), stone = U.C(104, 66, 78), hard = U.C(74, 42, 58), bg = U.C(38, 10, 20),
                rim = U.C(255, 156, 64), plant = U.C(255, 112, 64), crystal = U.C(255, 140, 100) },
            new Biome { name = "FORGOTTEN KINGDOM", start = 518,
                dirt = U.C(150, 112, 92), stone = U.C(200, 184, 152), hard = U.C(108, 138, 140), bg = U.C(18, 28, 34),
                rim = U.C(255, 222, 142), plant = U.C(110, 206, 156), crystal = U.C(255, 228, 140) },
        };

        // THE VOID WORLD: every biome drowned in black and violet, and the forgotten kingdom is the void itself
        public static Biome[] Cur => World.Void ? VoidAll : All;
        static Biome[] voidAll;
        public static Biome[] VoidAll
        {
            get
            {
                if (voidAll != null) return voidAll;
                voidAll = new Biome[All.Length];
                for (int i = 0; i < All.Length; i++)
                {
                    var b = All[i];
                    voidAll[i] = i == Kingdom
                        ? new Biome { name = "THE VOID", start = b.start, dirt = U.C(18, 10, 28), stone = U.C(32, 20, 50), hard = U.C(10, 6, 18), bg = U.C(3, 1, 6),
                                      rim = U.C(196, 96, 255), plant = U.C(96, 44, 160), crystal = U.C(226, 130, 255) }
                        : new Biome { name = "VOID " + b.name, start = b.start,
                                      dirt = Color32.Lerp(b.dirt, U.C(22, 12, 32), 0.72f), stone = Color32.Lerp(b.stone, U.C(30, 18, 46), 0.72f),
                                      hard = Color32.Lerp(b.hard, U.C(14, 8, 22), 0.78f), bg = Color32.Lerp(b.bg, U.C(4, 2, 8), 0.78f),
                                      rim = Color32.Lerp(b.rim, U.C(176, 84, 255), 0.6f), plant = Color32.Lerp(b.plant, U.C(120, 60, 200), 0.6f),
                                      crystal = Color32.Lerp(b.crystal, U.C(206, 100, 255), 0.6f) };
                }
                return voidAll;
            }
        }

        // the biomes are half again as long as they once were; creatures still toughen at the old pace (see Enemy)
        public const float Stretch = 1.5f;

        public static int Index(int depth)
        {
            // the void: every biome at once, jumbled in bands (only its own kingdom at the bottom stays put)
            if (World.Void && depth >= 20 && depth < All[Kingdom].start)
                return (int)(U.Hash(depth / 14, Gen.Seed, 913) % (uint)Kingdom);
            int i = 0;
            for (int k = 0; k < All.Length; k++) if (depth >= All[k].start) i = k;
            return i;
        }

        public static Biome At(int x, int y)
        {
            int depth = -y;
            float j = (U.H01(x >> 2, y >> 1, 77) - 0.5f) * 10f;
            return Cur[Index(depth + (int)j)];
        }
    }
}
