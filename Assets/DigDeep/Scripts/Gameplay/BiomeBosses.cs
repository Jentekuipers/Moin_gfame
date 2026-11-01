using UnityEngine;

namespace DigDeep
{
    // The newer biomes' bosses.
    //  CLOUD REACHES  - NIMBUS (a thundercloud: rain, lightning, gusts, cloudlets) and IRIS, the rainbow dragon (Dragons.cs)
    //  CRIMSON FOREST - THE HEARTWOOD (a rooted tree: roots from the floor, blood rain, saplings, heartbeats) and
    //                   THE BLOODSTAG (charges wall to wall, stamps shockwaves, tosses its antlers)
    //  SUNKEN BEACH   - THE KING CRAB (sidles, snaps, blows bubbles, leaps and slams) and THE KRAKEN (rises out of a
    //                   flooded hall, tentacles burst from the floor, ink)
    //  NEON CANOPY    - LUMINA, THE NEON JAGUAR (pounces, vanishes, comes at you from behind, a trail of light)
    //  CORAL REEF     - THE ABYSSAL ANGLER (in the dark and the deep: lunges, its lure pulls you in)
    public abstract class BiomeBoss : Boss
    {
        protected void Go(int s, float time) { state = s; stateT = time; }
        protected Color Hit => new Color(1f, 0.4f, 0.4f);
        protected int Dmg => ContactDmg(arena.Depth);
    }

    // ================================================================== NIMBUS
    public class Nimbus : BiomeBoss
    {
        const int Drift = 0, Rain = 1, Bolts = 2, Gust = 3;
        float shotT; int gustDir;
        static readonly Color RainC = new Color(0.55f, 0.75f, 1f), BoltC = new Color(1f, 0.95f, 0.45f);

        public Nimbus(BossArena a, float hp)
        {
            SetupBoss("NIMBUS", "THE STORM THAT THINKS", "nimbus", hp, 2.2f, new Vector2(2.2f, 1f), a, new Vector2(a.CenterX, a.CeilY - 2.2f));
            Go(Drift, 2f);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            float hoverY = arena.CeilY - 2.2f + Mathf.Sin(t * 1.3f) * 0.4f;
            switch (state)
            {
                case Drift:
                    pos = Vector2.MoveTowards(pos, new Vector2(Mathf.Clamp(P.pos.x + Mathf.Sin(t * 0.7f) * 4f, arena.MinX + 2.5f, arena.MaxX - 2.5f), hoverY), 4f * dt);
                    if (stateT <= 0f)
                    {
                        cycle++;
                        int k = cycle % 3;
                        if (k == 1) { Go(Rain, Phase2 ? 4f : 3f); shotT = 0f; G.sfx.Play("rumble", 0.6f, 1.4f); }
                        else if (k == 2) { Go(Bolts, 1.2f); shotT = 0f; }
                        else { Go(Gust, 2.4f); gustDir = P.pos.x < pos.x ? -1 : 1; G.sfx.Play("dash", 0.8f, 0.5f); }
                        if (Phase2 && cycle % 4 == 0) for (int i = 0; i < 2; i++) G.Spawn(new Puffling(Mathf.FloorToInt(pos.x) + (i == 0 ? -2 : 2), Mathf.FloorToInt(pos.y), arena.Depth));   // cloudlets break off it
                    }
                    break;
                case Rain:   // it follows you and pours
                    pos = Vector2.MoveTowards(pos, new Vector2(Mathf.Clamp(P.pos.x, arena.MinX + 2.5f, arena.MaxX - 2.5f), hoverY), 5f * dt);
                    shotT -= dt;
                    if (shotT <= 0f)
                    {
                        shotT = Phase2 ? 0.07f : 0.1f;
                        var b = Shot(pos + new Vector2(Random.Range(-2f, 2f), -0.8f), new Vector2(0f, -9f), Art.Circle, RainC, 6f, 0f, 0.3f);
                        b.chills = Random.value < 0.2f;
                        b.body = RainC;
                    }
                    if (stateT <= 0f) Go(Drift, Phase2 ? 1.2f : 1.8f);
                    break;
                case Bolts:  // lightning strikes, marked first, one of them where you stand
                    if (shotT == 0f)
                    {
                        shotT = 1f;
                        int n = Phase2 ? 5 : 3;
                        for (int i = 0; i < n; i++)
                        {
                            float x = i == 0 ? P.pos.x : Random.Range(arena.MinX + 1f, arena.MaxX - 1f);
                            float h = (arena.CeilY - arena.FloorY) * 0.5f;
                            G.Spawn(new Hazard(new Vector2(x, arena.FloorY + h), new Vector2(0.55f, h), 0.9f, 0.25f, BoltC, Dmg, false,
                                () => { G.lighting.Flash(new Vector2(x, arena.FloorY + 3f), 8f, 0.4f); G.sfx.Play("zap", 0.7f, 0.7f); }));
                        }
                    }
                    if (stateT <= 0f) Go(Drift, 1.5f);
                    break;
                case Gust:   // a wind that shoves you across the hall, and a hail of ice with it
                    {
                        var p = P;
                        if (p.mode != Player.Mode.Dead) p.vel.x += gustDir * 22f * dt;
                        if (Random.value < 0.7f) G.fx.Burst(new Vector2(gustDir > 0 ? arena.MinX + 1f : arena.MaxX - 1f, Random.Range(arena.FloorY + 0.5f, arena.CeilY - 0.5f)), 1, new Color(0.9f, 0.95f, 1f, 0.7f), 14f * gustDir, 0.5f, 0f, 1f, false);
                        shotT -= dt;
                        if (shotT <= 0f) { shotT = 0.35f; var b = Shot(pos, new Vector2(gustDir * Random.Range(5f, 9f), Random.Range(-3f, 1f)), Art.Circle, new Color(0.85f, 0.95f, 1f), 4f, 0f, 0.35f); b.chills = true; }
                        if (stateT <= 0f) Go(Drift, 1.6f);
                        break;
                    }
            }
            DrawBoss(0f);
        }

