using UnityEngine;

namespace DigDeep
{
    // SNAPPER (jungle): a rooted flytrap. It gapes when you come close, then snaps shut on whatever is in front.
    // Plants burn three times as fast.
    public class Snapper : Enemy
    {
        float biteT, cd;
        bool biting;
        public override bool Plant => true;
        // rooted to a floor (normal up), a wall (sideways) or a ceiling (upside down)
        Vector2Int normal = Vector2Int.up, root;
        bool rooted;

        // a living seed struck rock: a snapper grows out of that surface, inside the arena
        public static void Grow(Vector2Int cell, Vector2Int n, BossArena a)
        {
            if (a == null || !a.Active || cell.x < a.MinX || cell.x >= a.MaxX || cell.y < a.FloorY || cell.y >= a.CeilY) return;
            if (Game.I.world.Solid(cell.x, cell.y)) return;
            foreach (var e in Game.I.ents) if (!e.dead && e is Snapper o && (o.pos - new Vector2(cell.x + 0.5f, cell.y + 0.5f)).sqrMagnitude < 1.2f) return;
            var s = new Snapper(cell.x, cell.y, a.Depth);
            s.normal = n; s.root = cell - n; s.rooted = true;
            Vector2 nf = new Vector2(n.x, n.y);
            s.pos = new Vector2(cell.x + 0.5f, cell.y + 0.5f) - nf * (0.5f - s.half.y - 0.002f);
            if (n.x != 0) s.half = new Vector2(s.half.y, s.half.x);
            Game.I.Spawn(s);
            Game.I.fx.Burst(s.pos, 14, new Color(0.6f, 1f, 0.4f), 5f, 0.4f, 0f);
            Game.I.sfx.Play("break", 0.5f, 1.4f);
        }

        public Snapper(int x, int y, int depth)
        {
            half = new Vector2(0.42f, 0.4f);
            pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
            Setup("Snapper", "snapper", 3, depth);
            gold = 3;
            blood = new Color(0.45f, 0.9f, 0.35f);
            facing = Random.value < 0.5f ? -1 : 1;
            cd = Random.Range(0.3f, 1.2f);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(5f, true, dt);
            cd -= dt;
            if (rooted && normal != Vector2Int.up) { RootedTick(dt); return; }
            vel.x = 0f;
            vel.y = Mathf.Max(vel.y - 40f * dt, -16f);
            Vector2 p = pos;
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) vel.y = 0f;
            pos = p;

            if (!biting && HasTarget && cd <= 0f)
            {
                Vector2 d = TPos - pos;
                if (Mathf.Abs(d.x) < 2.3f && Mathf.Abs(d.y) < 1.3f)
                {
                    biting = true;
                    biteT = 0.38f;   // the tell: it gapes and shivers
                    facing = d.x > 0 ? 1 : -1;
                    G.sfx.Play("bat", 0.5f, 0.6f);
                }
            }
            float yOff = -half.y;
            if (biting)
            {
                biteT -= dt;
                yOff += Random.Range(-0.03f, 0.03f);
                if (biteT <= 0f)
                {
                    biting = false;
                    cd = 1.4f;
                    Vector2 c = pos + new Vector2(facing * 1.0f, 0.1f), h = new Vector2(0.85f, 0.55f);
                    var pl = G.player;
                    if (pl.mode != Player.Mode.Dead && U.Overlap(c, h, pl.pos, Player.Half)) pl.Hurt(Mathf.RoundToInt(dmg * 1.3f), pos);
                    G.fx.Burst(c, 8, new Color(0.6f, 1f, 0.4f), 4f, 0.25f, 10f);
                    G.sfx.Play("hammer", 0.5f, 1.6f);
                }
            }
            animT = biting ? 0.17f : 0f;   // frame 0 closed, frame 1 open
            Draw(yOff);
        }

