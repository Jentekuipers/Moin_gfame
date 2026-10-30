using UnityEngine;

namespace DigDeep
{
    // =====================================================================================
    // THE VERDANT MAW (end of the Overgrown Jungle): a huge flower hanging from the middle of the ceiling.
    // Vines burst up out of the floor where the earth trembles, it lobs arcs of poison seeds, spits rings of thorns,
    // and now and then shoots a living seed: where it hits the floor, a wall or the ceiling, a snapper grows out,
    // rooted to that surface. In its second phase poison drips from the ceiling. It's a plant: set it alight and it
    // takes 50% more damage (and burns three times as hard).
    // =====================================================================================
    public class VerdantMaw : Boss
    {
        float attackT = 2.5f, rainT, ringOff;
        static readonly Color Leaf = new Color(0.55f, 1f, 0.4f), Venom = new Color(0.8f, 0.45f, 1f), SeedC = new Color(1f, 0.85f, 0.3f);
        public override bool Plant => true;

        public VerdantMaw(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("THE VERDANT MAW", "HEART OF THE JUNGLE", "verdantmaw", hp, 2.4f, new Vector2(1.5f, 1.5f), a, floor + Vector2.up * 1.51f);
            blood = new Color(0.5f, 0.95f, 0.35f);
            pos = new Vector2(a.CenterX, a.CeilY - half.y - 0.01f);   // always hanging from the middle of the ceiling
        }

        public override bool Hurt(float d, Vector2 knock) => base.Hurt(Burning ? d * 1.5f : d, knock);

        Vector2 Mouth => pos + Vector2.up * (half.y * -0.35f);

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt;
            vel = Vector2.zero;
            pos = new Vector2(arena.CenterX, arena.CeilY - half.y - 0.01f);
            FacePlayer();
            attackT -= dt;
            if (attackT <= 0f)
            {
                cycle++;
                attackT = Phase2 ? 1.7f : 2.4f;
                squash = -0.15f;
                switch (cycle % 4)
                {
                    case 0: VineLash(); attackT += 0.4f; break;
                    case 1: SeedBarrage(); break;
                    case 2: PlantSeeds(Phase2 ? 3 : 2); break;
                    default:
                        ringOff += 15f;
                        Ring(Mouth, Phase2 ? 16 : 11, 5f, ringOff, Art.Seed, Leaf);
                        if (Phase2) Ring(Mouth, 8, 3.2f, ringOff + 22f, Art.Seed, Leaf);
                        G.sfx.Play("shoot", 0.7f, 0.6f);
                        break;
                }
                if (cycle % 4 != 2 && Random.value < 0.35f) PlantSeeds(1);   // now and then a living seed with it
            }
            if (Phase2)
            {
                rainT -= dt;
                if (rainT <= 0f) { rainT = 1.5f; CeilingRain(2, Art.Seed, Venom); }
            }
            DrawBoss(-half.y);
            // hangs upside-down from the ceiling, swaying
            go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y + half.y), 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, 180f + Mathf.Sin(t * 1.3f) * 3f);
            sr.flipX = facing > 0;
        }

        // LIVING SEEDS: big golden seeds; where one strikes rock, a snapper grows out of that surface
        void PlantSeeds(int n)
        {
            int alive = 0;
            foreach (var e in G.ents) if (!e.dead && e is Snapper && Mathf.Abs(e.pos.y - pos.y) < 14f) alive++;
            for (int i = 0; i < n; i++)
            {
                Vector2 dir = U.Rot(ToPlayer, Random.Range(-35f, 35f));
                var s = new BossShot(Mouth, dir * 8f, Art.Seed, SeedC, 0f, i * 0.12f, 1.6f);
                if (alive + i < 6) s.onWall = (cell, normal) => Snapper.Grow(cell, normal, arena);
                G.Spawn(s);
            }
            G.sfx.Play("launcher", 0.6f, 0.9f);
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
    }
}
