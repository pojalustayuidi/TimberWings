#if UNITY_EDITOR
// Assets/_Project/Editor/Buildings/BuildSiteNormalizer.cs
// Меню: Tools > BuilderSim > Привести стройки к единому виду
// Зоны сдачи в сцене собирались разными инструментами (BuildZone у костра, DropZone у палатки), и вели себя по-разному.
// Это меню приводит их к одному набору и пишет отчёт:
//   * у каждой зоны есть триггер-коллайдер и метка BuildSiteTagView + подсветка;
//   * completesTutorial включён только у костра (зона рядом с CAMPFIRE_Point), у остальных выключен;
//   * у костра есть Build Site Data ровно с двумя этапами (4 + 4 бревна: стадии 2 и 3, лишней 4-й стадии нет), обучение завершается после второго;
//   * «пустые» зоны (нет ни данных, ни стадий) закрываются: пока их не настроили, они не принимают брёвна.
// Ничего не удаляет. Откат: Ctrl+Z.
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildSiteNormalizer
{
    const string DataFolder = "Assets/_Game/BuildSites";

    [MenuItem("Tools/BuilderSim/Привести стройки к единому виду")]
    static void Run()
    {
        var sb = new StringBuilder("=== Стройки ===\n");
        int changes = 0;

        var all = Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all.Length == 0)
        {
            EditorUtility.DisplayDialog("Стройки", "В сцене нет ни одной зоны сдачи (BuildingController).", "OK");
            return;
        }

        var camp = FindCampfire(all);
        sb.AppendLine(camp != null ? "Костёр: «" + camp.name + "» (родитель: " + ParentName(camp) + ")" : "! Костёр не найден: нет CAMPFIRE_Point рядом с зоной сдачи.");

        foreach (var bc in all)
        {
            bool isCamp = bc == camp;
            sb.AppendLine("— «" + bc.name + "» (родитель: " + ParentName(bc) + ")" + (isCamp ? "  [КОСТЁР]" : ""));

            // 1. триггер
            var cols = bc.GetComponents<Collider>();
            if (cols.Length == 0)
            {
                var box = Undo.AddComponent<BoxCollider>(bc.gameObject);
                box.isTrigger = true;
                box.size = new Vector3(4f, 3f, 4f);
                box.center = new Vector3(0f, 1.5f, 0f);
                sb.AppendLine("    + BoxCollider (триггер)");
                changes++;
            }
            else
            {
                foreach (var c in cols)
                    if (!c.isTrigger)
                    {
                        Undo.RecordObject(c, "Trigger");
                        c.isTrigger = true;
                        sb.AppendLine("    ~ коллайдер стал триггером");
                        changes++;
                    }
            }

            // 2. завершение обучения только у костра
            if (bc.completesTutorial != isCamp)
            {
                Undo.RecordObject(bc, "completesTutorial");
                bc.completesTutorial = isCamp;
                sb.AppendLine("    ~ completesTutorial = " + isCamp);
                changes++;
            }

            // 3. метка и подсветка
            var view = bc.GetComponent<BuildSiteTagView>();
            if (view == null)
            {
                view = Undo.AddComponent<BuildSiteTagView>(bc.gameObject);
                view.buildingName = isCamp ? "Костёр" : GuessName(bc);
                sb.AppendLine("    + BuildSiteTagView («" + view.buildingName + "»)");
                changes++;
            }
            if (bc.GetComponent<BuildSiteHighlighter>() == null)
            {
                Undo.AddComponent<BuildSiteHighlighter>(bc.gameObject);
                sb.AppendLine("    + BuildSiteHighlighter");
                changes++;
            }

            // 4. данные
            if (isCamp)
            {
                changes += FixCampfireData(bc, sb);
                if (bc.tutorialStages != GameBalance.CampfireSteps)
                {
                    Undo.RecordObject(bc, "tutorialStages");
                    bc.tutorialStages = GameBalance.CampfireSteps;
                    sb.AppendLine("    ~ tutorialStages = " + GameBalance.CampfireSteps + " (вступление ждёт обе стадии костра)");
                    changes++;
                }
            }
            else if (!bc.storyManaged && bc.data == null && bc.stages == null && bc.GetComponentInParent<HutStages>() == null)
            {
                // заглушка: ни данных, ни стадий. Не даём ей принимать брёвна, пока её не настроят
                if (!bc.locked)
                {
                    Undo.RecordObject(bc, "lock stub");
                    bc.locked = true;
                    sb.AppendLine("    ~ заглушка без данных закрыта (locked). Настройте Data и снимите галочку, когда будет нужна");
                    changes++;
                }
                if (view != null && view.lockedAfter == "Костёр")
                {
                    Undo.RecordObject(view, "lockedAfter");
                    view.lockedAfter = GuessName(bc);
                    changes++;
                }
            }
        }

        if (changes > 0)
        {
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        sb.AppendLine(changes == 0 ? "Менять нечего." : "Изменений: " + changes + ". Сохраните сцену (Ctrl+S).");
        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("Стройки", sb.ToString(), "OK");
    }

    // ---------------------------------------------------------------- костёр
    static BuildingController FindCampfire(BuildingController[] all)
    {
        var cp = GameObject.Find("CAMPFIRE_Point");
        if (cp != null)
        {
            var b = cp.GetComponentInParent<BuildingController>();
            if (b == null) b = cp.GetComponentInChildren<BuildingController>(true);
            for (var t = cp.transform.parent; b == null && t != null; t = t.parent)
                b = t.GetComponentInChildren<BuildingController>(true);
            if (b != null) return b;
        }
        foreach (var bc in all)
            if (bc.data != null && bc.data.name.ToLower().Contains("campfire")) return bc;
        return null;
    }

    static int FixCampfireData(BuildingController bc, StringBuilder sb)
    {
        int changes = 0;

        if (bc.data == null)
        {
            Directory.CreateDirectory(DataFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath(DataFolder + "/BuildSite_Campfire.asset");
            var d = ScriptableObject.CreateInstance<BuildSiteData>();
            d.buildingName = "Костёр";
            AssetDatabase.CreateAsset(d, path);
            Undo.RecordObject(bc, "Campfire data");
            bc.data = d;
            sb.AppendLine("    + Build Site Data: " + path);
            changes++;
        }

        var data = bc.data;
        int wood = 0;
        if (data.steps != null) foreach (var s in data.steps) wood += s.woodCost;

        // этапов не два (например, лишний 3-й = «4-я стадия») или нулевая цена: пересобираем 4 + 4 бревна (GDD: костёр = 4 дерева = 8 бревен)
        if (data.steps == null || data.steps.Length != GameBalance.CampfireSteps || wood == 0)
        {
            Undo.RecordObject(data, "Campfire steps");
            data.buildingName = "Костёр";
            data.startStage = 1;
            data.steps = new[]
            {
                new BuildSiteData.Step { name = "Каменный круг и дрова", woodCost = GameBalance.StartBackpack, buildTime = 2.5f, rewardXp = GameBalance.CampfireStepXp[0] },
                new BuildSiteData.Step { name = "Разжечь костёр", woodCost = GameBalance.StartBackpack, buildTime = 2.5f, rewardXp = GameBalance.CampfireStepXp[1] }
            };
            EditorUtility.SetDirty(data);
            sb.AppendLine("    ~ этапы костра: 4 + 4 бревна (стадии 2 и 3), лишних стадий нет");
            changes++;
        }
        else
        {
            // цену не трогаем, награду выравниваем: два этапа дают 45 XP, вместе с рубкой хватает на 2-й уровень
            for (int i = 0; i < data.steps.Length; i++)
                if (data.steps[i].rewardXp != GameBalance.CampfireStepXp[i])
                {
                    Undo.RecordObject(data, "Campfire xp");
                    data.steps[i].rewardXp = GameBalance.CampfireStepXp[i];
                    EditorUtility.SetDirty(data);
                    sb.AppendLine("    ~ XP этапа " + (i + 1) + " = " + GameBalance.CampfireStepXp[i]);
                    changes++;
                }
        }
        return changes;
    }

    static string ParentName(Component c) { return c.transform.parent != null ? c.transform.parent.name : "—"; }

    static string GuessName(BuildingController bc)
    {
        if (bc.data != null && !string.IsNullOrEmpty(bc.data.buildingName)) return bc.data.buildingName;
        return bc.transform.parent != null ? bc.transform.parent.name : bc.name;
    }
}
#endif