        public override void AddLight(Lighting l) => l.Add(pos, state == Bolts ? 6f : 3.5f, 0.6f);
    }

    // ================================================================== THE HEARTWOOD
    public class Heartwood : BiomeBoss
    {
        const int Idle = 0, Roots = 1, BloodRain = 2, Saplings = 3, Pulse = 4;
        float shotT;
        static readonly Color Root = new Color(0.55f, 0.3f, 0.2f), Blood = new Color(0.85f, 0.1f, 0.2f);

        public Heartwood(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("THE HEARTWOOD", "THE FOREST'S OLD RED HEART", "heartwood", hp, 2.4f, new Vector2(1.4f, 1.6f), a, floor + Vector2.up * 1.61f);
            Go(Idle, 2f);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            vel.x = 0f;
            Move(dt);
            switch (state)
            {
                case Idle:
                    // it shuffles a little towards you on its roots
                    vel.x = Mathf.Sign(P.pos.x - pos.x) * 0.6f; Move(dt);
                    if (stateT <= 0f)
                    {
                        cycle++;
                        int k = cycle % 4;
                        if (k == 1) { Go(Roots, 2.2f); shotT = 0f; }
                        else if (k == 2) { Go(BloodRain, 2.5f); shotT = 0f; G.sfx.Play("rumble", 0.8f, 0.8f); }
                        else if (k == 3) { Go(Saplings, 0.8f); }
                        else { Go(Pulse, 1.6f); shotT = 0f; }
                    }
                    break;
                case Roots:  // roots burst up from the floor in a line towards you
                    shotT -= dt;
                    if (shotT <= 0f)
                    {
                        shotT = Phase2 ? 0.25f : 0.35f;
                        int i = Mathf.RoundToInt((2.2f - stateT) / shotT);
                        float x = pos.x + Mathf.Sign(P.pos.x - pos.x) * (2f + i * 1.6f);
                        if (x > arena.MinX && x < arena.MaxX)
                            G.Spawn(new Hazard(new Vector2(x, arena.FloorY + 1.2f), new Vector2(0.5f, 1.2f), 0.6f, 0.4f, Root, Dmg));
                    }
                    if (stateT <= 0f) Go(Idle, 1.4f);
                    break;
                case BloodRain:  // it weeps blood from the roof: drops that hurt, and pools that heal its creatures
                    shotT -= dt;
                    if (shotT <= 0f)
                    {
                        shotT = Phase2 ? 0.12f : 0.18f;
                        float x = Random.Range(arena.MinX + 0.6f, arena.MaxX - 0.6f);
                        var b = Shot(new Vector2(x, arena.CeilY - 0.4f), Vector2.zero, Art.Circle, Blood, 26f, 0.4f, 0.35f);
                        b.body = Blood;
                        if (Random.value < 0.25f) Fluids.Spill(new Vector2(x, arena.CeilY - 0.6f), Fluids.Blood, 8, Vector2.down * 4f);
                    }
                    if (stateT <= 0f) Go(Idle, 1.6f);
                    break;
                case Saplings:
                    if (stateT <= 0f)
                    {
                        int n = Phase2 ? 3 : 2;
                        for (int i = 0; i < n; i++)
                        {
                            float x = Random.Range(arena.MinX + 2f, arena.MaxX - 2f);
                            G.Spawn(new Weepling(Mathf.FloorToInt(x), Mathf.FloorToInt(arena.FloorY), arena.Depth));
                            G.fx.Burst(new Vector2(x, arena.FloorY + 0.4f), 10, Root, 4f, 0.4f, 10f);
                        }
                        G.sfx.Play("rumble", 0.5f, 1.3f);
                        Go(Idle, 1.8f);
                    }
                    break;
                case Pulse:  // its heart beats: rings of blood
                    shotT -= dt;
                    if (shotT <= 0f)
                    {
                        shotT = 0.5f;
                        int n = Phase2 ? 16 : 12;
                        float off = Random.value * 30f;
                        for (int i = 0; i < n; i++)
                        {
                            float a = (off + i * 360f / n) * Mathf.Deg2Rad;
                            var b = Shot(pos + Vector2.up * 0.4f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5f, Art.Circle, Blood, 0f, 0f, 0.4f);
                            b.body = Blood;
                        }
                        squash = 0.15f;
                        G.sfx.Play("hit", 0.7f, 0.5f);
                    }
                    if (stateT <= 0f) Go(Idle, 1.5f);
                    break;
            }
            DrawBoss(-half.y);
        }

