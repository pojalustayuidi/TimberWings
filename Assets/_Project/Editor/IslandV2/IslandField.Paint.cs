// Assets/_Project/Editor/IslandV2/IslandField.Paint.cs
// Остров v2, этап 2 (задача F): помощники для окраски земли. Часть partial-класса IslandField (основная часть: IslandField.cs).
// Здесь только чтение данных раскладки, ничего не меняет ни высоту, ни проходимость, ни расстановку деревьев.
//
// Методы:
//   Step(a, b, x)                   плавный порог как smoothstep в шейдере: 0 при x<=a, 1 при x>=b (НЕ Mathf.SmoothStep, тот работает иначе)
//   ForestNoise(x, z)               шум леса (его же использует расстановка деревьев AllowedForTree)
//   ForestWeight(x, z)              0..1: насколько здесь «густой лес» для окраски (шум леса минус поляны, тропы, здания)
//   PathEdgeNearest(x, z, d1, d2)   расстояния от края двух ближайших троп (меньше нуля = на тропе)
//   PlazaCenter                     центр площадки у костра (CAMPFIRE_Point)
using UnityEngine;

namespace IslandV2
{
    public partial class IslandField
    {
        // Mathf.SmoothStep(from, to, t) в Unity интерполирует значения from..to по t в 0..1, а не делает порог. Для порогов используем этот метод.
        public static float Step(float a, float b, float x)
        {
            if (b - a < 1e-5f) return x >= b ? 1f : 0f;
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        public float ForestNoise(float x, float z)
        {
            float sc = Mathf.Max(5f, L.forestNoiseScale);
            return Mathf.PerlinNoise(x / sc + sz * 1.3f, z / sc + sx * 0.7f);
        }

        public float ForestWeight(float x, float z)
        {
            float n = ForestNoise(x, z);
            float soft = Mathf.Max(0.005f, L.forestPaintSoft);
            float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(L.forestThreshold - soft, L.forestThreshold + soft, n));
            if (w <= 0f) return 0f;

            w *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(L.pathClear * 0.55f, L.pathClear * 1.25f, PathAxisDistance(x, z)));
            if (w <= 0f) return 0f;

            float bd = float.MaxValue;
            for (int i = 0; i < L.anchors.Count; i++)
            {
                if (L.anchors[i].kind != IslandLayout.AnchorKind.Building) continue;
                bd = Mathf.Min(bd, RectDistance(L.anchors[i], x, z));
            }
            if (bd < float.MaxValue)
                w *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(L.buildingClear * 0.5f, L.buildingClear * 1.4f, bd));

            var p = new Vector2(x, z);
            for (int i = 0; i < L.meadows.Count; i++)
            {
                float r = Mathf.Max(0.5f, L.meadows[i].radius);
                w *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1.25f, Vector2.Distance(L.meadows[i].pos, p) / r));
            }
            return w;
        }

        public void PathEdgeNearest(float x, float z, out float d1, out float d2)
        {
            var p = new Vector2(x, z);
            d1 = float.MaxValue; d2 = float.MaxValue;
            for (int i = 0; i < pathDense.Count; i++)
            {
                int si;
                float d = DistToPolyline(pathDense[i], p, out si) - pathHalf[i];
                if (d < d1) { d2 = d1; d1 = d; }
                else if (d < d2) d2 = d;
            }
        }

        public Vector2 PlazaCenter
        {
            get
            {
                var a = L.FindAnchor("CAMPFIRE_Point");
                return a != null ? AnchorWorld(a) : Vector2.zero;
            }
        }
    }
}
