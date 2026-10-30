using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // The shot-changing trinkets that hook into every bullet: splits, ghosts, swelling shots, skipping,
    // wild effects, worms, curses that leave water behind... Everything a shot carries is copied into its
    // shards and ghosts, so every item combos with every other one.
    public partial class Bullet
    {
        bool poisons, chains, skip, ghost, wormy, swell;
        float swellSize, swellDmg, swellSpeed, swellMin, ghostKidT = -1f;

        // read by FriendlyWorm when a shot turns into a worm
        internal float Damage => dmg;
        internal float Radius => radius;
        internal float Size => size;
        internal Color Col => col;
        internal bool Burns => burns;
        internal bool Freezes => frost;
        internal bool Poisons => poisons;
        internal bool Explodes => explosive;
        internal Color Tint => FxTint(col);
        internal bool Homes => homing;
        internal int Pierces => pierce;
        internal int Bounces => bounces;
        internal bool IsLaser => laser;
        internal bool IsFlame => flame;
        internal bool Ghostly => ghost;

        void ApplyExtraEffects(Player p, Weapon w, AttackDef d)
        {
            var ps = p.s;
            // WILD CARD: every so often a shot gets a random extra effect on top of everything else
            if (ps.wildShots && Chance.Roll("wild", p.Lucky(0.3f)))
                switch (Random.Range(0, 7))
                {
                    case 0: if (radius <= 0f) explosive = true; else radius *= 1.4f; break;
                    case 1: poisons = true; break;
                    case 2: burns = true; break;
                    case 3: frost = true; break;
                    case 4: homing = true; break;
                    case 5: pierce = Mathf.Max(pierce, 999); break;
                    default: bounces += 2; break;
                }
            // KINDLING / TOXIC SKIN: a chance for any shot to burn or poison
            if (ps.fireChance > 0f && Chance.Roll("fire", p.Lucky(ps.fireChance))) burns = true;
            if (ps.poisonChance > 0f && Chance.Roll("poison", p.Lucky(ps.poisonChance))) poisons = true;
            bounces += ps.extraBounces;   // BOUNCY ROUNDS stack: one more bounce each
            knock *= ps.knockMul;
            // SKIPPING modifier: shots arc down and skip off floors
            if (w != null && w.Has(Mod.Skip) && !flame && !linger) { skip = true; grav += 26f; lob = false; }
            // SWELL SHOT: 3x as big (and as strong) out of the barrel, shrinking fast along with its speed
            if (ps.swell && !flame)
            {
                swell = true;
                swellSize = size; swellDmg = dmg; swellSpeed = vel.magnitude;
                swellMin = laser ? 0.5f : 0.3f;   // lasers start wide and thin down, but never below half
                size *= 3f; dmg *= 3f;
            }
            // THE WRITHING: gun shots hatch into friendly worms (there's a cap, so spray guns don't flood the cave)
            // (every weapon: flamethrower puffs hatch now and then too, as fire worms)
            if (ps.wormShots && d != null && (!flame || Random.value < 0.12f) && FriendlyWorm.Alive < 24) wormy = true;
            // SOUL SPLINTERS: a homing ghost bursts out of the shot a moment after it leaves the barrel
            if (ps.ghostSwarm && d != null && (d.delay >= 0.08f || Random.value < 0.3f)) ghostKidT = 0.12f;
        }

        // the colour of every damage type the shot carries
        Color FxTint(Color c)
        {
            Color acc = Color.black;
            int n = 0;
            if (burns) { acc += new Color(1f, 0.5f, 0.12f); n++; }
            if (poisons) { acc += new Color(0.5f, 1f, 0.3f); n++; }
            if (frost) { acc += new Color(0.55f, 0.9f, 1f); n++; }
            if (explosive) { acc += new Color(1f, 0.35f, 0.15f); n++; }
            if (homing && !nativeHoming) { acc += new Color(0.75f, 0.45f, 1f); n++; }
            if (chains) { acc += Mods.Col(Mod.Chain); n++; }
            if (n == 0) return c;
            acc /= n; acc.a = 1f;
            return acc;
        }

        // Runs every frame before the shot moves. True if the shot is gone.
        bool TickExtras(float dt)
        {
            // charged water sets off anything explosive
            if ((radius > 0f || explosive) && Electric.Level(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y)) > 0.12f)
            {
                G.fx.Burst(pos, 10, new Color(0.85f, 0.95f, 1f), 5f, 0.25f, 0f);
                if (radius > 0f) Explode(pos); else { ModBlast(pos); Kill(); }
                return true;
            }
            if (ghostKidT > 0f)
            {
                ghostKidT -= dt;
                if (ghostKidT <= 0f && owner != null)
                {
                    int n = 1 + (Chance.Roll("splinter", owner.Lucky(0.25f)) ? 1 : 0);
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 dir = U.Rot(vel.sqrMagnitude > 0.01f ? vel.normalized : Vector2.right, Random.Range(-50f, 50f));
                        var g = MakeGhost(pos, dir * 16f);
                        g.dmg = dmg * 0.35f; g.size = size * 0.6f; g.radius = radius * 0.5f; g.life = 1.3f;
                        g.homing = true; g.homeTarget = null; g.lockTarget = false; g.pierce = 0;
                        G.Spawn(g);
                    }
                }
            }
            return false;
        }

        void TickSwell()
        {
            float m = swellMin + (3f - swellMin) * Mathf.Exp(-age * 3.5f);
            size = swellSize * m;
            dmg = swellDmg * m;   // a shot that has shrunk to nothing barely scratches
            float spd = swellSpeed * Mathf.Lerp(0.35f, 1f, (m - swellMin) / (3f - swellMin));
            if (vel.sqrMagnitude > 0.01f) vel = vel.normalized * spd;
        }

        // SKIPPING: hitting a floor, the shot skips back up at half speed. Explosive shots pop a small (25%) blast
        // on every skip. Too slow to skip again: it ends (a bomb then goes off in full). True if it handled the hit.
        bool SkipOff(Vector2 prev, int cx, int cy)
        {
            int px = Mathf.FloorToInt(prev.x), py = Mathf.FloorToInt(prev.y);
            bool floorHit = py > cy && vel.y < 0f && !Wd.Solid(px, py) && Wd.Solid(px, cy);
            if (!floorHit) return false;
            pos = prev;
            vel = new Vector2(vel.x, -vel.y) * 0.5f;
            if (vel.magnitude < 7f) { Expire(); return true; }
            if (radius > 0f || explosive)
                G.Explode(prev, Mathf.Max(0.45f, (radius > 0f ? radius : 1.1f) * 0.25f), dmg * 0.25f, owner == null || owner.s.blastProof || !(selfHurt || modExplosive), true, false, false, team == Team.Player);
            G.fx.Burst(prev, 6, TileDraw.DebrisColor(Wd.Get(cx, cy), Biomes.At(cx, cy)), 4f, 0.3f, 20f);
            G.sfx.Play("ricochet", 0.25f, Random.Range(1.2f, 1.5f));
            return true;
        }

        // A shot hit something and is about to vanish: splits, spectral ghosts, the water curse.
        void OnImpact(Vector2 at, Vector2 outward, Entity justHit)
        {
            if (team != Team.Player || owner == null || returning || ghost) return;
            var s = owner.s;
            bool fast = w != null && w.def.delay < 0.08f;
            if (splitGen == 0 && !flame && (!fast || Random.value < 0.35f))   // fast spray guns only split now and then
            {
                if (s.splash) Shards(at, outward, justHit, 2, 0.25f * s.splashMul, false);
                if (s.splash4) Shards(at, outward, justHit, 4, 0.25f * s.splashMul, false);
                if (s.cloudSplit) Shards(at, outward, justHit, 3, 0.3f, true);
            }
            // SPECTRAL SHOTS: a ghost of the shot flies on through the rock, further the more digging power it had
            if (s.spectral && splitGen == 0 && !flame && !noPrism)
            {
                float range = Mathf.Clamp(1.5f + mine * Mathf.Max(1, s.digPower) * 1.2f, 2f, 14f);
                float sp = Mathf.Max(vel.magnitude, 12f);
                var g = MakeGhost(at + vel.normalized * 0.2f, vel.normalized * sp);
                g.life = range / sp;
                if (justHit != null) g.hit.Add(justHit);
                G.Spawn(g);
            }
            // CURSE OF WATER: every shot leaves a puddle where it lands
            if (owner.Cursed(Curse.Water) && splitGen == 0 && (!fast || Random.value < 0.3f)) WaterPocket(at);
        }

        void WaterPocket(Vector2 at)
        {
            int x = Mathf.FloorToInt(at.x), y = Mathf.FloorToInt(at.y);
            int placed = 0;
            for (int i = 0; i < 6 && placed < 3; i++)
            {
                int dx = i == 0 ? 0 : Random.Range(-1, 2), dy = i == 0 ? 0 : Random.Range(-1, 2);
                if (Wd.Solid(x + dx, y + dy) || Wd.LiquidType(x + dx, y + dy) == Fluids.Lava) continue;
                Wd.SetLiquid(x + dx, y + dy, Fluids.Water, 1f);
                placed++;
            }
            if (placed > 0) G.fx.Burst(at, 8, new Color(0.6f, 0.9f, 1f), 3f, 0.4f, 15f);
        }

        // Copy of this shot for splits and ghosts: same damage types, size, colour and everything else.
        Entity spareE;   // a split shard leaves the foe it burst from alone for a moment
        float spareT;

        // THE WRITHING: the shot flies (and digs, and hits) like a normal one; wherever it ends, a worm hatches
        public override void Kill()
        {
            if (dead) return;
            if (wormy && team == Team.Player && owner != null && owner == G.player && G.state == Game.State.Playing && FriendlyWorm.Alive < 24)
            {
                wormy = false;
                G.Spawn(new FriendlyWorm(owner, w, this));
            }
            // frost shots that end in water now and then flash-freeze a small patch (one in five, and never on you)
            if (frost && team == Team.Player && Wd.LiquidType(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y)) == Fluids.Water && Random.value < 0.2f)
                FreezeAt(pos, 0, false);
            base.Kill();
        }

        Bullet CloneShot(Vector2 at, Vector2 v)
        {
            var b = (Bullet)MemberwiseClone();
            b.go = null; b.sr = null; b.dead = false; b.first = true;
            b.hit = new HashSet<Entity>();
            b.pos = at; b.vel = v;
            b.age = 0f; b.tilePierce = 0; b.lastTile = new Vector2Int(int.MinValue, 0);
            b.homeTarget = null; b.lockTarget = false; b.crit = false;
            b.wormy = false; b.ghostKidT = -1f; b.swell = false;
            b.returning = false;
            b.spareE = null; b.spareT = 0f;
            return b;
        }

        void Shards(Vector2 at, Vector2 outward, Entity justHit, int n, float scale, bool cloud)
        {
            float baseA = Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg;
            bool wall = justHit == null;
            for (int i = 0; i < n; i++)
            {
                // off a wall they fan back out of it; off a foe they burst all around it
                float a = baseA + (wall ? (i - (n - 1) * 0.5f) * (cloud ? 55f : 40f) : i * 360f / n) + Random.Range(-12f, 12f);
                Vector2 dir = U.Rot(Vector2.right, a);
                var b = CloneShot(at + dir * 0.18f, dir * (cloud ? 2.2f : Mathf.Max(vel.magnitude * scale, 9f)));
                // the foe the shot burst on can be hit again by its shards, just not in the same instant
                b.spareE = justHit; b.spareT = justHit != null ? 0.08f : 0f;
                b.splitGen = splitGen + 1;
                b.dmg = Mathf.Max(0.2f, dmg * scale);
                if (radius > 0f) b.radius = Mathf.Max(0.4f, radius * scale);
                b.size = size * (cloud ? 0.5f : 0.6f);
                b.life = cloud ? Random.Range(2.6f, 3.4f) : Random.Range(0.3f, 0.45f);
                // they keep what the shot could still do: bounces (RICOCHET, BOUNCY ROUNDS), piercing, homing, lingering...
                b.blockPierce = 0; b.accel = 0f;
                if (homing) { b.homeT = 0f; b.life = Mathf.Max(b.life, 0.8f); }   // homing shards get time to find a target
                if (linger || bounces > 0) b.life = Mathf.Max(b.life, linger ? 3f : 0.9f);
                if (cloud) { b.linger = true; b.grav = 0f; b.lob = false; b.boomerang = false; }   // CLOUD BURST: they drift off slowly and hang there
                b.Spawned();
                G.Spawn(b);
            }
        }

        // A ghost of this shot: it passes through rock without harming it (the void still stops it), still hurts foes
        // and still sets off hazards. Exploding ghosts still explode when they run out, and that blast is real.
        Bullet MakeGhost(Vector2 at, Vector2 v)
        {
            var b = CloneShot(at, v);
            b.ghost = true; b.phase = true; b.mine = 0; b.splitGen = 1;
            b.bounces = 0; b.lob = false; b.grav = 0f; b.accel = 0f; b.flame = false; b.linger = false; b.boomerang = false;
            b.Spawned();
            return b;
        }

        // On-hit effects the shot itself carries (on top of what the weapon and trinkets already did)
        void OnHitExtras(Enemy en, float d, Vector2 dir)
        {
            if (owner == null || team != Team.Player) return;
            if (crit && owner.s.critCoin) G.Spawn(new Coin(en.pos + Vector2.up * 0.3f, 1));   // LUCKY MARBLE: crits knock a coin loose
            if (!en.dead)
            {
                if (burns) en.Ignite(2f);
                if (frost)
                {
                    en.Freeze(1.5f);
                    // a foe hit while swimming gets iced into the water around it
                    if (Wd.FluidAtPoint(en.pos) == Fluids.Water && Random.value < 0.5f) FreezeAt(en.pos, 1, false, en);
                }
                if (poisons) en.Poison(3.5f, Mathf.Max(0.3f, d * 0.25f));
                // BATTERING RAM: hard-hitting shots send foes flying into each other and the walls, like a kick
                if (owner.s.ramming && knock >= 7f) en.Launch(dir * Mathf.Min(24f, knock * 1.4f) + Vector2.up * 4f, d * 0.4f);
            }
        }
    }

    // THE WRITHING: a shot hatched into a friendly worm. It burrows through rock (without harming it) towards the
    // nearest foe, bites 3 times and dies, or wriggles off after 10 seconds. It keeps everything the shot carried:
    // its damage, explosions, poison, fire and frost, and the weapon's own modifiers.
    public class FriendlyWorm : Entity
    {
        public static int Alive;
        readonly Player owner;
        readonly Weapon w;
        readonly float radius, size;
        float dmg;
        readonly bool burns, frost, poisons, explodes, homes, laser, pierces;
        readonly Color col;
        float auraCd;   // fire, ice and poison worms hurt what they wriggle through, not just what they bite
        readonly Transform[] segs = new Transform[5];
        readonly List<Vector2> trail = new List<Vector2>();
        Vector2 vel;
        float life = 10f, biteCd, t;
        int bites = 3;
        bool counted;
        bool swords;     // THE WRITHING + THOUSAND BLADES: every worm carries a sword in its jaws
        Transform sword;

        public override bool AlwaysActive => true;

        public FriendlyWorm(Player p, Weapon w, Bullet b)
        {
            owner = p; this.w = w;
            pos = b.pos;
            vel = (b.vel.sqrMagnitude > 0.01f ? b.vel.normalized : Vector2.right) * 9f;
            dmg = Mathf.Max(b.Damage, p.Dmg(0.8f)) * 1.5f;   // slower than a bullet, so each bite hits harder
            radius = b.Radius; size = Mathf.Clamp(b.Size, 0.8f, 2f);
            burns = b.Burns || b.IsFlame; frost = b.Freezes; poisons = b.Poisons; explodes = b.Explodes;
            homes = b.Homes; laser = b.IsLaser; pierces = b.Pierces > 0;
            // it takes on the shot's colours: ice worms, fire worms, venom worms, laser worms...
            Color tint = b.Tint;
            bool element = burns || frost || poisons || explodes || homes;
            col = element ? tint : Color.Lerp(new Color(1f, 0.6f, 0.7f), b.Col, 0.5f);
            if (laser) col = Color.Lerp(col, Color.white, 0.25f);
            bites += Mathf.Min(2, b.Bounces) + (pierces ? 1 : 0);   // bouncy and piercing shots make longer-lived worms
            if (b.Ghostly) life *= 0.6f;
            Build(p);
        }

        // a worm flung off a melee swing (THE WRITHING on a pickaxe, sword, hammer...): it carries your shot items
        public FriendlyWorm(Player p, Weapon w, Vector2 at, Vector2 dir, float dmg)
        {
            owner = p; this.w = w;
            pos = at;
            vel = (dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.right) * 9f;
            this.dmg = Mathf.Max(dmg, p.Dmg(0.8f));
            radius = 0f; size = 1f;
            var ps = p.s;
            burns = ps.shotFire; frost = ps.shotFrost; poisons = ps.shotPoison; explodes = ps.shotExplode;
            homes = ps.shotHoming; laser = ps.shotLaser || ps.megaLaser; pierces = false;
            Color acc = Color.black; int n = 0;
            if (burns) { acc += new Color(1f, 0.5f, 0.12f); n++; }
            if (poisons) { acc += new Color(0.5f, 1f, 0.3f); n++; }
            if (frost) { acc += new Color(0.55f, 0.9f, 1f); n++; }
            if (explodes) { acc += new Color(1f, 0.35f, 0.15f); n++; }
            if (homes) { acc += new Color(0.75f, 0.45f, 1f); n++; }
            col = n > 0 ? new Color(acc.r / n, acc.g / n, acc.b / n, 1f) : new Color(1f, 0.6f, 0.7f);
            if (laser) col = Color.Lerp(col, Color.white, 0.25f);
            bites += Mathf.Min(2, ps.extraBounces);
            Build(p);
        }

        void Build(Player p)
        {
            MakeGO("Worm", Art.Circle, 16);
            sr.color = Color.Lerp(col, Color.white, 0.3f);
            go.transform.localScale = Vector3.one * 0.42f * size;
            for (int i = 0; i < segs.Length; i++)
            {
                var g = new GameObject("WormSeg");
                g.transform.SetParent(G.runRoot, false);
                var r = Art.AddSR(g, Art.Circle, 15);
                r.color = Color.Lerp(col, new Color(0.35f, 0.15f, 0.25f), i * 0.12f);
                g.transform.localScale = Vector3.one * (0.36f - i * 0.04f) * size;
                g.transform.position = new Vector3(pos.x, pos.y, 0f);
                segs[i] = g.transform;
            }
            Alive++; counted = true;
            t = Random.value * 6f;
            swords = p.s.swordShots;
            if (swords)
            {
                dmg *= 1.2f * p.s.meleeMul;   // melee damage, like the blades
                bites++;
                var sg = new GameObject("WormSword");
                sg.transform.SetParent(G.runRoot, false);
                Art.AddSR(sg, Art.Bullets["b_sword"], 17);
                sg.transform.localScale = Vector3.one * 0.9f * size;
                sword = sg.transform;
            }
        }

        public override void Tick(float dt)
        {
            life -= dt; biteCd -= dt; t += dt;
            if (owner != G.player) { Kill(); return; }
            if (life <= 0f) { Burst(); return; }
            Enemy target = null;
            float bd = homes ? 22f * 22f : 14f * 14f;   // homing worms sniff out prey from further away
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en) || en.Intangible || en.team == Team.Player) continue;
                float d2 = (en.pos - pos).sqrMagnitude;
                if (d2 < bd) { bd = d2; target = en; }
            }
            Vector2 goal = target != null ? target.pos : owner.pos + new Vector2(Mathf.Cos(t) * 2.5f, 1.5f + Mathf.Sin(t * 1.3f));
            Vector2 want = (goal - pos).normalized;
            want = U.Rot(want, Mathf.Sin(t * 9f) * 35f);   // it wriggles
            float spd = (target != null ? 9f : 6f) * (laser ? 1.4f : 1f) * (homes ? 1.15f : 1f);
            vel = Vector3.RotateTowards(vel, want * spd, (homes ? 9f : 6f) * dt, 30f * dt);
            TickElement(dt);
            Vector2 next = pos + vel * dt;
            // it burrows through rock, but bumps off the void
            if (Wd.Get(Mathf.FloorToInt(next.x), Mathf.FloorToInt(next.y)) == Block.Bedrock)
            {
                vel = -vel * 0.8f + Random.insideUnitCircle * 2f;
                G.fx.Burst(pos, 3, col, 2f, 0.2f, 0f);
            }
            else pos = next;
            if (Wd.Solid(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y)) && Random.value < 0.15f)
                G.fx.Burst(pos, 1, new Color(0.5f, 0.4f, 0.35f), 1.5f, 0.3f, 10f);
            if (target != null && biteCd <= 0f && U.Overlap(pos, new Vector2(0.3f, 0.3f) * size, target.pos, target.half)) Bite(target);
            if (dead) return;

            trail.Insert(0, pos);
            if (trail.Count > 40) trail.RemoveAt(trail.Count - 1);
            for (int i = 0; i < segs.Length; i++)
            {
                int idx = Mathf.Min(trail.Count - 1, (i + 1) * 4);
                Vector2 sp = trail[idx];
                segs[i].position = new Vector3(U.Snap(sp.x), U.Snap(sp.y), 0f);
            }
            go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y), 0f);
            if (sword != null)
            {
                Vector2 fwd = vel.sqrMagnitude > 0.01f ? vel.normalized : Vector2.right;
                sword.position = new Vector3(U.Snap(pos.x + fwd.x * 0.35f * size), U.Snap(pos.y + fwd.y * 0.35f * size), 0f);
                sword.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(fwd.y, fwd.x) * Mathf.Rad2Deg);
                // the blade bats enemy shots back, like the flying swords do
                Vector2 tip = pos + fwd * 0.5f * size;
                foreach (var e in G.ents)
                {
                    if (e.dead || !(e is Bullet eb) || eb.team == Team.Player || (eb.pos - tip).sqrMagnitude > 0.3f) continue;
                    eb.Deflect(Team.Player, fwd);
                    G.sfx.PlayAt(tip, "reflect", 0.4f, 1.3f, 0.1f);
                    G.fx.Sparks(eb.pos, new Color(0.8f, 0.95f, 1f));
                }
            }
        }

        void Bite(Enemy en)
        {
            Vector2 dir = vel.sqrMagnitude > 0.01f ? vel.normalized : Vector2.up;
            if (en.TakeShot(dmg, dir * 3f + Vector2.up * 2f))
            {
                owner.OnWeaponHit(en, w, dmg, pos, dir);
                if (!en.dead)
                {
                    if (burns) en.Ignite(2f);
                    if (frost) en.Freeze(1.5f);
                    if (poisons) en.Poison(3.5f, dmg * 0.25f);
                }
            }
            if (radius > 0f || explodes) G.Explode(pos, radius > 0f ? radius * 0.6f : 1f, dmg * 0.6f, true, true, false);
            if (swords)
            {
                // a slash: everything else right around the bite gets cut too
                G.HitEnemies(pos + dir * 0.4f, new Vector2(0.8f, 0.8f) * size, dmg * 0.6f, dir * 5f + Vector2.up * 2f);
                G.fx.Burst(pos + dir * 0.4f, 6, new Color(0.85f, 0.95f, 1f), 5f, 0.2f, 0f);
                G.sfx.Play("swing", 0.3f, 1.5f);
            }
            G.sfx.Play("splat", 0.3f, 1.6f);
            biteCd = 0.45f;
            vel = pierces ? dir * 7f : -dir * 5f;   // piercing worms bite straight through and keep going
            bites--;
            if (bites <= 0) Burst();
        }

        // the element trail: embers that light what can burn, frost that chills, venom that sickens
        void TickElement(float dt)
        {
            if (burns && Random.value < 0.35f) G.fx.Burst(pos, 1, new Color(1f, Random.Range(0.4f, 0.8f), 0.15f), 2f, 0.35f, -4f);
            if (frost && Random.value < 0.3f) G.fx.Burst(pos, 1, new Color(0.75f, 0.95f, 1f), 1.5f, 0.5f, 2f);
            if (poisons && Random.value < 0.3f) G.fx.Burst(pos, 1, new Color(0.5f, 1f, 0.3f), 1f, 0.6f, 6f);
            if (laser && Random.value < 0.4f) G.fx.Burst(pos, 1, col, 0.8f, 0.2f, 0f);
            if (burns && Random.value < dt * 3f)
            {
                int x = Mathf.FloorToInt(pos.x), y = Mathf.FloorToInt(pos.y);
                if (Fire.HasFuel(x, y)) Fire.Ignite(x, y);
            }
            auraCd -= dt;
            if (auraCd > 0f || !(burns || frost || poisons)) return;
            auraCd = 0.5f;
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en) || en.Intangible || en.team == Team.Player) continue;
                if (!U.Overlap(pos, new Vector2(0.5f, 0.5f) * size, en.pos, en.half)) continue;
                if (burns) en.Ignite(1.5f);
                if (frost) en.Freeze(0.6f);
                if (poisons) en.Poison(2f, dmg * 0.15f);
            }
        }

        void Burst()
        {
            G.fx.Burst(pos, 12, col, 4f, 0.4f, 10f);
            // going out with a bang (or a frost ring, or a cloud of venom)
            if (explodes || radius > 0f) G.Explode(pos, Mathf.Max(1.2f, radius * 0.8f), dmg * 0.8f, true, true, false);
            if (frost || poisons)
                foreach (var e in G.ents)
                {
                    if (e.dead || !(e is Enemy en) || en.Intangible || en.team == Team.Player || (en.pos - pos).sqrMagnitude > 4f) continue;
                    if (frost) en.Freeze(1.2f);
                    if (poisons) en.Poison(3f, dmg * 0.2f);
                }
            if (frost) G.fx.Ring(pos, 1.6f, new Color(0.75f, 0.95f, 1f));
            Kill();
        }

        public override void Kill()
        {
            if (dead) return;
            base.Kill();
            if (sword != null) Object.Destroy(sword.gameObject);
            foreach (var s in segs) if (s != null) Object.Destroy(s.gameObject);
            if (counted) { Alive--; counted = false; }
        }

        public override void AddLight(Lighting l) => l.Add(pos, 1.2f, 0.4f);
    }
}
