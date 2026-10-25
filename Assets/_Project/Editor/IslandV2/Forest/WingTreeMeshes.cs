#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/WingTreeMeshes.cs (Editor)  НОВЫЙ ФАЙЛ
// Процедурные модели «кулисных» деревьев: массовые нерубимые деревья того же стиля, что хранители (мультяшные, пастельные, облачные), но дешёвые.
// Только геометрия; материалы, префабы и расстановка в ForestWings.cs. Строит через тот же TreeMB, что SacredTreeMeshes (flat shading, пивот у корней,
// Y вверх, метры, до 4 сабмешей). Детерминировано: форма зависит только от (вид, форма 0..2, lod).
//
// 7 видов, у каждого 3 формы (разные наклон ствола, число и посадка пухов, ярусы; seed зашит в номер формы):
//   Cloud     Облачко-зонтик    плоская пушистая крона, белая с лиловым низом            ~8.9 м   сабмеши: 0 ствол, 1 белое, 2 розовые пухи, 3 лиловый низ
//   Cotton    Сахарная вата     круглая розово-сиреневая крона на тонком стволе           ~8.5 м   0 ствол, 1 розовое, 2 сиреневое, 3 тёмно-сиреневый низ
//   Willow    Радужная ива      купол и длинные свисающие пряди розовый/мятный/голубой    ~9.2 м   0 ствол, 1 розовая (купол + пряди), 2 мятные пряди, 3 голубые пряди и низ купола
//   Mushroom  Мятный гриб       шляпа с белыми пятнами на кремовой ножке                  ~5.5 м   0 ножка, 1 мятная шляпа, 2 белые пятна и воротничок, 3 светлый низ шляпы
//   Moon      Лунное дерево     бледно-голубая крона, жемчужные шарики-плоды на нитях     ~9.1 м   0 ствол и нити, 1 голубая крона, 2 шарики (светлые, без градиента), 3 васильковый низ
//   Ribbon    Ленточное         спиральный ствол с розовой лентой, крона тремя ярусами    ~11.2 м  0 ствол, 1 сиреневые ярусы, 2 розовые ярусы и лента, 3 голубой низ
//   Bush      Облачный куст     карлик для подлеска у групп: кучка пухов                  ~1.4 м   0 не используется, 1 белое, 2 розовое, 3 мятное (низа нет)
// Размеры в игре 4-14 м получаются масштабом корня (ForestWings: 0.72-1.25, с зажимом по высоте); куст 1-2 м.
// LOD0: шары по 80 граней (до ~1000 треугольников на дерево), LOD1: шары по 20 граней, меньше прядей и пятен (~350).
//
// ЕДИНЫЙ ЗНАК «НЕ РУБИТСЯ» (общий язык с хранителями, но дешёвый): Wreath(), венок из камней у корней. Низкие камни и стоячие камни-стелы,
// на каждом втором-третьем бирюзовая руна (сабмеш 1 = тот же материал Sacred_Rune, что у хранителей). Один меш на вид, 170-250 треугольников, без света.
using UnityEngine;

namespace IslandV2
{
    public enum WingKind { Cloud = 0, Cotton = 1, Willow = 2, Mushroom = 3, Moon = 4, Ribbon = 5, Bush = 6 }

    public static class WingMeshes
    {
        public const int Variants = 3;
        public static readonly WingKind[] AllKinds = { WingKind.Cloud, WingKind.Cotton, WingKind.Willow, WingKind.Mushroom, WingKind.Moon, WingKind.Ribbon, WingKind.Bush };

        // ============================================================ габариты видов (читает ForestWings)
        public static string Id(WingKind k) { return k.ToString(); }

        public static string Title(WingKind k)
        {
            switch (k)
            {
                case WingKind.Cloud: return "Облачко-зонтик";
                case WingKind.Cotton: return "Сахарная вата";
                case WingKind.Willow: return "Радужная ива";
                case WingKind.Mushroom: return "Мятный гриб-дерево";
                case WingKind.Moon: return "Лунное дерево";
                case WingKind.Ribbon: return "Ленточное дерево";
                default: return "Облачный куст";
            }
        }

