// Assets/_Project/Scripts/Pets/PetTypes.cs (НЕ в Editor)
// Общие перечисления питомцев (GDD 4.10 «Питомцы»).

// Редкость. Порядок важен: чем больше число, тем реже (PetRoller и GameBalance индексируют массивы по нему).
public enum PetRarity { Common = 0, Uncommon = 1, Rare = 2, Legendary = 3 }

// Тип клети. Порядок = индексы в GameBalance.PetCratePrice / PetCrateOdds / PetCrateLevel.
public enum CrateType { Forest = 0, River = 1, Sky = 2 }

// Что усиливает питомец. Первые шесть совпадают по номерам с UpgradeType (ChopPower, MoveSpeed, Capacity,
// DoubleYield, Haggle, SawSpeed), плюс доход рабочих (WorkerManager.IncomeOf).
public enum PetStat { ChopPower = 0, MoveSpeed = 1, Capacity = 2, DoubleYield = 3, Haggle = 4, SawSpeed = 5, WorkerIncome = 6 }

// Русские названия для UI (окно клети, коллекция).
public static class PetText
{
    public static string Rarity(PetRarity r)
    {
        switch (r)
        {
            case PetRarity.Common: return "Обычный";
            case PetRarity.Uncommon: return "Необычный";
            case PetRarity.Rare: return "Редкий";
            default: return "Легендарный";
        }
    }

    public static string Crate(CrateType c)
    {
        switch (c)
        {
            case CrateType.Forest: return "Лесная клеть";
            case CrateType.River: return "Речная клеть";
            default: return "Небесная клеть";
        }
    }

    public static string Stat(PetStat s)
    {
        switch (s)
        {
            case PetStat.ChopPower: return "Скорость рубки";
            case PetStat.MoveSpeed: return "Скорость бега";
            case PetStat.Capacity: return "Вместимость рюкзака";
            case PetStat.DoubleYield: return "Двойная добыча";
            case PetStat.Haggle: return "Цена продажи";
            case PetStat.SawSpeed: return "Скорость пилы";
            default: return "Доход рабочих";
        }
    }
}
