using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // CPU light map: a low-res darkness texture with holes punched by light sources.
    // Pipeline-agnostic (works in URP 2D, URP 3D or built-in) since it's just an alpha-blended sprite.
    public class Lighting
    {
        public const int RES = 4;   // light texels per tile
        Texture2D tex;
        Color32[] buf;
        float[] lum;
        int tw, th;
        Vector2 origin;
        readonly GameObject go;
        readonly SpriteRenderer sr;

        struct FlashL { public Vector2 p; public float r, t, max; }
        readonly List<FlashL> flashes = new List<FlashL>();

        public Lighting(Transform root)
        {
            go = new GameObject("Darkness");
            go.transform.SetParent(root, false);
            sr = Art.AddSR(go, null, 100);
        }

        void Ensure(int w, int h)
        {
            if (tex != null && w == tw && h == th) return;
            if (tex != null) { Object.Destroy(sr.sprite); Object.Destroy(tex); }
            tw = w; th = h;
            tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            buf = new Color32[tw * th];
            lum = new float[tw * th];
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, tw, th), Vector2.zero, RES, 0, SpriteMeshType.FullRect);
        }

        public void Begin(Vector2 camPos, float halfW, float halfH)
        {
            int w = Mathf.CeilToInt(halfW * 2f + 4f) * RES, h = Mathf.CeilToInt(halfH * 2f + 4f) * RES;
            Ensure(w, h);
            origin = new Vector2(Mathf.Floor(camPos.x - halfW - 2f), Mathf.Floor(camPos.y - halfH - 2f));
            System.Array.Clear(lum, 0, lum.Length);
        }

        public bool InView(Vector2 p, float margin) =>
            p.x > origin.x - margin && p.y > origin.y - margin && p.x < origin.x + tw / (float)RES + margin && p.y < origin.y + th / (float)RES + margin;

        public void Add(Vector2 p, float r, float intensity = 1f)
        {
            if (r <= 0f || lum == null) return;
            float lx = (p.x - origin.x) * RES, ly = (p.y - origin.y) * RES, rr = r * RES;
            int x0 = Mathf.Max(0, (int)(lx - rr)), x1 = Mathf.Min(tw - 1, (int)(lx + rr));
            int y0 = Mathf.Max(0, (int)(ly - rr)), y1 = Mathf.Min(th - 1, (int)(ly + rr));
            if (x0 > x1 || y0 > y1) return;
            float inv = 1f / (rr * rr);
            for (int y = y0; y <= y1; y++)
            {
                float dy = y + 0.5f - ly;
                int row = y * tw;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - lx;
                    float d = (dx * dx + dy * dy) * inv;
                    if (d >= 1f) continue;
                    float v = (1f - d);
                    v = v * v * (3f - 2f * v) * intensity;
                    if (v > lum[row + x]) lum[row + x] = v;
                }
            }
        }

        // A light that only reaches what you can see: vis holds how visible each cell is (0..1), vw x vh cells from (vx0, vy0).
        // It's sampled smoothly between cell centres, so the edges fade out in texel steps rather than cutting off at a cell.
        public void AddVis(Vector2 p, float r, float intensity, float[] vis, int vx0, int vy0, int vw, int vh)
        {
            if (r <= 0f || lum == null) return;
            float lx = (p.x - origin.x) * RES, ly = (p.y - origin.y) * RES, rr = r * RES;
            int x0 = Mathf.Max(0, (int)(lx - rr)), x1 = Mathf.Min(tw - 1, (int)(lx + rr));
            int y0 = Mathf.Max(0, (int)(ly - rr)), y1 = Mathf.Min(th - 1, (int)(ly + rr));
            if (x0 > x1 || y0 > y1) return;
            float inv = 1f / (rr * rr);
            for (int y = y0; y <= y1; y++)
            {
                float wy = origin.y + (y + 0.5f) / RES - vy0 - 0.5f;   // in cell-centre space
                int cy = Mathf.FloorToInt(wy); float fy = wy - cy;
                if (cy < 0 || cy + 1 >= vh) continue;
                float dy = y + 0.5f - ly;
                int row = y * tw;
                for (int x = x0; x <= x1; x++)
                {
                    float wx = origin.x + (x + 0.5f) / RES - vx0 - 0.5f;
                    int cx = Mathf.FloorToInt(wx); float fx = wx - cx;
                    if (cx < 0 || cx + 1 >= vw) continue;
                    int k = cy * vw + cx;
                    float vv = Mathf.Lerp(Mathf.Lerp(vis[k], vis[k + 1], fx), Mathf.Lerp(vis[k + vw], vis[k + vw + 1], fx), fy);
                    if (vv <= 0.01f) continue;
                    float dx = x + 0.5f - lx;
                    float d = (dx * dx + dy * dy) * inv;
                    if (d >= 1f) continue;
                    float v = (1f - d);
                    v = v * v * (3f - 2f * v) * intensity * vv;
                    if (v > lum[row + x]) lum[row + x] = v;
                }
            }
        }

        // a brightness field over cells (sampled smoothly between cell centres): remembered places glow softly
        public void AddField(float[] f, int fx0, int fy0, int fw, int fh)
        {
            if (lum == null) return;
            for (int y = 0; y < th; y++)
            {
                float wy = origin.y + (y + 0.5f) / RES - fy0 - 0.5f;
                int cy = Mathf.FloorToInt(wy); float ty = wy - cy;
                if (cy < 0 || cy + 1 >= fh) continue;
                int row = y * tw;
                for (int x = 0; x < tw; x++)
                {
                    float wx = origin.x + (x + 0.5f) / RES - fx0 - 0.5f;
                    int cx = Mathf.FloorToInt(wx); float tx = wx - cx;
                    if (cx < 0 || cx + 1 >= fw) continue;
                    int k = cy * fw + cx;
                    float v = Mathf.Lerp(Mathf.Lerp(f[k], f[k + 1], tx), Mathf.Lerp(f[k + fw], f[k + fw + 1], tx), ty);
                    if (v > lum[row + x]) lum[row + x] = v;
                }
            }
        }

        // every texel of one cell at least this bright (remembered cells)
        public void FloorCell(int cx, int cy, float v)
        {
            if (lum == null) return;
            int x0 = (cx - (int)origin.x) * RES, y0 = (cy - (int)origin.y) * RES;
            if (x0 < 0 || y0 < 0 || x0 + RES > tw || y0 + RES > th) return;
            for (int y = y0; y < y0 + RES; y++)
            {
                int row = y * tw;
                for (int x = x0; x < x0 + RES; x++) if (v > lum[row + x]) lum[row + x] = v;
            }
        }

        // Full light over a whole rectangle (soft one-tile edge).
        public void AddRect(Vector2 min, Vector2 max, float intensity)
        {
            if (lum == null || intensity <= 0f) return;
            int x0 = Mathf.Max(0, (int)((min.x - origin.x) * RES)), x1 = Mathf.Min(tw - 1, (int)((max.x - origin.x) * RES));
            int y0 = Mathf.Max(0, (int)((min.y - origin.y) * RES)), y1 = Mathf.Min(th - 1, (int)((max.y - origin.y) * RES));
            for (int y = y0; y <= y1; y++)
            {
                int row = y * tw;
                float ey = Mathf.Min(y - y0, y1 - y) / (float)RES;
                for (int x = x0; x <= x1; x++)
                {
                    float e = Mathf.Min(ey, Mathf.Min(x - x0, x1 - x) / (float)RES);
                    float v = Mathf.Clamp01(e + 0.15f) * intensity;
                    if (v > lum[row + x]) lum[row + x] = v;
                }
            }
        }

        // the void dragon's DARK: 0 normal, 1 almost nothing to see
        public static float Blackout;
        public void Dim(float m) { if (lum == null) return; for (int k = 0; k < lum.Length; k++) lum[k] *= m; }

        public void Flash(Vector2 p, float r, float t) => flashes.Add(new FlashL { p = p, r = r, t = t, max = t });

        public void End(float dt, float darkness)
        {
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var f = flashes[i];
                f.t -= dt;
                if (f.t <= 0f) { flashes.RemoveAt(i); continue; }
                flashes[i] = f;
                Add(f.p, f.r, f.t / f.max);
            }
            for (int k = 0; k < lum.Length; k++)
            {
                float a = darkness * (1f - Mathf.Clamp01(lum[k]));
                a = Mathf.Round(a * 7f) / 7f;   // banded falloff for a pixel-art look
                buf[k] = new Color32(Pal.Night.r, Pal.Night.g, Pal.Night.b, (byte)(a * 255f));   // darkness is deep violet, not black
            }
            tex.SetPixels32(buf);
            tex.Apply(false);
            go.transform.position = new Vector3(origin.x, origin.y, 0);
        }
    }
}
