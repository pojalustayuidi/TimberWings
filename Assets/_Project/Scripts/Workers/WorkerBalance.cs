// Assets/_Project/Scripts/Workers/WorkerBalance.cs (НЕ в Editor)
// ВСЕ числа системы рабочих (дизайн «Система рабочих»). Своих чисел нет ни в CampSimulation, ни в WorkerManager, ни в UI:
// править баланс только здесь. Без сцены и без MonoBehaviour, поэтому проверяется unit-тестами Edit Mode.
using System;
using UnityEngine;

// Тип пака. Порядок = индексы в PackPrice / PackUnlockLevel / PackOdds.
public enum WorkerPackType { Artel = 0, Workshop = 1, Guild = 2 }

public static class WorkerBalance
{
    // ====================================================== лагерь
    public const int CampSlots = 6;                       // слотов в лагере
    public const int MaxPerRole = 3;                      // не больше 3 рабочих одной роли
    public const bool PerksStackAcrossRole = true;        // перки одного типа складываются между рабочими роли (с потолками)

    public const int BaseLogStock = 30;                   // склад лагеря: бревна
    public const int BasePlankStock = 10;                 // склад лагеря: доски
    public const float SellRate = 0.8f;                   // «Продать на месте»: потеря 20%

    // ====================================================== базы выработки (1 уровень, Обычный)
    public const double ClerkBaseIncome = 0.45;           // монет/с
    public const double LumberjackBaseRate = 0.20;        // бревен/с
    public const double SawyerBaseTime = 10.0;            // секунд на доску
    public const int SawyerLogsPerPlank = 2;              // бревен на доску
    public const double ClerkTickSeconds = 1.0;           // Приказчик платит тиками раз в секунду

    // ====================================================== редкости (индекс = (int)WorkerRarity)
    public static readonly float[] RarityMult = { 1.0f, 1.2f, 1.5f, 2.0f, 2.8f };   // множитель выработки
    public static readonly int[] RarityPerks = { 0, 1, 2, 3, 4 };                    // сколько перков

    // Пул персонажей (проверяет WorkerDatabaseTools): 27 = 3 роли × 9, на роль 3 обычных, 2 необычных, 2 редких, 1 легендарный, 1 мифический
    public const int PoolPerRole = 9;
    public const int PoolTotal = 27;
    public static readonly int[] PoolPerRarity = { 3, 2, 2, 1, 1 };

    public static float RarityMultiplier(WorkerRarity r) { return RarityMult[Mathf.Clamp((int)r, 0, RarityMult.Length - 1)]; }
    public static int PerkCount(WorkerRarity r) { return RarityPerks[Mathf.Clamp((int)r, 0, RarityPerks.Length - 1)]; }

    // ====================================================== уровни и опыт
    public const int MaxLevel = 10;
    public const double XpPerSecond = 0.5;                // опыт только за реальную работу
    public const double XpBase = 120.0;                   // опыт до 2 уровня
    public const double XpGrowth = 1.35;                  // рост требования за уровень
    public const float LevelStep = 0.06f;                 // +6% выработки за уровень
    public const float MasterBonus = 1.1f;                // «Мастер»: ×1.1 на максимальном уровне

    // Опыт, нужный, чтобы с уровня level перейти на level+1: 120 × 1.35^(L−1) (округление до целого). На максимуме 0
    public static int XpToNext(int level)
    {
        if (level < 1) level = 1;
        if (level >= MaxLevel) return 0;
        return (int)Math.Round(XpBase * Math.Pow(XpGrowth, level - 1));
    }

    // Суммарный опыт от начала, с которого начинается уровень level (1 уровень = 0)
    public static double TotalXpForLevel(int level)
    {
        level = Mathf.Clamp(level, 1, MaxLevel);
        double sum = 0;
        for (int l = 1; l < level; l++) sum += XpToNext(l);
        return sum;
    }

    // Уровень по суммарному опыту (1..MaxLevel)
    public static int LevelFromTotalXp(double totalXp)
    {
        int level = 1;
        while (level < MaxLevel && totalXp + 1e-9 >= TotalXpForLevel(level + 1)) level++;
        return level;
    }

    // Множитель уровня: 1 + 0.06 × (L−1); на 10 уровне ещё ×1.1. Максимум ×1.694
    public static double LevelMult(int level)
    {
        level = Mathf.Clamp(level, 1, MaxLevel);
        double m = 1.0 + LevelStep * (level - 1);
        if (level >= MaxLevel) m *= MasterBonus;
        return m;
    }

    // ====================================================== выработка (без перков и внешних множителей)
    // Приказчик: монет/с
    public static double ClerkIncome(WorkerRarity r, int level)
    {
        return ClerkBaseIncome * RarityMultiplier(r) * LevelMult(level);
    }

    // Дровосек: бревен/с. speedBonus: сумма «Тяжёлой руки» уже с потолком (0.15 = +15%)
    public static double LumberjackRate(WorkerRarity r, int level, float speedBonus)
    {
        return LumberjackBaseRate * RarityMultiplier(r) * LevelMult(level) * (1.0 + speedBonus);
    }

    // Пильщик: секунд на доску. timeCut: сумма «Быстрой пилы» уже с потолком, отрицательная (−0.15 = на 15% быстрее)
    public static double SawyerTime(WorkerRarity r, int level, float timeCut)
    {
        double t = SawyerBaseTime / (RarityMultiplier(r) * LevelMult(level)) * (1.0 + timeCut);
        return Math.Max(0.1, t);
    }

