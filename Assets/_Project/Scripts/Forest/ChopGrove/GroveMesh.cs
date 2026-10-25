// Assets/_Project/Scripts/Forest/ChopGrove/GroveMesh.cs (НЕ в Editor)
// Набор примитивов low-poly для поляны рубки: коробки, цилиндры, конусы, «камни» (икосфера с шумом), флажки.
// Всё пишется в ОДИН меш с цветом вершин (шейдер CandyToon умножает на цвет), поэтому вся поляна = 1-2 draw call.
//  • props-меши: каждая грань отдельными вершинами, значит нормали плоские (граненый «конфетный» вид как на референсе);
//  • ground-меши: вершины общие (Vert + TriUp), нормали сглаженные, цвета плавно переходят в траву.
// Направление граней выбирается само по вектору «наружу», поэтому перепутать порядок обхода нельзя.
using System.Collections.Generic;
using UnityEngine;

public sealed class GroveMesh
{
    public readonly List<Vector3> verts = new List<Vector3>(8192);
    public readonly List<Color32> cols = new List<Color32>(8192);
    public readonly List<int> tris = new List<int>(16384);

    readonly Stack<Matrix4x4> stack = new Stack<Matrix4x4>();
    Matrix4x4 m = Matrix4x4.identity;
    public System.Random rng;

    public GroveMesh(int seed) { rng = new System.Random(seed); }

    public int TriCount { get { return tris.Count / 3; } }

