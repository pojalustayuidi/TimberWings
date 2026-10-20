#if UNITY_EDITOR
// =====================================================================================
//  Assets/_Project/Editor/Location/Modules/LocationSpring.cs
//  LocationSpring - исток реки: скала-каскад с гротом.
//
//  Что строится (всё в "u" внутри узла Location/Spring, localScale = worldScale):
//    * Каскад из K каменных ступеней (springSteps), от воды реки вверх к роднику (springHeight, метры над водой).
//      Каждая ступень это гранёная плита-"тело" со ровной верхней площадкой (по ней течёт вода), мшистой кромкой и
//      слоями породы. Ступени заходят друг под друга, поэтому щелей между ними нет.
//    * Грот: на самом верху две скальные "створки" и каменная перемычка над тёмным проёмом, из которого вытекает ручей.
//    * Ручей: по площадкам вода течёт мелким потоком, на уступах падает лентой, в конце каждого падения пена.
//      Лента начинается на кромке уступа (шейдер Project/Waterfall рисует пену в начале ленты).
//    * Каменные "берега" по бокам ступеней, колонны у уступов, шпили на вершине, валуны у подножия, трава и цветы.
//    * Один MeshCollider по всей скале (как у LocationHeadMountain).
//  Запускается ДО реки (LocationModules). Если построился, ставит ctx.springBuilt = true,
//  и LocationRiver пропускает старые "дугу валунов" и тонкую ленту водопадика.
//  Гору LocationHeadMountain при включённом истоке-каскаде не строим (они мешают друг другу).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

public sealed class LocationSpring
{
    readonly LocationCtx c;
    readonly System.Random rng;
    Transform node;

    readonly Dictionary<string, LocMB> mbs = new Dictionary<string, LocMB>();
    readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    LocMB colMb;

    float wy, zf, streamW, aEnd;
    int K;
    float[] yTop, aFront, len, bodyHalf;
    Mesh mRing;
    Material ringMat;

    LocationSpring(LocationCtx ctx)
    {
        c = ctx;
        rng = new System.Random(ctx.seed * 31 + 7);
    }

    public static void Build(LocationCtx ctx) { new LocationSpring(ctx).Run(); }

