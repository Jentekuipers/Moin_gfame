using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Hue-shifted colour ramps: shadows lean towards violet/blue, highlights towards warm orange,
    // instead of just multiplying brightness (which turns everything muddy grey-brown).
    // Five levels: 0 shadow, 1 dark, 2 base, 3 light, 4 highlight.
    public static class Pal
    {
        static readonly Dictionary<long, Color32> cache = new Dictionary<long, Color32>();
        const float Cold = 0.72f, Warm = 0.10f;
        static readonly float[] Val = { 0.5f, 0.73f, 1f, 1.2f, 1.42f };
        static readonly float[] Sat = { 1.18f, 1.08f, 1f, 0.92f, 0.78f };
        static readonly float[] Shift = { 0.16f, 0.08f, 0f, 0.07f, 0.13f };

        // The colour the darkness and the empty void are tinted with: deep violet, never pure black.
        public static readonly Color32 Night = new Color32(14, 7, 26, 255);

        public static Color32 Ramp(Color32 c, int lv)
        {
            lv = Mathf.Clamp(lv, 0, 4);
            if (lv == 2) return c;
            long key = ((long)c.r << 24) | ((long)c.g << 16) | ((long)c.b << 8) | (uint)lv;
            if (cache.TryGetValue(key, out var hit)) return hit;
            Color.RGBToHSV(c, out float h, out float s, out float v);
            float target = lv < 2 ? Cold : Warm;
            h = LerpHue(h, target, Shift[lv] * Mathf.Clamp01(s * 2f + 0.2f));
            s = Mathf.Clamp01(s * Sat[lv]);
            v = Mathf.Clamp01(v * Val[lv] + (lv == 4 ? 0.04f : 0f));
            Color32 r = Color.HSVToRGB(h, s, v);
            r.a = c.a;
            if (cache.Count > 20000) cache.Clear();
            cache[key] = r;
            return r;
        }

        // A brightness factor (as the old code used) mapped to the nearest ramp level.
        public static Color32 Shade(Color32 c, float s)
        {
            int lv = s < 0.64f ? 0 : s < 0.84f ? 1 : s < 1.07f ? 2 : s < 1.24f ? 3 : 4;
            return Ramp(c, lv);
        }

        static float LerpHue(float a, float b, float t)
        {
            float d = Mathf.Repeat(b - a + 0.5f, 1f) - 0.5f;
            return Mathf.Repeat(a + d * t, 1f);
        }
    }
}
