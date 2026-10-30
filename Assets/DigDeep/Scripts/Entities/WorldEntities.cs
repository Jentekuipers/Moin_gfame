using UnityEngine;

namespace DigDeep
{
    // Sand, spikes, chests, crates and TNT: wobble, then drop. (Boulders hand over to BoulderBody.) TNT that drops 3+ blocks goes off.
    public class Faller : Entity
    {
        readonly Block b;
        readonly int x;
        readonly int y;
        float wobble, vy;
        bool falling, wasWet;
        int startRow, hp;

        public override bool AlwaysActive => true;

        Rarity crate;

        public Faller(int x, int y, Block b)
        {
            this.x = x; this.y = y; this.b = b;
            if (b == Block.Crate) crate = Crates.RarityAt(x, y);
            pos = new Vector2(x + 0.5f, y + 0.5f);
            wobble = b == Block.Boulder ? 0.55f : 0.14f;
            MakeGO("Faller", Art.TileSprite(b, Biomes.At(x, y)), 7);
            sr.enabled = false;
            if (b == Block.Spikes) sr.flipY = true;   // falling spikes point down, at whatever they land on
            if (b == Block.Boulder) G.sfx.Play("rumble", 0.7f);
        }

        public override void Tick(float dt)
        {
            if (!falling)
            {
                wobble -= dt;
                if (Random.value < 0.35f) G.fx.Burst(new Vector2(pos.x + Random.Range(-0.4f, 0.4f), pos.y - 0.5f), 1, new Color(0.6f, 0.5f, 0.4f), 1f, 0.4f, 12f);
                if (wobble > 0f) return;
                if (Wd.Get(x, y) != b || Wd.Solid(x, y - 1) || Wd.Get(x, y - 1) == Block.Plate) { Wd.ClearPending(x, y); Kill(); return; }   // (an arena ledge holds it too)
                Wd.ClearPending(x, y);
                if (b == Block.Boulder)
                {
                    // boulders are physics bodies: they fall, roll and keep their cracks
                    Wd.PushBoulder(x, y, Vector2.zero);
                    Kill();
                    return;
                }
                hp = Wd.Hp(x, y);   // keeps its damage when it lands
                Wd.Set(x, y, Block.Air);
                falling = true;
                sr.enabled = true;
                startRow = y;
                Place();
                return;
            }
            vy = Mathf.Max(vy - 38f * dt, -15f);
            // into water: a splash, and TNT that drops into water only counts its fall from where it leaves it
            bool inWater = Wd.FluidAtPoint(pos) == Fluids.Water;
            if (inWater && !wasWet) Fluids.Impact(pos, 1.1f, new Vector2(0f, vy));
            if (inWater) startRow = Mathf.FloorToInt(pos.y - 0.5f);
            wasWet = inWater;
            if (b != Block.Boulder) vy = Fluids.Sink(pos, vy, dt);   // boulders plunge straight through
            pos.y += vy * dt;
            int row = Mathf.FloorToInt(pos.y - 0.5f);
            if (Wd.ImpactGlass(x, row, -vy, 5f)) { vy *= 0.6f; }   // falls through glass, smashing it
            else if (Wd.Solid(x, row)) { Land(row + 1); return; }

            var hb = new Vector2(0.45f, 0.45f);
            var pl = G.player;
            // falling spikes and sand hurt; chests and crates just land next to you
            // only the bottom of it hurts: standing on (or beside) a block as it drops is fine
            if (pl.mode != Player.Mode.Dead && (b == Block.Spikes || b == Block.Sand) && vy < -1f && pl.pos.y + Player.Half.y < pos.y + 0.1f
                && Mathf.Abs(pl.pos.x - pos.x) < 0.45f + Player.Half.x - 0.15f && U.Overlap(pos, hb, pl.pos, Player.Half))
            {
                Player.Cause = b == Block.Spikes ? "FALLING SPIKES" : "FALLING SAND";
                pl.Hurt(b == Block.Spikes ? 60 : 20, pos + Vector2.up);   // (sand stings; it doesn't crush)
                if (b == Block.Sand) pl.Extinguish();   // a load of sand smothers the flames
            }
            foreach (var e in G.ents)
                if (!e.dead && e is Enemy en && (b == Block.Spikes || b == Block.Sand) && U.Overlap(pos, hb, en.pos, en.half)) en.Hurt(8, Vector2.down * 2f);
            Place();
        }

