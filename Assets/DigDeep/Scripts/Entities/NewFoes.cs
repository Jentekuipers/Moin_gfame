using UnityEngine;

namespace DigDeep
{
    // =====================================================================================
    // Creatures drawn by hand: slimes, turtles, skeletons, glider bats, wolves, rock mages, the lantern-shroom elite,
    // and the void mages of the void world. Plus a one-shot animation and the void spikes.
    // =====================================================================================

    // Plays a strip of frames once where something happened (a bat's death, a coin being picked up), then vanishes.
    public class FrameAnim : Entity
    {
        readonly Sprite[] frames;
        readonly float fps;
        float t;
        Vector2 vel;
        readonly float grav;

        public FrameAnim(Vector2 at, Sprite[] frames, float fps, bool flip, int order = 12, Vector2 vel = default, float grav = 0f)
        {
            pos = at; this.frames = frames; this.fps = fps; this.vel = vel; this.grav = grav;
            MakeGO("Anim", frames[0], order);
            sr.flipX = flip;
        }

        public override bool AlwaysActive => true;

        public override void Tick(float dt)
        {
            t += dt;
            int i = (int)(t * fps);
            if (i >= frames.Length) { Kill(); return; }
            vel.y -= grav * dt;
            pos += vel * dt;
            sr.sprite = frames[i];
            Place();
        }
    }

    // A loose bone flying off a smashed skeleton; it bounces, settles and fades.
    public class BoneBit : Entity
    {
        Vector2 vel;
        float spin, life = 6f;
        static readonly Vector2 H = new Vector2(0.15f, 0.12f);

        public BoneBit(Vector2 at, Vector2 v)
        {
            pos = at; vel = v;
            MakeGO("Bone", Art.Bones[Random.Range(0, Art.Bones.Length)], 9);
            spin = Random.Range(-600f, 600f);
        }

        public override void Tick(float dt)
        {
            life -= dt;
            if (life <= 0f) { Kill(); return; }
            vel.y = Mathf.Max(vel.y - 30f * dt, -15f);
            Vector2 p = pos;
            if (Wd.MoveX(ref p, H, vel.x * dt, out _)) vel.x *= -0.4f;
            if (Wd.MoveY(ref p, H, vel.y * dt, out _)) { vel.y *= -0.3f; vel.x *= 0.6f; spin *= 0.5f; }
            pos = p;
            go.transform.rotation = Quaternion.Euler(0f, 0f, go.transform.eulerAngles.z + spin * dt);
            if (life < 1f) sr.color = new Color(1f, 1f, 1f, life);
            Place();
        }

        public static void Scatter(Vector2 at, int n)
        {
            for (int i = 0; i < n; i++) G.Spawn(new BoneBit(at, new Vector2(Random.Range(-5f, 5f), Random.Range(4f, 10f))));
        }
    }

    // SLIMES: the water slimes, now hand-drawn. They wobble along, stretch tall as they fall and squash when they land.
    public class Slime : Walker
    {
        float fallT, landT, hitT, jumpCd = 1.5f, ledgeT;
        bool wasGround = true, leaping, braveLedges;

