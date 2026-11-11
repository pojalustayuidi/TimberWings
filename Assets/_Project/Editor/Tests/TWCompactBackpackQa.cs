// Unity Editor QA: mathematical + live wiring checks. Never mutates game state.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TWCompactBackpackQa
{
    [MenuItem("Tools/BuilderSim/UI/Backpack/QA compact backpack")]
    public static void Run()
    {
        var issues = new List<string>();
        int[] capacities = { 4, 16, 21, 32, 48, 54, 100, 1000 };
        foreach (int cap in capacities)
        {
            for (int logs = 0; logs <= cap; logs++)
            {
                float ratio = Mathf.Clamp01(logs / (float)cap);
                float total = 0f;
                for (int i = 0; i < 4; i++)
                    total += Mathf.Clamp01(4f * ratio - i);
                if (Mathf.Abs(total / 4f - ratio) > 0.0001f)
                {
                    issues.Add("Ошибка заполнения " + logs + "/" + cap);
                    break;
                }
            }
            // Overfilled save: still displays the saved count but fills 100%.
            if (Mathf.Clamp01((cap + 5f) / cap) != 1f)
                issues.Add("Переполнение не ограничивается для " + cap);
        }

        if (AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Resources/TWBackpackOrange.png") == null)
            issues.Add("Нет Assets/_Project/Resources/TWBackpackOrange.png — оранжевая иконка не загрузится.");

        if (Application.isPlaying)
        {
            var inv = Object.FindObjectOfType<PlayerInventory>();
            var tag = Object.FindObjectOfType<BackpackTag>();
            var playerHead = Object.FindObjectOfType<PlayerHeadHud>();
            TWCompactBackpackHud visual = null;
            foreach (TWCompactBackpackHud candidate in Object.FindObjectsOfType<TWCompactBackpackHud>())
                if (candidate != null && candidate.IsReady) { visual = candidate; break; }
            if (inv == null) issues.Add("PlayerInventory не найден в сцене.");
            if (tag == null) issues.Add("BackpackTag не найден в сцене.");
            if (playerHead == null) issues.Add("PlayerHeadHud не найден в сцене.");
            if (visual == null) issues.Add("TWCompactBackpackHud ещё не построил плашку.");

            if (inv != null && tag != null)
            {
                if (tag.Capacity != inv.Capacity)
                    issues.Add("BackpackTag.Capacity расходится с PlayerInventory.Capacity.");
                if (tag.Carried != inv.woodCount)
                    issues.Add("BackpackTag.Carried расходится с количеством брёвен.");
                if (tag.Free != inv.FreeSlots)
                    issues.Add("Свободное место не учитывает pendingLogs одинаково в двух системах.");
                if (tag.IsFull != (inv.woodCount >= inv.Capacity))
                    issues.Add("Разные признаки полного рюкзака.");
            }
            if (inv != null && visual != null)
            {
                if (visual.DisplayedCarried != inv.woodCount || visual.DisplayedCapacity != inv.Capacity)
                    issues.Add("Компактный HUD показывает устаревшие игровые данные.");
                if (visual.DisplayedText != inv.woodCount + "/" + inv.Capacity)
                    issues.Add("Текст счётчика не соответствует инвентарю.");
                if (!visual.UsesOrangeIcon)
                    issues.Add("Новая оранжевая иконка не загрузилась через Resources.");
                if (visual.QuarterSlots != 4)
                    issues.Add("Должно быть ровно 4 деления.");
                // The fill is animated, so only validate its allowed range here.
                for (int i = 0; i < visual.QuarterSlots; i++)
                    if (visual.QuarterFill(i) < 0f || visual.QuarterFill(i) > 1f)
                        issues.Add("Недопустимое заполнение сегмента " + (i + 1));
            }
        }

        bool pass = issues.Count == 0;
        string result = pass
            ? (Application.isPlaying
                ? "PASS: инвентарь, вместимость, текст, иконка и 4 сегмента подключены."
                : "PASS: расчёты для 4, 16, 21, 32, 48, 54, 100, 1000 + PNG. В Play проверь подключение к игроку.")
            : string.Join("\n", issues.ToArray());
        if (pass) Debug.Log("[TW Backpack QA] " + result);
        else foreach (string issue in issues) Debug.LogError("[TW Backpack QA] " + issue);
        EditorUtility.DisplayDialog("TimberWings — рюкзак", result +
            "\n\nВнешний вид, анимации и работу катсцен проверь в Play Mode.", "OK");
    }
}
#endif
