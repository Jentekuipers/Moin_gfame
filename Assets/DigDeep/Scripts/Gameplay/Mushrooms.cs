using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // GIANT MUSHROOMS (the fungal hollows): a pale stalk with a broad violet cap you can stand on (from above, like a
    // ledge) for as long as it's whole. The cap sags under whatever stands on it (the heavier, the deeper) and springs
    // back with a jelly wobble when you get off or jump. Fire burns it away: a burning cap gives off clouds of violet
    // spores, and breathing them makes you see things (see Hallucination below).
    public class MushroomCap : Entity
    {
        public static readonly List<MushroomCap> All = new List<MushroomCap>();
        readonly float footY, height, halfW;
        float disp, dvel;        // how far the cap is pressed down, and how fast it's moving (a damped spring)
        float burnT = -1f, puffT, hp = 12f;
        bool wasOn;
        readonly Transform stem, cap;
        readonly SpriteRenderer stemSr, capSr;
        readonly List<SpriteRenderer> flames = new List<SpriteRenderer>();

        public override bool AlwaysActive => burnT >= 0f;   // (a burning cap always finishes burning, even off-screen)
        // where caps have burnt away this run: the chunk never grows them back (kept in the save, too)
        public static readonly List<Vector2> Burnt = new List<Vector2>();
        public static bool BurntAt(int x, int floorY)
        {
            foreach (var b in Burnt) if (Mathf.Abs(b.x - (x + 0.5f)) < 0.6f && Mathf.Abs(b.y - floorY) < 0.6f) return true;
            return false;
        }
        // water (or any liquid but lava) over the cap: it can't catch, and a burning one goes out
        bool Doused()
        {
            for (int k = -1; k <= 1; k++)
            {
                byte f = Wd.FluidAtPoint(new Vector2(pos.x + k * halfW * 0.6f, Top + 0.15f));
                if (Fluids.IsLiq(f) && f != Fluids.Lava) return true;
            }
            return false;
        }
        public float Top => footY + height + disp;
        public bool Whole => !dead && burnT < 0f;

        public MushroomCap(int x, int floorY, int stalk, float halfW, System.Random r)
        {
            this.halfW = halfW;
            footY = floorY;
            height = stalk;
            pos = new Vector2(x + 0.5f, footY + height);
            MakeGO("Mushroom", null, 3);
            var sg = new GameObject("Stalk"); sg.transform.SetParent(go.transform, false);
            stemSr = Art.AddSR(sg, Stalk(), 3); stem = sg.transform;
            var cg = new GameObject("Cap"); cg.transform.SetParent(go.transform, false);
            capSr = Art.AddSR(cg, Cap(Mathf.RoundToInt(halfW * 32f), r.Next(1000)), 4); cap = cg.transform;
            All.Add(this);
            Render();
        }

        // ---- standing on it (the world asks: see World.Ground / MoveY)
        public static bool Under(Vector2 p, Vector2 h, float feetFrom, float feetTo, out float top)
        {
            top = 0f;
            if (World.DropThrough) return false;
            foreach (var m in All)
            {
                if (!m.Whole) continue;
                if (p.x + h.x <= m.pos.x - m.halfW || p.x - h.x >= m.pos.x + m.halfW) continue;
                float t = m.Top;
                if (feetFrom >= t - 0.06f && feetTo <= t + 0.001f) { top = t; return true; }
            }
            return false;
        }

        bool Riding(Vector2 p, Vector2 h, float vy)
        {
            if (vy > 0.5f) return false;
            if (p.x + h.x <= pos.x - halfW || p.x - h.x >= pos.x + halfW) return false;
            float feet = p.y - h.y;
            return Mathf.Abs(feet - Top) < 0.12f;
        }

        public override void Tick(float dt)
        {
            if (burnT >= 0f) { Burn(dt); if (dead) return; }
            else
            {
                // fire under or on it sets it alight
                int cx = Mathf.FloorToInt(pos.x), ty = Mathf.FloorToInt(Top);
                for (int dx = -1; dx <= 1 && burnT < 0f; dx++)
                    for (int dy = -1; dy <= 0; dy++)
                        if (Fire.IsBurning(cx + dx, ty + dy)) { Ignite(); break; }
                // fire shots (and flames) set it alight too
                if (burnT < 0f)
                    foreach (var e in G.ents)
                        if (!e.dead && e is Bullet b && b.Burns && Mathf.Abs(b.pos.x - pos.x) < halfW && b.pos.y > Top - 0.4f && b.pos.y < Top + 0.6f) { Ignite(); break; }
            }

            // what's standing on it, and how heavy that is
            float load = 0f;
            var pl = G.player;
            bool plOn = pl.mode != Player.Mode.Dead && Whole && Riding(pl.pos, Player.Half, pl.vel.y);
            if (plOn) load += 1f;
            foreach (var e in G.ents)
                if (!e.dead && e is Enemy en && !(en is Boss) && Whole && Riding(en.pos, en.half, en.vel.y)) load += Mathf.Clamp(en.half.x * en.half.y * 6f, 0.3f, 2f);
            // the spring: pressed down by the load, then back up with a wobble
            float target = -Mathf.Min(1.1f, load * 0.38f);
            float before = Top;
            dvel += ((target - disp) * 70f - dvel * 5.5f) * dt;
            disp += dvel * dt;
            disp = Mathf.Clamp(disp, -1.3f, 0.5f);
            // carry whoever stands on it up and down with it
            float moved = Top - before;
            if (plOn && Mathf.Abs(moved) > 0f)
            {
                var np = new Vector2(pl.pos.x, Top + Player.Half.y);
                if (!Wd.BoxSolid(np, Player.Half)) pl.pos = np;
            }
            // a jump off it kicks it down, and it bounces back up
            if (wasOn && !plOn && pl.vel.y > 4f) dvel -= 3.5f;
            wasOn = plOn;
            Render();
        }

        void Render()
        {
            float h = Mathf.Max(0.3f, height + disp);
            go.transform.position = new Vector3(pos.x, footY, 0f);
            stem.localPosition = new Vector3(0f, 0f, 0f);
            stem.localScale = new Vector3(1f, h / Mathf.Max(0.1f, stemSr.sprite.bounds.size.y), 1f);
            float squash = Mathf.Clamp(-disp, -0.4f, 1f) * 0.18f;
            cap.localPosition = new Vector3(0f, h - 0.1f, 0f);
            cap.localScale = new Vector3(1f + squash, 1f - squash * 1.3f, 1f);
        }

        // ---- fire: it burns away, and its spores go up in violet clouds
        public void Ignite()
        {
            if (burnT >= 0f || dead || Doused()) return;
            burnT = 0f;
            G.sfx.Play("ignite", 0.45f, 0.8f, 0.1f);
            for (int k = 0; k < 3; k++)
            {
                var f = new GameObject("CapFire"); f.transform.SetParent(cap, false);
                f.transform.localPosition = new Vector3((k - 1) * halfW * 0.6f, 0.25f, 0f);
                var fs = Art.AddSR(f, Art.Flames[k % Art.Flames.Length], 6);
                f.transform.localScale = Vector3.one * 0.8f;
                flames.Add(fs);
            }
        }

        public void Blast(float dmg)
        {
            hp -= dmg;
            if (hp <= 0f) Ignite();
            else dvel -= Mathf.Min(12f, dmg * 2f);   // knocked: it wobbles
        }

        void Burn(float dt)
        {
            if (Doused())
            {
                // put out: the flames die, the char stays
                burnT = -1f;
                foreach (var f in flames) if (f != null) Object.Destroy(f.gameObject);
                flames.Clear();
                G.fx.Burst(new Vector2(pos.x, Top + 0.3f), 8, new Color(0.85f, 0.85f, 0.9f, 0.7f), 2f, 0.6f, -3f, 1f, false);
                G.sfx.Play("fizz", 0.4f, 1f, 0.1f);
                return;
            }
            burnT += dt; puffT -= dt;
            foreach (var f in flames) if (f != null) f.sprite = Art.Flames[(int)(Time.time * 12f + f.GetInstanceID()) % Art.Flames.Length];
            capSr.color = Color.Lerp(Color.white, new Color(0.3f, 0.2f, 0.25f), burnT / 2.2f);
            if (puffT <= 0f)
            {
                puffT = 0.18f;
                G.Spawn(new SporeVapor(new Vector2(pos.x + Random.Range(-halfW, halfW), Top + 0.2f)));
            }
            if (Random.value < 0.3f) G.fx.Burst(new Vector2(pos.x + Random.Range(-halfW, halfW), Top), 1, new Color(1f, 0.6f, 0.2f), 1.2f, 0.5f, -5f, 1f, false);
            if (burnT > 2.2f)
            {
                // burnt through: it crumbles, and a last big cloud of spores bursts out
                for (int k = 0; k < 5; k++) G.Spawn(new SporeVapor(new Vector2(pos.x + Random.Range(-halfW, halfW), Top + Random.Range(0f, 0.6f))));
                G.fx.Burst(new Vector2(pos.x, Top), 18, new Color(0.45f, 0.3f, 0.4f), 4f, 0.6f, 12f);
                G.sfx.Play("break", 0.5f, 0.7f);
                Burnt.Add(new Vector2(pos.x, footY));
                Kill();
            }
        }

        public override void AddLight(Lighting l)
        {
            if (burnT >= 0f) l.Add(new Vector2(pos.x, Top + 0.3f), 3f, 0.8f);
            else l.Add(new Vector2(pos.x, Top), 1.6f, 0.3f);   // the cap's underside glows faintly
        }

        public override void Kill()
        {
            base.Kill();
            All.Remove(this);
        }

        // ---- the pictures
        static Sprite stalkSpr;
        static Sprite Stalk()
        {
            if (stalkSpr != null) return stalkSpr;
            int w = 6, h = 16;
            var px = new Color32[w * h];
            Color32 o = new Color32(24, 12, 34, 255), a = new Color32(226, 214, 196, 255), b = new Color32(184, 168, 160, 255), c = new Color32(140, 120, 130, 255);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = x == 0 || x == w - 1 ? o : x == 1 ? a : x == w - 2 ? c : (y % 5 == 0 ? c : b);
            stalkSpr = Make(px, w, h, new Vector2(0.5f, 0f));
            return stalkSpr;
        }

        static Sprite Cap(int w, int seed)
        {
            w = Mathf.Max(24, w);
            int h = 10;
            var px = new Color32[w * h];
            Color32 o = new Color32(24, 12, 34, 255), top = new Color32(214, 92, 196, 255), mid = new Color32(168, 64, 168, 255),
                    dark = new Color32(110, 40, 120, 255), under = new Color32(240, 170, 230, 255), spot = new Color32(255, 236, 246, 255);
            var r = new System.Random(seed);
            float cx = (w - 1) * 0.5f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x - cx) / (w * 0.5f);
                    float edge = (h - 2) * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u * u * u)) + 2f;   // a flat-topped dome
                    if (y > edge) continue;
                    Color32 c = y <= 1 ? under : y > edge - 2 ? top : y > edge - 4 ? mid : dark;
                    if (y > edge - 1.2f || Mathf.Abs(u) > 0.97f || y == 0) c = o;
                    px[y * w + x] = c;
                }
            // pale spots
            for (int k = 0; k < w / 6; k++)
            {
                int sx = 2 + r.Next(w - 4), sy = 4 + r.Next(h - 6);
                if (px[sy * w + sx].a > 0 && px[sy * w + sx].r != 24) { px[sy * w + sx] = spot; if (sx + 1 < w && px[sy * w + sx + 1].a > 0 && px[sy * w + sx + 1].r != 24) px[sy * w + sx + 1] = spot; }
            }
            return Make(px, w, h, new Vector2(0.5f, 0.1f));
        }

        static Sprite Make(Color32[] px, int w, int h, Vector2 pivot)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, Art.PPU, 0, SpriteMeshType.FullRect);
        }
    }

    // A cloud of violet spores off a burning mushroom: it drifts up and spreads out, and breathing it in makes you
    // hallucinate (the longer you stand in it, the worse, up to a cap)
    public class SporeVapor : Entity
    {
        float t, life = Random.Range(2.6f, 3.6f), size = 0.5f;
        Vector2 vel = new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.4f, 0.9f));

        public override bool AlwaysActive => true;

        public SporeVapor(Vector2 at)
        {
            pos = at;
            MakeGO("Spores", Art.Circle, 26);
            sr.color = new Color(0.65f, 0.35f, 0.95f, 0f);
        }

        public override void Tick(float dt)
        {
            t += dt;
            if (t > life) { Kill(); return; }
            vel *= 1f - 0.6f * dt;
            vel.y += 0.15f * dt;
            pos += vel * dt;
            size = Mathf.Lerp(0.5f, 1.4f, t / life);
            float a = Mathf.Min(1f, t * 3f) * (1f - t / life) * 0.45f;
            sr.color = new Color(0.62f + 0.1f * Mathf.Sin(t * 5f), 0.35f, 0.95f, a);
            go.transform.localScale = Vector3.one * size * 1.4f;
            Place();
            var pl = G.player;
            if (pl.mode != Player.Mode.Dead && (pl.pos - pos).sqrMagnitude < size * size * 0.6f)
                Hallucination.Breathe(dt);
        }
    }

    // HALLUCINATION: what the spores do. The colours swim, the view sways, and the creatures stop looking like
    // themselves (each shows up as some other creature). It builds the longer you breathe the spores (to a cap) and
    // fades away on its own over a few seconds.
    public static class Hallucination
    {
        public static float Level;        // 0..1
        const float Cap = 1f, Fade = 0.16f;

        public static void Breathe(float dt) => Level = Mathf.Min(Cap, Level + dt * 0.55f);
        public static void Tick(float dt) => Level = Mathf.Max(0f, Level - dt * Fade);
        public static void Reset() => Level = 0f;
        public static bool Seeing => Level > 0.3f;

        // the swirling colour wash over the whole screen (drawn last, over everything)
        public static void Draw()
        {
            if (Level <= 0.01f) return;
            float t = Time.time;
            float W = Screen.width, H = Screen.height;
            var old = GUI.color;
            // a hue-cycling wash
            Color wash = Color.HSVToRGB(Mathf.Repeat(t * 0.12f, 1f), 0.8f, 1f);
            wash.a = 0.14f * Level;
            GUI.color = wash;
            GUI.DrawTexture(new Rect(0, 0, W, H), Art.White);
            // drifting bands of other colours, rolling down the screen
            int bands = 6;
            for (int i = 0; i < bands; i++)
            {
                float y = Mathf.Repeat(t * 40f + i * H / bands, H + 80f) - 40f;
                Color c = Color.HSVToRGB(Mathf.Repeat(t * 0.2f + i * 0.17f, 1f), 0.9f, 1f);
                c.a = 0.08f * Level;
                GUI.color = c;
                float bh = H / bands * (0.4f + 0.2f * Mathf.Sin(t * 1.3f + i));
                GUI.DrawTexture(new Rect(Mathf.Sin(t * 0.9f + i) * 30f * Level, y, W, bh), Art.White);
            }
            GUI.color = old;
        }

        // the view sways and breathes (CameraRig)
        public static float Roll => Level * 4.5f * Mathf.Sin(Time.time * 1.4f) + Level * 1.5f * Mathf.Sin(Time.time * 3.7f);
        public static float Zoom => 1f + Level * 0.05f * Mathf.Sin(Time.time * 2.1f);

        // each creature seen as some other creature (stable while it lasts)
        static readonly string[] Disguises = { "slime2", "turtle", "wolf", "bat", "mushelite", "toxictoad", "skeleton", "crawler", "bandit_axe_0", "glider" };
        public static string DisguiseFor(int id) => Disguises[(id & 0x7fffffff) % Disguises.Length];
    }
}
