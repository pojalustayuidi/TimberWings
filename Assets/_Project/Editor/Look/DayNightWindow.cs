// Assets/_Project/Editor/Look/DayNightWindow.cs (в папке Editor)
// Окно для проверки смены суток: Tools > BuilderSim > Время суток. Работает в Play Mode.
//   Ползунок и кнопки: поставить время сразу или плавно, скорость часов (0 / 1 / 10 / 60 / 200),
//   пропуск ночи как сон, серебряное дерево прямо сейчас, сброс «раз за ночь», настройка «Скорость суток».
using UnityEditor;
using UnityEngine;

public class DayNightWindow : EditorWindow
{
    static readonly string[] names = { "Рассвет", "Утро", "Полдень", "Закат", "Ночь", "Полночь" };
    static readonly float[] times = { 0.22f, 0.30f, 0.50f, 0.74f, 0.88f, 0.00f };
    static readonly string[] speedNames = { "Стоп", "x1", "x10", "x60", "x200" };
    static readonly float[] speeds = { 0f, 1f, 10f, 60f, 200f };

    Vector2 scroll;

    [MenuItem("Tools/BuilderSim/Время суток")]
    static void Open()
    {
        var w = GetWindow<DayNightWindow>("Время суток");
        w.minSize = new Vector2(320f, 380f);
    }

    void OnInspectorUpdate() { Repaint(); }

    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Запусти игру (Play Mode): цикл создаётся после загрузки сцены с игроком.", MessageType.Info);
            return;
        }
        var c = DayNightCycle.Instance;
        if (c == null)
        {
            EditorGUILayout.HelpBox("DayNightCycle ещё не создан (нужна сцена с PlayerMovement).", MessageType.Warning);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        // ---- время
        float t = DayNightCycle.Time01;
        int mins = Mathf.RoundToInt(t * 24f * 60f) % (24 * 60);
        EditorGUILayout.LabelField("Сейчас", (mins / 60).ToString("00") + ":" + (mins % 60).ToString("00") + "   " + DayNightCycle.Phase +
            "   (ночь " + DayNightCycle.Night01.ToString("0.00") + ", сумерки " + DayNightCycle.Twilight01.ToString("0.00") + ")", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        float nt = EditorGUILayout.Slider("Время (0..1)", t, 0f, 1f);
        if (EditorGUI.EndChangeCheck()) c.SetTime(nt);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Поставить сразу");
        TimeButtons(c, false);
        EditorGUILayout.LabelField("Плавно (2 с)");
        TimeButtons(c, true);

        // ---- скорость
        EditorGUILayout.Space();
        int cur = 1;
        for (int i = 0; i < speeds.Length; i++) if (Mathf.Approximately(speeds[i], DayNightCycle.DebugSpeed)) cur = i;
        int pick = GUILayout.Toolbar(cur, speedNames);
        if (pick != cur) DayNightCycle.DebugSpeed = speeds[pick];
        EditorGUILayout.LabelField("Скорость часов (отладка)", "x" + DayNightCycle.DebugSpeed + "   настройка игрока x" + GameSettings.DaySpeedMultiplier + "   костёр x" + DayNightCycle.ExternalSpeed);

        EditorGUI.BeginChangeCheck();
        var mode = (DaySpeedMode)EditorGUILayout.EnumPopup("Скорость суток (настройка)", GameSettings.DaySpeed);
        if (EditorGUI.EndChangeCheck()) GameSettings.DaySpeed = mode;

        // ---- сон и бонус
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Сон и ночной бонус", EditorStyles.boldLabel);
        if (GUILayout.Button("Пропустить ночь как сон (домотать до рассвета)"))
            c.WarpTo(GameBalance.SleepWakeTime, GameBalance.SleepFadeSeconds);

        if (GUILayout.Button("Серебряное дерево сейчас (ставит ночь и сбрасывает «раз за ночь»)"))
        {
            var sd = TimeOfDaySaveData.Instance;
            if (sd != null) sd.silverNight = -1;
            if (DayNightCycle.Phase != DayPhase.Night) c.SetTime(0.9f);
            var nb = c.GetComponent<NightBonus>();
            if (nb != null) nb.DebugSkipDelay();
        }

        if (GUILayout.Button("Сбросить «раз за ночь» (серебро снова доступно в эту ночь)"))
        {
            var sd = TimeOfDaySaveData.Instance;
            if (sd != null) sd.silverNight = -1;
        }

        var save = TimeOfDaySaveData.Instance;
        EditorGUILayout.LabelField("Ночей началось", save != null ? save.nights.ToString() : "-");
        EditorGUILayout.LabelField("Серебро было в ночь", save != null ? save.silverNight.ToString() : "-");
        EditorGUILayout.LabelField("Сон идёт / перемотка", CampSleep.Sleeping + " / " + DayNightCycle.Warping);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("В игре: F6 следующая фаза, F7 зажать = x60, F8 пропуск ночи, F9 панель с цифрами.\nУ костра: зажать F = ускорить время.", MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    static void TimeButtons(DayNightCycle c, bool smooth)
    {
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < names.Length; i++)
        {
            if (!GUILayout.Button(names[i])) continue;
            if (smooth) c.WarpTo(times[i], 2f); else c.SetTime(times[i]);
        }
        EditorGUILayout.EndHorizontal();
    }
}
