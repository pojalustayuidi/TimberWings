#if UNITY_EDITOR
// ISL-014: selective mesh-only refresh, without recreating prefabs, forest, colliders or scene objects.
// Source generators: TreeSpeciesMeshes / IslandV2.WingMeshes. Original mesh assets are backed up once.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ISL014TreeSilhouettes
{
    const string TreeDir = "Assets/_Project/Prefabs/Trees/Generated";
    const string WingDir = "Assets/_Project/Prefabs/Wings/Meshes";
    const string BackupDir = "Assets/_Project/Editor/ISL014_Backup";
    const string Menu = "Tools/BuilderSim/Лес/Деревья/ISL-014/";

    sealed class Entry
    {
        public string path;
        public string backup;
        public string name;
        public TreeSpecies species;
        public int variation;
        public int lod;
        public bool wing;
    }

    sealed class Prepared
    {
        public Entry entry;
        public Mesh original;
        public Mesh replacement;
        public int oldTris;
        public int newTris;
    }

    static List<Entry> Jobs()
    {
        var jobs = new List<Entry>();
        AddSpecies(jobs, TreeSpecies.Cedar, "Tree_Cedar", "ABCD");
        AddSpecies(jobs, TreeSpecies.Birch, "Tree_Birch", "ABCD");
        AddSpecies(jobs, TreeSpecies.Plain, "Tree_Random", "ABCD");
        AddSpecies(jobs, TreeSpecies.Oak, "Tree_Oak", "ABC");
        for (int v = 0; v < IslandV2.WingMeshes.Variants; v++)
            for (int lod = 0; lod < 2; lod++)
            {
                string id = "Wing_Cloud" + v + "_LOD" + lod;
                jobs.Add(new Entry
                {
                    wing = true, variation = v, lod = lod, name = id,
                    path = WingDir + "/" + id + ".asset",
                    backup = BackupDir + "/Wings/" + id + ".asset"
                });
            }
        return jobs;
    }

    static void AddSpecies(List<Entry> jobs, TreeSpecies sp, string name, string variants)
    {
        for (int v = 0; v < variants.Length; v++)
            for (int lod = 0; lod < 3; lod++)
            {
                string id = name + "_" + variants[v] + "_s" + (v + 1) + "_LOD" + lod;
                jobs.Add(new Entry
                {
                    species = sp, variation = v, lod = lod, name = id,
                    path = TreeDir + "/" + id + ".asset",
                    backup = BackupDir + "/Trees/" + id + ".asset"
                });
            }
    }

    static Mesh Generate(Entry entry)
    {
        if (entry.wing)
            return IslandV2.WingMeshes.Body(IslandV2.WingKind.Cloud, entry.variation, entry.lod, entry.name);
        return TreeSpeciesMeshes.BuildTree(entry.species, entry.variation, entry.variation + 1, entry.lod, entry.name);
    }

    static int Triangles(Mesh m)
    {
        int count = 0;
        for (int i = 0; i < m.subMeshCount; i++) count += (int)m.GetIndexCount(i) / 3;
        return count;
    }

    static bool ValidGeometry(Mesh oldMesh, Mesh newMesh, out string reason)
    {
        reason = null;
        if (newMesh == null || newMesh.vertexCount == 0) { reason = "пустой меш"; return false; }
        if (newMesh.subMeshCount != oldMesh.subMeshCount) { reason = "изменено количество submesh"; return false; }
        int before = Triangles(oldMesh), after = Triangles(newMesh);
        if (before == 0 || after > before) { reason = "возросло количество треугольников"; return false; }
        Vector3 a = oldMesh.bounds.size, b = newMesh.bounds.size;
        // Colliders and interaction ranges do not change. Avoid dramatic silhouette/bounds shifts.
        if (b.x > a.x * 1.15f + 0.01f || b.y > a.y * 1.15f + 0.01f || b.z > a.z * 1.15f + 0.01f)
        {
            reason = "габариты меша выросли более чем на 15%";
            return false;
        }
        return true;
    }

    static bool Prepare(List<Prepared> prepared, out string error)
    {
        error = null;
        foreach (Entry e in Jobs())
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(e.path);
            if (existing == null)
            {
                error = "Отсутствует Mesh-ассет: " + e.path + ". Команда не изменяет проект. Не запускай полную сборку автоматически.";
                return false;
            }
            Mesh next = Generate(e);
            prepared.Add(new Prepared { entry = e, original = existing, replacement = next, oldTris = Triangles(existing), newTris = Triangles(next) });
            string reason;
            if (!ValidGeometry(existing, next, out reason))
            {
                error = e.path + ": " + reason + ". Ничего не изменено.";
                return false;
            }
        }
        return true;
    }

    static void Dispose(List<Prepared> prepared)
    {
        foreach (var p in prepared) if (p.replacement != null) Object.DestroyImmediate(p.replacement);
        prepared.Clear();
    }

    static void EnsureFolder(string folder)
    {
        var parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string child = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(child)) AssetDatabase.CreateFolder(current, parts[i]);
            current = child;
        }
    }

    [MenuItem(Menu + "1. Проверить меши (без изменений)", false, 280)]
    static void Check()
    {
        var items = new List<Prepared>();
        try
        {
            string error;
            if (!Prepare(items, out error)) { Debug.LogError("ISL-014: " + error); return; }
            int before = 0, after = 0;
            foreach (var p in items) { before += p.oldTris; after += p.newTris; }
            Debug.Log("ISL-014: проверено " + items.Count + " Mesh-ассетов. Сумма треугольников в вариантах LOD (не кадре): " + before + " → " + after + ". Материалы, префабы, сцена и коллайдеры не менялись.");
        }
        catch (Exception ex) { Debug.LogError("ISL-014: проверка прервана: " + ex); }
        finally { Dispose(items); }
    }

    [MenuItem(Menu + "2. Применить силуэты (только меши)", false, 281)]
    static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("ISL-014: выйди из Play Mode."); return; }
        var items = new List<Prepared>();
        try
        {
            string error;
            if (!Prepare(items, out error)) { Debug.LogError("ISL-014: " + error); return; }
            // Back up ALL affected meshes before writing any. Never overwrite first-run originals.
            EnsureFolder(BackupDir + "/Trees");
            EnsureFolder(BackupDir + "/Wings");
            foreach (var p in items)
            {
                if (AssetDatabase.LoadAssetAtPath<Mesh>(p.entry.backup) == null &&
                    !AssetDatabase.CopyAsset(p.entry.path, p.entry.backup))
                    throw new InvalidOperationException("Не удалось сохранить резервную копию: " + p.entry.backup);
            }
            AssetDatabase.SaveAssets();
            foreach (var p in items)
            {
                p.original.Clear();
                EditorUtility.CopySerialized(p.replacement, p.original); // GUID and prefab references are unchanged.
                EditorUtility.SetDirty(p.original);
            }
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
            Debug.Log("ISL-014: обновлено " + items.Count + " Mesh-ассетов. Оригиналы: " + BackupDir + ". Лес, префабы, коллайдеры, материалы, LODGroup и позиции не пересоздавались.");
        }
        catch (Exception ex)
        {
            Debug.LogError("ISL-014: ошибка применения: " + ex + ". При необходимости запусти команду 3 (восстановление мешей).");
        }
        finally { Dispose(items); }
    }

    [MenuItem(Menu + "3. Восстановить исходные меши", false, 282)]
    static void Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("ISL-014: выйди из Play Mode."); return; }
        int restored = 0, missing = 0;
        foreach (var e in Jobs())
        {
            Mesh before = AssetDatabase.LoadAssetAtPath<Mesh>(e.backup);
            Mesh target = AssetDatabase.LoadAssetAtPath<Mesh>(e.path);
            if (before == null || target == null) { missing++; continue; }
            target.Clear();
            EditorUtility.CopySerialized(before, target);
            EditorUtility.SetDirty(target);
            restored++;
        }
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
        Debug.Log("ISL-014: восстановлено " + restored + " мешей, пропущено " + missing + ". Резервные копии сохранены. Для полного отката генератора также верни оба изменённых C# файла через Git.");
    }
}
#endif