        void Land(int land)
        {
            Kill();
            int fell = startRow - land;
            G.OnHeavyLanded(x, land);   // boulders, sand, chests... anything that falls presses a plate
            Wd.Impact(x, land - 1, -vy);   // something falling hard enough onto TNT sets it off
            Block at = Wd.Get(x, land);
            bool free = at == Block.Air || at == Block.Lava || at == Block.Spikes;
            if (b != Block.Boulder && b != Block.Sand) free = at == Block.Air;   // spikes, chests and crates need open air
            if (b == Block.Boulder) Fire.DouseNear(new Vector2(x + 0.5f, land + 0.5f), 1.0f);   // and smothers fire
            if (b == Block.Boulder && at == Block.Spikes) { Wd.Set(x, land, Block.Air); G.fx.Debris(new Vector2(x + 0.5f, land + 0.5f), new Color(0.65f, 0.65f, 0.7f), 8); }   // a boulder crushes spikes
            if (!free)
            {
                G.fx.Debris(new Vector2(x + 0.5f, land + 0.5f), TileDraw.DebrisColor(b, Biomes.At(x, land)), 16);
                G.sfx.Play("break", 0.8f);
                G.cam.Shake(0.22f);
            }
            else if (b == Block.TNT && fell >= 3)
            {
                Wd.Set(x, land, b);
                Wd.Break(x, land, false);   // the drop sets it off
            }
            else
            {
                if (b == Block.Crate) Wd.SetCrateRarity(x, land, crate);   // a crate keeps its colour when it falls
                Wd.Set(x, land, b);
                if (hp > 0 && b != Block.Sand) Wd.SetHp(x, land, hp);
                G.sfx.Play("land", 0.6f, b == Block.Sand ? 1.4f : 0.8f);
            }
        }
    }

    // A kicked crate of TNT: it flies nose first (tumbling a little), and goes off when it slams into something.
    // Dropped gently (or into water) it settles back down as a block.
    public class TntBody : Entity
    {
        Vector2 vel;
        float spin, age;
        static readonly Vector2 H = new Vector2(0.4f, 0.4f);
        public override bool AlwaysActive => true;

        public TntBody(int x, int y, Vector2 v)
        {
            pos = new Vector2(x + 0.5f, y + 0.5f);
            vel = v;
            MakeGO("TntBody", Art.TileSprite(Block.TNT, Biomes.At(x, y)), 7);
            spin = Random.Range(-1f, 1f) * 25f;
            Place();
        }

        void Boom() { Kill(); G.Explode(pos, 3.1f, 7, false, false); }

