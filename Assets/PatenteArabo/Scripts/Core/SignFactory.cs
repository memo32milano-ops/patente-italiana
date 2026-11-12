using System;
using System.Collections.Generic;
using UnityEngine;

namespace PatenteArabo
{
    /// <summary>Draws Italian road signs procedurally into textures (cached).</summary>
    public static class SignFactory
    {
        const int S = 256;
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        public static readonly Color Red = new Color(0.80f, 0.07f, 0.12f);
        public static readonly Color Blue = new Color(0.05f, 0.30f, 0.66f);
        public static readonly Color White = Color.white;
        public static readonly Color Black = new Color(0.08f, 0.08f, 0.08f);
        public static readonly Color Yellow = new Color(1f, 0.78f, 0.05f);

        public static bool Has(string id) => !string.IsNullOrEmpty(id) && id != "none";

        public static Sprite GetSprite(string id)
        {
            if (!Has(id)) return null;
            if (SpriteCache.TryGetValue(id, out var sp) && sp != null) return sp;
            var tex = GetTexture(id);
            sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            SpriteCache[id] = sp;
            return sp;
        }

        public static Texture2D GetTexture(string id)
        {
            if (Cache.TryGetValue(id, out var t) && t != null) return t;
            var c = new Canvas2D(S);
            Draw(id, c);
            t = c.ToTexture(id);
            Cache[id] = t;
            return t;
        }

