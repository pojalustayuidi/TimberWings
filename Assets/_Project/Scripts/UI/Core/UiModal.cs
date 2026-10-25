// Assets/_Project/Scripts/UI/Core/UiModal.cs (НЕ в Editor)
// Реестр модальных окон. Заменяет копипасту «а не открыто ли другое окно?»:
// сейчас в PetCrateWindow, PetCollectionWindow, AchievementsWindow, BuildWindow, SellWindow, PlankWorkshopWindow
// и MobileInput у каждого свой список из 5-8 окон, и новое окно приходится дописывать во все.
//
// Как подключить окно (2 строки):
//   void OnEnable()  { UiModal.Register(this, () => IsOpen, Close); }
//   void OnDisable() { UiModal.Unregister(this); }
// Если окно открыто, остальные не открываются:   if (UiModal.AnyOpen(this)) return;
// Управление героем блокируем так:               if (UiModal.AnyOpen()) return true;
// Esc закрывает верхнее окно одним обработчиком: if (Input.GetKeyDown(KeyCode.Escape) && UiModal.CloseTop()) ...
using System;
using System.Collections.Generic;
using UnityEngine;

public static class UiModal
{
    sealed class Entry { public object owner; public Func<bool> isOpen; public Action close; public int order; }

    static readonly List<Entry> entries = new List<Entry>();
    static int counter;

    /// <summary>Сработает при регистрации и удалении окна. Для подписчиков, которым важно знать, что набор окон изменился.</summary>
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { entries.Clear(); counter = 0; Changed = null; }

    public static void Register(object owner, Func<bool> isOpen, Action close = null)
    {
        if (owner == null || isOpen == null) return;
        Unregister(owner);
        entries.Add(new Entry { owner = owner, isOpen = isOpen, close = close, order = ++counter });
        if (Changed != null) Changed();
    }

    public static void Unregister(object owner)
    {
        int n = entries.RemoveAll(e => ReferenceEquals(e.owner, owner));
        if (n > 0 && Changed != null) Changed();
    }

    /// <summary>Открыто ли хоть одно окно. except — не считать это окно (чтобы оно не блокировало само себя).</summary>
    public static bool AnyOpen(object except = null)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (e.owner is UnityEngine.Object uo && uo == null) { entries.RemoveAt(i); continue; } // окно уничтожено
            if (ReferenceEquals(e.owner, except)) continue;
            if (e.isOpen()) return true;
        }
        return false;
    }

    /// <summary>Закрывает окно, зарегистрированное последним среди открытых. true = что-то закрыли (Esc «съеден»).</summary>
    public static bool CloseTop()
    {
        Entry top = null;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (e.owner is UnityEngine.Object uo && uo == null) { entries.RemoveAt(i); continue; }
            if (e.close != null && e.isOpen() && (top == null || e.order > top.order)) top = e;
        }
        if (top == null) return false;
        top.close();
        return true;
    }
}