    // ------------------------------------------------------------------ утилиты
    float Rf(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    Color E(int i) { return c.s.envColors[i]; }
    static float PowS(float v, float p) { return Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), p); }

    LocMB MB(string key)
    {
        LocMB m;
        if (!mbs.TryGetValue(key, out m)) { m = new LocMB(); mbs[key] = m; }
        return m;
    }

    // грань видна и участвует в коллизии
    void Tri(string key, Vector3 a, Vector3 b, Vector3 c2, Vector3 outward)
    {
        MB(key).Tri(a, b, c2, outward);
        colMb.Tri(a, b, c2, outward);
    }

    // грань только для вида (трава, цветы)
    void TriV(string key, Vector3 a, Vector3 b, Vector3 c2, Vector3 outward)
    {
        MB(key).Tri(a, b, c2, outward);
    }

    // ------------------------------------------------------------------ ось каскада
    // a = метры на север (вдоль русла) от переднего края каскада. Ось повторяет изгиб реки.
    Vector2 AxisXZ(float a) { float z = zf + a; return new Vector2(c.riverX(z), z); }

    Vector2 AxisDir(float a)
    {
        float z = zf + a;
        Vector2 d = new Vector2(c.riverX(z + 0.5f) - c.riverX(z - 0.5f), 1f);
        return d.normalized;
    }

    Vector2 AxisAcross(float a) { Vector2 d = AxisDir(a); return new Vector2(d.y, -d.x); }
    float YawAt(float a) { Vector2 d = AxisDir(a); return Mathf.Atan2(d.x, d.y); }

    Vector2 OnAxis(float a, float across)
    {
        Vector2 p = AxisXZ(a), n = AxisAcross(a);
        return p + n * across;
    }

    // ------------------------------------------------------------------ запуск
    void Run()
    {
        var s = c.s;
        if (!s.riverFullLength || !s.riverGorge || !s.riverHead) return;

        wy = c.waterLevel;
        colMb = new LocMB();

        // где кончается вода: там встаёт передний край каскада
        float srcZ = c.riverSrcZ;
        float sw = srcZ - 6f;
        while (sw < srcZ + 8f && c.heightU(c.riverX(sw + 0.25f), sw + 0.25f) < wy - 0.05f) sw += 0.25f;
        zf = sw + 0.3f;

        K = Mathf.Clamp(s.springSteps, 2, 5);
        float Hs = Mathf.Clamp(s.springHeight, 3f, 12f);
        streamW = Mathf.Max(1.6f, s.headFallWidth * 1.5f);

        // длины площадок; если до края острова не хватает места, сжимаем
        len = new float[K];
        float total = 0f;
        for (int k = 0; k < K; k++)
        {
            len[k] = Mathf.Lerp(3.4f, 2.6f, K > 1 ? k / (float)(K - 1) : 0f) * Rf(0.94f, 1.08f);
            total += len[k];
        }
        float need = total + 3.4f;
        float avail = c.half - 1.0f - zf;
        if (avail < need)
        {
            float k2 = Mathf.Max(0.62f, avail / need);
            for (int k = 0; k < K; k++) len[k] *= k2;
        }

        // высоты ступеней
        yTop = new float[K]; aFront = new float[K]; bodyHalf = new float[K];
        float[] rise = new float[K];
        float rs = 0f;
        for (int k = 0; k < K; k++) { rise[k] = Rf(0.85f, 1.2f) * (k == 0 ? 1.15f : 1f); rs += rise[k]; }
        float y = wy, a = 0f;
        for (int k = 0; k < K; k++)
        {
            y += rise[k] / rs * Hs;
            yTop[k] = y;
            aFront[k] = a;
            bodyHalf[k] = (streamW * 0.5f + 1.7f) * Mathf.Lerp(1f, 0.88f, K > 1 ? k / (float)(K - 1) : 0f);
            a += len[k];
        }
        aEnd = a;

        MakeMaterials();

        var go = new GameObject("Spring");
        go.transform.SetParent(c.root, false);
        go.transform.localScale = Vector3.one * c.ws;
        node = go.transform;

        BuildTerraces();
        BuildBanks();
        BuildGrotto();
        BuildFoot();
        BuildFlora();
        BuildWater();
        BuildCollider();
        Flush();

        c.springBuilt = true;
        c.springTopY = yTop[K - 1];
        Debug.Log("[LocationSpring] Исток: " + K + " ступеней, высота " + Hs.ToString("0.0") + " u над водой, длина " + aEnd.ToString("0.0") + " u.");
    }

    void MakeMaterials()
    {
        int[] pal = { 16, 1, 0, 2, 22 };
        int shift = rng.Next(pal.Length);
        for (int k = 0; k < 4; k++)
        {
            Color bc = E(pal[(k + shift) % pal.Length]);
            float sh = Mathf.Lerp(0.92f, 1.08f, k / 3f);
            mats["SpringRock" + k] = LocKit.Mat("SpringRock" + k, new Color(bc.r * sh, bc.g * sh, bc.b * sh, 1f), 0.05f);
        }
        mats["SpringCap"] = LocKit.Mat("SpringCap", Color.Lerp(E(17), new Color(0.42f, 0.72f, 0.28f), 0.35f), 0.05f);
        mats["SpringCleft"] = LocKit.Mat("SpringCleft", new Color(0.05f, 0.06f, 0.07f), 0.02f);
        mats["SpringGrassA"] = LocKit.Mat("SpringGrassA", new Color(0.36f, 0.68f, 0.24f), 0.1f);
        mats["SpringGrassB"] = LocKit.Mat("SpringGrassB", new Color(0.50f, 0.78f, 0.30f), 0.1f);
        mats["SpringFlowerA"] = LocKit.Mat("SpringFlowerA", new Color(1f, 0.62f, 0.78f), 0.2f);
        mats["SpringFlowerB"] = LocKit.Mat("SpringFlowerB", new Color(1f, 0.86f, 0.30f), 0.2f);
        mats["SpringFlowerC"] = LocKit.Mat("SpringFlowerC", new Color(0.98f, 0.97f, 0.92f), 0.2f);
    }

    string RockKey(int i) { return "SpringRock" + (((i % 4) + 4) % 4); }

    // ------------------------------------------------------------------ гранёная глыба
    // 4 кольца: низ, середина, плечо, кромка. Бока - порода, плечо (если mossBand) и верх - мох.
    // sq < 1 даёт "квадратный" контур (площадки ступеней), fixedRot держит вершины по осям.
    void Rock(string side, Vector2 ctr, float yb, float yt, float hx, float hz, float yaw, int n, float jag,
              float sq, bool fixedRot, Vector2 tilt, float bump, bool mossBand)
    {
        if (yt - yb < 0.05f) return;
        float cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw);
        float[] ry = { yb, Mathf.Lerp(yb, yt, 0.50f), Mathf.Lerp(yb, yt, 0.84f), yt };
        float[] rr = { 1.04f, 1.0f, 0.97f, 0.92f };
        var jit = new float[n];
        var ja = new float[n];
        for (int i = 0; i < n; i++) { jit[i] = Rf(1f - jag, 1f + jag); ja[i] = Rf(-0.2f, 0.2f); }
        float rot = fixedRot ? 0f : Rf(0f, 6.283f);
        bool tall = (yt - yb) > 0.8f;

        var ring = new Vector3[4, n];
        for (int k = 0; k < 4; k++)
            for (int i = 0; i < n; i++)
            {
                float ang = rot + (i + ja[i]) / n * Mathf.PI * 2f;
                float rad = rr[k] * jit[i];
                float lx = PowS(Mathf.Cos(ang), sq) * hx * rad;
                float lz = PowS(Mathf.Sin(ang), sq) * hz * rad;
                float wx = lx * cy + lz * sy;
                float wz = -lx * sy + lz * cy;
                float yy = ry[k];
                if (tall && (k == 1 || k == 2)) yy += Rf(-0.10f, 0.10f);
                if (k >= 2) yy += tilt.x * lx + tilt.y * lz;
                ring[k, i] = new Vector3(ctr.x + wx, yy, ctr.y + wz);
            }

        for (int k = 0; k < 3; k++)
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 p0 = ring[k, i], p1 = ring[k, j], p2 = ring[k + 1, j], p3 = ring[k + 1, i];
                Vector3 mid = (p0 + p1 + p2 + p3) * 0.25f;
                Vector3 o = new Vector3(mid.x - ctr.x, 0f, mid.z - ctr.y);
                if (o.sqrMagnitude < 1e-6f) o = Vector3.forward;
                string key = (k == 2 && mossBand) ? "SpringCap" : side;
                Tri(key, p0, p1, p2, o);
                Tri(key, p0, p2, p3, o);
            }

        Vector3 topC = new Vector3(ctr.x, yt + bump, ctr.y);
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Tri("SpringCap", ring[3, i], ring[3, j], topC, Vector3.up);
        }
    }

    // граненый шпиль: основание в земле, сужение, острая вершина, наклон
    void Crag(string key, Vector3 baseP, float r, float h, Vector2 lean, int sides)
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
                Vector3 p0 = ring[k, i], p1 = ring[k, j], p2 = ring[k + 1, j], p3 = ring[k + 1, i];
                Vector3 o = (p0 + p1 + p2 + p3) * 0.25f - ctr; o.y = 0f;
                Tri(key, p0, p1, p2, o);
                Tri(key, p0, p2, p3, o);
            }
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            Vector3 mid = (ring[2, i] + ring[2, j] + apex) / 3f;
            Tri(key, ring[2, i], ring[2, j], apex, mid - ctr);
        }
    }

    static readonly int[][] BoxF =
    {
        new[] { 0, 2, 6, 4 }, new[] { 1, 3, 7, 5 }, new[] { 0, 1, 5, 4 },
        new[] { 2, 3, 7, 6 }, new[] { 0, 1, 3, 2 }, new[] { 4, 5, 7, 6 }
    };

    void Box(string key, Vector3 center, Vector3 size, float yaw)
    {
        var v = new Vector3[8];
        float cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw);
        for (int i = 0; i < 8; i++)
        {
            float lx = ((i & 1) == 0 ? -0.5f : 0.5f) * size.x;
            float ly = ((i & 2) == 0 ? -0.5f : 0.5f) * size.y;
            float lz = ((i & 4) == 0 ? -0.5f : 0.5f) * size.z;
            v[i] = center + new Vector3(lx * cy + lz * sy, ly, -lx * sy + lz * cy);
        }
        foreach (var f in BoxF)
        {
            Vector3 mid = (v[f[0]] + v[f[1]] + v[f[2]] + v[f[3]]) * 0.25f;
            Vector3 o = mid - center;
            MB(key).Tri(v[f[0]], v[f[1]], v[f[2]], o);
            MB(key).Tri(v[f[0]], v[f[2]], v[f[3]], o);
        }
    }

    // ------------------------------------------------------------------ ступени
    void BuildTerraces()
    {
        for (int k = 0; k < K; k++)
        {
            float a0 = aFront[k];
            float a1 = aFront[k] + len[k] + 0.6f;               // заходит под верхнюю ступень
            float aMid = (a0 + a1) * 0.5f;
            // площадка ровная: bump = 0, наклона нет
            Rock(RockKey(k), AxisXZ(aMid), wy - 1.6f, yTop[k], bodyHalf[k], (a1 - a0) * 0.5f, YawAt(aMid),
                 8, 0.05f, 0.6f, true, Vector2.zero, 0f, true);
        }
    }

    // берега по бокам площадок и колонны у уступов
    void BuildBanks()
    {
        for (int k = 0; k < K; k++)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int j = 0; j < 2; j++)
                {
                    float a = aFront[k] + Rf(0.5f, Mathf.Max(0.8f, len[k] - 0.3f));
                    float hx = Rf(0.45f, 0.8f), hz = Rf(0.55f, 1.0f);
                    float bc = streamW * 0.5f + 0.12f + hx + Rf(0f, 0.5f);
                    bc = Mathf.Min(bc, bodyHalf[k] * 0.9f - hx * 0.6f);
                    float h = Rf(0.5f, 1.3f) * (j == 0 ? 1.3f : 1f);
                    Rock(RockKey(k + j + 1), OnAxis(a, side * bc), yTop[k] - 0.4f, yTop[k] + h, hx, hz,
                         YawAt(a) + Rf(-0.5f, 0.5f), 7, 0.15f, 1f, false,
                         new Vector2(Rf(-0.1f, 0.1f), Rf(-0.1f, 0.1f)), 0.05f, true);
                }
                // колонна у уступа: прижата к стене, сужает струю
                float colB = streamW * 0.55f + 0.95f;
                float yb = k == 0 ? wy - 1.0f : yTop[k - 1] - 0.2f;
                Rock(RockKey(k + 2), OnAxis(aFront[k] + 0.15f, side * colB), yb, yTop[k] + Rf(0.5f, 1.2f), 0.5f, 0.55f,
                     YawAt(aFront[k]) + Rf(-0.4f, 0.4f), 7, 0.12f, 1f, false,
                     new Vector2(Rf(-0.08f, 0.08f), Rf(-0.08f, 0.08f)), 0.04f, true);
            }
        }
    }

    // ------------------------------------------------------------------ грот на вершине
    void BuildGrotto()
    {
        int kt = K - 1;
        float yt = yTop[kt];
        float aw = aEnd + 0.9f;                                  // центр створок по оси
        float gap = streamW * 1.25f;                             // ширина проёма
        float wallH = Rf(3.2f, 3.9f);

        for (int side = -1; side <= 1; side += 2)
        {
            float hx = 1.7f;
            float bc = gap * 0.5f + hx * 0.85f;
            Rock(RockKey(K + 1), OnAxis(aw, side * bc), yt - 0.4f, yt + wallH * Rf(0.9f, 1.15f), hx, 1.6f, YawAt(aw),
                 8, 0.10f, 0.7f, false, new Vector2(Rf(-0.06f, 0.06f), Rf(-0.06f, 0.06f)), 0.15f, true);
            // боковой выступ-контрфорс пониже
            Rock(RockKey(K), OnAxis(aw + 0.2f, side * (bc + hx * 1.1f)), yt - 0.4f, yt + wallH * 0.55f, 1.2f, 1.3f,
                 YawAt(aw) + Rf(-0.4f, 0.4f), 7, 0.14f, 1f, false, new Vector2(-side * 0.12f, 0.05f), 0.1f, true);
        }

        // перемычка над проёмом
        Rock(RockKey(K + 2), OnAxis(aw - 0.1f, 0f), yt + 1.25f, yt + 2.25f, gap * 0.5f + 0.8f, 1.35f, YawAt(aw),
             8, 0.07f, 0.75f, true, new Vector2(0f, 0.04f), 0.08f, true);
        // над ней задняя масса, чтобы грот читался как вырез в скале
        Rock(RockKey(K + 3), OnAxis(aw + 0.9f, 0f), yt - 0.4f, yt + wallH + 0.5f, gap * 0.5f + 2.6f, 1.5f, YawAt(aw),
             8, 0.10f, 0.7f, false, Vector2.zero, 0.2f, true);

        // тёмная пещера: пол и задняя стенка видны сквозь проём
        Vector2 cv = OnAxis(aw + 0.7f, 0f);
        Box("SpringCleft", new Vector3(cv.x, yt + 0.8f, cv.y), new Vector3(gap + 0.6f, 1.9f, 0.9f), YawAt(aw));

        // шпили на вершине
        int peaks = 3 + rng.Next(2);
        for (int i = 0; i < peaks; i++)
        {
            float t = peaks > 1 ? i / (float)(peaks - 1) : 0.5f;
            float b = Mathf.Lerp(-1f, 1f, t) * (gap * 0.5f + 1.9f);
            Vector2 p = OnAxis(aw + Rf(0.2f, 1.2f), b);
            bool big = i == peaks / 2;
            float top = yt + wallH * 0.95f;
            Crag(RockKey(i + 1), new Vector3(p.x, top, p.y), big ? Rf(1.0f, 1.3f) : Rf(0.6f, 1.0f),
                 big ? Rf(2.6f, 3.6f) : Rf(1.4f, 2.6f), new Vector2(Rf(-0.1f, 0.1f), Rf(-0.1f, 0.1f)), rng.Next(5, 8));
        }
    }

    // ------------------------------------------------------------------ подножие: валуны на берегах и у первого уступа
    void BuildFoot()
    {
        float bh0 = bodyHalf[0];
        for (int i = 0; i < 7; i++)
        {
            int side = i % 2 == 0 ? -1 : 1;
            float a = Rf(-1.2f, 1.8f);
            float b = side * (bh0 + Rf(0.1f, 1.3f));
            Vector2 xz = OnAxis(a, b);
            float g = c.heightU(xz.x, xz.y);
            if (g < wy + 0.1f) continue;                          // не перегораживаем реку
            float sz = Rf(0.7f, 1.4f);
            Rock(RockKey(i), xz, g - 0.9f, g + sz * Rf(0.7f, 1.2f), sz, sz * Rf(0.8f, 1.2f), YawAt(a) + Rf(-0.8f, 0.8f),
                 7, 0.16f, 1f, false, new Vector2(Rf(-0.12f, 0.12f), Rf(-0.12f, 0.12f)), 0.05f, true);
        }
    }

    // ------------------------------------------------------------------ трава и цветы
    void Tuft(Vector3 p, float sc, string key)
    {
        int blades = 5 + rng.Next(3);
        for (int i = 0; i < blades; i++)
        {
            float ang = Rf(0f, 6.283f);
            float rad = Rf(0f, 0.12f) * sc;
            Vector3 b0 = p + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
            float h = Rf(0.35f, 0.8f) * sc;
            float la = Rf(0f, 6.283f);
            float lean = Rf(0.08f, 0.3f) * sc;
            Vector3 tip = b0 + new Vector3(Mathf.Cos(la) * lean, h, Mathf.Sin(la) * lean);
            Vector3 sd = new Vector3(-Mathf.Sin(la), 0f, Mathf.Cos(la)) * (0.045f * sc);
            Vector3 p0 = b0 + sd, p1 = b0 - sd;
            Vector3 o = new Vector3(Mathf.Cos(la), 0.3f, Mathf.Sin(la));
            TriV(key, p0, p1, tip, o);
            TriV(key, p1, p0, tip, -o);
        }
    }

    void Flower(Vector3 p, float sc, string key)
    {
        float h = Rf(0.25f, 0.5f) * sc;
        Vector3 top = p + new Vector3(0f, h, 0f);
        Vector3 apex = top + new Vector3(0f, 0.07f * sc, 0f);
        const int n = 5;
        for (int i = 0; i < n; i++)
        {
            float a0 = i / (float)n * Mathf.PI * 2f, a1 = (i + 1) / (float)n * Mathf.PI * 2f;
            Vector3 q0 = top + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * (0.08f * sc);
            Vector3 q1 = top + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * (0.08f * sc);
            TriV(key, q0, q1, apex, (q0 + q1) * 0.5f - top + Vector3.up * 0.3f);
        }
    }

    void BuildFlora()
    {
        string[] gk = { "SpringGrassA", "SpringGrassB" };
        string[] fk = { "SpringFlowerA", "SpringFlowerB", "SpringFlowerC" };
        for (int k = 0; k < K; k++)
            for (int side = -1; side <= 1; side += 2)
            {
                int cnt = 3 + rng.Next(3);
                for (int i = 0; i < cnt; i++)
                {
                    float a = aFront[k] + Rf(0.3f, len[k]);
                    float b = side * Rf(streamW * 0.5f + 0.4f, bodyHalf[k] * 0.85f);
                    Vector2 xz = OnAxis(a, b);
                    Vector3 p = new Vector3(xz.x, yTop[k] + 0.02f, xz.y);
                    Tuft(p, Rf(0.8f, 1.5f), gk[rng.Next(2)]);
                    if (rng.NextDouble() < 0.35) Flower(p + new Vector3(Rf(-0.15f, 0.15f), 0f, Rf(-0.15f, 0.15f)), Rf(0.9f, 1.3f), fk[rng.Next(3)]);
                }
            }
        // у подножия на земле
        for (int i = 0; i < 14; i++)
        {
            int side = rng.Next(2) == 0 ? -1 : 1;
            Vector2 xz = OnAxis(Rf(-1.5f, 2.5f), side * (bodyHalf[0] + Rf(0.3f, 2.2f)));
            float g = c.heightU(xz.x, xz.y);
            if (g < wy + 0.3f) continue;
            Tuft(new Vector3(xz.x, g, xz.y), Rf(0.9f, 1.7f), gk[rng.Next(2)]);
            if (rng.NextDouble() < 0.4) Flower(new Vector3(xz.x + 0.1f, g, xz.y), 1.1f, fk[rng.Next(3)]);
        }
    }

    // ------------------------------------------------------------------ вода
    Material MakeWaterMat()
    {
        Shader sh = Shader.Find("Project/Waterfall");
        if (sh != null)
        {
            var m = new Material(sh) { name = "LocSpringCascade" };
            m.SetColor("_Deep", new Color(0.26f, 0.60f, 0.86f, 1f));
            m.SetColor("_Light", new Color(0.86f, 0.96f, 1f, 1f));
            m.SetColor("_Foam", Color.white);
            m.SetFloat("_Speed", 2.4f);
            m.SetFloat("_Streaks", Mathf.Clamp(streamW * 3f, 6f, 12f));
            m.SetFloat("_FoamLen", 0.9f);
            return LocKit.SaveAsset(m, LocKit.MatDir + "/LocSpringCascade.mat");
        }
        Debug.LogWarning("[LocationSpring] Не найден шейдер Project/Waterfall (Scripts/Location/Waterfall.shader): ручей нарисован запасным материалом.");
        return LocKit.MatTransparent("LocSpringCascadeFallback", c.s.waterColor, 0.85f);
    }

    // ctrl: x = a (вдоль оси), y = высота, z = ширина ленты
    static List<Vector3> Dense(List<Vector3> ctrl, float step)
    {
        var res = new List<Vector3>();
        for (int i = 0; i < ctrl.Count - 1; i++)
        {
            Vector3 p = ctrl[i], q = ctrl[i + 1];
            float d = Mathf.Sqrt((q.x - p.x) * (q.x - p.x) + (q.y - p.y) * (q.y - p.y));
            int n = Mathf.Max(1, Mathf.CeilToInt(d / step));
            for (int k = 0; k < n; k++) res.Add(Vector3.Lerp(p, q, k / (float)n));
        }
        res.Add(ctrl[ctrl.Count - 1]);
        return res;
    }

    void WaterRibbon(string name, List<Vector3> ctrl, Material mat, float fadeEnd)
    {
        var d = Dense(ctrl, 0.3f);
        int m = d.Count;
        if (m < 2) return;
        var verts = new Vector3[m * 2];
        var uvs = new Vector2[m * 2];
        var cols = new Color[m * 2];
        var nor = new Vector3[m * 2];
        float acc = 0f;
        Vector3 prev = Vector3.zero;
        for (int i = 0; i < m; i++)
        {
            Vector3 q = d[i];
            Vector2 xz = AxisXZ(q.x), nn = AxisAcross(q.x);
            Vector3 pos = new Vector3(xz.x, q.y, xz.y);
            if (i > 0) acc += Vector3.Distance(pos, prev) * c.ws;
            prev = pos;
            Vector3 sd = new Vector3(nn.x, 0f, nn.y) * (q.z * 0.5f);
            verts[i * 2] = pos - sd;
            verts[i * 2 + 1] = pos + sd;
            uvs[i * 2] = new Vector2(0f, acc);
            uvs[i * 2 + 1] = new Vector2(1f, acc);
            float al = 1f;
            if (fadeEnd > 0f) al = 1f - fadeEnd * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, i / (float)(m - 1)));
            cols[i * 2] = cols[i * 2 + 1] = new Color(1f, 1f, 1f, al);
            nor[i * 2] = nor[i * 2 + 1] = Vector3.up;
        }
        var tris = new List<int>();
        for (int i = 0; i < m - 1; i++)
        {
            int a = i * 2, b = a + 1, c2 = a + 2, e = a + 3;
            tris.Add(a); tris.Add(c2); tris.Add(b);
            tris.Add(b); tris.Add(c2); tris.Add(e);
        }
        var mesh = new Mesh { name = "Spring_" + name };
        mesh.vertices = verts;
        mesh.SetUVs(0, new List<Vector2>(uvs));
        mesh.colors = cols;
        mesh.normals = nor;
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mesh = LocKit.SaveAsset(mesh, LocKit.Batches + "/Spring_" + name + ".asset");

        var go = new GameObject(name);
        go.transform.SetParent(node, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    void AddFoam(float a, float y, float size)
    {
        if (mRing == null || ringMat == null) return;
        Vector2 xz = AxisXZ(a);
        var foam = new GameObject("SpringFoam");
        foam.transform.SetParent(node, false);
        foam.transform.localPosition = new Vector3(xz.x, y, xz.y);
        foam.transform.localScale = new Vector3(size, 1f, size);
        foam.AddComponent<MeshFilter>().sharedMesh = mRing;
        var fr = foam.AddComponent<MeshRenderer>();
        fr.sharedMaterial = ringMat;
        fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fr.receiveShadows = false;
    }

    void BuildWater()
    {
        var mat = MakeWaterMat();
        float sw = streamW;

        // пена: тот же шейдер колец, что у реки (создаётся, если ещё нет)
        Shader rs = LocKit.EnsureShader(LocationRiver.RingShaderPath, LocationRiver.RingShaderSource);
        if (rs != null && rs.isSupported && !UnityEditor.ShaderUtil.ShaderHasError(rs))
        {
            mRing = LocKit.Ring();
            var m = new Material(rs) { name = "LocSpringFoamRing" };
            m.SetColor("_Color", Color.white);
            m.renderQueue = 3001;
            ringMat = LocKit.SaveAsset(m, LocKit.MatDir + "/LocSpringFoamRing.mat");
        }

        float yt = yTop[K - 1] + 0.10f;
        float lipTop = aFront[K - 1] + 0.12f;

        // верхний участок: из грота по площадке до первого уступа
        var top = new List<Vector3>();
        top.Add(new Vector3(aEnd + 1.1f, yt, sw * 0.8f));
        top.Add(new Vector3(aEnd + 0.2f, yt, sw * 1.0f));
        top.Add(new Vector3(lipTop + 1.0f, yt, sw * 1.0f));
        top.Add(new Vector3(lipTop, yTop[K - 1] + 0.08f, sw * 0.86f));
        WaterRibbon("CascadeTop", top, mat, 0f);

        for (int k = K - 1; k >= 0; k--)
        {
            float lip = aFront[k] + 0.12f;
            float af = aFront[k] - 0.28f;                          // вода идёт чуть впереди каменной грани
            float yH = yTop[k], yL = k > 0 ? yTop[k - 1] : wy;

            var r = new List<Vector3>();
            r.Add(new Vector3(lip, yH + 0.08f, sw * 0.86f));       // начало ленты: шейдер даёт здесь пену
            r.Add(new Vector3(af + 0.12f, yH - 0.02f, sw * 0.84f));
            r.Add(new Vector3(af - 0.10f, Mathf.Lerp(yH, yL, 0.35f), sw * 0.92f));
            r.Add(new Vector3(af - 0.22f, Mathf.Lerp(yH, yL, 0.70f), sw * 1.0f));
            if (k > 0)
            {
                r.Add(new Vector3(af - 0.30f, yL + 0.20f, sw * 1.1f));
                r.Add(new Vector3(af - 0.55f, yL + 0.10f, sw * 1.12f));
                float lipLow = aFront[k - 1] + 0.12f;
                r.Add(new Vector3(lipLow + 1.2f, yL + 0.10f, sw * 1.0f));
                r.Add(new Vector3(lipLow + 0.1f, yL + 0.08f, sw * 0.88f));
                WaterRibbon("CascadeStep" + k, r, mat, 0f);
                AddFoam(af - 0.55f, yL + 0.12f, sw * 1.0f);
            }
            else
            {
                r.Add(new Vector3(af - 0.30f, wy + 0.25f, sw * 1.15f));
                r.Add(new Vector3(af - 0.70f, wy + 0.04f, sw * 1.4f));
                r.Add(new Vector3(af - 1.30f, wy + 0.02f, sw * 1.7f));
                WaterRibbon("CascadeStep0", r, mat, 0.9f);          // конец растворяется в воде реки
                AddFoam(af - 0.70f, wy + 0.04f, sw * 1.9f);
            }
        }
    }

    // ------------------------------------------------------------------ коллайдер и вывод
    void BuildCollider()
    {
        var cg = new GameObject("Colliders");
        cg.transform.SetParent(node, false);
        var mesh = colMb.ToMesh("Spring_Col");
        mesh = LocKit.SaveAsset(mesh, LocKit.Batches + "/Spring_Col.asset");
        var mc = new GameObject("ShellCollider");
        mc.transform.SetParent(cg.transform, false);
        mc.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    void Flush()
    {
        var holder = new GameObject("Rock").transform;
        holder.SetParent(node, false);
        foreach (var kv in mbs)
        {
            if (kv.Value.v.Count == 0) continue;
            Material m;
            if (!mats.TryGetValue(kv.Key, out m)) continue;
            string nm = "Spring_" + kv.Key;
            var mesh = kv.Value.ToMesh(nm);
            mesh = LocKit.SaveAsset(mesh, LocKit.Batches + "/" + nm + ".asset");
            var go = new GameObject(nm);
            go.transform.SetParent(holder, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, UnityEditor.StaticEditorFlags.OccludeeStatic);
        }
    }
}
#endif
