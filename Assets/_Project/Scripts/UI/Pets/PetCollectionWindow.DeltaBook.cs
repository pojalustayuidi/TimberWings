// Assets/_Project/Scripts/UI/Pets/PetCollectionWindow.DeltaBook.cs (НЕ в Editor)
// ЧАСТЬ класса PetCollectionWindow (partial): дельта статов «что изменится, если взять питомца» и «Книга питомцев».
// Это кусок одного класса, поля и методы видны из других частей. Файл с объявлением и состоянием: PetCollectionWindow.cs.
// Остальные части: PetCollectionWindow.Actions.cs, PetCollectionWindow.Fill.cs, PetCollectionWindow.Build.cs, PetCollectionWindow.Presets.cs, PetCollectionWindow.Interact.cs, PetCollectionWindow.HudHelpers.cs.
// Методы этой части: StatValue, StatValueText, DeltaLine, F2, LiveLine, RefreshDetail, RefreshBook, Set, SetBtn.

using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UiLayout;

public partial class PetCollectionWindow
{
    // ------------------------------------------------------------ дельта: что изменится, если взять питомца
    static float StatValue(PetBonusSet b, PetStat st) { return st == PetStat.Capacity ? b.capacityLogs : b.Get(st); }

    static string StatValueText(PetStat st, PetBonusSet b)
    {
        return st == PetStat.Capacity ? "+" + b.capacityLogs : "+" + PetUi.Pct(b.Get(st)) + "%";
    }

    // «Скорость рубки +3% -> +5%»: «после» зелёное, если стало лучше, красноватое, если хуже
    static string DeltaLine(PetStat st, PetBonusSet before, PetBonusSet after)
    {
        float a = StatValue(before, st), b = StatValue(after, st);
        Color col = b > a + 1e-4f ? UiKit.Good : (b < a - 1e-4f ? UiKit.Warn : Dim);
        string unit = st == PetStat.Capacity ? " " + UiDraw.Plural(after.capacityLogs, "бревно", "бревна", "бревен") : "";
        return "<b>" + PetText.Stat(st) + "</b>  " + StatValueText(st, before) + Arrow
            + "<color=#" + PetUi.Hex(col) + ">" + StatValueText(st, after) + "</color>" + unit;
    }

