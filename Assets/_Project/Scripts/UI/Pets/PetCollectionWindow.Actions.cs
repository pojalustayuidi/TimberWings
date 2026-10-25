// Assets/_Project/Scripts/UI/Pets/PetCollectionWindow.Actions.cs (НЕ в Editor)
// ЧАСТЬ класса PetCollectionWindow (partial): действия игрока: открыть/закрыть, вкладки, экипировка, сортировка.
// Это кусок одного класса, поля и методы видны из других частей. Файл с объявлением и состоянием: PetCollectionWindow.cs.
// Остальные части: PetCollectionWindow.Fill.cs, PetCollectionWindow.DeltaBook.cs, PetCollectionWindow.Build.cs, PetCollectionWindow.Presets.cs, PetCollectionWindow.Interact.cs, PetCollectionWindow.HudHelpers.cs.
// Методы этой части: OnSlotClick, OnSlotUnequip, OnCardRightClick, OnEquipClick, OnMergeClick, OnAutoClick, SetTab, OnSortClick, OnFilterClick, OnUnequipClick, OnReleaseClick, NeighborUid, Toast.

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
    // ============================================================ действия
    void OnSlotClick(int i)
    {
        var pm = PetManager.Instance;
        if (pm == null) return;

        PetInstance e = pm.GetEquipped(i);
        if (e != null) { SelectUid(e.uid, true, true); return; }

        if (!pm.IsSlotOpen(i))
        {
            if (i == PetManager.SkillSlotIndex) { Toast("Этот слот открывает навык «Верный друг»"); return; }
            int lvl = i < GameBalance.PetSlotLevels.Length ? GameBalance.PetSlotLevels[i] : 0;
            Toast("Этот слот откроется на " + lvl + " уровне");
            return;
        }

        // пустой открытый слот: ставим (или переносим) туда выбранного питомца
        if (tab != Tab.Collection) { Toast("Открой вкладку «Коллекция», выбери питомца, и он встанет в слот", ToastKind.Info); return; }
        if (selectedUid >= 0 && FindInstance(selectedUid) != null) { QuickSlot(i); return; }
        Toast("Выбери питомца в коллекции, и он встанет в слот", ToastKind.Info);
    }

    // крестик на слоте и ПКМ по слоту: снять питомца (остаётся в коллекции)
    void OnSlotUnequip(int i)
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        if (pm.Unequip(i)) { SfxManager.Play("ui", 0.4f, 0.9f); Toast("Питомец снят: он в коллекции", ToastKind.Info); }
    }

    // ПКМ по карточке: выбрать и сразу взять или снять
    void OnCardRightClick(PoolCard pc)
    {
        if (tab != Tab.Collection || pc.index < 0 || pc.index >= items.Count) return;
        Entry e = items[pc.index];
        if (e.inst == null) return;
        SelectUid(e.inst.uid, true, false);
        ToggleSelectedEquip();
    }

    void OnEquipClick()
    {
        var pm = PetManager.Instance;
        if (pm == null || selectedUid < 0) return;
        if (pm.Equip(selectedUid)) { SfxManager.Play("coin", 0.7f, 1.1f); return; }

        // слоты заняты: ставим на место того, кого вытесняет питомец (его показывает дельта)
        PetInstance sel = FindInstance(selectedUid);
        PetInstance rep = pm.WouldReplace(sel);
        if (rep != null && pm.EquipReplacing(selectedUid, rep.uid)) SfxManager.Play("coin", 0.7f, 1.1f);
        else Toast("Все слоты заняты");
    }

    void OnMergeClick()
    {
        var pm = PetManager.Instance;
        PetInstance sel = FindInstance(selectedUid);
        if (pm == null || sel == null) return;
        int u = pm.Merge(sel.data);
        if (u < 0) { Toast("Слить нельзя"); return; }
        selectedUid = u;
        revealPending = true;                                       // покажем получившегося питомца, даже если список перестроится
        SfxManager.Play("coin", 0.8f, 1.3f);
        if (fx != null) fx.Burst(InnerToFx(DetX + DetW * 0.5f, DetY + 130f), new Color32(0xFF, 0xD2, 0x4A, 255), 22, 320f, 26f, 300f, 0.9f);
        Toast("Слияние: +1 звезда", ToastKind.Good);
    }

    void OnAutoClick()
    {
        var pm = PetManager.Instance;
        if (pm == null) return;
        if (pm.AutoEquipBest()) { SfxManager.Play("coin", 0.7f, 1.1f); Toast("Подобраны лучшие питомцы. Ctrl+Z: отменить", ToastKind.Good); }
        else Toast(pm.Owned.Count == 0 ? "Пока нет питомцев" : "Уже стоят лучшие", pm.Owned.Count == 0 ? ToastKind.Warn : ToastKind.Info);
    }

    void SetTab(Tab t)
    {
        if (tab == t) return;
        tab = t;
        contentsRt.anchoredPosition = Vector2.zero;
        armedUntil = 0f;
        animateNext = true;
        revealPending = t == Tab.Collection;                        // вернулись в коллекцию: показать выбранного
        SfxManager.Play("ui", 0.3f, 1.1f);
        Populate();
    }

    void OnSortClick()
    {
        sortMode = (SortMode)(((int)sortMode + 1) % SortNames.Length);
        contentsRt.anchoredPosition = Vector2.zero;
        animateNext = true;
        revealPending = true;
        SfxManager.Play("ui", 0.3f, 1.1f);
        Populate();
    }

    // следующая характеристика, которая есть у кого-то из питомцев; после последней снова «Все»
    void OnFilterClick()
    {
        var pm = PetManager.Instance;
        int next = filterStat;
        for (int guard = 0; guard < 9; guard++)
        {
            next++;
            if (next > 6) { next = -1; break; }
            bool any = false;
            if (pm != null) for (int i = 0; i < pm.Owned.Count; i++) if ((int)pm.Owned[i].data.stat == next) { any = true; break; }
            if (any) break;
        }
        filterStat = next;
        contentsRt.anchoredPosition = Vector2.zero;
        animateNext = true;
        revealPending = true;
        SfxManager.Play("ui", 0.3f, 1.1f);
        Populate();
    }

    void OnUnequipClick()
    {
        var pm = PetManager.Instance;
        if (pm == null || selectedUid < 0) return;
        for (int i = 0; i < PetManager.MaxSlots; i++)
        {
            var e = pm.GetEquipped(i);
            if (e != null && e.uid == selectedUid)
            {
                if (pm.Unequip(i)) SfxManager.Play("ui", 0.4f, 0.9f);
                return;
            }
        }
    }

    void OnReleaseClick()
    {
        var pm = PetManager.Instance;
        if (pm == null || selectedUid < 0) return;

        if (armedUntil <= 0f)                                       // первое нажатие: просим подтвердить
        {
            PetInstance cur = FindInstance(selectedUid);
            if (cur != null && pm.CanMerge(cur.data)) Toast("Дубликаты лучше слить в звезду: кнопка выше", ToastKind.Info);
            armedUntil = Time.unscaledTime + ArmTime;
            RefreshDetail();
            SfxManager.Play("hint", 0.4f, 0.9f);
            return;
        }

        armedUntil = 0f;
        int uid = selectedUid;
        int next = NeighborUid(uid);                                // выбор переходит к соседней карточке, а не прыгает в начало списка
        selectedUid = -1;
        if (pm.Release(uid))
        {
            selectedUid = next;
            revealPending = true;
            Toast("Питомец отпущен", ToastKind.Info);
            SfxManager.Play("ui", 0.5f, 0.7f);
        }
        else selectedUid = uid;
    }

    // питомец рядом с uid в текущем списке (следующий, иначе предыдущий); -1, если больше никого нет
    int NeighborUid(int uid)
    {
        int idx = -1;
        for (int i = 0; i < sorted.Count; i++) if (sorted[i].uid == uid) { idx = i; break; }
        if (idx < 0) return -1;
        if (idx + 1 < sorted.Count) return sorted[idx + 1].uid;
        if (idx > 0) return sorted[idx - 1].uid;
        return -1;
    }

    void Toast(string s, ToastKind kind = ToastKind.Warn)
    {
        toastText = s;
        toastKind = kind;
        toastStart = Time.unscaledTime;
        toastUntil = Time.unscaledTime + (kind == ToastKind.Warn ? 3f : 2.5f);
        if (kind != ToastKind.Good) SfxManager.Play("ui", 0.3f, 0.8f);   // у «получилось» уже есть свой звук
    }
}
