using UnityEngine;

namespace DigDeep
{
    // The creatures of the newer biomes.
    //  CLOUD REACHES  - PUFFLING (a little angry cloud: keeps its distance, blows you away and spits hail),
    //                   THUNDERBIRD (circles, then dives at you: the touch shocks), SPARK WISP (flashes, then zaps)
    //  CRIMSON FOREST - BLOOD LEECH (crawls after you, drinks from what it bites), CRIMSON HOUND (runs you down, leaps),
    //                   WEEPLING (a little tree that lobs blood), VAMPIRE BAT (heals on a bite). All heal in blood.
    //  SUNKEN BEACH   - CRABLING (sidles at you and pinches), GULL (swoops, snatches a few coins)
    //  NEON CANOPY    - NEON JAGUAR (prowls, then pounces), NEON MONKEY (hangs from the roof throwing glowing fruit),
    //                   GLOW MOTH (flutters, sheds burning dust)
    //  CORAL REEF     - SEA URCHIN (sits, throws spines). Plus the reef's and the beach's new fish (see Fish).

    // a flier that steers round rock towards a goal (like the bats)
    public abstract class Flier : Enemy
    {
        float navT; bool navOk; Vector2 navPt;
        protected float t;

        protected void Fly(Vector2 goal, float speed, float accel, float dt)
        {
            if (HasTarget)
            {
                navT -= dt;
                if (navT <= 0f) { navT = 0.25f + Random.value * 0.1f; navOk = Nav.Steer(pos, goal, out navPt); }
                if (navOk) goal = navPt;
            }
            Vector2 want = goal - pos;
            if (want.sqrMagnitude > 0.01f) want = want.normalized * speed;
            if (contactCd > 0f) want = -want * 0.6f;
            vel = Vector2.MoveTowards(vel, want, accel * dt);
            Vector2 p = pos;
            if (Wd.MoveX(ref p, half, vel.x * dt, out _)) { vel.x *= -0.5f; navT = 0f; }
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) { vel.y *= -0.5f; navT = 0f; }
            pos = p;
            if (Mathf.Abs(vel.x) > 0.3f) facing = vel.x > 0f ? 1 : -1;
        }
    }

    // ================================================================== CLOUD REACHES

    public class Puffling : Flier
    {
        float gustT = 2.5f;
        public Puffling(int x, int y, int depth)
        {
            pos = new Vector2(x + 0.5f, y + 0.5f); half = new Vector2(0.42f, 0.3f);
            Setup("Puffling", "puffcloud", 3, depth);
            gold = 2; blood = new Color(0.85f, 0.92f, 1f);
            t = Random.value * 5f;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt;
            FindTarget(11f, false, dt);
            Vector2 tp = HasTarget ? TPos : pos;
            Vector2 goal = HasTarget ? tp + new Vector2(Mathf.Sign(pos.x - tp.x) * 4f, 2.2f + Mathf.Sin(t) * 0.6f) : pos + new Vector2(Mathf.Sin(t * 0.6f) * 2f, Mathf.Cos(t * 0.8f));
            Fly(goal, 2.6f, 8f, dt);
            gustT -= dt;
            if (HasTarget && gustT <= 0f && (tp - pos).sqrMagnitude < 49f)
            {
                gustT = 3.2f;
                // a gust: blows you away from it, and a spatter of hail
                var p = G.player;
                Vector2 d = (p.pos - pos).normalized;
                if (p.mode != Player.Mode.Dead) p.vel += d * 9f + Vector2.up * 2f;
                G.fx.Burst(pos + d * 0.6f, 12, new Color(0.9f, 0.95f, 1f), 7f, 0.4f, 0f, 1f, false);
                for (int i = -1; i <= 1; i++)
                    G.Spawn(new Bullet(pos, U.Rot(d, i * 14f) * 8f, Team.Beast, EnemyShotDamage, dmg / 2, "b_pellet", new Color(0.8f, 0.95f, 1f), 0.8f));
                G.sfx.Play("dash", 0.4f, 1.5f);
            }
            Draw(Mathf.Sin(t * 2f) * 0.06f);
        }
    }

    public class Thunderbird : Flier
    {
        float diveT = 2f, diving;
        Vector2 diveV;
        public Thunderbird(int x, int y, int depth)
        {
            pos = new Vector2(x + 0.5f, y + 0.5f); half = new Vector2(0.45f, 0.28f);
            Setup("Thunderbird", "thunderbird", 3, depth);
            gold = 3; blood = new Color(1f, 0.95f, 0.5f);
            t = Random.value * 5f;
        }
        protected override void OnTouch(Player p) => p.Shock(Mathf.Max(6, dmg / 3));

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt;
            FindTarget(12f, false, dt);
            if (diving > 0f)
            {
                diving -= dt;
                vel = diveV;
                Vector2 p = pos;
                bool hit = Wd.MoveX(ref p, half, vel.x * dt, out _) | Wd.MoveY(ref p, half, vel.y * dt, out _);
                pos = p;
                if (hit) diving = 0f;
                if (Random.value < 0.5f) G.fx.Burst(pos, 1, new Color(1f, 0.95f, 0.5f), 1f, 0.2f, 0f);
            }
            else
            {
                Vector2 tp = HasTarget ? TPos : pos;
                Vector2 goal = HasTarget ? tp + new Vector2(Mathf.Cos(t * 1.4f) * 4f, 3.5f) : pos + new Vector2(Mathf.Sin(t) * 3f, 0f);
                Fly(goal, 4.2f, 10f, dt);
                diveT -= dt;
                if (HasTarget && diveT <= 0f && Wd.LineClear(pos, tp))
                {
                    diveT = 2.6f; diving = 0.55f;
                    diveV = (tp - pos).normalized * 13f;
                    facing = diveV.x > 0 ? 1 : -1;
                    flash = 0.1f;
                    G.sfx.Play("zap", 0.4f, 1.4f);
                }
            }
            Draw(0f);
        }

        public override void AddLight(Lighting l) { l.Add(pos, 1.8f, 0.4f); base.AddLight(l); }
    }

    public class SparkWisp : Flier
    {
        float zapT = 2f, warn;
        public SparkWisp(int x, int y, int depth)
        {
            pos = new Vector2(x + 0.5f, y + 0.5f); half = new Vector2(0.28f, 0.28f);
            Setup("Spark Wisp", "sparkwisp", 2, depth);
            gold = 2; blood = new Color(1f, 1f, 0.6f);
            t = Random.value * 5f;
        }
        protected override void OnTouch(Player p) => p.Shock(Mathf.Max(5, dmg / 3));

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt;
            FindTarget(10f, false, dt);
            Vector2 tp = HasTarget ? TPos : pos;
            Fly(HasTarget ? tp + new Vector2(Mathf.Sin(t) * 3f, 2.5f) : pos + new Vector2(Mathf.Sin(t) * 1.5f, Mathf.Cos(t * 1.3f)), 2.2f, 6f, dt);
            if (warn > 0f)
            {
                warn -= dt;
                flash = ((int)(warn * 16f)) % 2 == 0 ? 0.05f : 0f;
                if (warn <= 0f && HasTarget)
                {
                    var b = new Bullet(pos, (tp - pos).normalized * 12f, Team.Beast, EnemyShotDamage, dmg / 2, "b_zap", new Color(1f, 0.95f, 0.5f));
                    b.zaps = true;
                    G.Spawn(b);
                    G.sfx.Play("zap", 0.5f, 1.2f);
                }
            }
            else
            {
                zapT -= dt;
                if (HasTarget && zapT <= 0f && Wd.LineClear(pos, tp)) { zapT = 2.6f; warn = 0.6f; }
            }
            Draw(0f);
        }

        public override void AddLight(Lighting l) => l.Add(pos, 2.4f + Mathf.Sin(t * 9f) * 0.3f, 0.7f);
    }

    // ================================================================== CRIMSON FOREST

    public class BloodLeech : Walker
    {
        public BloodLeech(int x, int y, int depth)
        {
            pos = new Vector2(x + 0.5f, y + 0.25f); half = new Vector2(0.4f, 0.18f);
            Setup("Blood Leech", "bloodleech", 3, depth);
            crimson = true; gold = 2; blood = new Color(0.8f, 0.05f, 0.15f);
            facing = Random.value < 0.5f ? -1 : 1;
        }
        // it drinks from what it bites
        protected override void OnTouch(Player p) { hp = Mathf.Min(maxHp, hp + dmg * 0.05f); G.fx.Burst(pos, 4, new Color(0.9f, 0.1f, 0.2f), 2f, 0.4f, 5f); }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(8f, true, dt);
            bool path = HasTarget && Chase(TPos, dt, 7f);
            WalkMove(HasTarget ? 2.4f : 0.8f, dt, HasTarget, !path);
            animT += dt;
            Draw(-half.y + 1f / 16f);
        }
    }

    public class CrimsonHound : Walker
    {
        float leapCd = 1f;
        public CrimsonHound(int x, int y, int depth)
        {
            pos = new Vector2(x + 0.5f, y + 0.4f); half = new Vector2(0.5f, 0.32f);
            Setup("Crimson Hound", "crimhound", 5, depth);
            crimson = true; gold = 4; blood = new Color(0.8f, 0.05f, 0.15f);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            leapCd -= dt;
            FindTarget(11f, true, dt);
            bool path = HasTarget && Chase(TPos, dt, 10f);
            if (HasTarget && onGround && leapCd <= 0f && Mathf.Abs(TPos.y - pos.y) < 2f && Mathf.Abs(TPos.x - pos.x) < 5f)
            {
                leapCd = 2f;
                vel = new Vector2(Mathf.Sign(TPos.x - pos.x) * 9f, 9f);
                G.sfx.Play("jump", 0.4f, 0.7f);
            }
            WalkMove(HasTarget ? 5.2f : 1.2f, dt, true, !path && !HasTarget);
            animT += dt * (Mathf.Abs(vel.x) > 3f ? 1.5f : 0.6f);
            Draw(-half.y + 1f / 16f);
        }
    }

    public class Weepling : Walker
    {
        float lobT = 1.5f;
        public Weepling(int x, int y, int depth)
        {
            pos = new Vector2(x + 0.5f, y + 0.55f); half = new Vector2(0.35f, 0.55f);
            Setup("Weepling", "weepling", 4, depth);
            crimson = true; gold = 3; blood = new Color(0.5f, 0.3f, 0.2f);
            Plant_ = true;
        }
        bool Plant_;
        public override bool Plant => Plant_;

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(10f, true, dt);
            Fall(dt);
            lobT -= dt;
            if (HasTarget && lobT <= 0f)
            {
                lobT = 2.6f;
                Vector2 d = TPos - pos;
                facing = d.x > 0 ? 1 : -1;
                float tt = Mathf.Clamp(Mathf.Abs(d.x) / 6f, 0.5f, 1.2f);
                Vector2 v = new Vector2(d.x / tt, d.y / tt + 0.5f * LiquidGlob.Grav * tt);
                G.Spawn(new LiquidGlob(pos + Vector2.up * 0.5f, v, Fluids.Blood, 18, dmg / 2, this));
                G.sfx.Play("splat", 0.4f, 0.8f);
            }
            animT += dt * 0.5f;
            Draw(-half.y + 1f / 16f);
        }
    }

    public class VampireBat : Flier
    {
        public VampireBat(int x, int y, int depth)
        {
            pos = new Vector2(x + 0.5f, y + 0.5f); half = new Vector2(0.32f, 0.22f);
            Setup("Vampire Bat", "vampbat", 2, depth);
            crimson = true; gold = 2; blood = new Color(0.8f, 0.05f, 0.15f);
            t = Random.value * 6f;
        }
        protected override void OnTouch(Player p) { hp = Mathf.Min(maxHp, hp + maxHp * 0.4f); G.Popup(pos + Vector2.up * 0.6f, "SLURP", new Color(1f, 0.3f, 0.3f)); }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt;
            FindTarget(11f, false, dt);
            Vector2 goal = HasTarget ? TPos + new Vector2(0f, Mathf.Sin(t * 3f) * 0.8f) : pos + new Vector2(Mathf.Sin(t) * 2f, 0f);
            Fly(goal, 4f, 12f, dt);
            Draw(Mathf.Sin(t * 8f) * 0.05f);
        }
    }

    // ================================================================== SUNKEN BEACH

    public class Crabling : Walker
    {
        public Crabling(int x, int y, int depth)
        {
            pos = new Vector2(x + 0.5f, y + 0.25f); half = new Vector2(0.42f, 0.22f);
            Setup("Crabling", "crabling", 3, depth);
            gold = 2; blood = new Color(0.4f, 0.7f, 1f);
            waterLike = true;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(8f, true, dt);
            bool path = HasTarget && Chase(TPos, dt, 8f);
            WalkMove(HasTarget ? 3.4f : 1f, dt, true, !path);
            animT += dt * 1.5f;
            int f = facing; facing = 1;   // (a crab doesn't turn round: it sidles)
            Draw(-half.y + 1f / 16f);
            facing = f;
        }
    }

    public class Gull : Flier
    {
        float swoopT = 2f;
        public Gull(int x, int y, int depth)
        {
            pos = new Vector2(x + 0.5f, y + 0.5f); half = new Vector2(0.4f, 0.22f);
            Setup("Gull", "gull", 2, depth);
            gold = 1; blood = new Color(0.9f, 0.9f, 0.9f);
            t = Random.value * 5f;
        }
        // it snatches a few coins
        protected override void OnTouch(Player p)
        {
            int take = Mathf.Min(p.s.gold, 3);
            if (take <= 0) return;
            p.s.gold -= take; gold += take;
            G.Popup(pos + Vector2.up * 0.6f, "-" + take + " GOLD", new Color(1f, 0.8f, 0.3f));
            G.sfx.Play("coin", 0.4f, 1.6f);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt;
            FindTarget(12f, false, dt);
            swoopT -= dt;
            Vector2 tp = HasTarget ? TPos : pos;
            bool swoop = HasTarget && swoopT < 0.8f;
            if (swoopT <= 0f) swoopT = 3f;
            Vector2 goal = swoop ? tp : HasTarget ? tp + new Vector2(Mathf.Cos(t) * 5f, 4f) : pos + new Vector2(Mathf.Sin(t * 0.5f) * 3f, 0f);
            Fly(goal, swoop ? 8f : 3.5f, 14f, dt);
            Draw(0f);
        }
    }

    // ================================================================== NEON CANOPY

    public class NeonJaguar : Walker
    {
        float pounceCd = 1.5f;
        public NeonJaguar(int x, int y, int depth)
        {
            pos = new Vector2(x + 0.5f, y + 0.3f); half = new Vector2(0.6f, 0.28f);
            Setup("Neon Jaguar", "neonjaguar", 5, depth);
            gold = 4; blood = new Color(0.4f, 1f, 0.8f);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            pounceCd -= dt;
            FindTarget(12f, true, dt);
            bool path = HasTarget && Chase(TPos, dt, 10f);
            if (HasTarget && onGround && pounceCd <= 0f && Mathf.Abs(TPos.x - pos.x) < 6f && TPos.y - pos.y < 3f)
            {
                pounceCd = 2.4f;
                Vector2 d = TPos - pos;
                vel = new Vector2(Mathf.Clamp(d.x * 2f, -12f, 12f), 8f + Mathf.Max(0f, d.y) * 2.5f);
                G.sfx.Play("dash", 0.5f, 0.8f);
            }
            WalkMove(HasTarget ? 3.8f : 1.1f, dt, true, !path && !HasTarget);
            animT += dt * (Mathf.Abs(vel.x) > 3f ? 1.4f : 0.5f);
            Draw(-half.y + 1f / 16f);
        }

        public override void AddLight(Lighting l) { l.Add(pos, 2.2f, 0.45f); base.AddLight(l); }
    }

    public class NeonMonkey : Enemy
    {
        float throwT = 1.5f, t;
        public NeonMonkey(int x, int y, int depth)
        {
            pos = new Vector2(x + 0.5f, y + 0.4f); half = new Vector2(0.32f, 0.38f);
            Setup("Neon Monkey", "neonmonkey", 3, depth);
            gold = 3; blood = new Color(1f, 0.5f, 0.9f);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt;
            // it hangs from the roof by its tail; knocked loose, it drops
            if (!Wd.Solid(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y + half.y + 0.3f)))
            {
                vel.y = Mathf.Max(vel.y - 30f * dt, -14f);
                Vector2 p = pos;
                if (Wd.MoveY(ref p, half, vel.y * dt, out _)) vel.y = 0f;
                pos = p;
            }
            FindTarget(10f, true, dt);
            throwT -= dt;
            if (HasTarget && throwT <= 0f)
            {
                throwT = 2.2f;
                Vector2 d = TPos - pos;
                facing = d.x > 0 ? 1 : -1;
                float tt = Mathf.Clamp(Mathf.Abs(d.x) / 7f, 0.4f, 1.1f);
                var b = new Bullet(pos, new Vector2(d.x / tt, d.y / tt + 0.5f * 22f * tt), Team.Beast, EnemyShotDamage, dmg / 2, "b_seed", Random.value < 0.5f ? new Color(1f, 0.4f, 0.9f) : new Color(0.4f, 1f, 0.9f)).Lobbed(22f, 0f);
                G.Spawn(b);
                G.sfx.Play("swing", 0.4f, 1.4f);
            }
            animT += dt * 0.4f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2f) * 8f);
            Draw(0f);
        }

        public override void AddLight(Lighting l) { l.Add(pos, 1.6f, 0.4f); base.AddLight(l); }
    }

    public class GlowMoth : Flier
    {
        float dustT = 1f;
        public GlowMoth(int x, int y, int depth)
        {
            pos = new Vector2(x + 0.5f, y + 0.5f); half = new Vector2(0.36f, 0.24f);
            Setup("Glow Moth", "glowmoth", 2, depth);
            gold = 2; blood = new Color(0.6f, 1f, 0.5f);
            t = Random.value * 5f;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt;
            FindTarget(10f, false, dt);
            Vector2 tp = HasTarget ? TPos : pos;
            Fly(HasTarget ? tp + new Vector2(Mathf.Sin(t * 1.7f) * 3f, 2f + Mathf.Cos(t * 2.3f)) : pos + new Vector2(Mathf.Sin(t) * 2f, Mathf.Cos(t * 1.4f)), 3f, 9f, dt);
            dustT -= dt;
            if (HasTarget && dustT <= 0f)
            {
                dustT = 1.6f;
                // a puff of burning dust drifting down
                for (int i = 0; i < 3; i++)
                    G.Spawn(new Bullet(pos, new Vector2(Random.Range(-1.5f, 1.5f), -2f), Team.Beast, EnemyShotDamage, dmg / 3, "b_seed", new Color(0.7f, 1f, 0.4f), 0.6f, 2.5f));
            }
            Draw(Mathf.Sin(t * 10f) * 0.05f);
        }

        public override void AddLight(Lighting l) => l.Add(pos, 2.6f + Mathf.Sin(t * 5f) * 0.3f, 0.65f);
    }

    // ================================================================== CORAL REEF

    public class Urchin : Enemy
    {
        float spineT = 2f;
        public Urchin(int x, int y, int depth)
        {
            pos = new Vector2(x + 0.5f, y + 0.32f); half = new Vector2(0.3f, 0.3f);
            Setup("Sea Urchin", "urchin", 4, depth);
            gold = 2; blood = new Color(0.6f, 0.3f, 0.9f);
            waterLike = true;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(7f, true, dt);
            spineT -= dt;
            if (HasTarget && spineT <= 0f)
            {
                spineT = 2.8f;
                for (int i = 0; i < 6; i++)
                {
                    float a = (i * 60f + 30f) * Mathf.Deg2Rad;
                    G.Spawn(new Bullet(pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f, Team.Beast, EnemyShotDamage, dmg / 2, "b_arrow", new Color(0.7f, 0.4f, 1f), 0.6f, 1.2f));
                }
                G.sfx.Play("shoot", 0.3f, 1.6f);
            }
            animT += dt * 0.5f;
            Draw(0f);
        }
    }
}
