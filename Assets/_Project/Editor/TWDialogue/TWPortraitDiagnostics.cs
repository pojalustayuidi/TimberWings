#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class TWPortraitDiagnostics
{
    [MenuItem("Tools/TimberWings/Dialogue Portraits/Repair-Verify")]
    public static void RepairAndVerify()
    {
        bool leo = FixPortrait("Leo");
        bool rico = FixPortrait("Rico");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        int views = Object.FindObjectsByType<DialoguePolishView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        int original = Object.FindObjectsByType<NpcDialoguePortraits>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        Debug.Log("[TW Portrait Fix] Assets: Leo=" + (leo ? "OK" : "MISSING") +
                  ", Rico=" + (rico ? "OK" : "MISSING") +
                  "; DialoguePolishView=" + views + "; NpcDialoguePortraits=" + original +
                  ". Runtime overlay attaches automatically when you enter Play Mode.");
        EditorUtility.DisplayDialog("TimberWings — Dialogue Portraits",
            "Leo: " + (leo ? "OK" : "NOT FOUND") + "\n" +
            "Rico: " + (rico ? "OK" : "NOT FOUND") + "\n" +
            "Dialogue windows in scene: " + views +
            "\n\nEnter Play Mode and start a dialogue with Leo or Rico.", "OK");
    }

    private static bool FixPortrait(string name)
    {
        string path = "Assets/_Project/Resources/DialoguePortraits/" + name + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("[TW Portrait Fix] Missing asset: " + path);
            return false;
        }
        bool change = importer.textureType != TextureImporterType.Sprite ||
                      importer.spriteImportMode != SpriteImportMode.Single ||
                      !importer.alphaIsTransparency || importer.mipmapEnabled ||
                      importer.textureCompression != TextureImporterCompression.Uncompressed;
        if (change)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        var result = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (result == null) Debug.LogError("[TW Portrait Fix] Sprite import failed: " + path);
        return result != null;
    }
}
#endif
