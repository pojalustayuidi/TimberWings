// Assets/_Project/Scripts/Location/FloatingIsland.cs (НЕ в Editor)
// Парящий остров и граница карты в одном компоненте (ворота, стена прохода, контур: FloatingIsland.Border.cs).
// Контур берётся из точек PathPoints под этим объектом (без них запасной прямоугольник), форму острова двигаешь точками.
//
// Что собирает кнопка «Собрать остров» (все части лежат под объектом "Island"):
//   Земля : если в сцене есть Terrain от LocationBuilder, он ОСТАЁТСЯ поверхностью острова (холмы, река, тропы, площадки целы).
//           Terrain обрезается по контуру острова дырками (TerrainData.SetHoles), всё за краем исчезает вместе с коллайдером.
//           Если Terrain нет, строится плоская поверхность Top по контуру (запасной вариант).
//   Cliff : рваный обрыв с травяной кромкой, землёй и скалой (цвета в вершинах, шейдер Project/IslandRock).
//           Верхняя кромка идёт по высоте Terrain, поэтому холмы у края не обрезаются.
// Старая плоскость Ground_Auto скрывается (рендерер выключается, коллайдер остаётся).
// Стена у прохода к мосту пересобирается по этому же контуру, поэтому там нельзя встать «в воздухе».
// Низ скалы (задача 3) цепляется к bottomRing.
// Силуэт: крупные бухты и мысы (низкочастотный шум, не трогают зону у GatePoint и защищённые места: тропы, река, площадки).
// Юбка: слои породы цветами вершин, уступы-полки на стыках слоёв, осыпь камней на полках.
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
public partial class FloatingIsland : MonoBehaviour
{
    public const string RootName = "Island";

    [Header("Контур")]
    [Tooltip("Шаг точек сглаженного края, метры")]
    public float edgeStep = 1.5f;
    [Tooltip("Насколько край кусается внутрь (метры). Наружу контур не выходит")]
    [Range(0f, 4f)] public float edgeNoise = 0.7f;
    [Tooltip("Сглаживание края: сколько раз контур «причёсывается» без усадки (Taubin). 0 = как раньше, 3-4 = плавные округлые берега. У прохода к мосту край и так ровный")]
    [Range(0, 8)] public int edgeSmooth = 4;
    public float edgeNoiseScale = 0.09f;
    [Tooltip("Радиус около прохода к мосту, где край ровный (шум плавно затухает до 2× радиуса)")]
    public float gateFlatten = 6f;

    [Header("Бухты и мысы (крупный силуэт)")]
    [Tooltip("Крупные бухты (внутрь) и мысы (наружу) по контуру. Отдельно от Edge Noise, шум низкочастотный")]
    public bool bays = true;
    [Tooltip("Максимальная глубина бухты внутрь, метры")]
    [Range(0f, 14f)] public float bayDepth = 6f;
    [Tooltip("Максимальный вынос мыса наружу, метры (за краем Terrain землю достраивает Apron)")]
    [Range(0f, 10f)] public float capeLength = 4f;
    [Tooltip("Частота шума: меньше = крупнее бухты. 0.03 это примерно 30 м от бухты до бухты")]
    [Range(0.01f, 0.12f)] public float bayScale = 0.03f;
    [Tooltip("Доля мелких изгибов поверх крупных (0 = только крупные)")]
    [Range(0f, 1f)] public float bayDetail = 0.3f;
    [Tooltip("Насколько резко выражены бухты и мысы (1 = как есть, больше = глубже и резче)")]
    [Range(0.4f, 2.5f)] public float bayContrast = 1f;
    [Tooltip("Радиус около GatePoint, где бухт и мысов нет совсем (метры). Дальше плавно нарастают на Bay Gate Fade")]
    public float bayGateClear = 14f;
    public float bayGateFade = 10f;
    [Tooltip("Бухта не подходит к защищённому ближе, чем на столько метров")]
    public float protectMargin = 2.5f;
    [Tooltip("Слои Terrain, которые бухты не трогают (части имени через запятую, без учёта регистра)")]
    public string protectLayers = "trail,path,pad,river,water";
    [Tooltip("Объекты сцены, которые бухты не трогают: имя объекта или любого родителя содержит одну из частей (через запятую). Пусто = не искать")]
    public string protectNames = "river,platform,pad,trail";
    [Tooltip("Объекты с Renderer крупнее этого (метры по X или Z) не считаются защищёнными: так огромный меш тропы не закроет весь остров")]
    public float protectMaxSize = 40f;
    [Tooltip("Ещё защищённые места руками: вокруг каждой точки радиус Keep Clear Radius")]
    public Transform[] keepClear = new Transform[0];
    public float keepClearRadius = 4f;

    [Header("Обрыв")]
    [Tooltip("Высота скалистой юбки от земли до нижнего кольца, метры (для референса 8–12)")]
    [Range(3f, 16f)] public float cliffDepth = 10f;
    [Tooltip("Рваность скалы (метры)")]
    public float cliffRoughness = 0.9f;
    [Tooltip("На сколько нижнее кольцо обрыва уходит внутрь (подбрюшье задачи 3 цепляется к нему)")]
    public float cliffTuck = 3f;
    public float lipBulge = 0.15f;
    [Tooltip("Закрыть низ обрыва плоской крышкой, пока нет подбрюшья (задача 3 это отключит)")]
    public bool capBottom = true;

    [Header("Слои породы, уступы и осыпь")]
    [Tooltip("Сколько слоёв породы в юбке (цвета слоёв ниже)")]
    [Range(2, 3)] public int rockLayers = 3;
    [Tooltip("Насколько нижний слой выступает полкой из-под верхнего (метры)")]
    [Range(0f, 4f)] public float ledgeWidth = 1.6f;
    [Tooltip("Какая часть периметра с уступами (0 = нигде, 1 = везде)")]
    [Range(0f, 1f)] public float ledgeAmount = 0.55f;
    [Tooltip("Частота шума уступов: меньше = длиннее полки")]
    [Range(0.02f, 0.2f)] public float ledgeScale = 0.06f;
    [Tooltip("Насколько высота границ слоёв гуляет вдоль края (метры)")]
    public float strataWobble = 0.9f;
    [Tooltip("Сколько осыпи (камни на полках и ниже них). 0 = нет")]
    [Range(0f, 1f)] public float screeDensity = 0.6f;
    [Tooltip("Потолок числа камней осыпи, чтобы меш не раздувался")]
    public int screeMaxRocks = 320;

    [Header("Старая земля")]
    [Tooltip("Твоя прежняя земля (плоскость, меш или Terrain). Пусто = ищем объект Ground_Auto, потом активный Terrain. Если ничего нет, остров ходит по собственной поверхности")]
    public GameObject groundObject;

    [Header("Рельеф (Terrain от LocationBuilder)")]
    [Tooltip("Резать Terrain дырками по контуру острова. Холмы, река и тропы остаются. Выключи, только если контур заведомо больше Terrain")]
    public bool cutTerrainToOutline = true;
    [Tooltip("На сколько метров верхняя кромка обрыва заходит на траву внутрь острова: закрывает зубчики на стыке с дырками Terrain")]
    [Range(0.2f, 1.5f)] public float lipOverlap = 0.6f;

    [Header("Поле вокруг Terrain")]
    [Tooltip("Достроить землю между краем Terrain и контуром острова. Высота края Terrain продолжается наружу, по этой земле можно ходить")]
    public bool buildApron = true;
    [Tooltip("Размер клетки поля, метры (меньше = ровнее стык с обрывом, больше клеток)")]
    [Range(0.3f, 1.5f)] public float apronCell = 0.5f;
    [Tooltip("На сколько метров поле опускается к краю: скругляет «плечо» вместо плоской полки на высоте края Terrain")]
    [Range(0f, 6f)] public float apronRoll = 2f;
    [Tooltip("На каком расстоянии от края Terrain поле опускается на Apron Roll (метры)")]
    [Range(0.5f, 12f)] public float apronRollLength = 3f;
    [Tooltip("Высота холмиков на поле (м, вверх и вниз): край перестаёт быть ровным валом")]
    [Range(0f, 6f)] public float apronHillHeight = 2.2f;
    [Tooltip("Размер холмиков: меньше число = крупнее холмы (0.07 это примерно 14 м от горки до горки)")]
    [Range(0.02f, 0.3f)] public float apronHillScale = 0.07f;
    [Tooltip("На каком расстоянии от края Terrain холмики набирают полную высоту (м)")]
    [Range(0.5f, 10f)] public float apronHillRamp = 3f;

    [Header("Декор края (камни, кусты, ёлки)")]
    public bool dressEdge = true;
    [Tooltip("Сколько декора по краю (0 = почти нет, 1 = густо)")]
    [Range(0f, 1f)] public float dressDensity = 0.6f;
    [Tooltip("На сколько метров контур отступает от края Terrain для кнопки «Контур вокруг Terrain»")]
    public float marginAroundTerrain = 3f;

    [Header("Материалы")]
    [Tooltip("Пусто = копия материала старой земли, а если у неё нет Renderer (Terrain), будет зелёный цвет острова")]
    public Material topMaterial;
    [Tooltip("Метров на один повтор текстуры, если topMaterial задан руками")]
    public float topUvMeters = 8f;
    [Tooltip("Пусто = создастся материал на шейдере Project/IslandRock")]
    public Material rockMaterial;
    public bool hideGroundPlane = true;

    [Header("Край как земля (текстура Terrain на кромке и поле)")]
    [Tooltip("Травяная кромка обрыва и поле Apron рисуются той же текстурой, что и Terrain (по мировым координатам), поэтому шва между краем и землёй нет. Выключи, чтобы вернуть однотонную кромку")]
    public bool matchTerrainTexture = true;
    [Tooltip("Своя текстура земли. Пусто = диффузная текстура главного (травяного) слоя Terrain")]
    public Texture2D groundTexture;
    [Tooltip("Метров на один повтор текстуры. 0 = взять размер тайла у слоя Terrain")]
    public float groundTextureMeters = 0f;
    [Tooltip("Подстройка яркости и тона кромки, если она заметно светлее или темнее Terrain (освещение у Terrain и у скалы разное)")]
    public Color groundTint = Color.white;

    [Header("Цвета")]
    public Color grassTop = new Color(0.42f, 0.67f, 0.26f);
    public Color grassDark = new Color(0.30f, 0.52f, 0.20f);
    public Color soil = new Color(0.46f, 0.33f, 0.23f);
    public Color rockWarm = new Color(0.64f, 0.56f, 0.49f);
    public Color rockMid = new Color(0.51f, 0.47f, 0.47f);
    public Color rockDark = new Color(0.36f, 0.34f, 0.41f);
    [Tooltip("Верхний слой юбки: тёплый светло-коричневый")]
    public Color rockLayerA = new Color(0.76f, 0.62f, 0.46f);
    [Tooltip("Средний слой юбки")]
    public Color rockLayerB = new Color(0.62f, 0.49f, 0.38f);
    [Tooltip("Нижний слой юбки: самый тёмный, дальше идёт Rock Dark подбрюшья")]
    public Color rockLayerC = new Color(0.47f, 0.39f, 0.36f);

    [Header("Подбрюшье (скала снизу)")]
    public bool buildUnderside = true;
    [Tooltip("Глубина всего острова от земли до острия, метры (обрыв + скала снизу)")]
    public float depth = 22f;
    [Tooltip("Насколько некоторые колонки скалы свисают ниже соседних")]
    public float undersideSpikes = 3.5f;
    [Tooltip("Корней, свисающих из-под кромки")]
    public int roots = 40;
    [Tooltip("Групп светящихся кристаллов")]
    public int crystals = 14;
    [Tooltip("Парящих камней вокруг")]
    public int debris = 7;

