// Assets/_Project/Scripts/Forest/ChopGrove/ChopGroveBuilder.cs (НЕ в Editor)
// Собирает «Поляну рубки» кодом по референсу (арка с топором, гирлянды и фонари, пни, телега с брёвнами, навес, козлы, брёвна,
// камни, кусты, цветы) и перекладывает тропы. Вешается на метку PAD_GroveSouth (ChopGroveBootstrap делает это сам при старте сцены).
//
// Части класса:
//   ChopGroveBuilder.cs         этот файл: настройки, сборка, земля под ногами, материал, деревья и старый декор
//   ChopGroveBuilder.Ground.cs  земля: площадки и тропы (меш с цветом вершин, плавный переход в траву)
//   ChopGroveBuilder.Props.cs   ворота, гирлянды, пни, козлы, телега, навес, брёвна, указатель, фонари
//   ChopGroveBuilder.Decor.cs   камни, кусты, цветы, трава, щепа (расстановка по свободным местам)
//
// Как работает:
//   • Корень _ChopGrove создаётся ребёнком метки. Локальные оси: +Z вглубь поляны, ворота на -Z (вход).
//   • Вход автоматически со стороны костра (CAMPFIRE_Point). Не угадал: autoEntry = false и entryYaw, или flipEntry.
//   • Всё в двух мешах (земля и реквизит) + несколько маленьких объектов (качающиеся фонари и вывеска с огнями FireSource).
//   • Сборка детерминирована (seed), можно пересобирать сколько угодно: ПКМ по заголовку компонента → «Собрать поляну».
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public partial class ChopGroveBuilder : MonoBehaviour
{
    public const string RootName = "_ChopGrove";

    [Header("Сборка")]
    [Tooltip("Собирать при старте игры. Если выключить: собирай вручную из меню компонента (ПКМ → Собрать поляну)")]
    public bool buildOnStart = true;
    public int seed = 7241;

    [Header("Вход (откуда игрок приходит)")]
    [Tooltip("Ворота и главная тропа смотрят на костёр (CAMPFIRE_Point)")]
    public bool autoEntry = true;
    [Tooltip("Если autoEntry выключен: направление ОТ поляны К входу, градусы мира (0 = +Z, 90 = +X, 180 = -Z)")]
    public float entryYaw = 180f;
    [Tooltip("Развернуть вход на 180°")]
    public bool flipEntry;

    [Header("Поляна")]
    public float clearingRadius = 9.5f;
    public GrovePath[] paths = GrovePath.Defaults();
    public GroveDisc[] discs = GroveDisc.Defaults();

    [Header("Земля (цвета)")]
    public Color dirtCenter = GrovePal.DirtCenter;
    public Color dirtMid = GrovePal.DirtMid;
    public Color dirtEdge = GrovePal.DirtEdge;
    [Tooltip("Цвет травы, в который плавно уходит край земли. Подгони под траву острова")]
    public Color grassEdge = GrovePal.Grass;
    [Tooltip("На сколько метров земля приподнята над лугом (от мерцания)")]
    public float groundLift = 0.04f;

    [Header("Детали")]
    [Range(0f, 2f)] public float scatterDensity = 1f;
    public bool bunting = true;
    public bool lanterns = true;
    public bool interactiveStump = true;
    public bool addAmbientEffects = true;

    [Header("Деревья и старый декор")]
    [Tooltip("Сдвинуть деревья, стоящие на тропах, площадках и реквизите, чуть в сторону (в редакторе сохрани сцену после сборки)")]
    public bool relocateTrees = true;
    public float treeMargin = 1.1f;
    [Tooltip("Выключить рендеры и коллайдеры старого декора поляны (объект с этим именем), чтобы он не дублировал новый")]
    public bool hideOldDecor = true;
    public string oldDecorName = "ChopGladeDecor";

    // ---------------------------------------------------------------- состояние сборки
    Transform root;
    GroveMesh props, ground;
    readonly List<Vector3> foot = new List<Vector3>();   // круги занятого места: x, z, радиус
    Transform collidersRoot;
    Transform liveRoot;                                   // фонари и вывеска (качаются, светятся)
    Transform camp;
    static readonly RaycastHit[] hitBuf = new RaycastHit[24];

    void Start()
    {
        if (buildOnStart && Application.isPlaying && transform.Find(RootName) == null) Build();
        else if (Application.isPlaying) EnsureOldDecorHidden();
    }

    // ---------------------------------------------------------------- вход
    float ResolveEntryYaw()
    {
        float yaw = entryYaw;
        if (autoEntry)
        {
            var cp = GameObject.Find("CAMPFIRE_Point");
            if (cp != null)
            {
                Vector3 d = cp.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 4f) yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }
        }
        if (flipEntry) yaw += 180f;
        return yaw;
    }

    // ---------------------------------------------------------------- главный метод
    [ContextMenu("Собрать поляну")]
    public void Build()
    {
        Clear();
        Physics.SyncTransforms();

        var rootGo = new GameObject(RootName);
        root = rootGo.transform;
        root.SetParent(transform, false);
        root.localPosition = Vector3.zero;
        root.rotation = Quaternion.Euler(0f, ResolveEntryYaw() + 180f, 0f);   // локальный +Z смотрит вглубь поляны
        Vector3 ls = transform.lossyScale;
        if (Mathf.Abs(ls.x - 1f) > 0.01f || Mathf.Abs(ls.y - 1f) > 0.01f || Mathf.Abs(ls.z - 1f) > 0.01f)
            root.localScale = new Vector3(1f / Mathf.Max(0.01f, ls.x), 1f / Mathf.Max(0.01f, ls.y), 1f / Mathf.Max(0.01f, ls.z));

        collidersRoot = NewChild("Colliders");
        liveRoot = NewChild("Live");
        props = new GroveMesh(seed);
        ground = new GroveMesh(seed + 17);
        foot.Clear();
        if (paths != null) foreach (var p in paths) if (p != null) p.samples = null;

        BuildGround();
        BuildProps();
        BuildDecor();
        Finish();

        if (relocateTrees) RelocateTrees();
        EnsureOldDecorHidden();
        if (addAmbientEffects) AddAmbient();
        Debug.Log("ChopGrove: поляна собрана (" + (props.TriCount + ground.TriCount) + " треугольников, 2 меша).", this);
    }

    [ContextMenu("Удалить собранную поляну")]
    public void Clear()
    {
        var old = transform.Find(RootName);
        if (old == null) return;
        if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
    }

    Transform NewChild(string n)
    {
        var go = new GameObject(n);
        go.transform.SetParent(root, false);
        return go.transform;
    }

    // ---------------------------------------------------------------- земля под ногами
    // Y в ЛОКАЛЬНЫХ координатах корня (0 = уровень метки)
    public float GroundY(float lx, float lz)
    {
        Vector3 w = root.TransformPoint(new Vector3(lx, 0f, lz));
        Vector3 origin = new Vector3(w.x, transform.position.y + 40f, w.z);
        int n = Physics.RaycastNonAlloc(origin, Vector3.down, hitBuf, 90f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; bool found = false;
        for (int i = 0; i < n; i++)
        {
            if (hitBuf[i].collider.transform.IsChildOf(root)) continue;
            if (hitBuf[i].distance < best) { best = hitBuf[i].distance; found = true; }
        }
        if (!found) return 0f;
        return (origin.y - best) - root.position.y;
    }

    // ---------------------------------------------------------------- свободное место
    // метров до ближайшей занятой области (<0 = занято): тропы, площадки, реквизит
    public float Clearance(float lx, float lz, bool includeDiscs = true)
    {
        Vector2 p = new Vector2(lx, lz);
        float best = float.MaxValue;
        if (paths != null)
            for (int i = 0; i < paths.Length; i++)
                if (paths[i] != null) best = Mathf.Min(best, paths[i].Clearance(p));
        if (includeDiscs && discs != null)
            for (int i = 0; i < discs.Length; i++)
                if (discs[i] != null) best = Mathf.Min(best, discs[i].Clearance(p));
        for (int i = 0; i < foot.Count; i++)
            best = Mathf.Min(best, Vector2.Distance(p, new Vector2(foot[i].x, foot[i].y)) - foot[i].z);
        return best;
    }

    void Occupy(float x, float z, float r) { foot.Add(new Vector3(x, z, r)); }

    // ---------------------------------------------------------------- материалы
    static Material groveMat;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { groveMat = null; }

    static Material GroveMaterial()
    {
        if (groveMat != null) return groveMat;
        Shader sh = Shader.Find("Project/CandyToon");
        bool candy = sh != null;
        if (!candy) sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        groveMat = new Material(sh) { name = "ChopGrove_Mat" };
        if (candy)
        {
            if (groveMat.HasProperty("_BaseColor")) groveMat.SetColor("_BaseColor", Color.white);
            if (groveMat.HasProperty("_VertexColorStrength")) groveMat.SetFloat("_VertexColorStrength", 1f);
            if (groveMat.HasProperty("_PosVary")) groveMat.SetFloat("_PosVary", 0.06f);
        }
        else Debug.LogWarning("ChopGrove: шейдер Project/CandyToon не найден, цвета вершин не будут видны. Проверь, что шейдер в проекте/сборке.");
        return groveMat;
    }

    GameObject MeshObject(string n, Transform parent, Mesh mesh, bool cast, bool receive = true)
    {
        var go = new GameObject(n);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = GroveMaterial();
        r.shadowCastingMode = cast ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = receive;
        return go;
    }

    void Finish()
    {
        MeshObject("Ground", root, ground.ToMesh("ChopGrove_Ground"), false);
        MeshObject("Props", root, props.ToMesh("ChopGrove_Props"), true);
    }

    // ---------------------------------------------------------------- коллайдеры (без рендера)
    void AddBoxCollider(string n, Vector3 center, Vector3 size, float yaw)
    {
        var go = new GameObject(n);
        go.transform.SetParent(collidersRoot, false);
        go.transform.localPosition = center;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.AddComponent<BoxCollider>().size = size;
    }

    void AddCapsuleCollider(string n, Vector3 center, float radius, float height)
    {
        var go = new GameObject(n);
        go.transform.SetParent(collidersRoot, false);
        go.transform.localPosition = center;
        var c = go.AddComponent<CapsuleCollider>();
        c.radius = radius; c.height = height;
    }

    // ---------------------------------------------------------------- деревья на тропах
    void RelocateTrees()
    {
        var list = new List<Transform>();
        foreach (var ri in FindObjectsByType<ResourceItem>(FindObjectsSortMode.None)) if (ri != null) list.Add(ri.transform);
        foreach (var sw in FindObjectsByType<ToonSway>(FindObjectsSortMode.None))
        {
            if (sw == null || sw.transform.IsChildOf(root)) continue;
            if (sw.GetComponentInParent<LandmarkTree>() != null) continue;   // дедушка и деревья-хранители стоят где стоят
            if (!list.Contains(sw.transform)) list.Add(sw.transform);
        }

        float maxR = clearingRadius + 8f;
        var keep = new List<Vector2>();   // уже стоящие деревья вокруг (чтобы не ставить впритык)
        var movers = new List<Transform>();
        foreach (var t in list)
        {
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            Vector3 lp = root.InverseTransformPoint(t.position);
            if (lp.x * lp.x + lp.z * lp.z > maxR * maxR) continue;
            if (Clearance(lp.x, lp.z) < treeMargin) movers.Add(t); else keep.Add(new Vector2(lp.x, lp.z));
        }

        int moved = 0;
        Vector2 center = discs != null && discs.Length > 0 && discs[0] != null ? discs[0].center : new Vector2(0f, 0.8f);
        foreach (var t in movers)
        {
            Vector3 lp = root.InverseTransformPoint(t.position);
            Vector2 p = new Vector2(lp.x, lp.z);
            Vector2 dir = p - center;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
            dir.Normalize();
            for (float s = 0.5f; s <= 11f; s += 0.5f)
            {
                Vector2 c = p + dir * s;
                if (Clearance(c.x, c.y) < treeMargin) continue;
                bool crowded = false;
                for (int i = 0; i < keep.Count; i++) if ((keep[i] - c).sqrMagnitude < 1.3f * 1.3f) { crowded = true; break; }
                if (crowded) continue;
                float dy = GroundY(c.x, c.y) - GroundY(p.x, p.y);
                Vector3 wp = root.TransformPoint(new Vector3(c.x, lp.y + dy, c.y));
                t.position = wp;
                keep.Add(c);
                moved++;
                break;
            }
        }
        if (moved > 0) Debug.Log("ChopGrove: сдвинуто деревьев с троп и площадок: " + moved + " из " + movers.Count + ".", this);
    }

    // ---------------------------------------------------------------- старый декор поляны
    void EnsureOldDecorHidden()
    {
        if (!hideOldDecor || string.IsNullOrEmpty(oldDecorName)) return;
        var old = GameObject.Find(oldDecorName);
        if (old == null) return;
        int n = 0;
        foreach (var r in old.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;   // эффекты (GladeVibe) оставляем
            if (r.enabled) { r.enabled = false; n++; }
        }
        foreach (var c in old.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        if (n > 0) Debug.Log("ChopGrove: выключено рендеров старого декора «" + oldDecorName + "»: " + n + " (сними hideOldDecor, чтобы вернуть).", this);
    }

    void AddAmbient()
    {
        // золотая пыль, лучи, светлячки, листопад: уже есть в проекте (GladeVibe), вешаем если его на поляне ещё нет
        foreach (var g in FindObjectsByType<GladeVibe>(FindObjectsSortMode.None))
            if (g != null && (g.transform.position - transform.position).sqrMagnitude < 15f * 15f) return;
        var go = new GameObject("GladeVibe");
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(0f, 0f, 0.8f);
        var v = go.AddComponent<GladeVibe>();
        v.radius = clearingRadius;
    }

    // ---------------------------------------------------------------- гизмо
    void OnDrawGizmosSelected()
    {
        Vector3 p = transform.position + Vector3.up * 0.2f;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        const int n = 48;
        Vector3 prev = p + new Vector3(clearingRadius, 0f, 0f);
        for (int i = 1; i <= n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            Vector3 cur = p + new Vector3(Mathf.Cos(a) * clearingRadius, 0f, Mathf.Sin(a) * clearingRadius);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
        float yaw = ResolveEntryYaw();
        Vector3 toEntry = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(p, p + toEntry * (clearingRadius + 4f));   // красная стрелка: сторона входа (ворота)
        Gizmos.DrawSphere(p + toEntry * (clearingRadius + 4f), 0.4f);
        Quaternion rot = Quaternion.Euler(0f, yaw + 180f, 0f);
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
        if (paths != null)
            foreach (var path in paths)
            {
                if (path == null || path.points == null) continue;
                for (int i = 0; i < path.points.Length - 1; i++)
                    Gizmos.DrawLine(p + rot * new Vector3(path.points[i].x, 0f, path.points[i].y), p + rot * new Vector3(path.points[i + 1].x, 0f, path.points[i + 1].y));
            }
    }
}
