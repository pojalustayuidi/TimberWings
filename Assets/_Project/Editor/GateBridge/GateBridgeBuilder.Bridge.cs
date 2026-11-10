#if UNITY_EDITOR
// Assets/_Project/Editor/GateBridge/GateBridgeBuilder.Bridge.cs (Editor)
// Геометрия моста: план пути, три вида, доски, опоры, тросы, подвесы, перила, граница у входа, коллайдеры ходьбы и невидимые заграждения.
// Ось s идёт от S2 (начало настила, 0,8 м внутри главного острова) к концу настила на дальнем острове (Lb).
//   sE = 0,8 край главного острова, sF = sE + bridgeLength край дальнего. Провал между ними = bridgeLength метров.
using System.Collections.Generic;
using UnityEngine;

sealed class GBPlan
{
    public Vector2 E, N, Tn, S2, Q2, C2;
    public Vector3 N3, T3;
    public float y0, yStart, yStubTop, yEnd, Lb, hw, sag, R, L, sE, sF;

    // центр настила: линия с провисом
    public Vector3 P(float s)
    {
        float t = s / Lb;
        Vector2 q = S2 + N * s;
        float y = Mathf.Lerp(yStart, yEnd, t) - sag * 4f * t * (1f - t);
        return new Vector3(q.x, y, q.y);
    }

    // путь для коллайдеров: настил плюс пологие въезды к земле с обоих берегов
    public Vector3 Pw(float s)
    {
        if (s >= 0f && s <= Lb) return P(s);
        Vector2 q = S2 + N * s;
        float y = s < 0f ? Mathf.Lerp(y0, yStart, Mathf.Clamp01((s + 0.7f) / 0.7f))
                         : Mathf.Lerp(yEnd, yStubTop, Mathf.Clamp01((s - Lb) / 0.9f));
        return new Vector3(q.x, y, q.y);
    }

    public void Frame(float s, out Vector3 f, out Vector3 r, out Vector3 u)
    {
        f = (P(s + 0.05f) - P(s - 0.05f)).normalized;
        r = Vector3.Cross(Vector3.up, f).normalized;
        u = Vector3.Cross(f, r).normalized;
    }

    // точка в системе настила: вдоль s, вбок x, вверх dy
    public Vector3 D(float s, float x, float dy)
    {
        Vector3 f, r, u;
        Frame(s, out f, out r, out u);
        return P(s) + r * x + u * dy;
    }
}

public static partial class GateBridgeBuilder
{
    static readonly Color WoodL = new Color(0.66f, 0.44f, 0.24f);
    static readonly Color WoodD = new Color(0.40f, 0.26f, 0.14f);
    static readonly Color WoodFresh = new Color(0.80f, 0.60f, 0.35f);
    static readonly Color RopeC = new Color(0.78f, 0.66f, 0.42f);
    static readonly Color PostC = new Color(0.34f, 0.22f, 0.12f);
    static readonly Color PostOld = new Color(0.25f, 0.18f, 0.12f);
    static readonly Color PostFresh = new Color(0.76f, 0.56f, 0.33f);
    static readonly Color PostFinal = new Color(0.52f, 0.35f, 0.19f);
    static readonly Color ClampC = new Color(0.16f, 0.12f, 0.09f);
    static readonly Color WarnC = new Color(0.72f, 0.30f, 0.22f);

    const float Pitch = 0.6f;       // шаг досок вдоль моста
    const float TowerH = 3.0f;      // высота опор над настилом
    const float TowerDx = 0.25f;    // опора стоит на столько снаружи от края настила
    const float CableMid = 1.15f;   // высота главного троса над настилом в середине пролёта

    // Участки настила: ближняя часть от главного берега и дальняя от дальнего.
    // Вид 2 после первого этапа: короткие обрубки у опор. После второго (deckStep2): доски растут к середине, посередине остаётся разрыв
    static void GapRange(GBPlan p, out float gapA, out float gapB)
    {
        float mid = (p.sE + p.sF) * 0.5f;
        float half = Mathf.Max(2.5f, p.L * 0.155f);   // при 25 м разрыв около 7,8 м
        gapA = mid - half; gapB = mid + half;
    }

