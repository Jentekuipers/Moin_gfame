using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    public class Chunk
    {
        public int ci;
        public Block[] t = new Block[World.W * World.CH];
        public byte[] hp = new byte[World.W * World.CH];
        public Texture2D tex;
        public Color32[] px;
        public GameObject go;
        public bool dirty;
        public Dictionary<int, List<int>> stains;
        public float[] liq = new float[World.W * World.CH];   // fluid amount per cell (0..1)
        public byte[] lt = new byte[World.W * World.CH];      // fluid type per cell (see Fluids)
        public byte[] fl = new byte[World.W * World.CH];      // plant growth in an empty cell: 1 grass, 2 vine (both burn)
        public bool[] seen = new bool[World.W * World.CH];    // has the player ever had this cell in sight? (it stays dimly lit)
        public int Y0 => ci * World.CH;
    }

    // Grid world, streamed in 16-row chunks. Each chunk is one texture painted pixel-by-pixel.
    public class World
    {
        public const int CH = 16, PX = 16, W = Gen.W;
        const float E = 0.001f;
        public enum Hit { Empty, Damaged, Broke, Unbreakable }

        readonly Dictionary<int, Chunk> chunks = new Dictionary<int, Chunk>();
        readonly HashSet<long> pendingFall = new HashSet<long>();
        readonly HashSet<long> anchored = new HashSet<long>();   // the floor under altars and shrines: never breaks

        public void Anchor(int x, int y) => anchored.Add(Key(x, y));
        public bool Anchored(int x, int y) => anchored.Contains(Key(x, y));
        readonly Transform root;
        readonly List<Chunk> scratch = new List<Chunk>();

        public World(Transform root) { this.root = root; }

        static long Key(int x, int y) => ((long)y << 16) ^ (long)(x & 0xFFFF);

        // ------------------------------------------------------------ data

        public Chunk GetChunk(int ci)
        {
            if (!chunks.TryGetValue(ci, out var c)) c = Generate(ci);
            return c;
        }

        Chunk Generate(int ci)
        {
            var c = new Chunk { ci = ci };
            chunks[ci] = c;
            for (int ly = 0; ly < CH; ly++)
                for (int x = 0; x < W; x++)
                {
                    int y = c.Y0 + ly;
                    Block b = Gen.Base(x, y);
                    // liquids aren't blocks: the generator's markers become fluid in an empty cell
                    if (b == Block.Lava || b == Block.Water)
                    {
                        c.lt[ly * W + x] = b == Block.Lava ? Fluids.Lava : Fluids.Water;
                        c.liq[ly * W + x] = 1f;
                        b = Block.Air;
                    }
                    c.t[ly * W + x] = b;
                    c.hp[ly * W + x] = (byte)MaxHp(b, y);
                }
            GrowPlants(c);
            BuildWood(c);
            GuaranteeCrate(c);
            Game.I.OnChunkGenerated(c);
            return c;
        }

        // At least one weapon crate in every 32-row band (two chunks), on a cave floor, below the camp.
        void GuaranteeCrate(Chunk c)
        {
            if ((c.ci & 1) != 0) return;
            int topDepth = -(c.Y0 + CH - 1);
            if (topDepth < 22) return;
            for (int i = 0; i < c.t.Length; i++) if (c.t[i] == Block.Crate) return;
            var r = new System.Random(Gen.Seed * 977 + c.ci * 131);
            var spots = new List<int>();
            for (int ly = 1; ly < CH; ly++)
                for (int x = 3; x < W - 3; x++)
                {
                    int i = ly * W + x;
                    if (c.t[i] != Block.Air || c.lt[i] != Fluids.None) continue;
                    if (!Tiles.Solid(c.t[(ly - 1) * W + x]) || c.t[(ly - 1) * W + x] == Block.Spikes) continue;
                    if (Gen.InCarve(x, -(c.Y0 + ly))) continue;
                    spots.Add(i);
                }
            if (spots.Count == 0) return;
            int k = spots[r.Next(spots.Count)];
            c.t[k] = Block.Crate;
            c.hp[k] = (byte)MaxHp(Block.Crate, c.Y0 + k / W);
        }

        public static int MaxHp(Block b, int y)
        {
            int hp = Tiles.Def(b).hp;
            int depth = -y;
            if (b == Block.Stone || b == Block.HardStone || b == Block.Obsidian || b == Block.Gold || b == Block.Gem)
                hp += Mathf.Max(0, depth) / 110;
            return Mathf.Clamp(hp, 0, 255);
        }

        public bool TryChunk(int ci, out Chunk c) => chunks.TryGetValue(ci, out c);

        // ------------------------------------------------------------ fluids

        public byte LiquidType(int x, int y)
        {
            if (x < 0 || x >= W || !chunks.TryGetValue(U.FloorDiv(y, CH), out var c)) return Fluids.None;
            return c.lt[(y - c.Y0) * W + x];
        }

        public float LiquidAmount(int x, int y)
        {
            if (x < 0 || x >= W || !chunks.TryGetValue(U.FloorDiv(y, CH), out var c)) return 0f;
            return c.liq[(y - c.Y0) * W + x];
        }

        public void SetLiquid(int x, int y, byte t, float a)
        {
            if (x < 0 || x >= W || !chunks.TryGetValue(U.FloorDiv(y, CH), out var c)) return;
            int i = (y - c.Y0) * W + x;
            c.lt[i] = a > 0f ? t : Fluids.None;
            c.liq[i] = a > 0f ? a : 0f;
        }

        public void AddSteam(int x, int y, float a)
        {
            if (x < 0 || x >= W || !chunks.TryGetValue(U.FloorDiv(y, CH), out var c)) return;
            int i = (y - c.Y0) * W + x;
            if (Fluids.Blocks(c.t[i])) return;
            byte t = c.lt[i];
            if (t == Fluids.Water || t == Fluids.Lava) return;
            c.lt[i] = Fluids.Steam;
            c.liq[i] = Mathf.Min(1f, (t == Fluids.Steam ? c.liq[i] : 0f) + a);
        }

        // Which fluid is this point actually inside? (below the surface of the liquid in its cell)
        public byte FluidAtPoint(Vector2 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
            byte t = LiquidType(x, y);
            if (t == Fluids.None) return t;
            float a = LiquidAmount(x, y);
            if (t == Fluids.Steam) return a > 0.25f ? t : Fluids.None;
            if (LiquidType(x, y + 1) == t) return t;
            return p.y - y <= a + 0.05f ? t : Fluids.None;
        }

        // Above the clouds (the fight with the God) there is nothing but sky.
        public static bool SkyMode;
        public static bool Void;   // the void world: a run through black versions of everything (see Game.EnterVoid)
        public const int SkyFloor = 40;

        public Block Get(int x, int y)
        {
            if (SkyMode && y > SkyFloor) return Block.Air;
            if (x < 0 || x >= W) return Block.Bedrock;
            var c = GetChunk(U.FloorDiv(y, CH));
            return c.t[(y - c.Y0) * W + x];
        }

        // Read without generating: falls back to the pure generator for unknown chunks.
        public Block Peek(int x, int y)
        {
            if (SkyMode && y > SkyFloor) return Block.Air;
            if (x < 0 || x >= W) return Block.Bedrock;
            if (chunks.TryGetValue(U.FloorDiv(y, CH), out var c)) return c.t[(y - c.Y0) * W + x];
            return Gen.Base(x, y);
        }

        // vision memory: cells the player has had in line of sight stay dimly lit
        public bool Seen(int x, int y)
        {
            if (x < 0 || x >= W || !chunks.TryGetValue(U.FloorDiv(y, CH), out var c)) return false;
            return c.seen[(y - c.Y0) * W + x];
        }

        public void MarkSeen(int x, int y)
        {
            if (x < 0 || x >= W || !chunks.TryGetValue(U.FloorDiv(y, CH), out var c)) return;
            c.seen[(y - c.Y0) * W + x] = true;
        }

        public void PokeRaw(Chunk c, int x, int y, Block b)
        {
            int i = (y - c.Y0) * W + x;
            c.t[i] = b; c.hp[i] = (byte)MaxHp(b, y);
        }

        public static bool GhostPlates;   // set while a boss moves: metal ledges don't block it
        public bool Solid(int x, int y)
        {
            Block b = Get(x, y);
            return Tiles.Solid(b) && !(GhostPlates && b == Block.Plate);
        }

        // ------------------------------------------------------------ grass and vines (they live in empty cells)

        public const byte Grass = 1, Vine = 2;

        public byte Flags(int x, int y)
        {
            if (x < 0 || x >= W || !chunks.TryGetValue(U.FloorDiv(y, CH), out var c)) return 0;
            return c.fl[(y - c.Y0) * W + x];
        }

        public void ClearFlags(int x, int y, byte mask)
        {
            if (x < 0 || x >= W || !chunks.TryGetValue(U.FloorDiv(y, CH), out var c)) return;
            int i = (y - c.Y0) * W + x;
            if ((c.fl[i] & mask) == 0) return;
            c.fl[i] = (byte)(c.fl[i] & ~mask);
            RedrawCell(x, y);
            // a vine that loses its top falls apart below too
            if ((mask & Vine) != 0 && (Flags(x, y - 1) & Vine) != 0) ClearFlags(x, y - 1, Vine);
        }

        static float GrassChance(int biome, Block below)
        {
            bool earth = below == Block.Dirt;
            bool rock = below == Block.Stone || below == Block.HardStone || below == Block.Brick || below == Block.GildBrick || below == Block.Wood;
            switch (biome)
            {
                case Biomes.Topsoil: return earth ? 0.45f : 0f;
                case Biomes.Jungle: return earth ? 0.95f : rock ? 0.7f : 0f;
                case Biomes.Fungal: return earth ? 0.2f : 0f;
                case Biomes.Kingdom: return earth ? 0.25f : 0f;
                default: return 0f;
            }
        }

        void GrowPlants(Chunk c)
        {
            for (int ly = 0; ly < CH; ly++)
                for (int x = 2; x < W - 2; x++)
                {
                    int i = ly * W + x, y = c.Y0 + ly;
                    if (c.t[i] != Block.Air || c.lt[i] != Fluids.None || y >= 0) continue;
                    int bio = Biomes.Index(-y);
                    Block below = ly > 0 ? c.t[i - W] : Peek(x, y - 1);   // (the real block, which may already be dug out)
                    // grass in patches: a coarse noise decides where it grows at all
                    float patch = U.H01(x >> 2, y >> 1, 401);
                    if (patch < 0.75f && U.H01(x, y, 402) < GrassChance(bio, below)) c.fl[i] |= Grass;
                    // vines (grass ropes) hang from ceilings, mostly in the jungle
                    Block above = ly < CH - 1 ? c.t[i + W] : Peek(x, y + 1);
                    float vineChance = bio == Biomes.Jungle ? 0.22f : bio == Biomes.Topsoil ? 0.02f : 0f;
                    if (Tiles.Solid(above) && above != Block.Spikes && U.H01(x, y, 403) < vineChance)
                    {
                        int len = 2 + (int)(U.Hash(x, y, 404) % 6);
                        for (int k = 0; k < len && ly - k >= 0; k++)
                        {
                            int j = (ly - k) * W + x;
                            if (c.t[j] != Block.Air || c.lt[j] != Fluids.None) break;
                            c.fl[j] |= Vine;
                        }
                    }
                }
        }

        // Dwarves were here: wooden scaffolds and platforms in the caves, and little wooden huts over some chests.
        void BuildWood(Chunk c)
        {
            int top = -(c.Y0 + CH - 1);
            if (top < 18) return;   // not in the camp
            var r = new System.Random(Gen.Seed * 613 + c.ci * 59);
            // huts over chests and weapon crates
            for (int ly = 0; ly < CH - 3; ly++)
                for (int x = 3; x < W - 3; x++)
                {
                    Block b = c.t[ly * W + x];
                    if ((b != Block.Chest && b != Block.Crate) || r.NextDouble() > 0.5) continue;
                    if (Gen.InCarve(x, -(c.Y0 + ly))) continue;
                    // posts either side, a roof two blocks up
                    for (int k = 0; k <= 1; k++) { WoodAt(c, x - 1, ly + k); WoodAt(c, x + 1, ly + k); }
                    for (int dx = -2; dx <= 2; dx++) WoodAt(c, x + dx, ly + 2);
                }
            // scaffolds: a plank platform on two posts
            int tries = 3 + r.Next(3);
            for (int k = 0; k < tries; k++)
            {
                int len = 3 + r.Next(4);
                int x0 = 3 + r.Next(W - 6 - len);
                int py = 4 + r.Next(CH - 6);
                bool ok = true;
                for (int x = x0; x < x0 + len && ok; x++) ok = c.t[py * W + x] == Block.Air && c.lt[py * W + x] == Fluids.None && !Gen.InCarve(x, -(c.Y0 + py));
                if (!ok) continue;
                // both posts must reach the floor within 4 blocks
                int legL = Leg(c, x0, py), legR = Leg(c, x0 + len - 1, py);
                if (legL < 1 || legR < 1) continue;
                for (int x = x0; x < x0 + len; x++) c.t[py * W + x] = Block.Wood;
                for (int d = 1; d <= legL; d++) c.t[(py - d) * W + x0] = Block.Wood;
                for (int d = 1; d <= legR; d++) c.t[(py - d) * W + x0 + len - 1] = Block.Wood;
                for (int x = x0; x < x0 + len; x++) c.hp[py * W + x] = (byte)MaxHp(Block.Wood, c.Y0 + py);
                for (int d = 1; d <= legL; d++) c.hp[(py - d) * W + x0] = (byte)MaxHp(Block.Wood, c.Y0 + py - d);
                for (int d = 1; d <= legR; d++) c.hp[(py - d) * W + x0 + len - 1] = (byte)MaxHp(Block.Wood, c.Y0 + py - d);
            }
        }

        // how many empty cells below (x, py) until solid ground, or -1 if it's too far / not in this chunk
        static int Leg(Chunk c, int x, int py)
        {
            for (int d = 1; d <= 4; d++)
            {
                int ly = py - d;
                if (ly < 0) return -1;
                Block b = c.t[ly * W + x];
                if (Tiles.Solid(b)) return d - 1 >= 1 ? d - 1 : -1;
                if (b != Block.Air || c.lt[ly * W + x] != Fluids.None) return -1;
            }
            return -1;
        }

        static void WoodAt(Chunk c, int x, int ly)
        {
            if (x < 1 || x >= W - 1 || ly < 0 || ly >= CH) return;
            int i = ly * W + x;
            if (c.t[i] != Block.Air || c.lt[i] != Fluids.None) return;
            c.t[i] = Block.Wood;
            c.hp[i] = (byte)MaxHp(Block.Wood, c.Y0 + ly);
            c.fl[i] = 0;
        }

        public void SetHp(int x, int y, int hp)
        {
            if (x < 0 || x >= W || (SkyMode && y > SkyFloor)) return;
            var c = GetChunk(U.FloorDiv(y, CH));
            c.hp[(y - c.Y0) * W + x] = (byte)Mathf.Clamp(hp, 1, 255);
            RedrawCell(x, y);
        }

        // Knock a resting boulder loose: it becomes a rolling physics body (keeping its cracks).
        public bool PushBoulder(int x, int y, Vector2 impulse)
        {
            if (Get(x, y) != Block.Boulder) return false;
            int hp = Hp(x, y);
            pendingFall.Remove(Key(x, y));
            Set(x, y, Block.Air);
            Game.I.Spawn(new BoulderBody(x, y, impulse, hp));
            return true;
        }

        public int Hp(int x, int y)
        {
            if (x < 0 || x >= W || (SkyMode && y > SkyFloor)) return 999;
            var c = GetChunk(U.FloorDiv(y, CH));
            return c.hp[(y - c.Y0) * W + x];
        }

        public void Set(int x, int y, Block b)
        {
            if (x < 0 || x >= W || (SkyMode && y > SkyFloor)) return;
            var c = GetChunk(U.FloorDiv(y, CH));
            int i = (y - c.Y0) * W + x;
            // a solid block landing in liquid pushes the liquid up out of the way
            if (Fluids.Blocks(b) && c.lt[i] != Fluids.None)
            {
                byte lt0 = c.lt[i]; float la = c.liq[i];
                c.lt[i] = Fluids.None; c.liq[i] = 0f;
                if (lt0 != Fluids.Steam && !Solid(x, y + 1))
                {
                    byte ut = LiquidType(x, y + 1);
                    if (ut == Fluids.None || ut == lt0 || ut == Fluids.Steam) SetLiquid(x, y + 1, lt0, Mathf.Min(1f, (ut == lt0 ? LiquidAmount(x, y + 1) : 0f) + la));
                }
            }
            c.t[i] = b;
            c.hp[i] = (byte)MaxHp(b, y);
            if (Tiles.Solid(b)) c.fl[i] = 0;   // a block filling the cell crushes any grass or vine there
            if (!Tiles.Solid(b) && c.stains != null) c.stains.Remove(i);
            RedrawAround(x, y);
            if (!Tiles.Solid(b))
            {
                CheckFall(x, y + 1);
                ClearFlags(x, y + 1, Grass);   // the grass that grew on this block goes with it
                ClearFlags(x, y - 1, Vine);    // and so does a vine that hung from it
            }
        }

        public Hit Damage(int x, int y, int amount, bool byPlayer, float goldMul = 1f, bool loot = true)
        {
            Block b = Get(x, y);
            var d = Tiles.Def(b);
            if (b == Block.Air || b == Block.Lava) return Hit.Empty;
            if (d.unbreakable || (anchored.Count > 0 && anchored.Contains(Key(x, y)))) return Hit.Unbreakable;
            if (BigOre.Is(b)) return DamageBig(x, y, amount, byPlayer);
            var c = GetChunk(U.FloorDiv(y, CH));
            int i = (y - c.Y0) * W + x;
            int hp = c.hp[i] - amount;
            if (hp > 0)
            {
                c.hp[i] = (byte)hp;
                RedrawCell(x, y);
                return Hit.Damaged;
            }
            Break(x, y, byPlayer, goldMul, loot);
            return Hit.Broke;
        }

        public void Break(int x, int y, bool byPlayer, float goldMul = 1f, bool loot = true)
        {
            Block b = Get(x, y);
            if (b == Block.Air) return;
            if (anchored.Count > 0 && anchored.Contains(Key(x, y))) return;
            if (BigOre.Is(b)) { DamageBig(x, y, 999999, byPlayer); return; }
            var d = Tiles.Def(b);
            var bio = Biomes.At(x, y);
            pendingFall.Remove(Key(x, y));
            Set(x, y, Block.Air);
            var g = Game.I;
            Vector2 cpos = new Vector2(x + 0.5f, y + 0.5f);
            g.fx.Debris(cpos, TileDraw.DebrisColor(b, bio), b == Block.Dirt || b == Block.Sand ? 9 : 12);
            if (loot)
            {
                if (d.goldMax > 0)
                {
                    int n = Random.Range(d.goldMin, d.goldMax + 1);
                    g.SpawnGold(cpos, Mathf.Max(1, Mathf.RoundToInt(n * goldMul)));
                }
                if (b == Block.Chest) g.OpenChest(cpos);
                if (b == Block.Supply) g.OpenSupply(cpos);
                if (b == Block.Crate) g.OpenCrate(cpos, Crates.RarityAt(x, y));
            }
            if (b == Block.TNT) g.Spawn(new Fuse(x, y, b, 0.2f, 3.1f, 7));
            if (b == Block.Volatile) g.Spawn(new Fuse(x, y, b, 0.55f, 1.9f, 4));
            if (byPlayer) g.OnPlayerBrokeBlock(x, y, b);
        }

        // Something slammed into (x, y). TNT goes off when hit or landed on hard enough.
        public void Impact(int x, int y, float speed)
        {
            if (speed < 20f) return;   // TNT only goes off from a really hard knock (or a shot, a blast, a slam)
            if (Get(x, y) == Block.TNT) Break(x, y, false);
        }

        // Is there a clear line of sight between two points?
        public bool LineClear(Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) return true;
            int n = Mathf.CeilToInt(len / 0.3f);
            for (int i = 1; i < n; i++)
            {
                Vector2 p = a + d * (i / (float)n);
                if (Tiles.Solid(Peek(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)))) return false;
            }
            return true;
        }

        // Distance until a ray hits something solid.
        public float RayDist(Vector2 o, Vector2 dir, float max)
        {
            for (float t = 0f; t < max; t += 0.1f)
            {
                Vector2 p = o + dir * t;
                if (Tiles.Solid(Peek(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)))) return t;
            }
            return max;
        }

        // ------------------------------------------------------------ stains: blood and scorch marks that stay

        public void Stain(Vector2 p, Color c, int r)
        {
            int gx = Mathf.FloorToInt(p.x * PX), gy = Mathf.FloorToInt(p.y * PX);
            byte a = (byte)Mathf.Clamp(c.a * 255f, 40f, 255f);
            for (int dy = -r + 1; dy < r; dy++)
                for (int dx = -r + 1; dx < r; dx++)
                    if (dx * dx + dy * dy < r * r) StainPx(gx + dx, gy + dy, c, a);
        }

        void StainPx(int gx, int gy, Color c, byte alpha)
        {
            int x = U.FloorDiv(gx, PX), y = U.FloorDiv(gy, PX);
            if (x < 0 || x >= W) return;
            if (!chunks.TryGetValue(U.FloorDiv(y, CH), out var ch)) return;
            int i = (y - ch.Y0) * W + x;
            Block b = ch.t[i];
            if (!Tiles.Solid(b) || BigOre.Is(b) || b == Block.Chest || b == Block.Crate || b == Block.Supply) return;
            int lx = gx - x * PX, ly = gy - y * PX;
            if (ch.stains == null) ch.stains = new Dictionary<int, List<int>>();
            if (!ch.stains.TryGetValue(i, out var list)) { list = new List<int>(); ch.stains[i] = list; }
            if (list.Count >= 120) list.RemoveAt(0);
            int r5 = (int)(Mathf.Clamp01(c.r) * 31f), g5 = (int)(Mathf.Clamp01(c.g) * 31f), b5 = (int)(Mathf.Clamp01(c.b) * 31f);
            int v = lx | (ly << 4) | ((alpha >> 4) << 8) | (r5 << 12) | (g5 << 17) | (b5 << 22);
            list.Add(v);
            if (ch.px != null)
            {
                int k = (ly + (y - ch.Y0) * PX) * W * PX + x * PX + lx;
                ch.px[k] = Blend(ch.px[k], v);
                ch.dirty = true;
            }
        }

        static Color32 Blend(Color32 under, int v)
        {
            float t = Mathf.Clamp01(((v >> 8) & 15) / 15f) * 0.85f;
            float r = ((v >> 12) & 31) * (255f / 31f), g = ((v >> 17) & 31) * (255f / 31f), b = ((v >> 22) & 31) * (255f / 31f);
            return new Color32((byte)(under.r + (r - under.r) * t), (byte)(under.g + (g - under.g) * t), (byte)(under.b + (b - under.b) * t), 255);
        }

        void ApplyStains(Chunk c, int x, int ly)
        {
            if (c.stains == null || !Tiles.Solid(c.t[ly * W + x]) || !c.stains.TryGetValue(ly * W + x, out var list)) return;
            foreach (int v in list)
            {
                int lx = v & 15, py = (v >> 4) & 15;
                int k = (py + ly * PX) * W * PX + x * PX + lx;
                c.px[k] = Blend(c.px[k], v);
            }
        }

        void CheckFall(int x, int y)
        {
            if (!chunks.ContainsKey(U.FloorDiv(y, CH))) return;
            Block b = Get(x, y);
            if (anchored.Contains(Key(x, y))) return;
            if (!Tiles.Def(b).gravity && b != Block.Spikes && b != Block.Chest && b != Block.Crate && b != Block.Supply) return;
            long k = Key(x, y);
            if (pendingFall.Contains(k)) return;
            pendingFall.Add(k);
            Game.I.Spawn(new Faller(x, y, b));
        }

        public void ClearPending(int x, int y) => pendingFall.Remove(Key(x, y));

        // Crates take their rarity from where they were generated; one that fell keeps its old rarity.
        readonly Dictionary<long, Rarity> crateRarity = new Dictionary<long, Rarity>();
        public void SetCrateRarity(int x, int y, Rarity r) => crateRarity[Key(x, y)] = r;
        public bool CrateRarity(int x, int y, out Rarity r) => crateRarity.TryGetValue(Key(x, y), out r);

        // ------------------------------------------------------------ collision

        public bool BoxSolid(Vector2 p, Vector2 h)
        {
            int x0 = Mathf.FloorToInt(p.x - h.x + E), x1 = Mathf.FloorToInt(p.x + h.x - E);
            int y0 = Mathf.FloorToInt(p.y - h.y + E), y1 = Mathf.FloorToInt(p.y + h.y - E);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (Solid(x, y)) return true;
            return false;
        }

        public bool MoveX(ref Vector2 p, Vector2 h, float dx, out Vector2Int hit)
        {
            hit = new Vector2Int(int.MinValue, 0);
            if (dx == 0f) return false;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(dx) / 0.45f));
            float sdx = dx / steps;
            int y0 = Mathf.FloorToInt(p.y - h.y + E), y1 = Mathf.FloorToInt(p.y + h.y - E);
            for (int s = 0; s < steps; s++)
            {
                float nx = p.x + sdx;
                if (sdx > 0)
                {
                    int cx = Mathf.FloorToInt(nx + h.x - E);
                    for (int y = y0; y <= y1; y++)
                        if (Solid(cx, y)) { p.x = cx - h.x; hit = new Vector2Int(cx, y); return true; }
                }
                else
                {
                    int cx = Mathf.FloorToInt(nx - h.x + E);
                    for (int y = y0; y <= y1; y++)
                        if (Solid(cx, y)) { p.x = cx + 1 + h.x; hit = new Vector2Int(cx, y); return true; }
                }
                p.x = nx;
            }
            return false;
        }

        public static bool DropThrough;   // set while the player holds down: spear ledges don't hold them

        // standing ground: solid blocks, or a spear ledge right under the feet
        public bool Ground(Vector2 p, Vector2 h)
        {
            if (BoxSolid(p, h)) return true;
            if (Spear.All.Count == 0) return false;
            float feet = p.y - h.y;   // (p is already nudged down a touch by the caller)
            return Spear.Under(p, h, feet + 0.1f, feet, out _);
        }

        public bool MoveY(ref Vector2 p, Vector2 h, float dy, out Vector2Int hit)
        {
            hit = new Vector2Int(int.MinValue, 0);
            if (dy == 0f) return false;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(dy) / 0.45f));
            float sdy = dy / steps;
            int x0 = Mathf.FloorToInt(p.x - h.x + E), x1 = Mathf.FloorToInt(p.x + h.x - E);
            for (int s = 0; s < steps; s++)
            {
                float ny = p.y + sdy;
                if (sdy > 0)
                {
                    int cy = Mathf.FloorToInt(ny + h.y - E);
                    // check the column nearest the body centre first so head-bumps dig the block you're under
                    int first = Mathf.Clamp(Mathf.FloorToInt(p.x), x0, x1);
                    if (Solid(first, cy)) { p.y = cy - h.y; hit = new Vector2Int(first, cy); return true; }
                    for (int x = x0; x <= x1; x++)
                        if (Solid(x, cy)) { p.y = cy - h.y; hit = new Vector2Int(x, cy); return true; }
                }
                else
                {
                    // spear ledges: you land on them from above only (walk and jump through them otherwise)
                    if (Spear.All.Count > 0 && Spear.Under(p, h, p.y - h.y, ny - h.y, out float top)) { p.y = top + h.y; return true; }
                    int cy = Mathf.FloorToInt(ny - h.y + E);
                    for (int x = x0; x <= x1; x++)
                        if (Solid(x, cy)) { p.y = cy + 1 + h.y; hit = new Vector2Int(x, cy); return true; }
                }
                p.y = ny;
            }
            return false;
        }

        // ------------------------------------------------------------ rendering

        public void Stream(float camY, float halfH)
        {
            int genLo = U.FloorDiv(Mathf.FloorToInt(camY - halfH - 26), CH);
            int genHi = U.FloorDiv(Mathf.FloorToInt(camY + halfH + 4), CH);
            int visLo = U.FloorDiv(Mathf.FloorToInt(camY - halfH - 2), CH);
            int visHi = U.FloorDiv(Mathf.FloorToInt(camY + halfH + 2), CH);
            genHi = Mathf.Min(genHi, 0); visHi = Mathf.Min(visHi, 0);
            for (int ci = genHi; ci >= genLo; ci--) GetChunk(ci);
            for (int ci = visHi; ci >= visLo; ci--)
            {
                var c = GetChunk(ci);
                if (c.tex == null) BuildTex(c);
            }
            scratch.Clear();
            foreach (var c in chunks.Values)
                if (c.tex != null && (c.ci < visLo - 2 || c.ci > visHi + 2)) scratch.Add(c);
            foreach (var c in scratch) ReleaseTex(c);
        }

        void BuildTex(Chunk c)
        {
            c.px = new Color32[W * PX * CH * PX];
            c.tex = new Texture2D(W * PX, CH * PX, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int ly = 0; ly < CH; ly++)
                for (int x = 0; x < W; x++) DrawCell(c, x, ly);
            c.tex.SetPixels32(c.px);
            c.tex.Apply(false);
            c.go = new GameObject("Chunk " + c.ci);
            c.go.transform.SetParent(root, false);
            c.go.transform.position = new Vector3(0, c.Y0, 0);
            var sp = Sprite.Create(c.tex, new Rect(0, 0, c.tex.width, c.tex.height), Vector2.zero, PX, 0, SpriteMeshType.FullRect);
            Art.AddSR(c.go, sp, 0);
            c.dirty = false;
        }

        void ReleaseTex(Chunk c)
        {
            if (c.go != null)
            {
                var sr = c.go.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null) Object.Destroy(sr.sprite);
                Object.Destroy(c.go);
            }
            if (c.tex != null) Object.Destroy(c.tex);
            c.go = null; c.tex = null; c.px = null; c.dirty = false;
        }

        // ------------------------------------------------------------ save / continue

        public void CaptureTo(RunSave r)
        {
            foreach (var c in chunks.Values)
            {
                var tb = new byte[c.t.Length];
                for (int i = 0; i < tb.Length; i++) tb[i] = (byte)c.t[i];
                r.chunks.Add(new ChunkSave { ci = c.ci, t = SaveGame.Pack(tb), hp = SaveGame.Pack(c.hp), lt = SaveGame.Pack(c.lt), liq = SaveGame.PackFloats(c.liq), fl = SaveGame.Pack(c.fl) });
            }
        }

        // Put a saved chunk back exactly as it was (instead of generating a fresh one).
        public void Inject(ChunkSave s)
        {
            var c = new Chunk { ci = s.ci };
            var tb = SaveGame.Unpack(s.t);
            for (int i = 0; i < c.t.Length && i < tb.Length; i++) c.t[i] = (Block)tb[i];
            var hb = SaveGame.Unpack(s.hp);
            System.Array.Copy(hb, c.hp, Mathf.Min(hb.Length, c.hp.Length));
            var lb = SaveGame.Unpack(s.lt);
            System.Array.Copy(lb, c.lt, Mathf.Min(lb.Length, c.lt.Length));
            SaveGame.UnpackFloats(s.liq, c.liq);
            var fb = SaveGame.Unpack(s.fl);
            System.Array.Copy(fb, c.fl, Mathf.Min(fb.Length, c.fl.Length));
            chunks[c.ci] = c;
            Game.I.OnChunkGenerated(c);
        }

        public void DestroyAll()
        {
            foreach (var c in chunks.Values) ReleaseTex(c);
            chunks.Clear();
        }

        bool OpenAt(int x, int y) => !Tiles.Solid(Peek(x, y));

        // ------------------------------------------------------------ big gold deposits (shared health pool)

        readonly Dictionary<long, int> bigHp = new Dictionary<long, int>();

        float BigDamage(int ox, int od, int size)
        {
            if (!bigHp.TryGetValue(Key(ox, -od), out int hp)) return 0f;
            return 1f - hp / (float)BigOre.MaxHp(size, od);
        }

        Hit DamageBig(int x, int y, int amount, bool byPlayer)
        {
            if (!BigOre.Find(x, -y, out var b, out int ox, out int od, out int size))
            {
                Set(x, y, Block.Air);
                return Hit.Broke;
            }
            long key = Key(ox, -od);
            int max = BigOre.MaxHp(size, od);
            if (!bigHp.TryGetValue(key, out int hp)) hp = max;
            hp -= amount;
            var center = new Vector2(ox + size * 0.5f, 1 - od - size * 0.5f);
            if (hp > 0)
            {
                bigHp[key] = hp;
                if (byPlayer) Game.I.BigOreHit(new Vector2(x + 0.5f, y + 0.5f), size);
                for (int j = 0; j < size; j++)
                    for (int i = 0; i < size; i++) RedrawCell(ox + i, -(od + j));
                return Hit.Damaged;
            }
            bigHp.Remove(key);
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                    if (Get(ox + i, -(od + j)) == b) Set(ox + i, -(od + j), Block.Air);
            Game.I.BigOreBreak(center, size, od);
            return Hit.Broke;
        }

        void DrawCell(Chunk c, int x, int ly)
        {
            int y = c.Y0 + ly;
            int i = ly * W + x;
            Block b = c.t[i];
            if (b == Block.Air && y > Gen.SurfaceTop)
            {
                // the open sky above the surface: see-through, so the stars show
                for (int py = 0; py < PX; py++)
                    for (int px = 0; px < PX; px++) c.px[(ly * PX + py) * W * PX + x * PX + px] = new Color32(0, 0, 0, 0);
                return;
            }
            if (BigOre.Is(b) && BigOre.Find(x, -y, out _, out int box, out int bod, out int bsize))
            {
                int cyUp = (bod + bsize - 1) - (-y);
                BigOre.Draw(c.px, W * PX, x * PX, ly * PX, b, x - box, cyUp, bsize, BigDamage(box, bod, bsize), box, bod, Biomes.At(x, y));
                return;
            }
            int mask = 0;
            if (OpenAt(x, y + 1)) mask |= TileDraw.UP;
            if (OpenAt(x + 1, y)) mask |= TileDraw.RIGHT;
            if (OpenAt(x, y - 1)) mask |= TileDraw.DOWN;
            if (OpenAt(x - 1, y)) mask |= TileDraw.LEFT;
            if (OpenAt(x - 1, y + 1)) mask |= TileDraw.UL;
            if (OpenAt(x + 1, y + 1)) mask |= TileDraw.UR;
            if (OpenAt(x + 1, y - 1)) mask |= TileDraw.DR;
            if (OpenAt(x - 1, y - 1)) mask |= TileDraw.DL;
            if (Peek(x, y + 1) == b) mask |= TileDraw.UPSAME;
            int max = MaxHp(b, y);
            float dmg = max > 0 && Tiles.Solid(b) ? 1f - c.hp[i] / (float)max : 0f;
            TileDraw.Draw(c.px, W * PX, x * PX, ly * PX, b, x, y, mask, dmg, Biomes.At(x, y), c.fl[i]);
            ApplyStains(c, x, ly);
        }

        public void RedrawCell(int x, int y)
        {
            if (x < 0 || x >= W) return;
            if (!chunks.TryGetValue(U.FloorDiv(y, CH), out var c) || c.px == null) return;
            DrawCell(c, x, y - c.Y0);
            c.dirty = true;
        }

        public void RedrawAround(int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    RedrawCell(x + dx, y + dy);
        }

        public void ApplyDirty()
        {
            foreach (var c in chunks.Values)
            {
                if (!c.dirty || c.tex == null) continue;
                c.tex.SetPixels32(c.px);
                c.tex.Apply(false);
                c.dirty = false;
            }
        }
    }
}
