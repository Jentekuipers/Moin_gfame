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

        // first it wipes the hall away: a pulsing wave out of the middle, until there's nothing but black
        float introT = 1f, waveR, waveMax;
        bool evaporating;
        float evapT;
        Creator creator;

        float SMinX => VoidStage.Active ? VoidStage.Min.x + 1f : arena.MinX;
        float SMaxX => VoidStage.Active ? VoidStage.Max.x - 1f : arena.MaxX;
        float SFloor => VoidStage.Active ? VoidStage.Min.y : arena.FloorY;

        public VoidLord(BossArena a, float hp)
        {
            SetupBoss("THE VOID ARCHMAGE", "WHAT WAITS UNDER EVERYTHING", "vmage_elite", hp, 2f, new Vector2(1f, 2.4f), a,
                new Vector2(a.CenterX, a.FloorY + ArenaRoom.IH * 0.5f));
            blood = Void;
            home = pos;
            intangible = true;
            VoidStage.CamLock = true; VoidStage.Center = pos;   // the view holds on the hall from here on
            waveMax = Mathf.Sqrt(G.cam.halfW * G.cam.halfW + G.cam.halfH * G.cam.halfH) + 1.5f;
            Go(S.Float, 2f);
        }

        float waitT = 2.2f;   // it gathers itself before the wave
        GameObject waveGo;

        void Intro(float dt)
        {
            float hw = G.cam.halfW, hh = G.cam.halfH;
            Vector2 c = VoidStage.Center;
            if (waitT > 0f)
            {
                // the dark pulls in towards it... then
                waitT -= dt;
                for (int n = 0; n < 3; n++)
                {
                    Vector2 o = Random.insideUnitCircle.normalized * Random.Range(3f, 9f);
                    G.fx.Burst(c + o * (waitT / 2.2f), 1, Void, 0.6f, 0.5f, 0f, 1.2f, false);
                }
                G.cam.Shake(0.05f + (2.2f - waitT) * 0.05f);
                if (waitT <= 0f)
                {
                    G.sfx.Play("explode", 1f, 0.4f);
                    G.sfx.Play("rumble", 1f, 0.35f);
                    G.cam.Shake(0.8f);
                    flash = 0.1f;
                    waveGo = new GameObject("VoidWave");
                    waveGo.transform.SetParent(G.runRoot, false);
                    var wr = Art.AddSR(waveGo, Art.Ring, 27);
                    wr.color = new Color(0.75f, 0.45f, 1f, 0.9f);
                }
                return;
            }
            // one wave of destruction, rolling out from the middle
            float prev = waveR;
            waveR += dt * waveMax / 1.6f;
            if (waveGo != null)
            {
                waveGo.transform.position = new Vector3(c.x, c.y, 0f);
                waveGo.transform.localScale = Vector3.one * waveR * 2f;
            }
            int x0 = Mathf.FloorToInt(c.x - hw) - 5, x1 = Mathf.CeilToInt(c.x + hw) + 5;
            int y0 = Mathf.FloorToInt(c.y - hh) - 5, y1 = Mathf.CeilToInt(c.y + hh) + 5;
            float reach = waveR >= waveMax ? 999f : waveR;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = (new Vector2(x + 0.5f, y + 0.5f) - c).magnitude;
                    if (d > reach || d <= prev - 0.01f) continue;
                    var cc = new Vector2(x + 0.5f, y + 0.5f);
                    Block b = Wd.Get(x, y);
                    if (b != Block.Air)
                    {
                        // each block pops up and away
                        if (Mathf.Abs(cc.x - c.x) < hw + 1f && Mathf.Abs(cc.y - c.y) < hh + 1f) G.Spawn(new Popper(cc, Art.TileSprite(b, Biomes.At(x, y)), 1f));
                        Wd.Set(x, y, Block.Air);
                    }
                    if (Wd.LiquidType(x, y) != Fluids.None)
                    {
                        // liquids boil off on the spot: a puff of steam that rises off the top of the screen
                        Wd.SetLiquid(x, y, Fluids.None, 0f);
                        G.fx.Burst(cc, 2, new Color(0.85f, 0.85f, 0.9f, 0.6f), 1.5f, 1.4f, -6f, 1.5f, false);
                    }
                }
            // everything it reaches pops: a jump to 1.5x, then gone
            foreach (var e in G.ents)
            {
                if (e.dead || e == this || e is BossArena || e is Player || e is Popper) continue;
                if ((e.pos - c).magnitude > reach) continue;
                if (e.sr != null && e.go != null) G.Spawn(new Popper(e.pos, e.sr.sprite, e.go.transform.localScale.x, e.sr.flipX, e.go.transform.rotation));
                e.Kill();
            }
            if (waveR >= waveMax)
            {
                VoidStage.Begin(c, hw, hh);
                intangible = false;
                introT = 0f;
                home = c + Vector2.up * 2f;
                if (waveGo != null) Object.Destroy(waveGo);
            }
        }

        public override void Die()
        {
            if (dead) return;
            if (!evaporating)
            {
                // it isn't over: it howls and boils away... and something reaches in for it
                evaporating = true;
                hp = 1f; intangible = true; evapT = 0f;
                G.boss = null;
                G.sfx.Play("death", 1f, 0.35f);
                G.sfx.Play("rumble", 1f, 0.4f);
                foreach (var e in G.ents) if (!e.dead && (e is BossShot || e is VoidMage || e is Hazard)) e.Kill();
                return;
            }
        }

        void Evaporate(float dt)
        {
            evapT += dt;
            float k = Mathf.Clamp01(evapT / 2.8f);
            G.cam.Shake(0.15f * (1f - k));
            if (Random.value < 0.9f) G.fx.Burst(pos + new Vector2(Random.Range(-1f, 1f), Random.Range(-2f, 2f)) * (1f - k * 0.6f), 2, Void, 2f, 0.8f, -6f, 1.5f, false);
            if (((int)(evapT * 6f)) % 3 == 0 && Random.value < 0.1f) G.sfx.Play("death", 0.4f, 0.3f + k * 0.3f);   // the howl breaks up
            go.transform.localScale = new Vector3(scale * (1f - k * 0.7f), scale * (1f + k * 0.4f), 1f);
            sr.color = new Color(tint.r, tint.g, tint.b, 1f - k * 0.85f);
            if (creator == null && evapT > 0.9f)
            {
                creator = new Creator(arena, maxHp * 1.25f, pos, this);
                G.Spawn(creator);
                arena.AddBoss(creator);
            }
        }

        // the Creator's hands clapped shut on it
        public void Vanish()
        {
            G.fx.Burst(pos, 50, Void, 8f, 0.8f, 0f, 2f);
            Kill();
        }

        void Go(S s, float time) { state = (int)s; stateT = time; }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            if (evaporating) { Evaporate(dt); return; }
            if (introT > 0f) { t += dt; Intro(dt); DrawBoss(0f); return; }
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
                        float bx = P.pos.x + P.vel.x * 0.2f;
                        G.Spawn(new Hazard(new Vector2(bx, VoidStage.Center.y), new Vector2(0.6f, G.cam.halfH + 1f), 0.7f, 0.3f, Void, ContactDmg(arena.Depth)));
                        G.sfx.Play("zap", 0.5f, 0.6f);
                    }
                    if (left <= 0 && shotT <= -0.8f) Go(S.Float, 1.3f);
                    break;
                case S.Spikes: // the floor splits into void spikes, rolling across the hall from one wall to the other
                    if (shotT <= 0f && left < 14)
                    {
                        shotT = 0.12f;
                        bool fromLeft = (cycle / 5) % 2 == 0;
                        float x = fromLeft ? SMinX + 1f + left * 2f : SMaxX - 1f - left * 2f;
                        G.Spawn(new VoidSpike(new Vector2(Mathf.Floor(x) + 0.5f, SFloor), false, ContactDmg(arena.Depth), 0.5f));
                        left++;
                    }
                    if (stateT <= 0f) Go(S.Float, 1.2f);
                    break;
                case S.Blink:  // it folds into the dark and comes out somewhere else, throwing a ring of void
                    flash = ((int)(stateT * 20f)) % 2 == 0 ? 0.03f : 0f;
                    if (stateT <= 0f)
                    {
                        G.fx.Burst(pos, 30, Void, 7f, 0.5f, 0f);
                        home = new Vector2(Random.Range(SMinX + 3f, SMaxX - 3f), SFloor + Random.Range(4f, 6f));
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
                                G.Spawn(new VoidMage(Mathf.FloorToInt(i == 0 ? SMinX + 3f : SMaxX - 3f), Mathf.FloorToInt(SFloor + 3f), arena.Depth,
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
