using UnityEngine;

namespace DigDeep
{
    // Liquids and gas, kept apart from the block grid. Every cell can hold an amount (0..1) of one fluid:
    // water, lava or steam. Liquids fall, then spread sideways, thinning out but never below MinLevel
    // (a puddle never just vanishes because it got thin). Lava is thick and slow. Water meeting lava turns
    // the lava into rock and boils off as steam. Steam rises, spreads under ceilings and slowly fades.
    // Cold biomes freeze water solid; hot biomes boil exposed water away.
    public class Fluids
    {
        public const byte None = 0, Water = 1, Lava = 2, Steam = 3;
        public const float MinLevel = 0.125f;
        const float Step = 1f / 30f;

        readonly World w;
        float acc, hissT, renderT;
        int frame;

        public Fluids(World w, Transform root)
        {
            this.w = w;
            go = new GameObject("Fluids");
            go.transform.SetParent(root, false);
            sr = Art.AddSR(go, null, 30);
        }

        // ------------------------------------------------------------------ simulation

        public void Tick(float dt, float camY, float halfH)
        {
            acc += dt;
            hissT -= dt;
            int n = 0;
            while (acc >= Step && n < 2) { acc -= Step; Simulate(camY, halfH); n++; }
            if (acc > Step) acc = 0f;
        }

        // reference to one cell's storage
        struct C { public Chunk c; public int i; public bool ok; }

        C Cell(int x, int y)
        {
            var r = new C();
            if (x < 0 || x >= World.W) return r;
            if (!w.TryChunk(U.FloorDiv(y, World.CH), out var ch)) return r;
            r.c = ch; r.i = (y - ch.Y0) * World.W + x; r.ok = true;
            return r;
        }

        static bool Solid(C k) => !k.ok || Blocks(k.c.t[k.i]);

        // Liquids flow around and through chests, crates and spikes like real water; only real blocks stop them.
        // Things sinking through water or lava slow right down.
        public static float Sink(Vector2 p, float vy, float dt)
        {
            byte f = Game.I.world.FluidAtPoint(p);
            if (f != Water && f != Lava) return vy;
            float cap = f == Lava ? -2f : -3.2f;
            return vy < cap ? Mathf.MoveTowards(vy, cap, 90f * dt) : vy;
        }

        public static bool Blocks(Block b) => Tiles.Solid(b) && b != Block.Chest && b != Block.Crate && b != Block.Supply && b != Block.Spikes;

        void Simulate(float camY, float halfH)
        {
            frame++;
            int yTop = Mathf.Min(0, Mathf.FloorToInt(camY + halfH + 14));
            int yBot = Mathf.FloorToInt(camY - halfH - 16);
            bool lavaStep = frame % 3 == 0;
            bool ltr = (frame & 1) == 0;
            // liquids from the bottom up, so falls settle in one pass
            for (int y = yBot; y <= yTop; y++)
                for (int k = 0; k < World.W; k++)
                    Liquid(ltr ? k : World.W - 1 - k, y, lavaStep, ltr);
            // steam from the top down
            for (int y = yTop; y >= yBot; y--)
                for (int k = 0; k < World.W; k++)
                    Gas(ltr ? k : World.W - 1 - k, y);
        }

