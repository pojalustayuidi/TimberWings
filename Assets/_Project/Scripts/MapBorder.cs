using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Граница карты из камней. Две формы:
//  • прямоугольник (Half Size / Center);
//  • своя линия: включи Use Custom Path, жми «Добавить точку» и двигай точки в сцене.
// Камни ставятся вдоль линии, невидимая стена не пускает игрока наружу.
// Мост построен: MapBorder.Instance.OpenGate().
public class MapBorder : MonoBehaviour
{
    public enum Side { North = 0, East = 1, South = 2, West = 3, None = 4 }

    // Rocks = старая граница из камней; FloatingIsland = без камней, край острова рисует FloatingIsland (обрыв и скала снизу)
    public enum Style { Rocks = 0, FloatingIsland = 1 }

    public static MapBorder Instance;

    // Проход к мосту открыт (плавно или сразу при загрузке). Слушает HudMinimap: перепечатывает карту
    public static event System.Action GateOpened;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { GateOpened = null; }

    [Header("Камни: префабы из папки Project")]
    public GameObject[] rockPrefabs;

    [Header("Стиль границы")]
    public Style style = Style.Rocks;   // по умолчанию старое поведение: сцены без этого поля не меняются, пока не нажмёшь Convert
    [Tooltip("Для парящего острова: на сколько метров невидимая стена стоит внутри от линии края")]
    public float edgeInset = 0.4f;
    [Tooltip("Парящий остров: с края можно упасть. Невидимых стен нет (кроме закрытого прохода к мосту), игрок падает в облака и красиво возвращается на остров (IslandEdgeGuard). Выключи, чтобы вернуть невидимые стены")]
    public bool allowFall = true;

    [Header("Форма")]
    public bool useCustomPath = false;          // true = граница по точкам из PathPoints
    public Vector2 center = Vector2.zero;        // для прямоугольника: x, z центра
    public Vector2 halfSize = new Vector2(30f, 30f);
    public float groundY = 0f;

    [Header("Камни")]
    public int rows = 2;
    public float spacing = 2.5f;
    public float rowSpacing = 2.5f;
    public Vector2 scaleRange = new Vector2(0.9f, 1.4f);
    public float outerBoost = 0.15f;
    public float jitter = 0.7f;
    public float waviness = 2f;
    public int seed = 7;
    public bool removeRockColliders = true;

    [Header("Проход к мосту (закрыт камнями до постройки моста)")]
    public Side gateSide = Side.North;           // для прямоугольника
    public float gateOffset = 0f;                // сдвиг вдоль стороны
    public float gateWidth = 8f;                 // для своей линии проход ставится в точке GatePoint

    [Header("Земля, фон, стена")]
    public float groundMargin = 14f;
    public float wallHeight = 6f;
    public float wallThickness = 1.5f;

    Transform gateRoot;

    void Awake()
    {
        Instance = this;
        gateRoot = transform.Find("GateRocks");

        // парящий остров: прямоугольные стены генератора локации (Boundary/Wall_N,S,E,W) стоят по краю Terrain, а не по краю острова.
        // Они и есть те самые «стенки», не пускающие за границу, и от Allow Fall не зависят, поэтому выключаем всегда.
        if (IsFloating) { DisableLegacyBoundary(); DisableHiddenGroundCollider(); }

        // можно упасть: старые невидимые стены по краю выключаем сразу, пересборка острова не нужна.
        // Проход к мосту (GateRocks) остаётся закрытым до постройки моста.
        if (IsFloating && allowFall)
        {
            var walls = transform.Find("Walls");
            if (walls != null) walls.gameObject.SetActive(false);
            if (GetComponent<IslandEdgeGuard>() == null) gameObject.AddComponent<IslandEdgeGuard>();
        }
    }