    static string F2(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

    // живое значение у игрока (через PlayerUpgrades.Calc): «Рубка: 3.00 с -> 2.85 с». Пусто, если игрока ещё нет или стат не считается тут
    static string LiveLine(PetStat st, PetBonusSet before, PetBonusSet after)
    {
        var pu = PlayerUpgrades.Instance;
        if (pu == null) return "";
        TalentBonus b0 = pu.Bonus;
        TalentBonus b1 = PetBonus.Rebase(b0, before, after);

        UpgradeType ut;
        switch (st)
        {
            case PetStat.ChopPower: ut = UpgradeType.ChopPower; break;
            case PetStat.MoveSpeed: ut = UpgradeType.MoveSpeed; break;
            case PetStat.Capacity: ut = UpgradeType.Capacity; break;
            case PetStat.DoubleYield: ut = UpgradeType.DoubleYield; break;
            case PetStat.Haggle: ut = UpgradeType.Haggle; break;
            case PetStat.SawSpeed: ut = UpgradeType.SawSpeed; break;
            default: return "";                  // доход рабочих считает WorkerManager
        }
        int lvl = pu.Level(ut);
        float v0 = PlayerUpgrades.Calc(ut, lvl, b0), v1 = PlayerUpgrades.Calc(ut, lvl, b1);

        string t0, t1, name;
        switch (st)
        {
            case PetStat.ChopPower: name = "Рубка"; t0 = F2(v0) + " с"; t1 = F2(v1) + " с"; break;
            case PetStat.SawSpeed: name = "Пила"; t0 = F2(v0) + " с"; t1 = F2(v1) + " с"; break;
            case PetStat.MoveSpeed: name = "Бег"; t0 = "x" + F2(v0); t1 = "x" + F2(v1); break;
            case PetStat.Haggle: name = "Цена продажи"; t0 = "x" + F2(v0); t1 = "x" + F2(v1); break;
            case PetStat.DoubleYield: name = "Двойная добыча"; t0 = Mathf.RoundToInt(v0 * 100f) + "%"; t1 = Mathf.RoundToInt(v1 * 100f) + "%"; break;
            default:
                int c0 = Mathf.RoundToInt(v0), c1 = Mathf.RoundToInt(v1);
                name = "Рюкзак"; t0 = c0.ToString(); t1 = c1 + " " + UiDraw.Plural(c1, "бревно", "бревна", "бревен"); break;
        }
        // для времени «лучше» = меньше
        bool better = (st == PetStat.ChopPower || st == PetStat.SawSpeed) ? v1 < v0 - 1e-4f : v1 > v0 + 1e-4f;
        bool worse = (st == PetStat.ChopPower || st == PetStat.SawSpeed) ? v1 > v0 + 1e-4f : v1 < v0 - 1e-4f;
        Color col = better ? UiKit.Good : (worse ? UiKit.Warn : Dim);
        return "<color=#" + PetUi.Hex(Soft) + ">" + name + ":</color> " + t0 + Arrow + "<color=#" + PetUi.Hex(col) + ">" + t1 + "</color>";
    }

    static readonly string[] StageNames = { "малыш", "подросток", "взрослый" };

    void RefreshDetail()
    {
        var pm = PetManager.Instance;
        PetInstance sel = FindInstance(selectedUid);
        bool has = sel != null && pm != null && tab == Tab.Collection;
        RefreshPat();

        detCard.rt.gameObject.SetActive(has);
        detName.gameObject.SetActive(has);
        detRarity.gameObject.SetActive(has);
        detExtra.gameObject.SetActive(has);
        detStat.gameObject.SetActive(has);
        detHead.gameObject.SetActive(has);
        detD1.gameObject.SetActive(has);
        detD2.gameObject.SetActive(has);
        detLive.gameObject.SetActive(has);
        detNote.gameObject.SetActive(has);
        releaseBtn.gameObject.SetActive(has);
        detEmpty.gameObject.SetActive(!has);
        if (!has)
        {
            equipBtn.gameObject.SetActive(false);
            unequipBtn.gameObject.SetActive(false);
            mergeBtn.gameObject.SetActive(false);
            Set(detEmpty, sorted.Count == 0 ? "" : "Выбери питомца");
            return;
        }

        PetData d = sel.data;
        Color acc = PetUi.RarityAccent(d.rarity);
        int stage = Mathf.Clamp(pm.GrowthStage, 0, 2);
        detCard.LiveStage = stage;                       // стадия роста: малыш, подросток, взрослый
        detCard.Set(d, sel.stars, false);
        detName.color = acc;
        Set(detName, PetUi.NameOf(d));
        detRarity.color = acc;
        Set(detRarity, PetText.Rarity(d.rarity) + "  ·  " + StageNames[stage]);
        Set(detStat, PetUi.StatLine(d, sel.stars));

        // звёзды и копии
        int copies = pm.CountOf(d);
        bool canMerge = pm.CanMerge(d);
        string extra = "";
        if (sel.stars > 0)
            extra = "<color=#FFD24A>Звёзды: " + sel.stars + " из " + GameBalance.PetStarMax + "</color>  <size=80%>(бонус +"
                + Mathf.RoundToInt((GameBalance.PetStarMult(sel.stars) - 1f) * 100f) + "%)</size>";
        else if (copies > 1)
            extra = "Копий: <b>x" + copies + "</b>  <size=80%>(слияние от " + GameBalance.PetMergeCount + ")</size>";
        Set(detExtra, extra);

        bool eq = pm.IsEquipped(sel.uid);
        bool free = pm.EquippedCount < pm.SlotCount;

        // дельта относительно текущих бонусов
        PetBonusSet before = pm.Bonus, after;
        PetInstance rep = null;
        string head;
        if (eq)
        {
            after = pm.PreviewBonus(null, sel);
            head = "Если снять:";
        }
        else
        {
            rep = pm.WouldReplace(sel);
            after = pm.PreviewBonus(sel, rep);
            head = rep != null ? "Заменит «" + PetUi.NameOf(rep.data) + "»:" : "Если взять:";
        }
        Set(detHead, head);
        Set(detD1, DeltaLine(d.stat, before, after));
        bool second = rep != null && rep.data.stat != d.stat;
        Set(detD2, second ? DeltaLine(rep.data.stat, before, after) : "");
        Set(detLive, LiveLine(d.stat, before, after));

        equipBtn.gameObject.SetActive(!eq);
        unequipBtn.gameObject.SetActive(eq);
        SetBtn(equipBtn, equipLabel, free || rep != null);
        Set(equipLabel, free ? "Взять с собой" : "Заменить");
        if (equipIco != null) equipIco.sprite = PetIcons.Get(free ? PetIco.Plus : PetIco.Swap);

        if (eq) Set(detNote, "<color=#" + PetUi.Hex(UiKit.Good) + ">Сейчас с тобой</color>");
        else if (free) Set(detNote, "");
        else if (pm.SlotCount < PetManager.SkillSlotIndex && pm.SlotCount < GameBalance.PetSlotLevels.Length)
            Set(detNote, "<color=#" + PetUi.Hex(UiKit.Warn) + ">Слоты заняты. Новый слот: ур. " + GameBalance.PetSlotLevels[pm.SlotCount] + "</color>");
        else if (!PetManager.SkillSlotUnlocked)
            Set(detNote, "<color=#" + PetUi.Hex(UiKit.Warn) + ">Слоты заняты. 4-й слот: навык «Верный друг»</color>");
        else Set(detNote, "");

        // слияние предлагается раньше «Отпустить»
        mergeBtn.gameObject.SetActive(canMerge);
        if (canMerge) Set(mergeLabel, "Слить " + GameBalance.PetMergeCount + " копии: +1 звезда");

        bool armed = armedUntil > 0f;
        bool precious = d.rarity >= PetRarity.Rare || sel.stars > 0;
        Set(releaseLabel, armed ? (precious ? "Отпустить навсегда?" : "Точно отпустить?") : "Отпустить");
        releaseLabel.color = armed ? UiKit.Warn : releaseColor;
        if (armed)
            Set(detNote, "<color=#" + PetUi.Hex(UiKit.Warn) + ">Питомец исчезнет навсегда"
                + (eq ? " и уйдёт из команды" : precious ? ": он ценный" : "") + "</color>");
    }

    // ------------------------------------------------------------ Книга питомцев
    void RefreshBook()
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        int found = pm.DiscoveredCount, total = pm.TotalSpecies;
        Set(bookTotal, "Открыто  <b>" + found + "/" + total + "</b>");
        bookBarFill.rectTransform.anchorMax = new Vector2(total > 0 ? Mathf.Clamp01(found / (float)total) : 0f, 1f);

        for (int r = 3; r >= 0; r--)
        {
            PetRarity rar = (PetRarity)r;
            int f, t;
            pm.RarityProgress(rar, out f, out t);
            bool done = t > 0 && f >= t;
            Color acc = PetUi.RarityAccent(rar);
            bookName[r].color = acc;
            Set(bookName[r], PetText.Rarity(rar));
            Set(bookCount[r], f + "/" + t);
            bookCount[r].color = done ? UiKit.Good : Color.white;
            bookCheck[r].gameObject.SetActive(done);
            float bonus = r < GameBalance.PetBookBonusByRarity.Length ? GameBalance.PetBookBonusByRarity[r] : 0f;
            bookBonus[r].color = done ? UiKit.Good : Dim;
            Set(bookBonus[r], (done ? "Получено: " : "За все виды: ") + "+" + PetUi.Pct(bonus) + "% ко всем");
        }
    }

    static void Set(TMP_Text t, string s) { if (t != null && t.text != s) t.text = s; }

    static void SetBtn(Button b, TMP_Text label, bool on)
    {
        if (b == null) return;
        b.interactable = on;
        if (label != null) label.color = on ? UiKit.Ink : OffText;
    }
}
