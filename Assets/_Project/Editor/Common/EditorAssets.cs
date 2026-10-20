// Assets/_Project/Editor/Common/EditorAssets.cs (Editor)
// Общие помощники работы с ассетами для Editor-билдеров (раньше EnsureFolder был скопирован в 18 файлов).
// Подключение: using static EditorAssets;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class EditorAssets
{
    // Создаёт папку и все недостающие родительские ("Assets/A/B/C"). Если папка уже есть: ничего не делает.
    public static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // Список префабов <-> строка путей (для хранения в EditorPrefs).
    public static string PathsOf(GameObject[] a)
    {
        if (a == null) return "";
        var l = new List<string>();
        foreach (var g in a) l.Add(g != null ? AssetDatabase.GetAssetPath(g) : "");
        return string.Join("|", l);
    }

    public static GameObject[] FromPaths(string s)
    {
        if (string.IsNullOrEmpty(s)) return new GameObject[0];
        var parts = s.Split('|');
        var res = new GameObject[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            res[i] = string.IsNullOrEmpty(parts[i]) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(parts[i]);
        return res;
    }
}
