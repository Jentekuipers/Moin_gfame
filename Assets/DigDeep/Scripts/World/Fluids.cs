using UnityEngine;

namespace DigDeep
{
    // Liquids and gas, kept apart from the block grid.
    //
    // WATER and LAVA are grains, Noita-style: every block holds 8x8 of them (each grain is 2x2 pixels of a
    // 16-pixel block). A grain falls, slides off diagonally, and runs sideways towards any drop nearby (or when
    // water above squeezes it). A flat surface stops moving, so a still pool is perfectly still. Lava is thick:
    // it moves a third as often and barely spreads.
    // Each block still knows its fluid type and how full it is (0..1), counted from its grains, so everything
    // else (swimming, fish, freezing, the arena floods...) reads and writes water per block as it always did.
    //
    // STEAM stays per block: it rises, spreads under ceilings and slowly fades.
    // Water meeting lava turns the lava into rock and boils off as steam. Cold biomes freeze water solid;
    // hot biomes boil exposed water away.
    public class Fluids
    {
        public const byte None = 0, Water = 1, Lava = 2, Steam = 3, Haste = 4;   // HASTE: a white mist-liquid; everything in it runs twice as fast
        // POISON: green, poisons whatever soaks in it while it's strong (it thins out in water); ACID: eats whatever it
        // touches (and itself as it does); FUMES: the poisonous green gas they give off (a gas, like steam: per block)
        public const byte Poison = 5, Acid = 6, Fumes = 7;
        public const float MinLevel = 0.125f;
        const float Step = 1f / 50f;
        public const int SUB = 8, SN = SUB * SUB;
        const byte TypeMask = 0x07, Moved = 0x80, Fell = 0x40;   // Fell: it was falling, and spreads out where it lands
        // MOMENTUM: a grain that was moving sideways keeps going (0x08: to the right; 0x30: how strongly, 0..3), and
        // shoves the water in its way along with it, so a surge rushes out across a pool instead of just seeping
        const byte MomRight = 0x08, MomMask = 0x30;
        public static bool IsLiq(byte t) => t == Water || t == Lava || t == Haste || t == Poison || t == Acid;
        public static bool IsGas(byte t) => t == Steam || t == Fumes;
        static bool Solute(byte t) => t == Haste || t == Poison || t == Acid;   // they mix into water
        static byte Mom(int dir, int m) => m <= 0 ? (byte)0 : (byte)((Mathf.Min(m, 3) << 4) | (dir > 0 ? MomRight : 0));

        readonly World w;
        float acc, hissT;
        int frame;
        byte parity;

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
            while (acc >= Step && n < 4) { acc -= Step; Simulate(camY, halfH); n++; }
            if (acc > Step) acc = 0f;
        }

        // reference to one block's storage
        struct C { public Chunk c; public int i; public bool ok; }

        Chunk lastCh;
        int lastCi = int.MinValue;
        bool TryCh(int ci, out Chunk ch)
        {
            if (ci == lastCi && lastCh != null) { ch = lastCh; return true; }
            if (w.TryChunk(ci, out ch)) { lastCi = ci; lastCh = ch; return true; }
            return false;
        }

