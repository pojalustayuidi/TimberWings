using UnityEngine;

// Assets/_Project/Scripts/Core/GameBalance.cs (НЕ в Editor)
// Единый источник чисел по GDD v0.2. Если число меняется, меняй его здесь, а не в десяти скриптах.
//
// Что НЕ подтягивается само (лежит в Inspector и сцене):
//   PlayerMovement.speed и ResourceItem.gatherTime. Их выставит меню Tools > BuilderSim > Настроить старт игры.
public static class GameBalance
{
    // ---------- игрок ----------
    public const float PlayerSpeed = 4f;      // м/с
    public const float FullBackpackSlowdown = 0.25f;   // -25% скорости × заполненность рюкзака

    // ---------- рубка ----------
    public const float ChopTime = 3f;         // с удержания E на дерево (ResourceItem.gatherTime)
    public const int HitsPerTree = 1;         // одно удержание = одно дерево
    public const int WoodPerTree = 2;         // бревна с дерева
    public const int StartBackpack = 4;       // стартовая вместимость
    public const int CampfireTrees = 4;       // GDD: 4 дерева (8 бревен) на костёр. Рюкзак 4, поэтому стройка принимает бревна частями
    public const float ChopGrace = 0.5f;      // после отпускания E прогресс рубки держится 0.5 с
    public const float TreeRespawnSeconds = 45f;   // GDD: дерево вырастает через 45 с

    // ---------- размеры деревьев (TreeSizing, ResourceItem, TreeRespawn, TreeGrowthVisual, LumberSkills.BaseWood, PlayerInteraction.GatherTime) ----------
    // Порядок во всех массивах = enum TreeSize: [0] молодое, [1] зрелое, [2] старое. Зрелое совпадает со старыми числами (2 бревна, ×1, 45 с).
    // Среднее бревен с дерева при долях ниже: 0.30×1 + 0.45×2 + 0.25×4 = 2.2 (было 2), экономика «4 дерева = 8 бревен» почти не сдвинулась.
    public static readonly int[] TreeLogs = { 1, 2, 4 };                  // бревна с дерева
    public static readonly float[] TreeChopMul = { 0.7f, 1f, 1.5f };       // множитель времени рубки (поверх силы топора, серии и навыков)
    public static readonly float[] TreeRespawnBySize = { 30f, 45f, 75f };  // секунды роста без навыков (TreeRespawnSeconds выше = зрелое, оставлена для совместимости)
    public static readonly float[] TreeScaleMul = { 0.7f, 1f, 1.3f };      // масштаб корня дерева (читается с изометрии: стволы и кроны разного размера)
    public static readonly float[] TreeSwayMul = { 1.3f, 1f, 0.7f };       // амплитуда качания на ветру: молодые гнутся сильнее
    public static readonly float[] TreeChance = { 0.30f, 0.45f, 0.25f };   // доли размеров в лесу (сумма 1)
    public const uint TreeSizeSeed = 20260u;                               // меняешь число: лес получает другую раскладку размеров, при том же числе она одна и та же каждый запуск
    public const bool GeneratedTreeModels = true;                          // true: деревья и пни рисуются из TreeMeshFactory (3 вида). false: остаётся префаб из сцены

    // отрост (TreeGrowthVisual): падение, пень, росток -> молодое -> зрелое
    public const float GrowFallSeconds = 0.9f;     // сколько дерево падает и проседает в пень
    public const float GrowSproutScale = 0.24f;    // масштаб ростка в начале отроста (доля от зрелого). Было 0.10: росток еле виднелся
    public const float GrowPopSeconds = 0.45f;     // росток «выскакивает» из земли с перелётом (пружинка)
    public const bool StumpSinks = true;           // true: пень свежий пару секунд, потом уходит в землю, и росток стоит в земле. false: пень стоит до конца роста (как раньше)
    public const float StumpHoldSeconds = 1.4f;    // сколько пень стоит ПОСЛЕ падения дерева (0 = уходит сразу)
    public const float StumpSinkSeconds = 0.9f;    // сколько пень уходит в землю
    public const float GrowStumpLift = 0.35f;      // насколько росток поднят над землёй, чтобы расти из среза пня (м при масштабе 1)
    public const int TreesOnIsland = 20;

