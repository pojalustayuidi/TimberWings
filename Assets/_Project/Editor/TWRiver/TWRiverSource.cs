// Assets/_Project/Editor/TWRiver/TWRiverSource.cs
// Исток: цельный скальный массив на кромке острова, из открытого грота вытекает вода.
// Как построен: «станции» поперёк реки от кромки до устья. На каждой станции один и тот же контур (слоистые уступы с двух сторон, гребень, арка проёма),
// контуры соединены гранями. Низ контура всегда под водой и под землёй, поэтому ничего не висит. Спереди проём остаётся открытым и тёмным внутри,
// сзади массив прижат к обрыву. Вода (River_Water) идёт внутри грота и затемняется шейдером.
using System.Collections.Generic;
using UnityEngine;

namespace IslandV2
{
    internal static class TWRiverSource
    {
        const int Ks = 3;     // уступов на каждом склоне
        const int Na = 9;     // точек в арке

        static void Flank(List<Vector2> dst, float A, float Wc, float yb, float yRef, float yTop, int seed)
        {
            dst.Add(new Vector2(A, yb));
            float prevO = A;
            for (int k = 1; k <= Ks; k++)
            {
                float fk = (float)k / Ks;
                float yk = yRef + (yTop - yRef) * Mathf.Pow(fk, 0.85f) + (TWNoise.Hash(k, seed, 11) - 0.5f) * 0.35f;
                float oK = Mathf.Max(Wc, Mathf.Lerp(A, Wc, fk) - TWNoise.Hash(k, seed, 12) * 0.35f);
                float riserO = prevO - (k == 1 ? 0.15f : 0.14f * TWNoise.Hash(k, seed, 13));
                dst.Add(new Vector2(riserO, yk));                                                         // верх вертикальной стенки уступа
                dst.Add(new Vector2(oK, yk + 0.10f + 0.22f * TWNoise.Hash(k, seed, 14)));                // полка, чуть вверх внутрь
                prevO = oK;
            }
        }

        static List<Vector2> Profile(TWRiverCtx x, int i, float t, float s, float yw, float baseL, float baseR,
                                     out int idxFootR, out int idxFootL)
        {
            int seed = x.seed + i * 17;
            float A = x.half + 5.3f + 0.6f * TWNoise.Hash(i, 1, seed);
            float Wc = 2.2f + 1.1f * TWNoise.Hash(i, 2, seed);
            float crest = Mathf.Lerp(6.4f, 5.0f, t) + 0.35f * TWNoise.Hash(i, 3, seed);
            float topRock = crest, topSoft = crest - 1.25f - 0.4f * TWNoise.Hash(i, 4, seed);
            float topL = x.rockSide > 0 ? topRock : topSoft;
            float topR = x.rockSide > 0 ? topSoft : topRock;

            var poly = new List<Vector2>();
            Flank(poly, A, Wc, baseL, yw, yw + topL, seed + 1);                           // слева (off > 0) от основания до гребня
            poly.Add(new Vector2(Wc * 0.35f, yw + topL + 0.30f + 0.35f * TWNoise.Hash(i, 5, seed)));
            poly.Add(new Vector2(-Wc * 0.20f, yw + Mathf.Max(topL, topR) + 0.15f + 0.30f * TWNoise.Hash(i, 6, seed)));
            poly.Add(new Vector2(-Wc * 0.85f, yw + topR + 0.20f));
            var R = new List<Vector2>();
            float Ar = A - 0.3f + 0.6f * TWNoise.Hash(i, 7, seed);
            Flank(R, Ar, Wc * 0.9f, baseR, yw, yw + topR, seed + 2);
            for (int k = R.Count - 1; k >= 0; k--) poly.Add(new Vector2(-R[k].x, R[k].y));   // справа: зеркально, от гребня вниз

            float wm = TWRiverSpec.MouthHalf * (0.86f + 0.14f * t);
            float hm = TWRiverSpec.MouthHeight * (0.80f + 0.20f * t);
            float sp = 0.9f;
            float yFoot = yw - 1.1f;
            idxFootR = poly.Count;
            poly.Add(new Vector2(-wm, yFoot));
            for (int m = 0; m < Na; m++)
            {
                float phi = Mathf.PI * m / (Na - 1);
                float jit = (m == 0 || m == Na - 1) ? 0f : 0.05f * (TWNoise.Hash(m, i, seed + 9) - 0.5f);
                poly.Add(new Vector2(-wm * Mathf.Cos(phi) * (1f + jit), yw + sp + (hm - sp) * Mathf.Sin(phi) * (1f + jit)));
            }
            idxFootL = poly.Count;
            poly.Add(new Vector2(wm, yFoot));
            return poly;
        }

