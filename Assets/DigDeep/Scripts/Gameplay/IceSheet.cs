using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // A thin sheet of ice lying on top of the blocks (the Crystal Colossus leaves it where it charges).
    // Slippery like an ice block: you slide about on it. It melts away after a while.
    public class IceSheet : Entity
    {
        static readonly Dictionary<long, IceSheet> cells = new Dictionary<long, IceSheet>();
        static long Key(int x, int y) => ((long)y << 32) ^ (uint)x;
        public static void ClearAll() => cells.Clear();

        // the open cell (x, y) has ice on its floor
        public static bool At(int x, int y) => cells.ContainsKey(Key(x, y));

        readonly int cx, cy;
        float life;
        const float Life = 12f;

        public override bool AlwaysActive => true;

        // ice on the floor of the open cell (x, y), if there's a block under it
        public static void Add(int x, int y)
        {
            var w = Game.I.world;
            if (w.Solid(x, y) || !w.Solid(x, y - 1)) return;
            if (cells.TryGetValue(Key(x, y), out var have)) { have.life = Life; return; }
            var s = new IceSheet(x, y);
            cells[Key(x, y)] = s;
            Game.I.Spawn(s);
        }

        IceSheet(int x, int y)
        {
            cx = x; cy = y; life = Life;
            pos = new Vector2(x + 0.5f, y + 0.09f);
            MakeGO("IceSheet", Art.Square, 7);
            go.transform.localScale = new Vector3(1f, 0.18f, 1f);
            sr.color = new Color(0.75f, 0.92f, 1f, 0.85f);
            Place();
        }

        public override void Tick(float dt)
        {
            life -= dt;
            var w = Game.I.world;
            if (life <= 0f || w.Solid(cx, cy) || !w.Solid(cx, cy - 1)) { Kill(); return; }
            float a = Mathf.Clamp01(life / 2f) * 0.85f;
            sr.color = new Color(0.75f, 0.92f, 1f, a);
            if (Random.value < dt * 0.4f) G.fx.Burst(pos + new Vector2(Random.Range(-0.4f, 0.4f), 0.1f), 1, Color.white, 0.5f, 0.4f, -1f, 1f, false);
            Place();
        }

        public override void Kill()
        {
            base.Kill();
            if (cells.TryGetValue(Key(cx, cy), out var s) && s == this) cells.Remove(Key(cx, cy));
        }
    }
}
