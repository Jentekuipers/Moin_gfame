using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // The diggers' own kits: the dolphin's tidal burst, the owl's shadow step and ambush, the cat's parry,
    // the octopus's camouflage and tentacles.
    public partial class Player
    {
        // ================================================================== TIDAL BURST (dolphin)

        void Tide()
        {
            const float r = 2.6f;
            int cx = Mathf.FloorToInt(pos.x), cy = Mathf.FloorToInt(pos.y);
            for (int y = cy - 3; y <= cy + 3; y++)
                for (int x = cx - 3; x <= cx + 3; x++)
                {
                    if ((new Vector2(x + 0.5f, y + 0.5f) - pos).magnitude > r) continue;
                    if (Wd.Solid(x, y) || Wd.LiquidType(x, y) == Fluids.Lava) continue;
                    Wd.SetLiquid(x, y, Fluids.Water, 1f);
                }
            foreach (var e in G.ents)
                if (!e.dead && e is Enemy en && !en.Intangible && (en.pos - pos).sqrMagnitude < (r + 0.8f) * (r + 0.8f))
                    en.TakeShot(Dmg(0.5f), (en.pos - pos).normalized * 9f + Vector2.up * 3f);
            G.fx.Burst(pos, 40, new Color(0.6f, 0.9f, 1f), 8f, 0.6f, 15f);
            G.fx.Ring(pos, r, new Color(0.6f, 0.9f, 1f));
            G.sfx.Play("splat", 0.8f, 0.7f);
            G.sfx.Play("land", 0.7f, 0.8f);
            G.cam.Shake(0.2f);
        }

        // ================================================================== SHADOW STEP (owl)

        // Vanish and reappear up to 6 blocks the way you hold (or the way you face). A decoy stays behind.
        bool Phase(float mx, bool up, bool down)
        {
            Vector2 dir = new Vector2(mx, up ? 1f : down ? -1f : 0f);
            if (dir.sqrMagnitude < 0.01f) dir = new Vector2(facing, 0f);
            dir.Normalize();
            Vector2 best = new Vector2(float.NaN, 0f);
            for (float t = 0.5f; t <= 6f; t += 0.25f)
            {
                Vector2 p = pos + dir * t;
                // nothing passes the void (or a sealed arena)
                if (Tiles.Def(Wd.Get(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y))).unbreakable) break;
                if (!Wd.BoxSolid(p, Half * 0.95f)) best = p;
            }
            if (float.IsNaN(best.x))
            {
                G.sfx.Play("deny", 0.5f);
                G.Popup(pos + Vector2.up, "BLOCKED", new Color(0.7f, 0.45f, 1f));
                return false;
            }
            G.Spawn(new Decoy(this, pos));
            G.fx.Burst(pos, 20, new Color(0.55f, 0.4f, 0.9f), 5f, 0.5f, 0f);
            G.fx.Smoke(pos, 6, 0.8f);
            pos = best;
            vel = dir * 4f;
            grace = 0.3f;
            G.fx.Burst(pos, 20, new Color(0.55f, 0.4f, 0.9f), 5f, 0.5f, 0f);
            G.fx.Ring(pos, 1.2f, new Color(0.7f, 0.5f, 1f));
            G.sfx.Play("dash", 0.7f, 1.6f);
            return true;
        }

        // AMBUSH: a hit on a foe's back (the side it's not facing) deals 1.5x
        float backstabPopT;
        public float Backstab(Enemy en)
        {
            if (!s.backstab || en == null) return 1f;
            if ((pos.x - en.pos.x) * en.facing >= 0f) return 1f;
            if (Time.time > backstabPopT) { backstabPopT = Time.time + 0.4f; G.Popup(en.pos + Vector2.up * 0.9f, "AMBUSH!", new Color(0.75f, 0.55f, 1f)); }
            return 1.5f;
        }

        // ================================================================== PARRY (black cat)

        float parryT, parryTotal;
        int parryHits;   // shots deflected / blows countered this parry
        public bool Parrying => mode == Mode.Parry;

        void StartParry()
        {
            mode = Mode.Parry;
            parryT = 1f; parryTotal = 0f; parryHits = 0;
            vel.x = 0f;
            CancelReload();
            G.sfx.Play("sword", 0.6f, 1.6f);
            G.fx.Ring(pos, 1.1f, new Color(0.9f, 0.9f, 1f));
        }

        void TickParry(float dt)
        {
            parryT -= dt; parryTotal += dt;
            // planted: no walking and no shooting until it's over
            vel.x = U.Approach(vel.x, 0f, 60f * dt);
            vel.y = Mathf.Max(vel.y - 58f * dt, -20f);
            MoveBody(dt, 0f);
            if (Random.value < 0.5f)
            {
                Vector2 o = Random.insideUnitCircle.normalized * 0.75f;
                G.fx.Burst(pos + o, 1, new Color(0.9f, 0.92f, 1f), 1f, 0.15f, 0f, 1f, false);
            }
            // after a successful parry, moving (or jumping) breaks out of it at once, with a short window of invincibility
            if (parryHits > 0 && (GameInput.Held(Btn.Left) || GameInput.Held(Btn.Right) || GameInput.Pressed(Btn.Jump)))
            {
                mode = Mode.Normal;
                invuln = Mathf.Max(invuln, 0.5f);
                G.fx.Burst(pos, 8, new Color(0.9f, 0.92f, 1f), 3f, 0.2f, 0f);
                G.sfx.Play("swing", 0.4f, 1.2f);
                return;
            }
            if (parryT <= 0f || parryTotal >= 2f)
            {
                mode = Mode.Normal;
                invuln = Mathf.Max(invuln, 0.8f);   // a short grace afterwards, as if you'd just been hit
                G.sfx.Play("swing", 0.4f, 0.8f);
            }
        }

        // every parried shot or countered blow stretches the parry (2 seconds at most)
        void ParryExtend()
        {
            parryT = Mathf.Min(parryT + 0.4f, 2f - parryTotal);
            parryHits++;
            G.HitStop(0.06f);
            G.cam.Shake(0.15f);
            G.fx.Ring(pos, 1.3f, new Color(1f, 1f, 1f));
            G.sfx.Play("reflect", 0.8f, 1.1f);
        }

        // a shot reaching you mid-parry flies straight back at whoever fired it
        public void ParryShot(Bullet b)
        {
            Vector2 back = b.vel.sqrMagnitude > 0.01f ? -b.vel.normalized : new Vector2(facing, 0f);
            b.Deflect(Team.Player, back);
            G.fx.Sparks(b.pos, new Color(0.85f, 0.95f, 1f));
            ParryExtend();
        }

        // a melee blow (anything that touches you) mid-parry gets a quick counter-slash
        void Counter(Vector2 from)
        {
            Vector2 dir = from - pos;
            dir = dir.sqrMagnitude < 0.01f ? new Vector2(facing, 0f) : dir.normalized;
            facing = dir.x >= 0f ? 1 : -1;
            float dmg = Dmg(4f) * s.meleeMul;
            foreach (var e in G.ents)
            {
                if (e.dead || !(e is Enemy en) || en.Intangible) continue;
                Vector2 v = en.pos - pos;
                if (v.magnitude > 2f || (v.magnitude > 0.8f && Vector2.Angle(dir, v) > 75f)) continue;
                if (en.TakeShot(dmg * Backstab(en), dir * 10f + Vector2.up * 3f)) OnWeaponHit(en, weapon, dmg, en.pos, dir);
            }
            var d = attack != null && attack.Melee ? attack : (AttackDef)DB.ById["dagger"];
            G.Spawn(new Slash(this, dir, d, false, 1));
            G.Popup(pos + Vector2.up * 1.1f, "COUNTER!", new Color(1f, 1f, 1f));
            ParryExtend();
        }

        // ================================================================== CAMOUFLAGE (octopus)

        public float camoT;
        public bool Camouflaged => camoT > 0f;

        void StartCamo()
        {
            camoT = 6f;
            G.fx.Smoke(pos, 8, 1f);
            G.fx.Burst(pos, 20, new Color(0.6f, 0.4f, 0.7f), 4f, 0.5f, 0f);
            G.sfx.Play("fizz", 0.4f, 1.6f);
        }

        // shooting, getting hurt or running out of time ends it
        public void EndCamo()
        {
            if (camoT <= 0f) return;
            camoT = 0f;
            G.fx.Burst(pos, 14, new Color(0.7f, 0.5f, 0.8f), 4f, 0.4f, 0f);
            G.sfx.Play("blip", 0.3f, 0.7f);
        }

        void TickCamo(float dt)
        {
            if (camoT <= 0f) return;
            camoT -= dt;
            if (camoT <= 0f) { camoT = 0.01f; EndCamo(); }
        }

        // ================================================================== TENTACLES (octopus, like the lukki mutation)

        const int NT = 4;
        const float Reach = 2.4f;
        readonly Vector2[] tentAnchor = new Vector2[NT], tentTip = new Vector2[NT];
        readonly bool[] gripping = new bool[NT];
        readonly Vector2Int[] gripCell = new Vector2Int[NT];
        readonly List<SpriteRenderer> tentSegs = new List<SpriteRenderer>();
        float seekT;
        const int Segs = 7;

        public bool TentacleGrip
        {
            get
            {
                if (!s.tentacles) return false;
                for (int i = 0; i < NT; i++) if (gripping[i]) return true;
                return false;
            }
        }

        void TickTentacles(float dt)
        {
            if (!s.tentacles || mode == Mode.Dead) return;
            for (int i = 0; i < NT; i++)
                if (gripping[i] && ((tentAnchor[i] - pos).magnitude > Reach * 1.15f || !Wd.Solid(gripCell[i].x, gripCell[i].y)))
                    gripping[i] = false;   // out of reach, or the block it held was dug away
            seekT -= dt;
            if (seekT <= 0f)
            {
                seekT = 0.08f;
                for (int i = 0; i < NT; i++)
                {
                    if (gripping[i]) continue;
                    // each tentacle feels around its own quarter (leaning the way you move) for the nearest surface
                    float baseA = 45f + 90f * i + (vel.sqrMagnitude > 1f ? Vector2.SignedAngle(Vector2.right, vel) * 0.3f : 0f);
                    float bestD = Reach; Vector2 hit = Vector2.zero, hitDir = Vector2.zero; bool found = false;
                    for (float a = -40f; a <= 40f; a += 10f)
                    {
                        Vector2 dir = U.Rot(Vector2.right, baseA + a);
                        float d = Wd.RayDist(pos, dir, Reach);
                        if (d < bestD - 0.01f) { bestD = d; hit = pos + dir * d; hitDir = dir; found = true; }
                    }
                    if (found)
                    {
                        Vector2 inside = hit + hitDir * 0.1f;
                        gripping[i] = true; tentAnchor[i] = hit;
                        gripCell[i] = new Vector2Int(Mathf.FloorToInt(inside.x), Mathf.FloorToInt(inside.y));
                    }
                }
            }
            for (int i = 0; i < NT; i++)
            {
                Vector2 rest = pos + U.Rot(Vector2.right, 45f + 90f * i) * 0.5f + Vector2.down * 0.2f;
                tentTip[i] = Vector2.MoveTowards(tentTip[i], gripping[i] ? tentAnchor[i] : rest, 22f * dt);
            }
        }

        void RenderTentacles()
        {
            if (!s.tentacles) return;
            if (tentSegs.Count == 0)
                for (int i = 0; i < NT * Segs; i++)
                {
                    Child("Tentacle", Art.Circle, 18, out var r);
                    tentSegs.Add(r);
                }
            bool vis = sr.enabled && mode != Mode.Dead;
            Color c = character.limb;
            if (Camouflaged) c.a = 0.2f;
            for (int i = 0; i < NT; i++)
            {
                Vector2 a = pos + new Vector2((i % 2 == 0 ? 0.12f : -0.12f), -0.15f);
                Vector2 b = tentTip[i];
                Vector2 mid = (a + b) * 0.5f + Vector2.down * 0.25f + new Vector2(-(b - a).y, (b - a).x).normalized * Mathf.Sin(Time.time * 6f + i * 1.7f) * 0.18f;
                for (int k = 0; k < Segs; k++)
                {
                    float t = (k + 1f) / Segs;
                    Vector2 p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * mid + t * t * b;   // a soft curve, like a limb
                    var r = tentSegs[i * Segs + k];
                    r.enabled = vis;
                    r.color = k == Segs - 1 && gripping[i] ? Color.Lerp(c, Color.white, 0.25f) : c;
                    r.transform.position = new Vector3(U.Snap(p.x), U.Snap(p.y), 0f);
                    float sz = Mathf.Lerp(0.3f, 0.13f, t) * s.bodyScale;
                    r.transform.localScale = new Vector3(sz, sz, 1f);
                }
            }
        }

        void DestroyTentacles()
        {
            foreach (var r in tentSegs) if (r != null) Object.Destroy(r.gameObject);
            tentSegs.Clear();
        }
    }

    // SHADOW STEP's decoy: a fading copy of the owl that every foe nearby goes after for 1.5 seconds.
    public class Decoy : Entity
    {
        public static Decoy Active;
        float life = 1.5f;

        public override bool AlwaysActive => true;

        public Decoy(Player p, Vector2 at)
        {
            pos = at;
            MakeGO("Decoy", p.character.frames[0], 19);
            sr.flipX = p.facing < 0;
            go.transform.position = new Vector3(U.Snap(at.x), U.Snap(at.y - Player.Half.y), 0f);
            Active = this;
        }

        public override void Tick(float dt)
        {
            life -= dt;
            if (life <= 0f) { G.fx.Smoke(pos, 5, 0.8f); Kill(); return; }
            sr.color = new Color(0.75f, 0.65f, 1f, 0.45f + 0.25f * Mathf.Sin(life * 20f));
        }

        public override void Kill()
        {
            if (Active == this) Active = null;
            base.Kill();
        }
    }
}
