using UnityEngine;

namespace DigDeep
{
    // Worn-down shelters around chests and weapon crates (like Terraria's underground cabins): a timber cabin, a
    // tumbledown brick ruin, or a little shrine. No doors: the sides stand open at the floor. Blocks are knocked out
    // here and there, and a back wall (its own sprite, with holes in it) shows someone lived here once.
    public static class Shelter
    {
        public enum Kind { Cabin, Ruin, Shrine }

        // the back walls, painted into the cave background behind each shelter (so whatever stands inside shows in front)
        public struct Wall { public Kind kind; public int seed, x0, y0, w, h; }
        static readonly System.Collections.Generic.Dictionary<long, Wall> walls = new System.Collections.Generic.Dictionary<long, Wall>();
        static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
        public static void Reset() => walls.Clear();

        public static void Build(World w, Chunk c, int x, int ly, System.Random r)
        {
            const int W = World.W;
            double k = r.NextDouble();
            var kind = k < 0.55 ? Kind.Cabin : k < 0.85 ? Kind.Ruin : Kind.Shrine;
            int half = kind == Kind.Shrine ? 2 : 3 + r.Next(2);   // half the inside width
            int ih = kind == Kind.Shrine ? 3 : 3 + r.Next(2);     // inside height
            int roofH = kind == Kind.Cabin ? half + 1 : 1;
            if (ly + ih + roofH >= World.CH || x - half - 1 < 2 || x + half + 1 > W - 3) return;
            Block wall = kind == Kind.Cabin ? Block.Wood : Block.Brick;
            int sbi = Biomes.Index(-(c.Y0 + ly));
            if (sbi == Biomes.Reef) wall = Block.Coral;          // sunken coral houses in the reef
            else if (sbi == Biomes.Cloud && kind != Kind.Cabin) wall = Block.Ice;   // ice ruins up in the clouds
            float worn = kind == Kind.Ruin ? 0.3f : 0.06f;

            // clear the inside (leave treasure and the chest itself)
            for (int yy = ly; yy < ly + ih; yy++)
                for (int xx = x - half; xx <= x + half; xx++)
                {
                    int i = yy * W + xx;
                    Block b = c.t[i];
                    if (b == Block.Chest || b == Block.Crate || b == Block.Supply || b == Block.Gold || b == Block.Gem || BigOre.Is(b)) continue;
                    if (Tiles.Def(b).unbreakable || Gen.InCarve(xx, -(c.Y0 + yy))) continue;
                    c.t[i] = Block.Air; c.lt[i] = Fluids.None; c.liq[i] = 0f; c.fl[i] = 0;
                }
            // the floor
            for (int xx = x - half - 1; xx <= x + half + 1; xx++) Put(c, xx, ly - 1, kind == Kind.Cabin ? Block.Wood : wall, 0f, r, true);
            if (kind == Kind.Shrine)
            {
                // two pillars and a lintel over the chest
                for (int yy = ly; yy < ly + ih; yy++) { Put(c, x - half, yy, wall, 0.05f, r, false); Put(c, x + half, yy, wall, 0.05f, r, false); }
                for (int xx = x - half - 1; xx <= x + half + 1; xx++) Put(c, xx, ly + ih, wall, 0.1f, r, false);
            }
            else
            {
                // walls, open at the bottom two rows (no doors)
                for (int yy = ly + 2; yy < ly + ih; yy++) { Put(c, x - half - 1, yy, wall, worn, r, false); Put(c, x + half + 1, yy, wall, worn, r, false); }
                if (kind == Kind.Cabin)
                {
                    // a pitched roof: each row a block narrower, up to a ridge
                    for (int row = 0; row < roofH; row++)
                    {
                        int hw = half + 2 - row;
                        if (hw < 0) break;
                        for (int xx = x - hw; xx <= x + hw; xx++) Put(c, xx, ly + ih + row, wall, row == 0 ? 0f : 0.04f, r, false);
                    }
                }
                else
                {
                    for (int xx = x - half - 1; xx <= x + half + 1; xx++) Put(c, xx, ly + ih, wall, worn, r, false);
                }
            }
            // the back wall (painted into the background: see TileDraw.Bg)
            var wl = new Wall { kind = kind, seed = r.Next(), x0 = x - half, y0 = c.Y0 + ly, w = half * 2 + 1, h = ih };
            for (int yy = 0; yy < ih; yy++)
                for (int xx = 0; xx < wl.w; xx++)
                    walls[Key(wl.x0 + xx, wl.y0 + yy)] = wl;
        }

