using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PatenteArabo
{
    /// <summary>
    /// Store screenshots on the iOS simulator / Android emulator: the build script starts the app with
    /// PA_SHOT=home|study|exam|langs|chapters|glossary|progress (and optional PA_LANG, PA_COURSE) and the app opens that page.
    /// Without the variable (every real user) nothing happens.
    /// </summary>
    public static class ShotMode
    {
        public static bool Active { get; private set; }

        public static void Run()
        {
            string shot = Environment.GetEnvironmentVariable("PA_SHOT");
            if (string.IsNullOrEmpty(shot)) return;
            Active = true;
            PlayerPrefs.SetInt("pa_trial_shown", 1);
            Lang.Set(Environment.GetEnvironmentVariable("PA_LANG") ?? "it");
            var course = Environment.GetEnvironmentVariable("PA_COURSE");
            if (!string.IsNullOrEmpty(course)) Course.Id = course;
            switch (shot)
            {
                case "study": App.I.Go(() => new StudyPage(StudyPage.Source.Chapter, Listato.Chapters.Length > 4 ? Listato.Chapters[4].n : Listato.Chapters[0].n)); break;
                case "exam":
                    App.I.Go(() => { var p = new ExamPage(new ExamOptions { Translation = Lang.Code != "it", Name = "ROSSI MARIO", Mode = Course.DefaultMode }); p.ShotQuestions(); return p; });
                    break;
                case "langs": App.I.Go(() => new LanguagePage(false)); break;
                case "langs2":   // same list scrolled to the end (font check for the last scripts)
                    App.I.Go(() => new LanguagePage(false));
                    var doc = App.I.GetComponent<UIDocument>();
                    doc.rootVisualElement.schedule.Execute(() =>
                        doc.rootVisualElement.Query<ScrollView>().ForEach(sv => sv.scrollOffset = new Vector2(0, 100000))).StartingIn(2500);
                    break;
                case "chapters": App.I.Go(() => new ChaptersPage()); break;
                case "glossary": App.I.Go(() => new GlossaryPage()); break;
                case "progress": App.I.Go(() => new ProgressPage()); break;
                case "paywall": App.I.Go(() => new PaywallPage()); break;
                case "propaywall": App.I.Go(() => new ProPaywallPage()); break;
                default: App.I.GoHome(); break;
            }
        }
    }
}
