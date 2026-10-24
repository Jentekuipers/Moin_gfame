using UnityEngine;

namespace DigDeep
{
    // Intro slideshow (Undertale-style framing: black screen, square painting up top, typed text below),
    // the Start screen, and the ending sequence. Paintings are PNG bytes in Resources/DigDeepArt,
    // decoded at runtime with point filtering so they stay crisp whatever the import settings.
    public class Story
    {
        struct Slide { public int img; public string text; }

        static readonly Slide[] Intro =
        {
            new Slide { img = 0, text = "LONG AGO, TWO PEOPLES SHARED THE LAND: THE DWARFS, AND THE GODS." },
            new Slide { img = 1, text = "KING HALDRIN WAS WISE AND KIND. THE GODS CALLED HIM FRIEND, AND GAVE HIM THE GOD-DIAMOND." },
            new Slide { img = 2, text = "WHEN HALDRIN DIED, HIS SON ORNIN TOOK THE THRONE. NO ONE HAD EVER TOLD HIM NO." },
            new Slide { img = 3, text = "BEING A FRIEND OF THE GODS WAS NOT ENOUGH FOR ORNIN. HE WANTED TO BE ONE." },
            new Slide { img = 4, text = "HE DROVE HIS PEOPLE TO RAISE PYRAMIDS THAT PIERCED THE CLOUDS." },
            new Slide { img = 5, text = "AT THE TOP, WITH THE DIAMOND'S POWER, HE RAISED HIS SWORD AGAINST THE HEAVENS." },
            new Slide { img = 6, text = "THE GODS ANSWERED WITH FIRE AND THUNDER." },
            new Slide { img = 7, text = "FOR ORNIN'S PRIDE, EVERY DWARF WAS CURSED: TWISTED INTO HALF-BEASTS." },
            new Slide { img = 8, text = "THEY FLED INTO THE DARK EARTH, TO DIG FOR ALL ETERNITY." },
            new Slide { img = -1, text = "THE GOD-DIAMOND STILL LIES FAR BELOW, GUARDED BY WHAT REMAINS OF ORNIN." },
            new Slide { img = -1, text = "DIG DEEP. FIND IT, AND THE CURSE WILL BE BROKEN." },
        };

        const string EndText = "THE CURSE WAS BROKEN. THE DWARFS RETURNED TO THE SUNLIT LAND, AND LIVED IN PEACE ONCE MORE.";
        const float FadeIn = 1.6f, FadeOut = 1.3f, TypeRate = 26f, Hold = 2.6f;

        readonly Texture2D[] slides = new Texture2D[9];
        readonly Texture2D ending, looming;
        int index;
        float t, blipT, endT;
        int typed;
        public bool IntroDone { get; private set; }
        public int titleSel;

        public Story()
        {
            for (int i = 0; i < slides.Length; i++) slides[i] = Load("slide" + (i + 1));
            ending = Load("ending");
            looming = Load("ending2");   // the same land, the god looming over it
        }