        C Cell(int x, int y)
        {
            var r = new C();
            if (x < 0 || x >= World.W) return r;
            if (!TryCh(U.FloorDiv(y, World.CH), out var ch)) return r;
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

        // ------------------------------------------------------------------ the grains of a block

        // a chunk's grains, built from its per-block amounts the first time they're needed
        public static void Ensure(Chunk c)
        {
            if (c.sub != null) return;
            c.mixP = new float[World.W * World.CH]; c.mixH = new float[World.W * World.CH];
            c.sub = new byte[World.W * World.CH * SN];
            c.cnt = new byte[World.W * World.CH];
            for (int i = 0; i < c.lt.Length; i++)
            {
                byte t = c.lt[i];
                if (IsLiq(t) && c.liq[i] > 0f) { Fill(c, i, t, c.liq[i]); Recount(c, i); }
            }
        }

        // a block filled to amount a, from the bottom up
        static void Fill(Chunk c, int i, byte t, float a)
        {
            int n = a <= 0f ? 0 : Mathf.Clamp(Mathf.RoundToInt(a * SN), 1, SN);
            int b = i * SN;
            for (int k = 0; k < SN; k++) c.sub[b + k] = k < n ? t : (byte)0;
        }

        // the block's type and amount, counted from its grains
        static void Recount(Chunk c, int i)
        {
            int b = i * SN, n = 0;
            int c1 = 0, c2 = 0, c4 = 0, c5 = 0, c6 = 0;
            for (int k = 0; k < SN; k++)
            {
                switch (c.sub[b + k] & TypeMask)
                {
                    case Water: c1++; break;
                    case Lava: c2++; break;
                    case Haste: c4++; break;
                    case Poison: c5++; break;
                    case Acid: c6++; break;
                }
            }
            n = c1 + c2 + c4 + c5 + c6;
            c.cnt[i] = (byte)n;
            if (n > 0)
            {
                // the block reads as whatever most of it is (water wins a tie)
                byte best = Water; int bn = c1;
                if (c2 > bn) { best = Lava; bn = c2; }
                if (c4 > bn) { best = Haste; bn = c4; }
                if (c5 > bn) { best = Poison; bn = c5; }
                if (c6 > bn) { best = Acid; bn = c6; }
                c.lt[i] = best; c.liq[i] = n / (float)SN;
            }
            else if (IsLiq(c.lt[i])) { c.lt[i] = None; c.liq[i] = 0f; }
        }

        // one grain more or less in a block
        static void Count(Chunk c, int i, byte t, int d)
        {
            int n = c.cnt[i] + d;
            if (n < 0) n = 0;
            c.cnt[i] = (byte)n;
            if (n > 0) { if (d > 0 || !IsLiq(c.lt[i])) c.lt[i] = t; c.liq[i] = n / (float)SN; }
            else { c.lt[i] = None; c.liq[i] = 0f; }
        }

        // (World.SetLiquid) set a block's fluid: its grains are laid in from the bottom
        public static void SetBlock(Chunk c, int i, byte t, float a)
        {
            if (c.sub != null)
            {
                if (IsLiq(t) && a > 0f) { Fill(c, i, t, a); Recount(c, i); return; }
                ClearGrains(c, i);
            }
            c.lt[i] = a > 0f ? t : None;
            c.liq[i] = a > 0f ? a : 0f;
        }

        public static void ClearGrains(Chunk c, int i)
        {
            if (c.sub == null) return;
            int b = i * SN;
            for (int k = 0; k < SN; k++) c.sub[b + k] = 0;
            c.cnt[i] = 0;
        }

        // the grain at sub-position (sx, sy) of a block, or None
        public static byte GrainAt(Chunk c, int i, int sx, int sy)
        {
            if (c.sub == null) return None;
            return (byte)(c.sub[i * SN + Mathf.Clamp(sy, 0, SUB - 1) * SUB + Mathf.Clamp(sx, 0, SUB - 1)] & TypeMask);
        }

        // take n grains of type t off the top of a block
        static int RemoveTop(Chunk c, int i, byte t, int n)
        {
            if (c.sub == null || n <= 0) return 0;
            int b = i * SN, got = 0;
            for (int k = SN - 1; k >= 0 && got < n; k--)
                if ((c.sub[b + k] & TypeMask) == t) { c.sub[b + k] = 0; got++; }
            if (got > 0) Recount(c, i);
            return got;
        }

        // grain coordinates (8 per block) -> its block and slot; false off the map or in an unloaded chunk
        bool Res(int gx, int gy, out Chunk c, out int ti, out int si)
        {
            c = null; ti = si = 0;
            int x = gx >> 3, y = gy >> 3;
            if (x < 0 || x >= World.W) return false;
            if (!TryCh(U.FloorDiv(y, World.CH), out c)) return false;
            Ensure(c);
            ti = (y - c.Y0) * World.W + x;
            si = ti * SN + (gy & 7) * SUB + (gx & 7);
            return true;
        }

        // BOULDERS are round: only the grains inside the circle are rock, so water fills the corners of their block
        static readonly bool[] RoundIn = MakeRound();
        static bool[] MakeRound()
        {
            var m = new bool[SN];
            for (int k = 0; k < SN; k++) { float dx = (k & 7) - 3.5f, dy = (k >> 3) - 3.5f; m[k] = dx * dx + dy * dy <= 3.7f * 3.7f; }
            return m;
        }
        // is this grain-spot rock?
        static bool Rock(Chunk c, int ti, int si) => Blocks(c.t[ti]) && (c.t[ti] != Block.Boulder || RoundIn[si & (SN - 1)]);
        bool RockAt(int gx, int gy) => Res(gx, gy, out var c, out var ti, out var si) && Rock(c, ti, si);

        // room for a grain here (open space, not a block, no grain already)
        bool Empty(int gx, int gy)
        {
            if (!Res(gx, gy, out var c, out var ti, out var si)) return false;
            return !Rock(c, ti, si) && c.sub[si] == 0;
        }

        bool Liquid(int gx, int gy)
        {
            if (!Res(gx, gy, out var c, out _, out var si)) return false;
            return (c.sub[si] & TypeMask) != 0;
        }

        void Simulate(float camY, float halfH)
        {
            frame++;
            parity = (frame & 1) != 0 ? Moved : (byte)0;
            lastCi = int.MinValue;
            if (pockets.Count > 0) TickPockets(Step);
            for (int i = rings.Count - 1; i >= 0; i--) { var g = rings[i]; g.t += Step; if (g.t > RingLife) rings.RemoveAt(i); else rings[i] = g; }
            if (drops.Count > 0) TickDrops(Step);
            int yTop = Mathf.Min(0, Mathf.FloorToInt(camY + halfH + 14));
            int yBot = Mathf.FloorToInt(camY - halfH - 16);
            bool lavaStep = (frame & 1) == 0;   // lava moves every other step
            bool ltr = (frame & 1) == 0;
            int W = World.W;
            // the blocks' own business first: freezing, boiling, lava meeting water
            for (int y = yBot; y <= yTop; y++)
            {
                if (!TryCh(U.FloorDiv(y, World.CH), out var ch)) continue;
                Ensure(ch);
                int ly = y - ch.Y0;
                for (int x = 0; x < W; x++)
                {
                    int i = ly * W + x;
                    if (ch.cnt[i] > 0) Interact(ch, i, x, y, lavaStep);
                }
            }
            // then the grains, from the bottom up so falls settle in one pass
            for (int y = yBot; y <= yTop; y++)
            {
                if (!TryCh(U.FloorDiv(y, World.CH), out var ch)) continue;
                int ly = y - ch.Y0;
                for (int sy = 0; sy < SUB; sy++)
                    for (int xx = 0; xx < W; xx++)
                    {
                        int x = ltr ? xx : W - 1 - xx;
                        int i = ly * W + x;
                        if (ch.cnt[i] == 0) continue;
                        // the inside of a pool (full, walled in by full water or rock below and to the sides) can't move
                        if (ch.cnt[i] == SN && Packed(x, y - 1) && Packed(x - 1, y) && Packed(x + 1, y)) continue;
                        for (int ss = 0; ss < SUB; ss++)
                        {
                            int sx = ltr ? ss : SUB - 1 - ss;
                            Grain(ch, i, sy * SUB + sx, x * SUB + sx, y * SUB + sy, lavaStep);
                        }
                    }
            }
            TickRockPush();
            // steam from the top down
            for (int y = yTop; y >= yBot; y--)
                for (int k = 0; k < W; k++)
                    Gas(ltr ? k : W - 1 - k, y);
        }

        bool Packed(int x, int y)
        {
            var k = Cell(x, y);
            if (!k.ok || Blocks(k.c.t[k.i])) return true;
            return k.c.cnt != null && k.c.cnt[k.i] == SN;
        }

        void Grain(Chunk ch, int i, int k, int gx, int gy, bool lavaStep)
        {
            int si = i * SN + k;
            byte v = ch.sub[si];
            if (v == 0 || (v & Moved) == parity) return;
            byte t = (byte)(v & TypeMask);
            if (!IsLiq(t)) { ch.sub[si] = 0; return; }
            if (Rock(ch, i, si)) { ch.sub[si] = 0; Count(ch, i, t, -1); return; }
            if (t == Lava && !lavaStep) { ch.sub[si] = (byte)((v & ~Moved) | parity); return; }
            // lava is far heavier than water: it sinks down through it (they swap places)
            if (t == Lava && Res(gx, gy - 1, out var lc, out var lti, out var lsi) && !Rock(lc, lti, lsi) && (lc.sub[lsi] & TypeMask) != 0 && (lc.sub[lsi] & TypeMask) != Lava)
            {
                byte wv = lc.sub[lsi];
                lc.sub[lsi] = (byte)(Lava | parity);
                ch.sub[si] = (byte)((wv & TypeMask) | parity);
                if (lc != ch || lti != i) { Recount(ch, i); Recount(lc, lti); }
                return;
            }
            int md = (v & MomRight) != 0 ? 1 : -1, mm = (v & MomMask) >> 4;   // the sideways push it carries
            // DISSOLVING: haste-mist and poison mixed with water spread through it and thin out (turning into plain water
            // bit by bit), so a little of either in a pool soon does nothing; acid doesn't thin, it eats the water (Chem)
            // (slowly when it all lies still, much faster where the liquids are moving)
            if (Solute(t) && Random.value < (mm > 0 || (v & Fell) != 0 ? 0.14f : 0.03f))
            {
                int dir = Random.Range(0, 4);
                int ox = dir == 0 ? 1 : dir == 1 ? -1 : 0, oy = dir == 2 ? 1 : dir == 3 ? -1 : 0;
                if (Res(gx + ox, gy + oy, out var dc0, out var dti0, out var dsi0) && !Rock(dc0, dti0, dsi0) && (dc0.sub[dsi0] & TypeMask) == Water)
                {
                    if (t != Acid && Random.value < 0.12f) { ch.sub[si] = (byte)(Water | parity); AddMix(ch, i, t); Recount(ch, i); return; }   // it's thinned away (and tints the water)
                    byte wv0 = dc0.sub[dsi0];
                    dc0.sub[dsi0] = (byte)((v & ~Moved) | parity);
                    ch.sub[si] = (byte)((wv0 & ~Moved) | parity);
                    if (dc0 != ch || dti0 != i) { Recount(ch, i); Recount(dc0, dti0); }
                    return;
                }
            }
            // water is heavier than the haste-mist: it sinks through it (they swap places)
            if (t == Water && Res(gx, gy - 1, out var bc, out var bti, out var bsi) && (bc.sub[bsi] & TypeMask) == Haste && !Rock(bc, bti, bsi))
            {
                byte hv = bc.sub[bsi];
                bc.sub[bsi] = (byte)(Water | parity | Fell);
                ch.sub[si] = (byte)((hv & ~Moved & ~Fell) | parity);
                if (bc != ch || bti != i) { Recount(ch, i); Recount(bc, bti); }
                return;
            }
            int cx = gx, cy = gy;
            int nm = 0, nd = md;   // the push it leaves with
            // fall (water a few grains a step, so it pours)
            bool fell = (v & Fell) != 0;
            int falls = t == Lava ? 2 : fell ? 8 : 4;   // a falling grain picks up speed
            for (int f = 0; f < falls && Empty(cx, cy - 1); f++) cy--;
            if (cy == gy)
            {
                // slide off diagonally (the way it's already heading, if it's moving)
                int d0 = mm > 0 ? md : ((gx * 7 + gy * 3 + frame) & 1) == 0 ? 1 : -1;
                for (int s = 0; s < 2 && cy == gy; s++)
                {
                    int d = s == 0 ? d0 : -d0;
                    if (Empty(cx + d, cy) && Empty(cx + d, cy - 1)) { cx += d; cy--; if (t != Lava) { nm = 2; nd = d; } }
                }
            }
            if (cy == gy && cx == gx && mm > 0)
            {
                // MOMENTUM: still moving sideways from before: it carries on, and pushes the water in its way
                int moved = 0;
                for (int n = 0; n < mm && Empty(cx + md, cy); n++) { cx += md; moved++; }
                if (moved < mm && t != Lava && RockAt(cx + md, cy))
                {
                    // it hits rock: its push goes into it (enough water shoves a loose boulder along), and the water rides
                    // up the round face of a boulder, climbs a wall a little, or bursts off it as spray
                    bool round = HitRock(cx + md, cy, md, mm);
                    if (mm >= 2 && Random.value < (round ? 0.12f : 0.04f) * mm / 3f)
                    {
                        ch.sub[si] = 0; Count(ch, i, t, -1);
                        AddDrop(new Vector2((cx + 0.5f) / SUB, (cy + 1.5f) / SUB), new Vector2(-md * Random.Range(0.3f, 2.2f), Random.Range(3.5f, 7.5f) * mm / 3f), t);
                        return;
                    }
                    if (Empty(cx + md, cy + 1)) { cx += md; cy++; nm = mm - 1; nd = md; }
                    else if (mm >= 2 && Empty(cx, cy + 1)) { cy++; nm = mm - 1; nd = md; }
                    else if (moved > 0) { nm = 0; }
                }
                else if (moved > 0) { nm = (frame & 3) == 0 ? mm - 1 : mm; nd = md; }
                else Shove(cx + md, cy, md, mm - 1);
            }
            if (cy == gy && cx == gx)
            {
                // run sideways, but only towards a drop within reach (or squeezed by water above):
                // a flat surface stays put, so still water is still
                int reach = t == Lava ? 16 : 48;   // it can feel a drop a few blocks off (so it finds its way along ledges and through gaps)
                bool squeezed = Liquid(cx, cy + 1);
                int d0 = ((gx * 5 + gy + frame) & 2) == 0 ? 1 : -1;
                int bestD = 0, bestK = 0, firstD = 0;
                for (int s = 0; s < 2 && bestD == 0; s++)
                {
                    int d = s == 0 ? d0 : -d0;
                    for (int n = 1; n <= reach; n++)
                    {
                        if (!Empty(cx + d * n, cy)) break;
                        if (firstD == 0) firstD = d;
                        if (Empty(cx + d * n, cy - 1)) { bestD = d; bestK = n; break; }
                    }
                }
                if (bestD != 0)
                {
                    cx += bestD * Mathf.Min(bestK, t == Lava ? 2 : 4);   // ...and heads that way, a few grains a step
                    if (t != Lava) { nm = 3; nd = bestD; }
                }
                else if (firstD != 0 && (squeezed || fell || (t != Lava && RockAt(cx, cy + 1) && Liquid(cx, cy - 1) && Empty(cx + firstD * 2, cy))))
                {
                    // (water pressed up against a ceiling is under pressure too: it spreads into any air gap along it, so
                    // a flooding tunnel fills right up instead of leaving a layer of air under the roof)
                    // landing from a fall it keeps going: it spreads out to the side instead of piling up; squeezed out
                    // from under deep water it shoots out (the deeper, the harder)
                    int run = 1, max = 1;
                    if (fell) max = 5;
                    else if (t != Lava)
                    {
                        int head = 1;
                        while (head < 16 && Liquid(cx, cy + 1 + head)) head++;
                        max = Mathf.Min(1 + head / 4, 4);
                    }
                    while (run < max && Empty(cx + firstD * (run + 1), cy)) run++;
                    cx += firstD * run;
                    if (t != Lava) { nm = run >= 3 ? 3 : 2; nd = firstD; }
                }
            }
            else if (cy < gy && mm > 0 && t != Lava)
            {
                // falling while pushed sideways: it drifts with the push (a surge shoves a falling stream aside)
                if (Empty(cx + md, cy)) { cx += md; nm = mm - ((frame & 1) == 0 ? 1 : 0); nd = md; }
                else { Shove(cx + md, cy, md, mm - 1); nm = 0; }
            }
            // PRESSURE: a grain deep under water, boxed in, is pushed up into any gap beside and above it (water fills
            // the space under a ledge, and rises again on the far side of a bend)
            if (cx == gx && cy == gy && t != Lava && Liquid(cx, cy + 1))
            {
                int head = 1;
                while (head < 12 && Liquid(cx, cy + 1 + head)) head++;
                if (head >= 8)
                {
                    int d0 = ((gx * 3 + gy + frame) & 1) == 0 ? 1 : -1;
                    for (int s2 = 0; s2 < 2; s2++)
                    {
                        int d = s2 == 0 ? d0 : -d0;
                        if (Empty(cx + d, cy + 1)) { cx += d; cy += 1; nm = 2; nd = d; break; }
                    }
                }
            }
            byte keep = (byte)(t | parity | (cy < gy ? Fell : 0) | Mom(nd, nm));
            if (cx == gx && cy == gy) { ch.sub[si] = (byte)(t | parity); return; }
            if (!Res(cx, cy, out var dc, out var dti, out var dsi)) { ch.sub[si] = (byte)(t | parity); return; }
            ch.sub[si] = 0;
            dc.sub[dsi] = keep;
            if (dc != ch || dti != i) { Count(ch, i, t, -1); Count(dc, dti, t, 1); }
        }

        // water slamming into a boulder: its push adds up, and enough of it knocks the boulder loose
        readonly System.Collections.Generic.Dictionary<long, float> rockPush = new System.Collections.Generic.Dictionary<long, float>();
        bool HitRock(int gx, int gy, int dir, int m)
        {
            if (!Res(gx, gy, out var c, out var ti, out _) || c.t[ti] != Block.Boulder) return false;
            int x = gx >> 3, y = gy >> 3;
            long key = ((long)y << 32) ^ (uint)x;
            rockPush.TryGetValue(key, out var f);
            rockPush[key] = f + dir * m;
            return true;
        }

        void TickRockPush()
        {
            if (rockPush.Count == 0) return;
            var keys = new System.Collections.Generic.List<long>(rockPush.Keys);
            foreach (var key in keys)
            {
                float f = rockPush[key] * 0.95f;   // the push fades fast: only a real surge of water keeps it up
                int x = (int)(uint)(key & 0xFFFFFFFFL), y = (int)(key >> 32);
                if (w.Get(x, y) != Block.Boulder || Mathf.Abs(f) < 1f) { rockPush.Remove(key); continue; }
                if (Mathf.Abs(f) > 140f)
                {
                    // a big wave rolls it: the more water, the harder
                    float sp = Mathf.Clamp(2.5f + (Mathf.Abs(f) - 140f) / 50f, 2.5f, 8f);
                    rockPush.Remove(key);
                    w.PushBoulder(x, y, new Vector2(Mathf.Sign(f) * sp, 1.2f));
                    Game.I.fx.Burst(new Vector2(x + 0.5f, y + 0.8f), 6, new Color(0.85f, 0.97f, 1f, 0.85f), 3f, 0.5f, 10f, 1f, false);
                    continue;
                }
                rockPush[key] = f;
            }
        }

        // a moving grain bumps into another: it hands on its push (a wave travels through the water)
        void Shove(int gx, int gy, int dir, int m)
        {
            if (m <= 0 || !Res(gx, gy, out var c, out _, out var si)) return;
            byte v = c.sub[si];
            if ((v & TypeMask) == 0 || (v & TypeMask) == Lava) return;
            int have = (v & MomMask) >> 4;
            if (have >= m && ((v & MomRight) != 0) == dir > 0) return;
            c.sub[si] = (byte)((v & ~(MomMask | MomRight)) | Mom(dir, m));
        }

        // freezing, boiling and lava meeting water: per block, as before
        void Interact(Chunk ch, int i, int x, int y, bool lavaStep)
        {
            var me = new C { c = ch, i = i, ok = true };
            if (ch.t[i] == Block.Boulder) return;   // (water round a boulder: just the corners of its block)
            if (Blocks(ch.t[i])) { Clear(me); return; }
            MixTile(ch, i, x, y);
            if (Chem(ch, i, x, y)) return;
            byte t = ch.lt[i];
            float a = ch.liq[i];
            int bi = Biomes.Index(-y);
            if (t == Water)
            {
                // a few lava grains sinking through the water: they glow on a moment, boiling the water round them, then
                // they're gone (or, a big enough lump of it, set to rock)
                int lb = i * SN, ln = 0;
                for (int k = 0; k < SN; k++) if ((ch.sub[lb + k] & TypeMask) == Lava) ln++;
                if (ln > 0)
                {
                    var at = new Vector2(x + Random.value, y + Random.value);
                    if (Random.value < 0.3f && RemoveTop(ch, i, Water, 1) > 0 && Random.value < 0.5f) Bubble(at, Random.Range(2.5f, 4.5f));
                    if (Random.value < 0.03f) Hiss(at);
                    if (Random.value < Mathf.Min(0.05f, 0.015f + (SN - ln) * 0.0005f))
                    {
                        int clump = ln;
                        for (int d = 0; d < 4; d++)
                        {
                            var n = Cell(x + (d == 0 ? 1 : d == 1 ? -1 : 0), y + (d == 2 ? 1 : d == 3 ? -1 : 0));
                            if (n.ok && n.c.lt[n.i] == Lava && n.c.cnt != null) clump += n.c.cnt[n.i];
                        }
                        for (int k = 0; k < SN; k++) if ((ch.sub[lb + k] & TypeMask) == Lava) ch.sub[lb + k] = 0;
                        RemoveTop(ch, i, Water, ln / 2);
                        Recount(ch, i);
                        Hiss(at);
                        if (clump >= SN * 3 / 4) { Clear(me); w.Set(x, y, Block.Stone); AddGas(Cell(x, y + 1), 0.5f); return; }
                        for (int k = 0; k < 2 + ln / 6; k++) Bubble(new Vector2(x + Random.value, y + Random.value), Random.Range(3f, 5f));
                        if (ch.cnt[i] == 0) return;
                    }
                }
                // cold biome: water freezes solid, but only after a while: it skins over from the surface and from
                // ice it touches (a few seconds), and still pools freeze through on their own given time
                if (bi == Biomes.Crystal)
                {
                    bool nearIce = w.Get(x + 1, y) == Block.Ice || w.Get(x - 1, y) == Block.Ice || w.Get(x, y + 1) == Block.Ice || w.Get(x, y - 1) == Block.Ice;
                    var abv = Cell(x, y + 1);
                    bool surface = !Solid(abv) && abv.c.lt[abv.i] != Water;
                    float chance = nearIce ? 0.006f : surface ? 0.0025f : 0.0008f;
                    if (a > 0.6f && Random.value < chance * Step * 30f) { Clear(me); w.Set(x, y, Block.Ice); return; }
                }
                // hot biome: exposed water boils away
                if (bi == Biomes.Magma)
                {
                    var up = Cell(x, y + 1);
                    if (!Solid(up) && up.c.lt[up.i] != Water && Random.value < 0.25f * Step * 30f)
                    {
                        RemoveTop(ch, i, Water, 1);
                        AddGas(up, 0.03f);
                    }
                }
            }
            else if (t == Lava)
            {
                // the void and the boss halls won't have lava: it boils away into steam on contact
                if (lavaStep && (VoidTouch(x, y) || ArenaRoom.Inside(x, -y))) { Clear(me); AddGas(me, Mathf.Max(0.3f, a)); return; }
                // sand touching lava melts into glass
                if (Random.value < 0.02f) Glassify(x, y);
                // lava touching ice or TNT
                if (React(x, y, me, x + 1, y) || React(x, y, me, x - 1, y) || React(x, y, me, x, y + 1) || React(x, y, me, x, y - 1)) return;
                // lava actually touching water (grain against grain): it lives on in the water for a moment, hissing, boiling
                // the water off in bubbles and steam, then sets into rock (a real clump) or is gone (a little)
                int touch = WaterTouch(ch, i, x, y, out var wc, out var wAt);
                if (touch > 0)
                {
                    if (Random.value < 0.3f && RemoveTop(wc.c, wc.i, Water, 1) > 0 && Random.value < 0.5f) Bubble(wAt + new Vector2(Random.Range(-0.3f, 0.3f), 0.1f), Random.Range(2.5f, 4.5f));
                    if (Random.value < 0.08f) { Game.I.fx.Smoke(wAt, 1, 0.6f); AddGas(Cell(x, y + 1), 0.05f); }
                    if (Random.value < 0.03f) Hiss(wAt);
                    if (Random.value < Mathf.Min(0.05f, 0.015f + touch * 0.001f)) { Quench(x, y, me, wc, new Vector2(Mathf.Floor(wAt.x), Mathf.Floor(wAt.y))); return; }
                }
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
                RemoveTop(lava.c, lava.i, Lava, 22);
                return lava.c.cnt[lava.i] == 0;
            }
            return false;
        }

        // how many water grains sit right against this block's lava grains (in it, or across its edges), and where
        int WaterTouch(Chunk ch, int i, int x, int y, out C water, out Vector2 at)
        {
            water = default; at = Vector2.zero;
            int n = 0, b = i * SN;
            for (int k = 0; k < SN; k++)
                if ((ch.sub[b + k] & TypeMask) == Water) { n++; if (!water.ok) { water = new C { c = ch, i = i, ok = true }; at = new Vector2(x + ((k & 7) + 0.5f) / SUB, y + ((k >> 3) + 0.5f) / SUB); } }
            for (int d = 0; d < 4; d++)
            {
                int dx = d == 0 ? 1 : d == 1 ? -1 : 0, dy = d == 2 ? 1 : d == 3 ? -1 : 0;
                var o = Cell(x + dx, y + dy);
                if (!o.ok || Blocks(o.c.t[o.i]) || o.c.lt[o.i] != Water || o.c.sub == null) continue;
                for (int e = 0; e < SUB; e++)
                {
                    // my edge grain and the one across from it
                    int mx = dx > 0 ? SUB - 1 : dx < 0 ? 0 : e, my = dy > 0 ? SUB - 1 : dy < 0 ? 0 : e;
                    int ox = dx > 0 ? 0 : dx < 0 ? SUB - 1 : e, oy = dy > 0 ? 0 : dy < 0 ? SUB - 1 : e;
                    if ((ch.sub[b + my * SUB + mx] & TypeMask) != Lava || (o.c.sub[o.i * SN + oy * SUB + ox] & TypeMask) != Water) continue;
                    n++;
                    if (!water.ok) { water = o; at = new Vector2(x + dx + (ox + 0.5f) / SUB, y + dy + (oy + 0.5f) / SUB); }
                }
            }
            return n;
        }

        // Water meets lava. A real clump of lava (most of a block's worth, counting what touches it) sets into
        // rock; a little lava just boils some water away and is gone. Either way some water turns to steam.
        void Quench(int x, int y, C lava, C water, Vector2 wAt)
        {
            int clump = lava.c.cnt[lava.i];
            for (int d = 0; d < 4; d++)
            {
                var n = Cell(x + (d == 0 ? 1 : d == 1 ? -1 : 0), y + (d == 2 ? 1 : d == 3 ? -1 : 0));
                if (n.ok && n.c.lt[n.i] == Lava && n.c.cnt != null) clump += n.c.cnt[n.i];
            }
            bool stone = clump >= SN * 3 / 4;
            Clear(lava);
            if (stone) w.Set(x, y, Block.Stone);
            if (water.ok && water.c.cnt != null)
            {
                RemoveTop(water.c, water.i, Water, stone ? 20 : 10);
                if (water.c.cnt[water.i] == 0) { Clear(water); AddGas(water, 0.7f); }
                else AddGas(Cell((int)wAt.x, (int)wAt.y + 1), stone ? 0.8f : 0.5f);
            }
            AddGas(Cell(x, y + 1), stone ? 0.6f : 0.4f);
            if (!stone) AddGas(Cell(x, y), 0.6f);
            Hiss(new Vector2(x + 0.5f, y + 0.5f));
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
            if (!me.ok || !IsGas(me.c.lt[me.i])) return;
            if (Blocks(me.c.t[me.i])) { Clear(me); return; }
            byte gt = me.c.lt[me.i];
            var up = Cell(x, y + 1);
            // steam slowly fades while it can still rise; trapped under a ceiling it just hangs there (fumes are heavier
            // and linger longer, and the void swallows them)
            if (gt == Fumes && VoidTouch(x, y)) { Clear(me); return; }
            bool canRise = !Solid(up) && (up.c.lt[up.i] == None || (up.c.lt[up.i] == gt && up.c.liq[up.i] < 0.98f));
            float a = me.c.liq[me.i] - (canRise || gt == Fumes ? (gt == Fumes ? 0.02f : 0.035f) * Step : 0f);
            if (a < 0.02f) { Clear(me); return; }
            me.c.liq[me.i] = a;
            if (!Solid(up) && (up.c.lt[up.i] == None || up.c.lt[up.i] == gt))
            {
                float ua = up.c.lt[up.i] == gt ? up.c.liq[up.i] : 0f;
                float mv = Mathf.Min(a, (1f - ua) * (gt == Fumes ? 0.25f : 0.6f));
                if (mv > 0.001f)
                {
                    up.c.lt[up.i] = gt; up.c.liq[up.i] = ua + mv;
                    a -= mv;
                    if (a < 0.02f) { Clear(me); return; }
                    me.c.liq[me.i] = a;
                }
            }
            for (int dx = -1; dx <= 1; dx += 2)
            {
                var n = Cell(x + dx, y);
                if (Solid(n) || (n.c.lt[n.i] != None && n.c.lt[n.i] != gt)) continue;
                float na = n.c.lt[n.i] == gt ? n.c.liq[n.i] : 0f;
                if (a <= na + 0.02f) continue;
                float flow = (a - na) * 0.25f;
                n.c.lt[n.i] = gt; n.c.liq[n.i] = na + flow;
                a -= flow;
                me.c.liq[me.i] = a;
            }
        }

        bool VoidTouch(int x, int y)
        {
            for (int i = 0; i < 4; i++)
            {
                Block b = w.Get(x + (i == 0 ? 1 : i == 1 ? -1 : 0), y + (i == 2 ? 1 : i == 3 ? -1 : 0));
                if (b == Block.Bedrock || b == Block.DarkMatter || b == Block.Seal) return true;
            }
            return false;
        }

        static void Clear(C k)
        {
            if (!k.ok) return;
            ClearGrains(k.c, k.i);
            k.c.lt[k.i] = None;
            k.c.liq[k.i] = 0f;
        }

        static void AddGas(C k, float amt)
        {
            if (!k.ok || Blocks(k.c.t[k.i])) return;
            byte t = k.c.lt[k.i];
            if (IsLiq(t)) return;
            if (t == Fumes) { k.c.liq[k.i] = Mathf.Min(1f, k.c.liq[k.i] + amt * 0.5f); return; }   // (fumes stay fumes)
            k.c.lt[k.i] = Steam;
            k.c.liq[k.i] = Mathf.Min(1f, (t == Steam ? k.c.liq[k.i] : 0f) + amt);
        }

        // poisonous fumes rise out of the liquid: they're put in the first open (not liquid) block above
        void AddFumes(int x, int y, float amt)
        {
            for (int k = 0; k < 8; k++, y++)
            {
                var c = Cell(x, y);
                if (!c.ok || Blocks(c.c.t[c.i])) return;
                byte t = c.c.lt[c.i];
                if (IsLiq(t)) continue;
                c.c.lt[c.i] = Fumes;
                c.c.liq[c.i] = Mathf.Min(1f, (t == Fumes || t == Steam ? c.c.liq[c.i] : 0f) + amt);
                return;
            }
        }

        void SteamUp(int x, int y, float amt)
        {
            for (int k = 0; k < 8; k++, y++)
            {
                var c = Cell(x, y);
                if (!c.ok || Blocks(c.c.t[c.i])) return;
                if (IsLiq(c.c.lt[c.i])) continue;
                AddGas(c, amt);
                return;
            }
        }

        // ------------------------------------------------------------------ chemistry: poison, acid
        int TakeType(Chunk c, int i, byte t, int n)
        {
            if (c.sub == null || n <= 0) return 0;
            int b = i * SN, got = 0;
            for (int k = 0; k < SN && got < n; k++)
                if ((c.sub[b + k] & TypeMask) == t) { c.sub[b + k] = 0; got++; }
            if (got > 0) Recount(c, i);
            return got;
        }

        // ---- mixing: liquids slowly seep into each other even lying still (grains trade places across the edges of
        // blocks, and haste-mist and poison thin away into the water), and what has dissolved tints the water
        static void AddMix(Chunk c, int i, byte t)
        {
            if (c.mixP == null) return;
            if (t == Poison) c.mixP[i] = Mathf.Min(1f, c.mixP[i] + 1f / 40f);
            else if (t == Haste) c.mixH[i] = Mathf.Min(1f, c.mixH[i] + 1f / 40f);
        }

        void MixTile(Chunk ch, int i, int x, int y)
        {
            int b = i * SN;
            // a grain on the edge of this block trades places with the one across the edge in the next block
            if (Random.value < 0.35f)
            {
                int dir = Random.Range(0, 4), e = Random.Range(0, SUB);
                int dx = dir == 0 ? 1 : dir == 1 ? -1 : 0, dy = dir == 2 ? 1 : dir == 3 ? -1 : 0;
                int mx = dx > 0 ? SUB - 1 : dx < 0 ? 0 : e, my = dy > 0 ? SUB - 1 : dy < 0 ? 0 : e;
                int ox = dx > 0 ? 0 : dx < 0 ? SUB - 1 : e, oy = dy > 0 ? 0 : dy < 0 ? SUB - 1 : e;
                var o = Cell(x + dx, y + dy);
                if (o.ok && o.c.sub != null && !Blocks(o.c.t[o.i]))
                {
                    int a1 = b + my * SUB + mx, a2 = o.i * SN + oy * SUB + ox;
                    byte t1 = (byte)(ch.sub[a1] & TypeMask), t2 = (byte)(o.c.sub[a2] & TypeMask);
                    if (t1 != 0 && t2 != 0 && t1 != t2 && t1 != Lava && t2 != Lava && (Solute(t1) || Solute(t2)))
                    {
                        byte v1 = ch.sub[a1];
                        ch.sub[a1] = o.c.sub[a2]; o.c.sub[a2] = v1;
                        Recount(ch, i); Recount(o.c, o.i);
                    }
                }
            }
            // a grain of haste-mist or poison with water beside it slowly thins away into it
            if (Random.value < 0.25f)
            {
                int k = Random.Range(0, SN);
                byte t = (byte)(ch.sub[b + k] & TypeMask);
                if ((t == Poison || t == Haste))
                {
                    int sx = k & 7, sy = k >> 3;
                    bool wet = (sx > 0 && (ch.sub[b + k - 1] & TypeMask) == Water) || (sx < 7 && (ch.sub[b + k + 1] & TypeMask) == Water)
                        || (sy > 0 && (ch.sub[b + k - SUB] & TypeMask) == Water) || (sy < 7 && (ch.sub[b + k + SUB] & TypeMask) == Water);
                    if (wet && Random.value < 0.12f) { ch.sub[b + k] = (byte)(Water | parity); AddMix(ch, i, t); Recount(ch, i); }
                }
            }
            // the tint spreads through the water and fades (very slowly) as it dilutes away
            if (ch.mixP == null) return;
            float mp = ch.mixP[i], mh = ch.mixH[i];
            if (mp <= 0.002f && mh <= 0.002f) { ch.mixP[i] = 0f; ch.mixH[i] = 0f; return; }
            if (ch.lt[i] != Water) { ch.mixP[i] = 0f; ch.mixH[i] = 0f; return; }
            mp *= 0.9993f; mh *= 0.9993f;
            for (int d = 0; d < 4; d++)
            {
                var n = Cell(x + (d == 0 ? 1 : d == 1 ? -1 : 0), y + (d == 2 ? 1 : d == 3 ? -1 : 0));
                if (!n.ok || n.c.mixP == null || n.c.lt[n.i] != Water) continue;
                float fp = (mp - n.c.mixP[n.i]) * 0.03f, fh = (mh - n.c.mixH[n.i]) * 0.03f;
                if (fp > 0f) { n.c.mixP[n.i] += fp; mp -= fp; }
                if (fh > 0f) { n.c.mixH[n.i] += fh; mh -= fh; }
            }
            ch.mixP[i] = mp; ch.mixH[i] = mh;
        }

        // ACID won't touch these: steel, the void, gilded brick, ore and minerals, glass (and anything unbreakable)
        public static bool AcidProof(Block b)
        {
            switch (b)
            {
                case Block.Plate: case Block.Bedrock: case Block.DarkMatter: case Block.Seal: case Block.GildBrick:
                case Block.Gold: case Block.Gem: case Block.Crystal: case Block.BigGold2: case Block.BigGold4: case Block.BigGold6:
                case Block.Glass: case Block.Steel: case Block.Air: case Block.Lava: case Block.Water:
                    return true;
            }
            return Tiles.Def(b).unbreakable;
        }

        float popT;

        // a tile with poison or acid in it: what they do to each other, to water and lava, and to the blocks round them
        bool Chem(Chunk ch, int i, int x, int y)
        {
            int b = i * SN, nW = 0, nL = 0, nP = 0, nA = 0;
            for (int k = 0; k < SN; k++)
            {
                switch (ch.sub[b + k] & TypeMask)
                {
                    case Water: nW++; break;
                    case Lava: nL++; break;
                    case Poison: nP++; break;
                    case Acid: nA++; break;
                }
            }
            if (nP == 0 && nA == 0) return false;
            Vector2 at = new Vector2(x + 0.5f, y + 0.5f);
            // the void swallows them fast
            if (VoidTouch(x, y))
            {
                TakeType(ch, i, Acid, 8); TakeType(ch, i, Poison, 8);
                AddFumes(x, y + 1, 0.06f);
                if (Random.value < 0.1f) Hiss(at);
                return ch.cnt[i] == 0;
            }
            // lava boils poison off into fumes; lava and acid together go off in little poison blasts
            if (nL > 0 && nP > 0 && Random.value < 0.4f) { TakeType(ch, i, Poison, 3); AddFumes(x, y + 1, 0.08f); if (Random.value < 0.1f) Hiss(at); }
            if (nL > 0 && nA > 0 && Random.value < 0.3f) { TakeType(ch, i, Acid, 2); TakeType(ch, i, Lava, 1); AddFumes(x, y + 1, 0.08f); if (Random.value < 0.08f) PoisonPop(at); }
            if (nA > 0)
            {
                // acid and poison eat each other: fumes, and now and then a little poisonous pop
                if (nP > 0 && Random.value < 0.25f) { TakeType(ch, i, Acid, 1); TakeType(ch, i, Poison, 1); AddFumes(x, y + 1, 0.05f); if (Random.value < 0.04f) PoisonPop(at); }
                // acid eats water, boiling it off as steam (and spending itself)
                if (nW > 0 && Random.value < 0.3f) { TakeType(ch, i, Water, 2); TakeType(ch, i, Acid, 1); SteamUp(x, y + 1, 0.05f); if (Random.value < 0.05f) Hiss(at); }
                // acid eats the blocks it touches: the more acid against it, the faster (and the acid is used up doing it)
                nA = 0;
                for (int k = 0; k < SN; k++) if ((ch.sub[b + k] & TypeMask) == Acid) nA++;
                if (nA > 0)
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        Block nb = w.Get(nx, ny);
                        if (!Tiles.Solid(nb) || AcidProof(nb)) continue;
                        if (Random.value >= nA / (float)SN * (d == 3 ? 0.09f : 0.05f)) continue;   // (it sits on the floor: that goes fastest)
                        w.Damage(nx, ny, 1, false, 1f, false);
                        TakeType(ch, i, Acid, 3);
                        AddFumes(x, y + 1, 0.03f);
                        Game.I.fx.Burst(new Vector2(nx + 0.5f, ny + 0.5f) - new Vector2(nx - x, ny - y) * 0.45f, 2, new Color(0.75f, 1f, 0.3f), 1.5f, 0.4f, -2f, 1f, false);
                        if (Random.value < 0.15f) Game.I.sfx.PlayAt(at, "fizz", 0.25f, Random.Range(1.3f, 1.7f));
                        if (ch.cnt[i] == 0) return true;
                    }
                // and it slowly gives off fumes on its own as it works
                if (Random.value < 0.004f) { TakeType(ch, i, Acid, 1); AddFumes(x, y + 1, 0.02f); }
            }
            return ch.cnt[i] == 0;
        }

