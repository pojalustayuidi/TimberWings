// Assets/_Project/Scripts/UI/menu/SaveSummary.cs (НЕ в Editor)
// Краткая сводка сохранения для меню: уровень и день. Читает файл напрямую (SaveSystem.PeekSections), игру не загружает.
using System;
using UnityEngine;

public static class SaveSummary
{
    [Serializable] class StatsDto { public int level = 1; }     // секция «stats» (PlayerStats)
    [Serializable] class TimeDto { public int day = 1; }        // секция «time» (TimeOfDaySaveData)

    // true: есть хоть что-то показать. level / day = 0, если секции в файле нет.
    public static bool TryGet(out int level, out int day)
    {
        level = 0; day = 0;
        string[] s = null;
        try { s = SaveSystem.PeekSections("stats", "time"); }
        catch (Exception e) { Debug.LogWarning("SaveSummary: " + e.Message); }
        if (s == null) return false;
        try { if (!string.IsNullOrEmpty(s[0])) level = Mathf.Max(0, JsonUtility.FromJson<StatsDto>(s[0]).level); } catch (Exception) { }
        try { if (!string.IsNullOrEmpty(s[1])) day = Mathf.Max(0, JsonUtility.FromJson<TimeDto>(s[1]).day); } catch (Exception) { }
        return level > 0 || day > 0;
    }

    public static string Text()
    {
        int l, d;
        if (!TryGet(out l, out d)) return null;
        if (l > 0 && d > 0) return "Уровень " + l + ", день " + d;
        if (l > 0) return "Уровень " + l;
        return "День " + d;
    }
}