        public Slime(int x, int y, int depth)
        {
            half = new Vector2(0.5f, 0.28f);
            pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
            Setup("Slime", "slime2", 2, depth);
            waterLike = true;
            blood = new Color(0.25f, 0.75f, 0.65f);
            gold = 2;
            facing = Random.value < 0.5f ? -1 : 1;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(7f, false, dt);
            float sp = 1.6f * (1f + depth / 500f);
            if (HasTarget && Mathf.Abs(TPos.y - pos.y) < 1f && Mathf.Abs(TPos.x - pos.x) < 5.5f) { facing = TPos.x > pos.x ? 1 : -1; sp *= 1.5f; }
            // now and then it leaps at you
            jumpCd -= dt;
            if (HasTarget && onGround && jumpCd <= 0f && Mathf.Abs(TPos.x - pos.x) < 6f && TPos.y > pos.y - 3f)
            {
                jumpCd = Random.Range(1.8f, 3.5f);
                if (Random.value < 0.6f)
                {
                    facing = TPos.x > pos.x ? 1 : -1;
                    vel = new Vector2(facing * Mathf.Clamp(Mathf.Abs(TPos.x - pos.x) * 1.6f, 3f, 7.5f), Random.Range(8f, 11f));
                    leaping = true;
                    G.sfx.Play("splat", 0.3f, 1.5f);
                }
            }
            if (onGround && vel.y <= 0f) leaping = false;
            // it doesn't mind dropping off a ledge (chasing you it never stops at one; wandering, now and then)
            ledgeT -= dt;
            if (ledgeT <= 0f) { ledgeT = Random.Range(2f, 5f); braveLedges = Random.value < 0.4f; }
            WalkMove(leaping ? Mathf.Abs(vel.x) : sp, dt, HasTarget, !(HasTarget || braveLedges || leaping));
            if (!onGround) fallT += dt; else fallT = 0f;
            if (onGround && !wasGround) landT = 0.3f;
            wasGround = onGround;
            landT -= dt;
            Draw(0f);
            if (dead) return;
            // the falling stretch, the landing squash and a jelly wobble when hit
            if (flash > 0.06f) hitT = 0.3f;
            hitT -= dt;
            Sprite over = hitT > 0f ? StripFrame("slime_hit", (int)((0.3f - hitT) * 16f))
                : fallT > 0.12f ? StripFrame("slime_fall", Mathf.Min(1, (int)(fallT * 6f)))
                : landT > 0f ? StripFrame("slime_fall", 2 + Mathf.Clamp((int)((0.3f - landT) * 10f), 0, 2)) : null;
            if (over != null) sr.sprite = over;
        }
    }

    // TURTLE: plods along. When you come near on its level it pulls into its shell and shoots across the floor,
    // bouncing off walls. Bullets glance off a sliding shell (it takes a third).
    public class Turtle : Walker
    {
        float slideT, coolT = 1.5f;
        int bounces;

        public Turtle(int x, int y, int depth)
        {
            half = new Vector2(0.5f, 0.32f);
            pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
            Setup("Turtle", "turtle", 5, depth);
            gold = 4;
            blood = new Color(0.4f, 0.7f, 0.5f);
            facing = Random.value < 0.5f ? -1 : 1;
        }

