using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // CAVE WORM: swims through rock and dirt like a dolphin through water. Underground it steers straight for
    // its prey; when it breaks out into open air it arcs through the cave and dives back in.
    // Its body is a chain of segments: shoot any of them.
    public class Worm : Enemy
    {
        readonly List<WormSegment> segs = new List<WormSegment>();
        readonly List<Vector2> trail = new List<Vector2>();
        readonly float speed, spacing;
        Vector2 dir;
        bool wasSolid = true;
        float wanderT, rumbleT;

        public Worm(int x, int y, int depth)
        {
            pos = new Vector2(x + 0.5f, y + 0.5f);
            half = new Vector2(0.34f, 0.34f);
            Setup("Worm", "wormhead", 7, depth);
            gold = 7;
            blood = new Color(0.9f, 0.55f, 0.5f);
            speed = 6.5f + Mathf.Min(depth, 400) / 200f;
            spacing = 0.55f;
            dir = Random.insideUnitCircle.normalized;
            int n = 5 + Mathf.Min(4, depth / 100);
            for (int i = 0; i < n; i++)
            {
                var s = new WormSegment(this, "wormseg", depth, 1f - i * 0.05f);
                segs.Add(s);
                G.Spawn(s);
            }
            for (int i = 0; i < 40; i++) trail.Add(pos);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(15f, false, dt);
            int cx = Mathf.FloorToInt(pos.x), cy = Mathf.FloorToInt(pos.y);
            bool solid = Wd.Solid(cx, cy);
            if (solid)
            {
                Vector2 want;
                if (HasTarget)
                {
                    Vector2 d = TPos - pos;
                    // swim underneath the prey, then burst up at it
                    want = Mathf.Abs(d.x) > 3f ? new Vector2(Mathf.Sign(d.x), d.y < -1f ? -0.3f : -0.6f).normalized : d.normalized;
                }
                else
                {
                    wanderT -= dt;
                    if (wanderT <= 0f) { wanderT = Random.Range(0.8f, 2f); dir = U.Rot(dir, Random.Range(-70f, 70f)); }
                    want = dir;
                }
                if (pos.y > -18f) want = new Vector2(want.x, -1f).normalized;          // stay below the camp
                if (pos.x < 4f) want.x = Mathf.Abs(want.x) + 0.5f;
                if (pos.x > World.W - 4f) want.x = -Mathf.Abs(want.x) - 0.5f;
                dir = Vector3.RotateTowards(dir, want.normalized, 170f * Mathf.Deg2Rad * dt, 0f);
                vel = dir * speed;
                if (Random.value < dt * 8f) G.fx.Burst(pos, 1, TileDraw.DebrisColor(Wd.Get(cx, cy), Biomes.At(cx, cy)), 2f, 0.4f, 10f);
                rumbleT -= dt;
                if (rumbleT <= 0f && HasTarget && (G.player.pos - pos).sqrMagnitude < 64f) { rumbleT = 0.9f; G.sfx.Play("rumble", 0.25f, 1.5f); }
            }
            else
            {
                // in the open: a falling arc, like a leaping dolphin
                bool wet = Wd.FluidAtPoint(pos) == Fluids.Water;
                vel.y -= (wet ? 8f : 26f) * dt;
                vel.y = Mathf.Max(vel.y, -22f);
                if (vel.sqrMagnitude > 0.01f) dir = vel.normalized;
            }
            if (solid != wasSolid)
            {
                Block b = solid ? Wd.Get(cx, cy) : Wd.Get(Mathf.FloorToInt(pos.x - vel.x * dt), Mathf.FloorToInt(pos.y - vel.y * dt));
                G.fx.Debris(pos, TileDraw.DebrisColor(b, Biomes.At(cx, cy)), 10);
                G.sfx.Material(b, true, 1.5f);
                if (!solid)
                {
                    vel = dir * speed * 1.65f;   // breaching: burst out of the rock
                    if ((G.player.pos - pos).sqrMagnitude < 150f) G.cam.Shake(0.15f);
                }
                wasSolid = solid;
            }
            // void blocks stop ordinary worms cold: they bounce off (only the Devourer boss tunnels through)
            {
                int ox = Mathf.FloorToInt(pos.x), oy = Mathf.FloorToInt(pos.y);
                int nx = Mathf.FloorToInt(pos.x + vel.x * dt), ny = Mathf.FloorToInt(pos.y + vel.y * dt);
                if (nx != ox && Wd.Get(nx, oy) == Block.Bedrock) { vel.x = -vel.x; dir.x = -dir.x; }
                if (ny != oy && Wd.Get(ox, ny) == Block.Bedrock) { vel.y = -vel.y * 0.6f; dir.y = -dir.y; }
                if (Wd.Get(Mathf.FloorToInt(pos.x + vel.x * dt), Mathf.FloorToInt(pos.y + vel.y * dt)) == Block.Bedrock) { vel = -vel; dir = -dir; }
            }
            pos += vel * dt;
            pos.x = Mathf.Clamp(pos.x, 2.4f, World.W - 2.4f);

            if ((trail[0] - pos).sqrMagnitude > 0.01f) trail.Insert(0, pos);
            int cap = Mathf.CeilToInt((segs.Count + 2) * spacing / 0.1f) + 4;
            while (trail.Count > cap) trail.RemoveAt(trail.Count - 1);
            PlaceSegments();

            Draw(0f);
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0, 0, ang);
            sr.flipX = false;
            sr.flipY = dir.x < 0f;
            if (solid) sr.color = new Color(0.45f, 0.4f, 0.4f, 0.55f);   // burrowing: a dim shape moving under the rock
        }

        void PlaceSegments()
        {
            float acc = 0f;
            int ti = 0;
            for (int s = 0; s < segs.Count; s++)
            {
                float want = (s + 1) * spacing;
                while (ti < trail.Count - 1 && acc + (trail[ti + 1] - trail[ti]).magnitude < want)
                {
                    acc += (trail[ti + 1] - trail[ti]).magnitude;
                    ti++;
                }
                Vector2 p = trail[Mathf.Min(ti, trail.Count - 1)];
                Vector2 prev = s == 0 ? pos : segs[s - 1].pos;
                segs[s].Follow(p, prev);
            }
        }

        public override void Die()
        {
            foreach (var s in segs) s.Burst();
            base.Die();
        }

        public override void Kill()
        {
            base.Kill();
            foreach (var s in segs) s.Kill();
        }
    }

    // One body segment of a worm (small worms and the Devourer). Damage to it goes to the head.
    public class WormSegment : Enemy
    {
        readonly Enemy owner;
        readonly float scale;
        public override bool Markable => false;

        public WormSegment(Enemy owner, string key, int depth, float scale)
        {
            this.owner = owner; this.scale = scale;
            pos = owner.pos;
            half = owner.half * scale * 0.9f;
            Setup("WormSegment", key, 999, depth);
            team = owner.team;
            dmg = owner.dmg;
            gold = 0;
            blood = owner.BloodColor;
            go.transform.localScale = Vector3.one * scale * (owner is Boss ? 2.2f : 1f);
            if (owner is Boss) half = owner.half * 0.75f * scale;
        }

        public override bool AlwaysActive => owner.AlwaysActive;

        public bool hidden;
        public void Follow(Vector2 p, Vector2 towards, bool show = true)
        {
            pos = p; hidden = !show;
            Vector2 d = towards - p;
            if (go == null) return;
            go.transform.position = new Vector3(U.Snap(p.x), U.Snap(p.y), 0f);
            if (d.sqrMagnitude > 0.0001f)
            {
                go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                if (owner is Boss) sr.flipY = d.x < 0f;   // upright either way, like the dragon's head
            }
            bool inRock = Wd.Solid(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
            sr.sprite = flash > 0f ? whites[0] : frames[0];
            sr.color = !show ? new Color(1f, 1f, 1f, 0f) : inRock ? new Color(0.45f, 0.4f, 0.4f, owner is Boss ? 0f : 0.5f) : Color.white;
        }

        // one blast catching several segments only hurts the worm once per frame
        static readonly Dictionary<Enemy, int> lastHit = new Dictionary<Enemy, int>();

        public override bool Hurt(float d, Vector2 knock)
        {
            if (dead || owner.dead) return false;
            flash = 0.08f;
            int f = Time.frameCount;
            if (lastHit.TryGetValue(owner, out int lf) && lf == f) return true;
            lastHit[owner] = f;
            if (lastHit.Count > 64) lastHit.Clear();
            return owner.TakeShot(d, knock * 0.25f);
        }

        public override void Tick(float dt)
        {
            flash -= dt; contactCd -= dt;
            if (owner.dead) { Kill(); return; }
            var p = G.player;
            bool inRock = Wd.Solid(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
            if (!inRock && !hidden && p.mode != Player.Mode.Dead && contactCd <= 0f && U.Overlap(pos, half * 0.85f, p.pos, Player.Half))
            {
                p.Hurt(dmg, pos);
                contactCd = 0.5f;
            }
        }

        // the head died: this piece bursts
        public void Burst()
        {
            if (dead) return;
            G.fx.Gibs(pos, Random.insideUnitCircle, blood, 3);
            Kill();
        }

        public override void Die() { }

        public override void AddLight(Lighting l) { }
    }
}
