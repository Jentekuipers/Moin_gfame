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
            int cut = r.IndexOfAny(new[] { '.', ';', '(' });
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

        void TipDef(Def d, Player p)
        {
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
            foreach (var c in DB.Combos)
            {
                if (c.IsTransform || System.Array.IndexOf(c.needs, d.id) < 0) continue;
                string s = p.Combo(c.id) ? "* " + c.name + ": " + c.desc : "? COMBO WITH " + ComboPartners(c, d.id, p);
                lines.Add((s, p.Combo(c.id) ? DB.ComboColor : new Color(0.65f, 0.6f, 0.45f)));
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
                lines.Add(((d.Melee ? "MELEE  " : "") + "DMG " + dmg.ToString("0.#", ci) + (perPull > 1 ? " X" + perPull : "") + "   RATE " + rate.ToString("0.0", ci) + "/S   MINE " + d.mine + ammo, TipStat));
            }
            else lines.Add((Armoury.Stats(d), TipStat));
            foreach (var m in w.mods) lines.Add(("+ " + Mods.Names[(int)m] + ": " + Mods.Desc[(int)m], Mods.Ui));
            if (w.curse != Curse.None)
            {
                lines.Add((Curses.Name(w.curse) + ": " + Curses.Desc[(int)w.curse], Curses.Col(w.curse)));
                lines.Add(("LIFTS AFTER " + (Curses.KillsToLift - w.curseKills) + " MORE KILLS", Curses.Col(w.curse)));
            }
            lines.Add((current ? "IN HAND" : "Q / WHEEL TO SWAP TO IT", new Color(0.6f, 0.6f, 0.6f)));
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

            // ----- health bar: grows a little with max HP, a white trail shows the chunk you just lost
            {
                float bw = Mathf.Clamp(60f + p.s.maxHp * 0.12f, 70f, 150f) * S, bh = 9f * S;
                float hx = pad, hy = pad;
                if (hurtShake > 0f) { hx += Random.Range(-2f, 2f) * S * hurtShake; hy += Random.Range(-2f, 2f) * S * hurtShake; }
                float k = Mathf.Clamp01(p.s.hp / (float)Mathf.Max(1, p.s.maxHp));
                hpTrail = Mathf.Max(k, Mathf.MoveTowards(hpTrail, k, Time.unscaledDeltaTime * 0.6f));
                bool low = k <= 0.25f;
                Box(hx - S, hy - S, bw + 2f * S, bh + 2f * S, hpFlashT > 0f ? Color.Lerp(Color.white, new Color(1f, 0.5f, 0.5f), Mathf.Abs(Mathf.Sin(Time.unscaledTime * 10f))) : new Color(0.9f, 0.85f, 0.85f));
                Box(hx, hy, bw, bh, new Color(0.12f, 0.04f, 0.06f));
                Box(hx, hy, bw * hpTrail, bh, new Color(1f, 0.92f, 0.85f));
                Color fill = low ? Color.Lerp(new Color(0.75f, 0.08f, 0.1f), new Color(1f, 0.35f, 0.3f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)) : new Color(0.86f, 0.16f, 0.2f);
                Box(hx, hy, bw * k, bh, fill);
                Box(hx, hy, bw * k, Mathf.Max(1f, S), new Color(1f, 0.5f, 0.5f));   // highlight
                PixelFont.Draw(hx + bw * 0.5f, hy + (bh - 7f * sm) * 0.5f, p.s.hp + " / " + p.s.maxHp, sm, Color.white, 1);
                if (Hover(hx, hy, bw, bh))
                    Tip("HEALTH " + p.s.hp + "/" + p.s.maxHp, new Color(1f, 0.45f, 0.45f), ("ALE, KEGS AND SOME TRINKETS HEAL YOU", TipGrey), ("HEART TRINKETS RAISE YOUR MAX HP", TipGrey));
                if (hpFlashT > 0f)
                    PixelFont.Draw(hx + bw + 4f * S, hy + 1f * S, "+" + hpGain + " MAX HP", sm, new Color(1f, 0.5f, 0.5f, Mathf.Clamp01(hpFlashT)));
                // air: bubbles that pop one by one while you're under water or in steam
                if (p.breath < Player.BreathMax - 0.01f)
                {
                    int bubbles = Mathf.CeilToInt(p.breath / Player.BreathMax * 8f);
                    bool out0 = p.breath <= 0f;
                    for (int i = 0; i < 8; i++)
                    {
                        float bx = hx + bw + 4f * S + i * 6f * S, byb = hy + 1f * S;
                        var bc = i < bubbles ? new Color(0.7f, 0.92f, 1f) : new Color(0.25f, 0.3f, 0.35f, 0.6f);
                        if (out0 && ((int)(Time.unscaledTime * 6f)) % 2 == 0) bc = new Color(1f, 0.4f, 0.4f);
                        var old = GUI.color; GUI.color = bc;
                        GUI.DrawTexture(new Rect(bx, byb, 5f * S, 5f * S), Art.Circle.texture);
                        GUI.color = old;
                    }
                }
            }

            // ----- gold + power
            float y = pad + 14f * S;
            float gp = 1f + goldPop * 0.3f;
            Icon(Art.Icons["coin"], pad, y - goldPop * S, 8f * S * gp, Color.white);
            PixelFont.Draw(pad + 11f * S, y + 0.5f * S - goldPop * 2f * S, p.s.gold.ToString(), S * (goldPop > 0.5f ? 1.25f : 1f), Color.Lerp(new Color(1f, 0.85f, 0.3f), Color.white, goldPop));
            y += 12f * S;
            DrawStats(p, pad, y, S, sm);

            // ----- depth
            PixelFont.Draw(Screen.width * 0.5f, pad, "DEPTH: -" + p.Depth, S, new Color(0.85f, 0.7f, 0.35f), 1);
            var bio = Biomes.All[Biomes.Index(p.Depth)];
            PixelFont.Draw(Screen.width * 0.5f, pad + 10f * S, bio.name, sm, (Color)bio.rim, 1);

            // ----- best
            PixelFont.Draw(Screen.width - pad, pad, "BEST -" + g.best, sm, new Color(0.7f, 0.7f, 0.75f), 2);

            // ----- trinkets: little icons stacking up on the right, Isaac-style
            DrawTrinkets(p, S, sm, pad);

            // ----- weapon panel (bottom left): current weapon big, the other one small, modifiers listed
            float bs = 18f * S;
            float by = Screen.height - pad - bs - 9f * S;
            DrawWeapons(p, pad, by, bs, S, sm);
            float sx = pad + bs * 1.6f + 4f * S + bs * 0.9f + 6f * S;
            Slot(sx, by, bs, S, sm, p.special != null ? p.special.icon : null, "SHIFT", null, p.charges > 0 ? 1f : 0.35f, new Color(0.45f, 0.85f, 1f));
            if (spFlash > 0f) Box(sx - S, by - S, bs + 2f * S, bs + 2f * S, new Color(0.6f, 0.9f, 1f, spFlash * 0.7f));   // flashes when a charge comes back
            if (p.special != null && Hover(sx, by, bs + 6f * S, bs))
            {
                TipDef(p.special, p);
                tipLines.Add(("CHARGES: " + p.charges + "/" + p.MaxCharges + "   RECHARGES OVER TIME, KILLS SPEED IT UP", TipStat));
            }
            float barX = sx + bs + S;
            if (p.special != null) ChargeBar(p, barX, by, 4f * S, bs, S, sm, sx, bs);
            float ix = barX + 4f * S + 4f * S;
            ItemStack st = p.items.Count > 0 ? p.items[Mathf.Clamp(p.sel, 0, p.items.Count - 1)] : null;
            Slot(ix, by, bs, S, sm, st != null ? st.def.icon : null, "C", st != null ? "x" + st.count : null, 1f, new Color(0.55f, 1f, 0.55f));
            if (st != null && Hover(ix, by, bs, bs))
            {
                TipDef(st.def, p);
                tipLines.Add(("CARRYING " + st.count + "   C TO USE, V TO CYCLE", TipStat));
            }
            PixelFont.Draw(ix + bs + 3f * S, by + bs - 7f * sm, p.ItemTotal + "/" + p.s.itemCap + (p.items.Count > 1 ? "  V:SWAP" : ""), sm, new Color(0.7f, 0.7f, 0.7f));
            string names = (p.special != null ? p.special.name : "") + (st != null ? "  /  " + st.def.name : "");
            PixelFont.Draw(sx, by - 12f * S, names, sm, new Color(0.45f, 0.85f, 1f));
            if (p.CurrentBuff != Player.Buff.None)
            {
                string b = p.CurrentBuff == Player.Buff.Speed ? "HUNTED: +SPEED" : p.CurrentBuff == Player.Buff.Damage ? "HUNTED: +DAMAGE" : "HUNTED: +FIRE RATE";
                PixelFont.Draw(Screen.width * 0.5f, Screen.height - pad - 30f * S, b + " " + p.BuffTime.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), S, Mods.Col(Mod.Hunter), 1);
            }

            if (g.boss != null && !g.boss.dead) DrawBossBar(g.boss, S, sm, pad);

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
            else if (biomeT > 0f)
            {
                float a = Mathf.Clamp01(biomeT);
                var c = (Color)bio.rim; c.a = a;
                PixelFont.Draw(Screen.width * 0.5f, Screen.height * 0.22f, bio.name, S * 2f, c, 1);
                PixelFont.Draw(Screen.width * 0.5f, Screen.height * 0.22f + 18f * S, "IT GETS NASTIER DOWN HERE", S, new Color(1f, 1f, 1f, a), 1);
            }

            // ----- controls help
            if (helpT > 0f && g.state == Game.State.Playing)
            {
                var c = new Color(1f, 1f, 1f, Mathf.Clamp01(helpT));
                string h1 = "A/D MOVE   SPACE JUMP   MOUSE AIM   LEFT CLICK SHOOT", h2 = "SHIFT SPECIAL   R RELOAD   C ITEM   V CYCLE   Q SWAP";
                PixelFont.Draw(Screen.width * 0.5f, Screen.height - pad - 30f * sm, h1, FitSize(h1, sm, Screen.width * 0.9f), c, 1);
                PixelFont.Draw(Screen.width * 0.5f, Screen.height - pad - 20f * sm, h2, FitSize(h2, sm, Screen.width * 0.9f), c, 1);
                PixelFont.Draw(Screen.width * 0.5f, Screen.height - pad - 10f * sm, "E BUY / TAKE   ESC PAUSE", sm, c, 1);
            }

            if (g.state == Game.State.Playing && p.mode != Player.Mode.Dead)
            {
                DrawCrosshair(g, p, S);
                if (tipTitle == null && hoverWeapon != null) TipWeapon(hoverWeapon, false);
                else if (tipTitle == null && hoverDef != null) TipDef(hoverDef, p);
                DrawTooltip(S, sm);
            }
            hoverDef = null; hoverWeapon = null;
            if (whiteFlash > 0f) Box(0, 0, Screen.width, Screen.height, new Color(1f, 1f, 1f, Mathf.Clamp01(whiteFlash)));

            // ----- pause / game over
            if (g.state != Game.State.Paused) { QuitHovered = false; OptionsHovered = false; }
            if (g.state != Game.State.Dead) ChangeHovered = false;
            if (g.state == Game.State.Paused) DrawPause(p, S, sm, pad);
            else if (g.state == Game.State.Dead)
            {
                float a = Mathf.Clamp01(g.deadT * 1.5f);
                Box(0, 0, Screen.width, Screen.height, new Color(0f, 0f, 0f, 0.6f * a));
                float cy = Screen.height * 0.3f;
                PixelFont.Draw(Screen.width * 0.5f, cy, "YOU DIED", S * 3f, new Color(1f, 0.3f, 0.3f, a), 1);
                PixelFont.Draw(Screen.width * 0.5f, cy + 30f * S, "DEPTH REACHED: -" + g.maxDepth, S, new Color(0.9f, 0.8f, 0.5f, a), 1);
                PixelFont.Draw(Screen.width * 0.5f, cy + 41f * S, "GOLD: " + p.s.gold + "    KILLS: " + p.s.kills, S, new Color(1f, 0.85f, 0.3f, a), 1);
                PixelFont.Draw(Screen.width * 0.5f, cy + 52f * S, "TRINKETS: " + p.skillOrder.Count + "    COMBOS: " + p.comboOrder.Count, S, new Color(0.75f, 0.55f, 1f, a), 1);
                PixelFont.Draw(Screen.width * 0.5f, cy + 63f * S, "BEST: -" + g.best + (g.newBest ? "  NEW RECORD!" : ""), S, new Color(0.8f, 0.8f, 0.9f, a), 1);
                if (g.deadT > 1f && ((int)(Time.unscaledTime * 2f)) % 2 == 0)
                    PixelFont.Draw(Screen.width * 0.5f, cy + 81f * S, "SPACE OR CLICK: DIG AGAIN", S, Color.white, 1);
                if (g.deadT > 1f)
                {
                    string ch = "E: CHANGE DIGGER";
                    float cbw = PixelFont.Width(ch, sm) + 10f * sm, cbx = Screen.width * 0.5f - cbw * 0.5f, cby = cy + 93f * S - 2f * sm;
                    ChangeHovered = new Rect(cbx, cby, cbw, 11f * sm).Contains(Event.current.mousePosition);
                    Box(cbx, cby, cbw, 11f * sm, ChangeHovered ? new Color(1f, 0.82f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.12f));
                    PixelFont.Draw(Screen.width * 0.5f, cy + 93f * S, ch, sm, ChangeHovered ? Color.black : Color.white, 1);
                }
            }
        }

        void DrawTrinkets(Player p, float S, float sm, float pad)
        {
            const int cols = 6;
            float sz = 10f * S, gap = 1f * S;
            float top = pad + 10f * S;
            float right = Screen.width - pad;
            int n = p.skillOrder.Count;
            for (int i = 0; i < n; i++)
            {
                var k = p.skillOrder[i];
                int col = i % cols, row = i / cols;
                float x = right - cols * (sz + gap) + gap + col * (sz + gap);   // oldest top-left, newest bottom-right
                float y = top + row * (sz + gap);
                Box(x - S * 0.5f, y - S * 0.5f, sz + S, sz + S, new Color(0f, 0f, 0f, 0.45f));
                Icon(k.icon, x, y, sz, Color.white);
                int cnt = p.SkillCount(k.id);
                if (Hover(x, y, sz, sz)) { TipDef(k, p); if (cnt > 1) tipLines.Insert(0, ("x" + cnt, Color.white)); }
                if (cnt > 1) PixelFont.Draw(x + sz, y + sz - 6f * sm, cnt.ToString(), sm, Color.white, 2);
            }
            int rows = (n + cols - 1) / cols;
            float cy = top + rows * (sz + gap) + (n > 0 ? 3f * S : 0f);
            foreach (var c in p.comboOrder)
            {
                PixelFont.Draw(right, cy, "* " + c.name, sm, DB.ColorOfCombo(c), 2);
                float cw = PixelFont.Width("* " + c.name, sm);
                if (Hover(right - cw, cy, cw, 8f * sm))
                {
                    string parts = "";
                    if (c.IsTransform) { foreach (var k in p.skillOrder) if (k.tags != null && System.Array.IndexOf(k.tags, c.tag) >= 0) parts += (parts.Length > 0 ? " + " : "") + k.name; }
                    else foreach (var need in c.needs) parts += (parts.Length > 0 ? " + " : "") + DB.Name(need);
                    Tip((c.IsTransform ? "TRANSFORMATION: " : "COMBO: ") + c.name, DB.ColorOfCombo(c), (c.desc, TipGrey), (parts, new Color(0.7f, 0.7f, 0.7f)));
                }
                cy += 9f * sm;
            }
        }

        int pausePage, pausePages = 1;
        public void ResetPausePage() => pausePage = 0;
        public void PageTurn(int d) => pausePage = Mathf.Clamp(pausePage + d, 0, Mathf.Max(0, pausePages - 1));

        // Pause screen: everything you carry as icons (hover for details, same as in-game), combos on the right.
        // Pages when it doesn't all fit.
        public bool QuitHovered { get; private set; }
        public bool ChangeHovered { get; private set; }   // the death screen's CHANGE DIGGER button
        public bool OptionsHovered { get; private set; }

        void DrawPause(Player p, float S, float sm, float pad)
        {
            tipTitle = null;
            Box(0, 0, Screen.width, Screen.height, new Color(0f, 0f, 0f, 0.82f));
            PixelFont.Draw(Screen.width * 0.5f, pad * 2f, "PAUSED", S * 3f, Color.white, 1);

            // options button, top left
            {
                string label = "OPTIONS (E)";
                float bw = PixelFont.Width(label, sm) + 10f * sm, bh = 11f * sm;
                float bx = pad * 2f, by = pad * 2f;
                OptionsHovered = new Rect(bx, by, bw, bh).Contains(Event.current.mousePosition);
                Box(bx, by, bw, bh, OptionsHovered ? new Color(1f, 0.82f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.12f));
                PixelFont.Draw(bx + bw * 0.5f, by + 2f * sm, label, sm, OptionsHovered ? Color.black : Color.white, 1);
            }
            // save & quit button, top right
            {
                string label = "SAVE & QUIT (Q)";
                float bw = PixelFont.Width(label, sm) + 10f * sm, bh = 11f * sm;
                float bx = Screen.width - pad * 2f - bw, by = pad * 2f;
                QuitHovered = new Rect(bx, by, bw, bh).Contains(Event.current.mousePosition);
                Box(bx, by, bw, bh, QuitHovered ? new Color(1f, 0.82f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.12f));
                PixelFont.Draw(bx + bw * 0.5f, by + 2f * sm, label, sm, QuitHovered ? Color.black : Color.white, 1);
            }
            PixelFont.Draw(Screen.width * 0.5f, pad * 2f + 26f * S, "ESC TO RESUME    HOVER FOR DETAILS", sm, new Color(0.8f, 0.8f, 0.8f), 1);

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
                for (int m = 0; m < w.mods.Count; m++) Box(gx + 1f * S + m * 3f * S, y + isz - 2f * S, 2f * S, 2f * S, Mods.Ui);
                if (Hover(gx, y, isz * 1.5f, isz)) TipWeapon(w, i == p.cur);
                gx += isz * 1.5f + 3f * S;
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
            y += isz + 8f * S;

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
                Icon(k.icon, cx + 1f * S, cy + 1f * S, isz - 2f * S, Color.white);
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
                    string parts = "";
                    if (c.needs != null) foreach (var nd in c.needs) parts += (parts.Length > 0 ? " + " : "") + DB.Name(nd);
                    Tip((c.IsTransform ? "TRANSFORMATION: " : "COMBO: ") + c.name, DB.ColorOfCombo(c), (c.desc, TipGrey), (parts, new Color(0.7f, 0.7f, 0.7f)));
                }
                ry += line * 1.15f;
            }
            if (pausePage == cPages - 1)
            {
                ry += line * 0.5f;
                int hints = 0;
                foreach (var c in DB.Combos)
                {
                    if (p.Combo(c.id) || c.IsTransform) continue;
                    int have = 0;
                    foreach (var need in c.needs) if (p.Owns(need)) have++;
                    if (have == 0 || have == c.needs.Length) continue;
                    if (ry > bottom - line) break;
                    if (hints == 0) { PixelFont.Draw(rx, ry, "HALF-FINISHED COMBOS", sm, new Color(0.6f, 0.6f, 0.6f)); ry += line; }
                    string str = "? ";
                    for (int i = 0; i < c.needs.Length; i++)
                        str += (i > 0 ? " + " : "") + (p.Owns(c.needs[i]) ? DB.Name(c.needs[i]) : "???");
                    PixelFont.Draw(rx, ry, str, FitSize(str, sm, colW), new Color(0.65f, 0.6f, 0.45f));
                    ry += line;
                    hints++;
                }
            }

            if (pausePages > 1)
                PixelFont.Draw(Screen.width * 0.5f, Screen.height - pad - 8f * S, "< A   PAGE " + (pausePage + 1) + " / " + pausePages + "   D >", S, Color.white, 1);
            DrawTooltip(S, sm);
        }

        // ================================================================== Isaac-style stat column

        static readonly string[] StatNames = { "SPD", "RATE", "DMG", "MINE", "JUMP", "LUCK" };
        static readonly string[] StatLong = { "SPEED", "FIRE RATE", "DAMAGE", "MINING", "JUMP", "LUCK" };
        static readonly string[] StatHelp =
        {
            "HOW FAST YOU RUN",
            "ATTACKS PER SECOND WITH THE WEAPON IN HAND",
            "DAMAGE PER TRIGGER PULL WITH THE WEAPON IN HAND",
            "HOW HARD YOUR SHOTS AND SWINGS HIT ROCK",
            "HOW HIGH YOU JUMP",
            "CRITS, LUCKY DROPS AND BETTER LOOT",
        };
        static Sprite[] statIcons;
        readonly float[] lastStat = new float[6], statDelta = new float[6], statT = new float[6];
        bool statInit;

        static float[] StatValues(Player p) => new[] { p.Speed, p.SwingsPerSec, p.WeaponDamage, (float)p.MinePower, p.JumpMul, p.Luck };

        static string Fmt(int i, float v)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (i == 3) return ((int)v).ToString(ci);
            if (i == 1 || i == 5) return v.ToString("0.0", ci);
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
            for (int i = 0; i < 6; i++)
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
                    Art.Icons["pickaxe"], Art.Trinket("wing", U.C(200, 230, 240)), Art.Trinket("coin", U.C(170, 230, 70)),
                };
            var v = StatValues(p);
            float row = 9f * sm;
            for (int i = 0; i < 6; i++)
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
                    PixelFont.Draw(x + 10f * sm + PixelFont.Width(val, sm) + 4f * sm, y + i * row, (d > 0f ? "+" : "") + Fmt(i == 3 ? 3 : 0, d), sm, c);
                }
            }
        }

        // Isaac-style active item charge bar: one segment per charge step, fills from the bottom.
        static void ChargeBar(Player p, float x, float y, float w, float h, float S, float sm, float slotX, float slotW)
        {
            var sp = p.special;
            int segs = Mathf.Max(1, sp.segs);
            bool full = p.charges >= p.MaxCharges;
            float frac = p.ChargeFrac;
            Box(x, y, w, h, new Color(0f, 0f, 0f, 0.75f));
            float inner = h - 2f * S;
            float fillH = inner * frac;
            Color fc = full ? Color.Lerp(new Color(1f, 1f, 0.6f), Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f))
                            : p.charges > 0 ? new Color(0.55f, 0.95f, 0.45f) : new Color(0.45f, 0.85f, 1f);
            Box(x + S, y + S + inner - fillH, w - 2f * S, fillH, fc);
            if (segs > 1)
                for (int i = 1; i < segs; i++)
                    Box(x + S, y + S + inner * i / segs - Mathf.Max(1f, S * 0.5f) * 0.5f, w - 2f * S, Mathf.Max(1f, S * 0.5f), new Color(0f, 0f, 0f, 0.9f));
            Box(x, y, w, S, Color.white); Box(x, y + h - S, w, S, Color.white);
            Box(x, y, S, h, Color.white); Box(x + w - S, y, S, h, Color.white);

            // stored uses as pips above the slot
            int max = p.MaxCharges;
            float pw = Mathf.Min(4f * S, (slotW + w + S) / Mathf.Max(1, max) - S);
            for (int i = 0; i < max; i++)
                Box(slotX + i * (pw + S), y - 4f * S, pw, 2f * S, i < p.charges ? new Color(0.55f, 0.95f, 0.45f) : new Color(0.2f, 0.25f, 0.3f));
        }

        // Stacked label above a pickup / shop item: action, combo hint, name, description, stats or charge info.
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
                y += 0.4f;
            }
            string line = !string.IsNullOrEmpty(d.desc) ? d.desc : DB.Extra(d);
            if (!string.IsNullOrEmpty(line)) { Label(at + Vector2.up * y, Short(line), new Color(0.85f, 0.85f, 0.85f)); y += 0.4f; }
            Label(at + Vector2.up * y, d.name + (DB.IsComboPiece(d, p) ? " *" : ""), DB.ColorOf(d)); y += 0.4f;
            if (action != null) Label(at + Vector2.up * y, action, Color.white);
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
            float bandH = 74f * S;
            Box(0, 0, W, bandH, new Color(0f, 0f, 0f, 0.78f));
            Box(0, bandH, W, S, new Color(gold.r, gold.g, gold.b, 0.6f));
            PixelFont.Draw(W * 0.5f, 6f * S, c.name + "  THE " + c.animal, S * 2f, gold, 1);
            PixelFont.Draw(W * 0.5f, 24f * S, Short(c.title, 50), sm, Color.white, 1);
            if (!string.IsNullOrEmpty(c.blurb)) PixelFont.Draw(W * 0.5f, 33f * S, Short(c.blurb, 64), FitSize(Short(c.blurb, 64), sm, W * 0.9f), new Color(0.85f, 0.85f, 0.9f), 1);

            // loadout cards: icon and name, hover for the details
            var cards = new List<(Def d, string label, Color col)>();
            if (DB.ById.TryGetValue(c.weapon, out var wd)) cards.Add((wd, "WEAPON", DB.KindColor(Kind.Attack)));
            if (DB.ById.TryGetValue(c.special, out var spd)) cards.Add((spd, "SHIFT", DB.KindColor(Kind.Special)));
            if (c.skills != null)
                for (int i = 0; i < c.skills.Length; i++)
                    if (DB.ById.TryGetValue(c.skills[i], out var sd)) cards.Add((sd, i == 0 ? "OWN SKILL" : "SKILL", i == 0 ? new Color(1f, 0.8f, 0.4f) : DB.KindColor(Kind.Skill)));
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

            string hint = "A / D OR CLICK: CHOOSE     CLICK AGAIN OR SPACE: START     ESC: BACK";
            PixelFont.Draw(W * 0.5f, H - pad - 10f * S, hint, FitSize(hint, sm, W * 0.94f), Color.white, 1);
            DrawTooltip(S, sm);
        }

        // The weapon panel. It jolts on every shot.
        void DrawWeapons(Player p, float x, float y, float bs, float S, float sm)
        {
            var w = p.weapon;
            float k = wKick;
            float big = bs * 1.6f;
            float ox = -k * 2f * S, oy = k * 1.5f * S;
            Color border = w != null ? w.Color : Color.grey;
            Box(x + ox, y + oy, big, bs, new Color(0f, 0f, 0f, 0.65f));
            if (w != null && Hover(x, y, big, bs)) TipWeapon(w, true);
            Box(x + ox, y + oy, big, S, border); Box(x + ox, y + oy + bs - S, big, S, border);
            Box(x + ox, y + oy, S, bs, border); Box(x + ox + big - S, y + oy, S, bs, border);
            if (w != null)
            {
                float isz = bs - 4f * S + k * 3f * S;
                Icon(w.def.icon, x + ox + (big - isz) * 0.5f, y + oy + (bs - isz) * 0.5f, isz, Color.white);
                if (p.ChargeFrac01 > 0f) Box(x + ox + S, y + oy + bs - 3f * S, (big - 2f * S) * p.ChargeFrac01, 2f * S, w.def.col);
                // ammo / heat
                if (w.def.UsesHeat)
                {
                    var hc = w.overheated ? (((int)(Time.unscaledTime * 8f)) % 2 == 0 ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.8f, 0.3f))
                                          : Color.Lerp(new Color(0.4f, 0.9f, 1f), new Color(1f, 0.4f, 0.2f), w.heat);
                    Box(x + ox + S, y + oy + bs - 3f * S, (big - 2f * S) * w.heat, 2f * S, hc);
                    if (w.overheated) PixelFont.Draw(x + ox + big * 0.5f, y + oy + 2f * S, "HOT", sm, hc, 1);
                }
                else if (p.MagSize(w) > 0)
                {
                    int mag = p.MagSize(w), am = Mathf.Max(0, w.ammo);
                    bool lowA = am <= Mathf.Max(1, mag / 4);
                    var ac = lowA ? (((int)(Time.unscaledTime * 6f)) % 2 == 0 ? new Color(1f, 0.35f, 0.3f) : Color.white) : Color.white;
                    if (p.reloading)
                    {
                        Box(x + ox + S, y + oy + bs - 3f * S, (big - 2f * S) * p.ReloadFrac, 2f * S, new Color(1f, 0.9f, 0.5f));
                        PixelFont.Draw(x + ox + big * 0.5f, y + oy + 2f * S, "RELOAD", sm, new Color(1f, 0.9f, 0.5f), 1);
                    }
                    PixelFont.Draw(x + ox + big - 2f * S, y + oy + bs - 9f * sm, am + "/" + mag, sm, ac, 2);
                }
                float ty = y - 12f * S;
                PixelFont.Draw(x, ty - (w.mods.Count > 0 ? 9f * sm : 0f), w.def.name, sm, w.Color);
                if (w.curse != Curse.None)
                {
                    // the curse rides above the weapon's name: its lantern, its name and the kills still needed
                    float cy = ty - (w.mods.Count > 0 ? 9f * sm : 0f) - 11f * sm;
                    var cc = Curses.Col(w.curse);
                    float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
                    Icon(Curses.Icon(w.curse), x, cy - 2f * sm, 10f * sm, Color.white);
                    string cn = Curses.Name(w.curse) + "  " + w.curseKills + "/" + Curses.KillsToLift;
                    PixelFont.Draw(x + 12f * sm, cy, cn, sm, new Color(cc.r, cc.g, cc.b, pulse));
                    if (Hover(x, cy - 2f * sm, 12f * sm + PixelFont.Width(cn, sm), 10f * sm))
                        Tip(Curses.Name(w.curse), cc, (Curses.Desc[(int)w.curse], TipGrey), ("KILL " + (Curses.KillsToLift - w.curseKills) + " MORE FOES TO LIFT IT", TipStat));
                }
                if (w.mods.Count > 0)
                {
                    float mx = x;
                    foreach (var m in w.mods)
                    {
                        Icon(Mods.Icon(m), mx, ty - sm, 8f * sm, Color.white);
                        string n = Mods.Names[(int)m];
                        if (Hover(mx, ty - sm, 9f * sm + PixelFont.Width(n, sm), 9f * sm)) Tip(n, Mods.Signature, (Mods.Desc[(int)m], TipGrey));
                        Box(mx - sm, ty - 2f * sm, 11f * sm + PixelFont.Width(n, sm), 11f * sm, new Color(Mods.Signature.r, Mods.Signature.g, Mods.Signature.b, 0.28f));   // a pink pill behind each modifier
                        PixelFont.Draw(mx + 9f * sm, ty, n, sm, Mods.Ui);
                        mx += 9f * sm + PixelFont.Width(n, sm) + 5f * sm;
                    }
                }
            }
            PixelFont.Draw(x + big * 0.5f, y + bs + 2f * S, "CLICK", sm, new Color(0.8f, 0.8f, 0.8f), 1);
            // the other slot, smaller
            var o = p.slots[1 - p.cur];
            float sx = x + big + 3f * S, ss = bs * 0.75f;
            var other = p.slots[1 - p.cur];
            if (other != null && Hover(sx, y + bs - ss, ss * 1.2f, ss)) TipWeapon(other, false);
            Box(sx, y + bs - ss, ss * 1.2f, ss, new Color(0f, 0f, 0f, 0.5f));
            if (o != null) Icon(o.def.icon, sx + 2f * S, y + bs - ss + 2f * S, ss - 4f * S, new Color(1f, 1f, 1f, 0.6f));
            PixelFont.Draw(sx + ss * 0.6f, y + bs + 2f * S, "Q", sm, new Color(0.6f, 0.6f, 0.6f), 1);
        }

        void DrawCrosshair(Game g, Player p, float S)
        {
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
            if (w.curse != Curse.None) { Label(at + Vector2.up * y, Curses.Name(w.curse), Curses.Col(w.curse)); y += 0.4f; }
            if (w.mods.Count > 0)
            {
                string ms = "";
                foreach (var m in w.mods) ms += (ms.Length > 0 ? " + " : "+ ") + Mods.Names[(int)m];
                Label(at + Vector2.up * y, ms, Mods.Ui);
                y += 0.4f;
            }
            Label(at + Vector2.up * y, Short(w.def.desc), new Color(0.85f, 0.85f, 0.85f)); y += 0.4f;
            Label(at + Vector2.up * y, w.def.name + (w.mods.Count >= 3 ? "  ***" : ""), w.Color); y += 0.4f;
            if (action != null) Label(at + Vector2.up * y, action, Color.white);
        }

        float bossTrail = 1f;

        void DrawBossBar(Boss b, float S, float sm, float pad)
        {
            float w = Screen.width * 0.46f, h = 5f * S;
            float x = (Screen.width - w) * 0.5f, y = pad + 26f * S;
            float k = b.HpFrac;
            bossTrail = Mathf.Max(k, Mathf.MoveTowards(bossTrail, k, Time.unscaledDeltaTime * 0.35f));
            PixelFont.Draw(Screen.width * 0.5f, y - 9f * sm, b.title, sm, new Color(1f, 0.45f, 0.4f), 1);
            Box(x - S, y - S, w + 2f * S, h + 2f * S, Color.white);
            Box(x, y, w, h, new Color(0.08f, 0.02f, 0.03f));
            Box(x, y, w * bossTrail, h, new Color(1f, 0.9f, 0.75f));
            Box(x, y, w * k, h, Color.Lerp(new Color(0.75f, 0.1f, 0.1f), new Color(0.95f, 0.25f, 0.2f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)));
            Box(x + w * 0.5f - S * 0.5f, y, S, h, new Color(0f, 0f, 0f, 0.5f));   // phase 2 marker
        }

        static void Slot(float x, float y, float bs, float S, float sm, Sprite icon, string key, string extra, float alpha, Color border)
        {
            Box(x, y, bs, bs, new Color(0f, 0f, 0f, 0.65f));
            Box(x, y, bs, S, border); Box(x, y + bs - S, bs, S, border);
            Box(x, y, S, bs, border); Box(x + bs - S, y, S, bs, border);
            if (icon != null) Icon(icon, x + 3f * S, y + 3f * S, bs - 6f * S, new Color(1f, 1f, 1f, alpha));
            PixelFont.Draw(x + bs * 0.5f, y + bs + 2f * S, key, sm, new Color(0.8f, 0.8f, 0.8f), 1);
            if (extra != null) PixelFont.Draw(x + bs - S, y + bs - 8f * sm, extra, sm, Color.white, 2);
        }
    }
}
