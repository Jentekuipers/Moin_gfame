using UnityEngine;

namespace DigDeep
{
    // Paints one 16x16 block into a pixel buffer. Exposed edges get a glowing rim like the reference shots.
    public static class TileDraw
    {
        public const int UP = 1, RIGHT = 2, DOWN = 4, LEFT = 8, UL = 16, UR = 32, DR = 64, DL = 128, UPSAME = 256;
        public static bool clearBg;

        static readonly Color32 bedrockC = U.C(26, 14, 38), brickC = U.C(122, 80, 58), obsC = U.C(32, 20, 44),
            magmaC = U.C(74, 26, 16), sandC = U.C(196, 160, 96), chestC = U.C(156, 92, 40), tntC = U.C(196, 42, 32),
            volC = U.C(204, 52, 94), boulderC = U.C(132, 126, 118);

        public static Color32 BaseColor(Block t, Biome b)
        {
            switch (t)
            {
                case Block.Dirt: return b.dirt;
                case Block.Stone: case Block.Gold: case Block.Gem: return b.stone;
                case Block.HardStone: return b.hard;
                case Block.Obsidian: return obsC;
                case Block.Bedrock: return bedrockC;
                case Block.Wood: return U.C(160, 104, 60);
                case Block.Brick: return brickC;
                case Block.Sand: return sandC;
                case Block.Magma: return magmaC;
                case Block.Crystal: return b.crystal;
                case Block.Volatile: return volC;
                case Block.TNT: return tntC;
                case Block.Chest: return chestC;
                case Block.Crate: return U.C(70, 76, 88);
                case Block.Ice: return U.C(150, 205, 235);
                case Block.Plate: return U.C(120, 128, 145);
                case Block.Boulder: return boulderC;
                case Block.Lava: return U.C(255, 110, 20);
                case Block.Spikes: return U.C(180, 180, 190);
                case Block.Seal: return U.C(70, 24, 96);
                case Block.DarkMatter: return U.C(26, 14, 38);
                default: return b.bg;
            }
        }

        public static Color32 DebrisColor(Block t, Biome b)
        {
            if (t == Block.Gold) return U.C(255, 210, 60);
            if (t == Block.Gem) return U.C(120, 255, 230);
            return BaseColor(t, b);
        }

        static Color32 RimColor(Block t, Biome b)
        {
            switch (t)
            {
                case Block.Bedrock: return U.C(72, 68, 78);
                case Block.Brick: return U.C(176, 124, 84);
                case Block.Crystal: return Pal.Shade(b.crystal, 1.35f);
                case Block.Volatile: return U.C(255, 140, 180);
                case Block.Magma: return U.C(255, 130, 40);
                case Block.Obsidian: return U.C(150, 110, 210);
                case Block.TNT: return U.C(255, 100, 70);
                case Block.Sand: return U.C(240, 214, 150);
                case Block.Seal: return U.C(255, 90, 220);
                case Block.DarkMatter: return U.C(120, 70, 210);
                case Block.Ice: return U.C(225, 250, 255);
                case Block.Plate: return U.C(225, 230, 240);
                default: return b.rim;
            }
        }

        static Color32 Bg(Biome b, int wx, int wy, int gx, int gy)
        {
            if (clearBg) return new Color32(0, 0, 0, 0);
            float n = U.H01(gx, gy, 4);
            if (wy >= 1)
            {
                // night sky above the surface
                Color32 sky = Color32.Lerp(U.C(18, 20, 44), U.C(6, 6, 16), Mathf.Clamp01((wy - 1) / 10f));
                if (n > 0.996f) return U.C(230, 230, 255);
                return sky;
            }
            // the cave wall behind: low-contrast clustered stone in the biome's dark, violet-shifted colours
            float cl = U.H01(gx >> 2, gy >> 2, 3), fine = U.H01(gx >> 1, gy >> 1, 4);
            int lv = cl < 0.3f ? 1 : 2;
            if (fine < 0.08f) lv = 0;
            else if (fine > 0.94f) lv = 3;
            return Pal.Ramp(b.bg, lv);
        }

