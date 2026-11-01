using UnityEngine;

namespace DigDeep
{
    // A shop deep underground is a little tavern-store: timbered walls with a wainscot of planks, rafters, shelves of
    // bottles and jars, pennants, a bar counter by the door with mugs on it, a framed picture, a stone hearth with a
    // crackling fire, candle chandeliers swaying from the ceiling, a rug, and a hanging sign outside each door.
    // (A premium shop: gilt trim and violet cloth.) The back wall is one picture drawn behind everything; the fire,
    // candles and sign are their own little sprites so they flicker and sway.
    public class ShopDecor : Entity
    {
        readonly int x0, floorY, tier;
        readonly Transform[] chandeliers = new Transform[2];
        readonly SpriteRenderer[] candleFlames = new SpriteRenderer[6];
        SpriteRenderer hearthFire;
        Vector2 hearthAt;
        readonly Vector2[] lampAt = new Vector2[2];
        float t, fireT;
        int fireFrame;

        const int TilesW = ShopRoom.Wd - 2, TilesH = ShopRoom.Ht - 2;   // the room's inside, in blocks
        const int PW = TilesW * 16, PH = TilesH * 16;                   // ...and in pixels

        public override bool AlwaysActive => false;

        public ShopDecor(ShopRoom room, int tier)
        {
            x0 = room.x0; floorY = room.FloorY; this.tier = tier;
            pos = new Vector2(x0 + 1 + TilesW * 0.5f, floorY + 1 + TilesH * 0.5f);
            MakeGO("ShopWall", tier > 0 ? PremiumWall(new System.Random(x0 * 131 + floorY)) : BackWall(new System.Random(x0 * 131 + floorY)), 1);   // in front of the rock, behind everything
            go.transform.position = new Vector3(x0 + 1, floorY + 1, 0f);

            // the hearth's fire (right-hand end of the back wall)
            hearthAt = new Vector2(x0 + 1 + (PW - 26) / 16f, floorY + 1 + 0.05f);   // (the middle of the hearth's mouth)
            var hf = new GameObject("HearthFire");
            hf.transform.SetParent(go.transform, false);
            hf.transform.position = new Vector3(hearthAt.x, hearthAt.y, 0f);
            hearthFire = Art.AddSR(hf, Art.Flames[0], 2);
            hf.transform.localScale = new Vector3(1.1f, 0.85f, 1f);

            // two candle chandeliers hanging from the ceiling, a third of the way in from each end
            for (int i = 0; i < 2; i++)
            {
                float cx = x0 + 1 + TilesW * (i == 0 ? 0.33f : 0.67f);
                float top = floorY + 1 + TilesH;
                var c = new GameObject("Chandelier");
                c.transform.SetParent(go.transform, false);
                c.transform.position = new Vector3(cx, top, 0f);
                Art.AddSR(c, tier > 0 ? CrystalChandelier(tier) : Chandelier(), 3);
                chandeliers[i] = c.transform;
                lampAt[i] = new Vector2(cx, top - 1.3f);
                for (int k = 0; k < 3; k++)
                {
                    var f = new GameObject("Candle");
                    f.transform.SetParent(c.transform, false);
                    f.transform.localPosition = new Vector3((k - 1) * 0.5f, k == 1 ? -1.0f : -1.125f, 0f);   // on top of each candle
                    var fs = Art.AddSR(f, Art.Flames[k % Art.Flames.Length], 4);
                    f.transform.localScale = new Vector3(0.28f, 0.32f, 1f);
                    candleFlames[i * 3 + k] = fs;
                }
            }

            // hanging signs outside both doors (the corridors either side lead in)
            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? x0 - 2f : x0 + ShopRoom.Wd + 2f;
                var s = new GameObject("ShopSign");
                s.transform.SetParent(go.transform, false);
                s.transform.position = new Vector3(sx, floorY + 3f, 0f);
                Art.AddSR(s, tier > 0 ? PremiumSign(tier) : Sign(side == 1), 3);
            }
        }

