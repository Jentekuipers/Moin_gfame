using UnityEngine;

namespace DigDeep
{
    // BANDITS: the raiders' close-quarters crew. Three kinds, each with its own weapon in hand, so you know what's
    // coming before it swings:
    //   SWORDSMAN (red sash, a short sword): steady, a quick wind-up and a wide cut
    //   CHIEFTAIN (horned helm, a great axe): slow and heavy, a long wind-up, a huge chop that smashes blocks
    //   CUTTHROAT (hood, a dagger): fast, short reach, lunges in and stabs, then backs off
    // Once one spots you it runs after you: it jumps up ledges, hacks through blocks in its way (and digs up or down
    // to get at you), and swings when you're in reach. The colours follow the depth (four clans). They fight the
    // beasts too, like the raiders they come down with.
    public class Bandit : Enemy
    {
        public enum Kind { Sword, Axe, Dagger }
        readonly Kind kind;
        readonly float speed, reach, windup, cooldown, dmgMul, arc;
        readonly int digPower;
        float atkT = -1f, coolT, swingT = -1f, hopT, navT, digCd, lungeT, aggroT, dirTimer;
        int navDir; bool navOn, grounded, struck;
        readonly Transform weapon;
        readonly SpriteRenderer weaponSr;

        static readonly string[] Names = { "Swordsman", "Chieftain", "Cutthroat" };
        static readonly string[] Types = { "sword", "axe", "dagger" };

        // which clan (colours) lives at this depth
        static int Clan(int depth)
        {
            int bi = Biomes.Index(Mathf.Max(0, depth));
            return bi == Biomes.Topsoil ? 0 : bi == Biomes.Jungle || bi == Biomes.Fungal ? 2 : bi == Biomes.Crystal || bi == Biomes.Storm ? 1 : 3;
        }

        public static string KeyFor(Kind k, int depth) => "bandit_" + Types[(int)k] + "_" + Clan(depth);

