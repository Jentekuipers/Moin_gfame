using UnityEngine;

namespace DigDeep
{
    // =====================================================================================
    // Elemental creatures: bees (poison), flame and frost wisps, magma slugs (fire), toxic toads and
    // spore puffs (poison), spark beetles (lightning). And the mimic.
    // =====================================================================================

    // A walker's footing: gravity, turning at walls and ledges, hopping one-block steps.
    public abstract class Walker : Enemy
    {
        protected bool onGround;

        protected void WalkMove(float sp, float dt, bool hop, bool turnAtLedge = true)
        {
            if (drowning) { facing = escapeDir; sp = Mathf.Max(sp, 2.5f); hop = true; turnAtLedge = false; }
            vel.x = U.Approach(vel.x, facing * sp, 20f * dt);
            vel.y = drowning ? U.Approach(vel.y, 4.5f, 40f * dt) : Mathf.Max(vel.y - 40f * dt, -16f);   // drowning: it swims for the surface
            Vector2 p = pos;
            if (Wd.MoveX(ref p, half, vel.x * dt, out _))
            {
                if (hop && (TryHop(p) || vel.y > 0.5f)) { }
                else { facing = -facing; vel.x = 0f; }
            }
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) vel.y = 0f;
            pos = p;
            onGround = Wd.Ground(pos + new Vector2(0f, -0.05f), half);
            if (onGround && turnAtLedge && Mathf.Abs(vel.x) > 0.05f)
            {
                int fx = Mathf.FloorToInt(pos.x + facing * (half.x + 0.1f));
                int fy = Mathf.FloorToInt(pos.y - half.y - 0.2f);
                if (!Wd.Solid(fx, fy)) { facing = -facing; vel.x = 0f; }
            }
        }

        protected void Fall(float dt)
        {
            vel.x = U.Approach(vel.x, 0f, 20f * dt);
            WalkMove(0f, dt, false, false);
        }