        static Vector3 To3(Vector2 A, Vector2 C, Vector2 p) { return new Vector3(A.x + C.x * p.x, p.y, A.y + C.y * p.x); }

        public static void Build(TWRiverCtx x)
        {
            var f = x.f;
            var ax = x.ax;
            float Lm = TWRiverSpec.MouthDist;
            float[] sSt = { -0.25f, 0.6f, 2.4f, 4.4f, 6.0f, Lm };
            int nS = sSt.Length;
            var A = new Vector2[nS]; var C = new Vector2[nS]; var yws = new float[nS];
            var P = new List<Vector2>[nS];
            int footR = 0, footL = 0;
            for (int i = 0; i < nS; i++)
            {
                float s = sSt[i];
                A[i] = ax.At(s);
                C[i] = ax.Side(Mathf.Max(0f, s));
                yws[i] = x.Yw(s);
                // основание углов: ниже воды и ниже земли в этой точке
                float Aest = x.half + 5.6f;
                float gl = f.Height(A[i].x + C[i].x * Aest, A[i].y + C[i].y * Aest);
                float gr = f.Height(A[i].x - C[i].x * Aest, A[i].y - C[i].y * Aest);
                float baseL = Mathf.Min(yws[i] - 1.2f, gl - 0.9f);
                float baseR = Mathf.Min(yws[i] - 1.2f, gr - 0.9f);
                P[i] = Profile(x, i, (float)i / (nS - 1), s, yws[i], baseL, baseR, out footR, out footL);
            }
            int N = P[0].Count;
            Vector2 T0 = ax.Tangent(Lm);
            Vector3 flow3 = new Vector3(T0.x, 0f, T0.y);

            // ------------- грани между станциями
            for (int st = 0; st + 1 < nS; st++)
            {
                for (int e = 0; e < N; e++)
                {
                    int e2 = (e + 1) % N;
                    bool bottomEdge = (e == footR - 1) || (e == footL);          // низ под землёй и водой: не строим
                    bool interior = e >= footR && e <= footL - 1;                // стенки грота
                    if (bottomEdge) continue;
                    if (interior && st == 0) continue;                           // на отрезке до заглушки грота нет
                    Vector3 a0 = To3(A[st], C[st], P[st][e]), a1 = To3(A[st], C[st], P[st][e2]);
                    Vector3 b0 = To3(A[st + 1], C[st + 1], P[st + 1][e]), b1 = To3(A[st + 1], C[st + 1], P[st + 1][e2]);
                    Vector2 d = P[st][e2] - P[st][e];
                    if (d.sqrMagnitude < 1e-8f) continue;
                    Vector2 ln = new Vector2(-d.y, d.x).normalized;               // влево от ребра: внутрь тела (контур против часовой)
                    Vector2 mid2 = (P[st][e] + P[st][e2]) * 0.5f + ln * 0.12f;
                    Vector3 inside = To3((A[st] + A[st + 1]) * 0.5f, (C[st] + C[st + 1]).normalized, mid2);
                    Vector3 cen = (a0 + a1 + b0 + b1) * 0.25f;
                    float yw = (yws[st] + yws[st + 1]) * 0.5f;
                    float hash = TWNoise.Hash(e, st, x.seed + 55);
                    Color col;
                    if (interior)
                    {
                        float depth = 1f - ((sSt[st] + sSt[st + 1]) * 0.5f - 0.6f) / (Lm - 0.6f);
                        col = x.pal.Cave(depth);
                    }
                    else
                    {
                        Vector3 nn = Vector3.Cross(a1 - a0, b0 - a0).normalized;
                        if (Vector3.Dot(nn, cen - inside) < 0f) nn = -nn;
                        bool crest = e >= (1 + 2 * Ks) - 1 && e <= (1 + 2 * Ks) + 3 + 1;
                        col = x.pal.Rock(cen.y - yw, nn, hash, crest ? 0.55f : (hash > 0.7f ? 0.25f : 0f));
                    }
                    x.rocks.TriAway(a0, a1, b1, col, inside);
                    x.rocks.TriAway(a0, b1, b0, col, inside);
                }
            }

            // ------------- крышки: фасад спереди (с открытым проёмом), задняя стенка у обрыва, заглушки грота
            AddCap(x, P[nS - 1], A[nS - 1], C[nS - 1], flow3, true, false, yws[nS - 1], footR, footL);
            AddCap(x, P[0], A[0], C[0], -flow3, false, false, yws[0], footR, footL);
            AddPlug(x, P[0], A[0], C[0], -flow3, footR, footL, false, 0f, yws[0]);          // задняя сторона: проём закрыт скалой
            AddPlug(x, P[1], A[1], C[1], flow3, footR, footL, true, 1f, yws[1]);            // конец тоннеля: тёмная стенка

            // якоря заземления: нижние углы контуров
            for (int i = 0; i < nS; i++)
            {
                x.rocks.anchors.Add(To3(A[i], C[i], P[i][0]));
                x.rocks.anchors.Add(To3(A[i], C[i], P[i][footR - 1]));
            }

            // ------------- прижимаем заднюю часть к обрыву: валуны на кромке и по бокам
            Extras(x, sSt, A, C, yws, P);
        }