        static void Put(Color32[] buf, int stride, int ox, int oy, int px, int py, Color32 c)
        {
            if (px < 0 || px > 15 || py < 0 || py > 15) return;
            buf[(oy + py) * stride + ox + px] = c;
        }

        public static void Draw(Color32[] buf, int stride, int ox, int oy, Block t, int wx, int wy, int mask, float dmg, Biome b, byte plants = 0)
        {
            if (t == Block.Bedrock) t = Block.DarkMatter;   // world walls and floors look like the void, not like background rock
            bool full = Tiles.Solid(t) && t != Block.Chest && t != Block.Boulder && t != Block.Crate;
            Color32 rim = RimColor(t, b);
            Color32 baseC = BaseColor(t, b);
            for (int py = 0; py < 16; py++)
                for (int px = 0; px < 16; px++)
                {
                    int gx = wx * 16 + px, gy = wy * 16 + py;
                    buf[(oy + py) * stride + ox + px] = full ? SolidPx(t, baseC, rim, mask, px, py, gx, gy) : Bg(b, wx, wy, gx, gy);
                }

            switch (t)
            {
                case Block.Air:
                    Decor(buf, stride, ox, oy, wx, wy, mask, b);
                    if ((plants & 2) != 0) Vine(buf, stride, ox, oy, wx, wy, b);
                    if ((plants & 1) != 0) GrassPatch(buf, stride, ox, oy, wx, wy, b);
                    break;
                case Block.Lava: Lava(buf, stride, ox, oy, wx, wy, mask); break;
                case Block.Spikes: Spikes(buf, stride, ox, oy); break;
                case Block.Chest: Chest(buf, stride, ox, oy); break;
                case Block.Crate: Crate(buf, stride, ox, oy, Crates.RarityAt(wx, wy)); break;
                case Block.Boulder: Boulder(buf, stride, ox, oy, wx, wy); break;
                case Block.Gold: Nuggets(buf, stride, ox, oy, wx, wy); break;
                case Block.Gem: Gems(buf, stride, ox, oy, wx, wy); break;
            }
            if (dmg > 0.01f && Tiles.Solid(t)) Cracks(buf, stride, ox, oy, wx, wy, dmg);
        }