        static Texture2D Load(string name)
        {
            var ta = Resources.Load<TextAsset>("DigDeepArt/" + name);
            if (ta == null) { Debug.LogWarning("DigDeep: painting DigDeepArt/" + name + " not found"); return null; }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(ta.bytes);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        static float SlideDur(int i) => FadeIn + Intro[i].text.Length / TypeRate + Hold + FadeOut;

        // ------------------------------------------------------------------ intro

        public void StartIntro() { index = 0; t = 0f; typed = 0; IntroDone = false; }

        public void TickIntro(float dt)
        {
            if (IntroDone) return;
            t += dt;
            int want = Mathf.Clamp((int)((t - FadeIn * 0.6f) * TypeRate), 0, Intro[index].text.Length);
            if (want > typed)
            {
                char ch = Intro[index].text[want - 1];
                typed = want;
                blipT -= 1f;
                if (ch != ' ' && blipT <= 0f) { Game.I.sfx.Play("blip", 0.18f, 1f, 0.04f); blipT = 1.5f; }
            }
            if (t >= SlideDur(index))
            {
                index++; t = 0f; typed = 0;
                if (index >= Intro.Length) IntroDone = true;
            }
        }

        static float Fade(float t, float dur) => Mathf.Clamp01(Mathf.Min(t / FadeIn, (dur - t) / FadeOut));

        public void DrawIntro()
        {
            Black();
            if (IntroDone || index >= Intro.Length) return;
            var sl = Intro[index];
            float a = Fade(t, SlideDur(index));
            a = a * a * (3f - 2f * a);   // smoothstep: slow, soft fades
            float W = Screen.width, H = Screen.height;
            float S = Mathf.Max(2f, Mathf.Floor(H / 270f));

            float textTop;
            if (sl.img >= 0 && slides[sl.img] != null)
            {
                // Undertale framing: a wide panel up top, scaled by whole pixels, the typed text underneath
                var tex = slides[sl.img];
                float k = Mathf.Max(1f, Mathf.Floor(Mathf.Min(H * 0.55f / tex.height, W * 0.82f / tex.width)));
                float pw = tex.width * k, ph = tex.height * k;
                float x = Mathf.Round((W - pw) * 0.5f), y = Mathf.Round(H * 0.08f);
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.DrawTexture(new Rect(x, y, pw, ph), tex);
                GUI.color = old;
                textTop = y + ph + H * 0.06f;
            }
            else textTop = H * 0.42f;

            DrawTyped(sl.text, typed, textTop, S, a, sl.img < 0);

            if (t > 1.2f || index > 0)
                PixelFont.Draw(W - S * 4f, H - S * 10f, "PRESS ANY BUTTON TO SKIP", Mathf.Max(1f, S - 1f), new Color(0.5f, 0.5f, 0.5f, 0.6f), 2, false);
        }

        // Undertale-like text block: big white pixel letters, word-wrapped, left aligned in a centred column.
        static void DrawTyped(string text, int count, float top, float S, float alpha, bool centre)
        {
            float W = Screen.width;
            float scale = Mathf.Max(2f, Mathf.Floor(S * 1.5f));
            int maxChars = Mathf.Max(16, Mathf.FloorToInt(W * 0.62f / (6f * scale)));
            var lines = PixelFont.Wrap(text, maxChars);
            float colW = 0f;
            foreach (var l in lines) colW = Mathf.Max(colW, PixelFont.Width(l, scale));
            float x0 = Mathf.Round((W - colW) * 0.5f);
            int left = count;
            for (int i = 0; i < lines.Count && left > 0; i++)
            {
                string l = lines[i];
                string shown = l.Substring(0, Mathf.Min(left, l.Length));
                left -= l.Length + 1;
                float y = top + i * 11f * scale;
                if (centre) PixelFont.Draw(W * 0.5f - PixelFont.Width(l, scale) * 0.5f, y, shown, scale, new Color(1f, 1f, 1f, alpha), 0, false);
                else PixelFont.Draw(x0, y, shown, scale, new Color(1f, 1f, 1f, alpha), 0, false);
            }
        }

        static void Black() => Box(0, 0, Screen.width, Screen.height, Color.black);

        static void Box(float x, float y, float w, float h, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), Art.White);
            GUI.color = old;
        }

        // ------------------------------------------------------------------ start screen

        public const string Continue = "CONTINUE", NewRun = "NEW RUN", WatchIntro = "WATCH INTRO", Options = "OPTIONS", Credits = "CREDITS", Quit = "QUIT", Test = "TEST MODE";
        static readonly string[] withSave = { Continue, NewRun, Test, Options, Credits, Quit }, noSave = { NewRun, Test, Options, Credits, Quit };
        public static string[] TitleItems => SaveGame.Exists ? withSave : noSave;

        // where each menu line was drawn (GUI coordinates), for the mouse
        public readonly System.Collections.Generic.List<Rect> itemRects = new System.Collections.Generic.List<Rect>();

