#if UNITY_EDITOR
// Assets/_Project/Editor/Location/Builder/LocationGeo.cs
// Примитивы, сборщик мешей, данные троп и площадок.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class LocationBuilder
{
    sealed class Geo
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 center)
        {
            Vector3 nr = Vector3.Cross(b - a, c - a);
            Vector3 mid = (a + b + c) / 3f;
            if (Vector3.Dot(nr, mid - center) < 0f) { Vector3 t = b; b = c; c = t; nr = -nr; }
            if (nr.sqrMagnitude < 1e-12f) return;
            nr.Normalize();
            v.Add(a); v.Add(b); v.Add(c);
            n.Add(nr); n.Add(nr); n.Add(nr);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 center)
        {
            Tri(a, b, c, center);
            Tri(a, c, d, center);
        }
    }

    // Все примитивы вписаны в единичный куб с центром в начале координат.
    static class Prim
    {
        public static Geo Box()
        {
            var g = new Geo();
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++)
                c[i] = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
            int[][] f =
            {
                new[] { 0, 2, 6, 4 }, new[] { 1, 3, 7, 5 }, new[] { 0, 1, 5, 4 },
                new[] { 2, 3, 7, 6 }, new[] { 0, 1, 3, 2 }, new[] { 4, 5, 7, 6 }
            };
            foreach (var q in f) g.Quad(c[q[0]], c[q[1]], c[q[2]], c[q[3]], Vector3.zero);
            return g;
        }

        public static Geo Ico(int variant)
        {
            var rnd = new System.Random(7000 + variant * 31);
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < v.Length; i++)
                v[i] = v[i].normalized * 0.5f * (0.84f + (float)rnd.NextDouble() * 0.30f);
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            var g = new Geo();
            for (int i = 0; i < f.Length; i += 3) g.Tri(v[f[i]], v[f[i + 1]], v[f[i + 2]], Vector3.zero);
            return g;
        }

        static float Hash(Vector3 p, float seed)
        {
            float v = Mathf.Sin(Vector3.Dot(p, new Vector3(12.9898f, 78.233f, 37.719f)) * 3.1f + seed) * 43758.5453f;
            return v - Mathf.Floor(v);
        }

        // икосаэдр с одним подразбиением (80 граней), неровная «скальная» поверхность
        public static Geo IcoSub(int variant, float jitter)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            float seed = variant * 17.3f;
            Func<Vector3, Vector3> J = p =>
            {
                Vector3 n = p.normalized;
                return n * 0.5f * (1f + jitter * (Hash(n, seed) * 2f - 1f));
            };
            var g = new Geo();
            for (int i = 0; i < f.Length; i += 3)
            {
                Vector3 a = v[f[i]].normalized, b = v[f[i + 1]].normalized, c = v[f[i + 2]].normalized;
                Vector3 ab = (a + b).normalized, bc = (b + c).normalized, ca = (c + a).normalized;
                g.Tri(J(a), J(ab), J(ca), Vector3.zero);
                g.Tri(J(ab), J(b), J(bc), Vector3.zero);
                g.Tri(J(ca), J(bc), J(c), Vector3.zero);
                g.Tri(J(ab), J(bc), J(ca), Vector3.zero);
            }
            return g;
        }

        public static Geo Cyl(int sides)
        {
            var g = new Geo();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * 0.5f, -0.5f, Mathf.Sin(a0) * 0.5f);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * 0.5f, -0.5f, Mathf.Sin(a1) * 0.5f);
                Vector3 t0 = b0 + Vector3.up, t1 = b1 + Vector3.up;
                g.Quad(b0, b1, t1, t0, Vector3.zero);
                g.Tri(new Vector3(0, 0.5f, 0), t0, t1, Vector3.zero);
            }
            return g;
        }

        public static Geo Cone(int sides)
        {
            var g = new Geo();
            Vector3 apex = new Vector3(0, 0.5f, 0);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * 0.5f, -0.5f, Mathf.Sin(a0) * 0.5f);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * 0.5f, -0.5f, Mathf.Sin(a1) * 0.5f);
                g.Tri(apex, b0, b1, Vector3.zero);
            }
            return g;
        }
    }

    sealed class Acc
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<int> t = new List<int>();
    }

    sealed class MeshBatch
    {
        readonly Dictionary<int, Acc> _mats = new Dictionary<int, Acc>();

        public void Add(int mat, Geo g, Matrix4x4 m)
        {
            Acc a;
            if (!_mats.TryGetValue(mat, out a)) { a = new Acc(); _mats[mat] = a; }
            Matrix4x4 nm = m.inverse.transpose;
            int b = a.v.Count;
            for (int i = 0; i < g.v.Count; i++)
            {
                a.v.Add(m.MultiplyPoint3x4(g.v[i]));
                a.n.Add(nm.MultiplyVector(g.n[i]).normalized);
                a.t.Add(b + i);
            }
        }

        public Mesh Build(string name, List<int> usedMats)
        {
            int total = 0;
            foreach (var kv in _mats) total += kv.Value.v.Count;
            if (total == 0) return null;
            var keys = new List<int>(_mats.Keys);
            keys.Sort();
            var vs = new List<Vector3>(total);
            var ns = new List<Vector3>(total);
            var tris = new List<int[]>();
            foreach (int k in keys)
            {
                var a = _mats[k];
                int off = vs.Count;
                vs.AddRange(a.v);
                ns.AddRange(a.n);
                var t = new int[a.t.Count];
                for (int i = 0; i < t.Length; i++) t[i] = a.t[i] + off;
                tris.Add(t);
                usedMats.Add(k);
            }
            var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            m.SetVertices(vs);
            m.SetNormals(ns);
            m.subMeshCount = tris.Count;
            for (int i = 0; i < tris.Count; i++) m.SetTriangles(tris[i], i);
            m.RecalculateBounds();
            return m;
        }
    }

    sealed class Trail
    {
        public string name;
        public Pad target;
        public readonly List<Vector2> p = new List<Vector2>();
        public readonly List<float> half = new List<float>();
        public float len;
        public float[] h;   // профиль высоты вдоль тропы, u (только «Выразительный» рельеф, см. LocationGen.Relief.cs)
    }

    sealed class Pad
    {
        public PadDef def;
        public Vector2 pos, fwd, right, entry;
        public float hw, hd, rad;
        public bool round;
        public Trail trail;
        public Vector2 fork, forkTan;
        public bool branch;

        public void Orient(Vector2 f)
        {
            fwd = f.normalized;
            right = new Vector2(fwd.y, -fwd.x);
            entry = pos + fwd * hd;
        }
    }
}
#endif