        static Color32 SolidPx(Block t, Color32 baseC, Color32 rim, int mask, int px, int py, int gx, int gy)
        {
            // texture in 2x2 clusters (not single noisy pixels) so blocks read crisp and clean
            float s = 0.86f + 0.26f * U.H01(gx >> 2, gy >> 2, 5);
            float n = U.H01(gx >> 1, gy >> 1, 6);
            if (n < 0.13f) s *= 0.74f; else if (n > 0.9f) s *= 1.2f;

            switch (t)
            {
                case Block.Brick:
                    {
                        int row = py >> 2, off = (row & 1) * 4;
                        if ((py & 3) == 0 || ((px + off) & 7) == 0) s = 0.55f;
                        break;
                    }
                case Block.Crystal:
                case Block.Volatile:
                    if ((px + (gy >> 3)) % 5 == 0) s *= 1.35f;
                    if (U.H01(gx >> 1, gy >> 1, 8) > 0.85f) s *= 1.3f;
                    if (t == Block.Volatile && px >= 6 && px <= 9 && py >= 6 && py <= 9) return U.C(255, 200, 220);
                    break;
                case Block.Magma:
                    if (U.H01(gx >> 1, gy >> 1, 11) > 0.8f) return U.C(255, 120, 30);
                    break;
                case Block.Obsidian:
                    if ((px + py) % 9 == 0 && n > 0.4f) return U.C(110, 80, 150);
                    break;
                case Block.TNT:
                    if (py >= 6 && py <= 9)
                    {
                        if ((py == 7 || py == 8) && px >= 3 && px <= 12 && px % 3 != 2) return U.C(50, 25, 25);
                        baseC = U.C(235, 225, 205); s = 1f;
                    }
                    else if (px % 5 == 0) s *= 0.7f;
                    if (py >= 14 && px == 8) return U.C(240, 220, 120);
                    break;
                case Block.DarkMatter:
                    // swirling void: soft cloudy bands with a few cold star-specks
                    {
                        float sw = Mathf.Sin(gx * 0.21f + Mathf.Sin(gy * 0.17f) * 2.2f) * 0.5f + 0.5f;
                        s = 0.75f + 0.5f * sw;
                        if (n > 0.985f) return U.C(210, 180, 255);
                        if (n > 0.96f) return U.C(110, 70, 170);
                    }
                    break;
                case Block.Seal:
                    if ((px + py) % 7 == 0 || (px - py + 16) % 7 == 0) return U.C(230, 110, 255);
                    break;
                case Block.Wood:
                    {
                        // planks: 4-pixel boards with dark seams, wood grain and the odd nail
                        int board = py >> 2;
                        if ((py & 3) == 0) return Pal.Ramp(baseC, 0);
                        int seam = (board * 7 + (gx >> 4) * 3) % 16;
                        if (px == seam) return Pal.Ramp(baseC, 0);
                        s = 0.95f + 0.12f * ((py & 3) == 3 ? 1f : 0f);
                        if (U.H01(gx >> 2, gy, 61) > 0.82f) s *= 0.82f;   // grain
                        if ((px == 2 || px == 13) && (py & 3) == 2 && (board & 1) == 0) return U.C(190, 190, 205);   // nails
                        break;
                    }
                case Block.Bedrock:
                    s = 0.7f + 0.6f * U.H01(gx >> 3, gy >> 3, 12) * (n > 0.5f ? 1f : 0.8f);
                    break;
            }

            Color32 c = Pal.Shade(baseC, s);
            // lit from above: rim light on top edges, a lighter line on the sides, violet shadow underneath
            int eTop = (mask & UP) != 0 ? 15 - py : 99;
            int eBot = (mask & DOWN) != 0 ? py : 99;
            int eSide = Mathf.Min((mask & LEFT) != 0 ? px : 99, (mask & RIGHT) != 0 ? 15 - px : 99);
            if ((mask & UL) != 0 && (mask & (UP | LEFT)) == 0) eTop = Mathf.Min(eTop, Mathf.Max(px, 15 - py));
            if ((mask & UR) != 0 && (mask & (UP | RIGHT)) == 0) eTop = Mathf.Min(eTop, Mathf.Max(15 - px, 15 - py));
            if ((mask & DR) != 0 && (mask & (DOWN | RIGHT)) == 0) eBot = Mathf.Min(eBot, Mathf.Max(15 - px, py));
            if ((mask & DL) != 0 && (mask & (DOWN | LEFT)) == 0) eBot = Mathf.Min(eBot, Mathf.Max(px, py));
            int e = Mathf.Min(eTop, Mathf.Min(eBot, eSide));
            if (eTop == 0) c = n < 0.2f ? Pal.Ramp(rim, 1) : rim;
            else if (eTop == 1) c = n > 0.3f ? Pal.Ramp(rim, 1) : Pal.Ramp(baseC, 3);
            else if (eTop == 2 && n > 0.55f) c = Pal.Ramp(baseC, 3);
            else if (eSide == 0) c = n < 0.3f ? Pal.Ramp(baseC, 3) : Color32.Lerp(Pal.Ramp(baseC, 3), rim, 0.45f);
            else if (eBot == 0) c = Pal.Ramp(baseC, 0);
            else if (eBot == 1) c = Pal.Ramp(baseC, 1);
            else if (e > 5 && t != Block.TNT) c = Color32.Lerp(c, Pal.Ramp(baseC, 1), 0.3f);   // deep inside a wall reads a touch darker
            return c;
        }

