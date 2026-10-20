// Assets/_Project/Scripts/Save/SaveSystem.cs (НЕ в Editor)
// Ядро сохранений. Один файл persistentDataPath/timberwings_save.json + резервная копия .bak.
// Системы регистрируются как ISaveable (Register в Awake, Unregister в OnDestroy), файл хранит секции по SaveKey.
//
// Правила:
//  - RequestSave(key) копит запрос и пишет файл через GameBalance.SaveDebounceSeconds (один файл целиком, key нужен для проверки опечаток).
//  - Во время Load / NewGame (IsLoading) RequestSave и запись игнорируются.
//  - Register после Load(): если секция есть в файле, она применяется прямо в Register; если секции нет, объект не трогаем.
//  - Запись: tmp, затем копия в основной файл. Резервная копия обновляется только если основной файл читается.
//  - Load: основной файл, при ошибке резервная копия (LoadedFromBackup), при ошибке обоих сброс на новую игру (LoadFailed).
//  - Load() может вызваться до создания UI (BeforeSceneLoad): итог лежит в PendingNotice, UI забирает его через ConsumeLoadNotice().
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public enum SaveLoadNotice { None, LoadedFromBackup, Failed }

public static class SaveSystem
{
    public const int FormatVersion = 1;
    const string FileName = "timberwings_save.json";

    // ключи из таблицы порядка (см. ISaveable); RequestSave с другим ключом даст предупреждение (опечатка)
    static readonly HashSet<string> KnownKeys = new HashSet<string>
        { "settings", "stats", "inventory", "upgrades", "skills", "workers", "workshop", "pets", "buildings", "story", "intro", "dialogue", "time" };

    [Serializable] class SaveSection { public string key; public string json; }
    [Serializable]
    class SaveFile
    {
        public int formatVersion;
        public string savedAtUtc;
        public List<SaveSection> sections = new List<SaveSection>();
    }

    // ------------------------------------------------------------ события
    public static event Action Saved;             // файл успешно записан (в т.ч. автосохранение)
    public static event Action SaveFailed;        // запись не удалась
    public static event Action Loaded;            // Load() применил файл (после снятия IsLoading)
    public static event Action LoadedFromBackup;  // основной файл был битый, загружена резервная копия
    public static event Action LoadFailed;        // читать нечего: обе копии битые, начата новая игра
    public static event Action NewGameStarted;    // NewGame() сбросил прогресс и удалил файлы

    // ------------------------------------------------------------ состояние
    static readonly List<ISaveable> items = new List<ISaveable>();               // по LoadOrder
    static readonly Dictionary<string, string> cache = new Dictionary<string, string>();   // последнее известное состояние секций (файл + свои записи)
    static readonly HashSet<string> warnedKeys = new HashSet<string>();
    static bool loadedOnce, dirty, mainGood;
    static int loadedVersion = FormatVersion;
    static int loadDepth;
    static float flushAt = -1f, autoTimer;
    static string filePath;

    public static bool IsLoading { get { return loadDepth > 0; } }

    public static SaveLoadNotice PendingNotice { get; private set; }

    public static string FilePath
    {
        get
        {
            if (filePath == null) filePath = Path.Combine(Application.persistentDataPath, FileName);
            return filePath;
        }
    }

