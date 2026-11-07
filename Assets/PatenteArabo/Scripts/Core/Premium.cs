using System;
using System.Collections.Generic;
using UnityEngine;

namespace PatenteArabo
{
    /// <summary>
    /// In-app purchases (Google Play Billing) and the free-plan limits.
    /// Product ids must be created with the same names in Google Play Console.
    /// </summary>
    public static class Premium
    {
        // one-time purchases
        public const string Lifetime = "premium_lifetime";     // everything, forever
        public const string Pass3M = "pass_3_months";          // everything for 90 days (one-time, not renewing)
        // subscription
#if UNITY_IOS
        // App Store product ids are unique in the whole developer account: subscriptions get the app prefix there
        public const string Monthly = "pi_premium_monthly";
#else
        public const string Monthly = "premium_monthly";       // monthly with free trial (set the trial in Play Console)
#endif
        // future add-on packs (other official lists)
        public const string PackAM = "pack_am_a";              // licences AM / A1 / A2 / A
        public const string PackPro = "pack_superiori_cqc";    // C / D / CQC

        // single add-ons, cheaper than Premium, for people who need only one thing
        public const string AddDrive = "addon_drive";          // unlimited 3D driving mode
        public const string AddListen = "addon_listen";        // listening exam (questions read aloud)
        public const string AddExams = "addon_exams";          // unlimited exam simulations
        public const string AddGold = "addon_gold";            // gold profile in the challenge + streak shield

        public static readonly string[] AddOns = { AddExams, AddDrive, AddListen, AddGold };
        public static readonly string[] InApp = { Lifetime, Pass3M, AddDrive, AddListen, AddExams, AddGold, PackAM, PackPro };
        // professional licences (C/CE, CQC goods, CQC passengers): separate subscription, not included in the B premium
#if UNITY_IOS
        public const string ProMonthly = "pi_pro_monthly";
        public const string ProYearly = "pi_pro_yearly";
#else
        public const string ProMonthly = "pro_monthly";        // monthly with 3-day free trial
        public const string ProYearly = "pro_yearly";          // yearly
#endif
        public static readonly string[] Subs = { Monthly, ProMonthly, ProYearly };

        // free plan
        public const int FreeChapters = 3;          // chapters 1..3 fully open
        public const int FreePerChapter = 15;       // first statements of every other chapter
        public const int FreeExamsPerDay = 2;
        public const int FreeDriveQuestionsPerDay = 5;

        static readonly Dictionary<string, string> Prices = new Dictionary<string, string>();
        public static event Action Changed;
        static bool _inited;

        public static bool Has
        {
            get
            {
                if (PlayerPrefs.GetInt("pa_own_" + Lifetime, 0) == 1) return true;
                if (PlayerPrefs.GetInt("pa_own_" + Monthly, 0) == 1) return true;
                long.TryParse(PlayerPrefs.GetString("pa_pass_until", "0"), out long until);
                return DateTimeOffset.UtcNow.ToUnixTimeSeconds() < until;
            }
        }

        static bool Own(string id) => PlayerPrefs.GetInt("pa_own_" + id, 0) == 1;
        public static bool Owns(string id) => Own(id);
        /// <summary>The professional section (C/CE + CQC goods + CQC passengers).</summary>
        public static bool HasPro => Own(ProMonthly) || Own(ProYearly) || Own(PackPro);
        /// <summary>Everything of the course being studied is unlocked.</summary>
        public static bool Unlocked => Course.IsPro ? HasPro : Has;
        public static bool HasDrive => Has || Own(AddDrive);
        public static bool HasListen => Has || Own(AddListen);
        public static bool HasExams => Has || Own(AddExams);
        public static bool HasGold => Has || Own(AddGold);

