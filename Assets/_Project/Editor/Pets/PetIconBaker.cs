// Assets/_Project/Editor/Pets/PetIconBaker.cs (Editor)
// Запекает иконки питомцев из PetIconKit в PNG и сам назначает их в PetData.icon (по id вида: sheep, raccoon, bear...).
// Меню: Tools > BuilderSim > Питомцы > Нарисовать иконки (PNG и назначить) / Лист иконок (превью).
// Запекать не обязательно: PetIconKit.Get(id) рисует то же самое на лету. Но PNG быстрее грузятся и их можно править в графическом редакторе.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static EditorAssets;

public static class PetIconBaker
{
    const string Folder = "Assets/_Project/Art/Pets/Icons";

    [MenuItem("Tools/BuilderSim/Питомцы/Нарисовать иконки (PNG и назначить)", false, 620)]
    public static void BakeAndAssign()
    {
        EnsureFolder(Folder);
        List<PetData> pets = LoadAllPets();
        int saved = 0, assigned = 0;

        foreach (string id in PetIconKit.Ids)
        {
            Texture2D tex = PetIconKit.Render(id);
            string path = Folder + "/" + id + ".png";
            File.WriteAllBytes(AbsPath(path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = 100f * PetIconKit.Q;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.filterMode = FilterMode.Bilinear;
                imp.maxTextureSize = 512;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            saved++;

            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            for (int i = 0; i < pets.Count; i++)
            {
                if (Key(pets[i]) != id || pets[i].icon == sp) continue;
                pets[i].icon = sp;
                EditorUtility.SetDirty(pets[i]);
                assigned++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("PetIconBaker: нарисовано иконок " + saved + " (" + Folder + "), назначено в PetData: " + assigned +
                  (assigned == 0 ? ". Ни у одного PetData id не совпал с видом (" + string.Join(", ", PetIconKit.Ids) + ")." : "."));
    }

    // все иконки на одном листе по 4 в ряд на розовом фоне: удобно сравнить с референсом
    [MenuItem("Tools/BuilderSim/Питомцы/Лист иконок (превью)", false, 621)]
    public static void Sheet()
    {
        EnsureFolder(Folder);
        int s = PetIconKit.PixelSize, cols = 4, rows = (PetIconKit.Ids.Length + cols - 1) / cols;
        var sheet = new Texture2D(s * cols, s * rows, TextureFormat.RGBA32, false);
        var bg = new Color[sheet.width * sheet.height];
        for (int i = 0; i < bg.Length; i++) bg[i] = new Color(1f, 0f, 1f, 1f);
        sheet.SetPixels(bg);

        for (int n = 0; n < PetIconKit.Ids.Length; n++)
        {
            Texture2D t = PetIconKit.Render(PetIconKit.Ids[n]);
            Color[] src = t.GetPixels();
            Object.DestroyImmediate(t);
            int cx = (n % cols) * s, cy = (rows - 1 - n / cols) * s;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    Color c = src[y * s + x];
                    int i = (cy + y) * sheet.width + cx + x;
                    Color d = bg[i];
                    bg[i] = new Color(c.r * c.a + d.r * (1f - c.a), c.g * c.a + d.g * (1f - c.a), c.b * c.a + d.b * (1f - c.a), 1f);
                }
            }
        }
        sheet.SetPixels(bg);
        string path = Folder + "/_sheet.png";
        File.WriteAllBytes(AbsPath(path), sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(path));
        Debug.Log("PetIconBaker: лист иконок " + path);
    }

    // id вида из PetData: поле id, а если пусто, имя ассета (Pet_sheep -> sheep)
    static string Key(PetData p)
    {
        string k = string.IsNullOrEmpty(p.id) ? p.name : p.id;
        k = k.ToLowerInvariant();
        return k.StartsWith("pet_") ? k.Substring(4) : k;
    }

    static List<PetData> LoadAllPets()
    {
        var list = new List<PetData>();
        foreach (string guid in AssetDatabase.FindAssets("t:PetData"))
        {
            var p = AssetDatabase.LoadAssetAtPath<PetData>(AssetDatabase.GUIDToAssetPath(guid));
            if (p != null) list.Add(p);
        }
        return list;
    }

    static string AbsPath(string assetPath)
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
    }

}
