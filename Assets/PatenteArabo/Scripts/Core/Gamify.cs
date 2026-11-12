using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PatenteArabo
{
    /// <summary>Points (XP), levels, day streak, study time, daily goal and badges. Stored on the phone.</summary>
    public static class Gamify
    {
        [Serializable]
        class Store
        {
            public long xp;
            public long weekXp; public string weekId = "";
            public long studySec; public long weekStudySec;
            public int streak; public string lastDay = ""; public int bestStreak; public string shieldDay = "";
            public string goalDay = ""; public int goalDone;
            public int answered, correct;
            public List<string> badges = new List<string>();
        }

        static Store _s;
        static string PathFile => System.IO.Path.Combine(Application.persistentDataPath, "gamify.json");
        public static event Action<string> BadgeEarned;
        public static event Action Changed;

        public const int DailyGoal = 20;

        static void Ensure()
        {
            if (_s != null) return;
            try { _s = SafeFile.Read(PathFile) is string js ? JsonUtility.FromJson<Store>(js) : new Store(); } catch { SafeFile.KeepBad(PathFile); _s = new Store(); }
            if (_s == null) _s = new Store();
            RollWeek();
        }

        static string Today => DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        static string WeekId
        {
            get
            {
                var d = DateTime.UtcNow;
                var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
                return d.Year + "-W" + cal.GetWeekOfYear(d, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            }
        }

        static void RollWeek()
        {
            if (_s.weekId != WeekId) { _s.weekId = WeekId; _s.weekXp = 0; _s.weekStudySec = 0; }
        }

        static float _pendingSec;
        public static void Save()
        {
            Ensure();
            try { SafeFile.Write(PathFile, JsonUtility.ToJson(_s)); } catch { }
        }

        static void TouchDay()
        {
            string today = Today;
            if (_s.lastDay == today) return;
            double gap = DateTime.TryParseExact(_s.lastDay, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var last) ? (DateTime.Now.Date - last.Date).TotalDays : 99;
            if (gap <= 1.01) _s.streak++;
            else if (gap <= 2.01 && ShieldReady) { _s.streak++; _s.shieldDay = today; ShieldUsed?.Invoke(); }
            else _s.streak = 1;
            _s.lastDay = today;
            if (_s.streak > _s.bestStreak) _s.bestStreak = _s.streak;
            if (_s.streak >= 3) Award("streak3");
            Premium.MaybeGiveStreakTrial(_s.streak);
            if (_s.streak >= 7) Award("streak7");
            if (_s.streak >= 30) Award("streak30");
        }

        public static void AddXp(int amount)
        {
            Ensure(); RollWeek(); TouchDay();
            _s.xp += amount; _s.weekXp += amount;
            if (Level >= 5) Award("level5");
            if (Level >= 10) Award("level10");
            Save();
            Changed?.Invoke();
        }

        /// <summary>Called for every answered statement (study, exam, drive).</summary>
        public static void Answered(bool correct)
        {
            Ensure();
            if (_s.goalDay != Today) { _s.goalDay = Today; _s.goalDone = 0; }
            _s.goalDone++;
            _s.answered++;
            if (correct) _s.correct++;
            if (_s.goalDone == DailyGoal) { Award("goal"); _s.xp += 30; _s.weekXp += 30; }
            if (_s.answered >= 100) Award("q100");
            if (_s.answered >= 1000) Award("q1000");
            AddXp(correct ? 10 : 2);
        }

        public static void ExamFinished(int errors)
        {
            AddXp(errors <= 3 ? 60 : 20);
            if (errors <= 3) Award("exam_pass");
            if (errors == 0) Award("exam_perfect");
        }

        /// <summary>Counts real study time; call every frame from study/exam/drive pages.</summary>
        public static void StudyTick(float dt)
        {
            Ensure();
            _pendingSec += dt;
            if (_pendingSec < 10f) return;
            int add = Mathf.FloorToInt(_pendingSec);
            _pendingSec -= add;
            RollWeek();
            _s.studySec += add; _s.weekStudySec += add;
            if (_s.studySec >= 3600) Award("hour1");
            if (_s.studySec >= 36000) Award("hour10");
            Save();
        }

        static void Award(string id)
        {
            if (_s.badges.Contains(id)) return;
            _s.badges.Add(id);
            BadgeEarned?.Invoke(id);
        }

        public static long Xp { get { Ensure(); return _s.xp; } }
        public static long WeekXp { get { Ensure(); RollWeek(); return _s.weekXp; } }
        public static long StudySeconds { get { Ensure(); return _s.studySec; } }
        public static long WeekStudySeconds { get { Ensure(); RollWeek(); return _s.weekStudySec; } }
        public static int Streak
        {
            get
            {
                Ensure();
                if (!DateTime.TryParseExact(_s.lastDay, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var last)) return 0;
                double gap = (DateTime.Now.Date - last.Date).TotalDays;
                if (gap <= 1.01) return _s.streak;
                return gap <= 2.01 && ShieldReady ? _s.streak : 0;
            }
        }

        /// <summary>Streak shield (Gold add-on / Premium): one missed day per week does not break the streak.</summary>
        public static bool ShieldReady
        {
            get
            {
                if (!Premium.HasGold) return false;
                return !DateTime.TryParseExact(_s.shieldDay, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d) || (DateTime.Now.Date - d.Date).TotalDays >= 7;
            }
        }
        public static event Action ShieldUsed;
        public static int BestStreak { get { Ensure(); return _s.bestStreak; } }
        public static int GoalToday { get { Ensure(); return _s.goalDay == Today ? _s.goalDone : 0; } }
        public static int Answers { get { Ensure(); return _s.answered; } }
        public static float Accuracy { get { Ensure(); return _s.answered == 0 ? 0 : _s.correct / (float)_s.answered; } }
        public static IReadOnlyList<string> Badges { get { Ensure(); return _s.badges; } }

        // level n needs 100 * n * (n + 1) / 2 XP
        public static int Level
        {
            get
            {
                long xp = Xp; int n = 1;
                while (100L * n * (n + 1) / 2 <= xp) n++;
                return n;
            }
        }
        public static float LevelProgress
        {
            get
            {
                int n = Level;
                long a = 100L * (n - 1) * n / 2, b = 100L * n * (n + 1) / 2;
                return Mathf.Clamp01((Xp - a) / (float)(b - a));
            }
        }

        public static readonly string[] AllBadges = { "goal", "streak3", "streak7", "streak30", "q100", "q1000", "exam_pass", "exam_perfect", "hour1", "hour10", "level5", "level10" };

        public static void Reset()
        {
            _s = new Store();
            try { if (File.Exists(PathFile)) File.Delete(PathFile); } catch { }
        }

        public static string FormatTime(long sec)
        {
            long h = sec / 3600, m = sec % 3600 / 60;
            return h > 0 ? h + "h " + m.ToString("00") + "m" : m + "m";
        }
    }

    /// <summary>The learner's public profile (name + photo) used in the challenge.</summary>
    public static class Profile
    {
        public static string Name
        {
            get => PlayerPrefs.GetString("pa_profile_name", "");
            set { PlayerPrefs.SetString("pa_profile_name", (value ?? "").Trim()); PlayerPrefs.Save(); }
        }
        public static string Country
        {
            get => PlayerPrefs.GetString("pa_profile_country", "");
            set { PlayerPrefs.SetString("pa_profile_country", value ?? ""); PlayerPrefs.Save(); }
        }
        /// <summary>0..15 = drawn avatar, -1 = own photo.</summary>
        public static int Avatar
        {
            get => PlayerPrefs.GetInt("pa_profile_avatar", 0);
            set { PlayerPrefs.SetInt("pa_profile_avatar", value); PlayerPrefs.Save(); }
        }
        public static bool InChallenge
        {
            get => PlayerPrefs.GetInt("pa_challenge_on", 0) == 1;
            set { PlayerPrefs.SetInt("pa_challenge_on", value ? 1 : 0); PlayerPrefs.Save(); }
        }
        public static bool AcceptedRules
        {
            get => PlayerPrefs.GetInt("pa_challenge_rules", 0) == 1;
            set { PlayerPrefs.SetInt("pa_challenge_rules", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        static string PhotoPath => System.IO.Path.Combine(Application.persistentDataPath, "avatar.jpg");
        static Texture2D _photo;

        public static Texture2D Photo
        {
            get
            {
                if (_photo != null) return _photo;
                if (!File.Exists(PhotoPath)) return null;
                _photo = new Texture2D(2, 2);
                _photo.LoadImage(File.ReadAllBytes(PhotoPath));
                return _photo;
            }
        }

        /// <summary>Small JPEG (96 px) of the photo, base64, to share in the challenge.</summary>
        public static string PhotoBase64()
        {
            try { return File.Exists(PhotoPath) ? Convert.ToBase64String(File.ReadAllBytes(PhotoPath)) : ""; } catch { return ""; }
        }

        public static void SetPhoto(byte[] jpeg)
        {
            File.WriteAllBytes(PhotoPath, jpeg);
            _photo = null;
            Avatar = -1;
        }

        public static void DeletePhoto()
        {
            try { if (File.Exists(PhotoPath)) File.Delete(PhotoPath); } catch { }
            _photo = null;
            if (Avatar < 0) Avatar = 0;
        }

        public static Texture2D AvatarTexture(int index, string photoB64 = null)
        {
            if (index < 0)
            {
                if (photoB64 == null) return Photo;
                if (photoB64.Length == 0) return Resources.Load<Texture2D>("PatenteArabo/Avatars/a0");
                try { var t = new Texture2D(2, 2); t.LoadImage(Convert.FromBase64String(photoB64)); return t; }
                catch { return Resources.Load<Texture2D>("PatenteArabo/Avatars/a0"); }
            }
            return Resources.Load<Texture2D>("PatenteArabo/Avatars/a" + Mathf.Clamp(index, 0, 15));
        }

        /// <summary>Opens the phone gallery; done gets a 96x96 JPEG or null.</summary>
        public static void PickPhoto(Action<byte[]> done)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var p = new AndroidJavaClass("com.memo.patente.PhotoPicker"))
                    p.CallStatic("pick", player.GetStatic<AndroidJavaObject>("currentActivity"), new NativeCallback(path =>
                    {
                        if (path == "cancel") { done?.Invoke(null); return; }
                        if (string.IsNullOrEmpty(path) || path == "error" || !File.Exists(path)) { Toast.Show(Lang.T("photo_error")); done?.Invoke(null); return; }
                        done?.Invoke(File.ReadAllBytes(path));
                    }));
            }
            catch (Exception e) { Debug.LogWarning(e.Message); Toast.Show(Lang.T("photo_error")); done?.Invoke(null); }
#elif UNITY_IOS && !UNITY_EDITOR
            IosNative.PickPhoto(path =>
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) { done?.Invoke(null); return; }
                done?.Invoke(File.ReadAllBytes(path));
            });
#else
            // editor: make a coloured test photo
            var t = new Texture2D(96, 96);
            var c = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
            for (int y = 0; y < 96; y++) for (int x = 0; x < 96; x++) t.SetPixel(x, y, Color.Lerp(c, Color.white, y / 96f));
            t.Apply();
            done?.Invoke(t.EncodeToJPG(70));
#endif
        }
    }
}
