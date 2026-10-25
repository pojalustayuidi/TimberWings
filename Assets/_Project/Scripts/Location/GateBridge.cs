using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Мост от ворот острова к острову-заглушке следующей локации.
//
// Как работает:
//   - стадии моста берутся из RiverBridge (1 сломан, 2 чинится, 3 готов), этот компонент только слушает его события;
//   - у каждой стадии свой объект (Stage1..Stage3), включается тот, что нужен. Коллайдер настила есть только у стадии 3;
//   - на стадии 1 с обрубка падают доски и исчезают в облаках;
//   - остров-заглушка с аркой и табличкой стоит всегда, невидимые стены не дают упасть с моста и с острова;
//   - всё строится кодом из редактора кнопкой «Собрать мост и заглушку». Положение берётся из GatePoint у FloatingIsland.
// Скрипт кладётся в Assets/_Project/Scripts/Location/ (НЕ в папку Editor).
public class GateBridge : MonoBehaviour
{
    [Header("Размеры (меняй и жми «Собрать мост и заглушку»)")]
    [Tooltip("Расстояние по воздуху от края главного острова до края острова-заглушки, метры")]
    public float bridgeLength = 25f;
    public float deckWidth = 2.6f;
    [Tooltip("Насколько мост провисает посередине, метры")]
    public float sag = 0.7f;
    [Tooltip("Радиус острова-заглушки, метры")]
    public float stubRadius = 9f;
    [Tooltip("На сколько метров верх острова-заглушки ниже края главного острова")]
    public float stubDrop = 0.6f;
    [Tooltip("Текст на табличке. \\n = перенос строки")]
    public string signText = "Следующая локация\\nскоро";
    [Tooltip("Размер букв на табличке. Если не влезает или мелко, подправь и пересобери")]
    public float signTextSize = 0.05f;
    public int seed = 5;

    [Header("Ссылки (проставляются при сборке)")]
    public RiverBridge source;
    public GameObject[] stages = new GameObject[3];
    public Mesh plankMesh;
    public Material plankMaterial;
    public Transform[] fallPoints = new Transform[0];

    [Header("Падающие доски (только стадия 1)")]
    public int fallingPlanks = 3;
    public Vector2 fallInterval = new Vector2(3f, 8f);
    public float fallDepth = 45f;

    class Faller
    {
        public Transform t;
        public float wait, vy, baseY;
        public Vector3 spin;
        public bool falling;
    }

    readonly List<Faller> fallers = new List<Faller>();
    int stage = 1;
    bool subscribed;

    void Awake()
    {
        if (plankMesh == null || plankMaterial == null) return;
        for (int i = 0; i < fallingPlanks; i++)
        {
            var go = new GameObject("FallingPlank");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = plankMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = plankMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            fallers.Add(new Faller { t = go.transform, wait = Random.Range(fallInterval.x, fallInterval.y) * (0.3f + i * 0.5f) });
        }
    }

    void OnEnable() { TrySubscribe(); }

    void OnDisable()
    {
        if (source != null && subscribed) source.OnStageChanged -= Apply;
        subscribed = false;
    }

    void Start()
    {
        TrySubscribe();
        Apply(source != null ? source.CurrentStage : 1);
    }

    void TrySubscribe()
    {
        if (subscribed) return;
        if (source == null) source = FindFirstObjectByType<RiverBridge>();
        if (source == null) return;
        source.OnStageChanged += Apply;
        subscribed = true;
    }

    // Показать нужную стадию (1..3). Работает и в редакторе (кнопки «Показать стадию»)
    public void Apply(int s)
    {
        stage = Mathf.Clamp(s, 1, 3);
        for (int i = 0; i < stages.Length; i++)
            if (stages[i] != null) stages[i].SetActive(i == stage - 1);
        if (stage != 1)
            foreach (var f in fallers) { f.t.gameObject.SetActive(false); f.falling = false; }
    }