        static void AddCap(TWRiverCtx x, List<Vector2> poly, Vector2 A, Vector2 C, Vector3 faceDir, bool front, bool dummy, float yw, int footR, int footL)
        {
            var tri = TWGeo.EarClip(poly);
            for (int i = 0; i + 2 < tri.Count; i += 3)
            {
                Vector3 a = To3(A, C, poly[tri[i]]), b = To3(A, C, poly[tri[i + 1]]), c = To3(A, C, poly[tri[i + 2]]);
                Vector3 cen = (a + b + c) / 3f;
                float hash = TWNoise.Hash(i, 3, x.seed + 91);
                Vector3 nn = faceDir;
                Color col = x.pal.Rock(cen.y - yw, nn, hash, 0f);
                x.rocks.TriToward(a, b, c, front ? Lighter(col) : col, cen + faceDir);
            }
        }

        static Color Lighter(Color c) { return new Color(Mathf.Clamp01(c.r * 1.04f), Mathf.Clamp01(c.g * 1.04f), Mathf.Clamp01(c.b * 1.05f), 1f); }

        // заполняем арку многоугольником: на задней стороне скалой, на конце тоннеля тёмной стенкой
        static void AddPlug(TWRiverCtx x, List<Vector2> poly, Vector2 A, Vector2 C, Vector3 faceDir, int footR, int footL, bool dark, float depth, float yw)
        {
            var hole = new List<Vector2>();
            for (int i = footR; i <= footL; i++) hole.Add(poly[i]);
            var tri = TWGeo.EarClip(hole);
            for (int i = 0; i + 2 < tri.Count; i += 3)
            {
                Vector3 a = To3(A, C, hole[tri[i]]), b = To3(A, C, hole[tri[i + 1]]), c = To3(A, C, hole[tri[i + 2]]);
                Vector3 cen = (a + b + c) / 3f;
                Color col = dark ? x.pal.Cave(1f) : x.pal.Rock(cen.y - yw, faceDir, TWNoise.Hash(i, 8, x.seed), 0f);
                x.rocks.TriToward(a, b, c, col, cen + faceDir);
            }
        }