        public override void Tick(float dt)
        {
            age += dt;
            bool wet = Wd.FluidAtPoint(pos) == Fluids.Water;
            vel.y = Mathf.Max(vel.y - (wet ? 14f : 34f) * dt, wet ? -4f : -20f);
            if (wet) vel *= 1f - Mathf.Min(0.8f, 3f * dt);
            float sp = vel.magnitude;
            Vector2 p = pos;
            bool hitX = Wd.MoveX(ref p, H, vel.x * dt, out _);
            bool hitY = Wd.MoveY(ref p, H, vel.y * dt, out _);
            pos = p;
            // slamming into something hard sets it off
            if ((hitX || hitY) && sp > 9f && !wet) { Boom(); return; }
            foreach (var e in G.ents)
                if (!e.dead && e is Enemy en && !en.Intangible && en.team != Team.Player && age > 0.05f && U.Overlap(pos, H, en.pos, en.half)) { Boom(); return; }
            if (hitX) vel.x *= -0.3f;
            if (hitY) { vel.y = 0f; vel.x *= 0.6f; }
            // at rest: a block of TNT again
            bool ground = Wd.BoxSolid(pos + new Vector2(0f, -0.05f), H);
            if (ground && sp < 1.2f && age > 0.3f)
            {
                int cx = Mathf.FloorToInt(pos.x), cy = Mathf.FloorToInt(pos.y);
                Kill();
                if (Wd.Get(cx, cy) == Block.Air) { Wd.Set(cx, cy, Block.TNT); G.sfx.Play("land", 0.5f, 0.9f); }
                else G.Explode(pos, 3.1f, 7, false, false);
                return;
            }
            // nose first: it turns to face where it's flying, wobbling a bit
            float want = sp > 1f ? Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg - 90f + Mathf.Sin(age * 14f) * spin : 0f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.MoveTowardsAngle(go.transform.eulerAngles.z, want, 720f * dt));
            if (Random.value < 0.4f) G.fx.Burst(pos - vel.normalized * 0.4f, 1, new Color(1f, 0.7f, 0.3f), 1f, 0.3f, 0f);
            Place();
            go.transform.rotation = Quaternion.Euler(0f, 0f, go.transform.eulerAngles.z);
        }
    }

    // Lit TNT / volatile crystal: flashes, then explodes.
    public class Fuse : Entity
    {
        float t;
        readonly float r;
        readonly int dmg;

        public override bool AlwaysActive => true;

        public Fuse(int x, int y, Block b, float delay, float radius, int dmg)
        {
            pos = new Vector2(x + 0.5f, y + 0.5f);
            t = delay; r = radius; this.dmg = dmg;
            MakeGO("Fuse", Art.TileSprite(b, Biomes.At(x, y)), 6);
            G.sfx.Play("fuse", 0.6f);
        }

        public override void Tick(float dt)
        {
            t -= dt;
            sr.color = ((int)(t * 20f)) % 2 == 0 ? Color.white : new Color(1f, 0.35f, 0.35f);
            float s = 1f + Mathf.Max(0f, 0.25f - t) * 1.2f;
            go.transform.localScale = new Vector3(s, s, 1f);
            if (t <= 0f) { Kill(); G.Explode(pos, r, dmg, false, false); return; }
            Place();
        }

        public override void AddLight(Lighting l) => l.Add(pos, 2.2f, 0.8f);
    }

    // Delayed explosion, used by CHAIN REACTION so kills ripple outwards.
    public class Blast : Entity
    {
        float t;
        readonly float r;
        readonly int dmg;
        readonly bool safe;

        public override bool AlwaysActive => true;

        public Blast(Vector2 p, float delay, float radius, int dmg, bool safe)
        {
            pos = p; t = delay; r = radius; this.dmg = dmg; this.safe = safe;
        }

        public override void Tick(float dt)
        {
            t -= dt;
            if (t <= 0f) { Kill(); G.Explode(pos, r, dmg, safe, true, safe); }
        }
    }

    // ECHO CHAMBER: a second, ghostly attack a moment after the first.
    public class EchoStrike : Entity
    {
        float t = 0.1f;
        readonly Player p;

        public override bool AlwaysActive => true;

        public EchoStrike(Player p) { this.p = p; pos = p.pos; }

        public override void Tick(float dt)
        {
            t -= dt;
            if (t > 0f) return;
            Kill();
            p.EchoFire();
        }
    }

    // GLIMMER STONE marker: a sparkle on revealed ore / chests that lasts a while.
    public class Glint : Entity
    {
        readonly int x, y;
        readonly Block b;
        float life;

        public Glint(int x, int y, Block b, float life)
        {
            this.x = x; this.y = y; this.b = b; this.life = life;
            pos = new Vector2(x + 0.5f, y + 0.5f);
        }

        public override void Tick(float dt)
        {
            life -= dt;
            if (life <= 0f || Wd.Get(x, y) != b) { Kill(); return; }
            if (Random.value < 0.08f)
                G.fx.Burst(pos + new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-0.4f, 0.4f)), 1, new Color(0.7f, 1f, 1f), 0.6f, 0.5f, 0f, 1f, false);
        }

        public override void AddLight(Lighting l) => l.Add(pos, 1.4f + 0.2f * Mathf.Sin(Time.time * 5f + x), Mathf.Min(0.7f, life * 0.3f));
    }

    // CROSS LASER beam visual: a bright bar that flashes and fades.
    public class Beam : Entity
    {
        float t;
        const float Life = 0.45f;
        readonly Vector2 size;

        public override bool AlwaysActive => true;

        public Beam(Vector2 center, Vector2 size)
        {
            pos = center; this.size = size;
            MakeGO("Beam", Art.Square, 30);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        public override void Tick(float dt)
        {
            t += dt;
            if (t >= Life) { Kill(); return; }
            float k = t / Life;
            float thin = Mathf.Lerp(1f, 0.15f, k);
            go.transform.localScale = new Vector3(size.x > size.y ? size.x : size.x * thin, size.y > size.x ? size.y : size.y * thin, 1f);
            sr.color = Color.Lerp(new Color(1f, 1f, 1f, 1f), new Color(1f, 0.2f, 0.25f, 0f), k);
        }

        public override void AddLight(Lighting l)
        {
            float k = 1f - t / Life;
            if (size.x > size.y) for (float x = 2f; x < World.W - 2; x += 3f) l.Add(new Vector2(x, pos.y), 2.2f * k, k);
            else for (float y = -size.y * 0.5f; y < size.y * 0.5f; y += 3f) l.Add(new Vector2(pos.x, pos.y + y), 2.2f * k, k);
        }
    }

    // Consumable: drills a 1-wide shaft straight down.
    public class BoreCharge : Entity
    {
        readonly int x;
        int y, n = 10;
        float t;

        public override bool AlwaysActive => true;

        public BoreCharge(int x, int y)
        {
            this.x = x; this.y = y;
            pos = new Vector2(x + 0.5f, y + 0.5f);
            G.sfx.Play("dash", 0.8f, 0.7f);
        }

        public override void Tick(float dt)
        {
            t -= dt;
            if (t > 0f) return;
            t = 0.035f;
            pos = new Vector2(x + 0.5f, y + 0.5f);
            if (n <= 0) { Kill(); return; }
            var r = Wd.Damage(x, y, 99, true, G.player.GoldMul);
            if (r == World.Hit.Unbreakable) { G.sfx.Play("clank", 0.5f); Kill(); return; }
            G.fx.TileFlash(new Vector2Int(x, y));
            G.fx.Burst(pos, 4, new Color(0.8f, 0.6f, 1f), 4f, 0.3f, 10f);
            G.cam.Shake(0.1f);
            if (r == World.Hit.Broke) G.sfx.Play("dig", 0.6f, 1f + (10 - n) * 0.05f);
            y--; n--;
        }
    }
}

namespace DigDeep
{
    // SENTRY special: a little turret that drops where you stand and shoots the nearest foe it can see.
    public class Sentry : Entity
    {
        readonly Player owner;
        float life = 10f, cd = 0.4f, vy, recoil;
        Vector2 aimDir = Vector2.right;