        static void Draw(string id, Canvas2D c)
        {
            float m = S / 2f;
            switch (id)
            {
                case "stop":
                {
                    var oct = Polygon(m, m, 124, 8, 22.5f);
                    c.Poly(oct, White);
                    c.Poly(Polygon(m, m, 114, 8, 22.5f), Red);
                    c.Text("STOP", m, m, 8.5f, White);
                    break;
                }
                case "precedenza":
                {
                    var outer = Tri(m, m + 10, 120, true);
                    c.Poly(outer, Red);
                    c.Poly(Tri(m, m + 10, 78, true), White);
                    break;
                }
                case "pericolo_generico":
                    DangerBase(c);
                    c.Rect(m - 9, 74, m + 9, 132, Black);
                    c.Circle(m, 150, 10, Black);
                    break;
                case "dosso":   // speed bump ahead
                    DangerBase(c);
                    c.Circle(m - 24, 156, 22, Black);
                    c.Circle(m + 24, 156, 22, Black);
                    c.Rect(m - 62, 156, m + 62, 166, White);
                    c.Rect(m - 62, 152, m + 62, 160, Black);
                    break;
                case "curva_pericolosa":   // dangerous bend to the right
                    DangerBase(c);
                    c.Line(m - 16, 160, m - 16, 124, 16, Black);
                    Arrow(c, m - 16, 128, m + 24, 84, 16, Black);
                    break;
                case "strada_sdrucciolevole":   // slippery road
                    DangerBase(c);
                    Car(c, m, 98, Black);
                    foreach (var sx in new[] { -16f, 16f })
                    {
                        c.Line(m + sx - 8, 138, m + sx + 6, 148, 6, Black);
                        c.Line(m + sx + 6, 148, m + sx - 6, 160, 6, Black);
                    }
                    break;
                case "discesa_pericolosa":   // steep descent
                    DangerBase(c);
                    c.Poly(new[] { new Vector2(m - 58, 160), new Vector2(m + 58, 160), new Vector2(m - 58, 104) }, Black);
                    c.Text("10%", m + 22, 120, 5.5f, Black);
                    break;
                case "pericolo_pedoni":
                    DangerBase(c);
                    Pedestrian(c, m, 116, 0.72f, Black);
                    break;
                case "croce_s_andrea":
                {
                    // Red-bordered white X
                    c.Line(30, 30, S - 30, S - 30, 40, Red);
                    c.Line(30, S - 30, S - 30, 30, 40, Red);
                    c.Line(30, 30, S - 30, S - 30, 24, White);
                    c.Line(30, S - 30, S - 30, 30, 24, White);
                    break;
                }
                case "divieto_accesso":
                    c.Circle(m, m, 124, White);
                    c.Circle(m, m, 118, Red);
                    c.Rect(m - 82, m - 20, m + 82, m + 20, White);
                    break;
                case "limite_50": SpeedLimit(c, "50"); break;
                case "limite_90": SpeedLimit(c, "90"); break;
                case "limite_130": SpeedLimit(c, "130"); break;
                case "divieto_sorpasso":
                    c.Circle(m, m, 124, Red);
                    c.Circle(m, m, 98, White);
                    Car(c, m - 34, m + 6, Red);
                    Car(c, m + 34, m + 6, Black);
                    break;
                case "obbligo_dritto":
                    c.Circle(m, m, 124, White);
                    c.Circle(m, m, 118, Blue);
                    Arrow(c, m, 196, m, 66, 22, White);
                    break;
                case "senso_unico":
                    c.RoundRect(18, 70, S - 18, S - 70, 14, White);
                    c.RoundRect(24, 76, S - 24, S - 76, 10, Blue);
                    Arrow(c, 52, m, S - 46, m, 22, White);
                    break;
                case "attraversamento_pedonale":
                    c.RoundRect(14, 14, S - 14, S - 14, 18, White);
                    c.RoundRect(22, 22, S - 22, S - 22, 14, Blue);
                    c.Poly(Tri(m, m + 8, 96, false), White);
                    Pedestrian(c, m, m + 26, 0.62f, Black);
                    break;
                case "rotatoria":
                    c.Circle(m, m, 124, White);
                    c.Circle(m, m, 118, Blue);
                    for (int k = 0; k < 3; k++)
                    {
                        float a0 = k * 120f + 10f;
                        c.Arc(m, m, 66, a0, a0 + 75f, 20, White);
                        float a1 = (a0 + 75f) * Mathf.Deg2Rad;
                        float ax = m + Mathf.Cos(a1) * 66, ay = m + Mathf.Sin(a1) * 66;
                        float tx = -Mathf.Sin(a1), ty = Mathf.Cos(a1);
                        c.Poly(new[]
                        {
                            new Vector2(ax + tx * 30, ay + ty * 30),
                            new Vector2(ax - ty * 26 - tx * 4, ay + tx * 26 - ty * 4),
                            new Vector2(ax + ty * 26 - tx * 4, ay - tx * 26 - ty * 4),
                        }, White);
                    }
                    break;
                case "divieto_sosta":
                case "divieto_fermata":
                    c.Circle(m, m, 124, Red);
                    c.Circle(m, m, 96, Blue);
                    c.Line(m - 66, m - 66, m + 66, m + 66, 26, Red);
                    if (id == "divieto_fermata") c.Line(m + 66, m - 66, m - 66, m + 66, 26, Red);
                    break;
                case "diritto_precedenza":
                    c.Poly(Polygon(m, m, 124, 4, 0), new Color(0.3f, 0.3f, 0.3f));
                    c.Poly(Polygon(m, m, 118, 4, 0), White);
                    c.Poly(Polygon(m, m, 78, 4, 0), Yellow);
                    break;
                case "parcheggio":
                    c.RoundRect(14, 14, S - 14, S - 14, 18, White);
                    c.RoundRect(22, 22, S - 22, S - 22, 14, Blue);
                    c.Text("P", m, m, 24, White);
                    break;
                case "semaforo_rosso":
                case "semaforo_giallo":
                case "semaforo_verde":
                {
                    c.RoundRect(78, 14, 178, S - 14, 22, Black);
                    Color off = new Color(0.25f, 0.25f, 0.25f);
                    c.Circle(m, 56, 30, id == "semaforo_rosso" ? new Color(1f, 0.1f, 0.1f) : off);
                    c.Circle(m, 128, 30, id == "semaforo_giallo" ? Yellow : off);
                    c.Circle(m, 200, 30, id == "semaforo_verde" ? new Color(0.1f, 0.9f, 0.3f) : off);
                    break;
                }
                default:
                    c.Circle(m, m, 120, Color.gray);
                    break;
            }
        }

        static void DangerBase(Canvas2D c)
        {
            float m = S / 2f;
            c.Poly(Tri(m, m - 6, 124, false), Red);
            c.Poly(Tri(m, m - 6, 84, false), White);
        }

        static void SpeedLimit(Canvas2D c, string num)
        {
            float m = S / 2f;
            c.Circle(m, m, 124, Red);
            c.Circle(m, m, 96, White);
            c.Text(num, m, m, num.Length > 2 ? 13 : 18, Black);
        }

        static void Car(Canvas2D c, float cx, float cy, Color col)
        {
            c.RoundRect(cx - 22, cy - 30, cx + 22, cy + 34, 10, col);
            c.Rect(cx - 30, cy - 22, cx - 22, cy - 8, col);
            c.Rect(cx + 22, cy - 22, cx + 30, cy - 8, col);
            c.Rect(cx - 30, cy + 14, cx - 22, cy + 28, col);
            c.Rect(cx + 22, cy + 14, cx + 30, cy + 28, col);
        }

