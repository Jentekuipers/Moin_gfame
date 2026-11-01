using UnityEngine;

namespace DigDeep
{
    // The pictures of the newer biomes: cloud blocks (puffy, they darken into rain clouds), coral, leaves, each biome's
    // grass and its own back wall.
    public static class BiomeTiles
    {
        static void Put(Color32[] buf, int stride, int ox, int oy, int px, int py, Color32 c) => TileDraw.Put(buf, stride, ox, oy, px, py, c);

        // ------------------------------------------------------------------ CLOUD: soft round puffs, lumpy where open
        public static void Cloud(Color32[] buf, int stride, int ox, int oy, int wx, int wy, int mask, bool gilded)
        {
            float dark = CloudWeather.Dark(wx, wy);
            Color32 lit = Color32.Lerp(U.C(250, 252, 255), U.C(120, 128, 150), dark), mid = Color32.Lerp(U.C(214, 226, 246), U.C(92, 100, 124), dark);
            Color32 shade = Color32.Lerp(U.C(168, 186, 222), U.C(66, 72, 96), dark), deep = Color32.Lerp(U.C(140, 158, 200), U.C(50, 54, 76), dark);
            bool up = (mask & TileDraw.UP) != 0, down = (mask & TileDraw.DOWN) != 0, left = (mask & TileDraw.LEFT) != 0, right = (mask & TileDraw.RIGHT) != 0;
            uint h = U.Hash(wx, wy, 301);
            for (int py = 0; py < 16; py++)
                for (int px = 0; px < 16; px++)
                {
                    // where a side is open the edge bulges into round lumps (two per side), so clouds never look boxy
                    bool inside = true;
                    if (up && py > 10) { float cx = px < 8 ? 4f + (h & 1) : 11.5f - ((h >> 1) & 1), r = 4.6f; float dx = px - cx, dy = py - 10.5f; inside &= dx * dx + dy * dy <= r * r || (py <= 11 && px > 2 && px < 13); }
                    if (down && py < 4) { float cx = px < 8 ? 4.5f : 11.5f, r = 4.4f; float dx = px - cx, dy = py - 4.5f; inside &= dx * dx + dy * dy <= r * r; }
                    if (left && px < 3) inside &= Mathf.Abs(py - 8f) < 5f + px * 1.2f;
                    if (right && px > 12) inside &= Mathf.Abs(py - 8f) < 5f + (15 - px) * 1.2f;
                    if (!inside) continue;
                    // lit from above: bright tops, a soft blue belly
                    float n = U.H01((wx * 16 + px) >> 2, (wy * 16 + py) >> 2, 302);
                    float k = py / 15f + (n - 0.5f) * 0.25f;
                    Color32 c = k > 0.7f ? lit : k > 0.42f ? mid : k > 0.2f ? shade : deep;
                    if (!up && !down && n > 0.82f) c = mid;
                    Put(buf, stride, ox, oy, px, py, c);
                }
            if (gilded) TileDraw.Nuggets(buf, stride, ox, oy, wx, wy);
            // a rain cloud drips
            if (dark > 0.5f && !up && ((h >> 4) & 3) == 0) { Put(buf, stride, ox, oy, (int)(h >> 8) & 15, 0, U.C(150, 200, 255)); }
        }

        // ------------------------------------------------------------------ CORAL: knobbly, in reef colours, with polyps
        static readonly Color32[] CoralC = { U.C(255, 112, 140), U.C(255, 152, 90), U.C(190, 120, 255), U.C(255, 206, 120), U.C(90, 220, 200) };
        public static void Coral(Color32[] buf, int stride, int ox, int oy, int wx, int wy, int mask)
        {
            uint h = U.Hash(wx >> 1, wy >> 1, 311);
            Color32 baseC = CoralC[h % (uint)CoralC.Length];
            for (int py = 0; py < 16; py++)
                for (int px = 0; px < 16; px++)
                {
                    int gx = wx * 16 + px, gy = wy * 16 + py;
                    bool edge = ((mask & TileDraw.UP) != 0 && py > 13) || ((mask & TileDraw.LEFT) != 0 && px < 2) || ((mask & TileDraw.RIGHT) != 0 && px > 13);
                    if (edge && U.H01(gx >> 1, gy >> 1, 312) < 0.45f) continue;   // ragged where it's open
                    float n = U.H01(gx >> 1, gy >> 1, 313);
                    int lv = n > 0.75f ? 3 : n < 0.2f ? 1 : 2;
                    if (py > 11) lv = Mathf.Min(4, lv + 1);
                    Color32 c = Pal.Ramp(baseC, lv);
                    // polyps: little pale rings
                    int cx = ((gx >> 2) << 2) + 2, cy = ((gy >> 2) << 2) + 2;
                    if (U.H01(gx >> 2, gy >> 2, 314) > 0.6f && Mathf.Abs(gx - cx) + Mathf.Abs(gy - cy) == 1) c = Pal.Ramp(baseC, 4);
                    Put(buf, stride, ox, oy, px, py, c);
                }
        }