    // появление деревьев (ResourceItem.BeginWave / PlaySpring, TreeFx)
    public const bool TreeWaveOnStart = true;       // при старте и загрузке деревья вырастают волной от игрока
    public const float TreeWaveSpeed = 14f;         // м/с: с какой скоростью волна бежит от игрока
    public const float TreeWaveMaxDelay = 2.5f;     // потолок задержки, с (дальние деревья не ждут вечно)
    public const float TreeWaveGrowSeconds = 0.8f;  // сколько растёт одно дерево: 0 → 1.06 → 1
    public const float TreeWaveOvershoot = 1.06f;   // перелёт масштаба в волне
    public const float TreeSpringSeconds = 0.45f;   // пружинка в конце респауна
    public const float TreeSpringAmount = 0.08f;    // сила пружинки (доля масштаба)
    public const float TreeFxMaxDist = 22f;         // дальше этого расстояния от игрока облачко и звук респауна не рисуются
    public const int TreePuffPool = 6;              // сколько систем частиц в пуле облачков
    public const bool TreeJitter = true;            // лёгкий наклон и сдвиг каждого дерева в рантайме (позиции в сцене не меняются)
    public const float TreeTiltDeg = 2.5f;          // наклон до ± столько градусов (старые наклонены меньше)
    public const float TreeShiftMax = 0.25f;        // сдвиг по земле до столько метров

    // ---------- ритм-рубка (PlayerInteraction, TreeHits, PlayerHeadHud) ----------
    // E входит в рубку, дальше каждое нажатие E = удар. Маркер ходит по шкале туда-сюда, попал в зону = сильный удар.
    // Промах не наказывается (столп «Уют без наказаний»): прогресс всё равно растёт, только медленно.
    // Спам не работает: между ударами пауза RhythmCooldown, а слабый удар даёт всего 4% дерева.
    // Расчёт (на бумаге, уточнить плейтестом), сила топора 0:
    //   идеальные удары: 0.25 + 0.25 + 0.34 + 0.34 = 4 удара, ~2.8 с (раньше 3.0 с);
    //   зелёные удары: 5-6 ударов, ~3.5-4 с; спам кнопки: ~11 с.
    // Сила топора (PlayerUpgrades.ChopTime) и таланты ускоряют рубку так же, как раньше: множитель = ChopTime / время с прокачкой.
    // Режим «зажми E» остаётся для доступности: PlayerInteraction.SetRhythmChop(false).
    public const bool RhythmChopDefault = false;      // по умолчанию «зажми E» + бонус на Пробел (ниже). Ритм-режим остался, включается SetRhythmChop(true)
    public const float RhythmSweepTime = 0.7f;        // с на проход маркера от края до края (центр пересекается каждые 0.7 с)
    public const float RhythmGoodHalf = 0.25f;        // половина ширины зелёной зоны (доля шкалы 0..0.5)
    public const float RhythmPerfectHalf = 0.10f;     // половина ширины золотой зоны
    public const float RhythmCooldown = 0.45f;        // пауза между ударами
    public const float RhythmPerfect = 0.25f;         // доля дерева за идеальный удар
    public const float RhythmGood = 0.17f;            // за хороший
    public const float RhythmWeak = 0.04f;            // за слабый (промах)
    public const int RhythmComboNeed = 3;             // удачных ударов подряд для бонуса
    public const float RhythmComboMul = 1.35f;        // множитель удара в серии
    public const int RhythmCleanCutLogs = 1;          // «Чистый спил»: добивающий идеальный удар в серии даёт +1 бревно

