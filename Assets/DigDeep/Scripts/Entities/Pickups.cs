using UnityEngine;

namespace DigDeep
{
    public class Coin : Entity
    {
        public Vector2 vel;
        public readonly int value;
        public float noMag;      // while > 0 the magnet and the player ignore this coin (tossed / near a gold altar)
        float age;
        public Vector2 pullTo;   // a gold bucket is reeling this coin in
        public float pullT;
        static readonly Vector2 H = new Vector2(0.12f, 0.12f);

        public Coin(Vector2 p, int value) : this(p, value, new Vector2(Random.Range(-4f, 4f), Random.Range(4f, 9f)), 0f) { }

        public Coin(Vector2 p, int value, Vector2 v, float noMag)
        {
            pos = p; this.value = value; vel = v; this.noMag = noMag;
            MakeGO("Coin", value >= 10 ? Art.GemDrop : value >= 5 ? Art.BigCoin : Art.Coin, 8);
        }

        public override void Tick(float dt)
        {
            age += dt;
            noMag -= dt;
            var pl = G.player;
            Vector2 d = pl.pos - pos;
            bool alive = pl.mode != Player.Mode.Dead && noMag <= 0f;
            pullT -= dt;
            if (pullT > 0f)
            {
                Vector2 to = pullTo - pos;
                vel = Vector2.MoveTowards(vel, to.normalized * Mathf.Min(10f, 3f + to.magnitude * 3f), 45f * dt);
                pos += vel * dt;
                if (Random.value < 0.2f) G.fx.Burst(pos, 1, new Color(1f, 0.85f, 0.3f), 0.5f, 0.3f, 0f, 1f, false);
            }
            else if (age > 0.3f && alive && d.magnitude < pl.s.magnet)
            {
                vel = Vector2.MoveTowards(vel, d.normalized * 14f, 70f * dt);
                pos += vel * dt;
            }
            else
            {
                vel.y = Fluids.Sink(pos, Mathf.Max(vel.y - 30f * dt, -15f), dt);
                Vector2 p = pos;
                if (Wd.MoveX(ref p, H, vel.x * dt, out _)) vel.x *= -0.5f;
                if (Wd.MoveY(ref p, H, vel.y * dt, out _))
                {
                    if (vel.y < 0f) { vel.y *= -0.4f; vel.x *= 0.8f; if (Mathf.Abs(vel.y) < 1f) { vel.y = 0f; vel.x *= 0.5f; } }
                    else vel.y = 0f;
                }
                pos = p;
            }
            if (age > 0.3f && alive && d.sqrMagnitude < 0.4f)
            {
                pl.AddGold(value);
                G.sfx.Play("coin", 0.4f, value >= 5 ? 1.15f : 1f, 0.08f);
                G.fx.Burst(pos, 3, new Color(1f, 0.9f, 0.4f), 2f, 0.2f, 0f);
                Kill();
                return;
            }
            if (age > 45f) { Kill(); return; }
            Place();
        }

        public override void AddLight(Lighting l) { if (value >= 5) l.Add(pos, 0.9f, 0.4f); }
    }

    // A dropped attack / special / trinket / item. Weapons need UP to swap so you never swap by accident.
    public class LootPickup : Entity
    {
        public readonly Def def;
        Vector2 vel;
        float noPick, bob;
        readonly bool rare;
        static readonly Vector2 H = new Vector2(0.3f, 0.3f);

        public LootPickup(Vector2 p, Def d, float noPick = 0.5f, bool rare = false)
        {
            pos = p; def = d; this.noPick = noPick; this.rare = rare;
            G.player?.MarkSeen(d);   // one-of-a-kind trinkets turn up once a run
            vel = new Vector2(Random.Range(-2f, 2f), 7f);
            MakeGO("Loot", d.icon, 9);
            if (d is ModDef) modGlow = ModGlow.Make(pos);
        }

        readonly ModGlow modGlow;

        public override void Kill()
        {
            base.Kill();
            modGlow?.Kill();
        }