    void Update()
    {
        if (stage != 1 || fallPoints == null || fallPoints.Length == 0) return;
        float dt = Time.deltaTime;
        foreach (var f in fallers)
        {
            if (!f.falling)
            {
                f.wait -= dt;
                if (f.wait > 0f) continue;
                var fp = fallPoints[Random.Range(0, fallPoints.Length)];
                if (fp == null) continue;
                f.t.position = fp.position + fp.right * Random.Range(-0.6f, 0.6f);
                f.t.rotation = fp.rotation;
                f.baseY = fp.position.y;
                f.vy = 0f;
                f.spin = new Vector3(Random.Range(-60f, 60f), Random.Range(-40f, 40f), Random.Range(-90f, 90f));
                f.falling = true;
                f.t.gameObject.SetActive(true);
                continue;
            }
            f.vy += 7f * dt;
            f.t.position += Vector3.down * (f.vy * dt);
            f.t.Rotate(f.spin * dt, Space.Self);
            if (f.t.position.y < f.baseY - fallDepth)
            {
                f.t.gameObject.SetActive(false);
                f.falling = false;
                f.wait = Random.Range(fallInterval.x, fallInterval.y);
            }
        }
    }
}

#if UNITY_EDITOR
// Накопитель плоско-затенённого меша из коробок, балок и граней (цвета в вершинах, шейдер Project/IslandRock)
sealed class GBMesh
{
    public readonly List<Vector3> V = new List<Vector3>();
    public readonly List<Color> C = new List<Color>();
    public readonly List<int> T = new List<int>();

    public void Tri(Vector3 a, Vector3 b, Vector3 d, Color col, Vector3 inside)
    {
        IslandUnderside.Face(V, C, T, a, b, d, col, col, col, inside);
    }

    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, Vector3 inside)
    {
        Tri(a, b, c, col, inside);
        Tri(a, c, d, col, inside);
    }

    // Коробка: центр, оси (right, up, fwd), половинные размеры
    public void Box(Vector3 c0, Vector3 r, Vector3 u, Vector3 f, float hx, float hy, float hz, Color col)
    {
        System.Func<int, int, int, Vector3> P = (sx, sy, sz) => c0 + r * (sx * hx) + u * (sy * hy) + f * (sz * hz);
        Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), col, c0);
        Quad(P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), col, c0);
        Quad(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), col, c0);
        Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), col, c0);
        Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), col, c0);
        Quad(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), col, c0);
    }

    // Балка квадратного сечения между двумя точками (верёвка)
    public void Beam(Vector3 a, Vector3 b, float hw, Color col)
    {
        Vector3 f = b - a;
        float len = f.magnitude;
        if (len < 1e-4f) return;
        f /= len;
        Vector3 r = Vector3.Cross(Vector3.up, f);
        if (r.sqrMagnitude < 1e-4f) r = Vector3.right;
        r.Normalize();
        Vector3 u = Vector3.Cross(f, r).normalized;
        Box((a + b) * 0.5f, r, u, f, hw, hw, len * 0.5f, col);
    }

    public void Cone(Vector3 bp, float radius, float h, Color col, int seg)
    {
        Vector3 tip = bp + Vector3.up * h, inside = bp + Vector3.up * (h * 0.4f);
        for (int j = 0; j < seg; j++)
        {
            float a0 = j * Mathf.PI * 2f / seg, a1 = (j + 1) * Mathf.PI * 2f / seg;
            Vector3 p0 = bp + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
            Vector3 p1 = bp + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
            Tri(p0, p1, tip, col, inside);
            Tri(p0, p1, bp, Sh(col, 0.8f), inside);
        }
    }

    public static Color Sh(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }

    public Mesh ToMesh(string name)
    {
        return IslandUnderside.ToMesh(name, V, C, T);
    }
}