    // ---------- бонус рубки: зажми E + Пробел в зелёной зоне (PlayerInteraction.TickChop, PlayerHeadHud) ----------
    // Пока держишь E, над таймером бегает маркер по шкале. Нажал Пробел, когда маркер в зелёной зоне: время рубки
    // сокращается на ChopBonusFraction от полного времени (сила топора учитывается сама). Промах: ничего не происходит,
    // таймер идёт как шёл (столп «Уют без наказаний»). Без бонуса дерево рубится ровно за ChopTime, бонус только ускоряет.
    // Расчёт (на бумаге, сила топора 0): один проход маркера 0.9 с, зона 30% шкалы = 0.27 с на проход,
    //   за 3 с реально попасть 2-3 раза: -0.3 с за попадание, итого ~2.1-2.4 с вместо 3.0 с (до -30%).
    //   Спам Пробела не помогает: пауза ChopBonusCooldown после любого нажатия, зона занимает только 30% шкалы.
    public const bool ChopBonusDefault = true;        // включён по умолчанию, в настройках доступности можно выключить
    public const float ChopBonusSweepTime = 0.9f;     // с на проход маркера от края до края
    public const float ChopBonusZoneHalf = 0.15f;     // половина ширины зелёной зоны (доля шкалы): зона = 30% шкалы
    public const float ChopBonusCooldown = 0.35f;     // пауза между нажатиями Пробела
    public const float ChopBonusFraction = 0.10f;     // сколько от полного времени рубки срезает попадание (0.10 = 10%)
    // Обучение бонусу: подсказка «ПРОБЕЛ в зелёной зоне» висит над деревом, в цели и на шкале, пока игрок не попал в зону
    // ChopBonusTeachHits раз (счётчик хранится в PlayerPrefs). На телефоне подсказка молчит: там пока нет кнопки Пробела.
    public const int ChopBonusTeachHits = 3;

    // ---------- прокачка (PlayerUpgrades) ----------
    // порядок = enum UpgradeType: ChopPower, MoveSpeed, Capacity, DoubleYield, Haggle, SawSpeed
    public static readonly int[] UpgradeMaxLevel = { 6, 6, 6, 6, 5, 5 };
    public static readonly int[] UpgradeBaseCost = { 25, 20, 30, 40, 60, 50 };
    public const float UpgradeCostGrowth = 1.5f;
    public const int SawUnlockLevel = 3;                 // «Скорость пилы»: пила появляется в хижине на 2-м уровне, апгрейд с 3-го
    public static readonly int[] FreePointLevels = { 3, 5, 7, 9 };

    // цена повышения с уровня L на L+1: база × 1.5^L
    public static int UpgradeCost(int type, int level)
    {
        return Mathf.RoundToInt(UpgradeBaseCost[type] * Mathf.Pow(UpgradeCostGrowth, level));
    }

    public const float ChopTimeFactor = 0.9f;            // время рубки × 0.9^L
    public const float SpeedPerLevel = 0.06f;            // скорость × (1 + 0.06·L)
    public const int CapacityPerLevel = 2;               // вместимость 4 + 2·L
    public const float DoublePerLevel = 0.05f;           // шанс двойной добычи 5% × L
    public const float HagglePerLevel = 0.05f;           // цена продажи × (1 + 0.05·L)
    public const float SawTimeFactor = 0.88f;            // время пилы × 0.88^L

    // ---------- торговля ----------
    public const int LogPrice = 2;
    public const int PlankPrice = 6;

    // ---------- пилорама (доски), GDD v0.4 ----------
    // Рецепт: LogsPerPlank бревна -> 1 доска. Потолок дизайна: доска не дороже MaxLogsPerPlank бревен (Setup Doctor следит).
    // Почему 2: бревно 2 монеты, доска 6 монет, значит доска даёт +2 монеты к цене сырья (4) и стоит делать.
    //   При 3 бревнах доска продавалась бы в ноль, и пила нужна была бы только для стройки.
    // Время: 6 с на доску (было 4). При 4 с пила успевала за рубкой и прокачка «Скорость пилы» ничего не давала.
    //   Партия 5 досок = 30 с (рубка 10 бревен ~ 30-35 с), с прокачкой 5 ур. 3.2 с на доску = 16 с на партию.
    // Очередь: не больше PlankQueueCap досок в работе (10 бревен за заход), склад готовых PlankOutputCap.
    public const int LogsPerPlank = 2;
    public const int MaxLogsPerPlank = 6;
    public const float PlankTime = 6f;
    public const int PlankQueueCap = 5;
    public const int PlankOutputCap = 10;

    // рецепт с защитой от опечатки: от 1 до MaxLogsPerPlank
    public static int PlankRecipe { get { return Mathf.Clamp(LogsPerPlank, 1, MaxLogsPerPlank); } }

    // старые имена (мастерская палатки): оставлены, чтобы ничего не сломалось
    public const int TentInputCap = PlankQueueCap * LogsPerPlank;
    public const int TentOutputCap = PlankOutputCap;

