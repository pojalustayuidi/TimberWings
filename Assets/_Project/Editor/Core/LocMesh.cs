#if UNITY_EDITOR
// Assets/_Project/Editor/Island/Core/LocMesh.cs
// LocMB: простой построитель меша с плоскими гранями. LocBatcher: слияние мешей по ячейкам и материалам.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class LocMB
{
    public readonly List<Vector3> v = new List<Vector3>();
    public readonly List<int> t = new List<int>();

    // Треугольник; нормаль гарантированно смотрит в сторону outward.
    public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        if (n.sqrMagnitude < 1e-14f) return;
        if (Vector3.Dot(n, outward) < 0f) { Vector3 tmp = b; b = c; c = tmp; }
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    public Mesh ToMesh(string name)
    {
        var m = new Mesh { name = name };
        m.indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}

public sealed class LocBatcher
{
    readonly float cell;
    readonly Dictionary<string, List<CombineInstance>> items = new Dictionary<string, List<CombineInstance>>();
    readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    // cell > 1e5: всё в одну ячейку
    public LocBatcher(float cell) { this.cell = cell; }

    public void Add(Mesh m, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        if (m == null || mat == null) return;
        int cx = cell > 1e5f ? 0 : Mathf.FloorToInt(pos.x / cell);
        int cz = cell > 1e5f ? 0 : Mathf.FloorToInt(pos.z / cell);
        string key = ("c" + cx + "_" + cz + "_" + mat.name).Replace(' ', '_').Replace('-', 'n');
        List<CombineInstance> l;
        if (!items.TryGetValue(key, out l))
        {
            l = new List<CombineInstance>();
            items[key] = l;
            mats[key] = mat;
        }
        var ci = new CombineInstance();
        ci.mesh = m;
        ci.transform = Matrix4x4.TRS(pos, rot, scale);
        l.Add(ci);
    }

    public int Flush(Transform parent, string baseName, bool shadows)
    {
        int n = 0;
        foreach (var kv in items)
        {
            string nm = baseName + "_" + kv.Key;
            var mesh = new Mesh { name = nm, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(kv.Value.ToArray(), true, true);
            mesh.RecalculateBounds();
            mesh = LocKit.SaveAsset(mesh, LocKit.Batches + "/" + nm + ".asset");
            var go = new GameObject(nm);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mats[kv.Key];
            if (!shadows)
            {
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);
            n++;
        }
        items.Clear();
        mats.Clear();
        return n;
    }
}
#endif