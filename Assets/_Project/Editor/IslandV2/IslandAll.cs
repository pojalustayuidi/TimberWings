#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/IslandAll.cs (Editor)
// Остров v2: ОДНА КНОПКА НА ВСЮ ЛОКАЦИЮ.
// Меню: Tools/BuilderSim/Локация/Остров v2/0. ВСЯ ЛОКАЦИЯ одной кнопкой (приоритет 119).
//
// По порядку:
//   1. «2. Собрать блокаут» (IslandBlockout.Build): земля (Terrain-коллайдер + меш-чанки + текстура цвета Candy), коробки зданий, обрыв,
//      река с водой и водопадом, мост, площадка лагеря, метки;
//   2. весь лес (ForestAll.Run): рабочие деревья и рощи, зона рубки, Дедушка, арка, хранители, подлесок (после блокаута, потому что блокаут
//      пересоздаёт _Generated, а лес живёт в _Manual и опирается на уже стоящую землю);
//   3. «3. Проверить блокаут» (контракт и проходимость): итог в Console.
// Один общий диалог подтверждения. Лес внутри диалог не показывает. Отмена (Ctrl+Z): блокаут и лес откатываются двумя шагами (у каждого своя группа отмены).
// Если блокаут не собрался (нет шейдера, ошибки компиляции, контур пуст), лес не запускается, причина в Console.
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public static class IslandAll
    {
        const string CheckMenu = "Tools/BuilderSim/Локация/Остров v2/3. Проверить блокаут (контракт и проходимость)";

        [MenuItem("Tools/BuilderSim/Локация/Остров v2/0. ВСЯ ЛОКАЦИЯ одной кнопкой (блокаут + лес)", false, 119)]
        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("IslandV2: останови Play Mode."); return; }
            if (EditorApplication.isCompiling) { Debug.LogWarning("IslandV2: подожди конца компиляции скриптов и шейдеров и нажми ещё раз."); return; }
            if (!EditorUtility.DisplayDialog("Вся локация",
                "Соберу заново: IslandV2/_Generated (земля, обрыв, река, мост, площадка) и в IslandV2/_Manual весь лес (рощи, зона рубки, Дедушка, арка, хранители, подлесок).\n" +
                "Ручные правки внутри этих корней пропадут (Ctrl+Z откатывает). Может занять несколько минут.\nПродолжить?",
                "Собрать", "Отмена")) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            try
            {
                EditorUtility.DisplayProgressBar("Вся локация", "Блокаут: земля, река, мост, площадка", 0.05f);
                IslandBlockout.Build(IslandLayout.GetOrCreate());

                var root = IslandBlockout.FindRoot();
                var gen = root != null ? root.transform.Find(IslandBlockout.GeneratedName) : null;
                if (gen == null || gen.Find(IslandBlockout.TerrainName) == null)
                {
                    Debug.LogError("IslandV2: блокаут не собрался (нет IslandV2/_Generated/" + IslandBlockout.TerrainName + "), лес не запускаю. Причина выше в Console: " +
                                   "чаще всего не найден шейдер Project/IslandV2/VertexColor (ошибка компиляции шейдера) или пустой контур в раскладке.");
                    return;
                }

                EditorUtility.ClearProgressBar();   // у леса свой индикатор
                ForestAll.Run(false);

                EditorUtility.DisplayProgressBar("Вся локация", "Проверка блокаута", 0.95f);
                if (!EditorApplication.ExecuteMenuItem(CheckMenu))
                    Debug.LogWarning("IslandV2: не нашёл пункт «3. Проверить блокаут», пропускаю проверку.");
            }
            finally { EditorUtility.ClearProgressBar(); }
            Undo.CollapseUndoOperations(group);

            Debug.Log("IslandV2: вся локация собрана (блокаут + лес). Сохрани сцену (Ctrl+S). Итоги блокаута, леса и проверки: сообщения выше в Console.");
        }
    }
}
#endif