    // ---------- рабочие ----------
    public const int MaxWorkers = 6;
    public const float WorkerIncome = 0.4f;              // монет/с на рабочего
    public const int WorkerBaseCost = 60;
    public const float WorkerCostGrowth = 1.4f;
    public const int XpPerWorker = 10;
    public static int WorkerCost(int n) { return Mathf.RoundToInt(WorkerBaseCost * Mathf.Pow(WorkerCostGrowth, n - 1)); }   // n = 1..6

    // ---------- питомцы (GDD v2.1, глава «Питомцы») ----------
    // Клети дают питомцев за монеты. Питомец усиливает одну характеристику (PetStat) на долю от PetBonusByRarity.
    // Порядок массивов: по CrateType (Forest, River, Sky) и по PetRarity (Common, Uncommon, Rare, Legendary).
    public static readonly int[] PetCratePrice = { 120, 400, 1200 };      // монет за клеть
    public static readonly int[] PetCrateLevel = { 4, 5, 7 };             // уровень игрока для открытия (Лесная: с главы pets; Небесная: после моста или с 7-го)

    // шансы в процентах, сумма каждой строки = 100 (Setup Doctor и «Проверить шансы» следят)
    public static readonly float[][] PetCrateOdds =
    {
        new float[] { 70f, 25f,  5f,  0f },   // Лесная
        new float[] { 45f, 35f, 17f,  3f },   // Речная
        new float[] {  0f, 40f, 45f, 15f },   // Небесная
    };

    // Гарантия: если подряд PetGuaranteeEvery клетей одного типа без Редкого, эта клеть даёт Редкого или выше.
    // ВНИМАНИЕ: гарантия поднимает реальную долю Редких и выше (Лесная ~12%, Речная ~22%, Небесная ~60%), см. «Проверить шансы».
    public const int PetGuaranteeEvery = 10;

    public static readonly float[] PetBonusByRarity = { 0.03f, 0.05f, 0.08f, 0.12f };   // бонус питомца (доля, 0.05 = +5%)
    public const float PetBonusCap = 0.25f;                                              // потолок суммы бонусов на одну характеристику
    public const int PetCapacityBase = 16;                                               // бонус рюкзака питомцев: доля × 16 (макс. рюкзак), округление вверх, минимум +1 бревно

    // Слияние и звёзды: PetMergeCount одинаковых питомцев сливаются в одного, у него +1 звезда (максимум PetStarMax).
    // Звезда умножает бонус питомца на (1 + PetStarBonus × звёзд). Сумма по характеристике всё равно режется потолком PetBonusCap.
    public const int PetMergeCount = 3;
    public const int PetStarMax = 3;
    public const float PetStarBonus = 0.15f;

    // «Книга питомцев»: когда открыты ВСЕ виды редкости, к каждой процентной характеристике добавляется эта доля
    // (сумма с бонусами питомцев режется PetBonusCap). Порядок: Common, Uncommon, Rare, Legendary.
    public static readonly float[] PetBookBonusByRarity = { 0.01f, 0.015f, 0.02f, 0.03f };

    public static readonly int[] PetSlotLevels = { 1, 5, 7 };      // уровень игрока для 1-го, 2-го, 3-го слота
    public static readonly int[] PetGrowthLevels = { 5, 8 };       // уровни, на которых питомец вырастает: малыш (0) -> подросток (1) -> взрослый (2)

    public static float PetBonus(PetRarity rarity) { return PetBonusByRarity[(int)rarity]; }
    public static float PetStarMult(int stars) { return 1f + PetStarBonus * Mathf.Clamp(stars, 0, PetStarMax); }
    public static float PetBonus(PetRarity rarity, int stars) { return PetBonusByRarity[(int)rarity] * PetStarMult(stars); }

    // сколько слотов экипировки у игрока этого уровня (1..3)
    public static int PetSlotsAt(int level)
    {
        int n = 0;
        for (int i = 0; i < PetSlotLevels.Length; i++) if (level >= PetSlotLevels[i]) n++;
        return n;
    }

    // стадия роста питомцев для уровня игрока: 0 малыш, 1 подросток, 2 взрослый
    public static int PetGrowthStage(int level)
    {
        int s = 0;
        for (int i = 0; i < PetGrowthLevels.Length; i++) if (level >= PetGrowthLevels[i]) s++;
        return s;
    }

