using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PatenteArabo
{
    /// <summary>
    /// Quick game modes that bring people back every day:
    /// Daily quiz (10 statements, same for everyone that day), Trap questions, Time attack (60 s).
    /// </summary>
    public class QuickQuizPage : Page
    {
        public enum Mode { Daily, Traps, TimeAttack }

        public static string Today => DateTime.Now.ToString("yyyyMMdd");
        public static bool DailyDone => PlayerPrefs.GetString("pa_daily_done", "") == Today;
        public static int DailyStreak
        {
            get
            {
                var last = PlayerPrefs.GetString("pa_daily_done", "");
                if (last != Today && last != DateTime.Now.AddDays(-1).ToString("yyyyMMdd")) return 0;
                return PlayerPrefs.GetInt("pa_daily_streak", 0);
            }
        }
        public static int BestTimeAttack => PlayerPrefs.GetInt("pa_ta_best", 0);

        const float TimeAttackSeconds = 60f;

        readonly Mode _mode;
        readonly List<Stmt> _list;
        int _idx, _right, _wrong;
        float _left = TimeAttackSeconds;
        bool _over, _waiting;
        VisualElement _body;
        Label _top, _timer;
        VisualElement _timeFill;

        public QuickQuizPage(Mode mode)
        {
            _mode = mode;
            switch (mode)
            {
                case Mode.Daily:
                {
                    // same 10 statements for everybody on the same day (seeded by the date)
                    var rnd = new System.Random(int.Parse(Today));
                    _list = Listato.All.OrderBy(_ => rnd.Next()).Take(10).ToList();
                    break;
                }
                case Mode.Traps:
                {
                    var rnd = new System.Random();
                    // statements with trap words (sempre, solo, mai...) + the user's own weak ones
                    var traps = Listato.All.Where(s => s.t != null && s.t.Length > 0 && Premium.StmtOpen(s)).OrderBy(_ => rnd.Next()).Take(15);
                    var weak = Srs.WeakList().OrderBy(_ => rnd.Next()).Take(5);
                    _list = weak.Concat(traps).Where(Premium.StmtOpen).GroupBy(s => s.id).Select(g => g.First()).Take(20).ToList();
                    break;
                }
                default:
                {
                    var rnd = new System.Random();
                    _list = Listato.All.Where(s => string.IsNullOrEmpty(s.f) && Premium.StmtOpen(s)).OrderBy(_ => rnd.Next()).Take(200).ToList();
                    break;
                }
            }
        }

        string Title() => _mode == Mode.Daily ? Lang.T("daily_quiz") : _mode == Mode.Traps ? Lang.T("traps_mode") : Lang.T("time_attack");

        public override void Build(VisualElement root)
        {
            var extra = Chrome.TopBar(root, Title());
            _top = U.T(extra, "", "bold");
            _top.style.marginLeft = 20; _top.style.marginRight = 20;
            if (_mode == Mode.TimeAttack)
            {
                var bar = U.El(root, "xp-bar");
                bar.style.height = 18; bar.style.marginLeft = 40; bar.style.marginRight = 40; bar.style.flexShrink = 0;
                _timeFill = U.El(bar, "xp-fill");
                _timeFill.style.width = Length.Percent(100);
                _timer = U.It(extra, "60", "lb-score");
            }
            _body = U.El(root);
            _body.style.flexGrow = 1;
            Show();
        }

        public override void Tick(float dt)
        {
            Gamify.StudyTick(dt);
            if (_mode != Mode.TimeAttack || _over) return;
            _left -= dt;
            if (_timer != null) _timer.text = Mathf.CeilToInt(Mathf.Max(0, _left)).ToString();
            if (_timeFill != null) _timeFill.style.width = Length.Percent(Mathf.Clamp01(_left / TimeAttackSeconds) * 100);
            if (_left <= 0) Finish();
        }

        void Show()
        {
            if (_over) return;
            if (_idx >= _list.Count) { Finish(); return; }
            _waiting = false;
            _body.Clear();
            _top.text = _mode == Mode.TimeAttack ? _right + " ✓" : (_idx + 1) + " / " + _list.Count;
            var s = _list[_idx];

            var sc = U.Scroll(_body);
            sc.style.flexGrow = 1;
            var c = sc.contentContainer;
            c.style.paddingLeft = 60; c.style.paddingRight = 60;
            var card = U.Col(c, "card", "quiz-card");
            var row = U.Row(card);
            var fig = Listato.Figure(s);
            if (fig != null)
            {
                var img = U.Img(row, fig, "");
                img.style.width = 260; img.style.height = 260; img.style.flexShrink = 0;
                img.style.marginRight = 24; img.style.marginLeft = 24;
            }
            var col = U.Col(row);
            col.style.flexGrow = 1; col.style.flexShrink = 1;
            U.It(col, s.it, "it-text");
            if (Lang.Code != "it" && !StudyPage.ItalianOnly)
            {
                var tr = QuestionText.Translation(s, out _);
                if (tr != null) U.T(col, tr, "tr-text").style.marginTop = 10;
            }

            var ans = U.Row(_body);
            ans.style.justifyContent = Justify.Center;
            ans.style.flexShrink = 0;
            ans.style.paddingBottom = 16;
            U.B(ans, Lang.Code == "it" ? "VERO" : "VERO\n" + Lang.T("true"), () => Answer(s, true), "vf", "btn-green").style.height = 130;
            U.B(ans, Lang.Code == "it" ? "FALSO" : "FALSO\n" + Lang.T("false"), () => Answer(s, false), "vf", "btn-red").style.height = 130;
        }

        void Answer(Stmt s, bool v)
        {
            if (_waiting || _over) return;
            _waiting = true;
            bool ok = v == s.True;
            if (ok) { _right++; Sfx.Right(); } else { _wrong++; Sfx.Wrong(); }
            Srs.Record(s.id, ok);
            Gamify.Answered(ok);

            if (_mode == Mode.TimeAttack)
            {
                // instant feedback, no explanation: speed is the game (a wrong answer costs 3 seconds)
                if (!ok) _left -= 3f;
                _idx++;
                _body.schedule.Execute(Show).StartingIn(ok ? 120 : 450);
                if (!ok) Toast.Show("✗  " + (s.True ? "VERO" : "FALSO") + "  (-3s)");
                return;
            }

            var v2 = U.Col(_body, "card", ok ? "verdict-ok" : "verdict-ko");
            v2.style.marginLeft = 60; v2.style.marginRight = 60; v2.style.flexShrink = 0;
            U.T(v2, (ok ? Lang.T("right") : Lang.T("wrong")) + "  -  " + Lang.T("official_answer") + ": " + (s.True ? "VERO" : "FALSO"), "h2", "wraptext");
            QuestionText.ShowExplanation(v2, s);
            var nx = U.IB(v2, "forward", Lang.T("next"), () => { _idx++; Show(); }, "btn-primary");
            nx.style.alignSelf = Align.FlexEnd;
        }

        void Finish()
        {
            if (_over) return;
            _over = true;
            _body.Clear();
            int xp = 0;
            string headline;
            bool record = false;
            switch (_mode)
            {
                case Mode.Daily:
                    if (!DailyDone)
                    {
                        bool continued = PlayerPrefs.GetString("pa_daily_done", "") == DateTime.Now.AddDays(-1).ToString("yyyyMMdd");
                        PlayerPrefs.SetInt("pa_daily_streak", continued ? PlayerPrefs.GetInt("pa_daily_streak", 0) + 1 : 1);
                        PlayerPrefs.SetString("pa_daily_done", Today);
                        PlayerPrefs.Save();
                        xp = 40 + _right * 5;
                    }
                    headline = Lang.T("daily_result", _right, _list.Count);
                    break;
                case Mode.Traps:
                    xp = _right * 4;
                    headline = Lang.T("daily_result", _right, _list.Count);
                    break;
                default:
                    xp = _right * 3;
                    record = _right > BestTimeAttack;
                    if (record) { PlayerPrefs.SetInt("pa_ta_best", _right); PlayerPrefs.Save(); }
                    headline = Lang.T("ta_result", _right);
                    break;
            }
            if (xp > 0) Gamify.AddXp(xp);
            Challenge.Submit();
            if (_right > 0 && _right * 10 >= (_right + _wrong) * 8) Fx.Confetti();

            var c = U.Col(_body, "center");
            c.style.flexGrow = 1;
            U.Icon(c, record || _wrong == 0 ? "star" : "check", 130, U.Hex(record || _wrong == 0 ? "F2C94C" : "6EDC96"));
            U.T(c, headline, "title", "wraptext").style.unityTextAlign = TextAnchor.MiddleCenter;
            if (record) U.T(c, Lang.T("new_record"), "gold", "bold");
            if (_mode == Mode.Daily) U.T(c, Lang.T("daily_streak", DailyStreak), "subtitle");
            if (_mode == Mode.TimeAttack) U.T(c, Lang.T("best_score", BestTimeAttack), "subtitle");
            if (xp > 0) U.It(c, "+" + xp + " XP", "lb-score");

            var btns = U.Row(c, "wrap");
            btns.style.justifyContent = Justify.Center;
            btns.style.marginTop = 20;
            if (_mode != Mode.Daily) U.IB(btns, "review", Lang.T("play_again"), () => App.I.Go(() => new QuickQuizPage(_mode), false), "btn-primary");
            U.IB(btns, "share", Lang.T("share"), () => NativeUtil.Share(ShareText()), "btn-gold");
            U.IB(btns, "home", Lang.T("home"), () => App.I.GoHome());
        }

        string ShareText()
        {
            string what = _mode == Mode.TimeAttack ? Lang.T("ta_result", _right) : Title() + ": " + _right + "/" + _list.Count;
            return "🚗 " + Brand.Name + " — " + what + "\n" + Lang.T("share_invite") + "\n" + NativeUtil.StoreUrl;
        }
    }

    /// <summary>Chapter theory card: the key rules of the chapter, then straight into its questions.</summary>
    public class TheoryPage : Page
    {
        readonly int _ch;
        public TheoryPage(int chapter) { _ch = chapter; }

        static Dictionary<string, List<string>> _cache;
        static string _cacheLang;

        /// <summary>Bullets for a chapter in the user's language (Italian when not available) and whether they are Italian.</summary>
        public static List<string> Bullets(int ch, out bool italian)
        {
            italian = false;
            if (_cacheLang != Lang.Code)
            {
                _cacheLang = Lang.Code;
                _cache = Load(Lang.Code);
            }
            if (_cache != null && _cache.TryGetValue(ch.ToString(), out var l)) return l;
            italian = true;
            var it = Load("it");
            return it != null && it.TryGetValue(ch.ToString(), out l) ? l : new List<string>();
        }

        static Dictionary<string, List<string>> Load(string code)
        {
            var ta = Resources.Load<TextAsset>("PatenteArabo/i18n/theory_" + code);
            if (ta == null) return null;
            var f = JsonUtility.FromJson<TheoryFile>(ta.text);
            var d = new Dictionary<string, List<string>>();
            if (f?.ch == null) return d;
            foreach (var c in f.ch) d[c.n.ToString()] = new List<string>(c.b ?? new string[0]);
            return d;
        }

        [Serializable] class TheoryChapter { public int n; public string[] b; }
        [Serializable] class TheoryFile { public TheoryChapter[] ch; }

        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, _ch + ". " + QuestionText.ChapterTitle(_ch));
            var sc = U.Scroll(root);
            var c = sc.contentContainer;
            c.style.paddingLeft = 70; c.style.paddingRight = 70; c.style.paddingBottom = 40;

            var go = U.Row(c, "daily-hero");
            U.Icon(go, "study", 64, U.Hex("F2C94C"));
            var gcol = U.Col(go);
            gcol.style.flexGrow = 1; gcol.style.flexShrink = 1; gcol.style.marginLeft = 18; gcol.style.marginRight = 18;
            var info = Listato.Chapters.FirstOrDefault(x => x.n == _ch);
            U.T(gcol, Lang.T("chapter_info", info != null ? info.count : 0, Mathf.RoundToInt(Srs.SeenShare(_ch) * 100)), "bold");
            U.Bar(gcol, Srs.Readiness(_ch));
            U.IB(go, "play", Lang.T("start_questions"), () => App.I.Go(() => new StudyPage(StudyPage.Source.Chapter, _ch)), "btn-gold", "glow").style.flexShrink = 0;

            var bullets = Bullets(_ch, out bool italian);
            var card = U.Col(c, "card");
            U.T(card, Lang.T("theory_title"), "h2");
            for (int bi = 0; bi < bullets.Count; bi++)
            {
                var b = bullets[bi];
                bool warn = b.StartsWith("Attenzione") || b.StartsWith("⚠") || b.StartsWith("تنبيه") || b.StartsWith("انتبه");
                var r = U.Row(card);
                r.style.marginBottom = 10; r.style.alignItems = Align.FlexStart;
                U.Icon(r, warn ? "warning" : "check", 36, U.Hex(warn ? "FF8282" : "6EDC96"));
                var l = italian ? U.It(r, b, "ex-text") : U.T(r, b, "ex-text");
                l.style.flexShrink = 1; l.style.marginLeft = 14; l.style.marginRight = 14;
                if (italian && Lang.Code != "it")
                {
                    var row = r; var lab = l;
                    QuestionText.TranslateCached("th" + _ch + "_" + bi, b, res =>
                    {
                        if (lab.panel == null) return;
                        var t = U.T(row, res, "ex-text");
                        t.style.flexShrink = 1; t.style.marginLeft = 14; t.style.marginRight = 14;
                        t.PlaceBehind(lab); lab.RemoveFromHierarchy();
                    });
                }
            }
            if (bullets.Count > 0)
                U.IB(card, "speaker", Lang.T("listen_it"), () =>
                {
                    var it = Load("it");
                    if (it != null && it.TryGetValue(_ch.ToString(), out var lst)) ItalianTts.Speak(string.Join(". ", lst));
                }, "btn-small").style.alignSelf = Align.FlexStart;
        }
    }

    /// <summary>Safe driving and the 2024 Highway Code: verified rules, statistics and official sources.</summary>
    public class SafetyPage : Page
    {
        [Serializable] class Sec { public string t; public string[] b; }
        [Serializable] class Src { public string t; public string u; }
        [Serializable] class SafeFile { public Sec[] sections; public Src[] src; }

        static SafeFile Load(string code)
        {
            var ta = Resources.Load<TextAsset>("PatenteArabo/i18n/safe_" + code);
            return ta == null ? null : JsonUtility.FromJson<SafeFile>(ta.text);
        }

        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, Lang.T("safety"));
            var sc = U.Scroll(root);
            var c = sc.contentContainer;
            c.style.paddingLeft = 70; c.style.paddingRight = 70; c.style.paddingBottom = 40;
            bool italian = false;
            var f = Load(Lang.Code);
            if (f == null) { f = Load("en"); }
            if (f == null) { f = Load("it"); italian = true; }
            if (f?.sections == null) return;
            string[] icons = { "warning", "info", "progress", "language", "calendar" };
            for (int i = 0; i < f.sections.Length; i++)
            {
                var sec = f.sections[i];
                var card = U.Col(c, "card");
                var h = U.Row(card);
                U.Icon(h, icons[i % icons.Length], 48, U.Hex(i == 0 ? "FF8282" : i == 2 ? "FCD34D" : "7DD3FC"));
                var ht = italian ? U.It(h, sec.t, "h2") : U.T(h, sec.t, "h2");
                ht.style.marginLeft = 16; ht.style.marginRight = 16; ht.style.flexShrink = 1;
                foreach (var b in sec.b ?? new string[0])
                {
                    var r = U.Row(card);
                    r.style.marginBottom = 8; r.style.alignItems = Align.FlexStart;
                    U.Icon(r, "check", 32, U.Hex("6EDC96"));
                    var l = italian ? U.It(r, b, "ex-text") : U.T(r, b, "ex-text");
                    l.style.flexShrink = 1; l.style.marginLeft = 14; l.style.marginRight = 14;
                }
            }
            U.T(c, Lang.T("sources"), "h2").style.marginTop = 16;
            if (f.src != null)
                foreach (var s in f.src)
                {
                    var url = s.u;
                    var b = U.B(c, s.t, () => Application.OpenURL(url), "btn-ghost", "btn-small");
                    b.style.unityTextAlign = TextAnchor.MiddleLeft; b.style.whiteSpace = WhiteSpace.Normal;
                    b.style.height = StyleKeyword.Auto; b.style.minHeight = 76;
                }
            U.T(c, Lang.T("safety_note"), "small", "muted", "wraptext").style.marginTop = 10;
        }
    }

    /// <summary>The user's own list of Italian words saved from the questions (tap a word, then "Save word").</summary>
    public static class Vocab
    {
        [Serializable] class Item { public string w; public string m; }
        [Serializable] class Store { public List<Item> items = new List<Item>(); }
        static Store _s;

        static Store S
        {
            get
            {
                if (_s != null) return _s;
                try { _s = JsonUtility.FromJson<Store>(PlayerPrefs.GetString("pa_vocab", "")); } catch { }
                return _s ??= new Store();
            }
        }

        static void Save() { PlayerPrefs.SetString("pa_vocab", JsonUtility.ToJson(S)); PlayerPrefs.Save(); }
        public static void Reload() { _s = null; }
        public static int Count => S.items.Count;
        public static bool Has(string w) => S.items.Exists(i => string.Equals(i.w, w, StringComparison.OrdinalIgnoreCase));
        public static void Toggle(string w, string meaning)
        {
            int k = S.items.FindIndex(i => string.Equals(i.w, w, StringComparison.OrdinalIgnoreCase));
            if (k >= 0) S.items.RemoveAt(k); else S.items.Insert(0, new Item { w = w, m = meaning ?? "" });
            Save();
        }
        public static IEnumerable<(string word, string meaning)> All() { foreach (var i in S.items) yield return (i.w, i.m); }
    }

    public class VocabPage : Page
    {
        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, Lang.T("my_words"));
            var sc = U.Scroll(root);
            var c = sc.contentContainer;
            c.style.paddingLeft = 70; c.style.paddingRight = 70; c.style.paddingBottom = 40;
            if (Vocab.Count == 0) { U.T(c, Lang.T("my_words_empty"), "ex-text", "muted", "wraptext"); return; }
            foreach (var (word, meaning) in Vocab.All().ToList())
            {
                var w = word;
                var r = U.Row(c, "card");
                r.style.alignItems = Align.Center;
                var col = U.Col(r);
                col.style.flexGrow = 1; col.style.flexShrink = 1; col.style.marginLeft = 10; col.style.marginRight = 10;
                U.It(col, w, "h2").style.marginBottom = 0;
                if (!string.IsNullOrEmpty(meaning)) U.T(col, meaning, "tr-text");
                Chrome.IconButton(r, "speaker", () => ItalianTts.Speak(w));
                Chrome.IconButton(r, "trash", () => { Vocab.Toggle(w, null); App.I.Rebuild(); });
            }
        }
    }

    /// <summary>Exam day: what to bring and how it works (based on the official rules), plus official links.</summary>
    public class ExamDayPage : Page
    {
        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, Lang.T("exam_day"));
            var sc = U.Scroll(root);
            var c = sc.contentContainer;
            c.style.paddingLeft = 70; c.style.paddingRight = 70; c.style.paddingBottom = 40;

            var card = U.Col(c, "card");
            U.T(card, Lang.T("checklist"), "h2");
            for (int i = 1; i <= 7; i++)
            {
                string key = "ed_" + i;
                bool on = PlayerPrefs.GetInt("pa_" + key, 0) == 1;
                var r = U.Row(card, "setting-row");
                var box = U.Raw(r, on ? "✓" : "", null, "check-box", on ? "check-on" : "");
                var l = U.T(r, Lang.T(key), "ex-text");
                l.style.flexShrink = 1; l.style.marginLeft = 16; l.style.marginRight = 16;
                Action toggle = () => { PlayerPrefs.SetInt("pa_" + key, on ? 0 : 1); PlayerPrefs.Save(); App.I.Rebuild(); };
                box.clicked += toggle;
                r.RegisterCallback<ClickEvent>(_ => { if (_.target != box) toggle(); });
            }

            var rules = U.Col(c, "card");
            U.T(rules, Lang.T("exam_rules_title"), "h2");
            foreach (var k in new[] { "rule_30", "rule_20", "rule_3", "ed_rule_tries", "ed_rule_foglio" })
                U.T(rules, "- " + Lang.T(k), "ex-text");

            U.T(c, Lang.T("official_links"), "h2").style.marginTop = 16;
            OfficialLinks.Add(c);
        }
    }

    public static class OfficialLinks
    {
        public static readonly (string title, string url)[] All =
        {
            ("Portale dell'Automobilista (MIT) — prenotazioni, punti patente, pratiche", "https://www.ilportaledellautomobilista.it/"),
            ("MIT — Come conseguire la patente B", "https://www.mit.gov.it/conseguimento-patente-b"),
            ("MIT — Conversione della patente estera", "https://www.mit.gov.it/conversione-patente-estera"),
            ("Prefettura — Stati con patenti convertibili (PDF)", "https://prefettura.interno.gov.it/sites/default/files/57/2024-01/elenco_stati_che_rilasciano_patenti_convertibili.pdf"),
            ("Codice della Strada — testo vigente (Normattiva)", "https://www.normattiva.it/uri-res/N2Ls?urn:nir:stato:decreto.legislativo:1992-04-30;285"),
        };

        public static void Add(VisualElement c)
        {
            foreach (var (title, url) in All)
            {
                var b = U.B(c, title, () => Application.OpenURL(url), "btn-ghost", "btn-small");
                b.style.unityTextAlign = TextAnchor.MiddleLeft;
                b.style.whiteSpace = WhiteSpace.Normal;
                b.style.height = StyleKeyword.Auto;
                b.style.minHeight = 76;
            }
            U.T(c, Lang.T("not_official"), "small", "muted", "wraptext").style.marginTop = 8;
        }
    }

    /// <summary>Small visual celebration (falling coloured pieces).</summary>
    public static class Fx
    {
        static readonly string[] Colors = { "F2C94C", "6EDC96", "7DD3FC", "F472B6", "FB923C", "A78BFA" };

        public static void Confetti()
        {
            var layer = U.El(App.I.Overlay);
            layer.pickingMode = PickingMode.Ignore;
            layer.style.position = Position.Absolute;
            layer.style.left = 0; layer.style.right = 0; layer.style.top = 0; layer.style.bottom = 0;
            var rnd = new System.Random();
            var pieces = new List<(VisualElement e, float x, float y, float vx, float vy, float r, float vr)>();
            for (int i = 0; i < 70; i++)
            {
                var p = new VisualElement { pickingMode = PickingMode.Ignore };
                p.style.position = Position.Absolute;
                p.style.width = rnd.Next(14, 26); p.style.height = rnd.Next(8, 16);
                p.style.backgroundColor = U.Hex(Colors[rnd.Next(Colors.Length)]);
                layer.Add(p);
                pieces.Add((p, (float)rnd.NextDouble() * 1920, -40 - (float)rnd.NextDouble() * 400,
                    ((float)rnd.NextDouble() - 0.5f) * 240, 500 + (float)rnd.NextDouble() * 500, (float)rnd.NextDouble() * 360, ((float)rnd.NextDouble() - 0.5f) * 720));
            }
            float t = 0;
            IVisualElementScheduledItem item = null;
            item = layer.schedule.Execute(() =>
            {
                t += 0.016f;
                for (int i = 0; i < pieces.Count; i++)
                {
                    var q = pieces[i];
                    q.e.style.left = q.x + q.vx * t;
                    q.e.style.top = q.y + q.vy * t;
                    q.e.style.rotate = new Rotate(q.r + q.vr * t);
                }
                if (t > 2.6f) { item.Pause(); layer.RemoveFromHierarchy(); }
            }).Every(16);
        }
    }
}