public static class GateBridgeBuilder
{
    static readonly Color WoodL = new Color(0.66f, 0.44f, 0.24f);
    static readonly Color WoodD = new Color(0.40f, 0.26f, 0.14f);
    static readonly Color RopeC = new Color(0.78f, 0.66f, 0.42f);
    static readonly Color PostC = new Color(0.34f, 0.22f, 0.12f);
    static readonly Color StoneC = new Color(0.60f, 0.58f, 0.64f);
    static readonly Color BoardC = new Color(0.11f, 0.16f, 0.23f);   // как цвет интерфейса #1C2A3A
    static readonly Color Glow = new Color(0.55f, 0.95f, 1f, 0f);    // альфа 0 = свечение в шейдере IslandRock

    // Высота настоящей земли (Terrain или поле Apron), стены прохода и декор не считаются
    static float Ground(Vector2 p, float fallback)
    {
        Physics.SyncTransforms();
        var hits = Physics.RaycastAll(new Vector3(p.x, fallback + 80f, p.y), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        foreach (var h in hits)
            if (h.collider is TerrainCollider || h.collider.gameObject.name == "Apron")
                best = Mathf.Max(best, h.point.y);
        return float.IsNegativeInfinity(best) ? fallback : best;
    }

    static void Frame(System.Func<float, Vector3> P, float t, out Vector3 f, out Vector3 r, out Vector3 u)
    {
        float a = Mathf.Clamp01(t - 0.01f), b = Mathf.Clamp01(t + 0.01f);
        f = (P(b) - P(a)).normalized;
        r = Vector3.Cross(Vector3.up, f).normalized;
        u = Vector3.Cross(f, r).normalized;
    }

    static GameObject NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static void AddRenderer(GameObject go, Mesh mesh, Material mat, bool shadows)
    {
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void Wall(Transform parent, string name, Vector3 center, Vector3 forward, float length, float height, float thickness)
    {
        var go = NewChild(parent, name);
        go.transform.position = center;
        if (forward.sqrMagnitude > 1e-6f) go.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        var bc = go.AddComponent<BoxCollider>();
        bc.size = new Vector3(thickness, height, length);
    }

    // Меш одной стадии моста
    static void BuildStageMesh(GBMesh m, int stage, System.Func<float, Vector3> P, float Lb, float deckW, System.Random rnd)
    {
        float hw = deckW * 0.5f;
        int n = Mathf.Max(8, Mathf.RoundToInt(Lb / 0.62f));

        // доски
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) / n;
            bool has = stage == 3 || (stage == 2 ? (t < 0.42f || t > 0.80f) : (t < 0.15f || t > 0.90f));
            double skip = rnd.NextDouble(), tiltR = rnd.NextDouble(), yawR = rnd.NextDouble(), colR = rnd.NextDouble();   // всегда тянем: порядок не зависит от ветки
            if (stage == 1 && i > 0 && i < n - 1 && skip < 0.18) has = false;
            if (!has) continue;
            Vector3 f, r, u;
            Frame(P, t, out f, out r, out u);
            float tilt = 0f;
            if (stage == 1 && ((t > 0.09f && t < 0.15f) || (t > 0.90f && t < 0.96f)))
                tilt = (tiltR < 0.5 ? -1f : 1f) * (8f + (float)tiltR * 30f);
            Quaternion q = Quaternion.AngleAxis(tilt, f) * Quaternion.AngleAxis(((float)yawR - 0.5f) * 6f, u);
            f = q * f; r = q * r; u = q * u;
            Color col = Color.Lerp(WoodL, WoodD, (float)colR);
            col.a = 1f;
            m.Box(P(t) - u * 0.06f, r, u, f, hw * 0.97f, 0.06f, 0.26f, col);
        }

        // верёвки перил и подвеса
        int steps = Mathf.Max(4, Mathf.RoundToInt(Lb));
        float[] levels = { 1.05f, 0.45f };
        float[] thick = { 0.05f, 0.04f };
        for (int side = -1; side <= 1; side += 2)
        {
            for (int li = 0; li < levels.Length; li++)
            {
                Vector3 prev = Vector3.zero;
                bool havePrev = false, prevOk = false;
                for (int k = 0; k <= steps; k++)
                {
                    float t = k / (float)steps;
                    Vector3 f, r, u;
                    Frame(P, t, out f, out r, out u);
                    Vector3 pos = P(t) + r * (side * (hw - 0.06f)) + u * levels[li];
                    bool ok = stage >= 2 || t <= 0.15f || t >= 0.90f;
                    if (ok && havePrev && prevOk) m.Beam(prev, pos, thick[li], RopeC);
                    prev = pos; prevOk = ok; havePrev = true;
                }

                if (stage == 1)
                {
                    // оборванные верёвки свисают вниз у обеих кромок провала
                    for (int end = 0; end < 2; end++)
                    {
                        float tE = end == 0 ? 0.15f : 0.90f;
                        Vector3 f, r, u;
                        Frame(P, tE, out f, out r, out u);
                        Vector3 start = P(tE) + r * (side * (hw - 0.06f)) + u * levels[li];
                        Vector3 dir = end == 0 ? f : -f;
                        Vector3 a = start;
                        for (int kk = 1; kk <= 5; kk++)
                        {
                            Vector3 b = start + dir * (0.3f * kk) - Vector3.up * (0.1f * kk * kk);
                            m.Beam(a, b, thick[li], RopeC);
                            a = b;
                        }
                    }
                }
            }

            // подвесные верёвки под настилом
            if (stage >= 2)
            {
                Vector3 prev = Vector3.zero;
                for (int k = 0; k <= steps; k++)
                {
                    float t = k / (float)steps;
                    Vector3 f, r, u;
                    Frame(P, t, out f, out r, out u);
                    Vector3 pos = P(t) + r * (side * (hw - 0.25f)) - u * 0.30f;
                    if (k > 0) m.Beam(prev, pos, 0.05f, RopeC);
                    prev = pos;
                }
            }
        }

        // столбы перил и якорные столбы
        int posts = Mathf.Max(2, Mathf.RoundToInt(Lb / 4f));
        for (int p = 0; p <= posts; p++)
        {
            float t = p / (float)posts;
            bool ok = stage >= 2 || t <= 0.16f || t >= 0.89f;
            if (!ok) continue;
            Vector3 f, r, u;
            Frame(P, t, out f, out r, out u);
            for (int side = -1; side <= 1; side += 2)
                m.Box(P(t) + r * (side * (hw - 0.06f)) + u * 0.55f, r, u, f, 0.07f, 0.6f, 0.07f, PostC);
        }
        for (int e = 0; e < 2; e++)
        {
            Vector3 f, r, u;
            Frame(P, e, out f, out r, out u);
            for (int side = -1; side <= 1; side += 2)
                m.Box(P(e) + r * (side * (hw + 0.12f)) + u * 0.85f, r, u, f, 0.16f, 0.95f, 0.16f, PostC);
        }
    }