        public override bool Hurt(float d, Vector2 knock) => base.Hurt(slideT > 0f ? d * 0.33f : d, slideT > 0f ? Vector2.zero : knock);

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(8f, true, dt);
            coolT -= dt;
            if (slideT > 0f)
            {
                slideT -= dt;
                float before = facing;
                WalkMove(11f, dt, false, false);
                if (facing != before) { bounces++; G.sfx.Play("land", 0.4f, 1.6f); G.cam.Shake(0.05f); if (bounces >= 3) slideT = 0f; }
                if (Random.value < 0.4f) G.fx.Burst(pos + new Vector2(0f, -half.y), 1, new Color(0.7f, 0.6f, 0.45f), 2f, 0.25f, 10f);
                if (slideT <= 0f) coolT = 2.5f;
                Draw(0f);
                if (!dead) { var sl = StripFrame("turtle_slide", ((int)(animT * 14f)) % 7); if (sl != null) sr.sprite = sl; }
                return;
            }
            float sp = 0.9f;
            if (HasTarget && coolT <= 0f && Mathf.Abs(TPos.y - pos.y) < 0.8f && Mathf.Abs(TPos.x - pos.x) < 7f)
            {
                facing = TPos.x > pos.x ? 1 : -1;
                slideT = 2.2f; bounces = 0;
                G.sfx.Play("dash", 0.6f, 1.2f);
            }
            WalkMove(sp, dt, false);
            Draw(0f);
        }
    }

    // SKELETON: bones buried in a dirt block. Dig the dirt out and it pulls itself together, shambles after you and
    // punches. While it rises it can't be hurt until it's nearly up, and it can't hurt you until it's fully up.
    // Smashed, it falls apart into bones. (One that isn't buried wakes when you come close.)
    public class Skeleton : Walker
    {
        bool awake, buried;
        int bx, by;
        float wakeT, punchT, punchCd = 1f;
        bool punched;
        const float WakeTime = 1.1f, HurtableAt = 0.3f;
        protected override bool Harmless => !awake || wakeT > 0f;

        // sink it into a dirt block where it stands
        public void Bury()
        {
            bx = Mathf.FloorToInt(pos.x); by = Mathf.FloorToInt(pos.y);
            if (Wd.Get(bx, by) != Block.Air) return;
            Wd.Set(bx, by, Block.Dirt);
            buried = true;
            intangible = true;   // shots and homing ignore it until it's nearly up
        }

        public Skeleton(int x, int y, int depth)
        {
            half = new Vector2(0.35f, 0.45f);
            pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
            Setup("Skeleton", "skeleton", 4, depth);
            gold = 4;
            blood = new Color(0.92f, 0.92f, 0.86f);
            facing = Random.value < 0.5f ? -1 : 1;
        }

        protected override bool LeavesCorpse => false;   // it falls apart into bones instead

        public override bool Hurt(float d, Vector2 knock)
        {
            if (buried && !awake) return false;           // safe inside its dirt
            if (!awake) { awake = true; wakeT = WakeTime; return false; }
            if (wakeT > HurtableAt) return false;         // still pulling itself together
            return base.Hurt(d, knock);
        }

        public void Wake() { if (!awake) { awake = true; wakeT = WakeTime; buried = false; G.sfx.Play("dig", 0.5f, 0.6f); } }

        public override void Die()
        {
            if (dead) return;
            BoneBit.Scatter(pos, 6);
            base.Die();
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(7f, false, dt);
            Sprite over = null;
            if (!awake)
            {
                if (buried)
                {
                    // the dirt it lies in is gone: up it comes
                    if (!Wd.Solid(bx, by)) { Wake(); G.fx.Burst(pos, 10, new Color(0.92f, 0.92f, 0.86f), 3f, 0.4f, 8f); }
                    else if (Random.value < 0.01f) G.fx.Burst(pos + Vector2.up * 0.3f, 1, new Color(0.6f, 0.5f, 0.4f), 0.6f, 0.4f, 6f);
                }
                else
                {
                    Fall(dt);
                    if (HasTarget && (TPos - pos).sqrMagnitude < 16f) Wake();
                }
                over = StripFrame("skel_wake", 0);
            }
            else if (wakeT > 0f)
            {
                // the bones rattle up into a skeleton
                wakeT -= dt;
                intangible = wakeT > HurtableAt;
                Fall(dt);
                over = StripFrame("skel_wake", (int)((WakeTime - wakeT) / WakeTime * 8f));
            }
            else if (punchT > 0f)
            {
                punchT -= dt;
                Fall(dt);
                if (Art.Strips.TryGetValue("skel_punch", out var pu) && pu != null)
                {
                    int fi = Mathf.Clamp((int)((0.7f - punchT) / 0.7f * pu.Length), 0, pu.Length - 1);
                    over = StripFrame("skel_punch", fi);
                    // the fist lands on the 6th frame
                    if (fi >= 5 && !punched)
                    {
                        punched = true;
                        Vector2 at = pos + new Vector2(facing * 0.8f, 0.1f);
                        var p = G.player;
                        if (p.mode != Player.Mode.Dead && U.Overlap(at, new Vector2(0.5f, 0.4f), p.pos, Player.Half)) p.Hurt(dmg, pos);
                        G.sfx.Play("hit", 0.5f, 0.8f);
                    }
                }
            }
            else
            {
                punchCd -= dt;
                float sp = 1.5f;
                bool path = false;
                if (HasTarget)
                {
                    path = Chase(TPos, dt);
                    if (Mathf.Abs(TPos.y - pos.y) < 1f && Mathf.Abs(TPos.x - pos.x) < 1.2f && punchCd <= 0f) { facing = TPos.x > pos.x ? 1 : -1; punchT = 0.7f; punched = false; punchCd = 1.4f; }
                }
                WalkMove(sp, dt, HasTarget, !path);
            }
            Draw(0f);
            if (!dead && over != null) sr.sprite = over;
            if (!dead && buried && !awake) sr.color = new Color(0.55f, 0.47f, 0.4f, 0.85f);   // bones showing through the dirt
        }
    }

    // GLIDER BAT: clings to the ceiling, then drops into a long swooping glide at you and flaps back up.
    public class GliderBat : Enemy
    {
        int st;   // 0 hanging, 1 gliding, 2 climbing back
        float t;
        Vector2 roost, glideV;

        public GliderBat(int x, int y, int depth)
        {
            pos = new Vector2(x + 0.5f, y + 0.6f); roost = pos;
            half = new Vector2(0.4f, 0.25f);
            Setup("Glider Bat", "glider", 2, depth);
            gold = 3;
            blood = new Color(0.6f, 0.4f, 0.9f);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt;
            FindTarget(10f, true, dt);
            switch (st)
            {
                case 0:
                    animT = 0f;
                    if (HasTarget && (TPos - pos).sqrMagnitude < 64f || flash > 0f)
                    {
                        st = 1; t = 0f;
                        Vector2 d = TPos - pos;
                        glideV = new Vector2(Mathf.Sign(d.x) * 7f, -6f);
                        G.sfx.Play("bat", 0.35f, 0.7f);
                    }
                    break;
                case 1:
                    // a falling swoop that flattens out
                    glideV.y = Mathf.MoveTowards(glideV.y, 2.5f, 9f * dt);
                    vel = glideV;
                    if (t > 1.8f) { st = 2; t = 0f; }
                    break;
                case 2:
                    vel = Vector2.MoveTowards(vel, (roost - pos).normalized * 4f, 12f * dt);
                    if ((roost - pos).sqrMagnitude < 0.2f || t > 4f) { st = 0; vel = Vector2.zero; if (t > 4f) roost = pos; }
                    break;
            }
            Vector2 p = pos;
            if (Wd.MoveX(ref p, half, vel.x * dt, out _)) { glideV.x *= -1f; vel.x *= -0.5f; }
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) { vel.y = 0f; if (st == 1) { st = 2; t = 0f; } }
            pos = p;
            if (Mathf.Abs(vel.x) > 0.1f) facing = vel.x > 0 ? 1 : -1;
            Draw(0f);
        }
    }

    // WOLF: hunts in pairs. Runs you down fast and leaps the last stretch.
    public class Wolf : Walker
    {
        float leapCd = 1f;

        public Wolf(int x, int y, int depth)
        {
            half = new Vector2(0.55f, 0.35f);
            pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
            Setup("Wolf", "wolf", 4, depth);
            gold = 4;
            blood = new Color(0.8f, 0.2f, 0.2f);
            facing = Random.value < 0.5f ? -1 : 1;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(11f, false, dt);
            leapCd -= dt;
            float sp = 2f;
            if (HasTarget)
            {
                Chase(TPos, dt, 11f);
                sp = 5.5f;
                float dy = TPos.y - pos.y, dx = Mathf.Abs(TPos.x - pos.x);
                if (onGround && leapCd <= 0f && dx < 3.5f && Mathf.Abs(dy) < 1.5f)
                {
                    facing = TPos.x > pos.x ? 1 : -1;
                    vel = new Vector2(facing * 9f, 8f);
                    leapCd = 1.8f;
                    G.sfx.Play("jump", 0.4f, 0.8f);
                }
                // you're up on a ledge: it springs up after you
                else if (onGround && leapCd <= 0f && dy > 1.2f && dy < 4.5f && dx < 5f && !Wd.Solid(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y + 1.2f)))
                {
                    facing = TPos.x > pos.x ? 1 : -1;
                    vel = new Vector2(facing * Mathf.Min(6f, dx * 2f), 14f);
                    leapCd = 1.4f;
                    G.sfx.Play("jump", 0.4f, 0.7f);
                }
            }
            if (onGround || vel.y <= 0f) WalkMove(sp, dt, HasTarget, !HasTarget);
            else
            {
                vel.y = Mathf.Max(vel.y - 40f * dt, -16f);
                Vector2 p = pos;
                if (Wd.MoveX(ref p, half, vel.x * dt, out _)) vel.x = 0f;
                if (Wd.MoveY(ref p, half, vel.y * dt, out _)) vel.y = 0f;
                pos = p;
                onGround = Wd.Ground(pos + new Vector2(0f, -0.05f), half);
            }
            animT += dt * (Mathf.Abs(vel.x) > 3f ? 1.5f : 0f);   // legs run faster when it runs
            Draw(0f);
        }
    }

    // ROCK MAGE: a hooded stone caster. Raises spikes of rock from the floor under you and blinks away if you get close.
    public class RockMage : Walker
    {
        float castT = 2f, blinkCd;

        public RockMage(int x, int y, int depth)
        {
            half = new Vector2(0.35f, 0.8f);
            pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
            Setup("Rock Mage", "rockmage", 5, depth);
            gold = 6;
            blood = new Color(0.6f, 0.65f, 0.6f);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(10f, true, dt);
            Fall(dt);
            castT -= dt; blinkCd -= dt;
            if (HasTarget)
            {
                facing = TPos.x > pos.x ? 1 : -1;
                if ((TPos - pos).sqrMagnitude < 4f && blinkCd <= 0f) Blink();
                if (castT <= 0f)
                {
                    castT = 2.6f;
                    // the ground under you heaves up
                    int fx = Mathf.FloorToInt(TPos.x), fy = Mathf.FloorToInt(TPos.y - Player.Half.y - 0.1f);
                    for (int k = 0; k < 3 && !Wd.Solid(fx, fy); k++) fy--;
                    var at = new Vector2(fx + 0.5f, fy + 1.5f);
                    G.Spawn(new Hazard(at, new Vector2(0.45f, 0.6f), 0.75f, 0.3f, new Color(0.7f, 0.65f, 0.55f), dmg));
                    G.sfx.Play("rumble", 0.4f, 1.5f);
                    flash = 0.04f;
                }
            }
            Draw(0f);
        }

        void Blink()
        {
            blinkCd = 4f;
            for (int k = 0; k < 12; k++)
            {
                int x = Mathf.FloorToInt(pos.x) + Random.Range(-6, 7), y = Mathf.FloorToInt(pos.y) + Random.Range(-2, 3);
                if (Wd.Solid(x, y) || Wd.Solid(x, y + 1) || !Wd.Solid(x, y - 1)) continue;
                G.fx.Burst(pos, 14, new Color(0.7f, 0.65f, 0.55f), 4f, 0.4f, 0f);
                pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
                G.fx.Burst(pos, 14, new Color(0.7f, 0.65f, 0.55f), 4f, 0.4f, 0f);
                G.sfx.Play("dash", 0.4f, 1.5f);
                return;
            }
        }
    }

    // THE LANTERN SHROOM: the fungal hollows' elite. A tall mushroom-hatted thing that swings a lantern of spores,
    // lobs it at you, and calls spore puffs. Always an elite (it carries an item).
    public class MushroomElite : Walker
    {
        float throwT = 2f, callT = 6f;

        public MushroomElite(int x, int y, int depth)
        {
            half = new Vector2(0.5f, 1.6f);
            pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
            Setup("Lantern Shroom", "mushelite", 9, depth);
            gold = 8;
            blood = new Color(0.6f, 0.75f, 0.85f);
            MakeEliteSoon();
        }

        protected override bool CanBeElite => true;

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(11f, true, dt);
            float sp = 0.8f;
            if (HasTarget)
            {
                facing = TPos.x > pos.x ? 1 : -1;
                throwT -= dt; callT -= dt;
                if (throwT <= 0f)
                {
                    throwT = 2.4f;
                    Vector2 from = pos + new Vector2(facing * 0.6f, 0.4f);
                    Vector2 d = TPos - from;
                    float tf = Mathf.Clamp(Mathf.Abs(d.x) / 7f, 0.4f, 1.2f);
                    var b = new Bullet(from, new Vector2(d.x / tf, d.y / tf + 9f * tf), Team.Beast, EnemyShotDamage, dmg, "b_spit", new Color(0.6f, 1f, 0.4f), 1.4f, 2f);
                    b.poison = true;
                    G.Spawn(b);
                    G.sfx.Play("swing", 0.5f, 0.6f);
                }
                if (callT <= 0f)
                {
                    callT = 8f;
                    for (int i = 0; i < 2; i++) G.Spawn(new SporePuff(Mathf.FloorToInt(pos.x) + (i == 0 ? -1 : 1), Mathf.FloorToInt(pos.y + 1f), depth));
                    G.sfx.Play("splat", 0.5f, 0.6f);
                }
            }
            WalkMove(sp, dt, false);
            Draw(0f);
        }

        public override void AddLight(Lighting l) { base.AddLight(l); l.Add(pos + new Vector2(facing * 0.6f, 0f), 3f, 0.8f); }
    }

    // THE VOID MAGES: drifting robed things of the void biome. They pass through rock.
    //   fire: volleys of void-fire   ice: rings of shards that chill   lightning: a bolt drops where you stand
    //   elite: all three, and void spikes from the floor
    public class VoidMage : Enemy
    {
        public enum Kind { Fire, Ice, Lightning, Elite }
        readonly Kind kind;
        float t, castT;
        static readonly Color Fire = new Color(1f, 0.45f, 0.2f), Ice = new Color(0.6f, 0.9f, 1f), Volt = new Color(0.85f, 0.7f, 1f), Void = new Color(0.65f, 0.35f, 1f);

        public VoidMage(int x, int y, int depth, Kind k)
        {
            kind = k;
            pos = new Vector2(x + 0.5f, y + 1f);
            half = new Vector2(0.5f, 1.2f);
            string key = k == Kind.Fire ? "vmage_fire" : k == Kind.Ice ? "vmage_ice" : k == Kind.Lightning ? "vmage_elec" : "vmage_elite";
            Setup(k == Kind.Elite ? "Void Archmage" : "Void Mage", key, k == Kind.Elite ? 14 : 6, depth);
            gold = k == Kind.Elite ? 20 : 7;
            blood = new Color(0.45f, 0.2f, 0.7f);
            castT = 1.5f + Random.value;
            t = Random.value * 6f;
            if (k == Kind.Elite) MakeEliteSoon();
        }

        protected override bool CanBeElite => kind == Kind.Elite;

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt;
            FindTarget(12f, false, dt);
            Vector2 goal = pos + new Vector2(Mathf.Sin(t * 0.6f), Mathf.Cos(t * 0.8f)) * 0.5f;
            if (HasTarget) goal = TPos + new Vector2(Mathf.Cos(t * 0.5f) * 5f, 2.5f + Mathf.Sin(t) * 0.5f);
            // they drift straight through rock
            vel = Vector2.MoveTowards(vel, Vector2.ClampMagnitude(goal - pos, 1f) * 2.2f, 6f * dt);
            pos += vel * dt;
            if (HasTarget)
            {
                facing = TPos.x > pos.x ? 1 : -1;
                castT -= dt;
                if (castT <= 0f) Cast();
            }
            if (Random.value < 0.3f) G.fx.Burst(pos + new Vector2(Random.Range(-0.4f, 0.4f), -1f), 1, Void, 0.8f, 0.6f, -2f, 1f, false);
            Draw(Mathf.Sin(t * 2f) * 0.08f);
        }

        void Cast()
        {
            var k = kind == Kind.Elite ? (Kind)Random.Range(0, 4) : kind;
            castT = kind == Kind.Elite ? 1.6f : 2.6f;
            Vector2 d = (TPos - pos).normalized;
            switch (k)
            {
                case Kind.Fire:
                    for (int i = -1; i <= 1; i++)
                        G.Spawn(new Bullet(pos, U.Rot(d, i * 10f) * 7f, Team.Beast, EnemyShotDamage, dmg, "b_fire", Fire));
                    G.sfx.Play("flame", 0.5f, 0.7f);
                    break;
                case Kind.Ice:
                    for (int i = 0; i < 10; i++)
                    {
                        float a = (i * 36f + t * 30f) * Mathf.Deg2Rad;
                        var b = new Bullet(pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5f, Team.Beast, EnemyShotDamage, dmg * 2 / 3, "b_enemy", Ice);
                        b.chills = true;
                        G.Spawn(b);
                    }
                    G.sfx.Play("shoot", 0.5f, 1.5f);
                    break;
                case Kind.Lightning:
                    {
                        float x = TPos.x;
                        G.Spawn(new Hazard(new Vector2(x, TPos.y + 1f), new Vector2(0.45f, 3f), 0.8f, 0.25f, Volt, dmg));
                        G.sfx.Play("zap", 0.5f, 0.7f);
                        break;
                    }
                default:   // void spikes along the floor under you
                    VoidSpike.Erupt(TPos, 3, dmg);
                    break;
            }
        }

        public override void AddLight(Lighting l) { l.Add(pos, 3f, 0.7f); }
    }

    // VOID SPIKES: black spikes in the void world. They stab up when you come near. Also raised by void casters.
    public class VoidSpike : Entity
    {
        readonly bool permanent;
        public bool Permanent => permanent;
        readonly int dmg;
        float warn, up, life;
        bool hit;

        public VoidSpike(Vector2 floor, bool permanent, int dmg, float warn = 0.6f)
        {
            this.permanent = permanent; this.dmg = dmg; this.warn = warn;
            pos = floor + new Vector2(0f, 0.01f);
            life = permanent ? 99999f : warn + 1.2f;
            MakeGO("VoidSpike", Art.VoidSpike, 10);
            go.transform.localScale = new Vector3(1.6f, 0f, 1f);
        }

        // a row of spikes erupting around a point on the floor
        public static void Erupt(Vector2 near, int n, int dmg)
        {
            for (int i = 0; i < n; i++)
            {
                int x = Mathf.FloorToInt(near.x) + (i - n / 2) * 2, y = Mathf.FloorToInt(near.y);
                for (int k = 0; k < 6 && !Game.I.world.Solid(x, y - 1); k++) y--;
                if (Game.I.world.Solid(x, y)) continue;
                G.Spawn(new VoidSpike(new Vector2(x + 0.5f, y), false, dmg, 0.6f + i * 0.08f));
            }
        }

        public override void Tick(float dt)
        {
            life -= dt;
            if (life <= 0f) { Kill(); return; }
            var p = G.player;
            float k;
            if (permanent)
            {
                // it waits flat in the floor and stabs when you come close
                bool near = p.mode != Player.Mode.Dead && Mathf.Abs(p.pos.x - pos.x) < 1.2f && p.pos.y - pos.y < 2f && p.pos.y > pos.y - 0.5f;
                up = Mathf.MoveTowards(up, near ? 1f : 0.15f, dt * (near ? 9f : 1.5f));
                if (up < 0.5f) hit = false;
                k = up;
            }
            else
            {
                warn -= dt;
                if (warn > 0f) { k = 0.1f; if (Random.value < 0.4f) G.fx.Burst(pos, 1, new Color(0.65f, 0.35f, 1f), 1.5f, 0.3f, -3f); }
                else k = Mathf.Clamp01(-warn * 10f);
            }
            go.transform.localScale = new Vector3(1.6f, 1.6f * k, 1f);
            if (k > 0.6f && !hit && p.mode != Player.Mode.Dead && U.Overlap(pos + Vector2.up * 0.5f * k, new Vector2(0.35f, 0.5f * k), p.pos, Player.Half))
            {
                hit = true;
                p.Hurt(dmg, pos);
            }
            go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y), 0f);
        }

        public override void AddLight(Lighting l) => l.Add(pos + Vector2.up * 0.4f, 1.6f, 0.5f);
    }
}
