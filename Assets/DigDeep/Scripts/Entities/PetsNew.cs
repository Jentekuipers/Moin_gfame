using UnityEngine;

namespace DigDeep
{
    // Hand-drawn pets: the dog, the slime pal, the bone buddy, the baby dragon and the (very rare) ace fox.
    // Ground pets trot after you, hop up ledges and turn into a ghost to catch up when they get stuck.
    public abstract class PetWalker : Pet
    {
        protected static readonly Vector2 H = new Vector2(0.3f, 0.25f);
        protected bool grounded;
        float stuckT;
        protected PetWalker(Player p, string kind) : base(p, kind) { }

        // walk towards goalX at speed sp (hopping up steps); returns true if blocked by a wall
        protected bool WalkTo(float goalX, float sp, float dt, float hop = 11f)
        {
            float dx = goalX - pos.x;
            float s = Mathf.Abs(dx) > 0.35f ? sp : 0f;
            vel.x = U.Approach(vel.x, Mathf.Sign(dx) * s, 40f * dt);
            vel.y = Mathf.Max(vel.y - 40f * dt, -16f);
            Vector2 p = pos;
            bool blocked = Wd.MoveX(ref p, H, vel.x * dt, out _);
            if (Wd.MoveY(ref p, H, vel.y * dt, out _)) { if (vel.y < 0f) grounded = true; vel.y = 0f; } else grounded = false;
            pos = p;
            if (grounded && (blocked || owner.pos.y - pos.y > 1.5f && Mathf.Abs(owner.pos.x - pos.x) < 2.5f)) vel.y = hop;
            stuckT = Mathf.Abs(dx) > 1.5f && Mathf.Abs(vel.x) < 0.5f ? stuckT + dt : 0f;
            if (stuckT > 1.5f) { homingBack = true; stuckT = 0f; vel = Vector2.zero; }
            return blocked;
        }

        protected float Home => owner.pos.x - owner.facing * 1.3f;
    }

    // GOOD DOG: bites foes, and fetches coins lying about (it noses them over to you).
    public class PetDog : PetWalker
    {
        public PetDog(Player p) : base(p, "dog") { }

        protected override void Behave(float dt)
        {
            var prey = cd <= 0f ? Nearest(pos, 4.5f, true) : null;
            Coin fetch = null;
            if (prey == null)
            {
                float bd = 25f;
                foreach (var e in G.ents)
                    if (!e.dead && e is Coin c && (c.pos - pos).sqrMagnitude < bd && Mathf.Abs(c.pos.y - pos.y) < 1.5f) { bd = (c.pos - pos).sqrMagnitude; fetch = c; }
            }
            float goal = prey != null && Mathf.Abs(prey.pos.y - pos.y) < 1.5f ? prey.pos.x : fetch != null ? fetch.pos.x : Home;
            WalkTo(goal, prey != null || fetch != null ? 8.5f : Mathf.Abs(goal - pos.x) > 3f ? 7.5f : 4f, dt);
            if (prey != null && U.Overlap(pos, H + new Vector2(0.15f, 0.05f), prey.pos, prey.half))
            {
                Bite(prey, Dmg(1.8f), new Vector2(Mathf.Sign(prey.pos.x - pos.x) * 5f, 3f));
                cd = 0.9f; vel.y = 5f;
            }
            if (fetch != null && (fetch.pos - pos).sqrMagnitude < 0.5f) { fetch.pullTo = owner.pos; fetch.pullT = 2f; }
        }
    }

    // SLIME PAL: bounces along behind you; foes that come close get a spinning splash.
    public class PetSlime : PetWalker
    {
        float spinT;
        public PetSlime(Player p) : base(p, "slime") { }

        protected override void Behave(float dt)
        {
            if (spinT > 0f)
            {
                spinT -= dt;
                vel.x = 0f;
                WalkTo(pos.x, 0f, dt);
                if (((int)(spinT * 10f)) % 2 == 0)
                    foreach (var e in G.ents)
                        if (!e.dead && e is Enemy en && !en.Intangible && en.team != Team.Player && (en.pos - pos).sqrMagnitude < 1.6f * 1.6f)
                            Bite(en, Dmg(0.45f), (en.pos - pos).normalized * 4f);
                return;
            }
            WalkTo(Home, 6f, dt, 9f);
            if (grounded && Mathf.Abs(vel.x) > 1f && Random.value < dt * 3f) vel.y = 6f;   // it bounces as it goes
            if (cd <= 0f && Nearest(pos, 1.6f, false) != null) { spinT = 0.6f; cd = 2f; G.sfx.Play("splat", 0.4f, 1.4f); }
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (dead || spinT <= 0f) return;
            if (Art.Strips.TryGetValue("slimepet_attack", out var a) && a != null) sr.sprite = a[((int)(t * 14f)) % a.Length];
        }
    }

