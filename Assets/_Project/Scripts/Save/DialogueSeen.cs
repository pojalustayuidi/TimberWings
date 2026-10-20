// Assets/_Project/Scripts/Save/DialogueSeen.cs (НЕ в Editor)
// Запоминает, какие «разовые» разговоры уже прозвучали (ключ «dialogue», LoadOrder 85), чтобы они не повторялись
// при каждом запуске сцены. Первым это нужно стартовой реплике Лео «Дядя? ...Никого. Ладно, осмотрюсь.».
// Создаётся сам до загрузки сцены. «Новая игра» очищает список, и реплика прозвучит снова.
// Использует DialogueSequence: для once-разговоров с триггером OnStart id берётся из пути объекта сам,
// для остальных можно задать DialogueSequence.saveId вручную.
using System;
using System.Collections.Generic;
using UnityEngine;

public class DialogueSeen : ISaveable
{
    static DialogueSeen instance;
    static readonly HashSet<string> seen = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; seen.Clear(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        instance = new DialogueSeen();
        SaveSystem.Register(instance);   // если Load() уже был, секция применится прямо здесь
    }

    public static bool Has(string id) { return !string.IsNullOrEmpty(id) && seen.Contains(id); }

    public static void Mark(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (seen.Add(id) && !SaveSystem.IsLoading) SaveSystem.RequestSave("dialogue");
    }

    // ------------------------------------------------------------ ISaveable
    public string SaveKey { get { return "dialogue"; } }
    public int LoadOrder { get { return 85; } }

    // ДОБАВЛЯТЬ ПОЛЯ ТОЛЬКО С ДЕФОЛТАМИ
    [Serializable]
    class SaveData
    {
        public int version = 1;
        public List<string> ids = new List<string>();
    }

    public string Save()
    {
        var d = new SaveData();
        d.ids.AddRange(seen);
        return JsonUtility.ToJson(d);
    }

    public void Load(string json, int formatVersion)
    {
        seen.Clear();
        if (string.IsNullOrEmpty(json)) return;

        SaveData d = null;
        try { d = JsonUtility.FromJson<SaveData>(json); }
        catch (Exception e) { Debug.LogWarning("DialogueSeen: не удалось прочитать список разговоров: " + e.Message); }
        if (d == null || d.ids == null) return;

        for (int i = 0; i < d.ids.Count; i++)
            if (!string.IsNullOrEmpty(d.ids[i])) seen.Add(d.ids[i]);
    }

    public void ResetToDefault() { seen.Clear(); }
}