    // ====================================================== перки
    public const float GoodDealIncome = 0.15f;            // Приказчик: +15% дохода
    public const float LuckyDealChance = 0.08f;           // 8% шанс, что тик даёт ×3
    public const int LuckyDealMult = 3;
    public const double BonusInterval = 90.0;             // «Премия»: раз в 90 с
    public const int BonusCoins = 40;                     //            +40 монет
    public const float MentorIncome = 0.05f;              // «Наставник»: +5% выработки всем рабочим
    public const float MentorCap = 0.15f;                 //               максимум +15% от всех наставников

    public const float DoubleStrikeChance = 0.10f;        // Дровосек: 10% шанс ×2 бревна
    public const float HeavyHandSpeed = 0.15f;            //           +15% скорости
    public const int RoomyLogs = 20;                      //           +20 к складу бревен
    public const double GoldenTreeInterval = 120.0;       //           раз в 120 с
    public const int GoldenTreeLogs = 6;                  //           пачка из 6 бревен

    public const float DoublePlankChance = 0.12f;         // Пильщик: 12% шанс ×2 доски
    public const float ThriftyChance = 0.15f;             //          15% шанс, что бревна не тратятся
    public const float FastSawTime = -0.15f;              //          −15% времени
    public const int StockerPlanks = 10;                  //          +10 к складу досок

    // Фирменные перки мификов (на каждую роль свой)
    public const int SignatureLuckyMult = 5;                  // Брита «Золотые весы»: ×5 вместо ×3
    public const double SignatureGoldenTreeInterval = 60.0;   // Хольгер «Поющий кедр»: 60 с вместо 120
    public const float SignatureEvenCutChance = 0.05f;        // Ингрид «Ровный распил»: 5% шанс отборной доски
    public const int SignatureEvenCutMult = 3;                //                         ×3 досок

    // Потолки: перки одного типа складываются, но не выше
    public const float ChanceCap = 0.50f;                 // шансы не больше 50%
    public const float SpeedCap = 0.40f;                  // ускорение не больше 40% (для времени это −40%)

    public static float CapChance(float v) { return Mathf.Clamp(v, 0f, ChanceCap); }
    public static float CapSpeed(float v) { return Mathf.Clamp(v, -SpeedCap, SpeedCap); }
    public static float CapMentor(float v) { return Mathf.Clamp(v, 0f, MentorCap); }

    // ====================================================== паки (индекс = (int)WorkerPackType)
    public static readonly int[] PackPrice = { 100, 450, 1400 };
    public static readonly int[] PackUnlockLevel = { 3, 5, 7 };          // уровень игрока (Артельный: постройка палатки)

    // Шансы в процентах по редкостям (Обычный, Необычный, Редкий, Легендарный, Мифический). Сумма каждой строки = 100
    public static readonly float[][] PackOdds =
    {
        new float[] { 70f, 24f,  5.5f,  0.5f, 0f   },   // Артельный
        new float[] { 38f, 38f, 20f,    4f,   0f   },   // Мастерской
        new float[] {  0f, 45f, 38f,   16.5f, 0.5f },   // Гильдейский
    };

    // Гарантии считаются отдельно для каждого типа пака
    public const int GuaranteeRareEvery = 10;             // Редкий или выше: каждые 10 паков (все типы)
    public const int GuaranteeLegendaryEvery = 40;        // Легендарный или выше: каждые 40 (Мастерской и Гильдейский)
    public const int GuaranteeMythicEvery = 100;          // Мифический: каждые 100 Гильдейских

    public static bool HasLegendaryGuarantee(WorkerPackType t) { return t == WorkerPackType.Workshop || t == WorkerPackType.Guild; }
    public static bool HasMythicGuarantee(WorkerPackType t) { return t == WorkerPackType.Guild; }

    public const bool FirstPackFree = true;               // подарок за постройку палатки (гарантирует первого рабочего)

    // Дубликат: опыт существующему рабочему по редкости; если он на максимуме, вместо опыта монеты
    public static readonly int[] DuplicateXp = { 150, 250, 400, 700, 1500 };
    public static readonly int[] DuplicateCoins = { 50, 80, 150, 300, 600 };

    public static int PackCost(WorkerPackType t) { return PackPrice[Mathf.Clamp((int)t, 0, PackPrice.Length - 1)]; }
    public static int PackLevel(WorkerPackType t) { return PackUnlockLevel[Mathf.Clamp((int)t, 0, PackUnlockLevel.Length - 1)]; }
    public static float[] OddsOf(WorkerPackType t) { return PackOdds[Mathf.Clamp((int)t, 0, PackOdds.Length - 1)]; }
    public static int DuplicateXpFor(WorkerRarity r) { return DuplicateXp[Mathf.Clamp((int)r, 0, DuplicateXp.Length - 1)]; }
    public static int DuplicateCoinsFor(WorkerRarity r) { return DuplicateCoins[Mathf.Clamp((int)r, 0, DuplicateCoins.Length - 1)]; }

    // Сумма шансов пака (должна быть 100): проверяют unit-тест и Setup Doctor
    public static float OddsSum(WorkerPackType t)
    {
        var o = OddsOf(t);
        float s = 0f;
        for (int i = 0; i < o.Length; i++) s += o[i];
        return s;
    }
}
