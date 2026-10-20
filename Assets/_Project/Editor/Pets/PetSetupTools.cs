// Assets/_Project/Editor/Pets/PetSetupTools.cs (Editor)
// Задача 3: заводит 8 видов питомцев, общую базу и ставит PetManager на сцену.
// Меню: Tools > BuilderSim > Питомцы > Создать виды / Поставить PetManager на сцену.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static EditorAssets;

public static class PetSetupTools
{
    const string PetsFolder = "Assets/_Project/Data/Pets";
    const string DatabasePath = "Assets/_Project/Data/PetDatabase.asset";
    const string ArtFolder = "Assets/_Project/Art/Pets";
    const string PrefabFolder = "Assets/_Project/Prefabs/Pets";

    struct Species
    {
        public string id, name;
        public PetRarity rarity;
        public PetStat stat;
        public Species(string id, string name, PetRarity rarity, PetStat stat)
        {
            this.id = id; this.name = name; this.rarity = rarity; this.stat = stat;
        }
    }

    // Таблица видов (редкость и характеристика закреплены за видом)
    static readonly Species[] Table =
    {
        new Species("chicken", "Курица", PetRarity.Common,    PetStat.DoubleYield),
        new Species("sheep",   "Овца",   PetRarity.Common,    PetStat.Capacity),
        new Species("pig",     "Свинья", PetRarity.Common,    PetStat.Haggle),
        new Species("dog",     "Собака", PetRarity.Uncommon,  PetStat.ChopPower),
        new Species("cat",     "Кошка",  PetRarity.Uncommon,  PetStat.SawSpeed),
        new Species("raccoon", "Енот",   PetRarity.Rare,      PetStat.DoubleYield),
        new Species("horse",   "Лошадь", PetRarity.Rare,      PetStat.MoveSpeed),
        new Species("wolf",    "Волк",   PetRarity.Legendary, PetStat.WorkerIncome),
    };

    // ---------------------------------------------------------------- 1) Создать виды

    [MenuItem("Tools/BuilderSim/Питомцы/Создать виды")]
    public static void CreateSpecies()
    {
        var log = new StringBuilder();
        int created = 0, updated = 0;
        var noPrefab = new List<string>();

        EnsureFolder(PetsFolder);
        EnsureFolder("Assets/_Project/Data");

        var all = new List<PetData>();
        for (int i = 0; i < Table.Length; i++)
        {
            Species s = Table[i];
            PetData pet = FindExistingPet(s.id);
            bool isNew = false;
            if (pet == null)
            {
                pet = ScriptableObject.CreateInstance<PetData>();
                pet.id = s.id;
                pet.displayName = s.name;
                pet.rarity = s.rarity;
                pet.stat = s.stat;
                string path = AssetDatabase.GenerateUniqueAssetPath(PetsFolder + "/Pet_" + s.id + ".asset");
                AssetDatabase.CreateAsset(pet, path);
                isNew = true;
                created++;
            }

            bool changed = false;
            // id и имя: не затираем, если уже заполнены игроком
            if (string.IsNullOrEmpty(pet.id)) { pet.id = s.id; changed = true; }
            if (string.IsNullOrEmpty(pet.displayName) || pet.displayName == "Питомец") { pet.displayName = s.name; changed = true; }
            // редкость и характеристика закреплены за видом: приводим к таблице
            if (!isNew && pet.rarity != s.rarity)
            {
                log.AppendLine("  " + s.id + ": редкость " + pet.rarity + " -> " + s.rarity + " (по таблице)");
                pet.rarity = s.rarity; changed = true;
            }
            if (!isNew && pet.stat != s.stat)
            {
                log.AppendLine("  " + s.id + ": характеристика " + pet.stat + " -> " + s.stat + " (по таблице)");
                pet.stat = s.stat; changed = true;
            }
            // префаб: только если поле пустое
            if (pet.prefab == null)
            {
                GameObject found = FindModelFor(s.id);
                if (found != null) { pet.prefab = found; changed = true; }
                else noPrefab.Add(s.id);
            }

            if (changed) EditorUtility.SetDirty(pet);
            if (changed && !isNew) updated++;
            all.Add(pet);
        }

        // база
        PetDatabase db = AssetDatabase.LoadAssetAtPath<PetDatabase>(DatabasePath);
        if (db == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:PetDatabase");
            if (guids.Length > 0)
            {
                string other = AssetDatabase.GUIDToAssetPath(guids[0]);
                db = AssetDatabase.LoadAssetAtPath<PetDatabase>(other);
                log.AppendLine("  Найдена база в другом месте, использую её: " + other);
            }
        }
        bool dbCreated = false;
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<PetDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
            dbCreated = true;
        }
        if (db.pets == null) db.pets = new List<PetData>();
        int removedNull = db.pets.RemoveAll(p => p == null);
        int added = 0;
        for (int i = 0; i < all.Count; i++)
        {
            if (!db.pets.Contains(all[i])) { db.pets.Add(all[i]); added++; }
        }
        EditorUtility.SetDirty(db);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var sb = new StringBuilder();
        sb.AppendLine("[Питомцы] Создать виды: готово.");
        sb.AppendLine("  Виды: создано " + created + ", обновлено " + updated + ", всего " + all.Count + " (папка " + PetsFolder + ").");
        sb.AppendLine("  База: " + (dbCreated ? "создана" : "найдена") + " (" + AssetDatabase.GetAssetPath(db) + "), добавлено видов " + added
            + (removedNull > 0 ? ", убрано пустых ссылок " + removedNull : "") + ", в базе " + db.pets.Count + ".");
        sb.Append(log.ToString());
        if (noPrefab.Count > 0)
            sb.AppendLine("  Без префаба (модель не найдена в " + ArtFolder + " и " + PrefabFolder + "): " + string.Join(", ", noPrefab.ToArray()) + ". Это нормально до задачи 9.");
        else
            sb.AppendLine("  Префабы найдены для всех видов.");
        Debug.Log(sb.ToString());