        static void Decor(Color32[] buf, int stride, int ox, int oy, int wx, int wy, int mask, Biome b)
        {
            if (clearBg) return;
            uint h = U.Hash(wx, wy, 21);
            if ((mask & DOWN) == 0)
            {
                // a few loose tufts on bare floors (real grass patches are drawn by GrassPatch)
                if ((h & 7) == 0)
                {
                    int n = 2 + (int)((h >> 2) & 1);
                    for (int k = 0; k < n; k++)
                    {
                        int px = (int)((h >> (4 + k * 4)) & 15);
                        int ht = 1 + (int)((h >> (16 + k * 2)) & 3);
                        for (int i = 0; i < ht; i++)
                            Put(buf, stride, ox, oy, px, i, i == ht - 1 ? Pal.Ramp(b.plant, 4) : i == 0 ? Pal.Ramp(b.plant, 1) : b.plant);
                    }
                }
                if (((h >> 26) % 9) == 0 && wy < 0)
                {
                    // tiny glowing mushroom
                    int px = 3 + (int)((h >> 10) % 9);
                    Color32 cap = Pal.Shade(b.rim, 1.1f);
                    Put(buf, stride, ox, oy, px, 0, U.C(220, 210, 190));
                    Put(buf, stride, ox, oy, px, 1, U.C(220, 210, 190));
                    for (int i = -1; i <= 1; i++) Put(buf, stride, ox, oy, px + i, 2, cap);
                    Put(buf, stride, ox, oy, px, 3, cap);
                }
            }
            if ((mask & UP) == 0 && wy < 0)
            {
                // roots / drips from ceilings
                if (((h >> 8) & 3) == 0)
                {
                    int px = (int)((h >> 12) & 15);
                    int len = 1 + (int)((h >> 20) & 3);
                    for (int i = 0; i < len; i++) Put(buf, stride, ox, oy, px, 15 - i, Pal.Ramp(b.plant, i == len - 1 ? 0 : 1));
                }
            }
        }

        // A patch of grass growing on the block below: dense blades of varying height, light tips.
        static void GrassPatch(Color32[] buf, int stride, int ox, int oy, int wx, int wy, Biome b)
        {
            Color32 dark = Pal.Ramp(b.plant, 1), mid = b.plant, lit = Pal.Ramp(b.plant, 3), tip = Pal.Ramp(b.plant, 4);
            for (int px = 0; px < 16; px++)
            {
                uint h = U.Hash(wx * 16 + px, wy, 23);
                int ht = 2 + (int)(h % 4) + ((h >> 4) % 5 == 0 ? 2 : 0);
                if ((h >> 8) % 6 == 0) ht = 1;   // a few short gaps
                for (int py = 0; py < ht; py++)
                    Put(buf, stride, ox, oy, px, py, py == ht - 1 ? (((h >> 12) & 1) == 0 ? tip : lit) : py == 0 ? dark : mid);
            }
            // the odd flower
            uint f = U.Hash(wx, wy, 24);
            if (f % 5 == 0)
            {
                int fx = 3 + (int)((f >> 4) % 10);
                Color32 petal = (f >> 8) % 2 == 0 ? U.C(255, 220, 110) : U.C(255, 130, 170);
                Put(buf, stride, ox, oy, fx, 5, petal); Put(buf, stride, ox, oy, fx - 1, 4, petal); Put(buf, stride, ox, oy, fx + 1, 4, petal);
                Put(buf, stride, ox, oy, fx, 4, U.C(255, 250, 220));
                for (int py = 0; py < 4; py++) Put(buf, stride, ox, oy, fx, py, dark);
            }
        }

        // A hanging vine (grass rope): a twisting stem with leaves on alternating sides.
        static void Vine(Color32[] buf, int stride, int ox, int oy, int wx, int wy, Biome b)
        {
            Color32 stem = Pal.Ramp(b.plant, 1), leaf = b.plant, lit = Pal.Ramp(b.plant, 3);
            for (int py = 0; py < 16; py++)
            {
                int sx = 7 + ((py + wy * 16) / 4 % 2 == 0 ? 0 : 1);
                Put(buf, stride, ox, oy, sx, py, stem);
                if ((py + wx) % 5 == 0)
                {
                    int side = ((py / 5 + wx) & 1) == 0 ? -1 : 1;
                    Put(buf, stride, ox, oy, sx + side, py, leaf);
                    Put(buf, stride, ox, oy, sx + side * 2, py, lit);
                    Put(buf, stride, ox, oy, sx + side * 2, py - 1, leaf);
                }
            }
        }

