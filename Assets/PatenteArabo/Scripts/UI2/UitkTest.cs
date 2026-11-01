using UnityEngine;
using UnityEngine.UIElements;

namespace PatenteArabo
{
    /// <summary>Temporary check that UI Toolkit's advanced text renders every script we need.</summary>
    public class UitkTest : MonoBehaviour
    {
        public static bool Wanted => Application.isEditor && System.IO.File.Exists("uitk_test.flag") && System.IO.File.ReadAllText("uitk_test.flag").Trim() == "on";

        void Start()
        {
            var ps = Resources.Load<PanelSettings>("PatenteArabo/UI/Panel");
            var doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            var root = doc.rootVisualElement;
            var uss = Resources.Load<StyleSheet>("PatenteArabo/UI/App");
            if (uss != null) root.styleSheets.Add(uss);
            root.style.backgroundColor = new Color(0.95f, 0.95f, 0.95f);
            root.style.paddingLeft = 40; root.style.paddingTop = 30;
            string[] lines =
            {
                "PanelSettings: " + (ps != null) + "  USS: " + (uss != null),
                "Italiano: Il segnale raffigurato vieta la sosta àèéìòù",
                "العربية: الإشارة دي معناها ممنوع الوقوف — لا تتجاوز ٥٠ كم/س",
                "اردو: یہ نشان پارکنگ کی ممانعت کرتا ہے",
                "فارسی: این تابلو توقف را ممنوع می‌کند",
                "हिन्दी: यह संकेत पार्किंग पर रोक लगाता है",
                "বাংলা: এই সংকেতটি পার্কিং নিষিদ্ধ করে",
                "ਪੰਜਾਬੀ: ਇਹ ਨਿਸ਼ਾਨ ਪਾਰਕਿੰਗ ਦੀ ਮਨਾਹੀ ਕਰਦਾ ਹੈ",
                "தமிழ்: இந்த அடையாளம் நிறுத்தத்தை தடை செய்கிறது",
                "中文：该标志禁止停车  日本語：駐車禁止の標識です  한국어: 주차 금지 표지",
                "Русский / Українська: Знак забороняє стоянку  Ελληνικά: Απαγορεύεται",
                "ไทย: ป้ายนี้ห้ามจอดรถ   ქართული: ნიშანი   עברית: התמרור אוסר חניה",
                "አማርኛ: ምልክቱ   Shqip: Sinjali ndalon parkimin ëç",
            };
            foreach (var s in lines)
            {
                var l = new Label(s);
                l.style.fontSize = 30;
                l.style.color = Color.black;
                l.style.marginBottom = 6;
                root.Add(l);
            }
        }
    }
}
