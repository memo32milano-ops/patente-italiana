using System.Collections.Generic;
using UnityEngine;

namespace PatenteArabo
{
    public class LangInfo
    {
        public string Code, Native, English;
        public bool Rtl;
        public bool MlKit;     // on-device machine translation available (Google ML Kit)
        public bool Core;      // hand-made translations shipped inside the app
        public LangInfo(string code, string native, string english, bool core, bool mlkit, bool rtl = false)
        { Code = code; Native = native; English = english; Core = core; MlKit = mlkit; Rtl = rtl; }
    }

    /// <summary>Interface language + translations of the question bank.</summary>
    public static class Lang
    {
        public static readonly LangInfo[] All =
        {
            new LangInfo("it", "Italiano", "Italian", true, true),
            new LangInfo("ar", "العربية", "Arabic", true, true, true),
            new LangInfo("en", "English", "English", true, true),
            new LangInfo("fr", "Français", "French", true, true),
            new LangInfo("es", "Español", "Spanish", true, true),
            new LangInfo("ro", "Română", "Romanian", true, true),
            new LangInfo("sq", "Shqip", "Albanian", true, true),
            new LangInfo("uk", "Українська", "Ukrainian", true, true),
            new LangInfo("zh", "中文", "Chinese", true, true),
            new LangInfo("bn", "বাংলা", "Bengali", true, true),
            new LangInfo("ur", "اردو", "Urdu", true, true, true),
            new LangInfo("hi", "हिन्दी", "Hindi", true, true),
            new LangInfo("ca", "Català", "Catalan", true, true),
            new LangInfo("ru", "Русский", "Russian", true, true),
            new LangInfo("de", "Deutsch", "German", true, true),
            new LangInfo("pt", "Português", "Portuguese", true, true),
            new LangInfo("pl", "Polski", "Polish", true, true),
            new LangInfo("tr", "Türkçe", "Turkish", true, true),
            new LangInfo("fa", "فارسی", "Persian", true, true, true),
            new LangInfo("ta", "தமிழ்", "Tamil", true, true),
            new LangInfo("te", "తెలుగు", "Telugu", true, true),
            new LangInfo("mr", "मराठी", "Marathi", true, true),
            new LangInfo("gu", "ગુજરાતી", "Gujarati", true, true),
            new LangInfo("kn", "ಕನ್ನಡ", "Kannada", true, true),
            new LangInfo("ja", "日本語", "Japanese", true, true),
            new LangInfo("ko", "한국어", "Korean", true, true),
            new LangInfo("vi", "Tiếng Việt", "Vietnamese", true, true),
            new LangInfo("th", "ไทย", "Thai", true, true),
            new LangInfo("id", "Bahasa Indonesia", "Indonesian", true, true),
            new LangInfo("ms", "Bahasa Melayu", "Malay", true, true),
            new LangInfo("tl", "Filipino", "Filipino", true, true),
            new LangInfo("sw", "Kiswahili", "Swahili", true, true),
            new LangInfo("el", "Ελληνικά", "Greek", true, true),
            new LangInfo("bg", "Български", "Bulgarian", true, true),
            new LangInfo("hr", "Hrvatski", "Croatian", true, true),
            new LangInfo("cs", "Čeština", "Czech", true, true),
            new LangInfo("sk", "Slovenčina", "Slovak", true, true),
            new LangInfo("sl", "Slovenščina", "Slovenian", true, true),
            new LangInfo("hu", "Magyar", "Hungarian", true, true),
            new LangInfo("nl", "Nederlands", "Dutch", true, true),
            new LangInfo("ka", "ქართული", "Georgian", true, true),
            new LangInfo("he", "עברית", "Hebrew", true, true, true),
            new LangInfo("mk", "Македонски", "Macedonian", true, true),
            new LangInfo("be", "Беларуская", "Belarusian", true, true),
            new LangInfo("lt", "Lietuvių", "Lithuanian", true, true),
            new LangInfo("lv", "Latviešu", "Latvian", true, true),
            new LangInfo("et", "Eesti", "Estonian", true, true),
            new LangInfo("sv", "Svenska", "Swedish", true, true),
            new LangInfo("da", "Dansk", "Danish", true, true),
            new LangInfo("fi", "Suomi", "Finnish", true, true),
            new LangInfo("no", "Norsk", "Norwegian", true, true),
        };

        static Dictionary<string, string> _ui, _en, _uiMt;
        static bool _translatingUi;
        static string _code;
        public static event System.Action Changed;

        public static bool Chosen => PlayerPrefs.HasKey("pa_lang");

        public static string Code
        {
            get
            {
                if (_code == null) _code = PlayerPrefs.GetString("pa_lang", GuessDevice());
                return _code;
            }
        }

        public static LangInfo Info => Get(Code);
        public static bool Rtl => Info.Rtl;
        /// <summary>Languages written in Arabic script (cursive, letters must stay joined).</summary>
        public static bool ArabicScript => Code == "ar" || Code == "fa" || Code == "ur";