    [Header("Небо и облака (ставится само при сборке)")]
    [Tooltip("Шейдер облаков для CloudSea. Ссылка нужна, чтобы шейдер попал в сборку игры")]
    public Shader cloudShader;

    [Header("Общее")]
    public int seed = 11;

    // Нижнее кольцо обрыва (мировые координаты), от него строится скала снизу
    [HideInInspector] public Vector3[] bottomRing = new Vector3[0];

    public List<Vector2> Outline { get { return GetOutline(); } }
    public float GroundY { get { return groundY; } }

    // Находится ли точка внутри исходного контура острова (плоская проверка, высота игнорируется)
    public bool Contains(Vector3 p)
    {
        return PointInPolygon(Outline, new Vector2(p.x, p.z));
    }

    public Transform EnsureRoot()
    {
        var t = transform.Find(RootName);
        if (t == null)
        {
            var go = new GameObject(RootName);
            go.transform.SetParent(transform, false);
            t = go.transform;
        }
        return t;
    }

    // ---------- контур ----------

    // Сглаженный контур края (x, z) против часовой стрелки. Детерминированный: по нему строится стена прохода.
    public List<Vector2> GetEdgeLoop()
    {
        var src = GetOutline();
        if (src.Count < 3) return src;
        if (SignedArea(src) < 0f) src.Reverse();

        // плавная кривая через твои точки (Catmull-Rom): через точки проходит, углы скругляет
        var dense = new List<Vector2>();
        int n = src.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 p0 = src[(i - 1 + n) % n], p1 = src[i], p2 = src[(i + 1) % n], p3 = src[(i + 2) % n];
            int steps = Mathf.Max(2, Mathf.CeilToInt((p2 - p1).magnitude / 0.5f));
            for (int s = 0; s < steps; s++) dense.Add(CatmullRom(p0, p1, p2, p3, s / (float)steps));
        }

        var loop = Resample(dense, Mathf.Max(0.5f, edgeStep));
        int m = loop.Count;
        float[] gk = GateFade(loop);

