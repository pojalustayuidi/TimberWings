// Assets/_Project/Editor/TWRiver/TWRiverSpec.cs
// Общие числа речной зоны: длина скального истока и затенение воды у родника.
using UnityEngine;

namespace IslandV2
{
    internal static class TWRiverSpec
    {
        public const int Revision = 4;
        public const string RootName = "TW_RIVER_ART";
        public const string AssetFolder = "Assets/_Project/IslandV2/RiverArt";

        public const float MouthDist = 9.0f;     // протяжённость скального истока вдоль реки
        public const float CaveFade = 3.0f;      // на каком расстоянии за устьем темнота воды сходит на нет

        // Unity Mathf.SmoothStep interpolates VALUES; it does not normalize a range.
        public static float Smooth(float from, float to, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));
        }

        public static readonly string[] LegacyRoots =
        {
            "TW_RIVER_OVERHAUL_V1", "TW_RIVER_OVERHAUL_V2", "TW_RIVER_OVERHAUL_V3"
        };

        // Исток есть, если река приходит на остров снаружи (вход на контуре)
        public static bool HasSource(IslandField f)
        {
            foreach (var e in f.RiverEdges) if (!e.isOutlet) return true;
            return false;
        }

        public static float CaveLength(IslandField f) { return HasSource(f) ? MouthDist : 0f; }

        // 1 внутри грота, плавно к 0 за устьем. along: метры вдоль течения от входа реки
        public static float CaveShade(float along, float caveLen)
        {
            if (caveLen <= 0.1f) return 0f;
            if (along <= caveLen) return 1f;
            return 1f - Smooth(caveLen, caveLen + CaveFade, along);
        }
    }
}

