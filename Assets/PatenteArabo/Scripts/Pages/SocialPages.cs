using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PatenteArabo
{
    // ------------------------------------------------------------------ profile
    public class ProfilePage : Page
    {
        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, Lang.T("profile"));
            var sc = U.Scroll(root);
            var c = sc.contentContainer;
            c.style.paddingLeft = 70; c.style.paddingRight = 70; c.style.paddingBottom = 50;

            var head = U.Row(c, "card");
            var av = U.El(head, "avatar-big");
            if (Premium.HasGold) av.AddToClassList("gold-frame");
            av.style.backgroundImage = new StyleBackground(Profile.AvatarTexture(Profile.Avatar));
            var col = U.Col(head);
            col.style.marginLeft = 30; col.style.marginRight = 30; col.style.flexGrow = 1;
            U.T(col, string.IsNullOrEmpty(Profile.Name) ? Lang.T("no_name") : Profile.Name, "title");
            U.T(col, Lang.T("level", Gamify.Level) + "  ·  " + Gamify.Xp + " XP  ·  " + Lang.T("study_time") + " " + Gamify.FormatTime(Gamify.StudySeconds), "subtitle");
            U.T(col, Lang.T("streak_best", Gamify.Streak, Gamify.BestStreak) + "  ·  " + Lang.T("accuracy") + " " + Mathf.RoundToInt(Gamify.Accuracy * 100) + "%", "muted");

            U.T(c, Lang.T("your_name"), "h2").style.marginTop = 20;
            var tf = new TextField { value = Profile.Name, maxLength = 20 };
            tf.style.fontSize = 34; tf.style.height = 80;
            tf.RegisterValueChangedCallback(e => Profile.Name = Filter(e.newValue));   // saved while typing (back button safe)
            tf.RegisterCallback<FocusOutEvent>(_ => { Profile.Name = Filter(tf.value); tf.SetValueWithoutNotify(Profile.Name); });
            c.Add(tf);
            U.T(c, Lang.T("name_note"), "small", "muted", "wraptext");

            U.T(c, Lang.T("country"), "h2").style.marginTop = 20;
            var cr = U.Row(c, "wrap");
            foreach (var code in new[] { "IT", "EG", "MA", "TN", "DZ", "AL", "RO", "UA", "IN", "BD", "PK", "CN", "PH", "NG", "SN", "PE", "EC", "BR", "LK", "MD", "OTHER" })
            {
                var cc = code;
                var b = U.B(cr, code == "OTHER" ? Lang.T("other") : code, () => { Profile.Country = cc; App.I.Rebuild(); }, "btn-small", Profile.Country == code ? "btn-primary" : "");
                b.style.minWidth = 110;
            }

            U.T(c, Lang.T("avatar"), "h2").style.marginTop = 20;
            var ag = U.Row(c, "wrap");
            for (int i = 0; i < 16; i++)
            {
                int ii = i;
                var a = U.Raw(ag, "", () => { Profile.Avatar = ii; App.I.Rebuild(); }, "avatar-pick");
                a.style.backgroundImage = new StyleBackground(Profile.AvatarTexture(i));
                if (Profile.Avatar == i) a.AddToClassList("avatar-pick-on");
            }
            var pr = U.Row(c, "wrap");
            pr.style.marginTop = 10;
            U.IB(pr, "image", Lang.T("use_photo"), () =>
            {
                App.I.Dialog(Lang.T("use_photo"), Lang.T("photo_rules"),
                    (Lang.T("cancel"), null, ""),
                    (Lang.T("accept"), () => Profile.PickPhoto(bytes =>
                    {
                        if (bytes == null) return;
                        Profile.SetPhoto(bytes);
                        App.I.Rebuild();
                    }), "btn-primary"));
            }, Profile.Avatar < 0 ? "btn-primary" : "");
            if (Profile.Photo != null) U.IB(pr, "trash", Lang.T("remove_photo"), () => { Profile.DeletePhoto(); App.I.Rebuild(); }, "btn-ghost");

            var chHead = U.T(c, Lang.T("challenge"), "h2");
            chHead.style.marginTop = 26;
            var join = U.Row(c, "setting-row");
            // the online challenge needs the Unity Gaming Services build; without it nothing "coming soon" is shown
            if (!Challenge.Available) { chHead.style.display = DisplayStyle.None; join.style.display = DisplayStyle.None; }
            U.Icon(join, "star", 44, U.Hex("F59E0B"));
            var jt = U.T(join, Lang.T("join_challenge"), "bold", "wraptext");
            jt.style.flexGrow = 1; jt.style.flexShrink = 1; jt.style.marginLeft = 20; jt.style.marginRight = 20;
            var sw = U.Raw(join, "", null, "switch");
            U.El(sw, "switch-knob");
            sw.EnableInClassList("switch-on", Profile.InChallenge);
            sw.clicked += () =>
            {
                if (!Profile.InChallenge)
                {
                    if (string.IsNullOrEmpty(Profile.Name)) { Toast.Show(Lang.T("need_name")); return; }
                    App.I.Dialog(Lang.T("challenge"), Lang.T("challenge_rules") + "\n\n" + Lang.T("age_confirm"),
                        (Lang.T("cancel"), null, ""),
                        (Lang.T("accept"), () => { Profile.AcceptedRules = true; Profile.InChallenge = true; Challenge.Submit(true); App.I.Rebuild(); }, "btn-primary"));
                }
                else
                {
                    App.I.Dialog(Lang.T("leave_challenge"), Lang.T("leave_challenge_body"),
                        (Lang.T("cancel"), null, ""),
                        (Lang.T("leave"), async () => { await Challenge.Leave(); App.I.Rebuild(); }, "btn-red"));
                }
            };
        }

        /// <summary>Keeps names short and removes characters that could be abused.</summary>
        static string Filter(string s)
        {
            s = (s ?? "").Trim();
            var chars = s.Where(ch => char.IsLetterOrDigit(ch) || ch == ' ' || ch == '.' || ch == '_' || ch == '-').ToArray();
            s = new string(chars);
            if (s.Length > 20) s = s.Substring(0, 20);
            return s;
        }
    }

    // ------------------------------------------------------------------ leaderboard
    public class LeaderboardPage : Page
    {
        static int _tab;
        VisualElement _list;

        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, Lang.T("challenge"));
            var tabs = U.Row(root);
            tabs.style.justifyContent = Justify.Center; tabs.style.flexShrink = 0;
            string[] names = { Lang.T("lb_week_xp"), Lang.T("lb_week_time"), Lang.T("lb_all_xp") };
            for (int i = 0; i < 3; i++)
            {
                int ii = i;
                U.B(tabs, names[i], () => { _tab = ii; App.I.Rebuild(); }, "btn-small", _tab == i ? "btn-primary" : "btn-ghost").style.minWidth = 300;
            }

            if (!Profile.InChallenge)
            {
                var invite = U.Row(root, "exam-banner");
                U.Icon(invite, "star", 56, U.Hex("FCD34D"));
                var it = U.T(invite, Lang.T("join_invite"), "bold", "wraptext");
                it.style.flexGrow = 1; it.style.flexShrink = 1; it.style.marginLeft = 16; it.style.marginRight = 16;
                U.B(invite, Lang.T("join"), () => App.I.Go(() => new ProfilePage()), "btn-gold", "btn-small");
            }

            var sc = U.Scroll(root);
            _list = sc.contentContainer;
            _list.style.paddingLeft = 60; _list.style.paddingRight = 60; _list.style.paddingBottom = 40;

            if (!Challenge.Available)
            {
                U.T(_list, Lang.T("challenge_soon"), "h2", "wraptext").style.marginTop = 40;
                return;
            }
            U.T(_list, Lang.T("loading"), "muted").style.marginTop = 30;
            Load();
        }

        async void Load()
        {
            try
            {
                var entries = await Challenge.Top(Challenge.Boards[_tab]);
                if (_list.panel == null) return;
                _list.Clear();
                if (entries.Count == 0) { U.T(_list, Lang.T("lb_empty"), "h2", "wraptext"); return; }
                foreach (var e in entries)
                {
                    if (!e.Me && Challenge.IsBlocked(e.PlayerId)) continue;
                    Row(e);
                }
            }
            catch (Exception ex)
            {
                if (_list.panel == null) return;
                _list.Clear();
                U.T(_list, Lang.T("lb_offline"), "h2", "wraptext");
                Debug.LogWarning(ex.Message);
            }
        }

        void Row(RankEntry e)
        {
            var row = U.Row(_list, "lb-row", e.Me ? "lb-me" : "", e.Rank <= 3 ? "lb-top" + e.Rank : "");
            var rank = U.It(row, e.Rank.ToString(), "lb-rank");
            var av = U.El(row, "lb-avatar");
            if (e.Gold || (e.Me && Premium.HasGold)) av.AddToClassList("gold-frame");
            var tex = e.Avatar < 0 && !Challenge.ShowPhotos && !e.Me ? Profile.AvatarTexture(0) : Profile.AvatarTexture(e.Avatar, e.Me ? null : (e.Photo ?? ""));
            av.style.backgroundImage = new StyleBackground(tex);
            var col = U.Col(row);
            col.style.flexGrow = 1; col.style.flexShrink = 1; col.style.marginLeft = 20; col.style.marginRight = 20;
            U.Fixed(col, e.Name + (e.Me ? "  (" + Lang.T("you") + ")" : ""), false, "bold");
            U.It(col, (string.IsNullOrEmpty(e.Country) ? "" : e.Country + " · ") + Lang.T("level", Math.Max(1, e.Level)), "small", "muted");
            string score = _tab == 1 ? Gamify.FormatTime((long)e.Score * 60) : ((long)e.Score) + " XP";
            U.It(row, score, "lb-score");
            if (!e.Me)
            {
                var more = U.B(row, "...", () => App.I.Dialog(e.Name, Lang.T("report_text"),
                    (Lang.T("cancel"), null, ""),
                    (Lang.T("report"), () =>
                    {
                        Challenge.Block(e.PlayerId);
                        Application.OpenURL("mailto:" + NativeUtil.SupportMail
                            + "?subject=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(Brand.Name + " - report " + e.PlayerId)
                            + "&body=" + UnityEngine.Networking.UnityWebRequest.EscapeURL("Player: " + e.Name + "\nID: " + e.PlayerId + "\n\n"));
                        App.I.Rebuild();
                    }, ""),
                    (Lang.T("block"), () => { Challenge.Block(e.PlayerId); App.I.Rebuild(); }, "btn-red")), "btn-ghost", "btn-small");
                more.style.minWidth = 80;
            }
        }
    }

    // ------------------------------------------------------------------ signs catalogue (official pictures)
    public class SignsPage : Page
    {
        static int _chapter = 2;

        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, Lang.T("signs"));
            var tabs = new ScrollView(ScrollViewMode.Horizontal);
            tabs.style.flexShrink = 0; tabs.style.height = 110;
            tabs.horizontalScrollerVisibility = ScrollerVisibility.Hidden; tabs.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            root.Add(tabs);
            var tr = U.RowLtr(tabs.contentContainer);
            foreach (var ch in Listato.Chapters)
            {
                var c = ch;
                if (!Listato.Chapter(c.n).Any(s => !string.IsNullOrEmpty(s.f))) continue;
                U.B(tr, c.n + ". " + QuestionText.ChapterTitle(c.n), () => { _chapter = c.n; App.I.Rebuild(); }, "btn-small", _chapter == c.n ? "btn-primary" : "btn-ghost");
            }

            var sc = U.Scroll(root);
            var grid = U.Row(sc.contentContainer, "wrap");
            grid.style.justifyContent = Justify.Center;
            grid.style.paddingBottom = 40;
            var groups = Listato.Chapter(_chapter).Where(s => !string.IsNullOrEmpty(s.f)).GroupBy(s => s.f).ToList();
            foreach (var g in groups)
            {
                var list = g.ToList();
                var first = list[0];
                var tile = U.Raw(grid, "", () => { Sfx.Click(); App.I.Go(() => new SignDetailPage(list)); }, "sign-tile");
                U.Img(tile, Listato.Figure(first), "sign-img");
                var right = list.Where(s => s.True).Take(1).FirstOrDefault();
                if (right != null)
                {
                    var l = U.It(tile, right.it, "small", "wraptext");
                    l.style.color = U.Hex("1F2937");
                    l.style.maxHeight = 90; l.style.overflow = Overflow.Hidden;
                }
            }
        }
    }

    /// <summary>One official picture with everything the list says about it: true facts and false traps.</summary>
    public class SignDetailPage : Page
    {
        readonly List<Stmt> _list;
        public SignDetailPage(List<Stmt> list) { _list = list; }

        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, Lang.T("signs"));
            var main = U.Row(root);
            main.style.flexGrow = 1; main.style.alignItems = Align.Stretch;
            var left = U.Col(main);
            left.style.width = 460; left.style.alignItems = Align.Center; left.style.flexShrink = 0;
            var f = U.Img(left, Listato.Figure(_list[0]), "figure");
            f.style.width = 420; f.style.height = 420;
            U.IB(left, "exam", Lang.T("practice_this"), () => App.I.Go(() => new StudyPage(_list, Lang.T("signs"))), "btn-primary").style.width = 420;

            var sc = U.Scroll(main);
            var c = sc.contentContainer;
            c.style.paddingLeft = 20; c.style.paddingRight = 30; c.style.paddingBottom = 30;
            Section(c, Lang.T("it_is_true"), _list.Where(s => s.True), "22C55E");
            Section(c, Lang.T("it_is_false"), _list.Where(s => !s.True), "EF4444");
        }

        static void Section(VisualElement c, string title, IEnumerable<Stmt> items, string color)
        {
            var h = U.T(c, title, "h2");
            h.style.color = U.Hex(color);
            h.style.marginTop = 16;
            foreach (var s in items)
            {
                var card = U.Col(c, "card");
                card.style.borderLeftColor = U.Hex(color); card.style.borderLeftWidth = 8;
                U.It(card, s.it, "ex-text", "bold");
                var tr = QuestionText.Translation(s, out _);
                if (tr != null) U.T(card, tr, "tr-text").style.fontSize = 28;
            }
        }
    }

    // ------------------------------------------------------------------ search
    public class SearchPage : Page
    {
        static string _q = "";
        VisualElement _res;

        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, Lang.T("search"));
            var tf = new TextField { value = _q };
            tf.style.fontSize = 34; tf.style.height = 84;
            tf.style.marginLeft = 60; tf.style.marginRight = 60; tf.style.flexShrink = 0;
            root.Add(tf);
            U.T(root, Lang.T("search_hint"), "small", "muted").style.marginLeft = 64;
            var sc = U.Scroll(root);
            _res = sc.contentContainer;
            _res.style.paddingLeft = 60; _res.style.paddingRight = 60; _res.style.paddingBottom = 40;
            tf.RegisterValueChangedCallback(e => { _q = e.newValue; Run(); });
            Run();
            tf.schedule.Execute(() => tf.Focus()).StartingIn(100);
        }

        void Run()
        {
            _res.Clear();
            var q = (_q ?? "").Trim().ToLowerInvariant();
            if (q.Length < 3) return;
            var hits = Listato.All.Where(s => s.it.ToLowerInvariant().Contains(q) ||
                                              (QuestionText.Translation(s, out _) ?? "").ToLowerInvariant().Contains(q)).Take(60).ToList();
            U.T(_res, Lang.T("results", hits.Count), "muted");
            for (int i = 0; i < hits.Count; i++)
            {
                int ii = i;
                var s = hits[i];
                var card = U.Raw(_res, "", () => App.I.Go(() => new StudyPage(hits, Lang.T("search"), ii)), "card");
                card.style.flexDirection = FlexDirection.Row; card.style.alignItems = Align.Center;
                var fig = Listato.Figure(s);
                if (fig != null) { var f = U.Img(card, fig, "figure"); f.style.width = 120; f.style.height = 120; f.style.flexShrink = 0; }
                var col = U.Col(card);
                col.style.flexShrink = 1; col.style.marginLeft = 16;
                U.It(col, s.it, "ex-text");
                U.It(col, (s.True ? "VERO" : "FALSO") + " · cap. " + s.c, "small", s.True ? "green" : "red");
            }
        }
    }

    // ------------------------------------------------------------------ exam history
    public class HistoryPage : Page
    {
        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, Lang.T("history"));
            var sc = U.Scroll(root);
            var c = sc.contentContainer;
            c.style.paddingLeft = 60; c.style.paddingRight = 60; c.style.paddingBottom = 40;
            if (ExamHistory.All.Count == 0) { U.T(c, Lang.T("nothing_here"), "h2").style.marginTop = 40; return; }
            foreach (var e in ExamHistory.All)
            {
                var entry = e;
                bool pass = e.errors <= ExamHistory.MaxOf(e);
                var row = U.Raw(c, "", () =>
                {
                    var sheet = entry.ids.Select(Listato.Get).Where(x => x != null).ToList();
                    var ans = entry.ans.Select(a => a < 0 ? (bool?)null : a == 1).ToArray();
                    App.I.Go(() => new ExamResultPage(sheet, ans, false, entry.seconds, ExamHistory.MaxOf(entry)));
                }, "card", pass ? "verdict-ok" : "verdict-ko");
                row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center;
                U.It(row, pass ? "IDONEO" : "NON IDONEO", "h2").style.flexGrow = 1;
                var when = DateTimeOffset.FromUnixTimeSeconds(e.time).LocalDateTime;
                U.It(row, when.ToString("dd/MM/yyyy HH:mm") + "  ·  " + Lang.T("errors_time", e.errors, (e.seconds / 60) + ":" + (e.seconds % 60).ToString("00")), "muted");
            }
        }
    }

    // ------------------------------------------------------------------ badges
    public class BadgesPage : Page
    {
        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, Lang.T("badges"));
            var sc = U.Scroll(root);
            var grid = U.Row(sc.contentContainer, "wrap");
            grid.style.justifyContent = Justify.Center;
            foreach (var b in Gamify.AllBadges)
            {
                bool has = Gamify.Badges.Contains(b);
                var card = U.Col(grid, "badge-card", has ? "badge-on" : "");
                U.Icon(card, has ? "premium" : "lock", 90, has ? U.Hex("FCD34D") : U.Hex("475569"));
                U.T(card, Lang.T("badge_" + b), "bold", "wraptext").style.unityTextAlign = TextAnchor.MiddleCenter;
                U.T(card, Lang.T("badge_" + b + "_d"), "small", "muted", "wraptext").style.unityTextAlign = TextAnchor.MiddleCenter;
            }
        }
    }
}