    // Коллайдер настила (только для стадии 3): замкнутая лента вдоль пути
    static Mesh BuildDeckCollider(System.Func<float, Vector3> P, float Lb, float deckW)
    {
        var m = new GBMesh();
        int K = Mathf.Max(6, Mathf.CeilToInt(Lb));
        float hw = deckW * 0.5f;
        var tl = new Vector3[K + 1]; var tr = new Vector3[K + 1]; var bl = new Vector3[K + 1]; var br = new Vector3[K + 1];
        for (int k = 0; k <= K; k++)
        {
            float t = k / (float)K;
            Vector3 f, r, u;
            Frame(P, t, out f, out r, out u);
            tl[k] = P(t) - r * hw; tr[k] = P(t) + r * hw;
            bl[k] = tl[k] - u * 0.3f; br[k] = tr[k] - u * 0.3f;
        }
        var col = Color.white;
        for (int k = 0; k < K; k++)
        {
            Vector3 inside = (tl[k] + tr[k] + bl[k + 1] + br[k + 1]) * 0.25f;
            m.Quad(tl[k], tr[k], tr[k + 1], tl[k + 1], col, inside);
            m.Quad(bl[k], br[k], br[k + 1], bl[k + 1], col, inside);
            m.Quad(tl[k], bl[k], bl[k + 1], tl[k + 1], col, inside);
            m.Quad(tr[k], br[k], br[k + 1], tr[k + 1], col, inside);
        }
        return m.ToMesh("GateBridgeDeckCollider");
    }

