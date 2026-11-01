using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PatenteArabo
{
    /// <summary>Glossy "3D" card with icon, shine sweep and press tilt.</summary>
    public static class GlossCard
    {
        public static VisualElement Make(VisualElement parent, string skin, string icon, string title, string sub, Action click, float w = 560, float h = 400, string badge = null)
        {
            var host = new VisualElement();
            host.AddToClassList("gcard");
            host.style.width = w; host.style.height = h;
            parent.Add(host);
            var bgTex = Resources.Load<Texture2D>("PatenteArabo/UI/Cards/" + skin);
            if (bgTex != null) host.style.backgroundImage = new StyleBackground(bgTex);

            var inner = U.El(host, "gcard-inner");
            inner.pickingMode = PickingMode.Ignore;

            // big faded watermark icon
            var wm = U.Icon(inner, icon, h * 0.62f, new Color(1, 1, 1, 0.13f));
            wm.AddToClassList("gcard-watermark");
            wm.style.right = Lang.Rtl ? StyleKeyword.Auto : (StyleLength)(-h * 0.06f);
            wm.style.left = Lang.Rtl ? (StyleLength)(-h * 0.06f) : StyleKeyword.Auto;

            // 3D icon: dark shadow copy + white icon in a glass bubble
            var bubble = U.El(inner, "gcard-bubble");
            var shadow = U.Icon(bubble, icon, 76, new Color(0, 0, 0, 0.35f));
            shadow.style.position = Position.Absolute; shadow.style.left = 26; shadow.style.top = 30;
            var ic = U.Icon(bubble, icon, 76);
            ic.style.position = Position.Absolute; ic.style.left = 22; ic.style.top = 24;

            var col = U.Col(inner);
            col.style.marginTop = StyleKeyword.Auto;
            col.pickingMode = PickingMode.Ignore;
            var t = U.T(col, title, "gcard-title"); t.pickingMode = PickingMode.Ignore;
            if (!string.IsNullOrEmpty(sub)) { var st = U.T(col, sub, "gcard-sub"); st.pickingMode = PickingMode.Ignore; }

            if (!string.IsNullOrEmpty(badge))
            {
                var bd = new Label(badge) { pickingMode = PickingMode.Ignore };
                bd.AddToClassList("gcard-badge");
                bd.style.position = Position.Absolute;
                if (Lang.Rtl) bd.style.left = 30; else bd.style.right = 30;
                bd.style.top = 30;
                inner.Add(bd);
            }

            // shine sweep
            var clip = U.El(host, "gcard-clip"); clip.pickingMode = PickingMode.Ignore;
            var shine = U.El(clip, "gcard-shine"); shine.pickingMode = PickingMode.Ignore;
            shine.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>("PatenteArabo/UI/Cards/shine"));
            float delay = UnityEngine.Random.Range(0f, 3000f);
            shine.schedule.Execute(() =>
            {
                shine.RemoveFromClassList("gcard-shine-go");
                shine.schedule.Execute(() => shine.AddToClassList("gcard-shine-go")).StartingIn(30);
            }).StartingIn((long)delay).Every(5200);

            host.RegisterCallback<PointerDownEvent>(_ => host.AddToClassList("gcard-down"));
            host.RegisterCallback<PointerLeaveEvent>(_ => host.RemoveFromClassList("gcard-down"));
            host.RegisterCallback<PointerUpEvent>(_ =>
            {
                if (!host.ClassListContains("gcard-down")) return;
                host.RemoveFromClassList("gcard-down");
                Sfx.Click();
                click?.Invoke();
            });
            return host;
        }
    }

    public class HomePage : Page
    {
        public override void OnHide() { Gamify.BadgeEarned -= OnBadge; Gamify.ShieldUsed -= OnShield; }
        void OnShield() => Toast.Show(Lang.T("shield_used"));
        void OnBadge(string id) => Toast.Show(Lang.T("badge_new") + " " + Lang.T("badge_" + id));

        public override void Build(VisualElement root)
        {
            Gamify.BadgeEarned -= OnBadge;
            Gamify.BadgeEarned += OnBadge;
            Gamify.ShieldUsed -= OnShield;
            Gamify.ShieldUsed += OnShield;
            if (Premium.TrialJustEarned)
                root.schedule.Execute(() => App.I.Dialog(Lang.T("trial_title"), Lang.T("trial_body", Premium.TrialDays),
                    (Lang.T("great"), null, "btn-gold"))).StartingIn(500);
            root.AddToClassList("home-bg");

            // ---------- header: profile, level, streak, goal
            var top = U.Row(root, "home-head");
            var av = U.Raw(top, "", () => App.I.Go(() => new ProfilePage()), "avatar-btn");
            av.style.backgroundImage = new StyleBackground(Profile.AvatarTexture(Profile.Avatar));
            if (Premium.HasGold) av.AddToClassList("gold-frame");
            var who = U.Col(top);
            who.style.flexGrow = 1; who.style.flexShrink = 1;
            who.style.marginLeft = 22; who.style.marginRight = 22;
            var name = string.IsNullOrEmpty(Profile.Name) ? Lang.T("hello") : Lang.T("hello_name", Profile.Name);
            U.T(who, name, "home-hello");
            var lvRow = U.Row(who);
            U.T(lvRow, Lang.T("level", Gamify.Level), "lv-chip");
            var bar = U.El(lvRow, "xp-bar");
            var fill = U.El(bar, "xp-fill");
            fill.style.width = Length.Percent(Gamify.LevelProgress * 100);
            U.It(lvRow, Gamify.Xp + " XP", "small", "muted").style.marginLeft = 12;

            var chips = U.Row(top);
            Chip(chips, "bell", Gamify.Streak.ToString(), Lang.T("streak_days"), "F97316");
            Chip(chips, "check", Gamify.GoalToday + "/" + Gamify.DailyGoal, Lang.T("goal_today"), "22C55E");
            if (!Premium.Has)
            {
                var pb = U.IB(top, "premium", Lang.T("go_premium"), () => App.I.Go(() => new PaywallPage()), "btn-gold", "btn-small", "glow");
                pb.style.flexShrink = 0;
            }
            Chrome.IconButton(top, "language", () => App.I.Go(() => new LanguagePage(false)));
            Chrome.IconButton(top, "settings", () => App.I.Go(() => new SettingsPage()));

            var sc = U.Scroll(root);
            var c = sc.contentContainer;

            var brand = U.Row(c, "brand-strip");
            U.It(brand, Brand.Name, "brand-name");
            if (Lang.Code != "it") U.T(brand, Lang.T("app_sub"), "brand-sub");
            else U.It(brand, Lang.T("app_sub"), "brand-sub");

            // ---------- readiness verdict + study plan
            float ready = Srs.OverallReadiness();
            string verdictKey = ready >= 0.8f ? "ready_yes" : ready >= 0.5f ? "ready_almost" : "ready_not";
            var banner = U.Row(c, "exam-banner");
            U.Icon(banner, ready >= 0.8f ? "check" : "progress", 56, U.Hex(ready >= 0.8f ? "6EDC96" : ready >= 0.5f ? "FCD34D" : "FF8282"));
            var bcol = U.Col(banner);
            bcol.style.marginLeft = 18; bcol.style.marginRight = 18; bcol.style.flexShrink = 1;
            U.T(bcol, Lang.T(verdictKey) + "  ·  " + Lang.T("readiness") + " " + Mathf.RoundToInt(ready * 100) + "%", "h2").style.marginBottom = 0;
            int days = Reminder.DaysToExam();
            if (days > 0)
            {
                int left = Listato.All.Count(st => Srs.Box(st.id) < 3);
                U.T(bcol, Lang.T("days_to_exam", days) + "  ·  " + Lang.T("plan_per_day", Mathf.CeilToInt(left / (float)days)), "small", "muted");
            }
            else U.T(bcol, Lang.T("set_exam_date_hint"), "small", "muted");
            banner.RegisterCallback<ClickEvent>(_ => App.I.Go(() => new ProgressPage()));

            // ---------- daily quiz hero
            var daily = U.Row(c, "daily-hero", QuickQuizPage.DailyDone ? "daily-done" : "");
            U.Icon(daily, QuickQuizPage.DailyDone ? "check" : "star", 70, U.Hex(QuickQuizPage.DailyDone ? "6EDC96" : "F2C94C"));
            var dcol = U.Col(daily);
            dcol.style.flexGrow = 1; dcol.style.flexShrink = 1; dcol.style.marginLeft = 20; dcol.style.marginRight = 20;
            U.T(dcol, Lang.T("daily_quiz"), "gcard-title");
            U.T(dcol, QuickQuizPage.DailyDone ? Lang.T("daily_done", QuickQuizPage.DailyStreak) : Lang.T("daily_sub"), "small");
            if (!QuickQuizPage.DailyDone)
            {
                var go = U.IB(daily, "play", Lang.T("play"), () => App.I.Go(() => new QuickQuizPage(QuickQuizPage.Mode.Daily)), "btn-gold", "glow");
                go.style.flexShrink = 0;
            }

            var grid = U.Row(c, "wrap");
            grid.style.justifyContent = Justify.Center;
            int due = Srs.DueList().Count;
            GlossCard.Make(grid, "study", "study", Lang.T("study"), Lang.T("study_sub"), () => App.I.Go(() => new ChaptersPage()));
            GlossCard.Make(grid, "exam", "exam", Lang.T("exam"), Lang.T("exam_sub"), () => ExamIntroPage.Open(false));
            GlossCard.Make(grid, "drive", "drive", Lang.T("drive"), Lang.T("drive_sub"), () => App.I.Go(() => new DrivePage()), badge: "3D");
            GlossCard.Make(grid, "challenge", "star", Lang.T("challenge"), Lang.T("challenge_sub"), () => App.I.Go(() => new LeaderboardPage()), badge: "TOP");
            GlossCard.Make(grid, "signs", "image", Lang.T("signs"), Lang.T("signs_sub"), () => App.I.Go(() => new SignsPage()));
            GlossCard.Make(grid, "review", "review", Lang.T("review"), due > 0 ? Lang.T("review_due", due) : Lang.T("review_sub"),
                () => App.I.Go(() => new StudyPage(StudyPage.Source.Review, 0)), badge: due > 0 ? due.ToString() : null);
            GlossCard.Make(grid, "progress", "progress", Lang.T("progress"), Lang.T("progress_sub", Mathf.RoundToInt(Srs.OverallReadiness() * 100)),
                () => App.I.Go(() => new ProgressPage()));
            GlossCard.Make(grid, "listen", "listen", Lang.T("listen"), Lang.T("listen_sub"), () => ExamIntroPage.Open(true));
            GlossCard.Make(grid, "exam", "clock", Lang.T("time_attack"), Lang.T("time_attack_sub", QuickQuizPage.BestTimeAttack),
                () => App.I.Go(() => new QuickQuizPage(QuickQuizPage.Mode.TimeAttack)), badge: "60s");
            GlossCard.Make(grid, "review", "warning", Lang.T("traps_mode"), Lang.T("traps_sub"),
                () => App.I.Go(() => new QuickQuizPage(QuickQuizPage.Mode.Traps)));
            GlossCard.Make(grid, "profile", "info", Lang.T("more"), Lang.T("more_sub"), () => App.I.Go(() => new MorePage()));

            var disc = U.T(c, Lang.T("disclaimer_short"), "small", "muted", "wraptext");
            disc.style.marginLeft = 60; disc.style.marginRight = 60; disc.style.marginTop = 10; disc.style.marginBottom = 30;
            disc.RegisterCallback<ClickEvent>(_ => App.I.Go(() => new InfoPage()));
        }

        static void Chip(VisualElement parent, string icon, string big, string small, string color)
        {
            var ch = U.Row(parent, "stat-chip");
            U.Icon(ch, icon, 40, U.Hex(color));
            var col = U.Col(ch);
            col.style.marginLeft = 10; col.style.marginRight = 10;
            U.It(col, big, "stat-big");
            U.T(col, small, "stat-small");
        }
    }

    /// <summary>Extra tools: search, saved questions, exam history, badges.</summary>
    public class MorePage : Page
    {
        public override void Build(VisualElement root)
        {
            Chrome.TopBar(root, Lang.T("more"));
            var sc = U.Scroll(root);
            var grid = U.Row(sc.contentContainer, "wrap");
            grid.style.justifyContent = Justify.Center;
            GlossCard.Make(grid, "study", "eye", Lang.T("search"), Lang.T("search_sub"), () => App.I.Go(() => new SearchPage()), 440, 300);
            GlossCard.Make(grid, "challenge", "star", Lang.T("bookmarks"), Lang.T("bookmarks_sub", Bookmarks.Count), () => App.I.Go(() => new StudyPage(StudyPage.Source.Bookmarks, 0)), 440, 300);
            GlossCard.Make(grid, "exam", "clock", Lang.T("history"), Lang.T("history_sub", ExamHistory.All.Count), () => App.I.Go(() => new HistoryPage()), 440, 300);
            GlossCard.Make(grid, "premium", "premium", Lang.T("badges"), Lang.T("badges_sub", Gamify.Badges.Count, Gamify.AllBadges.Length), () => App.I.Go(() => new BadgesPage()), 440, 300);
            GlossCard.Make(grid, "profile", "info", Lang.T("profile"), Lang.T("profile_sub"), () => App.I.Go(() => new ProfilePage()), 440, 300);
            GlossCard.Make(grid, "signs", "calendar", Lang.T("exam_day"), Lang.T("exam_day_sub"), () => App.I.Go(() => new ExamDayPage()), 440, 300);
            GlossCard.Make(grid, "review", "warning", Lang.T("safety"), Lang.T("safety_sub"), () => App.I.Go(() => new SafetyPage()), 440, 300);
            GlossCard.Make(grid, "listen", "language", Lang.T("my_words"), Lang.T("my_words_sub", Vocab.Count), () => App.I.Go(() => new VocabPage()), 440, 300);
            GlossCard.Make(grid, "drive", "settings", Lang.T("settings"), "", () => App.I.Go(() => new SettingsPage()), 440, 300);
        }
    }
}
