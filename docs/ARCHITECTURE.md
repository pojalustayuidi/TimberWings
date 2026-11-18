# Техническая архитектура

Срез 9afd413 / Unity 6000.3.10f1. Справочник исходников, не полный граф ссылок сцены. URP, C#.

## Структура

`Assets/_Project/Scripts` — runtime; `Editor` — редакторские билдеры, инструменты и проверки. `Data` содержит PetDatabase/WorkerDatabase и каталоги; `Generated`, `IslandV2`, `Resources`, `Prefabs`, `Art`, `Audio`, `Animation` — данные и визуал. `Packages`, `ProjectSettings` — Unity. Сцены: `Assets/_Project/Scenes/MainMenu.unity`, `MainLevel.unity`, тестовые `TreeShowcase.unity`, `TreeToonTest.unity`. Активные сцены сборки сверяются в Build Profiles.

| Подсистема | Пути внутри Scripts |
|---|---|
| Ядро | Core/GameBalance, Pricing, GameEvents, GameSettings, MobileInput, SceneRef, PerformanceBoot, Telemetry |
| Управление | Player/PlayerMovement, PlayerInteraction, PlayerInventory, PlayerStats, PlayerUpgrades, Player/Leo, Camera |
| Рубка | Resourse/ResourceItem, TreeSizing, TreeRespawn, TreeGrowthVisual, TreeCollision, ResourceHighlighter, TreeSpeciesInfo; Interaction/TreePolish, Forest |
| Стройка | Build/BuildingController, BuildStageLink, BuildSiteInteractable, Build/Stages; Workshop/PlankWorkshop |
| Торговля | Core/Pricing, Npc/TradingStall, UI/Construction |
| Сюжет | Tutorial/IntroFlow, ArrivalIntroSequence, StoryFlow, QuestGuide; Dialogue/DialogueManager, DialogueSequence, Speaker, VoiceBank |
| Питомцы | Pets/PetManager, PetData, PetDatabase, PetRoller, PetBonus, PetStage, PetFollower |
| Рабочие | Workers/WorkerManager, WorkerBalance, CampSimulation, WorkerCamp, WorkerPackSystem, WorkerPackRoller, Workers/Workyard |
| Навыки | Skills/PlayerSkills, SkillTree, LumberSkills, HostEffects |
| Достижения | Achievements/AchievementManager, AchievementDefs |
| Мир | Location/FloatingIsland, IslandV2Data, IslandEdgeGuard, RiverBridge, GateBridge, WaterSwim, WaterVolume; Core/DayNightCycle; Forest |
| Осмотры | Interaction/Inspection |
| Карта | UI/Map/HudMinimap.* (Bake, Fog, Layout, Progress, BigMap, CloudyMapV2/V3, CloudyMapWindow, Route), MapPoi, MapIcons |
| Финал | Location/Finale/FinalIslandPortal.*, GateBridgeFinale, LeoPortalPose; UI/Screens/EndingSequence.* |
| Сохранения | Save/SaveSystem, ISaveable, SaveAutoRunner, DialogueSeen |
| UI | UI/Core, Kit, Hud, HudPolish, Construction, Progression, Workers, Pets, Screens, menu, Localization |

## Основные потоки

- Добыча: MobileInput → PlayerInteraction → ресурс/TreeHits → инвентарь/статы → UI и события → сохранение. Визуальная анимация не должна начислять ресурс второй раз.
- Стройка: взаимодействие → BuildingController/стадия → списание ресурсов → BuildStageLink и визуал → сюжет/карта/save. Загрузка не выдаёт награду второй раз.
- Прогресс: PlayerStats + PlayerInventory (монеты) → PlayerUpgrades/PlayerSkills → расчёт бонусов (в т.ч. PetBonus) → UI. Pricing — торговля; WorkerBalance — рабочие.
- Сюжет: IntroFlow и StoryFlow с фазами → GateBridge/FinalIslandPortal → EndingSequence. Завершение должно быть идемпотентным при skip, ошибке видео, возврате, загрузке.
- Карта: HudMinimap.Bake классифицирует рендереры, временно заменяет материалы, вызывает camera.Render и восстанавливает материалы; URP post shader/Resources, динамические метки и fog отдельны. Подготовка по кадрам, перепечатка по событиям. CloudyMapV3 есть в коде, но наличие флага не гарантирует результат билда.
- SaveSystem: JSON `Application.persistentDataPath/timberwings_save.json`, секции ISaveable, версия формата 1, .tmp/.bak/.corrupt. Читает основной/бэкап, при двух повреждённых — новый старт. Порядок загрузки важен. Editor QA test mode не пишет настоящий сейв. New Game очищает данные; тесты на отдельных файлах.

## Контракты UI и Unity

- UI преимущественно строится кодом. UiKit/UiDraw/UiLayout — стиль, UiLayers — уровни canvas, UiModal — модальные окна, UiInputGuard.EnsureEventSystem — единая система ввода. Не создавать второй EventSystem.
- В новом окне регистрировать UiModal, освобождать регистрацию на OnDestroy, проверять AnyOpen, обрабатывать Esc и корректный возврат управления. Общие элементы в UI/Core или Kit, не размножать в окнах.
- Перед правкой partial-класса открыть все части и найти Unity lifecycle-методы. Не создавать вторые Awake/Update/LateUpdate.
- Проверять ссылки MonoBehaviour, prefab, scene, .meta/GUID перед удалением и перемещением. Не переименовывать существующие классы и пути механически (Resourse, menu).
- Числа в одном источнике на систему, Inspector-переопределения документировать. Не копировать формулы в UI.
- Editor-билдеры не должны требоваться в runtime. Запечённые меши, материалы, текстуры и прочие активы сохранять с .meta, включать зависимости в билд.
- Сторонние ассеты, резервные копии и устаревшие генераторы удалять только отдельной задачей после аудита ссылок.

## Editor и QA

Группы: Editor/IslandV2, GateBridge, Story, StoryInspection, TWLeo, TWLeoAnimation, TWMystic, TWTreePolish, TWWorkyard, TWHud, Pets, Workers, UI, Tests, Tools. Исторически Tools/BuilderSim разбито по подсистемам. Точные меню и успешность проверять в Unity; наличие скрипта не равно PASS.

## Технический долг / неизвестное

Сцены и часть ассетов хранятся через LFS; через исходники не подтверждены активные компоненты, Build Profiles, URP-конфигурация, зависимость роликов и материалов в standalone. В QA входят карта, финал, дубликаты EventSystem, старые warnings DrawGizmo, производительность, аудит файлов и сейвов. См. ROADMAP. Исходный AI_CONTEXT.md исторический: не считать старый блокаут актуальным состоянием острова.
