// Assets/_Project/Scripts/Core/GameSettings.cs (НЕ в Editor)
// Настройки игрока, которые живут в файле сохранения (секция «settings», LoadOrder 0): качество света и скорость суток.
// Меняются из SettingsMenu, действуют сразу (событие Changed), переживают перезапуск.
//
// Подписка для будущего DayNightCycle и света:
//   void OnEnable()  { GameSettings.Changed += Apply; Apply(); }
//   void OnDisable() { GameSettings.Changed -= Apply; }
//   void Apply() { speed = baseSpeed * GameSettings.DaySpeedMultiplier; if (GameSettings.DayFrozen) time = GameSettings.FixedDayTime; ... GameSettings.LightQuality ... }
// Пока света и смены суток нет, настройки просто хранятся и рассылают Changed.
//
// «Новая игра» настройки НЕ сбрасывает (это не прогресс): ResetToDefault и Load(null) оставляют текущие значения.
using System;
using UnityEngine;

public enum LightQualityLevel { Low = 0, Medium = 1, High = 2 }
public enum DaySpeedMode { Normal = 0, Slow = 1, FixedDay = 2 }

[Serializable]
public class GameSettingsData
{
    public int lightQuality = (int)LightQualityLevel.Medium;   // новые поля добавлять только с дефолтами
    public int daySpeed = (int)DaySpeedMode.Normal;
}

public static class GameSettings
{
    public const float FixedDayTime = 0.5f;     // время суток при «Фиксированный день» (0..1, 0.5 = полдень); заглушка до DayNightCycle
    public const float SlowDayFactor = 0.5f;    // «Медленная»: сутки идут вдвое медленнее

    public static event Action Changed;         // свет или скорость суток изменились (в т.ч. после «Продолжить»)

    static LightQualityLevel lightQuality = LightQualityLevel.Medium;
    static DaySpeedMode daySpeed = DaySpeedMode.Normal;
    static readonly Saveable saveable = new Saveable();

    public static LightQualityLevel LightQuality
    {
        get { return lightQuality; }
        set
        {
            if (lightQuality == value) return;
            lightQuality = value;
            Raise();
        }
    }

    public static DaySpeedMode DaySpeed
    {
        get { return daySpeed; }
        set
        {
            if (daySpeed == value) return;
            daySpeed = value;
            Raise();
        }
    }

    // множитель скорости суток: 1 обычная, 0.5 медленная, 0 стоит
    public static float DaySpeedMultiplier
    {
        get { return daySpeed == DaySpeedMode.Normal ? 1f : daySpeed == DaySpeedMode.Slow ? SlowDayFactor : 0f; }
    }

    public static bool DayFrozen { get { return daySpeed == DaySpeedMode.FixedDay; } }

    static void Raise()
    {
        SaveSystem.RequestSave("settings");   // во время загрузки SaveSystem сам игнорирует
        if (Changed == null) return;
        foreach (Delegate d in Changed.GetInvocationList())
        {
            try { ((Action)d)(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    // после «Продолжить» Load применил значения без событий: сообщаем один раз
    static void OnSaveLoaded()
    {
        if (Changed == null) return;
        foreach (Delegate d in Changed.GetInvocationList())
        {
            try { ((Action)d)(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        lightQuality = LightQualityLevel.Medium;
        daySpeed = DaySpeedMode.Normal;
        Changed = null;
    }

    // порядок с SaveAutoRunner.Boot не важен: если файл уже прочитан, секция применится прямо в Register
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        SaveSystem.Register(saveable);
        SaveSystem.Loaded += OnSaveLoaded;
    }

    sealed class Saveable : ISaveable
    {
        public string SaveKey { get { return "settings"; } }
        public int LoadOrder { get { return 0; } }

        public string Save()
        {
            return JsonUtility.ToJson(new GameSettingsData { lightQuality = (int)lightQuality, daySpeed = (int)daySpeed });
        }

        // без событий и RequestSave: Changed уйдёт по SaveSystem.Loaded
        public void Load(string json, int formatVersion)
        {
            if (string.IsNullOrEmpty(json)) return;   // секции нет: оставляем текущее
            GameSettingsData d = JsonUtility.FromJson<GameSettingsData>(json);
            if (d == null) return;
            lightQuality = (LightQualityLevel)Mathf.Clamp(d.lightQuality, 0, 2);
            daySpeed = (DaySpeedMode)Mathf.Clamp(d.daySpeed, 0, 2);
        }

        public void ResetToDefault() { }   // настройки не прогресс: «Новая игра» их не трогает
    }
}
