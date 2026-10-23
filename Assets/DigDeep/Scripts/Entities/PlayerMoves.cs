using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Movement specials (Shift) and movement skills: dash, swinging rope, quick dig, ground slam, wall jump.
    public partial class Player
    {
        float ropeLen, aegisT, poundHang, digFrom;
        const float RopeTime = 3f;   // you can only hang on for 3 seconds
        int ropeWarn;
        Vector2Int ropeCell;
        int wallDir;   // -1 / +1 while touching a wall in the air (for WALL JUMP), 0 otherwise
        readonly HashSet<Enemy> robbed = new HashSet<Enemy>();

        // ------------------------------------------------------------------ DASH

        void StartDash(float mx, bool up, bool down)
        {
            if (mode == Mode.Grapple) EndGrapple();
            Vector2 d = new Vector2(mx, (up ? 1f : 0f) - (down ? 1f : 0f));
            if (d.sqrMagnitude < 0.01f) d = new Vector2(facing, 0f);
            quickDir = d.normalized;
            mode = Mode.Quick;
            modeT = 0.14f;
            ghostT = 0f;
            squash = -0.2f;
            robbed.Clear();
            G.sfx.Play("dashq", 0.8f);
            G.fx.Burst(pos, 10, new Color(0.75f, 0.6f, 1f), 5f, 0.3f, 0f);
            G.fx.Smoke(pos - new Vector2(0f, Half.y), 4, 1f);
            G.cam.Kick(quickDir * 0.25f);
        }

        // PICKPOCKET: dashing through a foe lifts some gold off it
        void Pickpocket()
        {
            if (!s.pickpocket) return;
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en) || en is Boss || en is WormSegment || robbed.Contains(en)) continue;
                if (!U.Overlap(pos, Half + new Vector2(0.2f, 0.2f), en.pos, en.half)) continue;
                robbed.Add(en);
                G.SpawnGold(en.pos + Vector2.up * 0.3f, 3);
                G.sfx.Play("coin", 0.7f, 1.3f);
                G.Popup(en.pos + Vector2.up * 0.8f, "PICKPOCKET!", new Color(1f, 0.85f, 0.3f));
            }
        }

        // ------------------------------------------------------------------ SWINGING ROPE

        bool StartSwing()
        {
            Vector2 dir = aimDir;
            if (dir.y < 0.25f) dir = new Vector2((Mathf.Abs(dir.x) > 0.05f ? Mathf.Sign(dir.x) : facing) * 0.55f, 0.85f).normalized;
            float d = Wd.RayDist(pos, dir, 11f);
            if (d >= 11f)
            {
                G.sfx.Play("deny", 0.4f, 1.3f);
                G.Popup(pos + Vector2.up, "NOTHING TO HOLD ON TO", new Color(0.85f, 0.75f, 0.55f));
                return false;
            }
            if (mode == Mode.Grapple) EndGrapple();
            anchor = pos + dir * d;
            Vector2 inside = pos + dir * (d + 0.08f);
            ropeCell = new Vector2Int(Mathf.FloorToInt(inside.x), Mathf.FloorToInt(inside.y));
            ropeLen = Mathf.Clamp(d * 0.92f, 1.2f, 10f);
            mode = Mode.Swing;
            modeT = RopeTime;
            ropeWarn = 0;
            grounded = false;
            G.sfx.Play("grapple", 0.6f, 0.8f);
            G.fx.Burst(anchor, 6, new Color(0.85f, 0.75f, 0.55f), 3f, 0.3f, 8f);
            return true;
        }

        void TickSwing(float dt, float mx, bool up, bool down)
        {
            modeT -= dt;
            if (!Wd.Solid(ropeCell.x, ropeCell.y) || modeT <= 0f) { EndSwing(false); return; }
            if (GameInput.Take(Btn.Jump) || GameInput.Take(Btn.Special)) { EndSwing(true); return; }

            vel.y = Mathf.Max(vel.y - 38f * dt, -22f);
            vel.x += mx * 16f * dt;                                       // pump to swing higher
            ropeLen = Mathf.Clamp(ropeLen + ((down ? 5f : 0f) - (up ? 5f : 0f)) * dt, 1.2f, 10f);   // climb up / slide down

            Vector2 np = pos + vel * dt;
            Vector2 r = np - anchor;
            if (r.magnitude > ropeLen)
            {
                Vector2 n = r.normalized;
                np = anchor + n * ropeLen;
                float rv = Vector2.Dot(vel, n);
                if (rv > 0f) vel -= n * rv;   // the rope is taut: only the swing remains
            }
            Vector2 delta = np - pos, p = pos;
            if (Wd.MoveX(ref p, Half, delta.x, out _)) vel.x *= -0.25f;
            if (Wd.MoveY(ref p, Half, delta.y, out _)) vel.y = 0f;
            pos = p;
            grounded = false;
            DrawRope(pos + Vector2.up * 0.1f, anchor);
            // the last second: the rope creaks three times, faster and higher, and flashes red
            if (modeT < 1f)
            {
                int step = modeT < 0.25f ? 3 : modeT < 0.6f ? 2 : 1;
                if (step > ropeWarn)
                {
                    ropeWarn = step;
                    G.sfx.Play("blip", 0.7f, 0.9f + step * 0.3f, 0f);
                    G.sfx.Play("chip", 0.5f, 0.6f + step * 0.15f, 0f);
                    G.fx.Burst(anchor, 3, new Color(0.85f, 0.75f, 0.55f), 2f, 0.3f, 10f);
                }
                ropeSr.color = ((int)(modeT * 14f)) % 2 == 0 ? new Color(1f, 0.35f, 0.3f) : new Color(0.85f, 0.75f, 0.55f);
            }
            else ropeSr.color = new Color(0.85f, 0.75f, 0.55f);
            if (Random.value < vel.magnitude * dt * 0.6f) G.fx.Ghost(sr.sprite, go.transform.position, sr.flipX, new Color(0.9f, 0.8f, 0.6f, 0.35f));

            TickWeapon(dt);
            if (GameInput.Take(Btn.Swap)) SwapWeapon();
            if (GameInput.Take(Btn.Item)) UseItem();
        }

        void EndSwing(bool jumped)
        {
            mode = Mode.Normal;
            ropeSr.enabled = false;
            ropeSr.color = new Color(0.85f, 0.75f, 0.55f);
            if (!jumped && modeT <= 0f) { G.sfx.Play("swing", 0.6f, 0.6f); G.fx.Burst(anchor, 6, new Color(0.85f, 0.75f, 0.55f), 3f, 0.4f, 12f); }   // the rope snaps loose
            airJumps = s.airJumps;
            if (jumped)
            {
                vel = new Vector2(vel.x * 1.1f, Mathf.Max(vel.y, 0f) + 7f * JumpMul);
                G.sfx.Play("jump", 0.6f, 1.1f);
            }
        }

        // ------------------------------------------------------------------ QUICK DIG

        void StartDig()
        {
            if (mode == Mode.Grapple) EndGrapple();
            mode = Mode.Dig;
            modeT = 0.45f;
            digFrom = pos.y;
            pos.x = Mathf.FloorToInt(pos.x) + 0.5f;   // line up with one column
            vel = Vector2.zero;
            squash = 0.25f;
            G.sfx.Play("dash", 0.8f, 0.8f);
            G.fx.Burst(pos, 10, new Color(1f, 0.6f, 0.2f), 4f, 0.3f, 0f);
        }

        void TickDig(float dt)
        {
            modeT -= dt;
            vel = new Vector2(0f, -22f);
            bool blocked = false;
            BreakBox(pos + vel * dt, Half, Mathf.Max(1, s.digPower) * 8, ref blocked);
            G.HitEnemies(pos, Half + new Vector2(0.2f, 0.2f), Dmg(3f), Vector2.down * 4f + Vector2.right * facing * 3f);
            Vector2 p = pos;
            bool hit = Wd.MoveY(ref p, Half, vel.y * dt, out _);
            pos = p;
            G.fx.Burst(pos + Vector2.up * 0.4f, 2, new Color(1f, 0.6f, 0.2f), 1.5f, 0.25f, 0f);
            if (Random.value < 0.5f) G.fx.Ghost(sr.sprite, go.transform.position, sr.flipX, new Color(1f, 0.6f, 0.3f, 0.5f));
            bool deepEnough = pos.y <= digFrom - 6f;   // quick dig goes 6 blocks down
            if (blocked || hit || deepEnough || modeT <= 0f)
            {
                if (blocked) { G.sfx.Play("clank", 0.5f); G.cam.Shake(0.1f); }
                mode = Mode.Normal;
                vel = new Vector2(0f, -3f);
                grace = Mathf.Max(grace, 0.15f);
            }
        }

        // ------------------------------------------------------------------ GROUND SLAM (skill: press down in the air)

        bool HighEnoughToSlam()
        {
            Vector2 feet = pos - new Vector2(0f, Half.y);
            return Wd.RayDist(feet, Vector2.down, 1.6f) >= 1.5f;
        }

        void StartPound()
        {
            mode = Mode.Pound;
            modeT = 2f;
            poundHang = 0.08f;
            vel = Vector2.zero;
            squash = -0.3f;
            G.sfx.Play("swing", 0.6f, 0.6f);
        }

        void TickPound(float dt)
        {
            modeT -= dt;
            if (poundHang > 0f) { poundHang -= dt; return; }   // a tiny hang in the air before the drop
            vel = new Vector2(0f, -28f);
            G.HitEnemies(pos, Half + new Vector2(0.2f, 0.2f), Dmg(3f), Vector2.down * 4f);
            G.fx.Burst(pos + Vector2.up * 0.4f, 1, new Color(1f, 1f, 1f, 0.7f), 0.5f, 0.2f, 0f);
            Vector2 p = pos;
            bool landed = Wd.MoveY(ref p, Half, vel.y * dt, out _);
            pos = p;
            if (!landed && modeT > 0f && Wd.FluidAtPoint(pos) != Fluids.Water) return;
            mode = Mode.Normal;
            if (landed)
            {
                int fy = Mathf.FloorToInt(pos.y - Half.y - 0.1f);
                for (int x = Mathf.FloorToInt(pos.x - Half.x + E); x <= Mathf.FloorToInt(pos.x + Half.x - E); x++) Wd.Impact(x, fy, 28f);
                Shockwave(pos - new Vector2(0, Half.y - 0.3f), Combo("aftershock2") ? 3.6f : 2.4f, Dmg(3f) * s.meleeMul, 2, Src.Pound);
                G.cam.Shake(0.35f);
            }
            vel = new Vector2(0f, 4f);
            squash = 0.35f;
            grounded = false;
        }

        // ------------------------------------------------------------------ WALL JUMP (skill)

        // Called from TickNormal after gravity. Returns true if it used the jump press.
        bool WallMoves(float mx)
        {
            wallDir = 0;
            if (!s.wallJump || grounded) return false;
            if (Wd.BoxSolid(pos + new Vector2(0.06f, 0f), new Vector2(Half.x, Half.y * 0.8f))) wallDir = 1;
            else if (Wd.BoxSolid(pos + new Vector2(-0.06f, 0f), new Vector2(Half.x, Half.y * 0.8f))) wallDir = -1;
            if (wallDir == 0) return false;
            // pressing into the wall: slide down it slowly
            if (mx == wallDir && vel.y < -3f)
            {
                vel.y = -3f;
                if (Random.value < 0.3f) G.fx.Burst(pos + new Vector2(wallDir * Half.x, -0.2f), 1, new Color(0.7f, 0.6f, 0.5f), 1f, 0.3f, 6f);
            }
            if (!GameInput.Buffered(Btn.Jump) || (coyote > 0f && mx != wallDir)) return false;
            GameInput.Consume(Btn.Jump);
            vel = new Vector2(-wallDir * 8.5f, 15f * JumpMul);
            knockT = 0.16f;   // a moment where you can't steer straight back into the wall
            squash = -0.22f;
            if (Combo("parkour")) airJumps = s.airJumps;
            G.sfx.Play("jump", 0.6f, 1.2f);
            G.fx.Burst(pos + new Vector2(wallDir * Half.x, 0f), 6, new Color(0.7f, 0.6f, 0.5f), 3f, 0.3f, 5f);
            return true;
        }

        void TickAegis(float dt)
        {
            if (aegisT <= 0f) return;
            aegisT -= dt;
            if (Random.value < 0.5f) G.fx.Burst(pos + Random.insideUnitCircle * 0.4f, 1, new Color(1f, 0.9f, 0.4f), 1f, 0.3f, 0f);
        }

        // ------------------------------------------------------------------ chests and crates: walk up, press E

        void TreasureNearby()
        {
            int cx = Mathf.FloorToInt(pos.x), cy = Mathf.FloorToInt(pos.y);
            float best = 1.6f * 1.6f;
            Vector2Int hit = new Vector2Int(int.MinValue, 0);
            for (int y = cy - 1; y <= cy + 1; y++)
                for (int x = cx - 1; x <= cx + 1; x++)
                {
                    Block b = Wd.Get(x, y);
                    if (b != Block.Chest && b != Block.Crate) continue;
                    float d = (new Vector2(x + 0.5f, y + 0.5f) - pos).sqrMagnitude;
                    if (d < best) { best = d; hit = new Vector2Int(x, y); }
                }
            if (hit.x == int.MinValue) return;
            bool chest = Wd.Get(hit.x, hit.y) == Block.Chest;
            G.hud.Label(new Vector2(hit.x + 0.5f, hit.y + 1.6f), chest ? "E: OPEN CHEST" : "E: OPEN WEAPON CRATE",
                chest ? new Color(1f, 0.85f, 0.35f) : (Color)DB.RarityColor(Crates.RarityAt(hit.x, hit.y)));
            if (GameInput.Take(Btn.Interact))
            {
                Wd.Break(hit.x, hit.y, true, GoldMul);
                G.cam.Shake(0.15f);
            }
        }

        // ------------------------------------------------------------------ KICK (Tor's special): no charge, a fixed 1 s cooldown, long reach

        void DoKick()
        {
            float dir = Mathf.Abs(aimDir.x) > 0.1f ? Mathf.Sign(aimDir.x) : facing;
            facing = (int)dir;
            // a long boot: it reaches well past arm's length, so you can kick foes before they touch you
            Vector2 c = pos + new Vector2(dir * 1.35f, -0.05f), h = new Vector2(1.25f, 0.7f);
            float km = s.meleeMul * s.kickMul;   // BRASS KNUCKLES, STEEL TOE CAPS
            bool any = false;
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en) || en.Intangible || !U.Overlap(c, h, en.pos, en.half)) continue;
                any = true;
                en.TakeShot(Dmg(2.5f) * km * Backstab(en), new Vector2(dir * 4f, 2f));
                if (!en.dead) en.Launch(new Vector2(dir * 20f, 7f) * Mathf.Min(1.5f, Mathf.Sqrt(s.kickMul)), Dmg(2f) * km);
            }
            for (int i = BoulderBody.All.Count - 1; i >= 0; i--)
            {
                var bb = BoulderBody.All[i];
                if (U.Overlap(c, h, bb.pos, BoulderBody.H)) { bb.Push(new Vector2(dir * 16f, 4f)); any = true; }
            }
            int x0 = Mathf.FloorToInt(c.x - h.x), x1 = Mathf.FloorToInt(c.x + h.x), y0 = Mathf.FloorToInt(c.y - h.y), y1 = Mathf.FloorToInt(c.y + h.y);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (Wd.PushBoulder(x, y, new Vector2(dir * 16f, 3f))) any = true;
            // the boot smashes the blocks in front of you too (never the floor you stand on)
            Vector2 bc = pos + new Vector2(dir * (Half.x + 0.9f), 0.04f), bh = new Vector2(0.9f, Half.y - 0.06f);
            bool blockedK = false;
            if (BreakBox(bc, bh, Mathf.Max(2, s.digPower * 3), ref blockedK)) any = true;
            G.HitAltars(c, h, Src.Pick);
            squash = -0.25f;
            vel.x += dir * 3f;
            G.fx.Burst(c, 10, new Color(1f, 0.9f, 0.7f), 6f, 0.25f, 0f);
            G.fx.Smoke(pos - new Vector2(0f, Half.y), 3, 0.8f);
            G.sfx.Play("swing", 0.7f, 0.7f);
            if (any)
            {
                G.sfx.Play("hammer", 0.8f, 1.3f);
                G.HitStop(0.06f);
                G.cam.Shake(0.3f);
                G.cam.Punch(0.2f);
                G.cam.Kick(new Vector2(dir * 0.25f, 0f));
            }
            else G.cam.Shake(0.08f);
        }

        // ------------------------------------------------------------------ safety: never get stuck inside a block

        // If something solid ended up where you stand (a block settling, a pushed boulder...), step out to the
        // nearest free spot instead of letting the collision code shove you through walls.
        // chests (and crates) never shove you about: you can stand inside one that lands on you and walk out
        static bool StuckIn(Vector2 p, Vector2 h)
        {
            int x0 = Mathf.FloorToInt(p.x - h.x + E), x1 = Mathf.FloorToInt(p.x + h.x - E);
            int y0 = Mathf.FloorToInt(p.y - h.y + E), y1 = Mathf.FloorToInt(p.y + h.y - E);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var b = Wd.Get(x, y);
                    if (b == Block.Chest || b == Block.Crate) continue;
                    if (Wd.Solid(x, y)) return true;
                }
            return false;
        }

        void Unstick()
        {
            if (!StuckIn(pos, Half * 0.9f)) return;
            for (float r = 0.25f; r <= 2.01f; r += 0.25f)
                for (int k = 0; k < 8; k++)
                {
                    // try up and the sides first, down last
                    Vector2 dir = k switch
                    {
                        0 => Vector2.up, 1 => Vector2.left, 2 => Vector2.right, 3 => new Vector2(-1f, 1f).normalized,
                        4 => new Vector2(1f, 1f).normalized, 5 => new Vector2(-1f, -1f).normalized, 6 => new Vector2(1f, -1f).normalized, _ => Vector2.down,
                    };
                    Vector2 q = pos + dir * r;
                    if (!StuckIn(q, Half)) { pos = q; vel = Vector2.zero; return; }
                }
        }

        // ------------------------------------------------------------------ burning (you)

        public float burnT;
        float burnTick, poisonT, poisonTick;
        public bool Burning => burnT > 0f;
        public bool Poisoned => poisonT > 0f;

        public void SetOnFire()
        {
            if (mode == Mode.Dead || s.lavaImmune || s.fireImmune) return;
            if (Wd.FluidAtPoint(pos) == Fluids.Water) return;
            if (burnT <= 0f) { G.sfx.Play("ignite", 0.8f, 1.2f); G.Popup(pos + Vector2.up, "ON FIRE!", new Color(1f, 0.55f, 0.2f)); burnTick = 0.25f; }
            burnT = 3f;
        }

        public void Extinguish()
        {
            if (burnT <= 0f) return;
            burnT = 0f;
            G.sfx.Play("fizz", 0.7f);
            G.fx.Smoke(pos, 6, 1f);
        }

        public void Poison(float t)
        {
            if (mode == Mode.Dead || s.poisonImmune) return;
            if (poisonT <= 0f) { G.Popup(pos + Vector2.up, "POISONED", new Color(0.5f, 1f, 0.35f)); poisonTick = 0.5f; }
            poisonT = Mathf.Max(poisonT, t);
        }

        // Damage over time: no knockback or mercy time, just health ticking away.
        void TickBurnAndPoison(float dt)
        {
            if (burnT > 0f)
            {
                // water, a fast dash or swing, or a load of sand on your head puts it out
                if (Wd.FluidAtPoint(pos) == Fluids.Water || Wd.FluidAtPoint(pos - new Vector2(0f, Half.y - 0.1f)) == Fluids.Water) Extinguish();
                else if ((mode == Mode.Quick || mode == Mode.Swing) && vel.magnitude > 12f) Extinguish();
                else
                {
                    burnT -= dt;
                    burnTick -= dt;
                    if (burnTick <= 0f) { burnTick = 0.5f; DotDamage(Mathf.RoundToInt(4f + Depth * 0.05f), new Color(1f, 0.55f, 0.2f)); }
                    Fire.IgniteBox(pos, Half + new Vector2(0.08f, 0.08f));   // you set alight whatever you touch
                    Fire.MeltAround(pos, 1.2f);
                    if (Random.value < 0.6f) G.fx.Burst(pos + new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f)), 1, new Color(1f, Random.Range(0.4f, 0.9f), 0.15f), 1.5f, 0.4f, -6f, 1f, false);
                    if (burnT <= 0f) { G.sfx.Play("fizz", 0.4f, 1.3f); G.fx.Smoke(pos, 3, 0.7f); }
                }
            }
            if (poisonT > 0f)
            {
                poisonT -= dt;
                poisonTick -= dt;
                if (poisonTick <= 0f) { poisonTick = 0.5f; DotDamage(Mathf.RoundToInt(3f + Depth * 0.03f), new Color(0.5f, 1f, 0.35f)); }
                if (Random.value < 0.25f) G.fx.Burst(pos, 1, new Color(0.5f, 1f, 0.35f), 1f, 0.5f, -3f, 1f, false);
            }
        }

        void DotDamage(int d, Color c)
        {
            if (mode == Mode.Dead) return;
            if (s.armor > 0f) d = Mathf.Max(1, Mathf.RoundToInt(d * (1f - s.armor)));
            s.hp -= d;
            EndCamo();
            G.Popup(pos + Vector2.up * 0.9f, "-" + d, c);
            G.sfx.Play("hurt", 0.3f, 1.4f);
            if (s.hp <= 0) { s.hp = 1; invuln = 0f; grace = 0f; Hurt(1, pos); }
        }
    }
}