    // Остров-заглушка: плоский травяной верх, обрыв, скала книзу, пара ёлок. Локальные оси = мировые
    static Mesh BuildStub(FloatingIsland fi, float R, int seed, Vector2 toBridge)
    {
        var rnd = new System.Random(seed * 31 + 7);
        System.Func<float> R01 = () => (float)rnd.NextDouble();
        const int SEG = 16;
        float[] rad = { 1f, 1f, 0.95f, 0.66f, 0.36f, 0f };
        float[] yy = { 0f, -0.08f, -0.25f, -0.65f, -1.15f, -1.7f };
        Color[] rc = { fi.grassTop, fi.grassDark, fi.soil, fi.rockWarm, fi.rockMid, fi.rockDark };
        int Rn = rad.Length;
        var ring = new Vector3[Rn, SEG];
        var colr = new Color[Rn, SEG];
        for (int i = 0; i < Rn; i++)
            for (int j = 0; j < SEG; j++)
            {
                float a = j * Mathf.PI * 2f / SEG;
                float jr = (i <= 1 || i == Rn - 1) ? 1f : 0.85f + R01() * 0.3f;   // верхнее кольцо ровное: на него заходит мост
                float jy = (i >= 2 && i < Rn - 1) ? (R01() - 0.5f) * 0.04f * R : 0f;
                ring[i, j] = new Vector3(Mathf.Cos(a) * rad[i] * R * jr, yy[i] * R + jy, Mathf.Sin(a) * rad[i] * R * jr);
                colr[i, j] = GBMesh.Sh(rc[i], 0.92f + R01() * 0.16f);
            }

        var m = new GBMesh();
        Vector3 inside = new Vector3(0f, -0.8f * R, 0f);
        Vector3 topC = new Vector3(0f, 0.02f, 0f);
        Color gt = fi.grassTop; gt.a = 1f;
        for (int j = 0; j < SEG; j++)
        {
            int j2 = (j + 1) % SEG;
            m.Tri(topC, ring[0, j2], ring[0, j], gt, inside);
            for (int i = 0; i < Rn - 1; i++)
            {
                IslandUnderside.Face(m.V, m.C, m.T, ring[i, j], ring[i, j2], ring[i + 1, j2], colr[i, j], colr[i, j2], colr[i + 1, j2], inside);
                if (i < Rn - 2)
                    IslandUnderside.Face(m.V, m.C, m.T, ring[i, j], ring[i + 1, j2], ring[i + 1, j], colr[i, j], colr[i + 1, j2], colr[i + 1, j], inside);
            }
        }

        // ёлки подальше от прохода к мосту и от арки
        Color tree = new Color(0.18f, 0.42f, 0.24f);
        for (int k = 0; k < 6; k++)
        {
            float ang = R01() * Mathf.PI * 2f, dist = (0.3f + R01() * 0.4f) * R;
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            float toB = Vector2.Angle(dir, toBridge), toA = Vector2.Angle(dir, -toBridge);
            float h = 1.8f + R01() * 1.2f, rr = 0.5f + R01() * 0.3f;
            if (toB < 55f || (toA < 28f && dist > R * 0.4f)) continue;
            m.Cone(new Vector3(dir.x * dist, 0.02f, dir.y * dist), rr, h, tree, 6);
            m.Cone(new Vector3(dir.x * dist, 0.02f + h * 0.45f, dir.y * dist), rr * 0.75f, h * 0.75f, GBMesh.Sh(tree, 1.1f), 6);
        }
        return m.ToMesh("GateBridgeStub");
    }