        public override void AddLight(Lighting l) => l.Add(pos + Vector2.up * 0.4f, 4f + Mathf.Sin(t * 6f) * 0.5f, 0.7f);
    }

    // ================================================================== THE BLOODSTAG
    public class Bloodstag : BiomeBoss
    {
        const int Walk = 0, Paw = 1, Charge = 2, Rear = 3, Toss = 4;
        int bounces;
        public Bloodstag(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("THE BLOODSTAG", "ANTLERS LIKE A BRAMBLE", "bloodstag", hp, 2.2f, new Vector2(1.5f, 1.1f), a, floor + Vector2.up * 1.11f);
            Go(Walk, 1.5f);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            switch (state)
            {
                case Walk:
                    FacePlayer();
                    vel.x = U.Approach(vel.x, facing * 2.5f, 15f * dt);
                    Move(dt);
                    if (stateT <= 0f) { vel.x = 0f; cycle++; if (cycle % 3 == 0) Go(Toss, 0.9f); else Go(Paw, Phase2 ? 0.5f : 0.75f); }
                    break;
                case Paw:   // it paws the ground: a charge is coming
                    FacePlayer(); vel.x = 0f; Move(dt);
                    squash = 0.1f + Mathf.Sin(t * 30f) * 0.04f;
                    if (Random.value < 0.5f) G.fx.Burst(pos + new Vector2(-facing * half.x, -half.y + 0.1f), 2, new Color(0.5f, 0.3f, 0.2f), 4f, 0.3f, 20f);
                    if (stateT <= 0f) { bounces = Phase2 ? 2 : 1; Go(Charge, 5f); G.sfx.Play("dash", 1f, 0.6f); }
                    break;
                case Charge:
                    vel.x = facing * (Phase2 ? 15f : 12f);
                    if (Phase2 && Random.value < dt * 6f) Fluids.Spill(pos + Vector2.down * half.y, Fluids.Blood, 4, Vector2.zero);   // it bleeds as it runs
                    if (Move(dt) || stateT <= 0f)
                    {
                        G.cam.Shake(0.45f); G.sfx.Play("explode", 0.7f, 0.9f);
                        CeilingRain(Phase2 ? 5 : 3, Art.Circle, new Color(0.6f, 0.2f, 0.2f));
                        if (bounces-- > 0 && stateT > 0f) facing = -facing;
                        else { vel.x = 0f; Go(Rear, 0.6f); }
                    }
                    break;
                case Rear:  // up on its hind legs... and down: shockwaves both ways
                    squash = -0.15f; vel.x = 0f; Move(dt);
                    if (stateT <= 0f) { FloorWave(1, new Color(0.8f, 0.2f, 0.25f)); FloorWave(-1, new Color(0.8f, 0.2f, 0.25f)); G.cam.Shake(0.3f); Go(Walk, 1.6f); }
                    break;
                case Toss:  // it tosses its antlers: a spray of thorny shards
                    vel.x = 0f; Move(dt);
                    if (stateT <= 0f)
                    {
                        FacePlayer();
                        int n = Phase2 ? 9 : 7;
                        for (int i = 0; i < n; i++)
                        {
                            float a = Mathf.Lerp(30f, 150f, i / (float)(n - 1)) * Mathf.Deg2Rad;
                            var b = Shot(pos + Vector2.up * half.y, new Vector2(Mathf.Cos(a) * 7f, Mathf.Sin(a) * 9f), Art.Circle, new Color(0.9f, 0.25f, 0.3f), 14f, 0f, 0.4f);
                            b.body = new Color(0.9f, 0.25f, 0.3f);
                        }
                        G.sfx.Play("swing", 0.8f, 0.6f);
                        Go(Walk, 1.4f);
                    }
                    break;
            }
            animT += dt * (Mathf.Abs(vel.x) > 3f ? 2f : 0.6f);
            DrawBoss(-half.y);
        }
    }