    // ---------- сюжетная цепочка (StoryFlow, GDD v0.3) ----------
    // Здания открывает задание NPC, а не уровень. Уровень только условие начала главы.
    // Расчёт XP (на бумаге, уточнить плейтестом): костёр 4 дерева (12) + 20 + 25 = 57 -> 2-й уровень (нужно 50);
    // хижина 10 деревьев (30) + 45, доски 6 шт (30), рынок 20 + 25 -> 3-й уровень (нужно 150) приходит на рынке.
    public const int CampfireSteps = 2;                                  // стадии костра: 1 (исходная, без цены) + 2 и 3 = два этапа стройки
    public static readonly int[] CampfireStepXp = { 20, 25 };

    // Глава «Прокачка» (Мира): на 2-м уровне открывается окно, первым покупается «Вместимость рюкзака» (4 -> 6).
    // Монет за 2-й уровень 15*2 = 30, а рюкзак стоит 30 * 1.5^0 = 30: хватает впритык, без копилки.
    public const int UpgradeLevel = 2;

    // Глава «Припасы» (Мира): после костра просит 8 брёвен, по 4 за рейс (рюкзак на 4). Опыт за бревно подобран так,
    // чтобы к последнему рейсу игрок дошёл до 2-го уровня: опыт за бревно = недостающее до уровня / 8, но не меньше SupplyMinXp.
    public const int SupplyLogs = 8;
    public const int SupplyMinXp = 2;

    public const int HutLevel = 2;                                       // глава «Хижина» (Оскар)
    public static readonly int[] HutWood = { 10, 10 };                   // 20 бревен на 2 этапа
    public static readonly float[] HutTime = { 3f, 3f };
    public static readonly int[] HutXp = { 20, 25 };

    public const int MarketPlanks = 6;                                   // доски на прилавок (делаются на пиле хижины)
    public static readonly int[] MarketWood = { 6, 0 };
    public static readonly int[] MarketPlankCost = { 0, MarketPlanks };
    public static readonly float[] MarketTime = { 3f, 3f };
    public static readonly int[] MarketXp = { 20, 25 };

    public const int TentLevel = 3;                                      // глава «Палатка» (Оскар): лагерь для рабочих
    public const int TentWood = 10;
    public const int TentPlanks = 8;
    public const int TentCoins = 60;
    public const float TentBuildTime = 4f;
    public const int TentXp = 40;

    // Глава «Мост» (Оскар): подвесной мост через реку, три этапа подряд (GDD). Этап 1 открывается с BridgeLevel.
    // Итого: 40 брёвен, 70 досок, 800 монет. Брёвна и доски сдаются частями (рюкзак вмещает 4-16), монеты списываются
    // одним платежом, только когда брёвна и доски этапа уже сданы (BuildingController.DepositTick).
    // ВНИМАНИЕ: BridgeTime и BridgeXp в GDD не заданы, значения ниже предложены при аудите и ждут утверждения.
    public const int BridgeLevel = 5;
    public static readonly string[] BridgeNames = { "Опоры", "Настил", "Тросы и перила" };
    public static readonly int[] BridgeWood = { 40, 0, 0 };
    public static readonly int[] BridgePlanks = { 0, 30, 40 };
    public static readonly int[] BridgeCoins = { 150, 250, 400 };
    public static readonly float[] BridgeTime = { 5f, 5f, 6f };
    public static readonly int[] BridgeXp = { 40, 40, 60 };

