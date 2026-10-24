using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // =====================================================================================
    // Boss arena: a brick hall sitting on a full-width bedrock floor at the end of each biome.
    // The only way further down is through the arena: walk in, it seals, beat the boss, the floor opens.
    // =====================================================================================

    public class ArenaRoom : Carve
    {
        public const int AW = 30, IH = 10;      // outer width, interior height
        public readonly int D, index, ax0;      // D = depth of the bedrock floor's top row

        public const int Final = 430;          // Ornin's throne room: the bottom of the world
        public const int Bottom = Final + 2;   // everything below is bedrock
        public bool IsFinal => D == Final;

        public static IEnumerable<int> Depths(int max)
        {
            yield return 62;     // end of Topsoil: the Mole King
            yield return 100;    // the Devourer, a giant worm
            yield return 126;    // end of the Overgrown Jungle: the Verdant Maw
            yield return 164;    // end of Fungal Hollows: the Spore Mother
            yield return 202;    // Captain Vask of the raiders
            yield return 240;    // end of Crystal Depths: the Crystal Colossus
            yield return 290;    // the Ash Wraith
            yield return 332;    // end of Magma Core: the Cinder Lord
            yield return Final;  // King Ornin, guarding the God-Diamond
        }

        // 3-5 unbreakable one-block ledges to dodge onto. Bosses pass straight through them.
        readonly HashSet<long> plats = new HashSet<long>();

        public ArenaRoom(int depth, int index)
        {
            D = depth; this.index = index;
            ax0 = (Gen.W - AW) / 2;
            rects.Add(new RectInt(2, D - IH - 1, Gen.W - 4, IH + 3));

            var r = new System.Random(Gen.Seed * 31 + index * 7919 + 5);
            int n = 3 + r.Next(3);
            var xs = new List<int>();
            for (int tries = 0; xs.Count < n && tries < 200; tries++)
            {
                int x = ax0 + 3 + r.Next(AW - 6);
                if (Mathf.Abs(x - (ax0 + AW / 2)) < 2) continue;   // keep the middle clear
                bool ok = true;
                foreach (int o in xs) if (Mathf.Abs(o - x) < 4) { ok = false; break; }
                if (ok) xs.Add(x);
            }
            xs.Sort();
            for (int i = 0; i < xs.Count; i++)
            {
                // most ledges are an easy hop (top 2 blocks up); one next to a low ledge can sit higher
                int h = 2;
                bool lowNear = i > 0 && xs[i] - xs[i - 1] <= 5 && plats.Contains(Key(xs[i - 1], D - 2));
                if (lowNear && r.Next(2) == 0) h = 4;
                plats.Add(Key(xs[i], D - h));
            }
        }

        static long Key(int x, int d) => ((long)x << 32) ^ (uint)d;

        public override int AnchorDepth => D - 1;

        public override int TileAt(int x, int d)
        {
            if (d == D || d == D + 1) return (x >= 2 && x <= Gen.W - 3) ? (int)Block.Bedrock : -1;
            if (x < ax0 || x > ax0 + AW - 1) return -1;
            if (d == D - IH - 1) return (int)Block.Brick;                       // ceiling
            if (d >= D - IH && d <= D - 1)
            {
                if (x == ax0 || x == ax0 + AW - 1) return (int)Block.Brick;
                return plats.Contains(Key(x, d)) ? (int)Block.Plate : (int)Block.Air;
            }
            return -1;
        }

        public override void Spawn(Game g, System.Random r) => g.Spawn(new BossArena(this));
    }

    public class BossArena : Entity
    {
        readonly ArenaRoom room;
        public int Depth => room.D;
        public int Index => room.index;
        public float FloorY => 1 - room.D;                 // world y of the floor surface
        public float CeilY => 1 - room.D + ArenaRoom.IH;   // world y of the ceiling's underside
        public float MinX => room.ax0 + 1;
        public float MaxX => room.ax0 + ArenaRoom.AW - 1;
        public float CenterX => room.ax0 + ArenaRoom.AW * 0.5f;
        bool started, cleared;
        Boss boss;
        readonly List<Boss> bosses = new List<Boss>();
        public bool Started => started;
        public bool Active => started && !cleared;
        public static int ForceKind = -1;   // TEST MODE: the next arena fights this boss (see BossKinds)

        // every boss the arenas can hold (also the test mode's list)
        public static readonly string[] BossKinds = { "MOLE KING", "SPORE MOTHER", "CRYSTAL COLOSSUS", "CINDER LORD", "KING ORNIN", "THE DEVOURER",
            "CAPTAIN VASK", "ASH WRAITH", "VERDANT MAW", "UNDA, WATER DRAGON", "IGNIS, FIRE DRAGON", "IGNIS AND UNDA (BOTH)",
            "BOULDER TROLL", "HIVE QUEEN", "TOADSTOOL KING", "FORGE GOLEM" };
        public bool Cleared => cleared;
        public void MarkCleared() { started = true; cleared = true; }   // (restoring a saved run)

        public override bool AlwaysActive => true;

        public bool IsFinal => room.IsFinal;

        public BossArena(ArenaRoom room)
        {
            this.room = room;
            pos = new Vector2(CenterX, FloorY + ArenaRoom.IH * 0.5f);
            if (room.IsFinal) G.Spawn(new GodDiamond(this));
        }

        public override void Tick(float dt)
        {
            var p = G.player;
            if (p.mode == Player.Mode.Dead) return;
            // on the way up to the gods (and on the test road to them) every hall counts as beaten: nothing seals you in
            if ((Ascent.Flying || (TestMode.On && TestMode.PretendAllDiamonds)) && (!started || !cleared))
            {
                if (started && !cleared) SetPerimeter(Block.Brick);
                started = true; cleared = true;
                if (G.boss == boss) G.boss = null;
                return;
            }
            if (!started)
            {
                bool inside = p.pos.x > MinX + 0.4f && p.pos.x < MaxX - 0.4f && p.pos.y > FloorY && p.pos.y < CeilY - 0.6f;
                if (inside) { Begin(); return; }
                float dy = p.pos.y - CeilY;
                if (dy > 0f && dy < 14f && Mathf.Abs(p.pos.x - CenterX) < 18f)
                    G.hud.Label(new Vector2(CenterX, CeilY + 1.6f), room.IsFinal ? "THE THRONE OF ORNIN. THE GOD-DIAMOND SHINES BELOW" : "THE GROUND TREMBLES... SOMETHING BIG WAITS BELOW",
                        room.IsFinal ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 0.5f, 0.45f));
                if (p.grounded && p.pos.y < FloorY + 1.2f && p.pos.y > FloorY - 0.5f && (p.pos.x < MinX - 1f || p.pos.x > MaxX + 1f))
                    G.hud.Label(p.pos + Vector2.up * 1.5f, "UNBREAKABLE VOID. THE WAY DOWN IS THROUGH THE ARENA", new Color(0.8f, 0.75f, 0.7f));
            }
            if (started && !cleared && Random.value < 0.3f)
                foreach (var t in Torches()) G.fx.Burst(t, 1, new Color(1f, 0.6f, 0.2f), 1f, 0.35f, -4f, 1f, false);
            if (started && !cleared) Confine(p);
        }

        // Once the doors seal there is no way out: no dash, blink, grapple, super jump or teleport leaves the hall.
        void Confine(Player p)
        {
            float x0 = MinX + Player.Half.x + 0.02f, x1 = MaxX - Player.Half.x - 0.02f;
            float y0 = FloorY + Player.Half.y + 0.002f, y1 = CeilY - Player.Half.y - 0.02f;
            if (p.pos.x < x0) { p.pos.x = x0; if (p.vel.x < 0f) p.vel.x = 0f; }
            if (p.pos.x > x1) { p.pos.x = x1; if (p.vel.x > 0f) p.vel.x = 0f; }
            if (p.pos.y < y0) { p.pos.y = y0; if (p.vel.y < 0f) p.vel.y = 0f; }
            if (p.pos.y > y1) { p.pos.y = y1; if (p.vel.y > 0f) p.vel.y = 0f; }
        }

        IEnumerable<Vector2> Torches()
        {
            yield return new Vector2(MinX + 0.5f, FloorY + 4f);
            yield return new Vector2(MaxX - 0.5f, FloorY + 4f);
            yield return new Vector2(room.ax0 + ArenaRoom.AW / 3f, CeilY - 0.6f);
            yield return new Vector2(room.ax0 + ArenaRoom.AW * 2f / 3f, CeilY - 0.6f);
        }

        void SetPerimeter(Block b)
        {
            int ceilD = room.D - ArenaRoom.IH - 1;
            for (int x = room.ax0; x <= room.ax0 + ArenaRoom.AW - 1; x++) Wd.Set(x, -ceilD, b);
            for (int d = room.D - ArenaRoom.IH; d <= room.D - 1; d++)
            {
                Wd.Set(room.ax0, -d, b);
                Wd.Set(room.ax0 + ArenaRoom.AW - 1, -d, b);
            }
        }

        void Begin()
        {
            started = true;
            SetPerimeter(Block.Seal);
            int kind;
            switch (room.D)
            {
                case 62: kind = 0; break;
                case 100: kind = 5; break;
                case 126: kind = 8; break;
                case 164: kind = 1; break;
                case 202: kind = 2; break;   // the Crystal Colossus guards the crystal depths
                case 240: kind = 6; break;   // the Raider Captain holds the storm caverns
                case 290: kind = 7; break;
                default: kind = 3; break;
            }
            float hpMul = 2f * (0.8f + room.D / 400f);   // twice as tough as before, and tougher the deeper the arena
            var spawnAt = new Vector2(CenterX + (G.player.pos.x < CenterX ? 7f : -7f), FloorY);
            if (room.IsFinal) kind = 4;
            // the dragon worms: the water dragon may rule the crystal depths, the fire dragon the storm caverns;
            // in harder runs (after you've broken the curse) both can turn up together
            var rr = new System.Random(Gen.Seed * 13 + room.D);
            int wins = PlayerPrefs.GetInt("digdeep_wins", 0);
            float twin = Mathf.Min(0.6f, 0.12f + 0.2f * wins);
            if (room.D == 202 && rr.NextDouble() < 0.5) kind = rr.NextDouble() < twin ? 11 : 9;
            if (room.D == 240 && rr.NextDouble() < 0.5) kind = rr.NextDouble() < twin ? 11 : 10;
            // every biome's second boss: half the time the hall holds that one instead
            if (room.D == 62 && rr.NextDouble() < 0.5) kind = 12;
            if (room.D == 126 && rr.NextDouble() < 0.5) kind = 13;
            if (room.D == 164 && rr.NextDouble() < 0.5) kind = 14;
            if (room.D == 332 && rr.NextDouble() < 0.5) kind = 15;
            if (ForceKind >= 0) { kind = ForceKind; ForceKind = -1; }
            switch (kind)
            {
                case 4: boss = new OrninBoss(this, spawnAt, 720f * hpMul); break;
                case 0: boss = new MoleKing(this, spawnAt, 90f * hpMul); break;
                case 1: boss = new SporeMother(this, spawnAt, 165f * hpMul); break;
                case 2: boss = new CrystalColossus(this, spawnAt, 255f * hpMul); break;
                case 5: boss = new Devourer(this, spawnAt, 140f * hpMul); break;
                case 6: boss = new RaiderCaptain(this, spawnAt, 220f * hpMul); break;
                case 7: boss = new AshWraith(this, spawnAt, 300f * hpMul); break;
                case 8: boss = new VerdantMaw(this, new Vector2(CenterX, FloorY), 190f * hpMul); break;
                case 9: boss = new Dragon(this, Dragon.Elem.Water, 250f * hpMul, false); break;
                case 10: boss = new Dragon(this, Dragon.Elem.Fire, 260f * hpMul, false); break;
                case 12: boss = new BoulderTroll(this, spawnAt, 95f * hpMul); break;
                case 13: boss = new HiveQueen(this, spawnAt, 185f * hpMul); break;
                case 14: boss = new ToadstoolKing(this, spawnAt, 170f * hpMul); break;
                case 15: boss = new ForgeGolem(this, spawnAt, 370f * hpMul); break;
                case 11:
                    {
                        var f = new Dragon(this, Dragon.Elem.Fire, 175f * hpMul, true);
                        var w = new Dragon(this, Dragon.Elem.Water, 170f * hpMul, true);
                        G.Spawn(new DragonDuo(this, f, w));
                        G.Spawn(w);
                        bosses.Add(w);
                        G.boss2 = w;
                        boss = f;
                        break;
                    }
                default: boss = new CinderLord(this, spawnAt, 360f * hpMul); break;
            }
            G.Spawn(boss);
            bosses.Add(boss);
            G.boss = boss;
            G.music.PlayBoss();
            if (kind == 11) G.hud.Banner("IGNIS AND UNDA", "THE TWIN DRAGONS", new Color(1f, 0.35f, 0.3f));
            else G.hud.Banner(boss.title, boss.subtitle, new Color(1f, 0.35f, 0.3f));
            G.sfx.Play("rumble", 1f, 0.7f);
            G.sfx.Play("explode", 0.6f, 0.5f);
            G.cam.Shake(0.6f);
            G.lighting.Flash(pos, 16f, 0.5f);
        }

        public void OnBossDefeated()
        {
            if (cleared) return;
            // two bosses: the fight goes on until both are down
            foreach (var b in bosses)
                if (!b.dead) { G.boss = b; G.boss2 = null; G.hud.Banner("ONE DOWN", "THE OTHER FIGHTS ON", new Color(1f, 0.6f, 0.4f)); return; }
            cleared = true;
            if (G.boss == boss) G.boss = null;
            G.boss2 = null;
            SetPerimeter(Block.Brick);
            if (room.IsFinal)
            {
                if (G.diamond != null) G.diamond.Unlock();
                G.hud.Banner("ORNIN FALLS", "THE GOD-DIAMOND IS FREE", new Color(0.6f, 0.9f, 1f));
                G.music.Stop(4f);
                G.cam.Shake(1f);
                return;
            }
            int cx = Mathf.FloorToInt(CenterX);
            for (int x = cx - 1; x <= cx + 1; x++)
                for (int d = room.D; d <= room.D + 1; d++) Wd.Set(x, -d, Block.Air);
            var c = new Vector2(CenterX, FloorY + 3f);
            // some bosses carry something of their own (the dragons' hearts) instead of the usual reward
            int own = 0;
            foreach (var b in bosses)
            {
                var dd = b.Drop;
                if (dd == null) continue;
                G.Spawn(new LootPickup(c + Vector2.right * (own * 1.5f - 0.75f * (bosses.Count - 1)), dd, 0.8f, true));
                own++;
            }
            if (own > 0) { G.sfx.Play("chest", 0.9f, 0.9f); G.fx.Burst(c, 40, new Color(1f, 0.85f, 0.3f), 9f, 0.9f, 4f); G.fx.Ring(c, 3f, new Color(1f, 0.85f, 0.3f)); }
            else G.RareReward(c);
            G.Spawn(new HealOrb(c + Vector2.right * 2f, 100));
            G.hud.Banner("VICTORY!", "THE FLOOR CRACKS OPEN", new Color(1f, 0.85f, 0.3f));
            G.music.PlayLevel(3f);
            G.cam.Shake(0.8f);
        }

        public override void AddLight(Lighting l)
        {
            if (!started && !cleared)
            {
                // a faint glow leaking through the ceiling so you can find it
                l.Add(new Vector2(CenterX, CeilY + 0.5f), 3f, 0.35f);
                return;
            }
            float f = 1f + Mathf.Sin(Time.time * 9f) * 0.05f;
            foreach (var t in Torches()) l.Add(t, 5f * f, 0.85f);
        }
    }

    // =====================================================================================
    // Boss projectile: rocks, spores, fire, crystal shards, floor shockwaves.
    // =====================================================================================

    public class BossShot : Entity
    {
        Vector2 vel;
        readonly float grav, rad;
        float delay, life = 7f, spin;
        readonly Color col;
        public bool poison;   // jungle seeds poison you
        public bool chills, burns;   // frost shards slow you, embers set you alight
        public int dmgOverride = -1; // (the God above the clouds hits harder than the depth says)
        public Color body = Color.white;   // the shot's own tint once it flies

        public override bool AlwaysActive => true;

        public BossShot(Vector2 p, Vector2 v, Sprite s, Color c, float grav = 0f, float delay = 0f, float scale = 1f)
        {
            pos = p; vel = v; this.grav = grav; this.delay = delay; col = c;
            rad = 0.24f * scale;
            MakeGO("BossShot", s, 15);
            go.transform.localScale = Vector3.one * scale;
        }

        public override void Tick(float dt)
        {
            if (delay > 0f)
            {
                delay -= dt;
                sr.color = ((int)(delay * 14f)) % 2 == 0 ? Color.white : col;
                if (Random.value < 0.3f) G.fx.Burst(pos + Vector2.down * 0.3f, 1, col, 1f, 0.4f, 20f);
                Place();
                return;
            }
            sr.color = body;
            life -= dt;
            vel.y -= grav * dt;
            pos += vel * dt;
            spin += dt * 400f;
            go.transform.rotation = Quaternion.Euler(0, 0, spin);
            if (life <= 0f || Wd.Solid(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y)))
            {
                G.fx.Burst(pos, 8, col, 4f, 0.35f, 15f);
                Kill();
                return;
            }
            var pl = G.player;
            if (pl.mode != Player.Mode.Dead && U.Overlap(pos, new Vector2(rad, rad), pl.pos, Player.Half))
            {
                pl.Hurt(dmgOverride >= 0 ? dmgOverride : Enemy.ContactDmg(-Mathf.FloorToInt(pos.y)), pos);
                if (poison) pl.Poison(3f);
                if (chills) pl.Chill(2.5f);
                if (burns) pl.SetOnFire();
                G.fx.Burst(pos, 10, col, 5f, 0.35f, 10f);
                Kill();
                return;
            }
            if (Random.value < 0.35f) G.fx.Burst(pos, 1, col, 0.6f, 0.25f, 0f, 1f, false);
            Place();
        }

        public override void AddLight(Lighting l) => l.Add(pos, 1.5f, 0.65f);
    }

    // =====================================================================================
    // Boss base
    // =====================================================================================

    public abstract class Boss : Enemy
    {
        public string title, subtitle;
        protected BossArena arena;
        protected float scale = 1f, squash, stateT, t;
        protected Color tint = Color.white;
        protected int state, cycle;
        protected bool grounded;

        public override bool AlwaysActive => true;
        protected bool Phase2 => hp < maxHp * 0.5f;
        protected static Player P => Game.I.player;
        public float HpFrac => Mathf.Clamp01(hp / maxHp);
        public virtual Def Drop => null;   // a boss's own drop, replacing the usual reward
        protected virtual void OnDefeatedFree() { }   // a boss outside any arena (the God above the clouds)

        protected void SetupBoss(string nm, string sub, string key, float hpVal, float sc, Vector2 h, BossArena a, Vector2 at)
        {
            arena = a;
            half = h;
            pos = at;
            Setup(nm, key, 1, a.Depth);
            team = Team.Boss;
            maxHp = hp = hpVal;
            scale = sc;
            title = nm;
            subtitle = sub;
            gold = 40 + 25 * a.Index;
            sr.sortingOrder = 12;
            go.transform.localScale = new Vector3(sc, sc, 1f);
            blood = new Color(1f, 0.4f, 0.3f);
        }

        public override bool Hurt(float d, Vector2 knock)
        {
            bool r = base.Hurt(d, Vector2.zero);
            if (r)
            {
                squash = 0.08f;
                G.player.BonusEnergy(d * 0.06f);   // fighting a boss slowly recharges your special
            }
            return r;
        }

        public override void Die()
        {
            if (dead) return;
            base.Die();
            G.fx.Explosion(pos, 2.5f);
            G.fx.Burst(pos, 60, new Color(1f, 0.85f, 0.3f), 10f, 1.2f, 10f, 2f);
            G.sfx.Play("explode", 1f, 0.6f);
            G.HitStop(0.25f);
            ClearAttacks();
            if (arena != null) arena.OnBossDefeated();
            else OnDefeatedFree();
        }

        // The boss is dead: every shot, warning zone and summoned skull it left behind fizzles out.
        void ClearAttacks()
        {
            foreach (var e in G.ents)
            {
                if (e.dead) continue;
                if (e is ArenaWater aw) { aw.Drain(); continue; }   // the flood drains away
                bool mine = e is BossShot || e is Hazard || e is BeamStrike || e is HomingSkull || e is FloorFire || (e is Bullet b && b.team == Team.Boss) || (this is VerdantMaw && e is Snapper) || (this is HiveQueen && e is Bee) || e is SlugEmber;
                if (!mine) continue;
                G.fx.Burst(e.pos, 4, new Color(1f, 0.9f, 0.7f, 0.8f), 2f, 0.3f, 0f, 1f, false);
                e.Kill();
            }
        }

        // gravity walker physics inside the arena
        protected bool Move(float dt, bool gravity = true)
        {
            if (gravity) vel.y = Mathf.Max(vel.y - 40f * dt, -20f);
            Vector2 p = pos;
            World.GhostPlates = true;   // the dodge ledges are for the player: bosses pass through them
            bool hitX = Wd.MoveX(ref p, half, vel.x * dt, out _);
            if (hitX) vel.x = 0f;
            bool hitY = Wd.MoveY(ref p, half, vel.y * dt, out _);
            if (hitY) vel.y = 0f;
            pos = p;
            grounded = Wd.BoxSolid(pos + new Vector2(0f, -0.05f), half);
            World.GhostPlates = false;
            return hitX;
        }

        protected void FacePlayer() { facing = P.pos.x > pos.x ? 1 : -1; }
        protected Vector2 ToPlayer => (P.pos - pos).normalized;

        protected BossShot Shot(Vector2 p, Vector2 v, Sprite s, Color c, float grav = 0f, float delay = 0f, float sc = 1f)
        {
            var b = new BossShot(p, v, s, c, grav, delay, sc);
            G.Spawn(b);
            return b;
        }

        protected void Ring(Vector2 c, int n, float speed, float offsetDeg, Sprite s, Color col)
        {
            for (int i = 0; i < n; i++)
            {
                float a = (offsetDeg + i * 360f / n) * Mathf.Deg2Rad;
                Shot(c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed, s, col);
            }
        }

        protected void FloorWave(int dir, Color col)
            => Shot(new Vector2(pos.x + dir * (half.x + 0.3f), arena.FloorY + 0.35f), new Vector2(dir * 9f, 0f), Art.Fireball, col, 0f, 0f, 1.3f);

        protected void CeilingRain(int n, Sprite s, Color col)
        {
            for (int i = 0; i < n; i++)
            {
                float x = Random.Range(arena.MinX + 0.6f, arena.MaxX - 0.6f);
                Shot(new Vector2(x, arena.CeilY - 0.4f), Vector2.zero, s, col, 32f, 0.55f + Random.value * 0.5f, 0.6f);
            }
        }

        protected void DrawBoss(float yOff)
        {
            Draw(yOff);
            if (dead) return;
            squash = Mathf.MoveTowards(squash, 0f, Time.deltaTime * 1.5f);
            go.transform.localScale = new Vector3(scale * (1f + squash), scale * (1f - squash), 1f);
            sr.color = flash > 0f ? Color.white : (Phase2 ? Color.Lerp(tint, new Color(1f, 0.55f, 0.5f), 0.35f + 0.15f * Mathf.Sin(t * 10f)) : tint);
        }

        public override void AddLight(Lighting l) => l.Add(pos, 3f, 0.6f);
    }

    // =====================================================================================
    // 1. THE MOLE KING (end of Topsoil): charges, leaps with rock showers, burrows and erupts under you.
    // =====================================================================================

    public class MoleKing : Boss
    {
        enum S { Walk, Leap, Air, Sink, Under, Erupt, Curl, Roll }
        float targetX, spin;
        int rollBounces;
        static Sprite rock;

        public MoleKing(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("THE MOLE KING", "TYRANT OF THE TOPSOIL", "mole", hp, 2.6f, new Vector2(1.1f, 0.8f), a, floor + Vector2.up * 0.81f);
            if (rock == null) rock = Art.TileSprite(Block.Boulder, Biomes.All[0]);
            Go(S.Walk, 1.5f);
        }

        void Go(S s, float time) { state = (int)s; stateT = time; }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            switch ((S)state)
            {
                case S.Walk:
                    FacePlayer();
                    vel.x = U.Approach(vel.x, facing * (Phase2 ? 3.8f : 2.7f), 20f * dt);
                    Move(dt);
                    if (stateT <= 0f)
                    {
                        cycle++;
                        vel.x = 0f;
                        int k = cycle % 3;
                        if (k == 1) Go(S.Leap, 0.25f);
                        else if (k == 2) Go(S.Sink, 0.5f);
                        else { Go(S.Curl, Phase2 ? 0.6f : 0.85f); G.sfx.Play("rumble", 0.9f, 1.4f); }
                    }
                    break;
                case S.Leap:   // crouch, then jump at the player
                    squash = 0.2f;
                    vel.x = 0f;
                    Move(dt);
                    if (stateT <= 0f)
                    {
                        FacePlayer();
                        vel = new Vector2(Mathf.Clamp(P.pos.x - pos.x, -9f, 9f) * 0.9f, 16f);
                        G.sfx.Play("jump", 0.8f, 0.5f);
                        Go(S.Air, 3f);
                    }
                    break;
                case S.Air:
                    Move(dt);
                    if (grounded && vel.y <= 0f && stateT < 2.9f) Land();
                    break;
                case S.Sink:   // dig into the floor
                    intangible = stateT < 0.25f;
                    squash = Mathf.Lerp(0f, 0.5f, 1f - stateT / 0.5f);
                    if (Random.value < 0.6f) G.fx.Burst(pos + Vector2.down * half.y, 2, new Color(0.55f, 0.4f, 0.25f), 5f, 0.4f, 25f);
                    if (stateT <= 0f)
                    {
                        sr.enabled = false;
                        intangible = true;
                        targetX = pos.x;
                        G.sfx.Play("rumble", 0.9f);
                        Go(S.Under, Phase2 ? 1.1f : 1.6f);
                    }
                    break;
                case S.Under:  // a trail of dirt follows you... then stops
                    {
                        bool locked = stateT < 0.35f;
                        if (!locked) targetX = Mathf.MoveTowards(targetX, P.pos.x, (Phase2 ? 9f : 6.5f) * dt);
                        targetX = Mathf.Clamp(targetX, arena.MinX + half.x, arena.MaxX - half.x);
                        if (Random.value < (locked ? 1f : 0.5f))
                            G.fx.Burst(new Vector2(targetX + Random.Range(-0.6f, 0.6f), arena.FloorY + 0.1f), locked ? 3 : 1, new Color(0.6f, 0.45f, 0.3f), locked ? 6f : 3f, 0.35f, 25f);
                        if (locked) G.cam.Shake(0.05f);
                        if (stateT <= 0f)
                        {
                            pos = new Vector2(targetX, arena.FloorY + half.y + 0.01f);
                            sr.enabled = true;
                            intangible = false;
                            vel = new Vector2(0f, 14f);
                            squash = -0.3f;
                            G.sfx.Play("explode", 0.7f, 0.8f);
                            G.cam.Shake(0.35f);
                            G.fx.Burst(pos, 30, new Color(0.6f, 0.45f, 0.3f), 9f, 0.6f, 25f, 2f);
                            int n = Phase2 ? 7 : 5;
                            for (int i = 0; i < n; i++)
                                Shot(pos + Vector2.up * 0.5f, new Vector2(Mathf.Lerp(-6f, 6f, i / (float)(n - 1)), 12f + Random.value * 3f), rock, new Color(0.6f, 0.5f, 0.4f), 28f, 0f, 0.55f);
                            Go(S.Erupt, 3f);
                        }
                        break;
                    }
                case S.Erupt:
                    Move(dt);
                    if (grounded && vel.y <= 0f && stateT < 2.9f) Land();
                    break;
                case S.Curl:   // tucks into a ball, shaking and kicking up dirt: a roll is coming. Jump it!
                    FacePlayer();
                    vel.x = 0f;
                    Move(dt);
                    squash = 0.18f + Mathf.Sin(t * 45f) * 0.06f;
                    flash = ((int)(stateT * 12f)) % 2 == 0 ? 0.05f : 0f;
                    if (Random.value < 0.6f) G.fx.Burst(pos + new Vector2(-facing * half.x, -half.y + 0.1f), 2, new Color(0.6f, 0.45f, 0.3f), 4f, 0.35f, 20f);
                    if (stateT <= 0f) { rollBounces = Phase2 ? 2 : 1; Go(S.Roll, 6f); G.sfx.Play("dash", 1f, 0.5f); }
                    break;
                case S.Roll:
                    vel.x = facing * (Phase2 ? 15f : 12f);
                    spin -= facing * 900f * dt;
                    if (Random.value < 0.7f) G.fx.Burst(pos + new Vector2(0f, -half.y + 0.1f), 2, new Color(0.6f, 0.45f, 0.3f), 4f, 0.3f, 20f);
                    if (Move(dt) || stateT <= 0f)
                    {
                        G.cam.Shake(0.45f);
                        G.sfx.Play("explode", 0.7f, 0.9f);
                        CeilingRain(Phase2 ? 5 : 3, rock, new Color(0.6f, 0.5f, 0.4f));
                        if (rollBounces-- > 0 && stateT > 0f) facing = -facing;
                        else { spin = 0f; vel.x = 0f; Go(S.Walk, Phase2 ? 1.2f : 1.8f); }
                    }
                    break;
            }
            DrawBoss(0f);
            if (!dead) go.transform.rotation = Quaternion.Euler(0f, 0f, (S)state == S.Roll ? spin : 0f);
        }

        void Land()
        {
            squash = 0.3f;
            vel.x = 0f;
            G.sfx.Play("land", 1f, 0.5f);
            G.cam.Shake(0.4f);
            int n = Phase2 ? 5 : 3;
            for (int i = 0; i < n; i++)
                Shot(pos + Vector2.up * 0.6f, new Vector2(Random.Range(-7f, 7f), Random.Range(9f, 13f)), rock, new Color(0.6f, 0.5f, 0.4f), 26f, 0f, 0.55f);
            if (Phase2) { FloorWave(1, new Color(0.8f, 0.6f, 0.4f)); FloorWave(-1, new Color(0.8f, 0.6f, 0.4f)); }
            Go(S.Walk, Phase2 ? 1.4f : 2.2f);
        }
    }

    // =====================================================================================
    // 2. SPORE MOTHER (end of Fungal Hollows): spore rings, aimed volleys, spawns crawlers, spore rain.
    // =====================================================================================

    public class SporeMother : Boss
    {
        float attackT = 2f, burstT, rainT, ringOff;
        int burstLeft;
        static readonly Color Spore = new Color(0.6f, 1f, 0.45f);

        public SporeMother(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("SPORE MOTHER", "QUEEN OF THE HOLLOWS", "spitter", hp, 3f, new Vector2(1.3f, 1.15f), a, floor + Vector2.up * 1.16f);
            tint = new Color(1f, 0.85f, 1f);
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt;
            FacePlayer();
            vel.x = U.Approach(vel.x, facing * (Phase2 ? 1.6f : 1f), 6f * dt);
            Move(dt);
            Vector2 mouth = pos + Vector2.up * (half.y * 0.6f);

            attackT -= dt;
            if (attackT <= 0f)
            {
                cycle++;
                attackT = Phase2 ? 1.7f : 2.4f;
                squash = -0.15f;
                switch (cycle % 5)
                {
                    case 3: SporeWall(); attackT += 0.8f; break;
                    case 4: SporePods(); break;
                    case 0:
                        ringOff += 17f;
                        Ring(mouth, Phase2 ? 16 : 10, 4.5f, ringOff, Art.Spit, Spore);
                        G.sfx.Play("shoot", 0.7f, 0.5f);
                        break;
                    case 1:
                        burstLeft = Phase2 ? 3 : 2;
                        burstT = 0f;
                        break;
                    case 2:
                        int alive = 0;
                        foreach (var e in G.ents) if (!e.dead && e is Crawler && Mathf.Abs(e.pos.y - pos.y) < 12f) alive++;
                        if (alive < 4)
                        {
                            int fy = Mathf.FloorToInt(arena.FloorY);
                            G.Spawn(new Crawler(Mathf.FloorToInt(arena.MinX + 1.5f), fy, arena.Depth, false));
                            G.Spawn(new Crawler(Mathf.FloorToInt(arena.MaxX - 1.5f), fy, arena.Depth, false));
                            G.fx.Burst(mouth, 20, Spore, 6f, 0.5f, 5f);
                            G.sfx.Play("bat", 0.8f, 0.6f);
                        }
                        else Ring(mouth, 8, 5f, ringOff + 22f, Art.Spit, Spore);
                        break;
                }
            }
            if (burstLeft > 0)
            {
                burstT -= dt;
                if (burstT <= 0f)
                {
                    burstT = 0.35f;
                    burstLeft--;
                    Vector2 d = (P.pos - mouth).normalized;
                    int n = Phase2 ? 5 : 3;
                    for (int i = 0; i < n; i++) Shot(mouth, U.Rot(d, (i - (n - 1) * 0.5f) * 13f) * 6.5f, Art.Spit, Spore);
                    G.sfx.Play("shoot", 0.6f, 0.7f);
                }
            }
            if (Phase2)
            {
                rainT -= dt;
                if (rainT <= 0f) { rainT = 1.1f; CeilingRain(2, Art.Spit, Spore); }
            }
            DrawBoss(-half.y);
        }

        // A wall of spores drifts across the arena with one gap in it: find the gap.
        void SporeWall()
        {
            bool fromRight = P.pos.x < arena.CenterX;
            float x = fromRight ? arena.MaxX - 0.5f : arena.MinX + 0.5f;
            float dir = fromRight ? -1f : 1f;
            float gap = Random.Range(arena.FloorY + 1.2f, arena.FloorY + 3.2f);
            for (float y = arena.FloorY + 0.35f; y < arena.CeilY - 0.3f; y += 0.6f)
            {
                if (Mathf.Abs(y - gap) < 1.15f) continue;
                Shot(new Vector2(x, y), new Vector2(dir * (Phase2 ? 6f : 4.5f), 0f), Art.Spit, Spore, 0f, 0.7f, 0.8f);
            }
            G.sfx.Play("bat", 0.9f, 0.4f);
            G.fx.Burst(new Vector2(x, gap), 20, Spore, 6f, 0.5f, 0f);
        }

        // Glowing patches appear on the floor (one under you), then burst into rings of spores.
        void SporePods()
        {
            int n = Phase2 ? 4 : 3;
            int dmgP = Enemy.ContactDmg(arena.Depth);
            for (int i = 0; i < n; i++)
            {
                float x = i == 0 ? P.pos.x : Random.Range(arena.MinX + 1f, arena.MaxX - 1f);
                var c = new Vector2(x, arena.FloorY + 0.4f);
                G.Spawn(new Hazard(c, new Vector2(0.7f, 0.4f), 1.1f + i * 0.15f, 0.25f, Spore, dmgP, false,
                    () => Ring(c + Vector2.up * 0.3f, Phase2 ? 8 : 6, 4.5f, Random.value * 360f, Art.Spit, Spore)));
            }
            G.sfx.Play("shoot", 0.7f, 0.4f);
        }
    }

    // =====================================================================================
    // 3. CRYSTAL COLOSSUS (end of Crystal Depths): wind-up charges into the wall (crystal rain), stomps that
    //    send shockwaves along the floor. Takes extra damage while stunned.
    // =====================================================================================

    public class CrystalColossus : Boss
    {
        enum S { Walk, WindUp, Charge, Stun, Jump, Spikes }
        static Sprite shard;
        static readonly Color Cry = new Color(0.55f, 0.95f, 1f);

        public CrystalColossus(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("CRYSTAL COLOSSUS", "HEART OF THE DEPTHS", "golem", hp, 2.8f, new Vector2(1.3f, 0.75f), a, floor + Vector2.up * 0.76f);
            tint = new Color(0.65f, 0.92f, 1f);
            if (shard == null) shard = Art.TileSprite(Block.Crystal, Biomes.All[Biomes.Crystal]);
            Go(S.Walk, 1.5f);
        }

        void Go(S s, float time) { state = (int)s; stateT = time; }

        public override bool Hurt(float d, Vector2 knock) => base.Hurt(state == (int)S.Stun ? d * 1.5f : d, knock);

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            switch ((S)state)
            {
                case S.Walk:
                    FacePlayer();
                    vel.x = U.Approach(vel.x, facing * (Phase2 ? 3f : 2.2f), 15f * dt);
                    Move(dt);
                    if (stateT <= 0f)
                    {
                        cycle++;
                        vel.x = 0f;
                        int k = cycle % 3;
                        if (k == 1) { FacePlayer(); Go(S.WindUp, Phase2 ? 0.5f : 0.75f); G.sfx.Play("rumble", 0.8f, 1.3f); }
                        else if (k == 2) { vel.y = 17f; Go(S.Jump, 3f); G.sfx.Play("jump", 0.8f, 0.4f); }
                        else { Go(S.Spikes, Phase2 ? 2f : 1.6f); CrystalSpikes(); }
                    }
                    break;
                case S.WindUp:   // flashes and scrapes the floor: a charge is coming
                    flash = ((int)(stateT * 12f)) % 2 == 0 ? 0.05f : 0f;
                    if (Random.value < 0.5f) G.fx.Burst(pos + new Vector2(-facing * half.x, -half.y + 0.1f), 2, Cry, 3f, 0.3f, 10f);
                    Move(dt);
                    if (stateT <= 0f) { Go(S.Charge, 3f); G.sfx.Play("dash", 1f, 0.6f); }
                    break;
                case S.Charge:
                    vel.x = facing * (Phase2 ? 17f : 14f);
                    if (Random.value < 0.6f) G.fx.Burst(pos + new Vector2(0f, -half.y + 0.1f), 2, Cry, 3f, 0.3f, 10f);
                    if (Move(dt) || stateT <= 0f)
                    {
                        G.sfx.Play("explode", 0.9f, 0.7f);
                        G.cam.Shake(0.6f);
                        squash = 0.25f;
                        CeilingRain(Phase2 ? 10 : 6, shard, Cry);
                        Go(S.Stun, Phase2 ? 1.0f : 1.4f);
                    }
                    break;
                case S.Stun:
                    vel.x = 0f;
                    Move(dt);
                    if (Random.value < 0.2f) G.fx.Burst(pos + Vector2.up * half.y, 1, Color.white, 2f, 0.5f, -2f);
                    if (stateT <= 0f) Go(S.Walk, Phase2 ? 1.2f : 1.8f);
                    break;
                case S.Spikes:   // stomps while crystals grow up out of the floor
                    vel.x = 0f;
                    Move(dt);
                    squash = 0.1f + Mathf.Abs(Mathf.Sin(t * 10f)) * 0.08f;
                    if (Phase2 && stateT < 1f && stateT + dt >= 1f) CrystalSpikes();   // a second wave where you are now
                    if (stateT <= 0f) Go(S.Walk, 1.4f);
                    break;
                case S.Jump:
                    Move(dt);
                    if (grounded && vel.y <= 0f && stateT < 2.9f)
                    {
                        squash = 0.3f;
                        G.sfx.Play("land", 1f, 0.4f);
                        G.cam.Shake(0.5f);
                        FloorWave(1, Cry); FloorWave(-1, Cry);
                        if (Phase2) { stateT = 99f; Go(S.Walk, 0.6f); cycle++; DoubleWave(); }
                        else Go(S.Walk, 1.8f);
                    }
                    break;
            }
            DrawBoss(-half.y);
        }

        // Glowing cracks on the floor (one right under you), then crystal spikes shoot up out of them.
        void CrystalSpikes()
        {
            G.sfx.Play("land", 1f, 0.5f);
            G.cam.Shake(0.25f);
            int n = Phase2 ? 5 : 4;
            int dmgP = Enemy.ContactDmg(arena.Depth);
            for (int i = 0; i < n; i++)
            {
                float x = i == 0 ? P.pos.x : Random.Range(arena.MinX + 1f, arena.MaxX - 1f);
                G.Spawn(new Hazard(new Vector2(x, arena.FloorY + 1.3f), new Vector2(0.45f, 1.3f), 0.85f, 0.4f, Cry, dmgP));
            }
        }

        void DoubleWave() => G.Spawn(new DelayedCall(0.5f, () => { if (!dead) { FloorWave(1, Cry); FloorWave(-1, Cry); } }));
    }

    // =====================================================================================
    // 4. CINDER LORD (Magma Core and every 100 depth after): flies, spirals of fire, volleys, dive bombs.
    // =====================================================================================

    public class CinderLord : Boss
    {
        enum S { Hover, Spiral, Volley, DiveWind, Dive, Return, Meteors }
        float shotT, spiralA;
        int volleyLeft;
        Vector2 diveDir;
        static readonly Color Fire = new Color(1f, 0.6f, 0.2f);

        public CinderLord(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("CINDER LORD", "KING BELOW THE MAGMA", "imp", hp, 2.6f, new Vector2(1f, 1f), a, new Vector2(a.CenterX, a.FloorY + 7f));
            Go(S.Hover, 1.5f);
        }

        void Go(S s, float time) { state = (int)s; stateT = time; }

        Vector2 HoverPoint => new Vector2(arena.CenterX + Mathf.Sin(t * 0.7f) * 10f, arena.FloorY + 6.5f + Mathf.Sin(t * 1.4f) * 1.5f);

        void Fly(float dt, Vector2 target, float speed)
        {
            Vector2 want = target - pos;
            want = want.sqrMagnitude > 0.01f ? want.normalized * Mathf.Min(speed, want.magnitude * 4f) : Vector2.zero;
            vel = Vector2.MoveTowards(vel, want, 25f * dt);
            Move(dt, false);
            if (Mathf.Abs(vel.x) > 0.3f) facing = vel.x > 0 ? 1 : -1;
        }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            switch ((S)state)
            {
                case S.Hover:
                    Fly(dt, HoverPoint, 6f);
                    if (stateT <= 0f)
                    {
                        cycle++;
                        switch (cycle % 4)
                        {
                            case 3: Go(S.Meteors, 1.8f); Meteors(); break;
                            case 1: Go(S.Spiral, 2.2f); break;
                            case 2: volleyLeft = Phase2 ? 8 : 5; shotT = 0f; Go(S.Volley, 3f); break;
                            default: Go(S.DiveWind, 0.6f); G.sfx.Play("bat", 1f, 0.5f); break;
                        }
                    }
                    break;
                case S.Spiral:
                    Fly(dt, HoverPoint, 2f);
                    shotT -= dt;
                    if (shotT <= 0f)
                    {
                        shotT = 0.09f;
                        spiralA += 23f;
                        int arms = Phase2 ? 3 : 2;
                        for (int i = 0; i < arms; i++)
                        {
                            float a = (spiralA + i * 360f / arms) * Mathf.Deg2Rad;
                            Shot(pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5f, Art.Fireball, Fire);
                        }
                        if (((int)(stateT * 10f)) % 3 == 0) G.sfx.Play("shoot", 0.3f, 0.8f);
                    }
                    if (stateT <= 0f) Go(S.Hover, Phase2 ? 1.2f : 1.8f);
                    break;
                case S.Volley:
                    Fly(dt, HoverPoint, 3f);
                    shotT -= dt;
                    if (shotT <= 0f && volleyLeft > 0)
                    {
                        shotT = 0.16f;
                        volleyLeft--;
                        Shot(pos, ToPlayer * 8.5f, Art.Fireball, Fire);
                        G.sfx.Play("shoot", 0.5f, 0.7f);
                    }
                    if (volleyLeft <= 0 && shotT <= 0f) Go(S.Hover, Phase2 ? 1.2f : 1.8f);
                    break;
                case S.DiveWind:   // shrieks and flashes, then dives at where you are
                    vel *= 0.9f;
                    Move(dt, false);
                    flash = ((int)(stateT * 14f)) % 2 == 0 ? 0.05f : 0f;
                    if (stateT <= 0f)
                    {
                        diveDir = ToPlayer;
                        Go(S.Dive, 0.9f);
                        G.sfx.Play("dash", 1f, 1.2f);
                    }
                    break;
                case S.Dive:
                    vel = diveDir * (Phase2 ? 19f : 16f);
                    G.fx.Burst(pos, 2, Fire, 2f, 0.3f, 0f);
                    Vector2 before = pos;
                    Move(dt, false);
                    if ((pos - before).sqrMagnitude < (vel * dt).sqrMagnitude * 0.25f || stateT <= 0f)
                    {
                        G.cam.Shake(0.35f);
                        G.sfx.Play("explode", 0.6f, 1.1f);
                        Ring(pos, Phase2 ? 10 : 6, 5f, Random.value * 360f, Art.Fireball, Fire);
                        Go(S.Return, 0.8f);
                    }
                    break;
                case S.Meteors:   // raises its arms; burning circles mark where fire will fall
                    Fly(dt, new Vector2(arena.CenterX, arena.CeilY - 2f), 5f);
                    tint = Color.Lerp(Color.white, Fire, 0.5f + 0.5f * Mathf.Sin(t * 14f));
                    if (stateT <= 0f) { tint = Color.white; Go(S.Hover, Phase2 ? 1f : 1.5f); }
                    break;
                case S.Return:
                    Fly(dt, HoverPoint, 7f);
                    if (stateT <= 0f) Go(S.Hover, Phase2 ? 0.9f : 1.4f);
                    break;
            }
            DrawBoss(0f);
        }

        void Meteors()
        {
            G.sfx.Play("charge", 1f, 0.4f);
            int n = Phase2 ? 8 : 5;
            int dmgP = Enemy.ContactDmg(arena.Depth);
            for (int i = 0; i < n; i++)
            {
                float x = i == 0 ? P.pos.x : Random.Range(arena.MinX + 1f, arena.MaxX - 1f);
                G.Spawn(new Hazard(new Vector2(x, arena.FloorY + 0.9f), new Vector2(0.9f, 0.9f), 0.9f + i * 0.18f, 0.2f, Fire, dmgP, true));
            }
        }

        public override void AddLight(Lighting l) => l.Add(pos, 4.5f, 0.85f);
    }

    // =====================================================================================
    // 5. KING ORNIN, UNDEAD (bottom of the world): sword slams, God-Diamond beams, raises the dead, leaps.
    // =====================================================================================

    public class OrninBoss : Boss
    {
        enum S { Walk, SlamWind, Beams, Summon, Air, March }
        static readonly Color Curse = new Color(0.75f, 0.45f, 1f);
        static readonly Color Diamond = new Color(0.5f, 0.9f, 1f);
        float beamT;

        public OrninBoss(BossArena a, Vector2 floor, float hp)
        {
            SetupBoss("KING ORNIN", "THE KING WHO CHALLENGED THE GODS", "ornin", hp, 2.3f, new Vector2(1.4f, 1.9f), a, floor + Vector2.up * 1.91f);
            dmg = Enemy.ContactDmg(a.Depth) * 3 / 2;
            gold = 300;
            blood = new Color(0.55f, 0.75f, 0.4f);
            Go(S.Walk, 2f);
        }

        void Go(S s, float time) { state = (int)s; stateT = time; }

        public override void Tick(float dt)
        {
            BaseTick(dt);
            if (dead) return;
            t += dt; stateT -= dt;
            tint = Color.white;
            switch ((S)state)
            {
                case S.Walk:
                    FacePlayer();
                    vel.x = U.Approach(vel.x, facing * (Phase2 ? 2.6f : 1.7f), 12f * dt);
                    Move(dt);
                    if (Random.value < 0.05f) G.fx.Burst(pos + new Vector2(Random.Range(-1f, 1f), -half.y + 0.2f), 1, Curse, 1f, 0.6f, -3f);
                    if (stateT <= 0f)
                    {
                        cycle++;
                        vel.x = 0f;
                        switch (cycle % 5)
                        {
                            case 4: Go(S.March, 3f); MarchingBeams(); break;
                            case 1: FacePlayer(); Go(S.SlamWind, Phase2 ? 0.5f : 0.75f); G.sfx.Play("rumble", 0.8f, 0.8f); break;
                            case 2: Go(S.Beams, 2.2f); beamT = 0f; G.sfx.Play("charge", 1f, 0.5f); break;
                            case 3: Go(S.Summon, 1.2f); break;
                            default:
                                vel = new Vector2(Mathf.Clamp(P.pos.x - pos.x, -10f, 10f) * 0.85f, 17f);
                                G.sfx.Play("jump", 1f, 0.4f);
                                Go(S.Air, 3f);
                                break;
                        }
                    }
                    break;

                case S.SlamWind:   // raises the sword, flashing - then brings it down
                    squash = -0.12f;
                    flash = ((int)(stateT * 12f)) % 2 == 0 ? 0.05f : 0f;
                    Move(dt);
                    if (stateT <= 0f) Slam();
                    break;

                case S.Beams:      // channels the God-Diamond: warning lines, then beams
                    tint = Color.Lerp(Color.white, Diamond, 0.5f + 0.5f * Mathf.Sin(t * 12f));
                    vel.x = 0f;
                    Move(dt);
                    beamT -= dt;
                    if (beamT <= 0f && stateT > 0.6f)
                    {
                        beamT = Phase2 ? 0.55f : 0.8f;
                        G.Spawn(new BeamStrike(arena, new Vector2(P.pos.x, P.pos.y), true, 0.9f));
                        if (Random.value < 0.5f || Phase2) G.Spawn(new BeamStrike(arena, new Vector2(P.pos.x, P.pos.y), false, 1.0f));
                        if (Phase2) G.Spawn(new BeamStrike(arena, new Vector2(Random.Range(arena.MinX + 1f, arena.MaxX - 1f), 0f), true, 0.9f));
                    }
                    if (stateT <= 0f) Go(S.Walk, Phase2 ? 1.2f : 1.8f);
                    break;

                case S.Summon:     // the dead dwarfs of his kingdom crawl out of the floor
                    tint = Color.Lerp(Color.white, Curse, 0.6f);
                    vel.x = 0f;
                    Move(dt);
                    if (stateT <= 0.6f && stateT + dt > 0.6f)
                    {
                        int alive = 0;
                        foreach (var e in G.ents) if (!e.dead && e is Crawler && Mathf.Abs(e.pos.y - pos.y) < 12f) alive++;
                        int n = Mathf.Min(Phase2 ? 3 : 2, 5 - alive);
                        int fy = Mathf.FloorToInt(arena.FloorY);
                        for (int i = 0; i < n; i++)
                        {
                            int x = Mathf.FloorToInt(Random.Range(arena.MinX + 1f, arena.MaxX - 1f));
                            G.Spawn(new Crawler(x, fy, arena.Depth, true));
                            G.fx.Burst(new Vector2(x + 0.5f, fy + 0.3f), 18, Curse, 6f, 0.6f, 15f);
                        }
                        G.sfx.Play("rumble", 0.9f, 1.2f);
                        Ring(pos + Vector2.up * 1f, Phase2 ? 12 : 8, 4.5f, t * 50f, Art.Spit, Curse);
                    }
                    if (stateT <= 0f) Go(S.Walk, 1.6f);
                    break;

                case S.March:      // raises the diamond: a row of beams sweeps across the hall
                    tint = Color.Lerp(Color.white, Diamond, 0.5f + 0.5f * Mathf.Sin(t * 16f));
                    vel.x = 0f;
                    Move(dt);
                    if (stateT <= 0f) Go(S.Walk, Phase2 ? 1f : 1.5f);
                    break;
                case S.Air:
                    Move(dt);
                    if (grounded && vel.y <= 0f && stateT < 2.9f)
                    {
                        squash = 0.3f;
                        G.sfx.Play("explode", 0.8f, 0.6f);
                        G.cam.Shake(0.6f);
                        FloorWave(1, Curse); FloorWave(-1, Curse);
                        Ring(pos + Vector2.up * 0.8f, Phase2 ? 14 : 10, 5f, t * 70f, Art.Fireball, Curse);
                        Go(S.Walk, Phase2 ? 1.2f : 1.8f);
                    }
                    break;
            }
            DrawBoss(-half.y);
        }

        // Beams fire one after another from his side of the hall to the other. Step in behind the sweep.
        void MarchingBeams()
        {
            G.sfx.Play("charge", 1f, 0.6f);
            bool fromLeft = pos.x < arena.CenterX;
            int n = 9;
            float w = (arena.MaxX - arena.MinX) / n;
            for (int i = 0; i < n; i++)
            {
                int k = fromLeft ? i : n - 1 - i;
                float x = arena.MinX + w * (k + 0.5f);
                G.Spawn(new BeamStrike(arena, new Vector2(x, 0f), true, 0.8f + i * (Phase2 ? 0.16f : 0.22f)));
            }
            if (Phase2) G.Spawn(new BeamStrike(arena, P.pos, false, 1.6f));
        }

        void Slam()
        {
            squash = 0.25f;
            G.sfx.Play("explode", 1f, 0.7f);
            G.cam.Shake(0.7f);
            G.HitStop(0.05f);
            Vector2 c = pos + new Vector2(facing * 2.2f, -half.y + 1f);
            G.fx.Burst(c, 30, Diamond, 8f, 0.4f, 10f, 2f);
            G.fx.Ring(c, 2f, Diamond);
            var pl = P;
            if (pl.mode != Player.Mode.Dead && U.Overlap(c, new Vector2(1.7f, 1.3f), pl.pos, Player.Half)) pl.Hurt(2 * Enemy.ContactDmg(-Mathf.FloorToInt(pos.y)), pos);
            FloorWave(facing, Diamond);
            if (Phase2) { FloorWave(-facing, Diamond); CeilingRain(6, Art.TileSprite(Block.Brick, Biomes.All[Biomes.Kingdom]), new Color(0.8f, 0.7f, 0.5f)); }
            Go(S.Walk, Phase2 ? 1.2f : 1.8f);
        }

        public override void AddLight(Lighting l) => l.Add(pos + Vector2.up * 0.5f, 4.5f, 0.8f);
    }

    // A boss attack you can learn: a flashing warning area first, then it goes off.
    public class Hazard : Entity
    {
        readonly Vector2 half;
        readonly Color col;
        readonly int dmg;
        readonly bool meteor;
        readonly System.Action onFire;
        readonly float activeMax;
        float warn, active;
        bool fired, hitDone;

        public override bool AlwaysActive => true;

        public Hazard(Vector2 center, Vector2 half, float warn, float active, Color col, int dmg, bool meteor = false, System.Action onFire = null)
        {
            pos = center; this.half = half; this.warn = warn; this.active = active; activeMax = active;
            this.col = col; this.dmg = dmg; this.meteor = meteor; this.onFire = onFire;
            MakeGO("Hazard", meteor ? Art.Circle : Art.Square, 29);
            go.transform.localScale = meteor ? new Vector3(half.x * 2f, half.y * 2f, 1f) : new Vector3(half.x * 2f, half.y * 2f, 1f);
            sr.color = new Color(col.r, col.g, col.b, 0f);
        }

        public override void Tick(float dt)
        {
            if (!fired)
            {
                warn -= dt;
                // faster blinking right before it goes off
                float rate = warn < 0.35f ? 24f : 10f;
                float a = ((int)(warn * rate)) % 2 == 0 ? 0.45f : 0.18f;
                sr.color = new Color(col.r, col.g, col.b, a);
                if (meteor && warn < 0.3f) G.fx.Burst(pos + Vector2.up * (warn * 25f), 2, col, 1f, 0.2f, 0f, 2f, false);   // the fireball falling in
                if (warn > 0f) return;
                fired = true;
                G.sfx.Play(meteor ? "explode" : "shoot", 0.6f, meteor ? 1.2f : 0.5f);
                G.cam.Shake(0.22f);
                if (meteor) G.fx.Explosion(pos, 0.9f);
                else G.fx.Burst(pos, 18, col, 7f, 0.4f, 10f);
                onFire?.Invoke();
            }
            active -= dt;
            float k = Mathf.Clamp01(active / activeMax);
            sr.color = Color.Lerp(new Color(col.r, col.g, col.b, 0f), Color.white, k);
            var pl = G.player;
            if (!hitDone && pl.mode != Player.Mode.Dead && U.Overlap(pos, half, pl.pos, Player.Half * 0.85f))
            {
                hitDone = true;
                pl.Hurt(dmg, pos);
            }
            if (active <= 0f) Kill();
        }

        public override void AddLight(Lighting l) => l.Add(pos, fired ? 3f : 1.4f, fired ? 0.9f : 0.4f);
    }

    // A God-Diamond beam: a thin warning line, then a full-height (or full-width) blast.
    public class BeamStrike : Entity
    {
        readonly bool vertical;
        readonly BossArena a;
        float warn, active = 0.35f;
        bool fired;

        public override bool AlwaysActive => true;

        public BeamStrike(BossArena a, Vector2 at, bool vertical, float warn)
        {
            this.a = a; this.vertical = vertical; this.warn = warn;
            float midY = (a.FloorY + a.CeilY) * 0.5f;
            pos = vertical ? new Vector2(Mathf.Floor(at.x) + 0.5f, midY) : new Vector2((a.MinX + a.MaxX) * 0.5f, Mathf.Clamp(at.y, a.FloorY + 0.5f, a.CeilY - 0.5f));
            MakeGO("DiamondBeam", Art.Square, 29);
            Size(0.12f);
            sr.color = new Color(1f, 0.3f, 0.3f, 0.7f);
        }

        void Size(float thick)
        {
            float len = vertical ? a.CeilY - a.FloorY : a.MaxX - a.MinX;
            go.transform.localScale = vertical ? new Vector3(thick, len, 1f) : new Vector3(len, thick, 1f);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
        }

        public override void Tick(float dt)
        {
            if (!fired)
            {
                warn -= dt;
                sr.color = ((int)(warn * 14f)) % 2 == 0 ? new Color(1f, 0.3f, 0.3f, 0.8f) : new Color(1f, 0.8f, 0.8f, 0.4f);
                if (warn <= 0f)
                {
                    fired = true;
                    Size(1.1f);
                    G.sfx.Play("shoot", 0.8f, 0.4f);
                    G.cam.Shake(0.25f);
                }
                return;
            }
            active -= dt;
            float k = Mathf.Clamp01(active / 0.35f);
            sr.color = Color.Lerp(new Color(0.5f, 0.9f, 1f, 0f), Color.white, k);
            Size(1.1f * (0.4f + 0.6f * k));
            var pl = G.player;
            if (pl.mode != Player.Mode.Dead)
            {
                bool hit = vertical ? Mathf.Abs(pl.pos.x - pos.x) < 0.75f : Mathf.Abs(pl.pos.y - pos.y) < 0.75f;
                if (hit) pl.Hurt(Enemy.ContactDmg(-Mathf.FloorToInt(pos.y)), vertical ? new Vector2(pos.x, pl.pos.y) : new Vector2(pl.pos.x - pl.facing, pos.y));
            }
            if (active <= 0f) Kill();
        }

        public override void AddLight(Lighting l)
        {
            if (!fired) return;
            if (vertical) for (float y = a.FloorY; y < a.CeilY; y += 3f) l.Add(new Vector2(pos.x, y), 2.5f, 0.9f);
            else for (float x = a.MinX; x < a.MaxX; x += 3f) l.Add(new Vector2(x, pos.y), 2.5f, 0.9f);
        }
    }

    // The God-Diamond. Caged by Ornin's curse until he falls; touching it ends the game.
    public class GodDiamond : Entity
    {
        public bool Unlocked { get; private set; }
        readonly BossArena arena;
        readonly GameObject cage;
        readonly SpriteRenderer cageSr;
        float t;

        public override bool AlwaysActive => true;

        public GodDiamond(BossArena a)
        {
            arena = a;
            pos = new Vector2(a.CenterX, a.FloorY + 3.4f);
            MakeGO("GodDiamond", Art.Icons["goddiamond"], 13);
            go.transform.localScale = Vector3.one * 1.5f;
            cage = new GameObject("Curse");
            cage.transform.SetParent(G.runRoot, false);
            cage.transform.position = new Vector3(pos.x, pos.y, 0f);
            cageSr = Art.AddSR(cage, Art.Ring, 14);
            G.diamond = this;
        }

        public void Unlock()
        {
            Unlocked = true;
            cage.SetActive(false);
            G.fx.Burst(pos, 60, new Color(0.7f, 0.95f, 1f), 9f, 1.2f, 0f, 2f);
            G.lighting.Flash(pos, 14f, 1f);
            G.sfx.Play("chest", 1f, 0.7f);
        }

        public override void Tick(float dt)
        {
            t += dt;
            float bob = Mathf.Sin(t * 1.6f) * 0.15f;
            go.transform.position = new Vector3(U.Snap(pos.x), U.Snap(pos.y + bob), 0f);
            if (Random.value < (Unlocked ? 0.5f : 0.15f))
                G.fx.Burst(pos + new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(-0.8f, 0.8f)), 1, new Color(0.7f, 0.95f, 1f), 1f, 0.8f, -2f, 1f, false);
            if (!Unlocked)
            {
                float s = 3.2f + Mathf.Sin(t * 4f) * 0.2f;
                cage.transform.localScale = new Vector3(s, s, 1f);
                cageSr.color = new Color(0.8f, 0.4f, 1f, 0.55f + 0.25f * Mathf.Sin(t * 6f));
                var p = G.player;
                if (p.mode != Player.Mode.Dead && (p.pos - pos).sqrMagnitude < 16f)
                    G.hud.Label(pos + Vector2.up * 2.4f, "THE GOD-DIAMOND. ORNIN'S CURSE STILL BINDS IT", new Color(0.8f, 0.6f, 1f));
            }
        }

        public override void AddLight(Lighting l) => l.Add(pos, Unlocked ? 9f + Mathf.Sin(t * 3f) * 0.4f : 5f, 1f);
    }

    // Small helper: run an action after a delay (used for follow-up attacks).
    public class DelayedCall : Entity
    {
        float t;
        readonly System.Action a;
        public override bool AlwaysActive => true;
        public DelayedCall(float delay, System.Action a) { t = delay; this.a = a; }
        public override void Tick(float dt)
        {
            t -= dt;
            if (t <= 0f) { Kill(); a(); }
        }
    }
}
