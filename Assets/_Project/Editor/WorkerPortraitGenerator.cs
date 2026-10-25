#if UNITY_EDITOR
// Положить в: Assets/_Project/Scripts/Editor/Workers/WorkerPortraitGenerator.cs
// Меню: Tools > BuilderSim > Рабочие > Сгенерировать портреты
// Результат: Assets/Project/Art/Workers/Portraits/<id>.png (256x256, Sprite)
//            Assets/Project/Art/Workers/_Preview/_sheet.png (контактный лист 9x3)
// Без внешних ассетов: свой мини-растеризатор треугольников, детерминированный (id -> одинаковый результат).
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TimberWings.EditorTools
{
    public enum PRole { Clerk, Lumberjack, Sawyer }
    public enum PRarity { Common, Uncommon, Rare, Legendary, Mythic }
    public enum PHair { Bald, Short, Long, Braid, Curly, Bun, Messy }
    public enum PBeard { None, Stubble, Full, Mustache, Long }
    public enum PHat { None, Cap, Beanie, TopHat, Kerchief, Goggles }
    public enum PAcc { None, Coin, Scales, Axe, Saw, Ruler, Pencil, Abacus, Cone }

    public sealed class PortraitSpec
    {
        public string id, name;
        public int age;
        public bool female;
        public PRole role;
        public PRarity rarity;
        public Color skin, hair, cloth, accent;
        public PHair hairStyle;
        public PBeard beard;
        public PHat hat;
        public PAcc acc;
        public bool glasses;
    }

    public static class WorkerPortraitGenerator
    {
        const string OutDir = "Assets/Project/Art/Workers/Portraits";
        const string PreviewDir = "Assets/Project/Art/Workers/_Preview";
        const int Size = 256;          // итоговый размер
        const int Work = 512;          // рабочий размер (2x суперсэмплинг)

        // ------------------------------------------------------------------ данные
        static Color C(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString("#" + hex, out c);
            return c;
        }

        static PortraitSpec P(string id, string name, int age, bool f, PRole role, PRarity r,
            string skin, string hair, PHair hs, PBeard b, PHat hat, string cloth, string accent,
            PAcc acc, bool glasses = false)
        {
            return new PortraitSpec
            {
                id = id, name = name, age = age, female = f, role = role, rarity = r,
                skin = C(skin), hair = C(hair), hairStyle = hs, beard = b, hat = hat,
                cloth = C(cloth), accent = C(accent), acc = acc, glasses = glasses
            };
        }

        public static List<PortraitSpec> GetSpecs()
        {
            const string S1 = "F2C7A5", S2 = "E0A982", S3 = "D9A07A", S4 = "C98F66";
            var L = new List<PortraitSpec>();
            // ---- Приказчики (жёлтый акцент)
            L.Add(P("clerk_nils", "Нильс Кошелёк", 24, false, PRole.Clerk, PRarity.Common, S1, "7A4B2A", PHair.Messy, PBeard.None, PHat.None, "5B4A3A", "E8B84A", PAcc.Pencil));
            L.Add(P("clerk_agata", "Агата Медяк", 31, true, PRole.Clerk, PRarity.Common, S2, "3B2A24", PHair.Braid, PBeard.None, PHat.None, "E7DCC5", "5E8C61", PAcc.Coin, true));
            L.Add(P("clerk_rurik", "Рюрик Грош", 45, false, PRole.Clerk, PRarity.Common, S3, "5A5A5A", PHair.Short, PBeard.Mustache, PHat.Cap, "6E6E8A", "E8B84A", PAcc.None));
            L.Add(P("clerk_sigrid", "Сигрид Ларсен", 38, true, PRole.Clerk, PRarity.Uncommon, S1, "C9A24A", PHair.Long, PBeard.None, PHat.None, "3F6FA8", "E8B84A", PAcc.Scales));
            L.Add(P("clerk_bjorn", "Бьорн Тарн", 52, false, PRole.Clerk, PRarity.Uncommon, S3, "8A8A8A", PHair.Bald, PBeard.Stubble, PHat.None, "7A6A55", "E8B84A", PAcc.Pencil));
            L.Add(P("clerk_elsa", "Эльза Звонкая", 29, true, PRole.Clerk, PRarity.Rare, S1, "C8552B", PHair.Curly, PBeard.None, PHat.None, "D9D2C0", "C23B3B", PAcc.Coin));
            L.Add(P("clerk_gustav", "Густав Пенс", 57, false, PRole.Clerk, PRarity.Rare, S2, "CFCFCF", PHair.Short, PBeard.Mustache, PHat.TopHat, "4A3A3A", "E8B84A", PAcc.Coin));
            L.Add(P("clerk_frida", "Фрида Счётная", 48, true, PRole.Clerk, PRarity.Legendary, S2, "6A6A74", PHair.Bun, PBeard.None, PHat.None, "1F2F4A", "F2B134", PAcc.Abacus, true));
            L.Add(P("clerk_brita", "Брита Золотые Весы", 61, true, PRole.Clerk, PRarity.Mythic, S3, "E6E6EE", PHair.Braid, PBeard.None, PHat.None, "E0A030", "F2D45C", PAcc.Scales));
            // ---- Дровосеки (зелёный / красная клетка)
            L.Add(P("lumber_pekka", "Пекка Топорик", 20, false, PRole.Lumberjack, PRarity.Common, S1, "B07A3A", PHair.Messy, PBeard.None, PHat.Beanie, "4F7A4F", "A8452E", PAcc.Axe));
            L.Add(P("lumber_martta", "Мартта Щепка", 27, true, PRole.Lumberjack, PRarity.Common, S2, "5A3A2A", PHair.Short, PBeard.None, PHat.Kerchief, "4F7A4F", "C23B3B", PAcc.Axe));
            L.Add(P("lumber_olle", "Олле Пенёк", 41, false, PRole.Lumberjack, PRarity.Common, S3, "4A3A2A", PHair.Short, PBeard.Full, PHat.None, "5A6A4A", "8A5A3A", PAcc.Axe));
            L.Add(P("lumber_knut", "Кнут Бук", 36, false, PRole.Lumberjack, PRarity.Uncommon, S1, "A8452E", PHair.Short, PBeard.Stubble, PHat.None, "C23B3B", "3A2A2A", PAcc.Axe));
            L.Add(P("lumber_aune", "Ауне Ветка", 33, true, PRole.Lumberjack, PRarity.Uncommon, S4, "2B2B33", PHair.Braid, PBeard.None, PHat.None, "3F8A4F", "5A3A2A", PAcc.Axe));
            L.Add(P("lumber_johanna", "Йоханна Корень", 39, true, PRole.Lumberjack, PRarity.Rare, S2, "7A4B2A", PHair.Bun, PBeard.None, PHat.None, "6A7A4A", "8A5A3A", PAcc.Cone));
            L.Add(P("lumber_einar", "Эйнар Жила", 50, false, PRole.Lumberjack, PRarity.Rare, S3, "9A9A9A", PHair.Messy, PBeard.Mustache, PHat.None, "5A5A4A", "F2B134", PAcc.Axe));
            L.Add(P("lumber_tor", "Тор Глыба", 44, false, PRole.Lumberjack, PRarity.Legendary, S2, "D49A3A", PHair.Long, PBeard.Long, PHat.None, "3F6A3F", "8A5A3A", PAcc.Axe));
            L.Add(P("lumber_holger", "Хольгер Кедровый", 70, false, PRole.Lumberjack, PRarity.Mythic, S3, "F0F0F0", PHair.Long, PBeard.Long, PHat.None, "6A4A2A", "3F8A4F", PAcc.Cone));
            // ---- Пильщики (синий / голубой)
            L.Add(P("saw_til", "Тиль Опилка", 22, false, PRole.Sawyer, PRarity.Common, S1, "8A5A2A", PHair.Messy, PBeard.None, PHat.Goggles, "4A6FA5", "D9D2C0", PAcc.Saw));
            L.Add(P("saw_helga", "Хельга Брусок", 35, true, PRole.Sawyer, PRarity.Common, S2, "3B2A24", PHair.Bun, PBeard.None, PHat.None, "4A6FA5", "D9D2C0", PAcc.Pencil));
            L.Add(P("saw_ivar", "Ивар Рейка", 47, false, PRole.Sawyer, PRarity.Common, S3, "5A5A5A", PHair.Short, PBeard.Mustache, PHat.None, "5A7AAA", "3A4A6A", PAcc.Saw));
            L.Add(P("saw_solveig", "Сольвейг Рубанок", 30, true, PRole.Sawyer, PRarity.Uncommon, S1, "C9A24A", PHair.Braid, PBeard.None, PHat.Beanie, "4A6FA5", "F2D45C", PAcc.Ruler));
            L.Add(P("saw_vilhelm", "Вильхельм Стамеска", 55, false, PRole.Sawyer, PRarity.Uncommon, S2, "8A8A8A", PHair.Short, PBeard.Stubble, PHat.None, "7A6A4A", "4A3A2A", PAcc.Ruler));
            L.Add(P("saw_ingeborg", "Ингеборг Зубец", 42, true, PRole.Sawyer, PRarity.Rare, S3, "3A2A2A", PHair.Braid, PBeard.None, PHat.None, "3F5A8A", "8A8A8A", PAcc.Saw));
            L.Add(P("saw_dagni", "Дагни Шлиф", 34, true, PRole.Sawyer, PRarity.Rare, S4, "2B2B33", PHair.Short, PBeard.None, PHat.Kerchief, "4A6FA5", "A58AC8", PAcc.Ruler));
            L.Add(P("saw_lars", "Ларс Пила", 52, false, PRole.Sawyer, PRarity.Legendary, S2, "B8B8C0", PHair.Short, PBeard.Full, PHat.None, "2F4F8A", "C0C0C8", PAcc.Saw));
            L.Add(P("saw_ingrid", "Ингрид Ровная", 55, true, PRole.Sawyer, PRarity.Mythic, S1, "C8CCD8", PHair.Braid, PBeard.None, PHat.None, "7A9AC8", "E0E6F0", PAcc.Ruler));
            return L;
        }

        static Color RarityColor(PRarity r)
        {
            switch (r)
            {
                case PRarity.Uncommon: return C("6DBE6A");
                case PRarity.Rare: return C("4A90D9");
                case PRarity.Legendary: return C("F2B134");
                case PRarity.Mythic: return C("A55CFF");
                default: return C("9AA0A6");
            }
        }

        static Color Shade(Color c, float s)
        {
            return new Color(Mathf.Clamp01(c.r * s), Mathf.Clamp01(c.g * s), Mathf.Clamp01(c.b * s), c.a);
        }

        static Color A(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        static int Hash(string t)
        {
            unchecked
            {
                int h = 17;
                foreach (char ch in t) h = h * 31 + ch;
                return h & 0x7fffffff;
            }
        }

        // ------------------------------------------------------------------ растеризатор
        sealed class Canvas
        {
            public readonly int W;
            readonly float k;
            readonly Color[] pix;

            public Canvas(int size)
            {
                W = size;
                k = size / 256f;
                pix = new Color[size * size];
            }

            void Put(int x, int y, Color c)
            {
                int i = y * W + x;
                if (c.a >= 0.999f) pix[i] = c;
                else pix[i] = Color.Lerp(pix[i], new Color(c.r, c.g, c.b, 1f), c.a);
            }

            public void Gradient(Color top, Color bottom)
            {
                for (int y = 0; y < W; y++)
                {
                    Color c = Color.Lerp(top, bottom, y / (float)(W - 1));
                    c.a = 1f;
                    for (int x = 0; x < W; x++) pix[y * W + x] = c;
                }
            }

            public void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
            {
                a *= k; b *= k; c *= k;
                int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
                int maxX = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
                int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
                int maxY = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
                float d = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                if (Mathf.Abs(d) < 1e-6f) return;
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float qx = x + 0.5f, qy = y + 0.5f;
                        float l1 = ((b.y - c.y) * (qx - c.x) + (c.x - b.x) * (qy - c.y)) / d;
                        float l2 = ((c.y - a.y) * (qx - c.x) + (a.x - c.x) * (qy - c.y)) / d;
                        float l3 = 1f - l1 - l2;
                        if (l1 >= 0f && l2 >= 0f && l3 >= 0f) Put(x, y, col);
                    }
                }
            }

            // веер треугольников с плоским «low-poly» затенением (свет сверху-слева)
            public void Fan(Vector2 center, IList<Vector2> pts, Color col, int seed)
            {
                int n = pts.Count;
                for (int i = 0; i < n; i++)
                {
                    Vector2 p0 = pts[i], p1 = pts[(i + 1) % n];
                    Vector2 m = (p0 + p1) * 0.5f - center;
                    float ang = Mathf.Atan2(m.y, m.x);
                    float jit = ((Mathf.Abs(i * 37 + seed) % 7) / 7f - 0.5f) * 0.06f;
                    float shade = 0.92f + 0.13f * Mathf.Cos(ang + 2.356f) + jit;
                    Tri(center, p0, p1, Shade(col, shade));
                }
            }

            public void Poly(IList<Vector2> pts, Color col, int seed = 0)
            {
                Vector2 c = Vector2.zero;
                for (int i = 0; i < pts.Count; i++) c += pts[i];
                c /= pts.Count;
                Fan(c, pts, col, seed);
            }

            public void Facet(Vector2 c, float rx, float ry, Color col, int seed, int seg = 12)
            {
                var rnd = new System.Random(seed);
                var pts = new Vector2[seg];
                for (int i = 0; i < seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    float j = 1f + ((float)rnd.NextDouble() * 2f - 1f) * 0.05f;
                    pts[i] = c + new Vector2(Mathf.Cos(a) * rx * j, Mathf.Sin(a) * ry * j);
                }
                Fan(c + new Vector2(-rx * 0.08f, -ry * 0.08f), pts, col, seed);
            }

            public void Line(Vector2 a, Vector2 b, float w, Color col)
            {
                Vector2 dir = (b - a).normalized;
                Vector2 n = new Vector2(-dir.y, dir.x) * (w * 0.5f);
                Tri(a + n, a - n, b + n, col);
                Tri(b + n, a - n, b - n, col);
            }

            public void Rect(float x0, float y0, float x1, float y1, Color col)
            {
                Tri(new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x0, y1), col);
                Tri(new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1), col);
            }

            public Color[] Downsample()
            {
                int s = W / 2;
                var res = new Color[s * s];
                for (int y = 0; y < s; y++)
                {
                    for (int x = 0; x < s; x++)
                    {
                        Color a = pix[(y * 2) * W + x * 2];
                        Color b = pix[(y * 2) * W + x * 2 + 1];
                        Color c = pix[(y * 2 + 1) * W + x * 2];
                        Color d = pix[(y * 2 + 1) * W + x * 2 + 1];
                        res[y * s + x] = (a + b + c + d) * 0.25f;
                    }
                }
                return res;
            }
        }

        // ------------------------------------------------------------------ рисование портрета
        static void Draw(Canvas cv, PortraitSpec s)
        {
            int seed = Hash(s.id);
            Color rc = RarityColor(s.rarity);
            Color skin = s.skin, skinD = Shade(skin, 0.84f), hair = s.hair;
            Color dark = C("2A2230"), wood = C("8A5A3A"), steel = C("A9AFB8"), gold = C("F2B134");

            // фон
            cv.Gradient(Shade(rc, 0.45f), Shade(rc, 0.95f));
            var rnd = new System.Random(seed);
            for (int gx = 0; gx < 8; gx++)
            {
                for (int gy = 0; gy < 8; gy++)
                {
                    float x0 = gx * 32, y0 = gy * 32, x1 = x0 + 32, y1 = y0 + 32;
                    float r1 = ((float)rnd.NextDouble() - 0.5f) * 0.14f;
                    float r2 = ((float)rnd.NextDouble() - 0.5f) * 0.14f;
                    cv.Tri(new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x0, y1), r1 > 0 ? new Color(1, 1, 1, r1) : new Color(0, 0, 0, -r1));
                    cv.Tri(new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1), r2 > 0 ? new Color(1, 1, 1, r2) : new Color(0, 0, 0, -r2));
                }
            }
            if (s.rarity == PRarity.Mythic)
            {
                Color ray = A(Color.Lerp(rc, Color.white, 0.6f), 0.16f);
                Vector2 o = new Vector2(128, 120);
                for (int i = 0; i < 14; i++)
                {
                    float a0 = i * Mathf.PI * 2f / 14f, a1 = a0 + Mathf.PI / 14f;
                    cv.Tri(o, o + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * 300f, o + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * 300f, ray);
                }
            }

            // задние волосы
            if (s.hairStyle == PHair.Long) cv.Facet(new Vector2(128, 132), 58, 70, hair, seed + 3, 14);
            if (s.hairStyle == PHair.Braid)
            {
                cv.Facet(new Vector2(128, 112), 52, 58, hair, seed + 3, 14);
                for (int i = 0; i < 4; i++)
                    cv.Facet(new Vector2(176 + (i % 2) * 4, 176 + i * 17), 9, 10, Shade(hair, 0.95f), seed + 10 + i, 8);
            }
            if (s.hairStyle == PHair.Bun) cv.Facet(new Vector2(128, 52), 18, 16, hair, seed + 4, 10);

            // задние аксессуары
            if (s.acc == PAcc.Axe)
            {
                cv.Line(new Vector2(196, 252), new Vector2(216, 126), 8, wood);
                cv.Poly(new[] { new Vector2(204, 132), new Vector2(236, 118), new Vector2(240, 160), new Vector2(210, 156) }, steel, seed);
                cv.Line(new Vector2(236, 118), new Vector2(240, 160), 3, C("E8EDF2"));
            }
            if (s.acc == PAcc.Saw)
            {
                Vector2 b0 = new Vector2(188, 250), b1 = new Vector2(222, 138);
                cv.Poly(new[] { b0, b1, b1 + new Vector2(10, 4), b0 + new Vector2(12, 4) }, steel, seed);
                for (int i = 0; i < 7; i++)
                {
                    float t = i / 7f;
                    Vector2 p = Vector2.Lerp(b0, b1, t);
                    cv.Tri(p, p + new Vector2(-6, -3), Vector2.Lerp(b0, b1, t + 0.07f), Shade(steel, 0.8f));
                }
                cv.Facet(new Vector2(227, 134), 9, 9, wood, seed + 5, 8);
            }

            // торс
            cv.Poly(new[] { new Vector2(36, 256), new Vector2(46, 214), new Vector2(92, 190), new Vector2(164, 190), new Vector2(210, 214), new Vector2(220, 256) }, s.cloth, seed + 6);
            cv.Poly(new[] { new Vector2(98, 196), new Vector2(158, 196), new Vector2(170, 256), new Vector2(86, 256) }, s.accent, seed + 7);
            cv.Poly(new[] { new Vector2(112, 190), new Vector2(144, 190), new Vector2(128, 214) }, skinD, seed + 8);
            // шея
            cv.Poly(new[] { new Vector2(112, 156), new Vector2(144, 156), new Vector2(146, 194), new Vector2(110, 194) }, skinD, seed + 9);

            // уши и голова
            cv.Facet(new Vector2(82, 122), 8, 11, skinD, seed + 11, 8);
            cv.Facet(new Vector2(174, 122), 8, 11, skinD, seed + 12, 8);
            cv.Facet(new Vector2(128, 118), s.female ? 43 : 47, 54, skin, seed + 1, 14);

            // щёки
            if (s.female || s.age < 30)
            {
                Color blush = new Color(0.9f, 0.4f, 0.4f, 0.30f);
                cv.Facet(new Vector2(100, 138), 9, 6, blush, seed + 13, 8);
                cv.Facet(new Vector2(156, 138), 9, 6, blush, seed + 14, 8);
            }
            // нос
            cv.Poly(new[] { new Vector2(128, 124), new Vector2(121, 142), new Vector2(135, 142) }, Shade(skin, 0.88f), seed + 15);

            // очки (оправа и стекло под глазами)
            if (s.glasses)
            {
                Color rim = s.rarity >= PRarity.Legendary ? gold : dark;
                cv.Facet(new Vector2(113, 120), 16, 14, rim, seed + 16, 10);
                cv.Facet(new Vector2(143, 120), 16, 14, rim, seed + 17, 10);
                cv.Facet(new Vector2(113, 120), 12.5f, 10.5f, Shade(skin, 1.04f), seed + 18, 10);
                cv.Facet(new Vector2(143, 120), 12.5f, 10.5f, Shade(skin, 1.04f), seed + 19, 10);
                cv.Line(new Vector2(126, 118), new Vector2(130, 118), 3, rim);
            }
            // глаза
            cv.Facet(new Vector2(113, 120), 5, 6, dark, seed + 20, 8);
            cv.Facet(new Vector2(143, 120), 5, 6, dark, seed + 21, 8);
            cv.Facet(new Vector2(114.5f, 118), 1.6f, 1.6f, Color.white, seed + 22, 6);
            cv.Facet(new Vector2(144.5f, 118), 1.6f, 1.6f, Color.white, seed + 23, 6);
            // брови
            Color brow = Shade(hair, 0.75f);
            cv.Line(new Vector2(101, 108), new Vector2(123, 106), 4, brow);
            cv.Line(new Vector2(133, 106), new Vector2(155, 108), 4, brow);
            // морщины
            if (s.age >= 50)
            {
                Color wr = Shade(skin, 0.78f);
                cv.Line(new Vector2(100, 134), new Vector2(108, 136), 2, wr);
                cv.Line(new Vector2(148, 136), new Vector2(156, 134), 2, wr);
                cv.Line(new Vector2(112, 94), new Vector2(144, 94), 2, wr);
            }

            // борода
            Color beardCol = Shade(hair, 0.95f);
            if (s.beard == PBeard.Stubble)
            {
                cv.Poly(new[] { new Vector2(88, 134), new Vector2(94, 160), new Vector2(112, 178), new Vector2(128, 184), new Vector2(144, 178), new Vector2(162, 160), new Vector2(168, 134), new Vector2(150, 148), new Vector2(128, 152), new Vector2(106, 148) }, A(hair, 0.35f), seed + 24);
            }
            if (s.beard == PBeard.Full || s.beard == PBeard.Long)
            {
                cv.Fan(new Vector2(128, 150), new[] { new Vector2(82, 122), new Vector2(86, 152), new Vector2(102, 178), new Vector2(128, 188), new Vector2(154, 178), new Vector2(170, 152), new Vector2(174, 122), new Vector2(158, 138), new Vector2(142, 146), new Vector2(128, 148), new Vector2(114, 146), new Vector2(98, 138) }, beardCol, seed + 25);
            }
            if (s.beard == PBeard.Long)
            {
                cv.Poly(new[] { new Vector2(100, 176), new Vector2(156, 176), new Vector2(148, 214), new Vector2(128, 238), new Vector2(108, 214) }, Shade(beardCol, 0.96f), seed + 26);
            }
            if (s.beard == PBeard.Mustache)
            {
                cv.Poly(new[] { new Vector2(128, 142), new Vector2(100, 148), new Vector2(94, 160), new Vector2(112, 152), new Vector2(128, 150) }, beardCol, seed + 27);
                cv.Poly(new[] { new Vector2(128, 142), new Vector2(156, 148), new Vector2(162, 160), new Vector2(144, 152), new Vector2(128, 150) }, beardCol, seed + 28);
            }
            // рот
            Color mouth = C("7A2E2E");
            cv.Line(new Vector2(117, 153), new Vector2(128, 156), 3.5f, mouth);
            cv.Line(new Vector2(128, 156), new Vector2(139, 153), 3.5f, mouth);

            // передние волосы или чубчики под шапкой
            bool covered = s.hat == PHat.Cap || s.hat == PHat.Beanie || s.hat == PHat.Kerchief || s.hat == PHat.TopHat;
            if (covered || s.hairStyle == PHair.Bald)
            {
                if (s.hairStyle != PHair.Bald || !covered)
                {
                    cv.Facet(new Vector2(84, 112), 9, 15, hair, seed + 30, 8);
                    cv.Facet(new Vector2(172, 112), 9, 15, hair, seed + 31, 8);
                }
            }
            else
            {
                switch (s.hairStyle)
                {
                    case PHair.Curly:
                        cv.Facet(new Vector2(128, 90), 40, 24, hair, seed + 32, 12);
                        for (int i = 0; i < 9; i++)
                        {
                            float a = Mathf.PI + Mathf.PI * i / 8f;
                            cv.Facet(new Vector2(128 + Mathf.Cos(a) * 48, 98 + Mathf.Sin(a) * 40), 15, 15, hair, seed + 40 + i, 9);
                        }
                        break;
                    default:
                        HairCap(cv, 128, 100, 50, 38, 94, hair, seed + 33, s.hairStyle == PHair.Messy);
                        if (s.hairStyle == PHair.Long)
                        {
                            cv.Poly(new[] { new Vector2(78, 96), new Vector2(70, 176), new Vector2(88, 190), new Vector2(92, 110) }, hair, seed + 34);
                            cv.Poly(new[] { new Vector2(178, 96), new Vector2(186, 176), new Vector2(168, 190), new Vector2(164, 110) }, hair, seed + 35);
                        }
                        break;
                }
            }

            // головные уборы
            switch (s.hat)
            {
                case PHat.Cap:
                    HairCap(cv, 128, 100, 52, 38, 92, s.accent, seed + 50, false);
                    cv.Poly(new[] { new Vector2(92, 90), new Vector2(176, 90), new Vector2(194, 104), new Vector2(98, 100) }, Shade(s.accent, 0.75f), seed + 51);
                    break;
                case PHat.Beanie:
                    HairCap(cv, 128, 100, 53, 40, 94, s.accent, seed + 52, false);
                    cv.Poly(new[] { new Vector2(76, 92), new Vector2(180, 92), new Vector2(182, 104), new Vector2(74, 104) }, Shade(s.accent, 0.8f), seed + 53);
                    cv.Facet(new Vector2(128, 56), 9, 9, Shade(s.accent, 1.1f), seed + 54, 8);
                    break;
                case PHat.Kerchief:
                    HairCap(cv, 128, 100, 52, 38, 92, s.accent, seed + 55, false);
                    cv.Line(new Vector2(90, 76), new Vector2(166, 76), 3, Shade(s.accent, 0.7f));
                    cv.Line(new Vector2(84, 88), new Vector2(172, 88), 3, Shade(s.accent, 0.7f));
                    cv.Poly(new[] { new Vector2(172, 92), new Vector2(196, 84), new Vector2(194, 104) }, Shade(s.accent, 0.9f), seed + 56);
                    cv.Poly(new[] { new Vector2(172, 94), new Vector2(198, 106), new Vector2(182, 114) }, Shade(s.accent, 0.8f), seed + 57);
                    break;
                case PHat.TopHat:
                    {
                        Color hatCol = Shade(s.cloth, 0.8f);
                        cv.Facet(new Vector2(128, 84), 68, 11, hatCol, seed + 58, 14);
                        cv.Poly(new[] { new Vector2(96, 84), new Vector2(98, 28), new Vector2(158, 28), new Vector2(160, 84) }, hatCol, seed + 59);
                        cv.Facet(new Vector2(128, 28), 31, 7, Shade(hatCol, 1.15f), seed + 60, 12);
                        cv.Poly(new[] { new Vector2(96, 72), new Vector2(160, 72), new Vector2(160, 82), new Vector2(96, 82) }, s.accent, seed + 61);
                        break;
                    }
                case PHat.Goggles:
                    {
                        Color strap = C("2A2A33");
                        cv.Line(new Vector2(80, 86), new Vector2(176, 86), 6, strap);
                        cv.Facet(new Vector2(110, 84), 16, 14, strap, seed + 62, 10);
                        cv.Facet(new Vector2(146, 84), 16, 14, strap, seed + 63, 10);
                        cv.Facet(new Vector2(110, 84), 12.5f, 10.5f, C("9ED6E8"), seed + 64, 10);
                        cv.Facet(new Vector2(146, 84), 12.5f, 10.5f, C("9ED6E8"), seed + 65, 10);
                        break;
                    }
            }

            // передние аксессуары
            Color cord = C("6A5A3A");
            if (s.acc == PAcc.Coin)
            {
                cv.Line(new Vector2(114, 192), new Vector2(128, 212), 2.5f, cord);
                cv.Line(new Vector2(142, 192), new Vector2(128, 212), 2.5f, cord);
                cv.Facet(new Vector2(128, 222), 12, 12, gold, seed + 70, 10);
                cv.Facet(new Vector2(128, 222), 7, 7, Shade(gold, 0.82f), seed + 71, 8);
            }
            if (s.acc == PAcc.Scales)
            {
                cv.Line(new Vector2(114, 192), new Vector2(128, 212), 2.5f, cord);
                cv.Line(new Vector2(142, 192), new Vector2(128, 212), 2.5f, cord);
                if (s.rarity == PRarity.Mythic) cv.Facet(new Vector2(128, 228), 26, 18, new Color(1, 1, 1, 0.2f), seed + 72, 12);
                cv.Line(new Vector2(128, 212), new Vector2(128, 224), 2.5f, gold);
                cv.Line(new Vector2(110, 224), new Vector2(146, 224), 3, gold);
                cv.Tri(new Vector2(104, 228), new Vector2(118, 228), new Vector2(111, 242), gold);
                cv.Tri(new Vector2(138, 228), new Vector2(152, 228), new Vector2(145, 242), gold);
            }
            if (s.acc == PAcc.Cone)
            {
                cv.Line(new Vector2(114, 192), new Vector2(128, 212), 2.5f, cord);
                cv.Line(new Vector2(142, 192), new Vector2(128, 212), 2.5f, cord);
                cv.Facet(new Vector2(128, 226), 10, 15, C("7A4B2A"), seed + 73, 8);
                cv.Facet(new Vector2(128, 230), 6, 8, C("5A3A1E"), seed + 74, 6);
            }
            if (s.acc == PAcc.Ruler)
            {
                cv.Poly(new[] { new Vector2(90, 232), new Vector2(168, 220), new Vector2(171, 232), new Vector2(93, 244) }, C("E2C27A"), seed + 75);
                for (int i = 0; i < 7; i++)
                {
                    float t = i / 7f;
                    Vector2 a = Vector2.Lerp(new Vector2(94, 232), new Vector2(166, 221), t + 0.07f);
                    cv.Line(a, a + new Vector2(0.5f, 6), 1.5f, C("7A5A2A"));
                }
            }
            if (s.acc == PAcc.Abacus)
            {
                cv.Poly(new[] { new Vector2(140, 218), new Vector2(226, 218), new Vector2(226, 258), new Vector2(140, 258) }, wood, seed + 76);
                cv.Rect(146, 224, 220, 258, C("F3E7C8"));
                Color[] bc = { s.accent, C("C23B3B"), C("4A90D9") };
                for (int r = 0; r < 3; r++)
                {
                    float y = 234 + r * 10;
                    cv.Line(new Vector2(148, y), new Vector2(218, y), 2, C("5A3A1E"));
                    for (int i = 0; i < 4; i++)
                        cv.Facet(new Vector2(158 + i * 14 + r * 3, y), 5, 5, bc[r], seed + 80 + r * 4 + i, 6);
                }
            }
            if (s.acc == PAcc.Pencil)
            {
                cv.Line(new Vector2(176, 96), new Vector2(188, 126), 4.5f, C("F2D45C"));
                cv.Tri(new Vector2(185.5f, 125), new Vector2(190.5f, 127), new Vector2(188, 136), C("E8C9A0"));
                cv.Line(new Vector2(176, 96), new Vector2(177, 99), 4.5f, C("E86A6A"));
            }

            // звёзды легендарных
            if (s.rarity >= PRarity.Legendary)
            {
                Sparkle(cv, new Vector2(32, 38), 12, Color.Lerp(rc, Color.white, 0.5f));
                Sparkle(cv, new Vector2(226, 52), 9, Color.Lerp(rc, Color.white, 0.5f));
                if (s.rarity == PRarity.Mythic) Sparkle(cv, new Vector2(40, 190), 8, Color.white);
            }

            // рамка
            float f = s.rarity == PRarity.Mythic ? 11 : 9;
            cv.Rect(0, 0, 256, f, rc);
            cv.Rect(0, 256 - f, 256, 256, rc);
            cv.Rect(0, 0, f, 256, rc);
            cv.Rect(256 - f, 0, 256, 256, rc);
            Color inner = Shade(rc, 0.65f);
            cv.Rect(f, f, 256 - f, f + 3, inner);
            cv.Rect(f, 256 - f - 3, 256 - f, 256 - f, inner);
            cv.Rect(f, f, f + 3, 256 - f, inner);
            cv.Rect(256 - f - 3, f, 256 - f, 256 - f, inner);
            if (s.rarity == PRarity.Mythic)
            {
                Color glow = A(Color.Lerp(rc, Color.white, 0.7f), 0.8f);
                cv.Rect(f + 3, f + 3, 256 - f - 3, f + 5, glow);
                cv.Rect(f + 3, 256 - f - 5, 256 - f - 3, 256 - f - 3, glow);
                cv.Rect(f + 3, f + 3, f + 5, 256 - f - 3, glow);
                cv.Rect(256 - f - 5, f + 3, 256 - f - 3, 256 - f - 3, glow);
            }
        }

        static void Sparkle(Canvas cv, Vector2 c, float r, Color col)
        {
            cv.Poly(new[] { c + new Vector2(0, -r), c + new Vector2(r * 0.28f, -r * 0.28f), c + new Vector2(r, 0), c + new Vector2(r * 0.28f, r * 0.28f), c + new Vector2(0, r), c + new Vector2(-r * 0.28f, r * 0.28f), c + new Vector2(-r, 0), c + new Vector2(-r * 0.28f, -r * 0.28f) }, col, 1);
        }

        static void HairCap(Canvas cv, float cx, float cy, float rx, float ry, float hairline, Color col, int seed, bool spiky)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= 10; i++)
            {
                float a = Mathf.PI + Mathf.PI * i / 10f;
                pts.Add(new Vector2(cx + Mathf.Cos(a) * rx * 1.06f, cy + Mathf.Sin(a) * ry * 1.06f));
            }
            pts.Add(new Vector2(cx + rx * 0.7f, hairline));
            pts.Add(new Vector2(cx + rx * 0.35f, hairline - 8));
            pts.Add(new Vector2(cx, hairline + 2));
            pts.Add(new Vector2(cx - rx * 0.35f, hairline - 8));
            pts.Add(new Vector2(cx - rx * 0.7f, hairline));
            cv.Fan(new Vector2(cx, cy - 26), pts, col, seed);
            if (spiky)
            {
                float top = cy - ry * 1.06f;
                for (int i = 0; i < 5; i++)
                {
                    float x = cx - 30 + i * 15;
                    cv.Tri(new Vector2(x - 8, top + 6), new Vector2(x + 8, top + 6), new Vector2(x + (i - 2) * 4, top - 12), Shade(col, 1.02f));
                }
            }
        }

        // ------------------------------------------------------------------ экспорт
        static Color[] RenderPixels(PortraitSpec s)
        {
            var cv = new Canvas(Work);
            Draw(cv, s);
            Color[] px = cv.Downsample();   // строки сверху вниз
            const int R = 26;                // скругление углов
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = 0, dy = 0;
                    if (x < R) dx = R - x; else if (x >= Size - R) dx = x - (Size - R - 1);
                    if (y < R) dy = R - y; else if (y >= Size - R) dy = y - (Size - R - 1);
                    if (dx > 0 && dy > 0 && dx * dx + dy * dy > R * R)
                        px[y * Size + x] = new Color(0, 0, 0, 0);
                }
            }
            return px;
        }

        static Color[] FlipY(Color[] src, int w, int h)
        {
            var res = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    res[(h - 1 - y) * w + x] = src[y * w + x];
            return res;
        }

        static void SavePng(string path, Color[] topDown, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels(FlipY(topDown, w, h));
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }

        [MenuItem("Tools/BuilderSim/Рабочие/Сгенерировать портреты", false, 511)]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(OutDir);
            Directory.CreateDirectory(PreviewDir);
            var specs = GetSpecs();
            var rendered = new List<Color[]>();
            try
            {
                for (int i = 0; i < specs.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Портреты рабочих", specs[i].name, i / (float)specs.Count);
                    Color[] px = RenderPixels(specs[i]);
                    rendered.Add(px);
                    SavePng(OutDir + "/" + specs[i].id + ".png", px, Size, Size);
                }

                // контактный лист: 9 колонок x 3 строки (роль по строкам, редкость по возрастанию)
                int cols = 9, rows = 3;
                int sw = cols * Size, sh = rows * Size;
                var sheet = new Color[sw * sh];
                for (int i = 0; i < rendered.Count; i++)
                {
                    int cx = (i % cols) * Size, cy = (i / cols) * Size;
                    for (int y = 0; y < Size; y++)
                        for (int x = 0; x < Size; x++)
                            sheet[(cy + y) * sw + cx + x] = rendered[i][y * Size + x];
                }
                SavePng(PreviewDir + "/_sheet.png", sheet, sw, sh);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            foreach (var s in specs)
            {
                string path = OutDir + "/" + s.id + ".png";
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.textureCompression = TextureImporterCompression.Compressed;
                ti.filterMode = FilterMode.Bilinear;
                ti.SaveAndReimport();
            }
            Debug.Log("[Workers] Портреты готовы: " + specs.Count + " шт. Контактный лист: " + PreviewDir + "/_sheet.png");
        }
    }
}
#endif
