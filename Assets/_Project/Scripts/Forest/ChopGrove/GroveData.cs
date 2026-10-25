// Assets/_Project/Scripts/Forest/ChopGrove/GroveData.cs (НЕ в Editor)
// Палитра поляны, настройки троп/площадок и математика «расстояние до тропы» (для расстановки деревьев и мелочи).
using System;
using System.Collections.Generic;
using UnityEngine;

public static class GrovePal
{
    // дерево
    public static readonly Color Bark = new Color(0.45f, 0.30f, 0.18f);
    public static readonly Color BarkDark = new Color(0.34f, 0.22f, 0.13f);
    public static readonly Color Wood = new Color(0.83f, 0.60f, 0.34f);
    public static readonly Color WoodLight = new Color(0.94f, 0.76f, 0.48f);
    public static readonly Color WoodRing = new Color(0.76f, 0.52f, 0.28f);
    public static readonly Color Plank = new Color(0.72f, 0.52f, 0.31f);
    public static readonly Color PlankDark = new Color(0.56f, 0.39f, 0.23f);
    public static readonly Color Roof = new Color(0.64f, 0.47f, 0.30f);
    public static readonly Color RoofDark = new Color(0.52f, 0.37f, 0.24f);
    public static readonly Color Rope = new Color(0.82f, 0.68f, 0.42f);
    // камень, металл
    public static readonly Color Stone = new Color(0.62f, 0.57f, 0.76f);
    public static readonly Color StoneDark = new Color(0.48f, 0.44f, 0.64f);
    public static readonly Color Rock = new Color(0.60f, 0.54f, 0.82f);
    public static readonly Color Metal = new Color(0.62f, 0.66f, 0.72f);
    public static readonly Color MetalDark = new Color(0.30f, 0.33f, 0.40f);
    // зелень
    public static readonly Color Grass = new Color(0.52f, 0.76f, 0.28f);
    public static readonly Color GrassDark = new Color(0.34f, 0.62f, 0.24f);
    public static readonly Color Bush = new Color(0.32f, 0.68f, 0.30f);
    public static readonly Color BushTeal = new Color(0.17f, 0.56f, 0.52f);
    public static readonly Color Moss = new Color(0.45f, 0.72f, 0.30f);
    // земля
    public static readonly Color DirtCenter = new Color(0.90f, 0.74f, 0.48f);
    public static readonly Color DirtMid = new Color(0.83f, 0.65f, 0.40f);
    public static readonly Color DirtEdge = new Color(0.71f, 0.53f, 0.32f);
    public static readonly Color DirtPatch = new Color(0.66f, 0.49f, 0.30f);
    // огни, ткань
    public static readonly Color Lamp = new Color(1.00f, 0.86f, 0.52f);
    public static readonly Color[] Flags =
    {
        new Color(0.25f, 0.74f, 0.70f), new Color(0.97f, 0.62f, 0.30f), new Color(0.97f, 0.52f, 0.62f),
        new Color(0.99f, 0.83f, 0.32f), new Color(0.92f, 0.40f, 0.36f)
    };
    public static readonly Color[] Bulbs =
    {
        new Color(1f, 0.62f, 0.72f), new Color(1f, 0.86f, 0.42f), new Color(0.74f, 0.92f, 0.60f)
    };
    public static readonly Color[] Flowers =
    {
        new Color(1f, 0.86f, 0.30f), new Color(1f, 0.58f, 0.70f), new Color(0.98f, 0.98f, 0.92f), new Color(0.72f, 0.58f, 0.95f)
    };
}

// Тропа: ломаная по точкам (локальные метры от PAD, X вправо, Z вглубь поляны; вход с -Z), сглаживается сплайном.
[Serializable]
public class GrovePath
{
    public string name = "Тропа";
    public Vector2[] points;
    public float width = 2f;
    [Tooltip("Начало тропы растворяется в траве (конец уходит в лес)")] public bool fadeStart;
    [Tooltip("Конец тропы растворяется в траве")] public bool fadeEnd;

    [NonSerialized] public List<Vector2> samples;

    public static GrovePath[] Defaults()
    {
        return new[]
        {
            new GrovePath { name = "Главная от ворот", width = 3.2f, fadeStart = true, points = new[] {
                new Vector2(0.6f, -17f), new Vector2(0.3f, -12.5f), new Vector2(0f, -8.2f), new Vector2(-0.7f, -5.0f), new Vector2(-0.3f, -2.2f), new Vector2(0f, 0.8f) } },
            new GrovePath { name = "К пню с топором", width = 1.9f, points = new[] {
                new Vector2(-0.8f, 0.6f), new Vector2(-2.0f, 0.95f), new Vector2(-3.0f, 1.4f) } },
            new GrovePath { name = "К навесу", width = 1.9f, points = new[] {
                new Vector2(0.8f, 1.2f), new Vector2(2.4f, 2.6f), new Vector2(3.8f, 3.8f) } },
            new GrovePath { name = "К телеге", width = 1.8f, points = new[] {
                new Vector2(0.8f, 0.2f), new Vector2(1.4f, -0.05f), new Vector2(1.9f, -0.15f) } },
            new GrovePath { name = "Вглубь леса", width = 2.2f, fadeEnd = true, points = new[] {
                new Vector2(0.2f, 2.0f), new Vector2(0.1f, 5.0f), new Vector2(-0.8f, 8.0f), new Vector2(-1.4f, 12.5f) } }
        };
    }

    // Catmull-Rom, шаг около step метров
    public List<Vector2> Sample(float step)
    {
        if (samples != null) return samples;
        samples = new List<Vector2>();
        if (points == null || points.Length < 2) return samples;
        int n = points.Length;
        for (int i = 0; i < n - 1; i++)
        {
            Vector2 p0 = points[Mathf.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(i + 2, n - 1)];
            int seg = Mathf.Max(2, Mathf.CeilToInt((p2 - p1).magnitude / step));
            for (int k = 0; k < seg; k++)
            {
                float t = k / (float)seg, t2 = t * t, t3 = t2 * t;
                samples.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        samples.Add(points[n - 1]);
        return samples;
    }

    // расстояние от точки до оси тропы минус половина ширины (<0 = на тропе)
    public float Clearance(Vector2 p)
    {
        var s = Sample(0.5f);
        float best = float.MaxValue;
        for (int i = 0; i < s.Count - 1; i++)
        {
            Vector2 a = s[i], b = s[i + 1], ab = b - a;
            float l2 = ab.sqrMagnitude;
            float t = l2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2) : 0f;
            float d = (p - (a + ab * t)).magnitude;
            if (d < best) best = d;
        }
        return best - width * 0.5f;
    }
}

// Круглая площадка земли (центральная площадь, пятна у пня, навеса, телеги).
[Serializable]
public class GroveDisc
{
    public string name = "Площадка";
    public Vector2 center;
    public float radius = 2f;

    public static GroveDisc[] Defaults()
    {
        return new[]
        {
            new GroveDisc { name = "Площадь", center = new Vector2(0f, 0.8f), radius = 4.4f },
            new GroveDisc { name = "У пня с топором", center = new Vector2(-4.6f, 2.4f), radius = 2.3f },
            new GroveDisc { name = "У навеса", center = new Vector2(5.6f, 5.4f), radius = 2.7f },
            new GroveDisc { name = "У телеги", center = new Vector2(4.2f, -0.6f), radius = 2.4f },
            new GroveDisc { name = "У ворот", center = new Vector2(0f, -6.4f), radius = 2.3f }
        };
    }

    public float Clearance(Vector2 p) { return (p - center).magnitude - radius; }
}
