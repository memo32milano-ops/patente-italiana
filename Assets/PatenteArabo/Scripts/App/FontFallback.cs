using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace PatenteArabo
{
    /// <summary>
    /// Scripts that the iPhone's system fallback does not give to Unity (they showed as empty boxes):
    /// Tamil, Gujarati, Kannada, Thai, Georgian, Hebrew. Noto Sans for each ships in Resources and is added
    /// as a global fallback of the UI panel, so any text that Poppins cannot draw tries these first.
    /// </summary>
    public static class FontFallback
    {
        static readonly string[] Fonts = { "NotoSansTamil-Regular", "NotoSansGujarati-Regular", "NotoSansKannada-Regular",
                                           "NotoSansThai-Regular", "NotoSansGeorgian-Regular", "NotoSansHebrew-Regular" };

        public static void Apply(PanelSettings ps)
        {
            if (ps == null) return;
            try
            {
                var ts = ps.textSettings;
                if (ts == null) { ts = ScriptableObject.CreateInstance<PanelTextSettings>(); ps.textSettings = ts; }
                var list = ts.fallbackFontAssets ?? new List<FontAsset>();
                foreach (var name in Fonts)
                {
                    var font = Resources.Load<Font>("PatenteArabo/Fonts/" + name);
                    if (font == null) { Debug.LogWarning("[Patente] font missing " + name); continue; }
                    var fa = FontAsset.CreateFontAsset(font);
                    if (fa == null) { Debug.LogWarning("[Patente] font asset failed " + name); continue; }
                    fa.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                    list.Add(fa);
                }
                ts.fallbackFontAssets = list;
                Debug.Log("[Patente] font fallbacks " + list.Count);
            }
            catch (System.Exception e) { Debug.LogWarning("[Patente] font fallback: " + e.Message); }
        }
    }
}
