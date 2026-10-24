using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // TEST MODE (title screen): a normal run plus a panel (F1, or VIEW on a pad) to grab any weapon, modifier,
    // trinket or item, fight any boss, and switch on god mode and damage numbers. A training dummy shows your DPS.
    public static class TestMode
    {
        public static bool On, Open, God, Numbers = true;
        public static bool Setup;
        public static bool PretendAllDiamonds;   // the test god route: as if every digger had the God-Diamond   // the set-up screen before a test run: ESC goes back to the menu, nothing kept
        static int tab, scroll;
        static float openedAt;
        static string note; static float noteT;

        // what you picked, in order, so a new test run after dying starts with exactly the same things
        // (0: get a thing by id, 1: toggle a modifier on the weapon in hand, 2: +1 heart, 3: +1000 gold)
        static readonly List<(int op, string id, int mod)> picks = new List<(int, string, int)>();

        public static void ClearPicks() { picks.Clear(); }

        public static void OpenPanel(bool setup)
        {
            Open = true; Setup = setup;
            openedAt = Time.unscaledTime;   // the click that opened it doesn't count as a click inside it
        }

        // re-apply every pick, quietly, at the start of a new test run
        public static void Replay(Player p)
        {
            foreach (var (op, id, mod) in picks)
            {
                switch (op)
                {
                    case 0: if (DB.ById.TryGetValue(id, out var d)) p.Grant(d); break;
                    case 1: ToggleMod(p, (Mod)mod, false); break;
                    case 2: p.s.maxHp += Stats.Heart; p.s.hp += Stats.Heart; break;
                    case 3: p.AddGold(1000); break;
                    case 4: p.voidShards = 3; break;
                }
            }
        }

        // a modifier on the weapon in hand: on if it's off, off if it's on (3 at most)
        static bool ToggleMod(Player p, Mod m, bool loud)
        {
            var w = p.weapon;
            if (w == null) { if (loud) Note("NO WEAPON IN HAND"); return false; }
            if (w.Has(m)) { w.mods.Remove(m); return true; }
            if (w.mods.Count >= Mods.Max) { if (loud) Note("MAX 3 MODIFIERS: CLICK ONE TO TAKE IT OFF"); return false; }
            if (!Mods.Fits(w.def, m)) { if (loud) Note("DOESN'T FIT THE " + w.def.name); return false; }
            w.mods.Add(m);
            return true;
        }

        static void Note(string s) { note = s; noteT = 2f; }

        static float FitSize(float size, string s, float maxW)
        {
            while (size > 1f && PixelFont.Width(s, size) > maxW) size -= 1f;
            return size;
        }
        static readonly string[] Tabs = { "WEAPONS", "MODS", "TRINKETS", "ITEMS", "BOSSES", "CHEATS" };
        static string hoverName, hoverDesc;
        static Vector2 lastMouse;

        // floating damage numbers on every hit
        public static void Number(Vector2 at, float d)
        {
            if (Game.I == null) return;
            string s = d >= 10f ? Mathf.RoundToInt(d).ToString() : d.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            Game.I.Popup(at + new Vector2(Random.Range(-0.3f, 0.3f), 0.2f), s, new Color(1f, 0.95f, 0.5f));
        }

        public static void Tick(Game g)
        {
            // the panel pauses the game; the mouse wheel / d-pad scrolls the list, LB/RB (or Q/E) switch tabs
            float sc = GameInput.ScrollY();
            if (sc > 0.01f) scroll = Mathf.Max(0, scroll - 1);
            if (sc < -0.01f) scroll++;
            if (GameInput.Pressed(Btn.Up)) scroll = Mathf.Max(0, scroll - 1);
            if (GameInput.Pressed(Btn.Down)) scroll++;
            if (GameInput.PadPressed(Pad.LB) || GameInput.KeyDown("Q")) { tab = (tab + Tabs.Length - 1) % Tabs.Length; scroll = 0; }
            if (GameInput.PadPressed(Pad.RB) || GameInput.KeyDown("E")) { tab = (tab + 1) % Tabs.Length; scroll = 0; }
        }

        static void Box(float x, float y, float w, float h, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), Art.White);
            GUI.color = old;
        }

        static void Icon(Sprite s, float x, float y, float size)
        {
            if (s == null) return;
            var r = s.textureRect; var t = s.texture;
            float k = size / Mathf.Max(r.width, r.height);
            GUI.DrawTextureWithTexCoords(new Rect(x + (size - r.width * k) * 0.5f, y + (size - r.height * k) * 0.5f, r.width * k, r.height * k), t,
                new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height));
        }

        public static void Draw(Game g)
        {
            if (Event.current.type != EventType.Repaint) return;
            float W = Screen.width, H = Screen.height;
            float S = Mathf.Max(2f, Mathf.Floor(H / 270f * Settings.UiScale));
            float sm = Mathf.Max(1f, S - 1f);
            if (!Open)
            {
                PixelFont.Draw(4f * S, H * 0.5f, "TEST MODE: F1 / VIEW", sm, new Color(0.6f, 1f, 0.6f));
                return;
            }
            var mouse = Event.current.mousePosition;
            bool click = GameInput.MouseClicked() && Time.unscaledTime - openedAt > 0.3f;
            noteT -= Time.unscaledDeltaTime;
            var p = g.player;
            float pw = Mathf.Min(W - 16f * S, 380f * S), ph = Mathf.Min(H - 16f * S, 240f * S);
            float px = (W - pw) * 0.5f, py = (H - ph) * 0.5f;
            Box(px, py, pw, ph, new Color(0.04f, 0.04f, 0.08f, 0.94f));
            Box(px, py, pw, S, new Color(0.5f, 1f, 0.5f));
            PixelFont.Draw(px + pw * 0.5f, py + 4f * S, Setup ? "TEST RUN SET-UP" : "TEST PANEL", S * 1.5f, new Color(0.6f, 1f, 0.6f), 1);
            PixelFont.Draw(px + pw - 4f * S, py + 5f * S, Setup ? "ESC: BACK TO MENU" : "F1 / ESC CLOSE", sm, new Color(0.7f, 0.7f, 0.75f), 2);
            if (Setup) PixelFont.Draw(px + 4f * S, py + 5f * S, "START IT FROM BOSSES", sm, new Color(0.7f, 0.7f, 0.75f));

            // tabs
            float tw = (pw - 8f * S) / Tabs.Length, ty = py + 18f * S;
            for (int i = 0; i < Tabs.Length; i++)
            {
                var r = new Rect(px + 4f * S + i * tw, ty, tw - 2f * S, 11f * S);
                bool hov = r.Contains(mouse);
                if (hov && click) { tab = i; scroll = 0; }
                Box(r.x, r.y, r.width, r.height, tab == i ? new Color(0.5f, 1f, 0.5f, 0.85f) : new Color(1f, 1f, 1f, hov ? 0.15f : 0.06f));
                PixelFont.Draw(r.center.x, ty + 2.5f * S, Tabs[i], sm, tab == i ? Color.black : Color.white, 1);
            }

            float top = ty + 15f * S, bottom = py + ph - 20f * S;
            hoverName = null; hoverDesc = null;
            if (tab <= 3)
            {
                // a grid of everything in this category; click to get it
                var list = new List<Def>();
                if (tab == 0) foreach (var d in DB.Attacks) list.Add(d);
                if (tab == 1) foreach (var d in DB.ModDefs) list.Add(d);
                if (tab == 2) foreach (var d in DB.Trinkets) list.Add(d);
                if (tab == 3) { foreach (var d in DB.Items) list.Add(d); foreach (var d in DB.Upgrades) list.Add(d); }
                float cell = 22f * S;
                int cols = Mathf.Max(1, Mathf.FloorToInt((pw - 8f * S) / cell));
                int rows = Mathf.Max(1, Mathf.FloorToInt((bottom - top) / cell));
                int maxScroll = Mathf.Max(0, (list.Count + cols - 1) / cols - rows);
                scroll = Mathf.Clamp(scroll, 0, maxScroll);
                for (int i = scroll * cols; i < list.Count && i < (scroll + rows) * cols; i++)
                {
                    var d = list[i];
                    int li = i - scroll * cols;
                    float cx = px + 4f * S + (li % cols) * cell, cy = top + (li / cols) * cell;
                    var r = new Rect(cx, cy, cell - 2f * S, cell - 2f * S);
                    bool hov = r.Contains(mouse);
                    Color rc = DB.ColorOf(d);
                    // what you've got is lit up: modifiers on the weapon in hand, weapons you carry, trinkets you have
                    int have = 0;
                    if (d is ModDef md0) have = p.weapon != null && p.weapon.Has(md0.mod) ? 1 : 0;
                    else if (d is AttackDef) { foreach (var sl in p.slots) if (sl != null && sl.def == d) have = 1; }
                    else if (d is SkillDef) have = p.SkillCount(d.id);
                    Box(r.x, r.y, r.width, r.height, new Color(rc.r, rc.g, rc.b, have > 0 ? 0.6f : hov ? 0.35f : 0.12f));
                    if (have > 0)
                    {
                        Color hc = d is ModDef ? Mods.Signature : Color.white;
                        Box(r.x, r.y, r.width, S, hc); Box(r.x, r.y + r.height - S, r.width, S, hc);
                        Box(r.x, r.y, S, r.height, hc); Box(r.x + r.width - S, r.y, S, r.height, hc);
                        if (have > 1) PixelFont.Draw(r.x + r.width - 2f * S, r.y + r.height - 8f * sm, "x" + have, sm, Color.white, 2);
                    }
                    Icon(d.icon, r.x + 2f * S, r.y + 2f * S, r.width - 4f * S);
                    if (hov)
                    {
                        hoverName = d.name;
                        string extra = DB.Extra(d);
                        hoverDesc = d.desc + (string.IsNullOrEmpty(extra) ? "" : "   " + extra);
                        if (click)
                        {
                            if (d is ModDef md)
                            {
                                if (ToggleMod(p, md.mod, true)) { picks.Add((1, null, (int)md.mod)); g.sfx.Play("buy", 0.6f, p.weapon.Has(md.mod) ? 1f : 0.7f); }
                                else g.sfx.Play("deny", 0.5f);
                            }
                            else if (p.Grant(d)) { picks.Add((0, d.id, 0)); g.sfx.Play("buy", 0.6f); g.Popup(p.pos + Vector2.up, d.name, rc); }
                            else g.sfx.Play("deny", 0.5f);
                        }
                    }
                }
                if (maxScroll > 0) PixelFont.Draw(px + pw - 4f * S, bottom + 2f * S, "WHEEL / D-PAD: " + (scroll + 1) + "/" + (maxScroll + 1), sm, new Color(0.7f, 0.7f, 0.75f), 2);
            }
            else if (tab == 4)
            {
                // fight any boss: you're dropped into the next unused arena
                for (int i = 0; i < BossArena.BossKinds.Length; i++)
                {
                    float bx = px + 8f * S + (i % 2) * (pw * 0.5f - 6f * S), by = top + (i / 2) * 14f * S;
                    var r = new Rect(bx, by, pw * 0.5f - 12f * S, 12f * S);
                    bool hov = r.Contains(mouse);
                    Box(r.x, r.y, r.width, r.height, new Color(1f, 0.4f, 0.35f, hov ? 0.4f : 0.15f));
                    PixelFont.Draw(r.x + 4f * S, r.y + 3f * S, BossArena.BossKinds[i], sm, Color.white);
                    if (hov && click) { g.sfx.Play("rumble", 0.8f); Setup = false; g.TestFightBoss(i); return; }
                }
                {
                    // the fight above the clouds
                    int i = BossArena.BossKinds.Length;
                    float bx = px + 8f * S + (i % 2) * (pw * 0.5f - 6f * S), by = top + (i / 2) * 14f * S;
                    var r = new Rect(bx, by, pw * 0.5f - 12f * S, 12f * S);
                    bool hov = r.Contains(mouse);
                    Box(r.x, r.y, r.width, r.height, new Color(1f, 0.85f, 0.3f, hov ? 0.45f : 0.2f));
                    PixelFont.Draw(r.x + 4f * S, r.y + 3f * S, "THE GOD (FROM ORNIN'S HALL)", FitSize(sm, "THE GOD (FROM ORNIN'S HALL)", r.width - 6f * S), Color.white);
                    if (hov && click) { g.sfx.Play("rumble", 0.8f); g.TestGodRoute(); return; }
                }
                {
                    // or just play: a normal run with everything you picked
                    int i = BossArena.BossKinds.Length + 1;
                    float bx = px + 8f * S + (i % 2) * (pw * 0.5f - 6f * S), by = top + (i / 2) * 14f * S;
                    var r = new Rect(bx, by, pw * 0.5f - 12f * S, 12f * S);
                    bool hov = r.Contains(mouse);
                    Box(r.x, r.y, r.width, r.height, new Color(0.5f, 1f, 0.5f, hov ? 0.6f : 0.3f));
                    PixelFont.Draw(r.x + 4f * S, r.y + 3f * S, Setup ? "START A RUN WITH THIS GEAR" : "BACK TO THE RUN", sm, Color.white);
                    if (hov && click) { Open = false; Setup = false; g.sfx.Play("buy", 0.7f); return; }
                }
                PixelFont.Draw(px + pw * 0.5f, bottom - 6f * S, "YOU'RE DROPPED INTO THE NEXT UNUSED ARENA (8 FIGHTS PER TEST RUN)", sm, new Color(0.75f, 0.75f, 0.8f), 1);
            }
            else
            {
                var cheats = new List<(string, System.Action)>
                {
                    ("GOD MODE: " + (God ? "ON" : "OFF"), () => God = !God),
                    ("DAMAGE NUMBERS: " + (Numbers ? "ON" : "OFF"), () => Numbers = !Numbers),
                    ("SPAWN TRAINING DUMMY", () => g.Spawn(new TrainingDummy(p.pos + new Vector2(p.facing * 3f, 0.5f)))),
                    ("+1000 GOLD", () => { p.AddGold(1000); picks.Add((3, null, 0)); }),
                    ("FULL HEAL", () => p.s.hp = p.s.maxHp),
                    ("+1 HEART (MAX HP)", () => { p.s.maxHp += Stats.Heart; p.s.hp += Stats.Heart; picks.Add((2, null, 0)); }),
                    ("REFILL SPECIAL", () => { p.charges = p.MaxCharges; p.energy = 0f; }),
                    ("KILL ALL ENEMIES", () => { foreach (var e in g.ents) if (!e.dead && e is Enemy en && !(en is TrainingDummy) && !(en is WormSegment)) en.TakeShot(999999f, Vector2.up); }),
                    ("SPAWN CRAWLER", () => SpawnAt(g, p, (x, y, d) => new Crawler(x, y, d, false))),
                    ("SPAWN GOLEM", () => SpawnAt(g, p, (x, y, d) => new Crawler(x, y, d, true))),
                    ("SPAWN BAT", () => SpawnAt(g, p, (x, y, d) => new Bat(x, y + 2, d, false))),
                    ("SPAWN RAIDER", () => SpawnAt(g, p, (x, y, d) => new Raider(x, y, d, Raider.Kind.Gunner))),
                    ("SPAWN BRUTE", () => SpawnAt(g, p, (x, y, d) => new Raider(x, y, d, Raider.Kind.Brute))),
                    ("SPAWN SPITTER", () => SpawnAt(g, p, (x, y, d) => new Spitter(x, y, d))),
                    ("SPAWN BEES", () => { for (int k = 0; k < 4; k++) SpawnAt(g, p, (x, y, d) => new Bee(new Vector2(x + 0.5f, y + 2f), d)); }),
                    ("SPAWN FLAME WISP", () => SpawnAt(g, p, (x, y, d) => new Wisp(x, y + 2, d, false))),
                    ("SPAWN FROST WISP", () => SpawnAt(g, p, (x, y, d) => new Wisp(x, y + 2, d, true))),
                    ("SPAWN MAGMA SLUG", () => SpawnAt(g, p, (x, y, d) => new MagmaSlug(x, y, d))),
                    ("SPAWN TOXIC TOAD", () => SpawnAt(g, p, (x, y, d) => new ToxicToad(x, y, d))),
                    ("SPAWN SPORE PUFF", () => SpawnAt(g, p, (x, y, d) => new SporePuff(x, y + 2, d))),
                    ("SPAWN SPARK BEETLE", () => SpawnAt(g, p, (x, y, d) => new SparkBeetle(x, y, d))),
                    ("SPAWN MIMIC", () => SpawnAt(g, p, (x, y, d) => new Mimic(new Vector2(x + 0.5f, y + 0.5f), d))),
                    ("START THE ASCENT (FLY UP TO THE GOD)", () => { if (!World.SkyMode && !Ascent.Flying) { Open = false; Setup = false; g.StartAscent(); } }),
                    ("SPAWN TURTLE", () => SpawnAt(g, p, (x, y, d) => new Turtle(x, y, d))),
                    ("SPAWN SKELETON", () => SpawnAt(g, p, (x, y, d) => new Skeleton(x, y, d))),
                    ("SPAWN WOLF", () => SpawnAt(g, p, (x, y, d) => new Wolf(x, y, d))),
                    ("SPAWN GLIDER BAT", () => SpawnAt(g, p, (x, y, d) => new GliderBat(x, y + 3, d))),
                    ("SPAWN ROCK MAGE", () => SpawnAt(g, p, (x, y, d) => new RockMage(x, y, d))),
                    ("SPAWN SLIME", () => SpawnAt(g, p, (x, y, d) => new Slime(x, y, d))),
                    ("SPAWN LANTERN SHROOM", () => SpawnAt(g, p, (x, y, d) => new MushroomElite(x, y, d))),
                    ("SPAWN VOID MAGE", () => SpawnAt(g, p, (x, y, d) => new VoidMage(x, y + 1, d, (VoidMage.Kind)Random.Range(0, 3)))),
                    ("SPAWN VOID ARCHMAGE", () => SpawnAt(g, p, (x, y, d) => new VoidMage(x, y + 1, d, VoidMage.Kind.Elite))),
                    ("SPAWN SHRINE", () => SpawnShrine(g, p)),
                    ("VOID KEY (BOTH PARTS)", () => { p.voidShards = 3; picks.Add((4, null, 0)); }),
                    ("ENTER THE VOID NOW", () => { Open = false; Setup = false; g.RequestVoid(); }),
                    ("SPAWN ELITE GOLEM", () => SpawnAt(g, p, (x, y, d) => { var e = new Crawler(x, y, d, true); e.MakeEliteSoon(); return e; })),
                };
                int cc = cheats.Count > 16 ? 3 : 2;
                for (int i = 0; i < cheats.Count; i++)
                {
                    float colW = (pw - 16f * S) / cc;
                    float bx = px + 8f * S + (i % cc) * colW, by = top + (i / cc) * 13f * S;
                    var r = new Rect(bx, by, colW - 4f * S, 11f * S);
                    bool hov = r.Contains(mouse);
                    Box(r.x, r.y, r.width, r.height, new Color(0.5f, 1f, 0.5f, hov ? 0.35f : 0.12f));
                    float fs = sm; while (fs > 1f && PixelFont.Width(cheats[i].Item1, fs) > r.width - 6f * S) fs -= 1f;
                    PixelFont.Draw(r.x + 4f * S, r.y + 2.5f * S, cheats[i].Item1, fs, Color.white);
                    if (hov && click) { cheats[i].Item2(); g.sfx.Play("blip", 0.5f, 1.2f); }
                }
            }
            // what's under the mouse
            if (hoverName != null)
            {
                PixelFont.Draw(px + 6f * S, bottom + 2f * S, hoverName, sm, new Color(1f, 0.85f, 0.4f));
                if (!string.IsNullOrEmpty(hoverDesc))
                {
                    string hd = Hud.Short(hoverDesc, 90);
                    float ls = sm; while (ls > 1f && PixelFont.Width(hd, ls) > pw - 12f * S) ls -= 1f;
                    PixelFont.Draw(px + 6f * S, bottom + 11f * S, hd, ls, new Color(0.85f, 0.85f, 0.9f));
                }
            }
            else if (noteT > 0f && note != null) PixelFont.Draw(px + 6f * S, bottom + 6f * S, note, sm, new Color(1f, 0.5f, 0.4f));
            else PixelFont.Draw(px + 6f * S, bottom + 6f * S, tab == 1 ? "CLICK A MODIFIER TO PUT IT ON (OR TAKE IT OFF) THE WEAPON IN HAND, 3 AT MOST" : "CLICK TO GET IT    Q/E OR LB/RB: TABS", sm, new Color(0.7f, 0.7f, 0.75f));
            lastMouse = mouse;
        }

        static int shrineN;
        static void SpawnShrine(Game g, Player p)
        {
            int x = Mathf.FloorToInt(p.pos.x + p.facing * 3f), y = Mathf.FloorToInt(p.pos.y - Player.Half.y + 0.1f);
            switch (shrineN++ % 4)
            {
                case 0: g.Spawn(new RandomAltar(x, y)); break;
                case 1: g.Spawn(new DeathAltar(x, y)); break;
                case 2: g.Spawn(new GoldChalice(x, y)); break;
                default: g.Spawn(new StoneDragon(x, y)); break;
            }
        }

        static void SpawnAt(Game g, Player p, System.Func<int, int, int, Enemy> make)
        {
            int x = Mathf.FloorToInt(p.pos.x + p.facing * 4f), y = Mathf.FloorToInt(p.pos.y);
            for (int i = 0; i < 6 && Game.I.world.Solid(x, y); i++) y++;
            g.Spawn(make(x, y, Mathf.Max(20, -y)));
        }
    }

    // A punching bag: takes every hit, never dies, and shows your damage per second.
    public class TrainingDummy : Enemy
    {
        readonly List<(float t, float d)> hits = new List<(float, float)>();
        float clock, last;
        protected override bool Harmless => true;

        public TrainingDummy(Vector2 at)
        {
            pos = at;
            half = new Vector2(0.42f, 0.4f);
            Setup("Training Dummy", "golem", 999, 0);
            dmg = 0; gold = 0;
            blood = new Color(0.8f, 0.7f, 0.5f);
        }

        public override bool Hurt(float d, Vector2 knock)
        {
            if (dead) return false;
            flash = 0.08f;
            hits.Add((clock, d)); last = d;
            if (TestMode.Numbers) TestMode.Number(pos + Vector2.up * half.y, d);
            G.sfx.Play("hit", 0.5f, 1.1f, 0.1f);
            return true;
        }

        public override void Tick(float dt)
        {
            clock += dt; animT += dt; flash -= dt;
            vel.y = Mathf.Max(vel.y - 30f * dt, -15f);
            Vector2 p = pos;
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) vel.y = 0f;
            pos = p;
            hits.RemoveAll(h => clock - h.t > 4f);
            float sum = 0f; foreach (var h in hits) sum += h.d;
            float window = Mathf.Clamp(clock - (hits.Count > 0 ? hits[0].t : clock), 1f, 4f);
            G.hud.Label(pos + Vector2.up * 1.3f, "DPS " + Mathf.RoundToInt(sum / window) + "   LAST " + Mathf.RoundToInt(last), new Color(0.6f, 1f, 0.6f));
            Draw(-half.y);
        }
    }
}
