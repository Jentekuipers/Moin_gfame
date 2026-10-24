using UnityEngine;

namespace DigDeep
{
    public abstract class Entity
    {
        public GameObject go;
        public SpriteRenderer sr;
        public Vector2 pos;
        public bool dead;

        protected static Game G => Game.I;
        protected static World Wd => Game.I.world;

        protected void MakeGO(string name, Sprite s, int order)
        {
            go = new GameObject(name);
            go.transform.SetParent(G.runRoot, false);
            sr = Art.AddSR(go, s, order);
            go.transform.position = new Vector3(pos.x, pos.y, 0);
        }

        protected void Place(float yOff = 0f)
        {
            if (go != null) go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y + yOff), 0);
        }

        public abstract void Tick(float dt);
        public virtual void AddLight(Lighting l) { }
        public virtual bool AlwaysActive => false;

        public virtual void Kill()
        {
            if (dead) return;
            dead = true;
            if (go != null) Object.Destroy(go);
        }
    }

    // Every trinket and combo just nudges numbers / flags in here; gameplay code reads them.
    [System.Serializable]
    public class Stats
    {
        // ---- Isaac-style core stats (shown on the HUD) ----
        public const int Heart = 100;    // one heart on the HUD = 100 health
        public int maxHp = 3 * Heart, hp = 3 * Heart;
        public float damage = 1f;        // multiplies weapon damage (DMG)
        public float swing = 1f;         // multiplies fire rate (like Isaac's tears)
        public float speedMul = 1f;      // move speed (SPD)
        public int digPower = 1;         // mining strength (MINE)
        public bool pickpocket, slam, wallJump, stillCrit;   // diggers' own skills
        public float armor;              // fraction of incoming damage ignored (IRON HIDE)
        public float jumpMul = 1f;       // jump height (JMP)
        public float luck;               // crit / proc chances, better chest loot (LUCK)

        public int itemCap = 3, extraCharges, airJumps, gold, kills, pickBought;
        public float lightMul = 1f, goldMul = 1f, magnet = 1.6f, invulnBonus;
        public float coinMul = 1f, darkMul = 1f, blastMul = 1f;

        // ---- active item (special) charging ----
        public float chargeMul = 1f;     // energy needed per charge segment (lower = faster)
        public float digEnergy = 1f;     // (unused: digging no longer charges specials)
        public float killEnergy = 1f;    // multiplier on energy per kill
        public int refundSegs;           // segments instantly refilled after using a special
        public bool hardhat, spikeProof, berserk;

        public int bulldozer;            // 0 off, 1 one-shot blocks, 2 two-shot blocks, 3 anything breakable
        public bool blastProof;
        public int vampire;              // heal every N kills (0 = off)
        public float luckyChance;        // chance a dug block drops a coin
        public int glide;                // 0 off, 1 feather, 2 angel
        public int mossEvery;            // heal every N depth (0 = off)
        public int ember;                // 0 off, 1 sparks, 2 sparks + melts soft blocks
        public float echoChance;
        public int thorns;
        public float thornsR = 2.2f;
        public int stomp;
        public float stompBounce = 12f;
        public float critChance, critMul = 3f;
        public bool seeAll;
        public int bossExtra;   // TROPHY CASE: bosses offer this many more   // YOU SEE ALL: no darkness at all
        public bool aftershock, coil, lavaImmune, pickFrenzy, clover, bombRestock, deathEngine;
        public int goldToothEvery;
        public int chainExplode;         // 0 off, 1 small, 2 big
        public int killGoldMul = 1, killGoldBonus;
        public int phoenix;
        public int orbitals;
        public int starmap;              // 0 off, 1 altars, 2 altars + shops
        public bool dash, dashHit;       // QUICKSTEP: shift to dash (PHANTOM: dashing through foes hurts them)
        public float dashCd = 4f;
        public bool laserSight;          // LASER SIGHT: red aiming laser
        public float spreadMul = 1f;     // multiplies weapon spread
        public float reloadMul = 1f;     // reload time multiplier (lower = faster)
        public float magMul = 1f;        // magazine size multiplier
        public float heatMul = 1f;       // heat per shot multiplier for energy weapons
        public float coolMul = 1f;       // how fast energy weapons cool down
        public float reloadArmor;        // fraction of damage blocked while reloading
        public bool reloadGhost;         // untouchable while reloading an EMPTY weapon
        public bool tactical;            // first shot after a reload deals double damage

        // ---- Isaac-style shot effects, multipliers and flight (see Items in Loot.cs and README)
        public bool shotExplode, shotPoison, shotFire, shotFrost, shotLaser, shotHoming;   // "all shots are ..."
        public int multiShot = 1;        // DOUBLE / TRIPLE / QUAD BARREL: the best one you own counts
        public float multiPenalty = 1f;  // how hard multi-shots slow your fire rate (GUNSLINGER halves it)
        public float dmgMult = 1f;       // multiplies DMG on top of everything (OGRE'S SKULL, MAGIC TRUFFLE...)
        public float rateMult = 1f;      // multiplies fire rate on top of everything (HUMMINGBIRD HEART, SNAIL SHELL)
        public float shotSize = 1f;      // bigger (or smaller) shots, on top of the damage-based size
        public float dotMul = 1f;        // poison and burning hurt this much more
        public float dotDur = 1f;        // ... and last this much longer
        public int prism;                // SHRAPNEL PRISM: blasts you cause spray lasers (0 off, 1 on, 2 DEATH STAR)
        public bool wings, levitate, jetpack;
        public bool fireImmune, poisonImmune, plague, napalm, rocketman;
        public bool splash;              // SPLASH DAMAGE: shots burst into 3 small copies when they hit
        public bool linger, megaLaser, swordShots;   // LINGERING SHOTS, GOD-RAY, THOUSAND BLADES
        public float meleeMul = 1f, kickMul = 1f, meleeReach = 1f, petMul = 1f;
        public bool chargeShot;          // PRESSURE CHAMBER: hold fire to charge shots
        public float splashMul = 1f;     // SHRAPNEL STORM: stronger splash shards
        public float bodyScale = 1f;
        // ---- more run-changers (see README)
        public bool splash4, cloudSplit, swell, spectral, ghostSwarm, wildShots, ghostGun, freeDouble, wormShots;   // shot items
        public int extraBounces;          // BOUNCY ROUNDS (stacks)
        public float knockMul = 1f;       // knockback items
        public bool ramming;              // BATTERING RAM: hard hits send foes flying like a kick
        public bool dropPets, flyHits, petPoison, petFire;   // pet items
        public int ankh, twinLives;       // death defies (PHOENIX FEATHER is `phoenix`)
        public bool gambleRevive, mantle, buckler;
        public bool shockImmune, electrify;   // STORM HEART
        public float shopMul = 1f;        // MEMBER'S CARD
        public int shopExtra;             // MERCHANT'S LEDGER
        // ---- diggers' own kits
        public float fireChance, poisonChance;   // KINDLING (fox), TOXIC SKIN (frog)
        public bool gills, flippers;             // the dolphin
        public bool backstab;                    // AMBUSH (owl)
        public bool tentacles;                   // the octopus     // MAGIC TRUFFLE / BEHEMOTH: you grow (looks only)
    }

    public class ItemStack
    {
        public ItemDef def;
        public int count;
    }
}