    // ================================================================== THE KING CRAB
    public class KingCrab : BiomeBoss
    {
        const int Sidle = 0, Snap = 1, Bubbles = 2, Crouch = 3, Air = 4;
        float shotT;
        public KingCrab(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("THE KING CRAB", "LORD OF THE SUNKEN SHORE", "kingcrab", hp, 2.2f, new Vector2(1.9f, 0.8f), a, floor + Vector2.up * 0.81f);
            Go(Sidle, 1.5f);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            switch (state)
            {
                case Sidle:   // sideways after you, fast
                    vel.x = U.Approach(vel.x, Mathf.Sign(P.pos.x - pos.x) * (Phase2 ? 5f : 3.8f), 25f * dt);
                    Move(dt);
                    if (Mathf.Abs(P.pos.x - pos.x) < half.x + 1.2f && Mathf.Abs(P.pos.y - pos.y) < 2f) { Go(Snap, 0.45f); vel.x = 0f; flash = 0.1f; }
                    else if (stateT <= 0f) { cycle++; vel.x = 0f; if (cycle % 2 == 0) { Go(Bubbles, 2.4f); shotT = 0f; } else Go(Crouch, 0.5f); }
                    break;
                case Snap:   // the claws snap shut: get clear
                    vel.x = 0f; Move(dt);
                    if (stateT <= 0f)
                    {
                        var pl = P;
                        if (pl.mode != Player.Mode.Dead && U.Overlap(pos, new Vector2(half.x + 1.1f, half.y + 0.6f), pl.pos, Player.Half)) pl.Hurt(Mathf.RoundToInt(Dmg * 1.3f), pos);
                        G.sfx.Play("hammer", 0.8f, 0.7f); G.cam.Shake(0.2f);
                        G.fx.Burst(pos + new Vector2(0f, 0.6f), 10, new Color(1f, 0.6f, 0.3f), 5f, 0.3f, 5f);
                        Go(Sidle, 1.2f);
                    }
                    break;
                case Bubbles:   // a froth of slow bubbles drifting up and at you
                    vel.x = 0f; Move(dt);
                    shotT -= dt;
                    if (shotT <= 0f)
                    {
                        shotT = Phase2 ? 0.18f : 0.26f;
                        Vector2 d = U.Rot(ToPlayer, Random.Range(-35f, 35f));
                        var b = Shot(pos + Vector2.up * 0.6f, d * Random.Range(2.5f, 4.5f) + Vector2.up, Art.Circle, new Color(0.7f, 0.9f, 1f), -1f, 0f, 0.5f);
                        b.body = new Color(0.8f, 0.95f, 1f, 0.8f);
                    }
                    if (stateT <= 0f) Go(Sidle, 1.5f);
                    break;
                case Crouch:
                    squash = 0.2f; vel.x = 0f; Move(dt);
                    if (stateT <= 0f) { vel = new Vector2(Mathf.Clamp(P.pos.x - pos.x, -10f, 10f), 17f); Go(Air, 3f); G.sfx.Play("jump", 0.8f, 0.5f); }
                    break;
                case Air:
                    Move(dt);
                    if (grounded && vel.y <= 0f && stateT < 2.9f)
                    {
                        vel.x = 0f; squash = 0.3f;
                        G.cam.Shake(0.5f); G.sfx.Play("explode", 0.7f, 0.7f);
                        FloorWave(1, new Color(1f, 0.85f, 0.5f)); FloorWave(-1, new Color(1f, 0.85f, 0.5f));
                        CeilingRain(Phase2 ? 6 : 4, Art.TileSprite(Block.Boulder, Biomes.All[Biomes.Beach]), new Color(0.6f, 0.4f, 0.2f));   // coconuts!
                        Go(Sidle, 1.4f);
                    }
                    break;
            }
            animT += dt * (Mathf.Abs(vel.x) > 1f ? 2f : 0.4f);
            int f = facing; facing = 1;
            DrawBoss(-half.y);
            facing = f;
        }
    }