        // ---------- reward: 7 days of Premium after the first 3-day streak (once per install)
        public const int TrialDays = 7;
        public static bool TrialJustEarned
        {
            get
            {
                if (PlayerPrefs.GetInt("pa_trial_shown", 0) == 1 || PlayerPrefs.GetInt("pa_trial_given", 0) == 0) return false;
                PlayerPrefs.SetInt("pa_trial_shown", 1); PlayerPrefs.Save();
                return true;
            }
        }
        public static void MaybeGiveStreakTrial(int streak)
        {
            if (streak < 3 || Has || PlayerPrefs.GetInt("pa_trial_given", 0) == 1) return;
            PlayerPrefs.SetInt("pa_trial_given", 1);
            PlayerPrefs.SetString("pa_pass_until", (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + TrialDays * 86400L).ToString());
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static string Price(string id, string fallback) => Prices.TryGetValue(id, out var p) && !string.IsNullOrEmpty(p) ? p : fallback;

        public static void Init()
        {
            if (_inited) return;
            _inited = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var billing = new AndroidJavaClass("com.memo.patente.Billing"))
                {
                    var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    // subscriptions are re-checked at every start: clear and let the restore set it again
                    billing.CallStatic("init", activity, string.Join(",", InApp), string.Join(",", Subs), new NativeCallback(OnEvent));
                }
            }
            catch (Exception e) { Debug.LogWarning("Billing: " + e.Message); }
#elif UNITY_IOS && !UNITY_EDITOR
            var all = new List<string>(InApp); all.AddRange(Subs);
            IosNative.StoreInit(string.Join(",", all), OnEvent);
#endif
        }

        static bool _restoring = true;
        static readonly HashSet<string> _seenOwned = new HashSet<string>();

        static void OnEvent(string ev)
        {
            if (string.IsNullOrEmpty(ev)) return;
            var parts = ev.Split('|');
            bool endsBuy = true;
            switch (parts[0])
            {
                case "price":
                    if (parts.Length >= 3) Prices[parts[1]] = parts[2];
                    endsBuy = false;
                    break;
                case "owned":
                {
                    long time = 0;
                    if (parts.Length >= 3) long.TryParse(parts[2], out time);
                    string token = parts.Length >= 4 ? parts[3] : "";
                    Grant(parts[1], !_restoring && _buying, time, token);
                    _seenOwned.Add(parts[1]);
                    break;
                }
                case "pending":
                    Toast.Show(Lang.T("buy_pending"));
                    break;
                case "ready":
                    // only when Play really answered: anything not returned is no longer owned (expired, cancelled or refunded)
                    if (parts.Length < 2 || parts[1] == "ok")
                    {
                        foreach (var id in InApp) if (id != Pass3M && !_seenOwned.Contains(id)) PlayerPrefs.DeleteKey("pa_own_" + id);
                        foreach (var id in Subs) if (!_seenOwned.Contains(id)) PlayerPrefs.DeleteKey("pa_own_" + id);
                        if (!_seenOwned.Contains(Pass3M) && PlayerPrefs.GetInt("pa_pass_paid", 0) == 1)
                        { PlayerPrefs.DeleteKey("pa_pass_paid"); PlayerPrefs.SetString("pa_pass_until", "0"); }
                    }
                    _restoring = false;
                    _seenOwned.Clear();
                    PlayerPrefs.Save();
                    endsBuy = false;
                    break;
                case "cancel":
                    break;
                case "error":
                    Debug.LogWarning("Billing: " + ev);
                    if (_buying) Toast.Show(Lang.T("buy_error"));
                    break;
            }
            if (endsBuy) _buying = false;
            Changed?.Invoke();
        }

        /// <summary>Unlocks a product. The 3-month pass lasts 90 days from the purchase time given by Google Play
        /// (never from "now", so restoring or reinstalling cannot extend it); once expired it is consumed so it can be bought again.</summary>
        static void Grant(string id, bool fresh, long purchaseMs, string token)
        {
            if (id == Pass3M)
            {
                long start = purchaseMs > 0 ? purchaseMs / 1000 : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long until = start + 90L * 86400;
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= until)
                {
#if UNITY_ANDROID && !UNITY_EDITOR
                    try { using (var billing = new AndroidJavaClass("com.memo.patente.Billing")) billing.CallStatic("consume", token); } catch { }
#endif
                    PlayerPrefs.DeleteKey("pa_pass_paid");
                }
                else
                {
                    long.TryParse(PlayerPrefs.GetString("pa_pass_until", "0"), out long cur);
                    if (until > cur) PlayerPrefs.SetString("pa_pass_until", until.ToString());
                    PlayerPrefs.SetInt("pa_pass_paid", 1);
                }
            }
            else PlayerPrefs.SetInt("pa_own_" + id, 1);
            PlayerPrefs.Save();
            if (fresh) { Sfx.Right(); Toast.Show(Lang.T("buy_thanks")); }
        }

        static bool _buying;

        public static void Buy(string id)
        {
            _buying = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var billing = new AndroidJavaClass("com.memo.patente.Billing"))
                    billing.CallStatic("buy", player.GetStatic<AndroidJavaObject>("currentActivity"), id);
            }
            catch (Exception e) { Debug.LogWarning(e.Message); }
