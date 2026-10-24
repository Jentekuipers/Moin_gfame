using UnityEngine;

namespace DigDeep
{
    // FISH live in the cave's pools.
    //  MINNOW  - harmless, darts away from you.
    //  BITER   - goes for anything in the water with it, and leaps out at things near the surface.
    //  GROUPER - only in big pools: slow, tough, hits hard.
    // Out of the water they fall with it; once they hit the ground they flop about frantically, trying to get back in
    // (and suffocate after a while). They go with the flow: a pool draining through a hole sucks them along a curling
    // path towards it and they pour down with the water, dipping deep into the pool they land in. Water that freezes
    // around them traps them in the ice: smash the ice and the fish dies with it, melt it and the fish swims off.
    public class Fish : Enemy
    {
        public enum Kind { Minnow, Biter, Grouper }
        enum St { Swim, Air, Flop, Iced }

        readonly Kind kind;
        St st = St.Swim;
        Vector2 wander = Vector2.right;
        Vector2Int iceCell;
        float wanderT, jumpCd, biteCd, flopT, outT, drainT, levelAvg = 1f, spin, leapT;
        Vector2 drain;
        bool hasDrain, draining;
        readonly float speed;

        protected override bool Harmless => kind == Kind.Minnow || st == St.Flop || st == St.Iced;

        public Fish(int x, int y, int depth, Kind k)
        {
            kind = k;
            pos = new Vector2(x + 0.5f, y + 0.5f);
            half = k == Kind.Minnow ? new Vector2(0.3f, 0.16f) : k == Kind.Biter ? new Vector2(0.36f, 0.24f) : new Vector2(0.55f, 0.32f);
            Setup(k == Kind.Minnow ? "Minnow" : k == Kind.Biter ? "Biter" : "Grouper",
                  k == Kind.Minnow ? "fish_minnow" : k == Kind.Biter ? "fish_piranha" : "fish_grouper",
                  k == Kind.Minnow ? 1 : k == Kind.Biter ? 2 : 7, depth);
            waterLike = true;
            team = Team.Beast;
            speed = k == Kind.Minnow ? 3.4f : k == Kind.Biter ? 4.2f : 2.4f;
            if (k == Kind.Grouper) dmg = Mathf.RoundToInt(dmg * 1.7f);
            gold = k == Kind.Minnow ? 0 : k == Kind.Biter ? 2 : 5;
            blood = new Color(0.85f, 0.3f, 0.3f);
            facing = Random.value < 0.5f ? -1 : 1;
            wander = new Vector2(facing, Random.Range(-0.3f, 0.3f)).normalized;
            wanderT = Random.Range(0.5f, 2f);
        }

        // ---- water around the fish
        static bool Swimmable(int x, int y) => !Wd.Solid(x, y) && Wd.LiquidType(x, y) == Fluids.Water && Wd.LiquidAmount(x, y) > 0.45f;
        static bool Swimmable(Vector2 p) => Swimmable(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
        Vector2Int Cell => new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));

