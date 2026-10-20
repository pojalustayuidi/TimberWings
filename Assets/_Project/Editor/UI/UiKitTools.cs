// Assets/_Project/Editor/UI/UiKitTools.cs   (в папке Editor)
// Tools > BuilderSim > UI Kit:
//   «Образец в сцене»          добавляет объект UiKitShowcase (показывается в Play)
//   «Экспорт спрайтов в PNG»   сохраняет весь кит в Assets/_Project/UI/Kit с правильным 9-slice и PPU, чтобы можно было
//                              открыть PNG, подкрасить в редакторе или использовать как обычные спрайты в префабах
using System.IO;
using UnityEditor;
using UnityEngine;

public static class UiKitTools
{
    const string Dir = "Assets/_Project/UI/Kit";

    [MenuItem("Tools/BuilderSim/UI Kit/Образец в сцене (нажми Play)")]
    static void Showcase()
    {
        var go = new GameObject("UiKitShowcase");
        go.AddComponent<UiKitShowcase>();
        Undo.RegisterCreatedObjectUndo(go, "UiKit Showcase");
        Selection.activeObject = go;
        Debug.Log("UiKitShowcase добавлен в сцену. Нажми Play: образец появится поверх экрана. Esc закрывает.");
    }

    [MenuItem("Tools/BuilderSim/UI Kit/Экспорт спрайтов в PNG")]
    static void Export()
    {
        Directory.CreateDirectory(Dir);
        UiKit.ClearCache();
        var all = UiKit.BuildAll();
        var paths = new System.Collections.Generic.List<string>();
        var sprites = new System.Collections.Generic.List<Sprite>();

        foreach (var kv in all)
        {
            string path = Dir + "/" + kv.Key + ".png";
            File.WriteAllBytes(path, kv.Value.texture.EncodeToPNG());
            paths.Add(path);
            sprites.Add(kv.Value);
        }
        AssetDatabase.Refresh();

        for (int i = 0; i < paths.Count; i++)
        {
            var imp = AssetImporter.GetAtPath(paths[i]) as TextureImporter;
            if (imp == null) continue;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = sprites[i].pixelsPerUnit;
            imp.spriteBorder = sprites[i].border;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }

        UiKit.ClearCache();   // спрайты из памяти больше не нужны, дальше можно брать PNG из проекта
        Debug.Log("UiKit: сохранено " + paths.Count + " спрайтов в " + Dir);
    }
}
