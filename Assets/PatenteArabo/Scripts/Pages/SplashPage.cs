using UnityEngine;
using UnityEngine.UIElements;

namespace PatenteArabo
{
    /// <summary>
    /// Opening screen (instead of the engine logo): the app icon and an encouraging line in the learner's language,
    /// then home after a moment (or at the first tap).
    /// </summary>
    public class SplashPage : Page
    {
        float _t;
        bool _left;
        const float Duration = 2.2f;

        public override void Build(VisualElement root)
        {
            var c = U.Col(root, "center", "splash");
            c.style.flexGrow = 1;
            var logo = U.El(c, "splash-logo");
            logo.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>("PatenteArabo/Icons/app_logo"));
            U.It(c, Brand.Name, "title").style.marginTop = 26;
            var msg = U.T(c, Lang.T(Pick()), "splash-msg", "wraptext");
            msg.style.unityTextAlign = TextAnchor.MiddleCenter;
            var bar = U.El(c, "splash-bar");
            var fill = U.El(bar, "splash-fill");
            fill.schedule.Execute(() => fill.style.width = Length.Percent(Mathf.Clamp01(_t / Duration) * 100)).Every(30);
            root.RegisterCallback<PointerDownEvent>(_ => Leave());
        }

        /// <summary>First time: "you chose the right place"; then a different line each day.</summary>
        static string Pick()
        {
            if (Gamify.Xp <= 0) return "splash_1";
            int day = (int)(System.DateTime.Now.Date - new System.DateTime(2026, 1, 1)).TotalDays;
            return "splash_" + (2 + Mathf.Abs(day) % 4);
        }

        public override void Tick(float dt)
        {
            _t += dt;
            if (_t >= Duration) Leave();
        }

        void Leave()
        {
            if (_left) return;
            _left = true;
            App.I.GoHome();
        }
    }
}
