// Persistent Candy Kit PNGs. No runtime drawing or scene rebuilding.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class CandyUiKitGenerator
{
    public const int Q = 2;
    public const string OutDir = "Assets/_Project/Art/UI/CandyKit";
    public static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>(StringComparer.Ordinal);

    static CandyUiKitGenerator()
    { foreach (var item in Catalog) Borders.Add(item.key, item.border); }

    public static bool HasPngSignature(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            using (var file = File.OpenRead(path))
            {
                byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
                foreach (byte value in signature) if (file.ReadByte() != value) return false;
            }
            return true;
        }
        catch (IOException) { return false; }
    }

    public static void ConfigureImporter(TextureImporter importer, Vector4 border)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f * Q;
        importer.spriteBorder = border;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = false;
        importer.maxTextureSize = 2048;
        // Keep pivots, sprite GUIDs and file IDs. FullRect is required for dependable nine slicing.
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
    }

    public static bool ImporterMatches(TextureImporter importer, Vector4 border)
    {
        if (importer == null) return false;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        return importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single &&
            Mathf.Approximately(importer.spritePixelsPerUnit, 100f * Q) && importer.spriteBorder == border &&
            importer.sRGBTexture && !importer.mipmapEnabled && importer.filterMode == FilterMode.Bilinear &&
            importer.wrapMode == TextureWrapMode.Clamp && importer.alphaIsTransparency &&
            importer.npotScale == TextureImporterNPOTScale.None && !importer.isReadable &&
            importer.textureCompression == TextureImporterCompression.Uncompressed && importer.maxTextureSize >= 1024 &&
            settings.spriteMeshType == SpriteMeshType.FullRect && !settings.spriteGenerateFallbackPhysicsShape;
    }

    [MenuItem("Tools/BuilderSim/UI/Candy Kit/Настроить импорт спрайтов набора", false, 200)]
    public static void GenerateAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("[CandyKit] Останови Play перед настройкой импорта."); return; }
        EnsureFunSprites(false); // Never redraw any existing PNG here.
        int updated = 0;
        try
        {
            for (int i = 0; i < Catalog.Length; i++)
            {
                var item = Catalog[i];
                if (!HasPngSignature(item.AssetPath)) continue;
                EditorUtility.DisplayProgressBar("Candy Kit · импорт", item.key, (float)i / Catalog.Length);
                var importer = AssetImporter.GetAtPath(item.AssetPath) as TextureImporter;
                if (importer == null)
                {
                    AssetDatabase.ImportAsset(item.AssetPath, ImportAssetOptions.ForceSynchronousImport);
                    importer = AssetImporter.GetAtPath(item.AssetPath) as TextureImporter;
                }
                if (importer == null || ImporterMatches(importer, item.border)) continue;
                ConfigureImporter(importer, item.border);
                importer.SaveAndReimport();
                updated++;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        Debug.Log("[CandyKit] Импорт настроен. Обновлено: " + updated + ". PNG, имена и ссылки сохранены.");
        RunAudit();
    }

    public static List<string> Audit()
    {
        var issues = new List<string>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in Catalog)
        {
            if (!names.Add(item.key)) issues.Add(item.key + ": повтор ключа в каталоге.");
            var b = item.border;
            if (b.x < 0 || b.y < 0 || b.z < 0 || b.w < 0 || b.x + b.z >= item.width || b.y + b.w >= item.height)
                issues.Add(item.key + ": некорректная рамка 9-slice.");
            if (!HasPngSignature(item.AssetPath))
            {
                issues.Add(item.key + ": нет готового PNG (возможно, указатель Git LFS). Скопируй PNG из архива.");
                continue;
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(item.AssetPath);
            if (sprite == null) { issues.Add(item.key + ": не импортирован как Sprite / Single."); continue; }
            if (sprite.rect.width != item.width || sprite.rect.height != item.height)
                issues.Add(item.key + ": размер " + sprite.rect.size + ", ожидается " + item.width + " × " + item.height + ".");
            if (!ImporterMatches(AssetImporter.GetAtPath(item.AssetPath) as TextureImporter, b))
                issues.Add(item.key + ": импорт отличается от каталога. Нажми «Настроить импорт».");
        }
        return issues;
    }

    [MenuItem("Tools/BuilderSim/UI/Candy Kit/Проверить набор (QA)", false, 203)]
    public static void RunAudit()
    {
        var issues = Audit();
        if (issues.Count == 0) Debug.Log("[CandyKit] QA OK: " + Catalog.Length + " спрайтов, размеры, 9-slice и импорт проверены.");
        else Debug.LogError("[CandyKit] QA: проблем " + issues.Count + "\n" + string.Join("\n", issues));
    }

    [MenuItem("Tools/BuilderSim/UI/Candy Kit/Показать папку в Project", false, 201)]
    public static void PingFolder()
    {
        var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(OutDir);
        if (folder == null) { Debug.LogWarning("[CandyKit] Сначала скопируй Assets из архива в проект."); return; }
        Selection.activeObject = folder;
        EditorGUIUtility.PingObject(folder);
    }
}

public class CandyKitImportPostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string prefix = CandyUiKitGenerator.OutDir + "/";
        if (!assetPath.StartsWith(prefix, StringComparison.Ordinal) || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;
        string key = assetPath.Substring(prefix.Length, assetPath.Length - prefix.Length - 4);
        var importer = (TextureImporter)assetImporter;
        if (key == "CandyKit_Sheet")
        {
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            return;
        }
        Vector4 border;
        if (CandyUiKitGenerator.Borders.TryGetValue(key, out border)) CandyUiKitGenerator.ConfigureImporter(importer, border);
    }
}
