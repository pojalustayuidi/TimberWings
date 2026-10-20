// Assets/_Project/Scripts/Location/IslandWaterfall.cs (НЕ в Editor)
// Водопад парящего острова: река доходит до края и красиво стекает вниз, в облака. Вешается рядом с FloatingIsland (на объект MapBorder).
//
// Что делает кнопка «Собрать остров» (FloatingIsland.Build вызывает этот скрипт сам):
//   1. Находит конец реки (меши с RiverFlow; или твой объект Manual Mouth) и ближайшее место на краю острова.
//   2. Прорезает в поле у края русло и делает край ровным, чтобы вода уходила в аккуратную выемку, а не висела над травой.
//   3. Строит продолжение реки до кромки, падающую струю (дуга, расширяется, внизу растворяется) и вторую, тонкую завесу.
//   4. Ставит туман: брызги на кромке и густой пар там, где вода входит в облака. Цвет воды берётся у реки, время суток меняет подсветку.
// Всё лежит под объектом Island/FallFx. Это обычные меши и частицы, анимация идёт в шейдерах Project/Waterfall и Project/WaterfallMist.
// Шейдеры нужны в сборке игры: ссылки на них хранятся тут же (fallShader, mistShader), как у облаков.
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(FloatingIsland))]
public class IslandWaterfall : MonoBehaviour
{
    public const string FxName = "FallFx";   // «Fx» в имени: миникарта прячет такие объекты

    [Header("Река")]
    public bool enabledFall = true;
    [Tooltip("Где искать реку (родитель её мешей). Пусто = все объекты с RiverFlow в сцене")]
    public Transform riverRoot;
    [Tooltip("Если автопоиск ошибся: поставь пустой объект там, где река должна уйти за край. Его синяя ось Z = куда течёт вода")]
    public Transform manualMouth;
    [Tooltip("Ширина потока, метры. 0 = по ширине реки (для Manual Mouth по умолчанию 4)")]
    public float widthOverride = 0f;
    [Tooltip("Обрезать воду реки по контуру острова, чтобы она не висела над облаками")]
    public bool trimRiverToOutline = true;
    // Бюджет частиц водопада: туман 90 + 40 + 30 + брызги 40 = 200
    public const int ParticleBudget = 200;

    [Header("Русло у края")]
    [Tooltip("Насколько русло ниже уровня воды (метры)")]
    public float channelDepth = 0.35f;
    [Tooltip("На сколько вода опускается от конца реки до кромки (метры): течение видно даже на ровном месте")]
    public float channelDrop = 0.35f;
    [Tooltip("Ширина плавных берегов русла, метры")]
    public float bankBlend = 2.2f;
    [Tooltip("Радиус, где край острова ровный (без шума и рваной скалы), метры")]
    public float flatRadius = 7f;
    [Tooltip("На сколько метров русло продолжается за кромку (прячет шов на выемке)")]
    public float channelExtend = 3f;

    [Header("Падение")]
    [Tooltip("Струя стекает по скале острова (лучи по мешам Cliff и Underside) и только ниже скалы падает свободно. Выключи, чтобы вернуть старую параболу от кромки")]
    public bool hugCliff = true;
    [Tooltip("Зазор между водой и камнем, метры")]
    public float hugClearance = 0.12f;
    [Tooltip("Шаг рядов при стекании по скале, метры (меньше = точнее по скале, больше полигонов)")]
    [Range(0.25f, 1.2f)] public float hugStep = 0.45f;
    [Tooltip("Высота падения, метры. 0 = до моря облаков")]
    public float fallHeight = 0f;
    [Tooltip("Как у CloudSea.topDepthFactor: на какой доле глубины острова стоит верхний слой облаков")]
    [Range(0.2f, 1.2f)] public float cloudTopFactor = 0.7f;
    [Tooltip("Насколько струя уходит ниже верхнего слоя облаков (метры)")]
    public float extraBelowClouds = 3f;
    [Tooltip("Скорость, с которой вода слетает с кромки (м/с): больше = струя уходит дальше от скалы")]
    public float throwSpeed = 2.2f;
    public float gravity = 12f;
    [Tooltip("Во сколько раз струя шире внизу, чем вверху")]
    [Range(1f, 2.5f)] public float spread = 1.45f;
    [Tooltip("Вторая, более тонкая завеса впереди основной струи: даёт объём")]
    public bool veil = true;
    [Tooltip("Продолжение реки до кромки рисуется материалом самой реки (иначе материалом струи)")]
    public bool runoutUsesRiverMaterial = true;
    [Tooltip("Метров пути воды на один повтор текстуры реки на продолжении")]
    public float runoutUvMeters = 4f;

    [Header("Вода")]
    [Tooltip("Взять оттенок воды у материала реки")]
    public bool colorFromRiver = true;
    public Color deepColor = new Color(0.30f, 0.62f, 0.86f);
    public Color lightColor = new Color(0.84f, 0.95f, 1.00f);
    public Color foamColor = Color.white;
    public float flowSpeed = 3.2f;

    [Header("Туман и брызги")]
    public bool mist = true;
    [Range(0f, 2f)] public float mistAmount = 1f;
    [Tooltip("Ветер, м/с (x, z): туман сносит так же, как облака")]
    public Vector2 wind = new Vector2(0.9f, 0.4f);

    [Header("Материалы (создаются при сборке)")]
    public Shader fallShader;
    public Shader mistShader;
    public Material fallMat;
    public Material runoutMat;
    public Material mistMat;

    // найденное место (заполняет PrepareMouth). Нужно и без редактора: от него зависит контур края острова
    [HideInInspector] public bool mouthReady;
    [HideInInspector] public Vector3 mouthPos;     // конец реки, y = уровень воды
    [HideInInspector] public Vector2 flowDir;      // куда течёт вода (xz, единичный)
    [HideInInspector] public Vector2 lipXZ;        // где поток пересекает край острова
    [HideInInspector] public Vector2 outNormal;    // наружная нормаль края в этой точке
    [HideInInspector] public float mouthWidth = 4f;