        public override void Tick(float dt)
        {
            bob += dt; noPick -= dt;
            vel.y = Fluids.Sink(pos, Mathf.Max(vel.y - 30f * dt, -15f), dt);
            vel.x = U.Approach(vel.x, 0f, 4f * dt);
            Vector2 p = pos;
            if (Wd.MoveX(ref p, H, vel.x * dt, out _)) vel.x = 0f;
            if (Wd.MoveY(ref p, H, vel.y * dt, out _)) vel.y = 0f;
            pos = p;

            var pl = G.player;
            bool nearL = pl.mode != Player.Mode.Dead && U.Overlap(pos, new Vector2(0.45f, 0.55f), pl.pos, Player.Half);
            if (!nearL && G.MouseOver(pos, 0.6f)) G.hud.ItemLabel(pos + Vector2.up * 1.05f, def, null, pl, true);
            if (nearL)
            {
                bool needsUp = def.kind == Kind.Attack || def.kind == Kind.Special;
                G.hud.ItemLabel(pos + Vector2.up * 1.05f, def, needsUp ? "PRESS " + GameInput.Prompt(Btn.Interact) + " TO SWAP" : null, pl, G.MouseOver(pos, 0.6f));
                if (noPick <= 0f && (!needsUp || GameInput.Take(Btn.Interact)))
                {
                    if (pl.Grant(def)) { Kill(); return; }
                    noPick = 1.5f;
                }
            }
            float sparkle = rare ? 0.3f : 0.06f;
            if (Random.value < sparkle)
                G.fx.Burst(pos + new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(-0.2f, 0.4f)), 1, rare ? DB.ComboColor : DB.ColorOf(def), 0.5f, 0.5f, -1f);
            go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y + 0.05f + Mathf.Sin(bob * 3f) * 0.08f), 0);
            modGlow?.Tick(pos, sr);
        }

        public override void AddLight(Lighting l) => l.Add(pos, rare ? 3.5f : 2.4f, rare ? 0.9f : 0.75f);
    }

    // A weapon lying on the ground (from a crate, an altar, or the one you just swapped out).
    public class WeaponPickup : Entity
    {
        public readonly Weapon w;
        Vector2 vel;
        float noPick, bob, tilt;
        static readonly Vector2 H = new Vector2(0.3f, 0.25f);

        public WeaponPickup(Vector2 p, Weapon w, float noPick = 0.5f)
        {
            pos = p; this.w = w; this.noPick = noPick;
            vel = new Vector2(Random.Range(-2.5f, 2.5f), 7.5f);
            tilt = Random.Range(-25f, 25f);
            MakeGO("Weapon", w.def.icon, 9);
            // a glow in the weapon's rarity colour, drawn over the darkness so it tints the cave around it
            glow = new GameObject("WeaponGlow");
            glow.transform.SetParent(G.runRoot, false);
            glowSr = Art.AddSR(glow, Art.Glow, 101);
            var c = w.Color;
            glowBase = 0.22f + (int)w.def.rarity * 0.05f + w.mods.Count * 0.04f;
            glowSr.color = new Color(c.r, c.g, c.b, glowBase);
            float r = 2.2f + (int)w.def.rarity * 0.5f;
            glow.transform.localScale = new Vector3(r, r, 1f);
        }

        readonly GameObject glow;
        readonly SpriteRenderer glowSr;
        readonly float glowBase;

        public override void Kill()
        {
            base.Kill();
            if (glow != null) Object.Destroy(glow);
        }

        public override void Tick(float dt)
        {
            if (w.curse != Curse.None && Random.value < 0.25f)
                G.fx.Burst(pos + new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-0.2f, 0.5f)), 1, Curses.Col(w.curse), 0.6f, 0.7f, -2f, 1f, false);
            bob += dt; noPick -= dt;
            vel.y = Fluids.Sink(pos, Mathf.Max(vel.y - 30f * dt, -15f), dt);
            vel.x = U.Approach(vel.x, 0f, 4f * dt);
            Vector2 p = pos;
            if (Wd.MoveX(ref p, H, vel.x * dt, out _)) vel.x = 0f;
            if (Wd.MoveY(ref p, H, vel.y * dt, out _)) { vel.y = 0f; tilt = Mathf.MoveTowards(tilt, 0f, dt * 200f); }
            pos = p;

            var pl = G.player;
            bool nearW = pl.mode != Player.Mode.Dead && U.Overlap(pos, new Vector2(0.5f, 0.6f), pl.pos, Player.Half);
            if (!nearW && G.MouseOver(pos, 0.6f)) G.hud.WeaponLabel(pos + Vector2.up * 0.9f, w, null, pl, true);
            if (nearW)
            {
                bool free = pl.slots[0] == null || pl.slots[1] == null;
                G.hud.WeaponLabel(pos + Vector2.up * 0.9f, w, free ? "PRESS " + GameInput.Prompt(Btn.Interact) + " TO TAKE" : "PRESS " + GameInput.Prompt(Btn.Interact) + " TO SWAP", pl, G.MouseOver(pos, 0.6f));
                if (noPick <= 0f && GameInput.Take(Btn.Interact)) { pl.PickUpWeapon(w); Kill(); return; }
            }
            var c = w.Color;
            float sparkle = w.def.rarity >= Rarity.Rare || w.mods.Count > 0 ? 0.25f : 0.07f;
            if (Random.value < sparkle) G.fx.Burst(pos + new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-0.2f, 0.4f)), 1, c, 0.5f, 0.5f, -1f);
            go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y + 0.05f + Mathf.Sin(bob * 3f) * 0.07f), 0);
            go.transform.rotation = Quaternion.Euler(0, 0, tilt + Mathf.Sin(bob * 2f) * 4f);
            glow.transform.position = new Vector3(pos.x, pos.y, 0f);
            var gc = glowSr.color; gc.a = glowBase * (0.85f + 0.15f * Mathf.Sin(bob * 3f));
            glowSr.color = gc;
        }

        public override void AddLight(Lighting l) => l.Add(pos, 2.2f + (int)w.def.rarity * 0.5f + w.mods.Count * 0.4f, 0.8f);
    }

    // A floating heart left behind by a beaten boss. Touch it and it heals you straight away (it waits if you're full).
    public class HealOrb : Entity
    {
        readonly int amount;
        float bob;
        readonly GameObject glow;

        public HealOrb(Vector2 p, int amount)
        {
            pos = p; this.amount = amount;
            MakeGO("HealOrb", Art.Icons["heart"], 9);
            go.transform.localScale = Vector3.one * 1.3f;
            glow = new GameObject("HealGlow");
            glow.transform.SetParent(G.runRoot, false);
            var g = Art.AddSR(glow, Art.Glow, 101);
            g.color = new Color(1f, 0.35f, 0.4f, 0.3f);
            glow.transform.localScale = Vector3.one * 2.4f;
        }

        public override void Tick(float dt)
        {
            bob += dt;
            var pl = G.player;
            go.transform.position = new Vector3(pos.x, U.Snap(pos.y + Mathf.Sin(bob * 3f) * 0.12f), 0f);
            glow.transform.position = new Vector3(pos.x, pos.y, 0f);
            if (Random.value < 0.1f) G.fx.Burst(pos + Random.insideUnitCircle * 0.4f, 1, new Color(1f, 0.5f, 0.55f), 0.6f, 0.6f, -2f, 1f, false);
            if (pl.mode == Player.Mode.Dead || !U.Overlap(pos, new Vector2(0.5f, 0.5f), pl.pos, Player.Half)) return;
            if (pl.s.hp >= pl.s.maxHp) { G.hud.Label(pos + Vector2.up * 0.9f, "FULL HEALTH: IT WILL WAIT FOR YOU", Color.white); return; }
            if (!pl.NoHealing()) pl.s.hp = Mathf.Min(pl.s.maxHp, pl.s.hp + amount);
            G.Popup(pl.pos + Vector2.up, "+" + amount + " HEALTH", new Color(1f, 0.5f, 0.5f));
            G.sfx.Play("heal", 0.9f);
            G.fx.Burst(pos, 24, new Color(1f, 0.45f, 0.5f), 6f, 0.6f, -3f);
            G.fx.Ring(pos, 1.5f, new Color(1f, 0.5f, 0.55f));
            Kill();
        }

        public override void AddLight(Lighting l) => l.Add(pos, 2.5f, 0.8f);

        public override void Kill()
        {
            base.Kill();
            if (glow != null) Object.Destroy(glow);
        }
    }
}