    // ================================================================== THE KRAKEN
    public class Kraken : BiomeBoss
    {
        const int Rise = 0, Tentacles = 1, Spit = 2, Ink = 3, Sink = 4;
        float shotT; bool flooded;
        static readonly Color Tent = new Color(0.75f, 0.3f, 0.7f), Water = new Color(0.5f, 0.8f, 1f);

        public Kraken(BossArena a, float hp)
        {
            SetupBoss("THE KRAKEN", "IT CAME UP WITH THE TIDE", "kraken", hp, 2.3f, new Vector2(1.6f, 1.05f), a, new Vector2(a.CenterX, a.FloorY + 2f));
            Go(Rise, 1.5f);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            if (!flooded && Phase2) { flooded = true; ArenaWater.Fill(arena); G.hud.Banner("THE TIDE COMES IN", "", new Color(0.5f, 0.8f, 1f)); }
            // it bobs about the middle of the hall
            Vector2 home = new Vector2(Mathf.Clamp(P.pos.x * 0.3f + arena.CenterX * 0.7f, arena.MinX + 3f, arena.MaxX - 3f), arena.FloorY + 2.8f + Mathf.Sin(t * 1.5f) * 0.5f);
            pos = Vector2.MoveTowards(pos, home, 2.5f * dt);
            switch (state)
            {
                case Rise:
                    if (stateT <= 0f) { cycle++; int k = cycle % 3; Go(k == 1 ? Tentacles : k == 2 ? Spit : Ink, k == 1 ? 2.4f : k == 2 ? 2f : 3f); shotT = 0f; }
                    break;
                case Tentacles:   // tentacles burst up out of the floor, marked first, one under you
                    shotT -= dt;
                    if (shotT <= 0f)
                    {
                        shotT = Phase2 ? 0.45f : 0.65f;
                        float x = Random.value < 0.5f ? P.pos.x : Random.Range(arena.MinX + 1f, arena.MaxX - 1f);
                        G.Spawn(new Hazard(new Vector2(x, arena.FloorY + 2f), new Vector2(0.6f, 2f), 0.75f, 0.5f, Tent, Dmg));
                    }
                    if (stateT <= 0f) Go(Rise, 1.2f);
                    break;
                case Spit:   // balls of sea water
                    shotT -= dt;
                    if (shotT <= 0f)
                    {
                        shotT = 0.4f;
                        for (int i = -1; i <= 1; i++) { var b = Shot(pos + Vector2.up * 0.6f, U.Rot(ToPlayer, i * 15f) * 7f, Art.Circle, Water, 3f, 0f, 0.5f); b.body = Water; }
                        G.sfx.Play("splat", 0.6f, 0.7f);
                    }
                    if (stateT <= 0f) Go(Rise, 1.2f);
                    break;
                case Ink:    // a cloud of ink: the hall goes dark, and spits come out of it
                    Lighting.Blackout = Mathf.MoveTowards(Lighting.Blackout, stateT > 0.5f ? 0.75f : 0f, dt * 2f);
                    shotT -= dt;
                    if (shotT <= 0f) { shotT = 0.6f; var b = Shot(pos, ToPlayer * 6f, Art.Circle, new Color(0.25f, 0.15f, 0.3f), 0f, 0f, 0.6f); b.body = new Color(0.2f, 0.1f, 0.25f); b.OverDark(); }
                    if (Random.value < 0.6f) G.fx.Smoke(pos + Random.insideUnitCircle * 2f, 1, 1.2f);
                    if (stateT <= 0f) { Lighting.Blackout = 0f; Go(Rise, 1.2f); }
                    break;
            }
            animT += dt * 0.5f;
            DrawBoss(0f);
        }

