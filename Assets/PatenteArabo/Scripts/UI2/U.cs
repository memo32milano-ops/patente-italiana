using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PatenteArabo
{
    /// <summary>Small helpers to build UI Toolkit screens in code.</summary>
    public static class U
    {
        public static VisualElement El(VisualElement parent, params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) e.AddToClassList(c);
            parent?.Add(e);
            return e;
        }

        /// <summary>A row that follows the reading direction of the interface language.</summary>
        public static VisualElement Row(VisualElement parent, params string[] classes)
        {
            var e = El(parent, classes);
            e.AddToClassList(Lang.Rtl ? "row-rtl" : "row");
            return e;
        }

        /// <summary>A plain left-to-right row (Italian content, exam replica).</summary>
        public static VisualElement RowLtr(VisualElement parent, params string[] classes)
        {
            var e = El(parent, classes);
            e.AddToClassList("row");
            return e;
        }

        public static VisualElement Col(VisualElement parent, params string[] classes)
        {
            var e = El(parent, classes);
            e.AddToClassList("col");
            return e;
        }

        /// <summary>Text in the user's language (direction and alignment follow the language).</summary>
        public static Label T(VisualElement parent, string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) l.AddToClassList(c);
            l.languageDirection = Lang.Rtl ? LanguageDirection.RTL : LanguageDirection.LTR;
            if (Lang.Rtl) l.style.unityTextAlign = TextAnchor.UpperRight;
            parent?.Add(l);
            return l;
        }

        /// <summary>Text in a specific language (e.g. Italian) regardless of the interface language.</summary>
        public static Label Fixed(VisualElement parent, string text, bool rtl, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) l.AddToClassList(c);
            l.languageDirection = rtl ? LanguageDirection.RTL : LanguageDirection.LTR;
            if (rtl) l.style.unityTextAlign = TextAnchor.UpperRight;
            parent?.Add(l);
            return l;
        }

        public static Label It(VisualElement parent, string text, params string[] classes) => Fixed(parent, text, false, classes);

        public static Button B(VisualElement parent, string text, Action click, params string[] classes)
        {
            var b = new Button(() => { Sfx.Click(); click?.Invoke(); }) { text = text };
            b.AddToClassList("btn");
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) b.AddToClassList(c);
            b.languageDirection = Lang.Rtl ? LanguageDirection.RTL : LanguageDirection.LTR;
            parent?.Add(b);
            return b;
        }

        /// <summary>Button without the default app style (used by the exam replica).</summary>
        public static Button Raw(VisualElement parent, string text, Action click, params string[] classes)
        {
            var b = new Button(() => click?.Invoke()) { text = text };
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) b.AddToClassList(c);
            parent?.Add(b);
            return b;
        }

        static readonly System.Collections.Generic.Dictionary<string, Texture2D> Icons = new System.Collections.Generic.Dictionary<string, Texture2D>();

        public static Texture2D IconTex(string name)
        {
            if (!Icons.TryGetValue(name, out var t) || t == null)
            {
                t = Resources.Load<Texture2D>("PatenteArabo/Icons/" + name);
                Icons[name] = t;
            }
            return t;
        }

        /// <summary>A white drawn icon (tinted by color).</summary>
        public static VisualElement Icon(VisualElement parent, string name, float size, Color? tint = null)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.width = size; e.style.height = size; e.style.flexShrink = 0;
            var tex = IconTex(name);
            if (tex != null) e.style.backgroundImage = new StyleBackground(tex);
            e.style.unityBackgroundImageTintColor = tint ?? Color.white;
            if (Lang.Rtl && (name == "back" || name == "forward")) e.style.scale = new Scale(new Vector3(-1, 1, 1));
            parent?.Add(e);
            return e;
        }

        /// <summary>Button with a drawn icon followed by text (order follows reading direction).</summary>
        public static Button IB(VisualElement parent, string icon, string text, Action click, params string[] classes)
        {
            var b = B(parent, "", click, classes);
            b.style.flexDirection = Lang.Rtl ? FlexDirection.RowReverse : FlexDirection.Row;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            Icon(b, icon, 40);
            if (!string.IsNullOrEmpty(text))
            {
                var l = new Label(text) { pickingMode = PickingMode.Ignore };
                l.style.marginLeft = 12; l.style.marginRight = 12;
                l.languageDirection = Lang.Rtl ? LanguageDirection.RTL : LanguageDirection.LTR;
                l.style.whiteSpace = WhiteSpace.Normal;
                l.style.flexShrink = 1;
                l.name = "label";
                b.Add(l);
            }
            return b;
        }

        public static void SetIBText(Button b, string text)
        {
            var l = b.Q<Label>("label");
            if (l != null) l.text = text; else b.text = text;
        }

        public static ScrollView Scroll(VisualElement parent, params string[] classes)
        {
            var s = new ScrollView(ScrollViewMode.Vertical);
            s.style.flexGrow = 1;
            s.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            s.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            s.scrollDecelerationRate = 0.135f;
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) s.AddToClassList(c);
            parent?.Add(s);
            return s;
        }

        public static VisualElement Img(VisualElement parent, Texture2D tex, params string[] classes)
        {
            var e = El(parent, classes);
            if (tex != null) e.style.backgroundImage = new StyleBackground(tex);
            return e;
        }

        public static VisualElement Bar(VisualElement parent, float value, Color? color = null)
        {
            var bar = El(parent, "bar");
            var fill = El(bar, "bar-fill");
            fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100f);
            if (color.HasValue) fill.style.backgroundColor = color.Value;
            return bar;
        }

        public static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString("#" + h, out var c);
            return c;
        }

        public static void Grow(VisualElement e, float g = 1) { e.style.flexGrow = g; }
        public static void Space(VisualElement parent, float px) { var s = El(parent); s.style.height = px; s.style.width = px; s.style.flexShrink = 0; }
    }

    /// <summary>Tiny click sound + haptics.</summary>
    public static class Sfx
    {
        static AudioSource _src;
        static AudioClip _click, _ok, _ko;

        static AudioClip Tone(float f, float len, float vol)
        {
            int rate = 22050, n = (int)(rate * len);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * 18f);
                data[i] = Mathf.Sin(2 * Mathf.PI * f * t) * env * vol;
            }
            var c = AudioClip.Create("tone" + f, n, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }

        static void Ensure()
        {
            if (_src != null) return;
            var go = new GameObject("Sfx");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _src = go.AddComponent<AudioSource>();
            _click = Tone(1200, 0.05f, 0.15f);
            _ok = Tone(880, 0.25f, 0.3f);
            _ko = Tone(220, 0.3f, 0.3f);
        }

        public static bool On
        {
            get => PlayerPrefs.GetInt("pa_sfx", 1) == 1;
            set { PlayerPrefs.SetInt("pa_sfx", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool Vibrate
        {
            get => PlayerPrefs.GetInt("pa_vib", 1) == 1;
            set { PlayerPrefs.SetInt("pa_vib", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static void Click() { if (!On) return; Ensure(); _src.PlayOneShot(_click); }
        public static void Right() { if (!On) return; Ensure(); _src.PlayOneShot(_ok); }
        public static void Wrong()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Vibrate) Handheld.Vibrate();
#endif
            if (!On) return;
            Ensure(); _src.PlayOneShot(_ko);
        }
    }
}
