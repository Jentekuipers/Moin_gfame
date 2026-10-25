using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // The starting cave: a crackling campfire, two tents, and the diggers you didn't pick sitting by the fire.
    // Every piece is a CampProp: shoot or blow it up and it breaks, dig out the floor and it falls.
    public class Camp : Entity
    {
        CampProp fire;
        readonly List<CampProp> pieces = new List<CampProp>();
        readonly List<(SpriteRenderer sr, float phase, float baseY)> sitters = new List<(SpriteRenderer, float, float)>();
        float t, crackleT;

        // ---- the character lineup (main menu): every digger stands by the fire, you pick one
        public readonly bool lineup;
        readonly List<(GameObject g, SpriteRenderer r, Vector2 feet)> line = new List<(GameObject, SpriteRenderer, Vector2)>();
        GameObject marker;
        public int Hover = -1;   // set by the menu: the digger under the mouse

        public Camp(Vector2 floor, int playerChar, bool lineup = false)
        {
            pos = floor + Vector2.up * 0.5f;
            floorAt = floor;
            this.lineup = lineup;
            CampProp.All.Clear();
            if (World.Void && !lineup)
            {
                // a void run: no camp, nobody waiting. Just a black cave and the portal that spat you out
                G.Spawn(new VoidExit(new Vector2(Gen.CampCx - 5.5f, floor.y + 1.6f)));
                return;
            }

            // tents at the cave's ends
            Piece(new CampProp("TentLeft", Art.TentA, new Vector2(floor.x - 9f, floor.y), 3, 14f, new Color(0.75f, 0.6f, 0.4f))).gold = 1;
            Piece(new CampProp("TentRight", Art.TentB, new Vector2(floor.x + 8.5f, floor.y), 3, 14f, new Color(0.6f, 0.65f, 0.45f))).gold = 1;

            // logs to sit on, either side of the fire
            // the logs carry the other diggers, so they can't be broken (they still drop if you dig them out)
            var logL = Piece(new CampProp("LogLeft", Art.CampLog, new Vector2(floor.x - 3.2f, floor.y), 4, 8f, new Color(0.5f, 0.35f, 0.2f)));
            var logR = Piece(new CampProp("LogRight", Art.CampLog, new Vector2(floor.x + 3.2f, floor.y), 4, 8f, new Color(0.5f, 0.35f, 0.2f)));
            logL.unbreakable = logR.unbreakable = true;

            // the other diggers: two per log, facing the fire. They ride along if their log falls.
            float[] seats = { -3.9f, -2.5f, 2.5f, 3.9f };
            int seat = 0;
            float[] standing = { 4.6f, -8.3f, 9.5f, -10.2f };   // the rest stand around warming their hands
            int stand = 0;
            if (lineup) BuildLineup(floor);
            for (int i = 0; i < Chars.All.Count && !lineup; i++)
            {
                if (i == playerChar) continue;
                var c = Chars.All[i];
                if (seat >= seats.Length)
                {
                    if (stand >= standing.Length) continue;
                    var sg = new GameObject("Standing " + c.name);
                    sg.transform.SetParent(G.runRoot, false);
                    float sx = floor.x + standing[stand];
                    sg.transform.position = new Vector3(sx, floor.y, 0f);
                    var sr2 = Art.AddSR(sg, c.frames[0], 9);
                    sr2.flipX = standing[stand] > 0f;
                    standers.Add(sg);
                    stand++;
                    continue;
                }
                var log = seats[seat] < 0f ? logL : logR;
                var g = new GameObject("Sitting " + c.name);
                g.transform.SetParent(log.Go.transform, false);
                float lx = floor.x + seats[seat] - log.pos.x, ly = 0.25f;   // on top of the log
                g.transform.localPosition = new Vector3(lx, ly, 0f);
                var r = Art.AddSR(g, c.frames[5], 9);
                r.flipX = seats[seat] > 0f;   // face the fire
                sitters.Add((r, Random.value * 6f, ly));
                seat++;
            }

            fire = Piece(new CampProp("Campfire", Art.CampFire[0], floor, 8, 3f, new Color(1f, 0.6f, 0.2f)));

            // the rest of the camp: supplies, tools, lights and a few glowing mushrooms
            Color wood = new Color(0.55f, 0.37f, 0.2f), metal = new Color(0.5f, 0.52f, 0.6f), cloth = new Color(0.7f, 0.25f, 0.2f);
            Clutter("Woodpile", Art.CampWood, -12f, 4, 8f, wood);
            Clutter("FlagLeft", Art.CampFlag, -10.9f, 2, 4f, cloth);
            Clutter("LanternLeft", Art.CampLantern, -7.4f, 4, 3f, metal).Light(3.2f, 0.7f);
            Clutter("Bedroll", Art.CampBedroll, -6.6f, 2, 4f, cloth);
            Clutter("Barrel", Art.CampBarrel, -5f, 4, 8f, wood).gold = 1;
            Clutter("StewPot", Art.CampPot, -1.5f, 7, 6f, metal).steam = true;
            Clutter("Pickaxe", Art.CampPick, 1.5f, 5, 4f, metal);
            Clutter("Crates", Art.CampCrates, 5.4f, 4, 12f, wood).gold = 1;
            Clutter("LanternRight", Art.CampLantern, 6.8f, 4, 3f, metal).Light(3.2f, 0.7f);
            Clutter("Sign", Art.CampSign, 10.1f, 5, 5f, wood);   // an arrow pointing down: the only way is down
            Clutter("Cart", Art.CampCart, 11.4f, 4, 10f, metal).gold = 1;   // 5 coins in the whole camp
            Clutter("ShroomA", Art.CampShroomBlue, -11.1f, 5, 1f, new Color(0.3f, 0.6f, 1f)).Light(1.6f, 0.55f);
            Clutter("ShroomB", Art.CampShroom, -8.1f, 5, 1f, new Color(0.8f, 0.3f, 0.3f));
            Clutter("ShroomC", Art.CampShroomBlue, 9.4f, 5, 1f, new Color(0.3f, 0.6f, 1f)).Light(1.6f, 0.55f);
            Clutter("ShroomD", Art.CampShroomBlue, 4.4f, 5, 1f, new Color(0.3f, 0.6f, 1f)).Light(1.4f, 0.5f);
        }

        Vector2 floorAt;

        // every digger in a row either side of the fire, the leftmost first
        void BuildLineup(Vector2 floor)
        {
            int n = Chars.All.Count;
            int left = (n + 1) / 2;
            for (int i = 0; i < n; i++)
            {
                float dx = i < left ? -1.7f - (left - 1 - i) * 2.05f : 1.7f + (i - left) * 2.05f;
                var feet = new Vector2(floor.x + dx, floor.y);
                for (int k = 0; k < 6 && Wd.Solid(Mathf.FloorToInt(feet.x), Mathf.FloorToInt(feet.y + 0.05f)); k++) feet.y = Mathf.Floor(feet.y + 0.05f) + 1f;
                var g = new GameObject("Lineup " + Chars.All[i].name);
                g.transform.SetParent(G.runRoot, false);
                g.transform.position = new Vector3(feet.x, feet.y, 0f);
                var r = Art.AddSR(g, Chars.All[i].frames[0], 12);
                r.flipX = dx > 0f;   // facing the fire
                line.Add((g, r, feet));
            }
            marker = new GameObject("PickMarker");
            marker.transform.SetParent(G.runRoot, false);
            var mr = Art.AddSR(marker, Art.Arrow, 22);
            mr.color = new Color(1f, 0.85f, 0.3f);
            marker.transform.rotation = Quaternion.Euler(0f, 0f, -90f);   // pointing down at the chosen one
            marker.SetActive(false);   // only shown while choosing
        }

        public int LineCount => line.Count;
        public Vector2 LineFeet(int i) => line[Mathf.Clamp(i, 0, line.Count - 1)].feet;

        // which digger (if any) is at this world point
        public int LineAt(Vector2 w)
        {
            for (int i = 0; i < line.Count; i++)
            {
                var f = line[i].feet;
                if (Mathf.Abs(w.x - f.x) < 0.8f && w.y > f.y - 0.2f && w.y < f.y + 1.9f) return i;
            }
            return -1;
        }

        void TickLineup()
        {
            // on the title screen they just stand around the fire; the highlight only shows while choosing
            bool choosing = G.state == Game.State.Select;
            int sel = choosing ? G.charIndex : -1;
            if (marker != null && marker.activeSelf != choosing) marker.SetActive(choosing);
            for (int i = 0; i < line.Count; i++)
            {
                var (g, r, feet) = line[i];
                if (g == null) continue;
                bool on = i == sel;
                float bob = on ? Mathf.Abs(Mathf.Sin(t * 5f)) * 0.12f : 0f;
                g.transform.position = new Vector3(U.Snap(feet.x), U.Snap(feet.y + bob), 0f);
                float k = !choosing ? 0.85f : on ? 1f : i == Hover ? 0.8f : 0.45f;
                r.color = new Color(k, k, k * (on ? 1f : 1.05f), 1f);
                r.sprite = Chars.All[i].frames[on && ((int)(t * 3f)) % 4 == 0 ? 3 : 0];
                g.transform.localScale = on ? new Vector3(1.12f, 1.12f, 1f) : Vector3.one;
            }
            if (marker != null && sel >= 0 && sel < line.Count)
            {
                var f = line[sel].feet;
                marker.transform.position = new Vector3(U.Snap(f.x), U.Snap(f.y + 2.3f + Mathf.Sin(t * 4f) * 0.12f), 0f);
            }
        }

        // the chosen digger steps out of the line: the rest vanish in a puff (the camp is rebuilt with them seated)
        public void EndLineup(int chosen)
        {
            for (int i = 0; i < line.Count; i++)
            {
                if (line[i].g == null) continue;
                if (i != chosen) G.fx.Smoke(line[i].feet + Vector2.up * 0.8f, 3, 0.7f);
                Object.Destroy(line[i].g);
            }
            line.Clear();
            if (marker != null) Object.Destroy(marker);
        }

        CampProp Clutter(string name, Sprite s, float dx, int order, float hp, Color debris) =>
            Piece(new CampProp(name, s, new Vector2(floorAt.x + dx, floorAt.y), order, hp, debris));

        CampProp Piece(CampProp p)
        {
            pieces.Add(p);
            G.Spawn(p);
            return p;
        }

        bool FireLit => fire != null && !fire.dead;

        public override void Tick(float dt)
        {
            t += dt;
            if (lineup) TickLineup();
            if (FireLit)
            {
                pos = fire.pos + Vector2.up * 0.5f;
                fire.Sr.sprite = Art.CampFire[((int)(t * 8f)) % Art.CampFire.Length];
                if (Random.value < 0.25f)
                    G.fx.Burst(pos + new Vector2(Random.Range(-0.2f, 0.2f), 0.3f), 1, new Color(1f, Random.Range(0.5f, 0.85f), 0.2f), 1.2f, 0.9f, -5f, 1f, false);
                if (Random.value < 0.05f)
                    G.fx.Burst(pos + Vector2.up * 0.8f, 1, new Color(0.5f, 0.45f, 0.45f, 0.5f), 0.6f, 1.6f, -2f, 2f, false);   // smoke
            }

            // the others breathe, nod and look around a little
            foreach (var (r, phase, baseY) in sitters)
            {
                if (r == null) continue;   // their log was smashed
                float k = t + phase;
                var tr = r.transform;
                tr.localPosition = new Vector3(tr.localPosition.x, baseY + (Mathf.Sin(k * 1.7f) > 0.97f ? 0.0625f : 0f), 0f);
                tr.localScale = new Vector3(1f, 1f + Mathf.Sin(k * 2.2f) * 0.02f, 1f);
            }

            crackleT -= dt;
            var p = G.player;
            if (FireLit && crackleT <= 0f && (p.pos - pos).sqrMagnitude < 100f)
            {
                crackleT = Random.Range(0.25f, 0.9f);
                G.sfx.Play("fuse", 0.05f, Random.Range(0.6f, 1.4f), 0.1f);
            }
        }

        public override void AddLight(Lighting l)
        {
            if (!FireLit) return;
            float flick = 1f + Mathf.Sin(t * 13f) * 0.04f + Mathf.Sin(t * 7.3f) * 0.05f + Random.Range(-0.02f, 0.02f);
            l.Add(pos, 8.5f * flick, 1f);
        }

        public override bool AlwaysActive => true;

        readonly List<GameObject> standers = new List<GameObject>();

        public override void Kill()
        {
            base.Kill();
            foreach (var pc in pieces) pc.Kill();
            foreach (var g in standers) if (g != null) Object.Destroy(g);
            EndLineup(-1);
        }
    }

    // A breakable piece of scenery (tent, log, campfire). Falls when nothing holds it up,
    // and smashes after a long drop.
    public class CampProp : Entity
    {
        public static readonly List<CampProp> All = new List<CampProp>();

        public Vector2 half;
        public bool steam, unbreakable;
        public int gold;   // coins it drops when smashed
        float hp, vy, flashT, lightR, lightI, t;
        bool falling;
        float fallFrom;
        readonly Color debris;

        public GameObject Go => go;
        public SpriteRenderer Sr => sr;
        public Vector2 Center => pos + new Vector2(0f, half.y);

        public CampProp(string name, Sprite s, Vector2 feet, int order, float hp, Color debris)
        {
            pos = feet;
            // the cave floor curves up at the ends: stand on whatever ground is really there
            for (int i = 0; i < 6 && Wd.Solid(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y + 0.05f)); i++) pos.y = Mathf.Floor(pos.y + 0.05f) + 1f;
            this.hp = hp;
            this.debris = debris;
            MakeGO(name, s, order);
            var e = s.bounds.extents;
            half = new Vector2(Mathf.Max(0.3f, e.x * 0.85f), Mathf.Max(0.2f, e.y));
            All.Add(this);
        }

        public override void Tick(float dt)
        {
            flashT -= dt;
            t += dt;
            if (steam && Random.value < 0.06f)
                G.fx.Burst(Center + new Vector2(Random.Range(-0.2f, 0.2f), half.y), 1, new Color(0.9f, 0.9f, 0.95f, 0.5f), 0.5f, 1.4f, -2f, 1.5f, false);
            int row = Mathf.FloorToInt(pos.y - 0.05f);
            if (!Supported(row))
            {
                if (!falling) { falling = true; fallFrom = pos.y; vy = 0f; }
                vy = Mathf.Max(vy - 32f * dt, -16f);
                pos.y += vy * dt;
                int nr = Mathf.FloorToInt(pos.y - 0.05f);
                if (Supported(nr))
                {
                    pos.y = nr + 1f;
                    falling = false;
                    G.fx.Burst(pos, 6, debris, 2.5f, 0.3f, 6f);
                    G.sfx.Play("land", 0.4f, 0.8f);
                    if (fallFrom - pos.y > 5f && !unbreakable) { Break(); return; }   // smashed by the drop
                }
            }
            Place();
            if (sr != null) sr.color = flashT > 0f ? new Color(1f, 0.7f, 0.7f) : Color.white;
        }

        public CampProp Light(float r, float i) { lightR = r; lightI = i; return this; }

        public override void AddLight(Lighting l)
        {
            if (lightR <= 0f) return;
            float flick = 1f + Mathf.Sin(t * 11f + pos.x) * 0.05f;
            l.Add(pos + new Vector2(0f, half.y * 1.6f), lightR * flick, lightI);
        }

        bool Supported(int row)
        {
            int x0 = Mathf.FloorToInt(pos.x - half.x * 0.8f), x1 = Mathf.FloorToInt(pos.x + half.x * 0.8f);
            for (int x = x0; x <= x1; x++) if (Wd.Solid(x, row)) return true;
            return false;
        }

        public void Hurt(float d)
        {
            if (dead) return;
            if (unbreakable) { flashT = 0.05f; G.fx.Burst(Center, 2, debris, 2f, 0.2f, 8f); return; }
            hp -= d;
            flashT = 0.08f;
            G.fx.Burst(Center, 3, debris, 2.5f, 0.25f, 8f);
            G.sfx.Material(Block.Chest, false, 0.6f);
            if (hp <= 0f) Break();
        }

        void Break()
        {
            if (dead) return;
            G.fx.Debris(Center, debris, 16);
            G.fx.Smoke(Center, 3, 0.8f);
            G.sfx.Material(Block.Chest, true, 1f);
            if (gold > 0) G.SpawnGold(Center, gold);
            Kill();
        }

        public override void Kill()
        {
            base.Kill();
            All.Remove(this);
        }

        // Something hit an area: every prop inside takes the damage. Returns true if anything was hit.
        public static bool HitBox(Vector2 c, Vector2 h, float dmg)
        {
            bool any = false;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                if (i >= All.Count) continue;
                var p = All[i];
                if (p.dead || !U.Overlap(c, h, p.Center, p.half)) continue;
                p.Hurt(dmg);
                any = true;
            }
            return any;
        }
    }
}
