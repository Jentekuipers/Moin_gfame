using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Run-changing trinkets (Isaac-style): shot colours, flight (levitation, jetpack, wings) and the things
    // you visibly grow or wear (wings, horns, a halo, hats...).
    public partial class Player
    {
        // ================================================================== shots

        // Every damage type a shot carries tints it, whether it comes from a trinket ("all shots are poison") or from the
        // weapon's own modifiers (POISON, FIRE, FROST...). Lasers too: a poison laser is green. No effects: its own colour.
        public Color ShotTint(Color c, Weapon w = null)
        {
            bool Has(Mod m) => w != null && w.Has(m);
            Color acc = Color.black;
            int n = 0;
            if (s.shotFire || Has(Mod.Fire) || (w != null && w.def.flame)) { acc += new Color(1f, 0.5f, 0.12f); n++; }
            if (s.shotPoison || Has(Mod.Poison)) { acc += new Color(0.5f, 1f, 0.3f); n++; }
            if (s.shotFrost || Has(Mod.Frost)) { acc += new Color(0.55f, 0.9f, 1f); n++; }
            if (s.shotExplode || Has(Mod.Explosive)) { acc += new Color(1f, 0.35f, 0.15f); n++; }
            if (s.shotHoming || Has(Mod.Homing)) { acc += new Color(0.75f, 0.45f, 1f); n++; }
            if (Has(Mod.Chain)) { acc += Mods.Col(Mod.Chain); n++; }
            if (n == 0) return c;
            acc /= n;
            acc.a = 1f;
            return acc;
        }

        // ================================================================== flight

        float flapT, jetFuel = JetFuelMax, jetSfxT, levT;
        bool jetting;
        const float JetFuelMax = 1.5f;

        // LEVITATION: float towards wherever you hold. Let go and you drift on for a moment before you
        // come to a stop in mid-air. Jump still gives you a little hop upwards, even while floating.
        void Levitate(float dt, float mx, bool up, bool down)
        {
            var want = new Vector2(mx, up ? 1f : down ? -1f : 0f);
            if (want.sqrMagnitude > 1f) want.Normalize();
            // brisker than walking, and every other way of flying you own adds a little on top
            int flight = (s.jetpack ? 1 : 0) + (s.glide > 0 ? 1 : 0) + (s.airJumps > 0 ? 1 : 0) + (s.levitate && TentacleGrip ? 1 : 0);
            float spd = 8.6f * Speed * (1f + 0.12f * flight) * (1f - FireSlow * 0.5f);
            if (want.sqrMagnitude > 0.01f) vel = Vector2.MoveTowards(vel, want * spd, 38f * dt);
            else vel = Vector2.MoveTowards(vel, Vector2.zero, 4.5f * dt);   // the momentum lingers
            if (grounded && up) { vel.y = Mathf.Max(vel.y, 3.5f); grounded = false; }
            if (GameInput.Buffered(Btn.Jump))
            {
                GameInput.Consume(Btn.Jump);
                vel.y = Mathf.Min(Mathf.Max(vel.y, 0f) + 7.5f, 12f);
                squash = -0.15f;
                G.fx.Burst(pos - new Vector2(0f, Half.y), 8, new Color(0.75f, 0.55f, 1f), 3.5f, 0.35f, 0f);
                G.sfx.Play("jump", 0.4f, 1.35f);
            }
            airJumps = s.airJumps;
            coyote = 0f;
            levT += dt;
            if (s.levitate && Random.value < 0.3f) G.fx.Burst(pos - new Vector2(Random.Range(-0.3f, 0.3f), Half.y), 1, new Color(0.75f, 0.55f, 1f, 0.8f), 0.8f, 0.5f, 3f, 1f, false);
        }

        // JETPACK: once a jump has peaked (or you're falling), hold jump to thrust upwards until the fuel runs out.
        bool jetBlocked;
        void Jetpack(float dt)
        {
            if (grounded) { jetFuel = Mathf.Min(JetFuelMax, jetFuel + dt * 2.5f); jetting = false; jetBlocked = false; return; }
            bool hold = GameInput.Held(Btn.Jump);
            // a fresh press in the air spends a mid-air jump first (CLOUD STEP...); only once those are gone does a press fire the burners
            if (GameInput.Pressed(Btn.Jump) && coyote <= 0f && (airJumps > 0 || s.wings)) { jetBlocked = true; jetting = false; }
            if (!hold) jetBlocked = false;
            if (jetBlocked) return;
            if (hold && jetFuel > 0f && (jetting || vel.y < 2f))
            {
                if (!jetting) G.sfx.Play("rocket", 0.35f, 1.6f);
                jetting = true;
                jetFuel -= dt;
                // falling: the burners first have to beat gravity (~78/s), so they fire harder until you're rising again;
                // once rising it's the same gentle push as before (that part already felt right)
                vel.y = U.Approach(vel.y, 8f, (vel.y < 0f ? 175f : vel.y < 2f ? 100f : 75f) * dt);
                Vector2 nozzle = pos + new Vector2(-0.3f * facing * s.bodyScale, -0.1f);
                G.fx.Burst(nozzle, 2, new Color(1f, Random.Range(0.5f, 0.9f), 0.2f), 3f, 0.25f, -8f, 1f, false);
                if (Random.value < 0.25f) G.fx.Smoke(nozzle + Vector2.down * 0.3f, 1, 0.5f);
                jetSfxT -= dt;
                if (jetSfxT <= 0f) { jetSfxT = 0.12f; G.sfx.Play("flame", 0.18f, 0.7f, 0.1f); }
                // ROCKET MAN: the exhaust sets whatever is below you alight
                if (s.rocketman && Random.value < 0.5f) G.Spawn(new Spark(nozzle, new Vector2(Random.Range(-2f, 2f), -12f), 0.5f));
                if (jetFuel <= 0f) { G.sfx.Play("deny", 0.3f, 0.8f); G.fx.Smoke(nozzle, 4, 0.7f); }
            }
            else jetting = false;
        }

        // ================================================================== looks

        class LookPart { public string key; public Transform t; public SpriteRenderer r; }
        readonly List<LookPart> looks = new List<LookPart>();
        readonly List<string> lookKeys = new List<string>();
        bool looksBuilt;

        // What your trinkets and transformations visibly put on you. Rebuilt whenever they change.
        public void RebuildLooks()
        {
            DestroyLooks();
            looksBuilt = true;
            lookKeys.Clear();
            foreach (var k in skillOrder) if (!string.IsNullOrEmpty(k.look) && !lookKeys.Contains(k.look)) lookKeys.Add(k.look);
            foreach (var c in comboOrder) if (!string.IsNullOrEmpty(c.look) && !lookKeys.Contains(c.look)) lookKeys.Add(c.look);
            if (lookKeys.Contains("bighorns")) lookKeys.Remove("horns");   // the BEHEMOTH outgrows the ogre's horns
            if (go == null) return;
            SyncPets();
            foreach (var key in lookKeys)
            {
                var spr = Art.Look(key);
                if (spr == null) continue;   // particle-only looks (the plague flies)
                var t = Child("Look_" + key, spr, Art.LookBehind(key) ? 19 : 22, out var r);
                looks.Add(new LookPart { key = key, t = t, r = r });
            }
        }

        // ================================================================== pets

        readonly List<Pet> pets = new List<Pet>();

        void SyncPets()
        {
            pets.RemoveAll(x => x.dead);
            foreach (var k in skillOrder)
            {
                if (string.IsNullOrEmpty(k.pet) || pets.Exists(x => x.kind == k.pet)) continue;
                var pet = Pets.Make(k.pet, this);
                if (pet == null) continue;
                pets.Add(pet);
                G.Spawn(pet);
            }
        }

        void KillPets()
        {
            foreach (var x in pets) x.Kill();
            pets.Clear();
        }

        void DestroyLooks()
        {
            foreach (var l in looks) if (l.t != null) Object.Destroy(l.t.gameObject);
            looks.Clear();
        }

        void RenderLooks()
        {
            if (!looksBuilt) RebuildLooks();
            flapT -= Time.deltaTime;
            float bs = s.bodyScale, tm = Time.time;
            Vector3 feet = go.transform.position;
            bool vis = sr.enabled && mode != Mode.Dead;
            foreach (var l in looks)
            {
                Vector2 off = Vector2.zero;
                float sx = 1f, sy = 1f;
                Color col = Color.white;
                switch (l.key)
                {
                    case "wings":
                        off = new Vector2(-0.32f * facing, 0.5f);
                        // they beat hard on every air jump and flutter while you're in the air
                        float beat = flapT > 0f ? Mathf.Abs(Mathf.Sin(flapT * 30f)) : !grounded ? Mathf.Abs(Mathf.Sin(tm * 9f)) * 0.5f : 0.1f;
                        sy = 1f - 0.45f * beat;
                        break;
                    case "jetpack": off = new Vector2(-0.3f * facing, 0.38f); break;
                    case "halo":
                        off = new Vector2(0.04f * facing, 1.02f + Mathf.Sin(tm * 3f) * 0.03f);
                        col = new Color(1f, 1f, 1f, 0.85f + 0.15f * Mathf.Sin(tm * 5f));
                        break;
                    case "runes":
                        off = new Vector2(0f, -0.06f + Mathf.Sin(tm * 4f) * 0.02f);
                        col = new Color(1f, 1f, 1f, grounded ? 0.35f : 0.6f + 0.4f * Mathf.Sin(tm * 8f));
                        break;
                    case "horns": off = new Vector2(0.03f * facing, 0.86f); break;
                    case "bighorns": off = new Vector2(0.03f * facing, 0.9f); break;
                    case "flamecrown":
                        off = new Vector2(0.03f * facing, 0.92f);
                        sy = 0.85f + 0.2f * Mathf.Abs(Mathf.Sin(tm * 14f));
                        break;
                    case "bombhat": off = new Vector2(0.03f * facing, 0.9f); break;
                    case "cowboy": off = new Vector2(0.03f * facing, 0.88f); break;
                    case "headband": off = new Vector2(0.02f * facing, 0.74f); break;
                    case "visor":
                        off = new Vector2(0.1f * facing, 0.64f);
                        col = new Color(1f, 1f, 1f, 0.8f + 0.2f * Mathf.Sin(tm * 10f));
                        break;
                    case "buckler":
                        // the shield floats on the side you aim at
                        off = new Vector2(0f, Half.y) + aimDir * 0.85f;
                        l.t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg);
                        break;
                    case "ward":
                        off = new Vector2(0f, Half.y);
                        sx = sy = 1.6f + Mathf.Sin(tm * 4f) * 0.05f;
                        col = mantleUp ? new Color(1f, 0.95f, 0.6f, 0.3f + 0.1f * Mathf.Sin(tm * 6f)) : new Color(1f, 1f, 1f, 0f);
                        break;
                }
                off *= bs;
                l.t.position = new Vector3(U.Snap(feet.x + off.x), U.Snap(feet.y + off.y), 0f);
                l.t.localScale = new Vector3(sx * bs * (1f + squash), sy * bs * (1f - squash), 1f);
                l.r.flipX = facing < 0 && l.key != "buckler";
                l.r.color = col;
                l.r.enabled = vis;
            }
            if (!vis) return;
            if (lookKeys.Contains("flies") && Random.value < 0.3f)
                G.fx.Burst(pos + Random.insideUnitCircle * 0.7f * bs, 1, new Color(0.45f, 0.85f, 0.25f), 0.7f, 0.5f, 0f, 0.8f, false);
            if (lookKeys.Contains("flamecrown") && Random.value < 0.25f)
                G.fx.Burst(pos + new Vector2(0f, Half.y + 0.3f) * bs, 1, new Color(1f, Random.Range(0.5f, 0.9f), 0.2f), 1f, 0.4f, -6f, 1f, false);
        }

        // ================================================================== luck

        // LUCK raises every chance in the game a little (procs, drops, revives...). Bad luck lowers them.
        public ItemStack SelectedItem => items.Count == 0 ? null : items[Mathf.Clamp(sel, 0, items.Count - 1)];
        public void SpendSelectedItem()
        {
            var st = SelectedItem;
            if (st == null) return;
            st.count--;
            if (st.count <= 0) { items.Remove(st); if (sel >= items.Count) sel = 0; }
        }

        public float Lucky(float chance) => Mathf.Clamp(chance * (1f + 0.12f * Mathf.Clamp(s.luck, -5f, 15f)) * (s.loadedDie ? 2f : 1f), 0f, 0.95f);   // LOADED DIE: doubled

        // ================================================================== items that tick

        int mantleBiome = -1;
        bool mantleUp;
        float electrifyT, shockCd;
        public float stunT;

        void TickItems(float dt)
        {
            shockCd -= dt;
            // WARDSTONE: a holy ward that soaks the first hit in every biome
            if (s.mantle)
            {
                int bi = Biomes.Index(Depth);
                if (bi != mantleBiome)
                {
                    mantleBiome = bi;
                    if (!mantleUp) { mantleUp = true; G.Popup(pos + Vector2.up * 1.3f, "WARD RESTORED", new Color(1f, 0.95f, 0.6f)); G.sfx.Play("ready", 0.5f, 0.8f); }
                }
            }
            // BUCKLER: a shield on the side you aim that swallows enemy shots
            if (s.buckler && mode != Mode.Dead)
            {
                Vector2 c = pos + aimDir * 0.85f;
                foreach (var e in G.ents)
                {
                    if (e.dead || !(e is Bullet b) || b.team == Team.Player) continue;
                    if ((b.pos - c).sqrMagnitude > 0.5f * 0.5f) continue;
                    b.Kill();
                    G.fx.Sparks(b.pos, new Color(0.85f, 0.9f, 1f));
                    G.sfx.Play("clank", 0.4f, 1.5f, 0.1f);
                    kick = Mathf.Max(kick, 0.05f);
                }
            }
            // STORM HEART: wading into water charges it (it can't hurt you, it hurts everything else)
            if (s.electrify && Wd.FluidAtPoint(pos) == Fluids.Water)
            {
                electrifyT -= dt;
                if (electrifyT <= 0f) { electrifyT = 1.1f; Electric.Zap(pos, 0.6f, true); }
            }
        }

        // the WARDSTONE soaks a hit: true if it did
        bool UseMantle()
        {
            if (!s.mantle || !mantleUp) return false;
            mantleUp = false;
            invuln = 1f;
            G.Popup(pos + Vector2.up * 1.2f, "WARDED!", new Color(1f, 0.95f, 0.6f));
            G.sfx.Play("reflect", 0.8f, 0.6f);
            G.sfx.Play("holy", 0.4f, 1.4f);
            G.fx.Ring(pos, 2f, new Color(1f, 0.95f, 0.6f));
            G.fx.Burst(pos, 24, new Color(1f, 0.95f, 0.7f), 6f, 0.5f, 0f);
            G.HitStop(0.05f);
            return true;
        }

        // ================================================================== electricity

        // A jolt: some damage and a short stun. Rubber boots (STORM HEART) shrug it off.
        public void Shock(int dmg)
        {
            if (mode == Mode.Dead || s.shockImmune || shockCd > 0f || Invulnerable) return;
            shockCd = 0.9f;
            stunT = 0.45f;
            G.sfx.Play("shock", 0.8f);
            G.Popup(pos + Vector2.up, "ZAP!", new Color(0.7f, 0.95f, 1f));
            G.fx.Burst(pos, 16, new Color(0.85f, 0.97f, 1f), 6f, 0.25f, 0f);
            G.cam.Shake(0.25f);
            DotDamage(dmg, new Color(0.7f, 0.95f, 1f));
        }

        // ================================================================== death defies

        // Cheating death, in order: the full revives first, then the twin souls, then the gamble.
        bool TryRevive()
        {
            if (s.phoenix > 0)
            {
                s.phoenix--;
                Revive(s.maxHp, "REBORN!", "THE PHOENIX FEATHER CRUMBLES", new Color(1f, 0.6f, 0.2f));
                if (Combo("firebird")) G.Explode(pos, 3.2f, 8, true);
                return true;
            }
            if (s.ankh > 0) { s.ankh--; Revive(s.maxHp, "RISEN!", "THE ANKH SHATTERS", new Color(1f, 0.85f, 0.35f)); return true; }
            if (s.twinLives > 0)
            {
                s.twinLives--;
                Revive(10, "BACK FROM THE DEAD", s.twinLives > 0 ? "ONE LIFE LEFT" : "THE LAST ONE", new Color(0.75f, 0.85f, 1f));
                return true;
            }
            if (s.gambleRevive && Chance.Roll("rabbit", Lucky(0.25f))) { Revive(50, "LUCKY!", "THE RABBIT'S FOOT HOLDS", new Color(0.6f, 1f, 0.5f)); return true; }
            return false;
        }

        void Revive(int hp, string title, string sub, Color c)
        {
            s.hp = Mathf.Clamp(hp, 1, s.maxHp);
            invuln = 3f;   // a moment of grace: nothing can touch you
            burnT = 0f; poisonT = 0f; stunT = 0f;
            G.hud.Banner(title, sub, c);
            G.sfx.Play("holy", 1f);
            G.hud.ScreenFlash(0.5f);
            G.fx.Burst(pos, 40, c, 9f, 0.9f, -6f);
            G.fx.Ring(pos, 3f, c);
            G.lighting.Flash(pos, 9f, 0.8f);
            G.HitStop(0.15f);
        }

        // ================================================================== pets that come and go

        int temps;
        public void TempPetDied() => temps = Mathf.Max(0, temps - 1);

        // SPIRIT LANTERN: a fallen foe sometimes leaves a friendly spirit that fights until it has dealt its share
        public void MaybeDropPet(Vector2 at)
        {
            if (!s.dropPets || temps >= 4 || Random.value >= Lucky(0.12f)) return;
            string[] kinds = { "bat", "wisp", "beetle", "orb", "spore", "frost" };
            var pet = Pets.Make(kinds[Random.Range(0, kinds.Length)], this);
            if (pet == null) return;
            pet.MakeTemporary(at, Dmg(10f) * s.petMul);
            temps++;
            G.Spawn(pet);
        }

        int flies;
        public void FlyDied() => flies = Mathf.Max(0, flies - 1);

        // FLY SWARM: a little fly hatches from every hit (up to a dozen at once)
        public void SpawnFly(Vector2 at)
        {
            if (flies >= 12) return;
            flies++;
            var f = new PetFly(this);
            f.MakeTemporary(at, Dmg(2f) * s.petMul, 6f);
            G.Spawn(f);
        }
    }
}
