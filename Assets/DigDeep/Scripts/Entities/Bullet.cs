using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Every shot in the game: yours, the raiders', the spitters'. Fat, fast and loud.
    // Player shots chew through blocks; enemy shots hurt whoever their team hates.
    public partial class Bullet : Entity
    {
        public Team team;
        public bool poison;   // an enemy seed that poisons you
        public bool zaps;     // a drone's spark: it shocks instead of hurting normally
        public Vector2 vel;
        float dmg, life, age, knock, size, radius, grav, accel;
        int pierce, bounces, mine, tilePierce, blockPierce, playerDmg = 60;
        Vector2Int lastTile = new Vector2Int(int.MinValue, 0);
        bool lob, flame, crit, homing, nativeHoming, selfHurt, explosive, sharp, wallBreaker, highCal, hurtsOwner, first = true;
        bool burns, frost, modExplosive, laser, tinted, noPrism;   // item effects: see ApplyPlayerEffects
        bool linger, boomerang, returning, sword, mega, petShot;
        float stretch = 1f;
        int splitGen;                                            // SPLASH DAMAGE: shards don't split again
        float blastScale = 1f;                                   // charged shots: bigger EXPLOSIVE blasts
        Player owner;
        Weapon w;
        Color col;
        Sprite main;
        int chainGen;
        bool phase, lockTarget;
        HashSet<Entity> hit = new HashSet<Entity>();
        Enemy homeTarget;
        float homeT, spinA;

        public override bool AlwaysActive => true;

        // ---------------------------------------------------------------- player shot
        public Bullet(Player p, Weapon w, Vector2 at, Vector2 v, bool echo)
        {
            var d = w.def;
            owner = p; this.w = w; team = Team.Player;
            pos = at; vel = v;
            highCal = w.Has(Mod.HighCaliber);
            dmg = p.Dmg(d.dmg) * (highCal ? 2.5f : 1f) * (w.Has(Mod.Ricochet) && d.abuses ? 0.7f : 1f) * p.nextShotMul;
            crit = Random.value < p.CritChance + (w.Has(Mod.Crit) ? 0.2f : 0f) || (echo && p.Combo("echochamber") && Random.value < 0.5f) || p.nextShotCrit;
            if (crit) dmg *= p.s.critMul;
            life = d.life;
            knock = d.knock * (highCal ? 1.8f : 1f);
            size = d.size * (highCal ? 1.4f : 1f);
            pierce = w.Has(Mod.Pierce) ? 999 : d.pierce;
            bounces = d.bounces + (w.Has(Mod.Ricochet) ? 1 : 0);
            mine = d.mine;
            blockPierce = d.blockPierce;
            wallBreaker = w.Has(Mod.WallBreaker);
            radius = d.radius;
            explosive = w.Has(Mod.Explosive);
            grav = d.grav; lob = d.lob; accel = d.accel; flame = d.flame;
            nativeHoming = d.homing;
            homing = d.homing || w.Has(Mod.Homing);
            selfHurt = d.selfHurt;
            sharp = w.Has(Mod.Sharpshooter);
            col = echo ? new Color(0.8f, 0.6f, 1f) : crit ? new Color(1f, 0.55f, 0.45f) : d.col;
            main = Art.Bullets.TryGetValue(d.bullet, out var s) ? s : Art.Bullets["b_bullet"];

            ApplyPlayerEffects(p, w, d, echo || crit);
            Spawned();
        }

        // ---------------------------------------------------------------- enemy shot
        public Bullet(Vector2 at, Vector2 v, Team team, float dmgVsEnemies, int dmgVsPlayer, string sprite, Color col, float size = 1f, float life = 3f)
        {
            this.team = team; pos = at; vel = v;
            dmg = dmgVsEnemies; playerDmg = dmgVsPlayer;
            this.col = col; this.size = size; this.life = life;
            knock = 3f;
            main = Art.Bullets.TryGetValue(sprite, out var s) ? s : Art.Bullets["b_enemy"];
            Spawned();
        }

        public Bullet Lobbed(float gravity, float blastRadius) { lob = true; grav = gravity; radius = blastRadius; return this; }

        // Everything your trinkets do to "every shot". Your own shots, SHRAPNEL PRISM lasers and SPLASH shards (copies)
        // all go through here, so every item combos with every other one: prism lasers turn green with VIPER FANG,
        // bend with SEEKER'S EYE, grow with the GOD-RAY, linger with LINGERING SHOTS...
        // d is null for shots that don't come out of a gun (prism lasers).
        void ApplyPlayerEffects(Player p, Weapon w, AttackDef d, bool special)
        {
            var ps = p.s;
            modExplosive = w != null && w.Has(Mod.Explosive);
            burns = flame || (w != null && w.Has(Mod.Fire)) || ps.shotFire;
            frost = (w != null && w.Has(Mod.Frost)) || ps.shotFrost;
            poisons = (w != null && w.Has(Mod.Poison)) || ps.shotPoison;
            chains = w != null && w.Has(Mod.Chain);
            homing |= ps.shotHoming;
            float delay = d != null ? d.delay : 0.2f;
            // ALL SHOTS EXPLODE: very fast guns (and prism lasers) only blow up some of the time, or the world would vanish
            if (ps.shotExplode && radius <= 0f && (noPrism ? Random.value < 0.4f : delay >= 0.08f || Random.value < 0.3f)) explosive = true;

            // ---- lasers: LASER LENS and the GOD-RAY turn shots into lasers; laser guns and prism lasers already are
            bool nativeLaser = main == Art.Bullets["b_laser"];
            if ((ps.shotLaser || ps.megaLaser) && d != null && !nativeLaser)
            {
                // fast, straight and piercing. Bombs and rockets become lasers that blow up on whatever they hit.
                main = Art.Bullets["b_laser"];
                float sp = Mathf.Clamp(vel.magnitude * 1.6f, 40f, 62f);
                vel = vel.normalized * sp;
                life = flame ? 0.16f : Mathf.Clamp(d.life * d.speed / sp * 1.3f, 0.3f, 1.4f);
                if (lob) life = 0.9f;
                pierce = radius > 0f ? 0 : 999;
                lob = false; grav = 0f; accel = 0f; flame = false; bounces = Mathf.Min(bounces, 1);
                if (d.abuses) dmg *= 0.8f;   // spray guns pierce a lot of foes now
                if (!special) col = new Color(1f, 0.25f, 0.3f);
            }
            laser = main == Art.Bullets["b_laser"];
            if (laser)
            {
                stretch = 1.3f;
                if (ps.shotLaser && d == null) { pierce = 999; vel *= 1.3f; }   // LASER LENS sharpens prism lasers too
                if (ps.megaLaser)
                {
                    // GOD-RAY: every laser becomes a huge, epic beam
                    mega = true;
                    size *= 2.1f; dmg *= 1.5f; knock *= 1.5f; blockPierce += 2; mine = Mathf.Max(mine, 2); stretch = 2f; life *= 1.25f;
                    pierce = Mathf.Max(pierce, radius > 0f ? 0 : 999);
                    if (!special && col == new Color(1f, 0.25f, 0.3f)) col = new Color(1f, 0.93f, 0.55f);
                }
            }
            // THOUSAND BLADES: shots become flying swords that deal melee damage (lasers stay lasers, but still count as melee)
            if (ps.swordShots)
            {
                dmg *= 1.2f * ps.meleeMul;
                knock *= 1.6f;
                if (!laser && !flame) { sword = true; main = Art.Bullets["b_sword"]; stretch = 1f; }
            }
            // LINGERING SHOTS: they slow right down and hang in the air. The BOOMERANG curse wins over it.
            boomerang = p.Cursed(Curse.Boomerang) && !flame;
            linger = ps.linger && !flame && !boomerang;
            if (linger) { life = Mathf.Max(life, 3.5f); grav = 0f; accel = 0f; }
            if (boomerang) life = Mathf.Max(life, 3f);
            // PRESSURE CHAMBER: a charged shot is bigger, hits harder and blows up bigger (up to 2.5x)
            if (d != null && p.chargedMul > 1f)
            {
                float m = p.chargedMul;
                dmg *= m; size *= m; radius *= m; knock *= Mathf.Sqrt(m); blastScale = m;
            }
            // shots grow with your damage (and shrink when you trade it for fire rate)
            size *= Mathf.Clamp(Mathf.Pow(Mathf.Max(0.2f, p.Dmg(1f)), 0.35f), 0.75f, 1.9f) * ps.shotSize;
            ApplyExtraEffects(p, w, d);
            // and they take on the colour of every damage type they carry, from items and weapon modifiers alike
            if (!special) { var t = FxTint(col); tinted = t != col || laser; col = t; }
        }

        // SHRAPNEL PRISM: a laser thrown out of a blast. It never explodes into more lasers (DEATH STAR: it does explode).
        // It carries every shot item you own.
        public static Bullet Prism(Player p, Vector2 at, Vector2 dir, float dmg, bool blows)
        {
            var b = new Bullet(at, dir * 36f, Team.Player, dmg, 0, "b_laser", new Color(1f, 0.35f, 0.9f), 1f, 0.42f);
            b.owner = p;
            b.mine = 1;
            b.pierce = 2;
            b.knock = 4f;
            b.noPrism = true;
            if (blows) b.radius = 0.85f;
            b.ApplyPlayerEffects(p, null, null, false);
            return b;
        }

        // A pet's shot: hurts foes only. It never digs, sets anything off, lights grass or hits altars.
        public static Bullet Pet(Vector2 at, Vector2 v, float dmg, Color col)
        {
            var b = new Bullet(at, v, Team.Player, dmg, 0, "b_laser", col, 0.7f, 0.9f);
            b.petShot = true;
            b.knock = 2f;
            b.laser = true;
            b.tinted = true;
            return b;
        }

        // CHAIN modifier: a crackling spark that hunts down one particular enemy.
        public static Bullet ChainShot(Player p, Weapon w, Vector2 at, Enemy target, float dmg, int gen, HashSet<Entity> exclude, bool phase)
        {
            var b = new Bullet(p, w, at, (target.pos - at).normalized * 26f, false);
            b.dmg = dmg;
            b.chainGen = gen;
            b.phase = phase;
            b.lob = false; b.grav = 0f; b.accel = 0f; b.flame = false; b.radius = 0f; b.explosive = false;
            b.bounces = 0; b.pierce = 0; b.mine = phase ? 0 : 1; b.life = 1.2f; b.size = 0.85f;
            b.homing = true; b.lockTarget = true; b.homeTarget = target;
            b.main = Art.Bullets["b_laser"];
            b.col = Mods.Col(Mod.Chain);
            foreach (var e in exclude) b.hit.Add(e);
            return b;
        }

        void Spawned()
        {
            // the first frame of every shot is a big bright circle: the muzzle ignition
            MakeGO("Shot", Art.Circle, 15);
            go.transform.localScale = Vector3.one * 0.75f * Mathf.Max(0.8f, size);
            sr.color = Color.Lerp(Color.white, col, 0.25f);
        }

        public float Speed => vel.magnitude;

        // flamethrower flames and FIRE-modded shots set things alight
        bool Hot => team == Team.Player && (flame || burns);

        public void Deflect(Team t, Vector2 dir)
        {
            team = t;
            float sp = Mathf.Max(vel.magnitude, 14f) * 1.25f;
            vel = dir.normalized * sp;
            hit.Clear();
            life = Mathf.Max(life, 1.2f);
            homing = false;
            if (t == Team.Player)
            {
                owner = G.player;
                w = null;
                dmg = Mathf.Max(dmg, owner.Dmg(3f)) * 1.5f;
                knock = 8f;
                col = new Color(0.6f, 0.95f, 1f);
            }
            else
            {
                owner = null;
                col = new Color(1f, 0.45f, 0.95f);
            }
            sr.color = col;
        }

        public override void Tick(float dt)
        {
            age += dt; life -= dt; spareT -= dt;
            if (first)
            {
                first = false;
                sr.sprite = main;
                sr.color = flame ? new Color(1f, 0.95f, 0.6f) : Color.white;
                if (team != Team.Player || crit || w == null || tinted || laser) sr.color = Color.Lerp(Color.white, col, laser ? 0.75f : 0.55f);
                if (ghost) { var gc = Color.Lerp(sr.color, new Color(0.8f, 0.9f, 1f), 0.5f); gc.a = 0.5f; sr.color = gc; }
            }
            if (life <= 0f) { Expire(); return; }
            if (TickExtras(dt)) return;

            // fluids: water drags shots down, puts out fire; frost shots freeze water into an ice platform
            byte fl = Wd.FluidAtPoint(pos);
            if (fl == Fluids.Water)
            {
                if (flame && team == Team.Player && age < 0.04f)
                {
                    // point blank: the first instant of a flame still bites (and digs) before the water snuffs it
                    vel *= 0.3f;
                    life = Mathf.Min(life, 0.03f);
                }
                else if (flame || main == Art.Bullets["b_fire"])
                {
                    // fire boils water away into steam
                    int wx = Mathf.FloorToInt(pos.x), wy = Mathf.FloorToInt(pos.y);
                    float wa = Wd.LiquidAmount(wx, wy) - (flame ? 0.06f : 0.2f);
                    if (wa <= 0.02f) Wd.SetLiquid(wx, wy, Fluids.None, 0f); else Wd.SetLiquid(wx, wy, Fluids.Water, wa);
                    Wd.AddSteam(wx, wy + 1, flame ? 0.1f : 0.3f);
                    G.fx.Smoke(pos, 2, 0.7f);
                    Kill();
                    return;
                }
                // frost shots no longer freeze on touching water: they swim on and dig like any shot; a few of
                // them flash-freeze the water where they finally stop (see Kill), and foes they hit in water get iced in
                vel *= 1f - Mathf.Min(0.9f, 4f * dt);
                life -= dt;
            }
            else if (fl == Fluids.Lava && frost && team == Team.Player) { FreezeAround(); Kill(); return; }
            // LINGERING SHOTS: brake hard, then hang in the air, drifting
            if (linger)
            {
                float sp = vel.magnitude;
                if (sp > 1.1f) vel *= Mathf.Max(1.1f / sp, 1f - 3.2f * dt);
                if (life < 0.4f && sr != null) sr.enabled = ((int)(life * 20f)) % 2 == 0;   // blink out
            }
            // CURSE OF THE BOOMERANG: shots stall fast, then fly back to you (through rock) and vanish in your hands
            if (boomerang && owner != null)
            {
                if (!returning)
                {
                    vel *= 1f - Mathf.Min(0.9f, 6f * dt);
                    if (vel.magnitude < 3f || age > 0.6f) { returning = true; hit.Clear(); phase = true; homing = false; grav = 0f; accel = 0f; }
                }
                else
                {
                    Vector2 to = owner.pos - pos;
                    float dd = to.magnitude;
                    if (dd < 0.6f) { Kill(); return; }
                    vel = Vector2.MoveTowards(vel, to / dd * 22f, 80f * dt);
                    life = Mathf.Max(life, 0.3f);
                }
            }
            if (sword) DeflectNear();
            if (mega && Random.value < 0.6f) G.fx.Burst(pos - vel.normalized * 0.4f * size, 1, col, 1.5f, 0.25f, 0f, 1.4f, false);
            if (grav > 0f) vel.y -= grav * dt;
            if (accel > 0f) vel = Vector2.ClampMagnitude(vel + vel.normalized * accel * dt, 42f);
            if (flame) vel *= 1f - 2.2f * dt;
            if (homing) Steer(dt);
            if (swell) TickSwell();

            float dist = vel.magnitude * dt;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.22f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 prev = pos;
                pos += vel * dt / steps;
                int cx = Mathf.FloorToInt(pos.x), cy = Mathf.FloorToInt(pos.y);
                // nothing passes the void: ghosts, returning boomerangs and phasing sparks stop dead at it
                if (phase && Wd.Get(cx, cy) == Block.Bedrock) { if (radius > 0f) Explode(prev); else Kill(); return; }
                if (ghost && Wd.Get(cx, cy) == Block.TNT) Wd.Impact(cx, cy, 99f);   // ghosts still set off hazards
                if (team == Team.Player && owner != null && !returning && Wd.Get(cx, cy) == Block.Spikes)
                {
                    // spikes snap off when shot
                    owner.MineCell(cx, cy, 1, vel.normalized);
                    G.fx.Sparks(pos, new Color(0.85f, 0.85f, 0.9f));
                    G.sfx.Material(Block.Spikes, true, 0.8f);
                }
                // fire shots light grass and vines they fly through
                if (Hot && Fire.HasFuel(cx, cy) && !Wd.Solid(cx, cy)) Fire.Ignite(cx, cy);
                if (!phase && Wd.Solid(cx, cy) && !(cx == lastTile.x && cy == lastTile.y))
                {
                    if (Hot) Fire.Ignite(cx, cy);   // wood, chests and crates catch; rock doesn't
                    if (!HitTile(prev, cx, cy)) return;
                    continue;
                }
                if (HitEntities() || dead) return;
            }

            // effect trails: embers, venom, frost
            if (tinted && Random.value < 0.35f) G.fx.Burst(pos, 1, col, 1f, 0.25f, burns ? -4f : 4f, 0.8f, false);
            // smoke trails behind grenades and rockets
            if ((accel > 0f || lob || (nativeHoming && radius > 0f)) && Random.value < 0.6f) G.fx.Smoke(pos - vel.normalized * 0.2f, 1, 0.55f);
            if (flame)
            {
                float k = 1f - life / 0.38f;
                float sc = Mathf.Lerp(0.7f, 1.9f, k) * size;
                go.transform.localScale = new Vector3(sc, sc, 1f);
                var c = Color.Lerp(new Color(1f, 0.92f, 0.5f), new Color(0.75f, 0.12f, 0.05f), k);
                c.a = 1f - k * 0.7f;
                sr.color = c;
                if (Random.value < 0.08f) G.fx.Smoke(pos, 1, 0.6f);
            }
            else
            {
                float s = size;
                go.transform.localScale = new Vector3(s * stretch, s, 1f);
            }
            if ((lob && !linger) || main == Art.Bullets["b_disc"]) { spinA += dt * (lob ? -500f : 1400f); go.transform.rotation = Quaternion.Euler(0, 0, spinA); }
            else go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg);
            Place();
        }

        void Steer(float dt)
        {
            homeT -= dt;
            if (lockTarget && homeTarget != null && !homeTarget.dead) homeT = 1f;
            else if (homeT <= 0f || homeTarget == null || homeTarget.dead)
            {
                homeT = 0.08f;
                homeTarget = null;
                float best = float.MaxValue;
                // pick among every foe the shot can actually see: closest and most in front wins
                foreach (var e in G.ents)
                {
                    if (e.dead || !(e is Enemy en) || en.Intangible || hit.Contains(en) || !Teams.Hostile(team, en.team)) continue;
                    Vector2 v = en.pos - pos;
                    float d = v.magnitude;
                    if (d > 11f) continue;
                    float ang = Vector2.Angle(vel, v);
                    if (ang > 110f) continue;
                    if (!ghost && !Wd.LineClear(pos, en.pos)) continue;   // ghosts home in through rock
                    float score = d * (1f + ang / 60f);
                    if (score < best) { best = score; homeTarget = en; }
                }
            }
            if (homeTarget == null) return;
            Vector2 want = (homeTarget.pos - pos).normalized;
            float turn = (lockTarget ? 900f : nativeHoming ? 430f : 320f) * (laser ? 2.2f : 1f) * dt * Mathf.Deg2Rad;   // lasers bend hard
            vel = Vector3.RotateTowards(vel, want * vel.magnitude, turn, 0f);
        }

        // Returns true if the shot keeps going.
        bool HitTile(Vector2 prev, int cx, int cy)
        {
            Block b = Wd.Get(cx, cy);
            if (petShot)
            {
                // pets' shots never touch the world: they just fizzle on it
                G.fx.Burst(prev, 4, col, 3f, 0.15f, 0f);
                Kill();
                return false;
            }
            if (b == Block.Bedrock)
            {
                // the void swallows every shot (bombs still go off against it)
                if (radius > 0f || (lob && team == Team.Player)) { pos = prev; Explode(prev); } else { G.fx.Burst(prev, 4, col, 3f, 0.15f, 0f); Kill(); }
                return false;
            }
            if (skip && SkipOff(prev, cx, cy)) return !dead;   // SKIPPING: off the floor like a stone off water
            Wd.Impact(cx, cy, Mathf.Max(vel.magnitude, 10f));   // shots always set off TNT
            if (b == Block.Ice && (flame || main == Art.Bullets["b_fire"]))
            {
                // fire melts ice into steam
                Wd.Set(cx, cy, Block.Air);
                Wd.AddSteam(cx, cy, 0.6f);
                G.fx.Smoke(new Vector2(cx + 0.5f, cy + 0.5f), 4, 0.9f);
                Kill();
                return false;
            }
            if (lob && team == Team.Player && owner != null)
            {
                // player grenades (launcher, mortar) go off the moment they hit a wall
                pos = prev;
                OnImpact(prev, -vel.normalized, null);
                Explode(prev);
                return false;
            }
            if (lob)
            {
                Reflect(prev, cx, cy);
                vel *= 0.55f;
                pos = prev;
                if (vel.magnitude > 3f) G.sfx.Play("land", 0.2f, 2f);
                return true;
            }

            var r = World.Hit.Unbreakable;
            if (team == Team.Player && owner != null && mine > 0)
            {
                var def = Tiles.Def(b);
                int power = Mathf.Max(1, owner.s.digPower) * mine * (wallBreaker ? 3 : 1) * (highCal ? 2 : 1);
                if (power > 0) r = owner.MineCell(cx, cy, power, vel.normalized);
                else r = World.Hit.Damaged;
            }

            // hard-hitting shots shove boulders
            if (b == Block.Boulder && r != World.Hit.Broke && knock >= 5f) Wd.PushBoulder(cx, cy, vel.normalized * knock * 0.6f + Vector2.up * 1.5f);
            Color dc = TileDraw.DebrisColor(b, Biomes.At(cx, cy));
            if (flame) G.fx.Smoke(prev, 1, 0.8f);
            else
            {
                G.fx.Burst(prev, 4, team == Team.Player ? new Color(1f, 0.9f, 0.6f) : col, 7f, 0.15f, 10f);
                G.fx.Burst(prev, 3, dc, 5f, 0.6f, 32f, 1.5f);
                if (team == Team.Player) G.fx.Muzzle(prev, 0.4f * Mathf.Max(0.8f, size), Color.white);   // a bright impact pop
                if (r == World.Hit.Broke && team == Team.Player) G.cam.Shake(0.05f);
                if (r == World.Hit.Damaged) G.fx.TileFlash(new Vector2Int(cx, cy));
                if (r == World.Hit.Broke) G.sfx.Play("dig", 0.12f, Random.Range(0.95f, 1.35f), 0.1f);
                G.sfx.Material(b, r == World.Hit.Broke, team == Team.Player ? 1f : 0.6f);
            }

            if (radius > 0f) { OnImpact(prev, -vel.normalized, null); Explode(prev); return false; }   // (lobbed shells explode on their fuse instead)
            if (explosive) ModBlast(prev);   // the EXPLOSIVE modifier pops on every hit; the shot carries on
            int maxPierce = Mathf.Max(blockPierce, wallBreaker ? 2 : 0);
            if (r == World.Hit.Broke && tilePierce < maxPierce) { tilePierce++; return true; }
            // high-calibre rounds punch straight through blocks they don't break
            if (r == World.Hit.Damaged && blockPierce > 0 && tilePierce < blockPierce)
            {
                tilePierce++;
                lastTile = new Vector2Int(cx, cy);
                return true;
            }
            if (bounces > 0 && !flame)
            {
                bounces--;
                Reflect(prev, cx, cy);
                pos = prev;
                hurtsOwner = selfHurt;
                G.sfx.Play("ricochet", 0.3f, Random.Range(0.9f, 1.3f));
                return true;
            }
            OnImpact(prev, -vel.normalized, null);
            Kill();
            return false;
        }

        void FreezeAround() => FreezeAt(pos, 1, true);

        // Turns water (and lava) around a point into ice (and obsidian), never in the cells you're standing in.
        public static void FreezeAt(Vector2 at, int r, bool lavaToo, Entity keepClear = null)
        {
            int cx = Mathf.FloorToInt(at.x), cy = Mathf.FloorToInt(at.y);
            var pl = G.player;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    byte t = Wd.LiquidType(x, y);
                    if (t != Fluids.Water && !(lavaToo && t == Fluids.Lava)) continue;
                    var cc = new Vector2(x + 0.5f, y + 0.5f);
                    if (pl != null && U.Overlap(cc, new Vector2(0.5f, 0.5f), pl.pos, Player.Half)) continue;
                    if (keepClear is Enemy ke && U.Overlap(cc, new Vector2(0.5f, 0.5f), ke.pos, ke.half)) continue;
                    Wd.SetLiquid(x, y, Fluids.None, 0f);
                    Wd.Set(x, y, t == Fluids.Water ? Block.Ice : Block.Obsidian);
                }
            G.fx.Burst(at, 14, new Color(0.75f, 0.95f, 1f), 5f, 0.5f, 10f);
            G.sfx.PlayAt(at, "freeze", 0.5f, Random.Range(1f, 1.3f));
        }

        void Reflect(Vector2 prev, int cx, int cy)
        {
            int px = Mathf.FloorToInt(prev.x), py = Mathf.FloorToInt(prev.y);
            bool fx = px != cx && Wd.Solid(cx, py);
            bool fy = py != cy && Wd.Solid(px, cy);
            if (!fx && !fy) { fx = px != cx; fy = py != cy; }
            if (!fx && !fy) { vel = -vel; return; }
            if (fx) vel.x = -vel.x;
            if (fy) vel.y = -vel.y;
        }

        // Returns true if the shot is gone.
        bool HitEntities()
        {
            Vector2 h = new Vector2(0.15f, 0.15f) * Mathf.Max(0.8f, size);
            if (!petShot && CampProp.All.Count > 0 && CampProp.HitBox(pos, h, Mathf.Max(1f, dmg)))
            {
                if (radius > 0f) { Explode(pos); return true; }
                G.fx.Burst(pos, 3, col, 2f, 0.2f, 0f);
                Kill();
                return true;
            }
            // loose boulders (rolling, falling or settling) stop shots just like boulder blocks do
            if (!ghost && BoulderBody.All.Count > 0)
            {
                var bb = BoulderBody.At(pos, h);
                if (bb != null)
                {
                    if (team == Team.Player && owner != null && mine > 0)
                        bb.Damage(Mathf.Max(1, owner.s.digPower) * mine * (wallBreaker ? 3 : 1) * (highCal ? 2 : 1));
                    if (!bb.dead && knock >= 5f) bb.Push(vel.normalized * knock * 0.3f);
                    G.fx.Burst(pos, 3, TileDraw.DebrisColor(Block.Boulder, Biomes.At(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y))), 5f, 0.4f, 20f);
                    G.sfx.Material(Block.Boulder, false, team == Team.Player ? 0.8f : 0.5f);
                    if (radius > 0f) { OnImpact(pos, -vel.normalized, null); Explode(pos); return true; }
                    if (bounces > 0 && !flame)
                    {
                        bounces--;
                        Vector2 n = (pos - bb.pos).normalized;
                        vel = Vector2.Reflect(vel, n);
                        pos = bb.pos + n * (BoulderBody.H.x + 0.2f);
                        return false;
                    }
                    OnImpact(pos, -vel.normalized, null);
                    Kill();
                    return true;
                }
            }
            var pl = G.player;
            bool canHurtPlayer = team != Team.Player || (hurtsOwner && age > 0.15f);
            if (canHurtPlayer && pl.mode != Player.Mode.Dead && U.Overlap(pos, h, pl.pos, Player.Half * 0.85f))
            {
                if (pl.Parrying && team != Team.Player) { pl.ParryShot(this); return false; }   // PARRY: right back at you
                if (radius > 0f) { Explode(pos); return true; }
                if (zaps) { pl.Shock(playerDmg); G.fx.Burst(pos, 8, col, 4f, 0.3f, 5f); Kill(); return true; }
                if (!pl.Invulnerable)
                {
                    pl.Hurt(team == Team.Player ? 50 : playerDmg, pos);
                    if (team != Team.Player && main == Art.Bullets["b_fire"]) pl.SetOnFire();   // fireballs set you alight
                    if (poison) pl.Poison(3f);                                                  // jungle seeds poison you
                    G.fx.Burst(pos, 8, col, 4f, 0.3f, 5f);
                    Kill();
                    return true;
                }
            }

            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en) || hit.Contains(en) || en.Intangible) continue;
                if (spareT > 0f && en == spareE) continue;
                if (!Teams.Hostile(team, en.team)) continue;
                if (!U.Overlap(pos, h, en.pos, en.half)) continue;
                hit.Add(en);

                // mirror beetles bat shots straight back at whoever fired them
                if (en.Reflects)
                {
                    Deflect(en.team, U.Rot(-vel.normalized, Random.Range(-15f, 15f)));
                    pos += vel.normalized * 0.2f;
                    G.sfx.PlayAt(pos, "reflect", 0.6f, Random.Range(0.9f, 1.2f));   // only heard close by
                    G.fx.Sparks(pos, new Color(0.7f, 1f, 1f));
                    en.OnReflect();
                    return false;
                }
                if (radius > 0f) { OnImpact(pos, vel.normalized, en); Explode(pos); return true; }

                float d = dmg;
                if (owner != null && team == Team.Player) d *= owner.Backstab(en);   // AMBUSH
                bool weak = false;
                if (sharp && (pos - en.pos).magnitude < Mathf.Min(en.half.x, en.half.y) * 0.55f + 0.12f) { d *= 2.2f; weak = true; }
                if (en.Poisoned) d *= 1.25f;
                Vector2 dir = vel.normalized;
                if (en.TakeShot(d, dir * knock + Vector2.up * knock * 0.15f))
                {
                    if (owner != null) owner.OnWeaponHit(en, w, d, pos, dir, chainGen, hit);
                    else G.fx.Blood(pos, dir, en.BloodColor, 4);
                    if (petShot) Pets.Status(en, d);
                    else OnHitExtras(en, d, dir);
                    if (crit) G.Popup(en.pos + Vector2.up * 0.8f, "CRIT!", new Color(1f, 0.4f, 0.3f));
                    else if (weak) G.Popup(en.pos + Vector2.up * 0.8f, "WEAK SPOT!", Mods.Col(Mod.Sharpshooter));
                    if (knock >= 10f) G.HitStop(0.025f);
                }
                if (explosive) ModBlast(pos);
                if (pierce > 0) { pierce--; continue; }
                OnImpact(pos, vel.normalized, en);
                Kill();
                return true;
            }

            if (team == Team.Player && !petShot)
                foreach (var a in G.altars)
                {
                    if (a.dead || !a.Active || hit.Contains(a)) continue;
                    if (!U.Overlap(pos, h, a.pos, a.Half)) continue;
                    hit.Add(a);
                    a.OnHit(Src.Weapon);
                    if (pierce <= 0) { Kill(); return true; }
                }
            return false;
        }

        void Expire()
        {
            if (lob || radius > 0f) { Explode(pos); return; }
            if (!flame) G.fx.Burst(pos, 3, col, 2f, 0.2f, 0f);
            Kill();
        }

        void Explode(Vector2 at)
        {
            Kill();
            if (owner != null && team == Team.Player)
            {
                bool safe = owner.s.blastProof || (!selfHurt && !modExplosive);
                float r = radius > 0f ? radius : 1.15f;
                float e = radius > 0f ? dmg : dmg * 0.6f + 1f;
                G.Explode(at, r, e, safe, true, !noPrism && splitGen == 0);
                if (w != null && w.def.digPocket > 0f) G.DigPocket(at, w.def.digPocket, Mathf.Max(1, owner.s.digPower) * 4);
            }
            else G.Explode(at, Mathf.Max(radius, 1f), Mathf.Max(dmg, 3f), false, false);
        }

        void ModBlast(Vector2 at)
        {
            if (owner == null) return;
            G.Explode(at, (splitGen > 0 ? 0.5f : 1.4f) * blastScale, dmg * 0.9f + 2f, owner.s.blastProof || !modExplosive, true, !noPrism && splitGen == 0);   // only the EXPLOSIVE modifier can hurt you
        }

        public override void AddLight(Lighting l) => l.Add(pos, (flame ? 1.6f : mega ? 2.4f : 1.2f) * Mathf.Max(0.8f, size), team == Team.Player ? (mega ? 0.9f : 0.55f) : 0.75f);

        // THOUSAND BLADES: flying swords bat enemy shots back, like a melee swing
        void DeflectNear()
        {
            float r = 0.45f * Mathf.Max(1f, size);
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Bullet b) || b.team == Team.Player || (b.pos - pos).sqrMagnitude > r * r) continue;
                b.Deflect(Team.Player, vel.sqrMagnitude > 0.01f ? vel.normalized : Vector2.up);
                G.sfx.PlayAt(pos, "reflect", 0.5f, 1.2f, 0.1f);
                G.fx.Sparks(b.pos, new Color(0.8f, 0.95f, 1f));
            }
        }
    }

    // The visible swoosh of a melee swing. It also keeps batting bullets away while it lasts.
    public class Slash : Entity
    {
        float t;
        readonly float Life = 0.13f, scaleMul = 1f;
        readonly Player p;
        readonly Vector2 dir;
        readonly AttackDef d;
        readonly Color c;

        public override bool AlwaysActive => true;

        // tint/scaleMul: the bigger phantom blades melee weapons grow from shot items (laser, worm, ghost...)
        public Slash(Player p, Vector2 dir, AttackDef d, bool echo, int side, Color? tint = null, float scaleMul = 1f)
        {
            this.p = p; this.dir = dir; this.d = d; this.scaleMul = scaleMul;
            if (scaleMul > 1f) Life = 0.18f;
            pos = p.pos;
            MakeGO("Slash", Art.SlashArc, scaleMul > 1f ? 21 : 22);
            c = tint ?? (echo ? new Color(0.8f, 0.6f, 1f) : d.col);
            sr.color = c;
            sr.flipY = side < 0;
            go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            float sc = d.reach * p.s.meleeReach / 1.4f * scaleMul;
            go.transform.localScale = new Vector3(sc, sc * Mathf.Clamp(d.arc / 110f, 0.7f, 1.5f), 1f);
        }

        public override void Tick(float dt)
        {
            t += dt;
            if (t >= Life) { Kill(); return; }
            pos = p.pos + new Vector2(0f, 0.04f);
            float k = t / Life;
            var cc = c; cc.a = 1f - k * k;
            sr.color = cc;
            if (d.deflect && t > 0.02f && scaleMul <= 1f) p.DeflectAround(dir, d.reach * p.s.meleeReach + 0.45f, d.arc);
            go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y), 0);
        }

        public override void AddLight(Lighting l) => l.Add(pos + dir * d.reach * 0.6f, 1.6f, 0.5f * (1f - t / Life));
    }

    // Railgun streak.
    public class RailBeam : Entity
    {
        float t;
        readonly float life;
        readonly Vector2 a, b;
        readonly Color c;

        public override bool AlwaysActive => true;

        public RailBeam(Vector2 a, Vector2 b, Color c, float thick)
        {
            this.a = a; this.b = b; this.c = c;
            life = 0.35f;
            pos = (a + b) * 0.5f;
            MakeGO("Rail", Art.Square, 30);
            Vector2 d = b - a;
            go.transform.position = new Vector3(pos.x, pos.y, 0);
            go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            go.transform.localScale = new Vector3(d.magnitude, thick + 0.15f, 1f);
            this.thick = thick + 0.15f;
        }

        readonly float thick;

        public override void Tick(float dt)
        {
            t += dt;
            if (t >= life) { Kill(); return; }
            float k = t / life;
            go.transform.localScale = new Vector3((b - a).magnitude, thick * (1f - k), 1f);
            sr.color = Color.Lerp(Color.white, new Color(c.r, c.g, c.b, 0f), k);
        }

        public override void AddLight(Lighting l)
        {
            float k = 1f - t / life;
            Vector2 d = b - a;
            float len = d.magnitude;
            for (float s = 0f; s < len; s += 2.5f) l.Add(a + d / len * s, 2f * k, k);
        }
    }

    // A dead body: flung along the killing shot, tumbling, bleeding, coming to rest. Lingers for a while.
    public class Corpse : Entity
    {
        Vector2 vel;
        float spin, ang, life = 30f;
        bool resting;
        readonly Vector2 H;
        readonly Color blood;
        readonly float centerUp;   // sprite centre relative to its pivot, so bodies spin around their middle

        public override bool AlwaysActive => true;

        public Corpse(Enemy e, Vector2 kv)
        {
            pos = e.pos;
            var spr = e.CorpseSprite;
            centerUp = (spr.rect.height * 0.5f - spr.pivot.y) / Art.PPU;
            H = new Vector2(Mathf.Max(0.15f, e.half.x * 0.75f), Mathf.Max(0.12f, e.half.y * 0.6f));
            MakeGO("Corpse", e.CorpseSprite, 8);
            sr.color = new Color(0.62f, 0.55f, 0.55f);
            sr.flipX = e.facing < 0;
            vel = kv;
            spin = (Random.value < 0.5f ? -1f : 1f) * (250f + kv.magnitude * 45f);
            blood = e.BloodColor;
            G.RegisterCorpse(this);
        }

        public void Push(Vector2 v)
        {
            vel += v;
            resting = false;
            spin += Random.Range(-600f, 600f);
        }

        public override void Tick(float dt)
        {
            life -= dt;
            if (life <= 0f) { Kill(); return; }
            if (!resting)
            {
                vel.y = Fluids.Sink(pos, Mathf.Max(vel.y - 38f * dt, -22f), dt);
                float sp = vel.magnitude;
                Vector2 p = pos;
                if (Wd.MoveX(ref p, H, vel.x * dt, out var hx))
                {
                    if (sp > 8f) Splat(new Vector2(Mathf.Sign(vel.x), 0f), hx, sp);
                    vel.x *= -0.35f; spin *= 0.6f;
                }
                if (Wd.MoveY(ref p, H, vel.y * dt, out var hy))
                {
                    if (Mathf.Abs(vel.y) > 8f) Splat(new Vector2(0f, Mathf.Sign(vel.y)), hy, sp);
                    if (vel.y < 0f) { vel.y *= -0.3f; vel.x *= 0.7f; if (Mathf.Abs(vel.y) < 1.5f) vel.y = 0f; }
                    else vel.y = 0f;
                    spin *= 0.55f;
                }
                pos = p;
                ang += spin * dt;
                if (sp > 6f && Random.value < 0.6f) G.fx.Blood(pos, -vel.normalized, blood, 1);
                bool onFloor = Wd.BoxSolid(pos + new Vector2(0f, -0.05f), H);
                if (onFloor)
                {
                    vel.x = U.Approach(vel.x, 0f, 14f * dt);
                    spin = U.Approach(spin, 0f, 900f * dt);
                    if (Mathf.Abs(vel.x) < 0.4f && Mathf.Abs(vel.y) < 0.5f)
                    {
                        resting = true;
                        ang = Mathf.Round(ang / 90f) * 90f;
                    }
                }
            }
            else if (!Wd.BoxSolid(pos + new Vector2(0f, -0.05f), H)) resting = false;   // floor dug away

            if (Wd.FluidAtPoint(pos) == Fluids.Lava) { G.fx.Smoke(pos, 6, 1.2f); Kill(); return; }
            var c = sr.color; c.a = Mathf.Clamp01(life);
            sr.color = c;
            go.transform.rotation = Quaternion.Euler(0, 0, ang);
            Vector2 at = pos - U.Rot(new Vector2(0f, centerUp), ang);
            go.transform.position = new Vector3(U.Snap(at.x), U.Snap(at.y), 0);
        }

        void Splat(Vector2 dir, Vector2Int cell, float speed)
        {
            G.fx.Blood(pos, -dir, blood, 5);
            Wd.Stain(pos + dir * 0.3f, blood, 2);
            if (cell.x != int.MinValue) Wd.Impact(cell.x, cell.y, speed);
            G.sfx.Play("splat", 0.25f, Random.Range(0.8f, 1.2f));
        }
    }

    // SUPER BOMB: a moment of gathering light, then everything on screen dies.
    public class SuperBomb : Entity
    {
        float t = 0.5f;
        readonly Player p;

        public override bool AlwaysActive => true;

        public SuperBomb(Player p)
        {
            this.p = p;
            pos = p.pos;
            G.sfx.Play("charge", 1f, 0.5f, 0f);
            G.sfx.Play("rumble", 1f, 0.8f);
            G.hud.Banner("SUPER BOMB", "COVER YOUR EYES", new Color(1f, 0.95f, 0.7f));
        }

        public override void Tick(float dt)
        {
            t -= dt;
            pos = p.pos;
            for (int i = 0; i < 3; i++)
            {
                Vector2 o = Random.insideUnitCircle.normalized * Random.Range(2f, 5f);
                G.fx.Burst(pos + o, 1, new Color(1f, 0.95f, 0.7f), 2f, 0.25f, 0f, 1.5f, false);
            }
            G.cam.Shake(0.15f);
            if (t > 0f) return;
            Kill();
            Detonate();
        }

        void Detonate()
        {
            var g = G;
            Vector2 c = g.cam.pos;
            float hw = g.cam.halfW + 1f, hh = g.cam.halfH + 1f;
            g.hud.ScreenFlash(1f);
            g.HitStop(0.22f);
            g.cam.Shake(1f);
            g.cam.AddRumble(0.9f);
            g.sfx.Play("explode", 1f, 0.5f, 0f);
            g.sfx.Play("boom", 1f, 0.6f, 0f);
            g.lighting.Flash(c, 40f, 1f);

            foreach (var e in g.ents)
            {
                if (e.dead) continue;
                if (Mathf.Abs(e.pos.x - c.x) > hw || Mathf.Abs(e.pos.y - c.y) > hh) continue;
                if (e is Enemy en)
                {
                    Vector2 outv = (en.pos - pos).normalized;
                    if (en is Boss) en.TakeShot(en.maxHp * 0.15f + 20f, Vector2.zero);
                    else en.TakeShot(9999f, outv * 25f + Vector2.up * 8f);
                }
                else if (e is Bullet b && b.team != Team.Player) b.Kill();
                else if (e is Corpse co) co.Push((co.pos - pos).normalized * 20f + Vector2.up * 6f);
            }

            int r = 7;
            int cx = Mathf.FloorToInt(pos.x), cy = Mathf.FloorToInt(pos.y);
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r) continue;
                    g.world.Damage(x, y, 30, true, p.GoldMul);
                }
            for (int i = 0; i < 16; i++)
            {
                Vector2 q = c + new Vector2(Random.Range(-hw + 1f, hw - 1f), Random.Range(-hh + 1f, hh - 1f));
                g.Spawn(new FxDelay(Random.Range(0f, 0.5f), () => { G.fx.Explosion(q, Random.Range(1.2f, 2.6f)); G.sfx.Play("explode", 0.4f, Random.Range(0.8f, 1.3f)); G.cam.Shake(0.3f); }));
            }
            g.fx.Explosion(pos, 6f);
            g.fx.Smoke(pos, 30, 3f);
        }
    }

    // Runs a little bit of code after a delay (unaffected by distance from the player).
    public class FxDelay : Entity
    {
        float t;
        readonly System.Action a;
        public override bool AlwaysActive => true;
        public FxDelay(float delay, System.Action a) { t = delay; this.a = a; }
        public override void Tick(float dt)
        {
            t -= dt;
            if (t > 0f) return;
            Kill();
            a();
        }
    }
}