        // ------------------------------------------------------------------ LEAVES: clumps of leaves, ragged at the edges
        public static void Leaf(Color32[] buf, int stride, int ox, int oy, int wx, int wy, int mask, Biome b)
        {
            bool neon = b.idx == Biomes.Neon;
            for (int py = 0; py < 16; py++)
                for (int px = 0; px < 16; px++)
                {
                    int gx = wx * 16 + px, gy = wy * 16 + py;
                    bool open = ((mask & TileDraw.UP) != 0 && py > 12) || ((mask & TileDraw.DOWN) != 0 && py < 3) || ((mask & TileDraw.LEFT) != 0 && px < 3) || ((mask & TileDraw.RIGHT) != 0 && px > 12);
                    float n = U.H01(gx >> 1, gy >> 1, 321);
                    if (open && n < 0.5f) continue;
                    // leaf shapes: small diamonds of light and shade
                    int lx = (gx + ((gy >> 2) & 1) * 2) & 3, ly = gy & 3;
                    int lv = (lx + ly) % 4 == 0 ? 3 : (lx == 3 || ly == 0) ? 1 : 2;
                    if (n > 0.92f) lv = 4;
                    Color32 col = neon ? (U.H01(gx >> 3, gy >> 3, 322) < 0.5f ? b.plant : b.rim) : b.plant;
                    Put(buf, stride, ox, oy, px, py, Pal.Ramp(col, lv));
                }
            // the crimson forest's leaves drip blood
            if (b.idx == Biomes.Crimson && (mask & TileDraw.DOWN) != 0)
            {
                uint h = U.Hash(wx, wy, 323);
                if ((h & 3) == 0) { int px = (int)(h >> 4) & 15; for (int i = 0; i < 2 + (int)((h >> 9) & 3); i++) Put(buf, stride, ox, oy, px, i, U.C(170, 10, 30)); }
            }
        }

