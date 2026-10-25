// Assets/_Project/Editor/Look/ISL010TwilightSettings.cs
// Настройки силы ISL-010. Создаются через меню Unity Editor; в билде не используются.
using UnityEngine;

public sealed class ISL010TwilightSettings : ScriptableObject
{
    [Header("Cloudy Candy: золотисто-розовый закат")]
    [Tooltip("0 = не менять текущие цвета заката, 1 = применить целевую палитру полностью.")]
    [Range(0f, 1f)] public float sunsetStrength = 0.65f;

    [Header("Cloudy Candy: мягкая ночь")]
    [Tooltip("Ночной свет уже хорош. Поэтому по умолчанию только очень лёгкая коррекция палитры.")]
    [Range(0f, 1f)] public float nightStrength = 0.25f;
}
