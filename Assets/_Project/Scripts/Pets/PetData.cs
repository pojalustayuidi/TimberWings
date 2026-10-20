// Assets/_Project/Scripts/Pets/PetData.cs (НЕ в Editor)
// Один вид питомца: данные в ScriptableObject. Создаётся через Assets > Create > TimberWings > Питомец.
// Размер бонуса здесь НЕ хранится: он зависит только от редкости и лежит в GameBalance.PetBonusByRarity.
using UnityEngine;

[CreateAssetMenu(fileName = "Pet_New", menuName = "TimberWings/Питомец", order = 10)]
public class PetData : ScriptableObject
{
    [Tooltip("Уникальный id (для сохранений). Пусто = берётся имя ассета")]
    public string id;

    [Tooltip("Имя, которое видит игрок")]
    public string displayName = "Питомец";

    public PetRarity rarity = PetRarity.Common;

    [Tooltip("Какую характеристику усиливает")]
    public PetStat stat = PetStat.ChopPower;

    [Tooltip("Префаб питомца в мире (с PetFollower). Делает меню «Собрать префабы»")]
    public GameObject prefab;

    [Tooltip("Иконка для карточки. Пусто = UI рисует цветную карточку с названием")]
    public Sprite icon;

    [Tooltip("Необязательно: префабы стадий роста 0 малыш, 1 подросток, 2 взрослый. Пусто = везде prefab")]
    public GameObject[] growthPrefabs;

    // Бонус питомца как доля (0.05 = +5%). Для Capacity UI/менеджер переведут в бревна сами.
    public float Bonus { get { return GameBalance.PetBonus(rarity); } }

    // Префаб под стадию роста (0..2), с запасным вариантом
    public GameObject PrefabForStage(int stage)
    {
        if (growthPrefabs != null && growthPrefabs.Length > 0)
        {
            int i = Mathf.Clamp(stage, 0, growthPrefabs.Length - 1);
            if (growthPrefabs[i] != null) return growthPrefabs[i];
        }
        return prefab;
    }

    void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name;
    }
}