    FloatingIsland island;
    float tintAt;
    static readonly int TintId = Shader.PropertyToID("_Tint");

    public FloatingIsland Island
    {
        get { if (island == null) island = GetComponent<FloatingIsland>(); return island; }
    }

    // ---------- время суток ----------
    void Update()
    {
        if (Time.unscaledTime < tintAt) return;
        tintAt = Time.unscaledTime + 0.25f;
        if (fallMat == null && mistMat == null) return;

        float night = DayNightCycle.Night01, tw = DayNightCycle.Twilight01;
        float day = Mathf.Clamp01(1f - night - tw);
        Color c = DayNightCycle.Mix(Color.white, new Color(1.00f, 0.82f, 0.74f), new Color(0.34f, 0.42f, 0.68f), day, tw, night);
        if (fallMat != null) fallMat.SetColor(TintId, c);
        if (runoutMat != null && runoutMat != fallMat && runoutMat.HasProperty(TintId)) runoutMat.SetColor(TintId, c);
        if (mistMat != null) mistMat.SetColor(TintId, c);
    }

    // ---------- русло (читает FloatingIsland при сборке мешей и контура) ----------

    // Вес русла в точке (0..1) и высота дна. flat: 0..1, насколько рядом с руслом холмики и скат поля нужно убрать
    public float ChannelWeight(Vector2 q, out float bedY, out float flat)
    {
        bedY = 0f; flat = 0f;
        if (!mouthReady) return 0f;
        Vector2 a = new Vector2(mouthPos.x, mouthPos.z);
        Vector2 b = lipXZ + flowDir * channelExtend;
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        float s = len2 > 1e-4f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2) : 0f;
        float dist = Vector2.Distance(q, a + ab * s);
        float hw = mouthWidth * 0.5f + 0.25f;
        float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hw * 0.85f, hw + Mathf.Max(0.2f, bankBlend), dist));
        flat = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hw, hw + Mathf.Max(0.4f, bankBlend * 2f), dist));
        bedY = mouthPos.y - channelDepth - channelDrop * s;
        return w;
    }

    // Расстояние от точки до оси русла (для того чтобы не ставить декор в воду)
    public float DistToChannel(Vector2 q)
    {
        if (!mouthReady) return float.MaxValue;
        Vector2 a = new Vector2(mouthPos.x, mouthPos.z);
        Vector2 b = lipXZ + flowDir * channelExtend;
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        float s = len2 > 1e-4f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2) : 0f;
        return Vector2.Distance(q, a + ab * s);
    }

    // Уровень поверхности воды на доле s пути от конца реки (0) до конца русла за кромкой (1)
    float WaterYAt(float s) { return mouthPos.y - channelDepth * 0.15f - channelDrop * s; }

    // ---------- геометрия: поиск места ----------
    static bool PointInPolygon(List<Vector2> pts, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
        {
            Vector2 a = pts[i], b = pts[j];
            if (((a.y > p.y) != (b.y > p.y)) && (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x))
                inside = !inside;
        }
        return inside;
    }

    // Расстояние до края (всегда >= 0), ближайшая точка края и наружная нормаль там (контур против часовой)
    static float EdgeDistance(List<Vector2> loop, Vector2 p, out Vector2 closest, out Vector2 outN)
    {
        float best = float.MaxValue;
        closest = p; outN = Vector2.up;
        int n = loop.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = loop[i], b = loop[(i + 1) % n];
            Vector2 e = b - a;
            float l2 = e.sqrMagnitude;
            float t = l2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, e) / l2) : 0f;
            Vector2 c = a + e * t;
            float d = Vector2.Distance(p, c);
            if (d < best)
            {
                best = d; closest = c;
                Vector2 en = e.normalized;
                outN = new Vector2(en.y, -en.x);
            }
        }
        return best;
    }

    // Первое пересечение луча с контуром
    static bool RayLoop(List<Vector2> loop, Vector2 o, Vector2 d, out Vector2 hit, out Vector2 outN)
    {
        hit = o; outN = d;
        float best = float.MaxValue;
        bool found = false;
        int n = loop.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = loop[i], b = loop[(i + 1) % n];
            Vector2 e = b - a;
            float den = d.x * e.y - d.y * e.x;
            if (Mathf.Abs(den) < 1e-6f) continue;
            Vector2 ao = a - o;
            float t = (ao.x * e.y - ao.y * e.x) / den;
            float u = (ao.x * d.y - ao.y * d.x) / den;
            if (t > 0.01f && u >= 0f && u <= 1f && t < best)
            {
                best = t; found = true;
                hit = o + d * t;
                Vector2 en = e.normalized;
                outN = new Vector2(en.y, -en.x);
            }
        }
        return found;
    }

    // Точки мешей реки в мире. Нечитаемый меш заменяется сеткой точек по его габаритам
    List<Vector3> CollectRiverPoints(out Renderer bestSource, List<Renderer> sources, List<int> owner)
    {
        bestSource = null;
        var fx = Island.transform.Find(FloatingIsland.RootName + "/" + FxName);
        var rends = RiverRenderers();

        var pts = new List<Vector3>();
        foreach (var r in rends)
        {
            if (r == null || r is ParticleSystemRenderer) continue;
            if (fx != null && r.transform.IsChildOf(fx)) continue;
            int idx = sources.Count;
            sources.Add(r);
            var mf = r.GetComponent<MeshFilter>();
            var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh != null && mesh.isReadable)
            {
                var vs = mesh.vertices;
                int step = Mathf.Max(1, vs.Length / 40000);
                var tr = r.transform;
                for (int i = 0; i < vs.Length; i += step) { pts.Add(tr.TransformPoint(vs[i])); owner.Add(idx); }
            }
            else
            {
                Bounds b = r.bounds;
                for (int i = 0; i <= 8; i++)
                    for (int j = 0; j <= 8; j++)
                    {
                        pts.Add(new Vector3(Mathf.Lerp(b.min.x, b.max.x, i / 8f), b.max.y, Mathf.Lerp(b.min.z, b.max.z, j / 8f)));
                        owner.Add(idx);
                    }
            }
        }
        return pts;
    }

    // Автопоиск: конец реки, ближайший к краю острова. Возвращает положение, направление течения, ширину и меш-источник
    bool FindRiverEnd(List<Vector2> loop, out Vector3 pos, out Vector2 dir, out float width, out Renderer src)
    {
        pos = Vector3.zero; dir = Vector2.up; width = 4f; src = null;
        var sources = new List<Renderer>();
        var owner = new List<int>();
        Renderer dummy;
        var pts = CollectRiverPoints(out dummy, sources, owner);
        if (pts.Count == 0) return false;

        // точка реки внутри контура, ближайшая к краю
        float bestD = float.MaxValue;
        int bi = -1, outside = 0;
        float worstOut = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 q = new Vector2(pts[i].x, pts[i].z);
            Vector2 c, n;
            float d = EdgeDistance(loop, q, out c, out n);
            if (!PointInPolygon(loop, q)) { outside++; worstOut = Mathf.Max(worstOut, d); continue; }
            if (d < bestD) { bestD = d; bi = i; }
        }
        if (bi < 0) return false;
        if (outside > 0 && worstOut > 0.5f)
            Debug.LogWarning("IslandWaterfall: часть реки (" + worstOut.ToString("0.0") + " м) выходит за контур острова: этот кусок воды будет висеть в воздухе. Сдвинь точки контура наружу или укороти реку.");

        src = sources[owner[bi]];
        Vector2 p = new Vector2(pts[bi].x, pts[bi].z);

        // окрестность конца: ось реки (главная ось облака точек), центр
        var near = new List<Vector3>();
        const float R = 8f;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 q = new Vector2(pts[i].x, pts[i].z);
            if (Vector2.Distance(q, p) < R && PointInPolygon(loop, q)) near.Add(pts[i]);
        }

        Vector2 cn, outN;
        EdgeDistance(loop, p, out cn, out outN);
        if (near.Count < 4)
        {
            dir = outN; width = 4f;
            pos = new Vector3(p.x, pts[bi].y, p.y);
            return true;
        }

        Vector2 cen = Vector2.zero;
        foreach (var v in near) cen += new Vector2(v.x, v.z);
        cen /= near.Count;
        float cxx = 0f, cyy = 0f, cxy = 0f;
        foreach (var v in near)
        {
            float dx = v.x - cen.x, dz = v.z - cen.y;
            cxx += dx * dx; cyy += dz * dz; cxy += dx * dz;
        }
        float ang = 0.5f * Mathf.Atan2(2f * cxy, cxx - cyy);
        dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        if (Vector2.Dot(dir, p - cen) < 0f) dir = -dir;   // ось смотрит в сторону края
        Vector2 perp = new Vector2(-dir.y, dir.x);

        float maxAlong = float.MinValue;
        foreach (var v in near) maxAlong = Mathf.Max(maxAlong, Vector2.Dot(new Vector2(v.x, v.z) - cen, dir));

        float pmin = float.MaxValue, pmax = float.MinValue;
        var ys = new List<float>();
        foreach (var v in near)
        {
            Vector2 rel = new Vector2(v.x, v.z) - cen;
            if (Vector2.Dot(rel, dir) < maxAlong - 2f) continue;   // только последние 2 метра реки
            float pp = Vector2.Dot(rel, perp);
            pmin = Mathf.Min(pmin, pp); pmax = Mathf.Max(pmax, pp);
            ys.Add(v.y);
        }
        if (ys.Count == 0) { pmin = -2f; pmax = 2f; ys.Add(pts[bi].y); }
        ys.Sort();
        width = Mathf.Clamp(pmax - pmin, 1.5f, 16f);
        Vector2 xz = cen + dir * (maxAlong - 0.3f) + perp * ((pmin + pmax) * 0.5f);
        pos = new Vector3(xz.x, ys[ys.Count / 2], xz.y);
        return true;
    }

    // Находит конец реки и точку на краю, запоминает их. Вызывает FloatingIsland.Build перед сборкой поля и обрыва
    public bool PrepareMouth()
    {
        mouthReady = false;
        var fi = Island;
        if (fi == null) return false;
        var loop0 = fi.GetEdgeLoop();   // пока без ровного участка у водопада
        if (loop0.Count < 3) return false;
        if (trimRiverToOutline) TrimRiver(loop0);

        Vector3 pos; Vector2 dir; float width; Renderer src = null;
        if (manualMouth != null)
        {
            pos = manualMouth.position;
            Vector3 f = manualMouth.forward;
            dir = new Vector2(f.x, f.z);
            width = widthOverride > 0.1f ? widthOverride : 4f;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.zero;
        }
        else if (!FindRiverEnd(loop0, out pos, out dir, out width, out src))
        {
            Debug.LogWarning("IslandWaterfall: река не найдена (нет мешей с RiverFlow). Поставь Manual Mouth или укажи River Root. Остров соберётся без водопада.");
            return false;
        }
        if (widthOverride > 0.1f) width = widthOverride;

        Vector2 o = new Vector2(pos.x, pos.z);
        Vector2 hit, outN;
        if (dir.sqrMagnitude < 1e-4f || !RayLoop(loop0, o, dir.normalized, out hit, out outN))
        {
            Vector2 cl, n;
            EdgeDistance(loop0, o, out cl, out n);
            dir = n; hit = cl; outN = n;
        }
        dir = dir.normalized;

        // поток уходит с обрыва под прямым углом: сильно косое течение подворачиваем к нормали
        if (Vector2.Dot(dir, outN) < 0.75f)
        {
            dir = (dir * 0.5f + outN * 0.5f).normalized;
            Vector2 h2, n2;
            if (RayLoop(loop0, o, dir, out h2, out n2)) { hit = h2; outN = n2; }
        }

        float run = Vector2.Distance(o, hit);
        if (run > 25f)
            Debug.LogWarning("IslandWaterfall: конец реки далеко от края (" + run.ToString("0") + " м), русло получится длинным. Если река должна уходить в другом месте, задай Manual Mouth.");

        mouthPos = pos; flowDir = dir; lipXZ = hit; outNormal = outN; mouthWidth = width;
        mouthReady = true;

        // теперь край у водопада ровный: считаем точку выхода заново по нему
        var loop1 = fi.GetEdgeLoop();
        Vector2 h3, n3;
        if (loop1.Count >= 3 && RayLoop(loop1, o, dir, out h3, out n3)) { lipXZ = h3; outNormal = n3; }

        Debug.Log("IslandWaterfall: конец реки " + pos.ToString("0.0") + ", ширина " + width.ToString("0.0") + " м, до края " + Vector2.Distance(o, lipXZ).ToString("0.0") + " м" + (manualMouth != null ? " (Manual Mouth)" : "") + ".");
        mouthSource = src;
        return true;
    }

    Renderer mouthSource;   // меш реки, на котором нашли конец (берём цвет и материал); только на время сборки

    // ---------- меши ----------

    // Лента: центры рядов, полуширины (вектор поперёк потока), выпуклость вперёд, координата V и прозрачность каждого ряда
    static Mesh Ribbon(string name, Vector3[] c, Vector3[] side, Vector3[] bulge, float[] v, float[] alpha, int cols, float vOffset)
    {
        int rows = c.Length;
        var verts = new Vector3[rows * cols];
        var uvs = new Vector2[rows * cols];
        var cl = new Color[rows * cols];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
            {
                float u = j / (float)(cols - 1);
                float k = u * 2f - 1f;
                int idx = i * cols + j;
                verts[idx] = c[i] + side[i] * k + bulge[i] * (1f - k * k);
                uvs[idx] = new Vector2(u, v[i] + vOffset);
                cl[idx] = new Color(1f, 1f, 1f, alpha[i]);
            }
        var tris = new List<int>((rows - 1) * (cols - 1) * 6);
        for (int i = 0; i < rows - 1; i++)
            for (int j = 0; j < cols - 1; j++)
            {
                int a = i * cols + j, b = a + 1, d = a + cols, e = d + 1;
                tris.Add(a); tris.Add(d); tris.Add(b);
                tris.Add(b); tris.Add(d); tris.Add(e);
            }
        var m = new Mesh { name = name };
        m.vertices = verts; m.uv = uvs; m.colors = cl;
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static GameObject MeshPart(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    static bool ReadColor(Material m, out Color c)
    {
        c = Color.white;
        if (m == null) return false;
        if (m.HasProperty("_BaseColor")) { c = m.GetColor("_BaseColor"); return true; }
        if (m.HasProperty("_Color")) { c = m.GetColor("_Color"); return true; }
        return false;
    }

    Material EnsureFallMaterial()
    {
        if (fallShader == null) fallShader = Shader.Find("Project/Waterfall");
        if (fallShader == null)
        {
            Debug.LogWarning("IslandWaterfall: не найден шейдер Project/Waterfall (файл Waterfall.shader рядом со скриптом). Водопад не построен.");
            return null;
        }
        if (fallMat == null || fallMat.shader != fallShader) fallMat = new Material(fallShader) { name = "IslandWaterfall" };
        return fallMat;
    }

    // ---------- сборка водопада ----------

    // Строит всё под root/FallFx. Вызывается из FloatingIsland.Build после поля и обрыва
    public void BuildFall(Transform root)
    {
        var old = root.Find(FxName);
        if (old != null) DestroyImmediate(old.gameObject);
        if (!enabledFall || !mouthReady) return;

        var mat = EnsureFallMaterial();
        if (mat == null) return;

        // цвет воды у реки, материал продолжения
        Material riverMat = mouthSource != null ? mouthSource.sharedMaterial : null;
        RiverFlow riverFlow = mouthSource != null ? mouthSource.GetComponent<RiverFlow>() : null;
        Color deep = deepColor, light = lightColor;
        Color rc;
        if (colorFromRiver && ReadColor(riverMat, out rc))
        {
            deep = new Color(rc.r * 0.9f, rc.g * 0.95f, rc.b, 1f);
            light = Color.Lerp(new Color(rc.r, rc.g, rc.b, 1f), Color.white, 0.62f);
        }
        mat.SetColor("_Deep", deep);
        mat.SetColor("_Light", light);
        mat.SetColor("_Foam", foamColor);
        mat.SetFloat("_Speed", flowSpeed);
        mat.SetFloat("_Streaks", Mathf.Clamp(mouthWidth * 1.6f, 6f, 18f));
        mat.SetFloat("_FoamLen", 1.6f);

        var fx = new GameObject(FxName).transform;
        fx.SetParent(root, false);

        Vector2 f2 = flowDir;
        Vector3 f3 = new Vector3(f2.x, 0f, f2.y);
        Vector3 perp3 = new Vector3(-f2.y, 0f, f2.x);

        Vector2 a = new Vector2(mouthPos.x, mouthPos.z);
        float pathLen = Vector2.Distance(a, lipXZ + f2 * channelExtend);
        float lipS = pathLen > 0.01f ? Mathf.Clamp01(Vector2.Distance(a, lipXZ) / pathLen) : 1f;
        float lipY = WaterYAt(lipS);

        // 1. продолжение реки: от конца реки до кромки
        float runLen = Vector2.Distance(a, lipXZ);
        if (runLen > 0.5f)
        {
            const float overlap = 0.6f;
            int rows = Mathf.Max(3, Mathf.CeilToInt(runLen / 1.2f) + 1);
            var c = new Vector3[rows]; var sd = new Vector3[rows]; var bg = new Vector3[rows];
            var vv = new float[rows]; var al = new float[rows];
            float uvm = Mathf.Max(0.5f, runoutUvMeters);
            for (int i = 0; i < rows; i++)
            {
                float t = i / (float)(rows - 1);
                float dist = Mathf.Lerp(-overlap, runLen, t);
                float s = pathLen > 0.01f ? Mathf.Clamp01(dist / pathLen) : 0f;
                Vector3 p = new Vector3(a.x, 0f, a.y) + f3 * dist;
                p.y = WaterYAt(s) + 0.02f;
                c[i] = p;
                sd[i] = perp3 * (mouthWidth * 0.5f);
                bg[i] = Vector3.zero;
                vv[i] = dist / uvm;
                al[i] = 1f;
            }
            Material runMat;
            bool useRiver = runoutUsesRiverMaterial && riverMat != null;
            if (useRiver) runMat = riverMat;
            else
            {
                if (runoutMat == null || runoutMat.shader != fallShader) runoutMat = new Material(fallShader) { name = "IslandWaterfallRunout" };
                runoutMat.CopyPropertiesFromMaterial(mat);
                runoutMat.SetFloat("_Speed", flowSpeed * 0.45f);
                runoutMat.SetFloat("_FoamLen", 0.1f);
                runMat = runoutMat;
                vv = ScaleArr(vv, uvm);   // метры, как ждёт шейдер струи
            }
            var go = MeshPart(fx, "FallRunout", Ribbon("FallRunout", c, sd, bg, vv, al, 5, 0f), runMat);
            if (useRiver && riverFlow != null)
            {
                var rf = go.AddComponent<RiverFlow>();
                rf.speed = riverFlow.speed;
            }
        }

        // 2. падающая струя: сначала стекает по скале острова, ниже скалы падает свободно и растворяется в облаках
        float endY;
        if (fallHeight > 0.5f) endY = lipY - fallHeight;
        else
        {
            // верх облаков берём у самого CloudSea, если он есть в сцене (иначе своё значение), чтобы вода уходила ровно в облака
            float topF = cloudTopFactor;
            var cs = Object.FindFirstObjectByType<CloudSea>();
            if (cs != null) topF = cs.topDepthFactor;
            endY = Island.GroundY - Mathf.Max(8f, Island.depth) * topF - extraBelowClouds;
        }
        float H = Mathf.Max(3f, lipY - endY);
        Vector3 lip3 = new Vector3(lipXZ.x, lipY + 0.02f, lipXZ.y) + f3 * 0.05f;

        const int M = 9;                                   // колонок поперёк струи
        var rowsP = new List<Vector3[]>();                 // точки рядов (M штук)
        var rowsA = new List<float>();                     // прозрачность ряда
        float hw0 = mouthWidth * 0.5f;
        bool hugged = hugCliff && BuildHug(root, lip3, f3, perp3, endY, hw0, M, rowsP);
        int hugCount = rowsP.Count;
        for (int i = 0; i < hugCount; i++) rowsA.Add(1f);

        Vector3 start = hugged ? RowCenter(rowsP[rowsP.Count - 1]) : lip3;
        float v0 = hugged ? throwSpeed * 0.3f : throwSpeed;
        float g = Mathf.Max(2f, gravity);
        float H2 = start.y - endY;
        if (!hugged || H2 > 0.4f)
        {
            float T = Mathf.Sqrt(2f * Mathf.Max(0.5f, H2) / g);
            int NF = hugged ? 12 : 26;
            for (int i = hugged ? 1 : 0; i <= NF; i++)
            {
                float k = i / (float)NF;
                float tt = T * Mathf.Pow(k, 1.12f);
                Vector3 pc = start + f3 * (v0 * tt) + Vector3.down * (0.5f * g * tt * tt);
                float fdesc = Mathf.Clamp01((lipY - pc.y) / H);
                float hwr = hw0 * Mathf.Lerp(1f, spread, Mathf.SmoothStep(0f, 1f, fdesc));
                float bulge = 0.10f + 0.25f * Mathf.SmoothStep(0f, 1f, k);
                var row = new Vector3[M];
                for (int j = 0; j < M; j++)
                {
                    float kk = j / (float)(M - 1) * 2f - 1f;
                    row[j] = pc + perp3 * (kk * hwr) + f3 * (bulge * (1f - kk * kk));
                }
                rowsP.Add(row);
                rowsA.Add(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hugged ? 0.35f : 0.55f, 1f, k)));
            }
        }
        else
        {
            // скала дошла до самых облаков: просто гасим последние ряды
            int n = rowsA.Count;
            if (n >= 3) { rowsA[n - 3] = 0.85f; rowsA[n - 2] = 0.5f; rowsA[n - 1] = 0.15f; }
        }

        int RR = rowsP.Count;
        var fv = new float[RR];
        {
            float acc = 0f;
            Vector3 prevC = RowCenter(rowsP[0]);
            for (int i = 0; i < RR; i++)
            {
                Vector3 cc = RowCenter(rowsP[i]);
                if (i > 0) acc += Vector3.Distance(prevC, cc);
                prevC = cc;
                fv[i] = acc;
            }
        }
        MeshPart(fx, "FallSheet", GridRibbon("FallSheet", rowsP, fv, rowsA.ToArray(), M, 0f), mat);

        if (veil)
        {
            var vrows = new List<Vector3[]>();
            var va = new float[RR];
            for (int i = 0; i < RR; i++)
            {
                float k = i / (float)Mathf.Max(1, RR - 1);
                Vector3 cen = RowCenter(rowsP[i]);
                var row = new Vector3[M];
                for (int j = 0; j < M; j++) row[j] = cen + (rowsP[i][j] - cen) * 0.78f + f3 * (0.22f + 0.30f * k);
                vrows.Add(row);
                va[i] = rowsA[i] * 0.55f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.12f, k));   // у кромки завеса растёт из ничего
            }
            MeshPart(fx, "FallVeil", GridRibbon("FallVeil", vrows, fv, va, M, 5.3f), mat);
        }

        // 3. туман
        if (mist && mistAmount > 0.01f) BuildMist(fx, lip3, RowCenter(rowsP[RR - 1]), f3, perp3, endY);

        Debug.Log("IslandWaterfall: водопад собран, высота " + H.ToString("0.0") + " м" + (hugged ? ", по скале " + hugCount + " рядов." : ", по параболе (скала у кромки не найдена)."));
    }

    static Vector3 RowCenter(Vector3[] row)
    {
        Vector3 s = Vector3.zero;
        for (int i = 0; i < row.Length; i++) s += row[i];
        return s / row.Length;
    }

    // Сетка рядов x колонок: uv.x = 0..1 поперёк, uv.y = путь воды (метры), цвет A = прозрачность ряда
    static Mesh GridRibbon(string name, List<Vector3[]> rows, float[] v, float[] alpha, int cols, float vOffset)
    {
        int R = rows.Count;
        var verts = new Vector3[R * cols];
        var uvs = new Vector2[R * cols];
        var cl = new Color[R * cols];
        for (int i = 0; i < R; i++)
            for (int j = 0; j < cols; j++)
            {
                int idx = i * cols + j;
                verts[idx] = rows[i][j];
                uvs[idx] = new Vector2(j / (float)(cols - 1), v[i] + vOffset);
                cl[idx] = new Color(1f, 1f, 1f, alpha[i]);
            }
        var tris = new List<int>((R - 1) * (cols - 1) * 6);
        for (int i = 0; i < R - 1; i++)
            for (int j = 0; j < cols - 1; j++)
            {
                int a0 = i * cols + j, b0 = a0 + 1, d0 = a0 + cols, e0 = d0 + 1;
                tris.Add(a0); tris.Add(d0); tris.Add(b0);
                tris.Add(b0); tris.Add(d0); tris.Add(e0);
            }
        var m = new Mesh { name = name };
        m.vertices = verts; m.uv = uvs; m.colors = cl;
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // Стекание по скале. От кромки вниз берём лучами внутрь острова (с внешней стороны) расстояние до обрыва и подвала.
    // Лучи идут по временным MeshCollider на Cliff и Underside (после замеров удаляются).
    // Расстояния сначала "раздуваются" (максимум по соседям), потом сглаживаются: вода идёт чуть впереди камня и не врезается в выступы.
    // Возвращает false, если скалу у кромки найти не удалось: тогда водопад строится старой параболой.
    bool BuildHug(Transform root, Vector3 lip3, Vector3 f3, Vector3 perp3, float endY, float hw0, int M, List<Vector3[]> rows)
    {
        var cols = new List<Collider>();
        var temps = new List<MeshCollider>();
        string[] names = { "Cliff", "Underside" };
        foreach (var nm in names)
        {
            var t = root.Find(nm);
            if (t == null) continue;
            var existing = t.GetComponent<Collider>();
            if (existing != null) { cols.Add(existing); continue; }
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mc = t.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            cols.Add(mc);
            temps.Add(mc);
        }
        if (cols.Count == 0) return false;

        bool ok = false;
        try
        {
            Physics.SyncTransforms();
            float lipY = lip3.y;
            float step = Mathf.Max(0.25f, hugStep);
            float total = Mathf.Max(1f, lipY - endY);
            var raw = new List<float[]>();
            var ys = new List<float>();
            int firstValid = -1, lastValid = -1;
            for (int i = 0; i < 160; i++)
            {
                float y = lipY - 0.05f - i * step;
                if (y < endY) break;
                float f = Mathf.Clamp01((lipY - y) / total);
                float hw = hw0 * Mathf.Lerp(1f, spread, Mathf.SmoothStep(0f, 1f, f));
                var row = new float[M];
                int hits = 0;
                for (int j = 0; j < M; j++)
                {
                    float o = (j / (float)(M - 1) * 2f - 1f) * hw;
                    Vector3 origin = lip3 + perp3 * o + f3 * 6f;
                    origin.y = y;
                    var ray = new Ray(origin, -f3);
                    float best = float.MaxValue;
                    Vector3 bp = Vector3.zero;
                    foreach (var col in cols)
                    {
                        RaycastHit h;
                        if (col.Raycast(ray, out h, 14f) && h.distance < best) { best = h.distance; bp = h.point; }
                    }
                    if (best < float.MaxValue) { row[j] = Vector3.Dot(bp - lip3, f3); hits++; }
                    else row[j] = float.NaN;
                }
                bool valid = hits >= (M + 1) / 2;
                if (valid) { if (firstValid < 0) firstValid = i; lastValid = i; }
                else if (firstValid >= 0) break;     // скала кончилась: дальше свободное падение
                else if (i > 8) break;               // у кромки скалы нет
                raw.Add(row);
                ys.Add(y);
            }
            if (firstValid < 0 || lastValid - firstValid < 3) return false;

            int R = lastValid - firstValid + 1;
            var d = new float[R, M];
            for (int r = 0; r < R; r++)
            {
                var row = raw[firstValid + r];
                float sum = 0f; int cnt = 0;
                for (int j = 0; j < M; j++) if (!float.IsNaN(row[j])) { sum += row[j]; cnt++; }
                float mean = cnt > 0 ? sum / cnt : 0f;
                for (int j = 0; j < M; j++) d[r, j] = float.IsNaN(row[j]) ? mean : row[j];
            }
            // раздуть: максимум по соседним рядам и колонкам
            var m1 = new float[R, M];
            for (int r = 0; r < R; r++)
                for (int j = 0; j < M; j++)
                {
                    float mx = float.MinValue;
                    for (int dr = -1; dr <= 1; dr++)
                        for (int dj = -1; dj <= 1; dj++)
                        {
                            int rr = Mathf.Clamp(r + dr, 0, R - 1), jj = Mathf.Clamp(j + dj, 0, M - 1);
                            mx = Mathf.Max(mx, d[rr, jj]);
                        }
                    m1[r, j] = mx;
                }
            // сгладить по рядам (среднее не меньше исходного: максимум уже взят)
            var d2 = new float[R, M];
            for (int r = 0; r < R; r++)
                for (int j = 0; j < M; j++)
                {
                    float sum = 0f;
                    for (int dr = -1; dr <= 1; dr++) sum += m1[Mathf.Clamp(r + dr, 0, R - 1), j];
                    d2[r, j] = sum / 3f;
                }

            // верхний ряд: ровно на кромке, откуда вода уходит с реки
            var lipRow = new Vector3[M];
            for (int j = 0; j < M; j++)
            {
                float o = (j / (float)(M - 1) * 2f - 1f) * hw0;
                lipRow[j] = lip3 + perp3 * o;
            }
            rows.Add(lipRow);

            for (int r = 0; r < R; r++)
            {
                float y = ys[firstValid + r];
                float f = Mathf.Clamp01((lipY - y) / total);
                float hw = hw0 * Mathf.Lerp(1f, spread, Mathf.SmoothStep(0f, 1f, f));
                var row = new Vector3[M];
                for (int j = 0; j < M; j++)
                {
                    float kk = j / (float)(M - 1) * 2f - 1f;
                    float off = Mathf.Max(0.02f, d2[r, j] + hugClearance) + 0.06f * (1f - kk * kk);
                    Vector3 p = lip3 + perp3 * (kk * hw) + f3 * off;
                    p.y = y;
                    row[j] = p;
                }
                rows.Add(row);
            }
            ok = true;
        }
        finally
        {
            foreach (var mc in temps) if (mc != null) DestroyImmediate(mc);
            if (!ok) rows.Clear();
        }
        return ok;
    }

    static float[] ScaleArr(float[] a, float k)
    {
        var r = new float[a.Length];
        for (int i = 0; i < a.Length; i++) r[i] = a[i] * k;
        return r;
    }

    ParticleSystem NewParticles(Transform parent, string name, Vector3 pos, Quaternion rot, Material m)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = rot;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // иначе в редакторе нельзя менять длительность
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = m;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    void BuildMist(Transform fx, Vector3 lip, Vector3 landing, Vector3 f3, Vector3 perp3, float endY)
    {
        if (mistShader == null) mistShader = Shader.Find("Project/WaterfallMist");
        if (mistShader == null)
        {
            Debug.LogWarning("IslandWaterfall: не найден шейдер Project/WaterfallMist (файл WaterfallMist.shader рядом со скриптом): тумана не будет.");
            return;
        }
        if (mistMat == null || mistMat.shader != mistShader) mistMat = new Material(mistShader) { name = "IslandWaterfallMist" };

        Quaternion face = Quaternion.LookRotation(f3, Vector3.up);
        float k = mistAmount;
        float w = mouthWidth;
        Vector3 wv = new Vector3(wind.x, 0f, wind.y);

        // пар там, где вода входит в облака
        {
            var ps = NewParticles(fx, "MistBase", new Vector3(landing.x, endY + 2.5f, landing.z), face, mistMat);
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 90;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.2f, 5.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(4.5f, 8.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.92f, 0.96f, 1f, 0.55f), new Color(1f, 1f, 1f, 0.85f));
            var em = ps.emission; em.rateOverTime = 13f * k;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(w * 1.5f, 1.2f, Mathf.Max(3f, w));
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(wv.x * 0.5f, wv.x * 1.1f);
            vel.y = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
            vel.z = new ParticleSystem.MinMaxCurve(wv.z * 0.5f, wv.z * 1.1f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.28f), new GradientAlphaKey(0.55f, 0.65f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(1f, 1.7f)));
        }

        // небольшое облачко посередине падения: вода не «висит» голой полосой
        {
            Vector3 mid = Vector3.Lerp(lip, landing, 0.62f);
            var ps = NewParticles(fx, "MistMid", mid + f3 * 0.6f, face, mistMat);
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.96f, 1f, 0.25f), new Color(1f, 1f, 1f, 0.45f));
            var em = ps.emission; em.rateOverTime = 7f * k;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(w * 1.1f, 2.5f, 0.3f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(wv.x * 0.4f, wv.x * 0.9f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.4f, 0.2f);
            vel.z = new ParticleSystem.MinMaxCurve(wv.z * 0.4f, wv.z * 0.9f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.6f)));
        }

        // брызги у кромки
        {
            var ps = NewParticles(fx, "MistLip", lip + f3 * 0.35f, face, mistMat);
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 30;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.35f), new Color(1f, 1f, 1f, 0.6f));
            var em = ps.emission; em.rateOverTime = 9f * k;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(w * 0.9f, 0.2f, 0.5f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(f3.x * 0.4f - 0.2f, f3.x * 1.2f + 0.2f);
            vel.y = new ParticleSystem.MinMaxCurve(0.2f, 1.1f);
            vel.z = new ParticleSystem.MinMaxCurve(f3.z * 0.4f - 0.2f, f3.z * 1.2f + 0.2f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.5f)));
        }

        // брызги: мелкие капли с кромки вниз и наружу (последние 40 из бюджета 200)
        {
            var ps = NewParticles(fx, "Splash", lip + f3 * 0.2f, face, mistMat);
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.5f), new Color(1f, 1f, 1f, 0.85f));
            main.gravityModifier = 0.6f;
            var em = ps.emission; em.enabled = true; em.rateOverTime = 30f * Mathf.Clamp(k, 0.2f, 1.5f);   // 30*0.85 с < 40
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(w * 0.9f, 0.1f, 0.3f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                       new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
        }
    }

    // ---------- сервис ----------

    // Рендереры воды: riverRoot, иначе RiverFlow, иначе Location/River/Water из LocationBuilder
    List<Renderer> RiverRenderers()
    {
        var rends = new List<Renderer>();
        if (riverRoot != null) rends.AddRange(riverRoot.GetComponentsInChildren<Renderer>(true));
        else
        {
            foreach (var rf in Object.FindObjectsByType<RiverFlow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var r = rf.GetComponent<Renderer>();
                if (r != null) rends.Add(r);
            }
            if (rends.Count == 0)
            {
                // вода LocationBuilder: меш LocWater (ищем по всей сцене, в том числе среди неактивных, не полагаясь на имя корня)
                foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var mfw = mr.GetComponent<MeshFilter>();
                    if (mfw != null && mfw.sharedMesh != null && mfw.sharedMesh.name.StartsWith("LocWater")) rends.Add(mr);
                }
            }
        }
        return rends;
    }

    // Убирает треугольники воды, выходящие за контур острова (вершины пересобираются, лишние выбрасываются)
    void TrimRiver(List<Vector2> loop)
    {
        foreach (var r in RiverRenderers())
        {
            if (r == null || r is ParticleSystemRenderer) continue;
            var mf = r.GetComponent<MeshFilter>();
            var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null || !mesh.isReadable || mesh.subMeshCount != 1) continue;
            var vs = mesh.vertices;
            var tr = r.transform;
            var inside = new bool[vs.Length];
            bool anyOut = false;
            for (int i = 0; i < vs.Length; i++)
            {
                Vector3 q = tr.TransformPoint(vs[i]);
                inside[i] = PointInPolygon(loop, new Vector2(q.x, q.z));
                if (!inside[i]) anyOut = true;
            }
            if (!anyOut) continue;
            var tris = mesh.triangles;
            var uv0 = new List<Vector2>(); mesh.GetUVs(0, uv0);
            var uv1 = new List<Vector2>(); mesh.GetUVs(1, uv1);
            var remap = new int[vs.Length];
            for (int i = 0; i < remap.Length; i++) remap[i] = -1;
            var nv = new List<Vector3>(); var n0 = new List<Vector2>(); var n1 = new List<Vector2>(); var nt = new List<int>();
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                if (!(inside[a] && inside[b] && inside[c])) continue;
                foreach (int k in new[] { a, b, c })
                {
                    if (remap[k] < 0)
                    {
                        remap[k] = nv.Count; nv.Add(vs[k]);
                        if (uv0.Count == vs.Length) n0.Add(uv0[k]);
                        if (uv1.Count == vs.Length) n1.Add(uv1[k]);
                    }
                    nt.Add(remap[k]);
                }
            }
            if (nt.Count == 0) continue;
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(nv);
            if (n0.Count == nv.Count) mesh.SetUVs(0, n0);
            if (n1.Count == nv.Count) mesh.SetUVs(1, n1);
            mesh.SetTriangles(nt, 0);
            var nn = new Vector3[nv.Count];
            for (int i = 0; i < nn.Length; i++) nn[i] = Vector3.up;
            mesh.normals = nn;
            mesh.RecalculateBounds();
