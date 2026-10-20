// Assets/_Project/Scripts/Workers/WorkerDefinition.cs (НЕ в Editor)
// Один персонаж-рабочий (из 27): данные в ScriptableObject. Создаётся через Assets > Create > TimberWings > Рабочий
// или Editor-инструментом Tools > BuilderSim > Рабочие. Числа выработки здесь НЕ хранятся: они зависят от роли,
// редкости и уровня и лежат в WorkerBalance. Здесь только то, что делает персонажа персонажем.
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Worker_New", menuName = "TimberWings/Рабочий", order = 12)]
public class WorkerDefinition : ScriptableObject
{
    [Tooltip("Уникальный id (для сохранений), например worker_clerk_nils. Пусто = берётся имя ассета. После релиза не менять")]
    public string id;

    public string firstName = "Имя";
    public string lastName = "Фамилия";
    [Range(14, 99)] public int age = 30;

    public WorkerRole role = WorkerRole.Clerk;
    public WorkerRarity rarity = WorkerRarity.Common;

    [Tooltip("Перки по правилу редкости: Обычный 0, Необычный 1, Редкий 2, Легендарный 3, Мифический все 4. Фирменный перк мифика включается сам")]
    public List<WorkerPerk> perks = new List<WorkerPerk>();

    [TextArea(1, 3), Tooltip("Одна строка био. У мификов и легендарных можно подробнее")]
    public string bio;

    [Tooltip("Портрет 256×256. Пусто = UI рисует цветную карточку с инициалами")]
    public Sprite portrait;

    public string FullName { get { return (firstName + " " + lastName).Trim(); } }

    // Мифик с фирменным перком (по одному на роль, действует по правилам CampSimulation)
    public bool HasSignature { get { return rarity == WorkerRarity.Mythic; } }

    public bool Has(WorkerPerk p) { return perks != null && perks.Contains(p); }

    // Проверка по правилам дизайна: используют Editor-инструмент «Проверить базу» и Setup Doctor.
    // Возвращает true, если ошибок нет; иначе error = первая найденная проблема.
    public bool Validate(out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(id)) { error = "пустой id"; return false; }
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName)) { error = "не заполнены имя или фамилия"; return false; }

        int count = perks != null ? perks.Count : 0;
        int need = WorkerBalance.PerkCount(rarity);
        if (count != need) { error = "перков " + count + ", для редкости «" + WorkerText.Rarity(rarity) + "» нужно " + need; return false; }

        var seen = new HashSet<WorkerPerk>();
        for (int i = 0; i < count; i++)
        {
            var p = perks[i];
            if (WorkerText.RoleOf(p) != role) { error = "перк «" + WorkerText.Perk(p) + "» не принадлежит роли «" + WorkerText.Role(role) + "»"; return false; }
            if (!seen.Add(p)) { error = "перк «" + WorkerText.Perk(p) + "» указан дважды"; return false; }
        }
        return true;
    }

    void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name;
        if (age < 14) age = 14;
    }
}
