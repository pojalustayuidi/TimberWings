// Assets/_Project/Scripts/Location/FloatingIsland.Border.cs (НЕ в Editor)
// Граница карты = край острова. Отдельного MapBorder больше нет: камней нет, край рисует сам остров (обрыв, скала снизу, облака).
//
// Что здесь живёт:
//   • контур: точки PathPoints под этим объектом (меньше 3 точек = запасной прямоугольник Center / Half Size), форма двигается точками;
//   • проход к мосту: точка GatePoint, до постройки моста закрыт невидимой стеной (GateWalls), OpenGate() её опускает;
//   • с остальной части края можно упасть, стен там нет: игрока возвращает IslandEdgeGuard (вешается сам в Awake);
//   • гасит старые прямоугольные стены генератора локации и коллайдер скрытой плоскости Ground_Auto: по ним ходили за краем острова.
// Мост построен: FloatingIsland.Instance.OpenGate().
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public partial class FloatingIsland
{
    public static FloatingIsland Instance;

    // Проход к мосту открыт (плавно или сразу при загрузке). Слушает HudMinimap: перепечатывает карту
    public static event System.Action GateOpened;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; GateOpened = null; }

    public const string GateRootName = "GateWalls";
    const string LegacyGateRootName = "GateRocks";   // так проход назывался у MapBorder: старые сцены читаются, пока остров не пересобран

    // На сколько метров наружу от линии что-то выступает (небольшой запас для карты и UV земли)
    public const float OuterMargin = 1f;

    [Header("Запасная форма (если точек PathPoints меньше трёх)")]
    public Vector2 center = Vector2.zero;
    public Vector2 halfSize = new Vector2(30f, 30f);
    public float groundY = 0f;

    [Header("Проход к мосту (центр = объект GatePoint)")]
    public float gateWidth = 8f;
    [Tooltip("На сколько метров стена прохода стоит внутри от линии края")]
    public float edgeInset = 0.4f;
    public float wallHeight = 6f;
    public float wallThickness = 1.5f;

    Transform gateRoot;
    bool gateOpened;

    public Transform GateRoot
    {
        get
        {
            if (gateRoot == null) gateRoot = transform.Find(GateRootName);
            if (gateRoot == null) gateRoot = transform.Find(LegacyGateRootName);
            return gateRoot;
        }
    }

    void Awake()
    {
        Instance = this;
        DisableLegacyBoundary();
        DisableHiddenGroundCollider();
        if (GetComponent<IslandEdgeGuard>() == null) gameObject.AddComponent<IslandEdgeGuard>();
    }

    void OnEnable() { GameEvents.BuildingRestored += OnBuildingRestored; }
    void OnDisable() { GameEvents.BuildingRestored -= OnBuildingRestored; }

    // ---------- проход к мосту ----------

    // Загрузка сохранения: мост уже достроен, проход открываем сразу, без опускания стены
    void OnBuildingRestored(string id, int stage)
    {
        var b = BuildingController.FindById(id);
        if (b != null && b.SiteKind == BuildSiteKind.Bridge && b.IsFinished) OpenGateInstant();
    }

    public void OpenGateInstant()
    {
        if (gateOpened || GateRoot == null) return;
        gateOpened = true;
        GateRoot.gameObject.SetActive(false);
        if (GateOpened != null) GateOpened();
    }

    // Мост построен: стена прохода плавно уходит в землю. Повторный вызов (два события стройки подряд) игнорируется
    public void OpenGate()
    {
        if (gateOpened || GateRoot == null) return;
        gateOpened = true;
        StartCoroutine(Sink(GateRoot));
        if (GateOpened != null) GateOpened();   // карта подождёт rebakeDelay: стена к этому времени уже опустится
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

    // ---------- контур и ворота ----------

    // Точки по кругу против часовой стрелки (x, z): PathPoints, а без них запасной прямоугольник
    public List<Vector2> GetOutline()
    {
        var list = new List<Vector2>();
        var pr = transform.Find("PathPoints");
        if (pr != null && pr.childCount >= 3)
        {
            for (int i = 0; i < pr.childCount; i++)
            {
                var p = pr.GetChild(i).position;
                list.Add(new Vector2(p.x, p.z));
            }
            return list;
        }
        list.Add(new Vector2(center.x - halfSize.x, center.y - halfSize.y));
        list.Add(new Vector2(center.x + halfSize.x, center.y - halfSize.y));
        list.Add(new Vector2(center.x + halfSize.x, center.y + halfSize.y));
        list.Add(new Vector2(center.x - halfSize.x, center.y + halfSize.y));
        return list;
    }

    // Центр прохода к мосту (x, z) или null, если GatePoint нет
    public Vector2? GetGatePosition()
    {
        var g = transform.Find("GatePoint");
        return g != null ? new Vector2(g.position.x, g.position.z) : (Vector2?)null;
    }

    // Радиус острова по самому дальнему от центра габариту контура (свет, тени)
    public float Radius
    {
        get
        {
            var pts = GetOutline();
            Vector2 mn = pts[0], mx = pts[0];
            foreach (var p in pts) { mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p); }
            return Mathf.Max(mx.x - mn.x, mx.y - mn.y) * 0.5f;
        }
    }

    // ---------- старые стены и пол за краем ----------

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
            var top = transform.Find(RootName + "/Top");
            otherGround = top != null && top.GetComponent<Collider>() != null;
        }
        if (!otherGround) return;
        foreach (var c in ga.GetComponents<Collider>())
            if (c.enabled) { c.enabled = false; Debug.Log("FloatingIsland: выключил коллайдер скрытой земли Ground_Auto (по нему ходили за краем острова)", ga); }
    }

    // Выключает невидимые стены-прямоугольник от LocationBuilder: объект «Boundary» и одиночные Wall_N/Wall_S/Wall_E/Wall_W.
    // Они стоят по краю Terrain, а не острова, и не дают упасть. Стены прохода (GateWalls), стены моста (GateBridge)
    // и WaterBarrier над рекой не трогаем.
    public int DisableLegacyBoundary()
    {
        int off = 0;
        var all = FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var bc in all)
        {
            if (bc == null || bc.isTrigger) continue;
            var t = bc.transform;
            if (t.IsChildOf(transform)) continue;   // свои стены не трогаем
            bool inBoundary = t.parent != null && t.parent.name == "Boundary";
            string n = t.name;
            if (!inBoundary && n != "Wall_N" && n != "Wall_S" && n != "Wall_E" && n != "Wall_W") continue;
            var target = inBoundary ? t.parent.gameObject : t.gameObject;
            if (!target.activeSelf) continue;
            target.SetActive(false);
            off++;
            Debug.Log("FloatingIsland: выключил старую невидимую стену «" + target.name + "» (она не давала упасть с острова)", target);
        }
        return off;
    }

    // Земля по размеру контура, за краем цвет тумана вместо пустоты
    public void FitGround()
    {
        var pts = GetOutline();
        Vector2 min = pts[0], max = pts[0];
        foreach (var p in pts) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
        Vector2 size = (max - min) + Vector2.one * OuterMargin * 2f;
        Vector2 mid = (min + max) * 0.5f;

        var g = GameObject.Find("Ground_Auto");
        if (g != null)
        {
            g.transform.position = new Vector3(mid.x, g.transform.position.y, mid.y);
            g.transform.localScale = new Vector3(size.x / 10f, 1f, size.y / 10f);   // Plane = 10×10
        }

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
        }
    }

    // Убирает стену прохода (и остатки камней и стен от MapBorder в старых сценах), точки остаются
    public void ClearWalls()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i);
            string n = c.name;
            if (n != GateRootName && n != LegacyGateRootName && n != "Rocks" && n != "Walls") continue;
