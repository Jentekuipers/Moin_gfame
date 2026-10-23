using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // PETS: trinkets that follow you around and fight for you, each in its own way. They never get in your way:
    // they don't block you, can't be hurt, fly through rock (except the beetle, which walks) and never touch the world
    // (no digging, no TNT, no burning grass). Their damage scales with yours.
    public static class Pets
    {
        public static Pet Make(string kind, Player p)
        {
            switch (kind)
            {
                case "bat": return new PetBat(p);
                case "wisp": return new PetWisp(p);
                case "beetle": return new PetBeetle(p);
                case "orb": return new PetOrb(p);
                case "spore": return new PetSpore(p);
                case "frost": return new PetFrost(p);
                case "disc": return new PetDisc(p);
                case "magpie": return new PetMagpie(p);
            }
            return null;
        }

        // pets pass on your POISON and FROST shot trinkets, and VENOM TREATS / EMBER TREATS make them poison or burn
        // (only the foe: they never light the grass themselves)
        public static void Status(Enemy en, float dmg)
        {
            var s = Game.I.player.s;
            if (en.dead) return;
            if (s.shotPoison || s.petPoison) en.Poison(3f, Mathf.Max(0.3f, dmg * 0.35f));
            if (s.shotFrost) en.Freeze(1f);
            if (s.petFire) en.Ignite(2f);
        }
    }

    public abstract class Pet : Entity
    {
        public readonly string kind;
        protected readonly Player owner;
        protected Vector2 vel;
        protected float t, cd;
        protected int facing = 1;
        readonly Sprite[] frames;
        // a temporary pet (SPIRIT LANTERN, FLY SWARM) fades once it has dealt its share of damage or its time is up
        protected bool temp;
        float budget, tempLife;

        public override bool AlwaysActive => true;

        public void MakeTemporary(Vector2 at, float dmgBudget, float lifeTime = 25f)
        {
            temp = true;
            pos = at;
            budget = dmgBudget;
            tempLife = lifeTime;
        }

        // temporary pets spend their damage budget
        protected void Spent(float d)
        {
            if (!temp) return;
            budget -= d;
        }

        protected virtual void OnVanish() => owner.TempPetDied();
        protected bool homingBack;
        protected virtual bool CanGhost => true;

        protected Pet(Player p, string kind)
        {
            owner = p;
            this.kind = kind;
            pos = p.pos + new Vector2(-p.facing * 0.8f, 0.6f);
            frames = Art.PetFrames(kind);
            MakeGO("Pet_" + kind, frames[0], 19);
            G.fx.Burst(pos, 12, new Color(1f, 1f, 1f), 3f, 0.4f, 0f);
            t = Random.value * 6f;
        }

        protected float Dmg(float b) => owner.Dmg(b) * owner.s.petMul;

        // the closest foe within r (optionally only ones it can see)
        protected Enemy Nearest(Vector2 from, float r, bool sight)
        {
            Enemy best = null;
            float bd = r * r;
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en) || en.Intangible || en.team == Team.Player) continue;
                float d2 = (en.pos - from).sqrMagnitude;
                if (d2 >= bd) continue;
                if (sight && !Wd.LineClear(from, en.pos)) continue;
                bd = d2; best = en;
            }
            return best;
        }

        protected bool Bite(Enemy en, float dmg, Vector2 knock)
        {
            if (!en.TakeShot(dmg, knock)) return false;
            Spent(dmg);
            Pets.Status(en, dmg);
            G.fx.Blood(en.pos, knock.normalized, en.BloodColor, 4);
            G.sfx.Play("hit", 0.35f, 1.4f, 0.1f);
            return true;
        }

        public override void Tick(float dt)
        {
            if (owner != G.player || owner.dead) { Kill(); return; }
            t += dt; cd -= dt;
            if (temp)
            {
                tempLife -= dt;
                if (tempLife <= 0f || budget <= 0f)
                {
                    G.fx.Burst(pos, 10, new Color(0.8f, 0.9f, 1f, 0.8f), 3f, 0.4f, -2f);
                    OnVanish();
                    Kill();
                    return;
                }
            }
            // left behind or stuck: it turns into a little ghost and flies straight back to you through anything
            float far = (owner.pos - pos).magnitude;
            if (!homingBack && far > 9f && CanGhost) homingBack = true;
            if (homingBack)
            {
                pos = Vector2.MoveTowards(pos, owner.pos + Vector2.up * 0.6f, (10f + far * 1.5f) * dt);
                vel = Vector2.zero;
                if ((owner.pos - pos).magnitude < 1.2f) { homingBack = false; G.fx.Burst(pos, 8, new Color(0.85f, 0.9f, 1f, 0.8f), 2f, 0.3f, 0f); }
            }
            else Behave(dt);
            if (dead) return;
            if (Mathf.Abs(vel.x) > 0.3f) facing = vel.x > 0f ? 1 : -1;
            sr.sprite = frames[((int)(t * 8f)) % frames.Length];
            sr.flipX = facing < 0;
            sr.enabled = owner.mode != Player.Mode.Dead;
            if (temp) sr.color = new Color(0.8f, 0.92f, 1f, tempLife < 1.5f && ((int)(t * 12f)) % 2 == 0 ? 0.25f : 0.65f);   // a ghostly spirit
            else sr.color = homingBack ? new Color(0.8f, 0.9f, 1f, 0.45f) : Color.white;
            go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y), 0f);
        }

        protected abstract void Behave(float dt);

        // fly towards a point with a little lag, like a spring
        protected void Follow(Vector2 goal, float stiff, float dt)
        {
            Vector2 want = (goal - pos) * stiff;
            vel = Vector2.MoveTowards(vel, Vector2.ClampMagnitude(want, 14f), 40f * dt);
            Vector2 next = pos + vel * dt;
            if (Wd.Get(Mathf.FloorToInt(next.x), Mathf.FloorToInt(next.y)) == Block.Bedrock) vel *= -0.5f;   // not even pets pass the void
            else pos = next;
            if ((owner.pos - pos).sqrMagnitude > 18f * 18f) pos = owner.pos;   // left far behind: catch up
        }

        public override void AddLight(Lighting l) => l.Add(pos, 1.3f, 0.4f);
    }

    // BABY BAT: flutters around your head and dives at foes, then comes back.
    public class PetBat : Pet
    {
        Enemy prey;
        float diveT;
        public PetBat(Player p) : base(p, "bat") { }

        protected override void Behave(float dt)
        {
            if (prey != null)
            {
                diveT -= dt;
                if (prey.dead || diveT <= 0f) { prey = null; cd = 0.6f; return; }
                Vector2 d = prey.pos - pos;
                vel = Vector2.MoveTowards(vel, d.normalized * 15f, 90f * dt);
                pos += vel * dt;
                if (U.Overlap(pos, new Vector2(0.25f, 0.25f), prey.pos, prey.half))
                {
                    Bite(prey, Dmg(1.6f), d.normalized * 4f);
                    prey = null;
                    cd = 1.1f;
                    vel = -vel * 0.5f;
                }
                return;
            }
            Vector2 home = owner.pos + new Vector2(Mathf.Cos(t * 2.3f) * 1.1f, 1.1f + Mathf.Sin(t * 4.6f) * 0.3f);
            Follow(home, 5f, dt);
            if (cd <= 0f)
            {
                var e = Nearest(owner.pos, 6f, true);
                if (e != null) { prey = e; diveT = 0.7f; G.sfx.Play("bat", 0.25f, 1.6f); }
            }
        }
    }

    // WILL-O'-WISP: hovers at your shoulder and shoots little bolts at foes it can see.
    public class PetWisp : Pet
    {
        public PetWisp(Player p) : base(p, "wisp") { }

        protected override void Behave(float dt)
        {
            Vector2 home = owner.pos + new Vector2(-owner.facing * 0.75f, 0.95f + Mathf.Sin(t * 3f) * 0.12f);
            Follow(home, 7f, dt);
            if (Random.value < 0.3f) G.fx.Burst(pos, 1, new Color(0.55f, 0.9f, 1f, 0.8f), 0.6f, 0.4f, 2f, 1f, false);
            if (cd > 0f) return;
            var e = Nearest(pos, 8.5f, true);
            if (e == null) return;
            cd = 0.75f;
            Vector2 d = (e.pos - pos).normalized;
            facing = d.x > 0f ? 1 : -1;
            G.Spawn(Bullet.Pet(pos, d * 22f, Dmg(0.9f), owner.ShotTint(new Color(0.55f, 0.9f, 1f))));
            Spent(Dmg(0.9f));
            G.sfx.Play("zap", 0.2f, 1.7f, 0.1f);
        }

        public override void AddLight(Lighting l) => l.Add(pos, 2.2f, 0.7f);
    }

    // PET BEETLE: trots after you on the ground, hops up ledges and rams foes it meets.
    public class PetBeetle : Pet
    {
        static readonly Vector2 H = new Vector2(0.3f, 0.22f);
        bool grounded;
        float stuckT, chargeT;
        public PetBeetle(Player p) : base(p, "beetle") { }

        protected override void Behave(float dt)
        {
            var prey = cd <= 0f ? Nearest(pos, 4.5f, true) : null;
            float goalX = prey != null && Mathf.Abs(prey.pos.y - pos.y) < 1.5f ? prey.pos.x : owner.pos.x - owner.facing * 1.2f;
            float dx = goalX - pos.x;
            bool charging = prey != null && Mathf.Abs(dx) < 4.5f;
            float sp = charging ? 9f : Mathf.Abs(dx) > 3f ? 7.5f : Mathf.Abs(dx) > 0.4f ? 4f : 0f;
            vel.x = U.Approach(vel.x, Mathf.Sign(dx) * sp, 40f * dt);
            vel.y = Mathf.Max(vel.y - 40f * dt, -16f);
            Vector2 p = pos;
            bool blocked = Wd.MoveX(ref p, H, vel.x * dt, out _);
            if (Wd.MoveY(ref p, H, vel.y * dt, out _)) { if (vel.y < 0f) grounded = true; vel.y = 0f; } else grounded = false;
            pos = p;
            // hop up steps, or after you when you climb
            if (grounded && (blocked || owner.pos.y - pos.y > 1.5f && Mathf.Abs(dx) < 2.5f)) vel.y = 11f;
            stuckT = Mathf.Abs(dx) > 1.5f && Mathf.Abs(vel.x) < 0.5f ? stuckT + dt : 0f;
            // stuck: it turns into a ghost and flies back to you (being left far behind does the same, see Pet.Tick)
            if (stuckT > 1.5f) { homingBack = true; stuckT = 0f; vel = Vector2.zero; }
            if (charging)
            {
                chargeT += dt;
                if (U.Overlap(pos, H + new Vector2(0.1f, 0.05f), prey.pos, prey.half))
                {
                    Bite(prey, Dmg(2.2f), new Vector2(Mathf.Sign(dx) * 7f, 3f));
                    cd = 1f; vel.x = -Mathf.Sign(dx) * 4f; vel.y = 5f; chargeT = 0f;
                }
                else if (chargeT > 1.5f) { cd = 0.8f; chargeT = 0f; }
            }
            else chargeT = 0f;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (!dead) go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y), 0f);
        }
    }

    // SPIRIT ORB: circles you close, hurts every foe it touches and swallows enemy shots.
    public class PetOrb : Pet
    {
        readonly Dictionary<Enemy, float> touched = new Dictionary<Enemy, float>();
        public PetOrb(Player p) : base(p, "orb") { }

        protected override void Behave(float dt)
        {
            float a = t * 3.2f;
            pos = owner.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.35f;
            vel = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
            foreach (var e in G.ents)
            {
                if (e.dead) continue;
                if (e is Bullet b && b.team != Team.Player && (b.pos - pos).sqrMagnitude < 0.45f * 0.45f)
                {
                    b.Kill();
                    G.fx.Burst(pos, 6, new Color(0.8f, 0.95f, 1f), 3f, 0.2f, 0f);
                    G.sfx.Play("reflect", 0.25f, 1.5f, 0.1f);
                }
                else if (e is Enemy en && !en.Intangible && U.Overlap(pos, new Vector2(0.28f, 0.28f), en.pos, en.half))
                {
                    if (touched.TryGetValue(en, out var last) && t - last < 0.4f) continue;
                    touched[en] = t;
                    Bite(en, Dmg(1.1f), (en.pos - owner.pos).normalized * 3f);
                }
            }
        }

        public override void AddLight(Lighting l) => l.Add(pos, 2f, 0.6f);
    }

    // SPORE BUDDY: plods along your trail like a duckling and puffs poison at foes around it.
    public class PetSpore : Pet
    {
        readonly Queue<Vector2> trail = new Queue<Vector2>();
        public PetSpore(Player p) : base(p, "spore") { }

        protected override void Behave(float dt)
        {
            trail.Enqueue(owner.pos + new Vector2(0f, -0.1f));
            while (trail.Count > 22) trail.Dequeue();   // about a third of a second behind you
            Vector2 goal = trail.Peek();
            pos = Vector2.Lerp(pos, goal, Mathf.Min(1f, 10f * dt));
            vel = (goal - pos) * 10f;
            if (cd > 0f) return;
            if (Nearest(pos, 2.4f, false) == null) return;
            cd = 1.5f;
            foreach (var e in G.ents)
                if (!e.dead && e is Enemy en && !en.Intangible && (en.pos - pos).sqrMagnitude < 2.4f * 2.4f)
                {
                    en.Poison(3f, Dmg(0.7f));
                    Pets.Status(en, Dmg(0.7f));
                    Spent(Dmg(0.7f) * 3f);
                }
            G.fx.Burst(pos, 22, new Color(0.55f, 1f, 0.35f, 0.8f), 4f, 0.7f, -1f, 1.4f, false);
            G.fx.Ring(pos, 2.4f, new Color(0.55f, 1f, 0.35f));
            G.sfx.Play("splat", 0.3f, 1.3f);
        }
    }

    // FROST SPRITE: drifts at your other shoulder and freezes (and nips) every foe that comes close, then needs a rest.
    public class PetFrost : Pet
    {
        public PetFrost(Player p) : base(p, "frost") { }

        protected override void Behave(float dt)
        {
            Vector2 home = owner.pos + new Vector2(owner.facing * 0.7f, 1f + Mathf.Sin(t * 2.6f) * 0.15f);
            Follow(home, 6f, dt);
            if (Random.value < 0.25f) G.fx.Burst(pos, 1, new Color(0.85f, 0.97f, 1f, 0.8f), 0.5f, 0.6f, 3f, 1f, false);
            if (cd > 0f || Nearest(pos, 2.4f, false) == null) return;
            cd = 4f;
            foreach (var e in G.ents)
                if (!e.dead && e is Enemy en && !en.Intangible && en.team != Team.Player && (en.pos - pos).sqrMagnitude < 2.4f * 2.4f)
                {
                    en.Freeze(1.5f);
                    Bite(en, Dmg(1.2f), (en.pos - pos).normalized * 2f);
                }
            G.fx.Ring(pos, 2.4f, new Color(0.7f, 0.95f, 1f));
            G.fx.Burst(pos, 24, new Color(0.85f, 0.97f, 1f), 5f, 0.6f, 5f);
            G.sfx.Play("freeze", 0.5f, 1.3f);
        }

        public override void AddLight(Lighting l) => l.Add(pos, 1.8f, 0.5f);
    }

    // SAWBLADE PUP: hold fire and it winds up beside you; let go and it flies at the cursor, bouncing off walls and
    // slicing through foes like a disc, then comes home. It never cuts the rock.
    public class PetDisc : Pet
    {
        float charge, heldT, flyT;
        bool flying, returning;
        int bouncesLeft;
        readonly HashSet<Enemy> cut = new HashSet<Enemy>();
        public PetDisc(Player p) : base(p, "disc") { }
        protected override bool CanGhost => !flying;

        protected override void Behave(float dt)
        {
            bool held = GameInput.Held(Btn.Attack) && owner.mode == Player.Mode.Normal;
            if (flying)
            {
                flyT -= dt;
                if (!returning && flyT <= 0f) returning = true;
                if (returning)
                {
                    Vector2 to = owner.pos - pos;
                    if (to.magnitude < 0.6f) { flying = false; returning = false; return; }
                    vel = Vector2.MoveTowards(vel, to.normalized * 18f, 80f * dt);
                    pos += vel * dt;   // homeward it phases through rock
                }
                else
                {
                    Vector2 next = pos + vel * dt;
                    int nx = Mathf.FloorToInt(next.x), ny = Mathf.FloorToInt(next.y);
                    if (Wd.Solid(nx, ny))
                    {
                        // bounce like a disc: flip whichever axis ran into the rock
                        int px = Mathf.FloorToInt(pos.x), py = Mathf.FloorToInt(pos.y);
                        bool bx = px != nx && Wd.Solid(nx, py), by = py != ny && Wd.Solid(px, ny);
                        if (bx) vel.x = -vel.x;
                        if (by) vel.y = -vel.y;
                        if (!bx && !by) vel = -vel;
                        G.sfx.Play("ricochet", 0.25f, 1.4f);
                        if (--bouncesLeft < 0) returning = true;
                    }
                    else pos = next;
                }
                foreach (var e in G.ents)
                    if (!e.dead && e is Enemy en && !en.Intangible && en.team != Team.Player && !cut.Contains(en) && U.Overlap(pos, new Vector2(0.3f, 0.3f), en.pos, en.half))
                    {
                        cut.Add(en);
                        Bite(en, Dmg(1.2f + 2.8f * charge), vel.normalized * 4f);
                    }
                facing = vel.x >= 0f ? 1 : -1;
                return;
            }
            heldT = held ? heldT + dt : 0f;
            if (held && heldT > 0.15f)
            {
                // winding up at your side
                charge = Mathf.Min(1f, charge + dt / 1.2f);
                Follow(owner.pos + new Vector2(owner.facing * 0.6f, 0.2f), 9f, dt);
                if (Random.value < charge) G.fx.Burst(pos, 1, new Color(1f, 0.9f, 0.6f), 2f, 0.15f, 0f);
                if (charge >= 1f && Random.value < 0.1f) G.sfx.Play("blip", 0.1f, 2f, 0f);
                return;
            }
            if (!held && charge >= 0.2f)
            {
                flying = true; returning = false;
                flyT = 0.7f + charge * 0.8f;
                bouncesLeft = 3 + Mathf.RoundToInt(charge * 3f);
                Vector2 dir = (G.aim - pos).sqrMagnitude > 0.01f ? (G.aim - pos).normalized : Vector2.right;
                vel = dir * (12f + 14f * charge);
                cut.Clear();
                G.sfx.Play("disc", 0.5f, 1.2f);
                charge = 0f;
                return;
            }
            charge = 0f;
            Follow(owner.pos + new Vector2(-owner.facing * 0.7f, 0.15f + Mathf.Sin(t * 3f) * 0.1f), 6f, dt);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (!dead) go.transform.rotation = Quaternion.Euler(0f, 0f, t * (flying ? -1400f : -200f - charge * 1200f));
        }
    }

    // MAGPIE: snaps up the gold and gems lying around (before you can), and once it has hoarded 200 gold worth,
    // it drops 4 treasures of the kind found at this depth.
    public class PetMagpie : Pet
    {
        Coin prey;
        int stash;
        const int Goal = 200;
        public PetMagpie(Player p) : base(p, "magpie") { }

        protected override void Behave(float dt)
        {
            if (prey != null && (prey.dead || (prey.pos - owner.pos).sqrMagnitude > 12f * 12f)) prey = null;
            if (prey == null)
            {
                float bd = 7f * 7f;
                foreach (var e in G.ents)
                    if (!e.dead && e is Coin c && (c.pos - pos).sqrMagnitude < bd) { bd = (c.pos - pos).sqrMagnitude; prey = c; }
            }
            if (prey != null)
            {
                prey.noMag = 0.3f;   // hands off, it's the magpie's
                Vector2 d = prey.pos - pos;
                vel = Vector2.MoveTowards(vel, d.normalized * 11f, 60f * dt);
                pos += vel * dt;
                if (d.magnitude < 0.35f)
                {
                    stash += prey.value;
                    prey.Kill();
                    prey = null;
                    G.sfx.Play("coin", 0.25f, 1.6f);
                    if (stash >= Goal) PayOut();
                }
            }
            else Follow(owner.pos + new Vector2(-owner.facing * 0.9f, 1.3f + Mathf.Sin(t * 2f) * 0.2f), 5f, dt);
            if (stash > 0) G.hud.Label(pos + Vector2.up * 0.6f, stash + "/" + Goal, new Color(1f, 0.85f, 0.35f));
        }

        void PayOut()
        {
            stash -= Goal;
            var rng = new System.Random(Random.Range(0, int.MaxValue));
            for (int i = 0; i < 4; i++)
            {
                var d = DB.RollChest(owner.Depth, owner, rng);
                if (d != null) G.Spawn(new LootPickup(pos + new Vector2((i - 1.5f) * 0.4f, 0.2f), d, 0.6f));
            }
            G.hud.Banner("MAGPIE'S HOARD", "4 TREASURES", new Color(1f, 0.85f, 0.35f));
            G.sfx.Play("chest", 0.8f, 1.3f);
            G.fx.Burst(pos, 30, new Color(1f, 0.85f, 0.35f), 7f, 0.6f, 5f);
        }
    }

    // FLY SWARM: a little fly that hunts down foes (through rock) and nips them, gone after a few seconds.
    public class PetFly : Pet
    {
        public PetFly(Player p) : base(p, "fly") { }

        protected override void OnVanish() => owner.FlyDied();

        protected override void Behave(float dt)
        {
            var e = Nearest(pos, 12f, false);
            Vector2 goal = e != null ? e.pos : owner.pos + new Vector2(Mathf.Cos(t * 5f), 1f + Mathf.Sin(t * 7f)) * 0.8f;
            vel = Vector2.MoveTowards(vel, (goal - pos).normalized * 10f + Random.insideUnitCircle * 4f, 60f * dt);
            pos += vel * dt;
            if (e != null && cd <= 0f && U.Overlap(pos, new Vector2(0.15f, 0.15f), e.pos, e.half))
            {
                Bite(e, Dmg(0.5f), vel.normalized);
                cd = 0.35f;
            }
        }

        public override void AddLight(Lighting l) { }
    }
}
