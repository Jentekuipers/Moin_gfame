using UnityEngine;

namespace DigDeep
{
    public static class ShopBuilder
    {
        // Shops get better the deeper you go: normal, then PREMIUM (gilded, a second shelf), then LEGENDARY.
        public static int Tier(int depth, Player p, System.Random r)
        {
            float prem = p.Lucky(Mathf.Clamp(0.03f + (depth - 60) * 0.0018f, 0.03f, 0.8f));
            if (r.NextDouble() >= prem) return 0;
            float leg = Mathf.Clamp01((depth - 280) / 450f) * 0.6f;
            return r.NextDouble() < leg ? 2 : 1;
        }

        // the rarest of a few rolls (premium shelves are hand-picked); legendary shelves often carry a mythic
        static Def BestTrinket(int depth, Player p, System.Random r, int tier)
        {
            if (r.NextDouble() < (tier >= 2 ? 0.3f : 0.06f))
            {
                var mythic = new System.Collections.Generic.List<SkillDef>();
                foreach (var t in DB.Trinkets) if (t.rarity == Rarity.Relic && !t.ultra && p.SkillCount(t.id) < t.cap && DB.Unlocked(t) && !p.SeenOnce(t.id)) mythic.Add(t);
                if (mythic.Count > 0) return mythic[r.Next(mythic.Count)];
            }
            Def best = null;
            for (int i = 0; i < (tier >= 2 ? 5 : 3); i++)
            {
                var d = DB.RollTrinket(depth, p, r);
                if (d == null) continue;
                if (best == null || Rar(d) > Rar(best)) best = d;
            }
            return best;
        }

        static int Rar(Def d) => d is SkillDef sd ? (int)sd.rarity : d is AttackDef ad ? (int)ad.rarity : 0;

        // Shops only sell modified weapons: more modifiers the deeper you are (and in better shops). Some come cursed, a bit cheaper.
        static Weapon ShopWeapon(int depth, int tier, Player p, System.Random r)
        {
            var rar = Crates.RarityFor(depth + tier * 150, (float)r.NextDouble());
            if (tier >= 2 && rar < Rarity.Rare) rar = Rarity.Rare;
            var w = Crates.Roll(depth, rar, p, r);
            w.mods.Clear(); w.curse = Curse.None;
            int want = 1 + (depth > 200 ? 1 : 0) + (depth > 420 ? 1 : 0) + tier;
            want = Mathf.Clamp(want, 1, Mods.Max);
            for (int i = 0; i < want; i++) if (!Mods.AddRandom(w, r)) break;
            if (r.NextDouble() < 0.2f) { w.curse = Curses.Pick(r); w.curseKills = 0; }
            return w;
        }

        static Def Trinket(int depth, int tier, Player p, System.Random r) => tier > 0 ? BestTrinket(depth, p, r, tier) : DB.RollTrinket(depth, p, r);

        public static void Spawn(ShopRoom room, System.Random r)
        {
            var g = Game.I;
            var p = g.player;
            int standY = room.FloorY + 1;
            int depth = room.top;
            int tier = Tier(depth, p, r);
            bool premium = tier > 0;
            int roll = depth + tier * 150;   // better shelves stock as if you were much deeper
            g.Spawn(new Keeper(new Vector2(room.x0 + 2.5f, standY), tier, room.x0));
            if (premium)   // a premium shop is built of gilded brick: you can tell from the walls
                for (int gd = room.top - 3; gd <= room.top + ShopRoom.Ht; gd++)
                    for (int gx = room.x0 - 4; gx < room.x0 + ShopRoom.Wd + 4; gx++)
                        if (g.world.Get(gx, -gd) == Block.Brick) g.world.Set(gx, -gd, Block.GildBrick);

            // the shelf: a modded weapon, two trinkets (or a modifier), and the heal spot (half the time another trinket)
            int n = 4 + Mathf.Clamp(p.s.shopExtra, 0, 2);
            var all = new System.Collections.Generic.List<Def>();
            var weapons = new System.Collections.Generic.List<Weapon>();
            all.Add(null); weapons.Add(ShopWeapon(depth, tier, p, r));
            all.Add(Trinket(roll, tier, p, r));
            int k = r.Next(100);
            all.Add(k < (premium ? 30 : 35) ? DB.RollMod(p, r) : Trinket(roll, tier, p, r));
            all.Add(r.Next(2) == 0 ? (r.Next(2) == 0 ? DB.Ale : DB.ById.TryGetValue("stew", out var stew) ? stew : DB.Ale) : Trinket(roll, tier, p, r));
            for (int i = 4; i < n; i++) all.Add(Trinket(roll, tier, p, r));
            // a better shop has a second shelf above the first, its wares sitting in the gaps between the ones below
            int rows = premium ? 2 : 1;
            if (premium)
            {
                all.Add(null); weapons.Add(ShopWeapon(depth + 60, tier, p, r));
                for (int i = 1; i < n - 1; i++) all.Add(i == 2 && r.Next(3) == 0 ? DB.RollMod(p, r) : Trinket(roll, tier, p, r));
            }
            // never the same thing twice in one shop
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null && IsWeaponSlot(i, n)) continue;
                for (int tries = 0; tries < 12 && (all[i] == null || all.IndexOf(all[i]) < i); tries++) all[i] = Trinket(roll, tier, p, r);
                if (all[i] == null || all.IndexOf(all[i]) < i) all[i] = DB.Ale;
            }