        // ------------------------------------------------------------------ each new biome's grass
        public static void Grass(Color32[] buf, int stride, int ox, int oy, int wx, int wy, Biome b)
        {
            for (int px = 0; px < 16; px++)
            {
                uint h = U.Hash(wx * 16 + px, wy, 331);
                int ht = 1 + (int)(h % 4);
                switch (b.idx)
                {
                    case Biomes.Cloud:
                        // pale wisps of sky-grass, like frost
                        ht = 1 + (int)(h % 3);
                        for (int py = 0; py < ht; py++) Put(buf, stride, ox, oy, px, py, py == ht - 1 ? U.C(255, 255, 255) : U.C(196, 232, 236));
                        break;
                    case Biomes.Crimson:
                        // red blades, some with a bead of blood on the tip
                        ht = 2 + (int)(h % 5);
                        for (int py = 0; py < ht; py++) Put(buf, stride, ox, oy, px, py, Pal.Ramp(b.plant, py == 0 ? 0 : py == ht - 1 ? 3 : 2));
                        if ((h >> 8) % 9 == 0) Put(buf, stride, ox, oy, px, ht, U.C(150, 0, 20));
                        break;
                    case Biomes.Beach:
                        // sparse dune grass, leaning, and the odd shell
                        if ((h >> 6) % 3 != 0) break;
                        ht = 3 + (int)(h % 5);
                        for (int py = 0; py < ht; py++) Put(buf, stride, ox, oy, px + (py > ht / 2 ? 1 : 0), py, py == ht - 1 ? U.C(220, 230, 150) : U.C(150, 186, 96));
                        break;
                    case Biomes.Neon:
                        // glowing blades in pink, teal and lime
                        ht = 2 + (int)(h % 5);
                        Color32 g = (h >> 10) % 3 == 0 ? b.plant : (h >> 10) % 3 == 1 ? b.rim : b.crystal;
                        for (int py = 0; py < ht; py++) Put(buf, stride, ox, oy, px, py, py == ht - 1 ? U.C(255, 255, 255) : Pal.Ramp(g, py == 0 ? 2 : 3));
                        break;
                    case Biomes.Reef:
                        // sea grass swaying, and little fans of coral
                        ht = 3 + (int)(h % 6);
                        for (int py = 0; py < ht; py++) Put(buf, stride, ox, oy, px + ((py / 2 + (int)(h & 1)) & 1), py, Pal.Ramp(U.C(70, 190, 120), py == ht - 1 ? 3 : 2));
                        break;
                }
            }
            uint f = U.Hash(wx, wy, 332);
            if (b.idx == Biomes.Beach && f % 4 == 0)
            {
                // a shell
                int sx = 3 + (int)(f >> 4) % 10;
                Put(buf, stride, ox, oy, sx, 0, U.C(255, 220, 200)); Put(buf, stride, ox, oy, sx + 1, 0, U.C(250, 180, 170)); Put(buf, stride, ox, oy, sx + 2, 0, U.C(255, 220, 200)); Put(buf, stride, ox, oy, sx + 1, 1, U.C(255, 235, 220));
            }
            if (b.idx == Biomes.Reef && f % 3 == 0)
            {
                // a fan coral
                int sx = 2 + (int)(f >> 4) % 11;
                Color32 fc = (f >> 8) % 2 == 0 ? U.C(255, 120, 160) : U.C(255, 180, 90);
                for (int k = 0; k < 5; k++) for (int j = -k / 2; j <= k / 2; j++) Put(buf, stride, ox, oy, sx + j, k, k == 4 ? Pal.Ramp(fc, 4) : fc);
            }
            if (b.idx == Biomes.Neon && f % 3 == 0)
            {
                // a glowing flower
                int sx = 2 + (int)(f >> 4) % 11;
                Color32 fc = (f >> 8) % 2 == 0 ? U.C(255, 90, 230) : U.C(90, 255, 230);
                for (int k = 0; k < 4; k++) Put(buf, stride, ox, oy, sx, k, U.C(60, 200, 120));
                Put(buf, stride, ox, oy, sx - 1, 4, fc); Put(buf, stride, ox, oy, sx + 1, 4, fc); Put(buf, stride, ox, oy, sx, 5, fc); Put(buf, stride, ox, oy, sx, 4, U.C(255, 255, 220));
            }
        }

