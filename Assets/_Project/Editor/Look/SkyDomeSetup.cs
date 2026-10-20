#if UNITY_EDITOR
// Assets/_Project/Editor/Look/SkyDomeSetup.cs
// SkyDome ищет шейдер через Shader.Find. В готовой сборке такой шейдер вырезается, если на него никто не ссылается,
// поэтому добавляем его в Project Settings > Graphics > Always Included Shaders (один раз, само при загрузке редактора).
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SkyDomeSetup
{
    static SkyDomeSetup()
    {
        EditorApplication.delayCall += Ensure;
    }

    [MenuItem("Tools/BuilderSim/Небо: добавить шейдер в сборку")]
    static void Ensure()
    {
        var sh = Shader.Find("Project/SkyDome");
        if (sh == null) return;

        var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var arr = so.FindProperty("m_AlwaysIncludedShaders");
        if (arr == null) return;

        for (int i = 0; i < arr.arraySize; i++)
            if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) return;   // уже есть

        arr.arraySize++;
        arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log("[SkyDome] Шейдер Project/SkyDome добавлен в Always Included Shaders.");
    }
}
#endif
