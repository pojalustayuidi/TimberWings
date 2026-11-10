#if UNITY_EDITOR
// Assets/_Project/Editor/GateBridge/GateBridgeBuilder.cs (Editor)
// Сборка подвесного моста и дальнего острова с финальной площадкой. Меню: Tools > BuilderSim > Локация > Собрать подвесной мост и финальный остров.
//
// ЧТО ТРОГАЕТ СБОРКА (безопасно для сцены с ручными правками):
//   * создаёт и пересоздаёт ТОЛЬКО свой корень GB_Final (под объектом GateBridge, на нём метка GateBridgeOwned). Всё внутри него генерируется заново;
//   * старые объекты прежней сборки под GateBridge (Stage1..3, StubIsland, Arch с табличкой, Guards, FallPoint0/1) НЕ удаляются, а выключаются
//     (если ты их правил руками, они остаются в сцене выключенными). «Следующая локация скоро» уходит вместе со старой аркой;
//   * компонент GateBridge: перезаписывает ссылки stages, deckStep2, walkway, noteSpot, arrivalSpot, fallPoints, plankMesh, plankMaterial;
//   * StoryFlow.noteSpot: ставит точку записки, если она пуста, была старой точкой GateBridge или лежит под GateBridge. Чужую точку не трогает, только пишет предупреждение;
//   * НЕ трогает: BuildingController моста (идентификатор SaveId считается по пути и виду стройки: не переименовывай и не переноси его объект),
//     RiverBridge, GateBridgePlacement, GateWalls, данные стройки, цены, главы StoryFlow.
//   Прежняя кнопка «Собрать мост и заглушку» удаляла ВСЕ дети GateBridge через DestroyImmediate: ручные правки терялись. Здесь так не делается.
// Всё откатывается Ctrl+Z (одна группа Undo).
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class GateBridgeBuilder
{
    public const string OwnedRootName = "GB_Final";
    static readonly string[] LegacyNames = { "Stage1", "Stage2", "Stage3", "StubIsland", "Arch", "Guards", "FallPoint0", "FallPoint1" };

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

    // План пути из контура острова и точки ворот (те же правила, что у прежней сборки)
    internal static GBPlan MakePlan(GateBridgePlacement fi, GateBridge gb, out string error)
    {
        error = null;
        var p = new GBPlan();
        p.E = new Vector2(fi.transform.position.x, fi.transform.position.z);
        Vector3 forward = fi.transform.forward;
        p.N = new Vector2(forward.x, forward.z).normalized;
        if (p.N.sqrMagnitude < 0.5f) { error = "Неправильное направление GateBridgePlacement."; return null; }
        p.Tn = new Vector2(p.N.y, -p.N.x);
        p.N3 = new Vector3(p.N.x, 0f, p.N.y);
        p.T3 = new Vector3(p.Tn.x, 0f, p.Tn.y);
        p.R = Mathf.Max(10f, gb.islandRadius);
        p.L = gb.bridgeLength;
        p.hw = gb.deckWidth * 0.5f;
        p.sag = gb.sag;
        p.y0 = Ground(p.E - p.N * 1.0f, fi.GroundY);
        p.yStart = p.y0 + 0.05f;
        p.yStubTop = p.y0 - gb.stubDrop;
        p.yEnd = p.yStubTop + 0.05f;
        p.S2 = p.E - p.N * 0.8f;
        p.Lb = p.L + 1.5f + 0.8f;
        p.Q2 = p.S2 + p.N * p.Lb;
        p.C2 = p.E + p.N * (p.L + p.R);
        p.sE = 0.8f;
        p.sF = 0.8f + p.L;
        return p;
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
        go.AddComponent<BoxCollider>().size = new Vector3(thickness, height, length);
    }

    static GateBridgeOwned FindOwned(Transform parent)
    {
        foreach (Transform t in parent)
        {
            var o = t.GetComponent<GateBridgeOwned>();
            if (o != null) return o;
        }
        return null;
    }

    public static string Build(GateBridge gb)
    {
        var log = new StringBuilder("=== Сборка моста и дальнего острова ===\n");
        var fi = gb.GetComponent<GateBridgePlacement>();
        if (fi == null) { log.AppendLine("ОШИБКА: на GateBridge нет GateBridgePlacement. Добавь компонент и настрой точку входа."); return log.ToString(); }
        string err;
        GBPlan p = MakePlan(fi, gb, out err);
        if (p == null) { log.AppendLine("ОШИБКА: " + err); return log.ToString(); }
        if (gb.transform.lossyScale != Vector3.one) log.AppendLine("! У GateBridge или его родителей масштаб не 1: геометрия строится в мировых координатах, поправь масштаб.");

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build GateBridge");
        int undoGroup = Undo.GetCurrentGroup();

        Material mat = fi.rockMaterial;
        if (mat == null)
        {
            var sh = Shader.Find("Project/IslandRock");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Simple Lit");
            mat = new Material(sh) { name = "IslandRock" };
            Undo.RecordObject(fi, "IslandRock material");
            fi.rockMaterial = mat;
            EditorUtility.SetDirty(fi);
        }
        if (mat.HasProperty("_GlowIntensity") && mat.GetFloat("_GlowIntensity") < 0.01f) mat.SetFloat("_GlowIntensity", 2.5f);

        // старая точка записки (чтобы понять, чья она)
        Transform oldNote = gb.noteSpot;

        // 1. свой корень пересоздаётся целиком
        GateBridgeOwned owned;
        int removed = 0;
        while ((owned = FindOwned(gb.transform)) != null) { Undo.DestroyObjectImmediate(owned.gameObject); removed++; }
        var rootGo = new GameObject(OwnedRootName);
        Undo.RegisterCreatedObjectUndo(rootGo, "GB_Final");
        rootGo.transform.SetParent(gb.transform, false);
        rootGo.transform.position = Vector3.zero;
        rootGo.transform.rotation = Quaternion.identity;
        rootGo.AddComponent<GateBridgeOwned>();
        Transform root = rootGo.transform;
        log.AppendLine(removed > 0 ? "Корень " + OwnedRootName + " пересоздан." : "Корень " + OwnedRootName + " создан.");

        // 2. старые объекты прежней сборки выключаем, не удаляем
        int off = 0;
        foreach (Transform ch in gb.transform)
        {
            if (ch == root || ch.GetComponent<GateBridgeOwned>() != null) continue;
            if (System.Array.IndexOf(LegacyNames, ch.name) < 0 || !ch.gameObject.activeSelf) continue;
            Undo.RecordObject(ch.gameObject, "Disable legacy " + ch.name);
            ch.gameObject.SetActive(false);
            off++;
        }
        log.AppendLine(off > 0 ? "Выключены старые объекты прежней сборки (остров-заглушка с табличкой, старые стадии, стены): " + off + ". Не удалены." : "Старых объектов прежней сборки не найдено или они уже выключены.");

        float sTA = 0.4f, sTB = p.Lb - 0.4f;

        // 3. три вида
        var s1 = NewChild(root, "Stage1");
        AddRenderer(s1, BuildBroken(p, new System.Random(gb.seed + 17)).ToMesh("GateBridgeStage1"), mat, true);
        var s2 = NewChild(root, "Stage2");
        AddRenderer(s2, BuildBuildingBase(p, new System.Random(gb.seed + 34), sTA, sTB).ToMesh("GateBridgeStage2"), mat, true);
        var step2 = NewChild(s2.transform, "DeckStep2");
        AddRenderer(step2, BuildBuildingStep2(p, new System.Random(gb.seed + 35), sTA, sTB).ToMesh("GateBridgeStage2Deck"), mat, true);
        var s3 = NewChild(root, "Stage3");
        AddRenderer(s3, BuildReady(p, new System.Random(gb.seed + 51), sTA, sTB).ToMesh("GateBridgeStage3"), mat, true);

        // 4. проход: непрерывные коллайдеры, включаются только при законченной стройке (GateBridge.Show)
        var walk = NewChild(root, "Walkway");
        walk.AddComponent<MeshCollider>().sharedMesh = BuildWalkMesh(p);

        // 5. заграждения по краю главного острова рядом с проходом (проход шире моста): низкие, камера их не задевает
        var guards = NewChild(root, "Guards");
        float cw = p.hw + 0.45f, capEnd = fi.gateWidth * 0.5f + 3f;
        if (capEnd > cw + 0.5f)
        {
            float capLen = capEnd - cw, capMid = (capEnd + cw) * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 c2 = p.E - p.N * fi.edgeInset + p.Tn * (side * capMid);
                Wall(guards.transform, "GateCap" + (side < 0 ? "L" : "R"), new Vector3(c2.x, p.y0 + 0.575f, c2.y), p.T3, capLen, 1.15f, fi.wallThickness);
            }
        }

        // 6. точки падения досок на виде 1 и доска для падения
        var fall = new System.Collections.Generic.List<Transform>();
        float[] fs = { p.sE + 2.6f, p.sF - 1.6f };
        for (int i = 0; i < fs.Length; i++)
        {
            Vector3 f, r, u;
            p.Frame(fs[i], out f, out r, out u);
            var fp = NewChild(root, "FallPoint" + i);
            fp.transform.position = p.P(fs[i]);
            fp.transform.rotation = Quaternion.LookRotation(f, Vector3.up);
            fall.Add(fp.transform);
        }
        var plank = new GBMesh();
        plank.Box(Vector3.zero, Vector3.right, Vector3.up, Vector3.forward, gb.deckWidth * 0.46f, 0.06f, 0.26f, Color.Lerp(WoodL, WoodD, 0.4f));

        // 7. дальний остров
        Transform noteSpot, arrival;
        BuildFarIsland(fi, gb, p, root, mat, log, out noteSpot, out arrival);

        // 8. ссылки
        Undo.RecordObject(gb, "GateBridge links");
        gb.stages = new[] { s1, s2, s3 };
        gb.deckStep2 = step2;
        gb.walkway = walk;
        gb.fallPoints = fall.ToArray();
        gb.plankMesh = plank.ToMesh("GateBridgePlank");
        gb.plankMaterial = mat;
        gb.noteSpot = noteSpot;
        gb.arrivalSpot = arrival;
        if (gb.source == null) gb.source = Object.FindFirstObjectByType<RiverBridge>(FindObjectsInactive.Include);
        gb.Preview(1, 0, false);   // в сцене по умолчанию вид 1: сломан. В игре вид ставит RiverBridge
        EditorUtility.SetDirty(gb);

        var sf = Object.FindFirstObjectByType<StoryFlow>(FindObjectsInactive.Include);
        if (sf == null) log.AppendLine("! StoryFlow в сцене нет: точку записки не привязали. Запусти Tools > BuilderSim > Сюжет > Настроить цепочку StoryFlow и повтори (в игре StoryFlow сам возьмёт GateBridge.noteSpot).");
        else if (sf.noteSpot == null || sf.noteSpot == oldNote || sf.noteSpot.IsChildOf(gb.transform))
        {
            Undo.RecordObject(sf, "StoryFlow noteSpot");
            sf.noteSpot = noteSpot;
            EditorUtility.SetDirty(sf);
            log.AppendLine("StoryFlow.noteSpot = площадка на дальнем острове (радиус подхода StoryFlow.noteRadius = " + sf.noteRadius.ToString("0.#") + " м).");
        }
        else log.AppendLine("! StoryFlow.noteSpot занят чужим объектом «" + sf.noteSpot.name + "»: не менял. Если записка должна быть на новой площадке, перетащи туда GateBridge/" + OwnedRootName + "/FarIsland/NotePedestal/NoteSpot.");

        var rb = gb.source;
        if (rb != null && rb.stages != null)
            foreach (var g in rb.stages)
                if (g != null && g.transform.IsChildOf(gb.transform) && g.transform.parent == gb.transform && System.Array.IndexOf(LegacyNames, g.name) >= 0)
                    log.AppendLine("! RiverBridge.stages ссылается на старый объект «" + g.name + "» под GateBridge: RiverBridge будет его включать поверх нового вида. Очисти массив RiverBridge.stages.");

        log.AppendLine("Мост: " + gb.bridgeLength.ToString("0.#") + " м, ширина " + gb.deckWidth.ToString("0.#") + " м; остров R = " + p.R.ToString("0.#") + " м. Показан вид 1. Дальше: Tools > BuilderSim > Локация > Проверить мост и финальный остров.");
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(gb.gameObject.scene);
        return log.ToString();
    }

    // Убрать сгенерированное и вернуть выключенные старые объекты
    public static void RemoveGenerated(GateBridge gb)
    {
        GateBridgeOwned owned;
        while ((owned = FindOwned(gb.transform)) != null) Undo.DestroyObjectImmediate(owned.gameObject);
        foreach (Transform ch in gb.transform)
            if (System.Array.IndexOf(LegacyNames, ch.name) >= 0 && !ch.gameObject.activeSelf)
            {
                Undo.RecordObject(ch.gameObject, "Enable legacy");
                ch.gameObject.SetActive(true);
            }
        Undo.RecordObject(gb, "GateBridge clear");
        gb.stages = new GameObject[3];
        gb.deckStep2 = null; gb.walkway = null; gb.noteSpot = null; gb.arrivalSpot = null;
        gb.fallPoints = new Transform[0];
        EditorSceneManager.MarkSceneDirty(gb.gameObject.scene);
    }

    public static GateBridge FindOrCreate(out string error)
    {
        error = null;
        GateBridge gb = Object.FindFirstObjectByType<GateBridge>(FindObjectsInactive.Include);
        if (gb != null)
        {
            if (gb.GetComponent<GateBridgePlacement>() == null)
                Undo.AddComponent<GateBridgePlacement>(gb.gameObject);
            return gb;
        }
        // New scene: create a standalone bridge. Position and rotate this object
        // manually at the actual shore before clicking Build.
        var go = new GameObject("GateBridge");
        Undo.RegisterCreatedObjectUndo(go, "Create GateBridge");
        gb = Undo.AddComponent<GateBridge>(go);
        Undo.AddComponent<GateBridgePlacement>(go);
        if (gb == null) error = "Компонент GateBridge не добавился: проверь, что GateBridge.cs лежит в Assets/_Project/Scripts/Location/ (не в Editor).";
        return gb;
    }

    [MenuItem("Tools/BuilderSim/Локация/Собрать подвесной мост и финальный остров", false, 103)]
    static void MenuBuild()
    {
        string err;
        var gb = FindOrCreate(out err);
        if (gb == null) { EditorUtility.DisplayDialog("GateBridge", err, "OK"); return; }
        string report = Build(gb);
        Debug.Log(report, gb);
        Selection.activeGameObject = gb.gameObject;
        EditorUtility.DisplayDialog("GateBridge", report + "\nСохрани сцену (Ctrl+S).", "OK");
    }
}
#endif
