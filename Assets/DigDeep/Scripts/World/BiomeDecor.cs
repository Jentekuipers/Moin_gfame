using UnityEngine;

namespace DigDeep
{
    // Decorations dotted about the newer biomes as each chunk is made (pictures only: nothing to bump into).
    //  BEACH  - palm trees (several kinds), parasols, sand castles, shells; seaweed and starfish under the water
    //  REEF   - kelp, seaweed, anemones, brain coral, starfish and shells under the water
    //  CLOUDS - nests, weather vanes, sky lanterns hanging from the roof
    //  CRIMSON- bone piles, blood shrooms, thorns, cocoons hanging from the roof
    //  NEON   - glowing shrooms and flowers, neon lanterns hanging from the roof
    public static class BiomeDecor
    {
        const int W = World.W, CH = World.CH;

        public static void Place(Chunk c)
        {
            if (Game.I == null) return;
            int top = -(c.Y0 + CH - 1);
            if (top < 20) return;
            int bi = Biomes.Index(-(c.Y0 + CH / 2));
            if (bi < Biomes.Cloud) return;
            var r = new System.Random(Gen.Seed * 577 + c.ci * 41);
            int lastX = -9, lastY = -99;
            for (int ly = 1; ly < CH - 1; ly++)
                for (int x = 3; x < W - 3; x++)
                {
                    int i = ly * W + x;
                    if (c.t[i] != Block.Air) continue;
                    int wy = c.Y0 + ly;
                    if (Gen.InCarve(x, -wy) || Gen.InShop(x, -wy)) continue;
                    Block below = c.t[i - W], above = c.t[i + W];
                    bool floor = Tiles.Solid(below) && below != Block.Leaf && below != Block.Spikes;
                    bool ceil = Tiles.Solid(above) && above != Block.Leaf && c.lt[i] == Fluids.None;
                    if (!floor && !ceil) continue;
                    if (ly == lastY && x - lastX < 3) continue;
                    bool wet = c.lt[i] != Fluids.None;
                    double k = r.NextDouble();
                    string kind = null; bool sway = false, glow = false, hang = false;
                    if (floor)
                        switch (bi)
                        {
                            case Biomes.Beach:
                                if (wet) kind = k < 0.07 ? "seaweed" : k < 0.1 ? "starfish" : k < 0.12 ? "shell" : null;
                                else if (k < 0.08 && Room(c, x, ly, 5, 2)) { kind = "palm"; sway = true; }
                                else if (k < 0.1 && Room(c, x, ly, 2, 1)) kind = "parasol";
                                else if (k < 0.13 && below == Block.Sand && Room(c, x, ly, 2, 1)) kind = "castle";
                                else if (k < 0.17) kind = r.Next(2) == 0 ? "shell" : "starfish";
                                break;
                            case Biomes.Reef:
                                if (wet) kind = k < 0.07 && Room(c, x, ly, 3, 0) ? "kelp" : k < 0.14 ? "seaweed" : k < 0.19 ? "anemone" : k < 0.23 ? "brain" : k < 0.26 ? "starfish" : k < 0.28 ? "shell" : null;
                                else kind = k < 0.04 ? "shell" : null;
                                sway = kind == "kelp" || kind == "seaweed";
                                break;
                            case Biomes.Cloud:
                                if (!wet) kind = k < 0.03 ? "nest" : k < 0.045 && Room(c, x, ly, 2, 0) ? "vane" : null;
                                break;
                            case Biomes.Crimson:
                                kind = k < 0.04 ? "bones" : wet ? null : k < 0.09 ? "bloodshroom" : k < 0.13 ? "thorns" : null;
                                break;
                            case Biomes.Neon:
                                if (!wet) { kind = k < 0.06 ? "neonshroom" : k < 0.12 && Room(c, x, ly, 2, 0) ? "neonflower" : null; glow = kind != null; }
                                break;
                        }
                    else if (ceil)
                    {
                        if (bi == Biomes.Cloud && k < 0.025) { kind = "skylantern"; glow = true; hang = true; }
                        else if (bi == Biomes.Crimson && k < 0.04) { kind = "cocoon"; hang = true; }
                        else if (bi == Biomes.Neon && k < 0.035) { kind = "neonlantern"; glow = true; hang = true; }
                    }
                    if (kind == null) continue;
                    var s = ArtDecor.Get(kind, r.Next(kind == "palm" ? 8 : 4));
                    if (s == null) continue;
                    float hgt = s.rect.height / Art.PPU;
                    Vector2 at = hang ? new Vector2(x + 0.5f, wy + 1f - hgt) : new Vector2(x + 0.5f, wy);
                    Decor.Add(at, s, 2, sway, glow, r.Next(2) == 0);
                    lastX = x; lastY = ly;
                }
        }

        // open air above (h blocks) and to either side (w blocks) of this floor spot
        static bool Room(Chunk c, int x, int ly, int h, int w)
        {
            for (int yy = ly; yy < ly + h; yy++)
                for (int xx = x - w; xx <= x + w; xx++)
                {
                    if (xx < 0 || xx >= W) return false;
                    if (yy >= CH) return true;   // (past the chunk's top: assume it's clear)
                    if (Tiles.Solid(c.t[yy * W + xx])) return false;
                }
            return true;
        }
    }
}
