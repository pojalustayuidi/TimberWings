// Assets/_Project/Editor/IslandV2/ISL017BuildingPadSettings.cs
// ISL-017. Только Editor: параметры аккуратных декоративных оснований зданий.
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public sealed class ISL017BuildingPadSettings : ScriptableObject
    {
        public const string AssetPath = IslandLayout.AssetFolder + "/ISL017_BuildingPads.asset";

        [Header("ISL-017 / Включение")]
        [Tooltip("Включает дополнительную окраску оснований при следующем запекании Candy-земли.")]
        public bool enabled = false;
        [Range(0f, 1f)] public float strength = 0.65f;
        [Range(0, 99999)] public int seed = 17017;

        [Header("Гравий и мягкая кромка (метры)")]
        [Range(0.35f, 3f)] public float apronReach = 1.65f;
        [Range(0f, 1f)] public float gravelStrength = 0.35f;
        [Range(0.3f, 3f)] public float gravelScale = 0.9f;
        [Range(0f, 1f)] public float softEdgeVariation = 0.25f;
        public Color gravelLight = new Color(0.94f, 0.87f, 0.74f, 1f);
        public Color gravelDark = new Color(0.79f, 0.71f, 0.64f, 1f);

        [Header("Чистый подход к входу (метры)")]
        [Range(0.6f, 3.5f)] public float approachLength = 2.0f;
        [Range(0.6f, 3.0f)] public float approachWidth = 1.35f;
        [Range(0f, 1f)] public float approachStrength = 0.45f;
        public Color approachColor = new Color(0.96f, 0.87f, 0.71f, 1f);
        [Range(0f, 1f)] public float cleanGround = 0.75f;

        [Header("Рабочие постройки вне основных PAD_ меток")]
        [Tooltip("Добавить основание под существующим CampTent, если он далеко от PAD_Tent. Не двигает саму палатку.")]
        public bool includeSceneCampTent = true;

        public static ISL017BuildingPadSettings GetExisting()
        {
            return AssetDatabase.LoadAssetAtPath<ISL017BuildingPadSettings>(AssetPath);
        }

        public static ISL017BuildingPadSettings GetOrCreate()
        {
            var s = GetExisting();
            if (s != null) return s;
            IslandLayout.EnsureFolder(IslandLayout.AssetFolder);
            s = CreateInstance<ISL017BuildingPadSettings>();
            AssetDatabase.CreateAsset(s, AssetPath);
            AssetDatabase.SaveAssets();
            return s;
        }
    }
}
