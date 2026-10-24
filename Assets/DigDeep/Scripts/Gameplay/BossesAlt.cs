using UnityEngine;

namespace DigDeep
{
    // =====================================================================================
    // The second boss of each biome: sometimes the hall holds this one instead.
    //   Topsoil: the BOULDER TROLL   Jungle: the HIVE QUEEN   Fungal: the TOADSTOOL KING   Magma: the FORGE GOLEM
    // (Crystal and Storm have the dragons.)
    // =====================================================================================

    // TOPSOIL: throws boulders, leaps and pounds the floor (shockwaves, falling rock), lowers its head and charges.
    // A charge into the wall stuns it for a moment.
    public class BoulderTroll : Boss
    {
        enum S { Walk, Wind, Crouch, Air, Brace, Charge, Stunned }
        static Sprite rock;
        static readonly Color Rock = new Color(0.65f, 0.55f, 0.45f);

        public BoulderTroll(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("THE BOULDER TROLL", "IT EATS STONES. AND DIGGERS", "troll", hp, 1.5f, new Vector2(0.95f, 1.15f), a, floor + Vector2.up * 1.16f);
            if (rock == null) rock = Art.TileSprite(Block.Boulder, Biomes.All[0]);
            blood = new Color(0.55f, 0.7f, 0.45f);
            Go(S.Walk, 1.5f);
        }

        void Go(S s, float time) { state = (int)s; stateT = time; }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            switch ((S)state)
            {
                case S.Walk:
                    FacePlayer();
                    vel.x = U.Approach(vel.x, facing * (Phase2 ? 3.4f : 2.5f), 15f * dt);
                    Move(dt);
                    if (stateT <= 0f)
                    {
                        cycle++;
                        vel.x = 0f;
                        switch (cycle % 3)
                        {
                            case 1: Go(S.Wind, 0.6f); break;
                            case 2: Go(S.Crouch, 0.4f); break;
                            default: Go(S.Brace, Phase2 ? 0.6f : 0.8f); G.sfx.Play("rumble", 0.8f, 0.8f); break;
                        }
                    }
                    break;
                case S.Wind:   // a boulder lifted over its head... then thrown at you
                    FacePlayer();
                    vel.x = 0f; Move(dt);
                    squash = -0.12f;
                    if (stateT <= 0f)
                    {
                        Vector2 from = pos + new Vector2(facing * 0.4f, half.y + 0.4f);
                        float dx = P.pos.x - from.x;
                        float tFly = Mathf.Clamp(Mathf.Abs(dx) / 9f, 0.5f, 1.2f);
                        Shot(from, new Vector2(dx / tFly, (P.pos.y - from.y) / tFly + 0.5f * 24f * tFly), rock, Rock, 24f, 0f, 1.1f);
                        if (Phase2)
                            for (int i = -1; i <= 1; i += 2)
                                Shot(from, new Vector2(dx / tFly + i * 3f, (P.pos.y - from.y) / tFly + 0.5f * 24f * tFly + 2f), rock, Rock, 24f, 0f, 0.6f);
                        G.sfx.Play("swing", 0.9f, 0.5f);
                        squash = 0.15f;
                        Go(S.Walk, Phase2 ? 1.2f : 1.8f);
                    }
                    break;
                case S.Crouch:
                    squash = 0.22f;
                    vel.x = 0f; Move(dt);
                    if (stateT <= 0f)
                    {
                        FacePlayer();
                        vel = new Vector2(Mathf.Clamp(P.pos.x - pos.x, -10f, 10f) * 0.85f, 16f);
                        G.sfx.Play("jump", 0.9f, 0.45f);
                        Go(S.Air, 3f);
                    }
                    break;
                case S.Air:
                    Move(dt);
                    if (grounded && vel.y <= 0f && stateT < 2.9f)
                    {
                        squash = 0.35f; vel.x = 0f;
                        G.sfx.Play("explode", 0.8f, 0.6f);
                        G.cam.Shake(0.5f);
                        FloorWave(1, Rock); FloorWave(-1, Rock);
                        CeilingRain(Phase2 ? 6 : 4, rock, Rock);
                        Go(S.Walk, Phase2 ? 1.3f : 2f);
                    }
                    break;
                case S.Brace:   // head down, stamping: a charge is coming
                    FacePlayer();
                    vel.x = 0f; Move(dt);
                    squash = 0.12f + Mathf.Sin(t * 40f) * 0.05f;
                    if (Random.value < 0.5f) G.fx.Burst(pos + new Vector2(-facing * half.x, -half.y + 0.1f), 2, Rock, 4f, 0.35f, 20f);
                    if (stateT <= 0f) { G.sfx.Play("dash", 1f, 0.45f); Go(S.Charge, 5f); }
                    break;
                case S.Charge:
                    vel.x = facing * (Phase2 ? 14f : 11.5f);
                    if (Random.value < 0.7f) G.fx.Burst(pos + new Vector2(0f, -half.y + 0.1f), 2, Rock, 4f, 0.3f, 20f);
                    if (Move(dt) || stateT <= 0f)
                    {
                        // smack into the wall: rock rains down and it reels
                        vel.x = 0f;
                        G.cam.Shake(0.6f);
                        G.sfx.Play("explode", 0.9f, 0.7f);
                        CeilingRain(Phase2 ? 7 : 5, rock, Rock);
                        squash = 0.4f;
                        Go(S.Stunned, Phase2 ? 1f : 1.5f);
                    }
                    break;
                case S.Stunned:
                    vel.x = 0f; Move(dt);
                    if (Random.value < 0.15f) G.fx.Burst(pos + Vector2.up * (half.y + 0.3f), 1, new Color(1f, 1f, 0.6f), 2f, 0.5f, 0f);
                    if (stateT <= 0f) Go(S.Walk, 1.4f);
                    break;
            }
            DrawBoss(0f);
        }

