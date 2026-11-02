using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PatenteArabo
{
    /// <summary>One official exam simulation (numbers from the MIT rules).</summary>
    public class ExamMode
    {
        public string Key;          // i18n key of the mode name
        public int Questions;       // statements in the sheet
        public int Minutes;         // time allowed
        public int MaxErrors;       // errors allowed (one more = NON IDONEO)
        public string Badge;        // "PATENTE B", "PATENTE C", "CQC MERCI"...
        public string Abil;         // "B", "C", "CQC merci"...
        public Func<ChapterInfo, int> Quota;   // statements per chapter (null = chapter.exam)
    }

    /// <summary>
    /// Which licence the learner is studying: B (default), C/CE, CQC goods or CQC passengers.
    /// Every store of progress, every data file and every exam rule depends on it.
    /// Sources: circ. MIT prot. 28825 del 19/09/2019 (C: 40 quesiti, 40 minuti, max 4 errori; C per titolari di C1: 20/20/2);
    /// DM MIT 5 luglio 2019 art. 11 (CQC: 70 quesiti, 90 minuti, max 7 errori = 40 parte comune + 30 specialistica;
    /// estensione 30/40/3; titolari attestato di idoneità professionale 40/50/4).
    /// </summary>
    public static class Course
    {
        public const string B = "b", C = "c", CqcM = "cqcm", CqcP = "cqcp";
        public static readonly string[] All = { B, C, CqcM, CqcP };

        static string _id;
        public static event Action Changed;

        public static string Id
        {
            get
            {
                if (_id == null)
                {
                    _id = PlayerPrefs.GetString("pa_course", B);
                    if (Array.IndexOf(All, _id) < 0) _id = B;
                }
                return _id;
            }
            set
            {
                if (Array.IndexOf(All, value) < 0 || value == Id) return;
                _id = value;
                PlayerPrefs.SetString("pa_course", value);
                PlayerPrefs.Save();
                Listato.Reset();
                QuestionText.Reset();
                Srs.Reset();
                Bookmarks.Reload();
                ExamHistory.Reset();
                Premium.ResetRank();
                Changed?.Invoke();
            }
        }

        public static bool IsB => Id == B;
        public static bool IsPro => Id != B;
        public static bool IsCqc => Id == CqcM || Id == CqcP;

        /// <summary>Data file in Resources/PatenteArabo/data.</summary>
        public static string DataName => Id == B ? "listato_b" : Id == C ? "listato_c" : "listato_cqc";
        /// <summary>Suffix of the text files (q_ar_c.json, ex_it_cqc.json...). B keeps the original names.</summary>
        public static string TextSuffix => Id == B ? "" : Id == C ? "_c" : "_cqc";
        /// <summary>Suffix of the files and prefs that store progress. CQC goods and passengers share the common part.</summary>
        public static string StoreSuffix => TextSuffix;
        /// <summary>Prefix of the keys in the machine-translation cache (statement ids repeat between lists).</summary>
        public static string MtPrefix => Id == B ? "" : TextSuffix.TrimStart('_') + ":";
        public static string FigureFile => Id == B ? "figures" : "figures_sup";

        /// <summary>Chapters of the current list that belong to this course (CQC: common part + chosen speciality).</summary>
        public static bool Visible(ChapterInfo ch)
        {
            if (Id == CqcM) return ch.sec == 0 || ch.sec == 1;
            if (Id == CqcP) return ch.sec == 0 || ch.sec == 2;
            return true;
        }

        public static string NameKey(string id) => id == B ? "course_b" : id == C ? "course_c" : id == CqcM ? "course_cqcm" : "course_cqcp";
        public static string Name => Lang.T(NameKey(Id));
        public static string Short(string id) => id == B ? "B" : id == C ? "C/CE" : id == CqcM ? "CQC Merci" : "CQC Persone";

        // ------------------------------------------------------------------ exam modes
        public static List<ExamMode> Modes
        {
            get
            {
                switch (Id)
                {
                    case C:
                        return new List<ExamMode>
                        {
                            new ExamMode { Key = "mode_c_full", Questions = 40, Minutes = 40, MaxErrors = 4, Badge = "PATENTE C", Abil = "C" },
                            new ExamMode { Key = "mode_c_from_c1", Questions = 20, Minutes = 20, MaxErrors = 2, Badge = "PATENTE C", Abil = "C (da C1)",
                                Quota = ScaledQuota(20, ch => ch.c1 == 1) },
                        };
                    case CqcM:
                    case CqcP:
                        string sp = Id == CqcM ? "MERCI" : "PERSONE";
                        return new List<ExamMode>
                        {
                            new ExamMode { Key = "mode_cqc_full", Questions = 70, Minutes = 90, MaxErrors = 7, Badge = "CQC " + sp, Abil = "CQC " + sp.ToLowerInvariant(),
                                Quota = ch => ch.sec == 0 ? 4 : 10 },
                            new ExamMode { Key = "mode_cqc_ext", Questions = 30, Minutes = 40, MaxErrors = 3, Badge = "CQC " + sp, Abil = "CQC " + sp.ToLowerInvariant(),
                                Quota = ch => ch.sec == 0 ? 0 : 10 },
                            new ExamMode { Key = "mode_cqc_att", Questions = 40, Minutes = 50, MaxErrors = 4, Badge = "CQC " + sp, Abil = "CQC " + sp.ToLowerInvariant(),
                                Quota = ch => ch.sec == 0 ? 4 : 0 },
                        };
                    default:
                        return new List<ExamMode> { new ExamMode { Key = "mode_b", Questions = 30, Minutes = 20, MaxErrors = 3, Badge = "PATENTE B", Abil = "B" } };
                }
            }
        }

        public static ExamMode DefaultMode => Modes[0];

        /// <summary>Spreads n statements over the chapters that pass the filter, proportionally to their size (largest remainder, min 1).</summary>
        static Func<ChapterInfo, int> ScaledQuota(int n, Func<ChapterInfo, bool> filter)
        {
            Dictionary<int, int> cache = null;
            return ch =>
            {
                if (cache == null)
                {
                    cache = new Dictionary<int, int>();
                    var chs = Listato.Chapters.Where(filter).ToList();
                    float tot = chs.Sum(c => (float)c.count);
                    var raw = chs.ToDictionary(c => c.n, c => n * c.count / Mathf.Max(1f, tot));
                    foreach (var c in chs) cache[c.n] = Mathf.Max(1, Mathf.FloorToInt(raw[c.n]));
                    while (cache.Values.Sum() < n) { var k = raw.OrderByDescending(kv => kv.Value - cache[kv.Key]).First().Key; cache[k]++; }
                    while (cache.Values.Sum() > n) { var k = raw.Where(kv => cache[kv.Key] > 1).OrderByDescending(kv => cache[kv.Key] - kv.Value).First().Key; cache[k]--; }
                }
                return cache.TryGetValue(ch.n, out int q) ? q : 0;
            };
        }
    }
}
