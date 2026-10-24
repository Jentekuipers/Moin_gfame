using UnityEngine;

namespace DigDeep
{
    // =====================================================================================
    // THE BONE SLIME (sometimes instead of the Devourer, depth 100): a great slime with a crowned skull floating
    // in it. It hops and slams (shockwaves, falling bones), spits globs, and swells up to call slimes and skeletons.
    // =====================================================================================
    public class BoneSlime : Boss
    {
        enum S { Sit, Crouch, Air, Land, Swell, Spit }
        int hops, left;
        float shotT;
        static readonly Color Goo = new Color(0.25f, 0.8f, 0.68f);
        Sprite[] idle, idleW;

        public BoneSlime(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("THE BONE SLIME", "IT ATE A KING. IT'S STILL HUNGRY", "boneslime", hp, 1.25f, new Vector2(2.1f, 1f), a, floor + Vector2.up * 1.01f);
            blood = Goo;
            idle = frames; idleW = whites;
            Go(S.Sit, 1.5f);
        }

        void Go(S s, float time) { state = (int)s; stateT = time; }

        void Use(string key)
        {
            if (key == null || !Art.Strips.TryGetValue(key, out var f) || f == null) { frames = idle; whites = idleW; return; }
            frames = f;
            whites = Art.EnemyWhite.TryGetValue(key, out var w) && w != null && w.Length == f.Length ? w : f;
        }

        void Frame(int i) { animT = (Mathf.Clamp(i, 0, frames.Length - 1) + 0.5f) / 6f; }

        int Minions()
        {
            int n = 0;
            foreach (var e in G.ents) if (!e.dead && (e is Slime || e is Skeleton)) n++;
            return n;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            switch ((S)state)
            {
                case S.Sit:
                    Use(null);
                    FacePlayer();
                    vel.x = U.Approach(vel.x, 0f, 20f * dt);
                    Move(dt);
                    if (stateT <= 0f)
                    {
                        cycle++;
                        switch (cycle % 3)
                        {
                            case 1: hops = Phase2 ? 3 : 2; Go(S.Crouch, 0.45f); break;
                            case 2: Go(S.Swell, 1.2f); G.sfx.Play("rumble", 0.7f, 1.2f); break;
                            default: left = Phase2 ? 7 : 5; shotT = 0.3f; Go(S.Spit, 2.5f); break;
                        }
                    }
                    break;
                case S.Crouch:
                    Use("bslime_jump");
                    Frame((int)((0.45f - stateT) / 0.45f * 6f));
                    Move(dt);
                    if (stateT <= 0f)
                    {
                        FacePlayer();
                        vel = new Vector2(Mathf.Clamp(P.pos.x - pos.x, -10f, 10f) * 0.9f, 15f);
                        G.sfx.Play("jump", 0.9f, 0.4f);
                        Go(S.Air, 3f);
                    }
                    break;
                case S.Air:
                    Use("bslime_jump");
                    Frame(vel.y > 4f ? 7 : vel.y > -4f ? 8 : 10);
                    Move(dt);
                    if (grounded && vel.y <= 0f && stateT < 2.9f)
                    {
                        vel.x = 0f;
                        G.cam.Shake(0.45f);
                        G.sfx.Play("land", 1f, 0.4f);
                        FloorWave(1, Goo); FloorWave(-1, Goo);
                        if (Art.Bones != null) CeilingRain(Phase2 ? 5 : 3, Art.Bones[Random.Range(0, Art.Bones.Length)], new Color(0.95f, 0.95f, 0.85f));
                        Go(S.Land, 0.5f);
                    }
                    break;
                case S.Land:
                    Use("bslime_jump");
                    Frame(11 + (int)((0.5f - stateT) / 0.5f * 8f));
                    Move(dt);
                    if (stateT <= 0f)
                    {
                        if (--hops > 0) Go(S.Crouch, 0.3f);
                        else Go(S.Sit, Phase2 ? 1.1f : 1.6f);
                    }
                    break;
                case S.Swell:   // it swells and shakes... and slimes and skeletons climb out of it
                    Use("bslime_angry");
                    Frame((int)((1.2f - stateT) / 1.2f * 8f));
                    Move(dt);
                    if (stateT <= 0f)
                    {
                        int n = Mathf.Min(Phase2 ? 3 : 2, 6 - Minions());
                        for (int i = 0; i < n; i++)
                        {
                            int x = Mathf.FloorToInt(pos.x) + (i % 2 == 0 ? -3 - i : 3 + i);
                            x = Mathf.Clamp(x, Mathf.CeilToInt(arena.MinX) + 1, Mathf.FloorToInt(arena.MaxX) - 2);
                            int y = Mathf.FloorToInt(arena.FloorY);
                            if (i == 1) { var sk = new Skeleton(x, y, arena.Depth); sk.Wake(); G.Spawn(sk); }
                            else G.Spawn(new Slime(x, y, arena.Depth));
                            G.fx.Burst(new Vector2(x + 0.5f, y + 0.5f), 10, Goo, 4f, 0.4f, 10f);
                        }
                        G.sfx.Play("splat", 0.8f, 0.6f);
                        Go(S.Sit, 1.2f);
                    }
                    break;
                case S.Spit:   // globs of slime lobbed in a spread
                    Use(null);
                    Move(dt);
                    shotT -= dt;
                    if (shotT <= 0f && left > 0)
                    {
                        left--; shotT = 0.22f;
                        Vector2 from = pos + Vector2.up * half.y;
                        float dx = P.pos.x - from.x + Random.Range(-3f, 3f);
                        Shot(from, new Vector2(dx * 0.9f, 11f + Random.value * 3f), Art.Spit, Goo, 22f, 0f, 1f).poison = Phase2;
                        G.sfx.Play("splat", 0.4f, 1.2f);
                    }
                    if (left <= 0 && shotT <= 0f) Go(S.Sit, 1.4f);
                    break;
            }
            DrawBoss(-half.y);
        }
    }

