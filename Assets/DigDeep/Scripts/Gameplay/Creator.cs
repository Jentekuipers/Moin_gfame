using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // The last hall of the void, once the Archmage has wiped it: nothing but black, the size of the screen.
    // Its edges are walls just out of sight, and the void's shots wrap from one side to the other.
    public static class VoidStage
    {
        public static bool Active, CamLock;
        public static Vector2 Center, Min, Max;

        public static void Begin(Vector2 c, float halfW, float halfH)
        {
            Active = true;
            Center = c;
            Min = c - new Vector2(halfW, halfH);
            Max = c + new Vector2(halfW, halfH);
            // no walls you could see: you fly free inside the screen, shots that leave it are gone into the dark
        }

        // the white void (after the Creator's clap): a white sheet over the tiles, behind everything else
        public static bool White;
        static GameObject whiteGo;
        public static void MakeWhite()
        {
            White = true;
            if (whiteGo != null) return;
            whiteGo = new GameObject("WhiteVoid");
            whiteGo.transform.SetParent(Game.I.runRoot, false);
            var r = Art.AddSR(whiteGo, Art.Square, 1);
            r.color = new Color(0.97f, 0.97f, 0.96f);
        }
        public static void Follow(CameraRig cam)
        {
            if (whiteGo == null) return;
            whiteGo.transform.position = new Vector3(cam.pos.x, cam.pos.y, 0f);
            whiteGo.transform.localScale = new Vector3(cam.halfW * 3.6f + 16f, cam.halfH * 3.6f + 16f, 1f);   // well past every edge, shake and all
        }

        // a little way past the screen's edge: anything of yours out there simply leaves into space
        // anything that drifts well off the screen is gone (foes, pickups, spears, corpses)
        public static void Sweep()
        {
            if (!Active) return;
            foreach (var e in Game.I.ents)
                if (!e.dead && !(e is Player) && !(e is BossArena) && !(e is Boss) && !(e is CreatorHand) && !InsideMargin(e.pos, 3f) && InsideMargin(e.pos, 30f)) e.Kill();
        }

        public static bool InsideMargin(Vector2 p, float m) => p.x >= Min.x - m && p.x <= Max.x + m && p.y >= Min.y - m && p.y <= Max.y + m;

        public static void End() { Active = false; CamLock = false; White = false; whiteGo = null; }

        // shots that leave one side come back in on the other
        public static void Wrap(ref Vector2 p)
        {
            float w = Max.x - Min.x, h = Max.y - Min.y;
            if (p.x < Min.x) p.x += w; else if (p.x > Max.x) p.x -= w;
            if (p.y < Min.y) p.y += h; else if (p.y > Max.y) p.y -= h;
        }

        public static bool Inside(Vector2 p) => p.x >= Min.x && p.x <= Max.x && p.y >= Min.y && p.y <= Max.y;
    }

    // ===================================================================================== THE CREATOR
    // Two hands out of the dark. They slam, sweep, clap and point; and like an animator at a desk they draw
    // things into the world: any creature of the deep, or a sketched gun that turns on you.
    public class Creator : Boss
    {
        enum S { Enter, Back, Idle, Tele, Act, Rest, Dying }
        enum A { Slam, Sweep, Point, Clap, DrawFoe, DrawGun, DoubleSlam }
        A act;
        readonly CreatorHand left;
        readonly Vector2 clapAt;
        Vector2 aim, goal, lgoal;
        int left_n;
        float shotT;
        bool clapped, lethalR;
        static readonly Color InkW = new Color(0.95f, 0.95f, 1f), Void = new Color(0.65f, 0.35f, 1f);
        // ink is white on the black void, and near-black once the void has turned white
        static Color Ink => VoidStage.White ? new Color(0.1f, 0.06f, 0.14f) : InkW;
        static readonly Vector2 HandHalf = new Vector2(1.8f, 1.1f);
        public static readonly Color HitRed = new Color(1f, 0.2f, 0.2f);
        public bool LethalLeft;
        VoidLord lord;

        protected override bool Harmless => !lethalR;
        protected override bool CanBeElite => false;
        int Dmg => ContactDmg(arena.Depth) + 20;
        float Floor => VoidStage.Min.y;

        public Creator(BossArena a, float hp, Vector2 at, VoidLord lord)
        {
            this.lord = lord;
            clapAt = at;
            SetupBoss("THE CREATOR", "", "creatorhand", hp, 3f, HandHalf, a, new Vector2(VoidStage.Max.x + 4f, at.y));
            blood = Ink;
            left = new CreatorHand(this, new Vector2(VoidStage.Min.x - 4f, at.y));
            G.Spawn(left);
            intangible = true;
            Go(S.Enter, 2.2f);
        }

        void Go(S s, float time) { state = (int)s; stateT = time; }

        // each hand rests on its own side, a little off the middle
        Vector2 RHome => new Vector2(VoidStage.Center.x + 6.5f, VoidStage.Center.y + 1.5f + Mathf.Sin(t * 1.3f) * 0.4f);
        Vector2 LHome => new Vector2(VoidStage.Center.x - 6.5f, VoidStage.Center.y + 1.5f + Mathf.Sin(t * 1.3f + 2f) * 0.4f);

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt; shotT -= dt;
            lethalR = false; LethalLeft = false;
            VoidStage.Follow(G.cam);
            VoidStage.Sweep();
            switch ((S)state)
            {
                case S.Enter:
                    // the hands come in from either side and clap where the Archmage was
                    goal = clapAt + Vector2.right * (stateT > 0.5f ? 3f : 0.7f);
                    lgoal = clapAt + Vector2.left * (stateT > 0.5f ? 3f : 0.7f);
                    pos = Vector2.Lerp(pos, goal, 1f - Mathf.Exp(-dt * (stateT > 0.5f ? 3f : 14f)));
                    left.pos = Vector2.Lerp(left.pos, lgoal, 1f - Mathf.Exp(-dt * (stateT > 0.5f ? 3f : 14f)));
                    if (stateT <= 0.35f && !clapped)
                    {
                        clapped = true;
                        G.sfx.Play("explode", 1f, 1.5f);
                        G.cam.Shake(0.8f);
                        G.lighting.Flash(clapAt, 14f, 0.6f);
                        G.hud.ScreenFlash(0.6f);
                        G.fx.Burst(clapAt, 60, Ink, 10f, 0.8f, 0f, 2f);
                        G.fx.Ring(clapAt, 4f, Ink);
                        if (lord != null) lord.Vanish();
                        VoidStage.MakeWhite();   // the void turns white for the rest of it
                        G.boss = this;   // its health bar and name come up
                        G.hud.Banner(title, "", Ink);
                        G.music.PlayBoss();
                    }
                    if (stateT <= 0f) Go(S.Back, 2.6f);
                    break;
                case S.Back:
                    // it slowly draws its hands back to either side; only then can it be hurt
                    goal = RHome; lgoal = LHome;
                    Follow(dt, 1.4f);
                    if (stateT <= 0f) { intangible = false; Go(S.Idle, 0.6f); }
                    break;
                case S.Dying:
                    TickDying(dt);
                    return;
                case S.Idle:
                    goal = RHome; lgoal = LHome;
                    Follow(dt, 5f);
                    if (stateT <= 0f) Choose();
                    break;
                case S.Tele:
                    Follow(dt, 7f);
                    if (Random.value < 0.6f) G.fx.Burst(act == A.DrawFoe || act == A.DrawGun ? pos + Vector2.down * 0.8f : pos, 1, Ink, 2f, 0.3f, 0f);
                    if (stateT <= 0f) Begin();
                    break;
                case S.Act:
                    Act(dt);
                    break;
                case S.Rest:
                    goal = RHome; lgoal = LHome;
                    Follow(dt, 4f);
                    if (stateT <= 0f) Go(S.Idle, Phase2 ? 0.4f : 0.8f);
                    break;
            }
            // touching a hand mid-attack hurts
            var pl = G.player;
            if (pl.mode != Player.Mode.Dead)
            {
                if (LethalLeft && U.Overlap(left.pos, half, pl.pos, Player.Half)) pl.Hurt(Dmg, left.pos);
            }
            DrawHands();
        }

        void Follow(float dt, float speed)
        {
            pos = Vector2.Lerp(pos, goal, 1f - Mathf.Exp(-dt * speed));
            left.pos = Vector2.Lerp(left.pos, lgoal, 1f - Mathf.Exp(-dt * speed));
        }

        // ---------------------------------------------------------------- choosing and starting attacks

        void Choose()
        {
            var opts = new List<A> { A.Slam, A.Sweep, A.Point, A.Clap, A.DrawFoe, A.DrawGun };
            if (Phase2) { opts.Add(A.DoubleSlam); opts.Add(A.DrawFoe); }
            opts.Remove(act);
            act = opts[Random.Range(0, opts.Count)];
            float tele = Phase2 ? 0.55f : 0.8f;
            switch (act)
            {
                case A.Slam: goal = new Vector2(P.pos.x, Mathf.Min(P.pos.y + 6f, VoidStage.Max.y - 1f)); lgoal = LHome; break;
                case A.DoubleSlam: goal = new Vector2(P.pos.x + 2.5f, Mathf.Min(P.pos.y + 6f, VoidStage.Max.y - 1f)); lgoal = new Vector2(P.pos.x - 2.5f, Mathf.Min(P.pos.y + 6f, VoidStage.Max.y - 1f)); break;
                case A.Sweep:
                    {
                        // (you fly out here: it swats across at the height you're at, not along a floor)
                        bool fromRight = P.pos.x < VoidStage.Center.x;
                        sweepY = Mathf.Clamp(P.pos.y, VoidStage.Min.y + HandHalf.y, VoidStage.Max.y - HandHalf.y);
                        goal = new Vector2(fromRight ? VoidStage.Max.x - 1.5f : VoidStage.Min.x + 1.5f, sweepY);
                        lgoal = LHome; tele += 0.3f; break;
                    }
                case A.Point: goal = new Vector2(VoidStage.Center.x + (P.pos.x < VoidStage.Center.x ? 6f : -6f), VoidStage.Center.y + 2f); lgoal = LHome; break;
                case A.Clap:
                    {
                        float y = Mathf.Clamp(P.pos.y, Floor + 1f, VoidStage.Max.y - 1.5f);
                        goal = new Vector2(VoidStage.Max.x - 1.5f, y); lgoal = new Vector2(VoidStage.Min.x + 1.5f, y);
                        break;
                    }
                case A.DrawFoe:
                case A.DrawGun:
                    aim = new Vector2(Random.Range(VoidStage.Min.x + 3f, VoidStage.Max.x - 3f), act == A.DrawFoe ? Floor + 1.5f : Random.Range(Floor + 4f, VoidStage.Max.y - 2f));
                    if (Random.value < 0.5f) { goal = aim + new Vector2(0.4f, 1f); lgoal = LHome; drawRight = true; }
                    else { lgoal = aim + new Vector2(-0.4f, 1f); goal = RHome; drawRight = false; }
                    break;
            }
            G.sfx.Play("rumble", 0.4f, 1.5f);
            Go(S.Tele, tele);
        }

        bool drawRight;

        void Begin()
        {
            switch (act)
            {
                case A.Slam: case A.DoubleSlam: vel = Vector2.zero; left_n = Phase2 ? 2 : 1; slamming = false; shotT = 0.15f; Go(S.Act, 6f); break;
                case A.Sweep: Go(S.Act, 2.4f); break;
                case A.Point: left_n = Phase2 ? 4 : 3; shotT = 0.1f; Go(S.Act, 6f); break;
                case A.Clap: shotT = 0.4f; Go(S.Act, 3f); break;
                case A.DrawFoe: case A.DrawGun: shotT = 1.1f; Go(S.Act, 3f); break;
            }
        }

        bool slamming;

        void Act(float dt)
        {
            switch (act)
            {
                case A.Slam:
                case A.DoubleSlam:
                    {
                        bool both = act == A.DoubleSlam;
                        if (!slamming)
                        {
                            // hover over you a moment, then drop like a stone, down through where you are
                            goal.x = Mathf.Lerp(goal.x, P.pos.x + (both ? 2.5f : 0f), dt * 3f);
                            goal.y = Mathf.Lerp(goal.y, Mathf.Min(P.pos.y + 6f, VoidStage.Max.y - 1f), dt * 3f);
                            if (both) { lgoal.x = Mathf.Lerp(lgoal.x, P.pos.x - 2.5f, dt * 3f); lgoal.y = goal.y; }
                            Follow(dt, 8f);
                            if (shotT <= 0f) { slamming = true; slamY = Mathf.Max(Floor + HandHalf.y, P.pos.y - 2.5f); }
                            break;
                        }
                        lethalR = true; LethalLeft = both;
                        pos.y -= 30f * dt;
                        if (both) left.pos.y -= 30f * dt;
                        if (pos.y <= slamY)
                        {
                            pos.y = slamY;
                            if (both) left.pos.y = slamY;
                            G.cam.Shake(0.6f);
                            G.sfx.Play("explode", 0.9f, 0.7f);
                            Shockwave(pos);
                            if (both) Shockwave(left.pos);
                            left_n--;
                            if (left_n > 0) { slamming = false; shotT = 0.35f; goal = new Vector2(P.pos.x, Mathf.Min(P.pos.y + 6f, VoidStage.Max.y - 1f)); lgoal = both ? new Vector2(P.pos.x - 2.5f, goal.y) : LHome; }
                            else Go(S.Rest, 1.2f);
                        }
                        break;
                    }
                case A.Sweep:
                    {
                        // flat across the screen at the height you were, from one side to the other
                        lethalR = true;
                        float dir = pos.x > VoidStage.Center.x ? -1f : 1f;
                        if (stateT > 2.3f) sweepDir = dir;
                        pos.x += sweepDir * 16f * dt;
                        pos.y = Mathf.MoveTowards(pos.y, sweepY, 4f * dt);
                        if (Random.value < 0.6f) G.fx.Burst(pos + new Vector2(-sweepDir * 1.2f, -0.6f), 2, Ink, 3f, 0.3f, 10f);
                        if (pos.x < VoidStage.Min.x + 1.2f || pos.x > VoidStage.Max.x - 1.2f) { if (stateT < 2.1f) Go(S.Rest, 1f); }
                        break;
                    }
                case A.Point:
                    // finger guns: volleys of ink at you (they wrap round the edges)
                    goal.y = VoidStage.Center.y + 2f + Mathf.Sin(t * 3f);
                    Follow(dt, 6f);
                    if (shotT <= 0f && left_n > 0)
                    {
                        left_n--; shotT = Phase2 ? 0.45f : 0.6f;
                        Vector2 d = (P.pos - pos).normalized;
                        for (int k = -1; k <= 1; k++) Ink1(pos + d * 1.2f, U.Rot(d, k * 12f) * 9f);
                        G.sfx.Play("shoot", 0.6f, 1.3f);
                    }
                    if (left_n <= 0 && shotT <= -0.4f) Go(S.Rest, 1f);
                    break;
                case A.Clap:
                    if (shotT <= 0f)
                    {
                        lethalR = true; LethalLeft = true;
                        goal = new Vector2(VoidStage.Center.x + 0.9f, goal.y); lgoal = new Vector2(VoidStage.Center.x - 0.9f, lgoal.y);
                        Follow(dt, 16f);
                        if (pos.x - left.pos.x < 2.2f)
                        {
                            Vector2 c = (pos + left.pos) * 0.5f;
                            G.sfx.Play("explode", 0.9f, 1.4f);
                            G.cam.Shake(0.5f);
                            G.fx.Burst(c, 40, Ink, 9f, 0.5f, 0f);
                            for (int k = 0; k < 10; k++)
                            {
                                float a = (k * 36f + 18f) * Mathf.Deg2Rad;
                                Ink1(c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f);
                            }
                            Go(S.Rest, 1.2f);
                        }
                    }
                    else Follow(dt, 8f);
                    break;
                case A.DrawFoe:
                case A.DrawGun:
                    {
                        // the hand scribbles at the spot... and what it drew comes alive
                        Vector2 tip = aim + new Vector2(Mathf.Cos(t * 18f) * 0.9f, Mathf.Sin(t * 23f) * 0.6f);
                        if (drawRight) goal = tip + new Vector2(0.4f, 1f); else lgoal = tip + new Vector2(-0.4f, 1f);
                        Follow(dt, 18f);
                        G.fx.Burst(tip, 2, Ink, 0.3f, 0.8f, 0f, 1f, false);
                        if (Random.value < 0.3f) G.sfx.Play("swing", 0.15f, 2f, 0.2f);
                        if (shotT <= 0f)
                        {
                            G.fx.Burst(aim, 25, Ink, 5f, 0.4f, 0f);
                            G.sfx.Play("buy", 0.6f, 0.6f);
                            if (act == A.DrawGun) G.Spawn(new DrawnGun(aim, Dmg));
                            else DrawFoe(aim);
                            Go(S.Rest, 0.9f);
                        }
                        break;
                    }
            }
        }

        float sweepDir = 1f, sweepY, slamY;

        // where the hand lands, ink splashes out in a ring (there's no floor to run along out here)
        void Shockwave(Vector2 at)
        {
            Vector2 c = at + Vector2.down * HandHalf.y;
            int n = Phase2 ? 12 : 9;
            float off = Random.value * 30f;
            for (int k = 0; k < n; k++)
            {
                float a = (off + k * 360f / n) * Mathf.Deg2Rad;
                Ink1(c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6.5f, 0.6f);
            }
            G.fx.Burst(c, 20, Ink, 7f, 0.4f, 0f);
            G.fx.Ring(c, 2.5f, Ink);
        }

        void Ink1(Vector2 at, Vector2 v, float sc = 0.45f)
        {
            var b = Shot(at, v, Art.Circle, Ink, 0f, 0f, sc);
            b.body = Color.Lerp(Ink, Void, 0.25f);
            b.dmgOverride = Dmg;
        }

        // a whole swarm of creatures of the deep, drawn into being at once; every one of them comes straight for you
        void DrawFoe(Vector2 at)
        {
            int alive = 0;
            foreach (var e in G.ents) if (!e.dead && e is Enemy en && !(en is Boss) && !(en is CreatorHand)) alive++;
            int cap = Phase2 ? 40 : 28;
            if (alive >= cap) { G.Spawn(new DrawnGun(at + Vector2.up * 3f, Dmg)); return; }
            int n = Mathf.Min(cap - alive, Phase2 ? 14 : 10);
            int d = arena.Depth;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = at + new Vector2(Random.Range(-4f, 4f), Random.Range(0f, 3.5f));
                p.x = Mathf.Clamp(p.x, VoidStage.Min.x + 1f, VoidStage.Max.x - 1f);
                p.y = Mathf.Clamp(p.y, VoidStage.Min.y + 1f, VoidStage.Max.y - 1f);
                int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
                Enemy e2;
                switch (Random.Range(0, 6))   // things that fly: there's no floor out here
                {
                    case 0: e2 = new Bat(x, y, d, false); break;
                    case 1: e2 = new Wisp(x, y, d, true); break;
                    case 2: e2 = new Wisp(x, y, d, false); break;
                    case 3: e2 = new Bat(x, y, d, false); break;
                    case 4: e2 = new Drone(x, y, d); break;
                    default: e2 = new Bee(p, d); break;
                }
                e2.Enrage();   // drawn to hunt you: they know where you are, always
                G.Spawn(e2);
                G.fx.Burst(p, 6, Ink, 3f, 0.3f, 0f);
            }
        }

        // ---------------------------------------------------------------- looks

        void DrawHands()
        {
            DrawBoss(0f);
            if (dead) return;
            bool side = (S)state == S.Act && (act == A.Clap || act == A.Sweep || ((act == A.Slam || act == A.DoubleSlam) && slamming));
            var strip = Art.Strips.TryGetValue(side ? "creator_side" : "creator_hand", out var s) ? s : null;
            if (strip != null) sr.sprite = flash > 0f && Art.EnemyWhite.TryGetValue(side ? "creatorside" : "creatorhand", out var w) ? w[0] : strip[0];
            sr.flipX = true;   // (the drawing is a left hand as seen: mirrored, it's the right one)
            if (VoidStage.White && flash <= 0f) sr.color = new Color(0.32f, 0.28f, 0.38f);   // dark against the white
            if (flash > 0f) sr.color = HitRed;   // (a white flash vanishes against the white ink)
            float rot = 0f;
            if ((S)state == S.Act && act == A.Point) { Vector2 d = (P.pos - pos).normalized; rot = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 180f; }
            if ((S)state == S.Act && act == A.Clap) rot = 90f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, rot);
            left.Pose(side, strip, flash > 0f, (S)state == S.Act && act == A.Clap ? -90f : 0f);
        }

        public override void Kill()
        {
            base.Kill();
            if (left != null && !left.dead) left.Kill();
        }

        // Beaten: the hands shudder in slow motion and come apart in a long string of blasts, fading out, while the
        // whole screen slowly whitens. Then it's over. (You keep flying all through it.)
        bool dying;
        float dyingT, blastT, shudderT;
        Vector2 rBase, lBase, rOff, lOff;
        const float DyingTime = 5f;

        public override void Die()
        {
            if (dead) return;
            if (!dying)
            {
                dying = true; intangible = true; dyingT = 0f;
                rBase = pos; lBase = left.pos;
                hp = 0f;
                foreach (var e in G.ents)
                    if (!e.dead && (e is DrawnGun || e is BossShot || (e is Enemy en && !(en is Boss) && !(en is CreatorHand)))) { G.fx.Burst(e.pos, 8, Ink, 3f, 0.4f, 0f); e.Kill(); }
                G.music.Stop(4f);
                G.sfx.Play("death", 1f, 0.4f);
                Go(S.Dying, DyingTime);
                return;
            }
            if (dyingT < DyingTime) return;   // (poison and the like can't cut the end short)
            G.holdWhite = 1f;
            base.Die();
        }

        void TickDying(float dt)
        {
            dyingT += dt; blastT -= dt; shudderT -= dt;
            float k = Mathf.Clamp01(dyingT / DyingTime);
            // a slow, heavy shudder (a new offset only every so often: it reads as slow motion)
            if (shudderT <= 0f)
            {
                shudderT = 0.07f;
                float amp = 0.08f + 0.22f * k;
                rOff = Random.insideUnitCircle * amp; lOff = Random.insideUnitCircle * amp;
            }
            pos = rBase + rOff; left.pos = lBase + lOff;
            // blasts burst all over both hands
            if (blastT <= 0f)
            {
                blastT = Mathf.Lerp(0.32f, 0.1f, k);
                for (int i = 0; i < 2; i++)
                {
                    Vector2 c = (i == 0 ? pos : left.pos) + new Vector2(Random.Range(-HandHalf.x, HandHalf.x), Random.Range(-HandHalf.y, HandHalf.y));
                    G.fx.Explosion(c, Random.Range(0.8f, 1.6f));
                    G.fx.Burst(c, 12, Ink, 6f, 0.6f, 0f, 1.5f);
                }
                G.sfx.Play("explode", 0.55f, Random.Range(0.45f, 0.7f), 0.1f);
                G.cam.Shake(0.25f);
            }
            // fading out, and the screen whitening
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dyingT - 1f) / (DyingTime - 1.5f)));
            DrawHands();
            var c1 = sr.color; c1.a = fade; sr.color = c1;
            left.Fade(fade);
            G.holdWhite = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dyingT - 0.8f) / (DyingTime - 0.8f)));
            if (dyingT >= DyingTime) Die();
        }

        public override void AddLight(Lighting l) { l.Add(pos, 5f, 0.9f); if (left != null) l.Add(left.pos, 5f, 0.9f); }
    }

    // The Creator's left hand: a mirror of the right. Hitting it hurts the Creator.
    public class CreatorHand : Enemy
    {
        readonly Creator owner;
        protected override bool Harmless => true;   // (the Creator decides when its hands hurt)
        protected override bool CanBeElite => false;
        protected override bool LeavesCorpse => false;
        public override bool AlwaysActive => true;

        public CreatorHand(Creator c, Vector2 at)
        {
            owner = c;
            pos = at;
            half = new Vector2(1.8f, 1.1f);
            Setup("THE CREATOR", "creatorhand", 1, 1);
            team = Team.Boss;
            maxHp = hp = 1e9f;
            go.transform.localScale = new Vector3(3f, 3f, 1f);
            sr.sortingOrder = 12;
        }

        public override bool Hurt(float d, Vector2 knock)
        {
            if (owner == null || owner.dead || owner.Intangible) return false;
            flash = 0.08f;
            return owner.TakeShot(d, knock);
        }

        public void Fade(float a) { var c = sr.color; c.a = a; sr.color = c; }

        public void Pose(bool side, Sprite[] strip, bool white, float rot)
        {
            if (strip != null) sr.sprite = white && Art.EnemyWhite.TryGetValue(side ? "creatorside" : "creatorhand", out var w) ? w[0] : strip[0];
            sr.flipX = false;
            sr.color = white ? Creator.HitRed : VoidStage.White ? new Color(0.32f, 0.28f, 0.38f) : Color.white;
            go.transform.rotation = Quaternion.Euler(0f, 0f, rot);
        }

        public override void Tick(float dt)
        {
            flash -= dt;
            if (owner == null || owner.dead) { Kill(); return; }
            Place();
        }
    }

    // A gun sketched in the air by the Creator: it turns to you, fires a few times, then is rubbed out.
    public class DrawnGun : Entity
    {
        static Sprite sketch;
        float t, shotT = 0.9f;
        int shots = 4;
        readonly int dmg;
        public override bool AlwaysActive => true;

        public DrawnGun(Vector2 at, int dmg)
        {
            pos = at; this.dmg = dmg;
            if (sketch == null)
                sketch = Art.Make(new[]
                {
                    ".WWWWWWWWWWW",
                    "W..........W",
                    "W..WWWWWWWWW",
                    "W..W........",
                    "W..W........",
                    "WWWW........",
                }, new Dictionary<char, Color32> { { 'W', U.C(240, 240, 255) } }, false, true);
            MakeGO("DrawnGun", sketch, 16);
            go.transform.localScale = new Vector3(2f, 2f, 1f);
        }

        public override void Tick(float dt)
        {
            t += dt; shotT -= dt;
            var p = G.player;
            Vector2 d = (p.pos - pos).normalized;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            sr.flipY = d.x < 0f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            // it's drawn in (wobbly lines), then fires
            float a = Mathf.Clamp01(t * 2f);
            float al = shots > 0 ? a : Mathf.Max(0f, 1f - (t - lastT) * 2f);
            sr.color = VoidStage.White ? new Color(0.12f, 0.08f, 0.16f, al) : new Color(1f, 1f, 1f, al);
            if (Random.value < 0.3f) G.fx.Burst(pos + Random.insideUnitCircle * 0.6f, 1, new Color(0.9f, 0.9f, 1f), 0.3f, 0.4f, 0f, 1f, false);
            if (shots > 0 && shotT <= 0f)
            {
                shots--; shotT = 0.55f;
                var ic = VoidStage.White ? new Color(0.12f, 0.08f, 0.16f) : new Color(0.95f, 0.95f, 1f);
                var b = new BossShot(pos + d * 1f, d * 10f, Art.Circle, ic, 0f, 0f, 0.4f);
                b.dmgOverride = dmg; b.body = ic;
                G.Spawn(b);
                G.sfx.Play("pistol", 0.5f, 1.3f);
                G.fx.Muzzle(pos + d * 1f, 0.6f, Color.white);
                if (shots == 0) lastT = t;
            }
            if (shots == 0 && t - lastT > 0.5f) { G.fx.Burst(pos, 14, Color.white, 3f, 0.4f, 0f); Kill(); return; }
            Place();
        }

        float lastT;
    }

    // Something hit by the void's wave: it jumps to 1.5x its size, then shrinks to nothing, fast.
    public class Popper : Entity
    {
        float t;
        readonly float baseScale;
        const float Life = 0.28f;
        public override bool AlwaysActive => true;

        public Popper(Vector2 at, Sprite s, float scale, bool flip = false, Quaternion rot = default)
        {
            pos = at; baseScale = scale;
            MakeGO("Pop", s, 14);
            sr.flipX = flip;
            if (rot.w != 0f || rot.x != 0f || rot.y != 0f || rot.z != 0f) go.transform.rotation = rot;
            go.transform.localScale = Vector3.one * scale;
        }

        public override void Tick(float dt)
        {
            t += dt;
            float k = t / Life;
            if (k >= 1f) { Kill(); return; }
            float sc = k < 0.3f ? Mathf.Lerp(1f, 1.5f, k / 0.3f) : Mathf.Lerp(1.5f, 0f, (k - 0.3f) / 0.7f);
            go.transform.localScale = Vector3.one * baseScale * sc;
        }
    }
}