    // есть ли что продолжать (основной файл или резервная копия)
    public static bool HasSave
    {
        get
        {
            try { return File.Exists(FilePath) || File.Exists(FilePath + ".bak"); }
            catch (Exception) { return false; }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        items.Clear(); cache.Clear(); warnedKeys.Clear();
        loadedOnce = dirty = mainGood = false;
        loadedVersion = FormatVersion;
        loadDepth = 0; flushAt = -1f; autoTimer = 0f;
        filePath = null;
        PendingNotice = SaveLoadNotice.None;
        Saved = null; SaveFailed = null; Loaded = null; LoadedFromBackup = null; LoadFailed = null; NewGameStarted = null;
    }

    // UI забирает итог загрузки, который мог случиться до его создания. Возвращает None, если показывать нечего.
    public static SaveLoadNotice ConsumeLoadNotice()
    {
        var n = PendingNotice;
        PendingNotice = SaveLoadNotice.None;
        return n;
    }

    // ------------------------------------------------------------ регистрация
    public static void Register(ISaveable s)
    {
        if (s == null) return;
        string key = s.SaveKey;
        if (string.IsNullOrEmpty(key)) { Debug.LogError("SaveSystem: пустой SaveKey у " + s.GetType().Name); return; }

        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(items[i], s)) return;
            if (items[i].SaveKey == key)
            {
                var uo = items[i] as UnityEngine.Object;
                bool dead = !ReferenceEquals(uo, null) && uo == null;   // старый объект уже уничтожен (перезагрузка сцены): не ошибка
                if (!dead) Debug.LogWarning("SaveSystem: ключ «" + key + "» уже занят (" + items[i].GetType().Name + "), заменён на " + s.GetType().Name);
                items.RemoveAt(i);
                break;
            }
        }

        int at = items.Count;
        for (int i = 0; i < items.Count; i++)
            if (items[i].LoadOrder > s.LoadOrder) { at = i; break; }
        items.Insert(at, s);

