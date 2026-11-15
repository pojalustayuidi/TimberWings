// TimberWings — Construction QA. Editor-only, uses the project's existing construction APIs.
// Install: Assets/_Project/Editor/Tests/ConstructionQaWindow.cs
// Open: Tools > BuilderSim > Отладка и тесты > Строительство > Открыть панель
// Changes to progress/resources are possible only in isolated Play Mode.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ConstructionQaWindow : EditorWindow
{
    const string Root = "Tools/BuilderSim/Отладка и тесты/Строительство/";
    const string MainScene = "Assets/_Recovery/0 (8).unity";
    const string IsolatedSessionKey = SaveSystem.EditorTestSessionKey;

    readonly List<BuildingController> sites = new List<BuildingController>(16);
    readonly List<string> labels = new List<string>(16);
    int selectedIndex;
    string preferredSaveId = "";
    Vector2 scroll;
    double refreshAt;
    string message = "";
    MessageType messageType = MessageType.Info;

    [MenuItem(Root + "Открыть панель", false, 1060)]
    public static void Open()
    {
        var window = GetWindow<ConstructionQaWindow>("TW · Строительство QA");
        window.minSize = new Vector2(420f, 580f);
        window.Show();
    }

    [InitializeOnLoadMethod]
    static void InstallCleanup()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
            SessionState.SetBool(IsolatedSessionKey, false);
    }

    void OnEnable()
    {
        refreshAt = 0;
    }

    void OnInspectorUpdate()
    {
        if (EditorApplication.isPlaying) Repaint();
    }

    void OnGUI()
    {
        if (EditorApplication.timeSinceStartup >= refreshAt)
        {
            RefreshSites();
            refreshAt = EditorApplication.timeSinceStartup + 0.7;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Space(8);
        EditorGUILayout.LabelField("TimberWings · Тест строительства", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Просматривай стадии без повторного прохождения сюжета или запускай настоящее строительство с анимацией молотка. " +
            "Все изменения прогресса выполняются только в изолированном Play Mode.", MessageType.Info);

        if (!String.IsNullOrEmpty(message))
        {
            EditorGUILayout.HelpBox(message, messageType);
            if (GUILayout.Button("Скрыть сообщение")) message = "";
        }

        if (!EditorApplication.isPlaying)
        {
            ShowEditControls();
            EditorGUILayout.EndScrollView();
            return;
        }
        if (!SaveSystem.EditorTestMode)
        {
            EditorGUILayout.HelpBox(
                "Обычный Play: тестовые изменения заблокированы, чтобы случайно не испортить настоящий сейв. " +
                "Останови Play и запусти его кнопкой из этого окна.", MessageType.Warning);
            if (GUILayout.Button("■ Остановить обычный Play", GUILayout.Height(32))) EditorApplication.isPlaying = false;
            EditorGUILayout.EndScrollView();
            return;
        }

        ShowPlayControls();
        EditorGUILayout.EndScrollView();
    }

    void ShowEditControls()
    {
        GUILayout.Space(8);
        EditorGUILayout.LabelField("Текущая сцена", SceneManager.GetActiveScene().path);
        if (sites.Count == 0)
            EditorGUILayout.HelpBox("В сцене не найдены BuildingController. Открой игровую сцену.", MessageType.Warning);
        DrawSiteSelector();
        if (GUILayout.Button("Выделить стройку в Scene View")) FrameSelected();

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScene) != null &&
                GUILayout.Button("Открыть основную игровую сцену"))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(MainScene);
                    RefreshSites();
                }
            }
            if (GUILayout.Button("▶ Запустить БЕЗОПАСНЫЙ Play", GUILayout.Height(42)))
            {
                // SaveSystem reads this BEFORE the first scene frame (SubsystemRegistration).
                SessionState.SetBool(IsolatedSessionKey, true);
                EditorApplication.isPlaying = true;
            }
        }

        GUILayout.Space(10);
        if (GUILayout.Button("Проверить связи и коллайдеры строек (без изменений)"))
            BuildingIntegrationAudit.Check();
        if (GUILayout.Button("Открыть QA моста и финала")) BridgeQaWindow.Open();
        EditorGUILayout.HelpBox(
            "В Edit Mode переключение стадий отключено: оно изменило бы объекты сцены и могло бы попасть в Git/сохранение. " +
            "После безопасного Play все тестовые изменения будут только в памяти.", MessageType.None);
    }

    void ShowPlayControls()
    {
        GUILayout.Space(7);
        EditorGUILayout.HelpBox(
            "ИЗОЛИРОВАННЫЙ Play: SaveSystem не записывает на диск. Stop сбросит все тестовые изменения.",
            MessageType.Info);
        if (GUILayout.Button("■ Завершить тестовый Play", GUILayout.Height(28)))
        {
            EditorApplication.isPlaying = false;
            return;
        }
        DrawSiteSelector();
        BuildingController site = SelectedSite;
        if (site == null)
        {
            EditorGUILayout.HelpBox("В текущей сцене нет подходящей стройки.", MessageType.Warning);
            return;
        }

        var story = StoryFlow.Instance;
        var player = PlayerMovement.Instance;
        var inv = PlayerInventory.Instance;
        bool ready = !SaveSystem.IsLoading && !BuildingController.AnyRestorePending &&
                     !BuildingController.AnyRevealing && player != null && inv != null;
        if (story != null) EditorGUILayout.LabelField("Сюжет", story.CurrentChapterId + " / " + story.CurrentChapterPhase);
        EditorGUILayout.LabelField("Объект", site.name + "  [" + site.SiteKind + "]");
        EditorGUILayout.LabelField("Стадии", site.StepsDone + " / " + site.StepsTotal);
        EditorGUILayout.LabelField("Состояние", site.IsFinished ? "Готово" : site.IsBuilding ? "Идёт стройка" : site.IsLocked ? "Заблокировано" : "Можно тестировать");
        EditorGUILayout.LabelField("Требуется", site.Cost + " бр. · " +
            Math.Max(0, site.PlankTotal - site.DepositedPlanks) + " досок · " +
            Math.Max(0, site.CoinTotal - site.DepositedCoins) + " монет");
        EditorGUILayout.LabelField("Рюкзак", inv.woodCount + "/" + inv.Capacity + " бр. · " +
            inv.planksCount + " досок · " + inv.coinsCount + " монет");

        using (new EditorGUI.DisabledScope(!ready))
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("1 · Подготовка и пропуск прогресса", EditorStyles.boldLabel);
            if (site.SiteKind == BuildSiteKind.Bridge)
            {
                EditorGUILayout.HelpBox(
                    "У моста особая связка с GateBridge, StoryFlow, порталом и камерой. " +
                    "Для подготовки моста используй специализированную QA-панель.", MessageType.Info);
                if (GUILayout.Button("Открыть тест моста (0–3 этапа)", GUILayout.Height(32)))
                    BridgeQaWindow.Open();
            }
            else
            {
                if (GUILayout.Button("Подготовить выбранную стройку с нуля", GUILayout.Height(34)))
                    PrepareSite(site);
            }

            EditorGUILayout.LabelField("Мгновенный просмотр состояния (без анимации):");
            using (new EditorGUI.DisabledScope(site.SiteKind == BuildSiteKind.Bridge || site.IsBuilding || site.StageRunning || site.IsRevealing))
            {
                EditorGUILayout.BeginHorizontal();
                for (int i = 0; i <= site.StepsTotal; i++)
                {
                    int step = i;
                    if (GUILayout.Button(step.ToString(), GUILayout.Height(29)))
                        RestoreStep(site, step);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.HelpBox(
                "Кнопки 0…N мгновенно меняют визуальную стадию без опыта, денег, эффектов и наград.",
                MessageType.None);

            GUILayout.Space(10);
            EditorGUILayout.LabelField("2 · Проверка VFX и молотка", EditorStyles.boldLabel);
            if (GUILayout.Button("Переместить Лео к стройке", GUILayout.Height(30)))
                TeleportToSite(site);
            using (new EditorGUI.DisabledScope(site.IsFinished || site.IsBuilding || site.IsRevealing))
            {
                if (GUILayout.Button("Автоматически сдать ресурсы на этап", GUILayout.Height(31)))
                    SupplySite(site);
                if (GUILayout.Button("▶ Строить с НАСТОЯЩИМ молотком", GUILayout.Height(38)))
                    StartRealBuilding(site);
                if (GUILayout.Button("▶ Завершить этап + КАМЕРА/VFX (без молотка)", GUILayout.Height(34)))
                    CompleteNextStage(site);
            }
            if (site.UseWindow && !site.IsFinished && !site.IsRevealing &&
                GUILayout.Button("Открыть обычное окно стройки (UI)"))
                BuildWindow.Toggle(site);

            GUILayout.Space(10);
            EditorGUILayout.LabelField("3 · Инструменты", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+4 брёвна")) Note("Добавлено " + inv.AddWood(4) + " брёвен (не более вместимости).", MessageType.Info);
            if (GUILayout.Button("+50 досок")) { inv.AddPlanks(50); Note("Добавлено 50 досок.", MessageType.Info); }
            if (GUILayout.Button("+2000 монет")) { inv.AddCoins(2000); Note("Добавлено 2000 монет.", MessageType.Info); }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Проверить стройки и их коллайдеры (без изменений)"))
                BuildingIntegrationAudit.Check();
        }
        if (!ready)
            EditorGUILayout.HelpBox(
                "Действия временно недоступны: идёт загрузка, показ построенной стадии или отсутствует Лео/рюкзак.",
                MessageType.Warning);
    }

    void DrawSiteSelector()
    {
        GUILayout.Space(6);
        EditorGUILayout.LabelField("Стройка", EditorStyles.boldLabel);
        if (sites.Count == 0) { EditorGUILayout.LabelField("Нет доступных строек."); return; }
        int next = EditorGUILayout.Popup(Mathf.Clamp(selectedIndex, 0, sites.Count - 1), labels.ToArray());
        if (next != selectedIndex)
        {
            selectedIndex = next;
            preferredSaveId = SelectedSite != null ? SelectedSite.SaveId : "";
        }
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Обновить список")) { refreshAt = 0; RefreshSites(); }
        if (GUILayout.Button("Выделить объект")) FrameSelected();
        EditorGUILayout.EndHorizontal();
    }

    void RefreshSites()
    {
        var previous = SelectedSite;
        if (previous != null) preferredSaveId = previous.SaveId;
        sites.Clear();
        labels.Clear();
        foreach (var b in UnityEngine.Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b == null || !b.gameObject.scene.IsValid() || !b.gameObject.scene.isLoaded) continue;
            sites.Add(b);
        }
        sites.Sort((a, b) =>
        {
            int byKind = a.SiteKind.CompareTo(b.SiteKind);
            return byKind != 0 ? byKind : String.CompareOrdinal(StoryFlow.PathOf(a), StoryFlow.PathOf(b));
        });
        for (int i = 0; i < sites.Count; i++)
            labels.Add(SiteName(sites[i].SiteKind) + "  —  " + StoryFlow.PathOf(sites[i]));
        if (!String.IsNullOrEmpty(preferredSaveId))
        {
            int match = sites.FindIndex(b => b.SaveId == preferredSaveId);
            if (match >= 0) selectedIndex = match;
        }
        selectedIndex = sites.Count == 0 ? 0 : Mathf.Clamp(selectedIndex, 0, sites.Count - 1);
    }

    BuildingController SelectedSite => selectedIndex >= 0 && selectedIndex < sites.Count ? sites[selectedIndex] : null;

    static string SiteName(BuildSiteKind k)
    {
        switch (k)
        {
            case BuildSiteKind.Campfire: return "Костёр";
            case BuildSiteKind.Hut: return "Хижина";
            case BuildSiteKind.Market: return "Лавка";
            case BuildSiteKind.Tent: return "Палатка";
            case BuildSiteKind.Bridge: return "Мост";
            default: return "Другая стройка";
        }
    }

    bool SafeToMutate(BuildingController site)
    {
        if (!Application.isPlaying || !SaveSystem.EditorTestMode || site == null ||
            SaveSystem.IsLoading || BuildingController.AnyRestorePending || BuildingController.AnyRevealing ||
            site.IsBuilding || site.StageRunning || site.IsRevealing)
        {
            Note("Нужно дождаться окончания текущего строительства/показа и включить изолированный Play.", MessageType.Warning);
            return false;
        }
        return true;
    }

    void PrepareSite(BuildingController site)
    {
        if (!SafeToMutate(site)) return;
        if (site.SiteKind == BuildSiteKind.Bridge)
        {
            BridgeQaWindow.Open();
            return;
        }

        // Resolve prerequisites silently using the same built-in editor restore API as Bridge QA.
        var story = StoryFlow.Instance;
        var intro = IntroFlow.Instance;
        if (site.SiteKind != BuildSiteKind.Campfire)
        {
            if (story == null)
            {
                Note("StoryFlow не найден. Открой игровую сцену.", MessageType.Error);
                return;
            }
            if (intro != null) intro.RestoreDoneFromStory();
            if (!PreparePrerequisites(site.SiteKind, site)) return;
        }
        if (!site.EditorTestRestoreSteps(0))
        {
            Note("Не удалось сбросить выбранную стройку.", MessageType.Error);
            return;
        }
        site.SetLocked(false);

        if (site.SiteKind == BuildSiteKind.Campfire)
        {
            if (intro != null)
                intro.Load("{\"version\":1,\"step\":4,\"campfireDone\":false}", SaveSystem.FormatVersion);
        }
        else
        {
            string chapter = ChapterFor(site.SiteKind);
            if (!story.EditorTestJumpToChapter(chapter))
                Note("Стадия подготовлена, но StoryFlow не смог перейти к главе '" + chapter + "'. Проверь запуск сцены.", MessageType.Warning);
        }
        TeleportToSite(site);
        if (String.IsNullOrEmpty(message) || messageType != MessageType.Warning)
            Note("Стройка готова к тесту с нуля. Изменения исчезнут после Stop.", MessageType.Info);
    }

    bool PreparePrerequisites(BuildSiteKind kind, BuildingController selected)
    {
        // Each prerequisite is restored silently; the selected building itself is never completed here.
        int target = (int)kind;
        var all = UnityEngine.Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in all)
        {
            if (b == null || b == selected) continue;
            BuildSiteKind prior = b.SiteKind;
            if (prior == BuildSiteKind.Other || prior == BuildSiteKind.Bridge || (int)prior >= target) continue;
            if (!b.EditorTestRestoreSteps(b.StepsTotal))
            {
                Note("Не удалось подготовить предыдущую стройку: " + b.name, MessageType.Warning);
                return false;
            }
        }
        // StoryFlow grants the appropriate chapter permission; do not change scene assets or scriptable data.
        return true;
    }

    static string ChapterFor(BuildSiteKind kind)
    {
        switch (kind)
        {
            case BuildSiteKind.Hut: return "hut";
            case BuildSiteKind.Market: return "market";
            case BuildSiteKind.Tent: return "tent";
            case BuildSiteKind.Bridge: return "bridge";
            default: return "";
        }
    }

    void RestoreStep(BuildingController site, int step)
    {
        if (!SafeToMutate(site)) return;
        if (site.EditorTestRestoreSteps(step))
            Note("Показана стадия " + step + "/" + site.StepsTotal + " (без наград и VFX).", MessageType.Info);
        else Note("Не удалось переключить стадию.", MessageType.Warning);
    }

    bool SupplySite(BuildingController site)
    {
        if (!SafeToMutate(site) || site.IsFinished || site.IsLocked)
        {
            Note("Стройка недоступна. Сначала нажми «Подготовить стройку».", MessageType.Warning);
            return false;
        }
        var inv = PlayerInventory.Instance;
        if (inv == null) { Note("Рюкзак не найден.", MessageType.Error); return false; }

        // Use normal inventory/deposit APIs. Several batches handle backpack capacity even for the bridge.
        for (int i = 0; i < 512 && !site.AllDeposited; i++)
        {
            if (site.Cost > 0 && inv.woodCount == 0 && inv.FreeSlots > 0)
                inv.AddWood(Mathf.Min(site.Cost, inv.FreeSlots));
            int missingPlanks = Math.Max(0, site.PlankTotal - site.DepositedPlanks - inv.planksCount);
            if (missingPlanks > 0) inv.AddPlanks(missingPlanks);
            int missingCoins = Math.Max(0, site.CoinTotal - site.DepositedCoins - inv.coinsCount);
            if (missingCoins > 0) inv.AddCoins(missingCoins);
            int before = site.Cost + Math.Max(0, site.PlankTotal - site.DepositedPlanks) +
                         Math.Max(0, site.CoinTotal - site.DepositedCoins);
            int moved = site.DepositAll(inv);
            if (site.AllDeposited) break;
            int after = site.Cost + Math.Max(0, site.PlankTotal - site.DepositedPlanks) +
                        Math.Max(0, site.CoinTotal - site.DepositedCoins);
            if (moved <= 0 || after >= before) break;
        }
        bool success = site.AllDeposited;
        Note(success ? "Материалы внесены через обычную логику DepositAll. Можно запускать анимацию." :
             "Не все материалы внесены. Проверь состояние рюкзака, замок и лимиты.",
             success ? MessageType.Info : MessageType.Warning);
        return success;
    }

    void StartRealBuilding(BuildingController site)
    {
        if (!SafeToMutate(site) || site.IsFinished || site.IsLocked) return;
        if (!site.PlayerInside)
        {
            TeleportToSite(site);
            Note("Лео должен стоять внутри триггера стройки. Подойди к зоне или дождись FixedUpdate после телепорта и нажми ещё раз.", MessageType.Warning);
            return;
        }
        if (!SupplySite(site)) return;
        if (!site.UseWindow)
        {
            Note("Для этой площадки отключено UseWindow: стройка запускается автоматически после сдачи материалов.", MessageType.Info);
            return;
        }
        if (site.StartStage())
            Note("Этап запущен обычным путём: Лео подойдёт к рабочей точке и начнёт бить молотком.", MessageType.Info);
        else Note("StartStage отказался запускаться: проверь окно, блокировку или показ предыдущей стадии.", MessageType.Warning);
    }

    void CompleteNextStage(BuildingController site)
    {
        if (!SafeToMutate(site) || site.IsFinished) return;
        if (site.EditorTestCompleteNextStep())
            Note("Этап завершён игровым путём: проверяй VFX, показ стадии и облёт камеры. Это НЕ тест ударов молотка.", MessageType.Info);
        else Note("Не удалось запустить завершение этапа.", MessageType.Warning);
    }

    void TeleportToSite(BuildingController site)
    {
        var player = PlayerMovement.Instance;
        if (player == null || player.ControlLocked || EndingSequence.Active || BuildWorkPose.MovementLocked)
        {
            Note("Лео недоступен: дождись окончания катсцены/анимации.", MessageType.Warning);
            return;
        }
        Vector3 origin = site.transform.position;
        var trigger = site.GetComponent<Collider>();
        if (trigger != null) origin = trigger.bounds.center;

        // Choose the nearest plausible solid floor; never force a position if no ground was found.
        Vector3 target = Vector3.zero;
        bool found = false;
        float bestScore = float.PositiveInfinity;
        float radius = trigger != null ? Mathf.Clamp(Mathf.Min(trigger.bounds.extents.x, trigger.bounds.extents.z), .3f, 2f) : 1f;
        Vector3[] offsets = {
            Vector3.zero, new Vector3(radius * .5f, 0, 0), new Vector3(-radius * .5f, 0, 0),
            new Vector3(0, 0, radius * .5f), new Vector3(0, 0, -radius * .5f),
            new Vector3(radius, 0, 0), new Vector3(-radius, 0, 0),
            new Vector3(0, 0, radius), new Vector3(0, 0, -radius)
        };
        foreach (Vector3 offset in offsets)
        {
            Vector3 p = origin + offset;
            var hits = Physics.RaycastAll(p + Vector3.up * 4f, Vector3.down, 9f, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.normal.y < .65f || hit.transform.IsChildOf(player.transform)) continue;
                if (hit.collider.attachedRigidbody != null) continue;
                if (Mathf.Abs(hit.point.y - site.transform.position.y) > 3.5f) continue;
                Vector3 point = hit.point + Vector3.up * .15f;
                var obstacles = Physics.OverlapCapsule(point + Vector3.up * .25f, point + Vector3.up * 1.5f, .32f, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                foreach (Collider collider in obstacles)
                {
                    if (collider == null || collider == hit.collider || collider.transform.IsChildOf(player.transform)) continue;
                    blocked = true;
                    break;
                }
                if (blocked) continue;
                float score = offset.sqrMagnitude + Mathf.Abs(hit.point.y - site.transform.position.y) * .4f;
                if (score >= bestScore) continue;
                bestScore = score;
                target = point;
                found = true;
            }
        }
        if (!found)
        {
            Note("Не найдено безопасное место рядом со стройкой. Перемести Лео вручную, чтобы не провалиться в геометрию.", MessageType.Warning);
            return;
        }
        player.CancelWalk();
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = target;
        }
        player.transform.position = target;
        Physics.SyncTransforms();
        CameraFollow.SnapToTarget();
        Note("Лео перемещён к " + SiteName(site.SiteKind) + ". Если триггер ещё не сработал, подвинься на шаг.", MessageType.Info);
    }

    void FrameSelected()
    {
        BuildingController b = SelectedSite;
        if (b == null) return;
        Selection.activeGameObject = b.gameObject;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.Frame(new Bounds(b.transform.position, new Vector3(8f, 8f, 8f)), false);
    }

    void Note(string text, MessageType type)
    {
        message = text;
        messageType = type;
        Debug.Log("[Construction QA] " + text);
        Repaint();
    }
}
