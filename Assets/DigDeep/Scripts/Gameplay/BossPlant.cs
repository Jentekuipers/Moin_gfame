using UnityEngine;

namespace DigDeep
{
    // =====================================================================================
    // THE VERDANT MAW (end of the Overgrown Jungle): a huge flower that lives in the thick bushes growing all over
    // its arena. It roots in one, attacks, then sinks into it and bursts out of another (the target bush shakes
    // first). In its second phase bushes also hang from the ceiling, and it drops out of those upside-down.
    // Vines burst up out of the floor where the earth trembles, it lobs arcs of poison seeds, sprouts snappers,
    // and spits rings of thorns. It's a plant: set it alight and it takes 50% more damage (and burns three times
    // as hard).
    // =====================================================================================
    public class VerdantMaw : Boss
    {
        enum S { Rooted, Sink, Under, Emerge }
        S mode = S.Rooted;
        float attackT = 2.5f, rainT, ringOff;
        int attacksHere, spot, nextSpot = -1;
        bool hanging;   // rooted in a ceiling bush
        static readonly Color Leaf = new Color(0.55f, 1f, 0.4f), Venom = new Color(0.8f, 0.45f, 1f);
        public override bool Plant => true;

        // bush spots: x, and whether it hangs from the ceiling
        readonly System.Collections.Generic.List<Vector2> spots = new System.Collections.Generic.List<Vector2>();
        readonly System.Collections.Generic.List<bool> ceil = new System.Collections.Generic.List<bool>();
        readonly System.Collections.Generic.List<GameObject> bushes = new System.Collections.Generic.List<GameObject>();
        bool ceilingGrown;

        public VerdantMaw(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("THE VERDANT MAW", "HEART OF THE JUNGLE", "verdantmaw", hp, 2.4f, new Vector2(1.5f, 1.5f), a, floor + Vector2.up * 1.51f);
            blood = new Color(0.5f, 0.95f, 0.35f);
            // floor bushes spread across the arena; the one in the middle is where it starts
            int n = 5;
            for (int i = 0; i < n; i++)
            {
                float x = Mathf.Lerp(a.MinX + 2.5f, a.MaxX - 2.5f, i / (float)(n - 1));
                AddBush(new Vector2(x, a.FloorY), false);
            }
            spot = n / 2;
            pos = new Vector2(spots[spot].x, a.FloorY + half.y + 0.01f);
        }

        void AddBush(Vector2 at, bool hang)
        {
            spots.Add(at); ceil.Add(hang);
            var b = new GameObject("MawBush");
            b.transform.SetParent(G.runRoot, false);
            Art.AddSR(b, Art.MawBush, 8);
            b.transform.position = new Vector3(at.x, at.y, 0f);
            b.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
            if (hang) b.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
            bushes.Add(b);
        }

        public override bool Hurt(float d, Vector2 knock) => base.Hurt(Burning ? d * 1.5f : d, knock);

        Vector2 Mouth => pos + Vector2.up * (half.y * (hanging ? -0.35f : 0.35f));

        Vector2 RootPos(int i) => ceil[i] ? new Vector2(spots[i].x, arena.CeilY - half.y - 0.01f) : new Vector2(spots[i].x, arena.FloorY + half.y + 0.01f);

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt;
            vel = Vector2.zero;   // only ever moves through the bushes
            if (Phase2 && !ceilingGrown)
            {
                // enraged: bushes sprout from the ceiling too
                ceilingGrown = true;
                for (int i = 0; i < 4; i++)
                {
                    var at = new Vector2(Mathf.Lerp(arena.MinX + 4f, arena.MaxX - 4f, i / 3f), arena.CeilY);
                    AddBush(at, true);
                    G.fx.Burst(at, 14, Leaf, 5f, 0.5f, -10f);
                }
                G.sfx.Play("rumble", 0.8f, 1.2f);
            }
            for (int i = 0; i < bushes.Count; i++)
            {
                // the bush it's about to burst from shakes and sheds leaves
                bool shake = i == nextSpot && (mode == S.Under || mode == S.Sink);
                float wob = shake ? Mathf.Sin(t * 40f) * 0.12f : Mathf.Sin(t * 1.5f + i) * 0.02f;
                bushes[i].transform.position = new Vector3(spots[i].x + wob, spots[i].y, 0f);
                if (shake && Random.value < 0.4f) G.fx.Burst(spots[i] + new Vector2(Random.Range(-1f, 1f), ceil[i] ? -0.6f : 0.6f), 1, Leaf, 3f, 0.4f, ceil[i] ? -15f : 15f);
            }

