using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // The player's weapons: aiming at the cursor, guns that mine, melee slashes, the railgun charge,
    // weapon slots and modifiers, the HUNTER mark and the QUICKSTEP dash.
    public partial class Player
    {
        public readonly Weapon[] slots = new Weapon[2];
        public int cur;
        public Weapon weapon => slots[cur];
        public AttackDef attack => slots[cur]?.def;

        public Vector2 aim, aimDir = Vector2.right;
        public float kick, chargeT;
        float fireCd, slowT, burstT, ghostT, huntT, buffT, sightT;

        // ---------------- reloading / overheating
        public bool reloading;
        bool reloadEmpty;
        float reloadT, reloadDur, lastShotT;
        public float nextShotMul = 1f;
        public float nextSpeedMul = 1f;   // a bow's arrow flies as fast as the string was drawn
        public bool nextShotCrit;
        // PRESSURE CHAMBER: hold fire to charge. chargedMul scales the next shot's size, damage and blast (up to 2.5x);
        // automatic guns let go a wild burst instead
        public float chargedMul = 1f;
        bool wildBurst;
        float burstGapNow, heatWarnT;
        int spearVolley, splitStep;
        float splitLast = -9f;

        public int MagSize(Weapon w) => w == null || w.def.mag <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(w.def.mag * s.magMul));
        public float ReloadFrac
        {
            get
            {
                var w = weapon;
                if (!reloading || w == null) return 0f;
                if (w.def.perShell) return MagSize(w) > 0 ? w.ammo / (float)MagSize(w) : 0f;
                return reloadDur > 0f ? 1f - reloadT / reloadDur : 0f;
            }
        }
        // GHOST RELOAD: untouchable through any reload (and while an energy weapon vents)
        public bool ReloadGhosting => s.reloadGhost && (reloading || (weapon != null && weapon.overheated));

        void StartReload()
        {
            var w = weapon;
            if (w == null || reloading || w.def.UsesHeat || MagSize(w) <= 0 || w.ammo >= MagSize(w)) return;
            var d = w.def;
            reloading = true;
            reloadEmpty = w.ammo <= 0;
            chargeT = 0f; burstLeft = 0;
            PerkReload();
            float mul = s.reloadMul * (Combo("quickdraw") ? 0.9f : 1f) * (Cursed(Curse.Jamming) ? 2f : 1f);
            reloadDur = reloadT = d.reload * mul;
            // shell-by-shell guns first have to open up: the first shell takes much longer than the rest
            if (d.perShell) reloadT = reloadDur * 1.6f;
            if (!d.perShell) G.sfx.Play(d.reloadSfx, 0.55f, d.reloadPitch * Random.Range(0.95f, 1.05f), 0f);
            // the old magazine / spent casings / smoke drop out
            Vector2 at = GunPivot + aimDir * 0.2f;
            switch (d.debris)
            {
                case Debris.Mag: G.fx.Magazine(at, new Vector2(-facing * 1.5f, 2f)); break;
                case Debris.Casings: for (int i = 0; i < Mathf.Max(1, d.mag - Mathf.Max(0, w.ammo)); i++) G.fx.Casing(at, new Vector2(Random.Range(-2f, 2f), Random.Range(1f, 4f))); break;
                case Debris.Smoke: G.fx.Smoke(at, 4, 0.8f); break;
            }
            if (s.reloadGhost) G.fx.Burst(pos, 10, new Color(0.7f, 0.6f, 1f), 3f, 0.4f, 0f);
        }

        void TickReload(float dt)
        {
            var w = weapon;
            var d = w.def;
            if (s.reloadGhost && Random.value < 0.3f) G.fx.Ghost(sr.sprite, go.transform.position, sr.flipX, new Color(0.6f, 0.5f, 1f, 0.4f));
            reloadT -= dt;
            if (reloadT > 0f) return;
            if (d.perShell)
            {
                w.ammo++;
                G.sfx.Play(d.reloadSfx, 0.45f, d.reloadPitch * Random.Range(0.92f, 1.08f), 0f);
                if (w.ammo < MagSize(w)) { reloadT = reloadDur; return; }
                G.sfx.Play("rl_pump", 0.5f, d.reloadPitch, 0.05f);
            }
            else w.ammo = MagSize(w);
            reloading = false;
            if (s.tactical) { nextShotMul = 2f; nextShotCrit = Combo("doubletap"); }
            G.hud.WeaponKick();
        }

        void CancelReload() { reloading = false; }

        // Spends ammo / adds heat for one trigger pull. False when the gun can't fire right now.
        bool Spend(Weapon w)
        {
            var d = w.def;
            if (d.UsesHeat)
            {
                if (w.overheated) return false;
                // every extra shot a pull lets loose (barrels, TRIPLE, mirror) heats the gun as much as one more pull
                int perPull = (w.Has(Mod.Triple) ? 3 : 1) * Mathf.Max(1, s.multiShot) * (s.freeDouble ? 2 : 1);
                w.heat += d.heatPerShot * perPull * s.heatMul * (Cursed(Curse.Jamming) ? 1.5f : 1f);
                if (w.heat >= 1f)
                {
                    w.heat = 1f;
                    w.overheated = true;
                    PerkOverheat();
                    G.sfx.Play("vent", 0.7f, d.reloadPitch, 0.05f);
                    G.fx.Smoke(Muzzle, 8, 1f);
                    G.Popup(pos + Vector2.up * 1.1f, "OVERHEATED", new Color(1f, 0.5f, 0.3f));
                }
                return true;
            }
            int mag = MagSize(w);
            if (mag <= 0) return true;
            if (w.ammo <= 0) return false;
            w.ammo--;
            return true;
        }
        int burstLeft, swingSide = 1;
        Vector2 quickDir;
        public Enemy huntMark;

        public enum Buff { None, Speed, Damage, Rate }
        Buff buff;
        public Buff CurrentBuff => buffT > 0f ? buff : Buff.None;
        public float BuffTime => Mathf.Max(0f, buffT);
        public bool BuffActive(Buff b) => buff == b && buffT > 0f;

        public float FireSlow => slowT > 0f && attack != null ? attack.slow : 0f;
        public float ChargeFrac01 => attack == null ? 0f : attack.type == WType.Rail ? Mathf.Clamp01(chargeT / attack.charge)
            : attack.bow ? Mathf.Clamp01(chargeT / BowDraw(weapon))
            : ChargesShots ? Mathf.Clamp01(chargeT / ChargeShotTime(weapon)) : 0f;
        bool ChargesShots => s.chargeShot && attack != null && attack.type == WType.Gun && !attack.bow;
        float BowDraw(Weapon w) => Mathf.Max(0.2f, 0.75f * DelayMul(w) / SwingRate);
        static bool Automatic(AttackDef d) => d.delay < 0.13f && d.burst <= 1;
        // a charge takes a little while, less for fast-firing guns and a high fire rate
        float ChargeShotTime(Weapon w) => w == null ? 1f : Mathf.Max(0.25f, (0.5f + w.def.delay * DelayMul(w) * 0.8f) / SwingRate);

        Vector2 GunPivot => pos + new Vector2(0f, 0.04f);
        // the tip of the gun as drawn (long guns like the minigun flash at their real barrel end)
        Vector2 Muzzle => GunPivot + aimDir * (attack != null && attack.Melee ? 0.5f
            : attack != null && attack.icon != null ? Mathf.Clamp(0.36f + attack.icon.bounds.extents.x, 0.6f, 1.2f) : 0.78f);

        Transform sight, glowT;
        SpriteRenderer sightSr, glowSr;

        public bool Holds(string id) => (slots[0] != null && slots[0].def.id == id) || (slots[1] != null && slots[1].def.id == id);

        public static float DelayMul(Weapon w)
        {
            if (w == null) return 1f;
            float m = 1f;
            if (w.Has(Mod.Triple)) m *= w.def.delay >= 0.6f ? 1.4f : w.def.delay < 0.12f ? 3f : 2f;
            if (w.Has(Mod.HighCaliber)) m *= 2.2f;
            // DOUBLE / TRIPLE / QUAD BARREL: each extra copy of a shot costs a third of your fire rate (GUNSLINGER: a sixth)
            var p = Game.I != null ? Game.I.player : null;
            if (p != null && p.s.multiShot > 1) m *= 1f + (p.s.multiShot - 1) * 0.33f * p.s.multiPenalty;
            return m;
        }

        Vector2 ThrowVel() => aimDir * 10f + Vector2.up * 3.5f + vel * 0.4f;

        void UpdateAim()
        {
            aim = G.aim;
            Vector2 d = aim - GunPivot;
            if (d.sqrMagnitude > 0.04f) aimDir = d.normalized;
            kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 3.2f);
        }

        // ================================================================== firing

        bool attackLock = true;   // the click that picked your digger isn't a shot: nothing fires until the button is let go

        void TickWeapon(float dt)
        {
            fireCd -= dt; slowT -= dt; lastShotT += dt;
            if (attackLock) { if (GameInput.Held(Btn.Attack)) return; attackLock = false; }
            var w = weapon;
            if (w == null) return;
            var d = w.def;
            if (w.ammo < 0) w.ammo = MagSize(w);
            // fed to the Fuse Altar: it fires the consumable now, endlessly, at a pace set by its own fire rate
            if (w.ammoItem != null)
            {
                reloading = false; chargeT = 0f; burstLeft = 0;
                if (GameInput.Held(Btn.Attack) && fireCd <= 0f)
                {
                    float baseCd = (d.delay + (d.type == WType.Rail ? d.charge : 0f) + d.burstGap * Mathf.Max(0, d.burst - 1)) * DelayMul(w) / SwingRate;
                    fireCd = Mathf.Max(0.2f, baseCd * ItemEffects.RateFactor(w.ammoItem));
                    FireItem(w);
                }
                return;
            }
            if (d.UsesHeat)
            {
                float cool = d.coolRate * s.coolMul * (Combo("cryocore") ? 1.5f : 1f) * (lastShotT > 0.25f ? 1f : 0.35f) * (w.overheated ? 0.55f : 1f);   // an overheated gun vents slowly: each gun's penalty scales with its own cooling rate
                w.heat = Mathf.Max(0f, w.heat - cool * dt);
                // the last quarter of the heat bar: warning pips that speed up and climb in pitch
                if (!w.overheated && w.heat > 0.75f && lastShotT < 0.3f)
                {
                    float k = Mathf.Clamp01((w.heat - 0.75f) / 0.25f);
                    heatWarnT -= dt;
                    if (heatWarnT <= 0f) { heatWarnT = Mathf.Lerp(0.22f, 0.05f, k); G.sfx.Play("heatwarn", 0.18f + 0.3f * k, Mathf.Lerp(0.9f, 1.9f, k), 0f); }
                }
                else heatWarnT = 0f;
                if (w.overheated)
                {
                    if (Random.value < 0.3f) G.fx.Smoke(Muzzle, 1, 0.5f);
                    if (s.reloadGhost && Random.value < 0.3f) G.fx.Ghost(sr.sprite, go.transform.position, sr.flipX, new Color(0.6f, 0.5f, 1f, 0.4f));
                    if (w.heat <= 0.05f) { w.overheated = false; G.sfx.Play("reload", 0.4f, 1.3f); }
                }
            }
            if (GameInput.Take(Btn.Reload)) StartReload();
            // clicking with an empty magazine reloads (every gun with a magazine)
            if (!reloading && MagSize(w) > 0 && w.ammo <= 0 && GameInput.Pressed(Btn.Attack)) { G.sfx.Play("deny", 0.25f, 1.8f); StartReload(); }
            if (reloading)
            {
                // shell-by-shell weapons can fire whatever is loaded, which stops the reload
                bool interrupt = d.perShell && w.ammo > 0 && GameInput.Held(Btn.Attack) && fireCd <= 0f;
                if (interrupt)
                {
                    // cutting a shell reload short means racking the action closed first
                    CancelReload();
                    fireCd = Mathf.Max(fireCd, 0.3f);
                    G.sfx.Play("rl_pump", 0.5f, d.reloadPitch * 1.1f, 0.05f);
                }
                else { TickReload(dt); return; }
            }
            if (MagSize(w) > 0 && w.ammo <= 0 && burstLeft <= 0) { StartReload(); return; }

            if (burstLeft > 0)
            {
                burstT -= dt;
                if (burstT <= 0f)
                {
                    if (Spend(w) || wildBurst) FireOnce(false); else burstLeft = 0;   // a charged burst always fires in full (free once the mag is dry)
                    burstLeft--; burstT = burstGapNow > 0f ? burstGapNow : d.burstGap;
                }
                if (burstLeft <= 0) { wildBurst = false; burstGapNow = 0f; }
                return;
            }

            bool held = GameInput.Held(Btn.Attack);
            if (d.type == WType.Rail)
            {
                if (held && fireCd <= 0f && !w.overheated)
                {
                    float before = chargeT;
                    chargeT = Mathf.Min(chargeT + dt * SwingRate, d.charge);
                    if (chargeT >= d.charge && before < d.charge) { G.sfx.Play("charge", 0.7f, 1.7f, 0f); G.fx.Ring(Muzzle, 0.6f, d.col); }
                    if (Random.value < 0.6f) G.fx.Burst(Muzzle + Random.insideUnitCircle * 0.5f, 1, d.col, 1.5f, 0.2f, 0f, 1f, false);
                    if (sightT <= 0f) { sightT = 0.12f; G.sfx.Play("blip", 0.12f, 1f + chargeT * 2f, 0f); }
                    sightT -= dt;
                    slowT = 0.1f;
                }
                else if (!held && chargeT > 0f)
                {
                    float power = Mathf.Clamp01(chargeT / d.charge);
                    chargeT = 0f;
                    if (power >= 0.25f && Spend(w))
                    {
                        RailShot(power, false);
                        fireCd = d.delay * DelayMul(w) / SwingRate;
                        TryEcho();
                    }
                }
                return;
            }

            if (d.bow)
            {
                // BOWS: hold to draw, let go to loose. Your fire rate draws the string faster.
                float need = BowDraw(w);
                if (held && fireCd <= 0f)
                {
                    float before = chargeT;
                    chargeT = Mathf.Min(chargeT + dt, need);
                    if (chargeT >= need && before < need) { G.sfx.Play("charge", 0.4f, 1.9f, 0f); G.fx.Ring(Muzzle, 0.5f, d.col); }
                    slowT = 0.1f;
                }
                else if (!held && chargeT > 0f)
                {
                    float f = chargeT / need;
                    chargeT = 0f;
                    if (f < 0.15f) return;   // a twitch of the string: nothing
                    lastShotT = 0f;
                    nextShotMul *= Mathf.Lerp(0.3f, 1.35f, f);
                    nextSpeedMul = Mathf.Clamp(f, 0.01f, 1f);   // 1%..100% of the arrow's speed
                    FireOnce(false);
                    fireCd = 0.12f;
                    TryEcho();
                }
                return;
            }

            if (ChargesShots)
            {
                float need = ChargeShotTime(w);
                bool canFire = fireCd <= 0f && !w.overheated && (MagSize(w) <= 0 || w.ammo > 0);
                if (held && canFire)
                {
                    float before = chargeT;
                    chargeT = Mathf.Min(chargeT + dt, need);
                    float f = chargeT / need;
                    if (chargeT >= need && before < need) { G.sfx.Play("charge", 0.6f, 1.5f, 0f); G.fx.Ring(Muzzle, 0.7f, d.col); }
                    if (Random.value < 0.3f + f * 0.6f) G.fx.Burst(Muzzle + Random.insideUnitCircle * (0.6f - f * 0.4f), 1, d.col, 1.5f, 0.2f, 0f, 0.6f + f, false);
                    sightT -= dt;
                    if (sightT <= 0f && f < 1f) { sightT = 0.1f; G.sfx.Play("blip", 0.1f, 0.8f + f * 1.6f, 0f); }
                    slowT = 0.1f;
                }
                else if (!held && chargeT > 0f)
                {
                    float f = chargeT / need;
                    chargeT = 0f;
                    ReleaseCharged(w, f);
                }
                return;
            }

            if (held && fireCd <= 0f)
            {
                if (!d.Melee && !Spend(w))
                {
                    if (w.overheated && GameInput.Pressed(Btn.Attack)) G.sfx.Play("deny", 0.25f, 1.6f);
                    return;
                }
                lastShotT = 0f;
                fireCd = d.delay * DelayMul(w) / SwingRate;
                if (d.Melee) MeleeSwing(false);
                else
                {
                    FireOnce(false);
                    if (d.burst > 1)
                    {
                        burstLeft = d.burst - 1; burstT = d.burstGap;
                        // a burst gun always fires clean bursts: fire rate shortens the pause, never merges bursts
                        fireCd = Mathf.Max(fireCd, (d.burst - 1) * d.burstGap + 0.12f);
                    }
                }
                TryEcho();
            }
        }

        // Let go of a charge: a tap is a normal shot; a full charge is 2.5x the shot. Automatic guns spit a wild burst
        // of up to half their magazine, longer the longer you held.
        void ReleaseCharged(Weapon w, float f)
        {
            var d = w.def;
            if (!Spend(w)) return;
            lastShotT = 0f;
            fireCd = d.delay * DelayMul(w) / SwingRate;
            if (d.spear)
            {
                // PRESSURE CHAMBER spears: the longer the charge, the more spears leave your hand at once
                spearVolley = f >= 0.25f ? Mathf.RoundToInt(f * 3f) : 0;
                FireOnce(false);
                if (spearVolley > 0) { G.fx.Ring(Muzzle, 0.4f + 0.2f * spearVolley, d.col); G.cam.Shake(0.1f + 0.05f * spearVolley); }
                spearVolley = 0;
            }
            else if (Automatic(d) && f >= 0.15f)
            {
                int mag = MagSize(w) > 0 ? MagSize(w) : 30;
                int n = Mathf.Max(2, Mathf.RoundToInt(mag * 0.5f * f));
                wildBurst = true;
                burstGapNow = Mathf.Max(0.025f, d.delay * 0.55f);
                FireOnce(false);
                burstLeft = n - 1; burstT = burstGapNow;
                G.cam.Shake(0.15f + f * 0.2f);
            }
            else
            {
                chargedMul = f >= 0.25f ? 1f + 1.5f * f : 1f;
                FireOnce(false);
                if (chargedMul > 1.4f) { G.HitStop(0.03f * chargedMul); G.fx.Ring(Muzzle, 0.5f * chargedMul, d.col); }
                chargedMul = 1f;
                if (d.burst > 1) { burstLeft = d.burst - 1; burstT = d.burstGap; fireCd = Mathf.Max(fireCd, (d.burst - 1) * d.burstGap + 0.12f); }
            }
            TryEcho();
        }

        void TryEcho()
        {
            float ec = s.echoChance > 0f ? s.echoChance + Mathf.Max(0f, s.luck) * 0.015f : 0f;
            if (ec > 0f && Chance.Roll("echo", ec)) G.Spawn(new EchoStrike(this));
        }

        public void EchoFire()
        {
            if (mode != Mode.Normal || attack == null) return;
            if (attack.Melee) MeleeSwing(true);
            else if (attack.type == WType.Rail) RailShot(0.6f, true);
            else FireOnce(true);
        }

        void Recoil(float f)
        {
            if (f == 0f) return;
            if (Cursed(Curse.Recoil)) f *= 3f;   // CURSE OF RECOIL
            // Super Crate Box style: every shot shoves you back. Shots in quick succession don't stack up into a
            // runaway slide: the push can't take you faster than one shot's worth (or how fast you were already going).
            float push = aimDir.x * f * 2.6f;
            float nx = vel.x - push;
            float cap = Mathf.Max(Mathf.Abs(vel.x), Mathf.Abs(push));
            vel.x = Mathf.Clamp(nx, -cap, cap);
            // shooting straight down in mid-air gives a little lift (rocket jumps!)
            if (!grounded && aimDir.y < -0.5f) vel.y = Mathf.Max(vel.y, -aimDir.y * f * 1.7f);
            else if (!grounded) vel.y -= aimDir.y * f * 0.5f;
        }

        void FireOnce(bool echo)
        {
            var w = weapon;
            var d = w.def;
            if (!echo) EndCamo();   // shooting gives you away
            if (s.stillCrit && stillAimT >= 0.5f) nextShotCrit = true;   // DEADLY STILLNESS
            stillAimT = 0f;
            Vector2 m = Muzzle;
            // muzzle inside a wall: shoot from closer in so point-blank shots still mine it
            Vector2 origin = Wd.Solid(Mathf.FloorToInt(m.x), Mathf.FloorToInt(m.y)) ? GunPivot + aimDir * 0.25f : m;
            bool triple = w.Has(Mod.Triple);
            int multi = Mathf.Max(1, s.multiShot) * (s.freeDouble ? 2 : 1);   // DOUBLE / TRIPLE / QUAD BARREL multiplies with the TRIPLE modifier (MIRROR SOUL: x2, free)
            for (int t = triple ? -1 : 0; t <= (triple ? 1 : 0); t++)
              for (int k = 0; k < multi; k++)
                for (int i = 0; i < d.count + (d.spear ? spearVolley : 0); i++)
                {
                    float shaky = Cursed(Curse.Shaking) ? 3f : 1f;   // CURSE OF SHAKING
                    float fan = (k - (multi - 1) * 0.5f) * (d.count > 1 ? 9f : 6f);
                    if (d.spear && spearVolley > 0) fan += (i - spearVolley * 0.5f) * 7f;   // a charged volley fans out
                    float wild = wildBurst ? 2.6f : 1f;   // a charged burst sprays wide
                    float ang = t * 12f + fan + Random.Range(-d.spread * shaky - (shaky > 1f ? 4f : 0f), d.spread * shaky + (shaky > 1f ? 4f : 0f)) * 0.5f * s.spreadMul * wild
                              + Random.Range(-1f, 1f) * (HeatWobble(w) + (wildBurst ? 6f : 0f));
                    Vector2 dir = U.Rot(aimDir, ang);
                    float spd = d.speed * (1f + Random.Range(-d.speedVar, d.speedVar)) * nextSpeedMul;
                    if (d.spear) G.Spawn(new Spear(this, w, origin, dir * spd, Dmg(d.dmg) * nextShotMul * (nextShotCrit ? s.critMul : 1f)));
                    else G.Spawn(new Bullet(this, w, origin, dir * spd, echo));
                }

            bool hc = w.Has(Mod.HighCaliber);
            G.fx.Muzzle(m, (0.75f + (d.count > 1 ? 0.45f : 0f)) * d.size * (hc ? 1.4f : 1f), d.col);
            kick = d.kick * (hc ? 1.5f : 1f) * chargedMul;
            slowT = 0.18f;
            Recoil(d.recoil * (hc ? 1.6f : 1f) * chargedMul);
            G.cam.Shake(Mathf.Max(0.07f, d.shake * 1.8f) * (hc ? 1.7f : 1f) * chargedMul);
            G.cam.Kick(-aimDir * Mathf.Max(0.1f, d.kick * 1.6f) * (hc ? 1.4f : 1f));
            if (d.shake >= 0.15f) G.cam.Punch(d.shake * 0.6f);
            G.sfx.Play(d.sfx, 0.62f, d.pitch * (hc ? 0.72f : 1f) * (echo ? 1.3f : 1f), 0.09f);
            if (d.id == "flamer" && Time.time - lastFlameT > 0.35f) G.sfx.Play("flame_start", 0.7f, 1f, 0.05f);   // the whoomp as it catches
            if (d.id == "theone")
            {
                // THE ONE: it goes off like a cannon, and the muzzle blast is an explosion of its own
                G.sfx.Play("boom", 1f, 0.55f, 0.04f);
                G.sfx.Play("explode_big", 0.75f, 0.85f, 0.05f);
                G.sfx.Play("sniper", 0.7f, 0.55f, 0.04f);
                G.Explode(m + aimDir * 0.7f, 1.5f, Dmg(d.dmg) * 0.3f, true, true, true, true);
                G.fx.Ring(m, 2.2f, new Color(1f, 0.9f, 0.5f));
                G.HitStop(0.06f);
            }
            if (d.id == "flamer") lastFlameT = Time.time;
            G.lighting.Flash(m, 2.6f, 0.06f);
            G.hud.WeaponKick();
            nextShotMul = 1f; nextShotCrit = false; nextSpeedMul = 1f;
            // PHANTOM ARSENAL: now and then a ghostly copy of your gun drifts out and fires at the cursor
            if (s.ghostGun && !echo && GhostGun.Alive < 3 && Chance.Roll("ghostgun", Lucky(d.delay < 0.12f ? 0.025f : 0.08f)))
                G.Spawn(new GhostGun(this, w, GunPivot + aimDir * 0.6f, aimDir));
            if (!d.flame && !d.lob && d.radius <= 0f && Random.value < 0.85f)
                G.fx.Casing(GunPivot + aimDir * 0.2f, new Vector2(-aimDir.x * Random.Range(1.5f, 3.5f) - facing * 1f, Random.Range(4f, 7.5f)));
        }

        void FireItem(Weapon w)
        {
            EndCamo();
            lastShotT = 0f;
            int n = Mathf.Max(1, s.multiShot) * (w.Has(Mod.Triple) ? 3 : 1);
            for (int i = 0; i < n; i++)
            {
                Vector2 dir = U.Rot(aimDir, (i - (n - 1) * 0.5f) * 9f);
                G.Spawn(new ItemShot(this, w.ammoItem, Muzzle, dir * 15f + vel * 0.3f + Vector2.up * 1.5f));
            }
            G.fx.Muzzle(Muzzle, 0.9f, new Color(0.8f, 0.5f, 1f));
            kick = 0.25f;
            Recoil(1.2f);
            G.cam.Shake(0.12f);
            G.sfx.Play("launcher", 0.6f, 1.1f, 0.08f);
            G.hud.WeaponKick();
        }

        // ------------------------------------------------------------------ melee

        float lastFlameT = -9f;

        void MeleeSwing(bool echo)
        {
            var w = weapon;
            var d = w.def;
            swingT = SwingTime;
            swingSide = -swingSide;
            if (!echo) EndCamo();
            bool hc = w.Has(Mod.HighCaliber);
            bool crit = Chance.Roll("crit", CritChance + (w.Has(Mod.Crit) ? 0.2f : 0f)) || (echo && Combo("echochamber") && Chance.Roll("echocrit", 0.5f));
            float dmg = Dmg(d.dmg) * (crit ? s.critMul : 1f) * (hc ? 2.5f : 1f) * s.meleeMul * MeleePower;   // BRASS KNUCKLES, GIANT'S GAUNTLET...
            float reach = d.reach * s.meleeReach * MeleeReachMul + 0.2f;   // a longer, more forgiving swing
            // EARTHSPLITTER: a three-hit combo; the third is a huge, far-reaching quake
            bool quake = false;
            if (d.id == "earthsplitter" && !echo)
            {
                splitStep = Time.time - splitLast < 1.1f ? (splitStep + 1) % 3 : 0;
                splitLast = Time.time;
                if (splitStep == 2) { quake = true; dmg *= 2.4f; reach *= 1.9f; fireCd += 0.25f; }
            }
            int power = Mathf.Max(1, s.digPower) * d.mine * (w.Has(Mod.WallBreaker) ? 3 : 1) * (hc ? 2 : 1);
            if (quake) power *= 2;

            var cells = new HashSet<Vector2Int>();
            var hitSet = new HashSet<Enemy>();
            Vector2 c0 = GunPivot;
            bool hitE = false;
            float feet = pos.y - Half.y + 0.02f, head = pos.y + Half.y - 0.02f;
            var offList = new List<float>();
            int multi = Mathf.Max(1, s.multiShot) * (s.freeDouble ? 2 : 1);
            foreach (float tb in w.Has(Mod.Triple) ? new[] { -35f, 0f, 35f } : new[] { 0f })
                for (int k = 0; k < multi; k++) offList.Add(tb + (k - (multi - 1) * 0.5f) * 22f);   // extra slashes fan out
            float[] offs = offList.ToArray();

            foreach (float off in offs)
            {
                Vector2 dir = U.Rot(aimDir, off);
                float digArc = Mathf.Min(d.arc, 70f) * 0.5f;
                for (float r = 0.8f; r <= reach + 0.01f; r += 0.4f)
                    for (float a = -digArc; a <= digArc + 0.1f; a += 10f)
                    {
                        Vector2 pnt = c0 + U.Rot(dir, a) * r;
                        // don't chew the floor you stand on unless you aim down (or the ceiling unless you aim up)
                        if (pnt.y < feet && dir.y > -0.35f) continue;
                        if (pnt.y > head && dir.y < 0.35f) continue;
                        cells.Add(new Vector2Int(Mathf.FloorToInt(pnt.x), Mathf.FloorToInt(pnt.y)));
                    }

                foreach (var e in G.ents)
                {
                    if (e.dead || !(e is Enemy en) || hitSet.Contains(en) || en.Intangible || en.thrall) continue;
                    // measured to the nearest point of the foe's body, not its centre, so big things (bosses) are easy to hit
                    Vector2 v = NearestPoint(en, c0) - c0;
                    float dist = v.magnitude;
                    if (dist > reach + 0.3f) continue;
                    if (dist > d.closeR && Vector2.Angle(dir, v) > d.arc * 0.5f + d.arcPad) continue;   // each blade's own hit shape
                    hitSet.Add(en);
                    float dd = dmg * Backstab(en);
                    bool weak = w.Has(Mod.Sharpshooter) && v.magnitude < reach * 0.55f;
                    if (weak) dd *= 2f;
                    if (en.Poisoned) dd *= 1.25f;
                    if (v.magnitude < 1.3f) dd *= 1.3f;   // point blank: getting this close is a risk, so it pays
                    Vector2 kn = dir * d.knock * (hc ? 1.8f : 1f) * s.knockMul + Vector2.up * 2f;
                    Unlocks.Src = 1;   // (a kill from this swing counts as a melee kill)
                    bool landed = en.TakeShot(dd, kn);
                    Unlocks.Src = 0;
                    if (landed)
                    {
                        hitE = true;
                        if (en.dead && s.meleeBoom) G.Spawn(new Blast(en.pos, 0.05f, 1.6f, Mathf.Max(3, Mathf.RoundToInt(dmg)), true));   // BRASS KNUCKLES: melee kills burst
                        OnWeaponHit(en, w, dd, en.pos, dir);
                        MeleeStatus(en);
                        if (crit) G.Popup(en.pos + Vector2.up, "CRIT!", new Color(1f, 0.4f, 0.3f));
                        else if (weak) G.Popup(en.pos + Vector2.up, "WEAK SPOT!", Mods.Col(Mod.Sharpshooter));
                    }
                }
                G.HitAltars(c0 + dir * reach * 0.6f, new Vector2(reach * 0.6f, reach * 0.6f), Src.Weapon);
                Keeper.HitAt(c0 + dir * reach * 0.6f, new Vector2(reach * 0.6f, reach * 0.6f), dmg, c0);
                if (d.deflect) DeflectAround(dir, reach + 0.45f, d.arc);
                if (d.shred) ShredAround(dir, reach + 0.3f, d.arc + 20f);
                G.Spawn(quake ? new Slash(this, dir, d, echo, swingSide, new Color(1f, 0.55f, 0.2f), 1.9f) : new Slash(this, dir, d, echo, swingSide));
                MythicSwing(dir, reach, d, echo);
            }
            MeleeExtensions(aimDir, reach, d, dmg, echo, hitSet);
            // SPLIT items on a blade: once the swing is done, little air slices fly off it, two for every split, fanned out
            // evenly to the sides of the swing (at most almost square to it)
            int slices = (s.splash ? 2 : 0) + (s.splash4 ? 4 : 0) + (s.cloudSplit ? 2 : 0);
            if (slices > 0)
            {
                Color sc = new Color(0.75f, 0.92f, 1f);
                float each = 0.25f * s.splashMul;
                for (int i = 0; i < slices; i++)
                {
                    float a = -80f + (i + 0.5f) * 160f / slices;
                    Vector2 sd = U.Rot(aimDir, a);
                    G.Spawn(new AirSlice(this, c0, sd, Mathf.Max(0.5f, dmg * each), sc, 0.14f + i * 0.015f, swingSide));
                }
            }
            bool broke = false, dmged = false, clank = false;
            // loose (rolling or falling) boulders too
            for (int bi = BoulderBody.All.Count - 1; bi >= 0; bi--)
            {
                var bb = BoulderBody.All[bi];
                if (bb.dead) continue;
                Vector2 bv = bb.pos - c0;
                if (bv.magnitude > reach + 0.5f || (bv.magnitude > 0.9f && Vector2.Angle(aimDir, bv) > d.arc * 0.5f + 25f)) continue;
                bb.Damage(Mathf.Max(2, power * 2));
                if (!bb.dead) bb.Push(aimDir * 2f);
                broke = true;
            }

            Block soundBlock = Block.Air;
            bool grass = false;
            foreach (var c in cells)
            {
                if ((Wd.Flags(c.x, c.y) & (World.Grass | World.Vine)) != 0) grass = true;
                Block b = Wd.Get(c.x, c.y);
                if (b != Block.Air && b != Block.Lava && soundBlock == Block.Air) soundBlock = b;
                // blades smash boulders apart (a couple of good swings)
                var r = Wd.Damage(c.x, c.y, b == Block.Boulder ? Mathf.Max(2, power * 2) : power, true, GoldMul);
                Vector2 cc = new Vector2(c.x + 0.5f, c.y + 0.5f);
                if (r == World.Hit.Broke) { broke = true; G.fx.TileFlash(c); }
                else if (r == World.Hit.Damaged) { dmged = true; G.fx.TileFlash(c); G.fx.Sparks(cc - aimDir * 0.45f, TileDraw.DebrisColor(b, Biomes.At(c.x, c.y))); }
                else if (r == World.Hit.Unbreakable) { clank = true; G.fx.Sparks(cc - aimDir * 0.45f, new Color(1f, 0.9f, 0.6f)); }
            }
            if (w.Has(Mod.Explosive) || s.shotExplode) G.Explode(c0 + aimDir * reach, 1.2f, dmg * 0.6f, true, true, true, true);   // a blade's blasts never hurt its swinger
            if (grass) G.sfx.Play("grass_cut", 0.5f, 1f, 0.08f);
            if (hitE && (d.id == "hammer" || d.id == "earthsplitter" || d.id == "sword")) G.sfx.Play("hit_big", 0.7f, 1f, 0.08f);   // a heavy blade biting in

            float pitch = d.pitch * (echo ? 1.35f : 1f);
            if (d.id == "earthsplitter")
            {
                // a heavy rock-tearing swing; the third, a seismic crack that shakes the whole cave
                G.sfx.Play(quake ? "split3" : "split", quake ? 1f : 0.75f, (quake ? 1f : 0.95f + splitStep * 0.08f) * (echo ? 1.3f : 1f), 0.04f);
                if (quake)
                {
                    G.HitStop(0.09f); G.cam.Shake(0.9f); G.cam.Punch(0.5f); G.hud.ScreenFlash(0.15f);
                    G.lighting.Flash(c0 + aimDir * reach * 0.5f, 7f, 0.25f);
                    for (int qi = 0; qi < 14; qi++)
                        G.fx.Debris(c0 + U.Rot(aimDir, Random.Range(-d.arc * 0.5f, d.arc * 0.5f)) * Random.Range(1f, reach), new Color(0.75f, 0.5f, 0.3f), 3);
                    G.fx.Ring(c0, reach, new Color(1f, 0.6f, 0.25f));
                }
            }
            else G.sfx.Play(d.sfx, 0.55f, pitch, 0.1f);
            if (soundBlock != Block.Air) G.sfx.Material(soundBlock, broke, 1.3f);
            if (broke) { G.sfx.Play("dig", 0.6f, pitch, 0.15f); G.HitStop(0.03f); G.cam.Shake(0.1f + d.shake); squash = 0.16f; }
            else if (dmged) { G.sfx.Play("chip", 0.75f, pitch, 0.15f); G.HitStop(0.022f); G.cam.Shake(0.06f + d.shake * 0.5f); squash = 0.1f; }
            else if (clank) { G.sfx.Play("clank", 0.55f); G.cam.Shake(0.05f); vel.x -= aimDir.x * 3f; }
            if (hitE)
            {
                G.HitStop(crit ? 0.08f : 0.05f);
                G.cam.Shake(crit ? 0.25f : 0.14f + d.shake);
                if (aimDir.y < -0.6f && !grounded) { vel.y = 13f; airJumps = s.airJumps; }   // pogo off enemies
            }
            if (broke && s.pickFrenzy) fireCd *= 0.75f;
            if (aimDir.y < -0.6f && broke && grounded) { snapX = Mathf.FloorToInt(pos.x) + 0.5f; snapT = 0.15f; }
            kick = d.kick;
            Recoil(d.recoil);
            G.cam.Kick(aimDir * d.kick * 0.5f);
            G.hud.WeaponKick();
        }

        // melee hits hard: getting up close is the risk, this is the reward
        public const float MeleePower = 1.6f;
        public const float MeleeReachMul = 1.25f;

        // the point of a foe's body closest to p
        static Vector2 NearestPoint(Enemy en, Vector2 p) =>
            new Vector2(Mathf.Clamp(p.x, en.pos.x - en.half.x, en.pos.x + en.half.x), Mathf.Clamp(p.y, en.pos.y - en.half.y, en.pos.y + en.half.y));

        // shot items that set things alight, poison or freeze work on melee hits too
        // The element colours your attacks carry (fire, poison, frost, blasts, sparks, homing, wild), in the order you
        // got them: trinkets first (oldest to newest), then the weapon's own modifiers. Melee swooshes wear the last one
        // and shed particles of all of them.
        static readonly Color FxFire = new Color(1f, 0.55f, 0.2f), FxPoison = new Color(0.55f, 1f, 0.35f), FxFrost = new Color(0.6f, 0.9f, 1f),
            FxBlast = new Color(1f, 0.78f, 0.35f), FxSpark = new Color(1f, 1f, 0.5f), FxHome = new Color(0.5f, 0.75f, 1f), FxWild = new Color(1f, 0.5f, 0.85f);
        public List<Color> AttackFx(Weapon w)
        {
            var l = new List<Color>();
            void Add(Color c) { l.Remove(c); l.Add(c); }   // the most recent counts as last
            foreach (var k in skillOrder)
                switch (k.id)
                {
                    case "kindle": case "dragon": case "heart_ignis": Add(FxFire); break;
                    case "toxin": case "viper": Add(FxPoison); break;
                    case "frostheart": case "heart_unda": Add(FxFrost); break;
                    case "cap": case "chain": Add(FxBlast); break;
                    case "seekeye": case "hunch": Add(FxHome); break;
                    case "wildcard": Add(FxWild); break;
                }
            if (w != null)
                foreach (var m in w.mods)
                    switch (m)
                    {
                        case Mod.Fire: Add(FxFire); break;
                        case Mod.Poison: Add(FxPoison); break;
                        case Mod.Frost: Add(FxFrost); break;
                        case Mod.Explosive: Add(FxBlast); break;
                        case Mod.Chain: Add(FxSpark); break;
                        case Mod.Homing: Add(FxHome); break;
                    }
            return l;
        }

        public void MeleeStatus(Enemy en)
        {
            if (en == null || en.dead) return;
            if (s.shotFire || (s.fireChance > 0f && Chance.Roll("fire", Lucky(s.fireChance)))) en.Ignite(2f);
            if (s.shotPoison || (s.poisonChance > 0f && Chance.Roll("poison", Lucky(s.poisonChance)))) en.Poison(3.5f, Dmg(1f) * 0.5f);
            if (s.shotFrost) en.Freeze(1.2f);
            if (s.clawBleed) en.Poison(3f, Dmg(1f) * 0.5f);   // SHARP CLAWS: they bleed
            if (s.wildShots && Chance.Roll("wild", Lucky(0.3f)))
                switch (Random.Range(0, 3)) { case 0: en.Ignite(2f); break; case 1: en.Poison(3f, Dmg(1f) * 0.4f); break; default: en.Freeze(1f); break; }
        }

        // Shot items grow PHANTOM BLADES on melee weapons: a bigger blade of laser, worms, steel, ghost-light...
        // swung at the same moment as the real one (a foe right in front of you gets hit by both).
        void MeleeExtensions(Vector2 dir, float reach, AttackDef d, float dmg, bool echo, HashSet<Enemy> mainHits)
        {
            if (s.shotLaser || s.megaLaser)
                PhantomBlade(dir, reach * (s.megaLaser ? 2.2f : 1.75f), Mathf.Max(30f, d.arc * 0.6f), dmg * (s.megaLaser ? 0.9f : 0.6f),
                    s.megaLaser ? new Color(1f, 0.93f, 0.55f) : new Color(1f, 0.3f, 0.4f), d, echo, 0);
            if (s.wormShots) PhantomBlade(dir, reach * 1.5f, d.arc + 10f, dmg * 0.5f, new Color(1f, 0.6f, 0.7f), d, echo, 1);
            if (s.swordShots) PhantomBlade(dir, reach * 1.6f, d.arc, dmg * 0.6f, new Color(0.8f, 0.9f, 1f), d, echo, 2);
            if (s.spectral) PhantomBlade(dir, reach * 1.9f, d.arc, dmg * 0.45f, new Color(0.7f, 0.8f, 1f, 0.75f), d, echo, 3);
            if (s.swell) PhantomBlade(dir, reach * 1.35f, d.arc + 25f, dmg * 0.7f, new Color(1f, 0.85f, 0.5f), d, echo, 4);
            if (s.shotHoming)
            {
                // a homing blade lashes out at the nearest foe the swing missed
                Enemy best = null;
                float bd = reach * 2.6f;
                foreach (var e in G.ents)
                {
                    if (e.dead || !(e is Enemy en) || en.Intangible || en.thrall || mainHits.Contains(en)) continue;
                    float dd = (en.pos - GunPivot).magnitude;
                    if (dd < bd) { bd = dd; best = en; }
                }
                if (best != null) PhantomBlade((best.pos - GunPivot).normalized, bd + 0.5f, 40f, dmg * 0.5f, new Color(0.75f, 0.45f, 1f), d, echo, 5);
            }
            if (s.splash || s.splash4 || s.cloudSplit)
            {
                // SPLASH: every foe the blade struck bursts with a little shockwave
                float k = (s.splash4 ? 0.5f : 0.3f) * s.splashMul;
                foreach (var en in mainHits)
                {
                    G.HitEnemies(en.pos, new Vector2(1.1f, 1.1f), dmg * k, (en.pos - pos).normalized * 4f + Vector2.up * 2f);
                    G.fx.Ring(en.pos, 1.1f, new Color(0.6f, 0.85f, 1f));
                }
            }
        }

        void PhantomBlade(Vector2 dir, float reach, float arc, float dmg, Color col, AttackDef d, bool echo, int kind)
        {
            Vector2 c0 = GunPivot;
            int worms = 0;
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en) || en.Intangible) continue;
                Vector2 v = NearestPoint(en, c0) - c0;
                float dist = v.magnitude;
                if (dist > reach + 0.3f) continue;
                if (dist > 0.9f && Vector2.Angle(dir, v) > arc * 0.5f + 25f) continue;
                float dd = dmg * Backstab(en);
                if (en.TakeShot(dd, dir * d.knock * 0.6f * s.knockMul + Vector2.up * 1.5f))
                {
                    OnWeaponHit(en, weapon, dd, en.pos, dir);
                    MeleeStatus(en);
                    G.fx.Burst(en.pos, 5, col, 4f, 0.2f, 0f);
                    // WORM BLADE: each hit flings a worm (two per swing at most)
                    if (kind == 1 && worms < 2 && FriendlyWorm.Alive < 24) { G.Spawn(new FriendlyWorm(this, weapon, en.pos, U.Rot(dir, Random.Range(-60f, 60f)), dmg * 0.6f)); worms++; }
                }
            }
            if (kind == 1 && worms == 0 && FriendlyWorm.Alive < 24 && Random.value < 0.4f)
                G.Spawn(new FriendlyWorm(this, weapon, c0 + dir * reach * 0.7f, dir, dmg * 0.6f));
            float baseReach = Mathf.Max(0.3f, d.reach * s.meleeReach * MeleeReachMul + 0.2f);
            G.Spawn(new Slash(this, dir, d, echo, swingSide, col, Mathf.Max(1.05f, reach / baseReach)));
            if (kind == 0)
                for (int i = 1; i <= 7; i++) G.fx.Burst(c0 + dir * reach * i / 7f, 1, col, 0.6f, 0.15f, 0f);   // the laser's beam edge
        }

        // Melee weapons bat enemy bullets straight back where you aim.
        // DAGGER: shots caught in the stab are cut to pieces, not sent back
        void ShredAround(Vector2 dir, float reach, float arc)
        {
            Vector2 c0 = GunPivot;
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Bullet b) || b.team == Team.Player) continue;
                Vector2 v = b.pos - c0;
                if (v.magnitude > reach) continue;
                if (v.magnitude > 0.5f && Vector2.Angle(dir, v) > arc * 0.5f) continue;
                G.fx.Burst(b.pos, 6, new Color(0.9f, 0.92f, 1f), 4f, 0.2f, 10f);
                G.sfx.Play("chip", 0.5f, 1.9f, 0.1f);
                b.Kill();
            }
        }

        // the mythic blades swing like nothing else
        void MythicSwing(Vector2 dir, float reach, AttackDef d, bool echo)
        {
            Vector2 c0 = GunPivot;
            switch (d.id)
            {
                case "prismblade":
                    // PRISM GREATSWORD: the swing splits into a rainbow, three ghost-arcs fanning out behind it
                    G.Spawn(new Slash(this, U.Rot(dir, 14f), d, echo, swingSide, new Color(1f, 0.35f, 0.4f, 0.8f), 1.06f));
                    G.Spawn(new Slash(this, U.Rot(dir, -14f), d, echo, swingSide, new Color(0.4f, 0.6f, 1f, 0.8f), 1.06f));
                    G.Spawn(new Slash(this, dir, d, echo, swingSide, new Color(0.5f, 1f, 0.6f, 0.7f), 1.18f));
                    for (int i = 0; i < 10; i++)
                        G.fx.Burst(c0 + U.Rot(dir, Random.Range(-d.arc * 0.5f, d.arc * 0.5f)) * reach * Random.Range(0.6f, 1.05f), 1,
                            Color.HSVToRGB(Random.value, 0.6f, 1f), 2f, 0.4f, 0f, 1.2f, false);
                    break;
                case "starpick":
                    // STAR PICK: a second, crossing gold slash, and stars flung off the edge of the swing
                    G.Spawn(new Slash(this, dir, d, echo, -swingSide, new Color(1f, 0.9f, 0.45f, 0.85f), 1.02f));
                    for (int i = 0; i < 5; i++)
                    {
                        Vector2 at = c0 + U.Rot(dir, Random.Range(-d.arc * 0.5f, d.arc * 0.5f)) * reach;
                        G.fx.Burst(at, 3, new Color(1f, 0.95f, 0.6f), 3.5f, 0.3f, 0f, 1.1f, false);
                    }
                    break;
                case "earthsplitter":
                    // EARTHSPLITTER: the swing tears rock up out of the ground along its arc
                    for (int i = 0; i < 6; i++)
                        G.fx.Debris(c0 + U.Rot(dir, Random.Range(-d.arc * 0.5f, d.arc * 0.5f)) * Random.Range(0.7f, 1f) * reach, new Color(0.7f, 0.48f, 0.3f), 2);
                    break;
                default:
                    if (d.swing == "smash")
                    {
                        // a hammer comes down hard: a thud of dust where it lands
                        G.fx.Burst(c0 + dir * reach * 0.8f, 8, new Color(0.75f, 0.68f, 0.58f), 4f, 0.35f, 15f);
                        G.cam.Punch(0.08f);
                    }
                    break;
            }
        }

        public void DeflectAround(Vector2 dir, float reach, float arc)
        {
            Vector2 c0 = GunPivot;
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Bullet b) || b.team == Team.Player) continue;
                Vector2 v = b.pos - c0;
                if (v.magnitude > reach) continue;
                if (v.magnitude > 0.6f && Vector2.Angle(dir, v) > arc * 0.5f + 25f) continue;
                b.Deflect(Team.Player, aimDir);
                G.sfx.Play("reflect", 0.7f, 1f, 0.1f);
                G.fx.Sparks(b.pos, new Color(0.7f, 0.95f, 1f));
                G.HitStop(0.03f);
            }
        }

        // ------------------------------------------------------------------ railgun

        // energy weapons get shakier the closer they run to overheating (degrees of extra wobble)
        static float HeatWobble(Weapon w) => w.def.UsesHeat ? w.heat * w.heat * 9f : 0f;

        void RailShot(float power, bool echo)
        {
            int multi = Mathf.Max(1, s.multiShot) * (s.freeDouble ? 2 : 1);
            for (int k = multi - 1; k >= 0; k--) RailShot(power, echo, (k - (multi - 1) * 0.5f) * 6f, k > 0);
        }

        void RailShot(float power, bool echo, float angOff, bool extra)
        {
            var w = weapon;
            var d = w.def;
            if (!extra) EndCamo();
            Vector2 aimDir = U.Rot(this.aimDir, angOff + Random.Range(-1f, 1f) * HeatWobble(w));
            bool hc = w.Has(Mod.HighCaliber);
            bool crit = Chance.Roll("crit", CritChance + (w.Has(Mod.Crit) ? 0.2f : 0f)) || (echo && Combo("echochamber") && Chance.Roll("echocrit", 0.5f));
            float dmg = Dmg(d.dmg) * (0.35f + 0.65f * power) * (hc ? 2.5f : 1f) * (crit ? s.critMul : 1f) * (s.megaLaser ? 1.5f : 1f) * (s.swordShots ? 1.2f * s.meleeMul : 1f);
            int minePow = Mathf.CeilToInt(Mathf.Max(1, s.digPower) * d.mine * power) * (w.Has(Mod.WallBreaker) ? 3 : 1);
            Vector2 o = GunPivot, p = o;
            var hitE = new HashSet<Enemy>();
            var done = new HashSet<Vector2Int>();
            var touchedAltars = new HashSet<Altar>();
            float len = 26f * (0.5f + 0.5f * power);
            int budget = Mathf.CeilToInt(6f * power) + 2 + (s.megaLaser ? 3 : 0);
            Vector2 hb = new Vector2(0.3f, 0.3f) * (s.megaLaser ? 2f : 1f);
            Vector2 seg = o + aimDir * 0.5f;
            Color beamCol = ShotTint(d.col, w);
            float thick = (0.1f + 0.25f * power) * (s.megaLaser ? 2.4f : 1f);
            p = o + aimDir * 0.3f;
            for (float t = 0.3f; t < len; t += 0.2f)
            {
                // homing bends the beam towards foes it can see; the beam is drawn as a chain of straight pieces
                if (s.shotHoming && Mathf.Repeat(t, 1f) < 0.2f)
                {
                    Enemy best = null; float bs = 9f;
                    foreach (var e0 in G.ents)
                        if (!e0.dead && e0 is Enemy en0 && !en0.Intangible && !hitE.Contains(en0))
                        {
                            Vector2 v = en0.pos - p;
                            float dd0 = v.magnitude;
                            if (dd0 < bs && Vector2.Angle(aimDir, v) < 70f && Wd.LineClear(p, en0.pos)) { bs = dd0; best = en0; }
                        }
                    if (best != null)
                    {
                        Vector2 nd = Vector3.RotateTowards(aimDir, (best.pos - p).normalized, 14f * Mathf.Deg2Rad, 0f);
                        if (Vector2.Angle(nd, aimDir) > 0.5f) { G.Spawn(new RailBeam(seg, p, beamCol, thick)); seg = p; aimDir = nd.normalized; }
                    }
                }
                p += aimDir * (t <= 0.3f ? 0f : 0.2f);
                var c = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
                if (!done.Contains(c) && Wd.Solid(c.x, c.y))
                {
                    done.Add(c);
                    G.sfx.Material(Wd.Get(c.x, c.y), true, 1.2f);
                    var r = Wd.Damage(c.x, c.y, minePow, true, GoldMul);
                    G.fx.TileFlash(c);
                    if (r != World.Hit.Broke) budget = 0; else budget--;
                    if (budget <= 0) break;
                }
                foreach (var e in G.ents)
                {
                    if (e.dead || !(e is Enemy en) || hitE.Contains(en) || en.Intangible || en.thrall) continue;
                    if (!U.Overlap(p, hb, en.pos, en.half)) continue;
                    hitE.Add(en);
                    float dd = en.Poisoned ? dmg * 1.25f : dmg;
                    if (en.TakeShot(dd, aimDir * d.knock * power)) OnWeaponHit(en, w, dd, p, aimDir);
                }
                foreach (var a in G.altars)
                    if (!a.dead && a.Active && !touchedAltars.Contains(a) && U.Overlap(p, new Vector2(0.3f, 0.3f), a.pos, a.Half)) { touchedAltars.Add(a); a.OnHit(Src.Weapon); }
            }
            G.Spawn(new RailBeam(seg, p, beamCol, thick));
            if (w.Has(Mod.Explosive) || s.shotExplode) G.Explode(p, 1.5f, dmg * 0.5f, s.blastProof || !w.Has(Mod.Explosive), true, true);
            if (extra) return;   // a multi-shot's extra beams: no second round of kick, sound and shake
            if (crit) G.Popup(pos + Vector2.up * 1.1f, "CRIT!", new Color(1f, 0.4f, 0.3f));
            G.fx.Muzzle(Muzzle, 1.3f * (0.5f + power), d.col);
            G.fx.Burst(p, 14, d.col, 7f, 0.4f, 0f);
            kick = d.kick * power;
            Recoil(d.recoil * power);
            G.cam.Shake(d.shake * (0.4f + power));
            G.cam.Kick(-aimDir * 0.4f * power);
            G.HitStop(0.05f * power);
            G.sfx.Play(d.sfx, 0.8f, 0.9f + 0.3f * (1f - power), 0.05f);
            G.lighting.Flash(o + aimDir * len * 0.3f, 6f * power, 0.15f);
            G.hud.WeaponKick();
        }

        // Shared on-hit effects for every weapon (bullets call this too).
        public void OnWeaponHit(Enemy en, Weapon w, float dmg, Vector2 at, Vector2 dir, int chainGen = 0, HashSet<Entity> chained = null)
        {
            if (w != null && w.Has(Mod.Poison)) en.Poison(3.5f, Mathf.Max(0.3f, dmg * 0.25f));
            if (w != null && w.Has(Mod.Frost)) en.Freeze(1.5f);
            if (w != null && (w.Has(Mod.Fire) || w.def.flame)) en.Ignite(2f);   // burns for 2 seconds, bosses too
            // items that change every shot ("all shots are ...")
            if (s.shotPoison && !(w != null && w.Has(Mod.Poison))) en.Poison(3.5f, Mathf.Max(0.3f, dmg * 0.25f));
            if (s.shotFire) en.Ignite(2f);
            if (s.shotFrost) en.Freeze(1.5f);
            if (s.flyHits) SpawnFly(en.pos);   // FLY SWARM: every hit hatches a fly (up to a dozen at once)
            G.fx.Blood(at, dir, en.BloodColor, 4 + Mathf.Min(8, (int)dmg));
            if (w != null && w.Has(Mod.Chain) && chainGen < 3) ChainFrom(en, w, dmg, chainGen, chained);
        }

        // CHAIN: the hit leaps on to the nearest other foe. Through rock only if it's close,
        // further away if nothing is in between.
        void ChainFrom(Enemy from, Weapon w, float dmg, int gen, HashSet<Entity> chained)
        {
            Enemy best = null;
            float bestScore = float.MaxValue;
            bool bestLos = false;
            foreach (var e in G.ents)
            {
                if (e.dead || e == from || !(e is Enemy o) || o.Intangible) continue;
                if (chained != null && chained.Contains(o)) continue;
                float d = (o.pos - from.pos).magnitude;
                if (d > 10f) continue;
                bool los = Wd.LineClear(from.pos, o.pos);
                if (!los && d > 4.5f) continue;
                float score = los ? d : d * 1.6f;
                if (score < bestScore) { bestScore = score; best = o; bestLos = los; }
            }
            if (best == null) return;
            var ex = chained != null ? new HashSet<Entity>(chained) : new HashSet<Entity>();
            ex.Add(from);
            G.Spawn(Bullet.ChainShot(this, w, from.pos, best, dmg * 0.7f, gen + 1, ex, !bestLos));
            G.Spawn(new RailBeam(from.pos, best.pos, Mods.Col(Mod.Chain), 0f));
            G.sfx.Play("zap", 0.35f, Random.Range(1f, 1.3f));
        }

        // Bullets that hit rock call this so the block counts as dug (charges specials etc).
        public World.Hit MineCell(int x, int y, int power, Vector2 dir)
        {
            var r = Wd.Damage(x, y, power, true, GoldMul);
            if (r == World.Hit.Broke && s.aftershock)
            {
                int nx = x + Mathf.RoundToInt(dir.x), ny = y + Mathf.RoundToInt(dir.y);
                if (Wd.Damage(nx, ny, Mathf.Max(1, power / 2), true, GoldMul) != World.Hit.Empty) G.fx.TileFlash(new Vector2Int(nx, ny));
            }
            if (r == World.Hit.Broke && s.pickFrenzy) fireCd *= 0.75f;
            return r;
        }

        // ================================================================== weapon slots

        public void TakeWeapon(Weapon w)
        {
            if (w != null) Records.Mark("w", w.def.id);
            if (slots[cur] != null && slots[1 - cur] == null) cur = 1 - cur;
            else if (slots[cur] != null) G.Spawn(new WeaponPickup(pos + Vector2.up * 0.3f, slots[cur], 1.2f));
            slots[cur] = w;
            fireCd = 0.2f; chargeT = 0f; burstLeft = 0; huntMark = null;
            CancelReload();
        }

        public void PickUpWeapon(Weapon w)
        {
            TakeWeapon(w);
            G.sfx.Play("pickup", 0.8f);
            G.sfx.Play("reload", 0.6f);
            G.hud.WeaponPop(w.def.name, w.mods.Count > 0 ? w.ModLine : w.def.desc, w.Color);
            if (w.curse != Curse.None)
            {
                G.hud.Banner(Curses.Name(w.curse) + Curses.Skull, Curses.Desc[(int)w.curse] + Curses.Skull, Curses.Col(w.curse));
                G.sfx.Play("cursed", 0.8f, 1f, 0.03f);
                G.fx.Burst(pos, 24, Curses.Col(w.curse), 5f, 0.8f, -4f);
            }
            G.fx.Burst(pos, 18, w.Color, 6f, 0.5f, 0f);
            G.cam.Shake(0.2f);
            G.cam.Punch(0.15f);
            G.hud.WeaponKick();
            RecomputeCombos();
        }

        void SwapWeapon()
        {
            if (slots[1 - cur] == null) { G.sfx.Play("deny", 0.3f, 1.3f); return; }
            cur = 1 - cur;
            CancelReload();
            fireCd = Mathf.Max(fireCd, 0.12f);
            chargeT = 0f; burstLeft = 0; huntMark = null;
            G.sfx.Play("reload", 0.6f, 1.1f);
            G.hud.WeaponKick();
            G.hud.WeaponPop(attack.name, null, weapon.Color);
            RecomputeCombos();
        }

        public bool ApplyMod(Mod m)
        {
            var w = weapon;
            string why = null;
            if (w == null) why = "NO WEAPON IN HAND";
            else if (w.Has(m)) why = "ALREADY " + Mods.Names[(int)m];
            else if (w.mods.Count >= Mods.Max) why = "MAX 3 MODIFIERS";
            else if (!Mods.Fits(w.def, m)) why = "DOESN'T FIT THE " + w.def.name;
            if (why != null)
            {
                G.Popup(pos + Vector2.up * 1.2f, why, new Color(1f, 0.5f, 0.4f));
                G.sfx.Play("deny", 0.5f);
                return false;
            }
            w.mods.Add(m);
            G.fx.Ring(pos, 2f, Mods.Col(m));
            return true;
        }

        // ================================================================== HUNTER mark + buffs

        void TickBuffs(float dt)
        {
            buffT -= dt;
            var w = weapon;
            if (w == null || !w.Has(Mod.Hunter)) { huntMark = null; return; }
            if (huntMark != null && (huntMark.dead || (huntMark.pos - pos).sqrMagnitude > 24f * 24f)) huntMark = null;
            huntT -= dt;
            if (huntMark != null || huntT > 0f) return;
            huntT = 0.5f;
            float best = 13f * 13f;
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en) || en.Intangible || !en.Markable) continue;
                float d2 = (en.pos - pos).sqrMagnitude;
                if (d2 < best && Wd.LineClear(pos, en.pos)) { best = d2; huntMark = en; }
            }
            if (huntMark != null)
            {
                G.Popup(huntMark.pos + Vector2.up, "MARKED", Mods.Col(Mod.Hunter));
                G.sfx.Play("blip", 0.4f, 0.7f, 0f);
            }
        }

        void OnHuntKill(Enemy e)
        {
            if (e != huntMark) return;
            huntMark = null;
            huntT = 1f;
            buff = (Buff)Random.Range(1, 4);
            buffT = 6f;
            string what = buff == Buff.Speed ? "+SPEED" : buff == Buff.Damage ? "+DAMAGE" : "+FIRE RATE";
            G.hud.Banner("HUNTED!", what + " FOR 6 SECONDS", Mods.Col(Mod.Hunter));
            G.fx.Ring(pos, 2.5f, Mods.Col(Mod.Hunter));
            G.sfx.Play("chest", 0.6f, 1.4f);
        }

        // ================================================================== DASH special (see PlayerMoves.cs)

        void TickQuick(float dt)
        {
            modeT -= dt;
            vel = quickDir * 30f;
            Vector2 p = pos;
            if (s.dashThrough)
            {
                // PHASE DASH: straight through rock, only the void (and a sealed hall) stops it
                Vector2 np = pos + vel * dt;
                bool wall = false;
                for (int k = 0; k < 4 && !wall; k++)
                {
                    Vector2 c = np + new Vector2((k & 1) == 0 ? -Half.x : Half.x, (k & 2) == 0 ? -Half.y : Half.y) * 0.9f;
                    if (Tiles.Def(Wd.Get(Mathf.FloorToInt(c.x), Mathf.FloorToInt(c.y))).unbreakable) wall = true;
                }
                if (!wall) p = np; else modeT = 0f;
            }
            else
            {
                Wd.MoveX(ref p, Half, vel.x * dt, out _);
                Wd.MoveY(ref p, Half, vel.y * dt, out _);
            }
            pos = p;
            ghostT -= dt;
            if (ghostT <= 0f)
            {
                ghostT = 0.022f;
                G.fx.Ghost(sr.sprite, go.transform.position, sr.flipX, new Color(0.6f, 0.45f, 1f, 0.7f));
            }
            Pickpocket();
            Fire.DouseNear(pos, 1.1f);   // dashing through flames snuffs them out (burning foes keep burning)
            if (s.dashHit) G.HitEnemies(pos, Half + new Vector2(0.2f, 0.2f), Dmg(2f) * s.meleeMul, quickDir * 6f + Vector2.up * 2f);
            if (modeT <= 0f)
            {
                mode = Mode.Normal;
                vel = new Vector2(quickDir.x * 9f, quickDir.y > 0f ? quickDir.y * 8f : 0f);
            }
        }

        // ================================================================== visuals

        void RenderWeapon()
        {
            var d = attack;
            if (glowT == null) { glowT = Child("HeldGlow", Art.Glow, 101, out glowSr); }
            if (d == null) { toolSr.enabled = false; glowSr.enabled = false; if (sightSr != null) sightSr.enabled = false; return; }
            // the weapon in your hands glows in its rarity colour too (softer than one lying on the ground)
            var wc = weapon.Color;
            glowSr.enabled = true;
            glowSr.color = new Color(wc.r, wc.g, wc.b, 0.12f + (int)d.rarity * 0.035f + weapon.mods.Count * 0.03f + kick * 0.3f);
            float gr = 1.6f + (int)d.rarity * 0.35f;
            glowT.localScale = new Vector3(gr, gr, 1f);
            Vector2 gp = GunPivot + aimDir * 0.4f;
            glowT.position = new Vector3(gp.x, gp.y, 0f);
            toolSr.enabled = true;
            toolSr.sprite = d.icon;
            float ang = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
            if (d.Melee)
            {
                float a;
                if (swingT > 0f)
                {
                    float t = 1f - swingT / SwingTime;
                    t = 1f - (1f - t) * (1f - t);
                    a = ang - 90f + Mathf.Lerp(d.arc * 0.55f, -d.arc * 0.55f, t) * swingSide;
                }
                else a = ang - 90f + 28f * facing;
                Vector2 headDir = U.Rot(Vector2.up, a);
                Vector2 tp = GunPivot + headDir * 0.32f + aimDir * 0.08f;
                tool.position = new Vector3(U.Snap(tp.x), U.Snap(tp.y), 0);
                tool.rotation = Quaternion.Euler(0, 0, a);
                toolSr.flipY = false;
                toolSr.color = Color.white;
            }
            else
            {
                Vector2 tp = GunPivot + aimDir * (0.4f - kick);
                tool.position = new Vector3(U.Snap(tp.x), U.Snap(tp.y), 0);
                tool.rotation = Quaternion.Euler(0, 0, ang);
                toolSr.flipY = aimDir.x < 0f;
                float ch = ChargeFrac01;
                toolSr.color = ch > 0f ? Color.Lerp(Color.white, d.col, ch * (0.6f + 0.4f * Mathf.Sin(Time.time * 30f))) : Color.white;
            }
            tool.localScale = Vector3.one * (1f + kick * 0.6f);

            // laser sight on the sniper, and on the railgun while it charges
            bool wantSight = d.id == "sniper" || (d.type == WType.Rail && chargeT > 0f) || (s.laserSight && !d.Melee);
            if (wantSight)
            {
                if (sight == null) { sight = Child("Sight", Art.Pixel, 19, out sightSr); }
                Vector2 o = Muzzle;
                float len = Wd.RayDist(o, aimDir, 24f);
                Vector2 e = o + aimDir * len;
                sightSr.enabled = true;
                sight.position = (Vector3)((o + e) * 0.5f);
                sight.rotation = Quaternion.Euler(0, 0, ang);
                sight.localScale = new Vector3(len * 16f, 1f, 1f);
                float al = d.type == WType.Rail ? 0.25f + 0.6f * ChargeFrac01 : 0.35f;
                sightSr.color = d.type == WType.Rail ? new Color(d.col.r, d.col.g, d.col.b, al) : new Color(1f, 0.15f, 0.15f, al);
            }
            else if (sightSr != null) sightSr.enabled = false;
        }

        void DestroyGunBits()
        {
            if (sight != null) Object.Destroy(sight.gameObject);
            if (glowT != null) Object.Destroy(glowT.gameObject);
        }
    }
}