        public Bandit(int x, int y, int depth, Kind k)
        {
            kind = k;
            team = Team.Bandit;
            half = k == Kind.Axe ? new Vector2(0.38f, 0.55f) : new Vector2(0.3f, 0.48f);
            pos = new Vector2(x + 0.5f, y + half.y + 0.002f);
            Setup(Names[(int)k], KeyFor(k, depth), k == Kind.Axe ? 9 : k == Kind.Dagger ? 4 : 6, depth);
            gold = k == Kind.Axe ? 7 : 5;
            blood = new Color(0.8f, 0.1f, 0.12f);
            float deep = 1f + depth / 700f;
            switch (k)
            {
                case Kind.Sword: speed = 2.7f * deep; reach = 1.25f; windup = 0.32f; cooldown = 0.95f; dmgMul = 1f; arc = 120f; digPower = 2; break;
                case Kind.Axe: speed = 1.9f * deep; reach = 1.6f; windup = 0.62f; cooldown = 1.6f; dmgMul = 1.7f; arc = 150f; digPower = 5; break;
                default: speed = 3.5f * deep; reach = 0.95f; windup = 0.16f; cooldown = 0.6f; dmgMul = 0.6f; arc = 70f; digPower = 1; break;
            }
            facing = Random.value < 0.5f ? -1 : 1;
            var g = new GameObject("BanditWeapon");
            g.transform.SetParent(G.runRoot, false);
            weaponSr = Art.AddSR(g, WeaponSprite(k), 11);
            weapon = g.transform;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            FindTarget(aggroT > 0f ? 13f : 9f, aggroT <= 0f, dt);   // once it's after you it keeps after you (round corners too)
            bool has = HasTarget;
            if (has && target == G.player) aggroT = 4f; else aggroT -= dt;
            vel.y = Mathf.Max(vel.y - 40f * dt, -16f);
            grounded = Wd.Ground(pos + new Vector2(0f, -0.05f), half);
            coolT -= dt; hopT -= dt; digCd -= dt; navT -= dt; lungeT -= dt;
            float want = 0f;

            if (has)
            {
                Vector2 d = TPos - pos;
                if (atkT < 0f && swingT < 0f) facing = d.x > 0f ? 1 : -1;
                bool inReach = Mathf.Abs(d.x) < reach + 0.35f && Mathf.Abs(d.y) < 1.2f;
                if (atkT < 0f && swingT < 0f)
                {
                    if (inReach && coolT <= 0f) StartAttack();
                    else
                    {
                        // run after it: straight at it, or round the rock by the ground path when that's shorter
                        if (navT <= 0f)
                        {
                            navT = 0.35f;
                            navOn = GroundNav.Step(new Vector2(pos.x, pos.y - half.y), TPos + Vector2.down * 0.4f, kind == Kind.Axe, out navDir, out _) && navDir != 0;
                        }
                        want = Mathf.Abs(d.x) < 0.3f ? 0f : (navOn ? navDir : Mathf.Sign(d.x));
                        if (kind == Kind.Dagger && coolT > 0.25f && Mathf.Abs(d.x) < 2.5f) want = -Mathf.Sign(d.x);   // a cutthroat darts back out after a stab
                        // up to it: jump (ledges, or it's above us)
                        if (grounded && hopT <= 0f && (d.y > 1.4f && Mathf.Abs(d.x) < 4f)) Jump(12.5f);
                        // dig to it: hack the block in the way, above or below
                        if (digCd <= 0f) DigToward(d, want);
                    }
                }
            }
            else
            {
                // wandering: pace about, turning at walls and edges
                dirTimer -= dt;
                if (dirTimer <= 0f) { dirTimer = Random.Range(1.5f, 3.5f); if (Random.value < 0.35f) facing = -facing; }
                want = facing * 0.4f;
            }
            if (atkT >= 0f || swingT >= 0f) want = 0f;

            if (drowning) { want = escapeDir; vel.y = U.Approach(vel.y, 4.5f, 60f * dt); }
            if (lungeT <= 0f) vel.x = U.Approach(vel.x, want * speed * WaterSlow, 22f * dt);
            Vector2 p = pos;
            if (Wd.MoveX(ref p, half, vel.x * dt, out var hx))
            {
                // a wall: hop it if it's low, hack through it if we're chasing, else turn round
                if (grounded && hopT <= 0f && want != 0f && TryHop(p)) { }
                else if (has && digCd <= 0f && hx.x != int.MinValue) { HackBlock(hx.x, hx.y); }
                else if (grounded && hopT <= 0f && want != 0f && has) Jump(10.5f);
                else if (!has) facing = -facing;
                vel.x = 0f;
            }
            if (Wd.MoveY(ref p, half, vel.y * dt, out _)) vel.y = 0f;
            pos = p;
            // wandering: don't walk off ledges
            if (!has && grounded)
            {
                int fx = Mathf.FloorToInt(pos.x + facing * (half.x + 0.1f)), fy = Mathf.FloorToInt(pos.y - half.y - 0.2f);
                if (!Wd.Solid(fx, fy) && !Wd.Solid(fx, fy - 1)) { facing = -facing; vel.x = 0f; }
            }

            TickAttack(dt);
            if (Mathf.Abs(vel.x) > 0.5f) animT += dt * 1.2f; else animT = 0f;   // legs only move when it walks
            Draw(-half.y);
            RenderWeapon();
        }

        void Jump(float v) { vel.y = v; hopT = 0.8f; }

        // the block between us and it: hack it (or dig up / down when it's above or below)
        void DigToward(Vector2 d, float want)
        {
            int cx = Mathf.FloorToInt(pos.x), feet = Mathf.FloorToInt(pos.y - half.y + 0.05f);
            if (d.y > 1.5f && Mathf.Abs(d.x) < 1.6f)
            {
                int head = Mathf.FloorToInt(pos.y + half.y + 0.1f);
                if (Wd.Solid(cx, head)) { HackBlock(cx, head); return; }
            }
            if (d.y < -1.5f && Mathf.Abs(d.x) < 1.2f && grounded)
            {
                if (Wd.Solid(cx, feet - 1)) { HackBlock(cx, feet - 1); return; }
            }
        }

