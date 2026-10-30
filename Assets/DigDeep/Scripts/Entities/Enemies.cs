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

        // ACID eats living things (not machines); strong POISON poisons them; FUMES hurt them too
        float acidHurtT, fumeHurtT;
        protected virtual bool Machine => this is Clank || this is Drone || this is TeslaCoil;
        void ChemTick(float dt, byte body)
        {
            acidHurtT -= dt; fumeHurtT -= dt;
            if (Machine || Intangible) return;
            if (body == Fluids.Acid && acidHurtT <= 0f && Fluids.Concentration(pos, Fluids.Acid) > 0.5f)
            {
                acidHurtT = 0.35f;
                TakeShot(Mathf.Max(2f, maxHp * 0.06f), Vector2.up * 1.5f);
                G.fx.Burst(pos, 4, new Color(0.85f, 1f, 0.3f), 2f, 0.4f, -3f, 1f, false);
                if (dead) return;
            }
            if (body == Fluids.Poison && Fluids.Concentration(pos, Fluids.Poison) >= 0.6f) Poison(2f, Mathf.Max(1f, maxHp * 0.03f));
            int fx = Mathf.FloorToInt(pos.x), fy = Mathf.FloorToInt(pos.y);
            if (fumeHurtT <= 0f && Wd.LiquidType(fx, fy) == Fluids.Fumes && Wd.LiquidAmount(fx, fy) > 0.12f)
            {
                fumeHurtT = 0.7f;
                TakeShot(Mathf.Max(1f, maxHp * 0.03f), Vector2.zero);
            }
        }

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
        public static bool ArrowKill;   // (killed by an arrow: always a body, so the arrow can pin it)

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

        public string kindKey;   // which creature it is (its art key): rerolls never turn a foe into the same thing
        protected void Setup(string name, string key, int baseHp, int depth)
        {
            this.depth = depth;
            kindKey = key;
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
            DotGold();
            if (Random.value < 0.6f) G.fx.Burst(pos, 3, new Color(0.5f, 1f, 0.3f), 1.5f, 0.4f, 5f);
        }

        public void Freeze(float t)
        {
            if (dead || frostCd > 0f || intangible) return;
            if (this is Boss || this is WormSegment) { frostCd = 3f; G.fx.Ring(pos, 1.2f, Ice); flash = 0.1f; return; }   // too big to freeze solid: just a chill
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

        public bool kickBoom;   // DYNAMITE BOOT: it goes off when it slams into something
        void KickBoom()
        {
            if (!kickBoom) return;
            kickBoom = false;
            G.Explode(pos, 2.3f, Mathf.Max(4f, kickDmg * 1.5f), true, true, true);
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
                    KickBoom();
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
            if (landed) { if (vyWas < -6f || Mathf.Abs(vel.x) > 8f) KickBoom(); vel.y = vyWas < -6f ? -vyWas * 0.25f : 0f; vel.x *= 0.8f; }   // a little bounce off hard landings
            pos = p;
            bool onGround = Wd.Ground(pos + new Vector2(0f, -0.05f), half);
            if (onGround && vel.y <= 0.01f) vel.x = Mathf.MoveTowards(vel.x, 0f, 16f * dt);   // skids to a stop

            if (vel.magnitude > 5f)
            {
                foreach (var e in G.ents)
                {
                    if (e.dead || !(e is Enemy o) || kickHits.Contains(o) || o.Intangible || !U.Overlap(pos, half, o.pos, o.half)) continue;
                    kickHits.Add(o);
                    KickBoom();
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
            if (thrall) Thralls.Remove(this);
            else if (G.player != null && G.player.s.raiseDead && Random.value < 0.3f) Raise(this);   // DEATH'S GUIDE
            Vector2 kd = lastKnock.sqrMagnitude > 0.01f ? lastKnock : Vector2.up * 3f;
            Vector2 dir = kd.normalized;
            // the body flies off along the killing shot
            if (!(this is Boss))
            {
                if (DeathAnim != null && !(ArrowKill && LeavesCorpse)) G.Spawn(new FrameAnim(pos, DeathAnim, 12f, facing < 0, 12, dir * 2f, 6f));
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
            hp *= 9f; maxHp *= 9f;   // a real fight: it always carries a trinket
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

        // drawn by the Creator (and the like): it knows where you are and comes for you, always
        public bool enraged;
        public void Enrage() => enraged = true;

        protected void FindTarget(float range, bool needSight, float dt)
        {
            if (enraged && !thrall && G.player.mode != Player.Mode.Dead && !G.player.Camouflaged) { target = G.player; return; }
            retargetT -= dt;
            if (retargetT > 0f && HasTarget && (TPos - pos).sqrMagnitude < range * range * 1.5f) return;
            retargetT = 0.35f + Random.value * 0.2f;
            target = null;
            float best = range * range;
            var pl = G.player;
            // SHADOW STEP's decoy draws everything near it
            var dc = Decoy.Active;
            if (!thrall && dc != null && !dc.dead && (dc.pos - pos).sqrMagnitude < range * range * 1.5f) { target = dc; return; }
            if (thrall) range *= 1.5f;   // the risen look further for something to kill
            if (!thrall && pl.mode != Player.Mode.Dead && !pl.Camouflaged)   // CAMOUFLAGE: they can't see you
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
        float burnMul = 1f;   // pets' burns carry BEAST TREATS and the like
        public void Ignite(float t, float mul = 1f)
        {
            if (dead || fireLike || intangible || this is WormSegment) return;
            if (Wd.FluidAtPoint(pos) == Fluids.Water) return;
            if (burnT <= 0f) { burnTick = 0.2f; G.sfx.Play("ignite", 0.3f, 1.3f); burnMul = mul; }
            else burnMul = Mathf.Max(burnMul, mul);
            burnT = Mathf.Max(burnT, t * G.player.s.dotDur);
        }

        // PICKPOCKET: every poison or fire bite shakes a coin loose
        protected void DotGold()
        {
            if (!G.player.s.pickpocket || this is WormSegment || Random.value > 0.45f) return;
            G.Spawn(new Coin(pos + Vector2.up * 0.2f, G.player.s.dotGoldX2 ? 2 : 1, new Vector2(Random.Range(-3f, 3f), Random.Range(4f, 7f)), 0.15f));
        }

        // HEART OF UNDA: water eats away at foes standing in it
        float waterTick;
        void TickWaterHurt(float dt)
        {
            if (!G.player.s.waterHurt || this is WormSegment || intangible) return;
            if (!InWater) { waterTick = 0f; return; }
            waterTick -= dt;
            if (waterTick > 0f) return;
            waterTick = 0.4f;
            float d = (1.5f + Mathf.Max(0, depth) * 0.012f) * G.player.s.dotMul * (this is Boss ? 1f : 1.6f) * 0.5f;
            hp -= d;
            flash = Mathf.Max(flash, 0.04f);
            G.fx.Burst(pos, 3, new Color(0.4f, 0.75f, 1f), 1.5f, 0.4f, 4f);
            DotGold();
            if (hp <= 0f) Die();
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
                float dps = (1.5f + Mathf.Max(0, depth) * 0.012f) * (Plant ? 3f : 1f) * G.player.s.dotMul * burnMul * (this is Boss ? 1f : 1.6f);   // burns hurt ordinary foes more
                DotGold();
                TakeShot(dps * 0.33f, Vector2.zero);
                if (dead) return;
            }
            Fire.IgniteBox(pos, half + new Vector2(0.05f, 0.05f));
            Fire.MeltAround(pos, 1f);
            // a burning body is a fire like any other: whatever touches it catches, just as fast as from a flame
            foreach (var o in G.ents)
                if (!o.dead && o != this && o is Enemy oe && !oe.Burning && !oe.Intangible && U.Overlap(pos, half + new Vector2(0.05f, 0.05f), oe.pos, oe.half)) oe.Ignite(2f);
            var pl = G.player;
            if (pl.mode != Player.Mode.Dead && U.Overlap(pos, half + new Vector2(0.05f, 0.05f), pl.pos, Player.Half))
            {
                burnTouchT += dt;
                if (burnTouchT > 0.5f) pl.SetOnFire();
            }
            else burnTouchT = 0f;
            if (Random.value < 0.7f) G.fx.Burst(pos + new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y)), 1, new Color(1f, Random.Range(0.4f, 0.9f), 0.15f), 1.5f, 0.4f, -6f, 1f, false);
        }

        // nothing from outside gets into a boss arena (worms tunnelling, bats flying): only what was born in one stays in it
        bool arenaChecked, arenaOk;
        Vector2 lastOut;
        void ArenaGuard()
        {
            bool inside = false;
            foreach (var a in BossArena.All) if (!a.dead && a.Inside(pos)) { inside = true; break; }
            if (!arenaChecked) { arenaChecked = true; arenaOk = inside; lastOut = pos; }
            if (arenaOk) return;
            if (inside) { pos = lastOut; vel = -vel * 0.3f; }
            else lastOut = pos;
        }

        bool wasWetE = true;

        // under water and out of breath: it swims up and makes for the nearest way out
        protected bool drowning;
        float burnTouchT;
        static float helpCd;
        protected int escapeDir = 1;
        float escapeT;
        int EscapeSide()
        {
            for (int k = 1; k <= 10; k++)
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 q = pos + new Vector2(s * k, 1f);
                    int qx = Mathf.FloorToInt(q.x), qy = Mathf.FloorToInt(q.y);
                    if (Wd.Solid(qx, qy)) continue;
                    if (Wd.LiquidType(qx, qy) != Fluids.Water || Wd.LiquidAmount(qx, qy) < 0.3f) return s;   // air (or shallows) that way
                }
            return facing;
        }

        // ---------------------------------------------------------------- DEATH'S GUIDE: the risen
        // A slain foe can rise again on your side: ghostly, it hunts your enemies, never you, and it keeps up with
        // you (into a boss hall too). Three at most: the oldest crumbles when a fourth rises.
        public bool thrall;
        public static readonly List<Enemy> Thralls = new List<Enemy>();
        public static bool ThrallFiring;   // (set while a risen foe ticks: its shots are yours)

        public static void Raise(Enemy dead)
        {
            if (dead == null || dead is Boss || dead is WormSegment || dead.thrall) return;
            var at = dead.pos;
            var type = dead.GetType();
            int depth = dead.depth;
            G.Spawn(new DelayedCall(0.7f, () =>
            {
                var e = Make(type, at, depth);
                if (e == null) return;
                e.pos = at;
                e.thrall = true;
                e.team = Team.Player;
                e.maxHp = e.hp = Mathf.Max(1f, e.maxHp * 0.8f);
                e.gold = 0;
                e.arenaChecked = true; e.arenaOk = true;
                Thralls.RemoveAll(t => t == null || t.dead);
                while (Thralls.Count >= 3) { var old = Thralls[0]; Thralls.RemoveAt(0); if (!old.dead) { G.fx.Smoke(old.pos, 8, 0.8f); old.Kill(); } }
                Thralls.Add(e);
                G.Spawn(e);
                G.fx.Burst(at, 24, new Color(0.7f, 0.45f, 1f), 5f, 0.6f, -6f);
                G.fx.Ring(at, 1.2f, new Color(0.75f, 0.5f, 1f));
                G.sfx.Play("death", 0.4f, 1.6f);
                G.Popup(at + Vector2.up, "RISEN", new Color(0.8f, 0.6f, 1f));
            }));
        }

        // a fresh foe of the same kind (whatever its constructor wants: grid position, depth, defaults)
        static Enemy Make(System.Type type, Vector2 at, int depth)
        {
            foreach (var ctor in type.GetConstructors())
            {
                var ps = ctor.GetParameters();
                var args = new object[ps.Length];
                int ints = 0; bool ok = true;
                for (int i = 0; i < ps.Length && ok; i++)
                {
                    var pt = ps[i].ParameterType;
                    if (pt == typeof(int)) { args[i] = ints == 0 ? Mathf.FloorToInt(at.x) : ints == 1 ? Mathf.FloorToInt(at.y) : depth; ints++; }
                    else if (pt == typeof(Vector2)) args[i] = at;
                    else if (pt == typeof(bool)) args[i] = false;
                    else if (pt == typeof(float)) args[i] = 1f;
                    else if (pt.IsEnum) args[i] = System.Enum.GetValues(pt).GetValue(0);
                    else ok = false;
                }
                if (!ok) continue;
                try { return ctor.Invoke(args) as Enemy; } catch { }
            }
            return null;
        }

        // the risen keep up with you: too far behind (or left outside the boss hall you're in), it drifts to your side
        void ThrallTick()
        {
            var p = G.player;
            if (p == null || p.mode == Player.Mode.Dead) return;
            bool behind = (p.pos - pos).sqrMagnitude > 15f * 15f;
            foreach (var a in BossArena.All) if (!a.dead && a.Active && a.Inside(p.pos) && !a.Inside(pos)) behind = true;
            if (!behind) return;
            G.fx.Burst(pos, 10, new Color(0.7f, 0.45f, 1f), 3f, 0.5f, -4f);
            Vector2 to = p.pos + new Vector2(Random.value < 0.5f ? -1.4f : 1.4f, 0.3f);
            if (Wd.BoxSolid(to, half)) to = p.pos;
            pos = to; vel = Vector2.zero;
            G.fx.Burst(pos, 14, new Color(0.7f, 0.45f, 1f), 3f, 0.5f, -4f);
        }

        protected void BaseTick(float dt)
        {
            if (!(this is Boss) && !(this is WormSegment) && BossArena.All.Count > 0 && !thrall) ArenaGuard();
            if (thrall) ThrallTick();
            // anything diving into water splashes it, by its size and speed
            if (!(this is Fish) && !(this is WormSegment))
            {
                bool wetNow = Wd.FluidAtPoint(pos) == Fluids.Water;
                if (wetNow && !wasWetE && vel.sqrMagnitude > 4f) Fluids.Impact(pos - new Vector2(0f, half.y * 0.5f), Mathf.Max(half.x, half.y) * 1.6f, vel);
                wasWetE = wetNow;
            }
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
            TickWaterHurt(dt);
            if (dead) return;
            if (elite) EliteTick(dt);
            if (poisonT > 0f)
            {
                PoisonTick(dt);
                if (Random.value < 0.15f) G.fx.Burst(pos, 1, new Color(0.5f, 1f, 0.3f), 1f, 0.5f, 5f);
                if (hp <= 0f) { Die(); return; }
            }
            var p = G.player;
            if (!thrall && p.mode != Player.Mode.Dead && !intangible && !Harmless && contactCd <= 0f && !(p.Camouflaged && p.s.camoGhost) && U.Overlap(pos, half * 0.85f, p.pos, Player.Half))
            {
                p.Hurt(dmg, pos);
                contactCd = 0.5f;
                OnTouch(p);
            }
            // the risen maul anything of the enemy's they touch
            if (thrall && !intangible && contactCd <= 0f)
                foreach (var e in G.ents)
                {
                    if (e.dead || !(e is Enemy o) || o.team == Team.Player || o.Intangible) continue;
                    if (!U.Overlap(pos, half * 0.9f, o.pos, o.half)) continue;
                    o.TakeShot(Mathf.Max(2f, dmg * 0.06f), (o.pos - pos).normalized * 5f + Vector2.up * 2f);
                    contactCd = 0.5f;
                    break;
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
            if (body == Fluids.Water || body == Fluids.Haste || body == Fluids.Poison || body == Fluids.Acid) Fluids.Stir(pos, vel, Mathf.Clamp(half.x, 0.25f, 1f));   // moving through it stirs it
            ChemTick(dt, body);
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
            bool wasDrowning = drowning;
            drowning = choking && head == Fluids.Water;
            if (drowning && !wasDrowning && team == Team.Beast && helpCd <= Time.time && !(this is Boss)) { helpCd = Time.time + 6f; G.sfx.PlayAt(pos, "help", 0.5f, Random.Range(0.9f, 1.15f), 0.08f, 14f); }   // a cry for help as it goes under
            escapeT -= dt;
            if (drowning && escapeT <= 0f) { escapeT = 0.6f; escapeDir = EscapeSide(); }
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
                if (thrall) c = Color.Lerp(c, new Color(0.75f, 0.55f, 1f, 0.85f), 0.55f + 0.1f * Mathf.Sin(Time.time * 4f));   // risen: ghostly violet
                sr.color = c;
            }
            WeakSpot();
            Place(yOff);
            XRay();
            // a hit makes it pop bigger for a frame or two
            if (!(this is Boss)) { float k = (1f + Mathf.Max(0f, flash) * 3f) * (elite ? 1.4f : 1f); go.transform.localScale = new Vector3(k, k, 1f); }
        }

        // X-RAY: a foe hidden in water, poison, acid, mist or thick gas keeps a red outline drawn over everything,
        // so you can always see where it is
        GameObject xrGo;
        SpriteRenderer xrSr;
        static readonly Dictionary<Sprite, Sprite> outlines = new Dictionary<Sprite, Sprite>();

        void XRay()
        {
            bool covered = false;
            if (sr != null && sr.enabled && !intangible)
            {
                byte f = Wd.FluidAtPoint(pos);
                covered = f != Fluids.None;
                if (!covered)
                {
                    int x = Mathf.FloorToInt(pos.x), y = Mathf.FloorToInt(pos.y);
                    byte lt = Wd.LiquidType(x, y);
                    covered = Fluids.IsGas(lt) && Wd.LiquidAmount(x, y) > 0.3f;
                }
            }
            if (!covered) { if (xrSr != null) xrSr.enabled = false; return; }
            var o = Outline(sr.sprite);
            if (o == null) return;
            if (xrGo == null)
            {
                xrGo = new GameObject("XRay");
                xrGo.transform.SetParent(go.transform, false);
                xrSr = Art.AddSR(xrGo, o, 99);
            }
            xrSr.enabled = true;
            xrSr.sprite = o;
            xrSr.flipX = sr.flipX; xrSr.flipY = sr.flipY;
            xrSr.color = new Color(1f, 0.2f, 0.18f, 0.6f + 0.25f * Mathf.Sin(Time.time * 7f));
        }

        // the edge pixels of a sprite, in white (tinted red when drawn)
        static Sprite Outline(Sprite s)
        {
            if (s == null) return null;
            if (outlines.TryGetValue(s, out var o)) return o;
            try
            {
                var r = s.textureRect;
                int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height), rx = Mathf.RoundToInt(r.x), ry = Mathf.RoundToInt(r.y);
                var src = s.texture.GetPixels32();
                int tw = s.texture.width;
                bool On(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && src[(ry + y) * tw + rx + x].a > 40;
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (On(x, y) && (!On(x - 1, y) || !On(x + 1, y) || !On(x, y - 1) || !On(x, y + 1))) px[y * w + x] = new Color32(255, 255, 255, 255);
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                tex.SetPixels32(px); tex.Apply();
                o = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(s.pivot.x / r.width, s.pivot.y / r.height), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            catch { o = null; }
            outlines[s] = o;
            return o;
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
                if ((HasTarget && (dist < 7f || enraged)) || flash > 0f) { awake = true; G.sfx.Play("bat", 0.4f); }
                else { animT = 0f; DrawHanging(); return; }
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

        // asleep, a bat hangs from the ceiling with its wings folded (its own picture, when there is one)
        void DrawHanging()
        {
            var hang = imp ? null : SpriteSheet.Frames("bathang");
            if (hang == null || hang.Length == 0) { Draw(0f); return; }
            var s = hang[0];
            float above = (s.rect.height - s.pivot.y) / s.pixelsPerUnit, below = s.pivot.y / s.pixelsPerUnit;
            int cy = Mathf.FloorToInt(pos.y);
            float yOff = Wd.Solid(Mathf.FloorToInt(pos.x), cy + 1) ? cy + 1f - pos.y - above : (above - below) * -0.5f;
            Draw(yOff);
            if (dead || sr == null) return;
            sr.sprite = s;
            if (flash > 0f) sr.color = new Color(1f, 0.6f, 0.6f);
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
            // jungle spitters hawk up gobs of liquid now and then: some poison, some acid
            if (Biomes.Index(depth) == Biomes.Jungle) juice = Random.value < 0.5f ? Fluids.Poison : Fluids.Acid;
        }

        readonly byte juice;
        int shots;

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
                    shots++;
                    if (juice != 0 && shots % 2 == 0)
                    {
                        // a lobbed gob of poison or acid: it bursts where it lands and pools there
                        float tFly = Mathf.Clamp(Mathf.Abs(d.x) / 6f, 0.45f, 1.2f);
                        var v = new Vector2(d.x / tFly, (d.y + 0.5f * LiquidGlob.Grav * tFly * tFly) / tFly);
                        G.Spawn(new LiquidGlob(pos + Vector2.up * 0.25f, v, juice, 26, dmg, this));
                        G.sfx.Play("splat", 0.5f, 0.7f);
                    }
                    else
                    {
                        Spit(dir);
                        if (triple) { Spit(U.Rot(dir, 18f)); Spit(U.Rot(dir, -18f)); }
                        G.sfx.Play("shoot", 0.5f, 0.6f);
                    }
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

    // A gob of poison or acid lobbed by a jungle spitter: it bursts on whatever it hits into a splash of the liquid,
    // which then pools on the ground and does its work there
    public class LiquidGlob : Entity
    {
        public const float Grav = 20f;
        Vector2 vel;
        readonly byte type;
        readonly int grains, dmg;
        readonly Enemy owner;
        float t;

        public override bool AlwaysActive => true;

        public LiquidGlob(Vector2 at, Vector2 v, byte type, int grains, int dmg, Enemy owner)
        {
            pos = at; vel = v; this.type = type; this.grains = grains; this.dmg = dmg; this.owner = owner;
            MakeGO("Glob", Art.Bullets.TryGetValue("b_spit", out var s) ? s : Art.Bullets["b_enemy"], 20);
            sr.color = type == Fluids.Acid ? new Color(0.9f, 1f, 0.35f) : new Color(0.45f, 0.9f, 0.3f);
            go.transform.localScale = Vector3.one * 1.4f;
            Place();
        }

        public override void Tick(float dt)
        {
            t += dt;
            vel.y -= Grav * dt;
            Vector2 next = pos + vel * dt;
            bool hit = t > 4f || Wd.Solid(Mathf.FloorToInt(next.x), Mathf.FloorToInt(next.y));
            var pl = G.player;
            if (!hit && pl.mode != Player.Mode.Dead && U.Overlap(next, new Vector2(0.2f, 0.2f), pl.pos, Player.Half))
            {
                hit = true;
                pl.Hurt(dmg, pos);
                if (type == Fluids.Poison) pl.Poison(3f);
            }
            if (!hit) { pos = next; if (Random.value < 0.5f) G.fx.Burst(pos, 1, sr.color, 0.5f, 0.3f, 8f); Place(); return; }
            Fluids.Spill(pos - vel.normalized * 0.15f, type, grains, vel);
            G.fx.Burst(pos, 10, sr.color, 4f, 0.35f, 15f);
            G.sfx.PlayAt(pos, "splat", 0.5f, type == Fluids.Acid ? 1.3f : 0.9f);
            Kill();
        }
    }
}