        // The start screen sits on top of the live campfire scene: a logo and a short list, nothing else.
        public void DrawTitle(Game g)
        {
            float W = Screen.width, H = Screen.height;
            float S = Mathf.Max(2f, Mathf.Floor(H / 270f * Settings.UiScale));
            float tt = Time.unscaledTime;

            // a dark band on the left keeps the text readable over the fire-lit cave
            for (int i = 0; i < 12; i++)
            {
                float a = 0.72f * (1f - i / 12f);
                Box(i * W * 0.04f, 0, W * 0.04f, H, new Color(0f, 0f, 0f, a));
            }

            float x = Mathf.Round(W * 0.07f);
            var gold = new Color(1f, 0.82f, 0.32f);
            PixelFont.Draw(x, H * 0.2f, "MOIN", S * 6f, gold);

            itemRects.Clear();
            float size = S * 1.6f, lh = 13f * S;
            float y0 = H * 0.2f + 60f * S;
            for (int i = 0; i < TitleItems.Length; i++)
            {
                bool sel = i == titleSel;
                float y = y0 + i * lh;
                string label = TitleItems[i];
                float w = PixelFont.Width(label, size);
                itemRects.Add(new Rect(x - 4f * S, y - 2f * S, w + 24f * S, lh));
                if (sel)
                {
                    Box(x - 4f * S, y - 2f * S, w + 16f * S, 11f * size / 1.6f + 2f * S, new Color(1f, 0.82f, 0.32f, 0.16f));
                    PixelFont.Draw(x + (((int)(tt * 3f)) % 2 == 0 ? 2f * S : 0f), y, label, size, Color.white);
                }
                else PixelFont.Draw(x, y, label, size, new Color(0.8f, 0.8f, 0.85f));
            }

            float sm = Mathf.Max(1f, S - 1f);
            int wins = PlayerPrefs.GetInt("digdeep_wins", 0);
            if (g.best > 0)
                PixelFont.Draw(x, H - S * 14f, "BEST DEPTH -" + g.best + (wins > 0 ? "   CURSE BROKEN " + wins + "X" : ""), sm, new Color(0.95f, 0.8f, 0.5f));
        }

        // the menu line under the mouse (-1: none)
        public int TitleItemAtMouse()
        {
            var m = GameInput.MouseScreen;
            var gp = new Vector2(m.x, Screen.height - m.y);
            for (int i = 0; i < itemRects.Count; i++) if (itemRects[i].Contains(gp)) return i;
            return -1;
        }

        // ------------------------------------------------------------------ credits

        public void DrawCredits()
        {
            Black();
            float W = Screen.width, H = Screen.height;
            float S = Mathf.Max(2f, Mathf.Floor(H / 270f));
            var gold = new Color(1f, 0.8f, 0.35f);
            var grey = new Color(0.7f, 0.7f, 0.75f);
            float y = H * 0.18f;
            PixelFont.Draw(W * 0.5f, y, "MOIN", S * 3f, gold, 1);
            y += 34f * S;
            (string, string)[] lines =
            {
                ("A GAME BY", "JENTE"),
                ("MUSIC", "JENTE  -  MOIN, ACHTERVOLGING"),
                ("ART, SOUND AND CODE", "MADE BY HAND, PIXEL BY PIXEL"),
                ("MADE WITH", "UNITY"),
                ("INSPIRED BY", "DIG DUG, NUCLEAR THRONE, SUPER CRATE BOX, NOITA, THE BINDING OF ISAAC"),
                ("THANK YOU", "FOR PLAYING"),
            };
            foreach (var (a, b) in lines)
            {
                PixelFont.Draw(W * 0.5f, y, a, Mathf.Max(1f, S - 1f), grey, 1);
                PixelFont.Draw(W * 0.5f, y + 10f * S, b, S, Color.white, 1);
                y += 26f * S;
            }
            if (((int)(Time.unscaledTime * 2f)) % 2 == 0)
                PixelFont.Draw(W * 0.5f, H - S * 14f, "PRESS ANY KEY", Mathf.Max(1f, S - 1f), new Color(0.6f, 0.6f, 0.65f), 1);
        }

        // ------------------------------------------------------------------ ending
        // Variant 0: the plain ending. 1: ...but the gods still loom over the dwarfs (not every digger has the
        // God-Diamond yet). 2: the true ending, after the God above the clouds has fallen.
        const string LoomText = "BUT FAR ABOVE THE CLOUDS, THE GODS STILL WATCHED.";
        const string TrueText = "THE GOD FELL, AND THE SKY WENT QUIET. NO CURSE WOULD EVER TOUCH THE DWARFS AGAIN.";
        int variant, typed2;
        float loomStart;   // when the second painting begins

        public void StartEnding(int variant = 0)
        {
            endT = 0f; typed = 0; typed2 = 0; this.variant = variant;
            loomStart = 5f + EndText.Length / (TypeRate * 0.8f) + 2.5f;
        }
        public float EndT => endT;
        string FirstText => variant == 2 ? TrueText : EndText;