        public Sentry(Vector2 feet, Player owner)
        {
            this.owner = owner;
            pos = feet;   // the sprite stands on its feet
            MakeGO("Sentry", Art.Icons["sentry"], 8);
            G.fx.Burst(pos + Vector2.up * 0.3f, 10, new Color(0.7f, 0.75f, 0.85f), 3f, 0.3f, 6f);
        }

        public override bool AlwaysActive => true;

        public override void Tick(float dt)
        {
            life -= dt;
            if (life <= 0f)
            {
                G.fx.Burst(pos, 14, new Color(0.7f, 0.75f, 0.85f), 4f, 0.4f, 8f);
                G.fx.Smoke(pos, 4, 0.8f);
                G.sfx.Play("clank", 0.5f, 1.4f);
                Kill();
                return;
            }
            // drop onto the ground
            int fx = Mathf.FloorToInt(pos.x), below = Mathf.FloorToInt(pos.y - 0.05f);
            if (!Wd.Solid(fx, below)) { vy = Fluids.Sink(pos + Vector2.up * 0.2f, Mathf.Max(vy - 30f * dt, -15f), dt); pos.y += vy * dt; }
            else { vy = 0f; pos.y = below + 1f; }
            int inside = Mathf.FloorToInt(pos.y + 0.1f);
            if (Wd.Solid(fx, inside)) pos.y = inside + 1f;   // buried by a block: pop up on top of it

            cd -= dt;
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 2f);
            Enemy best = null;
            float bd = 11f * 11f;
            Vector2 muzzle = pos + new Vector2(0f, 0.5f);
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en) || en.Intangible || !Teams.Hostile(Team.Player, en.team)) continue;
                float d = (en.pos - muzzle).sqrMagnitude;
                if (d < bd && Wd.LineClear(muzzle, en.pos)) { bd = d; best = en; }
            }
            if (best != null)
            {
                aimDir = (best.pos - muzzle).normalized;
                if (cd <= 0f)
                {
                    cd = 0.28f;
                    recoil = 0.12f;
                    Vector2 v = U.Rot(aimDir, Random.Range(-4f, 4f)) * 24f;
                    G.Spawn(new Bullet(muzzle + aimDir * 0.5f, v, Team.Player, owner.Dmg(1.3f) * owner.s.petMul, 0, "b_bullet", new Color(1f, 0.85f, 0.4f), 0.8f, 1.2f));
                    G.fx.Muzzle(muzzle + aimDir * 0.55f, 0.6f, new Color(1f, 0.85f, 0.4f));
                    G.sfx.Play("shoot", 0.25f, Random.Range(1.3f, 1.5f), 0.1f);
                }
            }
            Place();
            sr.flipX = aimDir.x < 0f;
            go.transform.position += (Vector3)(-aimDir * recoil);
            // blink when it's about to run out
            sr.color = life < 2f && ((int)(life * 8f)) % 2 == 0 ? new Color(1f, 0.5f, 0.5f) : Color.white;
        }

        public override void AddLight(Lighting l) => l.Add(pos, 1.6f, 0.5f);
    }
}

namespace DigDeep
{
    // A boulder knocked loose: a real physics body. It falls, rolls, can be pushed by you, enemies, kicks and
    // explosions, crushes what it lands on, and settles back into the grid (cracks and all) once it stops.
    public class BoulderBody : Entity
    {
        public static readonly System.Collections.Generic.List<BoulderBody> All = new System.Collections.Generic.List<BoulderBody>();
        public static readonly Vector2 H = new Vector2(0.45f, 0.45f);
        public Vector2 vel;
        int hp;
        float topY, restT, ang, hitCd;
        float pushHeld, pushedT;   // momentum: the longer you lean on it, the faster it rolls (up to a cap)
        bool grounded, wasWet = true;

        public override bool AlwaysActive => true;
        public float Speed => vel.magnitude;

        public float age, bumpCd;   // (the Boulder Troll bats its own boulders around)
        public Enemy thrower;       // a boulder never hurts whoever threw it

        // a boulder thrown from anywhere (the Boulder Troll's)
        public BoulderBody(Vector2 at, Vector2 v, int hp, Enemy thrower) : this(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y), v, hp)
        {
            pos = at; this.thrower = thrower; topY = pos.y;
            Place();
        }

        public BoulderBody(int x, int y, Vector2 v, int hp)
        {
            pos = new Vector2(x + 0.5f, y + 0.5f);
            vel = v;
            this.hp = Mathf.Max(1, hp);
            topY = pos.y;
            bio = Biomes.At(x, y);
            MakeGO("Boulder", Art.TileSprite(Block.Boulder, bio), 7);
            ShowCracks();
            All.Add(this);
            if (v.sqrMagnitude < 0.01f) G.sfx.Play("rumble", 0.6f);
        }

        // Explosions still crack a boulder while it's on the move.
        Biome bio;