            stateT -= dt;
            switch (mode)
            {
                case S.Rooted:
                    FacePlayer();
                    attackT -= dt;
                    if (attackT <= 0f)
                    {
                        cycle++;
                        attacksHere++;
                        attackT = Phase2 ? 1.7f : 2.4f;
                        squash = -0.15f;
                        switch (cycle % 4)
                        {
                            case 0: VineLash(); attackT += 0.4f; break;
                            case 1: SeedBarrage(); break;
                            case 2: Sprout(); break;
                            default:
                                ringOff += 15f;
                                Ring(Mouth, Phase2 ? 16 : 11, 5f, ringOff, Art.Seed, Leaf);
                                if (Phase2) Ring(Mouth, 8, 3.2f, ringOff + 22f, Art.Seed, Leaf);
                                G.sfx.Play("shoot", 0.7f, 0.6f);
                                break;
                        }
                    }
                    // after a couple of attacks (or when you get in its face) it ducks away to another bush
                    bool crowded = (P.pos - pos).sqrMagnitude < 9f;
                    if (attackT > 0.6f && (attacksHere >= (Phase2 ? 2 : 3) || (crowded && attacksHere >= 1)))
                    {
                        attacksHere = 0;
                        nextSpot = PickSpot();
                        mode = S.Sink; stateT = 0.45f;
                        G.sfx.Play("rumble", 0.6f, 1.6f);
                    }
                    break;
                case S.Sink:   // pulls down into its bush
                    squash = Mathf.Lerp(0f, 0.45f, 1f - stateT / 0.45f);
                    intangible = stateT < 0.2f;
                    if (Random.value < 0.5f) G.fx.Burst(spots[spot], 2, Leaf, 4f, 0.4f, hanging ? -15f : 15f);
                    if (stateT <= 0f)
                    {
                        intangible = true;
                        sr.enabled = false;
                        mode = S.Under; stateT = Phase2 ? 0.7f : 1f;
                    }
                    break;
                case S.Under:  // racing through the roots: the destination bush shakes
                    if (stateT <= 0f)
                    {
                        spot = nextSpot;
                        hanging = ceil[spot];
                        pos = RootPos(spot);
                        sr.enabled = true;
                        intangible = false;
                        squash = 0.45f;
                        mode = S.Emerge; stateT = 0.35f;
                        G.sfx.Play("break", 0.8f, 0.8f);
                        G.cam.Shake(0.25f);
                        G.fx.Burst(spots[spot], 26, Leaf, 8f, 0.6f, hanging ? -20f : 20f, 2f);
                        // bursting out flings thorns all around
                        ringOff += 11f;
                        Ring(Mouth, Phase2 ? 12 : 8, 4.5f, ringOff, Art.Seed, Leaf);
                    }
                    break;
                case S.Emerge:
                    squash = Mathf.Lerp(0f, -0.2f, 1f - stateT / 0.35f);
                    if (stateT <= 0f)
                    {
                        nextSpot = -1;
                        mode = S.Rooted;
                        attackT = Mathf.Min(attackT, 0.8f);
                    }
                    break;
            }

            if (Phase2 && mode == S.Rooted)
            {
                rainT -= dt;
                if (rainT <= 0f) { rainT = 1.5f; CeilingRain(2, Art.Seed, Venom); }
            }
            if (mode == S.Under) return;
            DrawBoss(-half.y);
            if (hanging)
            {
                // hangs upside-down from the ceiling bush
                go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y + half.y), 0f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, 180f + Mathf.Sin(t * 1.3f) * 3f);
                sr.flipX = facing > 0;
            }
            else go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.3f) * 3f);   // it sways
        }

        // somewhere else: often the bush closest to you, otherwise any other
        int PickSpot()
        {
            int best = -1;
            if (Random.value < 0.5f)
            {
                float bd = float.MaxValue;
                for (int i = 0; i < spots.Count; i++)
                {
                    if (i == spot) continue;
                    float d = Mathf.Abs(spots[i].x - P.pos.x) + (ceil[i] ? 2f : 0f);
                    if (d < bd) { bd = d; best = i; }
                }
            }
            if (best < 0) { best = Random.Range(0, spots.Count - 1); if (best >= spot) best++; }
            return best;
        }

        public override void Kill()
        {
            foreach (var b in bushes) if (b != null) Object.Destroy(b);
            bushes.Clear();
            base.Kill();
        }

        // The floor trembles in a few spots (one under you), then thorny vines burst up out of it.
        void VineLash()
        {
            int n = Phase2 ? 4 : 3;
            int dmgP = Enemy.ContactDmg(arena.Depth);
            for (int i = 0; i < n; i++)
            {
                float x = i == 0 ? P.pos.x : Random.Range(arena.MinX + 1f, arena.MaxX - 1f);
                var c = new Vector2(Mathf.Floor(x) + 0.5f, arena.FloorY + 1.6f);
                G.Spawn(new Hazard(c, new Vector2(0.45f, 1.6f), 0.9f + i * 0.12f, 0.3f, Leaf, dmgP, false,
                    () => { G.fx.Burst(c, 14, Leaf, 6f, 0.4f, 20f); G.sfx.Play("break", 0.5f, 1.3f); }));
            }
            G.sfx.Play("rumble", 0.7f, 1.4f);
        }

        // Arcs of poison seeds lobbed at you.
        void SeedBarrage()
        {
            int n = Phase2 ? 7 : 5;
            Vector2 m = Mouth;
            for (int i = 0; i < n; i++)
            {
                float tFly = 1.1f + i * 0.08f;
                Vector2 to = P.pos + new Vector2((i - (n - 1) * 0.5f) * 1.2f, 0f);
                var v = new Vector2((to.x - m.x) / tFly, (to.y - m.y + 0.5f * 9f * tFly * tFly) / tFly);
                var s = new BossShot(m, v, Art.Seed, Venom, 9f, i * 0.06f, 1.1f) { poison = true };
                G.Spawn(s);
            }
            G.sfx.Play("launcher", 0.6f, 1.3f);
        }

        // New snappers sprout at the edges of the arena (or, if there are enough already, a thorn ring).
        void Sprout()
        {
            int alive = 0;
            foreach (var e in G.ents) if (!e.dead && e is Snapper && Mathf.Abs(e.pos.y - pos.y) < 12f) alive++;
            if (alive < 3)
            {
                int fy = Mathf.FloorToInt(arena.FloorY);
                G.Spawn(new Snapper(Mathf.FloorToInt(arena.MinX + 2f), fy, arena.Depth));
                G.Spawn(new Snapper(Mathf.FloorToInt(arena.MaxX - 2f), fy, arena.Depth));
                G.fx.Burst(Mouth, 20, Leaf, 6f, 0.5f, 5f);
                G.sfx.Play("bat", 0.8f, 0.5f);
            }
            else Ring(Mouth, 9, 4.5f, ringOff + 30f, Art.Seed, Leaf);
        }
    }
}
