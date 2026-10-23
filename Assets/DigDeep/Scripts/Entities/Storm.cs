using UnityEngine;

namespace DigDeep
{
    // STORM CAVERNS: everything here either makes electricity or dies to it.

    // EEL: lives in the water. Every few seconds it lights the whole pool up, each burst stronger than the last.
    // Fished out of the water it flops about and slowly dies.
    public class Eel : Enemy
    {
        float zapT, strength = 0.3f, outT, t;
        public override bool ElectricProof => true;

        public Eel(int x, int y, int depth)
        {
            half = new Vector2(0.45f, 0.2f);
            pos = new Vector2(x + 0.5f, y + 0.5f);
            Setup("Eel", "eel", 3, depth);
            gold = 4;
            blood = new Color(1f, 0.9f, 0.4f);
            zapT = Random.Range(1.5f, 4f);
            t = Random.value * 6f;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt;
            FindTarget(9f, false, dt);
            bool wet = Wd.FluidAtPoint(pos) == Fluids.Water;
            if (wet)
            {
                outT = 0f;
                bool preyWet = HasTarget && Wd.FluidAtPoint(TPos) == Fluids.Water;
                Vector2 goal = preyWet ? TPos : pos + new Vector2(Mathf.Sin(t * 0.8f) * 3f, Mathf.Cos(t * 0.5f));
                Vector2 want = (goal - pos).normalized * (preyWet ? 3.4f : 1.6f);
                want.y += Mathf.Sin(t * 6f) * 0.8f;
                vel = Vector2.MoveTowards(vel, want, 10f * dt);
                // it won't swim out of the water
                if (Wd.FluidAtPoint(pos + new Vector2(0f, half.y + 0.1f)) != Fluids.Water && vel.y > 0f) vel.y = -0.5f;
                zapT -= dt;
                if (zapT < 0.5f && Random.value < 0.5f) G.fx.Burst(pos, 1, new Color(1f, 0.95f, 0.5f), 2f, 0.15f, 0f);   // the tell: it sparks before it fires
                if (zapT <= 0f)
                {
                    zapT = Random.Range(2.5f, 5f);
                    Electric.Zap(pos, strength, false);
                    strength = Mathf.Min(1f, strength + 0.2f);
                    G.fx.Ring(pos, 1.2f, new Color(1f, 0.95f, 0.5f));
                }
            }
            else
            {
                // flopping on dry land
                outT += dt;
                vel.y = Mathf.Max(vel.y - 30f * dt, -14f);
                if (Wd.BoxSolid(pos + new Vector2(0f, -0.06f), half) && Random.value < 2f * dt) { vel = new Vector2(Random.Range(-3f, 3f), 6f); G.sfx.Play("splat", 0.2f, 1.6f); }
                hp -= 2.5f * dt;
                if (hp <= 0f) { Die(); return; }
            }
            Vector2 p = pos;
            if (Wd.MoveX(ref p, half, vel.x * dt, out _)) vel.x = -vel.x * 0.5f;
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) vel.y = 0f;
            pos = p;
            if (Mathf.Abs(vel.x) > 0.2f) facing = vel.x > 0f ? 1 : -1;
            Draw(0f);
        }

