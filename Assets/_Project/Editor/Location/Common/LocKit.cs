#if UNITY_EDITOR
// Assets/_Project/Editor/Location/Common/LocKit.cs
// Общие утилиты модулей: папки ассетов, материалы, шейдеры, примитивы (все вписаны в единичный куб).
//
//   Assets/_Game/Location/                 основная сборка (Terrain, Meshes, Materials, Textures)
//   Assets/_Game/Location/Modules/         ассеты модулей, пересоздаются при каждой сборке
//   Assets/_Game/Location/Shaders/         сгенерированные шейдеры (остаются)
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class LocKit
{
    public const string Dir = "Assets/_Game/Location";
    public const string ModulesDir = Dir + "/Modules";
    public const string Batches = ModulesDir + "/Meshes";
    public const string MatDir = ModulesDir + "/Materials";
    public const string ShaderDir = Dir + "/Shaders";

    static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

    // Пакетный режим ассетов: меши и материалы модулей импортируются один раз в конце,
    // а не на каждый CreateAsset (раньше это давало сотни отдельных импортов и лаг).
    static readonly bool UseBatch = true;
    static bool inBatch;

    public static void BeginBatch()
    {
        if (!UseBatch || inBatch) return;
        AssetDatabase.StartAssetEditing();
        inBatch = true;
    }

    public static void EndBatch()
    {
        if (!inBatch) return;
        inBatch = false;
        AssetDatabase.StopAssetEditing();
    }

    // вызывается один раз перед запуском модулей (вне пакетного режима)
    public static void Reset()
    {
        cache.Clear();
        EnsureFolder(Dir);
        AssetDatabase.DeleteAsset(ModulesDir);
        EnsureFolder(Batches);
        EnsureFolder(MatDir);
        EnsureFolder(ShaderDir);
    }

    // ------------------------------------------------------------ assets
    public static T SaveAsset<T>(T obj, string path) where T : UnityEngine.Object
    {
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(obj, path);
        return obj;
    }

    public static void EnsureFolder(string path) { EditorAssets.EnsureFolder(path); }

    public static string ProjectPath(string assetRelative)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetRelative));
    }

    public static Type FindType(string name)
    {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = a.GetType(name);
            if (t != null) return t;
        }
        return null;
    }

    // обратные кавычки в исходнике шейдера превращаются в обычные
    public static Shader EnsureShader(string path, string source)
    {
        string full = ProjectPath(path);
        string src = source.Replace('`', '"');
        if (!File.Exists(full) || File.ReadAllText(full) != src)
        {
            File.WriteAllText(full, src);
            // в пакетном режиме импорт отложен, а шейдер нужен сразу: временно выходим из пакета
            bool was = inBatch;
            if (was) AssetDatabase.StopAssetEditing();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport); if (was) AssetDatabase.StartAssetEditing();
        }
        return AssetDatabase.LoadAssetAtPath<Shader>(path);
    }

    // ------------------------------------------------------------ materials
    // Материалы с одним именем должны быть одним ассетом: SaveAsset удаляет старый файл,
    // и все ранее выданные ссылки на него превращаются в «Missing».
    public static Material Cached(string name)
    {
        Material m;
        return cache.TryGetValue(name, out m) && m != null ? m : null;
    }

    public static void Cache(string name, Material m)
    {
        cache[name] = m;
    }

    public static Material Mat(string name, Color col, float smooth)
    {
        return Mat(name, col, smooth, 0f);
    }

    public static Material Mat(string name, Color col, float smooth, float emission)
    {
        Material m = Cached(name);
        if (m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        m = new Material(sh) { name = name };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", col * emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        m = SaveAsset(m, MatDir + "/" + name + ".mat");
        cache[name] = m;
        return m;
    }

    public static Material MatTransparent(string name, Color col, float smooth)
    {
        var m = Mat(name, col, smooth, 0f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetShaderPassEnabled("ShadowCaster", false);
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------ primitives
    public static Mesh Box()
    {
        var mb = new LocMB();
        var c = new Vector3[8];
        for (int i = 0; i < 8; i++)
            c[i] = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
        int[,] f = { { 0, 1, 3, 2 }, { 4, 6, 7, 5 }, { 0, 2, 6, 4 }, { 1, 5, 7, 3 }, { 0, 4, 5, 1 }, { 2, 3, 7, 6 } };
        for (int k = 0; k < 6; k++)
        {
            Vector3 a = c[f[k, 0]], b = c[f[k, 1]], cc = c[f[k, 2]], d = c[f[k, 3]];
            mb.Tri(a, b, cc, (a + b + cc) / 3f);
            mb.Tri(a, cc, d, (a + cc + d) / 3f);
        }
        return mb.ToMesh("Box");
    }

    // призма/конус высотой 1 (y от -0.5 до 0.5)
    public static Mesh Prism(string name, int n, float rBot, float rTop)
    {
        var mb = new LocMB();
        Vector3 cb = new Vector3(0f, -0.5f, 0f), ct = new Vector3(0f, 0.5f, 0f);
        for (int i = 0; i < n; i++)
        {
            float a0 = i / (float)n * Mathf.PI * 2f, a1 = (i + 1) / (float)n * Mathf.PI * 2f;
            Vector3 b0 = new Vector3(Mathf.Cos(a0) * rBot, -0.5f, Mathf.Sin(a0) * rBot);
            Vector3 b1 = new Vector3(Mathf.Cos(a1) * rBot, -0.5f, Mathf.Sin(a1) * rBot);
            Vector3 t0 = new Vector3(Mathf.Cos(a0) * rTop, 0.5f, Mathf.Sin(a0) * rTop);
            Vector3 t1 = new Vector3(Mathf.Cos(a1) * rTop, 0.5f, Mathf.Sin(a1) * rTop);
            mb.Tri(b0, b1, t1, (b0 + b1 + t1) / 3f);
            if (rTop > 0.0001f)
            {
                mb.Tri(b0, t1, t0, (b0 + t1 + t0) / 3f);
                mb.Tri(ct, t0, t1, Vector3.up);
            }
            mb.Tri(cb, b1, b0, Vector3.down);
        }
        return mb.ToMesh(name);
    }

    public static Mesh Octa()
    {
        var mb = new LocMB();
        Vector3[] p = { new Vector3(0.5f, 0, 0), new Vector3(-0.5f, 0, 0), new Vector3(0, 0.5f, 0), new Vector3(0, -0.5f, 0), new Vector3(0, 0, 0.5f), new Vector3(0, 0, -0.5f) };
        int[,] f = { { 0, 2, 4 }, { 4, 2, 1 }, { 1, 2, 5 }, { 5, 2, 0 }, { 0, 4, 3 }, { 4, 1, 3 }, { 1, 5, 3 }, { 5, 0, 3 } };
        for (int k = 0; k < 8; k++)
        {
            Vector3 a = p[f[k, 0]], b = p[f[k, 1]], c = p[f[k, 2]];
            mb.Tri(a, b, c, (a + b + c) / 3f);
        }
        return mb.ToMesh("Octa");
    }

    // плоское кольцо: uv.x = 0 внутри, 1 снаружи; uv.y = угол
    public static Mesh Ring()
    {
        const int n = 28;
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var t = new List<int>();
        for (int i = 0; i <= n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            v.Add(new Vector3(c * 0.5f, 0f, s * 0.5f)); uv.Add(new Vector2(0f, i / (float)n));
            v.Add(new Vector3(c, 0f, s)); uv.Add(new Vector2(1f, i / (float)n));
        }
        for (int i = 0; i < n; i++)
        {
            int a = i * 2;
            t.Add(a); t.Add(a + 2); t.Add(a + 1);
            t.Add(a + 1); t.Add(a + 2); t.Add(a + 3);
        }
        var m = new Mesh { name = "FoamRing" };
        m.SetVertices(v);
        m.SetUVs(0, uv);
        m.SetTriangles(t, 0);
        var nrm = new Vector3[v.Count];
        for (int i = 0; i < nrm.Length; i++) nrm[i] = Vector3.up;
        m.normals = nrm;
        m.RecalculateBounds();
        return m;
    }

    public static Mesh Flag()
    {
        var mb = new LocMB();
        Vector3 a = new Vector3(0f, 0f, -0.11f), b = new Vector3(0f, 0f, 0.11f), c = new Vector3(0f, -0.28f, 0f);
        mb.Tri(a, b, c, Vector3.right);
        mb.Tri(a, b, c, Vector3.left);
        return mb.ToMesh("Flag");
    }

    public static void AddTube(LocMB mb, Vector3 p0, Vector3 p1, float r0, float r1, int n)
    {
        Vector3 ax = (p1 - p0).normalized;
        Vector3 u = Vector3.Cross(ax, Vector3.up);
        if (u.sqrMagnitude < 1e-4f) u = Vector3.Cross(ax, Vector3.right);
        u.Normalize();
        Vector3 w = Vector3.Cross(ax, u);
        for (int i = 0; i < n; i++)
        {
            float a0 = i / (float)n * Mathf.PI * 2f, a1 = (i + 1) / (float)n * Mathf.PI * 2f;
            Vector3 d0 = u * Mathf.Cos(a0) + w * Mathf.Sin(a0);
            Vector3 d1 = u * Mathf.Cos(a1) + w * Mathf.Sin(a1);
            Vector3 b0 = p0 + d0 * r0, b1 = p0 + d1 * r0, t0 = p1 + d0 * r1, t1 = p1 + d1 * r1;
            Vector3 o = d0 + d1;
            mb.Tri(b0, b1, t1, o);
            mb.Tri(b0, t1, t0, o);
            mb.Tri(p0, b1, b0, -ax);
            mb.Tri(p1, t0, t1, ax);
        }
    }

    // икосаэдр с одним подразбиением (80 граней), плоские грани
    public static Mesh Ico(string name, int seed, Func<Vector3, int, Vector3> deform)
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        Vector3[] iv =
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
        };
        for (int i = 0; i < iv.Length; i++) iv[i] = iv[i].normalized;
        int[] fi =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };
        var mb = new LocMB();
        Action<Vector3, Vector3, Vector3> tri = (p, q, r) =>
        {
            Vector3 dp = deform(p, seed), dq = deform(q, seed), dr = deform(r, seed);
            mb.Tri(dp, dq, dr, (dp + dq + dr) / 3f);
        };
        for (int i = 0; i < fi.Length; i += 3)
        {
            Vector3 a = iv[fi[i]], b = iv[fi[i + 1]], c = iv[fi[i + 2]];
            Vector3 ab = ((a + b) * 0.5f).normalized, bc = ((b + c) * 0.5f).normalized, ca = ((c + a) * 0.5f).normalized;
            tri(a, ab, ca);
            tri(b, bc, ab);
            tri(c, ca, bc);
            tri(ab, bc, ca);
        }
        return mb.ToMesh(name);
    }

    public static Vector3 RockDeform(Vector3 p, int seed)
    {
        float n = Mathf.PerlinNoise(p.x * 1.6f + seed * 7.3f + 5f, p.z * 1.6f + p.y * 1.3f + 5f);
        Vector3 q = p * (0.78f + n * 0.5f);
        q.y *= 0.72f;
        if (q.y < -0.30f) q.y = -0.30f;
        return q;
    }
}
#endif