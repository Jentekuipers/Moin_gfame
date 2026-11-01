using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // THE CLOUD REACHES' weather.
    //  - A broken (or burned) cloud block drifts back together after about two seconds: a small puff fades in and
    //    swells where it was, and only becomes solid again once nothing is in the way.
    //  - Now and then a cloud with open air under it darkens into a rain cloud. The darkness runs through the whole
    //    cloud it belongs to (a fifth of a second per block, the higher parts too), and the bottom blocks rain for about
    //    three seconds before it all clears.
    public static class CloudWeather
    {
        const float RegrowTime = 2f, RainTime = 3f, Spread = 0.2f;
        static World Wd => Game.I != null ? Game.I.world : null;
        public static bool Restoring;

        static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

        // ------------------------------------------------------------------ regrowing
        static readonly Dictionary<long, CloudPuff> pending = new Dictionary<long, CloudPuff>();

        public static void Gone(int x, int y)
        {
            if (Restoring || Game.I == null || Game.I.world == null) return;
            long k = Key(x, y);
            if (pending.ContainsKey(k)) return;
            var p = new CloudPuff(x, y, RegrowTime);
            pending[k] = p;
            Game.I.Spawn(p);
        }

        public static void Done(int x, int y) => pending.Remove(Key(x, y));

        // fire touching a cloud: it boils away into steam (and comes back later all the same)
        public static void Evaporate(int x, int y)
        {
            var w = Wd;
            if (w == null) return;
            w.Set(x, y, Block.Air);
            w.AddSteam(x, y, 0.7f);
            Game.I.fx.Burst(new Vector2(x + 0.5f, y + 0.5f), 6, new Color(0.9f, 0.95f, 1f), 2f, 0.6f, -3f, 1f, false);
            Game.I.sfx.PlayAt(new Vector2(x + 0.5f, y + 0.5f), "fizz", 0.3f, 1.2f);
        }

        // ------------------------------------------------------------------ rain clouds
        struct Rain { public float start, end; }
        static readonly Dictionary<long, Rain> rain = new Dictionary<long, Rain>();
        static readonly Dictionary<long, int> shown = new Dictionary<long, int>();
        static float clock, checkT, dropT;
        static readonly List<long> scratch = new List<long>();

        public static void Reset() { pending.Clear(); rain.Clear(); shown.Clear(); clock = 0f; checkT = 3f; }

        public static float Dark(int x, int y)
        {
            if (!rain.TryGetValue(Key(x, y), out var r)) return 0f;
            float a = Mathf.Clamp01((clock - r.start) / 0.3f), b = Mathf.Clamp01((r.end - clock) / 0.5f);
            return Mathf.Min(a, b);
        }

        public static void Tick(float dt)
        {
            var w = Wd;
            var g = Game.I;
            if (w == null || g == null || g.player == null) return;
            clock += dt;
            // the odd cloud with air under it near you starts to rain (not often)
            checkT -= dt;
            if (checkT <= 0f)
            {
                checkT = 1f;
                Vector2 c = g.cam.pos;
                if (Biomes.Index(-Mathf.FloorToInt(c.y)) == Biomes.Cloud && Random.value < 0.22f)
                    for (int tries = 0; tries < 30; tries++)
                    {
                        int x = Mathf.FloorToInt(c.x + Random.Range(-g.cam.halfW, g.cam.halfW)), y = Mathf.FloorToInt(c.y + Random.Range(-g.cam.halfH, g.cam.halfH));
                        if (!IsCloud(w.Get(x, y)) || w.Solid(x, y - 1) || rain.ContainsKey(Key(x, y))) continue;
                        StartRain(w, x, y);
                        break;
                    }
            }
            if (rain.Count == 0) return;
            // the rain falls from the bottoms of the dark clouds; clouds that clear (or are broken) drop out
            dropT -= dt;
            bool drip = dropT <= 0f;
            if (drip) dropT = 0.12f;
            scratch.Clear();
            foreach (var kv in rain)
            {
                int x = (int)(kv.Key >> 32), y = (int)(uint)kv.Key;
                if (clock > kv.Value.end || !IsCloud(w.Get(x, y))) { scratch.Add(kv.Key); continue; }
                int lvl = Mathf.RoundToInt(Dark(x, y) * 2f);
                if (!shown.TryGetValue(kv.Key, out int was) || was != lvl) { shown[kv.Key] = lvl; w.RedrawCell(x, y); }
                if (drip && lvl == 2 && !w.Solid(x, y - 1) && Random.value < 0.18f) Fluids.RainDrop(new Vector2(x + Random.value, y - 0.05f));
            }
            foreach (long k in scratch)
            {
                rain.Remove(k); shown.Remove(k);
                w.RedrawCell((int)(k >> 32), (int)(uint)k);
            }
        }

        static bool IsCloud(Block b) => b == Block.Cloud || b == Block.CloudGold;

        // the darkness spreads out through the whole cloud from the block that started it
        static void StartRain(World w, int x0, int y0)
        {
            var q = new Queue<(int x, int y, int d)>();
            var seen = new HashSet<long> { Key(x0, y0) };
            q.Enqueue((x0, y0, 0));
            while (q.Count > 0 && seen.Count < 90)
            {
                var (x, y, d) = q.Dequeue();
                float s = clock + d * Spread;
                rain[Key(x, y)] = new Rain { start = s, end = s + RainTime };
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    long nk = Key(nx, ny);
                    if (seen.Contains(nk) || !IsCloud(w.Get(nx, ny))) continue;
                    seen.Add(nk);
                    q.Enqueue((nx, ny, d + 1));
                }
            }
            Game.I.sfx.Play("rumble", 0.25f, 1.6f);
        }
    }

    // A cloud block drifting back together: a little puff that fades in and swells, then turns solid when the way is clear.
    public class CloudPuff : Entity
    {
        readonly int cx, cy;
        float t;
        readonly float time;
        static Sprite pic;

        public override bool AlwaysActive => true;

        public CloudPuff(int x, int y, float time)
        {
            cx = x; cy = y; this.time = time;
            pos = new Vector2(x + 0.5f, y + 0.5f);
            MakeGO("CloudPuff", Pic(), 5);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            go.transform.localScale = Vector3.one * 0.2f;
            sr.color = new Color(1f, 1f, 1f, 0f);
        }

        static Sprite Pic()
        {
            if (pic != null) return pic;
            var px = new Color32[16 * 16];
            BiomeTiles.Cloud(px, 16, 0, 0, 0, 0, TileDraw.UP | TileDraw.DOWN | TileDraw.LEFT | TileDraw.RIGHT, false);
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixels32(px); tex.Apply();
            pic = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), Art.PPU);
            return pic;
        }

        public override void Tick(float dt)
        {
            var w = Game.I.world;
            Block here = w.Get(cx, cy);
            if (here != Block.Air) { CloudWeather.Done(cx, cy); Kill(); return; }   // something else filled the hole
            t += dt;
            float k = Mathf.Clamp01(t / time);
            float e = k * k * (3f - 2f * k);
            sr.color = new Color(1f, 1f, 1f, 0.15f + 0.75f * e);
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 0.95f, e) * (1f + Mathf.Sin(t * 5f) * 0.03f);
            if (Random.value < dt * 3f) G.fx.Burst(pos + Random.insideUnitCircle * 0.5f, 1, new Color(0.92f, 0.96f, 1f, 0.7f), 0.5f, 0.6f, 0f, 1f, false);
            if (k < 1f) return;
            // solid again only once nothing stands in it
            var p = G.player;
            if (p != null && p.mode != Player.Mode.Dead && U.Overlap(pos, new Vector2(0.5f, 0.5f), p.pos, Player.Half)) return;
            foreach (var e2 in G.ents)
                if (!e2.dead && e2 is Enemy en && !(en is Boss) && U.Overlap(pos, new Vector2(0.5f, 0.5f), en.pos, en.half)) return;
            CloudWeather.Restoring = true;
            try { w.Set(cx, cy, Block.Cloud); }
            finally { CloudWeather.Restoring = false; }
            CloudWeather.Done(cx, cy);
            Kill();
        }
    }
}