        public override void Tick(float dt)
        {
            t += dt;
            // the chandeliers sway a touch, the candles and the hearth flicker
            for (int i = 0; i < 2; i++)
                if (chandeliers[i] != null) chandeliers[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.3f + i * 2f) * 2.5f);
            fireT -= dt;
            if (fireT <= 0f)
            {
                fireT = Random.Range(0.08f, 0.14f);
                fireFrame = (fireFrame + 1 + Random.Range(0, 2)) % Art.Flames.Length;
                if (hearthFire != null) hearthFire.sprite = Art.Flames[fireFrame];
                for (int k = 0; k < candleFlames.Length; k++)
                    if (candleFlames[k] != null && Random.value < 0.5f) candleFlames[k].sprite = Art.Flames[Random.Range(0, Art.Flames.Length)];
                // a premium shop's crystal chandeliers glint now and then
                if (tier > 0 && Random.value < 0.35f)
                    G.fx.Burst(lampAt[Random.Range(0, 2)] + new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(-0.4f, 0.5f)), 1,
                        tier >= 2 ? new Color(0.85f, 0.7f, 1f) : new Color(1f, 0.95f, 0.75f), 0.4f, 0.5f, 0.2f, 1f, false);
                if (Random.value < 0.25f) G.fx.Burst(hearthAt + new Vector2(Random.Range(-0.3f, 0.3f), 0.6f), 1, new Color(1f, Random.Range(0.5f, 0.85f), 0.2f), 0.8f, 0.6f, -4f, 1f, false);
            }
        }

        public override void AddLight(Lighting l)
        {
            float fl = 0.9f + 0.1f * Mathf.Sin(t * 13f) * Mathf.Sin(t * 7.3f);
            l.Add(hearthAt + Vector2.up * 0.5f, 4.2f * fl, 0.85f);
            for (int i = 0; i < 2; i++) l.Add(lampAt[i], (tier > 0 ? 5.5f : 4.5f) * fl, tier > 0 ? 0.85f : 0.75f);
        }

        // ------------------------------------------------------------------ the pictures

        static Color32 C(int r, int g, int b, int a = 255) => new Color32((byte)r, (byte)g, (byte)b, (byte)a);
        static Color32 Sh(Color32 c, float k) => new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), c.a);

        static void Fill(Color32[] px, int w, int h, int x, int y, int rw, int rh, Color32 c)
        {
            for (int yy = y; yy < y + rh; yy++)
                for (int xx = x; xx < x + rw; xx++)
                    if (xx >= 0 && yy >= 0 && xx < w && yy < h) px[yy * w + xx] = c;
        }
        static void Dot(Color32[] px, int w, int h, int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = c; }

        static Sprite Make(Color32[] px, int w, int h, Vector2 pivot)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, Art.PPU, 0, SpriteMeshType.FullRect);
        }

        Sprite BackWall(System.Random r)
        {
            bool prem = tier > 0;
            var px = new Color32[PW * PH];
            // colours: warm plaster above, dark planks below, darker timber
            Color32 plaster = prem ? C(86, 58, 92) : C(96, 60, 52), plasterD = Sh(plaster, 0.82f);
            Color32 plank = C(98, 62, 38), plankD = C(74, 46, 28), plankL = C(124, 82, 50);
            Color32 timber = C(58, 36, 24), timberL = C(84, 54, 34);
            Color32 trim = prem ? C(232, 186, 74) : C(140, 96, 58);
            Color32 cloth = prem ? C(130, 70, 190) : C(176, 52, 48), clothL = Sh(cloth, 1.25f);

            // plaster with a mottled texture
            for (int y = 0; y < PH; y++)
                for (int x = 0; x < PW; x++)
                {
                    uint hsh = U.Hash(x / 2, y / 2, 991);
                    px[y * PW + x] = hsh % 9 == 0 ? plasterD : plaster;
                }
            // the wainscot: vertical planks up to about one and a half blocks
            int wain = 22;
            for (int y = 0; y < wain; y++)
                for (int x = 0; x < PW; x++)
                {
                    int col = x / 5;
                    Color32 c = x % 5 == 0 ? plankD : (U.Hash(col, y / 6, 17) % 5 == 0 ? plankL : plank);
                    px[y * PW + x] = c;
                }
            Fill(px, PW, PH, 0, wain, PW, 2, timber);
            Fill(px, PW, PH, 0, wain + 2, PW, 1, trim);
            // rafters under the ceiling and timber posts every four blocks
            Fill(px, PW, PH, 0, PH - 5, PW, 5, timber);
            Fill(px, PW, PH, 0, PH - 5, PW, 1, timberL);
            for (int bx = 0; bx <= TilesW; bx += 4)
            {
                int x = Mathf.Min(PW - 4, bx * 16 - 2);
                Fill(px, PW, PH, x, 0, 4, PH, timber);
                Fill(px, PW, PH, x + 1, 0, 1, PH, timberL);
                // a little brace from the post up to the rafter
                for (int k = 0; k < 6; k++) { Dot(px, PW, PH, x + 4 + k, PH - 11 + k, timber); Dot(px, PW, PH, x - 1 - k, PH - 11 + k, timber); }
            }

            // pennants strung under the rafter
            for (int x = 6; x < PW - 6; x += 9)
            {
                int sag = (int)(2f * Mathf.Sin((x % 64) / 64f * Mathf.PI));
                int top = PH - 6 - sag;
                for (int k = 0; k < 6; k++)
                    for (int j = 0; j < 6 - k; j++)
                        Dot(px, PW, PH, x + j - (6 - k) / 2 + 2, top - k, (x / 9) % 2 == 0 ? cloth : (prem ? trim : C(222, 190, 96)));
                Dot(px, PW, PH, x, top + 1, timberL);
            }

            // shelves on the back wall with bottles and jars (between the posts, above the wares)
            int shelfY = 50;
            for (int seg = 0; seg < TilesW / 4; seg++)
            {
                int sx = seg * 64 + 6, ex = seg * 64 + 58;
                if (seg == TilesW / 4 - 1) ex -= 30;   // (the hearth's chimney is at the far end)
                Fill(px, PW, PH, sx, shelfY, ex - sx, 2, timberL);
                Fill(px, PW, PH, sx, shelfY - 1, ex - sx, 1, timber);
                for (int x = sx + 2; x < ex - 3; x += 4 + r.Next(3))
                {
                    int kind = r.Next(4);
                    Color32 glass = new[] { C(90, 170, 110), C(150, 70, 60), C(90, 120, 190), C(200, 160, 80) }[r.Next(4)];
                    if (kind == 0) { Fill(px, PW, PH, x, shelfY + 2, 3, 5, glass); Fill(px, PW, PH, x + 1, shelfY + 7, 1, 2, glass); Dot(px, PW, PH, x, shelfY + 6, Sh(glass, 1.4f)); }   // a bottle
                    else if (kind == 1) { Fill(px, PW, PH, x, shelfY + 2, 4, 4, C(170, 120, 80)); Fill(px, PW, PH, x, shelfY + 6, 4, 1, C(120, 80, 50)); }   // a jar
                    else if (kind == 2) { Fill(px, PW, PH, x, shelfY + 2, 2, 7, C(230, 220, 190)); Fill(px, PW, PH, x + 2, shelfY + 2, 2, 6, C(150, 60, 50)); }   // books
                    else { Fill(px, PW, PH, x, shelfY + 2, 3, 3, C(200, 200, 210)); Dot(px, PW, PH, x + 1, shelfY + 5, C(250, 210, 90)); }   // a little lamp
                }
            }

            // the bar counter by the left door, mugs on it
            int cx0 = 4, cx1 = 52, ch = 18;
            Fill(px, PW, PH, cx0, 0, cx1 - cx0, ch, plankD);
            for (int x = cx0; x < cx1; x += 6) Fill(px, PW, PH, x, 0, 1, ch, timber);
            Fill(px, PW, PH, cx0 - 2, ch, cx1 - cx0 + 4, 3, timberL);
            Fill(px, PW, PH, cx0 - 2, ch + 2, cx1 - cx0 + 4, 1, trim);
            for (int m = 0; m < 3; m++)
            {
                int mx = cx0 + 8 + m * 13;
                Fill(px, PW, PH, mx, ch + 3, 4, 5, C(200, 160, 90));
                Fill(px, PW, PH, mx, ch + 7, 4, 1, C(250, 245, 230));
                Dot(px, PW, PH, mx + 4, ch + 5, C(200, 160, 90)); Dot(px, PW, PH, mx + 5, ch + 5, C(200, 160, 90)); Dot(px, PW, PH, mx + 4, ch + 4, C(200, 160, 90));
            }
            // a till on the counter
            Fill(px, PW, PH, cx1 - 12, ch + 3, 8, 6, C(150, 120, 60)); Fill(px, PW, PH, cx1 - 11, ch + 7, 6, 1, trim);

            // a framed picture (a pickaxe on it) above the counter
            int fx = 20, fy = 58;
            Fill(px, PW, PH, fx, fy, 18, 13, trim);
            Fill(px, PW, PH, fx + 1, fy + 1, 16, 11, C(60, 80, 110));
            for (int k = 0; k < 9; k++) Dot(px, PW, PH, fx + 4 + k, fy + 2 + k, C(150, 110, 70));
            for (int k = 0; k < 8; k++) Dot(px, PW, PH, fx + 3 + k, fy + 10 - k / 3, C(190, 190, 200));

            // the stone hearth at the right end: an arch with a dark mouth, and its chimney breast going up
            int hx0 = PW - 3 * 16 + 2, hx1 = PW - 6;
            Color32 stone = C(110, 104, 100), stoneD = C(80, 74, 72), soot = C(26, 18, 16);
            Fill(px, PW, PH, hx0 + 6, 30, hx1 - hx0 - 12, PH - 35, stoneD);
            for (int y = 30; y < PH - 5; y += 5) for (int x = hx0 + 6 + ((y / 5) % 2) * 4; x < hx1 - 6; x += 8) Dot(px, PW, PH, x, y, stone);
            for (int y = 0; y < 30; y++)
                for (int x = hx0; x < hx1; x++)
                {
                    bool brick = ((x - hx0 + (y / 4 % 2) * 4) % 8 == 0) || y % 4 == 0;
                    px[y * PW + x] = brick ? stoneD : stone;
                }
            // its mouth
            int mx0 = hx0 + 7, mx1 = hx1 - 7;
            for (int y = 0; y < 20; y++)
                for (int x = mx0; x < mx1; x++)
                {
                    float u = (x - (mx0 + mx1) * 0.5f) / ((mx1 - mx0) * 0.5f);
                    if (y < 14 || u * u + (y - 14) * (y - 14) / 36f < 1f) px[y * PW + x] = soot;
                }
            Fill(px, PW, PH, hx0 - 2, 28, hx1 - hx0 + 4, 3, timberL);   // the mantel
            Dot(px, PW, PH, hx0 + 6, 31, C(220, 210, 180)); Fill(px, PW, PH, hx0 + 12, 31, 3, 4, C(200, 160, 90));   // a candle stub and a jug on it

            // a rug on the floor in front of the wares
            int rx0 = 64, rx1 = PW - 64;
            Fill(px, PW, PH, rx0, 0, rx1 - rx0, 2, cloth);
            for (int x = rx0; x < rx1; x += 4) Dot(px, PW, PH, x, 1, clothL);
            Fill(px, PW, PH, rx0, 2, rx1 - rx0, 1, prem ? trim : C(222, 190, 96));

            return Make(px, PW, PH, Vector2.zero);
        }

        // a ring of iron on chains, three candles on it (the flames are separate, so they flicker)
        static Sprite Chandelier()
        {
            int w = 28, h = 24;
            var px = new Color32[w * h];
            Color32 iron = C(46, 40, 44), ironL = C(90, 82, 88), wax = C(236, 228, 200);
            for (int y = 6; y < h; y++) Dot(px, w, h, w / 2, y, iron);                         // the chain
            for (int k = 0; k < 7; k++) { Dot(px, w, h, w / 2 - 1 - k, 6 - k / 2, iron); Dot(px, w, h, w / 2 + 1 + k, 6 - k / 2, iron); }
            Fill(px, w, h, 3, 1, w - 6, 2, iron); Fill(px, w, h, 3, 2, w - 6, 1, ironL);         // the ring
            for (int k = 0; k < 3; k++)
            {
                int cx = w / 2 + (k - 1) * 8;
                Fill(px, w, h, cx - 1, 3 + (k == 1 ? 2 : 0), 2, 3, wax);                        // candles
                Dot(px, w, h, cx - 2, 2, ironL); Dot(px, w, h, cx + 1, 2, ironL);
            }
            return Make(px, w, h, new Vector2(0.5f, 1f));
        }

        // a hanging wooden sign: a coin on it (on one side a mug: it's an inn too)
        static Sprite Sign(bool mug)
        {
            int w = 18, h = 16;
            var px = new Color32[w * h];
            Color32 wood = C(120, 78, 46), woodD = C(80, 50, 30), iron = C(50, 44, 48), gold = C(240, 196, 70);
            Fill(px, w, h, 0, h - 2, w, 2, iron);                         // the bracket
            Dot(px, w, h, 4, h - 3, iron); Dot(px, w, h, 13, h - 3, iron); Dot(px, w, h, 4, h - 4, iron); Dot(px, w, h, 13, h - 4, iron);
            Fill(px, w, h, 1, 0, w - 2, h - 5, woodD);
            Fill(px, w, h, 2, 1, w - 4, h - 7, wood);
            if (!mug)
            {
                for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++) { float dx = x - 3, dy = y - 3; if (dx * dx + dy * dy <= 10f) Dot(px, w, h, 6 + x, 2 + y, gold); }
                Fill(px, w, h, 8, 4, 2, 3, C(200, 150, 40));
            }
            else
            {
                Fill(px, w, h, 6, 2, 5, 6, C(200, 160, 90)); Fill(px, w, h, 6, 7, 5, 1, C(250, 245, 230));
                Dot(px, w, h, 11, 4, C(200, 160, 90)); Dot(px, w, h, 12, 5, C(200, 160, 90)); Dot(px, w, h, 11, 6, C(200, 160, 90));
            }
            return Make(px, w, h, new Vector2(0.5f, 1f));
        }

        // A premium shop is a jeweller's salon, not a tavern: damask silk on the walls, a marble wainscot, gilded
        // pilasters and crown moulding, velvet drapes tied back with gold rope, glass display cabinets of gems,
        // a gilt-framed portrait, a marble fireplace and a red carpet with a gold border. (Legendary: royal violet.)
        Sprite PremiumWall(System.Random r)
        {
            bool legend = tier >= 2;
            var px = new Color32[PW * PH];
            Color32 silk = legend ? C(62, 30, 88) : C(92, 22, 34), silkD = Sh(silk, 0.72f), silkL = Sh(silk, 1.35f);
            Color32 gold = C(238, 192, 72), goldD = C(170, 118, 40), goldL = C(255, 238, 150);
            Color32 marble = C(226, 222, 214), marbleD = C(178, 172, 168), vein = C(140, 132, 136);
            Color32 velvet = legend ? C(120, 52, 170) : C(150, 26, 40), velvetD = Sh(velvet, 0.65f), velvetL = Sh(velvet, 1.3f);
            Color32 glass = C(150, 200, 220, 140), wood = C(70, 34, 26);

            // damask: a diamond lattice with a little fleur in each diamond
            for (int y = 0; y < PH; y++)
                for (int x = 0; x < PW; x++)
                {
                    int u = (x + y) % 12, v = (x - y + 1200) % 12;
                    Color32 c = silk;
                    if (u == 0 || v == 0) c = silkD;
                    else
                    {
                        int cxm = (x + y) / 12, cym = (x - y + 1200) / 12;
                        int du = u - 6, dv = v - 6;
                        if (Mathf.Abs(du) + Mathf.Abs(dv) <= 2 && ((cxm + cym) & 1) == 0) c = silkL;
                    }
                    px[y * PW + x] = c;
                }

            // marble wainscot with veins, a gold rail on top
            int wain = 22;
            for (int y = 0; y < wain; y++)
                for (int x = 0; x < PW; x++)
                {
                    uint h = U.Hash(x / 3, y / 3, 4711);
                    Color32 c = h % 7 == 0 ? marbleD : marble;
                    // diagonal veins
                    if (((x * 2 + y * 3 + (int)(U.Hash(x / 9, 0, 7) % 5)) % 37) == 0) c = vein;
                    px[y * PW + x] = c;
                }
            for (int x = 0; x < PW; x += 32) Fill(px, PW, PH, x, 0, 1, wain, marbleD);   // marble panels
            Fill(px, PW, PH, 0, wain, PW, 1, goldD);
            Fill(px, PW, PH, 0, wain + 1, PW, 2, gold);
            Fill(px, PW, PH, 0, wain + 3, PW, 1, goldL);

            // crown moulding: stepped gold under the ceiling with dentils
            Fill(px, PW, PH, 0, PH - 6, PW, 6, goldD);
            Fill(px, PW, PH, 0, PH - 5, PW, 2, gold);
            Fill(px, PW, PH, 0, PH - 3, PW, 1, goldL);
            for (int x = 1; x < PW; x += 4) Fill(px, PW, PH, x, PH - 8, 2, 2, gold);

            // gilded pilasters every four blocks: fluted, with capitals and bases
            for (int bx = 0; bx <= TilesW; bx += 4)
            {
                int x = Mathf.Clamp(bx * 16 - 3, 0, PW - 6);
                Fill(px, PW, PH, x, wain + 4, 6, PH - wain - 12, goldD);
                Fill(px, PW, PH, x + 1, wain + 4, 1, PH - wain - 12, goldL);
                Fill(px, PW, PH, x + 3, wain + 4, 1, PH - wain - 12, gold);
                Fill(px, PW, PH, x - 1, PH - 11, 8, 3, gold); Fill(px, PW, PH, x - 1, PH - 9, 8, 1, goldL);   // capital
                Fill(px, PW, PH, x - 1, wain + 4, 8, 2, gold);                                                // base
            }

            // velvet drapes at both ends, tied back with gold rope
            for (int side = 0; side < 2; side++)
            {
                for (int y = wain + 4; y < PH - 6; y++)
                {
                    float k = (y - (wain + 4)) / (float)(PH - 10 - wain);
                    int width = 4 + (int)(10 * Mathf.Abs(k - 0.35f) * 1.6f);   // pinched where the rope ties it
                    for (int i = 0; i < width; i++)
                    {
                        int x = side == 0 ? i : PW - 1 - i;
                        px[y * PW + x] = (i % 4 == 1) ? velvetL : (i % 4 == 3 ? velvetD : velvet);
                    }
                }
                int ry = wain + 4 + (int)((PH - 10 - wain) * 0.35f);
                for (int i = 0; i < 6; i++) Dot(px, PW, PH, side == 0 ? i : PW - 1 - i, ry, gold);
                Dot(px, PW, PH, side == 0 ? 6 : PW - 7, ry - 1, goldL); Dot(px, PW, PH, side == 0 ? 6 : PW - 7, ry - 2, gold);
                // a valance swag across the top
            }
            for (int x = 0; x < PW; x++)
            {
                int dip = (int)(3f * Mathf.Abs(Mathf.Sin(x / 32f * Mathf.PI)));
                Fill(px, PW, PH, x, PH - 7 - dip, 1, dip + 1, velvet);
                if (x % 32 == 0) { Fill(px, PW, PH, x - 1, PH - 9, 3, 3, gold); Dot(px, PW, PH, x, PH - 11, goldD); Dot(px, PW, PH, x, PH - 12, goldD); }
            }

            // glass display cabinets between the pilasters: wooden frames, gold edges, gems on velvet shelves
            Color32[] gems = { C(240, 60, 80), C(80, 200, 255), C(120, 240, 120), C(250, 220, 90), C(210, 120, 255), C(255, 255, 255) };
            int cabY = 44, cabH = 22;
            for (int seg = 0; seg < TilesW / 4; seg++)
            {
                int sx = seg * 64 + 10, ex = seg * 64 + 54;
                if (seg == 0) continue;               // (the portrait hangs there)
                if (seg == TilesW / 4 - 1) ex -= 30;
                if (ex - sx < 12) continue;
                Fill(px, PW, PH, sx - 1, cabY - 1, ex - sx + 2, cabH + 2, goldD);
                Fill(px, PW, PH, sx, cabY, ex - sx, cabH, wood);
                for (int shelf = 0; shelf < 2; shelf++)
                {
                    int sy = cabY + 1 + shelf * 11;
                    Fill(px, PW, PH, sx + 1, sy, ex - sx - 2, 9, velvetD);
                    Fill(px, PW, PH, sx + 1, sy, ex - sx - 2, 1, gold);
                    for (int x = sx + 3; x < ex - 4; x += 5 + r.Next(3))
                    {
                        var g = gems[r.Next(gems.Length)];
                        int kind = r.Next(3);
                        if (kind == 0) { Dot(px, PW, PH, x + 1, sy + 4, g); Fill(px, PW, PH, x, sy + 2, 3, 2, g); Dot(px, PW, PH, x + 1, sy + 1, g); Dot(px, PW, PH, x, sy + 3, C(255, 255, 255)); }   // a cut gem
                        else if (kind == 1) { for (int k = 0; k < 4; k++) { Dot(px, PW, PH, x + k, sy + 4 - (k == 1 || k == 2 ? 1 : 0), gold); } Dot(px, PW, PH, x + 1, sy + 5, g); }   // a ring / necklace
                        else { Fill(px, PW, PH, x, sy + 1, 2, 5, gold); Dot(px, PW, PH, x, sy + 5, g); Dot(px, PW, PH, x + 1, sy + 5, g); }   // a golden idol
                    }
                }
                // the glass: a faint sheen with a diagonal glare
                for (int y = cabY; y < cabY + cabH; y++)
                    for (int x = sx; x < ex; x++)
                        if (((x - sx) + (y - cabY)) % 17 == 0 || ((x - sx) + (y - cabY)) % 17 == 1) px[y * PW + x] = Color32.Lerp(px[y * PW + x], glass, 0.55f);
            }

            // a gilt-framed portrait (a crowned digger) over the left
            int fx = 22, fy = 40;
            if (fy + 18 < PH - 10)
            {
                Fill(px, PW, PH, fx - 1, fy - 1, 22, 20, goldD);
                Fill(px, PW, PH, fx, fy, 20, 18, gold);
                for (int k = 0; k < 20; k += 2) { Dot(px, PW, PH, fx + k, fy, goldL); Dot(px, PW, PH, fx + k, fy + 17, goldL); }
                Fill(px, PW, PH, fx + 2, fy + 2, 16, 14, C(40, 50, 70));
                Fill(px, PW, PH, fx + 6, fy + 2, 8, 6, velvet);           // shoulders
                Fill(px, PW, PH, fx + 7, fy + 8, 6, 5, C(230, 180, 140)); // face
                Fill(px, PW, PH, fx + 7, fy + 13, 6, 1, gold); Dot(px, PW, PH, fx + 7, fy + 14, gold); Dot(px, PW, PH, fx + 10, fy + 14, gold); Dot(px, PW, PH, fx + 12, fy + 14, gold);   // crown
            }

            // the counter by the left door: polished dark wood, marble top, a golden scale on it
            int cx0 = 4, cx1 = 52, ch = 18;
            Fill(px, PW, PH, cx0, 0, cx1 - cx0, ch, wood);
            for (int x = cx0 + 3; x < cx1 - 3; x += 9) { Fill(px, PW, PH, x, 3, 6, ch - 6, Sh(wood, 1.3f)); Fill(px, PW, PH, x + 1, 4, 4, ch - 8, wood); }
            Fill(px, PW, PH, cx0 - 2, ch, cx1 - cx0 + 4, 3, marble);
            Fill(px, PW, PH, cx0 - 2, ch, cx1 - cx0 + 4, 1, gold);
            int scx = cx0 + 14;
            Fill(px, PW, PH, scx, ch + 3, 1, 8, gold); Fill(px, PW, PH, scx - 6, ch + 10, 13, 1, gold);
            Fill(px, PW, PH, scx - 7, ch + 5, 4, 1, goldL); Fill(px, PW, PH, scx + 4, ch + 5, 4, 1, goldL);
            for (int k = 0; k < 4; k++) { Dot(px, PW, PH, scx - 6 + k / 2, ch + 6 + k, goldD); Dot(px, PW, PH, scx + 6 - k / 2, ch + 6 + k, goldD); }
            Fill(px, PW, PH, cx1 - 16, ch + 3, 5, 3, gold); Fill(px, PW, PH, cx1 - 15, ch + 6, 3, 1, goldL);   // a stack of coins
            Fill(px, PW, PH, cx1 - 9, ch + 3, 4, 2, gold);

            // a marble fireplace at the right end with a gold grate and a mirror above
            int hx0 = PW - 3 * 16 + 2, hx1 = PW - 6;
            Fill(px, PW, PH, hx0, 0, hx1 - hx0, 30, marble);
            for (int y = 0; y < 30; y++) for (int x = hx0; x < hx1; x++) if (((x * 3 + y * 2) % 29) == 0) px[y * PW + x] = vein;
            int mx0 = hx0 + 7, mx1 = hx1 - 7;
            for (int y = 0; y < 20; y++)
                for (int x = mx0; x < mx1; x++)
                {
                    float u = (x - (mx0 + mx1) * 0.5f) / ((mx1 - mx0) * 0.5f);
                    if (y < 14 || u * u + (y - 14) * (y - 14) / 36f < 1f) px[y * PW + x] = C(24, 14, 16);
                }
            Fill(px, PW, PH, mx0, 0, mx1 - mx0, 2, gold);
            for (int x = mx0 + 1; x < mx1; x += 3) Fill(px, PW, PH, x, 2, 1, 3, goldD);
            Fill(px, PW, PH, hx0 - 3, 28, hx1 - hx0 + 6, 3, marble); Fill(px, PW, PH, hx0 - 3, 30, hx1 - hx0 + 6, 1, gold);
            // the mirror
            int my0 = 36, my1 = Mathf.Min(PH - 14, 64);
            if (my1 > my0 + 6)
            {
                Fill(px, PW, PH, hx0 + 4, my0, hx1 - hx0 - 8, my1 - my0, gold);
                Fill(px, PW, PH, hx0 + 6, my0 + 2, hx1 - hx0 - 12, my1 - my0 - 4, C(130, 160, 185));
                for (int k = 0; k < 8; k++) Dot(px, PW, PH, hx0 + 9 + k, my1 - 5 - k, C(220, 235, 245));
                Dot(px, PW, PH, (hx0 + hx1) / 2, my1, goldL); Dot(px, PW, PH, (hx0 + hx1) / 2, my1 + 1, gold);
            }
            // gold candlesticks on the mantel
            Fill(px, PW, PH, hx0 + 2, 31, 1, 4, gold); Dot(px, PW, PH, hx0 + 2, 35, C(250, 240, 210));
            Fill(px, PW, PH, hx1 - 3, 31, 1, 4, gold); Dot(px, PW, PH, hx1 - 3, 35, C(250, 240, 210));

            // the red carpet with a gold border and tassels
            Color32 carpet = legend ? C(96, 40, 140) : C(170, 30, 44);
            int rx0 = 60, rx1 = PW - 60;
            Fill(px, PW, PH, rx0, 0, rx1 - rx0, 3, carpet);
            for (int x = rx0 + 2; x < rx1 - 2; x += 6) Dot(px, PW, PH, x, 1, gold);
            Fill(px, PW, PH, rx0, 3, rx1 - rx0, 1, gold);
            for (int y = 0; y < 3; y++) { Dot(px, PW, PH, rx0 - 1, y, goldL); Dot(px, PW, PH, rx1, y, goldL); }

            return Make(px, PW, PH, Vector2.zero);
        }

        // a gilded chandelier hung with crystal drops (the candle flames are separate, so they flicker)
        static Sprite CrystalChandelier(int tier)
        {
            int w = 32, h = 26;
            var px = new Color32[w * h];
            Color32 gold = C(238, 192, 72), goldD = C(170, 118, 40), goldL = C(255, 238, 150), wax = C(250, 244, 226);
            Color32 crystal = tier >= 2 ? C(220, 180, 255) : C(210, 240, 255), crystalD = tier >= 2 ? C(150, 110, 220) : C(130, 180, 220);
            for (int y = 8; y < h; y++) Dot(px, w, h, w / 2, y, (y % 2 == 0) ? gold : goldD);           // the chain
            Fill(px, w, h, w / 2 - 2, 8, 5, 3, gold); Dot(px, w, h, w / 2, 11, goldL);                     // the boss
            for (int k = 0; k < 9; k++) { Dot(px, w, h, w / 2 - 1 - k, 7 - k / 3, gold); Dot(px, w, h, w / 2 + 1 + k, 7 - k / 3, gold); }   // the arms
            Fill(px, w, h, 3, 3, w - 6, 2, gold); Fill(px, w, h, 3, 4, w - 6, 1, goldL);                  // the ring
            // crystal drops hanging below the ring
            for (int x = 4; x < w - 4; x += 3)
            {
                int len = 1 + ((x / 3) % 2);
                for (int k = 0; k < len; k++) Dot(px, w, h, x, 2 - k, crystalD);
                Dot(px, w, h, x, 1 - len, crystal);
            }
            for (int k = 0; k < 3; k++)
            {
                int cx = w / 2 + (k - 1) * 8;
                Fill(px, w, h, cx - 1, 5 + (k == 1 ? 2 : 0), 2, 3, wax);
                Dot(px, w, h, cx - 2, 4, goldL); Dot(px, w, h, cx + 1, 4, goldL);
            }
            return Make(px, w, h, new Vector2(0.5f, 1f));
        }

        // a gilt sign with a cut gem on it
        static Sprite PremiumSign(int tier)
        {
            int w = 18, h = 16;
            var px = new Color32[w * h];
            Color32 gold = C(238, 192, 72), goldD = C(170, 118, 40), goldL = C(255, 238, 150), iron = C(50, 44, 48);
            Color32 back = tier >= 2 ? C(70, 30, 100) : C(110, 20, 34);
            Color32 gem = tier >= 2 ? C(210, 130, 255) : C(90, 210, 255), gemL = C(255, 255, 255);
            Fill(px, w, h, 0, h - 2, w, 2, iron);
            Dot(px, w, h, 4, h - 3, gold); Dot(px, w, h, 13, h - 3, gold); Dot(px, w, h, 4, h - 4, gold); Dot(px, w, h, 13, h - 4, gold);
            Fill(px, w, h, 1, 0, w - 2, h - 5, goldD);
            Fill(px, w, h, 2, 1, w - 4, h - 7, back);
            Dot(px, w, h, 1, 0, goldL); Dot(px, w, h, w - 2, 0, goldL); Dot(px, w, h, 1, h - 6, goldL); Dot(px, w, h, w - 2, h - 6, goldL);
            // the gem: a diamond shape
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 7; x++)
                    if (Mathf.Abs(x - 3) + Mathf.Abs(y - 3) <= 3) Dot(px, w, h, 5 + x, 2 + y, (x + y) % 3 == 0 ? Sh(gem, 0.7f) : gem);
            Dot(px, w, h, 7, 6, gemL); Dot(px, w, h, 8, 7, gemL);
            Fill(px, w, h, 2, 1, w - 4, 1, gold);
            return Make(px, w, h, new Vector2(0.5f, 1f));
        }
    }
}