    // Скрытая плоскость Ground_Auto: рендер выключен, а коллайдер остаётся и торчит за краем острова (она прямоугольная, остров нет).
    // По нему можно ходить по воздуху за обрывом. Выключаем, если землёй служит Terrain или меш острова (Island/Top).
    public void DisableHiddenGroundCollider()
    {
        var ga = GameObject.Find("Ground_Auto");
        if (ga == null) return;
        var r = ga.GetComponent<Renderer>();
        if (r != null && r.enabled) return;   // плоскость ещё рисуется: это видимая земля, не трогаем
        bool otherGround = FindFirstObjectByType<Terrain>() != null;
        if (!otherGround)
        {
            var top = transform.Find(FloatingIsland.RootName + "/Top");
            otherGround = top != null && top.GetComponent<Collider>() != null;
        }
        if (!otherGround) return;
        foreach (var c in ga.GetComponents<Collider>())
            if (c.enabled) { c.enabled = false; Debug.Log("MapBorder: выключил коллайдер скрытой земли Ground_Auto (по нему ходили за краем острова)", ga); }
    }

    // Выключает невидимые стены-прямоугольник от LocationBuilder: объект «Boundary» и одиночные Wall_N/Wall_S/Wall_E/Wall_W.
    // У парящего острова границу задаёт сам MapBorder (Walls у края, если Allow Fall выключен), так что старые стены лишние.
    // Стены у прохода к мосту (GateRocks), стены моста (GateBridge) и WaterBarrier над рекой не трогаем.
    public int DisableLegacyBoundary()
    {
        int off = 0;
        var all = FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var bc in all)
        {
            if (bc == null || bc.isTrigger) continue;
            var t = bc.transform;
            if (t.IsChildOf(transform)) continue;   // свои стены MapBorder не трогаем
            bool legacy = t.parent != null && t.parent.name == "Boundary";
            if (!legacy)
            {
                string n = t.name;
                legacy = n == "Wall_N" || n == "Wall_S" || n == "Wall_E" || n == "Wall_W";
            }
            if (!legacy) continue;
            var target = (t.parent != null && t.parent.name == "Boundary") ? t.parent.gameObject : t.gameObject;
            if (!target.activeSelf) continue;
            target.SetActive(false);
            off++;
            Debug.Log("MapBorder: выключил старую невидимую стену «" + target.name + "» (она не давала выйти за границу)", target);
        }
        return off;
    }

    void OnEnable() { GameEvents.BuildingRestored += OnBuildingRestored; }
    void OnDisable() { GameEvents.BuildingRestored -= OnBuildingRestored; }

    // Загрузка сохранения: мост уже достроен, ворота открываем сразу, без опускания камней
    void OnBuildingRestored(string id, int stage)
    {
        var b = BuildingController.FindById(id);
        if (b != null && b.SiteKind == BuildSiteKind.Bridge && b.IsFinished) OpenGateInstant();
    }

    public void OpenGateInstant()
    {
        if (gateOpened) return;
        if (gateRoot == null) gateRoot = transform.Find("GateRocks");
        if (gateRoot == null) return;
        gateOpened = true;
        gateRoot.gameObject.SetActive(false);
        if (GateOpened != null) GateOpened();
    }

    // Мост построен: камни прохода плавно уходят в землю
    bool gateOpened;

    public void OpenGate()
    {
        if (gateOpened) return;   // повторный вызов (два события стройки подряд) не запускает опускание второй раз
        if (gateRoot == null) gateRoot = transform.Find("GateRocks");
        if (gateRoot == null) return;
        gateOpened = true;
        StartCoroutine(Sink(gateRoot));
        if (GateOpened != null) GateOpened();   // карта подождёт rebakeDelay: камни к этому времени уже опустятся
    }

    IEnumerator Sink(Transform t)
    {
        Vector3 from = t.position;
        const float dur = 1.6f;
        for (float k = 0f; k < dur; k += Time.deltaTime)
        {
            float e = k / dur; e *= e;
            t.position = from + Vector3.down * 6f * e;
            yield return null;
        }
        t.gameObject.SetActive(false);
    }

    // ---------- публичное для других систем (FloatingIsland, миникарта) ----------
    public bool IsFloating { get { return style == Style.FloatingIsland; } }

    // Контур острова (x, z) по кругу, как у стены и камней
    public List<Vector2> GetOutline() { return GetPoints(); }

    // Центр прохода к мосту (x, z) или null, если прохода нет
    public Vector2? GetGatePosition() { return GateWorld(GetPoints()); }

    // На сколько метров наружу от линии что-то выступает (камни у старого стиля, небольшой запас у парящего)
    public float OuterMargin
    {
        get { return style == Style.Rocks ? rows * rowSpacing * 0.5f + scaleRange.y : 1f; }
    }

    // ---------- форма ----------
    bool IsCustom()
    {
        var pr = transform.Find("PathPoints");
        return useCustomPath && pr != null && pr.childCount >= 3;
    }

    // Точки по кругу против часовой стрелки (x, z)
    List<Vector2> GetPoints()
    {
        var list = new List<Vector2>();
        if (IsCustom())
        {
            var pr = transform.Find("PathPoints");
            for (int i = 0; i < pr.childCount; i++)
            {
                var p = pr.GetChild(i).position;
                list.Add(new Vector2(p.x, p.z));
            }
        }
        else
        {
            Vector2 c = center, h = halfSize;
            list.Add(new Vector2(c.x - h.x, c.y - h.y)); // юго-запад
            list.Add(new Vector2(c.x + h.x, c.y - h.y)); // юго-восток
            list.Add(new Vector2(c.x + h.x, c.y + h.y)); // северо-восток
            list.Add(new Vector2(c.x - h.x, c.y + h.y)); // северо-запад
        }
        return list;
    }

    Vector2? GateWorld(List<Vector2> pts)
    {
        if (IsCustom())
        {
            var g = transform.Find("GatePoint");
            return g != null ? new Vector2(g.position.x, g.position.z) : (Vector2?)null;
        }
        if (gateSide == Side.None) return null;

        // рёбра прямоугольника: 0 юг, 1 восток, 2 север, 3 запад
        int edge = gateSide == Side.South ? 0 : gateSide == Side.East ? 1 : gateSide == Side.North ? 2 : 3;
        Vector2 a = pts[edge], b = pts[(edge + 1) % 4];
        return (a + b) * 0.5f + (b - a).normalized * gateOffset;
    }

    // Идёт по замкнутой линии с шагом step() и для каждой точки зовёт visit(позиция, направление, наружная нормаль)
    static void Walk(List<Vector2> pts, float orient, System.Func<float> step, System.Action<Vector2, Vector2, Vector2> visit)
    {
        float toNext = 0f;
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = pts[i], b = pts[(i + 1) % n];
            Vector2 seg = b - a;
            float len = seg.magnitude;
            if (len < 0.01f) continue;
            Vector2 dir = seg / len;
            Vector2 nrm = orient >= 0f ? new Vector2(dir.y, -dir.x) : new Vector2(-dir.y, dir.x);

            float t = toNext;
            while (t <= len)
            {
                visit(a + dir * t, dir, nrm);
                t += Mathf.Max(0.3f, step());
            }
            toNext = t - len;
        }
    }

    static Vector2 NearestPoint(List<Vector2> pts, Vector2 p)
    {
        Vector2 best = pts[0];
        float bd = float.MaxValue;
        for (int i = 0; i < pts.Count; i++)
        {
            float d = (pts[i] - p).sqrMagnitude;
            if (d < bd) { bd = d; best = pts[i]; }
        }
        return best;
    }

    static float Orientation(List<Vector2> pts)
    {
        float area = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];
            area += a.x * b.y - b.x * a.y;
        }
        return area >= 0f ? 1f : -1f;
    }

    // Земля по размеру границы, за ней цвет тумана вместо пустоты
    public void FitGround()
    {
        var pts = GetPoints();
        Vector2 min = pts[0], max = pts[0];
        foreach (var p in pts) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }

        // у парящего острова земля не выходит за контур (за краем пустота), у камней нужен запас под фон
        float ext = IsFloating ? OuterMargin : rows * rowSpacing + groundMargin;
        Vector2 size = (max - min) + Vector2.one * ext * 2f;
        Vector2 mid = (min + max) * 0.5f;

        var g = GameObject.Find("Ground_Auto");
        if (g != null)
        {
            g.transform.position = new Vector3(mid.x, g.transform.position.y, mid.y);
            g.transform.localScale = new Vector3(size.x / 10f, 1f, size.y / 10f); // Plane = 10×10
        }
        else if (!IsFloating) Debug.LogWarning("MapBorder: не нашёл Ground_Auto");   // у парящего острова землю ищет FloatingIsland (Ground Object / Terrain)

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
        }
    }

    // Убирает только камни и стены, твои точки остаются
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i);
            if (c.name != "Rocks" && c.name != "Walls" && c.name != "GateRocks") continue;
