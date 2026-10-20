// Assets/_Project/Scripts/Dialogue/DialogueLog.cs (НЕ в Editor)
// Журнал последних реплик (GDD 10.4.1: «журнал диалогов сохраняет последние 20»).
// Пропущенные реплики тоже попадают сюда (skipped = true), чтобы игрок мог перечитать то, что промотал.
// Окна журнала пока нет: Entries отдаёт список для будущего экрана «Дневник».
using System;
using System.Collections.Generic;
using UnityEngine;

public static class DialogueLog
{
    public const int Capacity = 20;

    public struct Entry
    {
        public string speaker;
        public string text;
        public bool skipped;
    }

    static readonly List<Entry> entries = new List<Entry>(Capacity + 1);
    public static IReadOnlyList<Entry> Entries { get { return entries; } }
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { entries.Clear(); Changed = null; }

    public static void Add(string speaker, string text, bool skipped)
    {
        if (string.IsNullOrEmpty(text)) return;
        entries.Add(new Entry { speaker = speaker ?? "", text = text, skipped = skipped });
        if (entries.Count > Capacity) entries.RemoveAt(0);
        if (Changed != null) Changed();
    }
}
