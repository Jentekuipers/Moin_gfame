using UnityEngine;

namespace DigDeep
{
    public static class U
    {
        public static uint Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)s * 2654435761u + 0x9E3779B9u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return h;
            }
        }

        public static float H01(int x, int y, int s) => (Hash(x, y, s) & 0xFFFFFF) / 16777216f;

        public static int FloorDiv(int a, int b) => a >= 0 ? a / b : (a - b + 1) / b;

        public static float Approach(float v, float t, float d) => v < t ? Mathf.Min(v + d, t) : Mathf.Max(v - d, t);

        public static Color32 Mul(Color32 c, float s) =>
            new Color32((byte)Mathf.Clamp(c.r * s, 0f, 255f), (byte)Mathf.Clamp(c.g * s, 0f, 255f), (byte)Mathf.Clamp(c.b * s, 0f, 255f), c.a);

        public static Color32 C(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

        public static float Fbm(float x, float y) =>
            Mathf.PerlinNoise(x, y) * 0.62f + Mathf.PerlinNoise(x * 2.1f + 17.3f, y * 2.1f + 5.1f) * 0.38f;

        public static bool Overlap(Vector2 a, Vector2 ha, Vector2 b, Vector2 hb) =>
            Mathf.Abs(a.x - b.x) < ha.x + hb.x && Mathf.Abs(a.y - b.y) < ha.y + hb.y;

        public static float Snap(float v) => Mathf.Round(v * 16f) / 16f;

        public static Vector2 Rot(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
