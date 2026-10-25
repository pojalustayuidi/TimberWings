#if UNITY_EDITOR
// Assets/_Project/Editor/Features/LocationIslandOutline.cs
// Задача 1: контур острова по раскладке.
//
// Что делает:
//  • «Контур по раскладке» строит точки PathPoints у FloatingIsland: почти квадрат (скруглённый), по краю бухты и мысы.
//    Площадки, тропы, река, исток, пруд, ворота и Dock остаются ВНУТРИ контура с запасом на ровную кромку.
//    Где за рекой стоит Dock, контур сам делает выступ (мыс) под платформу.
//  • «Проверить контур» читает реальный сглаженный край FloatingIsland (вместе с его бухтами) и пишет в Console,
//    что подошло к краю слишком близко или вышло наружу.
// Ничего не двигает: ни площадки, ни тропы, ни реку, ни GatePoint. Старые точки не пропадают:
// прежний узел PathPoints переименовывается в PathPoints_old и выключается (одна копия), всё откатывается Ctrl+Z.
// После этого жми у FloatingIsland «Собрать остров» (стены и ворота пересоберутся вместе с ним).
//
// Единицы: u = метры / worldScale (как в LocationBuilder). В мир: x = origin.x + u.x * worldScale, z = origin.z + u.y * worldScale.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Settings = LocationBuilder.Settings;

public static class LocationIslandOutline
{
    const string BackupName = "PathPoints_old";
    const string DockClearName = "DockKeepClear";
    const float Safety = 0.6f;          // запас сверх расчёта: упрощение точек и сглаживание могут чуть срезать угол
    const float SimplifyTol = 0.3f;     // допуск упрощения точек, u
    const float MaxSegment = 12f;       // самый длинный отрезок между точками PathPoints, u

    // Что обязано остаться внутри контура: круг (центр, радиус, запас до края сверх радиуса)
    public struct Keep
    {
        public Vector2 c;
        public float r;
        public float clear;
        public string name;
        public Keep(Vector2 c, float r, float clear, string name) { this.c = c; this.r = r; this.clear = clear; this.name = name; }
    }

    struct PadP
    {
        public LocationBuilder.PadDef def;
        public Vector2 pos;
        public float hw, hd, rad;
        public bool round;
    }

    // ------------------------------------------------------------------ настройки

    public static Settings LoadSettings()
    {
        var s = new Settings();
        string json = EditorPrefs.GetString(LocationBuilder.PrefsKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { JsonUtility.FromJsonOverwrite(json, s); }
            catch (System.Exception) { s = new Settings(); }
        }
        s.Validate();
        return s;
    }

    // ------------------------------------------------------------------ раскладка (как LocationGen.SolveLayout, без рельефа)

