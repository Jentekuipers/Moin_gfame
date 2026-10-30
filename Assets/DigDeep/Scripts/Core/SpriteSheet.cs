using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace DigDeep
{
    // Every sprite in the game, as files you can paint over.
    //
    // EXPORT (Unity menu MOIN > Export Sprite Sheet, in play mode, or F9 in a test run) writes to
    // Assets/StreamingAssets/SpriteSheet/:
    //   sheet_original.png   every sprite on one sheet (1 px gaps)      index.txt   name x y w h (y from the top)
    //   sprites/<name>.png   every sprite on its own
    //
    // EDIT and the game picks your art up on the next start:
    //   - paint on a copy of the sheet saved as  SpriteSheet/sheet.png  (same size and layout), and/or
    //   - drop single sprites (any size) into     SpriteSheet/overrides/<name>.png
    // Overrides win over the sheet. Hit-flash (white) frames of foes are rebuilt from your new art.
    //
    // overrides/anchors.txt (optional) places a picture that isn't the old size: "name nx ny base ox oy" puts pixel
    // (nx, ny) of the new picture (from its bottom-left) exactly where pixel (ox, oy) of sprite `base` used to be, so
    // bigger or smaller redraws stay put (and every frame of an animation lines up).
    // Extra animation frames: overrides/<Field>.<key>_<n>.png past the end of an animation (Enemy.mimic_2) add a
    // frame; overrides/Extra.<key>_<n>.png make new animations the game asks for by name (SpriteSheet.Frames).
    // One picture used in several places is exported once and replaced everywhere it's used.
    public static class SpriteSheet
    {
        static string Root => Path.Combine(Application.streamingAssetsPath, "SpriteSheet");

        // ------------------------------------------------------------------ what there is

        // (name, getter, setter) for every sprite the Art class holds, plus the item icons and the diggers
        struct Entry { public string name; public System.Func<Sprite> get; public System.Action<Sprite> set; }

        static List<Entry> Collect(bool withDefs)
        {
            var list = new List<Entry>();
            var seenNames = new HashSet<string>();
            void Add(string n, System.Func<Sprite> g, System.Action<Sprite> s)
            {
                n = Clean(n);
                if (!seenNames.Add(n)) return;
                list.Add(new Entry { name = n, get = g, set = s });
            }

            foreach (var f in typeof(Art).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (f.Name == "EnemyWhite") continue;   // rebuilt from the frames
                var v = f.GetValue(null);
                if (v is Sprite) { var fi = f; Add(f.Name, () => (Sprite)fi.GetValue(null), s => fi.SetValue(null, s)); }
                else if (v is Sprite[] arr) for (int i = 0; i < arr.Length; i++) { int k = i; Add(f.Name + "_" + i, () => arr[k], s => arr[k] = s); }
                else if (v is Dictionary<string, Sprite> d1)
                    foreach (var key in new List<string>(d1.Keys)) { var kk = key; Add(f.Name + "." + key, () => d1[kk], s => d1[kk] = s); }
                else if (v is Dictionary<string, Sprite[]> d2)
                    foreach (var key in new List<string>(d2.Keys))
                    {
                        var a = d2[key];
                        if (a == null) continue;
                        for (int i = 0; i < a.Length; i++)
                        {
                            int k = i; var kk = key; var fname = f.Name;
                            Add(f.Name + "." + key + "_" + i, () => a[k], s => { a[k] = s; if (fname == "Enemy" || fname == "Strips") Rewhite(kk, k, s); });
                        }
                    }
            }
            if (withDefs)
            {
                foreach (var d in DB.ById.Values) { var dd = d; if (dd.icon != null) Add("icon_" + dd.id, () => dd.icon, s => dd.icon = s); }
                foreach (var c in Chars.All)
                    if (c.frames != null)
                        for (int i = 0; i < c.frames.Length; i++) { var fr = c.frames; int k = i; Add("digger_" + c.id + "_" + i, () => fr[k], s => fr[k] = s); }
            }
            return list;
        }

        static string Clean(string n)
        {
            var ch = n.ToCharArray();
            for (int i = 0; i < ch.Length; i++) if (!(char.IsLetterOrDigit(ch[i]) || ch[i] == '_' || ch[i] == '.' || ch[i] == '-')) ch[i] = '_';
            return new string(ch);
        }

        // a foe's hit-flash frame follows its (new) shape
        static void Rewhite(string key, int i, Sprite s)
        {
            if (!Art.EnemyWhite.TryGetValue(key, out var w) || w == null || i >= w.Length || s == null) return;
            var px = Pixels(s);
            for (int k = 0; k < px.Length; k++) if (px[k].a > 0) px[k] = new Color32(255, 255, 255, px[k].a);
            w[i] = Build(px, (int)s.rect.width, (int)s.rect.height, s);
        }

        // ------------------------------------------------------------------ pixels in and out

        static Color32[] Pixels(Sprite s)
        {
            var r = s.textureRect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            try
            {
                var src = s.texture.GetPixels32();
                int tw = s.texture.width;
                var o = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++) o[y * w + x] = src[(Mathf.RoundToInt(r.y) + y) * tw + Mathf.RoundToInt(r.x) + x];
                return o;
            }
            catch { return new Color32[w * h]; }   // (an unreadable texture: blank)
        }

        static Sprite Build(Color32[] px, int w, int h, Sprite like, Anchor? anchor = null, Sprite baseSprite = null)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px); tex.Apply();
            Vector2 pivot = like != null ? new Vector2(like.pivot.x / like.rect.width, like.pivot.y / like.rect.height) : new Vector2(0.5f, 0.5f);
            var bs = baseSprite != null ? baseSprite : like;
            if (anchor.HasValue && bs != null)
            {
                // the new picture's anchor point lands where the base sprite's was: same pivot offset from it
                Vector2 pp = anchor.Value.n + (bs.pivot - anchor.Value.o);
                pivot = new Vector2(pp.x / w, pp.y / h);
            }
            float ppu = bs != null ? bs.pixelsPerUnit : Art.PPU;
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect);
        }

        // ------------------------------------------------------------------ export

        public static string Export()
        {
            var list = Collect(true);
            Directory.CreateDirectory(Path.Combine(Root, "sprites"));
            Directory.CreateDirectory(Path.Combine(Root, "overrides"));
            // pack in rows, tallest first
            var items = new List<(Entry e, Color32[] px, int w, int h)>();
            var exported = new HashSet<Sprite>();
            foreach (var e in list)
            {
                var s = e.get();
                if (s == null || !exported.Add(s)) continue;   // the same picture used in several places: once on the sheet
                var px = Pixels(s);
                int w = Mathf.RoundToInt(s.textureRect.width), h = Mathf.RoundToInt(s.textureRect.height);
                if (w <= 0 || h <= 0) continue;
                items.Add((e, px, w, h));
                var single = new Texture2D(w, h, TextureFormat.RGBA32, false);
                single.SetPixels32(px); single.Apply();
                File.WriteAllBytes(Path.Combine(Root, "sprites", e.name + ".png"), single.EncodeToPNG());
                Object.Destroy(single);
            }
            const int SheetW = 1024;
            int cx = 1, cy = 1, rowH = 0;
            var place = new List<(int x, int y)>();   // top-left, y down
            foreach (var it in items)
            {
                if (cx + it.w + 1 > SheetW) { cx = 1; cy += rowH + 1; rowH = 0; }
                place.Add((cx, cy));
                cx += it.w + 1; rowH = Mathf.Max(rowH, it.h);
            }
            int sheetH = cy + rowH + 1;
            var sheet = new Color32[SheetW * sheetH];
            var index = new System.Text.StringBuilder();
            index.AppendLine("# name x y w h   (pixels, y measured from the top of sheet.png)");
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i]; var (x0, y0) = place[i];
                for (int y = 0; y < it.h; y++)
                    for (int x = 0; x < it.w; x++)
                        sheet[(sheetH - 1 - (y0 + (it.h - 1 - y))) * SheetW + x0 + x] = it.px[y * it.w + x];   // textures run bottom-up
                index.AppendLine(it.e.name + " " + x0 + " " + y0 + " " + it.w + " " + it.h);
            }
            var tex = new Texture2D(SheetW, sheetH, TextureFormat.RGBA32, false);
            tex.SetPixels32(sheet); tex.Apply();
            File.WriteAllBytes(Path.Combine(Root, "sheet_original.png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            File.WriteAllText(Path.Combine(Root, "index.txt"), index.ToString());
            File.WriteAllText(Path.Combine(Root, "README.txt"),
                "MOIN sprite sheet (" + items.Count + " sprites)\n\n" +
                "Edit a copy of sheet_original.png and save it as sheet.png (keep the size and layout),\n" +
                "and/or put single sprites in overrides/<name>.png (any size; names as in sprites/).\n" +
                "The game loads them on the next start. Delete them to go back to the built-in art.\n" +
                "Tiles (dirt, stone...) are painted per pixel and aren't on the sheet.\n");
            return Root;
        }

        // ------------------------------------------------------------------ import (on start)

        // Art sprites: right after Art.Init (so weapons and trinkets pick up new icons when they're defined)
        public static void ApplyArt() => Apply(false);
        // item icons and diggers: after DB and the diggers exist
        public static void ApplyDefs() => Apply(true);

        static void Apply(bool defsOnly)
        {
            string root = Root;
            if (!Directory.Exists(root)) return;
            Dictionary<string, (int x, int y, int w, int h)> idx = null;
            Color32[] sheet = null; int sw = 0, sh = 0;
            string sheetPath = Path.Combine(root, "sheet.png"), idxPath = Path.Combine(root, "index.txt");
            if (File.Exists(sheetPath) && File.Exists(idxPath))
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (t.LoadImage(File.ReadAllBytes(sheetPath))) { sheet = t.GetPixels32(); sw = t.width; sh = t.height; }
                Object.Destroy(t);
                idx = new Dictionary<string, (int, int, int, int)>();
                foreach (var line in File.ReadAllLines(idxPath))
                {
                    if (line.StartsWith("#")) continue;
                    var p = line.Split(' ');
                    if (p.Length == 5) idx[p[0]] = (int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]), int.Parse(p[4]));
                }
            }
            string ovr = Path.Combine(root, "overrides");
            bool anyOvr = Directory.Exists(ovr);
            if (sheet == null && !anyOvr) return;
            var anchors = anyOvr ? ReadAnchors(Path.Combine(ovr, "anchors.txt")) : new Dictionary<string, Anchor>();
            var all = Collect(defsOnly);
            // every sprite as it was before anything was replaced (anchors measure against these)
            var before = new Dictionary<string, Sprite>();
            foreach (var e in all) before[e.name] = e.get();
            int n = 0;
            var done = new HashSet<string>();
            foreach (var e in all)
            {
                bool isDef = e.name.StartsWith("icon_") || e.name.StartsWith("digger_");
                if (isDef != defsOnly) continue;
                var old = before[e.name];
                string file = anyOvr ? Path.Combine(ovr, e.name + ".png") : null;
                if (file != null && File.Exists(file))
                {
                    done.Add(e.name);
                    var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (t.LoadImage(File.ReadAllBytes(file)))
                    {
                        try
                        {
                            Anchor? an = null; Sprite bs = null;
                            if (anchors.TryGetValue(e.name, out var a0)) { an = a0; before.TryGetValue(a0.baseName, out bs); }
                            var ns = Build(t.GetPixels32(), t.width, t.height, old, an, bs);
                            e.set(ns); n++;
                            // the same picture used elsewhere (that has no override of its own) changes with it
                            if (old != null)
                                foreach (var o in all)
                                    if (o.name != e.name && before[o.name] == old && !File.Exists(Path.Combine(ovr, o.name + ".png"))) o.set(ns);
                        }
                        catch { }
                    }
                    Object.Destroy(t);
                    continue;
                }
                if (sheet != null && idx.TryGetValue(e.name, out var r) && r.x + r.w <= sw && r.y + r.h <= sh)
                {
                    var px = new Color32[r.w * r.h];
                    bool changed = false;
                    var cur = old != null ? Pixels(old) : null;
                    for (int y = 0; y < r.h; y++)
                        for (int x = 0; x < r.w; x++)
                        {
                            var c = sheet[(sh - 1 - (r.y + (r.h - 1 - y))) * sw + r.x + x];
                            px[y * r.w + x] = c;
                            if (cur == null || cur.Length != px.Length || !Same(cur[y * r.w + x], c)) changed = true;
                        }
                    if (changed) { try { e.set(Build(px, r.w, r.h, old)); n++; } catch { } }
                }
            }
            if (anyOvr && !defsOnly) n += ExtraFrames(ovr, anchors, before, done);
            if (n > 0) Debug.Log("MOIN: " + n + " sprites replaced from StreamingAssets/SpriteSheet");
        }

        // ------------------------------------------------------------------ anchors and extra frames

        struct Anchor { public Vector2 n, o; public string baseName; }

        static Dictionary<string, Anchor> ReadAnchors(string path)
        {
            var d = new Dictionary<string, Anchor>();
            if (!File.Exists(path)) return d;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var line in File.ReadAllLines(path))
            {
                if (line.StartsWith("#")) continue;
                var p = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 6) continue;
                try
                {
                    d[p[0]] = new Anchor
                    {
                        n = new Vector2(float.Parse(p[1], ci), float.Parse(p[2], ci)),
                        baseName = p[3],
                        o = new Vector2(float.Parse(p[4], ci), float.Parse(p[5], ci)),
                    };
                }
                catch { }
            }
            return d;
        }

        // new animations made only of override files (Extra.<key>_<n>.png), for the game to ask for by name
        static readonly Dictionary<string, Sprite[]> extra = new Dictionary<string, Sprite[]>();
        public static Sprite[] Frames(string key) => extra.TryGetValue(key, out var f) ? f : null;

        // frames past the end of an existing animation (Enemy.mimic_2.png), and Extra.* animations
        static int ExtraFrames(string ovr, Dictionary<string, Anchor> anchors, Dictionary<string, Sprite> before, HashSet<string> done)
        {
            int n = 0;
            var rx = new System.Text.RegularExpressions.Regex(@"^(\w+)\.(.+)_(\d+)$");
            var files = new List<string>(Directory.GetFiles(ovr, "*.png"));
            files.Sort(System.StringComparer.Ordinal);
            foreach (var f in files)
            {
                string name = Path.GetFileNameWithoutExtension(f);
                if (done.Contains(name)) continue;
                var m = rx.Match(name);
                if (!m.Success) continue;
                string field = m.Groups[1].Value, key = m.Groups[2].Value;
                int i = int.Parse(m.Groups[3].Value);
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!t.LoadImage(File.ReadAllBytes(f))) { Object.Destroy(t); continue; }
                Anchor? an = null; Sprite bs = null;
                if (anchors.TryGetValue(name, out var a0)) { an = a0; before.TryGetValue(a0.baseName, out bs); }
                if (bs == null) before.TryGetValue(field + "." + key + "_0", out bs);
                Sprite s = null;
                try { s = Build(t.GetPixels32(), t.width, t.height, bs, an, bs); } catch { }
                Object.Destroy(t);
                if (s == null) continue;
                if (field == "Extra")
                {
                    extra.TryGetValue(key, out var arr);
                    extra[key] = Put(arr, i, s);
                    n++;
                    continue;
                }
                var fi = typeof(Art).GetField(field, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi == null || !(fi.GetValue(null) is Dictionary<string, Sprite[]> dict) || !dict.TryGetValue(key, out var cur) || cur == null) continue;
                dict[key] = Put(cur, i, s);
                n++;
                // foes need a hit-flash frame to match every new frame
                if (field == "Enemy" && Art.EnemyWhite.TryGetValue(key, out var wh) && wh != null)
                {
                    var px = Pixels(s);
                    for (int k = 0; k < px.Length; k++) if (px[k].a > 0) px[k] = new Color32(255, 255, 255, px[k].a);
                    Art.EnemyWhite[key] = Put(wh, i, Build(px, (int)s.rect.width, (int)s.rect.height, s));
                }
            }
            return n;
        }

        // arr with s at index i (grown if needed; any gap filled with the frame before it)
        static Sprite[] Put(Sprite[] arr, int i, Sprite s)
        {
            int len = Mathf.Max(arr != null ? arr.Length : 0, i + 1);
            var o = new Sprite[len];
            for (int k = 0; k < len; k++) o[k] = arr != null && k < arr.Length ? arr[k] : null;
            o[i] = s;
            for (int k = 0; k < len; k++) if (o[k] == null) o[k] = k > 0 ? o[k - 1] : s;
            return o;
        }

        static bool Same(Color32 a, Color32 b) => a.a == 0 && b.a == 0 || (a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a);
    }
}
