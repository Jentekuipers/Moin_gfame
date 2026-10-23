using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // ELECTRICITY in water. Sources (tesla coils, eels, machines wading in water, STORM HEART) let off bursts at random
    // moments. A burst floods out through every connected water cell, falling streams included (how far it gets down a
    // waterfall is a bit random), pulsing in and out for about a second. Weak bursts are thin forks of lightning and a
    // few sparks; strong ones (a source that keeps firing grows stronger) fill the water with crackling sparks.
    // Anything touching charged water is shocked (damage + a short stun), and charge sets off TNT, volatile crystal,
    // bombs, explosive shots and volatile enemies.
    public static class Electric
    {
        class Burst
        {
            public readonly Dictionary<long, int> cells = new Dictionary<long, int>();   // cell -> distance from the source
            public readonly List<Vector2Int> list = new List<Vector2Int>();
            public Vector2Int origin;
            public int maxDist;
            public float t, dur, strength, boltT, detT;
            public bool friendly;   // made by the player (STORM HEART): it never hurts the player
            public readonly List<List<Vector2>> bolts = new List<List<Vector2>>();
        }

        static readonly List<Burst> bursts = new List<Burst>();
        static readonly Dictionary<long, float> level = new Dictionary<long, float>();
        static readonly Dictionary<long, float> hostile = new Dictionary<long, float>();
        static readonly Queue<Vector2Int> queue = new Queue<Vector2Int>();
        static readonly Dictionary<long, int> falls = new Dictionary<long, int>();

        static Game G => Game.I;
        static World Wd => Game.I.world;
        static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

        public static void Reset() { bursts.Clear(); level.Clear(); hostile.Clear(); }

        static bool IsWater(int x, int y) => Wd.LiquidType(x, y) == Fluids.Water && Wd.LiquidAmount(x, y) > 0.05f;

        // how charged a cell is right now (0 = not at all). Also what the water renderer draws sparks from.
        public static float Level(int x, int y) => level.Count > 0 && level.TryGetValue(Key(x, y), out var v) ? v : 0f;
        public static bool Live(Vector2 p) => Level(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)) > 0.12f;
        public static bool Any => bursts.Count > 0;

        // the lightning forks of every burst, in world space (drawn by the water renderer)
        public static IEnumerable<List<Vector2>> Bolts
        {
            get { foreach (var b in bursts) foreach (var l in b.bolts) yield return l; }
        }

        // Let off a burst into the water at (or right next to) a point. Strength 0..1.
        public static void Zap(Vector2 at, float strength, bool friendly)
        {
            int ax = Mathf.FloorToInt(at.x), ay = Mathf.FloorToInt(at.y);
            Vector2Int start = new Vector2Int(int.MinValue, 0);
            for (int r = 0; r <= 1 && start.x == int.MinValue; r++)
                for (int dy = -r; dy <= r && start.x == int.MinValue; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (IsWater(ax + dx, ay + dy)) { start = new Vector2Int(ax + dx, ay + dy); break; }
            if (start.x == int.MinValue) return;

            var b = new Burst { origin = start, strength = Mathf.Clamp01(strength), friendly = friendly, dur = Random.Range(1.0f, 1.5f) };
            // flood out through the connected water; down a falling stream it only gets so far
            int fallReach = Random.Range(2, 10);
            queue.Clear(); falls.Clear();
            queue.Enqueue(start);
            b.cells[Key(start.x, start.y)] = 0;
            falls[Key(start.x, start.y)] = 0;
            while (queue.Count > 0 && b.cells.Count < 700)
            {
                var c = queue.Dequeue();
                long ck = Key(c.x, c.y);
                int d = b.cells[ck], f = falls[ck];
                b.list.Add(c);
                b.maxDist = Mathf.Max(b.maxDist, d);
                for (int k = 0; k < 4; k++)
                {
                    int nx = c.x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = c.y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    long nk = Key(nx, ny);
                    if (b.cells.ContainsKey(nk) || !IsWater(nx, ny)) continue;
                    // a thin, falling bit of water: the charge has a harder time following it
                    bool thin = Wd.LiquidAmount(nx, ny) < 0.6f && !Wd.Solid(nx, ny - 1);
                    int nf = thin ? f + 1 : 0;
                    if (nf > fallReach) continue;
                    b.cells[nk] = d + 1;
                    falls[nk] = nf;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
            bursts.Add(b);
            float near = (G.player.pos - new Vector2(start.x, start.y)).magnitude;
            if (near < 22f) G.sfx.Play("shock", Mathf.Lerp(0.6f, 0.15f, near / 22f) * (0.5f + 0.5f * b.strength), Random.Range(0.85f, 1.15f), 0.1f);
        }

        public static void Tick(float dt)
        {
            level.Clear(); hostile.Clear();
            if (bursts.Count == 0) return;
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                var b = bursts[i];
                b.t += dt;
                if (b.t >= b.dur) { bursts.RemoveAt(i); continue; }
                float env = b.t < 0.15f ? b.t / 0.15f : 1f - (b.t - 0.15f) / (b.dur - 0.15f);
                float pulse = 0.55f + 0.45f * Mathf.Sin(b.t * 28f);   // it throbs in and out
                float reachCap = Mathf.Lerp(Mathf.Min(3f, b.maxDist), b.maxDist, b.strength);
                float reach = reachCap * Mathf.Min(1f, b.t / 0.35f);
                float density = Mathf.Lerp(0.12f, 0.8f, b.strength) * env * pulse;
                foreach (var c in b.list)
                {
                    long k = Key(c.x, c.y);
                    int d = b.cells[k];
                    if (d > reach) continue;
                    float v = density * (1f - 0.5f * d / Mathf.Max(1f, reachCap));
                    if (!level.TryGetValue(k, out var old) || old < v) level[k] = v;
                    if (!b.friendly && (!hostile.TryGetValue(k, out var oh) || oh < v)) hostile[k] = v;
                }
                // forks of lightning, redrawn many times a second so they flicker
                b.boltT -= dt;
                if (b.boltT <= 0f)
                {
                    b.boltT = 0.06f;
                    b.bolts.Clear();
                    int n = 1 + Mathf.RoundToInt(b.strength * 4f);
                    for (int j = 0; j < n; j++) b.bolts.Add(MakeBolt(b, reach));
                }
                // sparks leaping off the edge of the charge: up, down and sideways out of the water
                if (Random.value < 0.6f)
                {
                    var c = b.list[Random.Range(0, b.list.Count)];
                    if (b.cells[Key(c.x, c.y)] <= reach)
                    {
                        Vector2 dir = Random.insideUnitCircle.normalized;
                        G.fx.Burst(new Vector2(c.x + 0.5f, c.y + 0.5f), 1, new Color(0.9f, 0.98f, 1f), 6f, 0.12f, 0f, 1f, false);
                        if (Random.value < 0.3f) G.fx.Sparks(new Vector2(c.x + 0.5f, c.y + 0.5f) + dir * 0.4f, new Color(0.85f, 0.95f, 1f));
                    }
                }
                // charge sets off anything explosive touching the water (checked a few times a second)
                b.detT -= dt;
                if (b.detT <= 0f)
                {
                    b.detT = 0.2f;
                    foreach (var c in b.list)
                    {
                        if (b.cells[Key(c.x, c.y)] > reach) continue;
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = c.x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = c.y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                            Block nb = Wd.Get(nx, ny);
                            if (nb == Block.TNT || nb == Block.Volatile) Wd.Break(nx, ny, false);
                        }
                    }
                }
            }
            ShockThings();
        }

        // a jagged path from the source out to some charged cell
        static List<Vector2> MakeBolt(Burst b, float reach)
        {
            var pts = new List<Vector2>();
            Vector2 a = new Vector2(b.origin.x + 0.5f, b.origin.y + 0.5f);
            Vector2Int target = b.origin;
            for (int tries = 0; tries < 6; tries++)
            {
                var c = b.list[Random.Range(0, b.list.Count)];
                if (b.cells[Key(c.x, c.y)] <= reach) { target = c; break; }
            }
            Vector2 z = new Vector2(target.x + Random.value, target.y + Random.value);
            int segs = Mathf.Clamp(Mathf.RoundToInt((z - a).magnitude * 1.5f), 2, 14);
            pts.Add(a);
            Vector2 perp = new Vector2(-(z - a).y, (z - a).x).normalized;
            for (int i = 1; i < segs; i++)
            {
                Vector2 p = Vector2.Lerp(a, z, i / (float)segs) + perp * Random.Range(-0.6f, 0.6f);
                int px = Mathf.FloorToInt(p.x), py = Mathf.FloorToInt(p.y);
                if (!b.cells.ContainsKey(Key(px, py))) p = Vector2.Lerp(a, z, i / (float)segs);   // keep it inside the water
                pts.Add(p);
            }
            pts.Add(z);
            return pts;
        }

        static float MaxUnder(Dictionary<long, float> map, Vector2 c, Vector2 h)
        {
            float m = 0f;
            int x0 = Mathf.FloorToInt(c.x - h.x), x1 = Mathf.FloorToInt(c.x + h.x), y0 = Mathf.FloorToInt(c.y - h.y), y1 = Mathf.FloorToInt(c.y + h.y);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (map.TryGetValue(Key(x, y), out var v) && v > m) m = v;
            return m;
        }

        static void ShockThings()
        {
            var p = G.player;
            int depth = Mathf.Max(0, p.Depth);
            if (p.mode != Player.Mode.Dead && MaxUnder(hostile, p.pos, Player.Half * 0.9f) > 0.12f)
                p.Shock(Mathf.RoundToInt(16f + depth * 0.08f));
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en)) continue;
                float v = MaxUnder(level, en.pos, en.half);
                if (v <= 0.12f) continue;
                bool mine = !hostile.ContainsKey(Key(Mathf.FloorToInt(en.pos.x), Mathf.FloorToInt(en.pos.y)));
                en.Shock(0.6f, mine ? p.Dmg(1.5f) : 4f + depth * 0.04f);
            }
        }

        // charged water lights up the dark
        public static void AddLights(Lighting l)
        {
            foreach (var b in bursts)
            {
                float env = Mathf.Clamp01(1f - b.t / b.dur) * (0.6f + 0.4f * Mathf.Sin(b.t * 28f));
                l.Add(new Vector2(b.origin.x + 0.5f, b.origin.y + 0.5f), 3f + b.strength * 3f, 0.8f * env);
                for (int i = 0; i < 3 && b.list.Count > 0; i++)
                {
                    var c = b.list[(i * 97 + (int)(b.t * 10f)) % b.list.Count];
                    l.Add(new Vector2(c.x + 0.5f, c.y + 0.5f), 2.5f, 0.5f * env * b.strength);
                }
            }
        }
    }

    // A crackling arc of lightning between two points (tesla coils, drones), drawn as a jagged chain.
    public class LightningArc : Entity
    {
        readonly Vector2 a, b;
        readonly List<Transform> parts = new List<Transform>();
        float life = 0.25f, redraw;

        public override bool AlwaysActive => true;

        public LightningArc(Vector2 a, Vector2 b)
        {
            this.a = a; this.b = b;
            pos = (a + b) * 0.5f;
            for (int i = 0; i < 8; i++)
            {
                var g = new GameObject("Arc");
                g.transform.SetParent(G.runRoot, false);
                var r = Art.AddSR(g, Art.Square, 30);
                r.color = new Color(0.85f, 0.97f, 1f);
                parts.Add(g.transform);
            }
            Layout();
        }

        void Layout()
        {
            int n = parts.Count;
            Vector2 prev = a;
            Vector2 perp = new Vector2(-(b - a).y, (b - a).x).normalized;
            for (int i = 0; i < n; i++)
            {
                Vector2 next = i == n - 1 ? b : Vector2.Lerp(a, b, (i + 1f) / n) + perp * Random.Range(-0.35f, 0.35f);
                Vector2 d = next - prev;
                var t = parts[i];
                t.position = (Vector3)((prev + next) * 0.5f);
                t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                t.localScale = new Vector3(d.magnitude + 0.05f, 0.1f, 1f);
                prev = next;
            }
        }

        public override void Tick(float dt)
        {
            life -= dt; redraw -= dt;
            if (life <= 0f) { Kill(); return; }
            if (redraw <= 0f) { redraw = 0.05f; Layout(); }
        }

        public override void Kill()
        {
            if (dead) return;
            base.Kill();
            foreach (var t in parts) if (t != null) Object.Destroy(t.gameObject);
        }

        public override void AddLight(Lighting l) => l.Add(pos, 3f, 0.8f * life / 0.25f);
    }
}