        static void Extras(TWRiverCtx x, float[] sSt, Vector2[] A, Vector2[] C, float[] yws, List<Vector2>[] P)
        {
            var f = x.f;
            float Lm = TWRiverSpec.MouthDist;
            int seed = x.seed + 301;
            float half = x.half;
            float yMouth = x.Yw(Lm);
            TWStone.ColorFn rockCol = (wy, n, h) => x.pal.Rock(wy - yMouth, n, h, 0.25f);

            // валуны в воде по бокам устья: проём сужается, вода выходит из тёмного отверстия
            float[] sFront = { Lm + 0.9f, Lm + 0.6f, Lm + 1.6f, Lm + 3.4f };
            float[] oFront = { TWRiverSpec.MouthHalf + 1.0f, TWRiverSpec.MouthHalf + 2.7f, TWRiverSpec.MouthHalf + 4.0f, 0.5f };
            float[] rFront = { 1.25f, 1.55f, 1.35f, 0.55f };
            for (int sd = -1; sd <= 1; sd += 2)
            {
                for (int k = 0; k < 3; k++)
                {
                    float s = sFront[k] + (TWNoise.Hash(k, sd, seed) - 0.5f) * 0.5f;
                    float off = sd * (oFront[k] + (TWNoise.Hash(k, sd, seed + 2) - 0.5f) * 0.4f);
                    float r = rFront[k];
                    float yw = x.Yw(s);
                    Vector3 c = x.P(s, off, yw + 0.12f * r);
                    TWStone.Build(x.rocks, c, new Vector3(r * 1.1f, r * 0.85f, r * 1.25f), TWNoise.Hash(k, sd, seed + 3) * 3f, seed + k * 5 + sd, rockCol, 0.6f);
                    x.mossSpots.Add(new Vector4(c.x, c.y + r * 0.8f, c.z, r * 0.7f));
                    x.stones.Add(new Vector4(s, off, yw, r));
                }
            }
            {
                // одиночный камень по центру ниже устья: делит струю на две
                float s = sFront[3];
                float yw = x.Yw(s);
                Vector3 c = x.P(s, 0.5f, yw + 0.05f);
                TWStone.Build(x.rocks, c, new Vector3(0.7f, 0.5f, 0.85f), 0.4f, seed + 77, rockCol, 0.5f);
                x.stones.Add(new Vector4(s, 0.5f, yw, 0.6f));
            }

            // крупные валуны на углах массива: смягчают ступенчатый силуэт, сидят в земле
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float[] ss = { Lm - 0.6f, Lm - 4.2f, 1.6f };
                for (int k = 0; k < ss.Length; k++)
                {
                    float s = ss[k];
                    float off = sd * (half + 5.6f + 0.4f + TWNoise.Hash(k, sd, seed + 8) * 0.8f);
                    float r = 1.5f + 0.7f * TWNoise.Hash(k, sd, seed + 9);
                    Vector3 g = x.OnGround(s, off, 0f);
                    Vector3 c = new Vector3(g.x, g.y + r * 0.15f, g.z);
                    TWStone.Build(x.rocks, c, new Vector3(r, r * 0.8f, r * 1.2f), TWNoise.Hash(k, sd, seed + 10) * 3f, seed + 40 + k * 3 + sd, rockCol, 0.7f);
                    x.mossSpots.Add(new Vector4(c.x, c.y + r * 0.7f, c.z, r * 0.8f));
                }
            }

            // валуны на гребне: нарушают ровную линию
            for (int k = 0; k < 4; k++)
            {
                float s = Mathf.Lerp(1.2f, Lm - 1.5f, (k + 0.5f) / 4f);
                float off = (TWNoise.Hash(k, 3, seed + 12) - 0.5f) * 5.2f;
                float t = s / Lm;
                bool rockSideUp = (off > 0f) == (x.rockSide > 0);
                float crestY = yws[1] + Mathf.Lerp(6.4f, 5.0f, t) - (rockSideUp ? 0f : 1.25f) - 0.1f;
                float r = 0.8f + 0.7f * TWNoise.Hash(k, 4, seed + 13);
                Vector3 c = x.P(s, off, crestY);
                TWStone.Build(x.rocks, c, new Vector3(r, r * 0.7f, r * 1.1f), TWNoise.Hash(k, 5, seed + 14) * 3f, seed + 60 + k, rockCol, 0.6f);
                x.mossSpots.Add(new Vector4(c.x, c.y + r * 0.55f, c.z, r * 0.9f));
            }
        }
    }
}
