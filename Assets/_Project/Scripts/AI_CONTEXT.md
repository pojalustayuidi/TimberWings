# Карта проекта для людей и нейросетей

Читай этот файл первым. Unity 6000.3, URP. Игра про лесоруба (в меню встречаются имена BuilderSim и Timber Wings, это одна игра).
Весь UI строится из кода (нет префабов окон), тексты на русском.

## 1. Папки

`Scripts/` (рантайм) и `Editor/` (только редактор, генераторы и меню Tools).

| Папка Scripts | Что внутри |
|---|---|
| Core | время суток, баланс (`GameBalance`), события, `MobileInput` (ввод + `ModalOpen`), настройки |
| Player | движение, инвентарь, статы, прокачка, взаимодействие |
| Build, Workshop, Npc, Interaction | стройки (`BuildingController`), пилорама, торговцы, подсказки E |
| Workers, Pets, Skills, Achievements | рабочие, питомцы, дерево навыков, достижения (логика) |
| Location | парящий остров (`FloatingIsland`, `IslandEdgeGuard`, `GateBridge`, `CloudSea`), настроение острова v2: `IslandLook` (+`.Haze`: дымка, тон теней земли) |
| Forest, Resourse | лес и деревья (папка названа с опечаткой, переименуй в Unity) |
| Tutorial, Dialogue | сюжет (`StoryFlow`, `IntroFlow`), реплики и озвучка |
| Vibe, Lights, Camera, Audio, Music | эффекты, огни, камера, звук |
| Save | сохранение (`SaveSystem`, `ISaveable`) |
| UI | весь интерфейс, см. раздел 3 |

В корне `Scripts` скриптов нет, всё лежит по папкам: этапы зданий в `Build/Stages`, `FloatingIsland` (он же граница карты), `MapZone`, `IslandAbyss`, `RiverFlow` в `Location`, `Speaker` в `Dialogue`, `ActionPrompt` в `Interaction`, `CampfireFlicker` в `Lights`.

## 2. Меню Tools (Editor)

Всё лежит в `Tools/BuilderSim/<Группа>/<Действие>`, у каждого пункта задан приоритет:
Локация (100+), Лес (200+), Здания (300+), Мир и персонаж (400+), Рабочие (500+), Питомцы (600+), Сюжет (700+), Визуал и свет (800+), UI (900+), Отладка и тесты (1000+).
Новый пункт добавляй в нужную группу: `[MenuItem("Tools/BuilderSim/Локация/Название", false, 106)]`.
Меню `GameObject/HUD Gain/...` отдельное, его не трогаем.

## 3. UI: правила

Папка `Scripts/UI` разложена по темам:

| Подпапка | Что внутри |
|---|---|
| Kit | `UiKit`, `UiDraw`, `UiLayout`, `UiKitShowcase`: общий стиль и рисование |
| Core | `UiLayers`, `UiModal`, `UiInputGuard`, `UiClickFallback`, safe area |
| Hud | основной HUD: `UIManager`, `HudGain*`, `HudPlanks`, `ObjectiveView`, `RewardPopups`, `ZoneToast`, `SaveIndicator`, `ChopRewards` |
| Map | миникарта и большая карта: `HudMinimap.*`, `MapIcons`, `MapPoi`, `MinimapPost.shader` |
| Construction | окна стройки, пилорамы и продажи |
| Progression | прокачка, дерево навыков, достижения (`UpgradeWindow`, `SkillTreeUi`, `Achievements*`) |
| Workers | окно рабочих, `WorkerUi`, колесо резерва |
| Pets | коллекция питомцев, клеть, `PetUi`, отладочная панель |
| Screens | полноэкранные: `SettingsMenu`, `EndingSequence` |
| menu | главное меню: `MainMenuStub`, `MenuFlow`, `SaveSummary` и др. |

Новое окно кладём в подпапку по теме (нет подходящей: создай новую). Общие вещи, нужные всем окнам, лежат в `Core` и `Kit`.

1. **Слои канвасов**: только через `UiLayers` (`canvas.sortingOrder = UiLayers.Window;`). Голые числа не писать.
2. **Модальные окна**: окно регистрируется в `UiModal`.
   - Окно с экземпляром: `UiModal.Register(this, () => IsOpen, Close)` в `Awake`, `UiModal.Unregister(this)` в `OnDestroy`.
   - Окно со статическим `IsOpen`: статический метод `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] RegisterModal()`.
   - Проверка «открыто ли другое окно»: `UiModal.AnyOpen(this)`. Блокировка героя: `MobileInput.ModalOpen` (внутри `UiModal.AnyOpen()`).
   - Новое окно не надо дописывать в чужие списки.
