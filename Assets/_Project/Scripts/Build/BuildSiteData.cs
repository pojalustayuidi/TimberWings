// Assets/_Project/Scripts/Build/BuildSiteData.cs
// Данные стройки: цена, время и награда каждого этапа. ЕДИНСТВЕННОЕ место, где это задаётся.
// Создать: Assets > Create > BuilderSim > Build Site Data
// (или ПКМ по BuildingController: «Создать Build Site Data из текущих полей»).
using System;
using UnityEngine;

[CreateAssetMenu(menuName = "BuilderSim/Build Site Data", fileName = "BuildSite_New")]
public class BuildSiteData : ScriptableObject
{
    [Serializable]
    public class Step
    {
        [Tooltip("Для себя: что это за этап, например «Каркас и лавки»")]
        public string name = "Этап";
        [Min(0)] public int woodCost = 4;            // сколько брёвен сдать (0 = брёвна не нужны)
        [Min(0)] public int plankCost = 0;           // сколько досок сдать
        [Min(0)] public int coinCost = 0;            // сколько монет заплатить (списываются разом, когда хватает на всё)
        [Min(0.1f)] public float buildTime = 2.5f;   // сколько секунд идёт стройка
        [Min(0)] public int rewardXp = 10;           // опыт за этот этап
        [Tooltip("Короткий текст в окне стройки под названием этапа. Пусто = общий текст по умолчанию")]
        [TextArea(1, 3)] public string description;
    }

    public string buildingName = "Костёр";

    [Tooltip("Значок здания в окне стройки. Пусто = значок метки BuildSiteTagView, а если и его нет, значок бревна")]
    public Sprite icon;

    [Tooltip("Какая стадия видна до первой сдачи (у костра 1)")]
    [Min(1)] public int startStage = 1;

    [Tooltip("Улучшения по порядку. У костра два: 1→2 и 2→3")]
    public Step[] steps = { new Step() };

    public int StepCount { get { return steps != null ? steps.Length : 0; } }
}