    // ------------------------------------------------------------ доски
    static void Plank(GBMesh m, GBPlan p, float s, float tilt, float yaw, Color col)
    {
        Vector3 f, r, u;
        p.Frame(s, out f, out r, out u);
        Quaternion q = Quaternion.AngleAxis(tilt, f) * Quaternion.AngleAxis(yaw, u);
        f = q * f; r = q * r; u = q * u;
        m.Box(p.P(s) - u * 0.06f, r, u, f, p.hw * 0.97f, 0.06f, 0.26f, col);
    }

    // Доски на отрезке [s0, s1]. broken: часть пропущена, у краёв разрыва доски перекошены (вид 1).
    // brokenAtStart / brokenAtEnd: у какого конца отрезка разрыв (для перекоса)
    static void Planks(GBMesh m, GBPlan p, System.Random rnd, float s0, float s1, bool broken, bool brokenAtStart, bool brokenAtEnd, bool fresh)
    {
        int n = Mathf.Max(1, Mathf.RoundToInt((s1 - s0) / Pitch));
        float pitch = (s1 - s0) / n;
        for (int i = 0; i < n; i++)
        {
            float s = s0 + (i + 0.5f) * pitch;
            double skip = rnd.NextDouble(), tiltR = rnd.NextDouble(), yawR = rnd.NextDouble(), colR = rnd.NextDouble();   // всегда тянем: порядок не зависит от ветки
            float tilt = 0f;
            if (broken)
            {
                bool nearBreak = (brokenAtStart && i < 3) || (brokenAtEnd && i >= n - 3);
                bool edge = (brokenAtStart && i == 0) || (brokenAtEnd && i == n - 1);
                if (!edge && skip < 0.16) continue;
                if (nearBreak) tilt = (tiltR < 0.5 ? -1f : 1f) * (8f + (float)tiltR * 30f);
            }
            float yaw = ((float)yawR - 0.5f) * (broken ? 6f : 3.5f);
            Color col = fresh ? Color.Lerp(WoodFresh, WoodL, (float)colR * 0.5f) : Color.Lerp(WoodL, WoodD, (float)colR);
            col.a = 1f;
            Plank(m, p, s, tilt, yaw, col);
        }
    }

