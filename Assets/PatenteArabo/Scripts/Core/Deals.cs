using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PatenteArabo
{
    /// <summary>
    /// Link to the Arabic deals channel on Telegram (t.me/ArabOfferteItalia), shown only to users whose
    /// interface is Arabic. No push notifications from the app (Google Play forbids ads in system
    /// notifications): the user opens the channel by choice, and Telegram itself notifies subscribers.
    /// A slim banner on the home screen, plus a one-time intro card (never on a day the paid-plan card showed).
    /// </summary>
    public static class Deals
    {
        public const string ChannelUrl = "https://t.me/ArabOfferteItalia";
        static string Today => DateTime.Now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

        public static bool Eligible => Lang.Code == "ar" && !ShotMode.Active;

        static void Open()
        {
            PlayerPrefs.SetInt("pa_deals_opened", 1); PlayerPrefs.Save();
            Application.OpenURL(ChannelUrl);
        }

        /// <summary>Slim banner on the home screen.</summary>
        public static void Banner(VisualElement parent)
        {
            if (!Eligible) return;
            var b = U.Row(parent, "promo-banner");
            U.Icon(b, "star", 48, U.Hex("F97316"));
            var col = U.Col(b, "status-col");
            U.T(col, "🔥 عروض أمازون بالعربي", "status-title");
            U.T(col, "خصومات حقيقية كل يوم للعرب في إيطاليا، على قناة تليجرام مجانية", "status-sub");
            var go = U.B(b, "افتح القناة", Open, "btn-gold", "btn-small");
            go.style.flexShrink = 0;
            b.RegisterCallback<ClickEvent>(_ => Open());
        }

        /// <summary>One-time intro card for Arabic users who already used the app a bit.</summary>
        public static void MaybeCard(VisualElement root)
        {
            if (!Eligible) return;
            if (PlayerPrefs.GetInt("pa_deals_card", 0) == 1 || PlayerPrefs.GetInt("pa_deals_opened", 0) == 1) return;
            if (Gamify.Xp < 30) return;
            if (PlayerPrefs.GetString("pa_promo_day", "") == Today) return;   // never two cards on the same day
            PlayerPrefs.SetInt("pa_deals_card", 1); PlayerPrefs.Save();
            root.schedule.Execute(() =>
            {
                if (App.I == null) return;
                App.I.Dialog("🔥 عروض أمازون بالعربي",
                    "عملنا قناة تليجرام مجانية بتنزل كل يوم أقوى الخصومات الحقيقية من أمازون إيطاليا: أدوات العمل، البيت والمطبخ، الموبايلات ومستلزمات الأطفال.\n\nانضم للقناة وفعّل الإشعارات عشان العروض بتخلص بسرعة.",
                    ("مش دلوقتي", null, "btn-ghost"),
                    ("افتح القناة", Open, "btn-gold"));
            }).StartingIn(1200);
        }
    }
}
