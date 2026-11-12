using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace PatenteArabo
{
    /// <summary>Learning mode: one official statement at a time, with translation, explanation, glossary and trap words.</summary>
    public class StudyPage : Page
    {
        public enum Source { Chapter, Random, Weak, Review, Figures, Bookmarks, List }

        List<Stmt> _given;
        string _givenTitle;
        public StudyPage(List<Stmt> list, string title, int start = 0) : this(Source.List, 0) { _list = list; _givenTitle = title; _idx = Mathf.Clamp(start, 0, Mathf.Max(0, list.Count - 1)); }

        public static bool ItalianOnly
        {
            get => PlayerPrefs.GetInt("pa_it_only", 0) == 1;
            set { PlayerPrefs.SetInt("pa_it_only", value ? 1 : 0); PlayerPrefs.Save(); }
        }
        public static bool ShowTraps
        {
            get => PlayerPrefs.GetInt("pa_traps", 1) == 1;
            set { PlayerPrefs.SetInt("pa_traps", value ? 1 : 0); PlayerPrefs.Save(); }
        }
        public static bool AutoRead
        {
            get => PlayerPrefs.GetInt("pa_autoread", 0) == 1;
            set { PlayerPrefs.SetInt("pa_autoread", value ? 1 : 0); PlayerPrefs.Save(); }
        }
        public static bool AutoNext
        {
            get => PlayerPrefs.GetInt("pa_autonext", 0) == 1;
            set { PlayerPrefs.SetInt("pa_autonext", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        readonly Source _src;
        readonly int _chapter;
        List<Stmt> _list;
        int _idx;
        readonly Dictionary<int, bool?> _answers = new Dictionary<int, bool?>();
        VisualElement _body, _root;
        Label _counter;
        bool _showTr;

        public StudyPage(Source src, int chapter)
        {
            _src = src;
            _chapter = chapter;
            var rnd = new System.Random();
            switch (src)
            {
                case Source.Chapter:
                    _list = Listato.Chapter(chapter).OrderBy(s => s.q).ThenBy(s => s.id).ToList();
                    _idx = Math.Max(0, _list.FindIndex(s => !Srs.Seen(s.id) && Premium.StmtOpen(s)));
                    break;
                case Source.Random: _list = Listato.All.OrderBy(_ => rnd.Next()).Take(200).ToList(); break;
                case Source.Weak: _list = Srs.WeakList().OrderBy(_ => rnd.Next()).ToList(); break;
                case Source.Review:
                    _list = Srs.DueList();
                    if (_list.Count == 0) _list = Srs.WeakList();
                    break;
                case Source.Figures: _list = Listato.All.Where(s => !string.IsNullOrEmpty(s.f)).OrderBy(_ => rnd.Next()).Take(200).ToList(); break;
                case Source.Bookmarks: _list = Bookmarks.List(); break;
                case Source.List: _list = new List<Stmt>(); break;
            }
            // free plan: random/weak/pictures/saved lists only use the statements that are open
            if (src != Source.Chapter && src != Source.List && !Premium.Unlocked) _list = _list.Where(Premium.StmtOpen).ToList();
        }

        int _spokenIdx = -1;   // auto-read once per statement, not again after answering
        public override void Tick(float dt) { Gamify.StudyTick(dt); }
        public override void OnHide() { Challenge.Submit(); }

        string Title()
        {
            switch (_src)
            {
                case Source.Chapter: return _chapter + ". " + QuestionText.ChapterTitle(_chapter);
                case Source.Weak: return Lang.T("weak_only");
                case Source.Review: return Lang.T("review");
                case Source.Figures: return Lang.T(Course.IsB ? "signs_only" : "figures_only");
                case Source.Bookmarks: return Lang.T("bookmarks");
                case Source.List: return _givenTitle;
                default: return Lang.T("random_all");
            }
        }

        public override void Build(VisualElement root)
        {
            _root = root;
            var extra = Chrome.TopBar(root, Title());
            _counter = U.T(extra, "", "muted", "bold");
            _counter.style.marginLeft = 20; _counter.style.marginRight = 20;
            _body = U.El(root);
            _body.style.flexGrow = 1;
            if (_list.Count == 0)
            {
                var c = U.Col(_body, "center");
                c.style.flexGrow = 1;
                U.T(c, Lang.T("nothing_here"), "h2");
                U.B(c, Lang.T("back"), () => App.I.Back(), "btn-primary");
                return;
            }
            Render();
        }

        void Render()
        {
            _body.Clear();
            ItalianTts.Stop();
            var s = _list[_idx];
            _counter.text = (_idx + 1) + " / " + _list.Count;
            if (!Premium.StmtOpen(s))
            {
                var lockBox = U.Col(_body, "center");
                lockBox.style.flexGrow = 1;
                U.Icon(lockBox, "lock", 120, U.Hex("F2C94C"));
                U.T(lockBox, Course.IsPro ? Lang.T("locked_chapter_pro", Premium.FreePerChapter) : Lang.T("locked_chapter", Premium.FreePerChapter), "h2", "wraptext").style.unityTextAlign = TextAnchor.MiddleCenter;
                U.IB(lockBox, "premium", Course.IsPro ? Lang.T("go_pro") : Lang.T("go_premium"), () => ProPaywallPage.OpenForCourse(), "btn-gold").style.width = 600;
                // the learner can still move to the open statements around this one
                var lockNav = U.Row(lockBox);
                lockNav.style.justifyContent = Justify.Center; lockNav.style.marginTop = 10;
                if (_idx > 0) U.IB(lockNav, "back", Lang.T("prev"), () => { _idx--; Render(); }, "btn-ghost");
                U.B(lockNav, Lang.T("back"), () => App.I.Back(), "btn-ghost");
                if (_idx < _list.Count - 1) U.IB(lockNav, "forward", Lang.T("next"), () => { _idx++; Render(); }, "btn-ghost");
                return;
            }
            if (AutoRead && _spokenIdx != _idx) ItalianTts.Speak(s.it);
            _spokenIdx = _idx;
            _showTr = !ItalianOnly;

            var main = U.Row(_body);
            main.style.flexGrow = 1;
            main.style.alignItems = Align.Stretch;
            main.style.paddingLeft = 30; main.style.paddingRight = 30; main.style.paddingTop = 16;

            // left (or right in RTL): figure + tools
            var side = U.Col(main);
            side.style.width = 420; side.style.flexShrink = 0;
            side.style.alignItems = Align.Center;
            var fig = Listato.Figure(s);
            if (fig != null) U.Img(side, fig, "figure");
            else
            {
                var ph = U.El(side, "figure", "center");
                ph.style.backgroundColor = U.Hex("16233A");
                var l = new Label("—"); l.style.fontSize = 80; l.style.color = U.Hex("3A4C6A");
                ph.Add(l);
            }
            U.IB(side, "speaker", Lang.T("listen_it"), () => ItalianTts.Speak(s.it), "btn-small").style.width = 380;
            var meta = U.It(side, "Quesito " + s.q + " · n° " + s.id + " · cap. " + s.c, "small", "muted");
            meta.style.marginTop = 6;
            bool fav = Bookmarks.Has(s.id);
            Button favB = null;
            favB = U.IB(side, "star", fav ? Lang.T("saved") : Lang.T("save"), () =>
            {
                Bookmarks.Toggle(s.id);
                U.SetIBText(favB, Bookmarks.Has(s.id) ? Lang.T("saved") : Lang.T("save"));
                favB.EnableInClassList("btn-gold", Bookmarks.Has(s.id));
            }, "btn-small", fav ? "btn-gold" : "");
            favB.style.width = 380;

            // content
            var sc = U.Scroll(main);
            sc.style.flexGrow = 1;
            var col = sc.contentContainer;
            col.style.paddingLeft = 20; col.style.paddingRight = 20;

            var card = U.Col(col, "card");
            Words(card, s);
            if (ShowTraps && s.t != null && s.t.Length > 0)
            {
                var traps = U.Row(card, "wrap");
                traps.style.marginTop = 10;
                U.Icon(traps, "warning", 32, U.Hex("FF8282"));
                U.T(traps, Lang.T("attention"), "small", "red", "bold");
                foreach (var w in s.t) U.It(traps, w, "chip", "chip-red");
            }

            if (Lang.Code != "it")
            {
                var trBox = U.Col(card);
                FillTranslation(trBox, s);
            }

            // answer area
            _answers.TryGetValue(s.id, out var given);
            var ans = U.Row(col);
            ans.style.justifyContent = Justify.Center;
            ans.style.marginTop = 6;
            if (given == null)
            {
                U.B(ans, Lang.Code == "it" ? "VERO" : "VERO\n" + Lang.T("true"), () => Answer(s, true), "vf", "btn-green").style.height = 130;
                U.B(ans, Lang.Code == "it" ? "FALSO" : "FALSO\n" + Lang.T("false"), () => Answer(s, false), "vf", "btn-red").style.height = 130;
            }
            else
            {
                bool ok = given.Value == s.True;
                var v = U.Col(col, "card", ok ? "verdict-ok" : "verdict-ko");
                U.T(v, (ok ? Lang.T("right") : Lang.T("wrong")) + "  -  " +
                       Lang.T("official_answer") + ": " + (s.True ? "VERO" : "FALSO"), "h2", "wraptext");
                QuestionText.ShowExplanation(v, s);
            }

            // bottom nav
            var nav = U.Row(_body);
            nav.style.flexShrink = 0;
            nav.style.justifyContent = Justify.SpaceBetween;
            nav.style.paddingLeft = 30; nav.style.paddingRight = 30; nav.style.paddingBottom = 8;
            var prev = U.IB(nav, "back", Lang.T("prev"), () => { if (_idx > 0) { _idx--; Render(); } });
            prev.SetEnabled(_idx > 0);
            prev.style.width = 340;
            var next = U.IB(nav, "forward", Lang.T("next"), () =>
            {
                if (_idx < _list.Count - 1) { _idx++; Render(); }
                else App.I.Dialog(Lang.T("done_title"), Lang.T("done_body", _answers.Count(a => a.Value.HasValue && a.Value.Value == Listato.Get(a.Key).True), _answers.Count),
                    (Lang.T("ok"), () => App.I.Back(), "btn-primary"));
            }, "btn-primary");
            next.style.width = 340;
        }

        void FillTranslation(VisualElement box, Stmt s)
        {
            box.Clear();
            if (!_showTr)
            {
                U.IB(box, "eye", Lang.T("show_translation"), () => { _showTr = true; FillTranslation(box, s); }, "btn-ghost", "btn-small");
                return;
            }
            var tr = QuestionText.Translation(s, out bool machine);
            if (tr != null)
            {
                U.T(box, tr, "tr-text");
                if (machine) U.T(box, Lang.T("machine_note"), "small", "muted");
            }
            else if (Lang.Info.MlKit)
            {
                var wait = U.T(box, Lang.T("translating"), "small", "muted");
                QuestionText.RequestMachine(s, res =>
                {
                    if (box.panel == null) return;
                    if (res == null)
                    {
                        wait.text = Lang.T("translate_failed");
                        U.IB(box, "review", Lang.T("translate_phone"), () => FillTranslation(box, s), "btn-ghost", "btn-small");
                    }
                    else FillTranslation(box, s);
                });
            }
        }

        /// <summary>The Italian sentence as tappable words: tap a word for its meaning.</summary>
        void Words(VisualElement parent, Stmt s)
        {
            var flow = U.El(parent);
            flow.style.flexDirection = FlexDirection.Row;
            flow.style.flexWrap = Wrap.Wrap;
            flow.languageDirection = LanguageDirection.LTR;
            var traps = new HashSet<string>(s.t ?? new string[0]);
            foreach (var raw in Regex.Split(s.it, @"\s+"))
            {
                if (raw.Length == 0) continue;
                var word = raw;
                var core = Regex.Replace(word.ToLowerInvariant(), @"[^\p{L}'’]", "");
                var b = new Button(() => ShowGloss(word)) { text = word };
                b.AddToClassList("word");
                if (traps.Contains(core) || traps.Any(t => t.Contains(' ') && s.it.ToLowerInvariant().Contains(t) && t.Split(' ').Contains(core)))
                    b.AddToClassList("word-trap");
                flow.Add(b);
            }
        }

        void ShowGloss(string word)
        {
            var clean = word.Trim(',', '.', ';', ':', '(', ')', '"', '«', '»');
            var meaning = Lang.Code == "it" ? null : QuestionText.Gloss(clean);
            App.I.CloseOverlays();
            var ov = U.El(App.I.Overlay, "overlay");
            ov.style.backgroundColor = new Color(0, 0, 0, 0.35f);
            ov.pickingMode = PickingMode.Position;
            ov.RegisterCallback<ClickEvent>(_ => ov.RemoveFromHierarchy());
            var d = U.Col(ov, "dialog");
            d.style.width = 900;
            U.It(d, clean, "title");
            Label mean;
            if (!string.IsNullOrEmpty(meaning)) mean = U.T(d, meaning, "tr-text");
            else mean = U.T(d, Lang.Code == "it" ? "" : Lang.T("no_gloss"), "muted");
            if (string.IsNullOrEmpty(meaning) && Lang.Code != "it")
                QuestionText.TranslateCached("w:" + clean.ToLowerInvariant(), clean, res =>
                {
                    if (mean.panel == null) return;
                    mean.text = res; mean.RemoveFromClassList("muted"); mean.AddToClassList("tr-text");
                });
            var row = U.Row(d, "wrap");
            U.IB(row, "speaker", Lang.T("listen_it"), () => ItalianTts.Speak(clean), "btn-small");
            bool saved = Vocab.Has(clean);
            Button sb = null;
            sb = U.IB(row, "star", saved ? Lang.T("word_saved") : Lang.T("save_word"), () =>
            {
                Vocab.Toggle(clean, string.IsNullOrEmpty(mean.text) || mean.ClassListContains("muted") ? "" : mean.text);
                U.SetIBText(sb, Vocab.Has(clean) ? Lang.T("word_saved") : Lang.T("save_word"));
                sb.EnableInClassList("btn-gold", Vocab.Has(clean));
            }, "btn-small", saved ? "btn-gold" : "");
        }

        void Answer(Stmt s, bool v)
        {
            bool ok = v == s.True;
            _answers[s.id] = v;
            Srs.Record(s.id, ok);
            Gamify.Answered(ok);
            if (ok) Sfx.Right(); else Sfx.Wrong();
            Render();
            if (AutoNext && ok && _idx < _list.Count - 1)
                _body.schedule.Execute(() => { if (_answers.ContainsKey(s.id) && _list[_idx] == s) { _idx++; Render(); } }).StartingIn(1200);
        }
    }
}