        string json;
        if (loadedOnce && cache.TryGetValue(key, out json))
        {
            loadDepth++;
            try { ApplyOne(s, json); }
            finally { loadDepth--; }
        }
    }

    public static void Unregister(ISaveable s)
    {
        if (s == null) return;
        for (int i = items.Count - 1; i >= 0; i--)
            if (ReferenceEquals(items[i], s)) { items.RemoveAt(i); return; }
    }

    // ------------------------------------------------------------ запись
    public static void RequestSave(string key = null)
    {
        if (loadDepth > 0) return;   // во время загрузки не пишем
        if (!string.IsNullOrEmpty(key) && !KnownKeys.Contains(key) && !IsRegisteredKey(key) && warnedKeys.Add(key))
            Debug.LogWarning("SaveSystem: RequestSave с неизвестным ключом «" + key + "» (в таблице: " + string.Join(", ", new List<string>(KnownKeys).ToArray()) + ")");
        dirty = true;
        if (flushAt < 0f) flushAt = Time.realtimeSinceStartup + GameBalance.SaveDebounceSeconds;
    }

    static bool IsRegisteredKey(string key)
    {
        for (int i = 0; i < items.Count; i++) if (items[i].SaveKey == key) return true;
        return false;
    }

    // Записать немедленно (кнопка «Сохранить сейчас», выход). false: идёт загрузка, нечего писать или ошибка записи.
    public static bool SaveNow()
    {
        if (loadDepth > 0) return false;
        dirty = false; flushAt = -1f; autoTimer = 0f;
        if (items.Count == 0) return false;

        var fresh = new Dictionary<string, string>();
        try
        {
            var file = new SaveFile { formatVersion = FormatVersion, savedAtUtc = DateTime.UtcNow.ToString("o") };
            var snapshot = items.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                string key = snapshot[i].SaveKey, json = null;
                try { json = snapshot[i].Save(); }
                catch (Exception e) { Debug.LogError("SaveSystem: Save() «" + key + "» упал: " + e); }
                if (json == null && !cache.TryGetValue(key, out json)) continue;   // не вышло: оставляем прошлую версию секции
                fresh[key] = json;
                file.sections.Add(new SaveSection { key = key, json = json });
            }
            foreach (var kv in cache)   // секции систем, которых сейчас нет в сцене, сохраняем как есть
                if (!fresh.ContainsKey(kv.Key)) file.sections.Add(new SaveSection { key = kv.Key, json = kv.Value });

            string text = JsonUtility.ToJson(file);
            if (Encoding.UTF8.GetByteCount(text) > GameBalance.SaveMaxFileBytes)
                throw new IOException("файл сохранения больше лимита " + GameBalance.SaveMaxFileBytes + " байт");
            WriteFile(text);
        }
        catch (Exception e)
        {
            Debug.LogError("SaveSystem: запись не удалась: " + e);
            dirty = true;                                          // не теряем несохранённое: повторим позже
            flushAt = Time.realtimeSinceStartup + 10f;
            Fire(SaveFailed);
            return false;
        }

        foreach (var kv in fresh) cache[kv.Key] = kv.Value;
        Fire(Saved);
        return true;
    }

    static void WriteFile(string text)
    {
        string p = FilePath, tmp = p + ".tmp", bak = p + ".bak";
        string dir = Path.GetDirectoryName(p);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        File.WriteAllText(tmp, text, new UTF8Encoding(false));

        SaveFile ignored;
        if (File.Exists(p) && (mainGood || TryRead(p, out ignored)))
        {
            try { File.Copy(p, bak, true); }   // резервной копией становится прошлый ЧИТАЕМЫЙ файл
            catch (Exception e) { Debug.LogWarning("SaveSystem: не удалось обновить резервную копию: " + e.Message); }
        }
        File.Copy(tmp, p, true);
        File.Delete(tmp);
        mainGood = true;
    }

    // ------------------------------------------------------------ загрузка
    // true: файл применён. false: файла нет (ничего не делаем) или он битый (тогда LoadFailed и сброс на новую игру).
    public static bool Load()
    {
        if (loadDepth > 0) return false;
        string p = FilePath, bak = p + ".bak";
        bool mainExists = File.Exists(p), bakExists = File.Exists(bak);
        if (!mainExists && !bakExists) return false;

        dirty = false; flushAt = -1f;

        SaveFile f;
        bool fromBackup = false;
        if (mainExists && TryRead(p, out f)) { }
        else if (bakExists && TryRead(bak, out f))
        {
            fromBackup = mainExists;   // если основного файла просто нет, это не поломка
            if (mainExists) Quarantine(p);
            try { File.Copy(bak, p, true); } catch (Exception e) { Debug.LogWarning("SaveSystem: не удалось восстановить основной файл из копии: " + e.Message); }
        }
        else
        {
            Debug.LogWarning("SaveSystem: сохранение повреждено, резервной копии нет. Новая игра.");
            Quarantine(p); Quarantine(bak);
            mainGood = false;
            cache.Clear(); loadedOnce = false;
            ResetAllInternal();
            PendingNotice = SaveLoadNotice.Failed;
            Fire(LoadFailed);
            return false;
        }

        mainGood = true;
        cache.Clear();
        for (int i = 0; i < f.sections.Count; i++)
            if (f.sections[i] != null && !string.IsNullOrEmpty(f.sections[i].key)) cache[f.sections[i].key] = f.sections[i].json;
        loadedVersion = f.formatVersion;
        loadedOnce = true;

        loadDepth++;
        try
        {
            var snapshot = items.ToArray();   // Load() систем может регистрировать/снимать другие
            for (int i = 0; i < snapshot.Length; i++)
            {
                string json;
                cache.TryGetValue(snapshot[i].SaveKey, out json);   // null: секции в файле нет, система сбросится в дефолт
                ApplyOne(snapshot[i], json);
            }
        }
        finally { loadDepth--; }

        if (fromBackup) PendingNotice = SaveLoadNotice.LoadedFromBackup;
        Fire(Loaded);
        if (fromBackup) Fire(LoadedFromBackup);
        return true;
    }

    // Новая игра: все системы в дефолт, файлы удалены. Сцену перезапускает вызывающий (меню).
    public static void NewGame()
    {
        if (loadDepth > 0) return;
        dirty = false; flushAt = -1f; autoTimer = 0f;
        ResetAllInternal();
        cache.Clear();
        loadedOnce = false;
        mainGood = false;
        PendingNotice = SaveLoadNotice.None;
        string p = FilePath;
        TryDelete(p); TryDelete(p + ".bak"); TryDelete(p + ".tmp");
        Fire(NewGameStarted);
    }

    // ------------------------------------------------------------ служебное
    static void ApplyOne(ISaveable s, string json)
    {
        try { s.Load(json, loadedVersion); }
        catch (Exception e)
        {
            Debug.LogError("SaveSystem: Load() «" + s.SaveKey + "» упал, сброс секции: " + e);
            try { s.ResetToDefault(); } catch (Exception e2) { Debug.LogError("SaveSystem: ResetToDefault() «" + s.SaveKey + "» упал: " + e2); }
        }
    }

    static void ResetAllInternal()
    {
        loadDepth++;
        try
        {
            var snapshot = items.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try { snapshot[i].ResetToDefault(); }
                catch (Exception e) { Debug.LogError("SaveSystem: ResetToDefault() «" + snapshot[i].SaveKey + "» упал: " + e); }
            }
        }
        finally { loadDepth--; }
    }

    // Прочитать секции из файла НЕ применяя их (для меню: «уровень, день» под кнопкой «Продолжить»).
    // Порядок результата = порядок ключей, нет секции или файла: null / null-элемент. Игру и кэш не трогает.
    public static string[] PeekSections(params string[] keys)
    {
        SaveFile f;
        if (!(TryRead(FilePath, out f) || TryRead(FilePath + ".bak", out f))) return null;
        var res = new string[keys.Length];
        for (int i = 0; i < f.sections.Count; i++)
        {
            var sec = f.sections[i];
            if (sec == null) continue;
            for (int k = 0; k < keys.Length; k++) if (sec.key == keys[k]) res[k] = sec.json;
        }
        return res;
    }

    static bool TryRead(string p, out SaveFile f)
    {
        f = null;
        try
        {
            var fi = new FileInfo(p);
            if (!fi.Exists || fi.Length <= 0 || fi.Length > GameBalance.SaveMaxFileBytes) return false;
            var parsed = JsonUtility.FromJson<SaveFile>(File.ReadAllText(p, Encoding.UTF8));
            if (parsed == null || parsed.formatVersion <= 0 || parsed.sections == null) return false;
            f = parsed;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("SaveSystem: не удалось прочитать «" + p + "»: " + e.Message);
            return false;
        }
    }

    // битый файл не удаляем: уходит в .corrupt (одна копия), чтобы можно было разобраться
    static void Quarantine(string p)
    {
        try
        {
            if (!File.Exists(p)) return;
            File.Copy(p, p + ".corrupt", true);
            File.Delete(p);
        }
        catch (Exception e) { Debug.LogWarning("SaveSystem: не удалось убрать битый файл: " + e.Message); }
    }

    static void TryDelete(string p)
    {
        try { if (File.Exists(p)) File.Delete(p); }
        catch (Exception e) { Debug.LogWarning("SaveSystem: не удалось удалить «" + p + "»: " + e.Message); }
    }

    static void Fire(Action a)
    {
        if (a == null) return;
        foreach (Delegate d in a.GetInvocationList())
        {
            try { ((Action)d)(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    // ------------------------------------------------------------ вызывается SaveAutoRunner
    internal static void Tick(float unscaledDeltaTime, bool timeRunning)
    {
        if (loadDepth > 0) return;
        if (dirty && Time.realtimeSinceStartup >= flushAt) SaveNow();
        if (timeRunning)   // в паузе (timeScale 0) автосохранение по таймеру не копится
        {
            autoTimer += unscaledDeltaTime;
            if (autoTimer >= GameBalance.SaveAutoIntervalSeconds) SaveNow();
        }
    }

    internal static void FlushIfDirty()
    {
        if (dirty && loadDepth == 0) SaveNow();
    }
}
