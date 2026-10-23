using UnityEngine;

namespace DigDeep
{
    // Writing the player into a RunSave and reading it back.
    public partial class Player
    {
        public void CaptureTo(RunSave r)
        {
            r.px = pos.x; r.py = pos.y;
            r.stats = JsonUtility.ToJson(s);
            foreach (var k in skillOrder) { r.skillIds.Add(k.id); r.skillCounts.Add(SkillCount(k.id)); }
            foreach (var c in combos) r.combos.Add(c);
            foreach (var c in combosFired) r.combosFired.Add(c);
            for (int i = 0; i < slots.Length; i++)
            {
                var w = slots[i];
                r.weaponIds.Add(w != null ? w.def.id : "");
                string m = "";
                if (w != null) foreach (var mod in w.mods) m += (m.Length > 0 ? "," : "") + (int)mod;
                r.weaponMods.Add(m);
                r.weaponAmmo.Add(w != null ? w.ammo : -1);
                r.weaponCurse.Add(w != null ? (int)w.curse : 0);
                r.weaponCurseKills.Add(w != null ? w.curseKills : 0);
            }
            r.curWeapon = cur;
            r.special = special != null ? special.id : "";
            r.charges = charges;
            r.energy = energy;
            foreach (var it in items) { r.itemIds.Add(it.def.id); r.itemCounts.Add(it.count); }
            r.itemSel = sel;
            r.nextMoss = nextMoss;
            foreach (var id in seenOnce) r.seenOnce.Add(id);
        }

        public void RestoreFrom(RunSave r)
        {
            pos = new Vector2(r.px, r.py);
            vel = Vector2.zero;
            if (!string.IsNullOrEmpty(r.stats)) JsonUtility.FromJsonOverwrite(r.stats, s);

            // skills: their effects are already inside the saved stats, so they're just put back, not re-applied
            skills.Clear(); skillOrder.Clear();
            for (int i = 0; i < r.skillIds.Count; i++)
                if (DB.ById.TryGetValue(r.skillIds[i], out var d) && d is SkillDef k)
                {
                    skillOrder.Add(k);
                    skills[k.id] = i < r.skillCounts.Count ? r.skillCounts[i] : 1;
                }
            seenOnce.Clear();
            if (r.seenOnce != null) foreach (var id in r.seenOnce) seenOnce.Add(id);
            combos.Clear(); combosFired.Clear(); comboOrder.Clear();
            foreach (var c in r.combosFired) combosFired.Add(c);
            foreach (var c in r.combos) combos.Add(c);
            foreach (var c in DB.Combos) if (combos.Contains(c.id)) comboOrder.Add(c);

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = null;
                if (i >= r.weaponIds.Count || !DB.ById.TryGetValue(r.weaponIds[i], out var d) || !(d is AttackDef a)) continue;
                var w = new Weapon(a);
                if (i < r.weaponMods.Count && !string.IsNullOrEmpty(r.weaponMods[i]))
                    foreach (var part in r.weaponMods[i].Split(','))
                        if (int.TryParse(part, out int m) && m >= 0 && m < (int)Mod.COUNT) w.mods.Add((Mod)m);
                w.ammo = i < r.weaponAmmo.Count ? r.weaponAmmo[i] : -1;
                if (i < r.weaponCurse.Count) { w.curse = (Curse)Mathf.Clamp(r.weaponCurse[i], 0, (int)Curse.COUNT - 1); w.curseKills = i < r.weaponCurseKills.Count ? r.weaponCurseKills[i] : 0; }
                slots[i] = w;
            }
            cur = Mathf.Clamp(r.curWeapon, 0, slots.Length - 1);
            if (slots[cur] == null) cur = 1 - cur;

            special = !string.IsNullOrEmpty(r.special) && DB.ById.TryGetValue(r.special, out var sd) ? sd as SpecialDef : special;
            charges = Mathf.Clamp(r.charges, 0, MaxCharges);
            energy = r.energy;

            items.Clear();
            for (int i = 0; i < r.itemIds.Count; i++)
                if (DB.ById.TryGetValue(r.itemIds[i], out var d) && d is ItemDef id)
                    items.Add(new ItemStack { def = id, count = i < r.itemCounts.Count ? r.itemCounts[i] : 1 });
            sel = Mathf.Clamp(r.itemSel, 0, Mathf.Max(0, items.Count - 1));
            nextMoss = r.nextMoss;
            RebuildLooks();
        }
    }
}