3. **EventSystem**: перед построением UI вызывай `UiInputGuard.EnsureEventSystem()`. Самому `new GameObject("EventSystem")` не создавать.
4. **Esc** и пауза (`Time.timeScale`) пока обрабатываются внутри каждого окна (`UiModal.CloseTop()` готов, но окна его не вызывают).
5. Стиль: `UiKit`, `UiDraw`, `UiLayout`.

### Большие окна разбиты на части (`partial class`)

В каждой части сверху комментарий: что в ней, список методов и остальные части. Поведение при разбиении не менялось.

| Класс | Части |
|---|---|
| `PetCollectionWindow` | основной (API, размеры, цвета, состояние, жизненный цикл), `.Actions`, `.Fill`, `.DeltaBook`, `.Build`, `.Presets`, `.Interact`, `.HudHelpers` |
| `WorkersWindow` | основной (+ клавиши и обновление), `.Tabs`, `.PackOpen`, `.Build` |
| `PetCrateWindow` | основной (+ действия), `.Screens`, `.Update`, `.Build` |
| `AchievementsWindow` | основной, `.Anim`, `.TabsData`, `.Build`, `.HudHelpers` |
| `HudMinimap` | `HudMinimap.*.cs` (Bake, BigMap, Events, Fog, Ground, Layout, MiniZoom, Route) |

Где что искать: поля и состояние лежат в основном файле; построение интерфейса (создание объектов) в `.Build`; обновление данных в `.Fill`, `.Tabs` и подобных.
Если правишь окно, сначала открой основной файл и прочитай карту частей в его шапке.

## 4. Как работать с этим проектом без потери контекста

- Перед правкой найди все использования символа поиском по всему проекту (в том числе Editor), не по одному файлу.
- Сцена бинарная (`*.unity`): какие компоненты на ней висят, из кода не видно. Перед удалением скрипта проверь его в Unity (поиск `t:ИмяКласса` в Hierarchy).
- Не переименовывай файлы MonoBehaviour: Unity привязывает компонент к файлу с тем же именем, что и класс (части `partial` можно называть как угодно).
- Один файл не больше примерно 700 строк: больше разбивай на части `partial` по разделам `// ====`.

## 5. Состояние работ

- Остров v2 (переписываем локацию заново), пакет `Editor/IslandV2`, меню `Tools/BuilderSim/Локация/Остров v2/` (120-126). Этапы: 1 серый блокаут и проходимость (код написан заново, в Unity не запускался), 2 цвет земли и троп, 3 свет, небо, дымка (код написан, в Unity не запускался), 4 обрыв и облака, 5 деревья и мелочь, 6 вода и водопады. Пункты меню: 1 раскладка (120), 2 собрать блокаут (121), 3 проверить блокаут (122), 4 замер Batches/Tris блокаута в Play Mode (123), 5-7 свет и дымка (124-126). Следующий свободный приоритет меню: 127.
- Этап 1, блокаут и проходимость (код написан, в Unity не запускался). Файлы `Editor/IslandV2`: `IslandLayout` (ScriptableObject, ВСЕ числа раскладки, ассет `Assets/_Project/IslandV2/IslandLayout.asset`: контур, рельеф, река, пруд, метки и здания, тропы, поляны, лес-заглушки, меш и Terrain, серые цвета, три профиля бюджета), `IslandField` (чистая математика по раскладке: контур по площади 15 400 м², высота, вода, броды, тропы, площадки; Y=0 на земле у костра), `IslandBlockout` (+`.Ground`: невидимый Terrain-коллайдер с дырами по контуру и видимые меш-чанки с цветом вершин; +`.Props`: серые коробки `BLK_*` с BoxCollider, лес-заглушки одним мешем `BLK_Trees` плюс капсулы-коллайдеры без рендера, маркеры), `IslandCheck` (контракт, место, лучи вниз, уклон, заливка проходимости по сетке 1 м, замер в Play Mode). Шейдер земли и заглушек `Shaders/IslandV2/IslandVertexColor.shader` (`Project/IslandV2/VertexColor`: свет, тени, туман, тон теней `_IslandShadowTint` от `IslandLook`).
  - Структура в сцене: корень `IslandV2` с детьми `_Generated` (пересобирается ЦЕЛИКОМ), `_Manual` (генератор не трогает), `Markers` (метки создаются, если нет; если есть, переставляются по раскладке). Метки: `CAMPFIRE_Point` (0,0,0), `SPAWN_Player`, `PAD_Hut`, `PAD_Market`, `PAD_QuestBoard`, `PAD_Tent`, `PAD_Forest`, `ForestGate`, `DEFERRED_Bridge` (у края по углу, мост отложен). Метки лежат прямо в `Markers`, без папки `Pads`.
  - Моста нет: только метка `DEFERRED_Bridge`, тропа к ней и проверка подхода. Здания пока серые коробки `BLK_*`, настоящие `BuildingController` ставят билдеры `Здания/*` на метки `PAD_*`. Старую локацию в сцене Island_v2 надо отключить (иначе дубли `CAMPFIRE_Point`, второй Terrain, `FloatingIsland`): сборка и проверка об этом предупреждают.
  - Правило воды: река вне брода и пруд считаются непроходимыми только в проверке (`waterBlocksWalk`). Физически блокаут воду не держит: русло мелкое, Terrain сплошной. Настоящие правила воды (плавание, утопление) этап 6.
  - РАЗРЫВ С ЭТАПОМ 3: `IslandLook` (дымка, тон теней), `CloudSea`, `IslandAbyss`, `IslandEdgeGuard`, `LightingRig` ищут `FloatingIsland` (контур `Outline`, `GroundY`, `depth`, цвета скалы). В сцене Island_v2 его нет, поэтому они там не включатся, пока этап 4 не даст им контур и высоту из `IslandField` (например, общий интерфейс «данные острова» вместо прямой ссылки на `FloatingIsland`). Падение с края без `IslandEdgeGuard` не обрабатывается.