        public override void Die() { Lighting.Blackout = 0f; base.Die(); }
    }

    // ================================================================== LUMINA, THE NEON JAGUAR
    public class Lumina : BiomeBoss
    {
        const int Prowl = 0, Crouch = 1, Pounce = 2, Roar = 3, Vanish = 4;
        float trailT;
        static readonly Color Neon1 = new Color(0.3f, 1f, 0.85f), Neon2 = new Color(1f, 0.3f, 0.85f);

        public Lumina(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("LUMINA", "THE NEON JAGUAR", "neonjaguar", hp, 3f, new Vector2(1.6f, 0.75f), a, floor + Vector2.up * 0.76f);
            Go(Prowl, 1.4f);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            switch (state)
            {
                case Prowl:
                    FacePlayer();
                    vel.x = U.Approach(vel.x, facing * 3f, 15f * dt);
                    Move(dt);
                    if (stateT <= 0f) { cycle++; vel.x = 0f; int k = cycle % 4; if (k == 3) Go(Roar, 0.8f); else if (k == 2 && Phase2) Go(Vanish, 1.2f); else Go(Crouch, 0.45f); }
                    break;
                case Crouch:
                    FacePlayer(); squash = 0.25f; vel.x = 0f; Move(dt);
                    if (stateT <= 0f)
                    {
                        Vector2 d = P.pos - pos;
                        vel = new Vector2(Mathf.Clamp(d.x * 1.6f, -18f, 18f), 12f + Mathf.Max(0f, d.y) * 2f);
                        G.sfx.Play("dash", 0.9f, 0.7f);
                        Go(Pounce, 3f);
                    }
                    break;
                case Pounce:   // a leap trailing light that hangs in the air a moment
                    Move(dt);
                    trailT -= dt;
                    if (trailT <= 0f) { trailT = 0.06f; var b = Shot(pos, Vector2.zero, Art.Circle, t % 0.2f < 0.1f ? Neon1 : Neon2, 0f, 0.35f, 0.4f); b.body = Neon1; }
                    if (grounded && vel.y <= 0f && stateT < 2.8f) { vel.x = 0f; squash = 0.25f; G.cam.Shake(0.3f); Go(Prowl, Phase2 ? 0.8f : 1.2f); }
                    break;
                case Roar:     // a roar: rings of neon light
                    vel.x = 0f; Move(dt);
                    if (stateT <= 0f)
                    {
                        Ring(pos, Phase2 ? 18 : 14, 5f, Random.value * 30f, Art.Circle, Neon1);
                        Ring(pos, Phase2 ? 18 : 14, 3.5f, 10f, Art.Circle, Neon2);
                        G.sfx.Play("rumble", 0.9f, 1.5f); G.cam.Shake(0.3f);
                        Go(Prowl, 1.4f);
                    }
                    break;
                case Vanish:   // its light goes out... and it's behind you
                    intangible = stateT > 0.25f;
                    sr.enabled = stateT <= 0.25f || ((int)(stateT * 20f)) % 4 == 0;
                    if (stateT <= 0.3f && intangible)
                    {
                        float side = P.facing;
                        pos = new Vector2(Mathf.Clamp(P.pos.x - side * 3f, arena.MinX + 2f, arena.MaxX - 2f), arena.FloorY + half.y + 0.01f);
                    }
                    if (stateT <= 0f) { intangible = false; sr.enabled = true; Go(Crouch, 0.35f); }
                    break;
            }
            animT += dt * (Mathf.Abs(vel.x) > 2f ? 1.6f : 0.5f);
            DrawBoss(-half.y);
        }

