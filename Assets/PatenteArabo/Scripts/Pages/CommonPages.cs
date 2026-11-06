using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PatenteArabo
{
    public static class Brand
    {
        public const string Name = "Patente Italiana";
        /// <summary>"Patente Italiana · <translated name>" (just the Italian name when the language is Italian).</summary>
        public static string Full => Name + " · " + Lang.T("app_sub");
        public const string Tagline = "Quiz Patente B";
    }

    public static class Chrome
    {
        /// <summary>Top bar with back button and title. Returns the area for extra buttons.</summary>
        public static VisualElement TopBar(VisualElement root, string title, Action back = null)
        {
            var bar = U.Row(root, "topbar");
            bar.style.flexShrink = 0;
            var b = U.B(bar, "", back ?? (() => App.I.Back()), "icon-btn");
            b.style.alignItems = Align.Center; b.style.justifyContent = Justify.Center;
            U.Icon(b, "back", 44);
            var t = U.T(bar, title, "topbar-title");
            t.style.unityTextAlign = Lang.Rtl ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            var extra = U.Row(bar);
            return extra;
        }

        public static Button IconButton(VisualElement parent, string icon, Action click, string cls = "icon-btn")
        {
            var b = U.B(parent, "", click, cls);
            b.style.alignItems = Align.Center; b.style.justifyContent = Justify.Center;
            U.Icon(b, icon, 44);
            return b;
        }
    }

    // ------------------------------------------------------------------ language
    public class LanguagePage : Page
    {
        readonly bool _first;
        public LanguagePage(bool first) { _first = first; }

        public override void Build(VisualElement root)
        {
            if (!_first) Chrome.TopBar(root, Lang.T("language"));
            var head = U.Col(root, "pad");
            head.style.alignItems = Align.Center;
            head.style.flexShrink = 0;
            if (_first)
            {
                var logo = U.Img(head, Resources.Load<Texture2D>("PatenteArabo/Icons/logo"), "");
                logo.style.width = 150; logo.style.height = 150;
                U.It(head, Brand.Name, "title");
                U.It(head, "Italian Driving Licence · الرخصة الإيطالية · Permis italien", "small", "muted");
                U.It(head, "Scegli la lingua · Choose your language · اختر لغتك · 选择语言 · भाषा चुनें", "subtitle");
            }
            var sc = U.Scroll(root);
            var grid = U.El(sc.contentContainer, "row", "wrap");
            grid.style.justifyContent = Justify.Center;
            grid.style.paddingBottom = 40;
            foreach (var l in Lang.All)
            {
                var info = l;
                var b = U.Raw(grid, "", () =>
                {
                    Sfx.Click();
                    Lang.Set(info.Code);
                    App.I.GoHome();
                }, "lang-tile");
                b.style.flexDirection = FlexDirection.Column;
                b.style.justifyContent = Justify.Center;
                var n = new Label(info.Native) { pickingMode = PickingMode.Ignore };
                n.style.fontSize = 34; n.style.unityFontStyleAndWeight = Lang.ArabicScript ? FontStyle.Normal : FontStyle.Bold; n.style.color = Color.white;
                n.style.unityTextAlign = TextAnchor.MiddleCenter;
                n.languageDirection = info.Rtl ? LanguageDirection.RTL : LanguageDirection.LTR;
                b.Add(n);
                var e = new Label(info.English) { pickingMode = PickingMode.Ignore };
                e.style.fontSize = 20; e.style.color = U.Hex("96A6BE"); e.style.unityTextAlign = TextAnchor.MiddleCenter;
                b.Add(e);
                if (Lang.Chosen && info.Code == Lang.Code) b.AddToClassList("lang-tile-on");
            }
        }
    }

    // ------------------------------------------------------------------ chapters
    public class ChaptersPage : Page
    {
        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, Lang.T("study"));
            var sc = U.Scroll(root);
            var quick = U.Row(sc.contentContainer, "wrap");
            quick.style.justifyContent = Justify.Center;
            quick.style.marginTop = 10;
            U.IB(quick, "random", Lang.T("random_all"), () => App.I.Go(() => new StudyPage(StudyPage.Source.Random, 0)), "btn-primary");
            U.IB(quick, "warning", Lang.T("weak_only"), () => App.I.Go(() => new StudyPage(StudyPage.Source.Weak, 0)));
            if (Listato.All.Any(s => !string.IsNullOrEmpty(s.f))) U.IB(quick, "image", Lang.T(Course.IsB ? "signs_only" : "figures_only"), () => App.I.Go(() => new StudyPage(StudyPage.Source.Figures, 0)));
            if (Course.IsPro) U.IB(quick, "language", Lang.T("glossary"), () => App.I.Go(() => new GlossaryPage()));

            var grid = U.Row(sc.contentContainer, "wrap");
            grid.style.justifyContent = Justify.Center;
            grid.style.paddingBottom = 40;
            foreach (var ch in Listato.Chapters)
            {
                var c = ch;
                bool open = Premium.ChapterOpen(c.n);
                var tile = U.Raw(grid, "", () => { Sfx.Click(); if (Course.IsB) App.I.Go(() => new TheoryPage(c.n)); else App.I.Go(() => new StudyPage(StudyPage.Source.Chapter, c.n)); }, "chapter-tile");
                var row = U.Row(tile);
                var num = new Label(c.n.ToString()); num.AddToClassList("chapter-num");
                row.Add(num);
                var col = U.Col(row);
                col.style.flexGrow = 1; col.style.flexShrink = 1;
                U.T(col, QuestionText.ChapterTitle(c.n), "chapter-title");
                U.It(col, c.it, "small", "muted", "wraptext");
                if (!open) U.Icon(row, "lock", 40, U.Hex("F2C94C"));
                var info = U.Row(tile);
                info.style.justifyContent = Justify.SpaceBetween;
                info.style.marginTop = 8;
                U.T(info, Lang.T("chapter_info", c.count, Mathf.RoundToInt(Srs.SeenShare(c.n) * 100)), "small", "muted");
                U.T(info, Mathf.RoundToInt(Srs.Readiness(c.n) * 100) + "%", "small", "green");
                U.Bar(tile, Srs.Readiness(c.n));
            }
        }
    }

    // ------------------------------------------------------------------ progress
    public class ProgressPage : Page
    {
        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, Lang.T("progress"));
            var sc = U.Scroll(root);
            var c = sc.contentContainer;
            var sum = U.Row(c, "wrap");
            sum.style.justifyContent = Justify.Center;
            Stat(sum, Mathf.RoundToInt(Srs.OverallReadiness() * 100) + "%", Lang.T("readiness"));
            Stat(sum, Srs.Exams.ToString(), Lang.T("exams_done"));
            Stat(sum, Srs.Passed.ToString(), Lang.T("exams_passed"));
            Stat(sum, Srs.BestExam < 0 ? "-" : Srs.BestExam.ToString(), Lang.T("best_errors"));
            Stat(sum, Srs.DueList().Count.ToString(), Lang.T("to_review"));

            var tip = U.T(c, Lang.T("readiness_tip"), "muted", "wraptext");
            tip.style.marginLeft = 50; tip.style.marginRight = 50; tip.style.marginBottom = 10;

            foreach (var ch in Listato.Chapters.OrderBy(x => Srs.Readiness(x.n)))
            {
                var card = U.Col(c, "card");
                card.style.marginLeft = 40; card.style.marginRight = 40;
                var row = U.Row(card);
                var t = U.T(row, ch.n + ". " + QuestionText.ChapterTitle(ch.n), "bold", "wraptext");
                t.style.flexGrow = 1; t.style.flexShrink = 1;
                U.T(row, Mathf.RoundToInt(Srs.Readiness(ch.n) * 100) + "%", "green", "bold");
                float r = Srs.Readiness(ch.n);
                U.Bar(card, r, r < 0.4f ? U.Hex("D64045") : r < 0.75f ? U.Hex("D4A017") : U.Hex("27AE60"));
            }
        }

        static void Stat(VisualElement parent, string big, string label)
        {
            var card = U.Col(parent, "card", "center");
            card.style.width = 300;
            U.It(card, big, "title");
            U.T(card, label, "muted", "wraptext").style.unityTextAlign = TextAnchor.MiddleCenter;
        }
    }

    // ------------------------------------------------------------------ paywall
    public class PaywallPage : Page
    {
        readonly string _reason, _addon;
        public PaywallPage(string reason = null, string addon = null) { _reason = reason; _addon = addon; }

        public override void OnHide() { Premium.Changed -= Refresh; }
        void Refresh() { if (Premium.Has || (_addon != null && Premium.Owns(_addon))) App.I.Back(); else App.I.Rebuild(); }

        public override void Build(VisualElement root)
        {
            Premium.Changed -= Refresh;
            Premium.Changed += Refresh;
            Chrome.TopBar(root, Lang.T("premium_title"));
            var sc = U.Scroll(root);
            var c = sc.contentContainer;
            c.style.paddingLeft = 70; c.style.paddingRight = 70; c.style.paddingBottom = 40;

            var head = U.Col(c, "center");
            U.Icon(head, "premium", 110, U.Hex("F2C94C"));
            U.T(head, Lang.T("premium_headline"), "title", "wraptext").style.unityTextAlign = TextAnchor.MiddleCenter;
            if (!string.IsNullOrEmpty(_reason)) U.T(head, _reason, "gold", "bold", "wraptext").style.unityTextAlign = TextAnchor.MiddleCenter;

            var feats = U.Col(c, "card");
            foreach (var k in new[] { "pf_chapters", "pf_exams", "pf_drive", "pf_listen", "pf_gold", "pf_review", "pf_offline", "pf_future" })
            {
                var r = U.Row(feats);
                r.style.marginBottom = 8;
                U.Icon(r, "check", 40, U.Hex("6EDC96"));
                var l = U.T(r, Lang.T(k), "ex-text");
                l.style.marginLeft = 14; l.style.marginRight = 14; l.style.flexShrink = 1;
            }

            var plans = U.Row(c, "wrap");
            plans.style.justifyContent = Justify.Center;
            Plan(plans, Lang.T("plan_monthly"), Premium.Price(Premium.Monthly, "€2,99"), Lang.T("plan_monthly_sub"), Premium.Monthly, false);
            Plan(plans, Lang.T("plan_pass"), Premium.Price(Premium.Pass3M, "€4,99"), Lang.T("plan_pass_sub"), Premium.Pass3M, false);
            Plan(plans, Lang.T("plan_lifetime"), Premium.Price(Premium.Lifetime, "€9,99"), Lang.T("plan_lifetime_sub"), Premium.Lifetime, true);

            // single add-ons: the one the user just hit first
            U.T(c, Lang.T("addons_title"), "h2").style.marginTop = 26;
            U.T(c, Lang.T("addons_sub"), "muted", "wraptext");
            var adds = U.Row(c, "wrap");
            adds.style.justifyContent = Justify.Center;
            var order = new List<string>();
            if (_addon != null) order.Add(_addon);
            foreach (var a in Premium.AddOns) if (!order.Contains(a)) order.Add(a);
            foreach (var a in order) AddOn(adds, a, a == _addon);

            var foot = U.Row(c, "wrap");
            foot.style.justifyContent = Justify.Center;
            U.B(foot, Lang.T("restore"), Premium.Restore, "btn-ghost", "btn-small");
            U.B(foot, Lang.T("manage_subs"), Premium.ManageSubscriptions, "btn-ghost", "btn-small");
            // App Store rule 3.1.2: subscriptions show the terms of use and the privacy policy
            U.B(foot, "Terms of Use (EULA)", () => Application.OpenURL(NativeUtil.TermsUrl), "btn-ghost", "btn-small");
            U.B(foot, Lang.T("privacy"), () => Application.OpenURL(NativeUtil.PrivacyUrl), "btn-ghost", "btn-small");
            U.T(c, Lang.T("premium_note"), "small", "muted", "wraptext").style.marginTop = 10;
        }

        static void Plan(VisualElement parent, string title, string price, string sub, string id, bool best)
        {
            var card = U.Col(parent, "card", best ? "card-hl" : "");
            card.style.width = 470; card.style.alignItems = Align.Center;
            if (best) U.T(card, Lang.T("best_value"), "gold", "bold");
            U.T(card, title, "h2").style.unityTextAlign = TextAnchor.MiddleCenter;
            U.It(card, price, "title");
            U.T(card, sub, "muted", "wraptext").style.unityTextAlign = TextAnchor.MiddleCenter;
            var b = U.B(card, Lang.T("buy"), () => Premium.Buy(id), best ? "btn-gold" : "btn-primary");
            b.style.width = 380;
        }

        static void AddOn(VisualElement parent, string id, bool hl)
        {
            string key = id.Replace("addon_", "ad_");
            string price = id == Premium.AddGold ? Premium.Price(id, "€0,99") : Premium.Price(id, "€1,99");
            var card = U.Col(parent, "card", hl ? "card-hl" : "");
            card.style.width = 340; card.style.alignItems = Align.Center;
            U.Icon(card, id == Premium.AddDrive ? "drive" : id == Premium.AddListen ? "listen" : id == Premium.AddExams ? "exam" : "star", 64,
                U.Hex(id == Premium.AddGold ? "F2C94C" : "7DD3FC"));
            U.T(card, Lang.T(key), "bold").style.unityTextAlign = TextAnchor.MiddleCenter;
            U.T(card, Lang.T(key + "_d"), "small", "muted", "wraptext").style.unityTextAlign = TextAnchor.MiddleCenter;
            if (Premium.Owns(id)) { U.T(card, Lang.T("owned"), "gold", "bold"); return; }
            U.It(card, price, "h2");
            var b = U.B(card, Lang.T("buy"), () => Premium.Buy(id), hl ? "btn-gold" : "btn-primary", "btn-small");
            b.style.width = 260;
        }
    }

    // ------------------------------------------------------------------ settings
    public class SettingsPage : Page
    {
        VisualElement _c;

        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, Lang.T("settings"));
            var sc = U.Scroll(root);
            _c = sc.contentContainer;
            _c.style.paddingLeft = 60; _c.style.paddingRight = 60; _c.style.paddingBottom = 50;

            // ---- account
            Section("sec_account");
            if (Premium.HasPro) Row("drive", Lang.T("pro_active"), null, null);
            else Row("drive", Lang.T("pro_title"), Lang.T("pro_headline"), () => App.I.Go(() => new ProPaywallPage()));
            if (Premium.Has) Row("premium", Lang.T("premium_active"), null, null);
            else Row("premium", Lang.T("go_premium"), Lang.T("premium_headline"), () => App.I.Go(() => new PaywallPage()));
            Row("download", Lang.T("restore"), null, Premium.Restore);
            Row("settings", Lang.T("manage_subs"), null, Premium.ManageSubscriptions);

            // ---- language & display
            Section("sec_display");
            Row("language", Lang.T("language"), Lang.Info.Native, () => App.I.Go(() => new LanguagePage(false)));
            Choice("textsize", Lang.T("text_size"), new[] { Lang.T("size_s"), Lang.T("size_m"), Lang.T("size_l") },
                Display.SizeIndex, i => { Display.SizeIndex = i; App.I.ApplyDisplay(); });
            Toggle("eye", Lang.T("italian_only"), StudyPage.ItalianOnly, v => StudyPage.ItalianOnly = v);
            Toggle("warning", Lang.T("show_traps"), StudyPage.ShowTraps, v => StudyPage.ShowTraps = v);

            // ---- study
            Section("sec_study");
            Toggle("speaker", Lang.T("auto_read"), StudyPage.AutoRead, v => StudyPage.AutoRead = v);
            Toggle("forward", Lang.T("auto_next"), StudyPage.AutoNext, v => StudyPage.AutoNext = v);
            Choice("exam", Lang.T("exam_lang_default"), new[] { Lang.T("mode_real"), Lang.T("mode_help") },
                ExamIntroPage.DefaultHelp ? 1 : 0, i => ExamIntroPage.DefaultHelp = i == 1);

            // ---- voice & sound
            Section("sec_voice");
            Choice("speaker", Lang.T("speech_rate"), new[] { Lang.T("slow"), Lang.T("normal"), Lang.T("fast") },
                ItalianTts.Rate < 0.8f ? 0 : ItalianTts.Rate < 1f ? 1 : 2,
                i => { ItalianTts.Rate = new[] { 0.7f, 0.9f, 1.1f }[i]; ItalianTts.Speak("Il segnale raffigurato vieta la sosta."); });
            Row("listen", Lang.T("test_voice"), null, () => ItalianTts.Speak("Benvenuto. Il segnale raffigurato indica un incrocio con diritto di precedenza."));
            Row("settings", Lang.T("install_voice"), Lang.T("install_voice_sub"), NativeUtil.OpenTtsSettings);
            Toggle("sound", Lang.T("sounds"), Sfx.On, v => Sfx.On = v);
            Toggle("bell", Lang.T("vibration"), Sfx.Vibrate, v => Sfx.Vibrate = v);

            // ---- goals
            Section("sec_goals");
            Choice("bell", Lang.T("reminder"), new[] { Lang.T("off"), "08:00", "13:00", "19:00", "21:00" },
                !Reminder.On ? 0 : Array.IndexOf(new[] { 8, 13, 19, 21 }, Reminder.Hour) + 1,
                i => Reminder.Set(i > 0, i == 0 ? 19 : new[] { 8, 13, 19, 21 }[i - 1]));
            Row("calendar", Lang.T("exam_date"), Reminder.ExamDate == "" ? Lang.T("not_set") : Reminder.ExamDate, PickDate);

            // ---- offline
            if (Lang.Info.MlKit && Lang.Code != "it")
            {
                Section("sec_offline");
                Button dl = null;
                bool stop = false;
                dl = Row("download", Lang.T("download_all"), Lang.T("download_all_sub"), () =>
                {
                    dl.SetEnabled(false);
                    QuestionText.PrefetchAll(Listato.All.ToList(), (d, n) =>
                    {
                        if (dl.panel == null) { stop = true; return; }
                        var l = dl.Q<Label>("sub");
                        if (l != null) l.text = d < 0 ? Lang.T("translate_failed") : d >= n ? Lang.T("download_done") : d + " / " + n;
                        if (d < 0) dl.SetEnabled(true);
                    }, () => stop);
                });
                Row("trash", Lang.T("clear_cache"), null, () => { QuestionText.ClearMachine(); Toast.Show(Lang.T("deleted")); });
            }

            // ---- data & privacy
            Section("sec_privacy");
            Row("privacy", Lang.T("privacy"), null, () => App.I.Dialog(Lang.T("privacy"), Lang.T("privacy_text"),
                (Lang.T("privacy_full"), () => Application.OpenURL(NativeUtil.PrivacyUrl), ""), (Lang.T("ok"), null, "btn-primary")));
            Row("review", Lang.T("reset_progress"), null, () => App.I.Dialog(Lang.T("reset_progress"), Lang.T("reset_confirm"),
                (Lang.T("cancel"), null, ""), (Lang.T("delete"), () => { Srs.DeleteAll(); Toast.Show(Lang.T("deleted")); App.I.GoHome(); }, "btn-red")));
            Row("trash", Lang.T("delete_data"), null, () => App.I.Dialog(Lang.T("delete_data"), Lang.T("delete_confirm"),
                (Lang.T("cancel"), null, ""), (Lang.T("delete"), () => { if (Profile.InChallenge) Challenge.Leave(); Srs.DeleteAll(); QuestionText.ClearMachine(); Toast.Show(Lang.T("deleted")); App.I.GoHome(); }, "btn-red")));

            // ---- about
            Section("sec_about");
            Row("info", Lang.T("about_sources"), null, () => App.I.Go(() => new InfoPage()));
            Row("star", Lang.T("rate_app"), null, () => Application.OpenURL(NativeUtil.StoreUrl));
            Row("share", Lang.T("share_app"), null, () => NativeUtil.Share(Lang.T("share_text") + " " + NativeUtil.StoreUrl));
            if (!string.IsNullOrEmpty(NativeUtil.SupportMail))
                Row("mail", Lang.T("contact"), NativeUtil.SupportMail, () => Application.OpenURL("mailto:" + NativeUtil.SupportMail + "?subject=" + Brand.Name));
