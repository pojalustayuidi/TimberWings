using UnityEngine;

internal enum AxeId { Old, Iron, Steel, Gunnar }

internal sealed class AxeDefinition
{
    internal readonly AxeId Id;
    internal readonly string Name, Description, Source;
    internal readonly float Power, Interval;
    internal readonly int Price;
    GameObject model;

    internal AxeDefinition(AxeId id, string name, string description, string source, float power,
        float interval, int price = 0)
    {
        Id = id;
        Name = name;
        Description = description;
        Source = source;
        Power = power;
        Interval = interval;
        Price = price;
    }

    internal GameObject Model => model != null ? model : (model = Resources.Load<GameObject>("Axes/" + Id));
}

internal static class AxeCatalog
{
    internal static readonly AxeDefinition[] All =
    {
        new AxeDefinition(AxeId.Old, "Старый топор", "Потёртая сталь, ясеневая рукоять и знакомое клеймо TW.",
            "Первый топор Лео", 1f, .56f),
        new AxeDefinition(AxeId.Iron, "Железный топор", "Тяжёлая кованая головка и оливковая обмотка.",
            "Продаётся у Рико после открытия прилавка", 1.27f, .56f, 90),
        new AxeDefinition(AxeId.Steel, "Стальной топор", "Закалённое полотно, светлая фаска и тёмная рукоять.",
            "Изготавливается в восстановленной кузнице", 1.50f, .52f, 240),
        new AxeDefinition(AxeId.Gunnar, "Топор Гуннара", "Зелёная сталь, бронзовая оправа и знак моста с двумя башнями.",
            "Восстанавливается по заданию Оскара", 1.70f, .50f)
    };

    internal static int EnchantmentCoinCost(int baseCoins) => TalentEffects.EnchantCoins(baseCoins);

    internal static bool IsValid(AxeId id) => (uint)id < (uint)All.Length;
    internal static AxeDefinition Get(AxeId id) => All[(int)id];
}