        void HackBlock(int x, int y)
        {
            Block b = Wd.Get(x, y);
            var def = Tiles.Def(b);
            if (def.unbreakable || b == Block.Brick || b == Block.GildBrick || Wd.Anchored(x, y) || b == Block.Chest || b == Block.Crate || b == Block.Supply) { digCd = 0.8f; return; }
            digCd = kind == Kind.Axe ? 0.9f : kind == Kind.Dagger ? 0.4f : 0.6f;
            swingT = 0f; struck = true;   // a quick hack (no wind-up for rock)
            facing = x + 0.5f > pos.x ? 1 : x + 0.5f < pos.x - 0.1f ? -1 : facing;
            var r = Wd.Damage(x, y, digPower, false);
            G.fx.TileFlash(new Vector2Int(x, y));
            G.sfx.Material(b, r == World.Hit.Broke, 0.7f);
        }

        void StartAttack()
        {
            atkT = 0f; struck = false;
            G.sfx.Play("swing", 0.35f, kind == Kind.Axe ? 0.6f : kind == Kind.Dagger ? 1.5f : 1.05f, 0.05f);
        }

        void TickAttack(float dt)
        {
            if (atkT >= 0f)
            {
                atkT += dt;
                flash = Mathf.Max(flash, ((int)(atkT * 16f)) % 2 == 0 && atkT > windup * 0.5f ? 0.03f : 0f);   // blinks just before it swings
                if (atkT >= windup)
                {
                    atkT = -1f; swingT = 0f; struck = false;
                    if (kind == Kind.Dagger) { vel.x = facing * 7f; lungeT = 0.15f; }   // the cutthroat lunges into its stab
                    if (kind == Kind.Axe) vel.y = Mathf.Max(vel.y, 3f);
                }
            }
            if (swingT >= 0f)
            {
                swingT += dt;
                if (!struck && swingT > 0.05f) { struck = true; Strike(); }
                if (swingT > 0.22f) { swingT = -1f; coolT = cooldown; }
            }
        }

        // the swing lands: whatever is in front of it, in reach, gets hit
        void Strike()
        {
            Vector2 c = pos + new Vector2(facing * reach * 0.55f, 0.1f);
            Vector2 box = new Vector2(reach * 0.6f, 0.75f);
            float hit = Mathf.Max(1f, dmg * dmgMul);
            var pl = G.player;
            if (pl.mode != Player.Mode.Dead && U.Overlap(c, box, pl.pos, Player.Half) && Teams.Hostile(team, Team.Player))
            {
                Player.Cause = "A " + Names[(int)kind].ToUpperInvariant();
                pl.Hurt(Mathf.RoundToInt(hit), pos);
                pl.Launch(new Vector2(facing * (kind == Kind.Axe ? 9f : 5f), kind == Kind.Axe ? 6f : 3f));
            }
            foreach (var e in G.ents)
                if (!e.dead && e != this && e is Enemy en && !en.Intangible && Teams.Hostile(team, en.team) && U.Overlap(c, box, en.pos, en.half))
                    en.TakeShot(hit, new Vector2(facing * 5f, 3f));
            // the cut: a quick arc of sparks where the blade passes
            Color sc = kind == Kind.Axe ? new Color(1f, 0.75f, 0.5f) : new Color(1f, 0.95f, 0.88f);
            for (int i = 0; i < 5; i++)
            {
                float a = (60f - i * 30f) * Mathf.Deg2Rad;
                Vector2 at = pos + new Vector2(facing * Mathf.Cos(a), Mathf.Sin(a)) * reach * 0.85f;
                G.fx.Burst(at, 1, sc, 1.5f, 0.12f, 0f, kind == Kind.Axe ? 1.4f : 1f, false);
            }
            if (kind == Kind.Axe)
            {
                G.cam.Shake(0.15f);
                // the great axe bites into the rock it hits
                int bx = Mathf.FloorToInt(c.x + facing * 0.5f), by = Mathf.FloorToInt(pos.y - half.y + 0.4f);
                if (Wd.Solid(bx, by) && !Tiles.Def(Wd.Get(bx, by)).unbreakable) Wd.Damage(bx, by, digPower, false);
            }
            G.sfx.Play(kind == Kind.Axe ? "hit_big" : "swing", kind == Kind.Axe ? 0.5f : 0.4f, kind == Kind.Dagger ? 1.6f : 1f, 0.06f);
        }

