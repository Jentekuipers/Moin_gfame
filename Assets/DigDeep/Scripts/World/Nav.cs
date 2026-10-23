using UnityEngine;

namespace DigDeep
{
    // Path finding for flying enemies: a breadth-first search over empty cells around the flyer, so bats and imps
    // fly around rock instead of pressing their faces against it. Cheap enough to run a few times a second per bat.
    public static class Nav
    {
        const int R = 14, W = 2 * R + 1;
        static readonly int[] came = new int[W * W];
        static readonly int[] seen = new int[W * W];
        static readonly int[] queue = new int[W * W];
        static int stamp;
        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        static World Wd => Game.I.world;

        // Where to steer to reach `to`: straight at it when the way is clear, otherwise the furthest point along a
        // path around the rock that can be seen from here. False when there's no way through nearby.
        public static bool Steer(Vector2 from, Vector2 to, out Vector2 waypoint)
        {
            waypoint = to;
            if (Wd.LineClear(from, to)) return true;
            int sx = Mathf.FloorToInt(from.x), sy = Mathf.FloorToInt(from.y);
            int tx = Mathf.FloorToInt(to.x), ty = Mathf.FloorToInt(to.y);
            if (Mathf.Abs(tx - sx) > R || Mathf.Abs(ty - sy) > R) return false;
            if (Wd.Solid(tx, ty)) return false;

            // search backwards from the target, so following `came` from the start walks straight to it
            stamp++;
            int Idx(int x, int y) => (y - sy + R) * W + (x - sx + R);
            int start = Idx(sx, sy), goal = Idx(tx, ty);
            int head = 0, tail = 0;
            queue[tail++] = goal; seen[goal] = stamp; came[goal] = -1;
            bool found = false;
            while (head < tail)
            {
                int cur = queue[head++];
                if (cur == start) { found = true; break; }
                int cx = cur % W - R + sx, cy = cur / W - R + sy;
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + DX[k], ny = cy + DY[k];
                    if (Mathf.Abs(nx - sx) > R || Mathf.Abs(ny - sy) > R) continue;
                    int ni = Idx(nx, ny);
                    if (seen[ni] == stamp) continue;
                    // the start cell may be partly inside rock (the flyer's centre is what counts)
                    if (ni != start && Wd.Solid(nx, ny)) continue;
                    // no cutting corners diagonally
                    if (k >= 4 && (Wd.Solid(cx + DX[k], cy) || Wd.Solid(cx, cy + DY[k]))) continue;
                    seen[ni] = stamp; came[ni] = cur;
                    queue[tail++] = ni;
                }
            }
            if (!found) return false;

            // follow the path a few steps and aim for the furthest cell we can already see
            int node = came[start], best = node;
            for (int i = 0; i < 8 && node >= 0; i++)
            {
                Vector2 c = new Vector2(node % W - R + sx + 0.5f, node / W - R + sy + 0.5f);
                if (i == 0 || Wd.LineClear(from, c)) best = node; else break;
                node = came[node];
            }
            if (best < 0) return false;
            waypoint = new Vector2(best % W - R + sx + 0.5f, best / W - R + sy + 0.5f);
            return true;
        }
    }
}
