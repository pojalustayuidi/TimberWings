// Assets/_Project/Scripts/Dialogue/VoiceBank.cs (НЕ в Editor)
// Ищет озвучку реплики по id (см. VoiceId). Файлы кладутся в Assets/.../Resources/Voice/<id>.mp3 (или .wav, .ogg).
// Если файла нет, DialogueManager работает как раньше (бормотание + субтитры): игра не ломается, пока озвучка пишется по частям.
// В редакторе каждая показанная реплика дописывается в <проект>/VoiceExport/seen_lines.csv: так видно реплики,
// которые добавили позже и которых ещё нет в основной таблице.
using System.Collections.Generic;
using UnityEngine;

public static class VoiceBank
{
    public const string Folder = "Voice/";
    // Новая озвучка 2.1 хранится отдельно от старых записей и не требует
    // редактировать бинарный AudioLibrary.asset или перенастраивать NPC в Inspector.
    public const string Voice21Folder = "Voice21/";
    public static bool Enabled = true;

    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { cache.Clear(); }

    public static AudioClip Find(string speakerDisplayName, string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        string id = VoiceId.Make(speakerDisplayName, text);
#if UNITY_EDITOR
        Record(id, speakerDisplayName, text);
#endif
        if (!Enabled) return null;
        // Проверяем записи 2.1 ПЕРЕД старой AudioLibrary: в старых проектах
        // replaceLegacyVoices=true, поэтому обычный fallback Resources/Voice
        // для новых строк был бы недоступен.
        var newer = Resources.Load<AudioClip>(Voice21Folder + id);
        if (newer != null) return newer;
        var library = DialogueAudioLibrary.Current;
        if (library != null)
        {
            var replacement = library.Find(id);
            if (replacement != null || library.replaceLegacyVoices) return replacement;
        }
        AudioClip clip;
        if (cache.TryGetValue(id, out clip)) return clip;
        clip = Resources.Load<AudioClip>(Folder + id);      // null, если файла ещё нет
        cache[id] = clip;
        return clip;
    }

#if UNITY_EDITOR
    static HashSet<string> seen;
    static string exportPath;

    static void Record(string id, string speaker, string text)
    {
        try
        {
            if (seen == null)
            {
                seen = new HashSet<string>();
                string dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "VoiceExport");
                System.IO.Directory.CreateDirectory(dir);
                exportPath = System.IO.Path.Combine(dir, "seen_lines.csv");
                if (System.IO.File.Exists(exportPath))
                    foreach (var l in System.IO.File.ReadAllLines(exportPath))
                    {
                        int i = l.IndexOf(';');
                        if (i > 0) seen.Add(l.Substring(0, i));
                    }
                else System.IO.File.WriteAllText(exportPath, "id;персонаж;текст\n", new System.Text.UTF8Encoding(true));
            }
            if (!seen.Add(id)) return;
            string safe = "\"" + VoiceId.Normalize(text).Replace("\"", "\"\"") + "\"";
            System.IO.File.AppendAllText(exportPath, id + ";" + (speaker ?? "") + ";" + safe + "\n");
        }
        catch (System.Exception e) { Debug.LogWarning("VoiceBank: не удалось записать seen_lines.csv: " + e.Message); }
    }
#endif
}
