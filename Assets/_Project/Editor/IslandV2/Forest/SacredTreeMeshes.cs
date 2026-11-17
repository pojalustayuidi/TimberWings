#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/SacredTreeMeshes.cs (Editor)  НОВЫЙ ФАЙЛ
// Процедурные модели «деревьев-хранителей» (нерубимых деревьев-достопримечательностей). Только геометрия, префабы и расстановка в ForestSacred.cs.
// Строит через тот же TreeMB, что и остальной лес (flat shading, пивот у корней, Y вверх, метры). Всё детерминировано: форма зависит только от (вид, lod).
//
// ЕДИНЫЙ ЗНАК «НЕ РУБИТСЯ» (общий для всех видов, строится отдельными мешами):
//   Mark  каменное кольцо у корней радиусом RingR: 10 низких камней + 5 внешних, на каждом большом камне бирюзовая руна (сабмеш 1 = светится)
//   Gem   кристалл-знак, посаженный у корней
//
// Сабмеши тела дерева (материалы собирает ForestSacred.Mats):
//   Cloud    0 кора, 1 белое облако, 2 розовые «подсветки», 3 сиреневый низ облаков
//   Blossom  0 кора, 1 светло-розовая крона, 2 насыщенно-розовые акценты, 3 тёмный низ кроны
//   Crystal  0 кора, 1 мятная крона, 2 кристаллы (светятся), 3 тёмный низ кроны
//   Lantern  0 кора и верёвки, 1 сине-мятная крона, 2 тёплые фонарики (светятся), 3 тёмный низ кроны
//   Sleeper  0 кора, 1 тёмно-бирюзовая крона, 2 лаймовый мох, 3 тёмное (лицо, низ кроны)
// LOD0 сохраняет дополнительные детали листвы. LOD1 сохраняет все основные ветви и точки крепления.
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
        public static Vector3 GemFoot(SacredKind k)
        {
            return new Vector3(TrunkR(k) * 0.77f + 0.23f, 0.08f, TrunkR(k) * 0.88f + 0.15f);
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

        // Крупные корни выходят на поверхность с изгибом, а концы уходят в землю.
        static void Roots(TreeMB b, int sub, Tr s, int n, float spread, int sides, int seed)
        {
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n + 0.07f * TreeMB.H(seed, i)) * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 side = Vector3.Cross(d, Vector3.up);
                float bend = (TreeMB.H(seed + 1, i) - 0.5f) * 0.9f;
                float r = s.r0;
                float reach = Mathf.Min(2.65f, r * spread * Mathf.Lerp(0.72f, 1.12f, TreeMB.H(seed + 2, i)));
                var p = new[]
                {
                    TPos(s, TOfY(s, r + 0.35f)) + d * (r * 0.38f),
                    d * (reach * 0.46f) + Vector3.up * (0.38f * r),
                    d * (reach * 0.78f) + side * (bend * r * 0.45f) + Vector3.up * (0.08f * r),
                    d * reach + side * (bend * r) - Vector3.up * 0.12f
                };
                b.Tube(sub, p, new[] { r * 0.41f, r * 0.31f, r * 0.19f, r * 0.035f }, sides, false);
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

        static Vector3[] BoughPath(Tr trunk, float height, float azimuth, float length, float rise)
        {
            return Limb(TPos(trunk, TOfY(trunk, height)), Az(azimuth), length, rise, 4);
        }

        static void Bough(TreeMB b, Vector3[] p, float radius, float tipRadius, int sides)
        {
            var r = new float[p.Length];
            for (int i = 0; i < r.Length; i++) r[i] = Mathf.Lerp(radius, tipRadius, i / (r.Length - 1f));
            b.Tube(0, p, r, sides, true);
        }

        static Vector3 Bough(TreeMB b, Tr trunk, float height, float azimuth, float length, float rise, float radius, int sides)
        {
            var path = BoughPath(trunk, height, azimuth, length, rise);
            Bough(b, path, radius, 0.055f, sides);
            return path[path.Length - 1];
        }

        static void Leader(TreeMB b, Tr trunk, Vector3 tip, int sides)
        {
            Vector3 start = TPos(trunk, 0.82f);
            Vector3 bend = Vector3.Lerp(TPos(trunk, 1f), tip, 0.45f) + new Vector3(0.11f, 0f, 0.09f);
            b.Tube(0, new[] { start, bend, tip }, new[] { TRad(trunk, 0.82f) * 0.65f, 0.12f, 0.04f }, sides, true);
        }

        // Ветвь доходит до центра объёма. Нижний пояс оставляет запас перекрытия на обоих LOD.
        static void CanopyPad(TreeMB b, int sub, Vector3 center, Vector3 size, int seed, int lod)
        {
            int sides = lod == 0 ? 9 : 7;
            var lower = new Vector3[sides];
            var upper = new Vector3[sides];
            for (int j = 0; j < sides; j++)
            {
                float angle = (j + TreeMB.H(seed, 0)) * (2f * Mathf.PI / sides);
                Vector3 across = new Vector3(Mathf.Cos(angle) * size.x, 0f, Mathf.Sin(angle) * size.z);
                float outline = 0.88f + 0.20f * TreeMB.H(seed, j + 1);
                lower[j] = center + across * (0.85f * outline)
                    + Vector3.down * (size.y * (0.26f + 0.14f * TreeMB.H(seed + 1, j)));
                upper[j] = center + across * outline
                    + Vector3.up * (size.y * (0.25f + 0.20f * TreeMB.H(seed + 2, j)));
            }
            Vector3 top = center + new Vector3(size.x * 0.13f, size.y, -size.z * 0.12f);
            Vector3 bottom = center + Vector3.down * (size.y * 0.78f);
            for (int j = 0; j < sides; j++)
            {
                int next = (j + 1) % sides;
                b.Tri(3, bottom, lower[j], lower[next], center);
                b.Tri(sub, lower[j], upper[j], upper[next], center);
                b.Tri(sub, lower[j], upper[next], lower[next], center);
                b.Tri(sub, top, upper[next], upper[j], center);
            }
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

        // ============================================================ ЕДИНЫЙ ЗНАК: кристалл у корней (пивот в центре)
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
            var s = new Tr { h = 4.4f, r0 = 0.46f, r1 = 0.19f, lean = 0.63f, twist = 3.8f, ph = 0.45f };
            Trunk(b, 0, s, 6, sd);
            Roots(b, 0, s, 5, 3.35f, sd, 11);
            Vector3 top = TPos(s, 1f);

            // Приподнятая сердцевина и три облачных уступа, разделённые просветами.
            Vector3 high = top + new Vector3(-0.2f, 1.65f, 0.1f);
            Leader(b, s, high, sd);
            CanopyPad(b, 1, high, new Vector3(1.77f, 1.52f, 1.59f), 101, lod);
            b.Blob(1, high + new Vector3(-0.30f, 0.92f, -0.16f), new Vector3(1.12f, 0.88f, 1.02f), 102, lv, 0.12f, 3);
            float[] directions = { 33f, 143f, 266f };
            for (int i = 0; i < directions.Length; i++)
            {
                Vector3 outwards = Az(directions[i]);
                Vector3 tip = Bough(b, s, 2.58f + i * 0.52f, directions[i], 2.17f, 1.70f, 0.25f, sd);
                CanopyPad(b, 1, tip, new Vector3(1.61f, 0.96f, 1.48f), 120 + i, lod);
                b.Blob(2, tip - outwards * 0.25f + Vector3.up * 0.59f, new Vector3(0.94f, 0.66f, 0.95f), 130 + i, lv, 0.12f, 3);
            }
            return b.Build(name);
        }

        // ============================================================ 2. ЦВЕТУЩЕЕ ДЕРЕВО
        static Mesh Blossom(int lod, string name)
        {
            var b = new TreeMB();
            int lv = lod == 0 ? 1 : 0, sd = lod == 0 ? 8 : 5;
            var s = new Tr { h = 4.0f, r0 = 0.53f, r1 = 0.19f, lean = 0.88f, twist = 1.9f, ph = 0.15f };
            Trunk(b, 0, s, 6, sd);
            Roots(b, 0, s, 6, 3.3f, sd, 21);
            Vector3 top = TPos(s, 1f);

            // Низкая горизонтальная ветвь противовесом наклонному главному стволу.
            float[] az = { 260f, 124f, 8f };
            float[] lengths = { 2.9f, 2.55f, 1.65f };
            float[] lifts = { 1.13f, 1.96f, 2.23f };
            for (int k = 0; k < 3; k++)
            {
                Vector3 dir = Az(az[k]);
                Vector3 e = Bough(b, s, 2.42f + k * 0.41f, az[k], lengths[k], lifts[k], 0.29f, sd);
                Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
                CanopyPad(b, 1, e, new Vector3(1.70f, 0.83f, 1.52f), 210 + k, lod);
                b.Blob(2, e + Vector3.up * 0.35f + dir * 0.58f, new Vector3(0.92f, 0.48f, 0.88f), 220 + k, lv, 0.17f, 3);
                if (lod == 0)
                    b.Blob(1, e + Vector3.up * 0.14f + side * 0.91f, new Vector3(0.80f, 0.51f, 0.81f), 225 + k, 0, 0.20f, 3);
            }
            Vector3 high = top + new Vector3(0.2f, 1.65f, -0.38f);
            Leader(b, s, high, sd);
            CanopyPad(b, 1, high, new Vector3(1.43f, 0.88f, 1.40f), 230, lod);
            b.Blob(2, high + new Vector3(-0.29f, 0.52f, -0.35f), new Vector3(0.88f, 0.52f, 0.95f), 231, lv, 0.17f, 3);

            for (int i = 0; i < (lod == 0 ? 26 : 12); i++)
            {
                float a = TreeMB.H(240, i) * Mathf.PI * 2f;
                float r = 0.87f + TreeMB.H(241, i) * 2.6f;
                Vector3 axis = Az(71f * i) * (0.06f + TreeMB.H(242, i) * 0.07f);
                Vector3 side = Vector3.Cross(axis, Vector3.up) * 0.55f;
                Vector3 c = new Vector3(Mathf.Cos(a) * r, 0.034f, Mathf.Sin(a) * r);
                Vector3 ridge = c + Vector3.up * 0.024f;
                int layer = i % 3 == 0 ? 2 : 1;
                b.Tri(layer, c - axis, c + side, ridge + axis, c + Vector3.down);
                b.Tri(layer, c - axis, ridge + axis, c - side, c + Vector3.down);
            }
            return b.Build(name);
        }

        // ============================================================ 3. КРИСТАЛЬНОЕ ДЕРЕВО
        struct Sh { public Vector3 c, d; public float len, rad; }

        static Tr CrystalTrunk => new Tr { h = 5.1f, r0 = 0.49f, r1 = 0.14f, lean = 0.53f, twist = 5.2f, ph = 2.0f };

        static Vector3[] CrystalBough(Tr trunk, int i)
        {
            return BoughPath(trunk, 2.38f + 0.38f * i, 45f + 90f * i,
                1.48f + 0.43f * (i % 2), 2.01f + 0.52f * TreeMB.H(302, i));
        }

        static List<Sh> CrystalShards()
        {
            var l = new List<Sh>();
            var trunk = CrystalTrunk;
            // кластер у корней
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f + 0.35f;
                float tilt = (16f + 22f * TreeMB.H(310, i)) * Mathf.Deg2Rad;
                float rr = 0.75f + 0.45f * TreeMB.H(311, i);
                Vector3 c = new Vector3(Mathf.Cos(a) * rr, -0.20f, Mathf.Sin(a) * rr);
                Vector3 d = new Vector3(Mathf.Cos(a) * Mathf.Sin(tilt), Mathf.Cos(tilt), Mathf.Sin(a) * Mathf.Sin(tilt));
                l.Add(new Sh { c = c, d = d, len = 0.9f + 1.1f * TreeMB.H(312, i), rad = 0.13f + 0.12f * TreeMB.H(313, i) });
            }
            // на стволе
            float[] ys = { 1.5f, 2.2f, 2.9f };
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.4f + 0.8f;
                Vector3 dd = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 basePoint = TPos(trunk, TOfY(trunk, ys[i])) + dd * 0.13f;
                l.Add(new Sh { c = basePoint, d = (dd * 0.8f + Vector3.up * 0.6f).normalized, len = 0.8f + 0.3f * TreeMB.H(320, i), rad = 0.10f });
            }
            // в кроне
            for (int i = 0; i < 4; i++)
            {
                Vector3 dd = Az(45f + 90f * i);
                Vector3 branchPoint = CrystalBough(trunk, i)[2];
                l.Add(new Sh { c = branchPoint, d = (dd * 0.76f + Vector3.up).normalized, len = 0.95f, rad = 0.13f });
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
            var s = CrystalTrunk;
            Trunk(b, 0, s, 6, sd);
            Roots(b, 0, s, 5, 3.55f, sd, 31);
            Vector3 top = TPos(s, 1f);

            // Острые, разнесённые по высоте мятные вершины оставляют просвет над кристаллами.
            Vector3 high = top + Vector3.up * 1.02f;
            Leader(b, s, high, sd);
            CanopyPad(b, 1, high, new Vector3(1.06f, 1.21f, 0.96f), 301, lod);
            for (int i = 0; i < 4; i++)
            {
                float az = 45f + 90f * i;
                Vector3[] branch = CrystalBough(s, i);
                Bough(b, branch, 0.23f, 0.058f, sd);
                Vector3 tip = branch[branch.Length - 1];
                CanopyPad(b, 1, tip, new Vector3(1.02f, 1.12f, 0.92f), 305 + i, lod);
                if (lod == 0)
                    b.Blob(1, tip - Vector3.up * 0.27f - Az(az) * 0.38f, new Vector3(0.58f, 0.65f, 0.64f), 312 + i, 0, 0.15f, 3);
            }

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
            var s = new Tr { h = 3.4f, r0 = 0.59f, r1 = 0.30f, lean = 0.42f, twist = 3.2f, ph = 0.2f };
            Trunk(b, 0, s, 6, sd);
            Roots(b, 0, s, 6, 3.1f, sd, 41);
            Vector3 top = TPos(s, 1f);

            Vector3 high = top + new Vector3(-0.45f, 2.26f, 0.31f);
            Leader(b, s, high, sd);
            CanopyPad(b, 1, top + Vector3.up * 0.78f, new Vector3(1.92f, 1.12f, 1.75f), 401, lod);
            CanopyPad(b, 1, high, new Vector3(1.42f, 0.83f, 1.30f), 415, lod);
            List<Vector3> lanterns = LanternSpots();
            for (int i = 0; i < 4; i++)
            {
                int first = i * 2;
                Vector3 middle = (lanterns[first] + lanterns[first + 1]) * 0.5f;
                float az = Mathf.Atan2(middle.x, middle.z) * Mathf.Rad2Deg;
                Vector3[] limb = BoughPath(s, 2.65f + (i % 2) * 0.22f, az, 2.55f, 1.70f + 0.12f * i);
                Bough(b, limb, 0.30f, 0.074f, sd);
                Vector3 tip = limb[limb.Length - 1];
                CanopyPad(b, 1, tip, new Vector3(1.73f, 0.83f, 1.56f), 405 + i, lod);
                if (lod == 0)
                    b.Blob(1, tip + Az(az) * 0.63f + Vector3.up * 0.42f, new Vector3(0.95f, 0.48f, 0.83f), 410 + i, lv, 0.18f, 3);

                for (int j = 0; j < 2; j++)
                {
                    Vector3 lamp = lanterns[first + j];
                    Vector3 from = Vector3.Lerp(limb[1], limb[2], 0.40f + j * 0.28f);
                    var socket = new Vector3(lamp.x, Mathf.Max(from.y + 0.19f, lamp.y + 0.76f), lamp.z);
                    Vector3 bend = Vector3.Lerp(from, socket, 0.58f) + Vector3.up * 0.09f;
                    Bough(b, new[] { from, bend, socket }, 0.123f, 0.064f, lod == 0 ? 6 : 4);
                    DrawLantern(b, lamp, socket, lod == 0);
                }
            }
            return b.Build(name);
        }

        static void DrawLantern(TreeMB b, Vector3 lamp, Vector3 socket, bool detail)
        {
            Vector3 upper = lamp + Vector3.up * 0.21f;
            Vector3 lower = lamp - Vector3.up * 0.22f;
            b.Tube(0, new[] { socket, upper }, new[] { 0.028f, 0.024f }, 4, false);
            b.Tube(2, new[] { lower, lamp + Vector3.up * 0.04f, upper }, new[] { 0.132f, 0.177f, 0.135f }, detail ? 6 : 4, false);
            GroveGeo.Box(b, 0, upper, new Vector3(0.151f, 0.043f, 0.151f), 0f);
            GroveGeo.Box(b, 0, lower, new Vector3(0.143f, 0.035f, 0.143f), 0f);
            if (!detail) return;

            for (int i = 0; i < 3; i++)
            {
                Vector3 rib = Az(30f + 120f * i);
                b.Tube(0, new[] { lower + rib * 0.124f, lamp + rib * 0.168f, upper + rib * 0.125f },
                    new[] { 0.014f, 0.018f, 0.014f }, 4, false);
            }
        }

        // ============================================================ 5. СОННЫЙ СТРАЖ
        // Лицо лежит на гранях именно этого LOD.
        static float FrontZ(Tr s, int sides, float x, float y)
        {
            float t = TOfY(s, y) * 6f;
            int lo = Mathf.Min(5, Mathf.FloorToInt(t));
            float radius = Mathf.Lerp(TRad(s, lo / 6f), TRad(s, (lo + 1) / 6f), t - lo);
            float z = 0f;
            for (int i = 0; i < sides; i++)
            {
                float a = 2f * Mathf.PI * i / sides, d = 2f * Mathf.PI * (i + 1) / sides;
                float xa = Mathf.Cos(a) * radius, xb = Mathf.Cos(d) * radius;
                if (x < Mathf.Min(xa, xb) || x > Mathf.Max(xa, xb) || Mathf.Abs(xb - xa) < 0.0001f) continue;
                z = Mathf.Max(z, Mathf.Lerp(Mathf.Sin(a), Mathf.Sin(d), (x - xa) / (xb - xa)) * radius);
            }
            return z;
        }

        static void Arc(TreeMB b, Tr s, int sides, float cx, float cy, float w, float dip, float rad)
        {
            const int pts = 5;
            var p = new Vector3[pts];
            var r = new float[pts];
            for (int i = 0; i < pts; i++)
            {
                float u = i / (pts - 1f) * 2f - 1f;
                float x = cx + u * w;
                float y = cy - dip * (1f - u * u);
                float z = FrontZ(s, sides, x, y) + 0.006f;
                p[i] = new Vector3(x, y, z);
                r[i] = rad;
            }
            b.Tube(3, p, r, 4, false);
        }

        static Mesh Sleeper(int lod, string name)
        {
            var b = new TreeMB();
            int lv = lod == 0 ? 1 : 0, sd = lod == 0 ? 8 : 5;
            var s = new Tr { h = 3.4f, r0 = 0.95f, r1 = 0.72f, lean = 0f, twist = 0f, ph = 0f };
            Trunk(b, 0, s, 6, sd);
            Roots(b, 0, s, 6, 2.50f, sd, 51);

            // лицо: закрытые глазки, улыбка, носик-сучок
            float eyeY = 2.05f;
            Arc(b, s, sd, -0.30f, eyeY, 0.18f, 0.05f, 0.043f);
            Arc(b, s, sd, 0.30f, eyeY, 0.18f, 0.05f, 0.043f);
            Arc(b, s, sd, 0f, 1.55f, 0.17f, 0.06f, 0.036f);
            b.Blob(0, new Vector3(0f, 1.8f, FrontZ(s, sd, 0f, 1.8f)), new Vector3(0.097f, 0.108f, 0.098f), 520, 0, 0.2f, -1);

            // Крона как тяжёлая мшистая крыша, с двумя вилами позади лица.
            Vector3 cc = new Vector3(-0.24f, 4.95f, -0.33f);
            Leader(b, s, cc, sd);
            b.Blob(1, cc, new Vector3(2.95f, 1.55f, 2.72f), 501, lv, 0.20f, 3);
            Bough(b, s, 2.81f, 117f, 2.12f, 1.51f, 0.38f, sd);
            Bough(b, s, 2.54f, 237f, 2.02f, 1.69f, 0.37f, sd);
            for (int i = 0; i < 5; i++)
            {
                Vector3 brim = Az(72f * i + 5f) * (2.06f + 0.38f * TreeMB.H(503, i));
                Vector3 c = cc + brim + Vector3.down * (0.59f + 0.49f * TreeMB.H(502, i));
                b.Blob(1, c, new Vector3(1.73f, 0.77f, 1.5f), 505 + i, lv, 0.22f, 3);
            }
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = cc + new Vector3(0.80f - 0.61f * i, 1.20f - i * 0.06f, -0.33f + 0.24f * i);
                b.Blob(2, c, new Vector3(0.78f, 0.44f, 0.79f), 515 + i, lv, 0.19f, 3);
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