    // ---------- показ готовой стадии здания (BuildingController, BuildReveal, BuildFx, CameraVibe) ----------
    // После КАЖДОГО этапа стройки окно закрывается, площадка блокируется на RevealTime, камера показывает здание.
    // Так игрок не проскакивает визуальные стадии, даже если ресурсов хватает на все этапы подряд.
    // Пропуск (E, Пробел, Esc) принимается не раньше RevealSkipGrace: случайное нажатие не съест показ.
    public const float RevealTime = 3f;            // с, обычная стадия (площадка заблокирована всё это время)
    public const float RevealTimeFinal = 4.5f;     // с, последняя стадия: дольше и больше эффектов
    public const float RevealZoom = 0.8f;          // CameraFollow.zoomBias во время показа (< 1 = ближе)
    public const float RevealOrbitDegrees = 25f;   // на сколько градусов камера обходит здание за весь показ
    public const float RevealCamIn = 0.9f;         // с, плавный наезд в начале
    public const float RevealCamOut = 0.6f;        // с, плавный возврат в конце
    public const float RevealSkipGrace = 0.6f;     // с от начала, раньше пропуск не принимается
    public const float RevealSkipTail = RevealCamOut;   // сколько ещё идёт возврат камеры после пропуска (равно RevealCamOut: без скачка)
    public const float RevealHintSeconds = 6f;     // сколько висит «Следующая стадия готова к постройке»
    public const float RevealDustInterval = 0.5f;  // с между облачками пыли на площадке во время показа
    public const float RevealFinalBurstInterval = 0.4f;   // с между дополнительными вспышками на последней стадии (после того, как здание собрано)
    // сборка стадии по деталям (BuildFx): детали прилетают сверху по очереди, снизу вверх, и встают на место
    public const float RevealAssembleShare = 0.6f;        // какая доля времени показа уходит на сборку (остальное: любуемся готовым)
    public const float RevealAssembleDelay = 0.25f;       // с до начала сборки (камера уже поехала)
    public const float RevealAssemblePieceTime = 0.5f;    // с на одну деталь: полёт, отскок, посадка
    public const float RevealAssembleDrop = 2.5f;         // с какой высоты летит деталь, метры
    public const float RevealAssembleDropFinal = 3.5f;    // то же на последней стадии
    public const float RevealAssembleTilt = 14f;          // начальный наклон детали, градусы
    public const float RevealLandFxInterval = 0.1f;       // не чаще, чем раз в столько секунд: стук, искры, пыль при посадке деталей
    public const int RevealFinalBursts = 3;        // сколько дополнительных вспышек после основной на последней стадии

    // ---------- опыт и уровни ----------
    public const int MaxLevel = 10;
    public const int XpPerTree = 3;
    public const int XpPerBuilding = 40;      // костёр, хижина, мастерская
    public const int XpPerPlank = 5;

    // опыт до следующего уровня с уровня L: 50 * L (суммарно до 10-го уровня 2250)
    public static int XpToNext(int level) { return 50 * level; }

    // накопленный опыт, с которым достигается уровень n (n=2 -> 50, n=3 -> 150, ... n=10 -> 2250)
    public static int XpForLevel(int n) { return n <= 1 ? 0 : 25 * n * (n - 1); }

    // монеты за достижение уровня n: 15 * n
    public static int CoinsForLevel(int n) { return n <= 1 ? 0 : 15 * n; }

    // ---------- дерево навыков (Skills/SkillTree, PlayerSkills) ----------
    // Очки навыков: по одному за каждый уровень со 2-го, максимум SkillMaxPoints (хватает на 9 из 18 очков трёх веток, приходится выбирать).
    public const int SkillMaxPoints = 9;
    public static readonly int[] SkillTierCost = { 1, 2, 3 };            // цена узла по ярусу (1..3)
    public static readonly int[] SkillTierLevel = { 2, 4, 7 };           // уровень игрока для яруса (1..3)
    public const int SkillResetCoins = 100;                              // сброс навыков: первый бесплатно, дальше столько монет

