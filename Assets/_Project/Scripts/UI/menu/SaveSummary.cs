// Assets/_Project/Scripts/UI/menu/SaveSummary.cs (НЕ в Editor)
// Краткая сводка сохранения для меню: уровень и день. Читает файл напрямую (SaveSystem.PeekSections), игру не загружает.
using System;
using System.IO;
using UnityEngine;

public static class SaveSummary
{
    [Serializable] class StatsDto { public int level = 1; }     // секция «stats» (PlayerStats)
    [Serializable] class TimeDto { public int day = 1; }        // секция «time» (TimeOfDaySaveData)

    static readonly string[] SummaryKeys = { "stats", "time" };
    static string cachedPath;
    static int cachedLevel, cachedDay;
    static (long bytes, long modified) mainVersion, backupVersion;
    static float nextCheck;
    static bool initialized, subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        cachedPath = null;
        cachedLevel = cachedDay = 0;
        initialized = subscribed = false;
        nextCheck = 0f;
    }

    static void Invalidate() { initialized = false; }

    // true: есть хоть что-то показать. level / day = 0, если секции в файле нет.
    public static bool TryGet(out int level, out int day)
    {
        if (!subscribed)
        {
            SaveSystem.Saved -= Invalidate;
            SaveSystem.Saved += Invalidate;
            SaveSystem.Loaded -= Invalidate;
            SaveSystem.Loaded += Invalidate;
            SaveSystem.NewGameStarted -= Invalidate;
            SaveSystem.NewGameStarted += Invalidate;
            subscribed = true;
        }
        string path = SaveSystem.FilePath;
        float now = Time.realtimeSinceStartup;
        if (!initialized || cachedPath != path || now >= nextCheck)
        {
            nextCheck = now + 0.5f;
            var main = FileVersion(path);
            var backup = FileVersion(path + ".bak");
            if (!initialized || cachedPath != path || mainVersion != main || backupVersion != backup)
            {
                Read(out cachedLevel, out cachedDay);
                cachedPath = path;
                mainVersion = main;
                backupVersion = backup;
                initialized = true;
            }
        }
        level = cachedLevel;
        day = cachedDay;
        return level > 0 || day > 0;
    }

    static (long bytes, long modified) FileVersion(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Exists ? (file.Length, file.LastWriteTimeUtc.Ticks) : (-1L, 0L);
        }
        catch (Exception) { return (-1L, 0L); }
    }

    static void Read(out int level, out int day)
    {
        level = 0;
        day = 0;
        string[] s = null;
        try { s = SaveSystem.PeekSections(SummaryKeys); }
        catch (Exception e) { Debug.LogWarning("SaveSummary: " + e.Message); }
        if (s == null) return;
        try { if (!string.IsNullOrEmpty(s[0])) level = Mathf.Max(0, JsonUtility.FromJson<StatsDto>(s[0]).level); } catch (Exception) { }
        try { if (!string.IsNullOrEmpty(s[1])) day = Mathf.Max(0, JsonUtility.FromJson<TimeDto>(s[1]).day); } catch (Exception) { }
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
