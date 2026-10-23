using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    public partial class Player : Entity
    {
        public static readonly Vector2 Half = new Vector2(0.32f, 0.42f);
        const float E = 0.001f;
        const float SwingTime = 0.12f;

        public readonly CharDef character;
        public Stats s = new Stats();
        public Vector2 vel;
        public int facing = 1;
        public bool grounded;

        public SpecialDef special;
        public int charges;        // stored special uses
        public float energy;       // progress towards the next use
        public readonly List<ItemStack> items = new List<ItemStack>();
        public int sel;
        public readonly Dictionary<string, int> skills = new Dictionary<string, int>();
        public readonly List<SkillDef> skillOrder = new List<SkillDef>();
        public readonly HashSet<string> combos = new HashSet<string>();
        readonly HashSet<string> combosFired = new HashSet<string>();
        // one-of-a-kind trinkets (cap 1) that have already turned up this run: they never show up again,
        // taken or not (other trinkets with a similar effect still can)
        public readonly HashSet<string> seenOnce = new HashSet<string>();
        public void MarkSeen(Def d) { if (d is SkillDef k && k.cap <= 1) seenOnce.Add(k.id); }
        public bool SeenOnce(string id) => seenOnce.Contains(id);
        public readonly List<ComboDef> comboOrder = new List<ComboDef>();

        public enum Mode { Normal, Grapple, Quick, Swing, Dig, Pound, Dead, Parry }
        public Mode mode;

        public float stillT, grace;
        float stillAimT;
        public int nextMoss;
        float knockT, modeT, coyote, invuln, pushT, swingT, squash, animT, snapT, snapX, bombT, superT, goldFrac, orbAng, orbCd;
        int airJumps, goldTally;
        Vector2 anchor;
        readonly Transform tool, rope, arrow;
        readonly SpriteRenderer toolSr, ropeSr, arrowSr;
        readonly List<Transform> orbs = new List<Transform>();

        // ---------------- derived stats ----------------
        public float Speed => Mathf.Clamp(s.speedMul + (BuffActive(Buff.Speed) ? 0.4f : 0f), 0.4f, 2.4f);
        public float JumpMul => Mathf.Clamp(s.jumpMul, 0.6f, 1.8f) * (Cursed(Curse.Weight) ? 0.75f : 1f);   // CURSE OF WEIGHT
        // FIRE RATE: bonuses above the base only count for half, so it climbs slowly (penalties still bite in full)
        public float SwingRate
        {
            get
            {
                float r = s.swing > 1f ? 1f + (s.swing - 1f) * 0.5f : s.swing;
                return Mathf.Clamp(r * (BuffActive(Buff.Rate) ? 1.35f : 1f), 0.3f, 2.5f) * Mathf.Clamp(s.rateMult, 0.2f, 3f);   // multipliers go past the cap
            }
        }
        public float Luck => s.luck;
        public float CritChance => Mathf.Max(0f, s.critChance + s.luck * 0.02f);
        public int MinePower => Mathf.Max(1, s.digPower) * Mathf.Max(1, attack != null ? attack.mine : 1);
        public float Dmg(float baseDmg) => baseDmg * Mathf.Max(0.2f, s.damage) * Mathf.Max(0.1f, s.dmgMult) * (s.berserk && s.hp <= Stats.Heart ? 1.5f : 1f) * (BuffActive(Buff.Damage) ? 1.5f : 1f);
        public float WeaponDamage => attack != null ? Dmg(attack.dmg * attack.count) : 0f;
        public float SwingsPerSec => attack != null ? SwingRate / Mathf.Max(0.02f, attack.delay * DelayMul(weapon)) : 0f;

        public bool Invulnerable => invuln > 0f || grace > 0f || mode == Mode.Quick || mode == Mode.Dig || mode == Mode.Pound || mode == Mode.Parry || ReloadGhosting;
        public int MaxCharges
        {
            get
            {
                if (special == null) return 0;
                int n = special.uses + s.extraCharges;
                return n;
            }
        }
        // specials recharge over time: energy is in seconds
        public float NeedEnergy => special == null ? 1f : Mathf.Max(0.3f, special.cd * s.chargeMul);
        public float ChargeFrac => charges >= MaxCharges ? 1f : Mathf.Clamp01(energy / NeedEnergy);
        public float GoldMul => s.goldMul * (attack != null && attack.doubleGold ? 2f : 1f);
        public int ItemTotal { get { int n = 0; foreach (var it in items) n += it.count; return n; } }
        public int Depth => Mathf.Max(0, -Mathf.FloorToInt(pos.y));

        public Player(Vector2 p, CharDef c)
        {
            pos = p;
            character = c;
            c.ApplyTo(s);
            c.innate?.Invoke(this);
            MakeGO("Player", c.frames[0], 20);
            tool = Child("Gun", DB.Pickaxe.icon, 21, out toolSr);
            rope = Child("Rope", Art.Pixel, 19, out ropeSr);
            ropeSr.color = new Color(0.85f, 0.75f, 0.55f);
            arrow = Child("StarArrow", Art.Arrow, 22, out arrowSr);
            arrowSr.color = new Color(1f, 0.85f, 0.3f, 0.85f);
            slots[0] = new Weapon((AttackDef)DB.ById[c.weapon]);
            special = (SpecialDef)DB.ById[c.special];
            if (c.skills != null)
                foreach (var id in c.skills)
                    if (DB.ById.TryGetValue(id, out var sd) && sd is SkillDef k) AddSkillQuiet(k);
            charges = MaxCharges;
            AddItem(DB.Ale, 1);
        }

        Transform Child(string name, Sprite spr, int order, out SpriteRenderer r)
        {
            var g = new GameObject(name);
            g.transform.SetParent(G.runRoot, false);
            r = Art.AddSR(g, spr, order);
            r.enabled = false;
            return g.transform;
        }

        void AddSkillQuiet(SkillDef k)
        {
            if (!skills.ContainsKey(k.id)) skillOrder.Add(k);
            skills[k.id] = SkillCount(k.id) + 1;
            ApplyStats(k.st);
            k.apply?.Invoke(this);
        }

        public int SkillCount(string id) => skills.TryGetValue(id, out var n) ? n : 0;
        public bool Combo(string id) => combos.Contains(id);
        public bool Owns(string id) => SkillCount(id) > 0 || Holds(id) || (special != null && special.id == id);
        public bool HasSkill(string id) => SkillCount(id) > 0;

        // ================================================================== charging (Isaac-style active item)

        // Kills and boss hits speed a special up, unless it has a fixed cooldown (KICK).
        public void BonusEnergy(float e)
        {
            if (special == null || special.fixedCd) return;
            AddEnergy(e);
        }

        public void AddEnergy(float e)
        {
            if (special == null || e <= 0f) return;
            if (charges >= MaxCharges) { energy = 0f; return; }
            energy += e;
            float need = NeedEnergy;
            while (energy >= need && charges < MaxCharges)
            {
                energy -= need;
                charges++;
                G.sfx.Play("ready", 0.75f, 1f + 0.1f * (charges - 1), 0f);   // you can hear your special is back
                G.fx.Ring(pos, 1.1f, new Color(0.45f, 0.85f, 1f));
                G.fx.Burst(pos, 8, new Color(0.45f, 0.85f, 1f), 3.5f, 0.3f, -2f);
                G.hud.SpecialReady();
            }
            if (charges >= MaxCharges) energy = 0f;
        }

        public void FillCharges()
        {
            charges = MaxCharges;
            energy = 0f;
        }

        // ================================================================== tick

        public override void Tick(float dt)
        {
            if (mode == Mode.Dead) return;
            invuln -= dt; grace -= dt; swingT -= dt; animT += dt;
            if (mode == Mode.Normal || mode == Mode.Swing) Unstick();
            UpdateAim();
            TickBuffs(dt);
            TickAegis(dt);
            TickBurnAndPoison(dt);
            TickItems(dt);
            TickCamo(dt);
            TickTentacles(dt);
            if (mode == Mode.Dead) return;
            squash = Mathf.MoveTowards(squash, 0f, dt * 3.5f);
            if (special != null && charges > MaxCharges) charges = MaxCharges;

            if (s.mossEvery > 0 && Depth >= nextMoss)
            {
                nextMoss += s.mossEvery;
                if (s.hp < s.maxHp) HealHp(RegenHeal);
            }
            if (s.bombRestock)
            {
                bombT += dt;
                if (bombT >= 75f)
                {
                    bombT = 0f;
                    if (AddItem(DB.Bomb, 1)) G.Popup(pos + Vector2.up, "+1 BOMB", new Color(1f, 0.85f, 0.3f));
                }
            }

            float mx = (GameInput.Held(Btn.Right) ? 1f : 0f) - (GameInput.Held(Btn.Left) ? 1f : 0f);
            bool up = GameInput.Held(Btn.Up), down = GameInput.Held(Btn.Down);
            if (stunT > 0f)
            {
                // shocked: you twitch on the spot and can't act
                stunT -= dt;
                mx = 0f; up = down = false;
                fireCd = Mathf.Max(fireCd, 0.05f);
                GameInput.Consume(Btn.Jump);
                if (Random.value < 0.4f) G.fx.Burst(pos + Random.insideUnitCircle * 0.4f, 1, new Color(0.85f, 0.97f, 1f), 3f, 0.12f, 0f, 1f, false);
            }
            // you always look where you aim
            if (mode != Mode.Quick) facing = aimDir.x >= 0f ? 1 : -1;

            bool anyInput = mx != 0f || up || down || GameInput.Held(Btn.Attack) || GameInput.Held(Btn.Special) ||
                            GameInput.Held(Btn.Item) || GameInput.Held(Btn.Jump);

            switch (mode)
            {
                case Mode.Normal: TickNormal(dt, mx, up, down); break;
                case Mode.Grapple: TickGrapple(dt); break;
                case Mode.Quick: TickQuick(dt); break;
                case Mode.Swing: TickSwing(dt, mx, up, down); break;
                case Mode.Dig: TickDig(dt); break;
                case Mode.Pound: TickPound(dt); break;
                case Mode.Parry: TickParry(dt); break;
            }
            if (mode == Mode.Normal) { TreasureNearby(); CurseFootsteps(); }
            // DEADLY STILLNESS: count how long you've stood still (a glint at the gun when the crit is ready)
            if (mode == Mode.Normal && grounded && Mathf.Abs(vel.x) < 0.2f) { stillAimT += dt; if (s.stillCrit && stillAimT >= 0.5f && stillAimT - dt < 0.5f) { G.fx.Burst(Muzzle, 6, new Color(0.6f, 1f, 1f), 3f, 0.3f, 0f); G.sfx.Play("blip", 0.4f, 2f, 0f); } }
            else stillAimT = 0f;
            if (mode != Mode.Dead) AddEnergy(dt);
            if (mode == Mode.Dead) return;
            Hazards();
            if (mode == Mode.Dead) return;

            if (mode == Mode.Normal && grounded && !anyInput && Mathf.Abs(vel.x) < 0.05f) stillT += dt;
            else stillT = 0f;

            if (Depth > G.maxDepth) G.maxDepth = Depth;
            Orbitals(dt);
            StarArrow();
            Render();
        }

        // ---------------- water: Noita-style. Slow, floaty, you bob up to the surface if you let go, and you run out of air.
        public const float BreathMax = 8f;
        public float breath = BreathMax;
        float chokeT, swimBoost;
        bool swimming, climbing;

        // trapped gas (steam pooled under a ceiling) chokes you just like water, even when it has spread thin
        static bool InGas(Vector2 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
            return Wd.LiquidType(x, y) == Fluids.Steam && Wd.LiquidAmount(x, y) > 0.08f;
        }

        void TickBreath(float dt)
        {
            byte head = Wd.FluidAtPoint(pos + new Vector2(0f, Half.y - 0.08f));
            byte body = Wd.FluidAtPoint(pos);
            bool choking = head == Fluids.Water || head == Fluids.Lava || head == Fluids.Steam || body == Fluids.Steam || InGas(pos + new Vector2(0f, Half.y - 0.08f)) || InGas(pos);
            if (s.gills && head == Fluids.Water && body != Fluids.Steam) choking = false;   // GILLS: water is air to a dolphin
            if (choking)
            {
                breath -= dt;
                if (Random.value < 0.05f) G.fx.Burst(pos + Vector2.up * (Half.y + 0.05f), 1, new Color(0.85f, 0.95f, 1f), 1f, 0.7f, -5f, 1f, false);
                if (breath <= 0f)
                {
                    breath = 0f;
                    chokeT -= dt;
                    if (chokeT <= 0f)
                    {
                        // no knockback or mercy time: you just slowly drown
                        chokeT = 0.5f;
                        int d = Mathf.Max(5, s.maxHp / 20);
                        s.hp -= d;
                        G.Popup(pos + Vector2.up * 0.9f, "-" + d, new Color(0.5f, 0.8f, 1f));
                        G.sfx.Play("hurt", 0.4f, 1.3f);
                        G.hud.DamageFlash();
                        if (s.hp <= 0) { s.hp = 1; Hurt(1, pos); }
                    }
                }
            }
            else breath = Mathf.Min(BreathMax, breath + dt * 4f);
        }

        void TickNormal(float dt, float mx, bool up, bool down)
        {
            TickBreath(dt);
            byte bodyF = Wd.FluidAtPoint(pos), feetF = Wd.FluidAtPoint(pos - new Vector2(0f, Half.y - 0.05f));
            bool wetBody = bodyF == Fluids.Water || bodyF == Fluids.Lava;
            bool wetFeet = feetF == Fluids.Water || feetF == Fluids.Lava;
            bool wasSwimming = swimming;
            swimming = wetBody || (wetFeet && !grounded && vel.y <= 1.5f && !GameInput.Held(Btn.Jump));
            if (swimming && !wasSwimming && vel.y < 0f)
            {
                // splash: even a small drop plunges you a little way under before you bob back up
                vel.y = Mathf.Clamp(vel.y, -7f, -5f);   // about 1.5 tiles from a small drop, under 3 from a big one
                G.fx.Burst(pos - new Vector2(0f, Half.y), 10, new Color(0.75f, 0.95f, 0.95f), 4f, 0.4f, 20f, 1f, false);
                G.sfx.Play("land", 0.4f, 1.6f);
            }
            if (swimming || (wetFeet && !grounded)) { Swim(dt, mx, down, wetBody); return; }

            knockT -= dt;
            superT -= dt;
            // LEVITATION: in the air (or pressing up on the ground) you float wherever you hold
            // (the octopus's TENTACLES do the same while one of them grips a wall)
            bool lift = (s.levitate || TentacleGrip) && knockT <= 0f && (!grounded || (up && !down));
            if (lift) { climbing = false; Levitate(dt, mx, up, down); }
            else
            {
            float target = mx * 6.4f * Speed * (1f - FireSlow);
            float acc = grounded ? (mx != 0f ? 95f : 120f) : 65f;   // tight, Super Crate Box-like control
            if (knockT > 0f) acc *= 0.12f;   // blown away by a blast: you can barely steer
            vel.x = U.Approach(vel.x, target, acc * dt);

            float g = 58f;   // heavier and snappier, same jump height
            if (superT > 0f && vel.y > 0f) { }   // a super jump rides its full arc
            else if (vel.y < 0f) g *= 1.35f;
            else if (!GameInput.Held(Btn.Jump)) g *= 2.1f;
            else if (vel.y < 3f) g *= 0.6f;   // floaty apex
            vel.y = Mathf.Max(vel.y - g * dt, -20f);
            if (s.glide > 0 && vel.y < 0f && GameInput.Held(Btn.Jump))
            {
                vel.y = Mathf.Max(vel.y, s.glide >= 2 ? -2.5f : -5f);
                if (Random.value < 0.15f) G.fx.Burst(pos + Vector2.up * 0.2f, 1, new Color(1f, 1f, 1f, 0.6f), 1f, 0.4f, 0f);
            }
            if (s.jetpack) Jetpack(dt);
            }

            // VINES (grass ropes): hold up or down on one to climb it; jump to let go
            bool onVine = (Wd.Flags(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y)) & World.Vine) != 0;
            if (!onVine || knockT > 0f || lift) climbing = false;
            else if ((up || (down && !grounded)) && !GameInput.Held(Btn.Jump)) climbing = true;
            if (climbing)
            {
                vel.y = up ? 4.2f : down ? -5f : 0f;
                vel.x = U.Approach(vel.x, mx * 2.5f, 60f * dt);
                pos.x = Mathf.MoveTowards(pos.x, Mathf.Floor(pos.x) + 0.5f, dt * (mx == 0f ? 4f : 0f));
                airJumps = s.airJumps;
                coyote = 0.09f;
                if ((up || down) && Random.value < dt * 3f) G.sfx.Play("dig", 0.15f, 1.8f);
                if (GameInput.Buffered(Btn.Jump)) climbing = false;   // the jump below takes you off it
            }
            if (grounded) { coyote = 0.09f; airJumps = s.airJumps; } else if (!climbing) coyote -= dt;
            if (!lift && !WallMoves(mx) && GameInput.Buffered(Btn.Jump))
            {
                if (coyote > 0f)
                {
                    GameInput.Consume(Btn.Jump);
                    float m = 1f;
                    if (s.coil && down)
                    {
                        m = 1.45f;
                        G.fx.Burst(pos - new Vector2(0, Half.y), 14, new Color(0.8f, 0.85f, 0.95f), 6f, 0.35f, 0f);
                        G.sfx.Play("jump", 0.6f, 0.7f);
                    }
                    Jump(m);
                }
                else if (airJumps > 0 || s.wings)
                {
                    GameInput.Consume(Btn.Jump);
                    if (!s.wings || airJumps > 0) airJumps--;   // WINGS OF ICARUS: never run out
                    if (s.wings) { flapT = 0.3f; G.sfx.Play("swing", 0.45f, 0.7f); }
                    Jump(0.88f);
                    G.fx.Burst(pos - new Vector2(0, Half.y), 10, new Color(0.8f, 0.9f, 1f), 4f, 0.3f, 0f);
                }
            }

            if (snapT > 0f) { snapT -= dt; pos.x = Mathf.MoveTowards(pos.x, snapX, dt * 8f); }
            // GROUND SLAM: press down in the air
            if (s.slam && !grounded && GameInput.Pressed(Btn.Down) && HighEnoughToSlam()) { StartPound(); return; }

            Stomp(dt);
            MoveBody(dt, mx);

            TickWeapon(dt);
            if (GameInput.Take(Btn.Swap)) SwapWeapon();
            if (GameInput.Take(Btn.Special)) UseSpecial(mx, up, down);
            if (GameInput.Take(Btn.Item)) UseItem();
            if (GameInput.Take(Btn.Cycle) && items.Count > 1)
            {
                sel = (sel + 1) % items.Count;
                G.sfx.Play("swing", 0.4f, 1.5f);
            }
        }

        void Swim(float dt, float mx, bool down, bool submerged)
        {
            float target = mx * 6.4f * Speed * (s.flippers ? 1.5f : 0.55f) * (1f - FireSlow);   // FLIPPERS: 1.5x your land speed
            vel.x = U.Approach(vel.x, target, (s.flippers ? 60f : 30f) * dt);
            bool head = Wd.FluidAtPoint(pos + new Vector2(0f, Half.y - 0.08f)) != Fluids.None;
            swimBoost -= dt;
            // a jump under water is a quick kick upwards; reach the surface during it and you leap out
            if (head && GameInput.Buffered(Btn.Jump)) { GameInput.Consume(Btn.Jump); vel.y = Mathf.Max(vel.y, 9.5f); swimBoost = 0.45f; G.fx.Burst(pos, 4, new Color(0.8f, 0.95f, 1f), 2f, 0.5f, -3f, 1f, false); G.sfx.Play("jump", 0.4f, 0.7f); }
            if (swimBoost > 0f && !head) { swimBoost = 0f; Jump(s.flippers ? 1.25f : 0.9f); MoveBody(dt, mx); return; }
            // FLIPPERS: shoot up out of the water and leap higher than a normal jump
            if (s.flippers && !head && vel.y > 6f) { Jump(1.25f); MoveBody(dt, mx); return; }
            if (Cursed(Curse.LeadBoots))
            {
                // CURSE OF LEAD BOOTS: you sink like a stone; kicking only slows the fall a little
                swimBoost = 0f;
                vel.y = U.Approach(vel.y, GameInput.Held(Btn.Jump) ? 1.2f : -3.5f, 12f * dt);
            }
            else if (swimBoost > 0f) { }
            else if (GameInput.Held(Btn.Jump)) vel.y = s.flippers ? U.Approach(vel.y, 15f, 80f * dt) : U.Approach(vel.y, 5.5f, 30f * dt);
            else if (down) vel.y = s.flippers ? U.Approach(vel.y, -12f, 70f * dt) : U.Approach(vel.y, -4f, 25f * dt);   // FLIPPERS dive fast too
            else if (vel.y < 0f) vel.y += 9f * dt;   // momentum carries you down a bit; buoyancy slowly wins
            else vel.y = U.Approach(vel.y, head ? 2.4f : 0f, 14f * dt);    // let go and you float up to the surface
            // at the surface you can hop out
            if (!head && GameInput.Buffered(Btn.Jump) && vel.y > -1f) { GameInput.Consume(Btn.Jump); Jump(s.flippers ? 1.2f : 0.8f); }
            airJumps = s.airJumps;
            coyote = 0f;
            if (Random.value < Mathf.Abs(vel.x) * dt * 2f) G.fx.Burst(pos, 1, new Color(0.8f, 0.95f, 1f), 1f, 0.5f, -3f, 1f, false);
            MoveBody(dt, mx);
            TickWeapon(dt);
            if (GameInput.Take(Btn.Swap)) SwapWeapon();
            if (GameInput.Take(Btn.Special)) UseSpecial(mx, GameInput.Held(Btn.Up), down);
            if (GameInput.Take(Btn.Item)) UseItem();
            if (GameInput.Take(Btn.Cycle) && items.Count > 1) { sel = (sel + 1) % items.Count; G.sfx.Play("swing", 0.4f, 1.5f); }
        }

        // An explosion throws you through the air.
        public void Launch(Vector2 v)
        {
            if (mode == Mode.Dead) return;
            if (mode == Mode.Grapple) EndGrapple();
            if (mode != Mode.Normal) return;
            vel = v;
            knockT = 0.45f;
            grounded = false;
            coyote = 0f;
            squash = -0.3f;
        }

        void Jump(float mul)
        {
            vel.y = 16.6f * Mathf.Sqrt(JumpMul) * mul;   // jump height scales linearly with JUMP, so low jumpers aren't crippled
            coyote = 0f;
            grounded = false;
            squash = -0.22f;
            G.sfx.Play("jump", 0.6f);
            G.fx.Burst(pos - new Vector2(0, Half.y), 5, new Color(0.6f, 0.5f, 0.4f), 2.5f, 0.3f, 5f);
        }

        void Stomp(float dt)
        {
            if (s.stomp <= 0 || vel.y > -2f) return;
            Vector2 next = pos + new Vector2(0f, vel.y * dt);
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en)) continue;
                if (pos.y - Half.y < en.pos.y) continue;
                if (!U.Overlap(next, Half, en.pos, en.half)) continue;
                en.Hurt(Dmg(s.stomp) * s.meleeMul * s.kickMul, Vector2.down * 2f);
                vel.y = s.stompBounce;
                grace = 0.25f;
                airJumps = s.airJumps;
                squash = -0.3f;
                G.sfx.Play("land", 0.8f, 1.3f);
                G.HitStop(0.04f);
                G.cam.Shake(0.15f);
                G.fx.Burst(en.pos + Vector2.up * en.half.y, 10, Color.white, 5f, 0.3f, 10f);
                break;
            }
        }

        void MoveBody(float dt, float mx)
        {
            var w = Wd;
            Vector2 p = pos;
            if (w.MoveX(ref p, Half, vel.x * dt, out var hx))
            {
                int dir = vel.x > 0 ? 1 : -1;
                if (mx != 0f && (mx > 0 ? 1 : -1) == dir)
                {
                    pushT += dt;
                    // lean on a boulder and it starts to roll (if there's room for it)
                    // (any row you span counts: swimming beside one resting on a ledge works too)
                    int by = int.MinValue;
                    if (hx.x != int.MinValue)
                        for (int yy = Mathf.FloorToInt(pos.y - Half.y + E); yy <= Mathf.FloorToInt(pos.y + Half.y - E); yy++)
                            if (Wd.Get(hx.x, yy) == Block.Boulder && !Wd.Solid(hx.x + dir, yy)) { by = yy; break; }
                    if (pushT > 0.1f && by != int.MinValue)
                    {
                        Wd.PushBoulder(hx.x, by, new Vector2(dir * BoulderBody.PushSpeed * Mathf.Clamp(Speed, 0.7f, 1.4f), 0f));
                        G.sfx.Play("rumble", 0.4f, 1.3f);
                        pushT = 0f;
                    }
                    else if (pushT > 0.07f && CanWalkDig(hx)) { WalkDig(hx); pushT = 0f; }
                }
                vel.x = 0f;
            }
            else pushT = 0f;

            bool wasGrounded = grounded;
            float vy = vel.y;
            if (superT > 0f && vel.y > 0f)
            {
                // super jump: smash through whatever is above and bash anything in the way
                bool blocked = false;
                if (BreakBox(p + new Vector2(0f, vel.y * dt + 0.06f), Half, s.digPower * 3, ref blocked)) G.cam.Shake(0.12f);
                G.HitEnemies(p, Half + new Vector2(0.25f, 0.3f), Dmg(2f), Vector2.up * 7f);
                if (Random.value < 0.6f) G.fx.Ghost(sr.sprite, go.transform.position, sr.flipX, new Color(1f, 0.85f, 0.4f, 0.6f));
            }
            if (w.MoveY(ref p, Half, vel.y * dt, out var hy))
            {
                if (vel.y > 0f)
                {
                    if (!TryNudge(ref p))
                    {
                        if (CanWalkDig(hy)) { WalkDig(hy); vel.y *= 0.6f; }   // head-butt soft blocks
                        else vel.y = 0f;
                    }
                }
                else vel.y = 0f;
            }
            pos = p;
            grounded = vel.y <= 0f && w.BoxSolid(pos + new Vector2(0, -0.04f), Half);
            if (!grounded && BoulderBody.All.Count > 0 && BoulderBody.StandOn(this, dt)) { grounded = true; if (vel.y < 0f) vel.y = 0f; }   // rolling boulders are solid on top
            if (grounded && !wasGrounded && vy < -17.5f)
            {
                // only slamming into TNT at (nearly) full falling speed sets it off; walking on it is safe
                int fy = Mathf.FloorToInt(pos.y - Half.y - 0.1f);
                for (int x = Mathf.FloorToInt(pos.x - Half.x + E); x <= Mathf.FloorToInt(pos.x + Half.x - E); x++) Wd.Impact(x, fy, -vy);
            }
            if (grounded && Mathf.Abs(vel.x) > 4f && Random.value < dt * 6f)
                G.fx.Smoke(pos - new Vector2(Mathf.Sign(vel.x) * 0.2f, Half.y - 0.1f), 1, 0.7f);   // dust kicked up while running
            if (grounded && !wasGrounded && vy < -9f)
            {
                squash = 0.32f;
                G.cam.Shake(Mathf.Clamp01((-vy - 9f) / 20f) * 0.25f);   // heavy landings thump the screen
                G.sfx.Play("land", 0.5f);
                G.fx.Burst(pos - new Vector2(0, Half.y), 6, new Color(0.55f, 0.45f, 0.38f), 3f, 0.3f, 5f);
            }
        }

        bool TryNudge(ref Vector2 p)
        {
            for (float o = 0.08f; o <= 0.3f; o += 0.08f)
            {
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    var q = new Vector2(p.x + o * sgn, p.y + 0.06f);
                    if (!Wd.BoxSolid(q, Half) && !Wd.BoxSolid(new Vector2(q.x, p.y), Half)) { p.x = q.x; return true; }
                }
            }
            return false;
        }

        bool CanWalkDig(Vector2Int c)
        {
            if (c.x == int.MinValue) return false;
            Block b = Wd.Get(c.x, c.y);
            var d = Tiles.Def(b);
            if (!d.solid || d.unbreakable || d.explosive || b == Block.Chest || b == Block.Crate || BigOre.Is(b)) return false;
            if (s.bulldozer <= 0) return false;   // walking through blocks is BADGER CLAWS only
            if (d.soft || s.bulldozer >= 3) return true;
            return s.bulldozer > 0 && Wd.Hp(c.x, c.y) <= MinePower * s.bulldozer;
        }

        void WalkDig(Vector2Int c)
        {
            Block was = Wd.Get(c.x, c.y);
            Wd.Break(c.x, c.y, true, GoldMul);
            G.fx.TileFlash(c);
            G.sfx.Play("dig", 0.4f, 1.15f, 0.15f);
            G.sfx.Material(was, true, 0.8f);
            G.cam.Shake(0.06f);
            G.HitStop(0.012f);
        }

        // Called by the game whenever the player breaks a block; only digging counts towards charge.
        public void OnBrokeBlock(Block b)
        {
            // digging no longer charges specials: only kills do
        }

        // ================================================================== special (X)

        void UseSpecial(float mx, bool up, bool down)
        {
            if (special == null) return;
            if (charges <= 0)
            {
                G.sfx.Play("deny", 0.35f, 1.4f);
                return;
            }
            bool ok = true;
            switch (special.type)
            {
                case Sp.Dash: StartDash(mx, up, down); break;
                case Sp.Grapple:
                    {
                        Vector2 d = aimDir;
                        G.Spawn(new Hook(pos + d * 0.3f, d * 30f, this, 9f));
                        G.sfx.Play("grapple", 0.6f);
                        break;
                    }
                case Sp.SuperJump: SuperJump(); break;
                case Sp.Rope: ok = StartSwing(); break;
                case Sp.QuickDig: StartDig(); break;
                case Sp.Blink: ok = Blink(7); break;
                case Sp.Kick: DoKick(); break;
                case Sp.Tide: Tide(); break;
                case Sp.Phase: ok = Phase(mx, up, down); break;
                case Sp.Parry: if (mode == Mode.Normal) StartParry(); else ok = false; break;
                case Sp.Camo: StartCamo(); break;
                case Sp.Invincible:
                    invuln = Mathf.Max(invuln, 0.5f);
                    aegisT = 0.5f;
                    G.sfx.Play("chest", 0.6f, 1.8f);
                    G.fx.Ring(pos, 1.4f, new Color(1f, 0.9f, 0.4f));
                    G.fx.Burst(pos, 16, new Color(1f, 0.9f, 0.4f), 5f, 0.35f, 0f);
                    break;
            }
            if (!ok) return;
            charges--;
            G.OnSpecialUsed(special.id, pos);
        }

        void Shockwave(Vector2 c, float r, float dmg, int tileMul, Src src)
        {
            G.fx.Ring(c, r, new Color(0.6f, 0.9f, 1f));
            G.fx.Burst(c, 24, new Color(0.7f, 0.95f, 1f), 9f, 0.4f, 0f);
            G.sfx.Play("explode", 0.5f, 1.4f);
            G.cam.Shake(0.3f);
            G.HitStop(0.05f);
            G.lighting.Flash(c, r * 2f, 0.3f);
            G.HitAltars(c, new Vector2(r, r), src);
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en)) continue;
                Vector2 d = en.pos - c;
                if (d.magnitude < r + 0.4f) en.Hurt(dmg, d.normalized * 8f + Vector2.up * 4f);
            }
            int x0 = Mathf.FloorToInt(c.x - r), x1 = Mathf.FloorToInt(c.x + r), y0 = Mathf.FloorToInt(c.y - r), y1 = Mathf.FloorToInt(c.y + r);
            float rr = r - 0.6f;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if ((new Vector2(x + 0.5f, y + 0.5f) - c).magnitude <= rr) Wd.Damage(x, y, s.digPower * tileMul, true, GoldMul);
        }

        void SuperJump()
        {
            superT = 0.6f;
            grace = 0.35f;
            vel = new Vector2(vel.x * 0.5f, 29f * Mathf.Sqrt(JumpMul));
            grounded = false; coyote = 0f;
            squash = -0.35f;
            G.sfx.Play("jump", 0.9f, 0.6f);
            G.sfx.Play("dash", 0.6f, 1.3f);
            G.fx.Burst(pos - new Vector2(0, Half.y), 18, new Color(1f, 0.85f, 0.4f), 6f, 0.4f, 5f);
            G.fx.Ring(pos - new Vector2(0, Half.y), 1.4f, new Color(1f, 0.85f, 0.4f));
            G.cam.Shake(0.2f);
        }

        // GLIMMER STONE: floods everything on screen with light for a while.
        // Enemies on that screen glow red and treasure sparkles.
        void Glimmer()
        {
            var c = G.cam;
            G.Reveal(c.pos, new Vector2(c.halfW + 1f, c.halfH + 1f), 15f);
            G.fx.Ring(pos, 10f, new Color(0.5f, 0.95f, 1f));
            G.sfx.Play("chest", 0.5f, 1.6f);
            int x0 = Mathf.FloorToInt(c.pos.x - c.halfW), x1 = Mathf.CeilToInt(c.pos.x + c.halfW);
            int y0 = Mathf.FloorToInt(c.pos.y - c.halfH), y1 = Mathf.CeilToInt(c.pos.y + c.halfH);
            int found = 0, foes = 0;
            for (int y = y0; y <= y1 && found < 80; y++)
                for (int x = x0; x <= x1 && found < 80; x++)
                {
                    Block b = Wd.Get(x, y);
                    if (b == Block.Gold || b == Block.Gem || b == Block.Chest || b == Block.Crate || b == Block.Crystal || BigOre.Is(b))
                    {
                        G.Spawn(new Glint(x, y, b, 15f));
                        found++;
                    }
                }
            foreach (var e in G.ents)
                if (!e.dead && e is Enemy en && !(en is WormSegment) && G.InReveal(en.pos)) foes++;
            G.Popup(pos + Vector2.up * 1.2f, foes + " FOES, " + found + " TREASURES", new Color(0.5f, 0.95f, 1f));
        }

        bool Blink(int dist)
        {
            int bx = Mathf.FloorToInt(pos.x);
            int feet = Mathf.FloorToInt(pos.y - Half.y + 0.01f);
            int ty = feet - dist;
            // can't blink through bedrock or a sealed boss arena: stop just above it
            for (int y = feet - 1; y >= ty; y--)
                if (Tiles.Def(Wd.Get(bx, y)).unbreakable) { ty = y + 1; break; }
            if (ty >= feet)
            {
                G.sfx.Play("deny", 0.5f);
                G.Popup(pos + Vector2.up, "BLOCKED", new Color(0.7f, 0.45f, 1f));
                return false;
            }
            for (int y = ty; y <= ty + 1; y++)
            {
                Block b = Wd.Get(bx, y);
                Wd.SetLiquid(bx, y, Fluids.None, 0f);
                if (b == Block.Spikes) Wd.Set(bx, y, Block.Air);
                else if (Tiles.Solid(b)) Wd.Break(bx, y, false);
            }
            G.fx.Burst(pos, 24, new Color(0.7f, 0.45f, 1f), 6f, 0.5f, 0f);
            G.fx.Ring(pos, 1.5f, new Color(0.7f, 0.45f, 1f));
            pos = new Vector2(bx + 0.5f, ty + Half.y + 0.002f);
            vel = Vector2.zero;
            grace = 0.3f;
            G.fx.Burst(pos, 24, new Color(0.7f, 0.45f, 1f), 6f, 0.5f, 0f);
            G.fx.Ring(pos, 1.5f, new Color(0.7f, 0.45f, 1f));
            G.lighting.Flash(pos, 5f, 0.4f);
            G.sfx.Play("dash", 0.8f, 1.5f);
            return true;
        }

        void Laser()
        {
            int cx = Mathf.FloorToInt(pos.x), cy = Mathf.FloorToInt(pos.y);
            const int reach = 16;
            float dmg = Dmg(10f);
            for (int x = 2; x < World.W - 2; x++) if (x != cx) LaserCell(x, cy);
            for (int y = cy - reach; y <= cy + reach; y++) if (y != cy) LaserCell(cx, y);

            var rowC = new Vector2(World.W * 0.5f, cy + 0.5f);
            var rowH = new Vector2(World.W * 0.5f, 0.55f);
            var colC = new Vector2(cx + 0.5f, cy + 0.5f);
            var colH = new Vector2(0.55f, reach + 0.5f);
            G.HitEnemies(rowC, rowH, dmg, Vector2.up * 4f);
            G.HitEnemies(colC, colH, dmg, Vector2.up * 4f);
            G.HitAltars(rowC, rowH, Src.Laser);
            G.HitAltars(colC, colH, Src.Laser);
            G.Spawn(new Beam(rowC, new Vector2(World.W, 0.9f)));
            G.Spawn(new Beam(colC, new Vector2(0.9f, reach * 2 + 1)));
            G.sfx.Play("explode", 0.9f, 1.8f);
            G.sfx.Play("shoot", 1f, 0.5f);
            G.cam.Shake(0.7f);
            G.HitStop(0.08f);
            G.lighting.Flash(pos, 14f, 0.6f);
        }

        void LaserCell(int x, int y)
        {
            var d = Tiles.Def(Wd.Get(x, y));
            if (!d.solid || d.unbreakable) return;
            Wd.Damage(x, y, 60, true, GoldMul);
        }

        bool BreakBox(Vector2 p, Vector2 h, int power, ref bool blocked)
        {
            bool broke = false;
            int x0 = Mathf.FloorToInt(p.x - h.x + E), x1 = Mathf.FloorToInt(p.x + h.x - E);
            int y0 = Mathf.FloorToInt(p.y - h.y + E), y1 = Mathf.FloorToInt(p.y + h.y - E);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!Wd.Solid(x, y)) continue;
                    var r = Wd.Damage(x, y, power, true, GoldMul);
                    if (r == World.Hit.Broke) { broke = true; G.fx.TileFlash(new Vector2Int(x, y)); }
                    else blocked = true;
                }
            if (broke) { G.sfx.Play("dig", 0.7f, 1.2f, 0.2f); G.cam.Shake(0.09f); }
            return broke;
        }

        public void StartGrapple(Vector2 a)
        {
            if (mode != Mode.Normal) return;
            anchor = a;
            mode = Mode.Grapple;
            modeT = 0.9f;
            G.sfx.Play("clank", 0.4f, 1.5f);
        }

        void TickGrapple(float dt)
        {
            modeT -= dt;
            Vector2 d = anchor - pos;
            float dist = d.magnitude;
            if (dist < 0.8f || modeT <= 0f || GameInput.Take(Btn.Jump)) { EndGrapple(); return; }
            vel = d / dist * 22f;
            Vector2 p = pos;
            bool hx = Wd.MoveX(ref p, Half, vel.x * dt, out _);
            bool hy = Wd.MoveY(ref p, Half, vel.y * dt, out _);
            pos = p;
            DrawRope(pos, anchor);
            if (hx && hy) EndGrapple();
        }

        void EndGrapple()
        {
            mode = Mode.Normal;
            vel *= 0.6f;
            vel.y = Mathf.Max(vel.y, 8f);
            ropeSr.enabled = false;
        }

        public void DrawRope(Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            ropeSr.enabled = true;
            rope.position = (Vector3)((a + b) * 0.5f);
            rope.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            rope.localScale = new Vector3(d.magnitude * 16f, 1f, 1f);
        }

        // ================================================================== items (C)

        public bool AddItem(ItemDef d, int n)
        {
            if (ItemTotal + n > s.itemCap) return false;
            foreach (var it in items)
                if (it.def == d) { it.count += n; return true; }
            items.Add(new ItemStack { def = d, count = n });
            return true;
        }

        void UseItem()
        {
            if (items.Count == 0) { G.sfx.Play("deny", 0.4f); return; }
            sel = Mathf.Clamp(sel, 0, items.Count - 1);
            var st = items[sel];
            Vector2 throwV = ThrowVel() * 0.92f;
            switch (st.def.type)
            {
                case It.Ale:
                    if (s.hp >= s.maxHp) { G.Popup(pos + Vector2.up, "ALREADY FULL", Color.white); G.sfx.Play("deny", 0.4f); return; }
                    if (NoHealing()) return;
                    s.hp = Mathf.Min(s.maxHp, s.hp + Stats.Heart);
                    G.sfx.Play("heal", 0.8f);
                    G.fx.Burst(pos, 16, new Color(1f, 0.75f, 0.2f), 4f, 0.6f, -4f);
                    G.Popup(pos + Vector2.up, "+" + Stats.Heart + " HEALTH", new Color(1f, 0.5f, 0.5f));
                    break;
                case It.Bomb:
                    G.Spawn(new Thrown(pos + Vector2.up * 0.2f, throwV, Thrown.Type.Bomb, 2.6f));
                    G.sfx.Play("swing", 0.6f);
                    break;
                case It.Flare:
                    G.Spawn(new Thrown(pos + Vector2.up * 0.2f, throwV * 1.2f, Thrown.Type.Flare, 0f));
                    G.sfx.Play("fuse", 0.6f);
                    break;
                case It.SuperBomb:
                    G.Spawn(new SuperBomb(this));
                    break;
                case It.Glimmer:
                    Glimmer();
                    break;
                case It.Sentry:
                    G.Spawn(new Sentry(new Vector2(pos.x, pos.y - Half.y), this));
                    G.sfx.Play("clank", 0.7f, 0.8f);
                    break;
                case It.Bore:
                    G.Spawn(new BoreCharge(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y - Half.y - 0.5f)));
                    snapX = Mathf.FloorToInt(pos.x) + 0.5f; snapT = 0.2f;
                    break;
            }
            st.count--;
            if (st.count <= 0)
            {
                items.RemoveAt(sel);
                if (sel >= items.Count) sel = 0;
            }
        }

        // ================================================================== loot / gold

        public bool Grant(Def d)
        {
            switch (d.kind)
            {
                case Kind.Attack:
                    TakeWeapon(new Weapon((AttackDef)d));
                    break;
                case Kind.Mod:
                    if (!ApplyMod(((ModDef)d).mod)) return false;
                    break;
                case Kind.Special:
                    if (special != null) G.Spawn(new LootPickup(pos + Vector2.up * 0.3f, special, 1.2f));
                    special = (SpecialDef)d;
                    energy = 0f;
                    break;
                case Kind.Skill:
                    {
                        var k = (SkillDef)d;
                        if (SkillCount(k.id) >= k.cap)
                        {
                            AddGold(25);
                            G.Popup(pos + Vector2.up, "ALREADY MAXED: +25 GOLD", new Color(1f, 0.85f, 0.3f));
                            G.sfx.Play("coin", 0.6f);
                            return true;
                        }
                        if (!skills.ContainsKey(k.id)) skillOrder.Add(k);
                        skills[k.id] = SkillCount(k.id) + 1;
                        ApplyStats(k.st);
                        k.apply?.Invoke(this);
                        break;
                    }
                case Kind.Item:
                    if (!AddItem((ItemDef)d, 1))
                    {
                        G.Popup(pos + Vector2.up, "BACKPACK FULL", new Color(1f, 0.5f, 0.4f));
                        G.sfx.Play("deny", 0.4f);
                        return false;
                    }
                    break;
                case Kind.Upgrade:
                    ((UpgradeDef)d).apply(this);
                    break;
            }
            G.sfx.Play("pickup", 0.8f);
            if (d.kind == Kind.Mod) G.hud.Banner(weapon.def.name + " + " + Mods.Names[(int)((ModDef)d).mod], d.desc, DB.ColorOf(d));
            else G.hud.Banner(d.name, string.IsNullOrEmpty(d.desc) ? DB.Extra(d) : d.desc, DB.ColorOf(d));
            G.fx.Burst(pos, 18, DB.ColorOf(d), 6f, 0.5f, 0f);
            RecomputeCombos();
            if (d.kind == Kind.Special) { charges = MaxCharges; energy = 0f; }   // a new special is ready to use
            return true;
        }

        public void ApplyStats(St st)
        {
            s.maxHp = Mathf.Max(50, s.maxHp + st.hp);
            if (st.hp > 0) s.hp += st.hp;
            s.hp = Mathf.Clamp(s.hp, 1, s.maxHp);
            s.damage += st.dmg;
            s.swing += st.swing;
            s.speedMul += st.speed;
            s.digPower = Mathf.Max(1, s.digPower + st.mining);
            s.jumpMul += st.jump;
            s.luck += st.luck;
        }

        public void RecomputeCombos()
        {
            foreach (var c in DB.Combos)
            {
                bool ok = true;
                if (c.IsTransform) ok = TagCount(c.tag) >= c.count;
                else foreach (var n in c.needs) if (!HasSkill(n)) { ok = false; break; }
                if (ok && !combos.Contains(c.id))
                {
                    combos.Add(c.id);
                    comboOrder.Add(c);
                    var cc = DB.ColorOfCombo(c);
                    if (combosFired.Add(c.id))
                    {
                        c.onActivate?.Invoke(this);
                        if (c.IsTransform)
                        {
                            // a TRANSFORMATION: a big moment
                            G.hud.Banner("TRANSFORMATION! " + c.name, c.desc, cc);
                            G.hud.ScreenFlash(0.6f);
                            G.sfx.Play("charge", 0.9f, 0.6f, 0f);
                            G.sfx.Play("chest", 0.9f, 0.8f);
                            G.cam.Shake(0.5f);
                            G.cam.Punch(0.3f);
                            G.HitStop(0.12f);
                            G.fx.Burst(pos, 60, cc, 11f, 1f, -2f);
                            G.fx.Ring(pos, 4f, cc);
                            G.lighting.Flash(pos, 10f, 0.8f);
                        }
                        else
                        {
                            G.hud.Banner("COMBO! " + c.name, c.desc, cc);
                            G.sfx.Play("chest", 0.8f, 1.2f);
                            G.fx.Burst(pos, 30, cc, 8f, 0.7f, 0f);
                            G.fx.Ring(pos, 2.5f, cc);
                            G.lighting.Flash(pos, 6f, 0.4f);
                        }
                    }
                    else G.Popup(pos + Vector2.up * 1.4f, (c.IsTransform ? "TRANSFORMED: " : "COMBO: ") + c.name, cc);
                }
                else if (!ok && combos.Contains(c.id))
                {
                    combos.Remove(c.id);
                    comboOrder.Remove(c);
                }
            }
            if (special != null && charges > MaxCharges) charges = MaxCharges;
            RebuildLooks();
        }

        // how many of your trinkets count towards a transformation set
        public int TagCount(string tag)
        {
            int n = 0;
            foreach (var k in skillOrder) if (k.tags != null && System.Array.IndexOf(k.tags, tag) >= 0) n++;
            return n;
        }

        public void AddGold(int v)
        {
            float f = v * s.coinMul + goldFrac;
            int n = (int)f;
            goldFrac = f - n;
            s.gold += n;
            if (s.goldToothEvery > 0)
            {
                goldTally += n;
                while (goldTally >= s.goldToothEvery)
                {
                    goldTally -= s.goldToothEvery;
                    if (s.hp < s.maxHp) HealHp(RegenHeal);
                }
            }
        }

        public void OnKill(Enemy e)
        {
            s.kills++;
            OnHuntKill(e);
            CurseKill();
            if (s.vampire > 0 && s.kills % s.vampire == 0 && s.hp < s.maxHp) HealHp(30);   // vampire fangs
            if (special != null && charges < MaxCharges && !special.fixedCd)
            {
                if (s.deathEngine && Random.value < 0.25f)
                {
                    charges++;
                    energy = 0f;
                    G.Popup(pos + Vector2.up * 1.2f, "CHARGE!", new Color(0.45f, 0.85f, 1f));
                }
                else AddEnergy((0.35f + e.gold * 0.05f) * s.killEnergy);   // kills shave time off the recharge
            }
            if (Combo("angel")) airJumps = s.airJumps;
        }

        // ================================================================== damage

        void Hazards()
        {
            int x0 = Mathf.FloorToInt(pos.x - Half.x + E), x1 = Mathf.FloorToInt(pos.x + Half.x - E);
            int y0 = Mathf.FloorToInt(pos.y - Half.y + E), y1 = Mathf.FloorToInt(pos.y + Half.y - E);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Block b = Wd.Get(x, y);
                    if (!s.lavaImmune && Wd.LiquidType(x, y) == Fluids.Lava && Wd.LiquidAmount(x, y) > Fluids.MinLevel * 1.6f && Wd.FluidAtPoint(new Vector2(Mathf.Clamp(pos.x, x, x + 0.99f), Mathf.Clamp(pos.y - Half.y + 0.05f, y, y + 0.99f))) == Fluids.Lava)
                    {
                        Hurt(HazardDmg, new Vector2(pos.x, y));
                        if (mode == Mode.Dead) return;
                        SetOnFire();   // lava sets you alight
                        vel.y = 14f;
                        return;
                    }
                    if (b == Block.Spikes && !s.spikeProof && pos.y - Half.y < y + 0.5f && vel.y <= 0.5f)
                    {
                        Hurt(HazardDmg, new Vector2(pos.x, y));
                        if (mode == Mode.Dead) return;
                        vel.y = 10f;
                        return;
                    }
                }
            if (grounded && !s.lavaImmune)
            {
                int fy = Mathf.FloorToInt(pos.y - Half.y - 0.1f);
                for (int x = x0; x <= x1; x++)
                    if (Wd.Get(x, fy) == Block.Magma)
                    {
                        Hurt(HazardDmg, new Vector2(pos.x, fy));
                        if (mode == Mode.Dead) return;
                        vel.y = 9f;
                        return;
                    }
            }
        }

        int HazardDmg => Mathf.RoundToInt(50f + Depth * 0.15f);

        // dmg is in health points (a heart is 100)
        public void Hurt(int dmg, Vector2 from)
        {
            if (mode == Mode.Parry) { Counter(from); return; }   // PARRY: whatever touches you gets countered
            if (mode == Mode.Dead || Invulnerable) return;
            EndCamo();
            if (UseMantle()) return;   // WARDSTONE
            if (s.armor > 0f && dmg < 999999) dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - s.armor)));   // IRON HIDE
            if (Cursed(Curse.Glass) && dmg < 999999) dmg = Mathf.RoundToInt(dmg * 1.5f);   // CURSE OF GLASS
            CursePoverty();
            if (reloading && s.reloadArmor > 0f && dmg < 999999) dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - s.reloadArmor)));
            if (mode == Mode.Grapple) EndGrapple();
            s.hp -= dmg;
            chargeT = 0f;
            G.Popup(pos + Vector2.up * 0.9f, "-" + Mathf.Min(dmg, 9999), new Color(1f, 0.3f, 0.3f));
            invuln = 1.2f + s.invulnBonus;
            float kx = pos.x - from.x;
            float dirx = Mathf.Abs(kx) < 0.05f ? -facing : Mathf.Sign(kx);
            vel = new Vector2(dirx * 7f, 9f);
            G.sfx.Play("hurt", 0.9f);
            G.fx.Blood(pos, vel.normalized, new Color(0.85f, 0.12f, 0.12f), 10);
            G.HitStop(0.09f);
            G.cam.Shake(0.4f);
            G.hud.DamageFlash();
            G.fx.Burst(pos, 14, new Color(1f, 0.25f, 0.25f), 6f, 0.5f, 20f);

            if (s.thorns > 0)
            {
                G.fx.Ring(pos, s.thornsR, new Color(0.5f, 1f, 0.4f));
                foreach (var e in G.ents)
                {
                    if (e.dead || !(e is Enemy en)) continue;
                    Vector2 d = en.pos - pos;
                    if (d.magnitude < s.thornsR) en.Hurt(Dmg(s.thorns), d.normalized * 7f + Vector2.up * 3f);
                }
            }

            if (s.hp <= 0)
            {
                if (TryRevive()) return;   // PHOENIX FEATHER, ANKH, TWIN SOULS, RABBIT'S FOOT
                Die();
            }
        }

        public const int RegenHeal = 40;   // what moss, fangs and the gold tooth give back each time

        public void HealHp(int hp)
        {
            if (NoHealing()) return;
            s.hp = Mathf.Min(s.maxHp, s.hp + hp);
            G.sfx.Play("heal", 0.6f);
            G.Popup(pos + Vector2.up, "+" + hp + " HEALTH", new Color(1f, 0.5f, 0.5f));
        }

        // n is in hearts
        public void Heal(int n)
        {
            if (NoHealing()) return;
            s.hp = Mathf.Min(s.maxHp, s.hp + n * Stats.Heart);
            G.sfx.Play("heal", 0.7f);
            G.Popup(pos + Vector2.up, "+" + n * Stats.Heart + " HEALTH", new Color(1f, 0.5f, 0.5f));
        }

        void Die()
        {
            s.hp = 0;
            mode = Mode.Dead;
            sr.enabled = false;
            toolSr.enabled = false;
            ropeSr.enabled = false;
            arrowSr.enabled = false;
            if (sightSr != null) sightSr.enabled = false;
            if (glowSr != null) glowSr.enabled = false;
            foreach (var o in orbs) o.gameObject.SetActive(false);
            G.fx.Burst(pos, 40, new Color(1f, 0.3f, 0.3f), 9f, 1f, 25f);
            G.fx.Burst(pos, 20, new Color(1f, 0.85f, 0.3f), 7f, 0.8f, 25f);
            G.sfx.Play("death", 0.9f);
            G.cam.Shake(0.6f);
            G.OnPlayerDied();
        }

        // ================================================================== relic visuals

        void Orbitals(float dt)
        {
            // CURSE OF THE DIGGER: two pickaxes circle you, digging away the ground (they never touch foes)
            int want = Cursed(Curse.Digger) ? 2 : 0;
            while (orbs.Count > want) { Object.Destroy(orbs[orbs.Count - 1].gameObject); orbs.RemoveAt(orbs.Count - 1); }
            while (orbs.Count < want)
            {
                var t = Child("Orbital", Art.Icons["pickaxe"], 21, out var r);
                r.enabled = true;
                r.color = Color.Lerp(Color.white, Curses.Col(Curse.Digger), 0.5f);
                t.localScale = Vector3.one * 0.75f;
                orbs.Add(t);
            }
            if (orbs.Count == 0) return;
            orbAng += dt * 4.5f;
            orbCd -= dt;
            bool hit = false;
            for (int i = 0; i < orbs.Count; i++)
            {
                float a = orbAng + i * Mathf.PI * 2f / orbs.Count;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 p = pos + dir * 1.5f;
                orbs[i].position = new Vector3(U.Snap(p.x), U.Snap(p.y), 0);
                orbs[i].rotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg * 2f);
                int cx = Mathf.FloorToInt(p.x), cy = Mathf.FloorToInt(p.y);
                var d = Tiles.Def(Wd.Get(cx, cy));
                if (d.solid && d.soft)
                {
                    Wd.Break(cx, cy, true, GoldMul);
                    G.sfx.Play("chip", 0.3f, 1.4f);
                }
            }
            if (hit) orbCd = 0.25f;
            if (Random.value < 0.1f) G.fx.Burst(pos + new Vector2(Mathf.Cos(orbAng), Mathf.Sin(orbAng)) * 1.5f, 1, Curses.Col(Curse.Digger), 1f, 0.4f, 0f, 1f, false);
        }

        void StarArrow()
        {
            arrowSr.enabled = false;
            if (s.starmap <= 0) return;
            var t = G.NearestTarget(pos, s.starmap >= 2);
            if (t == null) return;
            Vector2 d = t.Value - pos;
            if (d.magnitude < 3f) return;
            Vector2 n = d.normalized;
            Vector2 p = pos + n * 1.35f;
            arrowSr.enabled = true;
            arrow.position = new Vector3(U.Snap(p.x), U.Snap(p.y), 0);
            arrow.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg);
            arrowSr.color = new Color(1f, 0.85f, 0.3f, 0.55f + 0.3f * Mathf.Sin(Time.time * 6f));
        }

        // ================================================================== visuals

        void Render()
        {
            var fr = character.frames;
            Sprite f;
            if (mode == Mode.Grapple || mode == Mode.Quick || mode == Mode.Swing || mode == Mode.Dig || mode == Mode.Pound || superT > 0f) f = fr[3];
            else if (swingT > 0f && attack != null && attack.Melee) f = fr[4];
            else if (!grounded) f = fr[3];
            else if (Mathf.Abs(vel.x) > 0.5f) f = ((int)(animT * 10f * Speed)) % 2 == 0 ? fr[1] : fr[2];
            else f = fr[0];
            sr.sprite = f;
            sr.flipX = facing < 0;
            go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y - Half.y), 0);
            go.transform.localScale = new Vector3((1f + squash) * s.bodyScale, (1f - squash) * s.bodyScale, 1f);
            sr.enabled = invuln <= 0f || ((int)(Time.time * 16f)) % 2 == 0;
            if (stunT > 0f) go.transform.position += new Vector3(Random.Range(-1, 2) / 16f, 0f, 0f);   // shocked: twitching
            sr.color = stunT > 0f ? (((int)(Time.time * 30f)) % 2 == 0 ? new Color(0.75f, 0.95f, 1f) : Color.white)
                : burnT > 0f ? Color.Lerp(new Color(1f, 0.75f, 0.45f), new Color(1f, 0.45f, 0.2f), 0.5f + 0.5f * Mathf.Sin(Time.time * 30f))
                : poisonT > 0f ? Color.Lerp(Color.white, new Color(0.55f, 1f, 0.45f), 0.6f)
                : aegisT > 0f ? Color.Lerp(Color.white, new Color(1f, 0.85f, 0.3f), 0.5f + 0.5f * Mathf.Sin(Time.time * 40f)) : superT > 0f || mode == Mode.Dig ? new Color(1f, 0.9f, 0.6f) : mode == Mode.Quick ? new Color(0.8f, 0.7f, 1f) : Color.white;
            if (Camouflaged) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0.18f + 0.08f * Mathf.Sin(Time.time * 9f));   // CAMOUFLAGE: barely there
            RenderWeapon();
            if (Camouflaged) toolSr.color = new Color(1f, 1f, 1f, 0.2f);
            RenderLooks();
            RenderTentacles();
            if (mode != Mode.Grapple && mode != Mode.Swing) ropeSr.enabled = false;
        }

        // the main menu keeps a stand-in player at the camp (lots of systems want one): invisible and inert
        public void Hide()
        {
            if (go != null) go.SetActive(false);
            if (tool != null) tool.gameObject.SetActive(false);
            if (rope != null) rope.gameObject.SetActive(false);
            if (arrow != null) arrow.gameObject.SetActive(false);
            DestroyLooks();
            KillPets();
        }

        // stepping out of the character lineup: a little hop and a puff
        public void PopIn()
        {
            squash = -0.35f;
            vel = new Vector2(0f, 7f);
            invuln = 1f;
            G.fx.Burst(pos, 18, new Color(1f, 0.85f, 0.4f), 5f, 0.4f, 5f);
            G.fx.Smoke(pos - new Vector2(0f, Half.y), 4, 0.9f);
            G.sfx.Play("jump", 0.7f, 1.1f);
        }

        public override void Kill()
        {
            base.Kill();
            if (tool != null) Object.Destroy(tool.gameObject);
            if (rope != null) Object.Destroy(rope.gameObject);
            if (arrow != null) Object.Destroy(arrow.gameObject);
            DestroyGunBits();
            DestroyLooks();
            DestroyTentacles();
            KillPets();
            foreach (var o in orbs) if (o != null) Object.Destroy(o.gameObject);
        }
    }
}