        static void Lava(Color32[] buf, int stride, int ox, int oy, int wx, int wy, int mask)
        {
            int top = (mask & UPSAME) != 0 ? 16 : 13;
            for (int py = 0; py < top; py++)
                for (int px = 0; px < 16; px++)
                {
                    float n = U.H01(wx * 16 + px, wy * 16 + py, 31);
                    Color32 c = U.C(232, 84, 20);
                    if (top < 16 && py == top - 1) c = U.C(255, 224, 96);
                    else if (top < 16 && py == top - 2) c = U.C(255, 168, 50);
                    else if (n > 0.86f) c = U.C(255, 150, 40);
                    else if (n < 0.1f) c = U.C(170, 40, 10);
                    Put(buf, stride, ox, oy, px, py, c);
                }
        }

        static void Spikes(Color32[] buf, int stride, int ox, int oy)
        {
            for (int i = 0; i < 3; i++)
            {
                int cx = i * 5 + 3;
                for (int py = 0; py < 7; py++)
                {
                    int half = (6 - py) / 2;
                    for (int px = cx - half; px <= cx + half; px++)
                        Put(buf, stride, ox, oy, px, py, px <= cx ? U.C(214, 214, 224) : U.C(130, 130, 146));
                }
            }
            for (int px = 0; px < 16; px++) Put(buf, stride, ox, oy, px, 0, U.C(90, 86, 98));
        }

        static void Chest(Color32[] buf, int stride, int ox, int oy)
        {
            Color32 dark = U.C(60, 32, 16), gold = U.C(236, 184, 60), key = U.C(40, 20, 10);
            for (int py = 0; py <= 10; py++)
                for (int px = 2; px <= 13; px++)
                {
                    bool border = px == 2 || px == 13 || py == 0 || py == 10;
                    Color32 c;
                    if (border) c = dark;
                    else if (py == 6) c = gold;
                    else if (py > 6) c = Pal.Shade(chestC, 1.15f);
                    else c = (px % 4 == 0) ? Pal.Shade(chestC, 0.75f) : chestC;
                    if (py >= 3 && py <= 7 && (px == 7 || px == 8)) c = py == 5 ? key : gold;
                    Put(buf, stride, ox, oy, px, py, c);
                }
        }

        // Weapon crate: a riveted steel box with rarity-coloured bands (white, green, blue, gold).
        static void Crate(Color32[] buf, int stride, int ox, int oy, Rarity r)
        {
            Color32 band = r == Rarity.Relic ? U.C(255, 200, 50) : r == Rarity.Rare ? U.C(70, 150, 255) : r == Rarity.Uncommon ? U.C(80, 220, 90) : U.C(215, 215, 225);
            Color32 dark = U.C(26, 28, 34), steel = U.C(84, 90, 104), hi = U.C(140, 148, 165), rivet = U.C(190, 196, 210);
            for (int py = 0; py <= 11; py++)
                for (int px = 1; px <= 14; px++)
                {
                    bool border = px == 1 || px == 14 || py == 0 || py == 11;
                    Color32 c;
                    if (border) c = dark;
                    else if (py == 10) c = hi;
                    else if (py == 4 || py == 5 || px == 4 || px == 11) c = band;
                    else c = ((px + py) % 7 == 0) ? Pal.Shade(steel, 0.8f) : steel;
                    if ((px == 2 || px == 13) && (py == 1 || py == 9)) c = rivet;
                    Put(buf, stride, ox, oy, px, py, c);
                }
            // a little glint of the weapon outline on the lid
            for (int px = 6; px <= 9; px++) Put(buf, stride, ox, oy, px, 7, Pal.Shade(band, 1.2f));
            Put(buf, stride, ox, oy, 6, 8, Pal.Shade(band, 1.2f));
        }

