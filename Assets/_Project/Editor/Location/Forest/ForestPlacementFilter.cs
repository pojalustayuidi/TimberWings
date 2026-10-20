#if UNITY_EDITOR
// Assets/_Project/Editor/Location/Forest/ForestPlacementFilter.cs (Editor)
// Фильтр размещения деревьев: не на тропы, не в воду, не в постройки, только на земле. Обычный класс, в сцене объекта не создаёт.
// Фильтр размещения для генератора леса: не даёт ставить деревья и подлесок на тропинки, в воду и в постройки.
// Работает в Edit Mode (без Play) и в игре. Всё считается по реальной форме объектов, а не по прямоугольникам вокруг.
//
// Что собирает (по именам объектов в сцене, без учёта регистра, всё внутри иерархии объекта):
//   Ground_Auto            земля: даёт границы области (Terrain, меш или коллайдер) и высоту для GroundPoint
//   ground_pathOpen*       тропинки (объект, чьё имя НАЧИНАЕТСЯ с этого). Выключенные в иерархии пропускаются
//   River*                 река
//   LumberjackHut, TradingStall, CampTent, Campfire, OrderBoard, HouseProject (имена правятся в Инспекторе)
//                          постройки; выключенные и скрытые стадии учитываются: место под будущее здание занято сразу
// Меши растеризуются по треугольникам: извилистая тропа занимает ровно свою полосу. Меш без Read/Write заменяется
// прямоугольником по bounds (в Summary будет число таких мешей): включи Read/Write на мешах троп и реки.
// Дальше по растру считается точное евклидово расстояние (в метрах, шаг cellSize) до тропы, реки и построек отдельно.
//
// Использование из генератора:
//   var f = ForestPlacementFilter.Get();                      // найдёт в сцене или создаст
//   if (!f.CanSpawnAt(p, 0.6f)) continue;                     // не на тропе, не в воде, не вплотную к постройке
//   if (f.IsNearPathEdge(p)) { /* обочина: камни, цветы, пеньки */ }
//   if (f.TryGetPathNormal(p, out away)) rot = Quaternion.LookRotation(Vector3.Cross(Vector3.up, away));   // вдоль тропы
//   float d = f.PathDistance(p);                              // расстояние до тропы, м
// Маска обновляется сама, когда двигают объекты-источники или меняют настройки (проверка не чаще раза в 0.5 с на запросе),
// и в окне Scene после изменений иерархии. Новые или переименованные объекты внутри одного запуска генератора не отслеживаются:
// вызови Rebuild() перед генерацией.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ForestPlacementFilter
{
    [Header("Имена объектов в сцене (начало имени, регистр не важен)")]
    public string groundName = "Ground_Auto";
    public string pathPrefix = "ground_pathOpen";
    public string riverName = "River";
    public string[] buildingNames = { "LumberjackHut", "TradingStall", "CampTent", "Campfire", "OrderBoard", "HouseProject" };

    [Header("Запас сверх радиуса объекта, м")]
    public float pathMargin = 0.05f;
    public float riverMargin = 0.40f;
    public float buildingMargin = 1.50f;
    [Tooltip("Мелкая постройка (костёр, доска) занимает не меньше такого круга вокруг своей точки, м")]
    public float minBuildingRadius = 0.8f;

    [Header("Параметры")]
    [Tooltip("Размер ячейки маски, м. Меньше = точнее и тяжелее (сетка не больше ~1.2 млн ячеек)")]
    public float cellSize = 0.25f;
    [Tooltip("Точки вне земли (Ground_Auto) считаются запрещёнными")]
    public bool requireGround = true;
    [Tooltip("Пересобирать маску, когда сдвинули объект-источник или поменяли иерархию")]
    public bool autoRefresh = false;


    // ------------------------------------------------------------ состояние
    const byte FPath = 1, FWater = 2, FBuilding = 4;
    const float Far = 1e6f;
    const float MaxPart = 14f;   // часть постройки крупнее (зона взаимодействия и т.п.) в расчёт не берётся

    byte[] occ;
    float[] dPath, dWater, dBuild;
    int w, h;
    float x0, z0, cell = 0.25f;

    bool hasGround;
    Bounds groundBounds;
    readonly List<Collider> groundColliders = new List<Collider>();
    readonly List<Terrain> groundTerrains = new List<Terrain>();
    readonly List<Transform> watched = new List<Transform>();
    bool dirty = true, structureChanged;
    double lastCheck;

    public bool Ready { get; private set; }
    public string Summary { get; private set; }
    public int PathCount { get; private set; }
    public int RiverCount { get; private set; }
    public int BuildingCount { get; private set; }

    static ForestPlacementFilter instance;
    // обычный объект без компонента в сцене: на каждую генерацию создаётся заново
    public static ForestPlacementFilter Get() { instance = new ForestPlacementFilter(); return instance; }

    // ------------------------------------------------------------ запросы
    // можно ли ставить объект радиуса radius в точке: не на тропе, не в воде, не вплотную к постройке (и на земле)
    public bool CanSpawnAt(Vector3 point, float radius)
    {
        EnsureReady();
        if (!Ready) return false;
        if (requireGround && hasGround && !OnGround(point)) return false;
        if (PathDistance(point) < radius + pathMargin) return false;
        if (RiverDistance(point) < radius + riverMargin) return false;
        if (BuildingDistance(point) < radius + buildingMargin) return false;
        return true;
    }

    // обочина: расстояние от края тропы (ground_pathOpen) в пределах [minDistance, maxDistance]
    // Саму проверку на препятствия делает CanSpawnAt: зови оба
    public bool IsNearPathEdge(Vector3 point, float minDistance = 1.2f, float maxDistance = 2.8f)
    {
        EnsureReady();
        if (!Ready) return false;
        float d = PathDistance(point);
        return d >= minDistance && d <= maxDistance;
    }

    // расстояние от точки до края тропы, м (0 = на тропе, очень большое = троп нет рядом)
    public float PathDistance(Vector3 p) { EnsureReady(); return Dist(dPath, p); }
    public float RiverDistance(Vector3 p) { EnsureReady(); return Dist(dWater, p); }
    public float BuildingDistance(Vector3 p) { EnsureReady(); return Dist(dBuild, p); }

    public bool IsOnPath(Vector3 p) { return PathDistance(p) <= 0f; }
    public bool IsInRiver(Vector3 p) { return RiverDistance(p) <= 0f; }

    // единичное направление «от тропы» в точке (в плане). Поверни камень или пенёк вдоль обочины: LookRotation(Cross(up, away))
    public bool TryGetPathNormal(Vector3 p, out Vector3 awayFromPath)
    {
        awayFromPath = Vector3.zero;
        EnsureReady();
        if (!Ready || dPath == null) return false;
        float gx = Dist(dPath, p + new Vector3(cell, 0f, 0f)) - Dist(dPath, p - new Vector3(cell, 0f, 0f));
        float gz = Dist(dPath, p + new Vector3(0f, 0f, cell)) - Dist(dPath, p - new Vector3(0f, 0f, cell));
        var g = new Vector3(gx, 0f, gz);
        if (g.sqrMagnitude < 1e-8f || g.sqrMagnitude > 1e8f) return false;
        awayFromPath = g.normalized;
        return true;
    }

    // точка в плане внутри границ земли (Ground_Auto)
    public bool OnGround(Vector3 p)
    {
        if (!hasGround) return true;
        return p.x >= groundBounds.min.x && p.x <= groundBounds.max.x && p.z >= groundBounds.min.z && p.z <= groundBounds.max.z;
    }

    // точка на земле под (x, z): луч по коллайдерам Ground_Auto / Terrain, иначе верх bounds земли
    public bool GroundPoint(float x, float z, out Vector3 point)
    {
        point = new Vector3(x, 0f, z);
        EnsureReady();
        if (!hasGround) return false;
        var ray = new Ray(new Vector3(x, groundBounds.max.y + 50f, z), Vector3.down);
        RaycastHit hit;
        // Terrain первым: SampleHeight точнее и не зависит от порядка коллайдеров
        for (int i = 0; i < groundTerrains.Count; i++)
        {
            var t = groundTerrains[i];
            if (t == null) continue;
            Vector3 tp = t.transform.position, ts = t.terrainData != null ? t.terrainData.size : Vector3.zero;
            if (x < tp.x || z < tp.z || x > tp.x + ts.x || z > tp.z + ts.z) continue;
            point.y = t.SampleHeight(new Vector3(x, 0f, z)) + tp.y;
            return true;
        }
        for (int i = 0; i < groundColliders.Count; i++)
        {
            var c = groundColliders[i];
            if (c != null && c.Raycast(ray, out hit, groundBounds.size.y + 200f)) { point = hit.point; return true; }
        }
        point.y = groundBounds.max.y;
        return true;
    }

    float Dist(float[] a, Vector3 p)
    {
        if (a == null) return Far;
        int ix = Mathf.FloorToInt((p.x - x0) / cell), iz = Mathf.FloorToInt((p.z - z0) / cell);
        if ((uint)ix >= (uint)w || (uint)iz >= (uint)h) return Far;   // вне сетки препятствий нет
        return a[iz * w + ix];
    }

    // ------------------------------------------------------------ обновление
    void EnsureReady()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        if (dirty || !Ready)
        {
            if (!dirty && now - lastCheck < 1.0) return;   // прошлая сборка не удалась: не долбим каждым запросом
            Rebuild();
            return;
        }
        if (!autoRefresh || now - lastCheck < 0.5) return;
        lastCheck = now;
        for (int i = 0; i < watched.Count; i++)
            if (watched[i] == null || watched[i].hasChanged) { Rebuild(); return; }
    }

    public bool Rebuild()
    {
        Ready = false;
        dirty = false;
        structureChanged = false;
        lastCheck = Time.realtimeSinceStartupAsDouble;
        float t0 = Time.realtimeSinceStartup;

        var groundT = new List<Transform>();
        var pathT = new List<Transform>();
        var riverT = new List<Transform>();
        var bldT = new List<Transform>();
        CollectByName(groundT, pathT, riverT, bldT);
        // есть Terrain (его делает LocationBuilder): он главный, старая плоскость Ground_Auto из «Apply Look» высоту не определяет
        if (groundT.Exists(g => g != null && g.GetComponent<Terrain>() != null))
            groundT.RemoveAll(g => g == null || g.GetComponent<Terrain>() == null);
        PathCount = pathT.Count; RiverCount = riverT.Count; BuildingCount = bldT.Count;

        watched.Clear();
        var src = new List<Src>();
        var seen = new HashSet<Component>();
        unreadable = 0;

        // земля
        hasGround = false;
        groundColliders.Clear();
        groundTerrains.Clear();
        for (int i = 0; i < groundT.Count; i++) AddGround(groundT[i]);

        for (int i = 0; i < pathT.Count; i++) AddSources(pathT[i], FPath, false, src, seen);
        for (int i = 0; i < riverT.Count; i++) AddSources(riverT[i], FWater, false, src, seen);
        for (int i = 0; i < bldT.Count; i++)
        {
            AddSources(bldT[i], FBuilding, true, src, seen);
            src.Add(Src.Disc(bldT[i].position, minBuildingRadius, FBuilding));
        }
        foreach (var t in groundT) watched.Add(t);

        // область = земля + всё найденное (+ запас)
        bool any = false;
        Bounds area = new Bounds();
        if (hasGround) { area = groundBounds; any = true; }
        for (int i = 0; i < src.Count; i++)
        {
            if (!any) { area = src[i].wb; any = true; } else area.Encapsulate(src[i].wb);
        }
        if (!any)
        {
            occ = null; dPath = dWater = dBuild = null;
            Summary = "в сцене не найдено ни земли, ни троп, ни реки, ни построек: проверь имена в Инспекторе";
            Debug.LogWarning("[ForestPlacementFilter] " + Summary);
            return false;
        }
        area.Expand(new Vector3(8f, 0f, 8f));

        cell = Mathf.Max(0.05f, cellSize);
        float cells = (area.size.x / cell) * (area.size.z / cell);
        if (cells > 1200000f) cell *= Mathf.Sqrt(cells / 1200000f);
        x0 = area.min.x; z0 = area.min.z;
        w = Mathf.Max(1, Mathf.CeilToInt(area.size.x / cell));
        h = Mathf.Max(1, Mathf.CeilToInt(area.size.z / cell));
        occ = new byte[w * h];

        meshCache.Clear();
        for (int i = 0; i < src.Count; i++) Raster(src[i]);
        meshCache.Clear();

        dPath = Distance(FPath);
        dWater = Distance(FWater);
        dBuild = Distance(FBuilding);

        for (int i = 0; i < watched.Count; i++) if (watched[i] != null) watched[i].hasChanged = false;
        Ready = true;
        float ms = (Time.realtimeSinceStartup - t0) * 1000f;
        Summary = "тропы " + PathCount + ", река " + RiverCount + ", постройки " + BuildingCount + (hasGround ? ", земля найдена" : ", земля НЕ найдена")
                + " | сетка " + w + "x" + h + " по " + cell.ToString("0.0#") + " м, " + ms.ToString("F0") + " мс"
                + (unreadable > 0 ? " | мешей без Read/Write: " + unreadable : "");
        Debug.Log("[ForestPlacementFilter] " + Summary);
        if (PathCount == 0) Debug.LogWarning("[ForestPlacementFilter] не найден ни один активный объект, имя которого начинается с «" + pathPrefix + "»");
        if (unreadable > 0) Debug.LogWarning("[ForestPlacementFilter] " + unreadable + " мешей без Read/Write заняты прямоугольником по bounds. Включи Read/Write в Import Settings мешей троп и реки, чтобы маска шла по их форме");
        return true;
    }

    // ------------------------------------------------------------ поиск объектов
    static bool Starts(string n, string key) { return !string.IsNullOrEmpty(key) && n.StartsWith(key, StringComparison.OrdinalIgnoreCase); }

    void CollectByName(List<Transform> ground, List<Transform> path, List<Transform> river, List<Transform> bld)
    {
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene sc = SceneManager.GetSceneAt(s);
            if (!sc.isLoaded) continue;
            var roots = sc.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                var all = roots[r].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    Transform t = all[i];
                    string n = t.name;
                    if (Starts(n, pathPrefix)) { if (t.gameObject.activeInHierarchy) path.Add(t); continue; }
                    if (Starts(n, riverName)) { if (t.gameObject.activeInHierarchy) river.Add(t); continue; }
                    if (Starts(n, groundName) || t.GetComponent<Terrain>() != null) { ground.Add(t); continue; }
                    for (int b = 0; b < buildingNames.Length; b++)
                        if (Starts(n, buildingNames[b])) { bld.Add(t); break; }
                }
            }
        }
    }

    static bool SkipName(string n)
    {
        n = n.ToLowerInvariant();
        return n.Contains("outline") || n.Contains("hull") || n.Contains("highlight") || n.Contains("shadow");
    }

    void AddGround(Transform root)
    {
        var tr = root.GetComponent<Terrain>();
        if (tr != null && tr.terrainData != null)
        {
            Vector3 size = tr.terrainData.size;
            Include(new Bounds(tr.transform.position + size * 0.5f, size));
            groundTerrains.Add(tr);
        }
        var mfs = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < mfs.Length; i++)
            if (mfs[i].sharedMesh != null) Include(Transformed(mfs[i].sharedMesh.bounds, mfs[i].transform.localToWorldMatrix));
        var cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            if (!cols[i].isTrigger) { groundColliders.Add(cols[i]); if (cols[i].enabled) Include(cols[i].bounds); }
    }

    void Include(Bounds b)
    {
        if (!hasGround) { groundBounds = b; hasGround = true; } else groundBounds.Encapsulate(b);
    }

    // ------------------------------------------------------------ источники
    struct Src
    {
        public byte flag;
        public int kind;                 // 0 меш, 1 коробка, 2 прямоугольник по bounds, 3 круг
        public Mesh mesh; public Matrix4x4 m;
        public Transform t; public Vector3 c, size;
        public Bounds wb;
        public float r;

        public static Src Disc(Vector3 p, float r, byte f)
        {
            return new Src { flag = f, kind = 3, c = p, r = r, wb = new Bounds(p, new Vector3(r * 2f, 0.1f, r * 2f)) };
        }
    }

    int unreadable;

    void AddSources(Transform root, byte flag, bool building, List<Src> list, HashSet<Component> seen)
    {
        watched.Add(root);
        int before = list.Count;
        var mfs = root.GetComponentsInChildren<MeshFilter>(true);
        bool useMeshes = !(building && mfs.Length > 300);   // корень целого уровня: только круг, иначе закроем пол-острова
        if (useMeshes)
            for (int i = 0; i < mfs.Length; i++)
            {
                var mf = mfs[i];
                if (mf.sharedMesh == null || !seen.Add(mf) || SkipName(mf.gameObject.name)) continue;
                if (mf.GetComponent<Renderer>() == null) continue;
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                Bounds wb = Transformed(mf.sharedMesh.bounds, m);
                if (building && Mathf.Max(wb.size.x, wb.size.z) > MaxPart * 2f) continue;
                list.Add(new Src { flag = flag, kind = 0, mesh = mf.sharedMesh, m = m, wb = wb });
                watched.Add(mf.transform);
            }

        if (building)
        {
            var bcs = root.GetComponentsInChildren<BoxCollider>(true);
            for (int i = 0; i < bcs.Length; i++)
            {
                var bc = bcs[i];
                if (!seen.Add(bc) || SkipName(bc.gameObject.name)) continue;
                Bounds wb = Transformed(new Bounds(bc.center, bc.size), bc.transform.localToWorldMatrix);
                if (Mathf.Max(wb.size.x, wb.size.z) > MaxPart) continue;   // зона взаимодействия шире самой постройки
                list.Add(new Src { flag = flag, kind = 1, t = bc.transform, c = bc.center, size = bc.size, wb = wb });
                watched.Add(bc.transform);
            }
        }

        if (list.Count > before) return;

        // нет читаемых мешей: рендерер / коллайдер по bounds, иначе круг 1 м
        var rend = root.GetComponentInChildren<Renderer>(true);
        if (rend != null) { list.Add(new Src { flag = flag, kind = 2, wb = rend.bounds }); return; }
        var col = root.GetComponentInChildren<Collider>(true);
        if (col != null) { list.Add(new Src { flag = flag, kind = 2, wb = col.bounds }); return; }
        list.Add(Src.Disc(root.position, 1f, flag));
    }

    // ------------------------------------------------------------ растеризация
    readonly Dictionary<Mesh, KeyValuePair<Vector3[], int[]>> meshCache = new Dictionary<Mesh, KeyValuePair<Vector3[], int[]>>();

    static Bounds Transformed(Bounds lb, Matrix4x4 m)
    {
        Vector3 c = lb.center, e = lb.extents;
        Bounds r = new Bounds(m.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y, -e.z)), Vector3.zero);
        for (int i = 1; i < 8; i++)
            r.Encapsulate(m.MultiplyPoint3x4(c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z)));
        return r;
    }

    bool CellRange(float minX, float maxX, float minZ, float maxZ, int pad, out int ix0, out int ix1, out int iz0, out int iz1)
    {
        ix0 = Mathf.Max(0, Mathf.FloorToInt((minX - x0) / cell) - pad);
        ix1 = Mathf.Min(w - 1, Mathf.FloorToInt((maxX - x0) / cell) + pad);
        iz0 = Mathf.Max(0, Mathf.FloorToInt((minZ - z0) / cell) - pad);
        iz1 = Mathf.Min(h - 1, Mathf.FloorToInt((maxZ - z0) / cell) + pad);
        return ix0 <= ix1 && iz0 <= iz1;
    }

    void Raster(Src s)
    {
        int ix0, ix1, iz0, iz1;
        switch (s.kind)
        {
            case 0: RasterMesh(s.mesh, s.m, s.flag); break;
            case 1:
                if (!CellRange(s.wb.min.x, s.wb.max.x, s.wb.min.z, s.wb.max.z, 0, out ix0, out ix1, out iz0, out iz1)) break;
                Vector3 half = s.size * 0.5f;
                for (int iz = iz0; iz <= iz1; iz++)
                    for (int ix = ix0; ix <= ix1; ix++)
                    {
                        Vector3 l = s.t.InverseTransformPoint(new Vector3(x0 + (ix + 0.5f) * cell, s.wb.center.y, z0 + (iz + 0.5f) * cell)) - s.c;
                        if (Mathf.Abs(l.x) <= half.x && Mathf.Abs(l.z) <= half.z) occ[iz * w + ix] |= s.flag;
                    }
                break;
            case 2:
                if (!CellRange(s.wb.min.x, s.wb.max.x, s.wb.min.z, s.wb.max.z, 0, out ix0, out ix1, out iz0, out iz1)) break;
                for (int iz = iz0; iz <= iz1; iz++)
                    for (int ix = ix0; ix <= ix1; ix++) occ[iz * w + ix] |= s.flag;
                break;
            default:
                if (!CellRange(s.c.x - s.r, s.c.x + s.r, s.c.z - s.r, s.c.z + s.r, 0, out ix0, out ix1, out iz0, out iz1)) break;
                float r2 = s.r * s.r;
                for (int iz = iz0; iz <= iz1; iz++)
                {
                    float dz = z0 + (iz + 0.5f) * cell - s.c.z;
                    for (int ix = ix0; ix <= ix1; ix++)
                    {
                        float dx = x0 + (ix + 0.5f) * cell - s.c.x;
                        if (dx * dx + dz * dz <= r2) occ[iz * w + ix] |= s.flag;
                    }
                }
                break;
        }
    }

    void RasterMesh(Mesh m, Matrix4x4 l2w, byte f)
    {
        if (!m.isReadable)
        {
            unreadable++;
            Bounds b = Transformed(m.bounds, l2w);
            int a0, a1, b0, b1;
            if (CellRange(b.min.x, b.max.x, b.min.z, b.max.z, 0, out a0, out a1, out b0, out b1))
                for (int iz = b0; iz <= b1; iz++)
                    for (int ix = a0; ix <= a1; ix++) occ[iz * w + ix] |= f;
            return;
        }
        KeyValuePair<Vector3[], int[]> data;
        if (!meshCache.TryGetValue(m, out data)) { data = new KeyValuePair<Vector3[], int[]>(m.vertices, m.triangles); meshCache[m] = data; }
        Vector3[] vs = data.Key; int[] ts = data.Value;
        var wv = new Vector3[vs.Length];
        for (int i = 0; i < vs.Length; i++) wv[i] = l2w.MultiplyPoint3x4(vs[i]);
        for (int i = 0; i + 2 < ts.Length; i += 3)
        {
            Vector3 a = wv[ts[i]], b = wv[ts[i + 1]], c = wv[ts[i + 2]];
            Tri(a.x, a.z, b.x, b.z, c.x, c.z, f);
        }
    }

    // ячейка занята, если её центр не дальше полуячейки от треугольника в плане (тонкие полосы не теряются)
    void Tri(float ax, float az, float bx, float bz, float cx, float cz, byte f)
    {
        float area = (bx - ax) * (cz - az) - (bz - az) * (cx - ax);
        if (Mathf.Abs(area) < 1e-6f) return;   // вертикальная в плане грань: её закрывают пол, крыша и запас
        float sg = area > 0f ? 1f : -1f;
        float l0 = Mathf.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
        float l1 = Mathf.Sqrt((cx - bx) * (cx - bx) + (cz - bz) * (cz - bz));
        float l2 = Mathf.Sqrt((ax - cx) * (ax - cx) + (az - cz) * (az - cz));
        if (l0 < 1e-5f || l1 < 1e-5f || l2 < 1e-5f) return;
        int ix0, ix1, iz0, iz1;
        if (!CellRange(Mathf.Min(ax, Mathf.Min(bx, cx)), Mathf.Max(ax, Mathf.Max(bx, cx)), Mathf.Min(az, Mathf.Min(bz, cz)), Mathf.Max(az, Mathf.Max(bz, cz)), 1, out ix0, out ix1, out iz0, out iz1)) return;
        float tol = cell * 0.5f;
        for (int iz = iz0; iz <= iz1; iz++)
        {
            float pz = z0 + (iz + 0.5f) * cell;
            for (int ix = ix0; ix <= ix1; ix++)
            {
                float px = x0 + (ix + 0.5f) * cell;
                if (((bx - ax) * (pz - az) - (bz - az) * (px - ax)) * sg / l0 < -tol) continue;
                if (((cx - bx) * (pz - bz) - (cz - bz) * (px - bx)) * sg / l1 < -tol) continue;
                if (((ax - cx) * (pz - cz) - (az - cz) * (px - cx)) * sg / l2 < -tol) continue;
                occ[iz * w + ix] |= f;
            }
        }
    }

    // ------------------------------------------------------------ расстояния (точное евклидово преобразование, Felzenszwalb)
    float[] Distance(byte flag)
    {
        int n = w * h;
        var g = new double[n];
        bool any = false;
        for (int i = 0; i < n; i++)
        {
            if ((occ[i] & flag) != 0) { g[i] = 0.0; any = true; } else g[i] = 1e20;
        }
        if (!any) return null;

        int m = Mathf.Max(w, h);
        var f = new double[m]; var d = new double[m]; var v = new int[m]; var z = new double[m + 1];
        for (int ix = 0; ix < w; ix++)
        {
            for (int iz = 0; iz < h; iz++) f[iz] = g[iz * w + ix];
            Edt1D(f, d, v, z, h);
            for (int iz = 0; iz < h; iz++) g[iz * w + ix] = d[iz];
        }
        for (int iz = 0; iz < h; iz++)
        {
            int row = iz * w;
            for (int ix = 0; ix < w; ix++) f[ix] = g[row + ix];
            Edt1D(f, d, v, z, w);
            for (int ix = 0; ix < w; ix++) g[row + ix] = d[ix];
        }
        var res = new float[n];
        for (int i = 0; i < n; i++) res[i] = (float)(Math.Sqrt(g[i]) * cell);
        return res;
    }

    static void Edt1D(double[] f, double[] d, int[] v, double[] z, int n)
    {
        int k = 0;
        v[0] = 0; z[0] = double.NegativeInfinity; z[1] = double.PositiveInfinity;
        for (int q = 1; q < n; q++)
        {
            double s;
            while (true)
            {
                int p = v[k];
                s = ((f[q] + (double)q * q) - (f[p] + (double)p * p)) / (2.0 * q - 2.0 * p);
                if (s <= z[k]) k--; else break;
            }
            k++;
            v[k] = q; z[k] = s; z[k + 1] = double.PositiveInfinity;
        }
        k = 0;
        for (int q = 0; q < n; q++)
        {
            while (z[k + 1] < q) k++;
            double dq = q - v[k];
            d[q] = dq * dq + f[v[k]];
        }
    }
}
#endif
