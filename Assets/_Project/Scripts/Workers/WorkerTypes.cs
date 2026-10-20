// Assets/_Project/Scripts/Workers/WorkerTypes.cs (НЕ в Editor)
// Общие перечисления рабочих (дизайн «Система рабочих», задача 1) и русские названия для UI.
// ВАЖНО: порядок значений WorkerRole и WorkerRarity менять нельзя: по ним индексируются массивы в WorkerBalance
// и числа в сохранениях/ассетах. Новые значения добавлять только в конец.
using System.Collections.Generic;

// Роль. Порядок = индекс (WorkerManager.DebugGrantTeam перебирает 0..2).
public enum WorkerRole { Clerk = 0, Lumberjack = 1, Sawyer = 2 }

// Редкость. Чем больше число, тем реже. Индексы массивов WorkerBalance.RarityMult / RarityPerks / шансов паков.
public enum WorkerRarity { Common = 0, Uncommon = 1, Rare = 2, Legendary = 3, Mythic = 4 }

// Перки: по 4 на роль. Порядок внутри роли = порядок из дизайна (так же показываем в карточке).
public enum WorkerPerk
{
    // Приказчик
    GoodDeal = 0,       // «Выгодная сделка»: +15% дохода
    LuckyDeal = 1,      // «Счастливая сделка»: 8% шанс, что тик даёт ×3
    Bonus = 2,          // «Премия»: раз в 90 с +40 монет
    Mentor = 3,         // «Наставник»: +5% выработки всем, не больше +15%
    // Дровосек
    DoubleStrike = 4,   // «Двойной удар»: 10% шанс ×2 бревна
    HeavyHand = 5,      // «Тяжёлая рука»: +15% скорости
    Roomy = 6,          // «Вместительный»: +20 к складу бревен
    GoldenTree = 7,     // «Золотое дерево»: раз в 120 с пачка из 6 бревен
    // Пильщик
    DoublePlank = 8,    // «Двойная доска»: 12% шанс ×2 доски
    Thrifty = 9,        // «Экономный»: 15% шанс, что бревна не тратятся
    FastSaw = 10,       // «Быстрая пила»: −15% времени
    Stocker = 11        // «Запасливый»: +10 к складу досок
}

// Русские названия и описания для UI (окно «Лагерь», карточка рабочего, окно пака)
public static class WorkerText
{
    static readonly WorkerPerk[] ClerkPerks = { WorkerPerk.GoodDeal, WorkerPerk.LuckyDeal, WorkerPerk.Bonus, WorkerPerk.Mentor };
    static readonly WorkerPerk[] LumberPerks = { WorkerPerk.DoubleStrike, WorkerPerk.HeavyHand, WorkerPerk.Roomy, WorkerPerk.GoldenTree };
    static readonly WorkerPerk[] SawyerPerks = { WorkerPerk.DoublePlank, WorkerPerk.Thrifty, WorkerPerk.FastSaw, WorkerPerk.Stocker };

    // Четыре перка роли (массив общий, не менять)
    public static WorkerPerk[] PerksOf(WorkerRole role)
    {
        switch (role)
        {
            case WorkerRole.Clerk: return ClerkPerks;
            case WorkerRole.Lumberjack: return LumberPerks;
            default: return SawyerPerks;
        }
    }

    public static WorkerRole RoleOf(WorkerPerk perk)
    {
        int p = (int)perk;
        return p < 4 ? WorkerRole.Clerk : p < 8 ? WorkerRole.Lumberjack : WorkerRole.Sawyer;
    }

    public static string Role(WorkerRole r)
    {
        switch (r)
        {
            case WorkerRole.Clerk: return "Приказчик";
            case WorkerRole.Lumberjack: return "Дровосек";
            default: return "Пильщик";
        }
    }

    // Что роль производит (для подписей)
    public static string Output(WorkerRole r)
    {
        switch (r)
        {
            case WorkerRole.Clerk: return "монеты";
            case WorkerRole.Lumberjack: return "бревна";
            default: return "доски";
        }
    }

