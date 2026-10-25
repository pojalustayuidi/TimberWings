#if UNITY_EDITOR
// Assets/_Project/Editor/CandyWaterQualityMenu.cs
// Быстро переключить тир воды в редакторе, чтобы увидеть Low / Mid / High без сборки на телефон.
using UnityEditor;

public static class CandyWaterQualityMenu
{
    [MenuItem("Tools/Candy/Water quality/Low (вершинная маска)")]
    static void Low() { CandyWaterQuality.Apply(CandyWaterTier.Low); SceneView.RepaintAll(); }

    [MenuItem("Tools/Candy/Water quality/Mid (как раньше)")]
    static void Mid() { CandyWaterQuality.Apply(CandyWaterTier.Mid); SceneView.RepaintAll(); }

    [MenuItem("Tools/Candy/Water quality/High (глубина сцены)")]
    static void High() { CandyWaterQuality.Apply(CandyWaterTier.High); SceneView.RepaintAll(); }
}
#endif
