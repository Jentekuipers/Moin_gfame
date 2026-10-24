using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DigDeep
{
    // What kind of hit reached an altar. Weapon altars only respond to the matching one.
    public enum Src { Pick, Bolt, Explosion, Dash, Pound, Shockwave, Hook, Laser, Weapon }

    public enum PuzzleType { Boulder, Gold, Meditate, Race, Altar }

    // =====================================================================================
    // Chambers carved into the terrain for each puzzle
    // =====================================================================================

    public class EventRoom : Carve
    {
        public const int FW = 5, FH = 4;   // race finish chamber
        public PuzzleType type;
        public int x0, ceil, w, h = 5;
        public int fx0, ftop;
        public int Cx => x0 + w / 2;

        public void Build()
        {
            rects.Clear();
            rects.Add(new RectInt(x0 - 1, ceil - 4, w + 2, h + 4));
            if (type == PuzzleType.Race) rects.Add(new RectInt(fx0 - 1, ftop, FW + 2, FH));
        }

        public override int AnchorDepth => ceil + h - 1;

        public override int TileAt(int x, int d)
        {
            int t = Room(x, d, x0, ceil, w, h);
            if (t >= 0)
            {
                // meditation shrine gets brick pillars
                if (type == PuzzleType.Meditate && t == (int)Block.Air && (x == x0 || x == x0 + w - 1) && d > ceil) return (int)Block.Brick;
                return t;
            }
            switch (type)
            {
                case PuzzleType.Gold:
                    return -1;   // nothing hints at the answer: find your own way to get gold to fall in
                case PuzzleType.Race:
                    return Room(x, d, fx0, ftop, FW, FH);
            }
            return -1;
        }

        public override void Spawn(Game g, System.Random r)
        {
            float standY = 2 - ceil - h;
            var at = new Vector2(Cx + 0.5f, standY);
            switch (type)
            {
                case PuzzleType.Boulder:
                    g.AddAltar(new ButtonAltar(at));
                    ScatterHeavy(g.world, r);
                    break;
                case PuzzleType.Gold: g.AddAltar(new GoldAltar(at, ceil)); break;
                case PuzzleType.Meditate: g.AddAltar(new MeditateAltar(at)); break;
                case PuzzleType.Altar: g.AddAltar(new ReqAltar(at, r)); break;
                case PuzzleType.Race:
                    g.AddAltar(new RaceAltar(new Vector2(x0 + 1.5f, standY), new Vector2(fx0 + FW / 2 + 0.5f, 2 - ftop - FH), r));
                    break;
            }
        }
        // The pressure plate wants something heavy dropped on it. Nothing is placed for you: the rock above
        // just has a few more boulders and sand pockets than usual, and you have to dig up and hope.
        void ScatterHeavy(World w, System.Random r)
        {
            int cx = Cx;
            for (int d = ceil - 2; d >= ceil - 14; d--)
                for (int x = cx - 3; x <= cx + 3; x++)
                {
                    int y = -d;
                    Block b = w.Get(x, y);
                    if (b != Block.Dirt && b != Block.Stone && b != Block.HardStone) continue;
                    if (!w.Solid(x, y - 1)) continue;
                    double k = r.NextDouble();
                    bool col = x == cx;
                    if (k < (col ? 0.07 : 0.035)) w.Set(x, y, Block.Boulder);
                    else if (k < (col ? 0.17 : 0.09)) w.Set(x, y, Block.Sand);
                }
        }
    }

    public static class EventGen
    {
        public static void Build(System.Random r, int maxDepth)
        {
            int d = 24, n = 0;
            while (d < maxDepth)
            {
                PuzzleType t;
                if (n == 0) t = (PuzzleType)r.Next(3);   // first one is always a simple puzzle
                else
                {
                    int k = r.Next(100);
                    if (k < 20) t = PuzzleType.Boulder;
                    else if (k < 38) t = PuzzleType.Gold;
                    else if (k < 55) t = PuzzleType.Meditate;
                    else if (k < 74) t = d > 25 ? PuzzleType.Race : PuzzleType.Meditate;
                    else t = d > 28 ? PuzzleType.Altar : PuzzleType.Boulder;
                }
                var e = new EventRoom { type = t, ceil = d };
                e.w = (t == PuzzleType.Altar || t == PuzzleType.Race) ? 7 : 9;
                e.x0 = 4 + r.Next(Gen.W - 8 - e.w);
                if (t == PuzzleType.Race)
                {
                    e.ftop = d + 22 + r.Next(14);   // one to two screens further down
                    e.fx0 = 4 + r.Next(Gen.W - 8 - EventRoom.FW);
                }
                e.Build();
                if (Gen.TryAdd(e)) { n++; d += 38 + r.Next(40); }
                else d += 4;
            }
        }
    }

    // =====================================================================================
    // Altars
    // =====================================================================================

    public abstract class Altar : Entity
    {
        public bool done, failed;
        public Vector2 Half = new Vector2(0.6f, 0.6f);
        public bool Active => !done && !failed;
        public virtual Vector2 Target => pos;
        public virtual void OnHit(Src src) { }

        protected static Player P => Game.I.player;
        protected static readonly Color Hint = new Color(0.72f, 0.72f, 0.78f);
        protected static readonly Color Gold = new Color(1f, 0.82f, 0.3f);

        protected static bool Near(Vector2 at, float r) => P.mode != Player.Mode.Dead && (P.pos - at).sqrMagnitude < r * r;

        protected void Reward(Vector2 at)
        {
            done = true;
            G.RareReward(at);
        }

        protected GameObject Extra(string name, Sprite s, Vector2 at, int order, out SpriteRenderer r)
        {
            var g = new GameObject(name);
            g.transform.SetParent(G.runRoot, false);
            g.transform.position = new Vector3(at.x, at.y, 0);
            r = Art.AddSR(g, s, order);
            return g;
        }
    }

    // ------------------------------------------------------------------ boulder button
    public class ButtonAltar : Altar
    {
        readonly Vector2Int cell;
        readonly GameObject carving;
        readonly SpriteRenderer carvingSr;
        float stepT;

        public ButtonAltar(Vector2 stand)
        {
            pos = stand + new Vector2(0f, 0.3f);
            Half = new Vector2(0.5f, 0.4f);
            cell = new Vector2Int(Mathf.FloorToInt(stand.x), Mathf.RoundToInt(stand.y));
            MakeGO("PressurePlate", Art.PlateUp, 6);
            go.transform.position = new Vector3(stand.x, stand.y, 0);
            // a carving of a boulder on the wall behind: this plate wants something heavy
            carving = Extra("BoulderCarving", Art.TileSprite(Block.Boulder, Biomes.At(cell.x, cell.y)), stand + new Vector2(0f, 2.1f), 4, out carvingSr);
            carvingSr.color = new Color(0.6f, 0.55f, 0.55f, 0.55f);
            carving.transform.localScale = Vector3.one * 0.8f;
        }

        public void OnHeavy(int x, int y)
        {
            if (!Active || x != cell.x || Mathf.Abs(y - cell.y) > 1) return;
            sr.sprite = Art.PlateDown;
            carvingSr.color = new Color(1f, 0.85f, 0.4f, 0.9f);
            G.sfx.Play("clank", 0.9f, 0.6f);
            G.cam.Shake(0.4f);
            G.hud.Banner("CLICK!", "THE PLATE GIVES WAY", Gold);
            Reward(pos + Vector2.up * 0.8f);
        }

        public override void Tick(float dt)
        {
            if (!Active) return;
            // anything heavy rolled, pushed or dropped onto the plate presses it: a boulder (block or rolling body),
            // a chest, a crate, a block of stone...
            Block on = Wd.Get(cell.x, cell.y);
            if (on == Block.Boulder || on == Block.Chest || on == Block.Crate || on == Block.Stone || on == Block.HardStone || on == Block.TNT || on == Block.Obsidian)
            { OnHeavy(cell.x, cell.y); return; }
            foreach (var bb in BoulderBody.All)
                if (Mathf.Abs(bb.pos.x - pos.x) < 0.65f && Mathf.Abs(bb.pos.y - BoulderBody.H.y - (pos.y - 0.3f)) < 0.35f) { OnHeavy(cell.x, cell.y); return; }
            bool onIt = P.grounded && Mathf.Abs(P.pos.x - pos.x) < 0.6f && Mathf.Abs(P.pos.y - Player.Half.y - (pos.y - 0.3f)) < 0.25f;
            if (onIt)
            {
                stepT += dt;
                if (stepT > 0.2f && stepT - dt <= 0.2f)
                {
                    G.sfx.Play("clank", 0.5f, 1.7f);
                }
            }
            else stepT = 0f;
        }

        public override void AddLight(Lighting l) => l.Add(pos, Active ? 1.6f : 2.6f, Active ? 0.45f : 0.75f);


        public override void Kill()
        {
            base.Kill();
            if (carving != null) Object.Destroy(carving);
        }
    }

    // ------------------------------------------------------------------ gold bucket
    public class GoldAltar : Altar
    {
        readonly int need;
        int fill;
        readonly Vector2 stand;
        readonly GameObject fillGo;
        readonly SpriteRenderer fillSr;

        public GoldAltar(Vector2 stand, int depth)
        {
            this.stand = stand;
            need = Mathf.Max(15, Mathf.RoundToInt((15f + depth * 0.15f) / 5f) * 5);
            pos = stand + new Vector2(0f, 0.45f);
            Half = new Vector2(0.45f, 0.45f);
            MakeGO("GoldChalice", Art.Chalice ?? Art.Bucket, 6);
            go.transform.position = new Vector3(stand.x, stand.y, 0);
            fillGo = Extra("GoldFill", Art.Square, stand, 7, out fillSr);
            fillSr.color = new Color(1f, 0.8f, 0.2f);
            fillGo.SetActive(false);
        }

        public override void Tick(float dt)
        {
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Coin c)) continue;
                if (!Active) break;
                Vector2 d = c.pos - stand;
                // every coin loose in the bucket's chamber gets reeled in, flying over to drop through the mouth
                if (Mathf.Abs(d.x) < 5.5f && d.y > -0.5f && d.y < 6.5f)
                {
                    c.noMag = Mathf.Max(c.noMag, 0.15f);
                    c.pullTo = stand + new Vector2(0f, Mathf.Abs(d.x) > 0.3f ? 1.4f : 0.75f);   // over the rim first, then down in
                    c.pullT = 0.15f;
                }
                if (Mathf.Abs(d.x) < 0.34f && d.y > 0.45f && d.y < 1f)
                {
                    fill += c.value;
                    c.Kill();
                    G.sfx.Play("coin", 0.5f, Random.Range(0.85f, 1.15f));
                    G.fx.Burst(stand + Vector2.up * 0.6f, 4, new Color(1f, 0.9f, 0.4f), 2.5f, 0.3f, 10f);
                }
            }

            // a glint of gold at the bottom once anything is in: it doesn't tell you how much more it wants
            float k = fill <= 0 ? 0f : fill >= need ? 1f : 0.15f;
            fillGo.SetActive(k > 0f);
            // a heap of gold showing over the chalice's rim (a glint at first, brimming when it's full)
            float hgt = 0.12f * k;
            fillGo.transform.position = new Vector3(stand.x, stand.y + 0.8f + hgt * 0.5f, 0);
            fillGo.transform.localScale = new Vector3(0.6f, Mathf.Max(0.0625f, hgt), 1f);

            if (!Active) return;

            if (fill >= need)
            {
                G.hud.Banner("THE CHALICE OVERFLOWS", "YOUR OFFERING IS ACCEPTED", Gold);
                Reward(stand + Vector2.up * 1.3f);
            }
        }

        public override void AddLight(Lighting l) => l.Add(pos, Active ? 1.8f : 3f, Active ? 0.5f : 0.8f);

        public override void Kill()
        {
            base.Kill();
            if (fillGo != null) Object.Destroy(fillGo);
        }
    }

    // ------------------------------------------------------------------ meditation statue
    public class MeditateAltar : Altar
    {
        float t, humT;
        const float GlowStart = 7f, Total = 10f;
        static readonly Color Stone = new Color(0.72f, 0.72f, 0.78f), Lit = new Color(1f, 0.92f, 0.55f);

        public MeditateAltar(Vector2 stand)
        {
            pos = stand + new Vector2(0f, 0.45f);
            Half = new Vector2(0.45f, 0.45f);
            MakeGO("MeditationStatue", Art.StatueSit, 6);
            go.transform.position = new Vector3(stand.x, stand.y, 0);
            sr.color = Stone;
        }

        float Glow => Mathf.Clamp01((t - GlowStart) / (Total - GlowStart));

        public override void Tick(float dt)
        {
            if (!Active) { sr.color = Lit; return; }
            bool inRange = Mathf.Abs(P.pos.x - pos.x) < 2f && Mathf.Abs(P.pos.y - pos.y) < 1.5f && P.mode != Player.Mode.Dead;
            if (inRange && !GameInput.AnyHeld()) t += dt; else t = 0f;   // just: don't touch anything

            float k = Glow;
            sr.color = Color.Lerp(Stone, Lit, k);
            if (k > 0f)
            {
                if (Random.value < 0.15f + k * 0.6f)
                    G.fx.Burst(pos + new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(-0.3f, 0.5f)), 1, Lit, 1f + k, 0.8f, -4f);
                humT -= dt;
                if (humT <= 0f) { humT = 0.5f; G.sfx.Play("charge", 0.25f + 0.35f * k, 0.6f + 0.6f * k, 0f); }
            }

            if (t >= Total)
            {
                t = Total;
                G.fx.Ring(pos, 3f, Lit);
                G.lighting.Flash(pos, 8f, 0.8f);
                G.hud.Banner("ENLIGHTENMENT", "STILLNESS IS REWARDED", Gold);
                Reward(pos + Vector2.up * 1.3f);
            }
        }

        public override void AddLight(Lighting l)
        {
            if (!Active) { l.Add(pos, 3f, 0.65f); return; }
            float k = Glow;
            if (k > 0f) l.Add(pos, 1.5f + 6.5f * k, 0.35f + 0.65f * k);
        }
    }

    // ------------------------------------------------------------------ race the burning fuse
    public class RaceAltar : Altar
    {
        readonly Vector2 finish;
        readonly List<Vector2> path = new List<Vector2>();
        readonly List<float> cum = new List<float>();
        readonly List<Transform> segU = new List<Transform>(), segB = new List<Transform>();
        readonly List<SpriteRenderer> segUS = new List<SpriteRenderer>(), segBS = new List<SpriteRenderer>();
        readonly GameObject flag;
        readonly SpriteRenderer flagSr;
        readonly float L, burnTime;
        float burned, fuseSfx;
        bool started;
        Vector2 tip;

        public override bool AlwaysActive => started && Active;
        public override Vector2 Target => started ? finish : pos;

        public RaceAltar(Vector2 statueStand, Vector2 finishStand, System.Random r)
        {
            pos = statueStand + new Vector2(0f, 0.5f);
            Half = new Vector2(0.45f, 0.55f);
            MakeGO("RaceStatue", Art.StatueRun, 6);
            go.transform.position = new Vector3(statueStand.x, statueStand.y, 0);
            sr.color = new Color(0.8f, 0.8f, 0.85f);
            finish = finishStand + new Vector2(0f, 0.5f);
            flag = Extra("FinishFlag", Art.Flag, finishStand + new Vector2(0.2f, 0f), 6, out flagSr);

            // a right-angled fuse line that snakes through the rock to the finish
            Vector2 a = new Vector2(statueStand.x + 0.5f, statueStand.y + 0.1f);
            Vector2 e = new Vector2(finishStand.x - 0.3f, finishStand.y + 0.1f);
            float y1 = statueStand.y - 0.5f - r.Next(2, 6);
            float y2 = Mathf.Floor(Mathf.Lerp(y1, e.y, 0.35f + (float)r.NextDouble() * 0.35f)) + 0.5f;
            float midX = 4.5f + r.Next(Gen.W - 9);
            path.Add(a);
            path.Add(new Vector2(a.x, y1));
            path.Add(new Vector2(midX, y1));
            path.Add(new Vector2(midX, y2));
            path.Add(new Vector2(e.x, y2));
            path.Add(e);
            cum.Add(0f);
            for (int i = 1; i < path.Count; i++) cum.Add(cum[i - 1] + (path[i] - path[i - 1]).magnitude);
            L = cum[cum.Count - 1];
            burnTime = 5f + (statueStand.y - finishStand.y) * 0.5f;

            for (int i = 0; i < path.Count - 1; i++)
            {
                segU.Add(Extra("Fuse", Art.Pixel, path[i], 4, out var us).transform);
                us.color = new Color(0.78f, 0.62f, 0.4f);
                segUS.Add(us);
                segB.Add(Extra("FuseBurnt", Art.Pixel, path[i], 4, out var bs).transform);
                bs.color = new Color(0.16f, 0.12f, 0.1f);
                bs.enabled = false;
                segBS.Add(bs);
                Line(segU[i], path[i], path[i + 1]);
            }
            tip = a;
        }

        static void Line(Transform t, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            t.position = (Vector3)((a + b) * 0.5f);
            t.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            t.localScale = new Vector3(d.magnitude * 16f + 2f, 2f, 1f);
        }

        Vector2 PointAt(float dist)
        {
            for (int i = 0; i < path.Count - 1; i++)
                if (dist <= cum[i + 1])
                    return Vector2.Lerp(path[i], path[i + 1], (dist - cum[i]) / Mathf.Max(0.0001f, cum[i + 1] - cum[i]));
            return path[path.Count - 1];
        }

        void UpdateRope()
        {
            for (int i = 0; i < segU.Count; i++)
            {
                float s0 = cum[i], s1 = cum[i + 1];
                if (burned >= s1) { segUS[i].enabled = false; segBS[i].enabled = true; Line(segB[i], path[i], path[i + 1]); }
                else if (burned <= s0) { segUS[i].enabled = true; segBS[i].enabled = false; }
                else
                {
                    Vector2 m = Vector2.Lerp(path[i], path[i + 1], (burned - s0) / (s1 - s0));
                    segUS[i].enabled = true; segBS[i].enabled = true;
                    Line(segB[i], path[i], m);
                    Line(segU[i], m, path[i + 1]);
                }
            }
        }

        public override void OnHit(Src src)
        {
            if (started || !Active) return;
            started = true;
            G.hud.Banner("THE FUSE IS LIT!", "", new Color(1f, 0.6f, 0.2f));
            G.sfx.Play("fuse", 0.9f);
            G.sfx.Play("special", 0.7f, 0.7f);
            G.fx.Burst(pos, 20, new Color(1f, 0.7f, 0.3f), 6f, 0.5f, 5f);
            sr.color = new Color(1f, 0.75f, 0.45f);
        }

        public override void Tick(float dt)
        {
            if (!Active) return;
            if (!started) return;

            burned += L / burnTime * dt;
            tip = PointAt(burned);
            UpdateRope();
            G.fx.Burst(tip, 1, new Color(1f, 0.8f, 0.3f), 3f, 0.25f, 8f, 1f, false);
            fuseSfx -= dt;
            if (fuseSfx <= 0f && (P.pos - tip).sqrMagnitude < 200f) { fuseSfx = 0.35f; G.sfx.Play("fuse", 0.25f); }

            if (P.mode != Player.Mode.Dead && Mathf.Abs(P.pos.x - finish.x) < 1.2f && Mathf.Abs(P.pos.y - finish.y) < 1.2f)
            {
                flagSr.color = Gold;
                foreach (var s in segUS) s.enabled = false;
                G.hud.Banner("YOU BEAT THE FUSE!", "THE RUNNER SALUTES YOU", Gold);
                Reward(finish + Vector2.up * 0.8f);
            }
            else if (burned >= L)
            {
                failed = true;
                G.fx.Explosion(finish, 1.1f);
                G.sfx.Play("explode", 0.6f, 1.3f);
                flagSr.color = new Color(0.2f, 0.15f, 0.12f);
                sr.color = new Color(0.4f, 0.4f, 0.42f);
                G.hud.Banner("TOO SLOW...", "THE FLAG BURNS AWAY", new Color(0.8f, 0.4f, 0.3f));
            }
        }

        public override void AddLight(Lighting l)
        {
            if (started && Active) { l.Add(tip, 2.8f, 0.95f); l.Add(finish, 1.6f, 0.5f); }
            else if (Active) l.Add(pos, 1.6f, 0.4f);
            else if (done) l.Add(finish, 2.5f, 0.6f);
        }

        public override void Kill()
        {
            base.Kill();
            if (flag != null) Object.Destroy(flag);
            foreach (var t in segU) if (t != null) Object.Destroy(t.gameObject);
            foreach (var t in segB) if (t != null) Object.Destroy(t.gameObject);
        }
    }

    // ------------------------------------------------------------------ weapon / skill altar
    public class ReqAltar : Altar
    {
        enum Req { Special, Attack, Trinket }
        readonly Req kind;
        readonly string id;
        readonly Def def;
        readonly Vector2 stand;
        readonly GameObject icon;
        readonly SpriteRenderer iconSr;
        float wrongT, pulse;

        static readonly string[] SpecialIds = { "dash", "grapple", "superjump", "rope", "quickdig" };
        static readonly string[] AttackIds = { "shotgun", "smg", "sword", "grenade", "boltcaster", "flamer", "laserpistol", "disc" };
        static readonly string[] TrinketIds = { "lantern", "magnet", "swift", "spring", "nighteyes", "feather", "greedy", "prospector", "thick", "lucky", "diamondbit" };

        public ReqAltar(Vector2 stand, System.Random r)
        {
            this.stand = stand;
            pos = stand + new Vector2(0f, 0.9f);
            Half = new Vector2(0.8f, 0.9f);
            MakeGO("WeaponAltar", Art.AltarBase, 6);
            go.transform.position = new Vector3(stand.x, stand.y, 0);

            int k = r.Next(100);
            if (k < 55) { kind = Req.Special; id = SpecialIds[r.Next(SpecialIds.Length)]; }
            else if (k < 78) { kind = Req.Attack; id = AttackIds[r.Next(AttackIds.Length)]; }
            else { kind = Req.Trinket; id = TrinketIds[r.Next(TrinketIds.Length)]; }
            def = DB.ById[id];

            // the carving above the altar shows exactly what it wants
            icon = Extra("AltarCarving", def.icon, stand + new Vector2(0f, 1.35f), 7, out iconSr);
            icon.transform.localScale = Vector3.one * 1.3f;
        }

        // A special altar wakes up when you use that special right next to it.
        public void OnSpecial(string sid, Vector2 at)
        {
            if (!Active || kind != Req.Special || sid != id) return;
            if ((at - pos).sqrMagnitude < 9f) Awaken();
        }

        public override void OnHit(Src src)
        {
            if (!Active) return;
            bool ok = kind == Req.Attack && (src == Src.Weapon || src == Src.Pick) && P.attack != null && P.attack.id == id;
            if (ok) { Awaken(); return; }
            if (wrongT <= 0f)
            {
                wrongT = 1f;
                G.sfx.Play("clank", 0.4f, 0.7f);
            }
        }

        void Awaken()
        {
            iconSr.color = Color.white;
            G.fx.Ring(pos, 2.5f, Gold);
            G.lighting.Flash(pos, 7f, 0.6f);
            G.hud.Banner("THE ALTAR AWAKENS", "IT RECOGNISES THE " + def.name, Gold);
            Reward(stand + Vector2.up * 2.2f);
        }

        public override void Tick(float dt)
        {
            wrongT -= dt;
            pulse += dt;
            if (!Active)
            {
                iconSr.color = Color.Lerp(Color.white, Gold, 0.5f + 0.5f * Mathf.Sin(pulse * 3f));
                return;
            }
            float v = 0.42f + 0.06f * Mathf.Sin(pulse * 2f);
            iconSr.color = new Color(v, v, v + 0.05f);
            if (kind == Req.Trinket && Near(pos, 2f) && P.Owns(id)) Awaken();
        }

        public override void AddLight(Lighting l) => l.Add(pos + Vector2.up * 0.4f, Active ? 1.7f : 3f, Active ? 0.45f : 0.75f);

        public override void Kill()
        {
            base.Kill();
            if (icon != null) Object.Destroy(icon);
        }
    }
}
