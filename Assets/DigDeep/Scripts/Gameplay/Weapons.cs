using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DigDeep
{
    // ===================================================================================== weapons

    public enum WType { Gun, Melee, Rail }
    public enum Debris { Mag, Casings, Smoke, None }

    // A weapon. Guns fire projectiles that also chew through blocks; melee weapons slash an arc,
    // dig everything in it and bat enemy bullets back.
    public class AttackDef : Def
    {
        public WType type = WType.Gun;
        public Rarity rarity;
        public bool Melee => type == WType.Melee;

        public float dmg = 1f, delay = 0.2f;      // damage per bullet / slash, seconds between trigger pulls
        public float speed = 30f, speedVar, spread = 4f, life = 0.9f;
        public int count = 1, burst = 1, pierce, bounces, mine = 1;
        public int blockPierce;                   // how many blocks a shot punches through (sniper)
        public float digPocket;                   // explosions also blow out a pocket of rock this big (mortar)
        public float burstGap = 0.06f;
        public float knock = 3f;                  // how hard hits shove enemies (caliber!)
        public float recoil = 1f;                 // how hard each shot shoves you back
        public float kick = 0.12f;                // how far the gun jerks back visually
        public float shake = 0.05f;               // screen shake per shot
        public float slow = 0.15f;                // move slower while firing
        public float size = 1f;
        public float radius;                      // explodes on impact with this radius
        public float grav, accel;                 // lobbed grenades / accelerating rockets
        public bool lob, flame, homing, selfHurt, abuses, deflect, doubleGold;
        public float charge;                      // seconds to fully charge (railgun)
        public string sfx = "pistol", bullet = "b_bullet";
        public float pitch = 1f;
        public Color col = new Color(1f, 0.9f, 0.45f);
        public float reach = 1.4f, arc = 100f;    // melee

        // ---- reloading / overheating
        public int mag;                           // rounds per magazine (0 = never reloads)
        public float reload;                      // seconds for a full reload (or per shell when perShell)
        public bool perShell;                     // loads one shell at a time; firing cancels the reload
        public float heatPerShot, coolRate;       // energy weapons overheat instead of reloading
        public string reloadSfx = "rl_mag";
        public float reloadPitch = 1f;
        public Debris debris = Debris.Mag;
        public bool UsesHeat => heatPerShot > 0f;

        public float Dps => dmg * count * burst / Mathf.Max(0.02f, delay + burstGap * (burst - 1));
    }

    // A weapon you are actually carrying: the base weapon plus up to three modifiers.
    public class Weapon
    {
        public readonly AttackDef def;
        public readonly List<Mod> mods = new List<Mod>();
        public int ammo = -1;        // -1: full (filled on first use)
        public Curse curse;          // a cursed weapon's price (see Curses.cs), lifted after 10 kills
        public int curseKills;
        public float heat;
        public bool overheated;

        public Weapon(AttackDef d) { def = d; }

        public bool Has(Mod m) => mods.Contains(m);
        public bool CanAdd(Mod m) => mods.Count < Mods.Max && !Has(m) && Mods.Fits(def, m);

        public string ModLine
        {
            get
            {
                if (mods.Count == 0) return "";
                var s = "";
                for (int i = 0; i < mods.Count; i++) s += (i > 0 ? " + " : "") + Mods.Names[(int)mods[i]];
                return s;
            }
        }

        public Color Color => mods.Count >= 3 ? new Color(1f, 0.45f, 0.95f) : DB.RarityColor(def.rarity);
    }

    // ===================================================================================== modifiers

    public enum Mod { Homing, Ricochet, Triple, Crit, Pierce, Poison, HighCaliber, Hunter, Sharpshooter, Explosive, WallBreaker, Frost, Chain, Fire, Skip, COUNT }

    public class ModDef : Def { public Mod mod; }

    public static class Mods
    {
        public const int Max = 3;

        public static readonly string[] Names =
            { "HOMING", "RICOCHET", "TRIPLE", "CRIT", "ALL-PIERCE", "POISON", "HIGH-CALIBER", "HUNTER", "SHARPSHOOTER", "EXPLOSIVE", "WALL-BREAKER", "FROST", "CHAIN", "FIRE", "SKIPPING" };

        public static readonly string[] Desc =
        {
            "SHOTS STEER TOWARDS FOES THEY CAN SEE",
            "SHOTS BOUNCE OFF WALLS ONCE",
            "FIRES 3 SHOTS, BUT 50% SLOWER",
            "EVERY SHOT HAS +20% CRIT CHANCE",
            "SHOTS PIERCE EVERY ENEMY",
            "POISONED FOES TAKE 25% MORE DAMAGE",
            "HUGE DAMAGE, MUCH SLOWER FIRE RATE",
            "KILL THE MARKED FOE FOR A BUFF",
            "HIT THE WEAK SPOT FOR A BIG CRIT",
            "SHOTS EXPLODE. CAN HURT YOU!",
            "SHOTS SMASH THROUGH ROCK",
            "HITS FREEZE FOES SOLID FOR 1.5S",
            "HITS JUMP ON TO THE NEAREST FOE",
            "SETS FOES (BOSSES TOO) ALIGHT FOR 2S. LIGHTS GRASS AND WOOD",
            "SHOTS ARC DOWN AND SKIP OFF FLOORS LIKE STONES ON WATER, HALF AS FAST EACH TIME",
        };

        static readonly char[] Letters = { 'S', 'R', 'T', 'C', 'P', 'V', 'K', 'H', 'A', 'X', 'W', 'F', 'Z', 'B', 'K' };

        static readonly Color[] Cols =
        {
            new Color(0.5f, 0.9f, 1f), new Color(0.75f, 0.85f, 1f), new Color(1f, 0.85f, 0.4f), new Color(1f, 0.4f, 0.35f),
            new Color(0.85f, 0.85f, 0.9f), new Color(0.55f, 1f, 0.35f), new Color(1f, 0.6f, 0.25f), new Color(1f, 0.9f, 0.2f),
            new Color(1f, 0.3f, 0.5f), new Color(1f, 0.5f, 0.15f), new Color(0.75f, 0.55f, 0.35f),
            new Color(0.6f, 0.9f, 1f), new Color(0.45f, 0.75f, 1f), new Color(1f, 0.55f, 0.15f), new Color(0.55f, 0.85f, 0.75f),
        };

        static Sprite[] icons;

        public static Color Col(Mod m) => Cols[(int)m];

        // In menus and labels every modifier wears the same loud, pulsing hot pink: nothing else in the game uses it.
        public static readonly Color Signature = new Color(1f, 0.2f, 0.85f);
        public static Color Ui => Color.Lerp(Signature, new Color(1f, 0.8f, 1f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f));

        public static Sprite Icon(Mod m)
        {
            if (icons == null)
            {
                icons = new Sprite[(int)Mod.COUNT];
                for (int i = 0; i < icons.Length; i++)
                {
                    var c = Cols[i];
                    icons[i] = Art.Rune(new Color32((byte)(c.r * 200), (byte)(c.g * 200), (byte)(c.b * 200), 255), Letters[i]);
                }
            }
            return icons[(int)m];
        }

        // Some modifiers make no sense on some weapons.
        public static bool Fits(AttackDef d, Mod m)
        {
            if (d.Melee) return m != Mod.Homing && m != Mod.Ricochet && m != Mod.Pierce && m != Mod.Skip;
            if (d.type == WType.Rail) return m != Mod.Homing && m != Mod.Ricochet && m != Mod.Pierce && m != Mod.Skip;
            if (d.lob) return m != Mod.Ricochet && m != Mod.Explosive;
            if (d.radius > 0f && m == Mod.Explosive) return false;
            if (d.homing && m == Mod.Homing) return false;
            if (d.flame && m == Mod.Fire) return false;   // the flamethrower already burns
            if (d.flame && m == Mod.Skip) return false;
            return true;
        }

        // Rare, but more and more common the deeper you go. Three is ultra rare.
        public static void Roll(Weapon w, int depth, Rarity r, System.Random rng)
        {
            // first biome: a modded weapon is a rare treat; it ramps up from the second biome on
            float rb = r == Rarity.Relic ? 0.25f : r == Rarity.Rare ? 0.08f : r == Rarity.Uncommon ? 0.03f : 0f;
            float past = Mathf.Max(0, depth - 70);
            var pl = Game.I != null ? Game.I.player : null;
            float lk = 1f + 0.1f * Mathf.Clamp(pl != null ? pl.s.luck : 0f, -5f, 15f);
            float p1 = Mathf.Min((0.05f + depth * 0.0003f + past * 0.0012f + rb) * lk, 0.7f);
            float p2 = Mathf.Min(0.05f + past * 0.0007f + rb * 0.5f, 0.4f);
            float p3 = Mathf.Min(0.01f + Mathf.Max(0, depth - 150) * 0.0003f + rb * 0.2f, 0.12f);
            int n = 0;
            if (rng.NextDouble() < p1) { n = 1; if (rng.NextDouble() < p2) { n = 2; if (rng.NextDouble() < p3) n = 3; } }
            for (int i = 0; i < n; i++) AddRandom(w, rng);
        }

        public static bool AddRandom(Weapon w, System.Random rng)
        {
            var pool = new List<Mod>();
            for (int i = 0; i < (int)Mod.COUNT; i++) if (w.CanAdd((Mod)i)) pool.Add((Mod)i);
            if (pool.Count == 0) return false;
            w.mods.Add(pool[rng.Next(pool.Count)]);
            return true;
        }
    }

    // ===================================================================================== teams

    // Raiders carry guns and hate the beasts of the deep; the beasts hate everyone. Everybody hates you.
    public enum Team { Player, Beast, Bandit, Boss }

    public static class Teams
    {
        public static bool Hostile(Team a, Team b)
        {
            if (a == b) return false;
            if (a == Team.Player || b == Team.Player) return true;
            if (a == Team.Boss || b == Team.Boss) return false;
            return true;
        }
    }

    // ===================================================================================== weapon crates

    public static class Crates
    {
        // Each crate's rarity is fixed by where it sits; deeper crates are better.
        public static Rarity RarityAt(int x, int y)
        {
            var w = Game.I != null ? Game.I.world : null;
            if (w != null && w.CrateRarity(x, y, out var moved)) return moved;
            return RarityFor(Mathf.Max(0, -y), U.H01(x, y, Gen.Seed + 91));
        }

        // The first biome is almost all common crates, a few uncommon, and a rare one is a real event.
        // From the second biome on, better crates slowly take over.
        public static Rarity RarityFor(int d, float h)
        {
            float past = Mathf.Max(0, d - 70);
            // luck makes every better crate more likely
            var pl = Game.I != null ? Game.I.player : null;
            float lk = 1f + 0.1f * Mathf.Clamp(pl != null ? pl.s.luck : 0f, -5f, 15f);
            float relic = (d > 60 ? Mathf.Min(0.006f + (d - 60) * 0.0001f, 0.04f) : 0.002f) * lk;
            float rare = Mathf.Min(0.015f + past * 0.0008f, 0.25f) * lk;
            float unc = Mathf.Min(0.12f + d * 0.0005f + past * 0.0015f, 0.42f) * lk;
            if (h < relic) return Rarity.Relic;
            if (h < relic + rare) return Rarity.Rare;
            if (h < relic + rare + unc) return Rarity.Uncommon;
            return Rarity.Common;
        }

        public static Weapon Roll(int depth, Rarity r, Player p, System.Random rng)
        {
            AttackDef d = null;
            for (var rr = r; d == null; rr--)
            {
                d = PickOf(rr, depth, p, rng);
                if (rr == Rarity.Common) break;
            }
            if (d == null) d = DB.Pickaxe;
            var w = new Weapon(d);
            Mods.Roll(w, depth, r, rng);
            if (r == Rarity.Relic && w.mods.Count == 0) Mods.AddRandom(w, rng);
            Curses.MaybeCurse(w, depth, rng);   // sometimes: an extra modifier, paid for with a curse
            return w;
        }

        static AttackDef PickOf(Rarity r, int depth, Player p, System.Random rng)
        {
            var pool = new List<AttackDef>();
            foreach (var a in DB.Attacks)
            {
                if (a.rarity != r) continue;
                if (a.altarOnly && r != Rarity.Relic) continue;
                if (a.minDepth > depth + 40) continue;
                if (p != null && p.Holds(a.id)) continue;
                pool.Add(a);
            }
            return pool.Count == 0 ? null : pool[rng.Next(pool.Count)];
        }
    }

    // ===================================================================================== the armoury

    public static class Armoury
    {
        static AttackDef G(string id, string name, string desc, Rarity r, int minDepth, int price)
        {
            var d = new AttackDef { id = id, name = name, desc = desc, kind = Kind.Attack, rarity = r, minDepth = minDepth, price = price };
            d.icon = Art.Icons.TryGetValue(id, out var ic) ? ic : Art.Icons["revolver"];
            d.weight = r == Rarity.Common ? 10 : r == Rarity.Uncommon ? 6 : r == Rarity.Rare ? 3 : 1;
            if (r == Rarity.Relic) d.altarOnly = true;
            DB.AddAttack(d);
            return d;
        }

        public static void Define()
        {
            // Damage is balanced so every weapon of one rarity has about the same DPS:
            // common ~7.5, uncommon ~9, rare ~11, relic ~14. Fast guns hit softly, splash / piercing
            // / bouncing weapons a bit under target, fast weapons (and melee that also digs) a bit lower still.
            Color gold = new Color(1f, 0.85f, 0.3f), cyan = new Color(0.4f, 0.95f, 1f), purple = new Color(0.8f, 0.45f, 1f),
                  orange = new Color(1f, 0.6f, 0.2f), red = new Color(1f, 0.4f, 0.35f), white = new Color(1f, 0.97f, 0.85f);

            // ---------------- guns: common
            var a = G("revolver", "REVOLVER", "TRUSTY SIDEARM", Rarity.Common, 0, 90);
            a.dmg = 1.5f; a.delay = 0.2f; a.speed = 30f; a.spread = 5f; a.knock = 3.5f; a.recoil = 1.2f; a.kick = 0.14f; a.shake = 0.06f; a.size = 1.1f;

            a = G("smg", "MACHINE PISTOL", "FAST, WILD SPRAY", Rarity.Common, 0, 100);
            a.dmg = 0.58f; a.delay = 0.1f; a.speed = 28f; a.spread = 13f; a.knock = 1.5f; a.recoil = 0.5f; a.kick = 0.08f; a.shake = 0.035f;
            a.size = 0.9f; a.slow = 0.2f; a.abuses = true; a.sfx = "smg";

            a = G("shotgun", "SHOTGUN", "7 PELLETS. DEVASTATING UP CLOSE, USELESS FAR AWAY", Rarity.Common, 0, 110);
            a.dmg = 0.8f; a.count = 7; a.delay = 0.55f; a.speed = 24f; a.speedVar = 0.25f; a.spread = 28f; a.life = 0.3f; a.knock = 5.5f;
            a.recoil = 6f; a.kick = 0.3f; a.shake = 0.18f; a.slow = 0.35f; a.abuses = true; a.sfx = "shotgun"; a.bullet = "b_pellet";

            // ---------------- starter guns for the newer diggers (weak, but with a twist)
            a = G("mortar", "MINER'S MORTAR", "WEAK LOBBED BOMBS THAT BLOW OUT BIG POCKETS OF ROCK", Rarity.Common, 0, 100);
            a.dmg = 3.6f; a.delay = 0.7f; a.speed = 15f; a.spread = 3f; a.grav = 26f; a.lob = true; a.life = 1.0f; a.radius = 1.2f; a.knock = 0f;
            a.recoil = 2f; a.kick = 0.25f; a.shake = 0.12f; a.sfx = "launcher"; a.pitch = 1.15f; a.bullet = "b_grenade"; a.mine = 0; a.col = orange;
            a.digPocket = 2.4f;
            if (Art.Icons.TryGetValue("grenade", out var gIcon)) a.icon = gIcon;

            a = G("scout", "SCOUT RIFLE", "A LIGHT SNIPER. PIERCES 3 FOES AND 2 BLOCKS", Rarity.Common, 0, 110);
            a.dmg = 9f; a.delay = 1.1f; a.speed = 80f; a.spread = 0f; a.life = 1.5f; a.pierce = 3; a.knock = 10f; a.recoil = 3.5f; a.kick = 0.3f; a.shake = 0.16f;
            a.mine = 4; a.blockPierce = 2; a.slow = 0.25f; a.sfx = "sniper"; a.pitch = 1.2f; a.size = 1.1f; a.col = white;
            if (Art.Icons.TryGetValue("sniper", out var sIcon)) a.icon = sIcon;

            // ---------------- guns: uncommon
            a = G("goldgun", "GILDED REVOLVER", "ORE DROPS DOUBLE GOLD", Rarity.Uncommon, 0, 150);
            a.dmg = 1.75f; a.delay = 0.22f; a.speed = 30f; a.spread = 5f; a.knock = 3.5f; a.recoil = 1.2f; a.kick = 0.14f; a.shake = 0.06f;
            a.doubleGold = true; a.col = gold; a.pitch = 1.1f; a.size = 1.1f;

            a = G("rifle", "ASSAULT RIFLE", "3-ROUND BURSTS", Rarity.Uncommon, 15, 150);
            a.dmg = 1.4f; a.burst = 3; a.burstGap = 0.06f; a.delay = 0.42f; a.speed = 34f; a.spread = 6f; a.knock = 3f; a.recoil = 1f; a.kick = 0.12f;
            a.shake = 0.06f; a.sfx = "rifle";

            a = G("boltcaster", "BOLT CASTER", "BOLTS PIERCE 5 FOES AND DRILL THROUGH 3 BLOCKS", Rarity.Uncommon, 0, 140);
            a.dmg = 6.5f; a.delay = 0.55f; a.speed = 42f; a.spread = 0.5f; a.life = 1.2f; a.pierce = 5; a.knock = 6f; a.recoil = 1.5f; a.kick = 0.2f; a.shake = 0.1f;
            a.mine = 4; a.blockPierce = 3; a.sfx = "bolt"; a.bullet = "b_bolt"; a.col = white;

            a = G("laserpistol", "LASER PISTOL", "BEAMS PIERCE TWO FOES", Rarity.Uncommon, 0, 140);
            a.dmg = 1.2f; a.delay = 0.18f; a.speed = 55f; a.spread = 2f; a.pierce = 2; a.knock = 2f; a.recoil = 0.6f; a.kick = 0.1f; a.shake = 0.04f;
            a.sfx = "laser"; a.bullet = "b_laser"; a.col = cyan;

            a = G("grenade", "GRENADE LAUNCHER", "BOMBS THAT BLOW UP ON IMPACT. CAREFUL!", Rarity.Uncommon, 20, 160);
            a.dmg = 6.3f; a.delay = 0.65f; a.speed = 16f; a.spread = 3f; a.grav = 26f; a.lob = true; a.life = 1.0f; a.radius = 1.7f; a.knock = 0f;
            a.recoil = 2.5f; a.kick = 0.3f; a.shake = 0.12f; a.selfHurt = true; a.sfx = "launcher"; a.bullet = "b_grenade"; a.mine = 0; a.col = orange;

            a = G("flamer", "FLAMETHROWER", "SETS FOES, GRASS AND WOOD ALIGHT. MELTS ICE AND ROCK", Rarity.Uncommon, 25, 170);
            a.dmg = 0.21f; a.delay = 0.035f; a.speed = 13f; a.speedVar = 0.3f; a.spread = 16f; a.life = 0.38f; a.knock = 0.6f; a.recoil = 0.15f;
            a.kick = 0.05f; a.shake = 0.02f; a.slow = 0.2f; a.flame = true; a.sfx = "flame"; a.bullet = "b_flame"; a.col = orange; a.abuses = true;

            a = G("disc", "DISC GUN", "SAWBLADES BOUNCE OFF EVERYTHING", Rarity.Uncommon, 30, 160);
            a.dmg = 2.15f; a.delay = 0.32f; a.speed = 22f; a.spread = 4f; a.bounces = 4; a.pierce = 3; a.life = 2f; a.knock = 3f; a.recoil = 0.8f;
            a.kick = 0.15f; a.shake = 0.05f; a.abuses = true; a.sfx = "disc"; a.bullet = "b_disc"; a.col = new Color(0.85f, 0.85f, 0.9f);

            // ---------------- guns: rare
            a = G("sniper", "SNIPER RIFLE", "ONE HUGE SHOT. PIERCES 6 FOES AND PUNCHES THROUGH 6 BLOCKS", Rarity.Rare, 40, 230);
            a.dmg = 18f; a.delay = 1f; a.speed = 100f; a.spread = 0f; a.life = 1.8f; a.pierce = 6; a.knock = 16f; a.recoil = 7f; a.kick = 0.45f; a.shake = 0.3f;
            a.mine = 9; a.blockPierce = 6; a.slow = 0.4f; a.sfx = "sniper"; a.size = 1.3f; a.col = white;

            a = G("minigun", "MINIGUN", "A WALL OF LEAD. SLOWS YOU DOWN", Rarity.Rare, 60, 260);
            a.dmg = 0.37f; a.delay = 0.045f; a.speed = 30f; a.spread = 18f; a.knock = 2f; a.recoil = 0.9f; a.kick = 0.1f; a.shake = 0.05f;
            a.slow = 0.55f; a.abuses = true; a.sfx = "smg"; a.pitch = 0.85f;

            a = G("plasma", "PLASMA GUN", "BIG BURNING BALLS THAT MELT ROCK", Rarity.Rare, 50, 240);
            a.dmg = 5.6f; a.delay = 0.5f; a.speed = 16f; a.spread = 3f; a.radius = 1.1f; a.knock = 7f; a.recoil = 2.5f; a.kick = 0.25f; a.shake = 0.15f;
            a.mine = 2; a.sfx = "plasma"; a.bullet = "b_plasma"; a.size = 1.2f; a.col = purple; a.life = 1.6f;

            a = G("rocket", "ROCKET LAUNCHER", "HUGE BLASTS. DON'T HUG WALLS", Rarity.Rare, 60, 260);
            a.dmg = 10.5f; a.delay = 0.95f; a.speed = 12f; a.accel = 60f; a.spread = 2f; a.radius = 2.5f; a.knock = 4f; a.recoil = 4f; a.kick = 0.4f;
            a.shake = 0.25f; a.selfHurt = true; a.sfx = "rocket"; a.bullet = "b_rocket"; a.life = 2f; a.mine = 0; a.col = orange;

            a = G("railgun", "RAILGUN", "HOLD TO CHARGE. PIERCES EVERYTHING", Rarity.Rare, 80, 280);
            a.type = WType.Rail; a.dmg = 13f; a.charge = 0.7f; a.delay = 0.3f; a.knock = 14f; a.recoil = 6f; a.kick = 0.5f; a.shake = 0.4f; a.mine = 6;
            a.sfx = "rail"; a.col = cyan; a.slow = 0.3f;

            // ---------------- melee
            a = G("pickaxe", "PICKAXE", "THE OLD WAY. DIGS FAST", Rarity.Common, 0, 60);
            a.type = WType.Melee; a.dmg = 0.92f; a.delay = 0.17f; a.reach = 1.25f; a.arc = 70f; a.mine = 3; a.knock = 5f; a.recoil = 0f; a.kick = 0.1f;
            a.shake = 0.05f; a.sfx = "swing";
            DB.Pickaxe = a;

            a = G("dagger", "DAGGER", "QUICK LITTLE STABS. WEAK, BUT FAST. DIGS 2 BLOCKS", Rarity.Common, 0, 70);
            a.type = WType.Melee; a.dmg = 0.75f; a.delay = 0.13f; a.reach = 1.1f; a.arc = 75f; a.mine = 2; a.knock = 3f; a.recoil = 0f; a.kick = 0.06f;
            a.shake = 0.03f; a.sfx = "swing"; a.pitch = 1.5f; a.col = new Color(0.85f, 0.88f, 0.95f);

            a = G("sword", "DWARVEN SWORD", "DIGS 3 BLOCKS. BATS BULLETS BACK", Rarity.Common, 0, 100);
            a.type = WType.Melee; a.dmg = 2f; a.delay = 0.3f; a.reach = 1.55f; a.arc = 110f; a.mine = 3; a.knock = 9f; a.recoil = -1.5f; a.kick = 0.2f;
            a.shake = 0.1f; a.deflect = true; a.sfx = "sword";

            a = G("hammer", "WAR HAMMER", "SLOW. SMASHES A BIG AREA", Rarity.Uncommon, 0, 150);
            a.type = WType.Melee; a.dmg = 3.8f; a.delay = 0.5f; a.reach = 2.05f; a.arc = 120f; a.mine = 5; a.knock = 14f; a.recoil = -1f; a.kick = 0.3f;
            a.shake = 0.25f; a.deflect = true; a.sfx = "hammer";

            a = G("esword", "ENERGY SWORD", "WIDE BURNING ARC. DEFLECTS", Rarity.Rare, 50, 250);
            a.type = WType.Melee; a.dmg = 2.4f; a.delay = 0.26f; a.reach = 2f; a.arc = 150f; a.mine = 4; a.knock = 10f; a.recoil = -2f; a.kick = 0.2f;
            a.shake = 0.12f; a.deflect = true; a.sfx = "esword"; a.col = cyan;

            // ---------------- relics: puzzle altars and golden crates only
            a = G("seeker", "SEEKER SWARM", "4 HOMING MINI-ROCKETS", Rarity.Relic, 0, 320);
            a.dmg = 2.6f; a.count = 4; a.spread = 50f; a.speed = 16f; a.delay = 0.7f; a.homing = true; a.radius = 0.9f; a.knock = 3f; a.recoil = 2f;
            a.kick = 0.25f; a.shake = 0.12f; a.sfx = "rocket"; a.pitch = 1.4f; a.bullet = "b_seeker"; a.life = 2.2f; a.mine = 1; a.col = red;

            a = G("starpick", "STAR PICK", "BLINDINGLY FAST. HUGE MINING", Rarity.Relic, 0, 320);
            a.type = WType.Melee; a.dmg = 1.07f; a.delay = 0.1f; a.reach = 1.45f; a.arc = 90f; a.mine = 8; a.knock = 7f; a.kick = 0.1f; a.shake = 0.06f;
            a.deflect = true; a.sfx = "swing"; a.col = gold;

            a = G("earthsplitter", "EARTHSPLITTER", "SPLITS THE GROUND IN A HUGE ARC", Rarity.Relic, 0, 320);
            a.type = WType.Melee; a.dmg = 4.7f; a.delay = 0.42f; a.reach = 2.7f; a.arc = 160f; a.mine = 9; a.knock = 18f; a.recoil = -2f; a.kick = 0.35f;
            a.shake = 0.35f; a.deflect = true; a.sfx = "hammer"; a.pitch = 0.8f; a.col = orange;
        }

        static void R(string id, int mag, float reload, string sfx, float pitch = 1f, bool shell = false, Debris debris = Debris.Mag, float delay = -1f)
        {
            var a = (AttackDef)DB.ById[id];
            a.mag = mag; a.reload = reload; a.reloadSfx = sfx; a.reloadPitch = pitch; a.perShell = shell; a.debris = debris;
            if (delay >= 0f) a.delay = delay;
        }

        static void H(string id, float heat, float cool)
        {
            var a = (AttackDef)DB.ById[id];
            a.heatPerShot = heat; a.coolRate = cool; a.reloadSfx = "vent"; a.debris = Debris.Smoke;
        }

        // Magazines and reloads. Realistic-ish: the starting pistols hold 12, pistols and SMGs reload fast, the shotgun and
        // grenade launcher load shell by shell, the sniper and rocket launcher reload after every shot.
        // Energy weapons don't reload: they overheat and vent.
        public static void Reloads()
        {
            R("revolver", 12, 0.85f, "rl_revolver", 1f, false, Debris.Casings);
            R("goldgun", 12, 0.85f, "rl_revolver", 1.1f, false, Debris.Casings);
            R("smg", 25, 1.2f, "rl_mag", 1.2f);
            R("mortar", 4, 1.3f, "rl_grenade", 1.1f, false, Debris.None);
            R("scout", 3, 1.9f, "rl_bolt", 1.1f, false, Debris.Casings);
            R("shotgun", 6, 0.42f, "rl_shell", 1f, true, Debris.None);
            R("rifle", 30, 1.45f, "rl_mag", 1f);
            R("boltcaster", 3, 1.5f, "rl_bolt", 1f, false, Debris.None, 0.4f);
            R("grenade", 6, 0.5f, "rl_grenade", 1f, true, Debris.None, 0.45f);
            R("flamer", 100, 2f, "rl_flame", 1f, false, Debris.Smoke);
            R("disc", 5, 1.4f, "rl_disc", 1f, false, Debris.None);
            R("sniper", 1, 1.7f, "rl_sniper", 1f, false, Debris.Casings, 0.15f);
            R("minigun", 100, 2.6f, "rl_mag", 0.75f);
            R("rocket", 1, 1.0f, "rl_rocket", 1f, false, Debris.Smoke, 0.15f);
            R("seeker", 3, 1.6f, "rl_rocket", 1.3f, false, Debris.Smoke);
            H("laserpistol", 0.075f, 0.85f);
            H("plasma", 0.22f, 0.5f);
            H("railgun", 0.55f, 0.45f);
        }

        public static string Stats(AttackDef a)
        {
            var ci = CultureInfo.InvariantCulture;
            string dmg = a.dmg.ToString("0.##", ci) + (a.count > 1 ? "x" + a.count : "") + (a.burst > 1 ? "x" + a.burst : "");
            float rate = 1f / Mathf.Max(0.02f, a.delay + (a.type == WType.Rail ? a.charge : 0f));
            string ammo = a.UsesHeat ? "   OVERHEATS" : a.mag > 0 ? "   MAG " + a.mag : "";
            return (a.Melee ? "MELEE  " : "") + "DMG " + dmg + "   RATE " + rate.ToString("0.0", ci) + "/S   MINE " + a.mine + ammo;
        }
    }
}
