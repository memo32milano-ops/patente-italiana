using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PatenteArabo
{
    public class ExamOptions
    {
        public bool Translation;   // show the user's language under each statement
        public bool Audio;         // read every statement aloud (like the headphones option at the real exam)
        public string Name = "COGNOME NOME";
    }

    // ------------------------------------------------------------------ intro (app style, user's language)
    public class ExamIntroPage : Page
    {
        readonly bool _audio;
        public ExamIntroPage(bool audio = false) { _audio = audio; }

        public static bool DefaultHelp
        {
            get => PlayerPrefs.GetInt("pa_exam_help", 0) == 1;
            set { PlayerPrefs.SetInt("pa_exam_help", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Opens the exam (or the paywall when the free plan limit is reached).</summary>
        public static void Open(bool audio)
        {
            if (audio && !Premium.HasListen) { App.I.Go(() => new PaywallPage(Lang.T("listen_premium"), Premium.AddListen)); return; }
            if (!Premium.CanStartExam) { App.I.Go(() => new PaywallPage(Lang.T("exam_limit"), Premium.AddExams)); return; }
            App.I.Go(() => new ExamIntroPage(audio));
        }

        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, _audio ? Lang.T("listen") : Lang.T("exam"));
            var sc = U.Scroll(root);
            var c = sc.contentContainer;
            c.style.paddingLeft = 80; c.style.paddingRight = 80; c.style.paddingBottom = 40;

            var rules = U.Col(c, "card");
            U.T(rules, Lang.T("exam_rules_title"), "h2");
            U.T(rules, "- " + Lang.T("rule_30"), "ex-text");
            U.T(rules, "- " + Lang.T("rule_20"), "ex-text");
            U.T(rules, "- " + Lang.T("rule_3"), "ex-text");
            U.T(rules, "- " + Lang.T("rule_lang"), "ex-text");
            if (_audio) U.T(rules, "- " + Lang.T("rule_audio"), "ex-text", "gold");

            var opt = new ExamOptions { Audio = _audio, Translation = DefaultHelp && Lang.Code != "it", Name = PlayerPrefs.GetString("pa_name", "COGNOME NOME") };
            var modeCard = U.Col(c, "card");
            U.T(modeCard, Lang.T("exam_mode"), "h2");
            var row = U.Row(modeCard, "wrap");
            Button real = null, help = null;
            real = U.IB(row, "exam", Lang.T("mode_real"), () => { opt.Translation = false; real.AddToClassList("btn-primary"); help.RemoveFromClassList("btn-primary"); }, opt.Translation ? "" : "btn-primary");
            help = U.IB(row, "language", Lang.T("mode_help"), () => { opt.Translation = true; help.AddToClassList("btn-primary"); real.RemoveFromClassList("btn-primary"); }, opt.Translation ? "btn-primary" : "");
            if (Lang.Code == "it") help.SetEnabled(false);

            var nameCard = U.Col(c, "card");
            U.T(nameCard, Lang.T("candidate_name"), "h2");
            var tf = new TextField { value = opt.Name, maxLength = 40 };
            tf.style.fontSize = 32;
            tf.style.height = 70;
            tf.RegisterValueChangedCallback(e => { opt.Name = e.newValue.ToUpperInvariant(); PlayerPrefs.SetString("pa_name", opt.Name); });
            nameCard.Add(tf);

            var go = U.IB(c, "play", Lang.T("start_exam"), () => { Premium.ExamStarted(); App.I.Go(() => new ExamPage(opt), false); }, "btn-green");
            go.style.height = 130;
            go.style.fontSize = 44;
        }
    }

    // ------------------------------------------------------------------ the replica of the ministerial exam terminal
    public class ExamPage : Page
    {
        enum View { Welcome, Instructions, Questions, Summary }

        readonly ExamOptions _opt;
        readonly List<Stmt> _sheet;
        readonly bool?[] _ans;
        readonly string _scheda;
        View _view = View.Welcome;
        int _cur, _instr, _sumTop;
        float _timeLeft = 20 * 60;
        bool _running, _finished;
        VisualElement _scaler, _frame;
        Label _timer;

        const float W = 1440, H = 1080;

        public ExamPage(ExamOptions opt)
        {
            _opt = opt;
            _sheet = Listato.ExamSheet();
            _ans = new bool?[_sheet.Count];
            _scheda = UnityEngine.Random.Range(10000000, 19999999).ToString();
            if (opt.Translation) QuestionText.PrefetchAll(_sheet, (d, n) => { if (d == n && _view == View.Questions) Render(); }, () => _finished);
        }

        public override bool OnBack()
        {
            if (_finished) return false;
            App.I.Dialog(Lang.T("exit_exam"), Lang.T("exit_exam_body"),
                (Lang.T("cancel"), null, ""),
                (Lang.T("exit"), () => { _finished = true; App.I.Back(); }, "btn-red"));
            return true;
        }

        public override void OnHide() { ItalianTts.Stop(); }

        public override void Build(VisualElement root)
        {
            var bg = U.El(root, "ex-root");
            bg.languageDirection = LanguageDirection.LTR;
            _scaler = U.El(bg);
            _scaler.style.width = W; _scaler.style.height = H;
            _scaler.style.flexShrink = 0;
            bg.RegisterCallback<GeometryChangedEvent>(_ => Fit(bg));
            Render();
        }

        void Fit(VisualElement bg)
        {
            float w = bg.layout.width, h = bg.layout.height;
            if (w <= 0 || h <= 0) return;
            float k = Mathf.Min(w / W, h / H);
            _scaler.style.scale = new Scale(new Vector3(k, k, 1));
            _scaler.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
        }

        public override void Tick(float dt)
        {
            if (!_running || _finished) return;
            _timeLeft -= dt;
            if (_timer != null)
            {
                int t = Mathf.Max(0, Mathf.CeilToInt(_timeLeft));
                _timer.text = (t / 60).ToString("00") + " : " + (t % 60).ToString("00");
                _timer.style.color = t <= 60 ? U.Hex("DC1E28") : U.Hex("0A0A0A");
            }
            Gamify.StudyTick(dt);
            if (_timeLeft <= 0) Finish(true);
        }

        void Render()
        {
            _scaler.Clear();
            _frame = U.Col(_scaler, "ex-frame");
            Header(_frame);
            switch (_view)
            {
                case View.Welcome: Welcome(); break;
                case View.Instructions: Instructions(); break;
                case View.Questions: Questions(); break;
                case View.Summary: Summary(); break;
            }
        }

        void Header(VisualElement f)
        {
            var h = U.El(f, "ex-header");
            var left = U.Col(h);
            left.style.width = 360;
            var badge = new Label("PATENTE B"); badge.AddToClassList("ex-head-left"); left.Add(badge);
            var sub = new Label("simulazione d'esame"); sub.style.fontSize = 20; sub.style.color = U.Hex("5A6470"); left.Add(sub);
            var mid = new Label("SCHEDA\nESAME"); mid.AddToClassList("ex-head-mid"); h.Add(mid);
            var right = U.RowLtr(h);
            right.style.width = 360;
            right.style.justifyContent = Justify.FlexEnd;
            var r = new Label("Simulazione non ufficiale\nper l'esercitazione"); r.AddToClassList("ex-head-right"); right.Add(r);
            U.El(right, "ex-photo");
        }

        // ---------- welcome page (like the terminal before the exam starts)
        void Welcome()
        {
            var p = U.Col(_frame);
            p.style.flexGrow = 1; p.style.alignItems = Align.Center;
            p.style.backgroundColor = U.Hex("DFE6EB");
            var t1 = new Label("Quiz per Esame di Teoria Informatizzato"); t1.style.fontSize = 36; t1.style.color = U.Hex("1E1E1E"); t1.style.marginTop = 120;
            p.Add(t1);
            var t2 = new Label("Simulazione - Patente B"); t2.style.fontSize = 28; t2.style.color = U.Hex("505A64"); t2.style.marginTop = 20;
            p.Add(t2);
            var b = U.Raw(p, "Pronto per la Prova d'Esame", () => { _view = View.Instructions; _instr = 0; Render(); }, "ex-light-btn");
            b.style.width = 360; b.style.height = 70; b.style.marginTop = 80; b.style.fontSize = 24;
            if (_opt.Translation) Tr(p, Lang.T("ready_exam")).style.marginTop = 20;
        }

        static readonly string[][] InstrPages =
        {
            new[] { "Introduzione generale",
                "La scheda d'esame si compila toccando lo schermo con le dita.",
                "Il candidato può spostarsi liberamente da una domanda all'altra, cambiare le risposte date e vedere il riepilogo di tutte le domande con le risposte date.",
                "Non vengono date spiegazioni sul significato di termini o frasi contenuti nelle domande.",
                "Finito il questionario, il candidato conferma le risposte nella schermata di riepilogo. Dopo la conferma la prova è terminata e non si può più modificare la scheda." },
            new[] { "Come rispondere",
                "Per ogni domanda si risponde VERO o FALSO toccando il simbolo V oppure F.",
                "La risposta data è indicata dal simbolo barrato; si può cambiare toccando l'altro simbolo.",
                "I numeri in alto permettono di andare direttamente a una domanda; i riquadri grigi indicano le domande a cui si è già risposto." },
            new[] { "Istruzioni e avvertenze",
                "Tutte le risposte devono essere date entro il tempo a disposizione: le risposte non date sono considerate errate.",
                "Allo scadere del tempo non è più possibile rispondere e il questionario termina automaticamente.",
                "Per terminare, aprire il riepilogo e premere «Chiudi Esame»; verrà chiesta un'ulteriore conferma.",
                "La prova è superata se gli errori sono al massimo 3 su 30 domande." },
        };

        void Instructions()
        {
            var page = InstrPages[_instr];
            var p = U.Col(_frame, "ex-light-page");
            var title = new Label(page[0]); title.AddToClassList("ex-light-title"); p.Add(title);
            var box = U.Col(p, "ex-light-box");
            var trKeys = new[] { "instr1", "instr2", "instr3" };
            for (int i = 1; i < page.Length; i++)
            {
                var l = new Label(page[i]); l.AddToClassList("ex-light-text"); box.Add(l);
            }
            if (_opt.Translation) Tr(box, Lang.T(trKeys[_instr]));
            var nav = U.RowLtr(p);
            nav.style.justifyContent = Justify.Center;
            nav.style.marginTop = 16;
            var prev = U.Raw(nav, "Pagina precedente", () => { if (_instr > 0) { _instr--; Render(); } }, "ex-light-btn");
            prev.SetEnabled(_instr > 0);
            bool last = _instr == InstrPages.Length - 1;
            U.Raw(nav, last ? "Inizia esame" : "Pagina successiva", () =>
            {
                if (last) { _view = View.Questions; _running = true; _cur = 0; Render(); SpeakCurrent(); }
                else { _instr++; Render(); }
            }, "ex-light-btn");
            var pg = new Label((_instr + 1) + "/" + InstrPages.Length); pg.style.fontSize = 28; pg.style.color = U.Hex("1E1E1E");
            pg.style.position = Position.Absolute; pg.style.right = 40; pg.style.bottom = 30;
            p.Add(pg);
        }

        Label Tr(VisualElement parent, string text)
        {
            var l = U.T(parent, text, "ex-tr-light");
            return l;
        }

        // ---------- question screen
        void Questions()
        {
            var s = _sheet[_cur];
            int group = _cur / 10;

            var groups = U.El(_frame, "ex-groups");
            for (int g = 0; g < 3; g++)
            {
                int gg = g;
                var b = U.Raw(groups, "Domande da " + (g * 10 + 1) + " a " + (g * 10 + 10), () => { _cur = gg * 10; Render(); SpeakCurrent(); }, "ex-group");
                if (g == group) b.AddToClassList("ex-group-on");
            }
            var nums = U.El(_frame, "ex-nums");
            for (int i = group * 10; i < group * 10 + 10; i++)
            {
                int ii = i;
                var b = U.Raw(nums, (i + 1).ToString(), () => { _cur = ii; Render(); SpeakCurrent(); }, "ex-num");
                if (i == _cur) b.AddToClassList("ex-num-on");
                else if (_ans[i].HasValue) b.AddToClassList("ex-num-answered");
            }
            var strip = U.El(_frame, "ex-strip");
            for (int i = 0; i < _sheet.Count; i++)
            {
                var l = new Label((i + 1).ToString()); l.AddToClassList("ex-strip-cell");
                if (i == _cur) l.AddToClassList("ex-strip-on");
                else if (_ans[i].HasValue) l.AddToClassList("ex-strip-answered");
                strip.Add(l);
            }

            var body = U.El(_frame, "ex-body");
            var img = U.El(body, "ex-img");
            var fig = Listato.Figure(s);
            if (fig != null) img.style.backgroundImage = new StyleBackground(fig);

            var right = U.Col(body, "ex-right");
            var qh = U.El(right, "ex-qhead");
            if (_opt.Audio)
            {
                var spk = U.Raw(qh, "", () => ItalianTts.Speak(s.it), "ex-help");
                spk.style.width = 80; spk.style.marginRight = 20; spk.style.alignItems = Align.Center; spk.style.justifyContent = Justify.Center;
                U.Icon(spk, "speaker", 34, U.Hex("1E1E1E"));
            }
            var qnl = new Label("Domanda numero"); qnl.AddToClassList("ex-qnum-label"); qh.Add(qnl);
            var qn = new Label((_cur + 1).ToString()); qn.AddToClassList("ex-qnum"); qh.Add(qn);

            var tb = U.El(right, "ex-qtext-box");
            var inner = U.Col(tb, "ex-qtext-inner");
            var qt = new Label(s.it); qt.AddToClassList("ex-qtext"); inner.Add(qt);
            if (_opt.Translation)
            {
                var tr = QuestionText.Translation(s, out _);
                if (tr != null) U.T(inner, tr, "ex-tr");
            }

            var answers = U.Col(right, "ex-answers");
            var vf = U.El(answers, "ex-vf-row");
            VfButton(vf, "Vero", "V", true);
            VfButton(vf, "Falso", "F", false);
            var hint = new Label("per terminare l'esercitazione andare al riepilogo (toccando il simbolo del riepilogo qui sotto) e premere il bottone 'Chiudi Esame'");
            hint.AddToClassList("ex-hint");
            hint.style.whiteSpace = WhiteSpace.Normal;
            answers.Add(hint);

            Footer(true);
        }

        void VfButton(VisualElement parent, string label, string letter, bool value)
        {
            var col = U.Col(parent, "ex-vf");
            var l = new Label(label); l.AddToClassList("ex-vf-label"); col.Add(l);
            bool chosen = _ans[_cur].HasValue && _ans[_cur].Value == value;
            var b = U.Raw(col, letter, () =>
            {
                _ans[_cur] = value;
                Render();
            }, "ex-vf-btn");
            if (chosen)
            {
                b.AddToClassList("ex-vf-btn-x");
                var x = U.Icon(b, "cross", 90, U.Hex("1E4696"));
                x.style.position = Position.Absolute;
                x.style.left = 0; x.style.top = 0;
            }
        }

        void Footer(bool questionNav)
        {
            var f = U.El(_frame, "ex-footer");
            var tbox = U.Col(f, "ex-timer-box");
            var tl = new Label("TEMPO A DISPOSIZIONE"); tl.AddToClassList("ex-timer-label"); tbox.Add(tl);
            _timer = new Label("20 : 00"); _timer.AddToClassList("ex-timer"); tbox.Add(_timer);
            Tick(0);

            var fields = U.Col(f, "ex-fields");
            var l1 = new Label("Scheda Esame N."); l1.AddToClassList("ex-field-label"); fields.Add(l1);
            var v1 = new Label(_scheda); v1.AddToClassList("ex-field"); fields.Add(v1);
            var l2 = new Label("Cognome e Nome del Candidato"); l2.AddToClassList("ex-field-label"); fields.Add(l2);
            var v2 = new Label(_opt.Name); v2.AddToClassList("ex-field"); fields.Add(v2);

            var mid = U.Col(f, "ex-mid");
            U.Raw(mid, "Aiuto", () => App.I.Dialog("Aiuto", string.Join("\n\n", InstrPages.Select(p => string.Join("\n", p.Skip(1)))) +
                (Lang.Code != "it" ? "\n\n" + Lang.T("instr1") + "\n" + Lang.T("instr3") : ""), ("OK", null, "btn-primary")), "ex-help");
            var ab = new Label("Abilitaz. Richiesta: B"); ab.AddToClassList("ex-abil"); mid.Add(ab);

            if (!questionNav) return;
            var navs = U.El(f, "ex-navs");
            Nav(navs, "Riepilogo\nScheda", "menu", () => { _view = View.Summary; _sumTop = Mathf.Clamp(_cur - 1, 0, _sheet.Count - 4); ItalianTts.Stop(); Render(); }, true);
            Nav(navs, "Domanda\nPrecedente", "tri_left", () => { if (_cur > 0) { _cur--; Render(); SpeakCurrent(); } }, false);
            Nav(navs, "Domanda\nSuccessiva", "tri_right", () => { if (_cur < _sheet.Count - 1) { _cur++; Render(); SpeakCurrent(); } }, false);
        }

        static void Nav(VisualElement parent, string label, string icon, Action click, bool square)
        {
            var col = U.Col(parent, "ex-nav");
            var l = new Label(label); l.AddToClassList("ex-nav-label"); col.Add(l);
            var b = U.Raw(col, "", click, "ex-nav-btn");
            b.style.alignItems = Align.Center; b.style.justifyContent = Justify.Center;
            U.Icon(b, icon, 50, U.Hex("6E8296"));
            if (square) b.AddToClassList("ex-nav-sq");
        }

        void SpeakCurrent()
        {
            if (_opt.Audio && _view == View.Questions) ItalianTts.Speak(_sheet[_cur].it);
        }

        // ---------- summary (riepilogo)
        void Summary()
        {
            var root = U.Col(_frame, "ex-sum-root");
            var list = U.RowLtr(root);
            list.style.flexGrow = 1;
            list.style.alignItems = Align.Stretch;
            var rows = U.Col(list);
            rows.style.flexGrow = 1;
            for (int i = _sumTop; i < Mathf.Min(_sumTop + 4, _sheet.Count); i++)
            {
                int ii = i;
                var s = _sheet[i];
                var row = U.El(rows, "ex-sum-row");
                row.RegisterCallback<ClickEvent>(_ => { _cur = ii; _view = View.Questions; Render(); SpeakCurrent(); });
                var n = new Label((i + 1).ToString()); n.AddToClassList("ex-sum-num"); row.Add(n);
                var figBox = U.El(row);
                figBox.style.width = 136;
                var fig = Listato.Figure(s);
                if (fig != null) U.Img(figBox, fig, "ex-sum-fig");
                var text = U.Col(row, "ex-sum-text");
                var th = new Label("Testo della domanda"); th.AddToClassList("ex-sum-text-head"); text.Add(th);
                var tt = new Label(s.it); tt.AddToClassList("ex-sum-text-body"); text.Add(tt);
                foreach (var v in new[] { true, false })
                {
                    var cell = U.Col(row, "ex-sum-cell");
                    var ch = new Label(v ? "V" : "F"); ch.AddToClassList("ex-sum-cell-head"); cell.Add(ch);
                    if (_ans[i].HasValue && _ans[i].Value == v) { var ck = U.El(cell, "ex-sum-check"); U.Icon(ck, "check", 40, U.Hex("0A0A0A")); }
                }
            }
            var arrows = U.Col(list);
            arrows.style.width = 100; arrows.style.justifyContent = Justify.Center; arrows.style.alignItems = Align.Center;
            var up = U.Raw(arrows, "", () => { _sumTop = Mathf.Max(0, _sumTop - 4); Render(); }, "ex-nav-btn");
            up.style.marginBottom = 40; up.style.alignItems = Align.Center; up.style.justifyContent = Justify.Center;
            U.Icon(up, "tri_up", 50, U.Hex("6E8296"));
            var dn = U.Raw(arrows, "", () => { _sumTop = Mathf.Min(_sheet.Count - 4, _sumTop + 4); Render(); }, "ex-nav-btn");
            dn.style.alignItems = Align.Center; dn.style.justifyContent = Justify.Center;
            U.Icon(dn, "tri_down", 50, U.Hex("6E8296"));

            var btns = U.RowLtr(root);
            btns.style.justifyContent = Justify.Center;
            btns.style.height = 90;
            btns.style.alignItems = Align.Center;
            U.Raw(btns, "Ritorna alle Domande", () => { _view = View.Questions; Render(); SpeakCurrent(); }, "ex-sum-btn");
            U.Raw(btns, "Chiudi Esame", ConfirmClose, "ex-sum-btn");
            if (_opt.Translation)
            {
                var tr = U.T(root, Lang.T("summary_help"), "ex-tr");
                tr.style.marginLeft = 30; tr.style.marginRight = 30;
            }
            Footer(false);
        }

        void ConfirmClose()
        {
            int unanswered = _ans.Count(a => !a.HasValue);
            string body = "Confermi la consegna della scheda d'esame?" + (unanswered > 0 ? "\nDomande senza risposta: " + unanswered : "");
            if (Lang.Code != "it") body += "\n\n" + Lang.T("confirm_close", unanswered);
            App.I.Dialog("Chiudi Esame", body,
                ("Annulla", null, ""),
                ("Conferma", () => Finish(false), "btn-primary"));
        }

        void Finish(bool timeUp)
        {
            if (_finished) return;
            _finished = true;
            _running = false;
            ItalianTts.Stop();
            int errors = 0;
            for (int i = 0; i < _sheet.Count; i++)
            {
                bool ok = _ans[i].HasValue && _ans[i].Value == _sheet[i].True;
                if (!ok) errors++;
                if (_ans[i].HasValue) Srs.Record(_sheet[i].id, ok, false);
                else Srs.Record(_sheet[i].id, false, false);
            }
            Srs.ExamDone(errors);
            Gamify.ExamFinished(errors);
            ExamHistory.Add(_sheet, _ans, errors, 20 * 60 - Mathf.Max(0, _timeLeft));
            Challenge.Submit(true);
            var sheet = _sheet; var ans = _ans;
            float used = 20 * 60 - Mathf.Max(0, _timeLeft);
            App.I.Go(() => new ExamResultPage(sheet, ans, timeUp, used), false);
        }
    }

    // ------------------------------------------------------------------ result (app style, user's language)
    public class ExamResultPage : Page
    {
        readonly List<Stmt> _sheet;
        readonly bool?[] _ans;
        readonly bool _timeUp;
        readonly float _used;

        public ExamResultPage(List<Stmt> sheet, bool?[] ans, bool timeUp, float used)
        { _sheet = sheet; _ans = ans; _timeUp = timeUp; _used = used; }

        public override void Build(VisualElement root)
        {
            int errors = 0;
            for (int i = 0; i < _sheet.Count; i++) if (!(_ans[i].HasValue && _ans[i].Value == _sheet[i].True)) errors++;
            bool pass = errors <= 3;
            if (pass) Sfx.Right(); else Sfx.Wrong();

            Chrome.TopBar(root, Lang.T("result"), () => App.I.GoHome());
            var sc = U.Scroll(root);
            var c = sc.contentContainer;
            c.style.paddingLeft = 50; c.style.paddingRight = 50; c.style.paddingBottom = 40;

            var head = U.Col(c, "card", pass ? "verdict-ok" : "verdict-ko");
            head.style.alignItems = Align.Center;
            U.It(head, pass ? "IDONEO" : "NON IDONEO", "title");
            U.T(head, pass ? Lang.T("passed") : Lang.T("failed"), "h2");
            int m = Mathf.FloorToInt(_used / 60), s = Mathf.FloorToInt(_used % 60);
            U.T(head, Lang.T("errors_time", errors, m + ":" + s.ToString("00")) + (_timeUp ? "  -  " + Lang.T("time_up") : ""), "subtitle");

            var btns = U.Row(c);
            btns.style.justifyContent = Justify.Center;
            U.IB(btns, "review", Lang.T("new_exam"), () => { App.I.GoHome(); ExamIntroPage.Open(false); }, "btn-primary");
            U.IB(btns, "home", Lang.T("home"), () => App.I.GoHome());

            if (pass)
            {
                Fx.Confetti();
                if (!Premium.HasExams)
                {
                    var up = U.Row(c, "card", "card-hl");
                    up.style.alignItems = Align.Center;
                    U.Icon(up, "premium", 64, U.Hex("F2C94C"));
                    var ut = U.T(up, Lang.T("passed_upsell"), "bold", "wraptext");
                    ut.style.flexShrink = 1; ut.style.flexGrow = 1; ut.style.marginLeft = 18; ut.style.marginRight = 18;
                    U.B(up, Lang.T("go_premium"), () => App.I.Go(() => new PaywallPage(null, Premium.AddExams)), "btn-gold", "btn-small");
                }
            }
            if (errors > 0) U.T(c, Lang.T("your_errors"), "h2").style.marginTop = 20;
            for (int i = 0; i < _sheet.Count; i++)
            {
                var st = _sheet[i];
                bool ok = _ans[i].HasValue && _ans[i].Value == st.True;
                if (ok) continue;
                var card = U.Row(c, "card", "verdict-ko");
                card.style.alignItems = Align.FlexStart;
                var fig = Listato.Figure(st);
                if (fig != null) { var f = U.Img(card, fig, "figure"); f.style.width = 220; f.style.height = 220; f.style.flexShrink = 0; }
                var col = U.Col(card);
                col.style.flexGrow = 1; col.style.flexShrink = 1;
                U.It(col, (i + 1) + ". " + st.it, "it-text").style.fontSize = 32;
                var tr = QuestionText.Translation(st, out _);
                if (tr != null) U.T(col, tr, "tr-text").style.fontSize = 28;
                string yours = _ans[i].HasValue ? (_ans[i].Value ? "VERO" : "FALSO") : "-";
                U.T(col, Lang.T("you_answered", yours, st.True ? "VERO" : "FALSO"), "bold", "red").style.marginTop = 8;
                QuestionText.ShowExplanation(col, st);
            }
        }
    }
}