        // Chasing: face the way a path around the rock leads (re-planned a few times a second, so it follows the
        // cave as it's dug out). Returns true while it's following a path (so it mustn't turn back at ledges).
        float navT; int navDir; bool navHop, navOn;
        protected bool Chase(Vector2 target, float dt, float hopVel = 10f)
        {
            navT -= dt;
            if (navT <= 0f)
            {
                navT = 0.35f + Random.value * 0.1f;
                bool tall = half.y > 0.5f;
                Vector2 tf = target + Vector2.down * (Player.Half.y - 0.05f);
                navOn = GroundNav.Step(new Vector2(pos.x, pos.y - half.y), tf, tall, out navDir, out navHop) && navDir != 0;
            }
            if (!navOn) { facing = target.x > pos.x ? 1 : -1; return false; }
            facing = navDir;
            if (navHop && onGround && vel.y <= 0.1f)
            {
                // the next step is up a block: hop when at its foot
                int fx = Mathf.FloorToInt(pos.x + facing * (half.x + 0.25f)), fy = Mathf.FloorToInt(pos.y - half.y + 0.05f);
                if (Wd.Solid(fx, fy)) { vel.y = hopVel; navHop = false; }
            }
            return true;
        }
    }

    // BEES: jungle swarms. They buzz about, then dive at you. The sting poisons.
    public class Bee : Enemy
    {
        Vector2 home;
        float t, diveT;
        bool diving;
        Vector2 diveV;

        public Bee(Vector2 at, int depth)
        {
            pos = at; home = at;
            half = new Vector2(0.26f, 0.2f);
            Setup("Bee", "bee", 1, depth);
            gold = 1;
            dmg = Mathf.Max(1, dmg * 2 / 3);
            blood = new Color(1f, 0.85f, 0.3f);
            t = Random.value * 6f;
            diveT = 1.5f + Random.value * 2f;
        }

        protected override void OnTouch(Player p) => p.Poison(2f);

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt;
            FindTarget(9f, false, dt);
            if (diving)
            {
                diveT -= dt;
                vel = Vector2.MoveTowards(vel, diveV, 30f * dt);
                if (diveT <= 0f) { diving = false; diveT = 1.6f + Random.value * 1.6f; }
            }
            else
            {
                Vector2 goal;
                if (HasTarget)
                {
                    goal = TPos + new Vector2(Mathf.Cos(t * 2.3f + home.x) * 2.2f, 1.6f + Mathf.Sin(t * 3.1f) * 0.8f);
                    diveT -= dt;
                    if (diveT <= 0f && Wd.LineClear(pos, TPos))
                    {
                        diving = true; diveT = 0.55f;
                        diveV = (TPos - pos).normalized * 9f;
                        G.sfx.Play("bat", 0.25f, 1.8f, 0.1f);
                    }
                }
                else goal = home + new Vector2(Mathf.Cos(t * 1.7f) * 1.5f, Mathf.Sin(t * 2.6f) * 0.8f);
                Vector2 want = goal - pos;
                if (want.sqrMagnitude > 1f) want = want.normalized;
                vel = Vector2.MoveTowards(vel, want * 4.2f, 14f * dt);
            }
            Vector2 p = pos;
            if (Wd.MoveX(ref p, half, vel.x * dt, out _)) { vel.x *= -0.5f; diving = false; }
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) { vel.y *= -0.5f; diving = false; }
            pos = p;
            if (Mathf.Abs(vel.x) > 0.1f) facing = vel.x > 0 ? 1 : -1;
            animT += dt * 2f;   // wings: a blur
            Draw(0f);
        }
    }

    // WISPS: drifting flames (magma) and cold sparks (crystal). They keep their distance and shoot.
    // A flame wisp sets you alight and dies in water; a frost wisp's shards chill you.
    public class Wisp : Enemy
    {
        readonly bool frost;
        float t, shootT, navT;
        bool navOk;
        Vector2 navPt;

        public Wisp(int x, int y, int depth, bool frost)
        {
            this.frost = frost;
            pos = new Vector2(x + 0.5f, y + 0.5f);
            half = new Vector2(0.3f, 0.3f);
            Setup(frost ? "Frost Wisp" : "Flame Wisp", frost ? "frostwisp" : "flamewisp", 2, depth);
            fireLike = !frost;
            gold = 3;
            blood = frost ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 0.6f, 0.2f);
            t = Random.value * 6f;
            shootT = 1.5f + Random.value;
        }

        protected override void OnTouch(Player p) { if (frost) p.Chill(2f); else p.SetOnFire(); }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt;
            FindTarget(10f, false, dt);
            Vector2 goal = pos + new Vector2(Mathf.Sin(t) * 1f, Mathf.Cos(t * 1.3f) * 0.6f);
            if (HasTarget)
            {
                Vector2 tp = TPos;
                goal = tp + new Vector2(Mathf.Cos(t * 0.9f) * 3.5f, 2f + Mathf.Sin(t * 1.3f));
                if (Wd.Solid(Mathf.FloorToInt(goal.x), Mathf.FloorToInt(goal.y))) goal = tp;
                navT -= dt;
                if (navT <= 0f) { navT = 0.25f + Random.value * 0.1f; navOk = Nav.Steer(pos, goal, out navPt); }
                if (navOk) goal = navPt;
                float dist = (tp - pos).magnitude;
                shootT -= dt;
                if (shootT <= 0f && dist < 9f && Wd.LineClear(pos, tp))
                {
                    shootT = 2.2f + Random.value * 0.6f;
                    Vector2 d = (tp - pos).normalized;
                    if (frost)
                        for (int i = -1; i <= 1; i++)
                        {
                            var b = new Bullet(pos, U.Rot(d, i * 12f) * 6.5f, Team.Beast, EnemyShotDamage, dmg * 2 / 3, "b_enemy", new Color(0.65f, 0.92f, 1f));
                            b.chills = true;
                            G.Spawn(b);
                        }
                    else G.Spawn(new Bullet(pos, d * 6.5f, Team.Beast, EnemyShotDamage, dmg, "b_fire", new Color(1f, 0.6f, 0.2f)));
                    G.sfx.Play("shoot", 0.45f, frost ? 1.5f : 0.75f);
                }
            }
            Vector2 want = goal - pos;
            if (want.sqrMagnitude > 1f) want = want.normalized;
            vel = Vector2.MoveTowards(vel, want * 3f, 10f * dt);
            Vector2 p = pos;
            if (Wd.MoveX(ref p, half, vel.x * dt, out _)) { vel.x *= -0.5f; navT = 0f; }
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) { vel.y *= -0.5f; navT = 0f; }
            pos = p;
            if (Random.value < 0.3f) G.fx.Burst(pos + Vector2.down * 0.2f, 1, blood, 1f, 0.4f, frost ? 2f : -4f, 1f, false);
            Draw(0f);
        }

        public override void AddLight(Lighting l)
        {
            base.AddLight(l);
            l.Add(pos, 2.4f, 0.7f);
        }
    }

    // MAGMA SLUG: crawls slowly through the magma core leaving burning embers behind. Lava doesn't hurt it; water does.
    public class MagmaSlug : Walker
    {
        float emberT;

        public MagmaSlug(int x, int y, int depth)
        {
            half = new Vector2(0.45f, 0.24f);
            pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
            Setup("Magma Slug", "magmaslug", 5, depth);
            fireLike = true;
            gold = 4;
            dmg = dmg * 11 / 10;
            blood = new Color(1f, 0.55f, 0.2f);
            facing = Random.value < 0.5f ? -1 : 1;
        }

        protected override void OnTouch(Player p) => p.SetOnFire();

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(8f, false, dt);
            float sp = 0.9f;
            if (HasTarget && Mathf.Abs(TPos.y - pos.y) < 1.2f && Mathf.Abs(TPos.x - pos.x) < 6f) { facing = TPos.x > pos.x ? 1 : -1; sp = 1.5f; }
            WalkMove(sp, dt, false);
            emberT -= dt;
            if (emberT <= 0f && onGround)
            {
                emberT = 0.7f;
                G.Spawn(new SlugEmber(pos + new Vector2(-facing * 0.3f, -half.y + 0.2f), 3.5f));
            }
            Draw(0f);
        }

        public override void AddLight(Lighting l) { base.AddLight(l); l.Add(pos, 2f, 0.6f); }
    }

    // A small flame on the ground. Touch it and you catch fire. Water puts it out.
    public class SlugEmber : Entity
    {
        float life;
        readonly float life0;

        public SlugEmber(Vector2 p, float life)
        {
            pos = p; this.life = life; life0 = life;
            MakeGO("SlugEmber", Art.Fireball, 11);
            go.transform.localScale = Vector3.one * 0.6f;
        }

        public override void Tick(float dt)
        {
            life -= dt;
            if (life <= 0f || Wd.FluidAtPoint(pos) == Fluids.Water) { G.fx.Smoke(pos, 2, 0.6f); Kill(); return; }
            float k = Mathf.Clamp01(life / life0);
            float s = (0.4f + 0.3f * k) * (1f + 0.15f * Mathf.Sin(Time.time * 30f + pos.x));
            go.transform.localScale = new Vector3(s, s, 1f);
            if (Random.value < 0.25f) G.fx.Burst(pos, 1, new Color(1f, 0.6f, 0.2f), 1f, 0.4f, -5f, 1f, false);
            var pl = G.player;
            if (pl.mode != Player.Mode.Dead && U.Overlap(pos, new Vector2(0.25f, 0.25f), pl.pos, Player.Half)) pl.SetOnFire();
            Place();
        }

        public override void AddLight(Lighting l) => l.Add(pos, 1.6f, 0.55f);
    }

    // TOXIC TOAD: sits, hops towards you and spits poison. Bursts into a poison cloud when it dies.
    public class ToxicToad : Walker
    {
        float hopT, spitT, puffT;

        public ToxicToad(int x, int y, int depth)
        {
            half = new Vector2(0.4f, 0.3f);
            pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
            Setup("Toxic Toad", "toxictoad", 3, depth);
            gold = 3;
            blood = new Color(0.55f, 0.9f, 0.3f);
            hopT = 1f + Random.value * 2f;
            spitT = 2f + Random.value;
            facing = Random.value < 0.5f ? -1 : 1;
        }

        protected override void OnTouch(Player p) => p.Poison(2.5f);

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(9f, true, dt);
            if (onGround) vel.x = U.Approach(vel.x, 0f, 25f * dt);
            if (HasTarget)
            {
                facing = TPos.x > pos.x ? 1 : -1;
                spitT -= dt;
                if (puffT > 0f)
                {
                    puffT -= dt;
                    if (puffT <= 0f)
                    {
                        Vector2 d = (TPos - pos + Vector2.up * 0.8f).normalized;
                        var b = new Bullet(pos + new Vector2(facing * 0.3f, 0.1f), d * 7f, Team.Beast, EnemyShotDamage, dmg, "b_spit", new Color(0.6f, 1f, 0.3f));
                        b.poison = true;
                        G.Spawn(b);
                        G.sfx.Play("shoot", 0.5f, 0.55f);
                    }
                }
                else if (spitT <= 0f && onGround && (TPos - pos).sqrMagnitude < 49f) { spitT = 2.6f; puffT = 0.45f; }
                hopT -= dt;
                if (hopT <= 0f && onGround && puffT <= 0f)
                {
                    hopT = 1.4f + Random.value;
                    vel = new Vector2(facing * 4f, 9f);
                    G.sfx.Play("jump", 0.35f, 1.3f, 0.1f);
                }
            }
            vel.y = Mathf.Max(vel.y - 40f * dt, -16f);
            Vector2 p = pos;
            if (Wd.MoveX(ref p, half, vel.x * dt, out _)) vel.x = 0f;
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) vel.y = 0f;
            pos = p;
            onGround = Wd.Ground(pos + new Vector2(0f, -0.05f), half);
            animT = puffT > 0f ? 0.17f : 0f;
            Draw(0f);
        }

        public override void Die()
        {
            if (dead) return;
            PoisonCloud(pos, 6, depth);
            base.Die();
        }

        public static void PoisonCloud(Vector2 at, int n, int depth)
        {
            for (int i = 0; i < n; i++)
            {
                float a = (i * 360f / n + Random.value * 20f) * Mathf.Deg2Rad;
                var b = new Bullet(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3.5f, Team.Beast, 2f, ContactDmg(depth) / 2, "b_spit", new Color(0.6f, 1f, 0.3f), 1f, 0.6f);
                b.poison = true;
                G.Spawn(b);
            }
            G.fx.Burst(at, 16, new Color(0.55f, 0.95f, 0.35f, 0.8f), 3f, 0.8f, -1f, 2f, false);
        }
    }

    // SPORE PUFF: a floating ball of spores. It drifts towards you, swells up and bursts into a poison ring.
    public class SporePuff : Enemy
    {
        float t, swell;
        bool burst;

        public SporePuff(int x, int y, int depth)
        {
            pos = new Vector2(x + 0.5f, y + 0.5f);
            half = new Vector2(0.32f, 0.32f);
            Setup("Spore Puff", "sporepuff", 2, depth);
            gold = 2;
            blood = new Color(0.8f, 0.55f, 0.9f);
            t = Random.value * 6f;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt;
            FindTarget(8f, true, dt);
            Vector2 want = new Vector2(Mathf.Sin(t * 0.7f), Mathf.Cos(t * 0.9f)) * 0.4f;
            if (HasTarget)
            {
                Vector2 d = TPos - pos;
                want = d.normalized * 1.3f;
                if (swell <= 0f && d.sqrMagnitude < 2.4f * 2.4f) { swell = 0.6f; G.sfx.Play("fuse", 0.4f, 1.6f); }
            }
            if (swell > 0f)
            {
                swell -= dt;
                want = Vector2.zero;
                flash = ((int)(swell * 16f)) % 2 == 0 ? 0.03f : 0f;
                if (swell <= 0f) { Pop(); return; }
            }
            vel = Vector2.MoveTowards(vel, want, 4f * dt);
            Vector2 p = pos;
            if (Wd.MoveX(ref p, half, vel.x * dt, out _)) vel.x *= -0.5f;
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) vel.y *= -0.5f;
            pos = p;
            animT = swell > 0f ? 0.17f : 0f;
            Draw(Mathf.Sin(t * 2f) * 0.06f);
        }

        void Pop()
        {
            if (burst) return;
            burst = true;
            ToxicToad.PoisonCloud(pos, 10, depth);
            G.sfx.Play("splat", 0.6f, 1.3f);
            Die();
        }

        public override void Die()
        {
            if (dead) return;
            if (!burst) { burst = true; ToxicToad.PoisonCloud(pos, 8, depth); }
            base.Die();
        }
    }

    // SPARK BEETLE: crackles with stored lightning. It stops, charges, and lets out a ring of sparks.
    public class SparkBeetle : Walker
    {
        float chargeT, zapT;

        public SparkBeetle(int x, int y, int depth)
        {
            half = new Vector2(0.4f, 0.28f);
            pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
            Setup("Spark Beetle", "sparkbeetle", 4, depth);
            gold = 4;
            blood = new Color(0.7f, 0.9f, 1f);
            zapT = 2f + Random.value * 2f;
            facing = Random.value < 0.5f ? -1 : 1;
        }

        protected override void OnTouch(Player p) => p.Shock(dmg / 3);

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(8f, false, dt);
            if (chargeT > 0f)
            {
                chargeT -= dt;
                Fall(dt);
                flash = ((int)(chargeT * 14f)) % 2 == 0 ? 0.03f : 0f;
                if (Random.value < 0.6f) G.fx.Burst(pos + Random.insideUnitCircle * 0.5f, 1, new Color(1f, 0.95f, 0.5f), 3f, 0.15f, 0f);
                if (chargeT <= 0f)
                {
                    int n = 8;
                    float off = Random.value * 45f;
                    for (int i = 0; i < n; i++)
                    {
                        float a = (off + i * 360f / n) * Mathf.Deg2Rad;
                        var b = new Bullet(pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f, Team.Beast, EnemyShotDamage, dmg / 2, "b_zap", new Color(1f, 0.95f, 0.5f), 1f, 1.2f);
                        b.zaps = true;
                        G.Spawn(b);
                    }
                    G.sfx.Play("shock", 0.6f, 1.2f);
                    G.fx.Ring(pos, 1.6f, new Color(1f, 0.95f, 0.5f));
                }
                animT = 0.17f;
                Draw(0f);
                return;
            }
            float sp = 1.6f;
            if (HasTarget)
            {
                if (Mathf.Abs(TPos.y - pos.y) < 1.2f) { facing = TPos.x > pos.x ? 1 : -1; sp = 2.4f; }
                zapT -= dt;
                if (zapT <= 0f && (TPos - pos).sqrMagnitude < 36f) { zapT = 3.2f + Random.value; chargeT = 0.75f; G.sfx.Play("fuse", 0.4f, 2f); }
            }
            WalkMove(sp, dt, HasTarget);
            Draw(0f);
        }

        public override void AddLight(Lighting l) { base.AddLight(l); if (chargeT > 0f) l.Add(pos, 2.5f, 0.8f); }
    }

    // =====================================================================================
    // MIMIC: once in a long while a chest is not a chest. It waits until you come close, then bites,
    // bounces after you, and when it dies it spits out something rare.
    // =====================================================================================
    public class MimicLurker : Entity
    {
        readonly int x, y;

        public MimicLurker(int x, int y) { this.x = x; this.y = y; pos = new Vector2(x + 0.5f, y + 0.5f); }

        public override void Tick(float dt)
        {
            if (Wd.Peek(x, y) != Block.Chest) { Kill(); return; }
            var p = G.player;
            if (p.mode != Player.Mode.Dead && (p.pos - pos).sqrMagnitude < 1.7f * 1.7f) Wake(true);
        }

        void Wake(bool clearBlock)
        {
            if (dead) return;
            if (clearBlock) Wd.Set(x, y, Block.Air);
            G.Spawn(new Mimic(pos, -y));
            Kill();
        }

        // the chest was broken open from afar: it was the mimic all along
        public static bool TryWake(Vector2 p)
        {
            int cx = Mathf.FloorToInt(p.x), cy = Mathf.FloorToInt(p.y);
            foreach (var e in G.ents)
                if (!e.dead && e is MimicLurker m && m.x == cx && m.y == cy) { m.Wake(false); return true; }
            return false;
        }
    }

    public class Mimic : Enemy
    {
        float hopT = 0.45f;
        bool onGround;

        public Mimic(Vector2 at, int depth)
        {
            pos = at;
            half = new Vector2(0.42f, 0.38f);
            Setup("Mimic", "mimic", 14, depth);
            gold = 25;
            dmg = dmg * 3 / 2;
            blood = new Color(0.8f, 0.2f, 0.25f);
            vel = new Vector2(0f, 9f);
            G.sfx.Play("chest", 0.9f, 0.55f);
            G.sfx.Play("rumble", 0.6f, 1.5f);
            G.cam.Shake(0.35f);
            G.Popup(pos + Vector2.up, "MIMIC!", new Color(1f, 0.3f, 0.3f));
        }

        protected override bool CanBeElite => false;

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(16f, false, dt);
            if (HasTarget && onGround) facing = TPos.x > pos.x ? 1 : -1;
            vel.y = Mathf.Max(vel.y - 40f * dt, -18f);
            Vector2 p = pos;
            if (Wd.MoveX(ref p, half, vel.x * dt, out _)) vel.x *= -0.6f;
            bool hitY = Wd.MoveY(ref p, half, vel.y * dt, out _);
            pos = p;
            bool was = onGround;
            onGround = Wd.Ground(pos + new Vector2(0f, -0.05f), half);
            if (hitY) vel.y = 0f;
            if (onGround && !was) { G.sfx.Play("land", 0.6f, 0.8f); G.cam.Shake(0.1f); G.fx.Burst(pos + Vector2.down * half.y, 6, new Color(0.7f, 0.55f, 0.35f), 3f, 0.3f, 15f); }
            if (onGround)
            {
                vel.x = U.Approach(vel.x, 0f, 30f * dt);
                hopT -= dt;
                if (hopT <= 0f && HasTarget)
                {
                    float dx = TPos.x - pos.x;
                    vel = new Vector2(Mathf.Clamp(dx * 1.4f, -7f, 7f) + facing * 1.5f, Random.Range(9f, 13f));
                    hopT = Random.Range(0.3f, 0.65f);
                    G.sfx.Play("jump", 0.5f, 0.7f, 0.1f);
                }
            }
            animT = onGround && hopT > 0.15f ? 0f : 0.17f;   // the lid gapes in the air
            Draw(0f);
        }

        public override void Die()
        {
            if (dead) return;
            Vector2 at = pos;
            base.Die();
            G.RareReward(at);
        }
    }
}