        public override void AddLight(Lighting l) { if (!intangible) l.Add(pos, 5f, 0.7f); }
    }

    // ================================================================== THE ABYSSAL ANGLER
    public class AbyssAngler : BiomeBoss
    {
        const int Lurk = 0, Lunge = 1, Lure = 2, Swarm = 3;
        float floodT;
        Vector2 lungeV;
        static readonly Color LureC = new Color(1f, 0.95f, 0.5f);

        public AbyssAngler(BossArena a, float hp)
        {
            SetupBoss("THE ABYSSAL ANGLER", "A LIGHT IN THE DEEP", "abyssangler", hp, 2.2f, new Vector2(2f, 1.2f), a, new Vector2(a.CenterX, a.FloorY + 4f));
            Go(Lurk, 2f);
            ArenaWater.Fill(a);
        }

        Vector2 LurePos => pos + new Vector2(facing * 1.4f, 2.4f);

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            // the deep is dark: only its lure lights the hall
            Lighting.Blackout = Mathf.MoveTowards(Lighting.Blackout, 0.6f, dt);
            // the hall floods and drains in turn (come up for air!)
            floodT += dt;
            if (floodT > (Phase2 ? 10f : 12f)) { floodT = 0f; var aw = ArenaWater.For(arena); if (aw != null && aw.Full) aw.Drain(); else ArenaWater.Fill(arena); }
            switch (state)
            {
                case Lurk:
                    FacePlayer();
                    pos = Vector2.MoveTowards(pos, new Vector2(Mathf.Clamp(P.pos.x - facing * 5f, arena.MinX + 2.5f, arena.MaxX - 2.5f), Mathf.Clamp(P.pos.y + 1f, arena.FloorY + 1.5f, arena.CeilY - 1.5f)), 3f * dt);
                    if (stateT <= 0f) { cycle++; int k = cycle % 3; if (k == 1) { Go(Lunge, 0.6f); flash = 0.15f; } else if (k == 2) Go(Lure, 3f); else Go(Swarm, 0.5f); }
                    break;
                case Lunge:   // a pause, then it bites across the hall
                    if (stateT > 0.25f) { lungeV = ToPlayer * (Phase2 ? 18f : 14f); }
                    else
                    {
                        pos += lungeV * dt;
                        pos.x = Mathf.Clamp(pos.x, arena.MinX + 2f, arena.MaxX - 2f);
                        pos.y = Mathf.Clamp(pos.y, arena.FloorY + 1.2f, arena.CeilY - 1.2f);
                    }
                    if (stateT <= -0.45f) Go(Lurk, Phase2 ? 1f : 1.5f);
                    break;
                case Lure:    // its lure draws you in towards its jaws
                    {
                        var p = P;
                        Vector2 d = pos - p.pos;
                        if (p.mode != Player.Mode.Dead && d.magnitude > 1.5f) p.vel += d.normalized * 7f * dt;
                        if (Random.value < 0.5f) G.fx.Burst(LurePos, 1, LureC, 2f, 0.6f, 0f, 1f, false);
                        if (stateT <= 0f) Go(Lurk, 1.2f);
                        break;
                    }
                case Swarm:   // little anglers swim out of the dark
                    if (stateT <= 0f)
                    {
                        int n = Phase2 ? 3 : 2;
                        for (int i = 0; i < n; i++) G.Spawn(new Fish(Mathf.FloorToInt(pos.x) + i - 1, Mathf.FloorToInt(pos.y), arena.Depth, Fish.Kind.Angler));
                        Go(Lurk, 1.6f);
                    }
                    break;
            }
            animT += dt * 0.6f;
            DrawBoss(0f);
        }

        public override void AddLight(Lighting l)
        {
            l.Add(LurePos, 7f + Mathf.Sin(t * 4f) * 0.6f, 0.95f);
        }

        public override void Die() { Lighting.Blackout = 0f; base.Die(); }
    }
}
