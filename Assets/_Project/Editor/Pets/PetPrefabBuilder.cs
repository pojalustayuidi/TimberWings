// Assets/_Project/Editor/Pets/PetPrefabBuilder.cs (Editor)
// Собирает префабы питомцев из FBX в Assets/_Project/Models/Animals/FBX и назначает их в PetData.prefab.
// Меню:
//   Tools > BuilderSim > Питомцы > Собрать префабы (только недостающие)   существующие префабы НЕ трогает, твои правки целы
//   Tools > BuilderSim > Питомцы > Пересобрать все префабы (сотрёт правки) пересоздаёт всё с нуля
// Что делает для вида: находит FBX, включает цикл у клипов Idle/Run/Walk, приводит модель к нужной высоте
// (pivot у ног), кладёт PetFollower на корень и заполняет в нём Idle Clip / Move Clip.
// В консоли пишет список клипов каждой модели: если выбраны не те, просто перетащи нужные в PetFollower на префабе.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using static EditorAssets;

public static class PetPrefabBuilder
{
    const string FbxFolder = "Assets/_Project/Models/Animals/FBX";
    const string PrefabFolder = "Assets/_Project/Prefabs/Pets";

    struct Species
    {
        public string id;
        public string[] names;     // возможные имена FBX (без учёта регистра): сначала точные, потом по началу имени
        public float height;       // высота модели в метрах
        public Species(string id, float height, params string[] names)
        {
            this.id = id; this.height = height; this.names = names;
        }
    }

    // id совпадает с таблицей PetSetupTools. Высоты подбирай под масштаб своего игрока.
    static readonly Species[] Table =
    {
        new Species("chicken", 0.55f, "chicken", "chick"),
        new Species("sheep",   0.95f, "sheep"),
        new Species("pig",     0.75f, "pig"),
        new Species("dog",     0.80f, "dog"),
        new Species("cat",     0.55f, "cat"),
        new Species("raccoon", 0.60f, "raccoon", "racc"),
        new Species("horse",   1.60f, "horse"),
        new Species("wolf",    0.95f, "wolf"),
    };

    [MenuItem("Tools/BuilderSim/Питомцы/Собрать префабы (только недостающие)", false, 610)]
    public static void BuildMissing() { BuildAll(false); }

    [MenuItem("Tools/BuilderSim/Питомцы/Пересобрать все префабы (сотрёт правки)", false, 611)]
    public static void RebuildAll()
    {
        if (!EditorUtility.DisplayDialog("Пересобрать префабы питомцев",
            "Префабы Pet_*.prefab будут созданы заново, все твои правки в них пропадут. Продолжить?", "Пересобрать", "Отмена")) return;
        BuildAll(true);
    }

