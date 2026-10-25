#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/SacredTreeMeshes.cs (Editor)  НОВЫЙ ФАЙЛ
// Процедурные модели «деревьев-хранителей» (нерубимых деревьев-достопримечательностей). Только геометрия, префабы и расстановка в ForestSacred.cs.
// Строит через тот же TreeMB, что и остальной лес (flat shading, пивот у корней, Y вверх, метры). Всё детерминировано: форма зависит только от (вид, lod).
//
// ЕДИНЫЙ ЗНАК «НЕ РУБИТСЯ» (общий для всех видов, строится отдельными мешами):
//   Mark  каменное кольцо у корней радиусом RingR: 10 низких камней + 5 внешних, на каждом большом камне бирюзовая руна (сабмеш 1 = светится)
//   Gem   кристалл-знак (вытянутый октаэдр), висит над кроной, вращается (SacredVibe)
//
// Сабмеши тела дерева (материалы собирает ForestSacred.Mats):
//   Cloud    0 кора, 1 белое облако, 2 розовые «подсветки», 3 сиреневый низ облаков
//   Blossom  0 кора, 1 светло-розовая крона, 2 насыщенно-розовые акценты, 3 тёмный низ кроны
//   Crystal  0 кора, 1 мятная крона, 2 кристаллы (светятся), 3 тёмный низ кроны
//   Lantern  0 кора и верёвки, 1 сине-мятная крона, 2 тёплые фонарики (светятся), 3 тёмный низ кроны
//   Sleeper  0 кора, 1 тёмно-бирюзовая крона, 2 лаймовый мох, 3 тёмное (лицо, низ кроны)
// LOD0: шары по 80 граней (level 1), 8 граней у стволов. LOD1: шары по 20 граней (level 0), 5 граней у стволов.
using System.Collections.Generic;
using UnityEngine;

namespace IslandV2
{
    public static class SacredMeshes
    {
        public const float RingR = 2.7f;       // радиус каменного кольца, м (один на все виды: единый знак)

        // ============================================================ габариты по видам (читает ForestSacred)
        public static float TrunkR(SacredKind k)
        {
            switch (k)
            {
                case SacredKind.Cloud: return 0.45f;
                case SacredKind.Blossom: return 0.55f;
                case SacredKind.Crystal: return 0.50f;
                case SacredKind.Lantern: return 0.60f;
                default: return 0.95f;
            }
        }
        public static float TrunkH(SacredKind k)
        {
            switch (k)
            {
                case SacredKind.Cloud: return 3.2f;
                case SacredKind.Blossom: return 3.0f;
                case SacredKind.Crystal: return 3.4f;
                case SacredKind.Lantern: return 2.8f;
                default: return 3.4f;
            }
        }
        // высота центра кроны (для частиц)
        public static float CrownY(SacredKind k)
        {
            switch (k)
            {
                case SacredKind.Cloud: return 5.4f;
                case SacredKind.Blossom: return 4.8f;
                case SacredKind.Crystal: return 4.8f;
                case SacredKind.Lantern: return 4.4f;
                default: return 5.0f;
            }
        }
        public static float CrownR(SacredKind k)
        {
            switch (k)
            {
                case SacredKind.Cloud: return 2.6f;
                case SacredKind.Blossom: return 3.2f;
                case SacredKind.Crystal: return 2.2f;
                case SacredKind.Lantern: return 3.0f;
                default: return 3.8f;
            }
        }
        // высота парящего кристалла-знака над землёй
        public static float GemY(SacredKind k)
        {
            switch (k)
            {
                case SacredKind.Cloud: return 9.8f;
                case SacredKind.Blossom: return 8.4f;
                case SacredKind.Crystal: return 8.8f;
                case SacredKind.Lantern: return 8.2f;
                default: return 8.9f;
            }
        }

        // ============================================================ меш тела дерева
        public static Mesh Body(SacredKind k, int lod, string name)
        {
            switch (k)
            {
                case SacredKind.Cloud: return Cloud(lod, name);
                case SacredKind.Blossom: return Blossom(lod, name);
                case SacredKind.Crystal: return Crystal(lod, name);
                case SacredKind.Lantern: return Lantern(lod, name);
                default: return Sleeper(lod, name);
            }
        }