#if UNITY_EDITOR
            EditorUtility.SetDirty(mesh);
#endif
        }
    }

    public void RemoveFall()
    {
        var root = Island.transform.Find(FloatingIsland.RootName);
        if (root != null)
        {
            var fx = root.Find(FxName);
            if (fx != null) DestroyImmediate(fx.gameObject);
        }
        mouthReady = false;
    }

    void OnDrawGizmosSelected()
    {
        if (!mouthReady) return;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.95f);
        Vector3 a = mouthPos;
        Vector3 b = new Vector3(lipXZ.x, mouthPos.y, lipXZ.y);
        Gizmos.DrawSphere(a, 0.35f);
        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(b, 0.5f);
        Vector3 perp = new Vector3(-flowDir.y, 0f, flowDir.x) * (mouthWidth * 0.5f);
        Gizmos.DrawLine(a - perp, a + perp);
        Gizmos.DrawLine(b - perp, b + perp);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(IslandWaterfall))]
public class IslandWaterfallEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var w = (IslandWaterfall)target;
        GUILayout.Space(8);
        if (GUILayout.Button("Найти реку (показать точку в Scene)"))
        {
            if (w.PrepareMouth()) EditorSceneManager.MarkSceneDirty(w.gameObject.scene);
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Собрать остров с водопадом", GUILayout.Height(30))) w.Island.Build();
        if (GUILayout.Button("Убрать водопад (потом пересобери остров)")) w.RemoveFall();
        EditorGUILayout.HelpBox("Водопад собирается вместе с островом. Если река ушла не туда, поставь пустой объект в Manual Mouth: его синяя ось Z = куда течёт вода.", MessageType.None);
    }
}

public static class IslandWaterfallMenu
{
    [MenuItem("Tools/BuilderSim/Add Island Waterfall")]
    static void Add()
    {
        var f = Object.FindFirstObjectByType<FloatingIsland>();
        if (f == null) { Debug.LogWarning("IslandWaterfall: в сцене нет FloatingIsland. Сначала Tools/BuilderSim/Create Floating Island."); return; }
        var w = f.GetComponent<IslandWaterfall>();
        if (w == null) w = Undo.AddComponent<IslandWaterfall>(f.gameObject);
        Selection.activeGameObject = f.gameObject;
        f.Build();
    }
}
#endif
