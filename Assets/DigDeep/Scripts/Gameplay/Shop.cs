using UnityEngine;

namespace DigDeep
{
    public static class ShopBuilder
    {
        // A PREMIUM shop: gilded, rarer (and pricier) wares. More and more common the deeper you go.
        public static float PremiumChance(int depth, Player p) => Mathf.Min(0.45f, p.Lucky(0.04f + depth * 0.0007f));

        // the rarest of a few rolls (premium shelves are hand-picked)
        static Def BestTrinket(int depth, Player p, System.Random r)
        {
            if (r.NextDouble() < 0.12f)
            {
                var mythic = new System.Collections.Generic.List<SkillDef>();
                foreach (var t in DB.Trinkets) if (t.rarity == Rarity.Relic && p.SkillCount(t.id) < t.cap && DB.Unlocked(t) && !p.SeenOnce(t.id)) mythic.Add(t);
                if (mythic.Count > 0) return mythic[r.Next(mythic.Count)];
            }
            Def best = null;
            for (int i = 0; i < 3; i++)
            {
                var d = DB.RollTrinket(depth, p, r);
                if (d == null) continue;
                if (best == null || Rar(d) > Rar(best)) best = d;
            }
            return best;
        }

        static int Rar(Def d) => d is SkillDef sd ? (int)sd.rarity : d is AttackDef ad ? (int)ad.rarity : 0;

        public static void Spawn(ShopRoom room, System.Random r)
        {
            var g = Game.I;
            var p = g.player;
            int standY = room.FloorY + 1;
            int depth = room.top;
            bool premium = r.NextDouble() < PremiumChance(depth, p);
            int roll = premium ? depth + 150 : depth;   // premium shelves stock as if you were much deeper
            g.Spawn(new Keeper(new Vector2(room.x0 + 2.5f, standY), premium));

            Def[] offers = new Def[4];
            offers[0] = DB.RollAttack(roll, p, r);   // specials never change, so shops don't sell them
            offers[1] = premium ? BestTrinket(roll, p, r) : DB.RollTrinket(depth, p, r);
            int k = r.Next(100);
            // sometimes a weapon modifier: it goes onto whatever you're holding when you buy it
            offers[2] = premium ? (k < 30 ? DB.RollMod(p, r) : BestTrinket(roll, p, r))
                                : k < 35 ? DB.RollMod(p, r) : k < 65 ? DB.RollTrinket(depth, p, r) : DB.RollItem(depth, p, r);
            offers[3] = DB.Upgrades[r.Next(DB.Upgrades.Count)];
            if (offers[1] != null && offers[2] == offers[1]) offers[2] = DB.RollItem(roll, p, r);
            // MERCHANT'S LEDGER: more on the shelves (a trinket or an item each)
            int n = 4 + Mathf.Clamp(p.s.shopExtra, 0, 2);
            var all = new System.Collections.Generic.List<Def>(offers);
            for (int i = 4; i < n; i++) all.Add(r.Next(2) == 0 ? (premium ? BestTrinket(roll, p, r) : DB.RollTrinket(depth, p, r)) : DB.RollItem(roll, p, r));
            // never the same thing twice on one shelf: duplicates are swapped for something else
            for (int i = 0; i < n; i++)
            {
                if (all[i] == null) all[i] = DB.RollItem(roll, p, r);
                for (int tries = 0; tries < 12 && all[i] != null && all.IndexOf(all[i]) < i; tries++)
                {
                    int pick = (i + tries) % 3;
                    all[i] = pick == 0 ? (premium ? BestTrinket(roll, p, r) : DB.RollTrinket(depth, p, r))
                           : pick == 1 ? DB.RollItem(roll, p, r) : DB.RollMod(p, r);
                }
                if (all[i] != null && all.IndexOf(all[i]) < i) all[i] = DB.Upgrades[r.Next(DB.Upgrades.Count)];
            }
            float gap = n <= 4 ? 3f : 12f / (n - 1);
            for (int i = 0; i < n; i++)
            {
                var d = all[i] ?? DB.RollItem(depth, p, r);
                float x = room.x0 + 4.5f + (n <= 4 ? 1f : 0f) + i * gap;
                // each offer sits on an indestructible steel plate that stays put even if the floor is blown away
                g.world.Set(Mathf.FloorToInt(x), standY - 1, Block.Plate);
                int price = DB.Price(d, depth, p);
                if (premium) price = Mathf.RoundToInt(price * 1.6f);
                g.Spawn(new Pedestal(new Vector2(x, standY), d, price, premium));
            }
        }
    }