        static void Boulder(Color32[] buf, int stride, int ox, int oy, int wx, int wy)
        {
            const float r = 7.4f;
            for (int py = 0; py < 16; py++)
                for (int px = 0; px < 16; px++)
                {
                    float dx = px - 7.5f, dy = py - 7.5f, d2 = dx * dx + dy * dy;
                    if (d2 > r * r) continue;
                    float n = U.H01(wx * 16 + px, wy * 16 + py, 41);
                    // lit from the top-left: highlight, base, then violet shadow towards the bottom-right
                    float lt = (py - px) / 15f + 0.5f;   // 0 bottom-right .. 1 top-left
                    int lv = lt > 0.85f ? 4 : lt > 0.6f ? 3 : lt > 0.35f ? 2 : lt > 0.15f ? 1 : 0;
                    if (d2 > (r - 1.3f) * (r - 1.3f)) lv = Mathf.Min(lv, 1) - (lt < 0.5f ? 1 : 0);
                    else if (n < 0.08f) lv = Mathf.Max(0, lv - 1);
                    Put(buf, stride, ox, oy, px, py, Pal.Ramp(boulderC, Mathf.Max(0, lv)));
                }
        }

        static void Nuggets(Color32[] buf, int stride, int ox, int oy, int wx, int wy)
        {
            for (int k = 0; k < 4; k++)
            {
                uint h = U.Hash(wx, wy, 50 + k);
                int px = 2 + (int)(h % 11), py = 2 + (int)((h >> 8) % 11);
                Put(buf, stride, ox, oy, px, py + 1, U.C(255, 250, 170));
                Put(buf, stride, ox, oy, px + 1, py + 1, U.C(242, 192, 42));
                Put(buf, stride, ox, oy, px, py, U.C(242, 192, 42));
                Put(buf, stride, ox, oy, px + 1, py, U.C(170, 110, 20));
                if ((h >> 20 & 1) == 0) Put(buf, stride, ox, oy, px + 2, py, U.C(242, 192, 42));
            }
        }

        static void Gems(Color32[] buf, int stride, int ox, int oy, int wx, int wy)
        {
            Color32[] cols = { U.C(80, 240, 230), U.C(240, 80, 220), U.C(90, 240, 120), U.C(255, 90, 90) };
            for (int k = 0; k < 2; k++)
            {
                uint h = U.Hash(wx, wy, 60 + k);
                int cx = 4 + (int)(h % 8), cy = 4 + (int)((h >> 8) % 8);
                Color32 c = cols[(h >> 16) % 4];
                Put(buf, stride, ox, oy, cx, cy, c);
                Put(buf, stride, ox, oy, cx - 1, cy, Pal.Shade(c, 0.7f));
                Put(buf, stride, ox, oy, cx + 1, cy, Pal.Shade(c, 0.7f));
                Put(buf, stride, ox, oy, cx, cy - 1, Pal.Shade(c, 0.6f));
                Put(buf, stride, ox, oy, cx, cy + 1, U.C(255, 255, 255));
            }
        }

        static void Cracks(Color32[] buf, int stride, int ox, int oy, int wx, int wy, float dmg)
        {
            int steps = 3 + (int)(dmg * 18f);
            for (int branch = 0; branch < 2; branch++)
            {
                int px = 8, py = 8;
                for (int i = 0; i < steps; i++)
                {
                    uint h = U.Hash(wx * 7 + branch, wy, 300 + i);
                    int dir = (int)(h % 8);
                    if (branch == 1) dir = (dir + 4) % 8;
                    int dx = dir == 0 || dir == 1 || dir == 7 ? 1 : (dir >= 3 && dir <= 5 ? -1 : 0);
                    int dy = dir >= 1 && dir <= 3 ? 1 : (dir >= 5 ? -1 : 0);
                    px = Mathf.Clamp(px + dx, 1, 14); py = Mathf.Clamp(py + dy, 1, 14);
                    int idx = (oy + py) * stride + ox + px;
                    buf[idx] = Pal.Shade(buf[idx], 0.4f);
                }
            }
        }
    }
}