        // высота меша при масштабе 1, м (замерено по сгенерированным мешам)
        public static float Height(WingKind k)
        {
            switch (k)
            {
                case WingKind.Cloud: return 8.9f;
                case WingKind.Cotton: return 8.5f;
                case WingKind.Willow: return 9.2f;
                case WingKind.Mushroom: return 5.5f;
                case WingKind.Moon: return 9.1f;
                case WingKind.Ribbon: return 11.2f;
                default: return 1.4f;
            }
        }
        // полуширина кроны при масштабе 1 (самая широкая из трёх форм), м
        public static float CrownR(WingKind k)
        {
            switch (k)
            {
                case WingKind.Cloud: return 5.2f;
                case WingKind.Cotton: return 4.1f;
                case WingKind.Willow: return 4.6f;
                case WingKind.Mushroom: return 3.4f;
                case WingKind.Moon: return 3.6f;
                case WingKind.Ribbon: return 3.8f;
                default: return 1.3f;
            }
        }
        // радиус венка из камней, м (при масштабе 1)
        public static float RingR(WingKind k)
        {
            switch (k)
            {
                case WingKind.Cloud: return 2.0f;
                case WingKind.Cotton: return 1.7f;
                case WingKind.Willow: return 2.2f;
                case WingKind.Mushroom: return 1.8f;
                case WingKind.Moon: return 2.0f;
                case WingKind.Ribbon: return 2.4f;
                default: return 1.55f;
            }
        }
        // сплошной ствол для капсульного коллайдера: радиус, высота (у куста ствола нет: 0)
        public static float TrunkR(WingKind k)
        {
            switch (k)
            {
                case WingKind.Cloud: return 0.30f;
                case WingKind.Cotton: return 0.26f;
                case WingKind.Willow: return 0.34f;
                case WingKind.Mushroom: return 0.46f;
                case WingKind.Moon: return 0.28f;
                case WingKind.Ribbon: return 0.40f;
                default: return 0f;
            }
        }
        public static float TrunkH(WingKind k)
        {
            switch (k)
            {
                case WingKind.Cloud: return 5.0f;
                case WingKind.Cotton: return 3.4f;
                case WingKind.Willow: return 5.8f;
                case WingKind.Mushroom: return 3.0f;
                case WingKind.Moon: return 4.3f;
                case WingKind.Ribbon: return 6.0f;
                default: return 0f;
            }
        }
        // отбрасывает ли тень (куст и грибок нет: экономия теневого прохода)
        public static bool CastsShadow(WingKind k) { return k != WingKind.Bush && k != WingKind.Mushroom; }

        public static Mesh Body(WingKind k, int v, int lod, string name)
        {
            v = Mathf.Clamp(v, 0, Variants - 1);
            switch (k)
            {
                case WingKind.Cloud: return Cloud(v, lod, name);
                case WingKind.Cotton: return Cotton(v, lod, name);
                case WingKind.Willow: return Willow(v, lod, name);
                case WingKind.Mushroom: return Mushroom(v, lod, name);
                case WingKind.Moon: return Moon(v, lod, name);
                case WingKind.Ribbon: return Ribbon(v, lod, name);
                default: return Bush(v, lod, name);
            }
        }

        // ============================================================ общие помощники (как в SacredMeshes)
        struct Tr { public float h, r0, r1, lean, twist, ph; }

        static Vector3 TPos(Tr s, float t)
        {
            return new Vector3(Mathf.Sin(t * s.twist + s.ph) * s.lean * t, Mathf.Lerp(-0.25f, s.h, t), Mathf.Cos(t * s.twist * 0.8f + s.ph + 0.6f) * s.lean * 0.7f * t);
        }
        static float TRad(Tr s, float t) { return Mathf.Lerp(s.r0, s.r1, t) * (1f + 0.55f * Mathf.Exp(-t * 9f)); }
        static void Trunk(TreeMB b, int sub, Tr s, int segs, int sides) { b.TubeF(sub, t => TPos(s, t), t => TRad(s, t), segs, sides, true); }

