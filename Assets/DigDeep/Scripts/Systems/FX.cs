using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Pooled pixel particles + flashes. Kept deliberately cheap: one sprite renderer per particle.
    // Blood and goo particles stick to whatever wall or floor they hit and stain it for good.
    public class FX
    {
        class P
        {
            public GameObject go;
            public Transform t;
            public SpriteRenderer sr;
            public Vector2 p, v;
            public float life, max, g, drag;
            public Color c;
            public bool on, collide, stain, keep;
            public int stainR;
        }

        class Fl
        {
            public GameObject go;
            public Transform t;
            public SpriteRenderer sr;
            public float life, max, s0, s1;
            public Color c;
            public bool on, ghost;
        }

        readonly List<P> parts = new List<P>();
        readonly List<Fl> flashes = new List<Fl>();
        int nextP, nextF;

        public FX(Transform root)
        {
            var holder = new GameObject("FX").transform;
            holder.SetParent(root, false);
            for (int i = 0; i < 1400; i++)
            {
                var go = new GameObject("p");
                go.transform.SetParent(holder, false);
                var sr = Art.AddSR(go, Art.Pixel, 25);
                go.SetActive(false);
                parts.Add(new P { go = go, t = go.transform, sr = sr });
            }
            for (int i = 0; i < 120; i++)
            {
                var go = new GameObject("f");
                go.transform.SetParent(holder, false);
                var sr = Art.AddSR(go, Art.Square, 26);
                go.SetActive(false);
                flashes.Add(new Fl { go = go, t = go.transform, sr = sr });
            }
        }

        P GetP()
        {
            // long-lived casings / smoke don't get stolen while short sparks are around
            for (int tries = 0; tries < 8; tries++)
            {
                var q = parts[nextP];
                nextP = (nextP + 1) % parts.Count;
                if (q.on && q.keep && tries < 7) continue;
                q.on = true;
                q.stain = false; q.keep = false; q.drag = 0f; q.stainR = 1;
                q.sr.sortingOrder = 25;
                if (!q.go.activeSelf) q.go.SetActive(true);
                return q;
            }
            return parts[0];
        }

        public void Burst(Vector2 at, int n, Color c, float speed, float life = 0.5f, float grav = 30f, float size = 1f, bool collide = true)
        {
            for (int i = 0; i < n; i++)
            {
                var p = GetP();
                p.p = at + new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(-0.15f, 0.15f));
                float a = Random.value * Mathf.PI * 2f;
                float s = speed * (0.3f + Random.value * 0.7f);
                p.v = new Vector2(Mathf.Cos(a) * s, Mathf.Sin(a) * s + speed * 0.3f);
                p.life = p.max = life * (0.6f + Random.value * 0.6f);
                p.g = grav;
                p.c = c;
                p.collide = collide;
                p.sr.color = c;
                float sz = size * (Random.value < 0.35f ? 2f : 1f);
                p.t.localScale = new Vector3(sz, sz, 1f);
            }
        }

        // Blood, slime, sap: sprays mostly along `dir`, sticks to walls and floors where it lands.
        public void Blood(Vector2 at, Vector2 dir, Color c, int n)
        {
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
            float baseA = Mathf.Atan2(dir.y, dir.x);
            for (int i = 0; i < n; i++)
            {
                var p = GetP();
                p.p = at + Random.insideUnitCircle * 0.15f;
                float a = baseA + Random.Range(-1.1f, 1.1f);
                float s = Random.Range(3f, 15f);
                p.v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s + Vector2.up * Random.Range(0f, 3f);
                p.life = p.max = Random.Range(1.2f, 2.2f);
                p.g = 32f;
                float k = Random.Range(0.7f, 1.15f);
                p.c = new Color(c.r * k, c.g * k, c.b * k, 1f);
                p.collide = true;
                p.stain = true;
                p.stainR = Random.value < 0.3f ? 2 : 1;
                p.sr.color = p.c;
                float sz = Random.value < 0.4f ? 2f : 1f;
                p.t.localScale = new Vector3(sz, sz, 1f);
            }
        }

        // Chunky bits flung from a kill.
        public void Gibs(Vector2 at, Vector2 dir, Color c, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var p = GetP();
                p.p = at + Random.insideUnitCircle * 0.2f;
                Vector2 d = (dir + Random.insideUnitCircle * 0.9f).normalized;
                p.v = d * Random.Range(6f, 16f) + Vector2.up * 4f;
                p.life = p.max = Random.Range(3f, 6f);
                p.g = 38f;
                p.drag = 1.5f;
                p.c = new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f, 1f);
                p.collide = true;
                p.keep = true;
                p.sr.color = p.c;
                p.t.localScale = new Vector3(3f, 2f, 1f);
            }
            Blood(at, dir, c, n);
        }

        // Brass shell casings: they clink down and lie around for a while.
        public void Casing(Vector2 at, Vector2 v)
        {
            var p = GetP();
            p.p = at;
            p.v = v;
            p.life = p.max = Random.Range(6f, 9f);
            p.g = 40f;
            p.drag = 2f;
            p.c = new Color(1f, 0.8f, 0.3f);
            p.collide = true;
            p.keep = true;
            p.sr.color = p.c;
            p.sr.sortingOrder = 9;
            p.t.localScale = new Vector3(2f, 1f, 1f);
        }

        // An empty magazine clattering to the floor after a reload.
        public void Magazine(Vector2 at, Vector2 v)
        {
            var p = GetP();
            p.p = at;
            p.v = v;
            p.life = p.max = Random.Range(7f, 10f);
            p.g = 40f;
            p.drag = 2f;
            p.c = new Color(0.32f, 0.33f, 0.38f);
            p.collide = true;
            p.keep = true;
            p.sr.color = p.c;
            p.sr.sortingOrder = 9;
            p.t.localScale = new Vector3(2f, 4f, 1f);
        }

        // Drifting grey smoke that rises and fades.
        public void Smoke(Vector2 at, int n, float size)
        {
            for (int i = 0; i < n; i++)
            {
                var p = GetP();
                p.p = at + Random.insideUnitCircle * 0.2f * size;
                p.v = new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(0.3f, 1.4f)) * size;
                p.life = p.max = Random.Range(0.9f, 2.2f) * Mathf.Max(0.6f, size * 0.7f);
                p.g = -1.2f;
                p.drag = 0.8f;
                float gcol = Random.Range(0.32f, 0.55f);
                p.c = new Color(gcol, gcol, gcol * 1.05f, 0.55f);
                p.collide = false;
                p.keep = size > 1.5f;
                p.sr.color = p.c;
                p.sr.sortingOrder = 24;
                float sz = Random.Range(2f, 4f) * size;
                p.t.localScale = new Vector3(sz, sz, 1f);
            }
        }

        public void Debris(Vector2 at, Color32 c, int n)
        {
            Color baseC = c;
            for (int i = 0; i < n; i++)
            {
                float k = Random.Range(0.6f, 1.25f);
                var p = GetP();
                p.p = at + Random.insideUnitCircle * 0.3f;
                float a = Random.value * Mathf.PI * 2f;
                float s = Random.Range(2f, 8f);
                p.v = new Vector2(Mathf.Cos(a) * s, Mathf.Sin(a) * s + 2.5f);
                // most rubble vanishes fast, some of it stays on the floor for a while
                bool stays = Random.value < 0.3f;
                p.life = p.max = stays ? Random.Range(3f, 6f) : Random.Range(0.4f, 0.8f);
                p.keep = stays;
                p.g = 42f;
                p.drag = 1f;
                p.c = new Color(baseC.r * k, baseC.g * k, baseC.b * k);
                p.collide = true;
                p.sr.color = p.c;
                float sz = Random.value < 0.5f ? 2f : 1.5f;
                p.t.localScale = new Vector3(sz, sz, 1f);
            }
            Burst(at, 4, new Color(0.9f, 0.85f, 0.75f, 0.6f), 3f, 0.35f, 4f, 1f, false);
        }

        public void Sparks(Vector2 at, Color c) => Burst(at, 7, c, 9f, 0.22f, 12f, 1f);

        Fl GetF()
        {
            var f = flashes[nextF];
            nextF = (nextF + 1) % flashes.Count;
            f.on = true;
            f.ghost = false;
            f.sr.flipX = false;
            if (!f.go.activeSelf) f.go.SetActive(true);
            return f;
        }

        void Flash(Vector2 at, Sprite s, float s0, float s1, Color c, float life, int order)
        {
            var f = GetF();
            f.sr.sprite = s;
            f.sr.sortingOrder = order;
            f.t.position = new Vector3(at.x, at.y, 0);
            f.t.rotation = Quaternion.identity;
            f.s0 = s0; f.s1 = s1; f.c = c; f.life = f.max = life;
            f.t.localScale = new Vector3(s0, s0, 1f);
            f.sr.color = c;
        }

        // The white frame on a block you just hit: the single most important "punch" cue.
        public void TileFlash(Vector2Int cell) =>
            Flash(new Vector2(cell.x + 0.5f, cell.y + 0.5f), Art.Square, 1.05f, 0.85f, new Color(1f, 1f, 1f, 0.9f), 0.07f, 3);

        public void Ring(Vector2 at, float r, Color c) => Flash(at, Art.Ring, 0.3f, r * 2f, c, 0.25f, 27);

        // A big, blinding ball of light at the muzzle for a frame or two.
        public void Muzzle(Vector2 at, float size, Color c)
        {
            Flash(at, Art.Circle, size * 1.1f, size * 0.5f, Color.white, 0.05f, 28);
            Flash(at, Art.Circle, size * 1.5f, size * 0.7f, new Color(c.r, c.g, c.b, 0.8f), 0.07f, 27);
        }

        // An after-image of a sprite (dashing).
        public void Ghost(Sprite s, Vector3 at, bool flip, Color c)
        {
            var f = GetF();
            f.ghost = true;
            f.sr.sprite = s;
            f.sr.flipX = flip;
            f.sr.sortingOrder = 19;
            f.t.position = at;
            f.t.rotation = Quaternion.identity;
            f.s0 = 1f; f.s1 = 1f; f.c = c; f.life = f.max = 0.2f;
            f.t.localScale = Vector3.one;
            f.sr.color = c;
        }

        // Explosions are deliberately way bigger than they need to be.
        public void Explosion(Vector2 at, float r)
        {
            Flash(at, Art.Circle, r * 3f, r * 1.6f, new Color(1f, 1f, 0.9f, 1f), 0.08f, 28);
            Flash(at, Art.Circle, r * 2.2f, r * 3.2f, new Color(1f, 0.6f, 0.15f, 0.9f), 0.25f, 27);
            Flash(at, Art.Circle, r * 1.4f, r * 2.6f, new Color(0.9f, 0.25f, 0.08f, 0.7f), 0.35f, 26);
            Ring(at, r * 1.6f, new Color(1f, 0.85f, 0.45f));
            Burst(at, 30 + (int)(r * 14), new Color(1f, 0.6f, 0.15f), 13f * Mathf.Sqrt(r), 0.6f, 8f, 2f);
            Burst(at, 20, new Color(1f, 0.95f, 0.55f), 8f, 0.35f, 0f, 1.5f);
            Smoke(at, 8 + (int)(r * 5), 1.2f + r * 0.4f);
            // burning embers that fall and settle
            for (int i = 0; i < 6 + r * 3; i++)
            {
                var p = GetP();
                p.p = at + Random.insideUnitCircle * r * 0.4f;
                p.v = Random.insideUnitCircle * 9f + Vector2.up * 6f;
                p.life = p.max = Random.Range(1f, 2.5f);
                p.g = 25f;
                p.c = new Color(1f, Random.Range(0.4f, 0.7f), 0.1f);
                p.collide = true;
                p.sr.color = p.c;
                p.t.localScale = new Vector3(1.5f, 1.5f, 1f);
            }
        }

        public void Tick(float dt)
        {
            var w = Game.I.world;
            foreach (var p in parts)
            {
                if (!p.on) continue;
                p.life -= dt;
                if (p.life <= 0f) { p.on = false; p.go.SetActive(false); continue; }
                p.v.y -= p.g * dt;
                if (p.drag > 0f) p.v *= 1f - Mathf.Min(1f, p.drag * dt);
                Vector2 np = p.p + p.v * dt;
                if (p.collide && w.Solid(Mathf.FloorToInt(np.x), Mathf.FloorToInt(np.y)))
                {
                    if (p.stain)
                    {
                        // splat: paint the wall and vanish
                        w.Stain(np, p.c, p.stainR);
                        p.on = false; p.go.SetActive(false);
                        continue;
                    }
                    if (w.Solid(Mathf.FloorToInt(p.p.x), Mathf.FloorToInt(np.y))) { p.v.y *= -0.3f; p.v.x *= 0.6f; np.y = p.p.y; }
                    else { p.v.x *= -0.4f; np.x = p.p.x; }
                    if (Mathf.Abs(p.v.y) < 1f) p.v.y = 0f;
                }
                p.p = np;
                float a = Mathf.Clamp01(p.life / (p.max * 0.35f));
                var c = p.c; c.a *= a;
                p.sr.color = c;
                p.t.position = new Vector3(U.Snap(p.p.x), U.Snap(p.p.y), 0);
            }
            foreach (var f in flashes)
            {
                if (!f.on) continue;
                f.life -= dt;
                if (f.life <= 0f) { f.on = false; f.go.SetActive(false); continue; }
                float k = 1f - f.life / f.max;
                float s = Mathf.Lerp(f.s0, f.s1, k);
                f.t.localScale = new Vector3(s, s, 1f);
                var c = f.c; c.a *= f.ghost ? 1f - k : 1f - k * k;
                f.sr.color = c;
            }
        }
    }
}
