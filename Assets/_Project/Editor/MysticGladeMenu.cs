// Assets/_Project/Editor/Forest/MysticGladeMenu.cs
// Compatibility menu: builds persistent editor-authored scenery, never a DontSave preview.
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

    [MenuItem("Tools/BuilderSim/Лес/Таинственная поляна/Собрать в сцене", false, 260)]
    static void Build() { IslandV2.TWMysticArt.Apply(); }

    [MenuItem("Tools/BuilderSim/Лес/Таинственная поляна/Откатить окружение", false, 261)]
    static void Clear() { IslandV2.TWMysticArt.Rollback(); }
}