        // on a wall or the ceiling: it stays put, and bites along the surface
        void RootedTick(float dt)
        {
            if (!Wd.Solid(root.x, root.y)) { Die(); return; }   // the rock it grew from is gone
            Vector2 n = new Vector2(normal.x, normal.y), tan = new Vector2(normal.y, -normal.x);
            vel = Vector2.zero;
            if (!biting && HasTarget && cd <= 0f)
            {
                Vector2 d = TPos - pos;
                float along = Vector2.Dot(d, tan), up = Vector2.Dot(d, n);
                if (Mathf.Abs(along) < 2.3f && up > -0.6f && up < 1.6f)
                {
                    biting = true; biteT = 0.38f;
                    facing = along > 0 ? 1 : -1;
                    G.sfx.Play("bat", 0.5f, 0.6f);
                }
            }
            float shake = 0f;
            if (biting)
            {
                biteT -= dt;
                shake = Random.Range(-0.03f, 0.03f);
                if (biteT <= 0f)
                {
                    biting = false; cd = 1.4f;
                    Vector2 c = pos + tan * facing * 1f + n * 0.1f;
                    Vector2 h = normal.x != 0 ? new Vector2(0.55f, 0.85f) : new Vector2(0.85f, 0.55f);
                    var pl = G.player;
                    if (pl.mode != Player.Mode.Dead && U.Overlap(c, h, pl.pos, Player.Half)) pl.Hurt(Mathf.RoundToInt(dmg * 1.3f), pos);
                    G.fx.Burst(c, 8, new Color(0.6f, 1f, 0.4f), 4f, 0.25f, 0f);
                    G.sfx.Play("hammer", 0.5f, 1.6f);
                }
            }
            animT = biting ? 0.17f : 0f;
            Draw(0f);
            if (dead || go == null) return;
            Vector2 baseP = pos - n * (half.x < half.y ? half.x : half.y) + n * shake;
            go.transform.position = new Vector3(U.Snap(baseP.x), U.Snap(baseP.y), 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg - 90f);
        }
    }

    // SEED POD (jungle): a purple bulb hanging under the ceiling, spitting poison seeds at you.
    public class SeedPod : Enemy
    {
        float shootT, sway;
        int anchorX, anchorY;
        public override bool Plant => true;

        // kicked loose: it grabs onto a ceiling where it lands, or withers
        protected override void OnLaunchEnd()
        {
            int x = Mathf.FloorToInt(pos.x), y = Mathf.FloorToInt(pos.y);
            if (Wd.Solid(x, y + 1)) { anchorX = x; anchorY = y + 1; pos = new Vector2(x + 0.5f, y + 0.55f); }
            else Die();
        }

        public SeedPod(int x, int y, int depth)
        {
            half = new Vector2(0.36f, 0.36f);
            pos = new Vector2(x + 0.5f, y + 0.55f);
            anchorX = x; anchorY = y + 1;
            Setup("Seed Pod", "seedpod", 2, depth);
            gold = 2;
            blood = new Color(0.75f, 0.4f, 1f);
            shootT = Random.Range(1f, 2.5f);
            sway = Random.value * 6f;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            if (!Wd.Solid(anchorX, anchorY)) { Die(); return; }   // its stem was cut
            FindTarget(9f, true, dt);
            sway += dt;
            if (HasTarget)
            {
                shootT -= dt;
                if (shootT <= 0f)
                {
                    shootT = 2.6f;
                    Vector2 d = (TPos - pos).normalized;
                    for (int i = -1; i <= 1; i++)
                    {
                        var b = new Bullet(pos + Vector2.down * 0.2f, U.Rot(d, i * 14f) * 6.5f, Team.Beast, EnemyShotDamage, dmg * 6 / 10, "b_seed", new Color(0.75f, 0.4f, 1f));
                        b.poison = true;
                        G.Spawn(b);
                    }
                    G.sfx.Play("shoot", 0.5f, 1.4f);
                    animT = 0.17f;
                }
            }
            animT = shootT > 2.3f ? 0.17f : 0f;
            Draw(Mathf.Sin(sway * 2f) * 0.04f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(sway * 1.6f) * 8f);
        }
    }
}