        // the little poisonous blast acid makes with lava or poison
        void PoisonPop(Vector2 at)
        {
            if (popT > Time.time) return;
            popT = Time.time + 0.25f;
            var g = Game.I;
            g.fx.Burst(at, 14, new Color(0.55f, 1f, 0.3f), 5f, 0.5f, -3f, 1f, false);
            g.fx.Ring(at, 0.9f, new Color(0.6f, 1f, 0.35f));
            g.sfx.PlayAt(at, "explode", 0.35f, 1.6f);
            foreach (var e in g.ents)
                if (!e.dead && e is Enemy en && !en.Intangible && (en.pos - at).sqrMagnitude < 1.4f * 1.4f) { en.TakeShot(3f, (en.pos - at).normalized * 4f); en.Poison(3f, 1.5f); }
            var pl = g.player;
            if (pl.mode != Player.Mode.Dead && (pl.pos - at).sqrMagnitude < 1.3f * 1.3f) pl.Poison(3f);
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) AddFumes(Mathf.FloorToInt(at.x) + dx, Mathf.FloorToInt(at.y) + dy, 0.12f);
        }

        // heat (fire, a blast) next to this block: poison boils off into fumes, sand melts into glass
        public static void Heat(int x, int y, float strength = 1f)
        {
            var f = Game.I != null ? Game.I.fluids : null;
            if (f == null) return;
            f.lastCi = int.MinValue;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var c = f.Cell(x + dx, y + dy);
                    if (!c.ok || c.c.sub == null || c.c.cnt[c.i] == 0) continue;
                    int got = f.TakeType(c.c, c.i, Poison, Mathf.RoundToInt(4 * strength));
                    if (got > 0) f.AddFumes(x + dx, y + dy, got * 0.02f);
                }
            if (Random.value < 0.25f * strength) f.Glassify(x, y);
        }

        // sand by something hot melts into glass
        void Glassify(int x, int y)
        {
            for (int d = 0; d < 4; d++)
            {
                int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (w.Get(nx, ny) != Block.Sand) continue;
                w.Set(nx, ny, Block.Glass);
                Game.I.fx.Burst(new Vector2(nx + 0.5f, ny + 0.5f), 6, new Color(1f, 0.75f, 0.35f), 2f, 0.4f, -3f, 1f, false);
                Game.I.sfx.PlayAt(new Vector2(nx + 0.5f, ny + 0.5f), "fizz", 0.3f, 0.8f);
                return;
            }
        }

        // a blast boils the poison and acid near it off into fumes
        void BlastBoil(Vector2 c, float r)
        {
            int R = Mathf.CeilToInt(r * SUB), gx0 = Mathf.FloorToInt(c.x * SUB), gy0 = Mathf.FloorToInt(c.y * SUB), got = 0;
            for (int gy = gy0 - R; gy <= gy0 + R; gy++)
                for (int gx = gx0 - R; gx <= gx0 + R; gx++)
                {
                    if ((gx - gx0) * (gx - gx0) + (gy - gy0) * (gy - gy0) > R * R || !Res(gx, gy, out var ch, out var ti, out var si)) continue;
                    byte t = (byte)(ch.sub[si] & TypeMask);
                    if (t != Poison && t != Acid) continue;
                    ch.sub[si] = 0; Count(ch, ti, t, -1); got++;
                }
            if (got > 0)
                for (int k = 0; k < Mathf.Min(9, 1 + got / 20); k++) AddFumes(Mathf.FloorToInt(c.x + Random.Range(-r, r)), Mathf.FloorToInt(c.y + Random.Range(-r * 0.5f, r)), 0.25f);
        }

        // how strong a liquid is at a point: the share of the grains in that block that are of it (a splash of poison in a
        // pool of water is weak; a pool of poison is strong)
        public static float Concentration(Vector2 p, byte t)
        {
            var f = Game.I != null ? Game.I.fluids : null;
            if (f == null) return 0f;
            var c = f.Cell(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
            if (!c.ok || c.c.sub == null || c.c.cnt[c.i] == 0) return 0f;
            int b = c.i * SN, n = 0;
            for (int k = 0; k < SN; k++) if ((c.c.sub[b + k] & TypeMask) == t) n++;
            return n / (float)c.c.cnt[c.i];
        }

        // a blob of liquid thrown about (a spitter's glob): it bursts into flying drops that land as grains
        public static void Spill(Vector2 p, byte t, int n, Vector2 v)
        {
            var f = Game.I != null ? Game.I.fluids : null;
            if (f == null) return;
            for (int k = 0; k < n; k++)
                f.AddDrop(p + Random.insideUnitCircle * 0.2f, v * 0.25f + Random.insideUnitCircle * 3.5f + Vector2.up * 2f, t);
        }

        // ------------------------------------------------------------------ splashes
        // Something hitting the water knocks surface grains loose as flying drops; a blast flings all the water
        // near it. Drops fly on their own (gravity, no collisions with each other) and turn back into grains
        // wherever they come down.

        struct Drop { public Vector2 p, v; public byte t; public bool rise, push, bubble; }   // rise: shoved up through the water by a blast under it; push: shoved out sideways through it   // rise: shoved up through the water by a blast under it
        readonly System.Collections.Generic.List<Drop> drops = new System.Collections.Generic.List<Drop>();
        const int MaxDrops = 2500;

        // (old callers) a splash of a given strength, straight down
        public static void Splash(Vector2 p, float strength) => Impact(p, 1f, new Vector2(0f, -14f * Mathf.Clamp01(strength)));

        // something of a given size (in blocks, ~1 for you) hit the water at p with velocity v
        public static void Impact(Vector2 p, float size, Vector2 v)
        {
            var f = Game.I != null ? Game.I.fluids : null;
            if (f == null) return;
            float sp = Mathf.Min(v.magnitude, 16f);   // a bullet's speed doesn't make a tidal wave
            float force = size * (0.25f + sp / 16f);
            if (force < 0.05f) return;
            f.lastCi = int.MinValue;
            int r = Mathf.Clamp(Mathf.RoundToInt((0.4f + size * 0.8f) * SUB), 2, 3 * SUB);
            int n = Mathf.Clamp(Mathf.RoundToInt(force * 26f), 2, 160);
            float up = 2.5f + force * 4.5f;
            float side = Mathf.Clamp(v.x, -8f, 8f) * 0.35f;
            int gx0 = Mathf.FloorToInt(p.x * SUB), gy0 = Mathf.FloorToInt(p.y * SUB);
            for (int k = 0; k < n * 3 && n > 0; k++)
            {
                int gx = gx0 + Random.Range(-r, r + 1);
                if (!f.TakeSurface(gx, gy0, out var at, out var t)) continue;
                n--;
                float off = (gx - gx0) / (float)(r + 1);   // further out: lower and more sideways (a crown)
                var dv = new Vector2(off * up * 0.6f + side + Random.Range(-0.6f, 0.6f), up * (1f - Mathf.Abs(off) * 0.55f) * Random.Range(0.55f, 1.05f));
                f.AddDrop(at, dv, t);
            }
        }

        // (old callers) an explosion
        public static void Ripple(Vector2 c, float r) => Blast(c, r);

        // a blast flings the water around it outwards
        public static void Blast(Vector2 c, float r)
        {
            var f = Game.I != null ? Game.I.fluids : null;
            if (f == null || r <= 0f) return;
            f.lastCi = int.MinValue;
            f.BlastBoil(c, r * 0.7f);
            // under water, or close enough to touch water: the water is driven out from the blast, leaving a cavity that
            // holds a moment and then collapses (an underwater one throws a column of water up out of the surface as it does)
            bool under = Game.I.world.FluidAtPoint(c) == Water;
            if (!under && !f.AnyWater(c, r)) return;
            f.UnderwaterBlast(c, Mathf.Min(r, 4.5f), under);
        }

        bool AnyWater(Vector2 c, float r)
        {
            int x0 = Mathf.FloorToInt(c.x - r), x1 = Mathf.FloorToInt(c.x + r), y0 = Mathf.FloorToInt(c.y - r), y1 = Mathf.FloorToInt(c.y + r);
            var w = Game.I.world;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (w.LiquidType(x, y) == Water && (new Vector2(x + 0.5f, y + 0.5f) - c).sqrMagnitude < (r + 0.7f) * (r + 0.7f)) return true;
            return false;
        }

        // the top grain of the water in column gx near row gy0 (it's taken out of the water)
        bool TakeSurface(int gx, int gy0, out Vector2 at, out byte t)
        {
            at = Vector2.zero; t = 0;
            for (int gy = gy0 + 16; gy >= gy0 - 24; gy--)
            {
                if (!Res(gx, gy, out var ch, out var ti, out var si)) continue;
                byte v = (byte)(ch.sub[si] & TypeMask);
                if (v == 0 || !Empty(gx, gy + 1)) continue;
                ch.sub[si] = 0; Count(ch, ti, v, -1);
                at = new Vector2((gx + 0.5f) / SUB, (gy + 1.5f) / SUB);
                t = v;
                return true;
            }
            return false;
        }

        void AddDrop(Vector2 p, Vector2 v, byte t, bool rise = false, bool push = false)
        {
            if (drops.Count >= MaxDrops) { Land(p, t); return; }
            drops.Add(new Drop { p = p, v = v, t = t, rise = rise, push = push });
        }

        // STIR: something moving through the water (a shot, a blade, a swimmer) drags the liquid along with it: grains
        // around it trade places along its path (so a streak of haste-mist inside a pool is pulled through it and spreads
        // out), and those at the edge of the water get a shove sideways
        public static void Stir(Vector2 p, Vector2 v, float r = 0.35f)
        {
            var f = Game.I != null ? Game.I.fluids : null;
            if (f == null) return;
            float sp = v.magnitude;
            if (sp < 1.5f) return;
            f.lastCi = int.MinValue;
            Vector2 dir = v / sp;
            int reachG = Mathf.Clamp(Mathf.RoundToInt(sp * 0.12f), 1, 3);   // faster things drag it further
            int sx = Mathf.Abs(dir.x) >= 0.38f ? (dir.x > 0f ? 1 : -1) : 0, sy = Mathf.Abs(dir.y) >= 0.38f ? (dir.y > 0f ? 1 : -1) : 0;
            int R = Mathf.Max(1, Mathf.RoundToInt(r * SUB));
            int gx0 = Mathf.FloorToInt(p.x * SUB), gy0 = Mathf.FloorToInt(p.y * SUB);
            byte mom = Mom(sx, Mathf.Clamp(Mathf.RoundToInt(sp / 5f), 1, 3));
            for (int gy = gy0 - R; gy <= gy0 + R; gy++)
                for (int gx = gx0 - R; gx <= gx0 + R; gx++)
                {
                    if ((gx - gx0) * (gx - gx0) + (gy - gy0) * (gy - gy0) > R * R) continue;
                    if (!f.Res(gx, gy, out var ch, out var ti, out var si)) continue;
                    byte v0 = ch.sub[si];
                    byte t0 = (byte)(v0 & TypeMask);
                    if (t0 == 0 || t0 == Lava) continue;
                    // the grain behind it is dragged forward into this spot (they swap)
                    int bx = gx - sx * reachG, by = gy - sy * reachG;
                    if (Random.value < 0.7f && f.Res(bx, by, out var bc, out var bti, out var bsi) && !Rock(bc, bti, bsi))
                    {
                        byte bv = bc.sub[bsi], bt = (byte)(bv & TypeMask);
                        if (bt != 0 && bt != Lava && bt != t0)
                        {
                            ch.sub[si] = bv; bc.sub[bsi] = v0;
                            if (bc != ch || bti != ti) { Recount(ch, ti); Recount(bc, bti); }
                        }
                    }
                    if (sx != 0) ch.sub[si] = (byte)((ch.sub[si] & ~(MomMask | MomRight)) | mom);
                }
        }

        // A bubble of gas under water (fire, a blast): it wobbles up and turns to steam at the surface
        public static void Bubble(Vector2 p, float speed = 3f)
        {
            var f = Game.I != null ? Game.I.fluids : null;
            if (f == null || f.drops.Count >= MaxDrops) return;
            f.drops.Add(new Drop { p = p, v = new Vector2(Random.Range(-0.6f, 0.6f), speed), t = Water, bubble = true });
        }

        // FIN'S TIDAL BURST in water: the water around her is driven outwards from her middle, leaving a bubble
        public static void PushOut(Vector2 c, float r)
        {
            var f = Game.I != null ? Game.I.fluids : null;
            if (f == null) return;
            f.lastCi = int.MinValue;
            int R = Mathf.CeilToInt(r * SUB);
            int gx0 = Mathf.FloorToInt(c.x * SUB), gy0 = Mathf.FloorToInt(c.y * SUB);
            int moved = 0;
            for (int gy = gy0 - R; gy <= gy0 + R && moved < 1200; gy++)
                for (int gx = gx0 - R; gx <= gx0 + R && moved < 1200; gx++)
                {
                    float dx = gx - gx0, dy = gy - gy0;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > R) continue;
                    if (!f.Res(gx, gy, out var ch, out var ti, out var si)) continue;
                    byte t = (byte)(ch.sub[si] & TypeMask);
                    if (t == 0 || t == Lava) continue;
                    ch.sub[si] = 0; Count(ch, ti, t, -1);
                    Vector2 dir = d < 0.5f ? Random.insideUnitCircle.normalized : new Vector2(dx, dy) / d;
                    // the inner water moves first and fastest: a ring rolling outwards
                    float spd = Mathf.Lerp(16f, 9f, d / (R + 1f)) * Random.Range(0.85f, 1.1f);
                    f.AddDrop(new Vector2((gx + 0.5f) / SUB, (gy + 0.5f) / SUB), dir * spd, t, false, true);
                    moved++;
                }
            f.pockets.Add(new Pocket { c = c, r = r * 0.55f, life = 0.55f, push = true });
            Game.I.fx.Burst(c, 30, new Color(0.85f, 0.97f, 1f, 0.85f), 6f, 0.9f, -6f, 1f, false);
        }

        // FIN'S TIDAL BURST in (or by) water: a ball of new water appears around her all at once, shoving the water that
        // was there outwards (it surges out through the pool, and sprays off the surface); the part of the ball above
        // the surface simply falls, as it would in the air. Only where open space reaches from her (never through a wall).
        public static void CreateBall(Vector2 c, float r)
        {
            var f = Game.I != null ? Game.I.fluids : null;
            if (f == null) return;
            f.lastCi = int.MinValue;
            var w = Game.I.world;
            // the blocks open space reaches from her, within the ball
            var reach = new System.Collections.Generic.HashSet<long>();
            var q = new System.Collections.Generic.Queue<Vector2Int>();
            var c0 = new Vector2Int(Mathf.FloorToInt(c.x), Mathf.FloorToInt(c.y));
            q.Enqueue(c0); reach.Add(((long)c0.y << 32) ^ (uint)c0.x);
            while (q.Count > 0 && reach.Count < 200)
            {
                var b = q.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    var n = b + (k == 0 ? Vector2Int.right : k == 1 ? Vector2Int.left : k == 2 ? Vector2Int.up : Vector2Int.down);
                    if ((new Vector2(n.x + 0.5f, n.y + 0.5f) - c).magnitude > r + 0.8f) continue;
                    if (Blocks(w.Get(n.x, n.y))) continue;
                    long key = ((long)n.y << 32) ^ (uint)n.x;
                    if (reach.Add(key)) q.Enqueue(n);
                }
            }
            int R = Mathf.CeilToInt(r * SUB);
            int gx0 = Mathf.FloorToInt(c.x * SUB), gy0 = Mathf.FloorToInt(c.y * SUB);
            for (int gy = gy0 - R; gy <= gy0 + R; gy++)
                for (int gx = gx0 - R; gx <= gx0 + R; gx++)
                {
                    float dx = gx - gx0, dy = gy - gy0;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > R) continue;
                    int tx = gx >> 3, ty = gy >> 3;
                    if (!reach.Contains(((long)ty << 32) ^ (uint)tx)) continue;
                    if (!f.Res(gx, gy, out var ch, out var ti, out var si) || Rock(ch, ti, si)) continue;
                    byte had = (byte)(ch.sub[si] & TypeMask);
                    if (had == Lava) continue;
                    Vector2 dir = d < 0.5f ? Vector2.up : new Vector2(dx, dy) / d;
                    if (had == Water || had == Haste)
                    {
                        // the water already here is shoved out ahead of the new
                        ch.sub[si] = 0; Count(ch, ti, had, -1);
                        f.AddDrop(new Vector2((gx + 0.5f) / SUB, (gy + 0.5f) / SUB), dir * Random.Range(10f, 15f), had, false, true);
                    }
                    ch.sub[si] = (byte)(Water | f.parity);
                    Count(ch, ti, Water, 1);
                }
            Game.I.fx.Burst(c, 24, new Color(0.85f, 0.97f, 1f, 0.85f), 6f, 0.8f, -6f, 1f, false);
        }

        // a pushed drop that has run out of push inside the water: it settles in the nearest open spot further out
        void Settle(Vector2 p, Vector2 dir, byte t)
        {
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
            dir.Normalize();
            for (int k = 0; k < 160; k++)
            {
                Vector2 q = p + dir * (k / (float)SUB);
                int qx = Mathf.FloorToInt(q.x * SUB), qy = Mathf.FloorToInt(q.y * SUB);
                if (!Res(qx, qy, out var ch, out var ti, out var si)) break;
                if (Rock(ch, ti, si)) break;
                if (ch.sub[si] == 0) { ch.sub[si] = (byte)(t | parity); Count(ch, ti, t, 1); return; }
            }
            // no room that way: it rises to the top of the water (the level goes up), nothing is lost
            int ux = Mathf.FloorToInt(p.x * SUB), uy = Mathf.FloorToInt(p.y * SUB);
            for (int k = 0; k < 320; k++, uy++)
            {
                if (!Res(ux, uy, out var ch, out var ti, out var si) || Rock(ch, ti, si)) break;
                if (ch.sub[si] == 0) { ch.sub[si] = (byte)(t | parity); Count(ch, ti, t, 1); return; }
            }
            Land(p, t);
        }

        // ---- the air pocket of a blast under water
        struct Pocket { public Vector2 c; public float r, r0, grow, t, life; public bool push, jet; }
        readonly System.Collections.Generic.List<Pocket> pockets = new System.Collections.Generic.List<Pocket>();

        // empty a ball of water: the grains surge upwards through the water (and out of the top as a plume)
        void HollowOut(Vector2 c, float r, bool first)
        {
            int R = Mathf.CeilToInt(r * SUB);
            int gx0 = Mathf.FloorToInt(c.x * SUB), gy0 = Mathf.FloorToInt(c.y * SUB);
            int moved = 0;
            for (int gy = gy0 - R; gy <= gy0 + R && moved < 900; gy++)
                for (int gx = gx0 - R; gx <= gx0 + R && moved < 900; gx++)
                {
                    float dx = gx - gx0, dy = gy - gy0;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > R) continue;
                    if (!Res(gx, gy, out var ch, out var ti, out var si)) continue;
                    byte t = (byte)(ch.sub[si] & TypeMask);
                    if (t == 0) continue;
                    ch.sub[si] = 0; Count(ch, ti, t, -1);
                    Vector2 dir = d < 0.5f ? Vector2.up : new Vector2(dx, dy) / d;
                    float k = 1f - d / (R + 1f);
                    var v = new Vector2(dir.x * (first ? 5f : 2f) * k, (first ? 12f : 7f) + Mathf.Max(0f, dir.y) * 6f * k) * Random.Range(0.75f, 1.15f);
                    AddDrop(new Vector2((gx + 0.5f) / SUB, (gy + 0.5f) / SUB), v, t, true);
                    moved++;
                }
            // the boil of bubbles
            var g = Game.I;
            g.fx.Burst(c, first ? 40 : 6, new Color(0.85f, 0.97f, 1f, 0.85f), first ? 5f : 2f, first ? 1.1f : 0.8f, -9f, 1f, false);
        }

        // ---- a blast under water: the blast's shape is emptied at once, all that water driven outwards from the middle
        // (it surges out through the pool, lifting the surface and spraying off it); a pressure ring rolls out through
        // the water, slowing as it goes; the pocket of air holds for a moment and then the water closes in.
        void UnderwaterBlast(Vector2 c, float r, bool under = true)
        {
            // the heart of it boils to steam; the gas bubble then swells to the blast's size, driving the water out
            float core = under ? r * 0.3f : 0f;
            int R = Mathf.CeilToInt(core * SUB);
            int gx0 = Mathf.FloorToInt(c.x * SUB), gy0 = Mathf.FloorToInt(c.y * SUB);
            int boiled = 0;
            for (int gy = gy0 - R; gy <= gy0 + R; gy++)
                for (int gx = gx0 - R; gx <= gx0 + R; gx++)
                {
                    float dx = gx - gx0, dy = gy - gy0;
                    if (dx * dx + dy * dy > R * R || !Res(gx, gy, out var ch, out var ti, out var si)) continue;
                    byte t = (byte)(ch.sub[si] & TypeMask);
                    if (t != Water) continue;
                    ch.sub[si] = 0; Count(ch, ti, t, -1);
                    boiled++;
                }
            if (under) for (int k = 0; k < Mathf.Min(40, 6 + boiled / 6); k++) Bubble(c + Random.insideUnitCircle * core, Random.Range(2f, 6f));
            pockets.Add(new Pocket { c = c, r = r, r0 = under ? core : r * 0.6f, grow = under ? 0.16f : 0.08f, life = under ? 0.75f : 0.5f, push = true, jet = under });
            rings.Add(new Ring { c = c, r0 = r, t = 0f });
            var g = Game.I;
            if (under) g.fx.Burst(c, 40, new Color(0.85f, 0.97f, 1f, 0.85f), r * 2.2f, 1.2f, -8f, 1f, false);
            g.cam.Shake(0.35f);
        }

        // the pressure ring of an underwater blast
        struct Ring { public Vector2 c; public float r0, t; }
        readonly System.Collections.Generic.List<Ring> rings = new System.Collections.Generic.List<Ring>();
        const float RingLife = 1.3f;
        static float RingR(Ring g) => g.r0 + 14f * (1f - Mathf.Exp(-2.4f * g.t)) / 2.4f;   // fast at first, slowing with distance

        // the bubble Fin leaves: water creeping back in is pushed out again until it lets go
        void KeepClear(Vector2 c, float r)
        {
            int R = Mathf.CeilToInt(r * SUB);
            int gx0 = Mathf.FloorToInt(c.x * SUB), gy0 = Mathf.FloorToInt(c.y * SUB);
            for (int gy = gy0 - R; gy <= gy0 + R; gy++)
                for (int gx = gx0 - R; gx <= gx0 + R; gx++)
                {
                    float dx = gx - gx0, dy = gy - gy0;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > R || !Res(gx, gy, out var ch, out var ti, out var si)) continue;
                    byte t = (byte)(ch.sub[si] & TypeMask);
                    if (t == 0 || t == Lava) continue;   // (the haste-mist is shoved out with the water)
                    ch.sub[si] = 0; Count(ch, ti, t, -1);
                    Vector2 dir = d < 0.5f ? Vector2.up : new Vector2(dx, dy) / d;
                    AddDrop(c + dir * (r + 0.15f), dir * Random.Range(9f, 14f), t, false, true);   // shoved out past the rim
                }
        }

        bool InPocket(Vector2 p)
        {
            foreach (var pk in pockets) if ((p - pk.c).sqrMagnitude < pk.r * pk.r) return true;
            return false;
        }

        // the collapse: water rushing back in from every side crashes together and a column shoots up out of the surface
        void SurfaceJet(Vector2 c, float r)
        {
            int gx0 = Mathf.FloorToInt(c.x * SUB), gy0 = Mathf.FloorToInt(c.y * SUB);
            // the surface above the blast
            int top = int.MinValue;
            for (int gy = gy0; gy < gy0 + 30 * SUB; gy++)
            {
                if (!Res(gx0, gy, out var ch, out var ti, out _) || Blocks(ch.t[ti])) break;
                if (Liquid(gx0, gy) && Empty(gx0, gy + 1)) { top = gy; break; }
            }
            if (top == int.MinValue) return;
            // the deeper the blast, the more of its push the water soaks up on the way: a deep one barely lifts the surface
            float depth = (top - gy0) / (float)SUB;
            float energy = 1f / (1f + depth * 0.45f);
            if (energy < 0.15f) return;
            int half = Mathf.Clamp(Mathf.RoundToInt(r * 2f), 3, 8);
            for (int gx = gx0 - half; gx <= gx0 + half; gx++)
            {
                float k = 1f - Mathf.Abs(gx - gx0) / (float)(half + 1);
                int n = Mathf.RoundToInt(k * 4f * energy + 0.5f);
                for (int j = 0; j < n; j++)
                {
                    if (!TakeSurface(gx, top, out var at, out var t)) break;
                    float vy = Mathf.Min(10f, (5f + 9f * k) * energy * Mathf.Clamp(r / 3f, 0.6f, 1.2f)) * Random.Range(0.8f, 1.1f);   // capped: a column a couple of blocks high at most
                    var v = new Vector2((gx - gx0) * 0.1f + Random.Range(-0.4f, 0.4f), vy);
                    AddDrop(at, v, t);
                }
            }
        }

        void TickPockets(float dt)
        {
            for (int i = pockets.Count - 1; i >= 0; i--)
            {
                var pk = pockets[i];
                pk.t += dt;
                if (pk.t >= pk.life) { if (pk.jet) SurfaceJet(pk.c, pk.r); pockets.RemoveAt(i); continue; }
                // it holds a moment, then the water closes in from the edges
                float k = pk.t / pk.life;
                float r = pk.r * (k < 0.3f ? 1f : Mathf.Pow(1f - (k - 0.3f) / 0.7f, 0.7f));   // only a short hold before it closes
                if (pk.grow > 0f && pk.t < pk.grow) r = Mathf.Lerp(pk.r0, pk.r, pk.t / pk.grow);   // swelling: the water is shoved out as it grows
                if (r > 0.2f) { if (pk.push) KeepClear(pk.c, r); else HollowOut(pk.c, r, false); }
                pockets[i] = pk;
            }
        }

        void TickDrops(float dt)
        {
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var d = drops[i];
                if (d.bubble)
                {
                    int bx = Mathf.FloorToInt(d.p.x * SUB), by = Mathf.FloorToInt(d.p.y * SUB);
                    if (!Res(bx, by, out var bc, out var bti, out var bsi) || Rock(bc, bti, bsi)) { drops.RemoveAt(i); continue; }
                    if (!Liquid(bx, by) && !InPocket(d.p))
                    {
                        // out of the water: a puff of steam
                        Game.I.world.AddSteam(bx >> 3, by >> 3, 0.06f);
                        drops.RemoveAt(i);
                        continue;
                    }
                    d.v.y = Mathf.MoveTowards(d.v.y, 4.5f, 6f * dt);
                    d.v.x = Mathf.Sin((d.p.y + i) * 6f) * 0.8f;
                    d.p += d.v * dt;
                    drops[i] = d;
                    continue;
                }
                if (d.push)
                {
                    // driven out through the water: it slows as it goes, and settles in the first open spot further out
                    int px0 = Mathf.FloorToInt(d.p.x * SUB), py0 = Mathf.FloorToInt(d.p.y * SUB);
                    if (!Liquid(px0, py0) && Empty(px0, py0) && d.v.sqrMagnitude > 4f && !InPocket(d.p)) { d.push = false; d.v = Vector2.ClampMagnitude(d.v, 7f); drops[i] = d; continue; }   // out of the water: it flies (but not far: the water took most of its push)
                    Vector2 pp = d.p + d.v * dt;
                    if (!Res(Mathf.FloorToInt(pp.x * SUB), Mathf.FloorToInt(pp.y * SUB), out var pc, out var pti, out var psi) || Rock(pc, pti, psi) || d.v.sqrMagnitude < 2.25f)
                    { Settle(d.p, d.v, d.t); drops.RemoveAt(i); continue; }
                    d.v *= 1f - Mathf.Min(0.9f, 2.6f * dt);
                    d.p = pp;
                    drops[i] = d;
                    continue;
                }
                if (d.rise)
                {
                    // shoved up through the water: it rides up until it breaks the surface (a plume), or meets rock
                    int rx = Mathf.FloorToInt(d.p.x * SUB), ry = Mathf.FloorToInt(d.p.y * SUB);
                    if (!Liquid(rx, ry) && Empty(rx, ry)) { d.rise = false; drops[i] = d; continue; }   // out in the air: now it flies
                    Vector2 rp = d.p + d.v * dt;
                    if (!Res(Mathf.FloorToInt(rp.x * SUB), Mathf.FloorToInt(rp.y * SUB), out var rc, out var rti, out var rsi) || Rock(rc, rti, rsi))
                    { Land(d.p, d.t); drops.RemoveAt(i); continue; }
                    d.v.x *= 1f - Mathf.Min(0.9f, 4f * dt);
                    d.v.y = Mathf.Max(d.v.y - 6f * dt, 3f);   // the water slows it, but up it goes
                    d.p = rp;
                    drops[i] = d;
                    continue;
                }
                d.v.y -= 30f * dt;
                d.v *= 1f - Mathf.Min(0.5f, 0.6f * dt);
                Vector2 np = d.p + d.v * dt;
                // stepping grain by grain, so a fast drop doesn't skip through a thin wall
                int steps = Mathf.Max(1, Mathf.CeilToInt((np - d.p).magnitude * SUB));
                bool landed = false;
                Vector2 last = d.p;
                for (int k = 1; k <= steps; k++)
                {
                    Vector2 q = Vector2.Lerp(d.p, np, k / (float)steps);
                    if (!Empty(Mathf.FloorToInt(q.x * SUB), Mathf.FloorToInt(q.y * SUB))) { landed = true; break; }
                    last = q;
                }
                if (landed || np.y < -100000f) { Land(last, d.t, d.v.x); drops.RemoveAt(i); continue; }
                d.p = np;
                drops[i] = d;
            }
        }

        // a drop comes down: it becomes a grain again where it is (or just above, if that's taken)
        void Land(Vector2 p, byte t, float vx = 0f)
        {
            int gx = Mathf.FloorToInt(p.x * SUB), gy = Mathf.FloorToInt(p.y * SUB);
            float ax = Mathf.Abs(vx);
            byte mom = Mom(vx > 0f ? 1 : -1, ax > 6f ? 3 : ax > 3f ? 2 : ax > 1.2f ? 1 : 0);   // it lands still moving sideways
            for (int k = 0; k < 6; k++, gy++)
            {
                if (!Empty(gx, gy)) continue;
                Res(gx, gy, out var ch, out var ti, out var si);
                ch.sub[si] = (byte)(t | parity | mom);
                Count(ch, ti, t, 1);
                return;
            }
        }

        // ------------------------------------------------------------------ rendering (one texture over the view)
        // One texture pixel per grain. Water darkens with depth below its surface, the surface line is bright, and
        // grains with nothing under them (falling, splashing) are lighter, so streams and spray read on their own.

        const int PXL = SUB;
        readonly GameObject go;
        readonly SpriteRenderer sr;
        Texture2D tex;
        Color32[] buf;
        float[] tintP, tintH;   // per block in view: how much poison / haste-mist is dissolved in its water
        byte[] kind;   // per texture pixel: 0 empty, 1 water, 2 lava, 3 a block, 4 haste (9: a hidden spike, while smoothing)
        static bool KL(byte k) => k == 1 || k == 2 || k == 4 || k == 5 || k == 6;
        readonly System.Collections.Generic.List<Vector2Int> boulders = new System.Collections.Generic.List<Vector2Int>();
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
                kind = new byte[tw * th];
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, tw, th), Vector2.zero, PXL, 0, SpriteMeshType.FullRect);
            }
            lastCi = int.MinValue;
            int ox = Mathf.FloorToInt(camPos.x - halfW - 2f), oy = Mathf.FloorToInt(camPos.y - halfH - 2f);
            go.transform.position = new Vector3(ox, oy, 0f);
            System.Array.Clear(buf, 0, buf.Length);
            System.Array.Clear(kind, 0, kind.Length);
            bool any = false;
            int phase = (int)(time * 8f);
            bool zap = Electric.Any;
            int spark = Time.frameCount;   // sparks re-scatter every frame
            if (tintP == null || tintP.Length != cw * chh) { tintP = new float[cw * chh]; tintH = new float[cw * chh]; }
            System.Array.Clear(tintP, 0, tintP.Length); System.Array.Clear(tintH, 0, tintH.Length);
            // gather the grains (and draw steam, which is per block)
            for (int cy = 0; cy < chh; cy++)
                for (int cx = 0; cx < cw; cx++)
                {
                    int x = ox + cx, y = oy + cy;
                    var k = Cell(x, y);
                    if (!k.ok) continue;
                    bool isBoulder = k.c.t[k.i] == Block.Boulder;
                    if (Blocks(k.c.t[k.i]))
                    {
                        for (int py = 0; py < PXL; py++)
                            for (int px = 0; px < PXL; px++)
                                if (!isBoulder || RoundIn[py * SUB + px]) kind[(cy * PXL + py) * tw + cx * PXL + px] = 3;
                        if (isBoulder) boulders.Add(new Vector2Int(cx, cy));
                        if (!isBoulder) continue;
                    }
                    byte t = k.c.lt[k.i];
                    if (t == None) continue;
                    any = true;
                    if (IsGas(t))
                    {
                        bool fume = t == Fumes;
                        float a = k.c.liq[k.i];
                        for (int py = 0; py < PXL; py++)
                            for (int px = 0; px < PXL; px++)
                            {
                                int gx = x * PXL + px, gy = y * PXL + py;
                                float n = U.H01((gx >> 2) + phase / 2, (gy >> 2) - phase, 78);
                                byte al = (byte)Mathf.Clamp(a * 150f * (0.6f + 0.6f * n), 0f, 170f);
                                buf[(cy * PXL + py) * tw + cx * PXL + px] = fume ? new Color32((byte)(130 + n * 40), (byte)(205 + n * 30), 95, (byte)(al * 0.8f)) : new Color32(225, 230, 240, al);
                            }
                        continue;
                    }
                    Ensure(k.c);
                    if (k.c.mixP != null) { tintP[cy * cw + cx] = k.c.mixP[k.i]; tintH[cy * cw + cx] = k.c.mixH[k.i]; }
                    int b = k.i * SN;
                    for (int sy = 0; sy < SUB; sy++)
                        for (int sx = 0; sx < SUB; sx++)
                        {
                            int v = k.c.sub[b + sy * SUB + sx] & TypeMask;
                            if (v != 0) kind[(cy * PXL + sy) * tw + cx * PXL + sx] = (byte)v;
                        }
                }
            // smooth the look (not the water): lone grains sticking up off a surface are hidden, and one-grain notches
            // in a surface or the side of a stream are filled
            for (int py = 2; py < th - 2; py++)
                for (int px = 1; px < tw - 1; px++)
                {
                    int idx = py * tw + px;
                    byte v = kind[idx];
                    byte l = kind[idx - 1], r = kind[idx + 1], u = kind[idx + tw], d = kind[idx - tw];
                    bool lq = KL(l), rq = KL(r), uq = KL(u);
                    if (KL(v) && d != 0 && !lq && !rq && !uq) kind[idx] = 9;              // a spike: hidden
                    else if (v == 0 && lq && rq && d != 0 && d != 9) kind[idx] = l;                     // a notch: filled
                    else if (v == 0)
                    {
                        // a thin sliver of air inside the water (a pool draining leaves these, often along walls): filled in
                        byte u2 = kind[idx + 2 * tw], d2 = kind[idx - 2 * tw];
                        // (a block above counts too: water pressed up under a ledge or ceiling fills it)
                        bool above = uq || u == 3 || KL(u2) || u2 == 3, under = KL(d) || KL(d2);
                        if (above && under && !(lq || rq || l == 3 || r == 3)) above = uq || KL(u2);   // open air to both sides and a ledge above: leave it
                        if (!under && (d == 3 || d2 == 3) && (uq || KL(u2)) && (lq || rq)) under = true;   // a sliver between the water and a floor under it
                        byte fillT = lq ? l : rq ? r : uq ? u : KL(d) ? d : (byte)1;
                        if (above && under) kind[idx] = fillT;
                        // squeezed between a wall and the water
                        else if (((l == 3 && rq) || (r == 3 && lq)) && (d != 0 && d != 9)) kind[idx] = fillT;
                    }
                }
            for (int q = 0; q < kind.Length; q++) if (kind[q] == 9) kind[q] = 0;
            // colour them, column by column from the top: how far below the surface each grain is
            for (int px = 0; px < tw; px++)
            {
                int run = 0;
                for (int py = th - 1; py >= 0; py--)
                {
                    int idx = py * tw + px;
                    byte v = kind[idx];
                    if (v == 0 || v == 3) { run = 0; continue; }
                    bool below = py > 0 && kind[idx - tw] != 0;           // something under it
                    bool lone = (px == 0 || kind[idx - 1] == 0) && (px == tw - 1 || kind[idx + 1] == 0);
                    int gx = ox * PXL + px, gy = oy * PXL + py;
                    Color32 col;
                    if (v == Water)
                    {
                        if ((!below && run < 3) || (lone && run < 2))
                        {
                            // falling or flying: a light, glinting grain
                            col = U.H01(gx, gy + phase * 3, 64) > 0.75f ? new Color32(236, 252, 250, 235) : new Color32(150, 226, 234, 215);
                        }
                        else if (run == 0) col = new Color32(196, 250, 240, 235);
                        else if (run == 1) col = new Color32(112, 214, 220, 200);
                        else
                        {
                            col = new Color32(44, 150, 178, 165);   // one colour all the way down
                            if (U.H01((gx >> 1) + phase / 2, (gy >> 1) - phase / 3, 9) > 0.975f) col = new Color32(150, 230, 230, 170);   // a glint
                        }
                        // whatever has dissolved in it tints it: a sickly green for poison, a milky white for haste-mist
                        int ti = (py / PXL) * cw + px / PXL;
                        float tp = Mathf.Min(1f, tintP[ti] * 1.6f) * 0.75f, tht = Mathf.Min(1f, tintH[ti] * 1.6f) * 0.6f;
                        if (tp > 0.01f) col = Color32.Lerp(col, new Color32(96, 200, 84, (byte)Mathf.Max(col.a, 190)), tp);
                        if (tht > 0.01f) col = Color32.Lerp(col, new Color32(232, 232, 236, col.a), tht);
                    }
                    else if (v == Poison)
                    {
                        // POISON: a murky, sickly green, darker deeper down, with slow little bubbles of brighter green
                        float n = U.H01((gx >> 1) + phase / 2, (gy >> 1) - phase / 3, 93);
                        if ((!below && run < 3) || (lone && run < 2)) col = new Color32(150, 235, 110, 225);
                        else if (run == 0) col = new Color32(170, 250, 120, 240);
                        else if (run == 1) col = new Color32(96, 200, 70, 215);
                        else col = n > 0.97f ? new Color32(150, 240, 100, 220) : new Color32((byte)(40 + n * 14), (byte)(140 + n * 20), 44, 205);
                    }
                    else if (v == Acid)
                    {
                        // ACID: a hot yellow-green that seethes and glows
                        float n = U.H01((gx >> 1) + phase, (gy >> 1) - phase, 94);
                        if ((!below && run < 3) || (lone && run < 2)) col = new Color32(235, 255, 140, 235);
                        else if (run == 0) col = new Color32(240, 255, 150, 250);
                        else if (run == 1) col = new Color32(200, 255, 70, 230);
                        else col = new Color32((byte)(150 + n * 50), (byte)(220 + n * 30), (byte)(30 + n * 30), 220);
                    }
                    else if (v == Haste)
                    {
                        // HASTE: a milky white mist that drifts and shimmers (not icy: soft and warm-white, see-through)
                        float n = U.H01((gx >> 2) + phase, (gy >> 2) - phase / 2, 91), n2 = U.H01((gx >> 1) - phase * 2, gy >> 1, 92);
                        if ((!below && run < 3) || (lone && run < 2)) col = new Color32(255, 252, 246, 200);
                        else if (run == 0) col = new Color32(255, 255, 250, 235);
                        else if (run == 1) col = new Color32(246, 242, 236, 190);
                        else col = new Color32((byte)(228 + n * 20), (byte)(226 + n * 18), (byte)(220 + n * 14), (byte)(105 + n * 55 + (n2 > 0.96f ? 70 : 0)));
                    }
                    else
                    {
                        float n = U.H01((gx >> 1) + phase, (gy >> 1) - phase / 2, 77);
                        col = run == 0 ? new Color32(255, 235, 120, 255) : !below ? new Color32(255, 200, 90, 250) : new Color32(255, (byte)(90 + n * 90), (byte)(15 + n * 30), 245);
                    }
                    buf[idx] = col;
                    run++;
                }
            }
            // a boulder sitting in the water is seen through the water (tinted like it, not dry)
            foreach (var bq in boulders)
            {
                int bx0 = bq.x * PXL, by0 = bq.y * PXL, wet = 0, outN = 0;
                for (int k2 = 0; k2 < SN; k2++)
                {
                    if (RoundIn[k2]) continue;
                    outN++;
                    if (KL(kind[(by0 + (k2 >> 3)) * tw + bx0 + (k2 & 7)])) wet++;
                }
                bool topWet = by0 + SUB < th && KL(kind[(by0 + SUB) * tw + bx0 + 3]) && KL(kind[(by0 + SUB) * tw + bx0 + 4]);
                if (!topWet && wet * 2 < outN) continue;
                for (int k2 = 0; k2 < SN; k2++)
                {
                    if (!RoundIn[k2]) continue;
                    int idx = (by0 + (k2 >> 3)) * tw + bx0 + (k2 & 7);
                    if (kind[idx] == 3) buf[idx] = new Color32(44, 150, 178, 105);
                }
            }
            boulders.Clear();
            if (zap)
                for (int cy = 0; cy < chh; cy++)
                    for (int cx = 0; cx < cw; cx++)
                    {
                        var k = Cell(ox + cx, oy + cy);
                        if (k.ok && k.c.lt[k.i] == Water) Sparkle(cx, cy, ox + cx, oy + cy, spark);
                    }
            // the pressure rings of blasts under water: a bright ripple through the water, fading as it slows
            foreach (var g in rings)
            {
                float rr = RingR(g), fade = 1f - g.t / RingLife;
                int x0 = Mathf.FloorToInt((g.c.x - rr - 1f - ox) * PXL), x1 = Mathf.CeilToInt((g.c.x + rr + 1f - ox) * PXL);
                int y0 = Mathf.FloorToInt((g.c.y - rr - 1f - oy) * PXL), y1 = Mathf.CeilToInt((g.c.y + rr + 1f - oy) * PXL);
                for (int py = Mathf.Max(0, y0); py <= Mathf.Min(th - 1, y1); py++)
                    for (int px = Mathf.Max(0, x0); px <= Mathf.Min(tw - 1, x1); px++)
                    {
                        int idx = py * tw + px;
                        if (kind[idx] != Water) continue;
                        float wx = ox + (px + 0.5f) / PXL - g.c.x, wy = oy + (py + 0.5f) / PXL - g.c.y;
                        float off = Mathf.Abs(Mathf.Sqrt(wx * wx + wy * wy) - rr);
                        if (off > 0.35f) continue;
                        float a = (1f - off / 0.35f) * fade;
                        var bc = buf[idx];
                        buf[idx] = new Color32((byte)Mathf.Lerp(bc.r, 220, a), (byte)Mathf.Lerp(bc.g, 250, a), (byte)Mathf.Lerp(bc.b, 250, a), (byte)Mathf.Max(bc.a, 200 * a));
                    }
            }
            // the flying drops
            foreach (var d in drops)
            {
                if (d.push || d.rise) continue;   // (still inside the water: it's part of it)
                if (d.bubble)
                {
                    int bqx = Mathf.FloorToInt((d.p.x - ox) * PXL), bqy = Mathf.FloorToInt((d.p.y - oy) * PXL);
                    if (bqx < 1 || bqy < 1 || bqx >= tw - 1 || bqy >= th - 1) continue;
                    var bw = new Color32(225, 248, 255, 235);
                    buf[bqy * tw + bqx] = bw; buf[bqy * tw + bqx + 1] = bw; buf[(bqy + 1) * tw + bqx] = bw;   // a little round bubble
                    continue;
                }
                int qx = Mathf.FloorToInt((d.p.x - ox) * PXL), qy = Mathf.FloorToInt((d.p.y - oy) * PXL);
                if (qx < 0 || qy < 0 || qx >= tw || qy >= th) continue;
                any = true;
                buf[qy * tw + qx] = d.t == Lava ? new Color32(255, 210, 90, 250) : d.t == Haste ? new Color32(255, 253, 248, 220) : d.t == Poison ? new Color32(120, 220, 80, 235) : d.t == Acid ? new Color32(220, 255, 90, 240) : new Color32(200, 244, 246, 230);
            }
            if (zap && any) DrawBolts(ox, oy);
            sr.enabled = any;
            if (!any) return;
            tex.SetPixels32(buf);
            tex.Apply(false);
        }

        // ELECTRICITY: white sparks speckled through charged water; the stronger the charge, the more of it they fill
        void Sparkle(int cx, int cy, int x, int y, int seed)
        {
            float el = Electric.Level(x, y);
            if (el <= 0.01f) return;
            for (int py = 0; py < PXL; py++)
                for (int px = 0; px < PXL; px++)
                {
                    int idx = (cy * PXL + py) * tw + cx * PXL + px;
                    if (kind[idx] != Water) continue;
                    float n = U.H01(x * PXL + px + seed * 7, y * PXL + py - seed * 3, 501);
                    if (n >= el) continue;
                    buf[idx] = n < el * 0.4f ? new Color32(255, 255, 255, 255) : new Color32(200, 236, 255, 230);
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

        // lava glows
        public void AddLights(Lighting l, Vector2 camPos, float halfW, float halfH, float time)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(camPos.x - halfW - 3)), x1 = Mathf.Min(World.W - 1, Mathf.CeilToInt(camPos.x + halfW + 3));
            int y0 = Mathf.FloorToInt(camPos.y - halfH - 3), y1 = Mathf.CeilToInt(camPos.y + halfH + 3);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var k = Cell(x, y);
                    if (k.ok && k.c.lt[k.i] == Acid && ((x + y) & 1) == 0) { l.Add(new Vector2(x + 0.5f, y + 0.5f), 1.3f, 0.3f); continue; }   // acid glows faintly
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
