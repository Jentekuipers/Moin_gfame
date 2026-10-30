using UnityEngine;

namespace DigDeep
{
    // RAIDERS: surface treasure hunters who came down with guns. They shoot you, and they shoot the beasts,
    // and the beasts go for them. Let them fight, then clean up.
    public class Raider : Enemy
    {
        public enum Kind { Gunner, Brute, Marksman, Bomber }
        readonly Kind kind;
        readonly float speed, range;
        float fireT, hopT, telegraph, gunKick, navT, leapT;
        int navDir; bool navOn;
        bool grounded;
        Vector2 aimDir = Vector2.right;
        readonly Transform gun, laser;
        readonly SpriteRenderer gunSr, laserSr;

        static readonly string[] Keys = { "raider", "brute", "marksman", "bomber" };
        static readonly string[] Names = { "Raider", "Brute", "Marksman", "Bomber" };
        static readonly string[] Guns = { "revolver", "shotgun", "sniper", "grenade" };

        public Raider(int x, int y, int depth, Kind k)
        {
            kind = k;
            team = Team.Bandit;
            half = new Vector2(0.3f, 0.42f);
            pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
            Setup(Names[(int)k], Keys[(int)k], k == Kind.Brute ? 7 : k == Kind.Bomber ? 5 : 4, depth);
            gold = k == Kind.Gunner ? 4 : 6;
            blood = new Color(0.8f, 0.1f, 0.12f);
            speed = (k == Kind.Brute ? 1.6f : 2.1f) * (1f + depth / 600f);
            range = k == Kind.Marksman ? 17f : k == Kind.Brute ? 7f : k == Kind.Bomber ? 10f : 11f;
            fireT = 1f + Random.value * 1.5f;
            facing = Random.value < 0.5f ? -1 : 1;

            var g = new GameObject("RaiderGun");
            g.transform.SetParent(G.runRoot, false);
            gunSr = Art.AddSR(g, Art.Icons[Guns[(int)k]], 11);
            gun = g.transform;
            gun.localScale = Vector3.one * 0.8f;
            if (k == Kind.Marksman)
            {
                var lg = new GameObject("RaiderSight");
                lg.transform.SetParent(G.runRoot, false);
                laserSr = Art.AddSR(lg, Art.Pixel, 19);
                laserSr.enabled = false;
                laser = lg.transform;
            }
        }

        Vector2 MuzzlePos => pos + new Vector2(0f, 0.05f) + aimDir * 0.6f;

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(range + 2f, true, dt);
            bool has = HasTarget;
            vel.y = Mathf.Max(vel.y - 40f * dt, -16f);
            grounded = Wd.Ground(pos + new Vector2(0f, -0.05f), half);
            float want = 0f;
            if (has)
            {
                Vector2 d = TPos - pos;
                float dist = d.magnitude;
                bool locked = kind == Kind.Marksman && telegraph > 0f && telegraph < 0.2f;
                if (!locked)
                {
                    float turn = kind == Kind.Marksman && telegraph > 0f ? 2.5f : 9f;
                    aimDir = Vector2.Lerp(aimDir, d.normalized, 1f - Mathf.Exp(-dt * turn)).normalized;
                }
                facing = d.x > 0f ? 1 : -1;
                float near = kind == Kind.Brute ? 1.5f : kind == Kind.Marksman ? 7f : 4f;
                float far = kind == Kind.Brute ? 4f : kind == Kind.Marksman ? 13f : 8f;
                bool sight = Wd.LineClear(MuzzlePos, TPos);
                if (dist > far || !sight)
                {
                    // out of range or out of sight: work round the rock towards you
                    navT -= dt;
                    if (navT <= 0f) { navT = 0.4f; navOn = GroundNav.Step(new Vector2(pos.x, pos.y - half.y), TPos + Vector2.down * (Player.Half.y - 0.05f), false, out navDir, out _) && navDir != 0; }
                    want = navOn ? navDir : facing;
                }
                else if (dist < near) want = -facing;
                if (telegraph > 0f) want = 0f;
                // jump for a better shot (or to get up to you)
                if (grounded && hopT <= 0f && telegraph <= 0f && (d.y > 1.5f || (!sight && Random.value < dt * 1.2f)) && Random.value < dt * 2.5f)
                { vel.y = d.y > 1.5f ? 12f : 10f; hopT = 1.1f; }
                Fire(dt, d, dist);
            }
            else
            {
                telegraph = 0f;
                want = facing * 0.5f;
                aimDir = Vector2.Lerp(aimDir, new Vector2(facing, -0.2f).normalized, dt * 4f).normalized;
            }

