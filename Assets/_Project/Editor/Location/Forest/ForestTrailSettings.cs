#if UNITY_EDITOR
// Assets/_Project/Editor/Location/Forest/ForestTrailSettings.cs (Editor)
// Тропы леса, часть 1: настройки, данные плана, сбор данных из сцены, предпросмотр плана в Scene View.
//   ForestSettings (partial)  - поля троп (видны в «Настройки генерации» в режиме карты плотности)
//   ForestTrailInput          - что взято из сцены: зона леса, арка, лесопилка, ForestFindSpot, центры подзон
//   ForestTrailPlan           - результат раскладки: пути в мировых метрах (x,z) с полуширинами
//   ForestTrailPlanner        - Collect() читает сцену, Plan() вызывает раскладку на MakeTrail из LocationGen
//   ForestTrailPreview        - меню Tools/Forest/Тропы: план (предпросмотр): линии и поляны в Scene View, ничего не создаёт
// Сами меши, обочины и пересборка леса: ForestTrails.cs (часть 2).
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class ForestSettings
{
    [Header("Тропы: форма")] [DensityOnly]
    [Tooltip("Полная ширина главной тропы у арки, м (широкая, мягкая)")]
    [Min(1f)] public float forestTrailMouthWidth = 3.4f;
    [DensityOnly] [Tooltip("Полная ширина главной тропы в «коридоре» из деревьев, м")]
    [Min(0.6f)] public float forestTrailCorridorWidth = 1.8f;
    [DensityOnly] [Tooltip("Полная ширина ответвлений, м (у развилки чуть шире)")]
    [Min(0.6f)] public float forestTrailBranchWidth = 1.4f;
    [DensityOnly] [Tooltip("Длина от арки, на которой тропа сужается от устья до коридора, м")]
    [Min(1f)] public float forestTrailNarrowLength = 7f;
    [DensityOnly] [Tooltip("Расширение воронкой перед поляной (на каждую сторону), м")]
    [Min(0f)] public float forestTrailFlare = 0.9f;
    [DensityOnly] [Tooltip("Амплитуда изгибов (trailMeander из LocationGen), м")]
    [Min(0f)] public float forestTrailMeander = 3f;
    [DensityOnly] [Tooltip("Доля ширины, которая уходит в мягкий край (используется при сборке мешей)")]
    [Range(0.05f, 0.6f)] public float forestTrailSoftEdge = 0.35f;
    [DensityOnly] [Tooltip("Радиус тупиковой поляны (находка, NPC), м")]
    [Min(1f)] public float forestFindGladeRadius = 3.2f;
    [DensityOnly] [Tooltip("2 = берёзовое ответвление и тупиковая поляна; 3 = плюс короткий отросток")]
    [Range(2, 3)] public int forestTrailBranches = 3;
    [DensityOnly] [Tooltip("Другой вариант изгибов и мест развилок при том же seed леса")]
    public int forestTrailVariant = 0;

    [Header("Тропы: лес вдоль")] [DensityOnly]
    [Tooltip("Коридор: насколько гуще лес вдоль тропы за полосой clearance (0 = без усиления)")]
    [Range(0f, 1f)] public float forestTrailWallBoost = 0.6f;
    [DensityOnly] [Tooltip("Ширина полосы густого леса вдоль тропы, м")]
    [Min(0.5f)] public float forestTrailWallBand = 4f;

    [Header("Тропы: обочины")] [DensityOnly]
    [Tooltip("Общий множитель количества камней, цветов и корней на обочинах (0 = ничего)")]
    [Range(0f, 2f)] public float forestTrailDecor = 1f;
    [DensityOnly] public bool forestTrailRocks = true;
    [DensityOnly] public bool forestTrailFlowers = true;
    [DensityOnly] public bool forestTrailRoots = true;
}

// ------------------------------------------------------------ данные

public sealed class ForestTrailInput
{
    public Vector2 center;                  // центр зоны леса (x, z)
    public float radius;                    // круг: radius, прямоугольник: меньшая половина стороны
    public Func<Vector2, float> depth;      // метров внутрь зоны леса (>0 внутри)
    public Vector2 gate;                    // арка (ForestGate)
    public bool gateMissing;
    public Vector2 sawmill; public float sawmillRadius; public bool sawmillFixed;
    public Vector2 findSpot; public bool findFixed;
    public Vector2 cedar, birch, autumn;    // центры подзон (ForestZoneLayout)
}

public sealed class ForestTrailPath
{
    public string name;
    public bool main, glade;                // glade: путь заканчивается круглой поляной
    public readonly List<Vector2> p = new List<Vector2>();   // x, z в мире
    public readonly List<float> half = new List<float>();    // полуширина в точке, м
    public float len;
    public Vector2 end; public float endRadius;              // центр и радиус поляны (если glade)
}

public sealed class ForestTrailPlan
{
    public readonly List<ForestTrailPath> paths = new List<ForestTrailPath>();
    public readonly List<string> warnings = new List<string>();
    public Vector2 center, gate, sawmill, findSpot;
    public float radius, sawmillRadius, findRadius;
    public bool sawmillExisting, findExisting, hasFind;
}

// ------------------------------------------------------------ сбор данных и запуск раскладки

public static class ForestTrailPlanner
{
    public const string FindSpotName = "ForestFindSpot";
    public const string SawmillName = "ForestSawmill";

    static Vector2 XZ(Vector3 v) { return new Vector2(v.x, v.z); }

