using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // =====================================================================================
    // THE DRAGON WORMS. A fire dragon (storm caverns) and a water dragon (crystal depths): long serpents that fly
    // through the arena and slide through its walls. Deeper in harder runs you can meet both at once (see DragonDuo).
    //
    // FIRE   INFERNO     rises to the ceiling, slams down: the whole floor bursts into flame - get up on the ledges
    //        VOLLEY      lobs arcs of fireballs
    //        BREATH      from a wall, a sweeping cone of fire
    //        DASHES      flies through the arena twice, low then high, boiling away any water into steam
    // WATER  FLOOD       dives into the floor and the arena fills with water; it swims through twice, slowly,
    //                    low (right to left) then high (left to right), then the water drains away
    //        GEYSERS     the floor bursts upward under you
    //        ICE RING    a ring of ice shards
    //        BUBBLES     slow, drifting bubbles aimed at you
    // =====================================================================================
    public class Dragon : Boss
    {
        public enum Elem { Fire, Water }
        public enum Atk { None, Inferno, Volley, Breath, Dashes, Flood, Geysers, IceRing, Bubbles }
        enum S { Roam, Hide, Rise, Slam, Hover, Breath, WarnDash, Dash, Dive, Flooded }

        public readonly Elem elem;
        public DragonDuo duo;
        readonly List<WormSegment> segs = new List<WormSegment>();
        readonly List<Vector2> trail = new List<Vector2>();
        Vector2 dir = Vector2.up, goal;
        Atk atk;
        int zone;                   // 0 the whole arena, -1 only the left half, 1 only the right half (duo attacks)
        int dashN, volleyN;
        float dashDir, dashY, fireT, roamT = 2.5f, shotT;
        bool keepWater;              // the duo's flood stays for the fire dragon to boil away
        int solo;
        static readonly Color FireC = new Color(1f, 0.55f, 0.2f), WaterC = new Color(0.55f, 0.85f, 1f);

        Color Col => elem == Elem.Fire ? FireC : WaterC;
        public bool Idle => (S)state == S.Roam || (S)state == S.Hide;
        public bool Hidden => (S)state == S.Hide;

        public Dragon(BossArena a, Elem e, float hp, bool partOfDuo)
        {
            elem = e;
            string key = e == Elem.Fire ? "firedragon" : "waterdragon";
            SetupBoss(e == Elem.Fire ? "IGNIS" : "UNDA", e == Elem.Fire ? "THE FIRE DRAGON" : "THE WATER DRAGON", key, hp, 2.3f, new Vector2(0.75f, 0.6f), a,
                new Vector2(e == Elem.Fire ? a.MinX - 3f : a.MaxX + 3f, a.FloorY + 3f));
            blood = e == Elem.Fire ? new Color(1f, 0.5f, 0.2f) : new Color(0.4f, 0.75f, 1f);
            for (int i = 0; i < 10; i++)
            {
                var s = new WormSegment(this, key + "seg", a.Depth, 1f - i * 0.045f);
                segs.Add(s);
                G.Spawn(s);
            }
            for (int i = 0; i < 90; i++) trail.Add(pos);
            goal = new Vector2(a.CenterX, a.FloorY + 5f);
            state = (int)S.Roam;
            roamT = partOfDuo ? 99f : 2.2f;
        }

        bool InRock => Wd.Solid(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
        public bool enraged;                    // its partner died: redder, faster, angrier
        float Rage => enraged ? 1.35f : 1f;
        public bool mixHeights;                 // the fire dragon's dashes after the water dragon's: other heights, to mix you up
        float dashY2;
        float Spd => (Phase2 ? 8.5f : 7f) * Rage;
        public override Def Drop => DB.ById.TryGetValue(elem == Elem.Fire ? "heart_ignis" : "heart_unda", out var d) ? d : null;
        float H1 => arena.FloorY + 2.6f;                                          // the ledges' height
        float H2 => arena.FloorY + ArenaRoom.IH * 0.66f;                           // two thirds up
        float HalfMinX => zone > 0 ? arena.CenterX : arena.MinX;
        float HalfMaxX => zone < 0 ? arena.CenterX : arena.MaxX;

        // ---------------------------------------------------------------- orders (the duo, or itself when alone)
        public void Order(Atk a, int side = 0, bool holdWater = false)
        {
            atk = a; zone = side; keepWater = holdWater; intangible = false;
            float midX = (HalfMinX + HalfMaxX) * 0.5f;
            switch (a)
            {
                case Atk.Inferno: goal = new Vector2(midX, arena.CeilY - 1.4f); Go(S.Rise, 3f); break;
                case Atk.Volley: volleyN = Phase2 ? 4 : 3; shotT = 0.6f; goal = new Vector2(midX + Random.Range(-3f, 3f), arena.CeilY - 2f); Go(S.Hover, 5f); break;
                case Atk.Breath:
                    dashDir = zone != 0 ? zone : (P.pos.x < arena.CenterX ? 1f : -1f);   // breathes from the wall at the far side
                    goal = new Vector2(dashDir > 0 ? arena.MaxX - 1.5f : arena.MinX + 1.5f, arena.FloorY + 4.5f);
                    fireT = 0f; Go(S.Breath, 4.2f); break;
                case Atk.Dashes:   // right to left low, then left to right high... or, after the water dragon's, at other heights
                    dashN = 0;
                    if (mixHeights)
                    {
                        float[] hs = { arena.FloorY + 1.3f, arena.FloorY + ArenaRoom.IH * 0.45f, arena.FloorY + ArenaRoom.IH * 0.85f };
                        int a0 = Random.Range(0, 3), a1 = (a0 + Random.Range(1, 3)) % 3;
                        dashY2 = hs[a1];
                        StartDash(Random.value < 0.5f ? -1f : 1f, hs[a0]);
                    }
                    else { dashY2 = H2; StartDash(-1f, H1); }
                    break;
                case Atk.Flood: goal = new Vector2(arena.CenterX, arena.FloorY - 2.5f); Go(S.Dive, 2.5f); break;
                case Atk.Geysers: Geysers(); goal = new Vector2(midX, arena.CeilY - 2f); Go(S.Hover, 2f); volleyN = 0; break;
                case Atk.IceRing: volleyN = 0; goal = new Vector2(midX, arena.FloorY + 5f); Go(S.Hover, 1.6f); shotT = 1f; break;
                case Atk.Bubbles: volleyN = Phase2 ? 3 : 2; shotT = 0.7f; goal = new Vector2(midX, arena.CeilY - 2.5f); Go(S.Hover, 4f); break;
            }
            G.sfx.Play("rumble", 0.5f, elem == Elem.Fire ? 0.9f : 1.3f);
        }

        // slip into a wall corner and wait there, out of reach, while the other dragon attacks
        public void HideAway()
        {
            atk = Atk.None;
            bool left = elem == Elem.Fire;
            goal = new Vector2(left ? arena.MinX - 2.5f : arena.MaxX + 2.5f, left ? arena.FloorY + 1.5f : arena.CeilY - 1.5f);
            Go(S.Hide, 99f);
        }

        public void Roam(float time = 2f) { atk = Atk.None; intangible = false; roamT = time; Go(S.Roam, 99f); }

        void Go(S s, float time) { state = (int)s; stateT = time; }

        // ---------------------------------------------------------------- the loop
        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            switch ((S)state)
            {
                case S.Roam:
                    {
                        // a lazy figure-eight through the hall, spitting now and then
                        float w = ArenaRoom.AW * 0.33f, h = ArenaRoom.IH * 0.28f;
                        float ph = t * 0.55f + (elem == Elem.Fire ? 0f : 3.1f);
                        goal = new Vector2(arena.CenterX + Mathf.Sin(ph) * w, arena.FloorY + ArenaRoom.IH * 0.55f + Mathf.Sin(ph * 2f) * h);
                        Steer(goal, Spd, 170f, dt);
                        shotT -= dt;
                        if (shotT <= 0f && !InRock) { shotT = Phase2 ? 1.4f : 2f; Spit(); }
                        roamT -= dt;
                        if (roamT <= 0f && duo == null) NextSolo();
                        break;
                    }
                case S.Hide:
                    Steer(goal, Spd * 1.3f, 300f, dt);
                    intangible = InRock;
                    break;
                case S.Rise:   // INFERNO: up to the ceiling...
                    Steer(goal, Spd * 1.4f, 400f, dt);
                    if ((pos - goal).sqrMagnitude < 0.8f || stateT <= 0f) { vel = Vector2.down * 4f; dir = Vector2.down; Go(S.Slam, 2f); G.sfx.Play("rumble", 0.8f, 0.7f); }
                    break;
                case S.Slam:   // ...and down like a hammer
                    vel.y = Mathf.Max(vel.y - 70f * dt, -26f);
                    pos += vel * dt; dir = Vector2.down;
                    if (pos.y < arena.FloorY + 0.8f)
                    {
                        pos.y = arena.FloorY + 0.8f;
                        G.cam.Shake(0.6f); G.HitStop(0.08f);
                        G.sfx.Play("explode", 1f, 0.6f);
                        G.fx.Burst(new Vector2(pos.x, arena.FloorY + 0.2f), 30, FireC, 9f, 0.6f, 25f, 2f);
                        G.Spawn(new FloorFire(arena, HalfMinX, HalfMaxX, Phase2 ? 7.5f : 6.5f));
                        FloorWave(1, FireC); FloorWave(-1, FireC);
                        vel = Vector2.up * 12f;
                        Roam(duo != null ? 99f : 2.5f);
                    }
                    break;
                case S.Hover:   // VOLLEY / BUBBLES / ICE RING / after GEYSERS
                    Steer(goal + new Vector2(Mathf.Sin(t * 2f) * 1.2f, Mathf.Sin(t * 3f) * 0.5f), Spd * 0.8f, 220f, dt);
                    shotT -= dt;
                    if (shotT <= 0f && volleyN > 0 && !InRock)
                    {
                        volleyN--; shotT = atk == Atk.Bubbles ? 1f : 0.85f;
                        if (atk == Atk.Volley) Volley();
                        else if (atk == Atk.Bubbles) Bubbles();
                    }
                    if (atk == Atk.IceRing && shotT <= 0f && volleyN == 0)
                    {
                        volleyN = -1;
                        Ring(pos, Phase2 ? 14 : 10, 6f, t * 40f, Art.Spit, WaterC);
                        if (Phase2) Ring(pos, 8, 4f, t * 40f + 22f, Art.Spit, WaterC);
                        G.sfx.Play("freeze", 0.8f, 0.9f);
                    }
                    if (stateT <= 0f) Done();
                    break;
                case S.Breath:
                    {
                        Steer(goal, Spd * 1.4f, 400f, dt);
                        bool there = (pos - goal).sqrMagnitude < 1.2f;
                        if (!there) break;
                        dir = Vector2.right * -dashDir;
                        fireT += dt;
                        Vector2 mouth = pos + dir * 1.4f;
                        if (fireT < 0.7f) { if (Random.value < 0.5f) G.fx.Smoke(mouth, 1, 0.6f); break; }   // the telegraph: smoke curls from its jaws
                        float k = (fireT - 0.7f) / 1.6f;
                        if (k > 1f) { Done(); break; }
                        // sweep from the floor up through the hall
                        float ang = Mathf.Lerp(-35f, 30f, k) * (dashDir > 0 ? -1f : 1f);
                        if (Random.value < 0.75f)
                        {
                            Vector2 v = U.Rot(dir, ang + Random.Range(-5f, 5f)) * Random.Range(10f, 13f);
                            Shot(mouth, v, Art.Fireball, FireC, 0f, 0f, 0.8f);
                        }
                        if (Random.value < 0.3f) G.sfx.Play("flame", 0.4f, 0.8f, 0.1f);
                        break;
                    }
                case S.WarnDash:   // embers or bubbles at the wall where it will burst out
                    {
                        float wx = dashDir < 0 ? arena.MaxX - 0.1f : arena.MinX + 0.1f;
                        pos = new Vector2(dashDir < 0 ? arena.MaxX + 3f : arena.MinX - 3f, dashY);
                        if (Random.value < 0.8f) G.fx.Burst(new Vector2(wx, dashY + Random.Range(-0.8f, 0.8f)), 2, Col, 4f, 0.4f, 0f);
                        G.cam.Shake(0.04f);
                        if (stateT <= 0f)
                        {
                            float sp = (elem == Elem.Water ? (Phase2 ? 7.5f : 6.5f) : (Phase2 ? 12f : 10f)) * Rage;
                            vel = new Vector2(dashDir * sp, 0f);
                            dir = vel.normalized;
                            G.sfx.Play("dash", 1f, elem == Elem.Water ? 0.5f : 0.7f);
                            Go(S.Dash, 8f);
                        }
                        break;
                    }
                case S.Dash:
                    pos += vel * dt; dir = vel.normalized;
                    if (elem == Elem.Fire)
                    {
                        // it boils the water it rushes through
                        var aw = ArenaWater.For(arena);
                        if (aw != null) aw.BoilAround(pos, 2.2f);
                        if (Random.value < 0.6f) G.fx.Burst(pos - dir, 2, FireC, 3f, 0.4f, -4f);
                    }
                    else if (Random.value < 0.5f) G.fx.Burst(pos - dir, 1, WaterC, 2f, 0.5f, -3f, 1f, false);
                    if ((dashDir < 0 && pos.x < arena.MinX - 3f) || (dashDir > 0 && pos.x > arena.MaxX + 3f) || stateT <= 0f)
                    {
                        dashN++;
                        if (dashN < 2) StartDash(-dashDir, dashY2);
                        else
                        {
                            if (elem == Elem.Fire) { var aw = ArenaWater.For(arena); if (aw != null) aw.Vaporize(); }
                            if (atk == Atk.Flood && !keepWater) { var aw = ArenaWater.For(arena); if (aw != null) aw.Drain(); }
                            pos = new Vector2(Mathf.Clamp(pos.x, arena.MinX + 1f, arena.MaxX - 1f), arena.FloorY - 2.5f);
                            ResetTrail();
                            Done();
                        }
                    }
                    break;
                case S.Dive:   // FLOOD: down into the floor... and the arena fills
                    Steer(goal, Spd * 1.4f, 400f, dt);
                    if (pos.y < arena.FloorY - 1.5f || stateT <= 0f)
                    {
                        ArenaWater.Fill(arena);
                        G.sfx.Play("rumble", 1f, 1.4f);
                        G.cam.Shake(0.4f);
                        Go(S.Flooded, 1.6f);
                    }
                    break;
                case S.Flooded:
                    if (stateT <= 0f) { dashN = 0; dashY2 = H2; StartDash(-1f, H1); }
                    break;
            }
            if ((trail[0] - pos).sqrMagnitude > 0.02f) trail.Insert(0, pos);
            while (trail.Count > 140) trail.RemoveAt(trail.Count - 1);
            PlaceSegments();
            DrawBoss(0f);
            if (dead) return;
            if (sr != null)
            {
                int fi = (atk == Atk.Breath && fireT > 0.7f) || (S)state == S.Dash || (S)state == S.Slam ? 1 : 0;   // jaws open to attack
                sr.sprite = flash > 0f ? whites[fi] : frames[fi];
                sr.enabled = !InRock;
                sr.flipX = false;
                sr.flipY = dir.x < 0f;
            }
            if (go != null) go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        void Done()
        {
            atk = Atk.None;
            Roam(duo != null ? 99f : (Phase2 ? 1.6f : 2.2f) / (Rage * Rage));
        }

        // the partner is dead: it burns red with rage and fights on faster
        public void Enrage()
        {
            enraged = true; duo = null; mixHeights = false;
            tint = new Color(1f, 0.45f, 0.4f);
            G.hud.Banner(title + " IS ENRAGED", "", new Color(1f, 0.35f, 0.3f));
            G.sfx.Play("rumble", 1f, 0.7f);
            G.cam.Shake(0.5f);
            G.fx.Burst(pos, 30, new Color(1f, 0.3f, 0.2f), 8f, 0.7f, 0f);
            Roam(1f);
        }

        // alone, it cycles through its moves
        void NextSolo()
        {
            solo++;
            if (elem == Elem.Fire)
            {
                switch (solo % 4)
                {
                    case 1: Order(Atk.Inferno); break;
                    case 2: Order(Atk.Volley); break;
                    case 3: Order(Atk.Breath); break;
                    default: Order(Phase2 ? Atk.Dashes : Atk.Volley); break;
                }
            }
            else
            {
                switch (solo % 4)
                {
                    case 1: Order(Atk.Flood); break;
                    case 2: Order(Atk.Geysers); break;
                    case 3: Order(Atk.IceRing); break;
                    default: Order(Atk.Bubbles); break;
                }
            }
        }

        void StartDash(float d, float y)
        {
            dashDir = d; dashY = y;
            pos = new Vector2(d < 0 ? arena.MaxX + 3f : arena.MinX - 3f, y);
            ResetTrail();
            G.sfx.Play("rumble", 0.8f, elem == Elem.Fire ? 1f : 1.4f);
            Go(S.WarnDash, (elem == Elem.Water ? 1.1f : 0.85f) / Rage);
        }

        void ResetTrail() { trail.Clear(); for (int i = 0; i < 90; i++) trail.Add(pos); }

        void Steer(Vector2 to, float spd, float turnDeg, float dt)
        {
            Vector2 want = to - pos;
            if (want.sqrMagnitude < 0.04f) return;
            dir = Vector3.RotateTowards(dir, want.normalized, turnDeg * Mathf.Deg2Rad * dt, 0f);
            float s = Mathf.Min(spd, want.magnitude * 4f);
            pos += dir * s * dt;
        }

        // ---------------------------------------------------------------- shots
        void Spit()
        {
            Vector2 m = pos + dir * 1.2f;
            if (elem == Elem.Fire) Shot(m, ToPlayer * 8f, Art.Fireball, FireC);
            else Shot(m, ToPlayer * 6.5f, Art.Spit, WaterC);
            G.sfx.Play("shoot", 0.5f, elem == Elem.Fire ? 0.7f : 1.2f);
        }

        void Volley()
        {
            int n = Phase2 ? 5 : 3;
            for (int i = 0; i < n; i++)
            {
                float tFly = 1f + i * 0.1f;
                Vector2 to = P.pos + new Vector2((i - (n - 1) * 0.5f) * 1.6f, 0f);
                var v = new Vector2((to.x - pos.x) / tFly, (to.y - pos.y + 0.5f * 14f * tFly * tFly) / tFly);
                Shot(pos, v, Art.Fireball, FireC, 14f, i * 0.05f, 1.1f);
            }
            G.sfx.Play("launcher", 0.7f, 0.8f);
        }

        void Bubbles()
        {
            int n = Phase2 ? 4 : 3;
            for (int i = 0; i < n; i++) Shot(pos, U.Rot(ToPlayer, (i - (n - 1) * 0.5f) * 18f) * 3.8f, Art.Circle, WaterC, 0f, i * 0.08f, 1.3f);
            G.sfx.Play("blip", 0.6f, 0.6f);
        }

        void Geysers()
        {
            int n = Phase2 ? 5 : 3;
            int dmgP = Enemy.ContactDmg(arena.Depth);
            for (int i = 0; i < n; i++)
            {
                float x = i == 0 ? Mathf.Clamp(P.pos.x, HalfMinX + 1f, HalfMaxX - 1f) : Random.Range(HalfMinX + 1f, HalfMaxX - 1f);
                var c = new Vector2(Mathf.Floor(x) + 0.5f, arena.FloorY + ArenaRoom.IH * 0.5f);
                G.Spawn(new Hazard(c, new Vector2(0.5f, ArenaRoom.IH * 0.5f), 0.95f + i * 0.15f, 0.45f, WaterC, dmgP, false,
                    () => { G.fx.Burst(new Vector2(c.x, arena.FloorY + 0.5f), 18, WaterC, 9f, 0.6f, -5f, 1.5f, false); G.sfx.Play("splat", 0.6f, 0.7f); }));
            }
            G.sfx.Play("rumble", 0.7f, 1.5f);
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
            if (duo != null) duo.OnDragonDied(this);
            base.Die();
        }

        public override void Kill()
        {
            base.Kill();
            foreach (var s in segs) s.Kill();
        }

        public override void AddLight(Lighting l) { if (!InRock) l.Add(pos, 3.5f, 0.7f); }
    }

    // =====================================================================================
    // Both dragons at once. They take turns (the other hides in a wall corner), split the hall between them,
    // and combine: the fire dragon sets the floor alight, the water dragon floods the hall (putting it out before it
    // burns down) and swims through it, then the fire dragon tears through the water, boiling it into steam.
    // =====================================================================================
    public class DragonDuo : Entity
    {
        readonly BossArena arena;
        readonly Dragon fire, water;
        int step;
        float waitT = 2.5f;
        enum Phase { Pick, Combo1, Combo2, Combo3, Turn, Split }
        Phase ph = Phase.Pick;
        float comboT;
        Dragon turnA, turnB;

        public override bool AlwaysActive => true;

        public DragonDuo(BossArena a, Dragon f, Dragon w)
        {
            arena = a; fire = f; water = w;
            f.duo = this; w.duo = this;
            pos = new Vector2(a.CenterX, a.FloorY + 5f);
        }

        public void OnDragonDied(Dragon d)
        {
            // the survivor fights on alone
            var other = d == fire ? water : fire;
            if (other != null && !other.dead) other.Enrage();
            Kill();
        }

        public override void Tick(float dt)
        {
            if (fire.dead || water.dead) { Kill(); return; }
            switch (ph)
            {
                case Phase.Pick:
                    waitT -= dt;
                    if (waitT > 0f) return;
                    step++;
                    switch (step % 4)
                    {
                        case 1:   // the combo: fire floor -> flood (puts it out) -> boiling dashes
                            water.HideAway();
                            fire.Order(Dragon.Atk.Inferno);
                            ph = Phase.Combo1; comboT = 0f;
                            break;
                        case 2: case 0:   // turns: one attacks, the other hides
                            turnA = step % 8 < 4 ? fire : water; turnB = turnA == fire ? water : fire;
                            turnB.HideAway();
                            turnA.Order(turnA == fire ? (Random.value < 0.5f ? Dragon.Atk.Volley : Dragon.Atk.Breath)
                                                       : (Random.value < 0.5f ? Dragon.Atk.Geysers : Dragon.Atk.IceRing));
                            ph = Phase.Turn;
                            break;
                        default:  // split: both at once, each on its own half
                            int fs = Random.value < 0.5f ? -1 : 1;
                            fire.Order(Random.value < 0.5f ? Dragon.Atk.Inferno : Dragon.Atk.Volley, fs);
                            water.Order(Random.value < 0.5f ? Dragon.Atk.Geysers : Dragon.Atk.Bubbles, -fs);
                            ph = Phase.Split;
                            break;
                    }
                    break;
                case Phase.Combo1:
                    comboT += dt;
                    // the floor is burning: the flood comes before the fire would die down by itself
                    if (comboT > 1.2f && FloorFire.Burning(arena) && fire.Idle)
                    {
                        fire.HideAway();
                        water.Order(Dragon.Atk.Flood, 0, true);
                        ph = Phase.Combo2;
                    }
                    else if (comboT > 6f) { ph = Phase.Pick; waitT = 1f; }
                    break;
                case Phase.Combo2:
                    if (water.Idle)
                    {
                        water.HideAway();
                        fire.mixHeights = true;
                        fire.Order(Dragon.Atk.Dashes);
                        fire.mixHeights = false;
                        ph = Phase.Combo3;
                    }
                    break;
                case Phase.Combo3:
                    if (fire.Idle) Rest();
                    break;
                case Phase.Turn:
                    if (turnA.Idle) Rest();
                    break;
                case Phase.Split:
                    if (fire.Idle && water.Idle) Rest();
                    break;
            }
        }

        void Rest()
        {
            fire.Roam(99f); water.Roam(99f);
            ph = Phase.Pick;
            waitT = (fire.HpFrac < 0.5f || water.HpFrac < 0.5f) ? 1.6f : 2.4f;
        }
    }

    // =====================================================================================
    // The burning floor of the INFERNO: every floor tile (in its stretch) burns until water puts it out or it dies down.
    // Only the floor row burns: the ledges are safe.
    // =====================================================================================
    public class FloorFire : Entity
    {
        static readonly List<FloorFire> All = new List<FloorFire>();
        readonly BossArena arena;
        readonly int x0, x1;
        readonly bool[] lit;
        float life, touchT;
        readonly float y;

        public override bool AlwaysActive => true;

        public static void ClearAll() => All.Clear();

        public static bool Burning(BossArena a)
        {
            foreach (var f in All) if (!f.dead && f.arena == a) return true;
            return false;
        }

        public FloorFire(BossArena a, float minX, float maxX, float time)
        {
            arena = a; life = time;
            x0 = Mathf.FloorToInt(minX); x1 = Mathf.FloorToInt(maxX - 0.01f);
            y = a.FloorY;
            lit = new bool[x1 - x0 + 1];
            for (int i = 0; i < lit.Length; i++) lit[i] = !Wd.Solid(x0 + i, Mathf.FloorToInt(y));
            pos = new Vector2((minX + maxX) * 0.5f, y + 0.5f);
            All.Add(this);
            G.sfx.Play("ignite", 1f, 0.7f);
        }

        public override void Tick(float dt)
        {
            life -= dt;
            int any = 0;
            float ceil = arena.CeilY;
            for (int i = 0; i < lit.Length; i++)
            {
                if (!lit[i]) continue;
                int cx = x0 + i, cy = Mathf.FloorToInt(y);
                // water puts it out
                if (Wd.LiquidType(cx, cy) == Fluids.Water && Wd.LiquidAmount(cx, cy) > 0.2f)
                {
                    lit[i] = false;
                    G.fx.Smoke(new Vector2(cx + 0.5f, y + 0.4f), 2, 0.8f);
                    if (Random.value < 0.3f) G.sfx.Play("fizz", 0.3f, 1.2f);
                    continue;
                }
                any++;
                if (life < 1f && Random.value < dt * 2f) { lit[i] = false; continue; }   // dying down
                if (Random.value < 0.45f)
                    G.fx.Burst(new Vector2(cx + Random.Range(0.1f, 0.9f), y + 0.15f), 1, new Color(1f, Random.Range(0.35f, 0.85f), 0.15f), 1.5f, 0.45f, -7f);
                // the ceiling above it burns too
                if (Random.value < 0.35f)
                    G.fx.Burst(new Vector2(cx + Random.Range(0.1f, 0.9f), ceil - 0.15f), 1, new Color(1f, Random.Range(0.35f, 0.85f), 0.15f), 1.2f, 0.4f, -3f);
            }
            if (any == 0 || life <= 0f) { Kill(); return; }
            // and the walls, where the fire reaches them
            bool leftWall = lit.Length > 0 && lit[0] && x0 <= Mathf.FloorToInt(arena.MinX);
            bool rightWall = lit.Length > 0 && lit[lit.Length - 1] && x1 >= Mathf.FloorToInt(arena.MaxX - 0.01f);
            for (int k = 0; k < 2; k++)
            {
                if (!(k == 0 ? leftWall : rightWall) || Random.value > 0.9f) continue;
                float wx = k == 0 ? arena.MinX + 0.12f : arena.MaxX - 0.12f;
                G.fx.Burst(new Vector2(wx, Random.Range(y, ceil)), 1, new Color(1f, Random.Range(0.35f, 0.85f), 0.15f), 1.2f, 0.4f, -6f);
            }
            // like any fire: stay in it a moment (on the floor, against a burning wall or the ceiling) and you catch fire
            var p = G.player;
            if (p.mode != Player.Mode.Dead)
            {
                float feet = p.pos.y - Player.Half.y, head = p.pos.y + Player.Half.y;
                int px0 = Mathf.FloorToInt(p.pos.x - Player.Half.x), px1 = Mathf.FloorToInt(p.pos.x + Player.Half.x);
                bool inIt = false;
                for (int cx = px0; cx <= px1; cx++)
                {
                    int i = cx - x0;
                    if (i >= 0 && i < lit.Length && lit[i] && (feet < y + 0.9f || head > ceil - 0.6f)) inIt = true;
                }
                if (leftWall && p.pos.x - Player.Half.x < arena.MinX + 0.35f) inIt = true;
                if (rightWall && p.pos.x + Player.Half.x > arena.MaxX - 0.35f) inIt = true;
                if (!inIt) touchT = 0f;
                else
                {
                    touchT += dt;
                    if (touchT > 0.5f) p.SetOnFire();
                }
            }
        }

        public override void AddLight(Lighting l)
        {
            int n = 0;
            foreach (var b in lit) if (b) n++;
            if (n > 0) l.Add(pos, Mathf.Min(10f, 2f + n * 0.25f), 0.7f);
        }

        public override void Kill() { base.Kill(); All.Remove(this); }
    }

    // =====================================================================================
    // The water of a flooded arena: fills it bottom to top, keeps it full, then drains it away (or is boiled off
    // by the fire dragon into steam under the ceiling, which clears after a while).
    // =====================================================================================
    public class ArenaWater : Entity
    {
        static readonly List<ArenaWater> All = new List<ArenaWater>();
        public static void ClearAll() => All.Clear();
        readonly BossArena arena;
        enum S { Rising, Full, Draining, Steam }
        S st = S.Rising;
        float level, steamT;
        int bottom, top;

        public override bool AlwaysActive => true;
        public bool Full => st == S.Full;

        public static ArenaWater For(BossArena a)
        {
            foreach (var w in All) if (!w.dead && w.arena == a) return w;
            return null;
        }

        public static void Fill(BossArena a)
        {
            var w = For(a);
            if (w == null) G.Spawn(new ArenaWater(a));
            else { w.st = S.Rising; }
        }

        ArenaWater(BossArena a)
        {
            arena = a;
            bottom = Mathf.FloorToInt(a.FloorY); top = Mathf.FloorToInt(a.CeilY - 0.01f);
            pos = new Vector2(a.CenterX, a.FloorY + ArenaRoom.IH * 0.5f);
            All.Add(this);
        }

        int X0 => Mathf.FloorToInt(arena.MinX);
        int X1 => Mathf.FloorToInt(arena.MaxX - 0.01f);

        void SetRow(int y, bool water)
        {
            for (int x = X0; x <= X1; x++)
            {
                if (Wd.Solid(x, y)) continue;
                if (water) Wd.SetLiquid(x, y, Fluids.Water, 1f);
                else if (Wd.LiquidType(x, y) == Fluids.Water) Wd.SetLiquid(x, y, Fluids.None, 0f);
            }
        }

        public void Drain() { if (st != S.Steam) st = S.Draining; }

        // the fire dragon boils it all: the water is gone, steam hangs under the ceiling for a while
        public void Vaporize()
        {
            for (int y = bottom; y <= top; y++) SetRow(y, false);
            for (int y = top - 1; y <= top; y++)
                for (int x = X0; x <= X1; x++) if (!Wd.Solid(x, y)) Wd.AddSteam(x, y, 0.8f);
            G.fx.Smoke(pos, 30, 2f);
            G.sfx.Play("fizz", 1f, 0.7f);
            st = S.Steam; steamT = 5f; level = 0f;
        }

        public void BoilAround(Vector2 p, float r)
        {
            if (st == S.Steam) return;
            int x0 = Mathf.FloorToInt(p.x - r), x1 = Mathf.FloorToInt(p.x + r), y0 = Mathf.FloorToInt(p.y - r), y1 = Mathf.FloorToInt(p.y + r);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (x >= X0 && x <= X1 && y >= bottom && y <= top && Wd.LiquidType(x, y) == Fluids.Water)
                    {
                        Wd.SetLiquid(x, y, Fluids.None, 0f);
                        if (Random.value < 0.3f) G.fx.Smoke(new Vector2(x + 0.5f, y + 0.5f), 1, 0.6f);
                    }
            if (Random.value < 0.2f) G.sfx.Play("fizz", 0.4f, 1f);
        }

        public override void Tick(float dt)
        {
            int rows = top - bottom + 1;
            switch (st)
            {
                case S.Rising:
                    level = Mathf.Min(rows, level + dt * rows / 1.4f);
                    for (int i = 0; i < Mathf.CeilToInt(level); i++) SetRow(bottom + i, true);
                    if (Random.value < 0.6f) G.fx.Burst(new Vector2(Random.Range(arena.MinX, arena.MaxX), arena.FloorY + level), 1, new Color(0.75f, 0.95f, 1f), 2f, 0.4f, 0f, 1f, false);
                    if (level >= rows) st = S.Full;
                    break;
                case S.Full:
                    if (Random.value < dt * 2f) for (int y = bottom; y <= top; y++) SetRow(y, true);   // keep it topped up
                    break;
                case S.Draining:
                    level = Mathf.Max(0f, level - dt * rows / 1.6f);
                    for (int y = bottom + Mathf.CeilToInt(level); y <= top; y++) SetRow(y, false);
                    if (level <= 0f) { for (int y = bottom; y <= top; y++) SetRow(y, false); Kill(); }
                    break;
                case S.Steam:
                    steamT -= dt;
                    if (steamT <= 0f)
                    {
                        for (int y = bottom; y <= top; y++)
                            for (int x = X0; x <= X1; x++) if (Wd.LiquidType(x, y) == Fluids.Steam) Wd.SetLiquid(x, y, Fluids.None, 0f);
                        Kill();
                    }
                    break;
            }
        }

        public override void Kill() { base.Kill(); All.Remove(this); }
    }
}