            float gap = n <= 4 ? 3f : 12f / (n - 1);
            float priceMul = (1f + Mathf.Max(0, depth - 120) / 350f) * (tier == 2 ? 2.4f : tier == 1 ? 1.6f : 1f);   // deeper diggers carry more gold
            int wi = 0;
            for (int row = 0; row < rows; row++)
                for (int i = 0; i < (row == 0 ? n : n - 1); i++)
                {
                    int idx = row * n + i;
                    float x = room.x0 + 4.5f + (n <= 4 ? 1f : 0f) + i * gap + (row == 1 ? gap * 0.5f : 0f);
                    x = Mathf.Floor(x) + 0.5f;
                    int fy = standY + row * 2;   // the top shelf: two up, in between the ones below
                    // each offer sits on an indestructible steel plate that stays put even if the floor is blown away
                    g.world.Set(Mathf.FloorToInt(x), fy - 1, Block.Plate);
                    if (IsWeaponSlot(idx, n))
                    {
                        var w = weapons[wi++];
                        int wp = DB.Price(w.def, depth, p);
                        wp = Mathf.RoundToInt(wp * (1f + 0.35f * w.mods.Count) * (w.curse != Curse.None ? 0.7f : 1f) * priceMul / 5f) * 5;
                        g.Spawn(new Pedestal(new Vector2(x, fy), w, Mathf.Max(5, wp), tier) { shopX0 = room.x0, depth = depth, priceMul = priceMul });
                        continue;
                    }
                    var d = all[idx] ?? DB.Ale;
                    int price = Mathf.RoundToInt(DB.Price(d, depth, p) * priceMul / 5f) * 5;
                    g.Spawn(new Pedestal(new Vector2(x, fy), d, Mathf.Max(5, price), tier) { shopX0 = room.x0, depth = depth, priceMul = priceMul });
                }
        }

        static bool IsWeaponSlot(int idx, int n) => idx % n == 0;

        // MERCHANT'S LEDGER picked up inside a shop: one more ware appears on the bottom shelf right away
        public static void AddOffer(Player p)
        {
            var near = Pedestal.NearestShop(p.pos);
            if (near == null) return;
            var g = Game.I;
            float bestX = float.NaN, bestGap = 0f;
            for (float x = near.shopX0 + 3.5f; x <= near.shopX0 + ShopRoom.Wd - 1.5f; x += 1f)
            {
                float gap = 99f;
                foreach (var o in Pedestal.All) if (o.shopX0 == near.shopX0 && !o.dead) gap = Mathf.Min(gap, Mathf.Abs(o.pos.x - x) + (Mathf.Abs(o.BaseY - near.BaseY) > 0.5f ? 0.8f : 0f));
                if (gap > bestGap) { bestGap = gap; bestX = x; }
            }
            if (float.IsNaN(bestX) || bestGap < 1.2f) return;
            var d = DB.RollTrinket(near.depth, p, g.rng) ?? DB.Ale;
            int price = Mathf.Max(5, Mathf.RoundToInt(DB.Price(d, near.depth, p) * near.priceMul / 5f) * 5);
            int fy = Mathf.RoundToInt(near.BaseY);
            float lowest = near.BaseY;
            foreach (var o in Pedestal.All) if (o.shopX0 == near.shopX0) lowest = Mathf.Min(lowest, o.BaseY);
            fy = Mathf.RoundToInt(lowest);
            g.world.Set(Mathf.FloorToInt(bestX), fy - 1, Block.Plate);
            g.Spawn(new Pedestal(new Vector2(bestX, fy), d, price, near.Tier) { shopX0 = near.shopX0, depth = near.depth, priceMul = near.priceMul });
            g.fx.Burst(new Vector2(bestX, fy + 1f), 20, new Color(1f, 0.85f, 0.3f), 5f, 0.5f, 3f);
            g.sfx.Play("chest", 0.6f, 1.3f);
        }

        // MEMBER'S CARD picked up inside a shop: its prices drop on the spot
        public static void Reprice(Player p, float mul)
        {
            var near = Pedestal.NearestShop(p.pos);
            if (near == null) return;
            foreach (var o in Pedestal.All) if (o.shopX0 == near.shopX0 && !o.dead) o.MulPrice(mul);
        }
    }

    public class Pedestal : Entity, IRerollable
    {
        public static readonly System.Collections.Generic.List<Pedestal> All = new System.Collections.Generic.List<Pedestal>();
        Def def;
        Weapon weapon;   // a weapon for sale, with its modifiers (and maybe a curse)
        int price;

        // SPELL - REROLL CIRCLE: the ware turns into another of its kind and rarity (priced afresh)
        public void Reroll()
        {
            if (sold) return;
            var pl = G.player;
            if (weapon != null)
            {
                weapon = DigDeep.Reroll.Swap(weapon, pl);
                def = weapon.def;
                int wp = DB.Price(weapon.def, depth, pl);
                price = Mathf.Max(5, Mathf.RoundToInt(wp * (1f + 0.35f * weapon.mods.Count) * (weapon.curse != Curse.None ? 0.7f : 1f) * priceMul / 5f) * 5);
            }
            else
            {
                def = DigDeep.Reroll.Swap(def, pl);
                price = Mathf.Max(5, Mathf.RoundToInt(DB.Price(def, depth, pl) * priceMul / 5f) * 5);
            }
            sr.sprite = def.icon;
            pl?.MarkSeen(def);
        }
        public int shopX0 = int.MinValue, depth;
        public float priceMul = 1f;
        public float BaseY => baseY;
        public bool Sold => sold;
        public int Tier => tier;

        public void MulPrice(float m)
        {
            if (sold) return;
            price = Mathf.Max(5, Mathf.RoundToInt(price * m / 5f) * 5);
            G.fx.Burst(pos, 8, new Color(1f, 0.85f, 0.3f), 3f, 0.4f, 2f);
        }

        // the shop (its first pedestal) closest to p, within shouting distance
        public static Pedestal NearestShop(Vector2 p)
        {
            Pedestal best = null; float bd = 16f * 16f;
            foreach (var o in All)
            {
                if (o.dead || o.shopX0 == int.MinValue) continue;
                float d2 = (o.pos - p).sqrMagnitude;
                if (d2 < bd) { bd = d2; best = o; }
            }
            return best;
        }
        bool sold;
        float bob;
        readonly GameObject stand;
        readonly float baseY;

        readonly bool premium;
        readonly int tier;
        readonly GameObject halo;
        static readonly Color LegendC = new Color(0.8f, 0.5f, 1f);

        public Pedestal(Vector2 floorPos, Weapon w, int price, int tier) : this(floorPos, w.def, price, tier) { weapon = w; }

        public Pedestal(Vector2 floorPos, Def d, int price, int tier = 0)
        {
            def = d; this.price = price; this.tier = tier; premium = tier > 0;
            All.Add(this);
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
                ssr.color = tier >= 2 ? new Color(0.85f, 0.6f, 1f) : new Color(1f, 0.82f, 0.35f);
                halo = new GameObject("PremiumHalo");
                halo.transform.SetParent(G.runRoot, false);
                var hs = Art.AddSR(halo, Art.Glow, 8);
                hs.color = tier >= 2 ? new Color(0.75f, 0.45f, 1f, 0.35f) : new Color(1f, 0.8f, 0.3f, 0.3f);
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
                if (Random.value < dt * 5f) G.fx.Burst(pos + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.4f, 0.6f)), 1, tier >= 2 ? LegendC : new Color(1f, 0.9f, 0.45f), 0.6f, 0.6f, -1f);
            }
            G.hud.Label(pos + Vector2.up * 0.75f, price + "G", pl.s.gold >= price ? (premium ? new Color(1f, 0.95f, 0.55f) : new Color(1f, 0.85f, 0.3f)) : new Color(0.7f, 0.4f, 0.35f));
            if (pl.mode == Player.Mode.Dead) return;
            bool near0 = Mathf.Abs(pl.pos.x - pos.x) < 0.8f && Mathf.Abs(pl.pos.y - (baseY + Player.Half.y)) < 1.2f;
            bool hov0 = G.MouseOver(pos, 0.7f);
            // one text box at a time across everything you could take (shop, floor, boss rewards)
            PickFocus.Offer(this, hov0, near0, Mathf.Abs(pl.pos.x - pos.x) + Mathf.Abs(pl.pos.y - baseY) * 0.5f);
            bool mine = PickFocus.Is(this);
            bool near = near0 && mine, hov = hov0 && mine;
            if (!near && hov) Describe("", pl, true);
            if (near)
            {
                Describe("PRESS " + GameInput.Prompt(Btn.Interact), pl, hov);
                if (GameInput.Take(Btn.Interact))
                {
                    if (pl.s.gold < price)
                    {
                        G.sfx.Play("deny", 0.6f);
                        G.Popup(pos + Vector2.up * 0.5f, "NEED " + (price - pl.s.gold) + " MORE GOLD", new Color(1f, 0.5f, 0.4f));
                        return;
                    }
                    pl.s.gold -= price;
                    if (weapon != null) pl.TakeWeapon(weapon);
                    else if (!pl.Grant(def)) { pl.s.gold += price; return; }
                    G.sfx.Play("buy", 0.8f);
                    G.fx.Burst(pos, 24, new Color(1f, 0.85f, 0.3f), 6f, 0.6f, 5f);
                    sold = true;
                    sr.enabled = false;
                    modGlow?.Kill();
                    if (halo != null) Object.Destroy(halo);
                }
            }
        }

        void Describe(string action, Player pl, bool hovered)
        {
            if (weapon != null) G.hud.WeaponLabel(pos + Vector2.up * 1.4f, weapon, action, pl, hovered);
            else G.hud.ItemLabel(pos + Vector2.up * 1.4f, def, action, pl, hovered);
        }

        public override void AddLight(Lighting l) { if (!sold) l.Add(pos, premium ? 2.6f : 1.8f, 0.6f); }

        public override void Kill()
        {
            base.Kill();
            All.Remove(this);
            if (stand != null) Object.Destroy(stand);
            if (halo != null) Object.Destroy(halo);
            modGlow?.Kill();
        }
    }

    // The shopkeeper: stands behind the counter, but he's flesh and blood. He falls if the floor goes,
    // and blasts, bullets, swings, boulders, fire and lava can kill him.
    public class Keeper : Entity
    {
        public static readonly System.Collections.Generic.List<Keeper> All = new System.Collections.Generic.List<Keeper>();
        static readonly string[] Lines = { "WELCOME, DIGGER!", "FINE WARES, FAIR PRICES", "GOLD TALKS, DIGGER", "MIND THE TNT" };
        static readonly string[] PremiumLines = { "PREMIUM WARES ONLY", "ONLY THE FINEST, DIGGER", "RARE GOODS, RARE PRICES" };
        static readonly string[] LegendLines = { "LEGENDS FOR SALE", "FEW GET THIS DEEP, DIGGER", "PRICELESS. ALMOST" };
        static readonly Vector2 H = new Vector2(0.3f, 0.45f);
        readonly string line;
        readonly bool premium;
        readonly int tier;
        float hp = 30f, vy, hurtCd, flashT, burnT, fallFrom;
        bool grounded = true;

        // he walks his shop: up to a ware, points at it ("can I interest you...?"), then on to the next
        readonly float minX, maxX;
        readonly bool roams;
        float walkT, pointT, idleT = 1f, goalX;
        Pedestal pointAt;
        static readonly string Pitch = "CAN I INTEREST YOU IN THIS ITEM?";

        public Keeper(Vector2 floorPos, int tier = 0, int shopX0 = int.MinValue)
        {
            this.tier = tier; premium = tier > 0;
            roams = shopX0 != int.MinValue;
            minX = shopX0 + 1.5f; maxX = shopX0 + ShopRoom.Wd - 1.5f;
            goalX = floorPos.x;
            pos = new Vector2(floorPos.x, floorPos.y + H.y);
            fallFrom = pos.y;
            MakeGO("Shopkeeper", premium && Art.KeeperOld != null ? Art.KeeperOld : Art.Keeper, 11);
            string[] pool = tier >= 2 ? LegendLines : premium ? PremiumLines : Lines;
            line = pool[Random.Range(0, pool.Length)];
            if (!premium && Art.Keeper != Art.KeeperOld) go.transform.localScale = new Vector3(1.25f, 1.25f, 1f);   // the regular keeper stands a bit taller
            if (tier >= 2) sr.color = new Color(0.9f, 0.8f, 1f);
            Place(-H.y);
            All.Add(this);
        }

        float Top => sr != null && sr.sprite != null ? sr.sprite.bounds.size.y * go.transform.localScale.y : 1.5f;

        public override void Tick(float dt)
        {
            var pl = G.player;
            hurtCd -= dt; flashT -= dt; burnT -= dt;
            // gravity: he falls like anything else when the floor goes
            vy = Mathf.Max(vy - 38f * dt, -18f);
            Vector2 p = pos;
            if (Wd.MoveY(ref p, H, vy * dt, out _))
            {
                if (vy < 0f)
                {
                    float fell = fallFrom - p.y;
                    if (!grounded && fell > 4f) Hurt((fell - 4f) * 3f, p + Vector2.down);
                    grounded = true;
                }
                vy = 0f;
            }
            else grounded = false;
            pos = p;
            if (grounded || vy > 0f) fallFrom = pos.y;
            if (dead) return;
            // fire and lava hurt him too
            int cx = Mathf.FloorToInt(pos.x), cy = Mathf.FloorToInt(pos.y);
            if (Fire.IsBurning(cx, cy) || Fire.IsBurning(cx, cy - 1)) burnT = 2f;
            if (burnT > 0f) { Hurt(4f * dt, pos, true); if (Random.value < 0.3f) G.fx.Burst(pos, 1, new Color(1f, 0.6f, 0.2f), 1f, 0.3f, -3f); }
            if (Wd.FluidAtPoint(pos - new Vector2(0f, H.y * 0.8f)) == Fluids.Lava) Hurt(30f * dt, pos, true);
            if (dead) return;

            Wander(dt, pl);
            sr.color = flashT > 0f ? new Color(1f, 0.45f, 0.45f) : tier >= 2 ? new Color(0.9f, 0.8f, 1f) : Color.white;
            Place(-H.y);
            bool near = (pl.pos - pos).sqrMagnitude < 25f;
            var lc = tier >= 2 ? new Color(0.85f, 0.6f, 1f) : premium ? new Color(1f, 0.85f, 0.3f) : new Color(0.6f, 1f, 0.6f);
            if (pointAt != null && pointT > 0f && (pl.pos - pos).sqrMagnitude < 4.5f * 4.5f) G.hud.Label(pos + Vector2.up * (Top - H.y + 1.9f), Pitch, lc);
            else if (near && pointAt == null && walkT <= 0f && !roams) G.hud.Label(pos + Vector2.up * (Top - H.y + 1.9f), line, lc);
            if (premium && Random.value < dt * 3f) G.fx.Burst(pos + new Vector2(Random.Range(-1f, 14f), Random.Range(0f, 4f)), 1, tier >= 2 ? new Color(0.8f, 0.5f, 1f) : new Color(1f, 0.85f, 0.35f), 0.5f, 0.8f, -0.5f);
        }

        void Wander(float dt, Player pl)
        {
            var baseSpr = premium && Art.KeeperOld != null ? Art.KeeperOld : Art.Keeper;
            if (!roams || !grounded)
            {
                sr.sprite = baseSpr;
                sr.flipX = pl.pos.x < pos.x;
                return;
            }
            if (pointAt != null && (pointAt.dead || pointAt.Sold)) { pointAt = null; pointT = 0f; idleT = 0.6f; }
            if (pointT > 0f)
            {
                // holding his hand out at the ware
                pointT -= dt;
                sr.sprite = baseSpr;
                if (pointAt != null) sr.flipX = pointAt.pos.x < pos.x;
                if (pointT <= 0f) { pointAt = null; idleT = Random.Range(0.6f, 1.6f); }
                return;
            }
            if (idleT > 0f)
            {
                idleT -= dt;
                sr.sprite = baseSpr;
                sr.flipX = pl.pos.x < pos.x;
                if (idleT <= 0f) PickWare(pl);
                return;
            }
            // walking over to it
            float dx = goalX - pos.x;
            if (Mathf.Abs(dx) < 0.08f)
            {
                pos.x = goalX;
                if (pointAt != null) { pointT = Random.Range(2f, 3f); G.sfx.Play("swing", 0.15f, 0.7f); }
                else idleT = Random.Range(0.8f, 2f);
                return;
            }
            float step = Mathf.Sign(dx) * Mathf.Min(Mathf.Abs(dx), 1.7f * dt);
            Vector2 p = pos;
            if (Wd.MoveX(ref p, H, step, out _)) { goalX = pos.x; idleT = 1f; pointAt = null; return; }
            pos = p;
            walkT += dt;
            var walk = SpriteSheet.Frames("keeperwalk");
            sr.sprite = walk != null && walk.Length > 0 && !premium ? walk[((int)(walkT * 7f)) % walk.Length] : baseSpr;
            sr.flipX = dx < 0f;
        }

        // the ware nearest to you (or any, when you're not about), and a spot beside it to stand
        void PickWare(Player pl)
        {
            Pedestal best = null; float bd = float.MaxValue;
            bool playerHere = Mathf.Abs(pl.pos.y - pos.y) < 6f && pl.pos.x > minX - 3f && pl.pos.x < maxX + 3f;
            foreach (var o in Pedestal.All)
            {
                if (o.dead || o.Sold || o.pos.x < minX - 1f || o.pos.x > maxX + 1f || Mathf.Abs(o.BaseY - (pos.y - H.y)) > 4f) continue;
                float d = playerHere ? Mathf.Abs(o.pos.x - pl.pos.x) + Random.value * 1.5f : Random.value * 10f;
                if (o == pointAt) d += 5f;
                if (d < bd) { bd = d; best = o; }
            }
            pointAt = best;
            if (best == null) { goalX = Random.Range(minX, maxX); return; }
            float side = pos.x < best.pos.x ? -1f : 1f;
            goalX = Mathf.Clamp(best.pos.x + side * 1.1f, minX, maxX);
        }

        public void Hurt(float d, Vector2 from, bool dot = false)
        {
            if (dead || d <= 0f) return;
            if (!dot)
            {
                if (hurtCd > 0f) return;
                hurtCd = 0.15f;
                flashT = 0.12f;
                G.fx.Burst(pos, 6, new Color(0.8f, 0.15f, 0.15f), 3f, 0.3f, 10f);
                G.sfx.Play("hurt", 0.4f, 0.8f);
                if (hp - d > 0f) G.Popup(pos + Vector2.up * 1.2f, "HEY!", new Color(1f, 0.5f, 0.4f));
            }
            hp -= d;
            if (hp <= 0f) Die();
        }

        void Die()
        {
            G.fx.Burst(pos, 26, new Color(0.75f, 0.1f, 0.1f), 6f, 0.6f, 12f);
            G.sfx.Play("death", 0.5f, 0.8f);
            G.SpawnGold(pos, 15 + tier * 20);   // his till spills
            Kill();
        }

        // anything that hurts in an area (a blast, a swing, a bullet) checks for keepers in it
        public static bool HitAt(Vector2 c, Vector2 h, float dmg, Vector2 from)
        {
            bool any = false;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var k = All[i];
                if (k.dead || !U.Overlap(c, h, k.pos, H)) continue;
                k.Hurt(dmg, from);
                any = true;
            }
            return any;
        }

        public override void Kill()
        {
            base.Kill();
            All.Remove(this);
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
