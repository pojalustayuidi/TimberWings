// Assets/_Project/Scripts/Core/Pricing.cs (НЕ в Editor)
// Единое место, где считается цена продажи: базовая цена из GameBalance × «Торг» из прокачки (PlayerUpgrades.SellMultiplier).
// Окно продажи (SellWindow) берёт цены только отсюда: что показано в «Итого», то и заплатят.
// Новый товар (рыба, брус): добавить значение в SellItem и ветку в BasePrice.
using UnityEngine;

public enum SellItem { Log = 0, Plank = 1 }

public static class Pricing
{
    // базовая цена за штуку, без «Торга»
    public static int BasePrice(SellItem item)
    {
        switch (item)
        {
            case SellItem.Log: return GameBalance.LogPrice;
            case SellItem.Plank: return GameBalance.PlankPrice;
            default: return 0;
        }
    }

    // множитель «Торга» (1 = без прокачки)
    public static float Multiplier()
    {
        var up = PlayerUpgrades.Instance;
        return up != null ? up.SellMultiplier : 1f;
    }

    // цена одной штуки с учётом «Торга» (может быть дробной, в окне показывается как есть)
    public static float UnitPrice(SellItem item)
    {
        return BasePrice(item) * Multiplier();
    }

    // ---------- бонусы всей продажи (ветка «Хозяин»), считаются от итога окна ----------
    // «Оптовик» (boss_bulk): от SkillBulkMinItems предметов за раз +10%. «Базарный день» (boss_tongue): пока готов, ×1.5.
    // Бонусы перемножаются. Предметы считаются суммой по всем строкам (бревна и доски вместе).
    public static bool BulkApplies(int items)
    {
        return items >= GameBalance.SkillBulkMinItems && PlayerSkills.Has(SkillIds.BossBulk);
    }

    public static float SaleMultiplier(int items)
    {
        float m = 1f;
        if (BulkApplies(items)) m *= 1f + GameBalance.SkillBulkBonus;
        if (HostEffects.BazaarReady) m *= GameBalance.SkillBazaarMul;
        return m;
    }

    // итог продажи: сумма по строкам (уже с «Торгом») × бонусы продажи
    public static int ApplySale(int baseTotal, int items)
    {
        if (baseTotal <= 0) return 0;
        return Mathf.RoundToInt(baseTotal * SaleMultiplier(items));
    }

    // пометка для окна: какие бонусы сработали (пусто, если никаких)
    public static string SaleNote(int items)
    {
        string s = "";
        if (BulkApplies(items)) s += "Оптом +" + Mathf.RoundToInt(GameBalance.SkillBulkBonus * 100f) + "%";
        if (HostEffects.BazaarReady)
        {
            if (s.Length > 0) s += ", ";
            s += "Базарный день ×" + GameBalance.SkillBazaarMul.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }
        return s;
    }

    // сколько монет дадут за n штук. Округление по каждой позиции, итог окна = сумма позиций
    public static int Price(SellItem item, int n)
    {
        if (n <= 0) return 0;
        return Mathf.RoundToInt(n * BasePrice(item) * Multiplier());
    }
}
