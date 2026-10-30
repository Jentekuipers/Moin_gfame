using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // ACHIEVEMENTS AND UNLOCKS. FIN is the one digger you start with; the others, and a good share of the weapons and
    // trinkets, are earned: finishing runs, killing in total, little challenges (no kills in the first biome but
    // the boss, four foes in one blast...). Easy ones open common gear, hard ones the rare and mythic things.
    // Each unlock plays its own fanfare the moment it's earned; at the end of the run (on the death screen, or before
    // the title after a win) each is shown on its own card, Isaac-style.
    public class UnlockDef
    {
        public string id, title, how;
        public string[] rewards;   // weapon / trinket ids, or "char:<id>"
        public int tier;           // 1 easy .. 4 very hard
    }

    public static class Unlocks
    {
        const string K = "ul_";
        public static readonly List<UnlockDef> All = new List<UnlockDef>();
        static readonly Dictionary<string, UnlockDef> byReward = new Dictionary<string, UnlockDef>();
        static readonly Dictionary<string, UnlockDef> byId = new Dictionary<string, UnlockDef>();
        public static readonly List<UnlockDef> Pending = new List<UnlockDef>();   // earned this run, not yet shown

        static void U(string id, int tier, string title, string how, params string[] rewards)
        {
            var u = new UnlockDef { id = id, tier = tier, title = title, how = how, rewards = rewards };
            All.Add(u);
            byId[id] = u;
            foreach (var r in rewards) byReward[r] = u;
        }

        static bool inited;
        public static void Init()
        {
            if (inited) return;
            inited = true;
            // ---- easy: diggers and common/uncommon gear
            U("boss1", 1, "THE FIRST HALL", "BEAT YOUR FIRST BOSS", "char:bram", "crossbow");
            U("kills100", 1, "CULLER", "KILL 100 FOES (IN ALL RUNS)", "char:nell");
            U("deaths5", 1, "SECOND WIND", "DIE 5 TIMES", "char:ribb");
            U("runs3", 1, "STUBBORN", "PLAY 3 RUNS", "uzi");
            U("deep150", 1, "DEEPER", "REACH DEPTH 150", "deagle");
            U("gold1000", 1, "PROSPECTOR", "CARRY OUT 1000 GOLD (IN ALL RUNS)", "goldgun");
            // ---- medium
            U("pacifist", 2, "PACIFIST", "REACH THE SECOND BIOME WITHOUT KILLING ANYTHING BUT THE BOSS", "char:vex");
            U("boom4", 2, "FOUR IN ONE", "KILL 4 FOES WITH ONE EXPLOSION", "char:tor");
            U("kills250", 2, "THINNING THE HERD", "KILL 250 FOES (IN ALL RUNS)", "autocrossbow");
            U("burn50", 2, "PYROMANIAC", "KILL 50 BURNING FOES (IN ALL RUNS)", "flamer");
            U("melee200", 2, "UP CLOSE", "KILL 200 FOES WITH MELEE (IN ALL RUNS)", "hammer");
            U("pet30", 2, "PACK LEADER", "YOUR PETS KILL 30 FOES (IN ALL RUNS)", "petdragon");
            U("pin10", 2, "TAXIDERMIST", "PIN 10 FOES TO THE WALL WITH ARROWS", "stormbow");
            // ---- hard: rare gear
            U("untouched", 3, "UNTOUCHABLE", "REACH THE SECOND BIOME WITHOUT TAKING A HIT", "esword");
            U("boss5", 3, "HALL CRAWLER", "BEAT 5 BOSSES (IN ALL RUNS)", "char:ink", "supercrossbow");
            U("bossnohit", 3, "FLAWLESS", "BEAT A BOSS WITHOUT GETTING HIT", "char:liv");
            U("boom6", 3, "CARNAGE", "KILL 6 FOES WITH ONE EXPLOSION", "rocket");
            U("deep300", 3, "THE UNDERDARK", "REACH DEPTH 300", "railgun");
            U("kills1000", 3, "EXTERMINATOR", "KILL 1000 FOES (IN ALL RUNS)", "minigun");
            U("pet100", 3, "BEST FRIENDS", "YOUR PETS KILL 100 FOES (IN ALL RUNS)", "petfox");
            // ---- very hard: mythic
            U("win1", 4, "THE GOD-DIAMOND", "FINISH A RUN", "char:nyx", "deepbow");
            U("win3", 4, "MASTER DIGGER", "FINISH 3 RUNS", "prismblade");
            U("boss15", 4, "BOSS HUNTER", "BEAT 15 BOSSES (IN ALL RUNS)", "seeker");
            U("voidwin", 4, "INTO THE VOID", "BEAT A VOID RUN", "theone");
            // ---- every digger has their own: one for finishing a run with them, one for sealing the void with them
            string[] who = { "bram", "nell", "ribb", "tor", "fin", "vex", "nyx", "ink", "liv" };
            string[] winTitle = { "MOLE KING", "FOX FIRE", "TOP FROG", "UNBREAKABLE", "THE DEEP SEA", "NIGHT WATCH", "NINE LIVES", "ALL ARMS", "ONE SHOT" };
            string[] winGift = { "earthsplitter", "phoenix", "plasma", "adamant", "sniper", "wings", "blades", "crystalpike", "starpick" };
            string[] voidTitle = { "VOID MOLE", "VOID FOX", "VOID FROG", "VOID BEETLE", "VOID DOLPHIN", "VOID OWL", "VOID CAT", "VOID OCTOPUS", "VOID LYNX" };
            string[] voidGift = { "worldeater", "supernova", "writhing", "gauntlet", "starlance", "godray", "phantom", "mirror", "crown" };
            for (int i = 0; i < who.Length; i++)
            {
                string nm = who[i].ToUpperInvariant();
                U("win_" + who[i], 4, winTitle[i], "FINISH A RUN AS " + nm, winGift[i]);
                U("void_" + who[i], 4, voidTitle[i], "SEAL THE VOID AS " + nm, voidGift[i]);
            }
            // diggers who already won (or sealed the void) before there were unlocks keep what they earned
            for (int i = 0; i < who.Length; i++)
            {
                if (PlayerPrefs.GetInt("digdeep_won_" + who[i], 0) == 1) { PlayerPrefs.SetInt(K + "win_" + who[i], 1); PlayerPrefs.SetInt(K + "win1", 1); }
                if (PlayerPrefs.GetInt("digdeep_voidwon_" + who[i], 0) == 1) { PlayerPrefs.SetInt(K + "void_" + who[i], 1); PlayerPrefs.SetInt(K + "voidwin", 1); }
            }
        }

        public static bool Done(string id) { Init(); return PlayerPrefs.GetInt(K + id, 0) == 1; }

        // is this weapon / trinket / item open to find? (everything is, in test mode)
        public static bool Open(Def d)
        {
            if (d == null || TestMode.On) return true;
            Init();
            return !byReward.TryGetValue(d.id, out var u) || Done(u.id);
        }

        public static bool CharOpen(CharDef c)
        {
            if (c == null || TestMode.On || c.id == "fin") return true;
            Init();
            return !byReward.TryGetValue("char:" + c.id, out var u) || Done(u.id);
        }

        // how to unlock it (for the hover text), or null if it isn't locked
        public static string How(string rewardId)
        {
            Init();
            return byReward.TryGetValue(rewardId, out var u) && !Done(u.id) ? u.how : null;
        }

        public static void Grant(string id)
        {
            if (TestMode.On || Done(id)) return;
            Init();
            if (!byId.TryGetValue(id, out var u)) return;
            PlayerPrefs.SetInt(K + id, 1);
            PlayerPrefs.Save();
            Pending.Add(u);
            var g = Game.I;
            if (g != null)
            {
                g.sfx.Play("unlock", 0.8f, 1f, 0f);   // its own fanfare, the moment it's earned
                if (g.player != null && g.player.mode != Player.Mode.Dead) g.Popup(g.player.pos + Vector2.up * 1.4f, "UNLOCKED: " + u.title, new Color(1f, 0.85f, 0.3f));
            }
        }

        // ---------------------------------------------------------------- what the game reports

        static int runKillsBiome1;   // non-boss kills while still in the first biome
        static bool hurtBiome1, bossHit;
        static int lastBiome;

        // a fresh run starts the first-biome challenges over; a resumed one can't earn them (it's past the start)
        public static void RunStarted(bool fresh)
        {
            runKillsBiome1 = fresh ? 0 : 1; hurtBiome1 = !fresh; bossHit = false; Src = 0; BlastCount = -1;
            lastBiome = fresh || Game.I == null ? 0 : Biomes.Index(Game.I.maxDepth);
        }

        public static void Kill(Enemy e, Player p)
        {
            if (TestMode.On || e == null) return;
            if (!(e is Boss) && !(e is WormSegment) && lastBiome == 0) runKillsBiome1++;
            int total = Records.Get("kills") + (p != null ? p.s.kills : 0);
            if (total >= 100) Grant("kills100");
            if (total >= 250) Grant("kills250");
            if (total >= 1000) Grant("kills1000");
            if (e.Burning && Count("burnkills") >= 50) Grant("burn50");
            if (Src == 1) MeleeKill();
            else if (Src == 2) PetKill();
            if (BlastCount >= 0) BlastCount++;
        }

        public static int Src;              // what's dealing the damage right now: 1 a melee swing, 2 a pet's bite
        public static int BlastCount = -1;  // kills counted inside one explosion (-1 when none is going off)

        public static void MeleeKill() { if (Count("meleekills") >= 200) Grant("melee200"); }
        public static void PetKill() { int n = Count("petkills"); if (n >= 30) Grant("pet30"); if (n >= 100) Grant("pet100"); }
        public static void Pinned() { if (Count("pins") >= 10) Grant("pin10"); }
        static int Count(string k) { if (TestMode.On) return 0; Records.Add(k); return Records.Get(k); }

        public static void BlastKills(int n)
        {
            if (n >= 4) Grant("boom4");
            if (n >= 6) Grant("boom6");
        }

        public static void Hurt() { bossHit = true; if (lastBiome == 0) hurtBiome1 = true; }
        public static void BossFightStarted() { bossHit = false; }

        public static void BossBeaten()
        {
            Grant("boss1");
            if (!bossHit) Grant("bossnohit");
            int b = Records.Get("bosses");
            if (b >= 5) Grant("boss5");
            if (b >= 15) Grant("boss15");
        }

        public static void Depth(int d)
        {
            int bi = Biomes.Index(d);
            if (bi > lastBiome)
            {
                if (lastBiome == 0 && bi >= 1)
                {
                    if (runKillsBiome1 == 0) Grant("pacifist");
                    if (!hurtBiome1) Grant("untouched");
                }
                lastBiome = bi;
            }
            if (d >= 150) Grant("deep150");
            if (d >= 300) Grant("deep300");
        }

        public static void RunEnded(bool won)
        {
            if (TestMode.On) return;
            if (Records.Get("runs") >= 3) Grant("runs3");
            if (Records.Get("deaths") >= 5) Grant("deaths5");
            if (Records.Get("gold") >= 1000) Grant("gold1000");
            if (won)
            {
                Grant("win1");
                if (Records.Get("wins") >= 3) Grant("win3");
                if (Game.I != null && Game.I.player != null) Grant("win_" + Game.I.player.character.id);
            }
        }

        // the void sealed (the Void Archmage beaten)
        public static void VoidWon(string charId)
        {
            Grant("voidwin");
            if (!string.IsNullOrEmpty(charId)) Grant("void_" + charId);
        }

        // ---------------------------------------------------------------- what an unlock looks like

        public static Sprite RewardIcon(string r, out string name)
        {
            if (r.StartsWith("char:"))
            {
                string cid = r.Substring(5);
                foreach (var c in Chars.All)
                    if (c.id == cid) { name = c.name + " THE " + c.animal; return c.frames != null && c.frames.Length > 0 ? c.frames[0] : null; }
                name = cid.ToUpperInvariant();
                return null;
            }
            if (DB.ById.TryGetValue(r, out var d)) { name = d.name; return d.icon; }
            name = r.ToUpperInvariant();
            return null;
        }
    }

    // The unlock cards at the end of a run: one at a time; any click or key flicks the card away to the next.
    public static class UnlockPopup
    {
        static bool on;
        static float t, closeT = -1f;
        public static bool Active => on && Unlocks.Pending.Count > 0;

        public static void Begin()
        {
            if (Unlocks.Pending.Count == 0 || on) return;
            on = true; t = 0f; closeT = -1f;
            Game.I.sfx.Play("unlockcard", 0.7f, 1f, 0f);
        }

        // true while the cards are up (they take all the input)
        public static bool Tick(float dt)
        {
            if (!Active) { on = false; return false; }
            t += dt;
            if (closeT >= 0f)
            {
                closeT += dt;
                if (closeT >= 0.12f)
                {
                    Unlocks.Pending.RemoveAt(0);
                    closeT = -1f; t = 0f;
                    if (Unlocks.Pending.Count == 0) { on = false; return true; }
                    Game.I.sfx.Play("unlockcard", 0.7f, 1f, 0f);
                }
                return true;
            }
            if (t > 0.3f && (GameInput.AnyPressed() || GameInput.MouseClicked())) { closeT = 0f; Game.I.sfx.Play("swing", 0.5f, 1.6f); }
            return true;
        }

        static void Box(float x, float y, float w, float h, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), Art.White);
            GUI.color = old;
        }

        static void Pic(Sprite s, Rect at, Color c)
        {
            if (s == null) return;
            var tx = s.texture; var r = s.textureRect;
            float k = Mathf.Min(at.width / r.width, at.height / r.height);
            k = Mathf.Max(1f, Mathf.Floor(k));
            float w = r.width * k, h = r.height * k;
            var old = GUI.color; GUI.color = c;
            GUI.DrawTextureWithTexCoords(new Rect(at.center.x - w * 0.5f, at.center.y - h * 0.5f, w, h), tx,
                new Rect(r.x / tx.width, r.y / tx.height, r.width / tx.width, r.height / tx.height));
            GUI.color = old;
        }

        // the biggest whole scale (up to want) at which the line fits in maxW
        static float Fit(string str, float want, float maxW)
        {
            float z = want;
            while (z > 1f && PixelFont.Width(str, z) > maxW) z -= 1f;
            return z;
        }

        public static void Draw()
        {
            if (!Active) return;
            var u = Unlocks.Pending[0];
            float S = Mathf.Max(2f, Mathf.Floor(Screen.height / 270f * Settings.UiScale));
            float W = Screen.width, H = Screen.height;
            // pop in (a little overshoot), flick out (shrink and fade fast)
            float k = closeT >= 0f ? 1f - closeT / 0.12f : Mathf.Min(1f, t / 0.16f);
            float sc = closeT >= 0f ? Mathf.Lerp(0.2f, 1f, k) : (k < 1f ? Mathf.Lerp(0.6f, 1.06f, k) : Mathf.Lerp(1.06f, 1f, Mathf.Min(1f, (t - 0.16f) / 0.1f)));
            float alpha = closeT >= 0f ? k : Mathf.Min(1f, t / 0.1f);
            Box(0, 0, W, H, new Color(0f, 0f, 0f, 0.55f * alpha));
            float cw = 170f * S * sc, ch = 120f * S * sc;
            float cx = W * 0.5f - cw * 0.5f, cy = H * 0.5f - ch * 0.5f;
            Color tierC = u.tier >= 4 ? new Color(1f, 0.45f, 0.95f) : u.tier == 3 ? new Color(0.45f, 0.75f, 1f) : u.tier == 2 ? new Color(0.5f, 1f, 0.55f) : new Color(1f, 0.85f, 0.35f);
            // the card: a dark slab with a gold rim and a tier-coloured inner line, like the rest of the HUD
            Box(cx - 2f * S, cy - 2f * S, cw + 4f * S, ch + 4f * S, new Color(0.02f, 0.01f, 0.04f, alpha));
            Box(cx, cy, cw, ch, new Color(0.85f, 0.66f, 0.25f, alpha));
            Box(cx + S, cy + S, cw - 2f * S, ch - 2f * S, new Color(0.09f, 0.07f, 0.12f, alpha));
            Box(cx + 3f * S, cy + 3f * S, cw - 6f * S, S, new Color(tierC.r, tierC.g, tierC.b, 0.6f * alpha));
            if (sc < 0.7f) return;
            float ts = Mathf.Max(1f, Mathf.Round(S * sc));
            PixelFont.Draw(W * 0.5f, cy + 8f * S * sc, "YOU UNLOCKED", ts, new Color(0.9f, 0.85f, 0.75f, alpha), 1);
            // the reward(s): name in big letters and the picture, bobbing a little
            float bob = Mathf.Sin(t * 4f) * 1.5f * S;
            int n = u.rewards.Length;
            float slotW = (cw - 20f * S) / n;
            for (int i = 0; i < n; i++)
            {
                var icon = Unlocks.RewardIcon(u.rewards[i], out var nm);
                float sx = cx + 10f * S * sc + slotW * i;
                Pic(icon, new Rect(sx, cy + 29f * S * sc + bob, slotW, 40f * S * sc), new Color(1f, 1f, 1f, alpha));
                string nl = "'" + nm + "'";
                PixelFont.Draw(sx + slotW * 0.5f, cy + 73f * S * sc, nl, Fit(nl, ts * 2f, slotW - 2f * S), new Color(tierC.r, tierC.g, tierC.b, alpha), 1);
            }
            PixelFont.Draw(W * 0.5f, cy + 19f * S * sc, u.title, Fit(u.title, ts, cw - 10f * S), new Color(1f, 0.82f, 0.32f, alpha), 1);
            // how it was earned: one line if it fits, else split in two at a space near the middle
            {
                float hs = Mathf.Max(1f, ts - 1f), maxW = cw - 10f * S;
                var hc = new Color(0.7f, 0.7f, 0.78f, alpha);
                if (PixelFont.Width(u.how, hs) <= maxW) PixelFont.Draw(W * 0.5f, cy + 92f * S * sc, u.how, hs, hc, 1);
                else
                {
                    int cut = u.how.LastIndexOf(' ', u.how.Length / 2 + 4);
                    if (cut <= 0) cut = u.how.IndexOf(' ');
                    string a1 = cut > 0 ? u.how.Substring(0, cut) : u.how, a2 = cut > 0 ? u.how.Substring(cut + 1) : "";
                    float z = Mathf.Min(Fit(a1, hs, maxW), Fit(a2, hs, maxW));
                    PixelFont.Draw(W * 0.5f, cy + 90f * S * sc, a1, z, hc, 1);
                    PixelFont.Draw(W * 0.5f, cy + 90f * S * sc + 9f * z, a2, z, hc, 1);
                }
            }
            string more = Unlocks.Pending.Count > 1 ? "1 / " + Unlocks.Pending.Count + "   " : "";
            PixelFont.Draw(W * 0.5f, cy + ch - 10f * S * sc, more + "CLICK TO CONTINUE", Mathf.Max(1f, ts - 1f), new Color(0.55f, 0.55f, 0.62f, alpha * (0.6f + 0.4f * Mathf.Sin(t * 5f))), 1);
        }
    }
}