#elif UNITY_IOS && !UNITY_EDITOR
            IosNative.StoreBuy(id);
#else
            // editor: simulate a successful purchase so the flow can be tested
            Grant(id, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), "");
            Changed?.Invoke();
#endif
        }

        public static void Restore()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using (var billing = new AndroidJavaClass("com.memo.patente.Billing")) billing.CallStatic("restore"); } catch { }
#elif UNITY_IOS && !UNITY_EDITOR
            IosNative.StoreRestore();
#endif
            Toast.Show(Lang.T("restored"));
        }

        public static void ManageSubscriptions() =>
#if UNITY_IOS
            Application.OpenURL("https://apps.apple.com/account/subscriptions");
#else
            Application.OpenURL("https://play.google.com/store/account/subscriptions?package=" + Application.identifier);
#endif

        // ---------- free-plan counters (per day) ----------
        static string Today => DateTime.Now.ToString("yyyyMMdd");
        static int Used(string what) => PlayerPrefs.GetString("pa_day_" + what, "") == Today ? PlayerPrefs.GetInt("pa_cnt_" + what, 0) : 0;
        static void Use(string what)
        {
            int n = Used(what) + 1;
            PlayerPrefs.SetString("pa_day_" + what, Today);
            PlayerPrefs.SetInt("pa_cnt_" + what, n);
            PlayerPrefs.Save();
        }

        public const int FreeProExamsPerDay = 1;
        public const int FreeProChaptersC = 2, FreeProChaptersCqc = 1;
        static string ExamKey => Course.IsPro ? "exam_" + Course.Id : "exam";
        public static bool CanStartExam => Course.IsPro ? (HasPro || Used(ExamKey) < FreeProExamsPerDay) : (HasExams || Used("exam") < FreeExamsPerDay);
        public static void ExamStarted() { if (Course.IsPro ? !HasPro : !HasExams) Use(ExamKey); }
        public static bool CanDriveQuestion => HasDrive || Used("drive") < FreeDriveQuestionsPerDay;
        public static void DriveQuestionAsked() { if (!HasDrive) Use("drive"); }
        public static bool ChapterOpen(int chapter)
        {
            if (Unlocked) return true;
            if (!Course.IsPro) return chapter <= FreeChapters;
            int free = Course.Id == Course.C ? FreeProChaptersC : FreeProChaptersCqc;
            var chs = Listato.Chapters;
            for (int i = 0; i < chs.Length && i < free; i++) if (chs[i].n == chapter) return true;
            return false;
        }

        static Dictionary<int, int> _rank;
        public static void ResetRank() { _rank = null; }
        /// <summary>Free plan: chapters 1..3 fully, plus the first FreePerChapter statements of every other chapter
        /// (same order as the chapter study list) — whichever way the statement is reached (random, search, games...).</summary>
        public static bool StmtOpen(Stmt s)
        {
            if (s == null || Unlocked || ChapterOpen(s.c)) return true;
            if (_rank == null)
            {
                _rank = new Dictionary<int, int>();
                foreach (var ch in Listato.Chapters)
                {
                    int i = 0;
                    foreach (var st in System.Linq.Enumerable.ThenBy(System.Linq.Enumerable.OrderBy(Listato.Chapter(ch.n), x => x.q), x => x.id))
                        _rank[st.id] = i++;
                }
            }
            return _rank.TryGetValue(s.id, out int r) && r < FreePerChapter;
        }
    }

    /// <summary>Daily study reminder and exam date.</summary>
    public static class Reminder
    {
        public static bool On => PlayerPrefs.GetInt("pa_rem_on", 0) == 1;
        public static int Hour => PlayerPrefs.GetInt("pa_rem_h", 19);

        public static void Set(bool on, int hour)
        {
            PlayerPrefs.SetInt("pa_rem_on", on ? 1 : 0);
            PlayerPrefs.SetInt("pa_rem_h", hour);
            PlayerPrefs.Save();
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (on && !UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS"))
                    UnityEngine.Android.Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS");
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var r = new AndroidJavaClass("com.memo.patente.ReminderReceiver"))
                {
                    var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    if (on) r.CallStatic("schedule", activity, hour, 0, Brand.Name, Lang.T("reminder_text"));
                    else r.CallStatic("cancel", activity);
                }
            }
            catch (Exception e) { Debug.LogWarning("Reminder: " + e.Message); }
#elif UNITY_IOS && !UNITY_EDITOR
            IosNative.Reminder(on, hour, Brand.Name, Lang.T("reminder_text"));
#endif
        }

        /// <summary>Exam date as yyyy-MM-dd ("" = not set).</summary>
        public static string ExamDate
        {
            get => PlayerPrefs.GetString("pa_exam_date", "");
            set { PlayerPrefs.SetString("pa_exam_date", value ?? ""); PlayerPrefs.Save(); }
        }

        public static int DaysToExam()
        {
            if (!DateTime.TryParse(ExamDate, out var d)) return -1;
            return (int)Math.Ceiling((d.Date - DateTime.Now.Date).TotalDays);
        }
    }

    /// <summary>
    /// Google Play in-app updates: when a new version is on Google Play, the update screen appears
    /// as soon as the app opens or comes back to the foreground (checked at most every 10 minutes).
    /// </summary>
    public static class AppUpdate
    {
        static float _last = -9999f;

        public static void Check()
        {
            if (Time.realtimeSinceStartup - _last < 600f) return;
            _last = Time.realtimeSinceStartup;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var u = new AndroidJavaClass("com.memo.patente.Updater"))
                    u.CallStatic("check", player.GetStatic<AndroidJavaObject>("currentActivity"), new NativeCallback(OnEvent));
            }
            catch (Exception e) { Debug.LogWarning("Update check: " + e.Message); }
