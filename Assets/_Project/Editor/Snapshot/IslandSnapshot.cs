#if UNITY_EDITOR
// Assets/_Project/Editor/Island/Snapshot/IslandSnapshot.cs
// «Глаза» для Claude: один клик собирает ВЕСЬ остров в один текстовый файл и 3 картинки.
// Меню: Tools > BuilderSim > Остров > Снимок острова (текст + 3 картинки)
//
// Что попадает в снимок (всё читается из сцены и настроек, а не из памяти генераторов,
// поэтому снимок честный: он показывает то, что реально лежит в сцене, а не то, что «должно быть»):
//   контур острова и ворота (FloatingIsland) · Terrain (высоты, слои: тропы, площадки, скала, берег) ·
//   река (меши воды) · лес (зоны, деревья) · здания и точки интереса · иерархия Location/Island ·
//   все числовые настройки LocationBuilder · ASCII-карта сверху и ASCII-карта высот.
// Результат: <проект>/IslandSnapshot/island.md + island_top.png + island_view.png + island_side.png
// Что делать с результатом: прикрепить island.md (и картинки) к сообщению с задачей.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class IslandSnapshot
{
    const int Cols = 96;                 // ширина ASCII-карты в символах
    const int ShotW = 1920, ShotH = 1080;

    // имена компонентов-«точек интереса»; ищем по имени типа, чтобы снимок не ломался при переименовании классов
    static readonly string[] PoiTypes =
    {
        "GateBridge", "RiverBridge", "CloudSea", "IslandEdgeGuard",
        "TradingStall", "SupplyPoint", "PetCrateSite", "WorkerCamp", "PlankWorkshop", "NpcMarker", "ForestCameraZone"
    };

    [MenuItem("Tools/BuilderSim/Локация/Снимок острова (текст + 3 картинки)", false, 120)]
    static void MakeFull() { Run(true); }

    [MenuItem("Tools/BuilderSim/Локация/Снимок острова (только текст, быстро)", false, 121)]
    static void MakeTextOnly() { Run(false); }

    // ------------------------------------------------------------------ вход

    static void Run(bool shots)
    {
        if (Application.isPlaying) { Debug.LogWarning("[IslandSnapshot] Во время Play снимок не делается."); return; }

        string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "IslandSnapshot");
        Directory.CreateDirectory(dir);

        var d = new Data();
        try
        {
            EditorUtility.DisplayProgressBar("Снимок острова", "Читаю сцену", 0.1f);
            Collect(d);
            EditorUtility.DisplayProgressBar("Снимок острова", "Рисую карты", 0.4f);
            Rasterize(d);
            var sb = new StringBuilder(32768);
            Write(sb, d, shots);
            File.WriteAllText(Path.Combine(dir, "island.md"), sb.ToString(), new UTF8Encoding(false));

            if (shots && d.hasOutline)
            {
                EditorUtility.DisplayProgressBar("Снимок острова", "Скриншоты", 0.7f);
                TakeShots(d, dir);
            }
            Debug.Log("[IslandSnapshot] Готово: " + dir + "  (прикрепи island.md" + (shots ? " и три PNG" : "") + " к сообщению)");
            EditorUtility.RevealInFinder(Path.Combine(dir, "island.md"));
        }
        catch (Exception e) { Debug.LogException(e); }
        finally { EditorUtility.ClearProgressBar(); }
    }

    // ------------------------------------------------------------------ данные

    sealed class Poi { public string kind, name; public Vector3 pos; public string extra = ""; }
    sealed class ZoneInfo { public string name, shape, biome = ""; public Vector3 pos; public float radius; public Vector2 size; public int trees; }

    sealed class Data
    {
        public string scene;
        public bool hasOutline, hasTerrain;
        public List<Vector2> outline = new List<Vector2>();
        public Vector2? gate;
        public Rect bbox;
        public float area;
        public Vector2 centroid;

        public Terrain terrain;
        public Vector3 tPos, tSize;
        public float hMin, hMax;
        public string[] layerNames = new string[0];

        public List<Poi> pois = new List<Poi>();
        public List<Poi> markers = new List<Poi>();
        public List<ZoneInfo> zones = new List<ZoneInfo>();
        public List<Vector3> trees = new List<Vector3>();
        public int treeObjects;
        public List<string> hierarchy = new List<string>();
        public List<string> trails = new List<string>();
        public string settingsText = "";
        public string padsText = "";

        // растр
        public int rows;
        public float cellX, cellZ;
        public char[,] top;
        public char[,] heights;
        public bool[,] water;
        public bool[,] inside;
        public bool waterFound;
    }

    static void Collect(Data d)
    {
        d.scene = SceneManager.GetActiveScene().name;

        // --- контур острова
        var mb = UnityEngine.Object.FindFirstObjectByType<FloatingIsland>(FindObjectsInactive.Include);
        if (mb != null)
        {
            var pts = mb.GetOutline();
            if (pts != null && pts.Count >= 3)
            {
                d.outline = pts; d.hasOutline = true;
                d.gate = mb.GetGatePosition();
                float minX = 1e9f, minZ = 1e9f, maxX = -1e9f, maxZ = -1e9f;
                foreach (var p in pts) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y); }
                d.bbox = new Rect(minX, minZ, maxX - minX, maxZ - minZ);
                float a = 0f; Vector2 c = Vector2.zero;
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector2 p = pts[i], q = pts[(i + 1) % pts.Count];
                    float cr = p.x * q.y - q.x * p.y;
                    a += cr; c += (p + q) * cr;
                }
                d.area = Mathf.Abs(a) * 0.5f;
                d.centroid = Mathf.Abs(a) > 1e-3f ? c / (3f * a) : d.bbox.center;
            }
        }

        // --- terrain
        d.terrain = Terrain.activeTerrain != null ? Terrain.activeTerrain : UnityEngine.Object.FindFirstObjectByType<Terrain>();
        if (d.terrain != null && d.terrain.terrainData != null)
        {
            d.hasTerrain = true;
            d.tPos = d.terrain.transform.position;
            d.tSize = d.terrain.terrainData.size;
            var layers = d.terrain.terrainData.terrainLayers;
            d.layerNames = new string[layers.Length];
            for (int i = 0; i < layers.Length; i++) d.layerNames[i] = layers[i] != null ? layers[i].name : "?";
        }
        if (!d.hasOutline && d.hasTerrain)
        {
            d.bbox = new Rect(d.tPos.x, d.tPos.z, d.tSize.x, d.tSize.z);
            d.centroid = d.bbox.center;
            d.area = d.tSize.x * d.tSize.z;
        }

        // --- зоны (карта + лес)
        foreach (var z in UnityEngine.Object.FindObjectsByType<MapZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var fz = z.GetComponent<ForestZone>();
            d.zones.Add(new ZoneInfo
            {
                name = z.zoneName, shape = z.shape.ToString(), pos = z.transform.position,
                radius = z.radius, size = z.size, biome = fz != null ? fz.biome.ToString() : ""
            });
        }
        d.zones.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

        // --- деревья
        foreach (var r in UnityEngine.Object.FindObjectsByType<ResourceItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            d.trees.Add(r.transform.position);
        d.treeObjects = d.trees.Count;
        if (d.trees.Count == 0)
        {
            var tr = GameObject.Find(ForestZones.TreesRootName);
            if (tr != null)
                foreach (Transform c1 in tr.transform)
                {
                    if (c1.childCount == 0) d.trees.Add(c1.position);
                    else foreach (Transform c2 in c1) d.trees.Add(c2.position);
                }
        }
        foreach (var t in d.trees)
            foreach (var zi in d.zones)
                if (zi.biome != "" && InZone(zi, t)) { zi.trees++; break; }

        // --- здания
        foreach (var b in UnityEngine.Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            d.pois.Add(new Poi
            {
                kind = "Building", name = b.name, pos = b.transform.position,
                extra = b.SiteKind + (b.IsFinished ? ", достроено" : ", стройка")
            });
        }

        // --- прочие компоненты-точки по имени типа
        var set = new HashSet<string>(PoiTypes);
        foreach (var m in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (m == null) continue;
            string tn = m.GetType().Name;
            if (!set.Contains(tn)) continue;
            d.pois.Add(new Poi { kind = tn, name = m.name, pos = m.transform.position });
        }

        // --- маркеры генератора (Markers/*, Pads/*)
        var loc = GameObject.Find("Location");
        if (loc != null)
        {
            foreach (string folder in new[] { "Markers", "Pads" })
            {
                var f = loc.transform.Find(folder);
                if (f == null) continue;
                foreach (Transform c in f) d.markers.Add(new Poi { kind = folder, name = c.name, pos = c.position });
            }
        }
        var gateObj = GameObject.Find("ForestGate");
        if (gateObj != null && !d.markers.Exists(m => m.name == "ForestGate"))
            d.markers.Add(new Poi { kind = "Markers", name = "ForestGate", pos = gateObj.transform.position });

        // --- тропы леса
        var ft = GameObject.Find("ForestTrails");
        if (ft != null)
            foreach (Transform c in ft.transform)
            {
                Bounds b; string bs = TryBounds(c, out b) ? " центр (" + F(b.center.x) + ", " + F(b.center.z) + ") размер " + F(b.size.x) + "×" + F(b.size.z) : "";
                d.trails.Add(c.name + bs);
            }

        // --- иерархия
        foreach (string root in new[] { "Location", "Island", "ForestTrees", "ForestTrails" })
        {
            var go = GameObject.Find(root);
            if (go == null) { d.hierarchy.Add(root + ": НЕТ в сцене"); continue; }
            AddNode(d.hierarchy, go.transform, 0, 2);
        }

        // --- настройки
        DumpSettings(d);
    }

    static bool InZone(ZoneInfo z, Vector3 p)
    {
        Vector3 dlt = p - z.pos; dlt.y = 0f;
        if (z.shape == "Circle") return dlt.sqrMagnitude <= z.radius * z.radius;
        return Mathf.Abs(dlt.x) <= z.size.x * 0.5f && Mathf.Abs(dlt.z) <= z.size.y * 0.5f;   // поворот зоны не учитываем: для сводки хватает
    }

    static bool TryBounds(Transform t, out Bounds b)
    {
        b = new Bounds(t.position, Vector3.zero);
        bool any = false;
        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any;
    }

    static void AddNode(List<string> lines, Transform t, int depth, int maxDepth)
    {
        if (lines.Count > 260) return;
        Bounds b;
        string bs = TryBounds(t, out b) ? "  [центр " + F(b.center.x) + "," + F(b.center.z) + "  размер " + F(b.size.x) + "×" + F(b.size.z) + "]" : "";
        lines.Add(new string(' ', depth * 2) + "- " + t.name + (t.childCount > 0 ? " (" + t.childCount + ")" : "") + bs);
        if (depth >= maxDepth) return;
        // повторяющиеся дети (деревья, камни) сворачиваем: иначе файл на мегабайт
        if (t.childCount > 24) { lines.Add(new string(' ', (depth + 1) * 2) + "… " + t.childCount + " однотипных объектов"); return; }
        foreach (Transform c in t) AddNode(lines, c, depth + 1, maxDepth);
    }

    static void DumpSettings(Data d)
    {
        try
        {
            var s = new LocationBuilder.Settings();
            string json = EditorPrefs.GetString(LocationBuilder.PrefsKey, "");
            if (!string.IsNullOrEmpty(json)) JsonUtility.FromJsonOverwrite(json, s);
            var sb = new StringBuilder();
            int n = 0;
            foreach (var f in typeof(LocationBuilder.Settings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Type ft = f.FieldType;
                object v = f.GetValue(s);
                string txt = null;
                if (ft == typeof(float)) txt = F((float)v);
                else if (ft == typeof(int) || ft == typeof(bool) || ft == typeof(string)) txt = Convert.ToString(v);
                else if (ft == typeof(Vector2)) { var q = (Vector2)v; txt = "(" + F(q.x) + "," + F(q.y) + ")"; }
                else if (ft == typeof(Vector3)) { var q = (Vector3)v; txt = "(" + F(q.x) + "," + F(q.y) + "," + F(q.z) + ")"; }
                if (txt == null) continue;
                sb.Append(f.Name).Append('=').Append(txt).Append("   ");
                if (++n % 4 == 0) sb.AppendLine();
            }
            d.settingsText = sb.ToString();

            var pb = new StringBuilder();
            foreach (var p in s.pads)
                pb.AppendLine("| " + p.name + " | " + F(p.angle) + "° | " + F(p.distance) + " | " + (p.round ? "круг r=" + F(p.size.x) : F(p.size.x) + "×" + F(p.size.y)) + " | " + (string.IsNullOrEmpty(p.parent) ? "от центра" : "ответвление от " + p.parent) + " |");
            d.padsText = pb.ToString();
        }
        catch (Exception e) { d.settingsText = "не удалось прочитать настройки: " + e.Message; }
    }

    // ------------------------------------------------------------------ растр: ASCII-карты

    static void Rasterize(Data d)
    {
        if (!d.hasOutline && !d.hasTerrain) return;
        Rect r = d.bbox;
        r.xMin -= 2f; r.yMin -= 2f; r.xMax += 2f; r.yMax += 2f;
        d.bbox = r;

        int cols = Cols;
        d.cellX = r.width / cols;
        d.cellZ = d.cellX * 2f;                     // символ примерно вдвое выше своей ширины
        int rows = Mathf.Max(8, Mathf.CeilToInt(r.height / d.cellZ));
        d.rows = rows;
        d.top = new char[rows, cols];
        d.heights = new char[rows, cols];
        d.water = new bool[rows, cols];
        d.inside = new bool[rows, cols];

        // вода: растеризуем треугольники водных мешей
        d.waterFound = RasterWater(d);

        // деревья по ячейкам
        var treeCount = new int[rows, cols];
        foreach (var t in d.trees)
        {
            int c, rr;
            if (ToCell(d, t.x, t.z, out c, out rr)) treeCount[rr, c]++;
        }

        // точки поверх: здания, NPC, маркеры
        var overlay = new char[rows, cols];
        foreach (var p in d.pois)
        {
            int c, rr;
            if (!ToCell(d, p.pos.x, p.pos.z, out c, out rr)) continue;
            if (p.kind == "Building") overlay[rr, c] = 'B';
            else if (p.kind == "NpcMarker" && overlay[rr, c] == 0) overlay[rr, c] = 'N';
        }
        foreach (var m in d.markers)
        {
            int c, rr;
            if (!ToCell(d, m.pos.x, m.pos.z, out c, out rr)) continue;
            if (m.name == "ForestGate") overlay[rr, c] = 'F';
            else if (m.name == "CAMPFIRE_Point" && overlay[rr, c] == 0) overlay[rr, c] = 'C';
        }
        if (d.gate.HasValue)
        {
            int c, rr;
            if (ToCell(d, d.gate.Value.x, d.gate.Value.y, out c, out rr)) overlay[rr, c] = 'G';
        }

        // высоты
        var hv = new float[rows, cols];
        float hmin = 1e9f, hmax = -1e9f;
        for (int rr = 0; rr < rows; rr++)
            for (int c = 0; c < cols; c++)
            {
                Vector2 w = CellCenter(d, c, rr);
                bool ins = d.hasOutline ? PointInPoly(d.outline, w) : true;
                d.inside[rr, c] = ins;
                if (!ins || !d.hasTerrain) continue;
                float h = d.terrain.SampleHeight(new Vector3(w.x, 0f, w.y)) + d.tPos.y;
                hv[rr, c] = h;
                hmin = Mathf.Min(hmin, h); hmax = Mathf.Max(hmax, h);
            }
        d.hMin = hmin > 1e8f ? 0f : hmin; d.hMax = hmax < -1e8f ? 0f : hmax;

        var td = d.hasTerrain ? d.terrain.terrainData : null;
        for (int rr = 0; rr < rows; rr++)
            for (int c = 0; c < cols; c++)
            {
                if (!d.inside[rr, c]) { d.top[rr, c] = ' '; d.heights[rr, c] = ' '; continue; }

                float hn = d.hMax - d.hMin < 1e-3f ? 0f : (hv[rr, c] - d.hMin) / (d.hMax - d.hMin);
                d.heights[rr, c] = (char)('0' + Mathf.Clamp(Mathf.FloorToInt(hn * 10f), 0, 9));

                char ch = '.';
                if (td != null) ch = GroundChar(d, td, CellCenter(d, c, rr));
                int tc = treeCount[rr, c];
                if (tc >= 3) ch = 'T'; else if (tc > 0) ch = 't';
                if (d.water[rr, c]) ch = '~';
                if (overlay[rr, c] != 0) ch = overlay[rr, c];
                d.top[rr, c] = ch;
            }
    }

    static char GroundChar(Data d, TerrainData td, Vector2 w)
    {
        int aw = td.alphamapWidth, ah = td.alphamapHeight;
        int ax = Mathf.Clamp(Mathf.RoundToInt((w.x - d.tPos.x) / td.size.x * (aw - 1)), 0, aw - 1);
        int az = Mathf.Clamp(Mathf.RoundToInt((w.y - d.tPos.z) / td.size.z * (ah - 1)), 0, ah - 1);
        float[,,] a = td.GetAlphamaps(ax, az, 1, 1);
        int best = 0; float bw = -1f;
        int n = a.GetLength(2);
        for (int i = 0; i < n; i++) if (a[0, 0, i] > bw) { bw = a[0, 0, i]; best = i; }
        string name = best < d.layerNames.Length ? d.layerNames[best].ToLowerInvariant() : "";
        if (name.Contains("traildark") || name.Contains("trail")) return '=';
        if (name.Contains("pad")) return 'o';
        if (name.Contains("rock")) return '^';
        if (name.Contains("soil")) return ':';
        if (name.Contains("beach")) return ',';
        if (name.Contains("wet")) return ';';
        if (name.Contains("forest")) return '"';
        return '.';
    }

    static bool RasterWater(Data d)
    {
        var roots = new List<Transform>();
        var loc = GameObject.Find("Location");
        if (loc != null) { var rv = loc.transform.Find("River"); if (rv != null) roots.Add(rv); }
        if (roots.Count == 0 && loc != null) roots.Add(loc.transform);
        bool any = false;
        foreach (var root in roots)
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.name.ToLowerInvariant();
                if (n.Contains("rock") || n.Contains("barrier") || n.Contains("foam") || n.Contains("lily") || n.Contains("mist")) continue;
                var mat = mr.sharedMaterial;
                bool waterish = n.StartsWith("water") || (mat != null && mat.name.ToLowerInvariant().Contains("water"));
                if (!waterish) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                try
                {
                    var verts = mf.sharedMesh.vertices; var tris = mf.sharedMesh.triangles;
                    var m = mr.transform.localToWorldMatrix;
                    for (int i = 0; i + 2 < tris.Length; i += 3)
                    {
                        Vector3 a = m.MultiplyPoint3x4(verts[tris[i]]), b = m.MultiplyPoint3x4(verts[tris[i + 1]]), c = m.MultiplyPoint3x4(verts[tris[i + 2]]);
                        FillTri(d, new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z));
                    }
                    any = true;
                }
                catch (Exception) { /* меш нечитаемый: пропускаем */ }
            }
        return any;
    }

    static void FillTri(Data d, Vector2 a, Vector2 b, Vector2 c)
    {
        int c0, r0, c1, r1;
        float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
        float minZ = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), maxZ = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
        c0 = Mathf.Clamp(Mathf.FloorToInt((minX - d.bbox.xMin) / d.cellX), 0, Cols - 1);
        c1 = Mathf.Clamp(Mathf.FloorToInt((maxX - d.bbox.xMin) / d.cellX), 0, Cols - 1);
        r0 = Mathf.Clamp(Mathf.FloorToInt((d.bbox.yMax - maxZ) / d.cellZ), 0, d.rows - 1);
        r1 = Mathf.Clamp(Mathf.FloorToInt((d.bbox.yMax - minZ) / d.cellZ), 0, d.rows - 1);
        for (int rr = r0; rr <= r1; rr++)
            for (int cc = c0; cc <= c1; cc++)
            {
                Vector2 p = CellCenter(d, cc, rr);
                if (InTri(p, a, b, c)) d.water[rr, cc] = true;
            }
    }

    static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }
    static float Cross(Vector2 p, Vector2 a, Vector2 b) { return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y); }

    static Vector2 CellCenter(Data d, int c, int r)
    {
        return new Vector2(d.bbox.xMin + (c + 0.5f) * d.cellX, d.bbox.yMax - (r + 0.5f) * d.cellZ);   // строка 0 = север (+Z) сверху
    }

    static bool ToCell(Data d, float x, float z, out int c, out int r)
    {
        c = Mathf.FloorToInt((x - d.bbox.xMin) / d.cellX);
        r = Mathf.FloorToInt((d.bbox.yMax - z) / d.cellZ);
        return c >= 0 && c < Cols && r >= 0 && r < d.rows;
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

    // ------------------------------------------------------------------ текст

    static string F(float v) { return v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture); }

    static void Write(StringBuilder sb, Data d, bool shots)
    {
        sb.AppendLine("# Снимок острова");
        sb.AppendLine("Сцена: " + d.scene + " · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " · оси: север = +Z (верх карты), восток = +X (право). Единицы: метры мира.");
        sb.AppendLine();

        sb.AppendLine("## 1. Сводка");
        if (d.hasOutline)
        {
            sb.AppendLine("- Контур острова (FloatingIsland): " + d.outline.Count + " точек, ширина " + F(d.bbox.width - 4f) + " × глубина " + F(d.bbox.height - 4f) +
                          " м, площадь ≈ " + F(d.area) + " м², центр (" + F(d.centroid.x) + ", " + F(d.centroid.y) + ")");
            sb.AppendLine("- Ворота к следующей локации: " + (d.gate.HasValue ? "(" + F(d.gate.Value.x) + ", " + F(d.gate.Value.y) + ")" : "нет"));
        }
        else sb.AppendLine("- Контур острова не найден (нет FloatingIsland с ≥3 точками контура). Карта нарисована по границам Terrain.");
        if (d.hasTerrain)
            sb.AppendLine("- Terrain: позиция (" + F(d.tPos.x) + ", " + F(d.tPos.y) + ", " + F(d.tPos.z) + "), размер " + F(d.tSize.x) + " × " + F(d.tSize.z) + ", высотный диапазон на острове " + F(d.hMin) + " … " + F(d.hMax) + " м");
        else sb.AppendLine("- Terrain в сцене не найден.");
        sb.AppendLine("- Река (меши воды): " + (d.waterFound ? "найдена, отмечена на карте символом ~" : "меши воды не найдены (искал под Location/River)"));
        sb.AppendLine("- Деревьев: " + d.trees.Count + (d.treeObjects == 0 && d.trees.Count > 0 ? " (посчитаны по детям ForestTrees)" : ""));
        sb.AppendLine("- Зон: " + d.zones.Count + " · зданий/точек интереса: " + d.pois.Count + " · маркеров генератора: " + d.markers.Count);
        sb.AppendLine();

        if (d.top != null)
        {
            sb.AppendLine("## 2. Карта сверху (ASCII)");
            sb.AppendLine("Одна ячейка ≈ " + F(d.cellX) + " × " + F(d.cellZ) + " м. Верх = север.");
            sb.AppendLine("```");
            AppendGrid(sb, d.top, d.rows);
            sb.AppendLine("```");
            sb.AppendLine("Легенда: `.` трава · `\"` лесная земля · `=` тропа · `o` площадка · `,` пляж/берег · `;` влажная земля · `:` земля · `^` скала · `~` вода · `t` дерево · `T` густо (3+ в ячейке) · `B` здание · `N` NPC · `C` костёр · `F` арка леса · `G` ворота острова · `W` водопад · пробел = пустота за краем острова.");
            sb.AppendLine();
            sb.AppendLine("## 3. Карта высот (0 = низ, 9 = верх диапазона " + F(d.hMin) + " … " + F(d.hMax) + " м)");
            sb.AppendLine("```");
            AppendGrid(sb, d.heights, d.rows);
            sb.AppendLine("```");
            sb.AppendLine();
            WriteRiverRows(sb, d);
        }

        sb.AppendLine("## 4. Зоны");
        if (d.zones.Count == 0) sb.AppendLine("Зон нет.");
        else
        {
            sb.AppendLine("| Зона | Форма | Центр (x, z) | Размер | Биом леса | Деревьев |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var z in d.zones)
                sb.AppendLine("| " + z.name + " | " + z.shape + " | (" + F(z.pos.x) + ", " + F(z.pos.z) + ") | " +
                              (z.shape == "Circle" ? "r=" + F(z.radius) : F(z.size.x) + "×" + F(z.size.y)) + " | " + (z.biome == "" ? "—" : z.biome) + " | " + (z.biome == "" ? "—" : z.trees.ToString()) + " |");
        }
        sb.AppendLine();

        sb.AppendLine("## 5. Здания и точки интереса");
        if (d.pois.Count == 0) sb.AppendLine("Нет.");
        else
        {
            sb.AppendLine("| Тип | Объект | Позиция (x, y, z) | Заметка |");
            sb.AppendLine("|---|---|---|---|");
            d.pois.Sort((a, b) => string.Compare(a.kind + a.name, b.kind + b.name, StringComparison.Ordinal));
            foreach (var p in d.pois)
                sb.AppendLine("| " + p.kind + " | " + p.name + " | (" + F(p.pos.x) + ", " + F(p.pos.y) + ", " + F(p.pos.z) + ") | " + p.extra + " |");
        }
        sb.AppendLine();

        sb.AppendLine("## 6. Маркеры генератора и площадки");
        if (d.markers.Count == 0) sb.AppendLine("Нет (Location/Markers и Location/Pads пусты или не найдены).");
        else foreach (var m in d.markers) sb.AppendLine("- " + m.kind + "/" + m.name + ": (" + F(m.pos.x) + ", " + F(m.pos.y) + ", " + F(m.pos.z) + ")");
        sb.AppendLine();
        if (d.padsText.Length > 0)
        {
            sb.AppendLine("Раскладка площадок в настройках (угол от севера по часовой, расстояние в u = метры / worldScale):");
            sb.AppendLine("| Площадка | Угол | Расстояние | Размер | Откуда тропа |");
            sb.AppendLine("|---|---|---|---|---|");
            sb.Append(d.padsText);
            sb.AppendLine();
        }

        sb.AppendLine("## 7. Тропы леса");
        if (d.trails.Count == 0) sb.AppendLine("Объекта ForestTrails нет в сцене.");
        else foreach (var t in d.trails) sb.AppendLine("- " + t);
        sb.AppendLine();

        sb.AppendLine("## 8. Иерархия (глубина 2)");
        sb.AppendLine("```");
        foreach (var l in d.hierarchy) sb.AppendLine(l);
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## 9. Настройки LocationBuilder (все числа и флаги)");
        sb.AppendLine("```");
        sb.AppendLine(d.settingsText);
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## 10. Картинки");
        sb.AppendLine(shots ? "island_top.png (строго сверху, весь остров), island_view.png (обзор под углом 45°), island_side.png (низкий вид сбоку: край, водопад, подбрюшье)." : "Картинки не снимались (режим «только текст»).");
    }

    static void AppendGrid(StringBuilder sb, char[,] g, int rows)
    {
        for (int r = 0; r < rows; r++)
        {
            var line = new StringBuilder(Cols);
            for (int c = 0; c < Cols; c++) line.Append(g[r, c] == 0 ? ' ' : g[r, c]);
            sb.AppendLine(line.ToString().TrimEnd());
        }
    }

    static void WriteRiverRows(StringBuilder sb, Data d)
    {
        sb.AppendLine("## 3b. Река по рядам (с севера на юг)");
        if (!d.waterFound) { sb.AppendLine("Вода не найдена."); sb.AppendLine(); return; }
        sb.AppendLine("| z (центр ряда) | x от … до | ширина |");
        sb.AppendLine("|---|---|---|");
        for (int r = 0; r < d.rows; r += 2)
        {
            int first = -1, last = -1;
            for (int c = 0; c < Cols; c++) if (d.water[r, c]) { if (first < 0) first = c; last = c; }
            if (first < 0) continue;
            Vector2 a = CellCenter(d, first, r), b = CellCenter(d, last, r);
            sb.AppendLine("| " + F(a.y) + " | " + F(a.x - d.cellX * 0.5f) + " … " + F(b.x + d.cellX * 0.5f) + " | " + F(b.x - a.x + d.cellX) + " |");
        }
        sb.AppendLine();
    }

    // ------------------------------------------------------------------ скриншоты

    static void TakeShots(Data d, string dir)
    {
        Vector3 center = new Vector3(d.centroid.x, d.hasTerrain ? d.tPos.y + (d.hMin + d.hMax) * 0.5f : 0f, d.centroid.y);
        float R = Mathf.Max(d.bbox.width, d.bbox.height) * 0.5f;

        // 1. строго сверху
        var cam = NewCamera();
        try
        {
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(d.bbox.height * 0.5f, d.bbox.width * 0.5f / ((float)ShotW / ShotH)) * 1.04f;
            cam.transform.position = new Vector3(d.bbox.center.x, d.hMax + 300f, d.bbox.center.y);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.nearClipPlane = 1f; cam.farClipPlane = 1500f;
            Save(cam, Path.Combine(dir, "island_top.png"));
        }
        finally { UnityEngine.Object.DestroyImmediate(cam.gameObject); }

        // 2. обзор под 45°
        Persp(center, R, 45f, 45f, 1.05f, Path.Combine(dir, "island_view.png"));
        // 3. низкий вид сбоку (с юга): край острова, водопад, подбрюшье
        Persp(center, R, 14f, 0f, 1.25f, Path.Combine(dir, "island_side.png"));
    }

    static void Persp(Vector3 target, float radius, float pitch, float yaw, float k, string file)
    {
        var cam = NewCamera();
        try
        {
            cam.orthographic = false;
            cam.fieldOfView = 35f;
            float dist = radius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * k;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.rotation = rot;
            cam.transform.position = target - rot * Vector3.forward * dist;
            cam.nearClipPlane = 1f; cam.farClipPlane = dist + radius * 6f;
            Save(cam, file);
        }
        finally { UnityEngine.Object.DestroyImmediate(cam.gameObject); }
    }

    static Camera NewCamera()
    {
        var go = new GameObject("IslandSnapshotCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        var main = Camera.main;
        if (main != null) cam.CopyFrom(main);           // слои, пост-обработка, фон: как в игре
        else { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.12f, 0.16f, 0.2f); }
        cam.targetTexture = null;
        return cam;
    }

    static void Save(Camera cam, string file)
    {
        var rt = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        var prev = RenderTexture.active;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
        finally
        {
            cam.targetTexture = null;
            RenderTexture.active = prev;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
#endif
