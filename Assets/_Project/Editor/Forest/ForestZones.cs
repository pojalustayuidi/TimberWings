#if UNITY_EDITOR
// Assets/_Project/Editor/Island/Forest/ForestZones.cs (Editor)
// Зоны леса: поиск, создание, раскладка подзон, гизмо и ручки изменения размера в окне Scene.
//
// Главная зона леса (Forest) берётся так: выделенная MapZone, затем площадка леса из LocationBuilder («Кедровый Бор»),
// и только если её нет, создаётся новая круглая зона в центре вида Scene.
// Подзоны Cedar, Birch, Autumn раскладываются внутри главной зоны (ForestZoneLayout).
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ForestZones
{
    public const string ZonesRootName = "Forest Zones";
    public const string TreesRootName = "ForestTrees";
    public const string ManualName = "Manual";

    struct Spec
    {
        public ForestBiome biome; public string objName, zoneName, caption; public int priority; public Color color;
        public Spec(ForestBiome b, string o, string z, string c, int p, Color col) { biome = b; objName = o; zoneName = z; caption = c; priority = p; color = col; }
    }

    static readonly Spec MasterSpec = new Spec(ForestBiome.Forest, "Forest", "Лес", "", 0, new Color(0.30f, 0.62f, 0.32f));
    // порядок = слоты ForestZoneLayout.Compute: Cedar, Birch, Autumn
    static readonly Spec[] SubSpecs =
    {
        new Spec(ForestBiome.Cedar, "Cedar", "Кедровник", "ТЁМНЫЙ ЛЕС", 10, new Color(0.15f, 0.45f, 0.35f)),
        new Spec(ForestBiome.Birch, "Birch", "Берёзовая поляна", "СВЕТЛЫЙ ЛЕС", 20, new Color(0.85f, 0.90f, 0.50f)),
        new Spec(ForestBiome.Autumn, "Autumn", "Осенний уголок", "ОСЕННИЙ ЛЕС", 20, new Color(0.95f, 0.55f, 0.20f))
    };

    // ------------------------------------------------------------ корни в сцене
    public static Transform TreesRoot(bool create)
    {
        var go = GameObject.Find(TreesRootName);
        if (go != null) return go.transform;
        if (!create) return null;
        go = new GameObject(TreesRootName);
        Undo.RegisterCreatedObjectUndo(go, "Create " + TreesRootName);
        return go.transform;
    }

    public static Transform ManualRoot(bool create)
    {
        Transform root = TreesRoot(create);
        if (root == null) return null;
        Transform m = root.Find(ManualName);
        if (m != null || !create) return m;
        var go = new GameObject(ManualName);
        Undo.RegisterCreatedObjectUndo(go, "Create " + ManualName);
        go.transform.SetParent(root, false);
        return go.transform;
    }

    static Transform ZonesRoot(bool create)
    {
        var go = GameObject.Find(ZonesRootName);
        if (go != null) return go.transform;
        if (!create) return null;
        go = new GameObject(ZonesRootName);
        Undo.RegisterCreatedObjectUndo(go, "Create " + ZonesRootName);
        return go.transform;
    }

    // ------------------------------------------------------------ поиск
    public static List<ForestZone> SceneZones()
    {
        var res = new List<ForestZone>();
        var all = Object.FindObjectsByType<ForestZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].gameObject.scene.IsValid() && !EditorSceneManager.IsPreviewScene(all[i].gameObject.scene)) res.Add(all[i]);
        return res;
    }

    public static ForestZone Find(ForestBiome b)
    {
        var l = SceneZones();
        ForestZone best = null;
        for (int i = 0; i < l.Count; i++)
            if (l[i].biome == b && (best == null || l[i].Map.Area > best.Map.Area)) best = l[i];
        return best;
    }

    // ------------------------------------------------------------ создание
    public static string CreateMaster()
    {
        var existing = Find(ForestBiome.Forest);
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            return "Зона леса уже есть: «" + existing.Map.zoneName + "».";
        }

        // 1. выделенная MapZone
        MapZone src = null;
        var sel = Selection.activeGameObject;
        if (sel != null && sel.scene.IsValid()) src = sel.GetComponent<MapZone>();
        string from = "выделенная зона";
        // 2. площадка леса из LocationBuilder
        if (src == null) { src = ForestZoneLayout.FindForestMapZone(); from = "площадка леса из LocationBuilder"; }

        ForestZone fz;
        string msg;
        if (src != null)
        {
            if (src.GetComponent<ForestZone>() != null) { Selection.activeGameObject = src.gameObject; return "На выбранной зоне уже стоит лесная зона."; }
            fz = Undo.AddComponent<ForestZone>(src.gameObject);
            fz.biome = ForestBiome.Forest;
            msg = "Зона леса = «" + src.zoneName + "» (" + from + ").";
        }
        else
        {
            // 3. новая круглая зона
            var go = new GameObject(MasterSpec.objName);
            Undo.RegisterCreatedObjectUndo(go, "Create forest zone");
            go.transform.SetParent(ZonesRoot(true), false);
            Vector3 c = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            RaycastHit hit;
            if (Physics.Raycast(c + Vector3.up * 500f, Vector3.down, out hit, 1000f)) c = hit.point;
            go.transform.position = c;
            fz = Undo.AddComponent<ForestZone>(go);
            var mz = fz.Map;
            mz.zoneName = MasterSpec.zoneName; mz.caption = MasterSpec.caption; mz.priority = MasterSpec.priority;
            mz.color = MasterSpec.color; mz.announce = false;
            mz.shape = MapZone.Shape.Circle; mz.radius = 30f;
            msg = "Создана новая круглая зона леса (радиус 30 м) в центре вида. Растяни её ручками в Scene.";
        }
        EditorUtility.SetDirty(fz);
        EditorSceneManager.MarkSceneDirty(fz.gameObject.scene);
        Selection.activeGameObject = fz.gameObject;
        return msg;
    }

    public static string CreateSubZones()
    {
        var master = Find(ForestBiome.Forest);
        if (master == null) return "Сначала создай зону леса.";

        float y = master.transform.position.y;
        var slots = ForestZoneLayout.Compute(master.Map, ForestZoneLayout.EntryDir(master.transform.position));
        var made = new List<string>();
        for (int i = 0; i < SubSpecs.Length; i++)
        {
            if (Find(SubSpecs[i].biome) != null) continue;
            var s = SubSpecs[i];
            var go = new GameObject(s.objName);
            Undo.RegisterCreatedObjectUndo(go, "Create forest subzones");
            go.transform.SetParent(ZonesRoot(true), false);
            go.transform.position = new Vector3(slots[i].pos.x, y, slots[i].pos.z);
            var fz = Undo.AddComponent<ForestZone>(go);
            fz.biome = s.biome;
            var mz = fz.Map;
            mz.zoneName = s.zoneName; mz.caption = s.caption; mz.priority = s.priority; mz.color = s.color; mz.announce = true;
            mz.shape = MapZone.Shape.Circle; mz.radius = slots[i].radius;
            made.Add(s.zoneName);
        }
        EditorSceneManager.MarkSceneDirty(master.gameObject.scene);
        return made.Count == 0 ? "Все подзоны уже есть." : "Созданы подзоны: " + string.Join(", ", made) + ".";
    }

    // ------------------------------------------------------------ камера в лесу (игровая ForestCameraZone)
    public static string CreateCameraZone()
    {
        var master = Find(ForestBiome.Forest);
        if (master == null) return "Сначала создай зону леса.";
        var cz = Object.FindFirstObjectByType<ForestCameraZone>();
        MapZone m = master.Map;
        float r = m.shape == MapZone.Shape.Circle ? m.radius : Mathf.Min(m.size.x, m.size.y) * 0.5f;
        if (cz == null)
        {
            var go = new GameObject("ForestCameraZone");
            Undo.RegisterCreatedObjectUndo(go, "Create ForestCameraZone");
            cz = go.AddComponent<ForestCameraZone>();
            cz.ResetToRecommended();
        }
        else Undo.RecordObject(cz, "Update ForestCameraZone");
        cz.transform.position = master.transform.position;
        cz.radius = r * 0.85f;
        cz.fade = Mathf.Max(2f, r * 0.15f);
        EditorUtility.SetDirty(cz);
        EditorSceneManager.MarkSceneDirty(cz.gameObject.scene);
        return "Камера леса: центр зоны, радиус " + cz.radius.ToString("0.#") + " м.";
    }

    // ------------------------------------------------------------ выделенное -> ForestTrees/Manual
    public static string SelectionToManual()
    {
        var objs = Selection.gameObjects;
        if (objs.Length == 0) return "Ничего не выделено.";
        Transform manual = ManualRoot(true);
        int n = 0;
        foreach (var go in objs)
        {
            if (go == null || !go.scene.IsValid() || go.transform == manual || go.transform.IsChildOf(manual) || go.GetComponent<MapZone>() != null) continue;
            Undo.SetTransformParent(go.transform, manual, "Move to Manual");
            n++;
        }
        return "Перенесено в " + TreesRootName + "/" + ManualName + ": " + n + ". Генератор эти деревья не трогает.";
    }

    // ------------------------------------------------------------ перенос из старого леса
    static readonly string[] OldRoots =
    {
        "ForestTreeGenerator", "ForestPlacementFilter", "ForestZoneBootstrap", "ForestUnderstory", "ForestGrass", "ForestAtmosphere",
        "ForestGroundPaint", "ForestGround", "ForestMaskRunner", "ForestMaskDebugQuad", "ForestTreePalette"
    };

    public static string MigrateOld()
    {
        int moved = 0, removed = 0;
        Undo.IncrementCurrentGroup();
        int grp = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Migrate old forest");

        var gen = GameObject.Find("ForestGenerated");
        if (gen != null)
        {
            for (int i = gen.transform.childCount - 1; i >= 0; i--)
            {
                Transform c = gen.transform.GetChild(i);
                if (!c.name.StartsWith("_Trees_")) continue;
                string biome = c.name.Substring("_Trees_".Length);
                Transform root = TreesRoot(true);
                Transform dst = root.Find(biome);
                if (dst == null)
                {
                    Undo.SetTransformParent(c, root, "Migrate");
                    c.name = biome;
                    moved += c.childCount;
                }
                else
                {
                    for (int k = c.childCount - 1; k >= 0; k--) { Undo.SetTransformParent(c.GetChild(k), dst, "Migrate"); moved++; }
                    Undo.DestroyObjectImmediate(c.gameObject);
                }
            }
            if (gen.transform.childCount == 0) { Undo.DestroyObjectImmediate(gen); removed++; }
        }

        var man = GameObject.Find("ForestManual");
        if (man != null)
        {
            Transform dst = ManualRoot(true);
            for (int i = man.transform.childCount - 1; i >= 0; i--) { Undo.SetTransformParent(man.transform.GetChild(i), dst, "Migrate"); moved++; }
            Undo.DestroyObjectImmediate(man); removed++;
        }

        // старые объекты-носители рантайм-компонентов (без пользовательского содержимого)
        for (int i = 0; i < OldRoots.Length; i++)
        {
            var go = GameObject.Find(OldRoots[i]);
            if (go == null) continue;
            if (go.transform.childCount > 0 && OldRoots[i] != "ForestGround" && OldRoots[i] != "ForestGroundPaint") continue;
            Undo.DestroyObjectImmediate(go); removed++;
        }

        // старые деревья, которые остались в _Trees_* вне ForestGenerated (контейнеры под генератором)
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !t.name.StartsWith("_Trees_") || !t.gameObject.scene.IsValid()) continue;
            string biome = t.name.Substring("_Trees_".Length);
            Transform root = TreesRoot(true);
            Transform dst = root.Find(biome);
            if (dst == null) { var go = new GameObject(biome); Undo.RegisterCreatedObjectUndo(go, "Migrate"); go.transform.SetParent(root, false); dst = go.transform; }
            for (int k = t.childCount - 1; k >= 0; k--) { Undo.SetTransformParent(t.GetChild(k), dst, "Migrate"); moved++; }
            Undo.DestroyObjectImmediate(t.gameObject); removed++;
        }

        Undo.CollapseUndoOperations(grp);
        EditorSceneManager.MarkAllScenesDirty();
        return "Перенесено деревьев: " + moved + ", удалено старых объектов: " + removed + ". Дальше: Tools → Убрать слетевшие скрипты (Missing).";
    }

    // ------------------------------------------------------------ гизмо и ручки
    static GUIStyle labelStyle;

    public static Color BiomeColor(ForestBiome b)
    {
        switch (b)
        {
            case ForestBiome.Cedar: return new Color(0.10f, 0.72f, 0.62f);
            case ForestBiome.Birch: return new Color(0.92f, 0.90f, 0.25f);
            case ForestBiome.Autumn: return new Color(1.00f, 0.50f, 0.12f);
            case ForestBiome.Dev: return new Color(0.85f, 0.35f, 0.90f);
            default: return new Color(0.30f, 0.85f, 0.35f);
        }
    }

    static Vector3[] Outline(MapZone m, float inset, float y)
    {
        Vector3 c = m.transform.position; c.y = y;
        if (m.shape == MapZone.Shape.Circle)
        {
            float r = m.radius - inset;
            if (r <= 0.05f) return null;
            const int n = 64;
            var pts = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                pts[i] = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            }
            return pts;
        }
        float hx = m.size.x * 0.5f - inset, hz = m.size.y * 0.5f - inset;
        if (hx <= 0.05f || hz <= 0.05f) return null;
        Quaternion q = Quaternion.Euler(0f, m.transform.eulerAngles.y, 0f);
        return new[]
        {
            c + q * new Vector3(-hx, 0f, -hz), c + q * new Vector3(hx, 0f, -hz),
            c + q * new Vector3(hx, 0f, hz), c + q * new Vector3(-hx, 0f, hz), c + q * new Vector3(-hx, 0f, -hz)
        };
    }

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]

    [InitializeOnLoadMethod]
    static void HookScene() { SceneView.duringSceneGui += OnSceneGui; }

    // ручки: круг тянется за радиус, прямоугольник за четыре грани
    static void OnSceneGui(SceneView sv)
    {
        if (Application.isPlaying) return;
        var go = Selection.activeGameObject;
        if (go == null) return;
        var z = go.GetComponent<ForestZone>();
        if (z == null) return;
        MapZone m = z.Map;
        Vector3 c = m.transform.position;

        EditorGUI.BeginChangeCheck();
        if (m.shape == MapZone.Shape.Circle)
        {
            Handles.color = BiomeColor(z.biome);
            float r = Handles.RadiusHandle(Quaternion.identity, c, m.radius);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m, "Resize forest zone");
                m.radius = Mathf.Max(1f, r);
                EditorUtility.SetDirty(m);
            }
            return;
        }

        Quaternion q = Quaternion.Euler(0f, m.transform.eulerAngles.y, 0f);
        Vector3 right = q * Vector3.right, fwd = q * Vector3.forward;
        Vector2 size = m.size;
        Handles.color = BiomeColor(z.biome);
        size.x = Mathf.Max(1f, Edge(c, right, size.x * 0.5f) * 2f);
        size.x = Mathf.Max(1f, Edge(c, -right, size.x * 0.5f) * 2f);
        size.y = Mathf.Max(1f, Edge(c, fwd, size.y * 0.5f) * 2f);
        size.y = Mathf.Max(1f, Edge(c, -fwd, size.y * 0.5f) * 2f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(m, "Resize forest zone");
            m.size = size;
            EditorUtility.SetDirty(m);
        }
    }

    static float Edge(Vector3 c, Vector3 dir, float half)
    {
        Vector3 p = c + dir * half;
        Vector3 np = Handles.Slider(p, dir, HandleUtility.GetHandleSize(p) * 0.12f, Handles.CubeHandleCap, 0f);
        return Mathf.Max(0.5f, Vector3.Dot(np - c, dir));
    }
}
#endif