    // Арка с табличкой и светящимся кристаллом. Локальная ось z = направление «вперёд по мосту»
    static Mesh BuildArch()
    {
        var m = new GBMesh();
        Vector3 r = Vector3.right, u = Vector3.up, f = Vector3.forward;
        m.Box(new Vector3(-2.4f, 1.9f, 0f), r, u, f, 0.3f, 1.9f, 0.3f, StoneC);
        m.Box(new Vector3(2.4f, 1.9f, 0f), r, u, f, 0.3f, 1.9f, 0.3f, StoneC);
        m.Box(new Vector3(0f, 3.95f, 0f), r, u, f, 3.0f, 0.25f, 0.35f, GBMesh.Sh(StoneC, 0.92f));
        m.Box(new Vector3(0f, 3.0f, -0.42f), r, u, f, 1.9f, 0.4f, 0.06f, BoardC);
        // кристалл-октаэдр над аркой
        Vector3 c = new Vector3(0f, 5.0f, 0f);
        float rr = 0.45f;
        Vector3 top = c + Vector3.up * rr * 1.5f, bot = c - Vector3.up * rr * 1.5f;
        Vector3[] eq = { c + Vector3.right * rr, c + Vector3.forward * rr, c - Vector3.right * rr, c - Vector3.forward * rr };
        for (int i = 0; i < 4; i++)
        {
            m.Tri(top, eq[i], eq[(i + 1) % 4], Glow, c);
            m.Tri(bot, eq[i], eq[(i + 1) % 4], Glow, c);
        }
        return m.ToMesh("GateBridgeArch");
    }

