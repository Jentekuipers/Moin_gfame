using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace DigDeep
{
    public enum Kind { Attack, Special, Skill, Item, Upgrade, Mod }
    public enum Rarity { Common, Uncommon, Rare, Relic }
    public enum Sp { Dash, Grapple, SuperJump, Rope, Invincible, QuickDig, Blink, Kick, Tide, Phase, Parry, Camo }
    public enum It { Bomb, Ale, Flare, Bore, SuperBomb, Glimmer, Sentry }

    // Isaac-style stat changes carried by a trinket (or a character's base line).
    public struct St
    {
        public int hp, mining;
        public float dmg, swing, speed, jump, luck;

        public bool Any => hp != 0 || mining != 0 || dmg != 0f || swing != 0f || speed != 0f || jump != 0f || luck != 0f;
        public bool AllPositive => hp >= 0 && mining >= 0 && dmg >= 0f && swing >= 0f && speed >= 0f && jump >= 0f && luck >= 0f;

        static string F(float v) => (v > 0 ? "+" : "") + v.ToString("0.##", CultureInfo.InvariantCulture);

        public override string ToString()
        {
            var sb = new StringBuilder();
            void Add(string label, string v) { if (sb.Length > 0) sb.Append("  "); sb.Append(label).Append(' ').Append(v); }
            if (hp != 0) Add("MAX HP", (hp > 0 ? "+" : "") + hp);
            if (dmg != 0f) Add("DMG", F(dmg));
            if (swing != 0f) Add("FIRE RATE", F(swing));
            if (speed != 0f) Add("SPD", F(speed));
            if (mining != 0) Add("MINE", (mining > 0 ? "+" : "") + mining);
            if (jump != 0f) Add("JUMP", F(jump));
            if (luck != 0f) Add("LUCK", F(luck));
            return sb.ToString();
        }
    }

    public class Def
    {
        public string id, name, desc;
        public Kind kind;
        public Sprite icon;
        public int price = 100, minDepth, weight = 10;
        public bool altarOnly;   // only ever handed out by puzzle altars
    }

    // Special (Shift): a movement trick, or occasionally something unique. Recharges over `cd` seconds
    // (kills speed it up). Up to `uses` charges can be stored.
    public class SpecialDef : Def
    {
        public Sp type;
        public int segs = 1, uses = 1;
        public float cd = 3f;
        public bool fixedCd;   // always the same cooldown: kills and boss hits don't speed it up
    }

    // A trinket (passive). Can change stats, add an effect, or both.
    public class SkillDef : Def
    {
        public int cap = 1;
        public Rarity rarity;
        public string owner;   // a digger's own skill: only found in runs once you've won with that digger
        public St st;
        public System.Action<Player> apply;
        public string[] tags;  // transformation sets this trinket counts towards (SERAPH, INFERNO, ...)
        public string look;    // what it visibly changes on your digger (wings, horns, a halo...)
        public string pet;     // a pet that follows you around (see Pets.cs)
    }

    public class ItemDef : Def { public It type; }

    public class UpgradeDef : Def { public System.Action<Player> apply; }

    public class ComboDef
    {
        public string id, name, desc;
        public string[] needs;
        // TRANSFORMATIONS (Isaac-style): instead of fixed pieces, any `count` trinkets carrying `tag` will do
        public string tag;
        public int count;
        public string look;
        public bool IsTransform => tag != null;
        public System.Action<Player> onActivate;   // fires once, the first time the combo forms
    }

    public static class DB
    {
        public const float SegEnergy = 12f;      // energy per charge segment; only kills give energy (4 + more for tough foes)
        public const float KillEnergy = 4f;

        public static readonly List<AttackDef> Attacks = new List<AttackDef>();
        public static readonly List<SpecialDef> Specials = new List<SpecialDef>();
        public static readonly List<SkillDef> Trinkets = new List<SkillDef>();
        public static readonly List<ItemDef> Items = new List<ItemDef>();
        public static readonly List<UpgradeDef> Upgrades = new List<UpgradeDef>();
        public static readonly List<ComboDef> Combos = new List<ComboDef>();
        public static readonly List<ModDef> ModDefs = new List<ModDef>();
        public static readonly Dictionary<string, Def> ById = new Dictionary<string, Def>();
        public static AttackDef Pickaxe;
        public static ItemDef Ale, Bomb;
        static bool inited;

        public static Color KindColor(Kind k)
        {
            switch (k)
            {
                case Kind.Attack: return new Color(1f, 0.55f, 0.35f);
                case Kind.Special: return new Color(0.45f, 0.85f, 1f);
                case Kind.Skill: return new Color(0.75f, 0.55f, 1f);
                case Kind.Item: return new Color(0.55f, 1f, 0.55f);
                case Kind.Mod: return Mods.Signature;
                default: return new Color(1f, 0.85f, 0.3f);
            }
        }

        public static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return new Color(0.9f, 0.9f, 0.9f);
                case Rarity.Uncommon: return new Color(0.5f, 1f, 0.5f);
                case Rarity.Rare: return new Color(0.45f, 0.75f, 1f);
                default: return new Color(1f, 0.78f, 0.22f);
            }
        }

        public static Color ColorOf(Def d)
        {
            if (d is SkillDef k) return RarityColor(k.rarity);
            if (d is AttackDef a) return RarityColor(a.rarity);
            if (d is ModDef) return Mods.Ui;
            if (d.altarOnly) return new Color(1f, 0.78f, 0.22f);
            return KindColor(d.kind);
        }

        public static readonly Color ComboColor = new Color(1f, 0.82f, 0.25f);
        public static readonly Color TransformColor = new Color(0.45f, 1f, 0.9f);
        public static Color ColorOfCombo(ComboDef c) => c.IsTransform ? TransformColor : ComboColor;
        public static readonly Color StatUp = new Color(0.45f, 1f, 0.45f), StatDown = new Color(1f, 0.45f, 0.4f);

        // Extra info line shown under the description: stat changes or charge cost.
        public static string Extra(Def d)
        {
            if (d is SkillDef k && k.st.Any) return k.st.ToString();
            if (d is SpecialDef s) return "SPECIAL   RECHARGES IN " + s.cd.ToString("0.#", CultureInfo.InvariantCulture) + "S   MAX USES " + s.uses;
            if (d is AttackDef a) return Armoury.Stats(a);
            if (d is ModDef) return "APPLIES TO THE WEAPON IN YOUR HANDS";
            return null;
        }

        public static Color ExtraColor(Def d) => d is SkillDef k && !k.st.AllPositive ? new Color(1f, 0.7f, 0.4f) : (d is SkillDef ? StatUp : new Color(0.7f, 0.8f, 0.9f));

        public static void Init()
        {
            if (inited) return;
            inited = true;
            var I = Art.Icons;

            Color32 red = U.C(220, 50, 55), grey = U.C(160, 165, 180), green = U.C(80, 200, 100), blue = U.C(80, 150, 240),
                orange = U.C(250, 140, 40), gold = U.C(250, 200, 50), brown = U.C(150, 100, 60), purple = U.C(160, 90, 220),
                white = U.C(230, 230, 240), cyan = U.C(80, 220, 240), dark = U.C(70, 70, 90), pale = U.C(200, 230, 240),
                lime = U.C(170, 230, 70), pink = U.C(240, 110, 180), steel = U.C(110, 140, 170), meat = U.C(170, 75, 60),
                coffee = U.C(130, 80, 50), yellow = U.C(250, 230, 70);

            // ---------- weapons: see Weapons.cs ----------
            Armoury.Define();
            Armoury.Reloads();

            // ---------- specials (Shift): movement tricks, plus a couple of odd ones ----------
            S("dash", "DASH", "A QUICK, UNTOUCHABLE BURST WHERE YOU'RE HOLDING. PUTS OUT FLAMES YOU DASH THROUGH", I["dash"], Sp.Dash, 3.5f, 1, 100, 0);
            S("grapple", "GRAPPLING HOOK", "HOOK A WALL, ZIP TO IT", I["grapple"], Sp.Grapple, 2.5f, 2, 100, 0);
            S("superjump", "SUPER JUMP", "ROCKET STRAIGHT UP, SMASHING THROUGH DIRT ABOVE", I["superjump"], Sp.SuperJump, 4f, 1, 120, 0);
            S("rope", "SWINGING ROPE", "LASSO THE CEILING AND SWING. A/D TO PUMP, W/S TO CLIMB, JUMP TO LET GO", Art.Trinket("coil", brown), Sp.Rope, 2.5f, 1, 120, 0);
            S("quickdig", "QUICK DIG", "DRILL 6 BLOCKS STRAIGHT DOWN THROUGH ROCK AND BRICK", I["pound"], Sp.QuickDig, 4f, 1, 130, 0);
            S("blink", "BLINK RUNE", "TELEPORT 7 BLOCKS STRAIGHT DOWN", Art.Rune(U.C(150, 80, 230), 'B'), Sp.Blink, 7f, 1, 150, 30);
            var kick = S("kick", "KICK", "A LONG BOOT THAT SENDS FOES FLYING INTO EACH OTHER. BOULDERS TOO. JUST A 1S COOLDOWN", Art.Trinket("boot", brown), Sp.Kick, 1f, 1, 140, 0);
            kick.fixedCd = true;
            S("tide", "TIDAL BURST", "FLOOD A BALL OF WATER AROUND YOU, SHOVING FOES AWAY", Art.Trinket("drop", blue), Sp.Tide, 4.5f, 1, 140, 0);
            S("phase", "SHADOW STEP", "VANISH AND REAPPEAR UP TO 6 BLOCKS WHERE YOU HOLD (W A S D). A DECOY STAYS BEHIND FOR 1.5S", Art.Trinket("feather", purple), Sp.Phase, 3.5f, 1, 140, 0);
            var parry = S("parry", "PARRY", "STAND FIRM: SHOTS BOUNCE BACK AT THEIR SHOOTER, MELEE HITS GET A COUNTER. EACH ONE STRETCHES IT (MAX 2S)", Art.Trinket("blade", pale), Sp.Parry, 3f, 1, 140, 0);
            S("camo", "CAMOUFLAGE", "FADE INTO THE ROCK: FOES IGNORE YOU UNTIL YOU SHOOT, GET HURT, OR 6S PASS", Art.Trinket("eye", purple), Sp.Camo, 8f, 1, 140, 0);
            S("invincible", "AEGIS", "HALF A SECOND OF TOTAL INVINCIBILITY", Art.Trinket("shield", gold), Sp.Invincible, 6f, 1, 220, 40).weight = 2;

            // ---------- trinkets ----------
            // common
            Tr("heart", "STOUT HEART", "", "heart", red, Rarity.Common, 3, new St { hp = 25 }, null);
            Tr("grip", "STEEL GRIP", "", "fist", grey, Rarity.Common, 3, new St { mining = 1 }, null);
            Tr("whetstone", "WHETSTONE", "", "rock", steel, Rarity.Common, 2, new St { dmg = 0.5f }, null);
            Tr("diamondbit", "HAIR TRIGGER", "", "gem", cyan, Rarity.Common, 2, new St { swing = 0.2f }, null);
            Tr("swift", "SWIFT BOOTS", "", "boot", green, Rarity.Common, 2, new St { speed = 0.15f }, null);
            Tr("spring", "SPRING BOOTS", "", "boot", blue, Rarity.Common, 2, new St { jump = 0.15f }, null);
            Tr("marble", "LUCKY MARBLE", "", "orb", lime, Rarity.Common, 2, new St { luck = 1f }, null);
            Tr("jerky", "MOLE JERKY", "", "rock", meat, Rarity.Common, 2, new St { hp = 25 }, p => p.s.hp = Mathf.Min(p.s.maxHp, p.s.hp + 25)).weight = 5;
            Tr("coffee", "MINER'S COFFEE", "", "potion", coffee, Rarity.Common, 1, new St { swing = 0.15f, speed = 0.1f }, null);
            Tr("lantern", "LANTERN OIL", "+30% LIGHT RADIUS", "potion", orange, Rarity.Common, 2, default, p => p.s.lightMul += 0.3f);
            Tr("prospector", "PROSPECTOR", "+30% GOLD FROM ORE", "gem", gold, Rarity.Common, 1, new St { luck = 0.5f }, p => p.s.goldMul += 0.3f);
            Tr("magnet", "MAGNET", "PULLS GOLD FROM FAR AWAY", "horseshoe", red, Rarity.Common, 1, default, p => p.s.magnet *= 2f);
            Tr("thick", "THICK SKIN", "LONGER MERCY TIME AFTER HITS", "shield", brown, Rarity.Common, 1, new St { hp = 20 }, p => p.s.invulnBonus += 0.6f);
            Tr("lucky", "LUCKY COIN", "DUG BLOCKS SOMETIMES DROP GOLD", "coin", lime, Rarity.Common, 1, new St { luck = 1f }, p => p.s.luckyChance += 0.06f);
            Tr("greedy", "GREEDY GLOVES", "COINS ARE WORTH 25% MORE", "fist", gold, Rarity.Common, 1, new St { dmg = 0.3f }, p => p.s.coinMul += 0.25f);
            Tr("nighteyes", "NIGHT EYES", "THE DARK IS LESS DARK", "eye", purple, Rarity.Common, 1, default, p => p.s.darkMul *= 0.85f);
            Tr("feather", "FEATHER", "HOLD JUMP TO FALL SLOWLY", "feather", white, Rarity.Common, 1, new St { jump = 0.1f }, p => p.s.glide = Mathf.Max(p.s.glide, 1));
            Tr("moss", "MOSS BANDAGE", "HEAL 40 HP EVERY 60 DEPTH", "leaf", green, Rarity.Uncommon, 1, default, p => { p.s.mossEvery = 60; p.nextMoss = p.Depth + 60; }).weight = 2;
            Tr("hardhat", "HARD HAT", "FALLING BOULDERS CAN'T CRUSH YOU", "helmet", yellow, Rarity.Common, 1, new St { mining = 1 }, p => p.s.hardhat = true);

            // uncommon
            Tr("cloud", "CLOUD STEP", "+1 MID-AIR JUMP", "wing", pale, Rarity.Uncommon, 2, new St { jump = 0.1f }, p => p.s.airJumps++);
            Tr("pouch", "BATTERY PACK", "+1 MAX SPECIAL CHARGE", "bag", blue, Rarity.Uncommon, 1, default, p => { p.s.extraCharges++; });
            Tr("fuse", "DYNAMO", "SPECIALS CHARGE 25% FASTER", "hourglass", orange, Rarity.Uncommon, 1, default, p => p.s.chargeMul *= 0.75f);
            Tr("hymn", "WAR HYMN", "KILLS CHARGE SPECIALS 40% MORE", "hourglass", blue, Rarity.Uncommon, 1, default, p => p.s.killEnergy *= 1.4f);
            Tr("sparkplug", "SPARK PLUG", "SPECIALS CHARGE 15% FASTER", "coil", yellow, Rarity.Uncommon, 1, default, p => p.s.chargeMul *= 0.85f);
            Tr("soul", "SOUL JAR", "KILLS CHARGE SPECIALS TWICE AS MUCH", "potion", pink, Rarity.Uncommon, 1, default, p => p.s.killEnergy *= 2f);
            Tr("ember", "EMBER CORE", "DUG BLOCKS SPARK AND BURN ENEMIES", "orb", orange, Rarity.Uncommon, 1, new St { dmg = 0.3f }, p => p.s.ember = Mathf.Max(p.s.ember, 1));
            Tr("echo", "ECHO CHAMBER", "20% CHANCE EACH ATTACK FIRES TWICE", "ring", purple, Rarity.Uncommon, 1, default, p => p.s.echoChance += 0.2f);
            Tr("thorns", "THORN MAIL", "GETTING HIT HURTS ENEMIES NEARBY", "shield", green, Rarity.Uncommon, 1, new St { hp = 40 }, p => p.s.thorns = Mathf.Max(p.s.thorns, 3));
            Tr("stomp", "STOMP BOOTS", "JUMP ON ENEMIES TO HURT THEM", "boot", brown, Rarity.Uncommon, 1, new St { speed = 0.1f }, p => p.s.stomp = Mathf.Max(p.s.stomp, 2));
            Tr("sharp", "SHARP EDGE", "+15% CRIT CHANCE (3X DAMAGE)", "blade", grey, Rarity.Uncommon, 1, new St { dmg = 0.5f }, p => p.s.critChance += 0.15f);
            Tr("powder", "BLACK POWDER", "ALL EXPLOSIONS 35% BIGGER AND STRONGER", "potion", dark, Rarity.Uncommon, 1, default, p => p.s.blastMul *= 1.35f);
            Tr("aftershock", "AFTERSHOCK", "BROKEN BLOCKS CRACK THE ONE BEHIND", "rock", brown, Rarity.Uncommon, 1, new St { mining = 1 }, p => p.s.aftershock = true);
            Tr("goldtooth", "GOLD TOOTH", "HEAL 40 HP PER 250 GOLD PICKED UP", "tooth", gold, Rarity.Rare, 1, default, p => { if (p.s.goldToothEvery == 0) p.s.goldToothEvery = 250; }).weight = 1;
            Tr("coil", "COIL SPRING", "HOLD DOWN WHILE JUMPING TO SUPER JUMP", "coil", grey, Rarity.Uncommon, 1, new St { jump = 0.1f }, p => p.s.coil = true);
            Tr("bombbag", "BOMB BAG", "+1 ITEM SLOT AND A FREE BOMB", "bag", dark, Rarity.Uncommon, 1, default, p => { p.s.itemCap++; p.AddItem(Bomb, 1); });
            Tr("lasersight", "LASER SIGHT", "TIGHTER SPREAD AND A RED AIMING LASER", "eye", red, Rarity.Uncommon, 1, default, p => { p.s.laserSight = true; p.s.spreadMul *= 0.45f; });
            Tr("speedloader", "SPEED LOADER", "RELOAD 25% FASTER", "ring", steel, Rarity.Common, 2, default, p => p.s.reloadMul *= 0.75f);
            Tr("extmag", "EXTENDED MAG", "MAGAZINES HOLD 35% MORE", "bag", grey, Rarity.Uncommon, 1, default, p => p.s.magMul += 0.35f);
            Tr("steady", "STEADY HANDS", "TAKE 50% LESS DAMAGE WHILE RELOADING", "shield", steel, Rarity.Uncommon, 1, default, p => p.s.reloadArmor = Mathf.Max(p.s.reloadArmor, 0.5f));
            Tr("heatsink", "HEAT SINK", "ENERGY WEAPONS HEAT 25% LESS, COOL 50% FASTER", "coil", cyan, Rarity.Uncommon, 1, default, p => { p.s.heatMul *= 0.75f; p.s.coolMul *= 1.5f; });
            Tr("tactical", "TACTICAL RELOAD", "FIRST SHOT AFTER A RELOAD DEALS 2X DAMAGE", "blade", orange, Rarity.Rare, 1, default, p => p.s.tactical = true);
            Tr("rage", "RAGE MUSHROOM", "", "mushroom", red, Rarity.Uncommon, 1, new St { dmg = 1f, swing = 0.1f, speed = -0.1f }, null);

            // rare (chests from depth 50, shops, altars)
            Tr("blast", "BLAST PROOF", "IMMUNE TO EXPLOSIONS", "shield", red, Rarity.Rare, 1, default, p => p.s.blastProof = true);
            Tr("fangs", "VAMPIRE FANGS", "HEAL 30 HP EVERY 12 KILLS", "tooth", red, Rarity.Rare, 1, new St { dmg = 0.3f }, p => { if (p.s.vampire == 0) p.s.vampire = 12; }).weight = 1;
            Tr("midas", "MIDAS TOUCH", "ENEMIES DROP DOUBLE GOLD", "fist", gold, Rarity.Rare, 1, new St { luck = 1f }, p => p.s.killGoldMul = 2);
            Tr("chain", "CHAIN REACTION", "ENEMIES EXPLODE WHEN THEY DIE", "skull", orange, Rarity.Rare, 1, default, p => p.s.chainExplode = Mathf.Max(p.s.chainExplode, 1));
            Tr("glass", "GLASS CANNON", "", "gem", pale, Rarity.Rare, 1, new St { dmg = 1f, mining = 2, hp = -60 }, null);
            Tr("starmap", "STAR MAP", "AN ARROW POINTS TO NEARBY ALTARS", "star", gold, Rarity.Rare, 1, new St { luck = 1f }, p => p.s.starmap = Mathf.Max(p.s.starmap, 1));
            Tr("capacitor", "CAPACITOR", "+1 MAX CHARGE, SPECIALS CHARGE 20% FASTER", "hourglass", yellow, Rarity.Rare, 1, default, p => { p.s.extraCharges++; p.s.chargeMul *= 0.8f; });
            Tr("ironlung", "IRON LUNG", "", "heart", grey, Rarity.Rare, 1, new St { hp = 75, speed = -0.1f }, null);

            // diggers' own skills: each one starts with theirs. Win with a digger and theirs can turn up for everyone.
            Own("bram", Tr("bulldozer", "BADGER CLAWS", "WALK STRAIGHT THROUGH BLOCKS YOU CAN BREAK IN ONE HIT", "fist", orange, Rarity.Rare, 1, default, p => p.s.bulldozer = Mathf.Max(p.s.bulldozer, 1)));
            Tr("pickpocket", "PICKPOCKET", "DASHING THROUGH A FOE STEALS 3 GOLD FROM IT", "coin", orange, Rarity.Rare, 1, default, p => p.s.pickpocket = true);
            Tr("slam", "GROUND SLAM", "PRESS DOWN IN THE AIR TO SLAM THE GROUND", "fist", steel, Rarity.Rare, 1, default, p => p.s.slam = true);
            Tr("walljump", "WALL JUMP", "HUG A WALL IN THE AIR AND PRESS JUMP TO KICK OFF IT", "wing", blue, Rarity.Rare, 1, default, p => p.s.wallJump = true);
            Own("tor", Tr("ironhide", "IRON HIDE", "TAKE 25% LESS DAMAGE FROM EVERYTHING", "shield", grey, Rarity.Rare, 1, new St { hp = 25 }, p => p.s.armor = Mathf.Max(p.s.armor, 0.25f)));
            Own("liv", Tr("stillness", "DEADLY STILLNESS", "STAND STILL FOR HALF A SECOND AND YOUR NEXT SHOT ALWAYS CRITS", "eye", cyan, Rarity.Rare, 1, default, p => p.s.stillCrit = true));
            Tr("deathsguide", "DEATH'S GUIDE", "KILLS RECHARGE YOUR SPECIAL 3X AS MUCH", "skull", purple, Rarity.Rare, 1, default, p => p.s.killEnergy *= 3f);

            Own("nell", Tr("kindle", "KINDLING", "25% CHANCE (MORE WITH LUCK) THAT A SHOT IS A FIRE SHOT", "flame", orange, Rarity.Rare, 1, default, p => p.s.fireChance += 0.25f));
            Own("ribb", Tr("toxin", "TOXIC SKIN", "25% CHANCE (MORE WITH LUCK) THAT A SHOT POISONS", "drop", lime, Rarity.Rare, 1, default, p => p.s.poisonChance += 0.25f));
            Own("fin", Tr("gills", "GILLS", "BREATHE UNDER WATER FOREVER", "leaf", cyan, Rarity.Rare, 1, default, p => p.s.gills = true));
            Tr("flippers", "FLIPPERS", "SWIM 1.5X AS FAST AS YOU RUN, SHOOT UP WHEN HOLDING JUMP AND LEAP HIGH OUT OF THE WATER", "boot", cyan, Rarity.Uncommon, 1, default, p => p.s.flippers = true);
            Own("vex", Tr("ambush", "AMBUSH", "HITS FROM BEHIND DEAL 1.5X DAMAGE", "eye", purple, Rarity.Rare, 1, default, p => p.s.backstab = true));
            Own("nyx", Tr("claws", "SHARP CLAWS", "+25% MELEE DAMAGE", "blade", purple, Rarity.Rare, 1, default, p => p.s.meleeMul += 0.25f));
            Own("ink", Tr("tentacles", "TENTACLES", "LITTLE TENTACLES GRAB NEARBY WALLS. WHILE ONE HOLDS ON, YOU FLY WHERE YOU HOLD", "coil", pink, Rarity.Rare, 1, default, p => p.s.tentacles = true));

            // relics: only from puzzle altars
            Tr("ghostload", "GHOST RELOAD", "UNTOUCHABLE WHILE RELOADING AN EMPTY WEAPON", "feather", purple, Rarity.Relic, 1, default, p => p.s.reloadGhost = true);
            Tr("phoenix", "PHOENIX FEATHER", "CHEAT DEATH ONCE", "feather", orange, Rarity.Rare, 1, default, p => p.s.phoenix++);
            Tr("adamant", "ADAMANT HEART", "FULL HEAL", "heart", purple, Rarity.Relic, 1, new St { hp = 120 }, p => p.s.hp = p.s.maxHp);
            Tr("worldeater", "WORLD EATER", "", "skull", purple, Rarity.Relic, 1, new St { mining = 3, dmg = 1f }, null);
            Tr("crown", "MINER'S CROWN", "", "crown", gold, Rarity.Relic, 1, new St { hp = 60, dmg = 0.5f, swing = 0.2f, speed = 0.1f, mining = 1, jump = 0.1f, luck = 1f }, null);
            Tr("bottle", "LIGHTNING BOTTLE", "+1 MAX CHARGE, SPECIALS CHARGE 2X FASTER", "potion", cyan, Rarity.Relic, 1, default, p => { p.s.extraCharges++; p.s.chargeMul *= 0.5f; });
            // the dragons' hearts: only ever dropped by the dragons themselves
            Tr("heart_unda", "HEART OF UNDA", "BREATHE AND SWIM IN WATER. SHOTS FREEZE", "heart", U.C(90, 170, 255), Rarity.Relic, 1, new St { hp = 100 },
                p => { p.s.gills = true; p.s.flippers = true; p.s.shotFrost = true; }).owner = "_boss";
            Tr("heart_ignis", "HEART OF IGNIS", "FIRE AND LAVA CAN'T HURT YOU. SHOTS BURN", "heart", U.C(255, 110, 40), Rarity.Relic, 1, new St { hp = 100 },
                p => { p.s.fireImmune = true; p.s.lavaImmune = true; p.s.shotFire = true; }).owner = "_boss";


            // ---------- run-changers (Isaac-style): every shot, your stats, how you move ----------
            Color32 violet = U.C(170, 110, 255), lens = U.C(255, 70, 80), splashC = U.C(90, 170, 255), shell = U.C(190, 140, 80);
            // damage over time
            Tr("brew", "WITCH'S BREW", "POISON AND BURNING HURT 75% MORE AND LAST 50% LONGER", "potion", violet, Rarity.Uncommon, 1, default,
                p => { p.s.dotMul *= 1.75f; p.s.dotDur *= 1.5f; }).tags = new[] { "inferno", "plague" };
            // "all shots are ..."
            Tr("dragon", "DRAGON'S BREATH", "ALL SHOTS SET FOES ALIGHT, AND LIGHT GRASS AND WOOD", "flame", orange, Rarity.Rare, 1, default, p => p.s.shotFire = true).tags = new[] { "inferno" };
            Tr("viper", "VIPER FANG", "ALL SHOTS POISON", "tooth", lime, Rarity.Uncommon, 1, default, p => p.s.shotPoison = true).tags = new[] { "plague" };
            Tr("frostheart", "FROST HEART", "ALL SHOTS FREEZE FOES (EACH FOE ONCE EVERY 6S)", "snowflake", cyan, Rarity.Rare, 1, default, p => p.s.shotFrost = true).tags = new[] { "tech" };
            Tr("cap", "BLASTING CAP", "ALL SHOTS EXPLODE. THESE BLASTS NEVER HURT YOU", "bomb", orange, Rarity.Rare, 1, default, p => p.s.shotExplode = true).tags = new[] { "bombardier" };
            Tr("lens", "LASER LENS", "ALL SHOTS ARE PIERCING LASERS. BOMBS AND ROCKETS BECOME LASERS THAT EXPLODE", "lens", lens, Rarity.Rare, 1, default, p => p.s.shotLaser = true).tags = new[] { "tech" };
            Tr("seeker", "SEEKER'S EYE", "ALL SHOTS HOME IN ON FOES", "eye", violet, Rarity.Uncommon, 1, default, p => p.s.shotHoming = true).tags = new[] { "tech" };
            Tr("splash", "SPLASH DAMAGE", "SHOTS BURST INTO 2 SMALL COPIES WHEN THEY HIT SOMETHING (25% DAMAGE, BLAST AND SPEED)", "drop", splashC, Rarity.Rare, 1, default, p => p.s.splash = true).tags = new[] { "gunslinger" };
            var prism = Tr("prism", "SHRAPNEL PRISM", "EVERY BLAST YOU CAUSE SPRAYS 6+ LASERS. BIGGER BLASTS, MORE LASERS", "prism", pink, Rarity.Rare, 1, default, p => p.s.prism = Mathf.Max(p.s.prism, 1));
            prism.tags = new[] { "bombardier", "tech" }; prism.weight = 1;
            Tr("chamber", "PRESSURE CHAMBER", "HOLD FIRE TO CHARGE: UP TO 2.5X SIZE, DAMAGE AND BLAST. AUTOMATIC GUNS CHARGE A WILD BURST OF UP TO HALF A MAG", "chamber", steel, Rarity.Rare, 1, default, p => p.s.chargeShot = true).tags = new[] { "gunslinger", "tech" };
            // more barrels: the best one counts, and it multiplies with the TRIPLE modifier
            Tr("double", "DOUBLE BARREL", "EVERY SHOT FIRES TWICE. -25% FIRE RATE", "barrel2", steel, Rarity.Uncommon, 1, default, p => p.s.multiShot = Mathf.Max(p.s.multiShot, 2)).tags = new[] { "gunslinger" };
            Tr("triple", "TRIPLE BARREL", "EVERY SHOT FIRES 3 TIMES. -40% FIRE RATE", "barrel3", steel, Rarity.Rare, 1, default, p => p.s.multiShot = Mathf.Max(p.s.multiShot, 3)).tags = new[] { "gunslinger" };
            var quad = Tr("quad", "QUAD BARREL", "EVERY SHOT FIRES 4 TIMES. -50% FIRE RATE", "barrel4", steel, Rarity.Rare, 1, default, p => p.s.multiShot = Mathf.Max(p.s.multiShot, 4));
            quad.tags = new[] { "gunslinger" }; quad.weight = 1;
            // multipliers
            var ogre = Tr("ogre", "OGRE'S SKULL", "DAMAGE X1.5", "skull", green, Rarity.Rare, 1, new St { dmg = 0.5f }, p => p.s.dmgMult *= 1.5f);
            ogre.tags = new[] { "behemoth" }; ogre.look = "horns";
            var truffle = Tr("truffle", "MAGIC TRUFFLE", "DAMAGE X1.25. YOU GROW", "mushroom", violet, Rarity.Rare, 1, new St { hp = 50, speed = 0.1f, jump = 0.05f }, p => { p.s.dmgMult *= 1.25f; p.s.bodyScale += 0.12f; });
            truffle.tags = new[] { "behemoth", "plague" };
            Tr("hummingbird", "HUMMINGBIRD HEART", "FIRE RATE X2.2, DAMAGE X0.5", "heart", pink, Rarity.Uncommon, 1, default, p => { p.s.rateMult *= 2.2f; p.s.dmgMult *= 0.5f; p.s.shotSize *= 0.85f; }).tags = new[] { "gunslinger" };
            Tr("snail", "SNAIL SHELL", "FIRE RATE X0.45, DAMAGE X2.6, HUGE SHOTS", "snail", shell, Rarity.Uncommon, 1, default, p => { p.s.rateMult *= 0.45f; p.s.dmgMult *= 2.6f; p.s.shotSize *= 1.3f; }).tags = new[] { "behemoth" };
            // flight
            var wings = Tr("wings", "WINGS OF ICARUS", "UNLIMITED MID-AIR JUMPS", "wing", gold, Rarity.Rare, 1, default, p => p.s.wings = true);
            wings.tags = new[] { "seraph" }; wings.look = "wings";
            var lev = Tr("levitate", "LEVITATION", "FLOAT WHERE YOU HOLD (W A S D). YOU DRIFT ON WHEN YOU LET GO. JUMP FOR A BOOST", "orb", violet, Rarity.Rare, 1, default, p => p.s.levitate = true);
            lev.tags = new[] { "seraph" }; lev.look = "runes";
            var jet = Tr("jetpack", "JETPACK", "HOLD JUMP IN THE AIR TO FLY UP. 1.5S OF FUEL, REFILLS ON THE GROUND", "jetpack", steel, Rarity.Uncommon, 1, default, p => p.s.jetpack = true);
            jet.tags = new[] { "seraph" }; jet.look = "jetpack";


            // lingering shots, the god-ray, swords
            Tr("linger", "LINGERING SHOTS", "YOUR SHOTS BRAKE HARD AND HANG IN THE AIR FOR A LONG WHILE", "hourglass", violet, Rarity.Uncommon, 1, default, p => p.s.linger = true).tags = new[] { "tech" };
            var ray = Tr("godray", "GOD-RAY", "MYTHIC. EVERY SHOT IS A HUGE, EPIC LASER THAT TEARS THROUGH FOES AND ROCK. ALL LASERS GROW", "lens", gold, Rarity.Relic, 1, new St { dmg = 0.5f }, p => p.s.megaLaser = true);
            ray.tags = new[] { "tech" }; ray.look = "visor";
            Tr("blades", "THOUSAND BLADES", "ALL SHOTS ARE FLYING SWORDS: MELEE DAMAGE (+20%), AND THEY BAT ENEMY SHOTS BACK", "blade", steel, Rarity.Rare, 1, default, p => p.s.swordShots = true).tags = new[] { "brawler" };
            // melee
            Tr("knuckles", "BRASS KNUCKLES", "+30% MELEE DAMAGE: MELEE WEAPONS, KICKS, STOMPS, SLAMS, DASH HITS AND FLYING SWORDS", "fist", gold, Rarity.Common, 2, default, p => p.s.meleeMul += 0.3f).tags = new[] { "brawler" };
            Tr("steeltoe", "STEEL TOE CAPS", "KICKS AND STOMPS HIT TWICE AS HARD AND SEND FOES FLYING FURTHER", "boot", steel, Rarity.Uncommon, 1, default, p => p.s.kickMul *= 2f).tags = new[] { "brawler" };
            Tr("gauntlet", "GIANT'S GAUNTLET", "MELEE DAMAGE X1.6. MELEE WEAPONS REACH 25% FURTHER", "fist", red, Rarity.Rare, 1, default, p => { p.s.meleeMul *= 1.6f; p.s.meleeReach *= 1.25f; }).tags = new[] { "brawler", "behemoth" };
            // pets
            Pet("petbat", "BABY BAT", "A LITTLE BAT THAT FLUTTERS ROUND YOUR HEAD AND DIVES AT FOES", "wing", purple, Rarity.Uncommon, "bat");
            Pet("petwisp", "WILL-O'-WISP", "A WISP AT YOUR SHOULDER THAT SHOOTS FOES IT CAN SEE", "orb", cyan, Rarity.Uncommon, "wisp");
            Pet("petbeetle", "PET BEETLE", "TROTS AFTER YOU, HOPS UP LEDGES AND RAMS FOES", "rock", green, Rarity.Common, "beetle");
            Pet("petorb", "SPIRIT ORB", "CIRCLES YOU, HURTS FOES IT TOUCHES AND SWALLOWS ENEMY SHOTS", "ring", pale, Rarity.Rare, "orb");
            Pet("petspore", "SPORE BUDDY", "FOLLOWS YOUR TRAIL AND PUFFS POISON AT FOES AROUND IT", "mushroom", lime, Rarity.Uncommon, "spore");
            Tags("brawler", "stomp", "slam", "sharp");
            Tags("plague", "petspore");
            Tags("seraph", "petbat");
            Tags("tech", "petwisp");


            // ---------- more run-changers ----------
            Color32 ghostC = U.C(190, 210, 255), stormC = U.C(255, 236, 90);
            // shots
            Tr("bouncy", "BOUNCY ROUNDS", "ALL SHOTS BOUNCE OFF WALLS. EACH ONE YOU OWN ADDS ANOTHER BOUNCE", "ring", lime, Rarity.Uncommon, 3, default, p => p.s.extraBounces++).tags = new[] { "gunslinger" };
            Tr("swell", "SWELL SHOT", "SHOTS LEAVE 3X AS BIG AND STRONG, THEN SHRINK AND SLOW FAST. HIT CLOSE FOR BIG DAMAGE", "orb", pink, Rarity.Rare, 1, default, p => p.s.swell = true).tags = new[] { "behemoth" };
            Tr("giantrounds", "GIANT ROUNDS", "ALL SHOTS TWICE AS BIG. DAMAGE X1.15", "coin", steel, Rarity.Uncommon, 1, default, p => { p.s.shotSize *= 2f; p.s.dmgMult *= 1.15f; }).tags = new[] { "behemoth" };
            Tr("wasp", "WASP NEST", "FIRE RATE X2.5, BUT TINY SHOTS AND DAMAGE X0.6", "bag", yellow, Rarity.Uncommon, 1, default, p => { p.s.rateMult *= 2.5f; p.s.dmgMult *= 0.6f; p.s.shotSize *= 0.6f; }).tags = new[] { "gunslinger" };
            Tr("spectral", "SPECTRAL SHOTS", "WHERE A SHOT STOPS, ITS GHOST FLIES ON THROUGH THE ROCK (FURTHER WITH MORE MINING POWER), HURTING FOES BUT NOT BLOCKS", "skull", ghostC, Rarity.Rare, 1, default, p => p.s.spectral = true).tags = new[] { "tech" };
            Tr("splinters", "SOUL SPLINTERS", "A HOMING GHOST BURSTS OUT OF EVERY SHOT AND HUNTS FOES THROUGH ROCK", "feather", ghostC, Rarity.Rare, 1, default, p => p.s.ghostSwarm = true).tags = new[] { "tech" };
            Tr("wildcard", "WILD CARD", "SOME SHOTS GET A RANDOM EXTRA: EXPLOSIVE, POISON, FIRE, FROST, HOMING, PIERCING OR BOUNCY", "star", pink, Rarity.Uncommon, 1, new St { luck = 0.5f }, p => p.s.wildShots = true);
            Tr("phantom", "PHANTOM ARSENAL", "NOW AND THEN A GHOST OF YOUR GUN DRIFTS OUT AND FIRES ITS SHOTS AT THE CURSOR", "blade", ghostC, Rarity.Rare, 1, default, p => p.s.ghostGun = true).tags = new[] { "gunslinger" };
            // knockback
            Tr("slugs", "HEAVY SLUGS", "KNOCKBACK X2", "rock", steel, Rarity.Common, 1, new St { dmg = 0.3f }, p => p.s.knockMul *= 2f).tags = new[] { "brawler" };
            Tr("gale", "GALE CHARM", "KNOCKBACK X1.6", "feather", cyan, Rarity.Uncommon, 1, new St { speed = 0.1f }, p => p.s.knockMul *= 1.6f).tags = new[] { "seraph" };
            Tr("ram", "BATTERING RAM", "HARD HITS SEND FOES FLYING INTO EACH OTHER AND THE WALLS, LIKE A KICK. KNOCKBACK X1.3", "skull", brown, Rarity.Rare, 1, default, p => { p.s.ramming = true; p.s.knockMul *= 1.3f; }).tags = new[] { "brawler" };
            // mythic (relic tier: altars, golden crates, and now and then a chest)
            Tr("supernova", "SUPERNOVA SPLASH", "MYTHIC. SHOTS BURST INTO 4 SMALL COPIES WHEN THEY HIT SOMETHING", "drop", gold, Rarity.Relic, 1, default, p => p.s.splash4 = true).tags = new[] { "gunslinger" };
            Tr("cloudburst", "CLOUD BURST", "MYTHIC. SHOTS SPLIT INTO 3 SMALL SHOTS (30%) THAT DRIFT AWAY SLOWLY AND HANG IN THE AIR", "drop", ghostC, Rarity.Relic, 1, default, p => p.s.cloudSplit = true).tags = new[] { "tech" };
            Tr("mirror", "MIRROR SOUL", "MYTHIC. EVERY SHOT FIRES TWICE, WITH NO PENALTY", "gem", ghostC, Rarity.Relic, 1, default, p => p.s.freeDouble = true).tags = new[] { "gunslinger" };
            Tr("writhing", "THE WRITHING", "MYTHIC. YOUR SHOTS HATCH INTO FRIENDLY WORMS THAT BURROW TO FOES AND BITE THEM 3 TIMES", "coil", pink, Rarity.Relic, 1, default, p => p.s.wormShots = true).tags = new[] { "pets" };
            // defence
            var buck = Tr("buckler", "BUCKLER", "A SHIELD FLOATS WHERE YOU AIM AND SWALLOWS ENEMY SHOTS", "shield", steel, Rarity.Rare, 1, default, p => p.s.buckler = true);
            buck.look = "buckler"; buck.tags = new[] { "brawler" };
            var ward = Tr("wardstone", "WARDSTONE", "A HOLY WARD SOAKS THE FIRST HIT IN EVERY BIOME", "rock", gold, Rarity.Rare, 1, default, p => p.s.mantle = true);
            ward.look = "ward"; ward.tags = new[] { "seraph" };
            Tr("fireproof", "FIREPROOF CLOAK", "YOU CAN'T BE SET ALIGHT", "shield", orange, Rarity.Uncommon, 1, default, p => p.s.fireImmune = true).tags = new[] { "inferno" };
            Tr("antidote", "ANTIDOTE", "POISON CAN'T HURT YOU", "potion", lime, Rarity.Uncommon, 1, default, p => p.s.poisonImmune = true).tags = new[] { "plague" };
            Tr("flak", "FLAK SUIT", "IMMUNE TO EXPLOSIONS, BUT IT'S HEAVY: SLOWER, LOWER JUMPS", "shield", dark, Rarity.Uncommon, 1, new St { speed = -0.15f, jump = -0.1f }, p => p.s.blastProof = true).tags = new[] { "bombardier" };
            Tr("stormheart", "STORM HEART", "ELECTRICITY CAN'T HURT YOU, AND WATER YOU WADE INTO CRACKLES WITH CHARGE THAT HURTS FOES", "hourglass", stormC, Rarity.Rare, 1, default, p => { p.s.shockImmune = true; p.s.electrify = true; }).tags = new[] { "tech" };
            // cheating death
            Tr("rabbitfoot", "RABBIT'S FOOT", "A 25% CHANCE (MORE WITH LUCK) TO GET BACK UP WITH 50 HP WHEN YOU DIE", "fist", lime, Rarity.Rare, 1, new St { luck = 1f }, p => p.s.gambleRevive = true);
            Tr("ankh", "ANKH", "COME BACK ONCE WITH FULL HEALTH", "crown", gold, Rarity.Rare, 1, default, p => p.s.ankh++).weight = 2;
            Tr("twinsouls", "TWIN SOULS", "COME BACK TWICE, BUT WITH ONLY 10 HP", "heart", ghostC, Rarity.Rare, 1, default, p => p.s.twinLives += 2).weight = 2;
            // shops
            Tr("membercard", "MEMBER'S CARD", "SHOPS ARE 50% OFF", "coin", gold, Rarity.Rare, 1, default, p => p.s.shopMul *= 0.5f).weight = 2;
            Tr("ledger", "MERCHANT'S LEDGER", "SHOPS STOCK ONE MORE THING", "bag", brown, Rarity.Uncommon, 2, default, p => p.s.shopExtra++);
            // pets
            Pet("petdisc", "SAWBLADE PUP", "HOLD FIRE AND IT WINDS UP; LET GO AND IT FLIES AT THE CURSOR, BOUNCING AND SLICING", "ring", steel, Rarity.Uncommon, "disc");
            Pet("petfrost", "FROST SPRITE", "FREEZES AND NIPS FOES THAT COME CLOSE, THEN RESTS FOR A FEW SECONDS", "snowflake", cyan, Rarity.Uncommon, "frost");
            Pet("magpie", "MAGPIE", "SNAPS UP GOLD AND GEMS LYING AROUND. AT 200 GOLD WORTH IT DROPS 4 TREASURES", "feather", dark, Rarity.Rare, "magpie");
            Tr("lantern2", "SPIRIT LANTERN", "SLAIN FOES SOMETIMES LEAVE A FRIENDLY SPIRIT PET THAT FIGHTS UNTIL IT HAS DEALT ITS SHARE", "potion", ghostC, Rarity.Uncommon, 1, default, p => p.s.dropPets = true).tags = new[] { "pets" };
            Tr("flyswarm", "FLY SWARM", "EVERY HIT HATCHES A FLY THAT HUNTS FOES FOR A FEW SECONDS (THEY COUNT AS PETS)", "skull", lime, Rarity.Rare, 1, default, p => p.s.flyHits = true).tags = new[] { "pets", "plague" };
            Tr("treats", "BEAST TREATS", "PETS, SENTRIES AND OTHER HELPERS HIT 60% HARDER", "bag", orange, Rarity.Uncommon, 1, default, p => p.s.petMul *= 1.6f).tags = new[] { "pets" };
            Tr("venomtreat", "VENOM TREATS", "YOUR PETS POISON WHAT THEY HIT", "tooth", lime, Rarity.Uncommon, 1, default, p => p.s.petPoison = true).tags = new[] { "pets", "plague" };
            Tr("embertreat", "EMBER TREATS", "YOUR PETS SET WHAT THEY HIT ALIGHT", "flame", orange, Rarity.Uncommon, 1, default, p => p.s.petFire = true).tags = new[] { "pets", "inferno" };

            // older trinkets join the transformation sets too
            Tags("seraph", "feather", "cloud", "spring");
            Tags("inferno", "ember", "phoenix", "lantern");
            Tags("plague", "thorns", "moss", "rage");
            Tags("bombardier", "powder", "blast", "chain", "bombbag");
            Tags("gunslinger", "echo", "speedloader", "extmag", "tactical", "lasersight", "diamondbit");
            Tags("behemoth", "glass", "whetstone", "worldeater", "ironlung", "rage", "jerky");
            Tags("tech", "heatsink", "capacitor", "sparkplug", "bottle");

            // ---------- combos ----------
            // Only trinkets (skills) form combos. They are small bonuses on top, not run-defining jumps.
            C("goat", "MOUNTAIN GOAT", "+0.1 SPEED, +0.1 JUMP", new[] { "swift", "spring" }, p => { p.s.speedMul += 0.1f; p.s.jumpMul += 0.1f; });
            C("frenzy", "PICK FRENZY", "BREAKING A BLOCK CUTS 25% OFF YOUR NEXT SHOT DELAY", new[] { "grip", "diamondbit" }, p => p.s.pickFrenzy = true);
            C("mastersmith", "MASTER SMITH", "+0.25 DAMAGE, +1 MINING", new[] { "whetstone", "grip" }, p => { p.s.damage += 0.25f; p.s.digPower++; });
            C("highroller", "HIGH ROLLER", "+1 LUCK", new[] { "marble", "lucky" }, p => p.s.luck += 1f);
            C("bigboned", "BIG BONED", "+30 MAX HP", new[] { "jerky", "ironlung" }, p => { p.s.maxHp += 30; p.s.hp += 30; });
            C("overdrive", "OVERDRIVE", "+0.08 SPEED, +0.08 FIRE RATE", new[] { "coffee", "swift" }, p => { p.s.speedMul += 0.08f; p.s.swing += 0.08f; });
            C("safety", "SAFETY FIRST", "SPIKES CAN'T HURT YOU", new[] { "hardhat", "thick" }, p => p.s.spikeProof = true);
            C("worksong", "WORK SONG", "KILLS CHARGE SPECIALS 15% MORE", new[] { "hymn", "coffee" }, p => p.s.killEnergy *= 1.15f);
            C("shortcircuit", "SHORT CIRCUIT", "SPECIALS CHARGE ANOTHER 8% FASTER", new[] { "sparkplug", "fuse" }, p => p.s.chargeMul *= 0.92f);
            C("berserker", "BERSERKER", "+50% DAMAGE AT 100 HP OR LESS", new[] { "rage", "fangs" }, p => p.s.berserk = true);
            C("tesla", "TESLA COIL", "+1 MAX SPECIAL CHARGE", new[] { "capacitor", "pouch" }, p => p.s.extraCharges++);
            C("stormbottle", "STORM IN A BOTTLE", "KILLS CHARGE SPECIALS 30% MORE", new[] { "bottle", "hymn" }, p => p.s.killEnergy *= 1.3f);
            C("goldrush", "GOLD RUSH", "ORE GOLD +25%, COINS +10%", new[] { "prospector", "greedy" }, p => { p.s.goldMul += 0.25f; p.s.coinMul += 0.1f; });
            C("clover", "FOUR LEAF CLOVER", "CHESTS HAVE A 25% CHANCE OF A SECOND TREASURE", new[] { "lucky", "moss" }, p => p.s.clover = true);
            C("polarity", "POLARITY", "KILLS DROP +1 GOLD, BIGGER MAGNET", new[] { "magnet", "midas" }, p => { p.s.killGoldBonus += 1; p.s.magnet *= 1.3f; });
            C("ironclad", "IRONCLAD", "THORNS DEAL 4 IN A BIGGER RADIUS", new[] { "thick", "thorns" }, p => { p.s.thorns = 4; p.s.thornsR = 2.8f; });
            C("owl", "OWL SIGHT", "ENEMIES GLOW IN THE DARK", new[] { "nighteyes", "lantern" }, p => p.s.darkMul *= 0.95f);
            C("angel", "ANGEL WINGS", "BETTER GLIDE. KILLS RESTORE AIR JUMPS", new[] { "feather", "cloud" }, p => p.s.glide = 2);
            C("bloodsucker", "BLOODSUCKER", "FANGS HEAL EVERY 9 KILLS, TOOTH EVERY 180 GOLD", new[] { "goldtooth", "fangs" }, p => { p.s.vampire = 9; p.s.goldToothEvery = 180; });
            C("demolition", "DEMOLITION", "A FREE BOMB EVERY 75 SECONDS", new[] { "powder", "bombbag" }, p => p.s.bombRestock = true);
            C("demoexpert", "DEMOLITION EXPERT", "CHAIN BLASTS ARE TWICE AS BIG", new[] { "blast", "chain" }, p => p.s.chainExplode = 2);
            C("molten", "MOLTEN CORE", "BREAKING BLOCKS MELTS SOFT DIRT AROUND", new[] { "ember", "aftershock" }, p => p.s.ember = 2);
            C("echochamber", "RESONANCE", "ECHO ATTACKS CRIT HALF THE TIME", new[] { "echo", "sharp" }, null);
            C("trampoline", "TRAMPOLINE", "STOMPS DEAL 3 AND BOUNCE YOU HIGHER", new[] { "stomp", "coil" }, p => { p.s.stomp = 3; p.s.stompBounce = 16.5f; });
            C("overcharge", "OVERCHARGE", "+1 MAX CHARGE", new[] { "fuse", "pouch" }, p => p.s.extraCharges++);
            C("deathengine", "DEATH ENGINE", "KILLS CHARGE SPECIALS 25% MORE", new[] { "soul", "chain" }, p => p.s.killEnergy *= 1.25f);
            C("steamroller", "STEAMROLLER", "BULLDOZE BLOCKS UP TO 2 HITS", new[] { "bulldozer", "grip" }, p => p.s.bulldozer = Mathf.Max(p.s.bulldozer, 2));
            C("glasscannon", "GLASS CANNON", "CRITS DEAL 4X, +5% CRIT CHANCE", new[] { "glass", "sharp" }, p => { p.s.critMul = Mathf.Max(p.s.critMul, 4f); p.s.critChance += 0.05f; });
            C("kingmidas", "KING MIDAS", "ALL GOLD +50%", new[] { "crown", "midas" }, p => p.s.coinMul *= 1.5f);
            C("firebird", "FIREBIRD", "LAVA CAN'T HURT YOU. REVIVING EXPLODES", new[] { "phoenix", "ember" }, p => p.s.lavaImmune = true);
            C("titan", "TITAN", "+50 MAX HP", new[] { "adamant", "heart" }, p => { p.s.maxHp += 50; p.s.hp += 50; });
            C("maw", "THE MAW", "BULLDOZE ANY BREAKABLE BLOCK", new[] { "worldeater", "bulldozer" }, p => p.s.bulldozer = 3);
            C("deadeye", "DEADEYE", "+5% CRIT CHANCE, CRITS DEAL 3.5X", new[] { "lasersight", "sharp" }, p => { p.s.critChance += 0.05f; p.s.critMul = Mathf.Max(p.s.critMul, 3.5f); });
            C("quickdraw", "QUICKDRAW", "RELOAD ANOTHER 10% FASTER", new[] { "speedloader", "coffee" }, null);
            C("bandolier", "BANDOLIER", "MAGAZINES HOLD 15% MORE", new[] { "extmag", "bombbag" }, p => p.s.magMul += 0.15f);
            C("bulwark", "BULWARK", "TAKE 65% LESS DAMAGE WHILE RELOADING", new[] { "steady", "thick" }, p => p.s.reloadArmor = 0.65f);
            C("cryocore", "CRYO CORE", "ENERGY WEAPONS COOL 50% FASTER", new[] { "heatsink", "capacitor" }, null);
            C("doubletap", "DOUBLE TAP", "THE FIRST SHOT AFTER A RELOAD ALWAYS CRITS", new[] { "tactical", "sharp" }, null);
            C("spectre", "SPECTRE", "RELOADS ARE 20% FASTER", new[] { "ghostload", "steady" }, p => p.s.reloadMul *= 0.8f);
            C("parkour", "PARKOUR", "WALL JUMPS GIVE BACK YOUR AIR JUMPS", new[] { "walljump", "cloud" }, null);
            C("aftershock2", "SEISMIC SLAM", "GROUND SLAMS HIT TWICE AS WIDE", new[] { "slam", "aftershock" }, null);
            C("juggernaut", "JUGGERNAUT", "TAKE ANOTHER 10% LESS DAMAGE", new[] { "ironhide", "ironlung" }, p => p.s.armor = Mathf.Min(0.5f, p.s.armor + 0.1f));
            C("marksman", "MARKSMAN", "CRITS DEAL 4X", new[] { "stillness", "lasersight" }, p => p.s.critMul = Mathf.Max(p.s.critMul, 4f));
            C("phantom", "PHANTOM", "PICKPOCKET DASHES ALSO HURT FOES", new[] { "pickpocket", "thorns" }, p => p.s.dashHit = true);
            C("navigator", "NAVIGATOR", "STAR MAP ALSO POINTS TO SHOPS", new[] { "starmap", "nighteyes" }, p => p.s.starmap = 2);


            // synergies between the run-changers
            C("napalm", "NAPALM", "YOUR BLASTS LEAVE THE GROUND BURNING", new[] { "cap", "dragon" }, p => p.s.napalm = true);
            C("deathstar", "DEATH STAR", "PRISM LASERS EXPLODE, AND THERE ARE 4 MORE", new[] { "prism", "lens" }, p => p.s.prism = 2);
            C("rocketman", "ROCKET MAN", "JETPACK FLAMES SET FOES BELOW YOU ALIGHT", new[] { "jetpack", "dragon" }, p => p.s.rocketman = true);
            C("plaguebearer", "PLAGUE BEARER", "POISONED FOES BURST INTO POISON CLOUDS", new[] { "viper", "brew" }, p => p.s.plague = true);
            C("bullethell", "BULLET HELL", "DOUBLE/TRIPLE/QUAD BARRELS SLOW YOUR SHOOTING ONLY HALF AS MUCH", new[] { "quad", "hummingbird" }, p => p.s.multiPenalty *= 0.5f);
            C("holylight", "HOLY LIGHT", "HOMING LASERS. DAMAGE X1.15", new[] { "seeker", "lens" }, p => p.s.dmgMult *= 1.15f);
            C("shrapnelstorm", "SHRAPNEL STORM", "SPLASH SHARDS HIT TWICE AS HARD AND BLOW UP TWICE AS BIG", new[] { "splash", "cap" }, p => p.s.splashMul = 2f);

            // ---------- TRANSFORMATIONS: own 3 trinkets of a set and you change for good ----------
            T("seraph", "SERAPH", "FLIGHT SET: +0.5 DAMAGE, +1 MID-AIR JUMP, BETTER GLIDE", "halo", p => { p.s.damage += 0.5f; p.s.airJumps++; p.s.glide = 2; });
            T("inferno", "INFERNO", "FIRE SET: YOU CAN'T BURN, AND ALL YOUR SHOTS DO", "flamecrown", p => { p.s.fireImmune = true; p.s.shotFire = true; });
            T("plague", "PLAGUE LORD", "POISON SET: POISON CAN'T TOUCH YOU. POISONED FOES BURST INTO CLOUDS. DOTS +25%", "flies", p => { p.s.poisonImmune = true; p.s.plague = true; p.s.dotMul *= 1.25f; });
            T("bombardier", "BOMBARDIER", "BLAST SET: IMMUNE TO EXPLOSIONS, BLASTS 20% BIGGER, 2 FREE BOMBS", "bombhat", p => { p.s.blastProof = true; p.s.blastMul *= 1.2f; p.AddItem(Bomb, 2); });
            T("gunslinger", "GUNSLINGER", "GUN SET: DOUBLE/TRIPLE BARRELS SLOW YOUR SHOOTING ONLY HALF AS MUCH. RELOAD 20% FASTER", "cowboy", p => { p.s.multiPenalty *= 0.5f; p.s.reloadMul *= 0.8f; });
            T("behemoth", "BEHEMOTH", "BRUTE SET: +60 MAX HP, DAMAGE X1.2. YOU GROW", "bighorns", p => { p.s.maxHp += 60; p.s.hp += 60; p.s.dmgMult *= 1.2f; p.s.bodyScale += 0.1f; });
            T("brawler", "BRAWLER", "MELEE SET: MELEE DAMAGE X1.25, KICKS AND STOMPS X1.25", "headband", p => { p.s.meleeMul *= 1.25f; p.s.kickMul *= 1.25f; });
            T("pets", "BEASTMASTER", "PET SET: YOUR PETS DEAL DOUBLE DAMAGE", null, p => p.s.petMul *= 2f);
            C("swordlaser", "LIGHTSABRES", "FLYING-SWORD LASERS: DAMAGE X1.2", new[] { "blades", "lens" }, p => p.s.dmgMult *= 1.2f);
            C("orbguard", "GUARDIAN ANGEL", "YOUR PETS HIT 50% HARDER", new[] { "petorb", "wings" }, p => p.s.petMul *= 1.5f);
            T("tech", "TECHNOMANCER", "TECH SET: ENERGY WEAPONS HEAT HALF AS FAST, FIRE RATE X1.15", "visor", p => { p.s.heatMul *= 0.5f; p.s.rateMult *= 1.15f; });

            // ---------- consumables (C) ----------
            Ale = Itm("ale", "DWARVEN ALE", "HEALS 100 HP", I["ale"], It.Ale, 60);
            Bomb = Itm("bomb", "BOMB", "BIG BLAST. THROWN", I["bomb"], It.Bomb, 45);
            Itm("flare", "FLARE", "LIGHTS UP THE DARK", I["flare"], It.Flare, 25);
            Itm("bore", "BORE CHARGE", "DRILLS A SHAFT STRAIGHT DOWN", I["bore"], It.Bore, 50);
            Itm("glimmer", "GLIMMER STONE", "LIGHTS UP THE WHOLE SCREEN FOR 15S. ENEMIES GLOW RED, TREASURE SPARKLES", Art.Trinket("orb", cyan), It.Glimmer, 55);
            Itm("sentry", "SENTRY", "DROPS A TURRET THAT SHOOTS NEARBY FOES FOR 10S", I["sentry"], It.Sentry, 90).minDepth = 15;
            var sb = Itm("superbomb", "SUPER BOMB", "CLEARS THE WHOLE SCREEN", I["superbomb"], It.SuperBomb, 260);
            sb.weight = 1; sb.minDepth = 40;

            // ---------- weapon modifiers (shops) ----------
            for (int m = 0; m < (int)Mod.COUNT; m++)
            {
                var md = new ModDef { id = "mod_" + m, kind = Kind.Mod, mod = (Mod)m, name = Mods.Names[m] + " MOD", desc = Mods.Desc[m], icon = Mods.Icon((Mod)m), price = 170 };
                ModDefs.Add(md);
                Reg(md);
            }

            // ---------- shop-only upgrades ----------
            Upg("pick+", "PICKAXE +1", "PERMANENT +1 MINING", I["pickaxe"], 100, p => { p.s.digPower++; p.s.pickBought++; });
            Upg("pack+", "BACKPACK +1", "CARRY ONE MORE ITEM", I["backpack"], 120, p => p.s.itemCap++);
            Upg("barrel", "ALE BARREL", "+40 MAX HP AND FULL HEAL", I["heart"], 220, p => { p.s.maxHp += 40; p.s.hp = p.s.maxHp; });
            Upg("stew", "HEARTY STEW", "HEAL TO FULL", I["ale"], 80, p => p.s.hp = p.s.maxHp);


            foreach (var t in Transforms.Keys) if (TagCount(t) < 3) Debug.LogWarning("DigDeep: transformation " + t + " has too few trinkets");
            foreach (var c in Combos)
                foreach (var n in c.needs)
                    if (!ById.ContainsKey(n)) Debug.LogWarning("DigDeep: combo " + c.id + " needs unknown id " + n);
        }

        static void Reg(Def d) => ById[d.id] = d;

        static SpecialDef S(string id, string name, string desc, Sprite icon, Sp type, float cd, int uses, int price, int minDepth)
        {
            var d = new SpecialDef { id = id, name = name, desc = desc, icon = icon, kind = Kind.Special, type = type, cd = cd, uses = uses, price = price, minDepth = minDepth };
            Specials.Add(d);
            Reg(d);
            return d;
        }

        static SkillDef Tr(string id, string name, string desc, string shape, Color32 col, Rarity r, int cap, St st, System.Action<Player> apply)
        {
            var d = new SkillDef { id = id, name = name, desc = desc, icon = Art.Trinket(shape, col), kind = Kind.Skill, cap = cap, rarity = r, st = st, apply = apply };
            switch (r)
            {
                case Rarity.Common: d.price = 70; d.weight = 10; break;
                case Rarity.Uncommon: d.price = 110; d.weight = 6; d.minDepth = 12; break;
                case Rarity.Rare: d.price = 170; d.weight = 3; d.minDepth = 25; break;
                default: d.price = 300; d.weight = 2; d.altarOnly = true; break;
            }
            Trinkets.Add(d);
            Reg(d);
            return d;
        }

        public static readonly Dictionary<string, ComboDef> Transforms = new Dictionary<string, ComboDef>();

        static void T(string tag, string name, string desc, string look, System.Action<Player> onActivate)
        {
            var c = new ComboDef { id = "tf_" + tag, name = name, desc = desc, needs = new string[0], tag = tag, count = 3, look = look, onActivate = onActivate };
            Combos.Add(c);
            Transforms[tag] = c;
        }

        static void Tags(string tag, params string[] ids)
        {
            foreach (var id in ids)
            {
                if (!(ById.TryGetValue(id, out var d) && d is SkillDef k)) { Debug.LogWarning("DigDeep: no trinket " + id); continue; }
                if (k.tags == null) k.tags = new[] { tag };
                else if (System.Array.IndexOf(k.tags, tag) < 0) { var l = new List<string>(k.tags) { tag }; k.tags = l.ToArray(); }
            }
        }

        static int TagCount(string tag)
        {
            int n = 0;
            foreach (var t in Trinkets) if (t.tags != null && System.Array.IndexOf(t.tags, tag) >= 0) n++;
            return n;
        }

        static void Pet(string id, string name, string desc, string shape, Color32 col, Rarity r, string kind)
        {
            var d = Tr(id, name, desc, shape, col, r, 1, default, null);
            d.pet = kind;
            d.tags = new[] { "pets" };
        }

        static void Own(string ch, SkillDef d) { d.owner = ch; d.price = 220; d.weight = 2; }

        // Has this digger's own skill been unlocked for everyone (by winning a run with them)?
        public static bool Unlocked(SkillDef d) => string.IsNullOrEmpty(d.owner) || PlayerPrefs.GetInt("digdeep_won_" + d.owner, 0) == 1;

        static void C(string id, string name, string desc, string[] needs, System.Action<Player> onActivate)
        {
            Combos.Add(new ComboDef { id = id, name = name, desc = desc, needs = needs, onActivate = onActivate });
        }

        public static void AddAttack(AttackDef d) { Attacks.Add(d); Reg(d); }

        static ItemDef Itm(string id, string name, string desc, Sprite icon, It type, int price)
        {
            var d = new ItemDef { id = id, name = name, desc = desc, icon = icon, kind = Kind.Item, type = type, price = price };
            Items.Add(d);
            Reg(d);
            return d;
        }

        static void Upg(string id, string name, string desc, Sprite icon, int price, System.Action<Player> apply)
        {
            var d = new UpgradeDef { id = id, kind = Kind.Upgrade, name = name, desc = desc, icon = icon, price = price, apply = apply };
            Upgrades.Add(d);
            Reg(d);
        }

        public static int ComboCount(string id)
        {
            int n = 0;
            foreach (var c in Combos) if (System.Array.IndexOf(c.needs, id) >= 0) n++;
            return n;
        }

        public static bool IsComboPiece(Def d, Player p)
        {
            foreach (var c in Combos)
            {
                if (p.Combo(c.id) || System.Array.IndexOf(c.needs, d.id) < 0) continue;
                foreach (var n in c.needs)
                    if (n != d.id && p.HasSkill(n)) return true;
            }
            return false;
        }

        public static string Name(string id) => ById.TryGetValue(id, out var d) ? d.name : id.ToUpperInvariant();

        public static int Price(Def d, int depth, Player p)
        {
            float f = d.price * (1f + depth / 220f);
            if (p != null && p.Cursed(Curse.Rust)) f *= 1.5f;   // CURSE OF RUST
            if (p != null) f *= p.s.shopMul;                     // MEMBER'S CARD
            if (d.id == "pick+") f *= 1f + 0.6f * p.s.pickBought;
            return Mathf.Max(5, Mathf.RoundToInt(f / 5f) * 5);
        }

        static T Pick<T>(List<T> list, int depth, System.Random r, System.Func<T, bool> ok, System.Func<T, float> weight = null) where T : Def
        {
            float total = 0f;
            foreach (var d in list)
                if (!d.altarOnly && d.minDepth <= depth && ok(d)) total += weight != null ? weight(d) : d.weight;
            if (total <= 0f) return null;
            float roll = (float)r.NextDouble() * total;
            T last = null;
            foreach (var d in list)
            {
                if (d.altarOnly || d.minDepth > depth || !ok(d)) continue;
                last = d;
                roll -= weight != null ? weight(d) : d.weight;
                if (roll < 0f) return d;
            }
            return last;
        }

        // Better rarities fade in with depth, so the first biome mostly offers common gear.
        public static float DepthWeight(Rarity rar, int depth)
        {
            switch (rar)
            {
                case Rarity.Uncommon: return Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(depth / 100f));
                case Rarity.Rare: return Mathf.Lerp(0.35f, 1f, Mathf.Clamp01((depth - 25) / 150f));
                default: return 1f;
            }
        }

        public static Def RollAttack(int depth, Player p, System.Random r) =>
            Pick(Attacks, depth, r, d => !p.Holds(d.id) && d != Pickaxe, d => d.weight * DepthWeight(d.rarity, depth));

        public static Def RollMod(Player p, System.Random r)
        {
            var w = p.weapon;
            var pool = new List<ModDef>();
            foreach (var m in ModDefs) if (w == null || Mods.Fits(w.def, m.mod)) pool.Add(m);
            if (pool.Count == 0) pool.AddRange(ModDefs);
            return pool[r.Next(pool.Count)];
        }
        public static Def RollSpecial(int depth, Player p, System.Random r) => Pick(Specials, depth, r, d => d != p.special);
        public static Def RollItem(int depth, Player p, System.Random r) => Pick(Items, depth, r, d => true);

        // Luck shifts trinket odds towards rarer ones, like Isaac.
        public static Def RollTrinket(int depth, Player p, System.Random r)
        {
            float luck = Mathf.Clamp(p.s.luck, -3f, 10f);
            // PICKPOCKET only works while dashing: never offered to diggers without a dash
            bool dashes = p.special != null && p.special.id == "dash";
            return Pick(Trinkets, depth, r, d => p.SkillCount(d.id) < d.cap && Unlocked(d) && !p.SeenOnce(d.id) && (dashes || d.id != "pickpocket"), d =>
            {
                float w = d.weight * DepthWeight(d.rarity, depth);
                if (IsComboPiece(d, p)) w *= 0.55f;   // the second half of a combo is harder to find
                if (d.rarity == Rarity.Uncommon) w *= 1f + 0.15f * luck;
                else if (d.rarity == Rarity.Rare) w *= 1f + 0.3f * luck;
                return Mathf.Max(0.5f, w);
            });
        }

        // Chests: mostly small trinkets, Isaac-style.
        public static Def RollChest(int depth, Player p, System.Random r)
        {
            // now and then a chest holds a mythic (relic) trinket, more often with luck and depth
            if (r.NextDouble() < p.Lucky(0.02f + depth * 0.0001f))
            {
                var mythic = new List<SkillDef>();
                foreach (var t in Trinkets) if (t.rarity == Rarity.Relic && p.SkillCount(t.id) < t.cap && Unlocked(t) && !p.SeenOnce(t.id)) mythic.Add(t);
                if (mythic.Count > 0) return mythic[r.Next(mythic.Count)];
            }
            int k = r.Next(100);
            Def d;
            if (k < 78) d = RollTrinket(depth, p, r);
            else d = RollItem(depth, p, r);   // specials never change: each digger keeps their own
            return d ?? RollTrinket(depth, p, r) ?? RollItem(depth, p, r);
        }

        // Altar rewards: things you shouldn't have yet - relics, altar-only weapons, or deep-only loot.
        public static Def RollRare(int depth, Player p, System.Random r)
        {
            var pool = new List<Def>();
            var w = new List<int>();
            void Add(Def d, int weight) { if (weight > 0) { pool.Add(d); w.Add(weight); } }

            foreach (var t in Trinkets)
            {
                if (p.SkillCount(t.id) >= t.cap || !Unlocked(t) || p.SeenOnce(t.id)) continue;
                if (t.rarity == Rarity.Relic) Add(t, 5);
                else if (t.minDepth > depth) Add(t, t.rarity == Rarity.Rare ? 3 : 2);
                else if (t.rarity == Rarity.Rare) Add(t, 1);
            }
            foreach (var a in Attacks)
            {
                if (p.Holds(a.id) || a == Pickaxe) continue;
                if (a.altarOnly) Add(a, 4);
                else if (a.minDepth > depth) Add(a, 2);
            }

            if (pool.Count == 0) return RollTrinket(depth + 999, p, r) ?? Ale;
            int total = 0;
            foreach (var x in w) total += x;
            int roll = r.Next(total);
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= w[i];
                if (roll < 0) return pool[i];
            }
            return pool[pool.Count - 1];
        }
    }
}