        public static LangInfo Get(string code)
        {
            foreach (var l in All) if (l.Code == code) return l;
            return All[0];
        }

        public static void Set(string code)
        {
            _code = code;
            _ui = null;
            _uiMt = null;
            PlayerPrefs.SetString("pa_lang", code);
            PlayerPrefs.Save();
            QuestionText.Reset();
            if (Reminder.On) Reminder.Set(true, Reminder.Hour);   // notification text in the new language
            Changed?.Invoke();
        }

        static string GuessDevice()
        {
            string iso = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var loc = new AndroidJavaClass("java.util.Locale"))
                using (var def = loc.CallStatic<AndroidJavaObject>("getDefault"))
                    iso = def.Call<string>("getLanguage");
            }
            catch { }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                iso = IosNative.Locale();                       // e.g. "pt-BR", "zh-Hans-IT"
                if (!string.IsNullOrEmpty(iso)) iso = iso.Split('-')[0];
            }
            catch { }
#endif
            if (!string.IsNullOrEmpty(iso))
            {
                iso = iso.ToLowerInvariant();
                if (iso == "iw") iso = "he";
                if (iso == "in") iso = "id";
                if (iso == "nb" || iso == "nn") iso = "no";
                if (iso == "fil") iso = "tl";
                foreach (var l in All) if (l.Code == iso) return iso;
            }
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Arabic: return "ar";
                case SystemLanguage.English: return "en";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.Romanian: return "ro";
                case SystemLanguage.Ukrainian: return "uk";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional: return "zh";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Polish: return "pl";
                case SystemLanguage.Turkish: return "tr";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Dutch: return "nl";
                case SystemLanguage.Greek: return "el";
                default: return "it";
            }
        }

        static Dictionary<string, string> Load(string code)
        {
            var ta = Resources.Load<TextAsset>("PatenteArabo/i18n/ui_" + code);
            return MiniJson.ReadStringMap(ta != null ? ta.text : null);
        }

        /// <summary>Interface text for key in the current language (falls back to English, then the key).</summary>
        public static string T(string key)
        {
#if UNITY_IOS
            // the same texts serve both stores: on iPhone every "Google Play" becomes "App Store"
            return Fix(Raw(key));
        }

        static string Fix(string t) => t != null && t.Contains("Google Play") ? t.Replace("Google Play", "App Store") : t;

        static string Raw(string key)
        {
#endif
            if (_ui == null) _ui = Load(Code);
            if (_en == null) _en = Load("en");
            if (_ui.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)) return v;
            if (_ui.Count == 0)
            {
                if (_uiMt == null) _uiMt = LoadUiMt(Code);
                if (_uiMt.TryGetValue(key, out v) && !string.IsNullOrEmpty(v)) return v;
            }
            if (_en.TryGetValue(key, out v) && !string.IsNullOrEmpty(v)) return v;
            return key;
        }

        static string UiMtPath(string code) => System.IO.Path.Combine(Application.persistentDataPath, "uimt_" + code + ".json");

        static Dictionary<string, string> LoadUiMt(string code)
        {
            try { var p = UiMtPath(code); return MiniJson.ReadStringMap(System.IO.File.Exists(p) ? System.IO.File.ReadAllText(p) : null); }
            catch { return new Dictionary<string, string>(); }
        }

        /// <summary>For languages without a hand-made interface file: translate the English interface once on the phone (ML Kit) and keep it.</summary>
        public static void TranslateInterface(System.Action done)
        {
            if (_translatingUi || !Info.MlKit) return;
            if (_ui == null) _ui = Load(Code);
            if (_ui.Count > 0) return;
            if (_en == null) _en = Load("en");
            if (_uiMt == null) _uiMt = LoadUiMt(Code);
            var todo = new List<string>();
            foreach (var k in _en.Keys) if (!_uiMt.ContainsKey(k)) todo.Add(k);
            if (todo.Count == 0) return;
            _translatingUi = true;
            string code = Code;
            int i = 0;
            Toast.Show(T("translating"));
            void Next()
            {
                if (code != Code) { _translatingUi = false; return; }
                if (i >= todo.Count)
                {
                    _translatingUi = false;
                    try { System.IO.File.WriteAllText(UiMtPath(code), MiniJson.WriteStringMap(_uiMt)); } catch { }
                    done?.Invoke();
                    return;
                }
                string key = todo[i++];
                Translator.TranslateFrom("en", _en[key], code, res =>
                {
                    if (!string.IsNullOrEmpty(res)) _uiMt[key] = res;
                    else if (i == 1) { _translatingUi = false; return; } // model not available (no internet?) — try again next launch
                    Next();
                });
            }
            Next();
        }

        public static string T(string key, params object[] args)
        {
            try { return string.Format(T(key), args); }
            catch { return T(key); }
        }
    }
}
