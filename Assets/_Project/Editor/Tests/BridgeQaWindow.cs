// Быстрые проверки главы с мостом, не меняющие обычные сейвы и сцену.
// Все действия с прогрессом выполняются только в Play Mode с включённой изоляцией.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BridgeQaWindow : EditorWindow
{
    const string Menu = "Tools/BuilderSim/Отладка и тесты/Мост и финал/";
    const string MainScene = "Assets/_Recovery/0 (8).unity";

    Vector2 scroll;

    [InitializeOnLoadMethod]
    static void RegisterPlayModeCleanup()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        // SessionState может пережить перезагрузку домена, но не следующий обычный Play.
        if (change == PlayModeStateChange.EnteredEditMode)
            SessionState.SetBool(SaveSystem.EditorTestSessionKey, false);
    }

    [MenuItem(Menu + "Открыть панель", false, 1040)]
    public static void Open()
    {
        var window = GetWindow<BridgeQaWindow>("TimberWings · QA");
        window.minSize = new Vector2(370f, 520f);
        window.Show();
    }

    [MenuItem(Menu + "1. К началу стройки", false, 1041)]
    static void MenuPrepare() { if (Ready()) PrepareBridge(); }

    [MenuItem(Menu + "2. Достроить мост", false, 1042)]
    static void MenuComplete() { if (Ready()) SetBridgeSteps(3); }

    [MenuItem(Menu + "3. К порталу", false, 1043)]
    static void MenuPortal() { if (Ready()) GoToPortal(); }

    void OnInspectorUpdate()
    {
        if (EditorApplication.isPlaying) Repaint();
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Space(8);
        EditorGUILayout.LabelField("Тестирование моста и финала", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Мост — 3 оплачиваемых этапа. Быстрая смена состояния не запускает награды и облёт. Отдельная кнопка запускает обычное завершение этапа для проверки камеры.", MessageType.Info);

        GateBridge gate = FindGate();
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.LabelField("Сцена", SceneManager.GetActiveScene().path);
            if (gate == null) EditorGUILayout.HelpBox("В открытой сцене нет GateBridge.", MessageType.Warning);
            else if (!gate.HasBakedFinale) EditorGUILayout.HelpBox("Портал не запечён. Перед финальными тестами выполни Editor Bake и сохрани сцену.", MessageType.Warning);

            using (new EditorGUI.DisabledScope(gate == null))
            {
                if (GUILayout.Button("Проверить Editor Bake", GUILayout.Height(27)))
                    EditorUtility.DisplayDialog("GateBridge", GateBridgeBakeCheck.Validate(gate), "OK");
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScene) != null &&
                    GUILayout.Button("Открыть игровую сцену с мостом"))
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        EditorSceneManager.OpenScene(MainScene);
                }
                using (new EditorGUI.DisabledScope(gate == null))
                {
                    if (GUILayout.Button("▶ Play для тестов (сейвы защищены)", GUILayout.Height(38)))
                    {
                        SessionState.SetBool(SaveSystem.EditorTestSessionKey, true);
                        EditorApplication.isPlaying = true;
                    }
                }
            }
            EditorGUILayout.EndScrollView();
            return;
        }

        if (!SaveSystem.EditorTestMode)
        {
            EditorGUILayout.HelpBox("Play запущен обычным способом. Для изменения сюжета сначала включи защиту сохранений. Рекомендуется запускать Play кнопкой выше: защита тогда действует с первого кадра.", MessageType.Warning);
            if (GUILayout.Button("Включить защиту сохранений сейчас"))
            {
                SessionState.SetBool(SaveSystem.EditorTestSessionKey, true);
                SaveSystem.EnableEditorTestMode();
            }
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.HelpBox("Изолированный Play: автосохранение и NewGame заблокированы. Настоящий сейв не изменится; Stop вернёт обычный режим.", MessageType.Info);
        StoryFlow story = StoryFlow.Instance;
        BuildingController site = FindBridgeSite(gate, story);
        var player = PlayerMovement.Instance;
        EditorGUILayout.LabelField("Глава", story != null ? story.CurrentChapterId + " / " + story.CurrentChapterPhase : "StoryFlow не найден");
        EditorGUILayout.LabelField("Стройка", site != null ? site.StepsDone + "/" + site.StepsTotal + (site.IsFinished ? " · готово" : " · в процессе") : "BuildZone не найдена");
        EditorGUILayout.LabelField("Проход", gate != null && gate.WalkwayActive ? "открыт" : "закрыт");
        if (gate != null && !gate.HasBakedFinale) EditorGUILayout.HelpBox("Нет запечённого портала. Стройку можно тестировать, финал — после Editor Bake.", MessageType.Warning);
        if (story == null || site == null || gate == null || player == null)
            EditorGUILayout.HelpBox("Для проверки нужна игровая сцена с StoryFlow, GateBridge/BuildZone и Лео.", MessageType.Error);

        bool usable = story != null && site != null && gate != null && player != null && !BuildingController.AnyRestorePending && !SaveSystem.IsLoading;
        using (new EditorGUI.DisabledScope(!usable))
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Сюжет и строительство", EditorStyles.boldLabel);
            if (GUILayout.Button("Скипнуть всё до моста · этап 0", GUILayout.Height(34))) PrepareBridge();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("0 · Сломан")) SetBridgeSteps(0);
            if (GUILayout.Button("1 · Опоры")) SetBridgeSteps(1);
            if (GUILayout.Button("2 · Настил")) SetBridgeSteps(2);
            if (GUILayout.Button("3 · Готов")) SetBridgeSteps(3);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Завершить следующий этап с ОБЛЁТОМ"))
            {
                if (!site.EditorTestCompleteNextStep())
                    Debug.LogWarning("[Bridge QA] Этап не запущен: стройка готова, идёт показ, загрузка или первый кадр Play.", site);
            }
            if (GUILayout.Button("Выдать материалы для стройки (+4 бр., +10 досок, +500 монет)"))
            {
                PlayerInventory inventory = PlayerInventory.Instance;
                if (inventory != null)
                {
                    inventory.AddWood(4);
                    inventory.AddPlanks(10);
                    inventory.AddCoins(500);
                }
            }

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Перемещение Лео", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("К стройке")) Teleport(site.transform.position);
            if (GUILayout.Button("Начало моста")) Teleport(gate.DeckEntrance - gate.BridgeForward * 2.3f);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!site.IsFinished))
            {
                if (GUILayout.Button("На дальний остров") && gate.arrivalSpot != null)
                    Teleport(gate.arrivalSpot.position);
                if (GUILayout.Button("К камню портала")) GoToPortal();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Финал", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!site.IsFinished || !gate.HasBakedFinale))
            {
                if (GUILayout.Button("Активировать портал (реальный сценарий)", GUILayout.Height(31)))
                {
                    var portal = FindPortal(gate);
                    if (portal == null) Debug.LogWarning("[Bridge QA] Нет запечённого портала.");
                    else if (!portal.CanInteract)
                        Debug.LogWarning("[Bridge QA] Портал недоступен. Перейди в главу gunnar, подойди к камню, убедись, что идёт обычное управление.", portal);
                    else portal.Interact();
                }
            }
            if (GUILayout.Button("Только ролик и титры (без портала)"))
                EndingSequence.Play(() => Debug.Log("[Bridge QA] Ролик и титры завершены."));
        }
        GUILayout.Space(10);
        if (GUILayout.Button("Перезагрузить состояние из настоящего сейва"))
        {
            if (!SaveSystem.Load()) Debug.LogWarning("[Bridge QA] Сохранение для восстановления не найдено.");
        }
        EditorGUILayout.EndScrollView();
    }

    static bool Ready()
    {
        if (!Application.isPlaying || !SaveSystem.EditorTestMode)
        {
            Open();
            Debug.LogWarning("[Bridge QA] Открой окно и включи изолированный Play для тестов.");
            return false;
        }
        if (FindGate() == null || FindBridgeSite(FindGate(), StoryFlow.Instance) == null ||
            StoryFlow.Instance == null || PlayerMovement.Instance == null || SaveSystem.IsLoading || BuildingController.AnyRestorePending)
        {
            Debug.LogWarning("[Bridge QA] Дождись загрузки игровой сцены с GateBridge, StoryFlow и Лео.");
            return false;
        }
        GateBridge gate = FindGate();
        BuildingController site = FindBridgeSite(gate, StoryFlow.Instance);
        BuildStageLink link = site.GetComponent<BuildStageLink>();
        if (StoryFlow.Instance.bridge != site || gate.source == null || link == null ||
            link.build != site || link.bridge != gate.source)
        {
            Debug.LogError("[Bridge QA] Нарушена связка StoryFlow.bridge → BuildZone → BuildStageLink → RiverBridge. " +
                           "В Edit Mode пересобери GateBridge и выполни проверку Editor Bake.", site);
            return false;
        }
        return true;
    }

    static GateBridge FindGate() => Object.FindFirstObjectByType<GateBridge>(FindObjectsInactive.Include);
    static FinalIslandPortal FindPortal(GateBridge gate) => gate != null ? gate.GetComponentInChildren<FinalIslandPortal>(true) : null;

    static BuildingController FindBridgeSite(GateBridge gate, StoryFlow story)
    {
        if (gate != null)
        {
            Transform zone = gate.transform.Find("BuildZone");
            if (zone != null && zone.TryGetComponent(out BuildingController component)) return component;
        }
        return story != null && story.bridge != null && story.bridge.SiteKind == BuildSiteKind.Bridge ? story.bridge : null;
    }

    static bool PrepareBridge(bool movePlayer = true)
    {
        if (!Ready()) return false;
        GateBridge gate = FindGate();
        StoryFlow story = StoryFlow.Instance;
        BuildingController site = FindBridgeSite(gate, story);
        // Сначала доводим предыдущие здания до готовности без денег/опыта/повторных событий.
        foreach (BuildingController previous in new[] { story.hut, story.market, story.tent })
        {
            if (previous != null && !previous.EditorTestRestoreSteps(previous.StepsTotal))
            {
                Debug.LogWarning("[Bridge QA] Не удалось восстановить " + previous.name, previous);
                return false;
            }
        }
        foreach (BuildingController previous in Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (previous.SiteKind == BuildSiteKind.Campfire &&
                !previous.EditorTestRestoreSteps(previous.StepsTotal))
            {
                Debug.LogWarning("[Bridge QA] Не удалось подготовить костёр " + previous.name, previous);
                return false;
            }
        }
        PlayerStats stats = PlayerStats.Instance;
        if (stats != null)
        {
            int level = Mathf.Max(stats.currentLevel, GameBalance.BridgeLevel);
            stats.Load("{\"level\":" + level + ",\"xp\":" + Mathf.Max(stats.currentXP, PlayerStats.XpForLevel(level)) + "}", SaveSystem.FormatVersion);
            PlayerSkills.NotifyStatsReady();
        }
        if (!site.EditorTestRestoreSteps(0) || !story.EditorTestJumpToChapter("bridge"))
        {
            Debug.LogError("[Bridge QA] Не удалось перейти к главе 'bridge'. Проверь StoryFlow и BuildZone.");
            return false;
        }
        if (movePlayer) Teleport(site.transform.position);
        Debug.Log("[Bridge QA] До моста: ранние здания готовы, сюжет на 'bridge', стройка 0/3. Прогресс не сохранится.");
        return true;
    }

    static void SetBridgeSteps(int completed)
    {
        if (!PrepareBridge(false)) return;
        GateBridge gate = FindGate();
        BuildingController site = FindBridgeSite(gate, StoryFlow.Instance);
        if (!site.EditorTestRestoreSteps(completed)) return;
        if (completed >= site.StepsTotal)
            StoryFlow.Instance.EditorTestJumpToChapter("gunnar");
        Teleport(site.transform.position);
        Debug.Log("[Bridge QA] Сдано " + site.StepsDone + "/" + site.StepsTotal + ", проход: " + (gate.WalkwayActive ? "открыт" : "закрыт") + ".", gate);
    }

    static void GoToPortal()
    {
        if (!Ready()) return;
        GateBridge gate = FindGate();
        BuildingController site = FindBridgeSite(gate, StoryFlow.Instance);
        FinalIslandPortal portal = FindPortal(gate);
        if (!site.IsFinished || portal == null || !portal.IsBaked)
        {
            Debug.LogWarning("[Bridge QA] Сначала дострой мост и сделай Editor Bake с порталом.");
            return;
        }
        StoryFlow.Instance.EditorTestJumpToChapter("gunnar");
        Teleport(portal.Anchor.position);
    }

    static void Teleport(Vector3 position)
    {
        PlayerMovement player = PlayerMovement.Instance;
        if (player == null || player.ControlLocked || EndingSequence.Active)
        {
            Debug.LogWarning("[Bridge QA] Лео сейчас занят анимацией/катсценой, телепорт отменён.");
            return;
        }
        // Берём пол рядом с заданной высотой: луч сверху может первым встретить арку
        // или перила, на которые Лео телепортировать нельзя.
        float targetHeight = position.y;
        float closest = 2.2f;
        foreach (RaycastHit hit in Physics.RaycastAll(position + Vector3.up * 3f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(player.transform) || hit.collider.attachedRigidbody != null || hit.normal.y < .55f)
                continue;
            float offset = Mathf.Abs(hit.point.y - targetHeight);
            if (offset >= closest) continue;
            closest = offset;
            position.y = hit.point.y + .15f;
        }
        player.CancelWalk();
        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
        }
        player.transform.position = position;
        Physics.SyncTransforms();
        CameraFollow.SnapToTarget();
        Selection.activeGameObject = player.gameObject;
    }
}
