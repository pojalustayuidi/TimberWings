// Assets/_Project/Scripts/Pets/PetDatabase.cs (НЕ в Editor)
// Список всех питомцев. Один ассет на проект: Assets > Create > TimberWings > База питомцев.
// Шансы клетей здесь НЕ хранятся: единственный источник чисел GameBalance.PetCrateOdds (правило проекта).
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PetDatabase", menuName = "TimberWings/База питомцев", order = 11)]
public class PetDatabase : ScriptableObject
{
    public List<PetData> pets = new List<PetData>();

    // шансы клети по редкостям в процентах: [Common, Uncommon, Rare, Legendary]
    public float[] GetOdds(CrateType crate)
    {
        return GameBalance.PetCrateOdds[(int)crate];
    }

    public PetData Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < pets.Count; i++)
            if (pets[i] != null && pets[i].id == id) return pets[i];
        return null;
    }

    // складывает в into всех питомцев данной редкости (без лишних аллокаций, список очищается)
    public void CollectByRarity(PetRarity rarity, List<PetData> into)
    {
        into.Clear();
        for (int i = 0; i < pets.Count; i++)
            if (pets[i] != null && pets[i].rarity == rarity) into.Add(pets[i]);
    }
}