    // null = всё хорошо, иначе текст ошибки
    public static string Collect(ForestSettings fs, out ForestTrailInput inp)
    {
        inp = null;
        var master = ForestZones.Find(ForestBiome.Forest);
        if (master == null) return "Нет зоны леса: нажми «1. Создать зону леса».";
        MapZone mz = master.Map;
        Vector3 c3 = master.transform.position;
        float y0 = c3.y;

        var r = new ForestTrailInput();
        r.center = XZ(c3);
        r.radius = mz.shape == MapZone.Shape.Circle ? mz.radius : Mathf.Min(mz.size.x, mz.size.y) * 0.5f;
        r.depth = p => master.Depth(new Vector3(p.x, y0, p.y));

        Vector3 entry = ForestZoneLayout.EntryDir(c3);
        var gate = GameObject.Find("ForestGate");
        if (gate != null) r.gate = XZ(gate.transform.position);
        else { r.gate = r.center + XZ(entry) * Mathf.Max(1f, r.radius - 0.5f); r.gateMissing = true; }

        var slots = ForestZoneLayout.Compute(mz, entry);
        r.cedar = XZ(slots[0].pos); r.birch = XZ(slots[1].pos); r.autumn = XZ(slots[2].pos);

        // лесопилка: объект ForestSawmill главнее настроек, настройки главнее автоматического места
        var saw = GameObject.Find(SawmillName);
        if (saw != null)
        {
            r.sawmill = XZ(saw.transform.position);
            float rr = Mathf.Max(saw.transform.lossyScale.x, saw.transform.lossyScale.z) * 0.5f;
            r.sawmillRadius = rr >= 1f ? rr : fs.sawmillRadius;
            r.sawmillFixed = true;
        }
        else if (fs.sawmillEnabled && r.depth(fs.sawmillCenter) > fs.sawmillRadius * 0.5f)
        {
            r.sawmill = fs.sawmillCenter; r.sawmillRadius = fs.sawmillRadius; r.sawmillFixed = true;
        }

        var find = GameObject.Find(FindSpotName);
        if (find != null && r.depth(XZ(find.transform.position)) > 2f)
        {
            r.findSpot = XZ(find.transform.position); r.findFixed = true;
        }

        inp = r;
        return null;
    }

    public static ForestTrailPlan Plan(ForestSettings fs, out string error)
    {
        ForestTrailInput inp;
        error = Collect(fs, out inp);
        if (error != null) return null;
        var plan = LocationBuilder.PlanForestTrails(fs, inp);
        if (inp.gateMissing) plan.warnings.Add("Маркера ForestGate нет: арка взята на краю зоны со стороны центра острова.");
        return plan;
    }
}

// ------------------------------------------------------------ предпросмотр плана

public static class ForestTrailPreview
{
    static ForestTrailPlan plan;
    static float baseY;
    static bool hooked;

    [MenuItem("Tools/Forest/Тропы: план (предпросмотр)")]
    static void Show()
    {
        var fs = ForestSettings.Load();
        string err;
        plan = ForestTrailPlanner.Plan(fs, out err);
        if (plan == null) { Debug.LogWarning("[ForestTrails] " + err); return; }
        var m = ForestZones.Find(ForestBiome.Forest);
        baseY = m != null ? m.transform.position.y : 0f;
        if (!hooked) { SceneView.duringSceneGui += Draw; hooked = true; }
        var sb = new System.Text.StringBuilder("[ForestTrails] План: ");
        for (int i = 0; i < plan.paths.Count; i++)
            sb.Append(plan.paths[i].name).Append(' ').Append(plan.paths[i].len.ToString("0.#")).Append(" м; ");
        Debug.Log(sb.ToString());
        for (int i = 0; i < plan.warnings.Count; i++) Debug.LogWarning("[ForestTrails] " + plan.warnings[i]);
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/Forest/Тропы: убрать предпросмотр")]
    static void Hide()
    {
        plan = null;
        if (hooked) { SceneView.duringSceneGui -= Draw; hooked = false; }
        SceneView.RepaintAll();
    }

    static Vector3 P3(Vector2 p)
    {
        float y = baseY;
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(p.x, baseY + 200f, p.y), Vector3.down, out hit, 500f)) y = hit.point.y;
        return new Vector3(p.x, y + 0.25f, p.y);
    }

    static void Draw(SceneView sv)
    {
        if (plan == null) return;
        for (int k = 0; k < plan.paths.Count; k++)
        {
            var t = plan.paths[k];
            int n = t.p.Count;
            if (n < 2) continue;
            var c = new Vector3[n]; var l = new Vector3[n]; var r = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 tan = (t.p[Mathf.Min(i + 1, n - 1)] - t.p[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 nr = new Vector2(-tan.y, tan.x);
                c[i] = P3(t.p[i]); l[i] = P3(t.p[i] + nr * t.half[i]); r[i] = P3(t.p[i] - nr * t.half[i]);
            }
            Handles.color = t.main ? new Color(1f, 0.85f, 0.2f) : new Color(0.4f, 0.9f, 1f);
            Handles.DrawAAPolyLine(2f, c);
            Handles.DrawAAPolyLine(4f, l);
            Handles.DrawAAPolyLine(4f, r);
            Handles.Label(c[n / 2] + Vector3.up * 0.6f, t.name + " " + t.len.ToString("0.#") + " м");
            if (t.glade) Handles.DrawWireDisc(P3(t.end), Vector3.up, t.endRadius);
        }
        Handles.color = Color.white;
        Handles.Label(P3(plan.sawmill) + Vector3.up * 1.2f, "Лесопилка R=" + plan.sawmillRadius.ToString("0.#") + (plan.sawmillExisting ? " (объект)" : ""));
        if (plan.hasFind) Handles.Label(P3(plan.findSpot) + Vector3.up * 1.2f, "Находка / NPC R=" + plan.findRadius.ToString("0.#") + (plan.findExisting ? " (объект)" : ""));
        Handles.Label(P3(plan.gate) + Vector3.up * 1.2f, "Арка");
    }
}
#endif