        // the cracks stay on it while it rolls, exactly as damaged as the block was
        void ShowCracks()
        {
            if (sr == null) return;
            int max = Mathf.Max(1, World.MaxHp(Block.Boulder, Mathf.FloorToInt(pos.y)));
            float dmgFrac = 1f - Mathf.Clamp01(hp / (float)max);
            int level = dmgFrac <= 0.01f ? 0 : Mathf.Clamp(Mathf.RoundToInt(dmgFrac / 0.9f * 5f), 1, 5);
            sr.sprite = Art.CrackedTileSprite(Block.Boulder, bio, level);
        }

        public void Damage(int d)
        {
            if (dead || d <= 0) return;
            hp -= d;
            ShowCracks();
            G.fx.Burst(pos, 6, TileDraw.DebrisColor(Block.Boulder, Biomes.At(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y))), 4f, 0.4f, 20f);
            if (hp <= 0) Shatter();
        }

        public void Push(Vector2 impulse)
        {
            vel += impulse;
            restT = 0f;
        }

        public override void Tick(float dt)
        {
            hitCd -= dt; age += dt; bumpCd -= dt;
            pushedT -= dt;
            if (pushedT <= 0f) pushHeld = 0f;
            bool wet = Wd.FluidAtPoint(pos) == Fluids.Water;
            if (wet && !wasWet && age > 0.1f) Fluids.Impact(pos, 1.2f + Mathf.Min(Speed, 16f) * 0.08f, vel);   // a boulder hitting the water: the faster, the bigger the splash
            wasWet = wet;
            if (wet) Fluids.Stir(pos, vel, 0.55f);
            vel.y = Mathf.Max(vel.y - (wet ? 20f : 38f) * dt, wet ? -6f : -18f);
            // heavy: it grinds to a halt on flat ground, but not while someone keeps shoving it
            if (pushedT <= 0f) vel.x = Mathf.MoveTowards(vel.x, 0f, (grounded ? 7f : wet ? 2.5f : 0.5f) * dt);
            Vector2 p = pos;
            if (Wd.MoveX(ref p, H, vel.x * dt, out var hx))
            {
                float sp = Mathf.Abs(vel.x);
                int hdir = vel.x > 0f ? 1 : -1;
                if (hx.x != int.MinValue && Wd.ImpactGlass(hx.x, hx.y, sp, 2f)) vel.x *= 0.7f;   // it rolls right through glass
                else if (hx.x != int.MinValue && sp > 0.5f && Wd.Get(hx.x, hx.y) == Block.Boulder && !Wd.Solid(hx.x + hdir, hx.y))
                {
                    // it rolls into a resting boulder: that one's knocked loose and they share the push (heavy: neither flies)
                    Wd.PushBoulder(hx.x, hx.y, new Vector2(vel.x * 0.5f, 0f));
                    vel.x *= 0.5f;
                    pos = p;
                    if (sp > 3f) { G.cam.Shake(Mathf.Min(0.2f, sp * 0.02f)); G.sfx.Material(Block.Boulder, false, 0.8f); }
                }
                else
                {
                    if (sp > 7f)
                    {
                        G.cam.Shake(0.12f);
                        G.sfx.Material(Block.Boulder, false, 1f);
                        Wd.Impact(hx.x, hx.y, sp);   // smash into TNT and it goes off
                    }
                    // a hard-kicked boulder ploughs on through the wall: the faster, the more it smashes (and each block slows it)
                    if (sp > 6f && SmashThrough(hx, sp)) { vel.x *= 0.78f; pos = p; }
                    else vel.x *= -0.25f;
                }
            }
            float vy = vel.y;
            bool hitY = Wd.MoveY(ref p, H, vel.y * dt, out var hy);
            pos = p;
            if (hitY)
            {
                if (vy < 0f) { Landed(topY - pos.y, hy); if (dead) return; topY = pos.y; }   // (landed: the next fall counts from here)
                vel.y = 0f;
            }
            grounded = Wd.Ground(pos + new Vector2(0f, -0.05f), H);   // arena ledges and spear ledges hold it up too
            if (BumpBoulders(dt)) grounded = true;
            if (grounded || vel.y > 0f) topY = pos.y;

            // a boulder rolling or dropping over fire smothers it
            if (Speed > 0.5f)
            {
                Fire.DouseNear(pos, 1.0f);
                foreach (var e in G.ents)
                    if (!e.dead && e is Ember && (e.pos - pos).sqrMagnitude < 0.8f) { G.fx.Smoke(e.pos, 2, 0.4f); e.Kill(); }
            }
            // a boulder rolling or dropping over spikes flattens them
            if (Speed > 0.5f)
                for (int sx = Mathf.FloorToInt(pos.x - H.x); sx <= Mathf.FloorToInt(pos.x + H.x); sx++)
                    for (int sy = Mathf.FloorToInt(pos.y - H.y - 0.1f); sy <= Mathf.FloorToInt(pos.y + H.y); sy++)
                        if (Wd.Get(sx, sy) == Block.Spikes)
                        {
                            Wd.Set(sx, sy, Block.Air);
                            G.fx.Debris(new Vector2(sx + 0.5f, sy + 0.5f), new Color(0.65f, 0.65f, 0.7f), 8);
                            G.sfx.Play("break", 0.5f, 1.3f);
                        }
            ang -= vel.x * dt / H.x * Mathf.Rad2Deg;
            Place();
            if (grounded && Mathf.Abs(vel.x) < 0.6f) ang = Mathf.MoveTowardsAngle(ang, Mathf.Round(ang / 90f) * 90f, 360f * Time.deltaTime);   // rolls to a stop flat
            go.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            Collide();
            if (dead) return;
            // a moving boulder snaps any spear it rolls or drops onto
            if (Speed > 1f && Spear.All.Count > 0)
                for (int i = Spear.All.Count - 1; i >= 0; i--)
                    if (i < Spear.All.Count && Spear.All[i].Touches(pos, H + new Vector2(0.05f, 0.05f))) Spear.All[i].Snap();

            // stopped: slide onto the grid and become a block again
            if (grounded && pushedT <= 0f && Mathf.Abs(vel.x) < 0.6f && Mathf.Abs(vel.y) < 0.1f)
            {
                vel.x = 0f;
                float cx = Mathf.Floor(pos.x) + 0.5f;
                pos.x = Mathf.MoveTowards(pos.x, cx, 2.5f * dt);
                restT += dt;
                if (restT > 0.2f && Mathf.Abs(pos.x - cx) < 0.02f) Settle();
            }
            else restT = 0f;
        }

        bool SmashThrough(Vector2Int hit, float sp)
        {
            int dir = vel.x > 0f ? 1 : -1;
            int power = Mathf.RoundToInt(sp * 0.9f);
            bool any = false;
            int y0 = Mathf.FloorToInt(pos.y - H.y + 0.01f), y1 = Mathf.FloorToInt(pos.y + H.y - 0.01f);
            for (int y = y0; y <= y1; y++)
            {
                var b = Wd.Get(hit.x, y);
                if (!Wd.Solid(hit.x, y)) continue;
                if (Tiles.Def(b).unbreakable || Wd.Anchored(hit.x, y) || b == Block.Chest || b == Block.Crate || b == Block.Supply) return false;
                var r = Wd.Damage(hit.x, y, power, false);
                if (r == World.Hit.Unbreakable || r == World.Hit.Damaged) return false;
                any = true;
            }
            if (any) { G.cam.Shake(0.18f); G.sfx.Play("break", 0.6f, 0.8f); G.fx.Burst(new Vector2(hit.x + 0.5f, pos.y), 10, new Color(0.6f, 0.5f, 0.4f), 6f, 0.4f, 10f); }
            return any && !dead;
        }

        void Landed(float fell, Vector2Int hy)
        {
            if (hy.x != int.MinValue && Wd.ImpactGlass(hy.x, hy.y, fell * 6f, 3f)) { G.cam.Shake(0.1f); return; }   // drops onto glass and smashes it
            if (fell < 0.6f) return;
            int cx = Mathf.FloorToInt(pos.x), cy = Mathf.FloorToInt(pos.y);
            G.OnHeavyLanded(cx, cy);
            Wd.Impact(hy.x, hy.y, fell * 4f);
            G.cam.Shake(Mathf.Min(0.35f, 0.08f + fell * 0.04f));
            if (Wd.FluidAtPoint(pos) == Fluids.Water) G.sfx.PlayAt(pos, "boulder_wet", 0.8f, 1f, 0.08f, 16f);   // a deep, muffled thud on the bottom
            else G.sfx.Play("land", 0.7f, 0.6f);
            G.fx.Burst(pos - new Vector2(0f, H.y), 8, new Color(0.6f, 0.5f, 0.4f), 3f, 0.3f, 8f);
            // falling never breaks it: its health stays exactly as it was
        }

        public const float PushSpeed = 1.5f;      // the first nudge
        public const float PushMax = 4.2f;        // a boulder you've been leaning on for a while
        public const float HurtSpeed = 4.5f;      // slower than this it only shoves you

        // the speed a shove ramps up to after pushing for `held` seconds
        public static float PushTarget(float held) => Mathf.Lerp(PushSpeed, PushMax, Mathf.SmoothStep(0f, 1f, held / 1.6f));

        // keeps leaning on a loose boulder rolling (called by the player each frame they push it)
        public void Lean(int dir, float dt)
        {
            pushHeld += dt;
            pushedT = 0.12f;
            restT = 0f;
            float t = PushTarget(pushHeld) * Mathf.Clamp(G.player.Speed, 0.7f, 1.4f) / Mathf.Sqrt(Chain(dir));   // two in a row: heavier going
            if (vel.x * dir < t) vel.x = Mathf.MoveTowards(vel.x, dir * t, 6f * dt);
            if (Random.value < 0.1f) G.fx.Burst(pos - new Vector2(0f, H.y), 1, new Color(0.6f, 0.5f, 0.4f), 1.5f, 0.3f, 6f);
        }

        // the player stands on rolling boulders: they're solid on top (feet snap to the top, and you ride along)
        public static bool StandOn(Player pl, float dt)
        {
            if (pl.vel.y > 0.5f) return false;
            foreach (var b in All)
            {
                if (b.dead) continue;
                float top = b.pos.y + 0.5f;
                float feet = pl.pos.y - Player.Half.y;
                if (Mathf.Abs(pl.pos.x - b.pos.x) > H.x + Player.Half.x - 0.06f) continue;
                if (feet > top + 0.06f || feet < b.pos.y + 0.05f || pl.pos.y < b.pos.y + 0.3f) continue;
                var np = new Vector2(pl.pos.x, top + Player.Half.y);
                if (Wd.BoxSolid(np, Player.Half)) continue;
                if (Mathf.Abs(b.vel.x) > 0.01f) Wd.MoveX(ref np, Player.Half, b.vel.x * dt, out _);
                pl.pos = np;
                if (pl.vel.y < b.vel.y) pl.vel.y = Mathf.Max(0f, b.vel.y);
                return true;
            }
            return false;
        }

        // loose boulders against each other: side by side they shove each other along (sharing their speed, like two heavy
        // stones: a hard knock jolts, a slow one just pushes), and one landing on another rests on top of it
        bool BumpBoulders(float dt)
        {
            bool onOne = false;
            foreach (var o in All)
            {
                if (o == this || o.dead) continue;
                Vector2 d = o.pos - pos;
                float ox = 2f * H.x - Mathf.Abs(d.x), oy = 2f * H.y - Mathf.Abs(d.y);
                if (ox <= 0f || oy <= 0f) continue;
                if (ox < oy)
                {
                    float sd = d.x >= 0f ? 1f : -1f;
                    // pull apart (each half way, or all of it where one is against a wall)
                    var a = pos - new Vector2(sd * ox * 0.5f, 0f);
                    var b = o.pos + new Vector2(sd * ox * 0.5f, 0f);
                    bool aOk = !Wd.BoxSolid(a, H), bOk = !Wd.BoxSolid(b, H);
                    if (aOk && bOk) { pos = a; o.pos = b; }
                    else if (aOk) pos -= new Vector2(sd * ox, 0f);
                    else if (bOk) o.pos += new Vector2(sd * ox, 0f);
                    float closing = (vel.x - o.vel.x) * sd;
                    if (closing > 0f)
                    {
                        float m = (vel.x + o.vel.x) * 0.5f, bounce = closing * 0.15f;
                        vel.x = m - sd * bounce * 0.5f;
                        o.vel.x = m + sd * bounce * 0.5f;
                        o.restT = 0f;
                        o.pushedT = Mathf.Max(o.pushedT, pushedT);
                        if (closing > 3f && bumpCd <= 0f)
                        {
                            bumpCd = 0.25f;
                            G.cam.Shake(Mathf.Min(0.2f, closing * 0.025f));
                            G.sfx.Material(Block.Boulder, false, 0.8f);
                            G.fx.Burst((pos + o.pos) * 0.5f, 5, new Color(0.6f, 0.5f, 0.4f), 3f, 0.3f, 10f);
                        }
                    }
                }
                else if (d.y < 0f)
                {
                    // it's resting on the other one
                    var np = new Vector2(pos.x, o.pos.y + 2f * H.y);
                    if (!Wd.BoxSolid(np, H)) { pos = np; if (vel.y < o.vel.y) vel.y = o.vel.y; topY = pos.y; onOne = true; }
                }
            }
            return onOne;
        }

        // how many boulders in a row you're pushing, counting this one (each makes the shove slower)
        public int Chain(int dir)
        {
            int n = 1;
            Vector2 at = pos;
            for (int k = 0; k < 4; k++)
            {
                at += new Vector2(dir * 1f, 0f);
                int cx = Mathf.FloorToInt(at.x), cy = Mathf.FloorToInt(at.y);
                bool any = Wd.Get(cx, cy) == Block.Boulder;
                if (!any) foreach (var o in All) if (!o.dead && o != this && Mathf.Abs(o.pos.x - at.x) < 0.3f && Mathf.Abs(o.pos.y - at.y) < 0.5f) { any = true; break; }
                if (!any) break;
                n++;
            }
            return n;
        }

        // the loose boulder (if any) right where a player is leaning
        public static BoulderBody At(Vector2 p, Vector2 h)
        {
            foreach (var b in All) if (!b.dead && U.Overlap(p, h, b.pos, H)) return b;
            return null;
        }

        void Collide()
        {
            if (Speed > HurtSpeed) Keeper.HitAt(pos, H, Speed * Speed * 0.1f, pos - vel);   // a rolling boulder flattens a shopkeeper too
            var pl = G.player;
            if (pl.mode != Player.Mode.Dead && U.Overlap(pos, H, pl.pos, Player.Half))
            {
                Vector2 toPl = pl.pos - pos;
                float closing = Vector2.Dot(vel, toPl.normalized);   // only its own motion counts: just the side it's moving towards hurts
                bool fromAbove = vel.y < -3f && pl.pos.y < pos.y - 0.3f;
                bool onTop = toPl.y > H.y + Player.Half.y - 0.2f;
                if (!onTop && closing > HurtSpeed && Speed > HurtSpeed && hitCd <= 0f)
                {
                    hitCd = 0.4f;
                    if (fromAbove && pl.s.hardhat) { vel.y = 4f; vel.x += toPl.x < 0f ? 3f : -3f; }   // bonk off the hard hat
                    else
                    {
                        // damage grows with the square of its speed: a 4-block drop does ~250, a quick roll a scratch
                        int dmg = Mathf.Max(10, Mathf.RoundToInt(closing * closing * 0.83f));
                        Player.Cause = "A BOULDER";
                        pl.Hurt(dmg, pos);
                        vel *= 0.6f;
                    }
                }
                else if (!onTop && Mathf.Abs(toPl.y) < H.y + Player.Half.y - 0.15f)
                {
                    // it's solid from the side: you can't walk through it, only lean on it and shove it along
                    float side = Mathf.Sign(toPl.x == 0f ? -pl.vel.x : toPl.x);
                    var np = new Vector2(pos.x + side * (H.x + Player.Half.x + 0.001f), pl.pos.y);
                    if (!Wd.BoxSolid(np, Player.Half)) pl.pos.x = np.x;
                    // a slow boulder rolling into you just nudges you along
                    if (vel.x * side > 0.2f && pl.vel.x * side < Mathf.Abs(vel.x) + 0.3f) pl.vel.x = side * (Mathf.Abs(vel.x) + 0.3f);
                    bool pushing = Mathf.Abs(pl.vel.x) > 0.3f && Mathf.Sign(pl.vel.x) == -side;
                    if (pushing)
                    {
                        Lean((int)-side, Time.deltaTime);
                        // you can't outrun what you're pushing
                        float cap = Mathf.Abs(vel.x) + 0.4f;
                        pl.vel.x = Mathf.Clamp(pl.vel.x, -cap, cap);
                    }
                }
            }
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en) || en.Intangible || en == thrower || en is BoulderTroll || !U.Overlap(pos, H, en.pos, en.half)) continue;   // the troll's own rocks never hurt it
                float closing = Vector2.Dot(vel, (en.pos - pos).normalized);   // only what it's moving towards
                if (!(en is Boss) && Speed > 1.2f)
                {
                    // a rolling boulder flattens anything that isn't a boss
                    en.TakeShot(999999f, vel.normalized * 8f + Vector2.up * 3f);
                    continue;
                }
                if (closing > 2.5f && hitCd <= 0f)
                {
                    // a moving boulder flattens anything small; bosses just take a big hit
                    en.TakeShot(en is Boss ? closing * closing * 0.4f : 999999f, vel.normalized * 8f + Vector2.up * 3f);
                    G.HitStop(0.05f);
                    G.cam.Shake(0.2f);
                    if (en is Boss) { vel *= 0.4f; hitCd = 0.3f; }
                }
                else if (closing <= 2.5f && !(en is Boss) && Mathf.Abs(en.vel.x) > 0.3f && Mathf.Sign(en.vel.x) == Mathf.Sign(pos.x - en.pos.x))
                    vel.x = Mathf.MoveTowards(vel.x, Mathf.Sign(en.vel.x) * PushSpeed * 0.6f, 4f * Time.deltaTime);   // enemies shove it too
            }
        }

        void Settle()
        {
            int cx = Mathf.FloorToInt(pos.x), cy = Mathf.FloorToInt(pos.y);
            if (Wd.Solid(cx, cy)) { Shatter(); return; }
            // resting on a spear (not a real floor): it stays a body, lying there, instead of becoming a block that drops again
            if (!Wd.Solid(cx, cy - 1) && Wd.Get(cx, cy - 1) != Block.Plate) { restT = 0f; return; }
            // never turn back into a block on top of someone: wait until the cell is clear
            Vector2 cell = new Vector2(cx + 0.5f, cy + 0.5f), ch = new Vector2(0.5f, 0.5f);
            var pl = G.player;
            if (pl.mode != Player.Mode.Dead && U.Overlap(cell, ch, pl.pos, Player.Half)) { restT = 0f; return; }
            foreach (var e in G.ents)
                if (!e.dead && e is Enemy en && !en.Intangible && U.Overlap(cell, ch, en.pos, en.half)) { restT = 0f; return; }
            Kill();
            Wd.Set(cx, cy, Block.Boulder);
            Wd.SetHp(cx, cy, hp);
        }

        public void Shatter()
        {
            if (dead) return;
            G.fx.Debris(pos, TileDraw.DebrisColor(Block.Boulder, Biomes.At(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y))), 16);
            G.sfx.Play("break", 0.8f);
            G.cam.Shake(0.22f);
            Kill();
        }

        public override void Kill()
        {
            base.Kill();
            All.Remove(this);
        }
    }
}
