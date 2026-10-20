#if UNITY_EDITOR
// Assets/_Project/Scripts/UI/HudGainMenu.cs
// ПКМ по панели в Hierarchy -> HUD Gain -> «Это панель ...». Вешает HudGainAnchor с нужным Kind.
using UnityEditor;
using UnityEngine;

public static class HudGainMenu
{
    const string P = "GameObject/HUD Gain/";

    [MenuItem(P + "Это панель БРЁВЕН (дерево)", false, 30)]
    static void Wood() { Mark(HudGain.Kind.Wood); }

    [MenuItem(P + "Это панель УРОВНЯ (XP)", false, 31)]
    static void Xp() { Mark(HudGain.Kind.Xp); }

    [MenuItem(P + "Это панель МОНЕТ", false, 32)]
    static void Coin() { Mark(HudGain.Kind.Coin); }

    [MenuItem(P + "Это панель БРЁВЕН (дерево)", true)]
    [MenuItem(P + "Это панель УРОВНЯ (XP)", true)]
    [MenuItem(P + "Это панель МОНЕТ", true)]
    static bool Validate()
    {
        var go = Selection.activeGameObject;
        return go != null && go.GetComponent<RectTransform>() != null;
    }

    static void Mark(HudGain.Kind kind)
    {
        var go = Selection.activeGameObject;
        if (go == null) return;
        var a = go.GetComponent<HudGainAnchor>();
        if (a == null) a = Undo.AddComponent<HudGainAnchor>(go);
        Undo.RecordObject(a, "HUD Gain");
        a.kind = kind;
        EditorUtility.SetDirty(a);
        Debug.Log("[HudGain] " + go.name + " -> " + kind);
    }
}
#endif
