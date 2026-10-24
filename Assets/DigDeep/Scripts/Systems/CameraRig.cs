using UnityEngine;

namespace DigDeep
{
    public class CameraRig
    {
        public readonly Camera cam;
        public Vector2 pos;
        public float halfW, halfH;
        float trauma, lookY, rumble, punch;
        Vector2 kick;
        public const float Size = 8.5f;   // ~17 tiles tall, ~30 wide at 16:9 -> the 84-wide world is ~3 screens

        public CameraRig(Camera c)
        {
            cam = c;
            cam.orthographic = true;
            cam.orthographicSize = Size;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Pal.Night;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            UpdateSize();
        }

        // Crisp pixels: every game pixel is drawn as a whole number of screen pixels. The view is sized so
        // one world unit (16 art pixels) maps to 16 x an integer scale (about 17 tiles tall).
        public float BaseSize { get; private set; } = Size;
        public int PixelScale { get; private set; } = 4;

        void UpdateSize()
        {
            int sh = Mathf.Max(1, Screen.height);
            PixelScale = Mathf.Max(1, Mathf.RoundToInt(sh / (2f * Size * Art.PPU)));
            BaseSize = sh / (2f * Art.PPU * PixelScale);
            halfH = BaseSize;   // the unpunched size, so lighting / fluid buffers don't resize during a zoom punch
            halfW = halfH * cam.aspect;
        }

        // during a boss fight the screen calms down a lot: you need to read the bullets
        static float BossDamp => Game.I != null && Game.I.boss != null && !Game.I.boss.dead ? 0.35f : 1f;

        public void Shake(float a) { a *= Settings.Shake * BossDamp; trauma = Mathf.Min(1f, Mathf.Max(trauma, a) + a * 0.25f); }   // SCREEN SHAKE option

        // A directional shove (gun kick): the view jerks and springs back fast.
        public void Kick(Vector2 d) => kick = Vector2.ClampMagnitude(kick + d * Settings.Shake * BossDamp, 0.9f);

        // Vlambeer zoom punch: the view snaps in a little and eases back out (kills, big hits).
        public void Punch(float a) => punch = Mathf.Min(0.9f, Mathf.Max(punch, a * Settings.Shake * BossDamp));

        // Low constant rumble that builds with chain reactions and slowly dies away.
        public void AddRumble(float a) => rumble = Mathf.Min(0.9f, rumble + a * Settings.Shake * BossDamp);

        public void Snap(Vector2 p)
        {
            UpdateSize();
            pos = Clamp(p);
            Apply();
        }

        public void Tick(float dt, Vector2 target, Vector2 vel)
        {
            UpdateSize();
            float wantLook = vel.y < -6f ? -3f : 0f;
            lookY = Mathf.Lerp(lookY, wantLook, 1f - Mathf.Exp(-dt * 3f));
            Vector2 goal = target + new Vector2(0f, lookY);
            pos.x = Mathf.Lerp(pos.x, goal.x, 1f - Mathf.Exp(-dt * 8f));
            pos.y = Mathf.Lerp(pos.y, goal.y, 1f - Mathf.Exp(-dt * (goal.y < pos.y ? 10f : 6f)));
            pos = Clamp(pos);
            trauma = Mathf.Max(0f, trauma - dt * 1.8f);
            rumble = Mathf.Max(0f, rumble - dt * 0.35f);
            kick = Vector2.Lerp(kick, Vector2.zero, 1f - Mathf.Exp(-dt * 14f));   // springs back quickly
            punch = Mathf.Lerp(punch, 0f, 1f - Mathf.Exp(-dt * 10f));
            cam.orthographicSize = BaseSize - punch;
            Apply();
        }

        public void Apply()
        {
            // linear term so even small shots move the screen a pixel or two
            float s = trauma * 0.32f + trauma * trauma * 0.45f + rumble * 0.12f;
            Vector2 p = pos + kick + new Vector2(Random.Range(-s, s), Random.Range(-s, s));
            cam.transform.position = new Vector3(U.Snap(p.x), U.Snap(p.y), -10f);   // whole art pixels only
        }

        Vector2 Clamp(Vector2 p)
        {
            if (World.SkyMode || Ascent.Flying) return p;   // the ascent and above the clouds: no walls, no ceiling
            float minX = halfW, maxX = World.W - halfW;
            p.x = minX > maxX ? World.W * 0.5f : Mathf.Clamp(p.x, minX, maxX);
            p.y = Mathf.Min(p.y, 1f - halfH);   // never look above the dark-matter ceiling
            return p;
        }
    }
}
