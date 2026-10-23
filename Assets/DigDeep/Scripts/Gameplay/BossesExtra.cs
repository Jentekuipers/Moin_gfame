using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // =====================================================================================
    // THE DEVOURER (middle of the Fungal Hollows): a giant worm that swims through the arena's floor and walls.
    // Dust tracks you along the floor, then it bursts up in a leaping arc and dives back in.
    // Every third time it lunges straight out of a side wall instead (the wall cracks first).
    // =====================================================================================

    public class Devourer : Boss
    {
        enum S { Under, Breach, LungeWarn, Lunge }
        readonly List<WormSegment> segs = new List<WormSegment>();
        readonly List<Vector2> trail = new List<Vector2>();
        Vector2 dir = Vector2.up;
        float lungeY, lungeDir, prevVy;
        static readonly Color Dirt = new Color(0.6f, 0.45f, 0.3f);
        static readonly Color Acid = new Color(0.7f, 1f, 0.4f);

        public Devourer(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("THE DEVOURER", "IT SWIMS THROUGH STONE", "wormhead", hp, 2.4f, new Vector2(0.8f, 0.8f), a, new Vector2(floor.x, a.FloorY - 2f));
            blood = new Color(0.9f, 0.55f, 0.5f);
            for (int i = 0; i < 9; i++)
            {
                var s = new WormSegment(this, "wormseg", a.Depth, 1f - i * 0.05f);
                segs.Add(s);
                G.Spawn(s);
            }
            for (int i = 0; i < 80; i++) trail.Add(pos);
            Go(S.Under, 1.6f);
        }

        void Go(S s, float time) { state = (int)s; stateT = time; }

        bool InRock => Wd.Solid(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            switch ((S)state)
            {
                case S.Under:   // a trail of dust follows you along the floor... then stops
                    {
                        bool locked = stateT < 0.4f;
                        float tx = Mathf.Clamp(P.pos.x, arena.MinX + 1f, arena.MaxX - 1f);
                        if (!locked) pos.x = Mathf.MoveTowards(pos.x, tx, (Phase2 ? 10f : 7f) * dt);
                        pos.y = arena.FloorY - 2f;
                        dir = Vector2.right * Mathf.Sign(tx - pos.x + 0.01f);
                        if (Random.value < (locked ? 1f : 0.5f))
                            G.fx.Burst(new Vector2(pos.x + Random.Range(-0.8f, 0.8f), arena.FloorY + 0.1f), locked ? 3 : 1, Dirt, locked ? 7f : 3f, 0.35f, 25f);
                        if (locked) G.cam.Shake(0.06f);
                        if (stateT <= 0f)
                        {
                            cycle++;
                            if (cycle % 3 == 0) { StartLunge(); break; }
                            float side = Random.value < 0.5f ? -1f : 1f;
                            vel = new Vector2(side * Random.Range(3f, 6f), Phase2 ? 21f : 19f);
                            G.sfx.Play("explode", 0.8f, 0.7f);
                            G.cam.Shake(0.4f);
                            Ring(new Vector2(pos.x, arena.FloorY + 0.4f), Phase2 ? 10 : 7, 7f, 0f, Art.Fireball, Dirt);
                            Go(S.Breach, 5f);
                        }
                        break;
                    }
                case S.Breach:   // the leap
                    prevVy = vel.y;
                    vel.y -= 24f * dt;
                    if (pos.y > arena.CeilY - 1.2f && vel.y > 0f) vel.y = -vel.y * 0.3f;
                    if (pos.x < arena.MinX + 1f || pos.x > arena.MaxX - 1f) vel.x = -vel.x;
                    pos += vel * dt;
                    dir = vel.normalized;
                    if (prevVy > 0f && vel.y <= 0f)   // top of the arc: spits acid down at you
                    {
                        int n = Phase2 ? 5 : 3;
                        Vector2 d = ToPlayer;
                        for (int i = 0; i < n; i++) Shot(pos, U.Rot(d, (i - (n - 1) * 0.5f) * 14f) * 7f, Art.Spit, Acid);
                        G.sfx.Play("shoot", 0.7f, 0.5f);
                    }
                    if (vel.y < 0f && pos.y < arena.FloorY - 1.5f)
                    {
                        G.sfx.Play("explode", 0.6f, 0.9f);
                        G.fx.Burst(new Vector2(pos.x, arena.FloorY + 0.2f), 20, Dirt, 7f, 0.5f, 25f, 2f);
                        if (Phase2) { FloorWave(1, Dirt); FloorWave(-1, Dirt); }
                        Go(S.Under, Phase2 ? 1.1f : 1.6f);
                    }
                    break;
                case S.LungeWarn:   // the wall cracks where it will burst out
                    if (Random.value < 0.8f) G.fx.Burst(new Vector2(lungeDir > 0 ? arena.MinX + 0.1f : arena.MaxX - 0.1f, lungeY + Random.Range(-0.7f, 0.7f)), 2, Dirt, 5f, 0.35f, 20f);
                    G.cam.Shake(0.05f);
                    if (stateT <= 0f) { vel = new Vector2(lungeDir * (Phase2 ? 19f : 16f), 0f); G.sfx.Play("dash", 1f, 0.4f); Go(S.Lunge, 4f); }
                    break;
                case S.Lunge:
                    pos += vel * dt;
                    dir = vel.normalized;
                    if ((lungeDir > 0 && pos.x > arena.MaxX + 3f) || (lungeDir < 0 && pos.x < arena.MinX - 3f) || stateT <= 0f)
                    {
                        pos = new Vector2(Mathf.Clamp(pos.x, arena.MinX + 1f, arena.MaxX - 1f), arena.FloorY - 2f);
                        Go(S.Under, 1.2f);
                    }
                    break;
            }
            if ((trail[0] - pos).sqrMagnitude > 0.02f) trail.Insert(0, pos);
            while (trail.Count > 120) trail.RemoveAt(trail.Count - 1);
            PlaceSegments();
            DrawBoss(0f);
            if (dead) return;
            go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            sr.flipX = false;
            sr.flipY = dir.x < 0f;
            sr.enabled = !InRock;
        }

        void StartLunge()
        {
            lungeDir = P.pos.x < arena.CenterX ? -1f : 1f;   // comes from the far side, straight at you
            lungeY = Mathf.Clamp(P.pos.y, arena.FloorY + 0.8f, arena.CeilY - 1.5f);
            pos = new Vector2(lungeDir > 0 ? arena.MinX - 3f : arena.MaxX + 3f, lungeY);
            trail.Clear();
            for (int i = 0; i < 80; i++) trail.Add(pos);
            G.sfx.Play("rumble", 1f, 1.2f);
            Go(S.LungeWarn, Phase2 ? 0.7f : 0.95f);
        }

        void PlaceSegments()
        {
            const float spacing = 1.05f;
            float acc = 0f;
            int ti = 0;
            for (int s = 0; s < segs.Count; s++)
            {
                float want = (s + 1) * spacing;
                while (ti < trail.Count - 1 && acc + (trail[ti + 1] - trail[ti]).magnitude < want)
                {
                    acc += (trail[ti + 1] - trail[ti]).magnitude;
                    ti++;
                }
                Vector2 prev = s == 0 ? pos : segs[s - 1].pos;
                segs[s].Follow(trail[Mathf.Min(ti, trail.Count - 1)], prev);
            }
        }

        public override void Die()
        {
            if (dead) return;
            foreach (var s in segs) s.Burst();
            base.Die();
        }

        public override void Kill()
        {
            base.Kill();
            foreach (var s in segs) s.Kill();
        }
    }

    // =====================================================================================
    // CAPTAIN VASK (middle of the Crystal Depths): the raiders' leader. A laser sight shows where his minigun
    // will sweep; glowing circles show where grenades will land; he rolls across the hall and calls for help.
    // =====================================================================================

    public class RaiderCaptain : Boss
    {
        enum S { Walk, SweepWarn, Sweep, Grenades, Roll, Shotgun }
        Vector2 aim = Vector2.right;
        float fireT, gunKick;
        readonly Transform gun, laser;
        readonly SpriteRenderer gunSr, laserSr;
        static readonly Color ShotCol = new Color(1f, 0.55f, 0.25f);

        public RaiderCaptain(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("CAPTAIN VASK", "THE RAIDERS' LAST CAPTAIN", "captain", hp, 1.9f, new Vector2(0.55f, 0.8f), a, floor + Vector2.up * 0.81f);
            blood = new Color(0.8f, 0.1f, 0.12f);
            var g = new GameObject("CaptainGun");
            g.transform.SetParent(G.runRoot, false);
            gunSr = Art.AddSR(g, Art.Icons["minigun"], 13);
            gun = g.transform;
            gun.localScale = Vector3.one * 1.5f;
            var lg = new GameObject("CaptainSight");
            lg.transform.SetParent(G.runRoot, false);
            laserSr = Art.AddSR(lg, Art.Pixel, 19);
            laserSr.enabled = false;
            laser = lg.transform;
            Go(S.Walk, 1.5f);
        }

        void Go(S s, float time) { state = (int)s; stateT = time; }

        Vector2 Muzzle => pos + new Vector2(0f, 0.15f) + aim * 1.3f;

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            intangible = false;
            gunKick = Mathf.MoveTowards(gunKick, 0f, dt * 2f);
            Vector2 toP = (P.pos - Muzzle).normalized;
            switch ((S)state)
            {
                case S.Walk:
                    {
                        FacePlayer();
                        aim = Vector2.Lerp(aim, toP, dt * 6f).normalized;
                        float dist = Mathf.Abs(P.pos.x - pos.x);
                        float want = dist > 9f ? facing : dist < 5f ? -facing : 0f;
                        vel.x = U.Approach(vel.x, want * (Phase2 ? 3.4f : 2.6f), 15f * dt);
                        Move(dt);
                        if (Phase2 && dist < 3.5f && Mathf.Abs(P.pos.y - pos.y) < 2f) { Go(S.Shotgun, 0.45f); break; }
                        if (stateT <= 0f)
                        {
                            cycle++;
                            vel.x = 0f;
                            switch (cycle % 4)
                            {
                                case 1: Go(S.SweepWarn, Phase2 ? 0.6f : 0.85f); G.sfx.Play("blip", 0.6f, 1.2f, 0f); break;
                                case 2: Go(S.Grenades, 1.2f); Grenades(); break;
                                case 3: Go(S.Roll, 0.55f); vel.x = (pos.x < arena.CenterX ? 1f : -1f) * 17f; G.sfx.Play("dashq", 0.9f, 0.7f); break;
                                default: Reinforce(); Go(S.Walk, 1.2f); break;
                            }
                        }
                        break;
                    }
                case S.SweepWarn:   // the red laser shows exactly where the stream will start
                    aim = Vector2.Lerp(aim, toP, dt * 8f).normalized;
                    Move(dt);
                    if (stateT <= 0f) { Go(S.Sweep, Phase2 ? 2f : 1.6f); fireT = 0f; }
                    break;
                case S.Sweep:       // a stream of bullets that turns slowly towards you: keep moving
                    {
                        float turn = (Phase2 ? 85f : 60f) * Mathf.Deg2Rad * dt;
                        aim = Vector3.RotateTowards(aim, toP, turn, 0f);
                        Move(dt);
                        fireT -= dt;
                        if (fireT <= 0f)
                        {
                            fireT = 0.07f;
                            var v = U.Rot(aim, Random.Range(-4f, 4f)) * 15f;
                            G.Spawn(new Bullet(Muzzle, v, Team.Boss, 3f, Enemy.ContactDmg(arena.Depth) * 6 / 10, "b_enemy", ShotCol));
                            G.fx.Muzzle(Muzzle, 0.8f, ShotCol);
                            G.sfx.Play("smg", 0.35f, 0.8f, 0.1f);
                            gunKick = 0.15f;
                        }
                        if (stateT <= 0f) Go(S.Walk, Phase2 ? 1.1f : 1.6f);
                        break;
                    }
                case S.Grenades:
                    aim = Vector2.Lerp(aim, new Vector2(facing, 1f).normalized, dt * 6f).normalized;
                    Move(dt);
                    if (stateT <= 0f) Go(S.Walk, 1.4f);
                    break;
                case S.Roll:        // rolls across the hall; can't be hit mid-roll
                    intangible = true;
                    squash = 0.25f;
                    Move(dt);
                    if (Random.value < 0.6f) G.fx.Burst(pos + Vector2.down * half.y, 2, new Color(0.6f, 0.6f, 0.65f), 3f, 0.3f, 10f);
                    if (stateT <= 0f) { vel.x = 0f; Go(S.Walk, 0.8f); }
                    break;
                case S.Shotgun:     // too close: he flashes, then blasts a ring of pellets
                    flash = ((int)(stateT * 16f)) % 2 == 0 ? 0.05f : 0f;
                    vel.x = 0f;
                    Move(dt);
                    if (stateT <= 0f)
                    {
                        for (int i = 0; i < 12; i++)
                        {
                            float a = i * 30f * Mathf.Deg2Rad;
                            G.Spawn(new Bullet(pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 9f, Team.Boss, 3f, Enemy.ContactDmg(arena.Depth), "b_enemy", ShotCol, 1f, 1.2f));
                        }
                        G.sfx.Play("shotgun", 0.8f, 0.8f);
                        G.cam.Shake(0.3f);
                        Go(S.Roll, 0.5f);
                        vel.x = (P.pos.x < pos.x ? 1f : -1f) * 15f;
                    }
                    break;
            }
            DrawBoss(-half.y);
            RenderGun();
        }

        void Grenades()
        {
            G.sfx.Play("launcher", 0.9f, 0.8f);
            int n = Phase2 ? 6 : 4;
            int dmgP = Enemy.ContactDmg(arena.Depth);
            for (int i = 0; i < n; i++)
            {
                float x = i == 0 ? P.pos.x : Random.Range(arena.MinX + 1f, arena.MaxX - 1f);
                G.Spawn(new Hazard(new Vector2(x, arena.FloorY + 1f), new Vector2(1.1f, 1f), 1f + i * 0.2f, 0.2f, new Color(1f, 0.5f, 0.2f), dmgP, true));
            }
        }

        void Reinforce()
        {
            int alive = 0;
            foreach (var e in G.ents) if (!e.dead && e is Raider && Mathf.Abs(e.pos.y - pos.y) < 12f) alive++;
            G.sfx.Play("blip", 0.8f, 0.6f, 0f);
            G.hud.Banner("REINFORCEMENTS!", "", new Color(1f, 0.55f, 0.3f));
            int fy = Mathf.FloorToInt(arena.FloorY);
            for (int i = 0; i < Mathf.Min(2, 3 - alive); i++)
            {
                int x = Mathf.FloorToInt(i == 0 ? arena.MinX + 2f : arena.MaxX - 2f);
                var k = Phase2 ? Raider.Kind.Brute : Raider.Kind.Gunner;
                G.Spawn(new Raider(x, fy, arena.Depth, k));
                G.fx.Burst(new Vector2(x + 0.5f, fy + 1f), 14, new Color(0.7f, 0.7f, 0.75f), 5f, 0.4f, 10f);
            }
        }

        void RenderGun()
        {
            if (dead) return;
            Vector2 tp = pos + new Vector2(0f, 0.15f) + aim * (0.7f - gunKick);
            gun.position = new Vector3(U.Snap(tp.x), U.Snap(tp.y), 0);
            gun.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            gunSr.flipY = aim.x < 0f;
            bool sight = (S)state == S.SweepWarn || (S)state == S.Sweep;
            laserSr.enabled = sight;
            if (!sight) return;
            Vector2 o = Muzzle;
            float len = Wd.RayDist(o, aim, 30f);
            laser.position = (Vector3)(o + aim * len * 0.5f);
            laser.rotation = gun.rotation;
            laser.localScale = new Vector3(len * 16f, (S)state == S.SweepWarn ? 2f : 1f, 1f);
            laserSr.color = (S)state == S.SweepWarn && ((int)(t * 20f)) % 2 == 0 ? Color.white : new Color(1f, 0.15f, 0.15f, 0.8f);
        }

        public override void Kill()
        {
            base.Kill();
            if (gun != null) Object.Destroy(gun.gameObject);
            if (laser != null) Object.Destroy(laser.gameObject);
        }
    }

    // =====================================================================================
    // THE ASH WRAITH (middle of the Magma Core): a burning ghost. Where it will reappear glows first; it sends
    // slow homing skulls (shoot them down) and walks pillars of fire across the floor towards you.
    // =====================================================================================

    public class AshWraith : Boss
    {
        enum S { Float, TeleWarn, Skulls, Pillars }
        Vector2 dest;
        static readonly Color Ash = new Color(1f, 0.45f, 0.25f);

        public AshWraith(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("THE ASH WRAITH", "WHAT THE FIRE LEFT BEHIND", "wraith", hp, 2.6f, new Vector2(0.8f, 0.9f), a, new Vector2(a.CenterX, a.FloorY + 6f));
            fireLike = true;
            blood = Ash;
            Go(S.Float, 1.5f);
        }

        void Go(S s, float time) { state = (int)s; stateT = time; }

        Vector2 Home => new Vector2(arena.CenterX + Mathf.Sin(t * 0.6f) * 8f, arena.FloorY + 6f + Mathf.Sin(t * 1.3f) * 1.2f);

        void Drift(float dt, Vector2 target, float speed)
        {
            Vector2 want = target - pos;
            want = want.sqrMagnitude > 0.01f ? want.normalized * Mathf.Min(speed, want.magnitude * 3f) : Vector2.zero;
            vel = Vector2.MoveTowards(vel, want, 20f * dt);
            Move(dt, false);
            if (Mathf.Abs(vel.x) > 0.3f) facing = vel.x > 0 ? 1 : -1;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            tint = new Color(0.85f, 0.75f, 0.75f, 1f);
            if (Random.value < 0.4f) G.fx.Burst(pos + Random.insideUnitCircle * 0.8f, 1, Ash, 1f, 0.6f, -4f, 1f, false);
            switch ((S)state)
            {
                case S.Float:
                    Drift(dt, Home, 4f);
                    if (stateT <= 0f)
                    {
                        cycle++;
                        switch (cycle % 3)
                        {
                            case 1:
                                // where it will reappear: near you, but not on top of you
                                float side = Random.value < 0.5f ? -1f : 1f;
                                dest = new Vector2(Mathf.Clamp(P.pos.x + side * Random.Range(4f, 7f), arena.MinX + 1.5f, arena.MaxX - 1.5f),
                                                   Mathf.Clamp(P.pos.y + Random.Range(1f, 3f), arena.FloorY + 1.5f, arena.CeilY - 1.5f));
                                Go(S.TeleWarn, Phase2 ? 0.55f : 0.75f);
                                G.sfx.Play("charge", 0.7f, 0.6f);
                                break;
                            case 2: Go(S.Skulls, 1.4f); Skulls(); break;
                            default: Go(S.Pillars, 1.6f); Pillars(); break;
                        }
                    }
                    break;
                case S.TeleWarn:   // a burning ring marks the spot
                    vel *= 0.9f;
                    Move(dt, false);
                    tint = new Color(0.85f, 0.75f, 0.75f, Mathf.Clamp01(stateT * 2f));
                    for (int i = 0; i < 3; i++)
                    {
                        Vector2 o = Random.insideUnitCircle.normalized * 1.3f;
                        G.fx.Burst(dest + o, 1, Ash, 0.5f, 0.3f, 0f, 1.5f, false);
                    }
                    if (stateT <= 0f)
                    {
                        G.fx.Burst(pos, 20, new Color(0.3f, 0.3f, 0.3f), 5f, 0.6f, -2f, 2f, false);
                        pos = dest;
                        vel = Vector2.zero;
                        G.sfx.Play("explode", 0.6f, 1.4f);
                        G.fx.Ring(pos, 2f, Ash);
                        Ring(pos, Phase2 ? 12 : 8, 5f, t * 40f, Art.Fireball, Ash);
                        Go(S.Float, Phase2 ? 0.9f : 1.4f);
                    }
                    break;
                case S.Skulls:
                case S.Pillars:
                    Drift(dt, Home, 2f);
                    if (stateT <= 0f) Go(S.Float, Phase2 ? 1f : 1.5f);
                    break;
            }
            if (Phase2 && Random.value < dt * 0.6f) CeilingRain(1, Art.Fireball, Ash);
            DrawBoss(0f);
        }

        void Skulls()
        {
            int n = Phase2 ? 5 : 3;
            for (int i = 0; i < n; i++)
            {
                Vector2 v = U.Rot(Vector2.up, (i - (n - 1) * 0.5f) * 35f) * 4f;
                G.Spawn(new HomingSkull(pos + v.normalized * 1.2f, v, arena.Depth));
            }
            G.sfx.Play("bat", 0.9f, 0.4f);
        }

        // fire pillars marching along the floor from the wraith towards you
        void Pillars()
        {
            G.sfx.Play("rumble", 0.9f, 1.1f);
            float dirX = P.pos.x > pos.x ? 1f : -1f;
            int dmgP = Enemy.ContactDmg(arena.Depth);
            int n = Phase2 ? 8 : 6;
            for (int i = 0; i < n; i++)
            {
                float x = pos.x + dirX * (1.5f + i * 2.2f);
                if (x < arena.MinX + 0.5f || x > arena.MaxX - 0.5f) break;
                G.Spawn(new Hazard(new Vector2(x, arena.FloorY + 2f), new Vector2(0.55f, 2f), 0.55f + i * 0.16f, 0.35f, Ash, dmgP));
            }
        }

        public override void AddLight(Lighting l) => l.Add(pos, 4f, 0.8f);
    }

    // A slow burning skull that homes in on you. It can be shot down.
    public class HomingSkull : Enemy
    {
        float life = 5f;

        public override bool Markable => false;

        public HomingSkull(Vector2 p, Vector2 v, int depth)
        {
            pos = p; vel = v;
            half = new Vector2(0.25f, 0.25f);
            Setup("Skull", "skull", 1, depth);
            hp = maxHp = 2f;
            team = Team.Boss;
            gold = 0;
            blood = new Color(1f, 0.5f, 0.25f);
            fireLike = true;
        }

        public override void Tick(float dt)
        {
            animT += dt; flash -= dt; hurtCd -= dt;
            life -= dt;
            var pl = G.player;
            Vector2 want = (pl.pos - pos).normalized * 4.5f;
            vel = Vector3.RotateTowards(vel, want, 110f * Mathf.Deg2Rad * dt, 6f * dt);
            pos += vel * dt;
            if (Random.value < 0.5f) G.fx.Burst(pos, 1, blood, 0.8f, 0.3f, -3f, 1f, false);
            if (life <= 0f || Wd.Solid(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y))) { Pop(); return; }
            if (pl.mode != Player.Mode.Dead && U.Overlap(pos, half, pl.pos, Player.Half))
            {
                pl.Hurt(ContactDmg(depth), pos);
                Pop();
                return;
            }
            facing = vel.x >= 0f ? 1 : -1;
            Draw(0f);
        }

        void Pop()
        {
            G.fx.Burst(pos, 10, blood, 4f, 0.4f, 5f);
            G.sfx.Play("break", 0.4f, 1.6f);
            Kill();
        }

        // shot down: no gold, no kill credit
        public override void Die() => Pop();

        public override void AddLight(Lighting l) => l.Add(pos, 1.6f, 0.7f);
    }
}