        // the size of the pool a cell belongs to (capped), for spawning: big fish need big pools
        public static int PoolSize(Chunk c, int x, int ly, int cap)
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            var q = new System.Collections.Generic.Queue<int>();
            int W = World.W, H = World.CH;
            q.Enqueue(ly * W + x); seen.Add(ly * W + x);
            while (q.Count > 0 && seen.Count < cap)
            {
                int i = q.Dequeue(); int cx = i % W, cy = i / W;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
                    int j = ny * W + nx;
                    if (seen.Contains(j) || c.lt[j] != Fluids.Water) continue;
                    seen.Add(j); q.Enqueue(j);
                }
            }
            return seen.Count;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            biteCd -= dt; jumpCd -= dt; leapT -= dt;
            switch (st)
            {
                case St.Iced: TickIced(); return;
                case St.Swim: TickSwim(dt); break;
                case St.Air: TickAir(dt); break;
                case St.Flop: TickFlop(dt); break;
            }
            if (dead) return;
            if (st != St.Flop && Mathf.Abs(vel.x) > 0.2f) facing = vel.x > 0f ? 1 : -1;
            Draw(0f);
            float tilt = st == St.Flop ? spin : Mathf.Clamp(vel.y * 6f, -35f, 35f) * facing;
            if (go != null) go.transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        }

        // ---------------------------------------------------------------- frozen into the ice
        void TickIced()
        {
            vel = Vector2.zero;
            Block b = Wd.Get(iceCell.x, iceCell.y);
            if (b == Block.Ice) { Draw(0f); if (sr != null) sr.color = new Color(0.6f, 0.85f, 1f, 0.85f); return; }
            intangible = false;
            if (sr != null) sr.color = Color.white;
            if (Wd.LiquidType(iceCell.x, iceCell.y) == Fluids.Water)
            {
                st = St.Swim;                                    // melted: off it swims
                G.fx.Burst(pos, 6, new Color(0.75f, 0.95f, 1f), 2f, 0.4f, 0f);
            }
            else
            {
                // the ice was smashed with the fish inside it
                G.fx.Burst(pos, 10, new Color(0.8f, 0.95f, 1f), 4f, 0.4f, 10f);
                TakeShot(hp + 999f, Vector2.up * 2f);
            }
        }

        void Freeze(Vector2Int c)
        {
            st = St.Iced; iceCell = c;
            pos = new Vector2(c.x + 0.5f, c.y + 0.5f);
            intangible = true;
            G.sfx.Play("freeze", 0.3f, 1.4f);
        }

        // ---------------------------------------------------------------- swimming
        void TickSwim(float dt)
        {
            var c = Cell;
            if (Wd.Get(c.x, c.y) == Block.Ice) { Freeze(c); return; }
            if (!Swimmable(pos)) { st = St.Air; return; }   // the water left (or it leapt out)
            outT = 0f;

            // wander, steering clear of the pool's edges
            wanderT -= dt;
            if (wanderT <= 0f) { wanderT = Random.Range(1.2f, 3f); wander = U.Rot(new Vector2(Random.value < 0.5f ? -1f : 1f, 0f), Random.Range(-35f, 35f)); }
            if (!Swimmable(pos + wander * 0.9f)) { wander = U.Rot(-wander, Random.Range(-40f, 40f)); wanderT = Random.Range(0.6f, 1.5f); }
            Vector2 want = wander;
            float spd = speed;

            var pl = G.player;
            if (kind == Kind.Minnow)
            {
                // darts away from you
                if (pl.mode != Player.Mode.Dead && (pl.pos - pos).sqrMagnitude < 16f) { want = (pos - pl.pos).normalized; spd *= 1.8f; }
            }
            else
            {
                Entity tgt = FindPrey(kind == Kind.Biter ? 7f : 6f);
                if (tgt != null)
                {
                    Vector2 d = tgt.pos - pos;
                    bool tgtWet = Swimmable(tgt.pos) || Wd.FluidAtPoint(tgt.pos) == Fluids.Water;
                    if (tgtWet) { want = d.normalized; spd *= 1.6f; }
                    else if (kind == Kind.Biter && jumpCd <= 0f && d.y > 0.3f && d.y < 4.5f && Mathf.Abs(d.x) < 3f && !Swimmable(pos + Vector2.up))
                    {
                        // leap out at something near the surface
                        jumpCd = Random.Range(2f, 3.5f);
                        vel = new Vector2(d.x * 2.2f, Mathf.Sqrt(2f * 30f * (d.y + 0.8f)));
                        st = St.Air; leapT = 0.3f;   // (don't count the water it's leaving as landing)
                        G.fx.Burst(pos + Vector2.up * 0.3f, 8, new Color(0.75f, 0.95f, 1f), 3f, 0.4f, 20f, 1f, false);
                        G.sfx.Play("land", 0.3f, 1.8f);
                        return;
                    }
                    else { want = new Vector2(Mathf.Sign(d.x), 0.4f).normalized; spd *= 1.2f; }   // shadow it from below
                }
            }

            // a draining pool sucks the fish towards the hole, curling round on the way
            drainT -= dt;
            float lvl = Wd.LiquidAmount(c.x, c.y);
            levelAvg = Mathf.Lerp(levelAvg, lvl, dt * 1.5f);
            if (drainT <= 0f) { drainT = 0.3f; FindDrain(c); }
            Vector2 pull = Vector2.zero;
            if (hasDrain && (draining || levelAvg < 0.97f))
            {
                Vector2 d = drain - pos;
                float dist = Mathf.Max(0.5f, d.magnitude);
                Vector2 n = d / dist, tan = new Vector2(-n.y, n.x) * (facing >= 0 ? 1f : -1f);
                pull = (n * 5f + tan * 2.5f) / Mathf.Max(1f, dist * 0.35f);
                spd *= 0.6f;                                        // it struggles, but the current wins
            }

            float acc = kind == Kind.Grouper ? 6f : 12f;
            vel = Vector2.MoveTowards(vel, want * spd, acc * dt) + pull * dt;
            vel *= 1f - Mathf.Min(0.5f, 1.2f * dt);                 // water drag (this is also what turns a dive into a dip)
            Move(dt);
            Bite();
        }

        Entity FindPrey(float range)
        {
            Entity best = null; float bd = range * range;
            var pl = G.player;
            if (pl.mode != Player.Mode.Dead && !pl.Camouflaged) { float d = (pl.pos - pos).sqrMagnitude; if (d < bd) { bd = d; best = pl; } }
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en) || en is Fish || en is Boss || en.Intangible || en is WormSegment) continue;
                float d = (en.pos - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = en; }
            }
            return best;
        }

        // bites the other creatures it touches (you are handled by the usual contact damage)
        void Bite()
        {
            if (kind == Kind.Minnow || biteCd > 0f) return;
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en) || en is Fish || en.Intangible) continue;
                if (!U.Overlap(pos, half, en.pos, en.half)) continue;
                en.TakeShot(dmg * 0.08f, (en.pos - pos).normalized * 3f);
                biteCd = 0.6f;
                G.sfx.Play("splat", 0.25f, 1.6f);
                break;
            }
        }

        // a nearby spot where the water pours away through a gap below it
        void FindDrain(Vector2Int c)
        {
            hasDrain = false;
            float bd = 999f;
            for (int dy = -6; dy <= 2; dy++)
                for (int dx = -7; dx <= 7; dx++)
                {
                    int x = c.x + dx, y = c.y + dy;
                    if (Wd.LiquidType(x, y) != Fluids.Water || Wd.Solid(x, y)) continue;
                    if (Wd.Solid(x, y - 1)) continue;
                    float below = Wd.LiquidType(x, y - 1) == Fluids.Water ? Wd.LiquidAmount(x, y - 1) : 0f;
                    if (below > 0.85f) continue;                       // just more pool underneath
                    float d = dx * dx + dy * dy;
                    if (d < bd) { bd = d; drain = new Vector2(x + 0.5f, y - 0.2f); hasDrain = true; }
                }
            draining = hasDrain && Wd.LiquidAmount(c.x, c.y) < 0.995f;
        }

        // ---------------------------------------------------------------- out of the water
        void TickAir(float dt)
        {
            vel.y = Mathf.Max(vel.y - 30f * dt, -18f);
            bool landed = Move(dt);
            if (Swimmable(pos) && leapT <= 0f)
            {
                // splashing down: it plunges a little way under before the water slows it
                st = St.Swim;
                vel = new Vector2(vel.x * 0.6f, vel.y * 0.55f);
                G.fx.Burst(pos + Vector2.up * 0.4f, 8, new Color(0.75f, 0.95f, 1f), 3f, 0.4f, 20f, 1f, false);
                G.sfx.Play("land", 0.25f, 1.7f);
                return;
            }
            if (kind != Kind.Minnow) BiteAir();
            if (landed) { st = St.Flop; flopT = 0.15f; G.sfx.Play("splat", 0.25f, 1.9f); }   // the first touch of ground
        }

        void BiteAir()
        {
            if (biteCd > 0f) return;
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en) || en is Fish || en.Intangible || !U.Overlap(pos, half, en.pos, en.half)) continue;
                en.TakeShot(dmg * 0.08f, vel.normalized * 3f);
                biteCd = 0.6f;
                break;
            }
        }

        void TickFlop(float dt)
        {
            if (Swimmable(pos)) { st = St.Swim; spin = 0f; G.fx.Burst(pos, 6, new Color(0.75f, 0.95f, 1f), 2.5f, 0.4f, 15f, 1f, false); return; }
            var c = Cell;
            if (Wd.Get(c.x, c.y) == Block.Ice) { Freeze(c); return; }
            outT += dt;
            vel.y = Mathf.Max(vel.y - 30f * dt, -18f);
            bool grounded = Move(dt);
            if (grounded) vel.x *= 0.7f;
            spin = Mathf.MoveTowardsAngle(spin, grounded ? (facing > 0 ? 90f : -90f) * 0.9f : spin + 720f * dt, 900f * dt);
            flopT -= dt;
            if (grounded && flopT <= 0f)
            {
                // frantic hops, towards water if it can feel any nearby
                float dir = WaterSide(c);
                if (dir == 0f) dir = Random.value < 0.5f ? -1f : 1f;
                vel = new Vector2(dir * Random.Range(1.8f, 4.2f), Random.Range(4f, 7.5f));
                facing = Random.value < 0.5f ? -1 : 1;
                flopT = Random.Range(0.25f, 0.7f);
                G.sfx.Play("splat", 0.15f, Random.Range(1.6f, 2.2f));
                if (Random.value < 0.5f) G.fx.Burst(pos, 2, new Color(0.75f, 0.95f, 1f), 1.5f, 0.3f, 10f, 1f, false);
            }
            // out of water too long: it suffocates
            if (outT > 12f) TakeShot(hp + 999f, Vector2.up);
        }

        float WaterSide(Vector2Int c)
        {
            for (int r = 1; r <= 7; r++)
                for (int dy = -2; dy <= 2; dy++)
                {
                    if (Swimmable(c.x - r, c.y + dy)) return -1f;
                    if (Swimmable(c.x + r, c.y + dy)) return 1f;
                }
            return 0f;
        }

        // moves with collision; true if it hit the ground
        bool Move(float dt)
        {
            Vector2 p = pos;
            if (Wd.MoveX(ref p, half, vel.x * dt, out _)) { vel.x = -vel.x * 0.3f; wander.x = -wander.x; }
            bool landed = false;
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) { if (vel.y < 0f) landed = true; vel.y = 0f; }
            pos = p;
            return landed || (st == St.Flop && Wd.BoxSolid(pos + new Vector2(0f, -0.05f), half));
        }
    }
}