#if !UNITY_IOS
            Row("chat", "Parli Italiano", Lang.T("parli_sub"), () => Application.OpenURL("https://play.google.com/store/search?q=Parli%20Italiano&c=apps"));
#endif
            var ver = U.It(_c, Brand.Name + " v" + Application.version + " · " + Listato.All.Count + " · " + Listato.Version, "small", "muted");
            ver.style.marginTop = 30;
            ver.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        void PickDate()
        {
            var ov = U.El(App.I.Overlay, "overlay");
            ov.pickingMode = PickingMode.Position;
            var d = U.Col(ov, "dialog");
            U.T(d, Lang.T("exam_date"), "h2");
            DateTime cur = DateTime.TryParse(Reminder.ExamDate, out var x) ? x : DateTime.Now.Date.AddDays(30);
            var lbl = U.It(d, "", "title");
            lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            void Show() => lbl.text = cur.ToString("yyyy-MM-dd");
            Show();
            var r1 = U.RowLtr(d); r1.style.justifyContent = Justify.Center;
            U.B(r1, "-7", () => { cur = cur.AddDays(-7); Show(); }, "btn-small");
            U.B(r1, "-1", () => { cur = cur.AddDays(-1); Show(); }, "btn-small");
            U.B(r1, "+1", () => { cur = cur.AddDays(1); Show(); }, "btn-small");
            U.B(r1, "+7", () => { cur = cur.AddDays(7); Show(); }, "btn-small");
            U.B(r1, "+30", () => { cur = cur.AddDays(30); Show(); }, "btn-small");
            var r2 = U.Row(d); r2.style.justifyContent = Justify.Center;
            U.B(r2, Lang.T("not_set"), () => { Reminder.ExamDate = ""; ov.RemoveFromHierarchy(); App.I.Rebuild(); }, "btn-ghost");
            U.B(r2, Lang.T("ok"), () => { Reminder.ExamDate = cur.ToString("yyyy-MM-dd"); ov.RemoveFromHierarchy(); App.I.Rebuild(); }, "btn-primary");
        }

        void Section(string key)
        {
            var t = U.T(_c, Lang.T(key), "gold", "bold");
            t.style.marginTop = 26; t.style.marginBottom = 6; t.style.fontSize = 26;
        }

        Button Row(string icon, string title, string sub, Action click)
        {
            var b = U.Raw(_c, "", () => { if (click == null) return; Sfx.Click(); click(); }, "setting-row");
            b.style.flexDirection = Lang.Rtl ? FlexDirection.RowReverse : FlexDirection.Row;
            U.Icon(b, icon, 44, U.Hex("9DB8E8"));
            var col = U.Col(b);
            col.style.flexGrow = 1; col.style.flexShrink = 1;
            col.style.marginLeft = 20; col.style.marginRight = 20;
            col.pickingMode = PickingMode.Ignore;
            var t = U.T(col, title, "bold");
            t.pickingMode = PickingMode.Ignore;
            if (!string.IsNullOrEmpty(sub)) { var s = U.T(col, sub, "small", "muted", "wraptext"); s.name = "sub"; s.pickingMode = PickingMode.Ignore; }
            if (click != null) U.Icon(b, "forward", 30, U.Hex("5A6E8E"));
            return b;
        }

        void Toggle(string icon, string title, bool value, Action<bool> set)
        {
            var row = U.Row(_c, "setting-row");
            U.Icon(row, icon, 44, U.Hex("9DB8E8"));
            var t = U.T(row, title, "bold", "wraptext");
            t.style.flexGrow = 1; t.style.flexShrink = 1; t.style.marginLeft = 20; t.style.marginRight = 20;
            var sw = U.Raw(row, "", null, "switch");
            var knob = U.El(sw, "switch-knob");
            void Paint() { sw.EnableInClassList("switch-on", value); }
            Paint();
            sw.clicked += () => { Sfx.Click(); value = !value; set(value); Paint(); };
        }

        void Choice(string icon, string title, string[] options, int current, Action<int> set)
        {
            var box = U.Col(_c, "setting-row");
            box.style.alignItems = Align.Stretch;
            var head = U.Row(box);
            U.Icon(head, icon, 44, U.Hex("9DB8E8"));
            var t = U.T(head, title, "bold", "wraptext");
            t.style.marginLeft = 20; t.style.marginRight = 20; t.style.flexShrink = 1;
            var seg = U.Row(box, "wrap");
            seg.style.marginTop = 10;
            var btns = new List<Button>();
            for (int i = 0; i < options.Length; i++)
            {
                int ii = i;
                var b = U.B(seg, options[i], null, "btn-small", "seg");
                b.clicked += () => { current = ii; set(ii); for (int k = 0; k < btns.Count; k++) btns[k].EnableInClassList("btn-primary", k == ii); };
                if (i == current) b.AddToClassList("btn-primary");
                btns.Add(b);
            }
        }
    }

    // ------------------------------------------------------------------ info / sources / disclaimer
    public class InfoPage : Page
    {
        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, Lang.T("about_sources"));
            var sc = U.Scroll(root);
            var c = sc.contentContainer;
            c.style.paddingLeft = 60; c.style.paddingRight = 60; c.style.paddingBottom = 40;
            U.T(c, Lang.T("disclaimer_title"), "h2", "wraptext");
            U.T(c, Lang.T("disclaimer_long"), "ex-text").style.marginBottom = 24;
            U.It(c, "Questa app non è un'applicazione ufficiale del Ministero delle Infrastrutture e dei Trasporti né della Motorizzazione Civile e non è affiliata ad alcun ente governativo. " +
                    "I quesiti provengono dal listato pubblicato dal Ministero sul Portale dell'Automobilista; traduzioni e spiegazioni sono a cura dell'autore dell'app.", "ex-text", "muted").style.marginBottom = 24;
            U.T(c, Lang.T("sources"), "h2");
            Link(c, "Portale dell'Automobilista - Foglio rosa e prove d'esame (listato quiz patente B)",
                "https://www.ilportaledellautomobilista.it/web/portale-automobilista/foglio-rosa-e-prove-new");
            Link(c, "MIT - Circolare prot. 19281 del 05/07/2024 (nuovo listato)", "https://www.mit.gov.it/normativa/circolare-protocollo-19281-del-05072024");
            U.T(c, Lang.T("official_links"), "h2").style.marginTop = 24;
            OfficialLinks.Add(c);
            U.T(c, Lang.T("privacy_title"), "h2").style.marginTop = 24;
            U.T(c, Lang.T("privacy_short"), "ex-text", "muted");
            U.T(c, Lang.T("credits"), "h2").style.marginTop = 24;
            U.It(c, "3D: Kenney.nl (CC0) · Machine translation: Google ML Kit (on device) · Speech: Android Text-to-Speech", "ex-text", "muted");
        }

        static void Link(VisualElement parent, string text, string url)
        {
            var b = U.B(parent, text, () => Application.OpenURL(url), "btn-ghost", "btn-small");
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            b.style.whiteSpace = WhiteSpace.Normal;
            b.style.height = StyleKeyword.Auto;
            b.style.minHeight = 76;
        }
    }

    /// <summary>Display size preference (scales the whole interface).</summary>
    public static class Display
    {
        public static int SizeIndex
        {
            get => PlayerPrefs.GetInt("pa_size", 1);
            set { PlayerPrefs.SetInt("pa_size", value); PlayerPrefs.Save(); }
        }
        public static float Scale => new[] { 0.9f, 1f, 1.15f }[Mathf.Clamp(SizeIndex, 0, 2)];
    }
}
