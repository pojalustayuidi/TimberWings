// Assets/_Project/Scripts/UI/Pets/PetCollectionWindow.Fill.cs (НЕ в Editor)
// ЧАСТЬ класса PetCollectionWindow (partial): заполнение окна данными, пул карточек питомцев.
// Это кусок одного класса, поля и методы видны из других частей. Файл с объявлением и состоянием: PetCollectionWindow.cs.
// Остальные части: PetCollectionWindow.Actions.cs, PetCollectionWindow.DeltaBook.cs, PetCollectionWindow.Build.cs, PetCollectionWindow.Presets.cs, PetCollectionWindow.Interact.cs, PetCollectionWindow.HudHelpers.cs.
// Методы этой части: Compare, CompareSpecies, FindInstance, StatShort, Populate, LayoutCells, StartAppear, Bind, IsSelectedEntry, OnCardClick, ToggleSelectedEquip, OnCardClickCore, RecheckPortraits, AddEquippedMark, SelectUid, EnsureVisible, RefreshHeader, RefreshSlots, RefreshBonus.

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
    // ============================================================ заполнение
    int Compare(PetInstance a, PetInstance b)
    {
        int r;
        switch (sortMode)
        {
            case SortMode.Stat:
                r = ((int)a.data.stat).CompareTo((int)b.data.stat);
                if (r != 0) return r;
                r = b.Bonus.CompareTo(a.Bonus);
                if (r != 0) return r;
                break;
            case SortMode.Name:
                r = string.CompareOrdinal(PetUi.NameOf(a.data), PetUi.NameOf(b.data));
                if (r != 0) return r;
                break;
            case SortMode.Newest:
                if (a.isNew != b.isNew) return a.isNew ? -1 : 1;
                return b.uid.CompareTo(a.uid);
        }
        // остальное одинаково для всех режимов: редкость от Легендарных, звёзды, имя, номер
        r = ((int)b.data.rarity).CompareTo((int)a.data.rarity);
        if (r != 0) return r;
        r = b.stars.CompareTo(a.stars);
        if (r != 0) return r;
        r = string.CompareOrdinal(PetUi.NameOf(a.data), PetUi.NameOf(b.data));
        if (r != 0) return r;
        return a.uid.CompareTo(b.uid);
    }

    static int CompareSpecies(PetData a, PetData b)
    {
        int r = ((int)b.rarity).CompareTo((int)a.rarity);
        if (r != 0) return r;
        return string.CompareOrdinal(PetUi.NameOf(a), PetUi.NameOf(b));
    }

    PetInstance FindInstance(int uid)
    {
        if (uid < 0) return null;
        for (int i = 0; i < sorted.Count; i++) if (sorted[i].uid == uid) return sorted[i];
        return null;
    }

    static string StatShort(PetStat s)
    {
        switch (s)
        {
            case PetStat.ChopPower: return "Рубка";
            case PetStat.MoveSpeed: return "Бег";
            case PetStat.Capacity: return "Рюкзак";
            case PetStat.DoubleYield: return "Двойная добыча";
            case PetStat.Haggle: return "Цена";
            case PetStat.SawSpeed: return "Пила";
            default: return "Рабочие";
        }
    }

    // пересобирает список (данные, не объекты), слоты, бонусы и описание. Зовётся при открытии, смене вида и при PetManager.Changed
    void Populate()
    {
        dirty = false;
        var pm = PetManager.Instance;
        float keepY = contentsRt.anchoredPosition.y;

        sorted.Clear();
        if (pm != null)
            for (int i = 0; i < pm.Owned.Count; i++)
            {
                var o = pm.Owned[i];
                if ((filterStat < 0 || (int)o.data.stat == filterStat) && (rarFilter < 0 || (int)o.data.rarity == rarFilter)) sorted.Add(o);
            }
        sorted.Sort(Compare);

        items.Clear();
        if (tab == Tab.Collection)
        {
            for (int i = 0; i < sorted.Count; i++) items.Add(new Entry { inst = sorted[i], data = sorted[i].data });
        }
        else if (pm != null && pm.database != null)
        {
            var species = new List<PetData>();
            for (int i = 0; i < pm.database.pets.Count; i++) if (pm.database.pets[i] != null) species.Add(pm.database.pets[i]);
            species.Sort(CompareSpecies);
            for (int i = 0; i < species.Count; i++) items.Add(new Entry { data = species[i], locked = !pm.IsDiscovered(species[i]) });
        }

        // выбранный пропал или ещё не выбран: берём первого из слотов (если он прошёл фильтр), иначе первого в списке
        if (FindInstance(selectedUid) == null)
        {
            selectedUid = -1;
            if (pm != null)
                for (int s = 0; s < PetManager.MaxSlots && selectedUid < 0; s++)
                {
                    var e = pm.GetEquipped(s);
                    if (e != null && FindInstance(e.uid) != null) selectedUid = e.uid;
                }
            if (selectedUid < 0 && sorted.Count > 0) selectedUid = sorted[0].uid;
        }

        int rows = (items.Count + Cols - 1) / Cols;
        float h = 8f + rows * PitchY + 8f;
        contentsRt.sizeDelta = new Vector2(0f, h);
        float maxY = Mathf.Max(0f, h - (GridH - 16f));
        contentsRt.anchoredPosition = new Vector2(0f, Mathf.Clamp(keepY, 0f, maxY));

        // выбранного показываем сразу: после «Отпустить», слияния, смены сортировки или вкладки
        if (revealPending && tab == Tab.Collection)
        {
            revealPending = false;
            for (int i = 0; i < items.Count; i++)
                if (items[i].inst != null && items[i].inst.uid == selectedUid) { EnsureVisible(8f + (i / Cols) * PitchY); break; }
        }

        bool none = items.Count == 0;
        bool filtered = tab == Tab.Collection && pm != null && pm.Owned.Count > 0;
        emptyGrid.gameObject.SetActive(none);
        if (resetBtn != null) resetBtn.gameObject.SetActive(none && filtered);
        if (none)
            Set(emptyGrid, tab == Tab.Book ? "База питомцев пуста"
                : (filtered ? "Нет питомцев с такими фильтрами"
                    : "Пока нет ни одного питомца.\nОткрывай клети: Лесная, Речная и Небесная ждут в лагере."));

        detRoot.gameObject.SetActive(tab == Tab.Collection);
        bookRoot.gameObject.SetActive(tab == Tab.Book);

        RefreshHeader();
        RefreshBonus();
        LayoutCells(true);
        SelectUid(selectedUid, false, false);            // подсветка, слоты и описание (выбор переживает смену вкладки)
        if (tab == Tab.Book) RefreshBook();
    }

    // ------------------------------------------------------------ пул карточек
    // Видны максимум 4 строки, поэтому живут PoolSize карточек: индекс i всегда достаётся pool[i % PoolSize].
    // При прокрутке привязываются только вновь появившиеся индексы.
    void LayoutCells(bool rebind)
    {
        layoutDirty = false;
        float y = contentsRt.anchoredPosition.y;
        lastScrollY = y;

        int firstRow = Mathf.Max(0, Mathf.FloorToInt((y - 8f) / PitchY));
        int first = firstRow * Cols;
        int last = Mathf.Min(items.Count, first + PoolSize);

        for (int k = 0; k < pool.Count; k++)
        {
            var pc = pool[k];
            if (pc.index >= 0 && (pc.index < first || pc.index >= last || pc.index >= items.Count))
            {
                pc.index = -1;
                pc.view.rt.gameObject.SetActive(false);
            }
        }
        bool wave = animateNext && rebind;
        for (int i = first; i < last; i++)
        {
            var pc = pool[i % PoolSize];
            bool fresh = pc.index != i;
            if (!fresh && !rebind) continue;
            Bind(pc, i);
            if (wave) StartAppear(pc, (i - first) * 0.03f);              // открытие, смена вкладки или фильтра: волной
            else if (fresh) StartAppear(pc, 0f);                          // новая строка при прокрутке: быстро
        }
        if (wave) animateNext = false;
    }

    static void StartAppear(PoolCard pc, float delay)
    {
        pc.appear = 0f;
        pc.delay = delay;
        if (pc.cg != null) pc.cg.alpha = 0f;
    }

    void Bind(PoolCard pc, int i)
    {
        var pm = PetManager.Instance;
        Entry e = items[i];
        pc.index = i;
        pc.view.rt.gameObject.SetActive(true);

        float x = CellLeft + (i % Cols) * PitchX;
        float cy = 8f + (i / Cols) * PitchY;
        Anch(pc.view.rt, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x + CardW * 0.5f, -(cy + CardH * 0.5f)), new Vector2(CardW, CardH));

        if (e.locked)
        {
            pc.view.SetLocked(e.data);
            pc.mark.SetActive(false);
        }
        else
        {
            pc.view.Set(e.data, e.inst != null ? e.inst.stars : 0, e.inst != null && e.inst.isNew);
            pc.mark.SetActive(e.inst != null && pm != null && pm.IsEquipped(e.inst.uid));
        }
        pc.sc = IsSelectedEntry(e) ? 1.06f : 1f;
        pc.view.rt.localScale = new Vector3(pc.sc, pc.sc, 1f);
    }

    bool IsSelectedEntry(Entry e)
    {
        return tab == Tab.Collection && e.inst != null && selectedUid >= 0 && e.inst.uid == selectedUid;
    }

    void OnCardClick(PoolCard pc)
    {
        bool dbl = pc.index >= 0 && pc.index == lastClickIdx && Time.unscaledTime - lastClickT < 0.35f && tab == Tab.Collection;
        lastClickIdx = pc.index;
        lastClickT = Time.unscaledTime;
        OnCardClickCore(pc);
        if (dbl) ToggleSelectedEquip();
    }

    // Enter и двойной клик: питомец в слот, а если уже там, то снять
    void ToggleSelectedEquip()
    {
        var pm = PetManager.Instance;
        if (pm == null || selectedUid < 0) return;
        if (pm.IsEquipped(selectedUid)) OnUnequipClick(); else OnEquipClick();
    }

    void OnCardClickCore(PoolCard pc)
    {
        if (pc.index < 0 || pc.index >= items.Count) return;
        Entry e = items[pc.index];
        if (e.locked) { Toast("Ещё не найден: " + PetText.Rarity(e.data.rarity) + " питомец"); return; }
        if (e.inst == null)    // книга: открытый вид
        {
            Toast(PetUi.NameOf(e.data) + ": " + PetText.Rarity(e.data.rarity) + ", " + PetText.Stat(e.data.stat));
            return;
        }
        SelectUid(e.inst.uid, true, false);
    }

    // 3D-снимки рисуются в фоне: карточки, которым снимка не хватило, перерисовываем, когда он появится
    void RecheckPortraits()
    {
        for (int k = 0; k < pool.Count; k++)
            if (pool[k].index >= 0 && pool[k].view.pendingPortrait) Bind(pool[k], pool[k].index);
        if (detCard != null && detCard.pendingPortrait && tab == Tab.Collection) RefreshDetail();
        if (presetPortraitPending) RefreshPresets();
    }

    // зелёная галочка в правом нижнем углу карточки: питомец сейчас в слоте
    static GameObject AddEquippedMark(PetCardView cv)
    {
        var mid = new Vector2(0.5f, 0.5f);
        var m = UiKit.MakeImage(cv.rt, "Equipped", UiKit.Medallion, false);
        m.color = UiKit.Good;
        Anch(m.rectTransform, new Vector2(1f, 0f), mid, new Vector2(-26f, 62f), new Vector2(38f, 38f));
        var ic = UiKit.MakeIcon(m.transform, UiIcon.Check, 24f, "Check");
        ic.rectTransform.anchoredPosition = Vector2.zero;
        m.gameObject.SetActive(false);
        return m.gameObject;
    }

    void SelectUid(int uid, bool user, bool reveal)
    {
        selectedUid = uid;
        armedUntil = 0f;
        var pm = PetManager.Instance;
        if (uid != lastDetUid) { lastDetUid = uid; detT = 0f; bubbleT = -1f; }       // описание «переезжает» на нового питомца

        // игрок сам выбрал питомца: бейдж NEW гаснет
        if (user && pm != null && uid >= 0)
        {
            var si = FindInstance(uid);
            if (si != null && si.isNew) pm.MarkSeen(uid);
        }

        int idx = -1;
        if (tab == Tab.Collection && uid >= 0)
            for (int i = 0; i < items.Count; i++) if (items[i].inst != null && items[i].inst.uid == uid) { idx = i; break; }

        for (int k = 0; k < pool.Count; k++)
        {
            var pc = pool[k];
            if (pc.index < 0) continue;
            bool on = pc.index == idx;
            if (on && user) Bind(pc, pc.index);           // обновить NEW и звёзды (масштаб сглаживает AnimateCards)
        }

        bool frameWas = selFrame.gameObject.activeSelf;
        selFrame.gameObject.SetActive(idx >= 0);
        if (idx >= 0)
        {
            float x = CellLeft + (idx % Cols) * PitchX;
            float cy = 8f + (idx / Cols) * PitchY;
            selTarget = new Vector2(x + CardW * 0.5f, -(cy + CardH * 0.5f));
            if (!frameWas) selFrame.rectTransform.anchoredPosition = selTarget;   // появилась: сразу на месте, дальше плавно
            if (reveal) { EnsureVisible(cy); layoutDirty = true; }
        }

        RefreshSlots();
        RefreshDetail();
        if (user) SfxManager.Play("ui", 0.3f, 1.2f);
        if (user && uid >= 0 && tab == Tab.Collection && Random.value < 0.35f) ShowBubble(GreetPhrases[Random.Range(0, GreetPhrases.Length)]);
    }

    // прокручивает коллекцию так, чтобы карточка (верх = top) была в окне
    void EnsureVisible(float top)
    {
        float vh = GridH - 16f;
        float y = contentsRt.anchoredPosition.y;
        if (top < y) y = top - 8f;
        else if (top + CardH > y + vh) y = top + CardH - vh + 8f;
        float maxY = Mathf.Max(0f, contentsRt.sizeDelta.y - vh);
        contentsRt.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, maxY));
        if (scroll != null) scroll.velocity = Vector2.zero;
    }

    void RefreshHeader()
    {
        var pm = PetManager.Instance;
        int n = pm != null ? pm.Owned.Count : 0;
        Set(countText, n.ToString());

        int found = pm != null ? pm.DiscoveredCount : 0, total = pm != null ? pm.TotalSpecies : 0;
        Set(tabColLabel, "Коллекция  " + (sorted.Count != n ? sorted.Count + "/" + n : n.ToString()));
        Set(tabBookLabel, "Книга  " + found + "/" + total);
        tabColImg.color = tab == Tab.Collection ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);
        tabBookImg.color = tab == Tab.Book ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);

        bool col = tab == Tab.Collection;
        Set(sortLabel, "Сорт.: " + SortNames[(int)sortMode]);
        Set(filterLabel, "Фильтр: " + (filterStat < 0 ? "все" : StatShort((PetStat)filterStat)));
        SetBtn(sortBtn, sortLabel, col);
        SetBtn(filterBtn, filterLabel, col);
        for (int j = 0; j < 5; j++)
        {
            rarImg[j].color = (rarFilter == j - 1 && col) ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);
            if (rarBtn[j] != null) rarBtn[j].interactable = col;
        }
    }

    void RefreshSlots()
    {
        var pm = PetManager.Instance;
        int unlocked = pm != null ? pm.SlotCount : 1;
        int used = pm != null ? pm.EquippedCount : 0;
        Set(slotsLabel, "Команда  <size=65%><color=#" + PetUi.Hex(Dim) + ">" + used + " / " + unlocked + "</color></size>");

        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            bool isOpen = pm != null ? pm.IsSlotOpen(i) : i == 0;
            PetInstance e = pm != null ? pm.GetEquipped(i) : null;
            bool has = e != null;

            s.card.rt.gameObject.SetActive(has);
            if (s.xBtn != null) s.xBtn.SetActive(has);
            if (s.hover != null)
            {
                int lv = i < GameBalance.PetSlotLevels.Length ? GameBalance.PetSlotLevels[i] : 0;
                s.hover.tip = has ? "<b>" + PetUi.NameOf(e.data) + "</b>: клик выбирает  ·  крестик или ПКМ снимает  ·  цифра " + (i + 1) + " ставит выбранного сюда"
                    : isOpen ? "Свободный слот " + (i + 1) + ": выбери питомца и нажми сюда (или цифру " + (i + 1) + ")"
                    : i == PetManager.SkillSlotIndex ? "Закрыт: этот слот открывает навык «Верный друг»"
                    : "Закрыт: откроется на " + lv + " уровне";
            }
            s.baseCol = has ? Color.Lerp(Color.white, PetUi.RarityAccent(e.data.rarity), 0.45f) : Color.white;
            s.baseScale = has && e.uid == selectedUid ? 1.06f : 1f;
            int curUid = has ? e.uid : -1;
            if (lastUid[i] != -2 && curUid >= 0 && curUid != lastUid[i])
            {
                s.pop = 1f;
                if (fx != null)
                    fx.Burst(InnerToFx(TeamX + 10f + (i % 2) * (SlotW + SlotGapX) + SlotW * 0.5f, TeamY + SlotY + (i / 2) * (SlotH + SlotGapY) + SlotH * 0.5f),
                             PetUi.RarityAccent(e.data.rarity), 16, 280f, 22f, 320f, 0.75f);
            }
            lastUid[i] = curUid;
            if (has)
            {
                s.card.Set(e.data, e.stars, false);
                s.card.rt.localScale = e.uid == selectedUid ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
            }

            s.icon.gameObject.SetActive(!has);
            s.icon.sprite = UiKit.Icon(isOpen ? UiIcon.Paw : UiIcon.Lock);
            s.icon.color = new Color(1f, 1f, 1f, isOpen ? 0.28f : 0.5f);

            s.label.gameObject.SetActive(!has);
            if (!has)
            {
                int lvl = i < GameBalance.PetSlotLevels.Length ? GameBalance.PetSlotLevels[i] : 0;
                Set(s.label, isOpen ? "Пусто" : (i == PetManager.SkillSlotIndex ? "Навык" : "Ур. " + lvl));
            }
        }
        if (autoBtn != null) SetBtn(autoBtn, autoLabel, pm != null && pm.Owned.Count > 0);
        if (unequipAllBtn != null) SetBtn(unequipAllBtn, unequipAllLabel, pm != null && pm.EquippedCount > 0);
        if (undoBtn != null) SetBtn(undoBtn, undoLabel, pm != null && pm.CanUndoLoadout);
        RefreshPresets();
    }

    void RefreshBonus()
    {
        var pm = PetManager.Instance;
        PetBonusSet b = pm != null ? pm.Bonus : PetBonusSet.None;
        int n = 0;
        for (int k = 0; k < 7; k++)
        {
            PetStat st = (PetStat)k;
            string val = null;
            float frac = -1f;
            if (st == PetStat.Capacity)
            {
                if (b.capacityLogs > 0)
                    val = "+" + b.capacityLogs + " " + UiDraw.Plural(b.capacityLogs, "бревно", "бревна", "бревен");
            }
            else
            {
                float v = b.Get(st);
                if (v > 0.0001f)
                {
                    val = "+" + PetUi.Pct(v) + "%";
                    frac = Mathf.Clamp01(v / GameBalance.PetBonusCap);
                    if (v >= GameBalance.PetBonusCap - 1e-4f) val += " <size=70%>макс.</size>";
                }
            }
            if (val == null) continue;

            bonusIcon[n].gameObject.SetActive(true);
            bonusIcon[n].sprite = UiKit.Icon(PetUi.StatIcon(st));
            bonusText[n].gameObject.SetActive(true);
            bonusBarTrack[n].gameObject.SetActive(frac >= 0f);
            if (frac >= 0f)
            {
                bonusBarTgt[n] = Mathf.Max(0.02f, frac);                       // дальше доезжает AnimateMisc
                if (!bonusSigInit) bonusBarCur[n] = bonusBarTgt[n];            // первая отрисовка после открытия: сразу на месте
                bonusBarFill[n].color = frac >= 0.999f ? UiKit.BrassHi : UiKit.Good;
            }
            Set(bonusText[n], "<b>" + PetText.Stat(st) + "</b>  <color=#" + PetUi.Hex(UiKit.Good) + ">" + val + "</color>");
            n++;
        }
        // бонусы изменились (взяли, сняли, слили): панель коротко вспыхивает
        int sig = b.capacityLogs;
        for (int q = 0; q < 7; q++) sig = unchecked(sig * 31 + Mathf.RoundToInt(b.Get((PetStat)q) * 10000f));
        if (bonusSigInit && sig != bonusSig) bonusFlash = 1f;
        bonusSig = sig;
        bonusSigInit = true;

        for (int i = n; i < bonusIcon.Length; i++)
        {
            bonusIcon[i].gameObject.SetActive(false);
            bonusText[i].gameObject.SetActive(false);
            bonusBarTrack[i].gameObject.SetActive(false);
        }
        bonusEmpty.gameObject.SetActive(n == 0);
    }
}
