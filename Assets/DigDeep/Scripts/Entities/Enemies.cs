using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    public abstract class Enemy : Entity
    {
        public float hp, maxHp;
        public int dmg = 1, gold = 1;
        public Vector2 half = new Vector2(0.38f, 0.36f);
        public Vector2 vel;
        public int facing = 1;
        public Team team = Team.Beast;
        protected Sprite[] frames, whites;
        protected float animT, flash, hurtCd, contactCd;
        public Color blood = new Color(0.5f, 0.9f, 0.4f);
        protected bool intangible;   // e.g. a burrowed boss: can't hurt or be hurt
        protected int depth;

        public bool Intangible => intangible;
        protected virtual bool Harmless => false;   // touching it doesn't hurt (little fish, a fish flopping on land)
        protected bool fireLike, waterLike;   // water kills fire creatures outright, lava kills water creatures (and everything not made of fire)
        float breath = 6f, chokeT;
        protected bool InWater => Wd.FluidAtPoint(pos) == Fluids.Water;
        protected float WaterSlow => InWater ? 0.5f : 1f;
        public Color BloodColor => blood;
        public Sprite CorpseSprite => frames[0];
        public virtual bool Reflects => false;
        public virtual bool Markable => true;   // can the HUNTER modifier mark it?
        public virtual void OnReflect() { }

        float poisonT, poisonDps;
        public bool Poisoned => poisonT > 0f;

        // FROST: frozen solid in place for a moment. Each foe (bosses too) can only be frozen once every 6 seconds.
        float frozenT, frostCd;
        public bool Frozen => frozenT > 0f;
        static readonly Color Ice = new Color(0.55f, 0.85f, 1f);
        Vector2 lastKnock;
        GameObject weakGo;

        // who this creature is currently trying to kill (you, or someone from the other side)
        protected Entity target;
        float retargetT;

        // ELITES: now and then a creature born with the world is a bigger, meaner, glowing one. It carries an item.
        public static bool GenSpawning;   // set while a chunk is being populated
        public bool elite;
        bool elitePending;
        float novaT = 3f;
        protected virtual bool CanBeElite => !(this is Boss) && !(this is WormSegment) && !Harmless;
        protected virtual void OnTouch(Player p) { }   // after a touch hurts you (bee stings, burning slugs...)
        protected virtual Sprite[] DeathAnim => null;   // played where it dies (a bat's death), instead of a corpse
        protected virtual bool LeavesCorpse => true;

        // a frame of one of the extra hand-drawn strips (a slime's squash, a skeleton's punch...), flashing white when hit
        protected Sprite StripFrame(string key, int i)
        {
            if (!Art.Strips.TryGetValue(key, out var s) || s == null || s.Length == 0) return null;
            i = Mathf.Clamp(i, 0, s.Length - 1);
            if (flash > 0f && Art.EnemyWhite.TryGetValue(key, out var w) && w != null && w.Length == s.Length) return w[i];
            return s[i];
        }

        // THE VOID WORLD: every creature is a darker, meaner version of itself
        bool voidDone;
        float voidT = 4f;
        public static readonly Color VoidTint = new Color(0.55f, 0.38f, 0.72f);
        public bool IsVoid => voidDone;
        void MakeVoid()
        {
            voidDone = true;
            hp *= 1.6f; maxHp *= 1.6f;
            dmg = dmg * 3 / 2;
            gold = Mathf.Max(gold + 1, gold * 3 / 2);
            if (this is Boss b) b.TintVoid();
        }
        // void creatures burst into a ring of void shards when they die; void bosses also strike out every few seconds
        protected virtual void VoidStrike() { }

        protected void Setup(string name, string key, int baseHp, int depth)
        {
            this.depth = depth;
            if (GenSpawning && depth > 20 && Random.value < 0.012f) elitePending = true;
            int dd = depth + VoidDeeper;   // in the void you've gone deeper still, not back up
            maxHp = hp = Mathf.Max(1f, Mathf.Round(baseHp * (1f + dd / Biomes.Stretch / 85f) * 2f) / 2f);   // tougher the deeper it lives
            dmg = ContactDmg(depth);
            frames = Art.Enemy[key];
            whites = Art.EnemyWhite[key];
            MakeGO(name, frames[0], 10);
        }

        // Bullets and explosions: every one of them lands, no matter how fast they come.
        public bool TakeShot(float d, Vector2 knock)
        {
            hurtCd = 0f;
            return Hurt(d, knock);
        }

        public virtual bool Hurt(float d, Vector2 knock)
        {
            if (dead || hurtCd > 0f || intangible) return false;
            hurtCd = 0.12f;
            hp -= d;
            if (TestMode.On && TestMode.Numbers && !(this is WormSegment)) TestMode.Number(pos + Vector2.up * half.y, d);
            flash = 0.08f;   // flat white for a frame or two
            vel += knock * 1.4f;   // hits shove enemies back hard (Super Crate Box)
            if (knock.sqrMagnitude > 0.01f) lastKnock = knock;
            G.sfx.Play("hit", 0.6f, Random.Range(0.85f, 1.15f), 0.1f);
            G.fx.Burst(pos, 6, Color.white, 6f, 0.2f, 10f);
            if (knock.sqrMagnitude > 4f) { G.cam.Shake(0.06f); G.cam.Kick(knock.normalized * 0.06f); }   // not for flames and other drizzle
            if (hp <= 0.001f) Die();
            return true;
        }

        public void Poison(float t, float dps)
        {
            bool fresh = poisonT <= 0f;
            poisonT = Mathf.Max(poisonT, t * G.player.s.dotDur);   // WITCH'S BREW: longer poison and burning
            // VENOM GLAND: every poisoned hit stacks more on (each stack is a share of that one attack's damage)
            if (G.player.s.poisonStack && !fresh) poisonDps = Mathf.Min(poisonDps + dps * 0.6f, dps * 8f);
            else poisonDps = Mathf.Max(fresh ? 0f : poisonDps, dps);
            if (fresh) poisonTick = 0f;   // it bites straight away
        }

        float poisonTick;
        // poison hurts in little bites (each one flashes), starting the moment it lands
        void PoisonTick(float dt)
        {
            poisonT -= dt;
            poisonTick -= dt;
            if (poisonTick > 0f) return;
            poisonTick = 0.4f;
            float d = poisonDps * 0.4f * G.player.s.dotMul * (this is Boss ? 1f : 1.6f);
            hp -= d;
            flash = Mathf.Max(flash, 0.04f);
            if (Random.value < 0.6f) G.fx.Burst(pos, 3, new Color(0.5f, 1f, 0.3f), 1.5f, 0.4f, 5f);
        }

        public void Freeze(float t)
        {
            if (dead || frostCd > 0f || intangible) return;
            frozenT = t;
            frostCd = 6f;
            vel = Vector2.zero;
            G.sfx.Play("freeze", 0.6f, Random.Range(0.9f, 1.1f));
            G.fx.Ring(pos, 1.2f, Ice);
            G.fx.Burst(pos, 12, new Color(0.85f, 0.95f, 1f), 4f, 0.5f, 6f);
        }

        // ---------------------------------------------------------------- KICK: sent flying, bowling into others

        float kickT, kickDmg, kickSpin, kickSettle;
        readonly HashSet<Enemy> kickHits = new HashSet<Enemy>();
        public bool Kicked => kickT > 0f;

        // Sends this enemy flying. While it flies it hurts and launches every other enemy it hits,
        // smashes boulders loose, and slams into walls.
        public void Launch(Vector2 v, float chainDmg)
        {
            if (dead) return;
            if (this is Boss || this is WormSegment || this is Worm) { vel += v * 0.15f; return; }   // too big to boot
            kickT = 5f;          // a loose physics body until it has lost its momentum (or 5 s at most)
            kickSettle = 0f;
            kickDmg = chainDmg;
            kickSpin = 0f;
            vel = v;
            kickHits.Clear();
            kickHits.Add(this);
        }

        public void KickedTick(float dt)
        {
            kickT -= dt; animT += dt; flash -= dt; hurtCd -= dt; contactCd -= dt;
            byte kfl = Wd.FluidAtPoint(pos);
            bool inWater = kfl == Fluids.Water || kfl == Fluids.Lava;
            if (inWater)
            {
                // water soaks up the throw fast
                vel *= Mathf.Max(0f, 1f - 3.5f * dt);
                vel.y = Mathf.Max(vel.y - 9f * dt, -5f);
            }
            else vel.y = Mathf.Max(vel.y - 30f * dt, -18f);
            Vector2 p = pos;
            if (Wd.MoveX(ref p, half, vel.x * dt, out var hx))
            {
                if (Mathf.Abs(vel.x) > 8f)
                {
                    // slammed into a wall
                    TakeShot(kickDmg * 0.5f, Vector2.zero);
                    G.cam.Shake(0.15f);
                    G.sfx.Play("land", 0.7f, 0.7f);
                    if (hx.x != int.MinValue) Wd.PushBoulder(hx.x, hx.y, new Vector2(Mathf.Sign(vel.x) * Mathf.Abs(vel.x) * 0.7f, 2f));
                    if (dead) return;
                }
                vel.x *= -0.3f;
            }
            float vyWas = vel.y;
            bool landed = Wd.MoveY(ref p, half, vel.y * dt, out _);
            if (landed) { vel.y = vyWas < -6f ? -vyWas * 0.25f : 0f; vel.x *= 0.8f; }   // a little bounce off hard landings
            pos = p;
            bool onGround = Wd.Ground(pos + new Vector2(0f, -0.05f), half);
            if (onGround && vel.y <= 0.01f) vel.x = Mathf.MoveTowards(vel.x, 0f, 16f * dt);   // skids to a stop

            if (vel.magnitude > 5f)
            {
                foreach (var e in G.ents)
                {
                    if (e.dead || !(e is Enemy o) || kickHits.Contains(o) || o.Intangible || !U.Overlap(pos, half, o.pos, o.half)) continue;
                    kickHits.Add(o);
                    o.TakeShot(kickDmg, vel.normalized * 4f);
                    if (!o.dead) { o.Launch(vel * 0.85f + Vector2.up * 2f, kickDmg * 0.85f); o.kickHits.UnionWith(kickHits); }
                    vel *= 0.6f;
                    G.HitStop(0.04f);
                    G.cam.Shake(0.2f);
                    G.sfx.Play("hit", 0.8f, 0.8f);
                    G.fx.Burst((pos + o.pos) * 0.5f, 10, Color.white, 6f, 0.25f, 0f);
                }
                for (int i = BoulderBody.All.Count - 1; i >= 0; i--)
                {
                    var bb = BoulderBody.All[i];
                    if (U.Overlap(pos, half, bb.pos, BoulderBody.H)) { bb.Push(vel * 0.6f); vel *= 0.5f; }
                }
                if (Random.value < 0.5f) G.fx.Ghost(sr.sprite, go.transform.position, sr.flipX, new Color(1f, 1f, 1f, 0.35f));
            }

            float spd = vel.magnitude;
            if (spd > 3f) kickSpin += -Mathf.Sign(vel.x) * spd * 60f * dt;
            else kickSpin = Mathf.MoveTowardsAngle(kickSpin, 0f, 540f * dt);   // rights itself as it slows
            Draw(lastYOff);
            if (go != null) go.transform.rotation = Quaternion.Euler(0f, 0f, kickSpin);
            // settled: resting (on ground, or floating in water) with its momentum gone for half a second
            if ((onGround || landed || inWater) && spd < 2.5f) kickSettle += dt; else kickSettle = 0f;
            if (kickT <= 0f || kickSettle >= 0.5f)
            {
                kickT = 0f;
                kickSettle = 0f;
                if (go != null) go.transform.rotation = Quaternion.identity;
                OnLaunchEnd();
            }
        }

        // back on its own feet wherever it ended up (grid walkers re-sync to the cell they landed in)
        protected virtual void OnLaunchEnd() { }

        // ---------------------------------------------------------------- electricity

        float shockT, shockCd;
        public bool Shocked => shockT > 0f;
        public virtual bool ElectricProof => false;   // eels, tesla coils and machines that make the charge
        public virtual bool Mechanical => false;      // machines in water charge it up
        public virtual bool Volatile => false;        // blows up when shocked (drones)

        // A jolt: damage, then a short stun where it twitches on the spot. Bosses only take the damage.
        public void Shock(float t, float dmgAmt)
        {
            if (dead || intangible || ElectricProof || shockCd > 0f) return;
            shockCd = 0.7f;
            TakeShot(dmgAmt, Vector2.zero);
            G.fx.Burst(pos, 10, new Color(0.85f, 0.97f, 1f), 5f, 0.2f, 0f);
            G.sfx.Play("shock", 0.45f, Random.Range(1.1f, 1.4f), 0.1f);
            if (dead) return;
            if (Volatile) { Die(); G.Explode(pos, 1.8f, 6f, false, true, false); return; }
            if (!(this is Boss)) { shockT = Mathf.Max(shockT, t); vel = Vector2.zero; }
        }

        // Called by the game instead of Tick while shocked: it flickers, jitters and crackles.
        public void ShockedTick(float dt)
        {
            shockT -= dt; shockCd -= dt; flash -= dt; hurtCd -= dt; frostCd -= dt;
            vel = Vector2.zero;
            if (poisonT > 0f)
            {
                PoisonTick(dt);
                if (hp <= 0f) { Die(); return; }
            }
            if (sr != null)
            {
                bool on = ((int)(Time.time * 30f)) % 2 == 0;
                int fi = ((int)(animT * 6f)) % frames.Length;
                sr.sprite = on ? whites[fi] : frames[fi];
                sr.color = on ? new Color(0.75f, 0.95f, 1f) : Color.white;
                Place(lastYOff);
                go.transform.position += new Vector3(Random.Range(-1, 2) / 16f, Random.Range(-1, 2) / 16f, 0f);
            }
            if (Random.value < 0.4f) G.fx.Burst(pos + new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y)), 1, new Color(0.9f, 0.98f, 1f), 3f, 0.12f, 0f, 1f, false);
            if (shockT <= 0f && sr != null) sr.color = Color.white;
        }

        // Called by the game instead of Tick while frozen: no thinking, no moving, just ice.
        public void FrozenTick(float dt)
        {
            frozenT -= dt; frostCd -= dt; flash -= dt; hurtCd -= dt;
            vel = Vector2.zero;
            if (poisonT > 0f)
            {
                PoisonTick(dt);
                if (hp <= 0f) { Die(); return; }
            }
            if (sr != null)
            {
                int fi = ((int)(animT * 6f)) % frames.Length;
                sr.sprite = flash > 0f ? whites[fi] : frames[fi];
                sr.color = flash > 0f ? Color.white : Color.Lerp(Ice, Color.white, 0.25f + 0.15f * Mathf.Sin(Time.time * 6f));
            }
            if (Random.value < 0.12f) G.fx.Burst(pos + new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y)), 1, new Color(0.85f, 0.97f, 1f), 0.6f, 0.6f, -1f, 1f, false);
            if (frozenT <= 0f)
            {
                G.sfx.Play("chip", 0.5f, 1.8f);
                G.fx.Burst(pos, 14, new Color(0.75f, 0.92f, 1f), 6f, 0.4f, 25f);
                if (sr != null) sr.color = Color.white;
            }
        }

        public virtual void Die()
        {
            Kill();
            Vector2 kd = lastKnock.sqrMagnitude > 0.01f ? lastKnock : Vector2.up * 3f;
            Vector2 dir = kd.normalized;
            // the body flies off along the killing shot
            if (!(this is Boss))
            {
                if (DeathAnim != null) G.Spawn(new FrameAnim(pos, DeathAnim, 12f, facing < 0, 12, dir * 2f, 6f));
                else if (LeavesCorpse) G.Spawn(new Corpse(this, dir * Mathf.Clamp(kd.magnitude * 1.6f + 7f, 8f, 26f) + Vector2.up * 4f));
            }
            if (voidDone && !(this is Boss) && !(this is WormSegment))
            {
                // the void bloom: shards of black fly out of it
                for (int i = 0; i < 6; i++)
                {
                    float a = (i * 60f + Random.value * 20f) * Mathf.Deg2Rad;
                    G.Spawn(new Bullet(pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3.5f, Team.Beast, EnemyShotDamage, Mathf.Max(1, dmg / 3), "b_enemy", new Color(0.6f, 0.3f, 1f), 0.8f, 0.9f));
                }
            }
            G.fx.Blood(pos, dir, blood, 26);
            G.fx.Blood(pos, Vector2.up, blood, 10);
            G.fx.Gibs(pos, dir, blood, 6);
            G.fx.Burst(pos, 8, Color.white, 4f, 0.3f, 0f);
            G.sfx.Play("kill", 0.7f);
            G.sfx.Play("splat", 0.5f, Random.Range(0.8f, 1.1f));
            G.fx.Ring(pos, 0.9f, Color.white);
            G.fx.Burst(pos, 10, new Color(1f, 0.95f, 0.7f), 8f, 0.25f, 0f);
            G.HitStop(0.055f);
            G.cam.Shake(0.28f);
            G.cam.Punch(0.18f);
            G.cam.Kick(dir * 0.18f);
            var ps = G.player.s;
            G.SpawnGold(pos, gold * ps.killGoldMul + ps.killGoldBonus);
            if (elite)
            {
                // an elite carries something found at this depth
                var d = DB.RollTrinket(Mathf.Max(0, depth), G.player, G.rng) ?? DB.RollRare(Mathf.Max(0, depth), G.player, G.rng);
                if (d != null) G.Spawn(new LootPickup(pos, d, 0.6f, true));
                G.sfx.Play("chest", 0.8f, 1.1f);
                G.fx.Ring(pos, 2f, EliteColor);
            }
            G.OnEnemyKilled(this);
        }

        // ---------------------------------------------------------------- elites

        public void MakeEliteSoon() => elitePending = true;   // (test mode)

        void MakeElite()
        {
            elite = true;
            hp *= 6f; maxHp *= 6f;
            dmg = dmg * 3 / 2;
            gold = Mathf.Max(10, gold * 5);
            novaT = 2.5f + Random.value * 2f;
        }

        // the element of an elite comes from where it lives
        int EliteElem => Biomes.Index(Mathf.Max(0, depth));
        public Color EliteColor
        {
            get
            {
                switch (EliteElem)
                {
                    case Biomes.Jungle: case Biomes.Fungal: return new Color(0.55f, 1f, 0.35f);   // poison
                    case Biomes.Crystal: return new Color(0.6f, 0.9f, 1f);                       // frost
                    case Biomes.Storm: return new Color(1f, 0.95f, 0.45f);                       // lightning
                    case Biomes.Magma: return new Color(1f, 0.5f, 0.2f);                         // fire
                    case Biomes.Kingdom: return new Color(1f, 0.85f, 0.4f);                      // gold
                    default: return new Color(1f, 0.75f, 0.35f);                                 // stone
                }
            }
        }

        // now and then an elite lets loose a ring of its element
        void EliteTick(float dt)
        {
            if (Random.value < 0.5f) G.fx.Burst(pos + Random.insideUnitCircle * (half.x + 0.3f), 1, EliteColor, 1.4f, 0.6f, -2f, 1f, false);   // its aura
            if (!HasTarget || (TPos - pos).sqrMagnitude > 64f || intangible) return;
            novaT -= dt;
            if (novaT > 0f) return;
            novaT = 4f + Random.value * 2f;
            int n = 8;
            float off = Random.value * 45f;
            int e = EliteElem;
            string spr = e == Biomes.Magma ? "b_fire" : e == Biomes.Storm ? "b_zap" : (e == Biomes.Jungle || e == Biomes.Fungal) ? "b_spit" : "b_enemy";
            for (int i = 0; i < n; i++)
            {
                float a = (off + i * 360f / n) * Mathf.Deg2Rad;
                var b = new Bullet(pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5f, Team.Beast, EnemyShotDamage, dmg * 2 / 3, spr, EliteColor);
                if (e == Biomes.Jungle || e == Biomes.Fungal) b.poison = true;
                if (e == Biomes.Storm) b.zaps = true;
                if (e == Biomes.Crystal) b.chills = true;
                G.Spawn(b);
            }
            G.sfx.Play("shoot", 0.6f, 0.6f);
            G.fx.Ring(pos, 1.5f, EliteColor);
        }

        public override void Kill()
        {
            base.Kill();
            if (weakGo != null) Object.Destroy(weakGo);
        }

        // ---------------------------------------------------------------- targeting

        protected bool HasTarget => target != null && !target.dead && !(target is Player pl && (pl.mode == Player.Mode.Dead || pl.Camouflaged));
        protected Vector2 TPos => target != null ? target.pos : G.player.pos;

        protected void FindTarget(float range, bool needSight, float dt)
        {
            retargetT -= dt;
            if (retargetT > 0f && HasTarget && (TPos - pos).sqrMagnitude < range * range * 1.5f) return;
            retargetT = 0.35f + Random.value * 0.2f;
            target = null;
            float best = range * range;
            var pl = G.player;
            // SHADOW STEP's decoy draws everything near it
            var dc = Decoy.Active;
            if (dc != null && !dc.dead && (dc.pos - pos).sqrMagnitude < range * range * 1.5f) { target = dc; return; }
            if (pl.mode != Player.Mode.Dead && !pl.Camouflaged)   // CAMOUFLAGE: they can't see you
            {
                float d2 = (pl.pos - pos).sqrMagnitude * 0.85f;   // you are always a little more interesting
                if (pl.Cursed(Curse.Hunted)) d2 *= 0.25f;          // CURSE OF THE HUNTED: they spot you from twice as far
                if (d2 < best && (!needSight || Wd.LineClear(pos, pl.pos))) { best = d2; target = pl; }
            }
            foreach (var e in G.ents)
            {
                if (e.dead || e == this || !(e is Enemy o) || o.intangible || !Teams.Hostile(team, o.team)) continue;
                float d2 = (o.pos - pos).sqrMagnitude;
                if (d2 < best && (!needSight || Wd.LineClear(pos, o.pos))) { best = d2; target = o; }
            }
        }

        // A walker blocked by a wall hops up it if it's only one block high. True if it hopped.
        protected bool TryHop(Vector2 p)
        {
            if (!Wd.BoxSolid(p + new Vector2(0f, -0.06f), half) || vel.y > 0.5f) return false;
            int fx = Mathf.FloorToInt(p.x + facing * (half.x + 0.2f));
            int fy = Mathf.FloorToInt(p.y - half.y + 0.05f);
            if (!Wd.Solid(fx, fy) || Wd.Solid(fx, fy + 1) || Wd.Solid(fx, fy + 2) || Wd.Solid(Mathf.FloorToInt(p.x), fy + 2)) return false;
            vel.y = 10f;
            return true;
        }

        protected float EnemyShotDamage => 2f * (1f + (depth + VoidDeeper) / Biomes.Stretch / 110f);

        // What a touch (or a normal enemy bullet) does to you, in health points. A heart is 100.
        public static int ContactDmg(int depth) => Mathf.RoundToInt(60f + Mathf.Max(0, depth + VoidDeeper) / Biomes.Stretch * 0.25f);
        // the void lies under the whole world: there, depth carries on from the bottom
        public static int VoidDeeper => World.Void ? ArenaRoom.Final : 0;

        // ---------------------------------------------------------------- tick helpers

        // ---------------------------------------------------------------- fire

        protected float burnT;
        float burnTick;
        public bool Burning => burnT > 0f;
        public virtual bool Plant => false;   // plants go up like dry hay

        // Set alight for t seconds (fire bullets, flames, sparks, burning neighbours).
        public void Ignite(float t)
        {
            if (dead || fireLike || intangible || this is WormSegment) return;
            if (Wd.FluidAtPoint(pos) == Fluids.Water) return;
            if (burnT <= 0f) { burnTick = 0.2f; G.sfx.Play("ignite", 0.3f, 1.3f); }
            burnT = Mathf.Max(burnT, t * G.player.s.dotDur);
        }

        void TickBurning(float dt)
        {
            if (burnT <= 0f) return;
            if (Wd.FluidAtPoint(pos) == Fluids.Water)
            {
                burnT = 0f;
                G.sfx.Play("fizz", 0.5f);
                G.fx.Smoke(pos, 5, 0.9f);
                return;
            }
            burnT -= dt;
            burnTick -= dt;
            if (burnTick <= 0f)
            {
                burnTick = 0.33f / Mathf.Max(0.2f, G.player.s.burnRate);   // BELLOWS: burns tick faster
                float dps = (1.5f + Mathf.Max(0, depth) * 0.012f) * (Plant ? 3f : 1f) * G.player.s.dotMul * (this is Boss ? 1f : 1.6f);   // burns hurt ordinary foes more
                TakeShot(dps * 0.33f, Vector2.zero);
                if (dead) return;
            }
            Fire.IgniteBox(pos, half + new Vector2(0.05f, 0.05f));
            Fire.MeltAround(pos, 1f);
            if (Random.value < 0.7f) G.fx.Burst(pos + new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y)), 1, new Color(1f, Random.Range(0.4f, 0.9f), 0.15f), 1.5f, 0.4f, -6f, 1f, false);
        }

        protected void BaseTick(float dt)
        {
            animT += dt; flash -= dt; hurtCd -= dt; contactCd -= dt; frostCd -= dt; shockCd -= dt;
            if (elitePending) { elitePending = false; if (CanBeElite) MakeElite(); }
            if (!voidDone && World.Void && !(this is SkyGod)) MakeVoid();
            if (voidDone && this is Boss && !intangible && HasPlayerNear(14f))
            {
                voidT -= dt;
                if (voidT <= 0f) { voidT = 5.5f; VoidStrike(); }
            }
            TickBurning(dt);
            if (dead) return;
            if (elite) EliteTick(dt);
            if (poisonT > 0f)
            {
                PoisonTick(dt);
                if (Random.value < 0.15f) G.fx.Burst(pos, 1, new Color(0.5f, 1f, 0.3f), 1f, 0.5f, 5f);
                if (hp <= 0f) { Die(); return; }
            }
            var p = G.player;
            if (p.mode != Player.Mode.Dead && !intangible && !Harmless && contactCd <= 0f && U.Overlap(pos, half * 0.85f, p.pos, Player.Half))
            {
                p.Hurt(dmg, pos);
                contactCd = 0.5f;
                OnTouch(p);
            }
            // beasts maul raiders that get too close
            if (team == Team.Beast && !intangible && !Harmless && contactCd <= 0f)
                foreach (var e in G.ents)
                {
                    if (e.dead || !(e is Enemy o) || o.team != Team.Bandit) continue;
                    if (!U.Overlap(pos, half * 0.85f, o.pos, o.half)) continue;
                    o.TakeShot(EnemyShotDamage, (o.pos - pos).normalized * 6f + Vector2.up * 3f);
                    contactCd = 0.6f;
                    break;
                }
            // liquids
            byte body = Wd.FluidAtPoint(pos);
            byte head = Wd.FluidAtPoint(pos + Vector2.up * half.y * 0.6f);
            if (fireLike && (body == Fluids.Water || head == Fluids.Water))
            {
                G.fx.Smoke(pos, 10, 1.3f);
                G.sfx.Play("fuse", 0.5f, 1.6f);
                Die();
                return;
            }
            if (!fireLike && body == Fluids.Lava && !(this is Boss)) { G.fx.Smoke(pos, 6, 1.2f); Die(); return; }
            if (body == Fluids.Water && !(this is Boss)) vel.y = Mathf.Max(vel.y, -4f);   // sinks slowly
            // drowning / choking on steam
            bool choking = !waterLike && !(this is Boss) && (head == Fluids.Water || head == Fluids.Steam);
            if (choking)
            {
                breath -= dt;
                if (Random.value < 0.04f) G.fx.Burst(pos + Vector2.up * half.y, 1, new Color(0.8f, 0.95f, 1f), 1f, 0.6f, -4f, 1f, false);
                if (breath <= 0f)
                {
                    chokeT -= dt;
                    if (chokeT <= 0f) { chokeT = 0.5f; TakeShot(maxHp * 0.12f, Vector2.zero); }
                }
            }
            else breath = Mathf.Min(6f, breath + dt * 2f);
        }

        float lastYOff;

        protected void Draw(float yOff)
        {
            if (dead) return;
            lastYOff = yOff;
            int fi = ((int)(animT * 6f)) % frames.Length;
            sr.sprite = flash > 0f ? whites[fi] : frames[fi];
            sr.flipX = facing < 0;
            if (!(this is Boss))
            {
                Color c = Color.white;
                if (poisonT > 0f) c = new Color(0.65f, 1f, 0.55f);
                if (burnT > 0f) c = Color.Lerp(new Color(1f, 0.7f, 0.4f), new Color(1f, 0.4f, 0.15f), 0.5f + 0.5f * Mathf.Sin(Time.time * 30f));
                if (G.player.huntMark == this) c = Color.Lerp(c, new Color(1f, 0.9f, 0.2f), 0.5f + 0.5f * Mathf.Sin(Time.time * 10f));
                if (G.InReveal(pos)) c = Color.Lerp(c, new Color(1f, 0.25f, 0.25f), (0.45f + 0.15f * Mathf.Sin(Time.time * 6f)) * G.RevealFade);
                if (voidDone) c = Color.Lerp(c, VoidTint, 0.55f) * new Color(0.75f, 0.7f, 0.85f, 1f);
                if (elite) c = Color.Lerp(c, EliteColor, 0.35f + 0.15f * Mathf.Sin(Time.time * 5f));
                sr.color = c;
            }
            WeakSpot();
            Place(yOff);
            // a hit makes it pop bigger for a frame or two
            if (!(this is Boss)) { float k = (1f + Mathf.Max(0f, flash) * 3f) * (elite ? 1.4f : 1f); go.transform.localScale = new Vector3(k, k, 1f); }
        }

        // SHARPSHOOTER: a red dot on the weak spot of everything
        void WeakSpot()
        {
            var w = G.player.weapon;
            bool on = w != null && w.Has(Mod.Sharpshooter) && !intangible;
            if (!on) { if (weakGo != null && weakGo.activeSelf) weakGo.SetActive(false); return; }
            if (weakGo == null)
            {
                weakGo = new GameObject("WeakSpot");
                weakGo.transform.SetParent(G.runRoot, false);
                var r = Art.AddSR(weakGo, Art.Circle, 14);
                r.color = new Color(1f, 0.25f, 0.4f, 0.85f);
            }
            if (!weakGo.activeSelf) weakGo.SetActive(true);
            float s = 0.2f + 0.04f * Mathf.Sin(Time.time * 8f);
            weakGo.transform.localScale = new Vector3(s, s, 1f);
            weakGo.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y), 0);
        }

        bool HasPlayerNear(float r) => G.player != null && G.player.mode != Player.Mode.Dead && (G.player.pos - pos).sqrMagnitude < r * r;

        public override void AddLight(Lighting l)
        {
            if (elite) { l.Add(pos, 3f, 0.8f); return; }
            if (burnT > 0f) { l.Add(pos, 3.2f, 0.85f); return; }   // burning things light up the cave
            if (G.player.huntMark == this) l.Add(pos, 2.6f, 0.85f);
            // eyes glow faintly so you can spot things in the dark (much more with the OWL SIGHT combo)
            else if (G.player.Combo("owl")) l.Add(pos, 2.4f, 0.7f);
            else l.Add(pos, 1.1f, 0.25f);
        }
    }

    // Walks back and forth, turns at walls and ledges, charges at whatever it hates.
    public class Crawler : Enemy
    {
        readonly float speed;

        public Crawler(int x, int y, int depth, bool golem, bool slime = false)
        {
            half = golem ? new Vector2(0.42f, 0.4f) : new Vector2(0.38f, 0.28f);
            pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
            Setup(golem ? "Golem" : slime ? "Slime" : "Crawler", golem ? "golem" : slime ? "slime" : "crawler", golem ? 6 : 2, depth);
            if (slime) { waterLike = true; blood = new Color(0.4f, 0.75f, 1f); }
            speed = (golem ? 1.2f : 1.9f) * (1f + depth / 500f);
            if (golem) { dmg = dmg * 13 / 10; gold = 5; blood = new Color(0.6f, 0.55f, 0.5f); } else gold = 2;
            facing = Random.value < 0.5f ? -1 : 1;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(7f, false, dt);
            float sp = speed * (waterLike ? 1f : WaterSlow);
            if (HasTarget && Mathf.Abs(TPos.y - pos.y) < 0.9f && Mathf.Abs(TPos.x - pos.x) < 5.5f)
            {
                facing = TPos.x > pos.x ? 1 : -1;
                sp *= 1.5f;
            }
            vel.x = U.Approach(vel.x, facing * sp, 20f * dt);
            vel.y = Mathf.Max(vel.y - 40f * dt, -16f);
            Vector2 p = pos;
            bool chasing = HasTarget && Mathf.Abs(TPos.x - pos.x) < 6f && TPos.y > pos.y - 1.5f;
            if (Wd.MoveX(ref p, half, vel.x * dt, out _))
            {
                // a chasing crawler hops up a one-block step instead of turning around
                if (chasing && (TryHop(p) || vel.y > 0.5f)) { }   // mid-hop: keep pushing towards it
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
            Draw(-half.y);
        }
    }

    // Hangs from ceilings until something comes close, then swoops. Imps hover at range and throw fire.
    public class Bat : Enemy
    {
        bool awake, navOk;
        float t, shootT, navT;
        Vector2 navPt;
        readonly bool imp;

        protected override Sprite[] DeathAnim => imp ? null : Art.BatDeath;

        public Bat(int x, int y, int depth, bool imp)
        {
            this.imp = imp;
            fireLike = imp;
            pos = new Vector2(x + 0.5f, y + 0.6f);
            half = new Vector2(0.32f, 0.26f);
            Setup(imp ? "Imp" : "Bat", imp ? "imp" : "bat", imp ? 3 : 1, depth);
            gold = imp ? 4 : 1;
            blood = imp ? new Color(1f, 0.6f, 0.2f) : new Color(0.7f, 0.4f, 0.9f);
            t = Random.value * 6f;
            shootT = 2f;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(awake ? 12f : 7f, false, dt);
            Vector2 tp = HasTarget ? TPos : pos;
            Vector2 d = tp - pos;
            float dist = d.magnitude;
            if (!awake)
            {
                if ((HasTarget && dist < 7f) || flash > 0f) { awake = true; G.sfx.Play("bat", 0.4f); }
                else { animT = 0f; Draw(0f); return; }
            }
            t += dt;
            Vector2 goal = HasTarget ? tp : pos + new Vector2(Mathf.Sin(t) * 2f, 0f);
            if (imp && HasTarget)
            {
                goal = tp + new Vector2(d.x > 0 ? -4f : 4f, 2.5f);
                shootT -= dt;
                if (shootT <= 0f && dist < 10f)
                {
                    shootT = 2.4f;
                    G.Spawn(new Bullet(pos, d.normalized * 7f, Team.Beast, EnemyShotDamage, dmg, "b_fire", new Color(1f, 0.6f, 0.2f)));
                    G.sfx.Play("shoot", 0.5f, 0.7f);
                }
            }
            // fly around rock instead of into it: a path search a few times a second
            if (HasTarget)
            {
                if (Wd.Solid(Mathf.FloorToInt(goal.x), Mathf.FloorToInt(goal.y))) goal = tp;   // an imp's perch inside rock
                navT -= dt;
                if (navT <= 0f) { navT = 0.22f + Random.value * 0.12f; navOk = Nav.Steer(pos, goal, out navPt); }
                if (navOk) goal = navPt;
                else goal = pos + new Vector2(Mathf.Sin(t * 0.7f) * 2f, Mathf.Cos(t * 0.9f));   // no way through: circle about
            }
            Vector2 want = goal - pos;
            if (want.sqrMagnitude > 0.01f) want = want.normalized * (imp ? 3.2f : 3.7f);
            bool tight = HasTarget && navOk && !Wd.LineClear(pos, tp);
            want.y += Mathf.Sin(t * 5f) * (tight ? 0.4f : 1.5f);   // less wobble while squeezing through tunnels
            if (contactCd > 0f) want = -want * 0.6f;
            // keep a little personal space so a pack doesn't collapse into one stacked bat
            foreach (var e in G.ents)
            {
                if (e == this || e.dead || !(e is Bat o)) continue;
                Vector2 away = pos - o.pos;
                float dd = away.sqrMagnitude;
                if (dd < 0.81f && dd > 0.0001f) want += away.normalized * (0.9f - Mathf.Sqrt(dd)) * 9f;
                else if (dd <= 0.0001f) want += Random.insideUnitCircle * 3f;
            }
            vel = Vector2.MoveTowards(vel, want, (tight ? 20f : 12f) * dt);
            Vector2 p = pos;
            if (Wd.MoveX(ref p, half, vel.x * dt, out _)) { vel.x *= -0.5f; navT = 0f; }
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) { vel.y *= -0.5f; navT = 0f; }
            pos = p;
            if (Mathf.Abs(vel.x) > 0.1f) facing = vel.x > 0 ? 1 : -1;
            Draw(0f);
        }

        public override void AddLight(Lighting l)
        {
            base.AddLight(l);
            if (imp) l.Add(pos, 2.2f, 0.6f);
        }
    }

    // Dig Dug style: moves cell to cell and tunnels through soft dirt towards its prey.
    public class Mole : Enemy
    {
        Vector2Int cell, from, to;
        float mt;
        bool moving;
        readonly float stepTime;
        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        public Mole(int x, int y, int depth)
        {
            cell = new Vector2Int(x, y);
            pos = new Vector2(x + 0.5f, y + 0.5f);
            half = new Vector2(0.38f, 0.38f);
            Setup("Mole", "mole", 3, depth);
            stepTime = Mathf.Max(0.22f, 0.5f - depth / 800f);
            gold = 3;
            blood = new Color(1f, 0.5f, 0.3f);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            vel = Vector2.zero;
            if (!moving)
            {
                FindTarget(14f, false, dt);
                if (!HasTarget) { Draw(0f); return; }
                Vector2 tp = TPos;
                Vector2Int best = Vector2Int.zero;
                float bestScore = float.MaxValue;
                foreach (var dir in Dirs)
                {
                    var n = cell + dir;
                    Block b = Wd.Get(n.x, n.y);
                    var def = Tiles.Def(b);
                    bool ok = b == Block.Air || (def.soft && def.solid && !def.gravity);
                    if (!ok) continue;
                    float score = (new Vector2(n.x + 0.5f, n.y + 0.5f) - tp).sqrMagnitude + Random.value * 3f + (b != Block.Air ? 1.5f : 0f);
                    if (score < bestScore) { bestScore = score; best = dir; }
                }
                if (best == Vector2Int.zero) { Draw(0f); return; }
                from = cell; to = cell + best;
                if (Wd.Get(to.x, to.y) != Block.Air) Wd.Break(to.x, to.y, false, 1f, false);
                moving = true; mt = 0f;
                if (best.x != 0) facing = best.x;
            }
            mt += dt / stepTime;
            pos = Vector2.Lerp(new Vector2(from.x + 0.5f, from.y + 0.5f), new Vector2(to.x + 0.5f, to.y + 0.5f), Mathf.Min(mt, 1f));
            if (mt >= 1f) { moving = false; cell = to; }
            Draw(0f);
        }

        protected override void OnLaunchEnd()
        {
            // continue digging from wherever the kick dropped it (not from where it was kicked)
            cell = new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
            from = to = cell;
            moving = false;
            mt = 0f;
            pos = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
        }
    }

    // Stationary mushroom turret that spits at anything it hates.
    public class Spitter : Enemy
    {
        float shootT;
        readonly bool triple;

        public Spitter(int x, int y, int depth)
        {
            half = new Vector2(0.4f, 0.4f);
            pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
            Setup("Spitter", "spitter", 3, depth);
            shootT = 1f + Random.value * 2f;
            triple = depth > 140;
            gold = 3;
            blood = new Color(0.9f, 0.4f, 0.7f);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(9f, true, dt);
            shootT -= dt;
            if (HasTarget)
            {
                Vector2 d = TPos - pos;
                facing = d.x > 0 ? 1 : -1;
                if (shootT <= 0f && d.magnitude < 9f)
                {
                    shootT = 2.6f;
                    animT = 0.17f;
                    Vector2 dir = (d + Vector2.up * 0.4f).normalized;
                    Spit(dir);
                    if (triple) { Spit(U.Rot(dir, 18f)); Spit(U.Rot(dir, -18f)); }
                    G.sfx.Play("shoot", 0.5f, 0.6f);
                }
            }
            vel.x = 0f;
            vel.y = Mathf.Max(vel.y - 40f * dt, -16f);
            Vector2 p = pos;
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) vel.y = 0f;
            pos = p;
            if (shootT > 0.3f) animT = 0f;
            Draw(-half.y);
        }

        void Spit(Vector2 dir) =>
            G.Spawn(new Bullet(pos + Vector2.up * 0.25f, dir * 6f, Team.Beast, EnemyShotDamage, dmg, "b_spit", new Color(0.6f, 1f, 0.4f)));
    }
}
