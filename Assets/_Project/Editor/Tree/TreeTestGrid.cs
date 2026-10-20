#if UNITY_EDITOR
// Assets/_Project/Editor/Tree/TreeTestGrid.cs (Editor)
// Tools > Forest > Trees:
//   Тестовая сетка 20x20 (Manual)   ставит все виды и вариации в ForestTrees/Manual/TreeTest_20x20 (ряд = вид, столбец = вариация A-D), центр сетки = центр Scene view.
//   Убрать тестовую сетку           удаляет только TreeTest_20x20 (одно Ctrl+Z).
//   Игровая камера на сетку (55)    двигает Camera.main на 55 над сеткой (Ctrl+Z возвращает).
//   Скриншот игровой камеры         <проект>/Screenshots/Trees_55.png
// Рантайм-кода нет. Генератор леса папку Manual не трогает.
using System.IO;
using UnityEditor;
using UnityEngine;

public static class TreeTestGrid
{
    const string Dir = "Assets/_Project/Prefabs/Trees/Generated/";
    const string GridName = "TreeTest_20x20";
    const float RowStep = 5f;   // 5 видов: ряды по Z от -10 до +10 м
    const float ColStep = 6f;   // 4 вариации: столбцы по X от -9 до +9 м

    // ряд сверху вниз (дальний ряд = первый): основное, берёза, жёлтое, ёлка, дуб
    static readonly string[] Families = { "Tree_Random", "Tree_Birch", "Tree_Autumn", "Tree_Cedar", "Tree_Oak" };
    static readonly string[] Letters = { "A", "B", "C", "D" };

    [MenuItem("Tools/Forest/Trees/Тестовая сетка 20x20 (Manual)")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[TreeTest] Останови Play Mode."); return; }
        Transform manual = ForestZones.ManualRoot(true);
        if (manual == null) { Debug.LogWarning("[TreeTest] Не удалось создать ForestTrees/Manual."); return; }

        RemoveInternal();

        Vector3 c = Vector3.zero;
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) c = sv.pivot;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        var root = new GameObject(GridName);
        Undo.RegisterCreatedObjectUndo(root, "Tree test grid");
        root.transform.SetParent(manual, false);
        root.transform.position = c;

        int placed = 0, missing = 0;
        for (int row = 0; row < Families.Length; row++)
        {
            for (int col = 0; col < Letters.Length; col++)
            {
                string path = Dir + Families[row] + "_" + Letters[col] + "_s" + (col + 1) + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { if (col < 3) missing++; continue; }

                Vector3 p = c + new Vector3((col - 1.5f) * ColStep, 0f, (2f - row) * RowStep);
                p.y = GroundY(p, c.y);

                var go = PrefabUtility.InstantiatePrefab(prefab, root.transform) as GameObject;
                if (go == null) continue;
                Undo.RegisterCreatedObjectUndo(go, "Tree test grid");
                go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, col * 40f + row * 17f, 0f));
                placed++;
            }
        }
        Undo.CollapseUndoOperations(group);

        Selection.activeGameObject = root;
        Debug.Log("[TreeTest] Поставлено деревьев: " + placed + (missing > 0 ? ". Нет префабов: " + missing + " (сначала Tools > Forest > Trees > Build Species)" : "") + ". Центр сетки: " + c);
    }

    [MenuItem("Tools/Forest/Trees/Убрать тестовую сетку")]
    public static void Remove()
    {
        Undo.IncrementCurrentGroup();
        int n = RemoveInternal();
        Debug.Log("[TreeTest] Удалено сеток: " + n);
    }

    static int RemoveInternal()
    {
        Transform manual = ForestZones.ManualRoot(false);
        if (manual == null) return 0;
        int n = 0;
        for (int i = manual.childCount - 1; i >= 0; i--)
        {
            Transform ch = manual.GetChild(i);
            if (ch.name != GridName) continue;
            Undo.DestroyObjectImmediate(ch.gameObject);
            n++;
        }
        return n;
    }

    static float GroundY(Vector3 p, float fallback)
    {
        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.up * 100f, Vector3.down, out hit, 300f, ~0, QueryTriggerInteraction.Ignore)) return hit.point.y;
        return fallback;
    }

    [MenuItem("Tools/Forest/Trees/Игровая камера на сетку (55)")]
    public static void CameraToGrid()
    {
        GameObject g = GameObject.Find(GridName);
        Camera cam = Camera.main;
        if (g == null || cam == null) { Debug.LogWarning("[TreeTest] Нужны тестовая сетка и камера с тегом MainCamera."); return; }

        Vector3 center = g.transform.position + Vector3.up * 2f;
        Undo.RecordObject(cam.transform, "Camera to tree grid");
        Quaternion rot = Quaternion.Euler(55f, cam.transform.eulerAngles.y, 0f);
        cam.transform.rotation = rot;
        cam.transform.position = center - rot * Vector3.forward * 34f;
        Debug.Log("[TreeTest] Камера наведена на сетку под 55. Ctrl+Z вернёт её. Для кадра по ширине подвинь камеру вперёд/назад.");
    }

    [MenuItem("Tools/Forest/Trees/Скриншот игровой камеры")]
    public static void Screenshot()
    {
        Camera cam = Camera.main;
        if (cam == null) { Debug.LogWarning("[TreeTest] Нет камеры с тегом MainCamera."); return; }

        const int w = 1920, h = 1080;
        var rt = new RenderTexture(w, h, 24);
        RenderTexture prevT = cam.targetTexture;
        RenderTexture prevA = RenderTexture.active;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "Trees_55.png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log("[TreeTest] Скриншот: " + file);
            EditorUtility.RevealInFinder(file);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[TreeTest] Скриншот не получился (" + e.Message + "). Сделай снимок из Game view.");
        }
        finally
        {
            cam.targetTexture = prevT;
            RenderTexture.active = prevA;
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
#endif
