// Assets/_Project/Scripts/Resource/TreeMeshFactory.cs (НЕ в Editor)
// Три вида дерева (молодое, зрелое, старое) и пень, генерируются в рантайме: ассеты, префабы и редактор не нужны.
// Включается флагом ResourceItem.useGeneratedModel (по умолчанию GameBalance.GeneratedTreeModels). Выключишь: останется твой префаб.
//
// Что делает Apply(item):
//   - снимает со ствола и детей старые MeshRenderer/MeshFilter (коллайдеры и скрипты остаются);
//   - добавляет дочерний «GenModel» с мешем нужного размера (2 сабмеша: кора, листва);
//   - если у дерева нет коллайдера, добавляет капсулу.
// Меши граненые (flat shading), по 2 материала на дерево. Материалы берутся из активного render pipeline (URP Lit).
// Листва называется «GenTreeLeaf…», поэтому TreeSizing.ApplyTone красит её по размеру.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class TreeMeshFactory
{
    static Mesh[] treeMesh = new Mesh[3];
    static Mesh stumpMesh;
    static Material bark, leaf, cut;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        treeMesh = new Mesh[3];
        stumpMesh = null;
        bark = leaf = cut = null;
    }

    // ------------------------------------------------------------ применить к дереву
    public static Renderer Apply(ResourceItem item)
    {
        var root = item.transform;

        // 1) старые меши долой (DestroyImmediate: подсветка в первом же кадре не должна увидеть старый MeshFilter)
        var olds = item.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < olds.Length; i++)
        {
            if (olds[i] == null) continue;
            var go = olds[i].gameObject;
            var mf = go.GetComponent<MeshFilter>();
            Object.DestroyImmediate(olds[i]);
            if (mf != null) Object.DestroyImmediate(mf);
        }

        // 2) новая модель
        var g = new GameObject("GenModel");
        g.layer = root.gameObject.layer;
        g.transform.SetParent(root, false);
        g.AddComponent<MeshFilter>().sharedMesh = TreeMesh(item.Size);
        var r = g.AddComponent<MeshRenderer>();
        r.sharedMaterials = new[] { Bark(), Leaf() };

        // 3) коллайдер, если префаб был без него (иначе рубить будет нечего)
        if (item.GetComponentInChildren<Collider>(true) == null)
        {
            var cap = item.gameObject.AddComponent<CapsuleCollider>();
            cap.direction = 1;
            cap.radius = 0.55f;
            cap.height = 3.8f;
            cap.center = new Vector3(0f, 1.9f, 0f);
        }
        return r;
    }

    // пень: тот же материал коры + светлый срез. scale = множитель размера дерева
    public static GameObject CreateStump(Vector3 pos, Quaternion rot, float scale, int layer)
    {
        var g = new GameObject("GenStump");
        g.layer = layer;
        g.transform.SetPositionAndRotation(pos, rot);
        g.transform.localScale = Vector3.one * scale;
        g.AddComponent<MeshFilter>().sharedMesh = StumpMesh();
        g.AddComponent<MeshRenderer>().sharedMaterials = new[] { Bark(), Cut() };
        TreeCollision.AddStumpCollider(g);   // пень сплошной: сквозь него не пройти
        return g;
    }

    // ------------------------------------------------------------ материалы
    static Material MakeMat(string name, Color c, float smooth)
    {
        Shader sh = null;
        var rp = GraphicsSettings.currentRenderPipeline;
        if (rp != null && rp.defaultMaterial != null) sh = rp.defaultMaterial.shader;
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");

        var m = new Material(sh) { name = name };
        m.enableInstancing = true;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        return m;
    }

    static Material Bark() { if (bark == null) bark = MakeMat("GenTreeBark", new Color(0.42f, 0.28f, 0.16f), 0.05f); return bark; }
    static Material Leaf() { if (leaf == null) leaf = MakeMat("GenTreeLeaf", new Color(0.30f, 0.68f, 0.24f), 0.05f); return leaf; }
    static Material Cut() { if (cut == null) cut = MakeMat("GenTreeCut", new Color(0.78f, 0.58f, 0.34f), 0.05f); return cut; }

    // ------------------------------------------------------------ меши
    public static Mesh TreeMesh(TreeSize s)
    {
        int i = (int)s;
        if (treeMesh[i] != null) return treeMesh[i];

        var b = new MB();
        switch (s)
        {
            case TreeSize.Young:
                // тонкий ствол, одна светлая «шапка» и маленькая боковая
                b.Frustum(0, new Vector3(0f, -0.1f, 0f), 0.13f, new Vector3(0.05f, 1.4f, 0f), 0.08f, 6);
                b.Blob(1, new Vector3(0.05f, 1.95f, 0f), new Vector3(0.80f, 0.75f, 0.80f), 11);
                b.Blob(1, new Vector3(-0.35f, 1.55f, 0.20f), new Vector3(0.45f, 0.40f, 0.45f), 12);
                break;

            case TreeSize.Mature:
                // средний ствол, крона из трёх шаров
                b.Frustum(0, new Vector3(0f, -0.1f, 0f), 0.24f, new Vector3(0.05f, 2.1f, 0f), 0.14f, 7);
                b.Blob(1, new Vector3(0f, 2.50f, 0f), new Vector3(1.45f, 1.00f, 1.45f), 21);
                b.Blob(1, new Vector3(0.10f, 3.35f, 0f), new Vector3(1.05f, 0.90f, 1.05f), 22);
                b.Blob(1, new Vector3(0.75f, 2.95f, 0.30f), new Vector3(0.75f, 0.65f, 0.75f), 23);
                break;

            default:
                // толстый ствол с корневым расширением, сук и широкая раскидистая крона
                b.Frustum(0, new Vector3(0f, -0.1f, 0f), 0.62f, new Vector3(0f, 0.5f, 0f), 0.42f, 8);
                b.Frustum(0, new Vector3(0f, 0.5f, 0f), 0.42f, new Vector3(0.05f, 2.4f, 0f), 0.24f, 8);
                b.Frustum(0, new Vector3(0f, 1.6f, 0f), 0.13f, new Vector3(0.95f, 2.3f, 0.2f), 0.08f, 5);
                b.Blob(1, new Vector3(0f, 2.90f, 0f), new Vector3(1.65f, 1.05f, 1.60f), 31);
                b.Blob(1, new Vector3(-0.85f, 3.25f, 0.40f), new Vector3(1.05f, 0.85f, 1.05f), 32);
                b.Blob(1, new Vector3(0.90f, 3.25f, -0.30f), new Vector3(1.10f, 0.90f, 1.05f), 33);
                b.Blob(1, new Vector3(0f, 3.95f, 0f), new Vector3(0.95f, 0.80f, 0.95f), 34);
                break;
        }
        treeMesh[i] = b.Build("GenTree_" + s);
        return treeMesh[i];
    }

    public static Mesh StumpMesh()
    {
        if (stumpMesh != null) return stumpMesh;
        var b = new MB();
        b.Frustum(0, new Vector3(0f, -0.05f, 0f), 0.52f, new Vector3(0f, 0.45f, 0f), 0.42f, 8);
        b.Cap(1, new Vector3(0f, 0.45f, 0f), 0.42f, 8);
        stumpMesh = b.Build("GenStump");
        return stumpMesh;
    }

    // ------------------------------------------------------------ построитель граней (flat shading, 2 сабмеша)
    class MB
    {
        readonly List<Vector3> v = new List<Vector3>(512), n = new List<Vector3>(512);
        readonly List<int>[] t = { new List<int>(1024), new List<int>(1024) };

        static List<Vector3> icoV;
        static List<int> icoF;

        public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 nn = Vector3.Cross(b - a, c - a).normalized;
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            n.Add(nn); n.Add(nn); n.Add(nn);
            t[sub].Add(i); t[sub].Add(i + 1); t[sub].Add(i + 2);
        }

        // усечённый конус между двумя точками оси (боковая стенка, без днищ)
        public void Frustum(int sub, Vector3 p0, float r0, Vector3 p1, float r1, int sides)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 A0 = p0 + d0 * r0, A1 = p0 + d1 * r0, B0 = p1 + d0 * r1, B1 = p1 + d1 * r1;
                Tri(sub, A0, B0, B1);
                Tri(sub, A0, B1, A1);
            }
        }

        // плоская верхняя крышка (нормаль вверх)
        public void Cap(int sub, Vector3 c, float r, int sides)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 P0 = c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r;
                Vector3 P1 = c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r;
                Tri(sub, c, P1, P0);
            }
        }

        // «пухлый» шар: икосфера 80 граней, радиус каждой вершины слегка гуляет, низ приплюснут
        public void Blob(int sub, Vector3 center, Vector3 radii, int seed)
        {
            BuildIco();
            var p = new Vector3[icoV.Count];
            for (int i = 0; i < p.Length; i++)
            {
                Vector3 u = icoV[i];
                float j = 0.86f + 0.26f * H(seed, i);
                if (u.y < 0f) u.y *= 0.65f;
                p[i] = center + Vector3.Scale(u * j, radii);
            }
            for (int f = 0; f < icoF.Count; f += 3)
            {
                Vector3 a = p[icoF[f]], b = p[icoF[f + 1]], c = p[icoF[f + 2]];
                // ориентация наружу независимо от порядка обхода в таблице
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - center) < 0f) { Vector3 tmp = b; b = c; c = tmp; }
                Tri(sub, a, b, c);
            }
        }

        static float H(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        static void BuildIco()
        {
            if (icoV != null) return;
            float k = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var vs = new List<Vector3>
            {
                new Vector3(-1, k, 0), new Vector3(1, k, 0), new Vector3(-1, -k, 0), new Vector3(1, -k, 0),
                new Vector3(0, -1, k), new Vector3(0, 1, k), new Vector3(0, -1, -k), new Vector3(0, 1, -k),
                new Vector3(k, 0, -1), new Vector3(k, 0, 1), new Vector3(-k, 0, -1), new Vector3(-k, 0, 1)
            };
            for (int i = 0; i < vs.Count; i++) vs[i] = vs[i].normalized;
            int[] f0 =
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            var fs = new List<int>(240);
            var mid = new Dictionary<long, int>();
            System.Func<int, int, int> Mid = (a, b) =>
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                int m;
                if (mid.TryGetValue(key, out m)) return m;
                vs.Add(((vs[a] + vs[b]) * 0.5f).normalized);
                m = vs.Count - 1;
                mid[key] = m;
                return m;
            };
            for (int f = 0; f < f0.Length; f += 3)
            {
                int a = f0[f], b = f0[f + 1], c = f0[f + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                fs.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            icoV = vs;
            icoF = fs;
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetNormals(n);
            m.subMeshCount = 2;
            m.SetTriangles(t[0], 0);
            m.SetTriangles(t[1], 1);
            m.RecalculateBounds();
            return m;
        }
    }
}
