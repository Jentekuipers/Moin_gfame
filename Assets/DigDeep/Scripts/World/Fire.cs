using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Fire. Grass, vines, dwarven wood, chests and weapon crates burn (and TNT goes off).
    // A flame is exactly one block wide and lives 4 seconds:
    //  - after 1 second (and again at 2.5) it lights every burnable thing touching it
    //  - burning wood / chests / crates are gone after the 4 seconds (chests still drop their loot) and the
    //    flame drops down as an ember onto whatever is below
    //  - on rock, dirt and other things that don't burn it just dies down after its 4 seconds
    //  - it shrinks in its last second, and water puts it out with a hiss
    //  - it melts ice next to it, sets enemies alight, and sets you alight if you stand in it for half a second
    public static class Fire
    {
        static readonly Dictionary<long, Flame> burning = new Dictionary<long, Flame>();
        public const float Life = 4f;

        static Game G => Game.I;
        static World Wd => Game.I.world;
        static long Key(int x, int y) => ((long)y << 20) ^ (x & 0xFFFFF);

        public static void Reset() => burning.Clear();
        public static bool IsBurning(int x, int y) => burning.ContainsKey(Key(x, y));

        // put out every flame near a point (the fox's dash). Burning creatures aren't flames: they keep burning.
        public static void DouseNear(Vector2 c, float r)
        {
            if (burning.Count == 0) return;
            List<Flame> hit = null;
            foreach (var f in burning.Values)
                if ((new Vector2(f.cx + 0.5f, f.cy + 0.5f) - c).sqrMagnitude < r * r) (hit ?? (hit = new List<Flame>())).Add(f);
            if (hit != null) foreach (var f in hit) f.Douse();
        }
        internal static void Register(Flame f) => burning[Key(f.cx, f.cy)] = f;
        internal static void Unregister(Flame f) { if (burning.TryGetValue(Key(f.cx, f.cy), out var o) && o == f) burning.Remove(Key(f.cx, f.cy)); }

        // Something in this cell that can catch fire?
        public static bool HasFuel(int x, int y)
        {
            Block b = Wd.Get(x, y);
            if (Tiles.Def(b).burnable) return true;
            return !Tiles.Solid(b) && (Wd.Flags(x, y) & (World.Grass | World.Vine)) != 0;
        }

        // Light this cell if it can burn. TNT goes off, ice melts. Returns true if something caught fire.
        public static bool Ignite(int x, int y)
        {
            var w = Wd;
            if (w == null || y >= 1) return false;
            Block b = w.Get(x, y);
            if (b == Block.TNT) { w.Break(x, y, false); return true; }
            if (b == Block.Ice) { Melt(x, y); return false; }
            if (!HasFuel(x, y) || IsBurning(x, y)) return false;
            if (w.LiquidType(x, y) == Fluids.Water && w.LiquidAmount(x, y) > 0.3f) return false;
            G.Spawn(new Flame(x, y, Life, Tiles.Solid(b), false));
            return true;
        }

        // A flame with nothing to burn, sitting on top of whatever is below (cursed footsteps, landing embers).
        public static void Surface(int x, int y, float life, bool spareThePlayer)
        {
            var w = Wd;
            if (w == null || y >= 1 || IsBurning(x, y)) return;
            Block b = w.Get(x, y);
            if (Tiles.Solid(b)) return;
            if (w.LiquidType(x, y) == Fluids.Water) return;
            if (HasFuel(x, y)) { Ignite(x, y); return; }
            G.Spawn(new Flame(x, y, life, false, spareThePlayer));
        }

        // Light every burnable cell overlapping a box (a burning body brushing against things).
        public static void IgniteBox(Vector2 c, Vector2 h)
        {
            int x0 = Mathf.FloorToInt(c.x - h.x), x1 = Mathf.FloorToInt(c.x + h.x);
            int y0 = Mathf.FloorToInt(c.y - h.y), y1 = Mathf.FloorToInt(c.y + h.y);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (HasFuel(x, y) || Wd.Get(x, y) == Block.TNT) Ignite(x, y);
        }

        public static void Melt(int x, int y)
        {
            var w = Wd;
            if (w.Get(x, y) != Block.Ice) return;
            w.Set(x, y, Block.Air);
            w.SetLiquid(x, y, Fluids.Water, 1f);
            G.fx.Smoke(new Vector2(x + 0.5f, y + 0.5f), 3, 0.7f);
            G.sfx.Play("fizz", 0.35f, 1.3f);
        }

        // Heat melts nearby ice.
        public static void MeltAround(Vector2 p, float r)
        {
            int x0 = Mathf.FloorToInt(p.x - r), x1 = Mathf.FloorToInt(p.x + r), y0 = Mathf.FloorToInt(p.y - r), y1 = Mathf.FloorToInt(p.y + r);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (Wd.Peek(x, y) == Block.Ice) Melt(x, y);
        }

        // Explosions throw sparks a bit beyond their blast: they light burnable things and enemies, never you.
        public static void Sparks(Vector2 c, float r)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(8 + r * 5), 8, 26);
            for (int i = 0; i < n; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                if (dir.y < -0.3f) dir.y = -dir.y * 0.5f;   // mostly outwards and up
                float sp = Random.Range(9f, 14f);
                float life = r * 1.7f / sp * Random.Range(0.75f, 1.15f);
                G.Spawn(new Spark(c + dir * 0.3f, dir.normalized * sp, life));
            }
        }
    }

    public class Flame : Entity
    {
        public readonly int cx, cy;
        readonly float life;
        readonly bool onBlock;   // burning the block in its own cell (wood, chest, crate)
        bool sparePlayer;        // cursed footstep: harmless until you've stepped out of it
        float t, touchT, crackT;
        int spreads, frame;

        public override bool AlwaysActive => true;
        public float Height => t > life - 1f ? Mathf.Clamp01(life - t) : 1f;

        public Flame(int x, int y, float life, bool onBlock, bool sparePlayer)
        {
            cx = x; cy = y; this.life = life; this.onBlock = onBlock; this.sparePlayer = sparePlayer;
            pos = new Vector2(x + 0.5f, y + 0.5f);
            MakeGO("Flame", Art.Flames[0], onBlock ? 6 : 9);
            go.transform.position = new Vector3(x + 0.5f, y, 0f);
            Fire.Register(this);
            G.sfx.Play("ignite", 0.35f, Random.Range(0.85f, 1.2f));
            frame = Random.Range(0, 3);
        }

        public override void Tick(float dt)
        {
            t += dt;
            // water (in this cell, or pouring in from above) puts it out
            if ((Wd.LiquidType(cx, cy) == Fluids.Water && Wd.LiquidAmount(cx, cy) > 0.15f) ||
                (Wd.LiquidType(cx, cy + 1) == Fluids.Water && Wd.LiquidAmount(cx, cy + 1) > 0.3f))
            {
                Douse();
                return;
            }
            if (Random.value < dt * 2f) Fluids.Heat(cx, cy);   // the heat boils poison off and melts sand into glass
            // the burning block got knocked out from under it early
            // the thing it was burning is gone (knocked out, or the floor under it broke): it tumbles down, still burning
            if ((onBlock && !Tiles.Def(Wd.Get(cx, cy)).burnable) || (!onBlock && !Wd.Solid(cx, cy - 1) && !Fire.HasFuel(cx, cy)))
            {
                Burnout(false);
                if (t < life - 0.3f) G.Spawn(new Ember(new Vector2(cx + 0.5f, cy + 0.4f)));
                return;
            }

            // spread to everything touching it
            if ((spreads == 0 && t >= 1f) || (spreads == 1 && t >= 2.5f))
            {
                spreads++;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (dx != 0 || dy != 0) Fire.Ignite(cx + dx, cy + dy);
                Fire.MeltAround(pos, 1.3f);
            }

            // burn whatever stands in it
            Vector2 box = new Vector2(0.45f, 0.45f * Height);
            Vector2 c = new Vector2(cx + 0.5f, cy + box.y);
            foreach (var e in G.ents)
                if (!e.dead && e is Enemy en && U.Overlap(c, box, en.pos, en.half)) en.Ignite(2f);
            var p = G.player;
            bool inIt = p.mode != Player.Mode.Dead && U.Overlap(c, box, p.pos, Player.Half);
            if (!inIt) { sparePlayer = false; touchT = 0f; }
            else if (!sparePlayer)
            {
                touchT += dt;
                if (touchT > 0.5f) p.SetOnFire();
            }

            if (t >= life) { Burnout(true); return; }

            // looks: three-frame flicker, shrinking in the last second
            crackT -= dt;
            if (crackT <= 0f)
            {
                crackT = Random.Range(0.08f, 0.14f);
                frame = (frame + 1 + Random.Range(0, 2)) % Art.Flames.Length;
                sr.sprite = Art.Flames[frame];
                if (Random.value < 0.08f && (p.pos - pos).sqrMagnitude < 120f) G.sfx.Play("fuse", 0.08f, Random.Range(0.7f, 1.3f), 0.1f);
            }
            float h = Mathf.Max(0.15f, Height);
            go.transform.localScale = new Vector3(Random.value < 0.15f ? -1f : 1f, h * Random.Range(0.92f, 1.06f), 1f);
            if (Random.value < 0.12f) G.fx.Burst(new Vector2(cx + Random.Range(0.2f, 0.8f), cy + 0.9f * h), 1, new Color(1f, Random.Range(0.5f, 0.9f), 0.2f), 1f, 0.5f, -6f, 1f, false);
        }

        void Burnout(bool natural)
        {
            Kill();
            if (!natural) return;
            var w = Wd;
            Block b = w.Get(cx, cy);
            Vector2 c = new Vector2(cx + 0.5f, cy + 0.5f);
            if (onBlock && Tiles.Def(b).burnable)
            {
                // the block burns away (a chest still spills its loot), and the flame tumbles down
                w.Break(cx, cy, false);
                w.Stain(c + Vector2.down * 0.4f, new Color(0.08f, 0.06f, 0.06f, 0.55f), 2);
                G.Spawn(new Ember(c));
                G.fx.Smoke(c, 3, 0.9f);
            }
            else
            {
                // grass and vines are burned away; on bare rock the flame just dies down
                byte fl = w.Flags(cx, cy);
                if ((fl & (World.Grass | World.Vine)) != 0)
                {
                    bool vine = (fl & World.Vine) != 0;
                    w.ClearFlags(cx, cy, (byte)(World.Grass | World.Vine));
                    w.Stain(new Vector2(c.x, cy + 0.1f), new Color(0.1f, 0.07f, 0.06f, 0.45f), 2);
                    if (vine) G.Spawn(new Ember(c));   // burning vine bits fall
                }
                G.fx.Smoke(c, 2, 0.6f);
            }
        }

        public void Douse()
        {
            Kill();
            G.sfx.Play("fizz", 0.6f, Random.Range(0.9f, 1.1f));
            G.fx.Smoke(new Vector2(cx + 0.5f, cy + 0.4f), 6, 1f);
        }

        public override void Kill()
        {
            base.Kill();
            Fire.Unregister(this);
        }

        public override void AddLight(Lighting l)
        {
            float f = 1f + Mathf.Sin(Time.time * 17f + cx) * 0.08f + Random.Range(-0.04f, 0.04f);
            l.Add(new Vector2(cx + 0.5f, cy + 0.5f * Height), 2.6f * Mathf.Max(0.3f, Height) * f, 0.8f);
        }
    }

    // A burning scrap dropping off something that burned away: it falls, lights what it lands on (or what it
    // passes through), and otherwise burns briefly on the floor.
    public class Ember : Entity
    {
        float vy;
        public override bool AlwaysActive => true;

        public Ember(Vector2 p)
        {
            pos = p;
            MakeGO("Ember", Art.Flames[1], 9);
            go.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
        }

        public override void Tick(float dt)
        {
            vy = Mathf.Max(vy - 30f * dt, -14f);
            pos.y += vy * dt;
            int x = Mathf.FloorToInt(pos.x), y = Mathf.FloorToInt(pos.y);
            if (Wd.LiquidType(x, y) == Fluids.Water) { G.sfx.Play("fizz", 0.3f, 1.4f); G.fx.Smoke(pos, 2, 0.5f); Kill(); return; }
            if (Fire.HasFuel(x, y) && !Tiles.Solid(Wd.Get(x, y))) { Fire.Ignite(x, y); Kill(); return; }
            if (Wd.Solid(x, y))
            {
                // landed on top of the block at (x, y)
                if (!Fire.Ignite(x, y)) Fire.Surface(x, y + 1, 1.5f, false);
                Kill();
                return;
            }
            foreach (var e in G.ents)
                if (!e.dead && e is Enemy en && U.Overlap(pos, new Vector2(0.2f, 0.2f), en.pos, en.half)) en.Ignite(2f);
            sr.sprite = Art.Flames[(int)(Time.time * 12f) % Art.Flames.Length];
            go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y - 0.25f), 0f);
        }

        public override void AddLight(Lighting l) => l.Add(pos, 1.5f, 0.7f);
    }

    // An explosion spark: a hot speck that lights grass, wood and enemies it touches (never the player), and
    // dies on anything it touches.
    public class Spark : Entity
    {
        Vector2 vel;
        float life;
        public override bool AlwaysActive => true;

        public Spark(Vector2 p, Vector2 v, float life)
        {
            pos = p; vel = v; this.life = life;
            MakeGO("Spark", Art.Pixel, 16);
            go.transform.localScale = new Vector3(2f, 2f, 1f);
            sr.color = new Color(1f, 0.8f, 0.3f);
        }

        public override void Tick(float dt)
        {
            life -= dt;
            vel.y -= 14f * dt;
            pos += vel * dt;
            int x = Mathf.FloorToInt(pos.x), y = Mathf.FloorToInt(pos.y);
            if (life <= 0f) { Fire.Ignite(x, y); Kill(); return; }
            if (Wd.LiquidType(x, y) == Fluids.Water) { Kill(); return; }
            if (Fire.HasFuel(x, y) || Wd.Get(x, y) == Block.TNT || Wd.Get(x, y) == Block.Ice) { Fire.Ignite(x, y); Kill(); return; }
            if (Wd.Solid(x, y)) { Kill(); return; }
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en) || en.Intangible || !U.Overlap(pos, new Vector2(0.1f, 0.1f), en.pos, en.half)) continue;
                en.Ignite(2f);
                Kill();
                return;
            }
            sr.color = Random.value < 0.5f ? new Color(1f, 0.9f, 0.4f) : new Color(1f, 0.5f, 0.15f);
            go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y), 0f);
        }

        public override void AddLight(Lighting l) => l.Add(pos, 0.9f, 0.6f);
    }
}