    public class Pedestal : Entity
    {
        readonly Def def;
        readonly int price;
        bool sold;
        float bob;
        readonly GameObject stand;
        readonly float baseY;

        readonly bool premium;
        readonly GameObject halo;

        public Pedestal(Vector2 floorPos, Def d, int price, bool premium = false)
        {
            def = d; this.price = price; this.premium = premium;
            G.player?.MarkSeen(d);
            baseY = floorPos.y;
            pos = new Vector2(floorPos.x, floorPos.y + 1.0f);
            MakeGO("Offer", d.icon, 9);
            stand = new GameObject("Pedestal");
            stand.transform.SetParent(G.runRoot, false);
            stand.transform.position = new Vector3(floorPos.x, floorPos.y, 0);
            var ssr = Art.AddSR(stand, Art.Pedestal, 5);
            bob = Random.value * 5f;
            if (premium)
            {
                // gilded stand, a golden halo behind the ware
                ssr.color = new Color(1f, 0.82f, 0.35f);
                halo = new GameObject("PremiumHalo");
                halo.transform.SetParent(G.runRoot, false);
                var hs = Art.AddSR(halo, Art.Glow, 8);
                hs.color = new Color(1f, 0.8f, 0.3f, 0.3f);
                halo.transform.localScale = new Vector3(2.2f, 2.2f, 1f);
                halo.transform.position = new Vector3(pos.x, pos.y, 0f);
            }
            if (d is ModDef) modGlow = ModGlow.Make(pos);
        }

        readonly ModGlow modGlow;

