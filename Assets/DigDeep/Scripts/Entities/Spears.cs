using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // A thrown spear. It punches through a few blocks (more for rarer spears), then lodges in the first thing it
    // hits: a block it can't (or may no longer) break, or a foe. A foe takes one hit and then just wears it.
    // A spear lodged in a block sticks out like a little ledge: you (and foes) can stand on it, but walk and jump
    // through it from below. When the block it's in goes, or the foe it's in dies, it drops - head first, toppling
    // over the way a heavy-headed spear does - and can hit one foe on the way down, but never breaks blocks again.
    public class Spear : Entity
    {
        public static readonly List<Spear> All = new List<Spear>();
        const int MaxStuck = 20;
        public const float Len = 1.4f;   // tip to tail, in world units
        static readonly Vector2 Tip = new Vector2(0.12f, 0.12f);

        enum St { Flying, InBlock, InFoe, Falling, OnSpear }
        St st;
        Vector2 vel;
        float ang;          // degrees: the way the head points
        float spin;         // degrees per second (falling only)
        int blocksLeft;
        readonly float dmg, knock;
        readonly bool fiery;
        readonly Player owner;
        float life, fade = -1f;
        int cellX, cellY;   // the block it's lodged in
        Enemy foe; Vector2 foeOff; float foeAng;
        Spear parent;       // a spear lodged on another spear (a longer ledge): it drops when that one goes
        readonly HashSet<Enemy> hit = new HashSet<Enemy>();

        // as a platform: the part sticking out of the wall
        public bool IsPlatform;
        public float platX0, platX1, platTop;

        public Spear(Player p, Weapon w, Vector2 at, Vector2 v, float dmg)
        {
            owner = p;
            pos = at; vel = v;
            ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            this.dmg = dmg * (w.Has(Mod.HighCaliber) ? 1.8f : 1f);
            knock = w.def.knock;
            fiery = w.Has(Mod.Fire);
            blocksLeft = w.def.blockPierce + (w.Has(Mod.WallBreaker) ? 2 : 0);
            MakeGO("Spear", w.def.icon, 9);
            if (fiery) sr.color = new Color(1f, 0.75f, 0.55f);
            All.Add(this);
            Pose();
        }

        public override bool AlwaysActive => st == St.Flying || st == St.Falling;
        Vector2 Dir => new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
        Vector2 TipPos => pos + Dir * (Len * 0.5f);

        void Pose()
        {
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, ang);
        }

        public override void Tick(float dt)
        {
            life += dt;
            if (fade >= 0f)
            {
                fade += dt;
                sr.color = new Color(1f, 1f, 1f, 1f - fade / 0.6f);
                if (fade >= 0.6f) { Kill(); return; }
            }
            switch (st)
            {
                case St.Flying: Fly(dt); break;
                case St.Falling: Fall(dt); break;
                case St.InBlock:
                    if (!Wd.Solid(cellX, cellY)) Drop(Vector2.zero);   // the block went: down it comes
                    break;
                case St.OnSpear:
                    if (parent == null || parent.dead || parent.st == St.Falling || parent.st == St.Flying) Drop(Vector2.zero);
                    break;
                case St.InFoe:
                    if (foe == null || foe.dead) { Drop(foe != null ? foe.vel * 0.3f : Vector2.zero); break; }
                    pos = foe.pos + foeOff;
                    ang = foeAng;
                    break;
            }
            if (dead) return;
            UpdatePlatform();
            Pose();
        }

        // ------------------------------------------------------------------ in flight: straight and fast, a gentle arc

        void Fly(float dt)
        {
            if (life > 5f) { Kill(); return; }
            vel.y -= 9f * dt;
            ang = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;   // the head leads
            Travel(dt, true);
            if (Random.value < 0.3f) G.fx.Burst(pos - Dir * 0.6f, 1, new Color(0.9f, 0.85f, 0.7f, 0.6f), 0.3f, 0.2f, 0f);
        }

        // moves in small steps, checking the head against foes and blocks
        void Travel(float dt, bool mayBreak)
        {
            Vector2 move = vel * dt;
            int steps = Mathf.Max(1, Mathf.CeilToInt(move.magnitude / 0.2f));
            Vector2 step = move / steps;
            for (int s = 0; s < steps; s++)
            {
                pos += step;
                Vector2 tip = TipPos;
                if (HitSpears(tip)) return;
                if (dead) return;
                // a foe: one hit, and it wears the spear
                foreach (var e in G.ents)
                {
                    if (e.dead || !(e is Enemy en) || en.Intangible || en.team == Team.Player || hit.Contains(en)) continue;
                    if (!U.Overlap(tip, Tip, en.pos, en.half)) continue;
                    hit.Add(en);
                    float d = st == St.Falling ? dmg * 0.6f : dmg;
                    en.TakeShot(d, vel.normalized * knock);
                    if (fiery) en.Ignite(3f);
                    G.sfx.Play("hit", 0.6f, 0.8f);
                    G.fx.Burst(tip, 8, en.blood, 4f, 0.3f, 6f);
                    if (en.dead) { Drop(vel * 0.2f); return; }
                    StickFoe(en);
                    return;
                }
                // a block: break through while it still can, otherwise lodge
                int cx = Mathf.FloorToInt(tip.x), cy = Mathf.FloorToInt(tip.y);
                if (!Wd.Solid(cx, cy)) continue;
                Block b = Wd.Get(cx, cy);
                var def = Tiles.Def(b);
                bool hard = def.unbreakable || b == Block.Chest || b == Block.Crate || b == Block.Supply || Wd.Anchored(cx, cy);
                if (mayBreak && blocksLeft > 0 && BigOre.Is(b))
                {
                    // big gold: it chips it (or smashes it); if it holds, the spear lodges in it
                    var rb = Wd.Damage(cx, cy, Mathf.RoundToInt(dmg * 2f), true, owner != null ? owner.GoldMul : 1f);
                    if (rb == World.Hit.Broke) { blocksLeft--; vel *= 0.85f; continue; }
                    StickBlock(cx, cy, b);
                    return;
                }
                if (BigOre.Is(b)) hard = true;
                if (mayBreak && blocksLeft > 0 && !hard)
                {
                    var r = Wd.Damage(cx, cy, 999, true, owner != null ? owner.GoldMul : 1f);
                    if (r == World.Hit.Broke)
                    {
                        blocksLeft--;
                        vel *= 0.92f;
                        G.sfx.Material(b, true, 0.6f);
                        continue;
                    }
                }
                StickBlock(cx, cy, b);
                return;
            }
        }

        void StickBlock(int cx, int cy, Block b)
        {
            st = St.InBlock; cellX = cx; cellY = cy;
            vel = Vector2.zero; spin = 0f;
            G.sfx.Material(b, false, 0.8f);   // a thunk, nothing more: it didn't break anything
            Settled();
        }

        // Another lodged spear in the way (the whole shaft is checked, not just the head, so a spear that starts
        // right next to one still meets it). Coming in almost level, this one lodges against it and the two make one
        // longer ledge. Coming down at any steeper angle, it snaps the lodged spear. From below, it passes by.
        bool HitSpears(Vector2 tip)
        {
            Vector2 tail = pos - Dir * (Len * 0.5f);
            float slope = Mathf.Atan2(-vel.y, Mathf.Abs(vel.x)) * Mathf.Rad2Deg;   // > 0 coming down
            bool level = st == St.Flying && Mathf.Abs(slope) <= 12f;
            if (!level && slope <= 12f) return false;   // from below: through it, like a platform
            for (int i = 0; i < All.Count; i++)
            {
                var o = All[i];
                if (o == this || o.dead || o.fade >= 0f || o == parent || (o.st != St.InBlock && o.st != St.OnSpear)) continue;
                Vector2 a = o.pos - o.Dir * (Len * 0.5f), b = o.pos + o.Dir * (Len * 0.5f);
                if (level)
                {
                    // the head meets it (with some slack up and down, so you can actually aim for one)
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(tip - a, ab) / ab.sqrMagnitude);
                    Vector2 near = a + ab * t;
                    if (Mathf.Abs(tip.x - near.x) > 0.2f || Mathf.Abs(tip.y - near.y) > 0.35f) continue;
                    st = St.OnSpear; parent = o;
                    ang = vel.x >= 0f ? 0f : 180f;
                    pos = new Vector2(near.x, o.pos.y) - Dir * (Len * 0.5f - 0.1f);
                    vel = Vector2.zero; spin = 0f;
                    G.sfx.Play("hit", 0.4f, 1.5f);
                    Settled();
                    return true;
                }
                if (SegDist(tail, tip, a, b) > 0.16f) continue;
                o.Snap();   // from above: it breaks the one in the way and carries on
                vel *= 0.85f;
                return false;
            }
            return false;
        }

        static float SegDist(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            if (Cross(p1, p2, q1) * Cross(p1, p2, q2) < 0f && Cross(q1, q2, p1) * Cross(q1, q2, p2) < 0f) return 0f;   // they cross
            return Mathf.Min(Mathf.Min(PtSeg(p1, q1, q2), PtSeg(p2, q1, q2)), Mathf.Min(PtSeg(q1, p1, p2), PtSeg(q2, p1, p2)));
        }
        static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        static float PtSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        // snapped by a spear coming down on it
        void Snap()
        {
            G.fx.Burst(pos, 10, new Color(0.75f, 0.6f, 0.4f), 4f, 0.4f, 12f);
            G.sfx.Play("break", 0.5f, 1.4f);
            Kill();
        }

        void StickFoe(Enemy en)
        {
            st = St.InFoe; foe = en;
            // sink the head a little way into it
            pos += Dir * 0.15f;
            foeOff = pos - en.pos; foeAng = ang;
            vel = Vector2.zero; spin = 0f;
            Settled();
        }

        // too many spears lying about: the oldest lodged one crumbles away
        void Settled()
        {
            int n = 0;
            Spear oldest = null;
            foreach (var s in All)
            {
                if (s.dead || s.st == St.Flying || s.st == St.Falling || s.fade >= 0f || s.st == St.OnSpear) continue;
                n++;
                if (oldest == null || s.life > oldest.life) oldest = s;
            }
            if (n > MaxStuck && oldest != null) oldest.fade = 0f;
        }

        // ------------------------------------------------------------------ dropping: head-heavy, it topples point-down

        void Drop(Vector2 v)
        {
            st = St.Falling;
            foe = null; parent = null;
            vel = v;
            spin = 0f;
            hit.Clear();   // it can hit a foe again on the way down (once)
        }

        void Fall(float dt)
        {
            if (life > 60f) { Kill(); return; }
            vel.y = Mathf.Max(vel.y - 30f * dt, -20f);
            vel.x = Mathf.MoveTowards(vel.x, 0f, 1.5f * dt);
            // gravity pulls the heavy head down: a pendulum about the middle, settling point-down
            float off = Mathf.DeltaAngle(-90f, ang) * Mathf.Deg2Rad;   // 0 when pointing straight down
            spin += -Mathf.Sin(off) * 900f * dt;
            spin *= 1f - Mathf.Min(1f, 2.2f * dt);
            // and the faster it falls, the more the air lines it up behind its head
            float align = Mathf.Clamp01(-vel.y / 18f) * 4f * dt;
            ang = Mathf.LerpAngle(ang, -90f, align);
            ang += spin * dt;
            Travel(dt, false);
            if (dead || st != St.Falling) return;
            // landing flat on its side (it fell before it could turn): it lies where it lands
            if (Wd.Solid(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y - 0.12f)))
            {
                int cx = Mathf.FloorToInt(pos.x), cy = Mathf.FloorToInt(pos.y - 0.12f);
                pos.y = cy + 1 + 0.12f;
                StickBlock(cx, cy, Wd.Get(cx, cy));
            }
        }

        // ------------------------------------------------------------------ standing on it

        void UpdatePlatform()
        {
            IsPlatform = false;
            if (fade >= 0f) return;
            if (st == St.OnSpear)
            {
                // the whole shaft is ledge (it lies level, against the one it's lodged on)
                Vector2 dd = Dir;
                float ax = pos.x - dd.x * Len * 0.5f, bx = pos.x + dd.x * Len * 0.5f;
                platX0 = Mathf.Min(ax, bx); platX1 = Mathf.Max(ax, bx);
                platTop = pos.y + 0.06f;
                IsPlatform = true;
                return;
            }
            if (st != St.InBlock) return;
            Vector2 d = Dir;
            if (Mathf.Abs(d.y) > 0.64f) return;   // only a spear lodged roughly sideways makes a ledge
            Vector2 tail = pos - d * (Len * 0.5f);
            float face = d.x > 0f ? cellX : cellX + 1f;   // where it enters the wall
            platX0 = Mathf.Min(tail.x, face);
            platX1 = Mathf.Max(tail.x, face);
            if (platX1 - platX0 < 0.2f) return;
            float midX = (platX0 + platX1) * 0.5f;
            float t = Mathf.Abs(d.x) > 0.01f ? (midX - pos.x) / d.x : 0f;
            platTop = pos.y + d.y * t + 0.06f;
            IsPlatform = true;
        }

        // a ledge under this box (for landing and ground checks); World.DropThrough ignores them all
        public static bool Under(Vector2 p, Vector2 h, float feetFrom, float feetTo, out float top)
        {
            top = 0f;
            if (World.DropThrough) return false;
            foreach (var s in All)
            {
                if (s.dead || !s.IsPlatform) continue;
                if (p.x + h.x <= s.platX0 || p.x - h.x >= s.platX1) continue;
                if (feetFrom >= s.platTop - 0.02f && feetTo <= s.platTop + 0.001f) { top = s.platTop; return true; }
            }
            return false;
        }

        public override void Kill()
        {
            base.Kill();
            All.Remove(this);
        }
    }

    // An arrow lodged in a block: just battlefield clutter. It drops and fades when the block goes, and old ones
    // fade away so they never pile up.
    public class StuckArrow : Entity
    {
        static readonly List<StuckArrow> All = new List<StuckArrow>();
        const int Max = 40;
        readonly Vector2Int cell;
        float t, vy, fade = -1f;
        readonly float ang;

        StuckArrow(Vector2 at, Vector2 dir, Sprite s, Vector2Int cell)
        {
            pos = at; this.cell = cell;
            ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            MakeGO("Arrow", s, 8);
            go.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            All.Add(this);
            if (All.Count > Max) All[0].fade = 0f;
        }

        public static void Lodge(Vector2 at, Vector2 dir, Sprite s, Vector2Int cell)
        {
            if (s == null || dir.sqrMagnitude < 0.01f) return;
            Game.I.Spawn(new StuckArrow(at, dir, s, cell));
        }

        public override void Tick(float dt)
        {
            t += dt;
            if (fade < 0f && (t > 40f || !Wd.Solid(cell.x, cell.y))) fade = 0f;   // its block went (or it's been ages)
            if (fade >= 0f)
            {
                fade += dt;
                vy -= 25f * dt;
                pos.y += vy * dt;
                sr.color = new Color(1f, 1f, 1f, 1f - fade / 0.5f);
                if (fade >= 0.5f) { Kill(); return; }
            }
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
        }

        public override void Kill() { base.Kill(); All.Remove(this); }
        public static void ClearAll() => All.Clear();
    }
}
