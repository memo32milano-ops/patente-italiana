using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PatenteArabo
{
    [Serializable] public class ChapterInfo { public int n; public string it; public int count; public int exam; public int sec; public int c1; }

    [Serializable]
    public class Stmt
    {
        public int id;      // official statement number
        public int q;       // official "quesito" number (group)
        public int c;       // chapter 1..25
        public string it;   // Italian text, as in the ministerial list
        public int v;       // 1 = VERO, 0 = FALSO
        public string f;    // figure id ("" = none)
        public string[] t;  // trap words found in the text
        public bool True => v == 1;
    }

    [Serializable] class ListatoFile { public string version; public ChapterInfo[] chapters; public Stmt[] stmts; }
    [Serializable] class TextFile { public int[] ids; public string[] tx; }

    /// <summary>The official question list of the current course (B, C/CE or CQC) published by the Ministero delle Infrastrutture e dei Trasporti.</summary>
    public static class Listato
    {
        static ListatoFile _file;
        static string _loaded;
        static ChapterInfo[] _chapters;
        static Stmt[] _all;
        static Dictionary<int, Stmt> _byId;
        static Dictionary<int, List<Stmt>> _byChapter;

        public static void Reset() { _chapters = null; _all = null; _loaded = null; _file = null; }

        static void Ensure()
        {
            if (_chapters != null) return;
            string name = Course.DataName;
            if (_file == null || _loaded != name)
            {
                var ta = Resources.Load<TextAsset>("PatenteArabo/data/" + name);
                _file = ta != null ? JsonUtility.FromJson<ListatoFile>(ta.text) : new ListatoFile { chapters = new ChapterInfo[0], stmts = new Stmt[0] };
                _loaded = name;
            }
            _chapters = _file.chapters.Where(Course.Visible).ToArray();
            var vis = new HashSet<int>(_chapters.Select(c => c.n));
            _all = _file.stmts.Where(s => vis.Contains(s.c)).ToArray();
            _byId = _all.ToDictionary(s => s.id);
            _byChapter = _all.GroupBy(s => s.c).ToDictionary(g => g.Key, g => g.ToList());
        }

        public static string Version { get { Ensure(); return _file.version; } }
        public static ChapterInfo[] Chapters { get { Ensure(); return _chapters; } }
        public static IReadOnlyList<Stmt> All { get { Ensure(); return _all; } }
        public static Stmt Get(int id) { Ensure(); return _byId.TryGetValue(id, out var s) ? s : null; }
        public static List<Stmt> Chapter(int n) { Ensure(); return _byChapter.TryGetValue(n, out var l) ? l : new List<Stmt>(); }

        static Dictionary<string, (int off, int len)> _figIndex;
        static byte[] _figData;
        static string _figFile;
        static readonly Dictionary<string, Texture2D> _figCache = new Dictionary<string, Texture2D>();

        /// <summary>The official picture of a statement (all 400+ pictures are packed in one file).</summary>
        public static Texture2D Figure(Stmt s)
        {
            if (s == null || string.IsNullOrEmpty(s.f)) return null;
            if (_figCache.TryGetValue(s.f, out var tex) && tex != null) return tex;
            if (_figIndex == null || _figFile != Course.FigureFile)
            {
                _figFile = Course.FigureFile;
                _figIndex = new Dictionary<string, (int, int)>();
                var ta = Resources.Load<TextAsset>("PatenteArabo/data/" + _figFile);
                if (ta != null)
                {
                    _figData = ta.bytes;
                    int p = 0;
                    int Read() { int v = System.BitConverter.ToInt32(_figData, p); p += 4; return v; }
                    int n = Read();
                    for (int i = 0; i < n; i++)
                    {
                        int kl = Read();
                        string key = System.Text.Encoding.ASCII.GetString(_figData, p, kl); p += kl;
                        int dl = Read();
                        _figIndex[key] = (p, dl);
                        p += dl;
                    }
                }
            }
            if (!_figIndex.TryGetValue(s.f, out var e)) return null;
            var bytes = new byte[e.len];
            System.Buffer.BlockCopy(_figData, e.off, bytes, 0, e.len);
            tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.LoadImage(bytes);
            tex.wrapMode = TextureWrapMode.Clamp;
            _figCache[s.f] = tex;
            return tex;
        }

        /// <summary>An exam sheet with the official chapter distribution of the mode, never two statements from the same quesito
        /// (when a chapter has too few quesiti, the rest is filled with other statements of the same chapter).</summary>
        public static List<Stmt> ExamSheet(ExamMode mode = null, System.Random rnd = null)
        {
            Ensure();
            mode = mode ?? Course.DefaultMode;
            rnd = rnd ?? new System.Random();
            var res = new List<Stmt>();
            foreach (var ch in _chapters)
            {
                int want = mode.Quota != null ? mode.Quota(ch) : ch.exam;
                if (want <= 0) continue;
                var pool = Chapter(ch.n);
                var usedQ = new HashSet<int>();
                // prefer statements the learner has not seen yet
                var ordered = pool.OrderBy(s => Srs.Seen(s.id) ? 1 : 0).ThenBy(_ => rnd.Next()).ToList();
                var taken = new List<Stmt>();
                foreach (var s in ordered)
                {
                    if (taken.Count >= want) break;
                    if (usedQ.Add(s.q)) taken.Add(s);
                }
                foreach (var s in ordered)
                {
                    if (taken.Count >= want) break;
                    if (!taken.Contains(s)) taken.Add(s);
                }
                res.AddRange(taken);
            }
            return res;
        }

        /// <summary>Statements matching any of the keywords (lower-case substrings of the Italian text), optionally within chapters.</summary>
        public static List<Stmt> Find(string[] keywords, int[] chapters = null, bool needFigure = false)
        {
            Ensure();
            return _all.Where(s =>
                (chapters == null || chapters.Contains(s.c)) &&
                (!needFigure || !string.IsNullOrEmpty(s.f)) &&
                keywords.Any(k => s.it.ToLowerInvariant().Contains(k))).ToList();
        }
    }

    /// <summary>Translations and explanations of statements in the user's language.</summary>
    public static class QuestionText
    {
        static string _loadedLang;
        static Dictionary<int, string> _tx = new Dictionary<int, string>();
        static Dictionary<int, string> _ex = new Dictionary<int, string>();
        static Dictionary<string, string> _gloss = new Dictionary<string, string>();
        static Dictionary<string, string> _mt;
        static bool _mtDirty;

        public static void Reset() { _loadedLang = null; _exIt = null; }

        static Dictionary<int, string> LoadTexts(string res)
        {
            var d = new Dictionary<int, string>();
            var ta = Resources.Load<TextAsset>(res);
            if (ta == null) return d;
            var f = JsonUtility.FromJson<TextFile>(ta.text);
            if (f?.ids == null) return d;
            for (int i = 0; i < f.ids.Length && i < f.tx.Length; i++) d[f.ids[i]] = f.tx[i];
            return d;
        }

        static void Ensure()
        {
            string code = Lang.Code + Course.TextSuffix;
            if (_loadedLang == code) return;
            _loadedLang = code;
            _tx = LoadTexts("PatenteArabo/i18n/q_" + code);
            _ex = LoadTexts("PatenteArabo/i18n/ex_" + code);
            var g = Resources.Load<TextAsset>("PatenteArabo/i18n/gloss_" + Lang.Code);
            _gloss = MiniJson.ReadStringMap(g != null ? g.text : null);
            if (!Course.IsB)
            {
                // technical words of trucks / CQC on top of the general ones
                var gp = Resources.Load<TextAsset>("PatenteArabo/i18n/gloss_" + Lang.Code + "_pro");
                foreach (var kv in MiniJson.ReadStringMap(gp != null ? gp.text : null)) _gloss[kv.Key] = kv.Value;
            }
            _mt = null;
        }

        static string MtPath => Path.Combine(Application.persistentDataPath, "mt_" + Lang.Code + ".json");

        static Dictionary<string, string> Mt
        {
            get
            {
                if (_mt == null)
                {
                    try { _mt = MiniJson.ReadStringMap(File.Exists(MtPath) ? File.ReadAllText(MtPath) : null); }
                    catch { _mt = new Dictionary<string, string>(); }
                }
                return _mt;
            }
        }

        public static void ClearMachine()
        {
            _mt = new Dictionary<string, string>();
            _mtDirty = false;
            try { foreach (var f in Directory.GetFiles(Application.persistentDataPath, "mt_*.json")) File.Delete(f); } catch { }
        }

        public static void SaveMt()
        {
            if (!_mtDirty || _mt == null) return;
            try { File.WriteAllText(MtPath, MiniJson.WriteStringMap(_mt)); _mtDirty = false; } catch { }
        }

        /// <summary>Hand-made translation, or a cached machine translation, or null.</summary>
        public static string Translation(Stmt s, out bool machine)
        {
            machine = false;
            if (s == null || Lang.Code == "it") return null;
            Ensure();
            if (_tx.TryGetValue(s.id, out var t)) return t;
            if (Mt.TryGetValue(Course.MtPrefix + s.id, out t)) { machine = true; return t; }
            return null;
        }

        /// <summary>Asks the phone to translate (ML Kit) when no hand-made translation exists.</summary>
        public static void RequestMachine(Stmt s, Action<string> done)
        {
            if (s == null || !Lang.Info.MlKit || Lang.Code == "it") { done?.Invoke(null); return; }
            Translator.Translate(s.it, Lang.Code, res =>
            {
                if (!string.IsNullOrEmpty(res)) { Mt[Course.MtPrefix + s.id] = res; _mtDirty = true; SaveMt(); }
                done?.Invoke(res);
            });
        }

        /// <summary>Translates (on the phone) every statement that has no translation yet, one by one.</summary>
        public static void PrefetchAll(IList<Stmt> list, Action<int, int> progress, Func<bool> cancelled = null)
        {
            if (!Lang.Info.MlKit || Lang.Code == "it") { progress?.Invoke(list.Count, list.Count); return; }
            int i = 0;
            void Next()
            {
                while (i < list.Count && Translation(list[i], out _) != null) i++;
                progress?.Invoke(i, list.Count);
                if (i >= list.Count || (cancelled != null && cancelled())) { SaveMt(); return; }
                var s = list[i++];
                Translator.Translate(s.it, Lang.Code, res =>
                {
                    if (!string.IsNullOrEmpty(res)) { Mt[Course.MtPrefix + s.id] = res; _mtDirty = true; }
                    if (i % 25 == 0) SaveMt();
                    if (res == null && i <= 1) { progress?.Invoke(-1, list.Count); return; }
                    Next();
                });
            }
            Next();
        }

        static Dictionary<int, string> _exIt;

        /// <summary>Explanation in the user's language: hand-written, or a cached machine translation of the Italian one.</summary>
        public static string Explanation(Stmt s)
        {
            if (s == null) return null;
            Ensure();
            if (_ex.TryGetValue(s.id, out var e)) return e;
            if (Lang.Code != "it" && Mt.TryGetValue(Course.MtPrefix + "x" + s.id, out e)) return e;
            return null;
        }

        /// <summary>The Italian explanation (always shipped).</summary>
        public static string ExplanationIt(Stmt s)
        {
            if (s == null) return null;
            if (_exIt == null) _exIt = LoadTexts("PatenteArabo/i18n/ex_it" + Course.TextSuffix);
            return _exIt.TryGetValue(s.id, out var e) ? e : null;
        }

        /// <summary>Translates any Italian text on the phone (ML Kit) with a persistent cache. Calls back only on success.</summary>
        public static void TranslateCached(string key, string italian, Action<string> done)
        {
            if (Lang.Code == "it" || !Lang.Info.MlKit || string.IsNullOrEmpty(italian)) return;
            if (Mt.TryGetValue(key, out var hit)) { done?.Invoke(hit); return; }
            Translator.TranslateFrom("it", italian, Lang.Code, res =>
            {
                if (string.IsNullOrEmpty(res)) return;
                Mt[key] = res; _mtDirty = true; SaveMt();
                done?.Invoke(res);
            });
        }

        /// <summary>Adds the explanation to a panel: own language if available, else the Italian one, translated on the phone when possible.</summary>
        public static void ShowExplanation(UnityEngine.UIElements.VisualElement parent, Stmt s)
        {
            var own = Explanation(s);
            if (!string.IsNullOrEmpty(own)) { U.T(parent, own, "ex-text"); return; }
            var it = ExplanationIt(s);
            if (string.IsNullOrEmpty(it)) { U.T(parent, s.True ? Lang.T("generic_true") : Lang.T("generic_false"), "ex-text", "muted"); return; }
            var label = U.It(parent, it, "ex-text");
            if (Lang.Code == "it" || !Lang.Info.MlKit) return;
            Translator.TranslateFrom("it", it, Lang.Code, res =>
            {
                if (string.IsNullOrEmpty(res)) return;
                Mt[Course.MtPrefix + "x" + s.id] = res; _mtDirty = true; SaveMt();
                if (label.panel == null) return;
                var t = U.T(parent, res, "ex-text");
                t.PlaceBehind(label); label.RemoveFromHierarchy();
            });
        }

        public static bool HasExplanations { get { Ensure(); return _ex.Count > 0; } }

        public static string Gloss(string italianWord)
        {
            Ensure();
            var w = italianWord.ToLowerInvariant().Trim('\'', '’', ',', '.', ';', ':', '(', ')', '"');
            if (_gloss.TryGetValue(w, out var m)) return m;
            // try without elision (l'auto → auto)
            int ap = w.IndexOfAny(new[] { '\'', '’' });
            if (ap >= 0 && ap < w.Length - 1 && _gloss.TryGetValue(w.Substring(ap + 1), out m)) return m;
            return null;
        }

        public static string ChapterTitle(int n)
        {
            var key = (Course.IsB ? "ch" : Course.Id == Course.C ? "chc" : "chq") + n;
            var t = Lang.T(key);
            if (t == key)
            {
                var ch = Listato.Chapters.FirstOrDefault(c => c.n == n);
                return ch != null ? ch.it : key;
            }
            return t;
        }
    }

    /// <summary>Spaced repetition (Leitner boxes) for every statement, stored in one small file.</summary>
    public static class Srs
    {
        [Serializable] class Item { public int id; public int box; public long due; public int right; public int wrong; public long last; }
        [Serializable] class Store { public List<Item> items = new List<Item>(); public int exams; public int passed; public int bestExam = -1; public int driveBest; }

        static Store _s;
        static Dictionary<int, Item> _map;
        static readonly long[] Intervals = { 0, 600, 86400, 3 * 86400, 7 * 86400, 21 * 86400 };
        static string PathFile => System.IO.Path.Combine(Application.persistentDataPath, "progress" + Course.StoreSuffix + ".json");
        public static void Reset() { _s = null; _map = null; }

        static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        static void Ensure()
        {
            if (_s != null) return;
            try { _s = File.Exists(PathFile) ? JsonUtility.FromJson<Store>(File.ReadAllText(PathFile)) : new Store(); }
            catch { _s = new Store(); }
            if (_s == null) _s = new Store();
            _map = _s.items.ToDictionary(i => i.id);
        }

        public static void Save()
        {
            Ensure();
            try { File.WriteAllText(PathFile, JsonUtility.ToJson(_s)); } catch (Exception e) { Debug.LogWarning(e.Message); }
        }

        public static void Record(int id, bool correct, bool save = true)
        {
            Ensure();
            if (!_map.TryGetValue(id, out var it)) { it = new Item { id = id }; _map[id] = it; _s.items.Add(it); }
            if (correct) { it.right++; it.box = Mathf.Min(it.box + 1, Intervals.Length - 1); }
            else { it.wrong++; it.box = 0; }
            it.last = Now;
            it.due = Now + Intervals[it.box];
            if (save) Save();
        }

        public static bool Seen(int id) { Ensure(); return _map.ContainsKey(id); }
        public static bool Weak(int id) { Ensure(); return _map.TryGetValue(id, out var it) && it.box <= 1 && it.wrong > 0; }
        public static bool Due(int id) { Ensure(); return _map.TryGetValue(id, out var it) && it.wrong > 0 && it.due <= Now; }
        public static int Box(int id) { Ensure(); return _map.TryGetValue(id, out var it) ? it.box : -1; }

        public static List<Stmt> DueList() => Listato.All.Where(s => Due(s.id)).OrderBy(s => Box(s.id)).ToList();
        public static List<Stmt> WeakList() => Listato.All.Where(s => Weak(s.id)).ToList();

        /// <summary>0..1 readiness of a chapter: share of statements answered right at least twice in a row.</summary>
        public static float Readiness(int chapter)
        {
            Ensure();
            var list = Listato.Chapter(chapter);
            if (list.Count == 0) return 0;
            float sum = 0;
            foreach (var s in list)
                if (_map.TryGetValue(s.id, out var it)) sum += Mathf.Clamp01(it.box / 3f);
            return sum / list.Count;
        }

        public static float SeenShare(int chapter)
        {
            Ensure();
            var list = Listato.Chapter(chapter);
            return list.Count == 0 ? 0 : list.Count(s => _map.ContainsKey(s.id)) / (float)list.Count;
        }

        public static float OverallReadiness() =>
            Listato.Chapters.Length == 0 ? 0 : Listato.Chapters.Sum(c => Readiness(c.n) * c.exam) / Listato.Chapters.Sum(c => (float)c.exam);

        public static void ExamDone(int errors, int maxErrors = 3)
        {
            Ensure();
            _s.exams++;
            if (errors <= maxErrors) _s.passed++;
            if (_s.bestExam < 0 || errors < _s.bestExam) _s.bestExam = errors;
            Save();
        }

        public static int Exams { get { Ensure(); return _s.exams; } }
        public static int Passed { get { Ensure(); return _s.passed; } }
        public static int BestExam { get { Ensure(); return _s.bestExam; } }
        public static int DriveBest
        {
            get { Ensure(); return _s.driveBest; }
            set { Ensure(); _s.driveBest = value; Save(); }
        }

        /// <summary>Deletes all learning data stored on this phone (GDPR "right to erasure").</summary>
        public static void DeleteAll()
        {
            _s = new Store();
            _map = new Dictionary<int, Item>();
            try { foreach (var f in Directory.GetFiles(Application.persistentDataPath, "progress*.json")) File.Delete(f); } catch { }
            try { foreach (var f in Directory.GetFiles(Application.persistentDataPath, "exams*.json")) File.Delete(f); } catch { }
            foreach (var f in Directory.GetFiles(Application.persistentDataPath, "mt_*.json"))
                try { File.Delete(f); } catch { }
            Gamify.Reset();
            ExamHistory.Clear();
            // keep the language and everything that was paid for (or already given once, like the free trial)
            var keepS = new Dictionary<string, string>();
            var keepI = new Dictionary<string, int>();
            foreach (var k in new[] { "pa_lang", "pa_pass_until", "pa_course" }) if (PlayerPrefs.HasKey(k)) keepS[k] = PlayerPrefs.GetString(k);
            var intKeys = new List<string> { "pa_pass_paid", "pa_trial_given", "pa_trial_shown" };
            foreach (var id in Premium.InApp) intKeys.Add("pa_own_" + id);
            foreach (var id in Premium.Subs) intKeys.Add("pa_own_" + id);
            foreach (var k in intKeys) if (PlayerPrefs.HasKey(k)) keepI[k] = PlayerPrefs.GetInt(k);
            PlayerPrefs.DeleteAll();
            foreach (var kv in keepS) PlayerPrefs.SetString(kv.Key, kv.Value);
            foreach (var kv in keepI) PlayerPrefs.SetInt(kv.Key, kv.Value);
            PlayerPrefs.Save();
            Bookmarks.Reload();
            Vocab.Reload();
            Profile.DeletePhoto();
            try { foreach (var f in Directory.GetFiles(Application.persistentDataPath, "uimt_*.json")) File.Delete(f); } catch { }
        }
    }
}