    public static void Build(GateBridge gb)
    {
        var fi = Object.FindFirstObjectByType<FloatingIsland>();
        if (fi == null) { Debug.LogWarning("GateBridge: на сцене нет FloatingIsland."); return; }
        Vector2? gate = fi.GetGatePosition();
        if (!gate.HasValue) { Debug.LogWarning("GateBridge: у FloatingIsland нет GatePoint (проход). Поставь точку ворот и повтори."); return; }
        var loop = fi.GetEdgeLoop();
        int m = loop.Count;
        if (m < 8) { Debug.LogWarning("GateBridge: контур острова слишком короткий."); return; }

        // край острова около ворот и направление наружу
        int bi = 0;
        float bd = float.MaxValue;
        for (int i = 0; i < m; i++)
        {
            float d = (loop[i] - gate.Value).sqrMagnitude;
            if (d < bd) { bd = d; bi = i; }
        }
        Vector2 E = loop[bi];
        Vector2 dir = (loop[(bi + 3) % m] - loop[(bi - 3 + m) % m]).normalized;
        Vector2 N = new Vector2(dir.y, -dir.x);   // контур против часовой, наружу = вправо от хода
        Vector2 Tn = dir;

        // материал
        Material mat = fi.rockMaterial;
        if (mat == null)
        {
            var sh = Shader.Find("Project/IslandRock");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Simple Lit");
            mat = new Material(sh) { name = "IslandRock" };
            fi.rockMaterial = mat;
            EditorUtility.SetDirty(fi);
        }
        if (mat.HasProperty("_GlowIntensity") && mat.GetFloat("_GlowIntensity") < 0.01f) mat.SetFloat("_GlowIntensity", 2.5f);

        // чистим прошлую сборку
        for (int i = gb.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(gb.transform.GetChild(i).gameObject);
        gb.transform.localPosition = Vector3.zero;
        gb.transform.localRotation = Quaternion.identity;
        gb.transform.localScale = Vector3.one;

        // геометрия пути
        float y0 = Ground(E - N * 1.0f, fi.GroundY);
        float yStart = y0 + 0.05f;
        float yStubTop = y0 - gb.stubDrop;
        float yEnd = yStubTop + 0.05f;
        float R = Mathf.Max(4f, gb.stubRadius);
        Vector2 S2 = E - N * 0.8f;
        float Lb = gb.bridgeLength + 1.5f + 0.8f;
        Vector2 Q2 = S2 + N * Lb;
        Vector2 C2 = E + N * (gb.bridgeLength + R);
        float sagV = gb.sag;
        float yS = yStart, yQ = yEnd;
        System.Func<float, Vector3> P = t =>
        {
            Vector2 q = S2 + N * (Lb * t);
            float y = Mathf.Lerp(yS, yQ, t) - sagV * 4f * t * (1f - t);
            return new Vector3(q.x, y, q.y);
        };
        Vector3 N3 = new Vector3(N.x, 0f, N.y), T3 = new Vector3(Tn.x, 0f, Tn.y);

        // стадии
        var rootT = gb.transform;
        for (int s = 1; s <= 3; s++)
        {
            var go = NewChild(rootT, "Stage" + s);
            var gm = new GBMesh();
            BuildStageMesh(gm, s, P, Lb, gb.deckWidth, new System.Random(gb.seed + s * 17));
            AddRenderer(go, gm.ToMesh("GateBridgeStage" + s), mat, true);
            if (s == 3)
            {
                var deck = NewChild(go.transform, "Deck");
                deck.AddComponent<MeshCollider>().sharedMesh = BuildDeckCollider(P, Lb, gb.deckWidth);
            }
            gb.stages[s - 1] = go;
        }

        // точки, откуда падают доски (кромки провала на стадии 1)
        var fall = new List<Transform>();
        float[] tf = { 0.15f, 0.90f };
        for (int i = 0; i < tf.Length; i++)
        {
            Vector3 f, r, u;
            Frame(P, tf[i], out f, out r, out u);
            var fp = NewChild(rootT, "FallPoint" + i);
            fp.transform.position = P(tf[i]);
            fp.transform.rotation = Quaternion.LookRotation(f, Vector3.up);
            fall.Add(fp.transform);
        }
        gb.fallPoints = fall.ToArray();

        // доска для падения
        var plank = new GBMesh();
        plank.Box(Vector3.zero, Vector3.right, Vector3.up, Vector3.forward, gb.deckWidth * 0.46f, 0.06f, 0.26f, Color.Lerp(WoodL, WoodD, 0.4f));
        gb.plankMesh = plank.ToMesh("GateBridgePlank");
        gb.plankMaterial = mat;

        // остров-заглушка
        var stub = NewChild(rootT, "StubIsland");
        stub.transform.position = new Vector3(C2.x, yStubTop, C2.y);
        Mesh stubMesh = BuildStub(fi, R, gb.seed, -N);
        AddRenderer(stub, stubMesh, mat, true);
        stub.AddComponent<MeshCollider>().sharedMesh = stubMesh;

        // арка с табличкой
        var arch = NewChild(rootT, "Arch");
        Vector2 A2 = C2 + N * (R - 3.8f);
        arch.transform.position = new Vector3(A2.x, yStubTop, A2.y);
        arch.transform.rotation = Quaternion.LookRotation(N3, Vector3.up);
        AddRenderer(arch, BuildArch(), mat, true);

        var tgo = NewChild(arch.transform, "SignText");
        tgo.transform.localPosition = new Vector3(0f, 3.0f, -0.50f);
        var tm = tgo.AddComponent<TextMesh>();
        tm.text = (gb.signText ?? "").Replace("\\n", "\n");
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 64;
        tm.characterSize = gb.signTextSize;
        tm.color = new Color(1f, 0.93f, 0.75f);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 64);
        if (font != null)
        {
            tm.font = font;
            tgo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        // невидимые стены
        var guards = NewChild(rootT, "Guards");
        Vector2 endWall2 = C2 - N * (R - 4f);   // боковые стены идут чуть дальше края заглушки: кольцо стен на острове попадает в коридор
        float Lw = (endWall2 - S2).magnitude;
        Vector2 mid2 = (S2 + endWall2) * 0.5f;
        float yMid = (yStart + yEnd) * 0.5f;
        float off = gb.deckWidth * 0.5f + 0.25f;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 c = new Vector3(mid2.x, yMid + 1.5f, mid2.y) + T3 * (side * off);
            Wall(guards.transform, "BridgeSide" + (side < 0 ? "L" : "R"), c, N3, Lw, 5f, 0.4f);
        }

        // заглушки по краю главного острова слева и справа от коридора (проход шире моста)
        float cw = gb.deckWidth * 0.5f + 0.45f;
        float capEnd = fi.gateWidth * 0.5f + 3f;
        if (capEnd > cw + 0.5f)
        {
            float capLen = capEnd - cw, capMid = (capEnd + cw) * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 c2 = E - N * fi.edgeInset + Tn * (side * capMid);
                Wall(guards.transform, "GateCap" + (side < 0 ? "L" : "R"), new Vector3(c2.x, y0 + fi.wallHeight * 0.5f, c2.y), T3, capLen, fi.wallHeight, fi.wallThickness);
            }
        }