        Selection.activeObject = db;
        EditorGUIUtility.PingObject(db);
    }

    // ---------------------------------------------------------------- 2) PetManager на сцену

    [MenuItem("Tools/BuilderSim/Питомцы/Поставить PetManager на сцену")]
    public static void PlaceManager()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[Питомцы] Останови Play и повтори: менеджер ставится только в режиме редактирования.");
            return;
        }

        PetDatabase db = AssetDatabase.LoadAssetAtPath<PetDatabase>(DatabasePath);
        if (db == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:PetDatabase");
            if (guids.Length > 0) db = AssetDatabase.LoadAssetAtPath<PetDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        if (db == null)
        {
            Debug.Log("[Питомцы] Базы ещё нет, сначала создаю виды.");
            CreateSpecies();
            db = AssetDatabase.LoadAssetAtPath<PetDatabase>(DatabasePath);
        }
        if (db == null)
        {
            Debug.LogError("[Питомцы] Не удалось найти или создать PetDatabase. Запусти «Создать виды» и смотри Console.");
            return;
        }

        PetManager mgr = UnityEngine.Object.FindFirstObjectByType<PetManager>();
        string report;
        if (mgr == null)
        {
            var go = new GameObject("PetManager");
            Undo.RegisterCreatedObjectUndo(go, "Создать PetManager");
            mgr = go.AddComponent<PetManager>();
            SetDatabase(mgr, db);
            report = "создан объект PetManager, база подставлена";
        }
        else if (mgr.database == null)
        {
            SetDatabase(mgr, db);
            report = "PetManager уже был («" + mgr.name + "»), подставлена пустая база";
        }
        else
        {
            report = "PetManager уже стоит («" + mgr.name + "»), база уже задана (" + mgr.database.name + "), ничего не менял";
        }

        EditorUtility.SetDirty(mgr);
        EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);
        Selection.activeGameObject = mgr.gameObject;

        Debug.Log("[Питомцы] Поставить PetManager: " + report + ". Сцена: «" + mgr.gameObject.scene.name + "» (сохрани её Ctrl+S). Видов в базе: " + db.pets.Count + ".");
    }

    static void SetDatabase(PetManager mgr, PetDatabase db)
    {
        Undo.RecordObject(mgr, "PetManager: база");
        var so = new SerializedObject(mgr);
        SerializedProperty p = so.FindProperty("database");
        if (p != null)
        {
            p.objectReferenceValue = db;
            so.ApplyModifiedProperties();
        }
        else
        {
            mgr.database = db;
        }
    }

    // ---------------------------------------------------------------- вспомогательное

    // Ищет уже существующий PetData с таким id (по имени файла или по полю id), чтобы не плодить дубли
    static PetData FindExistingPet(string id)
    {
        string[] guids = AssetDatabase.FindAssets("t:PetData");
        PetData byName = null;
        for (int i = 0; i < guids.Length; i++)
        {
            PetData p = AssetDatabase.LoadAssetAtPath<PetData>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (p == null) continue;
            if (p.id == id) return p;
            if (byName == null && string.Equals(p.name, "Pet_" + id, StringComparison.OrdinalIgnoreCase)) byName = p;
        }
        return byName;
    }

    // Модель или префаб, в имени которого есть id (без учёта регистра). Сначала префабы, потом fbx и прочее.
    static GameObject FindModelFor(string id)
    {
        var folders = new List<string>();
        if (AssetDatabase.IsValidFolder(PrefabFolder)) folders.Add(PrefabFolder);
        if (AssetDatabase.IsValidFolder(ArtFolder)) folders.Add(ArtFolder);
        if (folders.Count == 0) return null;

        string[] guids = AssetDatabase.FindAssets("t:GameObject", folders.ToArray());
        GameObject best = null;
        int bestScore = int.MaxValue;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string file = Path.GetFileNameWithoutExtension(path);
            if (file.IndexOf(id, StringComparison.OrdinalIgnoreCase) < 0) continue;

            int score = 0;
            if (!path.StartsWith(PrefabFolder, StringComparison.Ordinal)) score += 10;      // префабы лучше сырых моделей
            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) score += 5;
            if (!string.Equals(file, id, StringComparison.OrdinalIgnoreCase)) score += 1;    // точное имя лучше частичного
            score += Mathf.Min(file.Length, 50) / 50;

            if (score < bestScore)
            {
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) { best = go; bestScore = score; }
            }
        }
        return best;
    }

}