#if UNITY_EDITOR
            DestroyImmediate(c.gameObject);
#else
            Destroy(c.gameObject);
#endif
        }
    }

#if UNITY_EDITOR
    public void Generate()
    {
        bool floating = IsFloating;
        if (!floating && (rockPrefabs == null || rockPrefabs.Length == 0))
        {
            Debug.LogWarning("MapBorder: перетащи хотя бы один префаб камня в Rock Prefabs");
            return;
        }

        // родитель должен стоять в нуле без масштаба; точки при этом не двигаем
        var keep = new List<Transform>();
        var pr = transform.Find("PathPoints");
        var gp = transform.Find("GatePoint");
        if (pr != null) for (int i = 0; i < pr.childCount; i++) keep.Add(pr.GetChild(i));
        if (gp != null) keep.Add(gp);
        var saved = new Vector3[keep.Count];
        for (int i = 0; i < keep.Count; i++) saved[i] = keep[i].position;
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;
        for (int i = 0; i < keep.Count; i++) keep[i].position = saved[i];

        Clear();

        var pts = GetPoints();
        var gate = GateWorld(pts);
        float orient = Orientation(pts);
        float gw2 = gateWidth * 0.5f;

        // парящий остров: стены идут по тому же сглаженному краю, который рисует FloatingIsland
        if (floating)
        {
            var fi = GetComponent<FloatingIsland>();
            if (fi != null)
            {
                var loop = fi.GetEdgeLoop();
                if (loop.Count >= 3)
                {
                    pts = loop;
                    orient = Orientation(pts);
                    if (gate.HasValue) gate = NearestPoint(pts, gate.Value);   // проход ровно на линии стены
                }
            }
        }

        var rnd = new System.Random(seed);
        float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

        var rocksRoot = new GameObject("Rocks").transform; rocksRoot.SetParent(transform, false);
        var wallsRoot = new GameObject("Walls").transform; wallsRoot.SetParent(transform, false);
        Transform gateR = null;
        if (gate.HasValue) { gateR = new GameObject("GateRocks").transform; gateR.SetParent(transform, false); }

        // камни: ряд за рядом, наружу от линии (у парящего острова камней нет, остаются только стены)
        for (int row = 0; row < (floating ? 0 : rows); row++)
        {
            int r = row;
            Walk(pts, orient, () => spacing * R(0.8f, 1.2f), (p, d, n) =>
            {
                bool inGate = gate.HasValue && Vector2.Distance(p, gate.Value) < gw2;
                float wave = Mathf.PerlinNoise(p.x * 0.12f + seed, p.y * 0.12f) * waviness;
                Vector2 q = p + n * (r * rowSpacing + wave + R(-jitter, jitter)) + d * R(-jitter, jitter);
                float scale = R(scaleRange.x, scaleRange.y) * (1f + r * outerBoost);
                Spawn(inGate ? gateR : rocksRoot, q, scale, R, rnd);
            });
        }

        // невидимая стена кусочками по 4 м
        const float ws = 4f;
        // у камней стена стоит посреди рядов, у парящего острова чуть внутри от края, чтобы игрок не нависал над пропастью
        float wallOut = floating ? -edgeInset : rowSpacing * (rows - 1) * 0.5f + 0.5f;
        Walk(pts, orient, () => ws, (p, d, n) =>
        {
            bool inGate = gate.HasValue && Vector2.Distance(p, gate.Value) < gw2 + 1f;
            if (floating && allowFall && !inGate) return;   // с острова можно упасть: стены только у прохода к мосту
            Vector2 q = p + n * wallOut;
            var go = new GameObject("Wall");
            go.transform.SetParent(inGate ? gateR : wallsRoot, false);
            go.transform.position = new Vector3(q.x, groundY + wallHeight * 0.5f, q.y);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(d.x, 0f, d.y));
            var bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(wallThickness, wallHeight, ws * 1.2f);
        });

        if (floating) { DisableLegacyBoundary(); DisableHiddenGroundCollider(); }   // старые стены от генератора локации в сцене тоже гасим, не только в рантайме

        FitGround();
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    void Spawn(Transform parent, Vector2 p, float scale, System.Func<float, float, float> R, System.Random rnd)
    {
        var prefab = rockPrefabs[rnd.Next(rockPrefabs.Length)];
        if (prefab == null) return;

        GameObject go = null;
        if (EditorUtility.IsPersistent(prefab))
            go = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (go == null)
        {
            go = Instantiate(prefab, parent);
            go.SetActive(true);
        }
        go.transform.position = new Vector3(p.x, groundY - 0.12f * scale, p.y);
        go.transform.rotation = Quaternion.Euler(R(-5f, 5f), R(0f, 360f), R(-5f, 5f));
        go.transform.localScale = prefab.transform.localScale * scale;
        go.isStatic = true;

        if (removeRockColliders)
            foreach (var col in go.GetComponentsInChildren<Collider>())
                DestroyImmediate(col);
    }

    // ---------- рисование линии ----------
    Transform EnsureChild(string n)
    {
        var t = transform.Find(n);
        if (t == null)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            t = go.transform;
        }
        return t;
    }

    static Vector3 ViewPoint(float y)
    {
        Vector3 p = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        p.y = y;
        return p;
    }

    // Новая точка встаёт в центр Scene-вида, двигай её стрелками
    public void AddPoint()
    {
        var pr = EnsureChild("PathPoints");
        var go = new GameObject("P" + pr.childCount);
        go.transform.SetParent(pr, true);
        go.transform.position = ViewPoint(groundY);
        Undo.RegisterCreatedObjectUndo(go, "Add border point");
        Selection.activeGameObject = go;
        useCustomPath = true;
    }

    // Быстрый старт: 8 точек по периметру прямоугольника, дальше тащи их куда нужно
    public void PointsFromRectangle()
    {
        var pr = EnsureChild("PathPoints");
        for (int i = pr.childCount - 1; i >= 0; i--) DestroyImmediate(pr.GetChild(i).gameObject);

        Vector2 c = center, h = halfSize;
        Vector2[] v =
        {
            new Vector2(c.x - h.x, c.y - h.y), new Vector2(c.x, c.y - h.y), new Vector2(c.x + h.x, c.y - h.y),
            new Vector2(c.x + h.x, c.y),
            new Vector2(c.x + h.x, c.y + h.y), new Vector2(c.x, c.y + h.y), new Vector2(c.x - h.x, c.y + h.y),
            new Vector2(c.x - h.x, c.y)
        };
        for (int i = 0; i < v.Length; i++)
        {
            var go = new GameObject("P" + i);
            go.transform.SetParent(pr, true);
            go.transform.position = new Vector3(v[i].x, groundY, v[i].y);
        }
        useCustomPath = true;
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    // Точка прохода к мосту для своей линии
    public void AddGatePoint()
    {
        var g = transform.Find("GatePoint");
        if (g == null)
        {
            var go = new GameObject("GatePoint");
            go.transform.SetParent(transform, true);
            go.transform.position = ViewPoint(groundY);
            Undo.RegisterCreatedObjectUndo(go, "Add gate point");
            g = go.transform;
        }
        Selection.activeGameObject = g.gameObject;
    }
#endif

    void OnDrawGizmosSelected()
    {
        var pts = GetPoints();
        if (pts.Count < 2) return;

        Gizmos.color = new Color(1f, 0.7f, 0.2f, 1f);
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];
            Vector3 A = new Vector3(a.x, groundY + 0.3f, a.y), B = new Vector3(b.x, groundY + 0.3f, b.y);
            Gizmos.DrawLine(A, B);
            if (IsCustom()) Gizmos.DrawSphere(A, 0.6f);
        }

        var gate = GateWorld(pts);
        if (gate.HasValue)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 1f);
            Gizmos.DrawWireSphere(new Vector3(gate.Value.x, groundY + 0.3f, gate.Value.y), gateWidth * 0.5f);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(MapBorder))]
