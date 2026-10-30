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
        static float helpT;
        protected PetWalker(Player p, string kind) : base(p, kind) { }

        // walk towards goalX at speed sp (hopping up steps); returns true if blocked by a wall
        protected override bool FreeAt(Vector2 p) => !Wd.BoxSolid(p, H);
        protected override Vector2 SafeSpot() => owner.pos - new Vector2(0f, Player.Half.y - H.y - 0.01f);

        // stuck inside a block (dug in, built over, dropped in by the ghost): pop out to the nearest free spot, or back to you
        void Unstick()
        {
            if (FreeAt(pos)) return;
            for (int ring = 1; ring <= 6; ring++)
                for (int k = 0; k < 8; k++)
                {
                    Vector2 o = U.Rot(Vector2.up, k * 45f) * ring * 0.2f;
                    if (FreeAt(pos + o)) { pos += o; vel = Vector2.zero; return; }
                }
            pos = SafeSpot();
            vel = Vector2.zero;
        }

        public override void Tick(float dt)
        {
            if (!homingBack && !dead) Unstick();
            base.Tick(dt);
        }

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
            if (stuckT > 1.5f)
            {
                // stuck, and you're a way off: GOOD DOG cries out for you before it comes looking
                if (this is PetDog && (owner.pos - pos).magnitude > 5f && helpT <= Time.time) { helpT = Time.time + 8f; G.sfx.PlayAt(pos, "help", 0.6f, 1.2f, 0.06f, 22f); }
                homingBack = true; stuckT = 0f; vel = Vector2.zero;
            }
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

    // ACE FOX (ultra rare): a tournament fox. He never stands still: he wavedashes left and right, left and right,
    // frantically, working his way after you (or a foe), and now and then SHINES for no reason at all. Up against a foe
    // he shines it over and over (each extra shine a little less likely, eight at most). A shine hurts what it touches
    // and sends every enemy shot near him straight back. His blaster floats along behind him, a little above, swaying,
    // and pops shots at foes he can see.
    public class PetFox : PetWalker
    {
        enum St { Pause, Dash, Shine, FoxCharge, FoxFly }
        St st = St.Pause;
        Enemy prey;
        float stT, afterT, dashDir, shootT, gunCd, gunKick, animT;
        int shines;
        float fireCd = 2f;
        Vector2 flyDir;
        readonly System.Collections.Generic.HashSet<Enemy> burnt = new System.Collections.Generic.HashSet<Enemy>();
        bool chain;   // shining at a foe (a multishine), not just for fun
        public PetFox(Player p) : base(p, "fox")
        {
            gunGo = new GameObject("FoxGun");
            gunSr = Art.AddSR(gunGo, null, 20);
            gunPos = pos + Vector2.up;
        }

        static readonly Color ShineC = new Color(0.55f, 0.88f, 1f), DashC = new Color(1f, 0.8f, 0.4f);
        readonly GameObject gunGo;
        readonly SpriteRenderer gunSr;
        Vector2 gunPos;
        Vector2 aim = Vector2.right;

        static Sprite[] F(string k) => SpriteSheet.Frames(k);

        protected override void Behave(float dt)
        {
            stT -= dt; afterT -= dt; gunCd -= dt; shootT -= dt; animT += dt; fireCd -= dt;
            if (prey != null && (prey.dead || (prey.pos - pos).sqrMagnitude > 12f * 12f)) prey = null;
            if (prey == null && cd <= 0f) { prey = Nearest(pos, 7f, true); cd = 0.3f; }

            switch (st)
            {
                case St.Pause:
                    Fall(dt, 0f);
                    if (stT <= 0f) NextMove();
                    break;
                case St.Dash:
                    {
                        // a wavedash: an air-dash slammed into the floor, sliding fast and low, with dust and afterimages
                        vel.x = dashDir * 14f;
                        bool blocked = Fall(dt, vel.x);
                        if (blocked && grounded) vel.y = 10f;   // a wall: hop up it
                        Trail(DashC);
                        if (Random.value < 0.6f) G.fx.Burst(pos + new Vector2(-dashDir * 0.3f, -0.2f), 1, new Color(0.9f, 0.85f, 0.75f), 2f, 0.25f, 6f);
                        if (prey != null && Mathf.Abs(prey.pos.x - pos.x) < 0.9f && Mathf.Abs(prey.pos.y - pos.y) < 1.2f) { StartShine(true); break; }
                        if (stT <= 0f) { st = St.Pause; stT = Random.Range(0.03f, 0.09f); vel.x = 0f; }
                        break;
                    }
                case St.FoxCharge:
                    {
                        // FIRE FOX: he hangs in the air, wreathed in flame, winding up...
                        vel = Vector2.zero;
                        if (prey != null) flyDir = (prey.pos - pos).normalized;
                        facing = flyDir.x >= 0f ? 1 : -1;
                        for (int k = 0; k < 2; k++)
                            G.fx.Burst(pos + Random.insideUnitCircle * 0.5f, 1, new Color(1f, Random.Range(0.45f, 0.85f), 0.15f), 2.5f, 0.35f, -5f, 1.2f, false);
                        if (stT <= 0f)
                        {
                            // ...and launches himself, nose first, like a meteor
                            st = St.FoxFly; stT = 0.38f; burnt.Clear();
                            G.sfx.Play("flame_start", 0.7f, 1.1f, 0.05f);
                            G.sfx.Play("boom", 0.35f, 1.6f, 0.05f);
                            G.cam.Shake(0.12f);
                            G.fx.Burst(pos, 14, new Color(1f, 0.85f, 0.4f), 7f, 0.35f, 0f);
                        }
                        break;
                    }
                case St.FoxFly:
                    {
                        Vector2 p = pos;
                        bool hx = Wd.MoveX(ref p, H, flyDir.x * 24f * dt, out _);
                        bool hy = Wd.MoveY(ref p, H, flyDir.y * 24f * dt, out _);
                        pos = p;
                        // a blazing trail
                        for (int k = 0; k < 3; k++)
                            G.fx.Burst(pos - flyDir * Random.Range(0f, 0.6f) + Random.insideUnitCircle * 0.25f, 1, new Color(1f, Random.Range(0.35f, 0.9f), 0.1f), 3f, 0.45f, -2f, 1.4f, false);
                        if (Random.value < 0.5f) G.fx.Burst(pos - flyDir * 0.8f, 1, new Color(1f, 1f, 0.8f), 1.5f, 0.25f, 0f, 1f, false);
                        Trail(new Color(1f, 0.55f, 0.15f));
                        // burning everything it ploughs through
                        foreach (var e in G.ents)
                            if (!e.dead && e is Enemy en && !en.Intangible && en.team != Team.Player && !burnt.Contains(en) && U.Overlap(pos, H + new Vector2(0.3f, 0.3f), en.pos, en.half))
                            {
                                burnt.Add(en);
                                Bite(en, Dmg(3f), flyDir * 8f + Vector2.up * 4f);
                                en.Ignite(3f);
                                G.fx.Burst(en.pos, 10, new Color(1f, 0.6f, 0.2f), 5f, 0.3f, 0f);
                                G.sfx.Play("ignite", 0.5f, 1.2f, 0.05f);
                            }
                        Fire.IgniteBox(pos, H + new Vector2(0.1f, 0.1f));
                        if (hx || hy || stT <= 0f)
                        {
                            if (hx || hy) { G.fx.Burst(pos + flyDir * 0.3f, 10, new Color(1f, 0.7f, 0.3f), 5f, 0.3f, 10f); G.cam.Shake(0.08f); }
                            st = St.Pause; stT = 0.15f; vel = flyDir * 4f; fireCd = Random.Range(3.5f, 6f);
                        }
                        break;
                    }
                case St.Shine:
                    vel.x = 0f;
                    Fall(dt, 0f);
                    if (stT <= 0f)
                    {
                        // keep shining? less likely with every shine, eight at most
                        float keep = chain && prey != null ? 0.85f - shines * 0.1f : 0.3f - shines * 0.1f;
                        if (shines < 8 && Random.value < keep) { if (grounded && Random.value < 0.5f) vel.y = 5f; Shine(); stT = 0.13f; }
                        else { st = St.Pause; stT = 0.05f; }
                    }
                    break;
            }
            TickGun(dt);
        }

        // the next wavedash: towards where he wants to be when it's far, otherwise just back and forth on the spot
        void NextMove()
        {
            if (Random.value < 0.12f) { StartShine(false); return; }   // a shine, just because
            // FIRE FOX: a foe a little way off: wind up and rocket into it
            if (prey != null && fireCd <= 0f && Random.value < 0.3f && (prey.pos - pos).magnitude > 2.5f && (prey.pos - pos).magnitude < 8f && Wd.LineClear(pos, prey.pos))
            {
                st = St.FoxCharge; stT = 0.4f; flyDir = (prey.pos - pos).normalized;
                G.sfx.Play("charge", 0.45f, 1.5f, 0.05f);
                G.sfx.Play("ignite", 0.4f, 0.9f);
                return;
            }
            float goal = prey != null ? prey.pos.x : Home;
            float dx = goal - pos.x;
            if (Mathf.Abs(dx) > 1.2f) dashDir = Random.value < 0.8f ? Mathf.Sign(dx) : -Mathf.Sign(dx);
            else dashDir = Random.value < 0.5f ? 1f : -1f;
            facing = dashDir > 0f ? 1 : -1;
            st = St.Dash;
            stT = Random.Range(0.1f, 0.2f);
            if (grounded && owner.pos.y - pos.y > 1.5f && Mathf.Abs(owner.pos.x - pos.x) < 3f) vel.y = 11f;   // you went up: so does he
            if (Random.value < 0.5f) G.sfx.Play("dashq", 0.2f, Random.Range(1.6f, 2f), 0.05f);
        }

        void StartShine(bool atFoe)
        {
            st = St.Shine; chain = atFoe; shines = 0; stT = 0.13f;
            if (prey != null) facing = prey.pos.x > pos.x ? 1 : -1;
            Shine();
        }

        // gravity and moving along: returns true if a wall stopped him
        bool Fall(float dt, float vx)
        {
            vel.y = Mathf.Max(vel.y - 40f * dt, -16f);
            Vector2 p = pos;
            bool blocked = Wd.MoveX(ref p, H, vx * dt, out _);
            if (Wd.MoveY(ref p, H, vel.y * dt, out _)) { if (vel.y < 0f) grounded = true; vel.y = 0f; } else grounded = false;
            pos = p;
            return blocked;
        }

        void Trail(Color c)
        {
            if (afterT > 0f || sr == null) return;
            afterT = 0.035f;
            G.fx.Ghost(sr.sprite, go.transform.position, sr.flipX, new Color(c.r, c.g, c.b, 0.55f));
        }

        // SHINE: a burst of hexagonal light. Hurts what it touches, pops it up, and sends enemy shots back where they came from.
        void Shine()
        {
            shines++;
            G.sfx.Play("shine", 0.55f, 1f, 0.04f);
            G.fx.Ring(pos, 1.1f, ShineC);
            for (int k = 0; k < 6; k++)
            {
                float a = k * 60f * Mathf.Deg2Rad;
                G.fx.Burst(pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.6f, 1, ShineC, 0.5f, 0.15f, 0f, 1.2f, false);
            }
            bool reflected = false;
            foreach (var e in G.ents)
            {
                if (e.dead) continue;
                if (e is Enemy en && !en.Intangible && en.team != Team.Player && (en.pos - pos).sqrMagnitude < 1.4f * 1.4f)
                    Bite(en, Dmg(1.6f), new Vector2(Mathf.Sign(en.pos.x - pos.x) * 4f, 9f));
                else if (e is Bullet b && b.team != Team.Player && (b.pos - pos).sqrMagnitude < 1.7f * 1.7f)
                {
                    var target = Nearest(pos, 10f, false);
                    b.Deflect(Team.Player, target != null ? target.pos - b.pos : b.pos - pos);
                    reflected = true;
                }
            }
            if (reflected)
            {
                // the reflector's own sound: a sharp glassy ping
                G.sfx.Play("reflect", 0.7f, 2.3f, 0.03f);
                G.sfx.Play("zap", 0.35f, 2f, 0.05f);
                G.fx.Ring(pos, 1.6f, Color.white);
            }
        }

        // the blaster floats after him, up and behind, swaying; it pops shots at foes he can see
        void TickGun(float dt)
        {
            float sway = Mathf.Sin(t * 3.1f) * 0.12f;
            Vector2 want = pos + new Vector2(-facing * 0.55f, 0.95f + Mathf.Sin(t * 2.3f) * 0.1f);
            gunPos = Vector2.Lerp(gunPos, want, 1f - Mathf.Exp(-7f * dt));
            var foe = prey != null && Wd.LineClear(gunPos, prey.pos) ? prey : Nearest(gunPos, 8f, true);
            Vector2 wantAim = foe != null ? (foe.pos - gunPos).normalized : new Vector2(facing, 0f);
            aim = Vector2.Lerp(aim, wantAim, 1f - Mathf.Exp(-12f * dt)).normalized;
            gunKick = Mathf.MoveTowards(gunKick, 0f, dt * 2.5f);
            if (foe != null && gunCd <= 0f)
            {
                gunCd = Random.Range(0.7f, 1.1f);
                gunKick = 0.18f;
                shootT = 0.2f;
                G.Spawn(Bullet.Pet(gunPos + aim * 0.5f, aim * 20f, Dmg(0.9f), new Color(0.45f, 0.85f, 1f)));
                G.sfx.Play("laser", 0.35f, 1.4f, 0.08f);
                G.fx.Muzzle(gunPos + aim * 0.55f, 0.45f, new Color(0.5f, 0.9f, 1f));
            }
            var gf = F("foxgun");
            if (gunSr != null)
            {
                gunSr.sprite = gf != null && gf.Length > 0 ? gf[gunKick > 0.12f ? Mathf.Min(2, gf.Length - 1) : gunKick > 0.02f ? Mathf.Min(1, gf.Length - 1) : 0] : Art.Bullets["b_laser"];
                // it faces whichever side it's shooting at (flipped, never upside down), tilted up or down a little
                bool left = aim.x < 0f;
                gunSr.flipX = left;
                gunSr.flipY = false;
                float tilt = Mathf.Clamp(Mathf.Atan2(aim.y, Mathf.Abs(aim.x)) * Mathf.Rad2Deg, -60f, 60f) + sway * 25f;
                float ang = left ? -tilt : tilt;
                Vector2 at = gunPos - aim * gunKick;
                gunGo.transform.position = new Vector3(U.Snap(at.x), U.Snap(at.y), 0f);
                gunGo.transform.rotation = Quaternion.Euler(0f, 0f, ang);
                gunSr.enabled = sr != null && sr.enabled && !homingBack;
            }
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (dead || sr == null) return;
            // his own drawn frames
            Sprite[] f = null;
            bool facesLeft = false;
            go.transform.rotation = Quaternion.identity;
            if (homingBack)
            {
                // flying back to you as a ghost: his first idle frame, see-through
                var gi = F("foxace_idle");
                if (gi != null && gi.Length > 0) { sr.sprite = gi[0]; sr.flipX = facing < 0; go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y + 0.47f), 0f); }
                return;
            }
            if (st == St.FoxCharge || st == St.FoxFly)
            {
                // FIRE FOX: the leaping frame, turned so his nose leads
                var sf = F("foxace_shoot");
                if (sf != null && sf.Length > 0)
                {
                    sr.sprite = sf[0];
                    bool left = flyDir.x < 0f;
                    sr.flipX = left;
                    float a = Mathf.Atan2(flyDir.y, Mathf.Abs(flyDir.x)) * Mathf.Rad2Deg;
                    go.transform.rotation = Quaternion.Euler(0f, 0f, left ? -a : a);
                    go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y + 0.2f), 0f);
                    if (st == St.FoxCharge) sr.color = ((int)(animT * 20f)) % 2 == 0 ? new Color(1f, 0.75f, 0.4f) : Color.white;   // flickering with flame
                }
                return;
            }
            if (st == St.Shine) f = F("foxace_shine");
            else if (st == St.Dash) { f = F("foxace_dash"); facesLeft = true; }
            else if (shootT > 0f) f = F("foxace_shoot");
            if (f == null || f.Length == 0) f = F("foxace_idle");
            if (f != null && f.Length > 0 && !homingBack)
            {
                float fps = st == St.Shine ? 18f : st == St.Dash ? 14f : 7f;
                sr.sprite = f[((int)(animT * fps)) % f.Length];
                sr.flipX = facesLeft ? facing > 0 : facing < 0;
                // (the frames stand on their feet: line them up with his)
                go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y + 0.47f), 0f);
            }
        }

        public override void Kill()
        {
            base.Kill();
            if (gunGo != null) Object.Destroy(gunGo);
        }
    }
}