        public void TickEnding(float dt)
        {
            endT += dt;
            Type(FirstText, endT - 5f, ref typed);
            if (variant == 1) Type(LoomText, endT - loomStart - 1.8f, ref typed2);
        }

        void Type(string text, float tt, ref int count)
        {
            int want = Mathf.Clamp((int)(tt * TypeRate * 0.8f), 0, text.Length);
            if (want > count)
            {
                char ch = text[want - 1];
                count = want;
                blipT -= 1f;
                if (ch != ' ' && blipT <= 0f) { Game.I.sfx.Play("blip", 0.18f, variant == 1 && endT > loomStart ? 0.7f : 0.9f, 0.04f); blipT = 1.5f; }
            }
        }

        // when the story (painting and text) has been read: it fades out, then THE END comes up in the middle
        float StoryDone => variant == 1 ? loomStart + 1.8f + LoomText.Length / (TypeRate * 0.8f) + 2.5f
                                        : 5f + FirstText.Length / (TypeRate * 0.8f) + 2.5f;
        const float StoryFade = 1.4f;
        float ExitAt => StoryDone + StoryFade + 1.2f;
        public bool EndingCanExit => endT > ExitAt;

        public void DrawEnding(Game g)
        {
            float W = Screen.width, H = Screen.height;
            float S = Mathf.Max(2f, Mathf.Floor(H / 270f));
            Black();
            // 0-2s: pure white. 2-4.5s: white fades into the peaceful painting.
            float white = endT < 2f ? 1f : 1f - Mathf.Clamp01((endT - 2f) / 2.5f);
            bool loom = variant == 1 && endT > loomStart + 0.9f;
            var pic = loom && looming != null ? looming : ending;
            if (pic != null && endT >= 2f)
            {
                float k = Mathf.Max(1f, Mathf.Floor(H * 0.56f / pic.height));
                float sz = pic.height * k;
                GUI.DrawTexture(new Rect(Mathf.Round((W - sz) * 0.5f), Mathf.Round(H * 0.07f), sz, sz), pic);
                float textTop = H * 0.07f + sz + H * 0.06f;
                if (loom)
                {
                    DrawTyped(LoomText, typed2, textTop, S, 1f, false);
                    int have = 0;
                    foreach (var c in Chars.All) if (PlayerPrefs.GetInt("digdeep_won_" + c.id, 0) == 1) have++;
                    float a2 = Mathf.Clamp01((endT - loomStart - 3f) * 0.8f);
                    PixelFont.Draw(W * 0.5f, textTop + 22f * S, "FIND THE GOD-DIAMOND WITH EVERY DIGGER   " + have + " / " + Chars.All.Count, Mathf.Max(1f, S - 1f), new Color(0.75f, 0.8f, 1f, a2), 1);
                }
                else DrawTyped(FirstText, typed, textTop, S, 1f, false);
            }
            // the story fades away... and THE END comes up in the middle of the screen
            float gone = Mathf.Clamp01((endT - StoryDone) / StoryFade);
            if (gone > 0f) Box(0, 0, W, H, new Color(0f, 0f, 0f, gone));
            if (endT > StoryDone + StoryFade)
            {
                float a = Mathf.Clamp01((endT - StoryDone - StoryFade) * 0.8f);
                var p = g.player;
                string stats = p != null ? p.character.name + " THE " + p.character.animal + "   DEPTH -" + g.maxDepth + "   GOLD " + p.s.gold + "   KILLS " + p.s.kills : "";
                string end = variant == 1 ? "THE END?" : "THE END";
                PixelFont.Draw(W * 0.5f, H * 0.5f - S * 12f, end, S * 3f, new Color(1f, 0.85f, 0.4f, a), 1);
                PixelFont.Draw(W * 0.5f, H * 0.5f + S * 18f, stats, Mathf.Max(1f, S - 1f), new Color(0.75f, 0.75f, 0.75f, a), 1);
            }
            // the cut between the two paintings: a dip to black
            if (variant == 1)
            {
                float d = Mathf.Abs(endT - (loomStart + 0.9f));
                float dark = 1f - Mathf.Clamp01(d / 0.9f);
                if (dark > 0f) Box(0, 0, W, H, new Color(0f, 0f, 0f, dark));
            }
            if (white > 0f) Box(0, 0, W, H, new Color(1f, 1f, 1f, white));
        }
    }
}
