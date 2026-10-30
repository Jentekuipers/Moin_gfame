using UnityEngine;

namespace DigDeep
{
    // What the reworked "plain stat" trinkets DO (Isaac-style: every trinket changes how you play or shoot).
    // Each is a flag in Stats; the hooks below are called from the places where it matters.
    public partial class Player
    {
        float holdFireT, sparkTrailT, killStreakT;
        int killStreak;

        public void GiveBuff(Buff b, float t)
        {
            buff = b;
            buffT = Mathf.Max(buffT, t);
        }

        // HAIR TRIGGER: holding fire spins it up. RAGE MUSHROOM: low on health, everything comes faster.
        float PerkRate()
        {
            float m = 1f;
            if (s.spinUp && holdFireT > 0.25f) m *= 1f + Mathf.Min(0.5f, (holdFireT - 0.25f) * 0.4f);
            if (s.rageLow && s.hp <= s.maxHp * 0.35f) m *= 1.5f;
            return m;
        }

        // every frame (movement perks)
        // SPRING BOOTS fire once: they only arm again after standing on the ground a moment (not on the bounce of their own blast)
        bool landWaveSpent; float groundedT;

        void TickPerks(float dt)
        {
            groundedT = grounded ? groundedT + dt : 0f;
            if (groundedT > 0.35f) landWaveSpent = false;
            holdFireT = GameInput.Held(Btn.Attack) ? holdFireT + dt : 0f;
            killStreakT -= dt;
            if (killStreakT <= 0f) killStreak = 0;
            // SWIFT BOOTS: running flat out kicks up sparks that burn what they touch
            if (s.sparkTrail && grounded && Mathf.Abs(vel.x) > 4.5f)
            {
                sparkTrailT -= dt;
                if (sparkTrailT <= 0f)
                {
                    sparkTrailT = 0.12f;
                    G.Spawn(new Spark(pos - new Vector2(Mathf.Sign(vel.x) * 0.3f, Half.y - 0.1f), new Vector2(-Mathf.Sign(vel.x) * Random.Range(1f, 3f), Random.Range(2f, 4f)), 0.6f));
                }
            }
            // DYNAMO: moving charges your special
            if (s.moveCharge && special != null && vel.sqrMagnitude > 9f) AddEnergy(dt * 0.35f);
        }

        // CURSE OF THE QUAKE (once the SPRING BOOTS' landing): landing hard from high up sets off a blast under you
        void PerkLanded(float vy)
        {
            // landing hard on glass shatters it
            if (vy < -14f)
                for (int gx = Mathf.FloorToInt(pos.x - Half.x + 0.05f); gx <= Mathf.FloorToInt(pos.x + Half.x - 0.05f); gx++)
                    Wd.ImpactGlass(gx, Mathf.FloorToInt(pos.y - Half.y - 0.1f), -vy, 14f);
            if (!(s.landWave || Cursed(Curse.Quake)) || vy > -16f || landWaveSpent) return;
            landWaveSpent = true; groundedT = 0f;
            float r = Mathf.Clamp(-vy * 0.1f, 1.6f, 3f);
            G.Explode(pos - new Vector2(0f, Half.y - 0.2f), r, Dmg(3f), true, true, true, false);
        }

        // THICK SKIN: getting hit throws a ring of shots out of you
        void PerkHurt()
        {
            if (!s.hitRing) return;
            float off = Random.value * 30f;
            for (int k = 0; k < 10; k++)
            {
                float a = (off + k * 36f) * Mathf.Deg2Rad;
                G.Spawn(Bullet.Pet(pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 15f, Dmg(1.5f), new Color(0.6f, 0.45f, 0.3f)));
            }
        }

        // a kill: MOLE JERKY, MINER'S COFFEE, WAR HYMN
        void PerkKill(Enemy e)
        {
            if (s.jerkyDrop && Chance.Roll("jerky", Lucky(0.06f))) G.Spawn(new HealOrb(e.pos + Vector2.up * 0.4f, 25));
            if (s.killRush) GiveBuff(Buff.Speed, 1.6f);
            killStreak++; killStreakT = 2f;
            if (s.streakRate && killStreak >= 3) { GiveBuff(Buff.Rate, 3f); killStreak = 0; G.fx.Ring(pos, 1.3f, new Color(1f, 0.85f, 0.35f)); }
        }

        // SPEED LOADER: starting a reload shoves everything close away
        void PerkReload()
        {
            if (!s.reloadPush) return;
            G.fx.Ring(pos, 2.4f, new Color(0.75f, 0.85f, 1f));
            G.sfx.Play("dash", 0.4f, 1.6f);
            foreach (var e in G.ents)
                if (!e.dead && e is Enemy en && !en.Intangible && en.team != Team.Player && !(en is Boss) && (en.pos - pos).sqrMagnitude < 2.4f * 2.4f)
                    en.TakeShot(Dmg(1f), (en.pos - pos).normalized * 12f + Vector2.up * 4f);
        }

        // GREEDY GLOVES: picking up gold slips a round into the gun (or cools an energy weapon)
        void PerkGold()
        {
            if (!s.goldReload) return;
            var w = weapon;
            if (w == null) return;
            if (w.def.UsesHeat) w.heat = Mathf.Max(0f, w.heat - 0.06f);
            else if (MagSize(w) > 0 && w.ammo < MagSize(w)) w.ammo++;
        }

        // HEAT SINK: overheating blasts out a frost nova
        void PerkOverheat()
        {
            if (!s.ventNova) return;
            G.fx.Ring(pos, 3f, new Color(0.6f, 0.9f, 1f));
            G.fx.Burst(pos, 30, new Color(0.8f, 0.95f, 1f), 7f, 0.5f, 0f);
            G.sfx.Play("freeze", 0.7f);
            foreach (var e in G.ents)
                if (!e.dead && e is Enemy en && !en.Intangible && en.team != Team.Player && (en.pos - pos).sqrMagnitude < 9f) en.Freeze(1.5f);
        }

        // using a special: SPARK PLUG, BATTERY PACK, CAPACITOR, DEATH'S GUIDE
        void PerkSpecial()
        {
            if (s.specialZap)
            {
                G.sfx.Play("zap", 0.6f);
                foreach (var e in G.ents)
                    if (!e.dead && e is Enemy en && !en.Intangible && en.team != Team.Player && (en.pos - pos).sqrMagnitude < 3.2f * 3.2f)
                    {
                        en.TakeShot(Dmg(2f), (en.pos - pos).normalized * 3f);
                        G.fx.Sparks(en.pos, new Color(1f, 1f, 0.5f));
                    }
            }
            if (s.specialBlast) G.Explode(pos, 1.8f, Dmg(3f), true, true, true, true);
            if (s.specialVolley)
                for (int k = 0; k < 8; k++)
                {
                    float a = k * 45f * Mathf.Deg2Rad;
                    G.Spawn(Bullet.Pet(pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 17f, Dmg(1.6f), new Color(1f, 0.95f, 0.5f)));
                }
            if (s.specialGhost) invuln = Mathf.Max(invuln, 1f);
        }

        // SOUL JAR: a special coming back makes your next shot a crit
        void PerkSpecialReady() { if (s.readyCrit) nextShotCrit = true; }

        // hits from anything of yours: WHETSTONE makes the first hit on a foe a crit
        float PerkHit(Enemy en)
        {
            float m = 1f;
            if (s.firstCrit && en.hp >= en.maxHp - 0.01f) m *= s.critMul;
            return m;
        }
    }
}
