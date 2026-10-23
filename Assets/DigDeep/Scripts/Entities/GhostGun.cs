using UnityEngine;

namespace DigDeep
{
    // PHANTOM ARSENAL: a see-through copy of the gun you were holding drifts slowly out of your hands and keeps firing
    // that gun's own shots at the cursor for a few seconds (no ammo needed).
    public class GhostGun : Entity
    {
        public static int Alive;
        readonly Player owner;
        readonly Weapon w;
        readonly Vector2 drift;
        float life = 3f, cd = 0.15f;
        bool counted;

        public override bool AlwaysActive => true;

        public GhostGun(Player p, Weapon w, Vector2 at, Vector2 dir)
        {
            owner = p; this.w = w;
            pos = at;
            drift = dir * 3.5f;
            MakeGO("GhostGun", w.def.icon, 21);
            sr.color = new Color(0.75f, 0.85f, 1f, 0.6f);
            Alive++; counted = true;
            G.fx.Burst(pos, 10, new Color(0.75f, 0.85f, 1f), 3f, 0.4f, 0f);
            G.sfx.Play("charge", 0.3f, 2f, 0f);
        }

        public override void Tick(float dt)
        {
            life -= dt; cd -= dt;
            if (life <= 0f || owner != G.player || owner.mode == Player.Mode.Dead) { G.fx.Burst(pos, 10, new Color(0.75f, 0.85f, 1f), 3f, 0.4f, 0f); Kill(); return; }
            Vector2 next = pos + drift * dt;
            if (Wd.Get(Mathf.FloorToInt(next.x), Mathf.FloorToInt(next.y)) != Block.Bedrock) pos = next;
            Vector2 aim = G.aim - pos;
            Vector2 dir = aim.sqrMagnitude > 0.01f ? aim.normalized : Vector2.right;
            if (cd <= 0f && !w.def.Melee && w.def.type != WType.Rail)
            {
                cd = Mathf.Clamp(w.def.delay * 1.3f, 0.22f, 0.9f);
                var d = w.def;
                for (int i = 0; i < d.count; i++)
                {
                    Vector2 v = U.Rot(dir, Random.Range(-d.spread, d.spread) * 0.5f) * d.speed * (1f + Random.Range(-d.speedVar, d.speedVar));
                    G.Spawn(new Bullet(owner, w, pos + dir * 0.4f, v, false));
                }
                G.fx.Muzzle(pos + dir * 0.45f, 0.6f, new Color(0.75f, 0.85f, 1f));
                G.sfx.Play(d.sfx, 0.3f, d.pitch * 1.15f, 0.1f);
            }
            float a = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0f, 0f, a);
            sr.flipY = dir.x < 0f;
            var c = sr.color; c.a = (life < 0.6f ? life / 0.6f : 1f) * (0.5f + 0.15f * Mathf.Sin(life * 12f));
            sr.color = c;
            go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y), 0f);
        }

        public override void Kill()
        {
            if (dead) return;
            base.Kill();
            if (counted) { Alive--; counted = false; }
        }

        public override void AddLight(Lighting l) => l.Add(pos, 1.6f, 0.4f);
    }
}
