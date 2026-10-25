// Assets/_Project/Scripts/Location/IslandLook.Haze.cs (НЕ в Editor)
// Остров v2, этап 3: дымка. Часть partial-класса IslandLook. Остальные части: IslandLook.cs (создание, жизнь, качество).
//
// Что рисует: мягкие круглые карточки (шейдер Project/IslandV2/Haze), повёрнутые к камере. Раскладка:
//   • «кольцо» по краю острова: карточки равномерно по контуру FloatingIsland, сдвинутые на edgeInset метров внутрь (дымка у обрыва, как на референсе);
//   • «низины»: доля innerShare карточек случайно внутри контура (у воды и в лощинах).
// Они медленно дрейфуют на месте (driftRadius, driftSpeed). Вблизи камеры карточки растворяются (nearFadeStart/End): игрок и тропа чистые,
// в изометрической камере дымка живёт у верхнего края кадра и на дальних краях, в камере сверху видна вся.
//
// Методы: HazeAwake, HazeDestroy (жизнь), RebuildHaze (раскладка под профиль бюджета), DrawHaze (кадр), BuildQuad, Inside, HazeColor.
// Бюджет: один instanced draw call на все карточки, 2 треугольника на карточку. Число карточек берётся из профиля
// (CloudCandyStyle.budgetMobileLow / budgetMobileHigh / budgetPc.hazeCards). Все числа из CloudCandyStyle.haze, в коде их нет.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class IslandLook
{
    const int MaxCards = 64;   // жёсткий потолок кода; реальное число задаёт профиль бюджета (hazeCards ≤ 64 в инспекторе)

    static readonly int HazeTintId = Shader.PropertyToID("_Tint");
    static readonly int HazeFadeId = Shader.PropertyToID("_Fade");

    Mesh hazeQuad;
    Material hazeMat;

    int hazeCount;
    int hazeBuiltRev = -1;
    Vector3[] hazeBase = new Vector3[MaxCards];
    float[] hazeSize = new float[MaxCards];
    float[] hazePhase = new float[MaxCards];
    float[] hazeSpeed = new float[MaxCards];
    Matrix4x4[] hazeMatrices = new Matrix4x4[MaxCards];

    // сколько карточек сейчас рисуется (для проверки бюджета в Editor)
    public int HazeCardCount { get { return hazeDebugEnabled && hazeMat != null ? hazeCount : 0; } }

    void HazeAwake()
    {
        var sh = Shader.Find("Project/IslandV2/Haze");
        if (sh == null)
        {
            Debug.LogWarning("[IslandLook] Шейдер Project/IslandV2/Haze не найден (файл Shaders/IslandV2/IslandHaze.shader). Дымка выключена.");
            return;
        }
        hazeMat = new Material(sh) { name = "IslandHaze (runtime)", hideFlags = HideFlags.DontSave, enableInstancing = true };
        hazeQuad = BuildQuad();
    }

    void HazeDestroy()
    {
        if (hazeMat != null) Destroy(hazeMat);
        if (hazeQuad != null) Destroy(hazeQuad);
    }

    // единичный квадрат лицом к +Z, uv 0..1. Границы кубом: карточка поворачивается к камере в шейдере, плоские границы тут не годятся
    static Mesh BuildQuad()
    {
        var m = new Mesh { name = "IslandHazeQuad" };
        m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) };
        m.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        m.bounds = new Bounds(Vector3.zero, Vector3.one);
        return m;
    }

    // ---------------------------------------------------------------- раскладка
    void RebuildHaze(CloudCandyStyle.IslandBudget budget, CloudCandyStyle.HazeSettings cfg)
    {
        hazeBuiltRev = styleRev;
        hazeCount = 0;
        if (hazeMat == null || !cfg.enabled) return;

        IslandSource island = IslandSource.Find();
        if (island == null) return;
        List<Vector2> outline = island.Outline;
        if (outline == null || outline.Count < 3) return;

        int total = Mathf.Clamp(budget.hazeCards, 0, MaxCards);
        if (total == 0) return;
        int inner = Mathf.RoundToInt(total * Mathf.Clamp01(cfg.innerShare));
        int ring = total - inner;

        // центр и периметр контура
        Vector2 c = Vector2.zero;
        for (int i = 0; i < outline.Count; i++) c += outline[i];
        c /= outline.Count;
        float perimeter = 0f;
        for (int i = 0; i < outline.Count; i++) perimeter += Vector2.Distance(outline[i], outline[(i + 1) % outline.Count]);
        if (perimeter < 1f) return;

        var rnd = new System.Random(cfg.seed * 31 + 7);
        float groundY = island.GroundY;
        int n = 0;

        // кольцо по краю: равные шаги по длине контура с небольшим разбросом
        for (int k = 0; k < ring && n < MaxCards; k++)
        {
            float s = (k + 0.25f + (float)rnd.NextDouble() * 0.5f) / ring * perimeter;
            Vector2 p = PointAlong(outline, s);
            Vector2 inward = (c - p).sqrMagnitude > 0.01f ? (c - p).normalized : Vector2.zero;
            Vector2 q = p + inward * cfg.edgeInset;
            AddCard(n++, q, groundY, cfg, rnd);
        }

        // низины: случайные точки внутри контура (ограниченное число попыток, чтобы не зависнуть на странной форме)
        Vector2 mn = outline[0], mx = outline[0];
        for (int i = 1; i < outline.Count; i++) { mn = Vector2.Min(mn, outline[i]); mx = Vector2.Max(mx, outline[i]); }
        for (int k = 0; k < inner && n < MaxCards; k++)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                var q = new Vector2(Mathf.Lerp(mn.x, mx.x, (float)rnd.NextDouble()), Mathf.Lerp(mn.y, mx.y, (float)rnd.NextDouble()));
                if (!Inside(outline, q)) continue;
                AddCard(n++, q, groundY, cfg, rnd);
                break;
            }
        }
        hazeCount = n;
    }

    void AddCard(int i, Vector2 xz, float groundY, CloudCandyStyle.HazeSettings cfg, System.Random rnd)
    {
        float h = Mathf.Lerp(cfg.height.x, cfg.height.y, (float)rnd.NextDouble());
        hazeBase[i] = new Vector3(xz.x, groundY + h, xz.y);
        hazeSize[i] = Mathf.Lerp(cfg.size.x, cfg.size.y, (float)rnd.NextDouble());
        hazePhase[i] = (float)rnd.NextDouble() * Mathf.PI * 2f;
        hazeSpeed[i] = 0.6f + (float)rnd.NextDouble() * 0.8f;
    }

    // точка на контуре на расстоянии s от начала (по длине)
    static Vector2 PointAlong(List<Vector2> poly, float s)
    {
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = poly[i], b = poly[(i + 1) % n];
            float len = Vector2.Distance(a, b);
            if (s <= len || i == n - 1) return Vector2.Lerp(a, b, len > 1e-4f ? Mathf.Clamp01(s / len) : 0f);
            s -= len;
        }
        return poly[0];
    }

    static bool Inside(List<Vector2> poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            Vector2 a = poly[i], b = poly[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }

    // ---------------------------------------------------------------- кадр
    // цвет дымки: цвет тумана сцены (его ведёт DayNightCycle: голубой днём, розовый на закате, тёмно-синий ночью), чуть светлее и с каплей цвета солнца
    Color HazeColor(CloudCandyStyle.HazeSettings cfg)
    {
        Color c = Color.Lerp(RenderSettings.fogColor, Color.white, cfg.whiten);
        Light sun = RenderSettings.sun;
        if (sun != null && sun.enabled) c = Color.Lerp(c, sun.color, cfg.sunTint * Mathf.Clamp01(sun.intensity));
        float night = DayNightCycle.Night01;
        c.a = cfg.alpha * Mathf.Lerp(1f, cfg.nightAlphaMul, night);
        return c;
    }

    void DrawHaze(CloudCandyStyle style, Camera camera)
    {
        var cfg = style.haze;
        if (!hazeDebugEnabled || !cfg.enabled || hazeCount == 0 || hazeMat == null || hazeQuad == null) return;

        hazeMat.SetColor(HazeTintId, HazeColor(cfg));
        hazeMat.SetVector(HazeFadeId, new Vector4(cfg.nearFadeStart, cfg.nearFadeEnd, cfg.farFadeStart, cfg.farFadeEnd));

        float t = Time.time;
        float rad = Mathf.Max(0.01f, cfg.driftRadius);
        for (int i = 0; i < hazeCount; i++)
        {
            float a = t * cfg.driftSpeed * hazeSpeed[i] / rad + hazePhase[i];
            Vector3 off = new Vector3(Mathf.Cos(a), 0.25f * Mathf.Sin(a * 0.7f), Mathf.Sin(a * 1.3f)) * cfg.driftRadius;
            float s = hazeSize[i];
            hazeMatrices[i] = Matrix4x4.TRS(hazeBase[i] + off, Quaternion.identity, new Vector3(s, s, s));
        }

        Camera target = cfg.onlyMainCamera ? camera : null;
        if (SystemInfo.supportsInstancing)
        {
            Graphics.DrawMeshInstanced(hazeQuad, 0, hazeMat, hazeMatrices, hazeCount, null, ShadowCastingMode.Off, false, 0, target, LightProbeUsage.Off);
        }
        else
        {
            for (int i = 0; i < hazeCount; i++)   // запасной путь без инстансинга: по draw call на карточку (на таких устройствах лучше снизить hazeCards)
                Graphics.DrawMesh(hazeQuad, hazeMatrices[i], hazeMat, 0, target, 0, null, ShadowCastingMode.Off, false);
        }
    }
}
