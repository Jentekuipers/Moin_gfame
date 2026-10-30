using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // A playable digger. Since the gods' curse every dwarf is half-beast: each animal brings its own
    // stats, weapon, special (Shift) and two starting skills, the first one their very own. Budget: each trades roughly the same amount of power.
    public class CharDef
    {
        public string id, name, animal, title, blurb, trait;
        public int hp, mining;   // hp in health points (75 for the most mobile digger, 200 for the tank)
        public float dmg, swing, speed, jump, luck;
        public string weapon, special;
        public string[] skills;   // starting skills; the first is the digger's own (see SkillDef.owner)
        public System.Action<Player> innate;
        public Sprite[] frames;   // idle, run1, run2, jump, dig, sit
        public Color limb = new Color(0.55f, 0.28f, 0.52f);   // tentacle colour (the octopus)

        public void ApplyTo(Stats s)
        {
            s.maxHp = s.hp = hp;   // in health points
            s.damage = dmg;
            s.swing = swing;
            s.speedMul = speed;
            s.digPower = 1;   // there is no mining stat any more: how hard you hit rock is down to the weapon
            s.jumpMul = jump;
            s.luck = luck;
        }
    }

    public static class Chars
    {
        public static readonly List<CharDef> All = new List<CharDef>();
        static bool inited;

        // FIN is the digger everybody starts with (the others are unlocked, see Unlocks)
        public static int FinIndex { get { Init(); for (int i = 0; i < All.Count; i++) if (All[i].id == "fin") return i; return 0; } }

        public static void Init()
        {
            if (inited) return;
            inited = true;

            All.Add(new CharDef
            {
                id = "bram", name = "BRAM", animal = "MOLE", title = "THE MOLE", blurb = "A BORN DIGGER IN A RED BANDANA. TOUGH, A LITTLE SLOW.",
                trait = "BADGER CLAWS (WALKS THROUGH BLOCKS HE CAN BREAK IN ONE HIT) + LUCKY COIN",
                hp = 150, dmg = 1f, swing = 1f, speed = 0.95f, mining = 2, jump = 1.05f, luck = 0f,
                weapon = "revolver", special = "quickdig", skills = new[] { "bulldozer", "lucky" },
                frames = Art.HeroMole(),
            });
            All.Add(new CharDef
            {
                id = "nell", name = "NELL", animal = "FOX", title = "THE FOX", blurb = "QUICK AND FIERY. HER SHOTS CATCH ALIGHT, HER DASH SNUFFS FLAMES OUT.",
                trait = "KINDLING (25% OF SHOTS ARE FIRE SHOTS) + PICKPOCKET",
                hp = 90, dmg = 0.95f, swing = 1.15f, speed = 1.2f, mining = 1, jump = 1.1f, luck = 1.5f,
                weapon = "goldgun", special = "dash", skills = new[] { "kindle", "pickpocket" },
                frames = Art.HeroFox(),
            });
            All.Add(new CharDef
            {
                id = "ribb", name = "RIBB", animal = "FROG", title = "THE FROG", blurb = "SPRINGY AND TOXIC. LEAPS FIRST, SLAMS DOWN AFTER.",
                trait = "TOXIC SKIN (25% OF SHOTS POISON) + GROUND SLAM",
                hp = 110, dmg = 1f, swing = 1f, speed = 1.05f, mining = 1, jump = 1.25f, luck = 0.5f,
                weapon = "shotgun", special = "superjump", skills = new[] { "toxin", "slam" },
                frames = Art.HeroFrog(),
            });
            All.Add(new CharDef
            {
                id = "tor", name = "TOR", animal = "BEETLE", title = "THE BEETLE", blurb = "A WALKING WALL IN A HORNED SHELL. KICKS FIRST, ASKS LATER.",
                trait = "IRON HIDE (TAKES 25% LESS DAMAGE) + THICK SKIN",
                hp = 200, dmg = 0.95f, swing = 0.85f, speed = 0.9f, mining = 2, jump = 1f, luck = 0f,
                weapon = "smg", special = "kick", skills = new[] { "ironhide", "thick" },
                frames = Art.HeroBeetle(),
            });
            All.Add(new CharDef
            {
                id = "fin", name = "FIN", animal = "DOLPHIN", title = "THE DOLPHIN", blurb = "AT HOME IN THE WET. BRINGS THE SEA WITH HER.",
                trait = "GILLS (NEVER DROWNS) + FLIPPERS (FAST SWIMMER, BIG LEAPS OUT OF THE WATER)",
                hp = 110, dmg = 1f, swing = 1.05f, speed = 1.05f, mining = 1, jump = 1.1f, luck = 0.5f,
                weapon = "mortar", special = "tide", skills = new[] { "gills", "flippers" },
                frames = Art.HeroDolphin(),
            });
            All.Add(new CharDef
            {
                id = "vex", name = "VEX", animal = "OWL", title = "THE OWL", blurb = "A HOODED WATCHER WITH A LANTERN GAZE. STRIKES FROM BEHIND.",
                trait = "AMBUSH (HITS FROM BEHIND DEAL 1.5X) + FEATHER (HOLD JUMP TO GLIDE)",
                hp = 75, dmg = 0.95f, swing = 1.2f, speed = 1f, mining = 1, jump = 1.1f, luck = 0.5f,
                weapon = "boltcaster", special = "phase", skills = new[] { "ambush", "feather" },
                innate = p => p.s.lightMul += 0.15f,   // an owl sees a little further in the dark
                frames = Art.HeroOwl(),
            });
            All.Add(new CharDef
            {
                id = "nyx", name = "NYX", animal = "BLACK CAT", title = "THE BLACK CAT", blurb = "A KNIFE IN THE DARK. TURNS EVERY BLOW BACK ON ITS OWNER.",
                trait = "SHARP CLAWS (+25% MELEE DAMAGE) + SWIFT BOOTS",
                hp = 95, dmg = 1.05f, swing = 1.1f, speed = 1.15f, mining = 1, jump = 1.15f, luck = 1f,
                weapon = "dagger", special = "parry", skills = new[] { "claws", "swift" },
                frames = Art.HeroCat(),
            });
            All.Add(new CharDef
            {
                id = "ink", name = "INK", animal = "OCTOPUS", title = "THE OCTOPUS", blurb = "CLINGS TO EVERYTHING, HIDES FROM EVERYONE.",
                trait = "TENTACLES (GRAB WALLS AND FLY WHILE HOLDING ON) + NIGHT EYES",
                hp = 85, dmg = 0.95f, swing = 1f, speed = 1f, mining = 1, jump = 1f, luck = 0.5f,
                weapon = "laserpistol", special = "camo", skills = new[] { "tentacles", "nighteyes" },
                frames = Art.HeroOctopus(), limb = new Color(0.55f, 0.27f, 0.52f),
            });
            All.Add(new CharDef
            {
                id = "liv", name = "LIV", animal = "LYNX", title = "THE LYNX", blurb = "PATIENT, SILENT, ONE SHOT ONE KILL.",
                trait = "DEADLY STILLNESS (STAND STILL: NEXT SHOT CRITS) + LASER SIGHT",
                hp = 85, dmg = 1.1f, swing = 1f, speed = 1.1f, mining = 1, jump = 1.15f, luck = 1f,
                weapon = "scout", special = "grapple", skills = new[] { "stillness", "lasersight" },
                frames = Art.HeroLynx(),
            });
        }
    }
}
