#if UNITY_EDITOR
// Assets/_Project/Editor/IslandV2/Forest/ForestUnderstoryCandySettings.cs
// ISL-008: только редакторский профиль декоративного подлеска.
// Не меняет IslandLayout, рабочие деревья, точки взаимодействия или NavMesh.
using UnityEditor;
using UnityEngine;

namespace IslandV2
{
    public sealed class ForestUnderstoryCandySettings : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/IslandV2/UnderstoryCandySettings.asset";

        [Header("Повторяемость")]
        [Tooltip("Одинаковый Seed + одинаковые лес и раскладка = одинаковое размещение.")]
        public int seed = 5151;

        [Header("Ограничители количества (как в оригинале)")]
        [Range(0, 260)] public int bushCap = 260;
        [Range(0, 520)] public int flowerCap = 520;
        [Range(0, 80)] public int rockCap = 80;

        [Header("Кластеры подлеска (0 = исходная равномерная вероятность)")]
        [Range(0f, 1f)] public float bushClusterStrength = 0.65f;
        [Min(3f)] public float bushClusterSize = 13f;
        [Range(0f, 1f)] public float flowerClusterStrength = 0.80f;
        [Min(3f)] public float flowerClusterSize = 9f;

        [Header("Свободная полоса ОТ КРАЯ тропы (метры)")]
        [Min(0.5f)] public float bushPathClearance = 1.10f;
        [Min(0.5f)] public float flowerPathClearance = 0.80f;
        [Min(0.5f)] public float rockPathClearance = 1.20f;

        public static ForestUnderstoryCandySettings Load()
        {
            var saved = AssetDatabase.LoadAssetAtPath<ForestUnderstoryCandySettings>(AssetPath);
            if (saved != null) return saved;
            // Создаём один постоянный профиль: команды из ForestAll тоже используют его.
            EditorAssets.EnsureFolder("Assets/_Project/IslandV2");
            saved = CreateInstance<ForestUnderstoryCandySettings>();
            AssetDatabase.CreateAsset(saved, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("ISL-008: создан профиль подлеска: " + AssetPath);
            return saved;
        }

        [MenuItem("Tools/BuilderSim/Лес/Красота леса/ISL-008: создать или открыть настройки подлеска", false, 228)]
        static void CreateOrOpen()
        {
            var saved = Load();
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);
        }
    }
}
#endif