        public override void Tick(float dt)
        {
            if (sold) return;
            bob += dt;
            var pl = G.player;
            go.transform.position = new Vector3(pos.x, U.Snap(pos.y + Mathf.Sin(bob * 2.5f) * 0.08f), 0);
            modGlow?.Tick(pos, sr);
            if (premium)
            {
                if (halo != null) halo.transform.position = new Vector3(pos.x, pos.y + Mathf.Sin(bob * 2.5f) * 0.08f, 0f);
                if (Random.value < dt * 5f) G.fx.Burst(pos + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.4f, 0.6f)), 1, new Color(1f, 0.9f, 0.45f), 0.6f, 0.6f, -1f);
            }
            G.hud.Label(pos + Vector2.up * 0.75f, price + "G", pl.s.gold >= price ? (premium ? new Color(1f, 0.95f, 0.55f) : new Color(1f, 0.85f, 0.3f)) : new Color(0.7f, 0.4f, 0.35f));
            if (pl.mode == Player.Mode.Dead) return;
            bool near = Mathf.Abs(pl.pos.x - pos.x) < 0.8f && Mathf.Abs(pl.pos.y - (baseY + Player.Half.y)) < 1.2f;
            if (!near && G.MouseOver(pos, 0.7f)) G.hud.ItemLabel(pos + Vector2.up * 1.4f, def, "WALK UP, PRESS E TO BUY", pl, true);
            if (near)
            {
                G.hud.ItemLabel(pos + Vector2.up * 1.4f, def, "PRESS E TO BUY", pl, G.MouseOver(pos, 0.7f));
                if (GameInput.Take(Btn.Interact))
                {
                    if (pl.s.gold < price)
                    {
                        G.sfx.Play("deny", 0.6f);
                        G.Popup(pos + Vector2.up * 0.5f, "NEED " + (price - pl.s.gold) + " MORE GOLD", new Color(1f, 0.5f, 0.4f));
                        return;
                    }
                    pl.s.gold -= price;
                    if (!pl.Grant(def)) { pl.s.gold += price; return; }
                    G.sfx.Play("buy", 0.8f);
                    G.fx.Burst(pos, 24, new Color(1f, 0.85f, 0.3f), 6f, 0.6f, 5f);
                    sold = true;
                    sr.enabled = false;
                    modGlow?.Kill();
                    if (halo != null) Object.Destroy(halo);
                }
            }
        }

        public override void AddLight(Lighting l) { if (!sold) l.Add(pos, premium ? 2.6f : 1.8f, 0.6f); }

        public override void Kill()
        {
            base.Kill();
            if (stand != null) Object.Destroy(stand);
            if (halo != null) Object.Destroy(halo);
            modGlow?.Kill();
        }
    }

    public class Keeper : Entity
    {
        static readonly string[] Lines = { "WELCOME, DIGGER!", "FINE WARES, FAIR PRICES", "GOLD TALKS, DIGGER", "MIND THE TNT" };
        static readonly string[] PremiumLines = { "PREMIUM WARES ONLY", "ONLY THE FINEST, DIGGER", "RARE GOODS, RARE PRICES" };
        readonly string line;
        readonly bool premium;

        public Keeper(Vector2 floorPos, bool premium = false)
        {
            this.premium = premium;
            pos = new Vector2(floorPos.x, floorPos.y + Player.Half.y);
            MakeGO("Shopkeeper", Art.Keeper, 11);
            line = premium ? PremiumLines[Random.Range(0, PremiumLines.Length)] : Lines[Random.Range(0, Lines.Length)];
            go.transform.position = new Vector3(floorPos.x, floorPos.y, 0);
            if (premium) sr.color = new Color(1f, 0.9f, 0.6f);   // in his Sunday best
        }

        public override void Tick(float dt)
        {
            var pl = G.player;
            sr.flipX = pl.pos.x < pos.x;
            if ((pl.pos - pos).sqrMagnitude < 25f) G.hud.Label(pos + Vector2.up * 1.3f, line, premium ? new Color(1f, 0.85f, 0.3f) : new Color(0.6f, 1f, 0.6f));
            if (premium && (pl.pos - pos).sqrMagnitude < 81f) G.hud.Label(pos + Vector2.up * 1.9f, "* PREMIUM SHOP *", new Color(1f, 0.95f, 0.55f));
            if (premium && Random.value < dt * 3f) G.fx.Burst(pos + new Vector2(Random.Range(-1f, 14f), Random.Range(0f, 4f)), 1, new Color(1f, 0.85f, 0.35f), 0.5f, 0.8f, -0.5f);
        }

        public override void AddLight(Lighting l) => l.Add(pos + Vector2.up * 1.5f, (premium ? 11f : 9f) + Mathf.Sin(Time.time * 3f) * 0.2f, 0.95f);
    }

    // The hot-pink halo and sparkles that make a weapon modifier impossible to miss.
    public class ModGlow
    {
        readonly GameObject go;
        readonly SpriteRenderer sr;

        ModGlow(Vector2 at)
        {
            go = new GameObject("ModGlow");
            go.transform.SetParent(Game.I.runRoot, false);
            sr = Art.AddSR(go, Art.Glow, 101);
            go.transform.localScale = new Vector3(2.6f, 2.6f, 1f);
            go.transform.position = new Vector3(at.x, at.y, 0f);
        }

        public static ModGlow Make(Vector2 at) => new ModGlow(at);

        public void Tick(Vector2 at, SpriteRenderer icon)
        {
            if (go == null) return;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var c = Mods.Signature;
            sr.color = new Color(c.r, c.g, c.b, 0.25f + 0.2f * k);
            if (icon != null) icon.color = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.95f), 0.4f * k);
            if (Random.value < 0.2f)
                Game.I.fx.Burst(at + new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-0.3f, 0.5f)), 1, Mods.Ui, 0.7f, 0.5f, -1f);
        }

        public void Kill() { if (go != null) Object.Destroy(go); }
    }
}