        static Vector3 Az(float deg) { float a = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)); }

        static Vector3[] Limb(Vector3 start, Vector3 dir, float len, float rise, int n)
        {
            var p = new Vector3[n];
            Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                p[i] = start + dir * (len * t) + Vector3.up * (rise * Mathf.Pow(t, 1.6f)) + side * (Mathf.Sin(t * 5f + dir.x * 3f) * 0.25f * t);
            }
            return p;
        }

        static void TubeLimb(TreeMB b, int sub, Vector3[] p, float r0, float r1, int sides)
        {
            var r = new float[p.Length];
            for (int i = 0; i < r.Length; i++) r[i] = Mathf.Lerp(r0, r1, i / (r.Length - 1f));
            b.Tube(sub, p, r, sides, true);
        }

        // ============================================================ 1. ОБЛАЧКО-ЗОНТИК
        static Mesh Cloud(int V, int lod, string name)
        {
            var b = new TreeMB();
            int lv = lod == 0 ? 1 : 0, sd = lod == 0 ? 6 : 5, sb = 100 + V * 40;
            var s = new Tr { h = 5.4f, r0 = 0.34f, r1 = 0.19f, lean = new[] { 0.35f, 0.65f, 0.15f }[V], twist = 4.0f, ph = 0.5f + V * 1.9f };
            Trunk(b, 0, s, 5, sd);
            Vector3 top = TPos(s, 1f);
            for (int k = 0; k < 3; k++)
                TubeLimb(b, 0, Limb(top + new Vector3(0f, -0.5f, 0f), Az(k * 120f + 25f + V * 40f), 1.8f, 1.5f, 4), 0.10f, 0.05f, sd);

            Vector3 cc = top + new Vector3(0f, 1.7f, 0f);
            float wide = new[] { 3.0f, 2.7f, 3.3f }[V];
            b.Blob(1, cc + new Vector3(0f, 0.1f, 0f), new Vector3(wide, 0.95f, wide), sb, lv, 0.16f, 3);
            int nr = new[] { 6, 5, 7 }[V];
            for (int i = 0; i < nr; i++)
            {
                float a = i / (float)nr * Mathf.PI * 2f + 0.3f + V;
                float k = 0.9f + 0.3f * TreeMB.H(sb + 10, i);
                Vector3 c = cc + new Vector3(Mathf.Cos(a) * wide * 0.92f, -0.45f + (TreeMB.H(sb + 11, i) - 0.5f) * 0.5f, Mathf.Sin(a) * wide * 0.92f);
                b.Blob(i % 3 == 0 ? 2 : 1, c, new Vector3(1.35f * k, 0.85f * k, 1.35f * k), sb + 20 + i, lv, 0.16f, 3);
            }
            for (int i = 0; i < 2; i++)
            {
                float a = i * 3.1f + 1.0f + V;
                b.Blob(i == 1 ? 2 : 1, cc + new Vector3(Mathf.Cos(a) * 0.9f, 0.95f, Mathf.Sin(a) * 0.9f), new Vector3(1.1f, 0.75f, 1.1f), sb + 40 + i, lv, 0.16f, 3);
            }
            // облачка-спутники
            for (int i = 0; i < 2; i++)
            {
                float a = i * 2.9f + 0.5f + V * 1.3f, r = wide + 1.2f + 0.4f * TreeMB.H(sb + 50, i);
                b.Blob(1, cc + new Vector3(Mathf.Cos(a) * r, -1.0f + i * 1.1f, Mathf.Sin(a) * r), new Vector3(0.5f, 0.34f, 0.5f), sb + 60 + i, 0, 0.16f, 3);
            }
            return b.Build(name);
        }

        // ============================================================ 2. САХАРНАЯ ВАТА
        static Mesh Cotton(int V, int lod, string name)
        {
            var b = new TreeMB();
            int lv = lod == 0 ? 1 : 0, sd = lod == 0 ? 6 : 5, sb = 200 + V * 40;
            var s = new Tr { h = 3.7f, r0 = 0.30f, r1 = 0.17f, lean = new[] { 0.5f, 0.2f, 0.85f }[V], twist = 3.4f, ph = 1.1f + V * 1.5f };
            Trunk(b, 0, s, 5, sd);
            Vector3 top = TPos(s, 1f);
            Vector3 cc = top + new Vector3(0f, 2.1f, 0f);
            b.Blob(1, cc, new Vector3(2.5f, 2.2f, 2.5f), sb, lv, 0.18f, 3);
            int nl = new[] { 5, 4, 6 }[V];
            for (int i = 0; i < nl; i++)
            {
                float a = i / (float)nl * Mathf.PI * 2f + 0.7f * V + 0.4f;
                Vector3 c = cc + new Vector3(Mathf.Cos(a) * 1.8f, (TreeMB.H(sb + 5, i) - 0.5f) * 1.2f, Mathf.Sin(a) * 1.8f);
                float k = 0.9f + 0.25f * TreeMB.H(sb + 6, i);
                b.Blob(i % 2 == 0 ? 2 : 1, c, new Vector3(1.05f * k, 0.95f * k, 1.05f * k), sb + 10 + i, lv, 0.18f, 3);
            }
            for (int i = 0; i < 2; i++)
            {
                float a = i * 3.4f + 0.3f + V;
                b.Blob(i == 0 ? 2 : 1, cc + new Vector3(Mathf.Cos(a) * 0.55f, 1.75f, Mathf.Sin(a) * 0.55f), new Vector3(1.0f, 0.85f, 1.0f), sb + 20 + i, lv, 0.18f, 3);
            }
            // летающие комочки ваты
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.2f + 0.9f + V, r = 2.9f + 0.4f * TreeMB.H(sb + 30, i), q = 0.36f + 0.08f * i;
                b.Blob(i != 1 ? 1 : 2, cc + new Vector3(Mathf.Cos(a) * r, -0.9f + i * 0.9f, Mathf.Sin(a) * r), new Vector3(q, 0.32f, q), sb + 35 + i, 0, 0.16f, 3);
            }
            return b.Build(name);
        }

        // ============================================================ 3. РАДУЖНАЯ ИВА
        static Mesh Willow(int V, int lod, string name)
        {
            var b = new TreeMB();
            int lv = lod == 0 ? 1 : 0, sd = lod == 0 ? 6 : 5, sb = 300 + V * 40;
            var s = new Tr { h = 6.2f, r0 = 0.38f, r1 = 0.22f, lean = new[] { 0.7f, 0.3f, 1.0f }[V], twist = 3.0f, ph = 0.4f + V * 2.1f };
            Trunk(b, 0, s, 5, sd);
            Vector3 top = TPos(s, 1f);
            for (int k = 0; k < 3; k++)
                TubeLimb(b, 0, Limb(top + new Vector3(0f, -0.3f, 0f), Az(k * 120f + 40f + V * 30f), 1.7f, 1.2f, 4), 0.14f, 0.07f, sd);

            Vector3 cc = top + new Vector3(0f, 1.9f, 0f);
            b.Blob(1, cc, new Vector3(2.7f, 1.05f, 2.7f), sb, lv, 0.14f, 3);
            for (int k = 0; k < 2; k++)
            {
                float a = k * 3.3f + 0.6f + V;
                b.Blob(1, cc + new Vector3(Mathf.Cos(a) * 1.9f, -0.3f, Mathf.Sin(a) * 1.9f), new Vector3(1.4f, 0.75f, 1.4f), sb + 5 + k, lv, 0.14f, 3);
            }
            // свисающие пряди: розовые, мятные, голубые по кругу
            int n = lod == 0 ? 14 : 8, sides = lod == 0 ? 4 : 3, segs = lod == 0 ? 4 : 3;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f + 0.35f * TreeMB.H(sb + 9, i) + V;
                float rho = 0.9f + 2.2f * TreeMB.H(sb + 10, i);
                Vector3 start = new Vector3(cc.x + Mathf.Cos(a) * rho, cc.y - 0.45f, cc.z + Mathf.Sin(a) * rho);
                Vector3 rad = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), perp = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                float len = 4.6f + 1.5f * TreeMB.H(sb + 11, i);
                var pts = new Vector3[segs + 1];
                var rr = new float[segs + 1];
                for (int k = 0; k <= segs; k++)
                {
                    float t = k / (float)segs;
                    pts[k] = start + rad * (0.65f * Mathf.Sin(t * Mathf.PI * 0.5f)) + perp * (Mathf.Sin(t * 5f + i) * 0.14f * t) + new Vector3(0f, -len * t, 0f);
                    rr[k] = Mathf.Lerp(0.115f, 0.04f, t);
                }
                b.Tube(1 + i % 3, pts, rr, sides, false);
            }
            return b.Build(name);
        }

        // ============================================================ 4. МЯТНЫЙ ГРИБ-ДЕРЕВО
        static Mesh Mushroom(int V, int lod, string name)
        {
            var b = new TreeMB();
            int lv = lod == 0 ? 1 : 0, sd = lod == 0 ? 7 : 5, sb = 400 + V * 40;
            var s = new Tr { h = 3.0f, r0 = 0.52f, r1 = 0.36f, lean = new[] { 0.2f, 0.45f, 0.1f }[V], twist = 3.0f, ph = 0.8f + V * 1.7f };
            Trunk(b, 0, s, 4, sd);
            Vector3 top = TPos(s, 1f);
            float rx = new[] { 3.0f, 2.6f, 3.3f }[V], ry = new[] { 1.25f, 1.45f, 1.1f }[V];
            Vector3 cc = top + new Vector3(0f, 0.9f, 0f);
            b.Blob(1, cc, new Vector3(rx, ry, rx), sb, lv, 0.10f, 3);
            b.Blob(1, cc + new Vector3(0.1f, ry * 0.72f, 0f), new Vector3(rx * 0.42f, ry * 0.5f, rx * 0.42f), sb + 3, lv, 0.12f, 3);
            // белые пятна лежат на поверхности шляпы
            int ns = lod == 0 ? 9 : 5;
            for (int i = 0; i < ns; i++)
            {
                float a = i * 2.39996f + V * 0.9f;
                float rho = rx * (0.52f + 0.36f * TreeMB.H(sb + 7, i));
                float x = Mathf.Cos(a) * rho, z = Mathf.Sin(a) * rho;
                float q = Mathf.Max(0f, 1f - (x / rx) * (x / rx) - (z / rx) * (z / rx));
                float y = ry * Mathf.Sqrt(q) * 0.9f - 0.03f;
                float k = 0.8f + 0.5f * TreeMB.H(sb + 8, i);
                b.Blob(2, cc + new Vector3(x, y, z), new Vector3(0.5f * k, 0.17f, 0.5f * k), sb + 20 + i, 0, 0.12f, -1);
            }
            // воротничок на ножке
            b.Blob(2, TPos(s, 0.62f), new Vector3(0.62f, 0.10f, 0.62f), sb + 50, 0, 0.12f, -1);
            return b.Build(name);
        }

        // ============================================================ 5. ЛУННОЕ ДЕРЕВО
        static Mesh Moon(int V, int lod, string name)
        {
            var b = new TreeMB();
            int lv = lod == 0 ? 1 : 0, sd = lod == 0 ? 6 : 5, sb = 500 + V * 40;
            var s = new Tr { h = 4.6f, r0 = 0.32f, r1 = 0.19f, lean = new[] { 0.5f, 0.25f, 0.8f }[V], twist = 4.0f, ph = 0.2f + V * 2.3f };
            Trunk(b, 0, s, 5, sd);
            Vector3 top = TPos(s, 1f);
            Vector3 cc = top + new Vector3(0f, 1.9f, 0f);
            b.Blob(1, cc, new Vector3(2.4f, 2.0f, 2.4f), sb, lv, 0.18f, 3);
            int nl = new[] { 5, 4, 6 }[V];
            for (int i = 0; i < nl; i++)
            {
                float a = i / (float)nl * Mathf.PI * 2f + 0.5f + V;
                Vector3 c = cc + new Vector3(Mathf.Cos(a) * 1.85f, -0.2f + (TreeMB.H(sb + 5, i) - 0.5f) * 1.2f, Mathf.Sin(a) * 1.85f);
                float k = 0.85f + 0.3f * TreeMB.H(sb + 6, i);
                b.Blob(1, c, new Vector3(1.15f * k, 0.95f * k, 1.15f * k), sb + 10 + i, lv, 0.18f, 3);
            }
            b.Blob(1, cc + new Vector3(0.15f, 1.7f, 0f), new Vector3(1.0f, 0.8f, 1.0f), sb + 20, lv, 0.18f, 3);
            // жемчужные шарики-плоды на тонких нитях под кроной
            int no = lod == 0 ? 9 : 6;
            for (int i = 0; i < no; i++)
            {
                float a = i / (float)no * Mathf.PI * 2f + 0.4f * TreeMB.H(sb + 30, i) + V;
                float rho = 1.3f + 1.5f * TreeMB.H(sb + 31, i);
                Vector3 attach = cc + new Vector3(Mathf.Cos(a) * rho, -1.05f - 0.2f * TreeMB.H(sb + 32, i), Mathf.Sin(a) * rho);
                float drop = 0.55f + 0.9f * TreeMB.H(sb + 33, i);
                Vector3 orb = attach + new Vector3(0f, -drop, 0f);
                b.Tube(0, new[] { attach, orb + new Vector3(0f, 0.2f, 0f) }, new[] { 0.022f, 0.022f }, 3, false);
                float k = 0.8f + 0.4f * TreeMB.H(sb + 34, i), q = 0.33f * k;
                b.Blob(2, orb, new Vector3(q, q, q), sb + 40 + i, 0, 0.10f, -1);
            }
            return b.Build(name);
        }

        // ============================================================ 6. ЛЕНТОЧНОЕ ДЕРЕВО
        static Mesh Ribbon(int V, int lod, string name)
        {
            var b = new TreeMB();
            int lv = lod == 0 ? 1 : 0, sd = lod == 0 ? 7 : 5, sb = 600 + V * 40;
            float turns = new[] { 1.5f, 2.0f, 2.5f }[V], ph = 0.3f + V * 1.4f, hh = 9.2f;
            System.Func<float, Vector3> pos = t =>
            {
                float k = 0.50f * Mathf.Pow(t, 0.8f);
                return new Vector3(Mathf.Sin(t * Mathf.PI * 2f * turns + ph) * k, hh * t - 0.25f * (1f - t), Mathf.Cos(t * Mathf.PI * 2f * turns + ph) * k);
            };
            System.Func<float, float> rad = t => Mathf.Lerp(0.46f, 0.20f, t) * (1f + 0.5f * Mathf.Exp(-t * 9f));

            b.TubeF(0, pos, rad, lod == 0 ? 10 : 7, sd, true);

            // лента: спираль вокруг ствола
            int ns = lod == 0 ? 24 : 12, sides = lod == 0 ? 4 : 3;
            var pts = new Vector3[ns + 1];
            var rr = new float[ns + 1];
            for (int i = 0; i <= ns; i++)
            {
                float t = 0.06f + 0.70f * i / ns;
                float phi = t * Mathf.PI * 2f * 3.2f + V;
                float o = rad(t) + 0.05f;
                pts[i] = pos(t) + new Vector3(Mathf.Cos(phi) * o, 0f, Mathf.Sin(phi) * o);
                rr[i] = 0.10f;
            }
            b.Tube(2, pts, rr, sides, false);

            // три яруса облачков, чередуются сиреневый и розовый
            float[][] tiers =
            {
                new[] { 6.0f, 3.0f, 8.3f, 2.3f, 10.3f, 1.6f },
                new[] { 6.6f, 2.7f, 8.8f, 2.2f, 10.7f, 1.5f },
                new[] { 5.6f, 3.2f, 7.9f, 2.4f, 10.0f, 1.7f }
            };
            for (int ti = 0; ti < 3; ti++)
            {
                float y = tiers[V][ti * 2], R = tiers[V][ti * 2 + 1];
                Vector3 cx = pos(Mathf.Min(1f, (y + 0.25f) / (hh + 0.25f)));
                Vector3 c = new Vector3(cx.x, y, cx.z);
                int sub = ti % 2 == 0 ? 1 : 2;
                b.Blob(sub, c, new Vector3(R * 0.85f, 0.85f - 0.1f * ti, R * 0.85f), sb + ti * 10, lv, 0.16f, 3);
                for (int i = 0; i < 4; i++)
                {
                    float a = i / 4f * Mathf.PI * 2f + 0.8f * ti + V;
                    Vector3 pc = c + new Vector3(Mathf.Cos(a) * R * 0.78f, -0.12f + (TreeMB.H(sb + 5, ti * 4 + i) - 0.5f) * 0.3f, Mathf.Sin(a) * R * 0.78f);
                    b.Blob(sub == 1 ? 2 : 1, pc, new Vector3(R * 0.42f, 0.52f, R * 0.42f), sb + 30 + ti * 4 + i, 0, 0.16f, 3);
                }
            }
            Vector3 tc = pos(1f);
            b.Blob(1, new Vector3(tc.x, hh + 1.1f, tc.z), new Vector3(0.9f, 0.7f, 0.9f), sb + 80, lv, 0.16f, 3);
            return b.Build(name);
        }

        // ============================================================ 7. ОБЛАЧНЫЙ КУСТ
        static Mesh Bush(int V, int lod, string name)
        {
            var b = new TreeMB();
            int lv = lod == 0 ? 1 : 0, sb = 700 + V * 40;
            int[] centerSub = { 1, 3, 2 };
            int[][] ringSub = { new[] { 2, 1, 2, 1 }, new[] { 1, 3, 1, 3 }, new[] { 1, 2, 3, 1 } };
            int[][] topSub = { new[] { 3, 1 }, new[] { 2, 1 }, new[] { 1, 3 } };
            b.Blob(centerSub[V], new Vector3(0f, 0.62f, 0f), new Vector3(0.9f, 0.7f, 0.9f), sb, lv, 0.16f, -1);
            int nr = lod == 0 ? 4 : 3;
            for (int i = 0; i < nr; i++)
            {
                float a = i / (float)nr * Mathf.PI * 2f + 0.5f + V;
                float k = 0.8f + 0.3f * TreeMB.H(sb + 2, i);
                b.Blob(ringSub[V][i], new Vector3(Mathf.Cos(a) * 0.78f, 0.42f + 0.1f * TreeMB.H(sb + 3, i), Mathf.Sin(a) * 0.78f), new Vector3(0.55f * k, 0.45f * k, 0.55f * k), sb + 10 + i, 0, 0.16f, -1);
            }
            if (lod == 0)
                for (int i = 0; i < 2; i++)
                {
                    float a = i * 3.0f + 1.0f + V;
                    b.Blob(topSub[V][i], new Vector3(Mathf.Cos(a) * 0.35f, 1.12f, Mathf.Sin(a) * 0.35f), new Vector3(0.42f, 0.36f, 0.42f), sb + 20 + i, 0, 0.16f, -1);
                }
            return b.Build(name);
        }

        // ============================================================ ЕДИНЫЙ ЗНАК: венок из камней у корней
        // 0 камень, 1 бирюзовые руны. Ось Y, центр в нуле, земля на y=0. Камни посажены в землю глубже половины (на склоне не «висят»).
        // Каждый третий камень стоячая стела с руной на внешней грани, следующий низкий с руной-полосой сверху, третий низкий с галькой рядом.
        public static Mesh Wreath(WingKind k, string name)
        {
            var b = new TreeMB();
            float R = RingR(k), sc = k == WingKind.Bush ? 0.5f : 1f;
            int n = Mathf.Max(6, Mathf.RoundToInt(R * 4.2f)), seed = 900 + (int)k * 7;
            for (int i = 0; i < n; i++)
            {
                float a = (i + 0.5f) / n * Mathf.PI * 2f + 0.12f * (TreeMB.H(seed, i) - 0.5f);
                float yaw = -(a * Mathf.Rad2Deg + 90f);   // длинной стороной вдоль окружности
                float rr = R * (0.96f + 0.10f * TreeMB.H(seed + 1, i));
                Vector3 rad = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                if (i % 3 == 0)
                {
                    float hx = (0.17f + 0.04f * TreeMB.H(seed + 2, i)) * sc, hy = (0.38f + 0.10f * TreeMB.H(seed + 3, i)) * sc, hz = 0.13f * sc;
                    Vector3 c = rad * rr + new Vector3(0f, hy - 0.16f * sc, 0f);
                    GroveGeo.Box(b, 0, c, new Vector3(hx, hy, hz), yaw);
                    GroveGeo.Box(b, 1, c + rad * (hz + 0.004f) + new Vector3(0f, hy * 0.12f, 0f), new Vector3(hx * 0.6f, hy * 0.65f, 0.016f), yaw);
                }
                else
                {
                    float hx = (0.30f + 0.10f * TreeMB.H(seed + 2, i)) * sc, hy = (0.20f + 0.05f * TreeMB.H(seed + 3, i)) * sc, hz = (0.18f + 0.06f * TreeMB.H(seed + 4, i)) * sc;
                    Vector3 c = rad * rr + new Vector3(0f, -0.05f * sc, 0f);
                    GroveGeo.Box(b, 0, c, new Vector3(hx, hy, hz), yaw);
                    if (i % 3 == 1)
                        GroveGeo.Box(b, 1, c + new Vector3(0f, hy + 0.004f, 0f), new Vector3(hx * 0.85f, 0.025f, hz * 0.7f), yaw);
                    else
                    {
                        float a2 = a + 0.14f, r2 = rr + 0.42f * sc;
                        GroveGeo.Box(b, 0, new Vector3(Mathf.Cos(a2) * r2, -0.06f * sc, Mathf.Sin(a2) * r2), new Vector3(hx * 0.5f, hy * 0.55f, hz * 0.6f), -(a2 * Mathf.Rad2Deg + 90f));
                    }
                }
            }
            return b.Build(name);
        }
    }
}
#endif