    // ------------------------------------------------------------ случайность и цвет
    public float Rand() { return (float)rng.NextDouble(); }
    public float Range(float a, float b) { return a + (b - a) * Rand(); }
    public Color Jit(Color c, float amt)
    {
        float k = 1f + (Rand() * 2f - 1f) * amt;
        return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);
    }
    static Color Mul(Color c, float k) { return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f); }

    // ------------------------------------------------------------ преобразования
    public void Push(Vector3 pos, Quaternion rot)
    {
        stack.Push(m);
        m = m * Matrix4x4.TRS(pos, rot, Vector3.one);
    }
    public void Pop() { m = stack.Pop(); }

    // ------------------------------------------------------------ базовые треугольники
    void Emit(Vector3 a, Vector3 b, Vector3 c, Color col)
    {
        int i = verts.Count;
        verts.Add(m.MultiplyPoint3x4(a));
        verts.Add(m.MultiplyPoint3x4(b));
        verts.Add(m.MultiplyPoint3x4(c));
        Color32 cc = col;
        cols.Add(cc); cols.Add(cc); cols.Add(cc);
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
    }

    // грань лицом в сторону outward (порядок обхода выбирается сам)
    public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col, Vector3 outward)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(n, outward) < 0f) { Vector3 t = b; b = c; c = t; }
        Emit(a, b, c, col);
    }

    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, Vector3 outward)
    {
        Tri(a, b, c, col, outward);
        Tri(a, c, d, col, outward);
    }

    // двусторонний треугольник (флажки, листья)
    public void TriBoth(Vector3 a, Vector3 b, Vector3 c, Color col)
    {
        Emit(a, b, c, col);
        Emit(a, c, b, col);
    }

    // ------------------------------------------------------------ коробка
    public void Box(Vector3 center, Vector3 size, Color col, float jit = 0.05f)
    {
        Vector3 h = size * 0.5f;
        Vector3[] c = new Vector3[8];
        for (int i = 0; i < 8; i++)
            c[i] = center + new Vector3((i & 1) != 0 ? h.x : -h.x, (i & 2) != 0 ? h.y : -h.y, (i & 4) != 0 ? h.z : -h.z);
        Color baseCol = Jit(col, jit);
        Quad(c[0], c[2], c[6], c[4], Mul(baseCol, 0.90f), Vector3.left);
        Quad(c[1], c[3], c[7], c[5], Mul(baseCol, 0.95f), Vector3.right);
        Quad(c[0], c[1], c[5], c[4], Mul(baseCol, 0.72f), Vector3.down);
        Quad(c[2], c[3], c[7], c[6], Mul(baseCol, 1.08f), Vector3.up);
        Quad(c[0], c[1], c[3], c[2], Mul(baseCol, 0.88f), Vector3.back);
        Quad(c[4], c[5], c[7], c[6], Mul(baseCol, 0.98f), Vector3.forward);
    }

    // балка между двумя точками (ось Y коробки совпадает с a→b)
    public void BoxBeam(Vector3 a, Vector3 b, float thickX, float thickZ, Color col, float jit = 0.05f)
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 0.001f) return;
        Push(a, Quaternion.FromToRotation(Vector3.up, d / len));
        Box(new Vector3(0f, len * 0.5f, 0f), new Vector3(thickX, len, thickZ), col, jit);
        Pop();
    }

    // ------------------------------------------------------------ цилиндр / конус
    // ось +Y от baseCenter. topRings / botRings: цвета колец торца от центра к краю (годовые кольца пня и бревна)
    public void Cylinder(Vector3 baseCenter, float rBot, float rTop, float h, int sides, Color col,
                         Color[] topRings = null, Color[] botRings = null, bool capTop = true, bool capBot = true, float jit = 0.05f)
    {
        sides = Mathf.Max(3, sides);
        float rot0 = Rand() * Mathf.PI;
        Vector3[] bt = new Vector3[sides], tp = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = rot0 + i / (float)sides * Mathf.PI * 2f;
            float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            bt[i] = baseCenter + new Vector3(cs * rBot, 0f, sn * rBot);
            tp[i] = baseCenter + new Vector3(cs * rTop, h, sn * rTop);
        }
        Vector3 mid = baseCenter + new Vector3(0f, h * 0.5f, 0f);
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            Color sc = Jit(col, jit);
            Vector3 face = (bt[i] + bt[j] + tp[i] + tp[j]) * 0.25f;
            Quad(bt[i], bt[j], tp[j], tp[i], sc, face - mid);
        }
        if (capTop) Cap(baseCenter + new Vector3(0f, h, 0f), rTop, sides, rot0, topRings, col, true);
        if (capBot) Cap(baseCenter, rBot, sides, rot0, botRings, col, false);
    }

    void Cap(Vector3 center, float r, int sides, float rot0, Color[] rings, Color fallback, bool up)
    {
        Vector3 outward = up ? Vector3.up : Vector3.down;
        int n = (rings != null && rings.Length > 0) ? rings.Length : 1;
        for (int k = 0; k < n; k++)
        {
            float r0 = r * k / n, r1 = r * (k + 1) / n;
            Color c = (rings != null && rings.Length > 0) ? rings[k] : Mul(fallback, up ? 1.1f : 0.7f);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                float a0 = rot0 + i / (float)sides * Mathf.PI * 2f, a1 = rot0 + j / (float)sides * Mathf.PI * 2f;
                Vector3 p1 = center + new Vector3(Mathf.Cos(a0) * r1, 0f, Mathf.Sin(a0) * r1);
                Vector3 p2 = center + new Vector3(Mathf.Cos(a1) * r1, 0f, Mathf.Sin(a1) * r1);
                if (k == 0) Tri(center, p1, p2, c, outward);
                else
                {
                    Vector3 q1 = center + new Vector3(Mathf.Cos(a0) * r0, 0f, Mathf.Sin(a0) * r0);
                    Vector3 q2 = center + new Vector3(Mathf.Cos(a1) * r0, 0f, Mathf.Sin(a1) * r0);
                    Quad(q1, p1, p2, q2, c, outward);
                }
            }
        }
    }

    public void Cone(Vector3 baseCenter, float r, float h, int sides, Color col, float jit = 0.06f, bool capBase = false)
    {
        sides = Mathf.Max(3, sides);
        Vector3 apex = baseCenter + new Vector3(0f, h, 0f);
        float rot0 = Rand() * Mathf.PI;
        Vector3 mid = baseCenter + new Vector3(0f, h * 0.33f, 0f);
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            float a0 = rot0 + i / (float)sides * Mathf.PI * 2f, a1 = rot0 + j / (float)sides * Mathf.PI * 2f;
            Vector3 p1 = baseCenter + new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r);
            Vector3 p2 = baseCenter + new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
            Tri(p1, p2, apex, Jit(col, jit), (p1 + p2 + apex) / 3f - mid);
            if (capBase) Tri(p1, p2, baseCenter, Mul(col, 0.7f), Vector3.down);
        }
    }

    // октаэдр: самый дешёвый «шарик» (лампочки, головки цветов, камешки), 8 граней
    public void Octa(Vector3 c, Vector3 r, Color col, float jit = 0.05f)
    {
        Vector3 px = c + new Vector3(r.x, 0, 0), nx = c - new Vector3(r.x, 0, 0);
        Vector3 py = c + new Vector3(0, r.y, 0), ny = c - new Vector3(0, r.y, 0);
        Vector3 pz = c + new Vector3(0, 0, r.z), nz = c - new Vector3(0, 0, r.z);
        Color b = Jit(col, jit);
        Vector3[] e = { px, pz, nx, nz };
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = e[i], bb = e[(i + 1) % 4];
            Tri(a, bb, py, Mul(b, 1.1f), (a + bb + py) / 3f - c);
            Tri(a, bb, ny, Mul(b, 0.75f), (a + bb + ny) / 3f - c);
        }
    }

    // ------------------------------------------------------------ «камень / куст»: икосфера в 1 деление (80 граней) с шумом
    static Vector3[] icoV;
    static int[] icoF;

    static void BuildIco()
    {
        if (icoV != null) return;
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        var v = new List<Vector3>
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
        };
        for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
        int[] f0 =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };
        var f = new List<int>();
        var cache = new Dictionary<long, int>();
        System.Func<int, int, int> mid = (a, b) =>
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            int idx;
            if (cache.TryGetValue(key, out idx)) return idx;
            v.Add(((v[a] + v[b]) * 0.5f).normalized);
            idx = v.Count - 1;
            cache[key] = idx;
            return idx;
        };
        for (int i = 0; i < f0.Length; i += 3)
        {
            int a = f0[i], b = f0[i + 1], c = f0[i + 2];
            int ab = mid(a, b), bc = mid(b, c), ca = mid(c, a);
            f.Add(a); f.Add(ab); f.Add(ca);
            f.Add(b); f.Add(bc); f.Add(ab);
            f.Add(c); f.Add(ca); f.Add(bc);
            f.Add(ab); f.Add(bc); f.Add(ca);
        }
        icoV = v.ToArray();
        icoF = f.ToArray();
    }

    // radius по осям (squash), noise 0..0.4 «мятость», верх светлее низа (как гранёная листва/камень на референсе)
    public void Blob(Vector3 center, Vector3 radius, Color col, float noise = 0.15f, float facet = 0.10f)
    {
        BuildIco();
        Vector3[] p = new Vector3[icoV.Length];
        for (int i = 0; i < p.Length; i++)
        {
            float k = 1f + (Rand() * 2f - 1f) * noise;
            p[i] = center + Vector3.Scale(icoV[i] * k, radius);
        }
        for (int i = 0; i < icoF.Length; i += 3)
        {
            Vector3 a = p[icoF[i]], b = p[icoF[i + 1]], c = p[icoF[i + 2]];
            Vector3 cen = (a + b + c) / 3f;
            Vector3 dir = (cen - center);
            float up = dir.sqrMagnitude > 1e-6f ? dir.normalized.y : 0f;
            Color fc = Mul(Jit(col, facet), 0.84f + 0.28f * Mathf.Clamp01(up * 0.5f + 0.5f));
            Tri(a, b, c, fc, dir);
        }
    }

    // ------------------------------------------------------------ земля (общие вершины, сглаженные нормали)
    public int Vert(Vector3 p, Color col)
    {
        verts.Add(p);
        cols.Add((Color32)col);
        return verts.Count - 1;
    }

    // треугольник по индексам, лицом вверх
    public void TriUp(int a, int b, int c)
    {
        Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
        if (n.y < 0f) { int t = b; b = c; c = t; }
        tris.Add(a); tris.Add(b); tris.Add(c);
    }

    // ------------------------------------------------------------ в Mesh
    public Mesh ToMesh(string name)
    {
        var mesh = new Mesh();
        mesh.name = name;
        mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