#if UNITY_EDITOR
            DestroyImmediate(c.gameObject);
#else
            Destroy(c.gameObject);
#endif
        }
        gateRoot = null;
    }

#if UNITY_EDITOR
    // Стена прохода кусочками по 4 м вдоль того же сглаженного края, что рисует остров. Больше нигде стен нет: с острова можно упасть
    public void BuildWalls()
    {
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

        ClearWalls();

        var gate0 = GetGatePosition();
        var loop = GetEdgeLoop();   // против часовой: наружная нормаль справа от направления обхода
        if (gate0.HasValue && loop.Count >= 3)
        {
            Vector2 gate = loop[0];   // проход ровно на линии стены: ближайшая точка края
            foreach (var q in loop) if ((q - gate0.Value).sqrMagnitude < (gate - gate0.Value).sqrMagnitude) gate = q;

            gateRoot = new GameObject(GateRootName).transform;
            gateRoot.SetParent(transform, false);

            const float ws = 4f;
            float reach = gateWidth * 0.5f + 1f;
            float toNext = 0f;
            for (int i = 0; i < loop.Count; i++)
            {
                Vector2 a = loop[i], b = loop[(i + 1) % loop.Count];
                float len = (b - a).magnitude;
                if (len < 0.01f) continue;
                Vector2 d = (b - a) / len, n = new Vector2(d.y, -d.x);
                float t = toNext;
                for (; t <= len; t += ws)
                {
                    Vector2 p = a + d * t;
                    if (Vector2.Distance(p, gate) >= reach) continue;
                    Vector2 q = p - n * edgeInset;
                    var go = new GameObject("Wall");
                    go.transform.SetParent(gateRoot, false);
                    go.transform.position = new Vector3(q.x, groundY + wallHeight * 0.5f, q.y);
                    go.transform.rotation = Quaternion.LookRotation(new Vector3(d.x, 0f, d.y));
                    go.AddComponent<BoxCollider>().size = new Vector3(wallThickness, wallHeight, ws * 1.2f);
                }
                toNext = t - len;
            }
        }

        DisableLegacyBoundary();
        DisableHiddenGroundCollider();
        FitGround();
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    // ---------- рисование контура ----------
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
        Undo.RegisterCreatedObjectUndo(go, "Add island point");
        Selection.activeGameObject = go;
    }

    // Точка прохода к мосту
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
}
