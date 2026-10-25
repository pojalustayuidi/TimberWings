// Assets/_Project/Editor/Forest/MysticGladeMenu.cs
// Превью зоны в Edit Mode: Tools > BuilderSim > Лес > Таинственная поляна. Превью в сцену не сохраняется (DontSave), деревья появляются только в Play Mode.
using UnityEditor;
using UnityEngine;

public static class MysticGladeMenu
{
    static MysticGladeBuilder Get(bool create)
    {
        var pad = GameObject.Find("PAD_Forest");
        if (pad == null) { Debug.LogWarning("MysticGlade: в сцене нет объекта PAD_Forest"); return null; }
        var b = pad.GetComponent<MysticGladeBuilder>();
        if (b == null && create) b = Undo.AddComponent<MysticGladeBuilder>(pad);
        return b;
    }

    [MenuItem("Tools/BuilderSim/Лес/Таинственная поляна/Собрать превью", false, 260)]
    static void Build() { var b = Get(true); if (b != null) b.Build(); }

    [MenuItem("Tools/BuilderSim/Лес/Таинственная поляна/Убрать превью", false, 261)]
    static void Clear() { var b = Get(false); if (b != null) b.Clear(); }
}