    static float SS(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static List<PadP> SolvePads(Settings s)
    {
        float half = s.terrainSize * 0.5f;
        var list = new List<PadP>();
        foreach (var d in s.pads)
        {
            var p = new PadP { def = d, round = d.round };
            p.hw = d.round ? d.size.x : d.size.x * 0.5f;
            p.hd = d.round ? d.size.x : d.size.y * 0.5f;
            p.rad = d.round ? d.size.x : Mathf.Max(p.hw, p.hd);
            list.Add(p);
        }
        for (int i = 0; i < list.Count; i++)
        {
            var p = list[i];
            float a = p.def.angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));   // угол от севера по часовой, север = +Z
            float dist = Mathf.Max(p.def.distance, s.centerRadius + 0.5f + p.hd);
            float maxR = half - p.rad - 3f;
            for (int it = 0; it < 120; it++)
            {
                Vector2 pos = dir * dist;
                bool ok = true;
                for (int j = 0; j < i; j++)
                    if ((pos - list[j].pos).magnitude < p.rad + list[j].rad + s.minPadGap) { ok = false; break; }
                if (ok) break;
                dist += 0.5f;
            }
            while (dist > s.centerRadius + 1f && Mathf.Max(Mathf.Abs(dir.x * dist), Mathf.Abs(dir.y * dist)) > maxR) dist -= 0.5f;
            p.pos = dir * dist;
            list[i] = p;
        }
        return list;
    }

    static float RiverBaseX(Settings s, List<PadP> pads)
    {
        float half = s.terrainSize * 0.5f, hw = s.riverWidth * 0.5f;
        int bi = pads.FindIndex(q => q.def.name == "Bridge");
        if (s.newRiver && bi >= 0) return LocationBuilder.RiverBaseX2(s, pads[bi].pos.x, pads[bi].hd, Mathf.Min(s.riverSourceZ, half - 8f), half);
        float rb = bi >= 0 ? pads[bi].pos.x - pads[bi].hd - 0.8f - s.riverBank - hw : -half * 0.85f;
        return rb + s.riverOffsetX;
    }

    // ось русла, как LocationGen.RiverX
    static float RiverX(Settings s, float baseX, float z)
    {
        if (s.newRiver)
        {
            float hf = s.terrainSize * 0.5f;
            return LocationBuilder.RiverAxis2(s, baseX, LocationBuilder.ZonePos("Source").x, Mathf.Min(s.riverSourceZ, hf - 8f), hf, z);
        }
        float mea = s.riverMeander * Mathf.Sin(z * 0.027f + 0.4f) * SS(8f, 30f, Mathf.Abs(z));
        return baseX + s.riverCurve * Mathf.Sin(z * 0.05f) + 0.4f * s.riverCurve * Mathf.Sin(z * 0.11f) + mea;
    }

    static void AddLine(List<Keep> keeps, Vector2 a, Vector2 b, float step, float r, float clear, string name)
    {
        float len = Vector2.Distance(a, b);
        int n = Mathf.Max(1, Mathf.CeilToInt(len / step));
        for (int i = 0; i <= n; i++) keeps.Add(new Keep(Vector2.Lerp(a, b, i / (float)n), r, clear, name));
    }

    // Все объекты раскладки, которые контур обязан обойти снаружи. exitU: где река уходит за южную кромку.
    public static List<Keep> BuildKeeps(Settings s, Vector2 gateU, out Vector2 exitU)
    {
        var keeps = new List<Keep>();
        float half = s.terrainSize * 0.5f;
        float B = half - 3f;
        float edgeClear = s.rimFlatWidth + 1f;   // ровная кромка + 1 u зазора до обрыва
        var pads = SolvePads(s);

        // площадки (прямоугольные по полудиагонали)
        foreach (var p in pads)
        {
            float r = p.round ? p.rad + 2f : Mathf.Sqrt(p.hw * p.hw + p.hd * p.hd) + 1f;
            keeps.Add(new Keep(p.pos, r, edgeClear, "Площадка " + p.def.name));
        }

        // тропы: от центра к площадкам, у ответвлений от развилки на родительской тропе
        float trailR = s.trailWidthAtPad * 0.5f + s.trailMeander + 1f;
        foreach (var p in pads)
        {
            Vector2 from = Vector2.zero;
            if (!string.IsNullOrEmpty(p.def.parent))
            {
                int pi = pads.FindIndex(q => q.def.name == p.def.parent);
                if (pi >= 0 && pads[pi].pos.sqrMagnitude > 0.01f) from = pads[pi].pos.normalized * p.def.forkDistance;
            }
            AddLine(keeps, from, p.pos, 4f, trailR, edgeClear, "Тропа " + p.def.name);
        }

        // ворота: прямая тропа от центра, конец тропы почти у кромки
        if (gateU.sqrMagnitude > 1f)
        {
            Vector2 toGate = gateU.normalized;
            float gl = gateU.magnitude;
            AddLine(keeps, Vector2.zero, toGate * Mathf.Max(0f, gl - 4f), 4f, s.trailWidth * 0.5f + 1f, edgeClear, "Тропа к воротам");
        }
        keeps.Add(new Keep(gateU, 0.5f, edgeClear, "Ворота"));

        // пруд: центр (22,-44) по раскладке; если в настройках включено озеро, берём его
        if (s.lakeEnabled) keeps.Add(new Keep(s.lakePos, s.lakeRadius + s.lakeShore, edgeClear, "Пруд"));
        else keeps.Add(new Keep(new Vector2(22f, -44f), 11f + 7f, edgeClear, "Пруд"));

        // Dock: край платформы может выходить за кромку, поэтому запас всего 1 u
        keeps.Add(new Keep(s.dockPos, Mathf.Sqrt(s.dockSize.x * s.dockSize.x + s.dockSize.y * s.dockSize.y) * 0.5f, 1f, "Dock"));

        // река: диски вдоль оси с шириной ущелья и дальним склоном. К южному краю радиус сходит на нет,
        // иначе контур выпятился бы за южную кромку и река не вышла бы наружу
        float baseX = RiverBaseX(s, pads);
        exitU = new Vector2(RiverX(s, baseX, -B), -B);
        if (s.buildRiver)
        {
            float rr = s.riverWidth * 0.5f * 1.4f + s.gorgeFarWall + s.riverSlope;
            if (s.newRiver) rr = s.riverWidth * 0.5f * Mathf.Max(1f, s.riverEndWidth) * 1.2f + s.riverBankRun + s.riverBrowWidth;   // Р3: русло с берегами и бровкой
            float srcZ = Mathf.Min(s.riverSourceZ, half - 8f);
            for (float z = -B + 2f; z <= srcZ + 0.01f; z += 3f)
                keeps.Add(new Keep(new Vector2(RiverX(s, baseX, z), z), rr * SS(-B, -B + 14f, z), edgeClear, "Река"));
            if (s.buildRiver && s.riverHead)
                keeps.Add(new Keep(new Vector2(RiverX(s, baseX, srcZ), srcZ + 2f), s.headHalfWidth + 1f, edgeClear, "Исток (скала)"));
        }
        return keeps;
    }

    // ------------------------------------------------------------------ форма контура

    static Vector2 SuperNormal(Vector2 q, float a, float b, float p)
    {
        float gx = p * Mathf.Pow(Mathf.Abs(q.x / a), p - 1f) * Mathf.Sign(q.x) / a;
        float gy = p * Mathf.Pow(Mathf.Abs(q.y / b), p - 1f) * Mathf.Sign(q.y) / b;
        var g = new Vector2(gx, gy);
        return g.sqrMagnitude > 1e-12f ? g.normalized : Vector2.up;
    }

    static void BlurLoop(float[] v, int w, int passes)
    {
        int n = v.Length;
        var tmp = new float[n];
        for (int ps = 0; ps < passes; ps++)
        {
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                for (int k = -w; k <= w; k++) sum += v[((i + k) % n + n) % n];
                tmp[i] = sum / (2 * w + 1);
            }
            System.Array.Copy(tmp, v, n);
        }
    }

    static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return (p - (a + ab * t)).magnitude;
    }

    static bool PointInPoly(List<Vector2> poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            Vector2 a = poly[i], b = poly[j];
            if (((a.y > p.y) != (b.y > p.y)) && (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)) inside = !inside;
        }
        return inside;
    }

    // Ramer-Douglas-Peucker по замкнутой линии без рекурсии. Между опорными точками добавляем исходные, если разрыв больше maxSeg.
    static List<Vector2> SimplifyClosed(Vector2[] pts, float tol, float maxSeg)
    {
        int n = pts.Length;
        var res = new List<Vector2>();
        if (n < 8) { res.AddRange(pts); return res; }

        int far = 0; float bd = -1f;
        for (int i = 1; i < n; i++)
        {
            float d = (pts[i] - pts[0]).sqrMagnitude;
            if (d > bd) { bd = d; far = i; }
        }
        var keep = new bool[n];
        keep[0] = true; keep[far] = true;

        var stack = new Stack<int[]>();
        stack.Push(new[] { 0, far });
        stack.Push(new[] { far, n });
        while (stack.Count > 0)
        {
            var sg = stack.Pop();
            int a = sg[0], b = sg[1];
            if (b - a < 2) continue;
            Vector2 A = pts[a % n], Bp = pts[b % n];
            float md = -1f; int mi = -1;
            for (int i = a + 1; i < b; i++)
            {
                float d = DistToSeg(pts[i % n], A, Bp);
                if (d > md) { md = d; mi = i; }
            }
            if (md > tol && mi >= 0)
            {
                keep[mi % n] = true;
                stack.Push(new[] { a, mi });
                stack.Push(new[] { mi, b });
            }
        }

        bool has = false; Vector2 last = Vector2.zero;
        for (int i = 0; i < n; i++)
        {
            if (keep[i] || (has && Vector2.Distance(pts[i], last) >= maxSeg))
            {
                res.Add(pts[i]); last = pts[i]; has = true;
            }
        }
        return res;
    }

    // Контур в u против часовой: скруглённый квадрат + бухты и мысы, обходящий всё из keeps.
    // Для каждой точки базовой формы считаем, насколько её надо вынести наружу (L), чтобы объекты оказались внутри с запасом;
    // бухта внутрь возможна, только пока она не заходит на объект (max(бухта, L)).
    public static List<Vector2> ComputeOutlineU(Settings s, Vector2 gateU, Vector2 exitU, List<Keep> keeps)
    {
        float half = s.terrainSize * 0.5f;
        float B = half - 3f;                               // база чуть внутри Terrain
        float zMax = Mathf.Min(B, gateU.y + 4f);           // север: кромка за воротами
        var c = new Vector2(0f, (-B + zMax) * 0.5f);
        float a = B, b = (zMax + B) * 0.5f;
        float p = Mathf.Clamp(s.outlineRound, 2f, 12f);

        // плотные точки базовой формы (суперэллипс), потом равномерно по длине с шагом 1 u
        const int N = 2880;
        var dense = new Vector2[N];
        for (int i = 0; i < N; i++)
        {
            float th = 2f * Mathf.PI * i / N;
            float cs = Mathf.Cos(th), sn = Mathf.Sin(th);
            float r = Mathf.Pow(Mathf.Pow(Mathf.Abs(cs / a), p) + Mathf.Pow(Mathf.Abs(sn / b), p), -1f / p);
            dense[i] = c + new Vector2(r * cs, r * sn);
        }
        var cum = new float[N + 1];
        for (int i = 1; i <= N; i++) cum[i] = cum[i - 1] + Vector2.Distance(dense[i - 1], dense[i % N]);
        float total = cum[N];
        int M = Mathf.Max(32, Mathf.RoundToInt(total));

        var pos = new Vector2[M];
        var nrm = new Vector2[M];
        int seg = 0;
        for (int k = 0; k < M; k++)
        {
            float d = total * k / M;
            while (seg < N - 1 && cum[seg + 1] < d) seg++;
            float len = cum[seg + 1] - cum[seg];
            float t = len > 1e-5f ? (d - cum[seg]) / len : 0f;
            pos[k] = Vector2.Lerp(dense[seg], dense[(seg + 1) % N], t);
            nrm[k] = SuperNormal(pos[k] - c, a, b, p);
        }

        // желаемые бухты/мысы (низкая частота) и обязательный вынос L
        float sx = (s.seed % 997) * 1.37f + 11.3f, sz = (s.seed % 991) * 0.73f + 5.1f;
        var want = new float[M];
        var lim = new float[M];
        for (int k = 0; k < M; k++)
        {
            Vector2 q = pos[k], n = nrm[k], t = new Vector2(-n.y, n.x);
            float nv = 0.65f * Mathf.PerlinNoise(500f + q.x * 0.035f + sx, 500f + q.y * 0.035f + sz)
                     + 0.35f * Mathf.PerlinNoise(500f + q.x * 0.09f + sz, 500f + q.y * 0.09f + sx);
            float sg = (float)System.Math.Tanh((nv - 0.5f) * 2f * 2.2f);        // -1 бухта, +1 мыс
            float w = sg >= 0f ? sg * s.outlineCape : sg * s.outlineBay;
            // у ворот и у выхода реки край ровный
            float flat = SS(6f, 18f, Vector2.Distance(q, gateU)) * SS(6f, 18f, Vector2.Distance(q, exitU));
            want[k] = w * flat;

            float lo = -1e9f;
            for (int i = 0; i < keeps.Count; i++)
            {
                var kp = keeps[i];
                float R = kp.r + kp.clear;
                Vector2 dv = kp.c - q;
                float lat = Vector2.Dot(dv, t);
                if (Mathf.Abs(lat) >= R) continue;
                float ahi = Vector2.Dot(dv, n) + Mathf.Sqrt(R * R - lat * lat) + Safety;
                if (ahi > lo) lo = ahi;
            }
            lim[k] = lo;
        }

        BlurLoop(want, 5, 2);
        var off = new float[M];
        for (int k = 0; k < M; k++) off[k] = Mathf.Max(want[k], lim[k]);
        BlurLoop(off, 2, 3);
        for (int k = 0; k < M; k++) off[k] = Mathf.Max(off[k], lim[k]);

        var poly = new Vector2[M];
        for (int k = 0; k < M; k++) poly[k] = pos[k] + nrm[k] * off[k];
        return SimplifyClosed(poly, SimplifyTol, MaxSegment);
    }

    // ------------------------------------------------------------------ сцена

    static Vector2 GateU(Settings s, FloatingIsland b)
    {
        float ws = Mathf.Max(0.01f, s.worldScale);
        var gp = b.transform.Find("GatePoint");
        if (gp != null) return new Vector2((gp.position.x - s.origin.x) / ws, (gp.position.z - s.origin.z) / ws);
        return new Vector2(0f, 60f);   // по раскладке: север у кромки около (0,60)
    }

    public static void Rebuild(Settings s, bool confirm)
    {
        var b = Object.FindFirstObjectByType<FloatingIsland>();
        if (b == null)
        {
            Debug.LogWarning("Контур острова: в сцене нет FloatingIsland. Сначала Tools > BuilderSim > Локация > Create Floating Island.");
            return;
        }
        float ws = Mathf.Max(0.01f, s.worldScale);
        Vector3 org = s.origin;

        Vector2 gateU = GateU(s, b), exitU;
        var keeps = BuildKeeps(s, gateU, out exitU);
        var ptsU = ComputeOutlineU(s, gateU, exitU, keeps);
        if (ptsU.Count < 8) { Debug.LogWarning("Контур острова: получилось меньше 8 точек, проверь настройки."); return; }

        if (confirm && !EditorUtility.DisplayDialog("Контур острова по раскладке",
            "Будет построено " + ptsU.Count + " точек PathPoints. Прежние точки сохранятся в выключенном узле «" + BackupName + "» (одна копия), Ctrl+Z всё вернёт.\nПлощадки, тропы, река и GatePoint не двигаются.",
            "Построить", "Отмена")) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Контур острова по раскладке");

        // прежние точки в запасной узел
        var old = b.transform.Find(BackupName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var cur = b.transform.Find("PathPoints");
        if (cur != null)
        {
            Undo.RecordObject(cur.gameObject, "Contour backup");
            cur.name = BackupName;
            cur.gameObject.SetActive(false);
        }

        var rootGo = new GameObject("PathPoints");
        Undo.RegisterCreatedObjectUndo(rootGo, "Контур острова");
        rootGo.transform.SetParent(b.transform, false);
        for (int i = 0; i < ptsU.Count; i++)
        {
            var go = new GameObject("P" + i);
            go.transform.SetParent(rootGo.transform, false);
            go.transform.position = new Vector3(org.x + ptsU[i].x * ws, b.groundY, org.z + ptsU[i].y * ws);
        }

        // GatePoint создаём только если его не было (существующий не двигаем)
        if (b.transform.Find("GatePoint") == null)
        {
            var g = new GameObject("GatePoint");
            Undo.RegisterCreatedObjectUndo(g, "Gate point");
            g.transform.SetParent(b.transform, false);
            g.transform.position = new Vector3(org.x + gateU.x * ws, b.groundY, org.z + gateU.y * ws);
        }

        // место Dock: бухты острова сюда не заходят (штатный механизм Keep Clear у FloatingIsland)
        var dk = b.transform.Find(DockClearName);
        if (dk == null)
        {
            var d = new GameObject(DockClearName);
            Undo.RegisterCreatedObjectUndo(d, "Dock keep clear");
            d.transform.SetParent(b.transform, false);
            dk = d.transform;
        }
        dk.position = new Vector3(org.x + s.dockPos.x * ws, b.groundY, org.z + s.dockPos.y * ws);
        var fi = b.GetComponent<FloatingIsland>();
        if (fi != null)
        {
            Undo.RecordObject(fi, "Contour keep clear");
            var list = new List<Transform>();
            if (fi.keepClear != null) list.AddRange(fi.keepClear);
            if (!list.Contains(dk)) list.Add(dk);
            fi.keepClear = list.ToArray();
            fi.InvalidateProtect();
            EditorUtility.SetDirty(fi);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(b.gameObject.scene);

        Vector2 mn = ptsU[0], mx = ptsU[0];
        foreach (var q in ptsU) { mn = Vector2.Min(mn, q); mx = Vector2.Max(mx, q); }
        Debug.Log("Контур острова: " + ptsU.Count + " точек PathPoints. Размах в u: x " + mn.x.ToString("0.#") + " .. " + mx.x.ToString("0.#")
            + ", z " + mn.y.ToString("0.#") + " .. " + mx.y.ToString("0.#") + ". Dock в (" + s.dockPos.x.ToString("0.#") + ", " + s.dockPos.y.ToString("0.#")
            + "). Дальше: «Собрать остров» у FloatingIsland, потом «Проверить контур».", b);
    }

    // Сверка реального края (с бухтами FloatingIsland) со списком объектов раскладки
    public static void Check(Settings s)
    {
        var b = Object.FindFirstObjectByType<FloatingIsland>();
        if (b == null) { Debug.LogWarning("Проверка контура: в сцене нет FloatingIsland."); return; }
        float ws = Mathf.Max(0.01f, s.worldScale);
        List<Vector2> loopW = b.GetEdgeLoop();
        if (loopW == null || loopW.Count < 3) { Debug.LogWarning("Проверка контура: у FloatingIsland меньше 3 точек."); return; }

        var loop = new List<Vector2>(loopW.Count);
        foreach (var q in loopW) loop.Add(new Vector2((q.x - s.origin.x) / ws, (q.y - s.origin.z) / ws));

        Vector2 gateU = GateU(s, b), exitU;
        var keeps = BuildKeeps(s, gateU, out exitU);

        // худший запас по каждому имени: расстояние до края минус (радиус + ровная кромка), > 0 всё хорошо
        var worst = new Dictionary<string, float>();
        var worstAt = new Dictionary<string, Vector2>();
        foreach (var k in keeps)
        {
            float d = float.MaxValue;
            for (int i = 0; i < loop.Count; i++) d = Mathf.Min(d, DistToSeg(k.c, loop[i], loop[(i + 1) % loop.Count]));
            bool inside = PointInPoly(loop, k.c);
            float slack = (inside ? d : -d) - (k.r + k.clear - 1f);   // -1: зазор в 1 u сверх кромки считаем запасом
            float prev;
            if (!worst.TryGetValue(k.name, out prev) || slack < prev) { worst[k.name] = slack; worstAt[k.name] = k.c; }
        }

        // самые тесные места первыми (у реки радиус к южному краю сходит на нет, поэтому её выход за кромку ошибкой не считается)
        var names = new List<string>(worst.Keys);
        names.Sort((x, y) => worst[x].CompareTo(worst[y]));
        var sb = new StringBuilder();
        int bad = 0;
        foreach (var n in names)
        {
            float sl = worst[n];
            if (sl >= 0f) continue;
            bad++;
            Vector2 at = worstAt[n];
            sb.AppendLine("  " + (sl < -1f ? "СНАРУЖИ/У КРАЯ" : "тесно") + ": " + n + ", запас " + sl.ToString("0.0") + " u, место (" + at.x.ToString("0") + ", " + at.y.ToString("0") + ") u");
        }

        Vector2 mn = loop[0], mx = loop[0];
        foreach (var q in loop) { mn = Vector2.Min(mn, q); mx = Vector2.Max(mx, q); }
        string head = "Проверка контура: " + keeps.Count + " контрольных кругов (площадки, тропы, река, исток, пруд, ворота, Dock), край " + loop.Count
            + " точек, размах u: x " + mn.x.ToString("0") + ".." + mx.x.ToString("0") + ", z " + mn.y.ToString("0") + ".." + mx.y.ToString("0") + ".";
        if (bad == 0) Debug.Log(head + " Всё внутри с запасом на кромку.", b);
        else Debug.LogWarning(head + " Проблем: " + bad + ".\n" + sb
            + "Что делать: уменьши Bay Depth у FloatingIsland (бухты острова режут внутрь), добавь место в Keep Clear, либо пересобери контур кнопкой «Контур по раскладке».", b);
    }
}

public static class LocationIslandOutlineMenu
{
    [MenuItem("Tools/BuilderSim/Локация/Контур острова по раскладке", false, 102)]
    static void Rebuild() { LocationIslandOutline.Rebuild(LocationIslandOutline.LoadSettings(), true); }

    [MenuItem("Tools/BuilderSim/Локация/Проверить контур острова", false, 103)]
    static void Check() { LocationIslandOutline.Check(LocationIslandOutline.LoadSettings()); }
}
#endif
