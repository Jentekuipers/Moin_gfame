using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // SPELL - REROLL CIRCLE, and everything that can be rerolled. Each thing turns into another of the SAME kind
    // (trinket -> trinket, weapon -> weapon, consumable -> consumable, foe -> foe, object -> object), the same rarity,
    // and never into what it already was. Weapons keep their number of modifiers, but which ones is rerolled too.
    // Bosses, blocks, altars and shrines are left alone.
    //
    // Adding new things later: put a new foe in Foes, a new loose object block in ObjectBlocks, and a new entity
    // that should reroll gets an IRerollable (it decides for itself what it turns into).
    public interface IRerollable { void Reroll(); }

    public static class Reroll
    {
        // every foe of the deep that can stand in for another (x, y, depth). Not bosses.
        public static readonly List<System.Func<int, int, int, Enemy>> Foes = new List<System.Func<int, int, int, Enemy>>
        {
            (x, y, d) => new Crawler(x, y, d, false), (x, y, d) => new Crawler(x, y, d, true), (x, y, d) => new Crawler(x, y, d, false, true),
            (x, y, d) => new Bat(x, y, d, false), (x, y, d) => new Bat(x, y, d, true), (x, y, d) => new Mole(x, y, d), (x, y, d) => new Spitter(x, y, d),
            (x, y, d) => new Raider(x, y, d, Raider.Kind.Gunner), (x, y, d) => new Raider(x, y, d, Raider.Kind.Brute),
            (x, y, d) => new Raider(x, y, d, Raider.Kind.Marksman), (x, y, d) => new Raider(x, y, d, Raider.Kind.Bomber),
            (x, y, d) => new Beetle(x, y, d), (x, y, d) => new Slime(x, y, d), (x, y, d) => new Turtle(x, y, d), (x, y, d) => new Skeleton(x, y, d),
            (x, y, d) => new GliderBat(x, y, d), (x, y, d) => new Wolf(x, y, d), (x, y, d) => new RockMage(x, y, d), (x, y, d) => new MushroomElite(x, y, d),
            (x, y, d) => new VoidMage(x, y, d, VoidMage.Kind.Fire), (x, y, d) => new VoidMage(x, y, d, VoidMage.Kind.Ice), (x, y, d) => new VoidMage(x, y, d, VoidMage.Kind.Lightning),
            (x, y, d) => new Bee(new Vector2(x + 0.5f, y + 0.5f), d), (x, y, d) => new Wisp(x, y, d, false), (x, y, d) => new Wisp(x, y, d, true),
            (x, y, d) => new MagmaSlug(x, y, d), (x, y, d) => new ToxicToad(x, y, d), (x, y, d) => new SporePuff(x, y, d), (x, y, d) => new SparkBeetle(x, y, d),
            (x, y, d) => new Mimic(new Vector2(x + 0.5f, y + 0.5f), d), (x, y, d) => new Snapper(x, y, d), (x, y, d) => new SeedPod(x, y, d),
            (x, y, d) => new Clank(x, y, d), (x, y, d) => new Drone(x, y, d), (x, y, d) => new TeslaCoil(x, y, d), (x, y, d) => new Worm(x, y, d),
        };
        // ones that only live in water: only picked for a foe that's in water
        public static readonly List<System.Func<int, int, int, Enemy>> WaterFoes = new List<System.Func<int, int, int, Enemy>>
        {
            (x, y, d) => new Fish(x, y, d, Fish.Kind.Minnow), (x, y, d) => new Fish(x, y, d, Fish.Kind.Biter), (x, y, d) => new Fish(x, y, d, Fish.Kind.Grouper),
            (x, y, d) => new Fish(x, y, d, Fish.Kind.Glow), (x, y, d) => new Fish(x, y, d, Fish.Kind.Gold),
            (x, y, d) => new Eel(x, y, d),
        };

        // loose objects that sit in the world as blocks (any of them can become any other)
        public static readonly List<Block> ObjectBlocks = new List<Block> { Block.TNT, Block.Boulder, Block.Volatile, Block.Supply };
        // containers: a chest can become a weapon crate and the other way round
        public static readonly List<Block> Containers = new List<Block> { Block.Chest, Block.Crate };

        // ------------------------------------------------------------------ what something becomes

        public static Def Swap(Def d, Player p)
        {
            var pool = new List<Def>();
            switch (d)
            {
                case SkillDef k:
                    foreach (var t in DB.Trinkets)
                        if (t != k && t.rarity == k.rarity && t.ultra == k.ultra && DB.Unlocked(t) && t.owner != "_boss" && (p == null || p.SkillCount(t.id) < t.cap)) pool.Add(t);
                    break;
                case AttackDef a:
                    foreach (var t in DB.Attacks) if (t != a && t.rarity == a.rarity && t != DB.Pickaxe && Unlocks.Open(t)) pool.Add(t);
                    break;
                case ItemDef it:
                    foreach (var t in DB.Items) if (t != it) pool.Add(t);
                    break;
                case ModDef m:
                    foreach (var t in DB.ModDefs) if (t != m) pool.Add(t);
                    break;
                case SpecialDef s:
                    foreach (var t in DB.Specials) if (t != s) pool.Add(t);
                    break;
                case UpgradeDef u:
                    foreach (var t in DB.Upgrades) if (t != u) pool.Add(t);
                    break;
            }
            return pool.Count == 0 ? d : pool[Random.Range(0, pool.Count)];
        }

        public static Weapon Swap(Weapon w, Player p)
        {
            var nd = Swap(w.def, p) as AttackDef ?? w.def;
            var nw = new Weapon(nd) { curse = w.curse, curseKills = w.curseKills, ammoItem = w.ammoItem };
            var rng = new System.Random(Random.Range(0, int.MaxValue));
            for (int i = 0; i < w.mods.Count; i++) if (!Mods.AddRandom(nw, rng)) break;
            return nw;
        }

        static Enemy SwapFoe(Enemy e)
        {
            int x = Mathf.FloorToInt(e.pos.x), y = Mathf.FloorToInt(e.pos.y - e.half.y + 0.05f);
            int depth = Mathf.Max(0, -y);
            var pool = new List<System.Func<int, int, int, Enemy>>(Foes);
            if (Game.I.world.FluidAtPoint(e.pos) == Fluids.Water) pool.AddRange(WaterFoes);
            for (int tries = 0; tries < 12 && pool.Count > 0; tries++)
            {
                int i = Random.Range(0, pool.Count);
                Enemy n = null;
                try { n = pool[i](x, y, depth); } catch { n = null; }
                if (n == null) { pool.RemoveAt(i); continue; }
                if (n.GetType() == e.GetType() && n.kindKey == e.kindKey) { n.Kill(); pool.RemoveAt(i); continue; }   // never the same thing again
                return n;
            }
            return null;
        }

        // ------------------------------------------------------------------ the spell

        public static void Circle(Vector2 c, float r)
        {
            var g = Game.I;
            var p = g.player;
            g.fx.Ring(c, r, new Color(0.75f, 0.45f, 1f));
            g.fx.Ring(c, r * 0.6f, new Color(1f, 0.8f, 1f));
            g.sfx.Play("holy", 0.5f, 1.6f);
            g.sfx.Play("chest", 0.6f, 1.4f);
            float r2 = r * r;
            var list = new List<Entity>(g.ents);
            foreach (var e in list)
            {
                if (e.dead || (e.pos - c).sqrMagnitude > r2) continue;
                Vector2 at = e.pos;
                switch (e)
                {
                    case LootPickup lp:
                        lp.Kill();
                        g.Spawn(new LootPickup(at, Swap(lp.def, p), 0.6f));
                        break;
                    case WeaponPickup wp:
                        wp.Kill();
                        g.Spawn(new WeaponPickup(at, Swap(wp.w, p), 0.6f));
                        break;
                    case IRerollable rr:
                        rr.Reroll();
                        break;
                    case BoulderBody bb:
                        {
                            int bx = Mathf.FloorToInt(at.x), by = Mathf.FloorToInt(at.y);
                            bb.Kill();
                            if (g.world.Get(bx, by) == Block.Air) g.world.Set(bx, by, Other(Block.Boulder));
                            break;
                        }
                    case Enemy en:
                        if (en is Boss || en is WormSegment || en is CreatorHand || en.Intangible) break;
                        var nf = SwapFoe(en);
                        if (nf == null) break;
                        en.Kill();   // (gone without a trace: no gold, no corpse)
                        if (en.enraged) nf.Enrage();
                        g.Spawn(nf);
                        break;
                    default: continue;
                }
                g.fx.Burst(at, 14, new Color(0.8f, 0.55f, 1f), 4f, 0.5f, -2f);
            }
            // loose objects lying in the world as blocks
            int x0 = Mathf.FloorToInt(c.x - r), x1 = Mathf.CeilToInt(c.x + r), y0 = Mathf.FloorToInt(c.y - r), y1 = Mathf.CeilToInt(c.y + r);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 cc = new Vector2(x + 0.5f, y + 0.5f);
                    if ((cc - c).sqrMagnitude > r2) continue;
                    var b = g.world.Get(x, y);
                    if (g.world.Anchored(x, y)) continue;
                    if (Containers.Contains(b))
                    {
                        var nb = b == Block.Chest ? Block.Crate : Block.Chest;
                        if (nb == Block.Crate) g.world.SetCrateRarity(x, y, Crates.RarityAt(x, y));
                        g.world.Set(x, y, nb);
                    }
                    else if (ObjectBlocks.Contains(b)) g.world.Set(x, y, Other(b));
                    else continue;
                    g.fx.Burst(cc, 10, new Color(0.8f, 0.55f, 1f), 3f, 0.4f, 0f);
                }
        }

        static Block Other(Block b)
        {
            var pool = new List<Block>();
            foreach (var o in ObjectBlocks) if (o != b) pool.Add(o);
            return pool.Count == 0 ? b : pool[Random.Range(0, pool.Count)];
        }
    }

    // THE FUSE ALTAR: no name, no words. Offer it the thing in your hands... and your weapon will throw that thing,
    // forever, instead of what it used to fire.
    public class FuseAltar : Shrine
    {
        public FuseAltar(int x, int y) : base(x, y, Sprite()) { }
        protected override string Title => "";
        protected override string What => "";
        protected override Color Col => new Color(0.6f, 0.3f, 0.9f);

        static Sprite spr;
        static Sprite Sprite()
        {
            if (spr != null) return spr;
            var pal = new Dictionary<char, Color32>
            {
                { 'k', U.C(20, 14, 30) }, { 'd', U.C(46, 36, 62) }, { 's', U.C(78, 66, 98) }, { 'l', U.C(110, 96, 132) },
                { 'm', U.C(150, 60, 220) }, { 'M', U.C(220, 150, 255) }, { 'r', U.C(170, 30, 60) },
            };
            spr = Art.Make(new[]
            {
                "....kkkkkk....",
                "...kdddddddk..",
                "..kdsssssssdk.",
                "..kdsmmmmmsdk.",
                "..kdsmMMMmsdk.",
                "..kdsmMrMmsdk.",
                "..kdsmMMMmsdk.",
                "..kdsmmmmmsdk.",
                "..kdsssssssdk.",
                "...kdlllllldk.",
                "....kdsssdk...",
                "....kdsssdk...",
                "...kddsssddk..",
                "..kdddddddddk.",
                ".kdssssssssssk",
                ".kkkkkkkkkkkkk",
            }, pal, true, false);
            return spr;
        }

        public override void Tick(float dt)
        {
            t += dt;
            // it breathes: the eye pulses, motes drift into it
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
            sr.color = used ? new Color(0.45f, 0.4f, 0.5f) : Color.Lerp(new Color(0.75f, 0.7f, 0.85f), Color.white, pulse);
            if (!used && Random.value < 0.12f)
            {
                Vector2 eye = pos + new Vector2(0f, 1.15f);
                G.fx.Burst(eye + Random.insideUnitCircle.normalized * 1.6f, 1, new Color(0.7f, 0.35f, 1f), 0.4f, 0.8f, 0f, 1f, false);
            }
            if (used) return;
            if (Near(1.6f))
            {
                G.hud.Label(pos + Vector2.up * 2.2f, "PRESS " + GameInput.Prompt(Btn.Interact) + " TO USE", Color.white);
                if (GameInput.Take(Btn.Interact)) Use();
            }
        }

        protected override void Use()
        {
            var p = P;
            var w = p.weapon;
            var st = p.SelectedItem;
            if (w == null || st == null) { G.sfx.Play("deny", 0.5f, 0.6f); return; }
            w.ammoItem = st.def;
            p.SpendSelectedItem();
            used = true;
            G.sfx.Play("holy", 0.8f, 0.5f);
            G.sfx.Play("rumble", 0.8f, 0.6f);
            G.cam.Shake(0.4f);
            G.hud.ScreenFlash(0.25f);
            G.fx.Burst(pos + Vector2.up * 1.1f, 40, new Color(0.7f, 0.35f, 1f), 6f, 0.9f, 0f, 1.6f);
            G.fx.Ring(p.pos, 2.2f, new Color(0.7f, 0.35f, 1f));
            G.fx.Burst(p.pos, 24, new Color(0.85f, 0.6f, 1f), 4f, 0.6f, -2f);
        }

        public override void AddLight(Lighting l) => l.Add(pos + Vector2.up * 1.1f, used ? 1.2f : 2.8f, used ? 0.3f : 0.8f);
    }

    // A consumable fired from a gun (after the Fuse Altar): it flies, and does what the consumable does where it lands.
    public class ItemShot : Entity
    {
        readonly ItemDef def;
        readonly Player owner;
        Vector2 vel;
        float life = 3f, spin;
        static readonly Vector2 H = new Vector2(0.15f, 0.15f);

        public ItemShot(Player p, ItemDef d, Vector2 at, Vector2 v)
        {
            owner = p; def = d; pos = at; vel = v;
            MakeGO("ItemShot", d.icon, 16);
            go.transform.localScale = Vector3.one * 0.75f;
        }

        public override bool AlwaysActive => true;

        public override void Tick(float dt)
        {
            life -= dt;
            if (life <= 0f) { Pop(); return; }
            vel.y -= 14f * dt;
            int steps = Mathf.Max(1, Mathf.CeilToInt(vel.magnitude * dt / 0.2f));
            for (int i = 0; i < steps; i++)
            {
                pos += vel * dt / steps;
                if (Wd.Solid(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y))) { pos -= vel.normalized * 0.25f; Pop(); return; }
                foreach (var e in G.ents)
                    if (!e.dead && e is Enemy en && !en.Intangible && en.team != Team.Player && U.Overlap(pos, H, en.pos, en.half)) { Pop(); return; }
            }
            spin += dt * 600f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, spin);
            Place();
        }

        void Pop()
        {
            Kill();
            ItemEffects.At(def, pos, owner);
        }
    }

    // What each consumable does where it lands (for the Fuse Altar's shots)
    public static class ItemEffects
    {
        // how much slower than the gun's own rate each kind of consumable fires
        public static float RateFactor(ItemDef d)
        {
            switch (d.type)
            {
                case It.SuperBomb: return 14f;
                case It.Sentry: return 10f;
                case It.Reroll: return 8f;
                case It.Bomb: case It.Bore: return 5f;
                case It.Glimmer: return 6f;
                default: return 3f;
            }
        }

        public static void At(ItemDef d, Vector2 at, Player p)
        {
            var g = Game.I;
            int cx = Mathf.FloorToInt(at.x), cy = Mathf.FloorToInt(at.y);
            switch (d.type)
            {
                case It.Bomb: g.Explode(at, 2.6f, 10, false, true, true); break;
                case It.SuperBomb: g.Explode(at, 5f, 24, false, true, true); break;
                case It.Flare: g.Spawn(new Thrown(at, Vector2.zero, Thrown.Type.Flare, 0f)); break;
                case It.Bore: g.Spawn(new BoreCharge(cx, cy)); break;
                case It.Glimmer: g.Reveal(at, new Vector2(6f, 6f), 8f); g.fx.Ring(at, 6f, new Color(0.5f, 0.95f, 1f)); g.sfx.Play("chest", 0.4f, 1.8f); break;
                case It.Sentry:
                    {
                        int alive = 0;
                        foreach (var e in g.ents) if (!e.dead && e is Sentry) alive++;
                        if (alive < 3) g.Spawn(new Sentry(at, p));
                        break;
                    }
                case It.Reroll: Reroll.Circle(at, 3f); break;
                case It.Ale:
                    // a splash of ale: it scalds foes and a little of it finds its way back to you
                    foreach (var e in g.ents)
                        if (!e.dead && e is Enemy en && en.team != Team.Player && (en.pos - at).sqrMagnitude < 2.2f) en.TakeShot(p.Dmg(2.5f), (en.pos - at).normalized * 3f);
                    if (!p.NoHealing()) p.s.hp = Mathf.Min(p.s.maxHp, p.s.hp + 4);
                    g.fx.Burst(at, 16, new Color(1f, 0.75f, 0.2f), 4f, 0.5f, 8f);
                    g.sfx.Play("splat", 0.5f, 1.4f);
                    break;
                default: g.Explode(at, 1.5f, 6, false, true, true); break;
            }
        }
    }
}