    static void BuildAll(bool overwrite)
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[Питомцы] Останови Play и повтори.");
            return;
        }
        if (!AssetDatabase.IsValidFolder(FbxFolder))
        {
            Debug.LogError("[Питомцы] Нет папки с моделями: " + FbxFolder);
            return;
        }

        EnsureFolder(PrefabFolder);

        var fbxPaths = new List<string>();
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { FbxFolder });
        for (int i = 0; i < guids.Length; i++) fbxPaths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));

        var pets = new Dictionary<string, PetData>();
        string[] petGuids = AssetDatabase.FindAssets("t:PetData");
        for (int i = 0; i < petGuids.Length; i++)
        {
            PetData p = AssetDatabase.LoadAssetAtPath<PetData>(AssetDatabase.GUIDToAssetPath(petGuids[i]));
            if (p == null) continue;
            string key = string.IsNullOrEmpty(p.id) ? p.name : p.id;
            if (!pets.ContainsKey(key)) pets.Add(key, p);
        }
        if (pets.Count == 0)
            Debug.LogWarning("[Питомцы] PetData не найдены: сначала Tools > BuilderSim > Питомцы > Создать виды. Префабы соберу, но назначать их некуда.");

        var log = new StringBuilder();
        log.AppendLine("[Питомцы] Префабы (" + (overwrite ? "пересборка" : "только недостающие") + "):");
        int built = 0, kept = 0, assigned = 0;

        for (int i = 0; i < Table.Length; i++)
        {
            Species sp = Table[i];
            string prefabPath = PrefabFolder + "/Pet_" + sp.id + ".prefab";
            GameObject prefab = null;

            if (!overwrite)
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    kept++;
                    log.AppendLine("  " + sp.id + ": префаб уже есть, не трогаю.");
                }
            }

            if (prefab == null)
            {
                List<string> candidates = FindCandidates(fbxPaths, sp.names);
                if (candidates.Count == 0)
                {
                    log.AppendLine("  " + sp.id + ": FBX не найден в " + FbxFolder + " (ищу имена: " + string.Join(", ", sp.names) + ")");
                    continue;
                }

                string fbxPath = Pick(candidates, sp.names);
                if (candidates.Count > 1)
                    log.AppendLine("  " + sp.id + ": подходящих FBX несколько (" + NamesOf(candidates) + "), взят «" + Path.GetFileNameWithoutExtension(fbxPath) + "»");

                prefab = BuildPrefab(sp, fbxPath, prefabPath, log);
                if (prefab == null) continue;
                built++;
            }

            PetData pet;
            if (pets.TryGetValue(sp.id, out pet) && pet.prefab != prefab)
            {
                pet.prefab = prefab;
                EditorUtility.SetDirty(pet);
                assigned++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        log.AppendLine("  Итого: собрано " + built + ", оставлено как было " + kept + ", назначено в PetData " + assigned + ".");
        Debug.Log(log.ToString());
    }

    // ---------------------------------------------------------------- один вид

    static GameObject BuildPrefab(Species sp, string fbxPath, string prefabPath, StringBuilder log)
    {
        // сначала импортёр (после реимпорта старые ссылки на ассеты FBX недействительны), потом загрузка
        EnableLoops(fbxPath);

        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbx == null)
        {
            log.AppendLine("  " + sp.id + ": не удалось загрузить " + fbxPath);
            return null;
        }

        var clips = new List<AnimationClip>();
        UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        for (int i = 0; i < all.Length; i++)
        {
            AnimationClip c = all[i] as AnimationClip;
            if (c != null && !c.name.StartsWith("__preview__", StringComparison.Ordinal)) clips.Add(c);
        }

        AnimationClip idle = PickClip(clips, "idle", "stand", "breath");
        AnimationClip move = PickClip(clips, "run", "gallop", "trot", "walk");
        if (idle == null && clips.Count > 0) idle = clips[0];

        var names = new List<string>();
        for (int i = 0; i < clips.Count; i++) names.Add(clips[i].name);
        log.AppendLine("  " + sp.id + ": FBX «" + Path.GetFileNameWithoutExtension(fbxPath) + "», клипы [" + string.Join(", ", names.ToArray()) + "]"
            + ", Idle=" + (idle != null ? idle.name : "НЕТ") + ", Move=" + (move != null ? move.name : "НЕТ"));

        var root = new GameObject("Pet_" + sp.id);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        model.name = "Model";
        model.transform.SetParent(root.transform, false);

        // размер и pivot у ног
        Bounds b;
        if (GetBounds(root, out b) && b.size.y > 0.0001f)
        {
            float s = sp.height / b.size.y;
            Vector3 baseScale = model.transform.localScale;
            model.transform.localScale = baseScale * s;
            model.transform.localPosition = new Vector3(-b.center.x * s, -b.min.y * s, -b.center.z * s);
        }
        else
        {
            log.AppendLine("  " + sp.id + ": не нашёл Renderer в модели, размер не менял.");
        }

        // колайдеры не нужны (PetFollower их всё равно выключает)
        Collider[] cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++) UnityEngine.Object.DestroyImmediate(cols[i]);

        // Animator (для generic-модели нужен Avatar)
        Animator anim = model.GetComponentInChildren<Animator>();
        if (anim == null)
        {
            anim = model.AddComponent<Animator>();
            for (int i = 0; i < all.Length; i++)
            {
                Avatar av = all[i] as Avatar;
                if (av != null) { anim.avatar = av; break; }
            }
        }
        anim.runtimeAnimatorController = null;
        anim.applyRootMotion = false;

        PetFollower follower = root.AddComponent<PetFollower>();
        if (follower == null || MonoScript.FromMonoBehaviour(follower) == null)
        {
            log.AppendLine("  " + sp.id + ": ОШИБКА. Скрипт PetFollower не найден как файл. Файл должен называться ровно PetFollower.cs "
                + "(не «PetFollower 1.cs» и не «PetFollower (1).cs»), и в проекте должна быть одна его копия.");
            UnityEngine.Object.DestroyImmediate(root);
            return null;
        }
        follower.animator = anim;
        follower.idleClip = idle;
        follower.moveClip = move;
        follower.shadowSize = Mathf.Max(0.5f, sp.height * 1.1f);

        // на всякий случай убираем «пустые» скрипты с корня, иначе Unity не даст сохранить префаб
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return saved;
    }

    // Включает loopTime у клипов Idle / Run / Walk и т.п. (в FBX клипы только для чтения, поэтому через импортёр)
    static void EnableLoops(string fbxPath)
    {
        ModelImporter imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (imp == null) return;

        ModelImporterClipAnimation[] clips = imp.clipAnimations;
        if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            string n = clips[i].name.ToLowerInvariant();
            bool loopable = n.Contains("idle") || n.Contains("stand") || n.Contains("breath")
                || n.Contains("run") || n.Contains("walk") || n.Contains("gallop") || n.Contains("trot");
            if (loopable && !clips[i].loopTime)
            {
                clips[i].loopTime = true;
                changed = true;
            }
        }
        if (!changed) return;

        imp.clipAnimations = clips;
        imp.SaveAndReimport();
    }

    // ---------------------------------------------------------------- поиск

    static List<string> FindCandidates(List<string> fbxPaths, string[] keys)
    {
        var res = new List<string>();
        for (int i = 0; i < fbxPaths.Count; i++)
        {
            string file = Path.GetFileNameWithoutExtension(fbxPaths[i]).ToLowerInvariant();
            for (int k = 0; k < keys.Length; k++)
            {
                if (file.StartsWith(keys[k], StringComparison.Ordinal)) { res.Add(fbxPaths[i]); break; }
            }
        }
        return res;
    }

    // сначала точное совпадение с именем по порядку keys, иначе самое короткое имя
    static string Pick(List<string> candidates, string[] keys)
    {
        for (int k = 0; k < keys.Length; k++)
            for (int i = 0; i < candidates.Count; i++)
                if (string.Equals(Path.GetFileNameWithoutExtension(candidates[i]), keys[k], StringComparison.OrdinalIgnoreCase))
                    return candidates[i];

        string best = candidates[0];
        for (int i = 1; i < candidates.Count; i++)
            if (Path.GetFileNameWithoutExtension(candidates[i]).Length < Path.GetFileNameWithoutExtension(best).Length) best = candidates[i];
        return best;
    }

    // клип, в имени которого есть ключ; ключи по приоритету, при нескольких берётся самое короткое имя
    static AnimationClip PickClip(List<AnimationClip> clips, params string[] keys)
    {
        for (int k = 0; k < keys.Length; k++)
        {
            AnimationClip best = null;
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i].name.ToLowerInvariant().Contains(keys[k]) && (best == null || clips[i].name.Length < best.name.Length))
                    best = clips[i];
            }
            if (best != null) return best;
        }
        return null;
    }

    static string NamesOf(List<string> paths)
    {
        var n = new List<string>();
        for (int i = 0; i < paths.Count; i++) n.Add(Path.GetFileNameWithoutExtension(paths[i]));
        return string.Join(", ", n.ToArray());
    }

    static bool GetBounds(GameObject go, out Bounds b)
    {
        b = new Bounds();
        bool first = true;
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < rs.Length; i++)
        {
            if (rs[i] is ParticleSystemRenderer) continue;
            if (first) { b = rs[i].bounds; first = false; }
            else b.Encapsulate(rs[i].bounds);
        }
        return !first;
    }

}