        void Liquid(int x, int y, bool lavaStep, bool ltr)
        {
            var me = Cell(x, y);
            if (!me.ok) return;
            byte t = me.c.lt[me.i];
            if (t != Water && t != Lava) return;
            if (t == Lava && !lavaStep) return;
            float a = me.c.liq[me.i];
            if (Blocks(me.c.t[me.i])) { Clear(me); return; }
            int depth = -y;
            int bi = Biomes.Index(depth);

            if (t == Water)
            {
                // cold biome: water freezes solid
                // cold biome: water freezes solid, but only after a while: it skins over from the surface and from
                // ice it touches (a few seconds), and still pools freeze through on their own given time
                if (bi == Biomes.Crystal)
                {
                    bool nearIce = w.Get(x + 1, y) == Block.Ice || w.Get(x - 1, y) == Block.Ice || w.Get(x, y + 1) == Block.Ice || w.Get(x, y - 1) == Block.Ice;
                    var abv = Cell(x, y + 1);
                    bool surface = !Solid(abv) && abv.c.lt[abv.i] != Water;
                    float chance = nearIce ? 0.006f : surface ? 0.0025f : 0.0008f;
                    if (a > 0.6f && Random.value < chance) { Clear(me); w.Set(x, y, Block.Ice); return; }
                }
                // hot biome: exposed water boils away
                if (bi == Biomes.Magma)
                {
                    var up = Cell(x, y + 1);
                    if (!Solid(up) && up.c.lt[up.i] != Water)
                    {
                        float ev = 0.05f * Step * 3f;
                        a -= ev;
                        AddGas(up, ev * 1.5f);
                        if (a < MinLevel * 0.5f) { Clear(me); return; }
                        me.c.liq[me.i] = a;
                    }
                }
            }
            else
            {
                // lava touching water (or ice) cools into rock and boils the water into steam
                if (React(x, y, me, x + 1, y) || React(x, y, me, x - 1, y) || React(x, y, me, x, y + 1) || React(x, y, me, x, y - 1)) return;
            }

            // fall
            var below = Cell(x, y - 1);
            if (!Solid(below))
            {
                byte bt = below.c.lt[below.i];
                if (bt == None || bt == Steam || bt == t)
                {
                    float room = 1f - (bt == t ? below.c.liq[below.i] : 0f);
                    float mv = Mathf.Min(a, room);
                    if (mv > 0.0001f)
                    {
                        if (bt == Steam) { below.c.liq[below.i] = 0f; }
                        below.c.lt[below.i] = t;
                        below.c.liq[below.i] = (bt == t ? below.c.liq[below.i] : 0f) + mv;
                        a -= mv;
                        if (a <= 0.0001f) { Clear(me); return; }
                        me.c.liq[me.i] = a;
                    }
                }
            }

            // spread sideways (lava is much thicker)
            float rate = t == Lava ? 0.3f : 0.45f;
            for (int s = 0; s < 2; s++)
            {
                int dx = (s == 0) == ltr ? 1 : -1;
                var n = Cell(x + dx, y);
                if (Solid(n)) continue;
                byte nt = n.c.lt[n.i];
                if (nt != None && nt != t && nt != Steam) continue;
                float na = nt == t ? n.c.liq[n.i] : 0f;
                // pour harder over an edge
                var nb = Cell(x + dx, y - 1);
                bool edge = !Solid(nb) && (nb.c.lt[nb.i] == None || nb.c.lt[nb.i] == Steam || (nb.c.lt[nb.i] == t && nb.c.liq[nb.i] < 1f));
                if (a <= na + 0.004f && !edge) continue;
                float flow = edge ? Mathf.Max(a - MinLevel, 0f) * 0.5f : (a - na) * rate * 0.5f;
                if (na <= 0f)
                {
                    if (a < MinLevel * 2f && !edge) continue;    // never spread thinner than MinLevel
                    flow = Mathf.Max(flow, MinLevel);
                }
                flow = Mathf.Min(flow, a - (edge ? 0f : MinLevel));
                if (edge && a <= MinLevel * 1.01f) flow = a;     // a last drop falls off the edge whole
                if (flow <= 0.0005f) continue;
                n.c.lt[n.i] = t;
                n.c.liq[n.i] = na + flow;
                a -= flow;
                if (a <= 0.0001f) { Clear(me); return; }
                me.c.liq[me.i] = a;
            }
        }