            if (drowning) { want = escapeDir; vel.y = U.Approach(vel.y, 4.5f, 60f * dt); }   // swim up and out
            if (leapT > 0f) { leapT -= dt; want = 0f; }   // mid-leap: it keeps its momentum
            else vel.x = U.Approach(vel.x, want * speed * WaterSlow, 20f * dt);
            Vector2 p = pos;
            if (Wd.MoveX(ref p, half, vel.x * dt, out _))
            {
                if (grounded && want != 0f && hopT <= 0f && has) { vel.y = 10f; hopT = 0.7f; }
                else if (!has) facing = -facing;
                vel.x = 0f;
            }
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) vel.y = 0f;
            pos = p;
            hopT -= dt;
            if (grounded && Mathf.Abs(want) > 0.01f)
            {
                int fx = Mathf.FloorToInt(pos.x + Mathf.Sign(want) * (half.x + 0.1f));
                int fy = Mathf.FloorToInt(pos.y - half.y - 0.2f);
                Block below = Wd.Get(fx, fy);
                if (!Tiles.Solid(below) && (Wd.LiquidType(fx, fy) == Fluids.Lava || !Wd.Solid(fx, fy - 1) || !has))
                {
                    // a gap: leap across to the next ledge if there's one in reach, else stop at the edge
                    if (hopT <= 0f && (has || Random.value < 0.3f) && LeapGap((int)Mathf.Sign(want))) { }
                    else
                    {
                        vel.x = 0f;
                        if (!has) facing = -facing;
                    }
                }
            }
            gunKick = Mathf.MoveTowards(gunKick, 0f, dt * 2.5f);
            Draw(-half.y);
            RenderGun();
        }

        // a spot to land on across the gap (2-5 blocks off, a little up or down): jump for it
        bool LeapGap(int dir)
        {
            int fy = Mathf.FloorToInt(pos.y - half.y + 0.05f);
            int x0 = Mathf.FloorToInt(pos.x);
            for (int dist = 2; dist <= 5; dist++)
                for (int dy = 1; dy >= -3; dy--)
                {
                    int cx = x0 + dir * dist, cy = fy + dy;
                    if (!Wd.Solid(cx, cy - 1) || Wd.Solid(cx, cy) || Wd.Solid(cx, cy + 1)) continue;
                    if (Wd.LiquidType(cx, cy) == Fluids.Lava || Wd.Get(cx, cy - 1) == Block.Spikes) continue;
                    // a clear flight path over the gap
                    if (Wd.Solid(x0, fy + 2) || Wd.Solid(x0 + dir, fy + 2)) continue;
                    float t = 0.45f + dist * 0.07f;
                    float tx = cx + 0.5f, ty = cy + half.y + 0.05f;
                    vel.x = (tx - pos.x) / t;
                    vel.y = Mathf.Min(15f, (ty - pos.y + 0.5f * 40f * t * t) / t);
                    leapT = t; hopT = t + 0.3f;
                    facing = dir;
                    return true;
                }
            return false;
        }

        void Fire(float dt, Vector2 d, float dist)
        {
            fireT -= dt;
            bool sees = dist < range;
            switch (kind)
            {
                case Kind.Gunner:
                    if (fireT <= 0f && sees)
                    {
                        fireT = Random.Range(0.9f, 1.4f) * (depth > 150 ? 0.75f : 1f);
                        Shoot(13f, 7f);
                        G.Spawn(new FxDelay(0.13f, () => { if (!dead) Shoot(13f, 9f); }));
                    }
                    break;
                case Kind.Brute:
                    if (fireT <= 0f && sees)
                    {
                        fireT = 1.9f;
                        for (int i = 0; i < 5; i++)
                        {
                            var b = new Bullet(MuzzlePos, U.Rot(aimDir, Random.Range(-17f, 17f)) * Random.Range(10.5f, 13f), Team.Bandit, EnemyShotDamage, dmg * 45 / 100,
                                "b_enemy", new Color(1f, 0.45f, 0.2f), 0.9f, 0.55f);
                            G.Spawn(b);
                        }
                        Feel(0.12f, 0.7f);
                    }
                    break;
                case Kind.Marksman:
                    if (telegraph > 0f)
                    {
                        telegraph -= dt;
                        if (telegraph <= 0f)
                        {
                            fireT = 2.6f;
                            G.Spawn(new Bullet(MuzzlePos, aimDir * 30f, Team.Bandit, EnemyShotDamage * 2f, dmg * 16 / 10, "b_enemy", new Color(1f, 0.3f, 0.3f), 1.2f, 1.5f));
                            Feel(0.15f, 0.55f);
                        }
                    }
                    else if (fireT <= 0f && sees)
                    {
                        telegraph = 0.85f;
                        G.sfx.Play("blip", 0.35f, 1.6f, 0f);
                    }
                    break;
                case Kind.Bomber:
                    if (fireT <= 0f && sees)
                    {
                        fireT = 2.4f;
                        float t = Mathf.Clamp(dist / 9f, 0.45f, 1f);
                        const float grav = 22f;
                        Vector2 v = new Vector2(d.x / t, d.y / t + 0.5f * grav * t);
                        G.Spawn(new Bullet(MuzzlePos, v, Team.Bandit, EnemyShotDamage * 2f, dmg, "b_grenade", new Color(1f, 0.5f, 0.2f), 1f, 1.6f).Lobbed(grav, 1.4f));
                        Feel(0.08f, 0.5f);
                    }
                    break;
            }
        }

        void Shoot(float speed, float spread)
        {
            G.Spawn(new Bullet(MuzzlePos, U.Rot(aimDir, Random.Range(-spread, spread) * 0.5f) * speed, Team.Bandit, EnemyShotDamage, dmg,
                "b_enemy", new Color(1f, 0.45f, 0.2f)));
            Feel(0.06f, 1f);
        }

        void Feel(float shake, float pitch)
        {
            gunKick = 0.18f;
            G.fx.Muzzle(MuzzlePos, 0.7f, new Color(1f, 0.6f, 0.3f));
            G.sfx.Play("eshot", 0.45f, pitch, 0.1f);
            G.lighting.Flash(MuzzlePos, 2f, 0.05f);
            if ((G.player.pos - pos).sqrMagnitude < 150f) G.cam.Shake(shake * 0.5f);
        }

        void RenderGun()
        {
            Vector2 tp = pos + new Vector2(0f, 0.05f) + aimDir * (0.35f - gunKick);
            gun.position = new Vector3(U.Snap(tp.x), U.Snap(tp.y), 0);
            gun.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg);
            gunSr.flipY = aimDir.x < 0f;
            gunSr.color = sr.color;
            if (laserSr == null) return;
            if (telegraph > 0f)
            {
                Vector2 o = MuzzlePos;
                float len = Wd.RayDist(o, aimDir, 20f);
                laserSr.enabled = true;
                laser.position = (Vector3)(o + aimDir * len * 0.5f);
                laser.rotation = gun.rotation;
                laser.localScale = new Vector3(len * 16f, telegraph < 0.2f ? 2f : 1f, 1f);
                bool blink = telegraph < 0.2f && ((int)(Time.time * 30f)) % 2 == 0;
                laserSr.color = blink ? Color.white : new Color(1f, 0.15f, 0.15f, 0.75f);
            }
            else laserSr.enabled = false;
        }

        public override void AddLight(Lighting l)
        {
            base.AddLight(l);
            if (telegraph > 0f) l.Add(MuzzlePos, 1.4f, 0.7f);
        }

        public override void Kill()
        {
            base.Kill();
            if (gun != null) Object.Destroy(gun.gameObject);
            if (laser != null) Object.Destroy(laser.gameObject);
        }
    }

    // MIRROR BEETLE: every few seconds it hunkers down under a shining shell that bats bullets straight back.
    // Wait it out, blow it up, or get in close with a blade.
    public class Beetle : Enemy
    {
        float shieldT, cycleT, pulse;
        readonly float speed;
        readonly Transform ring;
        readonly SpriteRenderer ringSr;

        public override bool Reflects => shieldT > 0f;
        public override void OnReflect() { pulse = 0.12f; }

        public Beetle(int x, int y, int depth)
        {
            half = new Vector2(0.4f, 0.32f);
            pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
            Setup("Mirror Beetle", "beetle", 6, depth);
            gold = 5;
            blood = new Color(0.35f, 0.75f, 0.95f);
            speed = 1.5f * (1f + depth / 500f);
            cycleT = 1f + Random.value * 2f;
            facing = Random.value < 0.5f ? -1 : 1;
            var g = new GameObject("Shell");
            g.transform.SetParent(G.runRoot, false);
            ringSr = Art.AddSR(g, Art.Ring, 12);
            ringSr.enabled = false;
            ring = g.transform;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(10f, false, dt);
            cycleT -= dt; shieldT -= dt; pulse -= dt;
            float dist = HasTarget ? (TPos - pos).magnitude : 99f;
            if (shieldT <= 0f && cycleT <= 0f && dist < 10f)
            {
                shieldT = 1.6f;
                cycleT = shieldT + 2.2f + Random.value * 1.5f;
                G.sfx.Play("shield", 0.5f);
                G.fx.Ring(pos, 1.2f, new Color(0.6f, 1f, 1f));
            }
            float sp = 0f;
            if (shieldT <= 0f)
            {
                sp = speed * WaterSlow;
                if (HasTarget && Mathf.Abs(TPos.y - pos.y) < 1.2f) { facing = TPos.x > pos.x ? 1 : -1; sp *= 1.4f; }
            }
            vel.x = U.Approach(vel.x, facing * sp, 20f * dt);
            vel.y = Mathf.Max(vel.y - 40f * dt, -16f);
            Vector2 p = pos;
            if (Wd.MoveX(ref p, half, vel.x * dt, out _))
            {
                bool chasing = HasTarget && Mathf.Abs(TPos.x - pos.x) < 6f && TPos.y > pos.y - 1.5f;
                if (chasing && (TryHop(p) || vel.y > 0.5f)) { }   // hop up one-block steps after its prey
                else { facing = -facing; vel.x = 0f; }
            }
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) vel.y = 0f;
            pos = p;
            if (Wd.BoxSolid(pos + new Vector2(0, -0.05f), half))
            {
                int fx = Mathf.FloorToInt(pos.x + facing * (half.x + 0.1f));
                int fy = Mathf.FloorToInt(pos.y - half.y - 0.2f);
                if (!Wd.Solid(fx, fy)) { facing = -facing; vel.x = 0f; }
            }
            if (shieldT > 0f) animT = 0f;
            Draw(-half.y);
            if (shieldT > 0f)
            {
                sr.color = Color.Lerp(new Color(0.75f, 1f, 1f), Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 25f));
                ringSr.enabled = true;
                float s = 1.25f + 0.1f * Mathf.Sin(Time.time * 12f) + (pulse > 0f ? 0.3f : 0f);
                ring.localScale = new Vector3(s, s, 1f);
                ring.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y), 0);
                ringSr.color = new Color(0.6f, 1f, 1f, Mathf.Clamp01(shieldT * 3f) * 0.85f);
            }
            else ringSr.enabled = false;
        }

        public override void AddLight(Lighting l)
        {
            base.AddLight(l);
            if (shieldT > 0f) l.Add(pos, 2.2f, 0.7f);
        }

        public override void Kill()
        {
            base.Kill();
            if (ring != null) Object.Destroy(ring.gameObject);
        }
    }
}