        public override bool Hurt(float d, Vector2 knock) => base.Hurt((S)state == S.Stunned ? d * 1.5f : d, knock);   // reeling: hit it hard
    }

    // JUNGLE: the queen of the bees. Calls her swarm, dives with her stinger, sheds rings of pollen and rains honey.
    public class HiveQueen : Boss
    {
        enum S { Hover, Summon, AimDive, Dive, Rise, Pollen, Honey }
        Vector2 hoverAt, diveV;
        int waves;
        float shotT;
        static readonly Color Pollen = new Color(1f, 0.9f, 0.35f), Honey = new Color(1f, 0.7f, 0.2f);

        public HiveQueen(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("THE HIVE QUEEN", "MOTHER OF THE SWARM", "hivequeen", hp, 1.4f, new Vector2(1.2f, 0.85f), a, new Vector2(floor.x, a.CeilY - 2.5f));
            blood = new Color(1f, 0.85f, 0.3f);
            Go(S.Hover, 1.5f);
        }

        void Go(S s, float time) { state = (int)s; stateT = time; }

        int Bees()
        {
            int n = 0;
            foreach (var e in G.ents) if (!e.dead && e is Bee) n++;
            return n;
        }

        void Fly(Vector2 to, float spd, float dt)
        {
            Vector2 d = to - pos;
            vel = Vector2.MoveTowards(vel, d.sqrMagnitude > 0.04f ? d.normalized * spd : Vector2.zero, 25f * dt);
            pos += vel * dt;
            pos.x = Mathf.Clamp(pos.x, arena.MinX + half.x, arena.MaxX - half.x);
            pos.y = Mathf.Clamp(pos.y, arena.FloorY + half.y, arena.CeilY - half.y);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            switch ((S)state)
            {
                case S.Hover:
                    FacePlayer();
                    hoverAt = new Vector2(Mathf.Clamp(P.pos.x + Mathf.Sin(t * 0.8f) * 6f, arena.MinX + 2f, arena.MaxX - 2f), arena.CeilY - 2.6f + Mathf.Sin(t * 2.2f) * 0.6f);
                    Fly(hoverAt, Phase2 ? 6f : 4.5f, dt);
                    if (stateT <= 0f)
                    {
                        cycle++;
                        switch (cycle % 4)
                        {
                            case 1: Go(S.Summon, 0.9f); G.sfx.Play("bat", 0.9f, 0.6f); break;
                            case 2: Go(S.AimDive, Phase2 ? 0.55f : 0.75f); G.sfx.Play("rumble", 0.6f, 1.6f); break;
                            case 3: waves = Phase2 ? 3 : 2; shotT = 0.3f; Go(S.Pollen, 3f); break;
                            default: Go(S.Honey, 1.4f); break;
                        }
                    }
                    break;
                case S.Summon:   // she trembles, and the swarm pours out of her
                    Fly(hoverAt, 1f, dt);
                    squash = Mathf.Sin(t * 50f) * 0.06f;
                    if (stateT <= 0f)
                    {
                        int n = Mathf.Min(Phase2 ? 5 : 3, 9 - Bees());
                        for (int i = 0; i < n; i++) G.Spawn(new Bee(pos + Random.insideUnitCircle * 0.8f, arena.Depth));
                        G.fx.Burst(pos, 20, Pollen, 6f, 0.5f, 0f);
                        Go(S.Hover, Phase2 ? 1.2f : 1.8f);
                    }
                    break;
                case S.AimDive:   // the stinger points at you... then she drops like a spear
                    FacePlayer();
                    vel = Vector2.zero;
                    flash = ((int)(stateT * 14f)) % 2 == 0 ? 0.03f : 0f;
                    if (stateT <= 0f)
                    {
                        diveV = (P.pos - pos).normalized * (Phase2 ? 18f : 15f);
                        G.sfx.Play("dash", 1f, 1.2f);
                        Go(S.Dive, 2f);
                    }
                    break;
                case S.Dive:
                    pos += diveV * dt;
                    if (Random.value < 0.6f) G.fx.Burst(pos, 1, Pollen, 2f, 0.3f, 0f);
                    if (pos.y <= arena.FloorY + half.y || pos.x <= arena.MinX + half.x || pos.x >= arena.MaxX - half.x || stateT <= 0f)
                    {
                        pos.x = Mathf.Clamp(pos.x, arena.MinX + half.x, arena.MaxX - half.x);
                        pos.y = Mathf.Max(pos.y, arena.FloorY + half.y);
                        G.cam.Shake(0.4f);
                        G.sfx.Play("explode", 0.6f, 1.1f);
                        PoisonRing(pos, Phase2 ? 12 : 8, 5.5f, Random.value * 30f);
                        vel = Vector2.zero;
                        Go(S.Rise, 1f);
                    }
                    break;
                case S.Rise:
                    Fly(new Vector2(pos.x, arena.CeilY - 2.6f), 5f, dt);
                    if (stateT <= 0f) Go(S.Hover, Phase2 ? 0.8f : 1.4f);
                    break;
                case S.Pollen:   // rings of pollen, each turned a little
                    Fly(hoverAt, 1.5f, dt);
                    shotT -= dt;
                    if (shotT <= 0f && waves > 0)
                    {
                        waves--; shotT = 0.6f;
                        PoisonRing(pos, Phase2 ? 14 : 11, 5f, t * 50f);
                        G.sfx.Play("shoot", 0.6f, 1.4f);
                    }
                    if (waves <= 0 && shotT <= 0f) Go(S.Hover, 1.4f);
                    break;
                case S.Honey:   // drops of honey from the ceiling: sticky, slowing
                    Fly(hoverAt, 2f, dt);
                    if (stateT <= 0f)
                    {
                        int n = Phase2 ? 9 : 6;
                        for (int i = 0; i < n; i++)
                        {
                            float x = Random.Range(arena.MinX + 0.6f, arena.MaxX - 0.6f);
                            Shot(new Vector2(x, arena.CeilY - 0.4f), Vector2.zero, Art.Spit, Honey, 26f, 0.5f + Random.value * 0.6f, 0.7f).chills = true;
                        }
                        Go(S.Hover, 1.6f);
                    }
                    break;
            }
            DrawBoss(0f);
            animT += dt * 3f;   // wings
        }

        void PoisonRing(Vector2 c, int n, float speed, float off)
        {
            for (int i = 0; i < n; i++)
            {
                float a = (off + i * 360f / n) * Mathf.Deg2Rad;
                Shot(c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed, Art.Seed, Pollen).poison = true;
            }
        }
    }

    // FUNGAL: a fat toad with a toadstool for a crown. Hops and slams (spore rings), coughs up drifting spore clouds,
    // lashes its tongue across the hall, and makes mushrooms burst out of the floor under you.
    public class ToadstoolKing : Boss
    {
        enum S { Sit, Crouch, Air, Cough, Tongue, Sprout }
        float shotT;
        int left;
        static readonly Color Spore = new Color(0.75f, 0.55f, 0.95f), Toxic = new Color(0.6f, 1f, 0.35f);

        public ToadstoolKing(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("THE TOADSTOOL KING", "HE WEARS THE ROT LIKE A CROWN", "toadstool", hp, 1.5f, new Vector2(1.3f, 1.05f), a, floor + Vector2.up * 1.06f);
            blood = new Color(0.7f, 0.6f, 0.4f);
            Go(S.Sit, 1.5f);
        }

        void Go(S s, float time) { state = (int)s; stateT = time; }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            switch ((S)state)
            {
                case S.Sit:
                    FacePlayer();
                    if (grounded) vel.x = U.Approach(vel.x, 0f, 20f * dt);
                    Move(dt);
                    if (stateT <= 0f)
                    {
                        cycle++;
                        switch (cycle % 4)
                        {
                            case 1: Go(S.Crouch, 0.5f); break;
                            case 2: left = Phase2 ? 4 : 3; shotT = 0.2f; Go(S.Cough, 3f); break;
                            case 3:
                                // a long warning band at mouth height on its side of you: the tongue shoots across
                                float x0 = facing > 0 ? pos.x : arena.MinX, x1 = facing > 0 ? arena.MaxX : pos.x;
                                G.Spawn(new Hazard(new Vector2((x0 + x1) * 0.5f, arena.FloorY + 0.9f), new Vector2((x1 - x0) * 0.5f, 0.45f), Phase2 ? 0.55f : 0.75f, 0.3f,
                                    new Color(1f, 0.45f, 0.55f), ContactDmg(arena.Depth)));
                                if (Phase2) G.Spawn(new Hazard(new Vector2((x0 + x1) * 0.5f, arena.FloorY + 3.4f), new Vector2((x1 - x0) * 0.5f, 0.4f), 1.1f, 0.3f,
                                    new Color(1f, 0.45f, 0.55f), ContactDmg(arena.Depth)));
                                Go(S.Tongue, Phase2 ? 1.4f : 1.1f);
                                G.sfx.Play("rumble", 0.6f, 1.3f);
                                break;
                            default: left = Phase2 ? 5 : 3; shotT = 0.1f; Go(S.Sprout, 4f); break;
                        }
                    }
                    break;
                case S.Crouch:
                    squash = 0.25f;
                    Move(dt);
                    if (stateT <= 0f)
                    {
                        FacePlayer();
                        vel = new Vector2(Mathf.Clamp(P.pos.x - pos.x, -11f, 11f) * 0.9f, 17f);
                        G.sfx.Play("jump", 0.9f, 0.4f);
                        Go(S.Air, 3f);
                    }
                    break;
                case S.Air:
                    Move(dt);
                    if (grounded && vel.y <= 0f && stateT < 2.9f)
                    {
                        squash = 0.4f; vel.x = 0f;
                        G.cam.Shake(0.5f);
                        G.sfx.Play("explode", 0.7f, 0.6f);
                        int n = Phase2 ? 14 : 10;
                        for (int i = 0; i < n; i++)
                        {
                            float a = (i * 180f / (n - 1)) * Mathf.Deg2Rad;   // a fan over the top
                            Shot(pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f, Art.Seed, Toxic).poison = true;
                        }
                        if (Phase2) { FloorWave(1, Toxic); FloorWave(-1, Toxic); }
                        Go(S.Sit, Phase2 ? 1.2f : 1.8f);
                    }
                    break;
                case S.Cough:   // slow spore clouds that drift after you
                    Move(dt);
                    shotT -= dt;
                    if (shotT <= 0f && left > 0)
                    {
                        left--; shotT = 0.6f;
                        squash = -0.15f;
                        Vector2 mouth = pos + new Vector2(facing * 0.9f, 0.2f);
                        Vector2 d = (P.pos - mouth).normalized;
                        var b = Shot(mouth, d * 2.6f, Art.Seed, Spore, 0f, 0f, 1.6f);
                        b.poison = true;
                        G.sfx.Play("splat", 0.5f, 0.7f);
                    }
                    if (left <= 0 && shotT <= 0f) Go(S.Sit, 1.5f);
                    break;
                case S.Tongue:
                    Move(dt);
                    squash = -0.1f;
                    if (stateT <= 0f) Go(S.Sit, 1.2f);
                    break;
                case S.Sprout:   // mushrooms burst out of the floor where you stand
                    Move(dt);
                    shotT -= dt;
                    if (shotT <= 0f && left > 0)
                    {
                        left--; shotT = Phase2 ? 0.45f : 0.6f;
                        float x = Mathf.Clamp(P.pos.x + Random.Range(-0.5f, 0.5f), arena.MinX + 0.6f, arena.MaxX - 0.6f);
                        var at = new Vector2(x, arena.FloorY + 1.2f);
                        G.Spawn(new Hazard(at, new Vector2(0.55f, 1.2f), 0.7f, 0.35f, Spore, ContactDmg(arena.Depth), false,
                            () => { for (int i = 0; i < 5; i++) { float a = (30f + i * 30f) * Mathf.Deg2Rad; Shot(at + Vector2.up, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4.5f, Art.Seed, Toxic, 9f).poison = true; } }));
                    }
                    if (left <= 0 && shotT <= 0f) Go(S.Sit, 1.4f);
                    break;
            }
            DrawBoss(0f);
        }
    }

    // MAGMA: an iron golem with a furnace for a heart. Pounds the floor alight, rains molten iron,
    // sweeps a flamethrower across the hall, and shrugs off rings of fireballs.
    public class ForgeGolem : Boss
    {
        enum S { Walk, Pound, Rain, Sweep, Ring }
        float shotT, ang;
        int left;
        static readonly Color Molten = new Color(1f, 0.55f, 0.15f);

        public ForgeGolem(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("THE FORGE GOLEM", "THE KING'S LAST SMITH", "forgegolem", hp, 1.5f, new Vector2(1.1f, 1.3f), a, floor + Vector2.up * 1.31f);
            blood = new Color(1f, 0.55f, 0.2f);
            fireLike = false;
            Go(S.Walk, 1.6f);
        }

        void Go(S s, float time) { state = (int)s; stateT = time; }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            switch ((S)state)
            {
                case S.Walk:
                    FacePlayer();
                    vel.x = U.Approach(vel.x, facing * (Phase2 ? 2.4f : 1.7f), 10f * dt);
                    Move(dt);
                    if (Random.value < 0.08f) G.sfx.Play("land", 0.25f, 0.5f);
                    if (stateT <= 0f)
                    {
                        cycle++; vel.x = 0f;
                        switch (cycle % 4)
                        {
                            case 1: Go(S.Pound, 0.9f); G.sfx.Play("rumble", 0.8f, 0.7f); break;
                            case 2: Go(S.Rain, 0.6f); break;
                            case 3: ang = -35f; Go(S.Sweep, 0.7f + 2.2f); break;
                            default: left = Phase2 ? 3 : 2; shotT = 0.5f; Go(S.Ring, 3f); break;
                        }
                    }
                    break;
                case S.Pound:   // fists raised... slam: the floor bursts into flame (jump onto a ledge!)
                    Move(dt);
                    squash = stateT > 0.2f ? -0.12f : 0.3f;
                    if (stateT <= 0f)
                    {
                        G.cam.Shake(0.7f);
                        G.sfx.Play("explode", 1f, 0.5f);
                        bool whole = Phase2;
                        float x0 = whole ? arena.MinX : (P.pos.x < arena.CenterX ? arena.MinX : arena.CenterX);
                        float x1 = whole ? arena.MaxX : (P.pos.x < arena.CenterX ? arena.CenterX : arena.MaxX);
                        G.Spawn(new FloorFire(arena, x0, x1, whole ? 3.5f : 4.5f));
                        FloorWave(1, Molten); FloorWave(-1, Molten);
                        Go(S.Walk, 2f);
                    }
                    break;
                case S.Rain:   // molten iron from the ceiling
                    Move(dt);
                    if (stateT <= 0f)
                    {
                        int n = Phase2 ? 10 : 7;
                        for (int i = 0; i < n; i++)
                        {
                            float x = Random.Range(arena.MinX + 0.6f, arena.MaxX - 0.6f);
                            Shot(new Vector2(x, arena.CeilY - 0.4f), Vector2.zero, Art.Fireball, Molten, 30f, 0.55f + Random.value * 0.6f, 0.8f).burns = true;
                        }
                        G.sfx.Play("rumble", 0.7f, 1.2f);
                        Go(S.Walk, 1.6f);
                    }
                    break;
                case S.Sweep:   // the furnace door opens: a stream of fire sweeping from the floor up over your head
                    Move(dt);
                    if (stateT > 2.2f)
                    {
                        if (Random.value < 0.5f) G.fx.Burst(pos + new Vector2(facing * 0.3f, 0.1f), 2, Molten, 3f, 0.3f, -3f);
                        break;
                    }
                    ang += dt * (Phase2 ? 50f : 38f);
                    shotT -= dt;
                    if (shotT <= 0f)
                    {
                        shotT = 0.06f;
                        Vector2 d = U.Rot(new Vector2(facing, 0f), ang * facing);
                        Shot(pos + new Vector2(facing * 0.3f, 0.1f), d * 11f, Art.Fireball, Molten, 0f, 0f, 0.7f).burns = true;
                        if (Random.value < 0.3f) G.sfx.Play("flame", 0.4f, 0.8f, 0.1f);
                    }
                    if (stateT <= 0f) Go(S.Walk, 1.4f);
                    break;
                case S.Ring:
                    Move(dt);
                    shotT -= dt;
                    if (shotT <= 0f && left > 0)
                    {
                        left--; shotT = 0.7f;
                        squash = 0.15f;
                        Ring(pos, Phase2 ? 16 : 12, 6f, t * 37f, Art.Fireball, Molten);
                        G.sfx.Play("shoot", 0.7f, 0.5f);
                    }
                    if (left <= 0 && shotT <= 0f) Go(S.Walk, 1.6f);
                    break;
            }
            DrawBoss(0f);
        }

        public override void AddLight(Lighting l) { l.Add(pos, 4f, 0.85f); }
    }
}