        bool React(int x, int y, C lava, int ox, int oy)
        {
            var o = Cell(ox, oy);
            if (!o.ok) return false;
            Block ob = o.c.t[o.i];
            if (ob == Block.TNT) { w.Break(ox, oy, false); return false; }   // lava sets TNT off
            if (ob == Block.Ice)
            {
                // the ice flashes into steam, and it costs the lava some of itself
                w.Set(ox, oy, Block.Air);
                AddGas(Cell(ox, oy), 0.9f);
                Hiss(new Vector2(ox + 0.5f, oy + 0.5f));
                lava.c.liq[lava.i] -= 0.35f;
                if (lava.c.liq[lava.i] <= 0.02f) { Clear(lava); return true; }
                return false;
            }
            if (o.c.lt[o.i] != Water || o.c.liq[o.i] < 0.03f) return false;
            Clear(lava);
            w.Set(x, y, Block.Stone);
            o.c.liq[o.i] -= 0.4f;
            if (o.c.liq[o.i] <= 0.02f) { Clear(o); AddGas(o, 0.9f); }
            else AddGas(Cell(ox, oy + 1), 0.9f);
            AddGas(Cell(x, y + 1), 0.6f);
            Hiss(new Vector2(x + 0.5f, y + 0.5f));
            return true;
        }

        void Hiss(Vector2 at)
        {
            var g = Game.I;
            g.fx.Smoke(at, 4, 1.1f);
            g.fx.Burst(at, 4, new Color(1f, 0.6f, 0.2f), 3f, 0.3f, 10f);
            if (hissT <= 0f && (g.player.pos - at).sqrMagnitude < 400f)
            {
                hissT = 0.15f;
                g.sfx.Play("fuse", 0.25f, Random.Range(1.4f, 1.8f));
            }
        }

        void Gas(int x, int y)
        {
            var me = Cell(x, y);
            if (!me.ok || me.c.lt[me.i] != Steam) return;
            if (Blocks(me.c.t[me.i])) { Clear(me); return; }
            var up = Cell(x, y + 1);
            // steam slowly fades while it can still rise; trapped under a ceiling it just hangs there
            bool canRise = !Solid(up) && (up.c.lt[up.i] == None || (up.c.lt[up.i] == Steam && up.c.liq[up.i] < 0.98f));
            float a = me.c.liq[me.i] - (canRise ? 0.035f * Step : 0f);
            if (a < 0.02f) { Clear(me); return; }
            me.c.liq[me.i] = a;
            if (!Solid(up) && (up.c.lt[up.i] == None || up.c.lt[up.i] == Steam))
            {
                float ua = up.c.lt[up.i] == Steam ? up.c.liq[up.i] : 0f;
                float mv = Mathf.Min(a, (1f - ua) * 0.6f);
                if (mv > 0.001f)
                {
                    up.c.lt[up.i] = Steam; up.c.liq[up.i] = ua + mv;
                    a -= mv;
                    if (a < 0.02f) { Clear(me); return; }
                    me.c.liq[me.i] = a;
                }
            }
            for (int dx = -1; dx <= 1; dx += 2)
            {
                var n = Cell(x + dx, y);
                if (Solid(n) || (n.c.lt[n.i] != None && n.c.lt[n.i] != Steam)) continue;
                float na = n.c.lt[n.i] == Steam ? n.c.liq[n.i] : 0f;
                if (a <= na + 0.02f) continue;
                float flow = (a - na) * 0.25f;
                n.c.lt[n.i] = Steam; n.c.liq[n.i] = na + flow;
                a -= flow;
                me.c.liq[me.i] = a;
            }
        }

        static void Clear(C k)
        {
            if (!k.ok) return;
            k.c.lt[k.i] = None;
            k.c.liq[k.i] = 0f;
        }

        static void AddGas(C k, float amt)
        {
            if (!k.ok || Blocks(k.c.t[k.i])) return;
            byte t = k.c.lt[k.i];
            if (t == Water || t == Lava) return;
            k.c.lt[k.i] = Steam;
            k.c.liq[k.i] = Mathf.Min(1f, (t == Steam ? k.c.liq[k.i] : 0f) + amt);
        }

        // ------------------------------------------------------------------ rendering (one texture over the view)

        const int PXL = 8;
        readonly GameObject go;
        readonly SpriteRenderer sr;
        Texture2D tex;
        Color32[] buf;
        int tw, th;

