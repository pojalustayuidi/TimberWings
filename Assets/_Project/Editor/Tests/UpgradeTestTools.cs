// Assets/_Project/Editor/Tests/UpgradeTestTools.cs (Editor)
// Быстрое тестирование прокачки без прохождения сюжета. Меню: Tools > BuilderSim > Тест прокачки.
// Все пункты, кроме «Открывать при старте», работают только в Play Mode.
//   Открыть окно прокачки   Ctrl+Shift+U   разблокирует окно (минуя сюжет) и открывает его
//   +1000 монет             Ctrl+Shift+M
//   +1 свободное очко       Ctrl+Shift+P
//   Уровень игрока...       ставит 3 / 5 / 7 / 10 (пила открывается с 3-го, правые таланты с 4-го и 6-го)
//   Всё на максимум / Сбросить прокачку
//   Открывать при старте    галка: окно само откроется через пару секунд после нажатия Play (+ монеты, очки, 5-й уровень)
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class UpgradeTestTools
{
    const string Root = "Tools/BuilderSim/Отладка и тесты/Тест прокачки/";
    const string AutoKey = "BuilderSim.UpgradeTest.AutoOpen";

    static double waitUntil;

    static UpgradeTestTools()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    static void OnPlayMode(PlayModeStateChange s)
    {
        if (s != PlayModeStateChange.EnteredPlayMode || !EditorPrefs.GetBool(AutoKey, false)) return;
        waitUntil = EditorApplication.timeSinceStartup + 20.0;   // ждём, пока StoryFlow создаст окно и игрока
        EditorApplication.update -= WaitAndOpen;
        EditorApplication.update += WaitAndOpen;
    }

    static void WaitAndOpen()
    {
        if (!Application.isPlaying || EditorApplication.timeSinceStartup > waitUntil)
        {
            EditorApplication.update -= WaitAndOpen;
            return;
        }
        if (UpgradeWindow.Instance == null || PlayerUpgrades.Instance == null || Object.FindFirstObjectByType<PlayerStats>() == null) return;
        EditorApplication.update -= WaitAndOpen;
        Prepare();
        OpenWindow();
    }

    static bool InPlay()
    {
        if (Application.isPlaying) return true;
        Debug.LogWarning("[Тест прокачки] Сначала нажми Play.");
        return false;
    }

    static void Prepare()
    {
        AddCoins(1000);
        SetLevel(5);
        var up = PlayerUpgrades.Instance;
        if (up != null && up.freePoints < 2) { up.DebugAddFreePoint(); up.DebugAddFreePoint(); }
    }

    // ---------------------------------------------------------------- пункты меню
    [MenuItem(Root + "Открыть окно прокачки %#u", false, 1040)]
    static void OpenWindow()
    {
        if (!InPlay()) return;
        var w = UpgradeWindow.Instance != null ? UpgradeWindow.Instance : Object.FindFirstObjectByType<UpgradeWindow>();
        if (w == null) { Debug.LogWarning("[Тест прокачки] UpgradeWindow ещё не создан (его создаёт StoryFlow)."); return; }
        w.SetOnly(-1);
        w.SetHighlight(-1);
        w.Unlock();
        w.OpenFor(-1);
    }

    [MenuItem(Root + "+1000 монет %#m", false, 1041)]
    static void Coins1000() { if (InPlay()) AddCoins(1000); }

    static void AddCoins(int n)
    {
        var inv = Object.FindFirstObjectByType<PlayerInventory>();
        if (inv == null) { Debug.LogWarning("[Тест прокачки] PlayerInventory не найден."); return; }
        inv.AddCoins(n);
        var st = Object.FindFirstObjectByType<PlayerStats>();
        if (st != null) st.coins = inv.coinsCount;
    }

    [MenuItem(Root + "+1 свободное очко %#p", false, 1042)]
    static void FreePoint()
    {
        if (!InPlay()) return;
        if (PlayerUpgrades.Instance != null) PlayerUpgrades.Instance.DebugAddFreePoint();
    }

    [MenuItem(Root + "Уровень игрока/3", false, 1043)] static void Lvl3() { if (InPlay()) SetLevel(3); }
    [MenuItem(Root + "Уровень игрока/5", false, 1044)] static void Lvl5() { if (InPlay()) SetLevel(5); }
    [MenuItem(Root + "Уровень игрока/7", false, 1045)] static void Lvl7() { if (InPlay()) SetLevel(7); }
    [MenuItem(Root + "Уровень игрока/10 (максимум)", false, 1046)] static void Lvl10() { if (InPlay()) SetLevel(GameBalance.MaxLevel); }

    static void SetLevel(int level)
    {
        var st = Object.FindFirstObjectByType<PlayerStats>();
        if (st == null) { Debug.LogWarning("[Тест прокачки] PlayerStats не найден."); return; }
        level = Mathf.Clamp(level, 1, GameBalance.MaxLevel);
        st.currentLevel = level;
        st.currentXP = Mathf.Max(st.currentXP, GameBalance.XpForLevel(level));
        st.RefreshUI();
    }

    [MenuItem(Root + "Всё на максимум", false, 1047)]
    static void MaxAll()
    {
        if (!InPlay()) return;
        if (PlayerUpgrades.Instance != null) PlayerUpgrades.Instance.DebugMaxAll();
    }

    [MenuItem(Root + "Сбросить прокачку и таланты", false, 1048)]
    static void Reset()
    {
        if (!InPlay()) return;
        if (PlayerUpgrades.Instance != null) PlayerUpgrades.Instance.DebugReset();
    }

    [MenuItem(Root + "Открывать при старте", false, 1049)]
    static void ToggleAuto() { EditorPrefs.SetBool(AutoKey, !EditorPrefs.GetBool(AutoKey, false)); }

    [MenuItem(Root + "Открывать при старте", true)]
    static bool ToggleAutoCheck()
    {
        Menu.SetChecked(Root + "Открывать при старте", EditorPrefs.GetBool(AutoKey, false));
        return true;
    }
}
