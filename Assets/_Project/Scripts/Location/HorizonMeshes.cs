using System.Collections.Generic;
using UnityEngine;

// Static geometry for the scenery on the horizon. Each island includes its
// trees and loose rocks in its mesh. The airship has a separate propeller.
internal static class HorizonMeshes
{
    internal enum Landform { Shelf, Spire, Fragments }

    static readonly Color Canvas = new Color32(233, 213, 163, 255);
    static readonly Color CanvasShade = new Color32(188, 165, 119, 255);
    static readonly Color Red = new Color32(153, 70, 54, 255);
    static readonly Color RedShade = new Color32(104, 49, 44, 255);
    static readonly Color Brass = new Color32(213, 157, 82, 255);
    static readonly Color Timber = new Color32(116, 69, 46, 255);
    static readonly Color TimberShade = new Color32(63, 46, 41, 255);
    static readonly Color Rope = new Color32(102, 81, 62, 255);

    internal static readonly Vector3 PropellerPivot = new Vector3(0f, -2.69f, -6.25f);

    internal static Mesh Airship()
    {
        var model = new Surface();
        AddEnvelope(model);
        AddTail(model);
        AddGondola(model);

        for (int side = -1; side <= 1; side += 2)
        {
            model.Beam(new Vector3(2.75f * side, 0.85f, -4.3f), new Vector3(1.40f * side, -2.34f, -2.8f),
                0.064f, 0.047f, Rope);
            model.Beam(new Vector3(2.95f * side, 0.43f, 0.8f), new Vector3(1.53f * side, -2.31f, 0.4f),
                0.060f, 0.046f, Rope);
            model.Beam(new Vector3(2.48f * side, 0.75f, 4.8f), new Vector3(1.09f * side, -2.31f, 3.7f),
                0.064f, 0.047f, Rope);
        }

        Vector3 shaft = PropellerPivot + Vector3.forward * 1.18f;
        model.Beam(shaft, PropellerPivot, 0.64f, 0.31f, TimberShade, 8);
        model.Beam(PropellerPivot + Vector3.forward * 0.22f, PropellerPivot - Vector3.forward * 0.12f,
            0.42f, 0.24f, Brass, 8);
        return model.Bake("TimberWings Airship");
    }

    static void AddEnvelope(Surface model)
    {
        const int panels = 14;
        float[] z = { -10f, -9.2f, -8.0f, -7.6f, -7.0f, -4.9f, -2.4f, 0f, 2.9f, 5.7f, 6.5f, 6.92f, 8.38f, 9.4f };
        float[] radius = { 0f, 1.25f, 2.08f, 2.35f, 2.67f, 3.26f, 3.69f, 3.83f, 3.62f, 2.91f, 2.65f, 2.46f, 1.46f, 0f };
        var rings = new Vector3[z.Length, panels];
        for (int row = 0; row < z.Length; row++)
        for (int panel = 0; panel < panels; panel++)
        {
            float angle = (panel + 0.5f) * (2f * Mathf.PI / panels);
            rings[row, panel] = new Vector3(Mathf.Cos(angle) * radius[row],
                2.25f + Mathf.Sin(angle) * radius[row] * 0.86f, z[row]);
        }

        for (int row = 0; row < z.Length - 1; row++)
        for (int panel = 0; panel < panels; panel++)
        {
            int next = (panel + 1) % panels;
            Color cloth = panel % 2 == 0 ? Canvas : Color.Lerp(Canvas, CanvasShade, 0.18f);
            bool lowerPanel = panel >= 8 && panel <= 12;
            if (lowerPanel) cloth = Color.Lerp(cloth, CanvasShade, 0.48f);
            if (row == 2 || row == 10) cloth = Brass;
            if (row == 3 || row == 11) cloth = lowerPanel ? RedShade : Red;
            Vector3 middle = new Vector3(0f, 2.25f, (z[row] + z[row + 1]) * 0.5f);
            if (row != 0)
                model.Face(rings[row, panel], rings[row, next], rings[row + 1, next], cloth, middle);
            if (row != z.Length - 2)
                model.Face(rings[row, panel], rings[row + 1, next], rings[row + 1, panel], cloth, middle);
        }
    }

