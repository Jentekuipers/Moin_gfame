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
        public const float Len = 1.4f;   // tip to tail, in world units (at size 1)
        static readonly Vector2 TipBase = new Vector2(0.12f, 0.12f);

        enum St { Flying, InBlock, InFoe, Falling, OnSpear, Returning }
        St st;
        Vector2 vel;
        float ang;          // degrees: the way the head points
        float spin;         // degrees per second (falling only)
        int blocksLeft;
        readonly float dmg, knock;
        readonly bool burns, poisons, freezes, boomerang;
        readonly Color tint;
        readonly bool tinted;
        readonly Player owner;
        readonly Weapon weapon;
        readonly float size;     // GIANT ROUNDS and friends make spears bigger too
        float len => Len * size;
        Vector2 Tip => TipBase * size;
        float life, fade = -1f, ghostKidT, flightT;
        bool ghost;              // SOUL SPLINTERS: a ghostly copy that homes in through rock and fades
        int cellX, cellY;   // the block it's lodged in
        Enemy foe; Vector2 foeOff; float foeAng;
        Spear parent;       // a spear lodged on another spear (a longer ledge): it drops when that one goes
        int bounces;
        readonly HashSet<Enemy> hit = new HashSet<Enemy>();

        // as a platform: the part sticking out of the wall
        public bool IsPlatform;
        public float platX0, platX1, platTop;

        public Spear(Player p, Weapon w, Vector2 at, Vector2 v, float dmg, bool ghost = false)
        {
            owner = p; weapon = w; this.ghost = ghost;
            pos = at; vel = v;
            ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            var ps = p.s;
            this.dmg = dmg * (w.Has(Mod.HighCaliber) ? 1.8f : 1f);
            knock = w.def.knock;
            burns = w.Has(Mod.Fire) || ps.shotFire;
            poisons = w.Has(Mod.Poison) || ps.shotPoison;
            freezes = w.Has(Mod.Frost) || ps.shotFrost;
            boomerang = !ghost && p.Cursed(Curse.Boomerang);
            size = Mathf.Clamp(ps.shotSize, 0.6f, 2.6f) * (w.Has(Mod.HighCaliber) ? 1.2f : 1f);
            blocksLeft = w.def.blockPierce + (w.Has(Mod.WallBreaker) ? 2 : 0);
            // the elements it carries colour it (and it sheds their sparks, embers and drips)
            var fx = p.AttackFx(w);
            tinted = fx.Count > 0;
            tint = tinted ? fx[fx.Count - 1] : Color.white;
            MakeGO(ghost ? "GhostSpear" : "Spear", w.def.icon, 9);
            go.transform.localScale = Vector3.one * size;
            if (ghost) sr.color = new Color(0.75f, 0.85f, 1f, 0.55f);
            else if (tinted) sr.color = Color.Lerp(Color.white, tint, 0.55f);
            if (!ghost && ps.ghostSwarm) ghostKidT = 0.12f;
            All.Add(this);
            Pose();
        }

        public override bool AlwaysActive => st == St.Flying || st == St.Falling || st == St.Returning;
        Vector2 Dir => new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
        Vector2 TipPos => pos + Dir * (len * 0.5f);
        bool Level => Mathf.Abs(Dir.y) <= 0.22f;   // within ~12 degrees of flat

        // lodged in the world (directly, or on a chain of spears that is): only these carry more spears
        bool Rooted
        {
            get
            {
                var s = this;
                for (int i = 0; i < 12 && s != null; i++)
                {
                    if (s.dead || s.fade >= 0f) return false;
                    if (s.st == St.InBlock) return true;
                    if (s.st != St.OnSpear) return false;
                    s = s.parent;
                }
                return false;
            }
        }

        void Pose()
        {
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, ang);
        }

        bool wasWet = true;

        public override void Tick(float dt)
        {
            life += dt;
            if (VoidStage.Active && !VoidStage.InsideMargin(pos, 3f)) { Kill(); return; }   // thrown out into the void
            bool wetNow = Wd.FluidAtPoint(pos) == Fluids.Water;
            if (wetNow && !wasWet && st == St.Flying) Fluids.Impact(pos, 0.45f, vel);   // a spear knifing into the water
            wasWet = wetNow;
            if (fade >= 0f)
            {
                fade += dt;
                var c = sr.color; c.a = (ghost ? 0.55f : 1f) * (1f - fade / 0.6f);
                sr.color = c;
                if (fade >= 0.6f) { Kill(); return; }
            }
            if (ghost) { TickGhost(dt); if (!dead) Pose(); return; }
            switch (st)
            {
                case St.Flying: Fly(dt); break;
                case St.Falling: Fall(dt); break;
                case St.Returning: Return(dt); break;
                case St.InBlock:
                    if (!Wd.Solid(cellX, cellY)) Drop(Vector2.zero);   // the block went: down it comes
                    else if (boomerang && life > flightT + 0.35f) StartReturn();
                    break;
                case St.OnSpear:
                    if (parent == null || parent.dead || parent.st == St.Falling || parent.st == St.Flying || parent.st == St.Returning) Drop(Vector2.zero);
                    else if (boomerang && life > flightT + 0.35f) StartReturn();
                    break;
                case St.InFoe:
                    if (foe == null || foe.dead) { Drop(foe != null ? foe.vel * 0.3f : Vector2.zero); break; }
                    pos = foe.pos + foeOff;
                    ang = foeAng;
                    if (boomerang && life > flightT + 0.35f) StartReturn();
                    break;
            }
            if (dead) return;
            if (tinted && st != St.Returning && Random.value < (st == St.Flying ? 0.5f : 0.06f))
                G.fx.Burst(pos + Dir * Random.Range(-len * 0.4f, len * 0.4f), 1, tint, 1.2f, 0.35f, burns ? -5f : 4f, 0.9f, false);
            UpdatePlatform();
            Pose();
        }

        // ------------------------------------------------------------------ in flight: straight and fast, a gentle arc

        void Fly(float dt)
        {
            if (life > 5f) { Kill(); return; }
            vel.y -= 9f * dt;
            ang = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;   // the head leads
            if (ghostKidT > 0f)
            {
                ghostKidT -= dt;
                if (ghostKidT <= 0f && owner != null)
                {
                    int n = 1 + (Chance.Roll("splinter", owner.Lucky(0.25f)) ? 1 : 0);
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 d = U.Rot(vel.sqrMagnitude > 0.01f ? vel.normalized : Vector2.right, Random.Range(-40f, 40f));
                        G.Spawn(new Spear(owner, weapon, pos, d * Mathf.Max(14f, vel.magnitude * 0.8f), dmg * 0.4f, true));
                    }
                }
            }
            if (boomerang && life > 0.6f) { StartReturn(); return; }
            Travel(dt, true);
            if (Random.value < 0.3f) G.fx.Burst(pos - Dir * 0.6f * size, 1, new Color(0.9f, 0.85f, 0.7f, 0.6f), 0.3f, 0.2f, 0f);
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
                // a loose boulder: the spear glances off it (a heavy rolling one snaps it, see BoulderBody)
                var bb = BoulderBody.At(tip, Tip);
                if (bb != null) { BounceOff(tip - bb.pos); return; }
                // a foe: one hit, and it wears the spear
                if (HitFoes(tip)) return;
                int cx = Mathf.FloorToInt(tip.x), cy = Mathf.FloorToInt(tip.y);
                Block b = Wd.Get(cx, cy);
                // spikes snap off as it passes
                if (b == Block.Spikes && owner != null && mayBreak)
                {
                    owner.MineCell(cx, cy, 1, vel.normalized);
                    G.fx.Sparks(tip, new Color(0.85f, 0.85f, 0.9f));
                    G.sfx.Material(Block.Spikes, true, 0.8f);
                    continue;
                }
                if (!Wd.Solid(cx, cy)) continue;
                var def = Tiles.Def(b);
                // boulders are too hard and round: it bounces off
                if (b == Block.Boulder) { BounceOff(tip - new Vector2(cx + 0.5f, cy + 0.5f)); return; }
                // chests and crates burst open (it doesn't cost the spear any of its punch)
                if (mayBreak && (b == Block.Chest || b == Block.Crate || b == Block.Supply) && !Wd.Anchored(cx, cy))
                {
                    Wd.Damage(cx, cy, 999, true);
                    G.sfx.Material(b, true, 0.7f);
                    vel *= 0.9f;
                    continue;
                }
                bool hard = def.unbreakable || Wd.Anchored(cx, cy);
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

        bool HitFoes(Vector2 tip)
        {
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en) || en.Intangible || en.team == Team.Player || hit.Contains(en)) continue;
                if (!U.Overlap(tip, Tip, en.pos, en.half)) continue;
                hit.Add(en);
                float d = st == St.Falling ? dmg * 0.6f : dmg;
                en.TakeShot(d, vel.normalized * knock);
                Status(en);
                G.sfx.Play("hit", 0.6f, 0.8f);
                G.fx.Burst(tip, 8, en.blood, 4f, 0.3f, 6f);
                if (ghost) { fade = Mathf.Max(fade, 0f); return true; }
                if (st == St.Returning) continue;
                if (en.dead) { Drop(vel * 0.2f); return true; }
                StickFoe(en);
                return true;
            }
            return false;
        }

        void Status(Enemy en)
        {
            if (en.dead) return;
            if (burns) en.Ignite(3f);
            if (poisons) en.Poison(3.5f, dmg * 0.35f);
            if (freezes) en.Freeze(1.2f);
            owner?.MeleeStatus(en);   // KINDLING, TOXIC SKIN, WILD CARD...
        }

        void BounceOff(Vector2 normal)
        {
            if (normal.sqrMagnitude < 1e-4f) normal = -vel;
            normal.Normalize();
            Vector2 v = Vector2.Reflect(vel, normal) * 0.35f;
            G.sfx.Play("clank", 0.4f, 1.4f);
            G.fx.Sparks(TipPos, new Color(0.9f, 0.85f, 0.7f));
            pos += normal * 0.1f;
            Drop(v);
            spin = Random.Range(-600f, 600f);
        }

        void StickBlock(int cx, int cy, Block b)
        {
            st = St.InBlock; cellX = cx; cellY = cy;
            vel = Vector2.zero; spin = 0f;
            flightT = life;
            G.sfx.Material(b, false, 0.8f);   // a thunk, nothing more: it didn't break anything
            Settled();
        }

        // Another lodged spear in the way (the whole shaft is checked, not just the head, so a spear that starts
        // right next to one still meets it). Only a near-level spear meeting a near-level spear that is itself stuck in
        // the world joins it into one longer ledge. Coming down at any steeper angle, it snaps the lodged one. From below,
        // or against a spear stuck in a foe, it passes by.
        bool HitSpears(Vector2 tip)
        {
            Vector2 tail = pos - Dir * (len * 0.5f);
            float slope = Mathf.Atan2(-vel.y, Mathf.Abs(vel.x)) * Mathf.Rad2Deg;   // > 0 coming down
            bool level = st == St.Flying && Mathf.Abs(slope) <= 12f;
            if (!level && slope <= 12f) return false;   // from below: through it, like a platform
            for (int i = 0; i < All.Count; i++)
            {
                var o = All[i];
                if (o == this || o.dead || o.ghost || o.fade >= 0f || o == parent || (o.st != St.InBlock && o.st != St.OnSpear)) continue;
                Vector2 a = o.pos - o.Dir * (o.len * 0.5f), b = o.pos + o.Dir * (o.len * 0.5f);
                if (level)
                {
                    if (!o.Level || !o.Rooted) continue;
                    // the head meets it (with some slack up and down, so you can actually aim for one)
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(tip - a, ab) / ab.sqrMagnitude);
                    Vector2 near = a + ab * t;
                    if (Mathf.Abs(tip.x - near.x) > 0.2f || Mathf.Abs(tip.y - near.y) > 0.35f) continue;
                    st = St.OnSpear; parent = o;
                    ang = vel.x >= 0f ? 0f : 180f;
                    pos = new Vector2(near.x, o.pos.y) - Dir * (len * 0.5f - 0.1f);
                    vel = Vector2.zero; spin = 0f;
                    flightT = life;
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

        // snapped (by a spear coming down on it, or a boulder rolling over it)
        public void Snap()
        {
            if (dead) return;
            G.fx.Burst(pos, 10, new Color(0.75f, 0.6f, 0.4f), 4f, 0.4f, 12f);
            G.sfx.Play("break", 0.5f, 1.4f);
            Kill();
        }

        // does this spear's shaft touch a box? (boulders use it to snap spears they roll over)
        public bool Touches(Vector2 c, Vector2 h)
        {
            Vector2 a = pos - Dir * (len * 0.5f), b = pos + Dir * (len * 0.5f);
            for (int i = 0; i <= 6; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / 6f);
                if (Mathf.Abs(p.x - c.x) <= h.x && Mathf.Abs(p.y - c.y) <= h.y) return true;
            }
            return false;
        }

        void StickFoe(Enemy en)
        {
            st = St.InFoe; foe = en;
            // sink the head a little way into it
            pos += Dir * 0.15f;
            foeOff = pos - en.pos; foeAng = ang;
            vel = Vector2.zero; spin = 0f;
            flightT = life;
            Settled();
        }

        // too many spears lying about: the oldest lodged one crumbles away
        void Settled()
        {
            int n = 0;
            Spear oldest = null;
            foreach (var s in All)
            {
                if (s.dead || s.ghost || s.st == St.Flying || s.st == St.Falling || s.st == St.Returning || s.fade >= 0f || s.st == St.OnSpear) continue;
                n++;
                if (oldest == null || s.life > oldest.life) oldest = s;
            }
            if (n > MaxStuck && oldest != null) oldest.fade = 0f;
        }

        // ------------------------------------------------------------------ CURSE OF THE BOOMERANG: back to your hand

        void StartReturn()
        {
            st = St.Returning;
            foe = null; parent = null;
            hit.Clear();
            IsPlatform = false;
            G.sfx.Play("swing", 0.4f, 0.8f);
        }

        void Return(float dt)
        {
            if (owner == null || owner.mode == Player.Mode.Dead) { fade = Mathf.Max(fade, 0f); return; }
            Vector2 to = owner.pos - pos;
            float d = to.magnitude;
            if (d < 0.7f)
            {
                // caught: the spear goes back in the quiver
                if (weapon != null && owner.MagSize(weapon) > 0) weapon.ammo = Mathf.Min(owner.MagSize(weapon), Mathf.Max(0, weapon.ammo) + 1);
                G.sfx.Play("reload", 0.4f, 1.4f);
                Kill();
                return;
            }
            vel = Vector2.MoveTowards(vel, to / d * 22f, 80f * dt);
            ang = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
            pos += vel * dt;
            HitFoes(TipPos);   // it still cuts through whatever is in the way (through rock, too)
            if (life > 12f) Kill();
        }

        // ------------------------------------------------------------------ SOUL SPLINTERS: ghost spears

        void TickGhost(float dt)
        {
            if (life > 1.4f && fade < 0f) fade = 0f;
            Enemy best = null; float bd = 9f * 9f;
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en) || en.Intangible || en.team == Team.Player || hit.Contains(en)) continue;
                float d2 = (en.pos - pos).sqrMagnitude;
                if (d2 < bd) { bd = d2; best = en; }
            }
            if (best != null) vel = Vector3.RotateTowards(vel, (best.pos - pos).normalized * vel.magnitude, 6f * dt, 0f);
            ang = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
            pos += vel * dt;
            if (fade < 0f) HitFoes(TipPos);
            if (Random.value < 0.4f) G.fx.Burst(pos - Dir * 0.5f * size, 1, new Color(0.75f, 0.85f, 1f, 0.6f), 0.4f, 0.3f, 0f);
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
            int cx = Mathf.FloorToInt(pos.x), cy = Mathf.FloorToInt(pos.y - 0.12f);
            if (Wd.Solid(cx, cy))
            {
                if (Wd.Get(cx, cy) == Block.Boulder || BoulderBody.At(pos, Tip) != null)
                {
                    // never rests on a boulder: it skitters off
                    if (++bounces > 4) { fade = Mathf.Max(fade, 0f); return; }
                    pos.y = cy + 1 + 0.12f;
                    vel = new Vector2((pos.x > cx + 0.5f ? 1f : -1f) * 3f, 4f);
                    return;
                }
                pos.y = cy + 1 + 0.12f;
                StickBlock(cx, cy, Wd.Get(cx, cy));
            }
        }

        // ------------------------------------------------------------------ standing on it

        void UpdatePlatform()
        {
            IsPlatform = false;
            if (fade >= 0f || ghost) return;
            if (st == St.OnSpear)
            {
                // the whole shaft is ledge (it lies level, against the one it's lodged on)
                Vector2 dd = Dir;
                float ax = pos.x - dd.x * len * 0.5f, bx = pos.x + dd.x * len * 0.5f;
                platX0 = Mathf.Min(ax, bx); platX1 = Mathf.Max(ax, bx);
                platTop = pos.y + 0.06f * size;
                IsPlatform = DryPart();
                return;
            }
            if (st != St.InBlock) return;
            Vector2 d = Dir;
            if (Mathf.Abs(d.y) > 0.64f) return;   // only a spear lodged roughly sideways makes a ledge
            Vector2 tail = pos - d * (len * 0.5f);
            float face = d.x > 0f ? cellX : cellX + 1f;   // where it enters the wall
            platX0 = Mathf.Min(tail.x, face);
            platX1 = Mathf.Max(tail.x, face);
            if (platX1 - platX0 < 0.2f) return;
            float midX = (platX0 + platX1) * 0.5f;
            float t = Mathf.Abs(d.x) > 0.01f ? (midX - pos.x) / d.x : 0f;
            platTop = pos.y + d.y * t + 0.06f * size;
            IsPlatform = DryPart();
        }

        // under water a spear is no ledge (you swim past it): only the part of it above the surface holds you
        bool DryPart()
        {
            float y = platTop + 0.08f;
            if (Wd.LiquidType(Mathf.FloorToInt(platX0), Mathf.FloorToInt(y)) == Fluids.None && Wd.LiquidType(Mathf.FloorToInt(platX1), Mathf.FloorToInt(y)) == Fluids.None) return true;
            float best0 = 0f, best1 = 0f, run0 = float.NaN;
            const float stepX = 0.125f;
            for (float x = platX0; x <= platX1 + 0.001f; x += stepX)
            {
                bool dry = Wd.FluidAtPoint(new Vector2(Mathf.Min(x, platX1), y)) != Fluids.Water;
                if (dry && float.IsNaN(run0)) run0 = x;
                if ((!dry || x + stepX > platX1 + 0.001f) && !float.IsNaN(run0))
                {
                    float end = dry ? platX1 : x;
                    if (end - run0 > best1 - best0) { best0 = run0; best1 = end; }
                    run0 = float.NaN;
                }
            }
            if (best1 - best0 < 0.2f) return false;
            platX0 = best0; platX1 = best1;
            return true;
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

        StuckArrow(Vector2 at, Vector2 dir, Sprite s, Vector2Int cell, float scale)
        {
            pos = at; this.cell = cell;
            ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            MakeGO("Arrow", s, 8);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            All.Add(this);
            if (All.Count > Max) All[0].fade = 0f;
        }

        public static void Lodge(Vector2 at, Vector2 dir, Sprite s, Vector2Int cell, float scale = 1f)
        {
            if (s == null || dir.sqrMagnitude < 0.01f) return;
            // set it exactly where the flight line enters that block, head a little way in
            Vector2 d = dir.normalized;
            Vector2 from = at - d * 1.5f;
            float tIn = 0f, tOut = 3f;
            for (int ax = 0; ax < 2; ax++)
            {
                float o = ax == 0 ? from.x : from.y, v = ax == 0 ? d.x : d.y;
                float lo = ax == 0 ? cell.x : cell.y, hi = lo + 1f;
                if (Mathf.Abs(v) < 1e-5f) { if (o < lo || o > hi) return; continue; }
                float t1 = (lo - o) / v, t2 = (hi - o) / v;
                if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                tIn = Mathf.Max(tIn, t1); tOut = Mathf.Min(tOut, t2);
            }
            Vector2 entry = tIn <= tOut ? from + d * tIn : at;
            Game.I.Spawn(new StuckArrow(entry - d * 0.08f * scale, dir, s, cell, scale));
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
