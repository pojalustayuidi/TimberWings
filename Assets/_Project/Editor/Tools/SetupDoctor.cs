#if UNITY_EDITOR
// Assets/_Project/Editor/Tools/SetupDoctor.cs
// Меню: Tools > BuilderSim > Проверить подключение (Setup Doctor)
// Сканирует открытую сцену и пишет в Console отчёт: что подключено (OK), что нужно поправить (ERR/WARN).
// Почти ничего не меняет: единственная правка, зона костра получает completesTutorial = true (у остальных зон он выключается).
// Строки FIX в отчёте говорят, что именно поправлено; после них сохрани сцену (Ctrl+S).
// Отчёт целиком копируется в буфер обмена: вставьте его в чат, и я увижу, что не подключено.
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SetupDoctor
{
    [MenuItem("Tools/BuilderSim/Отладка и тесты/Проверить подключение (Setup Doctor)", false, 1000)]
    static void Run()
    {
        var sb = new StringBuilder();
        int errors = 0, warns = 0;
        sb.AppendLine("=== Setup Doctor: сцена «" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "» ===");

        // ---------- настройки проекта
        try
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets != null && assets.Length > 0)
            {
                var prop = new SerializedObject(assets[0]).FindProperty("activeInputHandler");
                if (prop != null)
                {
                    if (prop.intValue == 1) { errors++; sb.AppendLine("ERR  Active Input Handling = Input System (new). Весь код читает старый Input: поставьте Both (Project Settings > Player > Other Settings)."); }
                    else sb.AppendLine("OK   Active Input Handling: " + (prop.intValue == 2 ? "Both" : "Input Manager (old)"));
                }
            }
        }
        catch { sb.AppendLine("WARN не удалось прочитать Active Input Handling, проверьте руками (должно быть Both)."); warns++; }

        // ---------- игрок
        var pm = Object.FindFirstObjectByType<PlayerMovement>();
        if (pm == null) { errors++; sb.AppendLine("ERR  Нет PlayerMovement: игрока на сцене не найдено."); }
        else
        {
            var go = pm.gameObject;
            sb.AppendLine("OK   Игрок: " + go.name);
            Need<Rigidbody>(go, "Rigidbody (движение)", true, sb, ref errors, ref warns);
            Need<PlayerInventory>(go, "PlayerInventory", true, sb, ref errors, ref warns);
            Need<PlayerStats>(go, "PlayerStats", true, sb, ref errors, ref warns);
            Need<BackpackTag>(go, "BackpackTag", true, sb, ref errors, ref warns);
            Need<PlayerInteraction>(go, "PlayerInteraction (рубка)", true, sb, ref errors, ref warns);
            Need<InteractionHub>(go, "InteractionHub (без него не работает E у NPC и построек)", true, sb, ref errors, ref warns);
            Need<ChopProgress>(go, "ChopProgress (кольцо рубки)", false, sb, ref errors, ref warns);
            var sp = go.GetComponent<Speaker>();
            if (sp == null) { warns++; sb.AppendLine("WARN у игрока нет Speaker: реплики Лео пойдут без пузыря. Меню: Tools > BuilderSim > Добавить реплики Лео игроку."); }
            else if (sp.displayName != "Лео") { warns++; sb.AppendLine("WARN у игрока Speaker.displayName = «" + sp.displayName + "», а в диалогах Лео зовут «Лео» (IntroFlow.playerName)."); }
            else sb.AppendLine("OK   Speaker «Лео» на игроке");
            if (pm.model == null) { warns++; sb.AppendLine("WARN PlayerMovement.model не назначен: герой не будет поворачиваться."); }
            if (!pm.useBalanceSpeed) { warns++; sb.AppendLine("WARN PlayerMovement.useBalanceSpeed выключен: скорость берётся из Inspector (" + pm.speed + "), а не из GameBalance (4)."); }
        }

        // ---------- деревья
        try
        {
            var trees = GameObject.FindGameObjectsWithTag("Resource");
            int withItem = 0, noCol = 0;
            foreach (var t in trees)
            {
                if (t.GetComponentInParent<ResourceItem>() != null) withItem++;
                if (t.GetComponent<Collider>() == null) noCol++;
            }
            if (trees.Length == 0) { errors++; sb.AppendLine("ERR  Нет ни одного объекта с тегом Resource: рубить нечего."); }
            else
            {
                sb.AppendLine("OK   Деревьев с тегом Resource: " + trees.Length + " (с ResourceItem: " + withItem + ")");
                if (withItem < trees.Length) { errors++; sb.AppendLine("ERR  У " + (trees.Length - withItem) + " деревьев с тегом Resource нет ResourceItem."); }
                if (noCol > 0) { errors++; sb.AppendLine("ERR  У " + noCol + " деревьев с тегом Resource нет Collider: игрок их не найдёт."); }
                if (trees.Length != GameBalance.TreesOnIsland) { warns++; sb.AppendLine("WARN по GDD на острове " + GameBalance.TreesOnIsland + " деревьев, сейчас " + trees.Length + "."); }
            }
        }
        catch { errors++; sb.AppendLine("ERR  Тег «Resource» не создан в Tags and Layers."); }

        // ---------- HUD
        if (Object.FindFirstObjectByType<UIManager>() == null) { errors++; sb.AppendLine("ERR  Нет UIManager: HUD не обновляется (Tools > BuilderSim > Build HUD)."); }
        else sb.AppendLine("OK   UIManager есть");
        bool wood = false, xp = false, coin = false;
        foreach (var a in Object.FindObjectsByType<HudGainAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (a.kind == HudGain.Kind.Wood) wood = true;
            if (a.kind == HudGain.Kind.Xp) xp = true;
            if (a.kind == HudGain.Kind.Coin) coin = true;
        }
        if (!wood) { warns++; sb.AppendLine("WARN нет HudGainAnchor(Kind=Wood): брёвна не будут лететь в HUD, начисление мгновенное."); }
        if (!xp) { warns++; sb.AppendLine("WARN нет HudGainAnchor(Kind=Xp)."); }
        if (!coin) { warns++; sb.AppendLine("WARN нет HudGainAnchor(Kind=Coin)."); }
        if (wood && xp && coin) sb.AppendLine("OK   HudGainAnchor: Wood, Xp, Coin на месте");

        // ---------- сюжет
        var intro = Object.FindFirstObjectByType<IntroFlow>();
        if (intro == null) { errors++; sb.AppendLine("ERR  Нет IntroFlow: цикл обучения (ходьба, Мира, костёр) не запустится. Tools > BuilderSim > Настроить старт игры."); }
        else sb.AppendLine("OK   IntroFlow есть (scriptedDialogue = " + intro.scriptedDialogue + ")");
        if (Object.FindFirstObjectByType<StoryFlow>(FindObjectsInactive.Include) == null)
        { errors++; sb.AppendLine("ERR  Нет StoryFlow: после вступления не будет заданий (8 брёвен Мире и дальше). Tools > BuilderSim > Настроить цепочку StoryFlow."); }
        else sb.AppendLine("OK   StoryFlow есть (SupplyPoint на Миру он навешивает сам)");

        // объекты, у которых слетел скрипт («Missing»): так бывает, если файл скрипта удалили и создали заново
        int missing = 0;
        foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject);
            if (n > 0) { missing += n; sb.AppendLine("WARN «" + tr.name + "»: слетевших скриптов (Missing): " + n); }
        }
        if (missing == 0) sb.AppendLine("OK   Слетевших скриптов (Missing) на сцене нет");
        else
        {
            warns++;
            sb.AppendLine("WARN слетевших скриптов всего: " + missing + ". Пустые компоненты безвредны, но ругаются в Console. Убрать: Tools > BuilderSim > Убрать слетевшие скрипты (Missing).");
            sb.AppendLine("     Нужные компоненты (игрок, Мира, IntroFlow, StoryFlow) проверяются выше: если у них OK, всё важное на месте.");
        }

        if (Object.FindFirstObjectByType<TutorialManager>() != null) { warns++; sb.AppendLine("WARN на сцене старый TutorialManager: он выключается сам, но его можно удалить."); }

        Speaker mira = null;
        foreach (var s in Object.FindObjectsByType<Speaker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (s.displayName == "Мира") mira = s;
        if (mira == null) { errors++; sb.AppendLine("ERR  Нет Speaker с именем «Мира»: нечего говорить в первом задании (Tools > BuilderSim > NPC Builder)."); }
        else
        {
            sb.AppendLine("OK   Мира найдена: " + mira.name);
            if (mira.GetComponent<DialogueSequence>() == null) { errors++; sb.AppendLine("ERR  У Миры нет DialogueSequence: IntroFlow не сможет начать разговор."); }
            else sb.AppendLine("OK   У Миры есть DialogueSequence");
            var miraSeq = mira.GetComponent<DialogueSequence>();
            if (miraSeq != null && intro == null && miraSeq.trigger != DialogueSequence.Trigger.OnInteract)
            { warns++; sb.AppendLine("WARN Мира говорит по триггеру «" + miraSeq.trigger + "», а IntroFlow переведёт её на E только после старта игры."); }
            if (mira.GetComponentInChildren<Collider>() == null) { warns++; sb.AppendLine("WARN у Миры нет коллайдера: игрок пройдёт сквозь неё."); }
            if (mira.GetComponent<TradingStall>() != null) { warns++; sb.AppendLine("WARN у Миры остался TradingStall: торговля теперь у NPC-торговца, два объекта спорят за E. Снимите компонент с Миры."); }
            else sb.AppendLine("OK   У Миры нет TradingStall (торговля у отдельного NPC)");
        }
        // NPC-торговец: отдельный NPC у прилавка, включается после главы «Рынок»
        var storyRef = Object.FindFirstObjectByType<StoryFlow>(FindObjectsInactive.Include);
        string merchantName = storyRef != null ? storyRef.merchantName : "Торговец";
        GameObject merchantGo = storyRef != null ? storyRef.merchant : null;
        if (merchantGo == null)
            foreach (var ms in Object.FindObjectsByType<Speaker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (ms.displayName == merchantName) { merchantGo = ms.gameObject; break; }
        if (merchantGo == null) { warns++; sb.AppendLine("WARN нет NPC-торговца «" + merchantName + "» (Speaker + DialogueSequence + NpcMarker + TradingStall): продавать некому, глава «Первая продажа» пропускается."); }
        else
        {
            sb.AppendLine("OK   Торговец найден: " + merchantGo.name);
            if (merchantGo.GetComponent<Speaker>() == null) { errors++; sb.AppendLine("ERR  У торговца нет Speaker."); }
            if (merchantGo.GetComponent<DialogueSequence>() == null) { warns++; sb.AppendLine("WARN У торговца нет DialogueSequence: реплику-знакомство сказать будет некому."); }
            if (merchantGo.GetComponent<TradingStall>() == null) { errors++; sb.AppendLine("ERR  У торговца нет TradingStall."); }
            if (VoiceId.SpeakerKey(merchantName) == "npc") { warns++; sb.AppendLine("WARN Имя «" + merchantName + "» не добавлено в VoiceId.SpeakerKey: озвучка уйдёт в «npc_»."); }

            // Priority забирает E у рубки: дерево ближе 6 м (радиус торговли 3 + запас) оставит рубку без E
            float nearest = float.MaxValue;
            foreach (var tree in Object.FindObjectsByType<ResourceItem>(FindObjectsSortMode.None))
            {
                Vector3 dv = tree.transform.position - merchantGo.transform.position;
                dv.y = 0f;
                nearest = Mathf.Min(nearest, dv.magnitude);
            }
            if (nearest < 6f) { warns++; sb.AppendLine("WARN Ближайшее дерево в " + nearest.ToString("0.0") + " м от торговца (нужно от 6 м): E у рубки будет конфликтовать."); }
            else sb.AppendLine("OK   Ближайшее дерево не ближе 6 м от торговца");
        }

        bool oskar = false;
        foreach (var s in Object.FindObjectsByType<Speaker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (s.displayName == "Оскар") oskar = true;
        sb.AppendLine(oskar ? "OK   Оскар найден" : "WARN Оскара на сцене нет (нужен для задания про хижину)");
        if (!oskar) warns++;

        if (GameObject.Find("CAMPFIRE_Point") == null) { errors++; sb.AppendLine("ERR  Нет объекта CAMPFIRE_Point: стрелке квеста некуда вести к костру."); }
        else sb.AppendLine("OK   CAMPFIRE_Point есть");

        // ---------- стройки
        var builds = Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int campfires = 0;
        var cpObj = GameObject.Find("CAMPFIRE_Point");
        var camp = IntroFlow.FindCampfireBuild(cpObj != null ? cpObj.transform : null);
        bool fixedAny = false;
        if (camp == null) { errors++; sb.AppendLine("ERR  Не найдена зона костра: нет BuildingController с «campfire» в пути или рядом с CAMPFIRE_Point."); }
        int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
        if (sceneCount > 1)
        {
            warns++;
            var names = new StringBuilder();
            for (int i = 0; i < sceneCount; i++) names.Append("«" + UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name + "» ");
            sb.AppendLine("WARN открыто сцен: " + sceneCount + " (" + names + "): объекты ВСЕХ сцен попадают в отчёт и в игру, возможны дубли. Закрой лишние (ПКМ по сцене в Hierarchy > Remove Scene).");
        }
        sb.AppendLine("--- Зоны сдачи (BuildingController): " + builds.Length);
        foreach (var b in builds)
        {
            // обучение завершает только костёр: у него флаг включаем, у остальных выключаем
            bool isCamp = b == camp;
            if (b.completesTutorial != isCamp)
            {
                Undo.RecordObject(b, "completesTutorial");
                b.completesTutorial = isCamp;
                EditorUtility.SetDirty(b);
                fixedAny = true;
                sb.AppendLine("FIX  «" + b.name + "»: completesTutorial = " + isCamp + (isCamp ? " (зона костра)" : " (не костёр)"));
            }

            string line = "     «" + b.name + "» (родитель: " + (b.transform.parent != null ? b.transform.parent.name : "-") + ", сцена: «" + b.gameObject.scene.name + "», путь: " + HierPath(b.transform) + ")"
                        + (isCamp ? " [КОСТЁР]" : "")
                        + " брёвен всего: " + b.TotalWoodAllSteps + ", locked: " + b.locked + ", completesTutorial: " + b.completesTutorial
                        + ", Data: " + (b.data != null ? b.data.name : "нет (запасные поля)");
            sb.AppendLine(line);
            if (b.completesTutorial)
            {
                campfires++;
                int need = GameBalance.CampfireTrees * GameBalance.WoodPerTree;
                if (b.TotalWoodAllSteps != need) { warns++; sb.AppendLine("WARN у костра цена " + b.TotalWoodAllSteps + " брёвен, по GDD " + need + " (4 дерева)."); }
            }
            if (b.GetComponent<BoxCollider>() == null) { errors++; sb.AppendLine("ERR  у зоны «" + b.name + "» нет BoxCollider (триггер)."); }
            if (b.GetComponent<BuildSiteTagView>() == null && b.GetComponentInParent<BuildSiteTagView>() == null) { warns++; sb.AppendLine("WARN у зоны «" + b.name + "» нет метки с ценой."); }
        }
        if (fixedAny) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        if (campfires == 0 && camp != null) { errors++; sb.AppendLine("ERR  completesTutorial у костра не включился, проверьте вручную."); }
        if (campfires > 1) { warns++; sb.AppendLine("WARN completesTutorial включено у " + campfires + " построек, должно быть только у костра."); }

        // несколько зон на одно здание: путаница, игра возьмёт лучшую, остальные лучше удалить
        CheckDuplicates("Хижина", builds, path => StoryFlow.MatchesSite(StoryFlow.Site.Hut, path), sb, ref warns);
        CheckDuplicates("Рынок", builds, path => StoryFlow.MatchesSite(StoryFlow.Site.Market, path), sb, ref warns);
        CheckDuplicates("Палатка", builds, path => StoryFlow.MatchesSite(StoryFlow.Site.Tent, path), sb, ref warns);
        CheckDuplicates("Костёр", builds, path => StoryFlow.HasAny(path, new[] { "campfire", "костёр", "костер" }), sb, ref warns);

        // здания с этапами без зоны сдачи
        foreach (var hs in Object.FindObjectsByType<HutStages>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool has = false;
            foreach (var b in builds) if (b.stages == hs || b.GetComponentInParent<HutStages>() == hs) has = true;
            if (!has) { warns++; sb.AppendLine("WARN у «" + hs.name + "» (HutStages) нет зоны сдачи: достроить его нельзя. Tools > BuilderSim > Создать зону сдачи, назначьте Stages и Data."); }
        }

        // ---------- мост (глава 9) и записка Гуннара (глава 10)
        sb.AppendLine("--- Мост");
        var sfBridge = Object.FindFirstObjectByType<StoryFlow>(FindObjectsInactive.Include);
        var bridgeBuild = sfBridge != null && sfBridge.bridge != null ? sfBridge.bridge : StoryFlow.FindSite(StoryFlow.Site.Bridge, builds);
        var riverBridge = Object.FindFirstObjectByType<RiverBridge>(FindObjectsInactive.Include);

        if (riverBridge == null) { warns++; sb.AppendLine("WARN (мост убран, hasBridge=false) Нет RiverBridge: моста в сцене нет (LocationBuilder > Река и мост). Финал среза недостижим."); }
        else
        {
            sb.AppendLine("OK   RiverBridge: " + HierPath(riverBridge.transform));
            int nonNull = 0;
            if (riverBridge.stages != null) foreach (var sg in riverBridge.stages) if (sg != null) nonNull++;
            if (nonNull < 3) { errors++; sb.AppendLine("ERR  У RiverBridge заполнено видов " + nonNull + " из 3 (stages): стадии моста не будут меняться."); }
            else sb.AppendLine("OK   У RiverBridge три вида (stages)");
            if (riverBridge.blockers == null) { errors++; sb.AppendLine("ERR  У RiverBridge нет blockers: мост не будет физически закрыт до достройки."); }
            else sb.AppendLine("OK   У RiverBridge есть blockers (держатся до последнего этапа)");
        }

        if (bridgeBuild == null)
        {
            errors++;
            sb.AppendLine("ERR  Нет зоны стройки моста (BuildingController с «bridge»/«мост» в пути). Tools > BuilderSim > Настроить цепочку StoryFlow создаст её. Глава «Мост» обязательна и не пропускается.");
        }
        else
        {
            sb.AppendLine("OK   Зона моста: " + HierPath(bridgeBuild.transform));
            if (bridgeBuild.GetComponent<BoxCollider>() == null) { errors++; sb.AppendLine("ERR  У зоны моста нет BoxCollider (триггер): сдать ресурсы нельзя."); }
            if (bridgeBuild.data == null) { errors++; sb.AppendLine("ERR  У зоны моста нет Build Site Data: цены не из GameBalance. Запусти «Настроить цепочку StoryFlow»."); }
            else
            {
                var bd = bridgeBuild.data;
                int want = GameBalance.BridgeWood.Length;
                if (bd.StepCount != want) { errors++; sb.AppendLine("ERR  В Build Site Data моста этапов " + bd.StepCount + ", по GameBalance " + want + "."); }
                else
                {
                    bool same = true;
                    for (int i = 0; i < want; i++)
                    {
                        var st = bd.steps[i];
                        if (st.woodCost != GameBalance.BridgeWood[i] || st.plankCost != GameBalance.BridgePlanks[i] || st.coinCost != GameBalance.BridgeCoins[i])
                        {
                            same = false;
                            errors++;
                            sb.AppendLine("ERR  Этап " + (i + 1) + " «" + st.name + "»: в данных " + st.woodCost + " брёвен / " + st.plankCost + " досок / " + st.coinCost + " монет, по GameBalance "
                                          + GameBalance.BridgeWood[i] + " / " + GameBalance.BridgePlanks[i] + " / " + GameBalance.BridgeCoins[i] + ".");
                        }
                    }
                    if (same) sb.AppendLine("OK   Цены трёх этапов моста совпадают с GameBalance (брёвен 40, досок 70, монет 800)");
                }
            }
            if (!bridgeBuild.locked) { warns++; sb.AppendLine("WARN Зона моста не закрыта (locked = false): до главы «Мост» её откроет StoryFlow при старте, но в редакторе она выглядит открытой."); }
            if (bridgeBuild.GetComponent<BuildSiteTagView>() == null) { warns++; sb.AppendLine("WARN У зоны моста нет BuildSiteTagView: игрок не увидит этап, цену и причину блокировки."); }
            if (bridgeBuild.completesTutorial) { errors++; sb.AppendLine("ERR  У зоны моста completesTutorial = true: достройка моста завершит вступление."); }

            var bridgeLink = bridgeBuild.GetComponent<BuildStageLink>();
            if (bridgeLink == null || bridgeLink.bridge == null) { warns++; sb.AppendLine("WARN BuildStageLink с мостом не повешен: StoryFlow добавит его при старте игры (если RiverBridge найден)."); }
            else sb.AppendLine("OK   BuildStageLink ведёт RiverBridge");
        }

        var islandRef = Object.FindFirstObjectByType<FloatingIsland>(FindObjectsInactive.Include);
        if (islandRef == null) { warns++; sb.AppendLine("WARN Нет FloatingIsland: стена у прохода к мосту не убирается (проход закрыт?)."); }
        else if (islandRef.GateRoot == null) { warns++; sb.AppendLine("WARN У FloatingIsland нет «GateWalls»: OpenGate() ничего не откроет. Поставь GatePoint и нажми «Собрать остров»."); }
        else sb.AppendLine("OK   FloatingIsland: GateWalls есть, откроется после третьего этапа");

        if (sfBridge != null && !oskar) { warns++; sb.AppendLine("WARN Нет Оскара: глава «Мост» пойдёт без разговора, но задание примется."); }
        if (pm != null && pm.GetComponent<Speaker>() == null) { warns++; sb.AppendLine("WARN У игрока нет Speaker «Лео»: записку Гуннара читать некому, финал засчитается без реплик."); }

        // ---------- рабочие (P2: найм и пассивный доход)
        CheckWorkers(sb, ref errors, ref warns);
        WorkerDatabaseTools.CheckForDoctor(sb, ref errors, ref warns);   // база рабочих (27 персонажей)

        // ---------- здания без логики (ожидаемо, это следующие сессии)
        Info<TentStages>("Мастерская (TentStages): визуал есть, логики «бревно→доска» ещё нет (сессия S5)", sb);
        Info<ShopStages>("Лавка (ShopStages): визуал есть, продажи ещё нет (сессия S4)", sb);
        Info<BoardStages>("Доска заданий (BoardStages): визуал есть, заказов ещё нет (сессия S9)", sb);

        // ---------- итог
        sb.AppendLine("=== Итог: ошибок " + errors + ", предупреждений " + warns + " ===");
        string report = sb.ToString();
        GUIUtility.systemCopyBuffer = report;
        if (errors > 0) Debug.LogError(report); else if (warns > 0) Debug.LogWarning(report); else Debug.Log(report);
        EditorUtility.DisplayDialog("Setup Doctor", "Ошибок: " + errors + ", предупреждений: " + warns + ".\nОтчёт в Console и скопирован в буфер обмена, вставьте его в чат.", "OK");
    }

    // ---------- рабочие (P2): баланс, менеджер, лагерь, UI, конфликты с монетами и опытом
    static void CheckWorkers(StringBuilder sb, ref int errors, ref int warns)
    {
        // баланс: единый источник GameBalance
        bool balanceOk = true;
        if (GameBalance.MaxWorkers < 1) { errors++; balanceOk = false; sb.AppendLine("ERR  GameBalance.MaxWorkers = " + GameBalance.MaxWorkers + ": мест для рабочих нет."); }
        if (GameBalance.WorkerIncome <= 0f) { errors++; balanceOk = false; sb.AppendLine("ERR  GameBalance.WorkerIncome = " + GameBalance.WorkerIncome + ": доход должен быть больше нуля."); }
        if (GameBalance.WorkerBaseCost < 1) { errors++; balanceOk = false; sb.AppendLine("ERR  GameBalance.WorkerBaseCost = " + GameBalance.WorkerBaseCost + ": цена первого рабочего должна быть больше нуля."); }
        if (GameBalance.WorkerCostGrowth < 1f) { errors++; balanceOk = false; sb.AppendLine("ERR  GameBalance.WorkerCostGrowth = " + GameBalance.WorkerCostGrowth + ": цена не должна падать с каждым рабочим."); }
        if (GameBalance.XpPerWorker < 0) { errors++; balanceOk = false; sb.AppendLine("ERR  GameBalance.XpPerWorker = " + GameBalance.XpPerWorker + ": опыт за найм не может быть отрицательным."); }
        if (balanceOk)
        {
            var costs = new StringBuilder();
            int prev = 0, total = 0;
            bool mono = true;
            for (int n = 1; n <= GameBalance.MaxWorkers; n++)
            {
                int c = GameBalance.WorkerCost(n);
                if (c < prev || c < 1) mono = false;
                prev = c;
                total += c;
                if (n > 1) costs.Append(" / ");
                costs.Append(c);
            }
            if (!mono) { errors++; sb.AppendLine("ERR  Цены рабочих не растут: " + costs + ". Проверь GameBalance.WorkerCost."); }
            else sb.AppendLine("OK   Цены рабочих (GameBalance.WorkerCost): " + costs + ", всего " + total + " монет; доход 1 рабочего " + GameBalance.WorkerIncome + " монет/с, полный состав " + (GameBalance.MaxWorkers * GameBalance.WorkerIncome) + " монет/с");

            // значения из ТЗ P2: 6 рабочих, 60 монет, рост 1.4, 0.4 монеты/с, 10 XP
            if (GameBalance.MaxWorkers != 6 || GameBalance.WorkerBaseCost != 60 || !Mathf.Approximately(GameBalance.WorkerCostGrowth, 1.4f)
                || !Mathf.Approximately(GameBalance.WorkerIncome, 0.4f) || GameBalance.XpPerWorker != 10)
            {
                warns++;
                sb.AppendLine("WARN Баланс рабочих не совпадает с ТЗ P2 (6 рабочих, 60 монет, рост 1.4, 0.4 монеты/с, 10 XP): "
                              + GameBalance.MaxWorkers + ", " + GameBalance.WorkerBaseCost + ", " + GameBalance.WorkerCostGrowth + ", " + GameBalance.WorkerIncome + ", " + GameBalance.XpPerWorker + ". Если это осознанное решение, обнови ТЗ.");
            }
            else sb.AppendLine("OK   Баланс рабочих совпадает с ТЗ P2 (6 / 60 / 1.4 / 0.4 / 10 XP)");
        }

        // менеджер
        var managers = Object.FindObjectsByType<WorkerManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (managers.Length == 0) { errors++; sb.AppendLine("ERR  Нет WorkerManager: рабочих нанять нельзя. Tools > BuilderSim > Настроить рабочих (WorkerManager + лагерь)."); }
        else if (managers.Length > 1)
        {
            errors++;
            sb.AppendLine("ERR  WorkerManager на сцене " + managers.Length + ", должен быть один (лишний в игре сам удалится, но уберите его):");
            foreach (var wm in managers) sb.AppendLine("       - " + HierPath(wm.transform));
        }
        else
        {
            sb.AppendLine("OK   WorkerManager: " + HierPath(managers[0].transform));
            if (!managers[0].isActiveAndEnabled) { errors++; sb.AppendLine("ERR  WorkerManager выключен: доход начисляться не будет."); }
            if (managers[0].database == null) { errors++; sb.AppendLine("ERR  У WorkerManager не назначена база рабочих (поле database): сохранённые рабочие не восстановятся. Tools > BuilderSim > Настроить рабочих."); }
            if (managers[0].maxFrameStep > 1f) { warns++; sb.AppendLine("WARN WorkerManager.maxFrameStep = " + managers[0].maxFrameStep + ": после долгого кадра прилетит большая пачка монет."); }
        }

        // лагерь (точка найма по E)
        var camps = Object.FindObjectsByType<WorkerCamp>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (camps.Length == 0) { errors++; sb.AppendLine("ERR  Нет WorkerCamp: окно «Рабочие» нечем открыть. Tools > BuilderSim > Настроить рабочих (WorkerManager + лагерь)."); }
        else
        {
            sb.AppendLine("OK   WorkerCamp: " + HierPath(camps[0].transform) + ", радиус " + camps[0].radius);
            if (camps.Length > 1) { warns++; sb.AppendLine("WARN WorkerCamp на сцене " + camps.Length + ": точек найма несколько, окно у всех одно и то же."); }
            foreach (var cp in camps)
            {
                if (cp.radius < 1f) { warns++; sb.AppendLine("WARN У WorkerCamp «" + cp.name + "» радиус " + cp.radius + ": игроку трудно встать в зону."); }
                if (cp.requireTentBuilt && cp.GetComponentInParent<TentStages>() == null
                    && (Object.FindFirstObjectByType<StoryFlow>() == null || Object.FindFirstObjectByType<StoryFlow>().tent == null))
                { warns++; sb.AppendLine("WARN У WorkerCamp «" + cp.name + "» нечем проверить, что палатка достроена (нет TentStages и StoryFlow.tent): лагерь будет открыт всегда."); }
                if (!cp.requireTentBuilt) { warns++; sb.AppendLine("WARN У WorkerCamp «" + cp.name + "» requireTentBuilt выключен: лагерь открыт до постройки палатки (режим теста)."); }
            }
        }

        // UI: окно строится из кода при первом E, но HUD монет и опыта должен быть на месте
        var ui = Object.FindFirstObjectByType<UIManager>();
        if (ui == null) { warns++; sb.AppendLine("WARN Нет UIManager: монеты и опыт после найма на экране не обновятся."); }
        else
        {
            if (ui.coinText == null) { warns++; sb.AppendLine("WARN У UIManager не назначен coinText: счётчик монет не покажет доход и трату."); }
            else sb.AppendLine("OK   UIManager.coinText на месте: доход и трата отразятся в HUD");
        }
        var wins = Object.FindObjectsByType<WorkersWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (wins.Length > 0) { warns++; sb.AppendLine("WARN WorkersWindow лежит на сцене (" + wins.Length + "): окно создаёт себя само при первом E, сохранённый объект лишний."); }
        else sb.AppendLine("OK   WorkersWindow строится из кода при первом E у лагеря (на сцене его хранить не нужно)");

        // конфликты с существующими системами монет и опыта
        var invs = Object.FindObjectsByType<PlayerInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var stats = Object.FindObjectsByType<PlayerStats>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (invs.Length > 1) { errors++; sb.AppendLine("ERR  PlayerInventory на сцене " + invs.Length + ": две системы монет, менеджер рабочих платит первой найденной."); }
        else if (invs.Length == 1) sb.AppendLine("OK   Монеты: одна система (PlayerInventory.coinsCount), рабочие платят и списывают только через неё");
        if (stats.Length > 1) { errors++; sb.AppendLine("ERR  PlayerStats на сцене " + stats.Length + ": две системы опыта, XP за найм уйдёт первой найденной."); }
        else if (stats.Length == 1) sb.AppendLine("OK   Опыт: одна система (PlayerStats.AddXP), XP за найм выдаётся один раз внутри WorkerManager.Hire");
        if (invs.Length == 1 && stats.Length == 1 && invs[0].gameObject != stats[0].gameObject)
        { warns++; sb.AppendLine("WARN PlayerInventory и PlayerStats на разных объектах: WorkerManager берёт PlayerStats с того же объекта, что и PlayerInventory, XP за найм не выдастся."); }
        sb.AppendLine("INFO Сохранений в проекте нет: найм рабочих сбрасывается при перезапуске игры (как монеты и опыт). Доход идёт по игровому времени и стоит на паузе (timeScale 0)");
    }

    static string HierPath(Transform t)
    {
        string p = t.name;
        for (var q = t.parent; q != null; q = q.parent) p = q.name + "/" + p;
        return p;
    }

    static void CheckDuplicates(string title, BuildingController[] builds, System.Func<string, bool> match, StringBuilder sb, ref int warns)
    {
        var list = new System.Collections.Generic.List<BuildingController>();
        foreach (var b in builds) if (b != null && match(StoryFlow.PathOf(b))) list.Add(b);
        if (list.Count < 2) return;

        warns++;
        sb.AppendLine("WARN «" + title + "»: зон сдачи " + list.Count + ", должна быть одна. Игра возьмёт лучшую (с триггером и ценой), остальные удали:");
        foreach (var b in list)
            sb.AppendLine("       - " + HierPath(b.transform) + " (сцена «" + b.gameObject.scene.name + "», оценка " + StoryFlow.SiteScore(b) + ", брёвен " + b.TotalWoodAllSteps
                          + (b.GetComponent<Collider>() == null ? ", БЕЗ коллайдера" : "") + ")");
    }

    static void Need<T>(GameObject go, string label, bool required, StringBuilder sb, ref int errors, ref int warns) where T : Component
    {
        if (go.GetComponent<T>() != null) { sb.AppendLine("OK   " + label); return; }
        if (required) { errors++; sb.AppendLine("ERR  у игрока нет " + label); }
        else { warns++; sb.AppendLine("WARN у игрока нет " + label); }
    }

    static void Info<T>(string text, StringBuilder sb) where T : Component
    {
        var found = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        sb.AppendLine((found != null ? "INFO " : "INFO нет на сцене: ") + text);
    }
}
#endif
