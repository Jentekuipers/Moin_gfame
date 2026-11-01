using UnityEngine;

namespace DigDeep
{
    // The newer biomes' big pieces, built into each chunk as it's made: great red trees in the crimson forest, palms
    // with coconuts and huts on stilts over the water on the beach, coral branches in the reef, glowing canopy in the
    // neon jungle.
    public static class BiomeBuild
    {
        const int W = World.W, CH = World.CH;

        public static void Decorate(World w, Chunk c)
        {
            int top = -(c.Y0 + CH - 1);
            if (top < 20) return;
            int bi = Biomes.Index(-(c.Y0 + CH / 2));
            if (bi < Biomes.Cloud) return;
            var r = new System.Random(Gen.Seed * 811 + c.ci * 97);
            switch (bi)
            {
                case Biomes.Crimson: Trees(c, r, false); break;
                case Biomes.Neon: Trees(c, r, true); Canopy(c, r); break;
                case Biomes.Beach: StiltHuts(c, r); Piers(c, r); Shacks(c, r); break;
                case Biomes.Reef: CoralBranches(c, r); break;
            }
        }

        static bool Air(Chunk c, int x, int ly) => x >= 2 && x < W - 2 && ly >= 0 && ly < CH && c.t[ly * W + x] == Block.Air && c.lt[ly * W + x] == Fluids.None;
        static bool Wet(Chunk c, int x, int ly) => x >= 2 && x < W - 2 && ly >= 0 && ly < CH && c.t[ly * W + x] == Block.Air && c.lt[ly * W + x] != Fluids.None;
        static bool Free(Chunk c, int x, int ly) => x >= 2 && x < W - 2 && ly >= 0 && ly < CH && c.t[ly * W + x] == Block.Air;
        static bool InCarve(Chunk c, int x, int ly) => Gen.InCarve(x, -(c.Y0 + ly)) || Gen.InShop(x, -(c.Y0 + ly));

        static void Set(Chunk c, int x, int ly, Block b, bool overWater = false)
        {
            if (x < 2 || x >= W - 2 || ly < 0 || ly >= CH) return;
            int i = ly * W + x;
            if (c.t[i] != Block.Air || (!overWater && c.lt[i] != Fluids.None) || InCarve(c, x, ly)) return;
            c.t[i] = b; c.lt[i] = Fluids.None; c.liq[i] = 0f; c.fl[i] = 0;
            c.hp[i] = (byte)World.MaxHp(b, c.Y0 + ly);
        }

        // a cave floor with this much clear air above it
        static bool FloorWithRoom(Chunk c, int x, int ly, int h)
        {
            if (ly < 1 || !Tiles.Solid(c.t[(ly - 1) * W + x]) || c.t[(ly - 1) * W + x] == Block.Leaf) return false;
            for (int k = 0; k < h; k++) if (!Air(c, x, ly + k)) return false;
            return true;
        }

        // ------------------------------------------------------------------ great trees: a thick trunk, a round crown
        static void Trees(Chunk c, System.Random r, bool neon)
        {
            int lastX = -99;
            for (int ly = 1; ly < CH - 8; ly++)
                for (int x = 5; x < W - 6; x++)
                {
                    if (x - lastX < 9 || r.NextDouble() > 0.06) continue;
                    int h = 5 + r.Next(4);
                    if (!FloorWithRoom(c, x, ly, h + 2) || !FloorWithRoom(c, x + 1, ly, h)) continue;
                    if (InCarve(c, x, ly)) continue;
                    lastX = x;
                    for (int k = 0; k < h; k++) { Set(c, x, ly + k, Block.Wood); Set(c, x + 1, ly + k, Block.Wood); }
                    // roots spreading on the floor
                    if (Air(c, x - 1, ly)) Set(c, x - 1, ly, Block.Wood);
                    if (Air(c, x + 2, ly)) Set(c, x + 2, ly, Block.Wood);
                    // the crown
                    int cy = ly + h, rx = neon ? 3 : 4, ry = 3;
                    for (int dy = -ry; dy <= ry; dy++)
                        for (int dx = -rx; dx <= rx + 1; dx++)
                        {
                            float u = (dx - 0.5f) / (rx + 0.5f), v = dy / (ry + 0.4f);
                            if (u * u + v * v > 1f || r.NextDouble() < 0.1) continue;
                            Set(c, x + dx, cy + dy, Block.Leaf);
                        }
                }
        }

