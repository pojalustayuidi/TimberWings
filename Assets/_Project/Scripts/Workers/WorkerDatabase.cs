// Assets/_Project/Scripts/Workers/WorkerDatabase.cs (НЕ в Editor)
// Список всех персонажей-рабочих (27 штук). Один ассет на проект: Assets/_Project/Data/WorkerDatabase.asset.
// Создаётся и проверяется меню Tools > BuilderSim > Рабочие. Шансы паков здесь НЕ хранятся: они в WorkerBalance.
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WorkerDatabase", menuName = "TimberWings/База рабочих", order = 13)]
public class WorkerDatabase : ScriptableObject
{
    public List<WorkerDefinition> workers = new List<WorkerDefinition>();

    public WorkerDefinition Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < workers.Count; i++)
            if (workers[i] != null && workers[i].id == id) return workers[i];
        return null;
    }

    // Складывает в into всех персонажей роли и редкости (список очищается, лишних аллокаций нет)
    public void Collect(WorkerRole role, WorkerRarity rarity, List<WorkerDefinition> into)
    {
        into.Clear();
        for (int i = 0; i < workers.Count; i++)
        {
            var w = workers[i];
            if (w != null && w.role == role && w.rarity == rarity) into.Add(w);
        }
    }

    public void CollectByRarity(WorkerRarity rarity, List<WorkerDefinition> into)
    {
        into.Clear();
        for (int i = 0; i < workers.Count; i++)
            if (workers[i] != null && workers[i].rarity == rarity) into.Add(workers[i]);
    }

    public int CountOf(WorkerRole role)
    {
        int n = 0;
        for (int i = 0; i < workers.Count; i++) if (workers[i] != null && workers[i].role == role) n++;
        return n;
    }
}
