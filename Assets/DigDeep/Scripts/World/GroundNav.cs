using UnityEngine;

namespace DigDeep
{
    // Path finding for walking enemies: a breadth-first search over the cells a walker can stand in (walk along,
    // hop up one block, drop down a few), so chasers go around rock instead of pressing their faces against it.
    // It's re-run every few tenths of a second, so a path follows the cave as it's dug and blown away.
    public static class GroundNav
    {
        const int R = 16, W = 2 * R + 1, MaxDrop = 6;
        static readonly int[] came = new int[W * W];
        static readonly int[] seen = new int[W * W];
        static readonly int[] queue = new int[W * W];
        static readonly byte[] how = new byte[W * W];   // 1 walk, 2 hop up, 3 drop
        static int stamp;

        static World Wd => Game.I.world;

        static bool Floor(int x, int y) => Wd.Solid(x, y) || Wd.Get(x, y) == Block.Plate;
        static bool Stand(int x, int y, bool tall) => !Wd.Solid(x, y) && (!tall || !Wd.Solid(x, y + 1)) && Floor(x, y - 1)
                                                      && Wd.LiquidType(x, y) != Fluids.Lava;

        // the cell something standing (or falling) at p ends up in
        static bool Settle(Vector2 feet, bool tall, out int x, out int y)
        {
            x = Mathf.FloorToInt(feet.x); y = Mathf.FloorToInt(feet.y + 0.05f);
            for (int i = 0; i < MaxDrop + 2; i++, y--)
            {
                if (Wd.Solid(x, y)) return false;
                if (Stand(x, y, tall)) return true;
            }
            return false;
        }

        // Which way to walk (-1, 0, +1) to reach `to`, and whether that first step needs a hop.
        // False when there's no way there nearby (then the walker just heads straight for it).
        public static bool Step(Vector2 feet, Vector2 toFeet, bool tall, out int dir, out bool hop)
        {
            dir = 0; hop = false;
            if (!Settle(feet, tall, out int sx, out int sy)) return false;
            if (!Settle(toFeet, tall, out int tx, out int ty)) return false;
            if (Mathf.Abs(tx - sx) > R || Mathf.Abs(ty - sy) > R) return false;
            if (sx == tx && sy == ty) return true;

            // search backwards from the target, so following `came` from the start walks straight to it
            stamp++;
            int Idx(int x, int y) => (y - sy + R) * W + (x - sx + R);
            bool In(int x, int y) => Mathf.Abs(x - sx) <= R && Mathf.Abs(y - sy) <= R;
            int start = Idx(sx, sy), goal = Idx(tx, ty);
            int head = 0, tail = 0;
            queue[tail++] = goal; seen[goal] = stamp; came[goal] = -1;
            bool found = false;
            while (head < tail && !found)
            {
                int cur = queue[head++];
                int cx = cur % W - R + sx, cy = cur / W - R + sy;
                // every cell that can step INTO (cx, cy): neighbours on the same row, one row lower (they hop up),
                // or higher up the same column above the next cell (they drop down)
                for (int side = -1; side <= 1; side += 2)
                {
                    int nx = cx + side;
                    // walk
                    Try(nx, cy, 1);
                    // the one below hops up into (cx, cy): its own head room must be clear
                    if (!Wd.Solid(nx, cy) && (!tall || !Wd.Solid(nx, cy + 1))) Try(nx, cy - 1, 2);
                    // ones above drop down into it: a clear column on our side
                    for (int k = 1; k <= MaxDrop; k++)
                    {
                        if (Wd.Solid(cx, cy + k)) break;
                        Try(nx, cy + k, 3);
                    }
                    if (found) break;
                }

                void Try(int x, int y, byte h)
                {
                    if (found || !In(x, y)) return;
                    int ni = Idx(x, y);
                    if (seen[ni] == stamp || !Stand(x, y, tall)) return;
                    seen[ni] = stamp; came[ni] = cur; how[ni] = h;
                    if (ni == start) { found = true; return; }
                    queue[tail++] = ni;
                }
            }
            if (!found) return false;
            int next = came[start];
            if (next < 0) return true;
            int nxC = next % W - R + sx;
            dir = nxC > sx ? 1 : nxC < sx ? -1 : 0;
            hop = how[start] == 2;
            return true;
        }
    }
}