        // ------------------------------------------------------------------ the neon canopy: glowing leaves hanging from the roof
        static void Canopy(Chunk c, System.Random r)
        {
            for (int ly = 2; ly < CH - 1; ly++)
                for (int x = 3; x < W - 3; x++)
                {
                    Block above = c.t[(ly + 1) * W + x];
                    if (!Air(c, x, ly) || !Air(c, x, ly - 1) || !Tiles.Solid(above) || above == Block.Leaf) continue;
                    if (r.NextDouble() > 0.12 || InCarve(c, x, ly)) continue;
                    int len = 1 + r.Next(3);
                    for (int k = 0; k < len; k++) { Set(c, x, ly - k, Block.Leaf); if (r.NextDouble() < 0.5) Set(c, x + 1, ly - k, Block.Leaf); }
                }
        }

        // ------------------------------------------------------------------ huts on stilts over the water, a boardwalk to the shore
        static void StiltHuts(Chunk c, System.Random r)
        {
            int built = 0;
            for (int ly = 0; ly < CH - 7; ly++)
            {
                int run = 0;
                for (int x = 3; x < W - 3; x++)
                {
                    bool surf = Wet(c, x, ly) && Air(c, x, ly + 1);
                    run = surf ? run + 1 : 0;
                    if (run < 8 || r.NextDouble() > 0.7) continue;
                    int x0 = x - 7, deck = ly + 2;
                    if (InCarve(c, x0, deck) || InCarve(c, x, deck)) { run = 0; continue; }
                    bool room = true;
                    for (int xx = x0 - 1; xx <= x + 1 && room; xx++) for (int yy = deck; yy < deck + 5 && room; yy++) room = Air(c, xx, yy);
                    if (!room) { run = 0; continue; }
                    Build(c, x0, x, deck, r);
                    if (++built >= 2) return;   // two a chunk at most
                    run = 0;
                }
            }
        }

        static void Build(Chunk c, int x0, int x1, int deck, System.Random r)
        {
            // the deck (a ledge you land on), posts down into the water
            for (int x = x0 - 1; x <= x1 + 1; x++) Set(c, x, deck, Block.Plate);
            foreach (int px in new[] { x0, x1 })
                for (int yy = deck - 1; yy >= 0 && yy >= deck - 9; yy--)
                {
                    int i = yy * W + px;
                    if (Tiles.Solid(c.t[i])) break;
                    c.t[i] = Block.Wood; c.lt[i] = Fluids.None; c.liq[i] = 0f; c.hp[i] = (byte)World.MaxHp(Block.Wood, c.Y0 + yy);
                }
            // walls with open sides (no doors), a thatched roof of leaves
            for (int yy = deck + 3; yy <= deck + 3; yy++) { Set(c, x0, yy, Block.Wood); Set(c, x1, yy, Block.Wood); }
            for (int x = x0 - 1; x <= x1 + 1; x++) Set(c, x, deck + 4, Block.Leaf);
            for (int x = x0 + 1; x <= x1 - 1; x++) Set(c, x, deck + 5, Block.Leaf);
            // something to find inside (now and then)
            int cx = (x0 + x1) / 2;
            if (r.NextDouble() < 0.35) Set(c, cx, deck + 1, Block.Chest);
            // a boardwalk out to the nearest dry land, on whichever side has it
            for (int s = -1; s <= 1; s += 2)
            {
                int start = s < 0 ? x0 - 2 : x1 + 2;
                int n = 0;
                for (int x = start; x >= 2 && x < W - 2 && n < 14; x += s, n++)
                {
                    if (!Free(c, x, deck)) break;
                    Set(c, x, deck, Block.Plate, true);
                    if (Tiles.Solid(c.t[(deck - 1) * W + x])) break;   // reached the shore
                }
            }
        }

