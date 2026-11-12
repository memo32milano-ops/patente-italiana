using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PatenteArabo
{
    /// <summary>
    /// Friendly in-app reminders of the paid plans for free users (no ads, no fake urgency):
    /// a slim banner on the home screen, and at most once a day (from the second day of use) a short card
    /// that shows the learner's own progress and what the plan adds. Never during an exam or a drive.
    /// </summary>
    public static class Promo
    {
        static string Today => DateTime.Now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        static bool _shownThisRun;

        /// <summary>Free user of the current course.</summary>
        public static bool Eligible => !Premium.Unlocked && !ShotMode.Active;

        static string MonthlyId => Course.IsPro ? Premium.ProMonthly : Premium.Monthly;

        /// <summary>Slim banner under the status tiles on the home screen.</summary>
        public static void Banner(VisualElement parent)
        {
            if (!Eligible) return;
            var b = U.Row(parent, "promo-banner");
            U.Icon(b, "premium", 48, U.Hex("FCD34D"));
            var col = U.Col(b, "status-col");
            U.T(col, Lang.T(Course.IsPro ? "promo_title_pro" : "promo_title"), "status-title");
            U.T(col, Premium.TrialFor(MonthlyId) ? Lang.T("promo_trial") : Lang.T(Course.IsPro ? "promo_sub_pro" : "promo_sub"), "status-sub");
            var go = U.B(b, Lang.T("see_plans"), () => ProPaywallPage.OpenForCourse(), "btn-gold", "btn-small");
            go.style.flexShrink = 0;
            b.RegisterCallback<ClickEvent>(_ => ProPaywallPage.OpenForCourse());
        }

        /// <summary>Once a day, not on the first day, not twice in one session.</summary>
        public static void MaybeCard(VisualElement root)
        {
            if (!Eligible || _shownThisRun) return;
            if (Gamify.Xp < 30) return;                                       // let new learners try the app first
            if (PlayerPrefs.GetString("pa_promo_day", "") == Today) return;
            string first = PlayerPrefs.GetString("pa_first_day", "");
            if (first == "") { PlayerPrefs.SetString("pa_first_day", Today); PlayerPrefs.Save(); return; }
            if (first == Today) return;
            _shownThisRun = true;
            PlayerPrefs.SetString("pa_promo_day", Today); PlayerPrefs.Save();
            int ready = Mathf.RoundToInt(Srs.OverallReadiness() * 100);
            root.schedule.Execute(() =>
            {
                if (App.I == null) return;
                App.I.Dialog(Lang.T("promo_card_title"),
                    Lang.T(Course.IsPro ? "promo_card_body_pro" : "promo_card_body", ready) +
                    (Premium.TrialFor(MonthlyId) ? "\n\n" + Lang.T("promo_trial") : ""),
                    (Lang.T("not_now"), null, "btn-ghost"),
                    (Lang.T("see_plans"), () => ProPaywallPage.OpenForCourse(), "btn-gold"));
            }).StartingIn(900);
        }
    }
}
