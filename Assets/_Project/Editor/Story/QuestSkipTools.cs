// Assets/_Project/Editor/Story/QuestSkipTools.cs (Editor)
// Пропуск квестов и быстрый показ окон. Меню: Tools > BuilderSim > Тест квестов. Работает только в Play Mode.
//   Пропустить вступление        Skip() у IntroFlow (WASD, Мира, костёр)
//   Пропустить текущую главу     Ctrl+Shift+K   засчитывает главу: награда, открытие зданий и окна прокачки
//   Пропустить все главы         то же для всех оставшихся
//   Открыть пилораму             Ctrl+Shift+W   открывает пилу, кладёт бревна в рюкзак, ставит игрока рядом и открывает окно
//   +10 брёвен / +10 досок
//   Открыть всё сразу            вступление и главы пропущены, пила открыта, прокачка открыта с монетами и очками
// Стройки (хижина, рынок, палатка) этот инструмент не достраивает: пропускаются квесты, а не мир.
using UnityEditor;
using UnityEngine;

public static class QuestSkipTools
{
    const string Root = "Tools/BuilderSim/Тест квестов/";

    static bool InPlay()
    {
        if (Application.isPlaying) return true;
        Debug.LogWarning("[Тест квестов] Сначала нажми Play.");
        return false;
    }

    [MenuItem(Root + "Пропустить вступление")]
    static void SkipIntro()
    {
        if (!InPlay()) return;
        var intro = IntroFlow.Instance != null ? IntroFlow.Instance : Object.FindFirstObjectByType<IntroFlow>(FindObjectsInactive.Include);
        if (intro == null) { Debug.LogWarning("[Тест квестов] IntroFlow не найден."); return; }
        intro.Skip();
    }

    [MenuItem(Root + "Пропустить текущую главу %#k")]
    static void SkipChapter()
    {
        if (!InPlay()) return;
        if (StoryFlow.Instance == null) { Debug.LogWarning("[Тест квестов] StoryFlow не найден."); return; }
        StoryFlow.Instance.DebugSkipChapter();
    }

    [MenuItem(Root + "Пропустить все главы")]
    static void SkipAll()
    {
        if (!InPlay()) return;
        if (StoryFlow.Instance == null) { Debug.LogWarning("[Тест квестов] StoryFlow не найден."); return; }
        StoryFlow.Instance.DebugSkipAll();
    }

    [MenuItem(Root + "Открыть пилораму %#w")]
    static void OpenSawmill()
    {
        if (!InPlay()) return;
        var ws = Object.FindFirstObjectByType<PlankWorkshop>(FindObjectsInactive.Include);
        if (ws == null) { Debug.LogWarning("[Тест квестов] PlankWorkshop (пила) не найдена в сцене."); return; }
        ws.gameObject.SetActive(true);
        ws.unlocked = true;

        var inv = Object.FindFirstObjectByType<PlayerInventory>();
        if (inv != null && inv.woodCount < 10) inv.AddWood(10 - inv.woodCount);

        // окно закрывается, если игрок дальше 1.5 радиуса от пилы: ставим рядом
        var mv = Object.FindFirstObjectByType<PlayerMovement>();
        if (mv != null && ws.DistanceToPlayer() > ws.radius)
        {
            var cc = mv.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            mv.transform.position = ws.transform.position + ws.transform.right * 2f;
            if (cc != null) cc.enabled = true;
        }

        var win = PlankWorkshopWindow.Instance;
        if (win != null && win.IsOpen) return;
        PlankWorkshopWindow.Toggle(ws);
    }

    [MenuItem(Root + "+10 брёвен")]
    static void Logs()
    {
        if (!InPlay()) return;
        var inv = Object.FindFirstObjectByType<PlayerInventory>();
        if (inv != null) inv.AddWood(10);
    }

    [MenuItem(Root + "+10 досок")]
    static void Planks()
    {
        if (!InPlay()) return;
        var inv = Object.FindFirstObjectByType<PlayerInventory>();
        if (inv != null) inv.AddPlanks(10);
    }

    [MenuItem(Root + "Открыть всё сразу")]
    static void Everything()
    {
        if (!InPlay()) return;
        SkipIntro();
        SkipAll();
        var ws = Object.FindFirstObjectByType<PlankWorkshop>(FindObjectsInactive.Include);
        if (ws != null) { ws.gameObject.SetActive(true); ws.unlocked = true; }
        var uw = UpgradeWindow.Instance;
        if (uw != null) { uw.SetOnly(-1); uw.SetHighlight(-1); uw.Unlock(); }
        Debug.Log("[Тест квестов] Квесты пропущены. Прокачка: Ctrl+Shift+U, пилорама: Ctrl+Shift+W.");
    }
}