        // шум по позиции (не по длине), поэтому шва на стыке круга нет; сдвиг только внутрь
        float[] bay = ComputeBayOffsets(loop);
        var res = new List<Vector2>(m);
        for (int j = 0; j < m; j++)
        {
            Vector2 prev = loop[(j - 1 + m) % m], next = loop[(j + 1) % m];
            Vector2 dir = (next - prev).normalized;
            Vector2 outN = new Vector2(dir.y, -dir.x);
            Vector2 p = loop[j];
            float nz = 0.65f * Mathf.PerlinNoise(p.x * edgeNoiseScale + seed * 1.37f, p.y * edgeNoiseScale + seed * 0.71f)
                     + 0.35f * Mathf.PerlinNoise(p.x * edgeNoiseScale * 2.7f + seed * 2.11f, p.y * edgeNoiseScale * 2.7f + seed * 1.43f);
            res.Add(p + outN * (bay[j] - nz * edgeNoise * gk[j]));   // bay < 0 бухта внутрь, > 0 мыс наружу
        }
        SmoothLoop(res, gk, edgeSmooth);
        return res;
    }

    // Taubin-сглаживание замкнутого контура: шаг к среднему соседей (lambda) и шаг назад (mu), поэтому остров не худеет.
    // Вес gk: у прохода к мосту контур не трогаем. Углы и зубчики от шума и бухт становятся плавными дугами.
    static void SmoothLoop(List<Vector2> pts, float[] gk, int iterations)
    {
        int m = pts.Count;
        if (iterations <= 0 || m < 5) return;
        const float lambda = 0.5f, mu = -0.53f;
        var tmp = new Vector2[m];
        for (int it = 0; it < iterations * 2; it++)
        {
            float f = (it & 1) == 0 ? lambda : mu;
            for (int j = 0; j < m; j++)
            {
                Vector2 avg = (pts[(j - 1 + m) % m] + pts[(j + 1) % m]) * 0.5f;
                tmp[j] = pts[j] + (avg - pts[j]) * f * gk[j];
            }
            for (int j = 0; j < m; j++) pts[j] = tmp[j];
        }
    }

    // ---------- бухты и мысы ----------
    [System.NonSerialized] bool[] protCells;
    [System.NonSerialized] bool protReady;
    [System.NonSerialized] float protX0, protZ0;
    [System.NonSerialized] int protNx, protNz;
    [System.NonSerialized] public int protCellCount, protObjectCount, protSkippedBig;
    const float ProtCell = 1f;

    public void InvalidateProtect() { protReady = false; protCells = null; }
    void OnValidate() { protReady = false; gizmoAt = -10f; }

    Terrain FindTerrainAny()
    {
        if (groundObject != null)
        {
            var t = groundObject.GetComponent<Terrain>();
            if (t == null) t = groundObject.GetComponentInChildren<Terrain>();
            if (t != null) return t;
        }
        var a = Terrain.activeTerrain;
        if (a != null) return a;
        return Object.FindFirstObjectByType<Terrain>(FindObjectsInactive.Include);
    }

    void MarkRect(float x0, float z0, float x1, float z1)
    {
        int i0 = Mathf.Max(0, Mathf.FloorToInt((x0 - protX0) / ProtCell)), i1 = Mathf.Min(protNx - 1, Mathf.FloorToInt((x1 - protX0) / ProtCell));
        int j0 = Mathf.Max(0, Mathf.FloorToInt((z0 - protZ0) / ProtCell)), j1 = Mathf.Min(protNz - 1, Mathf.FloorToInt((z1 - protZ0) / ProtCell));
        for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                int k = j * protNx + i;
                if (!protCells[k]) { protCells[k] = true; protCellCount++; }
            }
    }

    static string[] SplitKeys(string s)
    {
        if (string.IsNullOrEmpty(s)) return new string[0];
        var parts = s.Split(',');
        var res = new List<string>();
        for (int i = 0; i < parts.Length; i++)
        {
            string k = parts[i].Trim().ToLowerInvariant();
            if (k.Length > 0) res.Add(k);
        }
        return res.ToArray();
    }

    static bool HasKey(string name, string[] keys)
    {
        string n = name.ToLowerInvariant();
        for (int i = 0; i < keys.Length; i++) if (n.Contains(keys[i])) return true;
        return false;
    }

    // Карта «сюда бухты не заходят»: слои Terrain (тропы, площадки, река), объекты по имени, точки Keep Clear
    void EnsureProtect()
    {
        if (protReady) return;
        protReady = true;
        protCellCount = 0; protObjectCount = 0; protSkippedBig = 0;
        var pts = GetOutline();
        if (pts.Count < 3) { protCells = null; return; }
        Vector2 mn = pts[0], mx = pts[0];
        foreach (var p in pts) { mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p); }
        float pad = bayDepth + capeLength + 8f;
        mn -= Vector2.one * pad; mx += Vector2.one * pad;
        protX0 = mn.x; protZ0 = mn.y;
        protNx = Mathf.Clamp(Mathf.CeilToInt((mx.x - mn.x) / ProtCell) + 1, 1, 2048);
        protNz = Mathf.Clamp(Mathf.CeilToInt((mx.y - mn.y) / ProtCell) + 1, 1, 2048);
        protCells = new bool[protNx * protNz];

        // 1) слои раскраски Terrain
        var t = FindTerrainAny();
        var layerKeys = SplitKeys(protectLayers);
        if (t != null && t.terrainData != null && layerKeys.Length > 0)
        {
            var td = t.terrainData;
            var layers = td.terrainLayers;
            var hit = new List<int>();
            if (layers != null)
                for (int i = 0; i < layers.Length; i++)
                    if (layers[i] != null && HasKey(layers[i].name, layerKeys)) hit.Add(i);
            if (hit.Count > 0 && td.alphamapLayers == layers.Length)
            {
                int ar = td.alphamapResolution;
                var am = td.GetAlphamaps(0, 0, ar, ar);   // [z, x, слой]
                Vector3 tp = t.GetPosition(), sz = td.size;
                float cx = sz.x / ar, cz = sz.z / ar;
                for (int y = 0; y < ar; y++)
                    for (int x = 0; x < ar; x++)
                    {
                        float w = 0f;
                        for (int h = 0; h < hit.Count; h++) w = Mathf.Max(w, am[y, x, hit[h]]);
                        if (w < 0.35f) continue;
                        float wx = tp.x + (x + 0.5f) * cx, wz = tp.z + (y + 0.5f) * cz;
                        MarkRect(wx - cx * 0.5f, wz - cz * 0.5f, wx + cx * 0.5f, wz + cz * 0.5f);
                    }
            }
        }

        // 2) точки Keep Clear
        if (keepClear != null)
            for (int i = 0; i < keepClear.Length; i++)
            {
                if (keepClear[i] == null) continue;
                Vector3 q = keepClear[i].position;
                MarkRect(q.x - keepClearRadius, q.z - keepClearRadius, q.x + keepClearRadius, q.z + keepClearRadius);
                protObjectCount++;
            }

        // 3) объекты по имени (сам объект или родитель называется как тропа, площадка, река)
        var nameKeys = SplitKeys(protectNames);
        if (nameKeys.Length > 0)
        {
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int a = 0; a < all.Length; a++)
            {
                var tr = all[a];
                if (tr == null || tr.IsChildOf(transform) || tr.GetComponent<Terrain>() != null) continue;
                var r = tr.GetComponent<Renderer>();
                if (r == null) continue;   // контейнеры без меша пропускаем: защищаются их листья
                bool match = false;
                for (var up = tr; up != null; up = up.parent)
                    if (HasKey(up.name, nameKeys)) { match = true; break; }
                if (!match) continue;
                Bounds b = r.bounds;
                if (b.size.x > protectMaxSize || b.size.z > protectMaxSize) { protSkippedBig++; continue; }
                MarkRect(b.min.x, b.min.z, b.max.x, b.max.z);
                protObjectCount++;
            }
        }
    }

    bool IsProtected(Vector2 q)
    {
        if (protCells == null) return false;
        int i = Mathf.FloorToInt((q.x - protX0) / ProtCell), j = Mathf.FloorToInt((q.y - protZ0) / ProtCell);
        if (i < 0 || j < 0 || i >= protNx || j >= protNz) return false;
        return protCells[j * protNx + i];
    }

    // Насколько глубоко можно уйти внутрь от точки p (метры), чтобы не подойти к защищённому ближе protectMargin
    float AllowedInward(Vector2 p, Vector2 outN, float maxDepth)
    {
        for (float s = 0f; s <= maxDepth; s += 0.5f)
            if (IsProtected(p - outN * s)) return Mathf.Max(0f, s - protectMargin);
        return maxDepth;
    }

    // Смещение каждой точки контура вдоль наружной нормали: < 0 бухта, > 0 мыс. Только величина по модулю уменьшается
    // на всех этапах после защиты (эрозия, размытие в пределах эрозии, ограничение крутизны), поэтому защита не нарушается.
    float[] ComputeBayOffsets(List<Vector2> loop)
    {
        int m = loop.Count;
        var d = new float[m];
        if (!bays || m < 8 || (bayDepth <= 0f && capeLength <= 0f)) return d;
        EnsureProtect();
        Vector2? gate = GetGatePosition();

        var outs = new Vector2[m];
        for (int j = 0; j < m; j++)
        {
            Vector2 dir = (loop[(j + 1) % m] - loop[(j - 1 + m) % m]).normalized;
            outs[j] = new Vector2(dir.y, -dir.x);
        }

        var allowed = new float[m];
        for (int j = 0; j < m; j++)
        {
            Vector2 p = loop[j];
            float n1 = Mathf.PerlinNoise(500f + p.x * bayScale + seed * 2.9f, 500f + p.y * bayScale + seed * 1.3f);
            float n2 = Mathf.PerlinNoise(500f + p.x * bayScale * 2.3f + seed * 4.1f, 500f + p.y * bayScale * 2.3f + seed * 0.6f);
            float n = Mathf.Lerp(n1, n2, bayDetail * 0.5f);
            float s = (float)System.Math.Tanh((n - 0.5f) * 2f * bayContrast * 1.6f);
            float gb = 1f;
            if (gate.HasValue)
                gb = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(bayGateClear, bayGateClear + Mathf.Max(0.1f, bayGateFade), Vector2.Distance(p, gate.Value)));
            d[j] = (s > 0f ? s * capeLength : s * bayDepth) * gb;
            allowed[j] = AllowedInward(p, outs[j], Mathf.Max(0.5f, bayDepth));
        }

        // эрозия допуска: бухта в точке j не глубже допуска любой точки в окне ±K (размытие ниже не выйдет за допуск)
        const int K = 3;
        var eroded = new float[m];
        for (int j = 0; j < m; j++)
        {
            float lo = allowed[j];
            for (int k = -K; k <= K; k++) lo = Mathf.Min(lo, allowed[((j + k) % m + m) % m]);
            eroded[j] = lo;
        }
        for (int j = 0; j < m; j++) if (d[j] < 0f) d[j] = -Mathf.Min(-d[j], eroded[j]);

        // два прохода размытия [1 2 1]/4 (радиус 2 <= K)
        var tmp = new float[m];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int j = 0; j < m; j++) tmp[j] = (d[(j - 1 + m) % m] + 2f * d[j] + d[(j + 1) % m]) * 0.25f;
            System.Array.Copy(tmp, d, m);
        }

        // ограничение крутизны (контур не складывается): модуль только убывает
        float maxStep = Mathf.Max(0.3f, edgeStep * 0.85f);
        var pos = new float[m]; var neg = new float[m];
        for (int j = 0; j < m; j++) { pos[j] = Mathf.Max(d[j], 0f); neg[j] = Mathf.Max(-d[j], 0f); }
        for (int pass = 0; pass < 2 * m; pass++)
        {
            int j = pass % m, jp = (j - 1 + m) % m;
            pos[j] = Mathf.Min(pos[j], pos[jp] + maxStep); neg[j] = Mathf.Min(neg[j], neg[jp] + maxStep);
        }
        for (int pass = 0; pass < 2 * m; pass++)
        {
            int j = (m - 1 - pass % m), jn = (j + 1) % m;
            pos[j] = Mathf.Min(pos[j], pos[jn] + maxStep); neg[j] = Mathf.Min(neg[j], neg[jn] + maxStep);
        }
        for (int j = 0; j < m; j++) d[j] = pos[j] - neg[j];
        return d;
    }

    // 0 у прохода к мосту (ровный край), 1 далеко от него
    float[] GateFade(List<Vector2> loop)
    {
        var k = new float[loop.Count];
        var gate = GetGatePosition();
        for (int i = 0; i < k.Length; i++)
        {
            if (!gate.HasValue) { k[i] = 1f; continue; }
            float d = Vector2.Distance(loop[i], gate.Value);
            k[i] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(gateFlatten, gateFlatten * 2f, d));
        }
        return k;
    }

    static float SignedArea(List<Vector2> p)
    {
        float a = 0f;
        for (int i = 0; i < p.Count; i++)
        {
            Vector2 u = p[i], v = p[(i + 1) % p.Count];
            a += u.x * v.y - v.x * u.y;
        }
        return a * 0.5f;
    }

    static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    // Равномерные точки по замкнутой линии
    static List<Vector2> Resample(List<Vector2> pts, float step)
    {
        int n = pts.Count;
        var cum = new float[n + 1];
        float total = 0f;
        for (int i = 0; i < n; i++)
        {
            total += (pts[(i + 1) % n] - pts[i]).magnitude;
            cum[i + 1] = total;
        }
        int m = Mathf.Max(8, Mathf.RoundToInt(total / step));
        var res = new List<Vector2>(m);
        int seg = 0;
        for (int k = 0; k < m; k++)
        {
            float d = total * k / m;
            while (seg < n - 1 && cum[seg + 1] < d) seg++;
            float len = cum[seg + 1] - cum[seg];
            float t = len > 1e-5f ? (d - cum[seg]) / len : 0f;
            res.Add(Vector2.Lerp(pts[seg], pts[(seg + 1) % n], t));
        }
        return res;
    }

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

    // ---------- триангуляция (ear clipping), контур против часовой ----------
    static float Cross2(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

    static bool InsideTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        const float e = 1e-5f;
        return Cross2(b - a, p - a) > e && Cross2(c - b, p - b) > e && Cross2(a - c, p - c) > e;
    }

    static List<int> Triangulate(List<Vector2> v)
    {
        var idx = new List<int>(v.Count);
        for (int i = 0; i < v.Count; i++) idx.Add(i);
        var tris = new List<int>((v.Count - 2) * 3);
        int guard = 0, limit = v.Count * v.Count + 10;

        while (idx.Count > 3 && guard++ < limit)
        {
            bool clipped = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int ia = idx[(i - 1 + idx.Count) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                Vector2 a = v[ia], b = v[ib], c = v[ic];
                if (Cross2(b - a, c - b) <= 1e-6f) continue;   // вогнутая или прямая вершина: ухом не бывает

                bool ear = true;
                for (int k = 0; k < idx.Count; k++)
                {
                    int ik = idx[k];
                    if (ik == ia || ik == ib || ik == ic) continue;
                    if (InsideTri(v[ik], a, b, c)) { ear = false; break; }
                }
                if (!ear) continue;

                tris.Add(ia); tris.Add(ib); tris.Add(ic);
                idx.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped)   // вырожденный случай (почти прямые куски): срезаем как есть, чтобы не зависнуть
            {
                tris.Add(idx[0]); tris.Add(idx[1]); tris.Add(idx[2]);
                idx.RemoveAt(1);
            }
        }
        if (idx.Count == 3) { tris.Add(idx[0]); tris.Add(idx[1]); tris.Add(idx[2]); }
        return tris;
    }

    // ---------- сборка мешей ----------
    static float Noise(float x, float y, int s) { return Mathf.PerlinNoise(x + s * 3.17f, y + s * 1.93f); }

    Mesh BuildTop(List<Vector2> loop, Vector2 uvMin, Vector2 uvSize, bool matchGround)
    {
        float y = GroundY;
        int m = loop.Count;
        var verts = new Vector3[m];
        var uvs = new Vector2[m];
        var cols = new Color[m];
        var norms = new Vector3[m];
        for (int i = 0; i < m; i++)
        {
            verts[i] = new Vector3(loop[i].x, y, loop[i].y);
            norms[i] = Vector3.up;
            cols[i] = grassTop;
            uvs[i] = matchGround
                ? new Vector2((loop[i].x - uvMin.x) / uvSize.x, (loop[i].y - uvMin.y) / uvSize.y)
                : loop[i] / Mathf.Max(0.1f, topUvMeters);
        }

        var tris = Triangulate(loop);
        // лицевая сторона должна смотреть вверх
        if (tris.Count >= 3)
        {
            Vector3 nrm = Vector3.Cross(verts[tris[1]] - verts[tris[0]], verts[tris[2]] - verts[tris[0]]);
            if (nrm.y < 0f)
                for (int i = 0; i < tris.Count; i += 3) { int t = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = t; }
        }

        var mesh = new Mesh { name = "IslandTop" };
        mesh.vertices = verts; mesh.normals = norms; mesh.uv = uvs; mesh.colors = cols;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Точки уступов (полок) на юбке: сюда потом кладётся осыпь
    struct LedgePt { public Vector3 shelfBase, shelfEdge, faceMid; public Vector2 outDir; public float width; }
    readonly List<LedgePt> cliffLedges = new List<LedgePt>();

    static Color Shade(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }

    Mesh BuildCliff(List<Vector2> loop, Terrain terrain, out Vector3[] bottom)
    {
        int m = loop.Count;
        float y0 = GroundY;
        float D = Mathf.Max(4f, cliffDepth), T = Mathf.Max(0f, cliffTuck);
        int L = Mathf.Clamp(rockLayers, 2, 3);
        float inset = terrain != null ? Mathf.Max(lipOverlap, apronCell * 1.25f) : 0f;   // кромка заходит на траву и перекрывает зубчики стыка
        cliffLedges.Clear();

        // ---- описание колец сверху вниз ----
        // dy: глубина от земли (отрицательная); fixedOut: смещение наружу для верхних колец (null = по профилю юбки);
        // lvl: общий номер «уровня» (кольца с одним lvl качаются по высоте одинаково, поэтому полка получается ровной)
        // shelfOf: номер границы слоёв, если это верхнее кольцо слоя ниже полки (иначе -1)
        var rdy = new List<float>(); var rfix = new List<float>(); var rlvl = new List<int>();
        var rcol = new List<Color>(); var rshelf = new List<int>(); var rrough = new List<float>();
        const float NoFix = -999f;
        System.Action<float, float, int, Color, int, float> add = (pDy, pFix, pLvl, pCol, pShelf, pRough) =>
        { rdy.Add(pDy); rfix.Add(pFix); rlvl.Add(pLvl); rcol.Add(pCol); rshelf.Add(pShelf); rrough.Add(pRough); };

        add(0f, -inset, 0, grassTop, -1, 0f);
        add(-0.30f, lipBulge, 1, grassDark, -1, 0f);
        add(-0.75f, lipBulge * 0.3f, 2, soil, -1, 0.3f);

        Color[] lc = L == 3 ? new[] { rockLayerA, rockLayerB, rockLayerC } : new[] { rockLayerA, Color.Lerp(rockLayerB, rockLayerC, 0.5f) };
        float[] wgt = L == 3 ? new[] { 0.34f, 0.33f, 0.33f } : new[] { 0.5f, 0.5f };
        float top = -1f, body = D - 1f, cum = 0f;
        int lvl = 3;
        for (int k = 0; k < L; k++)
        {
            float yT = top - body * cum, yB = top - body * (cum + wgt[k]);
            cum += wgt[k];
            bool last = k == L - 1;
            Color cT = Shade(lc[k], 1.06f), cM = lc[k], cB = last ? Color.Lerp(Shade(lc[k], 0.88f), rockDark, 0.6f) : Shade(lc[k], 0.88f);
            int topLvl = lvl++;
            add(k == 0 ? yT : yT - 0.04f, NoFix, topLvl, cT, k == 0 ? -1 : k - 1, 1f);
            add((yT + yB) * 0.5f, NoFix, lvl++, cM, -1, 1.3f);
            add(yB, NoFix, last ? -1000 : lvl, cB, -1, 1f);   // низ слоя делит lvl с верхом следующего
        }
        int R = rdy.Count;

        // вес «земли» кольца: 0 кольцо = трава (текстура земли), 1 кольцо = бок кромки, 2 = земля, дальше скала
        var rgw = new float[R]; var rgs = new float[R];
        rgw[0] = 1f; rgs[0] = 1f;
        if (R > 1) { rgw[1] = 0.85f; rgs[1] = 0.78f; }

        // ---- наружные нормали, высота кромки ----
        var outs = new Vector2[m];
        for (int j = 0; j < m; j++)
        {
            Vector2 dir = (loop[(j + 1) % m] - loop[(j - 1 + m) % m]).normalized;
            outs[j] = new Vector2(dir.y, -dir.x);
        }
        float[] gk = GateFade(loop);

        var lipY = new float[m];
        for (int j = 0; j < m; j++)
        {
            Vector2 q = loop[j] - outs[j] * inset;
            lipY[j] = terrain != null ? ApronY(terrain, q) - 0.03f : y0;
        }

        var pos = new Vector3[R, m];
        var col = new Color[R, m];
        var shelfW = new float[R, m];
        float thr = 1f - ledgeAmount;
        for (int r = 0; r < R; r++)
        {
            float f = Mathf.Clamp01(-rdy[r] / D);   // доля глубины
            bool lastRing = r == R - 1;
            for (int j = 0; j < m; j++)
            {
                Vector2 p = loop[j];
                int lv = rlvl[r];
                float rough = (Noise(p.x * 0.21f + lv * 13.1f, p.y * 0.21f + lv * 7.7f, seed) - 0.5f) * 2f * cliffRoughness * f * rrough[r] * gk[j];
                float o = rfix[r] != NoFix ? rfix[r] : -T * Mathf.Pow(f, 1.9f);
                if (rfix[r] != NoFix) o += rough * 0.5f; else o += rough;

                // полка: верх слоя ниже границы выдаётся наружу, к своему низу втягивается обратно (террасы)
                float sh = 0f;
                if (rshelf[r] >= 0 && ledgeWidth > 0.01f)
                {
                    float mask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(thr, thr + 0.22f, Noise(p.x * ledgeScale + rshelf[r] * 17.3f, p.y * ledgeScale + 5f, seed + 61)));
                    float wv = 0.55f + 0.45f * Noise(p.x * 0.45f + rshelf[r] * 3f, p.y * 0.45f, seed + 71);
                    sh = ledgeWidth * mask * wv * gk[j];
                    o += sh;
                }
                shelfW[r, j] = sh;

                // верх следует за рельефом, глубже кольца переходят на абсолютную высоту (низ всегда на GroundY - cliffDepth)
                float kAbs = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((-rdy[r] - 0.3f) / (0.5f * D)));
                float yy = Mathf.Lerp(lipY[j] + rdy[r], y0 + rdy[r], kAbs);
                if (rdy[r] < -1f && !lastRing)
                    yy += (Noise(p.x * 0.33f + lv * 5.3f, p.y * 0.33f + lv * 2.1f, seed + 5) - 0.5f) * 2f * strataWobble * 0.5f * gk[j];
                pos[r, j] = new Vector3(p.x + outs[j].x * o, yy, p.y + outs[j].y * o);

                float strata = 1f + (Noise(p.x * 0.5f + lv * 3f, p.y * 0.5f, seed + 9) - 0.5f) * 0.18f;
                Color c = rcol[r] * strata; c.a = 1f;
                col[r, j] = c;
            }
        }

        // ---- уступы для осыпи ----
        for (int r = 1; r < R; r++)
        {
            if (rshelf[r] < 0) continue;
            for (int j = 0; j < m; j += 2)
            {
                if (shelfW[r, j] < ledgeWidth * 0.3f) continue;
                cliffLedges.Add(new LedgePt
                {
                    shelfBase = pos[r - 1, j],
                    shelfEdge = pos[r, j],
                    faceMid = pos[r + 1, j],
                    outDir = outs[j],
                    width = shelfW[r, j]
                });
            }
        }

        // порядок вершин: проверяем на одном четырёхугольнике, наружу ли смотрит лицо
        bool flip = false;
        {
            Vector3 a = pos[1, 0], b = pos[1, 1 % m], c = pos[2, 1 % m];
            Vector3 nrm = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(nrm, new Vector3(outs[0].x, 0f, outs[0].y)) < 0f) flip = true;
        }

        // нормали граней-четырёхугольников; по кольцу усредняем (круглый берег без «ёлочки» граней), по высоте оставляем жёсткими (слои породы)
        float nsign = flip ? -1f : 1f;
        var qn = new Vector3[R - 1, m];
        for (int r = 0; r < R - 1; r++)
            for (int j = 0; j < m; j++)
            {
                int j2 = (j + 1) % m;
                Vector3 n = nsign * Vector3.Cross(pos[r, j2] - pos[r, j], pos[r + 1, j2] - pos[r, j]);
                if (n.sqrMagnitude < 1e-8f) n = new Vector3(outs[j].x, 0f, outs[j].y);
                qn[r, j] = n.normalized;
            }
        var norms = new List<Vector3>(m * (R - 1) * 4);

        var verts = new List<Vector3>(m * (R - 1) * 4);
        var colors = new List<Color>(m * (R - 1) * 4);
        var cuvs = new List<Vector2>(m * (R - 1) * 4);
        var tris = new List<int>(m * (R - 1) * 6);
        for (int j = 0; j < m; j++)
        {
            int j2 = (j + 1) % m;
            for (int r = 0; r < R - 1; r++)
            {
                // квад между низом слоя и верхом слоя ниже = плоская полка: свой светлый цвет, а не градиент
                bool ledgeQuad = rshelf[r + 1] >= 0;
                Color c00 = col[r, j], c01 = col[r, j2], c11 = col[r + 1, j2], c10 = col[r + 1, j];
                if (ledgeQuad)
                {
                    c10 = Shade(c10, 1.10f); c11 = Shade(c11, 1.10f);
                    c00 = Color.Lerp(c00, c10, 0.7f); c01 = Color.Lerp(c01, c11, 0.7f);
                }
                int b0 = verts.Count;
                verts.Add(pos[r, j]); verts.Add(pos[r, j2]); verts.Add(pos[r + 1, j2]); verts.Add(pos[r + 1, j]);
                Vector3 nPrev = (qn[r, j] + qn[r, (j - 1 + m) % m]).normalized, nNext = (qn[r, j] + qn[r, j2]).normalized;
                norms.Add(nPrev); norms.Add(nNext); norms.Add(nNext); norms.Add(nPrev);
                colors.Add(c00); colors.Add(c01); colors.Add(c11); colors.Add(c10);
                Vector2 uA = new Vector2(rgw[r], rgs[r]), uB = new Vector2(rgw[r + 1], rgs[r + 1]);
                cuvs.Add(uA); cuvs.Add(uA); cuvs.Add(uB); cuvs.Add(uB);
                if (!flip) { tris.Add(b0); tris.Add(b0 + 1); tris.Add(b0 + 2); tris.Add(b0); tris.Add(b0 + 2); tris.Add(b0 + 3); }
                else { tris.Add(b0); tris.Add(b0 + 2); tris.Add(b0 + 1); tris.Add(b0); tris.Add(b0 + 3); tris.Add(b0 + 2); }
            }
        }

        bottom = new Vector3[m];
        for (int j = 0; j < m; j++) bottom[j] = pos[R - 1, j];

        // временная крышка снизу (подбрюшье задачи 3 её заменит)
        if (capBottom && !buildUnderside)
        {
            var flat = new List<Vector2>(m);
            for (int j = 0; j < m; j++) flat.Add(new Vector2(bottom[j].x, bottom[j].z));
            var ct = Triangulate(flat);
            int baseIdx = verts.Count;
            Color capCol = rockDark * 0.8f; capCol.a = 1f;
            for (int j = 0; j < m; j++) { verts.Add(bottom[j]); colors.Add(capCol); cuvs.Add(Vector2.zero); norms.Add(Vector3.down); }
            if (ct.Count >= 3)
            {
                Vector3 nrm = Vector3.Cross(bottom[ct[1]] - bottom[ct[0]], bottom[ct[2]] - bottom[ct[0]]);
                bool down = nrm.y < 0f;
                for (int i = 0; i < ct.Count; i += 3)
                {
                    tris.Add(baseIdx + ct[i]);
                    tris.Add(baseIdx + ct[down ? i + 1 : i + 2]);
                    tris.Add(baseIdx + ct[down ? i + 2 : i + 1]);
                }
            }
        }

        var mesh = new Mesh { name = "IslandCliff" };
        if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetUVs(0, cuvs);   // x = вес текстуры земли, y = затемнение (шейдер IslandRock)
        mesh.SetTriangles(tris, 0);
        mesh.SetNormals(norms);   // по кольцу сглажены, по высоте жёсткие: слои породы читаются, берег не гранёный
        mesh.RecalculateBounds();
        return mesh;
    }

    // Осыпь: камни лежат на полках и сыплются по скале под ними. Один меш без коллайдеров
    Mesh BuildScree()
    {
        var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
        if (screeDensity <= 0.01f || cliffLedges.Count == 0) return IslandUnderside.ToMesh("IslandScree", v, c, t);
        var rnd = new System.Random(seed * 23 + 11);
        System.Func<float> R01 = () => (float)rnd.NextDouble();
        int rocks = 0;
        float p = Mathf.Clamp01(screeDensity * 0.5f);   // доля уступов с осыпью
        for (int i = 0; i < cliffLedges.Count && rocks < screeMaxRocks; i++)
        {
            var lp = cliffLedges[i];
            if (R01() > p) continue;
            Vector3 outD = new Vector3(lp.outDir.x, 0f, lp.outDir.y);
            Vector3 tang = new Vector3(-outD.z, 0f, outD.x);
            // на полке
            int onShelf = 2 + (int)(R01() * 3f * screeDensity);
            for (int k = 0; k < onShelf; k++)
            {
                Vector3 g = Vector3.Lerp(lp.shelfBase, lp.shelfEdge, 0.25f + R01() * 0.6f) + tang * ((R01() - 0.5f) * 2.2f);
                AddScreeRock(v, c, t, g, 0.16f + R01() * 0.34f, R01, true);
                rocks++;
            }
            // язык осыпи вниз по скале: мельче и реже
            int tongue = 2 + (int)(R01() * 4f * screeDensity);
            for (int k = 0; k < tongue; k++)
            {
                float u = (k + 1f) / (tongue + 1f);
                Vector3 g = Vector3.Lerp(lp.shelfEdge, lp.faceMid, u * 0.9f) + outD * 0.12f + tang * ((R01() - 0.5f) * (0.6f + u * 1.4f));
                AddScreeRock(v, c, t, g, (0.22f - u * 0.08f) * (0.7f + R01() * 0.7f), R01, false);
                rocks++;
            }
        }
        return IslandUnderside.ToMesh("IslandScree", v, c, t);
    }

    // Низкополигональный камень: октаэдр с разбросом вершин, чуть приплюснутый и утопленный в поверхность
    void AddScreeRock(List<Vector3> v, List<Color> c, List<int> t, Vector3 g, float size, System.Func<float> R01, bool onShelf)
    {
        Vector3 mid = g + Vector3.up * size * 0.25f;
        float J() { return 0.7f + R01() * 0.6f; }
        Vector3[] q =
        {
            mid + Vector3.right * size * J(), mid - Vector3.right * size * J(),
            mid + Vector3.up * size * 0.7f * J(), mid - Vector3.up * size * 0.45f,
            mid + Vector3.forward * size * J(), mid - Vector3.forward * size * J()
        };
        int[,] f = { { 0, 2, 4 }, { 4, 2, 1 }, { 1, 2, 5 }, { 5, 2, 0 }, { 0, 3, 4 }, { 4, 3, 1 }, { 1, 3, 5 }, { 5, 3, 0 } };
        Color baseC = Color.Lerp(rockLayerA, rockWarm, R01());
        baseC = Shade(baseC, (onShelf ? 1.0f : 0.9f) * (0.88f + R01() * 0.24f));
        for (int i = 0; i < 8; i++)
        {
            Color fc = Shade(baseC, i < 4 ? 1.05f : 0.82f);
            IslandUnderside.Face(v, c, t, q[f[i, 0]], q[f[i, 1]], q[f[i, 2]], fc, fc, fc, mid);
        }
    }

    // Высота поверхности Terrain в мировых координатах
    static float SurfaceY(Terrain t, Vector2 xz)
    {
        Vector3 tp = t.GetPosition(), sz = t.terrainData.size;
        float x = Mathf.Clamp(xz.x, tp.x, tp.x + sz.x), z = Mathf.Clamp(xz.y, tp.z, tp.z + sz.z);   // за краем Terrain берём высоту края
        return t.SampleHeight(new Vector3(x, 0f, z)) + tp.y;
    }

    // Высота поля/кромки в точке: высота края Terrain, под самим Terrain чуть ниже, наружу плавно опускается на apronRoll
    float ApronY(Terrain t, Vector2 q)
    {
        Vector3 tp = t.GetPosition(), sz = t.terrainData.size;
        float d = Mathf.Max(Mathf.Max(tp.x - q.x, q.x - (tp.x + sz.x)), Mathf.Max(tp.z - q.y, q.y - (tp.z + sz.z)));   // >0 снаружи Terrain
        float y = SurfaceY(t, q) - 0.05f * Mathf.Clamp01(1f - d / 0.4f);
        if (buildApron && d > 0f)
        {
            // естественные холмики: шум разного размера, растёт от нуля у края Terrain (стык без ступеньки)
            float w = Mathf.SmoothStep(0f, 1f, d / Mathf.Max(0.5f, apronHillRamp));
            float sc = Mathf.Max(0.01f, apronHillScale);
            float n1 = Mathf.PerlinNoise(q.x * sc + seed * 3.1f, q.y * sc + seed * 1.7f) * 2f - 1f;
            float n2 = Mathf.PerlinNoise(q.x * sc * 2.6f + seed * 5.3f, q.y * sc * 2.6f + seed * 0.9f) * 2f - 1f;
            y += (n1 * 0.75f + n2 * 0.25f) * apronHillHeight * w;
            y -= apronRoll * Mathf.SmoothStep(0f, 1f, d / Mathf.Max(0.5f, apronRollLength));
        }
        return y;
    }

    static float OutsideDist(Terrain t, Vector2 q)
    {
        Vector3 tp = t.GetPosition(), sz = t.terrainData.size;
        return Mathf.Max(Mathf.Max(tp.x - q.x, q.x - (tp.x + sz.x)), Mathf.Max(tp.z - q.y, q.y - (tp.z + sz.z)));
    }

    // Высота земли в точке: Terrain внутри, поле снаружи
    float GroundAt(Terrain t, Vector2 q)
    {
        return OutsideDist(t, q) <= 0f ? SurfaceY(t, q) : ApronY(t, q);
    }

    static Color Tone(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }

    // Камни, кусты и редкие ёлки вдоль края одним мешем. Кластерами, с пустыми местами, ворота не заставляем
    Mesh BuildDressing(List<Vector2> loop, Terrain terrain)
    {
        var rnd = new System.Random(seed * 17 + 3);
        System.Func<float> R = () => (float)rnd.NextDouble();
        var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
        int m = loop.Count;
        float step = Mathf.Max(1.5f, 4.5f - 3f * dressDensity);
        Vector2? gate = GetGatePosition();
        Color bushC = new Color(0.27f, 0.55f, 0.22f), pineC = new Color(0.18f, 0.42f, 0.24f);

        float acc = 0f, next = 0f;
        for (int j = 0; j < m; j++)
        {
            acc += (loop[(j + 1) % m] - loop[j]).magnitude;
            if (acc < next) continue;
            next = acc + step * (0.6f + R() * 0.8f);

            Vector2 p = loop[j];
            if (Mathf.PerlinNoise(p.x * 0.05f + seed * 2.3f, p.y * 0.05f + seed * 0.4f) < 0.42f) continue;   // пустые места между кластерами
            if (gate.HasValue && (p - gate.Value).magnitude < gateWidth * 0.5f + 3f) continue;       // проход к мосту свободен

            Vector2 dir = (loop[(j + 1) % m] - loop[(j - 1 + m) % m]).normalized;
            Vector2 outN = new Vector2(dir.y, -dir.x);
            Vector2 q = p - outN * (0.9f + R() * 6f);
            if (OutsideDist(terrain, q) < -1.5f) continue;   // вглубь Terrain (тропы, лес) не заходим
            Vector3 g = new Vector3(q.x, GroundAt(terrain, q) - 0.05f, q.y);

            float kind = R();
            if (kind < 0.5f)
            {
                int cnt = 1 + (int)(R() * 3f);
                for (int i = 0; i < cnt; i++)
                {
                    Vector3 o = g + new Vector3((R() - 0.5f) * 1.6f, 0f, (R() - 0.5f) * 1.6f);
                    float sz = 0.35f + R() * 0.75f;
                    Color rc = Tone(Color.Lerp(rockWarm, rockMid, R()), 0.9f + R() * 0.2f);
                    Vector3 mid = o + Vector3.up * sz * 0.35f;
                    var cr = new Vector3[8];
                    for (int k = 0; k < 8; k++)
                    {
                        float sx = (k & 1) == 0 ? -1f : 1f, sy = (k & 2) == 0 ? -1f : 1f, sz2 = (k & 4) == 0 ? -1f : 1f;
                        float jt = 0.7f + R() * 0.4f;
                        cr[k] = mid + new Vector3(sx * sz * jt, sy * sz * 0.55f * jt, sz2 * sz * jt);
                    }
                    int[,] fc = { { 1, 3, 7, 5 }, { 0, 2, 6, 4 }, { 2, 3, 7, 6 }, { 0, 1, 5, 4 }, { 4, 5, 7, 6 }, { 0, 1, 3, 2 } };
                    for (int f = 0; f < 6; f++)
                    {
                        IslandUnderside.Face(v, c, t, cr[fc[f, 0]], cr[fc[f, 1]], cr[fc[f, 2]], rc, rc, rc, mid);
                        IslandUnderside.Face(v, c, t, cr[fc[f, 0]], cr[fc[f, 2]], cr[fc[f, 3]], rc, rc, rc, mid);
                    }
                }
            }
            else if (kind < 0.8f)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector3 o = g + new Vector3((R() - 0.5f) * 1.2f, 0f, (R() - 0.5f) * 1.2f);
                    float r0 = 0.45f + R() * 0.4f;
                    Vector3 mid = o + Vector3.up * r0 * 0.6f;
                    Color bc = Tone(bushC, 0.85f + R() * 0.25f);
                    Vector3 top = mid + Vector3.up * r0 * 0.8f, bot = mid - Vector3.up * r0 * 0.5f;
                    var eq = new Vector3[4];
                    for (int k = 0; k < 4; k++)
                    {
                        float a = k * Mathf.PI * 0.5f + R() * 0.4f;
                        eq[k] = mid + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r0 * (0.8f + R() * 0.4f);
                    }
                    for (int k = 0; k < 4; k++)
                    {
                        IslandUnderside.Face(v, c, t, top, eq[k], eq[(k + 1) % 4], bc, bc, bc, mid);
                        IslandUnderside.Face(v, c, t, bot, eq[k], eq[(k + 1) % 4], Tone(bc, 0.8f), Tone(bc, 0.8f), Tone(bc, 0.8f), mid);
                    }
                }
            }
            else
            {
                float h = 1.8f + R() * 1.4f, rr = 0.5f + R() * 0.3f;
                for (int layer = 0; layer < 2; layer++)
                {
                    Vector3 bp = g + Vector3.up * (layer == 0 ? 0f : h * 0.45f);
                    float rl = layer == 0 ? rr : rr * 0.75f, hl = layer == 0 ? h : h * 0.75f;
                    Color pc = Tone(pineC, layer == 0 ? 1f : 1.1f);
                    Vector3 tip = bp + Vector3.up * hl, ins = bp + Vector3.up * (hl * 0.4f);
                    for (int k = 0; k < 6; k++)
                    {
                        float a0 = k * Mathf.PI * 2f / 6f, a1 = (k + 1) * Mathf.PI * 2f / 6f;
                        Vector3 p0 = bp + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * rl;
                        Vector3 p1 = bp + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * rl;
                        IslandUnderside.Face(v, c, t, p0, p1, tip, pc, pc, pc, ins);
                        IslandUnderside.Face(v, c, t, p0, p1, bp, Tone(pc, 0.8f), Tone(pc, 0.8f), Tone(pc, 0.8f), ins);
                    }
                }
            }
        }
        return IslandUnderside.ToMesh("IslandEdgeDecor", v, c, t);
    }

    // Поле: сетка по контуру острова без той части, что целиком закрыта Terrain
    Mesh BuildApron(List<Vector2> loop, Terrain t, float uvMeters)
    {
        Vector3 tp = t.GetPosition(), sz = t.terrainData.size;
        float cell = Mathf.Max(0.3f, apronCell);
        Vector2 mn = loop[0], mx = loop[0];
        foreach (var p in loop) { mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p); }
        int nx = Mathf.CeilToInt((mx.x - mn.x) / cell) + 1, nz = Mathf.CeilToInt((mx.y - mn.y) / cell) + 1;

        // углы клеток: внутри контура ли, и лежат ли они под Terrain (с запасом 0.5 м от его края)
        var inPoly = new bool[nx + 1, nz + 1];
        var covered = new bool[nx + 1, nz + 1];
        for (int i = 0; i <= nx; i++)
            for (int j = 0; j <= nz; j++)
            {
                var q = new Vector2(mn.x + i * cell, mn.y + j * cell);
                inPoly[i, j] = PointInPolygon(loop, q);
                covered[i, j] = q.x > tp.x + 0.5f && q.x < tp.x + sz.x - 0.5f && q.y > tp.z + 0.5f && q.y < tp.z + sz.z - 0.5f;
            }

        var idx = new int[nx + 1, nz + 1];
        for (int i = 0; i <= nx; i++) for (int j = 0; j <= nz; j++) idx[i, j] = -1;
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var cols = new List<Color>(); var tris = new List<int>();

        System.Func<int, int, int> V = (i, j) =>
        {
            if (idx[i, j] >= 0) return idx[i, j];
            var q = new Vector2(mn.x + i * cell, mn.y + j * cell);
            float y = ApronY(t, q);   // под Terrain на 5 см, чтобы не мигало; наружу выходит на его высоту и плавно опускается
            verts.Add(new Vector3(q.x, y, q.y));
            uvs.Add(q / Mathf.Max(0.1f, uvMeters));
            cols.Add(grassTop);
            idx[i, j] = verts.Count - 1;
            return idx[i, j];
        };

        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                if (!(inPoly[i, j] && inPoly[i + 1, j] && inPoly[i, j + 1] && inPoly[i + 1, j + 1])) continue;
                if (covered[i, j] && covered[i + 1, j] && covered[i, j + 1] && covered[i + 1, j + 1]) continue;
                int a = V(i, j), b = V(i, j + 1), c = V(i + 1, j + 1), d2 = V(i + 1, j);
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(d2);
            }

        var mesh = new Mesh { name = "IslandApron" };
        if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------- текстура земли для кромки и поля ----------
    // Главный слой Terrain: травяной (по имени), иначе тот, чьих весов в карте раскраски больше всего
    static int DominantLayer(Terrain t)
    {
        var td = t.terrainData;
        var layers = td.terrainLayers;
        int n = layers != null ? layers.Length : 0;
        if (n <= 1) return 0;

        var sum = new float[n];
        int w = td.alphamapWidth, h = td.alphamapHeight;
        int sx = Mathf.Max(1, w / 48), sz = Mathf.Max(1, h / 48);
        for (int y = 0; y < h; y += sz)
            for (int x = 0; x < w; x += sx)
            {
                var a = td.GetAlphamaps(x, y, 1, 1);
                for (int l = 0; l < n && l < a.GetLength(2); l++) sum[l] += a[0, 0, l];
            }

        // Выбор слоя для поля вокруг Terrain. Раньше брался самый частый слой, из-за чего поле выходило серо-бежевым (тропа или земля),
        // а у Terrain трава. Теперь оценка = доля слоя × «зелёность» его текстуры; по имени трава усиливается, тропа/земля/камень/песок гасятся.
        int bestIdx = 0;
        float bestScore = -1f;
        var sb = new System.Text.StringBuilder("FloatingIsland: слои Terrain для поля:\n");
        for (int l = 0; l < n; l++)
        {
            if (layers[l] == null) continue;
            string nm = layers[l].name.ToLowerInvariant();
            bool grass = nm.Contains("grass") || nm.Contains("трав") || nm.Contains("gras") || nm.Contains("meadow") || nm.Contains("луг");
            bool bare = nm.Contains("dirt") || nm.Contains("path") || nm.Contains("trail") || nm.Contains("sand") || nm.Contains("rock") || nm.Contains("stone")
                     || nm.Contains("mud") || nm.Contains("gravel") || nm.Contains("road") || nm.Contains("тропа") || nm.Contains("земл") || nm.Contains("камен")
                     || nm.Contains("песок") || nm.Contains("грязь");
            float green = 0f;
            var tx = layers[l].diffuseTexture;
            if (tx != null)
            {
                Color ac = AverageColor(tx);
                green = ac.g - Mathf.Max(ac.r, ac.b);
            }
            float score = sum[l] * Mathf.Clamp01(0.2f + green * 8f) * (grass ? 3f : 1f) * (bare ? 0.2f : 1f);
            sb.AppendLine("  [" + l + "] " + layers[l].name + ": доля " + sum[l].ToString("0") + ", зелёность " + green.ToString("0.00") + ", оценка " + score.ToString("0.0"));
            if (score > bestScore) { bestScore = score; bestIdx = l; }
        }
        Debug.Log(sb.ToString() + "Выбран слой: " + (layers[bestIdx] != null ? layers[bestIdx].name : "?") + ". Не тот? Положи нужную текстуру в поле Ground Texture.");
        return bestIdx;
    }

    // Средний цвет текстуры (через уменьшенную копию: работает и с нечитаемыми текстурами)
    static Color AverageColor(Texture tex)
    {
        const int N = 8;
        var rt = RenderTexture.GetTemporary(N, N, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;
        Graphics.Blit(tex, rt);
        RenderTexture.active = rt;
        var t2 = new Texture2D(N, N, TextureFormat.RGBA32, false);
        t2.ReadPixels(new Rect(0, 0, N, N), 0, 0);
        t2.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        var px = t2.GetPixels();
        Object.DestroyImmediate(t2);
        Color c = Color.black;
        for (int i = 0; i < px.Length; i++) c += px[i];
        return c / Mathf.Max(1, px.Length);
    }

    bool ResolveGroundTexture(Terrain t, out Texture tex, out float meters)
    {
        tex = groundTexture; meters = groundTextureMeters;
        if (tex == null && t != null && t.terrainData != null)
        {
            var layers = t.terrainData.terrainLayers;
            int i = DominantLayer(t);
            if (layers != null && i >= 0 && i < layers.Length && layers[i] != null)
            {
                tex = layers[i].diffuseTexture;
                if (meters <= 0f) meters = Mathf.Max(0.5f, layers[i].tileSize.x);
            }
        }
        if (meters <= 0f) meters = Mathf.Max(0.5f, topUvMeters);
        return tex != null;
    }

    static void SetGround(Material m, Texture tex, float meters, Color tint, float all)
    {
        if (m == null) return;
        if (!m.HasProperty("_GroundTex"))
        {
            Debug.LogWarning("FloatingIsland: у материала «" + m.name + "» нет свойства _GroundTex. Обнови IslandRock.shader (положи новую версию рядом со скриптом).");
            return;
        }
        m.SetTexture("_GroundTex", tex);
        m.SetFloat("_GroundMeters", meters);
        m.SetColor("_GroundTint", tint);
        m.SetFloat("_AllGround", all);
    }

#if UNITY_EDITOR
    // Земля: поле groundObject -> Terrain (он главный, на нём холмы и тропы) -> Ground_Auto -> null
    GameObject FindGround(out Terrain terrain)
    {
        terrain = null;
        if (groundObject != null)
        {
            terrain = groundObject.GetComponent<Terrain>();
            if (terrain == null) terrain = groundObject.GetComponentInChildren<Terrain>();
            return groundObject;
        }
        terrain = Terrain.activeTerrain;
        if (terrain == null) terrain = Object.FindFirstObjectByType<Terrain>(FindObjectsInactive.Include);
        if (terrain != null) return terrain.gameObject;
        return GameObject.Find("Ground_Auto");
    }

    // Дырки в Terrain: остаются только клетки, целиком лежащие внутри контура. Холмы, река и раскраска троп не трогаются.
    static void CutTerrain(Terrain t, List<Vector2> loop)
    {
        var td = t.terrainData;
        int M = td.holesResolution;
        Vector3 tp = t.GetPosition();
        float dx = td.size.x / M, dz = td.size.z / M;

        var inside = new bool[M + 1, M + 1];   // углы клеток
        for (int j = 0; j <= M; j++)
            for (int i = 0; i <= M; i++)
                inside[j, i] = PointInPolygon(loop, new Vector2(tp.x + i * dx, tp.z + j * dz));

        var keep = new bool[M, M];   // true = земля есть, false = дырка
        for (int j = 0; j < M; j++)
            for (int i = 0; i < M; i++)
                keep[j, i] = inside[j, i] && inside[j, i + 1] && inside[j + 1, i] && inside[j + 1, i + 1];

        td.SetHoles(0, 0, keep);
        EditorUtility.SetDirty(td);
    }

    // Насколько далеко (метры) точка контура вылезает за прямоугольник Terrain; 0, если весь контур внутри
    static float OutlineOutsideTerrain(Terrain t, List<Vector2> loop)
    {
        Vector3 tp = t.GetPosition(), sz = t.terrainData.size;
        float worst = 0f;
        for (int i = 0; i < loop.Count; i++)
        {
            float ox = Mathf.Max(tp.x - loop[i].x, loop[i].x - (tp.x + sz.x));
            float oz = Mathf.Max(tp.z - loop[i].y, loop[i].y - (tp.z + sz.z));
            worst = Mathf.Max(worst, Mathf.Max(ox, oz));
        }
        return worst;
    }

    // Втягивает точки контура (PathPoints, GatePoint) внутрь Terrain с отступом. Форму потом поправь руками
    public void FitOutlineToTerrain()
    {
        Terrain t; FindGround(out t);
        if (t == null) { Debug.LogWarning("FloatingIsland: Terrain не найден, вписывать не во что."); return; }
        Vector3 tp = t.GetPosition(), sz = t.terrainData.size;
        const float margin = 2f;
        float x0 = tp.x + margin, x1 = tp.x + sz.x - margin, z0 = tp.z + margin, z1 = tp.z + sz.z - margin;

        var pr = transform.Find("PathPoints");
        if (pr != null && pr.childCount >= 3)
        {
            for (int i = 0; i < pr.childCount; i++)
            {
                var c = pr.GetChild(i);
                Undo.RecordObject(c, "Fit island outline");
                Vector3 p = c.position;
                p.x = Mathf.Clamp(p.x, x0, x1); p.z = Mathf.Clamp(p.z, z0, z1);
                c.position = p;
            }
            var gp = transform.Find("GatePoint");
            if (gp != null)
            {
                Undo.RecordObject(gp, "Fit island outline");
                Vector3 p = gp.position;
                p.x = Mathf.Clamp(p.x, x0, x1); p.z = Mathf.Clamp(p.z, z0, z1);
                gp.position = p;
            }
        }
        else
        {
            Undo.RecordObject(this, "Fit island outline");
            center = new Vector2((x0 + x1) * 0.5f, (z0 + z1) * 0.5f);
            halfSize = new Vector2((x1 - x0) * 0.5f, (z1 - z0) * 0.5f);
        }
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("FloatingIsland: контур вписан в Terrain с отступом " + margin + " м. Теперь жми «Собрать остров».");
    }

    // Настройки для «естественного края»: широкое поле с холмиками, неровный контур, декор
    public void ApplyNaturalEdgePreset()
    {
        Undo.RecordObject(this, "Natural edge preset");
        marginAroundTerrain = 8f;
        apronRoll = 2.5f;
        apronRollLength = 5f;
        apronHillHeight = 2.2f;
        apronHillScale = 0.07f;
        apronHillRamp = 3f;
        edgeNoise = 3.2f;
        lipOverlap = 0.8f;
        dressEdge = true;
        dressDensity = 0.6f;
        EditorUtility.SetDirty(this);
        Debug.Log("FloatingIsland: пресет «естественный край» применён. Дальше «Контур вокруг Terrain» и «Собрать остров».");
    }

    // Заменяет контур на скруглённый прямоугольник вокруг Terrain, шире него на marginAroundTerrain метров.
    // Твои прежние точки удаляются (Ctrl+Z вернёт). Проход к мосту (GatePoint) переезжает на ближайшую сторону.
    public void OutlineAroundTerrain()
    {
        Terrain t; FindGround(out t);
        if (t == null) { Debug.LogWarning("FloatingIsland: Terrain не найден."); return; }
        Vector3 tp = t.GetPosition(), sz = t.terrainData.size;
        float m = marginAroundTerrain;
        float x0 = tp.x - m, x1 = tp.x + sz.x + m, z0 = tp.z - m, z1 = tp.z + sz.z + m;
        float mx = (x0 + x1) * 0.5f, mz = (z0 + z1) * 0.5f;
        float r = Mathf.Min(8f, Mathf.Min(x1 - x0, z1 - z0) * 0.15f);

        var pr = transform.Find("PathPoints");
        if (pr == null)
        {
            var go = new GameObject("PathPoints");
            Undo.RegisterCreatedObjectUndo(go, "Outline around terrain");
            go.transform.SetParent(transform, false);
            pr = go.transform;
        }
        for (int i = pr.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(pr.GetChild(i).gameObject);

        Vector2[] v =
        {
            new Vector2(x0 + r, z0), new Vector2(mx, z0), new Vector2(x1 - r, z0),
            new Vector2(x1, z0 + r), new Vector2(x1, mz), new Vector2(x1, z1 - r),
            new Vector2(x1 - r, z1), new Vector2(mx, z1), new Vector2(x0 + r, z1),
            new Vector2(x0, z1 - r), new Vector2(x0, mz), new Vector2(x0, z0 + r)
        };
        for (int i = 0; i < v.Length; i++)
        {
            var go = new GameObject("P" + i);
            Undo.RegisterCreatedObjectUndo(go, "Outline around terrain");
            go.transform.SetParent(pr, true);
            go.transform.position = new Vector3(v[i].x, GroundY, v[i].y);
        }

        // ровный прямоугольник выглядит как «обрез»: делаем край неровным, но не глубже отступа от Terrain
        Undo.RecordObject(this, "Outline around terrain");
        edgeNoise = Mathf.Max(edgeNoise, Mathf.Min(2.2f, m * 0.7f));
        EditorUtility.SetDirty(this);

        var gp = transform.Find("GatePoint");
        if (gp != null)
        {
            Undo.RecordObject(gp, "Outline around terrain");
            Vector3 p = gp.position;
            float dW = Mathf.Abs(p.x - x0), dE = Mathf.Abs(p.x - x1), dS = Mathf.Abs(p.z - z0), dN = Mathf.Abs(p.z - z1);
            float best = Mathf.Min(Mathf.Min(dW, dE), Mathf.Min(dS, dN));
            p.x = Mathf.Clamp(p.x, x0 + r, x1 - r); p.z = Mathf.Clamp(p.z, z0 + r, z1 - r);
            if (best == dW) p.x = x0; else if (best == dE) p.x = x1; else if (best == dS) p.z = z0; else p.z = z1;
            gp.position = p;
        }
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("FloatingIsland: контур теперь вокруг Terrain (+" + m + " м). Жми «Собрать остров».");
    }

    // Что осталось над пустотой: объекты и деревья Terrain, которые лежали внутри исходного контура, а теперь вне края (бухта их срезала)
    void ReportOrphans(List<Vector2> loop, Terrain t)
    {
        if (!bays) return;
        var src = GetOutline();
        if (src.Count < 3) return;
        int trees = 0;
        if (t != null && t.terrainData != null)
        {
            Vector3 tp = t.GetPosition(), sz = t.terrainData.size;
            foreach (var ti in t.terrainData.treeInstances)
            {
                var q = new Vector2(tp.x + ti.position.x * sz.x, tp.z + ti.position.z * sz.z);
                if (PointInPolygon(src, q) && !PointInPolygon(loop, q)) trees++;
            }
        }
        var names = new List<string>();
        int objs = 0;
        var all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var r in all)
        {
            if (r == null || r.transform.IsChildOf(transform) || r.GetComponentInParent<Terrain>() != null) continue;
            Vector3 w = r.transform.position;
            var q = new Vector2(w.x, w.z);
            if (!PointInPolygon(src, q) || PointInPolygon(loop, q)) continue;
            objs++;
            if (names.Count < 12) names.Add(r.name);
        }
        if (trees + objs > 0)
            Debug.LogWarning("FloatingIsland: бухты оставили за краем " + objs + " объектов и " + trees + " деревьев Terrain (они будут висеть над пустотой)."
                + (names.Count > 0 ? " Объекты: " + string.Join(", ", names) + (objs > names.Count ? "…" : "") : "")
                + " Добавь их в Keep Clear / Protect Names или убери вручную.");
    }

    // Настройки под референс: бухты и мысы, толстая слоистая юбка с уступами и осыпью
    public void ApplyReferenceShapePreset()
    {
        Undo.RecordObject(this, "Reference shape preset");
        bays = true; bayDepth = 6f; capeLength = 4f; bayScale = 0.03f; bayDetail = 0.3f; bayContrast = 1f;
        cliffDepth = 10f; cliffTuck = 3f; cliffRoughness = 1.1f;
        rockLayers = 3; ledgeWidth = 1.6f; ledgeAmount = 0.55f; strataWobble = 0.9f; screeDensity = 0.6f;
        depth = Mathf.Max(depth, cliffDepth + 16f);   // подбрюшье не должно стать короче, чем было
        EditorUtility.SetDirty(this);
        Debug.Log("FloatingIsland: пресет «силуэт как на референсе» применён. Жми «Собрать остров».");
    }
    // Задача 1: отвесный обрыв, 3 тона породы по вертикали, узкие уступы. Ровную кромку даёт рельеф (rimFlatWidth)
    public void ApplyCliffPreset()
    {
        Undo.RecordObject(this, "Cliff preset");
        cliffDepth = 10f; cliffTuck = 0.8f; cliffRoughness = 0.7f; lipOverlap = 0.6f; lipBulge = 0.15f;
        rockLayers = 3; ledgeWidth = 1f; ledgeAmount = 0.35f; ledgeScale = 0.06f; strataWobble = 0.7f; screeDensity = 0.5f;
        depth = Mathf.Max(depth, cliffDepth + 16f);   // подбрюшье не короче прежнего
        EditorUtility.SetDirty(this);
        Debug.Log("FloatingIsland: пресет «отвесный обрыв» применён. Жми «Собрать остров».");
    }
    static string GetPath(Transform t)
    {
        string n = t.name;
        while (t.parent != null) { t = t.parent; n = t.name + "/" + n; }
        return n;
    }

    // Печатает в Console всё, что нужно, чтобы понять лаги и «пустоту» у края
    public void Diagnose()
    {
        var sb = new System.Text.StringBuilder("FloatingIsland: диагностика\n");
        Terrain t; FindGround(out t);
        var loop = GetEdgeLoop();
        Vector2 mn = loop.Count > 0 ? loop[0] : Vector2.zero, mx = mn;
        foreach (var p in loop) { mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p); }
        sb.AppendLine("Контур: " + loop.Count + " точек, x " + mn.x.ToString("0.#") + ".." + mx.x.ToString("0.#") + ", z " + mn.y.ToString("0.#") + ".." + mx.y.ToString("0.#") + ", GroundY " + GroundY.ToString("0.##"));
        if (t != null)
        {
            Vector3 tp = t.GetPosition(), sz = t.terrainData.size;
            sb.AppendLine("Terrain: x " + tp.x.ToString("0.#") + ".." + (tp.x + sz.x).ToString("0.#") + ", z " + tp.z.ToString("0.#") + ".." + (tp.z + sz.z).ToString("0.#") + ", drawHeightmap=" + t.drawHeightmap + ", holes " + t.terrainData.holesResolution);
            sb.AppendLine("Контур вылезает за Terrain на " + OutlineOutsideTerrain(t, loop).ToString("0.0") + " м");
        }
        else sb.AppendLine("Terrain: не найден");
        int meshes = 0, verts = 0;
        var root = transform.Find(RootName);
        if (root != null)
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                meshes++; verts += mf.sharedMesh.vertexCount;
            }
        InvalidateProtect();
        GetEdgeLoop();   // заодно строит карту защищённых мест
        sb.AppendLine("Бухты: защищено " + protCellCount + " клеток 1×1 м, объектов " + protObjectCount + (protSkippedBig > 0 ? ", пропущено слишком больших мешей: " + protSkippedBig : ""));
        sb.AppendLine("Меши острова: " + meshes + " шт, " + verts + " вершин");
        int gi = 0;
        if (root != null)
            foreach (var tf in root.GetComponentsInChildren<Transform>(true))
                if ((GameObjectUtility.GetStaticEditorFlags(tf.gameObject) & StaticEditorFlags.ContributeGI) != 0) gi++;
        sb.AppendLine("Объектов острова с Contribute GI: " + gi + " (должно быть 0)");
        // невидимые препятствия у края: коллайдеры, которые заходят в полосу 6 м за контуром (по ним ходят «по воздуху» или упираются)
        {
            Vector2 c0 = Vector2.zero;
            foreach (var p in loop) c0 += p;
            if (loop.Count > 0) c0 /= loop.Count;
            int listed = 0;
            foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (col == null || !col.enabled || col.isTrigger) continue;
                if (col is TerrainCollider) continue;
                if (col.transform.IsChildOf(transform)) continue;
                if (col.GetComponentInParent<PlayerMovement>() != null) continue;
                var bnd = col.bounds;
                bool near = false;
                for (int i = 0; i < loop.Count && !near; i += 2)
                {
                    Vector2 q = loop[i];
                    Vector2 outv = (q - c0).normalized;
                    for (float d = 0.3f; d <= 6f; d += 1.5f)
                    {
                        Vector2 w = q + outv * d;
                        if (bnd.Contains(new Vector3(w.x, bnd.center.y, w.y))) { near = true; break; }
                    }
                }
                if (!near || listed >= 25) continue;
                listed++;
                sb.AppendLine("Коллайдер за краем: " + GetPath(col.transform) + " (" + col.GetType().Name + ", размер " + bnd.size.ToString("0.#") + ")");
            }
            sb.AppendLine("Коллайдеров в полосе 6 м за краем: " + listed + (listed >= 25 ? "+" : "") + " (должно быть 0, кроме моста и ворот)");
        }
        sb.AppendLine("Идёт запекание света: " + Lightmapping.isRunning);
        Debug.Log(sb.ToString());
    }

    // Вернуть Terrain целиком (без дырок)
    static void UncutTerrain(Terrain t)
    {
        var td = t.terrainData;
        int M = td.holesResolution;
        var keep = new bool[M, M];
        for (int j = 0; j < M; j++)
            for (int i = 0; i < M; i++) keep[j, i] = true;
        td.SetHoles(0, 0, keep);
        EditorUtility.SetDirty(td);
    }

    // Убирает только меши острова (Build зовёт её перед пересборкой). Землю возвращает RestoreGround
    public void Clear()
    {
        var t = transform.Find(RootName);
        if (t != null) DestroyImmediate(t.gameObject);
    }

    // Только вернуть землю (на случай, если прошлая версия скрипта выключила рельеф)
    public void RestoreGround()
    {
        Terrain terrain;
        var g = FindGround(out terrain);
        if (terrain != null)
        {
            terrain.drawHeightmap = true;
            UncutTerrain(terrain);
        }
        var ga = GameObject.Find("Ground_Auto");
        if (ga != null)
        {
            var r = ga.GetComponent<Renderer>();
            if (r != null) r.enabled = true;
        }
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    Material GetRockMaterial()
    {
        if (rockMaterial != null) return rockMaterial;
        var sh = Shader.Find("Project/IslandRock");
        if (sh == null)
        {
            Debug.LogWarning("FloatingIsland: не найден шейдер Project/IslandRock (файл IslandRock.shader рядом со скриптом). Беру Simple Lit: цвета вершин не будут видны.");
            sh = Shader.Find("Universal Render Pipeline/Simple Lit");
        }
        if (sh == null) sh = Shader.Find("Standard");
        rockMaterial = new Material(sh) { name = "IslandRock" };
        return rockMaterial;
    }

    public void Build()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var pts = Outline;
        if (pts.Count < 3) { Debug.LogWarning("FloatingIsland: меньше 3 точек контура"); return; }

        // что было у земли: берём материал до того, как скроем плоскость
        Terrain groundTerrain;
        var groundGo = FindGround(out groundTerrain);
        Renderer groundR = groundGo != null ? groundGo.GetComponent<Renderer>() : null;
        // Ground_Auto прячется всегда, даже когда землёй служит Terrain
        var oldPlane = GameObject.Find("Ground_Auto");
        Renderer planeR = oldPlane != null ? oldPlane.GetComponent<Renderer>() : null;
        bool useTerrain = groundTerrain != null;
        Material top = topMaterial != null ? topMaterial : (groundR != null ? groundR.sharedMaterial : (planeR != null ? planeR.sharedMaterial : null));
        // UV «как у старой плоскости» верны только для Ground_Auto (его растягивает FitGround)
        bool matchGround = !useTerrain && topMaterial == null && groundR != null && groundGo.name == "Ground_Auto";
        if (groundGo == null)
            Debug.Log("FloatingIsland: старая земля не найдена (ни Ground_Auto, ни Terrain, ни поля Ground Object). Остров получит собственную поверхность с коллайдером.");

        InvalidateProtect();   // тропы, площадки, река могли сдвинуться: карту «сюда бухты не заходят» строим заново
        Clear();
        var root = EnsureRoot();
        root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity; root.localScale = Vector3.one;

        // UV как у старой плоскости: она растянута по границам с запасом OuterMargin (см. FitGround)
        Vector2 mn = pts[0], mx = pts[0];
        foreach (var p in pts) { mn = Vector2.Min(mn, p); mx = Vector2.Max(mx, p); }
        float ext = OuterMargin;
        Vector2 uvMin = mn - Vector2.one * ext;
        Vector2 uvSize = (mx - mn) + Vector2.one * ext * 2f;

        var loop = GetEdgeLoop();

        // поверхность: Terrain остаётся (обрезаем по контуру), а плоский Top строим только если Terrain нет
        if (useTerrain)
        {
            float gap = OutlineOutsideTerrain(groundTerrain, loop);
            if (gap > 0.05f && !buildApron)
                Debug.LogWarning("FloatingIsland: контур острова выходит за Terrain на " + gap.ToString("0.0") + " м, а «Build Apron» выключен: там будет пустота. Включи Build Apron или нажми «Вписать контур в Terrain».");
            groundTerrain.drawHeightmap = true;   // прошлая версия скрипта могла выключить рельеф
            if (cutTerrainToOutline) CutTerrain(groundTerrain, loop); else UncutTerrain(groundTerrain);
        }
        else
        {
            var topGo = new GameObject("Top");
            topGo.transform.SetParent(root, false);
            topGo.AddComponent<MeshFilter>().sharedMesh = BuildTop(loop, uvMin, uvSize, matchGround);
            var tr = topGo.AddComponent<MeshRenderer>();
            tr.sharedMaterial = top != null ? top : GetRockMaterial();
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // ходить по этому мешу: он ровно по контуру. Коллайдер старой плоскости Ground_Auto ниже выключается,
            // иначе он торчит за краем (плоскость прямоугольная, остров нет) и по воздуху можно ходить до 3 м за обрыв
            topGo.AddComponent<MeshCollider>().sharedMesh = topGo.GetComponent<MeshFilter>().sharedMesh;
        }

        // текстура земли: кромка обрыва и поле рисуются так же, как Terrain
        Material groundMat = null;
        {
            var rm0 = GetRockMaterial();
            Texture gTex; float gMeters;
            if (useTerrain && matchTerrainTexture && ResolveGroundTexture(groundTerrain, out gTex, out gMeters))
            {
                SetGround(rm0, gTex, gMeters, groundTint, 0f);
                groundMat = new Material(rm0) { name = "IslandGround" };
                SetGround(groundMat, gTex, gMeters, groundTint, 1f);
                Debug.Log("FloatingIsland: кромка и поле используют текстуру земли «" + gTex.name + "», " + gMeters.ToString("0.#") + " м на повтор.");
            }
            else
            {
                SetGround(rm0, null, 4f, groundTint, 0f);   // у кромки остаётся однотонный цвет grassTop
                if (useTerrain && matchTerrainTexture) Debug.LogWarning("FloatingIsland: не нашёл текстуру слоя Terrain. Положи свою в поле Ground Texture, иначе кромка останется однотонной.");
            }
        }

        // поле: земля между краем Terrain и контуром (если контур шире Terrain)
        if (useTerrain && buildApron && OutlineOutsideTerrain(groundTerrain, loop) > 0.05f)
        {
            var apronGo = new GameObject("Apron");
            apronGo.transform.SetParent(root, false);
            var am = BuildApron(loop, groundTerrain, topUvMeters);
            apronGo.AddComponent<MeshFilter>().sharedMesh = am;
            var ar = apronGo.AddComponent<MeshRenderer>();
            ar.sharedMaterial = groundMat != null ? groundMat : (top != null ? top : GetRockMaterial());
            ar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            apronGo.AddComponent<MeshCollider>().sharedMesh = am;
        }

        // обрыв
        Vector3[] bottom;
        var cliffGo = new GameObject("Cliff");
        cliffGo.transform.SetParent(root, false);
        cliffGo.AddComponent<MeshFilter>().sharedMesh = BuildCliff(loop, useTerrain ? groundTerrain : null, out bottom);
        var cr = cliffGo.AddComponent<MeshRenderer>();
        cr.sharedMaterial = GetRockMaterial();
        cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        bottomRing = bottom;

        // осыпь на уступах
        if (screeDensity > 0.01f && ledgeWidth > 0.01f)
        {
            var screeGo = new GameObject("CliffScree");
            screeGo.transform.SetParent(root, false);
            screeGo.AddComponent<MeshFilter>().sharedMesh = BuildScree();
            var sr = screeGo.AddComponent<MeshRenderer>();
            sr.sharedMaterial = GetRockMaterial();
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // скала, корни, кристаллы и парящие камни снизу
        if (buildUnderside)
        {
            var rm = GetRockMaterial();
            if (rm.HasProperty("_GlowIntensity") && rm.GetFloat("_GlowIntensity") < 0.01f) rm.SetFloat("_GlowIntensity", 2.5f);   // материал мог быть создан до появления свечения
            IslandUnderside.Build(this, root, rm);
        }

        // шейдер облаков: ссылка хранится на острове, чтобы шейдер не вырезало из сборки
        cloudShader = Shader.Find("Project/CloudSea");
        if (cloudShader == null) Debug.LogWarning("FloatingIsland: не найден шейдер Project/CloudSea (файл CloudSea.shader рядом со скриптом): моря облаков не будет.");

        // стена прохода по этому же контуру, земля по границам
        BuildWalls();

        // декор края: камни, кусты, ёлки (меш без коллайдеров)
        if (dressEdge && useTerrain && dressDensity > 0.01f)
        {
            var decor = new GameObject("EdgeDecor");
            decor.transform.SetParent(root, false);
            decor.AddComponent<MeshFilter>().sharedMesh = BuildDressing(loop, groundTerrain);
            var dr = decor.AddComponent<MeshRenderer>();
            dr.sharedMaterial = GetRockMaterial();
            dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // старую плоскость прячем (коллайдер остаётся). Terrain НЕ скрываем: это и есть поверхность острова
        if (hideGroundPlane && planeR != null)
        {
            planeR.enabled = false;
            // коллайдер скрытой плоскости больше не нужен: земля — Terrain (с дырками по контуру) или Top. Иначе невидимый пол за краем
            foreach (var pc in planeR.GetComponents<Collider>()) pc.enabled = false;
        }

        // Static без «Contribute GI»: меши острова не должны попадать в запекание света (оно сильно тормозит редактор).
        // Покачивающиеся камни (RiverBob) вообще не static.
        foreach (var tf in root.GetComponentsInChildren<Transform>(true))
        {
            var flags = tf.GetComponent<RiverBob>() != null ? (StaticEditorFlags)0 : (StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            GameObjectUtility.SetStaticEditorFlags(tf.gameObject, flags);
        }
        ReportOrphans(loop, useTerrain ? groundTerrain : null);
        sw.Stop();
        Debug.Log("FloatingIsland: остров собран за " + sw.ElapsedMilliseconds + " мс. Terrain " + (useTerrain ? "оставлен и обрезан" : "не найден, построен плоский Top") + ".");

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    List<Vector2> gizmoLoop;
    float gizmoAt = -10f;

    void OnDrawGizmosSelected()
    {
        // контур считается не чаще 4 раз в секунду, а не на каждую перерисовку Scene-вида
        if (gizmoLoop == null || Time.realtimeSinceStartup - gizmoAt > 0.25f)
        {
            gizmoLoop = GetEdgeLoop();
            gizmoAt = Time.realtimeSinceStartup;
        }
        var loop = gizmoLoop;
        if (loop.Count < 3) return;
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
        for (int i = 0; i < loop.Count; i++)
        {
            Vector2 a = loop[i], b = loop[(i + 1) % loop.Count];
            Gizmos.DrawLine(new Vector3(a.x, GroundY + 0.15f, a.y), new Vector3(b.x, GroundY + 0.15f, b.y));
        }

        Gizmos.color = new Color(1f, 0.7f, 0.2f, 1f);
        foreach (var p in GetOutline()) Gizmos.DrawSphere(new Vector3(p.x, GroundY + 0.3f, p.y), 0.6f);

        var gate = GetGatePosition();
        if (gate.HasValue)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 1f);
            Gizmos.DrawWireSphere(new Vector3(gate.Value.x, GroundY + 0.3f, gate.Value.y), gateWidth * 0.5f);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(FloatingIsland))]
public class FloatingIslandEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var f = (FloatingIsland)target;
        GUILayout.Space(8);
        if (GUILayout.Button("Силуэт как на референсе (бухты, слои, уступы)")) f.ApplyReferenceShapePreset();
        if (GUILayout.Button("Естественный край (пресет настроек)")) f.ApplyNaturalEdgePreset();
        if (GUILayout.Button("Контур вокруг Terrain (шире на Margin Around Terrain)")) f.OutlineAroundTerrain();
        if (GUILayout.Button("Вписать контур в Terrain (внутрь)")) f.FitOutlineToTerrain();
        if (GUILayout.Button("Собрать остров", GUILayout.Height(30))) f.Build();
        if (GUILayout.Button("Диагностика (печатает в Console)")) f.Diagnose();
        if (GUILayout.Button("Удалить остров")) { f.Clear(); f.RestoreGround(); }
        if (GUILayout.Button("Вернуть землю (рельеф, тропы, без дырок)")) f.RestoreGround();
        GUILayout.Space(4);
        if (GUILayout.Button("Добавить точку контура (в центр сцены)")) f.AddPoint();
        if (GUILayout.Button("Точка прохода к мосту")) f.AddGatePoint();
        if (GUILayout.Button("Выключить старые стены Boundary (не пускают за край)")) { int n = f.DisableLegacyBoundary(); f.DisableHiddenGroundCollider(); Debug.Log("FloatingIsland: выключено старых стен: " + n); EditorSceneManager.MarkSceneDirty(f.gameObject.scene); }
        EditorGUILayout.HelpBox("Двигай точки PathPoints (Ctrl+D на точке: копия встанет следующей по порядку) и жми «Собрать остров»: сборка заодно пересобирает стену прохода. Это и есть граница карты, отдельного MapBorder нет.", MessageType.None);
    }
}

public static class FloatingIslandMenu
{
    [MenuItem("Tools/BuilderSim/Локация/Create Floating Island", false, 101)]
    static void Create()
    {
        var f = Object.FindFirstObjectByType<FloatingIsland>();
        if (f == null)
        {
            var go = new GameObject("FloatingIsland");
            Undo.RegisterCreatedObjectUndo(go, "Create Floating Island");
            f = go.AddComponent<FloatingIsland>();
            var pl = Object.FindFirstObjectByType<PlayerMovement>();
            if (pl != null) { f.center = new Vector2(pl.transform.position.x, pl.transform.position.z); f.groundY = pl.transform.position.y; }
            f.halfSize = new Vector2(35f, 35f);
        }
        Selection.activeGameObject = f.gameObject;
        f.Build();
    }
}
#endif
