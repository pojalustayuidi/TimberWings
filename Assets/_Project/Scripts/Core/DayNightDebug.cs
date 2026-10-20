// Assets/_Project/Scripts/Core/DayNightDebug.cs (НЕ в Editor)
// Отладка смены суток прямо в игре. Существует только в редакторе и Development Build, в релиз не попадает.
// Создаётся из DayNightCycle сам. Клавиши:
//   F6  прыгнуть к следующей фазе (рассвет -> день -> закат -> ночь), плавно за 1.5 с
//   F7  зажать: сутки идут в 60 раз быстрее
//   F8  пропустить ночь как сон (домотать до рассвета)
//   F9  показать/скрыть панель: время, фаза, ночь/сумерки 0..1, скорость, счётчик ночей, серебряное дерево
// Окно с кнопками и ползунком: Tools > BuilderSim > Время суток.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

public sealed class DayNightDebug : MonoBehaviour
{
    public static KeyCode nextPhaseKey = KeyCode.F6;
    public static KeyCode boostKey = KeyCode.F7;
    public static KeyCode skipNightKey = KeyCode.F8;
    public static KeyCode panelKey = KeyCode.F9;

    bool panel;
    GUIStyle style;

    void Update()
    {
        var c = DayNightCycle.Instance;
        if (c == null) return;

        DayNightCycle.DebugBoost = Input.GetKey(boostKey) ? 60f : 1f;

        if (Input.GetKeyDown(panelKey)) panel = !panel;

        if (Input.GetKeyDown(nextPhaseKey))
        {
            float t = DayNightCycle.Time01;
            float[] starts = { GameBalance.TimeDawnStart, GameBalance.TimeDayStart, GameBalance.TimeDuskStart, GameBalance.TimeNightStart };
            float best = 2f, target = starts[0];
            for (int i = 0; i < starts.Length; i++)
            {
                float d = Mathf.Repeat(starts[i] + 0.001f - t, 1f);   // сколько вперёд до начала фазы
                if (d < best) { best = d; target = starts[i] + 0.001f; }
            }
            c.WarpTo(target, 1.5f);
        }

        if (Input.GetKeyDown(skipNightKey)) c.WarpTo(GameBalance.SleepWakeTime, GameBalance.SleepFadeSeconds);
    }

    void OnDestroy() { DayNightCycle.DebugBoost = 1f; }

    void OnGUI()
    {
        if (!panel) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true };
        }
        float t = DayNightCycle.Time01;
        int mins = Mathf.RoundToInt(t * 24f * 60f) % (24 * 60);
        var sd = TimeOfDaySaveData.Instance;
        string txt =
            "<b>Время суток</b>  " + (mins / 60).ToString("00") + ":" + (mins % 60).ToString("00") + "  (" + t.ToString("0.000") + ")\n" +
            "Фаза: " + DayNightCycle.Phase + "   Солнце: " + DayNightCycle.SunElevation(t).ToString("0") + "°\n" +
            "Ночь: " + DayNightCycle.Night01.ToString("0.00") + "   Сумерки: " + DayNightCycle.Twilight01.ToString("0.00") + "\n" +
            "Скорость: настройка x" + GameSettings.DaySpeedMultiplier.ToString("0.#") + ", отладка x" + (DayNightCycle.DebugSpeed * DayNightCycle.DebugBoost).ToString("0.#") +
            ", костёр x" + DayNightCycle.ExternalSpeed.ToString("0.#") + "\n" +
            "Ночей: " + (sd != null ? sd.nights : 0) + "   Серебро было в ночь: " + (sd != null ? sd.silverNight : -1) + "\n" +
            "Сон: " + CampSleep.Sleeping + "   Перемотка: " + DayNightCycle.Warping + "\n" +
            "<color=#AAAAAA>F6 фаза, F7 x60, F8 пропуск ночи, F9 скрыть</color>";
        GUI.Box(new Rect(10f, 10f, 430f, 150f), txt, style);
    }
}
#endif
