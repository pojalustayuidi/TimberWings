// Assets/_Project/Scripts/Workers/WorkerApprentice.cs (НЕ в Editor)
// «Подмастерья»: три Обычных Приказчика, в которых превращаются рабочие из старого сохранения (версия 1, цифровые рабочие).
// Это НЕ часть 27 персонажей: в WorkerDatabase их нет, в «Коллекции» и в паках они не участвуют (проверка базы «по 9 на роль» не ломается).
// Определения создаются в памяти один раз (ScriptableObject.CreateInstance), id постоянные: worker_apprentice_1..3.
// Выработка как у любого Обычного Приказчика 1 уровня (WorkerBalance), то есть 0.45 монеты/с против старых 0.4: без потери дохода.
using UnityEngine;

public static class WorkerApprentice
{
    public const int Count = WorkerBalance.MaxPerRole;      // роль ограничена тремя, больше Подмастерьев не нужно
    public const string IdPrefix = "worker_apprentice_";

    static WorkerDefinition[] cache;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { cache = null; }

    public static bool IsApprenticeId(string id) { return !string.IsNullOrEmpty(id) && id.StartsWith(IdPrefix); }

    // i = 0..2
    public static WorkerDefinition Get(int i)
    {
        if (i < 0 || i >= Count) return null;
        if (cache == null) cache = new WorkerDefinition[Count];
        if (cache[i] != null) return cache[i];

        var d = ScriptableObject.CreateInstance<WorkerDefinition>();
        d.hideFlags = HideFlags.HideAndDontSave;
        d.name = IdPrefix + (i + 1);
        d.id = IdPrefix + (i + 1);
        d.firstName = "Подмастерье";
        d.lastName = "";
        d.age = 19 + i * 3;
        d.role = WorkerRole.Clerk;
        d.rarity = WorkerRarity.Common;
        d.perks.Clear();
        d.bio = "Помогал в лагере ещё до прихода артели.";
        cache[i] = d;
        return d;
    }

    // По id из сохранения; null, если это не Подмастерье
    public static WorkerDefinition Find(string id)
    {
        if (!IsApprenticeId(id)) return null;
        for (int i = 0; i < Count; i++)
        {
            var d = Get(i);
            if (d != null && d.id == id) return d;
        }
        return null;
    }
}