    // значения эффектов (читают задачи 3 и 4; процент = доля, 0.08 = +8%)
    public const float SkillChainBonus = 0.08f;          // lumber_chain: +8% скорости рубки за дерево подряд
    public const float SkillChainMax = 0.24f;            // потолок серии
    public const float SkillChainPause = 4f;             // с, пауза между деревьями, после которой серия рвётся
    public const int SkillSturdyWood = 3;                // lumber_sturdy: бревен с дерева
    public const float SkillSturdyGrowth = 1.2f;         // ... дерево растёт в 1.2 раза дольше
    public const float SkillFastGrowth = 0.6f;           // lumber_fastgrow: время роста × 0.6 (быстрее на 40%)
    public const float SkillEchoChance = 0.2f;           // lumber_echo: шанс падения соседнего дерева
    public const int SkillEchoBonusWood = 1;             // ... столько бревен даёт упавший сосед (не умножается «Двойной добычей»)
    public const float SkillEchoRadius = 6f;             // «соседнее» дерево: ближайшее спелое в этом радиусе, м
    public const float SkillGoldenInterval = 90f;        // lumber_golden: раз в столько секунд золотое дерево
    public const int SkillGoldenWood = 6;
    public const float SkillGoldenMinDist = 5f;          // золотым становится спелое дерево не ближе и не дальше этих расстояний от игрока, м
    public const float SkillGoldenMaxDist = 30f;         // (если таких нет, берётся любое спелое)
    public const float SkillCarrySlowdown = 0.15f;       // carry_light: замедление от груза 15% вместо FullBackpackSlowdown
    public const float SkillDashMul = 2f;                // carry_dash: скорость ×2
    public const float SkillDashTime = 0.4f;
    public const float SkillDashCooldown = 8f;
    public const int SkillBasketBonus = 4;               // carry_basket: рюкзак +4
    public const float SkillLegsBonus = 0.12f;           // carry_legs: +12% скорости бега
    public const float SkillBazaarCooldown = 300f;       // boss_tongue: «Базарный день» раз в 5 минут
    public const float SkillBazaarMul = 1.5f;            // следующая продажа ×1.5
    public const int SkillHoardQueue = 2;                // boss_hoard: очередь пилы +2
    public const int SkillHoardOutput = 4;               // ... склад досок +4
    public const float SkillChiefBonus = 0.2f;           // boss_chief: рабочие приносят на 20% больше
    public const int SkillBulkMinItems = 10;             // boss_bulk: от 10 предметов за раз
    public const float SkillBulkBonus = 0.1f;            // ... цена +10%
    public const float SkillSawyerChance = 0.15f;        // boss_sawyer: шанс доски без трат брёвен

    // ---------- сохранения (SaveSystem, SaveAutoRunner) ----------
    public const float SaveAutoIntervalSeconds = 60f;   // автосохранение по таймеру (в паузе timeScale 0 пропускается)
    public const float SaveDebounceSeconds = 1f;        // RequestSave копит запросы и пишет файл раз в столько секунд
    public const long SaveMaxFileBytes = 16L * 1024 * 1024;   // файл крупнее считается битым
    public const float TimeOfDayDefault = 0.3f;         // время суток по умолчанию (0..1, 0.3 = утро), пока нет DayNightCycle (TimeOfDaySaveData)
    public const bool SaveAutoLoadOnStart = true;

    // ---------- смена суток (DayNightCycle, CampSleep, NightBonus) ----------
    public const float DayLengthMinutes = 11f;          // реальных минут на полные сутки (множитель «Скорость суток» в настройках)
    // границы фаз, доли суток (0 = полночь, 0.25 = раннее утро, 0.5 = полдень, 0.75 = вечер)
    public const float TimeDawnStart = 0.20f;           // рассвет: солнце у горизонта
    public const float TimeDayStart = 0.30f;            // день
    public const float TimeDuskStart = 0.70f;           // закат
    public const float TimeNightStart = 0.80f;          // ночь (до TimeDawnStart)
    // сон у костра
    public const float SleepFadeSeconds = 1.5f;         // весь сон: затемнение, перемотка, рассвет
    public const float SleepRadius = 4f;                // м от костра, на которых доступно «лечь спать»
    public const float SleepWakeTime = 0.25f;
    public const float CampTimeSpeed = 6f;              // ночью у горящего костра время идёт в столько раз быстрее (ночь ~4,4 мин -> ~45 с)
    public const float CampTimeRadiusExtra = 2f;        // ускорение работает на SleepRadius + столько метров           // во сколько просыпаемся (раннее утро)
    // серебряное дерево (ночной бонус без давления: раз за ночь, ничего не теряется)
    public const int SilverTreeLogs = 4;                // брёвен с серебряного дерева (не меньше)
    public const int SilverTreeXp = 12;                 // бонусный опыт сверх XpPerTree
    public const float SilverTreeDelay = 8f;            // секунд ночи до появления дерева
    public const float SilverTreeMinDist = 6f;          // выбирается спелое дерево в кольце от игрока, м
    public const float SilverTreeMaxDist = 22f;       // читать сохранение при старте игры; меню «Продолжить» (часть D) может выключить
}