        // the weapon in its hand: held ready, raised in the wind-up, swept through the swing
        void RenderWeapon()
        {
            if (weapon == null) return;
            float rest = kind == Kind.Dagger ? -20f : -35f;
            float ang = rest;
            if (atkT >= 0f) ang = Mathf.Lerp(rest, rest + arc * 0.7f, Mathf.Clamp01(atkT / windup));   // raised back over the shoulder
            else if (swingT >= 0f) ang = Mathf.Lerp(rest + arc * 0.7f, rest - arc * 0.45f, Mathf.Clamp01(swingT / 0.12f));   // and down through the cut
            Vector2 hand = pos + new Vector2(facing * (half.x * 0.7f), kind == Kind.Axe ? 0.05f : -0.02f);
            weapon.position = new Vector3(U.Snap(hand.x), U.Snap(hand.y), 0f);
            weapon.rotation = Quaternion.Euler(0f, 0f, facing > 0 ? ang : 180f - ang);
            weaponSr.flipY = facing < 0;
            weaponSr.color = sr != null ? sr.color : Color.white;
            weaponSr.sortingOrder = (atkT >= 0f ? 9 : 11);   // raised behind its head, swung in front
        }

        public override void Kill()
        {
            base.Kill();
            if (weapon != null) Object.Destroy(weapon.gameObject);
        }

        // ---------------------------------------------------------------- the weapons (pivot at the grip)
        static Sprite sword, axe, dagger;
        static Sprite WeaponSprite(Kind k)
        {
            if (k == Kind.Sword) return sword ??= Build(new[]
            {
                "..........o..",
                "oOo.ossssssso",
                "obgbSSSSSSSSs",
                "oOo.ossssssso",
            }, 1);
            if (k == Kind.Axe) return axe ??= Build(new[]
            {
                "..........ooooo.",
                ".........oSSSSso",
                "oooooooooosSSSso",
                "obbbbbbbbbbSSSSo",
                "oooooooooosSSSso",
                ".........oSSSSso",
                "..........ooooo.",
            }, 1);
            return dagger ??= Build(new[]
            {
                "oOo.oooo.",
                "obgbSSSso",
                "oOo.oooo.",
            }, 1);
        }

        static Sprite Build(string[] map, int gripX)
        {
            int h = map.Length, w = map[0].Length;
            var px = new Color32[w * h];
            for (int r = 0; r < h; r++)
                for (int c = 0; c < map[r].Length; c++)
                {
                    char ch = map[r][c];
                    Color32 col = ch switch
                    {
                        'o' => new Color32(24, 12, 34, 255),     // outline
                        'O' => new Color32(200, 160, 70, 255),   // the guard, brass
                        'b' => new Color32(110, 70, 40, 255),    // the grip, wood / leather
                        'g' => new Color32(240, 200, 90, 255),
                        's' => new Color32(150, 160, 175, 255),  // steel, shadowed
                        'S' => new Color32(215, 225, 235, 255),  // steel, lit
                        _ => new Color32(0, 0, 0, 0),
                    };
                    px[(h - 1 - r) * w + c] = col;
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2((gripX + 0.5f) / w, 0.5f), Art.PPU, 0, SpriteMeshType.FullRect);
        }
    }
}
