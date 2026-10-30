using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Every gameplay chance is a marble bag (Wyatt Cheng's example, a pseudo-random distribution): a "1 in 10" chance
    // is a bag of 10 marbles with one red one, drawn WITHOUT putting them back, refilled when it's empty. Over each
    // bag you get exactly the odds you were promised: no long droughts, no silly streaks. Each kind of roll has its
    // own bag (by key); when the odds change (luck, more items), that bag is refilled at the new odds.
    public static class Chance
    {
        class Bag { public int n, w, leftN, leftW; public float p; }
        static readonly Dictionary<string, Bag> bags = new Dictionary<string, Bag>();

        public static void Reset() { bags.Clear(); wbags.Clear(); }

        // true with probability p (0..1)
        public static bool Roll(string key, float p)
        {
            if (p <= 0f) return false;
            if (p >= 1f) return true;
            if (!bags.TryGetValue(key, out var b) || Mathf.Abs(b.p - p) > 0.0005f)
            {
                b = Make(p);
                // keep roughly how far through the old bag we were, so changing odds doesn't hand out a free win
                bags[key] = b;
            }
            if (b.leftN <= 0) { b.leftN = b.n; b.leftW = b.w; }
            bool win = Random.value * b.leftN < b.leftW;
            b.leftN--;
            if (win) b.leftW--;
            return win;
        }

        // the smallest bag that holds these odds closely enough
        static Bag Make(float p)
        {
            int bestN = 1, bestW = 0; float bestErr = 9f;
            if (p < 0.05f)
            {
                bestN = Mathf.Clamp(Mathf.RoundToInt(1f / p), 2, 2000); bestW = 1;
            }
            else
            {
                for (int n = 2; n <= 40; n++)
                {
                    int w = Mathf.Clamp(Mathf.RoundToInt(p * n), 0, n);
                    float err = Mathf.Abs(w / (float)n - p);
                    if (err < bestErr - 1e-4f) { bestErr = err; bestN = n; bestW = w; }
                    if (err < 0.01f) break;
                }
            }
            return new Bag { n = bestN, w = bestW, leftN = bestN, leftW = bestW, p = p };
        }

        // ---- weighted picks (which rarity, which of a few outcomes): a bag with marbles in proportion to the weights
        class WBag { public float[] w; public List<int> marbles = new List<int>(); }
        static readonly Dictionary<string, WBag> wbags = new Dictionary<string, WBag>();

        public static int Pick(string key, float[] weights, int size = 40)
        {
            float total = 0f;
            foreach (var x in weights) total += Mathf.Max(0f, x);
            if (total <= 0f) return -1;
            if (!wbags.TryGetValue(key, out var b) || !Same(b.w, weights, total))
            {
                b = new WBag { w = (float[])weights.Clone() };
                wbags[key] = b;
            }
            if (b.marbles.Count == 0)
            {
                // tiny weights still get their marble now and then: the bag grows until every outcome fits
                float minShare = float.MaxValue;
                foreach (var x in weights) if (x > 0f) minShare = Mathf.Min(minShare, x / total);
                int n = Mathf.Clamp(Mathf.RoundToInt(1f / Mathf.Max(minShare, 0.0005f)), size, 2000);
                for (int i = 0; i < weights.Length; i++)
                {
                    if (weights[i] <= 0f) continue;
                    int c = Mathf.Max(1, Mathf.RoundToInt(weights[i] / total * n));
                    for (int k = 0; k < c; k++) b.marbles.Add(i);
                }
            }
            int at = Random.Range(0, b.marbles.Count);
            int r = b.marbles[at];
            b.marbles[at] = b.marbles[b.marbles.Count - 1];
            b.marbles.RemoveAt(b.marbles.Count - 1);
            return r;
        }

        static bool Same(float[] a, float[] b, float total)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (Mathf.Abs(a[i] - b[i]) > total * 0.02f) return false;
            return true;
        }
    }
}
