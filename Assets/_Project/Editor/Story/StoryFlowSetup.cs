#if UNITY_EDITOR
// Assets/_Project/Editor/Story/StoryFlowSetup.cs
// Меню: Tools > BuilderSim > Настроить цепочку StoryFlow
// Порядок: 1) Настроить старт игры, 2) Привести стройки к единому виду, 3) это меню.
// Что делает (всё откатывается Ctrl+Z, цены берутся из GameBalance):
//   * добавляет в сцену StoryFlow;
//   * для хижины, рынка (TradingStall) и палатки находит зону сдачи (BuildingController) по названию.
//     Нет зоны: создаёт её у объекта стадий (HutStages / ShopStages / TentStages) вместе с меткой;
//   * создаёт/обновляет Build Site Data (хижина 2 этапа, рынок 2 этапа, палатка 1 этап) в Assets/_Game/BuildSites;
//   * закрывает эти стройки (locked + storyManaged): открывает их только сюжетная глава;
//   * вешает PlankWorkshop (пилу) на хижину;
//   * проверяет, что в сцене есть Оскар.
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StoryFlowSetup
{
    const string DataFolder = "Assets/_Game/BuildSites";

    [MenuItem("Tools/BuilderSim/Настроить цепочку StoryFlow")]
    static void Run()
    {
        var sb = new StringBuilder("=== StoryFlow ===\n");
        int changes = 0;

        // 1. StoryFlow
        var sf = Object.FindFirstObjectByType<StoryFlow>();
        if (sf == null)
        {
            var go = new GameObject("StoryFlow");
            Undo.RegisterCreatedObjectUndo(go, "StoryFlow");
            sf = go.AddComponent<StoryFlow>();
            sb.AppendLine("+ StoryFlow (главы 2-10: припасы, хижина, доски, рынок, продажа, палатка, мост, записка)");
            changes++;
        }
        else sb.AppendLine("OK StoryFlow уже есть");

        // 2. стройки
        var hut = EnsureSite(StoryFlow.Site.Hut, "Хижина", "задание Оскара", HutSteps(), sb, ref changes);
        var market = EnsureSite(StoryFlow.Site.Market, "Рынок", "задание Миры", MarketSteps(), sb, ref changes);
        var tent = EnsureSite(StoryFlow.Site.Tent, "Палатка", "задание Оскара", TentSteps(), sb, ref changes);
        var bridge = EnsureSite(StoryFlow.Site.Bridge, "Мост", "задание Оскара, " + GameBalance.BridgeLevel + "-й уровень", BridgeSteps(), sb, ref changes);

        Undo.RecordObject(sf, "StoryFlow links");
        if (hut != null) sf.hut = hut;
        if (market != null) sf.market = market;
        if (tent != null) sf.tent = tent;
        if (bridge != null) sf.bridge = bridge;
        var rb = Object.FindFirstObjectByType<RiverBridge>(FindObjectsInactive.Include);
        if (rb != null) sf.riverBridge = rb;
        else sb.AppendLine("! Нет RiverBridge: мост строится только LocationBuilder (Река и мост). Без него финал недостижим.");

        // 3. пила на хижине
        if (hut != null)
        {
            var ws = Object.FindFirstObjectByType<PlankWorkshop>(FindObjectsInactive.Include);
            if (ws == null)
            {
                var stages = hut.GetComponentInParent<HutStages>();
                var host = stages != null ? stages.gameObject : hut.gameObject;
                ws = Undo.AddComponent<PlankWorkshop>(host);
                sb.AppendLine("+ PlankWorkshop (пила) на «" + host.name + "»");
                changes++;
            }
            sf.workshop = ws;
        }
        else sb.AppendLine("! Без хижины нет пилы: доски делать негде, главы 2-3 и рынок будут пропущены.");

        // 4. Оскар
        bool oskar = false;
        foreach (var sp in Object.FindObjectsByType<Speaker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (sp != null && sp.displayName == sf.oskarName) oskar = true;
        sb.AppendLine(oskar
            ? "OK Оскар в сцене (StoryFlow сам включит его в главе и переведёт на E)"
            : "! Нет Оскара. Tools > BuilderSim > NPC Builder, пресет Oskar, поставь у хижины. Без него главы идут без разговора.");

        EditorUtility.SetDirty(sf);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        sb.AppendLine(changes == 0 ? "Менять нечего (данные и ссылки обновлены)." : "Изменений: " + changes + ". Сохраните сцену (Ctrl+S).");
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("StoryFlow", sb.ToString(), "OK");
    }

    // ---------------------------------------------------------------- данные этапов из GameBalance
    static BuildSiteData.Step[] HutSteps()
    {
        var a = new BuildSiteData.Step[GameBalance.HutWood.Length];
        string[] names = { "Стены и крыша", "Пила и верстак" };
        for (int i = 0; i < a.Length; i++)
            a[i] = new BuildSiteData.Step { name = names[i], woodCost = GameBalance.HutWood[i], buildTime = GameBalance.HutTime[i], rewardXp = GameBalance.HutXp[i] };
        return a;
    }

    static BuildSiteData.Step[] MarketSteps()
    {
        var a = new BuildSiteData.Step[GameBalance.MarketWood.Length];
        string[] names = { "Помост и стойки", "Прилавок и крыша" };
        for (int i = 0; i < a.Length; i++)
            a[i] = new BuildSiteData.Step
            {
                name = names[i], woodCost = GameBalance.MarketWood[i], plankCost = GameBalance.MarketPlankCost[i],
                buildTime = GameBalance.MarketTime[i], rewardXp = GameBalance.MarketXp[i]
            };
        return a;
    }

    static BuildSiteData.Step[] TentSteps()
    {
        return new[]
        {
            new BuildSiteData.Step
            {
                name = "Палатка для рабочих", woodCost = GameBalance.TentWood, plankCost = GameBalance.TentPlanks,
                coinCost = GameBalance.TentCoins, buildTime = GameBalance.TentBuildTime, rewardXp = GameBalance.TentXp
            }
        };
    }

    static BuildSiteData.Step[] BridgeSteps()
    {
        var a = new BuildSiteData.Step[GameBalance.BridgeWood.Length];
        for (int i = 0; i < a.Length; i++)
            a[i] = new BuildSiteData.Step
            {
                name = GameBalance.BridgeNames[i], woodCost = GameBalance.BridgeWood[i], plankCost = GameBalance.BridgePlanks[i],
                coinCost = GameBalance.BridgeCoins[i], buildTime = GameBalance.BridgeTime[i], rewardXp = GameBalance.BridgeXp[i]
            };
        return a;
    }

    // ---------------------------------------------------------------- одна стройка
    static BuildingController EnsureSite(StoryFlow.Site site, string title, string lockedAfter, BuildSiteData.Step[] steps, StringBuilder sb, ref int changes)
    {
        var all = Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var bc = StoryFlow.FindSite(site, all);

        if (bc == null)
        {
            bc = CreateZone(site, title, sb);
            if (bc == null)
            {
                sb.AppendLine("! «" + title + "»: нет ни зоны сдачи, ни объекта стадий (" + StageComponentName(site) + "). Собери здание билдером и запусти меню ещё раз.");
                return null;
            }
            changes++;
        }
        else sb.AppendLine("— «" + title + "»: зона «" + bc.name + "»");

        // данные (цены из GameBalance, источник правды один)
        var data = bc.data;
        if (data == null)
        {
            Directory.CreateDirectory(DataFolder);
            string path = DataFolder + "/BuildSite_" + title + ".asset";
            data = AssetDatabase.LoadAssetAtPath<BuildSiteData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<BuildSiteData>();
                AssetDatabase.CreateAsset(data, path);
            }
            Undo.RecordObject(bc, "Data " + title);
            bc.data = data;
        }
        Undo.RecordObject(data, "Steps " + title);
        data.buildingName = title;
        data.startStage = 1;
        data.steps = steps;
        EditorUtility.SetDirty(data);
        sb.AppendLine("    ~ этапов: " + steps.Length + " (цены из GameBalance)");

        Undo.RecordObject(bc, "Story lock " + title);
        bc.storyManaged = true;
        bc.locked = true;
        bc.completesTutorial = false;
        EditorUtility.SetDirty(bc);

        var view = bc.GetComponent<BuildSiteTagView>();
        if (view == null) { view = Undo.AddComponent<BuildSiteTagView>(bc.gameObject); view.buildingName = title; }
        Undo.RecordObject(view, "lockedAfter");
        view.lockedAfter = lockedAfter;
        if (bc.GetComponent<BuildSiteHighlighter>() == null) Undo.AddComponent<BuildSiteHighlighter>(bc.gameObject);

        changes++;
        return bc;
    }

    static string StageComponentName(StoryFlow.Site s)
    {
        return s == StoryFlow.Site.Hut ? "HutStages" : s == StoryFlow.Site.Market ? "ShopStages" : s == StoryFlow.Site.Bridge ? "RiverBridge" : "TentStages";
    }

    // зона сдачи у объекта стадий: как «Создать зону сдачи», но сразу в нужном месте
    static BuildingController CreateZone(StoryFlow.Site site, string title, StringBuilder sb)
    {
        Transform root = null, point = null;
        Vector3? bridgeAt = null;
        Vector3 zoneSize = new Vector3(4f, 3f, 4f);

        if (site == StoryFlow.Site.Hut)
        {
            foreach (var h in Object.FindObjectsByType<HutStages>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (StoryFlow.MatchesSite(site, StoryFlow.PathOf(h.transform))) { root = h.transform; point = h.BuildPoint; break; }
        }
        else if (site == StoryFlow.Site.Market)
        {
            var s = Object.FindFirstObjectByType<ShopStages>(FindObjectsInactive.Include);
            if (s != null) root = s.transform;
        }
        else if (site == StoryFlow.Site.Bridge)
        {
            // зона у берега, где стоит игрок (а не над провалом, там стенки): ближний к игроку край моста, сдвинутый на 2 м внутрь
            var rb = Object.FindFirstObjectByType<RiverBridge>(FindObjectsInactive.Include);
            if (rb != null)
            {
                root = rb.transform;
                var ga = GameObject.Find("BridgePoint_A");
                var gb = GameObject.Find("BridgePoint_B");
                if (ga != null && gb != null)
                {
                    var pm = Object.FindFirstObjectByType<PlayerMovement>();
                    Vector3 pa = ga.transform.position, pb = gb.transform.position;
                    bool aNear = pm == null || (pm.transform.position - pa).sqrMagnitude <= (pm.transform.position - pb).sqrMagnitude;
                    Vector3 near = aNear ? pa : pb, far = aNear ? pb : pa;
                    bridgeAt = near + (far - near).normalized * 2f;
                }
                zoneSize = new Vector3(8f, 3f, 8f);
            }
        }
        else
        {
            var t = Object.FindFirstObjectByType<TentStages>(FindObjectsInactive.Include);
            if (t != null) { root = t.transform; point = t.buildPoint; }
        }
        if (root == null) return null;

        var zg = new GameObject("BuildZone");
        Undo.RegisterCreatedObjectUndo(zg, "Зона " + title);
        zg.transform.SetParent(root, false);
        zg.transform.position = bridgeAt.HasValue ? bridgeAt.Value : (point != null ? point.position : root.position);

        var ls = zg.transform.lossyScale;
        var col = zg.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(zoneSize.x / Mathf.Max(0.01f, ls.x), zoneSize.y / Mathf.Max(0.01f, ls.y), zoneSize.z / Mathf.Max(0.01f, ls.z));
        col.center = new Vector3(0f, col.size.y * 0.5f, 0f);

        var bc = zg.AddComponent<BuildingController>();
        bc.useRing = true;
        bc.ringOffset = new Vector3(0f, 3f, 0f);

        sb.AppendLine("+ «" + title + "»: создана зона сдачи у «" + root.name + "». Подвинь её в Scene, если стоит не там");
        return bc;
    }
}
#endif