        static void Arrow(Canvas2D c, float x0, float y0, float x1, float y1, float w, Color col)
        {
            var d = new Vector2(x1 - x0, y1 - y0).normalized;
            var p = new Vector2(-d.y, d.x);
            var tip = new Vector2(x1, y1);
            var bas = tip - d * 46;
            c.Line(x0, y0, bas.x, bas.y, w, col);
            c.Poly(new[] { tip, bas + p * 34, bas - p * 34 }, col);
        }

        static void Pedestrian(Canvas2D c, float cx, float cy, float s, Color col)
        {
            c.Circle(cx + 4 * s, cy - 58 * s, 13 * s, col);
            c.Line(cx, cy - 40 * s, cx - 6 * s, cy + 10 * s, 18 * s, col);           // body
            c.Line(cx - 6 * s, cy + 10 * s, cx - 30 * s, cy + 56 * s, 14 * s, col);   // back leg
            c.Line(cx - 6 * s, cy + 10 * s, cx + 24 * s, cy + 56 * s, 14 * s, col);   // front leg
            c.Line(cx, cy - 32 * s, cx - 28 * s, cy - 2 * s, 11 * s, col);            // arm
            c.Line(cx, cy - 32 * s, cx + 26 * s, cy - 8 * s, 11 * s, col);            // arm
        }

        // y grows downward in this canvas (screen-like), converted on export.
        static Vector2[] Tri(float cx, float cy, float r, bool pointDown)
        {
            var pts = new Vector2[3];
            for (int i = 0; i < 3; i++)
            {
                float a = (pointDown ? 90f : -90f) + i * 120f;
                pts[i] = new Vector2(cx + Mathf.Cos(a * Mathf.Deg2Rad) * r, cy + Mathf.Sin(a * Mathf.Deg2Rad) * r);
            }
            return pts;
        }

        static Vector2[] Polygon(float cx, float cy, float r, int n, float rotDeg)
        {
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = (rotDeg + i * 360f / n) * Mathf.Deg2Rad;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return pts;
        }

        /// <summary>Tiny anti-aliased raster canvas (top-left origin).</summary>
        class Canvas2D
        {
            readonly int _s;
            readonly Color[] _px;
            const int SS = 3; // supersampling per axis

            public Canvas2D(int size)
            {
                _s = size;
                _px = new Color[size * size];
                for (int i = 0; i < _px.Length; i++) _px[i] = new Color(1, 1, 1, 0);
            }

            void Blend(int x, int y, Color c, float a)
            {
                if (a <= 0f || x < 0 || y < 0 || x >= _s || y >= _s) return;
                int i = y * _s + x;
                var d = _px[i];
                float outA = a + d.a * (1 - a);
                if (outA <= 0) return;
                var rgb = (new Color(c.r, c.g, c.b) * a + new Color(d.r, d.g, d.b) * d.a * (1 - a)) / outA;
                _px[i] = new Color(rgb.r, rgb.g, rgb.b, outA);
            }