    // BONE BUDDY: a little skeleton that lobs bones at foes it can see.
    public class PetSkeleton : PetWalker
    {
        public PetSkeleton(Player p) : base(p, "skeleton") { }

        protected override void Behave(float dt)
        {
            WalkTo(Home, Mathf.Abs(Home - pos.x) > 3f ? 7.5f : 4f, dt);
            if (cd > 0f) return;
            var e = Nearest(pos, 7f, true);
            if (e == null) return;
            cd = 1.3f;
            Vector2 d = (e.pos - pos);
            facing = d.x > 0f ? 1 : -1;
            G.Spawn(Bullet.Pet(pos + Vector2.up * 0.2f, d.normalized * 14f, Dmg(1.4f), owner.ShotTint(new Color(0.95f, 0.95f, 0.85f))));
            Spent(Dmg(1.4f));
            G.sfx.Play("swing", 0.3f, 1.6f, 0.1f);
        }
    }

    // BABY DRAGON: trots along and breathes a short cone of fire at foes in front of it.
    public class PetDragon : PetWalker
    {
        float breathT, tickT;
        public PetDragon(Player p) : base(p, "dragon") { }

        protected override void Behave(float dt)
        {
            var prey = Nearest(pos, 3.5f, true);
            if (breathT > 0f)
            {
                breathT -= dt; tickT -= dt;
                WalkTo(pos.x, 0f, dt);
                Vector2 mouth = pos + new Vector2(facing * 0.4f, 0.15f);
                G.fx.Burst(mouth + new Vector2(facing * Random.Range(0.2f, 2f), Random.Range(-0.3f, 0.3f)), 2, new Color(1f, Random.Range(0.4f, 0.8f), 0.2f), 2f, 0.3f, -3f);
                if (tickT <= 0f)
                {
                    tickT = 0.15f;
                    foreach (var e in G.ents)
                    {
                        if (e.dead || !(e is Enemy en) || en.Intangible || en.team == Team.Player) continue;
                        Vector2 d = en.pos - mouth;
                        if (d.x * facing < 0f || d.magnitude > 2.4f || Mathf.Abs(d.y) > 1.1f) continue;
                        Bite(en, Dmg(0.5f), new Vector2(facing * 2f, 1f));
                        en.Ignite(2f);
                    }
                }
                return;
            }
            WalkTo(prey != null ? prey.pos.x - Mathf.Sign(prey.pos.x - pos.x) * 1.5f : Home, 6.5f, dt);
            if (prey != null && cd <= 0f)
            {
                facing = prey.pos.x > pos.x ? 1 : -1;
                breathT = 0.8f; cd = 2.4f;
                G.sfx.Play("flame", 0.4f, 1.3f);
            }
        }

        public override void AddLight(Lighting l) => l.Add(pos, breathT > 0f ? 3f : 1.3f, breathT > 0f ? 0.8f : 0.4f);
    }

    // ACE FOX: dashes at foes, slashes them hard, and fires its blaster at whatever's left.
    public class PetFox : PetWalker
    {
        Enemy dashAt;
        float dashT;
        public PetFox(Player p) : base(p, "fox") { }

        protected override void Behave(float dt)
        {
            if (dashAt != null)
            {
                dashT -= dt;
                if (dashAt.dead || dashT <= 0f) { dashAt = null; return; }
                Vector2 d = dashAt.pos - pos;
                vel = d.normalized * 18f;
                pos += vel * dt;
                if (Random.value < 0.8f) G.fx.Burst(pos, 1, new Color(1f, 0.75f, 0.3f), 1f, 0.2f, 0f);
                if (U.Overlap(pos, H + new Vector2(0.2f, 0.1f), dashAt.pos, dashAt.half))
                {
                    Bite(dashAt, Dmg(4f), d.normalized * 8f);
                    G.fx.Ring(dashAt.pos, 1.2f, new Color(1f, 0.85f, 0.4f));
                    G.sfx.Play("sword", 0.5f, 1.3f);
                    // and a blaster shot at the next one
                    var next = Nearest(pos, 8f, true);
                    if (next != null && next != dashAt)
                        G.Spawn(Bullet.Pet(pos, (next.pos - pos).normalized * 24f, Dmg(1.5f), owner.ShotTint(new Color(0.5f, 0.9f, 1f))));
                    dashAt = null; cd = 1.1f; vel = new Vector2(-Mathf.Sign(d.x) * 4f, 6f);
                }
                return;
            }
            WalkTo(Home, Mathf.Abs(Home - pos.x) > 3f ? 9f : 4.5f, dt);
            if (cd <= 0f)
            {
                var e = Nearest(pos, 6f, true);
                if (e != null) { dashAt = e; dashT = 0.6f; G.sfx.Play("dash", 0.4f, 1.5f); }
            }
        }
    }
}