namespace PatenteArabo
{
    /// <summary>Statements saved by the learner.</summary>
    public static class Bookmarks
    {
        static HashSet<int> _set;
        public static void Reload() { _set = null; }
        static HashSet<int> Set
        {
            get
            {
                if (_set == null)
                {
                    _set = new HashSet<int>();
                    foreach (var p in PlayerPrefs.GetString("pa_bookmarks" + Course.StoreSuffix, "").Split(','))
                        if (int.TryParse(p, out var id)) _set.Add(id);
                }
                return _set;
            }
        }
        public static bool Has(int id) => Set.Contains(id);
        public static void Toggle(int id)
        {
            if (!Set.Remove(id)) Set.Add(id);
            PlayerPrefs.SetString("pa_bookmarks" + Course.StoreSuffix, string.Join(",", Set));
            PlayerPrefs.Save();
        }
        public static List<Stmt> List() => Set.Select(Listato.Get).Where(s => s != null).ToList();
        public static int Count => Set.Count;
    }

    /// <summary>History of finished exams (kept on the phone, last 50).</summary>
    public static class ExamHistory
    {
        [Serializable] public class Entry { public long time; public int errors; public int seconds; public int[] ids; public int[] ans; public int max; }
        /// <summary>Errors allowed in that exam (old entries of licence B have 0 = 3).</summary>
        public static int MaxOf(Entry e) => e.max > 0 ? e.max : 3;
        [Serializable] class Store { public List<Entry> list = new List<Entry>(); }
        static Store _s;
        static string PathFile => Path.Combine(Application.persistentDataPath, "exams" + Course.StoreSuffix + ".json");
        public static void Reset() { _s = null; }

        static void Ensure()
        {
            if (_s != null) return;
            try { _s = File.Exists(PathFile) ? JsonUtility.FromJson<Store>(File.ReadAllText(PathFile)) : new Store(); } catch { _s = new Store(); }
            if (_s == null) _s = new Store();
        }

        public static void Add(List<Stmt> sheet, bool?[] ans, int errors, float seconds, int max = 3)
        {
            Ensure();
            _s.list.Insert(0, new Entry
            {
                time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), errors = errors, seconds = Mathf.RoundToInt(seconds), max = max,
                ids = sheet.Select(x => x.id).ToArray(),
                ans = ans.Select(a => a.HasValue ? (a.Value ? 1 : 0) : -1).ToArray()
            });
            if (_s.list.Count > 50) _s.list.RemoveRange(50, _s.list.Count - 50);
            try { File.WriteAllText(PathFile, JsonUtility.ToJson(_s)); } catch { }
        }

        public static IReadOnlyList<Entry> All { get { Ensure(); return _s.list; } }

        public static void Clear() { _s = new Store(); try { File.Delete(PathFile); } catch { } }
    }
}