public class MapBorderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var b = (MapBorder)target;

        if (b.IsFloating)
            EditorGUILayout.HelpBox(b.allowFall
                ? "Парящий остров, Allow Fall включён: невидимых стен по краю нет, с края можно упасть. Падение и возвращение ведёт IslandEdgeGuard (добавляется сам при старте). Вид края и скалы снизу делает FloatingIsland."
                : "Парящий остров: камней нет, генерация ставит невидимые стены и ворота. Вид края и скалы снизу делает компонент FloatingIsland на этом же объекте.", MessageType.Info);

        GUILayout.Space(8);
        GUILayout.Label("Своя форма", EditorStyles.boldLabel);
        if (GUILayout.Button("Из прямоугольника в точки (старт)")) b.PointsFromRectangle();
        if (GUILayout.Button("Добавить точку (в центр сцены)")) b.AddPoint();
        if (GUILayout.Button("Точка прохода к мосту")) b.AddGatePoint();
        EditorGUILayout.HelpBox("Выдели точку и жми Ctrl+D: копия встанет следующей по порядку. Порядок точек = порядок в Hierarchy, линия замыкается сама.", MessageType.None);

        GUILayout.Space(8);
        if (GUILayout.Button("Сгенерировать границу", GUILayout.Height(32))) b.Generate();
        if (GUILayout.Button("Подогнать землю и фон")) { b.FitGround(); EditorSceneManager.MarkSceneDirty(b.gameObject.scene); }
        if (GUILayout.Button("Удалить камни и стены")) { b.Clear(); EditorSceneManager.MarkSceneDirty(b.gameObject.scene); }
        if (b.IsFloating && GUILayout.Button("Выключить старые стены Boundary (не пускают за край)")) { int n = b.DisableLegacyBoundary(); b.DisableHiddenGroundCollider(); Debug.Log("MapBorder: выключено старых стен: " + n); EditorSceneManager.MarkSceneDirty(b.gameObject.scene); }
    }
}

