using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PatenteArabo
{
    public static class AppBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (UnityEngine.Object.FindAnyObjectByType<App>() != null) return;
            if (UitkTest.Wanted) { new GameObject("UITK Test").AddComponent<UitkTest>(); return; }
            var go = new GameObject("PatenteArabo App");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<App>();
        }
    }

    /// <summary>A full-screen page of the app.</summary>
    public abstract class Page
    {
        public abstract void Build(VisualElement root);
        /// <summary>Return true if the page handled the back button itself.</summary>
        public virtual bool OnBack() => false;
        public virtual void OnHide() { }
        public virtual void Tick(float dt) { }
        /// <summary>Pages that show the 3D world keep the camera visible behind a transparent UI.</summary>
        public virtual bool Transparent => false;
        /// <summary>Pages with a running session (exam, drive) are not rebuilt by background events: that would restart them.</summary>
        public virtual bool Busy => false;
    }

    public class App : MonoBehaviour
    {
        public static App I { get; private set; }
        UIDocument _doc;
        VisualElement _root, _pageHost, _overlayHost;
        readonly Stack<Func<Page>> _history = new Stack<Func<Page>>();
        Page _page;
        Func<Page> _pageFactory;
        Camera _bgCam;

        public VisualElement Overlay => _overlayHost;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            MainThread.Init();
            Premium.Init();
            if (Reminder.On) Reminder.Set(true, Reminder.Hour);   // alarms are lost after a force-stop: schedule again at every start
            AppUpdate.Check();

            _bgCam = Camera.main;
            if (_bgCam != null) { _bgCam.clearFlags = CameraClearFlags.SolidColor; _bgCam.backgroundColor = U.Hex("090E1C"); }

            _doc = gameObject.AddComponent<UIDocument>();
            var panel = Resources.Load<PanelSettings>("PatenteArabo/UI/Panel");
            FontFallback.Apply(panel);
            _doc.panelSettings = panel;
            _root = _doc.rootVisualElement;
            ApplyDisplay();
            var uss = Resources.Load<StyleSheet>("PatenteArabo/UI/App");
            if (uss != null) _root.styleSheets.Add(uss);
            _root.style.flexGrow = 1;

            _pageHost = new VisualElement { name = "pages" };
            _pageHost.style.flexGrow = 1;
            _root.Add(_pageHost);
            _overlayHost = new VisualElement { name = "overlay", pickingMode = PickingMode.Ignore };
            _overlayHost.style.position = Position.Absolute;
            _overlayHost.style.left = 0; _overlayHost.style.right = 0; _overlayHost.style.top = 0; _overlayHost.style.bottom = 0;
            _root.Add(_overlayHost);

            Lang.Changed += () => { Rebuild(); Lang.TranslateInterface(() => Rebuild()); };
            Lang.TranslateInterface(() => Rebuild());

            if (!Lang.Chosen) Go(() => new LanguagePage(true), false);
            else Go(() => new SplashPage(), false);
            ShotMode.Run();
        }

        void ApplySafeArea()
        {
            var sa = Screen.safeArea;
            if (Screen.width <= 0 || _root.panel == null) return;
            // convert from screen pixels to panel units
            float sx = _root.layout.width / Screen.width, sy = _root.layout.height / Screen.height;
            if (float.IsNaN(sx) || sx <= 0) return;
            _pageHost.style.paddingLeft = sa.xMin * sx;
            _pageHost.style.paddingRight = (Screen.width - sa.xMax) * sx;
            _pageHost.style.paddingTop = (Screen.height - sa.yMax) * sy;
            _pageHost.style.paddingBottom = sa.yMin * sy;
        }

        /// <summary>Opens a page. push = remember the current page for the back button.</summary>
        public void Go(Func<Page> factory, bool push = true)
        {
            if (push && _pageFactory != null) _history.Push(_pageFactory);
            Show(factory);
        }

        public void GoHome()
        {
            _history.Clear();
            Show(() => new HomePage());
        }

        public void Back()
        {
            if (_page != null && _page.OnBack()) return;
            if (_history.Count > 0) Show(_history.Pop());
            else if (!(_page is HomePage)) Show(() => new HomePage());
        }

        void Show(Func<Page> factory)
        {
            _page?.OnHide();
            ItalianTts.Stop();
            for (int i = _overlayHost.childCount - 1; i >= 0; i--) if (!(_overlayHost[i] is Label)) _overlayHost.RemoveAt(i);   // dialogs go, toasts stay
            _pageHost.Clear();
            _pageFactory = factory;
            _page = factory();
            _root.languageDirection = Lang.Rtl ? LanguageDirection.RTL : LanguageDirection.LTR;
            // Arabic-script text: no synthetic bold (it adds spacing between letters and breaks the joining)
            _root.EnableInClassList("arabic-script", Lang.ArabicScript);
            var holder = new VisualElement();
            holder.style.flexGrow = 1;
            if (!_page.Transparent) holder.AddToClassList("screen");
            _pageHost.Add(holder);
            _page.Build(holder);
            ApplySafeArea();
        }

        public void ApplyDisplay()
        {
            if (_doc != null && _doc.panelSettings != null) _doc.panelSettings.scale = Display.Scale;
        }

        public void Rebuild()
        {
            if (_page != null && _page.Busy) return;
            if (_pageFactory != null) Show(_pageFactory);
        }

        void Update()
        {
            if (BackPressed())
            {
                int top = -1;
                for (int i = _overlayHost.childCount - 1; i >= 0; i--) if (!(_overlayHost[i] is Label)) { top = i; break; }
                if (top >= 0) _overlayHost.RemoveAt(top);
                else if (_page is HomePage && _history.Count == 0)
                {
                    if (Time.unscaledTime - _lastBack < 2f) Application.Quit();
                    else { _lastBack = Time.unscaledTime; Toast.Show(Lang.T("press_back_exit")); }
                }
                else Back();
            }
            _page?.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.25f));   // first frame after resume can be minutes long
            if (Time.frameCount % 30 == 0) ApplySafeArea();
        }

        float _lastBack = -10f;

        static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        void OnApplicationPause(bool p) { if (p) { Srs.Save(); QuestionText.SaveMt(); } else AppUpdate.Check(); }
        void OnApplicationQuit() { Srs.Save(); QuestionText.SaveMt(); }

        // ---------------- overlays ----------------
        public VisualElement Dialog(string title, string body, params (string text, Action act, string cls)[] buttons)
        {
            var ov = U.El(_overlayHost, "overlay");
            ov.pickingMode = PickingMode.Position;
            var d = U.Col(ov, "dialog");
            if (!string.IsNullOrEmpty(title)) U.T(d, title, "h2", "wraptext");
            if (!string.IsNullOrEmpty(body))
            {
                var sc = U.Scroll(d);
                sc.style.maxHeight = Length.Percent(60);
                U.T(sc, body, "ex-text");
            }
            var row = U.Row(d);
            row.style.justifyContent = Justify.Center;
            row.style.marginTop = 20;
            foreach (var b in buttons)
            {
                var act = b.act;
                var btn = U.B(row, b.text, () => { ov.RemoveFromHierarchy(); act?.Invoke(); }, b.cls);
                btn.style.minWidth = 300;
            }
            return d;
        }

        public void CloseOverlays() => _overlayHost.Clear();
    }

    public static class Toast
    {
        public static void Show(string text)
        {
            if (App.I == null || App.I.Overlay == null) return;
            foreach (var old in App.I.Overlay.Query<Label>(className: "toast").ToList()) old.RemoveFromHierarchy();   // one toast at a time
            var l = new Label(text);
            l.AddToClassList("toast");
            l.pickingMode = PickingMode.Ignore;
            l.languageDirection = Lang.Rtl ? LanguageDirection.RTL : LanguageDirection.LTR;
            App.I.Overlay.Add(l);
            l.schedule.Execute(() => l.RemoveFromHierarchy()).StartingIn(2200);
        }
    }
}
