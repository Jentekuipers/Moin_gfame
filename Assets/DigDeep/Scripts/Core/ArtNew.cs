using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Jente's hand-drawn sprites (Assets/New stuff), cleaned up into strips in Resources/DigDeepArt/new.
    // Each strip is N frames side by side. They get the game's violet outline so they sit with the rest of the art.
    public static partial class Art
    {
        public static Sprite[] CoinShine, CoinPickup, BatDeath, Bones, KeyShine;
        public static Sprite VoidKey, VoidSpike, JungleTemple, AltarRandom, AltarDeath, Chalice, DragonStatue, Portal, PokerAltar;
        public static readonly Dictionary<string, Sprite[]> Strips = new Dictionary<string, Sprite[]>();

        static void InitNew()
        {
            // replacements
            Enemy["bat"] = Load("bat", 5, true, true, "bat");
            BatDeath = Load("bat_death", 6, true, true);
            CoinShine = Load("coin_shine", 15, true, true);
            CoinPickup = Load("coin_pickup", 5, false, true);
            if (CoinShine != null) Coin = CoinShine[0];
            var fire = Load("fire", 6, false, false);
            if (fire != null) Flames = fire;
            var keeper = Load("shopkeeper", 2, true, false);
            KeeperOld = Keeper;   // the old generated keeper runs the premium shops
            if (keeper != null) Keeper = keeper[0];
            Enemy["toxictoad"] = Load("frog", 2, true, true, "toxictoad");

            // new creatures
            Enemy["slime2"] = Load("slime_walk", 4, true, false, "slime2");
            Strips["slime_fall"] = Load("slime_fall", 5, true, false, "slime_fall");
            Strips["slime_hit"] = Load("slime_hit", 5, true, false, "slime_hit");
            Enemy["turtle"] = Load("turtle_walk", 4, true, false, "turtle");
            Strips["turtle_slide"] = Load("turtle_slide", 7, true, false, "turtle_slide");
            Enemy["skeleton"] = Load("skel_walk", 4, true, false, "skeleton");
            Strips["skel_wake"] = Load("skel_wake", 8, true, false, "skel_wake");
            Strips["skel_punch"] = Load("skel_punch", 8, true, false, "skel_punch");
            Bones = Load("bones", 11, true, true);
            // the Creator's hands (Jente's drawing): a hand, and the same hand side-on
            Strips["creator_hand"] = Load("creator_hand", 1, true, true, "creatorhand");
            Strips["creator_side"] = Load("creator_side", 1, true, true, "creatorside");
            if (Strips["creator_hand"] != null) Enemy["creatorhand"] = Strips["creator_hand"];
            if (Strips["creator_side"] != null) Enemy["creatorside"] = Strips["creator_side"];
            if (!Enemy.ContainsKey("creatorhand")) { Enemy["creatorhand"] = Enemy["godhand"]; EnemyWhite["creatorhand"] = EnemyWhite["godhand"]; }
            Enemy["glider"] = Load("glider", 2, true, true, "glider");
            Enemy["wolf"] = Load("wolf", 2, true, false, "wolf");
            Enemy["rockmage"] = Load("rock_mage", 1, true, false, "rockmage");
            Enemy["mushelite"] = Load("mush_elite", 1, true, false, "mushelite");
            Enemy["boneslime"] = Load("bslime_idle", 4, true, false, "boneslime");
            Strips["bslime_angry"] = Load("bslime_angry", 8, true, false, "bslime_angry");
            Strips["bslime_jump"] = Load("bslime_jump", 19, true, false, "bslime_jump");
            Enemy["icegolem"] = Load("ice_golem", 1, true, true, "icegolem");
            foreach (var m in new[] { "vmage_fire", "vmage_ice", "vmage_elec", "vmage_elite" }) Enemy[m] = Load(m, 2, true, true, m);

            // pets
            foreach (var (file, n, kind) in new[] { ("dogpet", 2, "dog"), ("slimepet", 9, "slime"), ("skelpet", 2, "skeleton"), ("foxpet", 2, "fox"), ("dragonpet", 2, "dragon") })
            {
                var f = Load(file, n, true, true);
                if (f != null) petCache[kind] = f;
            }
            Strips["slimepet_attack"] = Load("slimepet_attack", 4, true, true);

            // things in the world
            KeyShine = Load("key", 12, true, true);
            VoidKey = One("void_key", true, true);
            VoidSpike = One("void_spike", true, false);
            JungleTemple = One("jungle_temple", false, true);
            AltarRandom = One("altar_random", true, false);
            AltarDeath = One("altar_death", true, false);
            Chalice = One("chalice", true, false);
            DragonStatue = One("dragon_statue", true, false);
            Portal = MakePortal();
            // the gambler's altar: a big red-and-white poker chip standing on a stone plinth
            PokerAltar = Make(new[]
            {
                ".....dddddd.....",
                "...ddWRRRRWdd...",
                "..dRRWRRRRWRRd..",
                ".dWWRRwwwwRRWWd.",
                ".dRRRwRRRRwRRRd.",
                "dRRRwRRkkRRwRRRd",
                "dWWRwRkRRkRwRWWd",
                "dWWRwRkRRkRwRWWd",
                "dRRRwRRkkRRwRRRd",
                ".dRRRwRRRRwRRRd.",
                ".dWWRRwwwwRRWWd.",
                "..dRRWRRRRWRRd..",
                "...ddWRRRRWdd...",
                ".....dddddd.....",
                "....ssssssss....",
                "...sSSSSSSSSs...",
                "..ssssssssssss..",
            }, new System.Collections.Generic.Dictionary<char, Color32>
            {
                { 'd', U.C(90, 20, 26) }, { 'R', U.C(206, 44, 52) }, { 'W', U.C(245, 240, 230) }, { 'w', U.C(255, 220, 120) },
                { 'k', U.C(60, 14, 18) }, { 's', U.C(90, 92, 104) }, { 'S', U.C(140, 144, 158) },
            });

            // weapons: their icons are what you hold
            foreach (var (file, id) in new[] { ("bow1", "bow"), ("bow2", "hunterbow"), ("bow3", "stormbow"), ("lsword", "prismblade") })
            {
                var s = One(file, true, true);
                if (s != null) Icons[id] = s;
            }
            // spears (they point right; the same sprite is held and thrown)
            string[] spearRows = { "................HH....", "BBBBBBBBBBBBBBwBHHHHHT", "................HH...." };
            Icons["spear"] = Make(spearRows, new Dictionary<char, Color32> { { 'B', U.C(150, 104, 62) }, { 'w', U.C(210, 190, 140) }, { 'H', U.C(130, 130, 140) }, { 'T', U.C(200, 200, 210) } }, true, true);
            Icons["ironspear"] = Make(spearRows, new Dictionary<char, Color32> { { 'B', U.C(110, 74, 44) }, { 'w', U.C(80, 80, 92) }, { 'H', U.C(190, 196, 210) }, { 'T', U.C(250, 250, 255) } }, true, true);
            Icons["crystalpike"] = Make(new[] { "...............HHH....", "BBBBBBBBBBBBBwBHHHHHHT", "...............HHH...." },
                new Dictionary<char, Color32> { { 'B', U.C(70, 80, 110) }, { 'w', U.C(120, 230, 255) }, { 'H', U.C(110, 220, 250) }, { 'T', U.C(230, 255, 255) } }, true, true);
            Icons["starlance"] = Make(new[] { "..............H.HH.....", "GGGGGGGGGGGGGgGHHHHHHT.", "..............H.HH....." },
                new Dictionary<char, Color32> { { 'G', U.C(236, 184, 60) }, { 'g', U.C(255, 240, 180) }, { 'H', U.C(140, 240, 255) }, { 'T', U.C(255, 255, 255) } }, true, true);
            var ap = new Dictionary<char, Color32> { { 'W', U.C(240, 236, 220) }, { 'B', U.C(120, 84, 52) }, { 'F', U.C(220, 70, 60) } };
            Bullets["b_arrow"] = Make(new[] { "F......W.", "FFBBBBBWW", "F......W." }, ap, true, true);
        }

        // a copy of a sprite, its colours pulled towards a tint (keeps the dark outline): weapon variants
        public static Sprite Tinted(Sprite s, Color tint)
        {
            if (s == null) return null;
            var r = s.textureRect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            Color32[] src;
            try { src = s.texture.GetPixels32(); } catch { return s; }
            int tw = s.texture.width;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = src[(Mathf.RoundToInt(r.y) + y) * tw + Mathf.RoundToInt(r.x) + x];
                    float lum = (c.r * 0.3f + c.g * 0.59f + c.b * 0.11f) / 255f;
                    if (c.a > 0 && lum > 0.22f)
                    {
                        var t = Color.Lerp((Color)c, tint * (0.45f + lum * 0.8f), 0.6f);
                        c = new Color32((byte)(Mathf.Clamp01(t.r) * 255), (byte)(Mathf.Clamp01(t.g) * 255), (byte)(Mathf.Clamp01(t.b) * 255), c.a);
                    }
                    px[y * w + x] = c;
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        static Sprite One(string name, bool outline, bool center)
        {
            var f = Load(name, 1, outline, center);
            return f != null ? f[0] : null;
        }

        // Loads a strip and (with an enemy key) its white hit-flash frames too.
        static Sprite[] Load(string name, int n, bool outline, bool center, string enemyKey = null)
        {
            var ta = Resources.Load<TextAsset>("DigDeepArt/new/" + name);
            if (ta == null)
            {
                Debug.LogWarning("DigDeep: sprite strip DigDeepArt/new/" + name + " not found");
                return enemyKey != null && Enemy.TryGetValue(enemyKey, out var old) ? old : null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(ta.bytes);
            var px = tex.GetPixels32();
            int fw = tex.width / Mathf.Max(1, n), fh = tex.height;
            var frames = new Sprite[n];
            for (int i = 0; i < n; i++) frames[i] = FromPixels(px, tex.width, i * fw, fw, fh, outline, center);
            Object.Destroy(tex);
            if (enemyKey != null)
            {
                var w = new Sprite[n];
                for (int i = 0; i < n; i++) w[i] = Silhouette(frames[i]);
                EnemyWhite[enemyKey] = w;
            }
            return frames;
        }

        // One frame (a column range of a strip) into its own sprite, with the violet outline like every other sprite.
        static Sprite FromPixels(Color32[] src, int srcW, int x0, int fw, int fh, bool outline, bool center)
        {
            int m = outline ? 1 : 0;
            int tw = fw + 2 * m, th = fh + 2 * m;
            var px = new Color32[tw * th];
            var solid = new bool[tw * th];
            int low = th;
            for (int y = 0; y < fh; y++)
                for (int x = 0; x < fw; x++)
                {
                    var c = src[y * srcW + x0 + x];
                    if (c.a < 100) continue;
                    c.a = 255;
                    int i = (y + m) * tw + x + m;
                    px[i] = c; solid[i] = true;
                    low = Mathf.Min(low, y + m);
                }
            if (outline)
            {
                var oc = new Color32(24, 12, 34, 255);
                for (int y = 0; y < th; y++)
                    for (int x = 0; x < tw; x++)
                    {
                        if (solid[y * tw + x]) continue;
                        bool nb = (x > 0 && solid[y * tw + x - 1]) || (x < tw - 1 && solid[y * tw + x + 1]) ||
                                  (y > 0 && solid[(y - 1) * tw + x]) || (y < th - 1 && solid[(y + 1) * tw + x]);
                        if (nb) px[y * tw + x] = oc;
                    }
            }
            if (low == th) low = 0;
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            Vector2 pivot = center ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, low / (float)th);
            return Sprite.Create(tex, new Rect(0, 0, tw, th), pivot, PPU, 0, SpriteMeshType.FullRect);
        }

        // The void portal: a black disc that dissolves into loose squares at its rim (like the void's gradients).
        static Sprite MakePortal()
        {
            int n = 48;
            var px = new Color32[n * n];
            var rnd = new System.Random(5);
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y += 2)
                for (int x = 0; x < n; x += 2)
                {
                    float d = Mathf.Sqrt((x + 1 - c) * (x + 1 - c) + (y + 1 - c) * (y + 1 - c)) / c;
                    // solid in the middle, crumbling into squares towards the edge
                    float keep = d < 0.55f ? 1f : Mathf.Clamp01(1f - (d - 0.55f) / 0.45f);
                    if (rnd.NextDouble() > keep * keep) continue;
                    Color32 col = d < 0.5f ? new Color32(4, 2, 8, 255) : d < 0.7f ? new Color32(30, 10, 46, 255) : new Color32(120, 50, 180, 255);
                    for (int yy = 0; yy < 2; yy++) for (int xx = 0; xx < 2; xx++) px[(y + yy) * n + x + xx] = col;
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
        }
    }
}