        // ------------------------------------------------------------------ the back walls
        public static Color32 Bg(Biome b, Color32 c, int lv, int gx, int gy, float fine)
        {
            switch (b.idx)
            {
                case Biomes.Cloud:
                    {
                        // far-off clouds drifting in the dusk, and now and then a rainbow arcing behind them
                        float cl = Mathf.PerlinNoise(gx * 0.018f + 40f, gy * 0.03f + 7f) + 0.35f * Mathf.PerlinNoise(gx * 0.06f, gy * 0.08f);
                        if (cl > 0.95f) return Color32.Lerp(c, U.C(170, 186, 226), cl > 1.08f ? 0.42f : 0.26f);
                        int rx = U.FloorDiv(gx, 220), ry = U.FloorDiv(gy, 160);
                        if (U.H01(rx, ry, 341) < 0.35f)
                        {
                            float cx = rx * 220 + 110, cy = ry * 160 + 20;
                            float d = Mathf.Sqrt((gx - cx) * (gx - cx) + (gy - cy) * (gy - cy));
                            int band = Mathf.FloorToInt((d - 70f) / 3f);
                            if (gy > cy && band >= 0 && band < 6)
                            {
                                Color32[] rb = { U.C(255, 90, 90), U.C(255, 170, 70), U.C(255, 240, 90), U.C(90, 230, 120), U.C(90, 160, 255), U.C(170, 110, 255) };
                                return Color32.Lerp(c, rb[5 - band], 0.32f);
                            }
                        }
                        if (U.H01(gx, gy, 342) > 0.998f) return U.C(230, 240, 255);   // a star through a gap
                        return c;
                    }
                case Biomes.Crimson:
                    {
                        // dark trunks far behind, their crowns dripping, and a red haze
                        int tx = U.FloorDiv(gx, 56), lx = gx - tx * 56;
                        if (U.H01(tx, 0, 351) < 0.6f)
                        {
                            float w = 5f + U.H01(tx, 1, 352) * 5f + Mathf.Sin(gy * 0.04f + tx) * 1.5f;
                            if (Mathf.Abs(lx - 28) < w) return Pal.Ramp(Color32.Lerp(b.bg, U.C(60, 20, 20), 0.5f), lx < 28 ? 1 : 0);
                        }
                        float crown = Mathf.PerlinNoise(gx * 0.04f + 3f, gy * 0.05f);
                        if (crown > 0.68f) return Color32.Lerp(c, b.plant, crown > 0.76f ? 0.28f : 0.16f);
                        if ((gx * 7 + gy * 3) % 61 == 0 && U.H01(gx >> 3, gy >> 4, 353) < 0.3f) return U.C(120, 10, 24);   // a drip
                        return c;
                    }
                case Biomes.Beach:
                    {
                        // sandstone in soft layers, with shells and the odd starfish set into it
                        int band = ((gy + (int)(3f * Mathf.Sin(gx * 0.05f))) % 20 + 20) % 20;
                        Color32 sand = Color32.Lerp(c, b.dirt, 0.18f);
                        if (band < 2) sand = Pal.Ramp(sand, 1);
                        uint h = U.Hash(gx >> 4, gy >> 4, 361);
                        if (h % 23 == 0)
                        {
                            int lx = gx & 15, ly = gy & 15;
                            if ((lx == 8 && ly > 4 && ly < 12) || (ly == 8 && lx > 4 && lx < 12) || (Mathf.Abs(lx - 8) == Mathf.Abs(ly - 8) && Mathf.Abs(lx - 8) < 3)) return Color32.Lerp(sand, U.C(255, 140, 100), 0.45f);
                        }
                        return sand;
                    }
                case Biomes.Neon:
                    {
                        // a black jungle behind, its leaves and vines drawn in glowing lines, and fireflies
                        float leaf = Mathf.Sin(gx * 0.07f + Mathf.Sin(gy * 0.04f) * 3f) * Mathf.Sin(gy * 0.06f + gx * 0.015f);
                        if (leaf > 0.7f && leaf < 0.76f) return Color32.Lerp(c, U.H01(gx >> 5, gy >> 5, 371) < 0.5f ? b.plant : b.rim, 0.45f);
                        int vx = (gx >> 4) * 16 + 7 + (int)(2f * Mathf.Sin(gy * 0.12f + (gx >> 4)));
                        if (gx == vx && U.H01(gx >> 4, gy >> 6, 372) < 0.25f) return Color32.Lerp(c, b.crystal, 0.4f);
                        if (U.H01(gx, gy, 373) > 0.998f) return U.C(255, 255, 160);
                        return Pal.Ramp(c, Mathf.Max(0, lv - 1));
                    }
                case Biomes.Reef:
                    {
                        // deep water: branching coral silhouettes, kelp swaying up, rising bubbles
                        int kx = (gx >> 3) * 8 + 4 + (int)(2f * Mathf.Sin(gy * 0.09f + (gx >> 3) * 1.7f));
                        if (gx == kx && U.H01(gx >> 3, gy >> 6, 381) < 0.3f) return Color32.Lerp(c, U.C(60, 160, 100), 0.35f);
                        int cx = U.FloorDiv(gx, 40), lx = gx - cx * 40, ly = ((gy % 64) + 64) % 64;
                        if (U.H01(cx, gy >> 6, 382) < 0.5f && ly < 30)
                        {
                            float branch = Mathf.Abs(lx - 20 - Mathf.Sin(ly * 0.3f) * (ly * 0.25f));
                            if (branch < 2f || Mathf.Abs(lx - 20 + ly * 0.5f) < 1.5f || Mathf.Abs(lx - 20 - ly * 0.45f) < 1.5f) return Color32.Lerp(c, U.C(255, 120, 150), 0.25f);
                        }
                        if (U.H01(gx >> 1, gy >> 1, 383) > 0.996f) return U.C(180, 230, 255);
                        return Color32.Lerp(c, U.C(20, 70, 120), 0.2f);
                    }
            }
            return c;
        }
    }
}