        // ------------------------------------------------------------------ wooden piers out over the water
        static void Piers(Chunk c, System.Random r)
        {
            for (int ly = 1; ly < CH - 3; ly++)
                for (int x = 4; x < W - 4; x++)
                {
                    // the shore: dry floor here, water just beside it
                    if (!Air(c, x, ly) || !Tiles.Solid(c.t[(ly - 1) * W + x]) || r.NextDouble() > 0.08) continue;
                    int dir = Wet(c, x + 1, ly - 1) || Wet(c, x + 1, ly) ? 1 : Wet(c, x - 1, ly - 1) || Wet(c, x - 1, ly) ? -1 : 0;
                    if (dir == 0 || InCarve(c, x, ly)) continue;
                    int deck = ly + 1, n = 0;
                    for (int xx = x; xx >= 3 && xx < W - 3 && n < 12; xx += dir, n++)
                    {
                        if (!Free(c, xx, deck)) break;
                        Set(c, xx, deck, Block.Plate, true);
                        if (n % 3 == 1)   // a post down into the water
                            for (int yy = deck - 1; yy >= 0 && yy >= deck - 8; yy--)
                            {
                                int i = yy * W + xx;
                                if (Tiles.Solid(c.t[i])) break;
                                c.t[i] = Block.Wood; c.lt[i] = Fluids.None; c.liq[i] = 0f; c.hp[i] = (byte)World.MaxHp(Block.Wood, c.Y0 + yy);
                            }
                    }
                    return;   // one a chunk
                }
        }

        // ------------------------------------------------------------------ little wooden beach shacks on the sand
        static void Shacks(Chunk c, System.Random r)
        {
            for (int ly = 1; ly < CH - 6; ly++)
                for (int x = 5; x < W - 6; x++)
                {
                    if (r.NextDouble() > 0.025) continue;
                    bool ok = true;
                    for (int xx = x - 2; xx <= x + 2 && ok; xx++) ok = FloorWithRoom(c, xx, ly, 5) && !InCarve(c, xx, ly);
                    if (!ok) continue;
                    // posts at the corners, a plank floor, a leaf-thatch roof (open sides)
                    for (int yy = ly; yy < ly + 3; yy++) { Set(c, x - 2, yy, Block.Wood); Set(c, x + 2, yy, Block.Wood); }
                    for (int xx = x - 3; xx <= x + 3; xx++) Set(c, xx, ly + 3, Block.Leaf);
                    for (int xx = x - 2; xx <= x + 2; xx++) Set(c, xx, ly + 4, Block.Leaf);
                    if (r.NextDouble() < 0.3) Set(c, x, ly, Block.Chest);
                    return;
                }
        }

        // ------------------------------------------------------------------ coral: branching fans growing up from the reef floor
        static void CoralBranches(Chunk c, System.Random r)
        {
            // soft corals (pink, they break at a touch) in clumps on the floors and growing out of the walls
            for (int ly = 1; ly < CH - 2; ly++)
                for (int x = 3; x < W - 3; x++)
                {
                    if (!Free(c, x, ly) || InCarve(c, x, ly)) continue;
                    bool floor = Tiles.Solid(c.t[(ly - 1) * W + x]) && c.t[(ly - 1) * W + x] != Block.Leaf;
                    bool wallL = Tiles.Solid(c.t[ly * W + x - 1]) && c.t[ly * W + x - 1] != Block.Leaf, wallR = Tiles.Solid(c.t[ly * W + x + 1]) && c.t[ly * W + x + 1] != Block.Leaf;
                    if (floor && r.NextDouble() < 0.12)
                    {
                        Set(c, x, ly, Block.Leaf, true);
                        if (r.NextDouble() < 0.5 && Free(c, x, ly + 1)) Set(c, x, ly + 1, Block.Leaf, true);
                    }
                    else if ((wallL || wallR) && r.NextDouble() < 0.05) Set(c, x, ly, Block.Leaf, true);
                }
            for (int ly = 1; ly < CH - 6; ly++)
                for (int x = 4; x < W - 4; x++)
                {
                    if (r.NextDouble() > 0.07) continue;
                    if (ly < 1 || !Tiles.Solid(c.t[(ly - 1) * W + x]) || !Free(c, x, ly) || InCarve(c, x, ly)) continue;
                    int h = 3 + r.Next(4), bx = x;
                    for (int k = 0; k < h; k++)
                    {
                        if (!Free(c, bx, ly + k)) break;
                        Set(c, bx, ly + k, Block.Coral, true);
                        if (k > 0 && r.NextDouble() < 0.35)
                        {
                            int s = r.Next(2) == 0 ? -1 : 1;
                            if (Free(c, bx + s, ly + k) && Free(c, bx + s, ly + k + 1)) { Set(c, bx + s, ly + k, Block.Coral, true); Set(c, bx + s, ly + k + 1, Block.Coral, true); }
                        }
                    }
                }
        }
    }
}
