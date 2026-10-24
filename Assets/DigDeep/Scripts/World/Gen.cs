using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // A hand-shaped region stamped into the procedural terrain (shops, puzzle altar chambers...).
    // Coordinates use depth (positive = down). Rects are (x, depth, w, h) and must cover every cell TileAt returns.
    public abstract class Carve
    {
        public bool spawned;
        public readonly List<RectInt> rects = new List<RectInt>();
        public abstract int TileAt(int x, int depth);   // a Block as int, or -1 if this carve doesn't own the cell
        public abstract int AnchorDepth { get; }        // spawn entities when this row's chunk is generated
        public abstract void Spawn(Game g, System.Random r);

        // Helper: a room with air inside and a brick floor (one wider on each side) as its last row.
        protected static int Room(int x, int d, int x0, int ceil, int w, int h)
        {
            if (d < ceil || d > ceil + h - 1) return -1;
            if (d == ceil + h - 1) return (x >= x0 - 1 && x <= x0 + w) ? (int)Block.Brick : -1;
            return (x >= x0 && x < x0 + w) ? (int)Block.Air : -1;
        }
    }

    public class ShopRoom : Carve
    {
        public const int Wd = 18, Ht = 7;
        public int x0, top;     // top = depth of the ceiling row

        public ShopRoom(int x0, int top)
        {
            this.x0 = x0; this.top = top;
            rects.Add(new RectInt(x0 - 4, top - 3, Wd + 8, Ht + 3));
        }

        public int FloorY => -(top + Ht - 1);
        public int CenterX => x0 + Wd / 2;
        public override int AnchorDepth => top + Ht - 1;
        public override void Spawn(Game g, System.Random r) => ShopBuilder.Spawn(this, r);

        public override int TileAt(int x, int depth)
        {
            int ry = depth - top;
            int cx = CenterX;
            if (ry < 0)
            {
                if (ry >= -3 && (x == cx - 1 || x == cx)) return (int)Block.Air; // shaft into the ceiling hole
                return -1;
            }
            bool inX = x >= x0 && x < x0 + Wd;
            if (ry == 0)
            {
                if (!inX) return -1;
                return (x == cx - 1 || x == cx) ? (int)Block.Air : (int)Block.Brick;
            }
            if (ry == Ht - 1)
            {
                if (x >= x0 - 4 && x < x0 + Wd + 4) return (int)Block.Brick;
                return -1;
            }
            if (ry >= Ht) return -1;
            if (inX)
            {
                if (x == x0 || x == x0 + Wd - 1) return ry >= Ht - 3 ? (int)Block.Air : (int)Block.Brick;
                return (int)Block.Air;
            }
            if (ry >= Ht - 3 && x >= x0 - 4 && x < x0 + Wd + 4) return (int)Block.Air; // side corridors
            return -1;
        }
    }

    // Pure function terrain: the same (x,y) always yields the same block for a given seed.
    public static class Gen
    {
        public const int W = 84;   // ~3 screens wide, bedrock walls at both sides
        const int MaxDepth = ArenaRoom.Final;   // the world ends under Ornin's throne
        static float o1, o2, o3, o4, o5;
        static int seed;
        public static int Seed => seed;
        public static readonly List<Carve> Carves = new List<Carve>();
        public static readonly List<ShopRoom> Shops = new List<ShopRoom>();
        static readonly Dictionary<int, List<Carve>> buckets = new Dictionary<int, List<Carve>>();

        public static void Init(int s)
        {
            seed = s;
            var r = new System.Random(s);
            o1 = (float)r.NextDouble() * 4000f; o2 = (float)r.NextDouble() * 4000f;
            o3 = (float)r.NextDouble() * 4000f; o4 = (float)r.NextDouble() * 4000f;
            o5 = (float)r.NextDouble() * 4000f;
            Carves.Clear(); Shops.Clear(); buckets.Clear();
            // boss arenas first: one at the end of each biome, then every 100 depth
            int bi = 0;
            foreach (int bd in ArenaRoom.Depths(MaxDepth)) Add(new ArenaRoom(bd, bi++));
            int d = 30;
            while (d < MaxDepth)
            {
                var shop = new ShopRoom(6 + r.Next(W - 12 - ShopRoom.Wd), d);
                if (!TryAdd(shop)) { d += 6; continue; }
                Shops.Add(shop);
                d += 50 + r.Next(-6, 12);
            }
            EventGen.Build(r, MaxDepth);
        }

        static void Add(Carve c)
        {
            Carves.Add(c);
            foreach (var rc in c.rects)
                for (int b = rc.y >> 4; b <= (rc.y + rc.height - 1) >> 4; b++)
                {
                    if (!buckets.TryGetValue(b, out var l)) buckets[b] = l = new List<Carve>();
                    if (!l.Contains(c)) l.Add(c);
                }
        }

        // Adds the carve unless it would overlap (with a margin) anything already placed.
        public static bool TryAdd(Carve c, int margin = 3)
        {
            foreach (var rc in c.rects)
            {
                if (rc.x < 2 || rc.x + rc.width > W - 2) return false;
                var big = new RectInt(rc.x - margin, rc.y - margin, rc.width + margin * 2, rc.height + margin * 2);
                for (int b = Mathf.Max(0, big.y) >> 4; b <= (big.y + big.height - 1) >> 4; b++)
                {
                    if (!buckets.TryGetValue(b, out var l)) continue;
                    foreach (var o in l)
                        foreach (var orc in o.rects)
                            if (big.Overlaps(orc)) return false;
                }
            }
            Add(c);
            return true;
        }

        public static int CarveAt(int x, int depth)
        {
            if (depth < 0 || !buckets.TryGetValue(depth >> 4, out var l)) return -1;
            for (int i = 0; i < l.Count; i++)
            {
                int t = l[i].TileAt(x, depth);
                if (t >= 0) return t;
            }
            return -1;
        }

        public static bool InCarve(int x, int depth) => CarveAt(x, depth) >= 0;

        static bool Open(int x, int y, int depth)
        {
            if (depth <= 3) return false;
            float n = U.Fbm(x * 0.075f + o1, y * 0.11f + o1 * 0.7f);
            float th = Mathf.Lerp(0.64f, 0.575f, Mathf.Clamp01(depth / 260f));
            if (depth < 14) th += (14 - depth) * 0.008f;
            if (n > th) return true;
            float w = Mathf.Abs(Mathf.PerlinNoise(x * 0.045f + o2, y * 0.06f + o2 * 0.5f) - 0.5f);
            return w < 0.022f + Mathf.Min(depth, 200) * 0.00005f;
        }

        // ---------------------------------------------------------------- starting camp
        // The run starts in a cave with a campfire. Above the camp's floor there is only dirt and
        // unbreakable dark matter: an organic, wavy ceiling seals off the way up, and dark-matter veins
        // spread down through the dirt. Nothing worth digging up for - the only way is down.
        public const int CampFloor = 14;          // depth of the camp's floor row
        public const int SurfaceTop = 12;         // world y of the top row of earth over the dark-matter crust
        public const int CampCx = W / 2;

        public static bool InCampCave(int x, int depth)
        {
            if (depth < 7 || depth >= CampFloor) return false;
            float dx = (x - CampCx) / 12.5f, dy = (depth - 11.3f) / 3.6f;
            return dx * dx + dy * dy < 1f;
        }

        static Block CampTile(int x, int depth)
        {
            if (depth <= 0) return Block.DarkMatter;
            if (InCampCave(x, depth)) return Block.Air;
            float top = 4.5f + (Mathf.PerlinNoise(x * 0.12f + o1, 7.3f) - 0.5f) * 5f;   // wavy dark-matter ceiling
            if (depth <= top) return Block.DarkMatter;
            if (depth < CampFloor && U.Fbm(x * 0.2f + o2, depth * 0.26f + o3) > 0.6f) return Block.DarkMatter;   // veins
            return Block.Dirt;
        }

        public static bool RawSolid(int x, int y)
        {
            if (x <= 1 || x >= W - 2) return true;
            if (y >= 1) return true;
            int d = -y;
            if (d >= ArenaRoom.Bottom) return true;
            if (d <= CampFloor) return Tiles.Solid(CampTile(x, d));
            int t = CarveAt(x, d);
            if (t >= 0) return Tiles.Solid((Block)t);
            if (BigOre.Find(x, d, out _, out _, out _, out _)) return true;
            return !Open(x, y, d);
        }

        static Block Material(int x, int y, int depth)
        {
            float rock = U.Fbm(x * 0.13f + o3, y * 0.13f + o3 * 0.3f);
            float th = 0.64f - Mathf.Min(depth, 260) * 0.001f;
            Block m = Block.Dirt;
            if (rock > th) m = Block.Stone;
            if (depth > 60 && rock > th + 0.18f) m = Block.HardStone;
            if (depth > 220 && rock > th + 0.30f) m = Block.Obsidian;
            if (m == Block.Dirt)
            {
                float s = Mathf.PerlinNoise(x * 0.16f + o4, y * 0.16f + o4 * 0.2f);
                if (Biomes.Index(depth) == Biomes.Magma && s > 0.6f) return Block.Magma;
                if (s > 0.74f) return Block.Sand;
            }
            // the forgotten kingdom: chunks of ancient dwarf masonry everywhere
            if (Biomes.Index(depth) >= Biomes.Kingdom && m != Block.Dirt && U.H01(x >> 2, y >> 1, 91) > 0.5f) return Block.Brick;
            return m;
        }

        public static Block Base(int x, int y)
        {
            if (x <= 1 || x >= W - 2) return Block.Bedrock;
            if (y > SurfaceTop) return Block.Air;                 // the open sky (only ever reached on the way to the gods)
            if (y >= SurfaceTop - 1) return Block.Dirt;           // a skin of earth on top of the crust: the surface
            if (y >= 1) return Block.DarkMatter;
            int depth = -y;
            if (depth >= ArenaRoom.Bottom) return Block.Bedrock;   // bottom of the world
            if (depth <= CampFloor) return CampTile(x, depth);

            int ct = CarveAt(x, depth);
            if (ct >= 0) return (Block)ct;
            if (BigOre.Find(x, depth, out var big, out _, out _, out _)) return big;

            float r = U.H01(x, y, seed + 11);

            if (Open(x, y, depth))
            {
                // pools of water settle in the bottoms of caves (frozen solid in the Crystal Depths, none in the Magma Core)
                int wbi = Biomes.Index(depth);
                if (depth > 18 && wbi != Biomes.Magma && Mathf.PerlinNoise(x * 0.11f + o5 * 2f, y * 0.11f + 7.3f) > (wbi == Biomes.Jungle ? 0.55f : wbi == Biomes.Storm ? 0.5f : 0.62f) &&
                    (RawSolid(x, y - 1) || (!RawSolid(x, y - 1) && RawSolid(x, y - 2))))
                    return wbi == Biomes.Crystal ? Block.Ice : Block.Water;
                if (RawSolid(x, y - 1))
                {
                    if (depth > 6 && r < 0.011f) return Block.Chest;
                    if (depth > 8 && r < 0.0145f) return Block.Crate;
                    int bi = Biomes.Index(depth);
                    if (bi == Biomes.Magma && Mathf.PerlinNoise(x * 0.2f + o5, y * 0.2f) > 0.6f) return Block.Lava;   // the Magma Core: plenty
                    if (bi != Biomes.Magma && bi != Biomes.Jungle && bi != Biomes.Storm && depth > 40 && Mathf.PerlinNoise(x * 0.09f + o4, y * 0.09f + 3.1f) > 0.8f) return Block.Lava;   // rare pockets elsewhere
                    float sp = depth < 22 ? 0f : Mathf.Min(0.015f + depth * 0.00018f, 0.09f);
                    if (r > 1f - sp) return Block.Spikes;
                }
                return Block.Air;
            }

            Block m = Material(x, y, depth);
            if (m == Block.Dirt || m == Block.Stone || m == Block.HardStone)
            {
                float gold = 0.022f + Mathf.Min(depth, 300) * 0.00004f;
                if (r < gold) return Block.Gold;
                float gem = depth < 45 ? 0f : Mathf.Min((depth - 45) * 0.00008f, 0.014f);
                if (r < gold + gem) return Block.Gem;
                float tnt = depth < 18 ? 0f : Mathf.Min(0.003f + depth * 0.00002f, 0.011f);
                if (r < gold + gem + tnt) return Block.TNT;
                // buried treasure only where something solid holds it up (never in a cave ceiling)
                if (r > 0.997f && depth > 5 && RawSolid(x, y - 1)) return Block.Chest;
                if (r > 0.9978f && depth > 20 && RawSolid(x, y - 1)) return Block.Crate;
                float r2 = U.H01(x, y, seed + 23);
                if (Biomes.Index(depth) >= Biomes.Crystal)
                {
                    if (r2 < 0.035f) return Block.Crystal;
                    if (depth > 165 && r2 < 0.035f + Mathf.Min((depth - 165) * 0.0001f, 0.02f)) return Block.Volatile;
                }
                if (depth > 10 && r2 > 1f - Mathf.Min(0.006f + depth * 0.00004f, 0.02f) && RawSolid(x, y - 1)) return Block.Boulder;
            }
            return m;
        }
    }
}