        // ============================================================ общие помощники
        struct Tr { public float h, r0, r1, lean, twist, ph; }

        static Vector3 TPos(Tr s, float t)
        {
            return new Vector3(Mathf.Sin(t * s.twist + s.ph) * s.lean * t, Mathf.Lerp(-0.25f, s.h, t), Mathf.Cos(t * s.twist * 0.8f + s.ph + 0.6f) * s.lean * 0.7f * t);
        }
        static float TRad(Tr s, float t) { return Mathf.Lerp(s.r0, s.r1, t) * (1f + 0.55f * Mathf.Exp(-t * 9f)); }
        static float TOfY(Tr s, float y) { return Mathf.Clamp01((y + 0.25f) / (s.h + 0.25f)); }

        static void Trunk(TreeMB b, int sub, Tr s, int segs, int sides)
        {
            b.TubeF(sub, t => TPos(s, t), t => TRad(s, t), segs, sides, true);
        }

        // корни-контрфорсы: расходятся от ствола и уходят в землю
        static void Roots(TreeMB b, int sub, Tr s, int n, float spread, int sides, int seed)
        {
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n + 0.07f * TreeMB.H(seed, i)) * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float r = s.r0;
                var p = new[]
                {
                    d * (r * 0.4f) + Vector3.up * (0.9f * r + 0.3f),
                    d * (r * 1.1f) + Vector3.up * (0.35f * r),
                    d * (r * spread) + Vector3.up * -0.05f
                };
                b.Tube(sub, p, new[] { r * 0.42f, r * 0.30f, r * 0.12f }, sides, false);
            }
        }

        static Vector3 Az(float deg) { float a = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)); }

        // ветка: дуга от start в сторону dir, длина len, подъём rise
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

        // кристалл-шип: призма с острой вершиной. baseC основание, dir куда растёт, len длина, rad радиус
        static void Shard(TreeMB b, int sub, Vector3 baseC, Vector3 dir, float len, float rad, int sides)
        {
            dir.Normalize();
            Vector3 u = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 w = Vector3.Cross(u, dir).normalized;
            Vector3 c1 = baseC + dir * (len * 0.68f);
            Vector3 apex = baseC + dir * len;
            Vector3 inside = baseC + dir * (len * 0.4f);
            var r0 = new Vector3[sides];
            var r1 = new Vector3[sides];
            for (int j = 0; j < sides; j++)
            {
                float a = j / (float)sides * Mathf.PI * 2f;
                Vector3 o = u * (Mathf.Cos(a) * rad) + w * (Mathf.Sin(a) * rad);
                r0[j] = baseC + o;
                r1[j] = c1 + o * 0.92f;
            }
            for (int j = 0; j < sides; j++)
            {
                int j1 = (j + 1) % sides;
                b.Tri(sub, r0[j], r0[j1], r1[j1], inside);
                b.Tri(sub, r0[j], r1[j1], r1[j], inside);
                b.Tri(sub, r1[j], r1[j1], apex, inside);
            }
        }

        // ============================================================ ЕДИНЫЙ ЗНАК: кольцо с рунами
        // 0 камень, 1 светящиеся руны. Ось Y, центр в нуле, земля на y=0.
        public static Mesh Mark(string name)
        {
            var b = new TreeMB();
            const int n = 10;
            for (int i = 0; i < n; i++)
            {
                float a = (i + 0.5f) / n * Mathf.PI * 2f;
                float yaw = -(a * Mathf.Rad2Deg + 90f);   // длинной стороной вдоль окружности
                Vector3 c = new Vector3(Mathf.Cos(a) * RingR, 0.05f, Mathf.Sin(a) * RingR);
                float hx = 0.32f + 0.08f * TreeMB.H(901, i), hz = 0.17f + 0.05f * TreeMB.H(902, i);
                GroveGeo.Box(b, 0, c, new Vector3(hx, 0.11f, hz), yaw);
                GroveGeo.Box(b, 1, c + Vector3.up * 0.115f, new Vector3(hx * 0.62f, 0.012f, 0.05f), yaw);   // руна-«чёрточка» на камне
                if (i % 2 == 0)
                {
                    // внешний камешек пониже и без руны: кольцо выглядит «выросшим», а не отлитым
                    float a2 = a + 0.17f;
                    Vector3 c2 = new Vector3(Mathf.Cos(a2) * (RingR + 0.55f), 0.03f, Mathf.Sin(a2) * (RingR + 0.55f));
                    GroveGeo.Box(b, 0, c2, new Vector3(0.20f, 0.07f, 0.16f), -(a2 * Mathf.Rad2Deg + 90f));
                }
            }
            return b.Build(name);
        }

        // точки свечения рун (локально, корень префаба): на каждом втором камне
        public static Vector3[] RuneSpots()
        {
            var res = new Vector3[5];
            for (int k = 0; k < 5; k++)
            {
                float a = (2 * k + 0.5f) / 10f * Mathf.PI * 2f;
                res[k] = new Vector3(Mathf.Cos(a) * RingR, 0.26f, Mathf.Sin(a) * RingR);
            }
            return res;
        }

        // ============================================================ ЕДИНЫЙ ЗНАК: кристалл над кроной (пивот в центре)
        public static Mesh Gem(string name)
        {
            var b = new TreeMB();
            Vector3 top = new Vector3(0f, 0.62f, 0f), bot = new Vector3(0f, -0.62f, 0f), o = Vector3.zero;
            var ring = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float a = (i + 0.5f) / 4f * Mathf.PI * 2f;
                ring[i] = new Vector3(Mathf.Cos(a) * 0.34f, 0f, Mathf.Sin(a) * 0.34f);
            }
            for (int i = 0; i < 4; i++)
            {
                int i1 = (i + 1) % 4;
                b.Tri(0, top, ring[i], ring[i1], o);
                b.Tri(0, bot, ring[i1], ring[i], o);
            }
            return b.Build(name);
        }

        // ============================================================ 1. ОБЛАЧНОЕ ДЕРЕВО
        static Mesh Cloud(int lod, string name)
        {
            var b = new TreeMB();
            int lv = lod == 0 ? 1 : 0, sd = lod == 0 ? 8 : 5;
            var s = new Tr { h = 3.5f, r0 = 0.42f, r1 = 0.26f, lean = 0.35f, twist = 4.2f, ph = 0.4f };
            Trunk(b, 0, s, 6, sd);
            Roots(b, 0, s, 5, 2.1f, sd, 11);
            Vector3 top = TPos(s, 1f);

            // три ветви-«ножки» держат облако
            for (int k = 0; k < 3; k++)
            {
                var pts = Limb(top + Vector3.down * 0.5f, Az(k * 120f + 25f), 1.7f, 1.4f, 4);
                TubeLimb(b, 0, pts, 0.15f, 0.07f, sd);
            }

            // облако: большая «шапка», кольцо пухлых шаров, верхние шары, макушка
            float cy = top.y + 1.9f;
            Vector3 cc = new Vector3(top.x, cy, top.z);
            b.Blob(1, cc, new Vector3(2.15f, 1.55f, 2.15f), 101, lv, 0.16f, 3);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + 0.3f;
                float y = cy - 0.55f + (TreeMB.H(110, i) - 0.5f) * 0.5f;
                Vector3 c = cc + new Vector3(Mathf.Cos(a) * 1.95f, y - cy, Mathf.Sin(a) * 1.95f);
                float k = 0.9f + 0.25f * TreeMB.H(111, i);
                b.Blob(i % 3 == 0 ? 2 : 1, c, new Vector3(1.25f * k, 0.95f * k, 1.25f * k), 120 + i, lv, 0.16f, 3);
            }
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f + 1.0f;
                Vector3 c = cc + new Vector3(Mathf.Cos(a) * 0.95f, 1.05f, Mathf.Sin(a) * 0.95f);
                b.Blob(i == 1 ? 2 : 1, c, new Vector3(1.05f, 0.85f, 1.05f), 130 + i, lv, 0.16f, 3);
            }
            b.Blob(1, cc + new Vector3(0.1f, 1.75f, 0f), new Vector3(0.85f, 0.70f, 0.85f), 140, lv, 0.16f, 3);

            // отдельные облачка-спутники вокруг кроны
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f + 0.5f;
                float r = 3.0f + 0.4f * TreeMB.H(150, i);
                Vector3 c = cc + new Vector3(Mathf.Cos(a) * r, -1.2f + i * 0.9f, Mathf.Sin(a) * r);
                b.Blob(1, c, new Vector3(0.45f, 0.32f, 0.45f), 160 + i, 0, 0.16f, 3);
            }
            return b.Build(name);
        }

        // ============================================================ 2. ЦВЕТУЩЕЕ ДЕРЕВО
        static Mesh Blossom(int lod, string name)
        {
            var b = new TreeMB();
            int lv = lod == 0 ? 1 : 0, sd = lod == 0 ? 8 : 5;
            var s = new Tr { h = 3.0f, r0 = 0.50f, r1 = 0.30f, lean = 0.7f, twist = 3.2f, ph = 1.1f };
            Trunk(b, 0, s, 6, sd);
            Roots(b, 0, s, 6, 2.3f, sd, 21);
            Vector3 top = TPos(s, 1f);

            // три главные ветви, каждая заканчивается плоской «шапкой» цветов
            float[] az = { 20f, 140f, 255f };
            for (int k = 0; k < 3; k++)
            {
                Vector3 dir = Az(az[k]);
                var pts = Limb(top + Vector3.down * 0.2f, dir, 2.3f, 1.8f, 5);
                TubeLimb(b, 0, pts, 0.20f, 0.08f, sd);
                Vector3 e = pts[pts.Length - 1];
                b.Blob(1, e + Vector3.up * 0.6f, new Vector3(1.9f, 0.95f, 1.9f), 210 + k, lv, 0.20f, 3);
                b.Blob(2, e + Vector3.up * 1.15f - dir * 0.4f, new Vector3(1.2f, 0.65f, 1.2f), 220 + k, lv, 0.20f, 3);
                Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
                b.Blob(1, e + Vector3.up * 0.3f + side * 1.5f, new Vector3(1.1f, 0.7f, 1.1f), 225 + k, 0, 0.20f, 3);
            }
            // центральная шапка
            b.Blob(1, top + Vector3.up * 1.9f, new Vector3(2.1f, 1.05f, 2.1f), 230, lv, 0.20f, 3);
            b.Blob(2, top + new Vector3(0.2f, 2.55f, -0.1f), new Vector3(1.1f, 0.6f, 1.1f), 231, lv, 0.20f, 3);

            // ковёр опавших лепестков на земле (плоские пятна)
            for (int i = 0; i < 9; i++)
            {
                float a = TreeMB.H(240, i) * Mathf.PI * 2f;
                float r = 1.3f + TreeMB.H(241, i) * 2.4f;
                Vector3 c = new Vector3(Mathf.Cos(a) * r, 0.03f, Mathf.Sin(a) * r);
                b.Blob(i % 3 == 0 ? 2 : 1, c, new Vector3(0.45f + 0.4f * TreeMB.H(242, i), 0.03f, 0.35f + 0.3f * TreeMB.H(243, i)), 250 + i, 0, 0.15f, -1);
            }
            return b.Build(name);
        }

        // ============================================================ 3. КРИСТАЛЬНОЕ ДЕРЕВО
        struct Sh { public Vector3 c, d; public float len, rad; }

        static List<Sh> CrystalShards()
        {
            var l = new List<Sh>();
            // кластер у корней
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f + 0.35f;
                float tilt = (16f + 22f * TreeMB.H(310, i)) * Mathf.Deg2Rad;
                float rr = 0.75f + 0.45f * TreeMB.H(311, i);
                Vector3 c = new Vector3(Mathf.Cos(a) * rr, -0.05f, Mathf.Sin(a) * rr);
                Vector3 d = new Vector3(Mathf.Cos(a) * Mathf.Sin(tilt), Mathf.Cos(tilt), Mathf.Sin(a) * Mathf.Sin(tilt));
                l.Add(new Sh { c = c, d = d, len = 0.9f + 1.1f * TreeMB.H(312, i), rad = 0.13f + 0.12f * TreeMB.H(313, i) });
            }
            // на стволе
            float[] ys = { 1.5f, 2.2f, 2.9f };
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.4f + 0.8f;
                Vector3 dd = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                l.Add(new Sh { c = dd * 0.40f + Vector3.up * ys[i], d = (dd * 0.8f + Vector3.up * 0.6f).normalized, len = 0.7f + 0.3f * TreeMB.H(320, i), rad = 0.10f });
            }
            // в кроне
            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * Mathf.PI * 2f + 0.9f;
                Vector3 dd = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                l.Add(new Sh { c = dd * 1.7f + Vector3.up * (4.7f + 0.4f * TreeMB.H(330, i)), d = (dd * 0.5f + Vector3.up).normalized, len = 1.0f, rad = 0.13f });
            }
            return l;
        }

        // точки свечения кристаллов (локально): у вершин
        public static Vector3[] CrystalSpots()
        {
            var sh = CrystalShards();
            var res = new Vector3[sh.Count];
            for (int i = 0; i < res.Length; i++) res[i] = sh[i].c + sh[i].d * (sh[i].len * 0.75f);
            return res;
        }

        static Mesh Crystal(int lod, string name)
        {
            var b = new TreeMB();
            int lv = lod == 0 ? 1 : 0, sd = lod == 0 ? 8 : 5, sides = lod == 0 ? 5 : 4;
            var s = new Tr { h = 3.4f, r0 = 0.46f, r1 = 0.28f, lean = 0.4f, twist = 5.2f, ph = 2.0f };
            Trunk(b, 0, s, 6, sd);
            Roots(b, 0, s, 5, 2.0f, sd, 31);
            Vector3 top = TPos(s, 1f);

            // воздушная крона: шапка и неплотное кольцо мятных шаров
            b.Blob(1, top + Vector3.up * 1.6f, new Vector3(1.5f, 1.2f, 1.5f), 301, lv, 0.22f, 3);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + 0.5f;
                Vector3 c = top + new Vector3(Mathf.Cos(a) * 1.7f, 1.4f + (TreeMB.H(302, i) - 0.5f) * 0.9f, Mathf.Sin(a) * 1.7f);
                b.Blob(1, c, new Vector3(1.0f, 0.8f, 1.0f), 305 + i, lv, 0.22f, 3);
            }
            b.Blob(1, top + new Vector3(0.1f, 2.7f, 0f), new Vector3(0.9f, 0.75f, 0.9f), 315, lv, 0.22f, 3);

            var sh = CrystalShards();
            for (int i = 0; i < sh.Count; i++) Shard(b, 2, sh[i].c, sh[i].d, sh[i].len, sh[i].rad, sides);
            return b.Build(name);
        }

        // ============================================================ 4. ФОНАРНОЕ ДЕРЕВО
        // точки подвеса фонариков (центр фонарика, локально)
        public static List<Vector3> LanternSpots()
        {
            var l = new List<Vector3>();
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f + 0.35f;
                float r = 1.1f + 1.25f * TreeMB.H(410, i);
                l.Add(new Vector3(Mathf.Cos(a) * r, 2.6f + 0.5f * TreeMB.H(420, i), Mathf.Sin(a) * r));
            }
            return l;
        }

        static Mesh Lantern(int lod, string name)
        {
            var b = new TreeMB();
            int lv = lod == 0 ? 1 : 0, sd = lod == 0 ? 8 : 5;
            var s = new Tr { h = 2.7f, r0 = 0.58f, r1 = 0.38f, lean = 0.3f, twist = 3.6f, ph = 0.2f };
            Trunk(b, 0, s, 6, sd);
            Roots(b, 0, s, 6, 2.2f, sd, 41);
            Vector3 top = TPos(s, 1f);

            // широкая плоская крона: под ней вешаются фонарики
            b.Blob(1, top + Vector3.up * 1.7f, new Vector3(2.2f, 1.3f, 2.2f), 401, lv, 0.22f, 3);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f + 0.2f;
                Vector3 c = top + new Vector3(Mathf.Cos(a) * 2.0f, 1.3f + (TreeMB.H(402, i) - 0.5f) * 0.5f, Mathf.Sin(a) * 2.0f);
                b.Blob(1, c, new Vector3(1.45f, 0.95f, 1.45f), 405 + i, lv, 0.22f, 3);
            }
            b.Blob(1, top + new Vector3(0f, 2.9f, 0f), new Vector3(1.4f, 0.9f, 1.4f), 415, lv, 0.22f, 3);

            // фонарики на верёвках
            foreach (var sp in LanternSpots())
            {
                Vector3 ropeTop = new Vector3(sp.x, 3.55f, sp.z);
                b.Tube(0, new[] { ropeTop, sp + Vector3.up * 0.30f }, new[] { 0.025f, 0.025f }, 4, false);
                b.Blob(2, sp, new Vector3(0.21f, 0.28f, 0.21f), 430 + (int)(sp.x * 10f), 0, 0.15f, -1);
                b.Blob(0, sp + Vector3.up * 0.30f, new Vector3(0.14f, 0.06f, 0.14f), 450 + (int)(sp.z * 10f), 0, 0.15f, -1);
            }
            return b.Build(name);
        }

        // ============================================================ 5. СОННЫЙ СТРАЖ
        // дуга на стволе: линия лица. Ствол прямой (lean = 0), лицо на +Z.
        static void Arc(TreeMB b, int sub, Tr s, float cx, float cy, float w, float dip, float rad, int pts)
        {
            var p = new Vector3[pts];
            var r = new float[pts];
            for (int i = 0; i < pts; i++)
            {
                float u = i / (pts - 1f) * 2f - 1f;
                float x = cx + u * w;
                float y = cy - dip * (1f - u * u);
                float rr = TRad(s, TOfY(s, y));
                float z = Mathf.Sqrt(Mathf.Max(0.05f, rr * rr - x * x)) + 0.03f;
                p[i] = new Vector3(x, y, z);
                r[i] = rad;
            }
            b.Tube(sub, p, r, 4, false);
        }

        static Mesh Sleeper(int lod, string name)
        {
            var b = new TreeMB();
            int lv = lod == 0 ? 1 : 0, sd = lod == 0 ? 8 : 5;
            var s = new Tr { h = 3.4f, r0 = 0.95f, r1 = 0.72f, lean = 0f, twist = 0f, ph = 0f };
            Trunk(b, 0, s, 6, sd);
            Roots(b, 0, s, 6, 2.0f, sd, 51);

            // лицо: закрытые глазки, улыбка, носик-сучок
            float eyeY = 2.05f;
            Arc(b, 3, s, -0.30f, eyeY, 0.17f, 0.05f, 0.032f, 5);
            Arc(b, 3, s, 0.30f, eyeY, 0.17f, 0.05f, 0.032f, 5);
            Arc(b, 3, s, 0f, 1.55f, 0.16f, 0.06f, 0.030f, 5);
            {
                float rr = TRad(s, TOfY(s, 1.8f));
                b.Blob(0, new Vector3(0f, 1.8f, rr + 0.02f), new Vector3(0.09f, 0.10f, 0.07f), 520, 0, 0.2f, -1);
            }

            // крона-купол: шапка, кольцо шаров, лаймовый мох сверху
            Vector3 cc = new Vector3(0f, 5.2f, 0f);
            b.Blob(1, cc, new Vector3(3.0f, 1.8f, 3.0f), 501, lv, 0.22f, 3);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f + 0.15f;
                Vector3 c = new Vector3(Mathf.Cos(a) * 2.4f, 4.4f + (TreeMB.H(502, i) - 0.5f) * 0.5f, Mathf.Sin(a) * 2.4f);
                b.Blob(1, c, new Vector3(1.5f, 1.1f, 1.5f), 505 + i, lv, 0.22f, 3);
            }
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f + 0.6f;
                float r = 1.1f + 0.7f * TreeMB.H(512, i);
                Vector3 c = new Vector3(Mathf.Cos(a) * r, 6.2f + 0.3f * TreeMB.H(513, i), Mathf.Sin(a) * r);
                b.Blob(2, c, new Vector3(0.9f, 0.6f, 0.9f), 515 + i, lv, 0.22f, 3);
            }
            // мох-кочки у корней
            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * Mathf.PI * 2f + 0.9f;
                Vector3 c = new Vector3(Mathf.Cos(a) * 1.5f, 0.08f, Mathf.Sin(a) * 1.5f);
                b.Blob(2, c, new Vector3(0.40f, 0.18f, 0.35f), 530 + i, 0, 0.2f, -1);
            }
            return b.Build(name);
        }
    }
}
#endif
