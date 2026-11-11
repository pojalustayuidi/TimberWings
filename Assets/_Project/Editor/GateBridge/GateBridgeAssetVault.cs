#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Unity does not retain a reliable asset reference to transient editor-created meshes
// after reopening a scene or producing a player build. Bake them as project assets.
internal static class GateBridgeAssetVault
{
    static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    static string folder;

    internal static bool Begin(GateBridge bridge, out string problem)
    {
        problem = null;
        string path = bridge.gameObject.scene.path;
        if (string.IsNullOrEmpty(path))
        {
            problem = "Сначала сохрани игровую сцену (Ctrl+S), затем запускай сборку: иначе меши некуда привязать.";
            return false;
        }
        string guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrEmpty(guid))
        {
            problem = "Не удалось найти GUID сцены: " + path;
            return false;
        }
        EnsureFolder("Assets/_Project", "Generated");
        EnsureFolder("Assets/_Project/Generated", "GateBridge");
        folder = "Assets/_Project/Generated/GateBridge/" + guid;
        EnsureFolder("Assets/_Project/Generated/GateBridge", guid);
        counts.Clear();
        return true;
    }

    static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
    }

    static string SafeName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "Unnamed";
        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
        return sb.ToString();
    }

    internal static Mesh Mesh(Mesh mesh)
    {
        if (mesh == null) return null;
        if (EditorUtility.IsPersistent(mesh)) return mesh;
        if (string.IsNullOrEmpty(folder)) throw new InvalidOperationException("GateBridgeAssetVault.Begin not called");
        string name = SafeName(mesh.name);
        counts.TryGetValue(name, out int index);
        counts[name] = index + 1;
        string path = folder + "/" + name + (index > 0 ? "_" + index : "") + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            Undo.RecordObject(existing, "Update baked bridge mesh");
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    internal static Material Material(Material material)
    {
        if (material == null || EditorUtility.IsPersistent(material)) return material;
        if (string.IsNullOrEmpty(folder)) throw new InvalidOperationException("GateBridgeAssetVault.Begin not called");
        string path = folder + "/" + SafeName(material.name) + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            Undo.RecordObject(existing, "Update baked bridge material");
            EditorUtility.CopySerialized(material, existing);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(material);
            return existing;
        }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    internal static BuildSiteData BridgeData()
    {
        if (string.IsNullOrEmpty(folder)) throw new InvalidOperationException("GateBridgeAssetVault.Begin not called");
        string path = folder + "/BuildSite_Bridge.asset";
        var data = AssetDatabase.LoadAssetAtPath<BuildSiteData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<BuildSiteData>();
            AssetDatabase.CreateAsset(data, path);
        }
        Undo.RecordObject(data, "Update bridge build stages");
        data.buildingName = "Мост";
        data.steps = BuildSiteData.BridgeSteps();
        EditorUtility.SetDirty(data);
        return data;
    }

    internal static void Finish()
    {
        AssetDatabase.SaveAssets();
        folder = null;
        counts.Clear();
    }
}
#endif
