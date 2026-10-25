using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // =====================================================================================
    // THE GOD ABOVE THE CLOUDS. Once every digger has found the God-Diamond, touching it lifts you up here.
    // A robed figure against the stars, arms spread, a blazing sunburst for a head. Only the head can be hurt;
    // touching the god does nothing. Every attack is signalled by what the arms and hands do first:
    //   hands raised high      -> STARFALL: stars rain down from the top of the sky
    //   arms spread wide       -> HALO: rings of light pour out of the head
    //   one hand points at you -> JUDGMENT: pillars of light come down where you are
    //   hands apart at your height, then a clap -> a band of light across the sky
    //   palms pressed down     -> TREMOR: the hands slam and burst into rings
    //   both hands to one side -> SWEEP: the hands drag across the sky, raining light behind them (half health)
    //   arms spread, palms up, head flaring -> SPIRAL: a spinning storm from the head (near the end)
    // =====================================================================================
    public class SkyGod : Boss
    {
        public static Vector2 Center => new Vector2(World.W * 0.5f, 600f);

        enum Pose { Rest, Up, Spread, Point, ClapOpen, ClapShut, Down, SweepL, SweepR, Offer }
        enum S { Idle, Tele, Act }
        const int Starfall = 0, Halo = 1, Judgment = 2, Clap = 3, Tremor = 4, Sweep = 5, Spiral = 6;
        // the second half of the fight: the god's void self, with moves of its own
        const int Rift = 7, Vortex = 8, VoidRain = 9, Mirror = 10;
        bool voidForm, transforming;
        float transT;
        Vector2 vortexAt;
        static readonly Color VoidL = new Color(0.78f, 0.5f, 1f), VoidD = new Color(0.35f, 0.12f, 0.6f);

        Pose pose = Pose.Rest;
        int move = -1, lastMove = -1, left;
        float shotT, idleShotT = 2f, spin;
        int pointSide = 1, sweepDir = 1;
        float clapY;
        readonly Vector2[] hand = new Vector2[2];
        readonly GameObject[] handGo = new GameObject[2];
        readonly SpriteRenderer[] handSr = new SpriteRenderer[2];
        readonly SpriteRenderer[,] arm = new SpriteRenderer[2, 4];   // sleeve outline, sleeve, forearm outline, forearm
        GameObject robeGo;
        SpriteRenderer robeSr;

        const int Dmg = 110;
        static readonly Color Gold = new Color(1f, 0.85f, 0.3f), Light = new Color(1f, 0.96f, 0.75f), Sky = new Color(0.6f, 0.8f, 1f);
        static readonly Color Cream = new Color(0.925f, 0.87f, 0.81f), Fold = new Color(0.55f, 0.45f, 0.38f), Blue = new Color(0.51f, 0.7f, 0.84f), BlueD = new Color(0.25f, 0.38f, 0.5f);

        protected override bool Harmless => true;   // touching the god doesn't hurt
        protected override bool CanBeElite => false;
        float HalfW => G.cam.halfW;
        float HalfH => G.cam.halfH;
        bool Phase3 => hp < maxHp * 0.25f;

        public SkyGod()
        {
            pos = Center + new Vector2(0f, 3.4f);
            half = new Vector2(0.85f, 0.85f);
            Setup("THE GOD", "godhead", 1, 400);
            team = Team.Boss;
            maxHp = hp = 16000f;
            scale = 1.35f;
            title = "THE GOD ABOVE THE CLOUDS";
            subtitle = "";
            gold = 500;
            blood = Gold;
            sr.sortingOrder = 13;

            robeGo = new GameObject("GodRobe");
            robeGo.transform.SetParent(G.runRoot, false);
            robeSr = Art.AddSR(robeGo, Art.Enemy["godrobe"][0], 4);
            robeGo.transform.localScale = new Vector3(2.8f, 2.8f, 1f);
            for (int i = 0; i < 2; i++)
            {
                for (int k = 0; k < 4; k++)
                {
                    var a = new GameObject("GodArm");
                    a.transform.SetParent(G.runRoot, false);
                    arm[i, k] = Art.AddSR(a, Art.Square, 5 + k);
                    arm[i, k].color = k == 0 ? Fold : k == 1 ? Cream : k == 2 ? BlueD : Blue;
                }
                handGo[i] = new GameObject("GodHand");
                handGo[i].transform.SetParent(G.runRoot, false);
                handSr[i] = Art.AddSR(handGo[i], Art.Enemy["godhand"][0], 9);
                handGo[i].transform.localScale = new Vector3(2.2f, 2.2f, 1f);
                hand[i] = Shoulder(i) + new Vector2(Side(i) * 3f, -3f);
            }
            state = (int)S.Idle; stateT = 2.5f;
        }

        static int Side(int i) => i == 0 ? -1 : 1;
        Vector2 Shoulder(int i) => pos + new Vector2(Side(i) * 1.7f, -1.5f);

        // ---------------------------------------------------------------- where the hands go for each pose
        Vector2 HandTarget(int i)
        {
            int s = Side(i);
            Vector2 h = pos;
            switch (pose)
            {
                case Pose.Up: return h + new Vector2(s * 3.2f, 2.4f + Mathf.Sin(t * 6f) * 0.2f);
                case Pose.Spread: return h + new Vector2(s * Mathf.Min(8f, HalfW - 2f), -0.6f);
                case Pose.Offer: return h + new Vector2(s * Mathf.Min(7f, HalfW - 2.5f), 0.6f + Mathf.Sin(t * 8f + i) * 0.3f);
                case Pose.Point:
                    if (s == pointSide)
                    {
                        Vector2 d = (P.pos - Shoulder(i)).normalized;
                        return Shoulder(i) + d * 6.5f;
                    }
                    return h + new Vector2(s * 3f, -4f);
                case Pose.ClapOpen: return new Vector2(Center.x + s * Mathf.Min(10f, HalfW - 1.5f), clapY);
                case Pose.ClapShut: return new Vector2(Center.x + s * 0.7f, clapY);
                case Pose.Down: return h + new Vector2(s * 4.5f, -5.5f);
                case Pose.SweepL: case Pose.SweepR:
                    {
                        float sx = pose == Pose.SweepL ? -1f : 1f;
                        return new Vector2(Center.x + sx * (HalfW - 2f), Center.y + (i == 0 ? 2.5f : -3.5f));
                    }
                default: return h + new Vector2(s * 4.2f, -3.6f + Mathf.Sin(t * 1.5f + i * 2f) * 0.3f);
            }
        }

        float HandSpeed => pose == Pose.ClapShut ? 18f : (pose == Pose.SweepL || pose == Pose.SweepR) && (S)state == S.Act ? 2.2f : Phase2 ? 7f : 5.5f;

        // ---------------------------------------------------------------- the fight
        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            if (!voidForm && !transforming && hp < maxHp * 0.5f)
            {
                // half way: the light cracks and the void underneath shows through
                transforming = true; transT = 0f; intangible = true;
                foreach (var e in G.ents) if (!e.dead && (e is BossShot || e is Hazard)) e.Kill();
                G.sfx.Play("rumble", 1f, 0.35f);
                G.cam.Shake(1f);
            }
            if (transforming)
            {
                transT += dt;
                pose = Pose.Up;
                float k = Mathf.Clamp01(transT / 3f);
                G.cam.Shake(0.08f);
                for (int n = 0; n < 3; n++)
                    G.fx.Burst(pos + Random.insideUnitCircle * (6f - k * 5f), 1, Color.Lerp(Light, VoidL, k), 3f, 0.6f, 0f, 1.5f, false);
                if (Random.value < 0.08f) { flash = 0.05f; G.sfx.Play("zap", 0.5f, 0.4f); }
                tint = Color.Lerp(Color.white, new Color(0.55f, 0.35f, 0.8f), k);
                for (int i = 0; i < 2; i++)
                {
                    arm[i, 0].color = Color.Lerp(Fold, new Color(0.1f, 0.03f, 0.18f), k);
                    arm[i, 1].color = Color.Lerp(Cream, new Color(0.25f, 0.1f, 0.42f), k);
                    arm[i, 2].color = Color.Lerp(BlueD, new Color(0.08f, 0.02f, 0.14f), k);
                    arm[i, 3].color = Color.Lerp(Blue, new Color(0.45f, 0.22f, 0.8f), k);
                }
                if (robeSr != null) robeSr.color = Color.Lerp(Color.white, new Color(0.35f, 0.2f, 0.55f), k);
                if (transT >= 3f)
                {
                    transforming = false; voidForm = true; intangible = false;
                    G.hud.ScreenFlash(0.7f);
                    G.fx.Ring(pos, 6f, VoidL);
                    G.sfx.Play("explode", 1f, 0.5f);
                    Go(S.Idle, 1f);
                }
                for (int i = 0; i < 2; i++) hand[i] = Vector2.Lerp(hand[i], HandTarget(i), 1f - Mathf.Exp(-dt * 4f));
                DrawBoss(0f);
                spin += dt * 120f * k;
                go.transform.rotation = Quaternion.Euler(0f, 0f, spin);
                DrawBody();
                return;
            }
            // the head drifts slowly over the robe
            Vector2 want = Center + new Vector2(Mathf.Sin(t * 0.35f) * 3.5f, 3.4f + Mathf.Sin(t * 0.8f) * 0.35f);
            pos = Vector2.Lerp(pos, want, 1f - Mathf.Exp(-dt * 2f));

            switch ((S)state)
            {
                case S.Idle:
                    pose = Pose.Rest;
                    if (Phase2)
                    {
                        // from half health on, the head spits aimed light between attacks
                        idleShotT -= dt;
                        if (idleShotT <= 0f)
                        {
                            idleShotT = Phase3 ? 0.8f : 1.2f;
                            Vector2 d = (P.pos - pos).normalized;
                            for (int k = -1; k <= 1; k++) Fire(pos, U.Rot(d, k * 14f) * 7f, Light);
                            G.sfx.Play("shoot", 0.5f, 1.6f);
                        }
                    }
                    if (stateT <= 0f) Choose();
                    break;
                case S.Tele:
                    // the hands glow while they get into place: something is coming
                    for (int i = 0; i < 2; i++) if (Random.value < 0.5f) G.fx.Burst(hand[i], 1, Gold, 2f, 0.4f, 0f);
                    if (stateT <= 0f) Begin();
                    break;
                case S.Act:
                    Act(dt);
                    break;
            }

            // arms and hands follow the pose
            for (int i = 0; i < 2; i++) hand[i] = Vector2.Lerp(hand[i], HandTarget(i), 1f - Mathf.Exp(-dt * HandSpeed));
            DrawBoss(0f);
            if (dead) return;
            spin += dt * (Phase3 ? 40f : 14f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, spin);
            DrawBody();
        }

        void Go(S s, float time) { state = (int)s; stateT = time; }

        void Choose()
        {
            var opts = new List<int> { Starfall, Halo, Judgment, Clap, Tremor };
            if (Phase2) opts.Add(Sweep);
            if (Phase3) { opts.Add(Spiral); opts.Add(Spiral); }
            if (voidForm) { opts = new List<int> { Rift, Vortex, VoidRain, Mirror, Clap }; if (Phase3) opts.Add(Spiral); }
            opts.Remove(lastMove);
            move = opts[Random.Range(0, opts.Count)];
            lastMove = move;
            float tele = Phase3 ? 0.65f : Phase2 ? 0.8f : 1f;
            switch (move)
            {
                case Starfall: pose = Pose.Up; break;
                case Halo: pose = Pose.Spread; break;
                case Judgment: pose = Pose.Point; pointSide = P.pos.x < pos.x ? -1 : 1; break;
                case Clap: pose = Pose.ClapOpen; clapY = P.pos.y; break;
                case Tremor: pose = Pose.Down; break;
                case Sweep: sweepDir = P.pos.x < Center.x ? 1 : -1; pose = sweepDir > 0 ? Pose.SweepL : Pose.SweepR; tele += 0.4f; break;
                case Spiral: pose = Pose.Offer; break;
                case Rift: pose = Pose.Spread; break;
                case Vortex: pose = Pose.Offer; vortexAt = new Vector2(Center.x + (P.pos.x < Center.x ? 1f : -1f) * Random.Range(4f, HalfW - 3f), Center.y + Random.Range(-HalfH + 3f, HalfH - 3f)); break;
                case VoidRain: pose = Pose.Up; break;
                case Mirror: pose = Pose.Spread; break;
            }
            G.sfx.Play("rumble", 0.5f, 1.4f);
            Go(S.Tele, tele);
        }

        void Begin()
        {
            switch (move)
            {
                case Starfall: left = Phase3 ? 34 : Phase2 ? 26 : 20; shotT = 0f; Go(S.Act, 9f); break;
                case Halo: left = Phase3 ? 5 : Phase2 ? 4 : 3; shotT = 0f; Go(S.Act, 9f); break;
                case Judgment: left = Phase3 ? 5 : Phase2 ? 4 : 3; shotT = 0f; Go(S.Act, 9f); break;
                case Clap: left = Phase3 ? 3 : Phase2 ? 2 : 1; shotT = 0.05f; Go(S.Act, 9f); break;
                case Tremor: left = Phase3 ? 3 : 2; shotT = 0.15f; pose = Pose.Down; Go(S.Act, 9f); hand[0] += Vector2.up * 1.5f; hand[1] += Vector2.up * 1.5f; break;
                case Sweep: pose = sweepDir > 0 ? Pose.SweepR : Pose.SweepL; shotT = 0f; Go(S.Act, 3.2f); break;
                case Spiral: shotT = 0f; Go(S.Act, Phase3 ? 3.6f : 3f); G.sfx.Play("rumble", 0.9f, 0.6f); break;
                case Rift: left = Phase3 ? 4 : 3; shotT = 0f; Go(S.Act, 9f); break;
                case Vortex: shotT = 0f; Go(S.Act, Phase3 ? 4.2f : 3.6f); G.sfx.Play("rumble", 1f, 0.4f); break;
                case VoidRain: left = Phase3 ? 40 : 30; shotT = 0f; Go(S.Act, 9f); break;
                case Mirror: left = Phase3 ? 5 : 4; shotT = 0f; Go(S.Act, 9f); break;
            }
        }

        void Done(float rest) { pose = Pose.Rest; Go(S.Idle, Phase3 ? rest * 0.6f : Phase2 ? rest * 0.8f : rest); }

        float Top => Center.y + HalfH - 0.3f;

        void Act(float dt)
        {
            shotT -= dt;
            switch (move)
            {
                case Starfall:
                    if (shotT <= 0f && left > 0)
                    {
                        left--; shotT = 0.09f;
                        float x = Center.x + Random.Range(-HalfW + 0.8f, HalfW - 0.8f);
                        Fire(new Vector2(x, Top), new Vector2(Random.Range(-1f, 1f), -2f), Light, 12f, 0.45f, 0.55f);
                    }
                    if (left <= 0 && shotT <= -0.6f) Done(1.3f);
                    break;
                case Halo:
                    if (shotT <= 0f && left > 0)
                    {
                        left--; shotT = 0.6f;
                        int n = Phase3 ? 22 : Phase2 ? 18 : 14;
                        float off = t * 47f;
                        for (int i = 0; i < n; i++)
                        {
                            float a = (off + i * 360f / n) * Mathf.Deg2Rad;
                            Fire(pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4.6f, Gold);
                        }
                        G.sfx.Play("shoot", 0.7f, 0.8f);
                        G.fx.Ring(pos, 2f, Gold);
                    }
                    if (left <= 0 && shotT <= 0f) Done(1.3f);
                    break;
                case Judgment:
                    if (shotT <= 0f && left > 0)
                    {
                        left--; shotT = Phase3 ? 0.35f : 0.45f;
                        float x = Mathf.Clamp(P.pos.x + P.vel.x * 0.25f, Center.x - HalfW + 0.7f, Center.x + HalfW - 0.7f);
                        G.Spawn(new Hazard(new Vector2(x, Center.y), new Vector2(0.75f, HalfH + 1f), 0.75f, 0.35f, Gold, Dmg));
                        G.sfx.Play("blip", 0.5f, 0.6f);
                    }
                    if (left <= 0 && shotT <= -0.8f) Done(1.2f);
                    break;
                case Clap:
                    if (pose == Pose.ClapOpen && shotT <= 0f)
                    {
                        clapY = Mathf.Clamp(P.pos.y, Center.y - HalfH + 0.8f, Center.y + HalfH - 1f);
                        pose = Pose.ClapShut; shotT = 0.3f;
                        G.Spawn(new Hazard(new Vector2(Center.x, clapY), new Vector2(HalfW + 1f, 0.7f), 0.3f, 0.35f, Gold, Dmg));
                    }
                    else if (pose == Pose.ClapShut && shotT <= 0f)
                    {
                        G.sfx.Play("explode", 0.8f, 1.4f);
                        G.cam.Shake(0.35f);
                        G.fx.Burst(new Vector2(Center.x, clapY), 30, Light, 9f, 0.5f, 0f);
                        for (int k = 0; k < 8; k++)
                        {
                            float a = (k * 45f + 22f) * Mathf.Deg2Rad;
                            Fire(new Vector2(Center.x, clapY), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5f, Light);
                        }
                        left--;
                        if (left > 0) { pose = Pose.ClapOpen; clapY = P.pos.y; shotT = Phase3 ? 0.45f : 0.6f; }
                        else Done(1.4f);
                    }
                    break;
                case Tremor:
                    if (shotT <= 0f && left > 0)
                    {
                        left--; shotT = 0.75f;
                        G.cam.Shake(0.4f);
                        G.sfx.Play("explode", 0.7f, 0.8f);
                        for (int i = 0; i < 2; i++)
                        {
                            int n = Phase2 ? 14 : 11;
                            float off = Random.value * 30f;
                            for (int k = 0; k < n; k++)
                            {
                                float a = (off + k * 360f / n) * Mathf.Deg2Rad;
                                Fire(hand[i], new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3.8f, Sky);
                            }
                            G.fx.Ring(hand[i], 1.6f, Sky);
                            hand[i] += Vector2.up * 1.6f;   // the hands lift for the next slam
                        }
                    }
                    if (left <= 0 && shotT <= 0f) Done(1.3f);
                    break;
                case Sweep:
                    // the hands drag across the sky, raining light behind them
                    if (shotT <= 0f)
                    {
                        shotT = Phase3 ? 0.06f : 0.08f;
                        for (int i = 0; i < 2; i++) Fire(hand[i], new Vector2(0f, -1.5f), Light, 5f, 0f, 0.5f);
                    }
                    if (stateT <= 0f) Done(1.2f);
                    break;
                case Rift:
                    // the sky splits in columns of void; the next set falls between the last
                    if (shotT <= 0f && left > 0)
                    {
                        shotT = 0.85f;
                        float off = (left % 2) * 1.6f;
                        for (float x = Center.x - HalfW + 0.8f + off; x < Center.x + HalfW; x += 3.2f)
                            G.Spawn(new Hazard(new Vector2(x, Center.y), new Vector2(0.55f, HalfH + 1f), 0.7f, 0.3f, VoidL, Dmg));
                        left--;
                        G.sfx.Play("blip", 0.5f, 0.5f);
                    }
                    if (left <= 0 && shotT <= -0.5f) Done(1.2f);
                    break;
                case Vortex:
                    {
                        // a hole in the sky drags you in and spins out void
                        Vector2 d = vortexAt - P.pos;
                        if (d.magnitude > 0.5f) P.vel += d.normalized * Mathf.Clamp(28f / Mathf.Max(1.5f, d.magnitude), 1f, 9f) * dt * 6f;
                        for (int n = 0; n < 3; n++)
                        {
                            float a = Random.value * 6.283f, r = Random.Range(0.5f, 3f);
                            G.fx.Burst(vortexAt + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, 1, n == 0 ? VoidD : VoidL, 1f, 0.4f, 0f, 1f, false);
                        }
                        if (shotT <= 0f)
                        {
                            shotT = Phase3 ? 0.1f : 0.14f;
                            float a = t * 220f * Mathf.Deg2Rad;
                            Fire(vortexAt, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4f, VoidL);
                        }
                        if ((P.pos - vortexAt).magnitude < 0.9f) P.Hurt(Dmg, vortexAt);
                        if (stateT <= 0f) Done(1.3f);
                        break;
                    }
                case VoidRain:
                    // black rain, slanting across the sky
                    if (shotT <= 0f && left > 0)
                    {
                        left--; shotT = 0.07f;
                        float x = Center.x + Random.Range(-HalfW, HalfW);
                        Fire(new Vector2(x, Top), new Vector2(4f, -3f), VoidD, 9f, 0.25f, 0.4f);
                    }
                    if (left <= 0 && shotT <= -0.6f) Done(1.2f);
                    break;
                case Mirror:
                    // shots step out of the edges of the sky, all aimed at you
                    if (shotT <= 0f && left > 0)
                    {
                        left--; shotT = 0.75f;
                        for (int k = 0; k < 6; k++)
                        {
                            Vector2 at;
                            switch (k % 4)
                            {
                                case 0: at = new Vector2(Center.x - HalfW + 0.5f, Center.y + Random.Range(-HalfH, HalfH)); break;
                                case 1: at = new Vector2(Center.x + HalfW - 0.5f, Center.y + Random.Range(-HalfH, HalfH)); break;
                                case 2: at = new Vector2(Center.x + Random.Range(-HalfW, HalfW), Top); break;
                                default: at = new Vector2(Center.x + Random.Range(-HalfW, HalfW), Center.y - HalfH + 0.5f); break;
                            }
                            Fire(at, (P.pos - at).normalized * 5.5f, VoidL, 0f, 0.45f);
                        }
                        G.sfx.Play("shoot", 0.6f, 0.5f);
                    }
                    if (left <= 0 && shotT <= -0.4f) Done(1.2f);
                    break;
                case Spiral:
                    if (shotT <= 0f)
                    {
                        shotT = 0.05f;
                        float a0 = t * 260f;
                        for (int k = 0; k < 3; k++)
                        {
                            float a = (a0 + k * 120f) * Mathf.Deg2Rad;
                            Fire(pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5.2f, k == 0 ? Light : Gold);
                        }
                    }
                    flash = ((int)(t * 20f)) % 2 == 0 ? 0.02f : 0f;
                    if (stateT <= 0f) Done(1.6f);
                    break;
            }
        }

        // a ball of light: drawn bigger than it hits
        void Fire(Vector2 at, Vector2 v, Color c, float grav = 0f, float delay = 0f, float sc = 0.45f)
        {
            if (voidForm) c = Color.Lerp(c, VoidL, 0.75f);
            var b = Shot(at, v, Art.Circle, c, grav, delay, sc);
            b.dmgOverride = Dmg;
            b.body = c;
        }

        // ---------------------------------------------------------------- the body
        void DrawBody()
        {
            // the robe hangs from just under the head
            if (robeGo != null)
            {
                Vector2 rp = new Vector2(pos.x * 0.6f + Center.x * 0.4f, pos.y - 5.65f);
                robeGo.transform.position = new Vector3(U.Snap(rp.x), U.Snap(rp.y), 0f);
            }
            for (int i = 0; i < 2; i++)
            {
                Vector2 sh = Shoulder(i), hd = hand[i];
                Vector2 mid = (sh + hd) * 0.5f;
                Vector2 perp = new Vector2(-(hd - sh).y, (hd - sh).x).normalized;
                if (perp.y > 0f) perp = -perp;   // elbows bend downwards
                Vector2 elbow = mid + perp * Mathf.Clamp(3.5f - (hd - sh).magnitude * 0.3f, 0.2f, 1.6f);
                Seg(arm[i, 0], sh, elbow, 1.15f);
                Seg(arm[i, 1], sh, elbow, 0.95f);
                Seg(arm[i, 2], elbow, hd, 0.62f);
                Seg(arm[i, 3], elbow, hd, 0.45f);
                // the hand: fingers along the forearm, a pointing hand during JUDGMENT / closed for TREMOR
                bool alt = (pose == Pose.Point && Side(i) == pointSide) || pose == Pose.Down || pose == Pose.ClapShut;
                handSr[i].sprite = Art.Enemy["godhand"][alt ? 1 : 0];
                handSr[i].flipX = Side(i) < 0;
                Vector2 dir = (hd - elbow).normalized;
                handGo[i].transform.position = new Vector3(U.Snap(hd.x), U.Snap(hd.y), 0f);
                handGo[i].transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
                Color hc = voidForm || transforming ? Color.Lerp(Color.white, new Color(0.4f, 0.22f, 0.65f), voidForm ? 1f : Mathf.Clamp01(transT / 3f)) : Color.white;
                handSr[i].color = (S)state == S.Tele ? Color.Lerp(hc, voidForm ? VoidL : Gold, 0.5f + 0.5f * Mathf.Sin(t * 20f)) : hc;
            }
        }

        static void Seg(SpriteRenderer r, Vector2 a, Vector2 b, float thick)
        {
            Vector2 d = b - a;
            r.transform.position = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, 0f);
            r.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            r.transform.localScale = new Vector3(d.magnitude + thick * 0.5f, thick, 1f);
        }

        public override void Kill()
        {
            base.Kill();
            if (robeGo != null) Object.Destroy(robeGo);
            for (int i = 0; i < 2; i++)
            {
                if (handGo[i] != null) Object.Destroy(handGo[i]);
                for (int k = 0; k < 4; k++) if (arm[i, k] != null) Object.Destroy(arm[i, k].gameObject);
            }
        }

        public override void Die()
        {
            if (dead) return;
            // the light goes out of it all at once
            for (int i = 0; i < 2; i++) G.fx.Burst(hand[i], 30, Gold, 8f, 1f, 0f, 2f);
            G.fx.Burst(pos + Vector2.down * 4f, 60, Cream, 9f, 1.2f, 0f, 2f);
            G.cam.Shake(1f);
            base.Die();
        }

        protected override void OnDefeatedFree() => G.OnGodDefeated();

        public override void AddLight(Lighting l) { l.Add(pos, 6f, 1f); }
    }

    // The sky behind the god: stars over deep night, and a sea of clouds below.
    public class SkyBackdrop : Entity
    {
        readonly GameObject clouds;
        float t;
        static Sprite starSprite, cloudSprite;

        public override bool AlwaysActive => true;

        public SkyBackdrop()
        {
            if (starSprite == null) Build();
            pos = SkyGod.Center;
            MakeGO("Stars", starSprite, -200);
            clouds = new GameObject("Clouds");
            clouds.transform.SetParent(G.runRoot, false);
            Art.AddSR(clouds, cloudSprite, -199);
        }

        static void Build()
        {
            int w = 900, h = 480;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            var rnd = new System.Random(7);
            Color top = new Color(0.02f, 0.02f, 0.07f), bot = new Color(0.12f, 0.07f, 0.2f);
            for (int y = 0; y < h; y++)
            {
                float k = 1f - y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float kk = k + ((x + y) % 2 == 0 ? 0.02f : -0.02f);
                    px[y * w + x] = Color.Lerp(top, bot, Mathf.Clamp01(kk * kk));
                }
            }
            for (int i = 0; i < 520; i++)
            {
                int x = rnd.Next(w), y = rnd.Next(h);
                double r = rnd.NextDouble();
                Color32 c = r < 0.08 ? new Color32(255, 240, 200, 255) : r < 0.4 ? new Color32(190, 190, 230, 255) : new Color32(110, 110, 160, 255);
                px[y * w + x] = c;
                if (r < 0.04)   // a few bright ones with a little cross
                {
                    if (x > 0) px[y * w + x - 1] = new Color32(140, 130, 120, 255);
                    if (x < w - 1) px[y * w + x + 1] = new Color32(140, 130, 120, 255);
                    if (y > 0) px[(y - 1) * w + x] = new Color32(140, 130, 120, 255);
                    if (y < h - 1) px[(y + 1) * w + x] = new Color32(140, 130, 120, 255);
                }
            }
            tex.SetPixels32(px); tex.Apply();
            starSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Art.PPU, 0, SpriteMeshType.FullRect);

            // the cloud sea: lumpy tops, lit rims, darker bellies
            int cw = 900, ch = 72;
            var ct = new Texture2D(cw, ch, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var cp = new Color32[cw * ch];
            Color32 rim = new Color32(250, 244, 255, 255), body = new Color32(196, 186, 226, 255), belly = new Color32(128, 112, 170, 255);
            for (int x = 0; x < cw; x++)
            {
                float hgt = 40f + Mathf.Sin(x * 0.045f) * 8f + Mathf.Sin(x * 0.11f + 1.3f) * 5f + Mathf.Abs(Mathf.Sin(x * 0.23f)) * 6f;
                int top2 = Mathf.Clamp(Mathf.RoundToInt(hgt), 0, ch - 1);
                for (int y = 0; y <= top2; y++)
                {
                    Color32 c = y >= top2 - 1 ? rim : y > top2 - 9 ? body : ((x + y) % 2 == 0 && y > top2 - 13) ? body : belly;
                    cp[y * cw + x] = c;
                }
            }
            ct.SetPixels32(cp); ct.Apply();
            cloudSprite = Sprite.Create(ct, new Rect(0, 0, cw, ch), new Vector2(0.5f, 0f), Art.PPU, 0, SpriteMeshType.FullRect);
        }

        public override void Tick(float dt)
        {
            t += dt;
            var cam = G.cam;
            go.transform.position = new Vector3(U.Snap(cam.pos.x), U.Snap(cam.pos.y), 0f);
            clouds.transform.position = new Vector3(U.Snap(cam.pos.x + Mathf.Sin(t * 0.1f) * 3f), U.Snap(cam.pos.y - cam.halfH - 0.6f), 0f);
            if (clouds.activeSelf != World.SkyMode) clouds.SetActive(World.SkyMode);   // the cloud sea is below you only once you're above it
            if (!World.SkyMode && !Ascent.Flying) Kill();
        }

        public override void Kill()
        {
            base.Kill();
            if (clouds != null) Object.Destroy(clouds);
        }
    }

    // =====================================================================================
    // THE ASCENT. With every digger's God-Diamond found, the white fades and you're back in Ornin's hall with the
    // power of flight, and an arrow pointing up. Fly up and you shoot upwards, fast, inside an invisible sphere
    // that wipes out every block it touches (even the void) and keeps you from harm: up through the whole world,
    // through the dark-matter crust to the surface (where the run is saved), on up into the sky and through a
    // thick bank of cloud... and out above it, where the God waits.
    // =====================================================================================
    public class Ascent : Entity
    {
        public static Ascent Active;
        public enum St { Ready, Rising }
        public St st;
        float speed, startY, pulse;
        bool saved;
        readonly GameObject arrow;
        readonly SpriteRenderer arrowSr;
        public const float Radius = 1.8f;
        public static float CloudY => Gen.SurfaceTop + 48f;   // the cloud bank
        public static float Cover;                             // how much of the screen the clouds hide (0-1)

        public override bool AlwaysActive => true;
        public static bool Flying => Active != null && !Active.dead;
        public static bool Shielded => Flying && Active.st == St.Rising;

        public Ascent(bool fromSurfaceSave)
        {
            Active = this;
            saved = fromSurfaceSave;
            st = St.Ready;
            startY = G.player.pos.y;
            arrow = new GameObject("AscentArrow");
            arrow.transform.SetParent(G.runRoot, false);
            arrowSr = Art.AddSR(arrow, Art.Arrow, 22);
            arrowSr.color = new Color(1f, 0.85f, 0.35f);
            arrow.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            arrow.transform.localScale = new Vector3(2f, 2f, 1f);
            Cover = 0f;
        }

        public override void Tick(float dt)
        {
            var p = G.player;
            if (p == null || p.mode == Player.Mode.Dead) return;
            pulse += dt;
            pos = p.pos;
            switch (st)
            {
                case St.Ready:
                    // the arrow bobs over you: up!
                    arrow.SetActive(true);
                    arrow.transform.position = new Vector3(U.Snap(p.pos.x), U.Snap(p.pos.y + 1.6f + Mathf.Abs(Mathf.Sin(pulse * 4f)) * 0.4f), 0f);
                    G.hud.Label(p.pos + Vector2.up * 2.6f, "FLY UP", new Color(1f, 0.85f, 0.35f));
                    if (GameInput.Held(Btn.Up) || p.pos.y > startY + 1.2f)
                    {
                        st = St.Rising;
                        speed = 6f;
                        G.sfx.Play("rumble", 1f, 0.6f);
                        G.sfx.Play("dash", 0.8f, 0.5f);
                        G.cam.Shake(0.5f);
                    }
                    break;
                case St.Rising:
                    arrow.SetActive(false);
                    float above = p.pos.y - Gen.SurfaceTop;
                    // up through the world at a tremendous speed; it eases off for the clouds
                    float top = above < 6f ? 46f : Mathf.Lerp(46f, 16f, Mathf.InverseLerp(6f, 30f, above));
                    speed = Mathf.MoveTowards(speed, top, 60f * dt);
                    p.ascentSpeed = speed;
                    Bore(p.pos);
                    if (Random.value < 0.3f) G.cam.Shake(0.08f);
                    if (!saved && p.pos.y > Gen.SurfaceTop + 1f)
                    {
                        // out in the open air for the first time: this is where a quit run comes back to
                        saved = true;
                        G.SaveAscentCheckpoint(new Vector2(p.pos.x, Gen.SurfaceTop + 1f + Player.Half.y + 0.05f));
                        G.hud.Banner("THE SURFACE", "THE SKY OPENS UP", new Color(0.7f, 0.9f, 1f));
                        G.fx.Burst(p.pos, 30, new Color(0.9f, 0.85f, 0.7f), 9f, 0.8f, 20f);
                    }
                    // the cloud bank: it swallows the whole screen for a moment
                    Cover = Mathf.InverseLerp(CloudY - 12f, CloudY - 4f, p.pos.y);
                    if (p.pos.y >= CloudY + 4f)
                    {
                        p.ascentSpeed = 0f;
                        Kill();
                        G.StartSkyFight(true);
                    }
                    break;
            }
        }

        // the sphere: every block it touches is gone, whatever it is
        static void Bore(Vector2 c)
        {
            int r = Mathf.CeilToInt(Radius);
            int cx = Mathf.FloorToInt(c.x), cy = Mathf.FloorToInt(c.y);
            for (int y = cy - r; y <= cy + r + 1; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= World.W) continue;
                    float dx = x + 0.5f - c.x, dy = y + 0.5f - c.y;
                    if (dx * dx + dy * dy > Radius * Radius) continue;
                    Block b = Wd.Get(x, y);
                    if (b == Block.Air) continue;
                    Wd.Set(x, y, Block.Air);
                    if (Random.value < 0.15f) G.fx.Burst(new Vector2(x + 0.5f, y + 0.5f), 2, new Color(1f, 0.9f, 0.6f), 5f, 0.3f, 10f);
                }
        }

        public override void Kill()
        {
            base.Kill();
            if (Active == this) Active = null;
            if (arrow != null) Object.Destroy(arrow);
            if (G.player != null) G.player.ascentSpeed = 0f;
        }
    }
}