        // кольцо стен по краю острова-заглушки, с проходом к мосту
        const int WS = 16;
        float a0 = Mathf.Atan2(-N.y, -N.x);
        float ringR = R * 0.9f;
        float segLen = 2f * Mathf.PI * ringR / WS * 1.15f;
        for (int k = 1; k < WS; k++)
        {
            float a = a0 + k * Mathf.PI * 2f / WS;
            Vector2 cp = C2 + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ringR;
            Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            Wall(guards.transform, "StubRim" + k, new Vector3(cp.x, yStubTop + 3f, cp.y), tangent, segLen, 6f, 0.5f);
        }

        // стена за аркой: игрок доходит до арки и дальше не идёт
        Vector2 W2 = A2 + N * 0.5f;
        Wall(guards.transform, "ArchWall", new Vector3(W2.x, yStubTop + 3f, W2.y), T3, R * 2f, 6f, 0.8f);

        gb.Apply(1);
        EditorUtility.SetDirty(gb);
        EditorSceneManager.MarkSceneDirty(gb.gameObject.scene);
        Debug.Log("GateBridge: мост и остров-заглушка собраны. Мост " + (gb.bridgeLength) + " м, радиус заглушки " + R + " м. Показана стадия 1; другие смотри кнопками «Показать стадию».");
    }

    public static void Clear(GateBridge gb)
    {
        for (int i = gb.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(gb.transform.GetChild(i).gameObject);
        gb.stages = new GameObject[3];
        gb.fallPoints = new Transform[0];
        EditorSceneManager.MarkSceneDirty(gb.gameObject.scene);
    }

    [MenuItem("Tools/BuilderSim/Локация/Build Gate Bridge", false, 103)]
    static void CreateFromMenu()
    {
        var island = Object.FindFirstObjectByType<FloatingIsland>();
        if (island == null) { Debug.LogWarning("GateBridge: на сцене нет FloatingIsland."); return; }
        var existing = island.transform.Find("GateBridge");
        GateBridge gb = existing != null ? existing.GetComponent<GateBridge>() : null;
        if (gb == null)
        {
            var go = existing != null ? existing.gameObject : new GameObject("GateBridge");
            if (existing == null) Undo.RegisterCreatedObjectUndo(go, "Create GateBridge");
            go.transform.SetParent(island.transform, false);
            gb = go.GetComponent<GateBridge>();
            if (gb == null) gb = go.AddComponent<GateBridge>();
            if (gb == null)
            {
                // Unity не даёт повесить на объект скрипт, который лежит в папке Editor
                Debug.LogError("GateBridge: компонент не добавился. Скорее всего, GateBridge.cs лежит в папке Editor. Перенеси его в Assets/_Project/Scripts/Location/, удали пустой объект GateBridge под FloatingIsland и повтори.");
                return;
            }
        }
        Build(gb);
        Selection.activeGameObject = gb.gameObject;
    }
}

[CustomEditor(typeof(GateBridge))]
public class GateBridgeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var gb = (GateBridge)target;
        EditorGUILayout.Space();
        if (GUILayout.Button("Собрать мост и заглушку", GUILayout.Height(30))) GateBridgeBuilder.Build(gb);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Показать стадию 1")) gb.Apply(1);
        if (GUILayout.Button("Показать стадию 2")) gb.Apply(2);
        if (GUILayout.Button("Показать стадию 3")) gb.Apply(3);
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Удалить мост и заглушку")) GateBridgeBuilder.Clear(gb);
    }
}
#endif
