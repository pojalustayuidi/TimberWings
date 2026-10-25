#if UNITY_EDITOR
// Assets/_Project/Editor/LocationBuilder.Outline.cs
// Секция окна «Контур острова и кромка» (задача 1). Вызывается одной строкой из OnGUI: DrawOutlineSection();
// Логика контура лежит в Features/LocationIslandOutline.cs.
using UnityEditor;
using UnityEngine;

public partial class LocationBuilder
{
    bool _fOutline;

    void DrawOutlineSection()
    {
        _fOutline = Fold(_fOutline, "Контур острова и кромка");
        if (!_fOutline) return;

        EditorGUILayout.LabelField("Кромка", EditorStyles.miniBoldLabel);
        _s.rimFlatWidth = EditorGUILayout.Slider("Ровная травяная кромка (u)", _s.rimFlatWidth, 0f, 6f);
        EditorGUILayout.HelpBox("Полоса у самого края, где земля идёт ровно (нужен рельеф «Выразительный»). 0 = как раньше. Действует после пересборки рельефа.", MessageType.None);

        EditorGUILayout.LabelField("Форма контура", EditorStyles.miniBoldLabel);
        _s.outlineRound = EditorGUILayout.Slider("Квадратность (2 круг .. 12 квадрат)", _s.outlineRound, 2f, 12f);
        _s.outlineBay = EditorGUILayout.Slider("Глубина бухт (u)", _s.outlineBay, 0f, 10f);
        _s.outlineCape = EditorGUILayout.Slider("Вынос мысов (u)", _s.outlineCape, 0f, 8f);

        EditorGUILayout.LabelField("Dock (смотровая и причал дирижаблей)", EditorStyles.miniBoldLabel);
        _s.dockPos = EditorGUILayout.Vector2Field("Центр (u)", _s.dockPos);
        _s.dockSize = EditorGUILayout.Vector2Field("Размер (u)", _s.dockSize);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Контур по раскладке", GUILayout.Height(24f))) { Save(); LocationIslandOutline.Rebuild(_s, true); }
        if (GUILayout.Button("Проверить контур", GUILayout.Height(24f))) { Save(); LocationIslandOutline.Check(_s); }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("Контур по раскладке заменяет точки PathPoints у FloatingIsland (старые уходят в выключенный PathPoints_old). Площадки, тропы и река не двигаются. После него жми «Собрать остров» у FloatingIsland.", MessageType.None);
    }
}
#endif
