// Assets/_Project/Scripts/Editor/Characters/MiraMeshKit.cs   (ТОЛЬКО в папке Editor!)
// Процедурные сетки для модели Миры: мягкая сфера (диаметр 1), тор (кольцо), усечённый цилиндр (высота 1), дуга-трубка.
// Нормали сглаженные, поэтому всё выглядит как «глина», а не как грани. Порядок вершин проверен: лицевая сторона
// там, где (b-a)x(c-a) совпадает с нормалью (так считает Unity).
using UnityEngine;

public static class MiraMeshKit
{
    // Заполняет переданный Mesh (так GUID ассета не меняется при пересборке)
    public static void Fill(Mesh m, MiraModelData.Kind kind, float a, float b, float c, float d)
    {
        m.Clear();
        switch (kind)
        {
            case MiraModelData.Kind.Sph:  Sphere(m, 12, 8); break;
            case MiraModelData.Kind.SphL: Sphere(m, 9, 6); break;
            case MiraModelData.Kind.Tor:  Torus(m, a, b, 18, 7); break;
            case MiraModelData.Kind.Cyl:  Frustum(m, a, b, 18); break;
            case MiraModelData.Kind.Arc:  Arc(m, a, b, c, d, 14, 7); break;
        }
        m.RecalculateBounds();
    }

    static void Apply(Mesh m, Vector3[] v, Vector3[] n, int[] t)
    {
        m.vertices = v;
        m.normals = n;
        m.triangles = t;
    }

    // ---------------------------------------------------------------- сфера
    static void Sphere(Mesh m, int lon, int lat)
    {
        var v = new Vector3[(lat + 1) * (lon + 1)];
        var n = new Vector3[v.Length];
        for (int i = 0; i <= lat; i++)
        {
            float phi = Mathf.PI * i / lat;
            float y = 0.5f * Mathf.Cos(phi), r = 0.5f * Mathf.Sin(phi);
            for (int j = 0; j <= lon; j++)
            {
                float th = 2f * Mathf.PI * j / lon;
                var p = new Vector3(r * Mathf.Cos(th), y, r * Mathf.Sin(th));
                v[i * (lon + 1) + j] = p;
                n[i * (lon + 1) + j] = p.sqrMagnitude > 1e-9f ? p.normalized : Vector3.up;
            }
        }
        var t = new int[lat * lon * 6];
        int k = 0;
        for (int i = 0; i < lat; i++)
            for (int j = 0; j < lon; j++)
            {
                int a = i * (lon + 1) + j, b = a + lon + 1;
                t[k++] = a; t[k++] = a + 1; t[k++] = b;
                t[k++] = a + 1; t[k++] = b + 1; t[k++] = b;
            }
        Apply(m, v, n, t);
    }

    // ---------------------------------------------------------------- тор в плоскости XZ (ось Y): R = радиус кольца, r = радиус трубки
    static void Torus(Mesh m, float R, float r, int U, int W)
    {
        var v = new Vector3[(U + 1) * (W + 1)];
        var n = new Vector3[v.Length];
        for (int i = 0; i <= U; i++)
        {
            float u = 2f * Mathf.PI * i / U, cu = Mathf.Cos(u), su = Mathf.Sin(u);
            for (int j = 0; j <= W; j++)
            {
                float w = 2f * Mathf.PI * j / W, cv = Mathf.Cos(w), sv = Mathf.Sin(w);
                v[i * (W + 1) + j] = new Vector3((R + r * cv) * cu, r * sv, (R + r * cv) * su);
                n[i * (W + 1) + j] = new Vector3(cv * cu, sv, cv * su);
            }
        }
        var t = new int[U * W * 6];
        int k = 0;
        for (int i = 0; i < U; i++)
            for (int j = 0; j < W; j++)
            {
                int a = i * (W + 1) + j, b = a + W + 1;
                t[k++] = a; t[k++] = a + 1; t[k++] = b;
                t[k++] = a + 1; t[k++] = b + 1; t[k++] = b;
            }
        Apply(m, v, n, t);
    }

    // ---------------------------------------------------------------- усечённый цилиндр высотой 1 (y от -0.5 до 0.5): rt сверху, rb снизу
    static void Frustum(Mesh m, float rt, float rb, int S)
    {
        var v = new System.Collections.Generic.List<Vector3>();
        var n = new System.Collections.Generic.List<Vector3>();
        var t = new System.Collections.Generic.List<int>();
        float slope = rb - rt;
        for (int ring = 0; ring < 2; ring++)
        {
            float y = ring == 0 ? 0.5f : -0.5f, rad = ring == 0 ? rt : rb;
            for (int j = 0; j <= S; j++)
            {
                float th = 2f * Mathf.PI * j / S, c = Mathf.Cos(th), s = Mathf.Sin(th);
                v.Add(new Vector3(rad * c, y, rad * s));
                n.Add(new Vector3(c, slope, s).normalized);
            }
        }
        for (int j = 0; j < S; j++)
        {
            int a = j, b = j + S + 1;
            t.Add(a); t.Add(a + 1); t.Add(b);
            t.Add(a + 1); t.Add(b + 1); t.Add(b);
        }
        for (int cap = 0; cap < 2; cap++)
        {
            float y = cap == 0 ? 0.5f : -0.5f, rad = cap == 0 ? rt : rb, ny = cap == 0 ? 1f : -1f;
            int baseIdx = v.Count;
            v.Add(new Vector3(0f, y, 0f)); n.Add(new Vector3(0f, ny, 0f));
            for (int j = 0; j <= S; j++)
            {
                float th = 2f * Mathf.PI * j / S;
                v.Add(new Vector3(rad * Mathf.Cos(th), y, rad * Mathf.Sin(th)));
                n.Add(new Vector3(0f, ny, 0f));
            }
            for (int j = 0; j < S; j++)
            {
                if (cap == 0) { t.Add(baseIdx); t.Add(baseIdx + 2 + j); t.Add(baseIdx + 1 + j); }
                else          { t.Add(baseIdx); t.Add(baseIdx + 1 + j); t.Add(baseIdx + 2 + j); }
            }
        }
        Apply(m, v.ToArray(), n.ToArray(), t.ToArray());
    }

    // ---------------------------------------------------------------- дуга-трубка в плоскости XY: радиус R, трубка r, углы a0..a1 (градусы)
    static void Arc(Mesh m, float R, float r, float a0, float a1, int U, int W)
    {
        var v = new Vector3[(U + 1) * (W + 1)];
        var n = new Vector3[v.Length];
        for (int i = 0; i <= U; i++)
        {
            float al = Mathf.Deg2Rad * (a0 + (a1 - a0) * i / U), ca = Mathf.Cos(al), sa = Mathf.Sin(al);
            for (int j = 0; j <= W; j++)
            {
                float w = 2f * Mathf.PI * j / W, cv = Mathf.Cos(w), sv = Mathf.Sin(w);
                v[i * (W + 1) + j] = new Vector3((R + r * cv) * ca, (R + r * cv) * sa, r * sv);
                n[i * (W + 1) + j] = new Vector3(cv * ca, cv * sa, sv);
            }
        }
        var t = new int[U * W * 6];
        int k = 0;
        for (int i = 0; i < U; i++)
            for (int j = 0; j < W; j++)
            {
                int a = i * (W + 1) + j, b = a + W + 1;
                t[k++] = a; t[k++] = b; t[k++] = a + 1;
                t[k++] = a + 1; t[k++] = b; t[k++] = b + 1;
            }
        Apply(m, v, n, t);
    }
}