- Этап 3, свет, небо, дымка (код написан, в Unity не запускался). Принцип: ничего не дублируем, все числа в ассете `CloudCandyStyle` (Resources), он уже ведёт `DayNightCycle`, `LightingRig`, `SkyDome`, `IslandAbyss`. Новые числа лежат в `CloudCandyStyle.Island.cs` (partial): `sunYawNoon`, облака неба, `fogMulIsland`, цвета фона `CloudSea` (`sea*`), тон теней земли (`groundShadow*`), блок `haze` (дымка), три профиля бюджета `budgetMobileLow` / `budgetMobileHigh` / `budgetPc` (= качество света Низкое / Среднее / Высокое). Палитру неба, света и ambient Cloud Candy НЕ трогали (она уже голубо-лавандовая, под референс).
  - Новое: `Scripts/Location/IslandLook.cs` + `IslandLook.Haze.cs` (создаётся само при `FloatingIsland` и игроке; глобальная переменная `_IslandShadowTint`, дымка: карточки по краю и в низинах одним instanced draw call, `Shaders/IslandV2/IslandHaze.shader`); `Editor/IslandV2/IslandLookSetup.cs` (меню 124 стиль, 125 шейдеры в сборку и URP), `IslandLookCheck.cs` (меню 126 проверка и замер Batches/Tris с дымкой и без).
  - Изменено: `IslandVertexColor.shader` (добавлены туман и тон теней: раньше земля единственная не уходила в дымку), `CloudCandyStyle.cs` (стал partial, `ApplyTo` передаёт sunYawNoon и облака неба), `LightingRig.cs` (дальность теней из профиля при `useBudgetShadowDistance`), `CloudSea.cs` (цвета фона из стиля), `IslandAbyss.cs` (множитель тумана из стиля), `CloudCandyStyleSetup.cs` (+`GetOrCreate`).
  - Не сделано на этапе 3: сами облака и обрыв (этап 4), лучи солнца, объёмный туман, смена каскадов теней из кода (ставятся руками в URP Asset каждого профиля, проверка только сверяет), любые правки UI, сохранений, моста, `StoryFlow`.
  - Все значения дымки, фона облаков и тона теней подобраны на глаз без запуска: смотреть в Unity и крутить в ассете Cloud Candy (в Play Mode правки видны сразу).
- Стартовая локация переделывается в парящий остров (убрать каменные границы, сгладить края, падение с экраном «вы упали», респаун в точке, сток воды, потом дирижабли). Мост на следующую локацию остаётся.
- Убрано из локации: река, мост через реку, скала у истока, водопад (`IslandWaterfall`, `WaterfallCliff`, `LocationRiver`, `LocationHeadMountain`, `LocationSpring`, `RiverFlow`, шейдеры водопада). Единая точка сборки: `Tools/BuilderSim/Локация/Собрать локацию целиком`.
- Остатки реки в генераторе рельефа (`LocationGen.River2.cs`, `CarveRiver*`, поля `river*` в настройках) выключены флагом `buildRiver = false` и ждут второй чистки. `RiverBridge` оставлен: на нём держатся `GateBridge`, `StoryFlow`, `BuildStageLink`.
- Кандидаты на удаление (проверить в Unity): `ChopRewardsTester`, `HudBarSpark`, `SafeAreaFitter` (дубль `HudSafeArea*`), `FloatingText`, `MainMenuLife`, `PlayerAnimator 1.controller`, старый режим «+N» в `UIManager`, миграция сохранений v1 в `WorkerManager`.
