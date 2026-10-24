using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Crossbow bolt: pierces enemies, punches through breakable blocks.
    public class Bolt : Entity
    {
        Vector2 vel;
        float life = 1.1f;
        int pierce;
        readonly float dmg;
        readonly bool crit;
        readonly HashSet<Entity> hit = new HashSet<Entity>();

        public Bolt(Vector2 p, Vector2 v, float dmg, int pierce, bool crit)
        {
            pos = p; vel = v; this.dmg = dmg; this.pierce = pierce; this.crit = crit;
            MakeGO("Bolt", Art.Bolt, 15);
            go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
            if (crit) sr.color = new Color(1f, 0.7f, 0.6f);
        }

        public override void Tick(float dt)
        {
            life -= dt;
            if (life <= 0f) { Kill(); return; }
            for (int s = 0; s < 3; s++)
            {
                pos += vel * dt / 3f;
                int cx = Mathf.FloorToInt(pos.x), cy = Mathf.FloorToInt(pos.y);
                if (Wd.Solid(cx, cy))
                {
                    Wd.Impact(cx, cy, 30f);
                    var r = Wd.Damage(cx, cy, G.player.s.digPower * 3, true, G.player.GoldMul);
                    G.fx.TileFlash(new Vector2Int(cx, cy));
                    if (r == World.Hit.Broke && pierce-- > 0)
                    {
                        G.sfx.Play("dig", 0.6f, 1.3f);
                        G.cam.Shake(0.06f);
                        continue;
                    }
                    G.fx.Sparks(pos - vel.normalized * 0.3f, new Color(1f, 0.9f, 0.6f));
                    G.sfx.Play("clank", 0.4f, 1.3f);
                    Kill();
                    return;
                }
                foreach (var e in G.ents)
                {
                    if (e.dead || hit.Contains(e)) continue;
                    if (e is Enemy en && U.Overlap(pos, new Vector2(0.25f, 0.15f), en.pos, en.half))
                    {
                        hit.Add(en);
                        en.TakeShot(dmg, vel.normalized * 7f);
                        G.fx.Blood(pos, vel.normalized, en.BloodColor, 6);
                        if (crit) G.Popup(en.pos + Vector2.up, "CRIT!", new Color(1f, 0.4f, 0.3f));
                        G.HitStop(0.03f);
                    }
                }
                foreach (var a in G.altars)
                {
                    if (!a.Active || hit.Contains(a)) continue;
                    if (U.Overlap(pos, new Vector2(0.25f, 0.15f), a.pos, a.Half)) { hit.Add(a); a.OnHit(Src.Bolt); }
                }
            }
            Place();
        }
    }

    // Dynamite, bombs, mega bombs and flares: bouncy thrown objects.
    public class Thrown : Entity
    {
        public enum Type { Dynamite, Bomb, Flare, Mega }
        readonly Type type;
        Vector2 vel;
        float fuse, life, spin;
        readonly float radius;
        bool stuck;
        static readonly Vector2 H = new Vector2(0.15f, 0.15f);

        public Thrown(Vector2 p, Vector2 v, Type type, float radius)
        {
            pos = p; vel = v; this.type = type; this.radius = radius;
            string key = type == Type.Dynamite ? "dynamite" : type == Type.Bomb ? "bomb" : type == Type.Mega ? "megabomb" : "flare";
            MakeGO(key, Art.Icons[key], 16);
            go.transform.localScale = Vector3.one * (type == Type.Mega ? 1.2f : 0.8f);
            fuse = type == Type.Dynamite ? 1.3f : 1.6f;
            life = 35f;
            if (type != Type.Flare) G.sfx.Play("fuse", 0.4f);
        }

        void Stick() { stuck = true; vel = Vector2.zero; G.sfx.Play("land", 0.3f, 2f); }

        public override void Tick(float dt)
        {
            if (type == Type.Flare && stuck)
            {
                life -= dt;
                if (Random.value < 0.25f) G.fx.Burst(pos + Vector2.up * 0.3f, 1, new Color(1f, 0.4f, 0.3f), 1.5f, 0.4f, -3f);
                if (life <= 0f) Kill();
                return;
            }
            vel.y = Mathf.Max(vel.y - 30f * dt, -18f);
            Vector2 p = pos;
            float spd = vel.magnitude;
            if (Wd.MoveX(ref p, H, vel.x * dt, out var hx)) { Wd.Impact(hx.x, hx.y, spd); if (type == Type.Flare) { pos = p; Stick(); return; } vel.x *= -0.45f; }
            if (Wd.MoveY(ref p, H, vel.y * dt, out var hy))
            {
                Wd.Impact(hy.x, hy.y, spd);
                if (type == Type.Flare) { pos = p; Stick(); return; }
                if (vel.y < 0f) { vel.y *= -0.35f; vel.x *= 0.7f; if (Mathf.Abs(vel.y) < 1.5f) vel.y = 0f; }
                else vel.y = 0f;
            }
            pos = p;
            spin += vel.x * dt * -120f;
            go.transform.rotation = Quaternion.Euler(0, 0, spin);

            if (type != Type.Flare)
            {
                fuse -= dt;
                if (Electric.Live(pos)) fuse = Mathf.Min(fuse, 0f);   // charged water sets bombs off
                if (Random.value < 0.3f) G.fx.Smoke(pos + Vector2.up * 0.2f, 1, 0.5f);
                if (type == Type.Dynamite)
                    foreach (var e in G.ents)
                        if (!e.dead && e is Enemy en && U.Overlap(pos, H, en.pos, en.half)) fuse = 0f;
                if (Random.value < 0.6f) G.fx.Burst(pos + Vector2.up * 0.3f, 1, new Color(1f, 0.85f, 0.3f), 2f, 0.15f, 0f);
                sr.color = fuse < 0.5f && ((int)(fuse * 20f)) % 2 == 0 ? new Color(1f, 0.4f, 0.4f) : Color.white;
                if (fuse <= 0f)
                {
                    Kill();
                    int dmg = type == Type.Mega ? 16 : type == Type.Bomb ? 12 : 6;   // blasts are risky, so they hit hard
                    G.Explode(pos, radius, dmg, false, true, true);   // your own bombs count as your blasts (SHRAPNEL PRISM, NAPALM)
                    if (type == Type.Mega) G.cam.Shake(1f);
                    return;
                }
            }
            Place();
        }

        public override void AddLight(Lighting l)
        {
            if (type == Type.Flare) l.Add(pos, 6.5f + Mathf.Sin(Time.time * 13f) * 0.25f, 1f);
            else l.Add(pos, 1.3f, 0.5f);
        }
    }

    // Grappling hook head. On hitting a wall it reels the player in.
    public class Hook : Entity
    {
        Vector2 vel;
        readonly Vector2 start;
        readonly Player owner;
        readonly float maxDist;
        readonly HashSet<Altar> touched = new HashSet<Altar>();

        public Hook(Vector2 p, Vector2 v, Player owner, float maxDist)
        {
            pos = p; start = p; vel = v; this.owner = owner; this.maxDist = maxDist;
            MakeGO("Hook", Art.Icons["grapple"], 17);
            go.transform.localScale = Vector3.one * 0.6f;
        }

        public override void Tick(float dt)
        {
            for (int s = 0; s < 3; s++)
            {
                pos += vel * dt / 3f;
                foreach (var a in G.altars)
                    if (a.Active && !touched.Contains(a) && U.Overlap(pos, new Vector2(0.2f, 0.2f), a.pos, a.Half))
                    {
                        touched.Add(a);
                        a.OnHit(Src.Hook);
                    }
                if (Wd.Solid(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y)))
                {
                    G.fx.Sparks(pos, new Color(1f, 0.9f, 0.6f));
                    G.cam.Shake(0.05f);
                    owner.StartGrapple(pos - vel.normalized * 0.45f);
                    Kill();
                    return;
                }
                foreach (var e in G.ents)
                    if (!e.dead && e is Enemy en && U.Overlap(pos, new Vector2(0.2f, 0.2f), en.pos, en.half))
                    {
                        en.Hurt(1, -vel.normalized * 6f);
                        Kill();
                        return;
                    }
            }
            if ((pos - start).magnitude > maxDist || owner.mode == Player.Mode.Dead) { Kill(); return; }
            owner.DrawRope(owner.pos, pos);
            Place();
        }
    }
}