            void Fill(float minX, float minY, float maxX, float maxY, Func<float, float, bool> inside, Color col)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt(minX) - 1), x1 = Mathf.Min(_s - 1, Mathf.CeilToInt(maxX) + 1);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(minY) - 1), y1 = Mathf.Min(_s - 1, Mathf.CeilToInt(maxY) + 1);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                        if (inside(x + (sx + 0.5f) / SS, y + (sy + 0.5f) / SS)) hit++;
                    if (hit > 0) Blend(x, y, col, hit / (float)(SS * SS) * col.a);
                }
            }

            public void Circle(float cx, float cy, float r, Color col) =>
                Fill(cx - r, cy - r, cx + r, cy + r, (x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r, col);

            public void Rect(float x0, float y0, float x1, float y1, Color col) =>
                Fill(x0, y0, x1, y1, (x, y) => x >= x0 && x <= x1 && y >= y0 && y <= y1, col);

            public void RoundRect(float x0, float y0, float x1, float y1, float r, Color col) =>
                Fill(x0, y0, x1, y1, (x, y) =>
                {
                    if (x < x0 || x > x1 || y < y0 || y > y1) return false;
                    float qx = Mathf.Max(Mathf.Max(x0 + r - x, x - (x1 - r)), 0);
                    float qy = Mathf.Max(Mathf.Max(y0 + r - y, y - (y1 - r)), 0);
                    return qx * qx + qy * qy <= r * r;
                }, col);

            public void Line(float ax, float ay, float bx, float by, float w, Color col)
            {
                float h = w / 2f;
                var a = new Vector2(ax, ay);
                var ab = new Vector2(bx - ax, by - ay);
                float len2 = Mathf.Max(ab.sqrMagnitude, 0.0001f);
                Fill(Mathf.Min(ax, bx) - h, Mathf.Min(ay, by) - h, Mathf.Max(ax, bx) + h, Mathf.Max(ay, by) + h, (x, y) =>
                {
                    var p = new Vector2(x, y) - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p, ab) / len2);
                    return (p - ab * t).sqrMagnitude <= h * h;
                }, col);
            }

            public void Arc(float cx, float cy, float r, float a0, float a1, float w, Color col)
            {
                float h = w / 2f;
                Fill(cx - r - h, cy - r - h, cx + r + h, cy + r + h, (x, y) =>
                {
                    float dx = x - cx, dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (Mathf.Abs(d - r) > h) return false;
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    float rel = Mathf.Repeat(ang - a0, 360f);
                    return rel <= a1 - a0;
                }, col);
            }

            public void Poly(Vector2[] pts, Color col)
            {
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                foreach (var p in pts)
                {
                    minX = Mathf.Min(minX, p.x); minY = Mathf.Min(minY, p.y);
                    maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y);
                }
                Fill(minX, minY, maxX, maxY, (x, y) =>
                {
                    bool inside = false;
                    for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                    {
                        if (((pts[i].y > y) != (pts[j].y > y)) &&
                            (x < (pts[j].x - pts[i].x) * (y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x))
                            inside = !inside;
                    }
                    return inside;
                }, col);
            }

            // 5x7 pixel font for the few glyphs we need on signs.
            static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
            {
                {'S', new[]{".###.","#...#","#....",".###.","....#","#...#",".###."}},
                {'T', new[]{"#####","..#..","..#..","..#..","..#..","..#..","..#.."}},
                {'O', new[]{".###.","#...#","#...#","#...#","#...#","#...#",".###."}},
                {'P', new[]{"####.","#...#","#...#","####.","#....","#....","#...."}},
                {'0', new[]{".###.","#...#","#..##","#.#.#","##..#","#...#",".###."}},
                {'1', new[]{"..#..",".##..","..#..","..#..","..#..","..#..",".###."}},
                {'2', new[]{".###.","#...#","....#","...#.","..#..",".#...","#####"}},
                {'3', new[]{"####.","....#","....#",".###.","....#","....#","####."}},
                {'4', new[]{"...#.","..##.",".#.#.","#..#.","#####","...#.","...#."}},
                {'5', new[]{"#####","#....","####.","....#","....#","#...#",".###."}},
                {'6', new[]{".###.","#....","#....","####.","#...#","#...#",".###."}},
                {'7', new[]{"#####","....#","...#.","..#..",".#...",".#...",".#..."}},
                {'8', new[]{".###.","#...#","#...#",".###.","#...#","#...#",".###."}},
                {'9', new[]{".###.","#...#","#...#",".####","....#","....#",".###."}},
            };

            public void Text(string s, float cx, float cy, float cell, Color col)
            {
                float gap = cell * 0.9f;
                float w = s.Length * 5 * cell + (s.Length - 1) * gap;
                float x = cx - w / 2f;
                float y0 = cy - 3.5f * cell;
                foreach (var ch in s)
                {
                    if (Glyphs.TryGetValue(ch, out var g))
                        for (int r = 0; r < 7; r++)
                        for (int k = 0; k < 5; k++)
                            if (g[r][k] == '#')
                                RoundRect(x + k * cell, y0 + r * cell, x + (k + 1) * cell + 0.6f, y0 + (r + 1) * cell + 0.6f,
                                    cell * 0.3f, col);
                    x += 5 * cell + gap;
                }
            }

            public Texture2D ToTexture(string name)
            {
                var tex = new Texture2D(_s, _s, TextureFormat.RGBA32, true) { name = "Sign_" + name, wrapMode = TextureWrapMode.Clamp };
                var flipped = new Color[_px.Length];
                for (int y = 0; y < _s; y++)
                    Array.Copy(_px, y * _s, flipped, (_s - 1 - y) * _s, _s);
                tex.SetPixels(flipped);
                tex.Apply(true);
                return tex;
            }
        }
    }
}
