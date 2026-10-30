using UnityEngine;

namespace DigDeep
{
    // =====================================================================================
    // The void key's parts, the void portal, and the shrines found now and then on the cave floor:
    // the altar of chance, the altar of death, the golden chalice and the sleeping stone dragon.
    // =====================================================================================

    // A part of the void key, left by a boss you beat quickly. Touch it to take it.
    public class VoidShard : Entity
    {
        readonly int part;
        float t, noPick = 0.8f;
        Vector2 vel = new Vector2(0f, 6f);

        public override bool AlwaysActive => true;

        public VoidShard(Vector2 at, int part)
        {
            pos = at; this.part = part;
            MakeGO("VoidShard", Art.KeyShine != null ? Art.KeyShine[0] : Art.Circle, 14);
            sr.color = new Color(0.8f, 0.6f, 1f);
            G.hud.Banner("A PART OF THE VOID KEY", part == 1 ? "BEAT THE NEXT BOSS WITHIN 6 MINUTES FOR THE OTHER" : "THE VOID KEY IS WHOLE. BEAT ORNIN WITH IT", new Color(0.75f, 0.45f, 1f));
            G.sfx.Play("chest", 0.9f, 0.6f);
        }

        public override void Tick(float dt)
        {
            t += dt; noPick -= dt;
            vel.y = Mathf.MoveTowards(vel.y, 0f, 12f * dt);
            pos += vel * dt;
            if (Art.KeyShine != null) sr.sprite = Art.KeyShine[((int)(t * 10f)) % Art.KeyShine.Length];
            go.transform.localScale = Vector3.one * 2f;
            if (Random.value < 0.3f) G.fx.Burst(pos, 1, new Color(0.7f, 0.4f, 1f), 1.5f, 0.5f, 0f);
            var p = G.player;
            if (noPick <= 0f && p.mode != Player.Mode.Dead && (p.pos - pos).sqrMagnitude < 1.2f)
            {
                p.voidShards |= part;
                G.sfx.Play("buy", 0.9f, 0.7f);
                G.fx.Ring(pos, 2f, new Color(0.7f, 0.4f, 1f));
                G.Popup(p.pos + Vector2.up, p.voidShards == 3 ? "THE VOID KEY!" : "VOID KEY PART " + part + " / 2", new Color(0.8f, 0.55f, 1f));
                Kill();
                return;
            }
            go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y + Mathf.Sin(t * 3f) * 0.1f), 0f);
        }

        public override void AddLight(Lighting l) => l.Add(pos, 3f, 0.8f);
    }

    // The black portal beside the God-Diamond (only with the whole void key). Step in to go down into the void world.
    // Where a void run begins: the portal you came through, spinning down and closing behind you.
    public class VoidExit : Entity
    {
        float t;
        public override bool AlwaysActive => true;
        public VoidExit(Vector2 at)
        {
            pos = at;
            MakeGO("VoidExit", Art.Portal, 8);
            go.transform.localScale = Vector3.one * 1.2f;
        }
        public override void Tick(float dt)
        {
            t += dt;
            float k = t < 1.8f ? 1.2f : Mathf.Max(0f, 1.2f - (t - 1.8f) * 0.8f);
            go.transform.localScale = new Vector3(k, k, 1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, t * 90f);
            if (Random.value < 0.6f) G.fx.Burst(pos + Random.insideUnitCircle * k, 1, new Color(0.7f, 0.4f, 1f), 1.5f, 0.5f, 0f);
            if (k <= 0f) { G.fx.Burst(pos, 20, new Color(0.6f, 0.3f, 1f), 5f, 0.5f, 0f); Kill(); return; }
            Place();
        }
        public override void AddLight(Lighting l) => l.Add(pos, 4f * Mathf.Clamp01(1.5f - t * 0.3f), 0.8f);
    }

    public class VoidPortal : Entity
    {
        float t, grow;

        public override bool AlwaysActive => true;

        public VoidPortal(Vector2 at)
        {
            pos = at;
            MakeGO("VoidPortal", Art.Portal, 8);
            go.transform.localScale = Vector3.zero;
            G.hud.Banner("A DOOR TO THE VOID", "THE VOID KEY OPENS IT. STEP IN, OR TAKE THE DIAMOND", new Color(0.75f, 0.45f, 1f));
            G.sfx.Play("rumble", 1f, 0.4f);
        }

        public override void Tick(float dt)
        {
            t += dt;
            grow = Mathf.MoveTowards(grow, 1f, dt * 0.8f);
            float s = grow * (2.2f + Mathf.Sin(t * 3f) * 0.08f);
            go.transform.localScale = new Vector3(s, s, 1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, t * 40f);
            go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y), 0f);
            // it pulls in loose specks of light
            if (Random.value < 0.6f)
            {
                Vector2 from = pos + Random.insideUnitCircle.normalized * 3f;
                G.fx.Burst(from, 1, new Color(0.7f, 0.45f, 1f), 1f, 0.4f, 0f, 1f, false);
            }
            var p = G.player;
            if (grow < 1f || p.mode == Player.Mode.Dead) return;
            float d2 = (p.pos - pos).sqrMagnitude;
            if (d2 < 9f) G.hud.Label(pos + Vector2.up * 2.4f, GameInput.Prompt(Btn.Interact) + ": ENTER THE VOID", new Color(0.8f, 0.55f, 1f));
            if (d2 < 2.2f && GameInput.Take(Btn.Interact)) { G.sfx.Play("rumble", 1f, 0.3f); G.RequestVoid(); }
        }

        public override void AddLight(Lighting l) => l.Add(pos, 4f, 0.9f);
    }

    // A shrine: stands on the floor, shows its name and what it does when you come near, and can be used once.
    public abstract class Shrine : Entity
    {
        protected bool used;
        protected float t;
        protected abstract string Title { get; }
        protected abstract string What { get; }
        protected virtual Color Col => new Color(1f, 0.85f, 0.4f);

        protected Shrine(int x, int y, Sprite s)
        {
            pos = new Vector2(x + 0.5f, y);
            MakeGO("Shrine", s, 8);
            for (int ax = x - 1; ax <= x + 1; ax++) Game.I.world.Anchor(ax, y - 1);   // the floor under it never breaks
            go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y), 0f);
        }

        protected Player P => G.player;
        protected bool Near(float r) => P.mode != Player.Mode.Dead && (P.pos - (pos + Vector2.up * 0.8f)).sqrMagnitude < r * r;

        public override void Tick(float dt)
        {
            t += dt;
            if (used) { sr.color = new Color(0.5f, 0.5f, 0.55f); return; }
            if (Near(4f))
            {
                G.hud.Label(pos + Vector2.up * 2.6f, Title, Col);
                G.hud.Label(pos + Vector2.up * 2.1f, What, new Color(0.9f, 0.9f, 0.9f));
            }
            if (Near(1.6f))
            {
                G.hud.Label(pos + Vector2.up * 1.6f, GameInput.Prompt(Btn.Interact) + ": USE", Color.white);
                if (GameInput.Take(Btn.Interact)) Use();
            }
            Idle(dt);
        }

        protected virtual void Idle(float dt) { }
        protected abstract void Use();

        protected void Done(string say, Color c)
        {
            used = true;
            G.Popup(P.pos + Vector2.up, say, c);
            G.fx.Ring(pos + Vector2.up * 0.6f, 2f, c);
        }

        public override void AddLight(Lighting l) { if (!used) l.Add(pos + Vector2.up * 0.6f, 2.6f, 0.7f); }
    }

    // ALTAR OF CHANCE: turns the weapon in your hand into another of the same rarity. Its modifiers carry over.
    public class RandomAltar : Shrine
    {
        public RandomAltar(int x, int y) : base(x, y, Art.AltarRandom) { }
        protected override string Title => "ALTAR OF CHANCE";
        protected override string What => "REROLL THE WEAPON IN YOUR HAND";
        protected override Color Col => new Color(1f, 0.45f, 0.45f);

        protected override void Use()
        {
            var w = P.weapon;
            if (w == null) { G.Popup(P.pos + Vector2.up, "NO WEAPON IN HAND", new Color(1f, 0.5f, 0.4f)); return; }
            var nw = Crates.Roll(Mathf.Max(0, -Mathf.FloorToInt(pos.y)), w.def.rarity, P, G.rng);
            if (nw == null || nw.def == w.def) nw = Crates.Roll(Mathf.Max(0, -Mathf.FloorToInt(pos.y)), w.def.rarity, P, G.rng);
            if (nw == null) return;
            foreach (var m in w.mods) if (!nw.Has(m) && nw.mods.Count < Mods.Max && Mods.Fits(nw.def, m)) nw.mods.Add(m);
            P.slots[P.cur] = nw;
            G.sfx.Play("buy", 0.8f, 1.2f);
            G.fx.Burst(P.pos, 24, Col, 6f, 0.5f, 0f);
            Done(nw.def.name + "!", Col);
        }

        protected override void Idle(float dt) { if (Random.value < 0.1f) G.fx.Burst(pos + new Vector2(Random.Range(-0.4f, 0.4f), 0.8f), 1, Col, 1f, 0.5f, -2f); }
    }

    // ALTAR OF DEATH: give up a heart of your health for good, and take a relic.
    public class DeathAltar : Shrine
    {
        public DeathAltar(int x, int y) : base(x, y, Art.AltarDeath) { }
        protected override string Title => "ALTAR OF DEATH";
        protected override string What => "GIVE ONE HEART OF MAX HEALTH FOR A RELIC";
        protected override Color Col => new Color(0.85f, 0.85f, 0.95f);

        protected override void Use()
        {
            if (P.s.maxHp <= Stats.Heart) { G.Popup(P.pos + Vector2.up, "YOU HAVE NOTHING TO GIVE", new Color(1f, 0.5f, 0.4f)); return; }
            P.s.maxHp -= Stats.Heart;
            P.s.hp = Mathf.Min(P.s.hp, P.s.maxHp);
            var d = DB.RollRare(Mathf.Max(0, -Mathf.FloorToInt(pos.y)) + 60, P, G.rng);
            if (d != null) G.Spawn(new LootPickup(pos + Vector2.up * 1.2f, d, 0.6f, true));
            G.sfx.Play("hurt", 0.8f, 0.6f);
            G.cam.Shake(0.3f);
            Done("-1 HEART", new Color(1f, 0.3f, 0.3f));
        }
    }

    // THE GAMBLER'S ALTAR: stake half your gold on a poker chip. Half the time it pays out double; otherwise it keeps it.
    public class GoldChalice : Shrine
    {
        public GoldChalice(int x, int y) : base(x, y, Art.PokerAltar) { }
        protected override string Title => "THE GAMBLER'S ALTAR";
        protected override string What => "STAKE HALF YOUR GOLD. HEADS IT PAYS DOUBLE";
        protected override Color Col => new Color(1f, 0.35f, 0.35f);

        protected override void Use()
        {
            int g = P.s.gold / 2;
            if (g < 10) { G.Popup(P.pos + Vector2.up, "NOT ENOUGH GOLD", new Color(1f, 0.5f, 0.4f)); return; }
            P.s.gold -= g;
            G.sfx.Play("coin", 0.8f, 0.7f);
            if (Chance.Roll("gamble", 0.5f))
            {
                G.SpawnGold(pos + Vector2.up * 1.2f, g * 2);
                G.sfx.Play("chest", 0.9f, 1.2f);
                Done("DOUBLE!", Col);
            }
            else
            {
                G.sfx.Play("deny", 0.6f, 0.6f);
                Done("THE HOUSE WINS", new Color(0.8f, 0.7f, 0.5f));
            }
        }

        protected override void Idle(float dt) { if (Random.value < 0.08f) G.fx.Burst(pos + new Vector2(Random.Range(-0.5f, 0.5f), 0.9f), 1, Col, 0.6f, 0.6f, -1f); }
    }

    // THE SLEEPING STONE DRAGON: too hard for picks and bullets. Land on its back hard (a ground pound, or a long
    // fall) three times and it cracks open and coughs up its hoard.
    public class StoneDragon : Shrine
    {
        int cracks;
        float hitCd;
        public StoneDragon(int x, int y) : base(x, y, Art.DragonStatue) { }
        protected override string Title => "THE SLEEPING STONE DRAGON";
        protected override string What => "LAND ON IT HARD: GROUND POUND, OR A LONG FALL";
        protected override Color Col => new Color(1f, 0.45f, 0.4f);

        Sprite[] anim;
        public override void Tick(float dt)
        {
            t += dt; hitCd -= dt;
            if (used) { sr.color = new Color(0.6f, 0.5f, 0.5f); return; }
            // it breathes in its sleep (if it has frames for that)
            if (anim == null)
            {
                var ex = SpriteSheet.Frames("dragonstatue");
                var l = new System.Collections.Generic.List<Sprite> { Art.DragonStatue };
                if (ex != null) l.AddRange(ex);
                anim = l.ToArray();
            }
            if (anim.Length > 1) sr.sprite = anim[((int)(t * 2.5f)) % anim.Length];
            if (Near(5f)) { G.hud.Label(pos + Vector2.up * 2.8f, Title, Col); G.hud.Label(pos + Vector2.up * 2.3f, What + "   " + cracks + "/3", new Color(0.9f, 0.9f, 0.9f)); }
            // a hard landing on its back
            var p = P;
            if (hitCd <= 0f && p.mode != Player.Mode.Dead && Mathf.Abs(p.pos.x - pos.x) < 1.8f && p.pos.y > pos.y && p.pos.y - pos.y < 2.6f
                && (p.mode == Player.Mode.Pound || p.vel.y < -14f))
            {
                hitCd = 0.6f;
                cracks++;
                G.cam.Shake(0.4f);
                G.sfx.Play("explode", 0.6f, 0.8f);
                G.fx.Burst(pos + Vector2.up, 20, new Color(0.7f, 0.6f, 0.55f), 6f, 0.5f, 20f);
                if (cracks >= 3) Use();
            }
        }

        protected override void Use()
        {
            used = true;
            G.RareReward(pos + Vector2.up * 1.5f);
            G.SpawnGold(pos + Vector2.up * 1.5f, 40 + Mathf.Max(0, -Mathf.FloorToInt(pos.y)) / 4);
            G.Popup(P.pos + Vector2.up, "ITS HOARD!", Col);
        }
    }
}