#endif
        }

        static void OnEvent(string ev)
        {
            if (string.IsNullOrEmpty(ev)) return;
            if (ev.StartsWith("available")) Toast.Show(Lang.T("update_available"));
            else if (ev == "downloaded") Toast.Show(Lang.T("update_installing"));
        }
    }

    public static class NativeUtil
    {
        public static void Share(string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var n = new AndroidJavaClass("com.memo.patente.Native"))
                    n.CallStatic("share", player.GetStatic<AndroidJavaObject>("currentActivity"), text);
            }
            catch { }
#elif UNITY_IOS && !UNITY_EDITOR
            IosNative.Share(text);
#else
            GUIUtility.systemCopyBuffer = text;
            Toast.Show(text);
#endif
        }

        public static void OpenTtsSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var n = new AndroidJavaClass("com.memo.patente.Native"))
                    n.CallStatic("openTtsSettings", player.GetStatic<AndroidJavaObject>("currentActivity"));
            }
            catch { }
#elif UNITY_IOS && !UNITY_EDITOR
            IosNative.OpenSettings();
#endif
        }

#if UNITY_IOS
        // App Store id: filled in when the app is created in App Store Connect (until then the search link works)
        public const string AppStoreId = "6831966088";
        public static string StoreUrl => string.IsNullOrEmpty(AppStoreId) ? "https://apps.apple.com/search?term=Patente%20Italiana" : "https://apps.apple.com/app/id" + AppStoreId;
        public const string TermsUrl = "https://www.apple.com/legal/internet-services/itunes/dev/stdeula/";
#else
        public const string StoreUrl = "https://play.google.com/store/apps/details?id=com.memo.patentemondo";
        public const string TermsUrl = "https://memo32milano-ops.github.io/patente-italiana/privacy";
#endif
        public const string PrivacyUrl = "https://memo32milano-ops.github.io/patente-italiana/privacy.html"; // GitHub Pages (docs/privacy.html)
        public const string SupportMail = "memo32milano@gmail.com"; // public contact e-mail (same as Play listing)
    }
}