    static void AddTail(Surface model)
    {
        model.Panel(new Vector3(0f, 3.8f, -7.35f), new Vector3(0f, 6.02f, -9.9f),
            new Vector3(0f, 5.63f, -10.6f), new Vector3(0f, 2.62f, -9.83f),
            Vector3.right, 0.115f, Red);
        model.Beam(new Vector3(0f, 3.74f, -7.4f), new Vector3(0f, 6.04f, -9.9f), 0.055f, 0.052f, Brass);
        for (int side = -1; side <= 1; side += 2)
        {
            model.Panel(new Vector3(0.90f * side, 1.94f, -7.50f), new Vector3(3.15f * side, 2.24f, -9.35f),
                new Vector3(2.85f * side, 2.12f, -10.08f), new Vector3(0.52f * side, 1.8f, -9.49f),
                Vector3.up, 0.10f, RedShade);
        }
    }

    static void AddGondola(Surface model)
    {
        float[] z = { -5.45f, -4.59f, -3.72f, -2.65f, 2.76f, 3.93f, 4.88f, 5.56f };
        float[] width = { 0.09f, 0.65f, 1.14f, 1.55f, 1.55f, 1.23f, 0.76f, 0.09f };
        Vector2[] section =
        {
            new Vector2(-1f, 0f), new Vector2(-0.83f, -0.43f), new Vector2(-0.41f, -0.84f),
            new Vector2(0.41f, -0.84f), new Vector2(0.83f, -0.43f), new Vector2(1f, 0f)
        };
        var rings = new Vector3[z.Length, section.Length];
        for (int row = 0; row < z.Length; row++)
        for (int strip = 0; strip < section.Length; strip++)
        {
            float belly = Mathf.Clamp(width[row] + 0.16f, 0.18f, 1f);
            rings[row, strip] = new Vector3(section[strip].x * width[row],
                -2.37f + section[strip].y * belly + Mathf.Abs(z[row]) * 0.045f, z[row]);
        }

        for (int row = 0; row < z.Length - 1; row++)
        {
            Vector3 middle = new Vector3(0f, -2.56f, (z[row] + z[row + 1]) * 0.5f);
            for (int strip = 0; strip < section.Length; strip++)
            {
                int next = (strip + 1) % section.Length;
                Color wood = strip == 2 ? TimberShade : strip == 5 ? TimberShade : Timber;
                model.Quad(rings[row, strip], rings[row, next], rings[row + 1, next], rings[row + 1, strip],
                    wood, middle);
            }
        }

        model.Box(new Vector3(0f, -1.92f, 0.15f), new Vector3(2.21f, 0.93f, 5.20f), Timber);
        model.Box(new Vector3(0f, -1.39f, 0.15f), new Vector3(2.43f, 0.20f, 5.47f), RedShade);
        model.Box(new Vector3(0f, -1.23f, 0.15f), new Vector3(2.10f, 0.20f, 5.22f), Red);
        Color glass = new Color(1f, 0.71f, 0.32f, 0.15f);
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < z.Length - 1; i++)
            {
                int strip = side < 0 ? 0 : 5;
                model.Beam(rings[i, strip], rings[i + 1, strip], 0.083f, 0.083f, Brass);
            }
            for (int window = 0; window < 4; window++)
            {
                float at = -1.68f + window * 1.15f;
                model.Box(new Vector3(1.114f * side, -1.88f, at), new Vector3(0.074f, 0.60f, 0.82f), TimberShade);
                model.Box(new Vector3(1.157f * side, -1.88f, at), new Vector3(0.047f, 0.43f, 0.64f), glass);
                model.Box(new Vector3(1.180f * side, -1.88f, at), new Vector3(0.037f, 0.45f, 0.070f), Brass);
            }
        }
        model.Beam(new Vector3(0f, -2.28f, 5.23f), new Vector3(0f, -2.70f, 5.23f),
            0.24f, 0.19f, glass, 6);
        model.Box(new Vector3(0f, -2.25f, 5.23f), new Vector3(0.43f, 0.13f, 0.43f), Brass);
    }

    internal static Mesh Propeller()
    {
        var model = new Surface();
        for (int blade = 0; blade < 3; blade++)
        {
            Quaternion spin = Quaternion.AngleAxis(blade * 120f, Vector3.forward);
            model.Panel(spin * new Vector3(-0.095f, 0.19f, 0f),
                spin * new Vector3(-0.18f, 1.33f, 0.10f),
                spin * new Vector3(0.15f, 1.42f, 0.08f),
                spin * new Vector3(0.25f, 0.35f, -0.055f),
                Vector3.forward, 0.07f, Timber);
        }
        model.Beam(Vector3.back * 0.10f, Vector3.forward * 0.18f, 0.2f, 0.24f, Brass, 8);
        return model.Bake("TimberWings Airship Propeller");
    }

    internal static Mesh Island(IslandSource source, Landform landform)
    {
        var model = new Surface();
        switch (landform)
        {
            case Landform.Shelf:
                AddRock(model, source, Vector3.zero, new Vector2(1.36f, 0.85f), 1.7f,
                    new Vector2(-0.32f, 0.19f), true, 121);
                AddTrees(model, 7, 181);
                break;
            case Landform.Spire:
                AddRock(model, source, Vector3.zero, new Vector2(0.72f, 0.66f), 2.55f,
                    new Vector2(0.29f, 0.09f), true, 144);
                AddTrees(model, 3, 265);
                break;
            default:
                AddRock(model, source, Vector3.zero, new Vector2(0.77f, 0.63f), 1.61f,
                    new Vector2(-0.16f, 0.21f), true, 196);
                AddRock(model, source, new Vector3(1.05f, -0.61f, -0.15f), new Vector2(0.29f, 0.26f),
                    0.92f, new Vector2(0.10f, -0.06f), false, 249);
                AddRock(model, source, new Vector3(-0.92f, -0.83f, 0.37f), new Vector2(0.28f, 0.24f),
                    0.58f, new Vector2(0.05f, -0.05f), false, 317);
                AddTrees(model, 2, 313);
                break;
        }
        return model.Bake("Horizon " + landform);
    }

    static void AddRock(Surface model, IslandSource source, Vector3 center, Vector2 extent, float depth,
        Vector2 lean, bool grass, int seed)
    {
        const int sides = 12;
        float[] width = { 0.91f, 1f, 0.95f, 0.83f, 0.64f, 0.49f, 0.25f, 0f };
        float[] level = { 0f, 0.065f, 0.19f, 0.32f, 0.54f, 0.72f, 0.87f, 1f };
        Color[] stratum = { grass ? source.grassDark : source.rockWarm, grass ? source.grassDark : source.rockWarm,
            source.soil, source.rockWarm, source.rockMid, source.rockWarm, source.rockDark, source.rockDark };
        var random = new System.Random(seed);
        var vertices = new Vector3[width.Length, sides];
        var colors = new Color[width.Length, sides];
        for (int side = 0; side < sides; side++)
        {
            float angle = (side + (float)random.NextDouble() * 0.3f) * (2f * Mathf.PI / sides);
            float mass = 0.77f + 0.47f * (float)random.NextDouble();
            for (int row = 0; row < width.Length; row++)
            {
                float down = level[row];
                float widthAt = width[row] * mass * (0.93f + 0.13f * (float)random.NextDouble());
                float irregular = row == width.Length - 1 ? 0f : (float)random.NextDouble() * 0.082f - 0.024f;
                vertices[row, side] = center + new Vector3(Mathf.Cos(angle) * extent.x * widthAt + down * lean.x,
                    (-down + irregular) * depth, Mathf.Sin(angle) * extent.y * widthAt + down * lean.y);
                float colorVariation = 0.91f + 0.16f * (float)random.NextDouble();
                Color color = stratum[row] * colorVariation;
                color.a = 1f;
                colors[row, side] = color;
            }
        }

        Vector3 top = center + Vector3.up * 0.12f;
        for (int side = 0; side < sides; side++)
        {
            int next = (side + 1) % sides;
            Color cap = grass ? source.grassTop : source.rockMid;
            model.Face(top, vertices[0, next], vertices[0, side], cap, colors[0, next], colors[0, side],
                center - Vector3.up * 0.2f);
            for (int row = 0; row < width.Length - 1; row++)
            {
                float at = (level[row] + level[row + 1]) * 0.5f;
                Vector3 middle = center + new Vector3(at * lean.x, -at * depth, at * lean.y);
                model.Face(vertices[row, side], vertices[row, next], vertices[row + 1, next],
                    colors[row, side], colors[row, next], colors[row + 1, next], middle);
                if (row < width.Length - 2)
                    model.Face(vertices[row, side], vertices[row + 1, next], vertices[row + 1, side],
                        colors[row, side], colors[row + 1, next], colors[row + 1, side], middle);
            }
        }
    }

    static void AddTrees(Surface model, int count, int seed)
    {
        var rnd = new System.Random(seed);
        Color needles = new Color32(68, 119, 93, 255);
        Color young = new Color32(106, 146, 104, 255);
        for (int i = 0; i < count; i++)
        {
            float a = (i + (float)rnd.NextDouble() * 0.45f) * (2f * Mathf.PI / count);
            float spread = 0.23f + 0.19f * (float)rnd.NextDouble();
            var foot = new Vector3(Mathf.Cos(a) * spread * 1.3f, 0.055f, Mathf.Sin(a) * spread * 0.74f);
            float h = 0.29f + (float)rnd.NextDouble() * 0.20f;
            model.Beam(foot, foot + Vector3.up * (h * 0.64f), 0.025f, 0.014f, TimberShade, 5);
            float branch = h * 0.32f;
            model.Beam(foot + Vector3.up * (h * 0.21f), foot + Vector3.up * (h * 0.81f), branch, 0f, needles, 6);
            model.Beam(foot + Vector3.up * (h * 0.52f), foot + Vector3.up * h, branch * 0.76f, 0f, young, 6);
        }
    }

    sealed class Surface
    {
        readonly List<Vector3> positions = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> indices = new List<int>();

        internal void Face(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc, Vector3 middle) =>
            IslandUnderside.Face(positions, colors, indices, a, b, c, ca, cb, cc, middle);

        internal void Face(Vector3 a, Vector3 b, Vector3 c, Color color, Vector3 middle) =>
            Face(a, b, c, color, color, color, middle);

        internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, Vector3 middle)
        {
            Face(a, b, c, color, middle);
            Face(a, c, d, color, middle);
        }

        internal void Panel(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float thickness, Color color)
        {
            Vector3 offset = normal * (thickness * 0.5f);
            Vector3 middle = (a + b + c + d) * 0.25f;
            Quad(a + offset, b + offset, c + offset, d + offset, color, middle);
            Quad(a - offset, b - offset, c - offset, d - offset, color, middle);
            Quad(a + offset, b + offset, b - offset, a - offset, color, middle);
            Quad(b + offset, c + offset, c - offset, b - offset, color, middle);
            Quad(c + offset, d + offset, d - offset, c - offset, color, middle);
            Quad(d + offset, a + offset, a - offset, d - offset, color, middle);
        }

        internal void Box(Vector3 center, Vector3 size, Color color)
        {
            var x = Vector3.right * (size.x * 0.5f);
            var z = Vector3.forward * (size.z * 0.5f);
            Panel(center - x - z, center - x + z, center + x + z, center + x - z, Vector3.up, size.y, color);
        }

        internal void Beam(Vector3 from, Vector3 to, float startRadius, float endRadius, Color color, int sides = 5)
        {
            Vector3 axis = (to - from).normalized;
            Vector3 right = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 up = Vector3.Cross(axis, right);
            Vector3 middle = (from + to) * 0.5f;
            for (int i = 0; i < sides; i++)
            {
                float a = i * (2f * Mathf.PI / sides);
                float b = (i + 1) * (2f * Mathf.PI / sides);
                Vector3 ba = right * Mathf.Cos(a) + up * Mathf.Sin(a);
                Vector3 bb = right * Mathf.Cos(b) + up * Mathf.Sin(b);
                Vector3 tipB = to + bb * endRadius;
                Vector3 baseA = from + ba * startRadius;
                Vector3 baseB = from + bb * startRadius;
                Face(baseA, baseB, tipB, color, middle);
                Face(from, baseB, baseA, color, middle);
                if (endRadius <= 0f) continue;
                Vector3 tipA = to + ba * endRadius;
                Face(baseA, tipB, tipA, color, middle);
                Face(to, tipA, tipB, color, middle);
            }
        }

        internal Mesh Bake(string name) => IslandUnderside.ToMesh(name, positions, colors, indices);
    }
}