        // the back wall's pixel at this spot, if there is one (false where it has crumbled away)
        public static bool WallPx(int wx, int wy, int gx, int gy, out Color32 c)
        {
            c = default;
            if (walls.Count == 0 || !walls.TryGetValue(Key(wx, wy), out var wl)) return false;
            int lx = gx - wl.x0 * 16, ly = gy - wl.y0 * 16, W = wl.w * 16, H = wl.h * 16;
            // a couple of holes where it has rotted or crumbled (the cave shows through)
            int holes = wl.kind == Kind.Ruin ? 3 : 1;
            for (int n = 0; n < holes; n++)
            {
                uint hh = U.Hash(wl.seed, n, 911);
                int hx = (int)(hh % (uint)W), hy = (int)((hh >> 10) % (uint)H), hr = 3 + (int)((hh >> 20) % 5);
                float dd = ((lx - hx) * (lx - hx) + (ly - hy) * (ly - hy)) / (float)(hr * hr);
                if (dd < 1f - U.H01(lx >> 1, ly >> 1, wl.seed) * 0.35f) return false;
            }
            Color32 a, b2, d, l;
            if (wl.kind == Kind.Cabin) { a = U.C(92, 60, 40); b2 = U.C(78, 50, 34); d = U.C(46, 30, 22); l = U.C(122, 84, 54); }
            else { a = U.C(80, 74, 88); b2 = U.C(68, 62, 76); d = U.C(40, 36, 48); l = U.C(104, 98, 112); }
            switch (wl.kind)
            {
                case Kind.Cabin:
                    {
                        // horizontal planks with seams, posts at the corners, a little window with a cross
                        int wx0 = W / 2 - 7, wy0 = H - 20;
                        if (lx >= wx0 && lx < wx0 + 14 && ly >= wy0 && ly < wy0 + 12)
                        {
                            bool frame = lx == wx0 || lx == wx0 + 13 || ly == wy0 || ly == wy0 + 11 || lx == wx0 + 6 || lx == wx0 + 7 || ly == wy0 + 5;
                            c = frame ? l : (lx - wx0 + ly - wy0) % 7 == 0 ? U.C(70, 96, 130) : U.C(34, 46, 70);
                            return true;
                        }
                        if (lx < 3 || lx >= W - 3) { c = lx == 1 || lx == W - 2 ? l : d; return true; }
                        int plank = ly / 5;
                        c = ly % 5 == 0 ? d : (U.Hash(plank, lx / 13, wl.seed) % 3 == 0 ? b2 : a);
                        if (lx % 26 == (plank * 9) % 26) c = d;   // plank ends
                        if (ly % 5 == 4) c = Pal.Shade(c, 1.12f);   // a lit edge on each plank
                        return true;
                    }
                case Kind.Ruin:
                    {
                        // old dressed stone, cracked, with an arched window
                        float u = (lx - W * 0.5f) / 7f, v = (ly - H * 0.6f) / 7f;
                        if (u * u + Mathf.Max(0f, v) * Mathf.Max(0f, v) < 1f && ly > H * 0.6f - 7f) { c = U.C(30, 26, 38); return true; }
                        int course = ly / 6, off = (course & 1) * 6;
                        c = (ly % 6 == 0 || (lx + off) % 12 == 0) ? d : (U.Hash((lx + off) / 12, course, wl.seed) % 4 == 0 ? b2 : a);
                        if ((lx * 3 + ly * 5 + wl.seed) % 41 == 0) c = d;   // cracks
                        return true;
                    }
                default:
                    {
                        // a carved arch with a glowing glyph in it
                        float u = (lx - W * 0.5f) / (W * 0.42f), v = (ly - H * 0.5f) / (H * 0.5f);
                        float q = u * u + Mathf.Max(0f, v) * Mathf.Max(0f, v);
                        bool inArch = q < 1f && ly < H - 2;
                        c = inArch ? b2 : a;
                        if (inArch && Mathf.Abs(q - 0.9f) < 0.1f) c = l;
                        int gxm = lx - W / 2, gym = ly - H / 2;
                        if (Mathf.Abs(gxm) <= 2 && Mathf.Abs(gym) <= 3 && (gxm == 0 || gym == 0 || Mathf.Abs(gxm) == Mathf.Abs(gym))) c = U.C(150, 230, 255);
                        return true;
                    }
            }
        }

        static void Put(Chunk c, int x, int ly, Block b, float worn, System.Random r, bool force)
        {
            if (x < 2 || x >= World.W - 2 || ly < 0 || ly >= World.CH) return;
            if (r.NextDouble() < worn) return;   // a block that has fallen out
            int i = ly * World.W + x;
            Block cur = c.t[i];
            if (cur == Block.Chest || cur == Block.Crate || cur == Block.Supply || Tiles.Def(cur).unbreakable || Gen.InCarve(x, -(c.Y0 + ly))) return;
            if (!force && cur != Block.Air && !Tiles.Def(cur).soft) return;
            c.t[i] = b; c.lt[i] = Fluids.None; c.liq[i] = 0f; c.fl[i] = 0;
            c.hp[i] = (byte)World.MaxHp(b, c.Y0 + ly);
        }
    }

}