    // =====================================================================================
    // THE VOID ARCHMAGE: the last thing at the bottom of the void world, where Ornin would sit. A towering robed
    // shape of white light. It casts every element, splits the floor with void spikes, blinks about the hall, and
    // calls void mages. Beat it and the void diamond is yours.
    // =====================================================================================
    public class VoidLord : Boss
    {
        enum S { Float, Fire, Ice, Bolts, Spikes, Blink, Call }
        int left;
        float shotT;
        Vector2 home;
        static readonly Color Fire = new Color(1f, 0.45f, 0.2f), Ice = new Color(0.6f, 0.9f, 1f), Void = new Color(0.65f, 0.35f, 1f);

        public VoidLord(BossArena a, float hp)
        {
            SetupBoss("THE VOID ARCHMAGE", "WHAT WAITS UNDER EVERYTHING", "vmage_elite", hp, 2f, new Vector2(1f, 2.4f), a,
                new Vector2(a.CenterX, a.FloorY + 4.6f));
            blood = Void;
            home = pos;
            Go(S.Float, 2f);
        }

        void Go(S s, float time) { state = (int)s; stateT = time; }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            // it floats over the hall, bobbing, never touching the floor
            Vector2 bob = home + new Vector2(Mathf.Sin(t * 0.7f) * 4f, Mathf.Sin(t * 1.6f) * 0.4f);
            pos = Vector2.Lerp(pos, bob, 1f - Mathf.Exp(-dt * 2f));
            FacePlayer();
            shotT -= dt;
            switch ((S)state)
            {
                case S.Float:
                    if (Random.value < 0.4f) G.fx.Burst(pos + new Vector2(Random.Range(-1f, 1f), -2.4f), 1, Void, 1f, 0.6f, -2f, 1f, false);
                    if (stateT <= 0f)
                    {
                        cycle++;
                        int k = cycle % (Phase2 ? 6 : 5);
                        switch (k)
                        {
                            case 1: left = Phase2 ? 5 : 3; shotT = 0.4f; Go(S.Fire, 4f); break;
                            case 2: left = Phase2 ? 3 : 2; shotT = 0.4f; Go(S.Ice, 3f); break;
                            case 3: left = Phase2 ? 5 : 3; shotT = 0.3f; Go(S.Bolts, 4f); break;
                            case 4: left = 0; shotT = 0.6f; Go(S.Spikes, 3.4f); G.sfx.Play("rumble", 0.9f, 0.5f); break;
                            case 5: Go(S.Call, 1f); break;
                            default: Go(S.Blink, 0.6f); break;
                        }
                    }
                    break;
                case S.Fire:   // fans of void-fire
                    if (shotT <= 0f && left > 0)
                    {
                        left--; shotT = 0.45f;
                        Vector2 d = ToPlayer;
                        for (int i = -2; i <= 2; i++) Shot(pos, U.Rot(d, i * 11f) * 8f, Art.Fireball, Fire, 0f, 0f, 0.9f).burns = true;
                        G.sfx.Play("flame", 0.6f, 0.6f);
                    }
                    if (left <= 0 && shotT <= 0f) Go(S.Float, Phase2 ? 1f : 1.5f);
                    break;
                case S.Ice:    // rings of ice shards, each turned a little
                    if (shotT <= 0f && left > 0)
                    {
                        left--; shotT = 0.6f;
                        int n = Phase2 ? 18 : 14;
                        for (int i = 0; i < n; i++)
                        {
                            float a = (t * 40f + i * 360f / n) * Mathf.Deg2Rad;
                            Shot(pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5f, Art.Circle, Ice, 0f, 0f, 0.4f).chills = true;
                        }
                        G.sfx.Play("shoot", 0.6f, 1.5f);
                    }
                    if (left <= 0 && shotT <= 0f) Go(S.Float, 1.4f);
                    break;
                case S.Bolts:  // lightning drops where you stand, one after another
                    if (shotT <= 0f && left > 0)
                    {
                        left--; shotT = Phase2 ? 0.4f : 0.55f;
                        G.Spawn(new BeamStrike(arena, P.pos + Vector2.right * P.vel.x * 0.2f, true, 0.7f));
                        G.sfx.Play("zap", 0.5f, 0.6f);
                    }
                    if (left <= 0 && shotT <= -0.8f) Go(S.Float, 1.3f);
                    break;
                case S.Spikes: // the floor splits into void spikes, rolling across the hall from one wall to the other
                    if (shotT <= 0f && left < 14)
                    {
                        shotT = 0.12f;
                        bool fromLeft = (cycle / 5) % 2 == 0;
                        float x = fromLeft ? arena.MinX + 1f + left * 2f : arena.MaxX - 1f - left * 2f;
                        G.Spawn(new VoidSpike(new Vector2(Mathf.Floor(x) + 0.5f, arena.FloorY), false, ContactDmg(arena.Depth), 0.5f));
                        left++;
                    }
                    if (stateT <= 0f) Go(S.Float, 1.2f);
                    break;
                case S.Blink:  // it folds into the dark and comes out somewhere else, throwing a ring of void
                    flash = ((int)(stateT * 20f)) % 2 == 0 ? 0.03f : 0f;
                    if (stateT <= 0f)
                    {
                        G.fx.Burst(pos, 30, Void, 7f, 0.5f, 0f);
                        home = new Vector2(Random.Range(arena.MinX + 3f, arena.MaxX - 3f), arena.FloorY + Random.Range(4f, 6f));
                        pos = home;
                        int n = Phase2 ? 20 : 14;
                        for (int i = 0; i < n; i++)
                        {
                            float a = (i * 360f / n) * Mathf.Deg2Rad;
                            Shot(pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4f, Art.Circle, Void, 0f, 0.2f, 0.45f).body = new Color(0.35f, 0.15f, 0.6f);
                        }
                        G.sfx.Play("dash", 0.8f, 0.5f);
                        Go(S.Float, 1.4f);
                    }
                    break;
                case S.Call:   // two void mages step out of the dark (only in the second half of the fight)
                    if (stateT <= 0f)
                    {
                        if (Phase2)
                        {
                            int mages = 0;
                            foreach (var e in G.ents) if (!e.dead && e is VoidMage) mages++;
                            for (int i = 0; i < 2 && mages + i < 3; i++)
                                G.Spawn(new VoidMage(Mathf.FloorToInt(i == 0 ? arena.MinX + 3f : arena.MaxX - 3f), Mathf.FloorToInt(arena.FloorY + 3f), arena.Depth,
                                    i == 0 ? VoidMage.Kind.Fire : VoidMage.Kind.Ice));
                            G.sfx.Play("rumble", 0.8f, 0.8f);
                        }
                        Go(S.Float, 1.2f);
                    }
                    break;
            }
            DrawBoss(0f);
        }

        public override void AddLight(Lighting l) => l.Add(pos, 7f, 1f);
    }
}