        public override void AddLight(Lighting l) { base.AddLight(l); l.Add(pos, 1.8f, zapT < 0.5f ? 0.9f : 0.4f); }
    }

    // CLANK: a walking machine. Wading through water, it charges it up around itself.
    public class Clank : Enemy
    {
        float chargeT = 1f;
        readonly float speed;
        public override bool Mechanical => true;
        public override bool ElectricProof => true;

        public Clank(int x, int y, int depth)
        {
            half = new Vector2(0.4f, 0.36f);
            pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
            Setup("Clank", "clank", 4, depth);
            gold = 4;
            blood = new Color(0.3f, 0.3f, 0.36f);
            speed = 1.6f * (1f + depth / 600f);
            facing = Random.value < 0.5f ? -1 : 1;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(8f, false, dt);
            float sp = speed;
            if (HasTarget && Mathf.Abs(TPos.y - pos.y) < 1.2f && Mathf.Abs(TPos.x - pos.x) < 7f) { facing = TPos.x > pos.x ? 1 : -1; sp *= 1.4f; }
            vel.x = U.Approach(vel.x, facing * sp, 20f * dt);
            vel.y = Mathf.Max(vel.y - 40f * dt, -16f);
            Vector2 p = pos;
            if (Wd.MoveX(ref p, half, vel.x * dt, out _))
            {
                bool chasing = HasTarget && Mathf.Abs(TPos.x - pos.x) < 6f && TPos.y > pos.y - 1.5f;
                if (chasing && (TryHop(p) || vel.y > 0.5f)) { }
                else { facing = -facing; vel.x = 0f; }
            }
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) vel.y = 0f;
            pos = p;
            // machines in water make electricity
            if (Wd.FluidAtPoint(pos) == Fluids.Water || Wd.FluidAtPoint(pos - new Vector2(0f, half.y - 0.1f)) == Fluids.Water)
            {
                chargeT -= dt;
                if (Random.value < 0.3f) G.fx.Burst(pos + Vector2.up * 0.3f, 1, new Color(1f, 0.95f, 0.5f), 2f, 0.15f, 0f);
                if (chargeT <= 0f) { chargeT = Random.Range(1.8f, 3f); Electric.Zap(pos - new Vector2(0f, half.y - 0.1f), 0.45f, false); }
            }
            Draw(-half.y);
        }
    }

    // DRONE: hovers at range and spits sparks that shock you. Its battery is volatile: electricity blows it up.
    public class Drone : Enemy
    {
        float shootT = 2f, t, navT;
        Vector2 navPt;
        bool navOk;
        public override bool Mechanical => true;
        public override bool Volatile => true;

        public Drone(int x, int y, int depth)
        {
            half = new Vector2(0.36f, 0.28f);
            pos = new Vector2(x + 0.5f, y + 0.5f);
            Setup("Drone", "drone", 2, depth);
            gold = 4;
            blood = new Color(0.3f, 0.3f, 0.36f);
            t = Random.value * 6f;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt;
            FindTarget(11f, true, dt);
            Vector2 goal = pos + new Vector2(Mathf.Sin(t) * 1.5f, Mathf.Cos(t * 0.7f) * 0.5f);
            if (HasTarget)
            {
                Vector2 d = TPos - pos;
                goal = TPos + new Vector2(d.x > 0 ? -4f : 4f, 2.5f);
                if (Wd.Solid(Mathf.FloorToInt(goal.x), Mathf.FloorToInt(goal.y))) goal = TPos + Vector2.up * 2f;
                navT -= dt;
                if (navT <= 0f) { navT = 0.3f; navOk = Nav.Steer(pos, goal, out navPt); }
                if (navOk) goal = navPt;
                shootT -= dt;
                if (shootT <= 0f && d.magnitude < 10f && Wd.LineClear(pos, TPos))
                {
                    shootT = 2.2f;
                    var b = new Bullet(pos, d.normalized * 7.5f, Team.Beast, EnemyShotDamage, Mathf.RoundToInt(14f + depth * 0.07f), "b_zap", new Color(0.7f, 0.95f, 1f));
                    b.zaps = true;
                    G.Spawn(b);
                    G.sfx.Play("zap", 0.4f, 1.3f);
                }
            }
            Vector2 want = goal - pos;
            if (want.sqrMagnitude > 0.01f) want = want.normalized * 3.4f;
            want.y += Mathf.Sin(t * 4f) * 0.6f;
            vel = Vector2.MoveTowards(vel, want, 12f * dt);
            Vector2 p = pos;
            if (Wd.MoveX(ref p, half, vel.x * dt, out _)) { vel.x *= -0.5f; navT = 0f; }
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) { vel.y *= -0.5f; navT = 0f; }
            pos = p;
            if (Mathf.Abs(vel.x) > 0.1f) facing = vel.x > 0 ? 1 : -1;
            Draw(0f);
        }

        public override void AddLight(Lighting l) { base.AddLight(l); l.Add(pos, 1.6f, 0.5f); }
    }

    // TESLA COIL: a hazard bolted to the floor. It hums, glows, then throws lightning at whatever is close
    // (you, foes, and the nearest water, which it charges harder every time). Shoot it to break it.
    public class TeslaCoil : Enemy
    {
        float cycle, strength = 0.3f;
        public override bool ElectricProof => true;
        public override bool Mechanical => true;

        public TeslaCoil(int x, int y, int depth)
        {
            half = new Vector2(0.35f, 0.5f);
            pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
            Setup("Tesla Coil", "tesla", 8, depth);
            gold = 5;
            blood = new Color(0.85f, 0.95f, 1f);
            cycle = Random.Range(1.5f, 4.5f);
        }

        Vector2 Top => pos + Vector2.up * (half.y - 0.1f);

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            vel = Vector2.zero;
            cycle -= dt;
            bool hot = cycle < 0.7f;
            if (hot)
            {
                if (Random.value < 0.5f) G.fx.Burst(Top, 1, new Color(1f, 0.95f, 0.6f), 2.5f, 0.12f, 0f);
                if (Random.value < dt * 6f) G.sfx.Play("blip", 0.12f, 2.2f, 0f);
            }
            if (cycle <= 0f) { cycle = Random.Range(2.5f, 5f); Discharge(); }
            animT = hot ? 0.17f : 0f;
            Draw(-half.y);
        }

        void Discharge()
        {
            bool any = false;
            int depthNow = Mathf.Max(0, depth);
            var p = G.player;
            if (p.mode != Player.Mode.Dead && (p.pos - Top).magnitude < 3.6f && Wd.LineClear(Top, p.pos))
            {
                G.Spawn(new LightningArc(Top, p.pos));
                p.Shock(Mathf.RoundToInt(18f + depthNow * 0.08f));
                any = true;
            }
            foreach (var e in G.ents)
            {
                if (e.dead || e == this || !(e is Enemy en) || en.ElectricProof || (en.pos - Top).magnitude > 3.6f) continue;
                G.Spawn(new LightningArc(Top, en.pos));
                en.Shock(0.6f, 4f + depthNow * 0.04f);
                any = true;
            }
            // the nearest water within reach takes the charge
            Vector2Int best = new Vector2Int(int.MinValue, 0);
            float bd = 99f;
            int cx = Mathf.FloorToInt(Top.x), cy = Mathf.FloorToInt(Top.y);
            for (int dy = -4; dy <= 2; dy++)
                for (int dx = -4; dx <= 4; dx++)
                {
                    if (Wd.LiquidType(cx + dx, cy + dy) != Fluids.Water) continue;
                    float d = dx * dx + dy * dy;
                    if (d < bd) { bd = d; best = new Vector2Int(cx + dx, cy + dy); }
                }
            if (best.x != int.MinValue)
            {
                Vector2 w = new Vector2(best.x + 0.5f, best.y + 0.5f);
                G.Spawn(new LightningArc(Top, w));
                Electric.Zap(w, strength, false);
                strength = Mathf.Min(1f, strength + 0.2f);
                any = true;
            }
            if (!any) G.Spawn(new LightningArc(Top, Top + Random.insideUnitCircle.normalized * 1.6f));
            G.sfx.Play("shock", 0.5f, 0.9f, 0.1f);
            G.lighting.Flash(Top, 5f, 0.2f);
        }

        public override void AddLight(Lighting l) { base.AddLight(l); l.Add(Top, 2.2f, cycle < 0.7f ? 1f : 0.45f); }
    }
}