    // ------------------------------------------------------------ опоры
    // Опора: пара столбов по бокам и перекладина. old = старые, кривые, сломанные; иначе новые с перекладиной
    static void Tower(GBMesh m, GBPlan p, float s, bool old, Color col)
    {
        Vector3 f, r, u;
        p.Frame(s, out f, out r, out u);
        float dx = p.hw + TowerDx;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 b = p.P(s) + r * (side * dx);
            if (!old)
            {
                m.Box(b + u * ((TowerH - 0.5f) * 0.5f), r, u, f, 0.17f, (TowerH + 0.5f) * 0.5f, 0.17f, col);
                m.Box(b + u * (TowerH + 0.08f), r, u, f, 0.22f, 0.08f, 0.22f, BoxShade(col, 0.85f));   // навершие
                // раскос к перекладине: в сторону настила
                m.Beam(b + u * 1.2f, b - r * (side * 0.55f) + u * (TowerH - 0.1f), 0.06f, BoxShade(col, 0.9f));
            }
            else
            {
                // старый столб: ниже, наклонён (один вбок, другой вдоль моста), верх расщеплён
                float h = side < 0 ? 2.3f : 1.35f;
                float lean = side < 0 ? 9f : -14f;
                Quaternion q = side < 0 ? Quaternion.AngleAxis(lean, f) : Quaternion.AngleAxis(lean, r);
                Vector3 ur = q * u, rr = q * r, ff = q * f;
                m.Box(b + ur * ((h - 0.5f) * 0.5f), rr, ur, ff, 0.17f, (h + 0.5f) * 0.5f, 0.17f, col);
                Vector3 top = b + ur * (h - 0.5f);
                Quaternion q2 = Quaternion.AngleAxis(side * 28f, ff);
                m.Box(top + (q2 * ur) * 0.18f, q2 * rr, q2 * ur, ff, 0.07f, 0.22f, 0.12f, BoxShade(col, 0.8f));   // расщеплённая щепа
            }
        }
        if (!old)
        {
            // перекладина поверх: над проходом (камера смотрит сверху, перекладина тонкая)
            m.Box(p.P(s) + u * (TowerH - 0.1f), r, u, f, dx + 0.2f, 0.11f, 0.14f, BoxShade(col, 0.92f));
        }
    }

    static Color BoxShade(Color c, float k) { return GBMesh.Sh(c, k); }

    // ------------------------------------------------------------ тросы
    static float CableH(GBPlan p, float s, float sTA, float sTB)
    {
        float u = Mathf.Clamp01((s - sTA) / (sTB - sTA));
        float k = 2f * u - 1f;
        return CableMid + ((TowerH - 0.12f) - CableMid) * k * k;
    }

    // Главные тросы: от верха опоры до верха опоры, провисают к середине. taut = натянутые (без лишнего провиса)
    static void MainCables(GBMesh m, GBPlan p, float sTA, float sTB)
    {
        float dx = p.hw + TowerDx;
        int steps = Mathf.Max(6, Mathf.RoundToInt((sTB - sTA) / 1.0f));
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 prev = Vector3.zero;
            for (int k = 0; k <= steps; k++)
            {
                float s = Mathf.Lerp(sTA, sTB, k / (float)steps);
                Vector3 pos = p.D(s, side * dx, CableH(p, s, sTA, sTB));
                if (k > 0) m.Beam(prev, pos, 0.05f, RopeC);
                prev = pos;
            }
        }
    }

    // Нижние направляющие тросы под настилом: по ним кладут доски
    static void FloorRopes(GBMesh m, GBPlan p, float sTA, float sTB)
    {
        int steps = Mathf.Max(6, Mathf.RoundToInt((sTB - sTA) / 1.0f));
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 prev = Vector3.zero;
            for (int k = 0; k <= steps; k++)
            {
                float s = Mathf.Lerp(sTA, sTB, k / (float)steps);
                Vector3 pos = p.D(s, side * (p.hw - 0.25f), -0.30f);
                if (k > 0) m.Beam(prev, pos, 0.05f, RopeC);
                prev = pos;
            }
        }
    }

    // Подвесы: вертикальные верёвки от главного троса к краю настила, на концах крепления
    static List<float> SuspenderPositions(float sTA, float sTB)
    {
        var l = new List<float>();
        for (float s = sTA + 1.4f; s < sTB - 1.0f; s += 2.3f) l.Add(s);
        return l;
    }

    static void Suspenders(GBMesh m, GBPlan p, float sTA, float sTB, List<float> at, float onlyFrom, float onlyTo, float onlyFrom2, float onlyTo2)
    {
        float dx = p.hw + TowerDx;
        foreach (float s in at)
        {
            bool ok = (s >= onlyFrom && s <= onlyTo) || (s >= onlyFrom2 && s <= onlyTo2);
            if (!ok) continue;
            Vector3 f, r, u;
            p.Frame(s, out f, out r, out u);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 top = p.D(s, side * dx, CableH(p, s, sTA, sTB));
                Vector3 bot = p.D(s, side * (p.hw + 0.12f), -0.02f);
                m.Beam(top, bot, 0.028f, RopeC);
                m.Box(bot, r, u, f, 0.08f, 0.08f, 0.08f, ClampC);                  // крепление у настила
                m.Box(top - u * 0.05f, r, u, f, 0.07f, 0.07f, 0.07f, ClampC);      // хомут на тросе
            }
        }
    }

    // Перила: две верёвки (поручень и нижняя) и стойки у подвесов
    static void Rails(GBMesh m, GBPlan p, float sA, float sB, List<float> posts)
    {
        float[] levels = { 1.0f, 0.5f };
        float[] thick = { 0.05f, 0.04f };
        int steps = Mathf.Max(6, Mathf.RoundToInt((sB - sA) / 1.0f));
        for (int side = -1; side <= 1; side += 2)
        {
            for (int li = 0; li < levels.Length; li++)
            {
                Vector3 prev = Vector3.zero;
                for (int k = 0; k <= steps; k++)
                {
                    float s = Mathf.Lerp(sA, sB, k / (float)steps);
                    Vector3 pos = p.D(s, side * (p.hw - 0.06f), levels[li]);
                    if (k > 0) m.Beam(prev, pos, thick[li], RopeC);
                    prev = pos;
                }
            }
        }
        var all = new List<float>(posts);
        all.Add(sA); all.Add(sB);
        foreach (float s in all)
        {
            Vector3 f, r, u;
            p.Frame(s, out f, out r, out u);
            for (int side = -1; side <= 1; side += 2)
                m.Box(p.P(s) + r * (side * (p.hw - 0.06f)) + u * 0.55f, r, u, f, 0.06f, 0.58f, 0.06f, PostC);
        }
    }

    // ------------------------------------------------------------ вид 1: сломан
    static GBMesh BuildBroken(GBPlan p, System.Random rnd)
    {
        var m = new GBMesh();
        float stubA = p.sE + 2.6f;          // обрубок у главного берега: 2,6 м за краем
        float stubB = p.sF - 1.6f;          // обрубок у дальнего: 1,6 м над провалом
        Planks(m, p, rnd, 0.3f, stubA, true, false, true, false);
        Planks(m, p, rnd, stubB, p.Lb - 0.3f, true, true, false, false);

        // старые опоры у обоих берегов, кривые и расщеплённые
        Tower(m, p, 1.1f, true, PostOld);
        Tower(m, p, p.Lb - 1.1f, true, PostOld);

        // оборванные тросы: от верха старых опор тянутся к обрубку и обрываются, свисают в пропасть
        for (int end = 0; end < 2; end++)
            for (int side = -1; side <= 1; side += 2)
                for (int li = 0; li < 2; li++)
                {
                    float sS = end == 0 ? 1.1f : p.Lb - 1.1f;
                    Vector3 start = p.D(sS, side * (p.hw + TowerDx), li == 0 ? 1.1f : 0.6f);
                    Vector3 f, r, u;
                    p.Frame(sS, out f, out r, out u);
                    Vector3 dir = end == 0 ? f : -f;
                    float len = (li == 0 ? 2.8f : 1.9f) + (side > 0 ? 0.5f : 0f);
                    Vector3 a = start;
                    for (int k = 1; k <= 7; k++)
                    {
                        float t = k / 7f;
                        Vector3 b = start + dir * (len * t) - Vector3.up * (0.05f * k * k * 0.9f);
                        m.Beam(a, b, 0.04f, RopeC);
                        a = b;
                    }
                    m.Beam(a, a - Vector3.up * 1.4f + dir * 0.1f, 0.035f, RopeC);   // свободный конец свисает
                }

        Barrier(m, p);
        return m;
    }

    // ------------------------------------------------------------ вид 2: строится (основа: после этапа «Опоры»)
    static GBMesh BuildBuildingBase(GBPlan p, System.Random rnd, float sTA, float sTB)
    {
        var m = new GBMesh();
        float shoreA = p.sE + 1.1f, shoreB = p.sF - 1.1f;
        Planks(m, p, rnd, 0.3f, shoreA, false, false, false, true);
        Planks(m, p, rnd, shoreB, p.Lb - 0.3f, false, false, false, true);
        Tower(m, p, sTA, false, PostFresh);
        Tower(m, p, sTB, false, PostFresh);
        MainCables(m, p, sTA, sTB);
        FloorRopes(m, p, sTA, sTB);
        Barrier(m, p);
        return m;
    }

    // Добавка вида 2 после этапа «Настил»: доски растут к середине, подвесы там, где доски уже лежат. Посередине разрыв
    static GBMesh BuildBuildingStep2(GBPlan p, System.Random rnd, float sTA, float sTB)
    {
        var m = new GBMesh();
        float gapA, gapB;
        GapRange(p, out gapA, out gapB);
        float shoreA = p.sE + 1.1f, shoreB = p.sF - 1.1f;
        Planks(m, p, rnd, shoreA, gapA, false, false, true, true);
        Planks(m, p, rnd, gapB, shoreB, false, true, false, true);
        Suspenders(m, p, sTA, sTB, SuspenderPositions(sTA, sTB), 0f, gapA, gapB, p.Lb);
        return m;
    }

    // ------------------------------------------------------------ вид 3: готов
    static GBMesh BuildReady(GBPlan p, System.Random rnd, float sTA, float sTB)
    {
        var m = new GBMesh();
        // подложка под досками: в щелях видна тёмная полоса, швы читаются линией, а не дырой
        m.Sweep(p.P, 0.3f, p.Lb - 0.3f, 1f, -p.hw * 0.98f, p.hw * 0.98f, -0.115f, -0.075f, GBMesh.Sh(WoodD, 0.6f));
        Planks(m, p, rnd, 0.3f, p.Lb - 0.3f, false, false, false, false);
        Tower(m, p, sTA, false, PostFinal);
        Tower(m, p, sTB, false, PostFinal);
        MainCables(m, p, sTA, sTB);
        FloorRopes(m, p, sTA, sTB);
        var at = SuspenderPositions(sTA, sTB);
        Suspenders(m, p, sTA, sTB, at, 0f, p.Lb, 0f, -1f);
        Rails(m, p, sTA + 0.3f, sTB - 0.3f, at);
        // поперечные балки под настилом через каждые несколько досок: видно, на чём лежит настил
        for (float s = sTA + 0.6f; s < sTB; s += 2.3f)
        {
            Vector3 f, r, u;
            p.Frame(s, out f, out r, out u);
            m.Box(p.P(s) - u * 0.2f, r, u, f, p.hw + 0.1f, 0.05f, 0.07f, GBMesh.Sh(WoodD, 0.85f));
        }
        return m;
    }

    // ------------------------------------------------------------ граница у входа (вид 1 и 2)
    // Две стойки, две верёвки и крест из досок поперёк прохода. Стоит перед невидимой стеной ворот (GateBridgePlacement.GateWalls),
    // чтобы Лео упирался во что-то видимое. Решётки поверх настила нет: проход закрыт стеной, а не коллайдером этой модели
    static void Barrier(GBMesh m, GBPlan p)
    {
        float s = -0.15f;
        Vector3 f, r, u;
        p.Frame(0.5f, out f, out r, out u);
        Vector3 c = p.Pw(s);
        float dx = p.hw + 0.12f;
        for (int side = -1; side <= 1; side += 2)
        {
            m.Box(c + r * (side * dx) + u * 0.58f, r, u, f, 0.07f, 0.6f, 0.07f, PostC);
            m.Box(c + r * (side * dx) + u * 1.2f, r, u, f, 0.1f, 0.04f, 0.1f, GBMesh.Sh(PostC, 0.8f));
        }
        m.Beam(c - r * dx + u * 0.95f, c + r * dx + u * 0.95f, 0.04f, RopeC);
        m.Beam(c - r * dx + u * 0.5f, c + r * dx + u * 0.5f, 0.04f, RopeC);
        for (int k = -1; k <= 1; k += 2)
        {
            Quaternion q = Quaternion.AngleAxis(k * 32f, f);
            m.Box(c + u * 0.72f, q * r, q * u, f, 0.85f, 0.06f, 0.03f, WarnC);
        }
    }

    // ------------------------------------------------------------ коллайдеры ходьбы
    // Одна непрерывная лента: пол (верх = верх досок) с пологими въездами с обоих берегов плюс низкие бортики 0,9 м вдоль всего моста.
    // Бортики низкие: камера (SphereCast от груди, 1,3 м) идёт выше и не упирается в них, а упасть с моста они не дают
    static Mesh BuildWalkMesh(GBPlan p)
    {
        var m = new GBMesh();
        float hw = p.hw;
        float s0 = -0.7f, s1 = p.Lb + 0.9f;
        m.Sweep(p.Pw, s0, s1, 0.5f, -hw, hw, -0.3f, 0f, Color.white);
        m.Sweep(p.Pw, 0f, p.Lb + 1.2f, 0.5f, hw, hw + 0.2f, -0.3f, 0.9f, Color.white);
        m.Sweep(p.Pw, 0f, p.Lb + 1.2f, 0.5f, -hw - 0.2f, -hw, -0.3f, 0.9f, Color.white);
        return m.ToMesh("GateBridgeWalk");
    }
}

#endif