public static class MapBorderMenu
{
    [MenuItem("Tools/BuilderSim/Create Map Border")]
    static void Create()
    {
        var existing = Object.FindFirstObjectByType<MapBorder>();
        if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }

        var go = new GameObject("MapBorder");
        go.AddComponent<MapBorder>();
        Selection.activeGameObject = go;
        Undo.RegisterCreatedObjectUndo(go, "Create Map Border");
    }

    // Старый MapBorder в сцене -> парящий остров: ставит стиль, вешает FloatingIsland, убирает камни, пересобирает стены
    [MenuItem("Tools/BuilderSim/Convert Border To Floating Island")]
    static void ConvertToFloating()
    {
        var b = Object.FindFirstObjectByType<MapBorder>();
        if (b == null)
        {
            // в сцене MapBorder не было: создаём, встаём вокруг игрока (или в ноль), размер 35 м от центра, потом правится в инспекторе
            var go = new GameObject("MapBorder");
            b = go.AddComponent<MapBorder>();
            Undo.RegisterCreatedObjectUndo(go, "Create Floating Island");
            var pl = Object.FindFirstObjectByType<PlayerMovement>();
            if (pl != null) b.center = new Vector2(pl.transform.position.x, pl.transform.position.z);
            b.halfSize = new Vector2(35f, 35f);
            if (pl != null) b.groundY = pl.transform.position.y;
        }

        Undo.RecordObject(b, "Floating Island");
        b.style = MapBorder.Style.FloatingIsland;
        var fi = b.GetComponent<FloatingIsland>();
        if (fi == null) fi = Undo.AddComponent<FloatingIsland>(b.gameObject);
        fi.Build();   // камни удаляются (Clear внутри Generate), остров собирается, стены и ворота встают по краю
        Selection.activeGameObject = b.gameObject;
        EditorSceneManager.MarkSceneDirty(b.gameObject.scene);
    }
}
#endif
