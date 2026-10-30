using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    public class Hud
    {
        struct Lbl { public Vector2 p; public string s; public Color c; }
        class Pop { public Vector2 p; public string s; public Color c; public float t; }
        struct Ban { public string title, sub; public Color c; }

        readonly List<Lbl> labels = new List<Lbl>();
        readonly List<Pop> pops = new List<Pop>();
        readonly List<Ban> banners = new List<Ban>();
        Ban cur;
        float bannerT, dmgFlash, helpT, biomeT, whiteFlash, wKick, hurtShake, goldPop, crossKick;
        int lastGold = -1;
        int lastBiome = -1, seenBiomes;   // seenBiomes: a bit per biome whose title has been shown this run
        const float BannerTime = 2.4f;

        public void Reset()
        {
            labels.Clear(); pops.Clear(); banners.Clear();
            bannerT = 0f; dmgFlash = 0f; helpT = 14f; lastBiome = -1; seenBiomes = 0;
            statInit = false;
            lastMaxHp = -1; hpFlashT = 0f;
            whiteFlash = 0f; wKick = 0f; hurtShake = 0f; goldPop = 0f; lastGold = -1; crossKick = 0f;
        }

        public void ClearFrame() => labels.Clear();

        // ---------------------------------------------------------------- mouse-over tooltips
        string tipTitle;
        Color tipColor;
        readonly List<(string, Color)> tipLines = new List<(string, Color)>();

        static readonly Color TipGrey = new Color(0.82f, 0.82f, 0.85f), TipStat = new Color(0.65f, 0.8f, 0.95f);

        bool Hover(float x, float y, float w, float h)
        {
            if (tipTitle != null || GameInput.UsingStick) return false;
            return new Rect(x, y, w, h).Contains(Event.current.mousePosition);
        }

        // ---------------------------------------------------------------- keeping text on screen

        // A description cut down to one short line for labels in the world; the full text lives in the hover tooltip.
        public static string Short(string s, int max = 40)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string r = s;
            // cut at the end of the first sentence (a '.' between digits is a decimal point, not the end: "+0.5")
            int cut = -1;
            for (int i = 0; i < r.Length; i++)
            {
                char ch = r[i];
                if (ch == ';' || ch == '(') { cut = i; break; }
                if (ch == '.' && !(i > 0 && i + 1 < r.Length && char.IsDigit(r[i - 1]) && char.IsDigit(r[i + 1]))) { cut = i; break; }
            }
            if (cut > 8) r = r.Substring(0, cut).TrimEnd(' ', ',', ':');
            if (r.Length <= max) return r;
            int sp = r.LastIndexOf(' ', max - 2);
            if (sp < max / 2) sp = max - 2;
            return r.Substring(0, sp).TrimEnd(' ', ',', ':', '+') + "..";
        }

        // Splits text into lines no wider than maxW (at this font size).
        public static List<string> Wrap(string s, float maxW, float size)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(s)) return lines;
            int maxChars = Mathf.Max(8, Mathf.FloorToInt((maxW / size + 1f) / PixelFont.CW));
            string cur = "";
            foreach (var word in s.Split(' '))
            {
                string w = word;
                while (w.Length > maxChars) { if (cur.Length > 0) { lines.Add(cur); cur = ""; } lines.Add(w.Substring(0, maxChars)); w = w.Substring(maxChars); }
                if (cur.Length == 0) cur = w;
                else if (cur.Length + 1 + w.Length <= maxChars) cur += " " + w;
                else { lines.Add(cur); cur = w; }
            }
            if (cur.Length > 0) lines.Add(cur);
            return lines;
        }

        // Draws centred text, wrapped onto as many lines as it needs; returns the height used.
        static float DrawWrapped(float cx, float y, string s, float size, Color c, float maxW)
        {
            float lh = 9f * size;
            var ls = Wrap(s, maxW, size);
            for (int i = 0; i < ls.Count; i++) PixelFont.Draw(cx, y + i * lh, ls[i], size, c, 1);
            return ls.Count * lh;
        }

        // Shrinks one line of text until it fits across maxW.
        static float FitSize(string s, float size, float maxW)
        {
            while (size > 1f && PixelFont.Width(s, size) > maxW) size -= 1f;
            return size;
        }

        // the item or weapon the mouse is over out in the world: its full details pop up beside the cursor
        Def hoverDef;
        Weapon hoverWeapon;

        void Tip(string title, Color c, params (string, Color)[] lines)
        {
            tipTitle = title; tipColor = c;
            tipLines.Clear();
            foreach (var l in lines) if (!string.IsNullOrEmpty(l.Item1)) tipLines.Add(l);
        }

        // the thing being hovered: its combo partners light up among your trinkets (drawn next frame);
        // a hovered combo lights up every trinket that makes it
        Def focusDef, focusNext;
        ComboDef focusCombo, focusComboNext;

        bool Lit(SkillDef k, Player p)
        {
            if (focusDef != null && DB.KnownPartners(focusDef, k, p)) return true;
            if (focusCombo == null) return false;
            if (focusCombo.IsTransform) return k.tags != null && System.Array.IndexOf(k.tags, focusCombo.tag) >= 0;
            return System.Array.IndexOf(focusCombo.needs, k.id) >= 0;
        }

        void ComboPop(SkillDef k, Player p, float x, float y, float sz, float S)
        {
            if (!Lit(k, p)) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f);
            var c = Color.Lerp(DB.ComboColor, Color.white, pulse * 0.6f);
            float g = S * (1f + pulse);
            Box(x - g, y - g, sz + 2f * g, S, c); Box(x - g, y + sz + g - S, sz + 2f * g, S, c);
            Box(x - g, y - g, S, sz + 2f * g, c); Box(x + sz + g - S, y - g, S, sz + 2f * g, c);
        }

        void TipDef(Def d, Player p)
        {
            focusNext = d;
            var lines = new List<(string, Color)>();
            if (!string.IsNullOrEmpty(d.desc)) lines.Add((d.desc, TipGrey));
            string extra = DB.Extra(d);
            if (d is SkillDef k && k.st.Any) lines.Add((k.st.ToString(), DB.ExtraColor(d)));
            else if (!string.IsNullOrEmpty(extra) && !(d is SkillDef)) lines.Add((extra, TipStat));
            // which transformation sets this trinket counts towards, and how far along you are
            if (d is SkillDef sk && sk.tags != null)
                foreach (var tag in sk.tags)
                    if (DB.Transforms.TryGetValue(tag, out var tf))
                    {
                        int have = p.TagCount(tag);
                        bool done = p.Combo(tf.id);
                        lines.Add((done ? "* " + tf.name + ": " + tf.desc : "TRANSFORMATION " + tf.name + " " + Mathf.Min(have, tf.count) + "/" + tf.count,
                            done ? DB.TransformColor : new Color(0.45f, 0.75f, 0.7f)));
                    }
            // combos only show once they're real: one you have, or the one this very thing would complete
            foreach (var c in DB.Combos)
            {
                if (c.IsTransform || System.Array.IndexOf(c.needs, d.id) < 0) continue;
                if (p.Combo(c.id)) lines.Add((c.name + ": " + c.desc, DB.ComboColor));
                else if (DB.Completes(c, d, p))
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
                    lines.Add(("COMPLETES COMBO " + c.name + "!", Color.Lerp(DB.ComboColor, Color.white, pulse)));
                    lines.Add((c.desc, DB.ComboColor));
                }
            }
            Tip(d.name, DB.ColorOf(d), lines.ToArray());
        }

        static string ComboPartners(ComboDef c, string self, Player p)
        {
            string s = "";
            foreach (var n in c.needs)
            {
                if (n == self) continue;
                s += (s.Length > 0 ? " + " : "") + (p.HasSkill(n) ? DB.Name(n) : "???");
            }
            return s;
        }

        void TipWeapon(Weapon w, bool current)
        {
            var lines = new List<(string, Color)> { (w.def.desc, TipGrey) };
            var pl = Game.I != null ? Game.I.player : null;
            var d = w.def;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            string ammo = d.UsesHeat ? "   OVERHEATS" : d.mag > 0 ? "   MAG " + d.mag : "";
            if (pl != null)
            {
                // the numbers as they'd really be in your hands: damage per shot (x shots per pull) and pulls per second
                int perPull = d.count * (w.Has(Mod.Triple) ? 3 : 1) * Mathf.Max(1, pl.s.multiShot) * (pl.s.freeDouble ? 2 : 1) * Mathf.Max(1, d.burst);
                float dmg = pl.Dmg(d.dmg) * (w.Has(Mod.HighCaliber) ? 2.5f : 1f) * (d.Melee ? pl.s.meleeMul * Player.MeleePower : 1f);
                float rate = pl.SwingRate / Mathf.Max(0.02f, d.delay * Player.DelayMul(w));
                lines.Add(((d.Melee ? "MELEE  " : "") + "DMG " + dmg.ToString("0.#", ci) + (perPull > 1 ? " X" + perPull : "") + "   RATE " + rate.ToString("0.0", ci) + "/S" + ammo, TipStat));
            }
            else lines.Add((Armoury.Stats(d), TipStat));
            foreach (var m in w.mods) lines.Add(("+ " + Mods.Names[(int)m], Mods.Ui));
            if (w.curse != Curse.None)
            {
                lines.Add((Curses.Name(w.curse) + ": " + Curses.Desc[(int)w.curse] + Curses.Skull, Curses.Col(w.curse)));
                lines.Add(("LIFTS AFTER " + (Curses.KillsToLift - w.curseKills) + " MORE KILLS", new Color(1f, 0.95f, 0.45f)));   // what you can do about it stands out
            }
            Tip(w.def.name, w.Color, lines.ToArray());
        }

        void DrawTooltip(float S, float sm)
        {
            if (tipTitle == null) return;
            var mp = Event.current.mousePosition;
            float line = 9f * sm;
            // long lines wrap so the box never runs off the screen
            float maxW = Mathf.Max(120f * sm, Screen.width * 0.4f);
            var wrapped = new List<(string, Color)>();
            foreach (var l in tipLines) foreach (var piece in Wrap(l.Item1, maxW, sm)) wrapped.Add((piece, l.Item2));
            tipLines.Clear(); tipLines.AddRange(wrapped);
            float w = PixelFont.Width(tipTitle, S);
            foreach (var l in tipLines) w = Mathf.Max(w, PixelFont.Width(l.Item1, sm));
            float h = 12f * S + tipLines.Count * line;
            float bw = w + 8f * S, bh = h + 4f * S;
            float x = mp.x + 10f * S, y = mp.y + 8f * S;
            if (x + bw > Screen.width - 2f) x = mp.x - bw - 6f * S;
            if (y + bh > Screen.height - 2f) y = Screen.height - bh - 2f;
            x = Mathf.Max(2f, x); y = Mathf.Max(2f, y);
            Box(x, y, bw, bh, new Color(0.03f, 0.03f, 0.06f, 0.93f));
            Box(x, y, bw, S, tipColor); Box(x, y + bh - S, bw, S, tipColor);
            Box(x, y, S, bh, tipColor); Box(x + bw - S, y, S, bh, tipColor);
            PixelFont.Draw(x + 4f * S, y + 3f * S, tipTitle, S, tipColor);
            float ly = y + 3f * S + 11f * S;
            foreach (var l in tipLines) { PixelFont.Draw(x + 4f * S, ly, l.Item1, sm, l.Item2); ly += line; }
        }
        public void Label(Vector2 wp, string s, Color c) => labels.Add(new Lbl { p = wp, s = s, c = c });

        public void Popup(Vector2 wp, string s, Color c)
        {
            if (pops.Count > 40) pops.RemoveAt(0);
            pops.Add(new Pop { p = wp, s = s, c = c, t = 0.9f });
        }

        // Banners queue up so an item banner and a COMBO! banner don't overwrite each other.
        public void Banner(string title, string sub, Color c)
        {
            if (banners.Count > 4) banners.RemoveAt(0);
            banners.Add(new Ban { title = title, sub = sub, c = c });
            if (bannerT > 0.6f && banners.Count > 0) bannerT = Mathf.Min(bannerT, 0.6f);   // hurry the current one along
        }

        public void DamageFlash() { dmgFlash = Settings.Flashes ? 0.35f : 0.08f; hurtShake = 0.4f * Settings.Shake; }

        // A one-or-two frame white-out for huge explosions.
        public void ScreenFlash(float a) { if (Settings.Flashes) whiteFlash = Mathf.Max(whiteFlash, a); }

        // The weapon panel jumps every time you fire.
        public void WeaponKick() { wKick = 1f; crossKick = 1f; }
        float spFlash;
        public void SpecialReady() => spFlash = 1f;

        // Super Crate Box style: the name of the gun you just grabbed slams onto the screen, big
        string popName, popSub; Color popCol; float popT;
        const float PopTime = 1.1f;
        public void WeaponPop(string name, string sub, Color c) { popName = name; popSub = sub; popCol = c; popT = PopTime; }

        public void Tick(float dt, Game g)
        {
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                pops[i].t -= dt;
                pops[i].p += Vector2.up * dt * 1.4f;
                if (pops[i].t <= 0f) pops.RemoveAt(i);
            }
            bannerT -= dt; dmgFlash -= dt; helpT -= dt; biomeT -= dt;
            whiteFlash = Mathf.Max(0f, whiteFlash - Time.unscaledDeltaTime * 9f);
            wKick = Mathf.MoveTowards(wKick, 0f, dt * 7f);
            spFlash = Mathf.MoveTowards(spFlash, 0f, dt * 3f);
            popT -= dt;
            crossKick = Mathf.MoveTowards(crossKick, 0f, dt * 9f);
            hurtShake -= dt;
            goldPop = Mathf.MoveTowards(goldPop, 0f, dt * 4f);
            if (lastGold >= 0 && g.player.s.gold > lastGold) goldPop = 1f;
            lastGold = g.player.s.gold;
            if (bannerT <= 0f && banners.Count > 0)
            {
                cur = banners[0];
                banners.RemoveAt(0);
                bannerT = BannerTime;
            }
            TrackStats(g.player, dt);
            int bi = Biomes.Index(g.player.Depth);
            if (bi != lastBiome)
            {
                // the title card only plays the first time you reach each biome (not when you climb back into one)
                if (lastBiome >= 0 && (seenBiomes & (1 << bi)) == 0) biomeT = 3f;
                seenBiomes |= 1 << bi;
                lastBiome = bi;
            }
        }

        static void Box(float x, float y, float w, float h, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), Art.White);
            GUI.color = old;
        }

        static void Icon(Sprite s, float x, float y, float size, Color c)
        {
            if (s == null) return;
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, size, size), s.texture, ScaleMode.ScaleToFit);
            GUI.color = old;
        }

        static Color Ink(float a) => new Color(Pal.Night.r / 255f, Pal.Night.g / 255f, Pal.Night.b / 255f, a);

        // an icon with a dark outline all round (or, flash: a white glow), the way Nuclear Throne draws its HUD
        static void IconO(Sprite s, float x, float y, float size, float o, Color c, float inkA = 0.95f, bool flash = false)
        {
            if (s == null) return;
            if (flash) { var w = new Color(1f, 1f, 1f, c.a); for (int i = 0; i < 4; i++) Icon(s, x + (i == 0 ? o : i == 1 ? -o : 0f), y + (i == 2 ? o : i == 3 ? -o : 0f), size, w); return; }
            if (o > 0f && inkA > 0f)
            {
                var k = Ink(inkA * c.a);
                Icon(s, x - o, y, size, k); Icon(s, x + o, y, size, k);
                Icon(s, x, y - o, size, k); Icon(s, x, y + o, size, k);
            }
            Icon(s, x, y, size, c);
        }

        public void Draw(Game g)
        {
            if (Event.current.type != EventType.Repaint) return;
            float S = Mathf.Max(2f, Mathf.Floor(Screen.height / 205f * Settings.UiScale));   // bigger UI (5x at 1080p), times the UI SCALE option
            float sm = Mathf.Max(1f, S - 1f);
            float pad = 4f * S;
            if (g.state == Game.State.Select) { DrawSelect(g, S, sm, pad); return; }
            if (g.player == null) return;
            var p = g.player;
            var cam = g.cam.cam;
            tipTitle = null;

            // ----- world labels & popups
            // text floating in the world is sized to the world (labels stack 0.4 tiles apart), not to the HUD
            float ppu = Screen.height / (2f * g.cam.BaseSize);
            float wsm = Mathf.Max(1f, Mathf.Floor(0.36f * ppu / 7f));
            foreach (var l in labels)
            {
                Vector3 sp = cam.WorldToScreenPoint(new Vector3(l.p.x, l.p.y, 0));
                PixelFont.Draw(sp.x, Screen.height - sp.y, l.s, wsm, l.c, 1);
            }
            foreach (var po in pops)
            {
                Vector3 sp = cam.WorldToScreenPoint(new Vector3(po.p.x, po.p.y, 0));
                var c = po.c; c.a = Mathf.Clamp01(po.t * 2f);
                PixelFont.Draw(sp.x, Screen.height - sp.y, po.s, wsm, c, 1);
            }

            if (dmgFlash > 0f) Box(0, 0, Screen.width, Screen.height, new Color(1f, 0f, 0f, dmgFlash * 0.6f));

            // ===== Nuclear Throne style: one tight cluster in the top-left, chunky outlines, icons without boxes =====
            Color ink = Ink(0.95f);
            float hx = pad, hy = pad;
            float hpW = Mathf.Clamp(60f + p.s.maxHp * 0.12f, 70f, 140f) * S, hpH = 10f * S;
            // ----- health bar: a white trail shows the chunk you just lost
            {
                float sx0 = 0f, sy0 = 0f;
                if (hurtShake > 0f) { sx0 = Random.Range(-2f, 2f) * S * hurtShake; sy0 = Random.Range(-2f, 2f) * S * hurtShake; }
                float bx = hx + sx0, byy = hy + sy0;
                float k = Mathf.Clamp01(p.s.hp / (float)Mathf.Max(1, p.s.maxHp));
                hpTrail = Mathf.Max(k, Mathf.MoveTowards(hpTrail, k, Time.unscaledDeltaTime * 0.6f));
                bool low = k <= 0.25f;
                var edge = hpFlashT > 0f ? Color.Lerp(ink, new Color(1f, 0.55f, 0.55f), Mathf.Abs(Mathf.Sin(Time.unscaledTime * 10f))) : ink;
                Box(bx - 2f * S, byy - 2f * S, hpW + 4f * S, hpH + 4f * S, edge);
                Box(bx, byy, hpW, hpH, new Color(0.2f, 0.05f, 0.07f));
                Box(bx, byy, hpW * hpTrail, hpH, new Color(1f, 0.92f, 0.85f));
                Color fill = low ? Color.Lerp(new Color(0.75f, 0.08f, 0.1f), new Color(1f, 0.35f, 0.3f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)) : new Color(0.9f, 0.18f, 0.18f);
                Box(bx, byy, hpW * k, hpH, fill);
                Box(bx, byy + hpH - 3f * S, hpW * k, 3f * S, fill * new Color(0.72f, 0.72f, 0.72f, 1f));   // shade
                Box(bx, byy, hpW * k, S, new Color(1f, 0.55f, 0.5f));                                     // shine
                PixelFont.Draw(bx + hpW * 0.5f, byy + (hpH - 7f * S) * 0.5f + 0.5f * S, p.s.hp + "/" + p.s.maxHp, S, Color.white, 1);
                if (Hover(bx, byy, hpW, hpH))
                    Tip("HEALTH " + p.s.hp + "/" + p.s.maxHp, new Color(1f, 0.45f, 0.45f), ("ALE, KEGS AND SOME TRINKETS HEAL YOU", TipGrey), ("HEART TRINKETS RAISE YOUR MAX HP", TipGrey));
                if (hpFlashT > 0f)
                    PixelFont.Draw(bx + hpW + 5f * S, byy + 1.5f * S, "+" + hpGain + " MAX HP", sm, new Color(1f, 0.5f, 0.5f, Mathf.Clamp01(hpFlashT)));
                // air: bubbles that pop one by one while you're under water or in steam
                if (p.breath < Player.BreathMax - 0.01f)
                {
                    int bubbles = Mathf.CeilToInt(p.breath / Player.BreathMax * 8f);
                    bool out0 = p.breath <= 0f;
                    for (int i = 0; i < 8; i++)
                    {
                        float bxx = bx + hpW + 5f * S + i * 6f * S, byb = byy + 2f * S;
                        var bc = i < bubbles ? new Color(0.7f, 0.92f, 1f) : new Color(0.25f, 0.3f, 0.35f, 0.6f);
                        if (out0 && ((int)(Time.unscaledTime * 6f)) % 2 == 0) bc = new Color(1f, 0.4f, 0.4f);
                        var old = GUI.color; GUI.color = bc;
                        GUI.DrawTexture(new Rect(bxx, byb, 5f * S, 5f * S), Art.Circle.texture);
                        GUI.color = old;
                    }
                }
            }

            // ----- the gun in your hands, its ammo, and the other one
            float small = Mathf.Max(1f, sm - (sm > 2f ? 1f : 0f));
            float wy = hy + hpH + 6f * S;
            float wsz = 14f * S;
            {
                var w = p.weapon;
                float kx = -wKick * 2f * S, ky = wKick * 1.5f * S;
                float tx = hx + wsz + 5f * S;
                if (w != null)
                {
                    IconO(w.def.icon, hx + kx, wy + ky, wsz, S, Color.white);
                    if (Hover(hx, wy, wsz, wsz)) TipWeapon(w, true);
                    if (p.ChargeFrac01 > 0f) { Box(hx, wy + wsz + S, wsz, 2f * S, ink); Box(hx, wy + wsz + S, wsz * p.ChargeFrac01, 2f * S, w.def.col); }
                    float ty = wy + (wsz - 7f * S) * 0.5f;
                    if (w.def.UsesHeat)
                    {
                        var hc = w.overheated ? (((int)(Time.unscaledTime * 8f)) % 2 == 0 ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.8f, 0.3f))
                                              : Color.Lerp(new Color(0.4f, 0.9f, 1f), new Color(1f, 0.4f, 0.2f), w.heat);
                        float hw = 26f * S;
                        Box(tx - S, ty + 1f * S, hw + 2f * S, 5f * S, ink);
                        Box(tx, ty + 2f * S, hw * w.heat, 3f * S, hc);
                        if (w.overheated) PixelFont.Draw(tx, ty - 7f * sm, "HOT", sm, hc);
                        tx += hw + 6f * S;
                    }
                    else if (p.MagSize(w) > 0)
                    {
                        int mag = p.MagSize(w), am = Mathf.Max(0, w.ammo);
                        bool lowA = am <= Mathf.Max(1, mag / 4);
                        var ac = lowA ? (((int)(Time.unscaledTime * 6f)) % 2 == 0 ? new Color(1f, 0.35f, 0.3f) : Color.white) : Color.white;
                        string at = am.ToString();
                        PixelFont.Draw(tx, ty, at, S, ac);
                        float aw = PixelFont.Width(at, S);
                        PixelFont.Draw(tx + aw + 2f * S, ty + 7f * S - 7f * small, "/" + mag, small, new Color(0.65f, 0.65f, 0.7f));
                        if (p.reloading)
                        {
                            Box(tx - S, ty + 8f * S, 24f * S + 2f * S, 3f * S, ink);
                            Box(tx, ty + 9f * S, 24f * S * p.ReloadFrac, S, new Color(1f, 0.9f, 0.5f));
                        }
                        tx += Mathf.Max(aw + 2f * S + PixelFont.Width("/" + mag, small), 24f * S) + 6f * S;
                    }
                    if (w.curse != Curse.None)
                    {
                        var cc = Curses.Col(w.curse);
                        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
                        IconO(Curses.Icon(w.curse), hx + wsz - 6f * S, wy - 2f * S, 7f * S, S * 0.5f, new Color(1f, 1f, 1f, pulse));
                        if (Hover(hx + wsz - 6f * S, wy - 2f * S, 7f * S, 7f * S))
                            Tip(Curses.Name(w.curse) + Curses.Skull, cc, (Curses.Desc[(int)w.curse] + Curses.Skull, TipGrey), ("KILL " + (Curses.KillsToLift - w.curseKills) + " MORE FOES TO LIFT IT (" + w.curseKills + "/" + Curses.KillsToLift + ")", TipStat));
                    }
                }
                // the other gun, smaller and dimmer
                var o = p.slots[1 - p.cur];
                if (o != null)
                {
                    float os = 10f * S;
                    IconO(o.def.icon, tx, wy + (wsz - os) * 0.5f, os, S * 0.5f, new Color(1f, 1f, 1f, 0.55f));
                    if (Hover(tx, wy, os, wsz)) TipWeapon(o, false);
                    PixelFont.Draw(tx + os * 0.5f, wy + wsz - S, GameInput.Prompt(Btn.Swap), small, new Color(0.6f, 0.6f, 0.65f), 1);
                }
            }

            // ----- ability and item: bottom left, each with the key that uses it
            float isz = 12f * S;
            float ry = Screen.height - pad - isz - 5f * S;
            float rx = hx;
            if (p.special != null)
            {
                bool ready = p.charges > 0;
                IconO(p.special.icon, rx, ry, isz, S, new Color(1f, 1f, 1f, ready ? 1f : 0.4f));
                if (spFlash > 0f) IconO(p.special.icon, rx, ry, isz, S, new Color(1f, 1f, 1f, spFlash * 0.6f), 0f, true);   // flashes when a charge comes back
                // charges as pips, the next one filling
                int max = p.MaxCharges;
                float pw = Mathf.Max(S, (isz - (max - 1) * S) / Mathf.Max(1, max));
                for (int i = 0; i < max; i++)
                {
                    float px = rx + i * (pw + S);
                    Box(px - S * 0.5f, ry + isz + S * 0.5f, pw + S, 3f * S, ink);
                    float f = i < p.charges ? 1f : i == p.charges ? p.ChargeFrac : 0f;
                    var pc = i < p.charges ? new Color(0.55f, 0.95f, 0.45f) : new Color(0.45f, 0.85f, 1f);
                    Box(px, ry + isz + S, pw * f, 2f * S, pc);
                }
                PixelFont.Draw(rx + isz + 3f * S, ry + (isz - 7f * sm) * 0.5f, GameInput.Prompt(Btn.Special), sm, new Color(0.6f, 0.8f, 0.95f));
                if (Hover(rx, ry, isz, isz + 4f * S))
                {
                    TipDef(p.special, p);
                    tipLines.Add(("CHARGES: " + p.charges + "/" + p.MaxCharges + "   RECHARGES OVER TIME, KILLS SPEED IT UP", TipStat));
                }
                rx += isz + 3f * S + PixelFont.Width(GameInput.Prompt(Btn.Special), sm) + 8f * S;
            }
            ItemStack st = p.items.Count > 0 ? p.items[Mathf.Clamp(p.sel, 0, p.items.Count - 1)] : null;
            if (st != null)
            {
                IconO(st.def.icon, rx, ry, isz, S, Color.white);
                PixelFont.Draw(rx + isz + S, ry + isz - 7f * small, st.count.ToString(), small, Color.white, 2);
                string ik = GameInput.Prompt(Btn.Item);
                PixelFont.Draw(rx + isz + 3f * S, ry + (isz - 7f * sm) * 0.5f, ik, sm, new Color(0.65f, 0.9f, 0.65f));
                if (p.items.Count > 1) PixelFont.Draw(rx + isz + 3f * S, ry + isz + S, GameInput.Prompt(Btn.Cycle) + ": NEXT", small, new Color(0.55f, 0.65f, 0.55f));
                if (Hover(rx, ry, isz, isz))
                {
                    TipDef(st.def, p);
                    tipLines.Add(("CARRYING " + st.count + " (" + p.ItemTotal + "/" + p.s.itemCap + ")   C TO USE, V TO CYCLE", TipStat));
                }
                rx += isz + 5f * S + PixelFont.Width(ik, sm) + 6f * S;
            }
            // gold, and the void key's parts beside it: just under the gun and its ammo
            {
                rx = hx; ry = wy + wsz + 5f * S; isz = 10f * S;
                float gp = 1f + goldPop * 0.3f;
                float gsz = 8f * S * gp;
                IconO(Art.Icons["coin"], rx, ry + (isz - gsz) * 0.5f, gsz, S * 0.5f, Color.white);
                string gs = p.s.gold.ToString();
                PixelFont.Draw(rx + gsz + 2f * S, ry + (isz - 7f * S) * 0.5f - goldPop * 2f * S, gs, S * (goldPop > 0.5f ? 1.25f : 1f), Color.Lerp(new Color(1f, 0.85f, 0.3f), Color.white, goldPop));
                float kx = rx + gsz + 2f * S + PixelFont.Width(gs, S * 1.25f) + 6f * S;
                if (p.voidShards != 0)
                {
                    if (p.voidShards == 3 && Art.VoidKey != null) IconO(Art.VoidKey, kx, ry - S, 11f * S, S * 0.5f, Color.white);
                    else if (Art.KeyShine != null)
                        for (int i = 0; i < 2; i++)
                            Icon(Art.KeyShine[0], kx + i * 7f * S, ry, 9f * S, (p.voidShards & (1 << i)) != 0 ? new Color(0.85f, 0.6f, 1f) : new Color(0.3f, 0.3f, 0.35f, 0.6f));
                    if (Hover(kx, ry - S, 16f * S, 11f * S))
                        Tip(p.voidShards == 3 ? "THE VOID KEY" : "PART OF THE VOID KEY", new Color(0.8f, 0.55f, 1f),
                            (p.voidShards == 3 ? "BEAT ORNIN WITH IT AND A DOOR TO THE VOID OPENS" : "BEAT THE SECOND BOSS WITHIN 6 MINUTES FOR THE OTHER PART", TipGrey));
                }
            }
            if (Settings.ShowStats) DrawStats(p, pad, ry + isz + 6f * S, S, sm);

            // ----- depth, top middle
            if (World.SkyMode) PixelFont.Draw(Screen.width * 0.5f, pad, "ABOVE THE CLOUDS", S, new Color(1f, 0.85f, 0.4f), 1);
            else
            {
                var bio = Biomes.Cur[Biomes.Index(p.Depth)];
                PixelFont.Draw(Screen.width * 0.5f, pad, "-" + (p.Depth + Enemy.VoidDeeper), S, new Color(0.95f, 0.85f, 0.55f), 1);
                PixelFont.Draw(Screen.width * 0.5f, pad + 9f * S, bio.name, small, (Color)bio.rim, 1);
            }

            // ----- trinkets: outlined icons along the top-right, Nuclear Throne mutation-style
            DrawTrinkets(p, S, sm, pad);
            if (p.CurrentBuff != Player.Buff.None)
            {
                string b = p.CurrentBuff == Player.Buff.Speed ? "+SPEED" : p.CurrentBuff == Player.Buff.Damage ? "+DAMAGE" : "+FIRE RATE";
                PixelFont.Draw(Screen.width * 0.5f, Screen.height - pad - 30f * S, b + " " + p.BuffTime.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), S, Mods.Col(Mod.Hunter), 1);
            }

            if (g.boss != null && !g.boss.dead) DrawBossBar(g.boss, S, sm, pad);
            if (g.boss2 != null && !g.boss2.dead) DrawBossBar(g.boss2, S, sm, pad, 16f * S, true);

            // ----- weapon name slam
            if (popT > 0f && !string.IsNullOrEmpty(popName))
            {
                float age = PopTime - popT;
                float sc = S * (age < 0.08f ? Mathf.Lerp(5f, 3f, age / 0.08f) : 3f);   // lands big, then settles
                float a = Mathf.Clamp01(popT * 3f);
                float py = Screen.height * 0.34f + Mathf.Max(0f, 0.08f - age) * 60f * S;
                var sh = new Color(Pal.Night.r / 255f, Pal.Night.g / 255f, Pal.Night.b / 255f, a);
                PixelFont.Draw(Screen.width * 0.5f + S, py + S, popName, sc, sh, 1);
                var pc = age < 0.06f ? Color.white : popCol; pc.a = a;
                PixelFont.Draw(Screen.width * 0.5f, py, popName, sc, pc, 1);
                if (!string.IsNullOrEmpty(popSub)) DrawWrapped(Screen.width * 0.5f, py + 9f * sc / S * S + 4f * S, Short(popSub, 60), sm, new Color(1f, 1f, 1f, a * 0.9f), Screen.width * 0.7f);
            }

            // ----- banner
            if (bannerT > 0f)
            {
                float a = Mathf.Clamp01(bannerT * 2.5f) * Mathf.Clamp01((BannerTime - bannerT) * 8f + 0.3f);
                var c = cur.c; c.a = a;
                PixelFont.Draw(Screen.width * 0.5f, Screen.height * 0.22f, cur.title, FitSize(cur.title, S * 2f, Screen.width * 0.9f), c, 1);
                DrawWrapped(Screen.width * 0.5f, Screen.height * 0.22f + 18f * S, cur.sub, S, new Color(1f, 1f, 1f, a), Screen.width * 0.6f);
            }
            else if (biomeT > 0f && !World.SkyMode)
            {
                var bio = Biomes.Cur[Biomes.Index(p.Depth)];
                float a = Mathf.Clamp01(biomeT);
                var c = (Color)bio.rim; c.a = a;
                PixelFont.Draw(Screen.width * 0.5f, Screen.height * 0.22f, bio.name, S * 2f, c, 1);
                PixelFont.Draw(Screen.width * 0.5f, Screen.height * 0.22f + 18f * S, "IT GETS NASTIER DOWN HERE", S, new Color(1f, 1f, 1f, a), 1);
            }

            // ----- controls help
            if (helpT > 0f && g.state == Game.State.Playing)
            {
                var c = new Color(1f, 1f, 1f, Mathf.Clamp01(helpT));
                // the help speaks the language of whatever you're holding: keys and mouse, or the pad's buttons
                string P(Btn b) => GameInput.Prompt(b);
                string h1 = GameInput.UsingStick
                    ? (GameInput.SwapSticks ? "R-STICK MOVE   L-STICK AIM   " : "L-STICK MOVE   R-STICK AIM   ") + P(Btn.Jump) + " JUMP   " + P(Btn.Attack) + " SHOOT"
                    : P(Btn.Left) + "/" + P(Btn.Right) + " MOVE   " + P(Btn.Jump) + " JUMP   MOUSE AIM   CLICK SHOOT";
                string h2 = P(Btn.Special) + " SPECIAL   " + P(Btn.Reload) + " RELOAD   " + P(Btn.Item) + " ITEM   " + P(Btn.Cycle) + " CYCLE   " + P(Btn.Swap) + " SWAP"
                    + (GameInput.UsingStick ? "   " + P(Btn.Zoom) + " LOOK FAR" : "");
                PixelFont.Draw(Screen.width * 0.5f, Screen.height - pad - 30f * sm, h1, FitSize(h1, sm, Screen.width * 0.9f), c, 1);
                PixelFont.Draw(Screen.width * 0.5f, Screen.height - pad - 20f * sm, h2, FitSize(h2, sm, Screen.width * 0.9f), c, 1);
                PixelFont.Draw(Screen.width * 0.5f, Screen.height - pad - 10f * sm, P(Btn.Interact) + " BUY / TAKE   " + (GameInput.UsingStick ? "MENU" : "ESC") + " PAUSE", sm, c, 1);
            }

            if (g.state == Game.State.Playing && p.mode != Player.Mode.Dead)
            {
                DrawCrosshair(g, p, S);
                if (tipTitle == null && hoverWeapon != null) TipWeapon(hoverWeapon, false);
                else if (tipTitle == null && hoverDef != null) TipDef(hoverDef, p);
                DrawTooltip(S, sm);
            }
            hoverDef = null; hoverWeapon = null;
            focusDef = focusNext; focusNext = null;
            focusCombo = focusComboNext; focusComboNext = null;
            if (whiteFlash > 0f) Box(0, 0, Screen.width, Screen.height, new Color(1f, 1f, 1f, Mathf.Clamp01(whiteFlash)));

            // ----- pause / game over
            if (g.state != Game.State.Paused) { QuitHovered = false; OptionsHovered = false; }
            if (g.state != Game.State.Dead) { ChangeHovered = false; DeadQuitHovered = false; }
            if (g.state == Game.State.Paused) DrawPause(p, S, sm, pad);
            else if (g.state == Game.State.Dead)
            {
                float a = Mathf.Clamp01(g.deadT * 1.5f);
                Box(0, 0, Screen.width, Screen.height, new Color(0f, 0f, 0f, 0.6f * a));
                float cy = Screen.height * 0.3f;
                PixelFont.Draw(Screen.width * 0.5f, cy - 16f * S, "YOU DIED", S * 3f, new Color(1f, 0.3f, 0.3f, a), 1);
                PixelFont.Draw(Screen.width * 0.5f, cy + 10f * S, "KILLED BY " + p.lastCause, sm, new Color(1f, 0.6f, 0.55f, a), 1);
                PixelFont.Draw(Screen.width * 0.5f, cy + 20f * S, "DEPTH -" + g.maxDepth + "    GOLD " + p.s.gold + "    KILLS " + p.s.kills, S, new Color(0.9f, 0.8f, 0.5f, a), 1);
                PixelFont.Draw(Screen.width * 0.5f, cy + 31f * S, "BEST -" + g.best + (g.newBest ? "  NEW RECORD!" : ""), sm, new Color(0.8f, 0.8f, 0.9f, a), 1);
                // what you carried: trinkets and combos, hover for details
                DeathLoot(p, cy + 42f * S, S, sm, a);
                bool test = TestMode.On;
                if (g.deadT > 1f && ((int)(Time.unscaledTime * 2f)) % 2 == 0)
                {
                    string again = test ? (GameInput.UsingStick ? "A: TEST AGAIN, SAME GEAR" : "SPACE OR CLICK: TEST AGAIN, SAME GEAR")
                                        : (GameInput.UsingStick ? "A: DIG AGAIN" : "SPACE OR CLICK: DIG AGAIN");
                    PixelFont.Draw(Screen.width * 0.5f, cy + 81f * S, again, S, Color.white, 1);
                }
                if (g.deadT > 1f)
                {
                    // buttons side by side, with room between them: CHANGE DIGGER (not in test runs) and QUIT
                    var mouse = Event.current.mousePosition;
                    string ch = (GameInput.UsingStick ? "X" : "E") + ": CHANGE DIGGER";
                    string qt = (GameInput.UsingStick ? "START" : "ESC") + ": QUIT TO MENU";
                    float bh = 11f * sm, gap = 10f * S, by2 = cy + 93f * S - 2f * sm;
                    float cbw = test ? 0f : PixelFont.Width(ch, sm) + 10f * sm, qbw = PixelFont.Width(qt, sm) + 10f * sm;
                    float total = cbw + qbw + (test ? 0f : gap);
                    float bx2 = Screen.width * 0.5f - total * 0.5f;
                    if (!test)
                    {
                        ChangeHovered = Sfx.HoverUi(new Rect(bx2, by2, cbw, bh).Contains(mouse), new Rect(bx2, by2, cbw, bh));
                        Box(bx2, by2, cbw, bh, ChangeHovered ? new Color(1f, 0.82f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.12f));
                        PixelFont.Draw(bx2 + cbw * 0.5f, by2 + 2f * sm, ch, sm, ChangeHovered ? Color.black : Color.white, 1);
                        bx2 += cbw + gap;
                    }
                    else ChangeHovered = false;
                    DeadQuitHovered = Sfx.HoverUi(new Rect(bx2, by2, qbw, bh).Contains(mouse), new Rect(bx2, by2, qbw, bh));
                    Box(bx2, by2, qbw, bh, DeadQuitHovered ? new Color(1f, 0.82f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.12f));
                    PixelFont.Draw(bx2 + qbw * 0.5f, by2 + 2f * sm, qt, sm, DeadQuitHovered ? Color.black : Color.white, 1);
                }
                DrawTooltip(S, sm);
            }
        }

        // the death screen's record of the run: every trinket as an icon, combos as names under them (hover either)
        void DeathLoot(Player p, float y, float S, float sm, float a)
        {
            tipTitle = null;
            float sz = 9f * S, gap = 2f * S;
            int n = p.skillOrder.Count;
            int cols = Mathf.Max(1, Mathf.Min(n, Mathf.FloorToInt(Screen.width * 0.6f / (sz + gap))));
            int rows = Mathf.Min(2, (n + cols - 1) / cols);
            for (int i = 0; i < Mathf.Min(n, cols * rows); i++)
            {
                var k = p.skillOrder[i];
                int inRow = Mathf.Min(cols, n - (i / cols) * cols);
                float x0 = Screen.width * 0.5f - (inRow * (sz + gap) - gap) * 0.5f;
                float x = x0 + (i % cols) * (sz + gap), yy = y + (i / cols) * (sz + gap);
                bool pop = Lit(k, p);
                float ps = pop ? sz * 0.25f * (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 9f)) : 0f;
                Icon(k.icon, x - ps * 0.5f, yy - ps * 0.5f, sz + ps, new Color(1f, 1f, 1f, a));
                ComboPop(k, p, x, yy, sz, S * 0.5f);
                Box(x, yy + sz, sz, Mathf.Max(1f, S * 0.5f), DB.RarityColor(k.rarity));
                int cnt = p.SkillCount(k.id);
                if (cnt > 1) PixelFont.Draw(x + sz, yy + sz - 6f * sm, cnt.ToString(), sm, Color.white, 2);
                if (Hover(x, yy, sz, sz)) { TipDef(k, p); if (cnt > 1) tipLines.Insert(0, ("x" + cnt, Color.white)); }
            }
            float cy = y + rows * (sz + gap) + 3f * S;
            if (p.comboOrder.Count == 0) return;
            // combos: one centred line of names
            float total = 0f;
            foreach (var c in p.comboOrder) total += PixelFont.Width(c.name, sm) + 8f * sm;
            float cx = Screen.width * 0.5f - total * 0.5f;
            foreach (var c in p.comboOrder)
            {
                float w = PixelFont.Width(c.name, sm);
                bool hov = Hover(cx - 2f * sm, cy - sm, w + 4f * sm, 10f * sm);
                if (hov)
                {
                    focusComboNext = c;
                    Box(cx - 2f * sm, cy - sm, w + 4f * sm, 10f * sm, new Color(1f, 1f, 1f, 0.12f));
                    string parts = "";
                    if (c.needs != null) foreach (var nd in c.needs) parts += (parts.Length > 0 ? " + " : "") + DB.Name(nd);
                    Tip(c.name, DB.ColorOfCombo(c), (c.desc, TipGrey), (parts, new Color(0.7f, 0.7f, 0.7f)));
                }
                var col = DB.ColorOfCombo(c); col.a = a;
                PixelFont.Draw(cx, cy, c.name, sm, col);
                cx += w + 8f * sm;
            }
        }

        void DrawTrinkets(Player p, float S, float sm, float pad)
        {
            const int cols = 8;
            float sz = 10f * S, gap = 2f * S;
            float top = pad;
            float right = Screen.width - pad;
            int n = p.skillOrder.Count;
            for (int i = 0; i < n; i++)
            {
                var k = p.skillOrder[i];
                int col = i % cols, row = i / cols;
                float x = right - (col + 1) * (sz + gap) + gap;   // newest furthest left, rows wrap downwards
                float y = top + row * (sz + gap);
                bool pop = Lit(k, p);
                float ps = pop ? sz * 0.25f * (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 9f)) : 0f;   // partners pop out
                IconO(k.icon, x - ps * 0.5f, y - ps * 0.5f, sz + ps, S, Color.white);
                ComboPop(k, p, x, y, sz, S * 0.5f);
                int cnt = p.SkillCount(k.id);
                if (Hover(x, y, sz, sz)) { TipDef(k, p); if (cnt > 1) tipLines.Insert(0, ("x" + cnt, Color.white)); }
                if (cnt > 1) PixelFont.Draw(x + sz + S, y + sz - 6f * sm, cnt.ToString(), sm, Color.white, 2);
            }
            // (combos are listed in the pause menu only)
        }

        int pausePage, pausePages = 1;
        public void ResetPausePage() => pausePage = 0;
        public void PageTurn(int d) => pausePage = Mathf.Clamp(pausePage + d, 0, Mathf.Max(0, pausePages - 1));

        // Pause screen: everything you carry as icons (hover for details, same as in-game), combos on the right.
        // Pages when it doesn't all fit.
        public bool QuitHovered { get; private set; }
        public bool ChangeHovered { get; private set; }   // the death screen's CHANGE DIGGER button
        public bool DeadQuitHovered { get; private set; } // the death screen's QUIT TO MENU button
        public bool OptionsHovered { get; private set; }

        void DrawPause(Player p, float S, float sm, float pad)
        {
            tipTitle = null;
            Box(0, 0, Screen.width, Screen.height, new Color(0f, 0f, 0f, 0.82f));
            PixelFont.Draw(Screen.width * 0.5f, pad * 2f, "PAUSED", S * 3f, Color.white, 1);

            // options button, top left
            {
                string label = GameInput.UsingStick ? "OPTIONS (X)" : "OPTIONS (E)";
                float bw = PixelFont.Width(label, sm) + 10f * sm, bh = 11f * sm;
                float bx = pad * 2f, by = pad * 2f;
                OptionsHovered = Sfx.HoverUi(new Rect(bx, by, bw, bh).Contains(Event.current.mousePosition), new Rect(bx, by, bw, bh));
                Box(bx, by, bw, bh, OptionsHovered ? new Color(1f, 0.82f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.12f));
                PixelFont.Draw(bx + bw * 0.5f, by + 2f * sm, label, sm, OptionsHovered ? Color.black : Color.white, 1);
            }
            // save & quit button, top right
            {
                string label = GameInput.UsingStick ? "SAVE & QUIT (Y)" : "SAVE & QUIT (Q)";
                float bw = PixelFont.Width(label, sm) + 10f * sm, bh = 11f * sm;
                float bx = Screen.width - pad * 2f - bw, by = pad * 2f;
                QuitHovered = Sfx.HoverUi(new Rect(bx, by, bw, bh).Contains(Event.current.mousePosition), new Rect(bx, by, bw, bh));
                Box(bx, by, bw, bh, QuitHovered ? new Color(1f, 0.82f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.12f));
                PixelFont.Draw(bx + bw * 0.5f, by + 2f * sm, label, sm, QuitHovered ? Color.black : Color.white, 1);
            }
            PixelFont.Draw(Screen.width * 0.5f, pad * 2f + 26f * S, GameInput.UsingStick ? "B TO RESUME    LEFT/RIGHT TURN PAGES" : "ESC TO RESUME    HOVER FOR DETAILS", sm, new Color(0.8f, 0.8f, 0.8f), 1);

            float top = pad * 2f + 42f * S;
            float bottom = Screen.height - pad - 14f * S;
            float line = 10f * sm;
            float colW = Screen.width * 0.5f - pad * 3f;
            float cell = 15f * S, isz = 12f * S;

            // ---- left: gear
            float x = pad * 2f, y = top;
            PixelFont.Draw(x, y, "GEAR", S, DB.KindColor(Kind.Attack));
            y += 11f * S;
            float gx = x;
            for (int i = 0; i < 2; i++)
            {
                var w = p.slots[i];
                if (w == null) continue;
                Box(gx, y, isz * 1.5f, isz, new Color(1f, 1f, 1f, i == p.cur ? 0.12f : 0.05f));
                Icon(w.def.icon, gx + 1f * S, y + 1f * S, isz - 2f * S, Color.white);
                // its modifiers: tiny runes down the right-hand side, each with its own popup
                float ms = isz / 3f;
                for (int m = 0; m < w.mods.Count; m++)
                {
                    float mx = gx + isz * 1.5f + S * 0.5f, my = y + m * ms;
                    Icon(Mods.Icon(w.mods[m]), mx, my, ms, Color.white);
                    if (Hover(mx, my, ms, ms)) Tip(Mods.Names[(int)w.mods[m]], Mods.Signature, (Mods.Desc[(int)w.mods[m]], TipGrey));
                }
                if (Hover(gx, y, isz * 1.5f, isz)) TipWeapon(w, i == p.cur);
                gx += isz * 1.5f + (w.mods.Count > 0 ? ms + S : 0f) + 3f * S;
            }
            if (p.special != null)
            {
                Box(gx, y, isz, isz, new Color(0.45f, 0.85f, 1f, 0.1f));
                Icon(p.special.icon, gx + 1f * S, y + 1f * S, isz - 2f * S, Color.white);
                if (Hover(gx, y, isz, isz)) { TipDef(p.special, p); tipLines.Add(("CHARGES: " + p.charges + "/" + p.MaxCharges, TipStat)); }
                gx += isz + 3f * S;
            }
            foreach (var it in p.items)
            {
                Box(gx, y, isz, isz, new Color(0.55f, 1f, 0.55f, 0.08f));
                Icon(it.def.icon, gx + 1f * S, y + 1f * S, isz - 2f * S, Color.white);
                PixelFont.Draw(gx + isz, y + isz - 6f * sm, "x" + it.count, sm, Color.white, 2);
                if (Hover(gx, y, isz, isz)) { TipDef(it.def, p); tipLines.Add(("CARRYING " + it.count, TipStat)); }
                gx += isz + 3f * S;
            }
            y += isz + 4f * S;
            y += 4f * S;

            // ---- left: trinkets as an icon grid
            int n = p.skillOrder.Count;
            PixelFont.Draw(x, y, "TRINKETS (" + n + ")", S, new Color(0.75f, 0.55f, 1f));
            y += 11f * S;
            int cols = Mathf.Max(1, Mathf.FloorToInt(colW / cell));
            int rows = Mathf.Max(1, Mathf.FloorToInt((bottom - y) / cell));
            int perPage = cols * rows;
            int tPages = Mathf.Max(1, (n + perPage - 1) / perPage);
            if (n == 0) PixelFont.Draw(x, y, "NONE YET. OPEN CHESTS!", sm, new Color(0.6f, 0.6f, 0.6f));

            // ---- right: combos (two lines each), half-finished ones after them
            float rx = Screen.width * 0.5f + pad;
            float ry0 = top + 11f * S;
            int perCombo = Mathf.Max(1, Mathf.FloorToInt((bottom - ry0) / (line * 1.15f)));
            int cPages = Mathf.Max(1, (p.comboOrder.Count + perCombo - 1) / perCombo);

            pausePages = Mathf.Max(tPages, cPages);
            pausePage = Mathf.Clamp(pausePage, 0, pausePages - 1);

            for (int i = pausePage * perPage; i < Mathf.Min(n, (pausePage + 1) * perPage); i++)
            {
                var k = p.skillOrder[i];
                int li = i - pausePage * perPage;
                float cx = x + (li % cols) * cell, cy = y + (li / cols) * cell;
                Box(cx, cy, isz, isz, new Color(1f, 1f, 1f, 0.06f));
                bool pop = Lit(k, p);
                float ps = pop ? isz * 0.25f * (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 9f)) : 0f;
                Icon(k.icon, cx + 1f * S - ps * 0.5f, cy + 1f * S - ps * 0.5f, isz - 2f * S + ps, Color.white);
                ComboPop(k, p, cx, cy, isz, S * 0.5f);
                Box(cx, cy + isz - S, isz, S, DB.RarityColor(k.rarity));
                int cnt = p.SkillCount(k.id);
                if (cnt > 1) PixelFont.Draw(cx + isz, cy + isz - 6f * sm, cnt.ToString(), sm, Color.white, 2);
                if (Hover(cx, cy, isz, isz)) { TipDef(k, p); if (cnt > 1) tipLines.Insert(0, ("x" + cnt, Color.white)); }
            }

            PixelFont.Draw(rx, top, "COMBOS (" + p.comboOrder.Count + ")", S, DB.ComboColor);
            float ry = ry0;
            for (int i = pausePage * perCombo; i < Mathf.Min(p.comboOrder.Count, (pausePage + 1) * perCombo); i++)
            {
                var c = p.comboOrder[i];
                string cn = "* " + c.name;
                bool hov = Hover(rx, ry - sm, Mathf.Max(PixelFont.Width(cn, sm), colW * 0.6f), line);
                if (hov) Box(rx - 2f * sm, ry - sm, PixelFont.Width(cn, sm) + 4f * sm, line, new Color(1f, 1f, 1f, 0.1f));
                PixelFont.Draw(rx, ry, cn, sm, DB.ColorOfCombo(c));
                if (hov)
                {
                    focusComboNext = c;
                    string parts = "";
                    if (c.needs != null) foreach (var nd in c.needs) parts += (parts.Length > 0 ? " + " : "") + DB.Name(nd);
                    Tip((c.IsTransform ? "TRANSFORMATION: " : "COMBO: ") + c.name, DB.ColorOfCombo(c), (c.desc, TipGrey), (parts, new Color(0.7f, 0.7f, 0.7f)));
                }
                ry += line * 1.15f;
            }
            if (pausePages > 1)
                PixelFont.Draw(Screen.width * 0.5f, Screen.height - pad - 8f * S, "< A   PAGE " + (pausePage + 1) + " / " + pausePages + "   D >", S, Color.white, 1);
            DrawTooltip(S, sm);
        }

        // ================================================================== Isaac-style stat column

        static readonly string[] StatNames = { "SPD", "RATE", "DMG", "JUMP", "LUCK" };
        static readonly string[] StatLong = { "SPEED", "FIRE RATE", "DAMAGE", "JUMP", "LUCK" };
        static readonly string[] StatHelp =
        {
            "HOW FAST YOU RUN",
            "ATTACKS PER SECOND WITH THE WEAPON IN HAND",
            "DAMAGE PER TRIGGER PULL WITH THE WEAPON IN HAND",
            "HOW HIGH YOU JUMP",
            "CRITS, LUCKY DROPS AND BETTER LOOT",
        };
        static Sprite[] statIcons;
        readonly float[] lastStat = new float[5], statDelta = new float[5], statT = new float[5];
        bool statInit;

        static float[] StatValues(Player p) => new[] { p.Speed, p.SwingsPerSec, p.WeaponDamage, p.JumpMul, p.Luck };

        static string Fmt(int i, float v)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (i == 1 || i == 4) return v.ToString("0.0", ci);
            return v.ToString("0.00", ci);
        }

        int lastMaxHp = -1, hpGain;
        float hpTrail = 1f;
        float hpFlashT;

        void TrackStats(Player p, float dt)
        {
            hpFlashT -= dt;
            if (lastMaxHp >= 0 && p.s.maxHp > lastMaxHp) { hpGain = p.s.maxHp - lastMaxHp; hpFlashT = 2.5f; }
            lastMaxHp = p.s.maxHp;
            var v = StatValues(p);
            for (int i = 0; i < 5; i++)
            {
                statT[i] -= dt;
                if (statInit && Mathf.Abs(v[i] - lastStat[i]) > 0.001f) { statDelta[i] = v[i] - lastStat[i]; statT[i] = 2.5f; }
                lastStat[i] = v[i];
            }
            statInit = true;
        }

        void DrawStats(Player p, float x, float y, float S, float sm)
        {
            if (statIcons == null)
                statIcons = new[]
                {
                    Art.Trinket("boot", U.C(80, 200, 100)), Art.Trinket("gem", U.C(80, 220, 240)), Art.Trinket("blade", U.C(220, 80, 80)),
                    Art.Trinket("wing", U.C(200, 230, 240)), Art.Trinket("coin", U.C(170, 230, 70)),
                };
            var v = StatValues(p);
            float row = 9f * sm;
            for (int i = 0; i < 5; i++)
            {
                Icon(statIcons[i], x, y + i * row - sm, 8f * sm, Color.white);
                string val = Fmt(i, v[i]);
                if (Hover(x, y + i * row - sm, 40f * sm, row)) Tip(StatLong[i] + ": " + val, Color.white, (StatHelp[i], TipGrey));
                PixelFont.Draw(x + 10f * sm, y + i * row, val, sm, new Color(0.9f, 0.9f, 0.9f));
                if (statT[i] > 0f)
                {
                    float d = statDelta[i];
                    var c = d > 0f ? DB.StatUp : DB.StatDown;
                    c.a = Mathf.Clamp01(statT[i]);
                    PixelFont.Draw(x + 10f * sm + PixelFont.Width(val, sm) + 4f * sm, y + i * row, (d > 0f ? "+" : "") + Fmt(0, d), sm, c);
                }
            }
        }

        // Stacked label above a pickup / shop item: action, combo hint, name, description, stats or charge info.
        const float LabelGap = 0.58f;   // world units between stacked label lines (they overlapped at 0.4)

        public void ItemLabel(Vector2 at, Def d, string action, Player p, bool hovered = false)
        {
            // kept short: name, one line on what it does, the action. Hover it for everything else.
            if (hovered) hoverDef = d;
            float y = 0f;
            if (d is ModDef md)
            {
                var w = p.weapon;
                bool ok = w != null && w.CanAdd(md.mod);
                string fit = w == null ? "NO WEAPON IN HAND" : ok ? "GOES ON YOUR " + w.def.name : w.Has(md.mod) ? "ALREADY ON IT" :
                             w.mods.Count >= Mods.Max ? "WEAPON IS FULL" : "WON'T FIT";
                Label(at + Vector2.up * y, fit, ok ? DB.StatUp : DB.StatDown);
                y += LabelGap;
            }
            // one short line on what it does, and the stat changes (if any) under it
            if (d is SkillDef sk && sk.st.Any && !string.IsNullOrEmpty(d.desc)) { Label(at + Vector2.up * y, sk.st.ToString(), DB.ExtraColor(d)); y += LabelGap; }
            string line = !string.IsNullOrEmpty(d.desc) ? d.desc : DB.Extra(d);
            if (!string.IsNullOrEmpty(line)) { Label(at + Vector2.up * y, Short(line), new Color(0.85f, 0.85f, 0.85f)); y += LabelGap; }
            var cc = DB.CompletesAny(d, p);
            if (cc != null)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (hovered ? 12f : 6f));
                Label(at + Vector2.up * y, "COMPLETES " + cc.name + "!", Color.Lerp(DB.ComboColor, Color.white, hovered ? k : k * 0.4f)); y += LabelGap;
            }
            Label(at + Vector2.up * y, d.name, DB.ColorOf(d)); y += LabelGap;
            if (!string.IsNullOrEmpty(action)) Label(at + Vector2.up * y, action, Color.white);
        }

        // ================================================================== character select


        // Character select, drawn over the campfire scene: the diggers stand in a line by the fire (in the world),
        // and the one picked is described in a band across the top of the screen.
        void DrawSelect(Game g, float S, float sm, float pad)
        {
            float W = Screen.width, H = Screen.height;
            tipTitle = null;
            var c = Chars.All[Mathf.Clamp(g.charIndex, 0, Chars.All.Count - 1)];
            var gold = new Color(1f, 0.82f, 0.3f);

            // top band
            float bandH = 90f * S;
            Box(0, 0, W, bandH, new Color(0f, 0f, 0f, 0.78f));
            Box(0, bandH, W, S, new Color(gold.r, gold.g, gold.b, 0.6f));
            if (!Unlocks.CharOpen(c))
            {
                // locked: a black shape and what it takes to free them
                PixelFont.Draw(W * 0.5f, 6f * S, "LOCKED", S * 2f, new Color(0.6f, 0.6f, 0.66f), 1);
                string how = Unlocks.How("char:" + c.id) ?? "";
                PixelFont.Draw(W * 0.5f, 26f * S, "TO UNLOCK:", sm, gold, 1);
                PixelFont.Draw(W * 0.5f, 36f * S, how, FitSize(how, sm, W * 0.9f), Color.white, 1);
                string lh = GameInput.UsingStick ? "LEFT / RIGHT: CHOOSE     B: BACK" : "A / D OR CLICK: CHOOSE     ESC: BACK";
                PixelFont.Draw(W * 0.5f, H - pad - 10f * S, lh, FitSize(lh, sm, W * 0.94f), Color.white, 1);
                return;
            }
            PixelFont.Draw(W * 0.5f, 6f * S, c.name + "  THE " + c.animal, S * 2f, gold, 1);
            PixelFont.Draw(W * 0.5f, 24f * S, Short(c.title, 50), sm, Color.white, 1);
            if (!string.IsNullOrEmpty(c.blurb)) PixelFont.Draw(W * 0.5f, 33f * S, Short(c.blurb, 64), FitSize(Short(c.blurb, 64), sm, W * 0.9f), new Color(0.85f, 0.85f, 0.9f), 1);

            // loadout cards: icon and name, hover for the details
            var cards = new List<(Def d, string label, Color col)>();
            if (DB.ById.TryGetValue(c.weapon, out var wd)) cards.Add((wd, "WEAPON", DB.KindColor(Kind.Attack)));
            if (DB.ById.TryGetValue(c.special, out var spd)) cards.Add((spd, "ABILITY", DB.KindColor(Kind.Special)));
            if (c.skills != null)
                for (int i = 0; i < c.skills.Length; i++)
                    if (DB.ById.TryGetValue(c.skills[i], out var sd)) cards.Add((sd, "SKILL", i == 0 ? new Color(1f, 0.8f, 0.4f) : DB.KindColor(Kind.Skill)));
            float isz = 12f * S, cw = Mathf.Min(70f * S, (W - 20f * S) / Mathf.Max(1, cards.Count));
            float cx0 = W * 0.5f - cards.Count * cw * 0.5f, cy = 44f * S;
            for (int i = 0; i < cards.Count; i++)
            {
                var (d, label, col) = cards[i];
                float x = cx0 + i * cw;
                bool hov = Hover(x, cy, cw, isz + 12f * S);
                if (hov) Box(x + 2f * S, cy - S, cw - 4f * S, isz + 13f * S, new Color(1f, 1f, 1f, 0.12f));
                Icon(d.icon, x + 4f * S, cy, isz, Color.white);
                PixelFont.Draw(x + isz + 7f * S, cy + S, label, sm, col);
                string nm = d.name;
                PixelFont.Draw(x + isz + 7f * S, cy + 10f * S, nm, FitSize(nm, sm, cw - isz - 9f * S), Color.white);
                if (hov)
                {
                    string extra = DB.Extra(d);
                    if (string.IsNullOrEmpty(extra)) Tip(d.name, col, (d.desc, TipGrey));
                    else Tip(d.name, col, (d.desc, TipGrey), (extra, TipStat));
                }
            }

            // names float over the chosen (and hovered) digger
            var camp = g.CampRef;
            if (camp != null && camp.lineup && g.cam != null)
            {
                void NameOver(int i, Color col2)
                {
                    if (i < 0 || i >= camp.LineCount) return;
                    var f = camp.LineFeet(i);
                    Vector3 sp = g.cam.cam.WorldToScreenPoint(new Vector3(f.x, f.y + 2.9f, 0f));
                    PixelFont.Draw(sp.x, H - sp.y, Chars.All[i].name, sm, col2, 1);
                }
                if (camp.Hover >= 0 && camp.Hover != g.charIndex) NameOver(camp.Hover, new Color(0.85f, 0.85f, 0.9f));
            }
            {
                // how far along the road to the gods, top left: the diamonds every digger has found
                int have = 0;
                foreach (var ch in Chars.All) if (PlayerPrefs.GetInt("digdeep_won_" + ch.id, 0) == 1) have++;
                bool all = have >= Chars.All.Count;
                float u = Mathf.Max(1f, S - 1f);
                WonGem(pad + 5.5f * u, pad, u, all, true);
                string cnt = have + " / " + Chars.All.Count;
                PixelFont.Draw(pad + 14f * u, pad + 1f * u, cnt, sm, all ? gold : new Color(0.7f, 0.9f, 1f));
                if (Hover(pad, pad, 14f * u + PixelFont.Width(cnt, sm), 10f * u))
                    Tip("GOD-DIAMONDS " + cnt, new Color(0.6f, 0.9f, 1f), (all ? "EVERY DIGGER HAS ONE. THE GODS AWAIT ABOVE THE CLOUDS" : "FIND THE GOD-DIAMOND WITH EVERY DIGGER", TipGrey));

                // this digger's achievements, under the loadout: the God-Diamond (greyed out until found)
                bool mine = PlayerPrefs.GetInt("digdeep_won_" + c.id, 0) == 1, beat = PlayerPrefs.GetInt("digdeep_godwon_" + c.id, 0) == 1;
                float gy = cy + isz + 15f * S;
                bool voided = PlayerPrefs.GetInt("digdeep_voidwon_" + c.id, 0) == 1;
                float gx1 = W * 0.5f - 8f * u, gx2 = W * 0.5f + 8f * u;
                WonGem(gx1, gy, u, beat, mine);
                if (Hover(gx1 - 6f * u, gy, 12f * u, 10f * u))
                    Tip("GOD-DIAMOND", new Color(0.6f, 0.9f, 1f), (beat ? "FOUND, AND THE GOD ABOVE THE CLOUDS BEATEN" : mine ? "FOUND" : "NOT FOUND YET", TipGrey));
                WonGem(gx2, gy, u, false, voided, true);
                if (Hover(gx2 - 6f * u, gy, 12f * u, 10f * u))
                    Tip("VOID DIAMOND", new Color(0.8f, 0.55f, 1f), (voided ? "THE VOID ARCHMAGE BEATEN" : "NOT FOUND YET. A SECRET KEY OPENS THE WAY", TipGrey));
            }

            string hint = GameInput.UsingStick ? "LEFT / RIGHT: CHOOSE     A: START     B: BACK" : "A / D OR CLICK: CHOOSE     CLICK AGAIN OR SPACE: START     ESC: BACK";
            PixelFont.Draw(W * 0.5f, H - pad - 10f * S, hint, FitSize(hint, sm, W * 0.94f), Color.white, 1);
            DrawTooltip(S, sm);
        }

        // An 11x9 pixel diamond with a tick in it, centred on x, top at y. Gold when the god has fallen too.
        void WonGem(float x, float y, float u, bool god, bool found = true, bool isVoid = false)
        {
            int[] hw = { 3, 4, 5, 5, 4, 3, 2, 1, 0 };
            Color o = new Color(0.08f, 0.06f, 0.12f), hi = god ? new Color(1f, 0.95f, 0.7f) : new Color(0.85f, 0.98f, 1f), mid = god ? new Color(1f, 0.78f, 0.25f) : new Color(0.4f, 0.85f, 1f);
            if (isVoid) { hi = new Color(0.75f, 0.5f, 1f); mid = new Color(0.22f, 0.1f, 0.34f); }
            if (!found) { hi = new Color(0.4f, 0.4f, 0.45f); mid = new Color(0.25f, 0.25f, 0.3f); }
            float x0 = Mathf.Round(x - 5.5f * u), y0 = Mathf.Round(y);
            for (int r = 0; r < hw.Length; r++)
                for (int col = 0; col < 11; col++)
                {
                    int dx = Mathf.Abs(col - 5);
                    if (dx > hw[r]) continue;
                    bool edge = dx == hw[r] || r == 0 || r == hw.Length - 1;
                    Box(x0 + col * u, y0 + r * u, u, u, edge ? o : r <= 1 ? hi : mid);
                }
            if (!found) return;
            // the tick
            int[,] tick = { { 3, 4 }, { 4, 5 }, { 5, 4 }, { 6, 3 }, { 7, 2 } };
            for (int i = 0; i < tick.GetLength(0); i++)
            {
                Box(x0 + tick[i, 0] * u, y0 + (tick[i, 1] + 1) * u, u, u, o);
                Box(x0 + tick[i, 0] * u, y0 + tick[i, 1] * u, u, u, Color.white);
            }
        }

        // The weapon panel. It jolts on every shot.
        void DrawCrosshair(Game g, Player p, float S)
        {
            if (GameInput.UsingStick)
            {
                // the circle the reticle rides on, faintly: the twin-stick aiming ring
                float r = g.PadAimDist;
                for (int i = 0; i < 40; i++)
                {
                    float a = i / 40f * Mathf.PI * 2f;
                    Vector3 q = g.cam.cam.WorldToScreenPoint(new Vector3(p.pos.x + Mathf.Cos(a) * r, p.pos.y + Mathf.Sin(a) * r, 0f));
                    Box(q.x - S * 0.5f, Screen.height - q.y - S * 0.5f, S, S, new Color(1f, 1f, 1f, 0.12f));
                }
            }
            Vector3 sp = g.cam.cam.WorldToScreenPoint(new Vector3(g.aim.x, g.aim.y, 0f));
            float cx = sp.x, cy = Screen.height - sp.y;
            float gap = (3f + crossKick * 4f + (p.attack != null ? p.attack.spread * 0.15f : 0f)) * S;
            float len = 3f * S, th = Mathf.Max(1f, S * 0.75f);
            Color c = p.ChargeFrac01 >= 1f ? new Color(0.5f, 1f, 1f) : Color.white;
            Color sh = new Color(0f, 0f, 0f, 0.6f);
            for (int pass = 0; pass < 2; pass++)
            {
                float o = pass == 0 ? th : 0f;
                Color cc = pass == 0 ? sh : c;
                Box(cx - gap - len + o, cy - th * 0.5f + o, len, th, cc);
                Box(cx + gap + o, cy - th * 0.5f + o, len, th, cc);
                Box(cx - th * 0.5f + o, cy - gap - len + o, th, len, cc);
                Box(cx - th * 0.5f + o, cy + gap + o, th, len, cc);
            }
            Box(cx - th * 0.5f, cy - th * 0.5f, th, th, c);
            // reload / heat bar under the crosshair, right where you're looking
            if (p.reloading || (p.weapon != null && p.weapon.def.UsesHeat && p.weapon.heat > 0.05f))
            {
                float k = p.reloading ? p.ReloadFrac : p.weapon.heat;
                Color bc = p.reloading ? new Color(1f, 0.9f, 0.5f) : (p.weapon.overheated ? new Color(1f, 0.3f, 0.2f) : Color.Lerp(new Color(0.4f, 0.9f, 1f), new Color(1f, 0.4f, 0.2f), k));
                float bw2 = 14f * S;
                Box(cx - bw2 * 0.5f, cy + gap + len + 3f * S, bw2, 2f * S, new Color(0f, 0f, 0f, 0.6f));
                Box(cx - bw2 * 0.5f, cy + gap + len + 3f * S, bw2 * k, 2f * S, bc);
            }
        }

        // Label above a weapon on the ground: modifiers, stats, name, action.
        public void WeaponLabel(Vector2 at, Weapon w, string action, Player p, bool hovered = false)
        {
            // short: modifiers and curse by name only, one line of description. Hover it for the numbers.
            if (hovered) hoverWeapon = w;
            float y = 0f;
            if (w.curse != Curse.None) { Label(at + Vector2.up * y, Curses.Name(w.curse) + Curses.Skull, Curses.Col(w.curse)); y += LabelGap; }
            if (w.mods.Count > 0)
            {
                string ms = "";
                foreach (var m in w.mods) ms += (ms.Length > 0 ? " + " : "+ ") + Mods.Names[(int)m];
                Label(at + Vector2.up * y, ms, Mods.Ui);
                y += LabelGap;
            }
            Label(at + Vector2.up * y, Short(w.def.desc), new Color(0.85f, 0.85f, 0.85f)); y += LabelGap;
            Label(at + Vector2.up * y, w.def.name + (w.mods.Count >= 3 ? "  ***" : ""), w.Color); y += LabelGap;
            if (!string.IsNullOrEmpty(action)) Label(at + Vector2.up * y, action, Color.white);
        }

        float bossTrail = 1f;

        float bossTrail2 = 1f;

        void DrawBossBar(Boss b, float S, float sm, float pad, float yo = 0f, bool second = false)
        {
            float w = Screen.width * 0.46f, h = 5f * S;
            float x = (Screen.width - w) * 0.5f, y = pad + 26f * S + yo;
            float k = b.HpFrac;
            float trail = second ? bossTrail2 : bossTrail;
            trail = Mathf.Max(k, Mathf.MoveTowards(trail, k, Time.unscaledDeltaTime * 0.35f));
            if (second) bossTrail2 = trail; else bossTrail = trail;
            PixelFont.Draw(Screen.width * 0.5f, y - 9f * sm, b.title, sm, new Color(1f, 0.45f, 0.4f), 1);
            Box(x - 2f * S, y - 2f * S, w + 4f * S, h + 4f * S, Ink(0.95f));
            Box(x, y, w, h, new Color(0.2f, 0.05f, 0.07f));
            Box(x, y, w * trail, h, new Color(1f, 0.9f, 0.75f));
            Box(x, y, w * k, h, Color.Lerp(new Color(0.75f, 0.1f, 0.1f), new Color(0.95f, 0.25f, 0.2f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)));
            Box(x + w * 0.5f - S * 0.5f, y, S, h, new Color(0f, 0f, 0f, 0.5f));   // phase 2 marker
        }
    }
}
