using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class TWTreePolishBuilder
{
    const string Menu = "Tools/BuilderSim/UI/Полировка рубки/";
    const string Art = "Assets/_Project/Art/TreePolish";
    [MenuItem(Menu + "1. Применить визуальную полировку", false, 240)]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("TREE POLISH: останови Play."); return; }
        var scene = SceneManager.GetActiveScene(); int group = -1;
        try
        {
            if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Сначала сохрани игровую сцену.");
            var player = One<PlayerInteraction>(scene); var ui = One<UIManager>(scene);
            if (player == null || ui == null || ui.coinText == null) throw new InvalidOperationException("В активной сцене нужны PlayerInteraction и UIManager.");
            var canvas = ui.coinText.GetComponentInParent<Canvas>(); if (canvas == null) throw new InvalidOperationException("Не найден Canvas HUD."); canvas = canvas.rootCanvas;
            var font = ui.coinText.font; if (font == null) throw new InvalidOperationException("Не назначен шрифт HUD.");
            var outlineShader = Shader.Find("TimberWings/Tree Focus Outline"); var groundShader = Shader.Find("TimberWings/Tree Focus Ground");
            if (outlineShader == null || groundShader == null) throw new InvalidOperationException("Нет шейдеров Tree Focus. Скопируй Assets целиком и дождись импорта.");
            var panel = Need("Panels/panel_chip"); var log = Need("Icons/icon_log");
            Directory.CreateDirectory(Art);
            var outline = Material("outline", outlineShader); var ground = Material("ground", groundShader);
            var ring = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "/ring.asset");
            if (ring == null) { ring = Ring(); AssetDatabase.CreateAsset(ring, Art + "/ring.asset"); }
            var arrow = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "/heading.asset");
            if (arrow == null) { arrow = Arrow(); AssetDatabase.CreateAsset(arrow, Art + "/heading.asset"); }
            var resources = All<ResourceItem>(scene);
            var unchanged = new Dictionary<ResourceItem, string>(); foreach (var item in resources) unchanged[item] = EditorJsonUtility.ToJson(item);
            var pairs = new List<TreePolishController.MeshPair>(); var bindings = new List<TreePolishController.MeshBinding>();
            var meshes = new Dictionary<Mesh, Mesh>(); int skipped = 0;
            foreach (var item in resources) foreach (var filter in item.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh source = filter.sharedMesh; if (source == null || filter.name == "Hull" || filter.name == "HighlightRing") continue;
                Mesh shell;
                if (!meshes.TryGetValue(source, out shell))
                {
                    try { shell = SmoothShell(source); } catch (Exception) { shell = null; skipped++; }
                    meshes[source] = shell; if (shell != null) pairs.Add(new TreePolishController.MeshPair { source = source, shell = shell });
                }
                bindings.Add(new TreePolishController.MeshBinding { filter = filter, source = source, shell = shell });
            }
            AssetDatabase.SaveAssets();
            string dir = "UserSettings/TW_TreePolishBackups/" + scene.name; Directory.CreateDirectory(dir);
            string backup = dir + "/" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity";
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Не удалось сохранить копию сцены.");
            Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Tree Polish: оформление взаимодействия");
            var owner = One<TreePolishController>(scene);
            if (owner == null)
            {
                var root = new GameObject("TW_TreePolish"); Undo.RegisterCreatedObjectUndo(root, "Tree Polish");
                owner = Undo.AddComponent<TreePolishController>(root);
            }
            Undo.RegisterCompleteObjectUndo(owner, "Tree Polish: настройки");
            RemoveUI(owner);
            owner.backup = backup; owner.interaction = player; owner.canvas = canvas;
            var prompt = One<ActionPrompt>(scene);
            owner.promptRect = prompt != null ? (prompt.group != null ? prompt.group.transform as RectTransform : prompt.transform as RectTransform) : null;
            owner.outline = outline; owner.ground = ground; owner.ring = ring; owner.chevron = arrow; owner.shells = pairs.ToArray(); owner.bindings = bindings.ToArray();
            foreach (var old in All<ResourceHighlighter>(scene)) Disable(owner, old);
            foreach (var old in All<InteractionHint>(scene)) Disable(owner, old);
            var card = NewUI(owner, "TreeFocusCard", canvas.transform); var bg = Undo.AddComponent<Image>(card.gameObject);
            bg.sprite = panel; bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 1.1f; bg.raycastTarget = false;
            owner.card = Undo.AddComponent<CanvasGroup>(card.gameObject); owner.card.alpha = 0; owner.card.blocksRaycasts = false; owner.card.interactable = false;
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, 0); card.anchoredPosition = new Vector2(0, 166); card.sizeDelta = new Vector2(386, 82);
            var icon = NewUI(owner, "Icon", card); owner.cardIcon = Undo.AddComponent<Image>(icon.gameObject);
            owner.cardIcon.sprite = log; owner.cardIcon.raycastTarget = false; owner.cardIcon.preserveAspect = true;
            HudPolishRoot.Anchor(icon, new Vector2(0,.5f), new Vector2(.5f,.5f), new Vector2(36,0), new Vector2(52,52));
            owner.amount = Text(owner, "BaseYield", card, font, 25); owner.detail = Text(owner, "FocusState", card, font, 18);
            owner.amount.text = "База: +2 бревна"; owner.detail.text = "Дерево выбрано"; owner.detail.color = CandyTag.Muted;
            Stretch(owner.amount.rectTransform, 74, 35, 24, 12); Stretch(owner.detail.rectTransform, 74, 12, 24, 44);
            var rewards = One<ChopRewards>(scene);
            if (rewards == null) { rewards = Undo.AddComponent<ChopRewards>(owner.gameObject); owner.ownsRewards = true; }
            Undo.RecordObject(rewards, "Tree Polish: пул полёта");
            if (!owner.poolCaptured) { owner.previousPoolRoot = rewards.logPoolRoot; owner.previousPool = rewards.logPool; owner.poolCaptured = true; }
            owner.rewards = rewards;
            var pool = NewUI(owner, "TreeLogFlightPool", canvas.transform); Stretch(pool);
            var poolGroup = Undo.AddComponent<CanvasGroup>(pool.gameObject); poolGroup.blocksRaycasts = false; poolGroup.interactable = false;
            rewards.logPoolRoot = pool; rewards.logPool = new Image[32];
            for (int i = 0; i < 32; i++)
            {
                var slot = NewUI(owner, "PooledLog_" + i, pool); var im = Undo.AddComponent<Image>(slot.gameObject);
                im.sprite = log; im.preserveAspect = true; im.raycastTarget = false; im.enabled = false; slot.sizeDelta = Vector2.one * rewards.iconSize;
                rewards.logPool[i] = im;
            }
            foreach (var item in unchanged) if (item.Key != null && item.Value != EditorJsonUtility.ToJson(item.Key))
                throw new InvalidOperationException("Изменился ресурсный компонент: " + item.Key.name);
            EditorUtility.SetDirty(owner); EditorUtility.SetDirty(rewards);
            var problems = Inspect(owner); if (problems.Count > 0) throw new InvalidOperationException(string.Join("\n", problems));
            Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = owner.gameObject;
            Debug.Log("TREE POLISH: готово. Одна карточка цели, контур, указатель движения и 32 иконки в пуле. Мешей контура: " + pairs.Count + ", запасной режим: " + skipped + ". Ctrl+S. Копия сцены: " + backup);
        }
        catch (Exception ex)
        {
            if (group >= 0) Undo.RevertAllDownToGroup(group);
            Debug.LogError("TREE POLISH: сборка отменена, изменения сцены отменены. " + ex);
        }
    }
    static List<T> All<T>(Scene scene) where T : Component
    { var items = new List<T>(); foreach (var root in scene.GetRootGameObjects()) items.AddRange(root.GetComponentsInChildren<T>(true)); return items; }
    static T One<T>(Scene scene) where T : Component
    {
        T result = null; foreach (var item in All<T>(scene)) if (item.gameObject.activeInHierarchy)
        { if (result != null) throw new InvalidOperationException("В активной сцене несколько " + typeof(T).Name); result = item; }
        return result;
    }
    static Sprite Need(string key)
    { var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CandyUiKitGenerator.OutDir + "/" + key + ".png"); if (sprite == null) throw new InvalidOperationException("Нет Candy Kit: " + key); return sprite; }
    static Material Material(string name, Shader shader)
    {
        string path = Art + "/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); mat.name = "TW_TreeFocus_" + name; AssetDatabase.CreateAsset(mat, path); }
        return mat;
    }
    static void Disable(TreePolishController owner, Behaviour old)
    {
        bool captured = false; foreach (var entry in owner.legacy) if (entry.target == old) captured = true;
        if (!captured) owner.legacy.Add(new TreePolishController.Legacy { target = old, enabled = old.enabled });
        Undo.RecordObject(old, "Tree Polish: прежняя подсказка"); old.enabled = false;
    }
    static RectTransform NewUI(TreePolishController owner, string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = canvasLayer(parent); go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Tree Polish UI"); owner.ownedUI.Add(go); return go.transform as RectTransform;
    }
    static int canvasLayer(Transform parent) { return parent != null ? parent.gameObject.layer : 5; }
    static TMP_Text Text(TreePolishController owner, string name, Transform parent, TMP_FontAsset font, float size)
    {
        var r = NewUI(owner, name, parent); var t = Undo.AddComponent<TextMeshProUGUI>(r.gameObject);
        t.font = font; t.fontSize = size; t.fontSizeMax = size; t.fontSizeMin = Mathf.Min(16, size); t.enableAutoSizing = true;
        t.color = CandyTag.Ink; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.MidlineLeft;
        t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap; return t;
    }
    static void Stretch(RectTransform r, float l = 0, float b = 0, float right = 0, float top = 0)
    { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(l,b); r.offsetMax = new Vector2(-right,-top); }
    static void RemoveUI(TreePolishController owner)
    { for (int i = owner.ownedUI.Count - 1; i >= 0; i--) if (owner.ownedUI[i] != null) Undo.DestroyObjectImmediate(owner.ownedUI[i]); owner.ownedUI.Clear(); }
    public static Mesh Ring()
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int segment = 0; segment < 28; segment++)
        {
            float a = segment * Mathf.PI * 2 / 28f, b = a + Mathf.PI * 2 / 28f * .74f; int start = vertices.Count;
            vertices.Add(new Vector3(Mathf.Sin(a),0,Mathf.Cos(a)) * .955f); vertices.Add(new Vector3(Mathf.Sin(a),0,Mathf.Cos(a)));
            vertices.Add(new Vector3(Mathf.Sin(b),0,Mathf.Cos(b))); vertices.Add(new Vector3(Mathf.Sin(b),0,Mathf.Cos(b)) * .955f);
            triangles.AddRange(new[] { start,start+1,start+2,start,start+2,start+3 });
        }
        var mesh = new Mesh { name = "TW_FocusRing" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds(); return mesh;
    }
    public static Mesh Arrow()
    {
        var mesh = new Mesh { name = "TW_WalkChevron" };
        mesh.vertices = new[] { new Vector3(-.6f,0,-.3f),new Vector3(0,0,.3f),new Vector3(.6f,0,-.3f),new Vector3(.6f,0,.02f),new Vector3(0,0,.64f),new Vector3(-.6f,0,.02f) };
        mesh.triangles = new[] {0,1,5,5,1,4,1,2,4,4,2,3}; mesh.RecalculateBounds(); return mesh;
    }
    public static Mesh SmoothShell(Mesh source)
    {
        if (source.vertexCount > 200000) return null;
        string guid; long local;
        string id = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out guid, out local) ? guid + "_" + local : "scene_" + source.GetInstanceID();
        string path = Art + "/shell_" + id + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
        using (var data = MeshUtility.AcquireReadOnlyMeshData(source))
        using (var positions = new NativeArray<Vector3>(source.vertexCount, Allocator.Temp))
        using (var normals = new NativeArray<Vector3>(source.vertexCount, Allocator.Temp))
        {
            var input = data[0]; input.GetVertices(positions);
            if (source.HasVertexAttribute(VertexAttribute.Normal)) input.GetNormals(normals);
            var sums = new Dictionary<Vector3Int, Vector3>();
            for (int i = 0; i < positions.Length; i++)
            {
                var key = Quantize(positions[i]); Vector3 sum; sums.TryGetValue(key, out sum);
                var n = normals[i]; if (n.sqrMagnitude < .0001f) n = positions[i] - source.bounds.center;
                sums[key] = sum + n;
            }
            var smooth = new Vector3[positions.Length];
            for (int i = 0; i < smooth.Length; i++) { var n = sums[Quantize(positions[i])]; if (n.sqrMagnitude < .0001f) n = positions[i] - source.bounds.center; smooth[i] = n.sqrMagnitude > .0001f ? n.normalized : Vector3.up; }
            var result = new Mesh { name = source.name + "_FocusShell", indexFormat = source.vertexCount > 65535 ? IndexFormat.UInt32 : source.indexFormat };
            result.vertices = positions.ToArray(); result.normals = smooth; result.subMeshCount = input.subMeshCount;
            for (int i = 0; i < input.subMeshCount; i++)
            {
                var sub = input.GetSubMesh(i);
                using (var indices = new NativeArray<int>(sub.indexCount, Allocator.Temp))
                { input.GetIndices(indices, i, true); result.SetIndices(indices.ToArray(), sub.topology, i, false); }
            }
            result.bounds = source.bounds; AssetDatabase.CreateAsset(result,path); return result;
        }
    }
    static Vector3Int Quantize(Vector3 p) { return new Vector3Int(Mathf.RoundToInt(p.x * 10000),Mathf.RoundToInt(p.y * 10000),Mathf.RoundToInt(p.z * 10000)); }
    static List<string> Inspect(TreePolishController owner)
    {
        var errors = new List<string>();
        if (owner == null) { errors.Add("Нет TreePolishController. Запусти пункт 1."); return errors; }
        if (owner.interaction == null || owner.card == null || owner.amount == null || owner.detail == null || owner.canvas == null) errors.Add("Не назначены ссылки на игрока/карточку.");
        if (owner.outline == null || owner.ground == null || owner.ring == null || owner.chevron == null) errors.Add("Нет материалов или мешей оформления.");
        if (owner.rewards == null || owner.rewards.logPool == null || owner.rewards.logPool.Length != 32) errors.Add("Пул полёта должен содержать 32 иконки.");
        foreach (var go in owner.ownedUI) if (go != null) foreach (var graphic in go.GetComponents<Graphic>()) if (graphic.raycastTarget) errors.Add("Декор перехватывает ввод: " + go.name);
        return errors;
    }
    [MenuItem(Menu + "2. Проверить оформление", false, 241)]
    static void QA()
    {
        try { var issues = Inspect(One<TreePolishController>(SceneManager.GetActiveScene())); if (issues.Count > 0) Debug.LogError("TREE POLISH QA: " + string.Join("\n",issues)); else Debug.Log("TREE POLISH QA OK: одна карточка цели, пул, материалы, геометрия и raycast. Контур и полёт проверь в Play Mode."); }
        catch (Exception ex) { Debug.LogError("TREE POLISH QA: " + ex.Message); }
    }
    [MenuItem(Menu + "3. Вернуть прежнее оформление", false, 242)]
    static void Revert()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene(); var owner = One<TreePolishController>(scene); if (owner == null) return;
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Tree Polish: откат");
        foreach (var old in owner.legacy) if (old.target != null) { Undo.RecordObject(old.target,"Tree Polish: компонент"); old.target.enabled = old.enabled; }
        if (owner.rewards != null && !owner.ownsRewards)
        { Undo.RecordObject(owner.rewards,"Tree Polish: пул"); owner.rewards.logPoolRoot = owner.previousPoolRoot; owner.rewards.logPool = owner.previousPool; }
        RemoveUI(owner); Undo.DestroyObjectImmediate(owner.gameObject); Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("TREE POLISH: прежнее оформление сцены возвращено. Ctrl+S. Исходные скрипты для полного отката лежат в Source_Before_TreePolish.");
    }
}
