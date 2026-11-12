using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PatenteArabo
{
    // ------------------------------------------------------------------ course switcher (home)
    public static class CourseSwitcher
    {
        /// <summary>Row of pills: B | C/CE | CQC Merci | CQC Persone. The professional ones carry a PRO tag until bought.</summary>
        public static void Make(VisualElement parent)
        {
            var row = U.Row(parent, "wrap", "course-row");
            row.style.justifyContent = Justify.Center;
            row.style.marginTop = 6; row.style.marginBottom = 6;
            foreach (var id in Course.All)
            {
                string cid = id;
                bool on = Course.Id == id;
                string text = Course.Short(id) + (id != Course.B && !Premium.HasPro ? "  PRO" : "");
                var b = U.B(row, text, () => Switch(cid), on ? "btn-primary" : "btn-ghost", "btn-small");
                b.style.marginLeft = 6; b.style.marginRight = 6;
            }
        }

        public static void Switch(string id)
        {
            if (Course.Id == id) return;
            Course.Id = id;
            Toast.Show(Lang.T("course_now", Lang.T(Course.NameKey(id))));
            App.I.GoHome();
        }
    }

    // ------------------------------------------------------------------ paywall of the professional section
    public class ProPaywallPage : Page
    {
        readonly string _reason;
        public ProPaywallPage(string reason = null) { _reason = reason; }

        /// <summary>The paywall that fits the course being studied.</summary>
        public static void OpenForCourse(string reason = null)
        {
            if (Course.IsPro) App.I.Go(() => new ProPaywallPage(reason));
            else App.I.Go(() => new PaywallPage(reason));
        }

        public override void OnHide() { Premium.Changed -= Refresh; }
        void Refresh() { if (Premium.HasPro) App.I.Back(); else App.I.Rebuild(); }

        public override void Build(VisualElement root)
        {
            Premium.Changed -= Refresh;
            Premium.Changed += Refresh;
            Chrome.TopBar(root, Lang.T("pro_title"));
            var sc = U.Scroll(root);
            var c = sc.contentContainer;
            c.style.paddingLeft = 70; c.style.paddingRight = 70; c.style.paddingBottom = 40;

            var head = U.Col(c, "center");
            U.Icon(head, "drive", 110, U.Hex("F2C94C"));
            { var hl = U.T(head, Lang.T("pro_headline"), "title", "wraptext"); hl.style.unityTextAlign = TextAnchor.MiddleCenter; hl.style.fontSize = 46; }
            if (!string.IsNullOrEmpty(_reason)) U.T(head, _reason, "gold", "bold", "wraptext").style.unityTextAlign = TextAnchor.MiddleCenter;

            var plans = U.Row(c, "wrap");
            plans.style.justifyContent = Justify.Center;
            Plan(plans, Lang.T("plan_monthly"), Premium.Price(Premium.ProMonthly, "€3,99") + Lang.T("per_month"), Premium.SubLine(Premium.ProMonthly, false, "€3,99"), Premium.ProMonthly, false);
            Plan(plans, Lang.T("plan_yearly"), Premium.Price(Premium.ProYearly, "€24,99") + Lang.T("per_year"), Lang.T("pro_yearly_sub") + "\n" + Premium.SubLine(Premium.ProYearly, true, "€24,99"), Premium.ProYearly, true);
            PaywallPage.Legal(c, Lang.T("pro_note"));

            // benefits in two columns: the plans and prices stay on the first screen
            var feats = U.Row(c, "card", "wrap");
            foreach (var k in new[] { "prof_lists", "prof_exams", "prof_translate", "prof_glossary", "prof_review", "prof_offline" })
            {
                var r = U.Row(feats);
                r.style.marginBottom = 6; r.style.width = Length.Percent(50);
                U.Icon(r, "check", 34, U.Hex("6EDC96"));
                var l = U.T(r, Lang.T(k), "ex-text");
                l.style.marginLeft = 14; l.style.marginRight = 14; l.style.flexShrink = 1;
            }

            var foot = U.Row(c, "wrap");
            foot.style.justifyContent = Justify.Center;
            U.B(foot, Lang.T("restore"), Premium.Restore, "btn-ghost", "btn-small");
            U.B(foot, Lang.T("manage_subs"), Premium.ManageSubscriptions, "btn-ghost", "btn-small");
            // App Store rule 3.1.2: subscriptions show the terms of use and the privacy policy
            U.B(foot, Lang.T("terms_eula"), () => Application.OpenURL(NativeUtil.TermsUrl), "btn-ghost", "btn-small");
            U.B(foot, Lang.T("privacy"), () => Application.OpenURL(NativeUtil.PrivacyUrl), "btn-ghost", "btn-small");
        }

        static void Plan(VisualElement parent, string title, string price, string sub, string id, bool best)
        {
            var card = U.Col(parent, "card", best ? "card-hl" : "");
            card.style.width = 470; card.style.alignItems = Align.Center;
            if (best) U.T(card, Lang.T("best_value"), "gold", "bold");
            U.T(card, title, "h2").style.unityTextAlign = TextAnchor.MiddleCenter;
            // price big, the period ("/mese") small next to it, so it always fits the card
            int cut = price.IndexOf(" /", System.StringComparison.Ordinal);
            var pr = U.Row(card); pr.style.alignItems = Align.FlexEnd; pr.style.justifyContent = Justify.Center;
            U.It(pr, cut > 0 ? price.Substring(0, cut) : price, "title");
            if (cut > 0) { var per = U.T(pr, price.Substring(cut + 1), "h2", "muted"); per.style.marginBottom = 8; per.style.marginLeft = 6; }
            U.T(card, sub, "muted", "wraptext").style.unityTextAlign = TextAnchor.MiddleCenter;
            if (Premium.Owns(id)) { U.T(card, Lang.T("owned"), "gold", "bold"); return; }
            var b = U.B(card, Lang.T("buy"), () => Premium.Buy(id), best ? "btn-gold" : "btn-primary");
            b.style.width = 380;
        }
    }

    // ------------------------------------------------------------------ glossary of the technical words (trucks, CQC)
    [Serializable] public class GlossTerm { public string it, di, ar, da, en, de, k; public int n; }
    [Serializable] class GlossFile { public GlossTerm[] terms; }

    public static class ProGlossary
    {
        static GlossTerm[] _all;
        public static GlossTerm[] All
        {
            get
            {
                if (_all == null)
                {
                    var ta = Resources.Load<TextAsset>("PatenteArabo/data/glossary_pro");
                    _all = ta != null ? (JsonUtility.FromJson<GlossFile>(ta.text)?.terms ?? new GlossTerm[0]) : new GlossTerm[0];
                }
                return _all;
            }
        }

        /// <summary>Terms of the current course (k = "c", "cqc" or "all").</summary>
        public static IEnumerable<GlossTerm> ForCourse =>
            All.Where(t => t.k == "all" || (Course.Id == Course.C ? t.k == "c" : Course.IsCqc ? t.k == "cqc" : true));
    }

    public class GlossaryPage : Page
    {
        static string _q = "";
        VisualElement _res;

        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, Lang.T("glossary"));
            var tf = new TextField { value = _q };
            tf.style.fontSize = 34; tf.style.height = 84;
            tf.style.marginLeft = 60; tf.style.marginRight = 60; tf.style.flexShrink = 0;
            root.Add(tf);
            U.T(root, Lang.T("glossary_hint"), "small", "muted", "wraptext").style.marginLeft = 64;
            var sc = U.Scroll(root);
            _res = sc.contentContainer;
            _res.style.paddingLeft = 60; _res.style.paddingRight = 60; _res.style.paddingBottom = 40;
            tf.RegisterValueChangedCallback(e => { _q = e.newValue; Run(); });
            Run();
        }

        void Run()
        {
            _res.Clear();
            var q = (_q ?? "").Trim().ToLowerInvariant();
            var list = ProGlossary.ForCourse
                .Where(t => q.Length == 0 || t.it.ToLowerInvariant().Contains(q) || (t.ar ?? "").Contains(q) || (t.en ?? "").ToLowerInvariant().Contains(q))
                .OrderBy(t => t.it, StringComparer.OrdinalIgnoreCase).Take(q.Length == 0 ? 400 : 80).ToList();
            U.T(_res, Lang.T("results", list.Count), "muted");
            foreach (var t in list) Card(t);
        }

        void Card(GlossTerm t)
        {
            var card = U.Col(_res, "card");
            var head = U.Row(card);
            var term = U.It(head, t.it, "h2");
            term.style.flexGrow = 1; term.style.flexShrink = 1; term.style.marginBottom = 0;
            U.Raw(head, "", () => ItalianTts.Speak(t.it), "btn-ghost", "btn-small").Add(U.IconTex("speaker") != null ? MakeIcon() : new VisualElement());
            if (t.n > 0) U.T(card, Lang.T("gloss_count", t.n), "small", "muted");
            U.It(card, t.di, "it-text", "wraptext");
            string code = Lang.Code;
            if (code == "it") return;
            string word = code == "ar" ? t.ar : code == "en" ? t.en : null;
            string def = code == "ar" ? t.da : code == "en" ? t.de : null;
            if (word != null)
            {
                U.T(card, word, "bold", "green");
                if (!string.IsNullOrEmpty(def)) U.T(card, def, "tr-text", "wraptext");
                return;
            }
            // other languages: English as a bridge right away, then the phone's translation of the Italian
            var w = U.T(card, t.en, "bold", "green");
            var d = U.T(card, t.de, "tr-text", "wraptext");
            QuestionText.TranslateCached("g:" + t.it, t.it, res => { if (w.panel != null) w.text = res; });
            QuestionText.TranslateCached("gd:" + t.it, t.di, res => { if (d.panel != null) d.text = res; });
        }

        static VisualElement MakeIcon()
        {
            var e = new VisualElement();
            e.style.width = 44; e.style.height = 44;
            e.style.backgroundImage = new StyleBackground(U.IconTex("speaker"));
            e.style.unityBackgroundImageTintColor = U.Hex("7DD3FC");
            return e;
        }
    }
}
