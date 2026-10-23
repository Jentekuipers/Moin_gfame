using UnityEngine;

namespace DigDeep
{
    // Curses carried on your weapons, and what they do to you.
    public partial class Player
    {
        public bool Cursed(Curse c)
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i] != null && slots[i].curse == c) return true;
            return false;
        }

        public bool AnyCurse
        {
            get
            {
                for (int i = 0; i < slots.Length; i++) if (slots[i] != null && slots[i].curse != Curse.None) return true;
                return false;
            }
        }

        // every kill counts towards lifting the curses on the weapons you carry
        void CurseKill()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var w = slots[i];
                if (w == null || w.curse == Curse.None) continue;
                w.curseKills++;
                if (w.curseKills < Curses.KillsToLift) continue;
                var c = w.curse;
                w.curse = Curse.None;
                G.hud.Banner("CURSE LIFTED", Curses.Name(c), Curses.Col(c));
                G.sfx.Play("chest", 0.8f, 1.5f);
                G.fx.Ring(pos, 2.5f, Curses.Col(c));
                G.fx.Burst(pos, 30, Curses.Col(c), 7f, 0.7f, -4f);
            }
        }

        // ---- CURSE OF THE FLAME: the block you just stepped off bursts into flame (not on you)
        Vector2Int lastFoot = new Vector2Int(int.MinValue, 0);

        void CurseFootsteps()
        {
            if (!Cursed(Curse.Flame)) { lastFoot.x = int.MinValue; return; }
            if (!grounded) return;
            var foot = new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y - Half.y + 0.05f));
            if (foot == lastFoot) return;
            if (lastFoot.x != int.MinValue && Wd.Solid(lastFoot.x, lastFoot.y - 1)) Fire.Surface(lastFoot.x, lastFoot.y, Fire.Life, true);
            lastFoot = foot;
        }

        // ---- CURSE OF POVERTY: a hit shakes coins loose
        void CursePoverty()
        {
            if (!Cursed(Curse.Poverty) || s.gold <= 0) return;
            int lose = Mathf.Max(1, Mathf.CeilToInt(s.gold * 0.15f));
            s.gold -= lose;
            int n = Mathf.Min(lose, 8), each = lose / n, extra = lose - each * n;
            for (int i = 0; i < n; i++)
                G.Spawn(new Coin(pos + Vector2.up * 0.3f, each + (i == 0 ? extra : 0), new Vector2(Random.Range(-6f, 6f), Random.Range(5f, 10f)), 0.9f));
            G.Popup(pos + Vector2.up * 1.3f, "-" + lose + " GOLD", Curses.Col(Curse.Poverty));
        }

        // ---- CURSE OF THIRST
        public bool NoHealing()
        {
            if (!Cursed(Curse.Thirst)) return false;
            G.Popup(pos + Vector2.up, "THIRST: NO HEALING", Curses.Col(Curse.Thirst));
            return true;
        }
    }
}