        public void Render(Vector2 camPos, float halfW, float halfH, float time)
        {
            int cw = Mathf.CeilToInt(halfW * 2f + 4f), chh = Mathf.CeilToInt(halfH * 2f + 4f);
            if (tex == null || cw * PXL != tw || chh * PXL != th)
            {
                if (tex != null) { Object.Destroy(sr.sprite); Object.Destroy(tex); }
                tw = cw * PXL; th = chh * PXL;
                tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                buf = new Color32[tw * th];
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, tw, th), Vector2.zero, PXL, 0, SpriteMeshType.FullRect);
            }
            int ox = Mathf.FloorToInt(camPos.x - halfW - 2f), oy = Mathf.FloorToInt(camPos.y - halfH - 2f);
            go.transform.position = new Vector3(ox, oy, 0f);
            System.Array.Clear(buf, 0, buf.Length);
            bool any = false;
            int phase = (int)(time * 8f);
            bool zap = Electric.Any;
            int spark = Time.frameCount;   // sparks re-scatter every frame
            for (int cy = 0; cy < chh; cy++)
                for (int cx = 0; cx < cw; cx++)
                {
                    int x = ox + cx, y = oy + cy;
                    var k = Cell(x, y);
                    if (!k.ok) continue;
                    byte t = k.c.lt[k.i];
                    if (t == None) continue;
                    any = true;
                    float a = k.c.liq[k.i];
                    var up = Cell(x, y + 1);
                    bool fullAbove = up.ok && up.c.lt[up.i] == t && t != Steam;
                    // pouring down into open space: draw a stream, like a waterfall, instead of a floating slab
                    if (t != Steam)
                    {
                        var dn = Cell(x, y - 1);
                        bool falling = dn.ok && !Blocks(dn.c.t[dn.i]) && (dn.c.lt[dn.i] == None || (dn.c.lt[dn.i] == t && dn.c.liq[dn.i] < 0.9f));
                        if (falling)
                        {
                            DrawStream(cx, cy, x, y, t, a, fullAbove, time, phase);
                            if (zap && t == Water) Sparkle(cx, cy, x, y, PXL, spark);
                            continue;
                        }
                    }
                    int h = t == Steam ? PXL : fullAbove ? PXL : Mathf.Clamp(Mathf.RoundToInt(a * PXL), 1, PXL);
                    for (int py = 0; py < h; py++)
                        for (int px = 0; px < PXL; px++)
                        {
                            Color32 col;
                            bool surface = py == h - 1 && !fullAbove;
                            int gx = x * PXL + px, gy = y * PXL + py;
                            if (t == Water)
                            {
                                float wave = Mathf.Sin(gx * 0.35f + time * 3f) * 0.5f + 0.5f;
                                col = surface ? new Color32(170, 246, 236, 220) : new Color32((byte)(30 + wave * 14), (byte)(140 + wave * 22), (byte)(176 + wave * 14), 150);   // teal, like the moodboard
                            }
                            else if (t == Lava)
                            {
                                float n = U.H01((gx >> 1) + phase, (gy >> 1) - phase / 2, 77);
                                col = surface ? new Color32(255, 235, 120, 255) : new Color32(255, (byte)(90 + n * 90), (byte)(15 + n * 30), 245);
                            }
                            else
                            {
                                float n = U.H01((gx >> 2) + phase / 2, (gy >> 2) - phase, 78);
                                byte al = (byte)Mathf.Clamp(a * 150f * (0.6f + 0.6f * n), 0f, 170f);
                                col = new Color32(225, 230, 240, al);
                            }
                            buf[(cy * PXL + py) * tw + cx * PXL + px] = col;
                        }
                    if (zap && t == Water) Sparkle(cx, cy, x, y, h, spark);
                }
            if (zap && any) DrawBolts(ox, oy);
            sr.enabled = any;
            if (!any) return;
            tex.SetPixels32(buf);
            tex.Apply(false);
        }

        // ELECTRICITY: white sparks speckled through charged water; the stronger the charge, the more of it they fill
        void Sparkle(int cx, int cy, int x, int y, int h, int seed)
        {
            float el = Electric.Level(x, y);
            if (el <= 0.01f) return;
            for (int py = 0; py < h; py++)
                for (int px = 0; px < PXL; px++)
                {
                    float n = U.H01(x * PXL + px + seed * 7, y * PXL + py - seed * 3, 501);
                    if (n >= el) continue;
                    buf[(cy * PXL + py) * tw + cx * PXL + px] = n < el * 0.4f ? new Color32(255, 255, 255, 255) : new Color32(200, 236, 255, 230);
                }
        }

        // the forks of lightning, one pixel wide, drawn into the water layer
        void DrawBolts(int ox, int oy)
        {
            var white = new Color32(255, 255, 255, 255);
            foreach (var bolt in Electric.Bolts)
                for (int i = 0; i + 1 < bolt.Count; i++)
                {
                    Vector2 a = (bolt[i] - new Vector2(ox, oy)) * PXL, b = (bolt[i + 1] - new Vector2(ox, oy)) * PXL;
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y))));
                    for (int k = 0; k <= steps; k++)
                    {
                        Vector2 q = Vector2.Lerp(a, b, k / (float)steps);
                        int qx = Mathf.FloorToInt(q.x), qy = Mathf.FloorToInt(q.y);
                        if (qx < 0 || qy < 0 || qx >= tw || qy >= th) continue;
                        buf[qy * tw + qx] = white;
                    }
                }
        }

        // A falling column of liquid: full cell height, as wide as there is liquid, with streaks running down.
        void DrawStream(int cx, int cy, int x, int y, byte t, float a, bool fromAbove, float time, int phase)
        {
            float amt = fromAbove ? Mathf.Max(a, 0.45f) : a;
            int w = Mathf.Clamp(Mathf.RoundToInt(amt * PXL * 1.5f), 2, PXL);
            // wobble the stream a pixel side to side so it looks alive
            int x0 = (PXL - w) / 2 + (w < PXL && U.H01(x, y + phase / 3, 41) > 0.7f ? 1 : 0);
            x0 = Mathf.Clamp(x0, 0, PXL - w);
            int scroll = (int)(time * (t == Lava ? 14f : 30f));
            for (int py = 0; py < PXL; py++)
                for (int px = x0; px < x0 + w; px++)
                {
                    int gx = x * PXL + px, gy = y * PXL + py;
                    bool edge = px == x0 || px == x0 + w - 1;
                    bool streak = ((gy + scroll) / 3 + gx * 2) % 5 == 0;
                    Color32 col;
                    if (t == Water)
                        col = streak ? new Color32(200, 250, 240, 225) : edge ? new Color32(110, 210, 214, 195) : new Color32(46, 152, 182, 175);
                    else
                    {
                        float n = U.H01((gx >> 1) + phase, ((gy + scroll) >> 1), 77);
                        col = streak ? new Color32(255, 240, 150, 255) : edge ? new Color32(220, 70, 20, 250) : new Color32(255, (byte)(110 + n * 80), (byte)(20 + n * 25), 250);
                    }
                    buf[(cy * PXL + py) * tw + cx * PXL + px] = col;
                }
        }

        // lava glows
        public void AddLights(Lighting l, Vector2 camPos, float halfW, float halfH, float time)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(camPos.x - halfW - 3)), x1 = Mathf.Min(World.W - 1, Mathf.CeilToInt(camPos.x + halfW + 3));
            int y0 = Mathf.FloorToInt(camPos.y - halfH - 3), y1 = Mathf.CeilToInt(camPos.y + halfH + 3);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var k = Cell(x, y);
                    if (!k.ok || k.c.lt[k.i] != Lava) continue;
                    float a = k.c.liq[k.i];
                    // only light from the top of a pool, plus a dim glow everywhere, to keep it cheap
                    var up = Cell(x, y + 1);
                    bool top = !up.ok || up.c.lt[up.i] != Lava;
                    float flick = 1f + Mathf.Sin(time * 5f + x * 1.3f + y * 0.7f) * 0.06f;
                    if (top) l.Add(new Vector2(x + 0.5f, y + a), (1.6f + a * 1.8f) * flick, 0.85f);
                    else if (((x + y) & 1) == 0) l.Add(new Vector2(x + 0.5f, y + 0.5f), 1.6f, 0.5f);
                }
        }
    }
}
