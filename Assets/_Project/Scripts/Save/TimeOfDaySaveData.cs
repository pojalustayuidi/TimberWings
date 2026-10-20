// Assets/_Project/Scripts/Save/TimeOfDaySaveData.cs (НЕ в Editor)
// Сохранение времени суток (ключ «time», порядок 90). DayNightCycle читает и пишет time01 и nights, NightBonus пишет silverNight;
// SaveSystem.RequestSave("time") зовут при смене фазы и после сна.
// Создаётся сам до загрузки сцены (RuntimeInitializeOnLoadMethod), в сцене ничего настраивать не нужно.
using System;
using UnityEngine;

public class TimeOfDaySaveData : ISaveable
{
    public static TimeOfDaySaveData Instance { get; private set; }

    // время суток 0..1 (0.3 = утро). По умолчанию из GameBalance
    public float time01 = GameBalance.TimeOfDayDefault;
    public int nights = 0;          // сколько ночей началось (считает DayNightCycle)
    public int day = 1;             // номер дня: растёт на каждом рассвете
    public int silverNight = -1;    // в какую ночь уже появлялось серебряное дерево (NightBonus); -1 = ни в какую

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        Instance = new TimeOfDaySaveData();
        SaveSystem.Register(Instance);   // если Load() уже был, секция применится прямо здесь
    }

    // ------------------------------------------------------------ ISaveable
    public string SaveKey { get { return "time"; } }
    public int LoadOrder { get { return 90; } }

    // ДОБАВЛЯТЬ ПОЛЯ ТОЛЬКО С ДЕФОЛТАМИ
    [Serializable]
    class SaveData
    {
        public int version = 1;
        public float time01 = GameBalance.TimeOfDayDefault;
        public int nights = 0;
        public int silverNight = -1;
        public int day = 1;
    }

    public string Save()
    {
        return JsonUtility.ToJson(new SaveData { time01 = time01, nights = nights, silverNight = silverNight, day = day });
    }

    public void Load(string json, int formatVersion)
    {
        SaveData d = null;
        if (!string.IsNullOrEmpty(json))
        {
            try { d = JsonUtility.FromJson<SaveData>(json); }
            catch (Exception e) { Debug.LogWarning("TimeOfDaySaveData: не удалось прочитать время суток: " + e.Message); }
        }
        if (d == null) { ResetToDefault(); return; }
        time01 = Mathf.Repeat(d.time01, 1f);
        nights = Mathf.Max(0, d.nights);
        silverNight = d.silverNight;
        day = Mathf.Max(1, d.day);
    }

    public void ResetToDefault()
    {
        time01 = GameBalance.TimeOfDayDefault;
        nights = 0;
        silverNight = -1;
        day = 1;
    }
}