    public static string Rarity(WorkerRarity r)
    {
        switch (r)
        {
            case WorkerRarity.Common: return "Обычный";
            case WorkerRarity.Uncommon: return "Необычный";
            case WorkerRarity.Rare: return "Редкий";
            case WorkerRarity.Legendary: return "Легендарный";
            default: return "Мифический";
        }
    }

    public static string Perk(WorkerPerk p)
    {
        switch (p)
        {
            case WorkerPerk.GoodDeal: return "Выгодная сделка";
            case WorkerPerk.LuckyDeal: return "Счастливая сделка";
            case WorkerPerk.Bonus: return "Премия";
            case WorkerPerk.Mentor: return "Наставник";
            case WorkerPerk.DoubleStrike: return "Двойной удар";
            case WorkerPerk.HeavyHand: return "Тяжёлая рука";
            case WorkerPerk.Roomy: return "Вместительный";
            case WorkerPerk.GoldenTree: return "Золотое дерево";
            case WorkerPerk.DoublePlank: return "Двойная доска";
            case WorkerPerk.Thrifty: return "Экономный";
            case WorkerPerk.FastSaw: return "Быстрая пила";
            default: return "Запасливый";
        }
    }

    // Описание перка с цифрами из WorkerBalance (правим баланс в одном месте, текст обновится сам)
    public static string PerkDesc(WorkerPerk p)
    {
        switch (p)
        {
            case WorkerPerk.GoodDeal: return "+" + Pct(WorkerBalance.GoodDealIncome) + " дохода";
            case WorkerPerk.LuckyDeal: return Pct(WorkerBalance.LuckyDealChance) + " шанс, что тик даёт ×" + WorkerBalance.LuckyDealMult;
            case WorkerPerk.Bonus: return "раз в " + (int)WorkerBalance.BonusInterval + " с даёт +" + WorkerBalance.BonusCoins + " монет";
            case WorkerPerk.Mentor: return "+" + Pct(WorkerBalance.MentorIncome) + " выработки всем рабочим (всего не больше +" + Pct(WorkerBalance.MentorCap) + ")";
            case WorkerPerk.DoubleStrike: return Pct(WorkerBalance.DoubleStrikeChance) + " шанс ×2 бревна";
            case WorkerPerk.HeavyHand: return "+" + Pct(WorkerBalance.HeavyHandSpeed) + " скорости";
            case WorkerPerk.Roomy: return "+" + WorkerBalance.RoomyLogs + " к складу бревен";
            case WorkerPerk.GoldenTree: return "раз в " + (int)WorkerBalance.GoldenTreeInterval + " с пачка из " + WorkerBalance.GoldenTreeLogs + " бревен";
            case WorkerPerk.DoublePlank: return Pct(WorkerBalance.DoublePlankChance) + " шанс ×2 доски";
            case WorkerPerk.Thrifty: return Pct(WorkerBalance.ThriftyChance) + " шанс, что бревна не тратятся";
            case WorkerPerk.FastSaw: return "−" + Pct(-WorkerBalance.FastSawTime) + " времени распила";
            default: return "+" + WorkerBalance.StockerPlanks + " к складу досок";
        }
    }

    // Фирменный перк мифика (одна строка на роль)
    public static string Signature(WorkerRole r)
    {
        switch (r)
        {
            case WorkerRole.Clerk: return "Золотые весы: «Счастливая сделка» даёт ×" + WorkerBalance.SignatureLuckyMult + " вместо ×" + WorkerBalance.LuckyDealMult;
            case WorkerRole.Lumberjack: return "Поющий кедр: «Золотое дерево» каждые " + (int)WorkerBalance.SignatureGoldenTreeInterval + " с вместо " + (int)WorkerBalance.GoldenTreeInterval;
            default: return "Ровный распил: " + Pct(WorkerBalance.SignatureEvenCutChance) + " шанс отборной доски (×" + WorkerBalance.SignatureEvenCutMult + " досок)";
        }
    }

    public static string Pack(WorkerPackType t)
    {
        switch (t)
        {
            case WorkerPackType.Artel: return "Артельный пак";
            case WorkerPackType.Workshop: return "Мастерской пак";
            default: return "Гильдейский пак";
        }
    }

    static string Pct(float v) { return UnityEngine.Mathf.RoundToInt(v * 100f) + "%"; }
}
