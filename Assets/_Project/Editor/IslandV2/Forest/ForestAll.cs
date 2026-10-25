#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/ForestAll.cs (Editor)
// A5 v2: одна кнопка на весь лес. Tools/BuilderSim/Лес/Красота леса/0. Всё сразу: Дедушка + лес + подлесок (приоритет 229).
// По порядку запускает готовые пункты:
//   1. серые заглушки леса блокаута (BLK_Trees и BLK_TreeColliders) выключаются, чтобы не было двух слоёв деревьев;
//   2. «4. Лес: расставить рабочие деревья» (большой рубимый лес рощами, ForestBackdrop + ForestGroves; фоновых Decor_* больше нет); в конце она сама собирает
//      зону рубки у PAD_GroveSouth (ChopZone: забор, арки, рабочие деревья группами, озеленение);
//      Тут же ставятся кулисные нерубимые деревья (ForestWings: 7 пастельных видов с венком из камней, перед рощами рубимых); отдельного шага им не нужно:
//      хранители (9) и лёгкие хранители (13) идут позже и сами убирают кулисы, задетые их кольцом.
//   2b. «8. Поляна рубки: уют и красота» (ForestGladeBeauty): поленница, лавки, фонари внутри забора;
//   2b2. «8b. Лагерь лесорубов» (ChopCamp): навес-пилорама, повозка с брёвнами, столбы с гирляндами и флажками, поленницы, лежащие брёвна;
//   2c. «9. Деревья-хранители: собрать и расставить» (ForestSacred): после леса и до подлеска;
//   2d. «13. Лёгкие хранители: кулисы» (ForestGuardLite): НОВЫЙ шаг, вставлен сразу после 9 (им нужны уже стоящие большие хранители и лес, чтобы чистить
//       полянки и не лезть к ним); порядок остальных шагов не менялся;
//   3. «1. Дедушка: собрать и поставить» (GrandpaTree, v2: вековое дерево с дуплом, цветением, фонариками, качелями);
//   3b. «7. Поляна Дедушки: лавки, фонари, уют» (GrandpaGrove): лавки, дорожка, фонари, флажки, пикник;
//   4. «6. Арка в лес + зона «Старый лес»» (ForestArch);
//   5. «2. Подлесок: кусты, цветы, камни» (ForestUnderstory): идёт последним, чтобы обойти новые деревья.
// Каждый шаг пересоздаёт только свой корень в IslandV2/_Manual, всё остальное в сцене не трогается. В конце одна сводка в Console.
// Заглушки блокаута «2. Собрать блокаут» включает обратно: после пересборки блокаута запусти эту кнопку ещё раз.
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static class ForestAll
    {
        const string Base = "Tools/BuilderSim/Лес/Красота леса/";

        [MenuItem("Tools/BuilderSim/Лес/Красота леса/0. Всё сразу: Дедушка + лес + подлесок", false, 229)]
        static void MenuRun() { Run(true); }

        // ask = false: без диалога подтверждения (так зовёт «Вся локация одной кнопкой», IslandAll.cs, у неё свой общий диалог)
        public static void Run(bool ask)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Лес: останови Play Mode."); return; }
            if (GameObject.Find("IslandV2") == null) { Debug.LogError("Лес: в сцене нет IslandV2 (пункт «2. Собрать блокаут»)."); return; }
            if (ask && !EditorUtility.DisplayDialog("Лес целиком",
                "Пересоздам в IslandV2/_Manual: ForestChop (деревья рощ и полян), ForestWings (кулисные деревья), ChopZone, ChopGladeDecor, ChopCamp, Grandpa, GrandpaGrove, ForestArch, ForestZone_Old, SacredTrees, GuardianLite, Understory. Твои ручные правки внутри них пропадут (Ctrl+Z откатывает).\nПродолжить?",
                "Расставить", "Отмена")) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            var sb = new StringBuilder("Лес целиком:\n");
            try
            {
                EditorUtility.DisplayProgressBar("Лес целиком", "Заглушки блокаута", 0.05f);
                sb.AppendLine("  заглушек леса отключено: " + HideStubs());

                // «4. Лес» сама собирает зону рубки (ChopZone: забор, арки, рабочие деревья группами, озеленение)
                EditorUtility.DisplayProgressBar("Лес целиком", "Большой лес и зона рубки", 0.20f);
                sb.AppendLine("  " + Step("4. Лес: расставить рабочие деревья", "ForestChop", "ChopZone", ForestWings.RootName));

                EditorUtility.DisplayProgressBar("Лес целиком", "Поляна рубки: уют", 0.45f);
                sb.AppendLine("  " + Step("8. Поляна рубки: уют и красота", "ChopGladeDecor"));

                EditorUtility.DisplayProgressBar("Лес целиком", "Лагерь лесорубов", 0.52f);
                sb.AppendLine("  " + Step("8b. Лагерь лесорубов: навес, повозка, флажки", "ChopCamp"));

                EditorUtility.DisplayProgressBar("Лес целиком", "Дедушка", 0.60f);
                sb.AppendLine("  " + Step("1. Дедушка: собрать и поставить", "Grandpa"));

                EditorUtility.DisplayProgressBar("Лес целиком", "Поляна Дедушки", 0.66f);
                sb.AppendLine("  " + Step("7. Поляна Дедушки: лавки, фонари, уют", "GrandpaGrove"));

                EditorUtility.DisplayProgressBar("Лес целиком", "Арка в лес", 0.72f);
                sb.AppendLine("  " + Step("6. Арка в лес + зона «Старый лес»", "ForestArch", "ForestZone_Old"));

                EditorUtility.DisplayProgressBar("Лес целиком", "Деревья-хранители", 0.78f);
                sb.AppendLine("  " + Step("9. Деревья-хранители: собрать и расставить", "SacredTrees"));

                EditorUtility.DisplayProgressBar("Лес целиком", "Лёгкие хранители", 0.84f);
                sb.AppendLine("  " + Step("13. Лёгкие хранители: кулисы", "GuardianLite"));

                EditorUtility.DisplayProgressBar("Лес целиком", "Подлесок", 0.90f);
                sb.AppendLine("  " + Step("2. Подлесок: кусты, цветы, камни", "Understory"));
            }
            finally { EditorUtility.ClearProgressBar(); }
            Undo.CollapseUndoOperations(group);

            sb.AppendLine("Дальше: «Остров v2 → 3. Проверить блокаут» (проходимость), «16. Проверка леса», потом правь руками. Подробности каждого шага выше в Console.");
            Debug.Log(sb.ToString());
        }

        // запускает пункт и проверяет, что его корни появились в сцене
        static string Step(string menu, params string[] expect)
        {
            // на время шага диалоги не нужны: пункты ниже их не показывают
            bool ok = EditorApplication.ExecuteMenuItem(Base + menu);
            if (!ok) return "[!] не нашёл пункт меню «" + menu + "» (скрипт не добавлен в проект или переименован)";
            var man = GameObject.Find("IslandV2/_Manual");
            var miss = new StringBuilder();
            foreach (var n in expect)
                if (man == null || man.transform.Find(n) == null) miss.Append(n + " ");
            return miss.Length == 0 ? "ок: «" + menu + "»" : "[!] «" + menu + "» отработал, но нет в _Manual: " + miss + "(смотри ошибку в Console)";
        }

        // серые лес-заглушки блокаута: меш и капсулы-коллайдеры
        static int HideStubs()
        {
            int n = 0;
            var gen = GameObject.Find("IslandV2/_Generated");
            if (gen == null) return 0;
            foreach (var name in new[] { "BLK_Trees", "BLK_TreeColliders" })
            {
                var t = gen.transform.Find(name);
                if (t == null || !t.gameObject.activeSelf) continue;
                Undo.RecordObject(t.gameObject, "Лес: выключить " + name);
                t.gameObject.SetActive(false);
                n++;
            }
            return n;
        }
    }
}
#endif
