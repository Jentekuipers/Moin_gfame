using System.Collections.Generic;
using UnityEngine;

namespace DigDeep
{
    // Auto-starts in whatever scene you press Play in. No prefabs, no assets, no setup.
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Object.FindAnyObjectByType<Game>() != null) return;
            new GameObject("DigDeep").AddComponent<Game>();
        }
    }

    public class Game : MonoBehaviour
    {
        public static Game I;
        public enum State { Intro, Title, Select, Playing, Paused, Dead, Ending, Options, Credits }

        public World world;
        public Fluids fluids;
        public Player player;
        public CameraRig cam;
        public Lighting lighting;
        public Hud hud;
        public Music music;
        public Boss boss, boss2;   // boss2: the second of a pair (the twin dragons)
        public Story story;
        public GodDiamond diamond;
        public float whiteAlpha;
        float introGrace;
        bool endMusic;
        public Sfx sfx;
        public FX fx;
        public Transform runRoot;
        public readonly List<Entity> ents = new List<Entity>();
        public readonly List<Altar> altars = new List<Altar>();
        readonly List<Corpse> corpses = new List<Corpse>();
        public Vector2 aim;   // where the cursor (or right stick) points, in world space
        readonly List<Entity> toAdd = new List<Entity>();

        public State state;
        public System.Random rng;
        public int seed, maxDepth, best, charIndex;
        public bool newBest;
        public float deadT, time;
        // GLIMMER STONE: a patch of the world lit to full brightness for a while
        public float revealT, revealMax;
        Vector2 revealC, revealH;

        public void Reveal(Vector2 c, Vector2 half, float t) { revealC = c; revealH = half; revealT = revealMax = t; lighting.Flash(c, half.x, 0.5f); }
        public bool InReveal(Vector2 p) => revealT > 0f && Mathf.Abs(p.x - revealC.x) < revealH.x && Mathf.Abs(p.y - revealC.y) < revealH.y;
        public float RevealFade => revealT <= 0f ? 0f : Mathf.Clamp01(revealT / 2f);
        float freeze;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 120;
            QualitySettings.antiAliasing = 0;   // crisp pixels: no smoothing
            Art.Init();
            DB.Init();
            Chars.Init();
            sfx = new Sfx(gameObject);
            music = new Music(gameObject);
            Settings.Load();   // volumes, screen, accessibility and key bindings
            var c = Camera.main;
            if (c == null)
            {
                var cg = new GameObject("Main Camera") { tag = "MainCamera" };
                c = cg.AddComponent<Camera>();
            }
            if (Object.FindAnyObjectByType<AudioListener>() == null) c.gameObject.AddComponent<AudioListener>();
            cam = new CameraRig(c);
            hud = new Hud();
            best = PlayerPrefs.GetInt("digdeep_best", 0);
            charIndex = Mathf.Clamp(PlayerPrefs.GetInt("digdeep_char", 0), 0, Chars.All.Count - 1);
            story = new Story();
            GoTitle();   // the intro slideshow is shelved for now (GoIntro() plays it)
        }

        void GoIntro()
        {
            state = State.Intro;
            story.StartIntro();
            music.PlayIntro(1f);
            introGrace = 0.4f;
        }

        // The start screen lives in the campfire cave of a fresh run: the world is built (and keeps ticking)
        // behind the menu, the diggers line up by the fire, and picking one starts the run right there.
        Camp camp;
        public Camp CampRef => camp;
        bool menuWorld;   // the current world is the menu's (no run has started in it yet)
        Vector2 CampFloor => new Vector2(Gen.CampCx + 0.5f, 1 - Gen.CampFloor);
        Vector2 MenuFocus => CampFloor + (state == State.Select ? new Vector2(0f, 3.2f) : new Vector2(-6f, 3.5f));

        void GoTitle()
        {
            if (!menuWorld || world == null) BeginRun(null, true);
            state = State.Title;
            Time.timeScale = 1f;
            whiteAlpha = 0f;
            music.PlayMenu();
        }

        // Has every digger found the God-Diamond? Then the gods above the clouds can be fought.
        public static bool AllDiamonds()
        {
            foreach (var c in Chars.All) if (PlayerPrefs.GetInt("digdeep_won_" + c.id, 0) != 1) return false;
            return true;
        }

        // touching the God-Diamond
        void BeginEnding()
        {
            // beating the game with a digger lets every digger find that digger's own skill
            if (TestMode.On)
            {
                // a test run never counts: the test god route pretends every digger already has a diamond
                if (TestMode.PretendAllDiamonds) { StartAscent(); return; }
                ShowEnding(AllDiamonds() ? 0 : 1);
                return;
            }
            SaveGame.Delete();
            if (player != null)
            {
                PlayerPrefs.SetInt("digdeep_won_" + player.character.id, 1);
                PlayerPrefs.Save();
            }
            PlayerPrefs.SetInt("digdeep_wins", PlayerPrefs.GetInt("digdeep_wins", 0) + 1);
            if (maxDepth > best) { best = maxDepth; PlayerPrefs.SetInt("digdeep_best", best); }
            PlayerPrefs.Save();
            // every digger has it now: the diamond lifts you up, above the clouds, to the gods
            if (AllDiamonds()) { StartAscent(); return; }
            ShowEnding(AllDiamonds() ? 0 : 1);
        }

        void ShowEnding(int variant)
        {
            World.SkyMode = false;
            state = State.Ending;
            Time.timeScale = 1f;
            whiteAlpha = 0f;
            endMusic = false;
            story.StartEnding(variant);
            music.Stop(1.5f);
            sfx.Play("chest", 1f, 0.5f);
        }

        // ------------------------------------------------------------ the God above the clouds

        public float skyWhite, cloudFade;

        // the white fades and you're back in Ornin's hall, able to fly, an arrow pointing up
        public void StartAscent()
        {
            Time.timeScale = 1f;
            skyWhite = 1f;
            state = State.Playing;
            if (diamond != null) { diamond.Kill(); diamond = null; }   // it's in you now
            player.mode = Player.Mode.Normal;
            player.vel = Vector2.zero;
            // if you quit before reaching the surface, you still come back at the surface
            SaveAscentCheckpoint(new Vector2(player.pos.x, Gen.SurfaceTop + 1f + Player.Half.y + 0.05f));
            Spawn(new Ascent(false));
            Spawn(new SkyBackdrop());
            hud.Banner("THE DIAMOND LIFTS YOU", "FLY UP. ALL THE WAY UP", new Color(1f, 0.85f, 0.35f));
            music.PlayIntro(3f);
        }

        RunSave BuildSave()
        {
            var r = new RunSave { seed = seed, charIndex = charIndex, maxDepth = maxDepth, time = time };
            player.CaptureTo(r);
            world.CaptureTo(r);
            foreach (var a in altars)
                if (!a.dead && a.done) { r.doneAltarX.Add(a.pos.x); r.doneAltarY.Add(a.pos.y); }
            foreach (var e in ents)
                if (!e.dead && e is BossArena ba && ba.Cleared) r.clearedArenas.Add(ba.Depth);
            return r;
        }

        // the run during the ascent and the fight above the clouds comes back to the surface, ready to fly up again
        public void SaveAscentCheckpoint(Vector2 at)
        {
            if (TestMode.On || player == null) return;
            var r = BuildSave();
            r.ascent = true; r.ax = at.x; r.ay = at.y;
            SaveGame.Write(r);
        }

        public void StartSkyFight(bool fromClouds = false)
        {
            World.SkyMode = true;
            Time.timeScale = 1f;
            if (fromClouds) { cloudFade = 1f; skyWhite = 0f; } else skyWhite = 1f;
            state = State.Playing;
            // everything of the depths is left behind
            foreach (var e in ents) if (!e.dead && (e is Enemy || e is BossShot || e is Hazard || e is Bullet)) e.Kill();
            boss = null; boss2 = null;
            player.mode = Player.Mode.Normal;
            player.pos = SkyGod.Center + new Vector2(0f, fromClouds ? -cam.halfH + 1.2f : -5f);
            player.vel = fromClouds ? Vector2.up * 8f : Vector2.zero;   // bursting up out of the clouds
            player.Extinguish();
            cam.Snap(SkyGod.Center);
            bool backdrop = false;
            foreach (var e in ents) if (!e.dead && e is SkyBackdrop) backdrop = true;
            foreach (var e in toAdd) if (!e.dead && e is SkyBackdrop) backdrop = true;
            if (!backdrop) Spawn(new SkyBackdrop());
            var god = new SkyGod();
            Spawn(god);
            boss = god;
            music.PlayBoss();
            hud.Banner("THE GOD ABOVE THE CLOUDS", "SHOOT THE HEAD", new Color(1f, 0.85f, 0.35f));
            sfx.Play("rumble", 1f, 0.5f);
            sfx.Play("chest", 1f, 0.4f);
        }

        // the god falls: the true ending
        public void OnGodDefeated()
        {
            boss = null;
            if (!TestMode.On)   // a test win doesn't count
            {
                if (player != null) PlayerPrefs.SetInt("digdeep_godwon_" + player.character.id, 1);
                PlayerPrefs.SetInt("digdeep_godwins", PlayerPrefs.GetInt("digdeep_godwins", 0) + 1);
                PlayerPrefs.Save();
                SaveGame.Delete();
            }
            music.Stop(3f);
            hud.Banner("THE GOD FALLS", "THE SKY GOES QUIET", new Color(1f, 0.9f, 0.5f));
            Spawn(new DelayedCall(4f, () => { skyWhite = 1f; Spawn(new DelayedCall(1.2f, () => ShowEnding(2))); }));
        }

        public void NewRun()
        {
            if (TestMode.On) { RestartTest(); return; }
            BeginRun(null, false, false);
        }

        // after dying in a test run: a fresh test run, back at the set-up screen, with everything you'd picked
        void RestartTest()
        {
            BeginRun(null, false, true);
            TestMode.Replay(player);
            TestMode.OpenPanel(true);
            state = State.Playing;
        }

        // ESC on the test set-up screen (or quitting a test): back to the menu, nothing kept
        public void QuitTest()
        {
            TestMode.ClearPicks();
            TestMode.PretendAllDiamonds = false;
            TestMode.On = false; TestMode.Open = false; TestMode.Setup = false;
            music.SetDuck(false);
            GoTitle();
        }

        // the death screen's QUIT
        void QuitFromDeath()
        {
            if (TestMode.On) { QuitTest(); return; }
            GoTitle();
        }

        // Save the run and go back to the title screen (pause screen: SAVE & QUIT).
        public void SaveAndQuit()
        {
            if (TestMode.On) { QuitTest(); return; }   // test runs are never saved
            // on the way to the gods the checkpoint at the surface is already saved: you come back there
            if (World.SkyMode || Ascent.Flying) { music.SetDuck(false); GoTitle(); return; }
            if (player == null || player.mode == Player.Mode.Dead) return;
            SaveGame.Write(BuildSave());
            music.SetDuck(false);
            sfx.Play("buy", 0.7f);
            GoTitle();
        }

        public void ContinueRun()
        {
            var r = SaveGame.Read();
            if (r == null) { SaveGame.Delete(); NewRun(); return; }
            BeginRun(r);
        }

        // TEST MODE: a run with the test panel (F1 / VIEW): every weapon, item and boss, god mode, damage numbers.
        // It never touches your saved run.
        void StartTestMode()
        {
            TestMode.ClearPicks();
            TestMode.PretendAllDiamonds = false;
            TestMode.On = true;
            BeginRun(null, false, true);
            TestMode.OpenPanel(true);
        }

        // TEST MODE: the road to the God. You're dropped into Ornin's hall, Ornin already beaten and the God-Diamond free,
        // as if every digger had found theirs: touch it, fly up through the world and the clouds, and fight the God.
        public void TestGodRoute()
        {
            if (World.SkyMode || Ascent.Flying) { hud.Banner("ALREADY ON THE WAY", "START A NEW TEST RUN", new Color(1f, 0.5f, 0.4f)); return; }
            TestMode.PretendAllDiamonds = true;
            float cx = (Gen.W - ArenaRoom.AW) / 2 + ArenaRoom.AW * 0.5f;
            player.pos = new Vector2(cx - 8f, 1 - ArenaRoom.Final + 1.2f);
            player.vel = Vector2.zero;
            cam.Snap(player.pos);
            world.Stream(cam.pos.y, cam.halfH);
            if (toAdd.Count > 0) { ents.AddRange(toAdd); toAdd.Clear(); }
            foreach (var e in ents)
                if (!e.dead && e is BossArena ba && !ba.Started) ba.MarkCleared();   // every hall counts as beaten
            if (diamond != null && !diamond.Unlocked) diamond.Unlock();
            TestMode.Open = false; TestMode.Setup = false;
            hud.Banner("THE GOD-DIAMOND", "AS IF EVERY DIGGER HAD ONE. TOUCH IT", new Color(0.6f, 0.9f, 1f));
        }

        // TEST MODE: drop into the next unused boss arena and fight the chosen boss there
        public void TestFightBoss(int kind)
        {
            if (World.SkyMode) { hud.Banner("NOT FROM UP HERE", "START A NEW TEST RUN", new Color(1f, 0.5f, 0.4f)); return; }
            int target = -1;
            foreach (int d in ArenaRoom.Depths(0))
            {
                if (d == ArenaRoom.Final) continue;
                bool used = false;
                foreach (var e in ents) if (!e.dead && e is BossArena ba && ba.Depth == d && ba.Started) used = true;
                if (!used) { target = d; break; }
            }
            if (target < 0) { hud.Banner("ALL ARENAS USED", "START A NEW TEST RUN FOR MORE FIGHTS", new Color(1f, 0.5f, 0.4f)); return; }
            BossArena.ForceKind = kind;
            float cx = (Gen.W - ArenaRoom.AW) / 2 + ArenaRoom.AW * 0.5f;
            player.pos = new Vector2(cx - 6f, 1 - target + 1.2f);
            player.vel = Vector2.zero;
            cam.Snap(player.pos);
            world.Stream(cam.pos.y, cam.halfH);
            TestMode.Open = false;
        }

        void BeginRun(RunSave save, bool menu = false, bool test = false)
        {
            if (save == null && !menu && !test) SaveGame.Delete();   // a new adventure replaces any saved one
            if (!test) TestMode.On = false;
            menuWorld = menu;
            if (world != null) world.DestroyAll();
            if (runRoot != null) Destroy(runRoot.gameObject);
            runRoot = new GameObject("Run").transform;
            ents.Clear(); toAdd.Clear(); altars.Clear(); corpses.Clear();
            boss = null; boss2 = null;
            diamond = null;
            whiteAlpha = 0f;
            World.SkyMode = false;
            BossArena.ForceKind = -1;   // a test boss pick never carries over into another run
            FloorFire.ClearAll(); ArenaWater.ClearAll();
            skyWhite = 0f; cloudFade = 0f;
            Ascent.Active = null; Ascent.Cover = 0f;
            revealT = 0f;
            BoulderBody.All.Clear();
            Fire.Reset();
            Electric.Reset();
            FriendlyWorm.Alive = 0; GhostGun.Alive = 0; Decoy.Active = null;
            Time.timeScale = 1f;
            if (!menu) music.PlayLevel(2.5f);
            seed = save != null ? save.seed : Random.Range(1, 999999);
            if (save != null) charIndex = Mathf.Clamp(save.charIndex, 0, Chars.All.Count - 1);
            rng = new System.Random(seed);
            hud.Reset();
            Gen.Init(seed);
            world = new World(runRoot);
            fluids = new Fluids(world, runRoot);
            fx = new FX(runRoot);
            lighting = new Lighting(runRoot);
            maxDepth = 0; deadT = 0f; time = 0f; freeze = 0f; newBest = false;
            state = menu ? State.Title : State.Playing;
            if (!menu) PlayerPrefs.SetInt("digdeep_char", charIndex);
            player = new Player(new Vector2(Gen.CampCx - 5.5f, 1 - Gen.CampFloor + Player.Half.y + 0.002f), Chars.All[charIndex]);
            if (menu) player.Hide();
            camp = new Camp(CampFloor, menu ? -1 : charIndex, menu);
            Spawn(camp);
            if (save != null)
            {
                // the world exactly as you left it (top to bottom, so everything spawns in order), then you
                save.chunks.Sort((a, b) => b.ci.CompareTo(a.ci));
                foreach (var cs in save.chunks) world.Inject(cs);
                ents.AddRange(toAdd); toAdd.Clear();
                foreach (var a in altars)
                    for (int i = 0; i < save.doneAltarX.Count; i++)
                        if (Mathf.Abs(a.pos.x - save.doneAltarX[i]) < 0.2f && Mathf.Abs(a.pos.y - save.doneAltarY[i]) < 0.2f) a.done = true;
                foreach (var e in ents)
                    if (e is BossArena ba && save.clearedArenas.Contains(ba.Depth)) ba.MarkCleared();
                player.RestoreFrom(save);
                maxDepth = save.maxDepth;
                time = save.time;
                if (save.ascent)
                {
                    // back at the surface, the God not yet fought: fly up when you're ready
                    player.pos = new Vector2(save.ax, save.ay);
                    player.vel = Vector2.zero;
                    Spawn(new Ascent(true));
                    Spawn(new SkyBackdrop());
                    music.PlayIntro(2f);
                }
            }
            aim = player.pos + Vector2.right * 3f;
            cam.Snap(menu ? MenuFocus : player.pos);
            world.Stream(cam.pos.y, cam.halfH);
        }

        // the chosen digger steps out of the lineup and the run begins, right where you are
        void StartChosen()
        {
            if (camp == null || !camp.lineup) { NewRun(); return; }
            SaveGame.Delete();
            menuWorld = false;
            PlayerPrefs.SetInt("digdeep_char", charIndex);
            Vector2 feet = camp.LineFeet(charIndex);
            camp.EndLineup(charIndex);
            camp.Kill();
            player.Kill();
            player = new Player(feet + Vector2.up * (Player.Half.y + 0.002f), Chars.All[charIndex]);
            camp = new Camp(CampFloor, charIndex);
            Spawn(camp);
            player.PopIn();
            state = State.Playing;
            music.PlayLevel(2f);
            maxDepth = 0; deadT = 0f; time = 0f; newBest = false;
            aim = player.pos + Vector2.right * 3f;
            sfx.Play("buy", 0.7f);
        }

        void EnterSelect()
        {
            if (!menuWorld || world == null) { BeginRun(null, true); music.PlayMenu(); }
            state = State.Select;
            charIndex = 0;   // the leftmost digger is up first
            sfx.Play("swing", 0.5f, 1.2f);
        }

        // the menu world idles behind the menus: the fire crackles, water flows, the camera drifts between shots
        void MenuWorldTick()
        {
            if (world == null || player == null) return;
            hud.ClearFrame();
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            time += dt;
            float fy = MenuFocus.y;
            for (int i = 0; i < ents.Count; i++)
            {
                var e = ents[i];
                if (e.dead || e is Enemy) continue;   // nothing hunts the stand-in
                if (!e.AlwaysActive && Mathf.Abs(e.pos.y - fy) >= 24f) continue;
                e.Tick(dt);
            }
            if (toAdd.Count > 0) { ents.AddRange(toAdd); toAdd.Clear(); }
            ents.RemoveAll(e => e.dead);
            fluids.Tick(dt, cam.pos.y, cam.halfH);
            fx.Tick(dt);
            cam.Tick(dt, MenuFocus, Vector2.zero);
            world.Stream(cam.pos.y, cam.halfH);
            BuildLights(dt);
        }

        // the mouse in world space (menus)
        Vector2 MouseWorld()
        {
            var m = GameInput.MouseScreen;
            Vector3 w = cam.cam.ScreenToWorldPoint(new Vector3(m.x, m.y, 10f));
            return new Vector2(w.x, w.y);
        }

        Vector2 lastMouse;

        // Mouse over a weapon crate or TNT: say what it is.
        void HoverBlocks()
        {
            if (GameInput.UsingStick || state != State.Playing) return;
            int x = Mathf.FloorToInt(aim.x), y = Mathf.FloorToInt(aim.y);
            Block b = world.Peek(x, y);
            Vector2 at = new Vector2(x + 0.5f, y + 1.3f);
            if (b == Block.Crate)
            {
                var r = Crates.RarityAt(x, y);
                hud.Label(at, (r == Rarity.Relic ? "RELIC" : r.ToString().ToUpperInvariant()) + " WEAPON CRATE", DB.RarityColor(r));
                hud.Label(at + Vector2.up * 0.4f, "BREAK IT FOR A WEAPON", new Color(0.8f, 0.8f, 0.8f));
            }
            else if (b == Block.TNT) hud.Label(at, "TNT: SHOOT IT, OR DROP SOMETHING ON IT", new Color(1f, 0.45f, 0.35f));
        }

        // Bodies pile up for a while, but not forever.
        public void RegisterCorpse(Corpse c)
        {
            corpses.RemoveAll(x => x.dead);
            corpses.Add(c);
            while (corpses.Count > 40) { corpses[0].Kill(); corpses.RemoveAt(0); }
        }

        public void Spawn(Entity e) => toAdd.Add(e);
        public void AddAltar(Altar a) { Spawn(a); altars.Add(a); }
        public void HitStop(float t) { if (Settings.HitStop) freeze = Mathf.Max(freeze, t); }
        public readonly OptionsMenu options = new OptionsMenu();
        float fps = 60f;

        // alt-tabbing away pauses the run (if that option is on)
        void OnApplicationFocus(bool focus)
        {
            if (focus || !Settings.PauseOnFocusLoss || state != State.Playing) return;
            state = State.Paused;
            music.SetDuck(true);
            hud.ResetPausePage();
        }
        public void Popup(Vector2 p, string s, Color c) => hud.Popup(p, s, c);

        // ------------------------------------------------------------ loop

        void Update()
        {
            float rdt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            GameInput.Tick(rdt);
            if (rdt > 0f) fps = Mathf.Lerp(fps, 1f / rdt, 0.05f);
            music.Tick(rdt);
            // the OS cursor hides while you're aiming; the HUD draws a crosshair instead
            Cursor.visible = state != State.Playing || (TestMode.On && TestMode.Open);

            if (state == State.Intro)
            {
                introGrace -= rdt;
                story.TickIntro(rdt);
                if (story.IntroDone || (introGrace <= 0f && GameInput.AnyPressed())) GoTitle();
                return;
            }
            if (state == State.Options) { options.Tick(rdt); return; }
            if (state == State.Credits)
            {
                if (GameInput.AnyPressed()) { state = State.Title; sfx.Play("swing", 0.5f, 1.2f); }
                return;
            }
            if (state == State.Title)
            {
                MenuWorldTick();
                var items = Story.TitleItems;
                int n = items.Length;
                story.titleSel = Mathf.Clamp(story.titleSel, 0, n - 1);
                if (GameInput.Pressed(Btn.Up)) { story.titleSel = (story.titleSel + n - 1) % n; sfx.Play("swing", 0.5f, 1.4f); }
                if (GameInput.Pressed(Btn.Down)) { story.titleSel = (story.titleSel + 1) % n; sfx.Play("swing", 0.5f, 1.4f); }
                // the mouse: hovering picks a line, clicking it confirms
                int over = story.TitleItemAtMouse();
                bool moved = (GameInput.MouseScreen - lastMouse).sqrMagnitude > 1f;
                lastMouse = GameInput.MouseScreen;
                if (over >= 0 && over < n && moved && over != story.titleSel) { story.titleSel = over; sfx.Play("swing", 0.3f, 1.6f); }
                bool click = GameInput.MouseClicked() && over >= 0 && over < n;
                if (click) story.titleSel = over;
                if (GameInput.Pressed(Btn.Jump) || click || (GameInput.Pressed(Btn.Attack) && !GameInput.MouseHeld()))
                {
                    sfx.Play("buy", 0.7f);
                    string pick = items[story.titleSel];
                    if (pick == Story.Continue) ContinueRun();
                    else if (pick == Story.NewRun) EnterSelect();
                    else if (pick == Story.Test) StartTestMode();
                    else if (pick == Story.Options) options.Show(State.Title);
                    else if (pick == Story.Credits) state = State.Credits;
                    else if (pick == Story.Quit) Application.Quit();
                    else GoIntro();
                }
                return;
            }
            if (state == State.Ending)
            {
                story.TickEnding(rdt);
                if (story.EndT > 2f && !endMusic) { endMusic = true; music.PlayIntro(3f); }
                if (story.EndingCanExit && GameInput.AnyPressed()) GoTitle();
                return;
            }

            if (state == State.Select)
            {
                MenuWorldTick();
                if (GameInput.Pressed(Btn.Pause)) { state = State.Title; sfx.Play("swing", 0.5f, 1.2f); return; }
                int n = Chars.All.Count;
                if (GameInput.Pressed(Btn.Left)) { charIndex = (charIndex + n - 1) % n; sfx.Play("swing", 0.5f, 1.4f); }
                if (GameInput.Pressed(Btn.Right)) { charIndex = (charIndex + 1) % n; sfx.Play("swing", 0.5f, 1.4f); }
                int hov = camp != null && camp.lineup && !GameInput.UsingStick ? camp.LineAt(MouseWorld()) : -1;
                if (camp != null) camp.Hover = hov;
                if (GameInput.MouseClicked() && hov >= 0)
                {
                    // click a digger to choose them; click the chosen one again to start
                    if (hov == charIndex) { StartChosen(); return; }
                    charIndex = hov;
                    sfx.Play("swing", 0.5f, 1.4f);
                }
                if (GameInput.Pressed(Btn.Jump)) { StartChosen(); return; }
                return;
            }

            // TEST MODE panel: F1 or the pad's VIEW button
            if (TestMode.On && state == State.Playing)
            {
                if (!TestMode.Setup && (GameInput.KeyDown("F1") || GameInput.PadPressed(Pad.View)))
                {
                    if (TestMode.Open) TestMode.Open = false; else TestMode.OpenPanel(false);
                    sfx.Play("swing", 0.5f, 1.3f);
                }
                if (TestMode.Open)
                {
                    if (GameInput.Pressed(Btn.Pause))
                    {
                        // on the set-up screen ESC is "never mind": back to the menu with nothing equipped
                        if (TestMode.Setup) QuitTest(); else TestMode.Open = false;
                        return;
                    }
                    hud.ClearFrame();
                    TestMode.Tick(this);
                    BuildLights(0f);
                    return;
                }
            }
            if (GameInput.Pressed(Btn.Pause) && state != State.Dead)
            {
                state = state == State.Paused ? State.Playing : State.Paused;
                music.SetDuck(state == State.Paused);
                hud.ResetPausePage();
            }
            if (state == State.Paused)
            {
                if (GameInput.Pressed(Btn.Swap) || (hud.QuitHovered && GameInput.Pressed(Btn.Attack))) { SaveAndQuit(); return; }
                if (GameInput.Pressed(Btn.Interact) || (hud.OptionsHovered && GameInput.Pressed(Btn.Attack))) { options.Show(State.Paused); return; }
                if (GameInput.Pressed(Btn.Left)) { hud.PageTurn(-1); sfx.Play("swing", 0.4f, 1.4f); }
                if (GameInput.Pressed(Btn.Right)) { hud.PageTurn(1); sfx.Play("swing", 0.4f, 1.4f); }
                return;
            }

            if (freeze > 0f) { freeze -= rdt; cam.Apply(); return; }   // hitstop: the whole world holds its breath

            hud.ClearFrame();
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (player != null && player.Cursed(Curse.Speed)) dt *= 1.5f;   // CURSE OF SPEED: everything 1.5x as fast
            time += dt;

            if (state == State.Dead)
            {
                deadT += dt;
                if (deadT > 0.4f && (GameInput.Pressed(Btn.Pause) || (hud.DeadQuitHovered && GameInput.MouseClicked()))) { QuitFromDeath(); return; }
                if (deadT > 1f && (GameInput.Pressed(Btn.Jump) || (GameInput.Pressed(Btn.Attack) && !hud.ChangeHovered && !hud.DeadQuitHovered))) { NewRun(); return; }
                if (!TestMode.On && deadT > 1f && (GameInput.Pressed(Btn.Interact) || GameInput.Pressed(Btn.Up) || (hud.ChangeHovered && GameInput.MouseClicked()))) { BeginRun(null, true); music.PlayMenu(); EnterSelect(); return; }
            }

            UpdateAim();
            HoverBlocks();
            player.Tick(dt);
            float py = player.pos.y;
            for (int i = 0; i < ents.Count; i++)
            {
                var e = ents[i];
                if (e.dead) continue;
                if (!e.AlwaysActive && Mathf.Abs(e.pos.y - py) >= 24f) continue;
                if (e is Enemy sk && sk.Shocked) sk.ShockedTick(dt);
                else if (e is Enemy fz && fz.Frozen) fz.FrozenTick(dt);
                else if (e is Enemy ke && ke.Kicked) ke.KickedTick(dt);
                else e.Tick(dt);
            }
            if (toAdd.Count > 0) { ents.AddRange(toAdd); toAdd.Clear(); }
            ents.RemoveAll(e => e.dead);
            altars.RemoveAll(a => a.dead);

            // approaching the God-Diamond: time slows, the world whitens
            if (diamond != null && diamond.Unlocked && player.mode != Player.Mode.Dead && state == State.Playing)
            {
                float d = (player.pos - diamond.pos).magnitude;
                Time.timeScale = Mathf.Lerp(1f, 0.15f, Mathf.InverseLerp(6f, 1.8f, d));
                whiteAlpha = Mathf.InverseLerp(3.8f, 1.3f, d);
                if (d < 1.35f) { BeginEnding(); return; }
            }
            else if (Time.timeScale != 1f || whiteAlpha > 0f) { Time.timeScale = 1f; whiteAlpha = 0f; }
            if (skyWhite > 0f) { skyWhite = Mathf.Max(0f, skyWhite - dt * 0.5f); whiteAlpha = Mathf.Max(whiteAlpha, skyWhite); }

            fluids.Tick(dt, cam.pos.y, cam.halfH);
            Electric.Tick(dt);
            fx.Tick(dt);
            hud.Tick(dt, this);
            // the camera leans a little towards where you aim
            Vector2 look = Vector2.ClampMagnitude((aim - player.pos) * 0.22f, 3.2f);
            if (World.SkyMode) cam.Tick(dt, SkyGod.Center, Vector2.zero);   // above the clouds the view holds still
            else
            {
                // shooting up through the world the view leads ahead of you, or you'd leave it behind
                if (Ascent.Shielded) look = Vector2.up * (player.vel.y / 6f);
                cam.Tick(dt, player.pos + (player.mode == Player.Mode.Dead ? Vector2.zero : look), Ascent.Shielded ? Vector2.zero : player.vel);
                world.Stream(cam.pos.y, cam.halfH);
            }
            if (cloudFade > 0f) cloudFade = Mathf.Max(0f, cloudFade - dt * 0.7f);
            BuildLights(dt);
        }

        // Is the mouse cursor over this world position?
        public bool MouseOver(Vector2 p, float r) => !GameInput.UsingStick && (aim - p).sqrMagnitude < r * r;

        // gamepad aim: the reticle rides a fixed circle around you, twin-stick style; holding LOOK / AIM doubles it
        Vector2 padAimDir = Vector2.right;
        float padAimDist = 4.5f;
        public float PadAimDist => padAimDist;

        void UpdateAim()
        {
            if (GameInput.UsingStick)
            {
                var st = GameInput.Stick;
                if (st.sqrMagnitude > 0.09f) padAimDir = st.normalized;
                float want = GameInput.AimRadius * (GameInput.Held(Btn.Zoom) ? 2f : 1f);
                padAimDist = Mathf.MoveTowards(padAimDist, want, 40f * Time.unscaledDeltaTime);
                aim = player.pos + padAimDir * padAimDist;
                return;
            }
            var m = GameInput.MouseScreen;
            Vector3 w = cam.cam.ScreenToWorldPoint(new Vector3(m.x, m.y, 10f));
            aim = new Vector2(w.x, w.y);
        }

        void LateUpdate() { if (world != null) world.ApplyDirty(); }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (state == State.Intro) story.DrawIntro();
            else if (state == State.Credits) story.DrawCredits();
            else if (state == State.Options)
            {
                // the options sit on top of whatever they were opened from
                if (options.FromPause) hud.Draw(this); else story.DrawTitle(this);
                options.Draw();
            }
            else if (state == State.Title) story.DrawTitle(this);
            else if (state == State.Ending) story.DrawEnding(this);
            else if (hud != null)
            {
                hud.Draw(this);
                if (TestMode.On && state == State.Playing) TestMode.Draw(this);
                float cover = Mathf.Max(Ascent.Flying ? Ascent.Cover : 0f, cloudFade);
                if (cover > 0f) DrawClouds(cover);
                if (whiteAlpha > 0f)
                {
                    GUI.color = new Color(1f, 1f, 1f, whiteAlpha);
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Art.White);
                    GUI.color = Color.white;
                }
            }
            if (Settings.ShowFps) PixelFont.Draw(4f, 4f, Mathf.RoundToInt(fps) + " FPS", Mathf.Max(1f, Mathf.Floor(Screen.height / 540f)), new Color(0.6f, 1f, 0.6f));
        }

        // the cloud bank on the way up: soft puffs rushing down past you, thick enough to hide everything
        void DrawClouds(float a)
        {
            float W = Screen.width, H = Screen.height;
            var old = GUI.color;
            GUI.color = new Color(0.9f, 0.88f, 0.97f, a);
            GUI.DrawTexture(new Rect(0, 0, W, H), Art.White);
            var tex = Art.Circle.texture;
            float u = H / 6f;
            for (int i = 0; i < 18; i++)
            {
                float r = u * (0.8f + (i * 37 % 10) * 0.08f);
                float x = (i * 0.618f % 1f) * (W + r) - r * 0.5f;
                float y = ((i * 0.37f + Time.time * 0.9f) % 1.3f) * H - r * 0.6f;
                GUI.color = i % 3 == 0 ? new Color(1f, 1f, 1f, a) : new Color(0.78f, 0.75f, 0.9f, a);
                GUI.DrawTexture(new Rect(Mathf.Round(x), Mathf.Round(y), r * 1.6f, r), tex);
            }
            GUI.color = old;
        }

        void BuildLights(float dt)
        {
            lighting.Begin(cam.pos, cam.halfW, cam.halfH);
            if (player.mode != Player.Mode.Dead)
                lighting.Add(player.pos, 5.2f * player.s.lightMul * (player.Cursed(Curse.Darkness) ? 0.5f : 1f) * (1f + Mathf.Sin(time * 7f) * 0.02f), 1f);
            else
                lighting.Add(player.pos, 3f, 0.6f);

            foreach (var e in ents)
                if (!e.dead && !(e is Altar) && lighting.InView(e.pos, 8f)) e.AddLight(lighting);
            // altars may light things far from their own position (burning fuse tip, finish flag)
            foreach (var a in altars)
                if (!a.dead) a.AddLight(lighting);

            int x0 = Mathf.Max(0, Mathf.FloorToInt(cam.pos.x - cam.halfW - 3)), x1 = Mathf.Min(World.W - 1, Mathf.CeilToInt(cam.pos.x + cam.halfW + 3));
            int y0 = Mathf.FloorToInt(cam.pos.y - cam.halfH - 3), y1 = Mathf.CeilToInt(cam.pos.y + cam.halfH + 3);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Block tb = world.Peek(x, y);
                    var d = Tiles.Def(tb);
                    if (d.light <= 0f) continue;
                    float flick = 1f + Mathf.Sin(time * 5f + x * 1.3f + y * 0.7f) * 0.06f;
                    if (tb == Block.Chest || tb == Block.Crate)
                    {
                        // treasure pulses and twinkles so it's hard to walk past
                        flick = 1f + Mathf.Sin(time * 3.5f + x) * 0.25f;
                        if (Random.value < dt * 3f)
                        {
                            Color tw = tb == Block.Chest ? new Color(1f, 0.9f, 0.4f) : (Color)DB.RarityColor(Crates.RarityAt(x, y));
                            fx.Burst(new Vector2(x + Random.Range(0.1f, 0.9f), y + Random.Range(0.3f, 1.1f)), 1, tw, 0.6f, 0.6f, -2f, 1.2f, false);
                        }
                    }
                    lighting.Add(new Vector2(x + 0.5f, y + 0.5f), d.light * flick, d.lightI);
                }

            fluids.AddLights(lighting, cam.pos, cam.halfW, cam.halfH, time);
            Electric.AddLights(lighting);
            if (revealT > 0f)
            {
                revealT -= dt;
                lighting.AddRect(revealC - revealH, revealC + revealH, RevealFade);
            }
            lighting.End(dt, (World.SkyMode ? 0f : Darkness(cam.pos.y)) * player.s.darkMul * (1f - Settings.Brightness * 0.6f));   // BRIGHTNESS option
            fluids.Render(cam.pos, cam.halfW, cam.halfH, time);
        }

        static float Darkness(float camY)
        {
            if (camY > Gen.SurfaceTop) return Mathf.Lerp(0.25f, 0f, (camY - Gen.SurfaceTop) / 10f);   // out under the open sky
            float d = -camY;
            if (d < 3f) return Mathf.Lerp(0.25f, 0.62f, Mathf.InverseLerp(-6f, 3f, d));
            return Mathf.Lerp(0.62f, 0.93f, Mathf.Clamp01((d - 3f) / 25f)) + Mathf.Clamp01((d - 100f) / 200f) * 0.04f;
        }

        // ------------------------------------------------------------ world events

        public void OnChunkGenerated(Chunk c)
        {
            Enemy.GenSpawning = true;
            try { PopulateChunk(c); }
            finally { Enemy.GenSpawning = false; }
        }

        void PopulateChunk(Chunk c)
        {
            var r = rng;
            for (int ly = 0; ly < World.CH; ly++)
                for (int x = 2; x < World.W - 2; x++)
                {
                    int y = c.Y0 + ly;
                    int depth = -y;
                    if (depth < 18) continue;   // the camp itself stays safe
                    if (Gen.InCarve(x, depth)) continue;
                    Block b = c.t[ly * World.W + x];
                    // the first stretch below the camp is busier than it used to be
                    float pr = Mathf.Min(0.013f + depth * 0.00007f, 0.03f);
                    float roll = (float)r.NextDouble();
                    byte liquid = c.lt[ly * World.W + x];
                    bool storm = Biomes.Index(depth) == Biomes.Storm;
                    if (liquid != Fluids.None)
                    {
                        // slimes live in the water (eels in the storm caverns)
                        if (liquid == Fluids.Water && storm && roll < pr * 3f) Spawn(new Eel(x, y, depth));
                        else if (liquid == Fluids.Water && roll < pr * 1.6f && c.liq[ly * World.W + x] > 0.6f && Fish.PoolSize(c, x, ly, 80) is int fishPool && fishPool >= 6)
                        {
                            // fish in the pools: minnows and biters, and groupers in the big ones (more biters deeper down)
                            float k2 = (float)r.NextDouble();
                            var kind = fishPool >= 45 && k2 < 0.25f ? Fish.Kind.Grouper : k2 < 0.6f - Mathf.Min(0.25f, depth / 800f) ? Fish.Kind.Minnow : Fish.Kind.Biter;
                            Spawn(new Fish(x, y, depth, kind));
                        }
                        else if (liquid == Fluids.Water && roll < pr * 2.5f && Tiles.Solid(world.Peek(x, y - 1))) Spawn(new Crawler(x, y, depth, false, true));
                        continue;
                    }
                    if (b == Block.Air)
                    {
                        if (roll >= pr) continue;
                        bool floor = Tiles.Solid(world.Peek(x, y - 1));
                        bool ceil = Tiles.Solid(world.Peek(x, y + 1));
                        float k = (float)r.NextDouble();
                        bool jungle = Biomes.Index(depth) == Biomes.Jungle;
                        float k2 = (float)r.NextDouble();
                        if (k2 < 0.3f && SpawnElemental(x, y, depth, floor, ceil, r)) { }
                        else if (floor && storm && k < 0.3f) Spawn(new TeslaCoil(x, y, depth));
                        else if (floor && storm && k < 0.6f) Spawn(new Clank(x, y, depth));
                        else if (ceil && storm && k < 0.5f) Spawn(new Drone(x, y, depth));
                        else if (floor && jungle && k < 0.4f) Spawn(new Snapper(x, y, depth));
                        else if (ceil && jungle && k < 0.45f) Spawn(new SeedPod(x, y, depth));
                        else if (floor)
                        {
                            if (depth > 45 && k < 0.18f) Spawn(new Spitter(x, y, depth));
                            else if (depth > 28 && k < 0.36f) SpawnRaiders(c, x, y, depth, r);
                            else if (depth > 55 && k < 0.47f) Spawn(new Beetle(x, y, depth));
                            else if (depth > 100 && k < 0.6f) Spawn(new Crawler(x, y, depth, true));
                            else Spawn(new Crawler(x, y, depth, false));
                        }
                        else if (ceil && depth > 12)
                        {
                            // bats roost in packs of 1-3 along the same ceiling
                            bool impPack = depth > 200 && k < 0.4f;
                            int pack = 1 + r.Next(3);
                            for (int i = 0, bx = x; i < pack && bx < World.W - 2; i++, bx += 1 + r.Next(2))
                                if (!Tiles.Solid(world.Peek(bx, y)) && Tiles.Solid(world.Peek(bx, y + 1))) Spawn(new Bat(bx, y, depth, impPack));
                        }
                        else if (depth > 200 && k < 0.3f) Spawn(new Bat(x, y, depth, true));
                    }
                    else if (b == Block.Chest && r.NextDouble() < 0.004) Spawn(new MimicLurker(x, y));   // insanely rare
                    else if (b == Block.Dirt && depth > 22 && roll < pr * 0.15f)
                    {
                        world.PokeRaw(c, x, y, Block.Air);
                        Spawn(new Mole(x, y, depth));
                    }
                    else if ((b == Block.Dirt || b == Block.Stone || b == Block.HardStone) && depth > 55 && roll < pr * 0.02f)
                        Spawn(new Worm(x, y, depth));
                }

            // shops and puzzle altars whose floor lies in this chunk
            int dTop = -(c.Y0 + World.CH - 1), dBot = -c.Y0;
            foreach (var carve in Gen.Carves)
            {
                if (carve.spawned) continue;
                int a = carve.AnchorDepth;
                if (a < dTop || a > dBot) continue;
                carve.spawned = true;
                carve.Spawn(this, r);
            }
        }

        // The elemental creatures of each biome. True if something was spawned.
        bool SpawnElemental(int x, int y, int depth, bool floor, bool ceil, System.Random r)
        {
            double k = r.NextDouble();
            switch (Biomes.Index(depth))
            {
                case Biomes.Topsoil:
                    if (floor && depth > 35 && k < 0.5) { Spawn(new ToxicToad(x, y, depth)); return true; }
                    return false;
                case Biomes.Jungle:
                    if (ceil)
                    {
                        int n = 3 + r.Next(3);   // a swarm
                        for (int i = 0; i < n; i++) Spawn(new Bee(new Vector2(x + 0.5f + (float)r.NextDouble() * 2f - 1f, y + 0.3f - (float)r.NextDouble()), depth));
                        return true;
                    }
                    if (floor && k < 0.6) { Spawn(new ToxicToad(x, y, depth)); return true; }
                    return false;
                case Biomes.Fungal:
                    if (!floor) { Spawn(new SporePuff(x, y, depth)); return true; }
                    if (k < 0.5) { Spawn(new ToxicToad(x, y, depth)); return true; }
                    return false;
                case Biomes.Crystal:
                    if (!floor || k < 0.4) { Spawn(new Wisp(x, y, depth, true)); return true; }
                    return false;
                case Biomes.Storm:
                    if (floor) { Spawn(new SparkBeetle(x, y, depth)); return true; }
                    return false;
                case Biomes.Magma:
                    if (floor && k < 0.6) { Spawn(new MagmaSlug(x, y, depth)); return true; }
                    Spawn(new Wisp(x, y, depth, false));
                    return true;
                default:   // the forgotten kingdom: a bit of everything
                    if (floor) { if (k < 0.5) Spawn(new SparkBeetle(x, y, depth)); else Spawn(new MagmaSlug(x, y, depth)); }
                    else Spawn(new Wisp(x, y, depth, k < 0.5));
                    return true;
            }
        }

        // Raiders travel in small squads.
        void SpawnRaiders(Chunk c, int x, int y, int depth, System.Random r)
        {
            int n = 1 + (r.NextDouble() < 0.45 ? 1 : 0) + (depth > 120 && r.NextDouble() < 0.3 ? 1 : 0);
            for (int i = 0; i < n; i++)
            {
                int xx = x + i * 2 * (r.NextDouble() < 0.5 ? -1 : 1);
                if (i > 0 && (xx < 2 || xx >= World.W - 2 || Tiles.Solid(world.Peek(xx, y)) || !Tiles.Solid(world.Peek(xx, y - 1)))) continue;
                double k = r.NextDouble();
                var kind = Raider.Kind.Gunner;
                if (depth > 150 && k < 0.2) kind = Raider.Kind.Bomber;
                else if (depth > 95 && k < 0.45) kind = Raider.Kind.Marksman;
                else if (depth > 70 && k < 0.7) kind = Raider.Kind.Brute;
                Spawn(new Raider(xx, y, depth, kind));
            }
        }

        public bool HitEnemies(Vector2 c, Vector2 half, float dmg, Vector2 knock)
        {
            bool any = false;
            foreach (var e in ents)
            {
                if (e.dead || !(e is Enemy en)) continue;
                if (U.Overlap(c, half, en.pos, en.half) && en.Hurt(dmg, knock)) any = true;
            }
            return any;
        }

        public void HitAltars(Vector2 c, Vector2 half, Src src)
        {
            CampProp.HitBox(c, half, src == Src.Explosion ? 12f : src == Src.Weapon ? 3f : 5f);   // tents and logs break too
            for (int i = 0; i < altars.Count; i++)
            {
                var a = altars[i];
                if (a.dead || !a.Active) continue;
                if (U.Overlap(c, half, a.pos, a.Half)) a.OnHit(src);
            }
        }

        // Blow out a round pocket of rock (the mortar's shells): no damage to anything alive.
        public void DigPocket(Vector2 c, float r, int power)
        {
            int x0 = Mathf.FloorToInt(c.x - r), x1 = Mathf.FloorToInt(c.x + r), y0 = Mathf.FloorToInt(c.y - r), y1 = Mathf.FloorToInt(c.y + r);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if ((new Vector2(x + 0.5f, y + 0.5f) - c).magnitude > r) continue;
                    Block b = world.Peek(x, y);
                    if (b == Block.Chest || b == Block.Crate || b == Block.TNT) continue;   // leave treasure (and TNT) alone
                    world.Damage(x, y, power, true, player.GoldMul);
                }
        }

        public void OnSpecialUsed(string id, Vector2 at)
        {
            foreach (var a in altars)
                if (!a.dead && a is ReqAltar r) r.OnSpecial(id, at);
        }

        public void OnHeavyLanded(int x, int y)
        {
            foreach (var a in altars)
                if (!a.dead && a is ButtonAltar b) b.OnHeavy(x, y);
        }

        public Vector2? NearestTarget(Vector2 from, bool shops)
        {
            Vector2? best = null;
            float bd = 90f * 90f;
            foreach (var a in altars)
            {
                if (a.dead || !a.Active) continue;
                float d = (a.Target - from).sqrMagnitude;
                if (d < bd) { bd = d; best = a.Target; }
            }
            if (shops)
                foreach (var s in Gen.Shops)
                {
                    var p = new Vector2(s.CenterX + 0.5f, s.FloorY + 1.5f);
                    float d = (p - from).sqrMagnitude;
                    if (d < bd) { bd = d; best = p; }
                }
            return best;
        }

        public void OnEnemyKilled(Enemy e)
        {
            player.OnKill(e);
            var s = player.s;
            if (s.chainExplode > 0) Spawn(new Blast(e.pos, 0.12f, s.chainExplode >= 2 ? 2.6f : 1.5f, 3, true));
            player.MaybeDropPet(e.pos);   // SPIRIT LANTERN
            // CURSE OF LAVA: the fallen leave a pool of lava behind
            if (player.Cursed(Curse.Lava) && !(e is Boss) && !(e is WormSegment))
            {
                int lx = Mathf.FloorToInt(e.pos.x), ly = Mathf.FloorToInt(e.pos.y);
                if (!world.Solid(lx, ly)) { world.SetLiquid(lx, ly, Fluids.Lava, 1f); fx.Smoke(e.pos, 4, 0.8f); sfx.Play("ignite", 0.4f, 0.7f); }
            }
            // PLAGUE: a poisoned foe bursts into a cloud that poisons everything around it
            if (s.plague && e.Poisoned)
            {
                foreach (var o in ents)
                    if (!o.dead && o is Enemy oe && oe != e && (oe.pos - e.pos).sqrMagnitude < 3f * 3f) oe.Poison(3.5f, player.Dmg(0.8f));
                fx.Burst(e.pos, 26, new Color(0.55f, 1f, 0.35f), 5f, 0.8f, -2f, 1.4f, false);
                fx.Ring(e.pos, 2.8f, new Color(0.55f, 1f, 0.35f));
                sfx.Play("splat", 0.5f, 0.6f);
            }
            if (Random.value < player.Lucky(0.035f)) Spawn(new LootPickup(e.pos, DB.Ale, 0.3f));
            // CURSE OF THE RESTLESS DEAD: some of the fallen get back up as bats
            if (player.Cursed(Curse.Restless) && !(e is Bat) && !(e is Boss) && !(e is WormSegment) && Random.value < 0.3f)
            {
                var bat = new Bat(Mathf.FloorToInt(e.pos.x), Mathf.FloorToInt(e.pos.y), player.Depth, false);
                Spawn(bat);
                fx.Burst(e.pos, 14, Curses.Col(Curse.Restless), 4f, 0.6f, -3f);
                sfx.Play("bat", 0.6f, 0.7f);
            }
        }

        // Trinket effects that trigger whenever the player breaks a block.
        public void OnPlayerBrokeBlock(int x, int y, Block b)
        {
            player.OnBrokeBlock(b);
            var s = player.s;
            var c = new Vector2(x + 0.5f, y + 0.5f);
            float lucky = s.luckyChance + Mathf.Max(0f, s.luck) * 0.01f;
            if (lucky > 0f && Random.value < lucky) SpawnGold(c, 1);
            if (s.ember > 0)
            {
                fx.Burst(c, 6, new Color(1f, 0.55f, 0.15f), 5f, 0.35f, 10f);
                HitEnemies(c, new Vector2(1.5f, 1.5f), 1, Vector2.up * 3f);
                if (s.ember >= 2)
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            var d = Tiles.Def(world.Get(x + dx, y + dy));
                            if (d.solid && d.soft) world.Break(x + dx, y + dy, false);
                        }
            }
        }

        // A hit on a big gold deposit: a coin or two flies at the player.
        public void BigOreHit(Vector2 at, int size)
        {
            int n = size >= 6 ? 2 : size == 4 ? (Random.value < 0.3f ? 2 : 1) : (Random.value < 0.6f ? 1 : 0);
            Vector2 dir = (player.pos - at).normalized;
            for (int i = 0; i < n; i++)
            {
                var v = dir * Random.Range(5f, 8f) + Vector2.up * Random.Range(3f, 6f);
                Spawn(new Coin(at + dir * 0.55f, 1, v, 0.1f));
            }
            fx.Burst(at, 6, new Color(1f, 0.85f, 0.3f), 4.5f, 0.3f, 15f);
            sfx.Play("coin", 0.35f, Random.Range(1.1f, 1.4f), 0.05f);
            cam.Shake(size == 4 ? 0.1f : 0.07f);
        }

        // The deposit gives way: a fountain of gold.
        public void BigOreBreak(Vector2 c, int size, int depth)
        {
            int amount = Mathf.RoundToInt(BigOre.Payout(size, depth) * player.GoldMul);
            int count = Mathf.Min(amount, size >= 6 ? 60 : size == 4 ? 40 : 18);
            int each = amount / count, rem = amount % count;
            for (int i = 0; i < count; i++)
            {
                var p = c + new Vector2(Random.Range(-size * 0.35f, size * 0.35f), Random.Range(-size * 0.35f, size * 0.35f));
                Spawn(new Coin(p, each + (i < rem ? 1 : 0), new Vector2(Random.Range(-6.5f, 6.5f), Random.Range(7f, 14f)), 0.35f));
            }
            var gold = new Color(1f, 0.82f, 0.25f);
            fx.Burst(c, size * 22, gold, 10f, 0.9f, 22f, 2f);
            fx.Burst(c, size * 8, Color.white, 7f, 0.4f, 0f);
            fx.Ring(c, size * 1.2f, gold);
            lighting.Flash(c, size * 3.5f, 0.6f);
            sfx.Play("break", 0.9f, 0.8f);
            sfx.Play("chest", 0.8f, size == 4 ? 0.8f : 1.1f);
            sfx.Play("coin", 0.8f, 0.9f);
            cam.Shake(size == 4 ? 0.6f : 0.35f);
            HitStop(size == 4 ? 0.1f : 0.06f);
            Popup(c + Vector2.up * size * 0.6f, "+" + amount + " GOLD", gold);
        }

        public void SpawnGold(Vector2 p, int amount)
        {
            int coins = 0;
            while (amount > 0 && coins < 14)
            {
                int v = amount >= 10 ? 10 : amount >= 5 ? 5 : 1;
                if (coins == 13) v = amount;
                Spawn(new Coin(p, v));
                amount -= v;
                coins++;
            }
        }

        public void OpenChest(Vector2 p)
        {
            if (MimicLurker.TryWake(p)) return;   // it was never a chest
            sfx.Play("chest", 0.8f);
            fx.Burst(p, 26, new Color(1f, 0.85f, 0.3f), 7f, 0.7f, 10f);
            lighting.Flash(p, 4f, 0.5f);
            int depth = Mathf.Max(0, -Mathf.FloorToInt(p.y));
            SpawnGold(p, 4 + rng.Next(8) + depth / 15);
            int n = player.s.clover && Random.value < player.Lucky(0.25f) ? 2 : 1;
            for (int i = 0; i < n; i++)
            {
                var d = DB.RollChest(depth, player, rng);
                if (d != null) Spawn(new LootPickup(p, d));
            }
        }

        // Weapon crates: a weapon of the crate's rarity, maybe with modifiers.
        public void OpenCrate(Vector2 p, Rarity rar)
        {
            var col = DB.RarityColor(rar);
            sfx.Play("break", 0.7f, 1.2f);
            sfx.Play("reload", 0.8f, 0.9f);
            sfx.Play("chest", 0.6f, rar == Rarity.Relic ? 0.7f : 1.2f);
            fx.Burst(p, 22, col, 7f, 0.6f, 10f);
            fx.Debris(p, U.C(84, 90, 104), 10);
            fx.Ring(p, rar == Rarity.Relic ? 3f : 1.6f, col);
            lighting.Flash(p, rar >= Rarity.Rare ? 6f : 3.5f, 0.5f);
            cam.Shake(rar >= Rarity.Rare ? 0.25f : 0.12f);
            int depth = Mathf.Max(0, -Mathf.FloorToInt(p.y));
            var w = Crates.Roll(depth, rar, player, rng);
            Spawn(new WeaponPickup(p, w, 0.5f));
            if (w.mods.Count > 0) Popup(p + Vector2.up * 1.6f, w.mods.Count >= 3 ? "!!! TRIPLE MODDED !!!" : w.mods.Count == 2 ? "DOUBLE MODDED!" : "MODDED!", w.Color);
            SpawnGold(p, 2 + rng.Next(4));
        }

        // Puzzle altar payout: something you shouldn't have yet.
        public void RareReward(Vector2 p)
        {
            sfx.Play("chest", 0.9f, 0.9f);
            sfx.Play("buy", 0.7f);
            fx.Burst(p, 40, new Color(1f, 0.85f, 0.3f), 9f, 0.9f, 4f);
            fx.Ring(p, 3f, new Color(1f, 0.85f, 0.3f));
            lighting.Flash(p, 8f, 0.8f);
            cam.Shake(0.3f);
            int depth = Mathf.Max(0, -Mathf.FloorToInt(p.y));
            // rewards are rolled as if you were already in the next biome
            int next = depth + 60;
            foreach (var b in Biomes.All) if (b.start > depth) { next = b.start + 15; break; }
            double k = rng.NextDouble();
            if (k < 0.35)
            {
                var cw = Crates.Roll(next, Crates.RarityFor(next, (float)rng.NextDouble() * 0.6f), player, rng);
                Spawn(new WeaponPickup(p, cw, 0.6f));
                SpawnGold(p, 10 + depth / 8);
                return;
            }
            var d = k < 0.6 ? DB.RollRare(next, player, rng) : (DB.RollTrinket(next, player, rng) ?? DB.RollRare(next, player, rng));
            if (d is AttackDef ad)
            {
                // out-of-depth weapons from altars always come with a modifier
                var w = new Weapon(ad);
                Mods.AddRandom(w, rng);
                if (rng.NextDouble() < 0.25) Mods.AddRandom(w, rng);
                Spawn(new WeaponPickup(p, w, 0.6f));
            }
            else Spawn(new LootPickup(p, d, 0.6f, true));
            SpawnGold(p, 10 + depth / 8);
        }

        float lastBoom;

        // Explosions are mini nukes: oversized fireball, smoke, a flash of white, bass, shake, scorch marks.
        float prismWindowT;
        int prismInWindow;

        void PlayerBlastExtras(Vector2 c, float r, float dmg)
        {
            var s = player.s;
            // NAPALM: the floor the blast touched keeps burning
            if (s.napalm)
            {
                int x0 = Mathf.FloorToInt(c.x - r), x1 = Mathf.FloorToInt(c.x + r), y0 = Mathf.FloorToInt(c.y - r), y1 = Mathf.FloorToInt(c.y + r);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        if ((new Vector2(x + 0.5f, y + 0.5f) - c).magnitude <= r + 0.3f && !world.Solid(x, y) && world.Solid(x, y - 1) && world.LiquidType(x, y) == Fluids.None)
                            Fire.Surface(x, y, Fire.Life, true);
            }
            // SHRAPNEL PRISM: lasers burst out of the blast. 6 of them, roughly evenly spread but never quite the same,
            // more for bigger blasts (and 4 more with DEATH STAR, which also makes them explode)
            if (s.prism > 0)
            {
                if (time - prismWindowT > 0.25f) { prismWindowT = time; prismInWindow = 0; }
                // more lasers with every laser trinket and extra barrel you own (each laser then carries all your shot effects)
                int n = 6 + Mathf.Max(0, Mathf.RoundToInt((r - 1.2f) * 2.5f)) + (s.prism >= 2 ? 4 : 0)
                        + (s.shotLaser ? 2 : 0) + (s.megaLaser ? 3 : 0) + (Mathf.Max(1, s.multiShot) - 1) * 2;
                n = Mathf.Min(n, 60 - prismInWindow);   // a cap, so chain reactions can't flood the screen
                if (n <= 0) return;
                prismInWindow += n;
                float a0 = Random.Range(0f, 360f), step = 360f / n;
                for (int i = 0; i < n; i++)
                {
                    float a = a0 + i * step + Random.Range(-step * 0.3f, step * 0.3f);
                    Vector2 dir = U.Rot(Vector2.right, a);
                    Spawn(Bullet.Prism(player, c + dir * Mathf.Min(0.4f, r * 0.3f), dir, Mathf.Max(1f, dmg * 0.35f), s.prism >= 2));
                }
                sfx.Play("laser", 0.5f, 0.8f, 0.1f);
            }
        }

        // byPlayer: a blast you caused (your shots, bombs, chain kills). Those can spray SHRAPNEL PRISM lasers and NAPALM.
        public void Explode(Vector2 c, float r, float dmg, bool safe = false, bool hurtBosses = true, bool byPlayer = false)
        {
            r *= player.s.blastMul;
            dmg *= player.s.blastMul;   // BLACK POWDER and friends make blasts bigger *and* stronger
            fx.Explosion(c, r);
            sfx.Play("explode", 0.95f, Random.Range(0.85f, 1.1f), 0.1f);
            sfx.Play("boom", Mathf.Clamp01(0.45f + r * 0.18f), Random.Range(0.85f, 1.1f), 0.05f);
            cam.Shake(Mathf.Min(1f, 0.35f + r * 0.15f));
            // explosions in quick succession build up a rumble that lingers (chain reactions!)
            cam.AddRumble(time - lastBoom < 0.6f ? 0.18f : 0.06f);
            lastBoom = time;
            HitStop(Mathf.Min(0.1f, 0.03f + r * 0.015f));
            lighting.Flash(c, r * 4f, 0.5f);
            if (r >= 2.4f) hud.ScreenFlash(Mathf.Min(0.8f, r * 0.2f));
            Fire.Sparks(c, r);   // sparks fly a bit past the blast and light grass, wood and enemies

            // blasts throw boulders around instead of just cracking them
            for (int i = BoulderBody.All.Count - 1; i >= 0; i--)
            {
                var bb = BoulderBody.All[i];
                Vector2 bd = bb.pos - c;
                float dist = bd.magnitude;
                if (dist >= r + 1.5f) continue;
                if (dist <= r) bb.Damage(Mathf.CeilToInt(dmg * 1.5f * (1f - dist / r * 0.5f)));   // the same damage a block there would take
                if (!bb.dead) bb.Push(bd.normalized * Mathf.Lerp(14f, 5f, dist / (r + 1.5f)) + Vector2.up * 4f);
            }
            int bx0 = Mathf.FloorToInt(c.x - r - 1f), bx1 = Mathf.FloorToInt(c.x + r + 1f), by0 = Mathf.FloorToInt(c.y - r - 1f), by1 = Mathf.FloorToInt(c.y + r + 1f);
            for (int y = by0; y <= by1; y++)
                for (int x = bx0; x <= bx1; x++)
                {
                    if (world.Peek(x, y) != Block.Boulder) continue;
                    Vector2 bd = new Vector2(x + 0.5f, y + 0.5f) - c;
                    float dist = bd.magnitude;
                    if (dist > r + 1f) continue;
                    if (dist <= r)
                    {
                        // it takes the blast's damage first: if that breaks it, it's gone; otherwise it flies off cracked
                        int bdmg = Mathf.CeilToInt(dmg * 1.5f * (1f - dist / r * 0.5f));
                        if (world.Hp(x, y) <= bdmg) { world.Damage(x, y, bdmg, false); continue; }
                        world.SetHp(x, y, world.Hp(x, y) - bdmg);
                    }
                    world.PushBoulder(x, y, (dist < 0.1f ? Vector2.up : bd / dist) * Mathf.Lerp(16f, 6f, dist / (r + 1f)) + Vector2.up * 5f);
                }
            int x0 = Mathf.FloorToInt(c.x - r), x1 = Mathf.FloorToInt(c.x + r);
            int y0 = Mathf.FloorToInt(c.y - r), y1 = Mathf.FloorToInt(c.y + r);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = (new Vector2(x + 0.5f, y + 0.5f) - c).magnitude;
                    if (d > r) continue;
                    world.Damage(x, y, Mathf.CeilToInt(dmg * 1.5f * (1f - d / r * 0.5f)), false);
                }
            // scorch marks on whatever survived
            for (int i = 0; i < 18 + r * 10; i++)
            {
                Vector2 q = c + Random.insideUnitCircle * (r + 0.6f);
                world.Stain(q, new Color(0.08f, 0.06f, 0.06f, 0.55f), 2);
            }
            foreach (var e in ents)
            {
                if (e.dead) continue;
                if (e is Enemy en)
                {
                    if (!hurtBosses && en is Boss) continue;
                    Vector2 v = en.pos - c;
                    if (v.magnitude < r + 0.4f) en.TakeShot(dmg, v.normalized * 11f + Vector2.up * 6f);
                }
                else if (e is Corpse co)
                {
                    Vector2 v = co.pos - c;
                    if (v.magnitude < r * 1.6f) co.Push(v.normalized * 14f + Vector2.up * 7f);
                }
            }
            HitAltars(c, new Vector2(r, r), Src.Explosion);
            if (byPlayer) PlayerBlastExtras(c, r, dmg);
            float pd = (player.pos - c).magnitude;
            if (!safe && !player.s.blastProof && player.mode != Player.Mode.Dead && pd < r + 0.3f)
                player.Hurt(Mathf.RoundToInt(70f + maxDepth * 0.3f), c);
            // every blast throws the player away, even ones that can't hurt them (rocket jumps!)
            float reach = r * 1.7f + 0.5f;
            if (player.mode != Player.Mode.Dead && pd < reach)
            {
                Vector2 dir = pd < 0.15f ? Vector2.up : (player.pos - c) / pd;
                dir = (dir + Vector2.up * 0.35f).normalized;
                float f = Mathf.Lerp(24f, 7f, pd / reach) * Mathf.Clamp(0.6f + r * 0.18f, 0.7f, 1.4f);
                player.Launch(dir * f);
            }
        }

        public void OnPlayerDied()
        {
            SaveGame.Delete();   // the run is over: nothing to continue
            music.Stop(2.5f);
            state = State.Dead;
            deadT = 0f;
            if (maxDepth > best)
            {
                best = maxDepth;
                newBest = true;
                PlayerPrefs.SetInt("digdeep_best", best);
                PlayerPrefs.Save();
            }
        }
    }
}
