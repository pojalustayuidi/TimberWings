#if UNITY_EDITOR
// =====================================================================================
//  Assets/_Project/Editor/Location/Modules/LocationHeadMountain.cs
//  LocationHeadMountain - большая скала-гора у истока реки.
//
//  Как устроено:
//   * В высотах Terrain (LocationGen.Core.cs, HeadRidge) лежит только «ядро» ~0.5 от высоты: оно держит коллизию
//     и спрятано внутри меша.
//   * Здесь строится слоистый меш: замкнутый контур вокруг оси (нос у воды -> хребет вдоль русла -> северный торец),
//     поднятый пластами. Каждый пласт = нижняя и верхняя часть стены (две полосы граней со смещением вершин) + уступ-полка.
//     Полки сужаются кверху, часть пластов нависает. Сверху гребень с шпилями, по бокам контрфорсы, у подножия валуны.
//   * Передняя стена по оси реки отдаётся в LocationRiver.BuildSpringFall (ctx.headFace): струйка течёт по уступам.
//   * Коллайдер: один MeshCollider по всей оболочке + невидимые боксы внутри массива (на случай прыжка / дыр в меше).
//  Запускается ДО реки (LocationModules): ручью нужен профиль стены.
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LocationHeadMountain
{
    readonly LocationCtx c;
    readonly System.Random rng;
    Transform node;

    readonly Dictionary<string, LocMB> mbs = new Dictionary<string, LocMB>();
    readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    LocMB colMb;

    // ось массива (в u)
    int n;
    float[] X, Z, W, crest;
    float y0, wy;
    float frontD0, northD0;
    float seedO;
    float rough;
    int nc, fc;                 // точек в северной и передней дугах

    // индексы периметра
    int M;
    Vector2[] anchor;
    float[] crestAt;

    LocationHeadMountain(LocationCtx ctx)
    {
        c = ctx;
        rng = new System.Random(ctx.seed * 29 + 101);
    }

    public static void Build(LocationCtx ctx) { new LocationHeadMountain(ctx).Run(); }

    // ------------------------------------------------------------------ утилиты
    float Rf(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    Color E(int i) { return c.s.envColors[i]; }
    static float SS(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / Mathf.Max(1e-5f, b - a));
        return t * t * (3f - 2f * t);
    }
    float Nz(float x, float z, float o) { return Mathf.PerlinNoise(x * 0.31f + o + 100f, z * 0.31f + o * 1.7f + 100f); }
    static float PowS(float v, float p) { return Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), p); }

    LocMB MB(string key)
    {
        LocMB m;
        if (!mbs.TryGetValue(key, out m)) { m = new LocMB(); mbs[key] = m; }
        return m;
    }

    void Tri(string key, Vector3 a, Vector3 b, Vector3 cc, Vector3 outward)
    {
        MB(key).Tri(a, b, cc, outward);
        colMb.Tri(a, b, cc, outward);
    }

    void Quad(string key, Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector3 outward)
    {
        Tri(key, a, b, cc, outward);
        Tri(key, a, cc, d, outward);
    }

    // ------------------------------------------------------------------ запуск
    void Run()
    {
        var s = c.s;
        if (!s.riverFullLength || !s.riverGorge || !s.riverHead) return;

        wy = c.waterLevel;
        rough = s.headRough;
        seedO = s.seed * 0.37f;
        colMb = new LocMB();

        // 1. где кончается вода: там встаёт нос горы
        float srcZ = c.riverSrcZ;
        float sw = srcZ - 6f;
        while (sw < srcZ + 8f && c.heightU(c.riverX(sw + 0.25f), sw + 0.25f) < wy - 0.05f) sw += 0.25f;
        frontD0 = 4.6f;
        northD0 = 3.0f;
        float frontS = sw + 0.5f;
        float z0 = frontS + frontD0;

        // 2. северный конец: до края острова
        float zEnd = c.half - 1.2f;
        for (float z = z0 + 4f; z < c.half; z += 0.5f)
            if (c.edgeIn(c.riverX(z), z) < 1.0f) { zEnd = z; break; }
        zEnd = Mathf.Min(Mathf.Max(zEnd, z0 + 4f), c.half - 0.8f);

        BuildAxis(z0, zEnd);

        var go = new GameObject("HeadMountain");
        go.transform.SetParent(c.root, false);
        go.transform.localScale = Vector3.one * c.ws;
        node = go.transform;

        BuildStrata();
        BuildCrags();
        BuildBoulders();
        BuildColliders();
        Flush();
    }

    // ------------------------------------------------------------------ ось, ширина, высота гребня
    void BuildAxis(float z0, float zEnd)
    {
        var s = c.s;
        float H = s.headHeight;
        n = Mathf.Max(3, Mathf.CeilToInt((zEnd - z0) / 1.3f));
        X = new float[n + 1]; Z = new float[n + 1]; W = new float[n + 1]; crest = new float[n + 1];
        y0 = wy - 2.2f;

        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            float z = Mathf.Lerp(z0, zEnd, t);
            Z[i] = z;
            X[i] = c.riverX(z) + (Nz(z * 0.5f, 3f, 1f) - 0.5f) * 1.4f * rough;
            float w = s.headHalfWidth * (0.92f + 0.36f * Nz(z * 0.4f, 0f, 7f));
            W[i] = w * Mathf.Lerp(1f, 0.82f, SS(0.7f, 1f, t));
            float h = H * (0.82f + 0.30f * Nz(z * 0.22f, 0f, 21f)) * Mathf.Lerp(1f, 0.9f, SS(0.55f, 1f, t));
            crest[i] = Mathf.Max(h, c.heightU(X[i], Z[i]) + 0.5f * H);
        }
        // сгладить гребень, чтобы не было иголок
        for (int pass = 0; pass < 2; pass++)
            for (int i = 1; i < n; i++) crest[i] = 0.25f * crest[i - 1] + 0.5f * crest[i] + 0.25f * crest[i + 1];
    }

    // ------------------------------------------------------------------ периметр
    int FrontCenterIdx() { return 2 * (n + 1) + nc + (fc - 1) / 2; }

    void PrepPerimeter()
    {
        nc = 5; fc = 9;
        M = 2 * (n + 1) + nc + fc;
        anchor = new Vector2[M];
        crestAt = new float[M];
        for (int i = 0; i <= n; i++)
        {
            anchor[i] = new Vector2(X[i], Z[i]); crestAt[i] = crest[i];
            int li = n + 1 + nc + (n - i);
            anchor[li] = new Vector2(X[i], Z[i]); crestAt[li] = crest[i];
        }
        for (int j = 0; j < nc; j++) { int idx = n + 1 + j; anchor[idx] = new Vector2(X[n], Z[n]); crestAt[idx] = crest[n] * 0.93f; }
        for (int j = 0; j < fc; j++) { int idx = 2 * (n + 1) + nc + j; anchor[idx] = new Vector2(X[0], Z[0]); crestAt[idx] = crest[0]; }
    }

    // контур одного кольца: insetS - сужение боков, insetF - отступ передней стены, fr - доля высоты 0..1, amp - рваность
    Vector3[] MakeRing(float insetS, float insetF, float fr, float seedK, float ySeed, float amp)
    {
        var p = new Vector3[M];
        float[] xs = new float[M], zs = new float[M];
        for (int i = 0; i <= n; i++)
        {
            float wR = Mathf.Max(0.9f, W[i] - insetS);
            xs[i] = X[i] + wR; zs[i] = Z[i];
            int li = n + 1 + nc + (n - i);
            xs[li] = X[i] - wR; zs[li] = Z[i];
        }
        float wN = Mathf.Max(0.9f, W[n] - insetS);
        float dN = Mathf.Max(0.5f, northD0 - insetS * 0.45f);
        for (int j = 0; j < nc; j++)
        {
            float a = Mathf.PI * (j + 1) / (nc + 1);
            int idx = n + 1 + j;
            xs[idx] = X[n] + PowS(Mathf.Cos(a), 0.65f) * wN;
            zs[idx] = Z[n] + Mathf.Pow(Mathf.Sin(a), 0.65f) * dN;
        }
        float w0 = Mathf.Max(0.9f, W[0] - insetS);
        float dF = Mathf.Max(0.3f, frontD0 - insetF);
        for (int j = 0; j < fc; j++)
        {
            float a = Mathf.PI + Mathf.PI * (j + 1) / (fc + 1);
            int idx = 2 * (n + 1) + nc + j;
            xs[idx] = X[0] + PowS(Mathf.Cos(a), 0.65f) * w0;
            zs[idx] = Z[0] + PowS(Mathf.Sin(a), 0.65f) * dF;      // sin<0: на юг, к воде
        }

        for (int k = 0; k < M; k++)
        {
            Vector2 dir = new Vector2(xs[k] - anchor[k].x, zs[k] - anchor[k].y);
            float mag = dir.magnitude;
            if (mag > 1e-4f) dir /= mag;
            float off = (Nz(xs[k], zs[k], seedK) - 0.5f) * 2f * amp + (float)(rng.NextDouble() - 0.5) * 0.35f * amp;
            if (k >= 2 * (n + 1) + nc)          // перед ручьём стена спокойнее: профиль водопада не должен петлять
                off *= Mathf.Lerp(0.12f, 1f, SS(0.5f, 2.6f, Mathf.Abs(xs[k] - X[0])));
            float x = xs[k] + dir.x * off;
            float z = zs[k] + dir.y * off;
            float y = y0 + (crestAt[k] - y0) * fr;
            if (fr > 0.001f && fr < 0.999f) y += (Nz(anchor[k].x * 1.7f + k * 0.61f, anchor[k].y * 1.3f, ySeed) - 0.5f) * 0.55f;   // уступы идут не по линейке; шум по индексу периметра, не по смещённой позиции
            p[k] = new Vector3(x, y, z);
        }
        return p;
    }

    // полоса между двумя кольцами: wall = боковая стена (нормаль наружу по горизонтали), иначе полка (вверх / вниз если нависает)
    void Strip(string key, Vector3[] A, Vector3[] B, bool wall)
    {
        for (int k = 0; k < M; k++)
        {
            int j = (k + 1) % M;
            Vector3 mid = (A[k] + A[j] + B[k] + B[j]) * 0.25f;
            Vector3 radial = new Vector3(mid.x - anchor[k].x, 0f, mid.z - anchor[k].y);
            Vector3 outward;
            if (wall) outward = radial.sqrMagnitude > 1e-6f ? radial : Vector3.forward;
            else
            {
                Vector3 mA = (A[k] + A[j]) * 0.5f, mB = (B[k] + B[j]) * 0.5f;
                float grow = Vector3.Dot(new Vector3(mB.x - mA.x, 0f, mB.z - mA.z), radial.normalized);
                outward = grow > 0.05f ? Vector3.down : Vector3.up;
            }
            Quad(key, A[k], A[j], B[j], B[k], outward);
        }
    }

    // ------------------------------------------------------------------ пласты
    void BuildStrata()
    {
        var s = c.s;
        PrepPerimeter();
        int L = s.headStrata;
        int fci = FrontCenterIdx();

        // палитра пластов
        int[] pal = { 16, 1, 0, 2, 22 };
        int shift = rng.Next(pal.Length);
        var wallLo = new string[L]; var wallHi = new string[L];
        for (int k = 0; k < L; k++)
        {
            Color baseC = E(pal[(k + shift) % pal.Length]);
            float shade = Mathf.Lerp(0.80f, 1.10f, k / (float)Mathf.Max(1, L - 1));
            Color lo = new Color(baseC.r * shade * 0.90f, baseC.g * shade * 0.90f, baseC.b * shade * 0.90f, 1f);
            Color hi = new Color(baseC.r * shade, baseC.g * shade, baseC.b * shade, 1f);
            wallLo[k] = "HeadRock_" + k + "a"; wallHi[k] = "HeadRock_" + k + "b";
            mats[wallLo[k]] = LocKit.Mat(wallLo[k], lo, 0.05f);
            mats[wallHi[k]] = LocKit.Mat(wallHi[k], hi, 0.05f);
        }
        mats["HeadLedge"] = LocKit.Mat("HeadLedge", Color.Lerp(E(17), E(16), 0.35f), 0.05f);     // полки: мох с землёй
        mats["HeadCap"] = LocKit.Mat("HeadCap", E(17), 0.05f);
        mats["HeadUnder"] = LocKit.Mat("HeadUnder", Color.Lerp(E(16), Color.black, 0.45f), 0.05f); // низ нависаний
        mats["HeadCleft"] = LocKit.Mat("HeadCleft", new Color(0.07f, 0.08f, 0.09f), 0.02f);
        mats["HeadCrag"] = LocKit.Mat("HeadCrag", E(pal[shift]), 0.05f);

        // доли высоты: нижние пласты толще
        var fr = new float[L + 1];
        for (int k = 0; k <= L; k++)
        {
            float t = k / (float)L;
            fr[k] = Mathf.Pow(t, 0.92f);
            if (k > 0 && k < L) fr[k] += (float)(rng.NextDouble() - 0.5) * 0.03f;
        }

        float wMin = float.MaxValue;
        for (int i = 0; i <= n; i++) wMin = Mathf.Min(wMin, W[i]);
        float depthBase = Mathf.Max(0.3f, (wMin - 1.6f)) / (float)L;      // суммарное сужение ~ до гребня шириной ~1.6 u

        float cumS = 0f, cumF = 0f;
        var facePts = new List<Vector2>();        // (z, y) передней стены по центру, снизу вверх
        int springLayer = Mathf.Clamp(Mathf.RoundToInt(L * 0.55f), 2, L - 1);
        float springY = 0f;
        float springZIn = 0f;
        Vector3[] prevTop = null;
        float prevTopInset = 0f;

        for (int k = 0; k < L; k++)
        {
            float lean = Rf(0.05f, 0.22f) * (1f + rough);
            float bS = cumS, bF = cumF;
            float tS = cumS + lean, tF = cumF + lean * 1.2f;
            float mS = (bS + tS) * 0.5f + Rf(-0.15f, 0.30f) * rough;
            float mF = (bF + tF) * 0.5f;

            Vector3[] B = MakeRing(bS, bF, fr[k], 10f + k * 3f, 200f + k * 7f, 0.55f * rough);
            Vector3[] Md = MakeRing(mS, mF, (fr[k] + fr[k + 1]) * 0.5f, 11f + k * 3f, 400f + k * 7f, 0.95f * rough);
            Vector3[] T = MakeRing(tS, tF, fr[k + 1], 12f + k * 3f, 200f + (k + 1) * 7f, 0.55f * rough);

            if (prevTop != null)
            {
                // полка между верхом предыдущего пласта и низом этого; если пласт нависает - у полки видна тёмная нижняя сторона
                bool over = bS < prevTopInset - 0.01f;
                Strip(over ? "HeadUnder" : "HeadLedge", prevTop, B, false);
            }

            Strip(wallLo[k], B, Md, true);
            Strip(wallHi[k], Md, T, true);

            // профиль стены по центру носа
            facePts.Add(new Vector2(B[fci].z, B[fci].y));
            facePts.Add(new Vector2(Md[fci].z, Md[fci].y));
            facePts.Add(new Vector2(T[fci].z, T[fci].y));
            if (k == springLayer) { springY = B[fci].y; springZIn = B[fci].z; }

            prevTop = T;
            prevTopInset = tS;

            // следующий уступ: полка глубиной depth; иногда пласт нависает (только по бокам, перед носа всегда отступ)
            float depth = depthBase * Rf(0.45f, 1.7f);
            bool overhang = k > 0 && k < L - 2 && rng.NextDouble() < 0.22 * (0.5 + rough);
            cumS = Mathf.Max(0f, tS + (overhang ? -Rf(0.3f, 0.8f) : depth));
            cumF = tF + Mathf.Max(0.45f, depth * Rf(0.8f, 1.4f));
            cumS = Mathf.Min(cumS, wMin - 1.4f);
        }

        CrestCap(prevTop);
        BuildFace(facePts, springY, springZIn);
    }

    // верхняя площадка: гребень по оси слегка вздут, чтобы читался хребет
    void CrestCap(Vector3[] T)
    {
        var C = new Vector3[n + 1];
        for (int i = 0; i <= n; i++)
            C[i] = new Vector3(X[i], T[i].y + 0.6f + 0.9f * Nz(Z[i] * 0.6f, 4f, 33f) * rough, Z[i]);

        for (int i = 0; i < n; i++)
        {
            int ri = i, rj = i + 1;
            int li = n + 1 + nc + (n - i), lj = n + 1 + nc + (n - (i + 1));
            Quad("HeadCap", T[ri], T[rj], C[i + 1], C[i], Vector3.up);
            Quad("HeadCap", T[lj], T[li], C[i], C[i + 1], Vector3.up);
        }
        // северный торец: веер от C[n] по дуге right_n -> дуга -> left_n
        int prev = n;
        for (int j = 0; j < nc; j++)
        {
            int idx = n + 1 + j;
            Tri("HeadCap", C[n], T[prev], T[idx], Vector3.up);
            prev = idx;
        }
        Tri("HeadCap", C[n], T[prev], T[n + 1 + nc], Vector3.up);
        // передний торец: left_0 -> дуга -> right_0
        prev = n + 1 + nc + n;          // left_0
        for (int j = 0; j < fc; j++)
        {
            int idx = 2 * (n + 1) + nc + j;
            Tri("HeadCap", C[0], T[prev], T[idx], Vector3.up);
            prev = idx;
        }
        Tri("HeadCap", C[0], T[prev], T[0], Vector3.up);
    }

    // ------------------------------------------------------------------ передняя стена: профиль для ручья + расщелина
    void BuildFace(List<Vector2> pts, float springY, float zIn)
    {
        // обрезаем по воде снизу и по высоте родника сверху
        var res = new List<Vector2>();
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 cur = pts[i];
            if (i > 0 && pts[i - 1].y < wy && cur.y >= wy && res.Count == 0)
            {
                float t = (wy - pts[i - 1].y) / Mathf.Max(1e-4f, cur.y - pts[i - 1].y);
                res.Add(Vector2.Lerp(pts[i - 1], cur, t));
            }
            if (cur.y >= wy && cur.y <= springY + 0.001f) res.Add(cur);
        }
        if (res.Count < 2) return;
        if (res[res.Count - 1].y < springY - 0.01f) res.Add(new Vector2(zIn, springY));

        float x0 = X[0];
        c.headFace = res;
        c.headSpringX = x0;
        c.headSpringY = springY;

        // тёмная расщелина над точкой выхода воды
        float fw = c.s.headFallWidth;
        var mb = MB("HeadCleft");
        AddBox(mb, new Vector3(x0, springY + 0.55f, zIn + 0.2f), new Vector3(fw * 1.35f, 1.2f, 0.55f), null);
        // каменные косяки по бокам расщелины
        AddBox(MB("HeadCrag"), new Vector3(x0 - fw * 0.95f, springY + 0.7f, zIn - 0.1f), new Vector3(0.7f, 1.8f, 0.9f), colMb);
        AddBox(MB("HeadCrag"), new Vector3(x0 + fw * 0.95f, springY + 0.7f, zIn - 0.1f), new Vector3(0.7f, 1.8f, 0.9f), colMb);
    }

    static readonly int[][] BoxF =
    {
        new[] { 0, 2, 6, 4 }, new[] { 1, 3, 7, 5 }, new[] { 0, 1, 5, 4 },
        new[] { 2, 3, 7, 6 }, new[] { 0, 1, 3, 2 }, new[] { 4, 5, 7, 6 }
    };

    void AddBox(LocMB mb, Vector3 center, Vector3 size, LocMB alsoCol)
    {
        var v = new Vector3[8];
        for (int i = 0; i < 8; i++)
            v[i] = center + new Vector3(((i & 1) == 0 ? -0.5f : 0.5f) * size.x, ((i & 2) == 0 ? -0.5f : 0.5f) * size.y, ((i & 4) == 0 ? -0.5f : 0.5f) * size.z);
        foreach (var f in BoxF)
        {
            Vector3 mid = (v[f[0]] + v[f[1]] + v[f[2]] + v[f[3]]) * 0.25f;
            Vector3 o = mid - center;
            mb.Tri(v[f[0]], v[f[1]], v[f[2]], o);
            mb.Tri(v[f[0]], v[f[2]], v[f[3]], o);
            if (alsoCol != null) { alsoCol.Tri(v[f[0]], v[f[1]], v[f[2]], o); alsoCol.Tri(v[f[0]], v[f[2]], v[f[3]], o); }
        }
    }

    // ------------------------------------------------------------------ шпили и контрфорсы
    // граненый шпиль: основание в земле, сужение, острая вершина, наклон
    void AddCrag(string key, Vector3 baseP, float r, float h, Vector2 lean, int sides)
    {
        float[] ys = { -0.9f, h * 0.42f, h * 0.80f };
        float[] rs = { r, r * 0.72f, r * 0.38f };
        float[] fr = { 0f, 0.42f, 0.8f };
        var ring = new Vector3[3, sides];
        float rot = Rf(0f, 6.28f);
        for (int k = 0; k < 3; k++)
            for (int i = 0; i < sides; i++)
            {
                float a = rot + (i + (float)rng.NextDouble() * 0.5f - 0.25f) / sides * Mathf.PI * 2f;
                float rad = rs[k] * Rf(0.8f, 1.2f);
                float lf = fr[k];
                ring[k, i] = baseP + new Vector3(Mathf.Cos(a) * rad + lean.x * h * lf, ys[k] + (k > 0 ? Rf(-0.2f, 0.2f) : 0f), Mathf.Sin(a) * rad + lean.y * h * lf);
            }
        Vector3 apex = baseP + new Vector3(lean.x * h, h, lean.y * h);
        Vector3 ctr = baseP + new Vector3(lean.x * h * 0.3f, h * 0.35f, lean.y * h * 0.3f);
        for (int k = 0; k < 2; k++)
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                Vector3 a = ring[k, i], b = ring[k, j], c2 = ring[k + 1, j], d = ring[k + 1, i];
                Vector3 o = (a + b + c2 + d) * 0.25f - ctr; o.y = 0f;
                Tri(key, a, b, c2, o);
                Tri(key, a, c2, d, o);
            }
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            Vector3 mid = (ring[2, i] + ring[2, j] + apex) / 3f;
            Tri(key, ring[2, i], ring[2, j], apex, mid - ctr);
        }
    }

    void BuildCrags()
    {
        var s = c.s;
        // шпили на гребне; самый высокий - в середине
        int peaks = Mathf.Clamp(n + 2, 6, 12);
        int tall = rng.Next(Mathf.Max(1, peaks / 3), Mathf.Max(2, peaks * 2 / 3));
        for (int i = 0; i < peaks; i++)
        {
            int ai = Mathf.Clamp(Mathf.RoundToInt((i + 0.5f) / peaks * n), 0, n);
            float wc = Mathf.Max(0.9f, W[ai] - (W[ai] - 1.6f) * 0.95f);
            float x = X[ai] + Rf(-0.6f, 0.6f) * wc;
            float z = Z[ai] + Rf(-0.5f, 0.5f);
            float y = crest[ai] + 0.5f;
            bool big = i == tall;
            float h = big ? Rf(5.5f, 7.0f) : Rf(2.2f, 4.8f);
            float r = big ? Rf(1.5f, 2.0f) : Rf(0.8f, 1.5f);
            AddCrag("HeadCrag", new Vector3(x, y, z), r, h, new Vector2(Rf(-0.1f, 0.1f), Rf(-0.1f, 0.1f)), rng.Next(5, 8));
        }

        // контрфорсы по бокам: наклонены к массиву, растут из земли
        int per = Mathf.Clamp(n / 2 + 1, 3, 6);
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < per; i++)
            {
                int ai = Mathf.Clamp(Mathf.RoundToInt((i + Rf(0.1f, 0.9f)) / per * n), 0, n);
                float off = W[ai] + Rf(0.1f, 0.9f);
                float x = X[ai] + side * off;
                float z = Z[ai] + Rf(-0.6f, 0.6f);
                float gy = c.heightU(x, z);
                if (gy < wy + 0.3f) continue;                       // не в воду и не в ущелье
                float h = Rf(5.5f, 10.5f) * Mathf.Lerp(1f, 0.75f, i / (float)Mathf.Max(1, per - 1));
                float r = Rf(1.4f, 2.4f);
                AddCrag(((i + side) & 1) == 0 ? "HeadCrag" : "HeadRock_0b", new Vector3(x, gy, z), r, h, new Vector2(-side * Rf(0.12f, 0.28f), Rf(-0.05f, 0.05f)), rng.Next(5, 8));
            }
        // скальный «зуб» на носу у воды, рядом с ручьём
        for (int side = -1; side <= 1; side += 2)
        {
            float x = X[0] + side * Rf(W[0] * 0.55f, W[0] * 0.85f);
            float z = Z[0] - frontD0 * Rf(0.55f, 0.8f);
            float gy = Mathf.Max(wy - 0.4f, c.heightU(x, z));
            AddCrag("HeadCrag", new Vector3(x, gy, z), Rf(0.9f, 1.4f), Rf(3.0f, 5.0f), new Vector2(-side * 0.1f, 0.12f), 6);
        }
    }

    // ------------------------------------------------------------------ валуны у подножия
    void BuildBoulders()
    {
        var bt = new LocBatcher(24f);
        var rock = new Mesh[3];
        for (int i = 0; i < rock.Length; i++) rock[i] = LocKit.Ico("HeadBoulder_" + i, 31 + i, LocKit.RockDeform);
        var mm = new[] { mats["HeadRock_0b"], mats["HeadRock_1a"], mats["HeadRock_2b"], mats["HeadCrag"] };
        Material moss = mats["HeadCap"];

        int count = Mathf.Clamp(n * 4, 12, 40);
        for (int k = 0, placed = 0; k < count * 6 && placed < count; k++)
        {
            int ai = rng.Next(0, n + 1);
            int side = rng.Next(2) == 0 ? -1 : 1;
            float d = W[ai] + Rf(0.8f, 3.6f);
            float x = X[ai] + side * d;
            float z = Z[ai] + Rf(-1.2f, 1.2f);
            if (k % 7 == 0) { x = X[0] + Rf(-1f, 1f) * W[0] * 0.8f; z = Z[0] - frontD0 - Rf(0.2f, 1.8f); }   // у воды перед носом
            float gy = c.heightU(x, z);
            if (gy < wy - 0.6f) continue;
            float size = Rf(1.3f, 3.2f);
            Vector3 sc = new Vector3(size * Rf(1.1f, 1.5f), size * Rf(0.7f, 1.0f), size * Rf(1.0f, 1.3f));
            int v = rng.Next(rock.Length);
            Vector3 pos = new Vector3(x, Mathf.Max(gy, wy - 0.3f) + sc.y * 0.25f, z);
            bt.Add(rock[v], mm[rng.Next(mm.Length)], pos, Quaternion.Euler(Rf(-8f, 8f), Rf(0f, 360f), Rf(-8f, 8f)), sc);
            if (size > 2f && rng.NextDouble() < 0.6)
                bt.Add(rock[v], moss, pos + Vector3.up * sc.y * 0.22f, Quaternion.Euler(0f, Rf(0f, 360f), 0f), new Vector3(sc.x * 0.66f, sc.y * 0.28f, sc.z * 0.66f));
            placed++;
        }
        var holder = new GameObject("Boulders").transform;
        holder.SetParent(node, false);
        bt.Flush(holder, "HeadBoulder", true);
    }

    // ------------------------------------------------------------------ коллизия
    void BuildColliders()
    {
        var cg = new GameObject("Colliders");
        cg.transform.SetParent(node, false);

        // 1. вся оболочка (шпили и контрфорсы тоже в ней)
        var mesh = colMb.ToMesh("HeadMountain_Col");
        mesh = LocKit.SaveAsset(mesh, LocKit.Batches + "/HeadMountain_Col.asset");
        var mc = new GameObject("ShellCollider");
        mc.transform.SetParent(cg.transform, false);
        mc.AddComponent<MeshCollider>().sharedMesh = mesh;

        // 2. невидимые боксы внутри массива: непроходимо, даже если в меше окажется дыра или игрок прыгнет.
        //    Лежат строго внутри контура, а по высоте уходят выше гребня.
        float wIn = float.MaxValue;
        for (int i = 0; i <= n; i++) wIn = Mathf.Min(wIn, W[i]);
        int step = Mathf.Max(1, n / 4);
        for (int i = 0; i < n; i += step)
        {
            int j = Mathf.Min(n, i + step);
            float cx = (X[i] + X[j]) * 0.5f, cz = (Z[i] + Z[j]) * 0.5f;
            float top = Mathf.Max(crest[i], crest[j]) + 10f;
            float w = Mathf.Max(1.2f, wIn * 0.8f);
            var g = new GameObject("Guard");
            g.transform.SetParent(cg.transform, false);
            g.transform.localPosition = new Vector3(cx, (y0 + top) * 0.5f, cz);
            float ang = Mathf.Atan2(X[j] - X[i], Z[j] - Z[i]) * Mathf.Rad2Deg;
            g.transform.localRotation = Quaternion.Euler(0f, ang, 0f);
            float len = Mathf.Sqrt((X[j] - X[i]) * (X[j] - X[i]) + (Z[j] - Z[i]) * (Z[j] - Z[i])) + 1.4f;
            g.AddComponent<BoxCollider>().size = new Vector3(w * 2f, top - y0, len);
        }
        // северный торец и нос перекрыты теми же боксами + оболочкой
    }

    // ------------------------------------------------------------------ вывод мешей
    void Flush()
    {
        var holder = new GameObject("Rock").transform;
        holder.SetParent(node, false);
        foreach (var kv in mbs)
        {
            if (kv.Value.v.Count == 0) continue;
            Material m;
            if (!mats.TryGetValue(kv.Key, out m)) m = mats["HeadCrag"];
            string nm = "HeadMountain_" + kv.Key;
            var mesh = kv.Value.ToMesh(nm);
            mesh = LocKit.SaveAsset(mesh, LocKit.Batches + "/" + nm + ".asset");
            var go = new GameObject(nm);
            go.transform.SetParent(holder, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, UnityEditor.StaticEditorFlags.OccludeeStatic);
        }
        Debug.Log("[LocationHeadMountain] Гора у истока: " + mbs.Count + " мешей, пластов " + c.s.headStrata + ", гребень до " + crest[0].ToString("0.0") + ".." + c.s.headHeight.ToString("0.0") + " u, шпили выше.");
    }
}
#endif
